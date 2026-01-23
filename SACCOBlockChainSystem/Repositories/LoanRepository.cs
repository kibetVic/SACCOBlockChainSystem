using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Repositories
{
    public class LoanRepository : ILoanRepository
    {
        private readonly ApplicationDbContext _context;

        public LoanRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Loan> GetByLoanNoAsync(string loanNo)
        {
            return await _context.Loans
                .Include(l => l.Loanbals)
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);
        }

        public async Task<IEnumerable<Loan>> GetMemberLoansAsync(string memberNo)
        {
            return await _context.Loans
                .Where(l => l.MemberNo == memberNo)
                .OrderByDescending(l => l.ApplicDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Loan>> GetPendingLoansAsync()
        {
            return await _context.Loans
                .Where(l => l.Status == 0) // 0 = Pending
                .Include(l => l.Member)
                .ToListAsync();
        }

        public async Task AddAsync(Loan loan)
        {
            await _context.Loans.AddAsync(loan);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Loan loan)
        {
            _context.Loans.Update(loan);
            await _context.SaveChangesAsync();
        }

        public async Task<Loanbal> GetLoanBalanceAsync(string loanNo)
        {
            return await _context.Loanbals
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);
        }

        public async Task<IEnumerable<Repay>> GetLoanRepaymentsAsync(string loanNo)
        {
            return await _context.Repays
                .Where(r => r.LoanNo == loanNo)
                .OrderBy(r => r.DateReceived)
                .ToListAsync();
        }
    }
}