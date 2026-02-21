using System;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class BlockchainTransaction
    {
        [Key]
        [StringLength(255)]
        public string TransactionId { get; set; } = null!;

        [StringLength(255)]
        public string? BlockHash { get; set; }

        [Required]
        [StringLength(50)]
        public string TransactionType { get; set; } = null!; // "MEMBER_REGISTRATION", "DEPOSIT", "LOAN_APPROVED", etc.

        [StringLength(50)]
        public string? MemberNo { get; set; }

        [StringLength(50)]
        public string? CompanyCode { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; } = 0;

        public DateTime Timestamp { get; set; }

        [Required]
        [StringLength(255)]
        public string DataHash { get; set; } = null!;

        [StringLength(255)]
        public string? OffChainReferenceId { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "PENDING"; // "PENDING", "CONFIRMED", "FAILED"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual Block? Block { get; set; }
    }
}