using System;
using System.Collections.Generic;

namespace CFD_API.DTO
{
    public class SignupResultDTO : ResultDTO
    {
        //public bool success { get; set; }
        public bool? isNewUser { get; set; }

        public int? userId { get; set; }
        public string token { get; set; }
    }

    public class UserDTO
    {
        public int id { get; set; }
        public string nickname { get; set; }
        public string picUrl { get; set; }
        public string phone { get; set; }
        public bool autoCloseAlert { get; set; }
        public string weChatOpenId { get; set; }
        public decimal? rewardAmount { get; set; }
    }

    public class BalanceDTO
    {
        public int id { get; set; }

        public decimal balance { get; set; }

        public decimal total { get; set; }

        public decimal available { get; set; }
    }

    public class PLReportDTO
    {
        //public decimal indexInvest { get; set; }
        //public decimal indexPL { get; set; }
        //public decimal fxInvest { get; set; }
        //public decimal fxPL { get; set; }
        //public decimal futureInvest { get; set; }
        //public decimal futurePL { get; set; }
        //public decimal stockUSInvest { get; set; }
        //public decimal stockUSPL { get; set; }

        public string name { get; set; }
        public decimal invest { get; set; }
        public decimal pl { get; set; }
    }

    public class StockAlertDTO
    {
        public int SecurityId { get; set; }
        public decimal? HighPrice { get; set; }
        public bool HighEnabled { get; set; }
        public decimal? LowPrice { get; set; }
        public bool LowEnabled { get; set; }
    }

    public class PushDTO
    {
        public string deviceToken;
        public int deviceType;
    }

    public class MonthDailyCheckInDTO
    {
        public int month { get; set; }
        public int monthDayCount { get; set; }
        public List<DailySignDTO> days { get; set; }
    }

    public class RewardIntroDTO
    {
        public string url { get; set; }
        public string imgUrl { get; set; }
        public string title { get; set; }
        public string text { get; set; }
    }

    public class DailySignDTO
    {
        public int day { get; set; }
    }

    /// <summary>
    /// 每日签到页面的信息集合，包括：累计交易金(未支付的)、累计签到天数、当天是否已经签到过、当天签到的奖励金额
    /// </summary>
    public class DailySignInfoDTO
    {
        public decimal TotalUnpaidAmount { get; set; }
        public int TotalSignDays { get; set; }
        public bool IsSignedToday { get; set; }
        public decimal AmountToday { get; set; }
    }

    public class RewardDTO
    {
        /// <summary>
        /// 每日签到奖励汇总
        /// </summary>
        public decimal totalDailySign { get; set; }
        /// <summary>
        /// 模拟交易奖励汇总
        /// </summary>
        public decimal totalDemoTransaction { get; set; }
        /// <summary>
        /// 模拟盘注册奖励（20元）
        /// </summary>
        public decimal demoRegister { get; set; }
    }

    public class MessageDTO {
        public int id { get; set; }

        public int userId { get; set; }

        public string title { get; set; }

        public string body { get; set; }

        public DateTime? createdAt { get; set; }

        public bool isReaded { get; set; }
    }

    public class LiveSignupFormDTO
    {
        public string username { get; set; }
        public string password { get; set; }

        public string email { get; set; }


        
    }

    /*
{
  "result": "0",
  "message": "OCR识别服务处理成功",
  "real_name": "刘六鹏",
  "id_code": "440681198910257747 ",
  "addr": "广东省佛山市顺德区陈村镇旧圩桂园路海棠大街37",
  "gender": "男",
  "ethnic": "汉",
  "photo": "{base64 data}",
  "issue_authority": "佛山市公安局顺德分局",
  "valid_period": "2006.01.26-2016.01.26",
  "transaction_id": "835aded785fd95267f29bea3c36476f2"
}
     */
    public class GZTOrcResultFormDTO
    {
        public string realName { get; set; }
        public string idCode { get; set; }
        public string addr { get; set; }
        public string gender { get; set; }
        public string ethnic { get; set; }
        public string photo { get; set; }
        public string issueAuth { get; set; }
        public string validPeriod { get; set; }
        public string transId { get; set; }
    }
}
