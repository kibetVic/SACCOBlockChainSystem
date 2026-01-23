// Controllers/MemberMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class MemberMvcController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MemberMvcController> _logger;

        public MemberMvcController(
            IMemberService memberService, ApplicationDbContext context, ILogger<MemberMvcController> logger)
        {
            _memberService = memberService;
            _context = context; // Initialize
            _logger = logger;
        }


        // Update the Index method in MemberMvcController.cs
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get all active members
                var members = await _memberService.GetAllMembersAsync();

                // Create a view model that includes both members and dashboard data
                var viewModel = new MembersIndexViewModel
                {
                    Members = members.Take(10).ToList(), // Top 10 members
                    AllMembers = members, // All members for the blockchain visualization
                    TotalMembers = members.Count,
                    ActiveMembers = members.Count(m => m.Status == 1),
                    // Add other required properties as needed
                };

                return View(viewModel); // Will look for Views/MemberMvc/Index.cshtml
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members");
                return View("Error");
            }
        }


        // GET: /MemberMvc/Register
        public IActionResult Register()
        {
            return View(new MemberRegistrationDTO()); 
        }


        // POST: /MemberMvc/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(MemberRegistrationDTO registration)
        {
            try
            {
                _logger.LogInformation("Register POST action called");
                _logger.LogInformation($"Model State IsValid: {ModelState.IsValid}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Model error: {error.ErrorMessage}");
                    }
                    return View(registration);
                }

                _logger.LogInformation($"Registering member: {registration.Surname} {registration.OtherNames}");
                var result = await _memberService.RegisterMemberAsync(registration);

                TempData["SuccessMessage"] = $"Member {result.MemberNo} registered successfully!";
                return RedirectToAction("Details", new { memberNo = result.MemberNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member");

                // Check for specific error types
                if (ex.Message.Contains("already exists") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("Duplicate") ||
                    ex.Message.Contains("Phone number") ||
                    ex.Message.Contains("ID number") ||
                    ex.Message.Contains("Email"))
                {
                    // Add error to ModelState instead of redirecting to error view
                    ModelState.AddModelError("", ex.Message);

                    // Also add to specific field if it's a field-specific error
                    if (ex.Message.Contains("ID number"))
                    {
                        ModelState.AddModelError("IdNo", ex.Message);
                    }
                    if (ex.Message.Contains("Phone number"))
                    {
                        ModelState.AddModelError("PhoneNo", ex.Message);
                    }
                    if (ex.Message.Contains("Email"))
                    {
                        ModelState.AddModelError("Email", ex.Message);
                    }
                }
                else
                {
                    // For other errors, add to ModelState
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                // Return to the same registration view with errors
                return View(registration);
            }
        }

        // GET: /MemberMvc/Details/{memberNo}
        public async Task<IActionResult> Details(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }
                return View(member);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member details");
                return View("Error");
            }
        }

        // GET: /MemberMvc/Search
        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Transactions(string memberNo = null)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                // Show search form when no member number provided
                return View("TransactionsSearch");
            }

            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    ViewBag.ErrorMessage = "Member not found";
                    return View("TransactionsSearch");
                }

                var viewModel = new MemberTransactionsViewModel
                {
                    Member = member,
                    Transactions = await GetMemberTransactionsAsync(memberNo),
                    LoanHistory = await GetMemberLoanHistoryAsync(memberNo),
                    TotalShares = await _memberService.GetMemberShareBalanceAsync(memberNo),
                };

                viewModel.LastTransactionDate = viewModel.Transactions.FirstOrDefault()?.ContributionDate;

                return View("Transactions", viewModel); // Specify view name explicitly
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member transactions");
                return View("Error");
            }
        }

        // GET: /MemberMvc/Transactions/{memberNo} (for direct links)
        [HttpGet("Transactions/{memberNo}")]
        public async Task<IActionResult> TransactionsWithMemberNo(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }

                var viewModel = new MemberTransactionsViewModel
                {
                    Member = member,
                    Transactions = await GetMemberTransactionsAsync(memberNo),
                    LoanHistory = await GetMemberLoanHistoryAsync(memberNo),
                    TotalShares = await _memberService.GetMemberShareBalanceAsync(memberNo),
                };

                viewModel.LastTransactionDate = viewModel.Transactions.FirstOrDefault()?.ContributionDate;

                return View("Transactions", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member transactions");
                return View("Error");
            }
        }

        // GET: /MemberMvc/SearchMembers (for AJAX search)
        [HttpGet("SearchMembers")]
        public async Task<IActionResult> SearchMembersAjax(string searchTerm)
        {
            try
            {
                var members = await _memberService.SearchMembersAsync(searchTerm);
                return Ok(new { success = true, data = members });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return Ok(new { success = false, message = "Error searching members" });
            }
        }

        // Helper methods
        private async Task<List<Transactions2>> GetMemberTransactionsAsync(string memberNo)
        {
            return await _context.Transactions2
                .Where(t => t.MemberNo == memberNo)
                .OrderByDescending(t => t.AuditDateTime)
                .Take(100)
                .ToListAsync();
        }

        private async Task<List<Loan>> GetMemberLoanHistoryAsync(string memberNo)
        {
            return await _context.Loans
                .Where(l => l.MemberNo == memberNo)
                .OrderByDescending(l => l.ApplicDate)
                .ToListAsync();
        }
    }
}