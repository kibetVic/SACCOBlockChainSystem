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
    public interface ISupplierService
    {
        Task<SupplierResponseDTO> CreateSupplierAsync(SupplierDTO dto, string createdBy);
        Task<SupplierResponseDTO> UpdateSupplierAsync(long id, SupplierDTO dto, string updatedBy);
        Task<bool> DeleteSupplierAsync(long id, string deletedBy);
        Task<SupplierResponseDTO> GetSupplierByIdAsync(long id);
        Task<List<SupplierResponseDTO>> GetAllSuppliersAsync(string companyCode);
        Task<List<SupplierResponseDTO>> SearchSuppliersAsync(SupplierSearchDTO searchDto);
        Task<SupplierViewModel> GetSupplierDashboardAsync(string companyCode);
        Task<decimal> GetSupplierOutstandingBalanceAsync(string supplierCode, string companyCode);
    }

    public class SupplierService : ISupplierService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SupplierService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly AuditTrailService _auditService;

        public SupplierService(
            ApplicationDbContext context,
            ILogger<SupplierService> logger,
            IBlockchainService blockchainService,
            AuditTrailService auditService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _auditService = auditService;
        }

        public async Task<SupplierResponseDTO> CreateSupplierAsync(SupplierDTO dto, string createdBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating supplier: {dto.SupplierName}");

                // Generate supplier code if not provided
                if (string.IsNullOrEmpty(dto.SupplierCode))
                {
                    dto.SupplierCode = await GenerateSupplierCodeAsync(dto.CompanyCode);
                }

                // Check for duplicate supplier code
                var existing = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.SupplierCode == dto.SupplierCode && s.CompanyCode == dto.CompanyCode);

                if (existing != null)
                {
                    throw new InvalidOperationException($"Supplier code '{dto.SupplierCode}' already exists.");
                }

                var supplier = new Supplier
                {
                    SupplierCode = dto.SupplierCode,
                    SupplierName = dto.SupplierName,
                    ContactPerson = dto.ContactPerson,
                    PhoneNo = dto.PhoneNo,
                    Email = dto.Email,
                    PhysicalAddress = dto.PhysicalAddress,
                    PostalAddress = dto.PostalAddress,
                    City = dto.City,
                    County = dto.County,
                    Country = dto.Country,
                    PinNo = dto.PinNo,
                    VatNo = dto.VatNo,
                    BankName = dto.BankName,
                    BankAccountNo = dto.BankAccountNo,
                    BankAccountName = dto.BankAccountName,
                    BankBranch = dto.BankBranch,
                    GlAccountNo = dto.GlAccountNo,
                    GlAccountName = dto.GlAccountName,
                    CompanyCode = dto.CompanyCode,
                    IsActive = dto.IsActive ?? true,
                    Status = dto.IsActive == true ? "Active" : "Inactive",
                    OpeningBalance = dto.OpeningBalance,
                    CurrentBalance = dto.OpeningBalance,
                    Remarks = dto.Remarks,
                    AuditId = createdBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = $"SUP-{DateTime.Now:yyyyMMddHHmmss}"
                };

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                var blockchainData = new
                {
                    SupplierId = supplier.Id,
                    SupplierCode = supplier.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUPPLIER_CREATED",
                    MemberNo = null,
                    CompanyCode = dto.CompanyCode,
                    Amount = supplier.OpeningBalance ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = supplier.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                supplier.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: supplier,
                    tableName: "Suppliers",
                    recordId: supplier.Id.ToString(),
                    userId: createdBy,
                    userName: createdBy,
                    companyCode: dto.CompanyCode,
                    module: "SupplierManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(supplier);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating supplier: {dto.SupplierName}");
                throw;
            }
        }

        public async Task<SupplierResponseDTO> UpdateSupplierAsync(long id, SupplierDTO dto, string updatedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier with ID {id} not found");
                }

                var oldSupplier = new
                {
                    supplier.SupplierName,
                    supplier.ContactPerson,
                    supplier.PhoneNo,
                    supplier.Email,
                    supplier.IsActive,
                    supplier.Status,
                    supplier.OpeningBalance,
                    supplier.CurrentBalance
                };

                supplier.SupplierName = dto.SupplierName ?? supplier.SupplierName;
                supplier.ContactPerson = dto.ContactPerson ?? supplier.ContactPerson;
                supplier.PhoneNo = dto.PhoneNo ?? supplier.PhoneNo;
                supplier.Email = dto.Email ?? supplier.Email;
                supplier.PhysicalAddress = dto.PhysicalAddress ?? supplier.PhysicalAddress;
                supplier.PostalAddress = dto.PostalAddress ?? supplier.PostalAddress;
                supplier.City = dto.City ?? supplier.City;
                supplier.County = dto.County ?? supplier.County;
                supplier.Country = dto.Country ?? supplier.Country;
                supplier.PinNo = dto.PinNo ?? supplier.PinNo;
                supplier.VatNo = dto.VatNo ?? supplier.VatNo;
                supplier.BankName = dto.BankName ?? supplier.BankName;
                supplier.BankAccountNo = dto.BankAccountNo ?? supplier.BankAccountNo;
                supplier.BankAccountName = dto.BankAccountName ?? supplier.BankAccountName;
                supplier.BankBranch = dto.BankBranch ?? supplier.BankBranch;
                supplier.GlAccountNo = dto.GlAccountNo ?? supplier.GlAccountNo;
                supplier.GlAccountName = dto.GlAccountName ?? supplier.GlAccountName;
                supplier.IsActive = dto.IsActive ?? supplier.IsActive;
                supplier.Status = dto.IsActive == true ? "Active" : "Inactive";
                supplier.OpeningBalance = dto.OpeningBalance ?? supplier.OpeningBalance;
                supplier.Remarks = dto.Remarks ?? supplier.Remarks;
                supplier.AuditId = updatedBy;
                supplier.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    SupplierId = supplier.Id,
                    SupplierCode = supplier.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    IsActive = supplier.IsActive,
                    UpdatedBy = updatedBy,
                    UpdatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUPPLIER_UPDATED",
                    MemberNo = null,
                    CompanyCode = supplier.CompanyCode,
                    Amount = supplier.OpeningBalance ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = supplier.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                supplier.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: oldSupplier,
                    newModel: supplier,
                    tableName: "Suppliers",
                    recordId: supplier.Id.ToString(),
                    userId: updatedBy,
                    userName: updatedBy,
                    companyCode: supplier.CompanyCode,
                    module: "SupplierManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return MapToResponseDTO(supplier);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating supplier with ID: {id}");
                throw;
            }
        }

        public async Task<bool> DeleteSupplierAsync(long id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Invoices)
                    .Include(s => s.Payments)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                {
                    throw new InvalidOperationException($"Supplier with ID {id} not found");
                }

                // Check if supplier has invoices
                if (supplier.Invoices.Any())
                {
                    throw new InvalidOperationException($"Cannot delete supplier '{supplier.SupplierName}' because they have {supplier.Invoices.Count} invoice(s).");
                }

                var supplierForAudit = new
                {
                    supplier.Id,
                    supplier.SupplierCode,
                    supplier.SupplierName,
                    supplier.PhoneNo,
                    supplier.Email,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    SupplierId = supplier.Id,
                    SupplierCode = supplier.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUPPLIER_DELETED",
                    MemberNo = null,
                    CompanyCode = supplier.CompanyCode,
                    Amount = supplier.OpeningBalance ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = supplier.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: supplierForAudit,
                    newModel: null,
                    tableName: "Suppliers",
                    recordId: supplier.Id.ToString(),
                    userId: deletedBy,
                    userName: deletedBy,
                    companyCode: supplier.CompanyCode,
                    module: "SupplierManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting supplier with ID: {id}");
                throw;
            }
        }

        public async Task<SupplierResponseDTO> GetSupplierByIdAsync(long id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Invoices)
                .FirstOrDefaultAsync(s => s.Id == id);

            return supplier != null ? MapToResponseDTO(supplier) : null;
        }

        public async Task<List<SupplierResponseDTO>> GetAllSuppliersAsync(string companyCode)
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode)
                .OrderBy(s => s.SupplierName)
                .Include(s => s.Invoices)
                .ToListAsync();

            return suppliers.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<SupplierResponseDTO>> SearchSuppliersAsync(SupplierSearchDTO searchDto)
        {
            // FIX: Remove .Include() and use a separate query for Invoices
            var query = _context.Suppliers
                .Where(s => s.CompanyCode == searchDto.CompanyCode);

            if (!string.IsNullOrEmpty(searchDto.SupplierName))
            {
                query = query.Where(s => s.SupplierName.Contains(searchDto.SupplierName));
            }

            if (!string.IsNullOrEmpty(searchDto.SupplierCode))
            {
                query = query.Where(s => s.SupplierCode.Contains(searchDto.SupplierCode));
            }

            if (!string.IsNullOrEmpty(searchDto.PhoneNo))
            {
                query = query.Where(s => s.PhoneNo.Contains(searchDto.PhoneNo));
            }

            if (!string.IsNullOrEmpty(searchDto.Email))
            {
                query = query.Where(s => s.Email.Contains(searchDto.Email));
            }

            if (searchDto.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == searchDto.IsActive);
            }

            var suppliers = await query
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            // Load invoices separately for each supplier
            foreach (var supplier in suppliers)
            {
                supplier.Invoices = await _context.InvoiceReceive
                    .Where(i => i.SupplierCode == supplier.SupplierCode && i.CompanyCode == searchDto.CompanyCode)
                    .ToListAsync();
            }

            return suppliers.Select(MapToResponseDTO).ToList();
        }

        public async Task<SupplierViewModel> GetSupplierDashboardAsync(string companyCode)
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode)
                .Include(s => s.Invoices)
                .ToListAsync();

            var viewModel = new SupplierViewModel
            {
                Suppliers = suppliers.Select(MapToResponseDTO).ToList(),
                TotalSuppliers = suppliers.Count,
                ActiveSuppliers = suppliers.Count(s => s.IsActive == true),
                InactiveSuppliers = suppliers.Count(s => s.IsActive != true),
                BlockchainVerifiedCount = suppliers.Count(s => !string.IsNullOrEmpty(s.BlockchainTxId)),
                TotalSupplierBalance = suppliers.Sum(s => s.CurrentBalance ?? 0),
                UserCompanyCode = companyCode
            };

            return viewModel;
        }

        public async Task<decimal> GetSupplierOutstandingBalanceAsync(string supplierCode, string companyCode)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierCode == supplierCode && s.CompanyCode == companyCode);

            return supplier?.CurrentBalance ?? 0;
        }

        #region Helper Methods

        private async Task<string> GenerateSupplierCodeAsync(string companyCode)
        {
            var prefix = "SUP";
            var year = DateTime.Now.Year.ToString().Substring(2);
            var month = DateTime.Now.Month.ToString("D2");

            var lastSupplier = await _context.Suppliers
                .Where(s => s.CompanyCode == companyCode && s.SupplierCode.StartsWith($"{prefix}{year}{month}"))
                .OrderByDescending(s => s.SupplierCode)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastSupplier != null && lastSupplier.SupplierCode.Length >= 9)
            {
                var seqPart = lastSupplier.SupplierCode.Substring(7);
                if (int.TryParse(seqPart, out int lastSeq))
                {
                    sequence = lastSeq + 1;
                }
            }

            return $"{prefix}{year}{month}{sequence:D4}";
        }

        private SupplierResponseDTO MapToResponseDTO(Supplier supplier)
        {
            var invoiceCount = supplier.Invoices?.Count ?? 0;
            var totalInvoiceAmount = supplier.Invoices?.Sum(i => i.InvoiceAmount) ?? 0;

            return new SupplierResponseDTO
            {
                Id = supplier.Id,
                SupplierCode = supplier.SupplierCode,
                SupplierName = supplier.SupplierName,
                ContactPerson = supplier.ContactPerson,
                PhoneNo = supplier.PhoneNo,
                Email = supplier.Email,
                PhysicalAddress = supplier.PhysicalAddress,
                PostalAddress = supplier.PostalAddress,
                City = supplier.City,
                County = supplier.County,
                Country = supplier.Country,
                PinNo = supplier.PinNo,
                VatNo = supplier.VatNo,
                BankName = supplier.BankName,
                BankAccountNo = supplier.BankAccountNo,
                BankAccountName = supplier.BankAccountName,
                BankBranch = supplier.BankBranch,
                GlAccountNo = supplier.GlAccountNo,
                GlAccountName = supplier.GlAccountName,
                CompanyCode = supplier.CompanyCode,
                IsActive = supplier.IsActive,
                Status = supplier.Status,
                OpeningBalance = supplier.OpeningBalance,
                CurrentBalance = supplier.CurrentBalance,
                Remarks = supplier.Remarks,
                BlockchainTxId = supplier.BlockchainTxId,
                CreatedBy = supplier.AuditId,
                CreatedDate = supplier.AuditTime?.ToString("dd/MM/yyyy HH:mm"),
                InvoiceCount = invoiceCount,
                TotalInvoiceAmount = totalInvoiceAmount
            };
        }

        #endregion
    }
}