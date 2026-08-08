// Controllers/MpesaC2BApiController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Text.Json;
using System.Text;

namespace SACCOBlockChainSystem.Controllers.Api
{
    [ApiController]
    [Route("api/mpesa/c2b")]
    [AllowAnonymous] // C2B callbacks come from Safaricom, not authenticated users
    public class MpesaC2BApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoanService _loanService;
        private readonly IContributionService _contributionService;
        private readonly IMemberService _memberService;
        private readonly ILogger<MpesaC2BApiController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public MpesaC2BApiController(
            ApplicationDbContext context,
            ILoanService loanService,
            IMemberService memberService,
            IContributionService contributionService,
            ILogger<MpesaC2BApiController> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _loanService = loanService;
            _memberService = memberService;
            _contributionService = contributionService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        /// <summary>
        /// Validation URL - Safaricom calls this before processing the payment
        /// </summary>
        [HttpPost("validation")]
        public async Task<IActionResult> ValidateTransaction([FromBody] MpesaC2BCallbackDTO callback)
        {
            _logger.LogInformation($"C2B Validation received: {JsonSerializer.Serialize(callback)}");

            try
            {
                // Validate the transaction
                var (isValid, message, companyCode) = await ValidatePaymentAsync(callback);

                if (!isValid)
                {
                    _logger.LogWarning($"C2B Validation failed: {message}");
                    return Ok(new MpesaValidationResponseDTO
                    {
                        ResultCode = 1,
                        ResultDesc = message
                    });
                }

                // Create pending records before confirmation
                await CreatePendingTransactionRecords(callback, companyCode);

                _logger.LogInformation($"C2B Validation successful for transaction {callback.TransactionId}");
                return Ok(new MpesaValidationResponseDTO
                {
                    ResultCode = 0,
                    ResultDesc = "Accepted"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in C2B validation for transaction {callback.TransactionId}");
                return Ok(new MpesaValidationResponseDTO
                {
                    ResultCode = 1,
                    ResultDesc = $"Validation error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Confirmation URL - Safaricom calls this after successful payment
        /// </summary>
        [HttpPost("confirmation")]
        public async Task<IActionResult> ConfirmTransaction([FromBody] MpesaC2BCallbackDTO callback)
        {
            _logger.LogInformation($"C2B Confirmation received: {JsonSerializer.Serialize(callback)}");

            try
            {
                // Process the payment
                var (success, message) = await ProcessLoanRepaymentAsync(callback);

                if (!success)
                {
                    _logger.LogWarning($"C2B Confirmation failed: {message}");
                    return Ok(new MpesaConfirmationResponseDTO
                    {
                        ResultCode = 1,
                        ResultDesc = message
                    });
                }

                _logger.LogInformation($"C2B Confirmation successful for transaction {callback.TransactionId}");
                return Ok(new MpesaConfirmationResponseDTO
                {
                    ResultCode = 0,
                    ResultDesc = "Success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in C2B confirmation for transaction {callback.TransactionId}");
                return Ok(new MpesaConfirmationResponseDTO
                {
                    ResultCode = 1,
                    ResultDesc = $"Confirmation error: {ex.Message}"
                });
            }
        }

        #region Private Helper Methods

        private async Task<(bool IsValid, string Message, string CompanyCode)> ValidatePaymentAsync(MpesaC2BCallbackDTO callback)
        {
            try
            {
                // 1. Check if transaction already processed
                var existingTransaction = await _context.ApiTransactions
                    .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);

                if (existingTransaction != null)
                {
                    return (false, "Transaction already processed", string.Empty);
                }

                // 2. Parse Account Reference to determine transaction type
                var (transactionType, reference) = ParseAccountReference(callback.AccountReference);

                if (transactionType != "LOAN")
                {
                    // Not a loan repayment - we can still validate it's a valid account
                    // For now, only loan repayments are supported
                    return (false, "Invalid account reference format. Use LOAN:LOANNO", string.Empty);
                }

                // 3. Get company code from Business ShortCode
                var apiSettings = await _context.ApiTables
                    .FirstOrDefaultAsync(a => a.ShortCode.ToString() == callback.BusinessShortCode && a.Status == "Active");

                if (apiSettings == null)
                {
                    return (false, $"Business ShortCode {callback.BusinessShortCode} not found", string.Empty);
                }

                string companyCode = apiSettings.CompanyCode ?? string.Empty;

                // 4. Validate loan exists
                var loan = await _loanService.GetLoanByNoAsync(reference, companyCode);
                if (loan == null)
                {
                    return (false, $"Loan {reference} not found", companyCode);
                }

                // 5. Check if loan is active (Disbursed or Endorsed)
                bool isActiveLoan = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;
                if (!isActiveLoan)
                {
                    return (false, $"Loan {reference} is not active for repayment. Current status: {(Status)loan.Status}", companyCode);
                }

                // 6. Validate amount is positive
                if (callback.Amount <= 0)
                {
                    return (false, "Invalid amount", companyCode);
                }

                return (true, "Validation passed", companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ValidatePaymentAsync");
                return (false, $"Validation error: {ex.Message}", string.Empty);
            }
        }

        private async Task CreatePendingTransactionRecords(MpesaC2BCallbackDTO callback, string companyCode)
        {
            var (transactionType, reference) = ParseAccountReference(callback.AccountReference);
            var conversationId = Guid.NewGuid().ToString();
            var checkoutId = callback.TransactionId;

            // Create ApiTransaction record (Pending)
            var apiTransaction = new ApiTransaction
            {
                CompanyCode = companyCode,
                ApiUser = "MPESA_C2B",
                ShortCode = int.TryParse(callback.BusinessShortCode, out int shortCode) ? shortCode : 0,
                TransactionCode = callback.TransactionId,
                CheckoutId = checkoutId,
                ConversationId = conversationId,
                Amount = callback.Amount,
                Recipient = callback.PhoneNumber,
                StatusCode = 1, // 1 = Pending
                ResultDescription = "C2B payment pending confirmation",
                created_at = DateTime.Now,
                updated_at = DateTime.Now,
                LoanNo = reference,
                AuditDateTime = DateTime.Now
            };

            _context.ApiTransactions.Add(apiTransaction);
            await _context.SaveChangesAsync();

            // Create TransactionDetail record (Pending)
            var transactionDetail = new TransactionDetail
            {
                CompanyCode = companyCode,
                TransactionId = Guid.NewGuid().ToString(),
                TransactionCode = callback.TransactionId,
                ResultCode = 1, // 1 = Pending
                ResultMessage = "C2B payment pending confirmation",
                Amount = callback.Amount,
                Status = "Pending",
                ConversationId = conversationId,
                ShortCode = int.TryParse(callback.BusinessShortCode, out shortCode) ? shortCode : 0,
                UpdatedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                MemberNo = reference, // Will be updated when we get member info
                Phone = callback.PhoneNumber,
                OriginatorConversationId = callback.TransactionId,
                MerchantRequestId = callback.TransactionId,
                CheckoutRequestId = checkoutId,
                AuditDateTime = DateTime.Now
            };

            _context.Transaction_Detail.Add(transactionDetail);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Pending records created for transaction {callback.TransactionId}");
        }

        private async Task<(bool Success, string Message)> ProcessLoanRepaymentAsync(MpesaC2BCallbackDTO callback)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Parse Account Reference
                var (transactionType, reference) = ParseAccountReference(callback.AccountReference);

                if (transactionType != "LOAN")
                {
                    return (false, "Not a loan repayment transaction");
                }

                // 2. Get company code from Business ShortCode
                var apiSettings = await _context.ApiTables
                    .FirstOrDefaultAsync(a => a.ShortCode.ToString() == callback.BusinessShortCode && a.Status == "Active");

                if (apiSettings == null)
                {
                    return (false, $"Business ShortCode {callback.BusinessShortCode} not found");
                }

                string companyCode = apiSettings.CompanyCode ?? string.Empty;

                // 3. Get loan details
                var loan = await _loanService.GetLoanByNoAsync(reference, companyCode);
                if (loan == null)
                {
                    return (false, $"Loan {reference} not found");
                }

                // 4. Get member details for phone number validation
                var member = await _contributionService.GetMemberByMemberNoAsync(loan.MemberNo);
                string cleanedCallbackPhone = CleanPhoneNumber(callback.PhoneNumber);
                string cleanedMemberPhone = CleanPhoneNumber(member?.PhoneNo ?? member?.MobileNo ?? "");

                // Log phone number matching for debugging (don't block if mismatch, just log)
                if (!string.IsNullOrEmpty(cleanedMemberPhone) && cleanedCallbackPhone != cleanedMemberPhone)
                {
                    _logger.LogWarning($"Phone number mismatch for loan {reference}. Member phone: {cleanedMemberPhone}, Callback phone: {cleanedCallbackPhone}");
                }

                // 5. Get M-Pesa GL Account from settings
                string mpesaGlAccount = await GetMpesaGlAccountAsync(companyCode);

                // 6. Process the repayment - THIS IS THE KEY: ANY AMOUNT WORKS!
                // The ProcessRepaymentAsync method already handles:
                // - ANY amount (full installment, partial, or overpayment)
                // - Automatic distribution: Penalty → Interest → Principal → Overpayment
                // - Early full settlement detection
                // - Schedule updates
                // - Loan status updates
                var repaymentDto = new LoanRepaymentDTO
                {
                    LoanNo = reference,
                    MemberNo = loan.MemberNo,
                    PaymentDate = DateTime.Now,
                    AmountPaid = callback.Amount,  // ANY amount - the method handles it!
                    PaymentMethod = "MPESA",
                    //GlAccountNo = mpesaGlAccount,
                    ReferenceNo = callback.TransactionId,
                    Remarks = $"C2B Repayment via M-Pesa. Phone: {callback.PhoneNumber}",
                    ReceivedBy = "MPESA_C2B_SYSTEM",
                    CompanyCode = companyCode
                };

                var repayment = await _loanService.ProcessRepaymentAsync(repaymentDto);

                // 7. Update ApiTransaction record to Success
                var apiTransaction = await _context.ApiTransactions
                    .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);

                if (apiTransaction != null)
                {
                    apiTransaction.StatusCode = 0; // 0 = Success
                    apiTransaction.ResultDescription = $"Loan repayment processed successfully. Receipt: {repayment.ReceiptNo}";
                    apiTransaction.updated_at = DateTime.Now;
                    apiTransaction.AuditDateTime = DateTime.Now;
                    _context.ApiTransactions.Update(apiTransaction);
                }

                // 8. Update TransactionDetail record to Success
                var transactionDetail = await _context.Transaction_Detail
                    .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);

                if (transactionDetail != null)
                {
                    transactionDetail.ResultCode = 0;
                    transactionDetail.ResultMessage = $"Loan repayment processed successfully. Receipt: {repayment.ReceiptNo}";
                    transactionDetail.Status = "Completed";
                    transactionDetail.UpdatedAt = DateTime.Now;
                    transactionDetail.MemberNo = loan.MemberNo;
                    transactionDetail.AuditDateTime = DateTime.Now;
                    _context.Transaction_Detail.Update(transactionDetail);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"C2B Repayment processed: Loan {reference}, Amount {callback.Amount:C}, Receipt {repayment.ReceiptNo}");

                return (true, "Repayment processed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing C2B repayment for transaction {callback.TransactionId}");

                // Update records to Failed status
                try
                {
                    var apiTransaction = await _context.ApiTransactions
                        .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);

                    if (apiTransaction != null)
                    {
                        apiTransaction.StatusCode = 2; // 2 = Failed
                        apiTransaction.ResultDescription = $"Error: {ex.Message}";
                        apiTransaction.updated_at = DateTime.Now;
                        _context.ApiTransactions.Update(apiTransaction);
                    }

                    var transactionDetail = await _context.Transaction_Detail
                        .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);

                    if (transactionDetail != null)
                    {
                        transactionDetail.ResultCode = 2;
                        transactionDetail.ResultMessage = $"Error: {ex.Message}";
                        transactionDetail.Status = "Failed";
                        transactionDetail.UpdatedAt = DateTime.Now;
                        _context.Transaction_Detail.Update(transactionDetail);
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Error updating transaction records to failed status");
                }

                return (false, $"Error: {ex.Message}");
            }
        }

        private (string Type, string Reference) ParseAccountReference(string accountReference)
        {
            if (string.IsNullOrEmpty(accountReference))
                return ("UNKNOWN", string.Empty);

            var parts = accountReference.Split(':');
            if (parts.Length == 2)
            {
                return (parts[0].ToUpper(), parts[1]);
            }

            return ("UNKNOWN", accountReference);
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return string.Empty;

            // Remove +, spaces, dashes
            phone = phone.Replace("+", "").Replace(" ", "").Replace("-", "");

            // Convert 07XXXXXXXX to 2547XXXXXXXX
            if (phone.StartsWith("0") && phone.Length == 10)
            {
                phone = "254" + phone.Substring(1);
            }

            return phone;
        }

        private async Task<string> GetMpesaGlAccountAsync(string companyCode)
        {
            // Get the active M-Pesa Bank record for this company
            // M-Pesa settlements go to a specific bank account linked to your Paybill
            var mpesaBank = await _context.Banks
                .FirstOrDefaultAsync(b => b.CompanyCode == companyCode &&
                                         b.IsActive == true &&
                                         (b.BankCode == "MPESA" ||
                                          b.BankName.ToLower().Contains("mpesa") ||
                                          b.BankName.ToLower().Contains("m-pesa") ||
                                          b.BankName.ToLower().Contains("mobile money")));

            if (mpesaBank != null && !string.IsNullOrEmpty(mpesaBank.GlAccountNo))
            {
                _logger.LogInformation($"Using M-Pesa Bank GL Account (DrAccNo): {mpesaBank.GlAccountNo} - {mpesaBank.BankName}");
                return mpesaBank.GlAccountNo;
            }

            // If no dedicated M-Pesa bank, find any active bank with a GL account
            var anyActiveBank = await _context.Banks
                .FirstOrDefaultAsync(b => b.CompanyCode == companyCode &&
                                         b.IsActive == true &&
                                         !string.IsNullOrEmpty(b.GlAccountNo));

            if (anyActiveBank != null)
            {
                _logger.LogWarning($"No M-Pesa bank found. Using default bank: {anyActiveBank.GlAccountNo} - {anyActiveBank.BankName}");
                return anyActiveBank.GlAccountNo;
            }

            // No bank configured at all - throw error
            _logger.LogError($"No active bank with GL Account found for company {companyCode}");
            throw new Exception($"Bank GL Account not configured. Please add a Bank record with a GL Account for M-Pesa settlements.");
        }

        #endregion




        #region contribution Methods

        // POST: /api/mpesa/c2b/contribution/validation
        [HttpPost("contribution/validation")]
        public async Task<IActionResult> ValidateContribution([FromBody] MpesaC2BCallbackDTO callback)
        {
            _logger.LogInformation($"C2B Contribution Validation received: {JsonSerializer.Serialize(callback)}");

            try
            {
                // Check if already processed
                var existing = await _context.ApiTransactions
                    .FirstOrDefaultAsync(t => t.TransactionCode == callback.TransactionId);
                if (existing != null)
                {
                    return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = "Transaction already processed" });
                }

                // Parse account reference (format: CONTRIB:MBR001, SHARE:MBR001, REG:MBR001, DEPOSIT:MBR001)
                var (type, reference) = ParseAccountReference(callback.AccountReference);
                if (type != "CONTRIB" && type != "SHARE" && type != "REG" && type != "DEPOSIT")
                {
                    return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = "Invalid account reference. Use CONTRIB:MemberNo, SHARE:MemberNo, REG:MemberNo, or DEPOSIT:MemberNo" });
                }

                // Get company from shortcode
                var apiSettings = await _context.ApiTables
                    .FirstOrDefaultAsync(a => a.ShortCode.ToString() == callback.BusinessShortCode && a.Status == "Active");
                if (apiSettings == null)
                {
                    return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = "Business ShortCode not found" });
                }

                // Validate member exists
                var member = await _contributionService.GetMemberByMemberNoAsync(reference);
                if (member == null)
                {
                    return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = $"Member {reference} not found" });
                }

                // Validate amount
                if (callback.Amount <= 0)
                {
                    return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = "Invalid amount" });
                }

                // Create pending records
                await CreateContributionPendingRecords(callback, apiSettings.CompanyCode, reference);

                _logger.LogInformation($"C2B Contribution Validation successful for member {reference}");
                return Ok(new MpesaValidationResponseDTO { ResultCode = 0, ResultDesc = "Accepted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in C2B contribution validation for {callback.TransactionId}");
                return Ok(new MpesaValidationResponseDTO { ResultCode = 1, ResultDesc = $"Error: {ex.Message}" });
            }
        }

        // POST: /api/mpesa/c2b/contribution/confirmation
        [HttpPost("contribution/confirmation")]
        public async Task<IActionResult> ConfirmContribution([FromBody] MpesaC2BCallbackDTO callback)
        {
            _logger.LogInformation($"C2B Contribution Confirmation received: {JsonSerializer.Serialize(callback)}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var (type, reference) = ParseAccountReference(callback.AccountReference);

                var apiSettings = await _context.ApiTables
                    .FirstOrDefaultAsync(a => a.ShortCode.ToString() == callback.BusinessShortCode && a.Status == "Active");
                if (apiSettings == null)
                {
                    return Ok(new MpesaConfirmationResponseDTO { ResultCode = 1, ResultDesc = "Business ShortCode not found" });
                }

                var member = await _contributionService.GetMemberByMemberNoAsync(reference);
                if (member == null)
                {
                    return Ok(new MpesaConfirmationResponseDTO { ResultCode = 1, ResultDesc = $"Member {reference} not found" });
                }

                // Determine share type based on transaction type
                string sharesCode = await DetermineShareTypeForContribution(type, member, callback.Amount, apiSettings.CompanyCode);
                if (string.IsNullOrEmpty(sharesCode))
                {
                    return Ok(new MpesaConfirmationResponseDTO { ResultCode = 1, ResultDesc = "Could not determine share type for this payment" });
                }

                // Get debit account from Bank (M-Pesa settlement account)
                string drAccNo = await GetMpesaDebitAccountAsync(apiSettings.CompanyCode);
                if (string.IsNullOrEmpty(drAccNo))
                {
                    return Ok(new MpesaConfirmationResponseDTO { ResultCode = 1, ResultDesc = "M-Pesa settlement account not configured. Please add a Bank record for M-PESA." });
                }

                var contributionDto = new ContributionDTO
                {
                    MemberNo = reference,
                    Amount = callback.Amount,
                    TransactionDate = DateTime.Now,
                    PaymentMethod = "MOBILE MONEY",
                    ReferenceNo = callback.TransactionId,
                    SharesCode = sharesCode,
                    Remarks = $"C2B Contribution via M-Pesa. Phone: {callback.PhoneNumber}. Ref: {callback.AccountReference}",
                    CreatedBy = "MPESA_C2B_SYSTEM",
                    CompanyCode = apiSettings.CompanyCode
                };

                var result = await _contributionService.AddContributionAsync(contributionDto);

                // Update records to success
                await UpdateContributionRecordsToSuccess(callback.TransactionId, result.ReceiptNo, reference);

                await transaction.CommitAsync();

                _logger.LogInformation($"C2B Contribution processed: Member {reference}, Amount {callback.Amount:C}, Receipt {result.ReceiptNo}");
                return Ok(new MpesaConfirmationResponseDTO { ResultCode = 0, ResultDesc = "Success" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing C2B contribution for {callback.TransactionId}");
                await UpdateContributionRecordsToFailed(callback.TransactionId, ex.Message);
                return Ok(new MpesaConfirmationResponseDTO { ResultCode = 1, ResultDesc = $"Error: {ex.Message}" });
            }
        }

        private async Task CreateContributionPendingRecords(MpesaC2BCallbackDTO callback, string companyCode, string memberNo)
        {
            var conversationId = Guid.NewGuid().ToString();
            int shortCode = int.TryParse(callback.BusinessShortCode, out int sc) ? sc : 0;

            var apiTransaction = new ApiTransaction
            {
                CompanyCode = companyCode,
                ApiUser = "MPESA_C2B",
                ShortCode = shortCode,
                TransactionCode = callback.TransactionId,
                CheckoutId = callback.TransactionId,
                ConversationId = conversationId,
                Amount = callback.Amount,
                Recipient = callback.PhoneNumber,
                StatusCode = 1,
                ResultDescription = "C2B contribution pending confirmation",
                created_at = DateTime.Now,
                updated_at = DateTime.Now,
                LoanNo = memberNo,
                AuditDateTime = DateTime.Now
            };
            _context.ApiTransactions.Add(apiTransaction);

            var transactionDetail = new TransactionDetail
            {
                CompanyCode = companyCode,
                TransactionId = Guid.NewGuid().ToString(),
                TransactionCode = callback.TransactionId,
                ResultCode = 1,
                ResultMessage = "C2B contribution pending confirmation",
                Amount = callback.Amount,
                Status = "Pending",
                ConversationId = conversationId,
                ShortCode = shortCode,
                UpdatedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                MemberNo = memberNo,
                Phone = callback.PhoneNumber,
                OriginatorConversationId = callback.TransactionId,
                MerchantRequestId = callback.TransactionId,
                CheckoutRequestId = callback.TransactionId,
                AuditDateTime = DateTime.Now
            };
            _context.Transaction_Detail.Add(transactionDetail);

            await _context.SaveChangesAsync();
        }

        private async Task UpdateContributionRecordsToSuccess(string transactionId, string receiptNo, string memberNo)
        {
            var apiTransaction = await _context.ApiTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionId);
            if (apiTransaction != null)
            {
                apiTransaction.StatusCode = 0;
                apiTransaction.ResultDescription = $"Contribution processed. Receipt: {receiptNo}";
                apiTransaction.updated_at = DateTime.Now;
                _context.ApiTransactions.Update(apiTransaction);
            }

            var transactionDetail = await _context.Transaction_Detail
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionId);
            if (transactionDetail != null)
            {
                transactionDetail.ResultCode = 0;
                transactionDetail.ResultMessage = $"Contribution processed. Receipt: {receiptNo}";
                transactionDetail.Status = "Completed";
                transactionDetail.MemberNo = memberNo;
                transactionDetail.UpdatedAt = DateTime.Now;
                _context.Transaction_Detail.Update(transactionDetail);
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpdateContributionRecordsToFailed(string transactionId, string errorMessage)
        {
            var apiTransaction = await _context.ApiTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionId);
            if (apiTransaction != null)
            {
                apiTransaction.StatusCode = 2;
                apiTransaction.ResultDescription = $"Error: {errorMessage}";
                apiTransaction.updated_at = DateTime.Now;
                _context.ApiTransactions.Update(apiTransaction);
            }

            var transactionDetail = await _context.Transaction_Detail
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionId);
            if (transactionDetail != null)
            {
                transactionDetail.ResultCode = 2;
                transactionDetail.ResultMessage = $"Error: {errorMessage}";
                transactionDetail.Status = "Failed";
                transactionDetail.UpdatedAt = DateTime.Now;
                _context.Transaction_Detail.Update(transactionDetail);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string> DetermineShareTypeForContribution(string transactionType, Member member, decimal amount, string companyCode)
        {
            var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);
            if (!shareTypes.Any()) return string.Empty;

            switch (transactionType)
            {
                case "REG":
                    return shareTypes.FirstOrDefault(st =>
                        st.SharesType.Contains("REG", StringComparison.OrdinalIgnoreCase) ||
                        st.SharesType.Contains("FEE", StringComparison.OrdinalIgnoreCase))?.SharesCode
                        ?? shareTypes.First().SharesCode;

                case "SHARE":
                    return shareTypes.FirstOrDefault(st => st.IsMainShares)?.SharesCode
                        ?? shareTypes.First().SharesCode;

                case "DEPOSIT":
                    return shareTypes.FirstOrDefault(st =>
                        st.Withdrawable == true && (st.UsedToGuarantee == true || st.UsedToOffset == true))?.SharesCode
                        ?? shareTypes.First().SharesCode;

                case "CONTRIB":
                default:
                    return shareTypes.FirstOrDefault(st => amount >= st.MinAmount)?.SharesCode
                        ?? shareTypes.First().SharesCode;
            }
        }

        private async Task<string> GetMpesaDebitAccountAsync(string companyCode)
        {
            // Look for M-Pesa bank record in Banks table
            var mpesaBank = await _context.Banks
                .FirstOrDefaultAsync(b => b.CompanyCode == companyCode &&
                                         b.IsActive == true &&
                                         (b.BankCode == "MPESA" ||
                                          b.BankName.ToLower().Contains("mpesa") ||
                                          b.BankName.ToLower().Contains("m-pesa") ||
                                          b.BankName.ToLower().Contains("mobile money")));

            if (mpesaBank != null && !string.IsNullOrEmpty(mpesaBank.GlAccountNo))
            {
                _logger.LogInformation($"Using M-Pesa Bank GL Account: {mpesaBank.GlAccountNo} - {mpesaBank.BankName}");
                return mpesaBank.GlAccountNo;
            }

            // Fallback: any active bank with GL account
            var anyBank = await _context.Banks
                .FirstOrDefaultAsync(b => b.CompanyCode == companyCode && b.IsActive == true && !string.IsNullOrEmpty(b.GlAccountNo));

            if (anyBank != null && !string.IsNullOrEmpty(anyBank.GlAccountNo))
            {
                _logger.LogWarning($"No M-Pesa bank found. Using default bank: {anyBank.GlAccountNo} - {anyBank.BankName}");
                return anyBank.GlAccountNo;
            }

            // Last resort: find Cash account in GL Setup
            var cashAccount = await _context.GlSetup
                .FirstOrDefaultAsync(g => g.CompanyCode == companyCode &&
                                         g.Status == true &&
                                         g.Type == "ASSET" &&
                                         (g.Glaccname != null && g.Glaccname.ToLower().Contains("cash")));

            if (cashAccount != null)
            {
                _logger.LogWarning($"No bank found. Using Cash GL Account: {cashAccount.AccNo} - {cashAccount.Glaccname}");
                return cashAccount.AccNo;
            }

            _logger.LogError($"No M-Pesa settlement account configured for company {companyCode}");
            return string.Empty;
        }

        #endregion
    }
}