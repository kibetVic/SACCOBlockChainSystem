using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    public class Devidend
    {
        [Key]
        public int Id { get; set; }

        public int DividendYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SavingsPool { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShareFlatRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WithholdingTaxRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWeightedSavings { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGrossDividend { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}
