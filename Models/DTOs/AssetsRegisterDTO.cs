using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class AssetsRegisterDTO
    {
        public long? Id { get; set; }

        [Display(Name = "Class")]
        [StringLength(50)]
        public string? Class { get; set; }

        [Required(ErrorMessage = "Asset Type is required")]
        [Display(Name = "Asset Type")]
        [StringLength(50)]
        public string? AssetType { get; set; }

        [Required(ErrorMessage = "Asset Name is required")]
        [Display(Name = "Asset Name")]
        [StringLength(50)]
        public string? AssetName { get; set; }

        [Display(Name = "Tag Number")]
        [StringLength(50)]
        public string? TagNo { get; set; }

        [Display(Name = "Serial Number")]
        [StringLength(50)]
        public string? SerialNo { get; set; }

        [Display(Name = "Quantity")]
        [Range(1, 999999999, ErrorMessage = "Please enter a valid whole number")]
        public int? Quantity { get; set; }

        [Display(Name = "Actual Value")]
        [Range(0, 9999999999.99, ErrorMessage = "Please enter a valid amount")]
        public decimal? ActualValue { get; set; }

        [Display(Name = "Market Value")]
        [Range(0, 9999999999.99, ErrorMessage = "Please enter a valid amount")]
        public decimal? MarketValue { get; set; }

        [Display(Name = "Total Value")]
        public decimal? TotalValue { get; set; }

        [Display(Name = "Date of Manufacture")]
        [DataType(DataType.Date)]
        public DateTime? DateOfManufacture { get; set; }

        [Display(Name = "Date Purchased")]
        [DataType(DataType.Date)]
        public DateTime? DatePurchased { get; set; }

        [Display(Name = "Transaction Number")]
        [StringLength(50)]
        public string? TransactionNo { get; set; }

        [Required(ErrorMessage = "Company Code is required")]
        [Display(Name = "Company")]
        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [Display(Name = "Location")]
        [StringLength(50)]
        public string? Location { get; set; }

        [Display(Name = "Posted")]
        public bool? Posted { get; set; }

        public string? AuditId { get; set; }
        public DateTime? AuditTime { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    public class AssetsRegisterResponseDTO
    {
        public long Id { get; set; }
        public string? Class { get; set; }
        public string? AssetType { get; set; }
        public string? AssetName { get; set; }
        public string? TagNo { get; set; }
        public string? SerialNo { get; set; }
        public int? Quantity { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? MarketValue { get; set; }
        public decimal? TotalValue { get; set; }
        public DateTime? DateOfManufacture { get; set; }
        public DateTime? DatePurchased { get; set; }
        public string? TransactionNo { get; set; }
        public string? CompanyCode { get; set; }
        public string? Location { get; set; }
        public bool? Posted { get; set; }
        public string? AuditId { get; set; }
        public DateTime? AuditTime { get; set; }
        public string? BlockchainTxId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
    }

    public class AssetsRegisterSearchDTO
    {
        public string? AssetName { get; set; }
        public string? AssetType { get; set; }
        public string? Class { get; set; }
        public string? TagNo { get; set; }
        public string? SerialNo { get; set; }
        public string? Location { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? CompanyCode { get; set; }
    }
}