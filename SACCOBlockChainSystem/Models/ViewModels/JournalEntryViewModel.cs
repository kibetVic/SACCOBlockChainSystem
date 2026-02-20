using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class JournalEntryViewModel
    {
        [Display(Name = "Voucher No")]
        public string? VoucherNo { get; set; }

        [Display(Name = "Voucher Date")]
        public DateTime VoucherDate { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Member No")]
        public string? MemberNo { get; set; }

        [Display(Name = "Loan No")]
        public string? LoanNo { get; set; }

        public string? CompanyCode { get; set; }

        public string? TransactionNo { get; set; }

        public string? AuditId { get; set; }

        public DateTime AuditDate { get; set; }

        public bool Posted { get; set; }

        public DateTime PostedDate { get; set; }

        // 🔥 Journal Lines
        public List<JournalDetailViewModel> Details { get; set; }
            = new List<JournalDetailViewModel>();
    }
}