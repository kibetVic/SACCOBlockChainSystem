using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    public class InvoiceReceive
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string? InvoiceNo { get; set; }

        [Required]
        [StringLength(50)]
        public string? SupplierCode { get; set; }

        [StringLength(200)]
        public string? SupplierName { get; set; }

        [Column(TypeName = "money")]
        public decimal InvoiceAmount { get; set; }

        [Column(TypeName = "money")]
        public decimal? AmountPaid { get; set; }

        [Column(TypeName = "money")]
        public decimal? Balance { get; set; }

        // Tax Percentage (e.g., 16 for 16%)
        public decimal? TaxPercentage { get; set; }

        [Column(TypeName = "money")]
        public decimal? TaxAmount { get; set; }

        [Column(TypeName = "money")]
        public decimal? DiscountAmount { get; set; }

        public DateTime? InvoiceDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? ReceivedDate { get; set; }

        // Removed Description field - now using line items

        [StringLength(50)]
        public string? PurchaseOrderNo { get; set; }

        [StringLength(50)]
        public string? GlAccountNo { get; set; }

        [StringLength(100)]
        public string? GlAccountName { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [StringLength(50)]
        public string? PaymentStatus { get; set; }

        [Column(TypeName = "money")]
        public decimal? TotalAmount { get; set; }

        [StringLength(50)]
        public string? ReceiptNo { get; set; }

        [StringLength(50)]
        public string? AuditId { get; set; }

        public DateTime? AuditTime { get; set; }

        [StringLength(50)]
        public string? TransactionNo { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(SupplierCode))]
        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<InvoicePayment>? Payments { get; set; } = new List<InvoicePayment>();

        // NEW: Invoice Items
        public virtual ICollection<InvoiceItem>? InvoiceItems { get; set; } = new List<InvoiceItem>();
    
    }    

    // NEW: Invoice Item Model
    [Table("InvoiceItems")]
    public class InvoiceItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public long InvoiceId { get; set; }

        [Required]
        [StringLength(200)]
        public string? Description { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "money")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "money")]
        public decimal Total { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        [ForeignKey(nameof(InvoiceId))]
        public virtual InvoiceReceive? Invoice { get; set; }
    }
}


