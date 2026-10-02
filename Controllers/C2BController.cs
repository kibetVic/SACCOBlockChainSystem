using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class C2BController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IContributionService _contributionService;
        private readonly ILogger<C2BController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // M-Pesa API credentials (store these in appsettings.json in production)
        private readonly string _consumerKey;
        private readonly string _consumerSecret;
        private readonly string _shortCode;
        private readonly string _passkey;

        public C2BController(
            ApplicationDbContext context,
            IContributionService contributionService,
            ILogger<C2BController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _context = context;
            _contributionService = contributionService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            // Load M-Pesa configuration
            _consumerKey = configuration["Mpesa:ConsumerKey"] ?? "";
            _consumerSecret = configuration["Mpesa:ConsumerSecret"] ?? "";
            _shortCode = configuration["Mpesa:ShortCode"] ?? "174379";
            _passkey = configuration["Mpesa:Passkey"] ?? "";
        }

        // GET: /api/C2B/GetMemberPhone
        [HttpGet("GetMemberPhone")]
        public async Task<IActionResult> GetMemberPhone()
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return Unauthorized(new { success = false, message = "Member not found" });
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                return Ok(new
                {
                    success = true,
                    phoneNumber = member.PhoneNo,
                    alternativePhone = member.MobileNo,
                    email = member.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member phone");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: /api/C2B/InitiateMpesaPayment
        [HttpPost("InitiateMpesaPayment")]
        public async Task<IActionResult> InitiateMpesaPayment([FromBody] MpesaPaymentRequest request)
        {
            try
            {
                _logger.LogInformation($"Initiating M-Pesa payment for amount: {request.Amount}");

                // Validate request
                if (request.Amount <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid amount" });
                }

                if (string.IsNullOrEmpty(request.PhoneNumber))
                {
                    return BadRequest(new { success = false, message = "Phone number is required" });
                }

                // Format phone number (254712345678)
                var formattedPhone = FormatPhoneNumber(request.PhoneNumber);
                if (string.IsNullOrEmpty(formattedPhone))
                {
                    return BadRequest(new { success = false, message = "Invalid phone number format" });
                }

                // Get member info
                var memberNo = GetLoggedInMemberNumber();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                // Generate unique transaction ID
                var transactionId = GenerateTransactionId();
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                // Generate password for STK push
                var password = GenerateMpesaPassword(_shortCode, _passkey, timestamp);

                // In production, you would call the actual M-Pesa API here
                // For now, we'll simulate the response

                // Simulate STK push response
                var checkoutRequestId = $"ws_CO_{DateTime.Now.Ticks}";
                var responseCode = "0";
                var responseDescription = "Success. Request accepted for processing";

                // Store transaction details
                var transactionDetail = new TransactionDetail
                {
                    CompanyCode = member.CompanyCode ?? "000",
                    TransactionId = transactionId,
                    TransactionCode = "MPESA_STK_PUSH",
                    ResultCode = responseCode == "0" ? 0 : 1,
                    ResultMessage = responseDescription,
                    Amount = request.Amount,
                    Status = "PENDING",
                    ConversationId = checkoutRequestId,
                    ShortCode = int.Parse(_shortCode),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    MemberNo = member.MemberNo,
                    Phone = formattedPhone,
                    OriginatorConversationId = transactionId,
                    MerchantRequestId = transactionId,
                    CheckoutRequestId = checkoutRequestId
                };

                _context.Transaction_Detail.Add(transactionDetail);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"M-Pesa STK push initiated: {checkoutRequestId}");

                return Ok(new
                {
                    success = true,
                    message = "STK push sent to your phone. Please enter your PIN to complete payment.",
                    checkoutRequestId = checkoutRequestId,
                    transactionId = transactionId,
                    amount = request.Amount,
                    phoneNumber = formattedPhone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating M-Pesa payment");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: /api/C2B/CheckPaymentStatus
        [HttpPost("CheckPaymentStatus")]
        public async Task<IActionResult> CheckPaymentStatus([FromBody] PaymentStatusRequest request)
        {
            try
            {
                var transaction = await _context.Transaction_Detail
                    .FirstOrDefaultAsync(t => t.CheckoutRequestId == request.CheckoutRequestId);

                if (transaction == null)
                {
                    return NotFound(new { success = false, message = "Transaction not found" });
                }

                // In production, you would query the actual M-Pesa API here
                // For simulation, we'll check if enough time has passed
                var timeElapsed = DateTime.Now - (transaction.CreatedAt ?? DateTime.Now);

                // Simulate payment confirmation after 5 seconds (for testing)
                if (timeElapsed.TotalSeconds > 5 && transaction.Status == "PENDING")
                {
                    // Simulate successful payment
                    transaction.Status = "COMPLETED";
                    transaction.ResultCode = 0;
                    transaction.ResultMessage = "Payment completed successfully";
                    transaction.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        status = "COMPLETED",
                        message = "Payment completed successfully",
                        amount = transaction.Amount
                    });
                }

                return Ok(new
                {
                    success = true,
                    status = transaction.Status,
                    message = transaction.Status == "PENDING" ? "Waiting for payment confirmation..." : transaction.ResultMessage,
                    amount = transaction.Amount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment status");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: /api/C2B/ConfirmPayment
        [HttpPost("ConfirmPayment")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            try
            {
                var transaction = await _context.Transaction_Detail
                    .FirstOrDefaultAsync(t => t.CheckoutRequestId == request.CheckoutRequestId);

                if (transaction == null)
                {
                    return NotFound(new { success = false, message = "Transaction not found" });
                }

                if (transaction.Status != "COMPLETED")
                {
                    return BadRequest(new { success = false, message = "Payment not completed yet" });
                }

                // Get member info
                var memberNo = GetLoggedInMemberNumber();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                // Create contribution record
                var contributionDto = new ContributionDTO
                {
                    MemberNo = member.MemberNo,
                    Amount = transaction.Amount ?? 0,
                    TransactionDate = DateTime.Now,
                    DepositedDate = DateTime.Now,
                    SharesCode = request.ShareTypeCode,
                    PaymentMethod = "MPESA",
                    ReferenceNo = transaction.CheckoutRequestId,
                    Remarks = $"M-Pesa payment via {transaction.Phone} - {request.Remarks}",
                    CreatedBy = member.MemberNo,
                    CompanyCode = member.CompanyCode ?? "000"
                };

                var result = await _contributionService.AddContributionAsync(contributionDto);

                // Update transaction with blockchain reference
                transaction.BlockchainTxId = result.BlockchainTxId;
                transaction.TransactionCode = "CONTRIBUTION";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Contribution saved successfully",
                    receiptNo = result.ReceiptNo,
                    amount = transaction.Amount,
                    contributionId = result.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming payment and saving contribution");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: /api/C2B/InitiateBankTransfer
        [HttpPost("InitiateBankTransfer")]
        public async Task<IActionResult> InitiateBankTransfer([FromBody] BankTransferRequest request)
        {
            try
            {
                _logger.LogInformation($"Initiating Bank Transfer for amount: {request.Amount}");

                // Validate request
                if (request.Amount <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid amount" });
                }

                if (string.IsNullOrEmpty(request.AccountNumber))
                {
                    return BadRequest(new { success = false, message = "Account number is required" });
                }

                if (string.IsNullOrEmpty(request.BankName))
                {
                    return BadRequest(new { success = false, message = "Bank name is required" });
                }

                // Get member info
                var memberNo = GetLoggedInMemberNumber();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                // Generate unique transaction ID
                var transactionId = GenerateTransactionId();

                // Store bank transfer details
                var transactionDetail = new TransactionDetail
                {
                    CompanyCode = member.CompanyCode ?? "000",
                    TransactionId = transactionId,
                    TransactionCode = "BANK_TRANSFER_INITIATED",
                    ResultCode = 0,
                    ResultMessage = "Bank transfer initiated",
                    Amount = request.Amount,
                    Status = "PENDING",
                    ConversationId = transactionId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    MemberNo = member.MemberNo,
                    OriginatorConversationId = transactionId
                };

                _context.Transaction_Detail.Add(transactionDetail);
                await _context.SaveChangesAsync();

                // Generate payment instructions
                var paymentInstructions = GenerateBankPaymentInstructions(request.BankName, request.AccountNumber, request.Amount, member);

                return Ok(new
                {
                    success = true,
                    message = "Bank transfer initiated. Please complete the payment using the instructions below.",
                    transactionId = transactionId,
                    amount = request.Amount,
                    paymentInstructions = paymentInstructions,
                    bankName = request.BankName,
                    accountNumber = request.AccountNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating bank transfer");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: /api/C2B/ConfirmBankTransfer
        [HttpPost("ConfirmBankTransfer")]
        public async Task<IActionResult> ConfirmBankTransfer([FromBody] ConfirmBankTransferRequest request)
        {
            try
            {
                var transaction = await _context.Transaction_Detail
                    .FirstOrDefaultAsync(t => t.TransactionId == request.TransactionId);

                if (transaction == null)
                {
                    return NotFound(new { success = false, message = "Transaction not found" });
                }

                if (transaction.Status == "COMPLETED")
                {
                    return BadRequest(new { success = false, message = "Transaction already completed" });
                }

                // Get member info
                var memberNo = GetLoggedInMemberNumber();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                // Update transaction status
                transaction.Status = "COMPLETED";
                transaction.ResultMessage = "Bank transfer confirmed";
                transaction.UpdatedAt = DateTime.Now;
                transaction.TransactionId = request.ReferenceNumber;
                await _context.SaveChangesAsync();

                // Create contribution record
                var contributionDto = new ContributionDTO
                {
                    MemberNo = member.MemberNo,
                    Amount = transaction.Amount ?? 0,
                    TransactionDate = DateTime.Now,
                    DepositedDate = DateTime.Now,
                    SharesCode = request.ShareTypeCode,
                    PaymentMethod = "BANK_TRANSFER",
                    ReferenceNo = request.ReferenceNumber,
                    Remarks = $"Bank transfer from {request.BankName} - {request.AccountNumber} - {request.Remarks}",
                    CreatedBy = member.MemberNo,
                    CompanyCode = member.CompanyCode ?? "000"
                };

                var result = await _contributionService.AddContributionAsync(contributionDto);

                // Update transaction with blockchain reference
                transaction.BlockchainTxId = result.BlockchainTxId;
                transaction.TransactionCode = "CONTRIBUTION";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Contribution saved successfully",
                    receiptNo = result.ReceiptNo,
                    amount = transaction.Amount,
                    contributionId = result.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming bank transfer");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

        private string GetLoggedInMemberNumber()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null) return null;

                var memberNoClaim = user.FindFirst("MemberNo")?.Value;
                if (!string.IsNullOrEmpty(memberNoClaim))
                {
                    return memberNoClaim;
                }

                var nameClaim = user.Identity?.Name;
                if (!string.IsNullOrEmpty(nameClaim))
                {
                    var member = _context.Members.FirstOrDefault(m => m.MemberNo == nameClaim);
                    return member?.MemberNo;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logged-in member number");
                return null;
            }
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            // Remove any non-digit characters
            var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (cleaned.StartsWith("0"))
            {
                cleaned = "254" + cleaned.Substring(1);
            }
            else if (cleaned.StartsWith("254"))
            {
                // Already in correct format
            }
            else if (cleaned.Length == 9 && cleaned.StartsWith("7"))
            {
                cleaned = "254" + cleaned;
            }
            else
            {
                return null;
            }

            return cleaned;
        }

        private string GenerateTransactionId()
        {
            return $"TXN{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 15).ToUpper()}";
        }

        private string GenerateMpesaPassword(string shortCode, string passkey, string timestamp)
        {
            var data = $"{shortCode}{passkey}{timestamp}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private object GenerateBankPaymentInstructions(string bankName, string accountNumber, decimal amount, Member member)
        {
            return new
            {
                bankName = bankName,
                accountNumber = accountNumber,
                accountName = $"{member.Surname} {member.OtherNames}",
                amount = amount,
                reference = $"CONTRIB_{member.MemberNo}_{DateTime.Now:yyyyMMddHHmmss}",
                instructions = new List<string>
                {
                    $"1. Log into your {bankName} online banking or visit any {bankName} branch",
                    $"2. Transfer KES {amount:N2} to the account number: {accountNumber}",
                    $"3. Account Name: {member.Surname} {member.OtherNames}",
                    $"4. Use the reference: CONTRIB_{member.MemberNo}_{DateTime.Now:yyyyMMddHHmmss}",
                    "5. After transfer, click 'I Have Made the Payment' to confirm"
                }
            };
        }

        #endregion
    }

    #region Request/Response Models

    public class MpesaPaymentRequest
    {
        public decimal Amount { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShareTypeCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public bool UseRegisteredPhone { get; set; } = true;
    }

    public class PaymentStatusRequest
    {
        public string CheckoutRequestId { get; set; } = string.Empty;
    }

    public class ConfirmPaymentRequest
    {
        public string CheckoutRequestId { get; set; } = string.Empty;
        public string ShareTypeCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class BankTransferRequest
    {
        public decimal Amount { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string ShareTypeCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class ConfirmBankTransferRequest
    {
        public string TransactionId { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string ShareTypeCode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    #endregion
}