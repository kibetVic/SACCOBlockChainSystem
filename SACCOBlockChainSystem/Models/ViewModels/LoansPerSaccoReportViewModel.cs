using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class LoansPerSaccoReportViewModel
	{
		// Loan Information
		public string MemberNo { get; set; }
		public string LoanNo { get; set; }
		public string FullName { get; set; }
		public string LoanCode { get; set; }
		public DateTime? ApplicDate { get; set; }
		public int? RepayPeriod { get; set; }
		public decimal? LoanAmt { get; set; }
		public decimal? Balance { get; set; }
		public string LoanStatus { get; set; }

		// Additional Info
		public decimal? PrincipalPaid { get; set; }
		public decimal? InterestPaid { get; set; }
		public decimal? TotalPaid { get; set; }
		public DateTime? LastPaymentDate { get; set; }
	}

	public class LoansPerSaccoIndexViewModel
	{
		// Completed Loans (Zero Balance)
		public List<LoansPerSaccoReportViewModel> CompletedLoans { get; set; } = new List<LoansPerSaccoReportViewModel>();

		// Incomplete Loans (With Balance)
		public List<LoansPerSaccoReportViewModel> IncompleteLoans { get; set; } = new List<LoansPerSaccoReportViewModel>();

		// Statistics
		public int TotalCompletedLoans { get; set; }
		public int TotalIncompleteLoans { get; set; }
		public int TotalLoans { get; set; }

		// Financial Totals
		public decimal TotalCompletedLoanAmount { get; set; }
		public decimal TotalIncompleteLoanAmount { get; set; }
		public decimal TotalOutstandingBalance { get; set; }
		public decimal TotalLoanAmount { get; set; }

		// Report Information
		public DateTime ReportDate { get; set; }
		public bool HasData { get; set; }
		public string UserCompanyCode { get; set; }
		public string CompanyName { get; set; }
	}
}