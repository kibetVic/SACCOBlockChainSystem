//using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using SACCOBlockChainSystem.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IDashboardCacheService _dashboardCacheService;
        private WalletService _walletService;

        public HomeController(
            IDashboardService dashboardService,
            IBlockchainService blockchainService,
            ILogger<HomeController> logger,
            ApplicationDbContext context,WalletService walletService,
            IWebHostEnvironment webHostEnvironment, IDashboardCacheService dashboardCacheService)
        {
            _dashboardService = dashboardService;
            _blockchainService = blockchainService;
            _logger = logger;
            _context = context;
            _walletService = walletService;
            _webHostEnvironment = webHostEnvironment;
            _dashboardCacheService = dashboardCacheService;
        }

        public async Task<IActionResult> AccountSetup()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            if (userRole.ToUpper() != "MEMBER")
            {
                return RedirectToAction("Index", "Home");
            }
            var wp = new WalletPinSetup();
            var mno = User.FindFirst("MemberNo")?.Value;
            var companyCode = User.FindFirst("CompanyCode")?.Value;
            ViewBag.MemberNo = mno;
            var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberNo == mno && m.CompanyCode == companyCode);
            if (!string.IsNullOrEmpty(member.Pin))
            {
                ViewBag.Reset = true;
            }
            else
            {
                ViewBag.Reset = false;
            }
             return View(wp);
        }

        [HttpPost]
        public async Task<IActionResult> AccountSetup(WalletPinSetup model)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            if (userRole.ToUpper() != "MEMBER")
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.MemberNo = User.FindFirst("MemberNo")?.Value;
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid try again.";
                return View(model);
            }
            var userCompanyCode = User.FindFirst("CompanyCode")?.Value;
            var memberNo = User.FindFirst("MemberNo")?.Value;
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == userCompanyCode);
            if(member == null)
            {
                TempData["ErrorMessage"] = "Your account is not active. Please contact administrator.";

                return View(model);
            }
            if(model.ConfirmPin != model.Pin)
            {
                TempData["ErrorMessage"] = "Confirmation and pin do not match";

                return View(model);
            }
            if (!string.IsNullOrEmpty(member.Pin))
            {
                if (string.IsNullOrEmpty(model.UserPin))
                {
                    TempData["ErrorMessage"] = "Old pin is required";

                    return View(model);
                }
                if(EncryptionHelper.Encrypt(member.Pin) != model.UserPin)
                {
                    TempData["ErrorMessage"] = "Old pin is invalid. Try again.";

                    return View(model);
                }
            }
            member.Pin = EncryptionHelper.Encrypt(model.Pin);
            //_context.Members.Update(member);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Pin set successfully. Proceed.";
            return RedirectToAction("Index", "Home");
            //return View(model);


        }
        public async Task<IActionResult> Index(string? companyCode)
        {
            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                // Handle Member role - redirect to MemberIndex
                if (userRole?.ToUpper() == "MEMBER")
                {
                    // ... existing member code ...
                }

                // Determine effective company code
                string effectiveCompanyCode = null;
                if (!isSuperAdmin)
                {
                    effectiveCompanyCode = userCompanyCode;
                    companyCode = userCompanyCode;
                }
                else if (!string.IsNullOrEmpty(companyCode))
                {
                    effectiveCompanyCode = companyCode;
                }

                // ✅ Get cached dashboard data - includes ALL calculations now!
                var dashboard = await _dashboardCacheService.GetDashboardDataAsync(effectiveCompanyCode, isSuperAdmin);

                // Get companies for filter dropdown (only for Super Admin)
                if (isSuperAdmin)
                {
                    dashboard.Companies = await _context.Companies
                        .Where(c => c.Project == true)
                        .Select(c => new CompanyInfo { Code = c.CompanyCode, Name = c.CompanyName ?? c.CompanyCode })
                        .OrderBy(c => c.Name)
                        .ToListAsync();
                }

                // Set UI properties
                dashboard.SelectedCompanyCode = effectiveCompanyCode ?? (isSuperAdmin ? "ALL" : userCompanyCode);
                dashboard.SelectedCompanyName = isSuperAdmin && string.IsNullOrEmpty(effectiveCompanyCode)
                    ? "All Companies"
                    : dashboard.SelectedCompanyName;
                dashboard.UserGroup = GetUserGroup();
                dashboard.UserRoles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                return View("Error");
            }
        }

        //public async Task<IActionResult> Index(string? companyCode)
        //{
        //    try
        //    {
        //        // Get the logged-in user's role and company code from claims
        //        var userRole = User.FindFirstValue(ClaimTypes.Role);
        //        var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
        //        var userCompanyCode = User.FindFirst("CompanyCode")?.Value ??
        //                              User.FindFirst("SaccoCode")?.Value ??
        //                              User.FindFirst("Company")?.Value;

        //        if(userRole.ToUpper() == "MEMBER")
        //        {
        //           // var companyCode = User.FindFirst("CompanyCode")?.Value;
        //            var uid = User.FindFirst("UserId")?.Value;

        //            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.MemberId == int.Parse(uid) && w.CompanyCode == userCompanyCode);
        //            if(wallet == null)
        //            {
        //                var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == int.Parse(uid) && m.CompanyCode == userCompanyCode);
        //                wallet = await _walletService.RegisterMemberAsync(member );
        //                if (wallet == null || wallet.MemberId == 0)
        //                {
        //                    ModelState.AddModelError(string.Empty, "Invalid no wallet associated with member.");
        //                    return RedirectToAction("MemberLogin", "Account");
        //                }
        //            }
        //            return View("MemberIndex");
        //        }

        //        // Determine the effective company code for filtering
        //        string effectiveCompanyCode = null;

        //        if (isSuperAdmin)
        //        {
        //            // Super Admin: Use selected company code if provided, otherwise null (show all)
        //            effectiveCompanyCode = string.IsNullOrEmpty(companyCode) ? null : companyCode;
        //            ViewBag.ShowCompanyFilter = true;
        //        }
        //        else
        //        {
        //            // Non-SuperAdmin: Always limited to their own company
        //            effectiveCompanyCode = userCompanyCode;
        //            companyCode = userCompanyCode; // Override any passed company code
        //            ViewBag.ShowCompanyFilter = false;
        //        }

        //        var userCompanyName = User.FindFirst("CompanyName")?.Value ??
        //                              User.FindFirst("SaccoName")?.Value;

        //        DashboardVM dashboard = await GetUniversalDashboardDataAsync(effectiveCompanyCode, isSuperAdmin);

        //        var cutoffDate = DateTime.Now.AddMonths(-6);

        //        // Build member query with role-based filtering
        //        var membersQuery = _context.Members.AsQueryable();

        //        // Apply filtering based on role and effective company code
        //        if (!isSuperAdmin && !string.IsNullOrEmpty(effectiveCompanyCode))
        //        {
        //            // Non-SuperAdmin: Filter by their company
        //            membersQuery = membersQuery.Where(m => m.CompanyCode == effectiveCompanyCode);
        //            dashboard.SelectedCompanyCode = effectiveCompanyCode;
        //            dashboard.SelectedCompanyName = userCompanyName ?? effectiveCompanyCode;
        //        }
        //        else if (isSuperAdmin && !string.IsNullOrEmpty(effectiveCompanyCode))
        //        {
        //            // SuperAdmin: Filter by selected company
        //            membersQuery = membersQuery.Where(m => m.CompanyCode == effectiveCompanyCode);
        //            dashboard.SelectedCompanyCode = effectiveCompanyCode;
        //            dashboard.SelectedCompanyName = effectiveCompanyCode;
        //        }
        //        else if (isSuperAdmin && string.IsNullOrEmpty(effectiveCompanyCode))
        //        {
        //            // SuperAdmin: No company filter - show ALL companies
        //            dashboard.SelectedCompanyName = "All Companies";
        //            dashboard.SelectedCompanyCode = "ALL";
        //            // Don't apply any company filter to membersQuery
        //        }

        //        // Get all members (filtered appropriately)
        //        var members = await membersQuery
        //            .Where(m => m.Dob.HasValue || m.Status.HasValue)
        //            .Select(m => new
        //            {
        //                m.MemberNo,
        //                m.Sex,
        //                m.Dob,
        //                m.Status,
        //                m.EffectDate,
        //                m.Withdrawn,
        //                m.Dormant,
        //                m.CompanyCode
        //            })
        //            .ToListAsync();

        //        // ==========================
        //        // MEMBER STATISTICS
        //        // ==========================
        //        dashboard.TotalWomen = members.Count(m =>
        //            !string.IsNullOrEmpty(m.Sex) && m.Sex.ToUpper() == "FEMALE");

        //        dashboard.TotalMen = members.Count(m =>
        //            !string.IsNullOrEmpty(m.Sex) && m.Sex.ToUpper() == "MALE");

        //        dashboard.TotalOthers = members.Count(m =>
        //            string.IsNullOrEmpty(m.Sex) ||
        //            (m.Sex.ToUpper() != "MALE" && m.Sex.ToUpper() != "FEMALE"));

        //        dashboard.TotalMembers = members.Count;

        //        // ACTIVE & DORMANT MEMBERS
        //        var activeMemberNos = await GetActiveMemberNumbersAsync(cutoffDate, effectiveCompanyCode, isSuperAdmin);
        //        var activeFromStatus = members.Where(m => m.Status == 1).Select(m => m.MemberNo).ToHashSet();

        //        var allActiveMembers = new HashSet<string>(activeMemberNos);
        //        allActiveMembers.UnionWith(activeFromStatus);

        //        dashboard.ActiveMembers = allActiveMembers.Count;
        //        dashboard.DormantMembers = dashboard.TotalMembers - dashboard.ActiveMembers;

        //        // ACTIVE/DORMANT BY GENDER
        //        dashboard.ActiveWomen = members.Count(m =>
        //            !string.IsNullOrEmpty(m.Sex) &&
        //            m.Sex.ToUpper() == "FEMALE" &&
        //            allActiveMembers.Contains(m.MemberNo));

        //        dashboard.DormantWomen = dashboard.TotalWomen - dashboard.ActiveWomen;

        //        dashboard.ActiveMen = members.Count(m =>
        //            !string.IsNullOrEmpty(m.Sex) &&
        //            m.Sex.ToUpper() == "MALE" &&
        //            allActiveMembers.Contains(m.MemberNo));

        //        dashboard.DormantMen = dashboard.TotalMen - dashboard.ActiveMen;

        //        // YOUTH CALCULATION (<= 35 years)
        //        var membersWithAge = members.Where(m => m.Dob.HasValue).ToList();

        //        dashboard.YouthTotal = membersWithAge.Count(m =>
        //        {
        //            var age = CalculateAgeSafe(m.Dob.Value);
        //            return age <= 35;
        //        });

        //        dashboard.YouthMale = membersWithAge.Count(m =>
        //        {
        //            var age = CalculateAgeSafe(m.Dob.Value);
        //            return age <= 35 && !string.IsNullOrEmpty(m.Sex) && m.Sex.ToUpper() == "MALE";
        //        });

        //        dashboard.YouthFemale = membersWithAge.Count(m =>
        //        {
        //            var age = CalculateAgeSafe(m.Dob.Value);
        //            return age <= 35 && !string.IsNullOrEmpty(m.Sex) && m.Sex.ToUpper() == "FEMALE";
        //        });

        //        // ==========================
        //        // FINANCIAL DATA FROM TABLES
        //        // ==========================

        //        // Get Member Contributions from ContribShares table
        //        var contributionsData = await GetContributionsDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalContributions = contributionsData.Total;
        //        dashboard.WomenContributions = contributionsData.Women;
        //        dashboard.MenContributions = contributionsData.Men;
        //        dashboard.OthersContributions = contributionsData.Others;

        //        // Get Share Capital from Shares table
        //        var shareCapitalData = await GetShareCapitalDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalShareCapital = shareCapitalData.Total;
        //        dashboard.WomenShareCapital = shareCapitalData.Women;
        //        dashboard.MenShareCapital = shareCapitalData.Men;
        //        dashboard.OthersShareCapital = shareCapitalData.Others;

        //        // Get Non-Withdrawable Deposits from ContribShares (DepositsAmount)
        //        var depositsData = await GetDepositsDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalDeposits = depositsData.Total;
        //        dashboard.WomenDeposits = depositsData.Women;
        //        dashboard.MenDeposits = depositsData.Men;
        //        dashboard.OthersDeposits = depositsData.Others;

        //        // Get Registration Fees from Members table (RegFee)
        //        var registrationData = await GetRegistrationFeesDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalRegistrationFees = registrationData.Total;
        //        dashboard.WomenRegistrationFees = registrationData.Women;
        //        dashboard.MenRegistrationFees = registrationData.Men;
        //        dashboard.OthersRegistrationFees = registrationData.Others;

        //        // Get Loans Taken from Loans table
        //        var loansTakenData = await GetLoansTakenDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalLoansTaken = loansTakenData.Total;
        //        dashboard.WomenLoansTaken = loansTakenData.Women;
        //        dashboard.MenLoansTaken = loansTakenData.Men;
        //        dashboard.OthersLoansTaken = loansTakenData.Others;

        //        // Get Loan Balances from Loans table (outstanding)
        //        var loanBalancesData = await GetLoanBalancesDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalLoanBalances = loanBalancesData.Total;
        //        dashboard.WomenLoanBalances = loanBalancesData.Women;
        //        dashboard.MenLoanBalances = loanBalancesData.Men;
        //        dashboard.OthersLoanBalances = loanBalancesData.Others;

        //        // Get Loans Paid from Loanbals table (Cleared)
        //        var loansPaidData = await GetLoansPaidDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalLoansPaid = loansPaidData.Total;
        //        dashboard.WomenLoansPaid = loansPaidData.Women;
        //        dashboard.MenLoansPaid = loansPaidData.Men;
        //        dashboard.OthersLoansPaid = loansPaidData.Others;

        //        // Get Total Loanees from Loans table (distinct members with loans)
        //        var loaneesData = await GetLoaneesDataAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalLoanees = loaneesData.Total;
        //        dashboard.WomenLoanees = loaneesData.Women;
        //        dashboard.MenLoanees = loaneesData.Men;
        //        dashboard.OthersLoanees = loaneesData.Others;

        //        dashboard.RepaymentRate = await CalculateRepaymentRateAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.PARPercent = await CalculatePARPercentAsync(effectiveCompanyCode, isSuperAdmin);
        //        //dashboard.AmountPastDueRate = await CalculatePenaltyInterestRateAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.AmountPastDueRate = await CalculateAmountPastDueRateAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.AmountPastDueRate = dashboard.PARPercent;
        //        dashboard.OutstandingLoanPortfolio = await CalculateOutstandingLoanPortfolioAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.ArrearsBalance = await CalculateArrearsBalanceAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.TotalArrears = dashboard.ArrearsBalance;
        //        dashboard.WomenParticipationRate = await CalculateWomenParticipationRateAsync(effectiveCompanyCode, isSuperAdmin);
        //        dashboard.LoanPortfolioHealth = GetLoanPortfolioHealth(dashboard.PARPercent);

        //        // Get grants data - SuperAdmin sees all, others see filtered
        //        dashboard.InclusionGrantTotal = await GetGrantTotalAsync("inclusion grant", effectiveCompanyCode, isSuperAdmin);
        //        dashboard.MatchingGrantTotal = await GetGrantTotalAsync("matching grant", effectiveCompanyCode, isSuperAdmin);

        //        // Load chart data
        //        dashboard.MonthlyTransactions = await GetMonthlyTransactionsDataAsync(6, effectiveCompanyCode, isSuperAdmin);
        //        dashboard.MemberGrowth = await GetMemberGrowthDataAsync(12, effectiveCompanyCode, isSuperAdmin);

        //        // Get companies for filter dropdown (only for Super Admin)
        //        if (isSuperAdmin)
        //        {
        //            dashboard.Companies = await _context.Companies
        //                .Where(c => c.Project == true)
        //                .Select(c => new CompanyInfo
        //                {
        //                    Code = c.CompanyCode,
        //                    Name = c.CompanyName ?? c.CompanyCode
        //                })
        //                .OrderBy(c => c.Name)
        //                .ToListAsync();
        //        }
        //        else
        //        {
        //            dashboard.Companies = new List<CompanyInfo>();
        //        }

        //        // Get user info
        //        dashboard.UserGroup = GetUserGroup();
        //        dashboard.UserRoles = User.Claims
        //            .Where(c => c.Type == ClaimTypes.Role)
        //            .Select(c => c.Value)
        //            .ToList();

        //        ViewData["Title"] = $"{dashboard.UserGroup} Dashboard";
        //        ViewData["Subtitle"] = $"SACCO Blockchain System - {dashboard.SelectedCompanyName}";

        //        return View(dashboard);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading dashboard: {Message}", ex.Message);
        //        _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);

        //        if (_webHostEnvironment.IsDevelopment())
        //        {
        //            return Content($"Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
        //        }

        //        return View("Error");
        //    }
        //}


        #region Financial Data Methods
        private static string NormalizeGender(string? gender)
        {
            if (string.IsNullOrEmpty(gender))
                return "OTHERS";

            var normalized = gender.ToUpper().Trim();

            if (normalized == "M" || normalized == "MALE")
                return "MALE";

            if (normalized == "F" || normalized == "FEMALE")
                return "FEMALE";

            return "OTHERS";
        }

        // Helper method to get filtered member numbers based on role and company
        private async Task<List<string>> GetFilteredMemberNosAsync(string? companyCode, bool isSuperAdmin)
        {
            var membersQuery = _context.Members.AsQueryable();

            // Only apply company filter if:
            // 1. Not SuperAdmin (they have a company assigned), OR
            // 2. SuperAdmin with a specific company selected
            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
            }
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
            }
            // If SuperAdmin and companyCode is null, don't filter - return all members

            return await membersQuery.Select(m => m.MemberNo).ToListAsync();
        }

        // CONTRIBUTIONS - From ContribShare table (ShareCapitalAmount + DepositsAmount)
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetContributionsDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var membersQuery = _context.Members.AsQueryable();

            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
            }
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
            }

            var members = await membersQuery
                .Select(m => new { MemberNo = m.MemberNo.Trim(), m.Sex })
                .ToListAsync();

            if (!members.Any())
            {
                _logger.LogInformation("No members found for contributions calculation");
                return (0, 0, 0, 0);
            }

            string targetCompanyCode = null;
            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                targetCompanyCode = companyCode;
            }
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                targetCompanyCode = companyCode;
            }

            List<dynamic> contributions;

            if (targetCompanyCode != null)
            {
                contributions = await _context.ContribShares
                    .Where(c => c.CompanyCode == targetCompanyCode
                        && c.MemberNo != null
                        && ((c.ShareCapitalAmount.HasValue && c.ShareCapitalAmount.Value > 0) ||
                            (c.DepositsAmount.HasValue && c.DepositsAmount.Value > 0)))
                    .Select(c => new
                    {
                        MemberNo = c.MemberNo.Trim(),
                        c.ShareCapitalAmount,
                        c.DepositsAmount
                    })
                    .ToListAsync<dynamic>();
            }
            else
            {
                contributions = await _context.ContribShares
                    .Where(c => c.MemberNo != null
                        && ((c.ShareCapitalAmount.HasValue && c.ShareCapitalAmount.Value > 0) ||
                            (c.DepositsAmount.HasValue && c.DepositsAmount.Value > 0)))
                    .Select(c => new
                    {
                        MemberNo = c.MemberNo.Trim(),
                        c.ShareCapitalAmount,
                        c.DepositsAmount
                    })
                    .ToListAsync<dynamic>();
            }

            var genderDict = members
                .GroupBy(m => m.MemberNo)
                .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

            decimal womenContributions = 0;
            decimal menContributions = 0;
            decimal othersContributions = 0;

            foreach (var c in contributions)
            {
                if (!genderDict.TryGetValue(c.MemberNo, out string gender)) continue;

                var normalizedGender = NormalizeGender(gender);
                var amount = (c.ShareCapitalAmount ?? 0) + (c.DepositsAmount ?? 0);

                if (amount <= 0) continue;

                if (normalizedGender == "FEMALE")
                    womenContributions += amount;
                else if (normalizedGender == "MALE")
                    menContributions += amount;
                else
                    othersContributions += amount;
            }

            var total = womenContributions + menContributions + othersContributions;

            _logger.LogInformation($"Contributions Summary - Total: {total:C}, Women: {womenContributions:C}, Men: {menContributions:C}, Others: {othersContributions:C}");
            _logger.LogInformation($"Company filter: {(targetCompanyCode ?? "ALL COMPANIES")}");

            return (total, womenContributions, menContributions, othersContributions);
        }

        // SHARE CAPITAL - From ContribShares table (ShareCapitalAmount)
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetShareCapitalDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var membersQuery = _context.Members.AsQueryable();

                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }

                var members = await membersQuery
                    .Select(m => new { MemberNo = m.MemberNo.Trim(), m.Sex })
                    .ToListAsync();

                if (!members.Any())
                {
                    _logger.LogInformation("No members found for share capital calculation");
                    return (0, 0, 0, 0);
                }

                string targetCompanyCode = null;
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    targetCompanyCode = companyCode;
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    targetCompanyCode = companyCode;
                }

                decimal totalShareCapital = 0;
                decimal womenShares = 0;
                decimal menShares = 0;
                decimal othersShares = 0;

                if (targetCompanyCode != null)
                {
                    var allShares = await _context.ContribShares
                        .Where(s => s.CompanyCode == targetCompanyCode
                            && s.ShareCapitalAmount.HasValue
                            && s.ShareCapitalAmount.Value > 0
                            && s.MemberNo != null)
                        .Select(s => new { MemberNo = s.MemberNo.Trim(), s.ShareCapitalAmount })
                        .ToListAsync();

                    var genderDict = members
                        .GroupBy(m => m.MemberNo)
                        .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

                    foreach (var share in allShares)
                    {
                        if (!genderDict.TryGetValue(share.MemberNo, out var gender)) continue;

                        var amount = share.ShareCapitalAmount ?? 0;
                        var normalizedGender = NormalizeGender(gender);

                        if (normalizedGender == "FEMALE")
                            womenShares += amount;
                        else if (normalizedGender == "MALE")
                            menShares += amount;
                        else
                            othersShares += amount;
                    }

                    totalShareCapital = womenShares + menShares + othersShares;
                }
                else
                {
                    var allShares = await _context.ContribShares
                        .Where(s => s.ShareCapitalAmount.HasValue
                            && s.ShareCapitalAmount.Value > 0
                            && s.MemberNo != null)
                        .Select(s => new { MemberNo = s.MemberNo.Trim(), s.ShareCapitalAmount })
                        .ToListAsync();

                    var allMembers = await _context.Members
                        .Select(m => new { MemberNo = m.MemberNo.Trim(), m.Sex })
                        .ToListAsync();

                    var genderDict = allMembers
                        .GroupBy(m => m.MemberNo)
                        .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

                    foreach (var share in allShares)
                    {
                        if (!genderDict.TryGetValue(share.MemberNo, out var gender)) continue;

                        var amount = share.ShareCapitalAmount ?? 0;
                        var normalizedGender = NormalizeGender(gender);

                        if (normalizedGender == "FEMALE")
                            womenShares += amount;
                        else if (normalizedGender == "MALE")
                            menShares += amount;
                        else
                            othersShares += amount;
                    }

                    totalShareCapital = womenShares + menShares + othersShares;
                }

                _logger.LogInformation($"Share Capital Summary - Total: {totalShareCapital:C}, Women: {womenShares:C}, Men: {menShares:C}, Others: {othersShares:C}");
                _logger.LogInformation($"Company filter: {(targetCompanyCode ?? "ALL COMPANIES")}");

                return (totalShareCapital, womenShares, menShares, othersShares);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating share capital from ContribShare");
                return (0, 0, 0, 0);
            }
        }

        // NON-WITHDRAWABLE DEPOSITS - From ContribShare table (DepositsAmount)
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetDepositsDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var membersQuery = _context.Members.AsQueryable();

                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }

                var members = await membersQuery
                    .Select(m => new { MemberNo = m.MemberNo.Trim(), m.Sex })
                    .ToListAsync();

                if (!members.Any())
                {
                    _logger.LogInformation("No members found for deposits calculation");
                    return (0, 0, 0, 0);
                }

                var memberNos = members.Select(m => m.MemberNo).ToList();

                string targetCompanyCode = null;
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    targetCompanyCode = companyCode;
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    targetCompanyCode = companyCode;
                }

                decimal totalDeposits = 0;
                decimal womenDeposits = 0;
                decimal menDeposits = 0;
                decimal othersDeposits = 0;

                if (targetCompanyCode != null)
                {
                    var allDeposits = await _context.ContribShares
                        .Where(d => d.CompanyCode == targetCompanyCode
                            && d.DepositsAmount.HasValue
                            && d.DepositsAmount.Value > 0
                            && d.MemberNo != null)
                        .Select(d => new { MemberNo = d.MemberNo.Trim(), d.DepositsAmount })
                        .ToListAsync();

                    var genderDict = members
                        .GroupBy(m => m.MemberNo)
                        .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

                    foreach (var deposit in allDeposits)
                    {
                        if (!genderDict.TryGetValue(deposit.MemberNo, out var gender)) continue;

                        var amount = deposit.DepositsAmount ?? 0;
                        var normalizedGender = NormalizeGender(gender);

                        if (normalizedGender == "FEMALE")
                            womenDeposits += amount;
                        else if (normalizedGender == "MALE")
                            menDeposits += amount;
                        else
                            othersDeposits += amount;
                    }

                    totalDeposits = womenDeposits + menDeposits + othersDeposits;
                }
                else
                {
                    var allDeposits = await _context.ContribShares
                        .Where(d => d.DepositsAmount.HasValue && d.DepositsAmount.Value > 0 && d.MemberNo != null)
                        .Select(d => new { MemberNo = d.MemberNo.Trim(), d.DepositsAmount })
                        .ToListAsync();

                    var allMembers = await _context.Members
                        .Select(m => new { MemberNo = m.MemberNo.Trim(), m.Sex })
                        .ToListAsync();

                    var genderDict = allMembers
                        .GroupBy(m => m.MemberNo)
                        .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

                    foreach (var deposit in allDeposits)
                    {
                        if (!genderDict.TryGetValue(deposit.MemberNo, out var gender)) continue;

                        var amount = deposit.DepositsAmount ?? 0;
                        var normalizedGender = NormalizeGender(gender);

                        if (normalizedGender == "FEMALE")
                            womenDeposits += amount;
                        else if (normalizedGender == "MALE")
                            menDeposits += amount;
                        else
                            othersDeposits += amount;
                    }

                    totalDeposits = womenDeposits + menDeposits + othersDeposits;
                }

                _logger.LogInformation($"Deposits Summary - Total: {totalDeposits:C}, Women: {womenDeposits:C}, Men: {menDeposits:C}, Others: {othersDeposits:C}");
                _logger.LogInformation($"Company filter: {(targetCompanyCode ?? "ALL COMPANIES")}");

                return (totalDeposits, womenDeposits, menDeposits, othersDeposits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating deposits from ContribShare");
                return (0, 0, 0, 0);
            }
        }

        // REGISTRATION FEES - From ContribShare table (RegFeeAmount)
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetRegistrationFeesDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                // Get all members with their gender and company
                var membersQuery = _context.Members.AsQueryable();

                // Apply company filter to members
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL members

                var members = await membersQuery
                    .Select(m => new { m.MemberNo, m.Sex, m.CompanyCode, m.EffectDate })
                    .ToListAsync();

                if (!members.Any())
                {
                    _logger.LogInformation("No members found for registration fees calculation");
                    return (0, 0, 0, 0);
                }

                // Get member numbers list
                var memberNos = members.Select(m => m.MemberNo).ToList();

                // OPTION 1: Get the FIRST registration fee per member (earliest record with RegFeeAmount > 0)
                var registrationFeesQuery = from cs in _context.ContribShares
                                            where memberNos.Contains(cs.MemberNo)
                                            && cs.RegFeeAmount.HasValue
                                            && cs.RegFeeAmount.Value > 0
                                            group cs by cs.MemberNo into g
                                            select new
                                            {
                                                MemberNo = g.Key,
                                                RegFeeAmount = g.Min(cs => cs.AuditTime), // Use earliest record date
                                                                                          // OR get the actual amount from the earliest record
                                                Amount = g.OrderBy(cs => cs.AuditTime)
                                                         .FirstOrDefault()
                                                         .RegFeeAmount ?? 0
                                            };

                // OPTION 2: Sum all RegFeeAmount for each member (if multiple registration fees possible)
                var registrationFeesSumQuery = from cs in _context.ContribShares
                                               where memberNos.Contains(cs.MemberNo)
                                               && cs.RegFeeAmount.HasValue
                                               && cs.RegFeeAmount.Value > 0
                                               group cs by cs.MemberNo into g
                                               select new
                                               {
                                                   MemberNo = g.Key,
                                                   TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                                               };

                // Choose one approach - I recommend Option 1 (earliest/first registration fee)
                var feesData = await registrationFeesQuery.ToListAsync();

                // Or use Option 2 if registration fees can be multiple
                // var feesData = await registrationFeesSumQuery.ToListAsync();

                // Create a dictionary for quick fee lookup
                var feeDict = feesData.ToDictionary(f => f.MemberNo, f => f.Amount);

                decimal womenFees = 0;
                decimal menFees = 0;
                decimal othersFees = 0;
                int membersWithFees = 0;
                int membersWithoutFees = 0;

                // Process each member
                foreach (var member in members)
                {
                    decimal regFee = 0;

                    if (feeDict.ContainsKey(member.MemberNo))
                    {
                        regFee = feeDict[member.MemberNo];
                        membersWithFees++;
                    }
                    else
                    {
                        membersWithoutFees++;
                        // Log missing fees for debugging
                        _logger.LogDebug($"Member {member.MemberNo} has no registration fee record in ContribShare");
                        continue;
                    }

                    var normalizedGender = NormalizeGender(member.Sex);

                    if (normalizedGender == "FEMALE")
                        womenFees += regFee;
                    else if (normalizedGender == "MALE")
                        menFees += regFee;
                    else
                        othersFees += regFee;
                }

                var total = womenFees + menFees + othersFees;

                _logger.LogInformation($"Registration Fees - Total: {total:C}, Women: {womenFees:C}, Men: {menFees:C}, Others: {othersFees:C}");
                _logger.LogInformation($"Members with fees: {membersWithFees}, Without fees: {membersWithoutFees}");

                return (total, womenFees, menFees, othersFees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating registration fees");
                return (0, 0, 0, 0);
            }
        }

        // Get total loans disbursed from Cheques table
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetLoansTakenDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                // Start with Cheques table, INNER JOIN to Loans (not LEFT JOIN)
                // This ensures we ONLY get cheques that have matching loans
                var query = from cheque in _context.Cheques
                            join loan in _context.Loans
                                on cheque.LoanNo equals loan.LoanNo  // INNER JOIN - critical change
                            join member in _context.Members
                                on loan.MemberNo equals member.MemberNo  // INNER JOIN - ensure valid member
                            where cheque.Amount.HasValue && cheque.Amount.Value > 0
                            select new
                            {
                                Amount = cheque.Amount.Value,
                                ChequeLoanNo = cheque.LoanNo ?? "",
                                LoanMemberNo = loan.MemberNo,
                                MemberSex = member.Sex,
                                CompanyCode = cheque.CompanyCode,
                                ChequeNo = cheque.ChequeNo,
                                Status = cheque.Status,
                                DateIssued = cheque.DateIssued
                            };

                // Apply company filter
                if (!string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(x => x.CompanyCode == companyCode);
                }

                // Execute query
                var data = await query.ToListAsync();

                // Debug logging
                _logger.LogInformation($"Found {data.Count} cheque records with valid loans and members");

                if (!data.Any())
                {
                    _logger.LogWarning("No cheque records found with matching loans and members");
                    return (0, 0, 0, 0);
                }

                decimal women = 0;
                decimal men = 0;
                decimal others = 0;

                foreach (var item in data)
                {
                    decimal amount = item.Amount;
                    string gender = NormalizeGender(item.MemberSex);

                    if (gender == "FEMALE")
                    {
                        women += amount;
                        _logger.LogDebug($"WOMEN: LoanNo: {item.ChequeLoanNo}, MemberNo: {item.LoanMemberNo}, Amount: {amount:C}");
                    }
                    else if (gender == "MALE")
                    {
                        men += amount;
                        _logger.LogDebug($"MEN: LoanNo: {item.ChequeLoanNo}, MemberNo: {item.LoanMemberNo}, Amount: {amount:C}");
                    }
                    else
                    {
                        others += amount;
                        _logger.LogDebug($"OTHERS: LoanNo: {item.ChequeLoanNo}, MemberNo: {item.LoanMemberNo}, Sex: '{item.MemberSex}', Amount: {amount:C}");
                    }
                }

                decimal total = women + men + others;
                int totalRecords = data.Count;
                int womenCount = data.Count(x => NormalizeGender(x.MemberSex) == "FEMALE");
                int menCount = data.Count(x => NormalizeGender(x.MemberSex) == "MALE");
                int othersCount = data.Count(x => NormalizeGender(x.MemberSex) == "OTHERS");

                // Detailed logging
                _logger.LogInformation($"=== LOANS TAKEN (DISBURSED) SUMMARY ===");
                _logger.LogInformation($"Total Amount: {total:C} from {totalRecords} cheques");
                _logger.LogInformation($"Women: {women:C} ({womenCount} cheques) - {(total > 0 ? (women / total * 100).ToString("F1") : "0")}%");
                _logger.LogInformation($"Men: {men:C} ({menCount} cheques) - {(total > 0 ? (men / total * 100).ToString("F1") : "0")}%");
                _logger.LogInformation($"Others: {others:C} ({othersCount} cheques) - {(total > 0 ? (others / total * 100).ToString("F1") : "0")}%");

                return (total, women, men, others);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating loans taken from Cheques table");
                return (0, 0, 0, 0);
            }
        }

        // LOAN BALANCES - From Loanbal table
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetLoanBalancesDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var query = from lb in _context.Loanbal
                        join m in _context.Members on lb.MemberNo equals m.MemberNo
                        select new
                        {
                            lb.Balance,
                            MemberNo = lb.MemberNo,
                            Sex = m.Sex,
                            CompanyCode = lb.Companycode
                        };

            // Apply company filter based on role
            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                query = query.Where(x => x.CompanyCode == companyCode);
            }
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
            {
                query = query.Where(x => x.CompanyCode == companyCode);
            }
            // If SuperAdmin and no companyCode, include ALL loan balances

            var loanBalances = await query.ToListAsync();

            decimal womenBalances = 0;
            decimal menBalances = 0;
            decimal othersBalances = 0;

            foreach (var lb in loanBalances)
            {
                var gender = NormalizeGender(lb.Sex);

                if (gender == "FEMALE")
                    womenBalances += lb.Balance;
                else if (gender == "MALE")
                    menBalances += lb.Balance;
                else
                    othersBalances += lb.Balance;
            }

            var total = womenBalances + menBalances + othersBalances;
            return (total, womenBalances, menBalances, othersBalances);
        }

        // LOANS PAID - From Repay table
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetLoansPaidDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var query = from r in _context.Repay
                            join m in _context.Members on r.MemberNo equals m.MemberNo
                            where r.Principal > 0 && r.Principal != null
                            select new
                            {
                                Principal = r.Principal ?? 0,
                                MemberNo = r.MemberNo,
                                Sex = m.Sex,
                                CompanyCode = m.CompanyCode
                            };

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(x => x.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(x => x.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL repayments

                var repayments = await query.ToListAsync();

                decimal womenPaid = 0;
                decimal menPaid = 0;
                decimal othersPaid = 0;

                foreach (var r in repayments)
                {
                    var gender = NormalizeGender(r.Sex);

                    if (gender == "FEMALE")
                        womenPaid += r.Principal;
                    else if (gender == "MALE")
                        menPaid += r.Principal;
                    else
                        othersPaid += r.Principal;
                }

                var total = womenPaid + menPaid + othersPaid;

                _logger.LogInformation($"Loans Paid - Total: {total}, Women: {womenPaid}, Men: {menPaid}");

                return (total, womenPaid, menPaid, othersPaid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loans paid data: {Message}", ex.Message);
                return (0, 0, 0, 0);
            }
        }

        // TOTAL LOANEES - Distinct members with loans
        private async Task<(int Total, int Women, int Men, int Others)> GetLoaneesDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var query = from l in _context.Loans
                            join m in _context.Members on l.MemberNo equals m.MemberNo
                            select new
                            {
                                l.MemberNo,
                                Sex = m.Sex,
                                CompanyCode = m.CompanyCode
                            };

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(x => x.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(x => x.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL loanees

                var borrowers = await query
                    .GroupBy(x => new { x.MemberNo, x.Sex })
                    .Select(g => new { g.Key.MemberNo, g.Key.Sex })
                    .ToListAsync();

                int womenLoanees = 0;
                int menLoanees = 0;
                int othersLoanees = 0;

                foreach (var b in borrowers)
                {
                    var gender = NormalizeGender(b.Sex);

                    if (gender == "FEMALE")
                        womenLoanees++;
                    else if (gender == "MALE")
                        menLoanees++;
                    else
                        othersLoanees++;
                }

                var total = womenLoanees + menLoanees + othersLoanees;

                return (total, womenLoanees, menLoanees, othersLoanees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loanees data: {Message}", ex.Message);
                return (0, 0, 0, 0);
            }
        }

        // Helper method to get grant totals with role-based filtering
        private async Task<decimal> GetGrantTotalAsync(string keyword, string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var query = _context.Journals
                    .Where(j =>
                        j.NARATION != null &&
                        j.TRANSTYPE == "CR" &&
                        j.NARATION.ToLower().Contains(keyword));

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(j => j.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(j => j.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL grants

                return await query.SumAsync(j => (decimal?)j.AMOUNT) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting grant total for {keyword}");
                return 0;
            }
        }

        #endregion

        // Calculate Repayment Rate (Current Month)
        private async Task<decimal> CalculateRepaymentRateAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                // Get active loans that should be making payments this month
                var activeLoansQuery = _context.Loans
                    .Where(l => l.ApplicDate <= DateTime.Now) // Loan was taken before or on today
                    .Where(l => l.LoanAmt > 0 && l.RepayPeriod > 0);

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    activeLoansQuery = activeLoansQuery.Where(l => l.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    activeLoansQuery = activeLoansQuery.Where(l => l.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL loans

                var activeLoans = await activeLoansQuery.ToListAsync();

                // Calculate expected payments for current month (only for loans that are still active)
                decimal expectedPayments = 0;
                foreach (var loan in activeLoans)
                {
                    // Calculate how many months the loan has been active
                    var monthsSinceStart = ((DateTime.Now.Year - loan.ApplicDate.Year) * 12) +
                                           (DateTime.Now.Month - loan.ApplicDate.Month);

                    var totalRepayPeriod = loan.RepayPeriod ?? 1;

                    // Only include if still within repayment period
                    if (monthsSinceStart < totalRepayPeriod)
                    {
                        var monthlyPayment = (loan.LoanAmt ?? 0) / totalRepayPeriod;
                        expectedPayments += monthlyPayment;
                    }
                }

                // Get actual payments for current month
                var repayQuery = from r in _context.Repay
                                 join l in _context.Loans on r.LoanNo equals l.LoanNo
                                 where r.DateReceived.HasValue &&
                                       r.DateReceived.Value.Month == currentMonth &&
                                       r.DateReceived.Value.Year == currentYear
                                 select new { r.Amount, l.CompanyCode };

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    repayQuery = repayQuery.Where(x => x.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    repayQuery = repayQuery.Where(x => x.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL repayments

                var actualPayments = await repayQuery.SumAsync(x => x.Amount ?? 0);

                // Repayment rate should never exceed 100%
                var rate = expectedPayments > 0 ? (actualPayments / expectedPayments) * 100 : 0;
                return Math.Min(rate, 100); // Cap at 100%
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating repayment rate");
                return 0;
            }
        }

        // Calculate PAR > 30 Days (Portfolio at Risk) using LoanBal and Aging Analysis logic
        private async Task<decimal> CalculatePARPercentAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;
                var asAtDateEnd = asAtDate.AddDays(1).AddSeconds(-1);

                // Get all active/disbursed loans with member and loan type data
                var loansQuery = from loan in _context.Loans
                                 join member in _context.Members
                                     on loan.MemberNo equals member.MemberNo
                                 join loantype in _context.Loantypes
                                     on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                     && loan.AuditTime <= asAtDateEnd
                                 select new
                                 {
                                     loan.MemberNo,
                                     loan.LoanNo,
                                     loan.LoanCode,
                                     loan.ApplicDate,
                                     loan.AuditTime,
                                     loan.RepayPeriod,
                                     loan.LoanAmt,
                                     loan.Aamount,
                                     loan.Interest,
                                     loan.CompanyCode,
                                     loan.Status,
                                     LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                     LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                     InterestRateFromType = lt != null ? lt.Interest : null
                                 };

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL loans

                var loans = await loansQuery.ToListAsync();

                if (!loans.Any())
                {
                    // Fallback: Get loans with positive balance from Loanbal
                    var loanbalQuery = from lb in _context.Loanbal
                                       join loan in _context.Loans on lb.LoanNo equals loan.LoanNo
                                       join member in _context.Members on loan.MemberNo equals member.MemberNo
                                       join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                       from lt in loanTypeJoin.DefaultIfEmpty()
                                       where lb.Balance > 0
                                       select new
                                       {
                                           loan.MemberNo,
                                           loan.LoanNo,
                                           loan.LoanCode,
                                           loan.ApplicDate,
                                           loan.AuditTime,
                                           loan.RepayPeriod,
                                           loan.LoanAmt,
                                           loan.Aamount,
                                           loan.Interest,
                                           loan.CompanyCode,
                                           loan.Status,
                                           LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                           LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                           InterestRateFromType = lt != null ? lt.Interest : null
                                       };

                    if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        loanbalQuery = loanbalQuery.Where(l => l.CompanyCode == companyCode);
                    }
                    else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        loanbalQuery = loanbalQuery.Where(l => l.CompanyCode == companyCode);
                    }

                    loans = await loanbalQuery.ToListAsync();
                }

                if (!loans.Any())
                {
                    return 0;
                }

                var loanNos = loans.Select(l => l.LoanNo).ToList();

                // Get loan balances from Loanbal table
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo))
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

                // Get the latest LastDate from Loanbal for each loan (this is the last payment/repayment date)
                var latestRepayments = await _context.Loanbal
                    .Where(r => loanNos.Contains(r.LoanNo) && r.LastDate <= asAtDateEnd)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastPaymentDate = g.Max(r => r.LastDate),
                        TotalBalance = g.Sum(r => r.Balance)
                    })
                    .ToDictionaryAsync(g => g.LoanNo, g => g);

                decimal totalOutstandingBalance = 0;
                decimal overdueAmount = 0;

                foreach (var loan in loans)
                {
                    // Get current balance from Loanbal
                    decimal currentBalance = 0;
                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        currentBalance = loanBalances[loan.LoanNo];
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Skip if loan is fully paid
                    if (currentBalance <= 0) continue;

                    totalOutstandingBalance += currentBalance;

                    // Get last payment date from Loanbal.LastDate
                    DateTime? lastPaymentDate = null;
                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                    }

                    // Calculate Days In Arrears (using the same logic as Aging Analysis)
                    int daysInArrears = 0;

                    if (lastPaymentDate.HasValue)
                    {
                        // Calculate next due date = last payment date + 1 month
                        var calculatedNextDueDate = lastPaymentDate.Value.AddMonths(1);

                        if (asAtDate > calculatedNextDueDate)
                        {
                            daysInArrears = (asAtDate - calculatedNextDueDate).Days;
                        }
                        else
                        {
                            daysInArrears = 0;
                        }
                    }
                    else
                    {
                        // No payments made yet - check if first payment is due
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                        else
                        {
                            daysInArrears = 0;
                        }
                    }

                    // Ensure days in arrears is not negative
                    if (daysInArrears < 0) daysInArrears = 0;

                    // PAR is calculated for loans with days in arrears > 30 days
                    // Using the ENTIRE outstanding balance, not just the overdue installment
                    if (daysInArrears > 30)
                    {
                        overdueAmount += currentBalance;
                    }
                }

                // Calculate PAR percentage
                decimal parPercentage = totalOutstandingBalance > 0
                    ? (overdueAmount / totalOutstandingBalance) * 100
                    : 0;

                return parPercentage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating PAR percentage using Loanbal");
                return 0;
            }
        }

        // Calculate Outstanding Loan Portfolio
        private async Task<decimal> CalculateOutstandingLoanPortfolioAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var loansQuery = _context.Loans.AsQueryable();

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL loans

                // Get total loan amount minus what has been repaid
                var loans = await loansQuery.ToListAsync();
                decimal totalOutstanding = 0;

                foreach (var loan in loans)
                {
                    var totalRepaid = await _context.Repay
                        .Where(r => r.LoanNo == loan.LoanNo)
                        .SumAsync(r => r.Amount ?? 0);

                    var outstanding = (loan.LoanAmt ?? 0) - totalRepaid;
                    if (outstanding > 0)
                    {
                        totalOutstanding += outstanding;
                    }
                }

                return totalOutstanding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating outstanding loan portfolio");
                return 0;
            }
        }

        // Calculate Arrears Balance (>30 Days) using LoanBal and Aging Analysis logic
        private async Task<decimal> CalculateArrearsBalanceAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;
                var asAtDateEnd = asAtDate.AddDays(1).AddSeconds(-1);

                // Get all active/disbursed loans with member and loan type data
                var loansQuery = from loan in _context.Loans
                                 join member in _context.Members
                                     on loan.MemberNo equals member.MemberNo
                                 join loantype in _context.Loantypes
                                     on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                     && loan.AuditTime <= asAtDateEnd
                                 select new
                                 {
                                     loan.MemberNo,
                                     loan.LoanNo,
                                     loan.LoanCode,
                                     loan.ApplicDate,
                                     loan.AuditTime,
                                     loan.RepayPeriod,
                                     loan.LoanAmt,
                                     loan.Aamount,
                                     loan.Interest,
                                     loan.CompanyCode,
                                     loan.Status,
                                     LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                     LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                     InterestRateFromType = lt != null ? lt.Interest : null
                                 };

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL loans

                var loans = await loansQuery.ToListAsync();

                if (!loans.Any())
                {
                    // Fallback: Get loans with positive balance from Loanbal
                    var loanbalQuery = from lb in _context.Loanbal
                                       join loan in _context.Loans on lb.LoanNo equals loan.LoanNo
                                       join member in _context.Members on loan.MemberNo equals member.MemberNo
                                       join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                       from lt in loanTypeJoin.DefaultIfEmpty()
                                       where lb.Balance > 0
                                       select new
                                       {
                                           loan.MemberNo,
                                           loan.LoanNo,
                                           loan.LoanCode,
                                           loan.ApplicDate,
                                           loan.AuditTime,
                                           loan.RepayPeriod,
                                           loan.LoanAmt,
                                           loan.Aamount,
                                           loan.Interest,
                                           loan.CompanyCode,
                                           loan.Status,
                                           LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                           LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                           InterestRateFromType = lt != null ? lt.Interest : null
                                       };

                    if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        loanbalQuery = loanbalQuery.Where(l => l.CompanyCode == companyCode);
                    }
                    else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        loanbalQuery = loanbalQuery.Where(l => l.CompanyCode == companyCode);
                    }

                    loans = await loanbalQuery.ToListAsync();
                }

                if (!loans.Any())
                {
                    return 0;
                }

                var loanNos = loans.Select(l => l.LoanNo).ToList();

                // Get loan balances from Loanbal table (current outstanding balance)
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo))
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

                // Get the latest LastDate from Loanbal for each loan
                var latestRepayments = await _context.Loanbal
                    .Where(r => loanNos.Contains(r.LoanNo) && r.LastDate <= asAtDateEnd)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastPaymentDate = g.Max(r => r.LastDate),
                        TotalBalance = g.Sum(r => r.Balance)
                    })
                    .ToDictionaryAsync(g => g.LoanNo, g => g);

                decimal arrearsBalance = 0;
                int arrearsCount = 0;

                foreach (var loan in loans)
                {
                    // Get current balance from Loanbal
                    decimal currentBalance = 0;
                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        currentBalance = loanBalances[loan.LoanNo];
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Skip if loan is fully paid
                    if (currentBalance <= 0) continue;

                    // Get last payment date from Loanbal.LastDate
                    DateTime? lastPaymentDate = null;
                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                    }

                    // Calculate Days In Arrears (using the same logic as Aging Analysis)
                    int daysInArrears = CalculateDaysInArrears(
                        asAtDate,
                        lastPaymentDate,
                        loan.AuditTime
                    );

                    // If days in arrears > 30, add the ENTIRE outstanding balance to arrears balance
                    if (daysInArrears > 30)
                    {
                        arrearsBalance += currentBalance;
                        arrearsCount++;
                    }
                }

                // Optional: Log the result for monitoring
                _logger.LogInformation($"Arrears Balance (>30 days): {arrearsBalance:C} for {arrearsCount} loans");

                return arrearsBalance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating arrears balance using Loanbal");
                return 0;
            }
        }

        // Helper method to calculate days in arrears (consistent with Aging Analysis)
        private int CalculateDaysInArrears(DateTime asAtDate, DateTime? lastPaymentDate, DateTime loanDisbursementDate)
        {
            if (lastPaymentDate.HasValue)
            {
                // Calculate next due date = last payment date + 1 month
                var calculatedNextDueDate = lastPaymentDate.Value.AddMonths(1);

                if (asAtDate > calculatedNextDueDate)
                {
                    return (asAtDate - calculatedNextDueDate).Days;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                // No payments made yet - check if first payment is due
                DateTime firstDueDate = loanDisbursementDate.AddMonths(1);
                if (asAtDate > firstDueDate)
                {
                    return (asAtDate - firstDueDate).Days;
                }
                else
                {
                    return 0;
                }
            }
        }

        // Calculate Women Participation Rate
        private async Task<decimal> CalculateWomenParticipationRateAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var membersQuery = _context.Members.AsQueryable();

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL members

                var totalMembers = await membersQuery.CountAsync();
                var womenMembers = await membersQuery.CountAsync(m => m.Sex == "FEMALE");

                return totalMembers > 0 ? (womenMembers * 100m / totalMembers) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating women participation rate");
                return 0;
            }
        }

        /// <summary>
        /// Calculates the Amount Past Due Rate - Penalty amount as percentage of overdue balance
        /// </summary>
        private async Task<decimal> CalculateAmountPastDueRateAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;
                var asAtDateEnd = asAtDate.AddDays(1).AddSeconds(-1);

                // Get all active/disbursed loans
                var loansQuery = from loan in _context.Loans
                                 join loantype in _context.Loantypes
                                     on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                     && loan.AuditTime <= asAtDateEnd
                                     && (loan.LoanAmt > 0 || loan.Aamount > 0)
                                 select new
                                 {
                                     loan.LoanNo,
                                     loan.LoanCode,
                                     loan.AuditTime,
                                     loan.LoanAmt,
                                     loan.Aamount,
                                     loan.CompanyCode,
                                     AttractsPenalty = lt != null && lt.Penalty == 1
                                 };

                // Apply company filter
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();

                if (!loans.Any())
                {
                    return 0;
                }

                var loanNos = loans.Select(l => l.LoanNo).ToList();
                var loanCodes = loans.Select(l => l.LoanCode).Distinct().ToList();

                // Get penalty configurations from Penalty table
                var penaltyConfigs = new Dictionary<string, Penalties>();
                if (loanCodes.Any())
                {
                    var penalties = await _context.Penalties
                        .Where(p => loanCodes.Contains(p.LoanCode) && p.Penalty == 1)
                        .ToListAsync();
                    penaltyConfigs = penalties.ToDictionary(p => p.LoanCode, p => p);
                }

                // Get loan balances
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo))
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

                // Get latest repayment dates
                var latestRepayments = await _context.Loanbal
                    .Where(r => loanNos.Contains(r.LoanNo) && r.LastDate <= asAtDateEnd)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastPaymentDate = g.Max(r => r.LastDate)
                    })
                    .ToDictionaryAsync(g => g.LoanNo, g => g);

                decimal totalPenaltyAmount = 0;
                decimal totalOverdueBalance = 0;

                foreach (var loan in loans)
                {
                    // Get current balance
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo)
                        ? loanBalances[loan.LoanNo]
                        : (loan.Aamount ?? loan.LoanAmt ?? 0);

                    if (currentBalance <= 0) continue;

                    // Get last payment date
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo)
                        ? latestRepayments[loan.LoanNo].LastPaymentDate
                        : null;

                    // Calculate days in arrears
                    int daysInArrears = CalculateDaysInArrearsForPenalty(asAtDate, lastPaymentDate, loan.AuditTime);

                    // Only consider loans with arrears > 30 days
                    if (daysInArrears > 30)
                    {
                        totalOverdueBalance += currentBalance;

                        // Calculate penalty amount if loan type attracts penalty
                        if (loan.AttractsPenalty && penaltyConfigs.ContainsKey(loan.LoanCode))
                        {
                            var penaltyConfig = penaltyConfigs[loan.LoanCode];
                            decimal penaltyAmount = CalculatePenaltyAmount(
                                currentBalance,
                                daysInArrears,
                                penaltyConfig.Value,
                                penaltyConfig.Mode,
                                penaltyConfig.Rate
                            );

                            totalPenaltyAmount += penaltyAmount;

                            _logger.LogDebug($"Loan {loan.LoanNo}: Balance={currentBalance:C}, Days={daysInArrears}, Penalty={penaltyAmount:C}");
                        }
                    }
                }

                // AMOUNT PAST DUE RATE = (Total Penalty Amount / Total Overdue Balance) × 100
                // This shows what percentage of the overdue balance is penalty
                decimal amountPastDueRate = totalOverdueBalance > 0
                    ? (totalPenaltyAmount / totalOverdueBalance) * 100
                    : 0;

                amountPastDueRate = Math.Round(amountPastDueRate, 1);

                _logger.LogInformation($"=== AMOUNT PAST DUE RATE ===");
                _logger.LogInformation($"Total Penalty Amount: {totalPenaltyAmount:C}");
                _logger.LogInformation($"Total Overdue Balance: {totalOverdueBalance:C}");
                _logger.LogInformation($"Amount Past Due Rate: {amountPastDueRate}%");

                return amountPastDueRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating amount past due rate");
                return 0;
            }
        }

        /// <summary>
        /// Calculate penalty amount for a specific loan
        /// </summary>
        private decimal CalculatePenaltyAmount(decimal balance, int daysInArrears, decimal penaltyValue, string penaltyMode, string penaltyRateType)
        {
            decimal penaltyAmount = 0;

            // Calculate number of penalty periods
            int numberOfPeriods = 1;
            switch (penaltyRateType?.ToLower())
            {
                case "daily":
                    numberOfPeriods = daysInArrears;
                    break;
                case "weekly":
                    numberOfPeriods = Math.Max(1, daysInArrears / 7);
                    break;
                case "monthly":
                    numberOfPeriods = Math.Max(1, daysInArrears / 30);
                    break;
                case "yearly":
                    numberOfPeriods = Math.Max(1, daysInArrears / 365);
                    break;
                default:
                    numberOfPeriods = 1;
                    break;
            }

            if (penaltyMode?.ToLower() == "percentage")
            {
                // Formula: Balance × (Penalty% / 100) × Number of Periods
                penaltyAmount = balance * (penaltyValue / 100) * numberOfPeriods;
            }
            else // Fixed amount mode
            {
                // Formula: Fixed Amount × Number of Periods
                penaltyAmount = penaltyValue * numberOfPeriods;

                // Cap penalty at 50% of balance (reasonable limit)
                decimal maxPenalty = balance * 0.5m;
                if (penaltyAmount > maxPenalty)
                {
                    penaltyAmount = maxPenalty;
                }
            }

            return Math.Round(Math.Max(0, penaltyAmount), 2);
        }

        /// <summary>
        /// Alternative: Calculate weighted average penalty rate directly from penalty configurations
        /// </summary>
        private async Task<decimal> CalculateWeightedPenaltyRateAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;
                var asAtDateEnd = asAtDate.AddDays(1).AddSeconds(-1);

                // Get all active/disbursed loans
                var loansQuery = from loan in _context.Loans
                                 join loantype in _context.Loantypes
                                     on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                     && loan.AuditTime <= asAtDateEnd
                                     && (loan.LoanAmt > 0 || loan.Aamount > 0)
                                 select new
                                 {
                                     loan.LoanNo,
                                     loan.LoanCode,
                                     loan.CompanyCode,
                                     AttractsPenalty = lt != null && lt.Penalty == 1
                                 };

                // Apply company filter
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();

                if (!loans.Any()) return 0;

                // Get penalty configurations
                var loanCodes = loans.Where(l => l.AttractsPenalty).Select(l => l.LoanCode).Distinct().ToList();

                var penaltyConfigs = await _context.Penalties
                    .Where(p => loanCodes.Contains(p.LoanCode) && p.Penalty == 1)
                    .ToDictionaryAsync(p => p.LoanCode, p => p);

                // Calculate weighted penalty rate
                decimal totalWeightedRate = 0;
                int configCount = 0;

                foreach (var config in penaltyConfigs.Values)
                {
                    decimal effectiveRate = 0;

                    if (config.Mode?.ToLower() == "percentage")
                    {
                        effectiveRate = config.Value;
                    }
                    else if (config.Mode?.ToLower() == "fixed")
                    {
                        // For fixed amount, use a default percentage (e.g., 5%)
                        effectiveRate = 5.0m;
                    }

                    totalWeightedRate += effectiveRate;
                    configCount++;
                }

                return configCount > 0 ? Math.Round(totalWeightedRate / configCount, 1) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating weighted penalty rate");
                return 5.9m;
            }
        }

        // Helper method for penalty calculation
        private int CalculateDaysInArrearsForPenalty(DateTime asAtDate, DateTime? lastPaymentDate, DateTime loanDisbursementDate)
        {
            if (lastPaymentDate.HasValue)
            {
                var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                if (asAtDate > nextDueDate)
                {
                    return (asAtDate - nextDueDate).Days;
                }
                return 0;
            }
            else
            {
                DateTime firstDueDate = loanDisbursementDate.AddMonths(1);
                if (asAtDate > firstDueDate)
                {
                    return (asAtDate - firstDueDate).Days;
                }
                return 0;
            }
        }

        // Get Loan Portfolio Health Status
        private string GetLoanPortfolioHealth(decimal parPercent)
        {
            if (parPercent < 5) return "Excellent";
            if (parPercent < 10) return "Good";
            if (parPercent < 20) return "Fair";
            return "At Risk";
        }

        private BlockchainDashboardData GetBlockchainData()
        {
            return new BlockchainDashboardData();
        }

        private List<WalletInfo> GetWalletData()
        {
            return new List<WalletInfo>();
        }

        private List<Models.Block> GetRecentBlocks(int count)
        {
            return new List<Models.Block>();
        }

        private List<BlockchainChain> GetBlockchainChains()
        {
            return new List<BlockchainChain>();
        }

        private async Task<HashSet<string>> GetActiveMemberNumbersAsync(DateTime cutoffDate, string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var activeFromTransactionsQuery = _context.Transactions2
                    .Where(t => t.Status == "COMPLETED" && t.ContributionDate >= cutoffDate)
                    .Select(t => t.MemberNo);

                var activeFromRepaysQuery = _context.Repay
                    .Where(r => r.DateReceived.HasValue && r.DateReceived.Value >= cutoffDate)
                    .Select(r => r.MemberNo);

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        activeFromTransactionsQuery = activeFromTransactionsQuery
                            .Where(t => membersInCompany.Contains(t));
                        activeFromRepaysQuery = activeFromRepaysQuery
                            .Where(r => membersInCompany.Contains(r));
                    }
                    else
                    {
                        return new HashSet<string>();
                    }
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        activeFromTransactionsQuery = activeFromTransactionsQuery
                            .Where(t => membersInCompany.Contains(t));
                        activeFromRepaysQuery = activeFromRepaysQuery
                            .Where(r => membersInCompany.Contains(r));
                    }
                    else
                    {
                        return new HashSet<string>();
                    }
                }
                // If SuperAdmin and no companyCode, include ALL members (no filtering)

                var activeTransactionMembers = await activeFromTransactionsQuery.Distinct().ToListAsync();
                var activeRepayMembers = await activeFromRepaysQuery.Distinct().ToListAsync();

                var activeSet = new HashSet<string>(activeTransactionMembers);
                activeSet.UnionWith(activeRepayMembers);

                return activeSet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active member numbers");
                return new HashSet<string>();
            }
        }

        private async Task<DashboardVM> GetUniversalDashboardDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var dashboard = new DashboardVM();

            try
            {
                var membersQuery = _context.Members.AsQueryable();

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                }
                // If SuperAdmin and no companyCode, include ALL members

                dashboard.TotalMembers = await membersQuery.CountAsync();
                dashboard.ActiveMembersByStatus = await membersQuery.CountAsync(m => m.Status == 1);

                // Blockchain stats are global (not filtered by company)
                dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions.CountAsync();
                dashboard.BlocksCreatedToday = await _context.Blocks
                    .Where(b => b.Timestamp.Date == DateTime.Today)
                    .CountAsync();
                dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions
                    .CountAsync(t => t.Status == "PENDING");

                var memberNo = User.FindFirst("MemberNo")?.Value;
                if (!string.IsNullOrEmpty(memberNo))
                {
                    var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
                    if (member != null)
                    {
                        dashboard.MemberShareBalance = await _context.Shares
                            .Where(s => s.MemberNo == memberNo)
                            .SumAsync(s => s.TotalShares ?? 0);

                        dashboard.MemberTotalLoans = await _context.Loans
                            .Where(l => l.MemberNo == memberNo && l.Status == 1)
                            .SumAsync(l => l.LoanAmt ?? 0);

                        dashboard.MemberRecentTransactionCount = await _context.Transactions2
                            .CountAsync(t => t.MemberNo == memberNo && t.ContributionDate.Date == DateTime.Today);
                    }
                }

                dashboard.RecentTransactions = await GetRecentTransactions(companyCode, isSuperAdmin);
                dashboard.QuickStats = await GetQuickStats(companyCode, isSuperAdmin);

                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting universal dashboard data");
                return dashboard;
            }
        }

        private async Task<List<RecentTransaction>> GetRecentTransactions(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var transactionsQuery = _context.Transactions2
                    .Where(t => t.Status == "COMPLETED")
                    .OrderByDescending(t => t.ContributionDate)
                    .Take(10);

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                    }
                    else
                    {
                        return new List<RecentTransaction>();
                    }
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                    }
                    else
                    {
                        return new List<RecentTransaction>();
                    }
                }
                // If SuperAdmin and no companyCode, include ALL transactions

                var recentTransactions = await transactionsQuery.ToListAsync();
                var result = new List<RecentTransaction>();

                foreach (var tx in recentTransactions)
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == tx.MemberNo);

                    result.Add(new RecentTransaction
                    {
                        TransactionId = tx.TransactionNo,
                        MemberName = member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown",
                        Type = tx.TransactionType,
                        Amount = tx.Amount,
                        Date = tx.ContributionDate,
                        Status = tx.Status,
                        BlockchainTxId = tx.BlockchainTxId ?? "Pending"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent transactions");
                return new List<RecentTransaction>();
            }
        }

        private async Task<DashboardQuickStats> GetQuickStats(string? companyCode, bool isSuperAdmin)
        {
            var today = DateTime.Today;
            var stats = new DashboardQuickStats();

            try
            {
                var transactionsQuery = _context.Transactions2
                    .Where(t => t.ContributionDate.Date == today && t.Status == "COMPLETED");

                var membersQuery = _context.Members.AsQueryable();
                var depositsQuery = _context.Transactions2
                    .Where(t => t.TransactionType == "DEPOSIT" && t.Status == "COMPLETED");
                var loansQuery = _context.Loans.Where(l => l.Status == 1);

                // Apply company filter based on role
                if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                        depositsQuery = depositsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        loansQuery = loansQuery.Where(l => membersInCompany.Contains(l.MemberNo));
                    }
                    else
                    {
                        return stats;
                    }
                }
                else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                        depositsQuery = depositsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        loansQuery = loansQuery.Where(l => membersInCompany.Contains(l.MemberNo));
                    }
                    else
                    {
                        return stats;
                    }
                }
                // If SuperAdmin and no companyCode, include ALL data

                stats.TransactionsToday = await transactionsQuery.CountAsync();
                stats.NewMembersToday = await membersQuery
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == today);
                stats.AverageDeposit = await depositsQuery.AverageAsync(t => t.Amount);
                stats.AverageLoan = await loansQuery.AverageAsync(l => l.LoanAmt ?? 0);
                stats.BlockchainUptime = 99.9m;

                var totalLoans = await loansQuery.CountAsync();
                var approvedLoans = await loansQuery.CountAsync(l => l.Status == 1);
                stats.LoanApprovalRate = totalLoans > 0 ? (approvedLoans * 100m / totalLoans) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick stats");
            }

            return stats;
        }
        private async Task<List<MonthlyTransactionData>> GetMonthlyTransactionsDataAsync(int months, string? companyCode, bool isSuperAdmin)
        {
            var data = new List<MonthlyTransactionData>();
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-months + 1);
            startDate = new DateTime(startDate.Year, startDate.Month, 1);

            try
            {
                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);
                    var monthName = monthDate.ToString("MMM yyyy");

                    var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                    var transactionsQuery = _context.Transactions2
                        .Where(t => t.Status == "COMPLETED" &&
                                   t.ContributionDate >= startOfMonth &&
                                   t.ContributionDate <= endOfMonth);

                    // Apply company filter based on role
                    if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        var memberNos = await _context.Members
                            .Where(m => m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();
                        if (memberNos.Any())
                        {
                            transactionsQuery = transactionsQuery.Where(t => memberNos.Contains(t.MemberNo));
                        }
                        else
                        {
                            transactionsQuery = transactionsQuery.Where(t => false);
                        }
                    }
                    else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        var memberNos = await _context.Members
                            .Where(m => m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();
                        if (memberNos.Any())
                        {
                            transactionsQuery = transactionsQuery.Where(t => memberNos.Contains(t.MemberNo));
                        }
                        else
                        {
                            transactionsQuery = transactionsQuery.Where(t => false);
                        }
                    }

                    var deposits = await transactionsQuery
                        .Where(t => t.TransactionType == "DEPOSIT" || t.TransactionType == "CONTRIBUTION")
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    var withdrawals = await transactionsQuery
                        .Where(t => t.TransactionType == "WITHDRAWAL")
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    // Get loan repayments from Repay table
                    decimal loanRepayments = 0;
                    if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        var memberNos = await _context.Members
                            .Where(m => m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        if (memberNos.Any())
                        {
                            loanRepayments = await _context.Repay
                                .Where(r => r.DateReceived.HasValue &&
                                           r.DateReceived.Value >= startOfMonth &&
                                           r.DateReceived.Value <= endOfMonth &&
                                           memberNos.Contains(r.MemberNo))
                                .SumAsync(r => (decimal?)r.Amount) ?? 0;
                        }
                    }
                    else
                    {
                        loanRepayments = await _context.Repay
                            .Where(r => r.DateReceived.HasValue &&
                                       r.DateReceived.Value >= startOfMonth &&
                                       r.DateReceived.Value <= endOfMonth)
                            .SumAsync(r => (decimal?)r.Amount) ?? 0;
                    }

                    data.Add(new MonthlyTransactionData
                    {
                        Month = monthName,
                        Deposits = deposits,
                        Withdrawals = withdrawals,
                        LoanRepayments = loanRepayments
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMonthlyTransactionsDataAsync");
            }

            return data;
        }

        private async Task<List<MemberGrowthData>> GetMemberGrowthDataAsync(int months, string? companyCode, bool isSuperAdmin)
        {
            var data = new List<MemberGrowthData>();
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-months);

            try
            {
                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);
                    var monthName = monthDate.ToString("MMM yyyy");

                    var membersQuery = _context.Members.AsQueryable();

                    // Apply company filter based on role
                    if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                    }
                    else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                    {
                        membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                    }
                    // If SuperAdmin and no companyCode, include ALL members

                    var newMembers = await membersQuery
                        .CountAsync(m => m.EffectDate.HasValue &&
                                        m.EffectDate.Value.Month == monthDate.Month &&
                                        m.EffectDate.Value.Year == monthDate.Year);

                    var totalMembers = await membersQuery
                        .CountAsync(m => m.EffectDate.HasValue &&
                                        m.EffectDate.Value <= monthDate);

                    data.Add(new MemberGrowthData
                    {
                        Period = monthName,
                        NewMembers = newMembers,
                        TotalMembers = totalMembers
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMemberGrowthDataAsync");
            }

            return data;
        }

        private int CalculateAgeSafe(DateTime birthDate)
        {
            try
            {
                var today = DateTime.Today;
                var age = today.Year - birthDate.Year;
                if (birthDate.Date > today.AddYears(-age)) age--;
                return age;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<RecentTransaction>> GetRecentTransactions(string? companyCode = null)
        {
            try
            {
                var transactionsQuery = _context.Transactions2
                    .Where(t => t.Status == "COMPLETED")
                    .OrderByDescending(t => t.ContributionDate)
                    .Take(10);

                if (!string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                    }
                    else
                    {
                        return new List<RecentTransaction>();
                    }
                }

                var recentTransactions = await transactionsQuery.ToListAsync();
                var result = new List<RecentTransaction>();

                foreach (var tx in recentTransactions)
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == tx.MemberNo);

                    result.Add(new RecentTransaction
                    {
                        TransactionId = tx.TransactionNo,
                        MemberName = member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown",
                        Type = tx.TransactionType,
                        Amount = tx.Amount,
                        Date = tx.ContributionDate,
                        Status = tx.Status,
                        BlockchainTxId = tx.BlockchainTxId ?? "Pending"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent transactions");
                return new List<RecentTransaction>();
            }
        }

        private async Task<DashboardQuickStats> GetQuickStats(string? companyCode = null)
        {
            var today = DateTime.Today;
            var stats = new DashboardQuickStats();

            try
            {
                var transactionsQuery = _context.Transactions2
                    .Where(t => t.ContributionDate.Date == today && t.Status == "COMPLETED");

                var membersQuery = _context.Members.AsQueryable();
                var depositsQuery = _context.Transactions2
                    .Where(t => t.TransactionType == "DEPOSIT" && t.Status == "COMPLETED");
                var loansQuery = _context.Loans.Where(l => l.Status == 1);

                if (!string.IsNullOrEmpty(companyCode))
                {
                    var membersInCompany = await _context.Members
                        .Where(m => m.CompanyCode == companyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (membersInCompany.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        membersQuery = membersQuery.Where(m => m.CompanyCode == companyCode);
                        depositsQuery = depositsQuery.Where(t => membersInCompany.Contains(t.MemberNo));
                        loansQuery = loansQuery.Where(l => membersInCompany.Contains(l.MemberNo));
                    }
                    else
                    {
                        return stats;
                    }
                }

                stats.TransactionsToday = await transactionsQuery.CountAsync();
                stats.NewMembersToday = await membersQuery
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == today);
                stats.AverageDeposit = await depositsQuery.AverageAsync(t => t.Amount);
                stats.AverageLoan = await loansQuery.AverageAsync(l => l.LoanAmt ?? 0);
                stats.BlockchainUptime = 99.9m;

                var totalLoans = await loansQuery.CountAsync();
                var approvedLoans = await loansQuery.CountAsync(l => l.Status == 1);
                stats.LoanApprovalRate = totalLoans > 0 ? (approvedLoans * 100m / totalLoans) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick stats");
            }

            return stats;
        }

        private string GetUserGroup()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")) return "Admin";
            if (User.IsInRole("Teller")) return "Teller";
            if (User.IsInRole("LoanOfficer")) return "Loan Officer";
            if (User.IsInRole("Auditor")) return "Auditor";
            if (User.IsInRole("BoardMember")) return "Board Member";

            var memberNo = User.FindFirst("MemberNo")?.Value;
            return !string.IsNullOrEmpty(memberNo) ? "Member" : "Guest";
        }

        // Updated GetChartData method with proper dynamic month handling
        [HttpGet]
        public async Task<IActionResult> GetChartData(string range = "6m", string? companyCode = null)
        {
            try
            {
                // Get user role for filtering
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                // Determine effective company code
                string effectiveCompanyCode = null;
                if (isSuperAdmin)
                {
                    effectiveCompanyCode = string.IsNullOrEmpty(companyCode) ? null : companyCode;
                }
                else
                {
                    effectiveCompanyCode = userCompanyCode;
                }

                // Determine number of months based on range parameter
                int months = range == "1y" ? 12 : 6;

                // Get dynamic monthly data (last N months from current date)
                var monthlyTransactions = await GetDynamicMonthlyTransactionsDataAsync(months, effectiveCompanyCode, isSuperAdmin);
                var memberGrowth = await GetDynamicMemberGrowthDataAsync(months, effectiveCompanyCode, isSuperAdmin);

                return Json(new
                {
                    success = true,
                    monthlyTransactions = monthlyTransactions.Select(m => new
                    {
                        month = m.Month,
                        deposits = m.Deposits,
                        withdrawals = m.Withdrawals,
                        loanRepayments = m.LoanRepayments
                    }),
                    memberGrowth = memberGrowth.Select(m => new
                    {
                        period = m.Period,
                        newMembers = m.NewMembers,
                        totalMembers = m.TotalMembers
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chart data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DYNAMIC METHOD - Gets last N months from current date (rolling window)
        private async Task<List<MonthlyTransactionData>> GetDynamicMonthlyTransactionsDataAsync(int months, string? companyCode, bool isSuperAdmin)
        {
            var data = new List<MonthlyTransactionData>();
            var endDate = DateTime.Now;

            // Start from X months ago, going back from current month
            // This creates a ROLLING WINDOW - always shows the most recent months
            var startDate = endDate.AddMonths(-months + 1);
            startDate = new DateTime(startDate.Year, startDate.Month, 1);

            try
            {
                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);

                    // Skip future months (should not happen, but just in case)
                    if (monthDate > endDate) continue;

                    var monthName = monthDate.ToString("MMM yyyy");
                    var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                    // Build base query
                    var transactionsQuery = _context.Transactions2
                        .Where(t => t.Status == "COMPLETED" &&
                                   t.ContributionDate >= startOfMonth &&
                                   t.ContributionDate <= endOfMonth);

                    // Apply company filter based on role
                    if (!string.IsNullOrEmpty(companyCode))
                    {
                        var memberNos = await _context.Members
                            .Where(m => m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        if (memberNos.Any())
                        {
                            transactionsQuery = transactionsQuery.Where(t => memberNos.Contains(t.MemberNo));
                        }
                        else
                        {
                            transactionsQuery = transactionsQuery.Where(t => false);
                        }
                    }

                    // Get deposits (including contributions)
                    var deposits = await transactionsQuery
                        .Where(t => t.TransactionType == "DEPOSIT" || t.TransactionType == "CONTRIBUTION")
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    // Get withdrawals
                    var withdrawals = await transactionsQuery
                        .Where(t => t.TransactionType == "WITHDRAWAL")
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    // Get loan repayments
                    decimal loanRepayments = 0;

                    if (!string.IsNullOrEmpty(companyCode))
                    {
                        var memberNos = await _context.Members
                            .Where(m => m.CompanyCode == companyCode)
                            .Select(m => m.MemberNo)
                            .ToListAsync();

                        if (memberNos.Any())
                        {
                            loanRepayments = await _context.Repay
                                .Where(r => r.DateReceived.HasValue &&
                                           r.DateReceived.Value >= startOfMonth &&
                                           r.DateReceived.Value <= endOfMonth &&
                                           memberNos.Contains(r.MemberNo))
                                .SumAsync(r => (decimal?)r.Amount) ?? 0;
                        }
                    }
                    else
                    {
                        loanRepayments = await _context.Repay
                            .Where(r => r.DateReceived.HasValue &&
                                       r.DateReceived.Value >= startOfMonth &&
                                       r.DateReceived.Value <= endOfMonth)
                            .SumAsync(r => (decimal?)r.Amount) ?? 0;
                    }

                    data.Add(new MonthlyTransactionData
                    {
                        Month = monthName,
                        MonthDate = monthDate, // Store actual date for sorting
                        Deposits = deposits,
                        Withdrawals = withdrawals,
                        LoanRepayments = loanRepayments
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDynamicMonthlyTransactionsDataAsync");
            }

            // Ensure data is ordered chronologically
            return data.OrderBy(d => d.MonthDate).ToList();
        }

        // DYNAMIC METHOD - Gets member growth for last N months (rolling window)
        private async Task<List<MemberGrowthData>> GetDynamicMemberGrowthDataAsync(int months, string? companyCode, bool isSuperAdmin)
        {
            var data = new List<MemberGrowthData>();
            var endDate = DateTime.Now;

            // Start from X months ago, going back from current month
            var startDate = endDate.AddMonths(-months + 1);
            startDate = new DateTime(startDate.Year, startDate.Month, 1);

            try
            {
                // Get cumulative total up to the start date (for running total calculation)
                decimal cumulativeTotalBeforeStart = 0;

                if (!string.IsNullOrEmpty(companyCode))
                {
                    cumulativeTotalBeforeStart = await _context.Members
                        .Where(m => m.CompanyCode == companyCode &&
                                   m.EffectDate.HasValue &&
                                   m.EffectDate.Value < startDate)
                        .CountAsync();
                }
                else
                {
                    cumulativeTotalBeforeStart = await _context.Members
                        .Where(m => m.EffectDate.HasValue && m.EffectDate.Value < startDate)
                        .CountAsync();
                }

                decimal runningTotal = cumulativeTotalBeforeStart;

                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);

                    // Skip future months
                    if (monthDate > endDate) continue;

                    var monthName = monthDate.ToString("MMM yyyy");
                    var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                    // Get new members for this month
                    int newMembers = 0;

                    if (!string.IsNullOrEmpty(companyCode))
                    {
                        newMembers = await _context.Members
                            .CountAsync(m => m.CompanyCode == companyCode &&
                                            m.EffectDate.HasValue &&
                                            m.EffectDate.Value >= startOfMonth &&
                                            m.EffectDate.Value <= endOfMonth);
                    }
                    else
                    {
                        newMembers = await _context.Members
                            .CountAsync(m => m.EffectDate.HasValue &&
                                            m.EffectDate.Value >= startOfMonth &&
                                            m.EffectDate.Value <= endOfMonth);
                    }

                    // Update running total
                    runningTotal += newMembers;

                    data.Add(new MemberGrowthData
                    {
                        Period = monthName,
                        PeriodDate = monthDate, // Store actual date for sorting
                        NewMembers = newMembers,
                        TotalMembers = (int)runningTotal
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDynamicMemberGrowthDataAsync");
            }

            // Ensure data is ordered chronologically
            return data.OrderBy(d => d.PeriodDate).ToList();
        }

        // Helper method to get current month's data for real-time updates
        [HttpGet]
        public async Task<IActionResult> GetCurrentMonthData(string? companyCode = null)
        {
            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                string effectiveCompanyCode = null;
                if (isSuperAdmin)
                {
                    effectiveCompanyCode = string.IsNullOrEmpty(companyCode) ? null : companyCode;
                }
                else
                {
                    effectiveCompanyCode = userCompanyCode;
                }

                var currentMonth = DateTime.Now;
                var startOfMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                // Get current month transactions
                var transactionsQuery = _context.Transactions2
                    .Where(t => t.Status == "COMPLETED" &&
                               t.ContributionDate >= startOfMonth &&
                               t.ContributionDate <= endOfMonth);

                if (!string.IsNullOrEmpty(effectiveCompanyCode))
                {
                    var memberNos = await _context.Members
                        .Where(m => m.CompanyCode == effectiveCompanyCode)
                        .Select(m => m.MemberNo)
                        .ToListAsync();

                    if (memberNos.Any())
                    {
                        transactionsQuery = transactionsQuery.Where(t => memberNos.Contains(t.MemberNo));
                    }
                }

                var deposits = await transactionsQuery
                    .Where(t => t.TransactionType == "DEPOSIT" || t.TransactionType == "CONTRIBUTION")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                var withdrawals = await transactionsQuery
                    .Where(t => t.TransactionType == "WITHDRAWAL")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                // Get new members this month
                int newMembers = 0;
                if (!string.IsNullOrEmpty(effectiveCompanyCode))
                {
                    newMembers = await _context.Members
                        .CountAsync(m => m.CompanyCode == effectiveCompanyCode &&
                                        m.EffectDate.HasValue &&
                                        m.EffectDate.Value >= startOfMonth &&
                                        m.EffectDate.Value <= endOfMonth);
                }
                else
                {
                    newMembers = await _context.Members
                        .CountAsync(m => m.EffectDate.HasValue &&
                                        m.EffectDate.Value >= startOfMonth &&
                                        m.EffectDate.Value <= endOfMonth);
                }

                return Json(new
                {
                    success = true,
                    currentMonth = currentMonth.ToString("MMM yyyy"),
                    deposits = deposits,
                    withdrawals = withdrawals,
                    newMembers = newMembers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current month data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats(string? companyCode = null)
        {
            try
            {
                // Get the logged-in user's role and company code from claims
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value ??
                                      User.FindFirst("SaccoCode")?.Value ??
                                      User.FindFirst("Company")?.Value;

                // Determine the effective company code for filtering
                string effectiveCompanyCode = null;

                if (isSuperAdmin)
                {
                    // Super Admin: Use selected company code if provided, otherwise null (show all)
                    effectiveCompanyCode = string.IsNullOrEmpty(companyCode) ? null : companyCode;
                }
                else
                {
                    // Non-SuperAdmin: Always limited to their own company
                    effectiveCompanyCode = userCompanyCode;
                }

                // Get dashboard data with role-based filtering
                var dashboard = await GetUniversalDashboardDataAsync(effectiveCompanyCode, isSuperAdmin);

                // Get additional financial data for the dashboard stats
                var contributionsData = await GetContributionsDataAsync(effectiveCompanyCode, isSuperAdmin);
                var shareCapitalData = await GetShareCapitalDataAsync(effectiveCompanyCode, isSuperAdmin);
                var loansTakenData = await GetLoansTakenDataAsync(effectiveCompanyCode, isSuperAdmin);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalMembers = dashboard.TotalMembers,
                        totalShareCapital = shareCapitalData.Total,
                        totalContributions = contributionsData.Total,
                        totalLoans = loansTakenData.Total,
                        blockchainTransactions = dashboard.TotalBlockchainTransactions,
                        activeMembers = dashboard.ActiveMembers,
                        totalWomen = dashboard.TotalWomen,
                        totalMen = dashboard.TotalMen,
                        youthTotal = dashboard.YouthTotal,
                        quickStats = dashboard.QuickStats,
                        // Include company filter info for UI
                        isSuperAdmin = isSuperAdmin,
                        selectedCompanyCode = effectiveCompanyCode ?? (isSuperAdmin ? "ALL" : userCompanyCode),
                        selectedCompanyName = isSuperAdmin && string.IsNullOrEmpty(effectiveCompanyCode) ? "All Companies" : dashboard.SelectedCompanyName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errorViewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };

            return View(errorViewModel);
        }
    }
}


