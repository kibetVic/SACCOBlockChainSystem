using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class SupplierController : Controller
    {
        private readonly ISupplierService _supplierService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<SupplierController> _logger;

        public SupplierController(
            ISupplierService supplierService,
            ICompanyContextService companyContextService,
            ILogger<SupplierController> logger)
        {
            _supplierService = supplierService;
            _companyContextService = companyContextService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var dashboard = await _supplierService.GetSupplierDashboardAsync(companyCode);

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "SACCO System";

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading supplier index");
                TempData["ErrorMessage"] = "Error loading suppliers: " + ex.Message;
                return View(new SupplierViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] SupplierDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var createdBy = User.Identity?.Name ?? "SYSTEM";

                var supplier = await _supplierService.CreateSupplierAsync(dto, createdBy);

                return Json(new { success = true, message = $"Supplier '{supplier.SupplierName}' created successfully!", supplier = supplier });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] SupplierDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                if (!dto.Id.HasValue)
                {
                    return Json(new { success = false, message = "Supplier ID is required" });
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var updatedBy = User.Identity?.Name ?? "SYSTEM";

                var supplier = await _supplierService.UpdateSupplierAsync(dto.Id.Value, dto, updatedBy);

                return Json(new { success = true, message = $"Supplier '{supplier.SupplierName}' updated successfully!", supplier = supplier });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var deletedBy = User.Identity?.Name ?? "SYSTEM";
                await _supplierService.DeleteSupplierAsync(id, deletedBy);

                return Json(new { success = true, message = "Supplier deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting supplier with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplier(long id)
        {
            try
            {
                var supplier = await _supplierService.GetSupplierByIdAsync(id);

                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found" });
                }

                return Json(new { success = true, supplier = supplier });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting supplier with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSuppliers()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var suppliers = await _supplierService.GetAllSuppliersAsync(companyCode);

                return Json(new { success = true, suppliers = suppliers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all suppliers");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var searchDto = new SupplierSearchDTO
                {
                    CompanyCode = companyCode,
                    SupplierName = term,
                    SupplierCode = term
                };

                var suppliers = await _supplierService.SearchSuppliersAsync(searchDto);

                return Json(new { success = true, suppliers = suppliers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching suppliers");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGlAccounts()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var glAccounts = await _supplierService.GetGlAccountsForDropdownAsync(companyCode);

                return Json(new { success = true, glAccounts = glAccounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GL accounts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGlAccountDetails(string glAccountNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var glAccount = await _supplierService.GetGlAccountByCodeAsync(glAccountNo, companyCode);

                if (glAccount == null)
                {
                    return Json(new { success = false, message = "GL Account not found" });
                }

                return Json(new { success = true, glAccount = glAccount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting GL account details for: {glAccountNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}