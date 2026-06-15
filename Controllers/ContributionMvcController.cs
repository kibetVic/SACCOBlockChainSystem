// Controllers/ContributionMvcController.cs
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
    public class ContributionMvcController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly IContributionService _contributionService;
        private readonly ILogger<ContributionMvcController> _logger;
        private readonly ApplicationDbContext _context;

        public ContributionMvcController(
            IMemberService memberService,
            IContributionService contributionService,
            ILogger<ContributionMvcController> logger,
            ApplicationDbContext context)
        {
            _memberService = memberService;
            _contributionService = contributionService;
            _logger = logger;
            _context = context;
        }

        // GET: /ContributionMvc/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                ViewBag.CompanyCode = companyCode;

                var allRecentContributions = await _contributionService.SearchContributionsAsync(
                    DateTime.Now.AddDays(-180),
                    DateTime.Now,
                    null,
                    null);

                var recentContributions = allRecentContributions
                    .Where(c => c.CompanyCode == companyCode)
                    .ToList();

                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);

                var viewModel = new
                {
                    RecentContributions = recentContributions,
                    ShareTypes = shareTypes,
                    TotalAmount = recentContributions.Sum(c => c.Amount),
                    TodayAmount = recentContributions
                        .Where(c => c.TransactionDate.Date == DateTime.Today)
                        .Sum(c => c.Amount)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contributions index");
                return View("Error");
            }
        }

        // GET: /ContributionMvc/Add
        public async Task<IActionResult> Add(string? memberNo = null)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);

                ViewBag.ShareTypes = shareTypes;
                ViewBag.CompanyCode = companyCode;

                var contributionDto = new ContributionDTO
                {
                    MemberNo = memberNo ?? string.Empty,
                    TransactionDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM",
                    CompanyCode = companyCode
                };

                return View(contributionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading add contribution form");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ContributionDTO contributionDto, bool printReceipt = true)
        {
            try
            {
                _logger.LogInformation("Add contribution POST action called");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid");

                    // Check if it's an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return BadRequest(new { Success = false, Message = "Invalid form data", Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
                    }

                    var companyCode = GetUserCompanyCode();
                    var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                    ViewBag.ShareTypes = shareTypes;
                    return View(contributionDto);
                }

                contributionDto.CompanyCode = GetUserCompanyCode();
                contributionDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";
                bool prompt = false;
                if (!string.IsNullOrEmpty(contributionDto.PromptPayment))
                {
                    if(contributionDto.PromptPayment.ToLower() == "true" || contributionDto.PromptPayment.ToLower() == "on")
                    {
                        prompt = true;
                    }
                }

                if (contributionDto.TransactionDate == default)
                {
                    contributionDto.TransactionDate = DateTime.Now;
                }

                _logger.LogInformation($"Adding contribution for member: {contributionDto.MemberNo}, Amount: {contributionDto.Amount:C}");

                var result = await _contributionService.AddContributionAsync(contributionDto,prompt);

                TempData["SuccessMessage"] = $"Contribution of {contributionDto.Amount:C} added successfully! Receipt: {result.ReceiptNo}";

                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "Contribution saved successfully",
                        ReceiptNo = result.ReceiptNo,
                        RedirectUrl = printReceipt ? Url.Action("PrintReceipt", new { receiptNo = result.ReceiptNo }) : null
                    });
                }

                if (printReceipt)
                {
                    return RedirectToAction("PrintReceipt", new { receiptNo = result.ReceiptNo });
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contribution");

                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, new { Success = false, Message = ex.Message });
                }

                if (ex.Message.Contains("not found in GL Setup"))
                {
                    ModelState.AddModelError("", ex.Message + " Please contact the administrator to configure the required accounts.");
                }
                else if (ex.Message.Contains("Validation error"))
                {
                    ModelState.AddModelError("", ex.Message.Replace("Validation error: ", ""));
                }
                else if (ex.Message.Contains("cannot be less than minimum") || ex.Message.Contains("cannot exceed maximum"))
                {
                    ModelState.AddModelError("Amount", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                ViewBag.ShareTypes = shareTypes;

                return View(contributionDto);
            }
        }

        // GET: /ContributionMvc/PrintReceipt/{receiptNo}
        public async Task<IActionResult> PrintReceipt(string receiptNo)
        {
            try
            {
                var contributions = await _contributionService.SearchContributionsAsync(null, null, null, null);
                var contribution = contributions.FirstOrDefault(c => c.ReceiptNo == receiptNo);

                if (contribution == null)
                {
                    return NotFound();
                }

                var member = await _contributionService.GetMemberByMemberNoAsync(contribution.MemberNo);
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                var shareType = shareTypes.FirstOrDefault(st => st.SharesCode == contribution.SharesCode);

                var companyName = await GetCompanyNameAsync(companyCode);
                var companyAddress = await GetCompanyAddressAsync(companyCode);
                var companyPhone = await GetCompanyPhoneAsync(companyCode);
                var companyEmail = await GetCompanyEmailAsync(companyCode);

                var receiptModel = new ReceiptViewModel
                {
                    ReceiptNo = contribution.ReceiptNo,
                    MemberNo = contribution.MemberNo,
                    MemberName = contribution.MemberName,
                    TransactionDate = contribution.TransactionDate,
                    Amount = contribution.Amount,
                    ShareTypeName = shareType?.SharesType ?? contribution.ShareTypeName,
                    PaymentMethod = "CASH", // You can store this in your Contrib table
                    ReferenceNo = contribution.ReferenceNo,
                    Remarks = contribution.Remarks,
                    BlockchainTxId = contribution.BlockchainTxId,
                    CompanyCode = companyCode,
                    CreatedBy = contribution.CreatedBy,
                    MemberPhone = member?.PhoneNo,
                    MemberIdNo = member?.Idno,
                    ShareBalanceAfter = contribution.TotalSharesAfter,
                    CompanyName = companyName,
                    CompanyAddress = companyAddress,
                    CompanyPhone = companyPhone,
                    CompanyEmail = companyEmail,
                    PrintedAt = DateTime.Now
                };

                return View(receiptModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing receipt {receiptNo}");
                TempData["ErrorMessage"] = "Error printing receipt: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: /ContributionMvc/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var contributions = await _contributionService.SearchContributionsAsync(null, null, null, null);
                var contribution = contributions.FirstOrDefault(c => c.Id == id);

                if (contribution == null)
                {
                    return NotFound();
                }

                var canEdit = CanEditContribution(contribution);
                if (!canEdit)
                {
                    TempData["ErrorMessage"] = "This contribution cannot be edited. It may be too old or already reconciled.";
                    return RedirectToAction("Details", new { id });
                }

                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);

                ViewBag.ShareTypes = shareTypes;
                ViewBag.CompanyCode = companyCode;
                ViewBag.ContributionId = id;

                var editDto = new ContributionDTO
                {
                    MemberNo = contribution.MemberNo,
                    TransactionDate = contribution.TransactionDate,
                    SharesCode = contribution.SharesCode,
                    Amount = contribution.Amount,
                    ReceiptNo = contribution.ReceiptNo,
                    Remarks = contribution.Remarks,
                    PaymentMethod = "CASH",
                    ReferenceNo = "",
                    CreatedBy = User.Identity?.Name,
                    CompanyCode = companyCode
                };

                return View(editDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for contribution {id}");
                return View("Error");
            }
        }

        // POST: /ContributionMvc/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ContributionDTO contributionDto, string editReason)
        {
            try
            {
                _logger.LogInformation($"Edit contribution POST action for ID: {id}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for contribution edit");
                    var companyCode = GetUserCompanyCode();
                    var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                    ViewBag.ShareTypes = shareTypes;
                    ViewBag.ContributionId = id;
                    return View(contributionDto);
                }

                if (string.IsNullOrEmpty(editReason))
                {
                    ModelState.AddModelError("", "Reason for edit is required");
                    var companyCode = GetUserCompanyCode();
                    var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                    ViewBag.ShareTypes = shareTypes;
                    ViewBag.ContributionId = id;
                    return View(contributionDto);
                }

                contributionDto.CompanyCode = GetUserCompanyCode();
                contributionDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var originalContributions = await _contributionService.SearchContributionsAsync(null, null, null, null);
                var originalContribution = originalContributions.FirstOrDefault(c => c.Id == id);

                if (originalContribution == null)
                {
                    return NotFound();
                }

                var correctedContribution = new ContributionDTO
                {
                    MemberNo = contributionDto.MemberNo,
                    TransactionDate = contributionDto.TransactionDate,
                    SharesCode = contributionDto.SharesCode,
                    Amount = contributionDto.Amount,
                    ReceiptNo = $"{originalContribution.ReceiptNo}-CORR",
                    Remarks = $"CORRECTION: {editReason}. Original: {originalContribution.Remarks}",
                    PaymentMethod = contributionDto.PaymentMethod,
                    ReferenceNo = contributionDto.ReferenceNo,
                    CreatedBy = contributionDto.CreatedBy,
                    CompanyCode = contributionDto.CompanyCode
                };

                var result = await _contributionService.AddContributionAsync(correctedContribution);

                TempData["SuccessMessage"] = $"Contribution corrected successfully! New Receipt: {result.ReceiptNo}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error editing contribution {id}");

                if (ex.Message.Contains("not found") ||
                    ex.Message.Contains("Validation error") ||
                    ex.Message.Contains("cannot be less") ||
                    ex.Message.Contains("cannot exceed"))
                {
                    ModelState.AddModelError("", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                ViewBag.ShareTypes = shareTypes;
                ViewBag.ContributionId = id;
                return View(contributionDto);
            }
        }

        // GET: /ContributionMvc/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var contributions = await _contributionService.SearchContributionsAsync(null, null, null, null);
                var contribution = contributions.FirstOrDefault(c => c.Id == id);

                if (contribution == null)
                {
                    return NotFound();
                }

                return View(contribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contribution details");
                return View("Error");
            }
        }

        // GET: /ContributionMvc/Member/{memberNo}
        public async Task<IActionResult> Member(string memberNo)
        {
            try
            {
                var history = await _contributionService.GetMemberContributionHistoryAsync(memberNo);
                return View(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading contributions for member {memberNo}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Search");
            }
        }

        // GET: /ContributionMvc/Search
        public async Task<IActionResult> Search()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);

                ViewBag.ShareTypes = shareTypes;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading search page");
                return View("Error");
            }
        }

        // GET: /ContributionMvc/SearchResults
        [HttpGet]
        public async Task<IActionResult> SearchResults(
            DateTime? fromDate,
            DateTime? toDate,
            string? memberNo,
            string? shareType)
        {
            try
            {
                var contributions = await _contributionService.SearchContributionsAsync(
                    fromDate, toDate, memberNo, shareType);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.MemberNo = memberNo;
                ViewBag.ShareType = shareType;
                ViewBag.TotalAmount = contributions.Sum(c => c.Amount);

                return View(contributions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contributions");
                return View("Error");
            }
        }

        // GET: /ContributionMvc/Report
        public async Task<IActionResult> Report()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var today = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var yearStart = new DateTime(today.Year, 1, 1);

                var todayContributions = await _contributionService.SearchContributionsAsync(today, today, null, null);
                var monthContributions = await _contributionService.SearchContributionsAsync(monthStart, today, null, null);
                var yearContributions = await _contributionService.SearchContributionsAsync(yearStart, today, null, null);

                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                var shareTypeSummary = new List<object>();

                foreach (var shareType in shareTypes)
                {
                    var contributions = await _contributionService.SearchContributionsAsync(
                        yearStart, today, null, shareType.SharesCode);

                    shareTypeSummary.Add(new
                    {
                        ShareType = shareType.SharesType,
                        Code = shareType.SharesCode,
                        Count = contributions.Count,
                        Total = contributions.Sum(c => c.Amount)
                    });
                }

                var viewModel = new
                {
                    Today = new
                    {
                        Count = todayContributions.Count,
                        Total = todayContributions.Sum(c => c.Amount)
                    },
                    ThisMonth = new
                    {
                        Count = monthContributions.Count,
                        Total = monthContributions.Sum(c => c.Amount)
                    },
                    ThisYear = new
                    {
                        Count = yearContributions.Count,
                        Total = yearContributions.Sum(c => c.Amount)
                    },
                    ShareTypeSummary = shareTypeSummary
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contribution report");
                return View("Error");
            }
        }

        #region Helper Methods

        private bool CanEditContribution(ContributionResponseDTO contribution)
        {
            var daysSinceContribution = (DateTime.Now - contribution.TransactionDate).TotalDays;
            if (daysSinceContribution > 7)
            {
                return false;
            }
            return true;
        }


        // Replace the existing helper methods with these:

        private async Task<string> GetCompanyNameAsync(string companyCode)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                return company?.CompanyName ?? "SACCO Blockchain System";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company name");
                return "SACCO Blockchain System";
            }
        }

        private async Task<string> GetCompanyAddressAsync(string companyCode)
        {
            try
            {
                // Try SaccoParram first
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                if (sacco != null && !string.IsNullOrEmpty(sacco.PhysicalAddress))
                {
                    return sacco.PhysicalAddress;
                }

                // Try Company table
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                // Build address from Company fields
                var addressParts = new List<string>();
                if (!string.IsNullOrEmpty(company?.Address)) addressParts.Add(company.Address);
                if (!string.IsNullOrEmpty(company?.County)) addressParts.Add(company.County);
                if (!string.IsNullOrEmpty(company?.SubCounty)) addressParts.Add(company.SubCounty);

                return addressParts.Any() ? string.Join(", ", addressParts) : "P.O. Box 12345 - 00100, Nairobi, Kenya";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company address");
                return "P.O. Box 12345 - 00100, Nairobi, Kenya";
            }
        }

        private async Task<string> GetCompanyPhoneAsync(string companyCode)
        {
            try
            {
                // Try SaccoParram first
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                if (sacco != null && !string.IsNullOrEmpty(sacco.Telephone))
                {
                    return sacco.Telephone;
                }

                // Try Company table
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                return company?.Telephone ?? "+254 700 000 000";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company phone");
                return "+254 700 000 000";
            }
        }

        private async Task<string> GetCompanyEmailAsync(string companyCode)
        {
            try
            {
                // Try SaccoParram first
                var sacco = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

                if (sacco != null && !string.IsNullOrEmpty(sacco.EmailAddress))
                {
                    return sacco.EmailAddress;
                }

                // Try Company table
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                return company?.Email ?? "info@sacco.co.ke";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company email");
                return "info@sacco.co.ke";
            }
        }

        // GET: /ContributionMvc/ReverseSearch
        [HttpGet]
        [Authorize(Roles = "Super Admin")]
        public async Task<IActionResult> ReverseSearch(string searchTerm)
        {
            try
            {
                if (!User.IsInRole("Super Admin"))
                {
                    TempData["ErrorMessage"] = "Only Super Administrators can reverse contributions.";
                    return RedirectToAction("Index");
                }

                ViewBag.SearchTerm = searchTerm;

                if (string.IsNullOrEmpty(searchTerm))
                {
                    return View();
                }

                var companyCode = GetUserCompanyCode();

                // Get contributions using the search service
                var contributions = await _contributionService.SearchContributionsAsync(null, null, null, null);

                // Find contribution that is NOT already reversed
                var contribution = contributions.FirstOrDefault(c =>
                    (c.ReceiptNo != null && c.ReceiptNo.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (c.TransactionNo != null && c.TransactionNo.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)));

                if (contribution == null)
                {
                    // Check if it's already reversed
                    var existingReversal = contributions.FirstOrDefault(c =>
                        c.ReceiptNo != null && c.ReceiptNo.Contains("-REVERSAL") &&
                        (c.ReceiptNo.Contains(searchTerm) || (c.TransactionNo != null && c.TransactionNo.Contains(searchTerm))));

                    if (existingReversal != null)
                    {
                        ViewBag.ErrorMessage = $"Transaction '{searchTerm}' has already been reversed. Reversal receipt: {existingReversal.ReceiptNo}";
                    }
                    else
                    {
                        ViewBag.ErrorMessage = $"No transaction found with Receipt/Transaction Number: '{searchTerm}'";
                    }
                    return View();
                }

                // Check if already reversed by Status or Receipt pattern
                if (contribution.Status == "REVERSED" || (contribution.ReceiptNo != null && contribution.ReceiptNo.Contains("-REVERSAL")))
                {
                    ViewBag.ErrorMessage = $"Transaction '{searchTerm}' has already been reversed.";
                    return View();
                }

                var reverseDto = new ContributionReverseDTO
                {
                    ContributionId = contribution.Id,
                    ReceiptNo = contribution.ReceiptNo,
                    MemberNo = contribution.MemberNo,
                    MemberName = contribution.MemberName,
                    Amount = contribution.Amount,
                    ShareTypeName = contribution.ShareTypeName,
                    TransactionDate = contribution.TransactionDate,
                    CreatedBy = contribution.CreatedBy,
                    BlockchainTxId = contribution.BlockchainTxId,
                    ReverseReason = string.Empty
                };

                return View("Reverse", reverseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching contribution for reversal");
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                return View();
            }
        }

        // POST: /ContributionMvc/Reverse
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Super Admin")]
        public async Task<IActionResult> Reverse(ContributionReverseDTO reverseDto)
        {
            try
            {
                _logger.LogInformation($"Reverse contribution POST action for ID: {reverseDto.ContributionId}");

                if (!ModelState.IsValid)
                {
                    return View("Reverse", reverseDto);
                }

                // Verify Super Admin role again
                if (!User.IsInRole("Super Admin"))
                {
                    TempData["ErrorMessage"] = "Only Super Administrators can reverse contributions.";
                    return RedirectToAction("Index");
                }

                var reversedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _contributionService.ReverseContributionAsync(
                    reverseDto.ContributionId,
                    reverseDto.ReverseReason,
                    reversedBy);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction("ReverseSearch");
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                    return View("Reverse", reverseDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reversing contribution {reverseDto.ContributionId}");
                TempData["ErrorMessage"] = $"Error reversing contribution: {ex.Message}";
                return View("Reverse", reverseDto);
            }
        }


        private string GetUserCompanyCode()
        {
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }

        #endregion

        // Helper to get payment method from contribution
        private string GetPaymentMethodFromContribution(ContributionResponseDTO contribution)
        {
            // You can store payment method in your Contrib table
            // For now, return a default or try to deduce from remarks/reference
            return "CASH";
        }


        // GET: /ContributionMvc/MemberContribute
        [Authorize]
        public async Task<IActionResult> MemberContribute()
        {
            try
            {
                // Get the logged-in member's number from claims
                var memberNo = GetLoggedInMemberNumber();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["ErrorMessage"] = "Member not found. Please login again.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                // Verify member exists and is active
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                if (member == null || member.Status != 1)
                {
                    TempData["ErrorMessage"] = "Your account is not active. Please contact administrator.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                var memberContributions = await _contributionService.GetMemberContributionsAsync(memberNo);
                var currentShareBalance = await _contributionService.GetMemberShareBalanceAsync(memberNo);

                ViewBag.ShareTypes = shareTypes;
                ViewBag.CompanyCode = companyCode;
                ViewBag.MemberName = $"{member.Surname} {member.OtherNames}";
                ViewBag.MemberNo = memberNo;
                ViewBag.CurrentShareBalance = currentShareBalance;

                // Create DTO with member already pre-filled
                var contributionDto = new ContributionDTO
                {
                    MemberNo = memberNo,
                    TransactionDate = DateTime.Now,
                    CreatedBy = member.MemberNo,
                    CompanyCode = companyCode
                };

                // Get recent contributions for this member only
                var recentContributions = await _contributionService.GetMemberContributionsAsync(memberNo);

                var viewModel = new
                {
                    ContributionDto = contributionDto,
                    RecentContributions = recentContributions,
                    ShareTypes = shareTypes,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    MemberNo = memberNo,
                    ShareBalance = currentShareBalance
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member contribution form");
                TempData["ErrorMessage"] = "Error loading contribution form";
                return RedirectToAction("MemberLogin", "Account");
            }
        }

        // POST: /ContributionMvc/MemberContribute
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MemberContribute(ContributionDTO contributionDto, bool printReceipt = true)
        {
            // Declare these outside try block so they're accessible in catch
            string loggedInMemberNo = null;
            Member member = null;

            try
            {
                _logger.LogInformation("Member contribution POST action called");

                // Get logged-in member number - CRITICAL: Override any submitted MemberNo
                loggedInMemberNo = GetLoggedInMemberNumber();

                if (string.IsNullOrEmpty(loggedInMemberNo))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return BadRequest(new { Success = false, Message = "Member session expired. Please login again." });
                    }
                    TempData["ErrorMessage"] = "Session expired. Please login again.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                // FORCE the MemberNo to be the logged-in member - PREVENT spoofing
                contributionDto.MemberNo = loggedInMemberNo;

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid for member contribution");

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return BadRequest(new { Success = false, Message = "Invalid form data", Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
                    }

                    var companyCode = GetUserCompanyCode();
                    var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                    ViewBag.ShareTypes = shareTypes;
                    return View(contributionDto);
                }

                // Verify member exists and is active
                member = await _contributionService.GetMemberByMemberNoAsync(loggedInMemberNo);
                if (member == null || member.Status != 1)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return BadRequest(new { Success = false, Message = "Member account is not active." });
                    }
                    TempData["ErrorMessage"] = "Your account is not active.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                contributionDto.CompanyCode = GetUserCompanyCode();
                contributionDto.CreatedBy = member.MemberNo; // Use MemberNo as CreatedBy

                if (contributionDto.TransactionDate == default)
                {
                    contributionDto.TransactionDate = DateTime.Now;
                }

                _logger.LogInformation($"Member {loggedInMemberNo} adding contribution of {contributionDto.Amount:C}");

                var result = await _contributionService.AddContributionAsync(contributionDto);

                TempData["SuccessMessage"] = $"Contribution of {contributionDto.Amount:C} added successfully! Receipt: {result.ReceiptNo}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "Contribution saved successfully",
                        ReceiptNo = result.ReceiptNo,
                        RedirectUrl = printReceipt ? Url.Action("PrintReceipt", new { receiptNo = result.ReceiptNo }) : null
                    });
                }

                if (printReceipt)
                {
                    return RedirectToAction("PrintReceipt", new { receiptNo = result.ReceiptNo });
                }

                return RedirectToAction("MemberContribute");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member contribution");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, new { Success = false, Message = ex.Message });
                }

                if (ex.Message.Contains("not found in GL Setup"))
                {
                    ModelState.AddModelError("", ex.Message + " Please contact the administrator.");
                }
                else if (ex.Message.Contains("Validation error"))
                {
                    ModelState.AddModelError("", ex.Message.Replace("Validation error: ", ""));
                }
                else if (ex.Message.Contains("cannot be less than minimum") || ex.Message.Contains("cannot exceed maximum"))
                {
                    ModelState.AddModelError("Amount", ex.Message);
                }
                else
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }

                var companyCode = GetUserCompanyCode();
                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
                ViewBag.ShareTypes = shareTypes;

                // Reload member contributions - use loggedInMemberNo if available, otherwise try to get it again
                var memberNoForReload = loggedInMemberNo ?? GetLoggedInMemberNumber();
                var recentContributions = await _contributionService.GetMemberContributionsAsync(memberNoForReload);
                var currentShareBalance = await _contributionService.GetMemberShareBalanceAsync(memberNoForReload);

                // Get member details if member is null (from catch block)
                if (member == null && !string.IsNullOrEmpty(memberNoForReload))
                {
                    member = await _contributionService.GetMemberByMemberNoAsync(memberNoForReload);
                }

                var memberName = member != null ? $"{member.Surname} {member.OtherNames}" : "Member";

                ViewBag.MemberName = memberName;
                ViewBag.MemberNo = memberNoForReload;
                ViewBag.CurrentShareBalance = currentShareBalance;

                var viewModel = new
                {
                    ContributionDto = contributionDto,
                    RecentContributions = recentContributions,
                    ShareTypes = shareTypes,
                    MemberName = memberName,
                    MemberNo = memberNoForReload,
                    ShareBalance = currentShareBalance
                };

                return View(viewModel);
            }
        }

        private string GetLoggedInMemberNumber()
        {
            try
            {
                // First try to get from claims
                var memberNoClaim = User.FindFirst("MemberNo")?.Value;
                if (!string.IsNullOrEmpty(memberNoClaim))
                {
                    _logger.LogDebug($"Found MemberNo in claims: {memberNoClaim}");
                    return memberNoClaim;
                }

                // Try from Name claim (for member login)
                var nameClaim = User.Identity?.Name;
                if (!string.IsNullOrEmpty(nameClaim))
                {
                    // Check if this username exists in Members table
                    var member = _context.Members.FirstOrDefault(m => m.MemberNo == nameClaim || m.UserName == nameClaim);
                    if (member != null)
                    {
                        _logger.LogDebug($"Found MemberNo from Name claim: {member.MemberNo}");
                        return member.MemberNo;
                    }
                }

                // Try from session
                var sessionMemberNo = HttpContext.Session.GetString("MemberNo");
                if (!string.IsNullOrEmpty(sessionMemberNo))
                {
                    _logger.LogDebug($"Found MemberNo in session: {sessionMemberNo}");
                    return sessionMemberNo;
                }

                _logger.LogWarning("Could not retrieve logged-in member number");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logged-in member number");
                return null;
            }
        }
    }
}