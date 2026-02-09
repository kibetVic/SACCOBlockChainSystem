using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public class LoanMvcController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly ILogger<LoanMvcController> _logger;
        private readonly ICompanyContextService _companyContextService;
        private readonly IMemberService _memberService;
        private readonly ILoanTypeService _loanTypeService;

        public LoanMvcController(
            ILoanService loanService,
            ILogger<LoanMvcController> logger,
            ICompanyContextService companyContextService,
            IMemberService memberService,
            ILoanTypeService loanTypeService)
        {
            _loanService = loanService;
            _logger = logger;
            _companyContextService = companyContextService;
            _memberService = memberService;
            _loanTypeService = loanTypeService;
        }

        // GET: /LoanMvc/Index
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var memberNo = User.FindFirst("MemberNo")?.Value;

                if (string.IsNullOrEmpty(memberNo))
                {
                    // Admin/Loan Officer view - show all loans
                    var searchCriteria = new LoanSearchDTO
                    {
                        CompanyCode = _companyContextService.GetCurrentCompanyCode()
                    };

                    var loans = await _loanService.SearchLoansAsync(searchCriteria);
                    return View("LoanOfficerIndex", loans);
                }
                else
                {
                    // Check if user is admin viewing as member
                    if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                    {
                        // Admin can view member's loans by specifying memberNo in query
                        var loans = await _loanService.GetMemberLoansAsync(memberNo);
                        return View("MemberIndex", loans);
                    }
                    else
                    {
                        return Forbid();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loans index");
                return View("Error");
            }
        }

        // GET: /LoanMvc/Apply
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Apply()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var loanTypes = await _loanTypeService.GetAllLoanTypesAsync(companyCode);

                ViewBag.EligibleLoanTypes = loanTypes;

                return View(new LoanApplicationDTO());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan application form");
                return View("Error");
            }
        }

        // POST: /LoanMvc/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Apply(LoanApplicationDTO application)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Reload loan types
                    var companyCode = _companyContextService.GetCurrentCompanyCode();
                    var loanTypes = await _loanTypeService.GetAllLoanTypesAsync(companyCode);
                    ViewBag.EligibleLoanTypes = loanTypes;

                    return View(application);
                }

                application.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanService.ApplyForLoanAsync(application);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Loan application submitted successfully! Loan Number: {result.LoanNo}";
                    return RedirectToAction("Details", new { loanNo = result.LoanNo });
                }
                else
                {
                    ModelState.AddModelError("", result.Message);

                    // Reload loan types
                    var companyCode = _companyContextService.GetCurrentCompanyCode();
                    var loanTypes = await _loanTypeService.GetAllLoanTypesAsync(companyCode);
                    ViewBag.EligibleLoanTypes = loanTypes;

                    return View(application);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");

                // Reload loan types
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var loanTypes = await _loanTypeService.GetAllLoanTypesAsync(companyCode);
                ViewBag.EligibleLoanTypes = loanTypes;

                return View(application);
            }
        }

        // GET: /LoanMvc/Details/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);
                return View(loanDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan details for {loanNo}");
                return View("Error");
            }
        }

        // GET: /LoanMvc/Guarantors/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Guarantors(string loanNo)
        {
            try
            {
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanDetails = loanDetails;

                return View(guarantors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading guarantors for loan {loanNo}");
                return View("Error");
            }
        }

        // POST: /LoanMvc/AddGuarantor
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> AddGuarantor(LoanGuarantorDTO guarantor)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please fill all required fields";
                    return RedirectToAction("Guarantors", new { loanNo = guarantor.LoanNo });
                }

                guarantor.ActionBy = User.Identity?.Name ?? "SYSTEM";

                var success = await _loanService.AddGuarantorAsync(guarantor);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Guarantor {guarantor.GuarantorMemberNo} added successfully";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to add guarantor";
                }

                return RedirectToAction("Guarantors", new { loanNo = guarantor.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding guarantor");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("Guarantors", new { loanNo = guarantor.LoanNo });
            }
        }

        // GET: /LoanMvc/Appraise/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Appraise(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanDetails = loanDetails;

                var appraisal = new LoanAppraisalDTO
                {
                    LoanNo = loanNo,
                    AppraisalBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(appraisal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading appraisal form for loan {loanNo}");
                return View("Error");
            }
        }

        // POST: /LoanMvc/Appraise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Appraise(LoanAppraisalDTO appraisal)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(appraisal.LoanNo);
                    ViewBag.LoanDetails = loanDetails;
                    return View(appraisal);
                }

                var success = await _loanService.AppraiseLoanAsync(appraisal);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Loan appraised successfully with recommendation: {appraisal.Recommendation}";
                    return RedirectToAction("Details", new { loanNo = appraisal.LoanNo });
                }
                else
                {
                    ModelState.AddModelError("", "Failed to appraise loan");

                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(appraisal.LoanNo);
                    ViewBag.LoanDetails = loanDetails;

                    return View(appraisal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appraising loan");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");

                // Reload loan details
                var loanDetails = await _loanService.GetLoanDetailsAsync(appraisal.LoanNo);
                ViewBag.LoanDetails = loanDetails;

                return View(appraisal);
            }
        }

        // GET: /LoanMvc/Endorse/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Endorse(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanDetails = loanDetails;

                var endorsement = new LoanEndorsementDTO
                {
                    LoanNo = loanNo,
                    EndorsedBy = User.Identity?.Name ?? "SYSTEM",
                    ApprovalDate = DateTime.Now
                };

                return View(endorsement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading endorsement form for loan {loanNo}");
                return View("Error");
            }
        }

        // POST: /LoanMvc/Endorse
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Endorse(LoanEndorsementDTO endorsement)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(endorsement.LoanNo);
                    ViewBag.LoanDetails = loanDetails;
                    return View(endorsement);
                }

                var success = await _loanService.EndorseLoanAsync(endorsement);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Loan {endorsement.Decision.ToLower()} successfully";
                    return RedirectToAction("Details", new { loanNo = endorsement.LoanNo });
                }
                else
                {
                    ModelState.AddModelError("", $"Failed to {endorsement.Decision.ToLower()} loan");

                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(endorsement.LoanNo);
                    ViewBag.LoanDetails = loanDetails;

                    return View(endorsement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error endorsing loan");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");

                // Reload loan details
                var loanDetails = await _loanService.GetLoanDetailsAsync(endorsement.LoanNo);
                ViewBag.LoanDetails = loanDetails;

                return View(endorsement);
            }
        }

        // GET: /LoanMvc/Disburse/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Disburse(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                if (loanDetails.StatusCode != 5) // Not approved
                {
                    TempData["ErrorMessage"] = "Loan must be approved before disbursement";
                    return RedirectToAction("Details", new { loanNo });
                }

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanDetails = loanDetails;

                var disbursement = new LoanDisbursementDTO
                {
                    LoanNo = loanNo,
                    DisbursementDate = DateTime.Now,
                    Amount = loanDetails.ApprovedAmount ?? loanDetails.AppliedAmount,
                    ProcessedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(disbursement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading disbursement form for loan {loanNo}");
                return View("Error");
            }
        }

        // POST: /LoanMvc/Disburse
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Disburse(LoanDisbursementDTO disbursement)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(disbursement.LoanNo);
                    ViewBag.LoanDetails = loanDetails;
                    return View(disbursement);
                }

                var success = await _loanService.DisburseLoanAsync(disbursement);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Loan disbursed successfully";
                    return RedirectToAction("Details", new { loanNo = disbursement.LoanNo });
                }
                else
                {
                    ModelState.AddModelError("", "Failed to disburse loan");

                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(disbursement.LoanNo);
                    ViewBag.LoanDetails = loanDetails;

                    return View(disbursement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disbursing loan");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");

                // Reload loan details
                var loanDetails = await _loanService.GetLoanDetailsAsync(disbursement.LoanNo);
                ViewBag.LoanDetails = loanDetails;

                return View(disbursement);
            }
        }

        // GET: /LoanMvc/Repay/{loanNo}
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Repay(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                if (loanDetails.StatusCode != 6) // Not disbursed
                {
                    TempData["ErrorMessage"] = "Loan must be disbursed before making repayments";
                    return RedirectToAction("Details", new { loanNo });
                }

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanDetails = loanDetails;

                var repayment = new LoanRepaymentDTO
                {
                    LoanNo = loanNo,
                    MemberNo = loanDetails.MemberNo,
                    PaymentDate = DateTime.Now,
                    ProcessedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(repayment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading repayment form for loan {loanNo}");
                return View("Error");
            }
        }

        // POST: /LoanMvc/Repay
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Repay(LoanRepaymentDTO repayment)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(repayment.LoanNo);
                    ViewBag.LoanDetails = loanDetails;
                    return View(repayment);
                }

                var success = await _loanService.MakeRepaymentAsync(repayment);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Repayment of {repayment.Amount:C} processed successfully";
                    return RedirectToAction("Details", new { loanNo = repayment.LoanNo });
                }
                else
                {
                    ModelState.AddModelError("", "Failed to process repayment");

                    // Reload loan details
                    var loanDetails = await _loanService.GetLoanDetailsAsync(repayment.LoanNo);
                    ViewBag.LoanDetails = loanDetails;

                    return View(repayment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing repayment");
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");

                // Reload loan details
                var loanDetails = await _loanService.GetLoanDetailsAsync(repayment.LoanNo);
                ViewBag.LoanDetails = loanDetails;

                return View(repayment);
            }
        }

        // GET: /LoanMvc/Search
        [Authorize(Policy = "Admin")]
        public IActionResult Search()
        {
            return View();
        }

        // GET: /LoanMvc/SearchResults
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> SearchResults(string? memberNo = null,
                                                     string? loanNo = null,
                                                     string? loanCode = null,
                                                     int? status = null,
                                                     DateTime? fromDate = null,
                                                     DateTime? toDate = null)
        {
            try
            {
                var searchCriteria = new LoanSearchDTO
                {
                    MemberNo = memberNo,
                    LoanNo = loanNo,
                    LoanCode = loanCode,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var loans = await _loanService.SearchLoansAsync(searchCriteria);

                ViewBag.SearchCriteria = searchCriteria;
                ViewBag.ResultCount = loans.Count;

                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return View("Error");
            }
        }

        // GET: /LoanMvc/Portfolio
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> Portfolio()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var portfolio = await _loanService.GetLoanPortfolioReportAsync(companyCode);

                return View(portfolio);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan portfolio");
                return View("Error");
            }
        }

        // POST: /LoanMvc/RemoveGuarantor/{loanNo}/{guarantorMemberNo}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> RemoveGuarantor(string loanNo, string guarantorMemberNo)
        {
            try
            {
                var success = await _loanService.RemoveGuarantorAsync(loanNo, guarantorMemberNo);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Guarantor {guarantorMemberNo} removed successfully";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove guarantor";
                }

                return RedirectToAction("Guarantors", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing guarantor from loan {loanNo}");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("Guarantors", new { loanNo });
            }
        }
    }
}