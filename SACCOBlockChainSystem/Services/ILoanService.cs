using SACCOBlockChainSystem.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanService
    {
        // Loan Application
        Task<LoanApplicationResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application);
        Task<LoanDetailsResponseDTO> GetLoanDetailsAsync(string loanNo);
        Task<List<LoanListResponseDTO>> GetMemberLoansAsync(string memberNo);

        // Loan Eligibility
        Task<LoanEligibilityDTO> CheckLoanEligibilityAsync(string memberNo, string loanCode, decimal amount);

        // Guarantorship
        Task<bool> AddGuarantorAsync(LoanGuarantorDTO guarantor);
        Task<bool> RemoveGuarantorAsync(string loanNo, string guarantorMemberNo);
        Task<List<LoanGuarantorDTO>> GetLoanGuarantorsAsync(string loanNo);

        // Appraisal
        Task<bool> AppraiseLoanAsync(LoanAppraisalDTO appraisal);

        // Endorsement
        Task<bool> EndorseLoanAsync(LoanEndorsementDTO endorsement);

        // Disbursement
        Task<bool> DisburseLoanAsync(LoanDisbursementDTO disbursement);

        // Repayment
        Task<bool> MakeRepaymentAsync(LoanRepaymentDTO repayment);

        // Loan Management
        Task<List<LoanListResponseDTO>> SearchLoansAsync(LoanSearchDTO searchCriteria);
        Task<bool> UpdateLoanStatusAsync(string loanNo, int status, string description, string updatedBy);
        Task<decimal> CalculateLoanBalanceAsync(string loanNo);

        // Reports
        Task<LoanPortfolioDTO> GetLoanPortfolioReportAsync(string companyCode);
    }

    // Response DTOs
    public class LoanApplicationResponseDTO
    {
        public bool Success { get; set; }
        public string LoanNo { get; set; } = null!;
        public string Message { get; set; } = null!;
        public decimal EligibleAmount { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    public class LoanPortfolioDTO
    {
        public int TotalLoans { get; set; }
        public int ActiveLoans { get; set; }
        public int PendingLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal TotalInterestAccrued { get; set; }
        public Dictionary<string, decimal> LoanTypeDistribution { get; set; } = new();
        public Dictionary<int, int> StatusDistribution { get; set; } = new();
    }
}