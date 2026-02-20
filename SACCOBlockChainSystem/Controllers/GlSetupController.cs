using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SACCOBlockChainSystem.Controllers
{
    [Route("GlSetup")]
    public class GlSetupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GlSetupController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // GET: /GlSetup
        // ===============================
        [HttpGet("")]
        public IActionResult Index()
        {
            ViewBag.Accounts = _context.GlSetup
                .OrderBy(x => x.AccNo)
                .ToList();

            // Populate dropdown lists
            ViewBag.AccountTypes = GetAccountTypes();
            ViewBag.AccountCategories = GetAccountCategories();
            ViewBag.Currencies = GetCurrencies();
            ViewBag.SubCategories = GetSubCategories();

            return View(new GlSetup());
        }

        // ===============================
        // GET: /GlSetup/GetGroupsByType
        // ===============================
        [HttpGet("GetGroupsByType")]
        public IActionResult GetGroupsByType(string accountType)
        {
            var accountTypes = GetAccountTypes();
            var selectedType = accountTypes.FirstOrDefault(t => t.Type == accountType);

            if (selectedType == null)
                return Json(new List<object>());

            var groups = selectedType.Groups.Select(g => new
            {
                name = g.Name,
                normalBalance = g.NormalBalance
            }).ToList();

            return Json(groups);
        }

        // ===============================
        // POST: /GlSetup/Save
        // ===============================
        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public IActionResult Save(GlSetup model)
        {
            // Remove validation for nullable fields
            ModelState.Remove("AuditDate");
            ModelState.Remove("EoyDate");
            ModelState.Remove("NewGlOpeningBalDate");

            if (!ModelState.IsValid)
            {
                ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                return View("Index", model);
            }

            try
            {
                // Set default values
                model.TransDate = DateTime.Now;
                model.Status = true;
                model.AuditDate = DateTime.Now;
                model.AuditId = User.Identity?.Name ?? "SYSTEM";

                // Set normal balance based on account group
                model.Normalbal = GetNormalBalanceByGroup(model.GlAccMainGroup);

                // Set default values for required fields
                if (string.IsNullOrEmpty(model.Type)) model.Type = "Balance Sheet";
                if (string.IsNullOrEmpty(model.SubType)) model.SubType = "Others";
                if (model.OpeningBal == 0) model.OpeningBal = 0;
                if (model.NewGlOpeningBal == 0) model.NewGlOpeningBal = 0;
                if (model.NewGlOpeningBalDate == DateTime.MinValue) model.NewGlOpeningBalDate = DateTime.Now;

                // Check if account number already exists
                var existingAccount = _context.GlSetup.FirstOrDefault(x => x.AccNo == model.AccNo);
                if (existingAccount != null)
                {
                    ModelState.AddModelError("AccNo", "Account number already exists.");
                    ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                    ViewBag.AccountTypes = GetAccountTypes();
                    ViewBag.AccountCategories = GetAccountCategories();
                    ViewBag.Currencies = GetCurrencies();
                    ViewBag.SubCategories = GetSubCategories();
                    return View("Index", model);
                }

                _context.GlSetup.Add(model);
                _context.SaveChanges();

                TempData["Success"] = "Account saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error saving account: {ex.Message}");
                ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                return View("Index", model);
            }
        }

        // ===============================
        // GET: /GlSetup/Edit/5
        // ===============================
        [HttpGet("Edit/{id}")]
        public IActionResult Edit(long id)
        {
            var account = _context.GlSetup.Find(id);
            if (account == null)
                return NotFound();

            ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
            ViewBag.AccountTypes = GetAccountTypes();
            ViewBag.AccountCategories = GetAccountCategories();
            ViewBag.Currencies = GetCurrencies();
            ViewBag.SubCategories = GetSubCategories();

            return View("Index", account);
        }

        // ===============================
        // POST: /GlSetup/Update
        // ===============================
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public IActionResult Update(GlSetup model)
        {
            // Remove validation for nullable fields
            ModelState.Remove("AuditDate");
            ModelState.Remove("EoyDate");

            if (!ModelState.IsValid)
            {
                ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                return View("Index", model);
            }

            try
            {
                var existing = _context.GlSetup.Find(model.GlId);
                if (existing == null)
                    return NotFound();

                // Check if account number is being changed and if it already exists
                if (existing.AccNo != model.AccNo)
                {
                    var duplicateAccount = _context.GlSetup.FirstOrDefault(x => x.AccNo == model.AccNo && x.GlId != model.GlId);
                    if (duplicateAccount != null)
                    {
                        ModelState.AddModelError("AccNo", "Account number already exists.");
                        ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }
                }

                // Update normal balance based on account group
                model.Normalbal = GetNormalBalanceByGroup(model.GlAccMainGroup);

                // Preserve audit information
                model.AuditDate = DateTime.Now;
                model.AuditId = User.Identity?.Name ?? "SYSTEM";
                model.TransDate = existing.TransDate; // Keep original transaction date

                _context.Entry(existing).CurrentValues.SetValues(model);
                _context.SaveChanges();

                TempData["Success"] = "Account updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating account: {ex.Message}");
                ViewBag.Accounts = _context.GlSetup.OrderBy(x => x.AccNo).ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                return View("Index", model);
            }
        }

        // ===============================
        // POST: /GlSetup/Delete
        // ===============================
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long glId)
        {
            try
            {
                var account = _context.GlSetup.Find(glId);
                if (account == null)
                    return NotFound();

                // Check if account is being used in transactions
                bool hasTransactions = _context.Gltransactions.Any(x => x.DrAccNo == account.AccNo || x.CrAccNo == account.AccNo);
                if (hasTransactions)
                {
                    TempData["Error"] = "Cannot delete account because it has associated transactions.";
                    return RedirectToAction(nameof(Index));
                }

                _context.GlSetup.Remove(account);
                _context.SaveChanges();

                TempData["Success"] = "Account deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting account: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // Private Methods - Data Sources
        // ===============================
        private List<AccountTypeConfig> GetAccountTypes()
        {
            return new List<AccountTypeConfig>
            {
                new AccountTypeConfig
                {
                    Type = "Income Statement",
                    Groups = new List<AccountGroup>
                    {
                        new AccountGroup { Name = "Expenses", NormalBalance = "DR" },
                        new AccountGroup { Name = "Income", NormalBalance = "CR" }
                    }
                },
                new AccountTypeConfig
                {
                    Type = "Balance Sheet",
                    Groups = new List<AccountGroup>
                    {
                        new AccountGroup { Name = "Assets", NormalBalance = "DR" },
                        new AccountGroup { Name = "Capital Reserved", NormalBalance = "DR" },
                        new AccountGroup { Name = "Liabilities", NormalBalance = "CR" },
                        new AccountGroup { Name = "Retained Earnings", NormalBalance = "CR" },
                        new AccountGroup { Name = "Revenue Reserved", NormalBalance = "CR" },
                        new AccountGroup { Name = "Shareholder Equity", NormalBalance = "CR" }
                    }
                }
            };
        }

        private List<AccountCategory> GetAccountCategories()
        {
            return new List<AccountCategory>
            {
                new AccountCategory { Id = 1, Name = "Operating Income" },
                new AccountCategory { Id = 2, Name = "Operating Assets" },
                new AccountCategory { Id = 3, Name = "Operating Expenses" },
                new AccountCategory { Id = 4, Name = "Operating Liabilities" },
                new AccountCategory { Id = 5, Name = "Investing Activities" }
            };
        }

        private List<Currency> GetCurrencies()
        {
            return new List<Currency>
            {
                new Currency { Id = 1, Code = "KSH", Name = "Kenyan Shilling" },
                new Currency { Id = 2, Code = "USD", Name = "US Dollar" },
                new Currency { Id = 3, Code = "GBP", Name = "British Pound" },
                new Currency { Id = 4, Code = "TSH", Name = "Tanzanian Shilling" },
                new Currency { Id = 5, Code = "USH", Name = "Ugandan Shilling" },
                new Currency { Id = 6, Code = "ZAR", Name = "South African Rand" }
            };
        }

        private List<SubCategory> GetSubCategories()
        {
            return new List<SubCategory>
            {
                new SubCategory { Id = 1, Name = "Loans" },
                new SubCategory { Id = 2, Name = "Interests" },
                new SubCategory { Id = 3, Name = "Shares" },
                new SubCategory { Id = 4, Name = "Others" }
            };
        }

        private string GetNormalBalanceByGroup(string groupName)
        {
            var accountTypes = GetAccountTypes();
            foreach (var type in accountTypes)
            {
                var group = type.Groups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                    return group.NormalBalance;
            }
            return "DR"; // Default
        }
    }

    // ===============================
    // Helper Classes
    // ===============================
    public class AccountTypeConfig
    {
        public string Type { get; set; } = "";
        public List<AccountGroup> Groups { get; set; } = new List<AccountGroup>();
    }

    public class AccountGroup
    {
        public string Name { get; set; } = "";
        public string NormalBalance { get; set; } = "";
    }

    public class AccountCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class Currency
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class SubCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}