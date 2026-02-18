// Controllers/GeneralLedgerInquiryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("GLInquiry")]
    public class GeneralLedgerInquiryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GeneralLedgerInquiryController> _logger;

        public GeneralLedgerInquiryController(
            ApplicationDbContext context,
            ILogger<GeneralLedgerInquiryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===============================
        // GET: /GLInquiry
        // ===============================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetCompanyCode();

                var model = new GeneralLedgerInquiryViewModel
                {
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now,
                    Accounts = await GetActiveAccounts(companyCode)
                };

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = GetCompanyName();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GL Inquiry page");
                TempData["Error"] = $"Error loading page: {ex.Message}";
                return View(new GeneralLedgerInquiryViewModel());
            }
        }

        // ===============================
        // POST: /GLInquiry/Search
        // ===============================
        [HttpPost("Search")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search([FromForm] GeneralLedgerInquiryViewModel model)
        {
            try
            {
                var companyCode = GetCompanyCode();

                // Validate dates
                if (model.StartDate > model.EndDate)
                {
                    TempData["Error"] = "Start date cannot be after end date.";
                    model.Accounts = await GetActiveAccounts(companyCode);
                    return View("Index", model);
                }

                if (string.IsNullOrEmpty(model.SelectedAccountNo))
                {
                    TempData["Error"] = "Please select an account.";
                    model.Accounts = await GetActiveAccounts(companyCode);
                    return View("Index", model);
                }

                // Get account details
                var account = await _context.GlSetup
                    .FirstOrDefaultAsync(x => x.AccNo == model.SelectedAccountNo
                                           && x.CompanyCode == companyCode);

                if (account == null)
                {
                    TempData["Error"] = "Selected account not found.";
                    model.Accounts = await GetActiveAccounts(companyCode);
                    return View("Index", model);
                }

                model.SelectedAccountName = account.Glaccname;
                model.CurrentAccount = $"{account.AccNo} - {account.Glaccname}";

                // Calculate opening balance as of start date
                model.OpeningBalance = await CalculateAccountBalance(
                    model.SelectedAccountNo,
                    model.StartDate.AddDays(-1),
                    companyCode);

                // Get transactions for the period
                model.Transactions = await GetAccountTransactions(
                    model.SelectedAccountNo,
                    model.StartDate,
                    model.EndDate,
                    companyCode);

                // Calculate book balance (opening balance + net activity)
                var periodDebit = model.Transactions.Sum(x => x.DebitAmount);
                var periodCredit = model.Transactions.Sum(x => x.CreditAmount);

                if (account.Normalbal == "DR")
                {
                    model.BookBalance = model.OpeningBalance + periodDebit - periodCredit;
                }
                else
                {
                    model.BookBalance = model.OpeningBalance + periodCredit - periodDebit;
                }

                model.Accounts = await GetActiveAccounts(companyCode);

                _logger.LogInformation($"GL Inquiry executed for Account: {model.SelectedAccountNo}, " +
                    $"Period: {model.StartDate:dd/MM/yyyy} - {model.EndDate:dd/MM/yyyy}");

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing GL Inquiry");
                TempData["Error"] = $"Error executing inquiry: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ===============================
        // GET: /GLInquiry/AccountDetails/{accountNo}
        // ===============================
        [HttpGet("AccountDetails/{accountNo}")]
        public async Task<IActionResult> AccountDetails(string accountNo, DateTime? asAt)
        {
            try
            {
                var companyCode = GetCompanyCode();
                var asAtDate = asAt ?? DateTime.Now;

                var account = await _context.GlSetup
                    .FirstOrDefaultAsync(x => x.AccNo == accountNo
                                           && x.CompanyCode == companyCode);

                if (account == null)
                    return NotFound();

                var balance = await CalculateAccountBalance(accountNo, asAtDate, companyCode);
                var transactions = await GetAccountTransactions(
                    accountNo,
                    asAtDate.AddMonths(-3),
                    asAtDate,
                    companyCode,
                    100);

                return Json(new
                {
                    success = true,
                    accountNo = account.AccNo,
                    accountName = account.Glaccname,
                    normalBalance = account.Normalbal,
                    currentBalance = balance,
                    openingBalance = account.OpeningBal,
                    transactionCount = transactions.Count,
                    recentTransactions = transactions.Take(10)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting account details for {accountNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===============================
        // GET: /GLInquiry/DownloadStatement
        // ===============================
        [HttpGet("DownloadStatement")]
        public async Task<IActionResult> DownloadStatement(string accountNo, DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetCompanyCode();

                var account = await _context.GlSetup
                    .FirstOrDefaultAsync(x => x.AccNo == accountNo
                                           && x.CompanyCode == companyCode);

                if (account == null)
                    return NotFound();

                var transactions = await GetAccountTransactions(
                    accountNo, startDate, endDate, companyCode);

                var openingBalance = await CalculateAccountBalance(
                    accountNo, startDate.AddDays(-1), companyCode);

                // Generate CSV content
                var csv = GenerateCsvStatement(account, transactions, openingBalance, startDate, endDate);

                var fileName = $"GL_Statement_{accountNo}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading statement");
                TempData["Error"] = $"Error downloading statement: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ===============================
        // GET: /GLInquiry/Print/{accountNo}
        // ===============================
        [HttpGet("Print/{accountNo}")]
        public async Task<IActionResult> Print(string accountNo, DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = GetCompanyCode();

                var account = await _context.GlSetup
                    .FirstOrDefaultAsync(x => x.AccNo == accountNo
                                           && x.CompanyCode == companyCode);

                if (account == null)
                    return NotFound();

                var transactions = await GetAccountTransactions(
                    accountNo, startDate, endDate, companyCode);

                var openingBalance = await CalculateAccountBalance(
                    accountNo, startDate.AddDays(-1), companyCode);

                ViewBag.Account = account;
                ViewBag.OpeningBalance = openingBalance;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.CompanyName = GetCompanyName();
                ViewBag.CompanyCode = companyCode;
                ViewBag.PrintDate = DateTime.Now;

                return View(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating print view");
                TempData["Error"] = $"Error generating print view: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ===============================
        // Private Helper Methods
        // ===============================
        private string GetCompanyCode()
        {
            var claim = User.FindFirst("CompanyCode");
            return claim?.Value ?? "001";
        }

        private string GetCompanyName()
        {
            var claim = User.FindFirst("CompanyName");
            return claim?.Value ?? "Main SACCO";
        }

        private async Task<List<AccountDropdownViewModel>> GetActiveAccounts(string companyCode)
        {
            return await _context.GlSetup
                .Where(x => x.Status == true
                         && x.CompanyCode == companyCode)
                .OrderBy(x => x.AccNo)
                .Select(x => new AccountDropdownViewModel
                {
                    AccountNo = x.AccNo ?? "",
                    AccountName = x.Glaccname ?? "",
                    NormalBalance = x.Normalbal,
                    IsActive = x.Status
                })
                .ToListAsync();
        }

        private async Task<decimal> CalculateAccountBalance(
            string accountNo,
            DateTime asAtDate,
            string companyCode)
        {
            // Get opening balance from GL Setup
            var account = await _context.GlSetup
                .FirstOrDefaultAsync(x => x.AccNo == accountNo
                                       && x.CompanyCode == companyCode);

            var openingBalance = account?.OpeningBal ?? 0;

            // Get transactions up to asAtDate
            var transactions = await _context.Gltransactions
                .Where(x => (x.DrAccNo == accountNo || x.CrAccNo == accountNo)
                         && x.TransDate <= asAtDate
                         && x.CompanyCode == companyCode
                         && x.DocPosted == 1)
                .ToListAsync();

            var totalDebit = transactions
                .Where(x => x.DrAccNo == accountNo)
                .Sum(x => x.Amount);

            var totalCredit = transactions
                .Where(x => x.CrAccNo == accountNo)
                .Sum(x => x.Amount);

            if (account?.Normalbal == "DR")
            {
                return openingBalance + totalDebit - totalCredit;
            }
            else
            {
                return openingBalance + totalCredit - totalDebit;
            }
        }

        private async Task<List<GeneralLedgerTransaction>> GetAccountTransactions(
            string accountNo,
            DateTime startDate,
            DateTime endDate,
            string companyCode,
            int? limit = null)
        {
            var query = _context.Gltransactions
                .Where(x => (x.DrAccNo == accountNo || x.CrAccNo == accountNo)
                         && x.TransDate >= startDate
                         && x.TransDate <= endDate
                         && x.CompanyCode == companyCode
                         && x.DocPosted == 1)
                .OrderBy(x => x.TransDate)
                .ThenBy(x => x.Id);

            if (limit.HasValue)
                query = (IOrderedQueryable<Gltransaction>)query.Take(limit.Value);

            var transactions = await query.ToListAsync();

            var result = new List<GeneralLedgerTransaction>();
            decimal runningBalance = await CalculateAccountBalance(accountNo, startDate.AddDays(-1), companyCode);

            foreach (var t in transactions)
            {
                var isDebit = t.DrAccNo == accountNo;
                var amount = t.Amount;

                var transaction = new GeneralLedgerTransaction
                {
                    AuditId = t.AuditId,
                    TransactionDate = t.TransDate,
                    TransactionNo = t.TransactionNo ?? t.DocumentNo,
                    TransactionRemarks = t.TransDescript,
                    MemberNo = ExtractMemberNo(t.TransDescript),
                    DebitAmount = isDebit ? amount : 0,
                    CreditAmount = !isDebit ? amount : 0,
                    DocumentNo = t.DocumentNo,
                    Source = t.Source
                };

                // Calculate running balance
                var account = await _context.GlSetup
                    .FirstOrDefaultAsync(x => x.AccNo == accountNo);

                if (account?.Normalbal == "DR")
                {
                    runningBalance += (isDebit ? amount : -amount);
                }
                else
                {
                    runningBalance += (!isDebit ? amount : -amount);
                }

                transaction.RunningBalance = runningBalance;
                result.Add(transaction);
            }

            return result;
        }

        private string ExtractMemberNo(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return "-";

            // Try to extract member number from description
            // This pattern may need adjustment based on your data
            var words = description.Split(' ');
            foreach (var word in words)
            {
                if (word.StartsWith("MEM") || word.StartsWith("MBR") ||
                    (word.Length >= 5 && word.All(char.IsDigit)))
                {
                    return word;
                }
            }
            return "-";
        }

        private string GenerateCsvStatement(
            GlSetup account,
            List<GeneralLedgerTransaction> transactions,
            decimal openingBalance,
            DateTime startDate,
            DateTime endDate)
        {
            var sb = new System.Text.StringBuilder();

            // Header
            sb.AppendLine($"General Ledger Statement");
            sb.AppendLine($"Account: {account.AccNo} - {account.Glaccname}");
            sb.AppendLine($"Period: {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}");
            sb.AppendLine($"Opening Balance: {openingBalance:N2}");
            sb.AppendLine();

            // Column Headers
            sb.AppendLine("Date,Transaction No,Description,Member No,Debit,Credit,Balance");

            // Transactions
            foreach (var t in transactions)
            {
                sb.AppendLine($"{t.TransactionDate:dd/MM/yyyy}," +
                    $"{t.TransactionNo}," +
                    $"\"{t.TransactionRemarks?.Replace("\"", "\"\"")}\"," +
                    $"{t.MemberNo}," +
                    $"{t.DebitAmount:N2}," +
                    $"{t.CreditAmount:N2}," +
                    $"{t.RunningBalance:N2}");
            }

            // Footer
            sb.AppendLine();
            sb.AppendLine($"Total Debits: {transactions.Sum(x => x.DebitAmount):N2}");
            sb.AppendLine($"Total Credits: {transactions.Sum(x => x.CreditAmount):N2}");
            sb.AppendLine($"Closing Balance: {(transactions.Any() ? transactions.Last().RunningBalance : openingBalance):N2}");
            sb.AppendLine($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            return sb.ToString();
        }
    }
}