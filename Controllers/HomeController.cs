//using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
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
using MemberViewModel = SACCOBlockChainSystem.Models.MemberViewModel;

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
        private readonly IWebHostEnvironment _env;
        private WalletService _walletService;

        public HomeController(
            IDashboardService dashboardService,
            IBlockchainService blockchainService,
            ILogger<HomeController> logger,
            IWebHostEnvironment env,
            ApplicationDbContext context,WalletService walletService,
            IWebHostEnvironment webHostEnvironment, IDashboardCacheService dashboardCacheService)
        {
            _dashboardService = dashboardService;
            _blockchainService = blockchainService;
            _logger = logger;
            _env = env;
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

       
        private async Task<List<string>> GetCompanyCodesInSameCountyAsync(string companyCode)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

            if (company == null || string.IsNullOrEmpty(company.County))
                return new List<string> { companyCode };

            return await _context.Companies
                .Where(c => c.County == company.County)
                .Select(c => c.CompanyCode)
                .ToListAsync();
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
        [Authorize]
        public async Task<IActionResult> Index(string? companyCode)
        {
            try
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);
                var isSuperAdmin = userRole == "Super Admin" || userRole == "SuperAdmin";
                var isCountyAdmin = userRole == "County Admin" || userRole == "CountyAdmin";
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                // County Admin Logic
                if (isCountyAdmin && !string.IsNullOrEmpty(userCompanyCode))
                {
                    // Step 1: Get the user's company
                    var userCompany = await _context.Companies
                        .FirstOrDefaultAsync(c => c.CompanyCode == userCompanyCode);

                    if (userCompany != null && !string.IsNullOrEmpty(userCompany.County))
                    {
                        // Step 2: Get all companies in the same county
                        var countyCompanies = await _context.Companies
                            .Where(c => c.County != null && c.County == userCompany.County)
                            .Select(c => c.CompanyCode)
                            .ToListAsync();

                        // Step 3: Get dashboard data for ALL companies in this county
                        // FIXED: Use a different variable name (countyDashboard) to avoid conflict
                        var countyDashboard = await _dashboardCacheService.GetDashboardDataForCompaniesAsync(
                            countyCompanies,
                            isCountyAdmin);

                        // Step 4: Populate company list for the dropdown
                        countyDashboard.Companies = await _context.Companies
                            .Where(c => countyCompanies.Contains(c.CompanyCode))
                            .Select(c => new CompanyInfo { Code = c.CompanyCode, Name = c.CompanyName ?? c.CompanyCode })
                            .OrderBy(c => c.Name)
                            .ToListAsync();

                        countyDashboard.SelectedCompanyCode = "ALL";
                        countyDashboard.SelectedCompanyName = $"County: {userCompany.County} ({countyDashboard.Companies.Count} companies)";
                        countyDashboard.UserGroup = GetUserGroup();
                        countyDashboard.UserRoles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
                        countyDashboard.IsCountyView = true;
                        countyDashboard.CountyName = userCompany.County;

                        return View(countyDashboard);
                    }
                    else
                    {
                        // Fallback: If user's company has no county, just show their company
                        _logger.LogWarning($"County Admin {userCompanyCode} has no county assigned. Falling back to single company view.");
                    }
                }

                // Handle Member role - redirect to MemberIndex
                if (userRole?.ToUpper() == "MEMBER")
                {
                    var uid = User.FindFirst("UserId")?.Value;

                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.MemberId == int.Parse(uid) && w.CompanyCode == userCompanyCode);
                    var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == int.Parse(uid) && m.CompanyCode == userCompanyCode);

                    if (wallet == null)
                    {
                        if (member != null)
                            wallet = await _walletService.RegisterMemberAsync(member);
                    }

                    var wg = await _context.WalletConfigurations.AsNoTracking().FirstOrDefaultAsync(w => w.CompanyCode == wallet.CompanyCode);
                    if (wg != null && wg.EnableWallets == true)
                    {
                        if (wg.RequireTransactionPin == true)
                        {
                            if (string.IsNullOrEmpty(member.Pin))
                            {
                                return RedirectToAction("AccountSetup", "Home");
                            }
                        }
                    }

                    // ============================================================
                    // GET FINANCIAL SUMMARY DATA
                    // ============================================================

                    // 1. Get total contributions from Contrib table
                    var totalContributions = await _context.Contribs
                        .AsNoTracking()
                        .Where(c => c.MemberNo == member.MemberNo && c.CompanyCode == member.CompanyCode && c.Posted == "Y")
                        .SumAsync(c => c.Amount ?? 0);

                    // 2. Get total share capital from ContribShares table
                    var totalShareCapital = await _context.ContribShares
                        .AsNoTracking()
                        .Where(cs => cs.MemberNo == member.MemberNo && cs.CompanyCode == member.CompanyCode)
                        .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                    // 3. Get total deposits from ContribShares table
                    var totalDeposits = await _context.ContribShares
                        .AsNoTracking()
                        .Where(cs => cs.MemberNo == member.MemberNo && cs.CompanyCode == member.CompanyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    // ============================================================
                    // DETERMINE IF MEMBER IS ACTIVE (3 CONSECUTIVE MONTHS OF DEPOSITS)
                    // ============================================================
                    // Get member's deposit dates from ContribShares
                    var memberDepositDates = await _context.ContribShares
                        .Where(cs => cs.MemberNo == member.MemberNo
                                     && cs.CompanyCode == member.CompanyCode
                                     && cs.DepositsAmount.HasValue
                                     && cs.DepositsAmount.Value > 0)
                        .Select(cs => cs.ContrDate /*?? cs.DepositedDate ?? cs.ReceiptDate ?? cs.AuditTime*/)
                        .ToListAsync();

                    // Filter out null dates
                    var validDates = memberDepositDates
                        .Where(d => d.HasValue && d.Value != DateTime.MinValue)
                        .Select(d => d.Value)
                        .ToList();

                    bool isActive = IsMemberActive(validDates);

                    // 4. Get loan summary
                    var activeLoan = await _context.Loans
                        .AsNoTracking()
                        .FirstOrDefaultAsync(l => l.MemberNo == member.MemberNo && l.CompanyCode == member.CompanyCode && l.Status != (int)Status.Closed && l.Status != (int)Status.Rejected);

                    var loanBalance = 0m;
                    var loanAmount = 0m;
                    var loanRepaid = 0m;
                    var totalInterest = 0m;

                    if (activeLoan != null)
                    {
                        // Get loan balance from Loanbal table
                        var loanbal = await _context.Loanbal
                            .AsNoTracking()
                            .FirstOrDefaultAsync(lb => lb.LoanNo == activeLoan.LoanNo && lb.MemberNo == member.MemberNo);

                        if (loanbal != null)
                        {
                            loanBalance = loanbal.Balance;
                            totalInterest = loanbal.IntrOwed;
                        }

                        loanAmount = activeLoan.LoanAmt ?? 0;

                        // Calculate total repaid from Repay table
                        loanRepaid = await _context.Repay
                            .AsNoTracking()
                            .Where(r => r.LoanNo == activeLoan.LoanNo && r.MemberNo == member.MemberNo)
                            .SumAsync(r => r.Amount ?? 0);
                    }

                    // 5. Get all loans (active and closed)
                    var allLoans = await _context.Loans
                        .AsNoTracking()
                        .Where(l => l.MemberNo == member.MemberNo && l.CompanyCode == member.CompanyCode)
                        .ToListAsync();

                    var totalLoanAmount = allLoans.Sum(l => l.LoanAmt ?? 0);
                    var activeLoansCount = allLoans.Count(l => l.Status != (int)Status.Closed && l.Status != (int)Status.Rejected);
                    var completedLoansCount = allLoans.Count(l => l.Status == (int)Status.Closed);

                    // 6. Get recent transactions (same as before)
                    var blockchainTransactions = await _context.BlockchainTransactions
                        .AsNoTracking()
                        .Where(t => t.CompanyCode == member.CompanyCode && t.MemberNo == member.MemberNo)
                        .Select(t => new MemberTransactionViewModel
                        {
                            TransactionId = t.TransactionId,
                            TransactionType = t.TransactionType,
                            Amount = t.Amount,
                            Status = t.Status,
                            CreatedAt = t.Timestamp,
                            ReceiptNo = t.OffChainReferenceId,
                            Source = "Blockchain"
                        })
                        .ToListAsync();

                    var contributions = await _context.Contribs
                        .AsNoTracking()
                        .Where(c => c.MemberNo == member.MemberNo && c.CompanyCode == member.CompanyCode && c.Posted == "Y")
                        .OrderByDescending(c => c.AuditDateTime)
                        .Select(c => new MemberTransactionViewModel
                        {
                            TransactionId = c.TransactionNo,
                            TransactionType = "Contribution",
                            Amount = c.Amount ?? 0,
                            Status = "COMPLETED",
                            CreatedAt = c.AuditDateTime ?? DateTime.Now,
                            ReceiptNo = c.ReceiptNo,
                            Source = "Contrib"
                        })
                        .ToListAsync();

                    var shareContributions = await _context.ContribShares
                        .AsNoTracking()
                        .Where(cs => cs.MemberNo == member.MemberNo && cs.CompanyCode == member.CompanyCode)
                        .OrderByDescending(cs => cs.AuditDateTime)
                        .Select(cs => new MemberTransactionViewModel
                        {
                            TransactionId = cs.TransactionNo,
                            TransactionType = "Share Contribution",
                            Amount = (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0),
                            Status = "COMPLETED",
                            CreatedAt = cs.AuditDateTime ?? DateTime.Now,
                            ReceiptNo = cs.ReceiptNo,
                            Source = "ContribShares"
                        })
                        .ToListAsync();

                    var loanRepayments = await _context.Repay
                        .AsNoTracking()
                        .Where(r => r.MemberNo == member.MemberNo && r.CompanyCode == member.CompanyCode)
                        .OrderByDescending(r => r.AuditDateTime)
                        .Select(r => new MemberTransactionViewModel
                        {
                            TransactionId = r.TransactionNo,
                            TransactionType = "Loan Repayment",
                            Amount = r.Amount ?? 0,
                            Status = "COMPLETED",
                            CreatedAt = r.AuditDateTime ?? r.AuditTime ?? DateTime.Now,
                            ReceiptNo = r.ReceiptNo,
                            Source = "Repay"
                        })
                        .ToListAsync();

                    // Combine all transactions for recent list
                    var allTransactions = new List<MemberTransactionViewModel>();
                    allTransactions.AddRange(blockchainTransactions);
                    allTransactions.AddRange(contributions);
                    allTransactions.AddRange(shareContributions);
                    allTransactions.AddRange(loanRepayments);

                    var latestTransactions = allTransactions
                        .OrderByDescending(t => t.CreatedAt)
                        .Take(5)
                        .ToList();

                    var memberView = new MemberViewModel
                    {
                        Member = member,
                        Wallets = new List<Wallet> { wallet },
                        MemberTransactions = latestTransactions,
                        UserCompanyCode = member.CompanyCode,
                        TotalBlockchainTransactions = blockchainTransactions.Count + contributions.Count + shareContributions.Count + loanRepayments.Count,

                        // Add financial summary properties
                        TotalContributions = totalContributions,
                        TotalShareCapital = totalShareCapital,
                        TotalDeposits = totalDeposits,
                        CurrentLoanBalance = loanBalance,
                        CurrentLoanAmount = loanAmount,
                        TotalLoanRepaid = loanRepaid,
                        TotalLoanInterest = totalInterest,
                        TotalAllLoans = totalLoanAmount,
                        ActiveLoansCount = activeLoansCount,
                        CompletedLoansCount = completedLoansCount,
                        HasActiveLoan = activeLoan != null
                    };

                    return View("MemberIndex", memberView);
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

                if (isSuperAdmin)
                {
                    dashboard.Companies = await _context.Companies
                        .Where(c => c.Project == true)
                        .Select(c => new CompanyInfo { Code = c.CompanyCode, Name = c.CompanyName ?? c.CompanyCode })
                        .OrderBy(c => c.Name)
                        .ToListAsync();

                    // Add company count to ViewBag for layout
                    ViewBag.CompanyCount = dashboard.Companies.Count;
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

        /// <summary>
        /// Determines if a member is active based on having 3 consecutive months of deposits
        /// Once active, always active (even if later deposits stop)
        /// </summary>
        private bool IsMemberActive(List<DateTime> depositDates)
        {
            if (depositDates == null || depositDates.Count < 3)
                return false;

            // Sort dates ascending
            var sortedDates = depositDates.OrderBy(d => d).ToList();

            // Group by year-month to check consecutive months
            var monthGroups = sortedDates
                .Select(d => new { Year = d.Year, Month = d.Month })
                .Distinct()
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            // Check for 3 consecutive months
            int consecutiveCount = 1;
            for (int i = 1; i < monthGroups.Count; i++)
            {
                var current = monthGroups[i];
                var previous = monthGroups[i - 1];

                // Check if months are consecutive
                bool isConsecutive = false;

                // Same year, next month
                if (current.Year == previous.Year && current.Month == previous.Month + 1)
                    isConsecutive = true;
                // Year boundary (Dec to Jan)
                else if (current.Year == previous.Year + 1 && current.Month == 1 && previous.Month == 12)
                    isConsecutive = true;

                if (isConsecutive)
                {
                    consecutiveCount++;
                    if (consecutiveCount >= 3)
                        return true; // Once active, always active!
                }
                else
                {
                    consecutiveCount = 1; // Reset if not consecutive
                }
            }

            return false;
        }


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

        // SHARE CAPITAL - From ContribShares table filtered by Sharetype where Issharecapital = 1 (true)
        private async Task<(decimal Total, decimal Women, decimal Men, decimal Others)> GetShareCapitalDataAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
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

                // ============================================================
                // FIXED: Join with Sharetype and filter by Issharecapital = 1
                // This ensures ONLY true share capital is included
                // ============================================================
                var shareCapitalQuery = from cs in _context.ContribShares
                                        join st in _context.Sharetypes
                                            on new { cs.Sharescode, cs.CompanyCode }
                                            equals new { Sharescode = st.SharesCode, st.CompanyCode }
                                        where cs.ShareCapitalAmount.HasValue
                                            && cs.ShareCapitalAmount.Value > 0
                                            && st.Issharecapital  // ✅ ONLY TRUE SHARE CAPITAL
                                        select new
                                        {
                                            cs.MemberNo,
                                            cs.ShareCapitalAmount,
                                            cs.CompanyCode,
                                            Sex = st.SharesType // Not used for gender, just for debugging
                                        };

                if (targetCompanyCode != null)
                {
                    shareCapitalQuery = shareCapitalQuery.Where(x => x.CompanyCode == targetCompanyCode);
                }

                var shareCapitalData = await shareCapitalQuery.ToListAsync();

                // Build gender lookup
                var genderDict = members
                    .GroupBy(m => m.MemberNo)
                    .ToDictionary(g => g.Key, g => g.First().Sex, StringComparer.OrdinalIgnoreCase);

                foreach (var item in shareCapitalData)
                {
                    if (!genderDict.TryGetValue(item.MemberNo, out var gender)) continue;

                    var amount = item.ShareCapitalAmount ?? 0;
                    var normalizedGender = NormalizeGender(gender);

                    if (normalizedGender == "FEMALE")
                        womenShares += amount;
                    else if (normalizedGender == "MALE")
                        menShares += amount;
                    else
                        othersShares += amount;
                }

                totalShareCapital = womenShares + menShares + othersShares;

                _logger.LogInformation($"Share Capital Summary (filtered by Issharecapital=1) - Total: {totalShareCapital:C}, Women: {womenShares:C}, Men: {menShares:C}, Others: {othersShares:C}");
                _logger.LogInformation($"Company filter: {(targetCompanyCode ?? "ALL COMPANIES")}");

                return (totalShareCapital, womenShares, menShares, othersShares);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating share capital from ContribShare with Issharecapital filter");
                return (0, 0, 0, 0);
            }
        }

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
                var query = _context.Gltransactions
                    .Where(j =>
                        j.TransDescript != null &&
                        j.TransDescript.ToLower().Contains(keyword.ToLower())); // ✅ Check for keyword in description

                // Apply company filter based on role
                if (!string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(j => j.CompanyCode == companyCode);
                }

                return await query.SumAsync(j => (decimal?)j.Amount) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting grant total for {keyword}");
                return 0;
            }
        }

        #endregion

        // Calculate Repayment Rate (Current Month) - Standard MFI Formula
        // Repayment Rate = Amount Received (this month) / (Amount Due this month + Arrears from previous months)
        private async Task<decimal> CalculateRepaymentRateAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var currentDate = DateTime.Now;
                var startOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var endOfCurrentMonth = startOfCurrentMonth.AddMonths(1).AddDays(-1);

                // Get all active loans with balances
                var activeLoansQuery = from lb in _context.Loanbal
                                       join l in _context.Loans on lb.LoanNo equals l.LoanNo
                                       where lb.Balance > 0
                                       select new { lb.LoanNo, lb.Balance, l.CompanyCode, l.RepayPeriod, l.LoanAmt };

                if (!string.IsNullOrEmpty(companyCode))
                {
                    activeLoansQuery = activeLoansQuery.Where(x => x.CompanyCode == companyCode);
                }

                var activeLoans = await activeLoansQuery.ToListAsync();

                if (!activeLoans.Any())
                {
                    return 0;
                }

                var loanNos = activeLoans.Select(l => l.LoanNo).ToList();

                // Calculate Total Amount Due (Outstanding Balance)
                decimal totalAmountDue = activeLoans.Sum(l => l.Balance);

                // Calculate Amount Received this month
                var repaymentsQuery = from r in _context.Repay
                                      where loanNos.Contains(r.LoanNo)
                                            && r.DateReceived.HasValue
                                            && r.DateReceived >= startOfCurrentMonth
                                            && r.DateReceived <= endOfCurrentMonth
                                      select r.Amount;

                decimal totalAmountReceived = await repaymentsQuery.SumAsync(r => r ?? 0);

                // Repayment Rate = Amount Received this month / Total Outstanding Balance
                decimal repaymentRate = totalAmountDue > 0
                    ? (totalAmountReceived / totalAmountDue) * 100
                    : 0;

                repaymentRate = Math.Min(repaymentRate, 100);

                _logger.LogInformation($"Repayment Rate - Received: {totalAmountReceived:C}, Outstanding: {totalAmountDue:C}, Rate: {repaymentRate:F1}%");

                return repaymentRate;
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

        // Calculate PAR > 60 Days (Portfolio at Risk) using LoanBal and Aging Analysis logic
        private async Task<decimal> CalculatePAR60PercentAsync(string? companyCode, bool isSuperAdmin)
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

                // Apply company filter
                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }

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

                    if (!string.IsNullOrEmpty(companyCode))
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

                // Get loan balances from Loanbal table (ONE QUERY)
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo))
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

                // Get the latest LastDate from Loanbal for each loan (ONE QUERY)
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
                decimal overdueAmount60Days = 0;  // For PAR > 60 days

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

                    // Calculate Days In Arrears
                    int daysInArrears = 0;

                    if (lastPaymentDate.HasValue)
                    {
                        // Calculate next due date = last payment date + 1 month
                        var calculatedNextDueDate = lastPaymentDate.Value.AddMonths(1);

                        if (asAtDate > calculatedNextDueDate)
                        {
                            daysInArrears = (asAtDate - calculatedNextDueDate).Days;
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
                    }

                    // Ensure days in arrears is not negative
                    if (daysInArrears < 0) daysInArrears = 0;

                    // PAR > 60 DAYS - Only add loans with days in arrears > 60
                    if (daysInArrears > 60)  // CHANGED FROM 30 TO 60
                    {
                        overdueAmount60Days += currentBalance;
                    }
                }

                // Calculate PAR percentage for >60 days
                decimal parPercentage60 = totalOutstandingBalance > 0
                    ? (overdueAmount60Days / totalOutstandingBalance) * 100
                    : 0;

                _logger.LogInformation($"=== PAR > 60 DAYS CALCULATION ===");
                _logger.LogInformation($"Total Outstanding Balance: {totalOutstandingBalance:C}");
                _logger.LogInformation($"Overdue >60 Days Balance: {overdueAmount60Days:C}");
                _logger.LogInformation($"PAR >60 Days: {parPercentage60:F1}%");

                return parPercentage60;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating PAR >60 days percentage");
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
        /// Calculates the Amount Past Due Rate - (Total Arrears > 30 Days / Outstanding Loan Portfolio) × 100
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

                // Get loan balances (Outstanding Loan Portfolio)
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

                decimal totalOutstandingPortfolio = 0;
                decimal totalArrearsBalance = 0;

                foreach (var loan in loans)
                {
                    // Get current balance (Outstanding Loan Portfolio)
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo)
                        ? loanBalances[loan.LoanNo]
                        : (loan.Aamount ?? loan.LoanAmt ?? 0);

                    if (currentBalance <= 0) continue;

                    // Add to total outstanding portfolio
                    totalOutstandingPortfolio += currentBalance;

                    // Get last payment date
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo)
                        ? latestRepayments[loan.LoanNo].LastPaymentDate
                        : null;

                    // Calculate days in arrears
                    int daysInArrears = CalculateDaysInArrearsForPenalty(asAtDate, lastPaymentDate, loan.AuditTime);

                    // If days in arrears > 30, add to total arrears balance
                    if (daysInArrears > 30)
                    {
                        totalArrearsBalance += currentBalance;
                    }
                }

                // AMOUNT PAST DUE RATE = (Total Arrears > 30 Days / Outstanding Loan Portfolio) × 100
                decimal amountPastDueRate = totalOutstandingPortfolio > 0
                    ? (totalArrearsBalance / totalOutstandingPortfolio) * 100
                    : 0;

                amountPastDueRate = Math.Round(amountPastDueRate, 1);

                _logger.LogInformation($"=== AMOUNT PAST DUE RATE ===");
                _logger.LogInformation($"Total Arrears (>30 days): {totalArrearsBalance:C}");
                _logger.LogInformation($"Outstanding Loan Portfolio: {totalOutstandingPortfolio:C}");
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
        /// Calculate penalty amount for a specific loan (kept for reference, not used in AmountPastDueRate)
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

                // ============================================================
                // FIXED: AverageDeposit - handles empty sequence with DefaultIfEmpty
                // ============================================================
                var avgDeposit = await depositsQuery
                    .Select(t => (decimal?)t.Amount)
                    .DefaultIfEmpty()
                    .AverageAsync();
                stats.AverageDeposit = avgDeposit ?? 0;

                // ============================================================
                // FIXED: AverageLoan - handles empty sequence with DefaultIfEmpty
                // ============================================================
                var avgLoan = await loansQuery
                    .Select(l => (decimal?)l.LoanAmt)
                    .DefaultIfEmpty()
                    .AverageAsync();
                stats.AverageLoan = avgLoan ?? 0;

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
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ShowErrorDetails = _env.IsDevelopment() // Use injected environment
            };

            // Get the last exception from the current context
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (exception != null)
            {
                errorViewModel.ErrorMessage = exception.Error.Message;
                errorViewModel.StackTrace = exception.Error.StackTrace;
                errorViewModel.ExceptionType = exception.Error.GetType().FullName;

                // Try to get HTTP status code if available
                var statusCode = HttpContext.Response.StatusCode;
                errorViewModel.StatusCode = statusCode;
            }

            return View(errorViewModel);
        }
    }

    internal record NewRecord(string LoanNo, string MemberNo, decimal? LoanAmt, decimal? Item, int? RepayPeriod, DateTime ApplicDate, DateTime AuditTime, string CompanyCode, int? LoanTypeRepayPeriod);
}


