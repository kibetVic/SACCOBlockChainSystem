using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class FullyPaidSharesReportViewModel
    {
        // Member Information
        public string MemberNo { get; set; }
        public string FullName { get; set; }
        public string Sex { get; set; }

        // Financial Information
        public decimal ShareCapital { get; set; }
        public decimal SavingsDeposits { get; set; }
        public decimal RegistrationFee { get; set; }

        // Status
        public bool IsFullyPaid { get; set; }
        public decimal MinimumShareRequirement { get; set; }
        public decimal MinimumSavingsRequirement { get; set; }
        public decimal RegistrationFeeRequirement { get; set; }
    }

    public class FullyPaidSharesIndexViewModel
    {
        public List<FullyPaidSharesReportViewModel> Members { get; set; } = new List<FullyPaidSharesReportViewModel>();

        // Statistics
        public int TotalMembers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherCount { get; set; }

        // Financial Totals
        public decimal TotalShareCapital { get; set; }
        public decimal TotalSavingsDeposits { get; set; }
        public decimal TotalRegistrationFee { get; set; }

        // Report Information
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
        public string UserCompanyCode { get; set; }
        public string CompanyName { get; set; }

        // Requirements
        public decimal MinimumShareRequirement { get; set; }
        public decimal RegistrationFeeRequirement { get; set; }
    }

    // Dynamic ViewModel for Fully Paid Shares with multiple share types
    public class FullyPaidSharesDynamicViewModel
    {
        public List<Dictionary<string, object>> Members { get; set; } = new List<Dictionary<string, object>>();
        public List<Sharetype> ShareTypes { get; set; } = new List<Sharetype>();
        public int TotalMembers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalSavingsDeposits { get; set; }
        public decimal TotalRegistrationFee { get; set; }
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
        public string UserCompanyCode { get; set; }
        public string CompanyName { get; set; }
        public decimal MinimumShareRequirement { get; set; }
        public decimal RegistrationFeeRequirement { get; set; }
    }
}