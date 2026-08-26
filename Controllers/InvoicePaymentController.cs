using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
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
        private readonly ApplicationDbContext _context;

        public InvoicePaymentController(
            IInvoicePaymentService invoicepaymentService,
            IInvoiceService invoiceService,
            ISupplierService supplierService,
            ICompanyContextService companyContextService,
            ApplicationDbContext context,
            ILogger<InvoicePaymentController> logger)
        {
            _invoicepaymentService = invoicepaymentService;
            _invoiceService = invoiceService;
            _supplierService = supplierService;
            _companyContextService = companyContextService;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                if (string.IsNullOrEmpty(companyCode))
                {
                    TempData["ErrorMessage"] = "Company code not found. Please log in again.";
                    return View(new InvoicePaymentViewModel());
                }

                var dashboard = await _invoicepaymentService.GetPaymentDashboardAsync(companyCode);
                if (dashboard == null)
                {
                    dashboard = new InvoicePaymentViewModel();
                }

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

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

                // Return success with receipt URL
                return Json(new
                {
                    success = true,
                    message = $"Payment of KES {payment.Amount:N0} recorded successfully! Receipt: {payment.ReceiptNo}",
                    payment = payment,
                    receiptUrl = Url.Action("PrintPaymentReceipt", new { receiptNo = payment.ReceiptNo })
                });
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

                // Return success with receipt URL
                return Json(new
                {
                    success = true,
                    message = $"Payment updated successfully!",
                    payment = payment,
                    receiptUrl = Url.Action("PrintPaymentReceipt", new { receiptNo = payment.ReceiptNo })
                });
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

        [HttpGet]
        public async Task<IActionResult> GetGlAccounts()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var glAccounts = await _invoicepaymentService.GetGlAccountsForDropdownAsync(companyCode);

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
                var glAccount = await _invoicepaymentService.GetGlAccountByCodeAsync(glAccountNo, companyCode);

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



        /// <summary>
        /// Print Payment Receipt - Called after successful payment
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintPaymentReceipt(string receiptNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                // Get payment details
                var payment = await _invoicepaymentService.GetPaymentByReceiptNoAsync(receiptNo, companyCode);

                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment receipt not found.";
                    return RedirectToAction("Index");
                }

                // Get supplier details
                var supplier = await _supplierService.GetSupplierByCodeAsync(payment.SupplierId, companyCode);

                // Get invoice details with items
                var invoice = await _invoiceService.GetInvoiceByNumberAsync(payment.InvoiceNo, companyCode);

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var companyName = company?.CompanyName ?? sacco?.SaccoName ?? "SACCO System";
                var companyAddress = company?.Address ?? sacco?.PhysicalAddress ?? "P.O. Box 12345 - 00100, Nairobi, Kenya";
                var companyPhone = company?.Telephone ?? sacco?.Telephone ?? "+254 700 000 000";
                var companyEmail = company?.Email ?? sacco?.EmailAddress ?? "info@sacco.co.ke";

                // Build receipt view model
                var receiptViewModel = new PaymentReceiptViewModel
                {
                    ReceiptNo = payment.ReceiptNo,
                    InvoiceNo = payment.InvoiceNo,
                    SupplierCode = payment.SupplierId,
                    SupplierName = payment.SupplierName ?? supplier?.SupplierName,
                    SupplierContact = supplier?.ContactPerson,
                    SupplierPhone = supplier?.PhoneNo,
                    SupplierEmail = supplier?.Email,
                    Amount = payment.Amount,
                    OpeningBalance = payment.OpeningBalance,
                    BalanceAfter = (payment.OpeningBalance ?? 0) - payment.Amount,
                    PaymentDate = payment.TransDate ?? DateTime.Now,
                    PaymentMethod = string.IsNullOrEmpty(payment.ChequeNo) ? "Bank Transfer" : "Cheque",
                    ChequeNo = payment.ChequeNo,
                    TransactionType = payment.Transtype ?? "Payment",
                    Particulars = payment.Particulars,
                    Remarks = payment.Remarks,
                    DebitAccountNo = payment.DebitAccno,
                    DebitAccountName = payment.DebitAccName,
                    SupplierAccountNo = payment.SupplierAccno,
                    SupplierAccountName = payment.SupplierAccName,
                    BlockchainTxId = payment.BlockchainTxId,
                    CreatedBy = payment.CreatedBy,
                    CreatedDate = DateTime.Now,
                    PaymentStatus = invoice?.PaymentStatus ?? "Confirmed",
                    TotalPaid = invoice?.AmountPaid ?? payment.Amount,
                    CompanyName = companyName,
                    CompanyAddress = companyAddress,
                    CompanyPhone = companyPhone,
                    CompanyEmail = companyEmail,
                    // NEW: Invoice Items
                    InvoiceItems = invoice?.InvoiceItems ?? new List<InvoiceItemResponseDTO>(),
                    InvoiceTotal = invoice?.TotalAmount ?? 0
                };

                return View("PrintPaymentReceipt", receiptViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing payment receipt for: {receiptNo}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Print Payment Receipt - Called after successful payment with auto-print
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintAndAutoPrintReceipt(string receiptNo)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();

                // Get payment details
                var payment = await _invoicepaymentService.GetPaymentByReceiptNoAsync(receiptNo, companyCode);

                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment receipt not found.";
                    return RedirectToAction("Index");
                }

                // Get supplier details
                var supplier = await _supplierService.GetSupplierByCodeAsync(payment.SupplierId, companyCode);

                // Get invoice details with items
                var invoice = await _invoiceService.GetInvoiceByNumberAsync(payment.InvoiceNo, companyCode);

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var companyName = company?.CompanyName ?? sacco?.SaccoName ?? "SACCO System";
                var companyAddress = company?.Address ?? sacco?.PhysicalAddress ?? "P.O. Box 12345 - 00100, Nairobi, Kenya";
                var companyPhone = company?.Telephone ?? sacco?.Telephone ?? "+254 700 000 000";
                var companyEmail = company?.Email ?? sacco?.EmailAddress ?? "info@sacco.co.ke";

                // Build receipt view model
                var receiptViewModel = new PaymentReceiptViewModel
                {
                    ReceiptNo = payment.ReceiptNo,
                    InvoiceNo = payment.InvoiceNo,
                    SupplierCode = payment.SupplierId,
                    SupplierName = payment.SupplierName ?? supplier?.SupplierName,
                    SupplierContact = supplier?.ContactPerson,
                    SupplierPhone = supplier?.PhoneNo,
                    SupplierEmail = supplier?.Email,
                    Amount = payment.Amount,
                    OpeningBalance = payment.OpeningBalance,
                    BalanceAfter = (payment.OpeningBalance ?? 0) - payment.Amount,
                    PaymentDate = payment.TransDate ?? DateTime.Now,
                    PaymentMethod = string.IsNullOrEmpty(payment.ChequeNo) ? "Bank Transfer" : "Cheque",
                    ChequeNo = payment.ChequeNo,
                    TransactionType = payment.Transtype ?? "Payment",
                    Particulars = payment.Particulars,
                    Remarks = payment.Remarks,
                    DebitAccountNo = payment.DebitAccno,
                    DebitAccountName = payment.DebitAccName,
                    SupplierAccountNo = payment.SupplierAccno,
                    SupplierAccountName = payment.SupplierAccName,
                    BlockchainTxId = payment.BlockchainTxId,
                    CreatedBy = payment.CreatedBy,
                    CreatedDate = DateTime.Now,
                    PaymentStatus = invoice?.PaymentStatus ?? "Confirmed",
                    TotalPaid = invoice?.AmountPaid ?? payment.Amount,
                    CompanyName = companyName,
                    CompanyAddress = companyAddress,
                    CompanyPhone = companyPhone,
                    CompanyEmail = companyEmail,
                    // NEW: Invoice Items
                    InvoiceItems = invoice?.InvoiceItems ?? new List<InvoiceItemResponseDTO>(),
                    InvoiceTotal = invoice?.TotalAmount ?? 0
                };

                // Add flag to auto-print
                ViewBag.AutoPrint = true;

                return View("PrintPaymentReceipt", receiptViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing payment receipt for: {receiptNo}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}