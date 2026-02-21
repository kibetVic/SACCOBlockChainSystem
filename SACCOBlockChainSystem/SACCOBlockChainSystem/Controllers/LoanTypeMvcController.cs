using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LoanTypeMvcController : Controller
    {
        private readonly ILoanTypeService _loanTypeService;
        private readonly ILogger<LoanTypeMvcController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public LoanTypeMvcController(
            ILoanTypeService loanTypeService,
            ILogger<LoanTypeMvcController> logger,
            ICompanyContextService companyContextService)
        {
            _loanTypeService = loanTypeService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        // GET: /LoanTypeMvc/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanTypes = await _loanTypeService.GetLoanTypesByCompanyAsync(companyCode);

                ViewBag.TotalLoanTypes = loanTypes.Count;
                ViewBag.ActiveLoanTypes = loanTypes.Count(lt => lt.TotalLoans > 0);
                ViewBag.TotalLoans = loanTypes.Sum(lt => lt.TotalLoans);
                ViewBag.TotalLoanAmount = loanTypes.Sum(lt => lt.TotalLoanAmount);

                return View(loanTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan types index");
                return View("Error");
            }
        }

        // GET: /LoanTypeMvc/Create
        public IActionResult Create()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanTypeDto = new LoanTypeCreateDTO
                {
                    CompanyCode = companyCode,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM",
                    Priority = 1,
                    GracePeriod = 0,
                    Penalty = false,
                    Bridging = false,
                    SelfGuarantee = false,
                    MobileLoan = false,
                    Ppacc = "200000", // Default PP account
                    ContraAccount = "100000" // Default contra account
                };

                return View(loanTypeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create loan type form");
                return View("Error");
            }
        }

        // POST: /LoanTypeMvc/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanTypeCreateDTO loanTypeDto)
        {
            try
            {
                _logger.LogInformation("Creating loan type");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for loan type creation");
                    return View(loanTypeDto);
                }

                // Set user information
                loanTypeDto.CompanyCode = GetUserCompanyCode();
                loanTypeDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanTypeService.CreateLoanTypeAsync(loanTypeDto);

                TempData["SuccessMessage"] = $"Loan type '{result.LoanType}' created successfully!";
                return RedirectToAction("Details", new { loanCode = result.LoanCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating loan type");

                if (ex.Message.Contains("already exists") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("required"))
                {
                    ModelState.AddModelError("", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                return View(loanTypeDto);
            }
        }

        // GET: /LoanTypeMvc/Edit/{loanCode}
        public async Task<IActionResult> Edit(string loanCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loanCode, companyCode);

                // Convert to UpdateDTO
                var updateDto = new LoanTypeUpdateDTO
                {
                    LoanType = loanType.LoanType,
                    ValueChain = loanType.ValueChain,
                    LoanProduct = loanType.LoanProduct,
                    LoanAcc = loanType.LoanAcc,
                    InterestAcc = loanType.InterestAcc,
                    PenaltyAcc = loanType.PenaltyAcc,
                    RepayPeriod = loanType.RepayPeriod,
                    Interest = loanType.Interest,
                    MaxAmount = loanType.MaxAmount,
                    Guarantor = loanType.Guarantor,
                    UseIntRange = loanType.UseIntRange,
                    EarningRatio = loanType.EarningRatio,
                    Penalty = loanType.Penalty,
                    ProcessingFee = loanType.ProcessingFee,
                    GracePeriod = loanType.GracePeriod,
                    RepayMethod = loanType.RepayMethod,
                    Bridging = loanType.Bridging,
                    SelfGuarantee = loanType.SelfGuarantee,
                    MobileLoan = loanType.MobileLoan,
                    Ppacc = loanType.Ppacc,
                    ContraAccount = loanType.ContraAccount,
                    Priority = loanType.Priority,
                    MaxLoans = loanType.MaxLoans,
                    CompanyCode = loanType.CompanyCode,
                    UpdatedBy = User.Identity?.Name ?? "SYSTEM"
                };

                ViewBag.LoanCode = loanCode;
                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for loan type {loanCode}");
                return View("Error");
            }
        }

        // POST: /LoanTypeMvc/Edit/{loanCode}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string loanCode, LoanTypeUpdateDTO loanTypeDto)
        {
            try
            {
                _logger.LogInformation($"Updating loan type: {loanCode}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for loan type update");
                    ViewBag.LoanCode = loanCode;
                    return View(loanTypeDto);
                }

                loanTypeDto.CompanyCode = GetUserCompanyCode();
                loanTypeDto.UpdatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanTypeService.UpdateLoanTypeAsync(loanCode, loanTypeDto);

                TempData["SuccessMessage"] = $"Loan type '{result.LoanType}' updated successfully!";
                return RedirectToAction("Details", new { loanCode = result.LoanCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan type {loanCode}");

                if (ex.Message.Contains("not found") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("Cannot change") ||
                    ex.Message.Contains("in use"))
                {
                    ModelState.AddModelError("", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                ViewBag.LoanCode = loanCode;
                return View(loanTypeDto);
            }
        }

        // GET: /LoanTypeMvc/Details/{loanCode}
        public async Task<IActionResult> Details(string loanCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loanCode, companyCode);

                return View(loanType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan type details {loanCode}");
                return View("Error");
            }
        }

        // GET: /LoanTypeMvc/Delete/{loanCode}
        public async Task<IActionResult> Delete(string loanCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loanCode, companyCode);
                var usageCount = await _loanTypeService.GetLoanTypeUsageCountAsync(loanCode, companyCode);

                ViewBag.UsageCount = usageCount;
                ViewBag.CanDelete = usageCount == 0;

                return View(loanType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading delete confirmation for loan type {loanCode}");
                return View("Error");
            }
        }

        // POST: /LoanTypeMvc/Delete/{loanCode}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string loanCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                await _loanTypeService.DeleteLoanTypeAsync(loanCode, companyCode);

                TempData["SuccessMessage"] = $"Loan type '{loanCode}' deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting loan type {loanCode}");

                if (ex.Message.Contains("in use") || ex.Message.Contains("not found"))
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                }

                return RedirectToAction("Delete", new { loanCode });
            }
        }

        // GET: /LoanTypeMvc/Search
        public IActionResult Search()
        {
            return View();
        }

        // GET: /LoanTypeMvc/SearchResults
        [HttpGet]
        public async Task<IActionResult> SearchResults(string searchTerm)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanTypes = await _loanTypeService.SearchLoanTypesAsync(searchTerm, companyCode);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.ResultCount = loanTypes.Count;

                return View(loanTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching loan types with term: {searchTerm}");
                return View("Error");
            }
        }

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
    }
}