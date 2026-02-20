//// Models/BudgetHeader.cs
//using System;
//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace SACCOBlockChainSystem.Models
//{
//    [Table("BudgetHeader")]
//    public class BudgetHeader
//    {
//        [Key]
//        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
//        public int BudgetID { get; set; }

//        [Required]
//        [StringLength(255)]
//        public string BudgetName { get; set; } = string.Empty;

//        [StringLength(255)]
//        public string? UserID { get; set; }

//        [Required]
//        [Column(TypeName = "date")]
//        public DateTime StartDate { get; set; }

//        [Required]
//        [Column(TypeName = "date")]
//        public DateTime EndDate { get; set; }

//        [Required]
//        [Column(TypeName = "decimal(18,2)")]
//        public decimal TotalBudget { get; set; }

//        public DateTime? CreatedDate { get; set; }

//        // Navigation property
//        public virtual ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
//    }

//    [Table("BudgetEntries")]
//    public class BudgetEntry
//    {
//        [Key]
//        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
//        public int EntryID { get; set; }

//        [Required]
//        public int BudgetID { get; set; }

//        [StringLength(50)]
//        public string? AccountNumber { get; set; }

//        [StringLength(255)]
//        public string? AccountName { get; set; }

//        [Required]
//        [Column(TypeName = "decimal(18,2)")]
//        public decimal Amount { get; set; }

//        [Column(TypeName = "decimal(18,2)")]
//        public decimal? CurrentBudget { get; set; }

//        [Column(TypeName = "date")]
//        public DateTime? EntryDate { get; set; }

//        public string? Notes { get; set; }

//        // Navigation property
//        [ForeignKey("BudgetID")]
//        public virtual BudgetHeader BudgetHeader { get; set; } = null!;
//    }
//}