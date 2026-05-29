// MODELS/Dividend.cs

using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class Dividend
    {
        [Key]
        public int Id { get; set; }

        public string MemberNo { get; set; }

        public decimal SavingsAmount { get; set; }

        public decimal DividendAmount { get; set; }

        // PRORATE or FLAT
        public string DividendType { get; set; }

        public decimal TotalDividendPool { get; set; }

        public DateTime ProcessDate { get; set; }

        public bool Paid { get; set; }

        public string? BlockchainTxId { get; set; }

        public string? AuditId { get; set; }

        public DateTime? AuditTime { get; set; }

        public string? CompanyCode { get; set; }
    }
}