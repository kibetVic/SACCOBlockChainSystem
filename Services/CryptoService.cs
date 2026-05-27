using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Services
{
    public interface ICryptoService
    {
        // ========== WALLET MANAGEMENT ==========
        Task<WalletResult> CreateWalletForMemberAsync(string memberNo, string companyCode);
        Task<Wallet> GetWalletByMemberNoAsync(string memberNo);
        Task<bool> HasWalletAsync(string memberNo);
        Task<WalletInfo> GetWalletInfoAsync(string memberNo);

        Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData);
        Task<bool> VerifySignatureAsync(string memberNo, string data, string signature);
        //Task<WalletResult> CreateWalletForMemberAsync(int memberId, string companyCode);

        //Task<Wallet> GetWalletByMemberIdAsync(int memberId);
        //Task<bool> HasWalletAsync(int memberId);
        //Task<WalletInfo> GetWalletInfoAsync(int memberId);

        //// ========== SIGNING ==========
        //Task<SigningResult> SignTransactionAsync(int memberId, object transactionData);
        //Task<bool> VerifySignatureAsync(int memberId, string data, string signature);

        // ========== VERIFICATION ==========
        Task<VerificationResult> VerifyTransactionAsync(int contribId);
        Task<ChainVerificationResult> VerifyMemberChainAsync(string memberNo);

        // ========== FRAUD DETECTION ==========
        Task<FraudResult> AnalyzeTransactionAsync(string memberNo, decimal amount, string transactionType);
    }

    public class WalletResult
    {
        public bool Success { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class SigningResult
    {
        public bool Success { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string TransactionHash { get; set; } = string.Empty;
        public long Nonce { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class VerificationResult
    {
        public bool IsValid { get; set; }
        public bool SignatureValid { get; set; }
        public bool ChainValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ChainVerificationResult
    {
        public bool IsValid { get; set; }
        public int TotalTransactions { get; set; }
        public int ValidSignatures { get; set; }
        public List<int> FailedTransactionIds { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class FraudResult
    {
        public int RiskScore { get; set; }
        public bool IsSuspicious { get; set; }
        public List<string> Flags { get; set; } = new();
        public bool ShouldBlock { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class CryptoService : ICryptoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CryptoService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuditTrailService _auditService;

        public CryptoService(
            ApplicationDbContext context,
            ILogger<CryptoService> logger,
            IHttpContextAccessor httpContextAccessor,
            AuditTrailService auditService)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _auditService = auditService;
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private byte[] DecryptPrivateKey(string encryptedPrivateKey)
        {
            var decrypted = EncryptionHelper.Decrypt(encryptedPrivateKey);
            return Convert.FromBase64String(decrypted);
        }

        private string ComputeHash(string data)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLower();
        }

        private string CreateCanonicalString(object data, long nonce)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            return $"{json}|nonce:{nonce}";
        }

        // ============================================================
        // WALLET MANAGEMENT
        // ============================================================

        public async Task<WalletResult> CreateWalletForMemberAsync(string memberNo, string companyCode)
        {
            _logger.LogInformation($"Creating wallet for member ID: {memberNo}, Company: {companyCode}");

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            if (member == null)
            {
                return new WalletResult { Success = false, Message = "Member not found" };
            }

            var existingWallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo && w.CompanyCode == companyCode);

            if (existingWallet != null)
            {
                return new WalletResult
                {
                    Success = true,
                    WalletAddress = existingWallet.Address,
                    Message = "Wallet already exists"
                };
            }

            try
            {
                var wallet = Wallet.CreateNewWallet(memberNo, companyCode);
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Wallet created for member {member.MemberNo}: {wallet.Address}");

                return new WalletResult
                {
                    Success = true,
                    WalletAddress = wallet.Address,
                    Message = "Wallet created successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create wallet for member ID {memberNo}");
                return new WalletResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<Wallet> GetWalletByMemberNoAsync(string memberNo)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);
        }

        public async Task<bool> HasWalletAsync(string memberNo)
        {
            return await _context.Wallets.AnyAsync(w => w.memberNo == memberNo);
        }

        public async Task<WalletInfo> GetWalletInfoAsync(string memberNo)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);

            if (wallet == null) return null;

            return new WalletInfo
            {
                WalletAddress = wallet.Address,
                CreatedAt = wallet.CreatedAt,
                IsActive = wallet.IsActive,
                TransactionNonce = wallet.TransactionNonce,
                Balance = wallet.Balance
            };
        }

        // ============================================================
        // SIGNING TRANSACTIONS
        // ============================================================

        public async Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);

            if (wallet == null)
            {
                return new SigningResult { Success = false, Message = "Member has no wallet. Create wallet first." };
            }

            if (!wallet.IsActive)
            {
                return new SigningResult { Success = false, Message = "Wallet is inactive" };
            }

            try
            {
                wallet.TransactionNonce++;
                wallet.LastUsedAt = DateTime.UtcNow;

                var canonicalData = CreateCanonicalString(transactionData, wallet.TransactionNonce);
                var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                var privateKeyBytes = DecryptPrivateKey(wallet.PrivateKeyEncrypted!);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

                var signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
                var signatureBase64 = Convert.ToBase64String(signature);
                var transactionHash = ComputeHash(canonicalData);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Transaction signed for member ID {memberNo}, nonce: {wallet.TransactionNonce}");

                return new SigningResult
                {
                    Success = true,
                    Signature = signatureBase64,
                    TransactionHash = transactionHash,
                    Nonce = wallet.TransactionNonce,
                    Message = "Transaction signed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sign transaction for member ID {memberNo}");
                return new SigningResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<bool> VerifySignatureAsync(string memberNo, string data, string signature)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);

            if (wallet == null || string.IsNullOrEmpty(wallet.PublicKey))
            {
                return false;
            }

            try
            {
                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signature);
                var publicKeyBytes = Convert.FromBase64String(wallet.PublicKey);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signature verification failed");
                return false;
            }
        }

        // ============================================================
        // VERIFICATION
        // ============================================================

        public async Task<VerificationResult> VerifyTransactionAsync(int contribId)
        {
            var contrib = await _context.Contribs
                .FirstOrDefaultAsync(c => c.Id == contribId);

            if (contrib == null)
            {
                return new VerificationResult { IsValid = false, Message = "Transaction not found" };
            }

            if (string.IsNullOrEmpty(contrib.TransactionSignature))
            {
                return new VerificationResult { IsValid = false, Message = "No signature found" };
            }

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == contrib.MemberNo);

            if (member == null)
            {
                return new VerificationResult { IsValid = false, Message = "Member not found" };
            }

            var txData = new
            {
                contrib.MemberNo,
                contrib.Amount,
                TransactionDate = contrib.ContrDate?.ToString("o"),
                contrib.Sharescode,
                contrib.ReceiptNo,
                contrib.TransactionNo,
                contrib.CompanyCode
            };

            var canonicalData = CreateCanonicalString(txData, contrib.TransactionSequence ?? 0);
            var isValid = await VerifySignatureAsync(member.MobileNo, canonicalData, contrib.TransactionSignature);

            contrib.IsSignatureVerified = isValid;
            contrib.SignatureVerifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new VerificationResult
            {
                IsValid = isValid,
                SignatureValid = isValid,
                ChainValid = true,
                Message = isValid ? "Verified" : "Verification failed"
            };
        }

        public async Task<ChainVerificationResult> VerifyMemberChainAsync(string memberNo)
        {
            var transactions = await _context.Contribs
                .Where(c => c.MemberNo == memberNo)
                .OrderBy(c => c.Id)
                .ToListAsync();

            var result = new ChainVerificationResult
            {
                TotalTransactions = transactions.Count,
                FailedTransactionIds = new List<int>()
            };

            string previousHash = null;
            int validCount = 0;

            foreach (var tx in transactions)
            {
                if (previousHash != null && tx.PreviousTransactionHash != previousHash)
                {
                    result.FailedTransactionIds.Add(tx.Id);
                    continue;
                }

                var verifyResult = await VerifyTransactionAsync(tx.Id);
                if (verifyResult.SignatureValid)
                {
                    validCount++;
                }
                else
                {
                    result.FailedTransactionIds.Add(tx.Id);
                }

                previousHash = tx.TransactionHash;
            }

            result.IsValid = result.FailedTransactionIds.Count == 0;
            result.ValidSignatures = validCount;
            result.Message = result.IsValid ? "Chain is valid" : $"Failed at {result.FailedTransactionIds.Count} transaction(s)";

            return result;
        }

        // ============================================================
        // FRAUD DETECTION
        // ============================================================

        public async Task<FraudResult> AnalyzeTransactionAsync(string memberNo, decimal amount, string transactionType)
        {
            var result = new FraudResult
            {
                RiskScore = 0,
                Flags = new List<string>(),
                IsSuspicious = false,
                ShouldBlock = false
            };

            var recentTransactions = await _context.Contribs
                .Where(c => c.MemberNo == memberNo)
                .OrderByDescending(c => c.Id)
                .Take(10)
                .ToListAsync();

            var avgAmount = recentTransactions.Any() ? recentTransactions.Average(c => c.Amount ?? 0) : 0;
            if (avgAmount > 0 && amount > avgAmount * 3)
            {
                result.RiskScore += 30;
                result.Flags.Add($"Amount {amount:C} is 3x above average {avgAmount:C}");
            }

            var lastHour = DateTime.UtcNow.AddHours(-1);
            var recentCount = recentTransactions.Count(c => c.ContrDate >= lastHour);
            if (recentCount >= 5)
            {
                result.RiskScore += 25;
                result.Flags.Add($"{recentCount} transactions in the last hour");
            }

            if (amount % 1000 == 0 && amount > 10000)
            {
                result.RiskScore += 10;
                result.Flags.Add($"Round number amount {amount:C} may indicate testing");
            }

            if (amount < 100 && recentTransactions.Any(c => (c.Amount ?? 0) > 10000))
            {
                result.RiskScore += 15;
                result.Flags.Add("Small amount after large transaction");
            }

            result.IsSuspicious = result.RiskScore >= 40;
            result.ShouldBlock = result.RiskScore >= 70;

            if (result.RiskScore >= 70)
            {
                result.Recommendation = "Block transaction and notify admin";
            }
            else if (result.RiskScore >= 40)
            {
                result.Recommendation = "Flag for review";
            }
            else
            {
                result.Recommendation = "Allow transaction";
            }

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            if (member != null)
            {
                member.FraudRiskScore = result.RiskScore;
                member.LastFraudAssessmentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return result;
        }

    }
}