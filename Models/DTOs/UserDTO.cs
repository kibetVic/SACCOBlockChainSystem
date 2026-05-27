using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class UserDTO
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [Display(Name = "Username")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "Login ID is required")]
        [StringLength(50, ErrorMessage = "Login ID cannot exceed 50 characters")]
        [Display(Name = "Login ID")]
        public string UserLoginId { get; set; } = null!;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }

        [Display(Name = "User Group/Role")]
        public string? UserGroup { get; set; }

        [Display(Name = "Company Code")]
        public string? CompanyCode { get; set; }

        [Display(Name = "Member Number")]
        public string? MemberNo { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNo { get; set; }

        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Display(Name = "Sub-County")]
        public string? SubCounty { get; set; }

        [Display(Name = "Ward")]
        public string? Ward { get; set; }

        [Display(Name = "CIG Code")]
        public string? Cigcode { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "User Status")]
        public string? Userstatus { get; set; }

        [Display(Name = "Password Status")]
        public string? PasswordStatus { get; set; }

        [Display(Name = "Approval Status")]
        public string? ApprovalStatus { get; set; }

        [Display(Name = "Is Locked")]
        public bool? IsLocked { get; set; }

        [Display(Name = "Date Created")]
        public DateTime? DateCreated { get; set; }
    }

    public class UserListDTO
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string UserLoginId { get; set; } = null!;
        public string? UserGroup { get; set; }
        public string? CompanyCode { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; }
        public DateTime? DateCreated { get; set; }
        public bool IsLocked { get; set; }
        public string Department { get; internal set; }
        public string CompanyName { get; internal set; }
    }   

    // ============================================================
    // USER DETAILS DTO
    // ============================================================

    public class UserDetailsDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserLoginId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Department { get; set; } = null!;
        public string SubCounty { get; set; } = null!;
        public string Ward { get; set; } = null!;
        public string UserGroup { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Userstatus { get; set; } = null!;
        public string ApprovalStatus { get; set; } = null!;
        public bool IsLocked { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
        public string? PasswordStatus { get; set; }
        public string? PassExpire { get; set; }
    }

    // ============================================================
    // USER RESPONSE DTO
    // ============================================================

    public class UserActionResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public string? BlockchainTxId { get; set; }
        public int? UserId { get; set; }
    }
}