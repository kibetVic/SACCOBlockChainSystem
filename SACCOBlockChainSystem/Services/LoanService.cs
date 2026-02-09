using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SACCOBlockChainSystem.Services
{
    public class LoanService : ILoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly ICompanyContextService _companyContextService;
        private readonly IMemberService _memberService;
        private readonly IShareService _shareService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoanService(
            ApplicationDbContext context,
            ILogger<LoanService> logger,
            IBlockchainService blockchainService,
            ICompanyContextService companyContextService,
            IMemberService memberService,
            IShareService shareService,
            IHttpContextAccessor httpContextAccessor = null)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _companyContextService = companyContextService;
            _memberService = memberService;
            _shareService = shareService;
            _httpContextAccessor = httpContextAccessor;
        }

        // Helper method to check user permissions
        private bool IsAdmin()
        {
            return _httpContextAccessor?.HttpContext?.User?.IsInRole("Admin") == true ||
                   _httpContextAccessor?.HttpContext?.User?.IsInRole("SuperAdmin") == true;
        }

        private bool IsAuthorizedForMember(string memberNo)
        {
            var currentUser = _httpContextAccessor?.HttpContext?.User;
            if (currentUser == null) return false;

            // Admins can access any member
            if (IsAdmin()) return true;

            // Regular members can only access their own data
            var currentMemberNo = currentUser.FindFirst("MemberNo")?.Value;
            return currentMemberNo == memberNo;
        }

        private string GetCurrentUserId()
        {
            return _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "SYSTEM";
        }

        public async Task<LoanApplicationResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application)
        {
            _logger.LogInformation($"Processing loan application for member: {application.MemberNo}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get company code
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                application.CompanyCode = companyCode;

                // For admin applying on behalf of member, ensure admin is logged in
                if (!IsAdmin() && application.CreatedBy == null)
                {
                    application.CreatedBy = GetCurrentUserId();
                }

                // Validate member exists
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == application.MemberNo &&
                                             m.CompanyCode == companyCode);

                if (member == null)
                    throw new ValidationException($"Member {application.MemberNo} not found");

                // Check loan eligibility
                var eligibility = await CheckLoanEligibilityAsync(application.MemberNo,
                    application.LoanCode, application.LoanAmount);

                if (!eligibility.IsEligible)
                    throw new ValidationException($"Loan application rejected: {eligibility.Reason}");

                // Get loan type details
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode &&
                                              lt.CompanyCode == companyCode);

                if (loanType == null)
                    throw new ValidationException($"Loan type {application.LoanCode} not found");

                // Generate loan number
                var loanNo = GenerateLoanNumber(companyCode);

                // Calculate insurance if applicable
                decimal? insurance = null;
                decimal? totalPremium = null;

                // Get member's total shares from Share model
                var totalShares = await _context.Shares
                    .Where(s => s.MemberNo == application.MemberNo && s.CompanyCode == companyCode)
                    .SumAsync(s => s.TotalShares ?? 0);

                // Get member's total share capital from ContribShare
                var shareCapital = await _context.ContribShares
                    .Where(cs => cs.MemberNo == application.MemberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                // Create loan record with initial status
                int initialStatus = IsAdmin() ? 1 : 1; // Both admins and members start at pending
                string initialStatusDesc = IsAdmin() ? 
                    "Loan application submitted by admin" : 
                    "Loan application submitted";

                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanCode,
                    ApplicDate = DateTime.Now,
                    LoanAmt = application.LoanAmount,
                    RepayPeriod = application.RepayPeriod,
                    Purpose = application.Purpose,
                    CompanyCode = companyCode,
                    IdNo = member.Idno,
                    BasicSalary = shareCapital,
                    PreparedBy = application.CreatedBy ?? "SYSTEM",
                    AddSecurity = application.SecurityDetails,
                    Sourceofrepayment = application.SourceOfRepayment,
                    Insurance = insurance,
                    TotalPremium = totalPremium,
                    InsPercent = null,
                    Sharecapital = totalShares,
                    Cshares = totalShares,
                    Status = initialStatus,
                    StatusDescription = initialStatusDesc,
                    Posted = "N",
                    AuditId = application.CreatedBy ?? GetCurrentUserId(),
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    RepayMethod = loanType.Repaymethod,
                    Interest = decimal.TryParse(loanType.Interest, out var interestRate) ? interestRate : 0,
                    Bridging = loanType.Bridging,
                    Guaranteed = loanType.Guarantor,
                    Gperiod = loanType.GracePeriod.ToString()
                };

                _context.Loans.Add(loan);

                // Create initial loan balance record
                var loanBal = new Loanbal
                {
                    LoanNo = loanNo,
                    LoanCode = application.LoanCode,
                    MemberNo = application.MemberNo,
                    Balance = application.LoanAmount,
                    IntrOwed = 0,
                    Installments = application.LoanAmount / application.RepayPeriod,
                    IntrOwed2 = 0,
                    FirstDate = DateTime.Now,
                    RepayRate = application.LoanAmount / application.RepayPeriod,
                    LastDate = DateTime.Now,
                    Duedate = DateTime.Now.AddMonths(application.RepayPeriod),
                    IntrCharged = 0,
                    Interest = decimal.TryParse(loanType.Interest, out var rate) ? rate : 0,
                    Companycode = companyCode,
                    Penalty = 0,
                    RepayRate2 = application.LoanAmount / application.RepayPeriod,
                    RepayMethod = loanType.Repaymethod ?? "MONTHLY",
                    Cleared = false,
                    AutoCalc = true,
                    IntrAmount = 0,
                    RepayPeriod = application.RepayPeriod,
                    IntBalance = 0,
                    InterestAccrued = 0,
                    AuditId = application.CreatedBy ?? GetCurrentUserId(),
                    AuditTime = DateTime.Now,
                    Processdate = DateTime.Now,
                    Nextduedate = DateTime.Now.AddMonths(1),
                    RepayMode = 1,
                    AuditDateTime = DateTime.Now
                };

                _context.Loanbals.Add(loanBal);

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    LoanType = loanType.LoanType1,
                    AppliedAmount = application.LoanAmount,
                    RepayPeriod = application.RepayPeriod,
                    Purpose = application.Purpose,
                    ApplicationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "APPLICATION_SUBMITTED",
                    CreatedBy = application.CreatedBy,
                    SubmittedBy = IsAdmin() ? "ADMIN" : "MEMBER",
                    EligibilityCheck = new
                    {
                        eligibility.TotalShares,
                        eligibility.AvailableGuarantee,
                        eligibility.MaxLoanEligibility
                    }
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_APPLICATION",
                    application.MemberNo,
                    companyCode,
                    application.LoanAmount,
                    loanNo,
                    blockchainData
                );

                // Update loan with blockchain transaction ID
                loan.BlockchainTxId = blockchainTx?.TransactionId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Loan application {loanNo} created successfully");

                return new LoanApplicationResponseDTO
                {
                    Success = true,
                    LoanNo = loanNo,
                    Message = "Loan application submitted successfully",
                    EligibleAmount = eligibility.MaxLoanEligibility,
                    BlockchainTxId = blockchainTx?.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing loan application for member {application.MemberNo}");
                throw;
            }
        }

        public async Task<LoanEligibilityDTO> CheckLoanEligibilityAsync(string memberNo, string loanCode, decimal amount)
        {
            var companyCode = _companyContextService.GetCurrentCompanyCode();

            try
            {
                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                    return new LoanEligibilityDTO
                    {
                        MemberNo = memberNo,
                        MemberName = "Unknown Member",
                        IsEligible = false,
                        Reason = "Member not found"
                    };

                // Get member's total shares from Share model
                var totalShares = await _context.Shares
                    .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                    .SumAsync(s => s.TotalShares ?? 0);

                // Get total guarantee provided by member
                var totalGuarantee = await _context.Loanguars
                    .Where(g => g.MemberNo == memberNo && g.CompanyCode == companyCode)
                    .SumAsync(g => g.Amount ?? 0);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                    return new LoanEligibilityDTO
                    {
                        MemberNo = memberNo,
                        MemberName = $"{member.Surname} {member.OtherNames}",
                        TotalShares = totalShares,
                        TotalGuarantee = totalGuarantee,
                        IsEligible = false,
                        Reason = "Loan type not found"
                    };

                // Calculate available guarantee (shares * loan-to-share ratio)
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.CompanyCode == companyCode && st.IsMainShares);

                var loanToShareRatio = shareType?.LoanToShareRatio ?? 3.0f;
                var maxGuarantee = totalShares * (decimal)loanToShareRatio;
                var availableGuarantee = maxGuarantee - totalGuarantee;

                // Check if member has active loans
                var activeLoans = await _context.Loans
                    .CountAsync(l => l.MemberNo == memberNo &&
                                    l.CompanyCode == companyCode &&
                                    (l.Status == 5 || l.Status == 6)); // Approved or Disbursed

                var totalLoanBalance = await _context.Loanbals
                    .Where(lb => lb.MemberNo == memberNo && lb.Companycode == companyCode && !lb.Cleared)
                    .SumAsync(lb => lb.Balance);

                // Check max loans per member
                if (loanType.MaxLoans.HasValue && activeLoans >= loanType.MaxLoans.Value)
                    return new LoanEligibilityDTO
                    {
                        MemberNo = memberNo,
                        MemberName = $"{member.Surname} {member.OtherNames}",
                        TotalShares = totalShares,
                        TotalGuarantee = totalGuarantee,
                        AvailableGuarantee = availableGuarantee,
                        ActiveLoans = activeLoans,
                        TotalLoanBalance = totalLoanBalance,
                        IsEligible = false,
                        Reason = $"Maximum loans limit reached ({loanType.MaxLoans.Value})"
                    };

                // Calculate max loan eligibility
                decimal maxLoanEligibility = 0;

                if (loanType.EarningRation.HasValue && loanType.EarningRation.Value > 0)
                {
                    maxLoanEligibility = totalShares * (decimal)loanType.EarningRation.Value;
                }
                else if (loanType.MaxAmount.HasValue)
                {
                    maxLoanEligibility = Math.Min(availableGuarantee, loanType.MaxAmount.Value);
                }
                else
                {
                    maxLoanEligibility = availableGuarantee;
                }

                // Admin override: if admin is checking eligibility, they can exceed limits with approval
                bool adminOverride = IsAdmin() && amount > maxLoanEligibility;
                
                if (!adminOverride && amount > maxLoanEligibility)
                    return new LoanEligibilityDTO
                    {
                        MemberNo = memberNo,
                        MemberName = $"{member.Surname} {member.OtherNames}",
                        TotalShares = totalShares,
                        TotalGuarantee = totalGuarantee,
                        AvailableGuarantee = availableGuarantee,
                        MaxLoanEligibility = maxLoanEligibility,
                        ActiveLoans = activeLoans,
                        TotalLoanBalance = totalLoanBalance,
                        IsEligible = false,
                        Reason = $"Requested amount ({amount:C}) exceeds maximum eligibility ({maxLoanEligibility:C})"
                    };

                return new LoanEligibilityDTO
                {
                    MemberNo = memberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    TotalShares = totalShares,
                    TotalGuarantee = totalGuarantee,
                    AvailableGuarantee = availableGuarantee,
                    MaxLoanEligibility = adminOverride ? amount : maxLoanEligibility,
                    ActiveLoans = activeLoans,
                    TotalLoanBalance = totalLoanBalance,
                    IsEligible = true,
                    Reason = adminOverride ? "Eligible with admin approval override" : "Eligible for loan"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking loan eligibility for member {memberNo}");
                return new LoanEligibilityDTO
                {
                    MemberNo = memberNo,
                    MemberName = "Error",
                    IsEligible = false,
                    Reason = $"Error checking eligibility: {ex.Message}"
                };
            }
        }

        public async Task<bool> AddGuarantorAsync(LoanGuarantorDTO guarantor)
        {
            _logger.LogInformation($"Adding guarantor {guarantor.GuarantorMemberNo} to loan {guarantor.LoanNo}");

            // Only admins can add guarantors
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can add guarantors");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                guarantor.CompanyCode = companyCode;

                // Check if loan exists
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo &&
                                             l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {guarantor.LoanNo} not found");

                // Check if guarantor exists
                var guarantorMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == guarantor.GuarantorMemberNo &&
                                             m.CompanyCode == companyCode);

                if (guarantorMember == null)
                    throw new ValidationException($"Guarantor member {guarantor.GuarantorMemberNo} not found");

                // Check if guarantor is already added
                var existingGuarantor = await _context.Loanguars
                    .FirstOrDefaultAsync(g => g.LoanNo == guarantor.LoanNo &&
                                             g.MemberNo == guarantor.GuarantorMemberNo &&
                                             g.CompanyCode == companyCode);

                if (existingGuarantor != null)
                    throw new ValidationException($"Member is already a guarantor for this loan");

                // Check guarantor's eligibility
                var guarantorShares = await _context.Shares
                    .Where(s => s.MemberNo == guarantor.GuarantorMemberNo && s.CompanyCode == companyCode)
                    .SumAsync(s => s.TotalShares ?? 0);

                var guarantorTotalGuarantee = await _context.Loanguars
                    .Where(g => g.MemberNo == guarantor.GuarantorMemberNo && g.CompanyCode == companyCode)
                    .SumAsync(g => g.Amount ?? 0);

                // Calculate available guarantee capacity (3x shares ratio)
                var availableGuarantee = (guarantorShares * 3) - guarantorTotalGuarantee;

                if (guarantor.Amount.HasValue && guarantor.Amount.Value > availableGuarantee)
                    throw new ValidationException($"Guarantor does not have enough guarantee capacity. Available: {availableGuarantee:C}");

                // Add guarantor
                var loanGuar = new Loanguar
                {
                    MemberNo = guarantor.GuarantorMemberNo,
                    LoanNo = guarantor.LoanNo,
                    Amount = guarantor.Amount ?? (loan.LoanAmt / 2), // Default to half of loan amount
                    Balance = guarantor.Amount ?? (loan.LoanAmt / 2),
                    AuditId = guarantor.ActionBy ?? GetCurrentUserId(),
                    AuditTime = DateTime.Now,
                    Collateral = guarantor.Collateral,
                    Description = guarantor.Description,
                    Transfered = false,
                    FullNames = $"{guarantorMember.Surname} {guarantorMember.OtherNames}",
                    CompanyCode = companyCode,
                    Id = GetNextLoanguarId()
                };

                _context.Loanguars.Add(loanGuar);

                // Update loan status
                loan.Status = 2; // Guarantors stage
                loan.StatusDescription = $"Guarantor added by admin: {guarantor.GuarantorMemberNo}";
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = guarantor.LoanNo,
                    GuarantorMemberNo = guarantor.GuarantorMemberNo,
                    GuarantorName = $"{guarantorMember.Surname} {guarantorMember.OtherNames}",
                    GuaranteeAmount = loanGuar.Amount,
                    Collateral = guarantor.Collateral,
                    AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    AddedBy = guarantor.ActionBy ?? GetCurrentUserId(),
                    AddedByRole = "ADMIN",
                    GuarantorShares = guarantorShares,
                    AvailableGuarantee = availableGuarantee
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_GUARANTOR_ADDED",
                    guarantor.GuarantorMemberNo,
                    companyCode,
                    loanGuar.Amount ?? 0,
                    $"{guarantor.LoanNo}_{guarantor.GuarantorMemberNo}",
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Guarantor {guarantor.GuarantorMemberNo} added to loan {guarantor.LoanNo} by admin");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error adding guarantor to loan {guarantor.LoanNo}");
                throw;
            }
        }

        public async Task<bool> AppraiseLoanAsync(LoanAppraisalDTO appraisal)
        {
            _logger.LogInformation($"Appraising loan {appraisal.LoanNo}");

            // Only admins can appraise loans
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can appraise loans");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                appraisal.CompanyCode = companyCode;

                // Get loan
                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .FirstOrDefaultAsync(l => l.LoanNo == appraisal.LoanNo &&
                                             l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {appraisal.LoanNo} not found");

                // Update loan with appraisal details
                loan.Status = 3; // Appraisal stage
                loan.StatusDescription = $"Loan appraised by admin: {appraisal.Recommendation}";

                if (appraisal.RecommendedAmount.HasValue)
                    loan.LoanAmt = appraisal.RecommendedAmount.Value;

                if (appraisal.RecommendedPeriod.HasValue)
                    loan.RepayPeriod = appraisal.RecommendedPeriod.Value;

                if (appraisal.InterestRate.HasValue)
                    loan.Interest = appraisal.InterestRate.Value;

                if (appraisal.ProcessingFee.HasValue)
                    loan.PremiumPayable = appraisal.ProcessingFee.Value;

                loan.AuditId = appraisal.AppraisalBy ?? GetCurrentUserId();
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                // Update loan balance record if amount changed
                var loanBal = await _context.Loanbals
                    .FirstOrDefaultAsync(lb => lb.LoanNo == appraisal.LoanNo);

                if (loanBal != null && appraisal.RecommendedAmount.HasValue)
                {
                    loanBal.Balance = appraisal.RecommendedAmount.Value;
                    loanBal.Installments = appraisal.RecommendedAmount.Value / (loan.RepayPeriod ?? 12);
                    loanBal.RepayRate = appraisal.RecommendedAmount.Value / (loan.RepayPeriod ?? 12);
                    loanBal.RepayRate2 = appraisal.RecommendedAmount.Value / (loan.RepayPeriod ?? 12);
                    loanBal.Interest = appraisal.InterestRate ?? loanBal.Interest;
                    loanBal.AuditTime = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = appraisal.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    AppraisalBy = appraisal.AppraisalBy ?? GetCurrentUserId(),
                    AppraisalByRole = "ADMIN",
                    Recommendation = appraisal.Recommendation,
                    Comments = appraisal.Comments,
                    RecommendedAmount = appraisal.RecommendedAmount,
                    RecommendedPeriod = appraisal.RecommendedPeriod,
                    InterestRate = appraisal.InterestRate,
                    ProcessingFee = appraisal.ProcessingFee,
                    AppraisalDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PreviousStatus = "GUARANTORS",
                    NewStatus = "APPRAISAL"
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_APPRAISAL",
                    loan.MemberNo,
                    companyCode,
                    appraisal.RecommendedAmount ?? loan.LoanAmt ?? 0,
                    appraisal.LoanNo,
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {appraisal.LoanNo} appraised successfully by admin");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error appraising loan {appraisal.LoanNo}");
                throw;
            }
        }

        public async Task<bool> EndorseLoanAsync(LoanEndorsementDTO endorsement)
        {
            _logger.LogInformation($"Endorsing loan {endorsement.LoanNo}");

            // Only admins can endorse loans
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can endorse loans");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                endorsement.CompanyCode = companyCode;

                // Get loan
                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .FirstOrDefaultAsync(l => l.LoanNo == endorsement.LoanNo &&
                                             l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {endorsement.LoanNo} not found");

                // Update loan status
                if (endorsement.Decision.ToUpper() == "APPROVED")
                {
                    loan.Status = 5; // Approved
                    loan.StatusDescription = "Loan approved by admin";
                    loan.Posted = "Y";

                    if (endorsement.ApprovedAmount.HasValue)
                        loan.LoanAmt = endorsement.ApprovedAmount.Value;

                    if (endorsement.ApprovedPeriod.HasValue)
                        loan.RepayPeriod = endorsement.ApprovedPeriod.Value;

                    if (endorsement.ApprovalDate.HasValue)
                        loan.AuditTime = endorsement.ApprovalDate.Value;

                    // Update loan balance
                    var loanBal = await _context.Loanbals
                        .FirstOrDefaultAsync(lb => lb.LoanNo == endorsement.LoanNo);

                    if (loanBal != null && endorsement.ApprovedAmount.HasValue)
                    {
                        loanBal.Balance = endorsement.ApprovedAmount.Value;
                        loanBal.Installments = endorsement.ApprovedAmount.Value / (loan.RepayPeriod ?? 12);
                        loanBal.RepayRate = endorsement.ApprovedAmount.Value / (loan.RepayPeriod ?? 12);
                        loanBal.RepayRate2 = endorsement.ApprovedAmount.Value / (loan.RepayPeriod ?? 12);
                        loanBal.Duedate = DateTime.Now.AddMonths(loan.RepayPeriod ?? 12);
                        loanBal.Nextduedate = DateTime.Now.AddMonths(1);
                        loanBal.AuditTime = DateTime.Now;
                    }
                }
                else if (endorsement.Decision.ToUpper() == "REJECTED")
                {
                    loan.Status = 7; // Rejected
                    loan.StatusDescription = $"Loan rejected by admin: {endorsement.Comments}";
                    loan.Posted = "N";
                }
                else
                {
                    throw new ValidationException($"Invalid decision: {endorsement.Decision}");
                }

                loan.AuditId = endorsement.EndorsedBy ?? GetCurrentUserId();
                loan.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = endorsement.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    EndorsedBy = endorsement.EndorsedBy ?? GetCurrentUserId(),
                    EndorsedByRole = "ADMIN",
                    Decision = endorsement.Decision,
                    Comments = endorsement.Comments,
                    ApprovedAmount = endorsement.ApprovedAmount,
                    ApprovedPeriod = endorsement.ApprovedPeriod,
                    ApprovalDate = endorsement.ApprovalDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DisbursementDate = endorsement.DisbursementDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                    EndorsementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PreviousStatus = "APPRAISAL",
                    NewStatus = endorsement.Decision.ToUpper() == "APPROVED" ? "APPROVED" : "REJECTED"
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_ENDORSEMENT",
                    loan.MemberNo,
                    companyCode,
                    endorsement.ApprovedAmount ?? loan.LoanAmt ?? 0,
                    endorsement.LoanNo,
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {endorsement.LoanNo} endorsed by admin with decision: {endorsement.Decision}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error endorsing loan {endorsement.LoanNo}");
                throw;
            }
        }

        public async Task<bool> DisburseLoanAsync(LoanDisbursementDTO disbursement)
        {
            _logger.LogInformation($"Disbursing loan {disbursement.LoanNo}");

            // Only admins can disburse loans
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can disburse loans");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                disbursement.CompanyCode = companyCode;

                // Get loan
                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .FirstOrDefaultAsync(l => l.LoanNo == disbursement.LoanNo &&
                                             l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {disbursement.LoanNo} not found");

                if (loan.Status != 5) // Must be approved
                    throw new ValidationException($"Loan must be approved before disbursement. Current status: {loan.Status}");

                // Update loan status
                loan.Status = 6; // Disbursed
                loan.StatusDescription = $"Loan disbursed by admin on {disbursement.DisbursementDate:yyyy-MM-dd}";
                loan.LoanAmt = disbursement.Amount;
                loan.AuditId = disbursement.ProcessedBy ?? GetCurrentUserId();
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                // Update loan balance
                var loanBal = await _context.Loanbals
                    .FirstOrDefaultAsync(lb => lb.LoanNo == disbursement.LoanNo);

                if (loanBal != null)
                {
                    loanBal.Balance = disbursement.Amount;
                    loanBal.FirstDate = disbursement.DisbursementDate;
                    loanBal.LastDate = disbursement.DisbursementDate;
                    loanBal.Duedate = disbursement.DisbursementDate.AddMonths(loan.RepayPeriod ?? 12);
                    loanBal.Nextduedate = disbursement.DisbursementDate.AddMonths(1);
                    loanBal.Processdate = disbursement.DisbursementDate;
                    loanBal.AuditTime = DateTime.Now;
                    loanBal.AuditDateTime = DateTime.Now;
                }

                // Create repayment schedule
                await CreateRepaymentScheduleAsync(loan, disbursement.DisbursementDate);

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = disbursement.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    DisbursementAmount = disbursement.Amount,
                    DisbursementDate = disbursement.DisbursementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    PaymentMethod = disbursement.PaymentMethod,
                    ReferenceNo = disbursement.ReferenceNo,
                    ProcessedBy = disbursement.ProcessedBy ?? GetCurrentUserId(),
                    ProcessedByRole = "ADMIN",
                    Remarks = disbursement.Remarks,
                    TotalGuarantors = await _context.Loanguars.CountAsync(g => g.LoanNo == disbursement.LoanNo),
                    TotalGuaranteeAmount = await _context.Loanguars.Where(g => g.LoanNo == disbursement.LoanNo).SumAsync(g => g.Amount ?? 0)
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_DISBURSEMENT",
                    loan.MemberNo,
                    companyCode,
                    disbursement.Amount,
                    disbursement.LoanNo,
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {disbursement.LoanNo} disbursed successfully by admin");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error disbursing loan {disbursement.LoanNo}");
                throw;
            }
        }

        public async Task<bool> MakeRepaymentAsync(LoanRepaymentDTO repayment)
        {
            _logger.LogInformation($"Processing repayment for loan {repayment.LoanNo}");

            // Only admins can process repayments
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can process loan repayments");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                repayment.CompanyCode = companyCode;

                // Get loan balance
                var loanBal = await _context.Loanbals
                    .FirstOrDefaultAsync(lb => lb.LoanNo == repayment.LoanNo &&
                                              lb.MemberNo == repayment.MemberNo);

                if (loanBal == null)
                    throw new ValidationException($"Loan balance not found for {repayment.LoanNo}");

                // Get loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == repayment.LoanNo);

                if (loan == null)
                    throw new ValidationException($"Loan {repayment.LoanNo} not found");

                // Create repayment record
                var repay = new Repay
                {
                    LoanNo = repayment.LoanNo,
                    MemberNo = repayment.MemberNo,
                    DateReceived = repayment.PaymentDate,
                    Amount = repayment.Amount,
                    Remarks = $"{repayment.PaymentType} processed by admin: {repayment.Remarks}",
                    ReceiptNo = repayment.ReceiptNo,
                    AuditId = repayment.ProcessedBy ?? GetCurrentUserId(),
                    AuditTime = DateTime.Now,
                    CompanyCode = companyCode,
                    Transno = GenerateTransactionNumber(),
                    AuditDateTime = DateTime.Now
                };

                _context.Repays.Add(repay);

                // Update loan balance based on payment type
                if (repayment.PaymentType == "PRINCIPAL")
                {
                    loanBal.Balance -= repayment.Amount;

                    // Check if loan is fully repaid
                    if (loanBal.Balance <= 0)
                    {
                        loanBal.Balance = 0;
                        loanBal.Cleared = true;
                        loanBal.Nextduedate = null;

                        // Update loan status
                        if (loan != null)
                        {
                            loan.Status = 8; // Closed/Paid
                            loan.StatusDescription = $"Loan fully repaid by admin on {repayment.PaymentDate:yyyy-MM-dd}";
                            loan.AuditTime = DateTime.Now;
                            loan.AuditDateTime = DateTime.Now;
                        }
                    }
                }
                else if (repayment.PaymentType == "INTEREST")
                {
                    loanBal.IntrOwed -= repayment.Amount;
                    if (loanBal.IntrOwed < 0) loanBal.IntrOwed = 0;
                }
                else if (repayment.PaymentType == "PENALTY")
                {
                    loanBal.Penalty -= repayment.Amount;
                    if (loanBal.Penalty < 0) loanBal.Penalty = 0;
                }

                loanBal.LastDate = repayment.PaymentDate;
                loanBal.AuditTime = DateTime.Now;
                loanBal.AuditDateTime = DateTime.Now;

                // Update next due date for principal payments
                if (repayment.PaymentType == "PRINCIPAL" && loanBal.Balance > 0)
                {
                    loanBal.Nextduedate = repayment.PaymentDate.AddMonths(1);
                }

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = repayment.LoanNo,
                    MemberNo = repayment.MemberNo,
                    PaymentAmount = repayment.Amount,
                    PaymentType = repayment.PaymentType,
                    PaymentDate = repayment.PaymentDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    ReceiptNo = repayment.ReceiptNo,
                    NewLoanBalance = loanBal.Balance,
                    NewInterestBalance = loanBal.IntrOwed,
                    NewPenaltyBalance = loanBal.Penalty,
                    ProcessedBy = repayment.ProcessedBy ?? GetCurrentUserId(),
                    ProcessedByRole = "ADMIN",
                    Remarks = repayment.Remarks,
                    IsFullyPaid = loanBal.Balance <= 0
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_REPAYMENT",
                    repayment.MemberNo,
                    companyCode,
                    repayment.Amount,
                    repayment.ReceiptNo ?? GenerateReceiptNumber(),
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Repayment of {repayment.Amount:C} processed by admin for loan {repayment.LoanNo}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing repayment for loan {repayment.LoanNo}");
                throw;
            }
        }

        public async Task<LoanDetailsResponseDTO> GetLoanDetailsAsync(string loanNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .Include(l => l.LoanType)
                    .Include(l => l.LoanGuarantors)
                    .Include(l => l.Loanbals)
                    .Include(l => l.Repays)
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {loanNo} not found");

                // Check authorization
                if (!IsAuthorizedForMember(loan.MemberNo))
                    throw new UnauthorizedAccessException("You are not authorized to view this loan");

                // Get member's share balance
                var shareBalance = await _context.Shares
                    .Where(s => s.MemberNo == loan.MemberNo && s.CompanyCode == companyCode)
                    .SumAsync(s => s.TotalShares ?? 0);

                // Get loan balance
                var loanBalance = loan.Loanbals.FirstOrDefault()?.Balance ?? 0;
                var interestBalance = loan.Loanbals.FirstOrDefault()?.IntrOwed ?? 0;

                // Get total guarantee
                var totalGuarantee = loan.LoanGuarantors.Sum(g => g.Amount ?? 0);

                // Prepare guarantors list
                var guarantors = loan.LoanGuarantors.Select(g => new LoanGuarantorDTO
                {
                    LoanNo = g.LoanNo ?? "",
                    GuarantorMemberNo = g.MemberNo,
                    Amount = g.Amount,
                    Collateral = g.Collateral,
                    Description = g.Description,
                    CompanyCode = g.CompanyCode,
                    ActionBy = g.AuditId
                }).ToList();

                // Prepare transactions list
                var transactions = new List<LoanTransactionDTO>();

                // Add application transaction
                transactions.Add(new LoanTransactionDTO
                {
                    TransactionDate = loan.ApplicDate,
                    TransactionType = "APPLICATION",
                    Description = "Loan application submitted",
                    Amount = loan.LoanAmt,
                    PerformedBy = loan.PreparedBy,
                    BlockchainTxId = loan.BlockchainTxId
                });

                // Add guarantor transactions
                foreach (var guarantor in loan.LoanGuarantors)
                {
                    transactions.Add(new LoanTransactionDTO
                    {
                        TransactionDate = guarantor.AuditTime ?? DateTime.Now,
                        TransactionType = "GUARANTOR",
                        Description = $"Guarantor added: {guarantor.MemberNo}",
                        Amount = guarantor.Amount,
                        PerformedBy = guarantor.AuditId
                    });
                }

                // Add repayment transactions
                foreach (var repay in loan.Repays.OrderByDescending(r => r.DateReceived))
                {
                    transactions.Add(new LoanTransactionDTO
                    {
                        TransactionDate = repay.DateReceived ?? DateTime.Now,
                        TransactionType = "REPAYMENT",
                        Description = $"Repayment: {repay.Remarks}",
                        Amount = repay.Amount,
                        PerformedBy = repay.AuditId,
                        BlockchainTxId = repay.BlockchainTxId
                    });
                }

                // Get status description
                var statusDescription = loan.Status switch
                {
                    1 => "Pending",
                    2 => "Guarantors",
                    3 => "Appraisal",
                    4 => "Endorsement",
                    5 => "Approved",
                    6 => "Disbursed",
                    7 => "Rejected",
                    8 => "Closed",
                    _ => "Unknown"
                };

                return new LoanDetailsResponseDTO
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    LoanCode = loan.LoanCode ?? "",
                    LoanType = loan.LoanType?.LoanType1 ?? loan.LoanCode ?? "",
                    ApplicationDate = loan.ApplicDate,
                    AppliedAmount = loan.LoanAmt ?? 0,
                    ApprovedAmount = loan.LoanAmt,
                    DisbursedAmount = loan.LoanAmt,
                    RepayPeriod = loan.RepayPeriod,
                    Purpose = loan.Purpose,
                    Status = statusDescription,
                    StatusCode = loan.Status ?? 1,
                    StatusDescription = loan.StatusDescription,
                    ShareBalance = shareBalance,
                    LoanBalance = loanBalance,
                    InterestBalance = interestBalance,
                    TotalGuarantee = totalGuarantee,
                    Guarantors = guarantors,
                    Transactions = transactions.OrderByDescending(t => t.TransactionDate).ToList(),
                    CompanyCode = loan.CompanyCode,
                    BlockchainTxId = loan.BlockchainTxId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan details for {loanNo}");
                throw;
            }
        }

        public async Task<List<LoanListResponseDTO>> GetMemberLoansAsync(string memberNo)
        {
            try
            {
                // Check authorization
                if (!IsAuthorizedForMember(memberNo))
                    throw new UnauthorizedAccessException("You are not authorized to view these loans");

                var companyCode = _companyContextService.GetCurrentCompanyCode();

                var loans = await _context.Loans
                    .Include(l => l.Member)
                    .Include(l => l.LoanType)
                    .Include(l => l.Loanbals)
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                return loans.Select(loan => new LoanListResponseDTO
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    LoanType = loan.LoanType?.LoanType1 ?? loan.LoanCode ?? "",
                    AppliedAmount = loan.LoanAmt ?? 0,
                    ApprovedAmount = loan.LoanAmt,
                    Status = loan.Status switch
                    {
                        1 => "Pending",
                        2 => "Guarantors",
                        3 => "Appraisal",
                        4 => "Endorsement",
                        5 => "Approved",
                        6 => "Disbursed",
                        7 => "Rejected",
                        8 => "Closed",
                        _ => "Unknown"
                    },
                    ApplicationDate = loan.ApplicDate,
                    Purpose = loan.Purpose,
                    LoanBalance = loan.Loanbals.FirstOrDefault()?.Balance ?? 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loans for member {memberNo}");
                throw;
            }
        }

        public async Task<List<LoanListResponseDTO>> SearchLoansAsync(LoanSearchDTO searchCriteria)
        {
            try
            {
                // Only admins can search all loans
                if (!IsAdmin())
                    throw new UnauthorizedAccessException("Only administrators can search all loans");

                var companyCode = _companyContextService.GetCurrentCompanyCode();
                searchCriteria.CompanyCode = companyCode;

                var query = _context.Loans
                    .Include(l => l.Member)
                    .Include(l => l.LoanType)
                    .Include(l => l.Loanbals)
                    .Where(l => l.CompanyCode == companyCode);

                // Apply filters
                if (!string.IsNullOrEmpty(searchCriteria.MemberNo))
                    query = query.Where(l => l.MemberNo.Contains(searchCriteria.MemberNo));

                if (!string.IsNullOrEmpty(searchCriteria.LoanNo))
                    query = query.Where(l => l.LoanNo.Contains(searchCriteria.LoanNo));

                if (!string.IsNullOrEmpty(searchCriteria.LoanCode))
                    query = query.Where(l => l.LoanCode == searchCriteria.LoanCode);

                if (searchCriteria.Status.HasValue)
                    query = query.Where(l => l.Status == searchCriteria.Status.Value);

                if (searchCriteria.FromDate.HasValue)
                    query = query.Where(l => l.ApplicDate >= searchCriteria.FromDate.Value);

                if (searchCriteria.ToDate.HasValue)
                    query = query.Where(l => l.ApplicDate <= searchCriteria.ToDate.Value);

                var loans = await query
                    .OrderByDescending(l => l.ApplicDate)
                    .Take(100)
                    .ToListAsync();

                return loans.Select(loan => new LoanListResponseDTO
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = loan.Member != null ? $"{loan.Member.Surname} {loan.Member.OtherNames}" : loan.MemberNo,
                    LoanType = loan.LoanType?.LoanType1 ?? loan.LoanCode ?? "",
                    AppliedAmount = loan.LoanAmt ?? 0,
                    ApprovedAmount = loan.LoanAmt,
                    Status = loan.Status switch
                    {
                        1 => "Pending",
                        2 => "Guarantors",
                        3 => "Appraisal",
                        4 => "Endorsement",
                        5 => "Approved",
                        6 => "Disbursed",
                        7 => "Rejected",
                        8 => "Closed",
                        _ => "Unknown"
                    },
                    ApplicationDate = loan.ApplicDate,
                    Purpose = loan.Purpose,
                    LoanBalance = loan.Loanbals.FirstOrDefault()?.Balance ?? 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                throw;
            }
        }

        public async Task<bool> UpdateLoanStatusAsync(string loanNo, int status, string description, string updatedBy)
        {
            try
            {
                // Only admins can update loan status
                if (!IsAdmin())
                    throw new UnauthorizedAccessException("Only administrators can update loan status");

                var companyCode = _companyContextService.GetCurrentCompanyCode();

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {loanNo} not found");

                loan.Status = status;
                loan.StatusDescription = description;
                loan.AuditId = updatedBy ?? GetCurrentUserId();
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain transaction for status update
                var statusDescription = status switch
                {
                    1 => "Pending",
                    2 => "Guarantors",
                    3 => "Appraisal",
                    4 => "Endorsement",
                    5 => "Approved",
                    6 => "Disbursed",
                    7 => "Rejected",
                    8 => "Closed",
                    _ => "Unknown"
                };

                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    PreviousStatus = loan.Status,
                    NewStatus = status,
                    StatusDescription = statusDescription,
                    UpdateReason = description,
                    UpdatedBy = updatedBy ?? GetCurrentUserId(),
                    UpdatedByRole = "ADMIN",
                    UpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_STATUS_UPDATE",
                    loan.MemberNo,
                    companyCode,
                    0,
                    loanNo,
                    blockchainData
                );

                _logger.LogInformation($"Loan {loanNo} status updated to {statusDescription} by admin");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan status for {loanNo}");
                throw;
            }
        }

        public async Task<decimal> CalculateLoanBalanceAsync(string loanNo)
        {
            try
            {
                var loanBal = await _context.Loanbals
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                return loanBal?.Balance ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating loan balance for {loanNo}");
                throw;
            }
        }

        public async Task<LoanPortfolioDTO> GetLoanPortfolioReportAsync(string companyCode)
        {
            try
            {
                // Only admins can view portfolio reports
                if (!IsAdmin())
                    throw new UnauthorizedAccessException("Only administrators can view portfolio reports");

                var loans = await _context.Loans
                    .Include(l => l.Loanbals)
                    .Where(l => l.CompanyCode == companyCode)
                    .ToListAsync();

                var loanBals = await _context.Loanbals
                    .Where(lb => lb.Companycode == companyCode)
                    .ToListAsync();

                var report = new LoanPortfolioDTO
                {
                    TotalLoans = loans.Count,
                    ActiveLoans = loans.Count(l => l.Status == 5 || l.Status == 6),
                    PendingLoans = loans.Count(l => l.Status < 5 && l.Status > 0),
                    TotalLoanAmount = loans.Sum(l => l.LoanAmt ?? 0),
                    TotalLoanBalance = loanBals.Where(lb => !lb.Cleared).Sum(lb => lb.Balance),
                    TotalInterestAccrued = loanBals.Sum(lb => lb.IntrOwed + lb.InterestAccrued)
                };

                // Loan type distribution
                var loanTypes = await _context.Loantypes
                    .Where(lt => lt.CompanyCode == companyCode)
                    .ToListAsync();

                foreach (var loanType in loanTypes)
                {
                    var typeLoans = loans.Where(l => l.LoanCode == loanType.LoanCode);
                    report.LoanTypeDistribution[loanType.LoanType1 ?? loanType.LoanCode] =
                        typeLoans.Sum(l => l.LoanAmt ?? 0);
                }

                // Status distribution
                for (int i = 1; i <= 8; i++)
                {
                    report.StatusDistribution[i] = loans.Count(l => l.Status == i);
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating loan portfolio report for company {companyCode}");
                throw;
            }
        }

        public async Task<List<LoanGuarantorDTO>> GetLoanGuarantorsAsync(string loanNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                // Get loan to check authorization
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                    throw new ValidationException($"Loan {loanNo} not found");

                // Check authorization
                if (!IsAuthorizedForMember(loan.MemberNo))
                    throw new UnauthorizedAccessException("You are not authorized to view this loan's guarantors");

                var guarantors = await _context.Loanguars
                    .Where(g => g.LoanNo == loanNo && g.CompanyCode == companyCode)
                    .ToListAsync();

                return guarantors.Select(g => new LoanGuarantorDTO
                {
                    LoanNo = g.LoanNo ?? "",
                    GuarantorMemberNo = g.MemberNo,
                    Amount = g.Amount,
                    Collateral = g.Collateral,
                    Description = g.Description,
                    CompanyCode = g.CompanyCode,
                    ActionBy = g.AuditId
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting guarantors for loan {loanNo}");
                throw;
            }
        }

        public async Task<bool> RemoveGuarantorAsync(string loanNo, string guarantorMemberNo)
        {
            _logger.LogInformation($"Removing guarantor {guarantorMemberNo} from loan {loanNo}");

            // Only admins can remove guarantors
            if (!IsAdmin())
                throw new UnauthorizedAccessException("Only administrators can remove guarantors");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                var guarantor = await _context.Loanguars
                    .FirstOrDefaultAsync(g => g.LoanNo == loanNo &&
                                             g.MemberNo == guarantorMemberNo &&
                                             g.CompanyCode == companyCode);

                if (guarantor == null)
                    throw new ValidationException($"Guarantor {guarantorMemberNo} not found for loan {loanNo}");

                _context.Loanguars.Remove(guarantor);

                // Check if there are still guarantors
                var remainingGuarantors = await _context.Loanguars
                    .CountAsync(g => g.LoanNo == loanNo && g.CompanyCode == companyCode);

                // Update loan status if no guarantors remain
                if (remainingGuarantors == 0)
                {
                    var loan = await _context.Loans
                        .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                    if (loan != null && loan.Status == 2)
                    {
                        loan.Status = 1;
                        loan.StatusDescription = "All guarantors removed by admin";
                        loan.AuditTime = DateTime.Now;
                        loan.AuditDateTime = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    GuarantorMemberNo = guarantorMemberNo,
                    RemovedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    RemovedBy = "ADMIN",
                    RemainingGuarantors = remainingGuarantors,
                    GuaranteeAmount = guarantor.Amount
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_GUARANTOR_REMOVED",
                    guarantorMemberNo,
                    companyCode,
                    0,
                    $"{loanNo}_{guarantorMemberNo}_REMOVED",
                    blockchainData
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Guarantor {guarantorMemberNo} removed from loan {loanNo} by admin");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error removing guarantor from loan {loanNo}");
                throw;
            }
        }

        #region Helper Methods

        private string GenerateLoanNumber(string companyCode)
        {
            var year = DateTime.Now.ToString("yy");
            var month = DateTime.Now.ToString("MM");

            // Get last loan number for this company
            var lastLoan = _context.Loans
                .Where(l => l.CompanyCode == companyCode && l.LoanNo.StartsWith($"{companyCode}LN{year}{month}"))
                .OrderByDescending(l => l.LoanNo)
                .FirstOrDefault();

            int sequence = 1;
            if (lastLoan != null)
            {
                var lastSequence = lastLoan.LoanNo.Length >= 4 ?
                    lastLoan.LoanNo.Substring(lastLoan.LoanNo.Length - 4) : "0001";
                if (int.TryParse(lastSequence, out int lastNum))
                {
                    sequence = lastNum + 1;
                }
            }

            return $"{companyCode}LN{year}{month}{sequence:0000}";
        }

        private async Task<List<LoanTypeEligibilityDTO>> GetEligibleLoanTypesForMemberAsync(string memberNo, string companyCode)
        {
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode)
                .ToListAsync();

            var memberShares = await _context.Shares
                .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                .SumAsync(s => s.TotalShares ?? 0);

            var memberGuarantee = await _context.Loanguars
                .Where(g => g.MemberNo == memberNo && g.CompanyCode == companyCode)
                .SumAsync(g => g.Amount ?? 0);

            var result = new List<LoanTypeEligibilityDTO>();

            foreach (var loanType in loanTypes)
            {
                // Calculate eligible amount
                decimal eligibleAmount = 0;
                var isEligible = true;
                var reason = "";

                // Check based on earning ratio
                if (loanType.EarningRation.HasValue && loanType.EarningRation.Value > 0)
                {
                    eligibleAmount = memberShares * (decimal)loanType.EarningRation.Value;
                }
                else
                {
                    // Check based on guarantee capacity (3x shares ratio)
                    var availableGuarantee = (memberShares * 3) - memberGuarantee;
                    eligibleAmount = availableGuarantee;
                }

                // Check max amount limit
                if (loanType.MaxAmount.HasValue && eligibleAmount > loanType.MaxAmount.Value)
                {
                    eligibleAmount = loanType.MaxAmount.Value;
                }

                // Check minimum amount (if any)
                if (eligibleAmount < 100)
                {
                    isEligible = false;
                    reason = "Insufficient shares/guarantee capacity";
                }

                result.Add(new LoanTypeEligibilityDTO
                {
                    LoanCode = loanType.LoanCode,
                    LoanType = loanType.LoanType1 ?? loanType.LoanCode,
                    MaxAmount = loanType.MaxAmount,
                    EligibleAmount = eligibleAmount,
                    RepayPeriod = loanType.RepayPeriod,
                    Interest = loanType.Interest,
                    IsEligible = isEligible,
                    Reason = reason
                });
            }

            return result;
        }

        private async Task CreateRepaymentScheduleAsync(Loan loan, DateTime disbursementDate)
        {
            if (loan.LoanAmt == null || loan.RepayPeriod == null) return;

            var loanBal = await _context.Loanbals
                .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo);

            if (loanBal != null)
            {
                loanBal.RepayPeriod = loan.RepayPeriod.Value;
                loanBal.Installments = loan.LoanAmt.Value / loan.RepayPeriod.Value;
                loanBal.RepayRate = loan.LoanAmt.Value / loan.RepayPeriod.Value;
                loanBal.RepayRate2 = loan.LoanAmt.Value / loan.RepayPeriod.Value;
            }
        }

        private string GenerateTransactionNumber()
        {
            return $"TXN{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private string GenerateReceiptNumber()
        {
            return $"RCP{DateTime.Now:yyyyMMdd}{new Random().Next(10000, 99999)}";
        }

        private int GetNextLoanguarId()
        {
            var maxId = _context.Loanguars.Max(g => (int?)g.Id) ?? 0;
            return maxId + 1;
        }

        #endregion
    }
}