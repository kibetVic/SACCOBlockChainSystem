// Models/ViewModels/SignupVm.cs
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class SignupVm
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Member Number")]
        public string MemberNo { get; set; }
        public string Department { get; set; }
        public string SubCounty { get; set; }
        public string Ward { get; set; }
        public object CompanyCode { get; set; }

        [Display(Name = "User Group/Role")]
        public string? UserGroup { get; set; }
        public List<string> AvailableUserGroups { get; set; } = new List<string>
    {
        "Member",
        "Teller",
        "LoanOfficer",
        "Auditor",
        "BoardMember",
        "Staff"
    };
    }
}