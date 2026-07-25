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
    public class InvoicePaymentController : Controller
    {
        private readonly IInvoicePaymentService _invoicepaymentService;
        private readonly IInvoiceService _invoiceService;
        private readonly ISupplierService _supplierService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<InvoicePaymentController> _logger;

        public InvoicePaymentController(
            IInvoicePaymentService invoicepaymentService,
            IInvoiceService invoiceService,
            ISupplierService supplierService,
            ICompanyContextService companyContextService,
            ILogger<InvoicePaymentController> logger)
        {
            invoicepaymentService = invoicepaymentService;
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
                var dashboard = await _invoicepaymentService.GetPaymentDashboardAsync(companyCode);

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "SACCO System";

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment index");
                TempData["ErrorMessage"] = "Error loading payments: " + ex.Message;
                return View(new InvoicePaymentViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] InvoicePaymentDTO dto)
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

                var payment = await _invoicepaymentService.CreatePaymentAsync(dto, createdBy);

                return Json(new { success = true, message = $"Payment of KES {payment.Amount:N0} recorded successfully!", payment = payment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] InvoicePaymentDTO dto)
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
                    return Json(new { success = false, message = "Payment ID is required" });
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var updatedBy = User.Identity?.Name ?? "SYSTEM";

                var payment = await _invoicepaymentService.UpdatePaymentAsync(dto.Id.Value, dto, updatedBy);

                return Json(new { success = true, message = $"Payment updated successfully!", payment = payment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var deletedBy = User.Identity?.Name ?? "SYSTEM";
                await _invoicepaymentService.DeletePaymentAsync(id, deletedBy);

                return Json(new { success = true, message = "Payment deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting payment with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPayment(long id)
        {
            try
            {
                var payment = await _invoicepaymentService.GetPaymentByIdAsync(id);

                if (payment == null)
                {
                    return Json(new { success = false, message = "Payment not found" });
                }

                return Json(new { success = true, payment = payment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting payment with ID: {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPaymentsByInvoice(string invoiceNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var payments = await _invoicepaymentService.GetPaymentsByInvoiceAsync(invoiceNo, companyCode);

                return Json(new { success = true, payments = payments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting payments for invoice: {invoiceNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPaymentsBySupplier(string supplierCode)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var payments = await _invoicepaymentService.GetPaymentsBySupplierAsync(supplierCode, companyCode);

                return Json(new { success = true, payments = payments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting payments for supplier: {supplierCode}");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}