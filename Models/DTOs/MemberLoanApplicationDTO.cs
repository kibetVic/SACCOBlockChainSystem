// Models/DTOs/MemberLoanApplicationDTO.cs
using System.ComponentModel.DataAnnotations;
using System;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class WithdrawalMethodDTO
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool RequiresPhoneNumber { get; set; }
        public string Icon { get; set; }
    }

    public class MemberDashboardDTO
    {
        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime MemberSince { get; set; }

        // Contribution Summary
        public decimal TotalDeposits { get; set; }
        public decimal TotalShares { get; set; }
        public decimal TotalSavings { get; set; }

        // Loan Summary
        public int ActiveLoansCount { get; set; }
        public decimal TotalOutstandingLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }

        // Eligibility
        public List<LoanProductDTO> AvailableLoanProducts { get; set; }
        public decimal MaxEligibleAmount { get; set; }

        // Lists
        public List<MemberLoanSummaryDTO> ActiveLoans { get; set; }
        public List<PendingGuaranteeDTO> PendingGuarantorRequests { get; set; }

        public DateTime LastLogin { get; set; }
    }

    public class PendingGuaranteeDTO
    {
        public string LoanNo { get; set; } = null!;
        public string ApplicantName { get; set; } = null!;
        public decimal LoanAmount { get; set; }
        public DateTime InvitationDate { get; set; }
    }

    public class GuarantorDTO
    {
        public string GuarantorMemberNo { get; set; }
        public string GuarantorName { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public string Email { get; set; }
        public string PhoneNo { get; set; }
    }

    public class MemberLoanSummaryDTO
    {
        public string LoanNo { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public decimal OutstandingBalance { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public decimal MonthlyInstallment { get; set; }
    }

    public class LoanProductDTO
    {
        public string LoanCode { get; set; }
        public string LoanName { get; set; }
        public string Description { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriodMonths { get; set; }
        public decimal Multiplier { get; set; }
        public bool IsMobileLoan { get; set; }
        public bool RequiresGuarantor { get; set; }
        public bool IsEligible { get; set; }
        public string EligibilityMessage { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal EstimatedMonthlyInstallment { get; set; }
        public decimal MaxEligibleAmount { get; internal set; }
    }

    public class LoanEligibilityDTO
    {
        public bool IsEligible { get; set; }
        public string Message { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal CurrentDeposits { get; set; }
        public decimal CurrentShares { get; set; }
        public decimal Multiplier { get; set; }
        public bool IsMobileLoan { get; set; }
        public bool RequiresGuarantor { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriodMonths { get; set; }
        public decimal EstimatedMonthlyInstallment { get; set; }
    }

    public class SelfLoanApplicationDTO
    {
        public string LoanCode { get; set; }
        public decimal PrincipalAmount { get; set; }
        public int RepayPeriod { get; set; }
        public string Purpose { get; set; }
        public string Remarks { get; set; }
        public string CompanyCode { get; set; }
        public string IpAddress { get; set; }
    }

    public class LoanApplicationResultDTO
    {
        public bool Success { get; set; }
        public string LoanNo { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public bool IsMobileLoan { get; set; }
        public bool CanWithdrawNow { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public string BlockchainTxId { get; set; }
    }

    public class LoanDisbursementResultDTO
    {
        public bool Success { get; set; }
        public string LoanNo { get; set; }
        public decimal Amount { get; set; }
        public string Message { get; set; }
        public string TransactionReference { get; set; }
        public string BlockchainTxId { get; set; }
    }

    public class AutoAppraisalResultDTO
    {
        public bool Success { get; set; }
        public string LoanNo { get; set; }
        public string Recommendation { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal RequestedAmount { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public decimal TotalInterest { get; set; }
        public string Message { get; set; }
    }
}