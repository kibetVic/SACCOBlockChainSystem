using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class DelinquentLoansReportViewModel
	{
		public string SaccoName { get; set; }
		public int NoOfDefaulters { get; set; }
		public decimal ExpectedAmount { get; set; }
		public decimal CollectedAmount { get; set; }
		public decimal DefaultedAmount { get; set; }
		public decimal RatePercentage { get; set; }

		// Detailed information
		public List<DelinquentLoanDetail> Defaulters { get; set; } = new List<DelinquentLoanDetail>();
	}

	public class DelinquentLoanDetail
	{
		public string MemberNo { get; set; }
		public string Names { get; set; }
		public string LoanNo { get; set; }
		public string LoanType { get; set; }
		public DateTime DateIssued { get; set; }
		public decimal LoanAmount { get; set; }
		public decimal OutstandingBalance { get; set; }
		public decimal ExpectedRepayment { get; set; }
		public decimal AmountCollected { get; set; }
		public decimal DefaultedAmount { get; set; }
		public int DaysOverdue { get; set; }
		public DateTime LastPaymentDate { get; set; }
	}

	public class DelinquentLoansIndexViewModel
	{
		public List<DelinquentLoansReportViewModel> DelinquentLoans { get; set; } = new List<DelinquentLoansReportViewModel>();

		// Overall Totals
		public int TotalDefaulters { get; set; }
		public decimal TotalExpectedAmount { get; set; }
		public decimal TotalCollectedAmount { get; set; }
		public decimal TotalDefaultedAmount { get; set; }
		public decimal OverallRatePercentage { get; set; }

		// Report Parameters
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}