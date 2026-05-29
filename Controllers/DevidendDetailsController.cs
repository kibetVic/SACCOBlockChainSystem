using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    public class DividendDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IContributionService _contributionService;

        public DividendDetailsController(ApplicationDbContext context)
        {
            _context = context;
            IContributionService contributionService;
        }

        // =========================================
        // INDEX
        // =========================================
        // =========================================
        // INDEX - Updated to include member names
        // =========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // First, get all dividend details without company code filter
            var data = await _context.DividendDetails
                .OrderByDescending(x => x.DividendYear)
                .Select(x => new DividendDetailsResponseDTO
                {
                    Id = x.Id,
                    DividendYear = x.DividendYear,
                    MemberNo = x.MemberNo,
                    WeightedSavings = x.WeightedSavings,
                    SavingsDividend = x.SavingsDividend,
                    ShareDividend = x.ShareDividend,
                    GrossDividend = x.GrossDividend,
                    WithholdingTax = x.WithholdingTax,
                    NetDividend = x.NetDividend,
                    CompanyCode = x.CompanyCode
                })
                .ToListAsync();

            // Get member names - don't filter by company code here
            var memberNumbers = data.Select(x => x.MemberNo).Distinct().ToList();
            var members = await _context.Members
                .Where(m => memberNumbers.Contains(m.MemberNo))  // No company code filter
                .ToDictionaryAsync(m => m.MemberNo, m => (m.Surname ?? "") + " " + (m.OtherNames ?? ""));

            ViewBag.MemberNames = members;

            return View(data);
        }
        // =========================================
        // SEARCH MEMBERS - Fixed version
        // =========================================
        [HttpGet]
        public async Task<IActionResult> SearchMembers(string term)
        {
            try
            {


                 if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Get company code from user claims (assuming it's stored there)
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Company code not found in user claims" });
                }

                var members = await _context.Members
                    .Where(m => m.CompanyCode == companyCode)
                    .Where(m =>
                        (m.MemberNo ?? "").Contains(term) ||
                        (m.Surname ?? "").Contains(term) ||
                        (m.OtherNames ?? "").Contains(term))
                    .Select(m => new
                    {
                        memberNo = m.MemberNo,
                        name = (m.Surname ?? "") + " " + (m.OtherNames ?? ""),
                        idno = m.Idno ?? "",
                        phoneNo = m.PhoneNo ?? ""
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = members
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
        // =========================================
        // CREATE DIVIDEND DETAILS
        // =========================================
        // =========================================
        // CREATE DIVIDEND DETAILS
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(DividendDetailsDTO model)
        {
            try
            {
                // Log incoming request
                System.Diagnostics.Debug.WriteLine($"=== Calculate called ===");
                System.Diagnostics.Debug.WriteLine($"Model Year: {model.DividendYear}");
                System.Diagnostics.Debug.WriteLine($"Model MemberNo: {model.MemberNo}");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new
                    {
                        success = false,
                        message = $"Invalid data: {string.Join(", ", errors)}"
                    });
                }

                // Try multiple ways to get company code
                string companyCode = null;

                // Method 1: From session
                companyCode = HttpContext.Session.GetString("CompanyCode");
                System.Diagnostics.Debug.WriteLine($"Method 1 - Session company code: '{companyCode}'");

                // Method 2: From user claims
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = User.FindFirst("CompanyCode")?.Value ?? User.FindFirst("companyCode")?.Value;
                    System.Diagnostics.Debug.WriteLine($"Method 2 - Claims company code: '{companyCode}'");
                }

                // Method 3: Get from the member directly
                if (string.IsNullOrEmpty(companyCode))
                {
                    var memberFromDb = await _context.Members
                        .FirstOrDefaultAsync(x => x.MemberNo == model.MemberNo);

                    if (memberFromDb != null)
                    {
                        companyCode = memberFromDb.CompanyCode;
                        System.Diagnostics.Debug.WriteLine($"Method 3 - Member company code: '{companyCode}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Method 3 - Member not found: {model.MemberNo}");
                    }
                }

                // Method 4: Get first available company code
                if (string.IsNullOrEmpty(companyCode))
                {
                    var anyMember = await _context.Members.FirstOrDefaultAsync();
                    if (anyMember != null)
                    {
                        companyCode = anyMember.CompanyCode;
                        System.Diagnostics.Debug.WriteLine($"Method 4 - First member company code: '{companyCode}'");
                    }
                }

                // Method 5: Get from Companies table
                if (string.IsNullOrEmpty(companyCode))
                {
                    var existingCodes = await _context.Members
                        .Where(m => m.CompanyCode != null)
                        .Select(m => m.CompanyCode)
                        .Distinct()
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"Method 5 - Available codes: {string.Join(", ", existingCodes)}");

                    if (existingCodes.Any())
                    {
                        companyCode = existingCodes.First();
                        System.Diagnostics.Debug.WriteLine($"Method 5 - Using first code: '{companyCode}'");
                    }
                    else
                    {
                        return Json(new
                        {
                            success = false,
                            message = "No company code found in the system. Please contact administrator."
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Final company code: '{companyCode}'");

                // Now use the company code to find the member
                var member = await _context.Members
                    .FirstOrDefaultAsync(x => x.MemberNo == model.MemberNo && x.CompanyCode == companyCode);

                if (member == null)
                {
                    var memberAnyCompany = await _context.Members
                        .FirstOrDefaultAsync(x => x.MemberNo == model.MemberNo);

                    if (memberAnyCompany != null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Member found but with different company code. Member's company: '{memberAnyCompany.CompanyCode}', Your company: '{companyCode}'"
                        });
                    }

                    return Json(new
                    {
                        success = false,
                        message = $"Member '{model.MemberNo}' not found in database."
                    });
                }

                System.Diagnostics.Debug.WriteLine($"Member found: {member.MemberNo}, Name: {member.Surname} {member.OtherNames}");

                // Check if dividend already exists
                var existingDividend = await _context.DividendDetails
                    .FirstOrDefaultAsync(x => x.MemberNo == model.MemberNo &&
                                              x.DividendYear == model.DividendYear &&
                                              x.CompanyCode == companyCode);

                if (existingDividend != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Dividend for year {model.DividendYear} already exists for this member."
                    });
                }

                // Calculate savings
                decimal memberSavings = await _context.Contribs
                    .Where(x => x.MemberNo == model.MemberNo && x.CompanyCode == companyCode)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;

                System.Diagnostics.Debug.WriteLine($"Member savings: {memberSavings}");

                decimal weightedSavings = memberSavings;
                decimal savingsRate = 0.10m;
                decimal shareRate = 0.05m;
                decimal withholdingRate = 0.05m;

                decimal savingsDividend = weightedSavings * savingsRate;
                decimal shareDividend = weightedSavings * shareRate;
                decimal grossDividend = savingsDividend + shareDividend;
                decimal withholdingTax = grossDividend * withholdingRate;
                decimal netDividend = grossDividend - withholdingTax;

                System.Diagnostics.Debug.WriteLine($"Calculations - Gross: {grossDividend}, Net: {netDividend}");

                var entity = new DividendDetails
                {
                    DividendYear = model.DividendYear,
                    MemberNo = model.MemberNo,
                    WeightedSavings = weightedSavings,
                    SavingsDividend = savingsDividend,
                    ShareDividend = shareDividend,
                    GrossDividend = grossDividend,
                    WithholdingTax = withholdingTax,
                    NetDividend = netDividend,
                    CompanyCode = companyCode
                };

                _context.DividendDetails.Add(entity);

                try
                {
                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"Save successful! Entity ID: {entity.Id}");
                }
                catch (DbUpdateException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"DB Save Error: {dbEx.InnerException?.Message ?? dbEx.Message}");
                    return Json(new
                    {
                        success = false,
                        message = $"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}"
                    });
                }

                string memberName = (member.Surname ?? "") + " " + (member.OtherNames ?? "");

                return Json(new
                {
                    success = true,
                    message = $"Dividend calculated successfully for {memberName}",
                    data = new
                    {
                        entity.DividendYear,
                        entity.MemberNo,
                        memberName = memberName,
                        weightedSavings = weightedSavings.ToString("N2"),
                        savingsDividend = savingsDividend.ToString("N2"),
                        shareDividend = shareDividend.ToString("N2"),
                        grossDividend = grossDividend.ToString("N2"),
                        withholdingTax = withholdingTax.ToString("N2"),
                        netDividend = netDividend.ToString("N2")
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error: {ex.ToString()}");
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }
        // =========================================
        // GET COMPANY CODE FROM SESSION
        // =========================================
        private string GetUserCompanyCode()
        {
            return HttpContext.Session.GetString("CompanyCode") ?? "";
        }
    }
}