using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models
{
    public partial class Loan
    {
        public int Id { get; set; }
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string? LoanCode { get; set; }
        public DateTime ApplicDate { get; set; }
        public decimal? LoanAmt { get; set; }
        public int? RepayPeriod { get; set; }
        public decimal? PremiumPayable { get; set; }
        public decimal? Phcf { get; set; }
        public decimal? TotalPremium { get; set; }
        public string CompanyCode { get; set; } = null!;
        public string? IdNo { get; set; }
        public string? JobGrp { get; set; }
        public decimal? BasicSalary { get; set; }
        public string? WitMemberNo { get; set; }
        public string? WitSigned { get; set; }
        public string? SupMemberNo { get; set; }
        public string? SupSigned { get; set; }
        public string? PreparedBy { get; set; }
        public string? Purpose { get; set; }
        public string? AddSecurity { get; set; }
        public decimal? Insurance { get; set; }
        public decimal? InsPercent { get; set; }
        public int? InsCalcType { get; set; }
        public string? Posted { get; set; }
        public string? AuditId { get; set; }
        public DateTime AuditTime { get; set; }
        public decimal? Aamount { get; set; }
        public string? Guaranteed { get; set; }
        public decimal? Cshares { get; set; }
        public decimal? MaxLoanamt { get; set; }
        public decimal? Grosspay { get; set; }
        public string? Refinancing { get; set; }
        public long? Loancount { get; set; }
        public bool? Bridging { get; set; }
        public string? RepayMethod { get; set; }
        public decimal? Interest { get; set; }
        public string? TransactionNo { get; set; }

        // New fields for loan processing workflow
        public int? Status { get; set; } // 1=Pending, 2=Guarantors, 3=Appraisal, 4=Endorsement, 5=Approved, 6=Disbursed, 7=Rejected, 8=Cancelled
        public string? StatusDescription { get; set; }

        public string? Sourceofrepayment { get; set; }
        public decimal? Repayrate { get; set; }
        public decimal? Sharecapital { get; set; }
        public DateTime? Rescheduledate { get; set; }
        public string? Gperiod { get; set; }
        public int? Run { get; set; }
        public int? Run2 { get; set; }
        public string? ApiKey { get; set; }
        public string? UserName { get; set; }
        public string? SerialNo { get; set; }
        public DateTime? AuditDateTime { get; set; }
        public string? BlockchainTxId { get; set; }

        // Navigation Properties
        public virtual Member? Member { get; set; }
        public virtual Loantype? LoanType { get; set; }
        public virtual ICollection<Loanbal> Loanbals { get; set; } = new List<Loanbal>();
        public virtual ICollection<Repay> Repays { get; set; } = new List<Repay>();
        public virtual ICollection<Loanguar> LoanGuarantors { get; set; } = new List<Loanguar>();
    }
}