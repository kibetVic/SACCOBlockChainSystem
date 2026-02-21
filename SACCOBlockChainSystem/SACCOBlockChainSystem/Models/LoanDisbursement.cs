using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    public class LoanDisbursement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string MemberNo { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string DisbursementNo { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DisbursedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProcessingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InsuranceFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LegalFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDeductions { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        public DateTime DisbursementDate { get; set; }

        [StringLength(50)]
        public string? DisbursementMethod { get; set; }

        [StringLength(100)]
        public string? BankName { get; set; }

        [StringLength(50)]
        public string? BankAccountNo { get; set; }

        [StringLength(50)]
        public string? ChequeNo { get; set; }

        [StringLength(100)]
        public string? MobileNo { get; set; }

        [StringLength(200)]
        public string? DisbursedBy { get; set; }

        [StringLength(200)]
        public string? AuthorizedBy { get; set; }

        public DateTime? AuthorizationDate { get; set; }

        [StringLength(50)]
        public string? VoucherNo { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }

        [ForeignKey("MemberNo")]
        public virtual Member? Member { get; set; }
    }
}