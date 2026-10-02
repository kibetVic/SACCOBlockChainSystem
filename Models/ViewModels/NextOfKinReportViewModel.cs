using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class NextOfKinReportViewModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string ReportTitle { get; set; } = "NEXT OF KIN REPORT";
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = string.Empty;
        public ReportSummary Summary { get; set; } = new ReportSummary();
        public string CompanyCode { get; set; }
        public List<MemberNextOfKinReportDto> MembersWithNextOfKin { get; set; } = new List<MemberNextOfKinReportDto>();
    }

    public class ReportSummary
    {
        public int TotalMembers { get; set; }
        public int TotalNextOfKeens { get; set; }
        public int MembersWithCompleteBenefit { get; set; }
        public int MembersWithInvalidBenefit { get; set; }
        public int MembersWithNoNextOfKin { get; set; }
    }

    public class MemberNextOfKinReportDto
    {
        public string MemberNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalNextOfKeens { get; set; }
        public decimal TotalBenefitPercentage { get; set; }
        public bool HasValidBenefit => TotalBenefitPercentage <= 100;
        public List<NextOfKinDetailDto> NextOfKeens { get; set; } = new List<NextOfKinDetailDto>();
    }

    public class NextOfKinDetailDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public decimal? BenefitPercentage { get; set; }
        public bool IsPrimary { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhysicalAddress { get; set; } = string.Empty;
    }

    public class MemberWithNextOfKinDTO
    {
        // Member Details
        public string MemberNo { get; set; }
        public string FullName { get; set; }
        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public string IdNumber { get; set; }
        public string PhoneNo { get; set; }
        public string Email { get; set; }
        public string PhysicalAddress { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Age { get; set; }
        public string MembershipType { get; set; }
        public string RegistrationType { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string Status { get; set; }
        public decimal? ShareCapital { get; set; }
        public string CIGGroup { get; set; }
        public string Department { get; set; }
        public string Station { get; set; }

        // Next of Kin List for this member
        public List<NextOfKinReportDTO> NextOfKeens { get; set; }

        // Member Summary
        public int TotalNextOfKeens => NextOfKeens?.Count ?? 0;
        public decimal TotalBenefitPercentage => NextOfKeens?.Sum(n => n.BenefitPercentage ?? 0) ?? 0;
        public bool HasValidBenefit => TotalBenefitPercentage <= 100;
    }

    public class NextOfKinReportDTO
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Relationship { get; set; }
        public string PhoneNo { get; set; }
        public string Email { get; set; }
        public string PhysicalAddress { get; set; }
        public string IdNumber { get; set; }
        public string PassportNumber { get; set; }
        public string Employer { get; set; }
        public string Occupation { get; set; }
        public decimal? BenefitPercentage { get; set; }
        public int? PriorityOrder { get; set; }
        public bool IsPrimary { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }

    public class ReportSummaryDTO
    {
        public int TotalMembers { get; set; }
        public int TotalNextOfKeens { get; set; }
        public int MembersWithCompleteBenefit { get; set; }
        public int MembersWithInvalidBenefit { get; set; }
        public int MembersWithNoNextOfKin { get; set; }
        public decimal AverageBenefitPercentage { get; set; }

        // Read-only computed properties
        public bool IsBenefitValid => TotalNextOfKeens > 0;
        public decimal OverallCompletionRate => TotalMembers > 0 ? (decimal)MembersWithCompleteBenefit / TotalMembers * 100 : 0;
    }
}