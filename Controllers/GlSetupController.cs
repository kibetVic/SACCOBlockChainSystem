using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("GlSetup")]
    public class GlSetupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GlSetupController> _logger;

        public GlSetupController(ApplicationDbContext context, ILogger<GlSetupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper method to get current user's company code from claims
        private string GetCurrentCompanyCode()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            if (string.IsNullOrEmpty(companyCode))
            {
                // Try to get from claims identity
                companyCode = User.Claims.FirstOrDefault(c => c.Type == "CompanyCode")?.Value;
            }
            return companyCode ?? "000"; // Default fallback
        }

        // Helper method to get current user's name
        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "SYSTEM";
        }

        // ===============================
        // GET: /GlSetup
        // ===============================
        [HttpGet("")]
        public IActionResult Index()
        {
            try
            {
                var companyCode = GetCurrentCompanyCode();
                _logger.LogInformation($"Loading GL accounts for company: {companyCode}");

                // Filter accounts by company code
                ViewBag.Accounts = _context.GlSetup
                    .Where(x => x.CompanyCode == companyCode)
                    .OrderBy(x => x.AccNo)
                    .ToList();

                // Populate dropdown lists
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                ViewBag.CompanyCode = companyCode;

                return View(new GlSetup());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GL Setup page");
                TempData["Error"] = "An error occurred while loading the page.";
                return View(new GlSetup());
            }
        }

        // ===============================
        // GET: /GlSetup/GetGroupsByType
        // ===============================
        [HttpGet("GetGroupsByType")]
        public IActionResult GetGroupsByType(string accountType)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by type");
                return Json(new List<object>());
            }
        }

        // ===============================
        // POST: /GlSetup/Save
        // ===============================
        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(GlSetup model)
        {
            try
            {
                var companyCode = GetCurrentCompanyCode();
                var userName = GetCurrentUserName();

                // Set company code from logged-in user
                model.CompanyCode = companyCode;

                // Remove validation for nullable fields
                ModelState.Remove("AuditDate");
                ModelState.Remove("EoyDate");
                ModelState.Remove("NewGlOpeningBalDate");

                if (!ModelState.IsValid)
                {
                    ViewBag.Accounts = _context.GlSetup
                        .Where(x => x.CompanyCode == companyCode)
                        .OrderBy(x => x.AccNo)
                        .ToList();
                    ViewBag.AccountTypes = GetAccountTypes();
                    ViewBag.AccountCategories = GetAccountCategories();
                    ViewBag.Currencies = GetCurrencies();
                    ViewBag.SubCategories = GetSubCategories();
                    return View("Index", model);
                }

                // Set default values
                model.TransDate = DateTime.Now;
                model.Status = true;
                model.AuditDate = DateTime.Now;
                model.AuditId = userName;

                // =========================================================
                // FIX: Set Glcode = AccNo
                // =========================================================
                model.Glcode = model.AccNo;

                // =========================================================
                // FIX: Handle Normal Balance based on Account Group
                // =========================================================
                if (model.GlAccMainGroup == "Capital Reserved")
                {
                    // For Capital Reserved, validate and preserve user-entered value
                    if (string.IsNullOrEmpty(model.Normalbal))
                    {
                        ModelState.AddModelError("Normalbal", "Normal Balance is required for Capital Reserved accounts.");
                        ViewBag.Accounts = _context.GlSetup
                            .Where(x => x.CompanyCode == companyCode)
                            .OrderBy(x => x.AccNo)
                            .ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }

                    // Ensure it's either DR or CR (uppercase)
                    model.Normalbal = model.Normalbal.ToUpper();
                    if (model.Normalbal != "DR" && model.Normalbal != "CR")
                    {
                        ModelState.AddModelError("Normalbal", "Normal Balance must be either DR or CR.");
                        ViewBag.Accounts = _context.GlSetup
                            .Where(x => x.CompanyCode == companyCode)
                            .OrderBy(x => x.AccNo)
                            .ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }
                }
                else
                {
                    // For other groups, auto-set the normal balance
                    model.Normalbal = GetNormalBalanceByGroup(model.GlAccMainGroup);
                }

                // Set default values for required fields
                if (string.IsNullOrEmpty(model.Type)) model.Type = "Balance Sheet";
                if (string.IsNullOrEmpty(model.SubType)) model.SubType = "Others";
                if (model.OpeningBal == 0) model.OpeningBal = 0;
                if (model.NewGlOpeningBal == 0) model.NewGlOpeningBal = 0;
                if (model.NewGlOpeningBalDate == DateTime.MinValue) model.NewGlOpeningBalDate = DateTime.Now;

                // Check if account number already exists for this company
                var existingAccount = _context.GlSetup
                    .FirstOrDefault(x => x.AccNo == model.AccNo && x.CompanyCode == companyCode);

                if (existingAccount != null)
                {
                    ModelState.AddModelError("AccNo", "Account number already exists for this company.");
                    ViewBag.Accounts = _context.GlSetup
                        .Where(x => x.CompanyCode == companyCode)
                        .OrderBy(x => x.AccNo)
                        .ToList();
                    ViewBag.AccountTypes = GetAccountTypes();
                    ViewBag.AccountCategories = GetAccountCategories();
                    ViewBag.Currencies = GetCurrencies();
                    ViewBag.SubCategories = GetSubCategories();
                    return View("Index", model);
                }

                _context.GlSetup.Add(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Account {model.AccNo} saved successfully for company {companyCode} by {userName}");

                // =========================================================
                // BLOCKCHAIN INTEGRATION FOR GL ACCOUNT CREATION
                // =========================================================
                try
                {
                    // Prepare blockchain data
                    var blockchainData = new
                    {
                        Action = "CREATE",
                        TransactionType = "GL_ACCOUNT_CREATION",
                        Glcode = model.Glcode,
                        AccountNo = model.AccNo,
                        Glaccname = model.Glaccname,
                        Glacctype = model.Glacctype,
                        GlAccMainGroup = model.GlAccMainGroup,
                        Normalbal = model.Normalbal,
                        Type = model.Type,
                        SubType = model.SubType,
                        OpeningBal = model.OpeningBal,
                        Status = model.Status,
                        IsSuspense = model.IsSuspense,
                        CreatedBy = userName,
                        CreatedAt = DateTime.Now,
                        CompanyCode = companyCode
                    };

                    // Generate block hash
                    string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                    if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                    else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                    // Create Block record
                    var block = new Block
                    {
                        BlockHash = blockHash,
                        PreviousHash = await GetLastBlockHashAsync(),
                        Timestamp = DateTime.Now,
                        Nonce = 0,
                        MerkleRoot = Guid.NewGuid().ToString(),
                        Confirmed = true,
                        CreatedAt = DateTime.Now
                    };

                    _context.Blocks.Add(block);
                    await _context.SaveChangesAsync();

                    // Generate data hash
                    var payloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData);
                    var dataHash = ComputeSHA256Hash(payloadJson);

                    // Create Blockchain Transaction
                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "GL_ACCOUNT_CREATION",
                        MemberNo = null,
                        CompanyCode = companyCode,
                        Amount = model.OpeningBal,
                        Timestamp = DateTime.Now,
                        DataHash = dataHash,
                        PayloadJson = payloadJson,
                        OffChainReferenceId = model.AccNo,
                        Status = "CONFIRMED",
                        BlockHash = block.BlockHash,
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    await _context.SaveChangesAsync();

                    // Update the GlSetup record with BlockchainTxId
                    model.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Blockchain transaction recorded for GL Account {model.AccNo}: {blockchainTx.TransactionId}");
                }
                catch (Exception blockchainEx)
                {
                    _logger.LogError(blockchainEx, $"Error recording blockchain transaction for GL Account {model.AccNo}, but account was saved");
                    // Don't throw - account was saved successfully, blockchain recording failed
                }

                TempData["Success"] = "Account saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving account");
                ModelState.AddModelError("", $"Error saving account: {ex.Message}");

                var companyCode = GetCurrentCompanyCode();
                ViewBag.Accounts = _context.GlSetup
                    .Where(x => x.CompanyCode == companyCode)
                    .OrderBy(x => x.AccNo)
                    .ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();
                return View("Index", model);
            }
        }


        // ===============================
        // POST: /GlSetup/Update
        // ===============================
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(GlSetup model)
        {
            try
            {
                var companyCode = GetCurrentCompanyCode();
                var userName = GetCurrentUserName();

                // Remove validation for nullable fields
                ModelState.Remove("AuditDate");
                ModelState.Remove("EoyDate");

                if (!ModelState.IsValid)
                {
                    ViewBag.Accounts = _context.GlSetup
                        .Where(x => x.CompanyCode == companyCode)
                        .OrderBy(x => x.AccNo)
                        .ToList();
                    ViewBag.AccountTypes = GetAccountTypes();
                    ViewBag.AccountCategories = GetAccountCategories();
                    ViewBag.Currencies = GetCurrencies();
                    ViewBag.SubCategories = GetSubCategories();
                    return View("Index", model);
                }

                // Find existing account with company code check
                var existing = _context.GlSetup
                    .FirstOrDefault(x => x.GlId == model.GlId && x.CompanyCode == companyCode);

                if (existing == null)
                {
                    TempData["Error"] = "Account not found or you don't have permission to update it.";
                    return RedirectToAction(nameof(Index));
                }

                // Store old values for blockchain audit
                var oldValues = new
                {
                    existing.Glcode,
                    existing.AccNo,
                    existing.Glaccname,
                    existing.Glacctype,
                    existing.GlAccMainGroup,
                    existing.Normalbal,
                    existing.Type,
                    existing.SubType,
                    existing.OpeningBal,
                    existing.Status,
                    existing.IsSuspense
                };

                // Check if account number is being changed and if it already exists in this company
                if (existing.AccNo != model.AccNo)
                {
                    var duplicateAccount = _context.GlSetup
                        .FirstOrDefault(x => x.AccNo == model.AccNo && x.CompanyCode == companyCode && x.GlId != model.GlId);

                    if (duplicateAccount != null)
                    {
                        ModelState.AddModelError("AccNo", "Account number already exists for this company.");
                        ViewBag.Accounts = _context.GlSetup
                            .Where(x => x.CompanyCode == companyCode)
                            .OrderBy(x => x.AccNo)
                            .ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }
                }

                // =========================================================
                // FIX: Set Glcode = AccNo (update if AccNo changed)
                // =========================================================
                model.Glcode = model.AccNo;

                // =========================================================
                // FIX: Handle Normal Balance based on Account Group
                // =========================================================
                if (model.GlAccMainGroup == "Capital Reserved")
                {
                    // For Capital Reserved, keep the user-entered value (DR or CR)
                    // Don't overwrite it - just validate it
                    if (string.IsNullOrEmpty(model.Normalbal))
                    {
                        ModelState.AddModelError("Normalbal", "Normal Balance is required for Capital Reserved accounts.");
                        ViewBag.Accounts = _context.GlSetup
                            .Where(x => x.CompanyCode == companyCode)
                            .OrderBy(x => x.AccNo)
                            .ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }

                    // Ensure it's either DR or CR (uppercase)
                    model.Normalbal = model.Normalbal.ToUpper();
                    if (model.Normalbal != "DR" && model.Normalbal != "CR")
                    {
                        ModelState.AddModelError("Normalbal", "Normal Balance must be either DR or CR.");
                        ViewBag.Accounts = _context.GlSetup
                            .Where(x => x.CompanyCode == companyCode)
                            .OrderBy(x => x.AccNo)
                            .ToList();
                        ViewBag.AccountTypes = GetAccountTypes();
                        ViewBag.AccountCategories = GetAccountCategories();
                        ViewBag.Currencies = GetCurrencies();
                        ViewBag.SubCategories = GetSubCategories();
                        return View("Index", model);
                    }
                }
                else
                {
                    // For other groups, auto-set the normal balance
                    model.Normalbal = GetNormalBalanceByGroup(model.GlAccMainGroup);
                }

                // Preserve audit information
                model.Status = true;
                model.AuditDate = DateTime.Now;
                model.AuditId = userName;
                model.TransDate = existing.TransDate; // Keep original transaction date
                model.CompanyCode = companyCode; // Ensure company code remains the same

                _context.Entry(existing).CurrentValues.SetValues(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Account {model.AccNo} updated successfully for company {companyCode} by {userName}");

                // =========================================================
                // BLOCKCHAIN INTEGRATION FOR GL ACCOUNT UPDATE
                // =========================================================
                try
                {
                    // Prepare blockchain data
                    var blockchainData = new
                    {
                        Action = "UPDATE",
                        TransactionType = "GL_ACCOUNT_UPDATE",
                        OldValues = oldValues,
                        NewValues = new
                        {
                            Glcode = model.Glcode,
                            AccountNo = model.AccNo,
                            Glaccname = model.Glaccname,
                            Glacctype = model.Glacctype,
                            GlAccMainGroup = model.GlAccMainGroup,
                            Normalbal = model.Normalbal,
                            Type = model.Type,
                            SubType = model.SubType,
                            OpeningBal = model.OpeningBal,
                            Status = model.Status,
                            IsSuspense = model.IsSuspense
                        },
                        UpdatedBy = userName,
                        UpdatedAt = DateTime.Now,
                        CompanyCode = companyCode,
                        PreviousBlockchainTxId = existing.BlockchainTxId
                    };

                    // Generate block hash
                    string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                    if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                    else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                    // Get previous block hash
                    var lastBlock = await _context.Blocks
                        .OrderByDescending(b => b.BlockId)
                        .FirstOrDefaultAsync();

                    string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                    // Create Block record
                    var block = new Block
                    {
                        BlockHash = blockHash,
                        PreviousHash = previousHash,
                        Timestamp = DateTime.Now,
                        Nonce = 0,
                        MerkleRoot = Guid.NewGuid().ToString(),
                        Confirmed = true,
                        CreatedAt = DateTime.Now
                    };

                    _context.Blocks.Add(block);
                    await _context.SaveChangesAsync();

                    // Generate data hash
                    var payloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData);
                    var dataHash = ComputeSHA256Hash(payloadJson);

                    // Create Blockchain Transaction
                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "GL_ACCOUNT_UPDATE",
                        MemberNo = null,
                        CompanyCode = companyCode,
                        Amount = model.OpeningBal,
                        Timestamp = DateTime.Now,
                        DataHash = dataHash,
                        PayloadJson = payloadJson,
                        OffChainReferenceId = model.AccNo,
                        Status = "CONFIRMED",
                        BlockHash = block.BlockHash,
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    await _context.SaveChangesAsync();

                    // Update the GlSetup record with new BlockchainTxId
                    existing.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Blockchain transaction recorded for GL Account update {model.AccNo}: {blockchainTx.TransactionId}");
                }
                catch (Exception blockchainEx)
                {
                    _logger.LogError(blockchainEx, $"Error recording blockchain transaction for GL Account update {model.AccNo}, but account was updated");
                    // Don't throw - account was updated successfully, blockchain recording failed
                }

                TempData["Success"] = "Account updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account");
                ModelState.AddModelError("", $"Error updating account: {ex.Message}");

                var companyCode = GetCurrentCompanyCode();
                ViewBag.Accounts = _context.GlSetup
                    .Where(x => x.CompanyCode == companyCode)
                    .OrderBy(x => x.AccNo)
                    .ToList();
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
            try
            {
                var companyCode = GetCurrentCompanyCode();

                var account = _context.GlSetup
                    .FirstOrDefault(x => x.GlId == id && x.CompanyCode == companyCode);

                if (account == null)
                {
                    TempData["Error"] = "Account not found or you don't have permission to edit it.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Accounts = _context.GlSetup
                    .Where(x => x.CompanyCode == companyCode)
                    .OrderBy(x => x.AccNo)
                    .ToList();
                ViewBag.AccountTypes = GetAccountTypes();
                ViewBag.AccountCategories = GetAccountCategories();
                ViewBag.Currencies = GetCurrencies();
                ViewBag.SubCategories = GetSubCategories();

                return View("Index", account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading account for edit: {id}");
                TempData["Error"] = "An error occurred while loading the account.";
                return RedirectToAction(nameof(Index));
            }
        }



        // =========================================================
        // HELPER METHODS FOR BLOCKCHAIN
        // =========================================================

        private async Task<string> GetLastBlockHashAsync()
        {
            try
            {
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();

                return lastBlock?.BlockHash ?? "0".PadLeft(64, '0');
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting last block hash, using default");
                return "0".PadLeft(64, '0');
            }
        }

        private string ComputeSHA256Hash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long glId)
        {
            try
            {
                var companyCode = GetCurrentCompanyCode();
                var userName = GetCurrentUserName();

                var account = _context.GlSetup
                    .FirstOrDefault(x => x.GlId == glId && x.CompanyCode == companyCode);

                if (account == null)
                {
                    TempData["Error"] = "Account not found or you don't have permission to delete it.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if account is being used in transactions (with company code filter)
                bool hasTransactions = _context.Gltransactions
                    .Any(x => (x.DrAccNo == account.AccNo || x.CrAccNo == account.AccNo) &&
                              x.CompanyCode == companyCode);

                if (hasTransactions)
                {
                    TempData["Error"] = "Cannot delete account because it has associated transactions.";
                    return RedirectToAction(nameof(Index));
                }

                _context.GlSetup.Remove(account);
                _context.SaveChanges();

                _logger.LogInformation($"Account {account.AccNo} deleted successfully for company {companyCode} by {userName}");
                TempData["Success"] = "Account deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account");
                TempData["Error"] = $"Error deleting account: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // GET: /GlSetup/GetAccountsForDropdown
        // Used by LoanType controller to get accounts for dropdowns
        // ===============================
        [HttpGet("GetAccountsForDropdown")]
        public async Task<IActionResult> GetAccountsForDropdown()
        {
            try
            {
                var companyCode = GetCurrentCompanyCode();

                var accounts = await _context.GlSetup
                    .Where(a => a.CompanyCode == companyCode && a.Status == true)
                    .OrderBy(a => a.AccNo)
                    .Select(a => new
                    {
                        Value = a.AccNo,
                        Text = $"{a.AccNo} - {a.Glaccname}"
                    })
                    .ToListAsync();

                return Json(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for dropdown");
                return Json(new List<object>());
            }
        }

        // ===============================
        // Private Methods - Data Sources
        // ===============================

        private List<AccountTypeConfig> GetAccountTypes()
        {
            return _context.GLAccountTypes
                .Select(at => new AccountTypeConfig
                {
                    Type = at.Type,
                    Groups = _context.GLAccountGroups
                        .Where(g => g.AccountTypeId == at.Id)
                        .Select(g => new AccountGroup
                        {
                            Name = g.Name,
                            NormalBalance = g.NormalBalance
                        })
                        .ToList()
                })
                .ToList();
        }

        private List<AccountSubCategory> GetAccountSubCategories(string groupName)
        {
            return _context.GLAccSubCatego
                .Where(x => x.GroupName == groupName)
                .Select(x => new AccountSubCategory
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code
                })
                .ToList();
        }


        [HttpGet("GetSubCategoriesByGroup")]
        public IActionResult GetSubCategoriesByGroup(string groupName)
        {
            try
            {
                var subCategories = GetAccountSubCategories(groupName);
                return Json(subCategories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sub categories for group: {groupName}");
                return Json(new List<AccountSubCategory>());
            }
        }

        private List<AccountCategory> GetAccountCategories()
        {
            return new List<AccountCategory>
    {
        new AccountCategory { Id = 1, Name = "BOSA" },
        new AccountCategory { Id = 2, Name = "Operating Income" },
        new AccountCategory { Id = 3, Name = "Operating Assets" },
        new AccountCategory { Id = 4, Name = "Operating Expenses" },
        new AccountCategory { Id = 5, Name = "Operating Liabilities" },
        new AccountCategory { Id = 6, Name = "Investing Activities" }
    };
        }

        private List<Currency> GetCurrencies()
        {
            return new List<Currency>
    {
        new Currency { Id = 1, Code = "KSH", Name = "Kenyan Shilling" },
        //new Currency { Id = 2, Code = "USD", Name = "US Dollar" },
        //new Currency { Id = 3, Code = "GBP", Name = "British Pound" },
        //new Currency { Id = 4, Code = "TSH", Name = "Tanzanian Shilling" },
        //new Currency { Id = 5, Code = "USH", Name = "Ugandan Shilling" },
        //new Currency { Id = 6, Code = "ZAR", Name = "South African Rand" }
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

        // NEW: Account SubCategory Class
        public class AccountSubCategory
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
        }
    }
}