using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    // Original ViewModel for non-dynamic version (kept for backward compatibility)
    public class PartiallyPaidSharesReportViewModel
    {
        public string MemberNo { get; set; }
        public string FullName { get; set; }
        public string Sex { get; set; }
        public decimal ShareCapital { get; set; }
        public decimal SavingsDeposits { get; set; }
        public decimal RegistrationFee { get; set; }
        public bool HasPaidShareCapital { get; set; }
        public bool HasPaidRegistrationFee { get; set; }
        public bool HasSavingsDeposits { get; set; }
        public bool MissingShareCapital { get; set; }
        public bool MissingRegistrationFee { get; set; }
        public bool MissingSavingsDeposits { get; set; }
    }

    public class PartiallyPaidSharesIndexViewModel
    {
        public List<PartiallyPaidSharesReportViewModel> Members { get; set; } = new List<PartiallyPaidSharesReportViewModel>();
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
        public int MembersMissingShareCapital { get; set; }
        public int MembersMissingRegistrationFee { get; set; }
        public int MembersMissingSavings { get; set; }
    }

    // NEW: Dynamic ViewModel for Partially Paid Shares
    public class PartiallyPaidSharesDynamicViewModel
    {
        public List<Dictionary<string, object>> Members { get; set; } = new List<Dictionary<string, object>>();
        public List<Sharetype> ShareTypes { get; set; } = new List<Sharetype>();
        public int TotalMembers { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
        public string UserCompanyCode { get; set; }
        public string CompanyName { get; set; }
        public decimal MinimumShareRequirement { get; set; }
        public int MembersWithZeroShareCapital { get; set; }
        public int MembersWithZeroSavings { get; set; }
        public int MembersWithZeroRegFee { get; set; }
    }
}

