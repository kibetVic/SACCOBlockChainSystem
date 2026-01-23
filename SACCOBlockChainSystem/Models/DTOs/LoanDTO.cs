using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class LoanApplicationDTO
    {
        [Required]
        public string MemberNo { get; set; } = null!;

        [Required]
        [Range(1000, 1000000)]
        public decimal Amount { get; set; }

        [Required]
        public string LoanType { get; set; } = "REGULAR"; // REGULAR, EMERGENCY, DEVELOPMENT

        [Required]
        [Range(1, 60)]
        public int RepaymentPeriod { get; set; } // In months

        [Required]
        public string Purpose { get; set; } = null!;

        public decimal? InterestRate { get; set; } = 12.5m;
        public decimal? MonthlyRepayment { get; set; }
        public string? SourceOfRepayment { get; set; }
        public string? AppliedBy { get; set; }
    }

    public class LoanRepaymentDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        [Range(100, 1000000)]
        public decimal Amount { get; set; }

        public string? ReceiptNo { get; set; }
        public string? Remarks { get; set; }
        public string? ProcessedBy { get; set; }
    }

    public class LoanApplicationResponseDTO
    {
        public bool Success { get; set; }
        public string LoanNumber { get; set; } = null!;
        public decimal Amount { get; set; }
        public string Status { get; set; } = null!; // PENDING, APPROVED, REJECTED
        public DateTime ApplicationDate { get; set; }
        public string BlockchainTxId { get; set; } = null!;
        public string? Message { get; set; }
    }

    public class LoanRepaymentResponseDTO
    {
        public bool Success { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal NewBalance { get; set; }
        public string BlockchainTxId { get; set; } = null!;
        public DateTime RepaymentDate { get; set; }
        public string? Message { get; set; }
    }
}