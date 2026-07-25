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
    public interface IInvoiceService
    {
        Task<InvoiceReceiveResponseDTO> CreateInvoiceAsync(InvoiceReceiveDTO dto, string createdBy);
        Task<InvoiceReceiveResponseDTO> UpdateInvoiceAsync(long id, InvoiceReceiveDTO dto, string updatedBy);
        Task<bool> DeleteInvoiceAsync(long id, string deletedBy);
        Task<InvoiceReceiveResponseDTO> GetInvoiceByIdAsync(long id);
        Task<List<InvoiceReceiveResponseDTO>> GetAllInvoicesAsync(string companyCode);
        Task<List<InvoiceReceiveResponseDTO>> SearchInvoicesAsync(InvoiceSearchDTO searchDto);
        Task<InvoiceReceiveResponseDTO> GetInvoiceByNumberAsync(string invoiceNo, string companyCode);
        Task<InvoiceReceiveViewModel> GetInvoiceDashboardAsync(string companyCode);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly AuditTrailService _auditService;

        public InvoiceService(
            ApplicationDbContext context,
            ILogger<InvoiceService> logger,
            IBlockchainService blockchainService,
            AuditTrailService auditService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _auditService = auditService;
        }

        public async Task<InvoiceReceiveResponseDTO> CreateInvoiceAsync(InvoiceReceiveDTO dto, string createdBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating invoice: {dto.InvoiceNo}");

                // Check for duplicate invoice
                var existing = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo && i.CompanyCode == dto.CompanyCode);

                if (existing != null)
                {
                    throw new InvalidOperationException($"Invoice number '{dto.InvoiceNo}' already exists.");
                }

                // Get supplier details
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == dto.SupplierCode && s.CompanyCode == dto.CompanyCode);

                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier '{dto.SupplierCode}' not found.");
                }

                var invoice = new InvoiceReceive
                {
                    InvoiceNo = dto.InvoiceNo,
                    SupplierCode = dto.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    InvoiceAmount = dto.InvoiceAmount,
                    AmountPaid = 0,
                    Balance = dto.InvoiceAmount,
                    TaxAmount = dto.TaxAmount,
                    DiscountAmount = dto.DiscountAmount,
                    InvoiceDate = dto.InvoiceDate ?? DateTime.Now,
                    DueDate = dto.DueDate ?? DateTime.Now.AddDays(30),
                    ReceivedDate = dto.ReceivedDate ?? DateTime.Now,
                    Description = dto.Description,
                    PurchaseOrderNo = dto.PurchaseOrderNo,
                    GlAccountNo = dto.GlAccountNo,
                    GlAccountName = dto.GlAccountName,
                    CompanyCode = dto.CompanyCode,
                    Status = "Pending",
                    PaymentStatus = "Unpaid",
                    TotalAmount = dto.InvoiceAmount,
                    Remarks = dto.Remarks,
                    AuditId = createdBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = $"INV-{DateTime.Now:yyyyMMddHHmmss}"
                };

                _context.InvoiceReceive.Add(invoice);
                await _context.SaveChangesAsync();

                // Update supplier current balance
                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) + dto.InvoiceAmount;
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    SupplierCode = invoice.SupplierCode,
                    SupplierName = invoice.SupplierName,
                    InvoiceAmount = invoice.InvoiceAmount,
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_CREATED",
                    MemberNo = null,
                    CompanyCode = dto.CompanyCode,
                    Amount = invoice.InvoiceAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = invoice.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                invoice.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: invoice,
                    tableName: "InvoiceReceive",
                    recordId: invoice.Id.ToString(),
                    userId: createdBy,
                    userName: createdBy,
                    companyCode: dto.CompanyCode,
                    module: "InvoiceManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(invoice);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating invoice: {dto.InvoiceNo}");
                throw;
            }
        }

        public async Task<InvoiceReceiveResponseDTO> UpdateInvoiceAsync(long id, InvoiceReceiveDTO dto, string updatedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.InvoiceReceive
                    .FirstOrDefaultAsync(i => i.Id == id && i.CompanyCode == dto.CompanyCode);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {id} not found");
                }

                // Don't allow editing paid invoices
                if (invoice.PaymentStatus == "Paid")
                {
                    throw new InvalidOperationException("Cannot edit a fully paid invoice.");
                }

                var oldInvoice = new
                {
                    invoice.InvoiceAmount,
                    invoice.DueDate,
                    invoice.Description,
                    invoice.Status,
                    invoice.PaymentStatus
                };

                invoice.InvoiceAmount = dto.InvoiceAmount;
                invoice.Balance = dto.InvoiceAmount - (invoice.AmountPaid ?? 0);
                invoice.TaxAmount = dto.TaxAmount;
                invoice.DiscountAmount = dto.DiscountAmount;
                invoice.DueDate = dto.DueDate ?? invoice.DueDate;
                invoice.Description = dto.Description ?? invoice.Description;
                invoice.PurchaseOrderNo = dto.PurchaseOrderNo ?? invoice.PurchaseOrderNo;
                invoice.GlAccountNo = dto.GlAccountNo ?? invoice.GlAccountNo;
                invoice.GlAccountName = dto.GlAccountName ?? invoice.GlAccountName;
                invoice.Remarks = dto.Remarks ?? invoice.Remarks;
                invoice.TotalAmount = dto.InvoiceAmount;
                invoice.AuditId = updatedBy;
                invoice.AuditTime = DateTime.Now;

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

                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    InvoiceAmount = invoice.InvoiceAmount,
                    Balance = invoice.Balance,
                    DueDate = invoice.DueDate,
                    PaymentStatus = invoice.PaymentStatus,
                    UpdatedBy = updatedBy,
                    UpdatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_UPDATED",
                    MemberNo = null,
                    CompanyCode = invoice.CompanyCode,
                    Amount = invoice.InvoiceAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = invoice.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                invoice.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: oldInvoice,
                    newModel: invoice,
                    tableName: "InvoiceReceive",
                    recordId: invoice.Id.ToString(),
                    userId: updatedBy,
                    userName: updatedBy,
                    companyCode: invoice.CompanyCode,
                    module: "InvoiceManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(invoice);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating invoice with ID: {id}");
                throw;
            }
        }

        public async Task<bool> DeleteInvoiceAsync(long id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.InvoiceReceive
                    .Include(i => i.AmountPaid)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {id} not found");
                }

                if (invoice.Payments.Any())
                {
                    throw new InvalidOperationException($"Cannot delete invoice '{invoice.InvoiceNo}' because it has {invoice.Payments.Count} payment(s).");
                }

                var invoiceForAudit = new
                {
                    invoice.Id,
                    invoice.InvoiceNo,
                    invoice.SupplierCode,
                    invoice.InvoiceAmount,
                    invoice.Balance,
                    invoice.Status,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                _context.InvoiceReceive.Remove(invoice);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    SupplierCode = invoice.SupplierCode,
                    InvoiceAmount = invoice.InvoiceAmount,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_DELETED",
                    MemberNo = null,
                    CompanyCode = invoice.CompanyCode,
                    Amount = invoice.InvoiceAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = invoice.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: invoiceForAudit,
                    newModel: null,
                    tableName: "InvoiceReceive",
                    recordId: invoice.Id.ToString(),
                    userId: deletedBy,
                    userName: deletedBy,
                    companyCode: invoice.CompanyCode,
                    module: "InvoiceManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting invoice with ID: {id}");
                throw;
            }
        }

        public async Task<InvoiceReceiveResponseDTO> GetInvoiceByIdAsync(long id)
        {
            var invoice = await _context.InvoiceReceive
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            return invoice != null ? MapToResponseDTO(invoice) : null;
        }

        public async Task<List<InvoiceReceiveResponseDTO>> GetAllInvoicesAsync(string companyCode)
        {
            var invoices = await _context.InvoiceReceive
                .Where(i => i.CompanyCode == companyCode)
                .OrderByDescending(i => i.InvoiceDate)
                .Include(i => i.Payments)
                .ToListAsync();

            return invoices.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<InvoiceReceiveResponseDTO>> SearchInvoicesAsync(InvoiceSearchDTO searchDto)
        {
            // FIX: Remove .Include() and use a separate query for Payments
            var query = _context.InvoiceReceive
                .Where(i => i.CompanyCode == searchDto.CompanyCode);

            if (!string.IsNullOrEmpty(searchDto.InvoiceNo))
            {
                query = query.Where(i => i.InvoiceNo.Contains(searchDto.InvoiceNo));
            }

            if (!string.IsNullOrEmpty(searchDto.SupplierCode))
            {
                query = query.Where(i => i.SupplierCode == searchDto.SupplierCode);
            }

            if (!string.IsNullOrEmpty(searchDto.SupplierName))
            {
                query = query.Where(i => i.SupplierName.Contains(searchDto.SupplierName));
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                query = query.Where(i => i.Status == searchDto.Status);
            }

            if (!string.IsNullOrEmpty(searchDto.PaymentStatus))
            {
                query = query.Where(i => i.PaymentStatus == searchDto.PaymentStatus);
            }

            if (searchDto.FromDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= searchDto.FromDate.Value);
            }

            if (searchDto.ToDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate <= searchDto.ToDate.Value);
            }

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            // Load payments separately for each invoice
            foreach (var invoice in invoices)
            {
                invoice.Payments = await _context.InvoicePayments
                    .Where(p => p.InvoiceNo == invoice.InvoiceNo && p.CompanyCode == searchDto.CompanyCode)
                    .ToListAsync();
            }

            return invoices.Select(MapToResponseDTO).ToList();
        }

        public async Task<InvoiceReceiveResponseDTO> GetInvoiceByNumberAsync(string invoiceNo, string companyCode)
        {
            var invoice = await _context.InvoiceReceive
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyCode == companyCode);

            return invoice != null ? MapToResponseDTO(invoice) : null;
        }

        public async Task<InvoiceReceiveViewModel> GetInvoiceDashboardAsync(string companyCode)
        {
            var invoices = await _context.InvoiceReceive
                .Where(i => i.CompanyCode == companyCode)
                .Include(i => i.Payments)
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode)
                .ToListAsync();

            var viewModel = new InvoiceReceiveViewModel
            {
                Invoices = invoices.Select(MapToResponseDTO).ToList(),
                TotalInvoices = invoices.Count,
                PendingInvoices = invoices.Count(i => i.Status == "Pending"),
                PartiallyPaidInvoices = invoices.Count(i => i.Status == "Partially Paid"),
                PaidInvoices = invoices.Count(i => i.Status == "Paid"),
                OverdueInvoices = invoices.Count(i => i.DueDate < DateTime.Now && i.Balance > 0),
                TotalInvoiceAmount = invoices.Sum(i => i.InvoiceAmount),
                TotalOutstanding = invoices.Sum(i => i.Balance ?? 0),
                UserCompanyCode = companyCode,
                Suppliers = suppliers.Select(s => new SupplierResponseDTO
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    SupplierName = s.SupplierName,
                    PhoneNo = s.PhoneNo,
                    Email = s.Email,
                    IsActive = s.IsActive
                }).ToList()
            };

            return viewModel;
        }

        #region Helper Methods

        private InvoiceReceiveResponseDTO MapToResponseDTO(InvoiceReceive invoice)
        {
            var totalPaid = invoice.Payments?.Sum(p => p.Amount) ?? 0;
            var daysOverdue = 0;

            if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTime.Now && invoice.Balance > 0)
            {
                daysOverdue = (DateTime.Now - invoice.DueDate.Value).Days;
            }

            return new InvoiceReceiveResponseDTO
            {
                Id = invoice.Id,
                InvoiceNo = invoice.InvoiceNo,
                SupplierCode = invoice.SupplierCode,
                SupplierName = invoice.SupplierName,
                InvoiceAmount = invoice.InvoiceAmount,
                AmountPaid = invoice.AmountPaid,
                Balance = invoice.Balance,
                TaxAmount = invoice.TaxAmount,
                DiscountAmount = invoice.DiscountAmount,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                ReceivedDate = invoice.ReceivedDate,
                Description = invoice.Description,
                PurchaseOrderNo = invoice.PurchaseOrderNo,
                GlAccountNo = invoice.GlAccountNo,
                GlAccountName = invoice.GlAccountName,
                CompanyCode = invoice.CompanyCode,
                Status = invoice.Status,
                PaymentStatus = invoice.PaymentStatus,
                TotalAmount = invoice.TotalAmount,
                Remarks = invoice.Remarks,
                BlockchainTxId = invoice.BlockchainTxId,
                CreatedBy = invoice.AuditId,
                CreatedDate = invoice.AuditTime?.ToString("dd/MM/yyyy HH:mm"),
                PaymentCount = invoice.Payments?.Count ?? 0,
                TotalPaid = totalPaid,
                DaysOverdue = daysOverdue
            };
        }

        #endregion
    }
}