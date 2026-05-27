using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoanApiController : ControllerBase
    {
        private readonly ILoanService _loanService;
        private readonly IMemberService _memberService; 
        private readonly IContributionService _contributionService;
        private readonly IShareService _shareService;
        private readonly ILoanTypeService _loanTypeService;
        private readonly ILogger<LoanApiController> _logger;
        private readonly ApplicationDbContext _context;  

        public LoanApiController(
            ILoanService loanService,
            IMemberService memberService,
            IContributionService contributionService,
            IShareService shareService,
            ILoanTypeService loanTypeService,
            ILogger<LoanApiController> logger,
            ApplicationDbContext context)  
        {
            _loanService = loanService;
            _memberService = memberService;
            _contributionService = contributionService;
            _shareService = shareService;
            _loanTypeService = loanTypeService;
            _logger = logger;
            _context = context;  
        }

        [HttpGet("validate-guarantor")]
        public async Task<IActionResult> ValidateGuarantor(string memberNo, string loanNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Validating guarantor - MemberNo: {memberNo}, LoanNo: {loanNo}, CompanyCode: {companyCode}");

                // Get the loan details
                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);
                if (loan == null)
                {
                    return Ok(new { success = false, message = "Loan not found" });
                }

                // Get guarantor member details
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return Ok(new { success = false, message = "Guarantor member not found" });
                }

                // Check if member is active
                bool isWithdrawn = member.Withdrawn ?? false;
                bool isArchived = member.Archived ?? false;
                int isDormant = member.Dormant ?? 0;

                if (isWithdrawn || isArchived || isDormant == 1)
                {
                    return Ok(new { success = false, message = "Guarantor is not an active member" });
                }

                // ============================================================
                // CRITICAL FIX: USE EXACT SAME LOGIC AS STAGE 1 ELIGIBILITY
                // ============================================================

                // 1. PRIMARY SOURCE: GET DEPOSITSAMOUNT FROM CONTRIBSHARE TABLE
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                _logger.LogInformation($"Member {memberNo}: ContribShares DepositsAmount = {totalDeposits:C}");

                // 2. FALLBACK: ONLY if no DepositsAmount found, check Shares table with UsedToGuarantee = true
                // THIS MATCHES STAGE 1 LOGIC EXACTLY
                if (totalDeposits <= 0)
                {
                    _logger.LogInformation($"No DepositsAmount found for guarantor {memberNo}, falling back to Shares table with UsedToGuarantee=true");

                    // Get share types that can be used for guarantee (matches Stage 1)
                    var validShareTypes = await _context.Sharetypes
                        .Where(s => s.CompanyCode == companyCode &&
                                   (s.UsedToGuarantee == true || s.UsedToOffset == true) &&
                                   s.Withdrawable == true)
                        .ToListAsync();

                    if (validShareTypes.Any())
                    {
                        foreach (var shareType in validShareTypes)
                        {
                            var memberShares = await _context.Shares
                                .Where(s => s.MemberNo == memberNo &&
                                           s.Sharescode == shareType.SharesCode &&
                                           s.CompanyCode == companyCode)
                                .SumAsync(s => s.TotalShares ?? 0);

                            if (memberShares > 0)
                            {
                                totalDeposits += memberShares;
                                _logger.LogInformation($"Added {shareType.SharesType}: {memberShares:C} from Shares table (UsedToGuarantee={shareType.UsedToGuarantee})");
                            }
                        }
                    }
                    else
                    {
                        // Last resort: get all shares (matches Stage 1 fallback)
                        var allShares = await _context.Shares
                            .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                            .SumAsync(s => s.TotalShares ?? 0);

                        if (allShares > 0)
                        {
                            totalDeposits = allShares;
                            _logger.LogWarning($"Using all shares as legacy fallback for guarantor: {allShares:C}");
                        }
                    }
                }

                _logger.LogInformation($"Member {memberNo}: Total Eligible Amount for Guarantee = {totalDeposits:C}");

                if (totalDeposits <= 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Member has no eligible savings/deposits. Total eligible: {totalDeposits:C}. Please make a deposit or ensure share type has UsedToGuarantee=true before becoming a guarantor."
                    });
                }

                // 2. GET ACTIVE LOANS ONLY (not closed/rejected/written off)
                var activeLoanNos = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                // 3. GET AMOUNT LOCKED FOR ACTIVE LOANS ONLY
                var lockedAmount = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo &&
                               g.CompanyCode == companyCode &&
                               g.Transfered == false &&
                               activeLoanNos.Contains(g.LoanNo))
                    .SumAsync(g => g.Balance ?? g.Amount ?? 0);

                var availableDeposits = totalDeposits - lockedAmount;

                _logger.LogInformation($"Guarantor {memberNo}: Total={totalDeposits:C}, Locked={lockedAmount:C}, Available={availableDeposits:C}");

                // 4. CHECK MINIMUM REQUIREMENT
                var saccoParams = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var minDepositRequirement = saccoParams?.MinGuarantor ?? 0;

                if (minDepositRequirement > 0 && availableDeposits < minDepositRequirement)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Member's available deposits ({availableDeposits:C}) is below minimum requirement of {minDepositRequirement:C}"
                    });
                }

                // 5. CALCULATE MAX GUARANTEE AMOUNT
                var existingLoanGuarantees = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var currentLoanGuaranteeTotal = existingLoanGuarantees.Sum(g => g.GuaranteeAmount);
                var remainingLoanAmount = (loan.LoanAmt ?? 0) - currentLoanGuaranteeTotal;
                var maxGuarantee = Math.Min(availableDeposits, remainingLoanAmount);

                if (maxGuarantee <= 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Cannot guarantee this loan. Available: {availableDeposits:C}, Remaining loan amount: {remainingLoanAmount:C}"
                    });
                }

                // Get member name
                var memberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                if (string.IsNullOrEmpty(memberName))
                {
                    memberName = member.MemberNo;
                }

                var responseData = new
                {
                    memberNo = member.MemberNo,
                    name = memberName,
                    idNo = member.Idno ?? "N/A",
                    totalDeposits = totalDeposits,
                    lockedAmount = lockedAmount,
                    availableAmount = availableDeposits,
                    maxGuarantee = maxGuarantee,
                    currentLoanGuaranteeTotal = currentLoanGuaranteeTotal,
                    loanAmount = (loan.LoanAmt ?? 0),
                    remainingLoanAmount = remainingLoanAmount,
                    isSelfGuarantee = memberNo == loan.MemberNo
                };

                string successMessage = $"Member is eligible to guarantee up to KES {maxGuarantee:N0} using their savings/deposits. Available: KES {availableDeposits:N0}";

                return Ok(new
                {
                    success = true,
                    message = successMessage,
                    data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating guarantor - MemberNo: {memberNo}, LoanNo: {loanNo}");
                return Ok(new { success = false, message = $"An error occurred while validating guarantor: {ex.Message}" });
            }
        }

        [HttpGet("get-max-guarantors")]
        public async Task<IActionResult> GetMaxGuarantors(string companyCode)
        {
            try
            {
                // This would come from your SaccoParram service
                // For now, return default
                return Ok(new { success = true, maxGuarantors = 5 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting max guarantors");
                return Ok(new { success = false, message = ex.Message });
            }
        }


        [HttpGet("check-eligibility")]
        public async Task<IActionResult> CheckEligibility(string memberNo, string companyCode)
        {
            try
            {
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return Ok(new { success = false, message = "Member not found" });
                }

                var totalShares = await _shareService.GetTotalSharesValueAsync(memberNo);
                var existingGuarantees = await _loanService.GetGuarantorTotalGuaranteesAsync(memberNo, companyCode);
                var availableShares = totalShares - existingGuarantees;

                var memberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                if (string.IsNullOrEmpty(memberName))
                {
                    memberName = member.MemberNo;
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        memberNo = member.MemberNo,
                        name = memberName,
                        idNo = member.Idno ?? "N/A",
                        totalShares = totalShares,
                        existingGuarantees = existingGuarantees,
                        availableShares = availableShares,
                        isActive = !(member.Withdrawn ?? false) && !(member.Archived ?? false) && (member.Dormant ?? 0) != 1
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking eligibility");
                return Ok(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchMembers([FromQuery] string searchTerm, [FromQuery] string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTerm) || searchTerm.Length < 3)
                {
                    return Ok(new { success = false, message = "Please enter at least 3 characters" });
                }

                var query = _context.Members
                    .Where(m => m.CompanyCode == companyCode && m.Withdrawn != true && m.Archived != true);

                query = query.Where(m =>
                    m.MemberNo.Contains(searchTerm) ||
                    (m.Surname + " " + m.OtherNames).Contains(searchTerm) ||
                    m.Idno.Contains(searchTerm) ||
                    m.PhoneNo.Contains(searchTerm) ||
                    m.MobileNo.Contains(searchTerm)
                );

                var members = await query
                    .Select(m => new
                    {
                        m.MemberNo,
                        m.Surname,
                        m.OtherNames,
                        m.Idno,
                        m.PhoneNo,
                        m.MobileNo,
                        m.Status,
                        m.Withdrawn,
                        m.Archived,
                        m.Dormant
                    })
                    .Take(50)
                    .ToListAsync();

                // Get all active loans once for efficiency
                var activeLoanNos = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                var result = new List<object>();

                foreach (var member in members)
                {
                    // Get total deposits from ContribShares - PRIMARY SOURCE
                    var totalDeposits = await _context.ContribShares
                        .Where(cs => cs.MemberNo == member.MemberNo && cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    // FALLBACK: Only if no DepositsAmount found
                    if (totalDeposits <= 0)
                    {
                        var validShareTypes = await _context.Sharetypes
                            .Where(s => s.CompanyCode == companyCode &&
                                       (s.UsedToGuarantee == true || s.UsedToOffset == true) &&
                                       s.Withdrawable == true)
                            .ToListAsync();

                        if (validShareTypes.Any())
                        {
                            foreach (var shareType in validShareTypes)
                            {
                                var memberShares = await _context.Shares
                                    .Where(s => s.MemberNo == member.MemberNo &&
                                               s.Sharescode == shareType.SharesCode &&
                                               s.CompanyCode == companyCode)
                                    .SumAsync(s => s.TotalShares ?? 0);
                                totalDeposits += memberShares;
                            }
                        }
                    }

                    // Get locked amount for active loans only
                    var lockedAmount = await _context.Loanguar
                        .Where(g => g.MemberNo == member.MemberNo &&
                                   g.CompanyCode == companyCode &&
                                   g.Transfered == false &&
                                   activeLoanNos.Contains(g.LoanNo))
                        .SumAsync(g => g.Balance ?? g.Amount ?? 0);

                    var availableDeposits = totalDeposits - lockedAmount;

                    result.Add(new
                    {
                        member.MemberNo,
                        Surname = member.Surname ?? "",
                        OtherNames = member.OtherNames ?? "",
                        FullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim(),
                        IdNo = member.Idno ?? "",
                        PhoneNo = member.PhoneNo ?? member.MobileNo ?? "",
                        Status = (member.Withdrawn == true || member.Archived == true || member.Dormant == 1) ? 0 : 1,
                        TotalDeposits = totalDeposits,
                        LockedAmount = lockedAmount,
                        AvailableDeposits = availableDeposits,
                        IsActive = member.Withdrawn != true && member.Archived != true && member.Dormant != 1
                    });
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return Ok(new { success = false, message = ex.Message });
            }
        }

        private async Task<decimal> CalculateAvailableDepositsForGuaranteeAsync(string memberNo, string companyCode)
        {
            try
            {
                // 1. Get total deposits from ContribShares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                _logger.LogInformation($"Member {memberNo}: Total Deposits = {totalDeposits:C}");

                if (totalDeposits <= 0)
                    return 0;

                // 2. Get ALL active loans (not Closed, Rejected, or WrittenOff)
                var activeLoanNos = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                _logger.LogInformation($"Active loans count: {activeLoanNos.Count}");

                // 3. Get amount locked as guarantor for active loans ONLY
                // This is CRITICAL - only count guarantor commitments for loans that are STILL ACTIVE
                var lockedAmount = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo &&
                               g.CompanyCode == companyCode &&
                               g.Transfered == false &&
                               activeLoanNos.Contains(g.LoanNo))
                    .SumAsync(g => g.Balance ?? g.Amount ?? 0);

                var availableAmount = totalDeposits - lockedAmount;

                _logger.LogInformation($"Member {memberNo}: Total Deposits={totalDeposits:C}, Locked={lockedAmount:C}, Available={availableAmount:C}");

                // Ensure we never return negative
                return Math.Max(0, availableAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating available deposits for {memberNo}");
                return 0;
            }
        }
    }
}