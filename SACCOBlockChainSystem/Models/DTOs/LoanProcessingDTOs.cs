using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // DTO for loan application
    public class LoanApplicationDTO
    {
        [Required]
        public string MemberNo { get; set; } = null!;

        [Required]
        public string LoanCode { get; set; } = null!;

        [Required]
        [Range(100, 10000000, ErrorMessage = "Loan amount must be between $100 and $10,000,000")]
        public decimal LoanAmount { get; set; }

        [Required]
        [Range(1, 120, ErrorMessage = "Repayment period must be between 1 and 120 months")]
        public int RepayPeriod { get; set; }

        [Required]
        [StringLength(500)]
        public string Purpose { get; set; } = null!;

        [StringLength(500)]
        public string? SecurityDetails { get; set; }

        [StringLength(100)]
        public string? SourceOfRepayment { get; set; }

        public string? CreatedBy { get; set; }

        // Will be set from context
        public string? CompanyCode { get; set; }
    }

    // DTO for loan guarantor
    public class LoanGuarantorDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        public string GuarantorMemberNo { get; set; } = null!;

        public decimal? Amount { get; set; }

        [StringLength(500)]
        public string? Collateral { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string? ActionBy { get; set; }

        // Will be set from context
        public string? CompanyCode { get; set; }
    }

    // DTO for loan appraisal
    public class LoanAppraisalDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        public string AppraisalBy { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Recommendation { get; set; } = null!; // APPROVE, REJECT, HOLD

        [Required]
        [StringLength(1000)]
        public string Comments { get; set; } = null!;

        public decimal? RecommendedAmount { get; set; }

        public int? RecommendedPeriod { get; set; }

        public decimal? InterestRate { get; set; }

        public decimal? ProcessingFee { get; set; }

        public string? CompanyCode { get; set; }
    }

    // DTO for loan endorsement
    public class LoanEndorsementDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        public string EndorsedBy { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Decision { get; set; } = null!; // APPROVED, REJECTED

        [Required]
        [StringLength(1000)]
        public string Comments { get; set; } = null!;

        public decimal? ApprovedAmount { get; set; }

        public int? ApprovedPeriod { get; set; }

        public DateTime? ApprovalDate { get; set; }

        public DateTime? DisbursementDate { get; set; }

        public string? CompanyCode { get; set; }
    }

    // DTO for loan disbursement/payment
    public class LoanDisbursementDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime DisbursementDate { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; } // CASH, CHEQUE, BANK_TRANSFER, MPESA

        [StringLength(100)]
        public string? ReferenceNo { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        public string? ProcessedBy { get; set; }

        public string? CompanyCode { get; set; }
    }

    // DTO for loan repayment
    public class LoanRepaymentDTO
    {
        [Required]
        public string LoanNo { get; set; } = null!;

        [Required]
        public string MemberNo { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [StringLength(50)]
        public string PaymentType { get; set; } = "PRINCIPAL"; // PRINCIPAL, INTEREST, PENALTY

        [StringLength(100)]
        public string? ReceiptNo { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        public string? ProcessedBy { get; set; }

        public string? CompanyCode { get; set; }
    }

    // DTO for loan details response
    public class LoanDetailsResponseDTO
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public decimal AppliedAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public decimal? DisbursedAmount { get; set; }
        public int? RepayPeriod { get; set; }
        public string? Purpose { get; set; }
        public string Status { get; set; } = null!;
        public int StatusCode { get; set; }
        public string? StatusDescription { get; set; }
        public decimal? ShareBalance { get; set; }
        public decimal? LoanBalance { get; set; }
        public decimal? InterestBalance { get; set; }
        public decimal? TotalGuarantee { get; set; }
        public List<LoanGuarantorDTO> Guarantors { get; set; } = new();
        public List<LoanTransactionDTO> Transactions { get; set; } = new();
        public string CompanyCode { get; set; } = null!;
        public string? BlockchainTxId { get; set; }
    }

    // DTO for loan transaction
    public class LoanTransactionDTO
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = null!; // APPLICATION, GUARANTOR, APPRAISAL, ENDORSEMENT, DISBURSEMENT, REPAYMENT
        public string Description { get; set; } = null!;
        public decimal? Amount { get; set; }
        public string? PerformedBy { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    // DTO for loan eligibility check
    public class LoanEligibilityDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public decimal TotalShares { get; set; }
        public decimal TotalGuarantee { get; set; }
        public decimal AvailableGuarantee { get; set; }
        public decimal MaxLoanEligibility { get; set; }
        public int ActiveLoans { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public List<LoanTypeEligibilityDTO> EligibleLoanTypes { get; set; } = new();
        public bool IsEligible { get; set; }
        public string? Reason { get; set; }
    }

    // DTO for loan type eligibility
    public class LoanTypeEligibilityDTO
    {
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal? MaxAmount { get; set; }
        public decimal EligibleAmount { get; set; }
        public int? RepayPeriod { get; set; }
        public string? Interest { get; set; }
        public bool IsEligible { get; set; }
        public string? Reason { get; set; }
    }

    // DTO for loan search
    public class LoanSearchDTO
    {
        public string? MemberNo { get; set; }
        public string? LoanNo { get; set; }
        public string? LoanCode { get; set; }
        public int? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? CompanyCode { get; set; }
    }

    // DTO for loan list response
    public class LoanListResponseDTO
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal AppliedAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string? Purpose { get; set; }
        public decimal? LoanBalance { get; set; }
    }
}