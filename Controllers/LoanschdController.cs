using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Controllers
{
    public class LoanschdController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoanschdController(ApplicationDbContext context)
        {
            _context = context;
        }

        // VIEW
        public IActionResult Create()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;

            ViewBag.LoanTypes = _context.Loantypes
                .Where(x => x.CompanyCode == companyCode)
                .ToList();

            return View();
        }

        // MEMBER NAME
        [HttpGet]
        public JsonResult GetMemberName(string memberNo)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            var member = _context.Members.FirstOrDefault(x => x.MemberNo == memberNo&& x.CompanyCode == companyCode);


            if (member == null)
                return Json(new { error = "Member not found" });

            return Json(new
            {
                memberName = member.Surname + " " + member.OtherNames
            });
        }

        // BLOCKCHAIN SCHEDULE
        [HttpPost]
        [Route("Loanschd/GenerateBlockchainSchedule")]
        public IActionResult GenerateBlockchainSchedule(
            string MemberNo,
            string LoanNo,
            string LoanCode,
            decimal InitialAmount,
            decimal InterestRate,
            int Period,
            DateTime StartDate)
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            var member = _context.Members.FirstOrDefault(x => x.MemberNo == MemberNo && x.CompanyCode == companyCode);

            var loanType = _context.Loantypes
                .FirstOrDefault(x => x.LoanCode == LoanCode && x.CompanyCode == companyCode);


            string memberName = member != null
                ? member.Surname + " " + member.OtherNames
                : "";

            string repayMethod = loanType?.Repaymethod ?? "AMRT";

            decimal monthlyRate = InterestRate / 100 / 12;
            decimal balance = InitialAmount;
            string previousHash = "0";

            var chain = new List<LoanScheduleBlock>();

            for (int i = 1; i <= Period; i++)
            {
                decimal interest = 0;
                decimal principal = 0;
                decimal payment = 0;

                // ===============================
                // AMORTIZED (FIXED EMI)
                // ===============================
                if (repayMethod == "AMRT")
                {
                    payment = (InitialAmount * monthlyRate) /
                              (1 - (decimal)Math.Pow((double)(1 + monthlyRate), -Period));

                    interest = balance * monthlyRate;
                    principal = payment - interest;
                }

                // ===============================
                // STRAIGHT LINE (FIXED PRINCIPAL)
                // ===============================
                else if (repayMethod == "STL")
                {
                    principal = InitialAmount / Period;

                    // FIXED INTEREST
                    interest = InitialAmount * monthlyRate;

                    payment = principal + interest;
                }

                // ===============================
                // REDUCING BALANCE (DECLINING)
                // ===============================
                else if (repayMethod == "RBAL")
                {
                    principal = InitialAmount / Period;
                    interest = balance * monthlyRate;
                    payment = principal + interest;
                }

                decimal closing = balance - principal;

                var block = new LoanScheduleBlock
                {
                    Index = i,
                    Timestamp = DateTime.Now,
                    MemberNo = MemberNo,
                    MemberName = memberName,
                    LoanNo = LoanNo,
                    Period = i,
                    PaymentDate = StartDate.AddMonths(i),

                    OpeningBalance = balance,
                    Principal = principal,
                    Interest = interest,
                    Payment = payment,
                    ClosingBalance = closing,

                    PreviousHash = previousHash
                };

                block.Hash = CalculateHash(block);
                previousHash = block.Hash;

                chain.Add(block);

                balance = closing;
            }

            return Json(chain);
        }

        // HASH
        private string CalculateHash(LoanScheduleBlock block)
        {
            var raw = $"{block.Index}{block.MemberNo}{block.LoanNo}{block.Period}" +
                      $"{block.OpeningBalance}{block.Principal}{block.Interest}" +
                      $"{block.ClosingBalance}{block.PreviousHash}";

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}