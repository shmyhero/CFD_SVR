namespace CFD_API.DTO
{
    public class SignupResultDTO
    {
        public bool success { get; set; }
        public bool? isNewUser { get; set; }

        public int userId { get; set; }
        public string token { get; set; }
    }

    public class UserDTO
    {
        
    }
}
