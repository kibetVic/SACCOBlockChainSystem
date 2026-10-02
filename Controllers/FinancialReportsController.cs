// Controllers/FinancialReportsController.cs
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
    [Route("Reports")]
    public class FinancialReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FinancialReportsController> _logger;

        public FinancialReportsController(
            ApplicationDbContext context,
            ILogger<FinancialReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===============================
        // GET: /Reports
        // ===============================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetCompanyCode();
                var model = new FinancialReportViewModel
                {
                    StartDate = new DateTime(DateTime.Now.Year, 1, 1),
                    EndDate = DateTime.Now
                };

                // Get suspense account info
                var suspenseAccount = await GetSuspenseAccount(companyCode);
                ViewBag.HasSuspenseAccount = suspenseAccount != null;
                ViewBag.SuspenseAccountNo = suspenseAccount?.AccNo;
                ViewBag.SuspenseAccountName = suspenseAccount?.Glaccname;

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = GetCompanyName();
                ViewBag.ReportDate = DateTime.Now;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports page");
                TempData["Error"] = $"Error loading page: {ex.Message}";
                return View(new FinancialReportViewModel());
            }
        }

        // ===============================
        // POST: /Reports/Generate
        // ===============================
        [HttpPost("Generate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(
            DateTime startDate,
            DateTime endDate,
            string reportType)
        {
            try
            {
                startDate = startDate.Date;
                endDate = endDate.Date.AddDays(1).AddTicks(-1);

                var companyCode = GetCompanyCode();
                var model = new FinancialReportViewModel
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    ReportType = reportType
                };

                // Get all accounts
                var accounts = await _context.GlSetup
                    .Where(x => x.Status == true && x.CompanyCode == companyCode)
                    .OrderBy(x => x.AccNo)
                    .ToListAsync();

                // Get suspense account
                var suspenseAccount = accounts.FirstOrDefault(x => x.IsSuspense);
                model.HasSuspenseAccount = suspenseAccount != null;
                model.SuspenseAccountNo = suspenseAccount?.AccNo;
                model.SuspenseAccountName = suspenseAccount?.Glaccname;

                // Get transactions for the period
                var transactions = await _context.Gltransactions
                    .Where(x => x.TransDate >= startDate
                             && x.TransDate <= endDate
                             && x.CompanyCode == companyCode
                             && x.DocPosted == 1)
                    .ToListAsync();

                // Get journal listings for the period
                var journalListings = await _context.JournalsListings
                    .Where(x => x.TransDate >= startDate
                             && x.TransDate <= endDate
                             && x.CompanyCode == companyCode
                             && x.TransType != "REF"
                             && x.TransType != "GRP")
                    .ToListAsync();

                switch (reportType)
                {
                    case "TrialBalance":
                        await GenerateTrialBalance(model, accounts, transactions, journalListings, companyCode);
                        break;
                    case "IncomeStatement":
                        await GenerateIncomeStatement(model, accounts, transactions, journalListings, companyCode);
                        break;
                    case "BalanceSheet":
                        await GenerateBalanceSheet(model, accounts, transactions, journalListings, companyCode);
                        break;
                    case "CashFlow":
                        await GenerateCashFlow(model, accounts, transactions, journalListings, companyCode);
                        break;
                }

                // Calculate suspense aging if suspense account exists
                if (suspenseAccount != null)
                {
                    model.SuspenseBalance = await CalculateAccountBalance(
                        suspenseAccount.AccNo ?? "", endDate, companyCode);

                    model.SuspenseAging = await CalculateSuspenseAging(
                        suspenseAccount.AccNo ?? "", companyCode);
                }

                ViewBag.CompanyCode = companyCode;
                ViewBag.CompanyName = GetCompanyName();
                ViewBag.ReportDate = DateTime.Now;

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                TempData["Error"] = $"Error generating report: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ===============================
        // POST: /Reports/CreateSuspenseEntry
        // ===============================
        [HttpPost("CreateSuspenseEntry")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Accountant")]
        public async Task<IActionResult> CreateSuspenseEntry(
            DateTime asAtDate,
            string notes = "Suspense entry to balance Balance Sheet")
        {
            try
            {
                var companyCode = GetCompanyCode();
                var auditUser = User.Identity?.Name ?? "SYSTEM";
                var now = DateTime.Now;

                // Get suspense account
                var suspenseAccount = await GetSuspenseAccount(companyCode);

                if (suspenseAccount == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No suspense account defined. Please create a suspense account in GL Setup first."
                    });
                }

                // Recalculate balance sheet totals
                var accounts = await _context.GlSetup
                    .Where(x => x.Status == true && x.CompanyCode == companyCode)
                    .ToListAsync();

                decimal totalAssets = 0;
                decimal totalLiabilities = 0;
                decimal totalEquity = 0;

                // Calculate Assets
                var assetAccounts = accounts.Where(x => x.Type == "Balance Sheet" &&
                                                       x.GlAccMainGroup == "Assets");
                foreach (var account in assetAccounts)
                {
                    totalAssets += await CalculateAccountBalance(
                        account.AccNo ?? "", asAtDate, companyCode);
                }

                // Calculate Liabilities
                var liabilityAccounts = accounts.Where(x => x.Type == "Balance Sheet" &&
                                                           x.GlAccMainGroup == "Liabilities");
                foreach (var account in liabilityAccounts)
                {
                    totalLiabilities += await CalculateAccountBalance(
                        account.AccNo ?? "", asAtDate, companyCode);
                }

                // Calculate Equity
                var equityAccounts = accounts.Where(x => x.Type == "Balance Sheet" &&
                                                        (x.GlAccMainGroup == "Capital Reserved" ||
                                                         x.GlAccMainGroup == "Retained Earnings" ||
                                                         x.GlAccMainGroup == "Shareholder Equity"));
                foreach (var account in equityAccounts)
                {
                    totalEquity += await CalculateAccountBalance(
                        account.AccNo ?? "", asAtDate, companyCode);
                }

                var difference = totalAssets - (totalLiabilities + totalEquity);

                if (Math.Abs(difference) < 0.01m)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Balance Sheet is already balanced. No suspense entry needed."
                    });
                }

                // Create suspense journal entry
                var voucherNo = $"SUS-{DateTime.Now:yyyyMMdd-HHmmss}";
                var amount = Math.Abs(difference);

                // Determine if suspense should be DR or CR
                bool suspenseIsDr = difference < 0; // If Assets are less, need DR suspense
                bool suspenseIsCr = difference > 0; // If Assets are more, need CR suspense

                var suspenseEntry = new JournalsListing
                {
                    VoucherNo = voucherNo,
                    AccountNo = suspenseAccount.AccNo,
                    AccountName = suspenseAccount.Glaccname,
                    Narration = notes,
                    MemberNo = "SYSTEM",
                    ShareType = "SUSPENSE",
                    LoanNo = "0",
                    AmountDr = suspenseIsDr ? amount : 0,
                    AmountCr = suspenseIsCr ? amount : 0,
                    Amount = amount,
                    TransType = suspenseIsDr ? "DR" : "CR",
                    AuditId = auditUser,
                    TransDate = asAtDate,
                    AuditDate = now,
                    Posted = true,
                    PostedDate = now,
                    TransactionNo = GenerateTransactionNumber(),
                    CompanyCode = companyCode
                };

                _context.JournalsListings.Add(suspenseEntry);

                // Also create corresponding GL transaction
                var glTransaction = new Gltransaction
                {
                    TransDate = asAtDate,
                    Amount = amount,
                    DrAccNo = suspenseIsDr ? suspenseAccount.AccNo : GetCashOrBankAccount(),
                    CrAccNo = suspenseIsCr ? suspenseAccount.AccNo : GetCashOrBankAccount(),
                    DocumentNo = voucherNo,
                    Source = "SUSPENSE",
                    Temp = "ADJ",
                    CompanyCode = companyCode,
                    TransDescript = notes,
                    AuditId = auditUser,
                    AuditTime = now,
                    TransactionNo = voucherNo,
                    DocPosted = 1,
                    Module = "GL",
                    Cash = 0,
                    Recon = false
                };

                _context.Gltransactions.Add(glTransaction);
                await _context.SaveChangesAsync();

                string direction = suspenseIsDr ? "DEBIT (added to Assets)" : "CREDIT (added to Liabilities/Equity)";

                _logger.LogInformation($"Balance Sheet suspense entry created: {voucherNo}, " +
                                      $"Amount: {amount:N2}, {direction}");

                return Json(new
                {
                    success = true,
                    message = $"Balance Sheet suspense entry created successfully. Voucher: {voucherNo}",
                    voucherNo = voucherNo,
                    amount = amount,
                    direction = direction,
                    placement = suspenseIsDr ? "Asset" : "Liability/Equity"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Balance Sheet suspense entry");
                return Json(new { success = false, message = $"Error creating suspense entry: {ex.Message}" });
            }
        }

        // ===============================
        // GET: /Reports/ClearSuspense
        // ===============================
        [HttpGet("ClearSuspense")]
        [Authorize(Roles = "Admin,SuperAdmin,Accountant")]
        public async Task<IActionResult> ClearSuspense()
        {
            try
            {
                var companyCode = GetCompanyCode();
                var suspenseAccount = await GetSuspenseAccount(companyCode);

                if (suspenseAccount == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No suspense account defined."
                    });
                }

                var suspenseBalance = await CalculateAccountBalance(
                    suspenseAccount.AccNo ?? "", DateTime.Now, companyCode);

                var aging = await CalculateSuspenseAging(
                    suspenseAccount.AccNo ?? "", companyCode);

                return Json(new
                {
                    success = true,
                    hasBalance = Math.Abs(suspenseBalance) > 0.01m,
                    balance = suspenseBalance,
                    aging = aging,
                    accountNo = suspenseAccount.AccNo,
                    accountName = suspenseAccount.Glaccname
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking suspense status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===============================
        // Report Generation Methods
        // ===============================
        private async Task GenerateTrialBalance(
    FinancialReportViewModel model,
    List<GlSetup> accounts,
    List<Gltransaction> transactions,
    List<JournalsListing> journalListings,
    string companyCode)
        {
            foreach (var account in accounts)
            {
                var accountTransactions = transactions
                    .Where(x => x.DrAccNo == account.AccNo || x.CrAccNo == account.AccNo);

                var accountJournals = journalListings
                    .Where(x => x.AccountNo == account.AccNo);

                var debit = accountTransactions
                    .Where(x => x.DrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                debit += accountJournals
                    .Where(x => x.TransType == "DR")
                    .Sum(x => x.AmountDr ?? 0);

                var credit = accountTransactions
                    .Where(x => x.CrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                credit += accountJournals
                    .Where(x => x.TransType == "CR")
                    .Sum(x => x.AmountCr ?? 0);

                var balance = account.Normalbal == "DR" ? debit - credit : credit - debit;

                // ============================================
                // SHOW ACCOUNTS WITH SIGNIFICANT BALANCE (>= 0.01)
                // ============================================

                bool hasSignificantBalance = Math.Abs(balance) >= 0.01m;
                bool isSuspense = account.IsSuspense;

                if (hasSignificantBalance || isSuspense)
                {
                    model.TrialBalance.Add(new TrialBalanceItem
                    {
                        AccountNo = account.AccNo ?? "",
                        AccountName = account.Glaccname ?? "",
                        AccountType = account.Type ?? "",
                        NormalBalance = account.Normalbal ?? "DR",
                        Debit = debit,
                        Credit = credit,
                        Balance = balance,
                        IsSuspense = account.IsSuspense
                    });
                }

                // Still accumulate totals regardless of display
                model.TotalDebits += debit;
                model.TotalCredits += credit;
            }
        }

        private async Task GenerateIncomeStatement(FinancialReportViewModel model, List<GlSetup> accounts, List<Gltransaction> transactions,List<JournalsListing> journalListings,string companyCode)
        {
            var revenueAccounts = accounts.Where(x => x.Type == "Income Statement" &&
                                                      (x.GlAccMainGroup?.ToLower() == "income" ||
                                                       x.GlAccMainGroup?.ToLower() == "revenue"));
            var expenseAccounts = accounts.Where(x => x.Type == "Income Statement" &&
                                                      (x.GlAccMainGroup?.ToLower() == "expenses" ||
                                                       x.GlAccMainGroup?.ToLower() == "expense"));

            foreach (var account in revenueAccounts)
            {
                var credit = transactions
                    .Where(x => x.CrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                credit += journalListings
                    .Where(x => x.AccountNo == account.AccNo && x.TransType == "CR")
                    .Sum(x => x.AmountCr ?? 0);

                var debit = transactions
                    .Where(x => x.DrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                debit += journalListings
                    .Where(x => x.AccountNo == account.AccNo && x.TransType == "DR")
                    .Sum(x => x.AmountDr ?? 0);

                var amount = credit - debit;

                // Only add if amount is significant (>= 0.01)
                if (Math.Abs(amount) >= 0.01m)
                {
                    model.IncomeStatement.Revenue.Add(new IncomeStatementItem
                    {
                        AccountNo = account.AccNo ?? "",
                        AccountName = account.Glaccname ?? "",
                        Amount = amount
                    });
                }
            }

            foreach (var account in expenseAccounts)
            {
                var debit = transactions
                    .Where(x => x.DrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                debit += journalListings
                    .Where(x => x.AccountNo == account.AccNo && x.TransType == "DR")
                    .Sum(x => x.AmountDr ?? 0);

                var credit = transactions
                    .Where(x => x.CrAccNo == account.AccNo)
                    .Sum(x => x.Amount);

                credit += journalListings
                    .Where(x => x.AccountNo == account.AccNo && x.TransType == "CR")
                    .Sum(x => x.AmountCr ?? 0);

                var amount = debit - credit;

                // Only add if amount is significant (>= 0.01)
                if (Math.Abs(amount) >= 0.01m)
                {
                    model.IncomeStatement.Expenses.Add(new IncomeStatementItem
                    {
                        AccountNo = account.AccNo ?? "",
                        AccountName = account.Glaccname ?? "",
                        Amount = amount
                    });
                }
            }
        }


        private async Task GenerateBalanceSheet(
            FinancialReportViewModel model,
            List<GlSetup> accounts,
            List<Gltransaction> transactions,
            List<JournalsListing> journalListings,
            string companyCode)
        {
            decimal totalAssets = 0;
            decimal totalLiabilities = 0;
            decimal totalEquity = 0;

            // Clear any existing data
            model.BalanceSheet.Assets.Clear();
            model.BalanceSheet.Liabilities.Clear();
            model.BalanceSheet.Equity.Clear();

            // Get Net Income from Income Statement for the period
            var netIncome = await CalculateNetIncome(accounts, transactions, journalListings, companyCode, model.EndDate);

            // ============================================
            // Process ALL Balance Sheet accounts dynamically
            // ============================================
            var balanceSheetAccounts = accounts.Where(x => x.Type == "Balance Sheet" && x.Status == true);

            foreach (var account in balanceSheetAccounts)
            {
                // Calculate balance from transactions
                var balance = CalculateBalanceFromTransactions(
                    account.AccNo ?? "",
                    account.Normalbal ?? "DR",
                    transactions,
                    journalListings);

                var group = account.GlAccMainGroup ?? "";
                var isSuspense = account.IsSuspense;
                var isRetainedEarnings = account.IsREarning ||
                                         group.Equals("Retained Earnings", StringComparison.OrdinalIgnoreCase) ||
                                         group.Contains("Retained", StringComparison.OrdinalIgnoreCase);

                // ============================================
                // Categorize the account based on its group
                // ============================================
                string category = DetermineAccountCategory(group, isSuspense, isRetainedEarnings);

                // Skip zero balance accounts (except retained earnings which should always show)
                if (Math.Abs(balance) < 0.01m && !isRetainedEarnings)
                    continue;

                // Add to the appropriate category
                switch (category)
                {
                    case "Asset":
                        model.BalanceSheet.Assets.Add(new BalanceSheetItem
                        {
                            AccountNo = account.AccNo ?? "",
                            AccountName = account.Glaccname ?? "",
                            Amount = balance,
                            IsSuspense = isSuspense,
                            IsRetainedEarnings = isRetainedEarnings
                        });
                        totalAssets += balance;
                        break;

                    case "Liability":
                        model.BalanceSheet.Liabilities.Add(new BalanceSheetItem
                        {
                            AccountNo = account.AccNo ?? "",
                            AccountName = account.Glaccname ?? "",
                            Amount = balance,
                            IsSuspense = isSuspense,
                            IsRetainedEarnings = isRetainedEarnings
                        });
                        totalLiabilities += balance;
                        break;

                    case "Equity":
                        model.BalanceSheet.Equity.Add(new BalanceSheetItem
                        {
                            AccountNo = account.AccNo ?? "",
                            AccountName = account.Glaccname ?? "",
                            Amount = balance,
                            IsSuspense = isSuspense,
                            IsRetainedEarnings = isRetainedEarnings
                        });
                        totalEquity += balance;
                        break;

                    default:
                        // If category couldn't be determined, log and default to Equity
                        _logger.LogWarning($"Account {account.AccNo} - {account.Glaccname} has unrecognized group: {account.GlAccMainGroup}. Defaulting to Equity.");
                        model.BalanceSheet.Equity.Add(new BalanceSheetItem
                        {
                            AccountNo = account.AccNo ?? "",
                            AccountName = account.Glaccname ?? "",
                            Amount = balance,
                            IsSuspense = isSuspense,
                            IsRetainedEarnings = isRetainedEarnings
                        });
                        totalEquity += balance;
                        break;
                }
            }

            // ============================================
            // Update Retained Earnings with Net Income
            // ============================================
            if (Math.Abs(netIncome) >= 0.01m)
            {
                // Find the retained earnings account
                var retainedEarningsAccount = model.BalanceSheet.Equity
                    .FirstOrDefault(x => x.IsRetainedEarnings == true ||
                                        x.AccountName?.Contains("Retained Earnings", StringComparison.OrdinalIgnoreCase) == true ||
                                        x.AccountName?.Contains("Retained Earning", StringComparison.OrdinalIgnoreCase) == true ||
                                        x.AccountName?.Contains("retained", StringComparison.OrdinalIgnoreCase) == true);

                if (retainedEarningsAccount != null)
                {
                    // Add net income to existing retained earnings
                    retainedEarningsAccount.Amount += netIncome;
                }
                else
                {
                    // No retained earnings account - add as separate entry
                    model.BalanceSheet.Equity.Add(new BalanceSheetItem
                    {
                        AccountNo = "NETINC",
                        AccountName = "Net Income (Current Period)",
                        Amount = netIncome,
                        IsSuspense = false,
                        IsRetainedEarnings = true
                    });
                }

                totalEquity += netIncome;
            }

            // ============================================
            // Set totals
            // ============================================
            model.TotalAssets = totalAssets;
            model.TotalLiabilities = totalLiabilities;
            model.TotalEquity = totalEquity;

            // ============================================
            // Check if balanced
            // ============================================
            var liabilitiesPlusEquity = totalLiabilities + totalEquity;
            var difference = totalAssets - liabilitiesPlusEquity;

            model.BalanceSheetBalanced = Math.Abs(difference) < 0.01m;

            if (!model.BalanceSheetBalanced)
            {
                model.HasSuspenseAccount = true;
                model.SuspenseBalance = Math.Abs(difference);
                model.SuspensePlacement = difference > 0 ? "Liability/Equity" : "Asset";

                _logger.LogWarning($"Balance Sheet unbalanced by {difference:N2}. Assets: {totalAssets:N2}, Liabilities+Equity: {liabilitiesPlusEquity:N2}");
            }

            _logger.LogInformation($"Balance Sheet Generated - Assets: {totalAssets:N2}, Liabilities: {totalLiabilities:N2}, Equity: {totalEquity:N2}, Net Income: {netIncome:N2}");
        }

        /// <summary>
        /// Calculates account balance from transactions (same logic as Trial Balance)
        /// </summary>
        private decimal CalculateBalanceFromTransactions(
            string accountNo,
            string normalBalance,
            List<Gltransaction> transactions,
            List<JournalsListing> journalListings)
        {
            // Get transactions for this account
            var accountTransactions = transactions
                .Where(x => x.DrAccNo == accountNo || x.CrAccNo == accountNo);

            var accountJournals = journalListings
                .Where(x => x.AccountNo == accountNo);

            // Calculate debits
            var debit = accountTransactions
                .Where(x => x.DrAccNo == accountNo)
                .Sum(x => x.Amount);

            debit += accountJournals
                .Where(x => x.TransType == "DR")
                .Sum(x => x.AmountDr ?? 0);

            // Calculate credits
            var credit = accountTransactions
                .Where(x => x.CrAccNo == accountNo)
                .Sum(x => x.Amount);

            credit += accountJournals
                .Where(x => x.TransType == "CR")
                .Sum(x => x.AmountCr ?? 0);

            // Calculate balance based on normal balance
            return normalBalance == "DR" ? debit - credit : credit - debit;
        }

        /// <summary>
        /// Determines the category of an account based on its group
        /// Uses case-insensitive comparison
        /// </summary>
        private string DetermineAccountCategory(string group, bool isSuspense, bool isRetainedEarnings)
        {
            // Suspense accounts default to Liability
            if (isSuspense)
            {
                return "Liability";
            }

            // Retained earnings always go to Equity (highest priority)
            if (isRetainedEarnings)
            {
                return "Equity";
            }

            // Use case-insensitive comparison
            if (string.IsNullOrEmpty(group))
                return "Equity";

            var groupLower = group.ToLower();

            // ============================================
            // ASSET GROUPS - Check these first
            // ============================================
            if (groupLower == "assets" ||
                groupLower == "asset" ||
                groupLower.Contains("current asset") ||
                groupLower.Contains("fixed asset") ||
                groupLower.Contains("non-current asset") ||
                groupLower.Contains("intangible asset") ||
                groupLower.Contains("property") ||
                groupLower.Contains("equipment") ||
                groupLower.Contains("investment"))
            {
                return "Asset";
            }

            // ============================================
            // LIABILITY GROUPS
            // ============================================
            if (groupLower == "liabilities" ||
                groupLower == "liability" ||
                groupLower.Contains("current liability") ||
                groupLower.Contains("non-current liability") ||
                groupLower.Contains("long term liability") ||
                groupLower.Contains("payable") ||
                groupLower.Contains("creditor") ||
                groupLower.Contains("deposit") ||
                groupLower.Contains("accrual"))
            {
                return "Liability";
            }

            // ============================================
            // EQUITY GROUPS
            // ============================================
            if (groupLower == "shareholder equity" ||
                groupLower == "shareholders equity" ||
                groupLower == "owner's equity" ||
                groupLower == "capital reserved" ||
                groupLower == "capital reserve" ||
                groupLower == "retained earnings" ||
                groupLower == "retained earning" ||
                groupLower.Contains("capital") ||
                groupLower.Contains("reserve") ||
                groupLower.Contains("equity") ||
                groupLower.Contains("shareholder") ||
                groupLower.Contains("owner") ||
                groupLower.Contains("retained") ||
                groupLower.Contains("fund") ||
                groupLower.Contains("surplus"))
            {
                return "Equity";
            }

            // ============================================
            // DEFAULT: If we can't determine, default to Equity
            // ============================================
            _logger.LogWarning($"Unrecognized account group: '{group}'. Defaulting to Equity.");
            return "Equity";
        }

        private async Task<decimal> CalculateNetIncome(List<GlSetup> accounts, List<Gltransaction> transactions,List<JournalsListing> journalListings,string companyCode, DateTime asAtDate)
        {
            decimal totalRevenue = 0;
            decimal totalExpenses = 0;

            // Get revenue accounts
            var revenueAccounts = accounts.Where(x => x.Type == "Income Statement" &&
                                                      (x.GlAccMainGroup?.ToLower() == "income" ||
                                                       x.GlAccMainGroup?.ToLower() == "revenue"));

            foreach (var account in revenueAccounts)
            {
                var balance = CalculateBalanceFromTransactions(
                    account.AccNo ?? "",
                    account.Normalbal ?? "CR",
                    transactions,
                    journalListings);
                totalRevenue += balance;
            }

            // Get expense accounts
            var expenseAccounts = accounts.Where(x => x.Type == "Income Statement" &&
                                                      (x.GlAccMainGroup?.ToLower() == "expenses" ||
                                                       x.GlAccMainGroup?.ToLower() == "expense"));

            foreach (var account in expenseAccounts)
            {
                var balance = CalculateBalanceFromTransactions(
                    account.AccNo ?? "",
                    account.Normalbal ?? "DR",
                    transactions,
                    journalListings);
                totalExpenses += balance;
            }

            return totalRevenue - totalExpenses;
        }


        private async Task<decimal> CalculateAccountBalance(string accountNo,DateTime asAtDate,string companyCode)
        {
            decimal totalDebits = 0;
            decimal totalCredits = 0;

            // ============================================
            // 1. Get opening balance from GlSetup
            // ============================================
            var account = await _context.GlSetup
                .FirstOrDefaultAsync(x => x.AccNo == accountNo && x.CompanyCode == companyCode);

            if (account == null)
                return 0;

            // If the asAtDate is after the opening balance date, include opening balance
            if (asAtDate >= account.NewGlOpeningBalDate)
            {
                if (account.Normalbal?.ToUpper() == "DR")
                    totalDebits += account.NewGlOpeningBal;
                else
                    totalCredits += account.NewGlOpeningBal;
            }

            // ============================================
            // 2. Get from GLTRANSACTIONS
            // ============================================
            var glTransactions = await _context.Gltransactions
                .Where(x => x.CompanyCode == companyCode
                         && x.DocPosted == 1
                         && x.TransDate <= asAtDate)
                .ToListAsync();

            totalDebits += glTransactions
                .Where(x => x.DrAccNo == accountNo)
                .Sum(x => x.Amount);

            totalCredits += glTransactions
                .Where(x => x.CrAccNo == accountNo)
                .Sum(x => x.Amount);

            // ============================================
            // 3. Get from Journals
            // ============================================
            var journalEntries = await _context.Journals
                .Where(x => x.ACCNO == accountNo
                         && x.CompanyCode == companyCode
                         && x.POSTED == true
                         && x.TRANSDATE <= asAtDate)
                .ToListAsync();

            foreach (var j in journalEntries)
            {
                if (j.TRANSTYPE == "DR")
                    totalDebits += j.AMOUNT ?? 0;
                else if (j.TRANSTYPE == "CR")
                    totalCredits += j.AMOUNT ?? 0;
            }

            // ============================================
            // 4. Get from JournalsListings
            // ============================================
            var journalListings = await _context.JournalsListings
                .Where(x => x.AccountNo == accountNo
                         && x.CompanyCode == companyCode
                         && x.Posted == true
                         && x.TransDate <= asAtDate
                         && x.TransType != "REF"
                         && x.TransType != "GRP")
                .ToListAsync();

            totalDebits += journalListings.Sum(x => x.AmountDr ?? 0);
            totalCredits += journalListings.Sum(x => x.AmountCr ?? 0);

            // ============================================
            // Return balance based on normal balance
            // ============================================
            return account.Normalbal?.ToUpper() == "DR"
                ? totalDebits - totalCredits
                : totalCredits - totalDebits;
        }


        /// <summary>
        /// Dynamically identifies cash accounts based on GL Setup
        /// </summary>
        private List<string> GetCashAccounts(List<GlSetup> accounts, string companyCode)
        {
            // Method 1: Look for accounts with "Cash" in the group name
            var cashAccounts = accounts
                .Where(x => x.Status == true &&
                           x.Type == "Balance Sheet" &&
                           (x.GlAccMainGroup?.ToLower().Contains("cash") == true ||
                            x.GlAccMainGroup?.ToLower().Contains("bank") == true ||
                            x.GlAccMainGroup?.ToLower() == "current assets" &&
                            (x.Glaccname?.ToLower().Contains("cash") == true ||
                             x.Glaccname?.ToLower().Contains("bank") == true ||
                             x.Glaccname?.ToLower().Contains("mpesa") == true ||
                             x.Glaccname?.ToLower().Contains("mobile") == true)))
                .Select(x => x.AccNo)
                .ToList();

            // Method 2: If no cash accounts found, look for accounts with "cash" in the name
            if (!cashAccounts.Any())
            {
                cashAccounts = accounts
                    .Where(x => x.Status == true &&
                               (x.Glaccname?.ToLower().Contains("cash") == true ||
                                x.Glaccname?.ToLower().Contains("bank") == true ||
                                x.Glaccname?.ToLower().Contains("mpesa") == true ||
                                x.Glaccname?.ToLower().Contains("mobile") == true))
                    .Select(x => x.AccNo)
                    .ToList();
            }

            // Method 3: Fallback - check account category
            if (!cashAccounts.Any())
            {
                cashAccounts = accounts
                    .Where(x => x.Status == true &&
                               (x.AccCategory?.ToLower() == "cash" ||
                                x.AccCategory?.ToLower() == "bank" ||
                                x.SubType?.ToLower() == "cash" ||
                                x.SubType?.ToLower() == "bank"))
                    .Select(x => x.AccNo)
                    .ToList();
            }

            return cashAccounts;
        }



        private async Task GenerateCashFlow(
    FinancialReportViewModel model,
    List<GlSetup> accounts,
    List<Gltransaction> transactions,
    List<JournalsListing> journalListings,
    string companyCode)
        {
            // ============================================
            // 1. Identify cash accounts dynamically
            // ============================================
            var cashAccounts = GetCashAccounts(accounts, companyCode);

            if (!cashAccounts.Any())
            {
                _logger.LogWarning("No cash accounts found for Cash Flow Statement. Please configure cash accounts in GL Setup.");

                // Add a message to the model
                model.CashFlow.OperatingActivities.Add(new CashFlowItem
                {
                    Description = "*** No cash accounts configured. Please set up cash accounts in GL Setup. ***",
                    Amount = 0
                });
                return;
            }

            // ============================================
            // 2. Get the primary cash account for beginning/ending balance
            // ============================================
            var primaryCashAccountNo = cashAccounts.First();
            var primaryCashAccount = accounts.FirstOrDefault(x => x.AccNo == primaryCashAccountNo);

            if (primaryCashAccount != null)
            {
                // Use the transactions list to calculate balances (consistent with Trial Balance)
                model.CashFlow.BeginningCash = CalculateBalanceFromTransactions(
                    primaryCashAccount.AccNo ?? "",
                    primaryCashAccount.Normalbal ?? "DR",
                    transactions.Where(x => x.TransDate < model.StartDate).ToList(),
                    journalListings.Where(x => x.TransDate < model.StartDate).ToList());

                model.CashFlow.EndingCash = CalculateBalanceFromTransactions(
                    primaryCashAccount.AccNo ?? "",
                    primaryCashAccount.Normalbal ?? "DR",
                    transactions,
                    journalListings);
            }

            // ============================================
            // 3. Process ALL cash transactions
            // ============================================
            var cashTransactions = transactions
                .Where(x => (cashAccounts.Contains(x.DrAccNo) || cashAccounts.Contains(x.CrAccNo)) &&
                           x.DocPosted == 1)
                .ToList();

            if (!cashTransactions.Any())
            {
                _logger.LogInformation($"No cash transactions found for the period.");

                model.CashFlow.OperatingActivities.Add(new CashFlowItem
                {
                    Description = "No cash transactions for this period",
                    Amount = 0
                });
                return;
            }

            foreach (var t in cashTransactions)
            {
                // Calculate amount (positive for cash inflow, negative for outflow)
                decimal amount = 0;
                string counterpartyAccount = "";

                if (cashAccounts.Contains(t.DrAccNo))
                {
                    // Cash is being debited (cash INCREASES)
                    amount = t.Amount;
                    counterpartyAccount = t.CrAccNo;
                }
                else if (cashAccounts.Contains(t.CrAccNo))
                {
                    // Cash is being credited (cash DECREASES)
                    amount = -t.Amount;
                    counterpartyAccount = t.DrAccNo;
                }

                var description = t.TransDescript ?? "Cash transaction";

                // Get counterparty account details
                var counterparty = accounts.FirstOrDefault(x => x.AccNo == counterpartyAccount);

                // Determine the category based on counterparty account
                string category = DetermineCashFlowCategory(counterparty, description);

                // Add to the appropriate category
                switch (category)
                {
                    case "Operating":
                        model.CashFlow.OperatingActivities.Add(new CashFlowItem
                        {
                            Description = description,
                            Amount = amount
                        });
                        break;
                    case "Investing":
                        model.CashFlow.InvestingActivities.Add(new CashFlowItem
                        {
                            Description = description,
                            Amount = amount
                        });
                        break;
                    case "Financing":
                        model.CashFlow.FinancingActivities.Add(new CashFlowItem
                        {
                            Description = description,
                            Amount = amount
                        });
                        break;
                    default:
                        // Default to Operating
                        model.CashFlow.OperatingActivities.Add(new CashFlowItem
                        {
                            Description = description,
                            Amount = amount
                        });
                        break;
                }
            }

            // ============================================
            // 4. Log the cash flow summary
            // ============================================
            _logger.LogInformation($"Cash Flow Summary - Period: {model.StartDate:dd/MM/yyyy} to {model.EndDate:dd/MM/yyyy}");
            _logger.LogInformation($"  Cash Accounts Found: {string.Join(", ", cashAccounts)}");
            _logger.LogInformation($"  Beginning Cash: {model.CashFlow.BeginningCash:N2}");
            _logger.LogInformation($"  Net Operating: {model.CashFlow.NetOperating:N2}");
            _logger.LogInformation($"  Net Investing: {model.CashFlow.NetInvesting:N2}");
            _logger.LogInformation($"  Net Financing: {model.CashFlow.NetFinancing:N2}");
            _logger.LogInformation($"  Net Cash Flow: {model.CashFlow.NetCashFlow:N2}");
            _logger.LogInformation($"  Ending Cash: {model.CashFlow.EndingCash:N2}");
        }

        /// <summary>
        /// Determines the cash flow category based on the counterparty account
        /// </summary>
        private string DetermineCashFlowCategory(GlSetup? counterparty, string description)
        {
            if (counterparty == null)
                return "Operating";

            var group = counterparty.GlAccMainGroup?.ToLower() ?? "";
            var name = counterparty.Glaccname?.ToLower() ?? "";
            var desc = description.ToLower();

            // ============================================
            // FINANCING ACTIVITIES
            // ============================================
            // Member share capital, loans, dividends
            if (group.Contains("capital") ||
                group.Contains("share") ||
                group.Contains("loan") ||
                group.Contains("borrow") ||
                group.Contains("dividend") ||
                group.Contains("equity") ||
                name.Contains("share capital") ||
                name.Contains("loan") ||
                name.Contains("dividend") ||
                desc.Contains("share") ||
                desc.Contains("dividend") ||
                desc.Contains("loan disbursement") ||
                desc.Contains("loan repayment") ||
                desc.Contains("member contribution"))
            {
                return "Financing";
            }

            // ============================================
            // INVESTING ACTIVITIES
            // ============================================
            // Purchase of fixed assets, investments
            if ((group.Contains("asset") || group.Contains("investment")) &&
                (name.Contains("fixed") ||
                 name.Contains("property") ||
                 name.Contains("equipment") ||
                 name.Contains("investment") ||
                 name.Contains("building") ||
                 name.Contains("vehicle") ||
                 name.Contains("furniture")))
            {
                return "Investing";
            }

            if (desc.Contains("invest") ||
                desc.Contains("asset purchase") ||
                desc.Contains("fixed asset") ||
                desc.Contains("equipment") ||
                desc.Contains("property") ||
                desc.Contains("building") ||
                desc.Contains("vehicle"))
            {
                return "Investing";
            }

            // ============================================
            // OPERATING ACTIVITIES (Default)
            // ============================================
            return "Operating";
        }

















        // ===============================
        // Helper Methods
        // ===============================
        private string GetCompanyCode()
        {
            return User.FindFirst("CompanyCode")?.Value ?? "001";
        }

        private string GetCompanyName()
        {
            return User.FindFirst("CompanyName")?.Value ?? "Main SACCO";
        }

        private string GenerateTransactionNumber()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random();
            var randomPart = random.Next(1000, 9999);
            return $"TXN-{date}-{randomPart}";
        }

        private string GetCashOrBankAccount()
        {
            try
            {
                var cashAccount = _context.GlSetup
                    .FirstOrDefault(x => x.AccNo == "1010" && x.Status == true);
                return cashAccount?.AccNo ?? "1010";
            }
            catch
            {
                return "1010";
            }
        }

        private async Task<GlSetup?> GetSuspenseAccount(string companyCode)
        {
            return await _context.GlSetup
                .FirstOrDefaultAsync(x => x.IsSuspense == true
                                       && x.CompanyCode == companyCode
                                       && x.Status == true);
        }

        private async Task<List<SuspenseAgingItem>> CalculateSuspenseAging( string suspenseAccountNo, string companyCode)
        {
            var suspenseEntries = await _context.JournalsListings
                .Where(x => x.AccountNo == suspenseAccountNo
                         && x.CompanyCode == companyCode
                         && x.TransType != "REF")
                .OrderBy(x => x.TransDate)
                .ToListAsync();

            var aging = new List<SuspenseAgingItem>();
            var now = DateTime.Now;

            foreach (var entry in suspenseEntries)
            {
                var daysOld = (now - (entry.TransDate ?? now)).Days;
                var amount = (entry.AmountDr ?? 0) - (entry.AmountCr ?? 0);

                aging.Add(new SuspenseAgingItem
                {
                    VoucherNo = entry.VoucherNo,
                    TransactionDate = entry.TransDate ?? now,
                    Description = entry.Narration,
                    Amount = Math.Abs(amount),
                    Type = amount > 0 ? "DR" : "CR",
                    DaysOld = daysOld,
                    AgingBucket = GetAgingBucket(daysOld)
                });
            }

            return aging;
        }

        private string GetAgingBucket(int daysOld)
        {
            if (daysOld <= 7) return "Current (0-7 days)";
            if (daysOld <= 30) return "1-30 days";
            if (daysOld <= 60) return "31-60 days";
            if (daysOld <= 90) return "61-90 days";
            return "Over 90 days";
        }

        private string GenerateCsvReport(FinancialReportViewModel model, string companyName, string companyCode)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"Company: {companyName} ({companyCode})");
            sb.AppendLine($"Report Type: {model.ReportType}");
            sb.AppendLine($"Period: {model.StartDate:dd/MM/yyyy} to {model.EndDate:dd/MM/yyyy}");
            sb.AppendLine($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            if (model.HasSuspenseAccount && Math.Abs(model.SuspenseBalance) > 0.01m)
            {
                sb.AppendLine($"*** WARNING: Suspense Account has balance of {model.SuspenseBalance:N2} ***");
            }
            sb.AppendLine();

            switch (model.ReportType)
            {
                case "TrialBalance":
                    sb.AppendLine("Trial Balance");
                    sb.AppendLine("Account No,Account Name,Account Type,Normal Balance,Debit,Credit,Balance,Suspense");
                    foreach (var item in model.TrialBalance)
                    {
                        sb.AppendLine($"{item.AccountNo},{item.AccountName},{item.AccountType},{item.NormalBalance},{item.Debit:N2},{item.Credit:N2},{item.Balance:N2},{(item.IsSuspense ? "YES" : "NO")}");
                    }
                    sb.AppendLine();
                    sb.AppendLine($"Total Debits:,{model.TotalDebits:N2}");
                    sb.AppendLine($"Total Credits:,{model.TotalCredits:N2}");
                    sb.AppendLine($"Difference:,{model.TotalDebits - model.TotalCredits:N2}");
                    break;

                case "IncomeStatement":
                    sb.AppendLine("Income Statement");
                    sb.AppendLine();
                    sb.AppendLine("Revenue");
                    foreach (var item in model.IncomeStatement.Revenue)
                    {
                        sb.AppendLine($"{item.AccountName},{item.Amount:N2}");
                    }
                    sb.AppendLine($"Total Revenue,{model.IncomeStatement.TotalRevenue:N2}");
                    sb.AppendLine();
                    sb.AppendLine("Expenses");
                    foreach (var item in model.IncomeStatement.Expenses)
                    {
                        sb.AppendLine($"{item.AccountName},{item.Amount:N2}");
                    }
                    sb.AppendLine($"Total Expenses,{model.IncomeStatement.TotalExpenses:N2}");
                    sb.AppendLine();
                    sb.AppendLine($"Net Income,{model.IncomeStatement.NetIncome:N2}");
                    break;

                case "BalanceSheet":
                    sb.AppendLine("Balance Sheet");
                    sb.AppendLine();
                    sb.AppendLine("Assets");
                    foreach (var item in model.BalanceSheet.Assets)
                    {
                        sb.AppendLine($"{item.AccountName},{item.Amount:N2}{(item.IsSuspense ? " (SUSPENSE)" : "")}");
                    }
                    sb.AppendLine($"Total Assets,{model.BalanceSheet.TotalAssets:N2}");
                    sb.AppendLine();
                    sb.AppendLine("Liabilities");
                    foreach (var item in model.BalanceSheet.Liabilities)
                    {
                        sb.AppendLine($"{item.AccountName},{item.Amount:N2}{(item.IsSuspense ? " (SUSPENSE)" : "")}");
                    }
                    sb.AppendLine($"Total Liabilities,{model.BalanceSheet.TotalLiabilities:N2}");
                    sb.AppendLine();
                    sb.AppendLine("Equity");
                    foreach (var item in model.BalanceSheet.Equity)
                    {
                        sb.AppendLine($"{item.AccountName},{item.Amount:N2}{(item.IsSuspense ? " (SUSPENSE)" : "")}");
                    }
                    sb.AppendLine($"Total Equity,{model.BalanceSheet.TotalEquity:N2}");
                    sb.AppendLine();
                    sb.AppendLine($"Total Liabilities & Equity,{model.BalanceSheet.TotalLiabilities + model.BalanceSheet.TotalEquity:N2}");

                    if (!model.BalanceSheetBalanced)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"*** BALANCE SHEET IS UNBALANCED by {model.BalanceSheetDifference:N2} ***");
                    }
                    break;
            }

            return sb.ToString();
        }
    }
}