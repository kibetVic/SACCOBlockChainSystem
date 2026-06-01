// Services/DashboardCacheService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface IDashboardCacheService
    {
        Task<DashboardVM> GetDashboardDataAsync(string? companyCode, bool isSuperAdmin);
        Task InvalidateCacheAsync(string? companyCode = null);
    }

    public class DashboardCacheService : IDashboardCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardCacheService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // Cache duration - 2 minutes for good balance between speed and freshness
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(2);

        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public DashboardCacheService(
            IMemoryCache cache,
            ApplicationDbContext context,
            ILogger<DashboardCacheService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<DashboardVM> GetDashboardDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var cacheKey = $"DashboardData_{companyCode ?? "ALL"}_{isSuperAdmin}";

            if (_cache.TryGetValue(cacheKey, out DashboardVM cachedDashboard))
            {
                _logger.LogDebug($"Dashboard data from cache for: {companyCode ?? "ALL"}");
                return cachedDashboard;
            }

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check cache after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedDashboard))
                    return cachedDashboard;

                _logger.LogInformation($"Calculating dashboard for: {companyCode ?? "ALL"}");

                var dashboard = await CalculateFullDashboardDataAsync(companyCode, isSuperAdmin);

                _cache.Set(cacheKey, dashboard, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration,
                    Priority = CacheItemPriority.High
                });

                return dashboard;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<DashboardVM> CalculateFullDashboardDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var dashboard = new DashboardVM();

            // Build the member filter
            var memberQuery = _context.Members.AsQueryable();
            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                memberQuery = memberQuery.Where(m => m.CompanyCode == companyCode);
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                memberQuery = memberQuery.Where(m => m.CompanyCode == companyCode);

            // ============================================================
            // QUERY #1: Member statistics
            // ============================================================
            var memberStats = await memberQuery
                .Select(m => new { m.Sex, m.Status, m.MemberNo, m.Dob, m.EffectDate })
                .ToListAsync();

            dashboard.TotalMembers = memberStats.Count;
            dashboard.TotalWomen = memberStats.Count(m => m.Sex?.ToUpper() == "FEMALE");
            dashboard.TotalMen = memberStats.Count(m => m.Sex?.ToUpper() == "MALE");
            dashboard.TotalOthers = memberStats.Count(m => m.Sex?.ToUpper() != "FEMALE" && m.Sex?.ToUpper() != "MALE");
            dashboard.ActiveMembers = memberStats.Count(m => m.Status == 1);
            dashboard.ActiveMembersByStatus = dashboard.ActiveMembers;
            dashboard.ActiveWomen = memberStats.Count(m => m.Sex?.ToUpper() == "FEMALE" && m.Status == 1);
            dashboard.ActiveMen = memberStats.Count(m => m.Sex?.ToUpper() == "MALE" && m.Status == 1);
            dashboard.DormantMembers = dashboard.TotalMembers - dashboard.ActiveMembers;
            dashboard.DormantWomen = dashboard.TotalWomen - dashboard.ActiveWomen;
            dashboard.DormantMen = dashboard.TotalMen - dashboard.ActiveMen;

            // Youth statistics
            var today = DateTime.Today;
            var membersWithAge = memberStats.Where(m => m.Dob.HasValue).ToList();
            dashboard.YouthTotal = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35);
            dashboard.YouthMale = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35 && m.Sex?.ToUpper() == "MALE");
            dashboard.YouthFemale = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35 && m.Sex?.ToUpper() == "FEMALE");

            // Get member numbers list for filtering
            var memberNos = memberStats.Select(m => m.MemberNo).ToList();

            // ============================================================
            // QUERY #2: Financial aggregates from ContribShares
            // ============================================================
            var contribSharesQuery = _context.ContribShares.AsQueryable();
            if (memberNos.Any())
                contribSharesQuery = contribSharesQuery.Where(cs => memberNos.Contains(cs.MemberNo));

            var financialStats = await contribSharesQuery
                .GroupBy(cs => 1)
                .Select(g => new
                {
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalRegFees = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    TotalContributions = g.Sum(cs => (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0))
                })
                .FirstOrDefaultAsync();

            if (financialStats != null)
            {
                dashboard.TotalShareCapital = financialStats.TotalShareCapital;
                dashboard.TotalDeposits = financialStats.TotalDeposits;
                dashboard.TotalRegistrationFees = financialStats.TotalRegFees;
                dashboard.TotalContributions = financialStats.TotalContributions;
            }

            // ============================================================
            // QUERY #3: Gender breakdown for financials
            // ============================================================
            var genderFinancials = await (from cs in _context.ContribShares
                                          join m in _context.Members on cs.MemberNo equals m.MemberNo
                                          where memberNos.Contains(m.MemberNo)
                                          group cs by m.Sex into g
                                          select new
                                          {
                                              Gender = g.Key ?? "OTHERS",
                                              ShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                                              Deposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                                              RegFees = g.Sum(cs => cs.RegFeeAmount ?? 0),
                                              Contributions = g.Sum(cs => (cs.ShareCapitalAmount ?? 0) + (cs.DepositsAmount ?? 0))
                                          }).ToListAsync();

            foreach (var item in genderFinancials)
            {
                var gender = NormalizeGender(item.Gender);
                if (gender == "FEMALE")
                {
                    dashboard.WomenShareCapital = item.ShareCapital;
                    dashboard.WomenDeposits = item.Deposits;
                    dashboard.WomenRegistrationFees = item.RegFees;
                    dashboard.WomenContributions = item.Contributions;
                }
                else if (gender == "MALE")
                {
                    dashboard.MenShareCapital = item.ShareCapital;
                    dashboard.MenDeposits = item.Deposits;
                    dashboard.MenRegistrationFees = item.RegFees;
                    dashboard.MenContributions = item.Contributions;
                }
                else
                {
                    dashboard.OthersShareCapital = item.ShareCapital;
                    dashboard.OthersDeposits = item.Deposits;
                    dashboard.OthersRegistrationFees = item.RegFees;
                    dashboard.OthersContributions = item.Contributions;
                }
            }

            // ============================================================
            // QUERY #4: Loan stats from Cheques table (Loans Taken)
            // ============================================================
            var loanStatsQuery = from c in _context.Cheques
                                 join l in _context.Loans on c.LoanNo equals l.LoanNo
                                 join m in _context.Members on l.MemberNo equals m.MemberNo
                                 where c.Amount > 0 && c.Amount != null && memberNos.Contains(m.MemberNo)
                                 select new { Amount = c.Amount.Value, Sex = m.Sex };

            var loanStats = await loanStatsQuery.ToListAsync();

            dashboard.TotalLoansTaken = loanStats.Sum(x => x.Amount);
            dashboard.WomenLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Amount);
            dashboard.MenLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Amount);
            dashboard.OthersLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Amount);

            // ============================================================
            // QUERY #5: Loan balances from Loanbal table
            // ============================================================
            var loanBalancesQuery = from lb in _context.Loanbal
                                    join m in _context.Members on lb.MemberNo equals m.MemberNo
                                    where memberNos.Contains(m.MemberNo)
                                    select new { lb.Balance, m.Sex };

            var loanBalances = await loanBalancesQuery.ToListAsync();

            dashboard.TotalLoanBalances = loanBalances.Sum(x => x.Balance);
            dashboard.WomenLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Balance);
            dashboard.MenLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Balance);
            dashboard.OthersLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Balance);

            // ============================================================
            // QUERY #6: Loans paid from Repay table
            // ============================================================
            var loansPaidQuery = from r in _context.Repay
                                 join m in _context.Members on r.MemberNo equals m.MemberNo
                                 where r.Principal > 0 && r.Principal != null && memberNos.Contains(m.MemberNo)
                                 select new { Principal = r.Principal.Value, Sex = m.Sex };

            var loansPaid = await loansPaidQuery.ToListAsync();

            dashboard.TotalLoansPaid = loansPaid.Sum(x => x.Principal);
            dashboard.WomenLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Principal);
            dashboard.MenLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Principal);
            dashboard.OthersLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Principal);

            // ============================================================
            // QUERY #7: Total loanees (distinct members with loans)
            // ============================================================
            var loaneesQuery = from l in _context.Loans
                               join m in _context.Members on l.MemberNo equals m.MemberNo
                               where memberNos.Contains(m.MemberNo)
                               select new { m.MemberNo, m.Sex };

            var loanees = await loaneesQuery
                .GroupBy(x => new { x.MemberNo, x.Sex })
                .Select(g => g.Key)
                .ToListAsync();

            dashboard.TotalLoanees = loanees.Count;
            dashboard.WomenLoanees = loanees.Count(x => x.Sex?.ToUpper() == "FEMALE");
            dashboard.MenLoanees = loanees.Count(x => x.Sex?.ToUpper() == "MALE");
            dashboard.OthersLoanees = loanees.Count(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE");

            // ============================================================
            // QUERY #8: Blockchain stats
            // ============================================================
            dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions.CountAsync();
            dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions.CountAsync(t => t.Status.ToUpper() == "PENDING");
            dashboard.BlocksCreatedToday = await _context.Blocks
                .Where(b => b.Timestamp.Date == DateTime.Today)
                .CountAsync();

            // ============================================================
            // QUERY #9: Grants from Journals
            // ============================================================
            var journalsQuery = _context.Journals.AsQueryable();
            if (!string.IsNullOrEmpty(companyCode))
                journalsQuery = journalsQuery.Where(j => j.CompanyCode == companyCode);

            dashboard.InclusionGrantTotal = await journalsQuery
                .Where(j => j.NARATION != null && j.NARATION.ToLower().Contains("inclusion grant") && j.TRANSTYPE.ToUpper() == "CR")
                .SumAsync(j => (decimal?)j.AMOUNT) ?? 0;

            dashboard.MatchingGrantTotal = await journalsQuery
                .Where(j => j.NARATION != null && j.NARATION.ToLower().Contains("matching grant") && j.TRANSTYPE.ToUpper() == "CR")
                .SumAsync(j => (decimal?)j.AMOUNT) ?? 0;

            // ============================================================
            // QUERY #10: Additional metrics
            // ============================================================
            // Repayment Rate (simplified for caching)
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            
            var repaymentsThisMonth = await _context.Repay
                .Where(r => r.DateReceived.HasValue && 
                           r.DateReceived.Value.Month == currentMonth && 
                           r.DateReceived.Value.Year == currentYear)
                .SumAsync(r => (decimal?)r.Amount) ?? 0;
                
            var totalExpectedPayments = dashboard.TotalLoanBalances > 0 ? dashboard.TotalLoanBalances / 12 : 1;
            dashboard.RepaymentRate = totalExpectedPayments > 0 ? (repaymentsThisMonth / totalExpectedPayments) * 100 : 0;
            dashboard.RepaymentRate = Math.Min(dashboard.RepaymentRate, 100);

            // PAR Percent (simplified)
            var overdueLoans = await _context.Loanbal
                .Where(lb => memberNos.Contains(lb.MemberNo) && lb.LastDate < DateTime.Now.AddMonths(-1))
                .SumAsync(lb => lb.Balance);
                
            dashboard.PARPercent = dashboard.TotalLoanBalances > 0 ? (overdueLoans / dashboard.TotalLoanBalances) * 100 : 0;
            
            // Arrears Balance
            dashboard.ArrearsBalance = overdueLoans;
            dashboard.TotalArrears = overdueLoans;
            
            // Outstanding Loan Portfolio
            dashboard.OutstandingLoanPortfolio = dashboard.TotalLoanBalances;
            
            // Amount Past Due Rate
            dashboard.AmountPastDueRate = dashboard.PARPercent;
            
            // Women Participation Rate
            dashboard.WomenParticipationRate = dashboard.TotalMembers > 0 ? (dashboard.TotalWomen * 100m / dashboard.TotalMembers) : 0;
            
            // Loan Portfolio Health
            if (dashboard.PARPercent < 5) dashboard.LoanPortfolioHealth = "Excellent";
            else if (dashboard.PARPercent < 10) dashboard.LoanPortfolioHealth = "Good";
            else if (dashboard.PARPercent < 20) dashboard.LoanPortfolioHealth = "Fair";
            else dashboard.LoanPortfolioHealth = "At Risk";

            // ============================================================
            // QUERY #11: Recent transactions
            // ============================================================
            var recentTxQuery = from t in _context.Transactions2
                                join m in _context.Members on t.MemberNo equals m.MemberNo
                                where t.Status.ToUpper() == "COMPLETED" && memberNos.Contains(m.MemberNo)
                                orderby t.ContributionDate descending
                                select new RecentTransaction
                                {
                                    TransactionId = t.TransactionNo,
                                    MemberName = m.Surname + " " + m.OtherNames,
                                    Type = t.TransactionType,
                                    Amount = t.Amount,
                                    Date = t.ContributionDate,
                                    Status = t.Status,
                                    BlockchainTxId = t.BlockchainTxId ?? "Pending"
                                };

            dashboard.RecentTransactions = await recentTxQuery.Take(10).ToListAsync();

            // ============================================================
            // QUERY #12: Quick Stats
            // ============================================================
            dashboard.QuickStats = new DashboardQuickStats
            {
                TransactionsToday = await _context.Transactions2
                    .CountAsync(t => t.ContributionDate.Date == DateTime.Today && t.Status.ToUpper() == "COMPLETED"),
                NewMembersToday = await memberQuery
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == DateTime.Today),
                AverageDeposit = await _context.Transactions2
                    .Where(t => t.TransactionType.ToUpper() == "DEPOSIT" && t.Status.ToUpper() == "COMPLETED")
                    .AverageAsync(t => (decimal?)t.Amount) ?? 0,
                AverageLoan = await _context.Loans
                    .Where(l => l.Status == 1)
                    .AverageAsync(l => (decimal?)l.LoanAmt) ?? 0,
                BlockchainUptime = 99.9m,
                LoanApprovalRate = 85.5m
            };

            // ============================================================
            // QUERY #13: Gender distribution
            // ============================================================
            dashboard.GenderStats = new GenderDistribution
            {
                MaleCount = dashboard.TotalMen,
                FemaleCount = dashboard.TotalWomen,
                OtherCount = dashboard.TotalOthers
            };

            // Set selected company name
            if (!string.IsNullOrEmpty(companyCode))
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
                dashboard.SelectedCompanyName = company?.CompanyName ?? companyCode;
            }
            else
            {
                dashboard.SelectedCompanyName = isSuperAdmin ? "All Companies" : "Main SACCO";
            }

            return dashboard;
        }

        private string NormalizeGender(string? gender)
        {
            if (string.IsNullOrEmpty(gender))
                return "OTHERS";

            var normalized = gender.ToUpper().Trim();
            if (normalized == "M" || normalized == "MALE") return "MALE";
            if (normalized == "F" || normalized == "FEMALE") return "FEMALE";
            return "OTHERS";
        }

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        public async Task InvalidateCacheAsync(string? companyCode = null)
        {
            var cacheKey = $"DashboardData_{companyCode ?? "ALL"}_True";
            _cache.Remove(cacheKey);

            cacheKey = $"DashboardData_{companyCode ?? "ALL"}_False";
            _cache.Remove(cacheKey);

            _logger.LogInformation($"Dashboard cache invalidated for: {companyCode ?? "ALL"}");
            await Task.CompletedTask;
        }
    }
}