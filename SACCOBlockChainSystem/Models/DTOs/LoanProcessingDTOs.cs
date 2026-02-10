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
}