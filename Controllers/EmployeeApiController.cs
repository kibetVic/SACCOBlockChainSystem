// Controllers/Api/EmployeeApiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockchainDb.Models;

namespace SACCOBlockChainSystem.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchEmployees([FromQuery] string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
            {
                return Ok(new { success = false, message = "Search term must be at least 3 characters" });
            }

            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? "DEFAULT";

                // Search for employees (Agents) only - exclude suppliers
                // Filter by RecruitementAgents to get only Agents, Staff, Board members, etc.
                var employees = await _context.Agents
                    .Where(a => a.CompanyCode == companyCode
                                && a.RecruitementAgents != null
                                && !a.RecruitementAgents.ToLower().Contains("supplier")
                                && !a.RecruitementAgents.ToLower().Contains("vendor")
                                && (a.Names.Contains(searchTerm)
                                    || (a.IdNo != null && a.IdNo.Contains(searchTerm))
                                    || (a.MobileNo != null && a.MobileNo.Contains(searchTerm))
                                    || (a.StaffCode != null && a.StaffCode.Contains(searchTerm))))
                    .OrderBy(a => a.Names)
                    .Select(a => new
                    {
                        id = a.Id,
                        idNo = a.IdNo,
                        names = a.Names,
                        fullName = a.Names,
                        surname = "",
                        otherNames = a.Names,
                        phoneNo = a.MobileNo,
                        mobileNo = a.MobileNo,
                        phone = a.MobileNo,
                        staffCode = a.StaffCode,
                        status = 1,
                        isActive = true,
                        recruitementAgents = a.RecruitementAgents
                    })
                    .Take(50) // Limit results for performance
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = employees
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}