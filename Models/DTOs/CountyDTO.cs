// LocationDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // ============================================================
    // COUNTY DTOs
    // ============================================================

    public class CountyDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "County Code is required")]
        [StringLength(10)]
        [Display(Name = "County Code")]
        public string CountyCode { get; set; } = null!;

        [Required(ErrorMessage = "County Name is required")]
        [StringLength(100)]
        [Display(Name = "County Name")]
        public string CountyName { get; set; } = null!;

        [StringLength(100)]
        [Display(Name = "County Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(100)]
        [Display(Name = "Region")]
        public string? Region { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? BlockchainTxId { get; set; }

        // Additional display properties
        public int SubCountyCount { get; set; }
    }

    public class CreateCountyDTO
    {
        [Required(ErrorMessage = "County Code is required")]
        [StringLength(10)]
        [Display(Name = "County Code")]
        public string CountyCode { get; set; } = null!;

        [Required(ErrorMessage = "County Name is required")]
        [StringLength(100)]
        [Display(Name = "County Name")]
        public string CountyName { get; set; } = null!;

        [StringLength(100)]
        [Display(Name = "County Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(100)]
        [Display(Name = "Region")]
        public string? Region { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
    }

    public class UpdateCountyDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "County Code is required")]
        [StringLength(10)]
        [Display(Name = "County Code")]
        public string CountyCode { get; set; } = null!;

        [Required(ErrorMessage = "County Name is required")]
        [StringLength(100)]
        [Display(Name = "County Name")]
        public string CountyName { get; set; } = null!;

        [StringLength(100)]
        [Display(Name = "County Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(100)]
        [Display(Name = "Region")]
        public string? Region { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? ModifiedBy { get; set; }
    }

    // ============================================================
    // SUBCOUNTY DTOs
    // ============================================================

    public class SubCountyDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SubCounty Code is required")]
        [StringLength(20)]
        [Display(Name = "SubCounty Code")]
        public string SubCountyCode { get; set; } = null!;

        [Required(ErrorMessage = "SubCounty Name is required")]
        [StringLength(100)]
        [Display(Name = "SubCounty Name")]
        public string SubCountyName { get; set; } = null!;

        public int CountyId { get; set; }

        [Display(Name = "County")]
        public string? CountyName { get; set; }

        [StringLength(100)]
        [Display(Name = "Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? BlockchainTxId { get; set; }

        public int WardCount { get; set; }
    }

    public class CreateSubCountyDTO
    {
        [Required(ErrorMessage = "SubCounty Code is required")]
        [StringLength(20)]
        [Display(Name = "SubCounty Code")]
        public string SubCountyCode { get; set; } = null!;

        [Required(ErrorMessage = "SubCounty Name is required")]
        [StringLength(100)]
        [Display(Name = "SubCounty Name")]
        public string SubCountyName { get; set; } = null!;

        [Required(ErrorMessage = "County is required")]
        public int CountyId { get; set; }

        [StringLength(100)]
        [Display(Name = "Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
    }

    public class UpdateSubCountyDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SubCounty Code is required")]
        [StringLength(20)]
        [Display(Name = "SubCounty Code")]
        public string SubCountyCode { get; set; } = null!;

        [Required(ErrorMessage = "SubCounty Name is required")]
        [StringLength(100)]
        [Display(Name = "SubCounty Name")]
        public string SubCountyName { get; set; } = null!;

        [Required(ErrorMessage = "County is required")]
        public int CountyId { get; set; }

        [StringLength(100)]
        [Display(Name = "Headquarters")]
        public string? Headquarters { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? ModifiedBy { get; set; }
    }

    // ============================================================
    // WARD DTOs
    // ============================================================

    public class WardDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ward Code is required")]
        [StringLength(20)]
        [Display(Name = "Ward Code")]
        public string WardCode { get; set; } = null!;

        [Required(ErrorMessage = "Ward Name is required")]
        [StringLength(100)]
        [Display(Name = "Ward Name")]
        public string WardName { get; set; } = null!;

        public int SubCountyId { get; set; }

        [Display(Name = "SubCounty")]
        public string? SubCountyName { get; set; }

        [Display(Name = "County")]
        public string? CountyName { get; set; }

        [StringLength(100)]
        [Display(Name = "Constituency")]
        public string? Constituency { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    public class CreateWardDTO
    {
        [Required(ErrorMessage = "Ward Code is required")]
        [StringLength(20)]
        [Display(Name = "Ward Code")]
        public string WardCode { get; set; } = null!;

        [Required(ErrorMessage = "Ward Name is required")]
        [StringLength(100)]
        [Display(Name = "Ward Name")]
        public string WardName { get; set; } = null!;

        [Required(ErrorMessage = "SubCounty is required")]
        public int SubCountyId { get; set; }

        [StringLength(100)]
        [Display(Name = "Constituency")]
        public string? Constituency { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? CreatedBy { get; set; }
    }

    public class UpdateWardDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ward Code is required")]
        [StringLength(20)]
        [Display(Name = "Ward Code")]
        public string WardCode { get; set; } = null!;

        [Required(ErrorMessage = "Ward Name is required")]
        [StringLength(100)]
        [Display(Name = "Ward Name")]
        public string WardName { get; set; } = null!;

        [Required(ErrorMessage = "SubCounty is required")]
        public int SubCountyId { get; set; }

        [StringLength(100)]
        [Display(Name = "Constituency")]
        public string? Constituency { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";

        public string? ModifiedBy { get; set; }
    }

    // ============================================================
    // LOCATION RESPONSE DTOs
    // ============================================================

    public class LocationResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public object? Data { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    public class CountyListResponseDTO
    {
        public List<CountyDTO> Counties { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
    }

    public class SubCountyListResponseDTO
    {
        public List<SubCountyDTO> SubCounties { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
    }

    public class WardListResponseDTO
    {
        public List<WardDTO> Wards { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
    }
}