// Services/SelfServiceLoanService.cs - COMPLETE UNIFIED VERSION
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using System.Data.Common;

namespace SACCOBlockChainSystem.Services
{
    public interface ISelfServiceLoanService
    {
        Task<MemberDashboardDTO> GetMemberDashboardAsync(string memberNo, string companyCode);
        Task<LoanEligibilityDTO> CheckLoanEligibilityAsync(string memberNo, string loanCode, string companyCode);
        Task<List<LoanProductDTO>> GetAvailableLoanProductsAsync(string memberNo, string companyCode);
        Task<LoanApplicationResultDTO> ApplyForLoanAsync(SelfLoanApplicationDTO application, string memberNo);
        Task<LoanApplicationResultDTO> GetLoanStatusAsync(string loanNo, string memberNo);
        Task<List<MemberLoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode);
        Task<LoanDisbursementResultDTO> WithdrawLoanAsync(string loanNo, string memberNo, string withdrawalMethod, string phoneNumber = null);
        Task<LoanEligibilityDTO> CheckMobileLoanEligibilityAsync(string memberNo, string companyCode);
        Task<List<LoanScheduleDTO>> GetLoanRepaymentScheduleAsync(string loanNo, string memberNo);
        Task<Dictionary<string, decimal>> GetMemberContributionSummaryAsync(string memberNo, string companyCode);

