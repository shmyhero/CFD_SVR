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
    }

    public class BalanceDTO
    {
        public int id { get; set; }

        public decimal balance { get; set; }

        public decimal total { get; set; }

        public decimal available { get; set; }
    }
}
