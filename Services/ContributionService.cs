using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Services
{
    public interface IContributionService
    {
        Task<ContributionResponseDTO> AddContributionAsync(ContributionDTO contributionDto);
        Task<List<ContributionResponseDTO>> GetMemberContributionsAsync(string memberNo);
        Task<List<ShareTypeDTO>> GetShareTypesAsync(string companyCode);
        Task<Member> GetMemberByMemberNoAsync(string memberNo);
        Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember);
        Task<decimal> GetMemberShareBalanceAsync(string memberNo);
        Task<MemberContributionHistoryDTO> GetMemberContributionHistoryAsync(string memberNo);
        Task<List<ContributionResponseDTO>> SearchContributionsAsync(DateTime? fromDate, DateTime? toDate, string? memberNo = null, string? shareType = null);
        Task<ContributionDeleteResultDTO> DeleteContributionAsync(int contributionId, string deleteReason, string deletedBy);
    }

    public class ContributionService : IContributionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<MemberService> _logger;
        private readonly ICompanyContextService _companyContextService;
        private readonly IHttpContextAccessor _httpContextAccesso;
        private readonly AuditTrailService _auditService;
        private readonly ICryptoService _cryptoService;
        // private readonly UserManager<IdentityUser> _userManager;

        public ContributionService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<MemberService> logger,
            IHttpContextAccessor httpContextAccessor,
            AuditTrailService auditService,
            //UserManager<IdentityUser> userManager,
            ICompanyContextService companyContextService,
            ICryptoService cryptoService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _httpContextAccessor = httpContextAccessor;
            _auditService = auditService;
            _logger = logger;
            //_userManager = userManager;
            _companyContextService = companyContextService;
            _cryptoService = cryptoService;
        }


        public async Task<ContributionResponseDTO> AddContributionAsync(ContributionDTO contributionDto)
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

                // Validate amount against share type minimum
                if (contributionDto.Amount < shareType.MinAmount)
                {
                    throw new ValidationException($"Amount cannot be less than minimum of {shareType.MinAmount:C}");
                }

                // Validate ContrDate (Contribution Date) - can be backdated, now, or future
                DateTime contributionDate = contributionDto.TransactionDate;

                // Optional: Add validation for future dates (if you want to restrict)
                // if (contributionDate > DateTime.Now.AddMonths(1))
                // {
                //     throw new ValidationException("Contribution date cannot be more than 1 month in the future.");
                // }

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


                // Determine contribution type
                string contributionCategory = DetermineContributionType(shareType, contributionDto);

                // Get existing total for this member + sharetype combination (for limit checking only)
                decimal existingTotal = await GetExistingContributionTotalAsync(
                    contributionDto.MemberNo,
                    contributionDto.SharesCode,
                    contributionCategory,
                    contributionDto.CompanyCode);

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

                // Get Sacco parameters
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == contributionDto.CompanyCode);

                // Generate receipt number
                var receiptNo = contributionDto.ReceiptNo ?? GenerateReceiptNumber(contributionDto.CompanyCode);

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
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 20),
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

                var signingResult = await _cryptoService.SignTransactionAsync(memberRecord.MemberNo, txDataForSigning);

                if (!signingResult.Success)
                {
                    throw new Exception($"Failed to sign transaction: {signingResult.Message}");
                }

                // =============================================
                // STEP: ATTACH SIGNATURE TO CONTRIB
                // =============================================
                contrib.TransactionSignature = signingResult.Signature;
                contrib.TransactionHash = signingResult.TransactionHash;
                contrib.TransactionSequence = signingResult.Nonce;
                contrib.IsSignatureVerified = false;

                // Get previous transaction hash for chaining
                var lastContrib = await _context.Contribs
                    .Where(c => c.MemberNo == contributionDto.MemberNo)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                // =============================================
                // FOR FIRST TRANSACTION: Use RAW Wallet Address as Genesis Hash
                // =============================================
                if (lastContrib != null)
                {
                    // Not the first transaction - link to previous transaction
                    contrib.PreviousTransactionHash = lastContrib.TransactionHash;
                    _logger.LogDebug($"Member {contributionDto.MemberNo} - Linked to previous transaction: {lastContrib.TransactionHash?.Substring(0, 16)}...");
                }
                else
                {
                    // FIRST TRANSACTION - Use the RAW wallet address as the genesis anchor
                    var memberWallet = await _cryptoService.GetWalletByMemberIdAsync(memberRecord.Id);

                    if (memberWallet != null && !string.IsNullOrEmpty(memberWallet.Address))
                    {
                        // IMPORTANT: Store the RAW wallet address directly (not hashed)
                        // This will be a 42-character string starting with "0x"
                        contrib.PreviousTransactionHash = memberWallet.Address;

                        _logger.LogInformation($"✅ FIRST TRANSACTION: Member {contributionDto.MemberNo}");
                        _logger.LogInformation($"   Wallet Address: {memberWallet.Address}");
                        _logger.LogInformation($"   Genesis Hash (Raw Wallet Address): {memberWallet.Address}");
                    }
                    else
                    {
                        // Fallback - create a deterministic genesis hash from member data
                        var genesisSource = $"{memberRecord.MemberNo}|{memberRecord.Idno}|{memberRecord.ApplicDate?.ToString("o") ?? DateTime.UtcNow.ToString("o")}";
                        var genesisHash = _cryptoService.ComputeHash(genesisSource);
                        contrib.PreviousTransactionHash = genesisHash;
                        _logger.LogWarning($"Member {contributionDto.MemberNo} - No wallet found, using deterministic genesis hash: {genesisHash}");
                    }
                }

                // Add fraud warning to remarks if suspicious
                if (fraudResult.IsSuspicious)
                {
                    contrib.Remarks = $"⚠️ FLAGGED: {string.Join("; ", fraudResult.Flags)} - {contrib.Remarks}";
                }

                _context.Contribs.Add(contrib);
                await _context.SaveChangesAsync();


                // ============================================================
                // CREATE A NEW CONTRIB SHARE ROW FOR EACH TRANSACTION
                // Each row stores ONLY this transaction's amount
                // ============================================================
                var contribShare = new ContribShare
                {
                    LocalId = contrib.Id,  // Link back to the Contrib record
                    MemberNo = contributionDto.MemberNo,
                    CompanyCode = contributionDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    Sharescode = contributionDto.SharesCode,
                    Remarks = contributionDto.Remarks,
                    AuditId = contributionDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    TransactionNo = contrib.TransactionNo,
                    ContrDate = contributionDate,  // Contribution date
                    LoanNo = null,
                    DepositedDate = depositedDate,  // Actual deposit date
                    ReceiptDate = receiptDate,      // Same as DepositedDate
                                                    // Store ONLY this transaction's amount in the appropriate column
                    ShareCapitalAmount = contributionCategory == "SHARE_CAPITAL" ? contributionDto.Amount : 0,
                    DepositsAmount = contributionCategory == "DEPOSIT" ? contributionDto.Amount : 0,
                    PassBookAmount = contributionCategory == "PASSBOOK" ? contributionDto.Amount : 0,
                    Donor = contributionCategory == "DONOR" ? contributionDto.Amount : 0,
                    LoanAmount = contributionCategory == "LOAN_REPAYMENT" ? contributionDto.Amount : 0,
                    RegFeeAmount = contributionCategory == "REGISTRATION_FEE" ? contributionDto.Amount : 0
                };

                _logger.LogInformation($"Creating new ContribShare row for {contributionCategory}: Amount = {contributionDto.Amount:C}");

                _context.ContribShares.Add(contribShare);
                await _context.SaveChangesAsync();

                // Update share balance if needed (for SHARE_CAPITAL or PASSBOOK types)
                if (contributionCategory == "SHARE_CAPITAL" ||
                    (contributionCategory == "PASSBOOK" && shareType.Issharecapital == 1))
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
                            if (shareType.Issharecapital == 1)
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
                contribShare.BlockchainTxId = blockchainTx.TransactionId;
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

        //public async Task<List<ContributionResponseDTO>> GetMemberContributionsAsync(string memberNo)
        //{
        //    var contributions = await _context.Contribs
        //        .Include(c => c.SharescodeNavigation)
        //        .Where(c => c.MemberNo == memberNo)
        //        .OrderByDescending(c => c.ContrDate)
        //        .Take(100)
        //        .ToListAsync();

        //    var member = await _context.Members
        //        .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

        //    return contributions.Select(c => new ContributionResponseDTO
        //    {
        //        Id = c.Id,
        //        MemberNo = c.MemberNo,
        //        MemberName = member != null ? $"{member.Surname} {member.OtherNames}" : c.MemberNo,
        //        TransactionDate = c.ContrDate ?? DateTime.MinValue,
        //        SharesCode = c.Sharescode ?? string.Empty,
        //        ShareTypeName = c.SharescodeNavigation?.SharesType ?? c.Sharescode ?? "Unknown",
        //        Amount = c.Amount ?? 0,
        //        ReceiptNo = c.ReceiptNo ?? string.Empty,
        //        Remarks = c.Remarks ?? string.Empty,
        //        BlockchainTxId = c.BlockchainTxId ?? string.Empty,
        //        CreatedAt = c.AuditTime,
        //        CreatedBy = c.AuditId ?? string.Empty,
        //        CompanyCode = c.CompanyCode ?? string.Empty
        //    }).ToList();
        //}

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
                    on new { SharesCode = c.Sharescode, c.CompanyCode }
                    equals new { s.SharesCode, s.CompanyCode } into shareJoin
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

        //public async Task<List<ContributionResponseDTO>> SearchContributionsAsync(DateTime? fromDate, DateTime? toDate, string? memberNo = null, string? shareType = null)
        //{
        //    var query = _context.Contribs.AsQueryable();

        //    // Apply filters
        //    if (fromDate.HasValue)
        //    {
        //        query = query.Where(c => c.ContrDate >= fromDate);
        //    }

        //    if (toDate.HasValue)
        //    {
        //        query = query.Where(c => c.ContrDate <= toDate);
        //    }

        //    if (!string.IsNullOrEmpty(memberNo))
        //    {
        //        query = query.Where(c => c.MemberNo.Contains(memberNo));
        //    }

        //    if (!string.IsNullOrEmpty(shareType))
        //    {
        //        query = query.Where(c => c.Sharescode == shareType);
        //    }

        //    // Execute query
        //    var contributions = await query
        //        .OrderByDescending(c => c.ContrDate)
        //        .Take(200)
        //        .ToListAsync();

        //    // Manually get member names for each contribution
        //    var result = new List<ContributionResponseDTO>();
        //    foreach (var c in contributions)
        //    {
        //        var member = await _context.Members
        //            .FirstOrDefaultAsync(m => m.MemberNo == c.MemberNo && m.CompanyCode == c.CompanyCode);

        //        var shareTypeObj = await _context.Sharetypes
        //            .FirstOrDefaultAsync(s => s.SharesCode == c.Sharescode && s.CompanyCode == c.CompanyCode);

        //        result.Add(new ContributionResponseDTO
        //        {
        //            Id = c.Id,
        //            MemberNo = c.MemberNo ?? string.Empty,
        //            MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : c.MemberNo ?? "Unknown",
        //            TransactionDate = c.ContrDate ?? DateTime.MinValue,
        //            SharesCode = c.Sharescode ?? string.Empty,
        //            ShareTypeName = shareTypeObj?.SharesType ?? c.Sharescode ?? "Unknown",
        //            Amount = c.Amount ?? 0,
        //            ReceiptNo = c.ReceiptNo ?? string.Empty,
        //            Remarks = c.Remarks ?? string.Empty,
        //            BlockchainTxId = c.BlockchainTxId ?? string.Empty,
        //            CreatedAt = c.AuditTime,
        //            CreatedBy = c.AuditId ?? string.Empty,
        //            CompanyCode = c.CompanyCode ?? string.Empty
        //        });
        //    }

        //    return result;
        //}

        public async Task<ContributionDeleteResultDTO> DeleteContributionAsync(int contributionId, string deleteReason, string deletedBy)
        {
            _logger.LogInformation($"Starting contribution deletion for ID: {contributionId}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Find the contribution
                var contribution = await _context.Contribs
                    .Include(c => c.MemberNoNavigation)
                    .FirstOrDefaultAsync(c => c.Id == contributionId && c.CompanyCode == currentCompanyCode);

                if (contribution == null)
                {
                    throw new ValidationException($"Contribution with ID {contributionId} not found");
                }

                // Store information for response and reversal
                var receiptNo = contribution.ReceiptNo ?? string.Empty;
                var memberNo = contribution.MemberNo;
                var amount = contribution.Amount ?? 0;
                var sharesCode = contribution.Sharescode;

                _logger.LogInformation($"Deleting contribution: Receipt {receiptNo}, Member {memberNo}, Amount {amount}");

                // Find and delete from ContribShare table if exists
                var contribShare = await _context.ContribShares
                    .FirstOrDefaultAsync(cs => cs.TransactionNo == contribution.TransactionNo);

                if (contribShare != null)
                {
                    _context.ContribShares.Remove(contribShare);
                    _logger.LogInformation($"Removed from ContribShare table");

                    // Reverse the share balance
                    await ReverseShareBalanceAsync(memberNo, sharesCode, amount, currentCompanyCode);
                }

                // Find GL Transaction if exists (using DocumentNo or TransactionNo)
                var glTransaction = await _context.Gltransactions
                    .FirstOrDefaultAsync(gl => gl.DocumentNo == receiptNo && gl.CompanyCode == currentCompanyCode);

                if (glTransaction != null)
                {
                    // Create reversal GL entry instead of just deleting
                    await CreateReversalGLTransactionAsync(contribution, glTransaction, deleteReason, deletedBy);

                    // Delete the original GL transaction
                    _context.Gltransactions.Remove(glTransaction);
                    _logger.LogInformation($"Removed original GL Transaction");
                }

                // Create blockchain reversal record
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        Action = "CONTRIBUTION_DELETION",
                        ContributionId = contributionId,
                        ReceiptNo = receiptNo,
                        MemberNo = memberNo,
                        MemberName = contribution.MemberNoNavigation != null ?
                            $"{contribution.MemberNoNavigation.Surname} {contribution.MemberNoNavigation.OtherNames}" : memberNo,
                        OriginalAmount = amount,
                        SharesCode = sharesCode,
                        DeleteReason = deleteReason,
                        DeletedBy = deletedBy,
                        DeletedAt = DateTime.Now,
                        OriginalTransactionDate = contribution.ContrDate,
                        OriginalCreatedBy = contribution.AuditId
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "CONTRIBUTION_DELETION",
                        memberNo,
                        currentCompanyCode,
                        -amount, // Negative amount for deletion
                        $"{receiptNo}-DELETED",
                        blockchainData
                    );

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording blockchain deletion transaction");
                    // Continue with deletion even if blockchain fails
                }

                // Remove the contribution
                _context.Contribs.Remove(contribution);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Contribution {receiptNo} deleted successfully");

                return new ContributionDeleteResultDTO
                {
                    Success = true,
                    Message = $"Contribution {receiptNo} has been successfully deleted",
                    ContributionId = contributionId,
                    ReceiptNo = receiptNo,
                    MemberNo = memberNo,
                    Amount = amount,
                    DeletedAt = DateTime.Now,
                    DeletedBy = deletedBy,
                    BlockchainTxId = blockchainTxId ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting contribution {contributionId}");

                if (ex is ValidationException)
                {
                    throw new Exception($"Validation error: {ex.Message}");
                }

                throw new Exception($"Error deleting contribution: {ex.Message}");
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
                _logger.LogInformation($"Updated share balance: {existingShare.TotalShares}");
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
            // Get all searchable text
            var shareTypeName = (shareType.SharesType ?? shareType.SharesCode ?? "").ToLower();
            var shareTypeCode = (shareType.SharesCode ?? "").ToLower();
            var remarks = (contributionDto.Remarks ?? "").ToLower();
            var referenceNo = (contributionDto.ReferenceNo ?? "").ToLower();
            var paymentMethod = (contributionDto.PaymentMethod ?? "").ToLower();

            _logger.LogDebug($"=== Determining Contribution Type (Word Priority Mode) ===");
            _logger.LogDebug($"ShareType Name: '{shareType.SharesType}'");
            _logger.LogDebug($"ShareType Code: '{shareType.SharesCode}'");
            _logger.LogDebug($"ShareType Flags: IsMainShares={shareType.IsMainShares}, UsedToGuarantee={shareType.UsedToGuarantee}, UsedToOffset={shareType.UsedToOffset}, Withdrawable={shareType.Withdrawable}, Issharecapital={shareType.Issharecapital}");
            _logger.LogDebug($"Remarks: '{contributionDto.Remarks}'");

            // ============================================================
            // PRIORITY 1: CHECK WORDS IN SHARETYPE NAME (HIGHEST PRIORITY)
            // This overrides ANY boolean flag configuration
            // ============================================================

            // 1.1 Check for DEPOSIT/SAVINGS words in ShareType name
            string[] depositKeywords = {
        "deposit", "savings", "saving", "deposits", "share deposit",
        "voluntary", "welfare", "emergency", "flexible"
    };

            foreach (var keyword in depositKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): DEPOSIT - matched '{keyword}' in '{shareType.SharesType}'");
                    return "DEPOSIT";
                }
            }

            // 1.2 Check for REGISTRATION FEE words in ShareType name
            string[] regFeeKeywords = {
        "reg fee", "reg fees", "registration", "registration fee", "entry fee",
        "joining fee", "admin fee", "processing fee", "fee", "annual fee",
        "membership fee", "initiation fee", "signup fee", "reg_fee",
        "regfee", "registration_fee", "member_fee"
    };

            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): REGISTRATION_FEE - matched '{keyword}' in '{shareType.SharesType}'");
                    return "REGISTRATION_FEE";
                }
            }

            // 1.3 Check for DONOR/GIFT words in ShareType name
            string[] donorKeywords = {
        "donor", "donation", "gift", "grant", "sponsor", "endowment", "charity"
    };

            foreach (var keyword in donorKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): DONOR - matched '{keyword}' in '{shareType.SharesType}'");
                    return "DONOR";
                }
            }

            // 1.4 Check for LOAN REPAYMENT words in ShareType name
            string[] loanKeywords = {
        "loan", "repayment", "installment", "emi", "loan recovery"
    };

            foreach (var keyword in loanKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): LOAN_REPAYMENT - matched '{keyword}' in '{shareType.SharesType}'");
                    return "LOAN_REPAYMENT";
                }
            }

            // 1.5 Check for PASSBOOK words in ShareType name
            string[] passbookKeywords = {
        "passbook", "pass book", "ledger", "pass_book"
    };

            foreach (var keyword in passbookKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): PASSBOOK - matched '{keyword}' in '{shareType.SharesType}'");
                    return "PASSBOOK";
                }
            }

            // 1.6 Check for SHARE CAPITAL words in ShareType name
            string[] shareCapitalKeywords = {
        "share capital","share capital", "share", "shares", "capital", "main shares", "equity",
        "core shares", "compulsory shares", "membership shares"
    };

            foreach (var keyword in shareCapitalKeywords)
            {
                if (shareTypeName.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Name): SHARE_CAPITAL - matched '{keyword}' in '{shareType.SharesType}'");
                    return "SHARE_CAPITAL";
                }
            }

            // ============================================================
            // PRIORITY 2: CHECK WORDS IN REMARKS FIELD (User-specified)
            // ============================================================

            // 2.1 Check for DEPOSIT words in Remarks
            foreach (var keyword in depositKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): DEPOSIT - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "DEPOSIT";
                }
            }

            // 2.2 Check for REGISTRATION FEE words in Remarks
            foreach (var keyword in regFeeKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): REGISTRATION_FEE - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "REGISTRATION_FEE";
                }
            }

            // 2.3 Check for DONOR words in Remarks
            foreach (var keyword in donorKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): DONOR - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "DONOR";
                }
            }

            // 2.4 Check for LOAN words in Remarks
            foreach (var keyword in loanKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): LOAN_REPAYMENT - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "LOAN_REPAYMENT";
                }
            }

            // 2.5 Check for PASSBOOK words in Remarks
            foreach (var keyword in passbookKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): PASSBOOK - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "PASSBOOK";
                }
            }

            // 2.6 Check for SHARE CAPITAL words in Remarks
            foreach (var keyword in shareCapitalKeywords)
            {
                if (remarks.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (Remarks): SHARE_CAPITAL - matched '{keyword}' in remarks: '{contributionDto.Remarks}'");
                    return "SHARE_CAPITAL";
                }
            }

            // ============================================================
            // PRIORITY 3: CHECK WORDS IN SHARETYPE CODE (Fallback for codes)
            // ============================================================

            foreach (var keyword in depositKeywords)
            {
                if (shareTypeCode.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Code): DEPOSIT - matched '{keyword}' in code: '{shareType.SharesCode}'");
                    return "DEPOSIT";
                }
            }

            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeCode.Contains(keyword))
                {
                    _logger.LogInformation($"✓ WORD MATCH (ShareType Code): REGISTRATION_FEE - matched '{keyword}' in code: '{shareType.SharesCode}'");
                    return "REGISTRATION_FEE";
                }
            }

            // ============================================================
            // PRIORITY 4: CHECK BOOLEAN FLAGS (Fallback - only if no words matched)
            // Your database flags are respected here, but only as last resort
            // ============================================================

            _logger.LogDebug($"No word matches found, falling back to boolean flags...");

            // 4.1 DEPOSIT/SAVINGS based on flags: Withdrawable=true AND (UsedToGuarantee=true OR UsedToOffset=true)
            if (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true))
            {
                _logger.LogInformation($"✓ BOOLEAN FALLBACK: DEPOSIT (Withdrawable={shareType.Withdrawable}, UsedToGuarantee={shareType.UsedToGuarantee}, UsedToOffset={shareType.UsedToOffset})");
                return "DEPOSIT";
            }

            // 4.2 REGISTRATION FEE based on flags
            if (shareType.Issharecapital == 0 &&
                shareType.UsedToGuarantee == false &&
                shareType.UsedToOffset == false &&
                shareType.Withdrawable == false)
            {
                _logger.LogInformation($"✓ BOOLEAN FALLBACK: REGISTRATION_FEE");
                return "REGISTRATION_FEE";
            }

            // 4.3 SHARE CAPITAL based on flags
            if (shareType.IsMainShares == true || shareType.Issharecapital == 1)
            {
                _logger.LogInformation($"✓ BOOLEAN FALLBACK: SHARE_CAPITAL (IsMainShares={shareType.IsMainShares}, Issharecapital={shareType.Issharecapital})");
                return "SHARE_CAPITAL";
            }

            // ============================================================
            // PRIORITY 5: CHECK PAYMENT METHOD & REFERENCE
            // ============================================================

            if (paymentMethod == "loan" || paymentMethod == "installment" || referenceNo.Contains("loan"))
            {
                _logger.LogInformation($"✓ PAYMENT METHOD FALLBACK: LOAN_REPAYMENT");
                return "LOAN_REPAYMENT";
            }

            if (paymentMethod == "donation" || paymentMethod == "grant")
            {
                _logger.LogInformation($"✓ PAYMENT METHOD FALLBACK: DONOR");
                return "DONOR";
            }

            // ============================================================
            // PRIORITY 6: DEFAULT TO SHARE CAPITAL
            // ============================================================
            _logger.LogWarning($"⚠ No match found for ShareType '{shareType.SharesType}' ({shareType.SharesCode}), defaulting to SHARE_CAPITAL");
            return "SHARE_CAPITAL";
        }

        private string GenerateReceiptNumber(string companyCode)
        {
            var now = DateTime.Now;
            var day = now.ToString("dd");
            var month = now.ToString("MM");
            var hour = now.ToString("HH");
            var minute = now.ToString("mm");
            var second = now.ToString("ss");
            var receiptNumber = $"REC{month}{day}{hour}{minute}{second.Substring(0, 1)}";

            var random = new Random();
            var existingReceipt = _context.Contribs
                .FirstOrDefault(c => c.ReceiptNo == receiptNumber);

            if (existingReceipt != null)
            {
                // Add a suffix if duplicate occurs
                receiptNumber = $"REC{month}{day}{hour}{minute}{second.Substring(0, 1)}{random.Next(0, 9)}";
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

            if (shareType.IsMainShares == true || shareType.Issharecapital == 1)
                return "SHARE_CAPITAL";

            return "SHARE_CAPITAL";
        }
    }
}
