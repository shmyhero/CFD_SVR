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

        public UserLiveStatus liveAccStatus { get; set; }
        public string liveAccRejReason { get; set; }
    }

    public enum UserLiveStatus
    {
        None = 0,
        Active = 1,
        Pending = 2,
        Rejected = 3,
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

    public class OcrFormDTO
    {
        public string accessId { get; set; }
        public string accessKey { get; set; }

        public string frontImg { get; set; }
        public string frontImgExt { get; set; }
        public string backImg { get; set; }
        public string backImgExt { get; set; }

        public string timeStamp { get; set; }
        public string sign { get; set; }
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
    public class GZTOcrResultFormDTO
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

    public class LiveSignupFormDTO
    {
        public string username { get; set; }
        public string password { get; set; }
        public string email { get; set; }

        public string realName { get; set; }
        public bool gender { get; set; }
        public string birthday { get; set; }
        public string ethnic { get; set; }
        public string idCode { get; set; }
        public string addr { get; set; }
        public string issueAuth { get; set; }
        public string validPeriod { get; set; }
        
        public int annualIncome { get; set; }
        public int netWorth { get; set; }
        public int investPct { get; set; }
        public string empStatus { get; set; }
        public int investFrq { get; set; }
        public bool hasProExp { get; set; }
        public bool hasAyondoExp { get; set; }
        public bool hasOtherQualif { get; set; }
        public bool expOTCDeriv { get; set; }
        public bool expDeriv { get; set; }
        public bool expShareBond { get; set; }

        //public string ocrTransId { get; set; }
    }

    public class AMSLiveUserCreateFormDTO
    {
        public string AddressCity { get; set; }
        public string AddressCountry { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressZip { get; set; }
        public string ClientIP { get; set; }
        public string Currency { get; set; }
        public string FirstName { get; set; }
        public string Gender { get; set; }
        public bool IsTestRecord { get; set; }
        public string Language { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
        public string PhonePrimary { get; set; }
        public string SalesRepGuid { get; set; }
        public string UserName { get; set; }
        public int AnnualIncome { get; set; }
        public string DateOfBirth { get; set; }
        public string Email { get; set; }
        public string EmploymentStatus { get; set; }
        public bool HasAttendedTraining { get; set; }
        public bool HasOtherQualification { get; set; }
        public bool HasProfessionalExperience { get; set; }
        public int InvestmentPortfolio { get; set; }
        public bool IsIDVerified { get; set; }
        public string JobTitle { get; set; }
        public string LeveragedProducts { get; set; }
        public string Nationality { get; set; }
        public int NetWorth { get; set; }
        public string Nickname { get; set; }
        public int NumberOfMarginTrades { get; set; }
        public string PhonePrimaryCountryCode { get; set; }
        public bool SubscribeTradeNotifications { get; set; }
    }
}
