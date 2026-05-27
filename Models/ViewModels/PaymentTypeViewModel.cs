// Models/ViewModels/PaymentTypeViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class PaymentTypeViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Payment Code")]
        public string? Code { get; set; }

        [Display(Name = "Payment Type")]
        public string? Name { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Default Expense Account")]
        public string? DefaultExpenseAccountNo { get; set; }

        [Display(Name = "Default Expense Account Name")]
        public string? DefaultExpenseAccountName { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }
    }

    public class PaymentTypeCreateDTO
    {
        [Required(ErrorMessage = "Payment Code is required")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Code must be between 2 and 20 characters")]
        [Display(Name = "Payment Code *")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "Payment Type Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Payment Type *")]
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

        // Hidden fields
        public string? CompanyCode { get; set; }
        public string? CreatedBy { get; set; }
    }
}