namespace CFD_API.DTO.Ayondo
{
    public class CreateAccountRequest
    {
        public string AddressCity { get; set; }
        public string AddressCountry { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressZip { get; set; }
        public int AnnualIncome { get; set; }
        public string Currency { get; set; }
        public string DateOfBirth { get; set; }
        public string Email { get; set; }
        public string EmploymentStatus { get; set; }
        public string FirstName { get; set; }
        public string Gender { get; set; }
        public bool HasAttendedTraining { get; set; }
        public bool HasOtherQualification { get; set; }
        public bool HasProfessionalExperience { get; set; }
        public int InvestmentPortfolio { get; set; }
        public bool IsTestRecord { get; set; }
        public string Language { get; set; }
        public string LastName { get; set; }
        public string LeadGuid { get; set; }
        public string Nationality { get; set; }
        public int NetWorth { get; set; }
        public int NumberOfMarginTrades { get; set; }
        public string Password { get; set; }
        public string ProductType { get; set; }
        public bool SubscribeOffers { get; set; }
        public bool SubscribeTradeNotifications { get; set; }
        public string UserName { get; set; }
        public string WhiteLabel { get; set; }
    }
}
