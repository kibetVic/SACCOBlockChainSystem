using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Spreadsheet;
using iText.Kernel.Pdf.Canvas.Parser.ClipperLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fonts = QuestPDF.Helpers.Fonts;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LoanMvcController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly ILoanTypeService _loanTypeService;
        private readonly IContributionService _contributionService;
        private readonly IMemberService _memberService;
        private readonly AppDbContext _appDbContext;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<LoanMvcController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ISaccoService _saccoService;
        private readonly ICollateralService _collateralService;

        public LoanMvcController(
            ILoanService loanService,
            ILoanTypeService loanTypeService,
            AppDbContext appDbContext,
            IMemberService memberService,
            IContributionService contributionService,
            ICompanyContextService companyContextService,
            ApplicationDbContext context,
            ISaccoService saccoService,
            ICollateralService collateralService,
            ILogger<LoanMvcController> logger)
        {
            _loanService = loanService;
            _loanTypeService = loanTypeService;
            _memberService = memberService;
            _contributionService = contributionService;
            _companyContextService = companyContextService;
            _logger = logger;
            _appDbContext = appDbContext;
            _saccoService = saccoService;
            _collateralService = collateralService;
            _context = context;
        }


        private int GetCompanyId()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                if (string.IsNullOrEmpty(companyCode))
                {
                    _logger.LogWarning("Company code is null or empty");
                    return 1; // Default company ID
                }

                var company = _context.Companies
                    .FirstOrDefault(c => c.CompanyCode == companyCode);

                if (company != null)
                {
                    return company.Id;
                }

                _logger.LogWarning($"Company not found for code: {companyCode}");
                return 1; // Default company ID
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company ID");
                return 1; // Default company ID
            }
        }

        private bool IsAdminUser()
        {
            return User.IsInRole("Admin") ||
                   User.HasClaim(c => c.Type == "UserGroup" && c.Value == "Admin");
        }

        [HttpGet]
        public async Task<IActionResult> CheckMemberEligibility(string memberNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                // Use the existing method name (it now returns the new tuple)
                var eligibility = await _loanService.CheckMemberEligibilityWithContributionsAsync(memberNo, companyCode);

                // Get member details
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);

                // Check for existing active loans
                var existingLoansResult = await _loanService.CheckExistingLoansAsync(memberNo, companyCode);

                // ============================================================
                // KEY: Find the first active loan that has bridging allowed
                // This is the loan that can be topped up
                // ============================================================
                string topUpLoanTypeCode = null;
                string topUpLoanTypeName = null;
                bool hasBridgingAllowed = false;
                List<object> activeLoans = new List<object>();

                if (existingLoansResult.ExistingLoans != null && existingLoansResult.ExistingLoans.Any())
                {
                    // Get all active loans with their bridging status
                    foreach (var loanSummary in existingLoansResult.ExistingLoans)
                    {
                        var loan = await _loanService.GetLoanByNoAsync(loanSummary.LoanNo, companyCode);
                        if (loan != null)
                        {
                            var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                            activeLoans.Add(new
                            {
                                loanSummary.LoanNo,
                                loanSummary.LoanType,
                                loanSummary.LoanStatus,
                                loanSummary.OutstandingBalance,
                                BridgingAllowed = loan.Bridging == true
                            });

                            // Find the FIRST loan that has bridging allowed
                            if (loan.Bridging == true && string.IsNullOrEmpty(topUpLoanTypeCode))
                            {
                                hasBridgingAllowed = true;
                                topUpLoanTypeCode = loan.LoanCode;
                                topUpLoanTypeName = loanType?.LoanType ?? loan.LoanCode;
                            }
                        }
                    }
                }

                // Only block if there are existing loans AND no bridging is allowed
                bool shouldBlock = existingLoansResult.HasExistingLoan && !hasBridgingAllowed;

                return Json(new
                {
                    success = eligibility.IsEligible,
                    message = eligibility.Message,
                    hasExistingLoan = shouldBlock,
                    existingLoans = existingLoansResult.ExistingLoans?.Select(l => new
                    {
                        l.LoanNo,
                        l.LoanType,
                        l.LoanStatus,
                        l.OutstandingBalance
                    }),
                    data = new
                    {
                        memberNo = member?.MemberNo,
                        name = member?.FullName ?? $"{member?.Surname} {member?.OtherNames}",
                        idNo = member?.Idno,
                        phone = member?.PhoneNo,
                        email = member?.Email,
                        shareCapital = member?.ShareCap ?? 0,
                        eligibleShares = eligibility.TotalEligibleShares,
                        totalEligibleShares = eligibility.TotalEligibleShares,
                        maxLoanAmount = eligibility.MaxLoanAmount,
                        hasValidShares = eligibility.HasValidShares,
                        availableShares = eligibility.TotalEligibleShares,
                        totalContributions = eligibility.TotalEligibleShares,
                        maxLoanAmountFromShares = eligibility.MaxLoanAmount
                    },
                    bridgingInfo = new
                    {
                        hasExistingLoans = existingLoansResult.HasExistingLoan,
                        hasBridgingAllowed = hasBridgingAllowed,
                        // ============================================================
                        // KEY: Only the EXACT loan type that has bridging allowed
                        // ============================================================
                        topUpLoanTypeCode = topUpLoanTypeCode,
                        topUpLoanTypeName = topUpLoanTypeName,
                        loans = activeLoans
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking member eligibility");
                return Json(new
                {
                    success = false,
                    message = $"Error checking eligibility: {ex.Message}"
                });
            }
        }


        #region Loan Deletion

        // GET: /LoanMvc/DeleteLoan
        [HttpGet]
        public IActionResult DeleteLoan()
        {
            try
            {
                // Check permission
                if (!User.IsInRole("Admin") && !User.IsInRole("Super Admin"))
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete loans";
                    return RedirectToAction("AllLoans");
                }

                // Return simple content for testing
                return Content("Delete Loan page - View file needs to be created at Views/LoanMvc/DeleteLoan.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading delete loan page");
                TempData["ErrorMessage"] = "Error loading page";
                return RedirectToAction("AllLoans");
            }
        }

        // POST: /LoanMvc/DeleteLoan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLoan(string loanNo, string reason)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                _logger.LogInformation($"DeleteLoan POST called for loan {loanNo}");

                // Validate input
                if (string.IsNullOrEmpty(loanNo))
                {
                    TempData["ErrorMessage"] = "Loan number is required";
                    return RedirectToAction("DeleteLoan");
                }

                if (string.IsNullOrEmpty(reason))
                {
                    TempData["ErrorMessage"] = "Please provide a reason for deleting the loan";
                    return RedirectToAction("DeleteLoan");
                }

                // Check permission
                if (!User.IsInRole("Admin") && !User.IsInRole("Super Admin"))
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete loans";
                    return RedirectToAction("AllLoans");
                }

                // Verify loan exists - DO NOT check for Closed status
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("DeleteLoan");
                }

                // REMOVE THIS CHECK - Allow deletion even if status is Closed
                // if (loan.Status == (int)Status.Closed)
                // {
                //     TempData["ErrorMessage"] = "This loan is already closed/deleted";
                //     return RedirectToAction("DeleteLoan");
                // }

                // Get counts for logging (query before deletion)
                var guarantorCount = await _context.Loanguar.CountAsync(g => g.LoanNo == loanNo);
                var collateralCount = await _context.ColloanGuars.CountAsync(cg => cg.LoanNo == loanNo);
                var scheduleCount = await _context.LoanSchedules.CountAsync(s => s.LoanNo == loanNo);
                var repaymentCount = await _context.Repay.CountAsync(r => r.LoanNo == loanNo);
                var appraisalExists = await _context.Appraisal.AnyAsync(a => a.LoanNo == loanNo);
                var endorsementExists = await _context.Endmain.AnyAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);
                var chequeExists = await _context.Cheques.AnyAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);
                var loanbalExists = await _context.Loanbal.AnyAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                _logger.LogInformation($"Loan {loanNo} - Found data: Guarantors={guarantorCount}, Collateral={collateralCount}, " +
                    $"Schedules={scheduleCount}, Repayments={repaymentCount}, Appraisal={appraisalExists}, " +
                    $"Endorsement={endorsementExists}, Cheque={chequeExists}, LoanBal={loanbalExists}");

                // Permanently delete the loan and all related data
                await _loanService.DeleteLoanAsync(loanNo, companyCode, User.Identity?.Name ?? "SYSTEM", reason);

                TempData["SuccessMessage"] = $"Loan {loanNo} has been PERMANENTLY DELETED. " +
                                             $"Removed: {guarantorCount} guarantor(s), " +
                                             $"{collateralCount} collateral(s), " +
                                             $"{scheduleCount} schedule entries, " +
                                             $"{repaymentCount} repayment(s)";

                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting loan {loanNo}");
                TempData["ErrorMessage"] = $"Error deleting loan: {ex.Message}";
                return RedirectToAction("DeleteLoan");
            }
        }

        // GET: /LoanMvc/DeleteLoanIndex
        [HttpGet]
        public IActionResult DeleteLoanIndex()
        {
            try
            {
                bool hasPermission = User.IsInRole("System Administrator") ||
                     User.IsInRole("Super Admin") ||
                     User.IsInRole("Finance Officer") ||
                     User.IsInRole("Loan Officer");

                if (!hasPermission)
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete loans";
                    return RedirectToAction("AllLoans");
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading delete loan page");
                TempData["ErrorMessage"] = "Error loading page";
                return RedirectToAction("AllLoans");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLoanDetailsForDeletion(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                _logger.LogInformation($"Getting loan details for deletion: {loanNo}");

                // Get the loan - DO NOT filter by status
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Loan not found" });
                }

                // REMOVE THIS CHECK - Allow showing loans even if status is Closed
                // if (loan.Status == (int)Status.Closed)
                // {
                //     return Json(new { success = false, message = "This loan is already closed/deleted" });
                // }

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get guarantors (include ALL, not just Transfered == false)
                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo)
                    .Select(g => new
                    {
                        Id = g.Id,
                        MemberNo = g.MemberNo,
                        Name = _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.FullName).FirstOrDefault() ?? g.MemberNo,
                        Amount = g.Amount ?? 0,
                        Transfered = g.Transfered
                    })
                    .ToListAsync();

                // Get collateral guarantees (include ALL, not just Balance > 0)
                var collateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo)
                    .Select(cg => new
                    {
                        Id = cg.Id,
                        ColCode = cg.ColCode,
                        DocNo = cg.DocNo,
                        MarketValue = cg.Mktvalue,
                        GuaranteeAmount = cg.Balance
                    })
                    .ToListAsync();

                // Check if loan has endorsement
                var hasEndorsement = await _context.Endmain
                    .AnyAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                // Check if loan has cheque
                var hasCheque = await _context.Cheques
                    .AnyAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                // Check if loan has loan balance
                var hasLoanBal = await _context.Loanbal
                    .AnyAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                // Get status name
                string statusName = ((Status)(loan.Status ?? 0)).ToString();

                return Json(new
                {
                    success = true,
                    loan = new
                    {
                        loanNo = loan.LoanNo,
                        memberNo = loan.MemberNo,
                        memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                        loanType = loanType?.LoanType1 ?? loan.LoanCode,
                        principalAmount = loan.LoanAmt ?? 0,
                        status = statusName,
                        applicationDate = loan.ApplicDate.ToString("yyyy-MM-dd"),
                        interestRate = loan.Interest ?? 0,
                        repayPeriod = loan.RepayPeriod ?? 0,
                        repayMethod = loan.RepayMethod ?? loanType?.Repaymethod ?? "N/A",
                        guarantors = guarantors,
                        collateralGuarantees = collateralGuarantees,
                        hasEndorsement = hasEndorsement,
                        hasCheque = hasCheque,
                        hasLoanBal = hasLoanBal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan details for deletion: {loanNo}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLoanForDeletion(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                _logger.LogInformation($"Getting loan details for deletion: {loanNo}");

                // Get the loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Loan not found" });
                }

                // Check if loan is already closed
                if (loan.Status == (int)Status.Closed)
                {
                    return Json(new { success = false, message = "This loan is already closed/deleted" });
                }

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get guarantors
                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .Select(g => new
                    {
                        Id = g.Id,
                        MemberNo = g.MemberNo,
                        Name = _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.FullName).FirstOrDefault() ?? g.MemberNo,
                        Amount = g.Amount ?? 0
                    })
                    .ToListAsync();

                // Get collateral guarantees
                var collateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .Select(cg => new
                    {
                        Id = cg.Id,
                        ColCode = cg.ColCode,
                        DocNo = cg.DocNo,
                        MarketValue = cg.Mktvalue,
                        GuaranteeAmount = cg.Balance
                    })
                    .ToListAsync();

                // Check if loan has endorsement
                var hasEndorsement = await _context.Endmain
                    .AnyAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                // Get status name
                string statusName = ((Status)(loan.Status ?? 0)).ToString();

                return Json(new
                {
                    success = true,
                    loan = new
                    {
                        loanNo = loan.LoanNo,
                        memberNo = loan.MemberNo,
                        memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                        loanType = loanType?.LoanType1 ?? loan.LoanCode,
                        principalAmount = loan.LoanAmt ?? 0,
                        status = statusName,
                        applicationDate = loan.ApplicDate,
                        interestRate = loan.Interest ?? 0,
                        repayPeriod = loan.RepayPeriod ?? 0,
                        repayMethod = loan.RepayMethod ?? loanType?.Repaymethod ?? "N/A",
                        guarantors = guarantors,
                        collateralGuarantees = collateralGuarantees,
                        hasEndorsement = hasEndorsement
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan details for deletion: {loanNo}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDeleteLoan(string loanNo, string reason)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                _logger.LogInformation($"ConfirmDeleteLoan POST called for loan {loanNo}");

                // Validate input
                if (string.IsNullOrEmpty(loanNo))
                {
                    TempData["ErrorMessage"] = "Loan number is required";
                    return RedirectToAction("DeleteLoanIndex");
                }

                if (string.IsNullOrEmpty(reason))
                {
                    TempData["ErrorMessage"] = "Please provide a reason for deleting the loan";
                    return RedirectToAction("DeleteLoanIndex");
                }

                // Check permission (only admin or specific roles)
                if (!User.IsInRole("Admin") && !User.IsInRole("Super Admin"))
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete loans";
                    return RedirectToAction("AllLoans");
                }

                // Verify loan exists and is not already closed
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("DeleteLoanIndex");
                }

                if (loan.Status == (int)Status.Closed)
                {
                    TempData["ErrorMessage"] = "This loan is already closed/deleted";
                    return RedirectToAction("DeleteLoanIndex");
                }

                // Get counts before deletion for logging
                var guarantorCount = await _context.Loanguar
                    .CountAsync(g => g.LoanNo == loanNo && g.Transfered == false);

                var collateralCount = await _context.ColloanGuars
                    .CountAsync(cg => cg.LoanNo == loanNo && cg.Balance > 0);

                // Delete the loan and release all guarantees
                await _loanService.DeleteLoanAsync(loanNo, companyCode, User.Identity?.Name ?? "SYSTEM", reason);

                TempData["SuccessMessage"] = $"Loan {loanNo} has been successfully deleted. " +
                                             $"Released {guarantorCount} guarantor(s) and {collateralCount} collateral guarantee(s).";

                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting loan {loanNo}");
                TempData["ErrorMessage"] = $"Error deleting loan: {ex.Message}";
                return RedirectToAction("DeleteLoanIndex");
            }
        }

        #endregion


        #region All Loans View

        [HttpGet]
        public async Task<IActionResult> AllLoans(int page = 1, int pageSize = 10)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var searchDto = new LoanSearchDTO
                {
                    CompanyCode = companyCode
                };

                var allLoans = await _loanService.SearchLoansAsync(searchDto);

                // Load loan types for filter dropdown
                ViewBag.LoanTypes = await _loanTypeService.GetLoanTypesByCompanyAsync(companyCode);

                // Calculate pagination
                var totalItems = allLoans.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var loans = allLoans
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = totalItems;

                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all loans");
                ViewBag.ErrorMessage = "Error loading loans";
                return View(new List<LoanSummaryDTO>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportAllLoans(LoanSearchDTO searchDto)
        {
            try
            {
                searchDto.CompanyCode = GetUserCompanyCode();
                var loans = await _loanService.SearchLoansAsync(searchDto);

                // Build CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Loan No,Member No,Member Name,Loan Type,Principal Amount,Approved Amount,Disbursed Amount,Outstanding Balance,Application Date,Status");

                foreach (var loan in loans)
                {
                    csv.AppendLine($"\"{loan.LoanNo}\",\"{loan.MemberNo}\",\"{loan.MemberName}\",\"{loan.LoanType}\",{loan.PrincipalAmount},{loan.ApprovedAmount},{loan.DisbursedAmount},{loan.OutstandingBalance},\"{loan.ApplicationDate:dd/MM/yyyy}\",\"{loan.LoanStatus}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"AllLoans_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting all loans");
                ViewBag.ErrorMessage = "Error exporting data";
                return RedirectToAction("AllLoans");
            }
        }

        #endregion


        #region Dashboard

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var dashboard = await _loanService.GetLoanDashboardAsync(companyCode);

                ViewBag.CompanyCode = companyCode;
                ViewBag.UserName = User.Identity?.Name;


                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan dashboard");
                return View("Error");
            }
        }

        #endregion


        #region Loan Application

        public async Task<IActionResult> Apply()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // LOAD ONLY APPROVED LOAN TYPES
                var loanTypes = await _loanTypeService.GetActiveLoanTypesAsync(companyCode);

                ViewBag.LoanTypes = loanTypes;
                ViewBag.CompanyCode = companyCode;

                return View(new LoanApplicationDTO
                {
                    CompanyCode = companyCode,
                    ApplicationDate = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan application form");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(LoanApplicationDTO application)
        {
            try
            {
                application.CompanyCode = GetUserCompanyCode();
                application.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                // ============================================================
                // NEW: Check bridging/refinancing eligibility
                // ============================================================
                var bridgingCheck = await _loanService.CanApplyForLoanTypeAsync(
                    application.MemberNo,
                    application.LoanCode,
                    application.CompanyCode);

                if (!bridgingCheck.CanApply)
                {
                    ViewBag.LoanTypes = await _loanTypeService.GetActiveLoanTypesAsync(application.CompanyCode);
                    ModelState.AddModelError("", bridgingCheck.Message);

                    _logger.LogWarning($"Bridging check failed for member {application.MemberNo}, loan {application.LoanCode}: {bridgingCheck.Message}");
                    return View(application);
                }

                //// Existing loans check (general)
                //var existingLoansCheck = await _loanService.CheckExistingLoansAsync(
                //    application.MemberNo,
                //    application.CompanyCode);

                //if (existingLoansCheck.HasExistingLoan)
                //{
                //    ViewBag.LoanTypes = await _loanTypeService.GetActiveLoanTypesAsync(application.CompanyCode);
                //    ViewBag.ExistingLoans = existingLoansCheck.ExistingLoans;
                //    ModelState.AddModelError("MemberNo", existingLoansCheck.Message);
                //    return View(application);
                //}

                // Eligibility check
                var eligibility = await _loanService.CheckMemberEligibilityWithContributionsAsync(
                    application.MemberNo,
                    application.CompanyCode);

                if (!eligibility.IsEligible)
                {
                    ViewBag.LoanTypes = await _loanTypeService.GetActiveLoanTypesAsync(application.CompanyCode);
                    ModelState.AddModelError("MemberNo", eligibility.Message);
                    return View(application);
                }

                // Loan type specific eligibility
                var loanTypeEligibility = await _loanService.CheckMemberEligibilityAsync(
                    application.MemberNo,
                    application.LoanCode,
                    application.CompanyCode);

                if (!loanTypeEligibility.IsEligible)
                {
                    ViewBag.LoanTypes = await _loanTypeService.GetActiveLoanTypesAsync(application.CompanyCode);
                    ModelState.AddModelError("", loanTypeEligibility.Message);
                    return View(application);
                }

                // Submit the application
                var loan = await _loanService.ApplyForLoanAsync(application);

                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(
                    application.LoanCode,
                    application.CompanyCode);

                var requiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                        loanType.Guarantor != "No" &&
                                        loanType.Guarantor != "N";

                if (requiresGuarantor &&
                    (loan.Guaranteed != "0" && !string.IsNullOrEmpty(loan.Guaranteed)))
                {
                    TempData["SuccessMessage"] = "Loan application created! Please assign the required guarantor(s).";
                    return RedirectToAction("AssignGuarantor", new { loanNo = loan.LoanNo });
                }

                TempData["SuccessMessage"] = $"Loan application {loan.LoanNo} submitted successfully!";
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application");

                ViewBag.LoanTypes = await _loanTypeService.GetActiveLoanTypesAsync(GetUserCompanyCode());
                ViewBag.CompanyCode = GetUserCompanyCode();

                if (ex.Message.Contains("contributions") ||
                    ex.Message.Contains("eligibility") ||
                    ex.Message.Contains("loan"))
                {
                    ModelState.AddModelError("", ex.Message);
                }

                return View(application);
            }
        }


        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, loan.CompanyCode);

                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var appraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                var approvals = await _loanService.GetLoanApprovalsAsync(loanNo);
                var disbursement = await _loanService.GetLoanDisbursementAsync(loanNo);  
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                // ADD THIS: Get LoanBalance separately
                var loanBalance = await _loanService.GetLoanBalanceAsync(loanNo);  

                ViewBag.Guarantors = guarantors;
                ViewBag.Appraisal = appraisal;
                ViewBag.Approvals = approvals;
                ViewBag.Disbursement = disbursement; 
                ViewBag.LoanBalance = loanBalance;   
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;
                ViewBag.LoanTypeName = loanType?.LoanType ?? "Unknown";
                ViewBag.CanEdit = loan.Status == (int)Status.Draft || loan.Status == (int)Status.Submitted;
                ViewBag.CanAppraise = loan.Status == (int)Status.Submitted;
                ViewBag.CanApprove = loan.Status == (int)Status.Submitted;
                ViewBag.CanEndorse = loan.Status == (int)Status.Approved;
                ViewBag.CanDisburse = loan.Status == (int)Status.Endorsed;
                ViewBag.CanRepay = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;

                return View(loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan details for {loanNo}");
                return View("Error");
            }
        }

        #endregion



        #region Guarantor Management

        [HttpGet]
        public async Task<IActionResult> LoansNeedingGuarantors()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                _logger.LogInformation($"Loading loans needing guarantors for company: {companyCode}");

                // DEBUG: Test each database call individually
                _logger.LogInformation("Step 1: Getting loans from database...");

                // Get loans with Draft status (1)
                var rawLoans = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode && l.Status == (int)Status.Draft)
                    .OrderByDescending(l => l.ApplicDate)
                    .Select(l => new
                    {
                        l.LoanNo,
                        l.MemberNo,
                        l.LoanAmt,
                        l.Status,
                        l.ApplicDate,
                        l.Guaranteed,
                        l.LoanCode
                    })
                    .ToListAsync();

                _logger.LogInformation($"Step 1 complete: Found {rawLoans.Count} loans");

                _logger.LogInformation("Step 2: Getting max guarantors from SACCO service...");
                var maxGuarantors = await _saccoService.GetMaxGuarantorsAsync(companyCode);
                _logger.LogInformation($"Step 2 complete: MaxGuarantors = {maxGuarantors}");

                var loansNeedingGuarantors = new List<object>();

                foreach (var loan in rawLoans)
                {
                    try
                    {
                        _logger.LogInformation($"Processing loan: {loan.LoanNo}");

                        // Get member name
                        var member = await _context.Members
                            .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                        var memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;
                        if (string.IsNullOrEmpty(memberName)) memberName = loan.MemberNo;

                        // Get loan type
                        var loanType = await _context.Loantypes
                            .FirstOrDefaultAsync(l => l.LoanCode == loan.LoanCode && l.CompanyCode == companyCode);

                        var loanTypeName = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown";

                        // Check if loan requires guarantors
                        var requiresGuarantor = false;
                        var requiredGuarantorsCount = 0;

                        if (loanType != null && !string.IsNullOrEmpty(loanType.Guarantor))
                        {
                            var guarantorValue = loanType.Guarantor;
                            if (guarantorValue.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                guarantorValue.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                                guarantorValue == "1")
                            {
                                requiresGuarantor = true;
                                requiredGuarantorsCount = 1;
                            }
                            else if (guarantorValue.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                     guarantorValue.Equals("N", StringComparison.OrdinalIgnoreCase) ||
                                     guarantorValue == "0")
                            {
                                requiresGuarantor = false;
                                requiredGuarantorsCount = 0;
                            }
                            else if (int.TryParse(guarantorValue, out int count))
                            {
                                requiresGuarantor = count > 0;
                                requiredGuarantorsCount = count;
                            }
                        }

                        if (!requiresGuarantor)
                        {
                            continue;
                        }

                        // Get existing guarantors
                        var existingGuarantors = await _context.Loanguar
                            .Where(g => g.LoanNo == loan.LoanNo && g.Transfered == false)
                            .ToListAsync();

                        var totalGuarantee = existingGuarantors.Sum(g => g.Amount ?? 0);
                        var loanAmount = loan.LoanAmt ?? 0;
                        var isFullyGuaranteed = totalGuarantee >= loanAmount;
                        var assignedCount = existingGuarantors.Count;

                        var stillNeedsGuarantors = assignedCount < requiredGuarantorsCount || !isFullyGuaranteed;

                        if (stillNeedsGuarantors)
                        {
                            var selfGuaranteeEnabled = loanType?.SelfGuarantee ?? false;
                            var isApplicantGuarantor = existingGuarantors.Any(g => g.MemberNo == loan.MemberNo);

                            loansNeedingGuarantors.Add(new
                            {
                                LoanNo = loan.LoanNo,
                                MemberName = memberName,
                                LoanType = loanTypeName,
                                PrincipalAmount = loanAmount,
                                LoanStatus = "Draft",
                                TotalGuarantee = totalGuarantee,
                                RemainingAmount = loanAmount - totalGuarantee,
                                IsFullyGuaranteed = isFullyGuaranteed,
                                GuarantorCount = assignedCount,
                                MaxGuarantors = maxGuarantors,
                                RequiredGuarantors = requiredGuarantorsCount,
                                AssignedGuarantors = assignedCount,
                                ApprovedGuarantors = assignedCount,
                                RemainingRequired = requiredGuarantorsCount - assignedCount,
                                IsSelfGuarantee = selfGuaranteeEnabled,
                                IsApplicantGuarantor = isApplicantGuarantor,
                                NeedsGuarantors = true
                            });
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, $"Error processing loan {loan.LoanNo}");
                    }
                }

                ViewBag.MaxGuarantors = maxGuarantors;
                ViewBag.TotalLoans = rawLoans.Count;
                ViewBag.EligibleLoans = loansNeedingGuarantors.Count;

                return View(loansNeedingGuarantors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loans needing guarantors");
                TempData["ErrorMessage"] = $"Error loading loans: {ex.Message}";
                return View(new List<object>());
            }
        }
        private int ParseGuaranteedValue(string? guaranteedValue)
        {
            if (string.IsNullOrEmpty(guaranteedValue))
                return 0;

            if (guaranteedValue.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                guaranteedValue.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                guaranteedValue == "1")
                return 1;

            if (guaranteedValue.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                guaranteedValue.Equals("N", StringComparison.OrdinalIgnoreCase) ||
                guaranteedValue == "0")
                return 0;

            if (int.TryParse(guaranteedValue, out int result))
                return result;

            return 0;
        }

        [HttpGet]
        public async Task<IActionResult> AssignGuarantor(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("LoansNeedingGuarantors");
                }

                _logger.LogInformation($"Loan {loanNo} status from DB: {loan.Status}");

                if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
                {
                    TempData["ErrorMessage"] = $"Cannot assign guarantors to loan in status '{loan.Status}'.";
                    return RedirectToAction("AllLoans");
                }

                // Get loan type
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);

                // Get max guarantors from SACCO parameters
                var maxGuarantors = await _saccoService.GetMaxGuarantorsAsync(companyCode);

                // Get existing member guarantors
                var existingGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var totalMemberGuarantee = existingGuarantors.Sum(g => g.GuaranteeAmount);

                // ============================================================
                // GET COLLATERAL GUARANTEES FROM COLLOANGUAR TABLE
                // ============================================================
                var existingCollateralGuaranteesRaw = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .ToListAsync();

                _logger.LogInformation($"Found {existingCollateralGuaranteesRaw.Count} collateral guarantees for loan {loanNo}");

                // Convert to DTOs with collateral descriptions
                var existingCollateralGuarantees = new List<CollateralGuaranteeResponseDTO>();

                // Get all collateral types for descriptions
                var collateralTypes = await _context.Collaterals
                    .Where(c => c.CompanyCode == companyCode)
                    .ToDictionaryAsync(c => c.ColCode, c => c);

                foreach (var cg in existingCollateralGuaranteesRaw)
                {
                    var collateral = collateralTypes.GetValueOrDefault(cg.ColCode);
                    existingCollateralGuarantees.Add(new CollateralGuaranteeResponseDTO
                    {
                        Id = cg.Id,
                        ColCode = cg.ColCode,
                        Coldescription = collateral?.Coldescription ?? cg.ColCode,
                        DocNo = cg.DocNo,
                        MarketValue = cg.Mktvalue,
                        GuaranteeAmount = cg.Balance,
                        RemainingBalance = cg.Balance,
                        AssignedDate = DateTime.Now,
                        BlockchainTxId = cg.BlockchainTxId
                    });
                }

                var totalCollateralGuarantee = existingCollateralGuarantees.Sum(g => g.GuaranteeAmount);

                // Calculate totals including collateral
                var totalGuarantee = totalMemberGuarantee + totalCollateralGuarantee;
                var loanAmount = loan.LoanAmt ?? 0;
                var remainingAmount = loanAmount - totalGuarantee;
                var isFullyGuaranteed = remainingAmount <= 0;

                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;
                var canProceed = isFullyGuaranteed || isSelfGuarantee;

                // ============================================================
                // USE COLLATERAL SERVICE TO GET MEMBER'S COLLATERALS
                // This gets the actual collaterals owned by the loan applicant
                // ============================================================
                var memberCollaterals = await _collateralService.GetMemberCollateralsAsync(loan.MemberNo, companyCode);

                _logger.LogInformation($"Found {memberCollaterals.Count} collaterals for member {loan.MemberNo}");

                // Convert to the format expected by the view
                var collateralList = memberCollaterals.Select(c => new Collateral
                {
                    Id = c.Id,
                    ColCode = c.ColCode,
                    Coldescription = c.Coldescription,
                    Percentage = c.Percentage,
                    MemberNo = c.MemberNo,
                    CompanyCode = c.CompanyCode,
                    BlockchainTxId = c.BlockchainTxId,
                    Photo = c.HasPhoto ? Convert.FromBase64String(c.PhotoBase64 ?? "") : null,
                    PhotoContentType = c.PhotoContentType
                }).ToList();

                // ============================================================
                // GET USED COLLATERALS (across all active loans)
                // ============================================================
                var usedCollaterals = await _context.ColloanGuars
                    .Where(cg => cg.CompanyCode == companyCode && cg.Balance > 0)
                    .Select(cg => new { cg.ColCode, cg.DocNo })
                    .ToListAsync();

                var usedCollateralKeys = usedCollaterals
                    .Select(u => $"{u.ColCode}|{u.DocNo}")
                    .ToHashSet();

                var usedDocumentsByColCode = usedCollaterals
                    .GroupBy(u => u.ColCode)
                    .ToDictionary(g => g.Key, g => g.Select(u => u.DocNo).ToHashSet());

                // ============================================================
                // SET ViewBag PROPERTIES
                // ============================================================
                ViewBag.Loan = loan;
                ViewBag.LoanType = loanType;
                ViewBag.ExistingGuarantors = existingGuarantors;
                ViewBag.TotalGuarantee = totalGuarantee;
                ViewBag.TotalMemberGuarantee = totalMemberGuarantee;
                ViewBag.TotalCollateralGuarantee = totalCollateralGuarantee;
                ViewBag.RemainingAmount = remainingAmount > 0 ? remainingAmount : 0;
                ViewBag.IsFullyGuaranteed = isFullyGuaranteed;
                ViewBag.CanProceed = canProceed;
                ViewBag.IsSelfGuarantee = isSelfGuarantee;
                ViewBag.CompanyCode = companyCode;
                ViewBag.MaxGuarantors = maxGuarantors;
                ViewBag.LoanAmount = loanAmount;

                // ============================================================
                // PASS ONLY THE LOANEE'S COLLATERALS TO THE VIEW
                // ============================================================
                ViewBag.CollateralTypes = collateralList;
                ViewBag.ExistingCollateralGuarantees = existingCollateralGuarantees;
                ViewBag.UsedCollateralKeys = usedCollateralKeys;
                ViewBag.UsedDocumentsByColCode = usedDocumentsByColCode;
                ViewBag.TotalMemberCollaterals = memberCollaterals.Count;

                // Count available collaterals (not already used)
                var availableCount = memberCollaterals
                    .Count(c => !usedCollateralKeys.Contains($"{c.ColCode}|"));
                ViewBag.AvailableCollateralsCount = availableCount;

                return View(loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading guarantor assignment for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading guarantor assignment: {ex.Message}";
                return RedirectToAction("LoansNeedingGuarantors");
            }
        }


        [HttpGet]
        public async Task<IActionResult> DebugLoanStatus(string loanNo)
        {
            var companyCode = GetUserCompanyCode();

            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

            if (loan == null)
            {
                return Json(new { error = "Loan not found" });
            }

            var result = new
            {
                LoanNo = loan.LoanNo,
                StatusFromDB = loan.Status,
                LoanAmt = loan.LoanAmt,
                Interest = loan.Interest,
                RepayPeriod = loan.RepayPeriod,
                CreatedAt = loan.AuditDateTime,
                ApplicationDate = loan.ApplicDate
            };

            return Json(result);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGuarantor(string loanNo, string guarantorMemberNo, decimal guaranteeAmount, string remarks)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                _logger.LogInformation($"Adding guarantor to loan {loanNo}. Member: {guarantorMemberNo}, Amount: {guaranteeAmount}");

                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

                _logger.LogInformation($"Loan {loanNo} current status: {loan.Status}");

                if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
                {
                    return Json(new { success = false, message = $"Cannot add guarantors to loan in status '{loan.Status}'. Loan must be in Draft or Submitted status." });
                }

                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;

                bool isSelfGuarantor = guarantorMemberNo == loan.MemberNo;

                if (!isSelfGuarantee && isSelfGuarantor)
                {
                    return Json(new { success = false, message = "Self guarantee is not allowed for this loan type. Please add another member as guarantor." });
                }

                if (guaranteeAmount < 1000)
                {
                    return Json(new { success = false, message = "Guarantee amount must be at least KES 1,000." });
                }

                if (!isSelfGuarantor)
                {
                    var existingGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                    if (existingGuarantors.Any(g => g.GuarantorMemberNo == guarantorMemberNo))
                    {
                        return Json(new { success = false, message = "This member is already a guarantor for this loan." });
                    }
                }

                var guarantor = new GuarantorAssignmentDTO
                {
                    GuarantorMemberNo = guarantorMemberNo,
                    GuaranteeAmount = guaranteeAmount,
                    Remarks = remarks,
                    CompanyCode = companyCode
                };

                var result = await _loanService.AssignGuarantorAsync(loanNo, guarantor, User.Identity?.Name ?? "SYSTEM");

                // Return JSON success response
                return Json(new
                {
                    success = true,
                    message = $"Guarantor {guarantorMemberNo} assigned with KES {guaranteeAmount:N0}!",
                    guarantorId = result.Id,
                    guarantorMemberNo = result.MemberNo,
                    guaranteeAmount = result.Amount,
                    loanStatus = loan.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning guarantor for {loanNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveGuarantor(int guarantorId, string loanNo)
        {
            try
            {
                await _loanService.RejectGuarantorAsync(guarantorId, "Removed by user", User.Identity?.Name ?? "SYSTEM");

                TempData["SuccessMessage"] = "Guarantor removed successfully";
                return RedirectToAction("AssignGuarantor", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing guarantor {guarantorId}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignGuarantor", new { loanNo });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitToAppraisal(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

                _logger.LogInformation($"SubmitToAppraisal - Loan {loanNo} status from DB: {loan.Status}");

                // Allow both Draft and Submitted status to be submitted
                if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
                {
                    TempData["ErrorMessage"] = $"Cannot submit loan to appraisal. Current status: '{loan.Status}'. Loan must be in Draft or Submitted status.";
                    return RedirectToAction("AssignGuarantor", new { loanNo });
                }

                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;

                // 1. Get member guarantors from Loanguar table
                var memberGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var totalMemberGuarantee = memberGuarantors.Sum(g => g.GuaranteeAmount);

                // 2. Get collateral guarantees from ColloanGuar table
                var collateralGuarantees = await _loanService.GetLoanCollateralGuaranteesAsync(loanNo);
                var totalCollateralGuarantee = collateralGuarantees.Sum(g => g.GuaranteeAmount);

                // 3. Calculate TOTAL guarantee
                var totalGuarantee = totalMemberGuarantee + totalCollateralGuarantee;
                var loanAmount = loan.LoanAmt ?? 0;

                var isFullyGuaranteed = totalGuarantee >= loanAmount;
                var isApplicantGuarantor = memberGuarantors.Any(g => g.GuarantorMemberNo == loan.MemberNo);

                // Log the values for debugging
                _logger.LogInformation($"SubmitToAppraisal - Loan {loanNo}:");
                _logger.LogInformation($"  - Member Guarantee: KES {totalMemberGuarantee:N0}");
                _logger.LogInformation($"  - Collateral Guarantee: KES {totalCollateralGuarantee:N0}");
                _logger.LogInformation($"  - Total Guarantee: KES {totalGuarantee:N0}");
                _logger.LogInformation($"  - Loan Amount: KES {loanAmount:N0}");
                _logger.LogInformation($"  - Is Self Guarantee: {isSelfGuarantee}");
                _logger.LogInformation($"  - Is Applicant Guarantor: {isApplicantGuarantor}");

                // ✅ FIX: Allow submission if self-guarantee is enabled and applicant is a guarantor
                // OR if loan is fully guaranteed
                bool canSubmit = false;

                if (isSelfGuarantee && isApplicantGuarantor)
                {
                    // Self-guarantee enabled and applicant is a guarantor - can submit even if not fully guaranteed
                    canSubmit = true;
                    _logger.LogInformation($"Self-guarantee enabled. Loan can be submitted with partial guarantee of KES {totalGuarantee:N0} out of KES {loanAmount:N0}");
                }
                else if (isFullyGuaranteed)
                {
                    // Fully guaranteed by other members or collateral
                    canSubmit = true;
                    _logger.LogInformation($"Loan fully guaranteed. Can submit to appraisal.");
                }
                else
                {
                    var remaining = loanAmount - totalGuarantee;
                    TempData["ErrorMessage"] = $"Loan requires guarantee of KES {remaining:N0} more. Total guarantee: KES {totalGuarantee:N0}, Loan amount: KES {loanAmount:N0}. Please add more guarantees or enable self-guarantee.";
                    return RedirectToAction("AssignGuarantor", new { loanNo });
                }

                if (!canSubmit)
                {
                    TempData["ErrorMessage"] = "Cannot submit loan to appraisal. Please ensure guarantees are in place.";
                    return RedirectToAction("AssignGuarantor", new { loanNo });
                }

                // Update loan status to Submitted (if not already)
                if (loan.Status != (int)Status.Submitted)
                {
                    loan.Status = (int)Status.Submitted;
                }
                loan.Posted = "SUBMIT";
                loan.UserName = User.Identity?.Name ?? "SYSTEM";
                loan.AuditDateTime = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Loan {loanNo} has been submitted for appraisal!";
                return RedirectToAction("Appraise", new { loanNo });
                // return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan {loanNo} to appraisal");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignGuarantor", new { loanNo });
            }
        }       


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SkipGuarantors(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;

                if (!isSelfGuarantee)
                {
                    TempData["ErrorMessage"] = "This loan requires guarantors. Please add guarantors or enable self guarantee.";
                    return RedirectToAction("AssignGuarantor", new { loanNo });
                }

                await _loanService.UpdateLoanStatusAsync(loanNo, Status.Submitted.ToString(), User.Identity?.Name ?? "SYSTEM",
                      "Self guarantee enabled. Moving to appraisal.");

                TempData["SuccessMessage"] = $"Loan {loanNo} submitted for appraisal (self guarantee).";
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan {loanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignGuarantor", new { loanNo });
            }
        }

        #endregion


        #region Collateral Guarantee Management


        [HttpGet]
        public async Task<IActionResult> AssignCollateralGuarantee(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                _logger.LogInformation($"=== AssignCollateralGuarantee called ===");
                _logger.LogInformation($"CompanyCode: '{companyCode}'");
                _logger.LogInformation($"LoanNo: '{loanNo}'");

                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("AllLoans");
                }

                if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
                {
                    TempData["ErrorMessage"] = $"Cannot add collateral guarantees to loan in status '{loan.Status}'";
                    return RedirectToAction("AllLoans");
                }

                // ============================================================
                // USE COLLATERAL SERVICE TO GET MEMBER'S COLLATERALS
                // This gets the actual collaterals owned by the loan applicant
                // ============================================================
                var memberCollateralsDTOs = await _collateralService.GetMemberCollateralsAsync(loan.MemberNo, companyCode);

                _logger.LogInformation($"Found {memberCollateralsDTOs.Count} collaterals for member {loan.MemberNo}");

                // Convert to the format expected by the view
                var memberCollaterals = memberCollateralsDTOs.Select(c => new Collateral
                {
                    Id = c.Id,
                    ColCode = c.ColCode,
                    Coldescription = c.Coldescription,
                    Percentage = c.Percentage,
                    MemberNo = c.MemberNo,
                    CompanyCode = c.CompanyCode,
                    BlockchainTxId = c.BlockchainTxId,
                    Photo = c.HasPhoto ? Convert.FromBase64String(c.PhotoBase64 ?? "") : null,
                    PhotoContentType = c.PhotoContentType
                }).ToList();

                // ============================================================
                // GET USED COLLATERALS
                // ============================================================
                var usedCollaterals = await _context.ColloanGuars
                    .Where(cg => cg.CompanyCode == companyCode && cg.Balance > 0)
                    .Select(cg => new { cg.ColCode, cg.DocNo })
                    .ToListAsync();

                var usedCollateralKeys = usedCollaterals
                    .Select(u => $"{u.ColCode}|{u.DocNo}")
                    .ToHashSet();

                var usedDocumentsByColCode = usedCollaterals
                    .GroupBy(u => u.ColCode)
                    .ToDictionary(g => g.Key, g => g.Select(u => u.DocNo).ToHashSet());

                // Get existing collateral guarantees for THIS loan
                var existingCollateralGuarantees = await _loanService.GetLoanCollateralGuaranteesAsync(loanNo);

                var totalCollateralGuarantee = existingCollateralGuarantees.Sum(g => g.GuaranteeAmount);
                var existingMemberGuarantees = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var totalMemberGuarantee = existingMemberGuarantees.Sum(g => g.GuaranteeAmount);

                var totalGuarantee = totalCollateralGuarantee + totalMemberGuarantee;
                var loanAmount = loan.LoanAmt ?? 0;
                var remainingAmount = loanAmount - totalGuarantee;
                var isFullyGuaranteed = remainingAmount <= 0;

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                var saccoParams = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var existingGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                // ============================================================
                // SET ViewBag PROPERTIES
                // ============================================================
                ViewBag.Loan = loan;
                ViewBag.LoanAmount = loanAmount;
                ViewBag.TotalGuarantee = totalGuarantee;
                ViewBag.TotalCollateralGuarantee = totalCollateralGuarantee;
                ViewBag.TotalMemberGuarantee = totalMemberGuarantee;
                ViewBag.RemainingAmount = remainingAmount > 0 ? remainingAmount : 0;
                ViewBag.IsFullyGuaranteed = isFullyGuaranteed;
                ViewBag.ExistingCollateralGuarantees = existingCollateralGuarantees;

                // ============================================================
                // PASS ONLY THE LOANEE'S COLLATERALS TO THE VIEW
                // ============================================================
                ViewBag.CollateralTypes = memberCollaterals;
                ViewBag.UsedCollateralKeys = usedCollateralKeys;
                ViewBag.UsedDocumentsByColCode = usedDocumentsByColCode;
                ViewBag.CompanyCode = companyCode;
                ViewBag.IsSelfGuarantee = loanType?.SelfGuarantee ?? false;
                ViewBag.MaxGuarantors = saccoParams?.MaxGuarantor ?? 5;
                ViewBag.LoanType = loanType;
                ViewBag.ExistingGuarantors = existingGuarantors;
                ViewBag.TotalMemberCollaterals = memberCollateralsDTOs.Count;

                // Count available collaterals (not already used)
                var availableCount = memberCollateralsDTOs
                    .Count(c => !usedCollateralKeys.Contains($"{c.ColCode}|"));
                ViewBag.AvailableCollateralsCount = availableCount;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading collateral guarantee assignment for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading page: {ex.Message}";
                return RedirectToAction("AllLoans");
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> AssignCollateralGuarantee(string loanNo)
        //{
        //    try
        //    {
        //        var companyCode = GetUserCompanyCode();

        //        LOG THE COMPANY CODE FOR DEBUGGING
        //        _logger.LogInformation($"=== AssignCollateralGuarantee called ===");
        //        _logger.LogInformation($"CompanyCode: '{companyCode}'");
        //        _logger.LogInformation($"LoanNo: '{loanNo}'");

        //        var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

        //        if (loan == null)
        //        {
        //            TempData["ErrorMessage"] = "Loan not found";
        //            return RedirectToAction("AllLoans");
        //        }

        //        if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
        //        {
        //            TempData["ErrorMessage"] = $"Cannot add collateral guarantees to loan in status '{loan.Status}'";
        //            return RedirectToAction("AllLoans");
        //        }

        //         ============================================================
        //         GET ALL COLLATERAL TYPES
        //         ============================================================
        //        var allCollateralTypes = await _context.Collaterals
        //            .Where(c => c.CompanyCode == companyCode)
        //            .OrderBy(c => c.ColCode)
        //            .ToListAsync();

        //        var allCollateralTypes = await _context.Collaterals
        //           .Where(c => c.CompanyCode == companyCode && c.MemberNo == loan.MemberNo)
        //           .OrderBy(c => c.ColCode)
        //           .ToListAsync();

        //        _logger.LogInformation($"Found {allCollateralTypes.Count} collaterals for member {loan.MemberNo}");

        //         ============================================================
        //         GET USED COLLATERALS(already assigned to ANY active loan)
        //         ============================================================
        //         Get all active collateral guarantees for ANY loan (not just this one)
        //         This prevents using the same collateral document for multiple loans
        //        var usedCollaterals = await _context.ColloanGuars
        //            .Where(cg => cg.CompanyCode == companyCode && cg.Balance > 0)
        //            .Select(cg => new { cg.ColCode, cg.DocNo })
        //            .ToListAsync();

        //        Create a set of used collateral identifiers(ColCode + DocNo combination)
        //        var usedCollateralKeys = usedCollaterals
        //            .Select(u => $"{u.ColCode}|{u.DocNo}")
        //            .ToHashSet();

        //        _logger.LogInformation($"Found {usedCollateralKeys.Count} used collateral document(s)");

        //         ============================================================
        //         FILTER OUT USED COLLATERALS FROM DROPDOWN
        //         ============================================================
        //         For collateral types dropdown, we need to know which ones have
        //         available documents.Since the same ColCode can have multiple DocNo,
        //         we need to track available documents per collateral type.

        //         Get all used documents grouped by ColCode
        //        var usedDocumentsByColCode = usedCollaterals
        //            .GroupBy(u => u.ColCode)
        //            .ToDictionary(g => g.Key, g => g.Select(u => u.DocNo).ToHashSet());

        //        Build a list of available collaterals with their available document counts
        //        var availableCollaterals = new List<dynamic>();

        //        foreach (var collateral in allCollateralTypes)
        //            {
        //                Get used documents for this collateral type

        //               var usedDocs = usedDocumentsByColCode.ContainsKey(collateral.ColCode)
        //                   ? usedDocumentsByColCode[collateral.ColCode]
        //                   : new HashSet<string>();

        //                For now, we don't have a list of all documents per collateral type.

        //                In a real system, you would have a MemberCollateral table.
        //                For this implementation, we'll assume each collateral type can be used

        //                multiple times with different document numbers, but the SAME document

        //                cannot be reused.

        //                We'll still show the collateral type in dropdown, but validation

        //                will prevent reusing the same document number.

        //               availableCollaterals.Add(new
        //               {
        //                   collateral.ColCode,
        //                   collateral.Coldescription,
        //                   collateral.Percentage,
        //                   HasUsedDocuments = usedDocs.Any(),
        //                   UsedDocumentCount = usedDocs.Count
        //               });
        //        }

        //        Get existing collateral guarantees for THIS loan

        //       var existingCollateralGuarantees = await _loanService.GetLoanCollateralGuaranteesAsync(loanNo);

        //        _logger.LogInformation($"Found {existingCollateralGuarantees.Count} collateral guarantees for loan {loanNo}");
        //        foreach (var g in existingCollateralGuarantees)
        //            {
        //                _logger.LogInformation($"  - Collateral: {g.ColCode}, Doc: {g.DocNo}, Amount: {g.GuaranteeAmount:C}");
        //            }

        //        var totalCollateralGuarantee = existingCollateralGuarantees.Sum(g => g.GuaranteeAmount);

        //        Get existing member guarantees
        //        var existingMemberGuarantees = await _loanService.GetLoanGuarantorsAsync(loanNo);
        //        var totalMemberGuarantee = existingMemberGuarantees.Sum(g => g.GuaranteeAmount);

        //        var totalGuarantee = totalCollateralGuarantee + totalMemberGuarantee;
        //        var loanAmount = loan.LoanAmt ?? 0;
        //        var remainingAmount = loanAmount - totalGuarantee;
        //        var isFullyGuaranteed = remainingAmount <= 0;

        //        Get loan type
        //       var loanType = await _context.Loantypes
        //           .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

        //        Get max guarantors from SACCO parameters
        //        var saccoParams = await _context.SaccoParram
        //            .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

        //        Get existing member guarantors
        //        var existingGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

        //         ============================================================
        //         SET ALL ViewBag PROPERTIES
        //         ============================================================
        //        ViewBag.Loan = loan;
        //        ViewBag.LoanAmount = loanAmount;
        //        ViewBag.TotalGuarantee = totalGuarantee;
        //        ViewBag.TotalCollateralGuarantee = totalCollateralGuarantee;
        //        ViewBag.TotalMemberGuarantee = totalMemberGuarantee;
        //        ViewBag.RemainingAmount = remainingAmount > 0 ? remainingAmount : 0;
        //        ViewBag.IsFullyGuaranteed = isFullyGuaranteed;
        //        ViewBag.ExistingCollateralGuarantees = existingCollateralGuarantees;
        //        ViewBag.CollateralTypes = allCollateralTypes;  // Pass all types, but we'll track used docs
        //        ViewBag.UsedCollateralKeys = usedCollateralKeys;  // Pass used keys for validation
        //        ViewBag.UsedDocumentsByColCode = usedDocumentsByColCode;
        //        ViewBag.CompanyCode = companyCode;
        //        ViewBag.IsSelfGuarantee = loanType?.SelfGuarantee ?? false;
        //        ViewBag.MaxGuarantors = saccoParams?.MaxGuarantor ?? 5;
        //        ViewBag.LoanType = loanType;
        //        ViewBag.ExistingGuarantors = existingGuarantors;

        //        return View();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error loading collateral guarantee assignment for {loanNo}");
        //        TempData["ErrorMessage"] = $"Error loading page: {ex.Message}";
        //        return RedirectToAction("AllLoans");
        //    }
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCollateralGuarantee(CollateralGuaranteeDTO guaranteeDto)
        {
            try
            {
                _logger.LogInformation($"=== ADD COLLATERAL GUARANTEE POST ===");
                _logger.LogInformation($"LoanNo: {guaranteeDto.LoanNo}");
                _logger.LogInformation($"ColCode: {guaranteeDto.ColCode}");
                _logger.LogInformation($"Amount: {guaranteeDto.GuaranteeAmount:C}");

                guaranteeDto.CompanyCode = GetUserCompanyCode();

                var result = await _loanService.AssignCollateralGuaranteeAsync(guaranteeDto, User.Identity?.Name ?? "SYSTEM");

                _logger.LogInformation($"Collateral guarantee created with ID: {result.Id}, Balance: {result.Balance:C}");

                // Verify it was saved
                var verify = await _context.ColloanGuars.FirstOrDefaultAsync(c => c.Id == result.Id);
                _logger.LogInformation($"Verification - Found: {verify != null}, Balance: {verify?.Balance:C}");

                TempData["SuccessMessage"] = $"Collateral {guaranteeDto.ColCode} (Doc: {guaranteeDto.DocNo}) assigned as guarantee for KES {guaranteeDto.GuaranteeAmount:N0}";

                return RedirectToAction("AssignCollateralGuarantee", new { loanNo = guaranteeDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding collateral guarantee for loan {guaranteeDto.LoanNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignCollateralGuarantee", new { loanNo = guaranteeDto.LoanNo });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCollateralGuarantee(long collateralGuaranteeId, string loanNo, string reason)
        {
            try
            {
                _logger.LogInformation($"Removing collateral guarantee {collateralGuaranteeId} from loan {loanNo}");

                await _loanService.ReleaseCollateralGuaranteeAsync(collateralGuaranteeId, User.Identity?.Name ?? "SYSTEM", reason);

                TempData["SuccessMessage"] = "Collateral guarantee removed successfully";

                return RedirectToAction("AssignCollateralGuarantee", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing collateral guarantee {collateralGuaranteeId}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssignCollateralGuarantee", new { loanNo });
            }
        }

        #endregion


        #region Loan Appraisal

        [HttpGet]
        public async Task<IActionResult> PendingAppraisal()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // Get loans with Submitted status (2) directly
                var submittedLoans = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode && l.Status == (int)Status.Submitted)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                _logger.LogInformation($"Found {submittedLoans.Count} loans with Submitted status");

                var pendingAppraisal = new List<dynamic>();

                foreach (var loan in submittedLoans)
                {
                    // Check if already appraised
                    var existingAppraisal = await _context.Appraisal
                        .FirstOrDefaultAsync(a => a.LoanNo == loan.LoanNo);

                    if (existingAppraisal != null)
                    {
                        _logger.LogInformation($"Loan {loan.LoanNo} already appraised, skipping");
                        continue;
                    }

                    // Get member name
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                    var memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;
                    if (string.IsNullOrEmpty(memberName)) memberName = loan.MemberNo;

                    // Get loan type name
                    var loanType = await _context.Loantypes
                        .FirstOrDefaultAsync(l => l.LoanCode == loan.LoanCode && l.CompanyCode == companyCode);

                    var loanTypeName = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown";

                    // Get total guarantee
                    var existingGuarantors = await _context.Loanguar
                        .Where(g => g.LoanNo == loan.LoanNo && g.Transfered == false)
                        .ToListAsync();

                    var totalGuarantee = existingGuarantors.Sum(g => g.Amount ?? 0);

                    pendingAppraisal.Add(new
                    {
                        LoanNo = loan.LoanNo,
                        MemberName = memberName,
                        LoanType = loanTypeName,
                        PrincipalAmount = loan.LoanAmt ?? 0,
                        TotalGuarantee = totalGuarantee,
                        ApplicationDate = loan.ApplicDate
                    });
                }

                ViewBag.Count = pendingAppraisal.Count;
                _logger.LogInformation($"Returning {pendingAppraisal.Count} loans for appraisal");

                return View(pendingAppraisal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending appraisal loans");
                TempData["ErrorMessage"] = $"Error loading loans pending appraisal: {ex.Message}";
                return View(new List<dynamic>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Appraise(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);
                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("Index");
                }

                if (loan.Status != (int)Status.Submitted)
                {
                    TempData["ErrorMessage"] = $"Loan cannot be appraised in status '{loan.Status}'. Loan must be in Submitted status.";
                    return RedirectToAction("AllLoans");
                }

                var existingAppraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                if (existingAppraisal != null)
                {
                    TempData["ErrorMessage"] = "This loan has already been appraised.";
                    return RedirectToAction("AllLoans");
                }

                var member = await _contributionService.GetMemberByMemberNoAsync(loan.MemberNo);
                if (member == null)
                {
                    TempData["ErrorMessage"] = "Member not found";
                    return RedirectToAction("Index");
                }

                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);

                var requiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                        loanType.Guarantor != "No" &&
                                        loanType.Guarantor != "N";

                // GET TOTAL GUARANTEE
                var totalGuarantee = await _loanService.GetTotalGuaranteeForLoanAsync(loanNo, companyCode);

                // Also get individual breakdown for display
                var memberGuarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var totalMemberGuarantee = memberGuarantors.Sum(g => g.GuaranteeAmount);

                var collateralGuarantees = await _loanService.GetLoanCollateralGuaranteesAsync(loanNo);
                var totalCollateralGuarantee = collateralGuarantees.Sum(g => g.GuaranteeAmount);

                var loanAmount = loan.LoanAmt ?? 0;
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;
                var isApplicantGuarantor = memberGuarantors.Any(g => g.GuarantorMemberNo == loan.MemberNo);

                _logger.LogInformation($"Loan {loanNo}: Member Guarantee: {totalMemberGuarantee:C}, Collateral Guarantee: {totalCollateralGuarantee:C}, Total: {totalGuarantee:C}");

                // ✅ FIX: Amount to appraise = MIN(loanAmount, totalGuarantee)
                // If guarantee is less than loan amount, only appraise the guaranteed amount
                decimal amountToAppraise;
                string amountSource;

                if (requiresGuarantor)
                {
                    // Cap the appraisal amount by the total guarantee
                    amountToAppraise = Math.Min(loanAmount, totalGuarantee);

                    if (totalGuarantee <= 0)
                    {
                        TempData["ErrorMessage"] = "This loan requires guarantors but no guarantees found. Please add member guarantors or collateral guarantees first.";
                        return RedirectToAction("AssignGuarantor", new { loanNo });
                    }

                    if (amountToAppraise <= 0)
                    {
                        TempData["ErrorMessage"] = $"Cannot appraise loan. Total guarantee amount is {totalGuarantee:C} which is less than minimum appraisal amount.";
                        return RedirectToAction("AssignGuarantor", new { loanNo });
                    }

                    if (isSelfGuarantee && isApplicantGuarantor)
                    {
                        amountSource = $"Appraisal Amount Limited to Guarantee: KES {amountToAppraise:N0} (Loan Applied: {loanAmount:C}, Total Guarantee: {totalGuarantee:C}) - Self Guarantee Enabled";
                    }
                    else
                    {
                        amountSource = $"Appraisal Amount Limited to Guarantee: KES {amountToAppraise:N0} (Loan Applied: {loanAmount:C}, Total Guarantee: {totalGuarantee:C})";
                    }

                    _logger.LogInformation($"Appraisal amount capped at guarantee: {amountToAppraise:C} (Loan: {loanAmount:C}, Guarantee: {totalGuarantee:C})");
                }
                else
                {
                    amountToAppraise = loanAmount;
                    amountSource = "Applied Principal Amount (No Guarantor Required)";
                }

                decimal interestRate = 0;
                if (!string.IsNullOrEmpty(loanType.Interest) && decimal.TryParse(loanType.Interest, out interestRate))
                {
                    if (interestRate > 1 && interestRate <= 100)
                    {
                        interestRate = interestRate / 100;
                    }
                }

                var appraisalDto = new LoanAppraisalDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    AppraisedBy = User.Identity?.Name ?? "SYSTEM",
                    AppliedAmount = loanAmount,
                    RecommendedAmount = amountToAppraise,
                    RecommendedInterestRate = interestRate * 100,
                    RecommendedPeriod = loan.RepayPeriod ?? 12,
                    AppraisalNotes = $"Loan Type: {loanType.LoanType}\n" +
                                    $"Loan Applied: KES {loanAmount:N0}\n" +
                                    $"Total Guarantee Available: KES {totalGuarantee:N0}\n" +
                                    $"Amount to Appraise: KES {amountToAppraise:N0}\n" +
                                    $"Member Guarantee: KES {totalMemberGuarantee:N0}\n" +
                                    $"Collateral Guarantee: KES {totalCollateralGuarantee:N0}\n"
                };

                ViewBag.Loan = loan;
                ViewBag.Member = member;
                ViewBag.LoanType = loanType;
                ViewBag.RequiresGuarantor = requiresGuarantor;
                ViewBag.TotalMemberGuarantee = totalMemberGuarantee;
                ViewBag.TotalCollateralGuarantee = totalCollateralGuarantee;
                ViewBag.TotalGuarantee = totalGuarantee;
                ViewBag.AmountToAppraise = amountToAppraise;
                ViewBag.AmountSource = amountSource;
                ViewBag.MemberGuarantors = memberGuarantors;
                ViewBag.CollateralGuarantees = collateralGuarantees;
                ViewBag.IsSelfGuarantee = isSelfGuarantee;
                ViewBag.IsApplicantGuarantor = isApplicantGuarantor;

                return View(appraisalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading appraisal form for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading appraisal: {ex.Message}";
                return RedirectToAction("AllLoans");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Appraise(LoanAppraisalDTO appraisalDto, bool printReport = true)
        {
            try
            {
                _logger.LogInformation($"=== APPRAISE POST CALLED ===");
                _logger.LogInformation($"LoanNo: {appraisalDto.LoanNo}");
                _logger.LogInformation($"AppraisalDecision: {appraisalDto.AppraisalDecision}");
                _logger.LogInformation($"RecommendedAmount: {appraisalDto.RecommendedAmount}");
                _logger.LogInformation($"PrintReport: {printReport}");

                if (string.IsNullOrEmpty(appraisalDto.AppraisalDecision))
                {
                    TempData["ErrorMessage"] = "Please select an appraisal decision.";
                    return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
                }

                if (string.IsNullOrEmpty(appraisalDto.AppraisalNotes))
                {
                    TempData["ErrorMessage"] = "Please enter appraisal notes.";
                    return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning($"ModelState invalid: {errors}");

                    TempData["ErrorMessage"] = $"Validation error: {errors}";
                    return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
                }

                appraisalDto.CompanyCode = GetUserCompanyCode();
                appraisalDto.AppraisedBy = User.Identity?.Name ?? "SYSTEM";

                // VERIFY GUARANTEES STILL EXIST BEFORE APPRAISAL
                var memberGuarantees = await _context.Loanguar
                    .Where(g => g.LoanNo == appraisalDto.LoanNo && g.Transfered == false)
                    .SumAsync(g => g.Amount ?? 0);

                var collateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == appraisalDto.LoanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);

                var totalGuarantee = memberGuarantees + collateralGuarantees;

                var loan = await _loanService.GetLoanByNoForDisplayAsync(appraisalDto.LoanNo, appraisalDto.CompanyCode);
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, appraisalDto.CompanyCode);
                var requiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                        loanType.Guarantor != "No" &&
                                        loanType.Guarantor != "N";
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;
                var isApplicantGuarantor = await _context.Loanguar
                    .AnyAsync(g => g.LoanNo == appraisalDto.LoanNo && g.MemberNo == loan.MemberNo && g.Transfered == false);

                var loanAmount = loan.LoanAmt ?? 0;
                var maxAppraisalAmount = requiresGuarantor ? Math.Min(loanAmount, totalGuarantee) : loanAmount;

                _logger.LogInformation($"Pre-appraisal verification - Member: {memberGuarantees:C}, Collateral: {collateralGuarantees:C}, Total: {totalGuarantee:C}");
                _logger.LogInformation($"Self Guarantee: {isSelfGuarantee}, Applicant Guarantor: {isApplicantGuarantor}");
                _logger.LogInformation($"Max Appraisal Amount: {maxAppraisalAmount:C} (Loan: {loanAmount:C}, Guarantee: {totalGuarantee:C})");

                if (requiresGuarantor)
                {
                    if (totalGuarantee <= 0)
                    {
                        TempData["ErrorMessage"] = "This loan requires guarantees but no guarantees found. Cannot proceed with appraisal.";
                        return RedirectToAction("AssignGuarantor", new { loanNo = appraisalDto.LoanNo });
                    }

                    if (appraisalDto.RecommendedAmount > maxAppraisalAmount)
                    {
                        TempData["ErrorMessage"] = $"Recommended amount KES {appraisalDto.RecommendedAmount:N0} exceeds the maximum allowed based on guarantees KES {maxAppraisalAmount:N0}.";
                        return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
                    }
                }

                _logger.LogInformation($"Calling AppraiseLoanAsync for loan {appraisalDto.LoanNo}");

                var appraisal = await _loanService.AppraiseLoanAsync(appraisalDto);

                if (appraisal != null)
                {
                    _logger.LogInformation($"Appraisal completed successfully for loan {appraisalDto.LoanNo}");

                    if (appraisalDto.AppraisalDecision == "Recommend")
                    {
                        TempData["SuccessMessage"] = $"Loan appraisal completed successfully. Loan has been moved to Approved status for endorsement.";
                    }
                    else if (appraisalDto.AppraisalDecision == "NotRecommend")
                    {
                        TempData["SuccessMessage"] = $"Loan has been rejected and will be closed.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"Loan appraisal completed with decision: {appraisalDto.AppraisalDecision}";
                    }

                    if (printReport)
                    {
                        return RedirectToAction("PrintAppraisalReport", new { loanNo = appraisalDto.LoanNo });
                    }

                    return RedirectToAction("Endorse", new { loanNo = appraisalDto.LoanNo });                    
                    //return RedirectToAction("AllLoans");
                }
                else
                {
                    TempData["ErrorMessage"] = "Appraisal returned null. Please check logs.";
                    return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan appraisal: {ex.Message}");
                TempData["ErrorMessage"] = $"Error submitting appraisal: {ex.Message}";
                return RedirectToAction("Appraise", new { loanNo = appraisalDto.LoanNo });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintAppraisalReport(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                // Get Loan Details
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                if (loan == null)
                {
                    return NotFound();
                }

                var member = await _contributionService.GetMemberByMemberNoAsync(loan.MemberNo);
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                var appraisal = await _loanService.GetLoanAppraisalAsync(loanNo);

                // Get Guarantors for this loan
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                // Get Total Shares from ContribShares
                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == loan.MemberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                // Get Repayment Period
                int repaymentPeriod = loan.RepayPeriod ?? 12;

                // Get company address
                string companyAddress = "P.O. Box 12345 - 00100, Nairobi, Kenya";
                if (company != null)
                {
                    var addressParts = new List<string>();
                    if (!string.IsNullOrEmpty(company.Address)) addressParts.Add(company.Address);
                    if (!string.IsNullOrEmpty(company.County)) addressParts.Add(company.County);
                    if (addressParts.Any()) companyAddress = string.Join(", ", addressParts);
                }

                var viewModel = new AppraisalReportViewModel
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    MemberIdNo = member?.Idno ?? "N/A",
                    MemberPhone = member?.PhoneNo ?? "N/A",
                    LoanType = loanType?.LoanType ?? "N/A",
                    AppliedAmount = loan.LoanAmt ?? 0,
                    RecommendedAmount = appraisal?.AmtRecommended ?? 0,
                    InterestRate = appraisal?.Interest ?? loan.Interest ?? 0,
                    RepaymentPeriod = repaymentPeriod,
                    ApplicationDate = loan.ApplicDate,
                    AppraisalDate = DateTime.Now,
                    AppraisalNotes = appraisal?.Reason ?? "",
                    TotalShares = totalShares,
                    Guarantors = guarantors,
                    TotalGuaranteeAmount = guarantors.Sum(g => g.GuaranteeAmount),
                    AppraisedBy = appraisal?.OfficerNames ?? User.Identity?.Name ?? "SYSTEM",
                    CompanyCode = companyCode,
                    CompanyName = company?.CompanyName ?? "SACCO Blockchain System",
                    CompanyAddress = companyAddress,
                    CompanyPhone = company?.Telephone ?? "+254 700 000 000",
                    CompanyEmail = company?.Email ?? "info@sacco.co.ke"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing appraisal report for loan {loanNo}");
                TempData["ErrorMessage"] = "Error printing appraisal report: " + ex.Message;
                return RedirectToAction("AllLoans");
            }
        }

        private async Task<string> GetCompanyNameAsync(string companyCode)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                return company?.CompanyName ?? "SACCO System";
            }
            catch
            {
                return "SACCO System";
            }
        }

        [HttpGet]
        public async Task<IActionResult> Approve(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.Status != (int)Status.Approved)
                {
                    ViewBag.ErrorMessage = "Loan is not ready for approval. Loan must be appraised first.";
                    return RedirectToAction("AllLoans");
                }

                var appraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                if (appraisal == null)
                {
                    ViewBag.ErrorMessage = "Loan must be appraised before approval";
                    return RedirectToAction("AllLoans");
                }

                var approvals = await _loanService.GetLoanApprovalsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Appraisal = appraisal;
                ViewBag.PreviousApprovals = approvals;
                ViewBag.ApprovalLevel = approvals.Count + 1;

                var approvalDto = new LoanApprovalDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    ApprovedBy = User.Identity?.Name ?? "SYSTEM",
                    ApprovalLevel = approvals.Count + 1,
                    IsFinalApproval = (approvals.Count + 1) >= GetRequiredApprovalLevels()
                };

                return View(approvalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading approval form for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(LoanApprovalDTO approvalDto)
        {
            try
            {
                approvalDto.CompanyCode = GetUserCompanyCode();
                approvalDto.ApprovedBy = User.Identity?.Name ?? "SYSTEM";

                var approval = await _loanService.ApproveLoanAsync(approvalDto);

                TempData["SuccessMessage"] = $"Loan {approvalDto.ApprovalStatus} successfully";
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan approval");
                TempData["ErrorMessage"] = ex.Message;
                return View(approvalDto);
            }
        }

        #endregion


        #region Loan Endorsement

        // GET: Pending Endorsement (Updated to show both pending and endorsed)
        [HttpGet]
        public async Task<IActionResult> PendingEndorsement()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // Get loans with their related data in one query
                var loansData = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode &&
                               (l.Status == (int)Status.Approved ||
                                l.Status == (int)Status.Endorsed ||
                                l.Status == (int)Status.Rejected))
                    .Select(l => new
                    {
                        Loan = l,
                        Member = _context.Members
                            .FirstOrDefault(m => m.MemberNo == l.MemberNo && m.CompanyCode == companyCode),
                        LoanType = _context.Loantypes
                            .FirstOrDefault(lt => lt.LoanCode == l.LoanCode && lt.CompanyCode == companyCode),
                        Endorsement = _context.Endmain
                            .FirstOrDefault(e => e.LoanNo == l.LoanNo && e.CompanyCode == companyCode),
                        IsDisbursed = _context.Loanbal
                            .Any(lb => lb.LoanNo == l.LoanNo && lb.Companycode == companyCode)
                    })
                    .OrderByDescending(x => x.Loan.ApplicDate)
                    .ToListAsync();

                var pendingEndorsement = new List<object>();
                var endorsedLoans = new List<object>();

                foreach (var data in loansData)
                {
                    var loan = data.Loan;
                    var member = data.Member;
                    var loanType = data.LoanType;
                    var endorsement = data.Endorsement;
                    var isDisbursed = data.IsDisbursed;

                    // Build member name
                    string memberName = "N/A";
                    if (member != null)
                    {
                        memberName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrEmpty(memberName)) memberName = member.MemberNo;
                    }

                    // Build loan type name
                    string loanTypeName = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown";

                    // Check if loan has endorsement
                    var hasEndorsement = endorsement != null;

                    // Get the actual status name
                    string statusName = ((Status)loan.Status).ToString();

                    // Get bridging status from the Loan table
                    bool isBridging = loan.Bridging ?? false;

                    if (loan.Status == (int)Status.Rejected)
                    {
                        // Rejected loans
                        endorsedLoans.Add(new
                        {
                            loan.LoanNo,
                            MemberName = memberName,
                            LoanType = loanTypeName,
                            ApprovedAmount = loan.LoanAmt ?? 0,
                            ApplicationDate = loan.ApplicDate,
                            EndorsementDate = endorsement?.MeetingDate,
                            EndorsedBy = endorsement?.ChairSigned,
                            MinuteNo = endorsement?.MinuteNo,
                            Status = statusName,
                            IsDisbursed = false,
                            CanEdit = false,
                            CanDisburse = false,
                            CanViewDetails = true,
                            HasEndorsement = hasEndorsement,
                            IsBridging = isBridging
                        });
                    }
                    else if (loan.Status == (int)Status.Endorsed || hasEndorsement)
                    {
                        // Endorsed loans
                        endorsedLoans.Add(new
                        {
                            loan.LoanNo,
                            MemberName = memberName,
                            LoanType = loanTypeName,
                            ApprovedAmount = loan.LoanAmt ?? 0,
                            ApplicationDate = loan.ApplicDate,
                            EndorsementDate = endorsement?.MeetingDate,
                            EndorsedBy = endorsement?.ChairSigned,
                            MinuteNo = endorsement?.MinuteNo,
                            Status = endorsement?.Accepted == "1" ? "Endorsed" : "Pending Endorsement",
                            IsDisbursed = isDisbursed,
                            CanEdit = !isDisbursed && loan.Status == (int)Status.Endorsed && endorsement?.Accepted == "1",
                            CanDisburse = !isDisbursed && loan.Status == (int)Status.Endorsed,
                            CanViewDetails = true,
                            HasEndorsement = hasEndorsement,
                            IsBridging = isBridging
                        });
                    }
                    else if (loan.Status == (int)Status.Approved && !hasEndorsement)
                    {
                        // Pending endorsement (Approved without endorsement)
                        pendingEndorsement.Add(new
                        {
                            loan.LoanNo,
                            MemberName = memberName,
                            LoanType = loanTypeName,
                            ApprovedAmount = loan.LoanAmt ?? 0,
                            ApplicationDate = loan.ApplicDate,
                            Status = statusName,
                            IsDisbursed = false,
                            CanEdit = false,
                            CanDisburse = false,
                            CanViewDetails = true,
                            HasEndorsement = hasEndorsement,  // ✅ ADD THIS
                            IsBridging = isBridging            // ✅ ADD THIS
                        });
                    }
                }

                ViewBag.PendingCount = pendingEndorsement.Count;
                ViewBag.EndorsedCount = endorsedLoans.Count;
                ViewBag.CompanyCode = companyCode;

                var viewModel = new
                {
                    PendingLoans = pendingEndorsement,
                    EndorsedLoans = endorsedLoans
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending endorsement loans");
                TempData["ErrorMessage"] = "Error loading loans pending endorsement";
                return View(new { PendingLoans = new List<object>(), EndorsedLoans = new List<object>() });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Endorse(LoanEndorsementDTO endorsementDto)
        {
            try
            {
                _logger.LogInformation($"=== ENDORSE POST CALLED ===");
                _logger.LogInformation($"LoanNo: {endorsementDto.LoanNo}");
                _logger.LogInformation($"IsAccepted: {endorsementDto.IsAccepted}");
                _logger.LogInformation($"Remarks: {endorsementDto.Remarks}");

                // Validate rejection reason if not accepted
                if (!endorsementDto.IsAccepted)
                {
                    if (string.IsNullOrWhiteSpace(endorsementDto.Remarks))
                    {
                        TempData["ErrorMessage"] = "Please provide a reason for rejecting the endorsement.";
                        return RedirectToAction("Endorse", new { loanNo = endorsementDto.LoanNo });
                    }
                }
                else
                {
                    // Only validate deductions if accepted
                    foreach (var deduction in endorsementDto.Deductions)
                    {
                        _logger.LogInformation($"Deduction: {deduction.DeductionCode}, Amount: {deduction.Amount}, GL Account: {deduction.GlAccountNo}, IsPercentage: {deduction.IsPercentage}, PercentageValue: {deduction.PercentageValue}");
                    }

                    // Validate that all deductions with amount > 0 have GL accounts
                    var invalidDeductions = endorsementDto.Deductions
                        .Where(d => d.Amount > 0 && string.IsNullOrEmpty(d.GlAccountNo))
                        .ToList();

                    if (invalidDeductions.Any())
                    {
                        var invalidNames = string.Join(", ", invalidDeductions.Select(d => d.DeductionName));
                        TempData["ErrorMessage"] = $"Please select income accounts for: {invalidNames}";
                        return RedirectToAction("Endorse", new { loanNo = endorsementDto.LoanNo });
                    }
                }

                endorsementDto.CompanyCode = GetUserCompanyCode();
                endorsementDto.EndorsedBy = User.Identity?.Name ?? "SYSTEM";

                var endorsement = await _loanService.CreateEndorsementAsync(endorsementDto);

                if (endorsementDto.IsAccepted)
                {
                    TempData["SuccessMessage"] = $"✅ Endorsement {endorsement.MinuteNo} completed successfully! The loan is now endorsed and waiting for Finance Officer to disburse the loan.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Endorsement has been rejected. Loan {endorsementDto.LoanNo} has been marked as Rejected.";
                }

                return RedirectToAction("EndorsementDetails");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan endorsement");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Endorse", new { loanNo = endorsementDto.LoanNo });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Endorse(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingEndorsement");
                }

                if (loan.Status != (int)Status.Approved)
                {
                    TempData["ErrorMessage"] = $"Cannot endorse loan in status '{loan.Status}'. Loan must be Approved.";
                    return RedirectToAction("PendingEndorsement");
                }

                var existingEndorsement = await _loanService.GetEndorsementByLoanNoAsync(loanNo, companyCode);
                if (existingEndorsement != null)
                {
                    TempData["ErrorMessage"] = "Endorsement already exists for this loan";
                    return RedirectToAction("PendingEndorsement");
                }

                // Get the actual Loantype entity
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                var availableDeductions = await _loanService.GetAvailableDeductionsAsync(companyCode);

                var allGlAccounts = await _context.GlSetup
                    .Where(g => g.CompanyCode == companyCode && g.Status == true)
                    .OrderBy(g => g.AccNo)
                    .Select(g => new
                    {
                        AccountNo = g.AccNo,
                        AccountName = g.Glaccname,
                        AccountType = g.Glacctype,
                        DisplayText = $"{g.Glaccname}"
                    })
                    .ToListAsync();

                if (!allGlAccounts.Any())
                {
                    TempData["ErrorMessage"] = "No GL accounts found. Please set up GL accounts first.";
                    return RedirectToAction("PendingEndorsement");
                }

                string defaultSourceAccountNo = null;

                var defaultBank = await _context.Banks
                    .Where(b => b.CompanyCode == companyCode &&
                               b.IsActive == true &&
                               !string.IsNullOrEmpty(b.GlAccountNo))
                    .OrderBy(b => b.Id)
                    .FirstOrDefaultAsync();

                if (defaultBank != null)
                {
                    defaultSourceAccountNo = defaultBank.GlAccountNo;
                    _logger.LogInformation($"Auto-selected source bank: {defaultBank.BankName} with GL Account: {defaultBank.GlAccountNo}");
                }
                else
                {
                    var defaultGlAccount = await _context.GlSetup
                        .Where(g => g.CompanyCode == companyCode &&
                                   g.Status == true &&
                                   (g.Glacctype == "CASH" || g.Glacctype == "BANK" || g.GlAccMainGroup == "ASSET"))
                        .OrderBy(g => g.AccNo)
                        .FirstOrDefaultAsync();

                    if (defaultGlAccount != null)
                    {
                        defaultSourceAccountNo = defaultGlAccount.AccNo;
                        _logger.LogInformation($"Auto-selected source GL account: {defaultGlAccount.AccNo} - {defaultGlAccount.Glaccname}");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "No source account found. Please set up a bank with a GL account or a cash GL account.";
                        return RedirectToAction("PendingEndorsement");
                    }
                }

                // ============================================================
                // CALCULATE REGISTRATION FEE - Supports both fixed and percentage
                // ============================================================
                var grossAmount = loan.LoanAmt ?? 0;
                decimal registrationFeeAmount = 0;
                bool isPercentageFee = false;
                decimal percentageValue = 0;

                if (loanType != null)
                {
                    var processingFee = loanType.Processingfee ?? 0;

                    if (processingFee > 0)
                    {
                        // Determine if it's a percentage or fixed amount
                        // If processing fee < 1000 and > 1, treat as percentage (2 = 2%)
                        // If processing fee < 1, treat as decimal percentage (0.02 = 2%)
                        // If processing fee >= 1000, treat as fixed amount
                        if (processingFee < 1000 && processingFee > 0)
                        {
                            isPercentageFee = true;

                            if (processingFee < 1)
                            {
                                // e.g., 0.02 = 2%
                                percentageValue = processingFee * 100;
                                registrationFeeAmount = grossAmount * processingFee;
                            }
                            else
                            {
                                // e.g., 2 = 2%
                                percentageValue = processingFee;
                                registrationFeeAmount = (grossAmount * processingFee) / 100;
                            }
                        }
                        else
                        {
                            // Fixed amount
                            isPercentageFee = false;
                            registrationFeeAmount = processingFee;
                        }
                    }
                }

                var defaultDeductions = new List<LoanDeductionDTO>();

                // 1. Add Registration Fee from LoanType
                if (registrationFeeAmount > 0)
                {
                    defaultDeductions.Add(new LoanDeductionDTO
                    {
                        DeductionCode = "REG_FEE",
                        DeductionName = "Registration Fee",
                        GlAccountNo = "",
                        GlAccountName = "",
                        Amount = registrationFeeAmount,
                        Description = isPercentageFee
                            ? $"Registration fee: {percentageValue:F2}% of {grossAmount:C} from {loanType?.LoanType1 ?? loan.LoanCode}"
                            : $"Registration fee: {registrationFeeAmount:C} from {loanType?.LoanType1 ?? loan.LoanCode}",
                        IsMandatory = false,
                        IsPercentage = isPercentageFee,
                        PercentageValue = percentageValue
                    });
                }

                // 2. Add other available deductions
                foreach (var deduction in availableDeductions)
                {
                    defaultDeductions.Add(new LoanDeductionDTO
                    {
                        DeductionCode = deduction.DeductionCode,
                        DeductionName = deduction.DeductionName,
                        GlAccountNo = "",
                        GlAccountName = "",
                        Amount = 0,
                        Description = deduction.Description,
                        IsMandatory = false,
                        IsPercentage = deduction.IsPercentage,
                        PercentageValue = deduction.PercentageValue
                    });
                }

                var endorsementDto = new LoanEndorsementDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    EndorsementDate = DateTime.Now,
                    EndorsedBy = User.Identity?.Name ?? "SYSTEM",
                    Deductions = defaultDeductions,
                    Remarks = "",
                    SourceAccountNo = defaultSourceAccountNo,
                    IsAccepted = true
                };

                ViewBag.Loan = loan;
                ViewBag.GrossAmount = grossAmount;
                ViewBag.AllGlAccounts = allGlAccounts;
                ViewBag.RegistrationFee = registrationFeeAmount;
                ViewBag.RegistrationFeeIsPercentage = isPercentageFee;
                ViewBag.RegistrationFeePercentage = percentageValue;
                ViewBag.LoanType = loanType;

                return View(endorsementDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading endorsement form for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading endorsement: {ex.Message}";
                return RedirectToAction("PendingEndorsement");
            }
        }

        // GET: Edit Endorsement
        [HttpGet]
        public async Task<IActionResult> EditEndorsement(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // Check if loan exists
                var loan = await _loanService.GetLoanByNoForDisplayAsync(loanNo, companyCode);
                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingEndorsement");
                }

                // Check if endorsement exists
                var existingEndorsement = await _loanService.GetEndorsementByLoanNoAsync(loanNo, companyCode);
                if (existingEndorsement == null)
                {
                    TempData["ErrorMessage"] = "Endorsement not found for this loan";
                    return RedirectToAction("PendingEndorsement");
                }

                // Check if already disbursed
                var isDisbursed = await _context.Loanbal
                    .AnyAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                if (isDisbursed)
                {
                    TempData["ErrorMessage"] = "Cannot edit endorsement for a disbursed loan";
                    return RedirectToAction("PendingEndorsement");
                }

                // Get endorsement data for editing
                var endorsementDto = await _loanService.GetEndorsementForEditAsync(loanNo, companyCode);

                // Get GL accounts for dropdown
                var allGlAccounts = await _context.GlSetup
                    .Where(g => g.CompanyCode == companyCode && g.Status == true)
                    .OrderBy(g => g.AccNo)
                    .Select(g => new
                    {
                        AccountNo = g.AccNo,
                        AccountName = g.Glaccname,
                        AccountType = g.Glacctype,
                        DisplayText = $"{g.Glaccname}"
                    })
                    .ToListAsync();

                if (!allGlAccounts.Any())
                {
                    TempData["ErrorMessage"] = "No GL accounts found. Please set up GL accounts first.";
                    return RedirectToAction("PendingEndorsement");
                }

                // Get banks for source account dropdown
                var banks = await _context.Banks
                    .Where(b => b.CompanyCode == companyCode && b.IsActive == true)
                    .OrderBy(b => b.BankName)
                    .Select(b => new
                    {
                        BankId = b.Id,
                        BankName = b.BankName,
                        GlAccountNo = b.GlAccountNo
                    })
                    .ToListAsync();

                // Get loan type for registration fee
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                var grossAmount = loan.LoanAmt ?? 0;

                // ✅ Ensure endorsementDto has the correct GrossAmount
                if (endorsementDto.GrossAmount == 0 && grossAmount > 0)
                {
                    endorsementDto.GrossAmount = grossAmount;
                }

                ViewBag.Loan = loan;
                ViewBag.GrossAmount = loan.LoanAmt ?? 0;
                ViewBag.AllGlAccounts = allGlAccounts;
                ViewBag.Banks = banks;
                ViewBag.RegistrationFee = loanType?.Processingfee ?? 0;
                ViewBag.LoanType = loanType;
                ViewBag.IsEdit = true;

                return View(endorsementDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit endorsement form for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading endorsement: {ex.Message}";
                return RedirectToAction("PendingEndorsement");
            }
        }

        // POST: Edit Endorsement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEndorsement(LoanEndorsementDTO endorsementDto)
        {
            try
            {
                _logger.LogInformation($"=== EDIT ENDORSEMENT POST CALLED ===");
                _logger.LogInformation($"LoanNo: {endorsementDto.LoanNo}");
                _logger.LogInformation($"GrossAmount from DTO: {endorsementDto.GrossAmount}");

                endorsementDto.CompanyCode = GetUserCompanyCode();
                endorsementDto.EndorsedBy = User.Identity?.Name ?? "SYSTEM";

                // ============================================================
                // FIX: If GrossAmount is 0, get it from the loan
                // ============================================================
                if (endorsementDto.GrossAmount == 0)
                {
                    var loan = await _context.Loans
                        .FirstOrDefaultAsync(l => l.LoanNo == endorsementDto.LoanNo && l.CompanyCode == endorsementDto.CompanyCode);

                    if (loan != null && loan.LoanAmt > 0)
                    {
                        endorsementDto.GrossAmount = loan.LoanAmt.Value;
                        _logger.LogInformation($"GrossAmount set from loan: {endorsementDto.GrossAmount}");
                    }
                    else
                    {
                        // Try to get from ViewBag as fallback
                        var viewBagGross = ViewBag.GrossAmount as decimal? ?? 0;
                        if (viewBagGross > 0)
                        {
                            endorsementDto.GrossAmount = viewBagGross;
                            _logger.LogInformation($"GrossAmount set from ViewBag: {endorsementDto.GrossAmount}");
                        }
                    }
                }

                _logger.LogInformation($"Final GrossAmount: {endorsementDto.GrossAmount}");

                // Validate deductions
                if (endorsementDto.IsAccepted)
                {
                    var invalidDeductions = endorsementDto.Deductions
                        .Where(d => d.Amount > 0 && string.IsNullOrEmpty(d.GlAccountNo))
                        .ToList();

                    if (invalidDeductions.Any())
                    {
                        var invalidNames = string.Join(", ", invalidDeductions.Select(d => d.DeductionName));
                        TempData["ErrorMessage"] = $"Please select income accounts for: {invalidNames}";
                        return RedirectToAction("EditEndorsement", new { loanNo = endorsementDto.LoanNo });
                    }

                    var totalDeductions = endorsementDto.Deductions.Sum(d => d.Amount);
                    var netAmount = endorsementDto.GrossAmount - totalDeductions;

                    _logger.LogInformation($"Gross: {endorsementDto.GrossAmount}, Total Ded: {totalDeductions}, Net: {netAmount}");

                    if (netAmount < 0)
                    {
                        TempData["ErrorMessage"] = $"Total deductions ({totalDeductions:C}) cannot exceed gross amount ({endorsementDto.GrossAmount:C})";
                        return RedirectToAction("EditEndorsement", new { loanNo = endorsementDto.LoanNo });
                    }
                }

                // Update endorsement
                var updatedEndmain = await _loanService.UpdateEndorsementAsync(endorsementDto);

                TempData["SuccessMessage"] = $"Endorsement updated successfully! Minute No: {updatedEndmain.MinuteNo}";

                return RedirectToAction("PendingEndorsement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating endorsement for {endorsementDto.LoanNo}");
                TempData["ErrorMessage"] = $"Error updating endorsement: {ex.Message}";
                return RedirectToAction("EditEndorsement", new { loanNo = endorsementDto.LoanNo });
            }
        }

        // GET: Endorsement Details
        [HttpGet]
        public async Task<IActionResult> EndorsementDetails(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var details = await _loanService.GetEndorsementDetailsAsync(loanNo, companyCode);

                return View(details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading endorsement details for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading details: {ex.Message}";
                return RedirectToAction("PendingEndorsement");
            }
        }


        #endregion

        #region Loan Disbursement

        [HttpGet]
        [FinanceOfficerOnly]
        public async Task<IActionResult> PendingDisbursement()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation($"PendingDisbursement: UserId={userId}");

                // Check if OTP was validated in this session
                var otpValidated = HttpContext.Session.GetString($"OtpValidated_{userId}");
                var otpValidatedAt = HttpContext.Session.GetString($"OtpValidatedAt_{userId}");

                bool isValidated = false;
                if (otpValidated == "true" && !string.IsNullOrEmpty(otpValidatedAt))
                {
                    if (DateTime.TryParse(otpValidatedAt, out var validatedAt))
                    {
                        var timeSinceValidation = DateTime.UtcNow - validatedAt;
                        isValidated = timeSinceValidation.TotalMinutes <= 5;

                        _logger.LogInformation($"PendingDisbursement: Validation check - IsValidated: {isValidated}, TimeSince: {timeSinceValidation.TotalMinutes:F2} minutes");

                        if (!isValidated)
                        {
                            _logger.LogInformation($"PendingDisbursement: OTP validation expired for user {userId}");
                        }
                    }
                }

                if (!isValidated)
                {
                    // Clear invalid session
                    HttpContext.Session.Remove($"OtpValidated_{userId}");
                    HttpContext.Session.Remove($"OtpValidatedAt_{userId}");

                    _logger.LogInformation($"PendingDisbursement: OTP not validated, redirecting to verification for user {userId}");

                    TempData["ErrorMessage"] = "Please verify your identity with OTP to access pending disbursements.";
                    return RedirectToAction("OtpVerification", "Account", new { returnUrl = Url.Action("PendingDisbursement", "LoanMvc") });
                }

                var companyCode = GetUserCompanyCode();
                _logger.LogInformation($"PendingDisbursement: User {userId} accessing pending disbursements for company {companyCode}");

                // Get loans with Endorsed status (5)
                var endorsedLoans = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode && l.Status == (int)Status.Endorsed)
                    .OrderByDescending(l => l.AuditDateTime)
                    .ToListAsync();

                var pendingDisbursement = new List<dynamic>();

                foreach (var loan in endorsedLoans)
                {
                    // Check if already disbursed (has Loanbal record)
                    var existingDisbursement = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                    if (existingDisbursement != null)
                    {
                        continue; // Skip already disbursed loans
                    }

                    // Get endorsement record
                    var endorsement = await _context.Endmain
                        .FirstOrDefaultAsync(e => e.LoanNo == loan.LoanNo && e.CompanyCode == companyCode);

                    if (endorsement == null)
                    {
                        continue; // Skip if no endorsement found
                    }

                    // Get cheque record
                    var cheque = await _context.Cheques
                        .FirstOrDefaultAsync(c => c.LoanNo == loan.LoanNo && c.CompanyCode == companyCode);

                    // Get member details
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                    // Get loan type
                    var loanType = await _context.Loantypes
                        .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                    // Calculate values
                    decimal approvedAmount = endorsement.AmtApproved;
                    decimal netAmount = cheque?.AmountIssued ?? (cheque?.Amount ?? approvedAmount);
                    decimal totalDeductions = approvedAmount - netAmount;

                    // Get GL transactions for deductions
                    var glTransactions = await _context.Gltransactions
                        .Where(g => g.DocumentNo == cheque.Voucherno && g.Source == "LOAN_ENDORSEMENT")
                        .ToListAsync();

                    if (glTransactions.Any())
                    {
                        totalDeductions = glTransactions.Sum(g => g.Amount);
                        netAmount = approvedAmount - totalDeductions;
                    }

                    pendingDisbursement.Add(new
                    {
                        LoanNo = loan.LoanNo,
                        MemberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo,
                        LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                        GrossAmount = approvedAmount,
                        TotalDeductions = totalDeductions,
                        NetAmount = netAmount,
                        ApplicationDate = loan.ApplicDate,
                        MemberMobile = member?.PhoneNo ?? member?.MobileNo ?? "N/A"
                    });
                }

                ViewBag.Count = pendingDisbursement.Count;
                ViewBag.OtpValidated = true;
                return View(pendingDisbursement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending disbursement loans");
                TempData["ErrorMessage"] = $"Error loading loans: {ex.Message}";
                return View(new List<dynamic>());
            }
        }

        [HttpGet]
        [FinanceOfficerOnly]
        public async Task<IActionResult> Disburse(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                if (string.IsNullOrEmpty(companyCode))
                {
                    _logger.LogError("CompanyCode is null or empty");
                    TempData["ErrorMessage"] = "Company code not found. Please log in again.";
                    return RedirectToAction("PendingDisbursement");
                }

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingDisbursement");
                }

                // ✅ CHECK IF ALREADY DISBURSED FIRST (before status check)
                var existingLoanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                if (existingLoanbal != null)
                {
                    TempData["ErrorMessage"] = "This loan has already been disbursed";
                    return RedirectToAction("AllLoans");
                }

                // ✅ THEN CHECK STATUS
                if (loan.Status != (int)Status.Endorsed)
                {
                    // If status is Disbursed (6), it's already been disbursed
                    if (loan.Status == (int)Status.Disbursed)
                    {
                        TempData["ErrorMessage"] = "This loan has already been disbursed";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Loan cannot be disbursed in status '{loan.Status}'. Loan must be Endorsed.";
                    }
                    return RedirectToAction("AllLoans");
                }

                if (loan.Status != (int)Status.Endorsed)
                {
                    TempData["ErrorMessage"] = $"Loan cannot be disbursed in status '{loan.Status}'. Loan must be Endorsed.";
                    return RedirectToAction("AllLoans");
                }

                //// Check if already disbursed
                //var existingLoanbal = await _context.Loanbal
                //    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                //if (existingLoanbal != null)
                //{
                //    TempData["ErrorMessage"] = "This loan has already been disbursed";
                //    return RedirectToAction("AllLoans");
                //}

                // GET MEMBER DETAILS
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get endorsement record
                var endorsement = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                // Get cheque record from endorsement (contains AmountIssued = net amount after deductions)
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(l => l.LoanCode == loan.LoanCode && l.CompanyCode == companyCode);

                // Get GL Accounts for dropdown
                var glAccounts = await _context.GlSetup
                    .Where(g => g.CompanyCode == companyCode && g.Status == true)
                    .OrderBy(g => g.AccNo)
                    .Select(g => new
                    {
                        AccountNo = g.AccNo,
                        AccountName = g.Glaccname,
                        DisplayText = $"{g.Glaccname}"
                    })
                    .ToListAsync();

                // Get Banks for dropdown
                var banks = await _context.Banks
                    .Where(b => b.CompanyCode == companyCode && b.IsActive == true)
                    .OrderBy(b => b.BankName)
                    .Select(b => new
                    {
                        BankId = b.Id,
                        BankCode = b.BankCode,
                        BankName = b.BankName,
                        AccountNumber = b.AccountNumber,
                        AccountName = b.AccountName,
                        Branch = b.Branch,
                        DisplayText = $"{b.BankName}"
                    })
                    .ToListAsync();

                // CORRECT: Net amount to disburse is AmountIssued from Cheque (after deductions)
                decimal netAmountToDisburse = cheque?.AmountIssued ?? endorsement?.AmtApproved ?? loan.LoanAmt ?? 0;

                // Approved amount from endorsement (before deductions)
                decimal approvedAmount = endorsement?.AmtApproved ?? loan.LoanAmt ?? 0;

                // Calculate total deductions
                decimal totalDeductions = approvedAmount - netAmountToDisburse;

                ViewBag.CashGlAccounts = glAccounts;
                ViewBag.Banks = banks;
                ViewBag.Loan = loan;
                ViewBag.Member = member;
                ViewBag.LoanType = loanType;
                ViewBag.Endorsement = endorsement;
                ViewBag.Cheque = cheque;
                ViewBag.ApprovedAmount = approvedAmount;
                ViewBag.NetAmount = netAmountToDisburse;
                ViewBag.TotalDeductions = totalDeductions;

                var disbursementDto = new LoanDisbursementDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    DisbursementDate = DateTime.Now,
                    DisbursedBy = User.Identity?.Name ?? "SYSTEM",
                    AuthorizedBy = User.Identity?.Name ?? "SYSTEM",
                    DisbursedAmount = netAmountToDisburse,
                    ProcessingFee = 0,
                    InsuranceFee = 0,
                    LegalFees = 0,
                    OtherFees = 0,
                    MobileNo = member?.PhoneNo ?? member?.MobileNo ?? ""
                };

                return View(disbursementDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading disbursement form for {loanNo}");
                TempData["ErrorMessage"] = $"Error loading disbursement: {ex.Message}";
                return RedirectToAction("PendingDisbursement");
            }
        }

        [HttpPost]
        [FinanceOfficerOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disburse(LoanDisbursementDTO disbursementDto)
        {
            try
            {
                _logger.LogInformation($"=== DISBURSE POST CALLED ===");
                _logger.LogInformation($"LoanNo: {disbursementDto.LoanNo}");

                if (string.IsNullOrEmpty(disbursementDto.DisbursementMethod))
                {
                    TempData["ErrorMessage"] = "Please select a disbursement method.";
                    return RedirectToAction("Disburse", new { loanNo = disbursementDto.LoanNo });
                }

                disbursementDto.CompanyCode = GetUserCompanyCode();
                disbursementDto.DisbursedBy = User.Identity?.Name ?? "SYSTEM";
                disbursementDto.AuthorizedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanService.DisburseLoanAsync(disbursementDto);

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == result.MemberNo && m.CompanyCode == disbursementDto.CompanyCode);

                string memberFullName = member != null
                    ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim()
                    : result.MemberNo;

                // ✅ CHECK B2C STATUS FROM ApiTransaction TABLE (NOT from result)
                // Use _appDbContext to query the B2C transaction records
                var b2cTransaction = await _appDbContext.ApiTransactions
                    .FirstOrDefaultAsync(t => t.LoanNo == disbursementDto.LoanNo && t.CompanyCode == disbursementDto.CompanyCode);

                if (b2cTransaction != null)
                {
                    // Get the corresponding transaction detail
                    var transactionDetail = await _appDbContext.Transaction_detail
                        .FirstOrDefaultAsync(td => td.ConversationId == b2cTransaction.ConversationId);

                    if (b2cTransaction.StatusCode == 0) // Success
                    {
                        TempData["SuccessMessage"] = $"✅ Loan disbursed successfully to {memberFullName}. Net Amount: KES {result.Amount:N0}. " +
                            $"M-Pesa payment of KES {b2cTransaction.Amount:N0} sent to {b2cTransaction.Recipient} successfully.\n" +
                            $"Transaction ID: {b2cTransaction.ConversationId}";
                    }
                    else if (b2cTransaction.StatusCode == 1) // Pending
                    {
                        TempData["WarningMessage"] = $"⚠️ Loan disbursed successfully to {memberFullName}. Net Amount: KES {result.Amount:N0}. " +
                            $"M-Pesa payment is PENDING. Check M-Pesa statement for confirmation.\n" +
                            $"Transaction ID: {b2cTransaction.ConversationId}";
                    }
                    else if (b2cTransaction.StatusCode == 2) // Failed
                    {
                        TempData["WarningMessage"] = $"⚠️ Loan disbursed successfully to {memberFullName}. Net Amount: KES {result.Amount:N0}. " +
                            $"However, M-Pesa payment FAILED: {b2cTransaction.ResultDescription ?? b2cTransaction.StatusCode.ToString()}\n" +
                            $"Transaction ID: {b2cTransaction.ConversationId}\n" +
                            $"Please process manual payment to the member via bank or cash.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"Loan disbursed successfully to {memberFullName}. Net Amount: KES {result.Amount:N0}";
                    }
                }
                else
                {
                    // No B2C transaction found - B2C was not enabled or phone missing
                    TempData["SuccessMessage"] = $"Loan disbursed successfully to {memberFullName}. Net Amount: KES {result.Amount:N0}";
                }

                return RedirectToAction("AllLoans");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Database error disbursing loan: {ex.Message}");

                string errorMessage = "Database error: ";
                if (ex.InnerException != null)
                {
                    errorMessage += ex.InnerException.Message;
                }
                else
                {
                    errorMessage += ex.Message;
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("Disburse", new { loanNo = disbursementDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disbursing loan: {ex.Message}");

                // Check if there's a B2C transaction even if exception occurred
                try
                {
                    var failedB2C = await _appDbContext.ApiTransactions
                        .FirstOrDefaultAsync(t => t.LoanNo == disbursementDto.LoanNo && t.CompanyCode == disbursementDto.CompanyCode);

                    if (failedB2C != null)
                    {
                        TempData["ErrorMessage"] = $"❌ B2C Payment Error: {failedB2C.ResultDescription ?? ex.Message}\n" +
                            $"Transaction ID: {failedB2C.ConversationId}\n" +
                            $"Please check the transaction status manually.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Error disbursing loan: {ex.Message}";
                    }
                }
                catch
                {
                    TempData["ErrorMessage"] = $"Error disbursing loan: {ex.Message}";
                }

                return RedirectToAction("Disburse", new { loanNo = disbursementDto.LoanNo });
            }
        }

        [HttpGet]
        [FinanceOfficerOnly]
        public async Task<IActionResult> PrintDisbursementReceipt(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // Get loan details
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                if (loan == null)
                {
                    return NotFound();
                }

                // Get member details
                var member = await _contributionService.GetMemberByMemberNoAsync(loan.MemberNo);

                // Get endorsement record
                var endmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                // Get cheque record
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get loan balance for monthly installment
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                // Get bank details if bank transfer
                string bankName = null;
                if (!string.IsNullOrEmpty(cheque?.ContraAcc))
                {
                    var bank = await _context.Banks
                        .FirstOrDefaultAsync(b => b.GlAccountNo == cheque.ContraAcc && b.CompanyCode == companyCode);
                    bankName = bank?.BankName;
                }

                // Calculate values
                decimal approvedAmount = endmain?.AmtApproved ?? loan.LoanAmt ?? 0;
                decimal netAmount = cheque?.AmountIssued ?? approvedAmount;
                decimal totalDeductions = approvedAmount - netAmount;

                // Get GL transactions for deductions breakdown
                var glTransactions = await _context.Gltransactions
                    .Where(g => g.DocumentNo == cheque.Voucherno && g.Source == "LOAN_ENDORSEMENT")
                    .ToListAsync();

                if (glTransactions.Any())
                {
                    totalDeductions = glTransactions.Sum(g => g.Amount);
                    netAmount = approvedAmount - totalDeductions;
                }

                // Determine disbursement method from cheque
                string disbursementMethod = "BANK_TRANSFER";
                if (cheque?.Paymethod != null)
                {
                    disbursementMethod = cheque.Paymethod switch
                    {
                        "MPESA" => "M-Pesa",
                        "CASH" => "Cash",
                        "CHEQUE" => "Cheque",
                        "BANK" => "Bank Transfer",
                        _ => cheque.Paymethod
                    };
                }

                // Get disbursed by from cheque or loan
                string disbursedBy = cheque?.UserName ?? loan.UserName ?? User.Identity?.Name ?? "SYSTEM";

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                // Build address - FIXED: removed null propagating operator
                string companyAddress = "P.O. Box 12345 - 00100, Nairobi, Kenya";
                if (company != null)
                {
                    var addressParts = new List<string>();
                    if (!string.IsNullOrEmpty(company.Address)) addressParts.Add(company.Address);
                    if (!string.IsNullOrEmpty(company.County)) addressParts.Add(company.County);
                    if (addressParts.Any()) companyAddress = string.Join(", ", addressParts);
                }

                var receiptModel = new DisbursementReceiptViewModel
                {
                    ReceiptNo = cheque?.Voucherno ?? $"DISB-{loanNo}",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    MemberPhone = member?.PhoneNo ?? "N/A",
                    MemberIdNo = member?.Idno ?? "N/A",
                    DisbursementDate = cheque?.DateIssued ?? DateTime.Now,
                    ApprovedAmount = approvedAmount,
                    TotalDeductions = totalDeductions,
                    NetAmount = netAmount,
                    DisbursementMethod = disbursementMethod,
                    ReferenceNo = cheque?.ChequeNo,
                    Remarks = cheque?.Remarks ?? "",
                    BlockchainTxId = cheque?.BlockchainTxId ?? loan.BlockchainTxId,
                    CompanyCode = companyCode,
                    CompanyName = company?.CompanyName ?? "SACCO Blockchain System",
                    CompanyAddress = companyAddress,
                    CompanyPhone = company?.Telephone ?? "+254 700 000 000",
                    CompanyEmail = company?.Email ?? "info@sacco.co.ke",
                    DisbursedBy = disbursedBy,
                    ChequeNo = cheque?.ChequeNo,
                    BankName = bankName,
                    InterestRate = loan.Interest,
                    RepaymentPeriod = loan.RepayPeriod,
                    MonthlyInstallment = loanbal?.RepayRate,
                    PrintedAt = DateTime.Now
                };

                return View(receiptModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing disbursement receipt for loan {loanNo}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("AllLoans");
            }
        }

        public class FinanceOfficerOnlyAttribute : AuthorizeAttribute
        {
            public FinanceOfficerOnlyAttribute()
            {
                Roles = "Finance Officer, Super Admin, Admin";
            }
        }

        // Custom attribute for OTP validation check
        public class OtpRequiredAttribute : AuthorizeAttribute
        {
            public OtpRequiredAttribute()
            {
                // This attribute can be used on actions that require OTP
            }
        }

        #endregion


        #region Loan Repayments

        [HttpGet]
        public async Task<IActionResult> Repay(string loanNo = null)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                ViewBag.CompanyCode = companyCode;

                _logger.LogInformation($"Repay page loading - CompanyCode: {companyCode}");

                // Load active GL accounts from GLSETUP
                var glAccounts = await _context.GlSetup
                    .Where(g => g.CompanyCode == companyCode && g.Status == true)
                    .OrderBy(g => g.AccNo)
                    .Select(g => new
                    {
                        AccNo = g.AccNo,
                        Glaccname = g.Glaccname,
                        Glacctype = g.Glacctype ?? "General",
                        GlAccMainGroup = g.GlAccMainGroup,
                        DisplayText = $"{g.Glaccname}"
                    })
                    .ToListAsync();

                _logger.LogInformation($"GL Accounts found: {glAccounts.Count}");
                ViewBag.GlAccounts = glAccounts;

                var repaymentDto = new LoanRepaymentDTO
                {
                    CompanyCode = companyCode,
                    PaymentDate = DateTime.Now,
                    ReceivedBy = User.Identity?.Name ?? "SYSTEM"
                };

                if (!string.IsNullOrEmpty(loanNo))
                {
                    var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                    if (loan != null && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed))
                    {
                        var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                        var totalOutstanding = schedule.Where(s => s.Status != "Paid").Sum(s => s.OutstandingAmount);
                        var nextInstallment = schedule.FirstOrDefault(s => s.Status == "Pending" || s.Status == "Overdue");

                        ViewBag.Loan = loan;
                        ViewBag.Schedule = schedule;
                        ViewBag.TotalOutstanding = totalOutstanding;
                        ViewBag.NextInstallment = nextInstallment;
                        ViewBag.PreSelectedLoanNo = loanNo;

                        repaymentDto.LoanNo = loanNo;
                        repaymentDto.MemberNo = loan.MemberNo;
                    }
                }

                return View(repaymentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repayment page");
                TempData["ErrorMessage"] = $"Error loading repayment page: {ex.Message}";
                return View(new LoanRepaymentDTO
                {
                    CompanyCode = GetUserCompanyCode(),
                    PaymentDate = DateTime.Now,
                    ReceivedBy = User.Identity?.Name ?? "SYSTEM"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveLoans(string memberNo = null, string loanNo = null, string companyCode = null)
        {
            try
            {
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = GetUserCompanyCode();
                }

                List<object> activeLoans = new List<object>();
                string memberName = null;
                string memberPhone = null;
                string memberEmail = null;
                string actualMemberNo = null;
                string memberIdNo = null;

                // CASE 1: Search by Loan Number
                if (!string.IsNullOrEmpty(loanNo))
                {
                    var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                    // Loans that can be repaid: Disbursed, Endorsed, Approved
                    bool canRepay = loan != null && (
                        loan.Status == (int)Status.Disbursed ||
                        loan.Status == (int)Status.Endorsed ||
                        loan.Status == (int)Status.Approved ||
                        (loan.Status == (int)Status.Submitted && loan.Guaranteed != "0"));

                    if (loan != null && canRepay)
                    {
                        actualMemberNo = loan.MemberNo;
                        var member = await _contributionService.GetMemberByMemberNoAsync(loan.MemberNo);
                        if (member != null)
                        {
                            memberName = $"{member.Surname} {member.OtherNames}".Trim();
                            memberPhone = member.PhoneNo;
                            memberEmail = member.Email;
                            memberIdNo = member.Idno;
                        }

                        var loanTypeName = "Unknown";
                        var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                        if (loanType != null)
                        {
                            loanTypeName = loanType.LoanType ?? loanType.LoanCode ?? "Unknown";
                        }

                        // GET CURRENT SCHEDULE
                        var currentSchedule = await _context.LoanSchedules
                            .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
                            .OrderBy(s => s.InstallmentNo)
                            .FirstOrDefaultAsync();

                        var loanbal = await _loanService.GetLoanBalanceAsync(loanNo);

                        decimal outstandingPrincipal = 0;
                        decimal outstandingInterest = 0;
                        decimal existingPenalty = 0;
                        decimal totalOutstanding = 0;
                        decimal nextInstallmentAmount = 0;
                        DateTime? dueDate = null;
                        int daysOverdue = 0;

                        if (currentSchedule != null)
                        {
                            outstandingPrincipal = currentSchedule.OutstandingPrincipal;
                            outstandingInterest = currentSchedule.OutstandingInterest;
                            existingPenalty = currentSchedule.PenaltyAmount;
                            totalOutstanding = currentSchedule.OutstandingTotal + currentSchedule.PenaltyAmount;
                            nextInstallmentAmount = currentSchedule.TotalInstallment;
                            dueDate = currentSchedule.DueDate;
                            daysOverdue = currentSchedule.DaysOverdue;
                        }
                        else if (loanbal != null)
                        {
                            outstandingPrincipal = loanbal.Balance;
                            outstandingInterest = loanbal.IntrOwed;
                            existingPenalty = loanbal.Penalty;
                            totalOutstanding = loanbal.Balance + loanbal.IntrOwed + loanbal.Penalty;
                            nextInstallmentAmount = loanbal.RepayRate;
                            dueDate = loanbal.Duedate;
                        }
                        else
                        {
                            outstandingPrincipal = loan.LoanAmt ?? 0;
                            outstandingInterest = 0;
                            totalOutstanding = outstandingPrincipal;
                        }

                        // ============================================================
                        // RECALCULATE DAYS OVERDUE BASED ON ACTUAL DATE
                        // ============================================================
                        int calculatedDaysOverdue = 0;
                        if (dueDate.HasValue)
                        {
                            calculatedDaysOverdue = (DateTime.Now.Date - dueDate.Value.Date).Days;
                            if (calculatedDaysOverdue < 0) calculatedDaysOverdue = 0;
                        }

                        // ============================================================
                        // CALCULATE PENALTY FOR OVERDUE LOAN
                        // ============================================================
                        decimal calculatedPenalty = existingPenalty;
                        string penaltyCalculationDetails = "";
                        string penaltyMode = "";
                        decimal penaltyValue = 0;
                        int gracePeriodDays = loanType?.GracePeriod ?? 0;

                        if (dueDate.HasValue && calculatedDaysOverdue > gracePeriodDays && loanType != null && loanType.Penalty == 1)
                        {
                            // Get penalty configuration
                            var penaltyConfig = await _context.Penalties
                                .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == companyCode && p.Penalty == 1);

                            if (penaltyConfig != null)
                            {
                                penaltyMode = penaltyConfig.Mode ?? "Percentage";
                                string penaltyRateType = penaltyConfig.Rate ?? "Monthly";
                                penaltyValue = penaltyConfig.Value;
                                short penaltyChargeItem = penaltyConfig.ChargeItem;

                                int overdueAfterGrace = calculatedDaysOverdue - gracePeriodDays;

                                // Calculate number of penalty periods based on Rate type
                                int numberOfPeriods = 1;
                                switch (penaltyRateType?.ToLower())
                                {
                                    case "daily":
                                        numberOfPeriods = overdueAfterGrace;
                                        break;
                                    case "weekly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 7.0);
                                        break;
                                    case "monthly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 30.0);
                                        break;
                                    case "yearly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 365.0);
                                        break;
                                    default:
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 30.0);
                                        break;
                                }

                                if (numberOfPeriods < 1) numberOfPeriods = 1;

                                // Determine base amount for penalty based on ChargeItem
                                decimal penaltyBaseAmount = 0;
                                switch (penaltyChargeItem)
                                {
                                    case 0: // Principal Only
                                        penaltyBaseAmount = outstandingPrincipal;
                                        break;
                                    case 1: // Interest Only
                                        penaltyBaseAmount = outstandingInterest;
                                        break;
                                    case 2: // Both Principal & Interest
                                    default:
                                        penaltyBaseAmount = outstandingPrincipal + outstandingInterest;
                                        break;
                                }

                                if (penaltyMode.ToLower() == "percentage")
                                {
                                    // PERCENTAGE MODE: Value is percentage rate
                                    calculatedPenalty = penaltyBaseAmount * (penaltyValue / 100) * numberOfPeriods;
                                    penaltyCalculationDetails = $"{penaltyValue}% × {numberOfPeriods} period(s) on KES {penaltyBaseAmount:N0}";
                                }
                                else if (penaltyMode.ToLower() == "fixed")
                                {
                                    // FIXED AMOUNT MODE: Value is fixed amount per period
                                    calculatedPenalty = penaltyValue * numberOfPeriods;
                                    penaltyCalculationDetails = $"KES {penaltyValue:N0} × {numberOfPeriods} period(s)";

                                    // Cap penalty at 50% of base amount
                                    decimal maxPenalty = penaltyBaseAmount * 0.5m;
                                    if (calculatedPenalty > maxPenalty)
                                    {
                                        calculatedPenalty = maxPenalty;
                                        penaltyCalculationDetails += " (capped at 50%)";
                                    }
                                }

                                _logger.LogInformation($"Penalty calculated for loan {loanNo}: {calculatedPenalty:C} - {penaltyCalculationDetails}");
                            }
                        }

                        // Calculate totals with penalty
                        decimal totalWithPenalty = outstandingPrincipal + outstandingInterest + calculatedPenalty;
                        decimal currentInstallmentDue = (currentSchedule?.OutstandingTotal ?? 0) + calculatedPenalty;

                        activeLoans.Add(new
                        {
                            loanNo = loan.LoanNo,
                            loanType = loanTypeName,
                            repayMethod = loan.RepayMethod ?? loanType?.RepayMethod ?? "AMT",
                            interestRate = loan.Interest ?? 0,
                            outstandingPrincipal = outstandingPrincipal,
                            outstandingInterest = outstandingInterest,
                            outstandingPenalty = calculatedPenalty,
                            totalOutstanding = totalWithPenalty,
                            nextDueDate = dueDate,
                            nextInstallmentAmount = nextInstallmentAmount,
                            dueDate = dueDate,
                            daysSinceLastPayment = calculatedDaysOverdue,
                            disbursementDate = loan.AuditDateTime ?? loan.ApplicDate,
                            installmentNo = currentSchedule?.InstallmentNo ?? 1,
                            totalPrincipalBalance = loanbal?.Balance ?? loan.LoanAmt ?? 0,
                            totalInterestBalance = loanbal?.IntrOwed ?? 0,
                            currentInstallmentDue = currentInstallmentDue,
                            isOverdue = calculatedDaysOverdue > 0,
                            gracePeriodDays = gracePeriodDays,
                            penaltyCalculationDetails = penaltyCalculationDetails,
                            penaltyMode = penaltyMode,
                            penaltyValue = penaltyValue
                        });
                    }

                    return Json(new
                    {
                        success = true,
                        memberNo = actualMemberNo,
                        memberName = memberName ?? "N/A",
                        idNo = memberIdNo ?? "N/A",
                        phone = memberPhone ?? "N/A",
                        email = memberEmail ?? "N/A",
                        loans = activeLoans
                    });
                }
                // CASE 2: Search by Member Number
                else if (!string.IsNullOrEmpty(memberNo))
                {
                    actualMemberNo = memberNo;
                    var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                    if (member != null)
                    {
                        memberName = $"{member.Surname} {member.OtherNames}".Trim();
                        memberPhone = member.PhoneNo;
                        memberEmail = member.Email;
                        memberIdNo = member.Idno;
                    }

                    // Get ALL loans for the member
                    var allLoans = await _context.Loans
                        .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                        .ToListAsync();

                    // Filter loans that can be repaid
                    var activeLoansList = allLoans
                        .Where(l => l.Status == (int)Status.Disbursed ||
                                   l.Status == (int)Status.Endorsed ||
                                   l.Status == (int)Status.Approved ||
                                   (l.Status == (int)Status.Submitted && l.Guaranteed != "0"))
                        .ToList();

                    foreach (var loan in activeLoansList)
                    {
                        var loanTypeName = "Unknown";
                        var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);
                        if (loanType != null)
                        {
                            loanTypeName = loanType.LoanType ?? loanType.LoanCode ?? "Unknown";
                        }

                        // GET CURRENT SCHEDULE
                        var currentSchedule = await _context.LoanSchedules
                            .Where(s => s.LoanNo == loan.LoanNo && s.Status != "Paid")
                            .OrderBy(s => s.InstallmentNo)
                            .FirstOrDefaultAsync();

                        var loanbal = await _loanService.GetLoanBalanceAsync(loan.LoanNo);

                        decimal outstandingPrincipal = 0;
                        decimal outstandingInterest = 0;
                        decimal existingPenalty = 0;
                        decimal totalOutstanding = 0;
                        decimal nextInstallmentAmount = 0;
                        DateTime? dueDate = null;
                        int daysOverdue = 0;

                        if (currentSchedule != null)
                        {
                            outstandingPrincipal = currentSchedule.OutstandingPrincipal;
                            outstandingInterest = currentSchedule.OutstandingInterest;
                            existingPenalty = currentSchedule.PenaltyAmount;
                            totalOutstanding = currentSchedule.OutstandingTotal + currentSchedule.PenaltyAmount;
                            nextInstallmentAmount = currentSchedule.TotalInstallment;
                            dueDate = currentSchedule.DueDate;
                            daysOverdue = currentSchedule.DaysOverdue;
                        }
                        else if (loanbal != null)
                        {
                            outstandingPrincipal = loanbal.Balance;
                            outstandingInterest = loanbal.IntrOwed;
                            existingPenalty = loanbal.Penalty;
                            totalOutstanding = loanbal.Balance + loanbal.IntrOwed + loanbal.Penalty;
                            nextInstallmentAmount = loanbal.RepayRate;
                            dueDate = loanbal.Duedate;
                        }
                        else
                        {
                            outstandingPrincipal = loan.LoanAmt ?? 0;
                            outstandingInterest = 0;
                            totalOutstanding = outstandingPrincipal;
                        }

                        // ============================================================
                        // RECALCULATE DAYS OVERDUE BASED ON ACTUAL DATE
                        // ============================================================
                        int calculatedDaysOverdue = 0;
                        if (dueDate.HasValue)
                        {
                            calculatedDaysOverdue = (DateTime.Now.Date - dueDate.Value.Date).Days;
                            if (calculatedDaysOverdue < 0) calculatedDaysOverdue = 0;
                        }

                        // ============================================================
                        // CALCULATE PENALTY FOR OVERDUE LOAN
                        // ============================================================
                        decimal calculatedPenalty = existingPenalty;
                        string penaltyCalculationDetails = "";
                        string penaltyMode = "";
                        decimal penaltyValue = 0;
                        int gracePeriodDays = loanType?.GracePeriod ?? 0;

                        //if (dueDate.HasValue && calculatedDaysOverdue > gracePeriodDays && loanType != null && loanType.Penalty)
                        if (dueDate.HasValue && calculatedDaysOverdue > gracePeriodDays && loanType != null && loanType.Penalty == 1)
                        {
                            var penaltyConfig = await _context.Penalties
                                .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == companyCode && p.Penalty == 1);

                            if (penaltyConfig != null)
                            {
                                penaltyMode = penaltyConfig.Mode ?? "Percentage";
                                string penaltyRateType = penaltyConfig.Rate ?? "Monthly";
                                penaltyValue = penaltyConfig.Value;
                                short penaltyChargeItem = penaltyConfig.ChargeItem;

                                int overdueAfterGrace = calculatedDaysOverdue - gracePeriodDays;

                                int numberOfPeriods = 1;
                                switch (penaltyRateType?.ToLower())
                                {
                                    case "daily":
                                        numberOfPeriods = overdueAfterGrace;
                                        break;
                                    case "weekly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 7.0);
                                        break;
                                    case "monthly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 30.0);
                                        break;
                                    case "yearly":
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 365.0);
                                        break;
                                    default:
                                        numberOfPeriods = (int)Math.Ceiling(overdueAfterGrace / 30.0);
                                        break;
                                }

                                if (numberOfPeriods < 1) numberOfPeriods = 1;

                                decimal penaltyBaseAmount = 0;
                                switch (penaltyChargeItem)
                                {
                                    case 0:
                                        penaltyBaseAmount = outstandingPrincipal;
                                        break;
                                    case 1:
                                        penaltyBaseAmount = outstandingInterest;
                                        break;
                                    default:
                                        penaltyBaseAmount = outstandingPrincipal + outstandingInterest;
                                        break;
                                }

                                if (penaltyMode.ToLower() == "percentage")
                                {
                                    calculatedPenalty = penaltyBaseAmount * (penaltyValue / 100) * numberOfPeriods;
                                    penaltyCalculationDetails = $"{penaltyValue}% × {numberOfPeriods} period(s) on KES {penaltyBaseAmount:N0}";
                                }
                                else if (penaltyMode.ToLower() == "fixed")
                                {
                                    calculatedPenalty = penaltyValue * numberOfPeriods;
                                    penaltyCalculationDetails = $"KES {penaltyValue:N0} × {numberOfPeriods} period(s)";

                                    decimal maxPenalty = penaltyBaseAmount * 0.5m;
                                    if (calculatedPenalty > maxPenalty)
                                    {
                                        calculatedPenalty = maxPenalty;
                                        penaltyCalculationDetails += " (capped at 50%)";
                                    }
                                }
                            }
                        }

                        decimal totalWithPenalty = outstandingPrincipal + outstandingInterest + calculatedPenalty;
                        decimal currentInstallmentDue = (currentSchedule?.OutstandingTotal ?? 0) + calculatedPenalty;

                        activeLoans.Add(new
                        {
                            loanNo = loan.LoanNo,
                            loanType = loanTypeName,
                            repayMethod = loan.RepayMethod ?? loanType?.RepayMethod ?? "AMT",
                            interestRate = loan.Interest ?? 0,
                            outstandingPrincipal = outstandingPrincipal,
                            outstandingInterest = outstandingInterest,
                            outstandingPenalty = calculatedPenalty,
                            totalOutstanding = totalWithPenalty,
                            nextDueDate = dueDate,
                            nextInstallmentAmount = nextInstallmentAmount,
                            dueDate = dueDate,
                            daysSinceLastPayment = calculatedDaysOverdue,
                            disbursementDate = loan.AuditDateTime ?? loan.ApplicDate,
                            installmentNo = currentSchedule?.InstallmentNo ?? 1,
                            totalPrincipalBalance = loanbal?.Balance ?? loan.LoanAmt ?? 0,
                            totalInterestBalance = loanbal?.IntrOwed ?? 0,
                            currentInstallmentDue = currentInstallmentDue,
                            isOverdue = calculatedDaysOverdue > 0,
                            gracePeriodDays = gracePeriodDays,
                            penaltyCalculationDetails = penaltyCalculationDetails,
                            penaltyMode = penaltyMode,
                            penaltyValue = penaltyValue
                        });
                    }

                    return Json(new
                    {
                        success = true,
                        memberNo = actualMemberNo,
                        memberName = memberName ?? "N/A",
                        idNo = memberIdNo ?? "N/A",
                        phone = memberPhone ?? "N/A",
                        email = memberEmail ?? "N/A",
                        loans = activeLoans
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please provide either a loan number or member number to search",
                        loans = new List<object>()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active loans");
                return Json(new { success = false, message = ex.Message, loans = new List<object>() });
            }
        }


        [HttpGet]
        public async Task<IActionResult> CalculateRepayment(string loanNo, decimal amount, DateTime paymentDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Loan not found" });
                }

                // GET CURRENT SCHEDULE - first unpaid installment
                var currentSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                if (currentSchedule == null)
                {
                    return Json(new { success = false, message = "No active installment found for this loan" });
                }

                // Get overall loan balance
                var loanbal = await _loanService.GetLoanBalanceAsync(loanNo);
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);

                // ============================================================
                // GET PENALTY CONFIGURATION FROM PENALTY TABLE
                // ============================================================
                bool attractsPenalty = false;
                string penaltyMode = "Percentage";
                string penaltyRateType = "Monthly";
                decimal penaltyValue = 0;
                short penaltyChargeItem = 0;

                if (loanType != null && loanType.Penalty == 1)
                {
                    var penaltyConfig = await _context.Penalties
                        .FirstOrDefaultAsync(p => p.LoanCode == loan.LoanCode && p.CompanyCode == companyCode && p.Penalty == 1);

                    if (penaltyConfig != null)
                    {
                        attractsPenalty = true;
                        penaltyMode = !string.IsNullOrEmpty(penaltyConfig.Mode) ? penaltyConfig.Mode : "Percentage";
                        penaltyRateType = !string.IsNullOrEmpty(penaltyConfig.Rate) ? penaltyConfig.Rate : "Monthly";
                        penaltyValue = penaltyConfig.Value;
                        penaltyChargeItem = penaltyConfig.ChargeItem;
                    }
                }

                int gracePeriodDays = loanType?.GracePeriod ?? 0;

                // ============================================================
                // CALCULATE PENALTY BASED ON PENALTY TABLE CONFIGURATION
                // ============================================================
                decimal penaltyAmount = 0;
                int daysOverdue = 0;
                int numberOfPeriods = 1;
                string penaltyCalculationDetails = "";

                if (attractsPenalty && paymentDate > currentSchedule.DueDate)
                {
                    daysOverdue = (paymentDate - currentSchedule.DueDate).Days;

                    if (daysOverdue > gracePeriodDays)
                    {
                        int overdueDaysAfterGrace = daysOverdue - gracePeriodDays;

                        // Calculate number of penalty periods based on Rate type
                        switch (penaltyRateType?.ToLower())
                        {
                            case "daily":
                                numberOfPeriods = overdueDaysAfterGrace;
                                break;
                            case "weekly":
                                numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 7.0);
                                break;
                            case "monthly":
                                numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);
                                break;
                            case "yearly":
                                numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 365.0);
                                break;
                            default:
                                numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);
                                break;
                        }

                        // Determine what amount to charge penalty on (ChargeItem)
                        decimal penaltyBaseAmount = 0;
                        switch (penaltyChargeItem)
                        {
                            case 0: // Principal Only
                                penaltyBaseAmount = currentSchedule.OutstandingPrincipal;
                                break;
                            case 1: // Interest Only
                                penaltyBaseAmount = currentSchedule.OutstandingInterest;
                                break;
                            case 2: // Both Principal & Interest
                            default:
                                penaltyBaseAmount = currentSchedule.OutstandingTotal;
                                break;
                        }

                        if (penaltyMode?.ToLower() == "percentage")
                        {
                            // PERCENTAGE MODE
                            penaltyAmount = penaltyBaseAmount * (penaltyValue / 100) * numberOfPeriods;
                            penaltyCalculationDetails = $"{penaltyValue}% of KES {penaltyBaseAmount:N0} × {numberOfPeriods} period(s)";
                        }
                        else if (penaltyMode?.ToLower() == "fixed")
                        {
                            // FIXED AMOUNT MODE
                            penaltyAmount = penaltyValue * numberOfPeriods;
                            penaltyCalculationDetails = $"KES {penaltyValue:N0} × {numberOfPeriods} period(s)";

                            // Cap penalty at 50% of the base amount
                            decimal maxPenalty = penaltyBaseAmount * 0.5m;
                            if (penaltyAmount > maxPenalty)
                            {
                                penaltyAmount = maxPenalty;
                                penaltyCalculationDetails += $" (capped at 50%)";
                            }
                        }

                        _logger.LogInformation($"Penalty calculated: {penaltyAmount:C} - {penaltyCalculationDetails}");
                    }
                }

                // Calculate TOTAL remaining balance (principal + interest + penalty)
                decimal totalRemainingPrincipal = loanbal?.Balance ?? 0;
                decimal totalRemainingInterest = loanbal?.IntrOwed ?? 0;
                decimal totalRemainingPenalty = (loanbal?.Penalty ?? 0) + penaltyAmount;
                decimal totalFullBalance = totalRemainingPrincipal + totalRemainingInterest + totalRemainingPenalty;

                // Current installment due (including penalty)
                decimal currentInstallmentDue = currentSchedule.OutstandingTotal + penaltyAmount;

                // Determine if payment is for current installment or full balance
                bool isFullBalancePayment = amount >= totalFullBalance - 0.01m;
                bool isExactlyCurrentDue = Math.Abs(amount - currentInstallmentDue) < 0.01m;

                decimal penaltyAllocated = 0;
                decimal interestAllocated = 0;
                decimal principalAllocated = 0;
                decimal overpayment = 0;
                decimal balanceAfter = 0;

                if (isFullBalancePayment)
                {
                    // Full balance payment - pay off everything
                    penaltyAllocated = totalRemainingPenalty;
                    interestAllocated = totalRemainingInterest;
                    principalAllocated = totalRemainingPrincipal;
                    overpayment = amount - totalFullBalance;
                    balanceAfter = 0;

                    _logger.LogInformation($"Full balance payment: Amount={amount:C}, Total Due={totalFullBalance:C}");
                }
                else
                {
                    // Regular payment - apply to current installment first
                    decimal remainingAmount = amount;

                    // Apply to penalty first
                    if (remainingAmount > 0 && penaltyAmount > 0)
                    {
                        penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
                        remainingAmount -= penaltyAllocated;
                    }

                    // Apply to interest (current installment)
                    if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
                    {
                        interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
                        remainingAmount -= interestAllocated;
                    }

                    // Apply to principal (current installment)
                    if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
                    {
                        principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
                        remainingAmount -= principalAllocated;
                    }

                    overpayment = remainingAmount;

                    // Calculate remaining balance after payment
                    decimal remainingPrincipalAfter = totalRemainingPrincipal - principalAllocated;
                    decimal remainingInterestAfter = totalRemainingInterest - interestAllocated;
                    decimal remainingPenaltyAfter = totalRemainingPenalty - penaltyAllocated;
                    balanceAfter = remainingPrincipalAfter + remainingInterestAfter + remainingPenaltyAfter;
                }

                _logger.LogInformation($"Repayment Calculation: Installment {currentSchedule.InstallmentNo}, " +
                    $"Amount={amount:C}, CurrentDue={currentInstallmentDue:C}, FullBalance={totalFullBalance:C}, " +
                    $"Penalty={penaltyAmount:C}, IsFullPayment={isFullBalancePayment}");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        penaltyAllocated = Math.Round(penaltyAllocated, 2),
                        interestAllocated = Math.Round(interestAllocated, 2),
                        principalAllocated = Math.Round(principalAllocated, 2),
                        overpayment = Math.Round(overpayment, 2),
                        balanceAfter = Math.Round(balanceAfter, 2),
                        penaltyAmount = Math.Round(penaltyAmount, 2),
                        daysOverdue,
                        installmentNo = currentSchedule.InstallmentNo,
                        dueDate = currentSchedule.DueDate.ToString("yyyy-MM-dd"),
                        currentInstallmentDue = Math.Round(currentInstallmentDue, 2),
                        totalFullBalance = Math.Round(totalFullBalance, 2),
                        isFullBalancePayment,
                        isExactlyCurrentDue,
                        penaltyCalculationDetails,
                        penaltyMode,
                        penaltyRateType,
                        penaltyValue,
                        gracePeriodDays
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating repayment");
                return Json(new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Repay(LoanRepaymentDTO repaymentDto, bool printReceipt = true)
        {
            try
            {
                _logger.LogInformation($"=== REPAY POST CALLED ===");
                _logger.LogInformation($"LoanNo: {repaymentDto.LoanNo}");
                _logger.LogInformation($"Amount: {repaymentDto.AmountPaid:C}");
                _logger.LogInformation($"PaymentMethod: {repaymentDto.PaymentMethod}");
                _logger.LogInformation($"PrintReceipt: {printReceipt}");

                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning($"ModelState invalid: {errors}");

                    TempData["ErrorMessage"] = $"Validation error: {errors}";
                    return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
                }

                if (string.IsNullOrEmpty(repaymentDto.LoanNo))
                {
                    TempData["ErrorMessage"] = "Please select a loan to repay";
                    return RedirectToAction("Repay");
                }

                if (string.IsNullOrEmpty(repaymentDto.PaymentMethod))
                {
                    TempData["ErrorMessage"] = "Please select a payment method";
                    return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
                }

                if (string.IsNullOrEmpty(repaymentDto.GlAccountNo))
                {
                    TempData["ErrorMessage"] = "Please select a GL Account";
                    return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
                }

                if (repaymentDto.AmountPaid <= 0)
                {
                    TempData["ErrorMessage"] = "Please enter a valid payment amount greater than zero";
                    return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
                }

                repaymentDto.CompanyCode = GetUserCompanyCode();
                repaymentDto.ReceivedBy = User.Identity?.Name ?? "SYSTEM";

                var repayment = await _loanService.ProcessRepaymentAsync(repaymentDto);

                TempData["SuccessMessage"] = $"Repayment of KES {repaymentDto.AmountPaid:N0} processed successfully. Receipt: {repayment.ReceiptNo}";

                if (printReceipt)
                {
                    return RedirectToAction("PrintRepaymentReceipt", new { receiptNo = repayment.ReceiptNo });
                }

                return RedirectToAction("AllLoans");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Database error processing repayment: {ex.Message}");

                string errorMessage = "Error processing repayment. ";
                if (ex.InnerException != null)
                {
                    if (ex.InnerException.Message.Contains("String or binary data would be truncated"))
                    {
                        errorMessage += "One or more fields exceed the maximum length allowed.";
                    }
                    else if (ex.InnerException.Message.Contains("FOREIGN KEY"))
                    {
                        errorMessage += "Referenced record does not exist.";
                    }
                    else
                    {
                        errorMessage += ex.InnerException.Message;
                    }
                }
                else
                {
                    errorMessage += ex.Message;
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing repayment: {ex.Message}");
                TempData["ErrorMessage"] = $"Error processing repayment: {ex.Message}";
                return RedirectToAction("Repay", new { loanNo = repaymentDto.LoanNo });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverseRepayment(int repaymentId, string reason, string loanNo)
        {
            try
            {
                _logger.LogInformation($"=== REVERSE REPAYMENT CALLED ===");
                _logger.LogInformation($"RepaymentId: {repaymentId}, Reason: {reason}");

                if (string.IsNullOrEmpty(reason))
                {
                    TempData["ErrorMessage"] = "Please provide a reason for reversing the repayment";
                    return RedirectToAction("Details", new { loanNo });
                }

                var repayment = await _loanService.ReverseRepaymentAsync(
                    repaymentId,
                    reason,
                    User.Identity?.Name ?? "SYSTEM");

                TempData["SuccessMessage"] = $"Repayment {repayment.ReceiptNo} reversed successfully";
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reversing repayment: {ex.Message}");
                TempData["ErrorMessage"] = $"Error reversing repayment: {ex.Message}";
                return RedirectToAction("Details", new { loanNo });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintRepaymentReceipt(string receiptNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                // Get repayment record
                var repayment = await _context.Repay
                    .FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo && r.CompanyCode == companyCode);

                if (repayment == null)
                {
                    return NotFound();
                }

                // Get loan details
                var loan = await _loanService.GetLoanByNoAsync(repayment.LoanNo, companyCode);
                if (loan == null)
                {
                    return NotFound();
                }

                // Get member details
                var member = await _contributionService.GetMemberByMemberNoAsync(repayment.MemberNo);

                // Get loan balance after this repayment
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == repayment.LoanNo && lb.Companycode == companyCode);

                // Calculate balance after
                decimal balanceAfter = repayment.LoanBalance ?? 0;
                decimal interestAfter = repayment.IntrOwed ?? 0;

                // Get total installments and paid count
                var totalSchedules = await _context.LoanSchedules
                    .CountAsync(s => s.LoanNo == repayment.LoanNo);

                var paidSchedules = await _context.LoanSchedules
                    .CountAsync(s => s.LoanNo == repayment.LoanNo && s.Status == "Paid");

                // Check if this is a full settlement
                bool isFullSettlement = balanceAfter <= 0.01m && interestAfter <= 0.01m;

                // Determine payment method from repayment or loan
                string paymentMethod = "CASH";
                string referenceNo = repayment.ApiKey;

                if (!string.IsNullOrEmpty(referenceNo) && referenceNo.StartsWith("CHQ"))
                {
                    paymentMethod = "CHEQUE";
                }
                else if (!string.IsNullOrEmpty(referenceNo) && (referenceNo.StartsWith("MPESA") || referenceNo.Length == 10))
                {
                    paymentMethod = "MPESA";
                }
                else if (!string.IsNullOrEmpty(referenceNo) && referenceNo.StartsWith("TRF"))
                {
                    paymentMethod = "BANK_TRANSFER";
                }

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                // Build address
                string companyAddress = "P.O. Box 12345 - 00100, Nairobi, Kenya";
                if (company != null)
                {
                    var addressParts = new List<string>();
                    if (!string.IsNullOrEmpty(company.Address)) addressParts.Add(company.Address);
                    if (!string.IsNullOrEmpty(company.County)) addressParts.Add(company.County);
                    if (addressParts.Any()) companyAddress = string.Join(", ", addressParts);
                }

                var receiptModel = new RepaymentReceiptViewModel
                {
                    ReceiptNo = repayment.ReceiptNo,
                    LoanNo = repayment.LoanNo,
                    MemberNo = repayment.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : repayment.MemberNo,
                    MemberPhone = member?.PhoneNo ?? member?.MobileNo ?? "N/A",
                    MemberIdNo = member?.Idno ?? "N/A",
                    PaymentDate = repayment.DateReceived ?? DateTime.Now,
                    AmountPaid = repayment.Amount ?? 0,
                    PrincipalAllocated = repayment.Principal ?? 0,
                    InterestAllocated = repayment.Interest ?? 0,
                    PenaltyAllocated = repayment.Penalty ?? 0,
                    BalanceAfter = balanceAfter,
                    InterestAfter = interestAfter,
                    PaymentMethod = paymentMethod,
                    ReferenceNo = referenceNo,
                    Remarks = repayment.Remarks,
                    BlockchainTxId = repayment.BlockchainTxId ?? loan.BlockchainTxId,
                    CompanyCode = companyCode,
                    CompanyName = company?.CompanyName ?? "SACCO Blockchain System",
                    CompanyAddress = companyAddress,
                    CompanyPhone = company?.Telephone ?? "+254 700 000 000",
                    CompanyEmail = company?.Email ?? "info@sacco.co.ke",
                    ReceivedBy = repayment.Transby ?? repayment.AuditId ?? "SYSTEM",
                    PaymentNumber = repayment.PaymentNo ?? 1,
                    TotalInstallments = totalSchedules,
                    InstallmentsPaid = paidSchedules,
                    IsFullSettlement = isFullSettlement,
                    PrintedAt = DateTime.Now
                };

                return View(receiptModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing repayment receipt for receipt {receiptNo}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("AllLoans");
            }
        }

        #endregion


        #region Loan Status Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLoanStatus(string loanNo, string newStatus)
        {
            try
            {

                var companyCode = GetUserCompanyCode();

                // Verify the loan exists and user has access
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    newStatus,
                    User.Identity?.Name ?? "SYSTEM",
                    $"Status updated to {newStatus}");

                ViewBag.SuccessMessage = $"Loan status updated to {newStatus} successfully";
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan status for {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLoan(string loanNo, string rejectionReason)
        {
            try
            {

                var companyCode = GetUserCompanyCode();

                // Verify the loan exists and user has access
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status to Rejected with reason
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    "Rejected",
                    User.Identity?.Name ?? "SYSTEM",
                    $"Loan rejected: {rejectionReason}");

                ViewBag.SuccessMessage = "Loan rejected successfully";
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting loan {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteOffLoan(string loanNo, string writeOffReason)
        {
            try
            {

                var companyCode = GetUserCompanyCode();
                var isAdmin = IsAdminUser();

                // Only admins can write off loans
                if (!isAdmin)
                {
                    ViewBag.ErrorMessage = "You don't have permission to write off loans";
                    //return RedirectToAction("Details", new { loanNo });
                    return RedirectToAction("AllLoans");
                }

                // Verify the loan exists
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status to WrittenOff
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    "WrittenOff",
                    User.Identity?.Name ?? "SYSTEM",
                    $"Loan written off: {writeOffReason}");

                ViewBag.SuccessMessage = "Loan written off successfully";
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing off loan {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                //return RedirectToAction("Details", new { loanNo });
                return RedirectToAction("AllLoans");
            }
        }

        #endregion

        #region Loan Search

        [HttpGet]
        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchResults(LoanSearchDTO searchDto)
        {
            try
            {
                searchDto.CompanyCode = GetUserCompanyCode();

                var results = await _loanService.SearchLoansAsync(searchDto);

                ViewBag.SearchCriteria = searchDto;
                ViewBag.ResultCount = results.Count;


                return View(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return View("Error");
            }
        }

        #endregion

        #region Member Loans

        [HttpGet]
        public async Task<IActionResult> MemberLoans(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                var loans = await _loanService.GetMemberLoansAsync(memberNo, companyCode);

                ViewBag.Member = member;
                ViewBag.LoanCount = loans.Count;
                ViewBag.TotalLoanAmount = loans.Sum(l => l.PrincipalAmount);
                ViewBag.TotalOutstanding = loans.Sum(l => l.OutstandingBalance);


                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loans for member {memberNo}");
                return View("Error");
            }
        }

        #endregion

        #region Loan Schedule

       
        [HttpGet]
        public IActionResult Schedule()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Schedule(string loanNo)
        {
            if (string.IsNullOrEmpty(loanNo))
            {
                ViewBag.Error = "Please enter Loan Number";
                return View();
            }

            var companyCode = GetUserCompanyCode();

            var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
            if (loan == null)
            {
                ViewBag.Error = "Loan not found";
                return View();
            }

            var schedule = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .ToListAsync();

            ViewBag.Loan = loan;

            return View(schedule);
        }

        [HttpGet]
        public async Task<IActionResult> ExportSchedule(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                var csv = new StringBuilder();
                csv.AppendLine("Installment,Due Date,Principal,Interest,Total,Paid,Outstanding,Penalty,Status,Paid Date");

                foreach (var inst in schedule)
                {
                    csv.AppendLine($"\"{inst.InstallmentNo}\",\"{inst.DueDate:dd/MM/yyyy}\",{inst.PrincipalAmount:N2},{inst.InterestAmount:N2},{inst.TotalInstallment:N2},{inst.PaidAmount:N2},{inst.OutstandingAmount:N2},{inst.PenaltyAmount:N2},\"{inst.Status}\",\"{inst.PaidDate?.ToString("dd/MM/yyyy") ?? ""}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"Schedule_{loanNo}_{DateTime.Now:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting schedule for {loanNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Loan Offset with Shares

        [HttpGet]
        public async Task<IActionResult> OffsetLoan()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                ViewBag.CompanyCode = companyCode;

                // Load GL accounts for dropdown (optional, for display)
                var glAccounts = await _context.GlSetup
                    .Where(g => g.CompanyCode == companyCode && g.Status == true)
                    .Select(g => new
                    {
                        AccNo = g.AccNo,
                        Glaccname = g.Glaccname,
                        DisplayText = $"{g.AccNo} - {g.Glaccname}"
                    })
                    .ToListAsync();

                ViewBag.GlAccounts = glAccounts;

                var offsetDto = new LoanOffsetDTO
                {
                    CompanyCode = companyCode,
                    ProcessedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(offsetDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan offset page");
                TempData["ErrorMessage"] = $"Error loading loan offset page: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableShares(string memberNo, string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = GetUserCompanyCode();
                }

                var availableShares = await _loanService.GetAvailableSharesForOffsetAsync(memberNo, companyCode);

                return Json(new
                {
                    success = true,
                    shares = availableShares
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting available shares for member {memberNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CalculateOffset(string loanNo, decimal amount, string sharesCode, string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = GetUserCompanyCode();
                }

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var loanbal = await _loanService.GetLoanBalanceAsync(loanNo);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Loan not found" });
                }

                // GET CURRENT SCHEDULE - first unpaid installment
                var currentSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                if (currentSchedule == null)
                {
                    return Json(new { success = false, message = "No active installment found for this loan" });
                }

                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);

                // ✅ CALCULATE FULL LOAN BALANCE (Total remaining)
                // This is the TOTAL amount needed to close the loan completely
                decimal totalRemainingPrincipal = loanbal?.Balance ?? 0;
                decimal totalRemainingInterest = loanbal?.IntrOwed ?? 0;
                decimal totalRemainingPenalty = loanbal?.Penalty ?? 0;
                decimal fullLoanBalance = totalRemainingPrincipal + totalRemainingInterest + totalRemainingPenalty;

                // ✅ Calculate current monthly installment due (for display only)
                decimal monthlyDue = currentSchedule.OutstandingTotal;

                // ✅ Calculate penalty for current installment if overdue
                decimal penaltyAmount = 0;
                int daysOverdue = 0;

                if (loanType != null && loanType.Penalty == 1 && DateTime.Now > currentSchedule.DueDate)
                {
                    daysOverdue = (DateTime.Now - currentSchedule.DueDate).Days;
                    int gracePeriodDays = loanType.GracePeriod > 0 ? loanType.GracePeriod : 0;

                    if (daysOverdue > gracePeriodDays)
                    {
                        int overdueDaysAfterGrace = daysOverdue - gracePeriodDays;
                        int overdueMonths = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);

                        // ✅ USE THE ACTUAL PENALTYVALUE FROM LOANTYPE (NOT HARDCODED)
                        decimal penaltyRatePercent = 0;
                        if (loanType.PenaltyValue != null)
                        {
                            decimal.TryParse(loanType.PenaltyValue.ToString(), out penaltyRatePercent);
                        }
                        decimal monthlyPenaltyRate = penaltyRatePercent / 100;

                        if (monthlyPenaltyRate > 0)
                        {
                            penaltyAmount = currentSchedule.OutstandingTotal * monthlyPenaltyRate * overdueMonths;
                            _logger.LogInformation($"Penalty calculated: {penaltyAmount:C} for {daysOverdue} days overdue " +
                                $"(Grace: {gracePeriodDays} days, Rate: {monthlyPenaltyRate:P}, Months: {overdueMonths})");
                        }
                    }
                }

                // ✅ Determine if payment is for monthly due or full balance
                bool isFullBalancePayment = amount >= fullLoanBalance - 0.01m;
                bool isMonthlyDuePayment = amount >= monthlyDue - 0.01m && amount < fullLoanBalance;

                decimal penaltyAllocated = 0;
                decimal interestAllocated = 0;
                decimal principalAllocated = 0;
                decimal overpayment = 0;
                decimal balanceAfter = 0;

                if (isFullBalancePayment)
                {
                    // FULL BALANCE PAYMENT - Pay off everything
                    penaltyAllocated = totalRemainingPenalty;
                    interestAllocated = totalRemainingInterest;
                    principalAllocated = totalRemainingPrincipal;
                    overpayment = amount - fullLoanBalance;
                    balanceAfter = 0;

                    _logger.LogInformation($"Full balance offset: Amount={amount:C}, Full Balance={fullLoanBalance:C}");
                }
                else
                {
                    // Apply to current installment first (Penalty -> Interest -> Principal)
                    decimal remainingAmount = amount;

                    // Apply to penalty
                    if (remainingAmount > 0 && penaltyAmount > 0)
                    {
                        penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
                        remainingAmount -= penaltyAllocated;
                    }

                    // Apply to interest (current schedule)
                    if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
                    {
                        interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
                        remainingAmount -= interestAllocated;
                    }

                    // Apply to principal (current schedule)
                    if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
                    {
                        principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
                        remainingAmount -= principalAllocated;
                    }

                    overpayment = remainingAmount;

                    // Calculate remaining balance after payment
                    decimal remainingPrincipalAfter = totalRemainingPrincipal - principalAllocated;
                    decimal remainingInterestAfter = totalRemainingInterest - interestAllocated;
                    decimal remainingPenaltyAfter = totalRemainingPenalty - penaltyAllocated;
                    balanceAfter = remainingPrincipalAfter + remainingInterestAfter + remainingPenaltyAfter;
                }

                _logger.LogInformation($"Offset Calculation: Monthly Due={monthlyDue:C}, Full Balance={fullLoanBalance:C}, Amount={amount:C}");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        penaltyAllocated = Math.Round(penaltyAllocated, 2),
                        interestAllocated = Math.Round(interestAllocated, 2),
                        principalAllocated = Math.Round(principalAllocated, 2),
                        overpayment = Math.Round(overpayment, 2),
                        balanceAfter = Math.Round(balanceAfter, 2),
                        penaltyAmount = Math.Round(penaltyAmount, 2),
                        daysOverdue,
                        installmentNo = currentSchedule.InstallmentNo,
                        dueDate = currentSchedule.DueDate,
                        monthlyDue = Math.Round(monthlyDue, 2),
                        fullLoanBalance = Math.Round(fullLoanBalance, 2),
                        isFullBalancePayment,
                        isMonthlyDuePayment
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating offset");
                return Json(new { success = false, message = ex.Message });
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> CalculateOffset(string loanNo, decimal amount, string sharesCode, string companyCode)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(companyCode))
        //        {
        //            companyCode = GetUserCompanyCode();
        //        }

        //        var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
        //        var loanbal = await _loanService.GetLoanBalanceAsync(loanNo);

        //        if (loan == null)
        //        {
        //            return Json(new { success = false, message = "Loan not found" });
        //        }

        //        // GET CURRENT SCHEDULE - first unpaid installment
        //        var currentSchedule = await _context.LoanSchedules
        //            .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
        //            .OrderBy(s => s.InstallmentNo)
        //            .FirstOrDefaultAsync();

        //        if (currentSchedule == null)
        //        {
        //            return Json(new { success = false, message = "No active installment found for this loan" });
        //        }

        //        var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, companyCode);

        //        // 7. CALCULATE PENALTY - USING ACTUAL LOANTYPE VALUES
        //        decimal penaltyAmount = 0;
        //        int daysOverdue = 0;

        //        if (loanType != null && loanType.Penalty == true && DateTime.Now > currentSchedule.DueDate)
        //        {
        //            daysOverdue = (DateTime.Now - currentSchedule.DueDate).Days;
        //            int gracePeriodDays = loanType.GracePeriod > 0 ? loanType.GracePeriod : 0;

        //            if (daysOverdue > gracePeriodDays)
        //            {
        //                int overdueDaysAfterGrace = daysOverdue - gracePeriodDays;
        //                int overdueMonths = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);

        //                // ✅ USE THE ACTUAL PENALTYVALUE FROM LOANTYPE (NOT HARDCODED)
        //                decimal penaltyRatePercent = 0;
        //                if (loanType.PenaltyValue != null)
        //                {
        //                    decimal.TryParse(loanType.PenaltyValue.ToString(), out penaltyRatePercent);
        //                }
        //                decimal monthlyPenaltyRate = penaltyRatePercent / 100;

        //                if (monthlyPenaltyRate > 0)
        //                {
        //                    penaltyAmount = currentSchedule.OutstandingTotal * monthlyPenaltyRate * overdueMonths;
        //                    _logger.LogInformation($"Penalty calculated: {penaltyAmount:C} for {daysOverdue} days overdue " +
        //                        $"(Grace: {gracePeriodDays} days, Rate: {monthlyPenaltyRate:P}, Months: {overdueMonths})");
        //                }
        //            }
        //        }

        //        // Calculate TOTAL remaining balance (principal + interest + penalty)
        //        decimal totalRemainingPrincipal = currentSchedule.OutstandingPrincipal;
        //        decimal totalRemainingInterest = currentSchedule.OutstandingInterest;
        //        decimal totalRemainingPenalty = (loanbal?.Penalty ?? 0) + penaltyAmount;
        //        decimal totalFullBalance = totalRemainingPrincipal + totalRemainingInterest + totalRemainingPenalty;

        //        // Determine if payment is for current installment or full balance
        //        bool isFullBalancePayment = amount >= totalFullBalance - 0.01m;

        //        decimal penaltyAllocated = 0;
        //        decimal interestAllocated = 0;
        //        decimal principalAllocated = 0;
        //        decimal overpayment = 0;
        //        decimal balanceAfter = 0;

        //        if (isFullBalancePayment)
        //        {
        //            // Full balance payment - pay off everything
        //            penaltyAllocated = totalRemainingPenalty;
        //            interestAllocated = totalRemainingInterest;
        //            principalAllocated = totalRemainingPrincipal;
        //            overpayment = amount - totalFullBalance;
        //            balanceAfter = 0;

        //            _logger.LogInformation($"Full balance offset: Amount={amount:C}, Total Due={totalFullBalance:C}");
        //        }
        //        else
        //        {
        //            // Regular payment - apply to current installment first
        //            decimal remainingAmount = amount;

        //            // Apply to penalty
        //            if (remainingAmount > 0 && penaltyAmount > 0)
        //            {
        //                penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
        //                remainingAmount -= penaltyAllocated;
        //            }

        //            // Apply to interest (current installment)
        //            if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
        //            {
        //                interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
        //                remainingAmount -= interestAllocated;
        //            }

        //            // Apply to principal (current installment)
        //            if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
        //            {
        //                principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
        //                remainingAmount -= principalAllocated;
        //            }

        //            overpayment = remainingAmount;

        //            // Calculate remaining balance after payment
        //            decimal remainingPrincipalAfter = totalRemainingPrincipal - principalAllocated;
        //            decimal remainingInterestAfter = totalRemainingInterest - interestAllocated;
        //            decimal remainingPenaltyAfter = totalRemainingPenalty - penaltyAllocated;
        //            balanceAfter = remainingPrincipalAfter + remainingInterestAfter + remainingPenaltyAfter;
        //        }

        //        _logger.LogInformation($"Offset Calculation: Installment {currentSchedule.InstallmentNo}, " +
        //            $"Amount={amount:C}, FullBalance={totalFullBalance:C}, IsFullPayment={isFullBalancePayment}");

        //        return Json(new
        //        {
        //            success = true,
        //            data = new
        //            {
        //                penaltyAllocated = Math.Round(penaltyAllocated, 2),
        //                interestAllocated = Math.Round(interestAllocated, 2),
        //                principalAllocated = Math.Round(principalAllocated, 2),
        //                overpayment = Math.Round(overpayment, 2),
        //                balanceAfter = Math.Round(balanceAfter, 2),
        //                penaltyAmount = Math.Round(penaltyAmount, 2),
        //                daysOverdue,
        //                installmentNo = currentSchedule.InstallmentNo,
        //                dueDate = currentSchedule.DueDate,
        //                currentInstallmentDue = currentSchedule.OutstandingTotal + penaltyAmount,
        //                totalFullBalance = Math.Round(totalFullBalance, 2),
        //                isFullBalancePayment
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error calculating offset");
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OffsetLoan(LoanOffsetDTO offsetDto)
        {
            try
            {
                _logger.LogInformation($"=== OFFSET LOAN POST CALLED ===");
                _logger.LogInformation($"LoanNo: {offsetDto.LoanNo}");
                _logger.LogInformation($"SharesCode: {offsetDto.SharesCode}");
                _logger.LogInformation($"Amount: {offsetDto.AmountToOffset:C}");

                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning($"ModelState invalid: {errors}");
                    TempData["ErrorMessage"] = $"Validation error: {errors}";
                    return RedirectToAction("OffsetLoan");
                }

                offsetDto.CompanyCode = GetUserCompanyCode();
                offsetDto.ProcessedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanService.OffsetLoanWithSharesAsync(offsetDto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    //return RedirectToAction("Details", new { loanNo = offsetDto.LoanNo });
                    return RedirectToAction("AllLoans");
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("OffsetLoan");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing loan offset: {ex.Message}");
                TempData["ErrorMessage"] = $"Error processing loan offset: {ex.Message}";
                return RedirectToAction("OffsetLoan");
            }
        }

        #endregion

        #region Reports

        [HttpGet]
        public async Task<IActionResult> Statement(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;


                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating loan statement for {loanNo}");
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintStatement(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;

                ViewBag.PrintMode = true;

                return View("Statement", loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing loan statement for {loanNo}");
                return View("Error");
            }
        }

        #endregion

        #region Helper Methods

        private string GetUserCompanyCode()
        {
            var companyCode = _companyContextService.GetCurrentCompanyCode();
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }

        private int GetRequiredApprovalLevels()
        {
            // This could be configured in system settings
            return 2; // Two-level approval: Loan Officer -> Manager
        }

        #endregion


        [HttpGet]
        public async Task<IActionResult> GetWorkflowCounts(string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = GetUserCompanyCode();
                }

                var dashboard = await _loanService.GetLoanDashboardAsync(companyCode);

                var counts = new
                {
                    success = true,
                    data = new
                    {
                        underAppraisal = dashboard.UnderAppraisal,
                        pendingApproval = dashboard.PendingApproval,
                        pendingFinalApproval = dashboard.PendingFinalApproval,
                        approvedPendingDisbursement = dashboard.ApprovedPendingDisbursement,
                        pendingApplications = dashboard.PendingApplications,
                        activeLoans = dashboard.ActiveLoans,
                        overdueLoans = dashboard.OverdueLoans
                    }
                };

                return Json(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow counts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchLoans(string searchType, string searchValue, string companyCode)
        {
            try
            {
                var loans = new List<object>();

                switch (searchType.ToLower())
                {
                    case "memberno":
                        var memberLoans = await _context.Loans
                            .Where(l => l.MemberNo.Contains(searchValue) && l.CompanyCode == companyCode)
                            .ToListAsync();
                        loans = await MapLoansToSearchResults(memberLoans);
                        break;

                    case "loanno":
                        var loanByNo = await _context.Loans
                            .Where(l => l.LoanNo.Contains(searchValue) && l.CompanyCode == companyCode)
                            .ToListAsync();
                        loans = await MapLoansToSearchResults(loanByNo);
                        break;

                    case "idno":
                        var membersById = await _context.Members
                            .Where(m => m.Idno == searchValue && m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        var loansById = await _context.Loans
                            .Where(l => membersById.Contains(l.MemberNo) && l.CompanyCode == companyCode)
                            .ToListAsync();
                        loans = await MapLoansToSearchResults(loansById);
                        break;

                    case "fullname":
                        var membersByName = await _context.Members
                            .Where(m => (m.Surname + " " + m.OtherNames).Contains(searchValue) && m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        var loansByName = await _context.Loans
                            .Where(l => membersByName.Contains(l.MemberNo) && l.CompanyCode == companyCode)
                            .ToListAsync();
                        loans = await MapLoansToSearchResults(loansByName);
                        break;

                    case "phoneno":
                        var membersByPhone = await _context.Members
                            .Where(m => m.PhoneNo.Contains(searchValue) && m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        var loansByPhone = await _context.Loans
                            .Where(l => membersByPhone.Contains(l.MemberNo) && l.CompanyCode == companyCode)
                            .ToListAsync();
                        loans = await MapLoansToSearchResults(loansByPhone);
                        break;
                }

                return Json(new { success = true, loans = loans });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<List<object>> MapLoansToSearchResults(List<Loan> loans)
        {
            var results = new List<object>();

            foreach (var loan in loans)
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo);

                var memberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo;

                results.Add(new
                {
                    loanNo = loan.LoanNo,
                    memberNo = loan.MemberNo,
                    memberName = memberName,
                    loanType = loan.LoanCode,
                    principalAmount = loan.LoanAmt ?? 0,
                    status = ((Status)(loan.Status ?? 0)).ToString(),
                    applicationDate = loan.ApplicDate
                });
            }

            return results;
        }

        #region Guarantor Reports
        [HttpGet]
        public async Task<IActionResult> GuarantorsPerLoanReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "SACCO BlockChain System";
                var printedBy = User.Identity?.Name ?? "System";
                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

                // Set default date range (last 6 months if not provided)
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddMonths(-6);
                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                // Ensure end date is at end of day
                endDate = endDate.Value.Date.AddDays(1).AddTicks(-1);

                // Validate date range
                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "Start date cannot be later than end date.";
                    return RedirectToAction("AllLoans");
                }

                // Check if date range is too large (more than 1 year)
                if ((endDate - startDate).Value.TotalDays > 365)
                {
                    TempData["WarningMessage"] = "Date range exceeds 1 year. Please narrow your search for better performance.";
                }

                _logger.LogInformation($"Generating Guarantors Per Loan Report for company: {companyCode}, Date Range: {startDate} to {endDate}");

                // Get report data from service
                var reportData = await _loanService.GetGuarantorsPerLoanReportAsync(companyCode, startDate, endDate);

                // Calculate summary statistics
                var totalLoans = reportData.Count;
                var totalGuarantors = reportData.Sum(l => l.Guarantors?.Count ?? 0);
                var totalGuaranteeAmount = reportData.Sum(l => l.TotalGuaranteeAmount);
                var fullyGuaranteed = reportData.Count(l => l.IsFullyGuaranteed);
                var partiallyGuaranteed = reportData.Count(l => !l.IsFullyGuaranteed && l.Guarantors.Any());
                var loansWithoutGuarantors = reportData.Count(l => !l.Guarantors.Any());
                var averageGuarantorsPerLoan = totalLoans > 0 ? Math.Round((decimal)totalGuarantors / totalLoans, 2) : 0;
                var averageGuaranteeAmount = totalLoans > 0 ? Math.Round(totalGuaranteeAmount / totalLoans, 2) : 0;

                // Create view model
                var viewModel = new GuarantorsPerLoanIndexViewModel
                {
                    Loans = reportData,
                    ReportDate = DateTime.Now,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = reportData.Any(),
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalLoans = totalLoans,
                    TotalGuarantors = totalGuarantors,
                    TotalGuaranteeAmount = totalGuaranteeAmount,
                    FullyGuaranteedLoans = fullyGuaranteed,
                    PartiallyGuaranteedLoans = partiallyGuaranteed,

                    // Additional properties for display
                    LoansWithoutGuarantors = loansWithoutGuarantors,
                    AverageGuarantorsPerLoan = averageGuarantorsPerLoan,
                    AverageGuaranteeAmount = averageGuaranteeAmount,
                    UserEmail = userEmail
                };

                // Store for export actions
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.CompanyCode = companyCode;

                _logger.LogInformation($"Report generated successfully. Total Loans: {totalLoans}, Total Guarantors: {totalGuarantors}");

                // Add success message if data exists
                if (viewModel.HasData)
                {
                    TempData["SuccessMessage"] = $"Report generated successfully with {totalLoans} loans and {totalGuarantors} guarantors.";
                }
                else
                {
                    TempData["InfoMessage"] = "No data found for the selected date range. Try adjusting your filters.";
                }

                return View("~/Views/LoanMvc/GuarantorsPerLoanReport.cshtml", viewModel);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogError(ex, "Null argument error in GuarantorsPerLoanReport");
                TempData["ErrorMessage"] = "Invalid data received. Please try again.";
                return RedirectToAction("AllLoans");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error in GuarantorsPerLoanReport");
                TempData["ErrorMessage"] = "An operation error occurred. Please contact support.";
                return RedirectToAction("AllLoans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GuarantorsPerLoanReport");
                TempData["ErrorMessage"] = $"Error loading report: {ex.Message}";
                return RedirectToAction("AllLoans");
            }
        }

        [HttpGet]
        public async Task<IActionResult> AllGuarantorsReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "SACCO BlockChain System";
                var printedBy = User.Identity?.Name ?? "System";

                // Set default date range (last 6 months if not provided)
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddMonths(-6);
                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                // Ensure end date is at end of day
                endDate = endDate.Value.Date.AddDays(1).AddTicks(-1);

                // Validate date range
                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "Start date cannot be later than end date.";
                    return RedirectToAction("AllLoans");
                }

                _logger.LogInformation($"Generating All Guarantors Report for company: {companyCode}, Date Range: {startDate} to {endDate}");

                var reportData = await _loanService.GetAllGuarantorsReportAsync(companyCode, startDate, endDate);

                // Calculate summary statistics safely
                var totalRecords = reportData?.Count ?? 0;
                var totalGuaranteeAmount = reportData?.Sum(g => g.GuaranteeAmount) ?? 0;
                var activeGuarantors = reportData?.Count(g => !g.Transfered) ?? 0;
                var releasedGuarantors = reportData?.Count(g => g.Transfered) ?? 0;
                var uniqueLoans = reportData?.Select(g => g.LoanNo).Where(l => !string.IsNullOrEmpty(l)).Distinct().Count() ?? 0;
                var uniqueGuarantors = reportData?.Select(g => g.GuarantorMemberNo).Where(m => !string.IsNullOrEmpty(m)).Distinct().Count() ?? 0;

                var viewModel = new AllGuarantorsIndexViewModel
                {
                    Guarantors = reportData ?? new List<AllGuarantorsReportDTO>(),
                    ReportDate = DateTime.Now,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = reportData != null && reportData.Any(),
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalRecords = totalRecords,
                    UniqueLoans = uniqueLoans,
                    UniqueGuarantors = uniqueGuarantors,
                    TotalGuaranteeAmount = totalGuaranteeAmount,
                    ActiveGuarantors = activeGuarantors,
                    ReleasedGuarantors = releasedGuarantors
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                if (viewModel.HasData)
                {
                    TempData["SuccessMessage"] = $"Report generated successfully with {totalRecords} records.";
                }
                else
                {
                    TempData["InfoMessage"] = "No guarantor records found for the selected date range.";
                }

                return View("~/Views/LoanMvc/AllGuarantorsReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all guarantors report");
                TempData["ErrorMessage"] = $"Error loading report: {ex.Message}";
                return RedirectToAction("AllLoans");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportGuarantorsPerLoanToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var reportData = await _loanService.GetGuarantorsPerLoanReportAsync(companyCode, startDate, endDate);

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("GuarantorsPerLoanReport");
                }

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

                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text("GUARANTORS PER LOAN REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics Table
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loans:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Guarantors:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Sum(l => l.Guarantors.Count).ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Guarantee:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(l => l.TotalGuaranteeAmount):N0}");
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Fully Guaranteed:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count(l => l.IsFullyGuaranteed).ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Partially Guaranteed:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count(l => !l.IsFullyGuaranteed && l.Guarantors.Any()).ToString());
                            });

                            // Loans with Guarantors Table
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("LOANS WITH GUARANTORS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // Loan No
                                    cols.RelativeColumn(1.0f);   // Member No
                                    cols.RelativeColumn(1.5f);   // Member Name
                                    cols.RelativeColumn(1.0f);   // Loan Amount
                                    cols.RelativeColumn(1.0f);   // Total Guarantee
                                    cols.RelativeColumn(0.8f);   // Status
                                    cols.RelativeColumn(0.8f);   // Guarantor Count
                                    cols.RelativeColumn(2.5f);   // Guarantors List
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Guarantee").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantors").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantor List").Bold().FontSize(8);
                                });

                                foreach (var loan in reportData)
                                {
                                    string guarantorList = string.Join(", ", loan.Guarantors.Select(g => $"{g.GuarantorName} (KES {g.GuaranteeAmount:N0})"));
                                    string statusBadge = loan.IsFullyGuaranteed ? "Fully Guaranteed" : "Partially Guaranteed";

                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.MemberName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanAmount:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.TotalGuaranteeAmount:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(statusBadge).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.Guarantors.Count.ToString()).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(guarantorList).FontSize(8);
                                }

                                // Totals row
                                table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(l => l.TotalGuaranteeAmount):N0}").Bold().FontSize(9);
                                table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4);
                            });
                        });

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
                return File(content, "application/pdf", $"GuarantorsPerLoan_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting guarantors per loan to PDF");
                TempData["Error"] = $"Error exporting to PDF: {ex.Message}";
                return RedirectToAction("GuarantorsPerLoanReport");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportGuarantorsPerLoanToExcel(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var reportData = await _loanService.GetGuarantorsPerLoanReportAsync(companyCode, startDate, endDate);

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("GuarantorsPerLoanReport");
                }

                using var workbook = new XLWorkbook();

                // Summary Sheet
                var summarySheet = workbook.Worksheets.Add("Summary");
                int currentRow = 1;

                summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
                summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                summarySheet.Cell(currentRow, 1).Value = $"GUARANTORS PER LOAN REPORT";
                summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
                summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                summarySheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
                summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
                summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
                currentRow += 2;

                // Statistics
                summarySheet.Cell(currentRow, 1).Value = "Total Loans with Guarantors:";
                summarySheet.Cell(currentRow, 2).Value = reportData.Count;
                summarySheet.Cell(currentRow, 3).Value = "Total Guarantors:";
                summarySheet.Cell(currentRow, 4).Value = reportData.Sum(l => l.Guarantors.Count);
                currentRow++;

                summarySheet.Cell(currentRow, 1).Value = "Total Guarantee Amount:";
                summarySheet.Cell(currentRow, 2).Value = reportData.Sum(l => l.TotalGuaranteeAmount);
                summarySheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 3).Value = "Fully Guaranteed Loans:";
                summarySheet.Cell(currentRow, 4).Value = reportData.Count(l => l.IsFullyGuaranteed);
                currentRow++;

                summarySheet.Cell(currentRow, 1).Value = "Partially Guaranteed Loans:";
                summarySheet.Cell(currentRow, 2).Value = reportData.Count(l => !l.IsFullyGuaranteed && l.Guarantors.Any());

                summarySheet.Columns().AdjustToContents();

                // Detailed Sheet - Loans with Guarantors
                var detailSheet = workbook.Worksheets.Add("Loans with Guarantors");
                currentRow = 1;

                detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                detailSheet.Range(currentRow, 1, currentRow, 9).Merge();
                detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(16);
                detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                detailSheet.Cell(currentRow, 1).Value = $"GUARANTORS PER LOAN DETAILS";
                detailSheet.Range(currentRow, 1, currentRow, 9).Merge();
                detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                string[] headers = { "Loan No", "Member No", "Member Name", "Loan Amount", "Loan Status", "Total Guarantee", "Fully Guaranteed", "Guarantor Count", "Guarantors" };

                for (int i = 0; i < headers.Length; i++)
                {
                    detailSheet.Cell(currentRow, i + 1).Value = headers[i];
                    detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;

                foreach (var loan in reportData)
                {
                    string guarantorList = string.Join(", ", loan.Guarantors.Select(g => $"{g.GuarantorName} (KES {g.GuaranteeAmount:N0})"));

                    detailSheet.Cell(currentRow, 1).Value = loan.LoanNo;
                    detailSheet.Cell(currentRow, 2).Value = loan.MemberNo;
                    detailSheet.Cell(currentRow, 3).Value = loan.MemberName;
                    detailSheet.Cell(currentRow, 4).Value = loan.LoanAmount;
                    detailSheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 5).Value = loan.LoanStatus;
                    detailSheet.Cell(currentRow, 6).Value = loan.TotalGuaranteeAmount;
                    detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 7).Value = loan.IsFullyGuaranteed ? "Yes" : "No";
                    detailSheet.Cell(currentRow, 8).Value = loan.Guarantors.Count;
                    detailSheet.Cell(currentRow, 9).Value = guarantorList;

                    currentRow++;
                }

                detailSheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"GuarantorsPerLoan_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting guarantors per loan to Excel");
                TempData["Error"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("GuarantorsPerLoanReport");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportAllGuarantorsToExcel(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var reportData = await _loanService.GetAllGuarantorsReportAsync(companyCode, startDate, endDate);

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("AllGuarantorsReport");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("All Guarantors");
                int currentRow = 1;

                // Header
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"ALL GUARANTORS REPORT";
                worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
                currentRow += 2;

                // Headers
                string[] headers = { "Loan No", "Borrower No", "Borrower Name", "Guarantor No", "Guarantor Name",
                            "ID No", "Phone", "Guarantee Amount", "Outstanding Balance", "Status", "Assigned Date" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;

                foreach (var item in reportData)
                {
                    worksheet.Cell(currentRow, 1).Value = item.LoanNo;
                    worksheet.Cell(currentRow, 2).Value = item.MemberNo;
                    worksheet.Cell(currentRow, 3).Value = item.MemberName;
                    worksheet.Cell(currentRow, 4).Value = item.GuarantorMemberNo;
                    worksheet.Cell(currentRow, 5).Value = item.GuarantorName;
                    worksheet.Cell(currentRow, 6).Value = item.GuarantorIdNo ?? "-";
                    worksheet.Cell(currentRow, 7).Value = item.GuarantorPhone ?? "-";
                    worksheet.Cell(currentRow, 8).Value = item.GuaranteeAmount;
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 9).Value = item.OutstandingBalance ?? 0;
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 10).Value = item.Transfered ? "Released" : "Active";
                    worksheet.Cell(currentRow, 11).Value = item.AssignedDate?.ToString("dd/MM/yyyy") ?? "-";
                    currentRow++;
                }

                // Totals
                currentRow++;
                worksheet.Cell(currentRow, 7).Value = "TOTAL:";
                worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                worksheet.Cell(currentRow, 8).Value = reportData.Sum(g => g.GuaranteeAmount);
                worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 9).Value = reportData.Sum(g => g.OutstandingBalance ?? 0);
                worksheet.Cell(currentRow, 9).Style.Font.SetBold();
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"AllGuarantors_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting all guarantors to Excel");
                TempData["Error"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("AllGuarantorsReport");
            }
        }


        [HttpGet]
        public async Task<IActionResult> ExportAllGuarantorsToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var reportData = await _loanService.GetAllGuarantorsReportAsync(companyCode, startDate, endDate);

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("AllGuarantorsReport");
                }

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

                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text("ALL GUARANTORS REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics Table
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Records:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Unique Loans:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Select(g => g.LoanNo).Distinct().Count().ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Unique Guarantors:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Select(g => g.GuarantorMemberNo).Distinct().Count().ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Guarantee:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(g => g.GuaranteeAmount):N0}");

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Active Guarantors:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count(g => !g.Transfered).ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Released Guarantors:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count(g => g.Transfered).ToString());
                            });

                            // All Guarantors Details Table
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("GUARANTOR DETAILS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // Loan No
                                    cols.RelativeColumn(1.0f);   // Borrower No
                                    cols.RelativeColumn(1.5f);   // Borrower Name
                                    cols.RelativeColumn(1.0f);   // Guarantor No
                                    cols.RelativeColumn(1.5f);   // Guarantor Name
                                    cols.RelativeColumn(1.0f);   // ID No
                                    cols.RelativeColumn(1.2f);   // Phone
                                    cols.RelativeColumn(1.0f);   // Guarantee Amount
                                    cols.RelativeColumn(1.0f);   // Outstanding Balance
                                    cols.RelativeColumn(0.8f);   // Status
                                    cols.RelativeColumn(1.0f);   // Assigned Date
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Borrower No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Borrower Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantor No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantor Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantee Amt").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Outstanding").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Assigned Date").Bold().FontSize(8);
                                });

                                foreach (var item in reportData)
                                {
                                    string status = item.Transfered ? "Released" : "Active";

                                    table.Cell().Border(0.2f).Padding(4).Text(item.LoanNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.MemberName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.GuarantorMemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.GuarantorName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.GuarantorIdNo ?? "-").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.GuarantorPhone ?? "-").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.GuaranteeAmount:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{(item.OutstandingBalance ?? 0):N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(status).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.AssignedDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                }

                                // Totals row
                                table.Cell().ColumnSpan(7).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(g => g.GuaranteeAmount):N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(g => g.OutstandingBalance ?? 0):N0}").Bold().FontSize(9);
                                table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4);
                            });
                        });

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
                return File(content, "application/pdf", $"AllGuarantors_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting all guarantors to PDF");
                TempData["Error"] = $"Error exporting to PDF: {ex.Message}";
                return RedirectToAction("AllGuarantorsReport");
            }
        }

        #endregion
    }
}