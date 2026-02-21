using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models;

public partial class Company
{
    public int Id { get; set; }

    public string CompanyCode { get; set; } = null!;

    public string? CompanyName { get; set; }

    public string? Cigcode { get; set; }

    public string? CountyCode { get; set; }

    public string? County { get; set; }

    public string? SubCounty { get; set; }

    public string? Ward { get; set; }

    public string? Village { get; set; }

    public string? Unitcode { get; set; }

    public string? Email { get; set; }

    public string? Contactperson { get; set; }

    public string? Telephone { get; set; }

    public string? Address { get; set; }

    public string? AccountNo { get; set; }

    public int? NoYears { get; set; }

    public int? NoEmployees { get; set; }

    public string? Location { get; set; }

    public string? Type { get; set; }

    public string? AuditId { get; set; }

    public DateTime? AuditTime { get; set; }

    public decimal? Capital { get; set; }

    public bool Project { get; set; }
}
