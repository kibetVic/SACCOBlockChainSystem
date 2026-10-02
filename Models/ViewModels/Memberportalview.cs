using System.Collections.Generic;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Models.ViewModels
{

    /// <summary>
    /// ViewModel for Share Contributions Statement
    /// </summary>
    public class ShareContributionsStatementViewModel
    {
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string MemberPhone { get; set; }
        public DateTime ReportDate { get; set; }
        public List<ShareTypeGroupDto> ShareTypeGroups { get; set; } = new();
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalTransactions { get; set; }
        public int ActiveShareTypes { get; set; }
        public bool HasData { get; set; }
    }

    /// <summary>
    /// DTO for Share Type Group
    /// </summary>
    public class ShareTypeGroupDto
    {
        public string ShareCode { get; set; }
        public string ShareTypeName { get; set; }
        public bool IsMainShares { get; set; }
        public bool IsShareCapital { get; set; }
        public int Priority { get; set; }
        public List<ShareContributionDetailDto> Contributions { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
    }

    /// <summary>
    /// DTO for Individual Share Contribution
    /// </summary>
    public class ShareContributionDetailDto
    {
        public int Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public decimal ShareBalance { get; set; }
        public string Description { get; set; }
        public string ReceiptNo { get; set; }
        public string TransactionNo { get; set; }
        public string ChequeNo { get; set; }
        public string RefNo { get; set; }
        public string Posted { get; set; }
        public string Status { get; set; }
    }

    // Guarantor Loans ViewModel
    public class MemberGuarantorLoansViewModel
    {
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string MemberPhone { get; set; }
        public int TotalGuaranteedLoans { get; set; }
        public int ActiveGuarantees { get; set; }
        public int OverdueGuarantees { get; set; }
        public decimal TotalGuaranteeAmount { get; set; }
        public decimal TotalRemainingGuarantee { get; set; }
        public List<MemberGuarantorLoanDetailDto> GuaranteedLoans { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
    }

    public class MemberGuarantorLoanDetailDto
    {
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public string LoaneeMemberNo { get; set; }
        public string LoaneeName { get; set; }
        public string LoaneePhone { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public decimal RemainingGuarantee { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public string LoanStatus { get; set; }
        public DateTime GuaranteeDate { get; set; }
        public string GuaranteeStatus { get; set; }
    }

    // Member Loans ViewModel
    public class MemberLoansViewModel
    {
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string MemberPhone { get; set; }
        public int TotalLoans { get; set; }
        public int ActiveLoans { get; set; }
        public int ClosedLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalOutstanding { get; set; }
        public List<MemberLoanDetailDto> Loans { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
    }

    public class MemberLoanDetailDto
    {
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal UnpaidInterest { get; set; }
        public decimal Penalty { get; set; }
        public decimal TotalOutstanding { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
        public int RepaymentPeriod { get; set; }
        public decimal InterestRate { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public int RepaymentCount { get; set; }
        public bool IsFullyPaid { get; set; }
    }

    // Loan Repayments ViewModel
    public class LoanRepaymentsViewModel
    {
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string MemberPhone { get; set; }
        public int TotalLoans { get; set; }
        public int TotalRepayments { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public decimal TotalPrincipalPaid { get; set; }
        public decimal TotalInterestPaid { get; set; }
        public decimal TotalPenaltyPaid { get; set; }
        public List<LoanRepaymentGroupDto> LoanGroups { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
    }

    public class LoanRepaymentGroupDto
    {
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public decimal TotalPrincipalPaid { get; set; }
        public decimal TotalInterestPaid { get; set; }
        public decimal TotalPenaltyPaid { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public int RepaymentCount { get; set; }
        public List<LoanRepaymentDetailDto> Repayments { get; set; } = new();
    }

    public class LoanRepaymentDetailDto
    {
        public DateTime? TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Penalty { get; set; }
        public decimal LoanBalance { get; set; }
        public string ReceiptNo { get; set; }
        public string TransactionNo { get; set; }
        public string Description { get; set; }
    }
}
