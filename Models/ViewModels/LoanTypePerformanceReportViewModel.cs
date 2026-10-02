using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class LoanTypePerformanceReportViewModel
	{
		public string LoanType { get; set; }
		public string LoanCode { get; set; }
		public decimal Amount { get; set; }           // Total amount issued
		public decimal TotalPrincipalBalance { get; set; }  // Total outstanding balance
		public decimal TotalArrears { get; set; }     // Total arrears for this loan type
		public decimal ParPercentage { get; set; }    // Portfolio at Risk percentage

		// Additional details
		public int TotalLoans { get; set; }
		public int ActiveLoans { get; set; }
		public int OverdueLoans { get; set; }
		public decimal UnpaidInterest { get; set; }
	}

	public class LoanTypePerformanceIndexViewModel
	{
		public List<LoanTypePerformanceReportViewModel> LoanTypes { get; set; } = new List<LoanTypePerformanceReportViewModel>();

		// Overall Totals
		public decimal TotalAmount { get; set; }
		public decimal TotalPrincipalBalance { get; set; }
		public decimal TotalArrears { get; set; }
		public decimal OverallParPercentage { get; set; }

		// Statistics
		public int TotalLoanTypes { get; set; }
		public int TotalLoans { get; set; }
		public int TotalActiveLoans { get; set; }
		public int TotalOverdueLoans { get; set; }

		// Report Parameters
		public DateTime AsAtDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}