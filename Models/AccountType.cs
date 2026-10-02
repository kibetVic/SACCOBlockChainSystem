// Models/AccountType.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static SACCOBlockChainSystem.Controllers.GlSetupController;

namespace SACCOBlockChainSystem.Models
{
    [Table("GLAccountTypes")]
    public class AccountType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Account Type")]
        public string Type { get; set; } = null!;

        [StringLength(50)]
        [Display(Name = "Normal Balance")]
        public string? NormalBalance { get; set; }

        [StringLength(255)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        // Navigation property
        public virtual ICollection<AccountGroup> AccountGroups { get; set; } = new List<AccountGroup>();
    }
}