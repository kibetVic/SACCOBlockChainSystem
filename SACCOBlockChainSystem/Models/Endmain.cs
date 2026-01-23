using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models;

public partial class Endmain
{
    public int Id { get; set; }

    public string LoanNo { get; set; } = null!;

    public string? CompanyCode { get; set; }

    public string? MinuteNo { get; set; }

    public DateTime? MeetingDate { get; set; }

    public decimal AmtApproved { get; set; }

    public string? Accepted { get; set; }

    public string? ChairSigned { get; set; }

    public string? SecSigned { get; set; }

    public string? MembSigned { get; set; }

    public string? Reasons { get; set; }

    public string? Remarks { get; set; }

    public string? AuditId { get; set; }

    public DateTime? AuditTime { get; set; }

    public string? TransactionNo { get; set; }
}
