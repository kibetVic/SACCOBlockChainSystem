using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanService
    {
        // Loan Operations
        Task<LoanResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application);
        Task<LoanResponseDTO> GetLoanAsync(string loanNo);
        Task<List<LoanResponseDTO>> GetLoansByMemberAsync(string memberNo);
        Task<List<LoanResponseDTO>> SearchLoansAsync(LoanSearchDTO search);
        Task<LoanResponseDTO> UpdateLoanStatusAsync(string loanNo, LoanUpdateDTO update);
        Task<bool> DeleteLoanAsync(string loanNo, string deletedBy);

        // Loan Workflow Operations
        Task<LoanResponseDTO> SubmitForGuarantorsAsync(string loanNo, string submittedBy);
        Task<LoanResponseDTO> SubmitForAppraisalAsync(string loanNo, string submittedBy);
        Task<LoanResponseDTO> SubmitForEndorsementAsync(string loanNo, string submittedBy);
        Task<LoanResponseDTO> ApproveLoanAsync(string loanNo, LoanUpdateDTO approval);
        Task<LoanResponseDTO> RejectLoanAsync(string loanNo, LoanUpdateDTO rejection);
        Task<LoanResponseDTO> DisburseLoanAsync(string loanNo, LoanDisbursementDTO disbursement);
        Task<LoanResponseDTO> CloseLoanAsync(string loanNo, string closedBy);

        // Guarantor Operations
        Task<bool> AddGuarantorAsync(string loanNo, LoanGuarantorDTO guarantor);
        Task<bool> RemoveGuarantorAsync(string loanNo, string guarantorMemberNo);
        Task<List<LoanGuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo);

        // Reports
        Task<decimal> GetMemberLoanEligibilityAsync(string memberNo);
        Task<List<LoanResponseDTO>> GetPendingLoansAsync();
        Task<List<LoanResponseDTO>> GetLoansByStatusAsync(int status);
    }
}