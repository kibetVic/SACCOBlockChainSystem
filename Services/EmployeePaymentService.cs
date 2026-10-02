// Services/EmployeePaymentService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockchainDb.Models;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface IEmployeePaymentService
    {
        Task<EmployeePaymentResponseDTO> ProcessPaymentAsync(EmployeePaymentDTO dto, string UserName, string companyCode);
        Task<EmployeePaymentResponseDTO> GetPaymentByIdAsync(long paymentId);
        Task<List<EmployeePaymentListDTO>> GetAllPaymentsAsync(string companyCode);
        Task<List<EmployeePaymentListDTO>> GetPaymentsByEmployeeAsync(string employeeIdNo, string companyCode);
        Task<PaymentSummaryDTO> GetPaymentSummaryAsync(string companyCode, DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> ReversePaymentAsync(long paymentId, string UserName, string reason);
        Task<Dictionary<string, string>> GetAvailableExpenseAccountsAsync(string companyCode);
        Task<Dictionary<string, string>> GetAvailableCashAccountsAsync(string companyCode);
        Task<List<PaymentTypeSimpleDTO>> GetPaymentTypesAsync(string companyCode);
        Task<decimal> GetEmployeeBalanceAsync(string employeeIdNo, string companyCode);
    }

    public class EmployeePaymentService : IEmployeePaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<EmployeePaymentService> _logger;
        private readonly IMpesaApiService _mpesaApiService; 
        private readonly IConfiguration _configuration;

        public EmployeePaymentService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IMpesaApiService mpesaApiService,              
            IConfiguration configuration,
            ILogger<EmployeePaymentService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
            _mpesaApiService = mpesaApiService;              
            _configuration = configuration;
        }

        public async Task<List<PaymentTypeSimpleDTO>> GetPaymentTypesAsync(string companyCode)
        {
            return await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Code)
                .Select(p => new PaymentTypeSimpleDTO
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    DefaultExpenseAccountNo = p.DefaultExpenseAccountNo
                })
                .ToListAsync();
        }

        public async Task<Dictionary<string, string>> GetAvailableExpenseAccountsAsync(string companyCode)
        {
            // Allow ANY active GL account - no restrictions on account type
            var expenseAccounts = await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .Select(g => new { g.AccNo, g.Glaccname })
                .ToListAsync();

            return expenseAccounts.ToDictionary(
                e => e.AccNo ?? string.Empty,
                e => $"{e.AccNo} - {e.Glaccname}"
            );
        }

        public async Task<Dictionary<string, string>> GetAvailableCashAccountsAsync(string companyCode)
        {
            // Allow ANY active GL account - no restrictions on account type or name
            var cashAccounts = await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .Select(g => new { g.AccNo, g.Glaccname })
                .ToListAsync();

            return cashAccounts.ToDictionary(
                c => c.AccNo ?? string.Empty,
                c => $"{c.AccNo} - {c.Glaccname}"
            );
        }

        public async Task<EmployeePaymentResponseDTO> ProcessPaymentAsync(EmployeePaymentDTO dto, string UserName, string companyCode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Processing payment for employee: {dto.EmployeeName}, Amount: {dto.Amount}");

                // Get the payment type from database
                var paymentType = await _context.PaymentTypes
                    .FirstOrDefaultAsync(p => p.Id == dto.PaymentTypeId && p.CompanyCode == companyCode && p.IsActive == true);

                if (paymentType == null)
                {
                    throw new InvalidOperationException($"Payment type with ID {dto.PaymentTypeId} not found or inactive.");
                }

                // Validate employee exists
                var employee = await _context.Agents
                    .FirstOrDefaultAsync(a => a.Id == dto.EmployeeId && a.CompanyCode == companyCode);

                if (employee == null)
                {
                    throw new InvalidOperationException($"Employee with ID {dto.EmployeeId} not found.");
                }

                // Use payment type's default expense account if not specified
                if (string.IsNullOrEmpty(dto.ExpenseAccountNo) && !string.IsNullOrEmpty(paymentType.DefaultExpenseAccountNo))
                {
                    dto.ExpenseAccountNo = paymentType.DefaultExpenseAccountNo;
                }

                // Validate GL accounts
                var expenseAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(g => g.AccNo == dto.ExpenseAccountNo && g.CompanyCode == companyCode);

                if (expenseAccount == null)
                {
                    throw new InvalidOperationException($"Expense account {dto.ExpenseAccountNo} not found.");
                }

                var cashAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(g => g.AccNo == dto.CashAccountNo && g.CompanyCode == companyCode);

                if (cashAccount == null)
                {
                    throw new InvalidOperationException($"Cash/Bank account {dto.CashAccountNo} not found.");
                }

                // Generate Voucher Number
                string voucherNo = await GenerateVoucherNumberAsync(companyCode);
                string transactionNo = GenerateTransactionNumber();

                // ============================================================
                // CHECK IF B2C PAYMENT SHOULD BE SENT
                // ============================================================
                bool sendB2CPayment = dto.SendB2CPayment ?? false;
                string b2cReference = null;
                string b2cConversationId = null;
                bool b2cPaymentSuccess = false;
                string b2cResponseMessage = "";

                // Get employee phone number for B2C
                string employeePhone = employee.MobileNo;
                if (!string.IsNullOrEmpty(employeePhone))
                {
                    employeePhone = employeePhone.Replace("+", "").Replace(" ", "").Replace("-", "");
                    if (employeePhone.StartsWith("0"))
                        employeePhone = "254" + employeePhone.Substring(1);
                    if (!employeePhone.StartsWith("254") && employeePhone.Length == 9)
                        employeePhone = "254" + employeePhone;
                }

                // ============================================================
                // SEND B2C PAYMENT IF ENABLED
                // ============================================================
                if (sendB2CPayment && !string.IsNullOrEmpty(employeePhone) && dto.Amount > 0)
                {
                    try
                    {
                        _logger.LogInformation($"Sending B2C payment of {dto.Amount:C} to employee {employee.Names} ({employeePhone})");

                        var b2cRequest = new B2CPaymentRequest
                        {
                            CompanyCode = companyCode,
                            Amount = dto.Amount,
                            PhoneNumber = employeePhone,
                            Reference = $"EMP-{employee.IdNo ?? employee.Id.ToString()}",
                            Remarks = $"{paymentType.Name} payment to {employee.Names ?? employee.Names} - Voucher: {voucherNo}",
                            SourceModule = "EMPLOYEE_PAYMENT",
                            SourceReference = employee.IdNo ?? employee.Id.ToString(),
                            RecipientName = employee.Names ?? employee.Names,
                            CommandID = "BusinessPayment",
                            CreatedBy = UserName,
                            ApiKey = _configuration["MpesaApi:DefaultApiKey"]
                        };

                        var b2cResponse = await _mpesaApiService.SendB2CPaymentAsync(b2cRequest);

                        if (b2cResponse.Success)
                        {
                            b2cPaymentSuccess = true;
                            b2cReference = b2cResponse.TransactionReference;
                            b2cConversationId = b2cResponse.OriginatorConversationID;
                            b2cResponseMessage = "B2C payment sent successfully";
                            _logger.LogInformation($"B2C payment sent successfully. Reference: {b2cReference}");
                        }
                        else
                        {
                            b2cPaymentSuccess = false;
                            b2cResponseMessage = b2cResponse.ResponseDescription;
                            _logger.LogWarning($"B2C payment failed: {b2cResponse.ResponseDescription}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"B2C payment error: {ex.Message}");
                        b2cPaymentSuccess = false;
                        b2cResponseMessage = $"Error: {ex.Message}";
                    }
                }

                // ============================================================
                // 1. Create Journal Entry (Header)
                // ============================================================
                var journal = new Journal
                {
                    VNO = voucherNo,
                    ACCNO = expenseAccount.AccNo,
                    NAME = employee.Names ?? employee.Names ?? "Unknown",
                    NARATION = $"{paymentType.Name} payment to {employee.Names ?? employee.Names}" +
                               (b2cPaymentSuccess ? $" - B2C Ref: {b2cReference}" : ""),
                    MEMBERNO = employee.IdNo ?? employee.Id.ToString(),
                    SHARETYPE = paymentType.Name,
                    Loanno = "0",
                    AMOUNT = dto.Amount,
                    TRANSTYPE = "PYM",
                    AUDITID = UserName,
                    TRANSDATE = dto.PaymentDate,
                    AUDITDATE = DateTime.Now,
                    POSTED = false,
                    POSTEDDATE = DateTime.Now,
                    Transactionno = transactionNo,
                    CompanyCode = companyCode,
                    BlockchainTxId = null,
                };

                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Journal entry created with ID: {journal.JVID}, Voucher: {voucherNo}");

                // ============================================================
                // 2. Create Journal Listing entries (Debit & Credit)
                // ============================================================
                var debitEntry = new JournalsListing
                {
                    VoucherNo = voucherNo,
                    AccountNo = expenseAccount.AccNo,
                    AccountName = expenseAccount.Glaccname,
                    Narration = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names}",
                    MemberNo = employee.IdNo ?? employee.Id.ToString(),
                    ShareType = paymentType.Name,
                    LoanNo = "0",
                    AmountDr = dto.Amount,
                    AmountCr = 0,
                    Amount = dto.Amount,
                    TransType = "DR",
                    AuditId = UserName,
                    TransDate = dto.PaymentDate,
                    AuditDate = DateTime.Now,
                    Posted = false,
                    PostedDate = DateTime.Now,
                    TransactionNo = transactionNo,
                    CompanyCode = companyCode,
                    BlockchainTxId = null
                };

                var creditEntry = new JournalsListing
                {
                    VoucherNo = voucherNo,
                    AccountNo = cashAccount.AccNo,
                    AccountName = cashAccount.Glaccname,
                    Narration = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names}",
                    MemberNo = employee.IdNo ?? employee.Id.ToString(),
                    ShareType = paymentType.Name,
                    LoanNo = "0",
                    AmountDr = 0,
                    AmountCr = dto.Amount,
                    Amount = dto.Amount,
                    TransType = "CR",
                    AuditId = UserName,
                    TransDate = dto.PaymentDate,
                    AuditDate = DateTime.Now,
                    Posted = false,
                    PostedDate = DateTime.Now,
                    TransactionNo = transactionNo,
                    CompanyCode = companyCode,
                    BlockchainTxId = null
                };

                _context.JournalsListings.AddRange(debitEntry, creditEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Journal listing entries created for voucher: {voucherNo}");

                // ============================================================
                // 3. Create GL Transaction Entries (Gltransaction)
                // ============================================================
                // Debit GL Transaction - Expense Account
                var glTransactionDebit = new Gltransaction
                {
                    TransDate = dto.PaymentDate,
                    Amount = dto.Amount,
                    DrAccNo = expenseAccount.AccNo,
                    CrAccNo = cashAccount.AccNo,
                    Temp = "PAYMENT",
                    DocumentNo = voucherNo,
                    Source = "EmployeePayment",
                    CompanyCode = companyCode,
                    TransDescript = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
                    AuditTime = DateTime.Now,
                    AuditId = UserName,
                    Cash = 1,
                    DocPosted = 0,
                    ChequeNo = dto.PaymentMethod == "Cheque" ? $"CHQ-{DateTime.Now:yyyyMMdd}" : null,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = transactionNo,
                    Module = "AP",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                // Credit GL Transaction - Cash Account
                var glTransactionCredit = new Gltransaction
                {
                    TransDate = dto.PaymentDate,
                    Amount = dto.Amount,
                    DrAccNo = expenseAccount.AccNo,
                    CrAccNo = cashAccount.AccNo,
                    Temp = "PAYMENT",
                    DocumentNo = voucherNo,
                    Source = "EmployeePayment",
                    CompanyCode = companyCode,
                    TransDescript = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
                    AuditTime = DateTime.Now,
                    AuditId = UserName,
                    Cash = 1,
                    DocPosted = 0,
                    ChequeNo = dto.PaymentMethod == "Cheque" ? $"CHQ-{DateTime.Now:yyyyMMdd}" : null,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = transactionNo,
                    Module = "AP",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Gltransactions.AddRange(glTransactionDebit, glTransactionCredit);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"GL Transaction entries created for voucher: {voucherNo}");

                // ============================================================
                // 4. Update General Ledger balances
                // ============================================================
                await UpdateGLBalanceAsync(expenseAccount.AccNo, dto.Amount, "DEBIT", companyCode);
                await UpdateGLBalanceAsync(cashAccount.AccNo, dto.Amount, "CREDIT", companyCode);

                // ============================================================
                // 5. Create GeneralLedger entries
                // ============================================================
                var expenseCurrentBal = expenseAccount.Bal ?? 0;
                var cashCurrentBal = cashAccount.Bal ?? 0;

                var glEntries = new List<GeneralLedger>
                {
                    new GeneralLedger
                    {
                        Transdate = dto.PaymentDate,
                        Source = "PAYMENT",
                        Debits = dto.Amount,
                        Credits = 0,
                        AccBal = expenseCurrentBal + dto.Amount,
                        Description = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
                        Glname = expenseAccount.Glaccname,
                        CompanyCode = companyCode,
                        AuditDateTime = DateTime.Now
                    },
                    new GeneralLedger
                    {
                        Transdate = dto.PaymentDate,
                        Source = "PAYMENT",
                        Debits = 0,
                        Credits = dto.Amount,
                        AccBal = cashCurrentBal - dto.Amount,
                        Description = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
                        Glname = cashAccount.Glaccname,
                        CompanyCode = companyCode,
                        AuditDateTime = DateTime.Now
                    }
                };

                _context.GeneralLedgers.AddRange(glEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"General ledger entries created");

                // ============================================================
                // 6. Record in Transactions table
                // ============================================================
                var transactionRecord = new Transaction
                {
                    TransactionNo = transactionNo,
                    Amount = dto.Amount,
                    TransDate = dto.PaymentDate,
                    AuditId = UserName,
                    AuditTime = DateTime.Now,
                    TransDescription = $"{paymentType.Name} payment to {employee.Names ?? employee.Names} - Voucher: {voucherNo}" +
                                       (b2cPaymentSuccess ? $" - B2C Ref: {b2cReference}" : ""),
                    Status = b2cPaymentSuccess ? "COMPLETED" : "PENDING_B2C",
                    CompanyCode = companyCode,
                    Channel = b2cPaymentSuccess ? "B2C" : "EMPLOYEE_PAYMENT"
                };

                _context.Transactions.Add(transactionRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Transaction record created: {transactionNo}");

                // ============================================================
                // 7. Record Transaction Detail
                // ============================================================
                var transactionDetail = new TransactionDetail
                {
                    CompanyCode = companyCode,
                    TransactionId = transactionNo,
                    TransactionCode = voucherNo,
                    ResultCode = b2cPaymentSuccess ? 0 : 1,
                    ResultMessage = b2cPaymentSuccess ? "Payment processed successfully" : $"Payment processed - B2C: {b2cResponseMessage}",
                    Amount = dto.Amount,
                    Status = b2cPaymentSuccess ? "SUCCESS" : "PENDING",
                    MemberNo = employee.IdNo ?? employee.Id.ToString(),
                    ConversationId = b2cConversationId,
                    OriginatorConversationId = b2cConversationId,
                    Phone = employeePhone,
                    CreatedAt = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                _context.Transaction_Detail.Add(transactionDetail);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Transaction detail created");

                // ============================================================
                // 8. Blockchain recording
                // ============================================================
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        Action = "EMPLOYEE_PAYMENT",
                        VoucherNo = voucherNo,
                        TransactionNo = transactionNo,
                        EmployeeId = employee.Id,
                        EmployeeIdNo = employee.IdNo,
                        EmployeeName = employee.Names ?? employee.Names,
                        Amount = dto.Amount,
                        PaymentType = new { paymentType.Id, paymentType.Code, paymentType.Name },
                        PaymentDate = dto.PaymentDate,
                        PaymentMethod = dto.PaymentMethod,
                        ExpenseAccount = new { AccountNo = expenseAccount.AccNo, AccountName = expenseAccount.Glaccname },
                        CashAccount = new { AccountNo = cashAccount.AccNo, AccountName = cashAccount.Glaccname },
                        B2CPaymentSent = b2cPaymentSuccess,
                        B2CReference = b2cReference,
                        B2CConversationId = b2cConversationId,
                        CreatedBy = UserName,
                        CreatedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "EMPLOYEE_PAYMENT_PROCESS",
                        null,
                        companyCode,
                        dto.Amount,
                        employee.IdNo,
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;

                        // Update all entries with BlockchainTxId
                        journal.BlockchainTxId = blockchainTxId;
                        debitEntry.BlockchainTxId = blockchainTxId;
                        creditEntry.BlockchainTxId = blockchainTxId;
                        glTransactionDebit.BlockchainTxId = blockchainTxId;
                        glTransactionCredit.BlockchainTxId = blockchainTxId;
                        transactionRecord.BlockchainTxId = blockchainTxId;
                        transactionDetail.BlockchainTxId = blockchainTxId;

                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction recorded: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for payment - continuing without blockchain");
                }

                // ============================================================
                // 9. Mark as posted
                // ============================================================
                journal.POSTED = true;
                journal.POSTEDDATE = DateTime.Now;
                debitEntry.Posted = true;
                debitEntry.PostedDate = DateTime.Now;
                creditEntry.Posted = true;
                creditEntry.PostedDate = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Payment completed successfully! Voucher: {voucherNo}");

                return new EmployeePaymentResponseDTO
                {
                    PaymentId = journal.JVID,
                    JournalVoucherNo = voucherNo,
                    EmployeeId = employee.Id,
                    EmployeeIdNo = employee.IdNo,
                    EmployeeName = employee.Names ?? employee.Names,
                    Amount = dto.Amount,
                    PaymentTypeId = paymentType.Id,
                    PaymentType = paymentType.Name,
                    PaymentTypeCode = paymentType.Code,
                    PaymentDate = dto.PaymentDate,
                    PaymentMethod = dto.PaymentMethod,
                    ExpenseAccountNo = expenseAccount.AccNo,
                    ExpenseAccountName = expenseAccount.Glaccname,
                    CashAccountNo = cashAccount.AccNo,
                    CashAccountName = cashAccount.Glaccname,
                    Status = b2cPaymentSuccess ? "COMPLETED" : "COMPLETED_B2C_FAILED",
                    BlockchainTxId = blockchainTxId,
                    B2CReference = b2cReference,
                    B2CPaymentSent = b2cPaymentSuccess,
                    CreatedAt = DateTime.Now,
                    CreatedBy = UserName
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing payment for employee {dto.EmployeeId}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        //public async Task<EmployeePaymentResponseDTO> ProcessPaymentAsync(EmployeePaymentDTO dto, string UserName, string companyCode)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        _logger.LogInformation($"Processing payment for employee: {dto.EmployeeName}, Amount: {dto.Amount}");

        //        // Get the payment type from database
        //        var paymentType = await _context.PaymentTypes
        //            .FirstOrDefaultAsync(p => p.Id == dto.PaymentTypeId && p.CompanyCode == companyCode && p.IsActive == true);

        //        if (paymentType == null)
        //        {
        //            throw new InvalidOperationException($"Payment type with ID {dto.PaymentTypeId} not found or inactive.");
        //        }

        //        // Validate employee exists
        //        var employee = await _context.Agents
        //            .FirstOrDefaultAsync(a => a.Id == dto.EmployeeId && a.CompanyCode == companyCode);

        //        if (employee == null)
        //        {
        //            throw new InvalidOperationException($"Employee with ID {dto.EmployeeId} not found.");
        //        }

        //        // Use payment type's default expense account if not specified
        //        if (string.IsNullOrEmpty(dto.ExpenseAccountNo) && !string.IsNullOrEmpty(paymentType.DefaultExpenseAccountNo))
        //        {
        //            dto.ExpenseAccountNo = paymentType.DefaultExpenseAccountNo;
        //        }

        //        // Validate GL accounts
        //        var expenseAccount = await _context.GlSetup
        //            .FirstOrDefaultAsync(g => g.AccNo == dto.ExpenseAccountNo && g.CompanyCode == companyCode);

        //        if (expenseAccount == null)
        //        {
        //            throw new InvalidOperationException($"Expense account {dto.ExpenseAccountNo} not found.");
        //        }

        //        var cashAccount = await _context.GlSetup
        //            .FirstOrDefaultAsync(g => g.AccNo == dto.CashAccountNo && g.CompanyCode == companyCode);

        //        if (cashAccount == null)
        //        {
        //            throw new InvalidOperationException($"Cash/Bank account {dto.CashAccountNo} not found.");
        //        }

        //        // Generate Voucher Number
        //        string voucherNo = await GenerateVoucherNumberAsync(companyCode);
        //        string transactionNo = GenerateTransactionNumber();

        //        // ============================================================
        //        // CHECK IF B2C PAYMENT SHOULD BE SENT
        //        // ============================================================
        //        bool sendB2CPayment = dto.SendB2CPayment ?? false;
        //        string b2cReference = null;
        //        string b2cConversationId = null;
        //        bool b2cPaymentSuccess = false;
        //        string b2cResponseMessage = "";

        //        // Get employee phone number for B2C
        //        string employeePhone = employee.PhoneNo ?? employee.MobileNo;
        //        if (!string.IsNullOrEmpty(employeePhone))
        //        {
        //            employeePhone = employeePhone.Replace("+", "").Replace(" ", "").Replace("-", "");
        //            if (employeePhone.StartsWith("0"))
        //                employeePhone = "254" + employeePhone.Substring(1);
        //            if (!employeePhone.StartsWith("254") && employeePhone.Length == 9)
        //                employeePhone = "254" + employeePhone;
        //        }

        //        // ============================================================
        //        // SEND B2C PAYMENT IF ENABLED
        //        // ============================================================
        //        if (sendB2CPayment && !string.IsNullOrEmpty(employeePhone) && dto.Amount > 0)
        //        {
        //            try
        //            {
        //                _logger.LogInformation($"Sending B2C payment of {dto.Amount:C} to employee {employee.Names} ({employeePhone})");

        //                var b2cRequest = new B2CPaymentRequest
        //                {
        //                    CompanyCode = companyCode,
        //                    Amount = dto.Amount,
        //                    PhoneNumber = employeePhone,
        //                    Reference = $"EMP-{employee.IdNo ?? employee.Id.ToString()}",
        //                    Remarks = $"{paymentType.Name} payment to {employee.Names ?? employee.Names} - Voucher: {voucherNo}",
        //                    SourceModule = "EMPLOYEE_PAYMENT",
        //                    SourceReference = employee.IdNo ?? employee.Id.ToString(),
        //                    RecipientName = employee.Names ?? employee.Names,
        //                    CommandID = "BusinessPayment",
        //                    CreatedBy = UserName,
        //                    ApiKey = _configuration["MpesaApi:DefaultApiKey"]
        //                };

        //                var b2cResponse = await _mpesaApiService.SendB2CPaymentAsync(b2cRequest);

        //                if (b2cResponse.Success)
        //                {
        //                    b2cPaymentSuccess = true;
        //                    b2cReference = b2cResponse.TransactionReference;
        //                    b2cConversationId = b2cResponse.OriginatorConversationID;
        //                    b2cResponseMessage = "B2C payment sent successfully";
        //                    _logger.LogInformation($"B2C payment sent successfully. Reference: {b2cReference}");
        //                }
        //                else
        //                {
        //                    b2cPaymentSuccess = false;
        //                    b2cResponseMessage = b2cResponse.ResponseDescription;
        //                    _logger.LogWarning($"B2C payment failed: {b2cResponse.ResponseDescription}");
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, $"B2C payment error: {ex.Message}");
        //                b2cPaymentSuccess = false;
        //                b2cResponseMessage = $"Error: {ex.Message}";
        //            }
        //        }


        //        // ============================================================
        //        // 1. Create Journal Entry (Header)
        //        // ============================================================
        //        var journal = new Journal
        //        {
        //            VNO = voucherNo,
        //            ACCNO = expenseAccount.AccNo,
        //            NAME = employee.Names ?? employee.Names ?? "Unknown",
        //            NARATION = $"{paymentType.Name} payment to {employee.Names ?? employee.Names}",
        //            MEMBERNO = employee.IdNo ?? employee.Id.ToString(),
        //            SHARETYPE = paymentType.Name,
        //            Loanno = "0",
        //            AMOUNT = dto.Amount,
        //            TRANSTYPE = "PYM",
        //            AUDITID = UserName,
        //            TRANSDATE = dto.PaymentDate,
        //            AUDITDATE = DateTime.Now,
        //            POSTED = false,
        //            POSTEDDATE = DateTime.Now,
        //            Transactionno = transactionNo,
        //            CompanyCode = companyCode,
        //            BlockchainTxId = null
        //        };

        //        _context.Journals.Add(journal);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"Journal entry created with ID: {journal.JVID}, Voucher: {voucherNo}");

        //        // ============================================================
        //        // 2. Create Journal Listing entries (Debit & Credit)
        //        // ============================================================
        //        var debitEntry = new JournalsListing
        //        {
        //            VoucherNo = voucherNo,
        //            AccountNo = expenseAccount.AccNo,
        //            AccountName = expenseAccount.Glaccname,
        //            Narration = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names}",
        //            MemberNo = employee.IdNo ?? employee.Id.ToString(),
        //            ShareType = paymentType.Name,
        //            LoanNo = "0",
        //            AmountDr = dto.Amount,
        //            AmountCr = 0,
        //            Amount = dto.Amount,
        //            TransType = "DR",
        //            AuditId = UserName,
        //            TransDate = dto.PaymentDate,
        //            AuditDate = DateTime.Now,
        //            Posted = false,
        //            PostedDate = DateTime.Now,
        //            TransactionNo = transactionNo,
        //            CompanyCode = companyCode,
        //            BlockchainTxId = null
        //        };

        //        var creditEntry = new JournalsListing
        //        {
        //            VoucherNo = voucherNo,
        //            AccountNo = cashAccount.AccNo,
        //            AccountName = cashAccount.Glaccname,
        //            Narration = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names}",
        //            MemberNo = employee.IdNo ?? employee.Id.ToString(),
        //            ShareType = paymentType.Name,
        //            LoanNo = "0",
        //            AmountDr = 0,
        //            AmountCr = dto.Amount,
        //            Amount = dto.Amount,
        //            TransType = "CR",
        //            AuditId = UserName,
        //            TransDate = dto.PaymentDate,
        //            AuditDate = DateTime.Now,
        //            Posted = false,
        //            PostedDate = DateTime.Now,
        //            TransactionNo = transactionNo,
        //            CompanyCode = companyCode,
        //            BlockchainTxId = null
        //        };

        //        _context.JournalsListings.AddRange(debitEntry, creditEntry);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"Journal listing entries created for voucher: {voucherNo}");

        //        // ============================================================
        //        // 3. Create GL Transaction Entries (Gltransaction)
        //        // ============================================================
        //        // Debit GL Transaction - Expense Account
        //        var glTransactionDebit = new Gltransaction
        //        {
        //            TransDate = dto.PaymentDate,
        //            Amount = dto.Amount,
        //            DrAccNo = expenseAccount.AccNo,  // Debit the expense account
        //            CrAccNo = cashAccount.AccNo,     // Credit the cash account
        //            Temp = "PAYMENT",
        //            DocumentNo = voucherNo,
        //            Source = "EmployeePayment",
        //            CompanyCode = companyCode,
        //            TransDescript = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
        //            AuditTime = DateTime.Now,
        //            AuditId = UserName,
        //            Cash = 1,
        //            DocPosted = 0,
        //            ChequeNo = dto.PaymentMethod == "Cheque" ? $"CHQ-{DateTime.Now:yyyyMMdd}" : null,
        //            Dregard = false,
        //            Recon = false,
        //            TransactionNo = transactionNo,
        //            Module = "AP",
        //            ReconId = 0,
        //            AuditDateTime = DateTime.Now,
        //            BlockchainTxId = null
        //        };

        //        // Credit GL Transaction - Cash Account
        //        var glTransactionCredit = new Gltransaction
        //        {
        //            TransDate = dto.PaymentDate,
        //            Amount = dto.Amount,
        //            DrAccNo = expenseAccount.AccNo,  // Debit the expense account
        //            CrAccNo = cashAccount.AccNo,     // Credit the cash account
        //            Temp = "PAYMENT",
        //            DocumentNo = voucherNo,
        //            Source = "EmployeePayment",
        //            CompanyCode = companyCode,
        //            TransDescript = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
        //            AuditTime = DateTime.Now,
        //            AuditId = UserName,
        //            Cash = 1,
        //            DocPosted = 0,
        //            ChequeNo = dto.PaymentMethod == "Cheque" ? $"CHQ-{DateTime.Now:yyyyMMdd}" : null,
        //            Dregard = false,
        //            Recon = false,
        //            TransactionNo = transactionNo,
        //            Module = "AP",
        //            ReconId = 0,
        //            AuditDateTime = DateTime.Now,
        //            BlockchainTxId = null
        //        };

        //        _context.Gltransactions.AddRange(glTransactionDebit, glTransactionCredit);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"GL Transaction entries created for voucher: {voucherNo}");

        //        // ============================================================
        //        // 4. Update General Ledger balances
        //        // ============================================================
        //        await UpdateGLBalanceAsync(expenseAccount.AccNo, dto.Amount, "DEBIT", companyCode);
        //        await UpdateGLBalanceAsync(cashAccount.AccNo, dto.Amount, "CREDIT", companyCode);

        //        // ============================================================
        //        // 5. Create GeneralLedger entries
        //        // ============================================================
        //        var expenseCurrentBal = expenseAccount.Bal ?? 0;
        //        var cashCurrentBal = cashAccount.Bal ?? 0;

        //        var glEntries = new List<GeneralLedger>
        //{
        //    new GeneralLedger
        //    {
        //        Transdate = dto.PaymentDate,
        //        Source = "PAYMENT",
        //        Debits = dto.Amount,
        //        Credits = 0,
        //        AccBal = expenseCurrentBal + dto.Amount,
        //        Description = $"DR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
        //        Glname = expenseAccount.Glaccname,
        //        CompanyCode = companyCode,
        //        AuditDateTime = DateTime.Now
        //    },
        //    new GeneralLedger
        //    {
        //        Transdate = dto.PaymentDate,
        //        Source = "PAYMENT",
        //        Debits = 0,
        //        Credits = dto.Amount,
        //        AccBal = cashCurrentBal - dto.Amount,
        //        Description = $"CR - {paymentType.Name} payment to {employee.Names ?? employee.Names} (Voucher: {voucherNo})",
        //        Glname = cashAccount.Glaccname,
        //        CompanyCode = companyCode,
        //        AuditDateTime = DateTime.Now
        //    }
        //};

        //        _context.GeneralLedgers.AddRange(glEntries);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"General ledger entries created");

        //        // ============================================================
        //        // 6. Record in Transactions table
        //        // ============================================================
        //        var transactionRecord = new Transaction
        //        {
        //            TransactionNo = transactionNo,
        //            Amount = dto.Amount,
        //            TransDate = dto.PaymentDate,
        //            AuditId = UserName,
        //            AuditTime = DateTime.Now,
        //            TransDescription = $"{paymentType.Name} payment to {employee.Names ?? employee.Names} - Voucher: {voucherNo}",
        //            Status = "COMPLETED",
        //            CompanyCode = companyCode,
        //            Channel = "EMPLOYEE_PAYMENT"
        //        };

        //        _context.Transactions.Add(transactionRecord);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"Transaction record created: {transactionNo}");

        //        // ============================================================
        //        // 7. Record Transaction Detail
        //        // ============================================================
        //        var transactionDetail = new TransactionDetail
        //        {
        //            CompanyCode = companyCode,
        //            TransactionId = transactionNo,
        //            TransactionCode = voucherNo,
        //            ResultCode = 0,
        //            ResultMessage = "Payment processed successfully",
        //            Amount = dto.Amount,
        //            Status = "SUCCESS",
        //            MemberNo = employee.IdNo ?? employee.Id.ToString(),
        //            CreatedAt = DateTime.Now,
        //            AuditDateTime = DateTime.Now
        //        };

        //        _context.Transaction_Detail.Add(transactionDetail);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation($"Transaction detail created");

        //        // ============================================================
        //        // 8. Blockchain recording
        //        // ============================================================
        //        string blockchainTxId = null;
        //        try
        //        {
        //            var blockchainData = new
        //            {
        //                Action = "EMPLOYEE_PAYMENT",
        //                VoucherNo = voucherNo,
        //                TransactionNo = transactionNo,
        //                EmployeeId = employee.Id,
        //                EmployeeIdNo = employee.IdNo,
        //                EmployeeName = employee.Names ?? employee.Names,
        //                Amount = dto.Amount,
        //                PaymentType = new { paymentType.Id, paymentType.Code, paymentType.Name },
        //                PaymentDate = dto.PaymentDate,
        //                PaymentMethod = dto.PaymentMethod,
        //                ExpenseAccount = new { AccountNo = expenseAccount.AccNo, AccountName = expenseAccount.Glaccname },
        //                CashAccount = new { AccountNo = cashAccount.AccNo, AccountName = cashAccount.Glaccname },
        //                CreatedBy = UserName,
        //                CreatedAt = DateTime.Now
        //            };

        //            var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
        //                "EMPLOYEE_PAYMENT_PROCESS",
        //                null,
        //                companyCode,
        //                dto.Amount,
        //                employee.IdNo,
        //                blockchainData);

        //            if (blockchainTx != null)
        //            {
        //                blockchainTxId = blockchainTx.TransactionId;

        //                // Update all entries with BlockchainTxId
        //                journal.BlockchainTxId = blockchainTxId;
        //                debitEntry.BlockchainTxId = blockchainTxId;
        //                creditEntry.BlockchainTxId = blockchainTxId;
        //                glTransactionDebit.BlockchainTxId = blockchainTxId;
        //                glTransactionCredit.BlockchainTxId = blockchainTxId;
        //                transactionRecord.BlockchainTxId = blockchainTxId;
        //                transactionDetail.BlockchainTxId = blockchainTxId;

        //                await _context.SaveChangesAsync();
        //                _logger.LogInformation($"Blockchain transaction recorded: {blockchainTxId}");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Failed to record blockchain transaction for payment - continuing without blockchain");
        //        }

        //        // ============================================================
        //        // 9. Mark as posted
        //        // ============================================================
        //        journal.POSTED = true;
        //        journal.POSTEDDATE = DateTime.Now;
        //        debitEntry.Posted = true;
        //        debitEntry.PostedDate = DateTime.Now;
        //        creditEntry.Posted = true;
        //        creditEntry.PostedDate = DateTime.Now;

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        _logger.LogInformation($"Payment completed successfully! Voucher: {voucherNo}");

        //        return new EmployeePaymentResponseDTO
        //        {
        //            PaymentId = journal.JVID,
        //            JournalVoucherNo = voucherNo,
        //            EmployeeId = employee.Id,
        //            EmployeeIdNo = employee.IdNo,
        //            EmployeeName = employee.Names ?? employee.Names,
        //            Amount = dto.Amount,
        //            PaymentTypeId = paymentType.Id,
        //            PaymentType = paymentType.Name,
        //            PaymentTypeCode = paymentType.Code,
        //            PaymentDate = dto.PaymentDate,
        //            PaymentMethod = dto.PaymentMethod,
        //            ExpenseAccountNo = expenseAccount.AccNo,
        //            ExpenseAccountName = expenseAccount.Glaccname,
        //            CashAccountNo = cashAccount.AccNo,
        //            CashAccountName = cashAccount.Glaccname,
        //            Status = "COMPLETED",
        //            BlockchainTxId = blockchainTxId,
        //            CreatedAt = DateTime.Now,
        //            CreatedBy = UserName
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, $"Error processing payment for employee {dto.EmployeeId}: {ex.Message}");
        //        _logger.LogError($"Stack trace: {ex.StackTrace}");
        //        throw;
        //    }
        //}


        public async Task<EmployeePaymentResponseDTO> GetPaymentByIdAsync(long paymentId)
        {
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.JVID == paymentId);

            if (journal == null) return null;

            var debitEntry = await _context.JournalsListings
                .FirstOrDefaultAsync(jl => jl.VoucherNo == journal.VNO && jl.AmountDr > 0);

            var creditEntry = await _context.JournalsListings
                .FirstOrDefaultAsync(jl => jl.VoucherNo == journal.VNO && jl.AmountCr > 0);

            var employee = await _context.Agents
                .FirstOrDefaultAsync(a => a.IdNo == journal.MEMBERNO || a.Id.ToString() == journal.MEMBERNO);

            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Name == journal.SHARETYPE);

            return new EmployeePaymentResponseDTO
            {
                PaymentId = journal.JVID,
                JournalVoucherNo = journal.VNO,
                EmployeeId = employee?.Id ?? 0,
                EmployeeIdNo = employee?.IdNo,
                EmployeeName = journal.NAME,
                Amount = journal.AMOUNT ?? 0,
                PaymentTypeId = paymentType?.Id ?? 0,
                PaymentType = journal.SHARETYPE,
                PaymentTypeCode = paymentType?.Code,
                Description = journal.NARATION,
                PaymentDate = journal.TRANSDATE ?? DateTime.Now,
                PaymentMethod = "Bank/Cash",
                ExpenseAccountNo = debitEntry?.AccountNo,
                ExpenseAccountName = debitEntry?.AccountName,
                CashAccountNo = creditEntry?.AccountNo,
                CashAccountName = creditEntry?.AccountName,
                Status = journal.POSTED ? "POSTED" : "PENDING",
                BlockchainTxId = journal.BlockchainTxId,
                CreatedAt = journal.AUDITDATE,
                CreatedBy = journal.AUDITID
            };
        }

        public async Task<List<EmployeePaymentListDTO>> GetAllPaymentsAsync(string companyCode)
        {
            // Get all valid agent/employee member numbers
            var agentMemberNos = await _context.Agents
                .Where(a => a.CompanyCode == companyCode)
                .Select(a => a.IdNo)
                .ToListAsync();

            var payments = await _context.Journals
                .Where(j => j.CompanyCode == companyCode
                            && (j.TRANSTYPE == "PYMT" || j.TRANSTYPE == "PYM" || j.TRANSTYPE == "PAY")
                            && agentMemberNos.Contains(j.MEMBERNO))  // Only include journals where MEMBERNO is an actual agent
                .OrderByDescending(j => j.AUDITDATE)
                .Select(j => new EmployeePaymentListDTO
                {
                    Id = j.JVID,
                    VoucherNo = j.VNO,
                    EmployeeName = j.NAME,
                    EmployeeIdNo = j.MEMBERNO,
                    Amount = j.AMOUNT ?? 0,
                    PaymentType = j.SHARETYPE,
                    PaymentDate = j.TRANSDATE ?? DateTime.Now,
                    Status = j.POSTED ? "POSTED" : "PENDING",
                    BlockchainTxId = j.BlockchainTxId
                })
                .ToListAsync();

            // Get payment type codes for each payment
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode)
                .ToDictionaryAsync(p => p.Name, p => p.Code);

            foreach (var payment in payments)
            {
                if (paymentTypes.TryGetValue(payment.PaymentType, out var code))
                {
                    payment.PaymentTypeCode = code;
                }
            }

            return payments;
        }

        public async Task<List<EmployeePaymentListDTO>> GetPaymentsByEmployeeAsync(string employeeIdNo, string companyCode)
        {
            var payments = await _context.Journals
                .Where(j => j.CompanyCode == companyCode
                            && j.TRANSTYPE == "PYM"
                            && j.MEMBERNO == employeeIdNo)
                .OrderByDescending(j => j.AUDITDATE)
                .Select(j => new EmployeePaymentListDTO
                {
                    Id = j.JVID,
                    VoucherNo = j.VNO,
                    EmployeeName = j.NAME,
                    EmployeeIdNo = j.MEMBERNO,
                    Amount = j.AMOUNT ?? 0,
                    PaymentType = j.SHARETYPE,
                    PaymentDate = j.TRANSDATE ?? DateTime.Now,
                    Status = j.POSTED ? "POSTED" : "PENDING",
                    BlockchainTxId = j.BlockchainTxId
                })
                .ToListAsync();

            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode)
                .ToDictionaryAsync(p => p.Name, p => p.Code);

            foreach (var payment in payments)
            {
                if (paymentTypes.TryGetValue(payment.PaymentType, out var code))
                {
                    payment.PaymentTypeCode = code;
                }
            }

            return payments;
        }


        public async Task<PaymentSummaryDTO> GetPaymentSummaryAsync(string companyCode, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Get all valid agent/employee member numbers
            var agentMemberNos = await _context.Agents
                .Where(a => a.CompanyCode == companyCode)
                .Select(a => a.IdNo)
                .ToListAsync();

            var query = _context.Journals
                .Where(j => j.CompanyCode == companyCode
                            && (j.TRANSTYPE == "PYMT" || j.TRANSTYPE == "PYM" || j.TRANSTYPE == "PAY")
                            && j.POSTED == true
                            && agentMemberNos.Contains(j.MEMBERNO));  // Only include journals where MEMBERNO is an actual agent

            if (fromDate.HasValue)
                query = query.Where(j => j.TRANSDATE >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(j => j.TRANSDATE <= toDate.Value);

            var payments = await query.ToListAsync();

            // Get all payment types for mapping
            var paymentTypes = await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode)
                .ToDictionaryAsync(p => p.Name, p => p.Code);

            // Group by SHARETYPE (which stores the payment type name)
            var paymentsByType = payments
                .GroupBy(p => p.SHARETYPE)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AMOUNT ?? 0));

            // Create a dictionary with payment type codes as keys
            var paymentsByTypeCode = new Dictionary<string, decimal>();
            foreach (var item in paymentsByType)
            {
                // Find the payment type code for this share type name
                if (paymentTypes.TryGetValue(item.Key, out var code))
                {
                    paymentsByTypeCode[code] = item.Value;
                }
                else
                {
                    // If no mapping found, use the name itself
                    paymentsByTypeCode[item.Key] = item.Value;
                }
            }

            var summary = new PaymentSummaryDTO
            {
                TotalPayments = payments.Sum(p => p.AMOUNT ?? 0),
                PaymentCount = payments.Count,
                PaymentsByType = paymentsByType,
                PaymentsByTypeCode = paymentsByTypeCode
            };

            return summary;
        }


        public async Task<bool> ReversePaymentAsync(long paymentId, string UserName, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var journal = await _context.Journals
                    .FirstOrDefaultAsync(j => j.JVID == paymentId);

                if (journal == null)
                    throw new InvalidOperationException("Payment not found.");

                if (journal.POSTED == false)
                    throw new InvalidOperationException("Cannot reverse an unposted payment.");

                // ============================================================
                // CHECK FOR B2C REVERSAL
                // ============================================================
                string b2cReference = journal.VNO;
                string b2cReversalResult = "Not attempted";

                // If there was a B2C payment, reverse it
                if (!string.IsNullOrEmpty(b2cReference))
                {
                    try
                    {
                        _logger.LogInformation($"Reversing B2C payment for reference: {b2cReference}");
                        var reversalResponse = await _mpesaApiService.ReverseB2CPaymentAsync(
                            journal.CompanyCode,
                            b2cReference,
                            reason,
                            UserName
                        );

                        if (reversalResponse.Success)
                        {
                            b2cReversalResult = "B2C reversal successful";
                            _logger.LogInformation($"B2C reversal successful for {b2cReference}");
                        }
                        else
                        {
                            b2cReversalResult = $"B2C reversal failed: {reversalResponse.ResponseDescription}";
                            _logger.LogWarning($"B2C reversal failed: {reversalResponse.ResponseDescription}");
                        }
                    }
                    catch (Exception ex)
                    {
                        b2cReversalResult = $"B2C reversal error: {ex.Message}";
                        _logger.LogError(ex, $"Error reversing B2C payment for {b2cReference}");
                        // Continue with reversal - don't block the GL reversal
                    }
                }

                var reverseVoucherNo = await GenerateVoucherNumberAsync(journal.CompanyCode);
                var reverseTransactionNo = GenerateTransactionNumber();

                var debitEntries = await _context.JournalsListings
                    .Where(jl => jl.VoucherNo == journal.VNO && jl.AmountDr > 0)
                    .ToListAsync();

                var creditEntries = await _context.JournalsListings
                    .Where(jl => jl.VoucherNo == journal.VNO && jl.AmountCr > 0)
                    .ToListAsync();

                // Create reversal journal header
                var reversalJournal = new Journal
                {
                    VNO = reverseVoucherNo,
                    ACCNO = journal.ACCNO,
                    NAME = journal.NAME,
                    NARATION = $"REVERSAL: {reason} - Original Voucher: {journal.VNO} - {b2cReversalResult}",
                    MEMBERNO = journal.MEMBERNO,
                    SHARETYPE = $"REVERSAL_{journal.SHARETYPE}",
                    Loanno = journal.Loanno,
                    AMOUNT = journal.AMOUNT,
                    TRANSTYPE = "REV",
                    AUDITID = UserName,
                    TRANSDATE = DateTime.Now,
                    AUDITDATE = DateTime.Now,
                    POSTED = false,
                    POSTEDDATE = DateTime.Now,
                    Transactionno = reverseTransactionNo,
                    CompanyCode = journal.CompanyCode,
                    BlockchainTxId = null,
                };

                _context.Journals.Add(reversalJournal);
                await _context.SaveChangesAsync();

                // Create reversal listing entries
                foreach (var debit in debitEntries)
                {
                    var reverseEntry = new JournalsListing
                    {
                        VoucherNo = reverseVoucherNo,
                        AccountNo = debit.AccountNo,
                        AccountName = debit.AccountName,
                        Narration = $"REVERSAL: {reason} - {debit.Narration} - {b2cReversalResult}",
                        MemberNo = debit.MemberNo,
                        ShareType = $"REVERSAL_{debit.ShareType}",
                        LoanNo = debit.LoanNo,
                        AmountDr = debit.AmountCr ?? 0,
                        AmountCr = debit.AmountDr ?? 0,
                        Amount = debit.Amount,
                        TransType = "REV",
                        AuditId = UserName,
                        TransDate = DateTime.Now,
                        AuditDate = DateTime.Now,
                        Posted = false,
                        PostedDate = DateTime.Now,
                        TransactionNo = reverseTransactionNo,
                        CompanyCode = journal.CompanyCode,
                        BlockchainTxId = null
                    };
                    _context.JournalsListings.Add(reverseEntry);
                }

                foreach (var credit in creditEntries)
                {
                    var reverseEntry = new JournalsListing
                    {
                        VoucherNo = reverseVoucherNo,
                        AccountNo = credit.AccountNo,
                        AccountName = credit.AccountName,
                        Narration = $"REVERSAL: {reason} - {credit.Narration} - {b2cReversalResult}",
                        MemberNo = credit.MemberNo,
                        ShareType = $"REVERSAL_{credit.ShareType}",
                        LoanNo = credit.LoanNo,
                        AmountDr = credit.AmountCr ?? 0,
                        AmountCr = credit.AmountDr ?? 0,
                        Amount = credit.Amount,
                        TransType = "REV",
                        AuditId = UserName,
                        TransDate = DateTime.Now,
                        AuditDate = DateTime.Now,
                        Posted = false,
                        PostedDate = DateTime.Now,
                        TransactionNo = reverseTransactionNo,
                        CompanyCode = journal.CompanyCode,
                        BlockchainTxId = null
                    };
                    _context.JournalsListings.Add(reverseEntry);
                }

                await _context.SaveChangesAsync();

                // Update GL balances
                foreach (var debit in debitEntries)
                {
                    await UpdateGLBalanceAsync(debit.AccountNo, debit.AmountDr ?? 0, "CREDIT", journal.CompanyCode);
                }

                foreach (var credit in creditEntries)
                {
                    await UpdateGLBalanceAsync(credit.AccountNo, credit.AmountCr ?? 0, "DEBIT", journal.CompanyCode);
                }

                journal.POSTED = false;
                journal.NARATION = $"{journal.NARATION} [REVERSED: {reason} on {DateTime.Now:yyyy-MM-dd HH:mm}] - B2C: {b2cReversalResult}";

                reversalJournal.POSTED = true;
                reversalJournal.POSTEDDATE = DateTime.Now;

                var reversalEntries = await _context.JournalsListings
                    .Where(jl => jl.VoucherNo == reverseVoucherNo)
                    .ToListAsync();

                foreach (var entry in reversalEntries)
                {
                    entry.Posted = true;
                    entry.PostedDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // ============================================================
                // Record blockchain reversal transaction
                // ============================================================
                try
                {
                    var blockchainData = new
                    {
                        Action = "PAYMENT_REVERSAL",
                        OriginalVoucherNo = journal.VNO,
                        ReversalVoucherNo = reverseVoucherNo,
                        Reason = reason,
                        Amount = journal.AMOUNT,
                        EmployeeName = journal.NAME,
                        B2CReversalResult = b2cReversalResult,
                        ReversedBy = UserName,
                        ReversedAt = DateTime.Now
                    };

                    await _blockchainService.CreateAndAddTransactionAsync(
                        "PAYMENT_REVERSAL",
                        null,
                        journal.CompanyCode,
                        journal.AMOUNT ?? 0,
                        journal.MEMBERNO,
                        blockchainData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain reversal transaction");
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error reversing payment {paymentId}");
                throw;
            }
        }

        //public async Task<bool> ReversePaymentAsync(long paymentId, string UserName, string reason)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        var journal = await _context.Journals
        //            .FirstOrDefaultAsync(j => j.JVID == paymentId);

        //        if (journal == null)
        //            throw new InvalidOperationException("Payment not found.");

        //        if (journal.POSTED == false)
        //            throw new InvalidOperationException("Cannot reverse an unposted payment.");

        //        var reverseVoucherNo = await GenerateVoucherNumberAsync(journal.CompanyCode);
        //        var reverseTransactionNo = GenerateTransactionNumber();

        //        var debitEntries = await _context.JournalsListings
        //            .Where(jl => jl.VoucherNo == journal.VNO && jl.AmountDr > 0)
        //            .ToListAsync();

        //        var creditEntries = await _context.JournalsListings
        //            .Where(jl => jl.VoucherNo == journal.VNO && jl.AmountCr > 0)
        //            .ToListAsync();

        //        // Create reversal journal header
        //        var reversalJournal = new Journal
        //        {
        //            VNO = reverseVoucherNo,
        //            ACCNO = journal.ACCNO,
        //            NAME = journal.NAME,
        //            NARATION = $"REVERSAL: {reason} - Original Voucher: {journal.VNO}",
        //            MEMBERNO = journal.MEMBERNO,
        //            SHARETYPE = $"REVERSAL_{journal.SHARETYPE}",
        //            Loanno = journal.Loanno,
        //            AMOUNT = journal.AMOUNT,
        //            TRANSTYPE = "REV",
        //            AUDITID = UserName,
        //            TRANSDATE = DateTime.Now,
        //            AUDITDATE = DateTime.Now,
        //            POSTED = false,
        //            POSTEDDATE = DateTime.Now,
        //            Transactionno = reverseTransactionNo,
        //            CompanyCode = journal.CompanyCode,
        //            BlockchainTxId = null
        //        };

        //        _context.Journals.Add(reversalJournal);
        //        await _context.SaveChangesAsync();

        //        // Create reversal listing entries
        //        foreach (var debit in debitEntries)
        //        {
        //            var reverseEntry = new JournalsListing
        //            {
        //                VoucherNo = reverseVoucherNo,
        //                AccountNo = debit.AccountNo,
        //                AccountName = debit.AccountName,
        //                Narration = $"REVERSAL: {reason} - {debit.Narration}",
        //                MemberNo = debit.MemberNo,
        //                ShareType = $"REVERSAL_{debit.ShareType}",
        //                LoanNo = debit.LoanNo,
        //                AmountDr = debit.AmountCr ?? 0,
        //                AmountCr = debit.AmountDr ?? 0,
        //                Amount = debit.Amount,
        //                TransType = "REV",
        //                AuditId = UserName,
        //                TransDate = DateTime.Now,
        //                AuditDate = DateTime.Now,
        //                Posted = false,
        //                PostedDate = DateTime.Now,
        //                TransactionNo = reverseTransactionNo,
        //                CompanyCode = journal.CompanyCode,
        //                BlockchainTxId = null
        //            };
        //            _context.JournalsListings.Add(reverseEntry);
        //        }

        //        foreach (var credit in creditEntries)
        //        {
        //            var reverseEntry = new JournalsListing
        //            {
        //                VoucherNo = reverseVoucherNo,
        //                AccountNo = credit.AccountNo,
        //                AccountName = credit.AccountName,
        //                Narration = $"REVERSAL: {reason} - {credit.Narration}",
        //                MemberNo = credit.MemberNo,
        //                ShareType = $"REVERSAL_{credit.ShareType}",
        //                LoanNo = credit.LoanNo,
        //                AmountDr = credit.AmountCr ?? 0,
        //                AmountCr = credit.AmountDr ?? 0,
        //                Amount = credit.Amount,
        //                TransType = "REV",
        //                AuditId = UserName,
        //                TransDate = DateTime.Now,
        //                AuditDate = DateTime.Now,
        //                Posted = false,
        //                PostedDate = DateTime.Now,
        //                TransactionNo = reverseTransactionNo,
        //                CompanyCode = journal.CompanyCode,
        //                BlockchainTxId = null
        //            };
        //            _context.JournalsListings.Add(reverseEntry);
        //        }

        //        await _context.SaveChangesAsync();

        //        // Update GL balances
        //        foreach (var debit in debitEntries)
        //        {
        //            await UpdateGLBalanceAsync(debit.AccountNo, debit.AmountDr ?? 0, "CREDIT", journal.CompanyCode);
        //        }

        //        foreach (var credit in creditEntries)
        //        {
        //            await UpdateGLBalanceAsync(credit.AccountNo, credit.AmountCr ?? 0, "DEBIT", journal.CompanyCode);
        //        }

        //        journal.POSTED = false;
        //        journal.NARATION = $"{journal.NARATION} [REVERSED: {reason} on {DateTime.Now:yyyy-MM-dd HH:mm}]";

        //        reversalJournal.POSTED = true;
        //        reversalJournal.POSTEDDATE = DateTime.Now;

        //        var reversalEntries = await _context.JournalsListings
        //            .Where(jl => jl.VoucherNo == reverseVoucherNo)
        //            .ToListAsync();

        //        foreach (var entry in reversalEntries)
        //        {
        //            entry.Posted = true;
        //            entry.PostedDate = DateTime.Now;
        //        }

        //        await _context.SaveChangesAsync();

        //        try
        //        {
        //            var blockchainData = new
        //            {
        //                Action = "PAYMENT_REVERSAL",
        //                OriginalVoucherNo = journal.VNO,
        //                ReversalVoucherNo = reverseVoucherNo,
        //                Reason = reason,
        //                Amount = journal.AMOUNT,
        //                EmployeeName = journal.NAME,
        //                ReversedBy = UserName,
        //                ReversedAt = DateTime.Now
        //            };

        //            await _blockchainService.CreateAndAddTransactionAsync(
        //                "PAYMENT_REVERSAL",
        //                null,
        //                journal.CompanyCode,
        //                journal.AMOUNT ?? 0,
        //                journal.MEMBERNO,
        //                blockchainData);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Failed to record blockchain reversal transaction");
        //        }

        //        await transaction.CommitAsync();
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, $"Error reversing payment {paymentId}");
        //        throw;
        //    }
        //}

        public async Task<decimal> GetEmployeeBalanceAsync(string employeeIdNo, string companyCode)
        {
            var totalPaid = await _context.Journals
                .Where(j => j.CompanyCode == companyCode
                            && j.TRANSTYPE == "PYM"
                            && j.MEMBERNO == employeeIdNo
                            && j.POSTED == true)
                .SumAsync(j => j.AMOUNT ?? 0);

            var totalReversed = await _context.Journals
                .Where(j => j.CompanyCode == companyCode
                            && j.TRANSTYPE == "REV"
                            && j.MEMBERNO == employeeIdNo
                            && j.NARATION != null
                            && j.NARATION.Contains("REVERSAL")
                            && j.POSTED == true)
                .SumAsync(j => j.AMOUNT ?? 0);

            return totalPaid - totalReversed;
        }

        #region Helper Methods

        private async Task<string> GenerateVoucherNumberAsync(string companyCode)
        {
            var lastJournal = await _context.Journals
                .Where(j => j.CompanyCode == companyCode && j.VNO != null && j.VNO.StartsWith("PAY"))
                .OrderByDescending(j => j.JVID)
                .FirstOrDefaultAsync();

            string prefix = "PAY";
            int nextNumber = 1;

            if (lastJournal?.VNO != null && lastJournal.VNO.Length > prefix.Length)
            {
                string numberPart = lastJournal.VNO.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D8}";
        }

        private string GenerateTransactionNumber()
        {
            return $"TXN{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        private async Task UpdateGLBalanceAsync(string accountNo, decimal amount, string transactionType, string companyCode)
        {
            var glAccount = await _context.GlSetup
                .FirstOrDefaultAsync(g => g.AccNo == accountNo && g.CompanyCode == companyCode);

            if (glAccount != null)
            {
                if (transactionType == "DEBIT")
                {
                    // For debit transactions
                    if (glAccount.Normalbal == "DEBIT" || glAccount.AccCategory == "EXPENSE")
                    {
                        glAccount.Bal = (glAccount.Bal ?? 0) + amount;
                        glAccount.CurrentBal = (glAccount.CurrentBal ?? 0) + amount;
                    }
                    else
                    {
                        glAccount.Bal = (glAccount.Bal ?? 0) - amount;
                        glAccount.CurrentBal = (glAccount.CurrentBal ?? 0) - amount;
                    }
                }
                else // CREDIT
                {
                    // For credit transactions
                    if (glAccount.Normalbal == "DEBIT" || glAccount.AccCategory == "ASSET")
                    {
                        glAccount.Bal = (glAccount.Bal ?? 0) - amount;
                        glAccount.CurrentBal = (glAccount.CurrentBal ?? 0) - amount;
                    }
                    else
                    {
                        glAccount.Bal = (glAccount.Bal ?? 0) + amount;
                        glAccount.CurrentBal = (glAccount.CurrentBal ?? 0) + amount;
                    }
                }

                glAccount.AuditDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        #endregion
    }
}