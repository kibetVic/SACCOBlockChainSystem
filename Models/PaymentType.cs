// Models/PaymentType.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("PaymentTypes")]
    public class PaymentType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Payment Type Code")]
        public string? Code { get; set; }  // e.g., SAL, COM, BON, ALL, REIMB, ADV, OVT

        [Required]
        [StringLength(100)]
        [Display(Name = "Payment Type Name")]
        public string? Name { get; set; }  // e.g., Salary, Commission, Bonus, Allowance

        [StringLength(250)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; } = 0;

        // Optional: Link to default expense account for this payment type
        [StringLength(50)]
        [Display(Name = "Default Expense Account No")]
        public string? DefaultExpenseAccountNo { get; set; }

        [StringLength(50)]
        [Display(Name = "Company Code")]
        public string? CompanyCode { get; set; }

        [StringLength(50)]
        [Display(Name = "Created By")]
        public string? AuditId { get; set; }

        [Display(Name = "Created At")]
        public DateTime AuditTime { get; set; } = DateTime.Now;

        [StringLength(255)]
        [Display(Name = "Blockchain Transaction ID")]
        public string? BlockchainTxId { get; set; }
    }
}