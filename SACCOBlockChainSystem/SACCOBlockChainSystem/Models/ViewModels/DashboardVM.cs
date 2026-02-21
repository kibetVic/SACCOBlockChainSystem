using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class DashboardVM
    {
        // User Information
        public string UserGroup { get; set; } = "Member";
        public List<string> UserRoles { get; set; } = new();

        // Basic Statistics (for all users)
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int PendingMembers { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalLoansIssued { get; set; }
        public decimal TotalLoanRepayments { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalWithdrawals { get; set; }

        // NEW: Gender Statistics
        public GenderDistribution GenderStats { get; set; } = new GenderDistribution();

        // NEW: Age Group Statistics
        public List<AgeGroupData> AgeGroups { get; set; } = new List<AgeGroupData>();

        // NEW: Employment Statistics
        public EmploymentStats EmploymentStatistics { get; set; } = new EmploymentStats();

        // NEW: Contribution Statistics
        public ContributionStats ContributionStatistics { get; set; } = new ContributionStats();

        // NEW: Loan Performance Statistics
        public LoanPerformanceStats LoanPerformance { get; set; } = new LoanPerformanceStats();

        // Blockchain Statistics
        public int TotalBlockchainTransactions { get; set; }
        public int BlocksCreatedToday { get; set; }
        public int PendingBlockchainTransactions { get; set; }
        public string BlockchainStatus { get; set; } = "Active";

        // Teller-Specific Statistics
        public int TotalTransactionsToday { get; set; }
        public decimal TotalDepositsToday { get; set; }
        public decimal TotalWithdrawalsToday { get; set; }
        public int PendingVerifications { get; set; } // For Auditor

        // Loan Officer Specific Statistics
        public int TotalLoansProcessedToday { get; set; }
        public int PendingApplicationsCount { get; set; }

        // Chart Data
        public List<MonthlyTransactionData> MonthlyTransactions { get; set; } = new();
        public List<MemberGrowthData> MemberGrowth { get; set; } = new();
        public List<LoanTypeDistribution> LoanTypeDistribution { get; set; } = new();
        public List<ShareTypeDistribution> ShareTypeDistribution { get; set; } = new();

        // NEW: Charts Data
        public List<MonthlyContributionData> MonthlyContributions { get; set; } = new List<MonthlyContributionData>();
        public List<LoanStatusDistribution> LoanStatusData { get; set; } = new List<LoanStatusDistribution>();
        public List<ShareGrowthData> ShareGrowth { get; set; } = new List<ShareGrowthData>();
        public List<MonthlyLoanData> MonthlyLoanIssuance { get; set; } = new List<MonthlyLoanData>();

        // Recent Activities
        public List<RecentTransaction> RecentTransactions { get; set; } = new();
        public List<RecentLoan> RecentLoans { get; set; } = new();
        public List<PendingLoan> PendingLoans { get; set; } = new();

        // NEW: Recent Contributions
        public List<RecentContribution> RecentContributions { get; set; } = new List<RecentContribution>();

        // Quick Stats
        public DashboardQuickStats QuickStats { get; set; } = new();

        // Member-Specific Data
        public decimal MemberShareBalance { get; set; }
        public decimal MemberTotalLoans { get; set; }
        public int MemberRecentTransactionCount { get; set; }

        // Blockchain Dashboard Data
        public BlockchainDashboardData BlockchainData { get; set; } = new BlockchainDashboardData();

        // Wallet Information
        public List<WalletInfo> Wallets { get; set; } = new List<WalletInfo>();

        // Recent Blocks
        public List<Models.Block> RecentBlocks { get; set; } = new List<Models.Block>();

        // Blockchain Chain Structure
        public List<BlockchainChain> BlockchainChains { get; set; } = new List<BlockchainChain>();

        // NEW: Summary Metrics
        public DashboardSummaryMetrics SummaryMetrics { get; set; } = new DashboardSummaryMetrics();
    }

    // NEW: Supporting Classes for Enhanced Dashboard

    public class GenderDistribution
    {
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherCount { get; set; }
        public decimal MalePercentage => Total > 0 ? (MaleCount * 100m / Total) : 0;
        public decimal FemalePercentage => Total > 0 ? (FemaleCount * 100m / Total) : 0;
        public decimal OtherPercentage => Total > 0 ? (OtherCount * 100m / Total) : 0;
        public int Total => MaleCount + FemaleCount + OtherCount;
    }

    public class AgeGroupData
    {
        public string AgeGroup { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public decimal Percentage { get; set; }
        public string Color { get; set; } = "#3498db";
    }

    public class EmploymentStats
    {
        public Dictionary<string, int> DepartmentDistribution { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> EmployerDistribution { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> RankDistribution { get; set; } = new Dictionary<string, int>();
    }

    public class ContributionStats
    {
        public decimal AverageMonthlyContribution { get; set; }
        public decimal TotalContributionsThisYear { get; set; }
        public decimal TotalContributionsLastYear { get; set; }
        public decimal GrowthRate => TotalContributionsLastYear > 0
            ? ((TotalContributionsThisYear - TotalContributionsLastYear) * 100m / TotalContributionsLastYear)
            : 0;
        public Dictionary<string, decimal> ContributionByMonth { get; set; } = new Dictionary<string, decimal>();
    }

    public class LoanPerformanceStats
    {
        public decimal AverageLoanAmount { get; set; }
        public decimal AverageRepaymentPeriod { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal RepaymentRate { get; set; }
        public Dictionary<string, int> LoansByStatus { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, decimal> LoanPerformanceByMonth { get; set; } = new Dictionary<string, decimal>();
    }

    public class MonthlyContributionData
    {
        public string Month { get; set; } = string.Empty;
        public decimal ShareCapital { get; set; }
        public decimal Deposits { get; set; }
        public decimal PassBook { get; set; }
        public decimal Total => ShareCapital + Deposits + PassBook;
    }

    public class LoanStatusDistribution
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public string Color { get; set; } = "#3498db";
    }

    public class ShareGrowthData
    {
        public string Period { get; set; } = string.Empty;
        public decimal TotalShares { get; set; }
        public decimal NewShares { get; set; }
        public decimal ShareCapitalGrowth { get; set; }
    }

    public class MonthlyLoanData
    {
        public string Month { get; set; } = string.Empty;
        public int LoansIssued { get; set; }
        public decimal TotalAmountIssued { get; set; }
        public decimal AverageLoanAmount => LoansIssued > 0 ? TotalAmountIssued / LoansIssued : 0;
        public int LoansRepaid { get; set; }
        public decimal AmountRepaid { get; set; }
    }

    public class RecentContribution
    {
        public string MemberNo { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // SHARE, DEPOSIT, LOAN_REPAYMENT
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
    }

    public class DashboardSummaryMetrics
    {
        public decimal MemberRetentionRate { get; set; }
        public decimal LoanToDepositRatio { get; set; }
        public decimal ShareCapitalGrowthRate { get; set; }
        public decimal AverageMemberAge { get; set; }
        public int ActiveLoanAccounts { get; set; }
        public int DormantAccounts { get; set; }
        public decimal PortfolioAtRisk { get; set; }
    }

    // Existing Classes (unchanged but included for completeness)

    public class MonthlyTransactionData
    {
        public string Month { get; set; } = string.Empty;
        public decimal Deposits { get; set; }
        public decimal Withdrawals { get; set; }
        public decimal LoanRepayments { get; set; }
    }

    public class MemberGrowthData
    {
        public string Period { get; set; } = string.Empty;
        public int NewMembers { get; set; }
        public int TotalMembers { get; set; }
    }

    public class LoanTypeDistribution
    {
        public string LoanType { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public string Color { get; set; } = "#3498db";
    }

    public class ShareTypeDistribution
    {
        public string ShareType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int MemberCount { get; set; }
        public string Color { get; set; } = "#2ecc71";
    }

    public class RecentTransaction
    {
        public string TransactionId { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Completed";
        public string BlockchainTxId { get; set; } = string.Empty;
    }

    public class RecentLoan
    {
        public string LoanNo { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ApplicationDate { get; set; }
    }

    public class PendingLoan
    {
        public string LoanNo { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ApplicationDate { get; set; }
        public int DaysPending { get; set; }
    }

    public class DashboardQuickStats
    {
        public decimal AverageDeposit { get; set; }
        public decimal AverageLoan { get; set; }
        public int TransactionsToday { get; set; }
        public int NewMembersToday { get; set; }
        public decimal LoanApprovalRate { get; set; }
        public decimal BlockchainUptime { get; set; } = 99.9m;
    }

    public class BlockchainDashboardData
    {
        public int TotalBlocks { get; set; }
        public int TotalTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public string? LatestBlockHash { get; set; }
        public DateTime? LatestBlockTimestamp { get; set; }
        public int TotalWallets { get; set; }
        public int ActiveWallets { get; set; }
        public int BlockchainHeight { get; set; }
        public bool IsChainValid { get; set; }
    }

    public class WalletInfo
    {
        public string Address { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; }
    }

    public class BlockchainChain
    {
        public string BlockHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int TransactionCount { get; set; }
        public long Nonce { get; set; }
        public string MerkleRoot { get; set; } = string.Empty;
    }
}