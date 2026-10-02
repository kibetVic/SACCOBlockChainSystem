// Models/ViewModels/ReceiptViewModel.cs
namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class ReceiptViewModel
    {
        public string? ReceiptNo { get; set; }
        public string? TransactionReceiptNo { get; set; }
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string? MemberPhone { get; set; }
        public string? MemberIdNo { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string ShareTypeName { get; set; } = null!;
        public string PaymentMethod { get; set; } = null!;
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        public string? BlockchainTxId { get; set; }
        public decimal ShareBalanceAfter { get; set; }
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyPhone { get; set; } = null!;
        public string CompanyEmail { get; set; } = null!;
        public string CreatedBy { get; set; } = null!;
        public DateTime PrintedAt { get; set; }
        public bool IsBulkReceipt { get; set; } = false;
        public int TotalContributions { get; set; }
        public List<ReceiptContributionItem> Contributions { get; set; } = new List<ReceiptContributionItem>();
    }

    public class ReceiptContributionItem
    {
        public string ShareTypeName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNo { get; set; }
        public string Remarks { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}