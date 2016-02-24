namespace CFD_API.DTO.Form
{
    public class SignupByPhoneFormDTO
    {
        public string phone { get; set; }
        public string verifyCode { get; set; }
    }

    public class SignupByWeChatFormDTO
    {
        
    }

    public class LoginFormDTO
    {
        public int userId { get; set; }
        public string token { get; set; }
    }
}
