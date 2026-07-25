using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("Suppliers")]
    public class Supplier
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string? SupplierCode { get; set; }

        [Required]
        [StringLength(200)]
        public string? SupplierName { get; set; }

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [StringLength(50)]
        public string? PhoneNo { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? PhysicalAddress { get; set; }

        [StringLength(100)]
        public string? PostalAddress { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? County { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        [StringLength(50)]
        public string? PinNo { get; set; }

        [StringLength(50)]
        public string? VatNo { get; set; }

        [StringLength(50)]
        public string? BankName { get; set; }

        [StringLength(50)]
        public string? BankAccountNo { get; set; }

        [StringLength(100)]
        public string? BankAccountName { get; set; }

        [StringLength(50)]
        public string? BankBranch { get; set; }

        [StringLength(50)]
        public string? GlAccountNo { get; set; }

        [StringLength(100)]
        public string? GlAccountName { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        public bool? IsActive { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [Column(TypeName = "money")]
        public decimal? OpeningBalance { get; set; }

        [Column(TypeName = "money")]
        public decimal? CurrentBalance { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        [StringLength(50)]
        public string? AuditId { get; set; }

        public DateTime? AuditTime { get; set; }

        [StringLength(50)]
        public string? TransactionNo { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation Properties
        public virtual ICollection<InvoiceReceive>? Invoices { get; set; } = new List<InvoiceReceive>();
        public virtual ICollection<InvoicePayment>? Payments { get; set; } = new List<InvoicePayment>();
    }
}