using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class MemberDTO
    {
        public string MemberNo { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string OtherNames { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Idno { get; set; } = null!;
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public string? PresentAddress { get; set; }
        public string? HomeAddress { get; set; }
        public string? OfficePhone { get; set; }
        public string? HomePhone { get; set; }
        public string CompanyCode { get; set; } = null!;
        public decimal CurrentBalance { get; set; }
        public decimal ShareBalance { get; set; }
        public decimal LoanBalance { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? DateJoined { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsDormant { get; set; }
        public string? ProfilePicture { get; set; }
        public string? BlockchainTxId { get; set; }
    }
    //public class MemberRegistrationDTO
    //{
    //    public string? Surname { get; set; }
    //    public string? OtherNames { get; set; }
    //    public string? IdNo { get; set; }
    //    public string? PhoneNo { get; set; }
    //    public string? Email { get; set; }
    //    public DateTime? DateOfBirth { get; set; }
    //    public string? Gender { get; set; }
    //    public string? CompanyCode { get; set; }
    //    public decimal InitialShares { get; set; } 
    //    public string? CreatedBy { get; set; }
    //}

    public class MemberResponseDTO
    {
        public string MemberNo { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string BlockchainTxId { get; set; }
        public decimal ShareBalance { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyCode { get; set; }
    }


    public class MemberRegistrationDTO
    {
        public string Surname { get; set; } = null!;
        public string OtherNames { get; set; } = null!;
        public string IdNo { get; set; } = null!;
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? PresentAddress { get; set; }
        public string? CompanyCode { get; set; }
        public decimal InitialShares { get; set; } = 0;
        public string? CreatedBy { get; set; }
    }

    public class MemberUpdateDTO
    {
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? PresentAddress { get; set; }
        public string? HomeAddress { get; set; }
        public string? Employer { get; set; }
        public string? Department { get; set; }
        public string? Station { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class MemberSummaryDTO
    {
        public string MemberNo { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string IdNumber { get; set; } = null!;
        public string? Phone { get; set; }
        public decimal ShareBalance { get; set; }
        public decimal LoanBalance { get; set; }
        public decimal TotalBalance { get; set; }
        public DateTime? MemberSince { get; set; }
        public string Status { get; set; } = null!;
        public int TotalTransactions { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }

    public class MemberTransactionSummary
    {
        public string TransactionType { get; set; } = null!;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }
}