using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("DividendDetails")]
    public class DividendDetails
    {
        [Key]
        public int Id { get; set; }

        public int DividendYear { get; set; }

        [Required]
        [StringLength(50)]
        public string MemberNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WeightedSavings { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SavingsDividend { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShareDividend { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossDividend { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WithholdingTax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetDividend { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }
    }
}