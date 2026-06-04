// Services/SelfServiceLoanService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using System.Security.Claims;

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
        Task<AutoAppraisalResultDTO> AutoAppraiseLoanAsync(string loanNo);
        Task<LoanEligibilityDTO> CheckMobileLoanEligibilityAsync(string memberNo, string companyCode);
        Task<List<LoanScheduleDTO>> GetLoanRepaymentScheduleAsync(string loanNo, string memberNo);
        Task<Dictionary<string, decimal>> GetMemberContributionSummaryAsync(string memberNo, string companyCode);
    }

    public class SelfServiceLoanService : ISelfServiceLoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly AppDbContext _appDbContext;
        private readonly ILogger<SelfServiceLoanService> _logger;
        private readonly ILoanService _loanService;
        private readonly IMemberService _memberService;
        private readonly IBlockchainService _blockchainService;
        private readonly AuditTrailService _auditService;

        public SelfServiceLoanService(
            ApplicationDbContext context,
            AppDbContext appDbContext,
            ILogger<SelfServiceLoanService> logger,
            ILoanService loanService,
            IMemberService memberService,
            IBlockchainService blockchainService,
            AuditTrailService auditService)
        {
            _context = context;
            _appDbContext = appDbContext;
            _logger = logger;
            _loanService = loanService;
            _memberService = memberService;
            _blockchainService = blockchainService;
            _auditService = auditService;
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

                // Get contribution summary
                var contributionSummary = await GetMemberContributionSummaryAsync(memberNo, companyCode);

                // Get active loans
                var activeLoans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                l.Status != (int)Status.Closed && l.Status != (int)Status.Rejected &&
                                l.Status != (int)Status.WrittenOff)
                    .Select(l => new MemberLoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        LoanType = l.LoanCode,
                        PrincipalAmount = l.LoanAmt ?? 0,
                        Status = ((Status)(l.Status ?? 0)).ToString(),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = l.AuditDateTime,
                        OutstandingBalance = 0,
                        NextPaymentDate = null,
                        MonthlyInstallment = 0
                    })
                    .ToListAsync();

                // Get pending guarantor invitations
                var pendingGuarantees = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo && g.CompanyCode == companyCode && g.Transfered == false)
                    .Select(g => new PendingGuaranteeDTO
                    {
                        LoanNo = g.LoanNo,
                        ApplicantName = _context.Loans.Where(l => l.LoanNo == g.LoanNo).Select(l => l.MemberNo).FirstOrDefault() ?? "",
                        LoanAmount = _context.Loans.Where(l => l.LoanNo == g.LoanNo).Select(l => l.LoanAmt ?? 0).FirstOrDefault(),
                        InvitationDate = g.AuditTime ?? DateTime.Now
                    })
                    .ToListAsync();

                // Get loan eligibility
                var loanProducts = await GetAvailableLoanProductsAsync(memberNo, companyCode);

                var dashboard = new MemberDashboardDTO
                {
                    MemberNo = member.MemberNo,
                    MemberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim(),
                    Email = member.Email ?? member.EmailAddress,
                    PhoneNumber = member.PhoneNo ?? member.MobileNo,
                    MemberSince = member.EffectDate ?? member.ApplicDate ?? DateTime.Now,

                    // Contribution Summary
                    TotalDeposits = contributionSummary.GetValueOrDefault("Deposits", 0),
                    TotalShares = contributionSummary.GetValueOrDefault("Shares", 0),
                    TotalSavings = contributionSummary.GetValueOrDefault("Deposits", 0) + contributionSummary.GetValueOrDefault("Shares", 0),

                    // Loan Summary
                    ActiveLoansCount = activeLoans.Count,
                    TotalOutstandingLoans = await CalculateTotalOutstandingAsync(memberNo, companyCode),
                    TotalLoanAmount = await _context.Loans.Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                        .SumAsync(l => l.LoanAmt ?? 0),

                    // Eligibility
                    AvailableLoanProducts = loanProducts,
                    MaxEligibleAmount = loanProducts.Max(lp => lp.MaxEligibleAmount),

                    // Lists
                    ActiveLoans = activeLoans,
                    PendingGuarantorRequests = pendingGuarantees,

                    LastLogin = DateTime.Now
                };

                return dashboard;
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
                // Get deposits from ContribShares (PRIMARY SOURCE)
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                result["Deposits"] = totalDeposits;

                // Get shares from ContribShares (ShareCapitalAmount)
                var totalShareCapital = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                result["Shares"] = totalShareCapital;

                // Get contributions by share type
                var shareTypeGroups = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .GroupBy(cs => cs.Sharescode)
                    .Select(g => new { SharesCode = g.Key, Total = g.Sum(cs => cs.DepositsAmount ?? 0) })
                    .ToListAsync();

                foreach (var group in shareTypeGroups)
                {
                    if (!string.IsNullOrEmpty(group.SharesCode))
                    {
                        var shareType = await _context.Sharetypes
                            .FirstOrDefaultAsync(s => s.SharesCode == group.SharesCode && s.CompanyCode == companyCode);
                        var key = shareType?.SharesType ?? group.SharesCode;
                        result[key] = group.Total;
                    }
                }

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
                                l.Status != (int)Status.Closed && l.Status != (int)Status.Rejected)
                    .ToListAsync();

                decimal totalOutstanding = 0;

                foreach (var loan in loans)
                {
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);
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

                // Get loan type
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
                        MinAmount = 0
                    };
                }

                // Check if this is a mobile loan (auto-approve)
                bool isMobileLoan = loanType.MobileLoan == true;

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Member not found",
                        IsMobileLoan = isMobileLoan
                    };
                }

                // Check member active status
                if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Your account is not active. Please contact the SACCO office.",
                        IsMobileLoan = isMobileLoan
                    };
                }

                // Check blacklist - Member doesn't have Blacklisted property, use Status instead
                // Status 0 typically means inactive/restricted
                if (member.Status == 0)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Your account has been restricted. Please contact the SACCO office.",
                        IsMobileLoan = isMobileLoan
                    };
                }

                // Get member deposits from ContribShares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                // Calculate multiplier - use LoanToShareRatio from Sharetype OR default to 3
                // Get the max LoanToShareRatio from Sharetypes for this company
                decimal multiplier = 3m; // Default multiplier

                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }

                // Calculate eligible amount based on deposits multiplied by ratio
                decimal eligibleAmount = totalDeposits * multiplier;

                // Apply loan type max amount
                decimal maxAmount = loanType.MaxAmount ?? eligibleAmount;

                // Check if member has existing active loans
                var activeLoanExists = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                   (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));

                // Check if member has any defaulted loans
                var hasDefaulted = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                   (l.Status == (int)Status.Defaulted || l.Status == (int)Status.WrittenOff));

                // For mobile loans, check if there's an active mobile loan
                bool hasActiveMobileLoan = false;
                if (isMobileLoan)
                {
                    hasActiveMobileLoan = await _context.Loans
                        .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                       l.LoanCode == loanCode &&
                                       (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));
                }

                // Determine eligibility
                bool isEligible = true;
                List<string> messages = new List<string>();

                if (hasDefaulted)
                {
                    isEligible = false;
                    messages.Add("You have defaulted on a previous loan. Please clear the default first.");
                }

                if (isMobileLoan && hasActiveMobileLoan)
                {
                    isEligible = false;
                    messages.Add("You already have an active mobile loan. Only one mobile loan is allowed at a time.");
                }
                else if (!isMobileLoan && activeLoanExists)
                {
                    // Check if loan type allows multiple loans
                    var maxLoans = loanType.MaxLoans ?? 1;
                    var currentActiveCount = await _context.Loans
                        .CountAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                        (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));

                    if (currentActiveCount >= maxLoans)
                    {
                        isEligible = false;
                        messages.Add($"You already have {currentActiveCount} active loan(s). Maximum allowed is {maxLoans}.");
                    }
                }

                // Check minimum deposit requirement - use a default or get from somewhere
                decimal minDepositRequirement = 0; // Default no minimum deposit requirement
                if (totalDeposits < minDepositRequirement)
                {
                    isEligible = false;
                    messages.Add($"Minimum deposit requirement is {minDepositRequirement:C}. Your current deposits: {totalDeposits:C}");
                }

                // Get interest rate - use Interest property from Loantype (it's a string)
                decimal interestRate = 12; // Default 12%
                if (!string.IsNullOrEmpty(loanType.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                // Get default repayment period
                int defaultRepayPeriod = loanType.RepayPeriod ?? 12;

                string message = isEligible
                    ? $"You are eligible for a {loanType.LoanType1} loan up to {Math.Min(eligibleAmount, maxAmount):C}"
                    : string.Join(" ", messages);

                return new LoanEligibilityDTO
                {
                    IsEligible = isEligible,
                    Message = message,
                    EligibleAmount = Math.Min(eligibleAmount, maxAmount),
                    MaxAmount = maxAmount,
                   // MinAmount = minAmount,
                    CurrentDeposits = totalDeposits,
                    CurrentShares = totalShares,
                    Multiplier = multiplier,
                    IsMobileLoan = isMobileLoan,
                    RequiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                       loanType.Guarantor != "No" &&
                                       loanType.Guarantor != "N" &&
                                       loanType.Guarantor != "0",
                    InterestRate = interestRate,
                    RepaymentPeriodMonths = defaultRepayPeriod,
                    EstimatedMonthlyInstallment = CalculateMonthlyInstallment(Math.Min(eligibleAmount, maxAmount), interestRate, defaultRepayPeriod)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking eligibility for member {memberNo}");
                throw;
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

                // Get the max LoanToShareRatio for multiplier
                decimal maxMultiplier = 3m;
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    maxMultiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }

                foreach (var loanType in loanTypes)
                {
                    // Skip inactive loan types if you have a status field
                    // var eligibility = await CheckLoanEligibilityAsync(memberNo, loanType.LoanCode, companyCode);

                    // Simplified eligibility check
                    bool isMobileLoan = loanType.MobileLoan == true;

                    // Get member deposits
                    var totalDeposits = await _context.ContribShares
                        .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    decimal eligibleAmount = totalDeposits * maxMultiplier;
                    bool isEligible = totalDeposits > 0; // Simplified check

                    // Get interest rate
                    decimal interestRate = 12;
                    if (!string.IsNullOrEmpty(loanType.Interest))
                    {
                        string interestStr = loanType.Interest.ToString().Replace("%", "");
                        if (decimal.TryParse(interestStr, out decimal parsedRate))
                        {
                            interestRate = parsedRate;
                        }
                    }

                    products.Add(new LoanProductDTO
                    {
                        LoanCode = loanType.LoanCode ?? "",
                        LoanName = loanType.LoanType1 ?? loanType.LoanCode ?? "Unknown",
                        Description = $"Apply for {loanType.LoanType1} loan",
                        MinAmount = 1000,
                        MaxAmount = loanType.MaxAmount ?? 1000000,
                        InterestRate = interestRate,
                        RepaymentPeriodMonths = loanType.RepayPeriod ?? 12,
                        Multiplier = maxMultiplier,
                        IsMobileLoan = isMobileLoan,
                        RequiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                           loanType.Guarantor != "No" &&
                                           loanType.Guarantor != "N",
                        IsEligible = isEligible,
                        EligibilityMessage = isEligible ? "You are eligible" : "You need to make deposits first",
                        EligibleAmount = eligibleAmount,
                        EstimatedMonthlyInstallment = CalculateMonthlyInstallment(Math.Min(eligibleAmount, loanType.MaxAmount ?? 1000000), interestRate, loanType.RepayPeriod ?? 12)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan products for member {memberNo}");
            }

            return products;
        }

        private decimal CalculateMonthlyInstallment(decimal principal, decimal annualInterestRate, int months)
        {
            if (principal <= 0 || months <= 0) return 0;

            decimal monthlyRate = (annualInterestRate / 100) / 12;

            if (monthlyRate > 0)
            {
                decimal factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
                return principal * monthlyRate * factor / (factor - 1);
            }

            return principal / months;
        }

        #endregion

        #region Loan Application

        //public async Task<LoanApplicationResultDTO> ApplyForLoanAsync(SelfLoanApplicationDTO application, string memberNo)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        _logger.LogInformation($"Self-service loan application for member {memberNo}, amount: {application.PrincipalAmount:C}, LoanCode: {application.LoanCode}");

        //        // Verify member exists
        //        var member = await _context.Members
        //            .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == application.CompanyCode);

        //        if (member == null)
        //        {
        //            _logger.LogError($"Member not found: {memberNo}, Company: {application.CompanyCode}");
        //            throw new InvalidOperationException($"Member not found with number: {memberNo}");
        //        }

        //        _logger.LogInformation($"Member found: {member.MemberNo}, Name: {member.Surname} {member.OtherNames}");

        //        // Get loan type
        //        var loanType = await _context.Loantypes
        //            .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode && lt.CompanyCode == application.CompanyCode);

        //        if (loanType == null)
        //        {
        //            _logger.LogError($"Loan product not found: {application.LoanCode}, Company: {application.CompanyCode}");
        //            throw new InvalidOperationException($"Loan product '{application.LoanCode}' not found");
        //        }

        //        _logger.LogInformation($"Loan type found: {loanType.LoanCode}, MobileLoan: {loanType.MobileLoan}");

        //        bool isMobileLoan = loanType.MobileLoan == true;

        //        // Check eligibility
        //        var eligibility = await CheckLoanEligibilityAsync(memberNo, application.LoanCode, application.CompanyCode);
        //        _logger.LogInformation($"Eligibility check: IsEligible={eligibility.IsEligible}, EligibleAmount={eligibility.EligibleAmount}");

        //        if (!eligibility.IsEligible)
        //        {
        //            throw new InvalidOperationException(eligibility.Message);
        //        }

        //        // Validate amount against eligibility
        //        if (application.PrincipalAmount > eligibility.EligibleAmount)
        //        {
        //            throw new InvalidOperationException($"Requested amount {application.PrincipalAmount:C} exceeds eligible amount of {eligibility.EligibleAmount:C}");
        //        }

        //        // Validate amount minimum
        //        if (application.PrincipalAmount < 1000) // Default minimum
        //        {
        //            throw new InvalidOperationException($"Minimum loan amount is 1,000");
        //        }

        //        // Validate repayment period
        //        int maxPeriod = loanType.RepayPeriod ?? 360;
        //        if (application.RepayPeriod > maxPeriod)
        //        {
        //            throw new InvalidOperationException($"Maximum repayment period is {maxPeriod} months");
        //        }

        //        if (application.RepayPeriod < 1)
        //        {
        //            throw new InvalidOperationException($"Repayment period must be at least 1 month");
        //        }

        //        // Create the loan application DTO
        //        var loanApplication = new LoanApplicationDTO
        //        {
        //            MemberNo = memberNo,
        //            LoanCode = application.LoanCode,
        //            PrincipalAmount = application.PrincipalAmount,
        //            RepayPeriod = application.RepayPeriod,
        //            CompanyCode = application.CompanyCode,
        //            Purpose = application.Purpose ?? "General purpose",
        //            Remarks = application.Remarks ?? "Applied via self-service portal",
        //            ApplicationDate = DateTime.Now,
        //            CreatedBy = memberNo + " (Self-Service)",
        //            Guarantors = new List<GuarantorAssignmentDTO>()
        //        };

        //        _logger.LogInformation($"Calling LoanService.ApplyForLoanAsync with Principal: {loanApplication.PrincipalAmount}, Period: {loanApplication.RepayPeriod}");

        //        // Call existing LoanService to create the loan
        //        var loan = await _loanService.ApplyForLoanAsync(loanApplication);

        //        if (loan == null)
        //        {
        //            throw new InvalidOperationException("Loan service returned null - loan was not created");
        //        }

        //        _logger.LogInformation($"Loan created successfully: {loan.LoanNo}");

        //        // For mobile loans, auto-approve
        //        if (isMobileLoan)
        //        {
        //            await _loanService.UpdateLoanStatusAsync(loan.LoanNo, "Submitted", memberNo, "Mobile loan - auto submitted");
        //            _logger.LogInformation($"Mobile loan status updated to Submitted for {loan.LoanNo}");
        //        }

        //        // Create blockchain record
        //        var blockchainData = new
        //        {
        //            TransactionType = "SELF_SERVICE_LOAN_APPLICATION",
        //            LoanNo = loan.LoanNo,
        //            MemberNo = memberNo,
        //            MemberName = $"{member.Surname} {member.OtherNames}",
        //            Amount = application.PrincipalAmount,
        //            LoanCode = application.LoanCode,
        //            IsMobileLoan = isMobileLoan,
        //            ApplicationDate = DateTime.Now,
        //            IpAddress = application.IpAddress
        //        };

        //        var blockchainTx = new BlockchainTransaction
        //        {
        //            TransactionId = Guid.NewGuid().ToString(),
        //            TransactionType = "SELF_SERVICE_LOAN_APPLICATION",
        //            MemberNo = memberNo,
        //            CompanyCode = application.CompanyCode,
        //            Amount = application.PrincipalAmount,
        //            Timestamp = DateTime.Now,
        //            DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
        //            PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
        //            OffChainReferenceId = loan.LoanNo,
        //            Status = "CONFIRMED",
        //            CreatedAt = DateTime.Now
        //        };

        //        _context.BlockchainTransactions.Add(blockchainTx);
        //        await _context.SaveChangesAsync();

        //        await transaction.CommitAsync();

        //        return new LoanApplicationResultDTO
        //        {
        //            Success = true,
        //            LoanNo = loan.LoanNo,
        //            Message = isMobileLoan
        //                ? $"Your mobile loan application for {application.PrincipalAmount:C} has been submitted and is ready for withdrawal!"
        //                : $"Your loan application for {application.PrincipalAmount:C} has been submitted successfully. You will be notified once processed.",
        //            Status = isMobileLoan ? "Ready for Withdrawal" : "Submitted",
        //            IsMobileLoan = isMobileLoan,
        //            CanWithdrawNow = isMobileLoan,
        //            BlockchainTxId = blockchainTx.TransactionId
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, $"Error in self-service loan application for member {memberNo}. Error: {ex.Message}");
        //        _logger.LogError($"Stack trace: {ex.StackTrace}");

        //        // Return a result with the error message instead of throwing
        //        return new LoanApplicationResultDTO
        //        {
        //            Success = false,
        //            Message = $"Application failed: {ex.Message}",
        //            IsMobileLoan = false,
        //            CanWithdrawNow = false
        //        };
        //    }
        //}

        public async Task<LoanApplicationResultDTO> ApplyForLoanAsync(SelfLoanApplicationDTO application, string memberNo)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Self-service loan application for member {memberNo}, amount: {application.PrincipalAmount:C}");

                // Verify member exists
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == application.CompanyCode);

                if (member == null)
                {
                    throw new InvalidOperationException("Member not found");
                }

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode && lt.CompanyCode == application.CompanyCode);

                if (loanType == null)
                {
                    throw new InvalidOperationException("Loan product not found");
                }

                bool isMobileLoan = loanType.MobileLoan == true;

                // Check eligibility
                var eligibility = await CheckLoanEligibilityAsync(memberNo, application.LoanCode, application.CompanyCode);

                if (!eligibility.IsEligible)
                {
                    throw new InvalidOperationException(eligibility.Message);
                }

                // Validate amount against eligibility
                if (application.PrincipalAmount > eligibility.EligibleAmount)
                {
                    throw new InvalidOperationException($"Requested amount exceeds eligible amount of {eligibility.EligibleAmount:C}");
                }

                // Validate repayment period
                int maxPeriod = loanType.RepayPeriod?? 360;
                if (application.RepayPeriod > maxPeriod)
                {
                    throw new InvalidOperationException($"Maximum repayment period is {maxPeriod} months");
                }

                // Create the loan application DTO - use GuarantorAssignmentDTO
                var loanApplication = new LoanApplicationDTO
                {
                    MemberNo = memberNo,
                    LoanCode = application.LoanCode,
                    PrincipalAmount = application.PrincipalAmount,
                    RepayPeriod = application.RepayPeriod,
                    CompanyCode = application.CompanyCode,
                    Purpose = application.Purpose,
                    Remarks = application.Remarks,
                    ApplicationDate = DateTime.Now,
                    CreatedBy = memberNo + " (Self-Service)",
                    Guarantors = new List<GuarantorAssignmentDTO>() 
                };

                // Call existing LoanService to create the loan
                var loan = await _loanService.ApplyForLoanAsync(loanApplication);

                // For mobile loans, auto-approve
                string approvalStatus = "Pending";
                if (isMobileLoan)
                {
                    await _loanService.UpdateLoanStatusAsync(loan.LoanNo, "Submitted", memberNo, "Mobile loan - auto submitted");
                    approvalStatus = "Auto-approved (Mobile Loan)";
                }

                // Create blockchain record
                var blockchainData = new
                {
                    TransactionType = "SELF_SERVICE_LOAN_APPLICATION",
                    LoanNo = loan.LoanNo,
                    MemberNo = memberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    Amount = application.PrincipalAmount,
                    LoanCode = application.LoanCode,
                    IsMobileLoan = isMobileLoan,
                    ApplicationDate = DateTime.Now,
                    IpAddress = application.IpAddress
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SELF_SERVICE_LOAN_APPLICATION",
                    MemberNo = memberNo,
                    CompanyCode = application.CompanyCode,
                    Amount = application.PrincipalAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loan.LoanNo,
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LoanApplicationResultDTO
                {
                    Success = true,
                    LoanNo = loan.LoanNo,
                    Message = isMobileLoan
                        ? $"Your mobile loan application for {application.PrincipalAmount:C} has been submitted and is ready for withdrawal!"
                        : $"Your loan application for {application.PrincipalAmount:C} has been submitted successfully. You will be notified once processed.",
                    Status = isMobileLoan ? "Ready for Withdrawal" : "Submitted",
                    IsMobileLoan = isMobileLoan,
                    CanWithdrawNow = isMobileLoan,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in self-service loan application for member {memberNo}");
                throw;
            }
        }

        #endregion

        #region Auto Appraisal
        public async Task<AutoAppraisalResultDTO> AutoAppraiseLoanAsync(string loanNo)
        {
            try
            {
                _logger.LogInformation($"Auto-appraising loan {loanNo}");

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null)
                {
                    return new AutoAppraisalResultDTO
                    {
                        Success = false,
                        Message = "Loan not found"
                    };
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                bool isMobileLoan = loanType?.MobileLoan == true;

                // Get member deposits from ContribShares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == loan.MemberNo && cs.CompanyCode == loan.CompanyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                // FIX: Get multiplier from Sharetype table - NO DEFAULT VALUE
                // Query the database to get the actual LoanToShareRatio from Sharetypes
                decimal multiplier = 0;

                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == loan.CompanyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    // Use the maximum LoanToShareRatio from all share types
                    multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }
                else
                {
                    // If NO share types have LoanToShareRatio configured, member cannot get a loan
                    _logger.LogWarning($"No Sharetype with LoanToShareRatio found for company {loan.CompanyCode}");
                    return new AutoAppraisalResultDTO
                    {
                        Success = false,
                        LoanNo = loanNo,
                        Recommendation = "REJECTED",
                        Message = "Loan configuration error: No share type multiplier configured. Please contact SACCO administrator.",
                        EligibleAmount = 0,
                        RequestedAmount = loan.LoanAmt ?? 0,
                        MonthlyInstallment = 0,
                        TotalInterest = 0
                    };
                }

                // Calculate eligible amount based on deposits × multiplier from database
                decimal eligibleAmount = totalDeposits * multiplier;
                decimal requestedAmount = loan.LoanAmt ?? 0;

                bool isEligible = requestedAmount <= eligibleAmount;
                string recommendation = isEligible ? "APPROVED" : "REJECTED";

                // For mobile loans, always approve if eligible
                if (isMobileLoan && isEligible)
                {
                    recommendation = "APPROVED";
                }

                // Get interest rate from loan type (parse from string)
                decimal interestRate = 0;
                if (loanType != null && !string.IsNullOrEmpty(loanType.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                // Calculate estimated monthly payment
                decimal monthlyPayment = CalculateMonthlyInstallment(
                    requestedAmount,
                    interestRate,
                    loan.RepayPeriod ?? 12);

                // Create appraisal record
                var appraisal = new Appraisal
                {
                    LoanNo = loanNo,
                    CompanyCode = loan.CompanyCode,
                    MemberNo = loan.MemberNo,
                    AppraisDate = DateTime.Now,
                    AuditTime = DateTime.Now,
                    AuditID = "SYSTEM_AUTO",
                    OfficerNames = "Auto-Appraisal System",
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                    Salary = 0,
                    Allowances = 0,
                    Shares = totalDeposits,
                    Loans = 0,
                    Deductions = 0,
                    AmtRecommended = requestedAmount,
                    TotalDeductions = 0,
                    Principal = requestedAmount,
                    Interest = interestRate,
                    TInterest = interestRate,
                    TotalInterest = (monthlyPayment * (loan.RepayPeriod ?? 12)) - requestedAmount,
                    RepayMethod = loan.RepayMethod ?? "AMT",
                    RepayRate = monthlyPayment,
                    Reason = recommendation,
                    NetMonthlySalary = 0,
                    SocietyPayment = monthlyPayment,
                    ExpectedNetSalary = 0,
                    DeductionToGross = 0,
                    TotalDedNewLoanToGross = 0,
                    NetSalaryToGross = 0,
                    TotalLoanToGross = 0,
                    TotalCoopDedToGross = 0,
                    BankLoan = 0,
                    Nssf = 0,
                    CopLoanded = 0,
                    OtherDed = 0,
                    StatutoryDed = 0,
                    StatutoryDedToGross = 0,
                    TotalDedToGrossLessStatutory = 0,
                    NoOfLoans = 0,
                    LoanGuarantor = 0
                };

                _context.Appraisal.Add(appraisal);

                // Update loan status based on recommendation
                if (recommendation == "APPROVED")
                {
                    loan.Status = (int)Status.Approved;
                    loan.Posted = "APPROVED";

                    // If mobile loan, also endorse automatically
                    if (isMobileLoan)
                    {
                        await AutoEndorseLoanAsync(loanNo);
                    }
                }
                else
                {
                    loan.Status = (int)Status.Rejected;
                    loan.Posted = "REJECTED";
                    loan.AddSecurity = $"Auto-rejected: Requested amount {requestedAmount:C} exceeds eligible amount {eligibleAmount:C} (Deposits: {totalDeposits:C} × {multiplier} = {eligibleAmount:C})";
                }

                await _context.SaveChangesAsync();

                return new AutoAppraisalResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Recommendation = recommendation,
                    EligibleAmount = eligibleAmount,
                    RequestedAmount = requestedAmount,
                    MonthlyInstallment = monthlyPayment,
                    TotalInterest = (monthlyPayment * (loan.RepayPeriod ?? 12)) - requestedAmount,
                    Message = recommendation == "APPROVED"
                        ? $"Loan auto-approved. Deposits: {totalDeposits:C} × {multiplier} = {eligibleAmount:C} eligible. Monthly payment: {monthlyPayment:C}"
                        : $"Loan rejected. Requested amount {requestedAmount:C} exceeds eligible amount {eligibleAmount:C} (Deposits: {totalDeposits:C} × {multiplier})"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error auto-appraising loan {loanNo}");
                throw;
            }
        }

        private async Task AutoEndorseLoanAsync(string loanNo)
        {
            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null) return;

                // Get appraisal
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo);

                if (appraisal == null) return;

                decimal approvedAmount = appraisal.AmtRecommended ?? loan.LoanAmt ?? 0;

                // Create endorsement
                var endmain = new Endmain
                {
                    LoanNo = loanNo,
                    CompanyCode = loan.CompanyCode,
                    MinuteNo = Guid.NewGuid().ToString().Substring(0, 10),
                    MeetingDate = DateTime.Now,
                    AmtApproved = approvedAmount,
                    Accepted = "1",
                    ChairSigned = "SYSTEM_AUTO",
                    SecSigned = "SYSTEM_AUTO",
                    MembSigned = loan.MemberNo,
                    Reasons = "Auto-endorsed for mobile loan",
                    Remarks = "Auto-endorsed - Mobile Loan",
                    AuditId = "SYSTEM_AUTO",
                    AuditTime = DateTime.Now,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15)
                };

                _context.Endmain.Add(endmain);
                await _context.SaveChangesAsync();

                // Create cheque record
                var cheque = new Cheque
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = approvedAmount,
                    AmountIssued = approvedAmount,
                    DateIssued = DateTime.Now,
                    Status = "Pending",
                    AuditId = "SYSTEM_AUTO",
                    AuditTime = DateTime.Now,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                    Voucherno = Guid.NewGuid().ToString().Substring(0, 10),
                    Voucheramount = approvedAmount,
                    Paymethod = "AUTO",
                    Amountinword = approvedAmount.ToString(),
                    Refloan = true,
                    Dregard = 0,
                    PaidBf = 0,
                    OrgAmt = approvedAmount,
                    LoanAcc = "LOAN_ASSET_ACCOUNT",
                    ContraAcc = "BANK_ACCOUNT",
                    PremiumAcc = "PREMIUM_ACCOUNT",
                    Offsetamount = 0,
                    IntrOwed = 0
                };

                _context.Cheques.Add(cheque);
                await _context.SaveChangesAsync();

                loan.Status = (int)Status.Endorsed;
                loan.Posted = "ENDORSED";
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Loan {loanNo} auto-endorsed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error auto-endorsing loan {loanNo}");
            }
        }

        #endregion

        #region Loan Withdrawal

        public async Task<LoanDisbursementResultDTO> WithdrawLoanAsync(string loanNo, string memberNo, string withdrawalMethod, string phoneNumber = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Withdrawal request for loan {loanNo} by member {memberNo}, method: {withdrawalMethod}");

                // Get loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    throw new InvalidOperationException("Loan not found");
                }

                // Check if loan is ready for withdrawal (Endorsed status)
                if (loan.Status != (int)Status.Endorsed)
                {
                    string statusName = ((Status)(loan.Status ?? 0)).ToString();
                    throw new InvalidOperationException($"Loan cannot be withdrawn. Current status: {statusName}");
                }

                // Get loan type to verify if mobile loan
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                bool isMobileLoan = loanType?.MobileLoan == true;

                // Get the cheque record
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == loan.CompanyCode);

                if (cheque == null)
                {
                    throw new InvalidOperationException("Disbursement record not found");
                }

                if (cheque.Status == "Disbursed")
                {
                    throw new InvalidOperationException("This loan has already been disbursed");
                }

                // Get member details for disbursement
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == loan.CompanyCode);

                string memberPhone = phoneNumber ?? member?.PhoneNo ?? member?.MobileNo ?? "";

                // Clean phone number for M-Pesa
                if (!string.IsNullOrEmpty(memberPhone) && withdrawalMethod == "MPESA")
                {
                    memberPhone = memberPhone.Replace("+", "").Replace(" ", "").Replace("-", "");
                    if (memberPhone.StartsWith("0"))
                        memberPhone = "254" + memberPhone.Substring(1);
                    if (!memberPhone.StartsWith("254") && memberPhone.Length == 9)
                        memberPhone = "254" + memberPhone;
                }

                decimal amountToDisburse = cheque.AmountIssued ?? cheque.Amount ?? 0;

                // Get GL accounts from loan type
                string loanAssetAccount = loanType?.LoanAcc ?? "LOAN_ASSET_ACCOUNT";
                string sourceAccount = cheque.ContraAcc ?? "BANK_ACCOUNT";

                // Create disbursement GL transaction
                var disbursementGL = new Gltransaction
                {
                    TransDate = DateTime.Now,
                    Amount = amountToDisburse,
                    DrAccNo = loanAssetAccount,
                    CrAccNo = sourceAccount,
                    Temp = "DISBURSEMENT",
                    DocumentNo = cheque.Voucherno,
                    Source = "LOAN_DISBURSEMENT",
                    CompanyCode = loan.CompanyCode,
                    TransDescript = $"Loan Disbursement - {loanNo} - Self-Service Withdrawal",
                    AuditTime = DateTime.Now,
                    AuditId = memberNo,
                    Cash = 0,
                    DocPosted = 1,
                    ChequeNo = cheque.ChequeNo,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Module = "LOAN",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now
                };

                _context.Gltransactions.Add(disbursementGL);
                await _context.SaveChangesAsync();

                // Create loan balance record
                var loanbal = new Loanbal
                {
                    LoanNo = loanNo,
                    LoanCode = loan.LoanCode,
                    MemberNo = memberNo,
                    Balance = amountToDisburse,
                    IntrOwed = 0,
                    Installments = loan.RepayPeriod ?? 12,
                    IntrOwed2 = 0,
                    FirstDate = DateTime.Now,
                    RepayRate = 0,
                    LastDate = DateTime.Now.AddMonths(loan.RepayPeriod ?? 12),
                    Duedate = DateTime.Now.AddMonths(1),
                    IntrCharged = 0,
                    Interest = loan.Interest ?? 0,
                    Companycode = loan.CompanyCode,
                    Penalty = 0,
                    RepayRate2 = 0,
                    RepayMethod = loan.RepayMethod ?? "AMT",
                    Cleared = false,
                    AutoCalc = true,
                    IntrAmount = 0,
                    RepayPeriod = loan.RepayPeriod ?? 12,
                    Remarks = $"Self-service withdrawal via {withdrawalMethod}",
                    AuditId = memberNo,
                    AuditTime = DateTime.Now,
                    IntBalance = 0,
                    CategoryCode = null,
                    InterestAccrued = 0,
                    Defaulter = "N",
                    Processdate = DateTime.Now,
                    Receiptno = null,
                    Cease = "N",
                    Nextduedate = DateTime.Now.AddMonths(1),
                    TransactionNo = disbursementGL.TransactionNo,
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

                // Update cheque status
                cheque.Status = "Disbursed";
                cheque.Balance = amountToDisburse;
                cheque.UserName = memberNo;
                cheque.AuditDateTime = DateTime.Now;
                _context.Cheques.Update(cheque);

                // Update loan status
                loan.Status = (int)Status.Disbursed;
                loan.Posted = "ACTIVE";
                loan.UserName = memberNo;
                loan.AuditDateTime = DateTime.Now;
                _context.Loans.Update(loan);

                await _context.SaveChangesAsync();

                await _loanService.GenerateLoanScheduleAsync(loanNo);

                // Create blockchain record
                var blockchainData = new
                {
                    TransactionType = "LOAN_WITHDRAWAL",
                    LoanNo = loanNo,
                    MemberNo = memberNo,
                    MemberName = $"{member?.Surname} {member?.OtherNames}",
                    Amount = amountToDisburse,
                    WithdrawalMethod = withdrawalMethod,
                    PhoneNumber = withdrawalMethod == "MPESA" ? memberPhone : null,
                    DisbursementDate = DateTime.Now,
                    GLTransactionId = disbursementGL.Id
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_WITHDRAWAL",
                    MemberNo = memberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = amountToDisburse,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update all records with BlockchainTxId
                loanbal.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                disbursementGL.BlockchainTxId = blockchainTx.TransactionId;
                cheque.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                string successMessage = withdrawalMethod == "MPESA"
                    ? $"KES {amountToDisburse:N0} has been sent to {memberPhone}. Check your M-Pesa."
                    : $"KES {amountToDisburse:N0} has been added to your FOSA account.";

                return new LoanDisbursementResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Amount = amountToDisburse,
                    Message = successMessage,
                    TransactionReference = disbursementGL.TransactionNo,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error withdrawing loan {loanNo}");
                throw;
            }
        }

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

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

                bool isMobileLoan = loanType?.MobileLoan == true;
                string statusName = ((Status)(loan.Status ?? 0)).ToString();

                bool canWithdraw = loan.Status == (int)Status.Endorsed;
                bool isApproved = loan.Status == (int)Status.Approved || loan.Status == (int)Status.Endorsed;

                return new LoanApplicationResultDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Status = statusName,
                    IsMobileLoan = isMobileLoan,
                    CanWithdrawNow = canWithdraw,
                    Message = GetStatusMessage(statusName, isMobileLoan, canWithdraw),
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
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .Select(l => new MemberLoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        LoanType = l.LoanCode,
                        PrincipalAmount = l.LoanAmt ?? 0,
                        Status = ((Status)(l.Status ?? 0)).ToString(),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = l.AuditDateTime,
                        OutstandingBalance = 0,
                        NextPaymentDate = null,
                        MonthlyInstallment = 0
                    })
                    .ToListAsync();

                // Get outstanding balances
                foreach (var loan in loans)
                {
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                    if (loanbal != null)
                    {
                        loan.OutstandingBalance = loanbal.Balance;
                        loan.MonthlyInstallment = loanbal.RepayRate;
                        loan.NextPaymentDate = loanbal.Nextduedate;
                    }
                }

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

                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .OrderBy(s => s.InstallmentNo)
                    .Select(s => new LoanScheduleDTO
                    {
                        InstallmentNo = s.InstallmentNo,
                        DueDate = s.DueDate,
                        PrincipalAmount = s.PrincipalAmount,
                        InterestAmount = s.InterestAmount,
                        TotalInstallment = s.TotalInstallment,
                        PaidAmount = s.PaidTotal,
                        OutstandingAmount = s.OutstandingTotal,
                        PenaltyAmount = s.PenaltyAmount,
                        Status = s.Status,
                        PaidDate = s.PaidDate,
                        OutstandingPrincipal = s.OutstandingPrincipal.ToString("N2"),
                        OutstandingInterest = s.OutstandingInterest.ToString("N2"),
                        OutstandingTotal = s.OutstandingTotal.ToString("N2"),
                        IsFlexible = s.IsFlexible,
                        MinimumPayment = s.MinimumPayment
                    })
                    .ToListAsync();

                return schedules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting repayment schedule for loan {loanNo}");
                return new List<LoanScheduleDTO>();
            }
        }

        private string GetStatusMessage(string status, bool isMobileLoan, bool canWithdraw)
        {
            return status switch
            {
                "Draft" => "Your application is being prepared. Please wait.",
                "Submitted" => isMobileLoan
                    ? "Your mobile loan is being processed. Check back in a few moments."
                    : "Your application has been submitted and is pending review.",
                "UnderAppraisal" => "Your loan is being appraised by our team.",
                "Approved" => "Your loan has been approved! Ready for disbursement.",
                "Endorsed" => "Your loan is ready for withdrawal! Click 'Withdraw' to receive funds.",
                "Disbursed" => "Your loan has been disbursed. Check your M-Pesa or FOSA account.",
                "Closed" => "Your loan has been fully repaid.",
                "Rejected" => "Your loan application was not approved. Please contact the SACCO office.",
                "Defaulted" => "Your loan is in default. Please contact the SACCO office immediately.",
                _ => "Status unknown. Please contact support."
            };
        }

        public async Task<LoanEligibilityDTO> CheckMobileLoanEligibilityAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Checking mobile loan eligibility for member {memberNo}, company {companyCode}");

                // Find the mobile loan product (where MobileLoan = true)
                var mobileLoanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.CompanyCode == companyCode && lt.MobileLoan == true);

                if (mobileLoanType == null)
                {
                    _logger.LogWarning($"No mobile loan product found for company {companyCode}");
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Mobile loan product is not available. Please contact SACCO administrator.",
                        IsMobileLoan = true,
                        EligibleAmount = 0,
                        MaxAmount = 0,
                        MinAmount = 0,
                        CurrentDeposits = 0,
                        CurrentShares = 0,
                        Multiplier = 0,
                        RequiresGuarantor = false,
                        InterestRate = 0,
                        RepaymentPeriodMonths = 0,
                        EstimatedMonthlyInstallment = 0
                    };
                }

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Member not found",
                        IsMobileLoan = true,
                        EligibleAmount = 0,
                        MaxAmount = 0,
                        MinAmount = 0
                    };
                }

                // Check member active status
                if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Your account is not active. Please contact the SACCO office.",
                        IsMobileLoan = true,
                        EligibleAmount = 0,
                        MaxAmount = 0,
                        MinAmount = 0
                    };
                }

                // Check account status
                if (member.Status == 0)
                {
                    return new LoanEligibilityDTO
                    {
                        IsEligible = false,
                        Message = "Your account has been restricted. Please contact the SACCO office.",
                        IsMobileLoan = true,
                        EligibleAmount = 0,
                        MaxAmount = 0,
                        MinAmount = 0
                    };
                }

                // Get member deposits from ContribShares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                // Get multiplier from Sharetype table
                decimal multiplier = 0;

                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.LoanToShareRatio.HasValue && s.LoanToShareRatio.Value > 0)
                    .ToListAsync();

                if (shareTypes.Any())
                {
                    multiplier = (decimal)shareTypes.Max(s => s.LoanToShareRatio.Value);
                }
                else
                {
                    // No multiplier configured - use default for mobile loans (3x)
                    multiplier = 3m;
                    _logger.LogWarning($"No Sharetype with LoanToShareRatio found for company {companyCode}, using default multiplier 3");
                }

                // Calculate eligible amount based on deposits × multiplier
                decimal eligibleAmount = totalDeposits * multiplier;

                // Apply loan type max amount
                decimal maxAmount = mobileLoanType.MaxAmount ?? eligibleAmount;
                decimal minAmount = 1000; // Minimum mobile loan amount

                // Check if member has existing active mobile loan
                bool hasActiveMobileLoan = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                   l.LoanCode == mobileLoanType.LoanCode &&
                                   (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));

                // Check if member has any defaulted loans
                var hasDefaulted = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo && l.CompanyCode == companyCode &&
                                   (l.Status == (int)Status.Defaulted || l.Status == (int)Status.WrittenOff));

                // Determine eligibility
                bool isEligible = true;
                List<string> messages = new List<string>();

                if (totalDeposits <= 0)
                {
                    isEligible = false;
                    messages.Add("You have no deposits. Please make a deposit first to qualify for a mobile loan.");
                }

                if (hasDefaulted)
                {
                    isEligible = false;
                    messages.Add("You have defaulted on a previous loan. Please clear the default first.");
                }

                if (hasActiveMobileLoan)
                {
                    isEligible = false;
                    messages.Add("You already have an active mobile loan. Only one mobile loan is allowed at a time.");
                }

                if (eligibleAmount < minAmount)
                {
                    isEligible = false;
                    messages.Add($"Your eligible loan amount ({eligibleAmount:C}) is below the minimum mobile loan amount ({minAmount:C}).");
                }

                // Get interest rate from loan type
                decimal interestRate = 12; // Default 12%
                if (!string.IsNullOrEmpty(mobileLoanType.Interest))
                {
                    string interestStr = mobileLoanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out decimal parsedRate))
                    {
                        interestRate = parsedRate;
                    }
                }

                int defaultRepayPeriod = mobileLoanType.RepayPeriod ?? 12;
                bool requiresGuarantor = !string.IsNullOrEmpty(mobileLoanType.Guarantor) &&
                                         mobileLoanType.Guarantor != "No" &&
                                         mobileLoanType.Guarantor != "N" &&
                                         mobileLoanType.Guarantor != "0";

                string message = isEligible
                    ? $"You are eligible for a Mobile Loan up to {Math.Min(eligibleAmount, maxAmount):C}! (Deposits: {totalDeposits:C} × {multiplier})"
                    : string.Join(" ", messages);

                return new LoanEligibilityDTO
                {
                    IsEligible = isEligible,
                    Message = message,
                    EligibleAmount = Math.Min(eligibleAmount, maxAmount),
                    MaxAmount = maxAmount,
                    MinAmount = minAmount,
                    CurrentDeposits = totalDeposits,
                    CurrentShares = totalShares,
                    Multiplier = multiplier,
                    IsMobileLoan = true,
                    RequiresGuarantor = requiresGuarantor,
                    InterestRate = interestRate,
                    RepaymentPeriodMonths = defaultRepayPeriod,
                    EstimatedMonthlyInstallment = CalculateMonthlyInstallment(Math.Min(eligibleAmount, maxAmount), interestRate, defaultRepayPeriod)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking mobile loan eligibility for member {memberNo}");
                throw;
            }
        }

        #endregion
    }
}