// DTOs/PaymentTypeDTO.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class PaymentTypeDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Payment Type Code is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Code must be between 2 and 50 characters")]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "Payment Type Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Payment Type")]
        public string? Name { get; set; }

        [StringLength(250)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Default Expense Account")]
        public string? DefaultExpenseAccountNo { get; set; }

        [Display(Name = "Default Expense Account Name")]
        public string? DefaultExpenseAccountName { get; set; }
    }

    public class PaymentTypeResponseDTO
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public string? DefaultExpenseAccountNo { get; set; }
        public string? DefaultExpenseAccountName { get; set; }
        public string? BlockchainTxId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }



    public class PaymentTypeSimpleDTO
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? DefaultExpenseAccountNo { get; set; }
    }
}