using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public enum LoanStatus
    {
        Draft = 1,
        Submitted = 2,
        UnderAppraisal = 3,
        Approved = 4,
        Rejected = 5,
        Disbursed = 6,
        Closed = 7,
        Defaulted = 8,
        WrittenOff = 9
    }

    public class Loan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string LoanNo { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string MemberNo { get; set; } = null!; // Changed from MemberId int

        [Required]
        [StringLength(50)]
        public string LoanCode { get; set; } = null!; // Changed from LoanTypeId int

        [Required]
        [StringLength(50)]
        public string CompanyCode { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrincipalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ApprovedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DisbursedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingPrincipal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingInterest { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingPenalty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOutstanding { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal InterestRate { get; set; }

        public int RepaymentPeriod { get; set; }

        public int RepaymentFrequency { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InstallmentAmount { get; set; }

        public DateTime ApplicationDate { get; set; }

        public DateTime? ApprovalDate { get; set; }

        public DateTime? DisbursementDate { get; set; }

        public DateTime? FirstPaymentDate { get; set; }

        public DateTime? MaturityDate { get; set; }

        [StringLength(30)]
        public string LoanStatus { get; set; } = "Draft"; // Using string to match SQL

        [StringLength(500)]
        public string Purpose { get; set; } = null!;

        [StringLength(500)]
        public string? Remarks { get; set; }

        public int? LoanTypeId { get; set; }

        public bool HasGuarantors { get; set; }

        public int RequiredGuarantors { get; set; }

        public int AssignedGuarantors { get; set; }

        public bool GuarantorsApproved { get; set; }

        public bool AppraisalCompleted { get; set; }

        public DateTime? AppraisalDate { get; set; }

        [StringLength(100)]
        public string? AppraisedBy { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProcessingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InsuranceFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LegalFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetDisbursement { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(255)]
        public string? BlockchainTxId { get; set; }

        // Navigation Properties
        [ForeignKey("MemberNo")]
        public virtual Member? Member { get; set; }

        [ForeignKey("LoanTypeId")]
        public virtual Loantype? LoanType { get; set; }

        public virtual ICollection<LoanGuarantor> LoanGuarantors { get; set; } = new List<LoanGuarantor>();
        public virtual ICollection<LoanAppraisal> LoanAppraisals { get; set; } = new List<LoanAppraisal>();
        public virtual ICollection<LoanApproval> LoanApprovals { get; set; } = new List<LoanApproval>();
        public virtual ICollection<LoanDisbursement> LoanDisbursements { get; set; } = new List<LoanDisbursement>();
        public virtual ICollection<LoanSchedule> LoanSchedules { get; set; } = new List<LoanSchedule>();
        public virtual ICollection<LoanRepayment> LoanRepayments { get; set; } = new List<LoanRepayment>();
        public virtual ICollection<LoanDocument> LoanDocuments { get; set; } = new List<LoanDocument>();
        public virtual ICollection<LoanAuditTrail> LoanAuditTrails { get; set; } = new List<LoanAuditTrail>();
    }
}