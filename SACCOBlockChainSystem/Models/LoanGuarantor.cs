using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public enum GuarantorStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Released = 4
    }

    public class LoanGuarantor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!; // Changed from LoanApplicationId

        [Required]
        [StringLength(50)]
        public string GuarantorMemberNo { get; set; } = null!; // Changed from GuarantorMemberId int

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GuaranteeAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AvailableShares { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LockedAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Using string to match SQL

        public DateTime AssignedDate { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? ReleasedDate { get; set; }

        [StringLength(100)]
        public string? ReleasedBy { get; set; }

        // Navigation Properties
        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }

        [ForeignKey("GuarantorMemberNo")]
        public virtual Member? GuarantorMember { get; set; }
    }
}