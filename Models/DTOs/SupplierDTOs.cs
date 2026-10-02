using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class SupplierDTO
    {
        public long? Id { get; set; }

        [StringLength(50)]
        public string? SupplierCode { get; set; }

        [Required(ErrorMessage = "Supplier Name is required")]
        [StringLength(200)]
        public string? SupplierName { get; set; }

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [StringLength(50)]
        public string? PhoneNo { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address")]
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

        public bool? IsActive { get; set; } = true;

        [Range(0, double.MaxValue, ErrorMessage = "Opening Balance must be a positive number")]
        public decimal? OpeningBalance { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class SupplierResponseDTO
    {
        public long Id { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? ContactPerson { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? PhysicalAddress { get; set; }
        public string? PostalAddress { get; set; }
        public string? City { get; set; }
        public string? County { get; set; }
        public string? Country { get; set; }
        public string? PinNo { get; set; }
        public string? VatNo { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankBranch { get; set; }
        public string? GlAccountNo { get; set; }
        public string? GlAccountName { get; set; }
        public string? CompanyCode { get; set; }
        public bool? IsActive { get; set; }
        public string? Status { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? CurrentBalance { get; set; }
        public string? Remarks { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
    }

    public class SupplierSearchDTO
    {
        public string? SupplierName { get; set; }
        public string? SupplierCode { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public bool? IsActive { get; set; }
        public string? CompanyCode { get; set; }
    }

    public class SupplierViewModel
    {
        public List<SupplierResponseDTO> Suppliers { get; set; } = new List<SupplierResponseDTO>();
        public int TotalSuppliers { get; set; }
        public int ActiveSuppliers { get; set; }
        public int InactiveSuppliers { get; set; }
        public int BlockchainVerifiedCount { get; set; }
        public decimal TotalSupplierBalance { get; set; }
        public string? UserCompanyCode { get; set; }

        // For GL Account dropdown
        public List<GlAccountDTO> GlAccounts { get; set; } = new List<GlAccountDTO>();
    }

    public class GlAccountDTO
    {
        public long GlId { get; set; }
        public string? Glcode { get; set; }
        public string? Glaccname { get; set; }
        public string? AccNo { get; set; }
        public string? Glacctype { get; set; }
        public string? GlAccMainGroup { get; set; }
        public decimal? CurrentBal { get; set; }
        public string? DisplayName => $"{Glcode} - {Glaccname}";
    }


    #region InvoiceReceive DTOs
    public class InvoiceReceiveDTO
    {
        public long? Id { get; set; }

        [Required(ErrorMessage = "Invoice Number is required")]
        [StringLength(50)]
        public string? InvoiceNo { get; set; }

        [Required(ErrorMessage = "Supplier is required")]
        [StringLength(50)]
        public string? SupplierCode { get; set; }

        [StringLength(200)]
        public string? SupplierName { get; set; }

        [Required(ErrorMessage = "Invoice Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Invoice Amount must be greater than 0")]
        public decimal InvoiceAmount { get; set; }

        public decimal? AmountPaid { get; set; }

        public decimal? Balance { get; set; }

        [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
        public decimal? TaxPercentage { get; set; }

        public decimal? TaxAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Discount must be a positive number")]
        public decimal? DiscountAmount { get; set; }

        [DataType(DataType.Date)]
        public DateTime? InvoiceDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ReceivedDate { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? PurchaseOrderNo { get; set; }

        [StringLength(50)]
        public string? GlAccountNo { get; set; }

        [StringLength(100)]
        public string? GlAccountName { get; set; }

        [Required(ErrorMessage = "Company Code is required")]
        [StringLength(50)]
        public string? CompanyCode { get; set; }

        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal? TotalAmount { get; set; }

        [StringLength(50)]
        public string? ReceiptNo { get; set; }

        public string? BlockchainTxId { get; set; }

        // NEW: Invoice Items
        public List<InvoiceItemDTO> InvoiceItems { get; set; } = new List<InvoiceItemDTO>();
    }

    public class InvoiceReceiveResponseDTO
    {
        public long Id { get; set; }
        public string? InvoiceNo { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? Balance { get; set; }
        public decimal? TaxPercentage { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? Description { get; set; }
        public string? PurchaseOrderNo { get; set; }
        public string? GlAccountNo { get; set; }
        public string? GlAccountName { get; set; }
        public string? CompanyCode { get; set; }
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public int PaymentCount { get; set; }
        public decimal? TotalPaid { get; set; }
        public int DaysOverdue { get; set; }

        // Invoice Items
        public List<InvoiceItemResponseDTO> InvoiceItems { get; set; } = new List<InvoiceItemResponseDTO>();
    }

    // NEW: Invoice Item Response DTO
    public class InvoiceItemResponseDTO
    {
        public long Id { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public long InvoiceId { get; set; }
        public string? CompanyCode { get; set; }
        public string? BlockchainTxId { get; set; }
    }


    public class InvoiceSearchDTO
    {
        public string? InvoiceNo { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? CompanyCode { get; set; }
    }

    public class InvoiceReceiveViewModel
    {
        public List<InvoiceReceiveResponseDTO> Invoices { get; set; } = new();
        public int TotalInvoices { get; set; }
        public int PendingInvoices { get; set; }
        public int PartiallyPaidInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public int OverdueInvoices { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
        public decimal TotalOutstanding { get; set; }
        public string? UserCompanyCode { get; set; }
        public List<SupplierResponseDTO> Suppliers { get; set; } = new();
        public List<GlAccountDTO> GlAccounts { get; set; } = new();
    }

    #endregion

    #region InvoicePayment DTOs

    public class InvoicePaymentDTO
    {
        public long? Id { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        public decimal? OpeningBalance { get; set; }

        [StringLength(50)]
        public string? SupplierId { get; set; }

        [StringLength(250)]
        public string? Particulars { get; set; }

        [DataType(DataType.Date)]
        public DateTime? TransDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? OpeningDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [StringLength(50)]
        public string? ChequeNo { get; set; }

        [Required(ErrorMessage = "Invoice Number is required")]
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

        public string? BlockchainTxId { get; set; }
    }

    public class InvoicePaymentResponseDTO
    {
        public long Id { get; set; }
        public string? CompanyCode { get; set; }
        public decimal Amount { get; set; }
        public decimal? OpeningBalance { get; set; }
        public string? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? Particulars { get; set; }
        public DateTime? TransDate { get; set; }
        public DateTime? OpeningDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? ChequeNo { get; set; }
        public string? InvoiceNo { get; set; }
        public string? Remarks { get; set; }
        public string? Transtype { get; set; }
        public string? SupplierAccno { get; set; }
        public string? DebitAccno { get; set; }
        public string? SupplierAccName { get; set; }
        public string? DebitAccName { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
    }

    public class InvoicePaymentViewModel
    {
        public List<InvoicePaymentResponseDTO> Payments { get; set; } = new();
        public int TotalPayments { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public string? UserCompanyCode { get; set; }
        public List<SupplierResponseDTO> Suppliers { get; set; } = new();
        public List<InvoiceReceiveResponseDTO> Invoices { get; set; } = new();
        public List<GlAccountDTO> GlAccounts { get; set; } = new();
    }

    #endregion
    public class PaymentReceiptViewModel
    {
        public string? ReceiptNo { get; set; }
        public string? InvoiceNo { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierContact { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierEmail { get; set; }
        public decimal Amount { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? BalanceAfter { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ChequeNo { get; set; }
        public string? TransactionType { get; set; }
        public string? Particulars { get; set; }
        public string? Remarks { get; set; }
        public string? DebitAccountNo { get; set; }
        public string? DebitAccountName { get; set; }
        public string? SupplierAccountNo { get; set; }
        public string? SupplierAccountName { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }

        // Company details
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyLogo { get; set; }

        // Payment status
        public string? PaymentStatus { get; set; }
        public decimal? TotalPaid { get; set; }
        public List<InvoiceItemResponseDTO> InvoiceItems { get; set; } = new List<InvoiceItemResponseDTO>();
        public decimal InvoiceTotal { get; set; }
    }
}