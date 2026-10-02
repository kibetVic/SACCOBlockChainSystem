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
        Task<List<GlAccountDTO>> GetGlAccountsForDropdownAsync(string companyCode);
        Task<GlAccountDTO> GetGlAccountByCodeAsync(string glAccountNo, string companyCode);
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

                // Validate GL Account if provided
                if (!string.IsNullOrEmpty(dto.GlAccountNo))
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.GlAccountNo && g.CompanyCode == dto.CompanyCode && g.Status == true);

                    if (glAccount == null)
                    {
                        throw new InvalidOperationException($"GL Account '{dto.GlAccountNo}' not found or inactive.");
                    }

                    // Auto-populate GL Account Name if not provided
                    if (string.IsNullOrEmpty(dto.GlAccountName))
                    {
                        dto.GlAccountName = glAccount.Glaccname;
                    }
                }

                // Calculate Tax Amount from Tax Percentage
                decimal calculatedTaxAmount = 0;
                if (dto.TaxPercentage.HasValue && dto.TaxPercentage.Value > 0)
                {
                    calculatedTaxAmount = (dto.InvoiceAmount * dto.TaxPercentage.Value) / 100;
                    dto.TaxAmount = calculatedTaxAmount;
                }

                // Calculate Total Amount
                decimal totalAmount = dto.InvoiceAmount + (dto.TaxAmount ?? 0) - (dto.DiscountAmount ?? 0);
                dto.TotalAmount = totalAmount;

                var invoice = new InvoiceReceive
                {
                    InvoiceNo = dto.InvoiceNo,
                    SupplierCode = dto.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    InvoiceAmount = dto.InvoiceAmount,
                    AmountPaid = 0,
                    Balance = totalAmount,
                    TaxPercentage = dto.TaxPercentage,
                    TaxAmount = dto.TaxAmount,
                    DiscountAmount = dto.DiscountAmount,
                    InvoiceDate = dto.InvoiceDate ?? DateTime.Now,
                    DueDate = dto.DueDate ?? DateTime.Now.AddDays(30),
                    ReceivedDate = dto.ReceivedDate ?? DateTime.Now,
                    PurchaseOrderNo = dto.PurchaseOrderNo,
                    GlAccountNo = dto.GlAccountNo,
                    GlAccountName = dto.GlAccountName,
                    CompanyCode = dto.CompanyCode,
                    Status = "Pending",
                    PaymentStatus = "Unpaid",
                    TotalAmount = totalAmount,
                    AuditId = createdBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = $"INV-{DateTime.Now:yyyyMMddHHmmss}",
                    ReceiptNo = dto.ReceiptNo,
                    InvoiceItems = new List<InvoiceItem>()
                };

                // Update status based on payment
                UpdateInvoiceStatus(invoice);

                _context.InvoiceReceive.Add(invoice);
                await _context.SaveChangesAsync();

                // ============================================================
                // SAVE INVOICE ITEMS
                // ============================================================
                if (dto.InvoiceItems != null && dto.InvoiceItems.Any())
                {
                    foreach (var itemDto in dto.InvoiceItems)
                    {
                        var item = new InvoiceItem
                        {
                            InvoiceId = invoice.Id,
                            Description = itemDto.Description,
                            Quantity = itemDto.Quantity,
                            UnitPrice = itemDto.UnitPrice,
                            Total = itemDto.Quantity * itemDto.UnitPrice,
                            CompanyCode = dto.CompanyCode
                        };

                        _context.InvoiceItems.Add(item);
                    }

                    await _context.SaveChangesAsync();

                    // Update InvoiceAmount from items (ensure consistency)
                    var totalItemsAmount = dto.InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
                    if (Math.Abs(invoice.InvoiceAmount - totalItemsAmount) > 0.01m)
                    {
                        // If there's a discrepancy, update the invoice amount
                        invoice.InvoiceAmount = totalItemsAmount;
                        // Recalculate total with tax and discount
                        decimal recalculatedTotal = totalItemsAmount + (dto.TaxAmount ?? 0) - (dto.DiscountAmount ?? 0);
                        invoice.TotalAmount = recalculatedTotal;
                        invoice.Balance = recalculatedTotal - (invoice.AmountPaid ?? 0);
                        await _context.SaveChangesAsync();
                    }
                }

                // Update supplier current balance
                supplier.CurrentBalance = (supplier.CurrentBalance ?? 0) + (invoice.TotalAmount ?? 0);
                await _context.SaveChangesAsync();

                // ============================================================
                // BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    SupplierCode = invoice.SupplierCode,
                    SupplierName = invoice.SupplierName,
                    InvoiceAmount = invoice.InvoiceAmount,
                    TaxAmount = invoice.TaxAmount,
                    DiscountAmount = invoice.DiscountAmount,
                    TotalAmount = invoice.TotalAmount,
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    GlAccountNo = invoice.GlAccountNo,
                    GlAccountName = invoice.GlAccountName,
                    ItemCount = dto.InvoiceItems?.Count ?? 0,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_CREATED",
                    MemberNo = null,
                    CompanyCode = dto.CompanyCode,
                    Amount = invoice.TotalAmount ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = invoice.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update invoice with BlockchainTxId
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

                // Reload invoice with items for response
                var savedInvoice = await _context.InvoiceReceive
                    .Include(i => i.InvoiceItems)
                    .FirstOrDefaultAsync(i => i.Id == invoice.Id);

                return MapToResponseDTO(savedInvoice);
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
                    .Include(i => i.InvoiceItems) // Include items for update
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

                // Validate GL Account if provided and changed
                if (!string.IsNullOrEmpty(dto.GlAccountNo) && dto.GlAccountNo != invoice.GlAccountNo)
                {
                    var glAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.GlAccountNo && g.CompanyCode == invoice.CompanyCode && g.Status == true);

                    if (glAccount == null)
                    {
                        throw new InvalidOperationException($"GL Account '{dto.GlAccountNo}' not found or inactive.");
                    }

                    if (string.IsNullOrEmpty(dto.GlAccountName))
                    {
                        dto.GlAccountName = glAccount.Glaccname;
                    }
                }

                var oldInvoice = new
                {
                    invoice.InvoiceAmount,
                    invoice.TaxAmount,
                    invoice.DiscountAmount,
                    invoice.TotalAmount,
                    invoice.DueDate,
                    invoice.GlAccountNo,
                    invoice.GlAccountName,
                    invoice.Status,
                    invoice.PaymentStatus,
                    ItemCount = invoice.InvoiceItems?.Count ?? 0
                };

                // Calculate Tax Amount from Tax Percentage
                decimal calculatedTaxAmount = 0;
                if (dto.TaxPercentage.HasValue && dto.TaxPercentage.Value > 0)
                {
                    calculatedTaxAmount = (dto.InvoiceAmount * dto.TaxPercentage.Value) / 100;
                    dto.TaxAmount = calculatedTaxAmount;
                }

                // Calculate Total Amount
                decimal totalAmount = dto.InvoiceAmount + (dto.TaxAmount ?? 0) - (dto.DiscountAmount ?? 0);
                dto.TotalAmount = totalAmount;

                // Update invoice
                invoice.InvoiceAmount = dto.InvoiceAmount;
                invoice.TaxPercentage = dto.TaxPercentage;
                invoice.TaxAmount = dto.TaxAmount;
                invoice.DiscountAmount = dto.DiscountAmount;
                invoice.TotalAmount = totalAmount;
                invoice.Balance = totalAmount - (invoice.AmountPaid ?? 0);
                invoice.DueDate = dto.DueDate ?? invoice.DueDate;
                invoice.PurchaseOrderNo = dto.PurchaseOrderNo ?? invoice.PurchaseOrderNo;
                invoice.GlAccountNo = dto.GlAccountNo ?? invoice.GlAccountNo;
                invoice.GlAccountName = dto.GlAccountName ?? invoice.GlAccountName;
                invoice.ReceiptNo = dto.ReceiptNo ?? invoice.ReceiptNo;
                invoice.AuditId = updatedBy;
                invoice.AuditTime = DateTime.Now;

                // ============================================================
                // UPDATE INVOICE ITEMS - Replace all items
                // ============================================================
                // Remove existing items
                if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
                {
                    _context.InvoiceItems.RemoveRange(invoice.InvoiceItems);
                }

                // Add new items
                if (dto.InvoiceItems != null && dto.InvoiceItems.Any())
                {
                    foreach (var itemDto in dto.InvoiceItems)
                    {
                        var item = new InvoiceItem
                        {
                            InvoiceId = invoice.Id,
                            Description = itemDto.Description,
                            Quantity = itemDto.Quantity,
                            UnitPrice = itemDto.UnitPrice,
                            Total = itemDto.Quantity * itemDto.UnitPrice,
                            CompanyCode = dto.CompanyCode
                        };

                        _context.InvoiceItems.Add(item);
                    }

                    // Update InvoiceAmount from items (ensure consistency)
                    var totalItemsAmount = dto.InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
                    if (Math.Abs(invoice.InvoiceAmount - totalItemsAmount) > 0.01m)
                    {
                        invoice.InvoiceAmount = totalItemsAmount;
                        // Recalculate total with tax and discount
                        decimal recalculatedTotal = totalItemsAmount + (dto.TaxAmount ?? 0) - (dto.DiscountAmount ?? 0);
                        invoice.TotalAmount = recalculatedTotal;
                        invoice.Balance = recalculatedTotal - (invoice.AmountPaid ?? 0);
                    }
                }

                // Update status
                UpdateInvoiceStatus(invoice);

                await _context.SaveChangesAsync();

                // ============================================================
                // BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    InvoiceAmount = invoice.InvoiceAmount,
                    TaxAmount = invoice.TaxAmount,
                    DiscountAmount = invoice.DiscountAmount,
                    TotalAmount = invoice.TotalAmount,
                    Balance = invoice.Balance,
                    DueDate = invoice.DueDate,
                    PaymentStatus = invoice.PaymentStatus,
                    GlAccountNo = invoice.GlAccountNo,
                    GlAccountName = invoice.GlAccountName,
                    ItemCount = dto.InvoiceItems?.Count ?? 0,
                    UpdatedBy = updatedBy,
                    UpdatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_UPDATED",
                    MemberNo = null,
                    CompanyCode = invoice.CompanyCode,
                    Amount = invoice.TotalAmount ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = invoice.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update invoice with new BlockchainTxId
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

                // Reload invoice with items for response
                var updatedInvoice = await _context.InvoiceReceive
                    .Include(i => i.InvoiceItems)
                    .FirstOrDefaultAsync(i => i.Id == invoice.Id);

                return MapToResponseDTO(updatedInvoice);
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
                    .Include(i => i.Payments)
                    .Include(i => i.InvoiceItems) // Include items for deletion
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
                    invoice.TaxAmount,
                    invoice.DiscountAmount,
                    invoice.TotalAmount,
                    invoice.Balance,
                    invoice.Status,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now,
                    ItemCount = invoice.InvoiceItems?.Count ?? 0
                };

                // Remove invoice items first
                if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
                {
                    _context.InvoiceItems.RemoveRange(invoice.InvoiceItems);
                }

                _context.InvoiceReceive.Remove(invoice);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    InvoiceId = invoice.Id,
                    InvoiceNo = invoice.InvoiceNo,
                    SupplierCode = invoice.SupplierCode,
                    InvoiceAmount = invoice.InvoiceAmount,
                    TotalAmount = invoice.TotalAmount,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "INVOICE_DELETED",
                    MemberNo = null,
                    CompanyCode = invoice.CompanyCode,
                    Amount = invoice.TotalAmount ?? 0,
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
                .Include(i => i.InvoiceItems) // IMPORTANT: Include invoice items
                .FirstOrDefaultAsync(i => i.Id == id);

            return invoice != null ? MapToResponseDTO(invoice) : null;
        }

        public async Task<List<InvoiceReceiveResponseDTO>> GetAllInvoicesAsync(string companyCode)
        {
            var invoices = await _context.InvoiceReceive
                .Where(i => i.CompanyCode == companyCode)
                .OrderByDescending(i => i.InvoiceDate)
                .Include(i => i.Payments)
                .Include(i => i.InvoiceItems) // Include invoice items
                .ToListAsync();

            return invoices.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<InvoiceReceiveResponseDTO>> SearchInvoicesAsync(InvoiceSearchDTO searchDto)
        {
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
                .Include(i => i.InvoiceItems) // Include invoice items
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
                .Include(i => i.InvoiceItems) // Include invoice items
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyCode == companyCode);

            return invoice != null ? MapToResponseDTO(invoice) : null;
        }

        public async Task<InvoiceReceiveViewModel> GetInvoiceDashboardAsync(string companyCode)
        {
            var invoices = await _context.InvoiceReceive
                .Where(i => i.CompanyCode == companyCode)
                .Include(i => i.Payments)
                .Include(i => i.InvoiceItems) // Include invoice items
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode)
                .ToListAsync();

            var glAccounts = await GetGlAccountsForDropdownAsync(companyCode);

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

        private void UpdateInvoiceStatus(InvoiceReceive invoice)
        {
            // Update Payment Status
            if (invoice.AmountPaid.HasValue && invoice.AmountPaid.Value > 0)
            {
                if (invoice.AmountPaid.Value >= invoice.TotalAmount)
                {
                    invoice.PaymentStatus = "Paid";
                    invoice.Status = "Paid";
                }
                else
                {
                    invoice.PaymentStatus = "Partially Paid";
                    invoice.Status = "Partially Paid";
                }
            }
            else
            {
                invoice.PaymentStatus = "Unpaid";
                // Check if overdue
                if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTime.Now)
                {
                    invoice.Status = "Overdue";
                }
                else
                {
                    invoice.Status = "Pending";
                }
            }

            // If paid or partially paid but overdue, keep status as paid/partially paid
            if (invoice.Status != "Paid" && invoice.Status != "Partially Paid")
            {
                if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTime.Now && invoice.Balance > 0)
                {
                    invoice.Status = "Overdue";
                }
            }
        }

        private InvoiceReceiveResponseDTO MapToResponseDTO(InvoiceReceive invoice)
        {
            var totalPaid = invoice.Payments?.Sum(p => p.Amount) ?? 0;
            var daysOverdue = 0;

            if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTime.Now && invoice.Balance > 0)
            {
                daysOverdue = (DateTime.Now - invoice.DueDate.Value).Days;
            }

            var response = new InvoiceReceiveResponseDTO
            {
                Id = invoice.Id,
                InvoiceNo = invoice.InvoiceNo,
                SupplierCode = invoice.SupplierCode,
                SupplierName = invoice.SupplierName,
                InvoiceAmount = invoice.InvoiceAmount,
                AmountPaid = invoice.AmountPaid,
                Balance = invoice.Balance,
                TaxPercentage = invoice.TaxPercentage,
                TaxAmount = invoice.TaxAmount,
                DiscountAmount = invoice.DiscountAmount,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                ReceivedDate = invoice.ReceivedDate,
                PurchaseOrderNo = invoice.PurchaseOrderNo,
                GlAccountNo = invoice.GlAccountNo,
                GlAccountName = invoice.GlAccountName,
                CompanyCode = invoice.CompanyCode,
                Status = invoice.Status,
                PaymentStatus = invoice.PaymentStatus,
                TotalAmount = invoice.TotalAmount,
                ReceiptNo = invoice.ReceiptNo,
                BlockchainTxId = invoice.BlockchainTxId,
                CreatedBy = invoice.AuditId,
                CreatedDate = invoice.AuditTime?.ToString("dd/MM/yyyy HH:mm"),
                PaymentCount = invoice.Payments?.Count ?? 0,
                TotalPaid = totalPaid,
                DaysOverdue = daysOverdue
            };

            // Map invoice items if they exist
            if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
            {
                response.InvoiceItems = invoice.InvoiceItems.Select(item => new InvoiceItemResponseDTO
                {
                    Id = item.Id,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Total = item.Total,
                    InvoiceId = item.InvoiceId,
                    CompanyCode = item.CompanyCode,
                    BlockchainTxId = item.BlockchainTxId
                }).ToList();
            }

            return response;
        }

        #endregion
    }
}