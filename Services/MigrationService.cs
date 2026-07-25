// Services/MigrationService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface IMigrationService
    {
        Task<MigrationResult> MigrateAllWalletsAndTransactionsAsync();
        Task<MigrationResult> MigrateMemberWalletsAndTransactionsAsync(string memberNo);
        Task<MigrationResult> RegenerateWalletForMemberAsync(string memberNo);
        Task<MigrationResult> ReSignMemberTransactionsAsync(string memberNo);
        Task<MigrationResult> FixMemberChainLinksAsync(string memberNo);
        Task<MigrationSummary> GetMigrationSummaryAsync();
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int WalletsMigrated { get; set; }
        public int TransactionsReSigned { get; set; }
        public int ChainLinksFixed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime CompletedAt { get; set; }
    }

    public class MigrationSummary
    {
        public int TotalMembers { get; set; }
        public int MembersWithWallets { get; set; }
        public int MembersWithoutWallets { get; set; }
        public int TotalTransactions { get; set; }
        public int TransactionsWithSignatures { get; set; }
        public int TransactionsWithoutSignatures { get; set; }
        public int ValidChains { get; set; }
        public int BrokenChains { get; set; }
    }

    public class MigrationService : IMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICryptoService _cryptoService;
        private readonly ILogger<MigrationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public MigrationService(
            ApplicationDbContext context,
            ICryptoService cryptoService,
            ILogger<MigrationService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _cryptoService = cryptoService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<MigrationSummary> GetMigrationSummaryAsync()
        {
            try
            {
                var summary = new MigrationSummary();

                // Get all members with their wallet status
                var members = await _context.Members.ToListAsync();
                summary.TotalMembers = members.Count;

                var walletMemberNos = await _context.Wallets
                    .Select(w => w.memberNo)
                    .Distinct()
                    .ToListAsync();

                summary.MembersWithWallets = walletMemberNos.Count;
                summary.MembersWithoutWallets = summary.TotalMembers - summary.MembersWithWallets;

                // Get transaction stats
                var transactions = await _context.Contribs.ToListAsync();
                summary.TotalTransactions = transactions.Count;
                summary.TransactionsWithSignatures = transactions.Count(t => !string.IsNullOrEmpty(t.TransactionSignature));
                summary.TransactionsWithoutSignatures = summary.TotalTransactions - summary.TransactionsWithSignatures;

                // Check chain integrity
                var membersWithTransactions = transactions.Select(t => t.MemberNo).Distinct().ToList();
                int validChains = 0;
                int brokenChains = 0;

                foreach (var memberNo in membersWithTransactions)
                {
                    var chainValid = await CheckChainIntegrityAsync(memberNo);
                    if (chainValid)
                        validChains++;
                    else
                        brokenChains++;
                }

                summary.ValidChains = validChains;
                summary.BrokenChains = brokenChains;

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration summary");
                throw;
            }
        }

        public async Task<MigrationResult> MigrateAllWalletsAndTransactionsAsync()
        {
            _logger.LogInformation("Starting full migration of all wallets and transactions...");

            var result = new MigrationResult
            {
                CompletedAt = DateTime.Now,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get all members
                var members = await _context.Members.ToListAsync();
                result.WalletsMigrated = 0;
                result.TransactionsReSigned = 0;
                result.ChainLinksFixed = 0;

                foreach (var member in members)
                {
                    try
                    {
                        _logger.LogInformation($"Processing member: {member.MemberNo}");

                        // 1. Regenerate wallet
                        var walletResult = await RegenerateWalletForMemberInternalAsync(member);
                        if (walletResult.Success)
                        {
                            result.WalletsMigrated++;
                        }
                        else
                        {
                            result.Errors.Add($"Member {member.MemberNo}: Wallet migration failed - {walletResult.Message}");
                            continue;
                        }

                        // 2. Re-sign all transactions for this member
                        var signResult = await ReSignMemberTransactionsInternalAsync(member.MemberNo);
                        result.TransactionsReSigned += signResult.TransactionsReSigned;
                        result.Errors.AddRange(signResult.Errors);
                        result.Warnings.AddRange(signResult.Warnings);

                        // 3. Fix chain links
                        var chainResult = await FixMemberChainLinksInternalAsync(member.MemberNo);
                        result.ChainLinksFixed += chainResult.ChainLinksFixed;
                        result.Errors.AddRange(chainResult.Errors);
                        result.Warnings.AddRange(chainResult.Warnings);

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing member {member.MemberNo}");
                        result.Errors.Add($"Member {member.MemberNo}: {ex.Message}");
                    }
                }

                await transaction.CommitAsync();
                result.Success = result.Errors.Count == 0;
                result.Message = result.Success
                    ? $"Migration completed successfully. {result.WalletsMigrated} wallets, {result.TransactionsReSigned} transactions re-signed, {result.ChainLinksFixed} chain links fixed."
                    : $"Migration completed with {result.Errors.Count} errors. Check errors list for details.";

                _logger.LogInformation(result.Message);
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Migration failed");
                result.Success = false;
                result.Message = $"Migration failed: {ex.Message}";
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        public async Task<MigrationResult> MigrateMemberWalletsAndTransactionsAsync(string memberNo)
        {
            _logger.LogInformation($"Starting migration for member: {memberNo}");

            var result = new MigrationResult
            {
                CompletedAt = DateTime.Now,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    result.Success = false;
                    result.Message = $"Member {memberNo} not found";
                    return result;
                }

                // 1. Regenerate wallet
                var walletResult = await RegenerateWalletForMemberInternalAsync(member);
                if (walletResult.Success)
                {
                    result.WalletsMigrated = 1;
                }
                else
                {
                    result.Errors.Add($"Wallet migration failed - {walletResult.Message}");
                    result.Success = false;
                    result.Message = walletResult.Message;
                    await transaction.RollbackAsync();
                    return result;
                }

                // 2. Re-sign transactions
                var signResult = await ReSignMemberTransactionsInternalAsync(memberNo);
                result.TransactionsReSigned = signResult.TransactionsReSigned;
                result.Errors.AddRange(signResult.Errors);
                result.Warnings.AddRange(signResult.Warnings);

                // 3. Fix chain links
                var chainResult = await FixMemberChainLinksInternalAsync(memberNo);
                result.ChainLinksFixed = chainResult.ChainLinksFixed;
                result.Errors.AddRange(chainResult.Errors);
                result.Warnings.AddRange(chainResult.Warnings);

                await transaction.CommitAsync();

                result.Success = result.Errors.Count == 0;
                result.Message = result.Success
                    ? $"Migration completed successfully for member {memberNo}"
                    : $"Migration completed with {result.Errors.Count} errors";

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Migration failed for member {memberNo}");
                result.Success = false;
                result.Message = $"Migration failed: {ex.Message}";
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        public async Task<MigrationResult> RegenerateWalletForMemberAsync(string memberNo)
        {
            var result = new MigrationResult { CompletedAt = DateTime.Now };

            try
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    result.Success = false;
                    result.Message = $"Member {memberNo} not found";
                    return result;
                }

                var walletResult = await RegenerateWalletForMemberInternalAsync(member);
                result.Success = walletResult.Success;
                result.Message = walletResult.Message;
                result.WalletsMigrated = walletResult.Success ? 1 : 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error regenerating wallet for member {memberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        public async Task<MigrationResult> ReSignMemberTransactionsAsync(string memberNo)
        {
            var result = new MigrationResult { CompletedAt = DateTime.Now };

            try
            {
                var signResult = await ReSignMemberTransactionsInternalAsync(memberNo);
                result.Success = signResult.Errors.Count == 0;
                result.Message = signResult.Message;
                result.TransactionsReSigned = signResult.TransactionsReSigned;
                result.Errors = signResult.Errors;
                result.Warnings = signResult.Warnings;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error re-signing transactions for member {memberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        public async Task<MigrationResult> FixMemberChainLinksAsync(string memberNo)
        {
            var result = new MigrationResult { CompletedAt = DateTime.Now };

            try
            {
                var chainResult = await FixMemberChainLinksInternalAsync(memberNo);
                result.Success = chainResult.Errors.Count == 0;
                result.Message = chainResult.Message;
                result.ChainLinksFixed = chainResult.ChainLinksFixed;
                result.Errors = chainResult.Errors;
                result.Warnings = chainResult.Warnings;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fixing chain links for member {memberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        #region Internal Helper Methods

        private async Task<MigrationResult> RegenerateWalletForMemberInternalAsync(Member member)
        {
            var result = new MigrationResult { CompletedAt = DateTime.Now };

            try
            {
                // Check if wallet exists
                var existingWallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.memberNo == member.MemberNo);

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

                if (existingWallet != null)
                {
                    // Update existing wallet
                    existingWallet.Address = walletAddress;
                    existingWallet.PublicKey = publicKey;
                    existingWallet.PrivateKeyEncrypted = encryptedPrivateKey;
                    existingWallet.LastUsedAt = DateTime.UtcNow;
                    _logger.LogInformation($"Updated wallet for member {member.MemberNo}: {walletAddress}");
                }
                else
                {
                    // Create new wallet
                    var newWallet = new Wallet
                    {
                        MemberId = member.Id,
                        memberNo = member.MemberNo,
                        CompanyCode = member.CompanyCode,
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

                    _context.Wallets.Add(newWallet);
                    _logger.LogInformation($"Created wallet for member {member.MemberNo}: {walletAddress}");
                }

                await _context.SaveChangesAsync();

                result.Success = true;
                result.Message = $"Wallet regenerated successfully for member {member.MemberNo}";
                result.WalletsMigrated = 1;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error regenerating wallet for member {member.MemberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        private async Task<MigrationResult> ReSignMemberTransactionsInternalAsync(string memberNo)
        {
            var result = new MigrationResult
            {
                CompletedAt = DateTime.Now,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            try
            {
                // Get member's wallet
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.memberNo == memberNo);

                if (wallet == null)
                {
                    result.Success = false;
                    result.Message = $"Wallet not found for member {memberNo}";
                    result.Errors.Add($"Wallet not found for member {memberNo}");
                    return result;
                }

                // Get all transactions for this member
                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    result.Success = true;
                    result.Message = $"No transactions found for member {memberNo}";
                    return result;
                }

                // Decrypt private key
                var privateKeyBase64 = EncryptionHelper.Decrypt(wallet.PrivateKeyEncrypted);
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

                int reSigned = 0;

                foreach (var transaction in transactions)
                {
                    try
                    {
                        // Reconstruct canonical data for this transaction
                        var canonicalData = ReconstructCanonicalData(transaction);
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

                        // Update transaction with new signature and hash
                        transaction.TransactionSignature = signature;
                        transaction.TransactionHash = transactionHash;
                        transaction.IsSignatureVerified = false;
                        transaction.SignatureVerifiedAt = null;

                        reSigned++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error re-signing transaction {transaction.Id} for member {memberNo}");
                        result.Errors.Add($"Transaction {transaction.Id}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                result.Success = result.Errors.Count == 0;
                result.TransactionsReSigned = reSigned;
                result.Message = $"{reSigned} transactions re-signed for member {memberNo}";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error re-signing transactions for member {memberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        private async Task<MigrationResult> FixMemberChainLinksInternalAsync(string memberNo)
        {
            var result = new MigrationResult
            {
                CompletedAt = DateTime.Now,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            try
            {
                // Get wallet for genesis anchor
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.memberNo == memberNo);

                if (wallet == null)
                {
                    result.Success = false;
                    result.Message = $"Wallet not found for member {memberNo}";
                    result.Errors.Add($"Wallet not found for member {memberNo}");
                    return result;
                }

                // Get all transactions in order
                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    result.Success = true;
                    result.Message = $"No transactions found for member {memberNo}";
                    return result;
                }

                int fixedLinks = 0;
                string previousHash = null;
                bool isFirst = true;

                foreach (var transaction in transactions)
                {
                    bool needsFix = false;

                    if (isFirst)
                    {
                        // First transaction - should link to wallet address
                        if (transaction.PreviousTransactionHash != wallet.Address)
                        {
                            transaction.PreviousTransactionHash = wallet.Address;
                            needsFix = true;
                            _logger.LogInformation($"Fixed genesis link for transaction {transaction.Id}: set to wallet address {wallet.Address}");
                        }
                    }
                    else
                    {
                        // Subsequent transactions - should link to previous transaction hash
                        if (transaction.PreviousTransactionHash != previousHash)
                        {
                            transaction.PreviousTransactionHash = previousHash;
                            needsFix = true;
                            _logger.LogInformation($"Fixed chain link for transaction {transaction.Id}: set to previous hash {previousHash?.Substring(0, 16)}...");
                        }
                    }

                    if (needsFix)
                    {
                        fixedLinks++;
                        // Mark as unverified so it gets re-verified
                        transaction.IsSignatureVerified = false;
                        transaction.SignatureVerifiedAt = null;
                    }

                    previousHash = transaction.TransactionHash;
                    isFirst = false;
                }

                await _context.SaveChangesAsync();

                result.Success = true;
                result.ChainLinksFixed = fixedLinks;
                result.Message = $"{fixedLinks} chain links fixed for member {memberNo}";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fixing chain links for member {memberNo}");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        private async Task<bool> CheckChainIntegrityAsync(string memberNo)
        {
            try
            {
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.memberNo == memberNo);

                if (wallet == null)
                    return false;

                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                if (!transactions.Any())
                    return true;

                string previousHash = null;
                bool isFirst = true;

                foreach (var tx in transactions)
                {
                    if (isFirst)
                    {
                        if (tx.PreviousTransactionHash != wallet.Address)
                            return false;
                    }
                    else
                    {
                        if (tx.PreviousTransactionHash != previousHash)
                            return false;
                    }

                    previousHash = tx.TransactionHash;
                    isFirst = false;
                }

                return true;
            }
            catch
            {
                return false;
            }
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

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            return JsonSerializer.Serialize(data, options);
        }

        private string DetermineContributionCategory(Contrib transaction)
        {
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

        #endregion
    }

    #region Encryption Helper

    //public static class EncryptionHelper
    //{
    //    private static readonly byte[] Key = Convert.FromBase64String("YourEncryptionKeyHere"); // Use secure key
    //    private static readonly byte[] IV = Convert.FromBase64String("YourIVHere"); // Use secure IV

    //    public static string Encrypt(string plainText)
    //    {
    //        using var aes = Aes.Create();
    //        aes.Key = Key;
    //        aes.IV = IV;

    //        var encryptor = aes.CreateEncryptor();
    //        var plainBytes = Encoding.UTF8.GetBytes(plainText);
    //        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

    //        return Convert.ToBase64String(encryptedBytes);
    //    }

    //    public static string Decrypt(string cipherText)
    //    {
    //        using var aes = Aes.Create();
    //        aes.Key = Key;
    //        aes.IV = IV;

    //        var decryptor = aes.CreateDecryptor();
    //        var encryptedBytes = Convert.FromBase64String(cipherText);
    //        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

    //        return Encoding.UTF8.GetString(decryptedBytes);
    //    }
    //}

    #endregion
}