using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Controllers
{
    public class DividendController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DividendController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // DASHBOARD
        // =========================
        public async Task<IActionResult> Index()
        {
            var dividends = await _context.Dividends
                .OrderByDescending(x => x.ProcessDate)
                .ToListAsync();

            ViewBag.TotalDividends = dividends.Sum(x => x.DividendAmount);

            return View(dividends);
        }

        // =========================
        // CALCULATE DIVIDENDS (WITH TAX)
        // =========================
        [HttpPost]
        public async Task<IActionResult> CalculateDividends([FromBody] DividendCalculationDTO model)
        {
            var members = await _context.Contribs
                .Where(x =>
                    x.Remarks != null &&
                    (
                        x.Remarks.ToLower() == "saving" ||
                        x.Remarks.ToLower() == "deposit" ||
                        x.Remarks.ToLower() == "sharecapital"
                    ))
                .ToListAsync();

            var grouped = members
                .GroupBy(x => x.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalAmount = g.Sum(x => x.Amount ?? 0m)
                })
                .ToList();

            var dividends = new List<Dividend>();

            decimal totalAmount = grouped.Sum(x => x.TotalAmount);

            if (model.DividendType == "PRORATE")
            {
                foreach (var m in grouped)
                {
                    decimal ratio = totalAmount > 0
                        ? m.TotalAmount / totalAmount
                        : 0;

                    decimal gross = model.TotalDividend * ratio;

                    dividends.Add(new Dividend
                    {
                        MemberNo = m.MemberNo,
                        SavingsAmount = m.TotalAmount,
                        DividendAmount = Math.Round(gross, 2),
                        DividendType = "PRORATE",
                        TotalDividendPool = model.TotalDividend,
                        ProcessDate = DateTime.Now,
                        Paid = false,
                        BlockchainTxId = Guid.NewGuid().ToString()
                    });
                }
            }
            else if (model.DividendType == "FLAT")
            {
                decimal flat = grouped.Count > 0
                    ? model.TotalDividend / grouped.Count
                    : 0;

                foreach (var m in grouped)
                {
                    dividends.Add(new Dividend
                    {
                        MemberNo = m.MemberNo,
                        SavingsAmount = m.TotalAmount,
                        DividendAmount = Math.Round(flat, 2),
                        DividendType = "FLAT",
                        TotalDividendPool = model.TotalDividend,
                        ProcessDate = DateTime.Now,
                        Paid = false,
                        BlockchainTxId = Guid.NewGuid().ToString()
                    });
                }
            }

            _context.Dividends.AddRange(dividends);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Dividends calculated successfully",
                count = dividends.Count
            });
        }

        // =========================
        // PAY DIVIDENDS
        // =========================
        [HttpPost]
        public async Task<IActionResult> PayDividend()
        {
            var pending = await _context.Dividends
                .Where(x => x.Paid == false)
                .ToListAsync();

            foreach (var d in pending)
                d.Paid = true;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Dividends paid successfully" });
        }
    }
}