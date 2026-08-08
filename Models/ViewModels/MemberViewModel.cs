using SACCOBlockChainSystem.Models.ViewModels;

namespace SACCOBlockChainSystem.Models
{
    internal class MemberViewModel
    {
        public Member Member { get; set; }
        public List<Wallet> Wallets { get; set; }
        public List<MemberTransactionViewModel> MemberTransactions { get; set; } = new List<MemberTransactionViewModel>();
        public string? UserCompanyCode { get; internal set; }
        public int TotalBlockchainTransactions { get; internal set; }
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