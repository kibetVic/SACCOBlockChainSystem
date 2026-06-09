//using SACCOBlockChainSystem.Models.DTOs;

//namespace SACCOBlockChainSystem.Models.ViewModels
//{
//    public class LoanApplicationViewModel
//    {
//        public string LoanCode { get; set; }
//        public string LoanName { get; set; }
//        public decimal PrincipalAmount { get; set; }
//        public int RepayPeriod { get; set; }
//        public string Purpose { get; set; }
//        public string Remarks { get; set; }

//        // Calculated/Display fields
//        public decimal MaxAmount { get; set; }
//        public decimal MinAmount { get; set; }
//        public decimal EligibleAmount { get; set; }
//        public decimal InterestRate { get; set; }
//        public int MaxRepayPeriod { get; set; }
//        public decimal EstimatedMonthlyInstallment { get; set; }
//        public bool IsMobileLoan { get; set; }
//        public bool RequiresGuarantor { get; set; }
//        public string CompanyCode { get; set; }
//    }

//    public class LoanStatusViewModel
//    {
//        public string LoanNo { get; set; }
//        public string Status { get; set; }
//        public string Message { get; set; }
//        public bool IsMobileLoan { get; set; }
//        public bool CanWithdraw { get; set; }
//        public decimal Amount { get; set; }
//        public DateTime ApplicationDate { get; set; }
//        public List<LoanScheduleDTO> RepaymentSchedule { get; set; }
//    }

//    public class LoanWithdrawalViewModel
//    {
//        public string LoanNo { get; set; }
//        public decimal Amount { get; set; }
//        public string WithdrawalMethod { get; set; }
//        public string MpesaPhoneNumber { get; set; }
//        public string MemberPhone { get; set; }
//        public List<WithdrawalMethodDTO> WithdrawalMethods { get; set; }
//    }

//    public class DisbursementConfirmationViewModel
//    {
//        public string LoanNo { get; set; }
//        public decimal Amount { get; set; }
//        public DateTime DisbursementDate { get; set; }
//    }

//    public class MobileLoanViewModel
//    {
//        public string LoanCode { get; set; }
//        public string LoanName { get; set; }
//        public decimal PrincipalAmount { get; set; }
//        public int RepayPeriod { get; set; }
//        public string Purpose { get; set; }

//        // Display fields
//        public decimal EligibleAmount { get; set; }
//        public decimal MaxAmount { get; set; }
//        public decimal MinAmount { get; set; }
//        public decimal InterestRate { get; set; }
//        public int MaxRepayPeriod { get; set; }
//        public decimal EstimatedMonthlyInstallment { get; set; }
//        public decimal CurrentDeposits { get; set; }
//        public decimal Multiplier { get; set; }
//    }
//}
