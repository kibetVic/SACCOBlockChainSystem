using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;

using SACCOBlockChainSystem.Models.ViewModels;

namespace SACCOBlockChainSystem.Services
{
    public interface ICountyReportService
    {
        // Contribution Reports
        Task<CountyReportViewModel> GetCountyReportAsync(string countyName, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<CountyReportViewModel>> GetAllCountyReportsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<CompanyContributionSummary> GetCompanyContributionSummaryAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<string>> GetAllCountiesAsync();

        Task<Company> GetCompanyByCodeAsync(string companyCode);

        // Loans Reports
        Task<CountyLoansReportViewModel> GetCountyLoansReportAsync(string countyName, DateTime? asAtDate = null);
        Task<List<CountyLoansReportViewModel>> GetAllCountyLoansReportsAsync(DateTime? asAtDate = null);
    }

    public class CountyReportService : ICountyReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CountyReportService> _logger;

        public CountyReportService(ApplicationDbContext context, ILogger<CountyReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // CONTRIBUTION REPORT
        // ============================================================

        public async Task<CountyReportViewModel> GetCountyReportAsync(string countyName, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddMonths(-12);
                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                // ============================================================
                // SQL: Get all companies in the county
                // SELECT CompanyCode, CompanyName FROM Companies 
                // WHERE County LIKE '%Bomet%' AND Project = 1
                // ============================================================
                var companies = await _context.Companies
                    .Where(c => c.County != null && c.County.ToLower().Contains(countyName.ToLower()))
                    .Select(c => new { c.CompanyCode, c.CompanyName })
                    .ToListAsync();

                if (!companies.Any())
                {
                    return new CountyReportViewModel
                    {
                        CountyName = countyName,
                        ReportDate = DateTime.Now,
                        StartDate = startDate,
                        EndDate = endDate,
                        TotalCompanies = 0,
                        TotalMembers = 0,
                        TotalContributions = 0,
                        CompanySummaries = new List<CompanyContributionSummary>()
                    };
                }

                var companyCodes = companies.Select(c => c.CompanyCode).ToList();

                // ============================================================
                // SQL: Get all members in these companies
                // SELECT MemberNo, Sex, CompanyCode FROM Members 
                // WHERE CompanyCode IN ('COMP001', 'COMP002', ...)
                // ============================================================
                var members = await _context.Members
                    .Where(m => companyCodes.Contains(m.CompanyCode))
                    .Select(m => new { m.MemberNo, m.Sex, m.CompanyCode, m.ShareCap, m.RegFee })
                    .ToListAsync();

                var memberNos = members.Select(m => m.MemberNo).ToList();

                // ============================================================
                // SQL: Get contributions from ContribShares table
                // SELECT MemberNo, CompanyCode, 
                //        SUM(ShareCapitalAmount) AS ShareCapital,
                //        SUM(DepositsAmount) AS Deposits,
                //        SUM(RegFeeAmount) AS RegFee
                // FROM ContribShares 
                // WHERE MemberNo IN (...) AND CompanyCode IN (...)
                // GROUP BY MemberNo, CompanyCode
                // ============================================================
                var contribSharesData = await _context.ContribShares
                    .Where(cs => memberNos.Contains(cs.MemberNo) && companyCodes.Contains(cs.CompanyCode))
                    .GroupBy(cs => new { cs.MemberNo, cs.CompanyCode })
                    .Select(g => new
                    {
                        g.Key.MemberNo,
                        g.Key.CompanyCode,
                        ShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                        Deposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                        RegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                    })
                    .ToListAsync();

                // Build a lookup dictionary for contributions by company and member
                var contribLookup = contribSharesData
                    .GroupBy(x => x.CompanyCode)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(x => x.MemberNo, x => new { x.ShareCapital, x.Deposits, x.RegFee })
                    );

                // Build company summaries
                var companySummaries = new List<CompanyContributionSummary>();

                foreach (var comp in companies)  // FIX: Changed from 'company' to 'comp' to avoid CS0136
                {
                    var companyMembers = members.Where(m => m.CompanyCode == comp.CompanyCode).ToList();

                    // FIX: Use proper type instead of dynamic
                    Dictionary<string, dynamic> companyContribs = null;
                    if (contribLookup.ContainsKey(comp.CompanyCode))
                    {
                        // Convert to dictionary with dynamic values
                        companyContribs = contribLookup[comp.CompanyCode]
                            .ToDictionary(
                                kvp => kvp.Key,
                                kvp => new { kvp.Value.ShareCapital, kvp.Value.Deposits, kvp.Value.RegFee } as dynamic
                            );
                    }
                    else
                    {
                        companyContribs = new Dictionary<string, dynamic>();
                    }

                    // Calculate totals per company
                    decimal totalShareCapital = 0;
                    decimal totalDeposits = 0;
                    decimal totalRegFees = 0;
                    decimal totalAll = 0;

                    // Gender breakdown
                    decimal femaleTotal = 0;
                    decimal maleTotal = 0;
                    decimal otherTotal = 0;
                    int femaleCount = 0;
                    int maleCount = 0;
                    int otherCount = 0;

                    foreach (var member in companyMembers)
                    {
                        var memberKey = member.MemberNo ?? "";
                        decimal shareCapital = 0;
                        decimal deposits = 0;
                        decimal regFee = 0;

                        if (companyContribs != null && companyContribs.ContainsKey(memberKey))
                        {
                            var contrib = companyContribs[memberKey];
                            shareCapital = contrib.ShareCapital;
                            deposits = contrib.Deposits;
                            regFee = contrib.RegFee;
                        }

                        var total = shareCapital + deposits + regFee;
                        totalShareCapital += shareCapital;
                        totalDeposits += deposits;
                        totalRegFees += regFee;
                        totalAll += total;

                        // Gender breakdown
                        var gender = NormalizeGender(member.Sex);
                        if (gender == "FEMALE")
                        {
                            femaleTotal += total;
                            femaleCount++;
                        }
                        else if (gender == "MALE")
                        {
                            maleTotal += total;
                            maleCount++;
                        }
                        else
                        {
                            otherTotal += total;
                            otherCount++;
                        }
                    }

                    companySummaries.Add(new CompanyContributionSummary
                    {
                        CompanyCode = comp.CompanyCode,
                        CompanyName = comp.CompanyName ?? comp.CompanyCode,
                        MemberCount = companyMembers.Count,
                        TotalContributions = totalAll,
                        TotalShareCapital = totalShareCapital,
                        TotalDeposits = totalDeposits,
                        TotalRegistrationFees = totalRegFees,
                        AverageContributionPerMember = companyMembers.Any() ? totalAll / companyMembers.Count : 0,
                        FemaleContributions = femaleTotal,
                        MaleContributions = maleTotal,
                        OtherContributions = otherTotal,
                        FemaleCount = femaleCount,
                        MaleCount = maleCount,
                        OtherCount = otherCount,
                        WomenPercentage = totalAll > 0 ? (femaleTotal / totalAll) * 100 : 0,
                        MenPercentage = totalAll > 0 ? (maleTotal / totalAll) * 100 : 0,
                        OtherPercentage = totalAll > 0 ? (otherTotal / totalAll) * 100 : 0,
                        PercentageOfTotal = 0
                    });
                }

                // Calculate totals
                var totalContributions = companySummaries.Sum(c => c.TotalContributions);
                var totalMembers = members.Count;
                var totalCompanies = companies.Count;

                // Calculate percentages
                foreach (var summary in companySummaries)
                {
                    summary.PercentageOfTotal = totalContributions > 0
                        ? (summary.TotalContributions / totalContributions) * 100
                        : 0;
                }

                // Build gender summary
                var genderSummary = new GenderContributionSummary
                {
                    FemaleTotal = companySummaries.Sum(c => c.FemaleContributions),
                    MaleTotal = companySummaries.Sum(c => c.MaleContributions),
                    OtherTotal = companySummaries.Sum(c => c.OtherContributions),
                    FemaleCount = companySummaries.Sum(c => c.FemaleCount),
                    MaleCount = companySummaries.Sum(c => c.MaleCount),
                    OtherCount = companySummaries.Sum(c => c.OtherCount)
                };

                var totalGenderContrib = genderSummary.FemaleTotal + genderSummary.MaleTotal + genderSummary.OtherTotal;
                genderSummary.FemalePercentage = totalGenderContrib > 0 ? (genderSummary.FemaleTotal / totalGenderContrib) * 100 : 0;
                genderSummary.MalePercentage = totalGenderContrib > 0 ? (genderSummary.MaleTotal / totalGenderContrib) * 100 : 0;
                genderSummary.OtherPercentage = totalGenderContrib > 0 ? (genderSummary.OtherTotal / totalGenderContrib) * 100 : 0;

                return new CountyReportViewModel
                {
                    CountyName = countyName,
                    ReportDate = DateTime.Now,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalCompanies = totalCompanies,
                    TotalMembers = totalMembers,
                    TotalContributions = totalContributions,
                    TotalShareCapital = companySummaries.Sum(c => c.TotalShareCapital),
                    TotalDeposits = companySummaries.Sum(c => c.TotalDeposits),
                    TotalRegistrationFees = companySummaries.Sum(c => c.TotalRegistrationFees),
                    CompanySummaries = companySummaries.OrderByDescending(c => c.TotalContributions).ToList(),
                    GenderSummary = genderSummary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating county report for {countyName}");
                throw;
            }
        }

