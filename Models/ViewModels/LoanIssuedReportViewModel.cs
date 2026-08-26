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

    #region Completed Loans Report ViewModels

    public class CompletedLoansReportViewModel
    {
        public string MemberNo { get; set; }
        public string Names { get; set; }
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public string Gender { get; set; }
        public string IDNo { get; set; }
        public string PhoneNo { get; set; }
        public decimal AmountIssued { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalPrincipal { get; set; }
        public decimal TotalInterest { get; set; }
        public decimal PrincipalAmount { get; set; } 
        public decimal InterestAmount { get; set; }
        public decimal Balance { get; set; }
        public int PaymentCount { get; set; }
        public DateTime? DateIssued { get; set; }
        public DateTime? DateCompleted { get; set; }
        public int RepayPeriod { get; set; }
        public decimal? InterestRate { get; set; }
    }

    public class CompletedLoansIndexViewModel
    {
        public List<CompletedLoansReportViewModel> Loans { get; set; } = new List<CompletedLoansReportViewModel>();
        public int TotalLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public int TotalWomen { get; set; }
        public int TotalMen { get; set; }
        public int TotalOthers { get; set; }
        public DateTime AsAtDate { get; set; }
        public bool HasData { get; set; }
        public string CompanyName { get; set; }
        public string PrintedBy { get; set; }
        public DateTime GeneratedOn { get; set; }
    }

    #endregion

    #region Active Loans Report ViewModels

    public class ActiveLoansReportViewModel
    {
        public string MemberNo { get; set; }
        public string Names { get; set; }
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public string Gender { get; set; }
        public string IDNo { get; set; }
        public string PhoneNo { get; set; }
        public decimal AmountIssued { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal UnpaidInterest { get; set; }
        public decimal Penalty { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalPrincipal { get; set; }
        public decimal TotalInterest { get; set; }
        public int PaymentCount { get; set; }
        public DateTime? DateIssued { get; set; }
        public int RepayPeriod { get; set; }
        public decimal? InterestRate { get; set; }
        public int DaysOverdue { get; set; }
        public string LoanStatus { get; set; }
    }

    public class ActiveLoansIndexViewModel
    {
        public List<ActiveLoansReportViewModel> Loans { get; set; } = new List<ActiveLoansReportViewModel>();
        public int TotalLoans { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalAmountIssued { get; set; }
        public int TotalWomen { get; set; }
        public int TotalMen { get; set; }
        public int TotalOthers { get; set; }
        public DateTime AsAtDate { get; set; }
        public bool HasData { get; set; }
        public string CompanyName { get; set; }
        public string PrintedBy { get; set; }
        public DateTime GeneratedOn { get; set; }
    }

    #endregion
}


