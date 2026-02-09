// Services/ShareTypeService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Services
{
    public class ShareTypeService : IShareTypeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ShareTypeService> _logger;
        private readonly IBlockchainService _blockchainService;

        public ShareTypeService(
            ApplicationDbContext context,
            ILogger<ShareTypeService> logger,
            IBlockchainService blockchainService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
        }

        public async Task<ShareTypeResponseDTO> CreateShareTypeAsync(ShareTypeCreateDTO shareTypeDto)
        {
            _logger.LogInformation($"Creating share type: {shareTypeDto.SharesCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate DTO
                await ValidateShareTypeAsync(shareTypeDto);

                // Check if share type already exists
                var existingShareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.SharesCode == shareTypeDto.SharesCode &&
                                              st.CompanyCode == shareTypeDto.CompanyCode);

                if (existingShareType != null)
                {
                    throw new ValidationException($"Share type with code '{shareTypeDto.SharesCode}' already exists");
                }

                // Create new share type
                var shareType = new Sharetype
                {
                    SharesCode = shareTypeDto.SharesCode,
                    SharesType = shareTypeDto.SharesType,
                    SharesAcc = shareTypeDto.SharesAcc,
                    ContraAcc = shareTypeDto.ContraAcc,
                    PlacePeriod = shareTypeDto.PlacePeriod,
                    LoanToShareRatio = shareTypeDto.LoanToShareRatio,
                    Issharecapital = shareTypeDto.Issharecapital,
                    Interest = shareTypeDto.Interest,
                    MaxAmount = shareTypeDto.MaxAmount,
                    Guarantor = shareTypeDto.Guarantor,
                    CompanyCode = shareTypeDto.CompanyCode,
                    IsMainShares = shareTypeDto.IsMainShares,
                    UsedToGuarantee = shareTypeDto.UsedToGuarantee,
                    UsedToOffset = shareTypeDto.UsedToOffset,
                    Withdrawable = shareTypeDto.Withdrawable,
                    Loanquaranto = shareTypeDto.Loanquaranto,
                    Priority = shareTypeDto.Priority,
                    MinAmount = shareTypeDto.MinAmount,
                    Ppacc = shareTypeDto.Ppacc,
                    LowerLimit = shareTypeDto.LowerLimit,
                    ElseRatio = shareTypeDto.ElseRatio,
                    AuditId = shareTypeDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                _context.Sharetypes.Add(shareType);
                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    ShareTypeCode = shareType.SharesCode,
                    ShareTypeName = shareType.SharesType,
                    CompanyCode = shareType.CompanyCode,
                    CreatedBy = shareTypeDto.CreatedBy,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Properties = new
                    {
                        shareType.IsMainShares,
                        shareType.MinAmount,
                        shareType.MaxAmount,
                        shareType.Withdrawable,
                        shareType.UsedToGuarantee,
                        shareType.Priority
                    }
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "SHARE_TYPE_CREATE",
                    "SYSTEM",
                    shareTypeDto.CompanyCode,
                    0,
                    shareType.SharesCode,
                    blockchainData
                );

                if (blockchainTx != null)
                {
                    // You might want to store blockchain transaction ID somewhere
                    _logger.LogInformation($"Blockchain transaction created: {blockchainTx.TransactionId}");
                }

                await transaction.CommitAsync();
                _logger.LogInformation($"Share type {shareType.SharesCode} created successfully");

                // Return response DTO
                return await GetShareTypeResponseDto(shareType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating share type {shareTypeDto.SharesCode}");
                throw;
            }
        }

        public async Task<ShareTypeResponseDTO> UpdateShareTypeAsync(string sharesCode, ShareTypeUpdateDTO shareTypeDto)
        {
            _logger.LogInformation($"Updating share type: {sharesCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get existing share type
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.SharesCode == sharesCode &&
                                              st.CompanyCode == shareTypeDto.CompanyCode);

                if (shareType == null)
                {
                    throw new KeyNotFoundException($"Share type '{sharesCode}' not found");
                }

                // Check if share type is in use (except for some fields)
                var usageCount = await GetShareTypeUsageCountAsync(sharesCode, shareTypeDto.CompanyCode);
                if (usageCount > 0)
                {
                    // Validate that critical fields aren't being changed when in use
                    if (shareType.IsMainShares != shareTypeDto.IsMainShares ||
                        shareType.MinAmount != shareTypeDto.MinAmount ||
                        shareType.Withdrawable != shareTypeDto.Withdrawable)
                    {
                        throw new ValidationException(
                            "Cannot change critical properties when share type is in use by members");
                    }
                }

                // Update fields
                shareType.SharesType = shareTypeDto.SharesType;
                shareType.SharesAcc = shareTypeDto.SharesAcc;
                shareType.ContraAcc = shareTypeDto.ContraAcc;
                shareType.PlacePeriod = shareTypeDto.PlacePeriod;
                shareType.LoanToShareRatio = shareTypeDto.LoanToShareRatio;
                shareType.Issharecapital = shareTypeDto.Issharecapital;
                shareType.Interest = shareTypeDto.Interest;
                shareType.MaxAmount = shareTypeDto.MaxAmount;
                shareType.Guarantor = shareTypeDto.Guarantor;
                shareType.UsedToGuarantee = shareTypeDto.UsedToGuarantee;
                shareType.UsedToOffset = shareTypeDto.UsedToOffset;
                shareType.Loanquaranto = shareTypeDto.Loanquaranto;
                shareType.Priority = shareTypeDto.Priority;
                shareType.Ppacc = shareTypeDto.Ppacc;
                shareType.LowerLimit = shareTypeDto.LowerLimit;
                shareType.ElseRatio = shareTypeDto.ElseRatio;
                shareType.AuditId = shareTypeDto.CreatedBy;
                shareType.AuditTime = DateTime.Now;
                shareType.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain transaction for update
                var blockchainData = new
                {
                    ShareTypeCode = shareType.SharesCode,
                    ShareTypeName = shareType.SharesType,
                    CompanyCode = shareType.CompanyCode,
                    UpdatedBy = shareTypeDto.CreatedBy,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UpdatedFields = new
                    {
                        OldName = shareType.SharesType,
                        NewName = shareTypeDto.SharesType,
                        OldMaxAmount = shareType.MaxAmount,
                        NewMaxAmount = shareTypeDto.MaxAmount
                    }
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "SHARE_TYPE_UPDATE",
                    "SYSTEM",
                    shareTypeDto.CompanyCode,
                    0,
                    shareType.SharesCode,
                    blockchainData
                );

                await transaction.CommitAsync();
                _logger.LogInformation($"Share type {sharesCode} updated successfully");

                return await GetShareTypeResponseDto(shareType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating share type {sharesCode}");
                throw;
            }
        }

        public async Task<bool> DeleteShareTypeAsync(string sharesCode, string companyCode)
        {
            _logger.LogInformation($"Deleting share type: {sharesCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.SharesCode == sharesCode &&
                                              st.CompanyCode == companyCode);

                if (shareType == null)
                {
                    throw new KeyNotFoundException($"Share type '{sharesCode}' not found");
                }

                // Check if share type is in use
                var usageCount = await GetShareTypeUsageCountAsync(sharesCode, companyCode);
                if (usageCount > 0)
                {
                    throw new ValidationException(
                        $"Cannot delete share type '{sharesCode}' because it's used by {usageCount} member(s)");
                }

                // Create blockchain transaction before deletion
                var blockchainData = new
                {
                    ShareTypeCode = shareType.SharesCode,
                    ShareTypeName = shareType.SharesType,
                    CompanyCode = companyCode,
                    DeletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DeletedBy = "SYSTEM" // In real app, get from current user
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "SHARE_TYPE_DELETE",
                    "SYSTEM",
                    companyCode,
                    0,
                    sharesCode,
                    blockchainData
                );

                // Delete from database
                _context.Sharetypes.Remove(shareType);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation($"Share type {sharesCode} deleted successfully");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting share type {sharesCode}");
                throw;
            }
        }

        public async Task<ShareTypeResponseDTO> GetShareTypeByCodeAsync(string sharesCode, string companyCode)
        {
            var shareType = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == sharesCode &&
                                          st.CompanyCode == companyCode);

            if (shareType == null)
            {
                throw new KeyNotFoundException($"Share type '{sharesCode}' not found");
            }

            return await GetShareTypeResponseDto(shareType);
        }

        public async Task<List<ShareTypeResponseDTO>> GetShareTypesByCompanyAsync(string companyCode)
        {
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesType)
                .ToListAsync();

            var result = new List<ShareTypeResponseDTO>();
            foreach (var shareType in shareTypes)
            {
                result.Add(await GetShareTypeResponseDto(shareType));
            }

            return result;
        }

        public async Task<List<ShareTypeSimpleDTO>> GetActiveShareTypesAsync(string companyCode)
        {
            return await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .Select(st => new ShareTypeSimpleDTO
                {
                    SharesCode = st.SharesCode,
                    SharesType = st.SharesType,
                    IsMainShares = st.IsMainShares,
                    MinAmount = st.MinAmount,
                    MaxAmount = st.MaxAmount,
                    UsedToGuarantee = st.UsedToGuarantee,
                    Withdrawable = st.Withdrawable,
                    Priority = st.Priority
                })
                .ToListAsync();
        }

        public async Task<List<ShareTypeResponseDTO>> SearchShareTypesAsync(string searchTerm, string companyCode)
        {
            var query = _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(st =>
                    st.SharesCode.Contains(searchTerm) ||
                    st.SharesType.Contains(searchTerm) ||
                    st.SharesAcc.Contains(searchTerm));
            }

            var shareTypes = await query
                .OrderBy(st => st.Priority)
                .ToListAsync();

            var result = new List<ShareTypeResponseDTO>();
            foreach (var shareType in shareTypes)
            {
                result.Add(await GetShareTypeResponseDto(shareType));
            }

            return result;
        }

        public async Task<bool> ValidateShareTypeAsync(ShareTypeCreateDTO shareTypeDto)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(shareTypeDto.SharesCode))
                throw new ValidationException("Share code is required");

            if (string.IsNullOrWhiteSpace(shareTypeDto.SharesType))
                throw new ValidationException("Share type name is required");

            if (string.IsNullOrWhiteSpace(shareTypeDto.SharesAcc))
                throw new ValidationException("Share account is required");

            if (string.IsNullOrWhiteSpace(shareTypeDto.Ppacc))
                throw new ValidationException("PP Account is required");

            if (shareTypeDto.MinAmount < 0)
                throw new ValidationException("Minimum amount cannot be negative");

            if (shareTypeDto.MaxAmount.HasValue && shareTypeDto.MaxAmount < shareTypeDto.MinAmount)
                throw new ValidationException("Maximum amount cannot be less than minimum amount");

            if (shareTypeDto.Priority < 1 || shareTypeDto.Priority > 10)
                throw new ValidationException("Priority must be between 1 and 10");

            // Check for duplicate share code in the same company
            var existing = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == shareTypeDto.SharesCode &&
                                          st.CompanyCode == shareTypeDto.CompanyCode);
            if (existing != null)
            {
                throw new ValidationException($"Share code '{shareTypeDto.SharesCode}' already exists");
            }

            return true;
        }

        public async Task<int> GetShareTypeUsageCountAsync(string sharesCode, string companyCode)
        {
            // Count members using this share type
            var memberCount = await _context.Shares
                .CountAsync(s => s.Sharescode == sharesCode &&
                               s.CompanyCode == companyCode);

            // Count contributions using this share type
            var contribCount = await _context.Contribs
                .CountAsync(c => c.Sharescode == sharesCode &&
                               c.CompanyCode == companyCode);

            return memberCount + contribCount;
        }

        private async Task<ShareTypeResponseDTO> GetShareTypeResponseDto(Sharetype shareType)
        {
            // Get usage statistics
            var totalMembers = await _context.Shares
                .CountAsync(s => s.Sharescode == shareType.SharesCode &&
                               s.CompanyCode == shareType.CompanyCode);

            var totalShares = await _context.Shares
                .Where(s => s.Sharescode == shareType.SharesCode &&
                          s.CompanyCode == shareType.CompanyCode)
                .SumAsync(s => s.TotalShares);

            return new ShareTypeResponseDTO
            {
                SharesCode = shareType.SharesCode,
                SharesType = shareType.SharesType,
                SharesAcc = shareType.SharesAcc,
                ContraAcc = shareType.ContraAcc,
                PlacePeriod = shareType.PlacePeriod,
                LoanToShareRatio = shareType.LoanToShareRatio,
                Issharecapital = shareType.Issharecapital,
                Interest = shareType.Interest,
                MaxAmount = shareType.MaxAmount,
                Guarantor = shareType.Guarantor,
                IsMainShares = shareType.IsMainShares,
                UsedToGuarantee = shareType.UsedToGuarantee,
                UsedToOffset = shareType.UsedToOffset,
                Withdrawable = shareType.Withdrawable,
                Loanquaranto = shareType.Loanquaranto,
                Priority = shareType.Priority,
                MinAmount = shareType.MinAmount,
                Ppacc = shareType.Ppacc,
                LowerLimit = shareType.LowerLimit,
                ElseRatio = shareType.ElseRatio,
                CompanyCode = shareType.CompanyCode,
                CreatedAt = shareType.AuditDateTime,
                CreatedBy = shareType.AuditId,
                LastUpdated = shareType.AuditDateTime,
                TotalMembers = totalMembers,
                TotalShares = (decimal)totalShares
            };
        }
    }
}