// Models/ViewModels/PendingGuaranteeViewModel.cs
namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class PendingGuaranteeViewModel
    {
        public int GuarantorId { get; set; }
        public string LoanNo { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantMemberNo { get; set; } = string.Empty;
        public decimal LoanAmount { get; set; }
        public decimal? ProposedAmount { get; set; }
        public DateTime InvitationDate { get; set; }
        public string ApplicantPhone { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public string? Token { get; set; }
        public string? InvitedEmail { get; set; }
        public decimal AvailableDeposits { get; set; }
        public decimal CurrentGuarantees { get; set; }
        public decimal MaxGuaranteeAmount { get; set; }
        public string LoanPurpose { get; set; } = string.Empty;
        public string LoanStatus { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public bool IsExpired { get; set; }
    }
}