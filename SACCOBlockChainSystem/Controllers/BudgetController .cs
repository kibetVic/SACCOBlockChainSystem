//// Controllers/BudgetController.cs
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SACCOBlockChainSystem.Data;
//using SACCOBlockChainSystem.Models;
//using SACCOBlockChainSystem.Models.ViewModels;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;

//namespace SACCOBlockChainSystem.Controllers
//{
//    [Authorize]
//    [Route("Budget")]
//    public class BudgetController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly ILogger<BudgetController> _logger;

//        public BudgetController(ApplicationDbContext context, ILogger<BudgetController> logger)
//        {
//            _context = context;
//            _logger = logger;
//        }

//        // ===============================
//        // GET: /Budget
//        // ===============================
//        [HttpGet("")]
//        public async Task<IActionResult> Index()
//        {
//            try
//            {
//                var model = new BudgetViewModel
//                {
//                    BudgetHeader = new BudgetHeader
//                    {
//                        BudgetName = $"Budget {DateTime.Now.Year}",
//                        StartDate = new DateTime(DateTime.Now.Year, 1, 1),
//                        EndDate = new DateTime(DateTime.Now.Year, 12, 31),
//                        TotalBudget = 0,
//                        UserID = User.Identity?.Name,
//                        CreatedDate = DateTime.Now
//                    },
//                    Accounts = await GetActiveAccounts()
//                };

//                ViewBag.CompanyCode = GetCompanyCode();
//                ViewBag.CompanyName = GetCompanyName();

//                return View(model);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error loading budget page");
//                TempData["Error"] = $"Error loading page: {ex.Message}";
//                return View(new BudgetViewModel());
//            }
//        }

//        // ===============================
//        // GET: /Budget/GetAccountDetails/{accountNo}
//        // ===============================
//        [HttpGet("GetAccountDetails/{accountNo}")]
//        public async Task<IActionResult> GetAccountDetails(string accountNo)
//        {
//            try
//            {
//                var companyCode = GetCompanyCode();

//                var account = await _context.GlSetup
//                    .FirstOrDefaultAsync(x => x.AccNo == accountNo
//                                           && x.CompanyCode == companyCode
//                                           && x.Status == true);

//                if (account == null)
//                    return Json(new { success = false, message = "Account not found." });

//                var currentUtilization = await CalculateCurrentUtilization(accountNo);

//                return Json(new
//                {
//                    success = true,
//                    accountNo = account.AccNo,
//                    accountName = account.Glaccname,
//                    openingBalance = account.OpeningBal?? 0,
//                    currentUtilization = currentUtilization,
//                    remainingBalance = (account.OpeningBal ?? 0) - currentUtilization
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error getting account details for {accountNo}");
//                return Json(new { success = false, message = ex.Message });
//            }
//        }

//        // ===============================
//        // POST: /Budget/SaveHeader
//        // ===============================
//        [HttpPost("SaveHeader")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> SaveHeader([FromBody] BudgetHeader header)
//        {
//            try
//            {
//                if (header == null)
//                    return Json(new { success = false, message = "Invalid budget header data." });

//                if (string.IsNullOrEmpty(header.BudgetName))
//                    return Json(new { success = false, message = "Budget name is required." });

//                if (header.TotalBudget <= 0)
//                    return Json(new { success = false, message = "Total budget must be greater than 0." });

//                if (header.StartDate > header.EndDate)
//                    return Json(new { success = false, message = "Start date cannot be after end date." });

//                header.UserID = User.Identity?.Name;
//                header.CreatedDate = DateTime.Now;

//                if (header.BudgetID > 0)
//                {
//                    // Update existing
//                    var existing = await _context.BudgetHeader.FindAsync(header.BudgetID);
//                    if (existing != null)
//                    {
//                        existing.BudgetName = header.BudgetName;
//                        existing.TotalBudget = header.TotalBudget;
//                        existing.StartDate = header.StartDate;
//                        existing.EndDate = header.EndDate;
//                        _context.BudgetHeader.Update(existing);
//                    }
//                }
//                else
//                {
//                    // Add new
//                    await _context.BudgetHeader.AddAsync(header);
//                }

//                await _context.SaveChangesAsync();

//                _logger.LogInformation($"Budget header saved: {header.BudgetName}, ID: {header.BudgetID}");

//                return Json(new
//                {
//                    success = true,
//                    message = "Budget header saved successfully!",
//                    budgetId = header.BudgetID
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error saving budget header");
//                return Json(new { success = false, message = $"Error saving budget header: {ex.Message}" });
//            }
//        }

