using SACCOBlockChainSystem.ViewModels;
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class BlockchainDashboardViewModel
    {
        // Summary Statistics
        public BlockchainSummaryViewModel Summary { get; set; } = new();

        // Company Info
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        // Latest Blocks (for chain visualization)
        public List<BlockViewModel> RecentBlocks { get; set; } = new();

        // Pending Transactions
        public List<TransactionSummaryViewModel> PendingTransactions { get; set; } = new();

        // Recent Activity
        public List<TransactionSummaryViewModel> RecentActivity { get; set; } = new();

        // Transaction Types Distribution
        public Dictionary<string, int> TransactionTypeDistribution { get; set; } = new();

        // Charts Data
        public DashboardChartsViewModel Charts { get; set; } = new();

        // Verification Status
        public bool BlockchainValid { get; set; }
        public DateTime LastVerifiedAt { get; set; }
        public int BlockHeight { get; set; }
    }

    public class BlockchainSummaryViewModel
    {
        public int TotalBlocks { get; set; }
        public int TotalTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int ConfirmedTransactions { get; set; }
        public decimal TotalVolume { get; set; }
        public int MembersWithTransactions { get; set; }
        public double ConfirmationRate => TotalTransactions > 0 ? (double)ConfirmedTransactions / TotalTransactions * 100 : 0;
        public string LatestBlockHash { get; set; } = string.Empty;
        public DateTime? LatestBlockTimestamp { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class BlockViewModel
    {
        public int BlockId { get; set; }
        public string BlockHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public string ShortHash => BlockHash.Length > 12 ? BlockHash[..12] + "..." : BlockHash;
        public string ShortPreviousHash => string.IsNullOrEmpty(PreviousHash) ? "Genesis" :
            (PreviousHash.Length > 12 ? PreviousHash[..12] + "..." : PreviousHash);
        public DateTime Timestamp { get; set; }
        public string MerkleRoot { get; set; } = string.Empty;
        public int Nonce { get; set; }
        public bool Confirmed { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string TimeAgo => GetTimeAgo(Timestamp);

        private static string GetTimeAgo(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hr ago";
            return $"{(int)diff.TotalDays} days ago";
        }
    }

    //public class TransactionSummaryViewModel
    //{
    //    public string TransactionId { get; set; } = string.Empty;
    //    public string ShortTransactionId => TransactionId?.Length > 12 ? TransactionId.Substring(0, 12) + "..." : TransactionId;
    //    public string TransactionType { get; set; } = string.Empty;
    //    public string? MemberNo { get; set; }
    //    public decimal Amount { get; set; }
    //    public DateTime Timestamp { get; set; }
    //    public string Status { get; set; } = string.Empty;
    //    public string? BlockHash { get; set; }
    //    public string FormattedAmount => Amount.ToString("C");
    //    public string? TimeAgo { get; set; }
    //}

    public class DashboardChartsViewModel
    {
        public List<ChartDataPoint> DailyTransactions { get; set; } = new();
        public List<ChartDataPoint> WeeklyVolume { get; set; } = new();
        public List<ChartDataPoint> TransactionTypes { get; set; } = new();
        public List<ChartDataPoint> BlockGrowth { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class ChainVisualizationViewModel
    {
        public List<BlockViewModel> Blocks { get; set; } = new();
        public int TotalBlocks { get; set; }
        public bool IsValid { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string ValidationMessage => IsValid ? "✓ Blockchain is valid" : "✗ Blockchain validation failed";
    }
}


