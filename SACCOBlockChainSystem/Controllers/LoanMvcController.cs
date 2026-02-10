using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LoanMvcController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly IMemberService _memberService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<LoanMvcController> _logger;

        public LoanMvcController(
            ILoanService loanService,
            IMemberService memberService,
            ICompanyContextService companyContextService,
            ILogger<LoanMvcController> logger)
        {
            _loanService = loanService;
            _memberService = memberService;
            _companyContextService = companyContextService;
            _logger = logger;
        }

        // GET: Loan/Application
        public IActionResult Application()
        {
            return View();
        }

        // POST: Loan/SearchMember
        [HttpPost]
        public async Task<IActionResult> SearchMember(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    TempData["ErrorMessage"] = $"Member {memberNo} not found";
                    return RedirectToAction("Application");
                }

                var eligibility = await _loanService.GetMemberLoanEligibilityAsync(memberNo);
                var existingLoans = await _loanService.GetLoansByMemberAsync(memberNo);
                var shareBalance = await _memberService.GetShareBalanceAsync(memberNo);

                var viewModel = new
                {
                    Member = member,
                    Eligibility = eligibility,
                    ShareBalance = shareBalance,
                    ExistingLoans = existingLoans
                };

                TempData["MemberData"] = Newtonsoft.Json.JsonConvert.SerializeObject(viewModel);
                return RedirectToAction("LoanForm", new { memberNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching member {memberNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Application");
            }
        }

        // GET: Loan/LoanForm/{memberNo}
        public IActionResult LoanForm(string memberNo)
        {
            if (TempData["MemberData"] is string memberDataJson)
            {
                var memberData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(memberDataJson);
                ViewBag.MemberData = memberData;
            }
            else
            {
                return RedirectToAction("Application");
            }

            return View();
        }

        // POST: Loan/SubmitApplication
        [HttpPost]
        public async Task<IActionResult> SubmitApplication([FromForm] LoanApplicationDTO application)
        {
            try
            {
                // Get current user from context
                var currentUser = User.Identity?.Name ?? "SYSTEM";
                application.CreatedBy = currentUser;

                var result = await _loanService.ApplyForLoanAsync(application);

                TempData["SuccessMessage"] = $"Loan application submitted successfully! Loan Number: {result.LoanNo}";
                return RedirectToAction("ApplicationDetails", new { loanNo = result.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("LoanForm", new { memberNo = application.MemberNo });
            }
        }

        // GET: Loan/ApplicationDetails/{loanNo}
        public async Task<IActionResult> ApplicationDetails(string loanNo)
        {
            try
            {
                var loan = await _loanService.GetLoanAsync(loanNo);
                return View(loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan details {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Loan/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var pendingLoans = await _loanService.GetPendingLoansAsync();
                ViewBag.PendingLoans = pendingLoans;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan dashboard");
                TempData["ErrorMessage"] = ex.Message;
                return View();
            }
        }

        // GET: Loan/Search
        public IActionResult Search()
        {
            return View();
        }

        // POST: Loan/SearchResults
        [HttpPost]
        public async Task<IActionResult> SearchResults([FromForm] LoanSearchDTO search)
        {
            try
            {
                var results = await _loanService.SearchLoansAsync(search);
                return View(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                TempData["ErrorMessage"] = ex.Message;
                return View(new List<LoanResponseDTO>());
            }
        }

        // GET: Loan/Manage/{loanNo}
        public async Task<IActionResult> Manage(string loanNo)
        {
            try
            {
                var loan = await _loanService.GetLoanAsync(loanNo);
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Guarantors = guarantors;
                ViewBag.NextStatuses = Helpers.LoanStatusHelper.GetNextAllowedStatuses(loan.Status);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan management {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Loan/UpdateStatus
        [HttpPost]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> UpdateStatus(string loanNo, [FromForm] LoanUpdateDTO update)
        {
            try
            {
                update.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
                var result = await _loanService.UpdateLoanStatusAsync(loanNo, update);

                TempData["SuccessMessage"] = $"Loan status updated to {result.StatusDescription}";
                return RedirectToAction("Manage", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan status {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Manage", new { loanNo });
            }
        }

        // GET: Loan/Guarantors/{loanNo}
        public async Task<IActionResult> Guarantors(string loanNo)
        {
            try
            {
                var loan = await _loanService.GetLoanAsync(loanNo);
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Guarantors = guarantors;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading guarantors for loan {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Loan/AddGuarantor
        [HttpPost]
        public async Task<IActionResult> AddGuarantor(string loanNo, [FromForm] LoanGuarantorDTO guarantor)
        {
            try
            {
                var success = await _loanService.AddGuarantorAsync(loanNo, guarantor);

                if (success)
                    TempData["SuccessMessage"] = "Guarantor added successfully";
                else
                    TempData["ErrorMessage"] = "Failed to add guarantor";

                return RedirectToAction("Guarantors", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding guarantor to loan {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Guarantors", new { loanNo });
            }
        }

        // GET: Loan/Reports
        public async Task<IActionResult> Reports()
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Get counts by status
                var statusCounts = new Dictionary<string, int>();
                for (int i = 1; i <= 8; i++)
                {
                    var loans = await _loanService.GetLoansByStatusAsync(i);
                    statusCounts.Add(Helpers.LoanStatusHelper.GetStatusDescription(i), loans.Count);
                }

                ViewBag.StatusCounts = statusCounts;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan reports");
                TempData["ErrorMessage"] = ex.Message;
                return View();
            }
        }

        // GET: Loan/Blockchain/{loanNo}
        public async Task<IActionResult> Blockchain(string loanNo)
        {
            try
            {
                var loan = await _loanService.GetLoanAsync(loanNo);
                ViewBag.Loan = loan;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading blockchain info for loan {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Dashboard");
            }
        }
    }
}