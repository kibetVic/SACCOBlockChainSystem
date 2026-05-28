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
        Task<WalletResult> CreateWalletForMemberAsync(int memberId, string memberNo, string companyCode);
        Task<Wallet> GetWalletByMemberNoAsync(string memberNo);
        Task<bool> HasWalletAsync(string memberNo);
        Task<WalletInfo> GetWalletInfoAsync(string memberNo);
        Task<Wallet> GetWalletByMemberIdAsync(int memberId);
        string ComputeHash(string data);
        Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData);
        Task<bool> VerifySignatureAsync(string memberNo, string data, string signature);
        Task<VerificationResult> VerifyTransactionAsync(int contribId);
        Task<ChainVerificationResult> VerifyMemberChainAsync(string memberNo);
        Task<ChainVerificationResult> VerifyMemberChainWithGenesisAsync(string memberNo);
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

        public string ComputeHash(string data)
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

        /// <summary>
        /// Verifies the transaction chain including the genesis anchor (wallet address)
        /// </summary>
        public async Task<ChainVerificationResult> VerifyMemberChainWithGenesisAsync(string memberNo)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            if (member == null)
            {
                return new ChainVerificationResult
                {
                    IsValid = false,
                    Message = "Member not found",
                    TotalTransactions = 0,
                    ValidSignatures = 0,
                    FailedTransactionIds = new List<int>()
                };
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);

            var transactions = await _context.Contribs
                .Where(c => c.MemberNo == memberNo)
                .OrderBy(c => c.Id)
                .ToListAsync();

            var result = new ChainVerificationResult
            {
                TotalTransactions = transactions.Count,
                FailedTransactionIds = new List<int>(),
                ValidSignatures = 0
            };

            if (transactions.Count == 0)
            {
                result.IsValid = true;
                result.Message = "No transactions to verify";
                return result;
            }

            // Verify FIRST transaction's genesis hash matches wallet address
            var firstTx = transactions.First();

            // IMPORTANT: Use the RAW wallet address for comparison (not hashed)
            string expectedGenesisHash = wallet?.Address ?? $"GENESIS-{member.MemberNo}";

            _logger.LogInformation($"Verifying genesis for member {memberNo}");
            _logger.LogInformation($"   Expected: {expectedGenesisHash}");
            _logger.LogInformation($"   Found: {firstTx.PreviousTransactionHash ?? "NULL"}");

            if (firstTx.PreviousTransactionHash != expectedGenesisHash)
            {
                result.FailedTransactionIds.Add(firstTx.Id);
                result.Message = $"Genesis hash mismatch! Expected: {expectedGenesisHash}, Found: {firstTx.PreviousTransactionHash}";
                result.IsValid = false;
                _logger.LogWarning(result.Message);
                return result;
            }

            _logger.LogInformation($"Genesis hash verified for member {memberNo}: {expectedGenesisHash}");

            // Verify remaining chain
            string previousHash = firstTx.TransactionHash;
            int validCount = firstTx.IsSignatureVerified == true ? 1 : 0;

            for (int i = 1; i < transactions.Count; i++)
            {
                var tx = transactions[i];

                if (tx.PreviousTransactionHash != previousHash)
                {
                    result.FailedTransactionIds.Add(tx.Id);
                    result.Message = $"Chain broken at transaction {tx.Id}";
                    break;
                }

                // Verify signature
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
            result.Message = result.IsValid
                ? $"Chain is valid with correct genesis anchor ✅ - {validCount}/{transactions.Count} verified"
                : $"Failed at {result.FailedTransactionIds.Count} transaction(s) ❌";

            return result;
        }

        /// <summary>
        /// Verifies a transaction by ID using strict ECDsa.VerifyData
        /// </summary>

        public async Task<VerificationResult> VerifyTransactionAsync(int contribId)
        {
            var contrib = await _context.Contribs
                .Include(c => c.SharescodeNavigation)
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

            // Get share type to determine contribution category
            var shareType = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == contrib.Sharescode && st.CompanyCode == contrib.CompanyCode);

            // Determine contribution category
            string contributionCategory = DetermineContributionCategory(shareType, contrib.Remarks, contrib.Amount ?? 0);

            // Recreate the EXACT transaction data that was signed
            var txData = new
            {
                MemberNo = contrib.MemberNo,
                Amount = contrib.Amount,
                TransactionDate = contrib.ContrDate?.ToString("o"),
                SharesCode = contrib.Sharescode,
                ReceiptNo = contrib.ReceiptNo,
                TransactionNo = contrib.TransactionNo,
                CompanyCode = contrib.CompanyCode,
                ContributionCategory = contributionCategory
            };

            var canonicalData = CreateCanonicalString(txData, contrib.TransactionSequence ?? 0);

            // DEBUG: Log the canonical data for comparison
            _logger.LogInformation($"=== VERIFICATION DEBUG ===");
            _logger.LogInformation($"Transaction ID: {contribId}");
            _logger.LogInformation($"Canonical Data: {canonicalData}");
            _logger.LogInformation($"Stored Signature: {contrib.TransactionSignature?.Substring(0, Math.Min(50, contrib.TransactionSignature?.Length ?? 0))}...");
            _logger.LogInformation($"Stored Nonce: {contrib.TransactionSequence}");
            _logger.LogInformation($"Member No: {contrib.MemberNo}");

            // STRICT VERIFICATION
            bool isValid = await VerifySignatureAsync(member.MemberNo, canonicalData, contrib.TransactionSignature);

            _logger.LogInformation($"Verification Result: {(isValid ? "VALID ✅" : "INVALID ❌")}");

            // Check chain integrity
            bool chainValid = true;
            if (!string.IsNullOrEmpty(contrib.PreviousTransactionHash))
            {
                var prevTx = await _context.Contribs
                    .Where(c => c.MemberNo == contrib.MemberNo && c.Id < contrib.Id)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                if (prevTx != null && prevTx.TransactionHash != contrib.PreviousTransactionHash)
                {
                    chainValid = false;
                    _logger.LogWarning($"Chain broken at transaction {contrib.Id}: Expected {prevTx.TransactionHash}, Got {contrib.PreviousTransactionHash}");
                }
            }

            // Update verification status
            contrib.IsSignatureVerified = isValid;
            contrib.SignatureVerifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated IsSignatureVerified = {isValid} for transaction {contribId}");

            return new VerificationResult
            {
                IsValid = isValid && chainValid,
                SignatureValid = isValid,
                ChainValid = chainValid,
                Message = isValid && chainValid ? "Verified ✅" : (isValid ? "Chain broken ❌" : "Invalid signature ❌")
            };
        }

        //public async Task<VerificationResult> VerifyTransactionAsync(int contribId)
        //{
        //    var contrib = await _context.Contribs
        //        .Include(c => c.SharescodeNavigation)
        //        .FirstOrDefaultAsync(c => c.Id == contribId);

        //    if (contrib == null)
        //    {
        //        return new VerificationResult { IsValid = false, Message = "Transaction not found" };
        //    }

        //    if (string.IsNullOrEmpty(contrib.TransactionSignature))
        //    {
        //        return new VerificationResult { IsValid = false, Message = "No signature found" };
        //    }

        //    var member = await _context.Members
        //        .FirstOrDefaultAsync(m => m.MemberNo == contrib.MemberNo);

        //    if (member == null)
        //    {
        //        return new VerificationResult { IsValid = false, Message = "Member not found" };
        //    }

        //    // Get share type to determine contribution category
        //    var shareType = await _context.Sharetypes
        //        .FirstOrDefaultAsync(st => st.SharesCode == contrib.Sharescode && st.CompanyCode == contrib.CompanyCode);

        //    // Determine contribution category (same logic as in ContributionService)
        //    string contributionCategory = DetermineContributionCategory(shareType, contrib.Remarks, contrib.Amount ?? 0);

        //    // Recreate the EXACT transaction data that was signed (MUST MATCH Signing)
        //    var txData = new
        //    {
        //        MemberNo = contrib.MemberNo,
        //        Amount = contrib.Amount,
        //        TransactionDate = contrib.ContrDate?.ToString("o"),
        //        SharesCode = contrib.Sharescode,
        //        ReceiptNo = contrib.ReceiptNo,
        //        TransactionNo = contrib.TransactionNo,
        //        CompanyCode = contrib.CompanyCode,
        //        ContributionCategory = contributionCategory 
        //    };

        //    var canonicalData = CreateCanonicalString(txData, contrib.TransactionSequence ?? 0);

        //    _logger.LogDebug($"Verifying transaction {contribId}");
        //    _logger.LogDebug($"Canonical data: {canonicalData}");

        //    // STRICT VERIFICATION - Using ECDsa.VerifyData
        //    bool isValid = await VerifySignatureAsync(member.MemberNo, canonicalData, contrib.TransactionSignature);

        //    // Check chain integrity
        //    bool chainValid = true;
        //    if (!string.IsNullOrEmpty(contrib.PreviousTransactionHash))
        //    {
        //        var prevTx = await _context.Contribs
        //            .Where(c => c.MemberNo == contrib.MemberNo && c.Id < contrib.Id)
        //            .OrderByDescending(c => c.Id)
        //            .FirstOrDefaultAsync();

        //        if (prevTx != null && prevTx.TransactionHash != contrib.PreviousTransactionHash)
        //        {
        //            chainValid = false;
        //            _logger.LogWarning($"Chain broken at transaction {contrib.Id}: Expected {prevTx.TransactionHash}, Got {contrib.PreviousTransactionHash}");
        //        }
        //    }

        //    // Update verification status
        //    contrib.IsSignatureVerified = isValid;
        //    contrib.SignatureVerifiedAt = DateTime.UtcNow;
        //    await _context.SaveChangesAsync();

        //    return new VerificationResult
        //    {
        //        IsValid = isValid && chainValid,
        //        SignatureValid = isValid,
        //        ChainValid = chainValid,
        //        Message = isValid && chainValid ? "Verified ✅" : (isValid ? "Chain broken ❌" : "Invalid signature ❌")
        //    };
        //}

        /// <summary>
        /// Helper method to determine contribution category (must match the one in ContributionService)
        /// </summary>
        private string DetermineContributionCategory(Sharetype shareType, string remarks, decimal amount)
        {
            if (shareType == null) return "SHARE_CAPITAL";

            var shareTypeName = (shareType.SharesType ?? shareType.SharesCode ?? "").ToLower();
            var remarksLower = (remarks ?? "").ToLower();

            // Check for DEPOSIT/SAVINGS
            string[] depositKeywords = { "deposit", "savings", "saving", "voluntary", "welfare" };
            foreach (var keyword in depositKeywords)
            {
                if (shareTypeName.Contains(keyword) || remarksLower.Contains(keyword))
                    return "DEPOSIT";
            }

            // Check for REGISTRATION FEE
            string[] regFeeKeywords = { "reg fee", "registration", "fee", "joining fee", "membership fee" };
            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeName.Contains(keyword) || remarksLower.Contains(keyword))
                    return "REGISTRATION_FEE";
            }

            // Check for DONOR
            string[] donorKeywords = { "donor", "donation", "gift", "grant" };
            foreach (var keyword in donorKeywords)
            {
                if (shareTypeName.Contains(keyword) || remarksLower.Contains(keyword))
                    return "DONOR";
            }

            // Check for LOAN REPAYMENT
            string[] loanKeywords = { "loan", "repayment", "installment" };
            foreach (var keyword in loanKeywords)
            {
                if (shareTypeName.Contains(keyword) || remarksLower.Contains(keyword))
                    return "LOAN_REPAYMENT";
            }

            // Check for PASSBOOK
            string[] passbookKeywords = { "passbook", "pass book", "ledger" };
            foreach (var keyword in passbookKeywords)
            {
                if (shareTypeName.Contains(keyword))
                    return "PASSBOOK";
            }

            // Check boolean flags
            if (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true))
                return "DEPOSIT";

            if (shareType.Issharecapital == 0 && shareType.UsedToGuarantee == false && shareType.UsedToOffset == false && shareType.Withdrawable == false)
                return "REGISTRATION_FEE";

            if (shareType.IsMainShares == true || shareType.Issharecapital == 1)
                return "SHARE_CAPITAL";

            return "SHARE_CAPITAL";
        }

        public async Task<WalletResult> CreateWalletForMemberAsync(int memberId, string memberNo, string companyCode)
        {
            _logger.LogInformation($"Creating wallet for member: {memberNo}, Company: {companyCode}");

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                return new WalletResult { Success = false, Message = "Member not found" };
            }

            _logger.LogInformation($"Found member: ID={member.Id}, MemberNo={member.MemberNo}, CompanyCode={member.CompanyCode}");

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
                var wallet = Wallet.CreateNewWallet(member.Id, member.MemberNo, companyCode);

                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();

                member.WalletAddress = wallet.Address;
                member.IsWalletActive = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Wallet created for member {memberNo} (ID: {member.Id}): {wallet.Address}");

                return new WalletResult
                {
                    Success = true,
                    WalletAddress = wallet.Address,
                    Message = "Wallet created successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create wallet for member {memberNo}");
                return new WalletResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<Wallet> GetWalletByMemberNoAsync(string memberNo)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);
        }

        public async Task<Wallet> GetWalletByMemberIdAsync(int memberId)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.MemberId == memberId);
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

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);
                if (member != null)
                {
                    member.TransactionNonce = wallet.TransactionNonce;
                    member.LastTransactionHash = transactionHash;
                    member.LastTransactionSignature = signatureBase64;
                    member.LastSignatureAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Transaction signed for member {memberNo}, nonce: {wallet.TransactionNonce}");

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
                _logger.LogError(ex, $"Failed to sign transaction for member {memberNo}");
                return new SigningResult { Success = false, Message = ex.Message };
            }
        }

        // ============================================================
        // VERIFICATION - USING STRICT ECDsa.VerifyData
        // ============================================================

        /// <summary>
        /// Verifies a digital signature using the member's public key
        /// Uses strict ECDsa.VerifyData method as specified
        /// </summary>
        public async Task<bool> VerifySignatureAsync(string memberNo, string data, string signature)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.memberNo == memberNo);

            if (wallet == null || string.IsNullOrEmpty(wallet.PublicKey))
            {
                _logger.LogWarning($"VerifySignatureAsync: No wallet or public key found for member {memberNo}");
                return false;
            }

            try
            {
                // Convert the data to bytes
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);

                // Convert the signature from Base64
                byte[] signatureBytes = Convert.FromBase64String(signature);

                // Convert the public key from Base64
                byte[] publicKeyBytes = Convert.FromBase64String(wallet.PublicKey);

                // Create ECDsa instance and import the public key
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                // STRICT VERIFICATION - Using the exact method you specified
                bool isValid = ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);

                _logger.LogInformation($"VerifySignatureAsync for member {memberNo}: {(isValid ? "VALID ✅" : "INVALID ❌")}");

                return isValid;
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, $"VerifySignatureAsync: Invalid Base64 format for member {memberNo}");
                return false;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, $"VerifySignatureAsync: Cryptographic error for member {memberNo}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"VerifySignatureAsync: Unexpected error for member {memberNo}");
                return false;
            }
        }


        /// <summary>
        /// Verifies the entire transaction chain for a member
        /// </summary>
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

            _logger.LogInformation($"Starting chain verification for member {memberNo}, {transactions.Count} transactions");

            foreach (var tx in transactions)
            {
                // Check chain continuity
                if (previousHash != null && tx.PreviousTransactionHash != previousHash)
                {
                    result.FailedTransactionIds.Add(tx.Id);
                    _logger.LogWarning($"Chain broken at transaction {tx.Id}: Expected {previousHash}, Got {tx.PreviousTransactionHash}");
                    continue;
                }

                // Verify signature using strict ECDsa.VerifyData
                var verifyResult = await VerifyTransactionAsync(tx.Id);
                if (verifyResult.SignatureValid)
                {
                    validCount++;
                }
                else
                {
                    result.FailedTransactionIds.Add(tx.Id);
                    _logger.LogWarning($"Invalid signature at transaction {tx.Id}");
                }

                previousHash = tx.TransactionHash;
            }

            result.IsValid = result.FailedTransactionIds.Count == 0;
            result.ValidSignatures = validCount;
            result.Message = result.IsValid
                ? $"Chain is valid ✅ - {validCount}/{transactions.Count} signatures verified"
                : $"Failed at {result.FailedTransactionIds.Count} transaction(s) ❌";

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

    //public class CryptoService : ICryptoService
    //{
    //    private readonly ApplicationDbContext _context;
    //    private readonly ILogger<CryptoService> _logger;
    //    private readonly IHttpContextAccessor _httpContextAccessor;
    //    private readonly AuditTrailService _auditService;

    //    public CryptoService(
    //        ApplicationDbContext context,
    //        ILogger<CryptoService> logger,
    //        IHttpContextAccessor httpContextAccessor,
    //        AuditTrailService auditService)
    //    {
    //        _context = context;
    //        _logger = logger;
    //        _httpContextAccessor = httpContextAccessor;
    //        _auditService = auditService;
    //    }

    //    // ============================================================
    //    // PRIVATE HELPERS
    //    // ============================================================

    //    private byte[] DecryptPrivateKey(string encryptedPrivateKey)
    //    {
    //        var decrypted = EncryptionHelper.Decrypt(encryptedPrivateKey);
    //        return Convert.FromBase64String(decrypted);
    //    }

    //    private string ComputeHash(string data)
    //    {
    //        using var sha256 = SHA256.Create();
    //        var bytes = Encoding.UTF8.GetBytes(data);
    //        var hash = sha256.ComputeHash(bytes);
    //        return Convert.ToHexString(hash).ToLower();
    //    }

    //    private string CreateCanonicalString(object data, long nonce)
    //    {
    //        var json = System.Text.Json.JsonSerializer.Serialize(data);
    //        return $"{json}|nonce:{nonce}";
    //    }

    //    // ============================================================
    //    // WALLET MANAGEMENT
    //    // ============================================================
    //    public async Task<WalletResult> CreateWalletForMemberAsync(int memberId, string memberNo, string companyCode)
    //    {
    //        //var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == memberId);

    //        _logger.LogInformation($"Creating wallet for member: {memberNo}, Company: {companyCode}");

    //        // Find member by MemberNo AND CompanyCode
    //        var member = await _context.Members
    //            .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

    //        if (member == null)
    //        {
    //            return new WalletResult { Success = false, Message = "Member not found" };
    //        }

    //        _logger.LogInformation($"Found member: ID={member.Id}, MemberNo={member.MemberNo}, CompanyCode={member.CompanyCode}");

    //        // Check if wallet already exists
    //        var existingWallet = await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.memberNo == memberNo && w.CompanyCode == companyCode);

    //        if (existingWallet != null)
    //        {
    //            return new WalletResult
    //            {
    //                Success = true,
    //                WalletAddress = existingWallet.Address,
    //                Message = "Wallet already exists"
    //            };
    //        }

    //        try
    //        {
    //            // Create wallet with MemberId AND MemberNo
    //            var wallet = Wallet.CreateNewWallet(member.Id, member.MemberNo, companyCode);

    //            _context.Wallets.Add(wallet);
    //            await _context.SaveChangesAsync();

    //            // =============================================
    //            // CRITICAL: Update Member table with WalletAddress
    //            // =============================================
    //            member.WalletAddress = wallet.Address;
    //            member.IsWalletActive = true;
    //            await _context.SaveChangesAsync();

    //            _logger.LogInformation($"Wallet created for member {memberNo} (ID: {member.Id}): {wallet.Address}");
    //            _logger.LogInformation($"Updated Member {memberNo} with WalletAddress: {wallet.Address}");

    //            return new WalletResult
    //            {
    //                Success = true,
    //                WalletAddress = wallet.Address,
    //                Message = "Wallet created successfully"
    //            };
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, $"Failed to create wallet for member {memberNo}");
    //            return new WalletResult { Success = false, Message = ex.Message };
    //        }
    //    }


    //    public async Task<Wallet> GetWalletByMemberNoAsync(string memberNo)
    //    {
    //        return await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.memberNo == memberNo);
    //    }

    //    public async Task<bool> HasWalletAsync(string memberNo)
    //    {
    //        return await _context.Wallets.AnyAsync(w => w.memberNo == memberNo);
    //    }

    //    public async Task<WalletInfo> GetWalletInfoAsync(string memberNo)
    //    {
    //        var wallet = await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.memberNo == memberNo);

    //        if (wallet == null) return null;

    //        return new WalletInfo
    //        {
    //            WalletAddress = wallet.Address,
    //            CreatedAt = wallet.CreatedAt,
    //            IsActive = wallet.IsActive,
    //            TransactionNonce = wallet.TransactionNonce,
    //            Balance = wallet.Balance
    //        };
    //    }

    //    public async Task<Wallet> GetWalletByMemberIdAsync(int memberId)
    //    {
    //        return await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.MemberId == memberId);
    //    }

       
    //    // ============================================================
    //    // SIGNING TRANSACTIONS
    //    // ============================================================

    //    public async Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData)
    //    {
    //        // Find wallet by MemberNo
    //        var wallet = await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.memberNo == memberNo);

    //        if (wallet == null)
    //        {
    //            return new SigningResult { Success = false, Message = "Member has no wallet. Create wallet first." };
    //        }

    //        if (!wallet.IsActive)
    //        {
    //            return new SigningResult { Success = false, Message = "Wallet is inactive" };
    //        }

    //        try
    //        {
    //            wallet.TransactionNonce++;
    //            wallet.LastUsedAt = DateTime.UtcNow;

    //            var canonicalData = CreateCanonicalString(transactionData, wallet.TransactionNonce);
    //            var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

    //            var privateKeyBytes = DecryptPrivateKey(wallet.PrivateKeyEncrypted!);

    //            using var ecdsa = ECDsa.Create();
    //            ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

    //            var signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
    //            var signatureBase64 = Convert.ToBase64String(signature);
    //            var transactionHash = ComputeHash(canonicalData);

    //            // Also update the Member table's nonce for consistency
    //            var member = await _context.Members
    //                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);
    //            if (member != null)
    //            {
    //                member.TransactionNonce = wallet.TransactionNonce;
    //                member.LastTransactionHash = transactionHash;
    //                member.LastTransactionSignature = signatureBase64;
    //                member.LastSignatureAt = DateTime.UtcNow;
    //            }

    //            await _context.SaveChangesAsync();

    //            _logger.LogInformation($"Transaction signed for member {memberNo}, nonce: {wallet.TransactionNonce}");

    //            return new SigningResult
    //            {
    //                Success = true,
    //                Signature = signatureBase64,
    //                TransactionHash = transactionHash,
    //                Nonce = wallet.TransactionNonce,
    //                Message = "Transaction signed successfully"
    //            };
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, $"Failed to sign transaction for member {memberNo}");
    //            return new SigningResult { Success = false, Message = ex.Message };
    //        }
    //    }


    //    public async Task<bool> VerifySignatureAsync(string memberNo, string data, string signature)
    //    {
    //        var wallet = await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.memberNo == memberNo);

    //        if (wallet == null || string.IsNullOrEmpty(wallet.PublicKey))
    //        {
    //            return false;
    //        }

    //        try
    //        {
    //            var dataBytes = Encoding.UTF8.GetBytes(data);
    //            var signatureBytes = Convert.FromBase64String(signature);
    //            var publicKeyBytes = Convert.FromBase64String(wallet.PublicKey);

    //            using var ecdsa = ECDsa.Create();
    //            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

    //            return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Signature verification failed");
    //            return false;
    //        }
    //    }

    //    // ============================================================
    //    // VERIFICATION
    //    // ============================================================


    //    public async Task<ChainVerificationResult> VerifyMemberChainWithGenesisAsync(string memberNo)
    //    {
    //        var member = await _context.Members
    //            .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

    //        if (member == null)
    //        {
    //            return new ChainVerificationResult
    //            {
    //                IsValid = false,
    //                Message = "Member not found"
    //            };
    //        }

    //        var wallet = await _context.Wallets
    //            .FirstOrDefaultAsync(w => w.MemberId == member.Id);

    //        var transactions = await _context.Contribs
    //            .Where(c => c.MemberNo == memberNo)
    //            .OrderBy(c => c.Id)
    //            .ToListAsync();

    //        var result = new ChainVerificationResult
    //        {
    //            TotalTransactions = transactions.Count,
    //            FailedTransactionIds = new List<int>()
    //        };

    //        if (transactions.Count == 0)
    //        {
    //            result.IsValid = true;
    //            result.Message = "No transactions to verify";
    //            return result;
    //        }

    //        // Verify FIRST transaction's genesis hash matches wallet
    //        var firstTx = transactions.First();
    //        string expectedGenesisHash = wallet?.Address ?? $"GENESIS-{member.MemberNo}";

    //        if (firstTx.PreviousTransactionHash != expectedGenesisHash)
    //        {
    //            result.FailedTransactionIds.Add(firstTx.Id);
    //            result.Message = $"Genesis hash mismatch! Expected: {expectedGenesisHash}, Found: {firstTx.PreviousTransactionHash}";
    //            _logger.LogWarning(result.Message);
    //            return result;
    //        }

    //        // Verify remaining chain
    //        string previousHash = firstTx.TransactionHash;

    //        for (int i = 1; i < transactions.Count; i++)
    //        {
    //            var tx = transactions[i];

    //            if (tx.PreviousTransactionHash != previousHash)
    //            {
    //                result.FailedTransactionIds.Add(tx.Id);
    //                result.Message = $"Chain broken at transaction {tx.Id}";
    //                break;
    //            }

    //            var verifyResult = await VerifyTransactionAsync(tx.Id);
    //            if (!verifyResult.SignatureValid)
    //            {
    //                result.FailedTransactionIds.Add(tx.Id);
    //            }

    //            previousHash = tx.TransactionHash;
    //        }

    //        result.IsValid = result.FailedTransactionIds.Count == 0;
    //        result.ValidSignatures = transactions.Count - result.FailedTransactionIds.Count;
    //        result.Message = result.IsValid ? "Chain is valid with correct genesis anchor" : $"Failed at {result.FailedTransactionIds.Count} transaction(s)";

    //        return result;
    //    }

    //    public async Task<VerificationResult> VerifyTransactionAsync(int contribId)
    //    {
    //        var contrib = await _context.Contribs
    //            .FirstOrDefaultAsync(c => c.Id == contribId);

    //        if (contrib == null)
    //        {
    //            return new VerificationResult { IsValid = false, Message = "Transaction not found" };
    //        }

    //        if (string.IsNullOrEmpty(contrib.TransactionSignature))
    //        {
    //            return new VerificationResult { IsValid = false, Message = "No signature found" };
    //        }

    //        var member = await _context.Members
    //            .FirstOrDefaultAsync(m => m.MemberNo == contrib.MemberNo);

    //        if (member == null)
    //        {
    //            return new VerificationResult { IsValid = false, Message = "Member not found" };
    //        }

    //        var txData = new
    //        {
    //            contrib.MemberNo,
    //            contrib.Amount,
    //            TransactionDate = contrib.ContrDate?.ToString("o"),
    //            contrib.Sharescode,
    //            contrib.ReceiptNo,
    //            contrib.TransactionNo,
    //            contrib.CompanyCode
    //        };

    //        var canonicalData = CreateCanonicalString(txData, contrib.TransactionSequence ?? 0);
    //        var isValid = await VerifySignatureAsync(member.MobileNo, canonicalData, contrib.TransactionSignature);

    //        contrib.IsSignatureVerified = isValid;
    //        contrib.SignatureVerifiedAt = DateTime.UtcNow;
    //        await _context.SaveChangesAsync();

    //        return new VerificationResult
    //        {
    //            IsValid = isValid,
    //            SignatureValid = isValid,
    //            ChainValid = true,
    //            Message = isValid ? "Verified" : "Verification failed"
    //        };
    //    }

    //    public async Task<ChainVerificationResult> VerifyMemberChainAsync(string memberNo)
    //    {
    //        var transactions = await _context.Contribs
    //            .Where(c => c.MemberNo == memberNo)
    //            .OrderBy(c => c.Id)
    //            .ToListAsync();

    //        var result = new ChainVerificationResult
    //        {
    //            TotalTransactions = transactions.Count,
    //            FailedTransactionIds = new List<int>()
    //        };

    //        string previousHash = null;
    //        int validCount = 0;

    //        foreach (var tx in transactions)
    //        {
    //            if (previousHash != null && tx.PreviousTransactionHash != previousHash)
    //            {
    //                result.FailedTransactionIds.Add(tx.Id);
    //                continue;
    //            }

    //            var verifyResult = await VerifyTransactionAsync(tx.Id);
    //            if (verifyResult.SignatureValid)
    //            {
    //                validCount++;
    //            }
    //            else
    //            {
    //                result.FailedTransactionIds.Add(tx.Id);
    //            }

    //            previousHash = tx.TransactionHash;
    //        }

    //        result.IsValid = result.FailedTransactionIds.Count == 0;
    //        result.ValidSignatures = validCount;
    //        result.Message = result.IsValid ? "Chain is valid" : $"Failed at {result.FailedTransactionIds.Count} transaction(s)";

    //        return result;
    //    }

    //    // ============================================================
    //    // FRAUD DETECTION
    //    // ============================================================

    //    public async Task<FraudResult> AnalyzeTransactionAsync(string memberNo, decimal amount, string transactionType)
    //    {
    //        var result = new FraudResult
    //        {
    //            RiskScore = 0,
    //            Flags = new List<string>(),
    //            IsSuspicious = false,
    //            ShouldBlock = false
    //        };

    //        var recentTransactions = await _context.Contribs
    //            .Where(c => c.MemberNo == memberNo)
    //            .OrderByDescending(c => c.Id)
    //            .Take(10)
    //            .ToListAsync();

    //        var avgAmount = recentTransactions.Any() ? recentTransactions.Average(c => c.Amount ?? 0) : 0;
    //        if (avgAmount > 0 && amount > avgAmount * 3)
    //        {
    //            result.RiskScore += 30;
    //            result.Flags.Add($"Amount {amount:C} is 3x above average {avgAmount:C}");
    //        }

    //        var lastHour = DateTime.UtcNow.AddHours(-1);
    //        var recentCount = recentTransactions.Count(c => c.ContrDate >= lastHour);
    //        if (recentCount >= 5)
    //        {
    //            result.RiskScore += 25;
    //            result.Flags.Add($"{recentCount} transactions in the last hour");
    //        }

    //        if (amount % 1000 == 0 && amount > 10000)
    //        {
    //            result.RiskScore += 10;
    //            result.Flags.Add($"Round number amount {amount:C} may indicate testing");
    //        }

    //        if (amount < 100 && recentTransactions.Any(c => (c.Amount ?? 0) > 10000))
    //        {
    //            result.RiskScore += 15;
    //            result.Flags.Add("Small amount after large transaction");
    //        }

    //        result.IsSuspicious = result.RiskScore >= 40;
    //        result.ShouldBlock = result.RiskScore >= 70;

    //        if (result.RiskScore >= 70)
    //        {
    //            result.Recommendation = "Block transaction and notify admin";
    //        }
    //        else if (result.RiskScore >= 40)
    //        {
    //            result.Recommendation = "Flag for review";
    //        }
    //        else
    //        {
    //            result.Recommendation = "Allow transaction";
    //        }

    //        var member = await _context.Members
    //            .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

    //        if (member != null)
    //        {
    //            member.FraudRiskScore = result.RiskScore;
    //            member.LastFraudAssessmentAt = DateTime.UtcNow;
    //            await _context.SaveChangesAsync();
    //        }

    //        return result;
    //    }

    //    string ICryptoService.ComputeHash(string data)
    //    {
    //        return ComputeHash(data);
    //    }
    //}
}