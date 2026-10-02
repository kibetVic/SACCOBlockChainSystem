using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    // ============================================================
    // CONTRIBUTION REPORT
    // ============================================================
    public class CountyReportViewModel
    {
        public string CountyName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalMembers { get; set; }
        public decimal TotalContributions { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalRegistrationFees { get; set; }
        public List<CompanyContributionSummary> CompanySummaries { get; set; } = new List<CompanyContributionSummary>();
        public GenderContributionSummary GenderSummary { get; set; } = new GenderContributionSummary();
        public bool IsLoading { get; set; } = false;
        public string CacheKey { get; set; }

        // For layout display
        public bool IsCountyView { get; set; } = true;
        public List<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();
        public string SelectedCompanyName { get; set; }
        public string SelectedCompanyCode { get; set; }
        public string UserGroup { get; set; }
        public List<string> UserRoles { get; set; } = new List<string>();
    }

    public class CompanyContributionSummary
    {
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalContributions { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalRegistrationFees { get; set; }
        public decimal AverageContributionPerMember { get; set; }
        public decimal FemaleContributions { get; set; }
        public decimal MaleContributions { get; set; }
        public decimal OtherContributions { get; set; }
        public int FemaleCount { get; set; }
        public int MaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public decimal WomenPercentage { get; set; }
        public decimal MenPercentage { get; set; }
        public decimal OtherPercentage { get; set; }
    }

    public class GenderContributionSummary
    {
        public decimal FemaleTotal { get; set; }
        public decimal MaleTotal { get; set; }
        public decimal OtherTotal { get; set; }
        public int FemaleCount { get; set; }
        public int MaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal FemalePercentage { get; set; }
        public decimal MalePercentage { get; set; }
        public decimal OtherPercentage { get; set; }
    }



    // ============================================================
    // LOANS REPORT
    // ============================================================
    public class CountyLoansReportViewModel
    {
        public string CountyName { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime? AsAtDate { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalLoansPaid { get; set; }  // NEW
        public decimal OverallAmountPastDueRate { get; set; }
        public decimal OverallPAR30 { get; set; }
        public decimal OverallPAR60 { get; set; }
        public decimal OverallPAR90 { get; set; }  // NEW
        public List<SaccoLoanSummary> SaccoSummaries { get; set; } = new List<SaccoLoanSummary>();
        public bool IsLoading { get; set; } = false;
        public string CacheKey { get; set; }
        public bool IsCountyView { get; set; } = false;
        public string SelectedCompanyName { get; set; }
        public string SelectedCompanyCode { get; set; }
        public List<CompanyInfo> Companies { get; set; } = new List<CompanyInfo>();
        public string UserGroup { get; set; }
        public List<string> UserRoles { get; set; } = new List<string>();
    }

    public class SaccoLoanSummary
    {
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public int NumberOfLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal AmountPastDueRate { get; set; }
        public decimal PAR30 { get; set; }
        public decimal PAR60 { get; set; }
        public decimal PAR90 { get; set; }  // NEW
        public int LoansInArrears30 { get; set; }
        public int LoansInArrears60 { get; set; }
        public int LoansInArrears90 { get; set; }  // NEW
        public decimal TotalArrears30 { get; set; }
        public decimal TotalArrears60 { get; set; }
        public decimal TotalArrears90 { get; set; }  // NEW
        public decimal TotalLoansPaid { get; set; }  // NEW
        public decimal PercentageOfTotal { get; set; }
        public string PortfolioHealth { get; set; }
    }
}