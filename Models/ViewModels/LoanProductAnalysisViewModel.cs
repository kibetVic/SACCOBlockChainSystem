using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class LoanProductAnalysisDetailViewModel
	{
		public string MemberNo { get; set; }
		public string Names { get; set; }
		public decimal UnpaidInterest { get; set; }
		public decimal LoanIssued { get; set; }
		public DateTime? DateIssued { get; set; }
		public decimal LoanBalance { get; set; }
		public decimal Arrears { get; set; }
		public decimal ParPercentage { get; set; }
		public string LoanNo { get; set; }
	}

	public class LoanProductAnalysisGroupViewModel
	{
		public string LoanType { get; set; }
		public string LoanCode { get; set; }

		// Summary for this loan type
		public int NoOfLoanees { get; set; }
		public decimal TotalUnpaidInterest { get; set; }
		public decimal TotalLoanIssued { get; set; }
		public decimal TotalLoanBalance { get; set; }
		public decimal TotalArrears { get; set; }
		public decimal ParPercentage { get; set; }

		// Detailed loans
		public List<LoanProductAnalysisDetailViewModel> Loans { get; set; } = new List<LoanProductAnalysisDetailViewModel>();
	}

	public class LoanProductAnalysisIndexViewModel
	{
		public List<LoanProductAnalysisGroupViewModel> LoanProducts { get; set; } = new List<LoanProductAnalysisGroupViewModel>();

		// Overall Totals
		public int TotalNoOfLoanees { get; set; }
		public decimal TotalUnpaidInterest { get; set; }
		public decimal TotalLoanIssued { get; set; }
		public decimal TotalLoanBalance { get; set; }
		public decimal TotalArrears { get; set; }
		public decimal OverallParPercentage { get; set; }
		public int TotalLoanTypes { get; set; }

		// Report Parameters
		public DateTime AsAtDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}