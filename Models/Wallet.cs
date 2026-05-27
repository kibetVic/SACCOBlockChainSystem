using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Models
{
    public class Wallet
    {
        [NotMapped]
        public Member Member { get; set; }
        public string MemberNo { get; set; }
        public int MemberId { get; set; }
        public decimal CapitalBalance { get; set; }
        public decimal DepositBalance { get; set; }
        public string CompanyCode { get; set; }
        [Key]
        [MaxLength(100)]
        public string Address { get; set; }

        [Required]
        public string PublicKey { get; set; }

        // Note: In production, this should be encrypted
        public string? PrivateKeyEncrypted { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Balance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActivity { get; set; }

        
    }
}