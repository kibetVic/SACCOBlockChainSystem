using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class LoanAuditTrail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!; // Changed from LoanApplicationId

        [StringLength(50)]
        public string? MemberNo { get; set; }

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string PreviousStatus { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string NewStatus { get; set; } = null!;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? Changes { get; set; }

        [Required]
        [StringLength(100)]
        public string PerformedBy { get; set; } = null!;

        [StringLength(50)]
        public string? PerformedByRole { get; set; }

        public DateTime PerformedDate { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation Property
        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }
    }
}