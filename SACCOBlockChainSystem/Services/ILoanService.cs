using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanService
    {
        Task<LoanApplicationResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application);
        Task<bool> ApproveLoanAsync(string loanNo, string approvedBy);
        Task<LoanRepaymentResponseDTO> ProcessRepaymentAsync(LoanRepaymentDTO repayment);
        Task<List<Loan>> GetMemberLoansAsync(string memberNo);
        Task<Loan> GetLoanByLoanNoAsync(string loanNo);
        Task<decimal> CalculateLoanEligibilityAsync(string memberNo);
    }
}