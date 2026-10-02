using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using SACCOBlockChainSystem.ViewModels;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class VerificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICryptoService _cryptoService;
        private readonly ILogger<VerificationController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public VerificationController(
            ApplicationDbContext context,
            ICryptoService cryptoService,
            ILogger<VerificationController> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _cryptoService = cryptoService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        // GET: /Verification/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Verification/Search - Returns JSON for AJAX search
        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            try
            {
                _logger.LogInformation($"Searching for: {searchTerm}");

                if (string.IsNullOrEmpty(searchTerm) || searchTerm.Length < 3)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please enter at least 3 characters to search",
                        data = new List<object>()
                    });
                }

                var companyCode = _companyContextService.GetCurrentCompanyCode();

                if (string.IsNullOrEmpty(companyCode))
                {
                    _logger.LogWarning("Company code is null or empty");
                    return Json(new { success = false, message = "Company code not found", data = new List<object>() });
                }

                _logger.LogInformation($"Searching in company: {companyCode} for term: {searchTerm}");

                // Search by Receipt No, Transaction No, or Member No
                var transactions = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .Where(c => c.CompanyCode != null && c.CompanyCode == companyCode)
                    .Where(c =>
                        (c.ReceiptNo != null && c.ReceiptNo.Contains(searchTerm)) ||
                        (c.TransactionNo != null && c.TransactionNo.Contains(searchTerm)) ||
                        (c.MemberNo != null && c.MemberNo.Contains(searchTerm)))
                    .OrderByDescending(c => c.Id)
                    .Take(50)
                    .ToListAsync();

                _logger.LogInformation($"Found {transactions.Count} transactions");

                var results = transactions.Select(t => new
                {
                    transactionId = t.Id,
                    receiptNo = t.ReceiptNo ?? "N/A",
                    transactionDate = t.ContrDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
                    memberNo = t.MemberNo ?? "N/A",
                    memberName = t.MemberNoNavigation != null
                        ? $"{t.MemberNoNavigation.Surname} {t.MemberNoNavigation.OtherNames}"
                        : "Unknown",
                    amount = t.Amount ?? 0,
                    shareType = t.Sharescode ?? "Unknown",
                    isSignatureVerified = t.IsSignatureVerified ?? false,
                    blockchainTxId = t.BlockchainTxId,
                    status = (t.IsSignatureVerified ?? false) ? "Verified" : "Pending",
                    statusClass = (t.IsSignatureVerified ?? false) ? "success" : "warning"
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = results,
                    count = results.Count,
                    searchTerm = searchTerm
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for: {searchTerm}");
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // POST: /Verification/VerifyTransaction
        [HttpPost]
        public async Task<IActionResult> VerifyTransaction([FromBody] VerifyTransactionRequest request)
        {
            try
            {
                _logger.LogInformation($"Verifying transaction ID: {request.TransactionId}");

                var result = await _cryptoService.VerifyTransactionAsync(request.TransactionId);

                // Get transaction details for response
                var transaction = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .FirstOrDefaultAsync(c => c.Id == request.TransactionId);

                if (transaction == null)
                {
                    return Json(new { success = false, message = "Transaction not found" });
                }

                // Get member and wallet info
                var member = transaction.MemberNoNavigation;
                var wallet = await _cryptoService.GetWalletByMemberNoAsync(transaction.MemberNo);

                // Build response
                var verificationResult = new
                {
                    success = true,
                    isValid = result.IsValid,
                    signatureValid = result.SignatureValid,
                    chainValid = result.ChainValid,
                    isGenesisTransaction = result.IsGenesisTransaction,
                    genesisAnchor = result.GenesisAnchor,
                    message = result.Message,
                    transaction = new
                    {
                        id = transaction.Id,
                        receiptNo = transaction.ReceiptNo,
                        amount = transaction.Amount,
                        date = transaction.ContrDate,
                        shareType = transaction.Sharescode,
                        signature = transaction.TransactionSignature?.Substring(0, Math.Min(50, transaction.TransactionSignature?.Length ?? 0)) + "...",
                        hash = transaction.TransactionHash,
                        previousHash = transaction.PreviousTransactionHash,
                        sequence = transaction.TransactionSequence,
                        isVerified = transaction.IsSignatureVerified,
                        verifiedAt = transaction.SignatureVerifiedAt
                    },
                    member = new
                    {
                        memberNo = member?.MemberNo,
                        name = member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown",
                        idNo = member?.Idno,
                        phone = member?.PhoneNo,
                        walletAddress = wallet?.Address,
                        hasWallet = wallet != null
                    }
                };

                return Json(verificationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying transaction {request.TransactionId}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Verification/VerifyMemberChain
        [HttpPost]
        public async Task<IActionResult> VerifyMemberChain([FromBody] VerifyChainRequest request)
        {
            try
            {
                _logger.LogInformation($"Verifying member chain for: {request.MemberNo}");

                var result = await _cryptoService.VerifyMemberChainWithGenesisAsync(request.MemberNo);

                // Get detailed transaction status
                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == request.MemberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                var transactionStatuses = new List<object>();
                string previousHash = null;
                bool isFirst = true;
                var wallet = await _cryptoService.GetWalletByMemberNoAsync(request.MemberNo);

                for (int i = 0; i < transactions.Count; i++)
                {
                    var tx = transactions[i];

                    // Check chain link
                    bool chainLinked;
                    if (isFirst)
                    {
                        // First transaction - link to wallet address
                        chainLinked = tx.PreviousTransactionHash == wallet?.Address;
                    }
                    else
                    {
                        // Subsequent transactions - link to previous hash
                        chainLinked = tx.PreviousTransactionHash == previousHash;
                    }

                    bool sigValid = tx.IsSignatureVerified ?? false;

                    transactionStatuses.Add(new
                    {
                        id = tx.Id,
                        receiptNo = tx.ReceiptNo,
                        amount = tx.Amount,
                        date = tx.ContrDate,
                        previousHash = tx.PreviousTransactionHash,
                        transactionHash = tx.TransactionHash,
                        isSignatureValid = sigValid,
                        isChainLinked = chainLinked,
                        isGenesis = isFirst,
                        status = (sigValid && chainLinked) ? "✅ Valid" :
                                 (sigValid ? "⚠️ Chain Broken" :
                                 (chainLinked ? "❌ Invalid Signature" : "❌ Both Invalid")),
                        statusColor = (sigValid && chainLinked) ? "success" :
                                     (sigValid ? "warning" : "danger")
                    });

                    previousHash = tx.TransactionHash;
                    isFirst = false;
                }

                return Json(new
                {
                    success = true,
                    isValid = result.IsValid,
                    totalTransactions = result.TotalTransactions,
                    validSignatures = result.ValidSignatures,
                    failedTransactionIds = result.FailedTransactionIds,
                    brokenChainIds = result.BrokenChainIds,
                    genesisAnchor = result.GenesisAnchor,
                    isFirstTransactionValid = result.IsFirstTransactionValid,
                    message = result.Message,
                    transactions = transactionStatuses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying member chain for {request.MemberNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        //// POST: /Verification/VerifyTransaction
        //[HttpPost]
        //public async Task<IActionResult> VerifyTransaction([FromBody] VerifyTransactionRequest request)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Verifying transaction ID: {request.TransactionId}");

        //        var result = await _cryptoService.VerifyTransactionAsync(request.TransactionId);

        //        // Get transaction details for response
        //        var transaction = await _context.Contribs
        //            .Include(c => c.MemberNoNavigation)
        //            .FirstOrDefaultAsync(c => c.Id == request.TransactionId);

        //        if (transaction == null)
        //        {
        //            return Json(new { success = false, message = "Transaction not found" });
        //        }

        //        var member = transaction.MemberNoNavigation;
        //        var wallet = await _cryptoService.GetWalletByMemberNoAsync(transaction.MemberNo);

        //        var verificationResult = new
        //        {
        //            success = true,
        //            isValid = result.IsValid,
        //            signatureValid = result.SignatureValid,
        //            chainValid = result.ChainValid,
        //            message = result.Message,
        //            transaction = new
        //            {
        //                id = transaction.Id,
        //                receiptNo = transaction.ReceiptNo,
        //                amount = transaction.Amount,
        //                date = transaction.ContrDate,
        //                shareType = transaction.Sharescode,
        //                signature = transaction.TransactionSignature?.Substring(0, Math.Min(50, transaction.TransactionSignature?.Length ?? 0)) + "...",
        //                hash = transaction.TransactionHash,
        //                previousHash = transaction.PreviousTransactionHash,
        //                sequence = transaction.TransactionSequence,
        //                isVerified = transaction.IsSignatureVerified,
        //                verifiedAt = transaction.SignatureVerifiedAt
        //            },
        //            member = new
        //            {
        //                memberNo = member?.MemberNo,
        //                name = member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown",
        //                idNo = member?.Idno,
        //                phone = member?.PhoneNo,
        //                walletAddress = wallet?.Address
        //            }
        //        };

        //        return Json(verificationResult);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error verifying transaction {request.TransactionId}");
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}

        // GET: /Verification/Transaction/{id}
        public async Task<IActionResult> Transaction(int id)
        {
            try
            {
                var transaction = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (transaction == null)
                {
                    return NotFound();
                }

                var member = transaction.MemberNoNavigation;
                var wallet = await _cryptoService.GetWalletByMemberNoAsync(transaction.MemberNo);

                var viewModel = new TransactionVerificationViewModel
                {
                    TransactionId = transaction.Id.ToString(),
                    MemberNo = transaction.MemberNo ?? "",
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown",
                    ReceiptNo = transaction.ReceiptNo ?? "",
                    Amount = transaction.Amount ?? 0,
                    TransactionDate = transaction.ContrDate ?? DateTime.Now,
                    ShareType = transaction.Sharescode ?? "Unknown",
                    TransactionSignature = transaction.TransactionSignature ?? "",
                    TransactionHash = transaction.TransactionHash ?? "",
                    PreviousTransactionHash = transaction.PreviousTransactionHash ?? "",
                    TransactionSequence = transaction.TransactionSequence,
                    IsSignatureVerified = transaction.IsSignatureVerified ?? false,
                    SignatureVerifiedAt = transaction.SignatureVerifiedAt,
                    WalletAddress = wallet?.Address ?? "No wallet found",
                    PublicKey = wallet?.PublicKey?.Substring(0, Math.Min(50, wallet?.PublicKey?.Length ?? 0)) + "..." ?? "N/A",
                    MemberIdNo = member?.Idno ?? "",
                    MemberPhone = member?.PhoneNo ?? "",
                    MemberEmail = member?.Email ?? "",
                    BlockchainTxId = transaction.BlockchainTxId ?? "",

                    // Default verification status (will be updated via AJAX)
                    SignatureValid = false,
                    ChainValid = false,
                    VerificationMessage = "Click 'Verify' to check this transaction",
                    VerifiedAt = DateTime.Now,
                    VerifiedBy = User.Identity?.Name ?? "System"
                };

                ViewBag.CompanyCode = _companyContextService.GetCurrentCompanyCode();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading transaction {id}");
                TempData["ErrorMessage"] = $"Error loading transaction: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: /Verification/MemberChain/{memberNo}
        [HttpGet("MemberChain/{memberNo}")]
        public async Task<IActionResult> MemberChain(string memberNo)
        {
            try
            {
                _logger.LogInformation($"Loading member chain for: {memberNo}");

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    _logger.LogWarning($"Member not found: {memberNo}");
                    TempData["ErrorMessage"] = $"Member {memberNo} not found";
                    return RedirectToAction("Index");
                }

                var wallet = await _cryptoService.GetWalletByMemberNoAsync(memberNo);

                var transactions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                var summary = new ChainVerificationSummaryViewModel
                {
                    MemberNo = member.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}".Trim(),
                    TotalTransactions = transactions.Count,
                    WalletAddress = wallet?.Address ?? "No wallet found",
                    GenesisHash = transactions.FirstOrDefault()?.PreviousTransactionHash ?? "No transactions",
                    GenesisValid = false,
                    IsChainValid = false,
                    VerificationMessage = "Click 'Verify Chain' to validate all transactions",
                    VerifiedAt = DateTime.Now,
                    Transactions = transactions.Select(t => new TransactionSummaryDto
                    {
                        Id = t.Id,
                        ReceiptNo = t.ReceiptNo ?? "N/A",
                        Amount = t.Amount ?? 0,
                        TransactionDate = t.ContrDate ?? DateTime.Now,
                        ShareType = t.Sharescode ?? "Unknown",
                        IsSignatureValid = t.IsSignatureVerified ?? false,
                        IsChainLinked = false,
                        StatusIcon = "⏳",
                        StatusColor = "secondary"
                    }).ToList()
                };

                return View(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading member chain for {memberNo}");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        //// POST: /Verification/VerifyMemberChain
        //[HttpPost]
        //public async Task<IActionResult> VerifyMemberChain([FromBody] VerifyChainRequest request)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Verifying member chain for: {request.MemberNo}");

        //        var result = await _cryptoService.VerifyMemberChainWithGenesisAsync(request.MemberNo);

        //        // Get detailed transaction status
        //        var transactions = await _context.Contribs
        //            .Where(c => c.MemberNo == request.MemberNo)
        //            .OrderBy(c => c.Id)
        //            .ToListAsync();

        //        var transactionStatuses = new List<object>();
        //        string previousHash = null;

        //        for (int i = 0; i < transactions.Count; i++)
        //        {
        //            var tx = transactions[i];
        //            bool chainLinked = (i == 0) || (tx.PreviousTransactionHash == previousHash);
        //            bool sigValid = tx.IsSignatureVerified ?? false;

        //            transactionStatuses.Add(new
        //            {
        //                id = tx.Id,
        //                receiptNo = tx.ReceiptNo,
        //                amount = tx.Amount,
        //                date = tx.ContrDate,
        //                isSignatureValid = sigValid,
        //                isChainLinked = chainLinked,
        //                status = (sigValid && chainLinked) ? "✅ Valid" : (sigValid ? "⚠️ Chain Broken" : "❌ Invalid"),
        //                statusColor = (sigValid && chainLinked) ? "success" : (sigValid ? "warning" : "danger")
        //            });

        //            previousHash = tx.TransactionHash;
        //        }

        //        return Json(new
        //        {
        //            success = true,
        //            isValid = result.IsValid,
        //            totalTransactions = result.TotalTransactions,
        //            validSignatures = result.ValidSignatures,
        //            failedTransactionIds = result.FailedTransactionIds,
        //            message = result.Message,
        //            transactions = transactionStatuses
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error verifying member chain for {request.MemberNo}");
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}
    }

    public class VerifyTransactionRequest
    {
        public int TransactionId { get; set; }
    }

    public class VerifyChainRequest
    {
        public string MemberNo { get; set; } = string.Empty;
    }
}