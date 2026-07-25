//// Models/ViewModels/MultipleContributionViewModel.cs
//using SACCOBlockChainSystem.Models.DTOs;

//namespace SACCOBlockChainSystem.Models.ViewModels
//{
//    public class MultipleContributionViewModel
//    {
//        public string? MemberNo { get; set; }
//        public string? MemberName { get; set; }
//        public DateTime TransactionDate { get; set; } = DateTime.Now;
//        public DateTime? DepositedDate { get; set; }
//        public string? PaymentMethod { get; set; } = "CASH";
//        public string? ReferenceNo { get; set; }
//        public bool PrintReceipt { get; set; } = true;
//        public List<ContributionItemViewModel> Contributions { get; set; } = new List<ContributionItemViewModel>();
//        public List<ShareTypeDTO> ShareTypes { get; set; } = new List<ShareTypeDTO>();
//        public string? CompanyCode { get; set; }
//        public List<ContributionResponseDTO>? RecentContributions { get; set; }
//    }

//    public class ContributionItemViewModel
//    {
//        public string? SharesCode { get; set; }
//        public decimal Amount { get; set; }
//        public string? Remarks { get; set; }
//        public bool IsValid { get; set; }
//        public string? ValidationMessage { get; set; }
//    }
//}