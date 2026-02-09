using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // DTO for creating a loan type
    public class LoanTypeCreateDTO
    {
        [Required]
        [StringLength(20)]
        public string LoanCode { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string LoanType { get; set; } = null!;

        [StringLength(100)]
        public string? ValueChain { get; set; }

        [StringLength(100)]
        public string? LoanProduct { get; set; }

        [Required]
        [StringLength(20)]
        public string LoanAcc { get; set; } = null!;

        [StringLength(20)]
        public string? InterestAcc { get; set; }

        [StringLength(20)]
        public string? PenaltyAcc { get; set; }

        public int? RepayPeriod { get; set; }

        [StringLength(50)]
        public string? Interest { get; set; }

        public decimal? MaxAmount { get; set; }

        [StringLength(1)]
        public string? Guarantor { get; set; }

        public bool? UseIntRange { get; set; }

        public decimal? EarningRatio { get; set; }

        public bool Penalty { get; set; }

        public decimal? ProcessingFee { get; set; }

        public int GracePeriod { get; set; }

        [StringLength(20)]
        public string? RepayMethod { get; set; }

        public bool Bridging { get; set; }

        public bool SelfGuarantee { get; set; }

        public bool MobileLoan { get; set; }

        [Required]
        [StringLength(20)]
        public string Ppacc { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string ContraAccount { get; set; } = null!;

        public decimal? MinLoanAmount { get; set; }

        public decimal? MaxLoanAmount { get; set; }

        public int? MaxLoans { get; set; }

        public int Priority { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        // Will be set from current user context
        public string? CompanyCode { get; set; }
    }

    // DTO for updating a loan type
    public class LoanTypeUpdateDTO
    {
        [Required]
        [StringLength(100)]
        public string LoanType { get; set; } = null!;

        [StringLength(100)]
        public string? ValueChain { get; set; }

        [StringLength(100)]
        public string? LoanProduct { get; set; }

        [Required]
        [StringLength(20)]
        public string LoanAcc { get; set; } = null!;

        [StringLength(20)]
        public string? InterestAcc { get; set; }

        [StringLength(20)]
        public string? PenaltyAcc { get; set; }

        public int? RepayPeriod { get; set; }

        [StringLength(50)]
        public string? Interest { get; set; }

        public decimal? MaxAmount { get; set; }

        [StringLength(1)]
        public string? Guarantor { get; set; }

        public bool? UseIntRange { get; set; }

        public decimal? EarningRatio { get; set; }

        public bool Penalty { get; set; }

        public decimal? ProcessingFee { get; set; }

        public int GracePeriod { get; set; }

        [StringLength(20)]
        public string? RepayMethod { get; set; }

        public bool Bridging { get; set; }

        public bool SelfGuarantee { get; set; }

        public bool MobileLoan { get; set; }

        [Required]
        [StringLength(20)]
        public string Ppacc { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string ContraAccount { get; set; } = null!;

        public decimal? MinLoanAmount { get; set; }

        public decimal? MaxLoanAmount { get; set; }

        public int? MaxLoans { get; set; }

        public int Priority { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        // Will be set from current user context
        public string? CompanyCode { get; set; }
    }

    // DTO for loan type response
    public class LoanTypeResponseDTO
    {
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public string? ValueChain { get; set; }
        public string? LoanProduct { get; set; }
        public string LoanAcc { get; set; } = null!;
        public string? InterestAcc { get; set; }
        public string? PenaltyAcc { get; set; }
        public int? RepayPeriod { get; set; }
        public string? Interest { get; set; }
        public decimal? MaxAmount { get; set; }
        public string? Guarantor { get; set; }
        public bool? UseIntRange { get; set; }
        public decimal? EarningRatio { get; set; }
        public bool Penalty { get; set; }
        public decimal? ProcessingFee { get; set; }
        public int GracePeriod { get; set; }
        public string? RepayMethod { get; set; }
        public bool Bridging { get; set; }
        public bool SelfGuarantee { get; set; }
        public bool MobileLoan { get; set; }
        public string Ppacc { get; set; } = null!;
        public string ContraAccount { get; set; } = null!;
        public decimal? MinLoanAmount { get; set; }
        public decimal? MaxLoanAmount { get; set; }
        public int? MaxLoans { get; set; }
        public int Priority { get; set; }
        public string? CompanyCode { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int TotalLoans { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public int ActiveLoans { get; set; }
    }

    // Simple DTO for dropdown lists
    public class LoanTypeSimpleDTO
    {
        public string LoanCode { get; set; } = null!;
        public string LoanType { get; set; } = null!;
        public decimal? MaxAmount { get; set; }
        public int? RepayPeriod { get; set; }
        public string? Interest { get; set; }
        public bool Bridging { get; set; }
        public bool MobileLoan { get; set; }
        public int Priority { get; set; }
        public bool IsEligible { get; set; } = true; // Add this property
        public decimal EligibleAmount { get; set; } // Add this if needed
        public string? Reason { get; set; } // Add this if needed
    }
}