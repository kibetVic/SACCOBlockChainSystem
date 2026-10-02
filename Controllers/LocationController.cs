using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LocationController : Controller
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(ILocationService locationService, ILogger<LocationController> logger)
        {
            _locationService = locationService;
            _logger = logger;
        }

        // ============================================================
        // COUNTY ACTIONS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Counties()
        {
            try
            {
                var counties = await _locationService.GetAllCountiesAsync();
                ViewBag.ActiveTab = "Counties";
                return View(counties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading counties page");
                TempData["ErrorMessage"] = "Error loading counties. Please try again.";
                return View(new CountyListResponseDTO());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCounty(CreateCountyDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please correct the errors in the form.";
                    var counties = await _locationService.GetAllCountiesAsync();
                    ViewBag.ActiveTab = "Counties";
                    return View("Counties", counties);
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.CreatedBy = currentUser;

                var result = await _locationService.CreateCountyAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                   // 
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Counties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating county");
                TempData["ErrorMessage"] = $"Error creating county: {ex.Message}";
                return RedirectToAction(nameof(Counties));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCounty(UpdateCountyDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please correct the errors in the form.";
                    return RedirectToAction(nameof(Counties));
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.ModifiedBy = currentUser;

                var result = await _locationService.UpdateCountyAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Counties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating county");
                TempData["ErrorMessage"] = $"Error updating county: {ex.Message}";
                return RedirectToAction(nameof(Counties));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCounty(int id)
        {
            try
            {
                var currentUser = User.Identity?.Name ?? "System";
                var result = await _locationService.DeleteCountyAsync(id, currentUser);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Counties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting county");
                TempData["ErrorMessage"] = $"Error deleting county: {ex.Message}";
                return RedirectToAction(nameof(Counties));
            }
        }

        // ============================================================
        // SUBCOUNTY ACTIONS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> SubCounties(int? countyId = null)
        {
            try
            {
                var subCounties = await _locationService.GetAllSubCountiesAsync(countyId);
                var counties = await _locationService.GetAllCountiesAsync();

                ViewBag.ActiveTab = "SubCounties";
                ViewBag.Counties = counties.Counties;
                ViewBag.SelectedCountyId = countyId;

                return View(subCounties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sub-counties page");
                TempData["ErrorMessage"] = "Error loading sub-counties. Please try again.";
                return View(new SubCountyListResponseDTO());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubCounty(CreateSubCountyDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Please correct the errors: {string.Join(", ", errors)}";
                    return RedirectToAction(nameof(SubCounties));
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.CreatedBy = currentUser;

                // Generate SubCountyCode if not provided or is preview
                if (string.IsNullOrEmpty(dto.SubCountyCode) || dto.SubCountyCode.Contains("NEW"))
                {
                    dto.SubCountyCode = await GenerateSubCountyCode(dto.CountyId);
                }

                var result = await _locationService.CreateSubCountyAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(SubCounties), new { countyId = dto.CountyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-county");
                TempData["ErrorMessage"] = $"Error creating sub-county: {ex.Message}";
                return RedirectToAction(nameof(SubCounties));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubCounty(UpdateSubCountyDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Please correct the errors: {string.Join(", ", errors)}";
                    return RedirectToAction(nameof(SubCounties));
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.ModifiedBy = currentUser;

                var result = await _locationService.UpdateSubCountyAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(SubCounties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sub-county");
                TempData["ErrorMessage"] = $"Error updating sub-county: {ex.Message}";
                return RedirectToAction(nameof(SubCounties));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubCounty(int id)
        {
            try
            {
                var currentUser = User.Identity?.Name ?? "System";
                var result = await _locationService.DeleteSubCountyAsync(id, currentUser);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(SubCounties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sub-county");
                TempData["ErrorMessage"] = $"Error deleting sub-county: {ex.Message}";
                return RedirectToAction(nameof(SubCounties));
            }
        }

        // ============================================================
        // WARD ACTIONS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Wards(int? subCountyId = null)
        {
            try
            {
                var wards = await _locationService.GetAllWardsAsync(subCountyId);
                var subCounties = await _locationService.GetAllSubCountiesAsync();

                ViewBag.ActiveTab = "Wards";
                ViewBag.SubCounties = subCounties.SubCounties;
                ViewBag.SelectedSubCountyId = subCountyId;

                return View(wards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wards page");
                TempData["ErrorMessage"] = "Error loading wards. Please try again.";
                return View(new WardListResponseDTO());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWard(CreateWardDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Please correct the errors: {string.Join(", ", errors)}";
                    return RedirectToAction(nameof(Wards));
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.CreatedBy = currentUser;

                // Generate WardCode if not provided
                if (string.IsNullOrEmpty(dto.WardCode))
                {
                    dto.WardCode = await GenerateWardCode(dto.SubCountyId);
                }

                var result = await _locationService.CreateWardAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Wards), new { subCountyId = dto.SubCountyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ward");
                TempData["ErrorMessage"] = $"Error creating ward: {ex.Message}";
                return RedirectToAction(nameof(Wards));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWard(UpdateWardDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = $"Please correct the errors: {string.Join(", ", errors)}";
                    return RedirectToAction(nameof(Wards));
                }

                var currentUser = User.Identity?.Name ?? "System";
                dto.ModifiedBy = currentUser;

                var result = await _locationService.UpdateWardAsync(dto);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Wards));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ward");
                TempData["ErrorMessage"] = $"Error updating ward: {ex.Message}";
                return RedirectToAction(nameof(Wards));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWard(int id)
        {
            try
            {
                var currentUser = User.Identity?.Name ?? "System";
                var result = await _locationService.DeleteWardAsync(id, currentUser);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }

                return RedirectToAction(nameof(Wards));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ward");
                TempData["ErrorMessage"] = $"Error deleting ward: {ex.Message}";
                return RedirectToAction(nameof(Wards));
            }
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private async Task<string> GenerateSubCountyCode(int countyId)
        {
            // Get county code
            var counties = await _locationService.GetAllCountiesAsync();
            var county = counties.Counties.FirstOrDefault(c => c.Id == countyId);
            var countyCode = county?.CountyCode ?? "UNK";

            // Get existing sub-counties for this county
            var existingSubCounties = await _locationService.GetSubCountiesByCountyIdAsync(countyId);
            var nextNumber = existingSubCounties.Count() + 1;

            return $"{countyCode}-{nextNumber:D2}";
        }

        private async Task<string> GenerateWardCode(int subCountyId)
        {
            // Get sub-county code
            var subCounties = await _locationService.GetAllSubCountiesAsync();
            var subCounty = subCounties.SubCounties.FirstOrDefault(s => s.Id == subCountyId);
            var subCountyCode = subCounty?.SubCountyCode ?? "SUB";

            // Get existing wards for this sub-county
            var existingWards = await _locationService.GetWardsBySubCountyIdAsync(subCountyId);
            var nextNumber = existingWards.Count() + 1;

            return $"{subCountyCode}-{nextNumber:D2}";
        }

        // ============================================================
        // API ENDPOINTS FOR AJAX
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetSubCountiesByCounty(int countyId)
        {
            try
            {
                var subCounties = await _locationService.GetSubCountiesByCountyIdAsync(countyId);
                return Json(new { success = true, data = subCounties });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sub-counties by county");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWardsBySubCounty(int subCountyId)
        {
            try
            {
                var wards = await _locationService.GetWardsBySubCountyIdAsync(subCountyId);
                return Json(new { success = true, data = wards });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wards by sub-county");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}