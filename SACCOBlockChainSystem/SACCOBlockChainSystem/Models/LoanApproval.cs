using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    public class LoanApproval
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        public int ApprovalLevel { get; set; }

        [Required]
        [StringLength(20)]
        public string ApprovalStatus { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ApprovedAmount { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal ApprovedInterestRate { get; set; }

        public int ApprovedPeriod { get; set; }

        [StringLength(500)]
        public string? ApprovalComments { get; set; }

        [StringLength(100)]
        public string ApprovedBy { get; set; } = null!;

        public DateTime ApprovalDate { get; set; }

        [StringLength(50)]
        public string? ApprovalRole { get; set; }

        [StringLength(100)]
        public string? RejectedBy { get; set; }

        public DateTime? RejectedDate { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        public bool IsFinalApproval { get; set; }

        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }
    }
}