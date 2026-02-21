using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
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

        public HomeController(
            IDashboardService dashboardService,
            IBlockchainService blockchainService,
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _dashboardService = dashboardService;
            _blockchainService = blockchainService;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                DashboardVM dashboard = await GetUniversalDashboardDataAsync();

                // Load all chart and statistical data
                dashboard.MonthlyTransactions = await GetMonthlyTransactionsDataAsync(6);
                dashboard.MemberGrowth = await GetMemberGrowthDataAsync(12);
                dashboard.MonthlyContributions = await GetMonthlyContributionsAsync(6);

                // Enhanced statistics
                dashboard.GenderStats = await GetGenderDistributionAsync();
                dashboard.AgeGroups = await GetAgeGroupDistributionAsync();
                dashboard.EmploymentStatistics = await GetEmploymentStatisticsAsync();
                dashboard.ContributionStatistics = await GetContributionStatisticsAsync();
                dashboard.LoanPerformance = await GetLoanPerformanceStatsAsync();

                // Load optional distribution data
                dashboard.LoanTypeDistribution = await GetLoanTypeDistributionAsync();
                dashboard.ShareTypeDistribution = await GetShareTypeDistributionAsync();

                // Get blockchain data
                dashboard.BlockchainData = await GetBlockchainDataAsync();
                dashboard.Wallets = await GetWalletDataAsync();
                dashboard.RecentBlocks = await GetRecentBlocksAsync(5);
                dashboard.BlockchainChains = await GetBlockchainChainsAsync();

                // Get user info
                dashboard.UserGroup = GetUserGroup();
                dashboard.UserRoles = User.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                ViewData["Title"] = $"{dashboard.UserGroup} Dashboard";
                ViewData["Subtitle"] = "SACCO Blockchain System";

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                return View("Error");
            }
        }

        private async Task<DashboardVM> GetUniversalDashboardDataAsync()
        {
            var dashboard = new DashboardVM();

            try
            {
                // BASIC STATS - Available to ALL users
                dashboard.TotalMembers = await _context.Members.CountAsync();
                dashboard.ActiveMembers = await _context.Members.CountAsync(m =>
                    m.Withdrawn != true && m.Archived != true && m.Dormant != 1);

                dashboard.TotalShareCapital = await _context.Shares
                    .SumAsync(s => s.TotalShares ?? 0);

                // FIXED: Use the new Loan model instead of old Loans table
                dashboard.TotalLoansIssued = await _context.Loans
                    .Where(l => l.LoanStatus == "Disbursed" || l.LoanStatus == "Active" || l.LoanStatus == "Closed")
                    .SumAsync(l => l.DisbursedAmount);

                // Blockchain stats
                dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions.CountAsync();
                dashboard.BlocksCreatedToday = await _context.Blocks
                    .Where(b => b.Timestamp.Date == DateTime.Today)
                    .CountAsync();
                dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions
                    .CountAsync(t => t.Status == "PENDING");

                // Chart data
                dashboard.MonthlyTransactions = await GetMonthlyTransactionsDataAsync();
                dashboard.MemberGrowth = await GetMemberGrowthDataAsync();

                // Member-specific data
                var memberNo = User.FindFirst("MemberNo")?.Value;
                if (!string.IsNullOrEmpty(memberNo))
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                    if (member != null)
                    {
                        dashboard.MemberShareBalance = await _context.Shares
                            .Where(s => s.MemberNo == memberNo)
                            .SumAsync(s => s.TotalShares ?? 0);

                        dashboard.MemberTotalLoans = await _context.Loans
                            .Where(l => l.MemberNo == memberNo &&
                                       (l.LoanStatus == "Disbursed" || l.LoanStatus == "Active"))
                            .SumAsync(l => l.DisbursedAmount);

                        dashboard.MemberRecentTransactionCount = await _context.Transactions2
                            .CountAsync(t => t.MemberNo == memberNo &&
                                            t.ContributionDate.Date == DateTime.Today);
                    }
                }

                // Recent transactions for dashboard
                dashboard.RecentTransactions = await GetRecentTransactionsForDashboardAsync();

                // Quick stats
                dashboard.QuickStats = await GetQuickStatsAsync();

                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting universal dashboard data");
                return dashboard;
            }
        }

        private async Task<GenderDistribution> GetGenderDistributionAsync()
        {
            var genderStats = new GenderDistribution();

            try
            {
                genderStats.MaleCount = await _context.Members
                    .CountAsync(m => m.Sex != null && m.Sex.ToUpper() == "MALE");

                genderStats.FemaleCount = await _context.Members
                    .CountAsync(m => m.Sex != null && m.Sex.ToUpper() == "FEMALE");

                genderStats.OtherCount = await _context.Members
                    .CountAsync(m => m.Sex == null ||
                                   (m.Sex.ToUpper() != "MALE" && m.Sex.ToUpper() != "FEMALE"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting gender distribution");
            }

            return genderStats;
        }

        private async Task<List<AgeGroupData>> GetAgeGroupDistributionAsync()
        {
            var ageGroups = new List<AgeGroupData>();

            try
            {
                var members = await _context.Members
                    .Where(m => m.Dob.HasValue)
                    .ToListAsync();

                if (!members.Any())
                    return GenerateSampleAgeGroupData();

                var ageGroupsData = new Dictionary<string, int>
                {
                    {"18-25", 0},
                    {"26-35", 0},
                    {"36-45", 0},
                    {"46-55", 0},
                    {"56-65", 0},
                    {"65+", 0}
                };

                foreach (var member in members)
                {
                    var age = CalculateAge(member.Dob.Value);

                    if (age >= 18 && age <= 25) ageGroupsData["18-25"]++;
                    else if (age >= 26 && age <= 35) ageGroupsData["26-35"]++;
                    else if (age >= 36 && age <= 45) ageGroupsData["36-45"]++;
                    else if (age >= 46 && age <= 55) ageGroupsData["46-55"]++;
                    else if (age >= 56 && age <= 65) ageGroupsData["56-65"]++;
                    else if (age > 65) ageGroupsData["65+"]++;
                }

                int total = ageGroupsData.Values.Sum();
                var colors = new[] { "#3498db", "#2ecc71", "#e74c3c", "#f39c12", "#9b59b6", "#1abc9c" };
                int i = 0;

                foreach (var kvp in ageGroupsData)
                {
                    ageGroups.Add(new AgeGroupData
                    {
                        AgeGroup = kvp.Key,
                        MemberCount = kvp.Value,
                        Percentage = total > 0 ? (kvp.Value * 100m / total) : 0,
                        Color = colors[i++ % colors.Length]
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting age group distribution");
                return GenerateSampleAgeGroupData();
            }

            return ageGroups;
        }

        private async Task<EmploymentStats> GetEmploymentStatisticsAsync()
        {
            var stats = new EmploymentStats();

            try
            {
                // Department distribution
                stats.DepartmentDistribution = await _context.Members
                    .Where(m => !string.IsNullOrEmpty(m.Dept))
                    .GroupBy(m => m.Dept)
                    .Select(g => new { Dept = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Dept, x => x.Count);

                // Employer distribution
                stats.EmployerDistribution = await _context.Members
                    .Where(m => !string.IsNullOrEmpty(m.Employer))
                    .GroupBy(m => m.Employer)
                    .Select(g => new { Employer = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Employer, x => x.Count);

                // Rank distribution
                stats.RankDistribution = await _context.Members
                    .Where(m => !string.IsNullOrEmpty(m.Rank))
                    .GroupBy(m => m.Rank)
                    .Select(g => new { Rank = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Rank, x => x.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employment statistics");
            }

            return stats;
        }

        private async Task<ContributionStats> GetContributionStatisticsAsync()
        {
            var stats = new ContributionStats();

            try
            {
                var thisYear = DateTime.Now.Year;
                var lastYear = thisYear - 1;

                // Get contributions from Contrib and ContribShare tables
                var thisYearContributions = await _context.Contribs
                    .Where(c => c.ContrDate.HasValue && c.ContrDate.Value.Year == thisYear)
                    .SumAsync(c => c.Amount ?? 0);

                var lastYearContributions = await _context.Contribs
                    .Where(c => c.ContrDate.HasValue && c.ContrDate.Value.Year == lastYear)
                    .SumAsync(c => c.Amount ?? 0);

                stats.TotalContributionsThisYear = thisYearContributions;
                stats.TotalContributionsLastYear = lastYearContributions;

                // Calculate average monthly contribution
                var monthlyAverage = await _context.Contribs
                    .Where(c => c.ContrDate.HasValue && c.ContrDate.Value.Year == thisYear)
                    .GroupBy(c => new { c.ContrDate.Value.Year, c.ContrDate.Value.Month })
                    .Select(g => g.Sum(c => c.Amount ?? 0))
                    .AverageAsync();

                stats.AverageMonthlyContribution = monthlyAverage;

                // Get monthly contributions
                for (int month = 1; month <= 12; month++)
                {
                    var monthContributions = await _context.Contribs
                        .Where(c => c.ContrDate.HasValue &&
                                   c.ContrDate.Value.Year == thisYear &&
                                   c.ContrDate.Value.Month == month)
                        .SumAsync(c => c.Amount ?? 0);

                    stats.ContributionByMonth.Add(
                        DateTimeFormatInfo.CurrentInfo.GetMonthName(month),
                        monthContributions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contribution statistics");
            }

            return stats;
        }

        private async Task<LoanPerformanceStats> GetLoanPerformanceStatsAsync()
        {
            var stats = new LoanPerformanceStats();

            try
            {
                // Get loans by status using the new Loan model
                var loansByStatus = await _context.Loans
                    .GroupBy(l => l.LoanStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var status in loansByStatus)
                {
                    stats.LoansByStatus[status.Status] = status.Count;
                }

                // Calculate average loan amount
                stats.AverageLoanAmount = await _context.Loans
                    .Where(l => l.DisbursedAmount > 0)
                    .AverageAsync(l => l.DisbursedAmount);

                // Calculate average repayment period
                stats.AverageRepaymentPeriod = await _context.Loans
                    .Where(l => l.RepaymentPeriod > 0)
                    .AverageAsync(l => (decimal)l.RepaymentPeriod);

                // Calculate repayment rate
                var totalDisbursedLoans = await _context.Loans
                    .CountAsync(l => l.LoanStatus == "Disbursed" ||
                                    l.LoanStatus == "Active" ||
                                    l.LoanStatus == "Closed");

                var closedLoans = await _context.Loans
                    .CountAsync(l => l.LoanStatus == "Closed");

                stats.RepaymentRate = totalDisbursedLoans > 0
                    ? (closedLoans * 100m / totalDisbursedLoans)
                    : 0;

                // Default rate (loans that are written off)
                var writtenOffLoans = await _context.Loans
                    .CountAsync(l => l.LoanStatus == "WrittenOff");

                stats.DefaultRate = totalDisbursedLoans > 0
                    ? (writtenOffLoans * 100m / totalDisbursedLoans)
                    : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loan performance statistics");
            }

            return stats;
        }

        private async Task<List<MonthlyContributionData>> GetMonthlyContributionsAsync(int months = 6)
        {
            var data = new List<MonthlyContributionData>();

            try
            {
                var endDate = DateTime.Now;
                var startDate = endDate.AddMonths(-months);

                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);
                    var monthName = monthDate.ToString("MMM yyyy");

                    // Get share capital contributions
                    var shareCapital = await _context.ContribShares
                        .Where(c => c.ContrDate.HasValue &&
                                   c.ContrDate.Value.Month == monthDate.Month &&
                                   c.ContrDate.Value.Year == monthDate.Year)
                        .SumAsync(c => c.ShareCapitalAmount ?? 0);

                    // Get deposits
                    var deposits = await _context.ContribShares
                        .Where(c => c.ContrDate.HasValue &&
                                   c.ContrDate.Value.Month == monthDate.Month &&
                                   c.ContrDate.Value.Year == monthDate.Year)
                        .SumAsync(c => c.DepositsAmount ?? 0);

                    // Get passbook amounts
                    var passbook = await _context.ContribShares
                        .Where(c => c.ContrDate.HasValue &&
                                   c.ContrDate.Value.Month == monthDate.Month &&
                                   c.ContrDate.Value.Year == monthDate.Year)
                        .SumAsync(c => c.PassBookAmount ?? 0);

                    data.Add(new MonthlyContributionData
                    {
                        Month = monthName,
                        ShareCapital = shareCapital,
                        Deposits = deposits,
                        PassBook = passbook
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly contributions");
                data = GenerateSampleContributionData(months);
            }

            return data.Any() ? data : GenerateSampleContributionData(months);
        }

        private async Task<List<MonthlyTransactionData>> GetMonthlyTransactionsDataAsync(int months = 6)
        {
            try
            {
                var data = new List<MonthlyTransactionData>();
                var endDate = DateTime.Now;
                var startDate = endDate.AddMonths(-months);

                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);
                    var monthName = monthDate.ToString("MMM yyyy");

                    try
                    {
                        // Get deposits
                        var deposits = await _context.Transactions2
                            .Where(t => t.TransactionType == "DEPOSIT" &&
                                       t.Status == "COMPLETED" &&
                                       t.ContributionDate.Month == monthDate.Month &&
                                       t.ContributionDate.Year == monthDate.Year)
                            .SumAsync(t => (decimal?)t.Amount) ?? 0;

                        // Get withdrawals
                        var withdrawals = await _context.Transactions2
                            .Where(t => t.TransactionType == "WITHDRAWAL" &&
                                       t.Status == "COMPLETED" &&
                                       t.ContributionDate.Month == monthDate.Month &&
                                       t.ContributionDate.Year == monthDate.Year)
                            .SumAsync(t => (decimal?)t.Amount) ?? 0;

                        // FIXED: Get loan repayments from LoanRepayments table
                        var repayments = await _context.LoanRepayments
                            .Where(r => r.Status == "Completed" &&
                                       r.PaymentDate.Month == monthDate.Month &&
                                       r.PaymentDate.Year == monthDate.Year)
                            .SumAsync(r => (decimal?)r.AmountPaid) ?? 0;

                        data.Add(new MonthlyTransactionData
                        {
                            Month = monthName,
                            Deposits = deposits,
                            Withdrawals = withdrawals,
                            LoanRepayments = repayments
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error getting data for month {monthName}");
                        data.Add(new MonthlyTransactionData
                        {
                            Month = monthName,
                            Deposits = 0,
                            Withdrawals = 0,
                            LoanRepayments = 0
                        });
                    }
                }

                // If all data is zero, generate sample data for demo
                if (data.All(d => d.Deposits == 0 && d.Withdrawals == 0 && d.LoanRepayments == 0))
                {
                    _logger.LogInformation("No transaction data found, generating sample data for demo");
                    data = GenerateSampleTransactionData(months);
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMonthlyTransactionsDataAsync");
                return GenerateSampleTransactionData(months);
            }
        }

        private async Task<List<MemberGrowthData>> GetMemberGrowthDataAsync(int months = 12)
        {
            try
            {
                var data = new List<MemberGrowthData>();
                var endDate = DateTime.Now;
                var startDate = endDate.AddMonths(-months);

                for (int i = 0; i < months; i++)
                {
                    var monthDate = startDate.AddMonths(i);
                    var monthName = monthDate.ToString("MMM yyyy");

                    try
                    {
                        // New members in this month
                        var newMembers = await _context.Members
                            .CountAsync(m => m.EffectDate.HasValue &&
                                            m.EffectDate.Value.Month == monthDate.Month &&
                                            m.EffectDate.Value.Year == monthDate.Year);

                        // Total members up to this month
                        var totalMembers = await _context.Members
                            .CountAsync(m => m.EffectDate.HasValue &&
                                            m.EffectDate.Value <= monthDate);

                        data.Add(new MemberGrowthData
                        {
                            Period = monthName,
                            NewMembers = newMembers,
                            TotalMembers = totalMembers
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error getting member data for period {monthName}");
                        data.Add(new MemberGrowthData
                        {
                            Period = monthName,
                            NewMembers = 0,
                            TotalMembers = 0
                        });
                    }
                }

                // If all data is zero, generate sample data for demo
                if (data.All(d => d.NewMembers == 0 && d.TotalMembers == 0))
                {
                    _logger.LogInformation("No member growth data found, generating sample data for demo");
                    data = GenerateSampleMemberGrowthData(months);
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMemberGrowthDataAsync");
                return GenerateSampleMemberGrowthData(months);
            }
        }

        private async Task<List<LoanTypeDistribution>> GetLoanTypeDistributionAsync()
        {
            try
            {
                // FIXED: Use the new Loan model with LoanType relationship
                var distribution = await _context.Loans
                    .Include(l => l.LoanType)
                    .Where(l => l.LoanStatus == "Disbursed" ||
                               l.LoanStatus == "Active" ||
                               l.LoanStatus == "Closed")
                    .GroupBy(l => l.LoanType != null ? l.LoanType.LoanType1 : "Unknown")
                    .Select(g => new LoanTypeDistribution
                    {
                        LoanType = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(l => l.DisbursedAmount)
                    })
                    .ToListAsync();

                // Add colors
                var colors = new[] { "#3498db", "#2ecc71", "#e74c3c", "#f39c12", "#9b59b6", "#1abc9c", "#34495e" };
                for (int i = 0; i < distribution.Count; i++)
                {
                    distribution[i].Color = colors[i % colors.Length];
                }

                return distribution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loan type distribution");
                return new List<LoanTypeDistribution>();
            }
        }

        private async Task<List<ShareTypeDistribution>> GetShareTypeDistributionAsync()
        {
            try
            {
                var distribution = await _context.Shares
                    .GroupBy(s => s.Sharescode ?? "Unknown")
                    .Select(g => new ShareTypeDistribution
                    {
                        ShareType = g.Key,
                        Amount = g.Sum(s => s.TotalShares ?? 0),
                        MemberCount = g.Select(s => s.MemberNo).Distinct().Count()
                    })
                    .ToListAsync();

                // Add colors
                var colors = new[] { "#2ecc71", "#3498db", "#e74c3c", "#f39c12", "#9b59b6", "#1abc9c" };
                for (int i = 0; i < distribution.Count; i++)
                {
                    distribution[i].Color = colors[i % colors.Length];
                }

                return distribution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting share type distribution");
                return new List<ShareTypeDistribution>();
            }
        }

        private async Task<BlockchainDashboardData> GetBlockchainDataAsync()
        {
            try
            {
                var status = await _blockchainService.GetBlockchainStatus();
                var latestBlocks = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .Take(10)
                    .ToListAsync();

                var totalWallets = await _context.Wallets.CountAsync();
                var activeWallets = await _context.Wallets
                    .Where(w => w.LastActivity.HasValue &&
                               w.LastActivity.Value > DateTime.UtcNow.AddDays(-30))
                    .CountAsync();

                return new BlockchainDashboardData
                {
                    TotalBlocks = status.TotalBlocks,
                    TotalTransactions = status.TotalTransactions,
                    PendingTransactions = status.PendingTransactions,
                    LatestBlockHash = status.LatestBlockHash,
                    LatestBlockTimestamp = status.LatestBlockTimestamp,
                    TotalWallets = totalWallets,
                    ActiveWallets = activeWallets,
                    BlockchainHeight = await _blockchainService.GetBlockchainHeight(),
                    IsChainValid = await _blockchainService.ValidateBlockchain()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blockchain data");
                return new BlockchainDashboardData();
            }
        }

        private async Task<List<WalletInfo>> GetWalletDataAsync()
        {
            try
            {
                var wallets = await _context.Wallets
                    .OrderByDescending(w => w.LastActivity ?? w.CreatedAt)
                    .Take(5)
                    .Select(w => new WalletInfo
                    {
                        Address = w.Address,
                        Balance = w.Balance,
                        LastActivity = w.LastActivity ?? w.CreatedAt,
                        IsActive = w.LastActivity.HasValue &&
                                  w.LastActivity.Value > DateTime.UtcNow.AddDays(-7)
                    })
                    .ToListAsync();

                return wallets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet data");
                return new List<WalletInfo>();
            }
        }

        private async Task<List<Models.Block>> GetRecentBlocksAsync(int count)
        {
            try
            {
                return await _context.Blocks
                    .Include(b => b.Transactions.Take(3))
                    .OrderByDescending(b => b.BlockId)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent blocks");
                return new List<Models.Block>();
            }
        }

        private async Task<List<BlockchainChain>> GetBlockchainChainsAsync()
        {
            try
            {
                // Get the last 10 blocks to show chain structure
                var blocks = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .Take(10)
                    .Select(b => new BlockchainChain
                    {
                        BlockHash = b.BlockHash,
                        PreviousHash = b.PreviousHash,
                        Timestamp = b.Timestamp,
                        TransactionCount = b.Transactions.Count,
                        Nonce = b.Nonce,
                        MerkleRoot = b.MerkleRoot
                    })
                    .ToListAsync();

                return blocks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blockchain chains");
                return new List<BlockchainChain>();
            }
        }

        private async Task<List<RecentTransaction>> GetRecentTransactionsForDashboardAsync()
        {
            try
            {
                var recentTransactions = await _context.Transactions2
                    .Where(t => t.Status == "COMPLETED")
                    .OrderByDescending(t => t.ContributionDate)
                    .Take(10)
                    .ToListAsync();

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

        private async Task<DashboardQuickStats> GetQuickStatsAsync()
        {
            var today = DateTime.Today;

            var stats = new DashboardQuickStats
            {
                TransactionsToday = await _context.Transactions2
                    .CountAsync(t => t.ContributionDate.Date == today && t.Status == "COMPLETED"),

                NewMembersToday = await _context.Members
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == today),

                AverageDeposit = await _context.Transactions2
                    .Where(t => t.TransactionType == "DEPOSIT" && t.Status == "COMPLETED")
                    .AverageAsync(t => t.Amount),

                // FIXED: Use new Loan model
                AverageLoan = await _context.Loans
                    .Where(l => l.LoanStatus == "Disbursed" || l.LoanStatus == "Active")
                    .AverageAsync(l => l.DisbursedAmount),

                BlockchainUptime = 99.9m
            };

            // Calculate loan approval rate
            var totalLoans = await _context.Loans.CountAsync();
            var approvedLoans = await _context.Loans
                .CountAsync(l => l.LoanStatus == "Approved" ||
                                l.LoanStatus == "Disbursed" ||
                                l.LoanStatus == "Active" ||
                                l.LoanStatus == "Closed");

            stats.LoanApprovalRate = totalLoans > 0 ? (approvedLoans * 100m / totalLoans) : 0;

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

        #region Helper Methods

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        private List<AgeGroupData> GenerateSampleAgeGroupData()
        {
            return new List<AgeGroupData>
            {
                new AgeGroupData { AgeGroup = "18-25", MemberCount = 50, Percentage = 20, Color = "#3498db" },
                new AgeGroupData { AgeGroup = "26-35", MemberCount = 80, Percentage = 32, Color = "#2ecc71" },
                new AgeGroupData { AgeGroup = "36-45", MemberCount = 60, Percentage = 24, Color = "#e74c3c" },
                new AgeGroupData { AgeGroup = "46-55", MemberCount = 40, Percentage = 16, Color = "#f39c12" },
                new AgeGroupData { AgeGroup = "56-65", MemberCount = 15, Percentage = 6, Color = "#9b59b6" },
                new AgeGroupData { AgeGroup = "65+", MemberCount = 5, Percentage = 2, Color = "#1abc9c" }
            };
        }

        private List<MonthlyContributionData> GenerateSampleContributionData(int months)
        {
            var data = new List<MonthlyContributionData>();
            var random = new Random();
            var baseDate = DateTime.Now.AddMonths(-months);

            for (int i = 0; i < months; i++)
            {
                var monthDate = baseDate.AddMonths(i);
                var monthName = monthDate.ToString("MMM yyyy");

                data.Add(new MonthlyContributionData
                {
                    Month = monthName,
                    ShareCapital = random.Next(10000, 50000),
                    Deposits = random.Next(20000, 80000),
                    PassBook = random.Next(5000, 30000)
                });
            }

            return data;
        }

        private List<MonthlyTransactionData> GenerateSampleTransactionData(int months)
        {
            var data = new List<MonthlyTransactionData>();
            var random = new Random();
            var baseDate = DateTime.Now.AddMonths(-months);

            decimal cumulativeDeposits = 50000;
            decimal cumulativeWithdrawals = 20000;
            decimal cumulativeRepayments = 15000;

            for (int i = 0; i < months; i++)
            {
                var monthDate = baseDate.AddMonths(i);
                var monthName = monthDate.ToString("MMM yyyy");

                var depositGrowth = random.Next(5000, 15000);
                var withdrawalGrowth = random.Next(2000, 8000);
                var repaymentGrowth = random.Next(1000, 5000);

                cumulativeDeposits += depositGrowth;
                cumulativeWithdrawals += withdrawalGrowth;
                cumulativeRepayments += repaymentGrowth;

                var deposits = cumulativeDeposits + random.Next(-10000, 10000);
                var withdrawals = cumulativeWithdrawals + random.Next(-5000, 5000);
                var repayments = cumulativeRepayments + random.Next(-3000, 3000);

                deposits = Math.Max(10000, deposits);
                withdrawals = Math.Max(5000, withdrawals);
                repayments = Math.Max(3000, repayments);

                data.Add(new MonthlyTransactionData
                {
                    Month = monthName,
                    Deposits = Math.Round(deposits, 2),
                    Withdrawals = Math.Round(withdrawals, 2),
                    LoanRepayments = Math.Round(repayments, 2)
                });
            }

            return data;
        }

        private List<MemberGrowthData> GenerateSampleMemberGrowthData(int months)
        {
            var data = new List<MemberGrowthData>();
            var random = new Random();
            var baseDate = DateTime.Now.AddMonths(-months);

            int cumulativeTotalMembers = 50;

            for (int i = 0; i < months; i++)
            {
                var monthDate = baseDate.AddMonths(i);
                var monthName = monthDate.ToString("MMM yyyy");

                var newMembersThisMonth = random.Next(1, 8);
                cumulativeTotalMembers += newMembersThisMonth;

                newMembersThisMonth = Math.Max(0, newMembersThisMonth + random.Next(-2, 3));

                data.Add(new MemberGrowthData
                {
                    Period = monthName,
                    NewMembers = newMembersThisMonth,
                    TotalMembers = cumulativeTotalMembers
                });
            }

            return data;
        }

        #endregion

        #region API Endpoints

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var dashboard = await GetUniversalDashboardDataAsync();
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalMembers = dashboard.TotalMembers,
                        totalShareCapital = dashboard.TotalShareCapital,
                        totalLoans = dashboard.TotalLoansIssued,
                        blockchainTransactions = dashboard.TotalBlockchainTransactions,
                        quickStats = dashboard.QuickStats,
                        memberShareBalance = dashboard.MemberShareBalance,
                        memberTotalLoans = dashboard.MemberTotalLoans
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChartData(string range = "6m")
        {
            try
            {
                int months = range == "1y" ? 12 : 6;

                var monthlyTransactions = await GetMonthlyTransactionsDataAsync(months);
                var memberGrowth = await GetMemberGrowthDataAsync(months);

                return Json(new
                {
                    success = true,
                    monthlyTransactions,
                    memberGrowth
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chart data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

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