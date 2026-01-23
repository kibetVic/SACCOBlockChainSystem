using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize(Policy = "MemberOnly")]
    public class LoanMvcController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly IMemberService _memberService;

        public LoanMvcController(ILoanService loanService, IMemberService memberService)
        {
            _loanService = loanService;
            _memberService = memberService;
        }

        public IActionResult Apply()
        {
            return View();
        }

        public async Task<IActionResult> MyLoans()
        {
            var memberNo = User.FindFirst("MemberNo")?.Value;
            if (string.IsNullOrEmpty(memberNo))
                return RedirectToAction("Index", "Home");

            var loans = await _loanService.GetMemberLoansAsync(memberNo);
            return View(loans);
        }

        public IActionResult Repay()
        {
            return View();
        }

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Pending()
        {
            // This would require a method in LoanService to get pending loans
            return View();
        }
    }
}