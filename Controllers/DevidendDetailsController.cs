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
            var companyCode = GetUserCompanyCode();

            var data = await _context.DividendDetails
                .Where(x => x.CompanyCode == companyCode)
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

            // Get member names for display
            var memberNumbers = data.Select(x => x.MemberNo).Distinct().ToList();
            var members = await _context.Members
                .Where(m => memberNumbers.Contains(m.MemberNo) && m.CompanyCode == companyCode)
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
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid data supplied."
                    });
                }
                var debugMemberNo = model.MemberNo;
                var debugYear = model.DividendYear;
             
                var companyCode = GetUserCompanyCode();

                var member = await _context.Members
                    .FirstOrDefaultAsync(x =>
                        x.MemberNo == model.MemberNo &&
                        x.CompanyCode == companyCode);

                if (member == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Member not found."
                    });
                }

                decimal memberSavings = await _context.Contribs
                    .Where(x =>
                       x.MemberNo.Trim().ToLower() == model.MemberNo.Trim().ToLower() &&
                        x.CompanyCode == companyCode)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;

                decimal weightedSavings = memberSavings;

                decimal savingsRate = 0.10m;
                decimal shareRate = 0.05m;
                decimal withholdingRate = 0.05m;

                decimal savingsDividend = weightedSavings * savingsRate;
                decimal shareDividend = weightedSavings * shareRate;

                decimal grossDividend = savingsDividend + shareDividend;

                decimal withholdingTax = grossDividend * withholdingRate;

                decimal netDividend = grossDividend - withholdingTax;

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
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Dividend calculated successfully",
                    data = new
                    {
                        entity.DividendYear,
                        entity.MemberNo,
                        entity.NetDividend
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.ToString()   // 👈 NOT ex.Message
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