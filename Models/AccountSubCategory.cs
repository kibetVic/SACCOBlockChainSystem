// Models/AccountSubCategory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    [Table("GLAccSubCatego")]
    public class AccountSubCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Sub Category Name")]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(50)]
        [Display(Name = "Sub Category Code")]
        public string Code { get; set; } = null!;

        [StringLength(50)]
        [Display(Name = "Group Name")]
        public string GroupName { get; set; } = null!;

        [StringLength(255)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
    }

    // DTO for the subcategory
    public class AccountSubCategoryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string GroupName { get; set; } = null!;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}