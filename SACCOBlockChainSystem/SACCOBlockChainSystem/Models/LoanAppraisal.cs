using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public enum AppraisalDecision
    {
        Pending = 1,
        Recommend = 2,
        NotRecommend = 3,
        CounterOffer = 4
    }

    public class LoanAppraisal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!; // Changed from LoanApplicationId

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AppliedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RecommendedAmount { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal RecommendedInterestRate { get; set; }

        public int RecommendedPeriod { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MemberSharesValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MemberDepositsValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyIncome { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExistingLoanObligations { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DisposableIncome { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DebtToIncomeRatio { get; set; }

        public int CreditScore { get; set; }

        public bool ExistingLoanDefault { get; set; }

        public int LoanHistoryRating { get; set; }

        [StringLength(20)]
        public string AppraisalDecision { get; set; } = "Pending"; // Using string to match SQL

        [StringLength(1000)]
        public string AppraisalNotes { get; set; } = null!;

        [StringLength(500)]
        public string? RiskFactors { get; set; }

        [StringLength(500)]
        public string? MitigationFactors { get; set; }

        [StringLength(100)]
        public string AppraisedBy { get; set; } = null!;

        public DateTime AppraisalDate { get; set; }

        [StringLength(100)]
        public string? VerifiedBy { get; set; }

        public DateTime? VerifiedDate { get; set; }

        public bool IsFinal { get; set; }

        // Navigation Property
        [ForeignKey("LoanNo")]
        public virtual Loan? Loan { get; set; }
    }
}