using System;

namespace SACCOBlockChainSystem.Models
{
    public class LoanScheduleBlock
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }

        public string MemberNo { get; set; }
        public string MemberName { get; set; }
        public string LoanNo { get; set; }

        public int Period { get; set; }
        public DateTime PaymentDate { get; set; }

        public decimal OpeningBalance { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Payment { get; set; }
        public decimal ClosingBalance { get; set; }

        public string PreviousHash { get; set; }
        public string Hash { get; set; }
    }
}