        public async Task<List<CountyReportViewModel>> GetAllCountyReportsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var counties = await _context.Companies
                    .Where(c => c.County != null && c.County != "")
                    .Select(c => c.County)
                    .Distinct()
                    .ToListAsync();

                var reports = new List<CountyReportViewModel>();

                foreach (var county in counties)
                {
                    var report = await GetCountyReportAsync(county, startDate, endDate);
                    reports.Add(report);
                }

                return reports.OrderBy(r => r.CountyName).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating all county reports");
                throw;
            }
        }

        public async Task<CompanyContributionSummary> GetCompanyContributionSummaryAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                if (company == null)
                    return null;

                var members = await _context.Members
                    .Where(m => m.CompanyCode == companyCode)
                    .ToListAsync();

                var memberNos = members.Select(m => m.MemberNo).ToList();

                var contribShares = await _context.ContribShares
                    .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                    .GroupBy(cs => cs.MemberNo)
                    .Select(g => new
                    {
                        MemberNo = g.Key,
                        ShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                        Deposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                        RegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                    })
                    .ToListAsync();

                var contribDict = contribShares.ToDictionary(x => x.MemberNo);

                decimal totalAll = 0;
                decimal femaleTotal = 0;
                decimal maleTotal = 0;
                decimal otherTotal = 0;
                int femaleCount = 0;
                int maleCount = 0;
                int otherCount = 0;

                foreach (var member in members)
                {
                    decimal shareCapital = 0;
                    decimal deposits = 0;
                    decimal regFee = 0;

                    if (contribDict.ContainsKey(member.MemberNo))
                    {
                        var c = contribDict[member.MemberNo];
                        shareCapital = c.ShareCapital;
                        deposits = c.Deposits;
                        regFee = c.RegFee;
                    }

                    var total = shareCapital + deposits + regFee;
                    totalAll += total;

                    var gender = NormalizeGender(member.Sex);
                    if (gender == "FEMALE")
                    {
                        femaleTotal += total;
                        femaleCount++;
                    }
                    else if (gender == "MALE")
                    {
                        maleTotal += total;
                        maleCount++;
                    }
                    else
                    {
                        otherTotal += total;
                        otherCount++;
                    }
                }

                return new CompanyContributionSummary
                {
                    CompanyCode = companyCode,
                    CompanyName = company.CompanyName ?? companyCode,
                    MemberCount = members.Count,
                    TotalContributions = totalAll,
                    TotalShareCapital = contribShares.Sum(c => c.ShareCapital),
                    TotalDeposits = contribShares.Sum(c => c.Deposits),
                    TotalRegistrationFees = contribShares.Sum(c => c.RegFee),
                    AverageContributionPerMember = members.Any() ? totalAll / members.Count : 0,
                    FemaleContributions = femaleTotal,
                    MaleContributions = maleTotal,
                    OtherContributions = otherTotal,
                    FemaleCount = femaleCount,
                    MaleCount = maleCount,
                    OtherCount = otherCount,
                    PercentageOfTotal = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company contribution summary for {companyCode}");
                throw;
            }
        }

        public async Task<List<string>> GetAllCountiesAsync()
        {
            try
            {
                return await _context.Companies
                    .Where(c => c.County != null && c.County != "")
                    .Select(c => c.County)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all counties");
                return new List<string>();
            }
        }

        public async Task<Company> GetCompanyByCodeAsync(string companyCode)
        {
            try
            {
                return await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company by code: {companyCode}");
                return null;
            }
        }

        private string NormalizeGender(string gender)
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


        // ============================================================
        // COUNTY LOANS REPORT
        // ============================================================

        public async Task<CountyLoansReportViewModel> GetCountyLoansReportAsync(string countyName, DateTime? asAtDate = null)
        {
            try
            {
                if (!asAtDate.HasValue)
                    asAtDate = DateTime.Now.Date;

                // Get all companies in the county
                var companies = await _context.Companies
                    .Where(c => c.County != null && c.County.ToLower().Contains(countyName.ToLower()))
                    .Select(c => new { c.CompanyCode, c.CompanyName })
                    .ToListAsync();

                if (!companies.Any())
                {
                    return new CountyLoansReportViewModel
                    {
                        CountyName = countyName,
                        ReportDate = DateTime.Now,
                        AsAtDate = asAtDate,
                        TotalCompanies = 0,
                        SaccoSummaries = new List<SaccoLoanSummary>()
                    };
                }

                var companyCodes = companies.Select(c => c.CompanyCode).ToList();

                // Build the report using the SQL approach
                var saccoSummaries = new List<SaccoLoanSummary>();
                decimal grandTotalBalance = 0;
                decimal grandTotalArrears30 = 0;
                decimal grandTotalArrears60 = 0;
                decimal grandTotalArrears90 = 0;
                int grandTotalLoansCount = 0;
                decimal grandTotalLoansPaid = 0;

                foreach (var company in companies)
                {
                    var summary = await GetSaccoLoanSummaryOptimizedAsync(company.CompanyCode, asAtDate.Value);
                    saccoSummaries.Add(summary);
                    grandTotalBalance += summary.TotalLoanBalance;
                    grandTotalArrears30 += summary.TotalArrears30;
                    grandTotalArrears60 += summary.TotalArrears60;
                    grandTotalArrears90 += summary.TotalArrears90;
                    grandTotalLoansCount += summary.NumberOfLoans;
                    grandTotalLoansPaid += summary.TotalLoansPaid;
                }

                decimal overallPAR30 = grandTotalBalance > 0 ? (grandTotalArrears30 / grandTotalBalance) * 100 : 0;
                decimal overallPAR60 = grandTotalBalance > 0 ? (grandTotalArrears60 / grandTotalBalance) * 100 : 0;
                decimal overallPAR90 = grandTotalBalance > 0 ? (grandTotalArrears90 / grandTotalBalance) * 100 : 0;
                decimal overallAmountPastDueRate = grandTotalBalance > 0 ? (grandTotalArrears30 / grandTotalBalance) * 100 : 0;

                // Calculate percentages for each company
                foreach (var summary in saccoSummaries)
                {
                    summary.PercentageOfTotal = grandTotalBalance > 0
                        ? (summary.TotalLoanBalance / grandTotalBalance) * 100
                        : 0;
                }

                return new CountyLoansReportViewModel
                {
                    CountyName = countyName,
                    ReportDate = DateTime.Now,
                    AsAtDate = asAtDate,
                    TotalCompanies = companies.Count,
                    TotalLoans = grandTotalLoansCount,
                    TotalLoanAmount = saccoSummaries.Sum(s => s.TotalLoanAmount),
                    TotalLoanBalance = grandTotalBalance,
                    TotalLoansPaid = grandTotalLoansPaid,
                    OverallAmountPastDueRate = overallAmountPastDueRate,
                    OverallPAR30 = overallPAR30,
                    OverallPAR60 = overallPAR60,
                    OverallPAR90 = overallPAR90,
                    SaccoSummaries = saccoSummaries.OrderByDescending(s => s.TotalLoanBalance).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating county loans report for {countyName}");
                throw;
            }
        }

        /// <summary>
        /// Optimized method to get SACCO loan summary using batch loading
        /// </summary>
        private async Task<SaccoLoanSummary> GetSaccoLoanSummaryOptimizedAsync(string companyCode, DateTime asAtDate)
        {
            try
            {
                // ============================================================
                // STEP 1: Get ALL loans for this company (no status filter)
                // ============================================================
                var loans = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode)
                    .Select(l => new
                    {
                        l.LoanNo,
                        l.MemberNo,
                        l.LoanAmt,
                        l.Aamount,
                        l.AuditTime,
                        l.ApplicDate,
                        l.CompanyCode,
                        l.Status,
                        l.LoanCode
                    })
                    .ToListAsync();

                int numberOfLoans = loans.Count;

                if (numberOfLoans == 0)
                {

                    return new SaccoLoanSummary
                    {
                        CompanyCode = companyCode,
                        CompanyName = (await _context.Companies
                        .FirstOrDefaultAsync(c => c.CompanyCode == companyCode))?.CompanyName ?? companyCode,
                        NumberOfLoans = 0,
                        TotalLoanAmount = 0,
                        TotalLoanBalance = 0,
                        AmountPastDueRate = 0,
                        PAR30 = 0,
                        PAR60 = 0,
                        PAR90 = 0,
                        LoansInArrears30 = 0,
                        LoansInArrears60 = 0,
                        LoansInArrears90 = 0,
                        TotalArrears30 = 0,
                        TotalArrears60 = 0,
                        TotalArrears90 = 0,
                        TotalLoansPaid = 0,
                        PortfolioHealth = "No Loans"
                    };
                }

                var loanNos = loans.Select(l => l.LoanNo).ToList();

                // ============================================================
                // STEP 2: Get loan balances from Loanbal (batch load)
                // ============================================================
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                    .Select(lb => new { lb.LoanNo, lb.Balance, lb.LastDate })
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.LastDate });

                // ============================================================
                // STEP 3: Get loan disbursement dates from Cheques (batch load)
                // ============================================================
                var loanDisbursements = await _context.Cheques
                    .Where(c => loanNos.Contains(c.LoanNo) && c.CompanyCode == companyCode && c.Amount > 0)
                    .Select(c => new { c.LoanNo, c.DateIssued, c.Amount })
                    .ToDictionaryAsync(c => c.LoanNo, c => new { c.DateIssued, c.Amount });

                // ============================================================
                // STEP 4: Get latest repayments (batch load)
                // ============================================================
                var latestRepayments = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Principal > 0)
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
                // STEP 5: Get loan types for grace period (batch load)
                // ============================================================
                var loanCodes = loans.Where(l => l.LoanCode != null).Select(l => l.LoanCode).Distinct().ToList();
                var loanTypes = await _context.Loantypes
                    .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                    .Select(lt => new { lt.LoanCode, lt.GracePeriod, lt.RepayPeriod, lt.Interest, lt.Penalty, lt.Repaymethod })
                    .ToDictionaryAsync(lt => lt.LoanCode, lt => new { lt.GracePeriod, lt.RepayPeriod, lt.Interest, lt.Penalty, lt.Repaymethod });

                // ============================================================
                // STEP 6: Calculate PAR metrics for each loan
                // ============================================================
                decimal totalBalance = 0;
                decimal totalArrears30 = 0;
                decimal totalArrears60 = 0;
                decimal totalArrears90 = 0;
                int loansInArrears30 = 0;
                int loansInArrears60 = 0;
                int loansInArrears90 = 0;
                decimal totalLoansPaid = 0;

                foreach (var loan in loans)
                {
                    decimal balance = 0;
                    DateTime? lastPaymentDate = null;
                    DateTime? disbursementDate = null;
                    decimal loanAmount = loan.LoanAmt ?? loan.Aamount ?? 0;

                    // Get balance from Loanbal
                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        balance = loanBalances[loan.LoanNo].Balance;
                        var lastDateFromBal = loanBalances[loan.LoanNo].LastDate;
                        if (lastDateFromBal != DateTime.MinValue)
                            lastPaymentDate = lastDateFromBal;
                    }
                    else
                    {
                        balance = loanAmount;
                    }

                    // Get disbursement date
                    if (loanDisbursements.ContainsKey(loan.LoanNo))
                    {
                        disbursementDate = loanDisbursements[loan.LoanNo].DateIssued;
                    }
                    else
                    {
                        disbursementDate = loan.AuditTime;
                    }

                    // Get last payment date from repayments
                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        var repay = latestRepayments[loan.LoanNo];
                        lastPaymentDate = repay.LastDateReceived ?? lastPaymentDate;
                        totalLoansPaid += repay.TotalPrincipal;
                    }

                    // Get grace period from loan type
                    int gracePeriod = 0;
                    if (loan.LoanCode != null && loanTypes.ContainsKey(loan.LoanCode))
                    {
                        gracePeriod = loanTypes[loan.LoanCode].GracePeriod;
                    }

                    // Calculate days in arrears
                    int daysInArrears = CalculateDaysInArrearsWithGracePeriod(
                        asAtDate,
                        lastPaymentDate,
                        disbursementDate ?? loan.AuditTime,
                        gracePeriod
                    );

                    // Add to totals only if balance > 0
                    if (balance > 0)
                    {
                        totalBalance += balance;

                        if (daysInArrears > 30)
                        {
                            totalArrears30 += balance;
                            loansInArrears30++;
                        }
                        if (daysInArrears > 60)
                        {
                            totalArrears60 += balance;
                            loansInArrears60++;
                        }
                        if (daysInArrears > 90)
                        {
                            totalArrears90 += balance;
                            loansInArrears90++;
                        }
                    }
                }

                // Calculate PAR percentages
                decimal par30 = totalBalance > 0 ? (totalArrears30 / totalBalance) * 100 : 0;
                decimal par60 = totalBalance > 0 ? (totalArrears60 / totalBalance) * 100 : 0;
                decimal par90 = totalBalance > 0 ? (totalArrears90 / totalBalance) * 100 : 0;
                decimal amountPastDueRate = totalBalance > 0 ? (totalArrears30 / totalBalance) * 100 : 0;

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                return new SaccoLoanSummary
                {
                    CompanyCode = companyCode,
                    CompanyName = company?.CompanyName ?? companyCode,
                    NumberOfLoans = numberOfLoans,
                    TotalLoanAmount = loans.Sum(l => l.LoanAmt ?? l.Aamount ?? 0),
                    TotalLoanBalance = totalBalance,
                    AmountPastDueRate = amountPastDueRate,
                    PAR30 = par30,
                    PAR60 = par60,
                    PAR90 = par90,
                    LoansInArrears30 = loansInArrears30,
                    LoansInArrears60 = loansInArrears60,
                    LoansInArrears90 = loansInArrears90,
                    TotalArrears30 = totalArrears30,
                    TotalArrears60 = totalArrears60,
                    TotalArrears90 = totalArrears90,
                    TotalLoansPaid = totalLoansPaid,
                    PortfolioHealth = GetPortfolioHealth(par30)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting SACCO loan summary for {companyCode}");
                throw;
            }
        }

        /// <summary>
        /// Calculate days in arrears with grace period consideration
        /// </summary>
        private int CalculateDaysInArrearsWithGracePeriod(DateTime asAtDate, DateTime? lastPaymentDate, DateTime loanDisbursementDate, int gracePeriod)
        {
            if (lastPaymentDate.HasValue && lastPaymentDate.Value != DateTime.MinValue)
            {
                // Calculate next due date = last payment date + 1 month
                var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                if (asAtDate > nextDueDate)
                {
                    var daysOverdue = (asAtDate - nextDueDate).Days;
                    // Subtract grace period if applicable
                    return daysOverdue > gracePeriod ? daysOverdue - gracePeriod : 0;
                }
                return 0;
            }
            else
            {
                // No payments made yet - check if first payment is due
                DateTime firstDueDate = loanDisbursementDate.AddMonths(1);
                if (asAtDate > firstDueDate)
                {
                    var daysOverdue = (asAtDate - firstDueDate).Days;
                    return daysOverdue > gracePeriod ? daysOverdue - gracePeriod : 0;
                }
                return 0;
            }
        }

        public async Task<List<CountyLoansReportViewModel>> GetAllCountyLoansReportsAsync(DateTime? asAtDate = null)
        {
            try
            {
                if (!asAtDate.HasValue)
                    asAtDate = DateTime.Now.Date;

                var counties = await _context.Companies
                    .Where(c => c.County != null && c.County != "")
                    .Select(c => c.County)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                var reports = new List<CountyLoansReportViewModel>();

                foreach (var county in counties)
                {
                    var report = await GetCountyLoansReportAsync(county, asAtDate);
                    reports.Add(report);
                }

                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating all county loans reports");
                throw;
            }
        }

        private string GetPortfolioHealth(decimal parPercent)
        {
            if (parPercent < 5) return "Excellent";
            if (parPercent < 10) return "Good";
            if (parPercent < 20) return "Fair";
            return "At Risk";
        }
    }
}