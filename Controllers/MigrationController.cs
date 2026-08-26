// Controllers/MigrationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    //[Authorize(Roles = "Super Admin")]
    [Authorize]
    public class MigrationController : Controller
    {
        private readonly IMigrationService _migrationService;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(
            IMigrationService migrationService,
            ILogger<MigrationController> logger)
        {
            _migrationService = migrationService;
            _logger = logger;
        }

        // GET: /Migration/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var summary = await _migrationService.GetMigrationSummaryAsync();
                return View(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading migration dashboard");
                TempData["ErrorMessage"] = $"Error loading migration dashboard: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: /Migration/MigrateAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MigrateAll()
        {
            try
            {
                _logger.LogInformation("Starting full migration via admin request");

                // Run migration in background to avoid timeout
                var result = await _migrationService.MigrateAllWalletsAndTransactionsAsync();

                TempData["MigrationResult"] = System.Text.Json.JsonSerializer.Serialize(result);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                    TempData["MigrationErrors"] = string.Join("\n", result.Errors);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during full migration");
                TempData["ErrorMessage"] = $"Migration failed: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Migration/MigrateMember
        public IActionResult MigrateMember()
        {
            return View();
        }

        // POST: /Migration/MigrateMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MigrateMember(string memberNo)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["ErrorMessage"] = "Please enter a member number";
                    return View();
                }

                _logger.LogInformation($"Starting migration for member: {memberNo}");

                var result = await _migrationService.MigrateMemberWalletsAndTransactionsAsync(memberNo);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    TempData["MigrationDetails"] = $"Wallets: {result.WalletsMigrated}, Transactions: {result.TransactionsReSigned}, Chain Links: {result.ChainLinksFixed}";
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                    if (result.Errors.Any())
                    {
                        TempData["MigrationErrors"] = string.Join("\n", result.Errors);
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during migration for member {memberNo}");
                TempData["ErrorMessage"] = $"Migration failed: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Migration/RegenerateWallet
        public IActionResult RegenerateWallet()
        {
            return View();
        }

        // POST: /Migration/RegenerateWallet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateWallet(string memberNo)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["ErrorMessage"] = "Please enter a member number";
                    return View();
                }

                var result = await _migrationService.RegenerateWalletForMemberAsync(memberNo);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error regenerating wallet for {memberNo}");
                TempData["ErrorMessage"] = $"Failed: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Migration/Status
        [HttpGet]
        public async Task<IActionResult> Status()
        {
            try
            {
                var summary = await _migrationService.GetMigrationSummaryAsync();
                return Json(new
                {
                    success = true,
                    totalMembers = summary.TotalMembers,
                    membersWithWallets = summary.MembersWithWallets,
                    membersWithoutWallets = summary.MembersWithoutWallets,
                    totalTransactions = summary.TotalTransactions,
                    transactionsWithSignatures = summary.TransactionsWithSignatures,
                    transactionsWithoutSignatures = summary.TransactionsWithoutSignatures,
                    validChains = summary.ValidChains,
                    brokenChains = summary.BrokenChains
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}