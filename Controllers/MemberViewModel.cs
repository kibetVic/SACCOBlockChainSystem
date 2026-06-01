using SACCOBlockChainSystem.Models.ViewModels;

namespace SACCOBlockChainSystem.Models
{
    internal class MemberViewModel
    {
        public Member Member { get; set; }
        public List<Wallet> Wallets { get; set; }
        //public List<BlockchainTransaction> MemberTransactions { get; set; } = new List<BlockchainTransaction>();
        public List<MemberTransactionViewModel> MemberTransactions { get; set; } = new List<MemberTransactionViewModel>();
        public string? UserCompanyCode { get; internal set; }
        public int TotalBlockchainTransactions { get; internal set; }
    }
}