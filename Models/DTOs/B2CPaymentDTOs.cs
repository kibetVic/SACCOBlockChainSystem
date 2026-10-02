// Models/DTOs/B2CPaymentDTOs.cs
using System;
using System.Collections.Generic;

namespace SACCOBlockChainSystem.Models.DTOs
{
    /// <summary>
    /// Request to send a B2C payment
    /// </summary>
    public class B2CPaymentRequest
    {
        public string CompanyCode { get; set; }
        public decimal Amount { get; set; }
        public string PhoneNumber { get; set; }
        public string Reference { get; set; }
        public string Remarks { get; set; }
        public string SourceModule { get; set; }
        public string SourceReference { get; set; }
        public string RecipientName { get; set; }
        public string CommandID { get; set; } = "BusinessPayment";
        public string ApiKey { get; set; }
        public string InitiatorName { get; set; }
        public string OriginatorConversationID { get; set; }
        public string Occasion { get; set; }
        public string CreatedBy { get; set; }
        public string CustomerName { get; set; }
        public string TransactionType { get; set; }
        public string AccountReference { get; set; }
    }
    public class B2CPaymentResponse
    {
        public bool Success { get; set; }
        public string Provider { get; set; }
        public string TransactionReference { get; set; }
        public string OriginatorConversationID { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseDescription { get; set; }
        public string RecipientPhone { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "PENDING";
        public DateTime InitiatedAt { get; set; } = DateTime.Now;
        public object RawResponse { get; set; }
        public string ErrorMessage { get; set; }
        public string CheckoutRequestID { get; set; }
        public string InternalTransactionId { get; set; }
    }
    public class B2CTransactionRecord
    {
        public int Id { get; set; }
        public string CompanyCode { get; set; }
        public string Provider { get; set; }
        public string TransactionReference { get; set; }
        public string OriginatorConversationID { get; set; }
        public decimal Amount { get; set; }
        public string RecipientPhone { get; set; }
        public string RecipientName { get; set; }
        public string SourceModule { get; set; }
        public string SourceReference { get; set; }
        public string Status { get; set; } // PENDING, SUCCESS, FAILED
        public string ResponseCode { get; set; }
        public string ResponseDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string BlockchainTxId { get; set; }
    }
    public class B2CQueryRequest
    {
        public string CompanyCode { get; set; }
        public string TransactionReference { get; set; }
        public string ApiKey { get; set; }
    }
}