//        // ===============================
//        // POST: /Budget/SaveEntries
//        // ===============================
//        [HttpPost("SaveEntries")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> SaveEntries([FromBody] List<BudgetEntry> entries)
//        {
//            try
//            {
//                if (entries == null || !entries.Any())
//                    return Json(new { success = false, message = "No budget entries to save." });

//                var budgetId = entries.First().BudgetID;

//                // Remove existing entries for this budget
//                var existingEntries = await _context.BudgetEntries
//                    .Where(x => x.BudgetID == budgetId)
//                    .ToListAsync();

//                if (existingEntries.Any())
//                {
//                    _context.BudgetEntries.RemoveRange(existingEntries);
//                    await _context.SaveChangesAsync();
//                }

//                // Set entry date if not provided
//                foreach (var entry in entries)
//                {
//                    entry.EntryDate = entry.EntryDate ?? DateTime.Now;

//                    // Get account details for CurrentBudget (Opening Balance)
//                    if (!string.IsNullOrEmpty(entry.AccountNumber))
//                    {
//                        var account = await _context.GlSetup
//                            .FirstOrDefaultAsync(x => x.AccNo == entry.AccountNumber);
//                        entry.CurrentBudget = account?.OpeningBal ?? 0;
//                    }
//                }

//                await _context.BudgetEntries.AddRangeAsync(entries);
//                await _context.SaveChangesAsync();

//                _logger.LogInformation($"Budget entries saved: {entries.Count} entries for budget ID {budgetId}");

//                return Json(new
//                {
//                    success = true,
//                    message = "Budget entries saved successfully!",
//                    entryCount = entries.Count
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error saving budget entries");
//                return Json(new { success = false, message = $"Error saving budget entries: {ex.Message}" });
//            }
//        }

//        // ===============================
//        // GET: /Budget/List
//        // ===============================
//        [HttpGet("List")]
//        public async Task<IActionResult> List()
//        {
//            try
//            {
//                var budgets = await _context.BudgetHeader
//                    .OrderByDescending(x => x.CreatedDate)
//                    .ToListAsync();

//                ViewBag.CompanyCode = GetCompanyCode();
//                ViewBag.CompanyName = GetCompanyName();

//                return View(budgets);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error loading budget list");
//                TempData["Error"] = $"Error loading budgets: {ex.Message}";
//                return View(new List<BudgetHeader>());
//            }
//        }

//        // ===============================
//        // GET: /Budget/Entries/{budgetId}
//        // ===============================
//        [HttpGet("Entries/{budgetId}")]
//        public async Task<IActionResult> GetEntries(int budgetId)
//        {
//            try
//            {
//                var entries = await _context.BudgetEntries
//                    .Where(x => x.BudgetID == budgetId)
//                    .OrderByDescending(x => x.EntryDate)
//                    .ToListAsync();

//                return Json(new { success = true, entries = entries });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error loading budget entries for {budgetId}");
//                return Json(new { success = false, message = ex.Message });
//            }
//        }

//        // ===============================
//        // GET: /Budget/Edit/{id}
//        // ===============================
//        [HttpGet("Edit/{id}")]
//        public async Task<IActionResult> Edit(int id)
//        {
//            try
//            {
//                var budget = await _context.BudgetHeaders
//                    .FirstOrDefaultAsync(x => x.BudgetID == id);

//                if (budget == null)
//                    return NotFound();

//                var entries = await _context.BudgetEntries
//                    .Where(x => x.BudgetID == id)
//                    .OrderByDescending(x => x.EntryDate)
//                    .ToListAsync();

//                var entryViewModels = new List<BudgetEntryViewModel>();

//                foreach (var entry in entries)
//                {
//                    var account = await _context.GlSetup
//                        .FirstOrDefaultAsync(x => x.AccNo == entry.AccountNumber);

//                    var currentUtilization = await CalculateCurrentUtilization(entry.AccountNumber ?? "");

//                    entryViewModels.Add(new BudgetEntryViewModel
//                    {
//                        EntryID = entry.EntryID,
//                        BudgetID = entry.BudgetID,
//                        AccountNumber = entry.AccountNumber,
//                        AccountName = entry.AccountName ?? account?.Glaccname,
//                        Amount = entry.Amount,
//                        CurrentBudget = entry.CurrentBudget,
//                        EntryDate = entry.EntryDate,
//                        Notes = entry.Notes,
//                        OpeningBalance = account?.OpeningBal ?? 0,
//                        CurrentUtilization = currentUtilization
//                    });
//                }

//                var model = new BudgetViewModel
//                {
//                    BudgetHeader = budget,
//                    BudgetEntries = entryViewModels,
//                    Accounts = await GetActiveAccounts()
//                };

