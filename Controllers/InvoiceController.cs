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
    public class InvoiceController : Controller
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ISupplierService _supplierService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(
            IInvoiceService invoiceService,
            ISupplierService supplierService,
            ICompanyContextService companyContextService,
            ILogger<InvoiceController> logger)
        {
            _invoiceService = invoiceService;
            _supplierService = supplierService;
            _companyContextService = companyContextService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var dashboard = await _invoiceService.GetInvoiceDashboardAsync(companyCode);

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "SACCO System";

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice index");
                TempData["ErrorMessage"] = "Error loading invoices: " + ex.Message;
                return View(new InvoiceReceiveViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] InvoiceReceiveDTO dto)
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

                var invoice = await _invoiceService.CreateInvoiceAsync(dto, createdBy);

                return Json(new { success = true, message = $"Invoice '{invoice.InvoiceNo}' created successfully!", invoice = invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] InvoiceReceiveDTO dto)
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
                    return Json(new { success = false, message = "Invoice ID is required" });
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var updatedBy = User.Identity?.Name ?? "SYSTEM";

                var invoice = await _invoiceService.UpdateInvoiceAsync(dto.Id.Value, dto, updatedBy);

                return Json(new { success = true, message = $"Invoice '{invoice.InvoiceNo}' updated successfully!", invoice = invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var deletedBy = User.Identity?.Name ?? "SYSTEM";
                await _invoiceService.DeleteInvoiceAsync(id, deletedBy);

                return Json(new { success = true, message = "Invoice deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting invoice with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoice(long id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "Invoice not found" });
                }

                return Json(new { success = true, invoice = invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting invoice with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoiceByNumber(string invoiceNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var invoice = await _invoiceService.GetInvoiceByNumberAsync(invoiceNo, companyCode);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "Invoice not found" });
                }

                return Json(new { success = true, invoice = invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting invoice: {invoiceNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllInvoices()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var invoices = await _invoiceService.GetAllInvoicesAsync(companyCode);

                return Json(new { success = true, invoices = invoices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all invoices");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var searchDto = new InvoiceSearchDTO
                {
                    CompanyCode = companyCode,
                    InvoiceNo = term,
                    SupplierName = term
                };

                var invoices = await _invoiceService.SearchInvoicesAsync(searchDto);

                return Json(new { success = true, invoices = invoices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}