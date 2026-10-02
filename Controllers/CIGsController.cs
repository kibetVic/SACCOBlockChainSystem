using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    public class CIGsController : Controller
    {
        private readonly IGIGsService _gigsService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CIGsController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public CIGsController(
            IGIGsService gigsService,
            ApplicationDbContext context,
            ILogger<CIGsController> logger,
            ICompanyContextService companyContextService)
        {
            _gigsService = gigsService;
            _context = context;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        public async Task<IActionResult> Index(string search = null)
        {
            try
            {
                var gigs = await _gigsService.GetAllGIGsAsync(search);

                // Generate new GIG code for the form using logged-in user's company code
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var newGigCode = await _gigsService.GenerateGIGCodeAsync(currentCompanyCode);
                ViewBag.NewGigCode = newGigCode;
                ViewBag.CurrentSearch = search;
                ViewBag.CompanyName = _companyContextService.GetCurrentUserGroup();

                return View(gigs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GIGs Index");
                TempData["ErrorMessage"] = $"Error loading GIGs: {ex.Message}";
                return View(new List<GIGsResponseDTO>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] GIGsDTO gigDto)
        {
            try
            {
                _logger.LogInformation("=== CREATE GIG REQUEST ===");

                if (gigDto == null)
                {
                    _logger.LogWarning("gigDto is null - possible JSON deserialization issue");
                    return Json(new { success = false, message = "Invalid request data. Please check the form and try again." });
                }

                _logger.LogInformation($"Received GIG data: GigName={gigDto.GigName}, GigCode={gigDto.GigCode}");

                // Validate required fields
                if (string.IsNullOrWhiteSpace(gigDto.GigName))
                {
                    return Json(new { success = false, message = "GIG Name is required" });
                }

                // Auto-generate GIG code if not provided
                if (string.IsNullOrEmpty(gigDto.GigCode))
                {
                    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                    gigDto.GigCode = await _gigsService.GenerateGIGCodeAsync(currentCompanyCode);
                    _logger.LogInformation($"Auto-generated GIG code: {gigDto.GigCode}");
                }

                var result = await _gigsService.CreateGIGAsync(gigDto);
                return Json(new { success = true, message = "GIG created successfully", gig = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error creating GIG");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating GIG");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromBody] GIGsDTO gigDto)
        {
            try
            {
                _logger.LogInformation($"=== EDIT GIG REQUEST for ID: {id} ===");

                if (gigDto == null)
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                if (string.IsNullOrWhiteSpace(gigDto.GigName))
                {
                    return Json(new { success = false, message = "GIG Name is required" });
                }

                var result = await _gigsService.UpdateGIGAsync(id, gigDto);
                return Json(new { success = true, message = "GIG updated successfully", gig = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error updating GIG");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating GIG");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation($"=== DELETE GIG REQUEST for ID: {id} ===");

                var result = await _gigsService.DeleteGIGAsync(id);
                return Json(new { success = true, message = "GIG deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error deleting GIG");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting GIG");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGIGDetails(int id)
        {
            try
            {
                _logger.LogInformation($"=== GET GIG DETAILS for ID: {id} ===");

                var gig = await _gigsService.GetGIGByIdAsync(id);
                if (gig == null)
                {
                    return Json(new { success = false, message = "GIG not found" });
                }
                return Json(new { success = true, gig });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GIG details");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateGIGCode()
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var gigCode = await _gigsService.GenerateGIGCodeAsync(currentCompanyCode);
                return Json(new { success = true, gigCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating GIG code");
                return Json(new { success = false, message = ex.Message });
            }
        }


        // GIGsController.cs
        [HttpGet]
        public async Task<IActionResult> GetGIGMemberCounts()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var gigs = await _context.CIGs
                    .Where(g => g.CompanyCode == companyCode && g.Status == "Active")
                    .ToListAsync();

                var counts = new List<object>();
                foreach (var gig in gigs)
                {
                    var count = await _context.Members
                        .CountAsync(m => m.Cigcode == gig.GigCode && m.CompanyCode == companyCode);

                    counts.Add(new { id = gig.Id, count = count });
                }

                return Json(new { success = true, counts = counts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GIG member counts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetGIGMembers([FromBody] GetGIGMembersRequest request)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var allMembers = new List<object>();

                if (request.GigIds == null || !request.GigIds.Any())
                {
                    return Json(new { success = false, message = "No GIGs selected" });
                }

                foreach (var gigId in request.GigIds)
                {
                    // Get the GIG details
                    var gig = await _context.CIGs
                        .FirstOrDefaultAsync(g => g.Id == gigId && g.CompanyCode == companyCode);

                    if (gig != null)
                    {
                        // Get ALL members for this GIG - NO Status filter to get all members
                        var gigMembers = await _context.Members
                            .Where(m => m.Cigcode == gig.GigCode
                                && m.CompanyCode == companyCode)
                            .Select(m => new
                            {
                                m.MemberNo,
                                FullName = (m.Surname ?? "") + " " + (m.OtherNames ?? ""),
                                m.PhoneNo,
                                m.Surname,
                                m.OtherNames,
                                m.Email,
                                m.Idno,
                                m.Status,
                                GigId = gig.Id,
                                GigName = gig.GigName,
                                GigCode = gig.GigCode
                            })
                            .ToListAsync();

                        if (gigMembers.Any())
                        {
                            allMembers.AddRange(gigMembers);
                        }
                    }
                }

                if (!allMembers.Any())
                {
                    return Json(new
                    {
                        success = true,
                        members = new List<object>(),
                        message = "No members found in the selected GIGs"
                    });
                }

                return Json(new { success = true, members = allMembers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GIG members");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetGIGMembersByCode([FromBody] GetGIGMembersByCodeRequest request)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var allMembers = new List<object>();

                if (request.GigCodes == null || !request.GigCodes.Any())
                {
                    return Json(new { success = false, message = "No GIGs selected" });
                }

                foreach (var gigCode in request.GigCodes)
                {
                    // Get the GIG details
                    var gig = await _context.CIGs
                        .FirstOrDefaultAsync(g => g.GigCode == gigCode && g.CompanyCode == companyCode);

                    if (gig != null)
                    {
                        // Get ALL members for this GIG using Cigcode - NO Status filter
                        var gigMembers = await _context.Members
                            .Where(m => m.Cigcode == gigCode
                                && m.CompanyCode == companyCode)
                            .Select(m => new
                            {
                                m.MemberNo,
                                FullName = (m.Surname ?? "") + " " + (m.OtherNames ?? ""),
                                m.PhoneNo,
                                m.Surname,
                                m.OtherNames,
                                m.Email,
                                m.Idno,
                                m.Status,
                                GigId = gig.Id,
                                GigName = gig.GigName,
                                GigCode = gig.GigCode
                            })
                            .ToListAsync();

                        if (gigMembers.Any())
                        {
                            allMembers.AddRange(gigMembers);
                        }
                    }
                }

                if (!allMembers.Any())
                {
                    return Json(new
                    {
                        success = true,
                        members = new List<object>(),
                        message = "No members found in the selected GIGs"
                    });
                }

                return Json(new { success = true, members = allMembers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GIG members by code");
                return Json(new { success = false, message = ex.Message });
            }
        }
       
        public class GetGIGMembersRequest
        {
            public List<int> GigIds { get; set; } = new List<int>();
        }

        public class GetGIGMembersByCodeRequest
        {
            public List<string> GigCodes { get; set; } = new List<string>();
        }
    }
}