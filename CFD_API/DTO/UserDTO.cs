namespace CFD_API.DTO
{
    public class SignupDTO
    {
        public bool success { get; set; }
        public bool? isNewUser { get; set; }
        public string token { get; set; }
    }
}
