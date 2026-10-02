using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class LoanRecoveryReportViewModel
	{
		public string LoanType { get; set; }
		public string LoanCode { get; set; }
		public DateTime? TransactionDate { get; set; }
		public string MemberNo { get; set; }
		public string LoanNo { get; set; }
		public string Names { get; set; }
		public decimal Principal { get; set; }
		public decimal Interest { get; set; }
		public decimal Amount { get; set; }
		public decimal Accrued { get; set; }
		public decimal LoanBalance { get; set; }
		public string ReceiptNo { get; set; }
		public DateTime? DueDate { get; set; }
		public int InstallmentNo { get; set; }
	}

	public class LoanRecoveryGroupViewModel
	{
		public string LoanType { get; set; }
		public string LoanCode { get; set; }
		public List<LoanRecoveryReportViewModel> Repayments { get; set; } = new List<LoanRecoveryReportViewModel>();

		// Group Totals
		public decimal TotalPrincipal { get; set; }
		public decimal TotalInterest { get; set; }
		public decimal TotalAmount { get; set; }
		public decimal TotalAccrued { get; set; }
		public int TotalTransactions { get; set; }
	}

	public class LoanRecoveryIndexViewModel
	{
		public List<LoanRecoveryGroupViewModel> Groups { get; set; } = new List<LoanRecoveryGroupViewModel>();

		// Overall Totals
		public decimal TotalPrincipal { get; set; }
		public decimal TotalInterest { get; set; }
		public decimal TotalAmount { get; set; }
		public decimal TotalAccrued { get; set; }
		public int TotalTransactions { get; set; }
		public int TotalLoanTypes { get; set; }

		// Report Parameters
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}