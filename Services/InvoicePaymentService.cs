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

                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo && i.CompanyCode == dto.CompanyCode);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice '{dto.InvoiceNo}' not found.");
                }

                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == dto.SupplierId && s.CompanyCode == dto.CompanyCode);

                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier not found.");
                }

                if (dto.Amount > invoice.Balance)
                {
                    throw new InvalidOperationException($"Payment amount ({dto.Amount:C}) exceeds invoice balance ({invoice.Balance:C}).");
                }

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
                    SupplierAccno = supplier.GlAccountNo,
                    DebitAccno = dto.DebitAccno,
                    SupplierAccName = supplier.GlAccountName,
                    DebitAccName = dto.DebitAccName,
                    ReceiptNo = dto.ReceiptNo ?? $"RCPT-{DateTime.Now:yyyyMMddHHmmss}",
                    TransactionNo = $"PAY-{DateTime.Now:yyyyMMddHHmmss}",
                    AuditId = createdBy,
                    AuditTime = DateTime.Now
                };

                _context.InvoicePayments.Add(payment);
                await _context.SaveChangesAsync();

                invoice.AmountPaid = (invoice.AmountPaid ?? 0) + dto.Amount;
                invoice.Balance = invoice.InvoiceAmount - invoice.AmountPaid;

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

                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) - dto.Amount;
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    PaymentId = payment.Id,
                    InvoiceNo = payment.InvoiceNo,
                    SupplierId = payment.SupplierId,
                    Amount = payment.Amount,
                    ReceiptNo = payment.ReceiptNo,
                    PaymentDate = payment.TransDate,
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

                payment.BlockchainTxId = blockchainTx.TransactionId;
                invoice.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

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
                    blockchainTxId: blockchainTx.TransactionId
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

                var oldPayment = new
                {
                    payment.Amount,
                    payment.ChequeNo,
                    payment.Remarks
                };

                payment.Amount = dto.Amount;
                payment.ChequeNo = dto.ChequeNo ?? payment.ChequeNo;
                payment.Remarks = dto.Remarks ?? payment.Remarks;
                payment.TransDate = dto.TransDate ?? payment.TransDate;
                payment.AuditId = updatedBy;
                payment.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Recalculate invoice balance
                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == payment.InvoiceNo && i.CompanyCode == dto.CompanyCode);

                if (invoice != null)
                {
                    var totalPayments = await _context.InvoicePayments
                        .Where(p => p.InvoiceNo == payment.InvoiceNo && p.CompanyCode == dto.CompanyCode)
                        .SumAsync(p => p.Amount);

                    invoice.AmountPaid = totalPayments;
                    invoice.Balance = invoice.InvoiceAmount - totalPayments;

                    if (invoice.Balance <= 0)
                    {
                        invoice.PaymentStatus = "Paid";
                        invoice.Status = "Paid";
                    }
                    else if (totalPayments > 0)
                    {
                        invoice.PaymentStatus = "Partially Paid";
                        invoice.Status = "Partially Paid";
                    }

                    await _context.SaveChangesAsync();
                }

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

                payment.BlockchainTxId = blockchainTx.TransactionId;
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
                    blockchainTxId: blockchainTx.TransactionId
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

                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == payment.InvoiceNo && i.CompanyCode == payment.CompanyCode);

                if (invoice != null)
                {
                    invoice.AmountPaid = (invoice.AmountPaid ?? 0) - payment.Amount;
                    invoice.Balance = invoice.InvoiceAmount - invoice.AmountPaid;

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

                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == payment.SupplierId && s.CompanyCode == payment.CompanyCode);

                if (supplier != null)
                {
                    supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) + payment.Amount;
                    await _context.SaveChangesAsync();
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
                .Where(i => i.CompanyCode == companyCode)
                .ToListAsync();

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
                    IsActive = s.IsActive
                }).ToList(),
                Invoices = invoices.Select(i => new InvoiceReceiveResponseDTO
                {
                    Id = i.Id,
                    InvoiceNo = i.InvoiceNo,
                    SupplierCode = i.SupplierCode,
                    SupplierName = i.SupplierName,
                    InvoiceAmount = i.InvoiceAmount,
                    Balance = i.Balance,
                    PaymentStatus = i.PaymentStatus
                }).ToList()
            };

            return viewModel;
        }

        #region Helper Methods

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