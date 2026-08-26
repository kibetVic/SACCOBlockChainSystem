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
    public interface IAssetsRegisterService
    {
        Task<AssetsRegisterResponseDTO> CreateAssetAsync(AssetsRegisterDTO dto, string createdBy);
        Task<AssetsRegisterResponseDTO> UpdateAssetAsync(long id, AssetsRegisterDTO dto, string updatedBy);
        Task<bool> DeleteAssetAsync(long id, string deletedBy);
        Task<AssetsRegisterResponseDTO> GetAssetByIdAsync(long id);
        Task<List<AssetsRegisterResponseDTO>> GetAllAssetsAsync(string companyCode);
        Task<List<AssetsRegisterResponseDTO>> SearchAssetsAsync(AssetsRegisterSearchDTO searchDto);
        Task<List<AssetsRegisterResponseDTO>> GetAssetsByTypeAsync(string assetType, string companyCode);
        Task<decimal> GetTotalAssetValueAsync(string companyCode);
        Task<bool> PostAssetAsync(long id, string postedBy);
    }
    public class AssetsRegisterService : IAssetsRegisterService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AssetsRegisterService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly AuditTrailService _auditService;

        public AssetsRegisterService(
            ApplicationDbContext context,
            ILogger<AssetsRegisterService> logger,
            IBlockchainService blockchainService,
            AuditTrailService auditService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _auditService = auditService;
        }

        public async Task<AssetsRegisterResponseDTO> CreateAssetAsync(AssetsRegisterDTO dto, string createdBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating new asset: {dto.AssetName}");

                // Calculate total value if not provided
                decimal? totalValue = dto.TotalValue;
                if (!totalValue.HasValue && dto.Quantity.HasValue && dto.ActualValue.HasValue)
                {
                    totalValue = dto.Quantity.Value * dto.ActualValue.Value;
                }

                var asset = new AssetsRegister
                {
                    Class = dto.Class,
                    AssetType = dto.AssetType,
                    AssetName = dto.AssetName,
                    TagNo = dto.TagNo,
                    SerialNo = dto.SerialNo,
                    Quantity = dto.Quantity,
                    ActualValue = dto.ActualValue,
                    MarketValue = dto.MarketValue,
                    TotalValue = totalValue ?? dto.ActualValue,
                    DateOfManufacture = dto.DateOfManufacture,
                    DatePurchased = dto.DatePurchased,
                    TransactionNo = dto.TransactionNo ?? $"AST-{DateTime.Now:yyyyMMddHHmmss}",
                    CompanyCode = dto.CompanyCode,
                    Location = dto.Location,
                    Posted = dto.Posted ?? true,
                    AuditId = createdBy,
                    AuditTime = DateTime.Now
                };

                _context.AssetsRegister.Add(asset);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                var blockchainData = new
                {
                    AssetId = asset.Id,
                    AssetName = asset.AssetName,
                    AssetType = asset.AssetType,
                    Class = asset.Class,
                    TagNo = asset.TagNo,
                    SerialNo = asset.SerialNo,
                    Quantity = asset.Quantity,
                    ActualValue = asset.ActualValue,
                    MarketValue = asset.MarketValue,
                    TotalValue = asset.TotalValue,
                    Location = asset.Location,
                    DatePurchased = asset.DatePurchased,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "ASSET_REGISTERED",
                    MemberNo = null,
                    CompanyCode = dto.CompanyCode,
                    Amount = asset.TotalValue ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = asset.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                asset.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // Save audit trail
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: asset,
                    tableName: "AssetsRegister",
                    recordId: asset.Id.ToString(),
                    userId: createdBy,
                    userName: createdBy,
                    companyCode: dto.CompanyCode,
                    module: "AssetManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Asset {asset.AssetName} created successfully with ID: {asset.Id}");

                return MapToResponseDTO(asset);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating asset: {dto.AssetName}");
                throw;
            }
        }

        public async Task<AssetsRegisterResponseDTO> UpdateAssetAsync(long id, AssetsRegisterDTO dto, string updatedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating asset with ID: {id}");

                var asset = await _context.AssetsRegister.FindAsync(id);
                if (asset == null)
                {
                    throw new InvalidOperationException($"Asset with ID {id} not found");
                }

                // Store old values for audit
                var oldAsset = new
                {
                    asset.Class,
                    asset.AssetType,
                    asset.AssetName,
                    asset.TagNo,
                    asset.SerialNo,
                    asset.Quantity,
                    asset.ActualValue,
                    asset.MarketValue,
                    asset.TotalValue,
                    asset.Location,
                    asset.DatePurchased,
                    asset.Posted
                };

                // Update asset
                asset.Class = dto.Class ?? asset.Class;
                asset.AssetType = dto.AssetType ?? asset.AssetType;
                asset.AssetName = dto.AssetName ?? asset.AssetName;
                asset.TagNo = dto.TagNo ?? asset.TagNo;
                asset.SerialNo = dto.SerialNo ?? asset.SerialNo;
                asset.Quantity = dto.Quantity ?? asset.Quantity;
                asset.ActualValue = dto.ActualValue ?? asset.ActualValue;
                asset.MarketValue = dto.MarketValue ?? asset.MarketValue;
                asset.Location = dto.Location ?? asset.Location;
                asset.DatePurchased = dto.DatePurchased ?? asset.DatePurchased;
                asset.DateOfManufacture = dto.DateOfManufacture ?? asset.DateOfManufacture;
                asset.Posted = dto.Posted ?? asset.Posted;
                asset.AuditId = updatedBy;
                asset.AuditTime = DateTime.Now;

                // Recalculate total value if needed
                if (dto.Quantity.HasValue && dto.ActualValue.HasValue)
                {
                    asset.TotalValue = dto.Quantity.Value * dto.ActualValue.Value;
                }
                else if (dto.TotalValue.HasValue)
                {
                    asset.TotalValue = dto.TotalValue;
                }

                await _context.SaveChangesAsync();

                // Record blockchain transaction
                var blockchainData = new
                {
                    AssetId = asset.Id,
                    AssetName = asset.AssetName,
                    AssetType = asset.AssetType,
                    Class = asset.Class,
                    TagNo = asset.TagNo,
                    SerialNo = asset.SerialNo,
                    Quantity = asset.Quantity,
                    ActualValue = asset.ActualValue,
                    MarketValue = asset.MarketValue,
                    TotalValue = asset.TotalValue,
                    Location = asset.Location,
                    UpdatedBy = updatedBy,
                    UpdatedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "ASSET_UPDATED",
                    MemberNo = null,
                    CompanyCode = asset.CompanyCode,
                    Amount = asset.TotalValue ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = asset.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                asset.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // Save audit trail
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: oldAsset,
                    newModel: asset,
                    tableName: "AssetsRegister",
                    recordId: asset.Id.ToString(),
                    userId: updatedBy,
                    userName: updatedBy,
                    companyCode: asset.CompanyCode,
                    module: "AssetManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Asset {asset.AssetName} updated successfully");

                return MapToResponseDTO(asset);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating asset with ID: {id}");
                throw;
            }
        }

        public async Task<bool> DeleteAssetAsync(long id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Deleting asset with ID: {id}");

                var asset = await _context.AssetsRegister.FindAsync(id);
                if (asset == null)
                {
                    throw new InvalidOperationException($"Asset with ID {id} not found");
                }

                // Store for audit before deletion
                var assetForAudit = new
                {
                    asset.Id,
                    asset.AssetName,
                    asset.AssetType,
                    asset.Class,
                    asset.TagNo,
                    asset.SerialNo,
                    asset.Quantity,
                    asset.ActualValue,
                    asset.TotalValue,
                    asset.Location,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                _context.AssetsRegister.Remove(asset);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                var blockchainData = new
                {
                    AssetId = asset.Id,
                    AssetName = asset.AssetName,
                    AssetType = asset.AssetType,
                    DeletedBy = deletedBy,
                    DeletedDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "ASSET_DELETED",
                    MemberNo = null,
                    CompanyCode = asset.CompanyCode,
                    Amount = asset.TotalValue ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = asset.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Save audit trail
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: assetForAudit,
                    newModel: null,
                    tableName: "AssetsRegister",
                    recordId: asset.Id.ToString(),
                    userId: deletedBy,
                    userName: deletedBy,
                    companyCode: asset.CompanyCode,
                    module: "AssetManagement",
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Asset {asset.AssetName} deleted successfully");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting asset with ID: {id}");
                throw;
            }
        }

        public async Task<AssetsRegisterResponseDTO> GetAssetByIdAsync(long id)
        {
            var asset = await _context.AssetsRegister.FindAsync(id);
            if (asset == null)
            {
                return null;
            }

            return MapToResponseDTO(asset);
        }

        public async Task<List<AssetsRegisterResponseDTO>> GetAllAssetsAsync(string companyCode)
        {
            var assets = await _context.AssetsRegister
                .Where(a => a.CompanyCode == companyCode)
                .OrderByDescending(a => a.AuditTime)
                .ToListAsync();

            return assets.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<AssetsRegisterResponseDTO>> SearchAssetsAsync(AssetsRegisterSearchDTO searchDto)
        {
            var query = _context.AssetsRegister
                .Where(a => a.CompanyCode == searchDto.CompanyCode);

            if (!string.IsNullOrEmpty(searchDto.AssetName))
            {
                query = query.Where(a => a.AssetName.Contains(searchDto.AssetName));
            }

            if (!string.IsNullOrEmpty(searchDto.AssetType))
            {
                query = query.Where(a => a.AssetType.Contains(searchDto.AssetType));
            }

            if (!string.IsNullOrEmpty(searchDto.Class))
            {
                query = query.Where(a => a.Class.Contains(searchDto.Class));
            }

            if (!string.IsNullOrEmpty(searchDto.TagNo))
            {
                query = query.Where(a => a.TagNo.Contains(searchDto.TagNo));
            }

            if (!string.IsNullOrEmpty(searchDto.SerialNo))
            {
                query = query.Where(a => a.SerialNo.Contains(searchDto.SerialNo));
            }

            if (!string.IsNullOrEmpty(searchDto.Location))
            {
                query = query.Where(a => a.Location.Contains(searchDto.Location));
            }

            if (searchDto.FromDate.HasValue)
            {
                query = query.Where(a => a.DatePurchased >= searchDto.FromDate.Value);
            }

            if (searchDto.ToDate.HasValue)
            {
                query = query.Where(a => a.DatePurchased <= searchDto.ToDate.Value);
            }

            var assets = await query
                .OrderByDescending(a => a.AuditTime)
                .ToListAsync();

            return assets.Select(MapToResponseDTO).ToList();
        }

        public async Task<List<AssetsRegisterResponseDTO>> GetAssetsByTypeAsync(string assetType, string companyCode)
        {
            var assets = await _context.AssetsRegister
                .Where(a => a.AssetType == assetType && a.CompanyCode == companyCode)
                .OrderByDescending(a => a.AuditTime)
                .ToListAsync();

            return assets.Select(MapToResponseDTO).ToList();
        }

        public async Task<decimal> GetTotalAssetValueAsync(string companyCode)
        {
            return await _context.AssetsRegister
                .Where(a => a.CompanyCode == companyCode)
                .SumAsync(a => a.TotalValue ?? 0);
        }

        public async Task<bool> PostAssetAsync(long id, string postedBy)
        {
            try
            {
                var asset = await _context.AssetsRegister.FindAsync(id);
                if (asset == null)
                {
                    throw new InvalidOperationException($"Asset with ID {id} not found");
                }

                asset.Posted = true;
                asset.AuditId = postedBy;
                asset.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Asset {asset.AssetName} posted successfully");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting asset with ID: {id}");
                throw;
            }
        }

        private AssetsRegisterResponseDTO MapToResponseDTO(AssetsRegister asset)
        {
            return new AssetsRegisterResponseDTO
            {
                Id = asset.Id,
                Class = asset.Class,
                AssetType = asset.AssetType,
                AssetName = asset.AssetName,
                TagNo = asset.TagNo,
                SerialNo = asset.SerialNo,
                Quantity = asset.Quantity.HasValue ? (int)asset.Quantity.Value : (int?)null,
                ActualValue = asset.ActualValue,
                MarketValue = asset.MarketValue,
                TotalValue = asset.TotalValue,
                DateOfManufacture = asset.DateOfManufacture,
                DatePurchased = asset.DatePurchased,
                TransactionNo = asset.TransactionNo,
                CompanyCode = asset.CompanyCode,
                Location = asset.Location,
                Posted = asset.Posted,
                AuditId = asset.AuditId,
                AuditTime = asset.AuditTime,
                BlockchainTxId = asset.BlockchainTxId,
                CreatedBy = asset.AuditId,
                CreatedDate = asset.AuditTime?.ToString("dd/MM/yyyy HH:mm")
            };
        }
    }
}