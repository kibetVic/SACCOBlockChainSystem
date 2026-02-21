using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanService
    {
        // Loan Application
        Task<Loan> ApplyForLoanAsync(LoanApplicationDTO application);
        Task<Loan> GetLoanByNoAsync(string loanNo, string companyCode);
        Task<List<LoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode);
        Task<List<LoanSummaryDTO>> SearchLoansAsync(LoanSearchDTO searchDto);
        Task<LoanDashboardDTO> GetLoanDashboardAsync(string companyCode);

        // Guarantor Management
        Task<LoanGuarantor> AssignGuarantorAsync(string loanNo, GuarantorAssignmentDTO guarantor, string assignedBy);
        Task<List<GuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo);
        Task<bool> ApproveGuarantorAsync(int guarantorId, string approvedBy);
        Task<bool> RejectGuarantorAsync(int guarantorId, string remarks, string rejectedBy);
        Task<bool> ReleaseGuarantorAsync(int guarantorId, string releasedBy);
        Task<bool> ValidateGuarantorEligibilityAsync(string memberNo, decimal guaranteeAmount, string companyCode);

        // Loan Appraisal
        Task<LoanAppraisal> AppraiseLoanAsync(LoanAppraisalDTO appraisalDto);
        Task<LoanAppraisal> GetLoanAppraisalAsync(string loanNo);

        // Loan Approval
        Task<LoanApproval> ApproveLoanAsync(LoanApprovalDTO approvalDto);
        Task<List<LoanApproval>> GetLoanApprovalsAsync(string loanNo);
        Task<bool> IsLoanApprovedAsync(string loanNo);

        // Disbursement
        Task<LoanDisbursement> DisburseLoanAsync(LoanDisbursementDTO disbursementDto);
        Task<LoanDisbursement> GetLoanDisbursementAsync(string loanNo);

        // Schedule Generation
        Task<List<LoanSchedule>> GenerateLoanScheduleAsync(string loanNo);
        Task<List<LoanScheduleDTO>> GetLoanScheduleAsync(string loanNo);
        Task UpdateOverdueStatusesAsync(string companyCode);

        // Repayments
        Task<LoanRepayment> ProcessRepaymentAsync(LoanRepaymentDTO repaymentDto);
        Task<List<LoanRepayment>> GetLoanRepaymentsAsync(string loanNo);
        Task<LoanRepayment> ReverseRepaymentAsync(int repaymentId, string reason, string reversedBy);

        // State Management
        Task<bool> UpdateLoanStatusAsync(string loanNo, string newStatus, string performedBy, string? remarks = null);
        Task<bool> CanTransitionAsync(string loanNo, string targetStatus);

        // Validation
        Task<(bool IsValid, string Message)> ValidateLoanApplicationAsync(LoanApplicationDTO application);
        Task<(bool IsEligible, string Message)> CheckMemberEligibilityAsync(string memberNo, string loanCode, string companyCode);
        Task<decimal> CalculateMaximumLoanAmountAsync(string memberNo, string loanCode, string companyCode);

        // Audit
        Task<List<LoanAuditTrail>> GetLoanAuditTrailAsync(string loanNo);
    }
}