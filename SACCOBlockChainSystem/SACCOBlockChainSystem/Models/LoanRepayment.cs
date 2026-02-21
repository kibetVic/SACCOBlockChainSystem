using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class LoanRepayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!; // Changed from LoanApplicationId

        [Required]
        [StringLength(50)]
        public string MemberNo { get; set; } = null!; // Changed from MemberId int

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string ReceiptNo { get; set; } = null!;

        public DateTime PaymentDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InterestAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrincipalAllocated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OverpaymentAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfterPayment { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? ReferenceNo { get; set; }

        [StringLength(200)]
        public string? ReceivedBy { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed"; // Using string to match SQL

        public DateTime? ReversedDate { get; set; }

        [StringLength(100)]
        public string? ReversedBy { get; set; }

        [StringLength(500)]
        public string? ReversalReason { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation Properties
        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }

        [ForeignKey("MemberNo")]
        public virtual Member? Member { get; set; }
    }
}