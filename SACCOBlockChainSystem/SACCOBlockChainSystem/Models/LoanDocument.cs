using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class LoanDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!; // Changed from LoanApplicationId

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string DocumentName { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public string FilePath { get; set; } = null!;

        [StringLength(255)]
        public string? FileName { get; set; }

        public long FileSize { get; set; }

        [StringLength(50)]
        public string ContentType { get; set; } = null!;

        public DateTime UploadedDate { get; set; }

        [StringLength(100)]
        public string UploadedBy { get; set; } = null!;

        public bool IsVerified { get; set; }

        public DateTime? VerifiedDate { get; set; }

        [StringLength(100)]
        public string? VerifiedBy { get; set; }

        // Navigation Property
        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }
    }
}