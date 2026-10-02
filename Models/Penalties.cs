using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models;

[Table("Penalty")]
public partial class Penalties
{
    [Key]
    [StringLength(5)]
    public string LoanCode { get; set; } = null!;

    [StringLength(10)]
    public string Mode { get; set; } = "Fixed";

    [StringLength(10)]
    public string Rate { get; set; } = "Monthly";

    [Column(TypeName = "money")]
    public decimal Value { get; set; }

    public short ChargeItem { get; set; }

    public int Penalty { get; set; }

    [StringLength(50)]
    public string? CompanyCode { get; set; }

    [ForeignKey("LoanCode, CompanyCode")]
    public virtual Loantype? Loantype { get; set; }
}