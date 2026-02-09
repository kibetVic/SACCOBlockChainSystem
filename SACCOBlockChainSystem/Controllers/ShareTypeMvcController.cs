// Controllers/ShareTypeMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class ShareTypeMvcController : Controller
    {
        private readonly IShareTypeService _shareTypeService;
        private readonly ILogger<ShareTypeMvcController> _logger;

        public ShareTypeMvcController(
            IShareTypeService shareTypeService,
            ILogger<ShareTypeMvcController> logger)
        {
            _shareTypeService = shareTypeService;
            _logger = logger;
        }

        // GET: /ShareTypeMvc/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _shareTypeService.GetShareTypesByCompanyAsync(companyCode);

                ViewBag.TotalShareTypes = shareTypes.Count;
                ViewBag.MainShareTypes = shareTypes.Count(st => st.IsMainShares);
                ViewBag.TotalMembersUsing = shareTypes.Sum(st => st.TotalMembers);

                return View(shareTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading share types index");
                return View("Error");
            }
        }

        // GET: /ShareTypeMvc/Create
        public IActionResult Create()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypeDto = new ShareTypeCreateDTO
                {
                    CompanyCode = companyCode,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM",
                    IsMainShares = true,
                    Priority = 1,
                    MinAmount = 0,
                    Ppacc = "200000", // Default PP account
                    LowerLimit = 0,
                    ElseRatio = 0
                };

                return View(shareTypeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create share type form");
                return View("Error");
            }
        }

        // POST: /ShareTypeMvc/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShareTypeCreateDTO shareTypeDto)
        {
            try
            {
                _logger.LogInformation("Creating share type");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for share type creation");
                    return View(shareTypeDto);
                }

                // Set user information
                shareTypeDto.CompanyCode = GetUserCompanyCode();
                shareTypeDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _shareTypeService.CreateShareTypeAsync(shareTypeDto);

                TempData["SuccessMessage"] = $"Share type '{result.SharesType}' created successfully!";
                return RedirectToAction("Details", new { sharesCode = result.SharesCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating share type");

                if (ex.Message.Contains("already exists") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("required"))
                {
                    ModelState.AddModelError("", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                return View(shareTypeDto);
            }
        }

        // GET: /ShareTypeMvc/Edit/{sharesCode}
        public async Task<IActionResult> Edit(string sharesCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, companyCode);

                // Convert to UpdateDTO
                var updateDto = new ShareTypeUpdateDTO
                {
                    SharesCode = shareType.SharesCode,
                    SharesType = shareType.SharesType,
                    SharesAcc = shareType.SharesAcc,
                    ContraAcc = shareType.ContraAcc,
                    PlacePeriod = shareType.PlacePeriod,
                    LoanToShareRatio = shareType.LoanToShareRatio,
                    Issharecapital = shareType.Issharecapital,
                    Interest = shareType.Interest,
                    MaxAmount = shareType.MaxAmount,
                    Guarantor = shareType.Guarantor,
                    IsMainShares = shareType.IsMainShares,
                    UsedToGuarantee = shareType.UsedToGuarantee,
                    UsedToOffset = shareType.UsedToOffset,
                    Withdrawable = shareType.Withdrawable,
                    Loanquaranto = shareType.Loanquaranto,
                    Priority = shareType.Priority,
                    MinAmount = shareType.MinAmount,
                    Ppacc = shareType.Ppacc,
                    LowerLimit = shareType.LowerLimit,
                    ElseRatio = shareType.ElseRatio,
                    CompanyCode = shareType.CompanyCode,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for share type {sharesCode}");
                return View("Error");
            }
        }

        // POST: /ShareTypeMvc/Edit/{sharesCode}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string sharesCode, ShareTypeUpdateDTO shareTypeDto)
        {
            try
            {
                _logger.LogInformation($"Updating share type: {sharesCode}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for share type update");
                    return View(shareTypeDto);
                }

                shareTypeDto.CompanyCode = GetUserCompanyCode();
                shareTypeDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _shareTypeService.UpdateShareTypeAsync(sharesCode, shareTypeDto);

                TempData["SuccessMessage"] = $"Share type '{result.SharesType}' updated successfully!";
                return RedirectToAction("Details", new { sharesCode = result.SharesCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating share type {sharesCode}");

                if (ex.Message.Contains("not found") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("Cannot change") ||
                    ex.Message.Contains("in use"))
                {
                    ModelState.AddModelError("", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                return View(shareTypeDto);
            }
        }

        // GET: /ShareTypeMvc/Details/{sharesCode}
        public async Task<IActionResult> Details(string sharesCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, companyCode);

                return View(shareType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading share type details {sharesCode}");
                return View("Error");
            }
        }

        // GET: /ShareTypeMvc/Delete/{sharesCode}
        public async Task<IActionResult> Delete(string sharesCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareType = await _shareTypeService.GetShareTypeByCodeAsync(sharesCode, companyCode);
                var usageCount = await _shareTypeService.GetShareTypeUsageCountAsync(sharesCode, companyCode);

                ViewBag.UsageCount = usageCount;
                ViewBag.CanDelete = usageCount == 0;

                return View(shareType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading delete confirmation for share type {sharesCode}");
                return View("Error");
            }
        }

        // POST: /ShareTypeMvc/Delete/{sharesCode}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string sharesCode)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                await _shareTypeService.DeleteShareTypeAsync(sharesCode, companyCode);

                TempData["SuccessMessage"] = $"Share type '{sharesCode}' deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting share type {sharesCode}");

                if (ex.Message.Contains("in use") || ex.Message.Contains("not found"))
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                }

                return RedirectToAction("Delete", new { sharesCode });
            }
        }

        // GET: /ShareTypeMvc/Search
        public IActionResult Search()
        {
            return View();
        }

        // GET: /ShareTypeMvc/SearchResults
        [HttpGet]
        public async Task<IActionResult> SearchResults(string searchTerm)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _shareTypeService.SearchShareTypesAsync(searchTerm, companyCode);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.ResultCount = shareTypes.Count;

                return View(shareTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching share types with term: {searchTerm}");
                return View("Error");
            }
        }

        // GET: /ShareTypeMvc/Report
        public async Task<IActionResult> Report()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _shareTypeService.GetShareTypesByCompanyAsync(companyCode);

                var reportData = new
                {
                    TotalShareTypes = shareTypes.Count,
                    MainShareTypes = shareTypes.Count(st => st.IsMainShares),
                    TotalMembers = shareTypes.Sum(st => st.TotalMembers),
                    TotalShares = shareTypes.Sum(st => st.TotalShares),
                    ShareTypesByPriority = shareTypes
                        .GroupBy(st => st.Priority)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            Priority = g.Key,
                            Count = g.Count(),
                            Types = g.Select(st => st.SharesType)
                        }),
                    UsageStatistics = shareTypes.Select(st => new
                    {
                        st.SharesType,
                        st.TotalMembers,
                        st.TotalShares,
                        UsagePercentage = shareTypes.Sum(x => x.TotalMembers) > 0 ?
                            (st.TotalMembers * 100.0 / shareTypes.Sum(x => x.TotalMembers)) : 0
                    })
                };

                return View(reportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading share types report");
                return View("Error");
            }
        }

        private string GetUserCompanyCode()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }
    }
}