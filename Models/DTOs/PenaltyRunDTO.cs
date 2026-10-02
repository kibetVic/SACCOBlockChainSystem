// Models/DTOs/PenaltyRunDTO.cs
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class PenaltyRunResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime RunDate { get; set; }
        public int TotalLoansProcessed { get; set; }
        public int TotalPenaltiesApplied { get; set; }
        public decimal TotalPenaltyAmount { get; set; }
        public List<PenaltyDetailDTO> Details { get; set; } = new();
        public int TotalCompanies { get; set; }
        public int TotalLoansChecked { get; set; }
        public int TotalOverdueInstallments { get; set; }
        public string? CompanyCode { get; set; }
    }

    public class PenaltyDetailDTO
    {
        public string LoanNo { get; set; } = string.Empty;
        public string MemberNo { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public int InstallmentNo { get; set; }
        public DateTime DueDate { get; set; }
        public decimal InstallmentAmount { get; set; }
        public int DaysOverdue { get; set; }
        public int GracePeriodDays { get; set; }
        public decimal PenaltyRate { get; set; }
        public string PenaltyMode { get; set; } = string.Empty;
        public string PenaltyRateType { get; set; } = string.Empty;
        public int NumberOfPeriods { get; set; }
        public decimal PenaltyAmountCalculated { get; set; }
        public decimal OldPenaltyAmount { get; set; }
        public decimal NewPenaltyAmount { get; set; }
        public bool IsApplied { get; set; }
        public string? ErrorMessage { get; set; }
    }
}