namespace SACCOBlockChainSystem.Models.DTOs
{
    public class LoanCalculatorDTO
    {
        public decimal Principal { get; set; }
        public decimal InterestRate { get; set; }
        public int Period { get; set; }
        public string RepayMethod { get; set; }
        public DateTime StartDate { get; set; }
    }
}