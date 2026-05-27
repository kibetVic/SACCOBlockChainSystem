using SACCOBlockChainSystem.Models.DTOs;
using System;

namespace SACCOBlockChainSystem.Models.ViewModels
{
	public class LoanIssuedReportViewModel
	{
		public int No { get; set; }
		public string MemberNo { get; set; }
		public string LoanNo { get; set; }
		public string Name { get; set; }
		public DateTime? ApplicationDate { get; set; }
		public DateTime? AppraisalDate { get; set; }
		public DateTime? EndorsementDate { get; set; }
		public DateTime? DateIssued { get; set; }
		public int LoanPeriodMonths { get; set; }
		public decimal LoanApplied { get; set; }
		public decimal ApprovedAmount { get; set; }
		public decimal? InterestRate { get; set; }
		public string LoanType { get; set; }
	}

    public class RepayViewModel
    {
        public LoanRepaymentDTO RepaymentDto { get; set; } = new LoanRepaymentDTO();
        public List<LoanSummaryDTO> ActiveLoans { get; set; } = new List<LoanSummaryDTO>();
        public List<GlSetup> GlAccounts { get; set; } = new List<GlSetup>();
    }
}
