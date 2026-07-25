// Controllers/InquiryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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


        // In InquiryController.cs or a new ApiController

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

                var result = await _inquiryService.GetGuarantorLoansAsync(memberNo, companyCode, userId);

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

                var result = await _inquiryService.GetGuarantorLoansAsync(memberNo, companyCode, userId);

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
    }
}