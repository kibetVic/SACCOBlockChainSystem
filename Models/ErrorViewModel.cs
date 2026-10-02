namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        // New properties to show actual error details
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public int? StatusCode { get; set; }
        public string? ExceptionType { get; set; }
        public bool ShowErrorDetails { get; set; }
    }
}