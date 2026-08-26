namespace SACCOBlockChainSystem.ViewModels
{
    using System.ComponentModel.DataAnnotations;

    public class WalletPinSetup
    {
        [Required]
        public string MemberNo { get; set; } = string.Empty;

        public string? UserPin { get; set; }

        [Required]
        [Display(Name = "PIN")]
        [StringLength(6, MinimumLength = 4,
            ErrorMessage = "PIN must be between 4 and 6 digits.")]
        [RegularExpression(@"^\d{4,6}$",
            ErrorMessage = "PIN must contain only numbers.")]
        public string Pin { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Confirm PIN")]
        [Compare("Pin",
            ErrorMessage = "PIN confirmation does not match.")]
        public string ConfirmPin { get; set; } = string.Empty;
    }
}