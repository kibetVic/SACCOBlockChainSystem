// Controllers/EmployeePaymentController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

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
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var companyName = User.FindFirst("CompanyName")?.Value ?? "JUHUDI SACCO";

            var payments = await _paymentService.GetAllPaymentsAsync(companyCode);
            var summary = await _paymentService.GetPaymentSummaryAsync(companyCode, fromDate, toDate);

            ViewBag.Summary = summary;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CompanyName = companyName;

            return View(payments);
        }

        // GET: EmployeePayment/Create
        public async Task<IActionResult> Create()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

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

                return Json(new { success = true, message = $"Payment of {result.Amount:C} processed successfully. Voucher: {result.JournalVoucherNo}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return Json(new { success = false, message = ex.Message });
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
    }
}