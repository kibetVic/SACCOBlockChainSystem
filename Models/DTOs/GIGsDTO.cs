using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class GIGsDTO
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "GIG Code is required")]
        [StringLength(50)]
        [Display(Name = "GIG Code")]
        public string GigCode { get; set; } = null!;

        [Required(ErrorMessage = "GIG Name is required")]
        [StringLength(200)]
        [Display(Name = "GIG Name")]
        public string? GigName { get; set; }

        // CompanyCode will be taken from logged-in user, not from form
        [Display(Name = "Company Code")]
        public string? CompanyCode { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [StringLength(50)]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(100)]
        [Display(Name = "Contact Email")]
        public string? ContactEmail { get; set; }

        [StringLength(100)]
        [Display(Name = "Chairperson")]
        public string? Chairperson { get; set; }

        [Display(Name = "Registration Date")]
        [DataType(DataType.Date)]
        public DateTime? RegistrationDate { get; set; }

        [Display(Name = "Total Members")]
        [Range(0, int.MaxValue, ErrorMessage = "Total Members must be a positive number")]
        public int? TotalMembers { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string? Status { get; set; } = "Active";
    }

    public class GIGsResponseDTO
    {
        public int Id { get; set; }
        public string GigCode { get; set; } = null!;
        public string? GigName { get; set; }
        public string? CompanyCode { get; set; }
        public string? CompanyName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Chairperson { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public int? TotalMembers { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? BlockchainTxId { get; set; }
    }

    // DTO for a single member within a CIG
    public class MemberPerCIGDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }
        public DateTime? JoinedDate { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Gender { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? Rank { get; set; }
        public decimal? ShareCapital { get; set; }
        public bool? IsActive { get; set; }
    }

    // DTO for a CIG with its members
    public class CIGReportDTO
    {
        public string CIGCode { get; set; } = null!;
        public string CIGName { get; set; } = null!;
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Chairperson { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? Status { get; set; }
        public int TotalMembers { get; set; }
        public List<MemberPerCIGDTO> Members { get; set; } = new List<MemberPerCIGDTO>();
    }

    // Main report view model
    public class MembersPerCIGReportViewModel
    {
        public string CompanyName { get; set; } = "SACCO BlockChain System";
        public string CompanyCode { get; set; } = null!;
        public DateTime ReportDate { get; set; } = DateTime.Now;
        public string PrintedBy { get; set; } = "System";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalCIGs { get; set; }
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int InactiveMembers { get; set; }
        public List<CIGReportDTO> CIGs { get; set; } = new List<CIGReportDTO>();

        // For filtering
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
    }
}
