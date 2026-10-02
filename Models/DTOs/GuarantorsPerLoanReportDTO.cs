// Models/DTOs/GuarantorReportDTOs.cs
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.DTOs
{
    // DTO for Guarantors Per Loan Report
    public class GuarantorsPerLoanReportDTO
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public decimal LoanAmount { get; set; }
        public string LoanStatus { get; set; } = null!;
        public DateTime? ApplicationDate { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public decimal TotalGuaranteeAmount { get; set; }
        public bool IsFullyGuaranteed { get; set; }
        public List<GuarantorDetailDTO> Guarantors { get; set; } = new();
    }

    // DTO for Individual Guarantor Detail
    public class GuarantorDetailDTO
    {
        public int Id { get; set; }
        public string GuarantorMemberNo { get; set; } = null!;
        public string GuarantorName { get; set; } = null!;
        public string? IdNo { get; set; }
        public string? PhoneNo { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public decimal? Balance { get; set; }
        public string? Collateral { get; set; }
        public string? Description { get; set; }
        public bool Transfered { get; set; }
        public DateTime? Transdate { get; set; }
        public DateTime? AuditTime { get; set; }
        public string? AuditId { get; set; }
    }

    // DTO for All Guarantors Summary Report
    public class AllGuarantorsReportDTO
    {
        public string LoanNo { get; set; } = null!;
        public string MemberNo { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string GuarantorMemberNo { get; set; } = null!;
        public string GuarantorName { get; set; } = null!;
        public string? GuarantorIdNo { get; set; }
        public string? GuarantorPhone { get; set; }
        public decimal GuaranteeAmount { get; set; }
        public decimal? OutstandingBalance { get; set; }
        public string? Collateral { get; set; }
        public string? Description { get; set; }
        public bool Transfered { get; set; }
        public DateTime? AssignedDate { get; set; }
        public decimal LoanAmount { get; set; }
        public string LoanStatus { get; set; } = null!;
    }

    // ViewModel for Guarantors Per Loan Report
    public class GuarantorsPerLoanIndexViewModel
    {
        public List<GuarantorsPerLoanReportDTO> Loans { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool HasData { get; set; }
        public string CompanyName { get; set; } = null!;
        public string PrintedBy { get; set; } = null!;
        public DateTime GeneratedOn { get; set; }

        // Summary Statistics
        public int TotalLoans { get; set; }
        public int TotalGuarantors { get; set; }
        public decimal TotalGuaranteeAmount { get; set; }
        public int FullyGuaranteedLoans { get; set; }
        public int PartiallyGuaranteedLoans { get; set; }
        public int LoansWithoutGuarantors { get; internal set; }
        public decimal AverageGuarantorsPerLoan { get; internal set; }
        public decimal AverageGuaranteeAmount { get; internal set; }
        public string UserEmail { get; internal set; }
    }

    // ViewModel for All Guarantors Report
    public class AllGuarantorsIndexViewModel
    {
        public List<AllGuarantorsReportDTO> Guarantors { get; set; } = new();
        public DateTime ReportDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool HasData { get; set; }
        public string CompanyName { get; set; } = null!;
        public string PrintedBy { get; set; } = null!;
        public DateTime GeneratedOn { get; set; }

        // Summary Statistics
        public int TotalRecords { get; set; }
        public int UniqueLoans { get; set; }
        public int UniqueGuarantors { get; set; }
        public decimal TotalGuaranteeAmount { get; set; }
        public int ActiveGuarantors { get; set; }
        public int ReleasedGuarantors { get; set; }
    }
}