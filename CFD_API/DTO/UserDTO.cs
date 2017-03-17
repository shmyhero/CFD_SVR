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

    public class UserBaseDTO
    {
        public int id { get; set; }
        public string nickname { get; set; }
        public string picUrl { get; set; }
    }

    public class MeDTO : UserBaseDTO
    {
        //public int id { get; set; }
        //public string nickname { get; set; }
        //public string picUrl { get; set; }
        public string phone { get; set; }
        public bool autoCloseAlert { get; set; }
        public string weChatOpenId { get; set; }
        public decimal? rewardAmount { get; set; }

        public UserLiveStatus liveAccStatus { get; set; }
        public string liveAccRejReason { get; set; }
        public string liveUsername { get; set; }
        public string liveEmail { get; set; }
        public bool autoCloseAlert_Live { get; set; }
        public string bankCardStatus { get; set; }
    }

    public class UserDTO : UserBaseDTO
    {
        //public int id { get; set; }
        //public string nickname { get; set; }
        //public string picUrl { get; set; }
        public decimal roi { get; set; }
        public int posCount { get; set; }
        public decimal winRate { get; set; }
    }

    public class MyInfoDTO
    {
        //public int id { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string email { get; set; }
        public string addr { get; set; }
    }

    public class UserDetailDTO : UserBaseDTO
    {
        public int followerCount { get; set; }
        public bool isFollowing { get; set; }
        public decimal totalPl { get; set; }
        public decimal avgPl { get; set; }
        public decimal winRate { get; set; }
        public List<CardDTO> cards { get; set; }
    }

    public class NewDepositDTO
    {
        public string transferId { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string email { get; set; }
        public string addr { get; set; }
    }

    public enum UserLiveStatus
    {
        None = 0,
        Active = 1,
        Pending = 2,
        Rejected = 3,
    }

    public class LiveUserInfoDTO
    {
        public string lastName { get; set; }
        public string firstName { get; set; }
        public string identityID { get; set; }
        public string bankCardNumber { get; set; }
        public string bankName { get; set; }
        public string bankCardStatus { get; set; }
        public string bankCardRejectReason { get; set; } 
        public string bankIcon { get; set; }
        public string branch { get; set; }
        public string province { get; set; }
        public string city { get; set; }

        public string addr { get; set; }

        public DateTime? lastWithdrawAt { get; set; }
        public decimal? lastWithdraw { get; set; }
    }

    public struct BankCardUpdateStatus
    {
        public static string Approved = "Approved";
        public static string PendingReview = "PendingReview";
        public static string Rejected = "Rejected";
    }

    public class BalanceDTO
    {
        public int id { get; set; }

        public decimal balance { get; set; }

        public decimal total { get; set; }

        public decimal available { get; set; }

        /// <summary>
        /// 可提现余额
        /// </summary>
        public decimal refundable { get; set; }

        /// <summary>
        /// 提现说明
        /// </summary>
        public string comment { get; set; }
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
        /// 卡牌产生的交易金
        /// </summary>
        public decimal totalCard { get; set; }
        /// <summary>
        /// 模拟盘注册奖励（20元）
        /// </summary>
        public decimal demoRegister { get; set; }
    }

    public class TotalRewardDTO
    {
        public decimal? total { get; set; }
        public decimal? paid { get; set; }
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

    public class OcrFaceCheckFormDTO
    {
        public string accessId { get; set; }
        public string accessKey { get; set; }

        public string transaction_id { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }

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
    //public class GZTOcrResultFormDTO
    //{
    //    public string realName { get; set; }
    //    public string idCode { get; set; }
    //    public string addr { get; set; }
    //    public string gender { get; set; }
    //    public string ethnic { get; set; }
    //    public string photo { get; set; }
    //    public string issueAuth { get; set; }
    //    public string validPeriod { get; set; }
    //    public string transId { get; set; }
    //}

    public class LiveSignupFormDTO
    {
        public bool? confirmMifidOverride { get; set; }

        public string username { get; set; }
        public string password { get; set; }
        public string email { get; set; }

        //public string realName { get; set; }
        //public string firstName { get; set; }
        //public string lastName { get; set; }
        public bool? gender { get; set; }
        public string birthday { get; set; }
        public string ethnic { get; set; }
        //public string idCode { get; set; }
        public string addr { get; set; }
        public string issueAuth { get; set; }
        public string validPeriod { get; set; }
        
        public int? annualIncome { get; set; }
        public int? netWorth { get; set; }
        public int? investPct { get; set; }
        public string empStatus { get; set; }
        public int? investFrq { get; set; }
        public bool? hasProExp { get; set; }
        public bool? hasAyondoExp { get; set; }
        public bool? hasOtherQualif { get; set; }
        public bool? expOTCDeriv { get; set; }
        public bool? expDeriv { get; set; }
        public bool? expShareBond { get; set; }

        //public string ocrTransId { get; set; }

        public string sourceOfFunds { get; set; }
        public string employerName { get; set; }
        public string employerSector { get; set; }
        public string empPosition { get; set; }
        public int? monthlyIncome { get; set; }
        public int? investments { get; set; }
        public bool? hasTraining { get; set; }
        public bool? hasDemoAcc { get; set; }
        public string otherQualif { get; set; }
        public bool? hasTradedHighLev { get; set; }
        public bool? hasTradedMidLev { get; set; }
        public bool? hasTradedNoLev { get; set; }
        public int? highLevBalance { get; set; }
        public int? highLevFrq { get; set; }
        public int? highLevRisk { get; set; }
        public int? midLevBalance { get; set; }
        public int? midLevFrq { get; set; }
        public int? midLevRisk { get; set; }
        public int? noLevBalance { get; set; }
        public int? noLevFrq { get; set; }
        public int? noLevRisk { get; set; }
    }

    public class AMSLiveUserCreateFormDTO
    {
        public bool? confirmMifidOverride { get; set; }
        public bool? confirmDSA { get; set; }
        public bool? confirmTerms { get; set; }
        public bool? isPhoneVerified { get; set; }

        public string origin { get; set; }
        public string productType { get; set; }

        //public string addressCity { get; set; }
        /// <summary>
        /// https://en.wikipedia.org/wiki/ISO_3166-1
        /// </summary>
        public string addressCountry { get; set; }
        public string addressLine1 { get; set; }
        //public string AddressLine2 { get; set; }
        //public string AddressZip { get; set; }
        //public string ClientIP { get; set; }
        public string currency { get; set; }
        /// <summary>
        /// YYYY-MM-DD
        /// </summary>
        public string dateOfBirth { get; set; }
        public string email { get; set; }
        public string employerName { get; set; }
        public string employerSector { get; set; }
        public string employmentStatus { get; set; }
        public string firstname { get; set; }
        public string gender { get; set; }
        public bool? isIdVerified { get; set; }
        public bool? isTestRecord { get; set; }
        public string jobTitle { get; set; }
        public string language { get; set; }
        public string lastname { get; set; }
        public string mifidGuid { get; set; }
        /// <summary>
        /// https://en.wikipedia.org/wiki/ISO_3166-1
        /// </summary>
        public string nationality { get; set; }
        public string nickname { get; set; }
        public string password { get; set; }
        public string phonePrimary { get; set; }
        /// <summary>
        /// https://en.wikipedia.org/wiki/ISO_3166-1
        /// </summary>
        public string phonePrimaryIso2 { get; set; }
        public string sourceOfFunds { get; set; }
        /// <summary>
        /// Opt-in for marketing emails.
        /// </summary>
        public bool? subscribeOffers { get; set; }
        /// <summary>
        /// Opt-in for trade notification emails.
        /// </summary>
        public bool? subscribeTradeNotifications { get; set; }
        public string username { get; set; }
        //public string SalesRepGuid { get; set; }
        //public int AnnualIncome { get; set; }
        //public int InvestmentPortfolio { get; set; }
        //public string JobTitle { get; set; }
        //public string LeveragedProducts { get; set; }
        //public int NetWorth { get; set; }
        //public int NumberOfMarginTrades { get; set; }
        //public string PhonePrimaryCountryCode { get; set; }
    }

    public class AMSLiveUserMifidFormDTO
    {
        public bool? hasAttendedTraining { get; set; }
        public bool? hasDemoAccount { get; set; }
        public bool? hasOtherQualification { get; set; }
        public bool? hasProfessionalExperience { get; set; }
        public int? investments { get; set; }
        public int? monthlyNetIncome { get; set; }
        public string[] otherQualification { get; set; }
        public bool? hasTradedHighLev { get; set; }
        public bool? hasTradedMidLev { get; set; }
        public bool? hasTradedNoLev { get; set; }
        public int? highLevBalance { get; set; }
        public int? highLevFrequency { get; set; }
        public int? highLevRisk { get; set; }
        public int? midLevBalance { get; set; }
        public int? midLevFrequency { get; set; }
        public int? midLevRisk { get; set; }
        public int? noLevBalance { get; set; }
        public int? noLevFrequency { get; set; }
        public int? noLevRisk { get; set; }
    }

    /// <summary>
    /// 用户以文本形式输入，服务端转使用BankStatement模板转换为Ayondo需要的图片形式
    /// </summary>
    public class LiveUserBankCardOriginalFormDTO
    {
        /// <summary>
        /// Client Real Name - must be same with what in ID Card
        /// </summary>
        public string AccountHolder { get; set; }
        /// <summary>
        /// Bank card number
        /// </summary>
        public string AccountNumber { get; set; }
        /// <summary>
        /// Ayondo Live Account GUID
        /// </summary>
        public string NameOfBank { get; set; }
        public string Info { get; set; }
        /// <summary>
        /// 转到WeCollect之后，就不再需要SwiftCode，银行地址了
        /// </summary>
        //public string SwiftCode { get; set; }
        //public string AddressOfBank { get; set; }
        /// <summary>
        /// 支行
        /// </summary>
        public string Branch { get; set; }
        /// <summary>
        /// 省
        /// </summary>
        public string Province { get; set; }
        /// <summary>
        /// 市
        /// </summary>
        public string City { get; set; }
    }

    public class LiveUserBankCardFormDTO
    {
        public string accountHolder { get; set; }
        public string bankStatementContent { get; set; }
        //public string BankStatementContentStr { get; set; }
        public string bankStatementContentType { get { return "image/jpeg"; } }
        public string bankStatementFilename { get; set; }
        /// <summary>
        /// bic就是swift code
        /// </summary>
        public string bic { get; set; }
        //该字段被移到Header里面
        ////Ayondo Live Account GUID
        //public string Guid { get; set; }
        /// <summary>
        /// Bank card number
        /// </summary>
        public string accountNumber { get; set; }
        public string nameOfBank { get; set; }
        
        public string branch { get; set; }
      
        public string province { get; set; }

        public string city { get; set; }
        /// <summary>
        /// internal bank account number
        /// 只有欧洲银行有IBAN，国内银行没有。 但因为AMS接口需要这个字段，所以必须加上，但给空值。
        /// </summary>
        public string iban { get; set; }
        public string idCardNumber { get; set; }
        public string info { get; set; }


        ////在Ayondo的更新发布前，先使用老接口定义
        //public string AccountHolder { get; set; }
        //public string IdentityID { get; set; }
        //public string BankStatementContent { get; set; }
        ////public string BankStatementContentStr { get; set; }
        //public string BankStatementContentType { get { return "image/jpeg"; } }
        //public string BankStatementFileName { get; set; }
        ////Ayondo Live Account GUID
        //public string Guid { get; set; }
        ///// <summary>
        ///// Bank card number
        ///// </summary>
        //public string AccountNumber { get; set; }
        //public string NameOfBank { get; set; }


    }

    public class LiveUserRefundDTO
    {
        public decimal Amount;
    }

    public class BankCardUpdateDTO
    {
        //public string GUID { get; set; }
        public string status { get; set; }
        public string rejectionInfo { get; set; }
        public string rejectionType { get; set; }

    }

    public class ProofOfAddressDTO
    {
        public string imageBase64 { get; set; }

        public string text { get; set; }
    }
}
