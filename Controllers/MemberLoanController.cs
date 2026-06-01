using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;
using System.Text;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class MemberLoanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AppDbContext _appDbContext;
        private readonly ILoanService _loanService;
        private readonly IMemberService _memberService;
        private readonly IContributionService _contributionService;
        private readonly IEmailService _emailService;
        private readonly ILogger<MemberLoanController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public MemberLoanController(
            ApplicationDbContext context,
            AppDbContext appDbContext,
            ILoanService loanService,
            IMemberService memberService,
            IContributionService contributionService,
            IEmailService emailService,
            ILogger<MemberLoanController> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _appDbContext = appDbContext;
            _loanService = loanService;
            _memberService = memberService;
            _contributionService = contributionService;
            _emailService = emailService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        // GET: /MemberLoan/Apply
        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return RedirectToAction("MemberLogin", "Account");
                }

                var companyCode = GetUserCompanyCode();

                // Get member eligibility
                var eligibility = await _loanService.CheckMemberEligibilityWithContributionsAsync(memberNo, companyCode);

                // Get active loan types (Approved only)
                var loanTypesRaw = await _context.Loantypes
                    .Where(lt => lt.CompanyCode == companyCode && lt.ApprovalStatus == "Approved")
                    .ToListAsync();

                var loanTypes = loanTypesRaw.Select(lt => new LoanTypeSimpleDTO
                {
                    LoanCode = lt.LoanCode,
                    LoanType = lt.LoanType1,
                    MaxAmount = lt.MaxAmount,
                    RepayPeriod = lt.RepayPeriod,
                    Interest = lt.Interest,
                    Guarantor = lt.Guarantor,
                    ProcessingFee = lt.Processingfee?.ToString(),
                    RepayMethod = lt.Repaymethod ?? "AMT",
                    SelfGuarantee = lt.SelfGuarantee?.ToString(),
                    Priority = lt.Priority ?? 1,
                    IsEligible = true
                }).ToList();

                // Get active loan count
                var activeLoanCount = await _context.Loans
                    .CountAsync(l => l.MemberNo == memberNo &&
                                   l.CompanyCode == companyCode &&
                                   (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));

                ViewBag.EligibleAmount = eligibility.TotalEligibleShares;
                ViewBag.MaxLoanAmount = eligibility.MaxLoanAmount;
                ViewBag.TotalShares = eligibility.TotalEligibleShares;
                ViewBag.ActiveLoanCount = activeLoanCount;
                ViewBag.LoanTypes = loanTypes;
                ViewBag.CompanyCode = companyCode;

                return View(new MemberLoanApplicationDTO
                {
                    MemberNo = memberNo,
                    CompanyCode = companyCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan application page");
                TempData["ErrorMessage"] = "Error loading application page";
                return RedirectToAction("Dashboard", "MemberMvc");
            }
        }

        // POST: /MemberLoan/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply([FromBody] MemberLoanApplicationDTO application)
        {
            try
            {
                _logger.LogInformation($"Member loan application received from {application.MemberNo}");

                var companyCode = GetUserCompanyCode();
                application.CompanyCode = companyCode;

                // Validate eligibility again
                var eligibility = await _loanService.CheckMemberEligibilityWithContributionsAsync(application.MemberNo, companyCode);

                if (!eligibility.IsEligible)
                {
                    return Json(new { success = false, message = eligibility.Message });
                }

                if (application.PrincipalAmount > eligibility.MaxLoanAmount)
                {
                    return Json(new { success = false, message = $"Loan amount exceeds your maximum eligible amount of KES {eligibility.MaxLoanAmount:N0}" });
                }

                // Validate loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    return Json(new { success = false, message = "Invalid loan type selected" });
                }

                if (loanType.MaxAmount.HasValue && application.PrincipalAmount > loanType.MaxAmount)
                {
                    return Json(new { success = false, message = $"Loan amount exceeds maximum for this loan type (KES {loanType.MaxAmount:N0})" });
                }

                // Create loan application (Draft status)
                var loanApplication = new Loan
                {
                    LoanNo = await GenerateLoanNumberAsync(loanType.LoanCode, application.MemberNo, companyCode),
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanCode,
                    CompanyCode = companyCode,
                    LoanAmt = application.PrincipalAmount,
                    Interest = decimal.TryParse(loanType.Interest, out decimal interest) ? interest : 0,
                    RepayPeriod = application.RepayPeriod,
                    ApplicDate = DateTime.Now,
                    Status = (int)Status.Draft,
                    Purpose = application.Purpose,
                    AddSecurity = application.Remarks,
                    Guaranteed = "0", // Not yet guaranteed
                    RepayMethod = loanType.Repaymethod ?? "AMT",
                    AuditId = application.MemberNo,
                    AuditTime = DateTime.Now,
                    Posted = "Draft",
                    UserName = application.MemberNo,
                    AuditDateTime = DateTime.Now
                };

                _context.Loans.Add(loanApplication);
                await _context.SaveChangesAsync();

                // Create guarantor invitations
                foreach (var invite in application.GuarantorInvites)
                {
                    var token = Guid.NewGuid().ToString();
                    var expiry = DateTime.Now.AddDays(7); // 7 days to respond

                    // Create pending guarantor record
                    var pendingGuarantor = new Loanguar
                    {
                        LoanNo = loanApplication.LoanNo,
                        MemberNo = invite.MemberNo ?? string.Empty,
                        Amount = invite.ProposedAmount,
                        Balance = invite.ProposedAmount,
                        CompanyCode = companyCode,
                        AuditTime = DateTime.Now,
                        AuditId = application.MemberNo,
                        Transfered = false,
                        FullNames = invite.FullName,
                        Description = $"Invited via email: {invite.Email}",
                        Tguaranto = invite.ProposedAmount
                    };

                    _context.Loanguar.Add(pendingGuarantor);
                    await _context.SaveChangesAsync();

                    // Send email invitation
                    await SendGuarantorInvitationEmail(invite.Email, loanApplication.LoanNo, application.MemberNo,
                        application.PrincipalAmount, token, pendingGuarantor.Id);
                }

                _logger.LogInformation($"Loan application {loanApplication.LoanNo} created successfully");

                return Json(new { success = true, message = "Application submitted successfully", loanNo = loanApplication.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: /MemberLoan/GuarantorResponse/{token}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GuarantorResponse(string token, int guarantorId)
        {
            try
            {
                var guarantor = await _context.Loanguar
                    .FirstOrDefaultAsync(g => g.Id == guarantorId && g.Transfered == false);

                if (guarantor == null)
                {
                    ViewBag.ErrorMessage = "Invalid or expired invitation link.";
                    return View("Error");
                }

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo);

                if (loan == null)
                {
                    ViewBag.ErrorMessage = "Loan not found.";
                    return View("Error");
                }

                // Get member details for applicant
                var applicant = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo);

                // Get guarantor's eligibility
                var guarantorMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == guarantor.MemberNo);

                if (guarantorMember == null && !string.IsNullOrEmpty(guarantor.MemberNo))
                {
                    // Try to find by email - extract the email first to avoid null propagating operator in lambda
                    var email = guarantor.Description?.Replace("Invited via email: ", "");
                    if (!string.IsNullOrEmpty(email))
                    {
                        guarantorMember = await _context.Members
                            .FirstOrDefaultAsync(m => m.Email == email);
                    }
                }

                decimal eligibleAmount = 0;
                decimal currentGuarantees = 0;
                decimal maxGuarantee = 0;

                if (guarantorMember != null)
                {
                    // Calculate eligible amount from deposits
                    eligibleAmount = await _context.ContribShares
                        .Where(cs => cs.MemberNo == guarantorMember.MemberNo && cs.CompanyCode == loan.CompanyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    // Get current guarantees
                    var activeLoanNos = await _context.Loans
                        .Where(l => l.CompanyCode == loan.CompanyCode &&
                                   l.Status != (int)Status.Closed &&
                                   l.Status != (int)Status.Rejected)
                        .Select(l => l.LoanNo)
                        .ToListAsync();

                    currentGuarantees = await _context.Loanguar
                        .Where(g => g.MemberNo == guarantorMember.MemberNo &&
                                   g.CompanyCode == loan.CompanyCode &&
                                   g.Transfered == false &&
                                   activeLoanNos.Contains(g.LoanNo))
                        .SumAsync(g => g.Amount ?? 0);

                    maxGuarantee = eligibleAmount - currentGuarantees;
                }
                else
                {
                    // If not a member, they need to register first
                    ViewBag.NeedsRegistration = true;
                    ViewBag.InvitationEmail = guarantor.Description?.Replace("Invited via email: ", "");
                    ViewBag.LoanNo = loan.LoanNo;
                    ViewBag.GuarantorId = guarantor.Id;
                }

                var responseModel = new GuarantorsResponseDTO
                {
                    Id = guarantor.Id,
                    LoanNo = guarantor.LoanNo,
                    GuarantorMemberNo = guarantor.MemberNo ?? "",
                    GuaranteeAmount = guarantor.Amount ?? 0,
                    Status = "Pending",
                    InvitationToken = token
                };

                ViewBag.LoanDetails = new
                {
                    LoanNo = loan.LoanNo,
                    ApplicantName = applicant != null ? $"{applicant.Surname} {applicant.OtherNames}".Trim() : loan.MemberNo,
                    LoanAmount = loan.LoanAmt,
                    ApplicationDate = loan.ApplicDate
                };
                ViewBag.EligibleAmount = eligibleAmount;
                ViewBag.CurrentGuarantees = currentGuarantees;
                ViewBag.MaxGuarantee = maxGuarantee;
                ViewBag.Token = token;

                return View(responseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading guarantor response page");
                ViewBag.ErrorMessage = "An error occurred. Please try again.";
                return View("Error");
            }
        }

        // POST: /MemberLoan/GuarantorResponse
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuarantorResponse(string action, string loanNo, string guarantorMemberNo,
            decimal guaranteeAmount, string remarks, string invitationToken)
        {
            try
            {
                var guarantor = await _context.Loanguar
                    .FirstOrDefaultAsync(g => g.LoanNo == loanNo &&
                                              (g.MemberNo == guarantorMemberNo || string.IsNullOrEmpty(g.MemberNo)));

                if (guarantor == null)
                {
                    TempData["ErrorMessage"] = "Invalid invitation.";
                    return RedirectToAction("Index", "Home");
                }

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (action == "accept")
                {
                    // Validate member exists
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == guarantorMemberNo);

                    if (member == null)
                    {
                        // Try to find by email or create temporary record
                        var email = guarantor.Description?.Replace("Invited via email: ", "");
                        member = await _context.Members
                            .FirstOrDefaultAsync(m => m.Email == email);

                        if (member == null)
                        {
                            TempData["ErrorMessage"] = "You must be a registered member to guarantee a loan. Please register first.";
                            return RedirectToAction("Register", "MemberMvc");
                        }

                        guarantor.MemberNo = member.MemberNo;
                        guarantor.FullNames = $"{member.Surname} {member.OtherNames}".Trim();
                        await _context.SaveChangesAsync();
                    }

                    // Validate eligibility
                    var isValid = await _loanService.ValidateGuarantorEligibilityAsync(guarantorMemberNo, guaranteeAmount, loan.CompanyCode);

                    if (!isValid)
                    {
                        TempData["ErrorMessage"] = "You are not eligible to guarantee this amount. Please check your available deposits.";
                        return RedirectToAction("GuarantorResponse", new { token = invitationToken, guarantorId = guarantor.Id });
                    }

                    // Update guarantor record
                    guarantor.Amount = guaranteeAmount;
                    guarantor.Balance = guaranteeAmount;
                    guarantor.Transfered = false;
                    guarantor.Description = remarks ?? "Accepted via email invitation";
                    guarantor.AuditTime = DateTime.Now;
                    guarantor.Transdate = DateTime.Now;

                    await _context.SaveChangesAsync();

                    // Check if all required guarantors have accepted
                    var allGuarantors = await _context.Loanguar
                        .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                        .ToListAsync();

                    var acceptedCount = allGuarantors.Count(g => g.Amount > 0);
                    var totalGuarantee = allGuarantors.Sum(g => g.Amount ?? 0);
                    var loanAmount = loan.LoanAmt ?? 0;

                    // If fully guaranteed, update loan status to Submitted
                    if (totalGuarantee >= loanAmount)
                    {
                        loan.Status = (int)Status.Submitted;
                        loan.Posted = "SUBMIT";
                        loan.Guaranteed = "1";
                        await _context.SaveChangesAsync();

                        // Notify applicant that loan is fully guaranteed
                        await NotifyLoanFullyGuaranteed(loan.MemberNo, loan.LoanNo);
                    }
                    else
                    {
                        // Notify applicant of partial guarantee
                        await NotifyGuarantorAccepted(loan.MemberNo, loan.LoanNo, guaranteeAmount, guarantor.MemberNo);
                    }

                    TempData["SuccessMessage"] = $"You have successfully guaranteed KES {guaranteeAmount:N0} for loan {loanNo}. Thank you!";
                }
                else if (action == "reject")
                {
                    guarantor.Transfered = true;
                    guarantor.Description = $"Rejected: {remarks ?? "No reason provided"}";
                    guarantor.AuditTime = DateTime.Now;
                    await _context.SaveChangesAsync();

                    // Notify applicant of rejection
                    await NotifyGuarantorRejected(loan.MemberNo, loan.LoanNo, guarantor.MemberNo);

                    TempData["InfoMessage"] = "You have declined to guarantee this loan.";
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing guarantor response");
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /MemberLoan/MyLoans
        [HttpGet]
        public async Task<IActionResult> MyLoans()
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return RedirectToAction("MemberLogin", "Account");
                }

                var companyCode = GetUserCompanyCode();

                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .Select(l => new MemberLoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        LoanType = _context.Loantypes.Where(lt => lt.LoanCode == l.LoanCode).Select(lt => lt.LoanType1).FirstOrDefault() ?? l.LoanCode,
                        PrincipalAmount = l.LoanAmt ?? 0,
                        Status = ((Status)(l.Status ?? 0)).ToString(),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = l.AuditDateTime,
                        OutstandingBalance = _context.Loanbal.Where(lb => lb.LoanNo == l.LoanNo).Select(lb => lb.Balance).FirstOrDefault(),
                        NextPaymentDate = _context.Loanbal.Where(lb => lb.LoanNo == l.LoanNo).Select(lb => lb.Nextduedate).FirstOrDefault(),
                        MonthlyInstallment = _context.Loanbal.Where(lb => lb.LoanNo == l.LoanNo).Select(lb => lb.RepayRate).FirstOrDefault()
                    })
                    .ToListAsync();

                // Get pending guarantor requests (where member is guarantor)
                var pendingGuarantees = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo && g.Transfered == false && (g.Amount == null || g.Amount == 0))
                    .Select(g => new PendingGuaranteeDTO
                    {
                        LoanNo = g.LoanNo,
                        ApplicantName = _context.Members.Where(m => m.MemberNo == _context.Loans.Where(l => l.LoanNo == g.LoanNo).Select(l => l.MemberNo).FirstOrDefault())
                                        .Select(m => m.Surname + " " + m.OtherNames).FirstOrDefault() ?? g.LoanNo,
                        LoanAmount = _context.Loans.Where(l => l.LoanNo == g.LoanNo).Select(l => l.LoanAmt).FirstOrDefault() ?? 0,
                        InvitationDate = g.AuditTime ?? DateTime.Now
                    })
                    .ToListAsync();

                ViewBag.PendingGuarantees = pendingGuarantees;
                ViewBag.ActiveLoansCount = loans.Count(l => l.Status == "Disbursed" || l.Status == "Endorsed");
                ViewBag.PendingLoansCount = loans.Count(l => l.Status == "Draft" || l.Status == "Submitted");

                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member loans");
                TempData["ErrorMessage"] = "Error loading your loans";
                return View(new List<MemberLoanSummaryDTO>());
            }
        }

        // GET: /MemberLoan/Details/{loanNo}
        [HttpGet]
        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return RedirectToAction("MemberLogin", "Account");
                }

                var companyCode = GetUserCompanyCode();

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    return NotFound();
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                var schedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();

                var repayments = await _context.Repay
                    .Where(r => r.LoanNo == loanNo && r.Posted == true)
                    .OrderByDescending(r => r.DateReceived)
                    .ToListAsync();

                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo)
                    .Select(g => new
                    {
                        g.Id,
                        g.MemberNo,
                        FullName = g.FullNames ?? _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.Surname + " " + m.OtherNames).FirstOrDefault() ?? g.MemberNo,
                        g.Amount,
                        g.Transfered,
                        g.Description
                    })
                    .ToListAsync();

                ViewBag.LoanType = loanType;
                ViewBag.LoanBalance = loanbal;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;
                ViewBag.Guarantors = guarantors;
                ViewBag.CanRepay = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;

                return View(loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan details");
                TempData["ErrorMessage"] = "Error loading loan details";
                return RedirectToAction("MyLoans");
            }
        }

        #region Helper Methods

        private string GetLoggedInMemberNumber()
        {
            var memberNoClaim = User.FindFirst("MemberNo")?.Value;
            if (!string.IsNullOrEmpty(memberNoClaim))
            {
                return memberNoClaim;
            }

            var nameClaim = User.Identity?.Name;
            if (!string.IsNullOrEmpty(nameClaim))
            {
                var member = _context.Members.FirstOrDefault(m => m.MemberNo == nameClaim);
                return member?.MemberNo;
            }

            return null;
        }

        private string GetUserCompanyCode()
        {
            var companyCode = _companyContextService.GetCurrentCompanyCode();
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }
            return companyCode;
        }

        private async Task<string> GenerateLoanNumberAsync(string loanCode, string memberNo, string companyCode)
        {
            string milliseconds = DateTime.Now.ToString("fff");
            return $"{milliseconds}{loanCode}{memberNo}";
        }

        private async Task SendGuarantorInvitationEmail(string email, string loanNo, string applicantNo,
            decimal loanAmount, string token, int guarantorId)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var responseUrl = $"{baseUrl}/MemberLoan/GuarantorResponse?token={token}&guarantorId={guarantorId}";

                var subject = $"Loan Guarantor Request - Loan {loanNo}";
                var body = $@"
                    <h2>Loan Guarantor Request</h2>
                    <p>Dear Member,</p>
                    <p><strong>{applicantNo}</strong> has requested you to be a guarantor for a loan of <strong>KES {loanAmount:N0}</strong>.</p>
                    <p>Please click the button below to respond to this request:</p>
                    <p><a href='{responseUrl}' style='background-color: #2563eb; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Respond to Request</a></p>
                    <p>If you are not a registered member, you will be prompted to register before accepting the guarantee.</p>
                    <p>This invitation will expire in 7 days.</p>
                    <hr>
                    <p><small>If you did not expect this request, please ignore this email.</small></p>
                ";

                await _emailService.SendEmailAsync(email, subject, body);
                _logger.LogInformation($"Guarantor invitation sent to {email} for loan {loanNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send guarantor invitation to {email}");
            }
        }

        private async Task NotifyLoanFullyGuaranteed(string memberNo, string loanNo)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
            if (member?.Email != null)
            {
                var subject = $"Loan {loanNo} - Fully Guaranteed!";
                var body = $@"
            <h2>Loan Fully Guaranteed!</h2>
            <p>Dear {member.Surname},</p>
            <p>Your loan application <strong>{loanNo}</strong> has been fully guaranteed and has been submitted for review.</p>
            <p>You will be notified once the loan is approved and disbursed.</p>
            <p>Thank you for choosing our SACCO.</p>";
                await _emailService.SendEmailAsync(member.Email, subject, body);
            }
        }

        private async Task NotifyGuarantorAccepted(string memberNo, string loanNo, decimal amount, string guarantorNo)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
            if (member?.Email != null)
            {
                // Get values asynchronously FIRST, before building the email body
                var loanAmount = await _context.Loans
                    .Where(l => l.LoanNo == loanNo)
                    .Select(l => l.LoanAmt)
                    .FirstOrDefaultAsync() ?? 0;

                var totalGuarantee = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo)
                    .SumAsync(g => g.Amount ?? 0);

                var remainingNeeded = loanAmount - totalGuarantee;

                var subject = $"Loan {loanNo} - Guarantor Accepted";
                var body = $@"
            <h2>Guarantor Accepted!</h2>
            <p>Dear {member.Surname},</p>
            <p>A guarantor has accepted to guarantee <strong>KES {amount:N0}</strong> for your loan <strong>{loanNo}</strong>.</p>
            <p>You need KES {remainingNeeded:N0} more in guarantees.</p>
            <p>You will be notified when your loan is fully guaranteed.</p>
        ";
                await _emailService.SendEmailAsync(member.Email, subject, body);
            }
        }

        private async Task NotifyGuarantorRejected(string memberNo, string loanNo, string guarantorNo)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
            if (member?.Email != null)
            {
                var subject = $"Loan {loanNo} - Guarantor Declined";
                var body = $@"
                    <h2>Guarantor Declined</h2>
                    <p>Dear {member.Surname},</p>
                    <p>A guarantor has declined to guarantee your loan <strong>{loanNo}</strong>.</p>
                    <p>You may want to invite another guarantor to complete your application.</p>
                ";
                await _emailService.SendEmailAsync(member.Email, subject, body);
            }
        }

        #endregion
    }
}