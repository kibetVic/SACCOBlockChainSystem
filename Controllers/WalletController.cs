using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IUserService _userService;
        private readonly IMemberService _memberService;
        private readonly WalletService _walletService;

        public WalletController(ApplicationDbContext context, IUserService userService, ILogger<AccountController> logger, IMemberService memberService, WalletService walletService)
        {
            _context = context;
            _userService = userService; // This should be set
            _logger = logger;
            _walletService = walletService;
            _memberService = memberService;
        }
        public async Task<IActionResult> Index()
        {
            var isUser = User.Identity.IsAuthenticated;
            if (!isUser)
            {
                return RedirectToAction("Login","AccountController");
            }
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            var wallets = _context.Wallets.AsNoTracking().Where(c=>c.CompanyCode == companyCode).ToList();
            var members = await _memberService.GetAllMembersAsync();
            foreach (var wallet in wallets) {
                wallet.Member = members.FirstOrDefault(m => m.Id == wallet.MemberId) ?? new Models.Member();
            }
            return View(wallets);
        }

        public async Task<IActionResult> Create()
        {
            var isUser = User.Identity.IsAuthenticated;
            if (!isUser)
            {
                return RedirectToAction("Login", "AccountController");
            }
            var companyCode = User.FindFirst("CompanyCode")?.Value;

            // Get member IDs that already have wallets
            var walletMemberIds = await _context.Wallets
                .AsNoTracking()
                .Where(w => w.CompanyCode == companyCode)
                .Select(w => w.MemberId)
                .ToListAsync();

            // Get all members
            var members = await _memberService.GetAllMembersAsync();

            // Filter members not in wallets
            var membersWithoutWallets = members
                .Where(m => !walletMemberIds.Contains(m.Id))
                .ToList();

            return View(membersWithoutWallets);
        }

        [HttpGet]
        public async Task<IActionResult> CreateWallet(string mno)
        {
            var isUser = User.Identity.IsAuthenticated;
            if (!isUser)
            {
                return RedirectToAction("Login", "AccountController");
            }
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            //var wallets = _context.Wallets.AsNoTracking().Where(c => c.CompanyCode == companyCode).ToList();
            var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m=>m.MemberNo == mno && m.CompanyCode == companyCode);
            if (member == null)
                return NotFound();
            var wallet = await _context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.MemberId == member.Id && w.CompanyCode == member.CompanyCode);
            member.Wallet = wallet;
            return View(member);
        }

        [HttpGet]
        public async Task<IActionResult> ViewWallet(string mno)
        {
            var isUser = User.Identity.IsAuthenticated;
            if (!isUser)
            {
                return RedirectToAction("Login", "AccountController");
            }
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            //var wallets = _context.Wallets.AsNoTracking().Where(c => c.CompanyCode == companyCode).ToList();
            var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberNo == mno && m.CompanyCode == companyCode);
            if (member == null)
                return NotFound();
            var wallet = await _context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.MemberId == member.Id && w.CompanyCode == member.CompanyCode);
            if(wallet == null)
            {
                return NotFound();
            }
            wallet.Member = member;
            return View(wallet);
        }
        [HttpPost]
        public async Task<IActionResult> RegisterMemberWallet(int id, string mno)
        {
            var isUser = User.Identity.IsAuthenticated;
            if (!isUser)
            {
                return RedirectToAction("Login", "AccountController");
            }
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value;

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
                var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == mno && m.CompanyCode == companyCode);
                if(member == null)
                {
                    return Ok(new { Success = false, Message = "No member found" });
                }
                var result = await _walletService.RegisterMemberAsync(member);

                return Ok(new
                {
                    Success = true,
                    Message = "Member registered successfully",
                    Data = result ,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while registering member",
                    Error = ex.Message
                });
            }
        }

    }
}
