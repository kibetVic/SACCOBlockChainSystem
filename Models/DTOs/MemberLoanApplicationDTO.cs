// Models/DTOs/MemberLoanApplicationDTO.cs
using System.ComponentModel.DataAnnotations;

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
        public decimal TotalDeposits { get; set; }
        public decimal TotalShares { get; set; }
        public decimal TotalSavings { get; set; }
        public int ActiveLoansCount { get; set; }
        public decimal TotalOutstandingLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public List<LoanProductDTO> AvailableLoanProducts { get; set; }
        public decimal MaxEligibleAmount { get; set; }
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
        public bool CanWithdraw { get; internal set; }
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
        public string? RepayMethod { get; set; }
        public decimal Multiplier { get; set; }
        public bool IsMobileLoan { get; set; }
        public bool RequiresGuarantor { get; set; }
        public bool IsEligible { get; set; }
        public string? EligibilityMessage { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal EstimatedMonthlyInstallment { get; set; }
        public decimal MaxEligibleAmount { get; set; }
        public decimal ProcessingFee { get; set; }  
        public decimal ProcessingFeeAmount { get; set; }  
        public decimal NetDisbursement { get; set; }
        public bool SelfGuarantee { get; set; }  
        public string GuaranteeInfo { get; set; }
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
        public string RepayMethod { get; set; }  
        public decimal ProcessingFee { get; set; }  
        public decimal EstimatedMonthlyInstallment { get; set; }
        public decimal TotalInterest { get; set; }  
        public decimal TotalRepayment { get; set; }  
        public decimal NetDisbursement { get; set; }
        public bool SelfGuarantee { get; set; }  
        public decimal AvailableSharesForGuarantee { get; set; } 
        public decimal RequiredGuaranteeAmount { get; set; }
        public decimal ProcessingFeeAmount { get; internal set; }
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
        public string StatusUpdateBlockchainTxId { get; internal set; }
    }

    public class LoanSchedulesDTO
    {
        public int InstallmentNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
        public decimal TotalInstallment { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public decimal PenaltyAmount { get; set; }
        public string Status { get; set; }
        public DateTime? PaidDate { get; set; }
        public string OutstandingPrincipal { get; set; }
        public string OutstandingInterest { get; set; }
        public string OutstandingTotal { get; set; }
        public bool IsFlexible { get; set; }
        public decimal MinimumPayment { get; set; }
    }

    //public class LoanRepaymentDTO
    //{
    //    public string LoanNo { get; set; }
    //    public decimal Amount { get; set; }
    //    public string PaymentMethod { get; set; }
    //    public string MpesaPhoneNumber { get; set; }
    //    public string ChequeNumber { get; set; }
    //    public string ReferenceNumber { get; set; }
    //    public string Remarks { get; set; }
    //    public string IpAddress { get; set; }
    //}

    public class RepaymentResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ReceiptNo { get; set; }
        public string TransactionReference { get; set; }
        public decimal PrincipalPaid { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal PenaltyPaid { get; set; }
        public decimal NewBalance { get; set; }
        public bool IsFullyPaid { get; set; }
        public string BlockchainTxId { get; set; }
    }

    public class RepaymentHistoryDTO
    {
        public int Id { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Penalty { get; set; }
        public decimal LoanBalance { get; set; }
        public string PaymentMethod { get; set; }
        public string ChequeNo { get; set; }
        public string MpesaNumber { get; set; }
        public string Status { get; set; }
        public string BlockchainTxId { get; set; }
    }

    public class PaymentMethodDTO
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public bool RequiresPhoneNumber { get; set; }
        public bool RequiresChequeNumber { get; set; }
    }

    public class RepaymentSchedulerDTO
    {
        public int InstallmentNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Status { get; set; }
        public DateTime? PaidDate { get; set; }
    }
}