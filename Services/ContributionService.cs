using DocumentFormat.OpenXml.Bibliography;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Services
{
    public interface IContributionService
    {
        Task<BulkContributionResponseDTO> BulkAddContributionsAsync(BulkContributionRequestDTO request);
        Task<ContributionResponseDTO> AddContributionAsync(ContributionDTO contributionDto, bool prompt = false);
        Task<List<ContributionResponseDTO>> GetMemberContributionsAsync(string memberNo);
        Task<List<ShareTypeDTO>> GetShareTypesAsync(string companyCode);
        Task<Member> GetMemberByMemberNoAsync(string memberNo);
        Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember);
        Task<decimal> GetMemberShareBalanceAsync(string memberNo);
        Task<MemberContributionHistoryDTO> GetMemberContributionHistoryAsync(string memberNo);
        Task<List<ContributionResponseDTO>> SearchContributionsAsync(DateTime? fromDate, DateTime? toDate, string? memberNo = null, string? shareType = null);
        Task<ContributionDeleteResultDTO> ReverseContributionAsync(int contributionId, string deleteReason, string deletedBy);
        Task<MemberShareTypeTotalsDTO> GetMemberShareTypeTotalsAsync(string memberNo, string companyCode);
    }

    public class ContributionService : IContributionService
    {
        private readonly ApplicationDbContext _context;
        private readonly AppDbContext _db;
        private readonly IBlockchainService _blockchainService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<MemberService> _logger;
        private readonly ICompanyContextService _companyContextService;
        private readonly IHttpContextAccessor _httpContextAccesso;
        private readonly AuditTrailService _auditService;
        private readonly ICryptoService _cryptoService;
        // private readonly UserManager<IdentityUser> _userManager;

        public ContributionService(
            ApplicationDbContext context, AppDbContext db,
            IBlockchainService blockchainService,
            ILogger<MemberService> logger,
            IHttpContextAccessor httpContextAccessor,
            AuditTrailService auditService,
            //UserManager<IdentityUser> userManager,
            ICompanyContextService companyContextService,
            ICryptoService cryptoService)
        {
            _context = context;
            _db = db;
            _blockchainService = blockchainService;
            _httpContextAccessor = httpContextAccessor;
            _auditService = auditService;
            _logger = logger;
            //_userManager = userManager;
            _companyContextService = companyContextService;
            _cryptoService = cryptoService;
        }


        public async Task<BulkContributionResponseDTO> BulkAddContributionsAsync(BulkContributionRequestDTO request)
        {
            var response = new BulkContributionResponseDTO
            {
                Success = false,
                Errors = new List<string>()
            };

            // ============================================================
            // STEP 1: BEGIN TRANSACTION (SAME AS SINGLE)
            // ============================================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Starting bulk contribution for member: {request.MemberNo}, Count: {request.Contributions.Count}");

                // ============================================================
                // STEP 2: VALIDATE MEMBER EXISTS (SAME AS SINGLE)
                // ============================================================
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo && m.CompanyCode == request.CompanyCode);

                if (member == null)
                {
                    response.Message = $"Member {request.MemberNo} not found";
                    return response;
                }

                // ============================================================
                // STEP 3: VALIDATE SHARE TYPES EXIST (SAME AS SINGLE)
                // ============================================================
                var shareTypeCodes = request.Contributions.Select(c => c.SharesCode).Distinct().ToList();
                var shareTypes = await _context.Sharetypes
                    .Where(st => shareTypeCodes.Contains(st.SharesCode) && st.CompanyCode == request.CompanyCode)
                    .ToDictionaryAsync(st => st.SharesCode, st => st);

                var invalidShares = shareTypeCodes.Where(code => !shareTypes.ContainsKey(code)).ToList();
                if (invalidShares.Any())
                {
                    response.Errors.Add($"Invalid share types: {string.Join(", ", invalidShares)}");
                    response.Message = "Some share types are invalid";
                    return response;
                }

                // ============================================================
                // STEP 4: PREPARE FOR MULTIPLE CONTRIBUTIONS
                // ============================================================
                var validatedContributions = new List<ValidatedContribution>();
                var validationErrors = new List<string>();

                // Get Sacco parameters once (SAME AS SINGLE)
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == request.CompanyCode);

                // ============================================================
                // STEP 5: VALIDATE EACH CONTRIBUTION (SAME AS SINGLE)
                // ============================================================
                foreach (var item in request.Contributions)
                {
                    try
                    {
                        var shareType = shareTypes[item.SharesCode];

                        // STEP 5a: Determine contribution type (SAME AS SINGLE)
                        string contributionCategory = DetermineContributionType(shareType, new ContributionDTO { SharesCode = item.SharesCode });
                        bool isMajorShareType = contributionCategory != "UNKNOWN";

                        _logger.LogInformation($"Contribution Category: {contributionCategory}, IsMajor: {isMajorShareType}");

                        // STEP 5b: Get existing total (SAME AS SINGLE)
                        decimal existingTotal = await GetExistingContributionTotalAsync(
                            request.MemberNo,
                            item.SharesCode,
                            contributionCategory,
                            request.CompanyCode);

                        // STEP 5c: Validate amount against minimum (SAME AS SINGLE)
                        if (existingTotal < shareType.MinAmount && item.Amount < shareType.MinAmount)
                        {
                            throw new ValidationException(
                                $"Amount cannot be less than minimum of {shareType.MinAmount:C}. " +
                                $"Current total: {existingTotal:C}. You need to contribute at least {shareType.MinAmount - existingTotal:C} more to reach the minimum."
                            );
                        }

                        decimal newTotal = existingTotal + item.Amount;

                        // STEP 5d: Check maximum contribution limit (SAME AS SINGLE)
                        if (shareType.MaxAmount.HasValue && newTotal > shareType.MaxAmount.Value)
                        {
                            decimal remainingAllowed = shareType.MaxAmount.Value - existingTotal;
                            if (remainingAllowed <= 0)
                            {
                                throw new ValidationException(
                                    $"Maximum {shareType.SharesType} limit of {shareType.MaxAmount.Value:C} has already been reached. " +
                                    $"Current total: {existingTotal:C}. No further contributions allowed.");
                            }
                            else
                            {
                                throw new ValidationException(
                                    $"Amount {item.Amount:C} exceeds remaining limit for {shareType.SharesType}. " +
                                    $"Current total: {existingTotal:C}, Maximum: {shareType.MaxAmount.Value:C}, " +
                                    $"Remaining allowed: {remainingAllowed:C}. Please reduce the amount.");
                            }
                        }

                        // STEP 5e: Validate prerequisites (SAME AS SINGLE)
                        await ValidateContributionPrerequisitesBulkAsync(
                            request.MemberNo,
                            item.SharesCode,
                            request.CompanyCode);

                        // STEP 5f: Validate dates (SAME AS SINGLE)
                        DateTime contributionDate = item.TransactionDate ?? DateTime.Now;
                        DateTime depositedDate;
                        if (item.DepositedDate.HasValue)
                        {
                            depositedDate = item.DepositedDate.Value.Date;
                            if (depositedDate > DateTime.Now.Date)
                            {
                                throw new ValidationException("Deposit date cannot be in the future.");
                            }
                        }
                        else
                        {
                            depositedDate = DateTime.Now.Date;
                        }
                        DateTime receiptDate = depositedDate;

                        // STEP 5g: Get GL Accounts (SAME AS SINGLE)
                        string drAcc = null;
                        string crAcc = shareType.SharesAcc;

                        if (string.IsNullOrEmpty(crAcc))
                        {
                            throw new Exception($"Share type '{item.SharesCode}' does not have a GL Account configured.");
                        }

                        string paymentMethod = item.PaymentMethod?.ToUpper() ?? "CASH";

                        switch (paymentMethod)
                        {
                            case "BANK TRANSFER":
                            case "CHEQUE":
                                if (!string.IsNullOrEmpty(item.ReferenceNo))
                                {
                                    var bank = await _context.Banks
                                        .FirstOrDefaultAsync(b => b.CompanyCode == request.CompanyCode &&
                                                                  b.IsActive == true &&
                                                                  (b.BankCode == item.ReferenceNo ||
                                                                   b.BankName.Contains(item.ReferenceNo) ||
                                                                   b.AccountNumber == item.ReferenceNo));

                                    if (bank != null && !string.IsNullOrEmpty(bank.GlAccountNo))
                                    {
                                        drAcc = bank.GlAccountNo;
                                        break;
                                    }
                                }

                                var defaultBank = await _context.Banks
                                    .FirstOrDefaultAsync(b => b.CompanyCode == request.CompanyCode &&
                                                              b.IsActive == true &&
                                                              !string.IsNullOrEmpty(b.GlAccountNo));

                                if (defaultBank != null)
                                {
                                    drAcc = defaultBank.GlAccountNo;
                                    break;
                                }
                                goto case "CASH";

                            case "MOBILE MONEY":
                            case "CASH":
                            default:
                                if (sacco != null && !string.IsNullOrEmpty(sacco.RetainedEarnings))
                                {
                                    drAcc = sacco.RetainedEarnings;
                                }
                                else
                                {
                                    var cashAccount = await _context.GlSetup
                                        .FirstOrDefaultAsync(g => g.CompanyCode == request.CompanyCode &&
                                                                  g.Status == true &&
                                                                  (g.Type == "ASSET" || g.Type == "Asset") &&
                                                                  (g.Glaccname != null &&
                                                                   (g.Glaccname.ToLower().Contains("cash") ||
                                                                    g.Glaccname.ToLower().Contains("bank"))));

                                    if (cashAccount != null)
                                    {
                                        drAcc = cashAccount.AccNo;
                                    }
                                }
                                break;
                        }

                        if (string.IsNullOrEmpty(drAcc))
                        {
                            var suspenseAccount = await _context.GlSetup
                                .FirstOrDefaultAsync(g => g.CompanyCode == request.CompanyCode &&
                                                          g.IsSuspense == true);
                            if (suspenseAccount != null)
                            {
                                drAcc = suspenseAccount.AccNo;
                            }
                        }

                        // STEP 5h: Check/Create wallet (SAME AS SINGLE)
                        var memberRecord = await _context.Members
                            .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo);

                        if (memberRecord != null)
                        {
                            var memberHasWallet = await _cryptoService.HasWalletAsync(memberRecord.MobileNo);
                            if (!memberHasWallet)
                            {
                                _logger.LogWarning($"Member {memberRecord.MemberNo} has no wallet. Creating now...");
                                var walletResult = await _cryptoService.CreateWalletForMemberAsync(
                                    memberRecord.Id,
                                    memberRecord.MemberNo,
                                    request.CompanyCode);
                                if (!walletResult.Success)
                                {
                                    throw new Exception($"Cannot process transaction: {walletResult.Message}");
                                }
                            }
                        }

                        // STORE VALIDATED CONTRIBUTION
                        validatedContributions.Add(new ValidatedContribution
                        {
                            Item = item,
                            ShareType = shareType,
                            Category = contributionCategory,
                            IsMajorShareType = isMajorShareType,
                            DrAcc = drAcc,
                            CrAcc = crAcc,
                            ExistingTotal = existingTotal,
                            ContributionDate = contributionDate,
                            DepositedDate = depositedDate,
                            ReceiptDate = receiptDate,
                            PaymentMethod = paymentMethod
                        });
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Validation error for {item.SharesCode}: {ex.Message}");
                    }
                }

                if (validationErrors.Any())
                {
                    response.Errors = validationErrors;
                    response.Message = $"Validation failed: {validationErrors.Count} error(s) found";
                    return response;
                }

                if (!validatedContributions.Any())
                {
                    response.Message = "No valid contributions to save";
                    return response;
                }

                // ============================================================
                // STEP 6: GET/CREATE WALLET ONCE (SAME AS SINGLE)
                // ============================================================
                var memberRecordForWallet = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo);

                if (memberRecordForWallet == null)
                {
                    throw new Exception($"Member {request.MemberNo} not found");
                }

                // Check if wallet exists, create if not (SAME AS SINGLE)
                var hasWallet = await _cryptoService.HasWalletAsync(memberRecordForWallet.MobileNo);
                if (!hasWallet)
                {
                    _logger.LogWarning($"Member {memberRecordForWallet.MemberNo} has no wallet. Creating now...");
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(
                        memberRecordForWallet.Id,
                        memberRecordForWallet.MemberNo,
                        request.CompanyCode);
                    if (!walletResult.Success)
                    {
                        throw new Exception($"Cannot process transaction: {walletResult.Message}");
                    }
                }

                // Get the wallet (should exist now)
                var memberWallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecordForWallet.Id);

                if (memberWallet == null)
                {
                    // One more attempt to create wallet
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(
                        memberRecordForWallet.Id,
                        memberRecordForWallet.MemberNo,
                        request.CompanyCode);

                    if (!walletResult.Success)
                    {
                        throw new Exception($"Failed to create wallet: {walletResult.Message}");
                    }
                    memberWallet = walletResult.Wallet;
                }

                // ============================================================
                // STEP 6.5: GENERATE BULK RECEIPT NUMBER (SAME FOR ALL CONTRIBUTIONS)
                // ============================================================
                string combinedReceiptNo = $"BULK-{DateTime.Now:yyyyMMddHHmmss}";
                _logger.LogInformation($"Generated bulk receipt number: {combinedReceiptNo}");

                // ============================================================
                // STEP 7: PROCESS EACH CONTRIBUTION
                // ============================================================
                var savedContributions = new List<ContributionResponseDTO>();
                decimal totalAmount = 0;

                // Lists to collect entities for batch save
                var allContribs = new List<Contrib>();
                var allContribShares = new List<ContribShare>();
                var allGlTransactions = new List<Gltransaction>();
                var allBlocks = new List<Block>();
                var allBlockchainTx = new List<BlockchainTransaction>();
                var allAuditData = new List<(object ExtraData, object NewModel, string ReceiptNo)>();

                // Get last block hash once
                var lastBlockHash = await GetLastBlockHashAsync();

                // Get last contrib for sequence number
                var lastContrib = await _context.Contribs
                    .Where(c => c.MemberNo == request.MemberNo)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                // Use long for nonce to match database type
                long currentNonce = lastContrib?.TransactionSequence ?? 0;

                foreach (var validated in validatedContributions)
                {
                    var item = validated.Item;
                    var shareType = validated.ShareType;
                    var category = validated.Category;
                    var isMajorShareType = validated.IsMajorShareType;
                    var drAcc = validated.DrAcc;
                    var crAcc = validated.CrAcc;
                    var existingTotal = validated.ExistingTotal;
                    var contributionDate = validated.ContributionDate;
                    var depositedDate = validated.DepositedDate;
                    var receiptDate = validated.ReceiptDate;
                    var paymentMethod = validated.PaymentMethod;

                    _logger.LogInformation($"Processing bulk contribution: {item.SharesCode} - {item.Amount:C}");

                    // Generate receipt and transaction numbers
                    var receiptNo = GenerateReceiptNumber(request.CompanyCode);
                    var transactionNo = GenerateTransactionNumber(request.CompanyCode);

                    // ============================================================
                    // STEP 7a: CREATE CONTRIB RECORD (SAME AS SINGLE)
                    // ============================================================
                    var contrib = new Contrib
                    {
                        MemberNo = request.MemberNo,
                        ContrDate = contributionDate,
                        Amount = item.Amount,
                        CompanyCode = request.CompanyCode,
                        ReceiptNo = receiptNo,
                        Remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                        AuditId = request.CreatedBy,
                        AuditTime = DateTime.Now,
                        AuditDateTime = DateTime.Now,
                        Sharescode = item.SharesCode,
                        TransactionNo = transactionNo,
                        Posted = "Y",
                        Locked = "N",
                        StaffNo = null,
                        DepositedDate = depositedDate,
                        ReceiptDate = receiptDate,
                        RefNo = item.ReferenceNo,
                        ShareBal = 0,
                        TransBy = request.CreatedBy,
                        ChequeNo = item.PaymentMethod == "CHEQUE" ? item.ReferenceNo : null,
                        TransDate = contributionDate,
                        SharesAcc = shareType.SharesAcc,
                        ContraAcc = shareType.ContraAcc,
                        CashBookdate = DateTime.Now,
                        Dregard = 0,
                        Offs = 0,
                        ApiKey = null,
                        UserName = request.CreatedBy,
                        Run = 0,
                        Run2 = 0,
                        MrCleared = "N",
                        Mrno = null,
                        Offset = false,
                        TransferDesc = null,
                        Schemecode = request.CompanyCode,
                        Status = "Active"
                    };

                    // ============================================================
                    // STEP 7b: FRAUD DETECTION (SAME AS SINGLE)
                    // ============================================================
                    var fraudResult = await _cryptoService.AnalyzeTransactionAsync(
                        request.MemberNo,
                        item.Amount,
                        category);

                    if (fraudResult.ShouldBlock)
                    {
                        throw new Exception($"Transaction blocked by fraud detection: {string.Join(", ", fraudResult.Flags)}");
                    }

                    if (fraudResult.IsSuspicious)
                    {
                        contrib.Remarks = $"{contrib.Remarks}";
                    }

                    // ============================================================
                    // STEP 7c: SIGN THE TRANSACTION (SAME AS SINGLE)
                    // ============================================================
                    var txDataForSigning = new
                    {
                        MemberNo = contrib.MemberNo,
                        Amount = contrib.Amount,
                        TransactionDate = contrib.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                        SharesCode = contrib.Sharescode,
                        ReceiptNo = contrib.ReceiptNo,
                        TransactionNo = contrib.TransactionNo,
                        CompanyCode = contrib.CompanyCode,
                        ContributionCategory = category
                    };

                    var canonicalData = _cryptoService.GetCanonicalData(txDataForSigning);
                    contrib.CanonicalData = canonicalData;

                    var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                    // Decrypt private key
                    var privateKeyBase64 = EncryptionHelper.Decrypt(memberWallet.PrivateKeyEncrypted);
                    var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

                    // Sign using ECDSA P-256
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

                    var signatureBytes = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
                    var signature = Convert.ToBase64String(signatureBytes);

                    // Generate transaction hash
                    using var sha = SHA256.Create();
                    var hashBytes = sha.ComputeHash(dataBytes);
                    var transactionHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                    // Increment nonce for each contribution
                    currentNonce++;

                    // Attach signature to contrib
                    contrib.TransactionSignature = signature;
                    contrib.TransactionHash = transactionHash;
                    contrib.TransactionSequence = currentNonce;
                    contrib.IsSignatureVerified = false;

                    // ============================================================
                    // STEP 7d: CHAIN LINKING (SAME AS SINGLE)
                    // ============================================================
                    if (lastContrib != null)
                    {
                        contrib.PreviousTransactionHash = lastContrib.TransactionHash;
                        _logger.LogDebug($"Member {request.MemberNo} - Linked to previous transaction: {lastContrib.TransactionHash?.Substring(0, 16)}...");
                    }
                    else
                    {
                        if (memberWallet != null && !string.IsNullOrEmpty(memberWallet.Address))
                        {
                            contrib.PreviousTransactionHash = memberWallet.Address;
                            _logger.LogInformation($"✅ FIRST TRANSACTION: Member {request.MemberNo}");
                            _logger.LogInformation($"   Wallet Address (Genesis Anchor): {memberWallet.Address}");
                        }
                        else
                        {
                            var genesisSource = $"{memberRecordForWallet.MemberNo}|{memberRecordForWallet.Idno}|{memberRecordForWallet.ApplicDate?.ToString("o") ?? DateTime.UtcNow.ToString("o")}";
                            var genesisHash = _cryptoService.ComputeHash(genesisSource);
                            contrib.PreviousTransactionHash = genesisHash;
                            _logger.LogWarning($"Member {request.MemberNo} - No wallet found, using deterministic genesis hash: {genesisHash}");
                        }
                    }

                    // Update lastContrib for next iteration
                    lastContrib = contrib;

                    // Add to list for batch save
                    allContribs.Add(contrib);

                    // ============================================================
                    // STEP 7e: HANDLE PROMPT PAYMENT (MPESA STK) - SAME AS SINGLE
                    // ============================================================
                    if (request.PromptPayment)
                    {
                        try
                        {
                            var res = await CreateTransactionDeposit(contrib, contrib.CompanyCode, contrib.AuditId, contrib.ReceiptNo, null);
                            if (res != null && res.Success == true)
                            {
                                _logger.LogInformation($"Prompt payment initiated for {receiptNo}");
                            }
                            else if (res != null && res.Success == false)
                            {
                                throw new Exception($"Could not process prompt for {request.MemberNo}. Try again.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Prompt payment failed for {receiptNo}: {ex.Message}");
                            // Don't fail the transaction - prompt is optional
                        }
                    }

                    // ============================================================
                    // STEP 7f: CREATE CONTRIB SHARE (SAME AS SINGLE)
                    // ============================================================
                    if (isMajorShareType)
                    {
                        var contribShare = new ContribShare
                        {
                            LocalId = 0, // Will be set after save
                            MemberNo = request.MemberNo,
                            CompanyCode = request.CompanyCode,
                            ReceiptNo = receiptNo,
                            Sharescode = item.SharesCode,
                            Remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                            AuditId = request.CreatedBy,
                            AuditTime = DateTime.Now,
                            AuditDateTime = DateTime.Now,
                            TransactionNo = contrib.TransactionNo,
                            ContrDate = contributionDate,
                            LoanNo = null,
                            Isharecapital = shareType.Issharecapital,
                            DepositedDate = depositedDate,
                            ReceiptDate = receiptDate,
                            ShareCapitalAmount = category == "SHARE_CAPITAL" ? item.Amount : 0,
                            DepositsAmount = category == "DEPOSIT" ? item.Amount : 0,
                            PassBookAmount = category == "PASSBOOK" ? item.Amount : 0,
                            Donor = category == "DONOR" ? item.Amount : 0,
                            LoanAmount = category == "LOAN_REPAYMENT" ? item.Amount : 0,
                            RegFeeAmount = category == "REGISTRATION_FEE" ? item.Amount : 0
                        };

                        allContribShares.Add(contribShare);
                        _logger.LogInformation($"✅ ContribShare created for: {category}, Amount: {item.Amount:C}");
                    }
                    else
                    {
                        _logger.LogInformation($"⏭️ SKIPPING ContribShares for: {shareType.SharesType} ({shareType.SharesCode}) - Category: UNKNOWN");
                    }

                    // ============================================================
                    // STEP 7g: UPDATE SHARE BALANCE (SAME AS SINGLE)
                    // ============================================================
                    if (category == "SHARE_CAPITAL" || (category == "PASSBOOK" && shareType.Issharecapital == true))
                    {
                        // Check if share exists
                        var existingShare = await _context.Shares
                            .FirstOrDefaultAsync(s => s.MemberNo == request.MemberNo &&
                                                     s.Sharescode == item.SharesCode &&
                                                     s.CompanyCode == request.CompanyCode);

                        if (existingShare != null)
                        {
                            // Update existing share
                            existingShare.TotalShares = (existingShare.TotalShares ?? 0) + item.Amount;
                            existingShare.TransDate = DateTime.Now;
                            existingShare.AuditTime = DateTime.Now;
                            existingShare.AuditDateTime = DateTime.Now;
                            existingShare.AuditId = request.CreatedBy;
                        }
                        else
                        {
                            // Create new share record
                            var newShare = new Share
                            {
                                MemberNo = request.MemberNo,
                                Sharescode = item.SharesCode,
                                TotalShares = item.Amount,
                                Initshares = item.Amount,
                                CompanyCode = request.CompanyCode,
                                TransDate = DateTime.Now,
                                AuditTime = DateTime.Now,
                                AuditDateTime = DateTime.Now,
                                AuditId = request.CreatedBy
                            };
                            _context.Shares.Add(newShare);
                        }

                        _logger.LogInformation($"Updated share balance for {request.MemberNo} - {item.SharesCode}: +{item.Amount:C}");
                    }

                    // ============================================================
                    // STEP 7h: UPDATE WALLET BALANCE (SAME AS SINGLE)
                    // ============================================================
                    if (memberWallet != null)
                    {
                        memberWallet.LastActivity = DateTime.UtcNow;

                        switch (category)
                        {
                            case "SHARE_CAPITAL":
                                // Update Capital Balance (for shares)
                                memberWallet.CapitalBalance += item.Amount;
                                memberWallet.Balance += item.Amount; // Also update total balance
                                _logger.LogInformation($"Updated CapitalBalance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.CapitalBalance:C}");
                                break;

                            case "DEPOSIT":
                                // Update Deposit Balance (savings/deposits)
                                memberWallet.DepositBalance += item.Amount;
                                memberWallet.Balance += item.Amount; // Also update total balance
                                _logger.LogInformation($"Updated DepositBalance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.DepositBalance:C}");
                                break;

                            case "PASSBOOK":
                                // For PASSBOOK, update CapitalBalance if Issharecapital=1, otherwise Balance
                                if (shareType.Issharecapital == true)
                                {
                                    memberWallet.CapitalBalance += item.Amount;
                                    _logger.LogInformation($"Updated CapitalBalance (PASSBOOK) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
                                }
                                else
                                {
                                    memberWallet.Balance += item.Amount;
                                    _logger.LogInformation($"Updated Balance (PASSBOOK) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
                                }
                                break;

                            case "LOAN_REPAYMENT":
                                // For loan repayment, update Balance
                                memberWallet.Balance += item.Amount;
                                _logger.LogInformation($"Updated Balance (LOAN_REPAYMENT) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
                                break;

                            case "DONOR":
                                // For donor contributions, update Balance
                                memberWallet.Balance += item.Amount;
                                _logger.LogInformation($"Updated Balance (DONOR) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
                                break;

                            case "REGISTRATION_FEE":
                                // Registration fees don't add to balance (they're fees)
                                _logger.LogInformation($"Registration fee of {item.Amount:C} collected from member {memberRecordForWallet.MemberNo} (no balance update)");
                                break;

                            default:
                                // Default - update general balance
                                memberWallet.Balance += item.Amount;
                                _logger.LogInformation($"Updated Balance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.Balance:C}");
                                break;
                        }
                    }

                    // ============================================================
                    // STEP 7i: CREATE GL TRANSACTION (SAME AS SINGLE)
                    // ============================================================
                    var glTransaction = new Gltransaction
                    {
                        TransDate = contributionDate,
                        Amount = item.Amount,
                        DrAccNo = drAcc,
                        CrAccNo = crAcc,
                        Temp = "N",
                        DocumentNo = receiptNo,
                        Source = request.MemberNo,
                        CompanyCode = request.CompanyCode,
                        TransDescript = $"{category} - {request.MemberNo} [BULK: {combinedReceiptNo}]",
                        AuditTime = DateTime.Now,
                        AuditDateTime = DateTime.Now,
                        AuditId = request.CreatedBy,
                        Cash = paymentMethod == "CASH" ? 1 : 0,
                        DocPosted = 1,
                        ChequeNo = paymentMethod == "CHEQUE" ? item.ReferenceNo : null,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = contrib.TransactionNo,
                        Module = "SHARES",
                        ReconId = 0
                    };

                    allGlTransactions.Add(glTransaction);

                    // ============================================================
                    // STEP 7j: CREATE BLOCK (SAME AS SINGLE)
                    // ============================================================
                    string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                    if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                    else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                    var block = new Block
                    {
                        BlockHash = blockHash,
                        PreviousHash = lastBlockHash,
                        Timestamp = DateTime.Now,
                        Nonce = 0,
                        MerkleRoot = Guid.NewGuid().ToString(),
                        Confirmed = true,
                        CreatedAt = DateTime.Now
                    };

                    allBlocks.Add(block);

                    // Update lastBlockHash for next block
                    lastBlockHash = blockHash;

                    // ============================================================
                    // STEP 7k: CREATE BLOCKCHAIN TRANSACTION (SAME AS SINGLE)
                    // ============================================================
                    var blockchainData = new
                    {
                        TransactionType = "CONTRIBUTION",
                        MemberNo = request.MemberNo,
                        MemberName = $"{member.Surname} {member.OtherNames}",
                        ShareType = shareType.SharesType,
                        ShareTypeCode = shareType.SharesCode,
                        ContributionCategory = category,
                        Amount = item.Amount,
                        ReceiptNo = receiptNo,
                        ContributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        DepositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        ReceiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        PaymentMethod = paymentMethod,
                        ReferenceNo = item.ReferenceNo,
                        Remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                        CompanyCode = request.CompanyCode,
                        CreatedBy = request.CreatedBy,
                        DrAccount = drAcc,
                        CrAccount = crAcc,
                        BlockHash = blockHash,
                        BulkReceiptNo = combinedReceiptNo,
                        CumulativeTotalAfter = existingTotal + item.Amount,
                        MaxLimit = shareType.MaxAmount,
                        RemainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + item.Amount) : (decimal?)null
                    };

                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "CONTRIBUTION",
                        MemberNo = request.MemberNo,
                        CompanyCode = request.CompanyCode,
                        Amount = item.Amount,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                        OffChainReferenceId = receiptNo,
                        Status = "CONFIRMED",
                        BlockHash = block.BlockHash,
                        CreatedAt = DateTime.Now
                    };

                    allBlockchainTx.Add(blockchainTx);

                    // ============================================================
                    // STEP 7l: UPDATE REFERENCES (SAME AS SINGLE)
                    // ============================================================
                    // Update contrib with blockchain transaction ID
                    contrib.BlockchainTxId = blockchainTx.TransactionId;

                    // Update glTransaction with blockchain transaction ID
                    glTransaction.BlockchainTxId = blockchainTx.TransactionId;

                    // ============================================================
                    // STEP 7m: PREPARE AUDIT DATA (SAME AS SINGLE)
                    // ============================================================
                    var auditExtraData = new
                    {
                        amount = item.Amount,
                        memberName = $"{member.Surname} {member.OtherNames}",
                        memberNumber = request.MemberNo,
                        shareType = shareType.SharesType,
                        shareTypeCode = item.SharesCode,
                        contributionCategory = category,
                        receiptNumber = receiptNo,
                        bulkReceiptNo = combinedReceiptNo,
                        contributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        depositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        receiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        paymentMethod = paymentMethod,
                        referenceNo = item.ReferenceNo ?? "",
                        remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                        cumulativeTotalAfter = existingTotal + item.Amount,
                        maxLimit = shareType.MaxAmount,
                        remainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + item.Amount) : (decimal?)null,
                        drAccount = drAcc,
                        crAccount = crAcc,
                        blockchainTxId = blockchainTx.TransactionId
                    };

                    var contribForAudit = new
                    {
                        MemberNo = request.MemberNo,
                        ContrDate = contributionDate,
                        DepositedDate = depositedDate,
                        ReceiptDate = receiptDate,
                        Amount = item.Amount,
                        ReceiptNo = receiptNo,
                        BulkReceiptNo = combinedReceiptNo,
                        Remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                        Sharescode = item.SharesCode,
                        TransactionNo = transactionNo,
                        CompanyCode = request.CompanyCode,
                        BlockchainTxId = blockchainTx.TransactionId,
                        CreatedAt = DateTime.Now,
                        CreatedBy = request.CreatedBy
                    };

                    allAuditData.Add((auditExtraData, contribForAudit, receiptNo));

                    // ============================================================
                    // STEP 7n: ADD TO RESPONSE (SAME AS SINGLE)
                    // ============================================================
                    var cumulativeShareBalance = await GetMemberShareBalanceAsync(request.MemberNo);

                    savedContributions.Add(new ContributionResponseDTO
                    {
                        Id = contrib.Id,
                        MemberNo = request.MemberNo,
                        MemberName = $"{member.Surname} {member.OtherNames}",
                        TransactionDate = contributionDate,
                        DepositedDate = depositedDate,
                        ReceiptDate = receiptDate,
                        SharesCode = item.SharesCode,
                        ShareTypeName = shareType.SharesType ?? shareType.SharesCode,
                        Amount = item.Amount,
                        ShareCapitalAmount = category == "SHARE_CAPITAL" ? item.Amount : 0,
                        DepositsAmount = category == "DEPOSIT" ? item.Amount : 0,
                        RegFeeAmount = category == "REGISTRATION_FEE" ? item.Amount : 0,
                        Donor = category == "DONOR" ? item.Amount : 0,
                        LoanAmount = category == "LOAN_REPAYMENT" ? item.Amount : 0,
                        PassBookAmount = category == "PASSBOOK" ? item.Amount : 0,
                        TotalSharesAfter = cumulativeShareBalance,
                        ReceiptNo = receiptNo,
                        Remarks = $"{item.Remarks} [BULK: {combinedReceiptNo}]",
                        BlockchainTxId = blockchainTx.TransactionId,
                        CreatedAt = DateTime.Now,
                        CreatedBy = request.CreatedBy,
                        CompanyCode = request.CompanyCode,
                        TransactionNo = transactionNo,
                        TransactionHash = contrib.TransactionHash,
                        TransactionSignature = contrib.TransactionSignature,
                        IsSignatureVerified = contrib.IsSignatureVerified ?? false
                    });

                    totalAmount += item.Amount;
                }

                // ============================================================
                // STEP 8: ADD ALL ENTITIES TO CONTEXT
                // ============================================================
                foreach (var contrib in allContribs)
                {
                    _context.Contribs.Add(contrib);
                }

                foreach (var contribShare in allContribShares)
                {
                    _context.ContribShares.Add(contribShare);
                }

                foreach (var glTrans in allGlTransactions)
                {
                    _context.Gltransactions.Add(glTrans);
                }

                foreach (var block in allBlocks)
                {
                    _context.Blocks.Add(block);
                }

                foreach (var blockchainTx in allBlockchainTx)
                {
                    _context.BlockchainTransactions.Add(blockchainTx);
                }

                // Update wallet if changed
                if (memberWallet != null)
                {
                    _context.Wallets.Update(memberWallet);
                }

                // ============================================================
                // STEP 9: SAVE ALL CHANGES AT ONCE (ONLY ONCE!)
                // ============================================================
                await _context.SaveChangesAsync();

                // ============================================================
                // STEP 10: UPDATE LOCAL IDs FOR CONTRIB SHARES (SAME AS SINGLE)
                // ============================================================
                // After SaveChanges, contrib.Id is generated
                foreach (var contrib in allContribs)
                {
                    var matchingShares = allContribShares.Where(cs => cs.TransactionNo == contrib.TransactionNo).ToList();
                    foreach (var share in matchingShares)
                    {
                        share.LocalId = contrib.Id;
                        // Also ensure the BlockchainTxId is set
                        if (string.IsNullOrEmpty(share.BlockchainTxId) && !string.IsNullOrEmpty(contrib.BlockchainTxId))
                        {
                            share.BlockchainTxId = contrib.BlockchainTxId;
                        }
                    }
                }

                // Only save again if we updated LocalIds
                if (allContribShares.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // STEP 11: SAVE AUDIT TRAILS (SAME AS SINGLE)
                // ============================================================
                foreach (var auditData in allAuditData)
                {
                    var (extraData, newModel, receiptNo) = auditData;

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: newModel,
                        tableName: "Contribs",
                        recordId: receiptNo,
                        userId: request.CreatedBy,
                        userName: request.CreatedBy,
                        companyCode: request.CompanyCode,
                        module: "Contributions",
                        extraData: System.Text.Json.JsonSerializer.Serialize(extraData),
                        blockchainTxId: ((dynamic)newModel).BlockchainTxId
                    );
                }

                // ============================================================
                // STEP 12: COMMIT TRANSACTION (SAME AS SINGLE)
                // ============================================================
                await transaction.CommitAsync();

                // ============================================================
                // STEP 13: BUILD RESPONSE
                // ============================================================
                response.Success = true;
                response.Message = $"All {savedContributions.Count} contributions saved successfully!";
                response.ReceiptNo = combinedReceiptNo; // Return the bulk receipt number
                response.SavedCount = savedContributions.Count;
                response.TotalAmount = totalAmount;
                response.Contributions = savedContributions;

                _logger.LogInformation($"Bulk contribution completed: {savedContributions.Count} items, Total: {totalAmount:C}, Bulk Receipt: {combinedReceiptNo}");

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in bulk contribution");

                response.Errors.Add(ex.Message);
                response.Message = $"An error occurred while processing bulk contributions: {ex.Message}";
                return response;
            }
        }


        //public async Task<BulkContributionResponseDTO> BulkAddContributionsAsync(BulkContributionRequestDTO request)
        //{
        //    var response = new BulkContributionResponseDTO
        //    {
        //        Success = false,
        //        Errors = new List<string>()
        //    };

        //    // ============================================================
        //    // STEP 1: BEGIN TRANSACTION (SAME AS SINGLE)
        //    // ============================================================
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        _logger.LogInformation($"Starting bulk contribution for member: {request.MemberNo}, Count: {request.Contributions.Count}");

        //        // ============================================================
        //        // STEP 2: VALIDATE MEMBER EXISTS (SAME AS SINGLE)
        //        // ============================================================
        //        var member = await _context.Members
        //            .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo && m.CompanyCode == request.CompanyCode);

        //        if (member == null)
        //        {
        //            response.Message = $"Member {request.MemberNo} not found";
        //            return response;
        //        }

        //        // ============================================================
        //        // STEP 3: VALIDATE SHARE TYPES EXIST (SAME AS SINGLE)
        //        // ============================================================
        //        var shareTypeCodes = request.Contributions.Select(c => c.SharesCode).Distinct().ToList();
        //        var shareTypes = await _context.Sharetypes
        //            .Where(st => shareTypeCodes.Contains(st.SharesCode) && st.CompanyCode == request.CompanyCode)
        //            .ToDictionaryAsync(st => st.SharesCode, st => st);

        //        var invalidShares = shareTypeCodes.Where(code => !shareTypes.ContainsKey(code)).ToList();
        //        if (invalidShares.Any())
        //        {
        //            response.Errors.Add($"Invalid share types: {string.Join(", ", invalidShares)}");
        //            response.Message = "Some share types are invalid";
        //            return response;
        //        }

        //        // ============================================================
        //        // STEP 4: PREPARE FOR MULTIPLE CONTRIBUTIONS
        //        // ============================================================
        //        var validatedContributions = new List<ValidatedContribution>();
        //        var validationErrors = new List<string>();

        //        // Get Sacco parameters once (SAME AS SINGLE)
        //        var sacco = await _context.SaccoParram
        //            .FirstOrDefaultAsync(s => s.CompanyCode == request.CompanyCode);

        //        // ============================================================
        //        // STEP 5: VALIDATE EACH CONTRIBUTION (SAME AS SINGLE)
        //        // ============================================================
        //        foreach (var item in request.Contributions)
        //        {
        //            try
        //            {
        //                var shareType = shareTypes[item.SharesCode];

        //                // STEP 5a: Determine contribution type (SAME AS SINGLE)
        //                string contributionCategory = DetermineContributionType(shareType, new ContributionDTO { SharesCode = item.SharesCode });
        //                bool isMajorShareType = contributionCategory != "UNKNOWN";

        //                _logger.LogInformation($"Contribution Category: {contributionCategory}, IsMajor: {isMajorShareType}");

        //                // STEP 5b: Get existing total (SAME AS SINGLE)
        //                decimal existingTotal = await GetExistingContributionTotalAsync(
        //                    request.MemberNo,
        //                    item.SharesCode,
        //                    contributionCategory,
        //                    request.CompanyCode);

        //                // STEP 5c: Validate amount against minimum (SAME AS SINGLE)
        //                if (existingTotal < shareType.MinAmount && item.Amount < shareType.MinAmount)
        //                {
        //                    throw new ValidationException(
        //                        $"Amount cannot be less than minimum of {shareType.MinAmount:C}. " +
        //                        $"Current total: {existingTotal:C}. You need to contribute at least {shareType.MinAmount - existingTotal:C} more to reach the minimum."
        //                    );
        //                }

        //                decimal newTotal = existingTotal + item.Amount;

        //                // STEP 5d: Check maximum contribution limit (SAME AS SINGLE)
        //                if (shareType.MaxAmount.HasValue && newTotal > shareType.MaxAmount.Value)
        //                {
        //                    decimal remainingAllowed = shareType.MaxAmount.Value - existingTotal;
        //                    if (remainingAllowed <= 0)
        //                    {
        //                        throw new ValidationException(
        //                            $"Maximum {shareType.SharesType} limit of {shareType.MaxAmount.Value:C} has already been reached. " +
        //                            $"Current total: {existingTotal:C}. No further contributions allowed.");
        //                    }
        //                    else
        //                    {
        //                        throw new ValidationException(
        //                            $"Amount {item.Amount:C} exceeds remaining limit for {shareType.SharesType}. " +
        //                            $"Current total: {existingTotal:C}, Maximum: {shareType.MaxAmount.Value:C}, " +
        //                            $"Remaining allowed: {remainingAllowed:C}. Please reduce the amount.");
        //                    }
        //                }

        //                // STEP 5e: Validate prerequisites (SAME AS SINGLE)
        //                await ValidateContributionPrerequisitesBulkAsync(
        //                    request.MemberNo,
        //                    item.SharesCode,
        //                    request.CompanyCode);

        //                // STEP 5f: Validate dates (SAME AS SINGLE)
        //                DateTime contributionDate = item.TransactionDate ?? DateTime.Now;
        //                DateTime depositedDate;
        //                if (item.DepositedDate.HasValue)
        //                {
        //                    depositedDate = item.DepositedDate.Value.Date;
        //                    if (depositedDate > DateTime.Now.Date)
        //                    {
        //                        throw new ValidationException("Deposit date cannot be in the future.");
        //                    }
        //                }
        //                else
        //                {
        //                    depositedDate = DateTime.Now.Date;
        //                }
        //                DateTime receiptDate = depositedDate;

        //                // STEP 5g: Get GL Accounts (SAME AS SINGLE)
        //                string drAcc = null;
        //                string crAcc = shareType.SharesAcc;

        //                if (string.IsNullOrEmpty(crAcc))
        //                {
        //                    throw new Exception($"Share type '{item.SharesCode}' does not have a GL Account configured.");
        //                }

        //                string paymentMethod = item.PaymentMethod?.ToUpper() ?? "CASH";

        //                switch (paymentMethod)
        //                {
        //                    case "BANK TRANSFER":
        //                    case "CHEQUE":
        //                        if (!string.IsNullOrEmpty(item.ReferenceNo))
        //                        {
        //                            var bank = await _context.Banks
        //                                .FirstOrDefaultAsync(b => b.CompanyCode == request.CompanyCode &&
        //                                                          b.IsActive == true &&
        //                                                          (b.BankCode == item.ReferenceNo ||
        //                                                           b.BankName.Contains(item.ReferenceNo) ||
        //                                                           b.AccountNumber == item.ReferenceNo));

        //                            if (bank != null && !string.IsNullOrEmpty(bank.GlAccountNo))
        //                            {
        //                                drAcc = bank.GlAccountNo;
        //                                break;
        //                            }
        //                        }

        //                        var defaultBank = await _context.Banks
        //                            .FirstOrDefaultAsync(b => b.CompanyCode == request.CompanyCode &&
        //                                                      b.IsActive == true &&
        //                                                      !string.IsNullOrEmpty(b.GlAccountNo));

        //                        if (defaultBank != null)
        //                        {
        //                            drAcc = defaultBank.GlAccountNo;
        //                            break;
        //                        }
        //                        goto case "CASH";

        //                    case "MOBILE MONEY":
        //                    case "CASH":
        //                    default:
        //                        if (sacco != null && !string.IsNullOrEmpty(sacco.RetainedEarnings))
        //                        {
        //                            drAcc = sacco.RetainedEarnings;
        //                        }
        //                        else
        //                        {
        //                            var cashAccount = await _context.GlSetup
        //                                .FirstOrDefaultAsync(g => g.CompanyCode == request.CompanyCode &&
        //                                                          g.Status == true &&
        //                                                          (g.Type == "ASSET" || g.Type == "Asset") &&
        //                                                          (g.Glaccname != null &&
        //                                                           (g.Glaccname.ToLower().Contains("cash") ||
        //                                                            g.Glaccname.ToLower().Contains("bank"))));

        //                            if (cashAccount != null)
        //                            {
        //                                drAcc = cashAccount.AccNo;
        //                            }
        //                        }
        //                        break;
        //                }

        //                if (string.IsNullOrEmpty(drAcc))
        //                {
        //                    var suspenseAccount = await _context.GlSetup
        //                        .FirstOrDefaultAsync(g => g.CompanyCode == request.CompanyCode &&
        //                                                  g.IsSuspense == true);
        //                    if (suspenseAccount != null)
        //                    {
        //                        drAcc = suspenseAccount.AccNo;
        //                    }
        //                }

        //                // STEP 5h: Check/Create wallet (SAME AS SINGLE)
        //                var memberRecord = await _context.Members
        //                    .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo);

        //                if (memberRecord != null)
        //                {
        //                    var memberHasWallet = await _cryptoService.HasWalletAsync(memberRecord.MobileNo);
        //                    if (!memberHasWallet)
        //                    {
        //                        _logger.LogWarning($"Member {memberRecord.MemberNo} has no wallet. Creating now...");
        //                        var walletResult = await _cryptoService.CreateWalletForMemberAsync(
        //                            memberRecord.Id,
        //                            memberRecord.MemberNo,
        //                            request.CompanyCode);
        //                        if (!walletResult.Success)
        //                        {
        //                            throw new Exception($"Cannot process transaction: {walletResult.Message}");
        //                        }
        //                    }
        //                }

        //                // STORE VALIDATED CONTRIBUTION
        //                validatedContributions.Add(new ValidatedContribution
        //                {
        //                    Item = item,
        //                    ShareType = shareType,
        //                    Category = contributionCategory,
        //                    IsMajorShareType = isMajorShareType,
        //                    DrAcc = drAcc,
        //                    CrAcc = crAcc,
        //                    ExistingTotal = existingTotal,
        //                    ContributionDate = contributionDate,
        //                    DepositedDate = depositedDate,
        //                    ReceiptDate = receiptDate,
        //                    PaymentMethod = paymentMethod
        //                });
        //            }
        //            catch (Exception ex)
        //            {
        //                validationErrors.Add($"Validation error for {item.SharesCode}: {ex.Message}");
        //            }
        //        }

        //        if (validationErrors.Any())
        //        {
        //            response.Errors = validationErrors;
        //            response.Message = $"Validation failed: {validationErrors.Count} error(s) found";
        //            return response;
        //        }

        //        if (!validatedContributions.Any())
        //        {
        //            response.Message = "No valid contributions to save";
        //            return response;
        //        }

        //        // ============================================================
        //        // STEP 6: GET/CREATE WALLET ONCE (SAME AS SINGLE)
        //        // ============================================================
        //        var memberRecordForWallet = await _context.Members
        //            .FirstOrDefaultAsync(m => m.MemberNo == request.MemberNo);

        //        if (memberRecordForWallet == null)
        //        {
        //            throw new Exception($"Member {request.MemberNo} not found");
        //        }

        //        // Check if wallet exists, create if not (SAME AS SINGLE)
        //        var hasWallet = await _cryptoService.HasWalletAsync(memberRecordForWallet.MobileNo);
        //        if (!hasWallet)
        //        {
        //            _logger.LogWarning($"Member {memberRecordForWallet.MemberNo} has no wallet. Creating now...");
        //            var walletResult = await _cryptoService.CreateWalletForMemberAsync(
        //                memberRecordForWallet.Id,
        //                memberRecordForWallet.MemberNo,
        //                request.CompanyCode);
        //            if (!walletResult.Success)
        //            {
        //                throw new Exception($"Cannot process transaction: {walletResult.Message}");
        //            }
        //        }

        //        // Get the wallet (should exist now)
        //        var memberWallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecordForWallet.Id);

        //        if (memberWallet == null)
        //        {
        //            // One more attempt to create wallet
        //            var walletResult = await _cryptoService.CreateWalletForMemberAsync(
        //                memberRecordForWallet.Id,
        //                memberRecordForWallet.MemberNo,
        //                request.CompanyCode);

        //            if (!walletResult.Success)
        //            {
        //                throw new Exception($"Failed to create wallet: {walletResult.Message}");
        //            }
        //            memberWallet = walletResult.Wallet;
        //        }

        //        // ============================================================
        //        // STEP 7: PROCESS EACH CONTRIBUTION
        //        // ============================================================
        //        var savedContributions = new List<ContributionResponseDTO>();
        //        decimal totalAmount = 0;

        //        // Lists to collect entities for batch save
        //        var allContribs = new List<Contrib>();
        //        var allContribShares = new List<ContribShare>();
        //        var allGlTransactions = new List<Gltransaction>();
        //        var allBlocks = new List<Block>();
        //        var allBlockchainTx = new List<BlockchainTransaction>();
        //        var allAuditData = new List<(object ExtraData, object NewModel, string ReceiptNo)>();

        //        // Get last block hash once
        //        var lastBlockHash = await GetLastBlockHashAsync();

        //        // Get last contrib for sequence number
        //        var lastContrib = await _context.Contribs
        //            .Where(c => c.MemberNo == request.MemberNo)
        //            .OrderByDescending(c => c.Id)
        //            .FirstOrDefaultAsync();

        //        // Use long for nonce to match database type
        //        long currentNonce = lastContrib?.TransactionSequence ?? 0;

        //        foreach (var validated in validatedContributions)
        //        {
        //            var item = validated.Item;
        //            var shareType = validated.ShareType;
        //            var category = validated.Category;
        //            var isMajorShareType = validated.IsMajorShareType;
        //            var drAcc = validated.DrAcc;
        //            var crAcc = validated.CrAcc;
        //            var existingTotal = validated.ExistingTotal;
        //            var contributionDate = validated.ContributionDate;
        //            var depositedDate = validated.DepositedDate;
        //            var receiptDate = validated.ReceiptDate;
        //            var paymentMethod = validated.PaymentMethod;

        //            _logger.LogInformation($"Processing bulk contribution: {item.SharesCode} - {item.Amount:C}");

        //            // Generate receipt and transaction numbers
        //            var receiptNo = GenerateReceiptNumber(request.CompanyCode);
        //            var transactionNo = GenerateTransactionNumber(request.CompanyCode);

        //            // ============================================================
        //            // STEP 7a: CREATE CONTRIB RECORD (SAME AS SINGLE)
        //            // ============================================================
        //            var contrib = new Contrib
        //            {
        //                MemberNo = request.MemberNo,
        //                ContrDate = contributionDate,
        //                Amount = item.Amount,
        //                CompanyCode = request.CompanyCode,
        //                ReceiptNo = receiptNo,
        //                Remarks = item.Remarks,
        //                AuditId = request.CreatedBy,
        //                AuditTime = DateTime.Now,
        //                AuditDateTime = DateTime.Now,
        //                Sharescode = item.SharesCode,
        //                TransactionNo = transactionNo,
        //                Posted = "Y",
        //                Locked = "N",
        //                StaffNo = null,
        //                DepositedDate = depositedDate,
        //                ReceiptDate = receiptDate,
        //                RefNo = item.ReferenceNo,
        //                ShareBal = 0,
        //                TransBy = request.CreatedBy,
        //                ChequeNo = item.PaymentMethod == "CHEQUE" ? item.ReferenceNo : null,
        //                TransDate = contributionDate,
        //                SharesAcc = shareType.SharesAcc,
        //                ContraAcc = shareType.ContraAcc,
        //                CashBookdate = DateTime.Now,
        //                Dregard = 0,
        //                Offs = 0,
        //                ApiKey = null,
        //                UserName = request.CreatedBy,
        //                Run = 0,
        //                Run2 = 0,
        //                MrCleared = "N",
        //                Mrno = null,
        //                Offset = false,
        //                TransferDesc = null,
        //                Schemecode = request.CompanyCode,
        //                Status = "Active"
        //            };

        //            // ============================================================
        //            // STEP 7b: FRAUD DETECTION (SAME AS SINGLE)
        //            // ============================================================
        //            var fraudResult = await _cryptoService.AnalyzeTransactionAsync(
        //                request.MemberNo,
        //                item.Amount,
        //                category);

        //            if (fraudResult.ShouldBlock)
        //            {
        //                throw new Exception($"Transaction blocked by fraud detection: {string.Join(", ", fraudResult.Flags)}");
        //            }

        //            if (fraudResult.IsSuspicious)
        //            {
        //                contrib.Remarks = $"{contrib.Remarks}";
        //            }

        //            // ============================================================
        //            // STEP 7c: SIGN THE TRANSACTION (SAME AS SINGLE)
        //            // ============================================================
        //            var txDataForSigning = new
        //            {
        //                MemberNo = contrib.MemberNo,
        //                Amount = contrib.Amount,
        //                TransactionDate = contrib.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
        //                SharesCode = contrib.Sharescode,
        //                ReceiptNo = contrib.ReceiptNo,
        //                TransactionNo = contrib.TransactionNo,
        //                CompanyCode = contrib.CompanyCode,
        //                ContributionCategory = category
        //            };

        //            var canonicalData = _cryptoService.GetCanonicalData(txDataForSigning);
        //            contrib.CanonicalData = canonicalData;

        //            var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

        //            // Decrypt private key
        //            var privateKeyBase64 = EncryptionHelper.Decrypt(memberWallet.PrivateKeyEncrypted);
        //            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

        //            // Sign using ECDSA P-256
        //            using var ecdsa = ECDsa.Create();
        //            ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

        //            var signatureBytes = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
        //            var signature = Convert.ToBase64String(signatureBytes);

        //            // Generate transaction hash
        //            using var sha = SHA256.Create();
        //            var hashBytes = sha.ComputeHash(dataBytes);
        //            var transactionHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        //            // Increment nonce for each contribution
        //            currentNonce++;

        //            // Attach signature to contrib
        //            contrib.TransactionSignature = signature;
        //            contrib.TransactionHash = transactionHash;
        //            contrib.TransactionSequence = currentNonce;
        //            contrib.IsSignatureVerified = false;

        //            // ============================================================
        //            // STEP 7d: CHAIN LINKING (SAME AS SINGLE)
        //            // ============================================================
        //            if (lastContrib != null)
        //            {
        //                contrib.PreviousTransactionHash = lastContrib.TransactionHash;
        //                _logger.LogDebug($"Member {request.MemberNo} - Linked to previous transaction: {lastContrib.TransactionHash?.Substring(0, 16)}...");
        //            }
        //            else
        //            {
        //                if (memberWallet != null && !string.IsNullOrEmpty(memberWallet.Address))
        //                {
        //                    contrib.PreviousTransactionHash = memberWallet.Address;
        //                    _logger.LogInformation($"✅ FIRST TRANSACTION: Member {request.MemberNo}");
        //                    _logger.LogInformation($"   Wallet Address (Genesis Anchor): {memberWallet.Address}");
        //                }
        //                else
        //                {
        //                    var genesisSource = $"{memberRecordForWallet.MemberNo}|{memberRecordForWallet.Idno}|{memberRecordForWallet.ApplicDate?.ToString("o") ?? DateTime.UtcNow.ToString("o")}";
        //                    var genesisHash = _cryptoService.ComputeHash(genesisSource);
        //                    contrib.PreviousTransactionHash = genesisHash;
        //                    _logger.LogWarning($"Member {request.MemberNo} - No wallet found, using deterministic genesis hash: {genesisHash}");
        //                }
        //            }

        //            // Update lastContrib for next iteration
        //            lastContrib = contrib;

        //            // Add to list for batch save
        //            allContribs.Add(contrib);

        //            // ============================================================
        //            // STEP 7e: HANDLE PROMPT PAYMENT (MPESA STK) - SAME AS SINGLE
        //            // ============================================================
        //            if (request.PromptPayment)
        //            {
        //                try
        //                {
        //                    var res = await CreateTransactionDeposit(contrib, contrib.CompanyCode, contrib.AuditId, contrib.ReceiptNo, null);
        //                    if (res != null && res.Success == true)
        //                    {
        //                        _logger.LogInformation($"Prompt payment initiated for {receiptNo}");
        //                    }
        //                    else if (res != null && res.Success == false)
        //                    {
        //                        throw new Exception($"Could not process prompt for {request.MemberNo}. Try again.");
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    _logger.LogWarning($"Prompt payment failed for {receiptNo}: {ex.Message}");
        //                    // Don't fail the transaction - prompt is optional
        //                }
        //            }

        //            // ============================================================
        //            // STEP 7f: CREATE CONTRIB SHARE (SAME AS SINGLE)
        //            // ============================================================
        //            if (isMajorShareType)
        //            {
        //                var contribShare = new ContribShare
        //                {
        //                    LocalId = 0, // Will be set after save
        //                    MemberNo = request.MemberNo,
        //                    CompanyCode = request.CompanyCode,
        //                    ReceiptNo = receiptNo,
        //                    Sharescode = item.SharesCode,
        //                    Remarks = item.Remarks,
        //                    AuditId = request.CreatedBy,
        //                    AuditTime = DateTime.Now,
        //                    AuditDateTime = DateTime.Now,
        //                    TransactionNo = contrib.TransactionNo,
        //                    ContrDate = contributionDate,
        //                    LoanNo = null,
        //                    Isharecapital = shareType.Issharecapital,
        //                    DepositedDate = depositedDate,
        //                    ReceiptDate = receiptDate,
        //                    ShareCapitalAmount = category == "SHARE_CAPITAL" ? item.Amount : 0,
        //                    DepositsAmount = category == "DEPOSIT" ? item.Amount : 0,
        //                    PassBookAmount = category == "PASSBOOK" ? item.Amount : 0,
        //                    Donor = category == "DONOR" ? item.Amount : 0,
        //                    LoanAmount = category == "LOAN_REPAYMENT" ? item.Amount : 0,
        //                    RegFeeAmount = category == "REGISTRATION_FEE" ? item.Amount : 0
        //                };

        //                allContribShares.Add(contribShare);
        //                _logger.LogInformation($"✅ ContribShare created for: {category}, Amount: {item.Amount:C}");
        //            }
        //            else
        //            {
        //                _logger.LogInformation($"⏭️ SKIPPING ContribShares for: {shareType.SharesType} ({shareType.SharesCode}) - Category: UNKNOWN");
        //            }

        //            // ============================================================
        //            // STEP 7g: UPDATE SHARE BALANCE (SAME AS SINGLE)
        //            // ============================================================
        //            if (category == "SHARE_CAPITAL" || (category == "PASSBOOK" && shareType.Issharecapital == true))
        //            {
        //                // Check if share exists
        //                var existingShare = await _context.Shares
        //                    .FirstOrDefaultAsync(s => s.MemberNo == request.MemberNo &&
        //                                             s.Sharescode == item.SharesCode &&
        //                                             s.CompanyCode == request.CompanyCode);

        //                if (existingShare != null)
        //                {
        //                    // Update existing share
        //                    existingShare.TotalShares = (existingShare.TotalShares ?? 0) + item.Amount;
        //                    existingShare.TransDate = DateTime.Now;
        //                    existingShare.AuditTime = DateTime.Now;
        //                    existingShare.AuditDateTime = DateTime.Now;
        //                    existingShare.AuditId = request.CreatedBy;
        //                }
        //                else
        //                {
        //                    // Create new share record
        //                    var newShare = new Share
        //                    {
        //                        MemberNo = request.MemberNo,
        //                        Sharescode = item.SharesCode,
        //                        TotalShares = item.Amount,
        //                        Initshares = item.Amount,
        //                        CompanyCode = request.CompanyCode,
        //                        TransDate = DateTime.Now,
        //                        AuditTime = DateTime.Now,
        //                        AuditDateTime = DateTime.Now,
        //                        AuditId = request.CreatedBy
        //                    };
        //                    _context.Shares.Add(newShare);
        //                }

        //                _logger.LogInformation($"Updated share balance for {request.MemberNo} - {item.SharesCode}: +{item.Amount:C}");
        //            }

        //            // ============================================================
        //            // STEP 7h: UPDATE WALLET BALANCE (SAME AS SINGLE)
        //            // ============================================================
        //            if (memberWallet != null)
        //            {
        //                memberWallet.LastActivity = DateTime.UtcNow;

        //                switch (category)
        //                {
        //                    case "SHARE_CAPITAL":
        //                        // Update Capital Balance (for shares)
        //                        memberWallet.CapitalBalance += item.Amount;
        //                        memberWallet.Balance += item.Amount; // Also update total balance
        //                        _logger.LogInformation($"Updated CapitalBalance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.CapitalBalance:C}");
        //                        break;

        //                    case "DEPOSIT":
        //                        // Update Deposit Balance (savings/deposits)
        //                        memberWallet.DepositBalance += item.Amount;
        //                        memberWallet.Balance += item.Amount; // Also update total balance
        //                        _logger.LogInformation($"Updated DepositBalance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.DepositBalance:C}");
        //                        break;

        //                    case "PASSBOOK":
        //                        // For PASSBOOK, update CapitalBalance if Issharecapital=1, otherwise Balance
        //                        if (shareType.Issharecapital == true)
        //                        {
        //                            memberWallet.CapitalBalance += item.Amount;
        //                            _logger.LogInformation($"Updated CapitalBalance (PASSBOOK) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
        //                        }
        //                        else
        //                        {
        //                            memberWallet.Balance += item.Amount;
        //                            _logger.LogInformation($"Updated Balance (PASSBOOK) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
        //                        }
        //                        break;

        //                    case "LOAN_REPAYMENT":
        //                        // For loan repayment, update Balance
        //                        memberWallet.Balance += item.Amount;
        //                        _logger.LogInformation($"Updated Balance (LOAN_REPAYMENT) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
        //                        break;

        //                    case "DONOR":
        //                        // For donor contributions, update Balance
        //                        memberWallet.Balance += item.Amount;
        //                        _logger.LogInformation($"Updated Balance (DONOR) for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}");
        //                        break;

        //                    case "REGISTRATION_FEE":
        //                        // Registration fees don't add to balance (they're fees)
        //                        _logger.LogInformation($"Registration fee of {item.Amount:C} collected from member {memberRecordForWallet.MemberNo} (no balance update)");
        //                        break;

        //                    default:
        //                        // Default - update general balance
        //                        memberWallet.Balance += item.Amount;
        //                        _logger.LogInformation($"Updated Balance for member {memberRecordForWallet.MemberNo}: +{item.Amount:C}, New: {memberWallet.Balance:C}");
        //                        break;
        //                }
        //            }

        //            // ============================================================
        //            // STEP 7i: CREATE GL TRANSACTION (SAME AS SINGLE)
        //            // ============================================================
        //            var glTransaction = new Gltransaction
        //            {
        //                TransDate = contributionDate,
        //                Amount = item.Amount,
        //                DrAccNo = drAcc,
        //                CrAccNo = crAcc,
        //                Temp = "N",
        //                DocumentNo = receiptNo,
        //                Source = request.MemberNo,
        //                CompanyCode = request.CompanyCode,
        //                TransDescript = $"{category} - {request.MemberNo}",
        //                AuditTime = DateTime.Now,
        //                AuditDateTime = DateTime.Now,
        //                AuditId = request.CreatedBy,
        //                Cash = paymentMethod == "CASH" ? 1 : 0,
        //                DocPosted = 1,
        //                ChequeNo = paymentMethod == "CHEQUE" ? item.ReferenceNo : null,
        //                Dregard = false,
        //                Recon = false,
        //                TransactionNo = contrib.TransactionNo,
        //                Module = "SHARES",
        //                ReconId = 0
        //            };

        //            allGlTransactions.Add(glTransaction);

        //            // ============================================================
        //            // STEP 7j: CREATE BLOCK (SAME AS SINGLE)
        //            // ============================================================
        //            string blockHash = Guid.NewGuid().ToString().Replace("-", "");
        //            if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
        //            else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

        //            var block = new Block
        //            {
        //                BlockHash = blockHash,
        //                PreviousHash = lastBlockHash,
        //                Timestamp = DateTime.Now,
        //                Nonce = 0,
        //                MerkleRoot = Guid.NewGuid().ToString(),
        //                Confirmed = true,
        //                CreatedAt = DateTime.Now
        //            };

        //            allBlocks.Add(block);

        //            // Update lastBlockHash for next block
        //            lastBlockHash = blockHash;

        //            // ============================================================
        //            // STEP 7k: CREATE BLOCKCHAIN TRANSACTION (SAME AS SINGLE)
        //            // ============================================================
        //            var blockchainData = new
        //            {
        //                TransactionType = "CONTRIBUTION",
        //                MemberNo = request.MemberNo,
        //                MemberName = $"{member.Surname} {member.OtherNames}",
        //                ShareType = shareType.SharesType,
        //                ShareTypeCode = shareType.SharesCode,
        //                ContributionCategory = category,
        //                Amount = item.Amount,
        //                ReceiptNo = receiptNo,
        //                ContributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                DepositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                ReceiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                PaymentMethod = paymentMethod,
        //                ReferenceNo = item.ReferenceNo,
        //                Remarks = item.Remarks,
        //                CompanyCode = request.CompanyCode,
        //                CreatedBy = request.CreatedBy,
        //                DrAccount = drAcc,
        //                CrAccount = crAcc,
        //                BlockHash = blockHash,
        //                CumulativeTotalAfter = existingTotal + item.Amount,
        //                MaxLimit = shareType.MaxAmount,
        //                RemainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + item.Amount) : (decimal?)null
        //            };

        //            var blockchainTx = new BlockchainTransaction
        //            {
        //                TransactionId = Guid.NewGuid().ToString(),
        //                TransactionType = "CONTRIBUTION",
        //                MemberNo = request.MemberNo,
        //                CompanyCode = request.CompanyCode,
        //                Amount = item.Amount,
        //                Timestamp = DateTime.Now,
        //                DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
        //                PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
        //                OffChainReferenceId = receiptNo,
        //                Status = "CONFIRMED",
        //                BlockHash = block.BlockHash,
        //                CreatedAt = DateTime.Now
        //            };

        //            allBlockchainTx.Add(blockchainTx);

        //            // ============================================================
        //            // STEP 7l: UPDATE REFERENCES (SAME AS SINGLE)
        //            // ============================================================
        //            // Update contrib with blockchain transaction ID
        //            contrib.BlockchainTxId = blockchainTx.TransactionId;

        //            // Update glTransaction with blockchain transaction ID
        //            glTransaction.BlockchainTxId = blockchainTx.TransactionId;

        //            // ============================================================
        //            // STEP 7m: PREPARE AUDIT DATA (SAME AS SINGLE)
        //            // ============================================================
        //            var auditExtraData = new
        //            {
        //                amount = item.Amount,
        //                memberName = $"{member.Surname} {member.OtherNames}",
        //                memberNumber = request.MemberNo,
        //                shareType = shareType.SharesType,
        //                shareTypeCode = item.SharesCode,
        //                contributionCategory = category,
        //                receiptNumber = receiptNo,
        //                contributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                depositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                receiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                paymentMethod = paymentMethod,
        //                referenceNo = item.ReferenceNo ?? "",
        //                remarks = item.Remarks ?? "",
        //                cumulativeTotalAfter = existingTotal + item.Amount,
        //                maxLimit = shareType.MaxAmount,
        //                remainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + item.Amount) : (decimal?)null,
        //                drAccount = drAcc,
        //                crAccount = crAcc,
        //                blockchainTxId = blockchainTx.TransactionId
        //            };

        //            var contribForAudit = new
        //            {
        //                MemberNo = request.MemberNo,
        //                ContrDate = contributionDate,
        //                DepositedDate = depositedDate,
        //                ReceiptDate = receiptDate,
        //                Amount = item.Amount,
        //                ReceiptNo = receiptNo,
        //                Remarks = item.Remarks,
        //                Sharescode = item.SharesCode,
        //                TransactionNo = transactionNo,
        //                CompanyCode = request.CompanyCode,
        //                BlockchainTxId = blockchainTx.TransactionId,
        //                CreatedAt = DateTime.Now,
        //                CreatedBy = request.CreatedBy
        //            };

        //            allAuditData.Add((auditExtraData, contribForAudit, receiptNo));

        //            // ============================================================
        //            // STEP 7n: ADD TO RESPONSE (SAME AS SINGLE)
        //            // ============================================================
        //            var cumulativeShareBalance = await GetMemberShareBalanceAsync(request.MemberNo);

        //            savedContributions.Add(new ContributionResponseDTO
        //            {
        //                Id = contrib.Id,
        //                MemberNo = request.MemberNo,
        //                MemberName = $"{member.Surname} {member.OtherNames}",
        //                TransactionDate = contributionDate,
        //                DepositedDate = depositedDate,
        //                ReceiptDate = receiptDate,
        //                SharesCode = item.SharesCode,
        //                ShareTypeName = shareType.SharesType ?? shareType.SharesCode,
        //                Amount = item.Amount,
        //                ShareCapitalAmount = category == "SHARE_CAPITAL" ? item.Amount : 0,
        //                DepositsAmount = category == "DEPOSIT" ? item.Amount : 0,
        //                RegFeeAmount = category == "REGISTRATION_FEE" ? item.Amount : 0,
        //                Donor = category == "DONOR" ? item.Amount : 0,
        //                LoanAmount = category == "LOAN_REPAYMENT" ? item.Amount : 0,
        //                PassBookAmount = category == "PASSBOOK" ? item.Amount : 0,
        //                TotalSharesAfter = cumulativeShareBalance,
        //                ReceiptNo = receiptNo,
        //                Remarks = item.Remarks ?? string.Empty,
        //                BlockchainTxId = blockchainTx.TransactionId,
        //                CreatedAt = DateTime.Now,
        //                CreatedBy = request.CreatedBy,
        //                CompanyCode = request.CompanyCode,
        //                TransactionNo = transactionNo,
        //                TransactionHash = contrib.TransactionHash,
        //                TransactionSignature = contrib.TransactionSignature,
        //                IsSignatureVerified = contrib.IsSignatureVerified ?? false
        //            });

        //            totalAmount += item.Amount;
        //        }

        //        // ============================================================
        //        // STEP 8: ADD ALL ENTITIES TO CONTEXT
        //        // ============================================================
        //        foreach (var contrib in allContribs)
        //        {
        //            _context.Contribs.Add(contrib);
        //        }

        //        foreach (var contribShare in allContribShares)
        //        {
        //            _context.ContribShares.Add(contribShare);
        //        }

        //        foreach (var glTrans in allGlTransactions)
        //        {
        //            _context.Gltransactions.Add(glTrans);
        //        }

        //        foreach (var block in allBlocks)
        //        {
        //            _context.Blocks.Add(block);
        //        }

        //        foreach (var blockchainTx in allBlockchainTx)
        //        {
        //            _context.BlockchainTransactions.Add(blockchainTx);
        //        }

        //        // Update wallet if changed
        //        if (memberWallet != null)
        //        {
        //            _context.Wallets.Update(memberWallet);
        //        }

        //        // ============================================================
        //        // STEP 9: SAVE ALL CHANGES AT ONCE (ONLY ONCE!)
        //        // ============================================================
        //        await _context.SaveChangesAsync();

        //        // ============================================================
        //        // STEP 10: UPDATE LOCAL IDs FOR CONTRIB SHARES (SAME AS SINGLE)
        //        // ============================================================
        //        // After SaveChanges, contrib.Id is generated
        //        foreach (var contrib in allContribs)
        //        {
        //            var matchingShares = allContribShares.Where(cs => cs.TransactionNo == contrib.TransactionNo).ToList();
        //            foreach (var share in matchingShares)
        //            {
        //                share.LocalId = contrib.Id;
        //                // Also ensure the BlockchainTxId is set
        //                if (string.IsNullOrEmpty(share.BlockchainTxId) && !string.IsNullOrEmpty(contrib.BlockchainTxId))
        //                {
        //                    share.BlockchainTxId = contrib.BlockchainTxId;
        //                }
        //            }
        //        }

        //        // Only save again if we updated LocalIds
        //        if (allContribShares.Any())
        //        {
        //            await _context.SaveChangesAsync();
        //        }

        //        // ============================================================
        //        // STEP 11: SAVE AUDIT TRAILS (SAME AS SINGLE)
        //        // ============================================================
        //        foreach (var auditData in allAuditData)
        //        {
        //            var (extraData, newModel, receiptNo) = auditData;

        //            await _auditService.SaveLogAsync(
        //                actionType: AuditActionType.Insert,
        //                oldModel: null,
        //                newModel: newModel,
        //                tableName: "Contribs",
        //                recordId: receiptNo,
        //                userId: request.CreatedBy,
        //                userName: request.CreatedBy,
        //                companyCode: request.CompanyCode,
        //                module: "Contributions",
        //                extraData: System.Text.Json.JsonSerializer.Serialize(extraData),
        //                blockchainTxId: ((dynamic)newModel).BlockchainTxId
        //            );
        //        }

        //        // ============================================================
        //        // STEP 12: COMMIT TRANSACTION (SAME AS SINGLE)
        //        // ============================================================
        //        await transaction.CommitAsync();

        //        // ============================================================
        //        // STEP 13: BUILD RESPONSE
        //        // ============================================================
        //        string combinedReceiptNo = $"BULK-{DateTime.Now:yyyyMMddHHmmss}";

        //        response.Success = true;
        //        response.Message = $"All {savedContributions.Count} contributions saved successfully!";
        //        response.ReceiptNo = combinedReceiptNo; // Return the bulk receipt number
        //        response.SavedCount = savedContributions.Count;
        //        response.TotalAmount = totalAmount;
        //        response.Contributions = savedContributions;

        //        _logger.LogInformation($"Bulk contribution completed: {savedContributions.Count} items, Total: {totalAmount:C}");

        //        return response;
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Error in bulk contribution");

        //        response.Errors.Add(ex.Message);
        //        response.Message = $"An error occurred while processing bulk contributions: {ex.Message}";
        //        return response;
        //    }
        //}
        public async Task<dynamic> CreateTransactionDeposit(Contrib ld, string CompanyCode, string UserId, string sessionId, string? email)
        {
            try
            {
                //var _db = _context;
                var member = _context.Members.AsNoTracking().FirstOrDefault(m => m.CompanyCode == CompanyCode && m.MemberNo.Contains(ld.MemberNo));

                String transactionno = System.DateTime.Now.ToString("yyyyMMddHHmmss"); // Better unique format
                var trans = new Transaction();
                var dt = DateTime.Parse(ld.ContrDate.ToString());//;
                var am = 0m;
                decimal.TryParse(ld.Amount.ToString(), out am);

                trans.TransactionNo = transactionno;
                trans.TransDate = dt;
                trans.TransDescription = ld.Remarks + " - " + ld.Sharescode;
                trans.Status = "Active";
                trans.Amount = am;
                trans.Channel = "BLOCKCHAIN";

                //trans.s
                trans.AuditId = sessionId;
                trans.AuditTime = DateTime.Now;
                trans.CompanyCode = CompanyCode;
                var transb = new Transactions2();
                transb.Companycode = CompanyCode;
                transb.TransactionNo = trans.TransactionNo;
                transb.Status = "Active";
                transb.ReceiptNo = transactionno;
                transb.AuditId = trans.AuditId;
                transb.Amount = trans.Amount;
                if (member == null)
                {
                    transb.MemberNo = ld.MemberNo ?? "";
                }
                else
                {
                    if (String.IsNullOrEmpty(ld.MemberNo)) { ld.MemberNo = ""; }
                    transb.MemberNo = member.MemberNo ?? ld.MemberNo;
                    if (!string.IsNullOrEmpty(member.Email))
                    {
                        transb.Contact = member.Email;
                    }
                    else
                    {
                        transb.Contact = member.PhoneNo;
                    }


                }

                //tra

                transb.ContributionDate = ld.ContrDate ?? DateTime.ParseExact(trans.TransDate.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);// trans.TransDate;//DateTime.ParseExact(contribDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                transb.DepositedDate = ld.DepositedDate ?? DateTime.ParseExact(trans.TransDate.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);// trans.TransDate; //DateTime.ParseExact(ld.DateDeposited, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                transb.PaymentMode = member?.PhoneNo ?? member?.MobileNo;
                transb.TransactionType = "DEPOSIT";
                transb.Status = trans.Status;

                transb.RunE = 102;
                _db.Add(transb);
                _db.Add(trans);
                await _db.SaveChangesAsync();
                var sima = new ApiSimulate();
                var refa = sima.BASE_URL + "/api/appcheck/reconcile?companycode=" + ld.CompanyCode;


                _ = Task.Run(() =>
                {
                    try
                    {
                        //SendAfterDelay(refa);
                        _ = SendAfterDelayAsync(refa, "");
                    }
                    catch (Exception ex)
                    {
                        // Crucial: Catch exceptions here, otherwise an unhandled 
                        // exception on a background task could crash the process.
                        Console.WriteLine($"Background task error: {ex.Message}");
                    }
                });

                return new
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "Transactions added successfully, Process to save.",
                    // Data = null
                };
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                // Log.Error(ex, "Error adding journals");

                return new
                {
                    Success = false,
                    StatusCode = 204,
                    Message = "Ensure all required fields are properly filled and try again.",
                    // Data = null
                };
            }
        }

        public async Task SendAfterDelayAsync(string url, string res)
        {
            try
            {
                await Task.Delay(2000); // wait 1.2 seconds

                using var httpClient = new HttpClient();
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                await httpClient.PostAsync(url + "&failed=" + res, content);
            }
            catch (Exception ex)
            {
                // log ex
            }
        }

        public async Task<ContributionResponseDTO> AddContributionAsync(ContributionDTO contributionDto, bool prompt = false)
        {
            _logger.LogInformation($"Starting contribution addition for member: {contributionDto.MemberNo}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate member exists
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == contributionDto.MemberNo &&
                                             m.CompanyCode == contributionDto.CompanyCode);

                if (member == null)
                {
                    throw new ValidationException($"Member {contributionDto.MemberNo} not found");
                }

                // Validate share type exists
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.SharesCode == contributionDto.SharesCode &&
                                              st.CompanyCode == contributionDto.CompanyCode);

                if (shareType == null)
                {
                    throw new ValidationException($"Share type {contributionDto.SharesCode} not found");
                }

                // Determine contribution type
                string contributionCategory = DetermineContributionType(shareType, contributionDto);

                // ============================================================
                // CHECK IF THIS IS A MAJOR SHARE TYPE
                // ============================================================
                bool isMajorShareType = contributionCategory != "UNKNOWN";

                _logger.LogInformation($"Contribution Category: {contributionCategory}, IsMajor: {isMajorShareType}");

                // Get existing total for this member + sharetype combination
                decimal existingTotal = await GetExistingContributionTotalAsync(
                    contributionDto.MemberNo,
                    contributionDto.SharesCode,
                    contributionCategory,
                    contributionDto.CompanyCode);

                // ============================================================
                // FIXED: Validate amount against share type minimum
                // Only enforce minimum if the member has NO contributions for this share type
                // Once they have reached the minimum, allow any amount
                // ============================================================
                if (existingTotal < shareType.MinAmount && contributionDto.Amount < shareType.MinAmount)
                {
                    // This is the first contribution or they haven't reached minimum yet
                    // They must contribute at least the minimum amount
                    throw new ValidationException(
                        $"Amount cannot be less than minimum of {shareType.MinAmount:C}. " +
                        $"Current total: {existingTotal:C}. You need to contribute at least {shareType.MinAmount - existingTotal:C} more to reach the minimum."
                    );
                }

                // If they already have >= minimum, allow any positive amount
                // (No minimum validation needed)

                decimal newTotal = existingTotal + contributionDto.Amount;

                // Check maximum contribution limit (using cumulative total)
                if (shareType.MaxAmount.HasValue && newTotal > shareType.MaxAmount.Value)
                {
                    decimal remainingAllowed = shareType.MaxAmount.Value - existingTotal;
                    if (remainingAllowed <= 0)
                    {
                        throw new ValidationException(
                            $"Maximum {shareType.SharesType} limit of {shareType.MaxAmount.Value:C} has already been reached. " +
                            $"Current total: {existingTotal:C}. No further contributions allowed.");
                    }
                    else
                    {
                        throw new ValidationException(
                            $"Amount {contributionDto.Amount:C} exceeds remaining limit for {shareType.SharesType}. " +
                            $"Current total: {existingTotal:C}, Maximum: {shareType.MaxAmount.Value:C}, " +
                            $"Remaining allowed: {remainingAllowed:C}. Please reduce the amount.");
                    }
                }

                // ============================================================
                // STEP: VALIDATE CONTRIBUTION PREREQUISITES ( Share Capital -> Deposit)
                // ============================================================
                await ValidateContributionPrerequisitesAsync(contributionDto.MemberNo, contributionDto.SharesCode, contributionDto.CompanyCode);

                // Validate ContrDate (Contribution Date) - can be backdated, now, or future
                DateTime contributionDate = contributionDto.TransactionDate;

                // 2. DepositedDate (Actual deposit date) - can be backdated or today, NOT future
                DateTime depositedDate;
                if (contributionDto.DepositedDate.HasValue)
                {
                    depositedDate = contributionDto.DepositedDate.Value.Date;

                    // Validate - cannot be future date
                    if (depositedDate > DateTime.Now.Date)
                    {
                        throw new ValidationException("Deposit date cannot be in the future.");
                    }
                }
                else
                {
                    depositedDate = DateTime.Now.Date;
                }

                // ReceiptDate is the same as DepositedDate (system generated)
                DateTime receiptDate = depositedDate;

                // Check maximum contribution limit (using cumulative total)
                if (shareType.MaxAmount.HasValue && newTotal > shareType.MaxAmount.Value)
                {
                    decimal remainingAllowed = shareType.MaxAmount.Value - existingTotal;
                    if (remainingAllowed <= 0)
                    {
                        throw new ValidationException(
                            $"Maximum {shareType.SharesType} limit of {shareType.MaxAmount.Value:C} has already been reached. " +
                            $"Current total: {existingTotal:C}. No further contributions allowed.");
                    }
                    else
                    {
                        throw new ValidationException(
                            $"Amount {contributionDto.Amount:C} exceeds remaining limit for {shareType.SharesType}. " +
                            $"Current total: {existingTotal:C}, Maximum: {shareType.MaxAmount.Value:C}, " +
                            $"Remaining allowed: {remainingAllowed:C}. Please reduce the amount.");
                    }
                }

                // Get Sacco parameters
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == contributionDto.CompanyCode);

                // Generate receipt number
                var receiptNo = contributionDto.ReceiptNo ?? GenerateReceiptNumber(contributionDto.CompanyCode);
                var transactionNo = contributionDto.TransactionNo ?? GenerateTransactionNumber(contributionDto.CompanyCode);

                // Create Contrib record (main transaction record)
                var contrib = new Contrib
                {
                    MemberNo = contributionDto.MemberNo,
                    ContrDate = contributionDate,  // Can be backdated, now, or future
                    Amount = contributionDto.Amount,
                    CompanyCode = contributionDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    Remarks = contributionDto.Remarks,
                    AuditId = contributionDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    Sharescode = contributionDto.SharesCode,
                    TransactionNo = transactionNo,
                    Posted = "Y",
                    Locked = "N",
                    StaffNo = null,
                    DepositedDate = depositedDate,  // Actual deposit date (can be backdated)
                    ReceiptDate = receiptDate,       // Same as DepositedDate (system generated)
                    RefNo = contributionDto.ReferenceNo,
                    ShareBal = 0,
                    TransBy = contributionDto.CreatedBy,
                    ChequeNo = contributionDto.PaymentMethod == "CHEQUE" ? contributionDto.ReferenceNo : null,
                    TransDate = contributionDto.TransactionDate,
                    SharesAcc = shareType.SharesAcc,
                    ContraAcc = shareType.ContraAcc,
                    CashBookdate = DateTime.Now,
                    Dregard = 0,
                    Offs = 0,
                    ApiKey = null,
                    UserName = contributionDto.CreatedBy,
                    Run = 0,
                    Run2 = 0,
                    MrCleared = "N",
                    Mrno = null,
                    Offset = false,
                    TransferDesc = null,
                    Schemecode = contributionDto.CompanyCode
                };

                // =============================================
                // STEP: CHECK/CREATE WALLET BEFORE SIGNING
                // =============================================
                var memberRecord = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == contributionDto.MemberNo);

                if (memberRecord == null)
                {
                    throw new Exception($"Member {contributionDto.MemberNo} not found");
                }

                var hasWallet = await _cryptoService.HasWalletAsync(memberRecord.MobileNo);
                if (!hasWallet)
                {
                    _logger.LogWarning($"Member {memberRecord.MemberNo} has no wallet. Creating now...");
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(memberRecord.Id, memberRecord.MemberNo, contributionDto.CompanyCode);
                    if (!walletResult.Success)
                    {
                        throw new Exception($"Cannot process transaction: {walletResult.Message}");
                    }
                }

                // =============================================
                // STEP: RUN FRAUD DETECTION
                // =============================================
                var fraudResult = await _cryptoService.AnalyzeTransactionAsync(
                    contributionDto.MemberNo,
                    contributionDto.Amount,
                    contributionCategory);

                if (fraudResult.ShouldBlock)
                {
                    throw new Exception($"Transaction blocked by fraud detection: {string.Join(", ", fraudResult.Flags)}");
                }


                // =============================================
                // STEP: SIGN THE TRANSACTION
                // =============================================
                var txDataForSigning = new
                {
                    MemberNo = contrib.MemberNo,
                    Amount = contrib.Amount,
                    TransactionDate = contrib.ContrDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                    SharesCode = contrib.Sharescode,
                    ReceiptNo = contrib.ReceiptNo,
                    TransactionNo = contrib.TransactionNo,
                    CompanyCode = contrib.CompanyCode,
                    ContributionCategory = contributionCategory
                };

                // Generate canonical data BEFORE signing - STORE IT
                var canonicalData = _cryptoService.GetCanonicalData(txDataForSigning);
                contrib.CanonicalData = canonicalData;  // ← THIS IS CRITICAL

                // Sign the canonical data
                var dataBytes = Encoding.UTF8.GetBytes(canonicalData);

                // Get member's wallet
                var memberWallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecord.Id);

                if (memberWallet == null)
                {
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(
                        memberRecord.Id,
                        memberRecord.MemberNo,
                        contributionDto.CompanyCode);

                    if (!walletResult.Success)
                    {
                        throw new Exception($"Failed to create wallet: {walletResult.Message}");
                    }
                    memberWallet = walletResult.Wallet;
                }

                // Decrypt private key
                var privateKeyBase64 = EncryptionHelper.Decrypt(memberWallet.PrivateKeyEncrypted);
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

                // Sign using ECDSA P-256
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

                var signatureBytes = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
                var signature = Convert.ToBase64String(signatureBytes);

                // Generate transaction hash
                using var sha = SHA256.Create();
                var hashBytes = sha.ComputeHash(dataBytes);
                var transactionHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                // Get next sequence number
                var lastContrib = await _context.Contribs
                    .Where(c => c.MemberNo == contributionDto.MemberNo)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                var nonce = (lastContrib?.TransactionSequence ?? 0) + 1;

                // Attach signature to contrib
                contrib.TransactionSignature = signature;
                contrib.TransactionHash = transactionHash;
                contrib.TransactionSequence = nonce;
                contrib.IsSignatureVerified = false;

                // =============================================
                // CHAIN LINKING
                // =============================================
                if (lastContrib != null)
                {
                    contrib.PreviousTransactionHash = lastContrib.TransactionHash;
                }
                else
                {
                    // FIRST TRANSACTION - Use WALLET ADDRESS as genesis anchor
                    if (memberWallet != null && !string.IsNullOrEmpty(memberWallet.Address))
                    {
                        contrib.PreviousTransactionHash = memberWallet.Address;
                        _logger.LogInformation($"✅ FIRST TRANSACTION: Member {contributionDto.MemberNo}");
                        _logger.LogInformation($"   Wallet Address (Genesis Anchor): {memberWallet.Address}");
                    }
                }


                // =============================================
                // CHAIN LINKING - CRITICAL FIX
                // =============================================
                if (lastContrib != null)
                {
                    // Not first transaction - link to previous transaction hash
                    contrib.PreviousTransactionHash = lastContrib.TransactionHash;
                    _logger.LogDebug($"Member {contributionDto.MemberNo} - Linked to previous transaction: {lastContrib.TransactionHash?.Substring(0, 16)}...");
                }
                else
                {
                    // FIRST TRANSACTION - Use the WALLET ADDRESS as genesis anchor
                    // This must match what verification expects!
                    if (memberWallet != null && !string.IsNullOrEmpty(memberWallet.Address))
                    {
                        // Store the RAW wallet address directly
                        contrib.PreviousTransactionHash = memberWallet.Address;

                        _logger.LogInformation($"✅ FIRST TRANSACTION: Member {contributionDto.MemberNo}");
                        _logger.LogInformation($"   Wallet Address (Genesis Anchor): {memberWallet.Address}");
                    }
                    else
                    {
                        // Fallback - should not happen if wallet was created
                        var genesisSource = $"{memberRecord.MemberNo}|{memberRecord.Idno}|{memberRecord.ApplicDate?.ToString("o") ?? DateTime.UtcNow.ToString("o")}";
                        var genesisHash = _cryptoService.ComputeHash(genesisSource);
                        contrib.PreviousTransactionHash = genesisHash;
                        _logger.LogWarning($"Member {contributionDto.MemberNo} - No wallet found, using deterministic genesis hash: {genesisHash}");
                    }
                }

                // Add fraud warning to remarks if suspicious
                if (fraudResult.IsSuspicious)
                {
                    contrib.Remarks = $"{contrib.Remarks}";
                }

                var pending_trans = false;
                if (prompt == true)
                {
                    var res = await CreateTransactionDeposit(contrib, contrib.CompanyCode, contrib.AuditId, contrib.ReceiptNo, null);
                    if (res != null && res.Success == true)
                    {
                        pending_trans = true;
                    }
                    else if (res != null && res.Success == false)
                    {
                        throw new Exception($"Could not process prompt for  {contributionDto.MemberNo}. Try again.");
                    }
                }

                _context.Contribs.Add(contrib);
                await _context.SaveChangesAsync();

                ContribShare? contribShare = null;

                // ============================================================
                // CREATE CONTRIB SHARE ROW ONLY FOR MAJOR SHARE TYPES
                // ============================================================
                if (isMajorShareType)
                {
                    // For UNKNOWN types, we skip this entirely
                    contribShare = new ContribShare
                    {
                        LocalId = contrib.Id,
                        MemberNo = contributionDto.MemberNo,
                        CompanyCode = contributionDto.CompanyCode,
                        ReceiptNo = receiptNo,
                        Sharescode = contributionDto.SharesCode,
                        Remarks = contributionDto.Remarks,
                        AuditId = contributionDto.CreatedBy,
                        AuditTime = DateTime.Now,
                        AuditDateTime = DateTime.Now,
                        TransactionNo = contrib.TransactionNo,
                        ContrDate = contributionDate,
                        LoanNo = null,
                        Isharecapital = shareType.Issharecapital,
                        DepositedDate = depositedDate,
                        ReceiptDate = receiptDate,
                        // Only set the relevant amount based on category
                        ShareCapitalAmount = contributionCategory == "SHARE_CAPITAL" ? contributionDto.Amount : 0,
                        DepositsAmount = contributionCategory == "DEPOSIT" ? contributionDto.Amount : 0,
                        PassBookAmount = contributionCategory == "PASSBOOK" ? contributionDto.Amount : 0,
                        Donor = contributionCategory == "DONOR" ? contributionDto.Amount : 0,
                        LoanAmount = contributionCategory == "LOAN_REPAYMENT" ? contributionDto.Amount : 0,
                        RegFeeAmount = contributionCategory == "REGISTRATION_FEE" ? contributionDto.Amount : 0
                    };

                    _context.ContribShares.Add(contribShare);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ ContribShare created for: {contributionCategory}, Amount: {contributionDto.Amount:C}");
                }
                else
                {
                    // ============================================================
                    // MINOR SHARE TYPE - SKIP ContribShares
                    // ============================================================
                    _logger.LogInformation($"⏭️ SKIPPING ContribShares for: {shareType.SharesType} ({shareType.SharesCode}) - Category: UNKNOWN");
                }

                // Update share balance if needed (for SHARE_CAPITAL or PASSBOOK types)
                if (contributionCategory == "SHARE_CAPITAL" ||
                    (contributionCategory == "PASSBOOK" && shareType.Issharecapital == true))
                {
                    await UpdateShareBalanceAsync(contributionDto.MemberNo,
                        contributionDto.SharesCode,
                        contributionDto.Amount,
                        contributionDto.CompanyCode);
                }

                // =============================================
                // STEP: UPDATE WALLET BALANCE AFTER SUCCESSFUL CONTRIBUTION
                // =============================================
                // Get or create wallet for the member
                var wallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecord.Id);
                if (wallet == null)
                {
                    // Create wallet if it doesn't exist
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(memberRecord.Id, memberRecord.MemberNo, contributionDto.CompanyCode);
                    if (!walletResult.Success)
                    {
                        _logger.LogWarning($"Failed to create wallet for balance update: {walletResult.Message}");
                    }
                    else
                    {
                        wallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecord.Id);
                    }
                }

                // Update wallet balances based on contribution category
                if (wallet != null)
                {
                    // Update last activity
                    wallet.LastActivity = DateTime.UtcNow;

                    // Update specific balances based on contribution type
                    switch (contributionCategory)
                    {
                        case "SHARE_CAPITAL":
                            // Update Capital Balance (for shares)
                            wallet.CapitalBalance += contributionDto.Amount;
                            wallet.Balance += contributionDto.Amount; // Also update total balance
                            _logger.LogInformation($"Updated CapitalBalance for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}, New: {wallet.CapitalBalance:C}");
                            break;

                        case "DEPOSIT":
                            // Update Deposit Balance (savings/deposits)
                            wallet.DepositBalance += contributionDto.Amount;
                            wallet.Balance += contributionDto.Amount; // Also update total balance
                            _logger.LogInformation($"Updated DepositBalance for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}, New: {wallet.DepositBalance:C}");
                            break;

                        case "PASSBOOK":
                            // For PASSBOOK, update CapitalBalance if Issharecapital=1, otherwise Balance
                            if (shareType.Issharecapital == true)
                            {
                                wallet.CapitalBalance += contributionDto.Amount;
                                _logger.LogInformation($"Updated CapitalBalance (PASSBOOK) for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}");
                            }
                            else
                            {
                                wallet.Balance += contributionDto.Amount;
                                _logger.LogInformation($"Updated Balance (PASSBOOK) for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}");
                            }
                            break;

                        case "LOAN_REPAYMENT":
                            // For loan repayment, update Balance (could be separate loan tracking)
                            wallet.Balance += contributionDto.Amount;
                            _logger.LogInformation($"Updated Balance (LOAN_REPAYMENT) for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}");
                            break;

                        case "DONOR":
                            // For donor contributions, update Balance
                            wallet.Balance += contributionDto.Amount;
                            _logger.LogInformation($"Updated Balance (DONOR) for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}");
                            break;

                        case "REGISTRATION_FEE":
                            // Registration fees don't add to balance (they're fees)
                            _logger.LogInformation($"Registration fee of {contributionDto.Amount:C} collected from member {memberRecord.MemberNo} (no balance update)");
                            break;

                        default:
                            // Default - update general balance
                            wallet.Balance += contributionDto.Amount;
                            _logger.LogInformation($"Updated Balance for member {memberRecord.MemberNo}: +{contributionDto.Amount:C}, New: {wallet.Balance:C}");
                            break;
                    }

                    // Save wallet changes
                    await _context.SaveChangesAsync();
                }


                // ============================================================
                // GET GL ACCOUNTS
                // ============================================================
                string drAcc = null; // Debit account (Where money comes FROM - Bank/Cash)
                string crAcc = null; // Credit account (Where money goes TO - Share Type account)

                // Credit account from Sharetype
                if (string.IsNullOrEmpty(shareType.SharesAcc))
                {
                    throw new Exception($"Share type '{shareType.SharesCode}' does not have a GL Account configured.");
                }
                crAcc = shareType.SharesAcc;

                // Debit account based on payment method
                string paymentMethod = contributionDto.PaymentMethod?.ToUpper() ?? "CASH";

                switch (paymentMethod)
                {
                    case "BANK TRANSFER":
                    case "CHEQUE":
                        if (!string.IsNullOrEmpty(contributionDto.ReferenceNo))
                        {
                            var bank = await _context.Banks
                                .FirstOrDefaultAsync(b => b.CompanyCode == contributionDto.CompanyCode &&
                                                          b.IsActive == true &&
                                                          (b.BankCode == contributionDto.ReferenceNo ||
                                                           b.BankName.Contains(contributionDto.ReferenceNo) ||
                                                           b.AccountNumber == contributionDto.ReferenceNo));

                            if (bank != null && !string.IsNullOrEmpty(bank.GlAccountNo))
                            {
                                drAcc = bank.GlAccountNo;
                                break;
                            }
                        }

                        var defaultBank = await _context.Banks
                            .FirstOrDefaultAsync(b => b.CompanyCode == contributionDto.CompanyCode &&
                                                      b.IsActive == true &&
                                                      !string.IsNullOrEmpty(b.GlAccountNo));

                        if (defaultBank != null)
                        {
                            drAcc = defaultBank.GlAccountNo;
                            break;
                        }
                        goto case "CASH";

                    case "MOBILE MONEY":
                    case "CASH":
                    default:
                        if (sacco != null && !string.IsNullOrEmpty(sacco.RetainedEarnings))
                        {
                            drAcc = sacco.RetainedEarnings;
                        }
                        else
                        {
                            var cashAccount = await _context.GlSetup
                                .FirstOrDefaultAsync(g => g.CompanyCode == contributionDto.CompanyCode &&
                                                          g.Status == true &&
                                                          (g.Type == "ASSET" || g.Type == "Asset") &&
                                                          (g.Glaccname != null &&
                                                           (g.Glaccname.ToLower().Contains("cash") ||
                                                            g.Glaccname.ToLower().Contains("bank"))));

                            if (cashAccount != null)
                            {
                                drAcc = cashAccount.AccNo;
                            }
                        }
                        break;
                }

                // Validate GL accounts
                if (string.IsNullOrEmpty(drAcc))
                {
                    var suspenseAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.CompanyCode == contributionDto.CompanyCode &&
                                                  g.IsSuspense == true);
                    if (suspenseAccount != null)
                    {
                        drAcc = suspenseAccount.AccNo;
                    }
                    else
                    {
                    }
                }

                // Create GL Transaction
                var glTransaction = new Gltransaction
                {
                    TransDate = contributionDate,  // Use contribution date for GL transaction
                    Amount = contributionDto.Amount,
                    DrAccNo = drAcc,
                    CrAccNo = crAcc,
                    Temp = "N",
                    DocumentNo = receiptNo,
                    Source = contributionDto.MemberNo,
                    CompanyCode = contributionDto.CompanyCode,
                    TransDescript = $"{contributionCategory} - {contributionDto.MemberNo}",
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    AuditId = contributionDto.CreatedBy,
                    Cash = paymentMethod == "CASH" ? 1 : 0,
                    DocPosted = 1,
                    ChequeNo = paymentMethod == "CHEQUE" ? contributionDto.ReferenceNo : null,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = contrib.TransactionNo,
                    Module = "SHARES",
                    ReconId = 0
                };

                _context.Gltransactions.Add(glTransaction);
                await _context.SaveChangesAsync();

                // ============================================================
                // CREATE BLOCK AND BLOCKCHAIN TRANSACTION
                // ============================================================
                string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = await GetLastBlockHashAsync(),
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    TransactionType = "CONTRIBUTION",
                    MemberNo = contributionDto.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    ShareType = shareType.SharesType,
                    ShareTypeCode = shareType.SharesCode,
                    ContributionCategory = contributionCategory,
                    Amount = contributionDto.Amount,
                    ReceiptNo = receiptNo,
                    ContributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    DepositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    ReceiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    PaymentMethod = paymentMethod,
                    ReferenceNo = contributionDto.ReferenceNo,
                    Remarks = contributionDto.Remarks,
                    CompanyCode = contributionDto.CompanyCode,
                    CreatedBy = contributionDto.CreatedBy,
                    DrAccount = drAcc,
                    CrAccount = crAcc,
                    BlockHash = blockHash,
                    CumulativeTotalAfter = existingTotal + contributionDto.Amount,
                    MaxLimit = shareType.MaxAmount,
                    RemainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + contributionDto.Amount) : (decimal?)null
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "CONTRIBUTION",
                    MemberNo = contributionDto.MemberNo,
                    CompanyCode = contributionDto.CompanyCode,
                    Amount = contributionDto.Amount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = receiptNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update blockchain references
                contrib.BlockchainTxId = blockchainTx.TransactionId;
                if (contribShare != null)
                {
                    contribShare.BlockchainTxId = blockchainTx.TransactionId;
                }
                glTransaction.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation($"Contribution {receiptNo} added successfully for member {contributionDto.MemberNo}");

                // ============================================================
                // SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    amount = contributionDto.Amount,
                    memberName = $"{member.Surname} {member.OtherNames}",
                    memberNumber = contributionDto.MemberNo,
                    shareType = shareType.SharesType,
                    shareTypeCode = contributionDto.SharesCode,
                    contributionCategory = contributionCategory,
                    receiptNumber = receiptNo,
                    contributionDate = contributionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    depositedDate = depositedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    receiptDate = receiptDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    paymentMethod = paymentMethod,
                    referenceNo = contributionDto.ReferenceNo ?? "",
                    remarks = contributionDto.Remarks ?? "",
                    cumulativeTotalAfter = existingTotal + contributionDto.Amount,
                    maxLimit = shareType.MaxAmount,
                    remainingLimit = shareType.MaxAmount.HasValue ? shareType.MaxAmount.Value - (existingTotal + contributionDto.Amount) : (decimal?)null,
                    drAccount = drAcc,
                    crAccount = crAcc,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var contribForAudit = new
                {
                    contrib.Id,
                    contrib.MemberNo,
                    contrib.ContrDate,
                    contrib.DepositedDate,
                    contrib.ReceiptDate,
                    contrib.Amount,
                    contrib.ReceiptNo,
                    contrib.Remarks,
                    contrib.Sharescode,
                    contrib.TransactionNo,
                    contrib.RefNo,
                    contrib.ChequeNo,
                    contrib.TransDate,
                    contrib.SharesAcc,
                    contrib.ContraAcc,
                    contrib.UserName,
                    contrib.CompanyCode,
                    BlockchainTxId = blockchainTx.TransactionId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = contributionDto.CreatedBy
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: contribForAudit,
                    tableName: "Contribs",
                    recordId: receiptNo,
                    userId: contributionDto.CreatedBy,
                    userName: contributionDto.CreatedBy,
                    companyCode: contributionDto.CompanyCode,
                    module: "Contributions",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // Get cumulative share balance for response
                var cumulativeShareBalance = await GetMemberShareBalanceAsync(contributionDto.MemberNo);

                return new ContributionResponseDTO
                {
                    Id = contrib.Id,
                    MemberNo = contributionDto.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    TransactionDate = contributionDate,
                    DepositedDate = depositedDate,
                    ReceiptDate = receiptDate,
                    SharesCode = contributionDto.SharesCode,
                    ShareTypeName = shareType.SharesType ?? shareType.SharesCode,
                    Amount = contributionDto.Amount,
                    ShareCapitalAmount = contributionCategory == "SHARE_CAPITAL" ? contributionDto.Amount : 0,
                    DepositsAmount = contributionCategory == "DEPOSIT" ? contributionDto.Amount : 0,
                    RegFeeAmount = contributionCategory == "REGISTRATION_FEE" ? contributionDto.Amount : 0,
                    Donor = contributionCategory == "DONOR" ? contributionDto.Amount : 0,
                    LoanAmount = contributionCategory == "LOAN_REPAYMENT" ? contributionDto.Amount : 0,
                    PassBookAmount = contributionCategory == "PASSBOOK" ? contributionDto.Amount : 0,
                    TotalSharesAfter = cumulativeShareBalance,
                    ReceiptNo = receiptNo,
                    Remarks = contributionDto.Remarks ?? string.Empty,
                    BlockchainTxId = contrib.BlockchainTxId ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    CreatedBy = contributionDto.CreatedBy,
                    CompanyCode = contributionDto.CompanyCode
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction rolled back due to error");

                if (ex is ValidationException)
                {
                    throw new Exception($"Validation error: {ex.Message}");
                }

                throw new Exception($"Error adding contribution: {ex.Message}");
            }
        }

        private async Task ValidateContributionPrerequisitesBulkAsync(string memberNo, string sharesCode, string companyCode)
        {
            // Get the share type to determine category
            var shareType = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == sharesCode && st.CompanyCode == companyCode);

            if (shareType == null)
            {
                throw new ValidationException($"Share type {sharesCode} not found");
            }

            // Determine the contribution category based on share type
            string category = DetermineContributionType(shareType, new ContributionDTO { SharesCode = sharesCode });

            // ============================================================
            // Get member totals for logging purposes only
            // ============================================================
            var memberTotals = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalPassBook = g.Sum(cs => cs.PassBookAmount ?? 0),
                    TotalDonor = g.Sum(cs => cs.Donor ?? 0),
                    TotalLoan = g.Sum(cs => cs.LoanAmount ?? 0)
                })
                .FirstOrDefaultAsync();

            decimal regFeePaid = memberTotals?.TotalRegFee ?? 0;
            decimal shareCapitalPaid = memberTotals?.TotalShareCapital ?? 0;
            decimal depositsPaid = memberTotals?.TotalDeposits ?? 0;

            // Get requirements from Sharetype
            decimal minShareCapital = shareType.MinAmount;
            decimal? maxShareCapital = shareType.MaxAmount;

            _logger.LogInformation($"=== Contribution Prerequisites Validation (Bulk) ===");
            _logger.LogInformation($"Member {memberNo} - Total RegFee: {regFeePaid:C}");
            _logger.LogInformation($"Member {memberNo} - Total ShareCapital: {shareCapitalPaid:C}");
            _logger.LogInformation($"Member {memberNo} - Total Deposits: {depositsPaid:C}");
            _logger.LogInformation($"ShareType {sharesCode} - Category: {category}, MinAmount: {minShareCapital:C}, MaxAmount: {maxShareCapital:C}");

            // ============================================================
            // HIERARCHY VALIDATION - NO BLOCKING, ONLY LOGGING
            // ============================================================
            switch (category)
            {
                case "REGISTRATION_FEE":
                    // Registration fee is always allowed
                    _logger.LogInformation($"Registration fee contribution allowed for member {memberNo}");
                    break;

                case "SHARE_CAPITAL":
                    // Share capital is always allowed (no prerequisites)
                    _logger.LogInformation($"Share Capital contribution allowed for member {memberNo}");
                    break;

                case "DEPOSIT":
                case "PASSBOOK":
                    // Deposit/PASSBOOK is always allowed - NO BLOCKING
                    // Only log the current status for informational purposes
                    if (shareCapitalPaid < minShareCapital)
                    {
                        _logger.LogInformation($"Deposit/PASSBOOK contribution allowed for member {memberNo} (ShareCapital: {shareCapitalPaid:C} is below minimum: {minShareCapital:C} - No blocking applied)");
                    }
                    else
                    {
                        _logger.LogInformation($"Deposit/PASSBOOK contribution allowed for member {memberNo} (ShareCapital: {shareCapitalPaid:C} >= Min: {minShareCapital:C})");
                    }
                    break;

                case "DONOR":
                case "LOAN_REPAYMENT":
                    // These are special categories - always allowed
                    _logger.LogInformation($"{category} contribution allowed for member {memberNo}");
                    break;

                default:
                    // SHARE_CAPITAL is the default - always allowed
                    _logger.LogInformation($"Default share capital contribution allowed for member {memberNo}");
                    break;
            }
        }

        private async Task UpdateShareBalanceAsync(string memberNo, string sharesCode, decimal amount, string companyCode)
        {
            // Find existing share record
            var existingShare = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo &&
                                         s.Sharescode == sharesCode &&
                                         s.CompanyCode == companyCode);

            if (existingShare != null)
            {
                // Update existing share
                existingShare.TotalShares = (existingShare.TotalShares ?? 0) + amount;
                existingShare.TransDate = DateTime.Now;
                existingShare.AuditTime = DateTime.Now;
                existingShare.AuditDateTime = DateTime.Now;

                _logger.LogDebug($"Updated share balance for {memberNo} - {sharesCode}: +{amount:C}, New Total: {existingShare.TotalShares:C}");
            }
            else
            {
                // Create new share record
                var newShare = new Share
                {
                    MemberNo = memberNo,
                    Sharescode = sharesCode,
                    TotalShares = amount,
                    Initshares = amount,
                    CompanyCode = companyCode,
                    TransDate = DateTime.Now,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    AuditId = "SYSTEM"
                };

                _context.Shares.Add(newShare);
                _logger.LogDebug($"Created new share record for {memberNo} - {sharesCode} with amount: {amount:C}");
            }
        }

        private async Task ValidateContributionPrerequisitesAsync(string memberNo, string sharesCode, string companyCode)
        {
            // Get the share type to determine category
            var shareType = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == sharesCode && st.CompanyCode == companyCode);

            if (shareType == null)
            {
                throw new ValidationException($"Share type {sharesCode} not found");
            }

            // Determine the contribution category based on share type
            string category = DetermineContributionType(shareType, new ContributionDTO { SharesCode = sharesCode });

            // ============================================================
            // FIX: Use SUM instead of FirstOrDefault to get TOTAL share capital
            // This gets the SUM of ALL share capital contributions for this member
            // ============================================================
            var memberTotals = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalPassBook = g.Sum(cs => cs.PassBookAmount ?? 0),
                    TotalDonor = g.Sum(cs => cs.Donor ?? 0),
                    TotalLoan = g.Sum(cs => cs.LoanAmount ?? 0)
                })
                .FirstOrDefaultAsync();

            decimal regFeePaid = memberTotals?.TotalRegFee ?? 0;
            decimal shareCapitalPaid = memberTotals?.TotalShareCapital ?? 0;
            decimal depositsPaid = memberTotals?.TotalDeposits ?? 0;

            // Get requirements from Sharetype
            decimal minShareCapital = shareType.MinAmount;
            decimal? maxShareCapital = shareType.MaxAmount;

            _logger.LogInformation($"=== Contribution Prerequisites Validation ===");
            _logger.LogInformation($"Member {memberNo} - Total RegFee: {regFeePaid:C}");
            _logger.LogInformation($"Member {memberNo} - Total ShareCapital: {shareCapitalPaid:C}");
            _logger.LogInformation($"Member {memberNo} - Total Deposits: {depositsPaid:C}");
            _logger.LogInformation($"ShareType {sharesCode} - Category: {category}, MinAmount: {minShareCapital:C}, MaxAmount: {maxShareCapital:C}");

            // ============================================================
            // HIERARCHY VALIDATION
            // ============================================================

            switch (category)
            {
                case "REGISTRATION_FEE":
                    // Registration fee is always allowed
                    _logger.LogInformation($"Registration fee contribution allowed for member {memberNo}");
                    break;

                case "SHARE_CAPITAL":
                    // Share capital is always allowed (no prerequisites)
                    _logger.LogInformation($"Share Capital contribution allowed for member {memberNo}");
                    break;

                case "DEPOSIT":
                case "PASSBOOK":  // PASSBOOK also requires share capital first
                                  // Check if share capital is paid (at least the minimum amount)
                    if (shareCapitalPaid < minShareCapital)
                    {
                        _logger.LogWarning($"DEPOSIT BLOCKED: Member {memberNo} has ShareCapital: {shareCapitalPaid:C}, Required: {minShareCapital:C}");

                        throw new ValidationException(
                            $"Minimum Share Capital of {minShareCapital:C} must be paid before you can make Deposits/Savings. " +
                            $"Current Share Capital: {shareCapitalPaid:C}. Please complete your share capital first."
                        );
                    }
                    _logger.LogInformation($"Deposit/PASSBOOK contribution allowed for member {memberNo} (ShareCapital: {shareCapitalPaid:C} >= Min: {minShareCapital:C})");
                    break;

                case "DONOR":
                case "LOAN_REPAYMENT":
                    // These are special categories - always allowed
                    _logger.LogInformation($"{category} contribution allowed for member {memberNo}");
                    break;

                default:
                    // SHARE_CAPITAL is the default - always allowed
                    _logger.LogInformation($"Default share capital contribution allowed for member {memberNo}");
                    break;
            }
        }

        public async Task<MemberShareTypeTotalsDTO> GetMemberShareTypeTotalsAsync(string memberNo, string companyCode)
        {
            // Get ALL contributions for this member and group by share type
            var memberContributions = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.Sharescode)
                .Select(g => new
                {
                    SharesCode = g.Key,
                    TotalShareCapitalAmount = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDepositsAmount = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalRegFeeAmount = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    TotalPassBookAmount = g.Sum(cs => cs.PassBookAmount ?? 0),
                    TotalDonor = g.Sum(cs => cs.Donor ?? 0),
                    TotalLoanAmount = g.Sum(cs => cs.LoanAmount ?? 0),
                    TransactionCount = g.Count()
                })
                .ToListAsync();

            // Get all share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .ToListAsync();

            var result = new MemberShareTypeTotalsDTO
            {
                MemberNo = memberNo,
                ShareTypeTotals = new List<ShareTypeTotalsDTO>()
            };

            foreach (var shareType in shareTypes)
            {
                // Find the contribution for this share type from the grouped results
                var contrib = memberContributions.FirstOrDefault(c => c.SharesCode == shareType.SharesCode);
                string category = DetermineContributionType(shareType, new ContributionDTO { SharesCode = shareType.SharesCode });

                decimal currentAmount = 0;
                switch (category)
                {
                    case "REGISTRATION_FEE":
                        currentAmount = contrib?.TotalRegFeeAmount ?? 0;
                        break;
                    case "SHARE_CAPITAL":
                        currentAmount = contrib?.TotalShareCapitalAmount ?? 0;
                        break;
                    case "DEPOSIT":
                        currentAmount = contrib?.TotalDepositsAmount ?? 0;
                        break;
                    case "PASSBOOK":
                        currentAmount = contrib?.TotalPassBookAmount ?? 0;
                        break;
                    case "DONOR":
                        currentAmount = contrib?.TotalDonor ?? 0;
                        break;
                    case "LOAN_REPAYMENT":
                        currentAmount = contrib?.TotalLoanAmount ?? 0;
                        break;
                    default:
                        currentAmount = contrib?.TotalShareCapitalAmount ?? 0;
                        break;
                }

                decimal maxAmount = shareType.MaxAmount ?? 0;
                decimal remainingAmount = maxAmount > 0 ? Math.Max(0, maxAmount - currentAmount) : 0;

                result.ShareTypeTotals.Add(new ShareTypeTotalsDTO
                {
                    SharesCode = shareType.SharesCode,
                    SharesType = shareType.SharesType ?? shareType.SharesCode,
                    Category = category,
                    CurrentAmount = currentAmount,
                    MinAmount = shareType.MinAmount,
                    MaxAmount = maxAmount,
                    RemainingAmount = remainingAmount,
                    IsFullyPaid = maxAmount > 0 && currentAmount >= maxAmount,
                    Priority = shareType.Priority,
                    IsMainShares = shareType.IsMainShares,
                    UsedToGuarantee = shareType.UsedToGuarantee,
                    UsedToOffset = shareType.UsedToOffset,
                    Withdrawable = shareType.Withdrawable,
                    TransactionCount = contrib?.TransactionCount ?? 0
                });
            }

            // Sort by priority
            result.ShareTypeTotals = result.ShareTypeTotals.OrderBy(s => s.Priority).ToList();

            return result;
        }

        public async Task<List<ContributionResponseDTO>> GetMemberContributionsAsync(string memberNo)
        {
            // ✅ FIX: Single query with includes
            var member = await _context.Members
                .Where(m => m.MemberNo == memberNo)
                .Select(m => new { m.Surname, m.OtherNames })
                .FirstOrDefaultAsync();

            var memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : memberNo;

            var contributions = await _context.Contribs
                .Where(c => c.MemberNo == memberNo)
                .OrderByDescending(c => c.ContrDate)
                .Take(100)
                .Select(c => new ContributionResponseDTO
                {
                    Id = c.Id,
                    MemberNo = c.MemberNo ?? string.Empty,
                    MemberName = memberName,
                    TransactionDate = c.ContrDate ?? DateTime.MinValue,
                    SharesCode = c.Sharescode ?? string.Empty,
                    ShareTypeName = c.SharescodeNavigation != null ? c.SharescodeNavigation.SharesType : c.Sharescode ?? "Unknown",
                    Amount = c.Amount ?? 0,
                    ReceiptNo = c.ReceiptNo ?? string.Empty,
                    Remarks = c.Remarks ?? string.Empty,
                    BlockchainTxId = c.BlockchainTxId ?? string.Empty,
                    CreatedAt = c.AuditTime,
                    CreatedBy = c.AuditId ?? string.Empty,
                    CompanyCode = c.CompanyCode ?? string.Empty
                })
                .ToListAsync();

            return contributions;
        }

        public async Task<List<ShareTypeDTO>> GetShareTypesAsync(string companyCode)
        {
            return await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .Select(st => new ShareTypeDTO
                {
                    SharesCode = st.SharesCode,
                    SharesType = st.SharesType ?? st.SharesCode,
                    SharesAcc = st.SharesAcc,
                    IsMainShares = st.IsMainShares,
                    UsedToGuarantee = st.UsedToGuarantee,
                    Withdrawable = st.Withdrawable,
                    MinAmount = st.MinAmount,
                    MaxAmount = st.MaxAmount ?? 0,
                    CompanyCode = st.CompanyCode ?? companyCode
                })
                .ToListAsync();
        }

        public async Task<MemberContributionHistoryDTO> GetMemberContributionHistoryAsync(string memberNo)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            if (member == null)
            {
                throw new ValidationException($"Member {memberNo} not found");
            }

            var contributions = await GetMemberContributionsAsync(memberNo);
            var shareBalance = await GetMemberShareBalanceAsync(memberNo);

            return new MemberContributionHistoryDTO
            {
                MemberNo = memberNo,
                MemberName = $"{member.Surname} {member.OtherNames}",
                Contributions = contributions.Select(c => new ContributionDetailDTO
                {
                    TransactionDate = c.TransactionDate,
                    SharesCode = c.SharesCode,
                    ShareTypeName = c.ShareTypeName,
                    Amount = c.Amount,
                    ReceiptNo = c.ReceiptNo,
                    Remarks = c.Remarks,
                    BlockchainTxId = c.BlockchainTxId,
                    CreatedBy = c.CreatedBy
                }).ToList(),
                TotalContributions = contributions.Sum(c => c.Amount),
                CurrentShareBalance = shareBalance,
                CompanyCode = member.CompanyCode ?? string.Empty
            };
        }

        public async Task<List<ContributionResponseDTO>> SearchContributionsAsync(DateTime? fromDate, DateTime? toDate, string? memberNo = null, string? shareType = null)
        {
            var query = _context.Contribs.AsQueryable();

            // Apply filters
            if (fromDate.HasValue)
            {
                query = query.Where(c => c.ContrDate >= fromDate);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.ContrDate <= toDate);
            }

            if (!string.IsNullOrEmpty(memberNo))
            {
                query = query.Where(c => c.MemberNo != null && c.MemberNo.Contains(memberNo));
            }

            if (!string.IsNullOrEmpty(shareType))
            {
                query = query.Where(c => c.Sharescode == shareType);
            }

            // ✅ FIX: Use LEFT JOIN with GroupJoin (works with EF Core)
            var result = await (
                from c in query
                join m in _context.Members
                    on new { c.MemberNo, c.CompanyCode }
                    equals new { MemberNo = m.MemberNo, CompanyCode = m.CompanyCode } into memberJoin
                from m in memberJoin.DefaultIfEmpty()
                join s in _context.Sharetypes
                    on new { SharesCode = c.Sharescode.Trim(), CompanyCode = c.CompanyCode.Trim() }
                    equals new { SharesCode = s.SharesCode.Trim(), CompanyCode = s.CompanyCode.Trim() } into shareJoin
                from s in shareJoin.DefaultIfEmpty()
                orderby c.ContrDate descending
                select new ContributionResponseDTO
                {
                    Id = c.Id,
                    MemberNo = c.MemberNo ?? string.Empty,
                    MemberName = (m != null ? (m.Surname + " " + m.OtherNames).Trim() : c.MemberNo ?? "Unknown"),
                    TransactionDate = c.ContrDate ?? DateTime.MinValue,
                    SharesCode = c.Sharescode ?? string.Empty,
                    ShareTypeName = (s != null ? s.SharesType : c.Sharescode) ?? "Unknown",
                    Amount = c.Amount ?? 0,
                    ReceiptNo = c.ReceiptNo ?? string.Empty,
                    Remarks = c.Remarks ?? string.Empty,
                    BlockchainTxId = c.BlockchainTxId ?? string.Empty,
                    CreatedAt = c.AuditTime,
                    CreatedBy = c.AuditId ?? string.Empty,
                    CompanyCode = c.CompanyCode ?? string.Empty
                })
                .Take(200)
                .ToListAsync();

            return result;
        }

        public async Task<ContributionDeleteResultDTO> ReverseContributionAsync(int contributionId, string deleteReason, string deletedBy)
        {
            _logger.LogInformation($"Starting contribution reversal for ID: {contributionId}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Find the original contribution
                var originalContribution = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .FirstOrDefaultAsync(c => c.Id == contributionId && c.CompanyCode == currentCompanyCode);

                if (originalContribution == null)
                {
                    throw new ValidationException($"Contribution with ID {contributionId} not found");
                }

                // Check if already reversed
                var existingReversal = await _context.Contribs
                    .FirstOrDefaultAsync(c => c.ReceiptNo == $"{originalContribution.ReceiptNo}-REVERSAL"
                                           && c.CompanyCode == currentCompanyCode);

                if (existingReversal != null)
                {
                    throw new ValidationException($"This contribution has already been reversed. Reversal Receipt: {existingReversal.ReceiptNo}");
                }

                var receiptNo = originalContribution.ReceiptNo ?? string.Empty;
                var memberNo = originalContribution.MemberNo;
                var originalAmount = originalContribution.Amount ?? 0;
                var sharesCode = originalContribution.Sharescode;

                _logger.LogInformation($"Reversing contribution: Receipt {receiptNo}, Member {memberNo}, Amount {originalAmount}");

                // ============================================================
                // STEP 1: Create REVERSAL Contrib record (negative amount)
                // ============================================================
                var reversalContribution = new Contrib
                {
                    MemberNo = memberNo,
                    ContrDate = DateTime.Now,  // Reversal date is today
                    DepositedDate = DateTime.Now,
                    ReceiptDate = DateTime.Now,
                    Amount = -originalAmount,  // ← NEGATIVE amount to reverse
                    CompanyCode = currentCompanyCode,
                    ReceiptNo = $"{receiptNo}-REVERSAL",  // ← New receipt number
                    Remarks = $"REVERSAL: {deleteReason}. Original: {originalContribution.Remarks}",
                    AuditId = deletedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    Sharescode = sharesCode,
                    TransactionNo = $"{originalContribution.TransactionNo}-REV",
                    Posted = "Y",
                    Locked = "Y",  // Lock reversal entries
                    StaffNo = originalContribution.StaffNo,
                    RefNo = originalContribution.RefNo,
                    ShareBal = 0,
                    TransBy = deletedBy,
                    ChequeNo = originalContribution.ChequeNo,
                    TransDate = DateTime.Now,
                    SharesAcc = originalContribution.SharesAcc,
                    ContraAcc = originalContribution.ContraAcc,
                    CashBookdate = DateTime.Now,
                    Dregard = 0,
                    Offs = 0,
                    UserName = deletedBy,
                    Run = 0,
                    Run2 = 0,
                    MrCleared = "N",
                    Offset = false,
                    Schemecode = currentCompanyCode,
                    Status = "REVERSED"  // Mark as reversal
                };

                _context.Contribs.Add(reversalContribution);
                await _context.SaveChangesAsync();

                // ============================================================
                // STEP 2: Create REVERSAL ContribShare record (negative amount)
                // ============================================================
                // Get the original ContribShare to know which column to reverse
                var originalContribShare = await _context.ContribShares
                    .FirstOrDefaultAsync(cs => cs.TransactionNo == originalContribution.TransactionNo);

                var reversalContribShare = new ContribShare
                {
                    LocalId = reversalContribution.Id,
                    MemberNo = memberNo,
                    CompanyCode = currentCompanyCode,
                    ReceiptNo = $"{receiptNo}-REVERSAL",
                    Sharescode = sharesCode,
                    Remarks = $"REVERSAL: {deleteReason}",
                    AuditId = deletedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    TransactionNo = reversalContribution.TransactionNo,
                    ContrDate = DateTime.Now,
                    DepositedDate = DateTime.Now,
                    ReceiptDate = DateTime.Now,
                    // Apply negative amount to the SAME column type
                    ShareCapitalAmount = originalContribShare?.ShareCapitalAmount > 0 ? -originalAmount : 0,
                    DepositsAmount = originalContribShare?.DepositsAmount > 0 ? -originalAmount : 0,
                    PassBookAmount = originalContribShare?.PassBookAmount > 0 ? -originalAmount : 0,
                    Donor = originalContribShare?.Donor > 0 ? -originalAmount : 0,
                    LoanAmount = originalContribShare?.LoanAmount > 0 ? -originalAmount : 0,
                    RegFeeAmount = originalContribShare?.RegFeeAmount > 0 ? -originalAmount : 0
                };

                _context.ContribShares.Add(reversalContribShare);
                await _context.SaveChangesAsync();

                // ============================================================
                // STEP 3: Reverse share balance (subtract instead of add)
                // ============================================================
                await ReverseShareBalanceAsync(memberNo, sharesCode, originalAmount, currentCompanyCode);

                // ============================================================
                // STEP 4: Create REVERSAL GL Transaction (swap DR/CR)
                // ============================================================
                var originalGL = await _context.Gltransactions
                    .FirstOrDefaultAsync(gl => gl.DocumentNo == receiptNo && gl.CompanyCode == currentCompanyCode);

                if (originalGL != null)
                {
                    // Create reversal with swapped DR and CR
                    var reversalGL = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = originalAmount,  // Positive amount but with swapped accounts
                        DrAccNo = originalGL.CrAccNo,  // SWAP: Original CR becomes DR
                        CrAccNo = originalGL.DrAccNo,  // SWAP: Original DR becomes CR
                        Temp = "REVERSAL",
                        DocumentNo = $"{receiptNo}-REV",
                        Source = originalGL.Source,
                        CompanyCode = currentCompanyCode,
                        TransDescript = $"REVERSAL: {originalGL.TransDescript} - Reason: {deleteReason}",
                        AuditTime = DateTime.Now,
                        AuditId = deletedBy,
                        AuditDateTime = DateTime.Now,
                        Cash = originalGL.Cash,
                        DocPosted = 1,
                        ChequeNo = originalGL.ChequeNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = reversalContribution.TransactionNo,
                        Module = "CONTRIBUTION_REVERSAL",
                        ReconId = 0
                    };

                    _context.Gltransactions.Add(reversalGL);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Created reversal GL transaction: {reversalGL.DocumentNo}");
                }

                // ============================================================
                // STEP 5: Update wallet balance (SUBTRACT instead of ADD)
                // ============================================================
                var memberRecord = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (memberRecord != null)
                {
                    var wallet = await _context.Wallets
                        .FirstOrDefaultAsync(w => w.MemberId == memberRecord.Id && w.CompanyCode == currentCompanyCode);

                    if (wallet != null)
                    {
                        // Determine which balance to update based on contribution type
                        if (originalContribShare?.ShareCapitalAmount > 0)
                        {
                            wallet.CapitalBalance -= originalAmount;
                            wallet.Balance -= originalAmount;
                            _logger.LogInformation($"Reversed CapitalBalance: -{originalAmount:C}, New: {wallet.CapitalBalance:C}");
                        }
                        else if (originalContribShare?.DepositsAmount > 0)
                        {
                            wallet.DepositBalance -= originalAmount;
                            wallet.Balance -= originalAmount;
                            _logger.LogInformation($"Reversed DepositBalance: -{originalAmount:C}, New: {wallet.DepositBalance:C}");
                        }
                        else
                        {
                            wallet.Balance -= originalAmount;
                            _logger.LogInformation($"Reversed Balance: -{originalAmount:C}, New: {wallet.Balance:C}");
                        }

                        wallet.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // ============================================================
                // STEP 6: Create blockchain reversal record
                // ============================================================
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        Action = "CONTRIBUTION_REVERSAL",
                        OriginalContributionId = contributionId,
                        OriginalReceiptNo = receiptNo,
                        ReversalReceiptNo = $"{receiptNo}-REVERSAL",
                        MemberNo = memberNo,
                        MemberName = originalContribution.MemberNoNavigation != null ?
                            $"{originalContribution.MemberNoNavigation.Surname} {originalContribution.MemberNoNavigation.OtherNames}" : memberNo,
                        OriginalAmount = originalAmount,
                        ReversalAmount = -originalAmount,
                        SharesCode = sharesCode,
                        DeleteReason = deleteReason,
                        ReversedBy = deletedBy,
                        ReversedAt = DateTime.Now,
                        OriginalTransactionDate = originalContribution.ContrDate,
                        OriginalCreatedBy = originalContribution.AuditId
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "CONTRIBUTION_REVERSAL",
                        memberNo,
                        currentCompanyCode,
                        -originalAmount,  // Negative amount for blockchain record
                        $"{receiptNo}-REV",
                        blockchainData
                    );

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;

                        // Update reversal record with blockchain ID
                        reversalContribution.BlockchainTxId = blockchainTxId;
                        reversalContribShare.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording blockchain reversal transaction");
                }

                // ============================================================
                // STEP 7: Mark original as reversed (instead of deleting)
                // ============================================================
                originalContribution.Status = "REVERSED";
                originalContribution.Remarks = $"[REVERSED] {originalContribution.Remarks} - Reversal: {deleteReason}";
                originalContribution.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Contribution {receiptNo} reversed successfully. New receipt: {reversalContribution.ReceiptNo}");

                return new ContributionDeleteResultDTO
                {
                    Success = true,
                    Message = $"Contribution {receiptNo} has been successfully reversed. Reversal Receipt: {reversalContribution.ReceiptNo}",
                    ContributionId = contributionId,
                    ReceiptNo = receiptNo,
                    MemberNo = memberNo,
                    Amount = originalAmount,
                    DeletedAt = DateTime.Now,
                    DeletedBy = deletedBy,
                    BlockchainTxId = blockchainTxId ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error reversing contribution {contributionId}");

                if (ex is ValidationException)
                {
                    throw new Exception($"Validation error: {ex.Message}");
                }

                throw new Exception($"Error reversing contribution: {ex.Message}");
            }
        }

        private async Task ReverseShareBalanceAsync(string memberNo, string sharesCode, decimal amount, string companyCode)
        {
            var existingShare = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo &&
                                         s.Sharescode == sharesCode &&
                                         s.CompanyCode == companyCode);

            if (existingShare != null)
            {
                existingShare.TotalShares -= amount;

                // If total shares becomes negative, set to 0
                if (existingShare.TotalShares < 0)
                {
                    _logger.LogWarning($"Share balance would become negative for member {memberNo}. Setting to 0.");
                    existingShare.TotalShares = 0;
                }

                existingShare.TransDate = DateTime.Now;
                existingShare.AuditTime = DateTime.Now;
                existingShare.AuditDateTime = DateTime.Now;

                _context.Shares.Update(existingShare);
                _logger.LogInformation($"Updated share balance after reversal: {existingShare.TotalShares:C}");
            }
        }

        private async Task CreateReversalGLTransactionAsync(Contrib contribution, Gltransaction originalGL, string deleteReason, string deletedBy)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            // Generate a unique transaction number
            var transactionNo = $"{originalGL.DocumentNo}-REV-{DateTime.Now:yyyyMMddHHmmss}";

            // Create reversal transaction (opposite entries - swap DR and CR)
            var reversalGL = new Gltransaction
            {
                TransDate = DateTime.Now,
                Amount = originalGL.Amount, // Same amount
                DrAccNo = originalGL.CrAccNo, // Swap: Original CR becomes DR
                CrAccNo = originalGL.DrAccNo, // Swap: Original DR becomes CR
                Temp = "REVERSAL",
                DocumentNo = $"{originalGL.DocumentNo}-REV",
                Source = originalGL.Source,
                CompanyCode = currentCompanyCode,
                TransDescript = $"REVERSAL: {originalGL.TransDescript} - Reason: {deleteReason}",
                AuditTime = DateTime.Now,
                AuditId = deletedBy,
                Cash = originalGL.Cash,
                DocPosted = 1,
                ChequeNo = originalGL.ChequeNo,
                Dregard = false,
                Recon = false,
                TransactionNo = transactionNo,
                Module = "CONTRIBUTION_REVERSAL",
                ReconId = 0,
                AuditDateTime = DateTime.Now
            };

            _context.Gltransactions.Add(reversalGL);
            _logger.LogInformation($"Created reversal GL transaction: {reversalGL.DocumentNo}");
        }

        private string DetermineContributionType(Sharetype shareType, ContributionDTO contributionDto)
        {
            // Get the ShareType name (THIS IS THE ONLY THING WE CHECK)
            var shareTypeName = (shareType.SharesType ?? shareType.SharesCode ?? "").Trim().ToLower();

            _logger.LogDebug($"=== Determining Contribution Type ===");
            _logger.LogDebug($"ShareType Name: '{shareType.SharesType}'");
            _logger.LogDebug($"ShareType Code: '{shareType.SharesCode}'");

            // ============================================================
            // CHECK SHARE TYPE NAME AGAINST KNOWN CATEGORIES
            // These should match the actual names in your Sharetypes table
            // ============================================================

            // 1. Check for SHARE CAPITAL
            string[] shareCapitalKeywords = {
        "share capital", "share capital", "share", "shares", "capital",
        "main shares", "equity", "core shares", "compulsory shares",
        "membership shares", "share", "shares"
    };

            foreach (var keyword in shareCapitalKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: SHARE_CAPITAL - '{shareType.SharesType}' contains '{keyword}'");
                    return "SHARE_CAPITAL";
                }
            }

            // 2. Check for DEPOSIT/SAVINGS
            string[] depositKeywords = {
        "deposit", "savings", "saving", "deposits", "share deposit",
        "voluntary", "welfare", "emergency", "flexible"
    };

            foreach (var keyword in depositKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: DEPOSIT - '{shareType.SharesType}' contains '{keyword}'");
                    return "DEPOSIT";
                }
            }

            // 3. Check for PASSBOOK
            string[] passbookKeywords = {
        "passbook", "pass book", "ledger", "pass_book"
    };

            foreach (var keyword in passbookKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: PASSBOOK - '{shareType.SharesType}' contains '{keyword}'");
                    return "PASSBOOK";
                }
            }

            // 4. Check for REGISTRATION FEE
            string[] regFeeKeywords = {
        "registration fee", "reg fee", "reg fees", "registration", "entry fee",
        "joining fee", "admin fee", "processing fee", "membership fee",
        "initiation fee", "signup fee", "reg_fee", "regfee"
    };

            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: REGISTRATION_FEE - '{shareType.SharesType}' contains '{keyword}'");
                    return "REGISTRATION_FEE";
                }
            }

            // 5. Check for DONOR
            string[] donorKeywords = {
        "donor", "donation", "gift", "grant", "sponsor", "endowment", "charity"
    };

            foreach (var keyword in donorKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: DONOR - '{shareType.SharesType}' contains '{keyword}'");
                    return "DONOR";
                }
            }

            // 6. Check for LOAN REPAYMENT
            string[] loanKeywords = {
        "loan repayment", "loan", "repayment", "installment", "emi", "loan recovery"
    };

            foreach (var keyword in loanKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ MATCH: LOAN_REPAYMENT - '{shareType.SharesType}' contains '{keyword}'");
                    return "LOAN_REPAYMENT";
                }
            }

            // ============================================================
            // 7. CHECK BOOLEAN FLAGS AS FALLBACK (If name doesn't match keywords)
            // ============================================================

            // If name contains "fee" but didn't match above, check if it's a fee type
            if (shareTypeName.Contains("fee"))
            {
                _logger.LogInformation($"✓ FLAG FALLBACK: REGISTRATION_FEE - '{shareType.SharesType}' contains 'fee'");
                return "REGISTRATION_FEE";
            }

            // If share capital related flags are true
            if (shareType.IsMainShares == true || shareType.Issharecapital == true)
            {
                _logger.LogInformation($"✓ FLAG FALLBACK: SHARE_CAPITAL - IsMainShares={shareType.IsMainShares}, Issharecapital={shareType.Issharecapital}");
                return "SHARE_CAPITAL";
            }

            // If withdrawable and used for guarantee/offset -> DEPOSIT
            if (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true))
            {
                _logger.LogInformation($"✓ FLAG FALLBACK: DEPOSIT - Withdrawable={shareType.Withdrawable}");
                return "DEPOSIT";
            }

            // ============================================================
            // 8. UNKNOWN - Any share type not matching above
            // Examples: SINKING FUND, WELFARE FUND, etc.
            // ============================================================
            _logger.LogWarning($"⚠ UNKNOWN ShareType: '{shareType.SharesType}' ({shareType.SharesCode}) - Will skip ContribShares");
            return "UNKNOWN";
        }

        private string GenerateReceiptNumber(string companyCode)
        {
            var now = DateTime.Now;
            var Year = now.ToString("yyyy");
            var day = now.ToString("dd");
            var month = now.ToString("MM");
            var hour = now.ToString("HH");
            var minute = now.ToString("mm");
            var second = now.ToString("ss");
            var receiptNumber = $"REC{Year}{month}{day}{hour}{minute}{second.Substring(0, 1)}";

            var random = new Random();
            var existingReceipt = _context.Contribs
                .FirstOrDefault(c => c.ReceiptNo == receiptNumber);

            if (existingReceipt != null)
            {
                // Add a suffix if duplicate occurs
                receiptNumber = $"REC{Year}{month}{day}{hour}{minute}{second.Substring(0, 1)}{random.Next(0, 9)}";
                receiptNumber = receiptNumber.Length > 12 ? receiptNumber.Substring(0, 12) : receiptNumber;
            }

            return receiptNumber;
        }
        private string GenerateTransactionNumber(string companyCode)
        {
            var now = DateTime.Now;
            var Year = now.ToString("yyyy");
            var day = now.ToString("dd");
            var month = now.ToString("MM");
            var hour = now.ToString("HH");
            var minute = now.ToString("mm");
            var second = now.ToString("ss");
            var receiptNumber = $"REC{Year}{month}{day}{hour}{minute}{second.Substring(0, 1)}";

            var random = new Random();
            var existingReceipt = _context.Contribs
                .FirstOrDefault(c => c.ReceiptNo == receiptNumber);

            if (existingReceipt != null)
            {
                // Add a suffix if duplicate occurs
                receiptNumber = $"TRNAS{Year}{month}{day}{hour}{minute}{second.Substring(0, 1)}{random.Next(0, 9)}";
                receiptNumber = receiptNumber.Length > 12 ? receiptNumber.Substring(0, 12) : receiptNumber;
            }

            return receiptNumber;
        }

        public async Task<decimal> GetMemberShareBalanceAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            // Get total share capital from Shares table
            var shareCapital = await _context.Shares
                .Where(s => s.MemberNo == memberNo && s.CompanyCode == currentCompanyCode)
                .SumAsync(s => s.TotalShares ?? 0);

            // Also get share capital from ContribShare table as backup
            var contribShareCapital = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == currentCompanyCode)
                .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

            // Return the larger amount (they should match, but this handles discrepancies)
            var totalShareBalance = Math.Max(shareCapital, contribShareCapital);

            _logger.LogDebug($"Member {memberNo} share balance: {totalShareBalance:C} (Shares: {shareCapital:C}, ContribShares: {contribShareCapital:C})");

            return totalShareBalance;
        }
        private async Task<decimal> GetExistingContributionTotalAsync(string memberNo, string sharesCode, string contributionCategory, string companyCode)
        {
            var contribShare = await _context.ContribShares
                .FirstOrDefaultAsync(cs => cs.MemberNo == memberNo &&
                                           cs.Sharescode == sharesCode &&
                                           cs.CompanyCode == companyCode);

            if (contribShare == null)
            {
                return 0;
            }

            // Return the SPECIFIC column based on contribution category
            switch (contributionCategory)
            {
                case "REGISTRATION_FEE":
                    return contribShare.RegFeeAmount ?? 0;
                case "DEPOSIT":
                    return contribShare.DepositsAmount ?? 0;
                case "DONOR":
                    return contribShare.Donor ?? 0;
                case "LOAN_REPAYMENT":
                    return contribShare.LoanAmount ?? 0;
                case "PASSBOOK":
                    return contribShare.PassBookAmount ?? 0;
                case "SHARE_CAPITAL":
                default:
                    return contribShare.ShareCapitalAmount ?? 0;
            }
        }
        public async Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember)
        {
            var member = await GetMemberByMemberNoAsync(memberNo);
            if (member == null) return false;

            // Update editable fields (all except MemberNo, Idno, CompanyCode)
            member.Surname = updatedMember.Surname ?? member.Surname;
            member.OtherNames = updatedMember.OtherNames ?? member.OtherNames;
            member.FullName = updatedMember.FullName ?? member.FullName;

            // Contact Information
            member.PhoneNo = updatedMember.PhoneNo ?? member.PhoneNo;
            member.HomeTelNo = updatedMember.HomeTelNo ?? member.HomeTelNo;
            member.Email = updatedMember.Email ?? member.Email;
            member.EmailAddress = updatedMember.EmailAddress ?? member.EmailAddress;

            // Personal Details
            member.Sex = updatedMember.Sex ?? member.Sex;
            member.Dob = updatedMember.Dob ?? member.Dob;
            member.Age = updatedMember.Age ?? member.Age;
            member.Mstatus = updatedMember.Mstatus ?? member.Mstatus;

            // Employment & Location
            member.Employer = updatedMember.Employer ?? member.Employer;
            member.Dept = updatedMember.Dept ?? member.Dept;
            member.Station = updatedMember.Station ?? member.Station;
            member.PresentAddr = updatedMember.PresentAddr ?? member.PresentAddr;

            // Company Information
            member.Cigcode = updatedMember.Cigcode ?? member.Cigcode;

            // Membership Details
            member.MembershipType = updatedMember.MembershipType ?? member.MembershipType;
            member.MemberDescription = updatedMember.MemberDescription ?? member.MemberDescription;

            // Status
            if (updatedMember.Status.HasValue)
            {
                member.Status = updatedMember.Status.Value;
            }

            // Update audit fields
            member.AuditId = updatedMember.AuditId ?? member.AuditId;
            member.AuditTime = DateTime.Now;
            member.AuditDateTime = DateTime.Now;

            // Create blockchain transaction for update
            var updateData = new
            {
                MemberNo = memberNo,
                UpdateTime = DateTime.Now,
                UpdatedBy = member.AuditId,
                UpdatedFields = new
                {
                    Surname = updatedMember.Surname,
                    OtherNames = updatedMember.OtherNames,
                    PhoneNo = updatedMember.PhoneNo,
                    LandLine = updatedMember.HomeTelNo,
                    Email = updatedMember.Email,
                    Gender = updatedMember.Sex,
                    DateOfBirth = updatedMember.Dob,
                    MaritalStatus = updatedMember.Mstatus,
                    Employer = updatedMember.Employer,
                    Department = updatedMember.Dept,
                    Station = updatedMember.Station,
                    PresentAddress = updatedMember.PresentAddr,
                    Cigcode = updatedMember.Cigcode,
                    MembershipType = updatedMember.MembershipType,
                    RegistrationType = updatedMember.MemberDescription,
                    Status = updatedMember.Status
                }
            };

            var blockchainTx = await _blockchainService.CreateTransaction(
                "MEMBER_UPDATE",
                memberNo,
                member.CompanyCode,
                0,
                memberNo,
                updateData
            );

            member.BlockchainTxId = blockchainTx.TransactionId;

            await _context.SaveChangesAsync();
            await _blockchainService.AddToBlockchain(blockchainTx);

            return true;
        }

        public async Task<Member> GetMemberByMemberNoAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            return await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);
        }
        private async Task<string> GetLastBlockHashAsync()
        {
            try
            {
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();

                return lastBlock?.BlockHash ?? "0".PadLeft(64, '0');
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting last block hash, using default");
                return "0".PadLeft(64, '0');
            }
        }
        public async Task<(string SharesCode, string Category, string Message)> DetermineContributionTypeFromC2BAsync(string memberNo, decimal amount, string companyCode)
        {
            _logger.LogInformation($"Determining contribution type for member {memberNo}, amount: {amount:C}");

            // Get member details
            var member = await GetMemberByMemberNoAsync(memberNo);
            if (member == null)
            {
                return (string.Empty, string.Empty, "Member not found");
            }

            // Get all share types for this company, ordered by priority (lower priority number = higher priority)
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ToListAsync();

            if (!shareTypes.Any())
            {
                return (string.Empty, string.Empty, "No share types configured");
            }

            // Try to match by amount first (exact matches for registration fees)
            var exactMatch = shareTypes.FirstOrDefault(st =>
                st.MinAmount > 0 && Math.Abs(amount - st.MinAmount) < 0.01m);

            if (exactMatch != null)
            {
                string category = DetermineCategoryFromShareType(exactMatch);
                _logger.LogInformation($"Exact amount match: {exactMatch.SharesCode} ({category}) - Amount: {amount:C}");
                return (exactMatch.SharesCode, category, $"Matched {exactMatch.SharesType}");
            }

            // Check for registration fee share types (lowest priority for special fees)
            var registrationFeeTypes = shareTypes.Where(st =>
                st.SharesType != null &&
                (st.SharesType.ToLower().Contains("reg") ||
                 st.SharesType.ToLower().Contains("fee") ||
                 st.SharesType.ToLower().Contains("registration"))).ToList();

            if (registrationFeeTypes.Any())
            {
                var regFeeType = registrationFeeTypes.First();
                string category = DetermineCategoryFromShareType(regFeeType);
                _logger.LogInformation($"Using registration fee share type: {regFeeType.SharesCode} ({category})");
                return (regFeeType.SharesCode, category, $"Registration fee to {regFeeType.SharesType}");
            }

            // Check for deposit/savings share types (withdrawable and used for guarantee/offset)
            var depositTypes = shareTypes.Where(st =>
                st.Withdrawable == true &&
                (st.UsedToGuarantee == true || st.UsedToOffset == true)).ToList();

            if (depositTypes.Any() && amount >= depositTypes.First().MinAmount)
            {
                var depositType = depositTypes.First();
                string category = DetermineCategoryFromShareType(depositType);
                _logger.LogInformation($"Using deposit share type: {depositType.SharesCode} ({category})");
                return (depositType.SharesCode, category, $"Deposit to {depositType.SharesType}");
            }

            // Default to main share capital
            var mainShares = shareTypes.FirstOrDefault(st => st.IsMainShares == true) ?? shareTypes.First();
            string defaultCategory = DetermineCategoryFromShareType(mainShares);
            _logger.LogInformation($"Defaulting to main share capital: {mainShares.SharesCode} ({defaultCategory})");
            return (mainShares.SharesCode, defaultCategory, $"Share capital contribution to {mainShares.SharesType}");
        }

        private string DetermineCategoryFromShareType(Sharetype shareType)
        {
            if (shareType.SharesType != null)
            {
                var typeName = shareType.SharesType.ToLower();
                if (typeName.Contains("reg") || typeName.Contains("fee") || typeName.Contains("registration"))
                    return "REGISTRATION_FEE";
                if (typeName.Contains("deposit") || typeName.Contains("savings"))
                    return "DEPOSIT";
                if (typeName.Contains("donor") || typeName.Contains("gift"))
                    return "DONOR";
                if (typeName.Contains("loan") || typeName.Contains("repayment"))
                    return "LOAN_REPAYMENT";
                if (typeName.Contains("passbook"))
                    return "PASSBOOK";
            }

            if (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true))
                return "DEPOSIT";

            if (shareType.IsMainShares == true || shareType.Issharecapital == true)
                return "SHARE_CAPITAL";

            return "SHARE_CAPITAL";
        }


        private class ValidatedContribution
        {
            public BulkContributionItemDTO Item { get; set; }
            public Sharetype ShareType { get; set; }
            public string Category { get; set; }
            public bool IsMajorShareType { get; set; }
            public string DrAcc { get; set; }
            public string CrAcc { get; set; }
            public decimal ExistingTotal { get; set; }
            public DateTime ContributionDate { get; set; }
            public DateTime DepositedDate { get; set; }
            public DateTime ReceiptDate { get; set; }
            public string PaymentMethod { get; set; }
        }
    }
}
