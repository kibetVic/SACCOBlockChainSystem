using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("AssetsRegister")]
    public class AssetsRegister
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [StringLength(50)]
        public string? Class { get; set; }

        [StringLength(50)]
        public string? AssetType { get; set; }

        [StringLength(50)]
        public string? AssetName { get; set; }

        [StringLength(50)]
        public string? TagNo { get; set; }

        [StringLength(50)]
        public string? SerialNo { get; set; }

        [Column(TypeName = "money")]
        public decimal? Quantity { get; set; }

        [Column(TypeName = "money")]
        public decimal? ActualValue { get; set; }

        [Column(TypeName = "money")]
        public decimal? MarketValue { get; set; }

        [Column(TypeName = "money")]
        public decimal? TotalValue { get; set; }

        public DateTime? DateOfManufacture { get; set; }

        public DateTime? DatePurchased { get; set; }

        [StringLength(50)]
        public string? TransactionNo { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        public bool? Posted { get; set; }

        [StringLength(50)]
        public string? Location { get; set; }

        [StringLength(50)]
        public string? AuditId { get; set; }

        public DateTime? AuditTime { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }
    }
}