//                ViewBag.CompanyCode = GetCompanyCode();
//                ViewBag.CompanyName = GetCompanyName();

//                return View("Index", model);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error editing budget {id}");
//                TempData["Error"] = $"Error loading budget: {ex.Message}";
//                return RedirectToAction(nameof(List));
//            }
//        }

//        // ===============================
//        // POST: /Budget/Delete/{id}
//        // ===============================
//        [HttpPost("Delete/{id}")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Delete(int id)
//        {
//            try
//            {
//                var budget = await _context.BudgetHeader
//                    .FirstOrDefaultAsync(x => x.BudgetID == id);

//                if (budget == null)
//                    return Json(new { success = false, message = "Budget not found." });

//                // Delete entries first
//                var entries = await _context.BudgetEntries
//                    .Where(x => x.BudgetID == id)
//                    .ToListAsync();

//                _context.BudgetEntries.RemoveRange(entries);
//                _context.BudgetHeader.Remove(budget);
//                await _context.SaveChangesAsync();

//                _logger.LogInformation($"Budget deleted: {budget.BudgetName}");

//                return Json(new { success = true, message = "Budget deleted successfully!" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error deleting budget {id}");
//                return Json(new { success = false, message = $"Error deleting budget: {ex.Message}" });
//            }
//        }

//        // ===============================
//        // GET: /Budget/Report/{id}
//        // ===============================
//        [HttpGet("Report/{id}")]
//        public async Task<IActionResult> Report(int id)
//        {
//            try
//            {
//                var budget = await _context.BudgetHeader
//                    .FirstOrDefaultAsync(x => x.BudgetID == id);

//                if (budget == null)
//                    return NotFound();

//                var entries = await _context.BudgetEntries
//                    .Where(x => x.BudgetID == id)
//                    .OrderBy(x => x.AccountNumber)
//                    .ToListAsync();

//                ViewBag.Budget = budget;
//                ViewBag.Entries = entries;
//                ViewBag.TotalAllocated = entries.Sum(x => x.Amount);
//                ViewBag.RemainingBudget = budget.TotalBudget - ViewBag.TotalAllocated;
//                ViewBag.CompanyCode = GetCompanyCode();
//                ViewBag.CompanyName = GetCompanyName();
//                ViewBag.ReportDate = DateTime.Now;

//                return View(entries);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error generating budget report {id}");
//                TempData["Error"] = $"Error generating report: {ex.Message}";
//                return RedirectToAction(nameof(List));
//            }
//        }

//        // ===============================
//        // Private Helper Methods
//        // ===============================
//        private string GetCompanyCode()
//        {
//            return User.FindFirst("CompanyCode")?.Value ?? "001";
//        }

//        private string GetCompanyName()
//        {
//            return User.FindFirst("CompanyName")?.Value ?? "Main SACCO";
//        }

//        private async Task<List<AccountDropdownModel>> GetActiveAccounts()
//        {
//            var companyCode = GetCompanyCode();

//            return await _context.GlSetup
//                .Where(x => x.Status == true && x.CompanyCode == companyCode)
//                .OrderBy(x => x.AccNo)
//                .Select(x => new AccountDropdownModel
//                {
//                    AccountNo = x.AccNo ?? "",
//                    AccountName = x.Glaccname ?? "",
//                    OpeningBalance = x.OpeningBal ?? 0
//                })
//                .ToListAsync();
//        }

//        private async Task<decimal> CalculateCurrentUtilization(string accountNo)
//        {
//            if (string.IsNullOrEmpty(accountNo))
//                return 0;

//            var companyCode = GetCompanyCode();
//            var currentYear = DateTime.Now.Year;

//            var startDate = new DateTime(currentYear, 1, 1);
//            var endDate = new DateTime(currentYear, 12, 31);

//            var transactions = await _context.Gltransactions
//                .Where(x => (x.DrAccNo == accountNo || x.CrAccNo == accountNo)
//                         && x.TransDate >= startDate
//                         && x.TransDate <= endDate
//                         && x.CompanyCode == companyCode
//                         && x.DocPosted == 1)
//                .ToListAsync();

//            var account = await _context.GlSetup
//                .FirstOrDefaultAsync(x => x.AccNo == accountNo);

//            if (account?.Normalbal == "DR")
//            {
//                return transactions.Where(x => x.DrAccNo == accountNo).Sum(x => x.Amount);
//            }
//            else
//            {
//                return transactions.Where(x => x.CrAccNo == accountNo).Sum(x => x.Amount);
//            }
//        }


//    }
//}