using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    public class LoanschdController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoanschdController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // utilities.SetUpPrivileges(this);
            return View(await _context.LOANSCHD.ToListAsync());
        }

        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var sched = await _context.LOANSCHD
                .FirstOrDefaultAsync(m => m.ID.ToString() == id);

            if (sched == null) return NotFound();

            return View(sched);
        }

        public IActionResult Create()
        {
            // Get CompanyCode from logged-in user claims
            var companyCode = User.FindFirst("CompanyCode")?.Value;

            if (string.IsNullOrEmpty(companyCode))
                throw new Exception("CompanyCode is missing from user claims");

            // Optional: MemberNo removed as requested
            ViewBag.memberno = string.Empty;

            var loanTypes = _context.Loantypes
                .AsNoTracking()
                .Where(x => x.CompanyCode == companyCode)
                .Select(x => new
                {
                    x.LoanCode,
                    x.LoanType1
                })
                .ToList();

            ViewBag.loantypes = new SelectList(
                loanTypes,
                "LoanCode",
                "LoanType1"
            );

            return View();
        }

        [HttpPost]
        public IActionResult populateInterestrate(string LoanCode, string MemberNo)
        {
            if (string.IsNullOrEmpty(LoanCode) || string.IsNullOrEmpty(MemberNo))
                return Json(new { error = "LoanCode or MemberNo is missing." });

            int count = _context.Loans
                .Count(k => k.MemberNo == MemberNo && k.LoanNo == LoanCode);

            string loancount = count > 0
                ? $"{LoanCode}{MemberNo}-{count + 1}"
                : $"{LoanCode}{MemberNo}";

            var loanType = _context.Loantypes
                .FirstOrDefault(k => k.LoanCode == LoanCode);

            if (loanType == null)
                return Json(new { error = "Loan type not found." });

            return Json(new
            {
                loancode = loanType.LoanCode?.Trim(),
                repaypriod = loanType.RepayPeriod,
                interestrate = Math.Round(decimal.Parse(loanType.Interest), 2),
                repaymethod = loanType.Repaymethod?.Trim(),
                loancount
            });
        }

        [HttpPost]
        [Route("Loanschd/GenerateSchedule")]
        public IActionResult GenerateSchedule(
            string MemberNo,
            decimal InitialAmount,
            decimal InterestRate,
            int Period,
            string RepaymentMethod,
            DateTime StartDate)
        {
            List<Loanschd> schedule = new();

            decimal monthlyRate = InterestRate / 100 / 12;
            decimal balance = InitialAmount;

            if (RepaymentMethod == "AMRT")
            {
                decimal monthlyPayment =
                    (InitialAmount * monthlyRate) /
                    (1 - (decimal)Math.Pow(1 + (double)monthlyRate, -Period));

                DateTime date = StartDate;

                for (int i = 1; i <= Period; i++)
                {
                    decimal interest = Math.Round(balance * monthlyRate, 2);
                    decimal principal = Math.Round(monthlyPayment - interest, 2);

                    balance = Math.Round(balance - principal, 2);

                    schedule.Add(new Loanschd
                    {
                        MemberNo = MemberNo,
                        Period = i,
                        Principal = principal,
                        Interest = interest,
                        Balance = balance,
                        Comments = "Loan Repayment Schedule",
                        FmtPer = date.ToString("MMM yyyy")
                    });

                    date = date.AddMonths(1);
                }
            }
            else if (RepaymentMethod == "STL")
            {
                DateTime date = StartDate;

                // Fixed monthly principal
                decimal monthlyPrincipal = Math.Round(InitialAmount / Period, 2);

                for (int i = 1; i <= Period; i++)
                {
                    // Flat interest on original amount (common SACCO STL logic)
                    decimal interest = Math.Round(InitialAmount * monthlyRate, 2);

                    decimal payment = monthlyPrincipal + interest;

                    balance = Math.Round(balance - monthlyPrincipal, 2);

                    // Prevent negative rounding drift on last row
                    if (i == Period)
                        balance = 0;

                    schedule.Add(new Loanschd
                    {
                        MemberNo = MemberNo,
                        Period = i,
                        Principal = monthlyPrincipal,
                        Interest = interest,
                        Balance = balance,
                        Comments = "Straight Line Loan Schedule",
                        FmtPer = date.ToString("MMM yyyy")
                    });

                    date = date.AddMonths(1);
                }
            }
            else if (RepaymentMethod == "RBAL")
            {
                DateTime date = StartDate;

                for (int i = 1; i <= Period; i++)
                {
                    decimal principal = Math.Round(InitialAmount / Period, 2);
                    decimal interest = Math.Round(balance * monthlyRate, 2);

                    balance = Math.Round(balance - principal, 2);

                    schedule.Add(new Loanschd
                    {
                        MemberNo = MemberNo,
                        Period = i,
                        Principal = principal,
                        Interest = interest,
                        Balance = balance,
                        Comments = "Loan Repayment Schedule",
                        FmtPer = date.ToString("MMM yyyy")
                    });

                    date = date.AddMonths(1);
                }
            }

            return Json(schedule);
        }
        [HttpGet]
        public IActionResult GetMemberName(string memberNo)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;

            if (string.IsNullOrEmpty(memberNo))
                return Json(new { error = "MemberNo is required" });

            var member = _context.Members
                .Where(x => x.MemberNo == memberNo && x.CompanyCode == companyCode)
                .Select(x => new
                {
                    name = (x.Surname + " " + x.OtherNames).Trim()
                })
                .FirstOrDefault();

            if (member == null)
                return Json(new { error = "Member not found" });

            return Json(member);
        }
        public async Task<IActionResult> Edit(string id)
        {
            // utilities.SetUpPrivileges(this);

            if (id == null) return NotFound();

            var schedule = await _context.LOANSCHD.FindAsync(id);

            if (schedule == null) return NotFound();

            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Loanschd loanschd)
        {
            if (id != loanschd.ID.ToString())
                return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(loanschd);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(loanschd);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var item = await _context.LOANSCHD
                .FirstOrDefaultAsync(m => m.ID.ToString() == id);

            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var item = await _context.LOANSCHD.FindAsync(id);

            _context.LOANSCHD.Remove(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool LoanschExists(string id)
        {
            return _context.LOANSCHD.Any(e => e.ID.ToString() == id);
        }
    }
}