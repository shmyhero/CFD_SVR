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
        public decimal HighPrice { get; set; }
        public bool HighEnabled { get; set; }
        public decimal LowPrice { get; set; }
        public bool LowEnabled { get; set; }
    }
}
