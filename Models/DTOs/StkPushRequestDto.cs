using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace SACCOBlockChainSystem.Models.DTOs
{
    public class StkPushRequest
    {
        public string CompanyCode { get; set; }
        public decimal Amount { get; set; }
        public string PhoneNumber { get; set; }
        public string Reference { get; set; }
        public string Remarks { get; set; }
        public string ApiKey { get; set; }
        public string CustomerName { get; set; }
        public string TransactionType { get; set; }
        public string AccountReference { get; set; }
        public string Provider { get; set; }  // "MPESA", "COOP", etc.
    }

    public class StkPushResponse
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseDescription { get; set; }
        public string TransactionReference { get; set; }  // CheckoutRequestID or MessageReference
        public string MerchantRequestID { get; set; }
        public string ConversationID { get; set; }
        public string ErrorMessage { get; set; }
        public dynamic RawResponse { get; set; }
    }

    public class MpesaApiResponse
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseDescription { get; set; }
        public string TransactionReference { get; set; }
        public string MerchantRequestID { get; set; }
        public string ConversationID { get; set; }
        public string ErrorMessage { get; set; }
        public dynamic RawResponse { get; set; }
    }

    public class StkPushResult
    {
        public bool Success { get; set; }
        public string TransactionReference { get; set; }
        public string Provider { get; set; }
        public string Message { get; set; }
        public bool ShouldFallbackToCash { get; set; }
        public string Error { get; set; }
    }
}
