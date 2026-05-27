using SACCOBlockChainSystem.Services;
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
        public int MemberId { get; set; }
        public string memberNo { get; set; }
        public decimal CapitalBalance { get; set; }
        public decimal DepositBalance { get; set; }
        public string CompanyCode { get; set; }
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

        // ========== CRYPTOGRAPHIC SIGNING PROPERTIES ==========
        public string? KeyVersion { get; set; } = "ECDSA-P256-V1";
        public bool IsActive { get; set; } = true;
        public DateTime? LastUsedAt { get; set; }
        public long TransactionNonce { get; set; } = 0;

        // ========== CREATE NEW WALLET ==========
        public static Wallet CreateNewWallet(int memberId, string memberNo, string companyCode)
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
            var privateKeyBytes = ecdsa.ExportECPrivateKey();

            var publicKey = Convert.ToBase64String(publicKeyBytes);

            // Encrypt the private key
            var privateKeyEncrypted = EncryptionHelper.Encrypt(Convert.ToBase64String(privateKeyBytes));

            using var sha256 = SHA256.Create();
            var publicKeyHash = sha256.ComputeHash(publicKeyBytes);
            var address = "0x" + Convert.ToHexString(publicKeyHash).Substring(0, 40).ToLower();

            return new Wallet
            {
                Address = address,
                PublicKey = publicKey,
                PrivateKeyEncrypted = privateKeyEncrypted,
                MemberId = memberId,
                memberNo = memberNo,
                CompanyCode = companyCode,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TransactionNonce = 0,
                Balance = 0,
                CapitalBalance = 0,
                DepositBalance = 0
            };
        }
    }
}