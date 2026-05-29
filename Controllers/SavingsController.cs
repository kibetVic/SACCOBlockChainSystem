using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;

namespace SACCOBlockChainSystem.Controllers
{
    public class SavingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SavingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // DASHBOARD
        public async Task<IActionResult> Index()
        {
            var savings = await _context.Contribs
                .Where(x =>
                    x.Remarks != null &&
                    (x.Remarks.ToLower() == "savings" ||
                     x.Remarks.ToLower() == "capital"))
                .OrderByDescending(x => x.ContrDate)
                .ToListAsync();

            ViewBag.TotalSavings = savings.Sum(x => x.Amount ?? 0);

            return View(savings);
        }

        // MEMBER SAVINGS
        public async Task<IActionResult> GetMemberSavings(string memberNo)
        {
            var data = await _context.Contribs
                .Where(x =>
                    x.MemberNo == memberNo &&
                    x.Remarks != null &&
                    (x.Remarks.ToLower() == "savings" ||
                     x.Remarks.ToLower() == "capital"))
                .OrderByDescending(x => x.ContrDate)
                .ToListAsync();

            ViewBag.MemberNo = memberNo;
            ViewBag.TotalSavings = data.Sum(x => x.Amount ?? 0);

            return View(data);
        }
    }
}