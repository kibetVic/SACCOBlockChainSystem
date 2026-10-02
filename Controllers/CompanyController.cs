using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    public class CompanyController : Controller
    {
        private readonly ICompanyService _companyService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanyController> _logger;
        private readonly IUserService _userService;

        public CompanyController(
            ICompanyService companyService,
            ApplicationDbContext context,
            ILogger<CompanyController> logger,
            IUserService userService)
        {
            _companyService = companyService;
            _context = context;
            _logger = logger;
            _userService = userService;
        }

        private async Task LoadDropdowns()
        {
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

            if (currentUserRole == "Super Admin")
            {
                ViewBag.UserGroups = await _userService.GetUserGroupsAsync();
                ViewBag.Companies = await _companyService.GetAllCompaniesForDropdownAsync();
            }
            else
            {
                var userCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == currentUserCompanyCode);

                ViewBag.UserGroups = new List<string> { "Member", "Teller", "LoanOfficer", "Auditor", "Staff" };
                ViewBag.Companies = userCompany != null
                    ? new List<object> { new { userCompany.CompanyCode, DisplayText = $"{userCompany.CompanyCode} - {userCompany.CompanyName}" } }
                    : new List<object>();
            }

            ViewBag.SubCounties = await _context.SubCounties
                .Where(s => s.Status == "Active")
                .OrderBy(s => s.SubCountyName)
                .Select(s => new { s.Id, s.SubCountyName })
                .ToListAsync();

            ViewBag.Wards = await _context.Wards
                .Where(w => w.Status == "Active")
                .OrderBy(w => w.WardName)
                .Select(w => new { w.Id, w.WardName, w.SubCountyId })
                .ToListAsync();
        }

        [Authorize]
        public async Task<IActionResult> Index(string search = null)
        {
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

            List<CompanyResponseDTO> companies;

            if (currentUserRole == "Super Admin")
            {
                companies = await _companyService.GetAllCompaniesAsync(search);
            }
            else
            {
                var userCompany = await _companyService.GetCompanyByCodeAsync(currentUserCompanyCode);
                companies = userCompany != null ? new List<CompanyResponseDTO> { userCompany } : new List<CompanyResponseDTO>();

                if (!string.IsNullOrEmpty(search) && companies.Any())
                {
                    companies = companies.Where(c =>
                        (c.CompanyName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                        (c.CompanyCode?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    ).ToList();
                }
            }

            if (currentUserRole == "Super Admin")
            {
                var newCompanyCode = await _companyService.GenerateCompanyCodeAsync();
                ViewBag.NewCompanyCode = newCompanyCode;
            }
            else
            {
                ViewBag.NewCompanyCode = null;
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentUserRole = currentUserRole;
            ViewBag.CurrentUserCompany = currentUserCompanyCode;

            await LoadDropdowns();

            return View(companies);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

            if (currentUserRole != "Super Admin")
            {
                TempData["ErrorMessage"] = "You don't have permission to create companies. Only Super Administrators can create companies.";
                return RedirectToAction("Index");
            }

            var newCompanyCode = await _companyService.GenerateCompanyCodeAsync();
            ViewBag.NewCompanyCode = newCompanyCode;

            await LoadDropdowns();

            return View(new CompanyDTO());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CompanyDTO companyDto)
        {
            try
            {
                _logger.LogInformation("=== CREATE COMPANY REQUEST ===");

                if (companyDto == null)
                {
                    return Json(new { success = false, message = "Invalid request data. Please check the form and try again." });
                }

                _logger.LogInformation($"Received company data: CompanyName={companyDto.CompanyName}, Email={companyDto.Email}");

                var validationErrors = new System.Text.StringBuilder();

                if (string.IsNullOrWhiteSpace(companyDto.CompanyName))
                    validationErrors.AppendLine("- Company Name is required");

                if (string.IsNullOrWhiteSpace(companyDto.Contactperson))
                    validationErrors.AppendLine("- Contact Person is required");

                if (string.IsNullOrWhiteSpace(companyDto.Telephone))
                    validationErrors.AppendLine("- Telephone Number is required");

                if (string.IsNullOrWhiteSpace(companyDto.Email))
                    validationErrors.AppendLine("- Email Address is required");

                if (string.IsNullOrWhiteSpace(companyDto.Address))
                    validationErrors.AppendLine("- Postal Address is required");

                if (!companyDto.NoEmployees.HasValue || companyDto.NoEmployees.Value <= 0)
                    validationErrors.AppendLine("- Number of Members is required");

                if (validationErrors.Length > 0)
                {
                    return Json(new { success = false, message = validationErrors.ToString() });
                }

                if (string.IsNullOrEmpty(companyDto.CompanyCode))
                {
                    companyDto.CompanyCode = await _companyService.GenerateCompanyCodeAsync();
                }

                var result = await _companyService.CreateCompanyAsync(companyDto);
                return Json(new { success = true, message = "Company created successfully", company = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating company");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

            var existingCompany = await _companyService.GetCompanyByIdAsync(id);

            if (existingCompany == null)
            {
                TempData["ErrorMessage"] = "Company not found.";
                return RedirectToAction("Index");
            }

            if (currentUserRole != "Super Admin" && existingCompany.CompanyCode != currentUserCompanyCode)
            {
                TempData["ErrorMessage"] = "You don't have permission to edit this company.";
                return RedirectToAction("Index");
            }

            var locationIds = await GetLocationIds(existingCompany.County, existingCompany.SubCounty, existingCompany.Ward);

            var companyWithIds = new
            {
                existingCompany.Id,
                existingCompany.CompanyCode,
                existingCompany.CompanyName,
                existingCompany.Contactperson,
                existingCompany.Telephone,
                existingCompany.Email,
                existingCompany.Address,
                existingCompany.NoEmployees,
                CSRegNO = existingCompany.CSRegNO ?? string.Empty,
                County = existingCompany.County ?? string.Empty,
                SubCounty = existingCompany.SubCounty ?? string.Empty,
                Ward = existingCompany.Ward ?? string.Empty,
                existingCompany.Village,
                CountyId = locationIds.CountyId,
                SubCountyId = locationIds.SubCountyId,
                WardId = locationIds.WardId,
                existingCompany.BusinessStatus
            };

            await LoadDropdowns();

            return Json(new { success = true, company = companyWithIds });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [FromBody] CompanyDTO model)
        {
            try
            {
                _logger.LogInformation($"=== EDIT COMPANY REQUEST: ID={id} ===");

                if (model == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                var currentUserCompanyCode = User.FindFirstValue("CompanyCode");

                var existingCompany = await _companyService.GetCompanyByIdAsync(id);

                if (existingCompany == null)
                {
                    return Json(new { success = false, message = "Company not found." });
                }

                if (currentUserRole != "Super Admin" && existingCompany.CompanyCode != currentUserCompanyCode)
                {
                    return Json(new { success = false, message = "You don't have permission to edit this company." });
                }

                var validationErrors = new System.Text.StringBuilder();

                if (string.IsNullOrWhiteSpace(model.CompanyName))
                    validationErrors.AppendLine("- Company Name is required");

                if (string.IsNullOrWhiteSpace(model.Contactperson))
                    validationErrors.AppendLine("- Contact Person is required");

                if (string.IsNullOrWhiteSpace(model.Telephone))
                    validationErrors.AppendLine("- Telephone Number is required");

                if (string.IsNullOrWhiteSpace(model.Email))
                    validationErrors.AppendLine("- Email Address is required");

                if (string.IsNullOrWhiteSpace(model.Address))
                    validationErrors.AppendLine("- Postal Address is required");

                if (!model.NoEmployees.HasValue || model.NoEmployees.Value <= 0)
                    validationErrors.AppendLine("- Number of Members is required");

                if (validationErrors.Length > 0)
                {
                    return Json(new { success = false, message = validationErrors.ToString() });
                }

                model.CompanyCode = existingCompany.CompanyCode;

                var result = await _companyService.UpdateCompanyAsync(id, model);
                return Json(new { success = true, message = $"Company '{result.CompanyName}' updated successfully!", company = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _companyService.DeleteCompanyAsync(id);
                return Json(new { success = true, message = "Company deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting company");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanyDetails(int id)
        {
            try
            {
                var company = await _companyService.GetCompanyByIdAsync(id);
                if (company == null)
                {
                    return Json(new { success = false, message = "Company not found" });
                }

                var locationIds = await GetLocationIds(company.County, company.SubCounty, company.Ward);

                var companyWithIds = new
                {
                    company.Id,
                    company.CompanyCode,
                    company.CompanyName,
                    company.Contactperson,
                    company.Telephone,
                    company.Email,
                    company.Address,
                    company.NoEmployees,
                    CSRegNO = company.CSRegNO ?? string.Empty,
                    company.County,
                    company.SubCounty,
                    company.Ward,
                    company.Village,
                    CountyId = locationIds.CountyId,
                    SubCountyId = locationIds.SubCountyId,
                    WardId = locationIds.WardId,
                    company.BusinessStatus
                };

                return Json(new { success = true, company = companyWithIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company details");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<(int? CountyId, int? SubCountyId, int? WardId)> GetLocationIds(string countyName, string subCountyName, string wardName)
        {
            int? countyId = null;
            int? subCountyId = null;
            int? wardId = null;

            if (!string.IsNullOrEmpty(countyName))
            {
                var county = await _context.Counties
                    .FirstOrDefaultAsync(c => c.CountyName == countyName && c.Status == "Active");
                countyId = county?.Id;
            }

            if (!string.IsNullOrEmpty(subCountyName))
            {
                var subCounty = await _context.SubCounties
                    .FirstOrDefaultAsync(s => s.SubCountyName == subCountyName && s.Status == "Active");
                subCountyId = subCounty?.Id;
            }

            if (!string.IsNullOrEmpty(wardName))
            {
                var ward = await _context.Wards
                    .FirstOrDefaultAsync(w => w.WardName == wardName && w.Status == "Active");
                wardId = ward?.Id;
            }

            return (countyId, subCountyId, wardId);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateCompanyCode()
        {
            try
            {
                var companyCode = await _companyService.GenerateCompanyCodeAsync();
                return Json(new { success = true, companyCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating company code");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCounties()
        {
            try
            {
                var counties = await _context.Counties
                    .Where(c => c.Status == "Active")
                    .OrderBy(c => c.CountyName)
                    .Select(c => new { value = c.Id, text = c.CountyName })
                    .ToListAsync();

                return Json(counties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting counties");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCounties(int countyId)
        {
            try
            {
                var subCounties = await _context.SubCounties
                    .Where(s => s.CountyId == countyId && s.Status == "Active")
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new { value = s.Id, text = s.SubCountyName })
                    .ToListAsync();

                return Json(subCounties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sub counties");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWards(int subCountyId)
        {
            try
            {
                var wards = await _context.Wards
                    .Where(w => w.SubCountyId == subCountyId && w.Status == "Active")
                    .OrderBy(w => w.WardName)
                    .Select(w => new { value = w.Id, text = w.WardName })
                    .ToListAsync();

                return Json(wards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wards");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}