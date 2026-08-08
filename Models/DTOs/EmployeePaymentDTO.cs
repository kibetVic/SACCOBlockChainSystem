// DTOs/EmployeePaymentDTO.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class EmployeePaymentDTO
    {
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public long EmployeeId { get; set; }

        [Required(ErrorMessage = "Employee ID Number is required")]
        [Display(Name = "ID Number")]
        public string? EmployeeIdNo { get; set; }

        [Required(ErrorMessage = "Employee Name is required")]
        [Display(Name = "Employee Name")]
        public string? EmployeeName { get; set; }

        [Required(ErrorMessage = "Payment amount is required")]
        [Range(0.01, 999999999.99, ErrorMessage = "Amount must be greater than 0")]
        [DataType(DataType.Currency)]
        [Display(Name = "Payment Amount")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment type is required")]
        [Display(Name = "Payment Type")]
        public int PaymentTypeId { get; set; }

        [Display(Name = "Payment Type Name")]
        public string? PaymentTypeName { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [Display(Name = "Expense Account")]
        public string? ExpenseAccountNo { get; set; }

        [Display(Name = "Cash/Bank Account")]
        public string? CashAccountNo { get; set; }
    }

    public class EmployeePaymentResponseDTO
    {
        public long PaymentId { get; set; }
        public string? JournalVoucherNo { get; set; }
        public long EmployeeId { get; set; }
        public string? EmployeeIdNo { get; set; }
        public string? EmployeeName { get; set; }
        public decimal Amount { get; set; }
        public int PaymentTypeId { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentTypeCode { get; set; }
        public string? Description { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNo { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ExpenseAccountNo { get; set; }
        public string? ExpenseAccountName { get; set; }
        public string? CashAccountNo { get; set; }
        public string? CashAccountName { get; set; }
        public string? Status { get; set; }
        public string? BlockchainTxId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class EmployeePaymentListDTO
    {
        public long Id { get; set; }
        public string? VoucherNo { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeIdNo { get; set; }
        public decimal Amount { get; set; }
        public int PaymentTypeId { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentTypeCode { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Status { get; set; }
        public string? BlockchainTxId { get; set; }
        public object CreatedBy { get; internal set; }
    }

    public class PaymentSummaryDTO
    {
        public decimal TotalPayments { get; set; }
        public int PaymentCount { get; set; }
        public Dictionary<string, decimal> PaymentsByType { get; set; } = new();
        public Dictionary<string, decimal> PaymentsByTypeCode { get; set; } = new();
        public decimal TotalByCash { get; set; }
        public decimal TotalByBank { get; set; }
        public decimal TotalByCheque { get; set; }
    }
    public class EmployeePaymentReceiptViewModel
    {
        // Receipt Details
        public string? ReceiptNo { get; set; }
        public string? VoucherNo { get; set; }
        public string? TransactionNo { get; set; }

        // Employee Details
        public string? EmployeeIdNo { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeePhone { get; set; }
        public string? EmployeeEmail { get; set; }

        // Payment Details
        public decimal Amount { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentTypeCode { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ChequeNo { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }

        // Account Details
        public string? ExpenseAccountNo { get; set; }
        public string? ExpenseAccountName { get; set; }
        public string? CashAccountNo { get; set; }
        public string? CashAccountName { get; set; }

        // Status
        public string? Status { get; set; }

        // Blockchain
        public string? BlockchainTxId { get; set; }

        // Created By
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        // Company Details
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyLogo { get; set; }
    }
}

