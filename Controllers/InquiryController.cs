// Controllers/InquiryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class InquiryController : Controller
    {
        private readonly IInquiryService _inquiryService;
        private readonly IMemberService _memberService;
        private readonly IUserService _userService;
        private readonly ILogger<InquiryController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ICompanyContextService _companyContextService;

        public InquiryController(
            IInquiryService inquiryService,
            IMemberService memberService,
            IUserService userService,
            ILogger<InquiryController> logger,
            ApplicationDbContext context,
            ICompanyContextService companyContextService)
        {
            _inquiryService = inquiryService;
            _memberService = memberService;
            _userService = userService;
            _logger = logger;
            _context = context;
            _companyContextService = companyContextService;
        }
        private async Task<string> GetCurrentUserCompanyCodeAsync()
        {
            // Try to get from claims first
            var companyCodeClaim = User.FindFirst("CompanyCode")?.Value
                                   ?? User.FindFirst("companyCode")?.Value;

            if (!string.IsNullOrEmpty(companyCodeClaim))
                return companyCodeClaim;

            // Try from session
            var sessionCompanyCode = HttpContext.Session.GetString("CompanyCode");
            if (!string.IsNullOrEmpty(sessionCompanyCode))
                return sessionCompanyCode;

            // Fallback: get username and then fetch from database
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user != null && !string.IsNullOrEmpty(user.CompanyCode))
                    return user.CompanyCode;
            }

            // Final fallback: use company context service
            return _companyContextService.GetCurrentCompanyCode();
        }

        private string GetUserCompanyCode()
        {
            try
            {
                // First try to get from claims
                var companyCodeClaim = User.FindFirst("CompanyCode")?.Value;
                if (!string.IsNullOrEmpty(companyCodeClaim))
                {
                    return companyCodeClaim.Trim();
                }

                // Try from session
                var sessionCompanyCode = HttpContext.Session.GetString("CompanyCode");
                if (!string.IsNullOrEmpty(sessionCompanyCode))
                {
                    return sessionCompanyCode;
                }

                // Try from company context service
                var contextCompanyCode = _companyContextService.GetCurrentCompanyCode();
                if (!string.IsNullOrEmpty(contextCompanyCode))
                {
                    return contextCompanyCode;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company code");
                return null;
            }
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim))
                return userIdClaim;

            return User.Identity?.Name ?? "Unknown";
        }

        private string GetCurrentUsername()
        {
            return User.Identity?.Name ?? "Unknown";
        }

        // GET: Inquiry/Index
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // GET: Inquiry/MemberInquiry
        public IActionResult MemberInquiry()
        {
            return View(new MemberSearchDTO());
        }

        // POST: Inquiry/MemberInquiry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MemberInquiry(MemberSearchDTO searchDto)
        {
            if (!ModelState.IsValid)
            {
                return View(searchDto);
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                var result = await _inquiryService.SearchMembersAsync(searchDto, companyCode, userId);
                return View("MemberSearchResults", result);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(searchDto);
            }
        }

        // GET: api/Inquiry/share/{memberNo}
        [HttpGet("api/Inquiry/share/{memberNo}")]
        public async Task<IActionResult> GetShareInquiryApi(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Unable to determine company code" });
                }

                var result = await _inquiryService.GetShareInquiryAsync(memberNo, companyCode, userId);

                if (result == null || result.ShareTypeSummaries == null || !result.ShareTypeSummaries.Any())
                {
                    return Ok(new
                    {
                        Success = true,
                        Data = new
                        {
                            memberNo = memberNo,
                            memberName = "No data found",
                            totalShareBalance = 0,
                            totalShareCapital = 0,
                            totalDeposits = 0,
                            lockedForGuarantees = 0,
                            availableShares = 0,
                            shareTypeSummaries = new List<object>()
                        },
                        Message = "No share data found for this member"
                    });
                }

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting share inquiry for {MemberNo}", memberNo);
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }


        // GET: api/Inquiry/loan/{memberNo}
        [HttpGet("api/Inquiry/loan/{memberNo}")]
        public async Task<IActionResult> GetLoanInquiryApi(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Unable to determine company code" });
                }

                var result = await _inquiryService.GetLoanInquiryAsync(memberNo, companyCode, userId);

                if (result == null || result.Loans == null || !result.Loans.Any())
                {
                    return Ok(new
                    {
                        Success = true,
                        Data = new
                        {
                            memberNo = memberNo,
                            memberName = "No data found",
                            totalLoanAmount = 0,
                            activeLoanCount = 0,
                            closedLoanCount = 0,
                            totalOutstandingBalance = 0,
                            loans = new List<object>()
                        },
                        Message = "No loan data found for this member"
                    });
                }

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loan inquiry for {MemberNo}", memberNo);
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }


        // GET: api/Inquiry/loan/{memberNo}/{loanNo}
        [HttpGet("api/Inquiry/loan/{memberNo}/{loanNo}")]
        public async Task<IActionResult> GetLoanDetailsApi(string memberNo, string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Unable to determine company code" });
                }

                // Get loan details
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    return Ok(new { Success = false, Message = "Loan not found" });
                }

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                // Get guarantors
                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.CompanyCode == companyCode)
                    .ToListAsync();

                // Get cheques
                var cheques = await _context.Cheques
                    .Where(c => c.LoanNo == loanNo && c.CompanyCode == companyCode)
                    .ToListAsync();

                // Get endmain (meeting approval)
                var endmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                var result = new
                {
                    loanNo = loan.LoanNo,
                    loanCode = loan.LoanCode,
                    loanType = loan.LoanCode,
                    memberNo = loan.MemberNo,
                    memberName = member != null ? $"{member.Surname} {member.OtherNames}" : loan.MemberNo,
                    applicDate = loan.ApplicDate,
                    loanAmt = loan.LoanAmt,
                    aamount = loan.Aamount,
                    interest = loan.Interest,
                    repayPeriod = loan.RepayPeriod,
                    premiumPayable = loan.PremiumPayable,
                    insurance = loan.Insurance,
                    status = loan.Status,
                    purpose = loan.Purpose,
                    sourceofrepayment = loan.Sourceofrepayment,
                    repayMethod = loan.RepayMethod,
                    preparedBy = loan.PreparedBy,
                    addSecurity = loan.AddSecurity,
                    blockchainTxId = loan.BlockchainTxId,
                    guarantors = guarantors.Select(g => new
                    {
                        g.MemberNo,
                        g.FullNames,
                        g.Amount,
                        g.Balance,
                        g.Collateral,
                        g.BlockchainTxId
                    }),
                    cheques = cheques.Select(c => new
                    {
                        c.ChequeNo,
                        c.Amount,
                        c.Balance,
                        c.DateIssued,
                        c.Status,
                        c.CollectorName,
                        c.CollectorId
                    }),
                    endmain = endmain != null ? new
                    {
                        endmain.MinuteNo,
                        endmain.MeetingDate,
                        endmain.AmtApproved,
                        endmain.Accepted,
                        endmain.ChairSigned,
                        endmain.SecSigned,
                        endmain.MembSigned,
                        endmain.Reasons,
                        endmain.Remarks
                    } : null
                };

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loan details for {LoanNo}", loanNo);
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // GET: Inquiry/MemberSearchResults (for pagination)
        public async Task<IActionResult> MemberSearchResults(
            string? memberNo, string? fullName, string? idNo, string? phoneNo,
            string? email, string? department, string? station, short? status,
            DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var searchDto = new MemberSearchDTO
            {
                MemberNo = memberNo,
                FullName = fullName,
                IdNo = idNo,
                PhoneNo = phoneNo,
                Email = email,
                Department = department,
                Station = station,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = 20
            };

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                var result = await _inquiryService.SearchMembersAsync(searchDto, companyCode, userId);
                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(MemberInquiry));
            }
        }

        // GET: Inquiry/MemberDetails/{memberNo}
        public async Task<IActionResult> MemberDetails(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                return RedirectToAction(nameof(MemberInquiry));
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                var result = await _inquiryService.GetMemberInquiryAsync(memberNo, companyCode, userId);
                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(MemberInquiry));
            }
        }

        // GET: Inquiry/ShareInquiry
        public IActionResult ShareInquiry()
        {
            return View();
        }

        // POST: Inquiry/ShareInquiry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareInquiry(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                TempData["Error"] = "Member Number is required";
                return RedirectToAction(nameof(ShareInquiry));
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["Error"] = "Unable to determine company code";
                    return RedirectToAction(nameof(ShareInquiry));
                }

                var result = await _inquiryService.GetShareInquiryAsync(memberNo, companyCode, userId);

                if (result == null || result.ShareTypeSummaries == null || !result.ShareTypeSummaries.Any())
                {
                    TempData["Error"] = $"No share data found for member {memberNo}";
                    return RedirectToAction(nameof(ShareInquiry));
                }

                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(ShareInquiry));
            }
        }

        // GET: Inquiry/LoanInquiry
        public IActionResult LoanInquiry()
        {
            return View();
        }

        // POST: Inquiry/LoanInquiry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoanInquiry(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                TempData["Error"] = "Member Number is required";
                return RedirectToAction(nameof(LoanInquiry));
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                var result = await _inquiryService.GetLoanInquiryAsync(memberNo, companyCode, userId);
                return View("LoanInquiryResult", result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(LoanInquiry));
            }
        }

        // GET: Inquiry/TransactionInquiry
        public IActionResult TransactionInquiry()
        {
            return View();
        }

        // POST: Inquiry/TransactionInquiry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransactionInquiry(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                TempData["Error"] = "Member Number is required";
                return RedirectToAction(nameof(TransactionInquiry));
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                var result = await _inquiryService.GetTransactionInquiryAsync(memberNo, companyCode, userId);
                return View("TransactionInquiryResult", result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(TransactionInquiry));
            }
        }


        [HttpGet("api/Member/search")]
        public async Task<IActionResult> SearchMembers([FromQuery] string searchTerm)
        {
            try
            {
                var members = await _memberService.SearchMembersAsync(searchTerm);
                return Ok(new
                {
                    Success = true,
                    Data = members.Select(m => new
                    {
                        m.MemberNo,
                        m.Surname,
                        m.OtherNames,
                        m.Idno,
                        m.PhoneNo,
                        m.CompanyCode,
                        m.Status
                    }),
                    Count = members.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while searching members",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("api/Member/{memberNo}")]
        public async Task<IActionResult> GetMember(string memberNo)
        {
            try
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == GetUserCompanyCode());

                if (member == null)
                {
                    return NotFound(new { Success = false, Message = "Member not found" });
                }

                return Ok(new
                {
                    Success = true,
                    member = new
                    {
                        member.MemberNo,
                        member.Surname,
                        member.OtherNames,
                        member.Idno,
                        member.PhoneNo,
                        member.Email,
                        member.Status,
                        member.CompanyCode
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("api/Loan/member/{memberNo}/inquiry")]
        public async Task<IActionResult> GetLoanInquiry(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                var result = new
                {
                    memberNo = memberNo,
                    memberName = member != null ? $"{member.Surname} {member.OtherNames}" : memberNo,
                    totalLoanAmount = loans.Sum(l => l.LoanAmt ?? 0),
                    activeLoanCount = loans.Count(l => l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed),
                    closedLoanCount = loans.Count(l => l.Status == (int)Status.Closed),
                    totalOutstandingBalance = loans.Sum(l => l.Aamount ?? 0),
                    loans = loans.Select(l => new
                    {
                        l.LoanNo,
                        l.LoanAmt,
                        l.Aamount,
                        l.Status,
                        l.ApplicDate,
                        l.LoanCode,
                        l.Interest,
                        l.RepayPeriod
                    })
                };

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        #region Guarantor Loans

        // GET: Inquiry/GuarantorLoans
        public async Task<IActionResult> GuarantorLoans(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                TempData["Error"] = "Member Number is required";
                return RedirectToAction("MemberInquiry");
            }

            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["Error"] = "Unable to determine company code";
                    return RedirectToAction("MemberInquiry");
                }

                var result = await GetGuarantorLoansDataAsync(memberNo, companyCode, userId);

                if (result == null || result.GuaranteedLoans == null || !result.GuaranteedLoans.Any())
                {
                    TempData["Info"] = $"Member {memberNo} is not a guarantor for any active loans.";
                    return RedirectToAction("MemberDetails", new { memberNo });
                }

                return View(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guarantor loans for {MemberNo}", memberNo);
                TempData["Error"] = ex.Message;
                return RedirectToAction("MemberInquiry");
            }
        }

        // GET: api/Inquiry/guarantor/{memberNo}
        [HttpGet("api/Inquiry/guarantor/{memberNo}")]
        public async Task<IActionResult> GetGuarantorLoansApi(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Unable to determine company code" });
                }

                var result = await GetGuarantorLoansDataAsync(memberNo, companyCode, userId);

                if (result == null || result.GuaranteedLoans == null || !result.GuaranteedLoans.Any())
                {
                    return Ok(new
                    {
                        Success = true,
                        Data = result,
                        Message = "No guaranteed loans found for this member"
                    });
                }

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guarantor loans for {MemberNo}", memberNo);
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Gets all guarantor loans data for a member
        /// </summary>
        private async Task<GuarantorLoanListResponseDTO> GetGuarantorLoansDataAsync(string memberNo, string companyCode, string userId)
        {
            // 1. Get the member (guarantor) details
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                throw new Exception($"Member {memberNo} not found");
            }

            // 2. Get ALL guarantor records for this member (where they are a guarantor)
            var guarantorRecords = await _context.Loanguar
                .Where(g => g.MemberNo == memberNo
                    && g.CompanyCode == companyCode
                    && g.Transfered == false) // Only active guarantees
                .ToListAsync();

            var guaranteedLoans = new List<GuarantorLoanDetailDTO>();
            decimal totalLockedAmount = 0;

            // 3. For each guarantor record, get the loan details
            foreach (var guarantor in guarantorRecords)
            {
                // Get the loan that was guaranteed
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo && l.CompanyCode == companyCode);

                if (loan == null) continue;

                // Get the loanee (borrower) details
                var loanee = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get loan balance
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                // Calculate outstanding balances
                decimal outstandingBalance = loanbal?.Balance ?? 0;
                decimal outstandingInterest = loanbal?.IntrOwed ?? 0;
                decimal outstandingPenalty = loanbal?.Penalty ?? 0;
                decimal totalOutstanding = outstandingBalance + outstandingInterest + outstandingPenalty;

                // Calculate expected completion date
                DateTime? expectedCompletionDate = null;
                if (loanbal?.FirstDate != null && loan.RepayPeriod.HasValue)
                {
                    expectedCompletionDate = loanbal.FirstDate.AddMonths(loan.RepayPeriod.Value);
                }
                else if (loan.ApplicDate != null && loan.RepayPeriod.HasValue)
                {
                    expectedCompletionDate = loan.ApplicDate.AddMonths(loan.RepayPeriod.Value);
                }

                // Check if loan is active
                bool isActive = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;

                // Check if loan is overdue
                bool isOverdue = false;
                int daysOverdue = 0;
                if (loanbal?.Nextduedate != null && loanbal.Nextduedate < DateTime.Now && outstandingBalance > 0)
                {
                    isOverdue = true;
                    daysOverdue = (DateTime.Now - loanbal.Nextduedate.Value).Days;
                }

                // Get the loan status string
                string loanStatus = GetLoanStatusString(loan.Status);

                // Calculate remaining guarantee
                decimal remainingGuarantee = (guarantor.Balance ?? 0);
                totalLockedAmount += remainingGuarantee;

                // Get member's total shares (the guarantor's shares)
                var memberShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0));

                // Get all locked shares for this member (from ALL loans they guarantee)
                var totalLockedForMember = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo
                        && g.CompanyCode == companyCode
                        && g.Transfered == false)
                    .SumAsync(g => g.Balance ?? 0);

                // Calculate available shares after this guarantee
                decimal availableShares = memberShares - totalLockedForMember;
                decimal shareBalanceAfterThisGuarantee = memberShares - totalLockedForMember;

                // Build the detail DTO
                var detail = new GuarantorLoanDetailDTO
                {
                    // Loanee (Borrower) Information
                    LoaneeMemberNo = loan.MemberNo,
                    LoaneeName = loanee != null ? $"{loanee.Surname ?? ""} {loanee.OtherNames ?? ""}".Trim() : loan.MemberNo,
                    LoaneePhone = loanee?.PhoneNo ?? loanee?.MobileNo ?? "N/A",
                    LoaneeIdNo = loanee?.Idno ?? "N/A",

                    // Loan Information
                    LoanNo = loan.LoanNo,
                    LoanCode = loan.LoanCode ?? "N/A",
                    LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                    PrincipalAmount = loan.LoanAmt ?? 0,
                    OutstandingBalance = totalOutstanding,
                    InterestRate = loan.Interest ?? 0,
                    RepaymentPeriod = loan.RepayPeriod ?? 0,
                    ApplicationDate = loan.ApplicDate,
                    DisbursementDate = loan.AuditDateTime,
                    ExpectedCompletionDate = expectedCompletionDate,
                    LoanStatus = loanStatus,
                    IsActive = isActive,
                    IsOverdue = isOverdue,
                    DaysOverdue = daysOverdue,

                    // Guarantor Information
                    GuarantorId = guarantor.Id,
                    GuarantorMemberNo = guarantor.MemberNo,
                    GuaranteeAmount = guarantor.Amount ?? 0,
                    GuaranteeBalance = guarantor.Balance ?? 0,
                    RemainingGuarantee = remainingGuarantee,
                    GuaranteeDate = guarantor.AuditTime ?? DateTime.Now,
                    GuaranteeStatus = guarantor.Transfered ? "Released" : "Active",
                    GuaranteeDescription = guarantor.Description,
                    CollateralType = guarantor.Collateral,

                    // Member's Share Information (the guarantor)
                    MemberTotalShares = memberShares,
                    MemberLockedShares = totalLockedForMember,
                    MemberAvailableShares = availableShares,
                    ShareBalanceAfterThisGuarantee = shareBalanceAfterThisGuarantee
                };

                guaranteedLoans.Add(detail);
            }

            // 4. Build the response
            var response = new GuarantorLoanListResponseDTO
            {
                MemberNo = member.MemberNo,
                MemberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim(),
                MemberPhone = member.PhoneNo ?? member.MobileNo ?? "N/A",
                MemberIdNo = member.Idno ?? "N/A",
                TotalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0)),
                LockedForGuarantees = totalLockedAmount,
                AvailableShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0)) - totalLockedAmount,
                TotalGuaranteedLoans = guaranteedLoans.Count,
                GuaranteedLoans = guaranteedLoans,
                InquiryTimestamp = DateTime.Now,
                InquiredBy = userId
            };

            return response;
        }

        /// <summary>
        /// Gets the loan status string from the status code
        /// </summary>
        private string GetLoanStatusString(int? status)
        {
            return status switch
            {
                1 => "Draft",
                2 => "Submitted",
                3 => "Under Appraisal",
                4 => "Approved",
                5 => "Endorsed",
                6 => "Disbursed",
                7 => "Closed",
                8 => "Defaulted",
                9 => "Written Off",
                10 => "Rejected",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Export guarantor loans to PDF - opens in new tab
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportGuarantorLoansToPdf(string memberNo)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member Number is required";
                    return RedirectToAction("MemberInquiry");
                }

                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var userId = GetCurrentUserId();

                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["Error"] = "Unable to determine company code";
                    return RedirectToAction("MemberInquiry");
                }

                // Get the guarantor loans data
                var data = await GetGuarantorLoansDataAsync(memberNo, companyCode, userId);

                if (data == null || data.GuaranteedLoans == null || !data.GuaranteedLoans.Any())
                {
                    TempData["Error"] = "No guaranteed loans found for this member";
                    return RedirectToAction("GuarantorLoans", new { memberNo });
                }

                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                // Generate PDF using QuestPDF
                using var stream = new MemoryStream();

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.MarginTop(1.5f, Unit.Centimetre);
                        page.MarginBottom(1.5f, Unit.Centimetre);
                        page.MarginLeft(1.2f, Unit.Centimetre);
                        page.MarginRight(1.2f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                        // Header
                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text($"GUARANTOR LOANS REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Member: {data.MemberName} ({data.MemberNo})").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        // Content
                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Shares:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.TotalShares:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Locked for Guarantees:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.LockedForGuarantees:N0}").FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Available Shares:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.AvailableShares:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loans Guaranteed:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text($"{data.TotalGuaranteedLoans}").FontSize(8);
                            });

                            // Loans Table
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("LOANS GUARANTEED").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.4f);  // #
                                    cols.RelativeColumn(1.0f);  // Loan No
                                    cols.RelativeColumn(1.3f);  // Loanee
                                    cols.RelativeColumn(0.9f);  // Loan Amount
                                    cols.RelativeColumn(0.9f);  // Outstanding
                                    cols.RelativeColumn(0.9f);  // Guarantee Amount
                                    cols.RelativeColumn(0.9f);  // Remaining
                                    cols.RelativeColumn(0.8f);  // Application Date
                                    cols.RelativeColumn(0.8f);  // Expected Completion
                                    cols.RelativeColumn(1.0f);  // Status
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loanee").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Outstanding").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantee Amt").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Remaining").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("App Date").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Expected Completion").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(7);
                                });

                                int serialNo = 1;
                                decimal totalGuaranteeAmount = 0;
                                decimal totalRemainingGuarantee = 0;

                                foreach (var loan in data.GuaranteedLoans.OrderByDescending(l => l.ApplicationDate))
                                {
                                    string statusText = loan.IsOverdue ? "OVERDUE" :
                                                        (loan.IsActive ? "ACTIVE" : loan.LoanStatus.ToUpper());

                                    // Determine background color for status
                                    QuestPDF.Infrastructure.Color statusBgColor;
                                    if (loan.IsOverdue)
                                        statusBgColor = QuestPDF.Helpers.Colors.Red.Medium;
                                    else if (loan.IsActive)
                                        statusBgColor = QuestPDF.Helpers.Colors.Green.Medium;
                                    else
                                        statusBgColor = QuestPDF.Helpers.Colors.Grey.Medium;

                                    totalGuaranteeAmount += loan.GuaranteeAmount;
                                    totalRemainingGuarantee += loan.RemainingGuarantee;

                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text((serialNo++).ToString()).FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoaneeName ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.PrincipalAmount:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.OutstandingBalance:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.GuaranteeAmount:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.RemainingGuarantee:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicationDate.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ExpectedCompletionDate?.ToString("dd/MM/yyyy") ?? "N/A").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4)
                                        .Background(statusBgColor)
                                        .Text(statusText)
                                        .FontColor(QuestPDF.Helpers.Colors.White)
                                        .FontSize(7);
                                }

                                // Totals row
                                table.Cell().ColumnSpan(5).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTALS:").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalGuaranteeAmount:N0}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalRemainingGuarantee:N0}").Bold().FontSize(8);
                                table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4);
                            });

                            // Share Summary
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("SHARE SUMMARY").FontSize(11).Bold();

                            contentCol.Item().Table(shareTable =>
                            {
                                shareTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                shareTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Shares:").Bold().FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.TotalShares:N0}").FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4);

                                shareTable.Cell().Border(0.2f).Background("#fff3cd").Padding(4).Text("Locked for Guarantees:").Bold().FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.LockedForGuarantees:N0}").FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4);

                                shareTable.Cell().Border(0.2f).Background("#d4edda").Padding(4).Text("Available Shares:").Bold().FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{data.AvailableShares:N0}").FontSize(8);
                                shareTable.Cell().Border(0.2f).Padding(4);
                            });
                        });

                        // Footer
                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.DefaultTextStyle(t => t.FontSize(8));
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                                x.Span($" | Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                            });
                    });
                }).GeneratePdf(stream);

                var content = stream.ToArray();
                return File(content, "application/pdf", $"GuarantorLoans_{data.MemberNo}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting guarantor loans to PDF for member {MemberNo}", memberNo);
                TempData["Error"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("GuarantorLoans", new { memberNo });
            }
        }

        #endregion
    }
}