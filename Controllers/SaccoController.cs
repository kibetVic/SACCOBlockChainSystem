using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class SaccoController : Controller
    {
        private readonly ISaccoService _saccoService;
        private WalletService _walletService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<SaccoController> _logger;

        public SaccoController(
            ISaccoService saccoService,
            WalletService walletService,
            ICompanyContextService companyContextService,
            ILogger<SaccoController> logger)
        {
            _saccoService = saccoService;
            _walletService = walletService;
            _companyContextService = companyContextService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var parametersList = await _saccoService.GetAllSaccoParametersAsync(companyCode);
                var glAccounts = await _saccoService.GetGlAccountsForDropdownAsync(companyCode);

                // Get existing parameter if it exists
                var existingParam = await _saccoService.GetSaccoParametersAsync(companyCode);
                var walletsParam = await _walletService.GetConfigurations(companyCode);

                ViewBag.ParametersList = parametersList;
                ViewBag.GlAccounts = glAccounts;
                ViewBag.IsEdit = existingParam != null && existingParam.Id > 0;
                ViewBag.EditId = existingParam?.Id;

                // Create model with existing data if available, otherwise use defaults
                var model = new SaccoParramDTO
                {
                    WalletConfig = walletsParam ?? new WalletConfig(),
                    CompanyCode = companyCode,
                    Id = existingParam?.Id ?? 0,
                    SaccoName = existingParam?.SaccoName ?? "",
                    NoOfEmployees = existingParam?.NoOfEmployees,
                    Address = existingParam?.Address,
                    Town = existingParam?.Town,
                    Telephone = existingParam?.Telephone,
                    Fax = existingParam?.Fax,
                    EmailAddress = existingParam?.EmailAddress,
                    Website = existingParam?.Website,
                    PhysicalAddress = existingParam?.PhysicalAddress,
                    CheckOffDate = existingParam?.CheckOffDate,
                    MembershipMaturityMonths = existingParam?.MembershipMaturityMonths ?? 3,
                    WithdrawalNoticeDays = existingParam?.WithdrawalNoticeDays ?? 30,
                    DividendProcessingDays = existingParam?.DividendProcessingDays ?? 14,
                    MaxGuarantor = existingParam?.MaxGuarantor ?? 5,
                    MinGuarantor = existingParam?.MinGuarantor ?? 1,
                    DefaultCurrency = existingParam?.DefaultCurrency ?? "KES",
                    DefaultRounding = existingParam?.DefaultRounding ?? 2,
                    SignificantLoanBalance = existingParam?.SignificantLoanBalance,
                    ActionOnDefaultedInterest = existingParam?.ActionOnDefaultedInterest,
                    Suspense = existingParam?.Suspense,
                    RetainedEarnings = existingParam?.RetainedEarnings,
                    Creditors = existingParam?.Creditors
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SACCO parameters");
                TempData["ErrorMessage"] = "Error loading SACCO parameters";
                return View(new SaccoParramDTO());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var parameters = await _saccoService.GetSaccoParametersByIdAsync(id);
                if (parameters == null)
                {
                    TempData["ErrorMessage"] = "SACCO parameters not found";
                    return RedirectToAction("Index");
                }

                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var parametersList = await _saccoService.GetAllSaccoParametersAsync(companyCode);
                var glAccounts = await _saccoService.GetGlAccountsForDropdownAsync(companyCode);

                ViewBag.ParametersList = parametersList;
                ViewBag.GlAccounts = glAccounts;
                ViewBag.IsEdit = true;
                ViewBag.EditId = id;

                var model = new SaccoParramDTO
                {
                    Id = parameters.Id,
                    SaccoName = parameters.SaccoName,
                    CompanyCode = parameters.CompanyCode,
                    NoOfEmployees = parameters.NoOfEmployees,
                    Address = parameters.Address,
                    Town = parameters.Town,
                    Telephone = parameters.Telephone,
                    Fax = parameters.Fax,
                    EmailAddress = parameters.EmailAddress,
                    Website = parameters.Website,
                    PhysicalAddress = parameters.PhysicalAddress,
                    CheckOffDate = parameters.CheckOffDate,
                    MembershipMaturityMonths = parameters.MembershipMaturityMonths,
                    WithdrawalNoticeDays = parameters.WithdrawalNoticeDays,
                    DividendProcessingDays = parameters.DividendProcessingDays,
                    MaxGuarantor = parameters.MaxGuarantor,
                    MinGuarantor = parameters.MinGuarantor,
                    DefaultCurrency = parameters.DefaultCurrency,
                    DefaultRounding = parameters.DefaultRounding,
                    SignificantLoanBalance = parameters.SignificantLoanBalance,
                    ActionOnDefaultedInterest = parameters.ActionOnDefaultedInterest,
                    Suspense = parameters.Suspense,
                    RetainedEarnings = parameters.RetainedEarnings,
                    Creditors = parameters.Creditors
                };

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading edit form");
                TempData["ErrorMessage"] = "Error loading edit form";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SaccoParramDTO model)
        {
            try
            {
                // DEBUG: Log all incoming data
                _logger.LogInformation("=== SAVE METHOD STARTED ===");
                _logger.LogInformation($"Model Id: {model.Id}");
                _logger.LogInformation($"Model SaccoName: {model.SaccoName}");
                _logger.LogInformation($"Model CompanyCode: {model.CompanyCode}");
                _logger.LogInformation($"Model Telephone: {model.Telephone}");
                _logger.LogInformation($"Model EmailAddress: {model.EmailAddress}");
                _logger.LogInformation($"Model PhysicalAddress: {model.PhysicalAddress}");
                _logger.LogInformation($"Model Town: {model.Town}");
                _logger.LogInformation($"Model Address: {model.Address}");
                _logger.LogInformation($"Model MembershipMaturityMonths: {model.MembershipMaturityMonths}");
                _logger.LogInformation($"Model WithdrawalNoticeDays: {model.WithdrawalNoticeDays}");
                _logger.LogInformation($"Model MaxGuarantor: {model.MaxGuarantor}");
                _logger.LogInformation($"Model Suspense: {model.Suspense}");
                _logger.LogInformation($"Model RetainedEarnings: {model.RetainedEarnings}");
                _logger.LogInformation($"Model Creditors: {model.Creditors}");

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning($"ModelState invalid: {errors}");

                    var companyCode = _companyContextService.GetCurrentCompanyCode();
                    var parametersList = await _saccoService.GetAllSaccoParametersAsync(companyCode);
                    var glAccounts = await _saccoService.GetGlAccountsForDropdownAsync(companyCode);

                    ViewBag.ParametersList = parametersList;
                    ViewBag.GlAccounts = glAccounts;
                    ViewBag.IsEdit = model.Id > 0;
                    return View("Index", model);
                }

                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Ensure CompanyCode is set correctly
                if (string.IsNullOrEmpty(model.CompanyCode))
                {
                    model.CompanyCode = currentCompanyCode;
                }

                // Get existing parameters for this company
                var existing = await _saccoService.GetSaccoParametersAsync(currentCompanyCode);

                _logger.LogInformation($"Existing parameters found: {(existing != null ? $"Yes (ID: {existing.Id})" : "No")}");

                if (existing != null && existing.Id > 0)
                {
                    // UPDATE existing record
                    _logger.LogInformation($"Updating existing SACCO parameters with ID: {existing.Id}");
                    model.Id = existing.Id; // Ensure the ID is set
                    await _saccoService.UpdateSaccoParametersAsync(model, User.Identity?.Name ?? "SYSTEM");
                    TempData["SuccessMessage"] = $"SACCO parameters for {model.SaccoName} updated successfully!";

                    // Update Wallet Config
                    if (model.WalletConfig != null)
                    {
                        await _walletService.UpdateWalletConfig(model.WalletConfig, currentCompanyCode);
                    }
                }
                else
                {
                    // CREATE new record
                    _logger.LogInformation($"Creating new SACCO parameters for company: {currentCompanyCode}");
                    await _saccoService.CreateSaccoParametersAsync(model, User.Identity?.Name ?? "SYSTEM");
                    TempData["SuccessMessage"] = $"SACCO parameters for {model.SaccoName} created successfully!";

                    // Create Wallet Config for new record
                    if (model.WalletConfig != null)
                    {
                        await _walletService.UpdateWalletConfig(model.WalletConfig, currentCompanyCode);
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SACCO parameters");
                TempData["ErrorMessage"] = $"Error saving: {ex.Message}";

                // Reload data for the view
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var parametersList = await _saccoService.GetAllSaccoParametersAsync(companyCode);
                var glAccounts = await _saccoService.GetGlAccountsForDropdownAsync(companyCode);

                ViewBag.ParametersList = parametersList;
                ViewBag.GlAccounts = glAccounts;
                ViewBag.IsEdit = model.Id > 0;

                return View("Index", model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _saccoService.DeleteSaccoParametersAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "SACCO parameters deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "SACCO parameters not found";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SACCO parameters");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DebugSave(SaccoParramDTO model)
        {
            // Log all model properties for debugging
            _logger.LogInformation("=== DEBUG: SaccoParramDTO Data ===");
            _logger.LogInformation($"Id: {model.Id}");
            _logger.LogInformation($"SaccoName: {model.SaccoName}");
            _logger.LogInformation($"CompanyCode: {model.CompanyCode}");
            _logger.LogInformation($"NoOfEmployees: {model.NoOfEmployees}");
            _logger.LogInformation($"Address: {model.Address}");
            _logger.LogInformation($"Town: {model.Town}");
            _logger.LogInformation($"Telephone: {model.Telephone}");
            _logger.LogInformation($"Fax: {model.Fax}");
            _logger.LogInformation($"EmailAddress: {model.EmailAddress}");
            _logger.LogInformation($"Website: {model.Website}");
            _logger.LogInformation($"PhysicalAddress: {model.PhysicalAddress}");
            _logger.LogInformation($"CheckOffDate: {model.CheckOffDate}");
            _logger.LogInformation($"MembershipMaturityMonths: {model.MembershipMaturityMonths}");
            _logger.LogInformation($"WithdrawalNoticeDays: {model.WithdrawalNoticeDays}");
            _logger.LogInformation($"DividendProcessingDays: {model.DividendProcessingDays}");
            _logger.LogInformation($"MaxGuarantor: {model.MaxGuarantor}");
            _logger.LogInformation($"MinGuarantor: {model.MinGuarantor}");
            _logger.LogInformation($"DefaultCurrency: {model.DefaultCurrency}");
            _logger.LogInformation($"DefaultRounding: {model.DefaultRounding}");
            _logger.LogInformation($"SignificantLoanBalance: {model.SignificantLoanBalance}");
            _logger.LogInformation($"ActionOnDefaultedInterest: {model.ActionOnDefaultedInterest}");
            _logger.LogInformation($"Suspense: {model.Suspense}");
            _logger.LogInformation($"RetainedEarnings: {model.RetainedEarnings}");
            _logger.LogInformation($"Creditors: {model.Creditors}");
            _logger.LogInformation("=== END DEBUG ===");

            return Json(new { success = true, data = model });
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var parameters = await _saccoService.GetSaccoParametersByIdAsync(id);
                if (parameters == null)
                {
                    return Json(new { success = false, message = "Parameters not found" });
                }

                return Json(new
                {
                    success = true,
                    parameter = new
                    {
                        parameters.Id,
                        parameters.SaccoName,
                        parameters.CompanyCode,
                        parameters.NoOfEmployees,
                        parameters.Address,
                        parameters.Town,
                        parameters.Telephone,
                        parameters.Fax,
                        parameters.EmailAddress,
                        parameters.Website,
                        parameters.PhysicalAddress,
                        CheckOffDate = parameters.CheckOffDate?.ToString("dd/MM/yyyy"),
                        parameters.MembershipMaturityMonths,
                        parameters.WithdrawalNoticeDays,
                        parameters.DividendProcessingDays,
                        parameters.MaxGuarantor,
                        parameters.MinGuarantor,
                        parameters.DefaultCurrency,
                        parameters.DefaultRounding,
                        parameters.SignificantLoanBalance,
                        parameters.ActionOnDefaultedInterest,
                        parameters.Suspense,
                        parameters.RetainedEarnings,
                        parameters.Creditors,
                        CreatedAt = parameters.CreatedAt?.ToString("dd/MM/yyyy HH:mm"),
                        parameters.CreatedBy,
                        UpdatedAt = parameters.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                        parameters.UpdatedBy,
                        parameters.BlockchainTxId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting parameters details");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}