using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Models
{
    public class Loanschd
    {
        [Key]
        public int ID { get; set; }

        public string? MemberNo { get; set; }
        public int? Period { get; set; }
        public decimal? Principal { get; set; }
        public decimal? Interest { get; set; }
        public decimal? Balance { get; set; }
        public string? Comments { get; set; }
        public string? FmtPer { get; set; }
        public decimal? contrib { get; set; }
        public decimal? Sharebalance { get; set; }
    }
}