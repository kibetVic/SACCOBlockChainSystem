// Models/ViewModels/MembersIndexViewModel.cs
using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class MembersIndexViewModel
    {
        public List<Member> Members { get; set; } = new List<Member>();
        public List<Member> AllMembers { get; set; } = new List<Member>();
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public decimal TotalShareCapital { get; set; }
        public List<BlockchainTransaction> MemberTransactions { get; set; } = new List<BlockchainTransaction>();
        public string? UserCompanyCode { get; internal set; }
        public int BlockchainVerifiedCount { get; set; }
    }
    public class MemberTransactionViewModel
    {
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ReceiptNo { get; set; }
        public string? Source { get; set; }
    }

    public class MemberViewModel
    {
        public Member? Member { get; set; }
        public List<Wallet>? Wallets { get; set; }
        public List<MemberTransactionViewModel>? MemberTransactions { get; set; }  
        public string? UserCompanyCode { get; set; }
        public int TotalBlockchainTransactions { get; set; }  
        public decimal TotalContributions { get; set; }
        public decimal TotalShareCapital { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal CurrentLoanBalance { get; set; }
        public decimal CurrentLoanAmount { get; set; }
        public decimal TotalLoanRepaid { get; set; }
        public decimal TotalLoanInterest { get; set; }
        public decimal TotalAllLoans { get; set; }
        public int ActiveLoansCount { get; set; }
        public int CompletedLoansCount { get; set; }
        public bool HasActiveLoan { get; set; }
    }
}