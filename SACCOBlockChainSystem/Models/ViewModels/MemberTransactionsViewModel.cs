// Models/ViewModels/MemberTransactionsViewModel.cs
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class MemberTransactionsViewModel
    {
        public Member Member { get; set; }
        public List<Transactions2> Transactions { get; set; } = new List<Transactions2>();
        public List<Loan> LoanHistory { get; set; } = new List<Loan>();
        public decimal TotalShares { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal TotalDeposits { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }
}