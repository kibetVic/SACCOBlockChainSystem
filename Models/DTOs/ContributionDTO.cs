// Models/DTOs/ContributionDTO.cs
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class ContributionDTO
    {
        [Required(ErrorMessage = "Member number is required")]
        public string MemberNo { get; set; } = null!;

        [Required(ErrorMessage = "Transaction date is required")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Share type is required")]
        public string SharesCode { get; set; } = null!;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        public string? ReceiptNo { get; set; }

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters")]
        public string? Remarks { get; set; }
        public string? PaymentMethod { get; set; } = "CASH";
        public string? ReferenceNo { get; set; }
        public string? PromptPayment { get; set; }
        public string CreatedBy { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public DateTime? DepositedDate { get; internal set; }
        public string? TransactionNo { get; internal set; }
    }

    public class ContributionResponseDTO
    {
        public int Id { get; set; }
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public DateTime TransactionDate { get; set; }
        public string SharesCode { get; set; } = null!;
        public string ShareTypeName { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal TotalSharesAfter { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public string Remarks { get; set; } = null!;
        public string BlockchainTxId { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public string? TransactionNo { get; set; }
        public decimal ShareCapitalAmount { get; set; }
        public decimal DepositsAmount { get; set; }
        public decimal RegFeeAmount { get; set; }
        public decimal Donor { get; set; }
        public decimal LoanAmount { get; set; }
        public decimal PassBookAmount { get; set; }
        public string? ReferenceNo { get; set; }
        public DateTime DepositedDate { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string? PromptPayment { get; set; }
        public string TransactionSignature { get; internal set; }
        public string TransactionHash { get; internal set; }
        public bool IsSignatureVerified { get; internal set; }
        public string Status { get; internal set; }
    }

    public class ShareTypeDTO
    {
        public string SharesCode { get; set; } = null!;
        public string SharesType { get; set; } = null!;
        public string SharesAcc { get; set; } = null!;
        public bool IsMainShares { get; set; }
        public bool UsedToGuarantee { get; set; }
        public bool Withdrawable { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public string CompanyCode { get; set; } = null!;
        public bool UsedToOffset { get; set; }
    }

    public class MemberContributionHistoryDTO
    {
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public List<ContributionDetailDTO> Contributions { get; set; } = new();
        public decimal TotalContributions { get; set; }
        public decimal CurrentShareBalance { get; set; }
        public string CompanyCode { get; set; } = null!;
    }

    public class ContributionDetailDTO
    {
        public DateTime TransactionDate { get; set; }
        public string SharesCode { get; set; } = null!;
        public string ShareTypeName { get; set; } = null!;
        public decimal Amount { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public string Remarks { get; set; } = null!;
        public string BlockchainTxId { get; set; } = null!;
        public string CreatedBy { get; set; } = null!;
    }

    // delete contribution
    public class ContributionDeleteResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public int ContributionId { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTime DeletedAt { get; set; }
        public string DeletedBy { get; set; } = null!;
        public string BlockchainTxId { get; set; } = null!;
    }

    public class ContributionDeleteDTO
    {
        [Required]
        public int ContributionId { get; set; }

        [Required(ErrorMessage = "Delete reason is required")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string DeleteReason { get; set; } = null!;

        public string? ReceiptNo { get; set; }
        public string? MemberNo { get; set; }
        public decimal? Amount { get; set; }

        // Additional fields for display
        public string? MemberName { get; set; }
        public string? ShareTypeName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    // Models/DTOs/ContributionReverseDTO.cs
    public class ContributionReverseDTO
    {
        [Required]
        public int ContributionId { get; set; }

        [Required(ErrorMessage = "Reversal reason is required")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string ReverseReason { get; set; } = null!;

        public string? ReceiptNo { get; set; }
        public string? MemberNo { get; set; }
        public decimal? Amount { get; set; }
        public string? MemberName { get; set; }
        public string? ShareTypeName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    public class MemberShareTypeTotalsDTO
    {
        public string MemberNo { get; set; } = null!;
        public List<ShareTypeTotalsDTO> ShareTypeTotals { get; set; } = new();
    }

    public class ShareTypeTotalsDTO
    {
        public string SharesCode { get; set; } = null!;
        public string SharesType { get; set; } = null!;
        public string Category { get; set; } = null!;
        public decimal CurrentAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public bool IsFullyPaid { get; set; }
        public int Priority { get; set; }
        public bool IsMainShares { get; set; }
        public bool UsedToGuarantee { get; set; }
        public bool UsedToOffset { get; set; }
        public bool Withdrawable { get; set; }
        public int TransactionCount { get; internal set; }
    }

    public class ContributionItemDTO
    {
        [Required]
        public string MemberNo { get; set; } = string.Empty;

        [Required]
        public string SharesCode { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        public string Remarks { get; set; } = string.Empty;

        public DateTime? DepositedDate { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public string? PaymentMethod { get; set; } = "CASH";

        public string? ReferenceNo { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string? CreatedBy { get; set; }
    }
}