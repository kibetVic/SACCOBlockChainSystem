using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Models
{
    public class Wallet
    {
        [Key]
        [MaxLength(100)]
        public string Address { get; set; } = null!;

        [Required]
        public string PublicKey { get; set; } = null!;

        public string? PrivateKeyEncrypted { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Balance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActivity { get; set; }

        // ========== LINK TO MEMBER (Using both MemberNo AND CompanyCode) ==========
        [Required]
        [MaxLength(50)]
        public string MemberNo { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string CompanyCode { get; set; } = null!;

        // Composite foreign key to Member (MemberNo + CompanyCode)
        [ForeignKey("MemberNo, CompanyCode")]
        public virtual Member? Member { get; set; }

        // ========== KEY MANAGEMENT ==========
        public string? KeyVersion { get; set; } = "ECDSA-P256-V1";
        public bool IsActive { get; set; } = true;
        public DateTime? LastUsedAt { get; set; }

        // ========== NONCE FOR REPLAY PROTECTION ==========
        public long TransactionNonce { get; set; } = 0;

        // ========== CREATE NEW WALLET ==========
        public static Wallet CreateNewWallet(string memberNo, string companyCode)
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
            var privateKeyBytes = ecdsa.ExportECPrivateKey();

            var publicKey = Convert.ToBase64String(publicKeyBytes);
            var privateKeyEncrypted = Convert.ToBase64String(privateKeyBytes);

            using var sha256 = SHA256.Create();
            var publicKeyHash = sha256.ComputeHash(publicKeyBytes);
            var address = "0x" + Convert.ToHexString(publicKeyHash).Substring(0, 40).ToLower();

            return new Wallet
            {
                Address = address,
                PublicKey = publicKey,
                PrivateKeyEncrypted = privateKeyEncrypted,
                MemberNo = memberNo,
                CompanyCode = companyCode,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TransactionNonce = 0,
                Balance = 0
            };
        }
    }
}