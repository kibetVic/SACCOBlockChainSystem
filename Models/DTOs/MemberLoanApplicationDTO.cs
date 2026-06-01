// Models/DTOs/MemberLoanApplicationDTO.cs
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class MemberLoanApplicationDTO
    {
        [Required]
        public string LoanCode { get; set; }

        [Required]
        [Range(1000, 5000000)]
        public decimal PrincipalAmount { get; set; }

        [Required]
        [Range(1, 360)]
        public int RepayPeriod { get; set; }

        [Required]
        public string Purpose { get; set; }

        public string Remarks { get; set; }

        // Guarantors (max 3)
        public List<GuarantorInviteDTO> GuarantorInvites { get; set; } = new List<GuarantorInviteDTO>();

        public string MemberNo { get; set; }
        public string CompanyCode { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime ApplicationDate { get; set; } = DateTime.Now;
    }

    public class GuarantorInviteDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string MemberNo { get; set; }

        [Required]
        public string FullName { get; set; }

        [Range(1000, 10000000)]
        public decimal? ProposedAmount { get; set; }
    }

    public class GuarantorsResponseDTO
    {
        public int Id { get; set; }
        public string LoanNo { get; set; }
        public string GuarantorMemberNo { get; set; }
        public string GuarantorName { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public string Status { get; set; } // Pending, Accepted, Rejected
        public DateTime? ResponseDate { get; set; }
        public string ResponseRemarks { get; set; }
        public string InvitationToken { get; set; }
        public DateTime InvitationExpiry { get; set; }
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public decimal AvailableShares { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedBy { get; set; }
        public string? Remarks { get; set; }
    }
    public class MemberLoanSummaryDTO
    {
        public string LoanNo { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public decimal OutstandingBalance { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public decimal MonthlyInstallment { get; set; }
    }

    public class PendingGuaranteeDTO
    {
        public string LoanNo { get; set; } = null!;
        public string ApplicantName { get; set; } = null!;
        public decimal LoanAmount { get; set; }
        public DateTime InvitationDate { get; set; }
    }
    
}