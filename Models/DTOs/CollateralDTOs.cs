// Models/DTOs/CollateralDTO.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class CollateralDTO
    {
        public long? Id { get; set; }

        [Required(ErrorMessage = "Collateral code is required")]
        [StringLength(50)]
        public string ColCode { get; set; } = null!;

        [Required(ErrorMessage = "Collateral description is required")]
        [StringLength(100)]
        public string Coldescription { get; set; } = null!;

        [Range(0, 100, ErrorMessage = "Percentage must be between 0 and 100")]
        public double Percentage { get; set; }

        [StringLength(50)]
        public string? MemberNo { get; set; }

        public string? CompanyCode { get; set; }

        // Photo upload - not stored in database directly, used for receiving file
        public IFormFile? PhotoFile { get; set; }

        // Base64 photo for display (populated when reading from DB)
        public string? PhotoBase64 { get; set; }
    }

    public class CollateralResponseDTO
    {
        public long Id { get; set; }
        public string ColCode { get; set; } = null!;
        public string Coldescription { get; set; } = null!;
        public double Percentage { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? CompanyCode { get; set; }
        public string? BlockchainTxId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? PhotoBase64 { get; set; } // Base64 encoded photo for display
        public string? PhotoContentType { get; set; }
        public bool HasPhoto { get; set; }
    }

    public class CollateralReportDTO
    {
        public string? MemberNo { get; set; }
        public string? Names { get; set; }
        public string? LoanNo { get; set; }
        public string? ColCode { get; set; }
        public string? Coldescription { get; set; }
        public decimal Mktvalue { get; set; }
        public decimal Balance { get; set; }
        public double Percentage { get; set; }
    }
}