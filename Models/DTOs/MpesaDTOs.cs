// Models/DTOs/MpesaDTOs.cs
using System.Text.Json.Serialization;

namespace SACCOBlockChainSystem.Models.DTOs
{
    public class MpesaC2BCallbackDTO
    {
        [JsonPropertyName("TransactionType")]
        public string TransactionType { get; set; } = string.Empty;

        [JsonPropertyName("TransID")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonPropertyName("TransTime")]
        public string TransactionTime { get; set; } = string.Empty;

        [JsonPropertyName("TransAmount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("BusinessShortCode")]
        public string BusinessShortCode { get; set; } = string.Empty;

        [JsonPropertyName("BillRefNumber")]
        public string AccountReference { get; set; } = string.Empty;

        [JsonPropertyName("MSISDN")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("MiddleName")]
        public string MiddleName { get; set; } = string.Empty;

        [JsonPropertyName("LastName")]
        public string LastName { get; set; } = string.Empty;
    }

    public class MpesaResponseDTO
    {
        public int ResultCode { get; set; }
        public string ResultDesc { get; set; } = string.Empty;
    }

    public class MpesaValidationResponseDTO
    {
        public int ResultCode { get; set; }
        public string ResultDesc { get; set; } = string.Empty;
    }

    public class MpesaConfirmationResponseDTO
    {
        public int ResultCode { get; set; }
        public string ResultDesc { get; set; } = string.Empty;
    }
    public class MpesaContributionCallbackDTO
    {
        [JsonPropertyName("TransactionType")]
        public string TransactionType { get; set; } = string.Empty;

        [JsonPropertyName("TransID")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonPropertyName("TransTime")]
        public string TransactionTime { get; set; } = string.Empty;

        [JsonPropertyName("TransAmount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("BusinessShortCode")]
        public string BusinessShortCode { get; set; } = string.Empty;

        [JsonPropertyName("BillRefNumber")]
        public string AccountReference { get; set; } = string.Empty;  // Format: CONTRIB:MBR001 or REG:MBR001 or SHARE:MBR001

        [JsonPropertyName("MSISDN")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("MiddleName")]
        public string MiddleName { get; set; } = string.Empty;

        [JsonPropertyName("LastName")]
        public string LastName { get; set; } = string.Empty;
    }
    public class MpesaContributionResponseDTO
    {
        public int ResultCode { get; set; }
        public string ResultDesc { get; set; } = string.Empty;
    }

    public class ContributionAccountReferenceDTO
    {
        public string Type { get; set; } = string.Empty;  // "CONTRIB", "REG", "SHARE", "DEPOSIT"
        public string MemberNo { get; set; } = string.Empty;
        public string? ShareTypeCode { get; set; }  // Optional - if specific share type is needed
    }

    public class C2BContributionConfigDTO
    {
        public string CompanyCode { get; set; } = string.Empty;
        public int ShortCode { get; set; }
        public string? DefaultShareTypeCode { get; set; }  // Default share type for generic contributions
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}