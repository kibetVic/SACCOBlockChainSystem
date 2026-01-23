namespace SACCOBlockChainSystem.Models.DTOs
{
    public class MemberRegistrationDTO
    {
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? CompanyCode { get; set; }
        public decimal InitialShares { get; set; } 
        public string? CreatedBy { get; set; }
    }

    public class MemberResponseDTO
    {
        public string MemberNo { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string BlockchainTxId { get; set; }
        public decimal ShareBalance { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyCode { get; set; }
    }
}