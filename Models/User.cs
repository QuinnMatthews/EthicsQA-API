namespace EthicsQA.API
{
    public class User
    {
        public required Guid id { get; set; } 
        public required string Phone { get; set; }
        public string? SMSCode { get; set; }
        public DateTime? SMSCodeTimestamp { get; set; }
        public bool SMSCodeUsed;
    }
}