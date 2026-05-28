using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Controllers
{
    public class DividendDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DividendDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================
        // INDEX
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

            return View(data);
        }


        // =========================================
        // SEARCH MEMBERS (BY NAME OR MEMBER NO)
        // =========================================
        [HttpGet]
        public async Task<IActionResult> SearchMembers(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new { success = false, data = new List<object>() });
                }

                var companyCode = GetUserCompanyCode();

                var data = await _context.Members
                    .Where(m => m.CompanyCode == companyCode &&
                           (
                               m.MemberNo.Contains(term) ||
                               (m.Surname ?? "").Contains(term) ||
                               (m.OtherNames ?? "").Contains(term)
                           ))
                    .Select(m => new
                    {
                        memberNo = m.MemberNo,
                        name = (m.Surname ?? "") + " " + (m.OtherNames ?? ""),
                        idno = m.Idno,
                        phoneNo = m.PhoneNo
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = data
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
        public async Task<IActionResult> Create(DividendDetailsDTO model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Invalid data supplied.";
                    return RedirectToAction("Index");
                }

                var companyCode = GetUserCompanyCode();

                // =========================================
                // CHECK MEMBER EXISTS
                // =========================================
                var member = await _context.Members
                    .FirstOrDefaultAsync(x =>
                        x.MemberNo == model.MemberNo &&
                        x.CompanyCode == companyCode);

                if (member == null)
                {
                    TempData["ErrorMessage"] = "Member not found.";
                    return RedirectToAction("Index");
                }

                // =========================================
                // GET TOTAL SAVINGS / CONTRIBUTIONS
                // =========================================
                decimal memberSavings = await _context.Contribs
                    .Where(x =>
                        x.MemberNo == model.MemberNo &&
                        x.CompanyCode == companyCode)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;

                // =========================================
                // DIVIDEND FORMULAS
                // =========================================

                decimal weightedSavings = memberSavings;

                // Rates
                decimal savingsRate = 0.10m;      // 10%
                decimal shareRate = 0.05m;        // 5%
                decimal withholdingRate = 0.05m;  // 5%

                // Calculations
                decimal savingsDividend = weightedSavings * savingsRate;

                decimal shareDividend = weightedSavings * shareRate;

                decimal grossDividend = savingsDividend + shareDividend;

                decimal withholdingTax = grossDividend * withholdingRate;

                decimal netDividend = grossDividend - withholdingTax;

                // =========================================
                // SAVE
                // =========================================
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

                TempData["SuccessMessage"] =
                    $"Dividend calculated successfully for {member.MemberNo}";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
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