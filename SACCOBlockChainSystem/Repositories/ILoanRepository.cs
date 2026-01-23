using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Repositories
{
    public interface ILoanRepository
    {
        Task<Loan> GetByLoanNoAsync(string loanNo);
        Task<IEnumerable<Loan>> GetMemberLoansAsync(string memberNo);
        Task<IEnumerable<Loan>> GetPendingLoansAsync();
        Task AddAsync(Loan loan);
        Task UpdateAsync(Loan loan);
        Task<Loanbal> GetLoanBalanceAsync(string loanNo);
        Task<IEnumerable<Repay>> GetLoanRepaymentsAsync(string loanNo);
    }
}