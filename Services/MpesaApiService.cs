// Services/MpesaApiService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface IMpesaApiService
    {
        //C2B module
        Task<MpesaApiResponse> SendStkPushAsync(StkPushRequest request);
        Task<MpesaApiResponse> QueryTransactionAsync(string conversationId);
       // Task<MpesaApiResponse> SendB2CPaymentAsync(B2CPaymentRequest request);

        //B2C module
        Task<B2CPaymentResponse> SendB2CPaymentAsync(B2CPaymentRequest request);
        Task<B2CPaymentResponse> QueryB2CStatusAsync(B2CQueryRequest request);
        Task<B2CPaymentResponse> ReverseB2CPaymentAsync(string companyCode, string transactionReference, string reason, string reversedBy);
        Task<ApiTable> GetB2CConfigurationAsync(string companyCode);
    }    

    public class MpesaApiService : IMpesaApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MpesaApiService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly AppDbContext _appDbContext;
        private readonly IBlockchainService _blockchainService;

        public MpesaApiService(
            IHttpClientFactory httpClientFactory,
            ILogger<MpesaApiService> logger,
            IConfiguration configuration,
            AppDbContext appDbContext,
            IBlockchainService blockchainService,
            ApplicationDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _appDbContext = appDbContext;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _blockchainService = blockchainService;
        }

        public async Task<MpesaApiResponse> SendStkPushAsync(StkPushRequest request)
        {
            try
            {
                _logger.LogInformation($"Sending STK Push for Company: {request.CompanyCode}, Amount: {request.Amount:C}");

                // Step 1: Get the API base URL from configuration
                var apiBaseUrl = _configuration["MpesaApi:BaseUrl"] ?? "http://localhost:5000";
                var endpoint = $"{apiBaseUrl}/api/Apis/Simulate";

                // Step 2: Build the request payload
                var payload = new
                {
                    action = "stk-push",
                    amount = request.Amount,
                    phone = request.PhoneNumber,
                    remarks = request.Reference,
                    companycode = request.CompanyCode,
                    ApiKey = request.ApiKey ?? _configuration["MpesaApi:DefaultApiKey"],
                    reference = request.Remarks,
                    simulateUrl = $"{apiBaseUrl}/api/Apis/Simulate"
                };

                // Step 3: Serialize and send request
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogDebug($"Calling M-PESA API: {endpoint}");
                var response = await httpClient.PostAsync(endpoint, content);

                // Step 4: Read response
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"M-PESA API Response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"M-PESA API returned error: {response.StatusCode}, Response: {responseContent}");
                    return new MpesaApiResponse
                    {
                        Success = false,
                        ResponseCode = response.StatusCode.ToString(),
                        ResponseDescription = $"API Error: {response.StatusCode}",
                        ErrorMessage = responseContent
                    };
                }

                // Step 5: Parse response
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var result = new MpesaApiResponse
                {
                    Success = true,
                    ResponseCode = root.TryGetProperty("responseCode", out var rc) ? rc.GetString() : "0",
                    ResponseDescription = root.TryGetProperty("responseDescription", out var rd) ? rd.GetString() : "Success",
                    RawResponse = JsonSerializer.Deserialize<dynamic>(responseContent)
                };

                // Step 6: Extract transaction reference (different for each provider)
                // M-PESA uses CheckoutRequestID, Co-op uses MessageReference
                if (root.TryGetProperty("checkoutRequestID", out var checkoutId))
                {
                    result.TransactionReference = checkoutId.GetString();
                }
                else if (root.TryGetProperty("conversationID", out var convId))
                {
                    result.TransactionReference = convId.GetString();
                }
                else if (root.TryGetProperty("messageReference", out var msgRef))
                {
                    result.TransactionReference = msgRef.GetString();
                }

                if (root.TryGetProperty("merchantRequestID", out var merchantId))
                {
                    result.MerchantRequestID = merchantId.GetString();
                }

                if (root.TryGetProperty("conversationID", out var convId2))
                {
                    result.ConversationID = convId2.GetString();
                }

                _logger.LogInformation($"STK Push sent successfully. Reference: {result.TransactionReference}");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP error calling M-PESA API: {ex.Message}");
                return new MpesaApiResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Network error",
                    ErrorMessage = ex.Message
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"JSON parsing error: {ex.Message}");
                return new MpesaApiResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Response parsing error",
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error calling M-PESA API: {ex.Message}");
                return new MpesaApiResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Unexpected error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<MpesaApiResponse> QueryTransactionAsync(string conversationId)
        {
            try
            {
                var apiBaseUrl = _configuration["MpesaApi:BaseUrl"] ?? "http://localhost:5000";
                var endpoint = $"{apiBaseUrl}/api/Apis/Simulate";

                var payload = new
                {
                    action = "stk-query",
                    conversationID = conversationId,
                    ApiKey = _configuration["MpesaApi:DefaultApiKey"]
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(endpoint, content);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new MpesaApiResponse
                    {
                        Success = false,
                        ResponseCode = response.StatusCode.ToString(),
                        ResponseDescription = "Query failed",
                        ErrorMessage = responseContent
                    };
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                return new MpesaApiResponse
                {
                    Success = true,
                    ResponseCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetString() : "0",
                    ResponseDescription = root.TryGetProperty("resultDesc", out var rd) ? rd.GetString() : "Success",
                    RawResponse = JsonSerializer.Deserialize<dynamic>(responseContent)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying transaction {conversationId}");
                return new MpesaApiResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Query error",
                    ErrorMessage = ex.Message
                };
            }
        }
                 
       
        public async Task<ApiTable> GetB2CConfigurationAsync(string companyCode)
        {
            var config = await _appDbContext.ApiTable
                .FirstOrDefaultAsync(a => a.CompanyCode == companyCode
                                          && a.Status == "Active"
                                          && a.Channel != null);

            if (config == null)
            {
                _logger.LogWarning($"No API configuration found for company: {companyCode}");
            }

            return config;
        }

        public async Task<B2CPaymentResponse> SendB2CPaymentAsync(B2CPaymentRequest request)
        {
            try
            {
                _logger.LogInformation($"Sending B2C payment: Company={request.CompanyCode}, Amount={request.Amount:C}, Phone={request.PhoneNumber}");

                // Step 1: Get company configuration
                var config = await GetB2CConfigurationAsync(request.CompanyCode);

                if (config == null)
                {
                    return new B2CPaymentResponse
                    {
                        Success = false,
                        ResponseCode = "404",
                        ResponseDescription = "No B2C configuration found for this company",
                        ErrorMessage = "Please configure API settings in ApiTable"
                    };
                }

                // Step 2: Generate unique transaction IDs
                var originatorConversationID = GenerateOriginatorConversationID();
                var internalTransactionId = $"B2C-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";

                // Step 3: Format phone number
                var formattedPhone = FormatPhoneNumber(request.PhoneNumber);
                if (string.IsNullOrEmpty(formattedPhone))
                {
                    return new B2CPaymentResponse
                    {
                        Success = false,
                        ResponseCode = "400",
                        ResponseDescription = "Invalid phone number format",
                        ErrorMessage = "Phone number must be in valid format (254XXXXXXXXX)"
                    };
                }

                // Step 4: Build request to M-PESA API System
                var apiBaseUrl = _configuration["MpesaApi:BaseUrl"] ?? "http://localhost:5000";
                var endpoint = $"{apiBaseUrl}/api/Apis/Simulate";

                var payload = new
                {
                    action = "b2c",
                    amount = request.Amount,
                    phone = formattedPhone,
                    reference = request.Reference ?? request.SourceReference,
                    companycode = request.CompanyCode,
                    ApiKey = request.ApiKey ?? _configuration["MpesaApi:DefaultApiKey"] ?? config.ApiPassword,
                    remarks = request.Remarks ?? $"{request.SourceModule} - {request.SourceReference}",
                    conversationID = originatorConversationID,
                    @ref = request.Reference
                };

                _logger.LogInformation($"Calling M-PESA API: {endpoint}");
                _logger.LogDebug($"Payload: {JsonSerializer.Serialize(payload)}");

                // Step 5: Send HTTP request
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"M-PESA API Response: {responseContent}");

                // Step 6: Parse response
                B2CPaymentResponse result;

                if (response.IsSuccessStatusCode)
                {
                    result = await ParseB2CResponse(responseContent, request, config.Channel, internalTransactionId);
                }
                else
                {
                    result = new B2CPaymentResponse
                    {
                        Success = false,
                        Provider = config.Channel,
                        ResponseCode = response.StatusCode.ToString(),
                        ResponseDescription = $"HTTP Error: {response.StatusCode}",
                        ErrorMessage = responseContent,
                        Amount = request.Amount,
                        RecipientPhone = formattedPhone,
                        InternalTransactionId = internalTransactionId,
                        OriginatorConversationID = originatorConversationID
                    };
                }

                // Step 7: Save transaction record to database
                await SaveB2CTransaction(request, result, config, originatorConversationID, internalTransactionId);

                // Step 8: Record blockchain transaction
                if (result.Success)
                {
                    await RecordBlockchainTransaction(request, result);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending B2C payment: {ex.Message}");
                return new B2CPaymentResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Internal error",
                    ErrorMessage = ex.Message,
                    Amount = request.Amount,
                    RecipientPhone = request.PhoneNumber
                };
            }
        }

       private async Task<B2CPaymentResponse> ParseB2CResponse(string responseContent, B2CPaymentRequest request, string provider, string internalTransactionId)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Check for success indicators
                bool isSuccess = false;
                string responseCode = "0";
                string responseDescription = "Success";
                string transactionReference = null;
                string originatorConversationID = null;
                string checkoutRequestID = null;

                // Try to get responseCode
                if (root.TryGetProperty("responseCode", out var rc))
                {
                    responseCode = rc.GetString() ?? "0";
                    isSuccess = responseCode == "0";
                }
                else if (root.TryGetProperty("ResponseCode", out var rc2))
                {
                    responseCode = rc2.GetString() ?? "0";
                    isSuccess = responseCode == "0";
                }
                else if (root.TryGetProperty("Success", out var success))
                {
                    isSuccess = success.GetBoolean();
                    responseCode = isSuccess ? "0" : "1";
                }
                else
                {
                    // If no explicit response code, check for conversation ID
                    if (root.TryGetProperty("conversationID", out _) ||
                        root.TryGetProperty("ConversationID", out _) ||
                        root.TryGetProperty("messageReference", out _) ||
                        root.TryGetProperty("MessageReference", out _))
                    {
                        isSuccess = true;
                        responseCode = "0";
                    }
                }

                // Get response description
                if (root.TryGetProperty("responseDescription", out var rd))
                    responseDescription = rd.GetString() ?? "Success";
                else if (root.TryGetProperty("ResponseDescription", out var rd2))
                    responseDescription = rd2.GetString() ?? "Success";
                else if (root.TryGetProperty("Message", out var msg))
                    responseDescription = msg.GetString() ?? "Success";

                // Get transaction reference
                if (root.TryGetProperty("conversationID", out var conv))
                    transactionReference = conv.GetString();
                else if (root.TryGetProperty("ConversationID", out var conv2))
                    transactionReference = conv2.GetString();
                else if (root.TryGetProperty("messageReference", out var msgRef))
                    transactionReference = msgRef.GetString();
                else if (root.TryGetProperty("MessageReference", out var msgRef2))
                    transactionReference = msgRef2.GetString();
                else if (root.TryGetProperty("checkoutRequestID", out var chk))
                    transactionReference = chk.GetString();

                // Get originator conversation ID
                if (root.TryGetProperty("originatorConversationID", out var orig))
                    originatorConversationID = orig.GetString();
                else if (root.TryGetProperty("OriginatorConversationID", out var orig2))
                    originatorConversationID = orig2.GetString();
                else if (string.IsNullOrEmpty(originatorConversationID))
                    originatorConversationID = transactionReference;

                // Get checkout request ID (for STK queries)
                if (root.TryGetProperty("checkoutRequestID", out var chkId))
                    checkoutRequestID = chkId.GetString();

                // Get status
                string status = isSuccess ? "PENDING" : "FAILED";
                if (root.TryGetProperty("status", out var st))
                    status = st.GetString() ?? status;

                return new B2CPaymentResponse
                {
                    Success = isSuccess,
                    Provider = provider ?? "MPESA",
                    TransactionReference = transactionReference ?? originatorConversationID,
                    OriginatorConversationID = originatorConversationID,
                    CheckoutRequestID = checkoutRequestID,
                    ResponseCode = responseCode,
                    ResponseDescription = responseDescription,
                    RecipientPhone = request.PhoneNumber,
                    Amount = request.Amount,
                    Status = status,
                    InitiatedAt = DateTime.Now,
                    InternalTransactionId = internalTransactionId,
                    RawResponse = JsonSerializer.Deserialize<dynamic>(responseContent)
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error parsing B2C response: {ex.Message}");
                _logger.LogDebug($"Raw response: {responseContent}");

                // Try to extract basic info from raw response
                return new B2CPaymentResponse
                {
                    Success = false,
                    Provider = provider ?? "MPESA",
                    ResponseCode = "500",
                    ResponseDescription = "Response parsing error",
                    ErrorMessage = ex.Message,
                    RecipientPhone = request.PhoneNumber,
                    Amount = request.Amount,
                    InternalTransactionId = internalTransactionId,
                    RawResponse = responseContent
                };
            }
        }

        private async Task SaveB2CTransaction(B2CPaymentRequest request, B2CPaymentResponse response, ApiTable config, string originatorConversationID, string internalTransactionId)
        {
            try
            {
                // Check if this company uses the CoopTransaction table or TransactionDetail
                var provider = config.Channel?.ToUpper() ?? "MPESA";

                // Determine which table to use based on provider
                if (provider == "COOP" || provider == "COOPBANK")
                {
                    // Use CoopTransaction table
                    var coopTransaction = new CoopTransaction
                    {
                        CompanyCode = request.CompanyCode,
                        TransactionId = internalTransactionId,
                        TransactionCode = response.TransactionReference ?? originatorConversationID,
                        ResultCode = int.TryParse(response.ResponseCode, out int rc) ? rc : 1,
                        ResultMessage = response.ResponseDescription,
                        Amount = response.Amount,
                        Status = response.Status,
                        ConversationId = response.TransactionReference ?? originatorConversationID,
                        ShortCode = config.ShortCode ?? 0,
                        UpdatedAt = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        MemberNo = request.SourceReference,
                        Phone = response.RecipientPhone,
                        OriginatorConversationId = originatorConversationID,
                        MerchantRequestId = response.CheckoutRequestID,
                        CheckoutRequestId = response.CheckoutRequestID,
                        AuditDateTime = DateTime.Now,
                        BlockchainTxId = null
                    };

                    _appDbContext.CoopTransactions.Add(coopTransaction);
                    await _appDbContext.SaveChangesAsync();

                    // Also save to ApiTransactions for consistency
                    var apiTransaction = new ApiTransaction
                    {
                        CompanyCode = request.CompanyCode,
                        ApiUser = request.CreatedBy ?? "SYSTEM",
                        ShortCode = config.ShortCode,
                        TransactionCode = $"{request.SourceModule}-{request.SourceReference}",
                        CheckoutId = response.CheckoutRequestID,
                        ConversationId = response.TransactionReference ?? originatorConversationID,
                        Amount = response.Amount,
                        Recipient = response.RecipientPhone,
                        StatusCode = int.TryParse(response.ResponseCode, out int code) ? code : 1,
                        ResultDescription = response.ResponseDescription,
                        Request = "b2c",
                        LoanNo = request.SourceReference,
                        created_at = DateTime.Now,
                        updated_at = DateTime.Now,
                        AuditDateTime = DateTime.Now
                    };

                    _appDbContext.ApiTransactions.Add(apiTransaction);
                    await _appDbContext.SaveChangesAsync();
                }
                else
                {
                    // Use TransactionDetail table (for MPESA and others)
                    var transactionDetail = new TransactionDetail
                    {
                        CompanyCode = request.CompanyCode,
                        TransactionId = internalTransactionId,
                        TransactionCode = response.TransactionReference ?? originatorConversationID,
                        ResultCode = int.TryParse(response.ResponseCode, out int rc) ? rc : 1,
                        ResultMessage = response.ResponseDescription,
                        Amount = response.Amount,
                        Status = response.Status,
                        ConversationId = response.TransactionReference ?? originatorConversationID,
                        ShortCode = config.ShortCode ?? 0,
                        UpdatedAt = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        MemberNo = request.SourceReference,
                        Phone = response.RecipientPhone,
                        OriginatorConversationId = originatorConversationID,
                        MerchantRequestId = response.CheckoutRequestID,
                        CheckoutRequestId = response.CheckoutRequestID,
                        AuditDateTime = DateTime.Now
                    };

                    _appDbContext.Transaction_detail.Add(transactionDetail);
                    await _appDbContext.SaveChangesAsync();

                    // Also save to ApiTransactions
                    var apiTransaction = new ApiTransaction
                    {
                        CompanyCode = request.CompanyCode,
                        ApiUser = request.CreatedBy ?? "SYSTEM",
                        ShortCode = config.ShortCode,
                        TransactionCode = $"{request.SourceModule}-{request.SourceReference}",
                        CheckoutId = response.CheckoutRequestID,
                        ConversationId = response.TransactionReference ?? originatorConversationID,
                        Amount = response.Amount,
                        Recipient = response.RecipientPhone,
                        StatusCode = int.TryParse(response.ResponseCode, out int code) ? code : 1,
                        ResultDescription = response.ResponseDescription,
                        Request = "b2c",
                        LoanNo = request.SourceReference,
                        created_at = DateTime.Now,
                        updated_at = DateTime.Now,
                        AuditDateTime = DateTime.Now
                    };

                    _appDbContext.ApiTransactions.Add(apiTransaction);
                    await _appDbContext.SaveChangesAsync();
                }

                _logger.LogInformation($"B2C transaction saved. InternalId: {internalTransactionId}, Reference: {response.TransactionReference}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving B2C transaction: {ex.Message}");
                // Don't throw - we want the payment to continue even if save fails
            }
        }

        private async Task RecordBlockchainTransaction(B2CPaymentRequest request, B2CPaymentResponse response)
        {
            try
            {
                var blockchainData = new
                {
                    TransactionType = "B2C_PAYMENT",
                    Provider = response.Provider,
                    Amount = request.Amount,
                    RecipientPhone = response.RecipientPhone,
                    RecipientName = request.RecipientName,
                    Reference = request.Reference,
                    SourceModule = request.SourceModule,
                    SourceReference = request.SourceReference,
                    TransactionReference = response.TransactionReference,
                    OriginatorConversationID = response.OriginatorConversationID,
                    ResponseCode = response.ResponseCode,
                    ResponseDescription = response.ResponseDescription,
                    InitiatedBy = request.CreatedBy,
                    InitiatedAt = DateTime.Now
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "B2C_PAYMENT",
                    request.SourceReference,
                    request.CompanyCode,
                    request.Amount,
                    response.TransactionReference,
                    blockchainData
                );

                // Update the transaction record with BlockchainTxId
                if (blockchainTx != null)
                {
                    // Find and update the transaction record
                    if (response.Provider?.ToUpper() == "COOP" || response.Provider?.ToUpper() == "COOPBANK")
                    {
                        var coopTx = await _appDbContext.CoopTransactions
                            .FirstOrDefaultAsync(t => t.ConversationId == response.TransactionReference
                                                       && t.CompanyCode == request.CompanyCode);
                        if (coopTx != null)
                        {
                            coopTx.BlockchainTxId = blockchainTx.TransactionId;
                            await _appDbContext.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        var txDetail = await _appDbContext.Transaction_detail
                            .FirstOrDefaultAsync(t => t.ConversationId == response.TransactionReference
                                                       && t.CompanyCode == request.CompanyCode);
                        if (txDetail != null)
                        {
                            txDetail.BlockchainTxId = blockchainTx.TransactionId;
                            await _appDbContext.SaveChangesAsync();
                        }
                    }

                    // Update ApiTransaction
                    var apiTx = await _appDbContext.ApiTransactions
                        .FirstOrDefaultAsync(t => t.ConversationId == response.TransactionReference
                                                   && t.CompanyCode == request.CompanyCode);
                    if (apiTx != null)
                    {
                        //apiTx.BlockchainTxId = blockchainTx.TransactionId;
                        await _appDbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording blockchain transaction for B2C payment: {ex.Message}");
                // Don't throw - blockchain logging is non-critical
            }
        }

        public async Task<B2CPaymentResponse> QueryB2CStatusAsync(B2CQueryRequest request)
        {
            try
            {
                _logger.LogInformation($"Querying B2C status: Company={request.CompanyCode}, Reference={request.TransactionReference}");

                var apiBaseUrl = _configuration["MpesaApi:BaseUrl"] ?? "http://localhost:5000";
                var endpoint = $"{apiBaseUrl}/api/Apis/Simulate";

                var payload = new
                {
                    action = "query",
                    conversationID = request.TransactionReference,
                    companycode = request.CompanyCode,
                    ApiKey = request.ApiKey ?? _configuration["MpesaApi:DefaultApiKey"]
                };

                var httpClient = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new B2CPaymentResponse
                    {
                        Success = false,
                        ResponseCode = response.StatusCode.ToString(),
                        ResponseDescription = "Query failed",
                        ErrorMessage = responseContent
                    };
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Parse result
                bool isSuccess = false;
                string resultCode = "0";
                string resultDesc = "Success";
                string status = "PENDING";

                if (root.TryGetProperty("resultCode", out var rc))
                {
                    resultCode = rc.GetString() ?? "0";
                    isSuccess = resultCode == "0";
                    status = isSuccess ? "SUCCESS" : "FAILED";
                }
                else if (root.TryGetProperty("ResponseCode", out var rc2))
                {
                    resultCode = rc2.GetString() ?? "0";
                    isSuccess = resultCode == "0";
                    status = isSuccess ? "SUCCESS" : "FAILED";
                }

                if (root.TryGetProperty("resultDesc", out var rd))
                    resultDesc = rd.GetString() ?? "Success";
                else if (root.TryGetProperty("ResponseDescription", out var rd2))
                    resultDesc = rd2.GetString() ?? "Success";

                // Check for transaction status in response
                if (root.TryGetProperty("status", out var st))
                    status = st.GetString() ?? status;

                return new B2CPaymentResponse
                {
                    Success = isSuccess,
                    ResponseCode = resultCode,
                    ResponseDescription = resultDesc,
                    Status = status,
                    TransactionReference = request.TransactionReference,
                    RawResponse = JsonSerializer.Deserialize<dynamic>(responseContent)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying B2C status: {ex.Message}");
                return new B2CPaymentResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Query error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<B2CPaymentResponse> ReverseB2CPaymentAsync(string companyCode, string transactionReference, string reason, string reversedBy)
        {
            try
            {
                _logger.LogInformation($"Reversing B2C payment: Company={companyCode}, Reference={transactionReference}");

                // Get the original transaction to get the amount and recipient
                var apiTx = await _appDbContext.ApiTransactions
                    .FirstOrDefaultAsync(t => t.ConversationId == transactionReference && t.CompanyCode == companyCode);

                if (apiTx == null)
                {
                    return new B2CPaymentResponse
                    {
                        Success = false,
                        ResponseCode = "404",
                        ResponseDescription = "Transaction not found"
                    };
                }

                var apiBaseUrl = _configuration["MpesaApi:BaseUrl"] ?? "http://localhost:5000";
                var endpoint = $"{apiBaseUrl}/api/Apis/Simulate";

                var payload = new
                {
                    action = "reversal",
                    amount = apiTx.Amount,
                    phone = apiTx.Recipient,
                    conversationID = transactionReference,
                    companycode = companyCode,
                    ApiKey = _configuration["MpesaApi:DefaultApiKey"],
                    remarks = reason
                };

                var httpClient = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new B2CPaymentResponse
                    {
                        Success = false,
                        ResponseCode = response.StatusCode.ToString(),
                        ResponseDescription = "Reversal failed",
                        ErrorMessage = responseContent
                    };
                }

                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                bool isSuccess = false;
                string resultCode = "0";

                if (root.TryGetProperty("responseCode", out var rc))
                {
                    resultCode = rc.GetString() ?? "0";
                    isSuccess = resultCode == "0";
                }

                return new B2CPaymentResponse
                {
                    Success = isSuccess,
                    ResponseCode = resultCode,
                    ResponseDescription = isSuccess ? "Reversal successful" : "Reversal failed",
                    TransactionReference = transactionReference,
                    RawResponse = JsonSerializer.Deserialize<dynamic>(responseContent)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reversing B2C payment: {ex.Message}");
                return new B2CPaymentResponse
                {
                    Success = false,
                    ResponseCode = "500",
                    ResponseDescription = "Reversal error",
                    ErrorMessage = ex.Message
                };
            }
        }

        #region Helper Methods

        private string GenerateOriginatorConversationID()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss") + Guid.NewGuid().ToString().Substring(0, 8);
        }

        private string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return null;

            // Remove any non-digit characters
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("0") && digits.Length == 10)
            {
                return "254" + digits.Substring(1);
            }
            else if (digits.StartsWith("254") && digits.Length == 12)
            {
                return digits;
            }
            else if (!digits.StartsWith("0") && digits.Length == 9)
            {
                return "254" + digits;
            }

            return digits;
        }

        #endregion
    }
}