using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class PortfolioAtRiskReportViewModel
	{
		public decimal LoanIssued { get; set; }
		public decimal UnpaidInterest { get; set; }
		public decimal LoanBalance { get; set; }
		public decimal Arrears { get; set; }
		public decimal ParPercentage { get; set; }

		// Additional details for breakdown
		public string MemberNo { get; set; }
		public string Names { get; set; }
		public string LoanNo { get; set; }
		public string LoanName { get; set; }
		public decimal PrincipalPaid { get; set; }
		public decimal InterestPaid { get; set; }
		public int DaysOverdue { get; set; }
	}

	public class PortfolioAtRiskIndexViewModel
	{
		public List<PortfolioAtRiskReportViewModel> Loans { get; set; } = new List<PortfolioAtRiskReportViewModel>();

		// Totals
		public decimal TotalLoanIssued { get; set; }
		public decimal TotalUnpaidInterest { get; set; }
		public decimal TotalLoanBalance { get; set; }
		public decimal TotalArrears { get; set; }
		public decimal ParPercentage { get; set; }

		// Statistics
		public int TotalLoans { get; set; }
		public int ActiveLoans { get; set; }
		public int OverdueLoans { get; set; }

		// Aging Breakdown (from Aging Analysis)
		public int PerformingCount { get; set; }
		public int SpecialMentionCount { get; set; }
		public int WatchfulCount { get; set; }
		public int SubstandardCount { get; set; }
		public int DoubtfulCount { get; set; }
		public int LossCount { get; set; }
		public int LossOver365Count { get; set; }

		// Report Parameters
		public DateTime AsAtDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}