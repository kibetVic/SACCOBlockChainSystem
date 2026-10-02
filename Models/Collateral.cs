using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("COLLATERALS")]
    public class Collateral
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ColCode { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Coldescription { get; set; } = null!;

        [StringLength(50)]
        public string? MemberNo { get; set; }  // Made nullable

        public double Percentage { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [Column(TypeName = "varbinary(max)")]
        public byte[]? Photo { get; set; }
        [StringLength(50)]
        public string? PhotoContentType { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation property to BlockchainTransaction
        [ForeignKey("BlockchainTxId")]
        public virtual BlockchainTransaction? BlockchainTransaction { get; set; }

        // Navigation property to Member (configured via Fluent API)
        public virtual Member? Member { get; set; }
    }
}