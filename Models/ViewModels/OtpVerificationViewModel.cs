// Models/ViewModels/OtpVerificationViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class OtpVerificationViewModel
    {
        [Required(ErrorMessage = "OTP is required")]
        [Display(Name = "One-Time Password")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits")]
        public string Otp { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        public string? UserId { get; set; }

        public string? UserName { get; set; }

        public DateTime? OtpExpiry { get; set; }

        public bool IsOtpValid { get; set; }

        public int RemainingSeconds { get; set; }
        public string? Email { get;  set; }
        public bool RequireOtp { get; internal set; }
    }
}
