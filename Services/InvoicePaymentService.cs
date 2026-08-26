using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface IInvoicePaymentService
    {
        // Payment CRUD
        Task<InvoicePaymentResponseDTO> CreatePaymentAsync(InvoicePaymentDTO dto, string createdBy);
        Task<InvoicePaymentResponseDTO> UpdatePaymentAsync(long id, InvoicePaymentDTO dto, string updatedBy);
        Task<bool> DeletePaymentAsync(long id, string deletedBy);
        Task<InvoicePaymentResponseDTO> GetPaymentByIdAsync(long id);
        Task<List<InvoicePaymentResponseDTO>> GetPaymentsByInvoiceAsync(string invoiceNo, string companyCode);
        Task<List<InvoicePaymentResponseDTO>> GetPaymentsBySupplierAsync(string supplierCode, string companyCode);
        Task<InvoicePaymentViewModel> GetPaymentDashboardAsync(string companyCode);
        Task<List<GlAccountDTO>> GetGlAccountsForDropdownAsync(string companyCode);
        Task<GlAccountDTO> GetGlAccountByCodeAsync(string glAccountNo, string companyCode);
        Task<InvoicePaymentResponseDTO> GetPaymentByReceiptNoAsync(string receiptNo, string companyCode);
    }

    public class InvoicePaymentService : IInvoicePaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoicePaymentService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly AuditTrailService _auditService;

        public InvoicePaymentService(
            ApplicationDbContext context,
            ILogger<InvoicePaymentService> logger,
            IBlockchainService blockchainService,
            AuditTrailService auditService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _auditService = auditService;
        }

        public async Task<InvoicePaymentResponseDTO> CreatePaymentAsync(InvoicePaymentDTO dto, string createdBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating payment for invoice: {dto.InvoiceNo}");

                // 1. Get and validate invoice
                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo && i.CompanyCode == dto.CompanyCode);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice '{dto.InvoiceNo}' not found.");
                }

                // 2. Get and validate supplier
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == dto.SupplierId && s.CompanyCode == dto.CompanyCode);

                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier not found.");
                }

                // 3. Validate payment amount
                if (dto.Amount > invoice.Balance)
                {
                    throw new InvalidOperationException($"Payment amount ({dto.Amount:C}) exceeds invoice balance ({invoice.Balance:C}).");
                }

                // 4. Validate Debit GL Account if provided
                if (!string.IsNullOrEmpty(dto.DebitAccno))
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.DebitAccno && g.CompanyCode == dto.CompanyCode && g.Status == true);

                    if (glAccount == null)
                    {
                        throw new InvalidOperationException($"Debit GL Account '{dto.DebitAccno}' not found or inactive.");
                    }

                    if (string.IsNullOrEmpty(dto.DebitAccName))
                    {
                        dto.DebitAccName = glAccount.Glaccname;
                    }
                }

                // 5. Set supplier GL account
                dto.SupplierAccno = supplier.GlAccountNo;
                dto.SupplierAccName = supplier.GlAccountName;

                // 6. Generate receipt number if not provided
                if (string.IsNullOrEmpty(dto.ReceiptNo))
                {
                    dto.ReceiptNo = $"RCPT-{DateTime.Now:yyyyMMddHHmmss}";
                }

                // 7. Create payment record
                var payment = new InvoicePayment
                {
                    CompanyCode = dto.CompanyCode,
                    Amount = dto.Amount,
                    OpeningBalance = invoice.Balance,
                    SupplierId = dto.SupplierId,
                    Particulars = dto.Particulars ?? $"Payment for invoice {dto.InvoiceNo}",
                    TransDate = dto.TransDate ?? DateTime.Now,
                    OpeningDate = dto.OpeningDate ?? DateTime.Now,
                    DueDate = dto.DueDate ?? invoice.DueDate,
                    ChequeNo = dto.ChequeNo,
                    InvoiceNo = dto.InvoiceNo,
                    Remarks = dto.Remarks,
                    Transtype = dto.Transtype ?? "Payment",
                    SupplierAccno = dto.SupplierAccno,
                    DebitAccno = dto.DebitAccno,
                    SupplierAccName = dto.SupplierAccName,
                    DebitAccName = dto.DebitAccName,
                    ReceiptNo = dto.ReceiptNo,
                    TransactionNo = $"PAY-{DateTime.Now:yyyyMMddHHmmss}",
                    AuditId = createdBy,
                    AuditTime = DateTime.Now
                };

                _context.InvoicePayments.Add(payment);
                await _context.SaveChangesAsync();

                // 8. Update invoice balance and status
                invoice.AmountPaid = (invoice.AmountPaid ?? 0) + dto.Amount;
                invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;

                // Update payment status
                if (invoice.Balance <= 0)
                {
                    invoice.PaymentStatus = "Paid";
                    invoice.Status = "Paid";
                }
                else
                {
                    invoice.PaymentStatus = "Partially Paid";
                    invoice.Status = "Partially Paid";
                }

                await _context.SaveChangesAsync();

                // 9. Update supplier current balance
                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) - dto.Amount;
                await _context.SaveChangesAsync();

                // 10. Create GL Transaction Entry
                var glTransaction = new Gltransaction
                {
                    TransDate = DateTime.Now,
                    Amount = dto.Amount,
                    DrAccNo = dto.DebitAccno ?? supplier.GlAccountNo, // Debit supplier account or specified debit account
                    CrAccNo = supplier.GlAccountNo ?? dto.DebitAccno, // Credit supplier account
                    Temp = "PAYMENT",
                    DocumentNo = dto.ReceiptNo,
                    Source = "InvoicePayment",
                    CompanyCode = dto.CompanyCode,
                    TransDescript = $"Payment for invoice {dto.InvoiceNo} - {supplier.SupplierName}",
                    AuditTime = DateTime.Now,
                    AuditId = createdBy,
                    Cash = 1,
                    DocPosted = 0,
                    ChequeNo = dto.ChequeNo,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = payment.TransactionNo,
                    Module = "AP",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now
                };

                _context.Gltransactions.Add(glTransaction);
                await _context.SaveChangesAsync();

                // 11. Create Journal Entry
                var journal = new Journal
                {
                    VNO = dto.ReceiptNo,
                    ACCNO = supplier.GlAccountNo,
                    NAME = supplier.SupplierName,
                    NARATION = $"Payment for invoice {dto.InvoiceNo} - {supplier.SupplierName}",
                    MEMBERNO = "N/A",
                    SHARETYPE = "N/A",
                    Loanno = "N/A",
                    AMOUNT = dto.Amount,
                    TRANSTYPE = "PAY",
                    AUDITID = createdBy,
                    TRANSDATE = DateTime.Now,
                    AUDITDATE = DateTime.Now,
                    POSTED = false,
                    POSTEDDATE = DateTime.Now,
                    Transactionno = payment.TransactionNo,
                    CompanyCode = dto.CompanyCode
                };

                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                // 12. Create Journals Listing Entry
                var journalsListing = new JournalsListing
                {
                    VoucherNo = dto.ReceiptNo,
                    AccountNo = supplier.GlAccountNo,
                    AccountName = supplier.SupplierName,
                    Narration = $"Payment for invoice {dto.InvoiceNo} - {supplier.SupplierName}",
                    MemberNo = "N/A",
                    ShareType = "N/A",
                    LoanNo = "N/A",
                    Amount = dto.Amount,
                    AmountDr = 0, 
                    AmountCr = dto.Amount,
                    TransType = "PAY",
                    AuditId = createdBy,
                    TransDate = DateTime.Now,
                    AuditDate = DateTime.Now,
                    Posted = false,
                    PostedDate = DateTime.Now,
                    TransactionNo = payment.TransactionNo,
                    CompanyCode = dto.CompanyCode
                };

                _context.JournalsListings.Add(journalsListing);
                await _context.SaveChangesAsync();

                // 13. Create Blockchain Data
                var blockchainData = new
                {
                    PaymentId = payment.Id,
                    InvoiceNo = payment.InvoiceNo,
                    SupplierId = payment.SupplierId,
                    SupplierName = supplier.SupplierName,
                    Amount = payment.Amount,
                    ReceiptNo = payment.ReceiptNo,
                    PaymentDate = payment.TransDate,
                    DebitAccno = payment.DebitAccno,
                    DebitAccName = payment.DebitAccName,
                    SupplierAccno = payment.SupplierAccno,
                    SupplierAccName = payment.SupplierAccName,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_PAYMENT",
                    MemberNo = null,
                    CompanyCode = dto.CompanyCode,
                    Amount = dto.Amount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = payment.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // 14. Update all BlockchainTxId fields
                var blockchainTxId = blockchainTx.TransactionId;

                // Update payment
                payment.BlockchainTxId = blockchainTxId;
                await _context.SaveChangesAsync();

                // Update invoice
                invoice.BlockchainTxId = blockchainTxId;
                await _context.SaveChangesAsync();

                // Update glTransaction
                glTransaction.BlockchainTxId = blockchainTxId;
                await _context.SaveChangesAsync();

                // Update journal
                journal.BlockchainTxId = blockchainTxId;
                await _context.SaveChangesAsync();

                // Update journalsListing
                journalsListing.BlockchainTxId = blockchainTxId;
                await _context.SaveChangesAsync();

                // 15. Save Audit Log
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: payment,
                    tableName: "InvoicePayments",
                    recordId: payment.Id.ToString(),
                    userId: createdBy,
                    userName: createdBy,
                    companyCode: dto.CompanyCode,
                    module: "PaymentManagement",
                    blockchainTxId: blockchainTxId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(payment);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating payment for invoice: {dto.InvoiceNo}");
                throw;
            }
        }

        public async Task<InvoicePaymentResponseDTO> UpdatePaymentAsync(long id, InvoicePaymentDTO dto, string updatedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var payment = await _context.InvoicePayments
                    .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == dto.CompanyCode);

                if (payment == null)
                {
                    throw new InvalidOperationException($"Payment with ID {id} not found");
                }

                // Get invoice
                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == payment.InvoiceNo && i.CompanyCode == dto.CompanyCode);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice '{payment.InvoiceNo}' not found.");
                }

                // Get supplier
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == payment.SupplierId && s.CompanyCode == dto.CompanyCode);

                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier not found.");
                }

                // Validate Debit GL Account if changed
                if (!string.IsNullOrEmpty(dto.DebitAccno) && dto.DebitAccno != payment.DebitAccno)
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.DebitAccno && g.CompanyCode == dto.CompanyCode && g.Status == true);

                    if (glAccount == null)
                    {
                        throw new InvalidOperationException($"Debit GL Account '{dto.DebitAccno}' not found or inactive.");
                    }

                    if (string.IsNullOrEmpty(dto.DebitAccName))
                    {
                        dto.DebitAccName = glAccount.Glaccname;
                    }
                }

                var oldPayment = new
                {
                    payment.Amount,
                    payment.ChequeNo,
                    payment.Remarks,
                    payment.DebitAccno,
                    payment.DebitAccName
                };

                // Reverse the old payment from invoice and supplier
                invoice.AmountPaid = (invoice.AmountPaid ?? 0) - payment.Amount;
                invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;

                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) + payment.Amount;

                // Update payment with new values
                payment.Amount = dto.Amount;
                payment.ChequeNo = dto.ChequeNo ?? payment.ChequeNo;
                payment.Remarks = dto.Remarks ?? payment.Remarks;
                payment.TransDate = dto.TransDate ?? payment.TransDate;
                payment.DebitAccno = dto.DebitAccno ?? payment.DebitAccno;
                payment.DebitAccName = dto.DebitAccName ?? payment.DebitAccName;
                payment.AuditId = updatedBy;
                payment.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Apply new payment to invoice and supplier
                invoice.AmountPaid = (invoice.AmountPaid ?? 0) + payment.Amount;
                invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;

                if (invoice.Balance <= 0)
                {
                    invoice.PaymentStatus = "Paid";
                    invoice.Status = "Paid";
                }
                else if (invoice.AmountPaid > 0)
                {
                    invoice.PaymentStatus = "Partially Paid";
                    invoice.Status = "Partially Paid";
                }
                else
                {
                    invoice.PaymentStatus = "Unpaid";
                    invoice.Status = "Pending";
                }

                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) - payment.Amount;

                await _context.SaveChangesAsync();

                // Update GL Transaction
                var glTransaction = await _context.Gltransactions
                    .FirstOrDefaultAsync(g => g.TransactionNo == payment.TransactionNo && g.CompanyCode == dto.CompanyCode);

                if (glTransaction != null)
                {
                    glTransaction.Amount = payment.Amount;
                    glTransaction.DrAccNo = payment.DebitAccno ?? supplier.GlAccountNo;
                    glTransaction.CrAccNo = supplier.GlAccountNo ?? payment.DebitAccno;
                    glTransaction.ChequeNo = payment.ChequeNo;
                    glTransaction.TransDescript = $"Payment for invoice {payment.InvoiceNo} - {supplier.SupplierName}";
                    glTransaction.AuditTime = DateTime.Now;
                    glTransaction.AuditId = updatedBy;
                    await _context.SaveChangesAsync();
                }

                // Update Journal
                var journal = await _context.Journals
                    .FirstOrDefaultAsync(j => j.Transactionno == payment.TransactionNo && j.CompanyCode == dto.CompanyCode);

                if (journal != null)
                {
                    journal.ACCNO = supplier.GlAccountNo;
                    journal.NAME = supplier.SupplierName;
                    journal.NARATION = $"Payment for invoice {payment.InvoiceNo} - {supplier.SupplierName}";
                    journal.AMOUNT = payment.Amount;
                    journal.AUDITID = updatedBy;
                    journal.AUDITDATE = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                // Update Journals Listing
                var journalsListing = await _context.JournalsListings
                    .FirstOrDefaultAsync(j => j.TransactionNo == payment.TransactionNo && j.CompanyCode == dto.CompanyCode);

                if (journalsListing != null)
                {
                    journalsListing.AccountNo = supplier.GlAccountNo;
                    journalsListing.AccountName = supplier.SupplierName;
                    journalsListing.Narration = $"Payment for invoice {payment.InvoiceNo} - {supplier.SupplierName}";
                    journalsListing.Amount = payment.Amount;
                    journalsListing.AmountDr = 0;  
                    journalsListing.AmountCr = payment.Amount;  
                    journalsListing.AuditId = updatedBy;
                    journalsListing.AuditDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                // Update Blockchain
                var blockchainData = new
                {
                    PaymentId = payment.Id,
                    InvoiceNo = payment.InvoiceNo,
                    Amount = payment.Amount,
                    UpdatedBy = updatedBy,
                    UpdatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_PAYMENT_UPDATED",
                    MemberNo = null,
                    CompanyCode = payment.CompanyCode,
                    Amount = payment.Amount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = payment.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                var blockchainTxId = blockchainTx.TransactionId;

                payment.BlockchainTxId = blockchainTxId;
                invoice.BlockchainTxId = blockchainTxId;
                if (glTransaction != null) glTransaction.BlockchainTxId = blockchainTxId;
                if (journal != null) journal.BlockchainTxId = blockchainTxId;
                if (journalsListing != null) journalsListing.BlockchainTxId = blockchainTxId;

                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: oldPayment,
                    newModel: payment,
                    tableName: "InvoicePayments",
                    recordId: payment.Id.ToString(),
                    userId: updatedBy,
                    userName: updatedBy,
                    companyCode: payment.CompanyCode,
                    module: "PaymentManagement",
                    blockchainTxId: blockchainTxId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(payment);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating payment with ID: {id}");
                throw;
            }
        }

        public async Task<bool> DeletePaymentAsync(long id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var payment = await _context.InvoicePayments
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (payment == null)
                {
                    throw new InvalidOperationException($"Payment with ID {id} not found");
                }

                // Get invoice
                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == payment.InvoiceNo && i.CompanyCode == payment.CompanyCode);

                if (invoice != null)
                {
                    // Reverse payment from invoice
                    invoice.AmountPaid = (invoice.AmountPaid ?? 0) - payment.Amount;
                    invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;

                    if (invoice.Balance <= 0)
                    {
                        invoice.PaymentStatus = "Paid";
                        invoice.Status = "Paid";
                    }
                    else if (invoice.AmountPaid > 0)
                    {
                        invoice.PaymentStatus = "Partially Paid";
                        invoice.Status = "Partially Paid";
                    }
                    else
                    {
                        invoice.PaymentStatus = "Unpaid";
                        invoice.Status = "Pending";
                    }

                    await _context.SaveChangesAsync();
                }

                // Get supplier
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == payment.SupplierId && s.CompanyCode == payment.CompanyCode);

                if (supplier != null)
                {
                    supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) + payment.Amount;
                    await _context.SaveChangesAsync();
                }

                // Delete GL Transaction
                var glTransaction = await _context.Gltransactions
                    .FirstOrDefaultAsync(g => g.TransactionNo == payment.TransactionNo && g.CompanyCode == payment.CompanyCode);

                if (glTransaction != null)
                {
                    _context.Gltransactions.Remove(glTransaction);
                }

                // Delete Journal
                var journal = await _context.Journals
                    .FirstOrDefaultAsync(j => j.Transactionno == payment.TransactionNo && j.CompanyCode == payment.CompanyCode);

                if (journal != null)
                {
                    _context.Journals.Remove(journal);
                }

                // Delete Journals Listing
                var journalsListing = await _context.JournalsListings
                    .FirstOrDefaultAsync(j => j.TransactionNo == payment.TransactionNo && j.CompanyCode == payment.CompanyCode);

                if (journalsListing != null)
                {
                    _context.JournalsListings.Remove(journalsListing);
                }

                var paymentForAudit = new
                {
                    payment.Id,
                    payment.InvoiceNo,
                    payment.SupplierId,
                    payment.Amount,
                    payment.ReceiptNo,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                _context.InvoicePayments.Remove(payment);
                await _context.SaveChangesAsync();

                // Record Blockchain deletion
                var blockchainData = new
                {
                    PaymentId = payment.Id,
                    InvoiceNo = payment.InvoiceNo,
                    Amount = payment.Amount,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_PAYMENT_DELETED",
                    MemberNo = null,
                    CompanyCode = payment.CompanyCode,
                    Amount = payment.Amount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = payment.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: paymentForAudit,
                    newModel: null,
                    tableName: "InvoicePayments",
                    recordId: payment.Id.ToString(),
                    userId: deletedBy,
                    userName: deletedBy,
                    companyCode: payment.CompanyCode,
                    module: "PaymentManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting payment with ID: {id}");
                throw;
            }
        }

        public async Task<InvoicePaymentResponseDTO> GetPaymentByIdAsync(long id)
        {
            var payment = await _context.InvoicePayments
                .FirstOrDefaultAsync(p => p.Id == id);

            return payment != null ? MapToResponseDTO(payment) : null;
        }

        public async Task<List<InvoicePaymentResponseDTO>> GetPaymentsByInvoiceAsync(string invoiceNo, string companyCode)
        {
            var payments = await _context.InvoicePayments
                .Where(p => p.InvoiceNo == invoiceNo && p.CompanyCode == companyCode)
                .OrderByDescending(p => p.TransDate)
                .ToListAsync();

            return payments.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<InvoicePaymentResponseDTO>> GetPaymentsBySupplierAsync(string supplierCode, string companyCode)
        {
            var payments = await _context.InvoicePayments
                .Where(p => p.SupplierId == supplierCode && p.CompanyCode == companyCode)
                .OrderByDescending(p => p.TransDate)
                .ToListAsync();

            return payments.Select(MapToResponseDTO).ToList();
        }

        public async Task<InvoicePaymentViewModel> GetPaymentDashboardAsync(string companyCode)
        {
            var payments = await _context.InvoicePayments
                .Where(p => p.CompanyCode == companyCode)
                .OrderByDescending(p => p.TransDate)
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode)
                .ToListAsync();

            var invoices = await _context.InvoiceReceive
                .Where(i => i.CompanyCode == companyCode && i.Balance > 0)
                .ToListAsync();

            var glAccounts = await GetGlAccountsForDropdownAsync(companyCode);

            var viewModel = new InvoicePaymentViewModel
            {
                Payments = payments.Select(MapToResponseDTO).ToList(),
                TotalPayments = payments.Count,
                TotalPaymentAmount = payments.Sum(p => p.Amount),
                UserCompanyCode = companyCode,
                Suppliers = suppliers.Select(s => new SupplierResponseDTO
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    SupplierName = s.SupplierName,
                    PhoneNo = s.PhoneNo,
                    Email = s.Email,
                    IsActive = s.IsActive,
                    GlAccountNo = s.GlAccountNo,
                    GlAccountName = s.GlAccountName
                }).ToList(),
                Invoices = invoices.Select(i => new InvoiceReceiveResponseDTO
                {
                    Id = i.Id,
                    InvoiceNo = i.InvoiceNo,
                    SupplierCode = i.SupplierCode,
                    SupplierName = i.SupplierName,
                    InvoiceAmount = i.InvoiceAmount,
                    Balance = i.Balance,
                    PaymentStatus = i.PaymentStatus,
                    TotalAmount = i.TotalAmount
                }).ToList(),
                GlAccounts = glAccounts
            };

            return viewModel;
        }

        public async Task<List<GlAccountDTO>> GetGlAccountsForDropdownAsync(string companyCode)
        {
            var glAccounts = await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .OrderBy(g => g.Glaccname)
                .Select(g => new GlAccountDTO
                {
                    GlId = g.GlId,
                    Glcode = g.Glcode,
                    Glaccname = g.Glaccname,
                    AccNo = g.AccNo,
                    Glacctype = g.Glacctype,
                    GlAccMainGroup = g.GlAccMainGroup,
                    CurrentBal = g.CurrentBal
                })
                .ToListAsync();

            return glAccounts;
        }

        public async Task<GlAccountDTO> GetGlAccountByCodeAsync(string glAccountNo, string companyCode)
        {
            var glAccount = await _context.GlSetup
                .Where(g => g.AccNo == glAccountNo && g.CompanyCode == companyCode && g.Status == true)
                .Select(g => new GlAccountDTO
                {
                    GlId = g.GlId,
                    Glcode = g.Glcode,
                    Glaccname = g.Glaccname,
                    AccNo = g.AccNo,
                    Glacctype = g.Glacctype,
                    GlAccMainGroup = g.GlAccMainGroup,
                    CurrentBal = g.CurrentBal
                })
                .FirstOrDefaultAsync();

            return glAccount;
        }

        #region Helper Methods
        public async Task<InvoicePaymentResponseDTO> GetPaymentByReceiptNoAsync(string receiptNo, string companyCode)
        {
            var payment = await _context.InvoicePayments
                .FirstOrDefaultAsync(p => p.ReceiptNo == receiptNo && p.CompanyCode == companyCode);

            return payment != null ? MapToResponseDTO(payment) : null;
        }
        private InvoicePaymentResponseDTO MapToResponseDTO(InvoicePayment payment)
        {
            return new InvoicePaymentResponseDTO
            {
                Id = payment.Id,
                CompanyCode = payment.CompanyCode,
                Amount = payment.Amount,
                OpeningBalance = payment.OpeningBalance,
                SupplierId = payment.SupplierId,
                SupplierName = payment.SupplierAccName,
                Particulars = payment.Particulars,
                TransDate = payment.TransDate,
                OpeningDate = payment.OpeningDate,
                DueDate = payment.DueDate,
                ChequeNo = payment.ChequeNo,
                InvoiceNo = payment.InvoiceNo,
                Remarks = payment.Remarks,
                Transtype = payment.Transtype,
                SupplierAccno = payment.SupplierAccno,
                DebitAccno = payment.DebitAccno,
                SupplierAccName = payment.SupplierAccName,
                DebitAccName = payment.DebitAccName,
                ReceiptNo = payment.ReceiptNo,
                BlockchainTxId = payment.BlockchainTxId,
                CreatedBy = payment.AuditId,
                CreatedDate = payment.AuditTime?.ToString("dd/MM/yyyy HH:mm")
            };
        }

        #endregion
    }
}