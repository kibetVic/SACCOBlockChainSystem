using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class GeneralLedgerReportViewModel
	{
		public DateTime TransDate { get; set; }
		public string Source { get; set; }
		public string Description { get; set; }
		public string ChequeNo { get; set; }
		public decimal Debits { get; set; }
		public decimal Credits { get; set; }
		public decimal AccBal { get; set; }
		public string DocumentNo { get; set; }
		public string TransactionNo { get; set; }
		public string DrAccNo { get; set; }
		public string CrAccNo { get; set; }
		public string AccountNo { get; set; }
		public string AccountName { get; set; }
	}

	public class GeneralLedgerGroupViewModel
	{
		public string AccountNo { get; set; }
		public string AccountName { get; set; }
		public List<GeneralLedgerReportViewModel> Transactions { get; set; } = new List<GeneralLedgerReportViewModel>();

		// Group Totals
		public decimal TotalDebits { get; set; }
		public decimal TotalCredits { get; set; }
		public decimal ClosingBalance { get; set; }
		public int TransactionCount { get; set; }
	}

	public class GeneralLedgerIndexViewModel
	{
		public List<GeneralLedgerGroupViewModel> Groups { get; set; } = new List<GeneralLedgerGroupViewModel>();

		// Overall Totals
		public decimal TotalDebits { get; set; }
		public decimal TotalCredits { get; set; }
		public int TotalTransactions { get; set; }
		public int TotalAccounts { get; set; }

		// Report Parameters
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }
	}
}