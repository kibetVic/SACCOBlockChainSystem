using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // =========================
    // Dividend Details DTO
    // =========================
    public class DividendDetailsDTO
    {
        [Required]
        [Display(Name = "Dividend Year")]
        public int DividendYear { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Member Number")]
        public string MemberNo { get; set; } = null!;

        [Display(Name = "Weighted Savings")]
        public decimal WeightedSavings { get; set; }

        [Display(Name = "Savings Dividend")]
        public decimal SavingsDividend { get; set; }

        [Display(Name = "Share Dividend")]
        public decimal ShareDividend { get; set; }

        [Display(Name = "Gross Dividend")]
        public decimal GrossDividend { get; set; }

        [Display(Name = "Withholding Tax")]
        public decimal WithholdingTax { get; set; }

        [Display(Name = "Net Dividend")]
        public decimal NetDividend { get; set; }

        [StringLength(50)]
        [Display(Name = "Company Code")]
        public string? CompanyCode { get; set; }
        public DividendStatus Status { get; set; }
    }

    // =========================
    // Response DTO (for listing / API)
    // =========================
    public class DividendDetailsResponseDTO
    {
        public int Id { get; set; }

        public int DividendYear { get; set; }

        public string MemberNo { get; set; } = null!;

        public decimal WeightedSavings { get; set; }

        public decimal SavingsDividend { get; set; }

        public decimal ShareDividend { get; set; }

        public decimal GrossDividend { get; set; }

        public decimal WithholdingTax { get; set; }

        public decimal NetDividend { get; set; }

        public string? CompanyCode { get; set; }
        public DividendStatus Status { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? BlockchainTxId { get; set; }
    }
}