        Task<RepaymentResultDTO> MakeRepaymentAsync(LoanRepaymentDTO repayment, string memberNo);
        Task<LoanRepaymentViewModel> GetRepaymentDetailsAsync(string loanNo, string memberNo);
        Task<List<RepaymentHistoryDTO>> GetRepaymentHistoryAsync(string loanNo, string memberNo);
    }

    public class SelfServiceLoanService : ISelfServiceLoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SelfServiceLoanService> _logger;
        private readonly AuditTrailService _auditService;

        public SelfServiceLoanService(
            ApplicationDbContext context,
            AuditTrailService auditService,
            ILogger<SelfServiceLoanService> logger)
        {
            _context = context;
            _auditService = auditService;
            _logger = logger;
        }



        #region Dashboard & Contributions

        public async Task<MemberDashboardDTO> GetMemberDashboardAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Getting dashboard for member: {memberNo}");

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                    throw new InvalidOperationException("Member not found");

                var contributionSummary = await GetMemberContributionSummaryAsync(memberNo, companyCode);

                var activeLoans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                l.Status != 7 && l.Status != 10 && l.Status != 9)
                    .Select(l => new MemberLoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        LoanType = l.LoanCode ?? "Unknown",
                        PrincipalAmount = l.LoanAmt ?? 0,
                        Status = GetStatusString(l.Status),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = l.AuditDateTime,
                        OutstandingBalance = 0,
                        NextPaymentDate = null,
                        MonthlyInstallment = 0
                    })
                    .ToListAsync();

                var loanProducts = await GetAvailableLoanProductsAsync(memberNo, companyCode);

                return new MemberDashboardDTO
                {
                    MemberNo = member.MemberNo,
                    MemberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim(),
                    Email = member.Email ?? member.EmailAddress,
                    PhoneNumber = member.PhoneNo ?? member.MobileNo,
                    MemberSince = member.EffectDate ?? member.ApplicDate ?? DateTime.Now,
                    TotalDeposits = contributionSummary.GetValueOrDefault("Deposits", 0),
                    TotalShares = contributionSummary.GetValueOrDefault("Shares", 0),
                    TotalSavings = contributionSummary.GetValueOrDefault("Deposits", 0) + contributionSummary.GetValueOrDefault("Shares", 0),
                    ActiveLoansCount = activeLoans.Count,
                    TotalOutstandingLoans = await CalculateTotalOutstandingAsync(memberNo, companyCode),
                    TotalLoanAmount = await _context.Loans.Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                        .SumAsync(l => l.LoanAmt ?? 0),
                    AvailableLoanProducts = loanProducts,
                    MaxEligibleAmount = loanProducts.Any() ? loanProducts.Max(lp => lp.MaxEligibleAmount) : 0,
                    ActiveLoans = activeLoans,
                    PendingGuarantorRequests = new List<PendingGuaranteeDTO>(),
                    LastLogin = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting dashboard for member {memberNo}");
                throw;
            }
        }

        public async Task<Dictionary<string, decimal>> GetMemberContributionSummaryAsync(string memberNo, string companyCode)
        {
            var result = new Dictionary<string, decimal>();

            try
            {
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                result["Deposits"] = totalDeposits;

                var totalShareCapital = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                result["Shares"] = totalShareCapital;

                _logger.LogInformation($"Contribution summary for {memberNo}: Deposits={totalDeposits:C}, Shares={totalShareCapital:C}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting contribution summary for {memberNo}");
            }

            return result;
        }

        private async Task<decimal> CalculateTotalOutstandingAsync(string memberNo, string companyCode)
        {
            try
            {
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                l.Status != 7 && l.Status != 10)
                    .ToListAsync();

                decimal totalOutstanding = 0;

                foreach (var loan in loans)
                {
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo);
                    totalOutstanding += (loanbal?.Balance ?? 0) + (loanbal?.IntrOwed ?? 0);
                }

                return totalOutstanding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating outstanding for {memberNo}");
                return 0;
            }
        }

        #endregion

        #region Loan Eligibility 

        public async Task<LoanEligibilityDTO> CheckLoanEligibilityAsync(string memberNo, string loanCode, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Checking eligibility for member {memberNo}, loan {loanCode}");

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Loan product not found",
                        EligibleAmount = 0,
                        MaxAmount = 0,
                        MinAmount = 500
                    };
                }

                string repayMethod = loanType.Repaymethod ?? "AMT";
                decimal processingFeePercentage = loanType.Processingfee ?? 0; // This is a PERCENTAGE (e.g., 5 = 5%)
                bool isMobileLoan = loanType.MobileLoan == true;
                bool selfGuarantee = loanType.SelfGuarantee == true;

                _logger.LogInformation($"Loan Type Config: RepayMethod={repayMethod}, ProcessingFeePercentage={processingFeePercentage}%, MobileLoan={isMobileLoan}, SelfGuarantee={selfGuarantee}");

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Member not found",
                        IsMobileLoan = isMobileLoan,
                        MinAmount = 500
                    };
                }

                // Get deposits and shares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                _logger.LogInformation($"Member Financials: Deposits={totalDeposits:C}, Shares={totalShares:C}");

                decimal eligibleAmount = 0;
                decimal maxAmount = 0;
                decimal minAmount = 500;
                decimal availableSharesForGuarantee = totalShares;
                decimal requiredGuaranteeAmount = 0;

                // ============================================================
                // FOR MOBILE LOANS WITH SELF-GUARANTEE
                // Loan amount is limited by shares (not deposits × multiplier)
                // The loan amount itself becomes the self-guarantee amount
                // ============================================================
                if (isMobileLoan && selfGuarantee)
                {
                    // Maximum loan = Total shares (100% of shares)
                    eligibleAmount = totalShares;
                    maxAmount = loanType.MaxAmount.HasValue ? Math.Min(loanType.MaxAmount.Value, totalShares) : totalShares;

                    // For self-guarantee, the loan amount applied for becomes the guarantee amount
                    requiredGuaranteeAmount = 0;
                    availableSharesForGuarantee = totalShares;

                    _logger.LogInformation($"Mobile Self-Guarantee Loan: Max based on shares={eligibleAmount:C}");
                }
                else
                {
                    // Regular loan calculation using multiplier
                    decimal multiplier = 3m;
                    var shareTypes = await _context.Sharetypes
                        .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                        .ToListAsync();

                    if (shareTypes.Any())
                    {
                        multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                    }

                    eligibleAmount = totalDeposits * multiplier;
                    maxAmount = loanType.MaxAmount ?? eligibleAmount;

                    // Regular self-guarantee uses percentage of loan amount
                    if (selfGuarantee)
                    {
                        decimal guaranteePercentage = 0.05m; // 5% for regular loans
                        requiredGuaranteeAmount = Math.Min(eligibleAmount, maxAmount) * guaranteePercentage;
                        if (requiredGuaranteeAmount < 500) requiredGuaranteeAmount = 500;
                        availableSharesForGuarantee = totalShares;
                    }

                    _logger.LogInformation($"Regular Loan: Multiplier={multiplier}, Eligible={eligibleAmount:C}");
                }

                // Check for defaulted loans
                var hasDefaulted = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                   (l.Status == 8 || l.Status == 9));

                bool isEligible = true;
                List<string> messages = new List<string>();

                // Basic eligibility checks
                if (hasDefaulted)
                {
                    isEligible = false;
                    messages.Add("You have defaulted on a previous loan. Please clear the default first.");
                }

                if (totalDeposits <= 0 && !isMobileLoan)
                {
                    isEligible = false;
                    messages.Add("You have no deposits. Please make a deposit first.");
                }

                if (isMobileLoan && selfGuarantee && totalShares <= 0)
                {
                    isEligible = false;
                    messages.Add("You have no shares. Please acquire shares first to qualify for a mobile loan.");
                }

                // Parse interest rate
                decimal interestRate = 12;
                if (!string.IsNullOrEmpty(loanType.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                int defaultRepayPeriod = loanType.RepayPeriod ?? 12;
                bool requiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                         loanType.Guarantor != "No" &&
                                         loanType.Guarantor != "N" &&
                                         loanType.Guarantor != "0";

                decimal eligibleAmountForCalc = Math.Min(eligibleAmount, maxAmount);

                // For calculation, use a default amount (will be updated when member selects amount)
                decimal amountForCalculation = eligibleAmountForCalc > 0 ? eligibleAmountForCalc : 500;

                // Calculate processing fee amount based on percentage
                decimal processingFeeAmount = (amountForCalculation * processingFeePercentage) / 100;

                _logger.LogInformation($"Processing Fee: {processingFeePercentage}% of {amountForCalculation:C} = {processingFeeAmount:C}");

                var calculation = LoanCalculationHelper.CalculateLoan(
                    amountForCalculation,
                    interestRate,
                    defaultRepayPeriod,
                    repayMethod,
                    processingFeeAmount,  // Pass the calculated amount, not the percentage
                    true);

                string message = isEligible
                    ? BuildEligibilityMessage(loanType, isMobileLoan, selfGuarantee, eligibleAmountForCalc, repayMethod,
                        calculation.MonthlyInstallment, totalShares, totalDeposits, processingFeePercentage)
                    : string.Join(" ", messages);

                return new LoanEligibilityDTO
                {
                    IsEligible = isEligible,
                    Message = message,
                    EligibleAmount = eligibleAmountForCalc,
                    MaxAmount = maxAmount,
                    MinAmount = minAmount,
                    CurrentDeposits = totalDeposits,
                    CurrentShares = totalShares,
                    Multiplier = isMobileLoan && selfGuarantee ? 1m : 3m,
                    IsMobileLoan = isMobileLoan,
                    RequiresGuarantor = requiresGuarantor,
                    SelfGuarantee = selfGuarantee,
                    AvailableSharesForGuarantee = totalShares,
                    RequiredGuaranteeAmount = requiredGuaranteeAmount,
                    InterestRate = interestRate,
                    RepaymentPeriodMonths = defaultRepayPeriod,
                    RepayMethod = repayMethod,
                    ProcessingFee = processingFeePercentage,  
                    ProcessingFeeAmount = processingFeeAmount, 
                    EstimatedMonthlyInstallment = calculation.MonthlyInstallment,
                    TotalInterest = calculation.TotalInterest,
                    TotalRepayment = calculation.TotalRepayment,
                    NetDisbursement = calculation.NetDisbursement
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking eligibility for member {memberNo}");
                throw;
            }
        }

        private string BuildEligibilityMessage(Loantype loanType, bool isMobileLoan, bool selfGuarantee,
            decimal eligibleAmount, string repayMethod, decimal monthlyInstallment,
            decimal totalShares, decimal totalDeposits, decimal processingFeePercentage)
        {
            string processingFeeText = processingFeePercentage > 0
                ? $" Processing fee: {processingFeePercentage}% of loan amount."
                : "";

            if (isMobileLoan && selfGuarantee)
            {
                return $"You are eligible for a {loanType.LoanType1} mobile loan up to {eligibleAmount:C}. " +
                       $"This loan uses your shares ({totalShares:C}) as self-guarantee. " +
                       $"Repayment method: {repayMethod}. " +
                       $"Monthly payment: {monthlyInstallment:C}.{processingFeeText}";
            }
            else if (selfGuarantee)
            {
                return $"You are eligible for a {loanType.LoanType1} loan up to {eligibleAmount:C}. " +
                       $"Repayment method: {repayMethod}. " +
                       $"Monthly payment: {monthlyInstallment:C}. " +
                       $"Self-guarantee available with shares: {totalShares:C}.{processingFeeText}";
            }
            else
            {
                return $"You are eligible for a {loanType.LoanType1} loan up to {eligibleAmount:C}. " +
                       $"Repayment method: {repayMethod}. " +
                       $"Monthly payment: {monthlyInstallment:C}.{processingFeeText}";
            }
        }
        

        public async Task<List<LoanProductDTO>> GetAvailableLoanProductsAsync(string memberNo, string companyCode)
        {
            var products = new List<LoanProductDTO>();

            try
            {
                var loanTypes = await _context.Loantypes
                    .Where(lt => lt.CompanyCode == companyCode)
                    .ToListAsync();

                decimal maxMultiplier = 3m;
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    maxMultiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }

                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                foreach (var loanType in loanTypes)
                {
                    bool isMobileLoan = loanType.MobileLoan == true;
                    string repayMethod = loanType.Repaymethod ?? "AMT";
                    decimal? processingFee = loanType.Processingfee;

                    decimal eligibleAmount = totalDeposits * maxMultiplier;
                    decimal maxAmount = loanType.MaxAmount ?? eligibleAmount;
                    bool isEligible = totalDeposits > 0;

                    // Parse interest rate
                    decimal interestRate = 12;
                    if (!string.IsNullOrEmpty(loanType.Interest))
                    {
                        string interestStr = loanType.Interest.ToString().Replace("%", "");
                        if (decimal.TryParse(interestStr, out decimal parsedRate))
                        {
                            interestRate = parsedRate;
                        }
                    }

                    int repayPeriod = loanType.RepayPeriod ?? 12;
                    decimal eligibleAmountForCalc = Math.Min(eligibleAmount, maxAmount);

                    // ***** Calculate using the repayment method *****
                    var calculation = LoanCalculationHelper.CalculateLoan(
                        eligibleAmountForCalc,
                        interestRate,
                        repayPeriod,
                        repayMethod,
                        processingFee,
                        true);

                    products.Add(new LoanProductDTO
                    {
                        LoanCode = loanType.LoanCode ?? "",
                        LoanName = loanType.LoanType1 ?? loanType.LoanCode ?? "Unknown",
                        Description = $"Apply for {loanType.LoanType1} loan using {repayMethod} calculation method",
                        MinAmount = 500,
                        MaxAmount = maxAmount,
                        InterestRate = interestRate,
                        RepaymentPeriodMonths = repayPeriod,
                        RepayMethod = repayMethod,
                        ProcessingFee = processingFee ?? 0,
                        Multiplier = maxMultiplier,
                        IsMobileLoan = isMobileLoan,
                        RequiresGuarantor = false,
                        IsEligible = isEligible,
                        EligibilityMessage = isEligible
                            ? $"You qualify for up to {eligibleAmountForCalc:C}. Monthly: {calculation.MonthlyInstallment:C}"
                            : "You need to make deposits first",
                        EligibleAmount = eligibleAmountForCalc,
                        SelfGuarantee = loanType.SelfGuarantee == true, 
                        GuaranteeInfo = loanType.SelfGuarantee == true
                            ? "Self-guarantee using your shares"
                            : "Requires external guarantor",  
                        EstimatedMonthlyInstallment = calculation.MonthlyInstallment,
                        MaxEligibleAmount = eligibleAmountForCalc,
                        NetDisbursement = calculation.NetDisbursement,
                        ProcessingFeeAmount = calculation.ProcessingFee
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan products for member {memberNo}");
            }

            return products;
        }

        #endregion

        #region Loan Application

        public async Task<LoanApplicationResultDTO> ApplyForLoanAsync(SelfLoanApplicationDTO application, string memberNo)
        {
            _logger.LogInformation($"=== STARTING LOAN APPLICATION ===");
            _logger.LogInformation($"Member: {memberNo}");
            _logger.LogInformation($"LoanCode: {application.LoanCode}");
            _logger.LogInformation($"PrincipalAmount: {application.PrincipalAmount}");
            _logger.LogInformation($"RepayPeriod: {application.RepayPeriod}");
            _logger.LogInformation($"CompanyCode: {application.CompanyCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string loanNo = GenerateLoanNumber();
                _logger.LogInformation($"Generated Loan Number: {loanNo}");

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == application.CompanyCode);

                if (member == null)
                {
                    _logger.LogError($"Member not found: {memberNo}");
                    return new LoanApplicationResultDTO
                    {
                        Success = false,
                        Message = $"Member not found: {memberNo}. Please contact support."
                    };
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode && lt.CompanyCode == application.CompanyCode);

                if (loanType == null)
                {
                    _logger.LogError($"Loan product not found: {application.LoanCode}");
                    return new LoanApplicationResultDTO
                    {
                        Success = false,
                        Message = $"Loan product '{application.LoanCode}' not found."
                    };
                }

                // ***** GET REPAYMENT METHOD AND PROCESSING FEE PERCENTAGE FROM LOANTYPE *****
                string repayMethod = loanType.Repaymethod ?? "AMT";
                decimal processingFeePercentage = loanType.Processingfee ?? 0; 
                bool isMobileLoan = loanType.MobileLoan == true;

                // Parse interest rate
                decimal interestRate = 12;
                if (!string.IsNullOrEmpty(loanType.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                // Validate repayment period against loan type maximum
                int maxRepayPeriod = loanType.RepayPeriod ?? 360;
                if (application.RepayPeriod > maxRepayPeriod)
                {
                    _logger.LogWarning($"Repayment period {application.RepayPeriod} exceeds maximum {maxRepayPeriod}");
                    return new LoanApplicationResultDTO
                    {
                        Success = false,
                        Message = $"Maximum repayment period for this loan is {maxRepayPeriod} months."
                    };
                }

                // ============================================================
                // CORRECT: Calculate processing fee as PERCENTAGE of loan amount
                // ============================================================
                decimal principalAmount = application.PrincipalAmount;
                decimal processingFeeAmount = (principalAmount * processingFeePercentage) / 100;
                decimal netDisbursement = principalAmount - processingFeeAmount;

                _logger.LogInformation($"Processing Fee Calculation: {processingFeePercentage}% of {principalAmount:C} = {processingFeeAmount:C}");
                _logger.LogInformation($"Net Disbursement: {principalAmount:C} - {processingFeeAmount:C} = {netDisbursement:C}");

                // Calculate loan details using the repayment method (pass the calculated fee amount)
                var calculation = LoanCalculationHelper.CalculateLoan(
                    principalAmount,
                    interestRate,
                    application.RepayPeriod,
                    repayMethod,
                    processingFeeAmount,  // Pass the calculated amount, not the percentage
                    true);

                _logger.LogInformation($"Loan Calculation: Method={repayMethod}, Monthly={calculation.MonthlyInstallment:C}, " +
                                       $"TotalInterest={calculation.TotalInterest:C}, ProcessingFee={calculation.ProcessingFee:C}, " +
                                       $"NetDisbursement={calculation.NetDisbursement:C}");

                // ============================================================
                // 1. CREATE BLOCK (for blockchain)
                // ============================================================
                string blockHash = GenerateBlockHash();
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Block created with hash: {blockHash}");

                // ============================================================
                // 2. CREATE LOAN RECORD
                // ============================================================
                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    LoanCode = application.LoanCode,
                    LoanAmt = principalAmount,
                    MaxLoanamt = principalAmount,
                    Aamount = principalAmount,
                    IdNo = member.Idno,
                    RepayPeriod = application.RepayPeriod,
                    ApplicDate = DateTime.Now,
                    CompanyCode = application.CompanyCode,
                    Purpose = application.Purpose ?? "Applied via self-service portal",
                    Status = isMobileLoan ? 2 : 1, // 2=Submitted, 1=Draft
                    Posted = isMobileLoan ? "SUBMITTED" : "DRAFT",
                    UserName = memberNo,
                    AuditDateTime = DateTime.Now,
                    AuditId = memberNo + " (Self-Service)",
                    Guaranteed = loanType.SelfGuarantee == true ? "SELF" : "NONE",
                    AuditTime = DateTime.Now,
                    Interest = interestRate,
                    RepayMethod = repayMethod,
                    Repayrate = calculation.MonthlyInstallment,
                    BasicSalary = 0,
                    Sharecapital = 0,
                    Run = 0,
                    Run2 = 0,
                    BlockchainTxId = null,
                };

                _context.Loans.Add(loan);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Loan record created successfully with ID: {loan.Id}");

                // ============================================================
                // CREATE AUTO SELF-GUARANTEE (if enabled for this loan type)
                // ============================================================
                if (loanType.SelfGuarantee == true)
                {
                    _logger.LogInformation($"Creating auto self-guarantee for loan {loanNo}");
                    var guaranteeResult = await CreateAutoGuaranteeAsync(loanNo, memberNo, application.CompanyCode, principalAmount);

                    if (guaranteeResult)
                    {
                        await UpdateLoanWithSelfGuaranteeAsync(loanNo, principalAmount * 0.1m);
                        _logger.LogInformation($"Self-guarantee created successfully for loan {loanNo}");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to create self-guarantee for loan {loanNo}");
                    }
                }

                // ============================================================
                // 3. CREATE BLOCKCHAIN TRANSACTION DATA
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "LOAN_APPLICATION",
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    MemberIdNo = member.Idno,
                    MemberName = $"{member.Surname} {member.OtherNames}".Trim(),
                    MemberPhone = member.PhoneNo ?? member.MobileNo,
                    LoanCode = application.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    LoanTypeDescription = loanType.LoanProduct,
                    PrincipalAmount = principalAmount,
                    InterestRate = interestRate,
                    RepayPeriod = application.RepayPeriod,
                    MaxRepayPeriodAllowed = maxRepayPeriod,
                    RepayMethod = repayMethod,
                    IsMobileLoan = isMobileLoan,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetDisbursement = netDisbursement,
                    MonthlyInstallment = calculation.MonthlyInstallment,
                    TotalInterest = calculation.TotalInterest,
                    TotalRepayment = calculation.TotalRepayment,
                    ApplicationDate = DateTime.Now,
                    Purpose = application.Purpose ?? "General purpose",
                    Remarks = application.Remarks ?? "Applied via self-service portal",
                    IpAddress = application.IpAddress,
                    Status = isMobileLoan ? "Submitted" : "Draft",
                    BlockHash = blockHash,
                    PreviousBlockHash = previousHash,
                    CreatedBy = memberNo,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Generate data hash for blockchain
                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                // ============================================================
                // 4. CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_APPLICATION",
                    MemberNo = memberNo,
                    CompanyCode = application.CompanyCode,
                    Amount = principalAmount,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Blockchain transaction created: {blockchainTx.TransactionId}");

                // ============================================================
                // 5. UPDATE LOAN WITH BLOCKCHAIN TRANSACTION ID
                // ============================================================
                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Loan updated with BlockchainTxId: {blockchainTx.TransactionId}");

                // ============================================================
                // 6. SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNumber = memberNo,
                    memberName = $"{member.Surname} {member.OtherNames}".Trim(),
                    memberIdNo = member.Idno,
                    loanCode = application.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    principalAmount = principalAmount,
                    interestRate = interestRate,
                    repayPeriod = application.RepayPeriod,
                    maxRepayPeriodAllowed = maxRepayPeriod,
                    repayMethod = repayMethod,
                    isMobileLoan = isMobileLoan,
                    processingFeePercentage = processingFeePercentage,
                    processingFeeAmount = processingFeeAmount,
                    netDisbursement = netDisbursement,
                    monthlyInstallment = calculation.MonthlyInstallment,
                    totalInterest = calculation.TotalInterest,
                    totalRepayment = calculation.TotalRepayment,
                    applicationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    purpose = application.Purpose ?? "General purpose",
                    remarks = application.Remarks ?? "Applied via self-service portal",
                    ipAddress = application.IpAddress,
                    status = isMobileLoan ? "Submitted" : "Draft",
                    blockHash = blockHash,
                    previousBlockHash = previousHash,
                    blockchainTxId = blockchainTx.TransactionId,
                    createdBy = memberNo
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.MemberNo,
                    loan.LoanCode,
                    loan.LoanAmt,
                    loan.MaxLoanamt,
                    loan.IdNo,
                    loan.Interest,
                    loan.RepayPeriod,
                    loan.ApplicDate,
                    loan.Status,
                    loan.Purpose,
                    loan.RepayMethod,
                    loan.Repayrate,
                    loan.Posted,
                    loan.UserName,
                    loan.CompanyCode,
                    ProcessingFeePercentage = processingFeePercentage,
                    BlockchainTxId = blockchainTx.TransactionId,
                    BlockHash = blockHash,
                    CreatedAt = DateTime.Now,
                    CreatedBy = memberNo
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: memberNo,
                    userName: memberNo,
                    companyCode: application.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // 7. FOR MOBILE LOANS - AUTO APPROVE
                // ============================================================
                if (isMobileLoan)
                {
                    _logger.LogInformation($"Processing mobile loan auto-approval for {loanNo}");

                    loan.Status = 2; // Submitted
                    loan.Posted = "SUBMITTED";
                    await _context.SaveChangesAsync();

                    // Auto-appraise with the correct calculation
                    var appraisalResult = await AutoAppraiseLoanInternalAsync(loanNo, calculation);
                    _logger.LogInformation($"Auto-appraisal result: {appraisalResult}");

                    if (appraisalResult)
                    {
                        await AutoEndorseLoanInternalAsync(loanNo);
                        _logger.LogInformation($"Loan {loanNo} auto-endorsed successfully");
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"=== LOAN APPLICATION COMPLETED SUCCESSFULLY ===");
                _logger.LogInformation($"Blockchain Tx ID: {blockchainTx.TransactionId}");
                _logger.LogInformation($"Block Hash: {block.BlockHash}");
                _logger.LogInformation($"Processing Fee: {processingFeePercentage}% = {processingFeeAmount:C}");

                string processingFeeMessage = processingFeePercentage > 0
                    ? $"Processing fee: {processingFeePercentage}% ({processingFeeAmount:C}). "
                    : "";

                return new LoanApplicationResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Message = isMobileLoan
                        ? $"Your {repayMethod} mobile loan of {principalAmount:C} has been approved! " +
                          $"{processingFeeMessage}" +
                          $"Net disbursement: {netDisbursement:C}. " +
                          $"Monthly payment: {calculation.MonthlyInstallment:C}\n\n" +
                          $"Blockchain Reference: {blockchainTx.TransactionId.Substring(0, 8)}..."
                        : $"Your {repayMethod} loan application for {principalAmount:C} has been submitted. " +
                          $"{processingFeeMessage}" +
                          $"Application ID: {loanNo}\n\n" +
                          $"Blockchain Reference: {blockchainTx.TransactionId.Substring(0, 8)}...",
                    Status = isMobileLoan ? "Ready for Withdrawal" : "Submitted",
                    IsMobileLoan = isMobileLoan,
                    CanWithdrawNow = isMobileLoan,
                    Amount = netDisbursement,
                    ApplicationDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (DbException dbEx)
            {
                await transaction.RollbackAsync();
                _logger.LogError(dbEx, $"Database error in loan application for member {memberNo}");
                return new LoanApplicationResultDTO
                {
                    Success = false,
                    Message = $"Database error: {dbEx.Message}. Please try again or contact support."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Unexpected error in loan application for member {memberNo}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return new LoanApplicationResultDTO
                {
                    Success = false,
                    Message = $"Application failed: {ex.Message}. Please try again."
                };
            }
        }

        private string GenerateBlockHash()
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var input = $"{Guid.NewGuid()}{DateTime.Now.Ticks}{new Random().Next()}";
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }
        private string GenerateLoanNumber()
        {
            string prefix = "LN";
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string randomPart = new Random().Next(500, 9999).ToString();
            int count = _context.Loans.Count() + 1;
            string sequence = count.ToString("D4");
            return $"{prefix}{datePart}{randomPart}{sequence}";
        }

        private async Task<bool> CreateAutoGuaranteeAsync(string loanNo, string memberNo, string companyCode, decimal loanAmount)
        {
            try
            {
                _logger.LogInformation($"Creating auto self-guarantee for loan {loanNo}, member {memberNo}");
                _logger.LogInformation($"Loan Amount: {loanAmount:C}");

                // Get member's total shares
                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                _logger.LogInformation($"Total Shares available: {totalShares:C}");

                // Get loan details
                var loan = await _context.Loans
                    .Where(l => l.LoanNo == loanNo)
                    .Select(l => new { l.LoanCode, l.LoanAmt })
                    .FirstOrDefaultAsync();

                if (loan == null)
                {
                    _logger.LogError($"Loan {loanNo} not found for self-guarantee");
                    return false;
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode);

                // ============================================================
                // CORRECTED: For self-guarantee, the FULL loan amount is guaranteed
                // The member is using their own shares as collateral
                // ============================================================

                // Guarantee amount = FULL loan amount (100%)
                decimal guaranteeAmount = loanAmount;

                // Check if member has enough shares to cover the full loan amount
                if (totalShares < guaranteeAmount)
                {
                    _logger.LogWarning($"Insufficient shares for self-guarantee. Required: {guaranteeAmount:C}, Available: {totalShares:C}");

                    // Option 1: Reject the loan (recommended - should have been caught in eligibility)
                    _logger.LogError($"Member {memberNo} does not have enough shares ({totalShares:C}) to guarantee loan amount {guaranteeAmount:C}");
                    return false;

                    // Option 2: Only guarantee up to available shares (not recommended for self-guarantee)
                    // guaranteeAmount = totalShares;
                }

                // For self-guarantee, the guaranteed amount equals the loan amount
                decimal finalGuaranteeAmount = guaranteeAmount;

                _logger.LogInformation($"Self-guarantee details: Loan={loanNo}, Loan Amount={loanAmount:C}, Total Shares={totalShares:C}, Guarantee Amount={finalGuaranteeAmount:C} (100% of loan)");

                // Check if guarantee already exists
                var existingGuarantee = await _context.Loanguar
                    .FirstOrDefaultAsync(g => g.LoanNo == loanNo && g.MemberNo == memberNo);

                if (existingGuarantee != null)
                {
                    _logger.LogInformation($"Self-guarantee already exists for loan {loanNo}");
                    return true;
                }

                // Create self-guarantee record
                var loanguar = new Loanguar
                {
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    Amount = finalGuaranteeAmount,      // Full loan amount
                    Balance = finalGuaranteeAmount,      // Full loan amount as balance
                    AuditId = "SYSTEM_AUTO",
                    AuditTime = DateTime.Now,
                    Collateral = "SELF_SHARES",
                    Description = $"Self-guarantee using member shares. Total shares: {totalShares:C}, Guarantee amount (100% of loan): {finalGuaranteeAmount:C}",
                    Transfered = false,
                    Transdate = DateTime.Now,
                    FullNames = await GetMemberFullNameAsync(memberNo, companyCode),
                    Tguaranto = finalGuaranteeAmount,    // Total guarantee amount
                    CompanyCode = companyCode,
                    BlockchainTxId = null
                };

                _context.Loanguar.Add(loanguar);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Self-guarantee created for loan {loanNo} with amount {finalGuaranteeAmount:C} (100% of loan)");

                // Update loan with self-guarantee information
                await UpdateLoanWithSelfGuaranteeAsync(loanNo, finalGuaranteeAmount);

                // Create blockchain record for guarantee
                var blockchainData = new
                {
                    TransactionType = "SELF_GUARANTEE",
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    LoanAmount = loanAmount,
                    GuaranteeAmount = finalGuaranteeAmount,
                    GuaranteePercentage = 100,  // 100% of loan amount
                    TotalShares = totalShares,
                    SharesUsedAsCollateral = finalGuaranteeAmount,
                    RemainingShares = totalShares - finalGuaranteeAmount,
                    LoanCode = loan.LoanCode,
                    LoanTypeName = loanType?.LoanType1,
                    IsMobileLoan = loanType?.MobileLoan == true,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string blockHash = GenerateBlockHash();
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();

                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SELF_GUARANTEE",
                    MemberNo = memberNo,
                    CompanyCode = companyCode,
                    Amount = finalGuaranteeAmount,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                loanguar.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Blockchain record created for self-guarantee: {blockchainTx.TransactionId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating auto self-guarantee for loan {loanNo}");
                return false;
            }
        }


        private async Task<string> GetMemberFullNameAsync(string memberNo, string companyCode)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null) return memberNo;

            return $"{member.Surname} {member.OtherNames}".Trim();
        }

        private async Task UpdateLoanWithSelfGuaranteeAsync(string loanNo, decimal guaranteeAmount)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan != null)
                {
                    loan.Guaranteed = "SELF";
                    loan.AddSecurity = $"Self-guaranteed with shares amount: {guaranteeAmount:C}";
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Loan {loanNo} updated with self-guarantee info");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan with self-guarantee info for {loanNo}");
            }
        }

        private async Task<bool> AutoAppraiseLoanInternalAsync(string loanNo, LoanCalculationResult calculation)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);
                if (loan == null) return false;

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == loan.CompanyCode);

                // Calculate total deposits and eligibility
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == loan.MemberNo && cs.CompanyCode == loan.CompanyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == loan.MemberNo && cs.CompanyCode == loan.CompanyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                decimal multiplier = 3m;
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == loan.CompanyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }

                // Get loan count for the member
                int loanCount = await _context.Loans
                    .CountAsync(l => l.MemberNo == loan.MemberNo && l.CompanyCode == loan.CompanyCode);

                decimal eligibleAmount = totalDeposits * multiplier;

                // For self-guarantee loans, also check shares
                bool isSelfGuarantee = loanType?.SelfGuarantee == true;
                bool isMobileLoan = loanType?.MobileLoan == true;

                bool approved = (loan.LoanAmt ?? 0) <= eligibleAmount;

                // For self-guarantee, also verify shares are sufficient
                if (approved && isSelfGuarantee)
                {
                    decimal requiredGuarantee = loan.LoanAmt ?? 0;
                    if (totalShares < requiredGuarantee)
                    {
                        approved = false;
                        _logger.LogWarning($"Self-guarantee failed: Shares {totalShares:C} < Loan amount {requiredGuarantee:C}");
                    }
                }

                // Parse interest rate from loanType (handle string to decimal conversion)
                decimal? interestRate = null;
                if (!string.IsNullOrEmpty(loanType?.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                decimal? totalInterestRate = null;
                if (!string.IsNullOrEmpty(loanType?.Interest)) 
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        totalInterestRate = parsedRate;
                    }
                }

                string appraisalReason = approved
                    ? $"APPROVED - Loan amount {loan.LoanAmt:C} within eligible amount {eligibleAmount:C} (Deposits: {totalDeposits:C} × {multiplier})"
                    : $"REJECTED - Loan amount {loan.LoanAmt:C} exceeds eligible amount {eligibleAmount:C} (Deposits: {totalDeposits:C} × {multiplier})";

                _logger.LogInformation($"Auto-appraisal for loan {loanNo}: {appraisalReason}");

                // ============================================================
                // 1. CREATE BLOCK FOR APPRAISAL
                // ============================================================
                string blockHash = GenerateBlockHash();
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Block created for appraisal: {blockHash}");

                // ============================================================
                // 2. CREATE APPRAISAL RECORD
                // ============================================================
                var appraisal = new Appraisal
                {
                    LoanNo = loanNo,
                    CompanyCode = loan.CompanyCode,
                    Salary = 0,
                    Allowances = 0,
                    MemberNo = loan.MemberNo,
                    AppraisDate = DateTime.Now,
                    AuditTime = DateTime.Now,
                    AuditID = "SYSTEM_AUTO",
                    OfficerNames = "Auto-Appraisal System",
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                    Shares = totalDeposits,
                    AmtRecommended = approved ? loan.LoanAmt : eligibleAmount,
                    Principal = loan.LoanAmt ?? 0,
                    Loans = loanCount,  
                    NoOfLoans = loanCount,  
                    TotalDeductions = 0,
                    Interest = interestRate,  
                    TInterest = totalInterestRate,  
                    Reason = appraisalReason,
                    RepayMethod = loan.RepayMethod ?? "AMT",
                    RepayRate = calculation.MonthlyInstallment,
                    TotalInterest = calculation.TotalInterest,
                    BlockchainTxId = null
                };

                _context.Appraisal.Add(appraisal);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Appraisal record created for loan {loanNo}");

                // ============================================================
                // 3. CREATE BLOCKCHAIN TRANSACTION DATA FOR APPRAISAL
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "LOAN_APPRAISAL",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    LoanCode = loan.LoanCode,
                    LoanTypeName = loanType?.LoanType1,
                    AppraisalDate = DateTime.Now,
                    AppraisalOfficer = "SYSTEM_AUTO",
                    RequestedAmount = loan.LoanAmt ?? 0,
                    EligibleAmount = eligibleAmount,
                    TotalDeposits = totalDeposits,
                    TotalShares = totalShares,
                    Multiplier = multiplier,
                    IsApproved = approved,
                    Decision = approved ? "APPROVED" : "REJECTED",
                    Reason = appraisalReason,
                    RepayMethod = loan.RepayMethod ?? "AMT",
                    MonthlyInstallment = calculation.MonthlyInstallment,
                    TotalInterest = calculation.TotalInterest,
                    ProcessingFee = calculation.ProcessingFee,
                    NetDisbursement = calculation.NetDisbursement,
                    PreviousStatus = GetStatusString(loan.Status),
                    NewStatus = approved ? "Approved" : "Rejected",
                    BlockHash = blockHash,
                    PreviousBlockHash = previousHash,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Generate data hash for blockchain
                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                // ============================================================
                // 4. CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_APPRAISAL",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = loan.LoanAmt ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Blockchain transaction created for appraisal: {blockchainTx.TransactionId}");

                // ============================================================
                // 5. UPDATE APPRAISAL WITH BLOCKCHAIN TRANSACTION ID
                // ============================================================
                appraisal.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // ============================================================
                // 6. UPDATE LOAN STATUS
                // ============================================================
                int previousStatus = loan.Status ?? 1;

                if (approved)
                {
                    loan.Status = 4; // Approved
                    loan.Posted = "APPROVED";
                }
                else
                {
                    loan.Status = 10; // Rejected
                    loan.Posted = "REJECTED";
                }

                loan.AuditDateTime = DateTime.Now;
                loan.BlockchainTxId = blockchainTx.TransactionId;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Loan {loanNo} status updated from {previousStatus} to {loan.Status}");

                // ============================================================
                // 7. CREATE BLOCKCHAIN TRANSACTION FOR LOAN STATUS UPDATE
                // ============================================================
                var statusBlockchainData = new
                {
                    TransactionType = "LOAN_STATUS_UPDATE",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    PreviousStatus = GetStatusString(previousStatus),
                    NewStatus = GetStatusString(loan.Status),
                    Reason = appraisalReason,
                    UpdatedBy = "SYSTEM_AUTO",
                    AppraisalBlockchainTxId = blockchainTx.TransactionId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string statusDataHash = await GenerateTransactionHashAsync(statusBlockchainData);

                // Create new block for status update
                string statusBlockHash = GenerateBlockHash();
                var statusLastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string statusPreviousHash = statusLastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var statusBlock = new Block
                {
                    BlockHash = statusBlockHash,
                    PreviousHash = statusPreviousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(statusBlock);
                await _context.SaveChangesAsync();

                var statusBlockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_STATUS_UPDATE",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = loan.LoanAmt ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = statusDataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(statusBlockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = statusBlock.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(statusBlockchainTx);
                await _context.SaveChangesAsync();

                // ============================================================
                // 8. SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNumber = loan.MemberNo,
                    memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    loanCode = loan.LoanCode,
                    loanTypeName = loanType?.LoanType1,
                    appraisalDate = DateTime.Now,
                    appraisalOfficer = "SYSTEM_AUTO",
                    requestedAmount = loan.LoanAmt ?? 0,
                    eligibleAmount = eligibleAmount,
                    totalDeposits = totalDeposits,
                    totalShares = totalShares,
                    multiplier = multiplier,
                    isApproved = approved,
                    decision = approved ? "APPROVED" : "REJECTED",
                    reason = appraisalReason,
                    repayMethod = loan.RepayMethod ?? "AMT",
                    monthlyInstallment = calculation.MonthlyInstallment,
                    totalInterest = calculation.TotalInterest,
                    processingFee = calculation.ProcessingFee,
                    netDisbursement = calculation.NetDisbursement,
                    previousStatus = GetStatusString(previousStatus),
                    newStatus = GetStatusString(loan.Status),
                    appraisalBlockchainTxId = blockchainTx.TransactionId,
                    statusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    appraisalBlockHash = blockHash,
                    statusUpdateBlockHash = statusBlockHash
                };

                var appraisalForAudit = new
                {
                    appraisal.LoanNo,
                    appraisal.MemberNo,
                    appraisal.AppraisDate,
                    appraisal.OfficerNames,
                    appraisal.AmtRecommended,
                    appraisal.Principal,
                    appraisal.Reason,
                    appraisal.RepayMethod,
                    appraisal.RepayRate,
                    appraisal.TotalInterest,
                    MultiplierUsed = multiplier,
                    EligibleAmount = eligibleAmount,
                    TotalDeposits = totalDeposits,
                    TotalShares = totalShares,
                    IsApproved = approved,
                    BlockchainTxId = blockchainTx.TransactionId,
                    StatusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "SYSTEM_AUTO"
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: appraisalForAudit,
                    tableName: "Appraisal",
                    recordId: loanNo,
                    userId: "SYSTEM_AUTO",
                    userName: "System Auto-Appraisal",
                    companyCode: loan.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                _logger.LogInformation($"=== AUTO-APPRAISAL COMPLETED SUCCESSFULLY ===");
                _logger.LogInformation($"Loan: {loanNo}, Decision: {(approved ? "APPROVED" : "REJECTED")}");
                _logger.LogInformation($"Appraisal Blockchain Tx: {blockchainTx.TransactionId}");
                _logger.LogInformation($"Status Update Blockchain Tx: {statusBlockchainTx.TransactionId}");

                return approved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in auto-appraisal for {loanNo}");
                throw; // Re-throw to let outer transaction handle rollback
            }
        }

        #region Loan Disbursement with GL Integration
        private async Task<bool> AutoEndorseLoanInternalAsync(string loanNo)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);
                if (loan == null) return false;

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == loan.CompanyCode);

                // Get loan type for processing fee and GL accounts
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                if (loanType == null) return false;

                // ============================================================
                // FIXED: Get the appraisal record for this loan
                // ============================================================
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo);

                // Get the bank/GL account for disbursement
                var bankAccount = await GetDisbursementBankAccountAsync(loan.CompanyCode);
                if (bankAccount == null)
                {
                    _logger.LogError($"No active bank account found for company {loan.CompanyCode}");
                    return false;
                }

                // Get GL accounts from loan type
                string loanAssetAccount = loanType.LoanAcc ?? "LOAN_ASSET_ACCOUNT";
                string contraAccount = bankAccount.GlAccountNo ?? "BANK_ACCOUNT";
                string processingFeeAccount = GetProcessingFeeGLAccount(loanType);

                decimal amountToDisburse = loan.LoanAmt ?? 0;

                if (amountToDisburse <= 0)
                {
                    _logger.LogError($"Loan amount is zero for loan {loanNo}");
                    return false;
                }

                // ============================================================
                // CORRECT: Calculate processing fee as PERCENTAGE of loan amount
                // ============================================================
                decimal processingFeePercentage = loanType.Processingfee ?? 0;
                decimal processingFeeAmount = (amountToDisburse * processingFeePercentage) / 100;
                decimal netAmount = amountToDisburse - processingFeeAmount;

                _logger.LogInformation($"=== AUTO-ENDORSEMENT FOR LOAN {loanNo} ===");
                _logger.LogInformation($"Gross Amount: {amountToDisburse:C}");
                _logger.LogInformation($"Processing Fee: {processingFeePercentage}% = {processingFeeAmount:C}");
                _logger.LogInformation($"Net Amount: {netAmount:C}");
                _logger.LogInformation($"Loan Asset Account: {loanAssetAccount}");
                _logger.LogInformation($"Bank Account: {contraAccount}");
                _logger.LogInformation($"Processing Fee Account: {processingFeeAccount}");

                // Generate transaction numbers
                string transactionNo = GenerateTransactionNumber();
                string chequeNo = GenerateChequeNumber();
                string voucherNo = GenerateVoucherNumber();

                // ============================================================
                // 1. CREATE BLOCK FOR ENDORSEMENT
                // ============================================================
                string blockHash = GenerateBlockHash();
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Block created for endorsement: {blockHash}");

                // ============================================================
                // 2. CREATE CHEQUE RECORD
                // ============================================================
                var cheque = new Cheque
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    ChequeNo = chequeNo,
                    Amount = amountToDisburse,
                    IntAmount = 0,
                    AmountIssued = netAmount,
                    ProcessingFee = processingFeeAmount,
                    DateIssued = DateTime.Now,
                    Status = "Pending",
                    AuditId = "SYSTEM_AUTO",
                    AuditTime = DateTime.Now,
                    TransactionNo = transactionNo,
                    Reasons = appraisal?.Reason ?? "Auto-endorsed loan",  // FIXED: Use appraisal instance
                    Voucherno = voucherNo,
                    Voucheramount = netAmount,
                    Paymethod = "AUTO",
                    Amountinword = NumberToWords(netAmount),
                    Refloan = true,
                    Dregard = 0,
                    PaidBf = 0,
                    OrgAmt = amountToDisburse,
                    LoanAcc = loanAssetAccount,
                    ContraAcc = contraAccount,
                    PremiumAcc = processingFeeAccount,
                    Offsetamount = processingFeeAmount,
                    IntrOwed = 0,
                    BlockchainTxId = null
                };

                _context.Cheques.Add(cheque);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Cheque record created for loan {loanNo}");

                // ============================================================
                // 3. CREATE GL TRANSACTIONS
                // ============================================================

                // Transaction 1: DR Loan Asset Account, CR Bank Account (for net amount)
                var glTransaction1 = new Gltransaction
                {
                    TransDate = DateTime.Now,
                    Amount = netAmount,
                    DrAccNo = loanAssetAccount,
                    CrAccNo = contraAccount,
                    Temp = "DISBURSEMENT",
                    DocumentNo = voucherNo,
                    Source = "LOAN_DISBURSEMENT",
                    CompanyCode = loan.CompanyCode,
                    TransDescript = $"Loan Disbursement - {loanNo} - Principal Amount",
                    AuditTime = DateTime.Now,
                    AuditId = "SYSTEM_AUTO",
                    Cash = 0,
                    DocPosted = 1,
                    ChequeNo = cheque.ChequeNo,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = transactionNo,
                    Module = "LOAN",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };
                _context.Gltransactions.Add(glTransaction1);

                // Transaction 2: Processing Fee Transaction
                if (processingFeeAmount > 0 && !string.IsNullOrEmpty(processingFeeAccount))
                {
                    var glTransaction2 = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = processingFeeAmount,
                        DrAccNo = contraAccount,          // DR Bank account (reduce bank balance)
                        CrAccNo = processingFeeAccount,   // CR Processing fee income account
                        Temp = "PROCESSING_FEE",
                        DocumentNo = voucherNo,
                        Source = "LOAN_PROCESSING_FEE",
                        CompanyCode = loan.CompanyCode,
                        TransDescript = $"Loan Processing Fee - {loanNo} - {processingFeePercentage}% = {processingFeeAmount:C}",
                        AuditTime = DateTime.Now,
                        AuditId = "SYSTEM_AUTO",
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = cheque.ChequeNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now,
                        BlockchainTxId = null
                    };
                    _context.Gltransactions.Add(glTransaction2);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"GL Transactions created for loan {loanNo}");

                // ============================================================
                // 4. CREATE ENDMAIN (ENDORSEMENT) RECORD
                // ============================================================
                var endmain = new Endmain
                {
                    LoanNo = loanNo,
                    CompanyCode = loan.CompanyCode,
                    MinuteNo = await GenerateMinuteNumberAsync(loan.CompanyCode),
                    MeetingDate = DateTime.Now,
                    AmtApproved = appraisal?.AmtRecommended ?? netAmount, 
                    Accepted = "1",
                    ChairSigned = "SYSTEM_AUTO",
                    SecSigned = "SYSTEM_AUTO",
                    MembSigned = loan.MemberNo,
                    Reasons = $"Auto-endorsed for mobile loan. Processing fee: {processingFeePercentage}% ({processingFeeAmount:C})",
                    Remarks = $"Auto-endorsed - Mobile Loan. Net disbursement: {netAmount:C}",
                    AuditId = "SYSTEM_AUTO",
                    AuditTime = DateTime.Now,
                    TransactionNo = transactionNo,
                    BlockchainTxId = null
                };

                _context.Endmain.Add(endmain);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Endmain record created for loan {loanNo}");

                // ============================================================
                // 5. CREATE BLOCKCHAIN TRANSACTION DATA FOR ENDORSEMENT
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "LOAN_ENDORSEMENT",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    MemberPhone = member?.PhoneNo ?? member?.MobileNo,
                    LoanCode = loan.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    EndorsementDate = DateTime.Now,
                    EndorsedBy = "SYSTEM_AUTO",
                    GrossAmount = amountToDisburse,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    LoanAssetAccount = loanAssetAccount,
                    BankAccount = contraAccount,
                    ProcessingFeeAccount = processingFeeAccount,
                    ChequeNo = cheque.ChequeNo,
                    VoucherNo = voucherNo,
                    TransactionNo = transactionNo,
                    PreviousStatus = GetStatusString(loan.Status),
                    NewStatus = "Endorsed",
                    BlockHash = blockHash,
                    PreviousBlockHash = previousHash,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Generate data hash for blockchain
                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                // ============================================================
                // 6. CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_ENDORSEMENT",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = netAmount,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Blockchain transaction created for endorsement: {blockchainTx.TransactionId}");

                // ============================================================
                // 7. UPDATE ALL RECORDS WITH BLOCKCHAIN TRANSACTION ID
                // ============================================================
                cheque.BlockchainTxId = blockchainTx.TransactionId;
                glTransaction1.BlockchainTxId = blockchainTx.TransactionId;

                if (processingFeeAmount > 0)
                {
                    var glTransaction2 = await _context.Gltransactions
                        .FirstOrDefaultAsync(gl => gl.TransactionNo == transactionNo && gl.Temp == "PROCESSING_FEE");
                    if (glTransaction2 != null)
                    {
                        glTransaction2.BlockchainTxId = blockchainTx.TransactionId;
                    }
                }

                endmain.BlockchainTxId = blockchainTx.TransactionId;

                // Update loan status
                loan.Status = 5; // Endorsed
                loan.Posted = "ENDORSED";
                loan.TransactionNo = transactionNo;
                loan.AuditDateTime = DateTime.Now;
                loan.BlockchainTxId = blockchainTx.TransactionId;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"All records updated with BlockchainTxId: {blockchainTx.TransactionId}");

                // ============================================================
                // 8. CREATE BLOCKCHAIN TRANSACTION FOR LOAN STATUS UPDATE
                // ============================================================
                var statusBlockchainData = new
                {
                    TransactionType = "LOAN_STATUS_UPDATE",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    PreviousStatus = "Approved",
                    NewStatus = "Endorsed",
                    UpdatedBy = "SYSTEM_AUTO",
                    EndorsementBlockchainTxId = blockchainTx.TransactionId,
                    ChequeNo = cheque.ChequeNo,
                    TransactionNo = transactionNo,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string statusDataHash = await GenerateTransactionHashAsync(statusBlockchainData);

                // Create new block for status update
                string statusBlockHash = GenerateBlockHash();
                var statusLastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string statusPreviousHash = statusLastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var statusBlock = new Block
                {
                    BlockHash = statusBlockHash,
                    PreviousHash = statusPreviousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(statusBlock);
                await _context.SaveChangesAsync();

                var statusBlockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_STATUS_UPDATE",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = netAmount,
                    Timestamp = DateTime.Now,
                    DataHash = statusDataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(statusBlockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = statusBlock.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(statusBlockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Status update blockchain transaction created: {statusBlockchainTx.TransactionId}");

                // ============================================================
                // 9. SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNumber = loan.MemberNo,
                    memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    loanCode = loan.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    endorsementDate = DateTime.Now,
                    endorsedBy = "SYSTEM_AUTO",
                    grossAmount = amountToDisburse,
                    processingFeePercentage = processingFeePercentage,
                    processingFeeAmount = processingFeeAmount,
                    netAmount = netAmount,
                    loanAssetAccount = loanAssetAccount,
                    bankAccount = contraAccount,
                    processingFeeAccount = processingFeeAccount,
                    chequeNo = cheque.ChequeNo,
                    voucherNo = voucherNo,
                    transactionNo = transactionNo,
                    previousStatus = "Approved",
                    newStatus = "Endorsed",
                    endorsementBlockchainTxId = blockchainTx.TransactionId,
                    statusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    endorsementBlockHash = blockHash,
                    statusUpdateBlockHash = statusBlockHash
                };

                var endorsementForAudit = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    EndorsementDate = DateTime.Now,
                    EndorsedBy = "SYSTEM_AUTO",
                    GrossAmount = amountToDisburse,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    ChequeNo = cheque.ChequeNo,
                    VoucherNo = voucherNo,
                    TransactionNo = transactionNo,
                    LoanAssetAccount = loanAssetAccount,
                    BankAccount = contraAccount,
                    ProcessingFeeAccount = processingFeeAccount,
                    Status = "Endorsed",
                    BlockchainTxId = blockchainTx.TransactionId,
                    StatusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    CreatedAt = DateTime.Now
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: null,
                    newModel: endorsementForAudit,
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: "SYSTEM_AUTO",
                    userName: "System Auto-Endorsement",
                    companyCode: loan.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                _logger.LogInformation($"=== AUTO-ENDORSEMENT COMPLETED SUCCESSFULLY ===");
                _logger.LogInformation($"Loan: {loanNo}, Status: Endorsed");
                _logger.LogInformation($"Processing Fee: {processingFeePercentage}% = {processingFeeAmount:C}");
                _logger.LogInformation($"Endorsement Blockchain Tx: {blockchainTx.TransactionId}");
                _logger.LogInformation($"Status Update Blockchain Tx: {statusBlockchainTx.TransactionId}");
                _logger.LogInformation($"Cheque No: {cheque.ChequeNo}");
                _logger.LogInformation($"Voucher No: {voucherNo}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in auto-endorsement for {loanNo}");
                return false;
            }
        }

        private async Task<string> GenerateMinuteNumberAsync(string companyCode)
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string prefix = "MIN";

            // Get the last minute number for today to determine sequence
            var lastMinute = await _context.Endmain
                .Where(e => e.CompanyCode == companyCode && e.MinuteNo.StartsWith(prefix + today))
                .OrderByDescending(e => e.MinuteNo)
                .Select(e => e.MinuteNo)
                .FirstOrDefaultAsync();

            int nextSequence = 1;

            if (!string.IsNullOrEmpty(lastMinute) && lastMinute.Length >= 14)
            {
                // Extract the last 4 digits as sequence number
                string sequenceStr = lastMinute.Substring(lastMinute.Length - 4);
                if (int.TryParse(sequenceStr, out int lastSeq))
                {
                    nextSequence = lastSeq + 1;
                }
            }

            // Format: MIN + YYYYMMDD + 4-digit sequence (padded with zeros)
            string minuteNo = $"{prefix}{today}{nextSequence:D4}";

            _logger.LogInformation($"Generated minute number: {minuteNo}");
            return minuteNo;
        }

        public async Task<LoanDisbursementResultDTO> WithdrawLoanAsync(string loanNo, string memberNo, string withdrawalMethod, string phoneNumber = null)
        {
            _logger.LogInformation($"=== WITHDRAWAL REQUEST ===");
            _logger.LogInformation($"LoanNo: {loanNo}, Member: {memberNo}, Method: {withdrawalMethod}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Get the loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return new LoanDisbursementResultDTO { Success = false, Message = "Loan not found" };
                }

                if (loan.Status != 5)
                {
                    return new LoanDisbursementResultDTO { Success = false, Message = $"Loan status is {loan.Status}, expected 5 (Endorsed)" };
                }

                // 2. Get loan type for configuration
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                if (loanType == null)
                {
                    return new LoanDisbursementResultDTO { Success = false, Message = "Loan product configuration not found" };
                }

                // ==================================================================
                // FIXED: Get the appraisal record for this loan to get TotalInterest
                // ==================================================================
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo);

                // 3. Get the cheque record
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == loan.CompanyCode);

                if (cheque == null)
                {
                    _logger.LogError($"Cheque record not found for loan {loanNo}");
                    return new LoanDisbursementResultDTO
                    {
                        Success = false,
                        Message = "Disbursement record not found. Please contact support."
                    };
                }

                if (cheque.Status == "Disbursed")
                {
                    return new LoanDisbursementResultDTO
                    {
                        Success = false,
                        Message = "This loan has already been disbursed"
                    };
                }

                // 4. Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == loan.CompanyCode);

                string memberPhone = phoneNumber ?? member?.PhoneNo ?? member?.MobileNo ?? "";
                string memberName = $"{member?.Surname ?? ""} {member?.OtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    memberName = member?.MemberNo ?? "Unknown Member";
                }

                // ============================================================
                // CORRECT: Calculate amounts using PERCENTAGE
                // ============================================================
                decimal principalAmount = loan.LoanAmt ?? 0;  
                decimal processingFeePercentage = loanType.Processingfee ?? 0;  
                decimal processingFeeAmount = (principalAmount * processingFeePercentage) / 100;
                decimal netAmount = principalAmount - processingFeeAmount;

                // Get total interest from appraisal or calculate
                decimal totalInterest = appraisal?.TotalInterest ?? 0;

                _logger.LogInformation($"Disbursement Calculation:");
                _logger.LogInformation($"  Principal Amount: {principalAmount:C}");
                _logger.LogInformation($"  Processing Fee Percentage: {processingFeePercentage}%");
                _logger.LogInformation($"  Processing Fee Amount: {processingFeeAmount:C}");
                _logger.LogInformation($"  Net Disbursement: {netAmount:C}");
                _logger.LogInformation($"  Total Interest: {totalInterest:C}");

                // Clean phone number for M-Pesa
                if (!string.IsNullOrEmpty(memberPhone) && withdrawalMethod == "MPESA")
                {
                    memberPhone = FormatPhoneNumber(memberPhone);
                }

                // Get GL accounts
                string loanAssetAccount = cheque.LoanAcc ?? loanType.LoanAcc;
                string sourceAccount = cheque.ContraAcc ?? await GetBankGLAccountAsync(loan.CompanyCode);
                string processingFeeAccount = cheque.PremiumAcc ?? GetProcessingFeeGLAccount(loanType);

                // ============================================================
                // 1. CREATE BLOCK FOR WITHDRAWAL
                // ============================================================
                string blockHash = GenerateBlockHash();
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Block created for withdrawal: {blockHash}");

                // ============================================================
                // 2. CREATE DISBURSEMENT GL TRANSACTION (Principal Amount)
                // ============================================================
                string transactionNo = GenerateTransactionNumber();

                var disbursementGL = new Gltransaction
                {
                    TransDate = DateTime.Now,
                    Amount = principalAmount,
                    DrAccNo = loanAssetAccount,
                    CrAccNo = sourceAccount,
                    Temp = "DISBURSEMENT",
                    DocumentNo = cheque.Voucherno,
                    Source = "LOAN_DISBURSEMENT",
                    CompanyCode = loan.CompanyCode,
                    TransDescript = $"Loan Disbursement - {loanNo} - Principal Amount",
                    AuditTime = DateTime.Now,
                    AuditId = memberNo,
                    Cash = 0,
                    DocPosted = 1,
                    ChequeNo = cheque.ChequeNo,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = transactionNo,
                    Module = "LOAN",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Gltransactions.Add(disbursementGL);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Disbursement GL transaction created for {principalAmount:C}");

                // ============================================================
                // 3. CREATE PROCESSING FEE GL TRANSACTION
                // ============================================================
                Gltransaction feeGL = null;
                if (processingFeeAmount > 0)
                {
                    feeGL = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = processingFeeAmount,
                        DrAccNo = sourceAccount,
                        CrAccNo = processingFeeAccount,
                        Temp = "PROCESSING_FEE",
                        DocumentNo = cheque.Voucherno,
                        Source = "LOAN_PROCESSING_FEE",
                        CompanyCode = loan.CompanyCode,
                        TransDescript = $"Processing Fee - {loanNo} - {processingFeePercentage}% = {processingFeeAmount:C}",
                        AuditTime = DateTime.Now,
                        AuditId = memberNo,
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = cheque.ChequeNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now,
                        BlockchainTxId = null
                    };
                    _context.Gltransactions.Add(feeGL);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Processing fee GL transaction created for {processingFeeAmount:C}");
                }

                // ============================================================
                // 4. UPDATE CHEQUE STATUS
                // ============================================================
                cheque.Status = "Disbursed";
                cheque.Balance = netAmount;
                cheque.UserName = memberNo;
                cheque.AuditDateTime = DateTime.Now;
                cheque.DateIssued = DateTime.Now;
                cheque.AuditId = memberNo;
                cheque.CollectorName = $"{memberName} - {memberPhone}";
                _context.Cheques.Update(cheque);

                // ============================================================
                // 5. UPDATE LOAN STATUS
                // ============================================================
                int previousStatus = loan.Status ?? 1;
                loan.Status = 6; // Disbursed
                loan.Posted = "ACTIVE";
                loan.UserName = memberNo;
                loan.AuditDateTime = DateTime.Now;
                loan.AuditTime = DateTime.Now;
                _context.Loans.Update(loan);

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Cheque and Loan status updated");

                // ============================================================
                // 6. CREATE LOAN BALANCE RECORD - USE FULL PRINCIPAL AMOUNT
                // ============================================================
                decimal monthlyInstallment = LoanCalculationHelper.CalculateMonthlyInstallment(
                    principalAmount,  // Use FULL principal amount, NOT net disbursement
                    loan.Interest ?? 12,
                    loan.RepayPeriod ?? 12,
                    loan.RepayMethod ?? "AMT");

                var loanbal = new Loanbal
                {
                    LoanNo = loanNo,
                    LoanCode = loan.LoanCode,
                    MemberNo = memberNo,
                    Balance = principalAmount,  
                    IntrOwed = totalInterest,   
                    Installments = loan.RepayPeriod ?? 12,
                    IntrOwed2 = 0,
                    FirstDate = DateTime.Now,
                    RepayRate = monthlyInstallment,
                    RepayRate2 = monthlyInstallment,
                    LastDate = DateTime.Now.AddMonths(loan.RepayPeriod ?? 12),
                    Duedate = DateTime.Now.AddMonths(1),
                    IntrCharged = totalInterest,  
                    Interest = loan.Interest ?? 0,
                    Companycode = loan.CompanyCode,
                    Penalty = 0,
                    RepayMethod = loan.RepayMethod ?? "AMT",
                    Cleared = false,
                    AutoCalc = true,
                    IntrAmount = totalInterest,  
                    RepayPeriod = loan.RepayPeriod ?? 12,
                    Remarks = $"Self-service withdrawal via {withdrawalMethod} - Processing fee: {processingFeePercentage}% ({processingFeeAmount:C})",
                    AuditId = memberNo,
                    AuditTime = DateTime.Now,
                    IntBalance = totalInterest,  
                    CategoryCode = null,
                    InterestAccrued = 0,
                    Defaulter = "N",
                    Processdate = DateTime.Now,
                    Receiptno = null,
                    Cease = "N",
                    Nextduedate = DateTime.Now.AddMonths(1),
                    TransactionNo = transactionNo,
                    Year = DateTime.Now.Year.ToString(),
                    Month = DateTime.Now.Month.ToString(),
                    RepayMode = 1,
                    Gperiod = null,
                    ApiKey = null,
                    UserName = memberNo,
                    Run = 0,
                    SerialNo = null,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Loanbal.Add(loanbal);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Loan balance record created: Principal={principalAmount:C}, Monthly Installment={monthlyInstallment:C}, Total Interest={totalInterest:C}");

                // ============================================================
                // 7. GENERATE REPAYMENT SCHEDULE
                // ============================================================
                await GenerateLoanScheduleAsync(loanNo, principalAmount, loan.Interest ?? 12, loan.RepayPeriod ?? 12, loan.RepayMethod ?? "AMT");

                // ============================================================
                // 8. CREATE BLOCKCHAIN TRANSACTION DATA FOR WITHDRAWAL
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "LOAN_WITHDRAWAL",
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    MemberName = memberName,
                    MemberPhone = member?.PhoneNo ?? member?.MobileNo,
                    LoanCode = loan.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    PrincipalAmount = principalAmount,
                    TotalInterest = totalInterest,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    WithdrawalMethod = withdrawalMethod,
                    PhoneNumber = withdrawalMethod == "MPESA" ? memberPhone : null,
                    DisbursementDate = DateTime.Now,
                    GLTransactionId = disbursementGL.Id,
                    FeeGLTransactionId = feeGL?.Id,
                    BankAccount = sourceAccount,
                    LoanAccount = loanAssetAccount,
                    FeeAccount = processingFeeAccount,
                    ChequeNo = cheque.ChequeNo,
                    VoucherNo = cheque.Voucherno,
                    TransactionNo = transactionNo,
                    MonthlyInstallment = monthlyInstallment,
                    RepayPeriod = loan.RepayPeriod,
                    InterestRate = loan.Interest,
                    RepayMethod = loan.RepayMethod,
                    PreviousStatus = GetStatusString(previousStatus),
                    NewStatus = "Disbursed",
                    BlockHash = blockHash,
                    PreviousBlockHash = previousHash,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Generate data hash for blockchain
                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                // ============================================================
                // 9. CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_WITHDRAWAL",
                    MemberNo = memberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = netAmount,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Blockchain transaction created for withdrawal: {blockchainTx.TransactionId}");

                // ============================================================
                // 10. UPDATE ALL RECORDS WITH BLOCKCHAIN TRANSACTION ID
                // ============================================================
                loanbal.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                disbursementGL.BlockchainTxId = blockchainTx.TransactionId;
                cheque.BlockchainTxId = blockchainTx.TransactionId;

                if (feeGL != null)
                {
                    feeGL.BlockchainTxId = blockchainTx.TransactionId;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"All records updated with BlockchainTxId: {blockchainTx.TransactionId}");

                // ============================================================
                // 11. CREATE BLOCKCHAIN TRANSACTION FOR LOAN STATUS UPDATE
                // ============================================================
                var statusBlockchainData = new
                {
                    TransactionType = "LOAN_STATUS_UPDATE",
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    PreviousStatus = "Endorsed",
                    NewStatus = "Disbursed",
                    UpdatedBy = memberNo,
                    WithdrawalMethod = withdrawalMethod,
                    WithdrawalBlockchainTxId = blockchainTx.TransactionId,
                    ChequeNo = cheque.ChequeNo,
                    TransactionNo = transactionNo,
                    PrincipalAmount = principalAmount,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string statusDataHash = await GenerateTransactionHashAsync(statusBlockchainData);

                // Create new block for status update
                string statusBlockHash = GenerateBlockHash();
                var statusLastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string statusPreviousHash = statusLastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var statusBlock = new Block
                {
                    BlockHash = statusBlockHash,
                    PreviousHash = statusPreviousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(statusBlock);
                await _context.SaveChangesAsync();

                var statusBlockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_STATUS_UPDATE",
                    MemberNo = memberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = netAmount,
                    Timestamp = DateTime.Now,
                    DataHash = statusDataHash,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(statusBlockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = statusBlock.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(statusBlockchainTx);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Status update blockchain transaction created: {statusBlockchainTx.TransactionId}");

                // ============================================================
                // 12. SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNumber = memberNo,
                    memberName = memberName,
                    memberPhone = member?.PhoneNo ?? member?.MobileNo,
                    loanCode = loan.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    withdrawalDate = DateTime.Now,
                    withdrawalMethod = withdrawalMethod,
                    principalAmount = principalAmount,
                    totalInterest = totalInterest,
                    processingFeePercentage = processingFeePercentage,
                    processingFeeAmount = processingFeeAmount,
                    netAmount = netAmount,
                    loanAssetAccount = loanAssetAccount,
                    bankAccount = sourceAccount,
                    processingFeeAccount = processingFeeAccount,
                    chequeNo = cheque.ChequeNo,
                    voucherNo = cheque.Voucherno,
                    transactionNo = transactionNo,
                    monthlyInstallment = monthlyInstallment,
                    repayPeriod = loan.RepayPeriod,
                    interestRate = loan.Interest,
                    repayMethod = loan.RepayMethod,
                    previousStatus = "Endorsed",
                    newStatus = "Disbursed",
                    mpesaNumber = withdrawalMethod == "MPESA" ? memberPhone : null,
                    withdrawalBlockchainTxId = blockchainTx.TransactionId,
                    statusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    withdrawalBlockHash = blockHash,
                    statusUpdateBlockHash = statusBlockHash
                };

                var withdrawalForAudit = new
                {
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    MemberName = memberName,
                    WithdrawalDate = DateTime.Now,
                    WithdrawalMethod = withdrawalMethod,
                    PrincipalAmount = principalAmount,
                    TotalInterest = totalInterest,
                    ProcessingFeePercentage = processingFeePercentage,
                    ProcessingFeeAmount = processingFeeAmount,
                    NetAmount = netAmount,
                    ChequeNo = cheque.ChequeNo,
                    VoucherNo = cheque.Voucherno,
                    TransactionNo = transactionNo,
                    LoanAssetAccount = loanAssetAccount,
                    BankAccount = sourceAccount,
                    ProcessingFeeAccount = processingFeeAccount,
                    MonthlyInstallment = monthlyInstallment,
                    Status = "Disbursed",
                    BlockchainTxId = blockchainTx.TransactionId,
                    StatusUpdateBlockchainTxId = statusBlockchainTx.TransactionId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = memberNo
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: null,
                    newModel: withdrawalForAudit,
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: memberNo,
                    userName: memberName,
                    companyCode: loan.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                string successMessage = withdrawalMethod == "MPESA"
                    ? $"KES {netAmount:N0} (after {processingFeePercentage}% processing fee = {processingFeeAmount:C}) has been sent to {memberPhone}. Check your M-Pesa."
                    : withdrawalMethod == "FOSA"
                        ? $"KES {netAmount:N0} (after {processingFeePercentage}% processing fee = {processingFeeAmount:C}) has been deposited to your FOSA account."
                        : $"KES {netAmount:N0} (after {processingFeePercentage}% processing fee = {processingFeeAmount:C}) is ready for collection via {withdrawalMethod}.";

                _logger.LogInformation($"=== WITHDRAWAL COMPLETED SUCCESSFULLY ===");
                _logger.LogInformation($"Principal: {principalAmount:C}, Fee: {processingFeePercentage}% = {processingFeeAmount:C}, Net: {netAmount:C}");
                _logger.LogInformation($"Withdrawal Blockchain Tx: {blockchainTx.TransactionId}");
                _logger.LogInformation($"Status Update Blockchain Tx: {statusBlockchainTx.TransactionId}");
                _logger.LogInformation($"Cheque No: {cheque.ChequeNo}");
                _logger.LogInformation($"Transaction No: {transactionNo}");

                return new LoanDisbursementResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Amount = netAmount,
                    Message = successMessage,
                    TransactionReference = transactionNo,
                    BlockchainTxId = blockchainTx.TransactionId,
                    StatusUpdateBlockchainTxId = statusBlockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error withdrawing loan {loanNo}");
                return new LoanDisbursementResultDTO
                {
                    Success = false,
                    Message = $"Withdrawal failed: {ex.Message}"
                };
            }
        }


        private async Task<Bank?> GetDisbursementBankAccountAsync(string companyCode)
        {
            return await _context.Banks
                .FirstOrDefaultAsync(b => b.CompanyCode == companyCode && b.IsActive == true);
        }

        /// <summary>
        /// Get bank GL account number
        /// </summary>
        private async Task<string> GetBankGLAccountAsync(string companyCode)
        {
            var bank = await GetDisbursementBankAccountAsync(companyCode);
            return bank?.GlAccountNo ?? "BANK_DEFAULT_ACCOUNT";
        }

        /// <summary>
        /// Get processing fee GL account - tries multiple sources
        /// </summary>
        private string GetProcessingFeeGLAccount(Loantype loanType)
        {
            // Priority order for processing fee GL account:
            // 1. PremiumAcc from loan type (often used for fees)
            // 2. ContraAccount from loan type
            // 3. Default fee account based on company

            if (!string.IsNullOrEmpty(loanType.PremiumAcc))
            {
                _logger.LogInformation($"Using PremiumAcc for processing fee: {loanType.PremiumAcc}");
                return loanType.PremiumAcc;
            }

            if (!string.IsNullOrEmpty(loanType.ContraAccount))
            {
                _logger.LogInformation($"Using ContraAccount for processing fee: {loanType.ContraAccount}");
                return loanType.ContraAccount;
            }

            // Default - you can configure this based on company
            string defaultFeeAccount = "PROCESSING_FEE_INCOME";
            _logger.LogWarning($"No processing fee GL account found, using default: {defaultFeeAccount}");
            return defaultFeeAccount;
        }

        /// <summary>
        /// Generate unique transaction number
        /// </summary>
        private string GenerateTransactionNumber()
        {
            return $"TXN_{DateTime.Now:yyyyMMddHHmmss}_{new Random().Next(500, 9999)}";
        }

        private string GenerateChequeNumber()
        {
            return $"CHQ{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(500, 9999)}";
        }

        /// <summary>
        /// Generate voucher number
        /// </summary>
        private string GenerateVoucherNumber()
        {
            return $"VCH_{DateTime.Now:yyyyMMddHHmmss}_{new Random().Next(100, 999)}";
        }

        /// <summary>
        /// Format phone number for M-Pesa (254 format)
        /// </summary>
        private string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber)) return phoneNumber;

            phoneNumber = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "");
            if (phoneNumber.StartsWith("0"))
                phoneNumber = "254" + phoneNumber.Substring(1);
            if (!phoneNumber.StartsWith("254") && phoneNumber.Length == 9)
                phoneNumber = "254" + phoneNumber;
            return phoneNumber;
        }


        public async Task<List<LoanSchedule>> GenerateLoanScheduleAsync(string loanNo)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            var loanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

            if (loanbal == null)
            {
                throw new InvalidOperationException($"Loan balance record not found for loan {loanNo}");
            }

            // Get the approved amount from Endmain (endorsement)
            var endmain = await _context.Endmain
                .FirstOrDefaultAsync(e => e.LoanNo == loanNo);

            // Principal amount is the APPROVED amount from appraisal
            decimal principalAmount = endmain?.AmtApproved ?? loan.LoanAmt ?? 0;

            if (principalAmount <= 0)
            {
                throw new InvalidOperationException("Cannot generate schedule. No approved amount found for this loan.");
            }

            _logger.LogInformation($"Generating schedule for loan {loanNo} with approved principal: {principalAmount:C}");
            _logger.LogInformation($"Repayment Method: {loan.RepayMethod}, Interest Rate: {loan.Interest}%, Period: {loan.RepayPeriod} months");

            var existingSchedule = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .ToListAsync();

            if (existingSchedule.Any())
            {
                _context.LoanSchedules.RemoveRange(existingSchedule);
                await _context.SaveChangesAsync();
            }

            var schedules = new List<LoanSchedule>();
            var annualInterestRate = loan.Interest ?? 0;
            var monthlyInterestRate = annualInterestRate / 100 / 12; // Convert percentage to decimal (e.g., 12% = 0.12, then /12 = 0.01)
            var months = loan.RepayPeriod ?? 12;
            var dueDate = loanbal.FirstDate.AddMonths(1);
            var repaymentMethod = loan.RepayMethod ?? "AMT";

            decimal totalInterestForLoan = 0;

            if (repaymentMethod == "STL")
            {
                // STL: Fixed Principal + Interest on ORIGINAL balance (not reducing)
                // In STL, interest is calculated on the original principal amount for each period
                decimal monthlyPrincipal = principalAmount / months;
                decimal monthlyInterest = principalAmount * monthlyInterestRate; // Interest on FULL principal
                decimal totalInstallment = monthlyPrincipal + monthlyInterest;
                totalInterestForLoan = monthlyInterest * months;

                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = monthlyPrincipal,
                        InterestAmount = monthlyInterest,
                        TotalInstallment = totalInstallment,
                        BalancePrincipal = remainingBalance - monthlyPrincipal,
                        BalanceInterest = totalInterestForLoan - (monthlyInterest * i),
                        BalanceTotal = (remainingBalance - monthlyPrincipal) + (totalInterestForLoan - (monthlyInterest * i)),
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = monthlyPrincipal,
                        OutstandingInterest = monthlyInterest,
                        OutstandingTotal = totalInstallment,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = false
                    };

                    schedules.Add(schedule);
                    remainingBalance -= monthlyPrincipal;
                }
            }
            else if (repaymentMethod == "AMT")
            {
                // AMT: Equal Monthly Installments (EMI)
                decimal monthlyPayment;
                if (monthlyInterestRate > 0)
                {
                    decimal factor = (decimal)Math.Pow((double)(1 + monthlyInterestRate), months);
                    monthlyPayment = principalAmount * monthlyInterestRate * factor / (factor - 1);
                }
                else
                {
                    monthlyPayment = principalAmount / months;
                }

                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    decimal interestAmount = remainingBalance * monthlyInterestRate;
                    decimal principalAmountPayment = monthlyPayment - interestAmount;
                    totalInterestForLoan += interestAmount;

                    if (i == months)
                    {
                        principalAmountPayment = remainingBalance;
                        monthlyPayment = principalAmountPayment + interestAmount;
                    }

                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = principalAmountPayment,
                        InterestAmount = interestAmount,
                        TotalInstallment = monthlyPayment,
                        BalancePrincipal = remainingBalance - principalAmountPayment,
                        BalanceInterest = totalInterestForLoan - (interestAmount * i),
                        BalanceTotal = (remainingBalance - principalAmountPayment) + (totalInterestForLoan - (interestAmount * i)),
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = principalAmountPayment,
                        OutstandingInterest = interestAmount,
                        OutstandingTotal = monthlyPayment,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = false
                    };

                    schedules.Add(schedule);
                    remainingBalance -= principalAmountPayment;
                }
            }
            else if (repaymentMethod == "RBAL")
            {
                // RBAL: Interest only minimum, principal flexible
                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    decimal interestAmount = remainingBalance * monthlyInterestRate;
                    totalInterestForLoan += interestAmount;

                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = 0,
                        InterestAmount = interestAmount,
                        TotalInstallment = interestAmount,
                        BalancePrincipal = remainingBalance,
                        BalanceInterest = totalInterestForLoan - (interestAmount * i),
                        BalanceTotal = remainingBalance,
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = 0,
                        OutstandingInterest = interestAmount,
                        OutstandingTotal = interestAmount,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = true,
                        MinimumPayment = interestAmount
                    };

                    schedules.Add(schedule);
                }
            }

            // Update loanbal with correct total interest
            loanbal.IntrOwed = totalInterestForLoan;
            loanbal.IntBalance = totalInterestForLoan;
            await _context.SaveChangesAsync();

            await _context.LoanSchedules.AddRangeAsync(schedules);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Generated {schedules.Count} schedule entries for loan {loanNo}. Total Interest: {totalInterestForLoan:C}");

            return schedules;
        }

        private async Task GenerateLoanScheduleAsync(string loanNo, decimal principal, decimal interestRate, int months, string repayMethod)
        {
            try
            {
                // Get the loan to retrieve CompanyCode
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null)
                {
                    _logger.LogError($"Loan {loanNo} not found for generating schedule");
                    return;
                }

                var calculation = LoanCalculationHelper.CalculateLoan(principal, interestRate, months, repayMethod);

                decimal runningPrincipalBalance = principal;
                decimal runningInterestBalance = 0;
                decimal runningTotalBalance = principal;

                foreach (var installment in calculation.Schedule)
                {
                    // Calculate running balances
                    decimal principalForInstallment = Math.Round(installment.PrincipalPayment, 2);
                    decimal interestForInstallment = Math.Round(installment.InterestPayment, 2);
                    decimal totalForInstallment = Math.Round(installment.TotalPayment, 2);

                    // Update running balances BEFORE this installment is paid
                    decimal balancePrincipalBefore = runningPrincipalBalance;
                    decimal balanceInterestBefore = runningInterestBalance;
                    decimal balanceTotalBefore = runningTotalBalance;

                    // After payment, remaining balances
                    runningPrincipalBalance = Math.Round(installment.RemainingBalance, 2);
                    runningInterestBalance = 0; // Interest is fully paid each installment in standard loan
                    runningTotalBalance = runningPrincipalBalance;

                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = installment.InstallmentNo,
                        DueDate = DateTime.Now.AddMonths(installment.InstallmentNo),

                        // Original amounts for this installment
                        PrincipalAmount = principalForInstallment,
                        InterestAmount = interestForInstallment,
                        TotalInstallment = totalForInstallment,

                        // Balance BEFORE payment (what was outstanding before this installment)
                        BalancePrincipal = balancePrincipalBefore,
                        BalanceInterest = balanceInterestBefore,
                        BalanceTotal = balanceTotalBefore,

                        // Amounts paid (initially 0, updated when payment is made)
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,

                        // Outstanding AFTER this installment (what remains if not paid)
                        OutstandingPrincipal = runningPrincipalBalance,
                        OutstandingInterest = runningInterestBalance,
                        OutstandingTotal = runningTotalBalance,

                        // Penalty (initially 0, updated when overdue)
                        PenaltyAmount = 0,

                        // Status and tracking
                        Status = "Pending",
                        PaidDate = null,
                        MinimumPayment = totalForInstallment,
                        IsFlexible = false,
                        PaymentReference = null,
                        BlockchainTxId = null,
                        DaysOverdue = 0
                    };

                    _context.LoanSchedules.Add(schedule);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Repayment schedule generated for loan {loanNo} with {calculation.Schedule.Count} installments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating repayment schedule for loan {loanNo}");
                throw;
            }
        }


        ///// <summary>
        ///// No round off
        ///// </summary>
        //private async Task GenerateLoanScheduleAsync(string loanNo, decimal principal, decimal interestRate, int months, string repayMethod)
        //{
        //    try
        //    {
        //        var loan = await _context.Loans
        //            .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

        //        if (loan == null)
        //        {
        //            _logger.LogError($"Loan {loanNo} not found for generating schedule");
        //            return;
        //        }

        //        var calculation = LoanCalculationHelper.CalculateLoan(principal, interestRate, months, repayMethod);

        //        decimal remainingPrincipal = principal;

        //        for (int i = 0; i < calculation.Schedule.Count; i++)
        //        {
        //            var installment = calculation.Schedule[i];

        //            decimal principalForInstallment = Math.Round(installment.PrincipalPayment, 2);
        //            decimal interestForInstallment = Math.Round(installment.InterestPayment, 2);
        //            decimal totalForInstallment = Math.Round(installment.TotalPayment, 2);

        //            // Calculate remaining principal after this payment
        //            decimal remainingAfterPayment = i < calculation.Schedule.Count - 1
        //                ? Math.Round(calculation.Schedule[i + 1].RemainingBalance, 2)
        //                : 0;

        //            var schedule = new LoanSchedule
        //            {
        //                // Required identifiers
        //                LoanNo = loanNo,
        //                CompanyCode = loan.CompanyCode,
        //                InstallmentNo = installment.InstallmentNo,
        //                DueDate = DateTime.Now.AddMonths(installment.InstallmentNo),

        //                // Payment amounts for this installment
        //                PrincipalAmount = principalForInstallment,
        //                InterestAmount = interestForInstallment,
        //                TotalInstallment = totalForInstallment,

        //                // Balances before this installment
        //                BalancePrincipal = remainingPrincipal,
        //                BalanceInterest = interestForInstallment,
        //                BalanceTotal = remainingPrincipal + interestForInstallment,

        //                // Paid amounts (initial - to be updated on payment)
        //                PaidPrincipal = 0,
        //                PaidInterest = 0,
        //                PaidTotal = 0,

        //                // Outstanding after this installment
        //                OutstandingPrincipal = remainingAfterPayment,
        //                OutstandingInterest = 0,  // Interest is fully paid each installment
        //                OutstandingTotal = remainingAfterPayment,

        //                // Penalty (to be updated if overdue)
        //                PenaltyAmount = 0,

        //                // Status tracking
        //                Status = "Pending",
        //                PaidDate = null,
        //                MinimumPayment = totalForInstallment,
        //                IsFlexible = false,
        //                PaymentReference = null,
        //                BlockchainTxId = null,
        //                DaysOverdue = 0
        //            };

        //            _context.LoanSchedules.Add(schedule);

        //            // Update remaining principal for next iteration
        //            remainingPrincipal = remainingAfterPayment;
        //        }

        //        await _context.SaveChangesAsync();
        //        _logger.LogInformation($"Repayment schedule generated for loan {loanNo} with {calculation.Schedule.Count} installments");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error generating repayment schedule for loan {loanNo}");
        //        throw;
        //    }
        //}


        /// <summary>
        /// Convert number to words (for cheque)
        /// </summary>
        private string NumberToWords(decimal number)
        {
            if (number == 0) return "Zero";

            long integerPart = (long)number;
            int fractionalPart = (int)((number - integerPart) * 100);

            string words = ConvertIntegerToWords(integerPart);

            if (fractionalPart > 0)
            {
                words += $" and {fractionalPart}/100";
            }

            return words + " only";
        }

        private string ConvertIntegerToWords(long number)
        {
            if (number == 0) return "Zero";

            string[] units = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
            string[] teens = { "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            string[] thousands = { "", "Thousand", "Million", "Billion" };

            string words = "";
            int i = 0;

            while (number > 0)
            {
                int chunk = (int)(number % 500);
                if (chunk > 0)
                {
                    string chunkWords = "";

                    int hundreds = chunk / 100;
                    int tensUnits = chunk % 100;

                    if (hundreds > 0)
                    {
                        chunkWords += units[hundreds] + " Hundred ";
                    }

                    if (tensUnits >= 10 && tensUnits <= 19)
                    {
                        chunkWords += teens[tensUnits - 10] + " ";
                    }
                    else
                    {
                        int ten = tensUnits / 10;
                        int unit = tensUnits % 10;

                        if (ten > 0)
                        {
                            chunkWords += tens[ten] + " ";
                        }
                        if (unit > 0)
                        {
                            chunkWords += units[unit] + " ";
                        }
                    }

                    words = chunkWords + thousands[i] + " " + words;
                }
                number /= 500;
                i++;
            }

            return words.Trim();
        }

        /// <summary>
        /// Generate transaction hash for blockchain
        /// </summary>
        private async Task<string> GenerateTransactionHashAsync(object data)
        {
            try
            {
                var jsonData = System.Text.Json.JsonSerializer.Serialize(data);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonData);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hash).ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transaction hash");
                return Guid.NewGuid().ToString().Replace("-", "");
            }
        }

        #endregion

        #endregion

        #region Loan Status & Queries

        public async Task<LoanApplicationResultDTO> GetLoanStatusAsync(string loanNo, string memberNo)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return new LoanApplicationResultDTO
                    {
                        Success = false,
                        Message = "Loan not found"
                    };
                }

                int status = loan.Status ?? 1;
                bool canWithdraw = status == 5; // Endorsed
                bool isMobileLoan = false;

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode);
                if (loanType != null)
                {
                    isMobileLoan = loanType.MobileLoan == true;
                }

                return new LoanApplicationResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Status = GetStatusString(loan.Status),
                    IsMobileLoan = isMobileLoan,
                    CanWithdrawNow = canWithdraw,
                    Message = GetStatusMessage(GetStatusString(loan.Status), isMobileLoan, canWithdraw),
                    Amount = loan.LoanAmt ?? 0,
                    ApplicationDate = loan.ApplicDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan status for {loanNo}");
                throw;
            }
        }

        public async Task<List<MemberLoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"GetMemberLoansAsync: MemberNo={memberNo}, CompanyCode={companyCode}");

                // First, check if there are any loans for this member (ignoring company code)
                var allLoansForMember = await _context.Loans
                    .Where(l => l.MemberNo == memberNo)
                    .ToListAsync();

                _logger.LogInformation($"Total loans found for member {memberNo} (ignoring company): {allLoansForMember.Count}");

                // Log what company codes exist in the loans
                foreach (var l in allLoansForMember)
                {
                    _logger.LogInformation($"Loan {l.LoanNo}: CompanyCode={l.CompanyCode}, Status={l.Status}");
                }

                // Option 1: Use the actual company code from the loan (ignore the passed company code)
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo)  // Only filter by member number
                    .OrderByDescending(l => l.ApplicDate)
                    .Select(l => new MemberLoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        LoanType = l.LoanCode ?? "Unknown",
                        PrincipalAmount = l.LoanAmt ?? 0,
                        Status = GetStatusString(l.Status),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = l.AuditDateTime,
                        OutstandingBalance = 0,
                        NextPaymentDate = null,
                        MonthlyInstallment = l.Repayrate ?? 0
                    })
                    .ToListAsync();

                // Get loan balances
                foreach (var loan in loans)
                {
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo);
                    if (loanbal != null)
                    {
                        loan.OutstandingBalance = loanbal.Balance;
                        loan.NextPaymentDate = loanbal.Nextduedate;
                    }
                }

                _logger.LogInformation($"Returning {loans.Count} loans for member {memberNo}");

                return loans;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting member loans for {memberNo}");
                return new List<MemberLoanSummaryDTO>();
            }
        }

        public async Task<List<LoanScheduleDTO>> GetLoanRepaymentScheduleAsync(string loanNo, string memberNo)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return new List<LoanScheduleDTO>();
                }

                // Get loan type for interest rate and repayment method
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode);

                decimal interestRate = loan.Interest ?? 12;
                string repayMethod = loan.RepayMethod ?? loanType?.Repaymethod ?? "AMT";
                int months = loan.RepayPeriod ?? 12;
                decimal principal = loan.LoanAmt ?? 0;

                // Generate schedule using the helper
                var calculation = LoanCalculationHelper.CalculateLoan(principal, interestRate, months, repayMethod);

                var schedule = calculation.Schedule.Select(s => new LoanScheduleDTO
                {
                    InstallmentNo = s.InstallmentNo,
                    DueDate = loan.ApplicDate.AddMonths(s.InstallmentNo),
                    PrincipalAmount = s.PrincipalPayment,
                    InterestAmount = s.InterestPayment,
                    TotalInstallment = s.TotalPayment,
                    OutstandingAmount = s.RemainingBalance,
                    Status = s.RemainingBalance <= 0 ? "Paid" : "Pending"
                }).ToList();

                return schedule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting repayment schedule for loan {loanNo}");
                return new List<LoanScheduleDTO>();
            }
        }
        
        public async Task<LoanEligibilityDTO> CheckMobileLoanEligibilityAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Checking mobile loan eligibility for member {memberNo}");

                var mobileLoanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.CompanyCode == companyCode && lt.MobileLoan == true);

                if (mobileLoanType == null)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Mobile loan product is not available.",
                        MinAmount = 500,
                        IsMobileLoan = true
                    };
                }

                // Get mobile loan specific settings
                string repayMethod = mobileLoanType.Repaymethod ?? "AMT";
                decimal? processingFee = mobileLoanType.Processingfee;

                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                decimal multiplier = 3m;
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }

                decimal eligibleAmount = totalDeposits * multiplier;
                decimal maxAmount = mobileLoanType.MaxAmount ?? eligibleAmount;
                decimal minAmount = 500;
                bool isEligible = totalDeposits > 0 && eligibleAmount >= minAmount;

                decimal interestRate = 12;
                if (!string.IsNullOrEmpty(mobileLoanType.Interest))
                {
                    string interestStr = mobileLoanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                int defaultRepayPeriod = mobileLoanType.RepayPeriod ?? 12;
                decimal eligibleAmountForCalc = Math.Min(eligibleAmount, maxAmount);

                var calculation = LoanCalculationHelper.CalculateLoan(
                    eligibleAmountForCalc,
                    interestRate,
                    defaultRepayPeriod,
                    repayMethod,
                    processingFee,
                    true);

                return new LoanEligibilityDTO
                {
                    IsEligible = isEligible,
                    Message = isEligible
                        ? $"You are eligible for a Mobile Loan up to {eligibleAmountForCalc:C}! " +
                          $"Using {repayMethod} calculation. Monthly: {calculation.MonthlyInstallment:C}"
                        : "You are not eligible for a mobile loan at this time.",
                    EligibleAmount = eligibleAmountForCalc,
                    MaxAmount = maxAmount,
                    MinAmount = minAmount,
                    CurrentDeposits = totalDeposits,
                    Multiplier = multiplier,
                    IsMobileLoan = true,
                    InterestRate = interestRate,
                    RepaymentPeriodMonths = defaultRepayPeriod,
                    RepayMethod = repayMethod,
                    ProcessingFee = processingFee ?? 0,
                    EstimatedMonthlyInstallment = calculation.MonthlyInstallment,
                    TotalInterest = calculation.TotalInterest,
                    NetDisbursement = calculation.NetDisbursement
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking mobile loan eligibility for member {memberNo}");
                throw;
            }
        }

        #endregion


        #region Loan Repayment - Complete Implementation
        public async Task<LoanRepaymentViewModel> GetRepaymentDetailsAsync(string loanNo, string memberNo)
        {
            try
            {
                _logger.LogInformation($"Getting repayment details for loan {loanNo}, member {memberNo}");

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null) return null;

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode);

                // Get the current pending installment from schedule
                var currentSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo && (s.Status == "Pending" || s.Status == "Partial"))
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                // Get loan balance
                var loanBalance = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                // Calculate outstanding amounts from schedule if available
                decimal outstandingPrincipal = 0;
                decimal outstandingInterest = 0;
                decimal installmentInterest = 0;  
                DateTime? nextDueDate = null;
                int daysOverdue = 0;
                decimal penaltyAmount = 0;

                if (currentSchedule != null)
                {
                    // FIXED: Use OutstandingPrincipal if set, otherwise calculate from remaining balance
                    outstandingPrincipal = currentSchedule.OutstandingPrincipal > 0
                        ? currentSchedule.OutstandingPrincipal
                        : currentSchedule.OutstandingTotal;  // Fallback

                    // FIXED: For interest, use InterestAmount from the schedule (what's owed for this installment)
                    // NOT OutstandingInterest which is never set
                    installmentInterest = currentSchedule.InterestAmount;
                    outstandingInterest = installmentInterest;  // Use the installment's interest amount

                    nextDueDate = currentSchedule.DueDate;

                    _logger.LogInformation($"Schedule #{currentSchedule.InstallmentNo}: Principal={outstandingPrincipal:C}, Interest={installmentInterest:C}, Total={currentSchedule.TotalInstallment:C}");

                    // Calculate days overdue
                    if (currentSchedule.DueDate < DateTime.Now.Date && currentSchedule.Status != "Paid")
                    {
                        daysOverdue = (DateTime.Now.Date - currentSchedule.DueDate.Date).Days;

                        // Calculate penalty if applicable
                        if (loanType != null && daysOverdue > (loanType.GracePeriod))
                        {
                            var penaltyConfig = await _context.Penalties
                                .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == loan.CompanyCode);

                            if (penaltyConfig != null)
                            {
                                penaltyAmount = CalculatePenalty(
                                    currentSchedule.TotalInstallment,
                                    daysOverdue - (loanType.GracePeriod),
                                    penaltyConfig);
                            }
                        }
                    }
                }
                else if (loanBalance != null)
                {
                    // Fallback to loan balance
                    outstandingPrincipal = loanBalance.Balance;
                    outstandingInterest = loanBalance.IntrOwed;
                    nextDueDate = loanBalance.Nextduedate;

                    if (nextDueDate.HasValue && nextDueDate.Value < DateTime.Now.Date)
                    {
                        daysOverdue = (DateTime.Now.Date - nextDueDate.Value.Date).Days;

                        if (loanType != null && daysOverdue > (loanType.GracePeriod))
                        {
                            var penaltyConfig = await _context.Penalties
                                .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == loan.CompanyCode);

                            if (penaltyConfig != null)
                            {
                                decimal monthlyInstallment = loan.Repayrate ?? (loan.LoanAmt ?? 0) / (loan.RepayPeriod ?? 12);
                                penaltyAmount = CalculatePenalty(monthlyInstallment, daysOverdue - (loanType.GracePeriod), penaltyConfig);
                            }
                        }
                    }
                }

                decimal totalOutstanding = outstandingPrincipal + outstandingInterest + penaltyAmount;

                // Round all amounts to whole numbers for display
                outstandingPrincipal = Math.Round(outstandingPrincipal, 0, MidpointRounding.AwayFromZero);
                outstandingInterest = Math.Round(outstandingInterest, 0, MidpointRounding.AwayFromZero);
                penaltyAmount = Math.Round(penaltyAmount, 0, MidpointRounding.AwayFromZero);
                totalOutstanding = Math.Round(totalOutstanding, 0, MidpointRounding.AwayFromZero);

                // Get repayment schedule for display
                var schedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .OrderBy(s => s.InstallmentNo)
                    .Select(s => new RepaymentSchedulerDTO
                    {
                        InstallmentNo = s.InstallmentNo,
                        DueDate = s.DueDate,
                        PrincipalAmount = Math.Round(s.PrincipalAmount, 0, MidpointRounding.AwayFromZero),
                        InterestAmount = Math.Round(s.InterestAmount, 0, MidpointRounding.AwayFromZero),
                        TotalAmount = Math.Round(s.TotalInstallment, 0, MidpointRounding.AwayFromZero),
                        OutstandingAmount = Math.Round(s.OutstandingTotal, 0, MidpointRounding.AwayFromZero),
                        Status = s.Status ?? "Pending",
                        PaidDate = s.PaidDate
                    })
                    .ToListAsync();

                var viewModel = new LoanRepaymentViewModel
                {
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    MemberName = $"{member?.Surname} {member?.OtherNames}".Trim(),
                    LoanAmount = Math.Round(loan.LoanAmt ?? 0, 0, MidpointRounding.AwayFromZero),
                    OutstandingBalance = outstandingPrincipal,
                    OutstandingInterest = outstandingInterest,  
                    TotalOutstanding = totalOutstanding,
                    MonthlyInstallment = Math.Round(loan.Repayrate ?? 0, 0, MidpointRounding.AwayFromZero),
                    NextPaymentDate = nextDueDate,
                    PaymentAmount = Math.Min(loan.Repayrate ?? totalOutstanding, totalOutstanding),
                    PaymentMethods = GetPaymentMethods(),
                    RepaymentSchedule = schedule,
                    PenaltyAmount = penaltyAmount,
                    DaysOverdue = daysOverdue,
                    HasPenalty = penaltyAmount > 0,
                    PenaltyMessage = penaltyAmount > 0 ? $"Penalty of {penaltyAmount:N0} KES applied for {daysOverdue} days overdue" : null
                };

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting repayment details for loan {loanNo}");
                return null;
            }
        }

        /// <summary>
        /// Calculate penalty based on Penalty configuration
        /// </summary>
        private decimal CalculatePenalty(decimal baseAmount, int overdueDays, Penalties penaltyConfig)
        {
            if (overdueDays <= 0) return 0;

            decimal penaltyAmount = 0;

            switch (penaltyConfig.Mode?.ToUpper())
            {
                case "FIXED":
                    // Fixed penalty amount
                    penaltyAmount = penaltyConfig.Value;
                    break;

                case "PERCENTAGE":
                default:
                    // Percentage of overdue amount
                    if (penaltyConfig.Rate?.ToUpper() == "DAILY")
                    {
                        penaltyAmount = (baseAmount * penaltyConfig.Value / 100) * overdueDays;
                    }
                    else // Monthly
                    {
                        int monthsOverdue = (int)Math.Ceiling((double)overdueDays / 30);
                        penaltyAmount = (baseAmount * penaltyConfig.Value / 100) * monthsOverdue;
                    }
                    break;
            }

            // Apply maximum penalty if specified
            if (penaltyConfig.Penalty > 0 && penaltyAmount > penaltyConfig.Penalty)
            {
                penaltyAmount = penaltyConfig.Penalty;
            }

            return penaltyAmount;
        }

        /// <summary>
        /// Get the current installment details from schedule
        /// </summary>
        private async Task<(LoanSchedule schedule, decimal outstandingPrincipal, decimal outstandingInterest, decimal totalDue, bool isOverdue, int daysOverdue)>
            GetCurrentInstallmentDetailsAsync(string loanNo, DateTime currentDate)
        {
            var currentSchedule = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo && (s.Status == "Pending" || s.Status == "Partial"))
                .OrderBy(s => s.InstallmentNo)
                .FirstOrDefaultAsync();

            if (currentSchedule == null)
            {
                return (null, 0, 0, 0, false, 0);
            }

            decimal outstandingPrincipal = currentSchedule.OutstandingPrincipal;
            decimal outstandingInterest = currentSchedule.OutstandingInterest;
            decimal totalDue = outstandingPrincipal + outstandingInterest;

            bool isOverdue = currentSchedule.DueDate < currentDate && currentSchedule.Status != "Paid";
            int daysOverdue = isOverdue ? (currentDate - currentSchedule.DueDate).Days : 0;

            return (currentSchedule, outstandingPrincipal, outstandingInterest, totalDue, isOverdue, daysOverdue);
        }

        /// <summary>
        /// Main repayment method with GL integration
        /// </summary>
        public async Task<RepaymentResultDTO> MakeRepaymentAsync(LoanRepaymentDTO repayment, string memberNo)
        {
            _logger.LogInformation($"=== REPAYMENT REQUEST ===");
            _logger.LogInformation($"LoanNo: {repayment.LoanNo}, Amount: {repayment.Amount}, Method: {repayment.PaymentMethod}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Get loan details
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == repayment.LoanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return new RepaymentResultDTO { Success = false, Message = "Loan not found" };
                }

                // 2. Get loan type for GL accounts and penalty configuration
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                if (loanType == null)
                {
                    return new RepaymentResultDTO { Success = false, Message = "Loan product configuration not found" };
                }

                decimal remainingAmount = repayment.Amount;
                decimal totalPrincipalPaid = 0;
                decimal totalInterestPaid = 0;
                decimal totalPenaltyPaid = 0;
                string lastReceiptNo = null;
                string lastTransactionNo = null;
                BlockchainTransaction lastBlockchainTx = null;

                bool isFullyPaid = false;

                // Loop through pending installments until payment amount is exhausted or loan is fully paid
                while (remainingAmount > 0 && !isFullyPaid)
                {
                    // Get current pending installment
                    var currentSchedule = await _context.LoanSchedules
                        .Where(s => s.LoanNo == repayment.LoanNo && (s.Status == "Pending" || s.Status == "Partial"))
                        .OrderBy(s => s.InstallmentNo)
                        .FirstOrDefaultAsync();

                    if (currentSchedule == null)
                    {
                        // No more pending installments - loan is fully paid
                        isFullyPaid = true;
                        break;
                    }

                    // Calculate outstanding amounts for this installment
                    decimal outstandingPrincipal = currentSchedule.OutstandingPrincipal;
                    decimal outstandingInterest = currentSchedule.OutstandingInterest;

                    // If OutstandingPrincipal is 0, use the original principal amount
                    if (outstandingPrincipal <= 0)
                    {
                        outstandingPrincipal = currentSchedule.PrincipalAmount - currentSchedule.PaidPrincipal;
                    }
                    if (outstandingInterest <= 0)
                    {
                        outstandingInterest = currentSchedule.InterestAmount - currentSchedule.PaidInterest;
                    }

                    decimal installmentTotalDue = outstandingPrincipal + outstandingInterest;

                    _logger.LogInformation($"Processing Installment #{currentSchedule.InstallmentNo}: Due={installmentTotalDue:C}, Remaining Payment={remainingAmount:C}");

                    // Calculate penalty if overdue
                    decimal penaltyAmount = 0;
                    bool isOverdue = currentSchedule.DueDate < DateTime.Now.Date && currentSchedule.Status != "Paid";
                    int daysOverdue = isOverdue ? (DateTime.Now.Date - currentSchedule.DueDate.Date).Days : 0;
                    int gracePeriod = loanType.GracePeriod;

                    if (isOverdue && daysOverdue > gracePeriod)
                    {
                        var penaltyConfig = await _context.Penalties
                            .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == loan.CompanyCode);

                        if (penaltyConfig != null)
                        {
                            int overdueDaysForPenalty = daysOverdue - gracePeriod;
                            penaltyAmount = CalculatePenalty(currentSchedule.TotalInstallment, overdueDaysForPenalty, penaltyConfig);
                            _logger.LogInformation($"Penalty for installment #{currentSchedule.InstallmentNo}: {penaltyAmount:C}");
                        }
                    }

                    decimal totalWithPenalty = installmentTotalDue + penaltyAmount;
                    decimal amountForThisInstallment = Math.Min(remainingAmount, totalWithPenalty);

                    // Allocate payment for this installment: Penalty first -> Interest -> Principal
                    decimal penaltyPaid = Math.Min(amountForThisInstallment, penaltyAmount);
                    decimal remainingAfterPenalty = amountForThisInstallment - penaltyPaid;

                    decimal interestPaid = Math.Min(remainingAfterPenalty, outstandingInterest);
                    decimal remainingAfterInterest = remainingAfterPenalty - interestPaid;

                    decimal principalPaid = Math.Min(remainingAfterInterest, outstandingPrincipal);

                    // Round to 2 decimal places
                    penaltyPaid = Math.Round(penaltyPaid, 2, MidpointRounding.AwayFromZero);
                    interestPaid = Math.Round(interestPaid, 2, MidpointRounding.AwayFromZero);
                    principalPaid = Math.Round(principalPaid, 2, MidpointRounding.AwayFromZero);

                    _logger.LogInformation($"Installment #{currentSchedule.InstallmentNo} Allocation: Penalty={penaltyPaid:C}, Interest={interestPaid:C}, Principal={principalPaid:C}");

                    // Update totals
                    totalPrincipalPaid += principalPaid;
                    totalInterestPaid += interestPaid;
                    totalPenaltyPaid += penaltyPaid;

                    // Update the schedule
                    currentSchedule.PaidPrincipal += principalPaid;
                    currentSchedule.PaidInterest += interestPaid;
                    currentSchedule.PaidTotal += amountForThisInstallment;
                    currentSchedule.OutstandingPrincipal -= principalPaid;
                    currentSchedule.OutstandingInterest -= interestPaid;
                    currentSchedule.OutstandingTotal = currentSchedule.OutstandingPrincipal + currentSchedule.OutstandingInterest;

                    // Check if this installment is fully paid
                    if (currentSchedule.OutstandingPrincipal <= 0 && currentSchedule.OutstandingInterest <= 0)
                    {
                        currentSchedule.Status = "Paid";
                        currentSchedule.PaidDate = DateTime.Now;
                        _logger.LogInformation($"Installment #{currentSchedule.InstallmentNo} fully paid");
                    }
                    else
                    {
                        currentSchedule.Status = "Partial";
                        _logger.LogInformation($"Installment #{currentSchedule.InstallmentNo} partially paid");
                    }

                    remainingAmount -= amountForThisInstallment;

                    // Check if loan is fully paid (no more pending installments)
                    var anyPending = await _context.LoanSchedules
                        .AnyAsync(s => s.LoanNo == repayment.LoanNo && (s.Status == "Pending" || s.Status == "Partial"));

                    if (!anyPending)
                    {
                        isFullyPaid = true;
                        loan.Status = 7; // Closed
                        loan.Posted = "CLOSED";
                        _logger.LogInformation($"Loan {repayment.LoanNo} is now fully paid!");
                    }
                }

                if (totalPrincipalPaid == 0 && totalInterestPaid == 0 && totalPenaltyPaid == 0)
                {
                    return new RepaymentResultDTO { Success = false, Message = "No payment was applied" };
                }

                // Update LoanBal table
                var loanBalance = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == repayment.LoanNo);

                if (loanBalance == null)
                {
                    loanBalance = new Loanbal
                    {
                        LoanNo = repayment.LoanNo,
                        LoanCode = loan.LoanCode,
                        MemberNo = memberNo,
                        Balance = loan.LoanAmt ?? 0,
                        IntrOwed = 0,
                        Installments = loan.RepayPeriod ?? 12,
                        FirstDate = DateTime.Now,
                        Companycode = loan.CompanyCode,
                        Interest = loan.Interest ?? 0,
                        RepayMethod = loan.RepayMethod ?? "AMT",
                        Cleared = false,
                        AutoCalc = true,
                        RepayPeriod = loan.RepayPeriod ?? 12,
                        AuditId = memberNo,
                        AuditTime = DateTime.Now,
                        Nextduedate = DateTime.Now.AddMonths(1),
                        TransactionNo = GenerateTransactionNumber(),
                        Year = DateTime.Now.Year.ToString(),
                        Month = DateTime.Now.Month.ToString(),
                        UserName = memberNo,
                        AuditDateTime = DateTime.Now
                    };
                    _context.Loanbal.Add(loanBalance);
                }

                // Get the next pending installment to set next due date
                var nextSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repayment.LoanNo && (s.Status == "Pending" || s.Status == "Partial"))
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                loanBalance.Balance = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repayment.LoanNo)
                    .SumAsync(s => s.OutstandingPrincipal);
                loanBalance.IntrOwed = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repayment.LoanNo)
                    .SumAsync(s => s.OutstandingInterest);
                loanBalance.Nextduedate = nextSchedule?.DueDate ?? (isFullyPaid ? null : DateTime.Now.AddMonths(1));

                // Create a single repayment record for the total payment
                string receiptNo = GenerateReceiptNumber();
                string transactionNo = GenerateTransactionNumber();

                var repay = new Repay
                {
                    LoanNo = repayment.LoanNo,
                    MemberNo = memberNo,
                    CompanyCode = loan.CompanyCode,
                    SerialNo = receiptNo,
                    DateReceived = DateTime.Now,
                    Amount = repayment.Amount,
                    Principal = totalPrincipalPaid,
                    Interest = totalInterestPaid,
                    Penalty = totalPenaltyPaid,
                    LoanBalance = loanBalance.Balance + loanBalance.IntrOwed,
                    ReceiptNo = receiptNo,
                    Chequeno = repayment.PaymentMethod == "MPESA" ? $"MPESA_{repayment.MpesaPhoneNumber}" : repayment.ChequeNumber,
                    Remarks = repayment.Remarks ?? $"Payment via {repayment.PaymentMethod}" +
                              (repayment.Amount > (totalPrincipalPaid + totalInterestPaid + totalPenaltyPaid)
                                  ? $" (Overpayment of {repayment.Amount - (totalPrincipalPaid + totalInterestPaid + totalPenaltyPaid):C} will be credited to deposit)"
                                  : ""),
                    AuditId = memberNo,
                    AuditTime = DateTime.Now,
                    TransactionNo = transactionNo,
                    TransDate = DateTime.Now,
                    Transby = memberNo,
                    UserName = memberNo,
                    AuditDateTime = DateTime.Now
                };

                _context.Repay.Add(repay);
                await _context.SaveChangesAsync();

                // Handle overpayment (if any)
                if (remainingAmount > 0)
                {
                    _logger.LogInformation($"Overpayment of {remainingAmount:C} will be credited to member's deposit account");
                    // TODO: Credit overpayment to member's deposit account
                    // This would require creating a deposit transaction
                }

                // Create GL transactions
                await CreateRepaymentGLTransactionsAsync(loan, loanType, repayment, totalPrincipalPaid, totalInterestPaid, totalPenaltyPaid, transactionNo);

                // Create blockchain record
                var blockchainTx = await CreateRepaymentBlockchainRecordAsync(
                    repayment.LoanNo, memberNo, loan.CompanyCode,
                    repayment.Amount, totalPrincipalPaid, totalInterestPaid, totalPenaltyPaid,
                    loanBalance.Balance + loanBalance.IntrOwed, transactionNo);

                repay.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation($"=== REPAYMENT COMPLETED SUCCESSFULLY ===");
                _logger.LogInformation($"Receipt: {receiptNo}, Amount: {repayment.Amount:C}, Principal Paid: {totalPrincipalPaid:C}, Interest Paid: {totalInterestPaid:C}");
                _logger.LogInformation($"Blockchain Tx: {blockchainTx.TransactionId}");

                return new RepaymentResultDTO
                {
                    Success = true,
                    Message = $"Payment of {repayment.Amount:N0} KES received successfully. Receipt No: {receiptNo}" +
                              (remainingAmount > 0 ? $" Overpayment of {remainingAmount:N0} KES credited to your deposit account." : ""),
                    ReceiptNo = receiptNo,
                    TransactionReference = transactionNo,
                    PrincipalPaid = totalPrincipalPaid,
                    InterestPaid = totalInterestPaid,
                    PenaltyPaid = totalPenaltyPaid,
                    NewBalance = loanBalance.Balance + loanBalance.IntrOwed,
                    IsFullyPaid = isFullyPaid,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error making repayment for loan {repayment.LoanNo}");
                return new RepaymentResultDTO
                {
                    Success = false,
                    Message = $"Repayment failed: {ex.Message}"
                };
            }
        }


        /// <summary>
        /// Create GL Transactions for Loan Repayment
        /// Similar to AutoEndorseLoanInternalAsync pattern:
        /// - DR Bank Account (Cash/MPESA/Bank)
        /// - CR Loan Asset Account (Principal portion)
        /// - CR Interest Income Account (Interest portion)
        /// - CR Penalty Income Account (Penalty portion)
        /// </summary>
        private async Task CreateRepaymentGLTransactionsAsync(Loan loan, Loantype loanType, LoanRepaymentDTO repayment, decimal principalPaid, decimal interestPaid, decimal penaltyPaid, string transactionNo)
        {
            try
            {
                _logger.LogInformation($"Creating GL Transactions for repayment of loan {loan.LoanNo}");
                _logger.LogInformation($"Principal: {principalPaid:C}, Interest: {interestPaid:C}, Penalty: {penaltyPaid:C}");
                _logger.LogInformation($"Loan Asset Account: {loanType.LoanAcc}");
                _logger.LogInformation($"Interest Account: {loanType.InterestAcc}");
                _logger.LogInformation($"Penalty Account: {loanType.PenaltyAcc}");

                // Get the bank/cash account for receiving payments
                string bankAccountNo = await GetBankGLAccountAsync(loan.CompanyCode);
                _logger.LogInformation($"Bank/Cash Account: {bankAccountNo}");

                string voucherNo = GenerateVoucherNumber();

                // Transaction 1: DR Bank Account, CR Loan Asset Account (Principal portion)
                if (principalPaid > 0 && !string.IsNullOrEmpty(loanType.LoanAcc))
                {
                    var principalGL = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = principalPaid,
                        DrAccNo = bankAccountNo,           // Debit bank/cash account
                        CrAccNo = loanType.LoanAcc,        // Credit loan asset account
                        Temp = "LOAN_REPAYMENT_PRINCIPAL",
                        DocumentNo = voucherNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = loan.CompanyCode,
                        TransDescript = $"Loan Principal Repayment - {loan.LoanNo} - Installment payment",
                        AuditTime = DateTime.Now,
                        AuditId = repayment.MpesaPhoneNumber ?? repayment.ChequeNumber ?? "SYSTEM",
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repayment.PaymentMethod == "MPESA" ? $"MPESA_{repayment.MpesaPhoneNumber}" : repayment.ChequeNumber,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };
                    _context.Gltransactions.Add(principalGL);
                    _logger.LogInformation($"Principal GL: DR {bankAccountNo}, CR {loanType.LoanAcc} = {principalPaid:C}");
                }

                // Transaction 2: DR Bank Account, CR Interest Income Account (Interest portion)
                if (interestPaid > 0 && !string.IsNullOrEmpty(loanType.InterestAcc))
                {
                    var interestGL = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = interestPaid,
                        DrAccNo = bankAccountNo,           // Debit bank/cash account
                        CrAccNo = loanType.InterestAcc,    // Credit interest income account
                        Temp = "LOAN_REPAYMENT_INTEREST",
                        DocumentNo = voucherNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = loan.CompanyCode,
                        TransDescript = $"Loan Interest Repayment - {loan.LoanNo} - Interest on loan",
                        AuditTime = DateTime.Now,
                        AuditId = repayment.MpesaPhoneNumber ?? repayment.ChequeNumber ?? "SYSTEM",
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repayment.PaymentMethod == "MPESA" ? $"MPESA_{repayment.MpesaPhoneNumber}" : repayment.ChequeNumber,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };
                    _context.Gltransactions.Add(interestGL);
                    _logger.LogInformation($"Interest GL: DR {bankAccountNo}, CR {loanType.InterestAcc} = {interestPaid:C}");
                }

                // Transaction 3: DR Bank Account, CR Penalty Income Account (Penalty portion)
                if (penaltyPaid > 0 && !string.IsNullOrEmpty(loanType.PenaltyAcc))
                {
                    var penaltyGL = new Gltransaction
                    {
                        TransDate = DateTime.Now,
                        Amount = penaltyPaid,
                        DrAccNo = bankAccountNo,           // Debit bank/cash account
                        CrAccNo = loanType.PenaltyAcc,     // Credit penalty income account
                        Temp = "LOAN_REPAYMENT_PENALTY",
                        DocumentNo = voucherNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = loan.CompanyCode,
                        TransDescript = $"Loan Penalty Repayment - {loan.LoanNo} - Overdue penalty",
                        AuditTime = DateTime.Now,
                        AuditId = repayment.MpesaPhoneNumber ?? repayment.ChequeNumber ?? "SYSTEM",
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repayment.PaymentMethod == "MPESA" ? $"MPESA_{repayment.MpesaPhoneNumber}" : repayment.ChequeNumber,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };
                    _context.Gltransactions.Add(penaltyGL);
                    _logger.LogInformation($"Penalty GL: DR {bankAccountNo}, CR {loanType.PenaltyAcc} = {penaltyPaid:C}");
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"GL Transactions created successfully for loan {loan.LoanNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating GL transactions for repayment of loan {loan.LoanNo}");
                throw; // Re-throw to let outer transaction handle rollback
            }
        }

        /// <summary>
        /// Create blockchain record for repayment
        /// </summary>
        private async Task<BlockchainTransaction> CreateRepaymentBlockchainRecordAsync(
            string loanNo, string memberNo, string companyCode,
            decimal amount, decimal principalPaid, decimal interestPaid, decimal penaltyPaid,
            decimal newBalance, string transactionNo)
        {
            // Create block
            string blockHash = GenerateBlockHash();
            var lastBlock = await _context.Blocks
                .OrderByDescending(b => b.BlockId)
                .FirstOrDefaultAsync();
            string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

            var block = new Block
            {
                BlockHash = blockHash,
                PreviousHash = previousHash,
                Timestamp = DateTime.Now,
                Nonce = 0,
                MerkleRoot = Guid.NewGuid().ToString(),
                Confirmed = true,
                CreatedAt = DateTime.Now
            };

            _context.Blocks.Add(block);
            await _context.SaveChangesAsync();

            // Create blockchain data
            var blockchainData = new
            {
                TransactionType = "LOAN_REPAYMENT",
                LoanNo = loanNo,
                MemberNo = memberNo,
                PaymentAmount = amount,
                PrincipalPaid = principalPaid,
                InterestPaid = interestPaid,
                PenaltyPaid = penaltyPaid,
                NewBalance = newBalance,
                TransactionNo = transactionNo,
                PaymentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                BlockHash = blockHash,
                PreviousBlockHash = previousHash
            };

            string dataHash = await GenerateTransactionHashAsync(blockchainData);

            var blockchainTx = new BlockchainTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                TransactionType = "LOAN_REPAYMENT",
                MemberNo = memberNo,
                CompanyCode = companyCode,
                Amount = amount,
                Timestamp = DateTime.Now,
                DataHash = dataHash,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                OffChainReferenceId = loanNo,
                Status = "CONFIRMED",
                BlockHash = block.BlockHash,
                CreatedAt = DateTime.Now
            };

            _context.BlockchainTransactions.Add(blockchainTx);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Blockchain transaction created for repayment: {blockchainTx.TransactionId}");

            return blockchainTx;
        }

        /// <summary>
        /// Get repayment history for a loan
        /// </summary>
        public async Task<List<RepaymentHistoryDTO>> GetRepaymentHistoryAsync(string loanNo, string memberNo)
        {
            try
            {
                _logger.LogInformation($"Getting repayment history for loan {loanNo}, member {memberNo}");

                // First, check if any repayments exist for this loan
                var repaymentCount = await _context.Repay
                    .Where(r => r.LoanNo == loanNo && r.MemberNo == memberNo)
                    .CountAsync();

                _logger.LogInformation($"Found {repaymentCount} repayments for loan {loanNo}");

                if (repaymentCount == 0)
                {
                    // Try without member filter
                    var countWithoutMember = await _context.Repay
                        .Where(r => r.LoanNo == loanNo)
                        .CountAsync();

                    _logger.LogInformation($"Found {countWithoutMember} repayments without member filter");

                    if (countWithoutMember > 0)
                    {
                        _logger.LogWarning($"Repayments exist but MemberNo doesn't match. Expected: {memberNo}");
                    }
                }

                var repayments = await _context.Repay
                    .Where(r => r.LoanNo == loanNo && r.MemberNo == memberNo)
                    .OrderByDescending(r => r.DateReceived)
                    .Select(r => new RepaymentHistoryDTO
                    {
                        Id = r.Id,
                        ReceiptNo = r.ReceiptNo ?? "N/A",
                        PaymentDate = r.DateReceived ?? DateTime.Now,
                        Amount = Math.Round(r.Amount ?? 0, 0, MidpointRounding.AwayFromZero),
                        Principal = Math.Round(r.Principal ?? 0, 0, MidpointRounding.AwayFromZero),
                        Interest = Math.Round(r.Interest ?? 0, 0, MidpointRounding.AwayFromZero),
                        Penalty = Math.Round(r.Penalty ?? 0, 0, MidpointRounding.AwayFromZero),
                        LoanBalance = Math.Round(r.LoanBalance ?? 0, 0, MidpointRounding.AwayFromZero),
                        PaymentMethod = GetPaymentMethodFromCheque(r.Chequeno),
                        ChequeNo = r.Chequeno,
                        Status = "Completed",
                        BlockchainTxId = r.BlockchainTxId
                    })
                    .ToListAsync();

                // Log each repayment found
                foreach (var repayment in repayments)
                {
                    _logger.LogInformation($"Repayment: Receipt={repayment.ReceiptNo}, Amount={repayment.Amount}, Date={repayment.PaymentDate}");
                }

                return repayments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting repayment history for loan {loanNo}");
                return new List<RepaymentHistoryDTO>();
            }
        }

        private string GetPaymentMethodFromCheque(string chequeNo)
        {
            if (string.IsNullOrEmpty(chequeNo)) return "CASH";
            if (chequeNo.StartsWith("MPESA")) return "MPESA";
            return "CHEQUE";
        }

        private List<PaymentMethodDTO> GetPaymentMethods()
        {
            return new List<PaymentMethodDTO>
            {
                new PaymentMethodDTO { Code = "MPESA", Name = "M-Pesa", Icon = "fa-mobile-alt", RequiresPhoneNumber = true, RequiresChequeNumber = false },
                new PaymentMethodDTO { Code = "CASH", Name = "Cash", Icon = "fa-money-bill-wave", RequiresPhoneNumber = false, RequiresChequeNumber = false },
                new PaymentMethodDTO { Code = "CHEQUE", Name = "Cheque", Icon = "fa-file-alt", RequiresPhoneNumber = false, RequiresChequeNumber = true },
                new PaymentMethodDTO { Code = "FOSA", Name = "FOSA Transfer", Icon = "fa-university", RequiresPhoneNumber = false, RequiresChequeNumber = false }
            };
        }

        private string GenerateReceiptNumber()
        {
            string prefix = "RCP";
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string randomPart = new Random().Next(1000, 9999).ToString();
            int count = _context.Repay.Count() + 1;
            string sequence = count.ToString("D6");
            return $"{prefix}{datePart}{randomPart}{sequence}";
        }

        #endregion

        #region Helper Methods

        private string GetStatusString(int? status)
        {
            return status switch
            {
                1 => "Draft",
                2 => "Submitted",
                3 => "UnderAppraisal",
                4 => "Approved",
                5 => "Endorsed",
                6 => "Disbursed",
                7 => "Closed",
                8 => "Defaulted",
                9 => "WrittenOff",
                10 => "Rejected",
                _ => "Unknown"
            };
        }

        private string GetStatusMessage(string status, bool isMobileLoan, bool canWithdraw)
        {
            return status switch
            {
                "Draft" => "Your application is being prepared.",
                "Submitted" => isMobileLoan ? "Your mobile loan is being processed." : "Your application has been submitted and is pending review.",
                "UnderAppraisal" => "Your loan is being appraised.",
                "Approved" => "Your loan has been approved!",
                "Endorsed" => "Your loan is ready for withdrawal!",
                "Disbursed" => "Your loan has been disbursed.",
                "Closed" => "Your loan has been fully repaid.",
                "Rejected" => "Your loan application was not approved.",
                "Defaulted" => "Your loan is in default.",
                _ => "Status unknown."
            };
        }

        #endregion

        #region Loan Calculation Helper (Embedded)
        public static class LoanCalculationHelper
        {
            /// <summary>
            /// Calculate loan details based on repayment method
            /// </summary>
            public static LoanCalculationResult CalculateLoan(
                decimal principal,
                decimal annualInterestRate,
                int months,
                string repayMethod,
                decimal? processingFee = null,
                bool deductProcessingFee = true)
            {
                var result = new LoanCalculationResult
                {
                    RepayMethod = repayMethod?.ToUpper() ?? "AMT"
                };

                // Calculate processing fee
                result.ProcessingFee = processingFee ?? 0;
                result.NetDisbursement = deductProcessingFee
                    ? principal - result.ProcessingFee
                    : principal;

                if (principal <= 0 || months <= 0)
                {
                    result.MonthlyInstallment = 0;
                    result.TotalInterest = 0;
                    result.TotalRepayment = 0;
                    return result;
                }

                decimal monthlyRate = annualInterestRate / 1200m; // annualRate/100/12

                switch (result.RepayMethod)
                {
                    case "STL":
                        CalculateStraightLineLoan(result, principal, monthlyRate, months);
                        break;
                    case "RBAL":
                        CalculateReducingBalanceLoan(result, principal, monthlyRate, months);
                        break;
                    case "AMT":
                    default:
                        CalculateAmortizedLoan(result, principal, monthlyRate, months);
                        break;
                }

                result.TotalRepayment = principal + result.TotalInterest;

                return result;
            }

            /// <summary>
            /// AMT - Amortized Loan (Equal Monthly Payments)
            /// Formula: P * r * (1+r)^n / ((1+r)^n - 1)
            /// </summary>
            private static void CalculateAmortizedLoan(LoanCalculationResult result, decimal principal, decimal monthlyRate, int months)
            {
                if (monthlyRate > 0)
                {
                    double rateDouble = (double)monthlyRate;
                    double factor = Math.Pow(1 + rateDouble, months);
                    decimal monthlyPayment = principal * monthlyRate * (decimal)factor / ((decimal)factor - 1);
                    result.MonthlyInstallment = Math.Round(monthlyPayment, 2);

                    // Calculate total interest
                    result.TotalInterest = Math.Round((result.MonthlyInstallment * months) - principal, 2);

                    // Generate schedule
                    decimal remainingBalance = principal;
                    for (int i = 1; i <= months; i++)
                    {
                        decimal interestPayment = remainingBalance * monthlyRate;
                        decimal principalPayment = result.MonthlyInstallment - interestPayment;

                        if (i == months)
                        {
                            principalPayment = remainingBalance;
                            interestPayment = result.MonthlyInstallment - principalPayment;
                        }

                        remainingBalance -= principalPayment;

                        result.Schedule.Add(new LoanScheduleItem
                        {
                            InstallmentNo = i,
                            PrincipalPayment = Math.Round(principalPayment, 2),
                            InterestPayment = Math.Round(interestPayment, 2),
                            TotalPayment = result.MonthlyInstallment,
                            RemainingBalance = Math.Round(Math.Max(0, remainingBalance), 2)
                        });
                    }
                }
                else
                {
                    // Zero interest
                    result.MonthlyInstallment = Math.Round(principal / months, 2);
                    result.TotalInterest = 0;
                }
            }

            /// <summary>
            /// STL - Straight Line Loan (Equal Principal, Decreasing Payments)
            /// Formula: Principal/n + (Remaining Balance × r)
            /// </summary>
            private static void CalculateStraightLineLoan(LoanCalculationResult result, decimal principal, decimal monthlyRate, int months)
            {
                decimal monthlyPrincipal = principal / months;
                decimal remainingBalance = principal;
                decimal totalInterest = 0;
                decimal firstPayment = 0;
                decimal lastPayment = 0;

                for (int i = 1; i <= months; i++)
                {
                    decimal interestPayment = remainingBalance * monthlyRate;
                    decimal totalPayment = monthlyPrincipal + interestPayment;

                    if (i == 1) firstPayment = totalPayment;
                    if (i == months) lastPayment = totalPayment;

                    totalInterest += interestPayment;
                    remainingBalance -= monthlyPrincipal;

                    result.Schedule.Add(new LoanScheduleItem
                    {
                        InstallmentNo = i,
                        PrincipalPayment = Math.Round(monthlyPrincipal, 2),
                        InterestPayment = Math.Round(interestPayment, 2),
                        TotalPayment = Math.Round(totalPayment, 2),
                        RemainingBalance = Math.Round(Math.Max(0, remainingBalance), 2)
                    });
                }

                // For STL, use average payment as the displayed monthly amount
                result.MonthlyInstallment = Math.Round((firstPayment + lastPayment) / 2, 2);
                result.TotalInterest = Math.Round(totalInterest, 2);
            }

            /// <summary>
            /// RBAL - Reducing Balance Loan (Similar to AMT but interest calculated on reducing balance)
            /// </summary>
            private static void CalculateReducingBalanceLoan(LoanCalculationResult result, decimal principal, decimal monthlyRate, int months)
            {
                // RBAL uses the same calculation as AMT
                CalculateAmortizedLoan(result, principal, monthlyRate, months);
                result.RepayMethod = "RBAL";
            }

            /// <summary>
            /// Calculate monthly installment only (for quick display)
            /// </summary>
            public static decimal CalculateMonthlyInstallment(decimal principal, decimal annualRate, int months, string repayMethod)
            {
                var result = CalculateLoan(principal, annualRate, months, repayMethod);
                return result.MonthlyInstallment;
            }

            /// <summary>
            /// Calculate total interest only
            /// </summary>
            public static decimal CalculateTotalInterest(decimal principal, decimal annualRate, int months, string repayMethod)
            {
                var result = CalculateLoan(principal, annualRate, months, repayMethod);
                return result.TotalInterest;
            }
        }

        #endregion
    }
}