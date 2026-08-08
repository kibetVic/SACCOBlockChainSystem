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
        Task<DashboardVM> GetDashboardDataForCompaniesAsync(List<string> companyCodes, bool isCountyAdmin);
        Task InvalidateCacheForCountyAsync(string county);
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


        private async Task<decimal> CalculateOutstandingLoanPortfolioAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var query = _context.Loanbal.AsQueryable();

                // Apply company filter
                if (!string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(lb => lb.Companycode == companyCode);
                }

                // Sum all balances (outstanding loan portfolio)
                decimal outstandingPortfolio = await query.SumAsync(lb => lb.Balance);

                _logger.LogInformation($"Outstanding Loan Portfolio: {outstandingPortfolio:C}");

                return outstandingPortfolio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating outstanding loan portfolio");
                return 0;
            }
        }


        private async Task<decimal> CalculateTotalArrearsAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var currentDate = DateTime.Now;

                // STEP 1: Get all active loans in ONE query
                var loansQuery = from lb in _context.Loanbal
                                 join l in _context.Loans on lb.LoanNo equals l.LoanNo
                                 join lt in _context.Loantypes on l.LoanCode equals lt.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where lb.Balance > 0
                                 select new
                                 {
                                     l.LoanNo,
                                     l.AuditTime,
                                     l.ApplicDate,
                                     lb.Balance,
                                     lb.LastDate,
                                     lb.RepayMethod,
                                     l.CompanyCode,
                                     l.LoanAmt,
                                     l.RepayPeriod,
                                     l.Interest,
                                     RepaymentMethod = lt != null ? lt.Repaymethod : lb.RepayMethod,
                                     GracePeriod = lt != null ? lt.GracePeriod : 0
                                 };

                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(x => x.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();

                if (!loans.Any()) return 0;

                // STEP 2: Get ALL last repayments in ONE query (BATCH LOADING)
                var loanNos = loans.Select(l => l.LoanNo).ToList();

                var lastRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo) && r.DateReceived.HasValue && r.Posted == true)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastDateReceived = g.Max(r => r.DateReceived),
                        LastPrincipal = g.OrderByDescending(r => r.DateReceived).Select(r => r.Principal).FirstOrDefault(),
                        LastAmount = g.OrderByDescending(r => r.DateReceived).Select(r => r.Amount).FirstOrDefault()
                    })
                    .ToDictionaryAsync(g => g.LoanNo, g => g);

                decimal totalMissedPayments = 0;
                int loansInArrears = 0;

                // STEP 3: Process in memory (NO MORE DATABASE CALLS)
                foreach (var loan in loans)
                {
                    // Get last payment from dictionary (in-memory lookup)
                    var lastRepayment = lastRepayments.ContainsKey(loan.LoanNo)
                        ? lastRepayments[loan.LoanNo]
                        : null;

                    DateTime? actualLastPaymentDate = lastRepayment?.LastDateReceived ?? loan.LastDate;

                    if (actualLastPaymentDate == DateTime.MinValue || actualLastPaymentDate == null)
                    {
                        actualLastPaymentDate = loan.AuditTime;
                    }

                    DateTime firstDueDate = loan.AuditTime.AddMonths(1).AddDays(loan.GracePeriod);

                    if (currentDate <= firstDueDate) continue;

                    // Calculate expected payments
                    int totalMonthsSinceDisbursement = ((currentDate.Year - loan.AuditTime.Year) * 12) +
                                                        (currentDate.Month - loan.AuditTime.Month);
                    if (totalMonthsSinceDisbursement < 0) totalMonthsSinceDisbursement = 0;

                    int expectedPayments = totalMonthsSinceDisbursement;

                    int actualPayments = 0;
                    if (actualLastPaymentDate.HasValue && actualLastPaymentDate.Value != DateTime.MinValue && actualLastPaymentDate.Value > loan.AuditTime)
                    {
                        actualPayments = ((actualLastPaymentDate.Value.Year - loan.AuditTime.Year) * 12) +
                                         (actualLastPaymentDate.Value.Month - loan.AuditTime.Month);
                        if (actualPayments < 0) actualPayments = 0;
                    }

                    int monthsMissed = expectedPayments - actualPayments;
                    if (monthsMissed <= 0) continue;

                    // Check if in arrears (>30 days)
                    bool isInArrears = false;
                    if (actualLastPaymentDate.HasValue && actualLastPaymentDate.Value != DateTime.MinValue)
                    {
                        var daysSinceLastPayment = (currentDate - actualLastPaymentDate.Value).Days;
                        isInArrears = daysSinceLastPayment > 30;
                    }
                    else
                    {
                        var daysSinceFirstDue = (currentDate - firstDueDate).Days;
                        isInArrears = daysSinceFirstDue > 30;
                    }

                    if (!isInArrears) continue;

                    // Calculate missed amount based on repayment method
                    decimal missedAmount = 0;
                    decimal monthlyPayment = 0;

                    switch (loan.RepaymentMethod?.ToUpper())
                    {
                        case "AMT":
                            monthlyPayment = CalculateMonthlyPaymentAmt(loan.LoanAmt ?? 0, loan.RepayPeriod ?? 12, loan.Interest ?? 0);
                            missedAmount = monthlyPayment * monthsMissed;
                            break;
                        case "STL":
                            missedAmount = CalculateMissedAmountStl(loan, monthsMissed);
                            break;
                        case "RBAL":
                            missedAmount = CalculateMissedAmountRbal(loan, monthsMissed);
                            break;
                        default:
                            monthlyPayment = (loan.LoanAmt ?? 0) / (loan.RepayPeriod ?? 12);
                            missedAmount = monthlyPayment * monthsMissed;
                            break;
                    }

                    missedAmount = Math.Min(missedAmount, loan.Balance);

                    if (missedAmount > 0)
                    {
                        totalMissedPayments += missedAmount;
                        loansInArrears++;
                    }
                }

                _logger.LogInformation($"=== TOTAL ARREARS CALCULATION ===");
                _logger.LogInformation($"Loans in Arrears: {loansInArrears}");
                _logger.LogInformation($"Total Missed Payments: {totalMissedPayments:C}");

                return totalMissedPayments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total arrears");
                return 0;
            }
        }

        /// <summary>
        /// Calculates the balance of loans with arrears >30 days
        /// Returns the ENTIRE outstanding balance of overdue loans
        /// </summary>
        private async Task<decimal> CalculateArrearsBalanceAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var currentDate = DateTime.Now;

                var loansQuery = from lb in _context.Loanbal
                                 join l in _context.Loans on lb.LoanNo equals l.LoanNo
                                 where lb.Balance > 0
                                 select new
                                 {
                                     l.LoanNo,
                                     l.AuditTime,
                                     lb.Balance,
                                     lb.LastDate,
                                     l.CompanyCode
                                 };

                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(x => x.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();
                decimal arrearsBalance = 0;

                foreach (var loan in loans)
                {
                    // Get actual last payment from Repay table
                    var lastRepayment = await _context.Repay
                        .Where(r => r.LoanNo == loan.LoanNo && r.DateReceived.HasValue && r.Posted == true)
                        .OrderByDescending(r => r.DateReceived)
                        .Select(r => r.DateReceived)
                        .FirstOrDefaultAsync();

                    DateTime? lastPaymentDate = lastRepayment ?? loan.LastDate;

                    // Calculate days overdue
                    int daysOverdue = 0;
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);

                    if (lastPaymentDate.HasValue && lastPaymentDate.Value != DateTime.MinValue)
                    {
                        var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (currentDate > nextDueDate)
                        {
                            daysOverdue = (currentDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        if (currentDate > firstDueDate)
                        {
                            daysOverdue = (currentDate - firstDueDate).Days;
                        }
                    }

                    if (daysOverdue > 30)
                    {
                        arrearsBalance += loan.Balance;
                    }
                }

                return arrearsBalance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating arrears balance");
                return 0;
            }
        }

        // Helper Methods for different repayment types

        private int CalculateActualPaymentsAmt(dynamic loan, DateTime? lastPaymentDate)
        {
            if (!lastPaymentDate.HasValue || lastPaymentDate.Value == DateTime.MinValue) return 0;

            // Count number of payments made based on months between disbursement and last payment
            int payments = ((lastPaymentDate.Value.Year - loan.AuditTime.Year) * 12) +
                           (lastPaymentDate.Value.Month - loan.AuditTime.Month);

            // Add one if we're past the disbursement month
            if (lastPaymentDate.Value.Day >= loan.AuditTime.Day)
            {
                payments++;
            }

            return payments > 0 ? payments : 0;
        }

        private int CalculateActualPaymentsStl(dynamic loan, DateTime? lastPaymentDate)
        {
            // Same logic as AMT for payment count
            return CalculateActualPaymentsAmt(loan, lastPaymentDate);
        }

        private int CalculateActualPaymentsRbal(dynamic loan, DateTime? lastPaymentDate)
        {
            // Same logic for payment count
            return CalculateActualPaymentsAmt(loan, lastPaymentDate);
        }

        private decimal CalculateMonthlyPaymentAmt(decimal loanAmount, int repayPeriod, decimal interestRate)
        {
            if (repayPeriod <= 0) return loanAmount;

            decimal monthlyInterestRate = (interestRate / 100) / 12;

            if (monthlyInterestRate <= 0) return loanAmount / repayPeriod;

            // Standard amortization formula
            decimal monthlyPayment = loanAmount * monthlyInterestRate *
                                     (decimal)Math.Pow(1 + (double)monthlyInterestRate, repayPeriod) /
                                     ((decimal)Math.Pow(1 + (double)monthlyInterestRate, repayPeriod) - 1);

            return monthlyPayment > 0 ? monthlyPayment : loanAmount / repayPeriod;
        }

        private decimal CalculateMissedAmountStl(dynamic loan, int monthsMissed)
        {
            // Straight Line: Principal is equal each month, interest declines
            decimal principalPerMonth = (loan.LoanAmt ?? 0) / (loan.RepayPeriod ?? 12);
            decimal totalMissed = 0;
            decimal remainingPrincipal = loan.LoanAmt ?? 0;
            decimal monthlyInterestRate = (loan.Interest ?? 0) / 100 / 12;

            // Get the number of payments already made
            int paymentsMade = ((loan.RepayPeriod ?? 12) - (int)(loan.Balance / principalPerMonth));
            if (paymentsMade < 0) paymentsMade = 0;

            // Calculate missed payments starting from the next missed payment
            for (int i = 1; i <= monthsMissed; i++)
            {
                int paymentNumber = paymentsMade + i;
                if (paymentNumber > (loan.RepayPeriod ?? 12)) break;

                // Update remaining principal
                remainingPrincipal = (loan.LoanAmt ?? 0) - (principalPerMonth * paymentsMade);
                decimal interestForMonth = remainingPrincipal * monthlyInterestRate;
                decimal monthlyPayment = principalPerMonth + interestForMonth;

                totalMissed += monthlyPayment;
                paymentsMade++;
            }

            return totalMissed;
        }

        private decimal CalculateMissedAmountRbal(dynamic loan, int monthsMissed)
        {
            // Reducing Balance: Interest recalculated on remaining balance each month
            decimal remainingBalance = loan.Balance;
            decimal monthlyInterestRate = (loan.Interest ?? 0) / 100 / 12;
            decimal totalMissed = 0;

            // Calculate standard monthly payment using amortization
            decimal standardMonthlyPayment = CalculateMonthlyPaymentAmt(loan.LoanAmt ?? 0, loan.RepayPeriod ?? 12, loan.Interest ?? 0);

            for (int i = 1; i <= monthsMissed && remainingBalance > 0; i++)
            {
                decimal interestForMonth = remainingBalance * monthlyInterestRate;
                decimal principalForMonth = standardMonthlyPayment - interestForMonth;

                if (principalForMonth < 0) principalForMonth = standardMonthlyPayment * 0.5m;

                totalMissed += standardMonthlyPayment;
                remainingBalance -= principalForMonth;
            }

            return totalMissed;
        }


        /// <summary>
        /// Calculate Profit/Loss using Gltransaction table
        /// This is more accurate as it has DR/CR accounts
        /// </summary>
        private async Task<(decimal LastMonthProfitLoss, decimal ThisMonthProfitLoss)> CalculateProfitLossFromGltransactionAsync(string? companyCode, bool isSuperAdmin)
        {
            try
            {
                var currentDate = DateTime.Now;

                // Define current month range
                var startOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var endOfCurrentMonth = startOfCurrentMonth.AddMonths(1).AddDays(-1);

                // Define last month range
                var startOfLastMonth = startOfCurrentMonth.AddMonths(-1);
                var endOfLastMonth = startOfCurrentMonth.AddDays(-1);

                // Get income accounts (Revenue/Income type)
                var incomeAccounts = await _context.GlSetup
                    .Where(g => g.Glacctype == "INCOME" || g.Glacctype == "REVENUE" || g.GlAccMainGroup == "INCOME")
                    .Select(g => g.AccNo)
                    .ToListAsync();

                // Get expense accounts
                var expenseAccounts = await _context.GlSetup
                    .Where(g => g.Glacctype == "EXPENSE" || g.Glacctype == "COST" || g.GlAccMainGroup == "EXPENSE")
                    .Select(g => g.AccNo)
                    .ToListAsync();

                var query = _context.Gltransactions.AsQueryable();

                // Apply company filter
                if (!string.IsNullOrEmpty(companyCode))
                {
                    query = query.Where(gl => gl.CompanyCode == companyCode);
                }

                // Get transactions for last month
                var lastMonthTransactions = await query
                    .Where(gl => gl.TransDate >= startOfLastMonth && gl.TransDate <= endOfLastMonth)
                    .ToListAsync();

                // Get transactions for current month
                var thisMonthTransactions = await query
                    .Where(gl => gl.TransDate >= startOfCurrentMonth && gl.TransDate <= endOfCurrentMonth)
                    .ToListAsync();

                // Calculate Last Month Profit/Loss
                decimal lastMonthIncome = 0;
                decimal lastMonthExpenses = 0;

                foreach (var transaction in lastMonthTransactions)
                {
                    // Income: When amount is credited to income account (CrAccNo is income)
                    if (incomeAccounts.Contains(transaction.CrAccNo))
                    {
                        lastMonthIncome += transaction.Amount;
                    }
                    // Expense: When amount is debited to expense account (DrAccNo is expense)
                    else if (expenseAccounts.Contains(transaction.DrAccNo))
                    {
                        lastMonthExpenses += transaction.Amount;
                    }
                }

                // Calculate This Month Profit/Loss
                decimal thisMonthIncome = 0;
                decimal thisMonthExpenses = 0;

                foreach (var transaction in thisMonthTransactions)
                {
                    if (incomeAccounts.Contains(transaction.CrAccNo))
                    {
                        thisMonthIncome += transaction.Amount;
                    }
                    else if (expenseAccounts.Contains(transaction.DrAccNo))
                    {
                        thisMonthExpenses += transaction.Amount;
                    }
                }

                decimal lastMonthProfitLoss = lastMonthIncome - lastMonthExpenses;
                decimal thisMonthProfitLoss = thisMonthIncome - thisMonthExpenses;

                _logger.LogInformation($"=== PROFIT/LOSS (GLTRANSACTION) ===");
                _logger.LogInformation($"Last Month - Income: {lastMonthIncome:C}, Expenses: {lastMonthExpenses:C}, Profit: {lastMonthProfitLoss:C}");
                _logger.LogInformation($"This Month - Income: {thisMonthIncome:C}, Expenses: {thisMonthExpenses:C}, Profit: {thisMonthProfitLoss:C}");

                return (lastMonthProfitLoss, thisMonthProfitLoss);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating profit/loss from Gltransaction");
                return (0, 0);
            }
        }

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

        // ============================================================
        // CALCULATE LOAN COUNT STATISTICS
        // ============================================================
        private async Task<(int Total, int Women, int Men, int Others)> CalculateLoanCountDataAsync(string? companyCode)
        {
            try
            {
                var loansQuery = from l in _context.Loans
                                 join m in _context.Members on l.MemberNo equals m.MemberNo
                                 select new { l.LoanNo, l.Status, l.CompanyCode, m.Sex };

                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(x => x.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();

                int womenCount = 0;
                int menCount = 0;
                int othersCount = 0;

                foreach (var loan in loans)
                {
                    var gender = NormalizeGender(loan.Sex);
                    if (gender == "FEMALE")
                        womenCount++;
                    else if (gender == "MALE")
                        menCount++;
                    else
                        othersCount++;
                }

                int total = womenCount + menCount + othersCount;

                _logger.LogInformation($"Loan Count - Total: {total}, Women: {womenCount}, Men: {menCount}, Others: {othersCount}");

                return (total, womenCount, menCount, othersCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating loan count data");
                return (0, 0, 0, 0);
            }
        }

        // ============================================================
        // CALCULATE LOAN STATUS BREAKDOWN
        // ============================================================
        private async Task<(int Completed, int Uncompleted, int Overdue, int Active)> CalculateLoanStatusBreakdownAsync(string? companyCode)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;

                var loansQuery = _context.Loans.AsQueryable();
                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }

                var loans = await loansQuery
                    .Select(l => new { l.LoanNo, l.Status, l.MemberNo, l.AuditTime })
                    .ToListAsync();

                int completed = 0;
                int uncompleted = 0;
                int active = 0;
                int overdue = 0;

                var loanNos = loans.Select(l => l.LoanNo).ToList();
                var loanBalances = new Dictionary<string, decimal>();

                if (loanNos.Any())
                {
                    loanBalances = await _context.Loanbal
                        .Where(lb => loanNos.Contains(lb.LoanNo))
                        .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);
                }

                foreach (var loan in loans)
                {
                    // Completed loans: Status = Closed (7)
                    if (loan.Status == (int)Status.Closed)
                    {
                        completed++;
                        continue;
                    }

                    // Uncompleted: Status != Closed, != Rejected, != WrittenOff
                    if (loan.Status != (int)Status.Rejected && loan.Status != (int)Status.WrittenOff)
                    {
                        uncompleted++;
                    }

                    // Active loans: Status = Disbursed (6) or Endorsed (5)
                    if (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                    {
                        active++;

                        // Check if overdue (>30 days)
                        decimal balance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : 0;
                        if (balance > 0)
                        {
                            // Get last repayment date
                            var lastRepayment = await _context.Repay
                                .Where(r => r.LoanNo == loan.LoanNo && r.DateReceived.HasValue)
                                .OrderByDescending(r => r.DateReceived)
                                .Select(r => r.DateReceived)
                                .FirstOrDefaultAsync();

                            DateTime? lastPaymentDate = lastRepayment ?? loan.AuditTime;
                            int daysOverdue = 0;

                            if (lastPaymentDate.HasValue)
                            {
                                var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                                if (asAtDate > nextDueDate)
                                {
                                    daysOverdue = (asAtDate - nextDueDate).Days;
                                }
                            }

                            if (daysOverdue > 30)
                            {
                                overdue++;
                            }
                        }
                    }
                }

                _logger.LogInformation($"Loan Status - Completed: {completed}, Uncompleted: {uncompleted}, Active: {active}, Overdue: {overdue}");

                return (completed, uncompleted, overdue, active);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating loan status breakdown");
                return (0, 0, 0, 0);
            }
        }

        // ============================================================
        // CALCULATE LOAN STATUS BREAKDOWN BY GENDER
        // ============================================================
        private async Task<(int WomenCompleted, int MenCompleted, int OthersCompleted,
                            int WomenActive, int MenActive, int OthersActive,
                            int WomenOverdue, int MenOverdue, int OthersOverdue)>
            CalculateLoanStatusByGenderAsync(string? companyCode)
        {
            try
            {
                var asAtDate = DateTime.Now.Date;

                var loansQuery = from l in _context.Loans
                                 join m in _context.Members on l.MemberNo equals m.MemberNo
                                 select new { l.LoanNo, l.Status, l.MemberNo, l.AuditTime, m.Sex, l.CompanyCode };

                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(x => x.CompanyCode == companyCode);
                }

                var loans = await loansQuery.ToListAsync();

                int womenCompleted = 0, menCompleted = 0, othersCompleted = 0;
                int womenActive = 0, menActive = 0, othersActive = 0;
                int womenOverdue = 0, menOverdue = 0, othersOverdue = 0;

                var loanNos = loans.Select(l => l.LoanNo).ToList();
                var loanBalances = new Dictionary<string, decimal>();

                if (loanNos.Any())
                {
                    loanBalances = await _context.Loanbal
                        .Where(lb => loanNos.Contains(lb.LoanNo))
                        .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);
                }

                foreach (var loan in loans)
                {
                    var gender = NormalizeGender(loan.Sex);

                    // COMPLETED loans: Status = Closed (7)
                    if (loan.Status == (int)Status.Closed)
                    {
                        if (gender == "FEMALE") womenCompleted++;
                        else if (gender == "MALE") menCompleted++;
                        else othersCompleted++;
                        continue;
                    }

                    // ACTIVE loans: Status = Disbursed (6) or Endorsed (5)
                    if (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                    {
                        // Count active by gender
                        if (gender == "FEMALE") womenActive++;
                        else if (gender == "MALE") menActive++;
                        else othersActive++;

                        // Check if OVERDUE (>30 days)
                        decimal balance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : 0;
                        if (balance > 0)
                        {
                            var lastRepayment = await _context.Repay
                                .Where(r => r.LoanNo == loan.LoanNo && r.DateReceived.HasValue)
                                .OrderByDescending(r => r.DateReceived)
                                .Select(r => r.DateReceived)
                                .FirstOrDefaultAsync();

                            DateTime? lastPaymentDate = lastRepayment ?? loan.AuditTime;
                            int daysOverdue = 0;

                            if (lastPaymentDate.HasValue)
                            {
                                var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                                if (asAtDate > nextDueDate)
                                {
                                    daysOverdue = (asAtDate - nextDueDate).Days;
                                }
                            }

                            if (daysOverdue > 30)
                            {
                                if (gender == "FEMALE") womenOverdue++;
                                else if (gender == "MALE") menOverdue++;
                                else othersOverdue++;
                            }
                        }
                    }
                }

                _logger.LogInformation($"Loan Status by Gender - Women: Completed={womenCompleted}, Active={womenActive}, Overdue={womenOverdue}");
                _logger.LogInformation($"Loan Status by Gender - Men: Completed={menCompleted}, Active={menActive}, Overdue={menOverdue}");
                _logger.LogInformation($"Loan Status by Gender - Others: Completed={othersCompleted}, Active={othersActive}, Overdue={othersOverdue}");

                return (womenCompleted, menCompleted, othersCompleted,
                        womenActive, menActive, othersActive,
                        womenOverdue, menOverdue, othersOverdue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating loan status by gender");
                return (0, 0, 0, 0, 0, 0, 0, 0, 0);
            }
        }


        // ============================================================
        // CALCULATE LOANEE STATISTICS (Distinct Members with Loans)
        // ============================================================
        private async Task<(int Total, int Women, int Men, int Others)> CalculateLoaneesDataAsync(string? companyCode)
        {
            try
            {
                var loansQuery = _context.Loans.AsQueryable();

                if (!string.IsNullOrEmpty(companyCode))
                {
                    loansQuery = loansQuery.Where(l => l.CompanyCode == companyCode);
                }

                // Get distinct members who have taken loans (INNER JOIN with Members)
                var loaneesQuery = from l in loansQuery
                                   join m in _context.Members on l.MemberNo equals m.MemberNo
                                   select new { m.MemberNo, m.Sex };

                var loanees = await loaneesQuery
                    .GroupBy(x => new { x.MemberNo, x.Sex })
                    .Select(g => new { g.Key.MemberNo, g.Key.Sex })
                    .ToListAsync();

                int womenLoanees = 0;
                int menLoanees = 0;
                int othersLoanees = 0;

                foreach (var l in loanees)
                {
                    var gender = NormalizeGender(l.Sex);
                    if (gender == "FEMALE")
                        womenLoanees++;
                    else if (gender == "MALE")
                        menLoanees++;
                    else
                        othersLoanees++;
                }

                int total = womenLoanees + menLoanees + othersLoanees;

                _logger.LogInformation($"Loanees - Total: {total}, Women: {womenLoanees}, Men: {menLoanees}, Others: {othersLoanees}");

                return (total, womenLoanees, menLoanees, othersLoanees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating loanees data");
                return (0, 0, 0, 0);
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
                        return true;
                }
                else
                {
                    consecutiveCount = 1;
                }
            }

            return false;
        }

        private async Task<DashboardVM> CalculateFullDashboardDataAsync(string? companyCode, bool isSuperAdmin)
        {
            var dashboard = new DashboardVM();

            // Build the member filter - get members for this company
            var memberQuery = _context.Members.AsQueryable();
            if (!string.IsNullOrEmpty(companyCode))
                memberQuery = memberQuery.Where(m => m.CompanyCode == companyCode);

            // ============================================================
            // QUERY #1: Member statistics - Get ALL members for this company
            // ============================================================
            var memberStats = await memberQuery
                .Select(m => new { m.Id, m.MemberNo, m.Sex, m.Status, m.Dob, m.EffectDate, m.CompanyCode })
                .ToListAsync();

            // Store the member IDs for filtering other queries
            var memberIds = memberStats.Select(m => m.Id).ToList();
            var memberNos = memberStats.Select(m => m.MemberNo).Distinct().ToList();

            // ============================================================
            // NEW: Determine Active Status based on 3 consecutive months of deposits
            // ============================================================
            // Get all deposit dates for all members in one query
            var allDeposits = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                             && cs.CompanyCode == companyCode
                             && cs.DepositsAmount.HasValue
                             && cs.DepositsAmount.Value > 0)
                .Select(cs => new { cs.MemberNo, Date = cs.ContrDate/* ?? cs.DepositedDate ?? cs.ReceiptDate ?? cs.AuditTime*/ })
                .ToListAsync();

            // Group deposit dates by member
            var memberDeposits = allDeposits
                .Where(d => d.Date != null && d.Date != DateTime.MinValue)
                .GroupBy(d => d.MemberNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => d.Date.Value).ToList()
                );

            // Determine active members
            var activeMemberNos = new HashSet<string>();
            var dormantMemberNos = new HashSet<string>();

            foreach (var member in memberStats)
            {
                if (memberDeposits.TryGetValue(member.MemberNo, out var depositDates))
                {
                    if (IsMemberActive(depositDates))
                        activeMemberNos.Add(member.MemberNo);
                    else
                        dormantMemberNos.Add(member.MemberNo);
                }
                else
                {
                    dormantMemberNos.Add(member.MemberNo);
                }
            }

            // Set dashboard properties
            dashboard.TotalMembers = memberStats.Count;
            dashboard.TotalWomen = memberStats.Count(m => m.Sex?.ToUpper() == "FEMALE");
            dashboard.TotalMen = memberStats.Count(m => m.Sex?.ToUpper() == "MALE");
            dashboard.TotalOthers = memberStats.Count(m => m.Sex?.ToUpper() != "FEMALE" && m.Sex?.ToUpper() != "MALE");

            // Active members by gender
            dashboard.ActiveMembers = activeMemberNos.Count;
            dashboard.ActiveMembersByStatus = activeMemberNos.Count;
            dashboard.ActiveWomen = memberStats.Count(m => m.Sex?.ToUpper() == "FEMALE" && activeMemberNos.Contains(m.MemberNo));
            dashboard.ActiveMen = memberStats.Count(m => m.Sex?.ToUpper() == "MALE" && activeMemberNos.Contains(m.MemberNo));

            // Dormant members by gender
            dashboard.DormantMembers = dashboard.TotalMembers - dashboard.ActiveMembers;
            dashboard.DormantWomen = dashboard.TotalWomen - dashboard.ActiveWomen;
            dashboard.DormantMen = dashboard.TotalMen - dashboard.ActiveMen;

            // Youth statistics
            var today = DateTime.Today;
            var membersWithAge = memberStats.Where(m => m.Dob.HasValue).ToList();
            dashboard.YouthTotal = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35);
            dashboard.YouthMale = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35 && m.Sex?.ToUpper() == "MALE");
            dashboard.YouthFemale = membersWithAge.Count(m => CalculateAge(m.Dob.Value) <= 35 && m.Sex?.ToUpper() == "FEMALE");

            // ============================================================
            // QUERY #2: Financial aggregates from ContribShares
            // FIXED: Use BOTH MemberNo AND CompanyCode
            // ============================================================
            var financialStats = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
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
            // FIXED: Use BOTH MemberNo AND CompanyCode
            // ============================================================
            var genderFinancials = await (from cs in _context.ContribShares
                                          join m in _context.Members on new { cs.MemberNo, cs.CompanyCode } equals new { m.MemberNo, m.CompanyCode }
                                          where m.CompanyCode == companyCode
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
            // FIXED: Use CompanyCode in joins
            // ============================================================
            var loanStatsQuery = from c in _context.Cheques
                                 join l in _context.Loans on new { c.LoanNo, c.CompanyCode } equals new { l.LoanNo, l.CompanyCode }
                                 join m in _context.Members on new { l.MemberNo, l.CompanyCode } equals new { m.MemberNo, m.CompanyCode }
                                 where c.Amount > 0 && c.Amount != null
                                       && m.CompanyCode == companyCode
                                 select new { Amount = c.Amount.Value, Sex = m.Sex };

            var loanStats = await loanStatsQuery.ToListAsync();

            dashboard.TotalLoansTaken = loanStats.Sum(x => x.Amount);
            dashboard.WomenLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Amount);
            dashboard.MenLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Amount);
            dashboard.OthersLoansTaken = loanStats.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Amount);

            // ============================================================
            // QUERY #5: Loan balances from Loanbal table
            // FIXED: Use CompanyCode in joins
            // ============================================================
            var loanBalancesQuery = from lb in _context.Loanbal
                                    join m in _context.Members on new { MemberNo = lb.MemberNo, CompanyCode = lb.Companycode } equals new { m.MemberNo, m.CompanyCode }
                                    where m.CompanyCode == companyCode
                                    select new { lb.Balance, m.Sex };

            var loanBalances = await loanBalancesQuery.ToListAsync();

            dashboard.TotalLoanBalances = loanBalances.Sum(x => x.Balance);
            dashboard.WomenLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Balance);
            dashboard.MenLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Balance);
            dashboard.OthersLoanBalances = loanBalances.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Balance);

            // ============================================================
            // QUERY #6: Loans paid from Repay table
            // FIXED: Use CompanyCode in joins
            // ============================================================
            var loansPaidQuery = from r in _context.Repay
                                 join m in _context.Members on new { r.MemberNo, r.CompanyCode } equals new { m.MemberNo, m.CompanyCode }
                                 where r.Principal > 0 && r.Principal != null
                                       && m.CompanyCode == companyCode
                                 select new { Principal = r.Principal.Value, Sex = m.Sex };

            var loansPaid = await loansPaidQuery.ToListAsync();

            dashboard.TotalLoansPaid = loansPaid.Sum(x => x.Principal);
            dashboard.WomenLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() == "FEMALE").Sum(x => x.Principal);
            dashboard.MenLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() == "MALE").Sum(x => x.Principal);
            dashboard.OthersLoansPaid = loansPaid.Where(x => x.Sex?.ToUpper() != "FEMALE" && x.Sex?.ToUpper() != "MALE").Sum(x => x.Principal);

            // ============================================================
            // QUERY #7: Total loanees (distinct members with loans)
            // FIXED: Use CompanyCode
            // ============================================================
            var loaneesQuery = from l in _context.Loans
                               join m in _context.Members on new { l.MemberNo, l.CompanyCode } equals new { m.MemberNo, m.CompanyCode }
                               where m.CompanyCode == companyCode
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
            // FIXED: Use MemberNo AND CompanyCode
            // ============================================================
            dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions
                .Where(t => memberNos.Contains(t.MemberNo) && t.CompanyCode == companyCode)
                .CountAsync();

            dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions
                .Where(t => t.Status.ToUpper() == "PENDING" && memberNos.Contains(t.MemberNo) && t.CompanyCode == companyCode)
                .CountAsync();

            dashboard.BlocksCreatedToday = await _context.Blocks
                .Where(b => b.Timestamp.Date == DateTime.Today)
                .CountAsync();

            // ============================================================
            // QUERY #9: Grants from Gltransactions
            // ============================================================
            var grantsQuery = _context.Gltransactions.AsQueryable();

            if (!string.IsNullOrEmpty(companyCode))
                grantsQuery = grantsQuery.Where(g => g.CompanyCode == companyCode);

            dashboard.InclusionGrantTotal = await grantsQuery
                .Where(g => g.TransDescript != null && g.TransDescript.ToLower().Contains("inclusion grant"))
                .SumAsync(g => (decimal?)g.Amount) ?? 0;

            dashboard.MatchingGrantTotal = await grantsQuery
                .Where(g => g.TransDescript != null && g.TransDescript.ToLower().Contains("matching grant"))
                .SumAsync(g => (decimal?)g.Amount) ?? 0;

            // ============================================================
            // QUERY #10: Loan Metrics (PAR, Arrears, Repayment Rate, etc.)
            // FIXED: Use CompanyCode in all joins
            // ============================================================

            // Get all loans for members of this company
            var loans = await _context.Loans
                .Where(l => memberNos.Contains(l.MemberNo) && l.CompanyCode == companyCode)
                .Select(l => new { l.LoanNo, l.MemberNo, l.LoanAmt, l.Aamount, l.AuditTime, l.ApplicDate, l.CompanyCode, l.LoanCode })
                .ToListAsync();

            var loanNos = loans.Select(l => l.LoanNo).ToList();

            if (loanNos.Any())
            {
                // Get loan balances - using CompanyCode
                var loanBalancesDict = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                    .Select(lb => new { lb.LoanNo, lb.Balance, lb.LastDate })
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.LastDate });

                // Get loan disbursement dates - using CompanyCode
                var loanDisbursements = await _context.Cheques
                    .Where(c => loanNos.Contains(c.LoanNo) && c.CompanyCode == companyCode && c.Amount > 0)
                    .Select(c => new { c.LoanNo, c.DateIssued })
                    .ToDictionaryAsync(c => c.LoanNo, c => c.DateIssued);

                // Get latest repayments - using CompanyCode
                var latestRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastDateReceived = g.Max(r => r.DateReceived),
                        TotalPrincipal = g.Sum(r => r.Principal ?? 0),
                        TotalAmount = g.Sum(r => r.Amount ?? 0)
                    })
                    .ToDictionaryAsync(r => r.LoanNo, r => new { r.LastDateReceived, r.TotalPrincipal, r.TotalAmount });

                // Get current month repayments - using CompanyCode
                var currentDate = DateTime.Now;
                var startOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var endOfCurrentMonth = startOfCurrentMonth.AddMonths(1).AddDays(-1);

                var currentMonthRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo)
                        && r.CompanyCode == companyCode
                        && r.DateReceived.HasValue
                        && r.DateReceived >= startOfCurrentMonth
                        && r.DateReceived <= endOfCurrentMonth)
                    .GroupBy(r => r.CompanyCode)
                    .Select(g => new
                    {
                        CompanyCode = g.Key,
                        TotalReceived = g.Sum(r => r.Amount ?? 0)
                    })
                    .ToDictionaryAsync(r => r.CompanyCode, r => r.TotalReceived);

                var asAtDate = DateTime.Now.Date;
                decimal totalBalance = 0;
                decimal totalArrears30 = 0;
                decimal totalArrears60 = 0;
                decimal totalCurrentMonthReceived = 0;

                foreach (var loan in loans)
                {
                    decimal balance = 0;
                    DateTime? lastPaymentDate = null;
                    DateTime? disbursementDate = null;

                    if (loanBalancesDict.ContainsKey(loan.LoanNo))
                    {
                        balance = loanBalancesDict[loan.LoanNo].Balance;
                        var lastDateFromBal = loanBalancesDict[loan.LoanNo].LastDate;
                        if (lastDateFromBal != DateTime.MinValue)
                            lastPaymentDate = lastDateFromBal;
                    }
                    else
                    {
                        balance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    if (balance <= 0) continue;
                    totalBalance += balance;

                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastDateReceived ?? lastPaymentDate;
                    }

                    if (loanDisbursements.ContainsKey(loan.LoanNo))
                    {
                        disbursementDate = loanDisbursements[loan.LoanNo];
                    }

                    int daysInArrears = 0;
                    if (lastPaymentDate.HasValue && lastPaymentDate.Value != DateTime.MinValue)
                    {
                        var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                            daysInArrears = (asAtDate - nextDueDate).Days;
                    }
                    else if (disbursementDate.HasValue)
                    {
                        var firstDueDate = disbursementDate.Value.AddMonths(1);
                        if (asAtDate > firstDueDate)
                            daysInArrears = (asAtDate - firstDueDate).Days;
                    }
                    else
                    {
                        var firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                            daysInArrears = (asAtDate - firstDueDate).Days;
                    }

                    if (daysInArrears > 30)
                        totalArrears30 += balance;

                    if (daysInArrears > 60)
                        totalArrears60 += balance;

                    if (currentMonthRepayments.ContainsKey(loan.CompanyCode ?? ""))
                    {
                        totalCurrentMonthReceived += currentMonthRepayments[loan.CompanyCode ?? ""];
                    }
                }


                // Get Loanees Statistics (Distinct Members with Loans)
                var loaneesData = await CalculateLoaneesDataAsync(companyCode);
                dashboard.TotalLoanees = loaneesData.Total;
                dashboard.WomenLoanees = loaneesData.Women;
                dashboard.MenLoanees = loaneesData.Men;
                dashboard.OthersLoanees = loaneesData.Others;

                // Get Loan Count Statistics
                var loanCountData = await CalculateLoanCountDataAsync(companyCode);
                dashboard.TotalLoanCount = loanCountData.Total;
                dashboard.WomenLoanCount = loanCountData.Women;
                dashboard.MenLoanCount = loanCountData.Men;
                dashboard.OthersLoanCount = loanCountData.Others;

                // Get Loan Status Breakdown
                var statusBreakdown = await CalculateLoanStatusBreakdownAsync(companyCode);
                dashboard.CompletedLoansCount = statusBreakdown.Completed;
                dashboard.UncompletedLoansCount = statusBreakdown.Uncompleted;
                dashboard.OverdueLoansCount = statusBreakdown.Overdue;
                dashboard.ActiveLoansCount = statusBreakdown.Active;

                // Get Loan Status by Gender
                var statusByGender = await CalculateLoanStatusByGenderAsync(companyCode);
                dashboard.WomenCompletedLoans = statusByGender.WomenCompleted;
                dashboard.MenCompletedLoans = statusByGender.MenCompleted;
                dashboard.OthersCompletedLoans = statusByGender.OthersCompleted;
                dashboard.WomenActiveLoans = statusByGender.WomenActive;
                dashboard.MenActiveLoans = statusByGender.MenActive;
                dashboard.OthersActiveLoans = statusByGender.OthersActive;
                dashboard.WomenOverdueLoans = statusByGender.WomenOverdue;
                dashboard.MenOverdueLoans = statusByGender.MenOverdue;
                dashboard.OthersOverdueLoans = statusByGender.OthersOverdue;

                // Calculate metrics
                dashboard.PARPercent = totalBalance > 0 ? (totalArrears30 / totalBalance) * 100 : 0;
                dashboard.PAR60Percent = totalBalance > 0 ? (totalArrears60 / totalBalance) * 100 : 0;
                dashboard.ArrearsBalance = totalArrears30;
                dashboard.TotalArrears = totalArrears30;

                dashboard.AmountPastDueRate = dashboard.TotalLoanBalances > 0
                    ? (dashboard.TotalArrears / dashboard.TotalLoanBalances) * 100
                    : 0;

                dashboard.RepaymentRate = dashboard.TotalLoanBalances > 0
                    ? Math.Min((totalCurrentMonthReceived / dashboard.TotalLoanBalances) * 100, 100)
                    : 0;

                dashboard.OutstandingLoanPortfolio = dashboard.TotalLoanBalances;

                if (dashboard.PARPercent < 5) dashboard.LoanPortfolioHealth = "Excellent";
                else if (dashboard.PARPercent < 10) dashboard.LoanPortfolioHealth = "Good";
                else if (dashboard.PARPercent < 20) dashboard.LoanPortfolioHealth = "Fair";
                else dashboard.LoanPortfolioHealth = "At Risk";

                dashboard.WomenParticipationRate = dashboard.TotalMembers > 0 ? (dashboard.TotalWomen * 100m / dashboard.TotalMembers) : 0;
            }

            // ============================================================
            // QUERY #11: Recent transactions
            // FIXED: Use CompanyCode in joins
            // ============================================================
            var recentTxQuery = from t in _context.Transactions2
                                join m in _context.Members on new { t.MemberNo, CompanyCode = t.Companycode } equals new { m.MemberNo, m.CompanyCode }
                                where t.Status.ToUpper() == "COMPLETED" && m.CompanyCode == companyCode
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

            //dashboard.RecentTransactions = await recentTxQuery.Take(10).ToListAsync();

            // ============================================================
            // QUERY #12: Profit/Loss from Gltransaction
            // ============================================================
            var (lastMonthProfitLoss, thisMonthProfitLoss) = await CalculateProfitLossFromGltransactionAsync(companyCode, isSuperAdmin);

            dashboard.LastMonthProfitLoss = lastMonthProfitLoss;
            dashboard.ThisMonthProfitLoss = thisMonthProfitLoss;

            if (dashboard.LastMonthProfitLoss != 0)
            {
                dashboard.ProfitLossChange = ((dashboard.ThisMonthProfitLoss - dashboard.LastMonthProfitLoss) / Math.Abs(dashboard.LastMonthProfitLoss)) * 100;
            }
            else
            {
                dashboard.ProfitLossChange = dashboard.ThisMonthProfitLoss > 0 ? 100 : 0;
            }

            // ============================================================
            // QUERY #13: Quick Stats
            // FIXED: Use same property names in the anonymous type
            // ============================================================
            dashboard.QuickStats = new DashboardQuickStats
            {
                TransactionsToday = await _context.Transactions2
                    .Join(_context.Members,
                          t => new { t.MemberNo, CompanyCode = t.Companycode },
                          m => new { m.MemberNo, m.CompanyCode },
                          (t, m) => new { t, m })
                    .Where(x => x.t.ContributionDate.Date == DateTime.Today
                                && x.t.Status.ToUpper() == "COMPLETED"
                                && x.m.CompanyCode == companyCode)
                    .CountAsync(),

                NewMembersToday = await memberQuery
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == DateTime.Today),

                AverageDeposit = await _context.Transactions2
                    .Join(_context.Members,
                          t => new { t.MemberNo, CompanyCode = t.Companycode },
                          m => new { m.MemberNo, m.CompanyCode },
                          (t, m) => new { t, m })
                    .Where(x => x.t.TransactionType.ToUpper() == "DEPOSIT"
                                && x.t.Status.ToUpper() == "COMPLETED"
                                && x.m.CompanyCode == companyCode)
                    .AverageAsync(x => (decimal?)x.t.Amount) ?? 0,

                AverageLoan = await _context.Loans
                    .Join(_context.Members,
                          l => new { l.MemberNo, l.CompanyCode },
                          m => new { m.MemberNo, m.CompanyCode },
                          (l, m) => new { l, m })
                    .Where(x => x.l.Status == 1 && x.m.CompanyCode == companyCode)
                    .AverageAsync(x => (decimal?)x.l.LoanAmt) ?? 0,

                BlockchainUptime = 99.9m,
                LoanApprovalRate = 85.5m
            };

            // ============================================================
            // QUERY #14: Gender distribution
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



        // ============================================================
        // NEW: Get dashboard data for multiple companies (County Admin)
        // ============================================================
        public async Task<DashboardVM> GetDashboardDataForCompaniesAsync(List<string> companyCodes, bool isCountyAdmin)
        {
            if (companyCodes == null || !companyCodes.Any())
            {
                return new DashboardVM();
            }

            // Create a cache key that includes all company codes sorted
            var sortedCodes = companyCodes.OrderBy(c => c).ToList();
            var cacheKey = $"DashboardData_County_{string.Join("_", sortedCodes)}";

            if (_cache.TryGetValue(cacheKey, out DashboardVM cachedDashboard))
            {
                _logger.LogDebug($"Dashboard data from cache for county with {companyCodes.Count} companies");
                return cachedDashboard;
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedDashboard))
                    return cachedDashboard;

                _logger.LogInformation($"Calculating dashboard for county with {companyCodes.Count} companies");

                var dashboard = await CalculateFullDashboardDataForCompaniesAsync(companyCodes, isCountyAdmin);

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

        // ============================================================
        // NEW: Invalidate cache for a specific county
        // ============================================================
        public async Task InvalidateCacheForCountyAsync(string county)
        {
            // Get all company codes in this county
            var companyCodes = await _context.Companies
                .Where(c => c.County == county)
                .Select(c => c.CompanyCode)
                .ToListAsync();

            if (companyCodes.Any())
            {
                var sortedCodes = companyCodes.OrderBy(c => c).ToList();
                var cacheKey = $"DashboardData_County_{string.Join("_", sortedCodes)}";
                _cache.Remove(cacheKey);
                _logger.LogInformation($"Dashboard cache invalidated for county: {county}");
            }

            await Task.CompletedTask;
        }

        // ============================================================
        // NEW: Calculate dashboard data for multiple companies
        // ============================================================
        private async Task<DashboardVM> CalculateFullDashboardDataForCompaniesAsync(List<string> companyCodes, bool isCountyAdmin)
        {
            var dashboard = new DashboardVM();

            // Build member filter for all companies
            var memberQuery = _context.Members
                .Where(m => companyCodes.Contains(m.CompanyCode));

            // ============================================================
            // QUERY #1: Member statistics
            // ============================================================
            var memberStats = await memberQuery
                .Select(m => new { m.Sex, m.Status, m.MemberNo, m.Dob, m.EffectDate, m.CompanyCode })
                .ToListAsync();

            dashboard.TotalMembers = memberStats.Count;
            dashboard.TotalWomen = memberStats.Count(m => m.Sex?.ToUpper() == "FEMALE");
            dashboard.TotalMen = memberStats.Count(m => m.Sex?.ToUpper() == "MALE");
            dashboard.TotalOthers = memberStats.Count(m => m.Sex?.ToUpper() != "FEMALE" && m.Sex?.ToUpper() != "MALE");
            dashboard.ActiveMembers = memberStats.Count(m => m.Status == 1);
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
            var contribSharesQuery = _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && companyCodes.Contains(cs.CompanyCode));

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
                                          where memberNos.Contains(m.MemberNo) && companyCodes.Contains(m.CompanyCode)
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
                                 where c.Amount > 0 && c.Amount != null
                                       && memberNos.Contains(m.MemberNo)
                                       && companyCodes.Contains(m.CompanyCode)
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
                                    where memberNos.Contains(m.MemberNo) && companyCodes.Contains(m.CompanyCode)
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
                                 where r.Principal > 0 && r.Principal != null
                                       && memberNos.Contains(m.MemberNo)
                                       && companyCodes.Contains(m.CompanyCode)
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
                               where memberNos.Contains(m.MemberNo) && companyCodes.Contains(m.CompanyCode)
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
            dashboard.TotalBlockchainTransactions = await _context.BlockchainTransactions
                .Where(t => companyCodes.Contains(t.CompanyCode))
                .CountAsync();

            dashboard.PendingBlockchainTransactions = await _context.BlockchainTransactions
                .Where(t => companyCodes.Contains(t.CompanyCode) && t.Status.ToUpper() == "PENDING")
                .CountAsync();

            dashboard.BlocksCreatedToday = await _context.Blocks
                .Where(b => b.Timestamp.Date == DateTime.Today)
                .CountAsync();

            // ============================================================
            // QUERY #9: Grants from Gltransactions
            // ============================================================
            var grantsQuery = _context.Gltransactions
                .Where(g => companyCodes.Contains(g.CompanyCode));

            dashboard.InclusionGrantTotal = await grantsQuery
                .Where(g => g.TransDescript != null && g.TransDescript.ToLower().Contains("inclusion grant"))
                .SumAsync(g => (decimal?)g.Amount) ?? 0;

            dashboard.MatchingGrantTotal = await grantsQuery
                .Where(g => g.TransDescript != null && g.TransDescript.ToLower().Contains("matching grant"))
                .SumAsync(g => (decimal?)g.Amount) ?? 0;

            // ============================================================
            // QUERY #10: Loan Metrics (PAR, Arrears, Repayment Rate, etc.)
            // USING SQL APPROACH - NO Posted/Status Filters
            // ============================================================

            // Get all loans for these companies (NO Status filter)
            var loans = await _context.Loans
                .Where(l => companyCodes.Contains(l.CompanyCode))
                .Select(l => new { l.LoanNo, l.MemberNo, l.LoanAmt, l.Aamount, l.AuditTime, l.ApplicDate, l.CompanyCode, l.LoanCode })
                .ToListAsync();

            var loanNos = loans.Select(l => l.LoanNo).ToList();

            if (loanNos.Any())
            {
                // ============================================================
                // Get loan balances (NO Posted filter)
                // ============================================================
                var loanBalancesDict = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo) && companyCodes.Contains(lb.Companycode))
                    .Select(lb => new { lb.LoanNo, lb.Balance, lb.LastDate })
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.LastDate });

                // ============================================================
                // Get loan disbursement dates (NO filter)
                // ============================================================
                var loanDisbursements = await _context.Cheques
                    .Where(c => loanNos.Contains(c.LoanNo) && companyCodes.Contains(c.CompanyCode) && c.Amount > 0)
                    .Select(c => new { c.LoanNo, c.DateIssued })
                    .ToDictionaryAsync(c => c.LoanNo, c => c.DateIssued);

                // ============================================================
                // Get latest repayments (NO Posted filter)
                // ============================================================
                var latestRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo) && companyCodes.Contains(r.CompanyCode))
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LastDateReceived = g.Max(r => r.DateReceived),
                        TotalPrincipal = g.Sum(r => r.Principal ?? 0),
                        TotalAmount = g.Sum(r => r.Amount ?? 0)
                    })
                    .ToDictionaryAsync(r => r.LoanNo, r => new { r.LastDateReceived, r.TotalPrincipal, r.TotalAmount });

                // ============================================================
                // Get current month repayments (NO Posted filter)
                // ============================================================
                var currentDate = DateTime.Now;
                var startOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var endOfCurrentMonth = startOfCurrentMonth.AddMonths(1).AddDays(-1);

                var currentMonthRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo)
                        && companyCodes.Contains(r.CompanyCode)
                        && r.DateReceived.HasValue
                        && r.DateReceived >= startOfCurrentMonth
                        && r.DateReceived <= endOfCurrentMonth)
                    .GroupBy(r => r.CompanyCode)
                    .Select(g => new
                    {
                        CompanyCode = g.Key,
                        TotalReceived = g.Sum(r => r.Amount ?? 0)
                    })
                    .ToDictionaryAsync(r => r.CompanyCode, r => r.TotalReceived);

                var asAtDate = DateTime.Now.Date;
                decimal totalBalance = 0;
                decimal totalArrears30 = 0;
                decimal totalArrears60 = 0;
                decimal totalCurrentMonthReceived = 0;

                // ============================================================
                // Calculate PAR, Arrears, and other metrics per loan
                // ============================================================
                foreach (var loan in loans)
                {
                    decimal balance = 0;
                    DateTime? lastPaymentDate = null;
                    DateTime? disbursementDate = null;

                    // Get balance
                    if (loanBalancesDict.ContainsKey(loan.LoanNo))
                    {
                        balance = loanBalancesDict[loan.LoanNo].Balance;
                        var lastDateFromBal = loanBalancesDict[loan.LoanNo].LastDate;
                        if (lastDateFromBal != DateTime.MinValue)
                            lastPaymentDate = lastDateFromBal;
                    }
                    else
                    {
                        balance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Skip if balance is zero or negative
                    if (balance <= 0) continue;
                    totalBalance += balance;

                    // Get last payment date from repayments
                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastDateReceived ?? lastPaymentDate;
                    }

                    // Get disbursement date
                    if (loanDisbursements.ContainsKey(loan.LoanNo))
                    {
                        disbursementDate = loanDisbursements[loan.LoanNo];
                    }

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    if (lastPaymentDate.HasValue && lastPaymentDate.Value != DateTime.MinValue)
                    {
                        var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                            daysInArrears = (asAtDate - nextDueDate).Days;
                    }
                    else if (disbursementDate.HasValue)
                    {
                        var firstDueDate = disbursementDate.Value.AddMonths(1);
                        if (asAtDate > firstDueDate)
                            daysInArrears = (asAtDate - firstDueDate).Days;
                    }
                    else
                    {
                        var firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                            daysInArrears = (asAtDate - firstDueDate).Days;
                    }

                    // Accumulate arrears
                    if (daysInArrears > 30)
                        totalArrears30 += balance;

                    if (daysInArrears > 60)
                        totalArrears60 += balance;

                    // Accumulate current month received
                    if (currentMonthRepayments.ContainsKey(loan.CompanyCode))
                    {
                        totalCurrentMonthReceived += currentMonthRepayments[loan.CompanyCode];
                    }
                }

                // ============================================================
                // Calculate all metrics
                // ============================================================

                // PAR > 30
                dashboard.PARPercent = totalBalance > 0 ? (totalArrears30 / totalBalance) * 100 : 0;

                // PAR > 60
                dashboard.PAR60Percent = totalBalance > 0 ? (totalArrears60 / totalBalance) * 100 : 0;

                // Arrears Balance (Balance of loans with arrears > 30 days)
                dashboard.ArrearsBalance = totalArrears30;

                // Total Arrears > 30 Days
                dashboard.TotalArrears = totalArrears30;

                // Amount Past Due Rate = (Total Arrears / Total Loan Balance) * 100
                dashboard.AmountPastDueRate = dashboard.TotalLoanBalances > 0
                    ? (dashboard.TotalArrears / dashboard.TotalLoanBalances) * 100
                    : 0;

                // Repayment Rate (Current Month)
                dashboard.RepaymentRate = dashboard.TotalLoanBalances > 0
                    ? Math.Min((totalCurrentMonthReceived / dashboard.TotalLoanBalances) * 100, 100)
                    : 0;

                // Outstanding Loan Portfolio
                dashboard.OutstandingLoanPortfolio = dashboard.TotalLoanBalances;

                // ============================================================
                // QUERY #11: Profit/Loss from Gltransaction
                // ============================================================
                var (lastMonthProfitLoss, thisMonthProfitLoss) = await CalculateProfitLossFromGltransactionForCompaniesAsync(companyCodes);
                dashboard.LastMonthProfitLoss = lastMonthProfitLoss;
                dashboard.ThisMonthProfitLoss = thisMonthProfitLoss;

                if (dashboard.LastMonthProfitLoss != 0)
                {
                    dashboard.ProfitLossChange = ((dashboard.ThisMonthProfitLoss - dashboard.LastMonthProfitLoss) / Math.Abs(dashboard.LastMonthProfitLoss)) * 100;
                }
                else
                {
                    dashboard.ProfitLossChange = dashboard.ThisMonthProfitLoss > 0 ? 100 : 0;
                }

                // ============================================================
                // QUERY #12: Loan Portfolio Health
                // ============================================================
                if (dashboard.PARPercent < 5) dashboard.LoanPortfolioHealth = "Excellent";
                else if (dashboard.PARPercent < 10) dashboard.LoanPortfolioHealth = "Good";
                else if (dashboard.PARPercent < 20) dashboard.LoanPortfolioHealth = "Fair";
                else dashboard.LoanPortfolioHealth = "At Risk";

                // ============================================================
                // QUERY #13: Women Participation Rate
                // ============================================================
                dashboard.WomenParticipationRate = dashboard.TotalMembers > 0 ? (dashboard.TotalWomen * 100m / dashboard.TotalMembers) : 0;
            }

            // ============================================================
            // QUERY #14: Recent transactions
            // ============================================================
            var recentTxQuery = from t in _context.Transactions2
                                join m in _context.Members on t.MemberNo equals m.MemberNo
                                where t.Status.ToUpper() == "COMPLETED"
                                      && memberNos.Contains(m.MemberNo)
                                      && companyCodes.Contains(m.CompanyCode)
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
            // QUERY #15: Quick Stats
            // ============================================================
            dashboard.QuickStats = new DashboardQuickStats
            {
                TransactionsToday = await _context.Transactions2
                    .Where(t => t.ContributionDate.Date == DateTime.Today
                                && t.Status.ToUpper() == "COMPLETED")
                    .Join(_context.Members,
                          t => t.MemberNo,
                          m => m.MemberNo,
                          (t, m) => new { t, m.CompanyCode })
                    .Where(x => companyCodes.Contains(x.CompanyCode))
                    .CountAsync(),

                NewMembersToday = await memberQuery
                    .CountAsync(m => m.EffectDate.HasValue && m.EffectDate.Value.Date == DateTime.Today),

                AverageDeposit = await _context.Transactions2
                    .Where(t => t.TransactionType.ToUpper() == "DEPOSIT"
                                && t.Status.ToUpper() == "COMPLETED")
                    .Join(_context.Members,
                          t => t.MemberNo,
                          m => m.MemberNo,
                          (t, m) => new { t.Amount, m.CompanyCode })
                    .Where(x => companyCodes.Contains(x.CompanyCode))
                    .AverageAsync(x => (decimal?)x.Amount) ?? 0,

                AverageLoan = await _context.Loans
                    .Where(l => l.Status == 1 && companyCodes.Contains(l.CompanyCode))
                    .AverageAsync(l => (decimal?)l.LoanAmt) ?? 0,

                BlockchainUptime = 99.9m,
                LoanApprovalRate = 85.5m
            };

            // ============================================================
            // QUERY #16: Gender distribution
            // ============================================================
            dashboard.GenderStats = new GenderDistribution
            {
                MaleCount = dashboard.TotalMen,
                FemaleCount = dashboard.TotalWomen,
                OtherCount = dashboard.TotalOthers
            };

            // Set selected company name
            var firstCompany = await _context.Companies
                .FirstOrDefaultAsync(c => companyCodes.Contains(c.CompanyCode));

            if (firstCompany != null)
            {
                dashboard.SelectedCompanyName = $"County: {firstCompany.County}";
                dashboard.CountyName = firstCompany.County;
            }
            else
            {
                dashboard.SelectedCompanyName = "County View";
            }

            dashboard.IsCountyView = true;

            return dashboard;
        }

        // ============================================================
        // Helper: Calculate Profit/Loss for multiple companies
        // ============================================================
        private async Task<(decimal LastMonthProfitLoss, decimal ThisMonthProfitLoss)> CalculateProfitLossFromGltransactionForCompaniesAsync(List<string> companyCodes)
        {
            try
            {
                var currentDate = DateTime.Now;

                // Define current month range
                var startOfCurrentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var endOfCurrentMonth = startOfCurrentMonth.AddMonths(1).AddDays(-1);

                // Define last month range
                var startOfLastMonth = startOfCurrentMonth.AddMonths(-1);
                var endOfLastMonth = startOfCurrentMonth.AddDays(-1);

                // Get income accounts (Revenue/Income type)
                var incomeAccounts = await _context.GlSetup
                    .Where(g => g.Glacctype == "INCOME" || g.Glacctype == "REVENUE" || g.GlAccMainGroup == "INCOME")
                    .Select(g => g.AccNo)
                    .ToListAsync();

                // Get expense accounts
                var expenseAccounts = await _context.GlSetup
                    .Where(g => g.Glacctype == "EXPENSE" || g.Glacctype == "COST" || g.GlAccMainGroup == "EXPENSE")
                    .Select(g => g.AccNo)
                    .ToListAsync();

                var query = _context.Gltransactions
                    .Where(gl => companyCodes.Contains(gl.CompanyCode));

                // Get transactions for last month
                var lastMonthTransactions = await query
                    .Where(gl => gl.TransDate >= startOfLastMonth && gl.TransDate <= endOfLastMonth)
                    .ToListAsync();

                // Get transactions for current month
                var thisMonthTransactions = await query
                    .Where(gl => gl.TransDate >= startOfCurrentMonth && gl.TransDate <= endOfCurrentMonth)
                    .ToListAsync();

                // Calculate Last Month Profit/Loss
                decimal lastMonthIncome = 0;
                decimal lastMonthExpenses = 0;

                foreach (var transaction in lastMonthTransactions)
                {
                    if (incomeAccounts.Contains(transaction.CrAccNo))
                        lastMonthIncome += transaction.Amount;
                    else if (expenseAccounts.Contains(transaction.DrAccNo))
                        lastMonthExpenses += transaction.Amount;
                }

                // Calculate This Month Profit/Loss
                decimal thisMonthIncome = 0;
                decimal thisMonthExpenses = 0;

                foreach (var transaction in thisMonthTransactions)
                {
                    if (incomeAccounts.Contains(transaction.CrAccNo))
                        thisMonthIncome += transaction.Amount;
                    else if (expenseAccounts.Contains(transaction.DrAccNo))
                        thisMonthExpenses += transaction.Amount;
                }

                decimal lastMonthProfitLoss = lastMonthIncome - lastMonthExpenses;
                decimal thisMonthProfitLoss = thisMonthIncome - thisMonthExpenses;

                _logger.LogInformation($"Profit/Loss - Last Month: {lastMonthProfitLoss:C}, This Month: {thisMonthProfitLoss:C}");

                return (lastMonthProfitLoss, thisMonthProfitLoss);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating profit/loss for companies");
                return (0, 0);
            }
        }
    }
}