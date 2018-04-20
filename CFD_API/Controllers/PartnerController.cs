using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON;
using CFD_COMMON.Azure;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System.Web;
using System.Drawing;
using System.ServiceModel;
using AyondoTrade.Model;
using CFD_COMMON.Utils.Extensions;
using System.Threading.Tasks;
using AyondoTrade.FaultModel;
using EntityFramework.Extensions;
using Newtonsoft.Json;
using ServiceStack.Text;
using System.Data.SqlTypes;
using System.Text;
using CFD_COMMON.IdentityVerify;
using System.Text.RegularExpressions;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/partner")]
    public class PartnerController : CFDController
    {
        private static readonly TimeSpan VERIFY_CODE_PERIOD = TimeSpan.FromHours(1);
        //private string[] codeArray = new string[62] { "0","1","2","3","4","5","6","7","8","9","a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"};
        /// <summary>
        /// 一级合伙人推荐码长度为3
        /// </summary>
        private int FirstLevelCodeLength = 3;
        /// <summary>
        /// 二级合伙人推荐码长度为5
        /// </summary>
        private int SecondLevelCodeLength = 5;
        private int ThirdLevelCodeLength = 7;

        public PartnerController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPost]
        [Route("login")]
        public PartnerDTO Login(PartnerLoginDTO form)
        {
            PartnerDTO dto = new PartnerDTO() { success = true };
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            if(verifyCodes.Any())
            {
                var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
                if(partner != null)
                {
                    dto = Mapper.Map<PartnerDTO>(partner);
                }
            }
            else
            {
                dto.success = false;
                dto.message = "验证码错误";
            }

            return dto;
        }

        [HttpPost]
        [Route("signup")]
        public ResultDTO SignUp(PartnerSignUpDTO form)
        {
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;

            if (!string.IsNullOrEmpty(form.partnerCode) && (form.partnerCode.Length < FirstLevelCodeLength || form.partnerCode.Length > ThirdLevelCodeLength))
            {
                return new ResultDTO() { success = false, message = "推荐码格式错误" };
            }

            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            if(!verifyCodes.Any())
                return new ResultDTO() { success = false, message = "验证码错误" };

            var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
            if(partner != null)
            {
                return new ResultDTO() { success = false, message = "该手机号已注册过合作人" };
            }

            #region 如果已经使用合伙人推荐码注册过App User,就用该PartnerCode生成合伙人
            var appUser = db.Users.FirstOrDefault(u => u.Phone == form.phone);
            if(appUser != null && !string.IsNullOrEmpty(appUser.PromotionCode))
            {
                var appPartner = db.Partners.FirstOrDefault(p => p.PromotionCode == appUser.PromotionCode);
                if(appPartner != null)
                {
                    form.partnerCode = appPartner.PartnerCode;
                }
            }
            #endregion

            //传入的合伙人码只能是一级或二级合伙人
            //以传入的合伙人码作为上级合伙人，生成下级合伙人码
            if (!string.IsNullOrEmpty(form.partnerCode) && form.partnerCode.Length != FirstLevelCodeLength && form.partnerCode.Length != SecondLevelCodeLength)
            {
                return new ResultDTO() { success = false, message = "合伙人码格式错误" };
            }

            //获取上级合伙人
            Partner parentPartner = null;
            if(!string.IsNullOrEmpty(form.partnerCode))
            {
                parentPartner = db.Partners.FirstOrDefault(p => p.PartnerCode == form.partnerCode);
                if(parentPartner == null)
                {
                    return new ResultDTO() { success = false, message = "合伙人码不存在" };
                }
            }

            string partnerCode = GetSubPartnerCode(form.partnerCode);
            int count = 0;
            while (db.Partners.Any(p => p.PartnerCode == partnerCode) && count <= 20)
            {
                count++;
                if (count == 20)
                {
                    return new ResultDTO() { success = false, message = "合伙人码创建失败" };
                }
                partnerCode = GetSubPartnerCode(form.partnerCode);
            }
            count = 0;
            string promotionCode = GetPromotionCode();
            while(db.Partners.Any(p => p.PromotionCode == promotionCode) && count <= 20)
            {
                count++;
                if (count == 20)
                {
                    return new ResultDTO() { success = false, message = "推荐码创建失败" };
                }
                promotionCode = GetPromotionCode();
            }

            //如果该手机号已经通过App注册过推荐码，就用自己的推荐码更新Promotion Code
            if (appUser != null)
            {
                appUser.PromotionCode = promotionCode;
                db.SaveChanges();
            }

            #region 保存合伙人记录
            partner = Mapper.Map<Partner>(form);
            partner.CreatedAt = DateTime.UtcNow;
            partner.RootCode = string.IsNullOrEmpty(form.partnerCode) ? partnerCode : form.partnerCode.Substring(0, 3);
            if (!string.IsNullOrEmpty(form.partnerCode))
            {
                partner.ParentCode = form.partnerCode;
            }

            partner.PartnerCode = partnerCode;
            partner.PromotionCode = promotionCode;
            partner.isAdmin = false;
            db.Partners.Add(partner);
            db.SaveChanges();
            #endregion

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("refer/{promotionCode}/{phone}/{verifyCode}")]
        public ResultDTO Refer(string promotionCode, string phone, string verifyCode)
        {
            #region 验证参数
            if(string.IsNullOrEmpty(promotionCode))
            {
                return new ResultDTO() { success = false, message = "缺少推荐码" };
            }

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(verifyCode))
            {
                return new ResultDTO() { success = false, message = "手机号为空/验证码" };
            }

            var misc = db.Miscs.FirstOrDefault(m => m.Key == "PhoneRegex");
            if (misc != null)
            {
                Regex regex = new Regex(misc.Value);
                if (!regex.IsMatch(phone))
                {
                    return new ResultDTO() { success = false, message = "手机号格式不正确" };
                }
            }

            if (db.PartnerReferHistorys.Any(o => o.FriendPhone == phone) || db.Users.Any(u=>u.Phone == phone))
            {
                return new ResultDTO() { success = false, message = "该手机号已被邀请/注册过哟！" };
            }

            var dtValidSince = DateTime.UtcNow.AddHours(-1);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == phone && o.Code == verifyCode && o.SentAt > dtValidSince);
            if (string.IsNullOrEmpty(verifyCode) || !verifyCodes.Any())
            {
                return new ResultDTO() { success = false, message = "输入的验证码不正确" };
            }

            var partner = db.Partners.FirstOrDefault(p=>p.PromotionCode == promotionCode);
            if(partner == null)
            {
                return new ResultDTO() { success = false, message = "推荐码错误" };
            }
            #endregion

            #region 添加到合伙人好友推荐表
            db.PartnerReferHistorys.Add(new PartnerReferHistory() {  RefereePhone = partner.Phone, FriendPhone = phone, CreatedAt = DateTime.UtcNow });
            #endregion

            #region 模拟盘开户
            var userService = new UserService(db);
            userService.CreateUserByPhone(phone);

            var user = db.Users.FirstOrDefault(o => o.Phone == phone);

            var nickname = "u" + user.Id.ToString("000000");
            user.Nickname = nickname;

            //check duplicate nickname and generate random suffix
            int tryCount = 0;
            while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
            {
                user.Nickname = nickname.TruncateMax(4) + Randoms.GetRandomAlphabeticString(4);

                tryCount++;

                if (tryCount > 10)
                {
                    CFDGlobal.LogWarning("Tryout exceeded: signupByPhone - check duplicate nickname and generate random suffix");
                    break;
                }
            }

            user.PromotionCode = promotionCode;
            #endregion

            db.SaveChanges();
            return new ResultDTO() { success = true };
        }

        /// <summary>
        /// 根据传入的合伙人码，生成下级合伙人码
        /// </summary>
        /// <param name="parentCode"></param>
        /// <returns></returns>
        private string GetSubPartnerCode(string parentCode)
        {
            int codeLength = 3;
            string subCode = string.Empty;
            if (string.IsNullOrEmpty(parentCode))
            {
                codeLength = 3;
            }
            else
            {
                switch (parentCode.Length)
                {
                    case 3: codeLength = 5; break;
                    case 5: codeLength = 7; break;
                    default: codeLength = 3; break;
                }
            }

            subCode = parentCode;

            int number;
            Random random = new Random();
            for (int i = 0; i < codeLength - parentCode.Length; i++)
            {
                number = random.Next(100);
                switch (number % 2)
                {
                    case 0:
                        subCode += ((char)('0' + (char)(number % 10))).ToString();
                        break;
                    case 1:
                        subCode += ((char)('A' + (char)(number % 26))).ToString();
                        break;
                    default:
                        break;
                }
            }
            
            return subCode;
        }

        /// <summary>
        /// 生成4位的推荐码
        /// </summary>
        /// <returns></returns>
        private string GetPromotionCode()
        {
            string code = string.Empty;

            int number;
            Random random = new Random();
            for (int i = 0; i < 4; i++)
            {
                number = random.Next(100);
                switch (number % 2)
                {
                    case 0:
                        code += ((char)('0' + (char)(number % 10))).ToString();
                        break;
                    case 1:
                        code += ((char)('A' + (char)(number % 26))).ToString();
                        break;
                    default:
                        break;
                }
            }

            return code;
        }
        [HttpGet]
        [Route("report")]
        public PartnerReportDTO GetPartnerReport(string partnerCode = "", string from = "", string to = "", string phone = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerView> query = db.PartnerViews;
            if (string.IsNullOrEmpty(partnerCode))
            {
                //get the level 1 partners
                query = query.Where(pv => pv.ParentCode == null && pv.RootCode == pv.PartnerCode);
            }
            else
            {
                //get the sub level partners
                query = query.Where(pv => pv.ParentCode == partnerCode);                  
            }
            //both from and to are provided..
            if ((string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) == false)
            {
                DateTime fromDate = DateTime.Parse(from);
                DateTime toDate = DateTime.Parse(to);
                query = query.Where(pv => pv.PartnerCreatedAt >= fromDate && pv.PartnerCreatedAt <= toDate);
            }
            //if the phone number is provided:
            if ((string.IsNullOrEmpty(phone)) == false)
            {
                query = query.Where(pv => pv.Phone == phone);
            }

            int count = query.Count();

            query = query.OrderByDescending(pv => pv.PartnerCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerReportRecordDTO>  records =  Mapper.Map<List<PartnerReportRecordDTO>>(query.ToList());
            return new PartnerReportDTO() { TotalCount = count, Records = records };
        }
       

        [HttpGet]
        [Route("userreport")]
        public PartnerUserReportDTO GetPartnerUserReport(string partnerCode = "", string from = "", string to = "", string phone = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerUserView> query = db.PartnerUserViews;
            //if partnerCode is provided
            if ((string.IsNullOrEmpty(partnerCode)) == false)            
            {
                //get users according partner code recursively;                  
                query = query.Where(puv => puv.PartnerCode.StartsWith(partnerCode));
            }

            //both from and to are provided..
            if ((string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) == false)
            {
                DateTime fromDate = DateTime.Parse(from);
                DateTime toDate = DateTime.Parse(to);
                query = query.Where(puv => puv.UserCreatedAt >= fromDate && puv.UserCreatedAt <= toDate);
            }
            //if the phone number is provided:
            if ((string.IsNullOrEmpty(phone)) == false)
            {
                query = query.Where(puv => puv.Phone == phone);
            }

            int count = query.Count();
            query = query.OrderByDescending(puv => puv.UserCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerUserReportRecordDTO> records = Mapper.Map<List<PartnerUserReportRecordDTO>>(query.ToList());
            return new PartnerUserReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("report/all")]
        [IPAuth]
        public string GetPartnerReport()
        {
            // Assumes that connection is a valid SqlConnection object.  
            string queryString =
              @" 
 ------ 所有入金用户  drop table #a drop table #b drop table #c
 select  * into #a from (
select sum(amount) deposite, accountId, u.Nickname,u.id, a.Timestamp, u.AyLiveApproveAt
 from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId 
where (transfertype= 'WeCollect - CUP' or transfertype = 'Adyen - Skrill' or (transfertype='bank wire' and amount>=0 and len(transferid)=36) )
--and u.id =2026
 and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052)
group by AccountId, u.Nickname,u.id,a.Timestamp,u.AyLiveApproveAt
 ) a order by deposite desc

 --select * from #a
   -- drop table #b
 ---  查询合伙人信息
SELECT [UserId]   -- layer2,3 etc
      ,p.[NickName]
      ,[AyliveUsername]
      ,[TradeCount]
      ,[PartnerCode]
      ,[OcrRealName]
      ,[IsDeposit]
      ,[Amount]
      ,[Name] as layer1
      ,[ParentCode]
      ,[RootCode], deposite,#a.id  -- partner id
	  ,#a.timestamp, AyLiveApproveAt
	    into #b
  FROM [CFD].[dbo].[PartnerUserView] p join #a on #a.id = p.UserId
  where --PartnerCode = 'b44' and
   IsDeposit = 'true'  and userid > 3264 and userid not in (3290,3590) and userid is not null 
 
   -- select * from #b  
   --分别得到介绍来的人的uid,和充值金额 
   select (userid) , layer1, sum(deposite) 充值金额  from #b where OcrRealName <> layer1 group by layer1,userid order by layer1 
    --得到介绍来的人的总充值金额 
   select  layer1, sum(deposite) 充值金额  from #b where OcrRealName <> layer1 group by layer1
   ---- Layer1, Layer2 数目
      select layer1 合伙人1级, count(distinct userid) 合伙人2级  from #b where OcrRealName <> layer1 group by layer1 --得到介绍来的人的uid,和充值金额

	----每个用户充值总数
 --  select count(userid) 每个用户充值笔数, layer1, sum(deposite) depositeSum,userid   from #b where OcrRealName <> layer1 group by userid,layer1
   
   --select count(userid) 每个用户充值笔数, layer1, sum(deposite),userid   from #b where OcrRealName <> layer1 and layer1 = N'戴燕群'  group by userid,layer1
   ----union all
   ----select layer1, sum(deposite) SelfDeposite from (select layer1,deposite  from #b where OcrRealName <> layer1 ) a 
   
   ----------------  合伙人的平均资金交易量和 非合伙人平均 之比
   select avg(investUSD*leverage)  from  [NewPositionHistory_live]   where userid in ( select distinct userid  from #b where OcrRealName <> layer1 )
   
  select avg(investUSD*leverage) from [NewPositionHistory_live]  where (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) 
  and userid not in ( select distinct userid  from #b where OcrRealName <> layer1 )
 ----------------  合伙人的总资金交易量和所有人 之比
   select sum(investUSD*leverage)  from  [NewPositionHistory_live]   where userid in ( select distinct userid  from #b where OcrRealName <> layer1 )
   or userid in (3914,12566, 3807)  
 
  select sum(investUSD*leverage) from [NewPositionHistory_live]  where (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) 
  and (userid not in ( select distinct userid  from #b where OcrRealName <> layer1 ) and userid not in (3914,12566, 3807))
  -- select 7895315.0000000000000/150913235.0000000000000 
   select sum(investUSD*leverage) from [NewPositionHistory_live]  where (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) 
  and (userid not in ( select distinct userid  from #b where OcrRealName <> layer1 ))
  /*
  ----------------------  合伙人的平均留存时间和非合伙人 之比。
  --- 平均活跃时间：80%的用户在此时间范围内活跃，之后不在交易。从注册时间到最后交易结束
	declare @startTime datetime = dateadd(month, -1,getdate()), @endTime datetime =  getdate()  
	-- select distinct userid into #d from [NewPositionHistory_live] where CreateTime is not null and ClosedAt is null

	 select avg(datediff(day, u.createdAt, n.closedat)) from [user] u   join ( select max(closedat) closedat, userid from [NewPositionHistory_live] 
	  group by userid having max(closedat) < @startTime ) n on u.id = n.userid where AyLiveAccountId is not null and (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) --and userid not in (select userid from #d)
	and (ClosedAt < @startTime  ) and userid in ( select distinct userid  from #b where OcrRealName <> layer1 )
	--32 days
	select avg(datediff(day, u.createdAt, n.closedat)) from [user] u   join ( select max(closedat) closedat, userid from [NewPositionHistory_live] 
	  group by userid having max(closedat) < @startTime ) n on u.id = n.userid where AyLiveAccountId is not null and (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) --and userid not in (select userid from #d)
	and (ClosedAt < @startTime  )  and userid not in ( select distinct userid  from #b where OcrRealName <> layer1 )
	*/

	------------------ 根据 id card 追踪 出金人是否在 合伙人中，交易笔数，注册时间,入金数目
	--  drop table #c
	select userid, LastName+FirstName name, IdCode,email,addr,EmpStatus into #c from UserInfo where IdCode in ('330702199602234428', '330781199501254513','320123198808064025', '330381199901071431',
	'330302199802071617', '33018419981031351X', '330227199811274258', '330724199802230737','23080219980302033X', '330282199808108245','33028219980323500X','330621199709221524','330381198912044135','33068319971209561X','33068319980611202X','331004199703180954','330724199804154520','33062419970416112X','330304199711161521','330324199804175880','330621199704288420','330127199712235020','330382199801195329','330726199612212530'
	,'341221199605162305', '330182198705053114', '342426199510144618', '330825199508212417', '330723199203050615', '511529199906176216', '43100219980814503X', '330723199103240614', '330723199804075375', '362527198803215716', '341282199612154912', '61242719870718061X','330721198102115419', '431002199712255015', '411424199805217129',  '330724199111162915', '362330199309046816', '370902198011040916', '360731199703070028', '330324199807077792', '331082199707158104', '330723199404042160', '330227199609268516', '330724199806176918', '33072419971205002X',
	 '522123199808231544', '331004199801110925')

	-- select userid, LastName+FirstName name, IdCode,email,addr,EmpStatus into #c from UserInfo where IdCode in ('33068319971209561X','33068319980611202X','331004199703180954','330724199804154520','33062419970416112X','330304199711161521','330324199804175880','330621199704288420','330127199712235020','330382199801195329','330726199612212530')

	 --  select distinct #b.userid,#b.*  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1 
	   --1. report version 出金用户信息以及合伙人：
	   select distinct #b.userid,nickname, #b.layer1  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1
	   
	   --select * from  [NewPositionHistory_live] where userid in (
	   --select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1)

	   -----这些用户交易数据avg： 
	      select sum(pl) TotalPL, avg(datediff(MINUTE, CreateTime, closedat)) avgHoldMINUTE,  sum(InvestUSD * leverage) / sum(InvestUSD) avgLeverage, avg(investusd) avgInvestUSD , count(*) tradeNum from  [NewPositionHistory_live] where userid in (
	   select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1) 

	   -- details， 按用户分组最大持仓时间分布，单位 分钟:
	   select max(datediff(SECOND, CreateTime, closedat)) 持仓时间,userid,count(*) 交易笔数,sum(pl) TotalPL, avg(datediff(MINUTE, CreateTime, closedat)) 平均持有时间min,  sum(InvestUSD * leverage) / sum(InvestUSD) 平均杠杆, avg(investusd) 平均交易本金  from  [NewPositionHistory_live] where userid in (
	   select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1)  group by userid order by userid 
	   --- 交易品种:
	    -- select * from [NewPositionHistory_live] 
	   select  count(*) tradeNum,userid,p.Cname from  [NewPositionHistory_live] n join  ProductList_live p on SecurityId = p.id where userid in (
	   select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1)  group by userid,p.Cname order by userid 
	   
	   --select count(userid), userid from  [NewPositionHistory_live] where userid in (
	   --select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1)  group by userid
	   -----2 special men:
	   --select sum(pl) TotalPL, avg(datediff(MINUTE, CreateTime, closedat)) avgHoldTime,  sum(InvestUSD * leverage) / sum(InvestUSD) avgLeverage, avg(investusd) avgInvestUSD , count(*) tradeNum from  [NewPositionHistory_live] where userid in (12626, 12718)

	   --select * from [NewPositionHistory_live] where userid  in (12626, 12718)

	   ----  这些出金用户出入金数量和时间分布 
	   select deposite 入金数量, a.Amount 出金数量, Nickname, #a.id as userID, #a.Timestamp 入金时间, a.Timestamp 出金时间, datediff(day,#a.Timestamp,a.Timestamp) 出入金间隔天数 from #a left join (
select  u.id, a.Timestamp, a.Amount from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId  
where  transfertype= 'eft'   -- or ( transfertype='bank wire' and amount>=0 and len(transferid)=36 and u.id = 2026)
and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052) 
 ) a on #a.id = a.id
	where #a.id in ( select distinct #b.userid  from #b join #c on #b.UserId= #c.UserId where OcrRealName <> layer1) order by userid


--	   -------------------- 查询其他合伙人是否在出金，以及交易情况
	   
--   select (userid) , layer1   from #b where OcrRealName <> layer1 
--   and layer1 = N'戴燕群' and [IsDeposit] = 'true'  and userid in (
--select  u.id from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId  
--where  transfertype= 'eft'   -- or ( transfertype='bank wire' and amount>=0 and len(transferid)=36 and u.id = 2026)
--and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052) 
-- ) and userid not in (select userid from #c)     --user：　12626　，　12718  14974


--select * from [user] where id = 14561

-----------------------   query if user reincharge again after withdraw
--select sum(amount) deposite, accountId, u.Nickname,u.id, a.Timestamp from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId join (
--select userid from UserInfo where IdCode in (
--'330702199602234428', '330781199501254513','320123198808064025', '330381199901071431',
--	'330302199802071617', '33018419981031351X', '330227199811274258', '330724199802230737','23080219980302033X', '330282199808108245','33028219980323500X','330621199709221524','330381198912044135','33068319971209561X','33068319980611202X','331004199703180954','330724199804154520','33062419970416112X','330304199711161521','330324199804175880','330621199704288420','330127199712235020','330382199801195329','330726199612212530',   --below is 2nd batch withdraw
--	'341221199605162305', '330182198705053114', '342426199510144618', '330825199508212417', '330723199203050615', '511529199906176216', '43100219980814503X', '330723199103240614', '330723199804075375', '362527198803215716', '341282199612154912', '61242719870718061X','330721198102115419', '431002199712255015', '411424199805217129',  '330724199111162915', '362330199309046816', '370902198011040916', '360731199703070028', '330324199807077792', '331082199707158104', '330723199404042160', '330227199609268516', '330724199806176918', '33072419971205002X', '522123199808231544', '331004199801110925' ) ) b on u.id = b.userid
--where (transfertype= 'WeCollect - CUP' or transfertype = 'Adyen - Skrill' or (transfertype='bank wire' and amount>=0 and len(transferid)=36) )
----and u.id =2026
-- and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052)
--group by AccountId, u.Nickname,u.id,a.Timestamp order by Timestamp desc

--- 根据所有合伙人userid 【 select distinct userid  from #b where OcrRealName <> layer1 group by layer1,userid 】得到相应最后交易信息
    select  #b.userid, count(n.id) tradeCount, max(ClosedAt) LastTradeTime  from #b left join [NewPositionHistory_live] n on #b.userid = n.UserId where OcrRealName <> layer1 and #b.userid not in (select userid from #c) group by layer1,#b.userid order by LastTradeTime

	-- Another Tab: ------- 按照日期统计合伙人数目
	--select  distinct layer1, convert(varchar(12), AyLiveApproveAt, 111) layer1加入日期 from #b where OcrRealName = layer1 and layer1 in (select layer1 from #b where OcrRealName <> layer1)
	select count(distinct userid) layer2人数, --layer1 
	convert(varchar(12), AyLiveApproveAt, 111) layer2加入日期  from #b where OcrRealName <> layer1 group by convert(varchar(12), AyLiveApproveAt, 111) order by layer2加入日期";

            SqlDataAdapter adapter = new SqlDataAdapter(queryString, CFDGlobal.GetDbConnectionString("CFDEntities"));

            DataSet ds = new DataSet();
            adapter.Fill(ds);

            var sb=new StringBuilder();

            foreach (DataTable table in ds.Tables)
            {
                sb.Append("<table>");

                sb.Append("<tr>");
                foreach (DataColumn column in table.Columns)
                {
                    sb.Append("<td>" + column + "</td>");
                }

                sb.Append("</tr>");

                foreach (DataRow dr in table.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var o in dr.ItemArray)
                    {
                        sb.Append("<td>"+o+"</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }

            return sb.ToString();
        }
    }
}