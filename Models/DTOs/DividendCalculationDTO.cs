namespace SACCOBlockChainSystem.Models.DTOs
{
    public class DividendCalculationDTO
    {
        public decimal TotalDividend { get; set; }

        public string DividendType { get; set; } = string.Empty; 
    }
}