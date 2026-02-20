using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class MemberRegistrationVm
    {
        // Company Information
        [Display(Name = "Company Code")]
        public string CompanyCode { get; set; } = string.Empty;

        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        // Member Number - Auto-generated but editable
        [Required(ErrorMessage = "Member Number is required")]
        [Display(Name = "Member No.")]
        public string MemberNo { get; set; } = string.Empty;

        // Personal Information
        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, ErrorMessage = "Surname cannot exceed 50 characters")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Other names are required")]
        [StringLength(100, ErrorMessage = "Other names cannot exceed 100 characters")]
        public string OtherNames { get; set; } = string.Empty;

        [Required(ErrorMessage = "ID Number is required")]
        [StringLength(20, ErrorMessage = "ID Number cannot exceed 20 characters")]
        [Display(Name = "ID Number")]
        public string IdNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNo { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? Email { get; set; }

        [Display(Name = "Address")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string? PresentAddr { get; set; }

        // Membership Type Radio Buttons
        [Required(ErrorMessage = "Please select membership type")]
        [Display(Name = "Membership Type")]
        public string MembershipType { get; set; } = "Individual"; // Individual or Corporate

        // Registration Type Radio Buttons
        [Required(ErrorMessage = "Please select registration type")]
        [Display(Name = "Registration Type")]
        public string RegistrationType { get; set; } = "Regular"; // Board Member, Regular, etc.

        // CIG Dropdown
        [Display(Name = "Group/OIC")]
        public string? CigCode { get; set; }

        [Display(Name = "Group Name")]
        public string? CigName { get; set; }

        // Date of Birth and Age
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Age")]
        [Range(18, 120, ErrorMessage = "Member must be at least 18 years old")]
        public int? Age { get; set; }

        // Other Fields
        public string? Gender { get; set; }
        public string? Employer { get; set; }
        public string? Dept { get; set; }

        [Range(0, 1000000, ErrorMessage = "Initial shares must be between 0 and 1,000,000")]
        [Display(Name = "Initial Shares")]
        public decimal InitialShares { get; set; } = 0;

        [Range(0, 5000, ErrorMessage = "Registration fee must be between 0 and 5,000")]
        [Display(Name = "Registration Fee")]
        public decimal? RegFee { get; set; } = 0;

        public bool Mstatus { get; set; } = true;

        // For dropdown lists
        public List<CompanySelectItem> Companies { get; set; } = new();
        public List<CigSelectItem> CigList { get; set; } = new();
        public List<SelectListItem> RegistrationTypes { get; set; } = new()
        {
            new SelectListItem { Value = "Regular", Text = "Regular Member" },
            new SelectListItem { Value = "BoardMember", Text = "Board Member" },
            new SelectListItem { Value = "Staff", Text = "Staff" },
            new SelectListItem { Value = "Associate", Text = "Associate Member" }
        };
    }

    public class CompanySelectItem
    {
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
    }

    public class CigSelectItem
    {
        public string CigCode { get; set; } = string.Empty;
        public string CigName { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
    }

    public class SelectListItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}