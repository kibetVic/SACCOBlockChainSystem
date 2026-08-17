// Models/DTOs/InquiryDTOs.cs
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // Member Search DTOs
    public class MemberSearchDTO
    {
        public string? MemberNo { get; set; }
        public string? FullName { get; set; }
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public short? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public string GetSearchDescription()
        {
            var criteria = new List<string>();
            if (!string.IsNullOrEmpty(MemberNo)) criteria.Add($"MemberNo={MemberNo}");
            if (!string.IsNullOrEmpty(FullName)) criteria.Add($"Name={FullName}");
            if (!string.IsNullOrEmpty(IdNo)) criteria.Add($"ID={IdNo}");
            if (!string.IsNullOrEmpty(PhoneNo)) criteria.Add($"Phone={PhoneNo}");
            if (!string.IsNullOrEmpty(Email)) criteria.Add($"Email={Email}");
            return criteria.Count > 0 ? string.Join(", ", criteria) : "All Members";
        }
    }

    public class MemberSearchResultDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public string Status { get; set; } = null!;
        public DateTime DateJoined { get; set; }
        public decimal ShareBalance { get; set; }
        public int GuarantorLoansCount { get; set; }
        public decimal TotalGuaranteeAmount { get; set; }
    }

    public class MemberSearchResponseDTO
    {
        public MemberSearchDTO SearchCriteria { get; set; } = null!;
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public List<MemberSearchResultDTO> Members { get; set; } = new();
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
    }

    // Member Inquiry DTOs
    public class MemberInquiryResponseDTO
    {
        // Personal Information
        public string MemberNo { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Age { get; set; }
        public string? Station { get; set; }
        public string? Department { get; set; }
        public string? Employer { get; set; }
        public string? MembershipType { get; set; }
        public string? RegistrationType { get; set; }
        public DateTime? DateJoined { get; set; }
        public string Status { get; set; } = null!;
        public bool IsActive { get; set; }

        // Financial Summary
        public decimal TotalContributions { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public int ActiveLoansCount { get; set; }
        public int TotalTransactions { get; set; }

        // Share Balances by Type
        public List<ShareBalanceByTypeDTO> ShareBalances { get; set; } = new();

        // Next of Keen
        public List<NextOfKeenSummaryDTO> NextOfKeens { get; set; } = new();

        // Audit
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
    }

    public class ShareBalanceByTypeDTO
    {
        public string SharesCode { get; set; } = null!;
        public string SharesType { get; set; } = null!;
        public decimal Balance { get; set; }
    }

    public class NextOfKeenSummaryDTO
    {
        public string FullName { get; set; } = null!;
        public string Relationship { get; set; } = null!;
        public string PhoneNo { get; set; } = null!;
        public bool IsPrimary { get; set; }
        public decimal BenefitPercentage { get; set; }
    }

    // Share Inquiry DTOs
    public class ShareInquiryResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public decimal TotalShareBalance { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal LockedForGuarantees { get; set; }
        public decimal AvailableShares { get; set; }
        public List<ShareTypeSummaryDTO> ShareTypeSummaries { get; set; } = new();
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
        public string? MemberIdNo { get; set; }
        public int? MemberPhone { get; set; }
        public decimal TotalRegFees { get; set; }
        public decimal TotalDonations { get; set; }
        public decimal TotalLoanAllocations { get; set; }
        public decimal TotalPassBook { get; set; }
    }

    public class ShareTypeSummaryDTO
    {
        public string SharesCode { get; set; } = null!;
        public string SharesType { get; set; } = null!;
        public decimal TotalShares { get; set; }
        public decimal ShareCapital { get; set; }
        public decimal Deposits { get; set; }
        public decimal RegFees { get; set; }
        public decimal Donations { get; set; }
        public decimal LoanAllocations { get; set; }
        public decimal PassBook { get; set; }
        public List<ShareTransactionDetailDTO> Transactions { get; set; } = new();
    }

    public class ShareTransactionDetailDTO
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal ShareCapital { get; set; }
        public decimal Deposits { get; set; }
        public string? ReceiptNo { get; set; }
        public string? Remarks { get; set; }
        public string? BlockchainTxId { get; set; }
        public decimal RegFees { get; set; }
        public decimal Donations { get; set; }
        public decimal LoanAllocations { get; set; }
        public decimal PassBook { get; set; }
    }

    // Loan Inquiry DTOs
    public class LoanInquiryResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public int TotalLoans { get; set; }
        public decimal TotalBorrowed { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalRepaid { get; set; }
        public int ActiveLoansCount { get; set; }
        public int OverdueLoansCount { get; set; }
        public int CompletedLoansCount { get; set; }
        public List<LoanDetailDTO> Loans { get; set; } = new();
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
        public string? MemberIdNo { get; set; }
        public int? MemberPhone { get; set; }
    }

    public class LoanDetailDTO
    {
        public string LoanNo { get; set; } = null!;
        public string? LoanCode { get; set; }
        public string LoanType { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal DisbursedAmount => ApprovedAmount > 0 ? ApprovedAmount : PrincipalAmount;
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriod { get; set; }
        public string? RepaymentMethod { get; set; }
        public string? Purpose { get; set; }
        public string Status { get; set; } = null!;
        public decimal OutstandingBalance { get; set; }
        public decimal OutstandingInterest { get; set; }
        public decimal TotalRepaid { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime? NextDueDate { get; set; }
        public bool IsOverdue { get; set; }
        public List<GuarantorInfoDTO> Guarantors { get; set; } = new();
        public List<RepaymentInfoDTO> RecentRepayments { get; set; } = new();
    }

    public class GuarantorInfoDTO
    {
        public string MemberNo { get; set; } = null!;
        public string? FullName { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public decimal Balance { get; set; }
    }

    public class RepaymentInfoDTO
    {
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Penalty { get; set; }
        public string? ReceiptNo { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    // Transaction Inquiry DTOs
    public class TransactionInquiryResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public int TotalTransactions { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal NetPosition { get; set; }
        public List<TransactionDetailDTO> Transactions { get; set; } = new();
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
    }

    public class TransactionDetailDTO
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public string? Reference { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? ProcessedBy { get; set; }
    }

    // Audit Trail DTO
    public class AuditRecordDTO
    {
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? Module { get; set; } = "Inquiry";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? BrowserAgent { get; set; }
        public string? CorrelationId { get; set; }
        public string? ExtraData { get; set; }
    }

    public class ShareVariationDTO
    {
        public string SharesCode { get; set; } = null!;
        public string? SharesType { get; set; }
        public bool IsMainShares { get; set; }
        public bool UsedToGuarantee { get; set; }
        public bool UsedToOffset { get; set; }
        public bool Withdrawable { get; set; }
        public decimal MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public int Priority { get; set; }
        public decimal? Interest { get; set; }
        public float? LoanToShareRatio { get; set; }
        public int TotalMembers { get; set; }
        public decimal TotalShares { get; set; }
    }

    // DTO for Guarantor Loans List
    public class GuarantorLoanListResponseDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string MemberPhone { get; set; } = null!;
        public string MemberIdNo { get; set; } = null!;
        public decimal TotalShares { get; set; }
        public decimal LockedForGuarantees { get; set; }
        public decimal AvailableShares { get; set; }
        public int TotalGuaranteedLoans { get; set; }
        public List<GuarantorLoanDetailDTO> GuaranteedLoans { get; set; } = new();
        public DateTime InquiryTimestamp { get; set; }
        public string? InquiredBy { get; set; }
    }

    public class GuarantorLoanDetailDTO
    {
        // Loanee (Borrower) Information
        public string LoaneeMemberNo { get; set; } = null!;
        public string LoaneeName { get; set; } = null!;
        public string LoaneePhone { get; set; } = null!;
        public string LoaneeIdNo { get; set; } = null!;

        // Loan Information
        public string LoanNo { get; set; } = null!;
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriod { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
        public string LoanStatus { get; set; } = null!;
        public bool IsActive { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }

        // Guarantor Information (the member who guaranteed)
        public int GuarantorId { get; set; }
        public string GuarantorMemberNo { get; set; } = null!;
        public decimal GuaranteeAmount { get; set; }
        public decimal GuaranteeBalance { get; set; }
        public decimal RemainingGuarantee { get; set; }
        public DateTime GuaranteeDate { get; set; }
        public string GuaranteeStatus { get; set; } = "Active";
        public string? GuaranteeDescription { get; set; }
        public string? CollateralType { get; set; }

        // Member's Share Information (the guarantor)
        public decimal MemberTotalShares { get; set; }
        public decimal MemberLockedShares { get; set; }
        public decimal MemberAvailableShares { get; set; }
        public decimal ShareBalanceAfterThisGuarantee { get; set; }
    }


    public class LoanRepaymentHistoryDTO
    {
        // Company Information
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyPhone { get; set; } = null!;
        public string CompanyEmail { get; set; } = null!;

        // Member Information
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string MemberIdNo { get; set; } = null!;
        public string MemberPhone { get; set; } = null!;
        public string MemberEmail { get; set; } = null!;

        // Loan Information
        public string LoanNo { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public string LoanCode { get; set; } = null!;
        public decimal PrincipalAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriod { get; set; }
        public string RepaymentMethod { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public string LoanStatus { get; set; } = null!;
        public bool IsOverdue { get; set; }
        public decimal TotalPrincipalPaid { get; set; }
        public decimal TotalInterestPaid { get; set; }
        public decimal TotalPenaltyPaid { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public decimal OutstandingPrincipal { get; set; }
        public decimal OutstandingInterest { get; set; }
        public decimal OutstandingPenalty { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OriginalTotalAmount { get; set; }
        public decimal PercentagePaid { get; set; }
        public bool IsFullyPaid { get; set; }

        // Repayment History
        public List<RepaymentHistoryDetailDTO> Repayments { get; set; } = new();

        // Audit
        public DateTime InquiryTimestamp { get; set; }
        public string InquiredBy { get; set; } = null!;
        public string? BlockchainTxId { get; set; }
    }

    public class RepaymentHistoryDetailDTO
    {
        public int Id { get; set; }
        public DateTime PaymentDate { get; set; }
        public int PaymentNumber { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public decimal AmountPaid { get; set; }
        public decimal PrincipalPaid { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal PenaltyPaid { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        public string? ProcessedBy { get; set; }
        public string Status { get; set; } = null!; // On-time, Overdue, Full Settlement
        public int? DaysOverdue { get; set; }
        public string? BlockchainTxId { get; set; }
    }
}