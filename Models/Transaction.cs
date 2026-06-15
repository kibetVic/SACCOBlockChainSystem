using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models;

public partial class Transaction
{
    public string? TransactionNo { get; set; }

    public decimal Amount { get; set; }

    public DateTime TransDate { get; set; } = DateTime.Now;

    public string AuditId { get; set; } = null!;

    public DateTime AuditTime { get; set; } = DateTime.Now;

    public string TransDescription { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? CompanyCode { get; set; }

    public int Id { get; set; }

    public string? Channel { get; set; }

    public DateTime? AuditDateTime { get; set; } = DateTime.Now;

    [NotMapped]
    public String? TransactionType { get; set; }
    [NotMapped]
    public String? ReceiptNo { get; set; }
    [NotMapped]
    public String? BlockchainTxId { get;set; }
}
