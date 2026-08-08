// Controllers/EmployeePaymentController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;
using ClosedXML.Excel;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SACCOBlockChainSystem.Controllers
{
    public class EmployeePaymentController : Controller
    {
        private readonly IEmployeePaymentService _paymentService;
        private readonly IAgentService _agentService;
        private readonly ILogger<EmployeePaymentController> _logger;
        private readonly ApplicationDbContext _context;

        public EmployeePaymentController(
            IEmployeePaymentService paymentService,
            IAgentService agentService,
            ILogger<EmployeePaymentController> logger,
            ApplicationDbContext context)
        {
            _paymentService = paymentService;
            _agentService = agentService;
            _logger = logger;
            _context = context;
        }

        // Helper method to get current username
        private async Task<string> GetCurrentUsernameAsync()
        {
            // Try to get username from claims
            var username = User.FindFirst(ClaimTypes.Name)?.Value ??
                          User.FindFirst("Username")?.Value ??
                          User.FindFirst("Email")?.Value ??
                          User.Identity?.Name;

            if (!string.IsNullOrEmpty(username))
            {
                return username;
            }

            // If username not found in claims, try to get from UserAccounts1 by ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var userAccount = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserId == userId);
                if (userAccount != null)
                {
                    return userAccount.UserName ?? userAccount.UserLoginId ?? "SYSTEM";
                }
            }

            return "SYSTEM";
        }

        // GET: EmployeePayment
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string paymentType = "all")
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var companyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

            var payments = await _paymentService.GetAllPaymentsAsync(companyCode);
            var summary = await _paymentService.GetPaymentSummaryAsync(companyCode, fromDate, toDate);

            // Filter by payment type if specified
            if (!string.IsNullOrEmpty(paymentType) && paymentType != "all")
            {
                payments = payments.Where(p => p.PaymentTypeCode == paymentType || p.PaymentType == paymentType).ToList();
            }

            // Get all payment types for filter dropdown
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Summary = summary;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CompanyName = companyName;
            ViewBag.PaymentTypes = new SelectList(paymentTypes, "Code", "Name");
            ViewBag.SelectedPaymentType = paymentType;

            return View(payments);
        }

        // GET: EmployeePayment/Create
        public async Task<IActionResult> Create()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "Amtech";

            var paymentTypes = await _paymentService.GetPaymentTypesAsync(companyCode);
            var employees = await _agentService.GetAgentsForDropdownAsync(companyCode);
            var expenseAccounts = await _paymentService.GetAvailableExpenseAccountsAsync(companyCode);
            var cashAccounts = await _paymentService.GetAvailableCashAccountsAsync(companyCode);

            ViewBag.PaymentTypes = new SelectList(paymentTypes, "Id", "Name");
            ViewBag.Employees = new SelectList(employees, "Id", "Names", "IdNo");
            ViewBag.ExpenseAccounts = new SelectList(expenseAccounts, "Key", "Value");
            ViewBag.CashAccounts = new SelectList(cashAccounts, "Key", "Value");

            return PartialView("_CreateModal", new EmployeePaymentDTO
            {
                PaymentDate = DateTime.Today
            });
        }

        // POST: EmployeePayment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] EmployeePaymentDTO dto)
        {
            try
            {
                // Get username instead of user ID
                var userName = await GetCurrentUsernameAsync();
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

                var employee = await _agentService.GetByIdAsync(dto.EmployeeId);
                if (employee != null)
                {
                    dto.EmployeeIdNo = employee.IdNo;
                    dto.EmployeeName = employee.Names;
                }

                var result = await _paymentService.ProcessPaymentAsync(dto, userName, companyCode);

                // Return success with payment ID and receipt URL
                return Json(new
                {
                    success = true,
                    message = $"Payment of {result.Amount:C} processed successfully. Voucher: {result.JournalVoucherNo}",
                    paymentId = result.PaymentId,
                    voucherNo = result.JournalVoucherNo,
                    receiptUrl = Url.Action("PrintEmployeePaymentReceipt", "EmployeePayment", new { id = result.PaymentId })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: EmployeePayment/PrintEmployeePaymentReceipt
        [HttpGet]
        public async Task<IActionResult> PrintEmployeePaymentReceipt(long id)
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

                // Get company details from database
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var companyName = company?.CompanyName ?? sacco?.SaccoName ?? "SACCO System";
                var companyAddress = company?.Address ?? sacco?.PhysicalAddress ?? "P.O. Box 12345 - 00100, Nairobi, Kenya";
                var companyPhone = company?.Telephone ?? sacco?.Telephone ?? "+254 700 000 000";
                var companyEmail = company?.Email ?? sacco?.EmailAddress ?? "info@sacco.co.ke";

                // Get payment details
                var payment = await _paymentService.GetPaymentByIdAsync(id);
                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment receipt not found.";
                    return RedirectToAction("Index");
                }

                // Get employee details
                var employee = await _agentService.GetByIdAsync(payment.EmployeeId);

                // Build receipt view model
                var receiptViewModel = new EmployeePaymentReceiptViewModel
                {
                    // Receipt Details
                    ReceiptNo = payment.JournalVoucherNo,
                    VoucherNo = payment.JournalVoucherNo,
                    TransactionNo = payment.ReferenceNo ?? $"TXN-{DateTime.Now:yyyyMMddHHmmss}",

                    // Employee Details
                    EmployeeIdNo = payment.EmployeeIdNo,
                    EmployeeName = payment.EmployeeName,
                    EmployeePhone = employee?.MobileNo,
                    EmployeeEmail = employee?.StaffCode,

                    // Payment Details
                    Amount = payment.Amount,
                    PaymentType = payment.PaymentType,
                    PaymentTypeCode = payment.PaymentTypeCode,
                    PaymentDate = payment.PaymentDate,
                    PaymentMethod = payment.PaymentMethod ?? "Bank Transfer",
                    ChequeNo = null,
                    Description = payment.Description,
                    Remarks = null,

                    // Account Details
                    ExpenseAccountNo = payment.ExpenseAccountNo,
                    ExpenseAccountName = payment.ExpenseAccountName,
                    CashAccountNo = payment.CashAccountNo,
                    CashAccountName = payment.CashAccountName,

                    // Status
                    Status = payment.Status,

                    // Blockchain
                    BlockchainTxId = payment.BlockchainTxId,

                    // Created By
                    CreatedBy = payment.CreatedBy,
                    CreatedDate = DateTime.Now,

                    // Company Details - from database
                    CompanyName = companyName,
                    CompanyAddress = companyAddress,
                    CompanyPhone = companyPhone,
                    CompanyEmail = companyEmail
                };

                return View("PrintEmployeePaymentReceipt", receiptViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing employee payment receipt for ID: {id}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: EmployeePayment/PrintAndAutoPrintEmployeeReceipt
        [HttpGet]
        public async Task<IActionResult> PrintAndAutoPrintEmployeeReceipt(long id)
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

                // Get company details from database
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                var companyName = company?.CompanyName ?? sacco?.SaccoName ?? "SACCO System";
                var companyAddress = company?.Address ?? sacco?.PhysicalAddress ?? "P.O. Box 12345 - 00100, Nairobi, Kenya";
                var companyPhone = company?.Telephone ?? sacco?.Telephone ?? "+254 700 000 000";
                var companyEmail = company?.Email ?? sacco?.EmailAddress ?? "info@sacco.co.ke";

                // Get payment details
                var payment = await _paymentService.GetPaymentByIdAsync(id);
                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment receipt not found.";
                    return RedirectToAction("Index");
                }

                // Get employee details
                var employee = await _agentService.GetByIdAsync(payment.EmployeeId);

                // Build receipt view model
                var receiptViewModel = new EmployeePaymentReceiptViewModel
                {
                    // Receipt Details
                    ReceiptNo = payment.JournalVoucherNo,
                    VoucherNo = payment.JournalVoucherNo,
                    TransactionNo = payment.ReferenceNo ?? $"TXN-{DateTime.Now:yyyyMMddHHmmss}",

                    // Employee Details
                    EmployeeIdNo = payment.EmployeeIdNo,
                    EmployeeName = payment.EmployeeName,
                    EmployeePhone = employee?.MobileNo,
                    EmployeeEmail = employee?.StaffCode,

                    // Payment Details
                    Amount = payment.Amount,
                    PaymentType = payment.PaymentType,
                    PaymentTypeCode = payment.PaymentTypeCode,
                    PaymentDate = payment.PaymentDate,
                    PaymentMethod = payment.PaymentMethod ?? "Bank Transfer",
                    ChequeNo = null,
                    Description = payment.Description,
                    Remarks = null,

                    // Account Details
                    ExpenseAccountNo = payment.ExpenseAccountNo,
                    ExpenseAccountName = payment.ExpenseAccountName,
                    CashAccountNo = payment.CashAccountNo,
                    CashAccountName = payment.CashAccountName,

                    // Status
                    Status = payment.Status,

                    // Blockchain
                    BlockchainTxId = payment.BlockchainTxId,

                    // Created By
                    CreatedBy = payment.CreatedBy,
                    CreatedDate = DateTime.Now,

                    // Company Details - from database
                    CompanyName = companyName,
                    CompanyAddress = companyAddress,
                    CompanyPhone = companyPhone,
                    CompanyEmail = companyEmail
                };

                // Add flag to auto-print
                ViewBag.AutoPrint = true;

                return View("PrintEmployeePaymentReceipt", receiptViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing employee payment receipt for ID: {id}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: EmployeePayment/Details/5
        public async Task<IActionResult> Details(long id)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);
            if (payment == null)
            {
                return NotFound();
            }

            return PartialView("_DetailsModal", payment);
        }

        // POST: EmployeePayment/Reverse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reverse(long id, string reason)
        {
            try
            {
                if (string.IsNullOrEmpty(reason))
                {
                    TempData["ErrorMessage"] = "Reason for reversal is required";
                    return RedirectToAction(nameof(Index));
                }

                // Get username instead of user ID
                var userName = await GetCurrentUsernameAsync();
                var result = await _paymentService.ReversePaymentAsync(id, userName, reason);

                if (result)
                {
                    TempData["SuccessMessage"] = "Payment reversed successfully";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to reverse payment";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reversing payment {id}");
                TempData["ErrorMessage"] = $"Error reversing payment: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: EmployeePayment/GetEmployeeDetails
        [HttpGet]
        public async Task<IActionResult> GetEmployeeDetails(long id)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var employee = await _agentService.GetByIdAsync(id);

            if (employee == null)
            {
                return Json(new { success = false, message = "Employee not found" });
            }

            var totalPaid = await _paymentService.GetEmployeeBalanceAsync(employee.IdNo, companyCode);

            return Json(new
            {
                success = true,
                employee = new
                {
                    id = employee.Id,
                    idNo = employee.IdNo,
                    name = employee.Names,
                    mobileNo = employee.MobileNo,
                    totalPaid = totalPaid
                }
            });
        }

        // GET: EmployeePayment/GetPaymentTypeDefaultAccount
        [HttpGet]
        public async Task<IActionResult> GetPaymentTypeDefaultAccount(int id)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                return Json(new { success = false, message = "Payment type not found" });
            }

            return Json(new
            {
                success = true,
                defaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo ?? "",
                paymentTypeName = paymentType.Name,
                paymentTypeCode = paymentType.Code
            });
        }

        // GET: EmployeePayment/GetExpenseAccounts
        [HttpGet]
        public async Task<IActionResult> GetExpenseAccounts()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var accounts = await _paymentService.GetAvailableExpenseAccountsAsync(companyCode);
            return Json(accounts);
        }

        // GET: EmployeePayment/GetCashAccounts
        [HttpGet]
        public async Task<IActionResult> GetCashAccounts()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var accounts = await _paymentService.GetAvailableCashAccountsAsync(companyCode);
            return Json(accounts);
        }

        // GET: EmployeePayment/GetPaymentSummary
        [HttpGet]
        public async Task<IActionResult> GetPaymentSummary(DateTime? fromDate, DateTime? toDate)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var summary = await _paymentService.GetPaymentSummaryAsync(companyCode, fromDate, toDate);
            return Json(summary);
        }

        // GET: EmployeePayment/Export
        public async Task<IActionResult> Export(DateTime? fromDate, DateTime? toDate)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var payments = await _paymentService.GetAllPaymentsAsync(companyCode);

            if (fromDate.HasValue)
                payments = payments.Where(p => p.PaymentDate >= fromDate.Value).ToList();
            if (toDate.HasValue)
                payments = payments.Where(p => p.PaymentDate <= toDate.Value).ToList();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Voucher No,Employee Name,ID Number,Amount,Payment Type,Payment Type Code,Payment Date,Status,Blockchain Tx ID,Created By");

            foreach (var payment in payments)
            {
                csv.AppendLine($"\"{payment.VoucherNo}\",\"{payment.EmployeeName}\",\"{payment.EmployeeIdNo}\",{payment.Amount},\"{payment.PaymentType}\",\"{payment.PaymentTypeCode}\",{payment.PaymentDate:yyyy-MM-dd},\"{payment.Status}\",\"{payment.BlockchainTxId}\",\"{payment.CreatedBy}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Payments_{DateTime.Now:yyyyMMdd}.csv");
        }

        #region Reports

        /// <summary>
        /// GET: Employee Payment Report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PaymentReport(DateTime? fromDate, DateTime? toDate, string paymentType = "all", string employeeId = "")
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var companyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

            // Get all payments with date filter
            var payments = await _paymentService.GetAllPaymentsAsync(companyCode);

            if (fromDate.HasValue)
                payments = payments.Where(p => p.PaymentDate >= fromDate.Value).ToList();
            if (toDate.HasValue)
                payments = payments.Where(p => p.PaymentDate <= toDate.Value).ToList();

            // Filter by payment type
            if (!string.IsNullOrEmpty(paymentType) && paymentType != "all")
            {
                payments = payments.Where(p => p.PaymentTypeCode == paymentType || p.PaymentType == paymentType).ToList();
            }

            // Filter by employee
            if (!string.IsNullOrEmpty(employeeId))
            {
                payments = payments.Where(p => p.EmployeeIdNo == employeeId || p.EmployeeName.Contains(employeeId)).ToList();
            }

            // Get payment types for filter dropdown
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Get employees for filter dropdown
            var employees = await _context.Agents
                .Where(a => a.CompanyCode == companyCode)
                .OrderBy(a => a.Names)
                .Select(a => new { a.Id, a.IdNo, a.Names })
                .ToListAsync();

            // Calculate summary statistics
            var summary = new
            {
                TotalPayments = payments.Count,
                TotalAmount = payments.Sum(p => p.Amount),
                AverageAmount = payments.Any() ? payments.Average(p => p.Amount) : 0,
                MinAmount = payments.Any() ? payments.Min(p => p.Amount) : 0,
                MaxAmount = payments.Any() ? payments.Max(p => p.Amount) : 0,
                ByPaymentType = payments.GroupBy(p => p.PaymentType)
                    .Select(g => new { Type = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
                    .OrderByDescending(g => g.Amount)
                    .ToList()
            };

            ViewBag.Summary = summary;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CompanyName = companyName;
            ViewBag.PaymentTypes = new SelectList(paymentTypes, "Code", "Name");
            ViewBag.SelectedPaymentType = paymentType;
            ViewBag.Employees = new SelectList(employees, "IdNo", "Names");
            ViewBag.SelectedEmployee = employeeId;

            return View("~/Views/Reports/EmployeePaymentReport.cshtml", payments);
        }

        /// <summary>
        /// POST: Export Employee Payment Report to Excel
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportPaymentReportToExcel(DateTime? fromDate, DateTime? toDate, string paymentType = "all", string employeeId = "")
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
                var companyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";

                // Get payments with filters
                var payments = await _paymentService.GetAllPaymentsAsync(companyCode);

                if (fromDate.HasValue)
                    payments = payments.Where(p => p.PaymentDate >= fromDate.Value).ToList();
                if (toDate.HasValue)
                    payments = payments.Where(p => p.PaymentDate <= toDate.Value).ToList();

                if (!string.IsNullOrEmpty(paymentType) && paymentType != "all")
                {
                    payments = payments.Where(p => p.PaymentTypeCode == paymentType || p.PaymentType == paymentType).ToList();
                }

                if (!string.IsNullOrEmpty(employeeId))
                {
                    payments = payments.Where(p => p.EmployeeIdNo == employeeId || p.EmployeeName.Contains(employeeId)).ToList();
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Employee Payment Report");
                    int currentRow = 1;

                    // Header
                    worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                    worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Report Title
                    string dateRange = "ALL TIME";
                    if (fromDate.HasValue && toDate.HasValue)
                        dateRange = $"{fromDate.Value:dd/MM/yyyy} - {toDate.Value:dd/MM/yyyy}";
                    else if (fromDate.HasValue)
                        dateRange = $"From {fromDate.Value:dd/MM/yyyy}";
                    else if (toDate.HasValue)
                        dateRange = $"Up to {toDate.Value:dd/MM/yyyy}";

                    worksheet.Cell(currentRow, 1).Value = $"EMPLOYEE PAYMENT REPORT - {dateRange}";
                    worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Statistics
                    worksheet.Cell(currentRow, 1).Value = "TOTAL PAYMENTS:";
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 2).Value = payments.Count;
                    worksheet.Cell(currentRow, 2).Style.Font.SetBold();

                    worksheet.Cell(currentRow, 4).Value = "TOTAL AMOUNT:";
                    worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 5).Value = payments.Sum(p => p.Amount);
                    worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(currentRow, 7).Value = "AVERAGE AMOUNT:";
                    worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Value = payments.Any() ? payments.Average(p => p.Amount) : 0;
                    worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    currentRow += 2;

                    // Headers - Removed BlockchainTxId and Created By
                    string[] headers = { "Voucher No", "Employee Name", "ID Number", "Amount", "Payment Type", "Payment Date", "Status" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(currentRow, i + 1).Value = headers[i];
                        worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                        worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    }
                    currentRow++;

                    // Data rows
                    foreach (var payment in payments.OrderByDescending(p => p.PaymentDate))
                    {
                        worksheet.Cell(currentRow, 1).Value = payment.VoucherNo;
                        worksheet.Cell(currentRow, 2).Value = payment.EmployeeName;
                        worksheet.Cell(currentRow, 3).Value = payment.EmployeeIdNo;
                        worksheet.Cell(currentRow, 4).Value = payment.Amount;
                        worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 5).Value = payment.PaymentType;
                        worksheet.Cell(currentRow, 6).Value = payment.PaymentDate.ToString("dd/MM/yyyy");
                        worksheet.Cell(currentRow, 7).Value = payment.Status;
                        worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                    }

                    // Grand Total
                    if (payments.Any())
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
                        worksheet.Cell(currentRow, 3).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                        worksheet.Cell(currentRow, 4).Value = payments.Sum(p => p.Amount);
                        worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    }

                    currentRow += 2;
                    worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"EmployeePaymentReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting employee payment report to Excel");
                TempData["ErrorMessage"] = "Error exporting: " + ex.Message;
                return RedirectToAction("PaymentReport");
            }
        }

        /// <summary>
        /// POST: Export Employee Payment Report to PDF
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportPaymentReportToPdf(DateTime? fromDate, DateTime? toDate, string paymentType = "all", string employeeId = "")
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
                var companyName = User.FindFirst("CompanyName")?.Value ?? "AMTECH SACCO";
                var printedBy = User.Identity?.Name ?? "System";

                // Get payments with filters
                var payments = await _paymentService.GetAllPaymentsAsync(companyCode);

                if (fromDate.HasValue)
                    payments = payments.Where(p => p.PaymentDate >= fromDate.Value).ToList();
                if (toDate.HasValue)
                    payments = payments.Where(p => p.PaymentDate <= toDate.Value).ToList();

                if (!string.IsNullOrEmpty(paymentType) && paymentType != "all")
                {
                    payments = payments.Where(p => p.PaymentTypeCode == paymentType || p.PaymentType == paymentType).ToList();
                }

                if (!string.IsNullOrEmpty(employeeId))
                {
                    payments = payments.Where(p => p.EmployeeIdNo == employeeId || p.EmployeeName.Contains(employeeId)).ToList();
                }

                if (!payments.Any())
                {
                    TempData["ErrorMessage"] = "No payments found for the selected filters.";
                    return RedirectToAction("PaymentReport");
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
                            header.Item().AlignCenter().Text("EMPLOYEE PAYMENT REPORT").FontSize(12).Bold();

                            string dateRange = "ALL TIME";
                            if (fromDate.HasValue && toDate.HasValue)
                                dateRange = $"{fromDate.Value:dd/MM/yyyy} - {toDate.Value:dd/MM/yyyy}";
                            else if (fromDate.HasValue)
                                dateRange = $"From {fromDate.Value:dd/MM/yyyy}";
                            else if (toDate.HasValue)
                                dateRange = $"Up to {toDate.Value:dd/MM/yyyy}";

                            header.Item().AlignCenter().Text($"Period: {dateRange}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                var totalAmount = payments.Sum(p => p.Amount);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Payments:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(payments.Count.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalAmount:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Average:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{(payments.Any() ? payments.Average(p => p.Amount) : 0):N0}").FontSize(8);
                            });

                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);

                            // Detailed Table - Removed BlockchainTxId and Created By columns
                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // Voucher No
                                    cols.RelativeColumn(1.8f);   // Employee Name
                                    cols.RelativeColumn(0.8f);   // ID No
                                    cols.RelativeColumn(0.8f);   // Amount
                                    cols.RelativeColumn(1.2f);   // Payment Type
                                    cols.RelativeColumn(0.8f);   // Payment Date
                                    cols.RelativeColumn(0.8f);   // Status
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Voucher No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Employee Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Payment Type").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Payment Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                                });

                                foreach (var payment in payments.OrderByDescending(p => p.PaymentDate))
                                {
                                    table.Cell().Border(0.2f).Padding(4).Text(payment.VoucherNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(payment.EmployeeName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(payment.EmployeeIdNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{payment.Amount:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(payment.PaymentType ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(payment.PaymentDate.ToString("dd/MM/yyyy")).FontSize(8);

                                    string statusColor = payment.Status == "POSTED" ? "#d4edda" : "#fff3cd";
                                    table.Cell().Border(0.2f).Background(statusColor).Padding(4).AlignCenter().Text(payment.Status ?? "").FontSize(8);
                                }

                                // Grand Total Row
                                if (payments.Any())
                                {
                                    table.Cell().ColumnSpan(3).Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(9);
                                    table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{payments.Sum(p => p.Amount):N0}").Bold().FontSize(9);
                                    table.Cell().ColumnSpan(3).Border(0.2f).Background("#f0f0f0").Padding(4);
                                }
                            });

                            // Payment Type Summary
                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                            contentCol.Item().Text("PAYMENT TYPE SUMMARY").FontSize(10).Bold();

                            contentCol.Item().Table(typeTable =>
                            {
                                typeTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                typeTable.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).Text("Payment Type").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Count").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text("Amount").Bold().FontSize(8);
                                });

                                var typeSummary = payments.GroupBy(p => p.PaymentType)
                                    .Select(g => new { Type = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
                                    .OrderByDescending(g => g.Amount);

                                foreach (var item in typeSummary)
                                {
                                    typeTable.Cell().Border(0.2f).Padding(4).Text(item.Type ?? "").FontSize(8);
                                    typeTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.Count.ToString()).FontSize(8);
                                    typeTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Amount:N0}").FontSize(8);
                                }

                                // Total row
                                typeTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).Text("TOTAL").Bold().FontSize(8);
                                typeTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text(payments.Count.ToString()).Bold().FontSize(8);
                                typeTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{payments.Sum(p => p.Amount):N0}").Bold().FontSize(8);
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

                return File(stream.ToArray(), "application/pdf", $"EmployeePaymentReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting employee payment report to PDF");
                TempData["ErrorMessage"] = "Error exporting: " + ex.Message;
                return RedirectToAction("PaymentReport");
            }
        }

        #endregion
    }
}