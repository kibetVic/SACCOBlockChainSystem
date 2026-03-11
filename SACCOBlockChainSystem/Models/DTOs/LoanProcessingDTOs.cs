using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // Loan Application DTO
    public class LoanApplicationDTO
    {
        public string MemberNo { get; set; } = null!;
        public string LoanCode { get; set; } = null!;
        public decimal LoanAmount { get; set; }
        public int RepayPeriod { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string SourceOfRepayment { get; set; } = string.Empty;
        public string? WitMemberNo { get; set; }
        public string? SupMemberNo { get; set; }
        public List<LoanGuarantorDTO> Guarantors { get; set; } = new();
        public string CreatedBy { get; set; } = null!;
    }

    // Loan Guarantor DTO
    public class LoanGuarantorDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullNames { get; set; } = null!;
        public decimal GuaranteedAmount { get; set; }
        public string? Collateral { get; set; }
        public string? Description { get; set; }
    }

    // Loan Update DTO
    public class LoanUpdateDTO
    {
        public int Status { get; set; }
        public string? StatusDescription { get; set; }
        public string? Remarks { get; set; }
        public string UpdatedBy { get; set; } = null!;
        public decimal? AppraisedAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public decimal? InterestRate { get; set; }
        public string? RejectionReason { get; set; }
    }

    // Loan Response DTO
    public class LoanResponseDTO
    {
        public int Id { get; set; }
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal LoanAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public decimal? DisbursedAmount { get; set; }
        public int RepayPeriod { get; set; }
        public DateTime ApplicDate { get; set; }
        public int Status { get; set; }
        public string StatusDescription { get; set; } = null!;
        public string? BlockchainTxId { get; set; }
        public List<LoanGuarantorResponseDTO> Guarantors { get; set; } = new();
        public string CompanyCode { get; set; } = null!;
    }

    // Guarantor Response DTO
    public class LoanGuarantorResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullNames { get; set; } = null!;
        public decimal GuaranteedAmount { get; set; }
        public decimal? AvailableBalance { get; set; }
        public string? Collateral { get; set; }
        public string Status { get; set; } = "Pending";
    }

    // Loan Search DTO
    public class LoanSearchDTO
    {
        public string? MemberNo { get; set; }
        public string? LoanNo { get; set; }
        public int? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? LoanCode { get; set; }
    }

    // Loan Disbursement DTO
    public class LoanDisbursementDTO
    {
        public string LoanNo { get; set; } = null!;
        public decimal DisbursedAmount { get; set; }
        public string DisbursementMethod { get; set; } = "Bank Transfer";
        public string? BankAccount { get; set; }
        public string? ChequeNo { get; set; }
        public string? ReferenceNo { get; set; }
        public string DisbursedBy { get; set; } = null!;
        public DateTime DisbursementDate { get; set; } = DateTime.Now;
    }
    public class LoanSummaryDTO
    {
        public int Id { get; set; }
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public string LoanCode { get; set; } = null!;

        // Amount fields for summary cards
        public decimal PrincipalAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public decimal? DisbursedAmount { get; set; }
        public decimal OutstandingBalance { get; set; }

        // Status fields
        public int Status { get; set; }
        public string StatusDescription { get; set; } = null!;
        public string LoanStatus { get; set; } = null!; // For filtering by status name

        // Dates
        public DateTime ApplicationDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public DateTime? MaturityDate { get; set; }

        // Repayment info
        public int RepaymentPeriod { get; set; }
        public decimal InterestRate { get; set; }
        public decimal? TotalPaid { get; set; }
        public int? InstallmentsPaid { get; set; }
        public int? TotalInstallments { get; set; }

        // Additional fields for table display
        public string? Purpose { get; set; }
        public decimal? AppraisedAmount { get; set; }
        public string? AppraisedBy { get; set; }
        public string? ApprovedBy { get; set; }
        public string? DisbursedBy { get; set; }
        public string? BlockchainTxId { get; set; }
        public string CompanyCode { get; set; } = null!;

        // Computed properties
        public decimal Principal => PrincipalAmount;
        public decimal? Approved => ApprovedAmount;
        public decimal? Disbursed => DisbursedAmount;
        public decimal Outstanding => OutstandingBalance;
        public decimal? PaidAmount => TotalPaid;

        // Helper methods for status grouping
        public bool IsPending => Status == 1 || Status == 2 || Status == 3 || Status == 4; // Draft, Submitted, UnderAppraisal, PendingFinalApproval
        public bool IsActive => Status == 7 || Status == 8; // Disbursed, Active
        public bool IsCompleted => Status == 9; // Closed
        public bool IsRejected => Status == 6; // Rejected
        public bool IsWrittenOff => Status == 10; // WrittenOff

        // Progress calculation
        public decimal? RepaymentProgress => TotalInstallments.HasValue && InstallmentsPaid.HasValue
            ? Math.Round((decimal)InstallmentsPaid.Value / TotalInstallments.Value * 100, 2)
            : 0;

        public decimal? PaymentProgress => PrincipalAmount > 0 && TotalPaid.HasValue
            ? Math.Round(TotalPaid.Value / PrincipalAmount * 100, 2)
            : 0;
    }
    public class LoanDashboardDTO
    {
        public int TotalLoans { get; set; }
        public int PendingLoans { get; set; }
        public int ActiveLoans { get; set; }
        public int ClosedLoans { get; set; }
        public int RejectedLoans { get; set; }
        public decimal TotalPrincipal { get; set; }
        public decimal TotalDisbursed { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalRepaid { get; set; }
        public decimal TotalInterest { get; set; }
        public List<LoanSummaryDTO> RecentLoans { get; set; } = new();
        public Dictionary<string, int> LoansByStatus { get; set; } = new();
        public Dictionary<string, decimal> LoansByType { get; set; } = new();
    }

    // Loan Schedule DTO
    public class LoanScheduleDTO
    {
        public int InstallmentNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? PaidDate { get; set; }
        public string? ReceiptNo { get; set; }
    }

    // Loan Repayment DTO
    public class LoanRepaymentDTO
    {
        public string LoanNo { get; set; } = null!;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Reference { get; set; }
        public string? ReceiptNo { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string ReceivedBy { get; set; } = null!;
        public string? Notes { get; set; }
        public bool IsPartialPayment { get; set; }
        public int? InstallmentNo { get; set; }
    }

    // Loan Approval DTO
    public class LoanApprovalDTO
    {
        public string LoanNo { get; set; } = null!;
        public decimal ApprovedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriod { get; set; }
        public string ApprovedBy { get; set; } = null!;
        public DateTime ApprovalDate { get; set; } = DateTime.Now;
        public string? Remarks { get; set; }
        public string? Conditions { get; set; }
    }

    // Loan Appraisal DTO
    public class LoanAppraisalDTO
    {
        public string LoanNo { get; set; } = null!;
        public decimal AppraisedAmount { get; set; }
        public decimal RecommendedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int RecommendedPeriod { get; set; }
        public string AppraisedBy { get; set; } = null!;
        public DateTime AppraisalDate { get; set; } = DateTime.Now;
        public string? Findings { get; set; }
        public string? Recommendations { get; set; }
        public string? RiskAssessment { get; set; }
        public bool IsRecommended { get; set; }
    }

    // Guarantor Assignment DTO
    public class GuarantorAssignmentDTO
    {
        public string LoanNo { get; set; } = null!;
        public string GuarantorMemberNo { get; set; } = null!;
        public string GuarantorName { get; set; } = null!;
        public decimal GuaranteedAmount { get; set; }
        public string? Relationship { get; set; }
        public string? Collateral { get; set; }
        public string? CollateralValue { get; set; }
        public DateTime AssignmentDate { get; set; } = DateTime.Now;
        public string AssignedBy { get; set; } = null!;
        public string Status { get; set; } = "Pending";
    }

    // Loan Balance DTO
    public class LoanBalanceDTO
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public decimal DisbursedAmount { get; set; }
        public decimal TotalRepaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal AccruedInterest { get; set; }
        public decimal Penalties { get; set; }
        public decimal TotalDue { get; set; }
        public DateTime LastPaymentDate { get; set; }
        public DateTime NextDueDate { get; set; }
        public int DaysOverdue { get; set; }
        public string Status { get; set; } = null!;
    }
}