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
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public DashboardCacheService(
            IMemoryCache cache,
            ApplicationDbContext context,
            ILogger<DashboardCacheService> logger)
        {
            _cache = cache;
            _context = context;
            _logger = logger;
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
                if (_cache.TryGetValue(cacheKey, out cachedDashboard))
                    return cachedDashboard;

                _logger.LogInformation($"Calculating dashboard for: {companyCode ?? "ALL"}");

                var dashboard = await CalculateDashboardDataAsync(companyCode, isSuperAdmin);

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

        private async Task<DashboardVM> CalculateDashboardDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var dashboard = new DashboardVM();

            // Build the member filter
            var memberQuery = _context.Members.AsQueryable();
            if (!isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                memberQuery = memberQuery.Where(m => m.CompanyCode == companyCode);
            else if (isSuperAdmin && !string.IsNullOrEmpty(companyCode))
                memberQuery = memberQuery.Where(m => m.CompanyCode == companyCode);
            // If SuperAdmin and no companyCode, include ALL

            // ============================================================
            // QUERY #1: Member statistics in ONE query
            // ============================================================
            var memberStats = await memberQuery
                .Select(m => new
                {
                    m.Sex,
                    m.Status,
                    m.MemberNo
                })
                .ToListAsync();

            dashboard.TotalMembers = memberStats.Count;
            dashboard.TotalWomen = memberStats.Count(m => m.Sex == "FEMALE");
            dashboard.TotalMen = memberStats.Count(m => m.Sex == "MALE");
            dashboard.TotalOthers = memberStats.Count(m => m.Sex != "FEMALE" && m.Sex != "MALE");
            dashboard.ActiveMembers = memberStats.Count(m => m.Status == 1);
            dashboard.ActiveMembersByStatus = dashboard.ActiveMembers;
            dashboard.ActiveWomen = memberStats.Count(m => m.Sex == "FEMALE" && m.Status == 1);
            dashboard.ActiveMen = memberStats.Count(m => m.Sex == "MALE" && m.Status == 1);
            dashboard.DormantMembers = dashboard.TotalMembers - dashboard.ActiveMembers;
            dashboard.DormantWomen = dashboard.TotalWomen - dashboard.ActiveWomen;
            dashboard.DormantMen = dashboard.TotalMen - dashboard.ActiveMen;

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
            // QUERY #3: Gender breakdown for financials (using Members join)
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
                                              RegFees = g.Sum(cs => cs.RegFeeAmount ?? 0)
                                          }).ToListAsync();

            foreach (var item in genderFinancials)
            {
                var gender = NormalizeGender(item.Gender);
                if (gender == "FEMALE")
                {
                    dashboard.WomenShareCapital = item.ShareCapital;
                    dashboard.WomenDeposits = item.Deposits;
                    dashboard.WomenRegistrationFees = item.RegFees;
                }
                else if (gender == "MALE")
                {
                    dashboard.MenShareCapital = item.ShareCapital;
                    dashboard.MenDeposits = item.Deposits;
                    dashboard.MenRegistrationFees = item.RegFees;
                }
                else
                {
                    dashboard.OthersShareCapital = item.ShareCapital;
                    dashboard.OthersDeposits = item.Deposits;
                    dashboard.OthersRegistrationFees = item.RegFees;
                }
            }

            // ============================================================
            // QUERY #4: Loan stats from Cheques table
            // ============================================================
            var loanStatsQuery = from c in _context.Cheques
                                 join l in _context.Loans on c.LoanNo equals l.LoanNo
                                 join m in _context.Members on l.MemberNo equals m.MemberNo
                                 where c.Amount > 0 && memberNos.Contains(m.MemberNo)
                                 select new { c.Amount, m.Sex };

            var loanStats = await loanStatsQuery.ToListAsync();

            dashboard.TotalLoansTaken = loanStats.Sum(c => c.Amount ?? 0);
            dashboard.WomenLoansTaken = loanStats.Where(c => c.Sex == "FEMALE").Sum(c => c.Amount ?? 0);
            dashboard.MenLoansTaken = loanStats.Where(c => c.Sex == "MALE").Sum(c => c.Amount ?? 0);
            dashboard.OthersLoansTaken = loanStats.Where(c => c.Sex != "FEMALE" && c.Sex != "MALE").Sum(c => c.Amount ?? 0);

            // ============================================================
            // QUERY #5: Loan balances from Loanbal table
            // ============================================================
            var loanBalancesQuery = from lb in _context.Loanbal
                                    join m in _context.Members on lb.MemberNo equals m.MemberNo
                                    where memberNos.Contains(m.MemberNo)
                                    select new { lb.Balance, m.Sex };

            var loanBalances = await loanBalancesQuery.ToListAsync();

            dashboard.TotalLoanBalances = loanBalances.Sum(lb => lb.Balance);
            dashboard.WomenLoanBalances = loanBalances.Where(lb => lb.Sex == "FEMALE").Sum(lb => lb.Balance);
            dashboard.MenLoanBalances = loanBalances.Where(lb => lb.Sex == "MALE").Sum(lb => lb.Balance);
            dashboard.OthersLoanBalances = loanBalances.Where(lb => lb.Sex != "FEMALE" && lb.Sex != "MALE").Sum(lb => lb.Balance);

            // ============================================================
            // QUERY #6: Loans paid from Repay table
            // ============================================================
            var loansPaidQuery = from r in _context.Repay
                                 join m in _context.Members on r.MemberNo equals m.MemberNo
                                 where r.Principal > 0 && memberNos.Contains(m.MemberNo)
                                 select new { r.Principal, m.Sex };

            var loansPaid = await loansPaidQuery.ToListAsync();

            dashboard.TotalLoansPaid = loansPaid.Sum(r => r.Principal ?? 0);
            dashboard.WomenLoansPaid = loansPaid.Where(r => r.Sex == "FEMALE").Sum(r => r.Principal ?? 0);
            dashboard.MenLoansPaid = loansPaid.Where(r => r.Sex == "MALE").Sum(r => r.Principal ?? 0);
            dashboard.OthersLoansPaid = loansPaid.Where(r => r.Sex != "FEMALE" && r.Sex != "MALE").Sum(r => r.Principal ?? 0);

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
            dashboard.WomenLoanees = loanees.Count(x => x.Sex == "FEMALE");
            dashboard.MenLoanees = loanees.Count(x => x.Sex == "MALE");
            dashboard.OthersLoanees = loanees.Count(x => x.Sex != "FEMALE" && x.Sex != "MALE");

            // ============================================================
            // QUERY #8: Blockchain stats
            // ============================================================
            dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions.CountAsync();
            dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions.CountAsync(t => t.Status == "PENDING");
            dashboard.BlocksCreatedToday = await _context.Blocks
                .Where(b => b.Timestamp.Date == DateTime.Today)
                .CountAsync();

            // ============================================================
            // QUERY #9: Recent transactions (LIMITED to 10)
            // ============================================================
            var recentTxQuery = from t in _context.Transactions2
                                join m in _context.Members on t.MemberNo equals m.MemberNo
                                where t.Status == "COMPLETED" && memberNos.Contains(m.MemberNo)
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
            // QUERY #10: Grants from Journals
            // ============================================================
            var journalsQuery = _context.Journals.AsQueryable();
            if (!string.IsNullOrEmpty(companyCode))
                journalsQuery = journalsQuery.Where(j => j.CompanyCode == companyCode);

            var inclusionGrant = await journalsQuery
                .Where(j => j.NARATION != null && j.NARATION.ToLower().Contains("inclusion grant") && j.TRANSTYPE == "CR")
                .SumAsync(j => (decimal?)j.AMOUNT) ?? 0;

            var matchingGrant = await journalsQuery
                .Where(j => j.NARATION != null && j.NARATION.ToLower().Contains("matching grant") && j.TRANSTYPE == "CR")
                .SumAsync(j => (decimal?)j.AMOUNT) ?? 0;

            dashboard.InclusionGrantTotal = inclusionGrant;
            dashboard.MatchingGrantTotal = matchingGrant;

            // ============================================================
            // QUERY #11: Youth statistics
            // ============================================================
            var today = DateTime.Today;
            var membersWithAge = memberStats
                .Join(_context.Members, ms => ms.MemberNo, m => m.MemberNo, (ms, m) => new { m.MemberNo, m.Sex, m.Dob })
                .Where(x => x.Dob.HasValue)
                .ToList();

            dashboard.YouthTotal = membersWithAge.Count(x =>
            {
                var age = CalculateAge(x.Dob.Value);
                return age <= 35;
            });

            dashboard.YouthMale = membersWithAge.Count(x =>
            {
                var age = CalculateAge(x.Dob.Value);
                return age <= 35 && x.Sex == "MALE";
            });

            dashboard.YouthFemale = membersWithAge.Count(x =>
            {
                var age = CalculateAge(x.Dob.Value);
                return age <= 35 && x.Sex == "FEMALE";
            });

            // ============================================================
            // QUERY #12: Gender distribution for GenderStats
            // ============================================================
            dashboard.GenderStats = new GenderDistribution
            {
                MaleCount = dashboard.TotalMen,
                FemaleCount = dashboard.TotalWomen,
                OtherCount = dashboard.TotalOthers
            };

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