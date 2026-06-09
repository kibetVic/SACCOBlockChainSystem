// Models/ViewModels/LoanApplicationViewModel.cs
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class LoanApplicationViewModel
    {
        [Required]
        public string LoanCode { get; set; }

        public string LoanName { get; set; }

        [Required]
        [Display(Name = "Principal Amount")]
        public decimal PrincipalAmount { get; set; }

        [Required]
        [Range(1, 360, ErrorMessage = "Repayment period must be between 1 and 360 months")]
        [Display(Name = "Repayment Period (months)")]
        public int RepayPeriod { get; set; }

        [StringLength(500)]
        [Display(Name = "Loan Purpose")]
        public string Purpose { get; set; }

        [StringLength(500)]
        public string Remarks { get; set; }

        public string CompanyCode { get; set; }

        // Display/Calculation properties
        public decimal MaxAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int MaxRepayPeriod { get; set; }
        public decimal EstimatedMonthlyInstallment { get; set; }
        public decimal TotalInterest { get; set; }  
        public decimal TotalRepayment { get; set; } 
        public string RepayMethod { get; set; }  
        public bool IsMobileLoan { get; set; }
        public bool RequiresGuarantor { get; set; }
        public bool SelfGuarantee { get; internal set; }
        public decimal AvailableSharesForGuarantee { get; internal set; }
    }

    public class LoanStatusViewModel
    {
        public string LoanNo { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public bool IsMobileLoan { get; set; }
        public bool CanWithdraw { get; set; }
        public decimal Amount { get; set; }
        public DateTime ApplicationDate { get; set; }
        public List<LoanScheduleDTO> RepaymentSchedule { get; set; }
    }

    public class LoanWithdrawalViewModel
    {
        public string LoanNo { get; set; }
        public decimal Amount { get; set; }
        public string MemberPhone { get; set; }

        [Required(ErrorMessage = "Please select withdrawal method")]
        public string WithdrawalMethod { get; set; }

        [Display(Name = "M-Pesa Phone Number")]
        public string MpesaPhoneNumber { get; set; }
        public List<WithdrawalMethodDTO> WithdrawalMethods { get; set; }
        public decimal ProcessingFee { get; set; }
        public string MemberAccount { get; set; }
        public decimal ProcessingFeePercentage { get; internal set; }
    }

    public class DisbursementConfirmationViewModel
    {
        public string LoanNo { get; set; }
        public decimal Amount { get; set; }
        public DateTime DisbursementDate { get; set; }
        public decimal NetAmount { get; set; } 
        public decimal ProcessingFee { get; set; }
        public string ChequeNo { get; set; }  
        public string WithdrawalMethod { get; set; }
        public decimal ProcessingFeePercentage { get; internal set; }
    }

    public class LoanDetailssViewModel
    {
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

    public class MobileLoanViewModel
    {
        public string LoanCode { get; set; }
        public string LoanName { get; set; }

        [Required]
        [Range(1000, 1000000, ErrorMessage = "Amount must be between 1,000 and 1,000,000")]
        public decimal PrincipalAmount { get; set; }

        [Required]
        [Range(1, 12, ErrorMessage = "Repayment period must be between 1 and 12 months")]
        public int RepayPeriod { get; set; }

        public string Purpose { get; set; }

        // Display properties
        public decimal EligibleAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int MaxRepayPeriod { get; set; }
        public decimal EstimatedMonthlyInstallment { get; set; }
        public decimal CurrentDeposits { get; set; }
        public decimal Multiplier { get; set; }
        public decimal AvailableShares { get; set; }
        public bool IsSelfGuarantee { get; set; } = true;
        public bool IsMobileLoan { get; set; }
    }

    public class LoanCalculationResult
    {
        public decimal MonthlyInstallment { get; set; }
        public decimal TotalInterest { get; set; }
        public decimal TotalRepayment { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal NetDisbursement { get; set; }
        public string RepayMethod { get; set; }
        public List<LoanScheduleItem> Schedule { get; set; } = new();
    }

    public class LoanScheduleItem
    {
        public int InstallmentNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalPayment { get; set; }
        public decimal InterestPayment { get; set; }
        public decimal TotalPayment { get; set; }
        public decimal RemainingBalance { get; set; }
        public string CompanyCode { get; set; } = null!;
    }

    public class LoanRepaymentViewModel
    {
        public string LoanNo { get; set; }
        public string MemberNo { get; set; }
        public string MemberName { get; set; }

        [Display(Name = "Loan Amount")]
        public decimal LoanAmount { get; set; }

        [Display(Name = "Outstanding Balance")]
        public decimal OutstandingBalance { get; set; }

        [Display(Name = "Outstanding Interest")]
        public decimal OutstandingInterest { get; set; }

        [Display(Name = "Total Outstanding")]
        public decimal TotalOutstanding { get; set; }

        [Display(Name = "Monthly Installment")]
        public decimal MonthlyInstallment { get; set; }

        [Display(Name = "Next Payment Due")]
        public DateTime? NextPaymentDate { get; set; }

        [Display(Name = "Payment Amount")]
        [Required(ErrorMessage = "Payment amount is required")]
        [Range(1, 10000000, ErrorMessage = "Payment amount must be between 1 and 10,000,000")]
        public decimal PaymentAmount { get; set; }

        [Display(Name = "Payment Method")]
        [Required(ErrorMessage = "Please select payment method")]
        public string PaymentMethod { get; set; } // MPESA, FOSA, CASH, CHEQUE

        [Display(Name = "M-Pesa Phone Number")]
        public string MpesaPhoneNumber { get; set; }

        [Display(Name = "Cheque Number")]
        public string ChequeNumber { get; set; }

        [Display(Name = "Reference Number")]
        public string ReferenceNumber { get; set; }

        [Display(Name = "Remarks")]
        [StringLength(500)]
        public string Remarks { get; set; }

        public List<PaymentMethodDTO> PaymentMethods { get; set; }
        public List<RepaymentSchedulerDTO> RepaymentSchedule { get; set; }
    }
    public class RepaymentConfirmationViewModel
    {
        public string LoanNo { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal PrincipalPaid { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal NewBalance { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string ReceiptNo { get; set; }
        public bool IsFullyPaid { get; set; }
        public string TransactionReference { get; set; }
        public string BlockchainTxId { get; set; }
    }
}