using System.Collections.Generic;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class LoanDetailsViewModel
    {
        public Loan Loan { get; set; }

        public Loanbal LoanBalance { get; set; }

        public Cheque Disbursement { get; set; }

        public List<GuarantorResponseDTO> Guarantors { get; set; }

        public Appraisal Appraisal { get; set; }

        public List<Endmain> Approvals { get; set; }

        public List<LoanScheduleDTO> Schedule { get; set; }

        public List<Repay> Repayments { get; set; }

        public List<AuditTrail> AuditTrail { get; set; }

        public string LoanTypeName { get; set; }

        public bool CanEdit { get; set; }
        public bool CanAppraise { get; set; }
        public bool CanApprove { get; set; }
        public bool CanEndorse { get; set; }
        public bool CanDisburse { get; set; }
        public bool CanRepay { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string LoanNo { get; set; }
        public string LoanType { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public string Status { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public List<LoanScheduleDTO> RepaymentSchedule { get; set; }
    }
}