using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class PeriodicLoanDefaulterViewModel
	{
		public string MemberNo { get; set; }
		public string Names { get; set; }
		public string LoanNo { get; set; }
		public decimal RepayRate { get; set; }
		public decimal Paid { get; set; }
		public decimal Expected { get; set; }
		public decimal DefAmount { get; set; }
		public string DPeriod { get; set; }
		public string PhoneNo { get; set; }
		public string LoanType { get; set; }
		public DateTime? DateIssued { get; set; }
		public DateTime? LastPaymentDate { get; set; }
		public int DaysOverdue { get; set; }
	}

	public class PeriodicLoanDefaultersIndexViewModel
	{
		public List<PeriodicLoanDefaulterViewModel> Defaulters { get; set; } = new List<PeriodicLoanDefaulterViewModel>();

		// Report Type: General or Region
		public string ReportType { get; set; } = "General";
		public string SelectedRegion { get; set; }

		// Totals
		public decimal GrandDefaultTotal { get; set; }
		public decimal TotalRepayRate { get; set; }
		public decimal TotalPaid { get; set; }
		public decimal TotalExpected { get; set; }
		public int TotalDefaulters { get; set; }

		// Report Parameters
		public DateTime AsAtDate { get; set; }
		public bool HasData { get; set; }
		public string CompanyName { get; set; }
		public string PrintedBy { get; set; }
		public DateTime GeneratedOn { get; set; }

		// Region options (for dropdown)
		public List<string> Regions { get; set; } = new List<string>();
	}
}