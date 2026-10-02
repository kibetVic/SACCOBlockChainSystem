// Models/ViewModels/DisbursementReceiptViewModel.cs
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class DisbursementReceiptViewModel
    {
        public string ReceiptNo { get; set; } = null!;
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string MemberPhone { get; set; } = null!;
        public string MemberIdNo { get; set; } = null!;
        public DateTime DisbursementDate { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetAmount { get; set; }
        public string DisbursementMethod { get; set; } = null!;
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        public string? BlockchainTxId { get; set; }
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyPhone { get; set; } = null!;
        public string CompanyEmail { get; set; } = null!;
        public string DisbursedBy { get; set; } = null!;
        public string? ChequeNo { get; set; }
        public string? BankName { get; set; }
        public decimal? InterestRate { get; set; }
        public int? RepaymentPeriod { get; set; }
        public decimal? MonthlyInstallment { get; set; }
        public DateTime PrintedAt { get; set; }
    }
    public class RepaymentReceiptViewModel
    {
        public string ReceiptNo { get; set; } = null!;
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string MemberPhone { get; set; } = null!;
        public string MemberIdNo { get; set; } = null!;
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal PrincipalAllocated { get; set; }
        public decimal InterestAllocated { get; set; }
        public decimal PenaltyAllocated { get; set; }
        public decimal BalanceAfter { get; set; }
        public decimal InterestAfter { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        public string? BlockchainTxId { get; set; }
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyPhone { get; set; } = null!;
        public string CompanyEmail { get; set; } = null!;
        public string ReceivedBy { get; set; } = null!;
        public int PaymentNumber { get; set; }
        public int TotalInstallments { get; set; }
        public int InstallmentsPaid { get; set; }
        public bool IsFullSettlement { get; set; }
        public DateTime PrintedAt { get; set; }
    }
    public class AppraisalReportViewModel
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string MemberIdNo { get; set; } = null!;
        public string MemberPhone { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal AppliedAmount { get; set; }
        public decimal RecommendedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int RepaymentPeriod { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime AppraisalDate { get; set; }
        public string AppraisalDecision { get; set; } = null!;
        public string AppraisalNotes { get; set; } = null!;
        public decimal TotalShares { get; set; }
        public List<GuarantorResponseDTO> Guarantors { get; set; } = new();
        public decimal TotalGuaranteeAmount { get; set; }
        public string AppraisedBy { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyPhone { get; set; } = null!;
        public string CompanyEmail { get; set; } = null!;
    }
}