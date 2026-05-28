namespace SACCOBlockChainSystem.Models
{
    internal class MemberViewModel
    {
        public Member Member { get; set; }
        public List<Wallet> Wallets { get; set; }
        public List<BlockchainTransaction> MemberTransactions { get; set; } = new List<BlockchainTransaction>();
        public string? UserCompanyCode { get; internal set; }

    }
}