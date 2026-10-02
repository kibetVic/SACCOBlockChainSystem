// Models/DTOs/InvoiceItemDTO.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class InvoiceItemDTO
    {
        public long? Id { get; set; }

        [Required(ErrorMessage = "Item description is required")]
        [StringLength(200)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 999999, ErrorMessage = "Quantity must be at least 1")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit price is required")]
        [Range(0.01, 999999999.99, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        public decimal Total => Quantity * UnitPrice;

        public long? InvoiceId { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }
    }
}