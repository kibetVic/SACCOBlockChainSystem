using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{

    public interface ICryptoService
    {
        // Wallet Management
        Task<bool> HasWalletAsync(string memberNo);
        Task<WalletResult> CreateWalletForMemberAsync(int memberId, string memberNo, string companyCode);
        Task<Wallet> GetWalletByMemberIdAsync(int memberId);
        Task<Wallet> GetWalletByMemberNoAsync(string memberNo);

        // Transaction Signing
        Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData);

        // Verification
        Task<VerificationResult> VerifyTransactionAsync(int transactionId);
        Task<ChainVerificationResult> VerifyMemberChainWithGenesisAsync(string memberNo);

        // Fraud Detection
        Task<FraudAnalysisResult> AnalyzeTransactionAsync(string memberNo, decimal amount, string category);

        // Utilities
        string ComputeHash(string data);
        string GetCanonicalData(object transactionData);
        Task<WalletInfo> GetWalletInfoAsync(string memberNo);
        Task<bool> VerifySignatureAsync(string memberNo, string data, string signature);
        Task<ChainVerificationResult> VerifyMemberChainAsync(string memberNo);
    }

    public class WalletResult
    {
        public bool Success { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Wallet? Wallet { get; set; }
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
        public bool IsGenesisTransaction { get; set; }
        public string GenesisAnchor { get; set; } = string.Empty;
    }

    public class ChainVerificationResult
    {
        public bool IsValid { get; set; }
        public int TotalTransactions { get; set; }
        public int ValidSignatures { get; set; }
        public List<int> FailedTransactionIds { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public List<int> BrokenChainIds { get; set; } = new();
        public string GenesisAnchor { get; set; } = string.Empty;
        public bool IsFirstTransactionValid { get; set; }
    }

    public class FraudResult
    {
        public int RiskScore { get; set; }
        public bool IsSuspicious { get; set; }
        public List<string> Flags { get; set; } = new();
        public bool ShouldBlock { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class FraudAnalysisResult
    {
        public bool IsSuspicious { get; set; }
        public bool ShouldBlock { get; set; }
        public List<string> Flags { get; set; } = new();
        public int Confidence { get; set; }
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
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private string CreateCanonicalString(object data, long nonce)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            return $"{json}|nonce:{nonce}";
        }

        #region Wallet Management

        public async Task<bool> HasWalletAsync(string memberNo)
        {
            return await _context.Wallets.AnyAsync(w => w.memberNo == memberNo);
        }

        public async Task<WalletResult> CreateWalletForMemberAsync(int memberId, string memberNo, string companyCode)
        {
            try
            {
                // Check if wallet already exists
                var existing = await _context.Wallets.FirstOrDefaultAsync(w => w.memberNo == memberNo);
                if (existing != null)
                {
                    return new WalletResult { Success = true, Message = "Wallet already exists", Wallet = existing };
                }

                // Generate new wallet using ECDSA P-256
                using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

                // Export keys
                var privateKeyBytes = ecdsa.ExportECPrivateKey();
                var privateKey = Convert.ToBase64String(privateKeyBytes);
                var publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

                // Generate wallet address from public key
                byte[] publicBytes = Encoding.UTF8.GetBytes(publicKey);
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(publicBytes);
                string walletAddress = Convert.ToHexString(hash).Substring(0, 40);

                // Encrypt private key
                var encryptedPrivateKey = EncryptionHelper.Encrypt(privateKey);

                var wallet = new Wallet
                {
                    MemberId = memberId,
                    memberNo = memberNo,
                    CompanyCode = companyCode,
                    Address = walletAddress,
                    PublicKey = publicKey,
                    PrivateKeyEncrypted = encryptedPrivateKey,
                    Balance = 0,
                    CapitalBalance = 0,
                    DepositBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created wallet for member {memberNo}: {walletAddress}");

                return new WalletResult
                {
                    Success = true,
                    Message = "Wallet created successfully",
                    Wallet = wallet
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating wallet for member {memberNo}");
                return new WalletResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<Wallet> GetWalletByMemberIdAsync(int memberId)
        {
            return await _context.Wallets.FirstOrDefaultAsync(w => w.MemberId == memberId);
        }

        public async Task<Wallet> GetWalletByMemberNoAsync(string memberNo)
        {
            return await _context.Wallets.FirstOrDefaultAsync(w => w.memberNo == memberNo);
        }

        #endregion

        #region Transaction Signing

        public async Task<SigningResult> SignTransactionAsync(string memberNo, object transactionData)
        {
            try
            {
                _logger.LogInformation($"Signing transaction for member: {memberNo}");

                // Get member's wallet
                var wallet = await GetWalletByMemberNoAsync(memberNo);
                if (wallet == null)
                {
                    return new SigningResult
                    {
                        Success = false,
                        Message = "Wallet not found for member"
                    };
                }

                if (string.IsNullOrEmpty(wallet.PrivateKeyEncrypted))
                {
                    return new SigningResult
                    {
                        Success = false,
                        Message = "Private key not found in wallet"
                    };
                }

                // Decrypt private key
                var privateKeyBase64 = EncryptionHelper.Decrypt(wallet.PrivateKeyEncrypted);
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

                // Get canonical data
                var canonicalData = GetCanonicalData(transactionData);
                var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                // Generate transaction hash (SHA-256)
                using var sha = SHA256.Create();
                var hashBytes = sha.ComputeHash(dataBytes);
                var transactionHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                // Sign using ECDSA P-256
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

                var signatureBytes = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
                var signature = Convert.ToBase64String(signatureBytes);

                // Get next sequence number
                var lastTransaction = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                var nonce = (lastTransaction?.TransactionSequence ?? 0) + 1;

                _logger.LogInformation($"Transaction signed successfully for member {memberNo}: Hash={transactionHash.Substring(0, 16)}...");

                return new SigningResult
                {
                    Success = true,
                    Signature = signature,
                    TransactionHash = transactionHash,
                    Nonce = nonce,
                    Message = "Transaction signed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error signing transaction for member {memberNo}");
                return new SigningResult { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region Transaction Verification
        public async Task<VerificationResult> VerifyTransactionAsync(int transactionId)
        {
            try
            {
                _logger.LogInformation($"Verifying transaction: {transactionId}");

                // Get transaction
                var transaction = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .FirstOrDefaultAsync(c => c.Id == transactionId);

                if (transaction == null)
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        Message = "Transaction not found"
                    };
                }

                var memberNo = transaction.MemberNo;
                if (string.IsNullOrEmpty(memberNo))
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        Message = "Transaction has no member number"
                    };
                }

                // Get wallet
                var wallet = await GetWalletByMemberNoAsync(memberNo);
                if (wallet == null)
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        Message = "Wallet not found for member"
                    };
                }

                // ============================================================
                // 1. VERIFY SIGNATURE - USING STORED CANONICAL DATA
                // ============================================================
                var signatureValid = false;
                var signatureMessage = "Signature invalid";

                if (!string.IsNullOrEmpty(transaction.TransactionSignature) &&
                    !string.IsNullOrEmpty(wallet.PublicKey))
                {
                    try
                    {
                        // CRITICAL: Use the stored canonical data
                        string canonicalData;

                        if (!string.IsNullOrEmpty(transaction.CanonicalData))
                        {
                            // ✅ Use the stored canonical data (what was actually signed)
                            canonicalData = transaction.CanonicalData;
                            _logger.LogInformation($"Using stored canonical data for transaction {transactionId}");
                        }
                        else
                        {
                            // ⚠️ Fallback: Reconstruct (for old transactions without CanonicalData)
                            var data = new
                            {
                                MemberNo = transaction.MemberNo ?? "",
                                Amount = transaction.Amount ?? 0,
                                TransactionDate = transaction.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                                SharesCode = transaction.Sharescode ?? "",
                                ReceiptNo = transaction.ReceiptNo ?? "",
                                TransactionNo = transaction.TransactionNo ?? "",
                                CompanyCode = transaction.CompanyCode ?? "",
                                ContributionCategory = DetermineContributionCategory(transaction)
                            };
                            canonicalData = GetCanonicalData(data);
                            _logger.LogWarning($"Reconstructed canonical data for transaction {transactionId} (fallback)");
                        }

                        // Verify signature
                        var publicKeyBytes = Convert.FromBase64String(wallet.PublicKey);
                        var signatureBytes = Convert.FromBase64String(transaction.TransactionSignature);
                        var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                        using var ecdsa = ECDsa.Create();
                        ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                        signatureValid = ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
                        signatureMessage = signatureValid ? "Signature valid" : "Signature invalid ❌";

                        _logger.LogInformation($"Signature verification for transaction {transactionId}: {(signatureValid ? "VALID ✅" : "INVALID ❌")}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error verifying signature for transaction {transactionId}");
                        signatureMessage = $"Signature verification error: {ex.Message}";
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(transaction.TransactionSignature))
                        signatureMessage = "Transaction has no signature";
                    else if (string.IsNullOrEmpty(wallet.PublicKey))
                        signatureMessage = "Member has no public key";
                }

                // ============================================================
                // 2. VERIFY CHAIN
                // ============================================================
                var chainValid = false;
                var chainMessage = "Chain invalid";
                var isGenesisTransaction = false;
                var genesisAnchor = string.Empty;

                if (!string.IsNullOrEmpty(transaction.PreviousTransactionHash))
                {
                    // Check if this is a genesis transaction (first for member)
                    var previousTransaction = await _context.Contribs
                        .Where(c => c.MemberNo == memberNo && c.Id < transactionId)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefaultAsync();

                    if (previousTransaction == null)
                    {
                        // This is the FIRST transaction - verify against wallet address
                        isGenesisTransaction = true;
                        genesisAnchor = wallet.Address;

                        // Compare previous hash with wallet address (genesis anchor)
                        chainValid = transaction.PreviousTransactionHash == wallet.Address;
                        chainMessage = chainValid ?
                            "Genesis transaction verified against wallet address" :
                            $"Genesis anchor mismatch: expected {wallet.Address}, got {transaction.PreviousTransactionHash}";
                    }
                    else
                    {
                        // Not first transaction - verify against previous transaction hash
                        chainValid = transaction.PreviousTransactionHash == previousTransaction.TransactionHash;
                        chainMessage = chainValid ?
                            "Chain link verified" :
                            $"Chain broken: expected {previousTransaction.TransactionHash}, got {transaction.PreviousTransactionHash}";
                    }
                }
                else
                {
                    // No previous hash - check if it's the first transaction
                    var anyPrevious = await _context.Contribs
                        .AnyAsync(c => c.MemberNo == memberNo && c.Id < transactionId);

                    if (!anyPrevious)
                    {
                        isGenesisTransaction = true;
                        genesisAnchor = wallet.Address;
                        chainValid = false;
                        chainMessage = $"Genesis transaction must have PreviousTransactionHash = wallet address ({wallet.Address})";
                    }
                    else
                    {
                        chainValid = false;
                        chainMessage = "Transaction has no previous hash but there are previous transactions";
                    }
                }

                // ============================================================
                // 3. OVERALL VALIDITY
                // ============================================================
                var isValid = signatureValid && chainValid;

                // Update transaction verification status if valid
                if (isValid && !(transaction.IsSignatureVerified ?? false))
                {
                    transaction.IsSignatureVerified = true;
                    transaction.SignatureVerifiedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // 4. RETURN RESULT
                // ============================================================
                return new VerificationResult
                {
                    IsValid = isValid,
                    SignatureValid = signatureValid,
                    ChainValid = chainValid,
                    Message = isValid ?
                        "✅ Transaction verified successfully" :
                        $"❌ Verification failed: {(signatureValid ? "" : "Invalid signature")}{(chainValid ? "" : (signatureValid ? " - Broken chain" : " and broken chain"))}",
                    IsGenesisTransaction = isGenesisTransaction,
                    GenesisAnchor = genesisAnchor
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying transaction {transactionId}");
                return new VerificationResult
                {
                    IsValid = false,
                    Message = $"Verification error: {ex.Message}"
                };
            }
        }

        #endregion

        #region Chain Verification

        public async Task<ChainVerificationResult> VerifyMemberChainWithGenesisAsync(string memberNo)
        {
            try
            {
                _logger.LogInformation($"Verifying chain for member: {memberNo}");

                var result = new ChainVerificationResult
                {
                    TotalTransactions = 0,
                    ValidSignatures = 0,
                    FailedTransactionIds = new List<int>(),
                    BrokenChainIds = new List<int>(),
                    IsValid = false
                };

                // Get wallet
                var wallet = await GetWalletByMemberNoAsync(memberNo);
                if (wallet == null)
                {
                    result.Message = "Wallet not found for member";
                    return result;
                }

                result.GenesisAnchor = wallet.Address;

                // Get all transactions for member in order
                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    result.Message = "No transactions found for this member";
                    result.IsValid = true; // Empty chain is valid
                    return result;
                }

                result.TotalTransactions = transactions.Count;

                // Verify each transaction
                string previousHash = null;
                bool isFirst = true;
                bool allValid = true;

                foreach (var tx in transactions)
                {
                    // Verify signature
                    var signatureValid = await VerifySingleTransactionSignature(tx, wallet);

                    if (signatureValid)
                    {
                        result.ValidSignatures++;
                    }
                    else
                    {
                        result.FailedTransactionIds.Add(tx.Id);
                        allValid = false;
                    }

                    // Verify chain link
                    bool chainLinked = false;

                    if (isFirst)
                    {
                        // First transaction - must link to wallet address
                        chainLinked = tx.PreviousTransactionHash == wallet.Address;
                        if (!chainLinked)
                        {
                            result.BrokenChainIds.Add(tx.Id);
                            allValid = false;
                        }
                        result.IsFirstTransactionValid = chainLinked;
                    }
                    else
                    {
                        // Subsequent transactions - link to previous hash
                        chainLinked = tx.PreviousTransactionHash == previousHash;
                        if (!chainLinked)
                        {
                            result.BrokenChainIds.Add(tx.Id);
                            allValid = false;
                        }
                    }

                    // Update transaction status if both valid
                    if (signatureValid && chainLinked)
                    {
                        if (!(tx.IsSignatureVerified ?? false))
                        {
                            tx.IsSignatureVerified = true;
                            tx.SignatureVerifiedAt = DateTime.Now;
                        }
                    }

                    previousHash = tx.TransactionHash;
                    isFirst = false;
                }

                await _context.SaveChangesAsync();

                result.IsValid = allValid && result.ValidSignatures == result.TotalTransactions;
                result.Message = result.IsValid ?
                    "✅ All transactions in chain are valid" :
                    $"❌ Chain verification failed: {result.FailedTransactionIds.Count} invalid signatures, {result.BrokenChainIds.Count} broken links";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying chain for member {memberNo}");
                return new ChainVerificationResult
                {
                    IsValid = false,
                    Message = $"Chain verification error: {ex.Message}"
                };
            }
        }

        private async Task<bool> VerifySingleTransactionSignature(Contrib transaction, Wallet wallet)
        {
            try
            {
                if (string.IsNullOrEmpty(transaction.TransactionSignature) ||
                    string.IsNullOrEmpty(wallet.PublicKey))
                {
                    return false;
                }

                // CRITICAL: Use stored canonical data if available
                string canonicalData;

                if (!string.IsNullOrEmpty(transaction.CanonicalData))
                {
                    // ✅ Use the stored canonical data
                    canonicalData = transaction.CanonicalData;
                }
                else
                {
                    // ⚠️ Fallback: Reconstruct
                    var data = new
                    {
                        MemberNo = transaction.MemberNo ?? "",
                        Amount = transaction.Amount ?? 0,
                        TransactionDate = transaction.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                        SharesCode = transaction.Sharescode ?? "",
                        ReceiptNo = transaction.ReceiptNo ?? "",
                        TransactionNo = transaction.TransactionNo ?? "",
                        CompanyCode = transaction.CompanyCode ?? "",
                        ContributionCategory = DetermineContributionCategory(transaction)
                    };
                    canonicalData = GetCanonicalData(data);
                }

                var publicKeyBytes = Convert.FromBase64String(wallet.PublicKey);
                var signatureBytes = Convert.FromBase64String(transaction.TransactionSignature);
                var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying signature for transaction {transaction.Id}");
                return false;
            }
        }

        #endregion
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

        private string DetermineContributionCategory(Contrib transaction)
        {
            // Simple determination - you can expand this
            var shareType = transaction.Sharescode?.ToLower() ?? "";

            if (shareType.Contains("reg") || shareType.Contains("fee"))
                return "REGISTRATION_FEE";
            if (shareType.Contains("deposit") || shareType.Contains("savings"))
                return "DEPOSIT";
            if (shareType.Contains("donor") || shareType.Contains("gift"))
                return "DONOR";
            if (shareType.Contains("loan") || shareType.Contains("repayment"))
                return "LOAN_REPAYMENT";
            if (shareType.Contains("passbook"))
                return "PASSBOOK";

            return "SHARE_CAPITAL";
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


        #region Utility Methods

        public string GetCanonicalData(object transactionData)
        {
            // Convert to JSON with consistent ordering
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            return JsonSerializer.Serialize(transactionData, options);
        }

        private string ReconstructCanonicalData(Contrib transaction)
        {
            // Reconstruct the data that was originally signed
            var data = new
            {
                MemberNo = transaction.MemberNo ?? "",
                Amount = transaction.Amount ?? 0,
                TransactionDate = transaction.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                SharesCode = transaction.Sharescode ?? "",
                ReceiptNo = transaction.ReceiptNo ?? "",
                TransactionNo = transaction.TransactionNo ?? "",
                CompanyCode = transaction.CompanyCode ?? "",
                ContributionCategory = DetermineContributionCategory(transaction)
            };

            return GetCanonicalData(data);
        }

        //private string DetermineContributionCategory(Contrib transaction)
        //{
        //    // Simple determination - you can expand this
        //    var shareType = transaction.Sharescode?.ToLower() ?? "";

        //    if (shareType.Contains("reg") || shareType.Contains("fee"))
        //        return "REGISTRATION_FEE";
        //    if (shareType.Contains("deposit") || shareType.Contains("savings"))
        //        return "DEPOSIT";
        //    if (shareType.Contains("donor") || shareType.Contains("gift"))
        //        return "DONOR";
        //    if (shareType.Contains("loan") || shareType.Contains("repayment"))
        //        return "LOAN_REPAYMENT";
        //    if (shareType.Contains("passbook"))
        //        return "PASSBOOK";

        //    return "SHARE_CAPITAL";
        //}       

        #endregion

        #region Fraud Detection

        public async Task<FraudAnalysisResult> AnalyzeTransactionAsync(
            string memberNo,
            decimal amount,
            string category)
        {
            var result = new FraudAnalysisResult
            {
                IsSuspicious = false,
                ShouldBlock = false,
                Flags = new List<string>(),
                Confidence = 0
            };

            try
            {
                // Get member's transaction history
                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderByDescending(c => c.Id)
                    .Take(10)
                    .ToListAsync();

                // Check for unusually large amount
                if (amount > 1000000) // 1M KES
                {
                    result.Flags.Add("Unusually large amount");
                    result.IsSuspicious = true;
                    result.Confidence += 30;
                }

                // Check for frequent transactions
                if (transactions.Count >= 5)
                {
                    var recentCount = transactions
                        .Where(t => t.ContrDate >= DateTime.Now.AddHours(-24))
                        .Count();

                    if (recentCount >= 3)
                    {
                        result.Flags.Add("Frequent transactions (3+ in 24 hours)");
                        result.IsSuspicious = true;
                        result.Confidence += 20;
                    }
                }

                // Check for duplicate amounts
                var duplicateCount = transactions
                    .Where(t => Math.Abs((t.Amount ?? 0) - amount) < 0.01m)
                    .Count();

                if (duplicateCount >= 3)
                {
                    result.Flags.Add("Multiple transactions with same amount");
                    result.IsSuspicious = true;
                    result.Confidence += 15;
                }

                // Block if confidence is high
                result.ShouldBlock = result.Confidence >= 60;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing transaction for member {memberNo}");
                result.Flags.Add($"Analysis error: {ex.Message}");
            }

            return result;
        }

        #endregion
    }

}