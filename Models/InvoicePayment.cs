using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("InvoicePayments")]
    public class InvoicePayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [Required]
        [Column(TypeName = "money")]
        public decimal Amount { get; set; }

        [Column(TypeName = "money")]
        public decimal? OpeningBalance { get; set; }

        [StringLength(50)]
        public string? SupplierId { get; set; }

        [StringLength(250)]
        public string? Particulars { get; set; }

        public DateTime? TransDate { get; set; }

        public DateTime? OpeningDate { get; set; }

        public DateTime? DueDate { get; set; }

        [StringLength(50)]
        public string? ChequeNo { get; set; }

        [StringLength(50)]
        public string? InvoiceNo { get; set; }

        [StringLength(250)]
        public string? Remarks { get; set; }

        [StringLength(10)]
        public string? Transtype { get; set; }

        [StringLength(50)]
        public string? SupplierAccno { get; set; }

        [StringLength(50)]
        public string? DebitAccno { get; set; }

        [StringLength(250)]
        public string? SupplierAccName { get; set; }

        [StringLength(250)]
        public string? DebitAccName { get; set; }

        [StringLength(50)]
        public string? ReceiptNo { get; set; }

        [StringLength(150)]
        public string? TransactionNo { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        [StringLength(50)]
        public string? AuditId { get; set; }

        public DateTime? AuditTime { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier? Supplier { get; set; }

        [ForeignKey(nameof(InvoiceNo))]
        public virtual InvoiceReceive? Invoice { get; set; }
    }
}