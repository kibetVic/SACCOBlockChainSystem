// Controllers/PaymentTypeController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    public class PaymentTypeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<PaymentTypeController> _logger;

        public PaymentTypeController(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<PaymentTypeController> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
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
                if (userAccount != null && !string.IsNullOrEmpty(userAccount.UserName))
                {
                    return userAccount.UserName;
                }

                // Also try UserLoginId as fallback
                if (userAccount != null && !string.IsNullOrEmpty(userAccount.UserLoginId))
                {
                    return userAccount.UserLoginId;
                }
            }

            return "SYSTEM";
        }

        // GET: PaymentType
        public async Task<IActionResult> Index()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var companyName = User.FindFirst("CompanyName")?.Value ?? "JUHUDI SACCO";

            // Get GL Accounts for dropdown
            var glAccounts = await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .OrderBy(g => g.AccNo)
                .Select(g => new GlSetup
                {
                    AccNo = g.AccNo,
                    Glaccname = g.Glaccname
                })
                .ToListAsync();

            // Get existing Payment Types
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Code)
                .Select(p => new PaymentTypeViewModel
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    IsActive = p.IsActive,
                    DisplayOrder = p.DisplayOrder,
                    DefaultExpenseAccountNo = p.DefaultExpenseAccountNo,
                    CreatedAt = p.AuditTime,
                    CreatedBy = p.AuditId
                })
                .ToListAsync();

            // Get expense account names for display
            foreach (var pt in paymentTypes)
            {
                if (!string.IsNullOrEmpty(pt.DefaultExpenseAccountNo))
                {
                    var expenseAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == pt.DefaultExpenseAccountNo && g.CompanyCode == companyCode);
                    pt.DefaultExpenseAccountName = expenseAccount?.Glaccname;
                }
            }

            ViewBag.GlAccounts = glAccounts;
            ViewBag.PaymentTypes = paymentTypes;
            ViewBag.CompanyName = companyName;

            var currentUser = await GetCurrentUsernameAsync();

            return View(new PaymentTypeCreateDTO
            {
                CompanyCode = companyCode,
                CreatedBy = currentUser,
                IsActive = true,
                DisplayOrder = 0
            });
        }

        // POST: PaymentType/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentTypeCreateDTO model, string action)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var userName = await GetCurrentUsernameAsync();

            try
            {
                if (action == "save")
                {
                    // Validate model
                    if (!ModelState.IsValid)
                    {
                        TempData["ErrorMessage"] = "Please correct the validation errors.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Check for duplicate code
                    var exists = await _context.PaymentTypes
                        .AnyAsync(p => p.Code == model.Code.ToUpper() && p.CompanyCode == companyCode);

                    if (exists)
                    {
                        TempData["ErrorMessage"] = $"Payment type code '{model.Code}' already exists. Please use a unique code.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Validate expense account if provided
                    string expenseAccountName = null;
                    if (!string.IsNullOrEmpty(model.DefaultExpenseAccountNo))
                    {
                        var expenseAccount = await _context.GlSetup
                            .FirstOrDefaultAsync(g => g.AccNo == model.DefaultExpenseAccountNo && g.CompanyCode == companyCode);

                        if (expenseAccount == null)
                        {
                            TempData["ErrorMessage"] = $"Expense account '{model.DefaultExpenseAccountNo}' not found.";
                            return RedirectToAction(nameof(Index));
                        }
                        expenseAccountName = expenseAccount.Glaccname;
                    }

                    // Create new payment type
                    var paymentType = new PaymentType
                    {
                        Code = model.Code?.ToUpper(),
                        Name = model.Name,
                        Description = model.Description,
                        IsActive = model.IsActive,
                        DisplayOrder = model.DisplayOrder,
                        DefaultExpenseAccountNo = model.DefaultExpenseAccountNo,
                        CompanyCode = companyCode,
                        AuditId = userName,  // Now stores username, not ID
                        AuditTime = DateTime.Now
                    };

                    _context.PaymentTypes.Add(paymentType);
                    await _context.SaveChangesAsync();

                    // Record blockchain transaction
                    try
                    {
                        var blockchainData = new
                        {
                            Action = "CREATE_PAYMENT_TYPE",
                            PaymentType = new
                            {
                                paymentType.Code,
                                paymentType.Name,
                                paymentType.Description,
                                paymentType.IsActive,
                                paymentType.DisplayOrder,
                                DefaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo,
                                DefaultExpenseAccountName = expenseAccountName
                            },
                            CreatedBy = userName,
                            CreatedAt = DateTime.Now
                        };

                        var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                            "PAYMENT_TYPE_CREATE",
                            null,
                            companyCode,
                            0,
                            "SYSTEM",
                            blockchainData);

                        if (blockchainTx != null)
                        {
                            paymentType.BlockchainTxId = blockchainTx.TransactionId;
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to record blockchain transaction for payment type creation");
                    }

                    TempData["SuccessMessage"] = $"Payment type '{model.Code} - {model.Name}' created successfully!";
                }
                else if (action == "update")
                {
                    return await Update(model, userName, companyCode);
                }
                else if (action == "delete")
                {
                    return await Delete(model.Code, userName, companyCode);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PaymentType Create/Update/Delete");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<IActionResult> Update(PaymentTypeCreateDTO model, string userName, string companyCode)
        {
            // Find existing payment type by code
            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Code == model.Code.ToUpper() && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                TempData["ErrorMessage"] = $"Payment type with code '{model.Code}' not found.";
                return RedirectToAction(nameof(Index));
            }

            // Store old values for audit
            var oldValues = new
            {
                paymentType.Code,
                paymentType.Name,
                paymentType.Description,
                paymentType.IsActive,
                paymentType.DisplayOrder,
                paymentType.DefaultExpenseAccountNo
            };

            // Validate expense account if provided
            string expenseAccountName = null;
            if (!string.IsNullOrEmpty(model.DefaultExpenseAccountNo))
            {
                var expenseAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(g => g.AccNo == model.DefaultExpenseAccountNo && g.CompanyCode == companyCode);

                if (expenseAccount == null)
                {
                    TempData["ErrorMessage"] = $"Expense account '{model.DefaultExpenseAccountNo}' not found.";
                    return RedirectToAction(nameof(Index));
                }
                expenseAccountName = expenseAccount.Glaccname;
            }

            // Update fields
            paymentType.Name = model.Name;
            paymentType.Description = model.Description;
            paymentType.IsActive = model.IsActive;
            paymentType.DisplayOrder = model.DisplayOrder;
            paymentType.DefaultExpenseAccountNo = model.DefaultExpenseAccountNo;
            paymentType.AuditId = userName;  // Store username
            paymentType.AuditTime = DateTime.Now;

            await _context.SaveChangesAsync();

            // Record blockchain transaction
            try
            {
                var blockchainData = new
                {
                    Action = "UPDATE_PAYMENT_TYPE",
                    PaymentTypeId = paymentType.Id,
                    OldValues = oldValues,
                    NewValues = new
                    {
                        paymentType.Name,
                        paymentType.Description,
                        paymentType.IsActive,
                        paymentType.DisplayOrder,
                        paymentType.DefaultExpenseAccountNo,
                        DefaultExpenseAccountName = expenseAccountName
                    },
                    ModifiedBy = userName,
                    ModifiedAt = DateTime.Now
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "PAYMENT_TYPE_UPDATE",
                    null,
                    companyCode,
                    0,
                    "SYSTEM",
                    blockchainData);

                if (blockchainTx != null)
                {
                    paymentType.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record blockchain transaction for payment type update");
            }

            TempData["SuccessMessage"] = $"Payment type '{model.Code} - {model.Name}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> Delete(string code, string userName, string companyCode)
        {
            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Code == code.ToUpper() && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                TempData["ErrorMessage"] = $"Payment type with code '{code}' not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if payment type is in use
            var isInUse = await _context.Journals
                .AnyAsync(j => j.SHARETYPE == paymentType.Name && j.CompanyCode == companyCode);

            if (isInUse)
            {
                TempData["ErrorMessage"] = $"Cannot delete payment type '{paymentType.Name}' as it is being used in existing payments. Please deactivate it instead.";
                return RedirectToAction(nameof(Index));
            }

            var paymentTypeDetails = new
            {
                paymentType.Id,
                paymentType.Code,
                paymentType.Name,
                paymentType.DefaultExpenseAccountNo
            };

            _context.PaymentTypes.Remove(paymentType);
            await _context.SaveChangesAsync();

            // Record blockchain transaction
            try
            {
                var blockchainData = new
                {
                    Action = "DELETE_PAYMENT_TYPE",
                    PaymentTypeDetails = paymentTypeDetails,
                    DeletedBy = userName,
                    DeletedAt = DateTime.Now
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    "PAYMENT_TYPE_DELETE",
                    null,
                    companyCode,
                    0,
                    "SYSTEM",
                    blockchainData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record blockchain transaction for payment type deletion");
            }

            TempData["SuccessMessage"] = $"Payment type '{paymentType.Code} - {paymentType.Name}' deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: PaymentType/GetPaymentTypeDetails
        [HttpGet]
        public async Task<IActionResult> GetPaymentTypeDetails(string code)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Code == code.ToUpper() && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                return Json(new { success = false, message = "Payment type not found" });
            }

            string expenseAccountName = null;
            if (!string.IsNullOrEmpty(paymentType.DefaultExpenseAccountNo))
            {
                var expenseAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(g => g.AccNo == paymentType.DefaultExpenseAccountNo);
                expenseAccountName = expenseAccount?.Glaccname;
            }

            return Json(new
            {
                success = true,
                paymentType = new
                {
                    paymentType.Id,
                    paymentType.Code,
                    paymentType.Name,
                    paymentType.Description,
                    paymentType.IsActive,
                    paymentType.DisplayOrder,
                    paymentType.DefaultExpenseAccountNo,
                    DefaultExpenseAccountName = expenseAccountName,
                    paymentType.AuditTime,
                    paymentType.AuditId
                }
            });
        }

        // GET: PaymentType/ToggleStatus
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var userName = await GetCurrentUsernameAsync();

            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                return Json(new { success = false, message = "Payment type not found" });
            }

            paymentType.IsActive = !paymentType.IsActive;
            paymentType.AuditId = userName;  // Store username
            paymentType.AuditTime = DateTime.Now;
            await _context.SaveChangesAsync();

            // Record blockchain transaction
            try
            {
                var blockchainData = new
                {
                    Action = paymentType.IsActive ? "ACTIVATE_PAYMENT_TYPE" : "DEACTIVATE_PAYMENT_TYPE",
                    PaymentTypeId = paymentType.Id,
                    PaymentTypeCode = paymentType.Code,
                    PaymentTypeName = paymentType.Name,
                    ModifiedBy = userName,
                    ModifiedAt = DateTime.Now
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    paymentType.IsActive ? "PAYMENT_TYPE_ACTIVATE" : "PAYMENT_TYPE_DEACTIVATE",
                    null,
                    companyCode,
                    0,
                    "SYSTEM",
                    blockchainData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record blockchain transaction");
            }

            return Json(new { success = true, isActive = paymentType.IsActive, message = $"Payment type {(paymentType.IsActive ? "activated" : "deactivated")} successfully" });
        }

        // GET: PaymentType/GetPaymentTypesList
        [HttpGet]
        public async Task<IActionResult> GetPaymentTypesList()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new { p.Id, p.Code, p.Name, p.DefaultExpenseAccountNo })
                .ToListAsync();
            return Json(paymentTypes);
        }
    }
}