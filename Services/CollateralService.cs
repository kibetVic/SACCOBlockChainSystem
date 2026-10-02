// Services/CollateralService.cs
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Colors = QuestPDF.Helpers.Colors;

// Use alias to resolve ambiguity
using MemberModel = SACCOBlockChainSystem.Models.Member;

namespace SACCOBlockChainSystem.Services
{
    public interface ICollateralService
    {
        Task<CollateralResponseDTO> CreateAsync(CollateralDTO dto, string userId);
        Task<CollateralResponseDTO> UpdateAsync(long id, CollateralDTO dto, string userId);
        Task<bool> DeleteAsync(long id, string userId);
        Task<CollateralResponseDTO> GetByIdAsync(long id);
        Task<List<CollateralResponseDTO>> GetAllAsync(string companyCode);
        Task<List<CollateralResponseDTO>> GetMemberCollateralsAsync(string memberNo, string companyCode);
        Task<string> GenerateColCodeAsync(string companyCode);
        Task<bool> IsColCodeUniqueAsync(string colCode, string companyCode, long? excludeId = null);
        Task<List<CollateralReportDTO>> GetCollateralReportAsync(string companyCode);
        Task<MemberModel?> GetMemberByMemberNoAsync(string memberNo, string companyCode);
        Task<List<MemberModel>> SearchMembersAsync(string searchTerm, string companyCode);
        Task<List<MemberCollateralDTO>> GetAvailableMemberCollateralsForGuaranteeAsync(string memberNo, string companyCode);
    }

    public class CollateralService : ICollateralService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<CollateralService> _logger;

        public CollateralService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<CollateralService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        public async Task<CollateralResponseDTO> CreateAsync(CollateralDTO dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate member exists
                if (!string.IsNullOrEmpty(dto.MemberNo))
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == dto.MemberNo && m.CompanyCode == dto.CompanyCode);

                    if (member == null)
                    {
                        throw new InvalidOperationException($"Member {dto.MemberNo} not found.");
                    }
                }

                // Check if code is unique
                if (!await IsColCodeUniqueAsync(dto.ColCode, dto.CompanyCode))
                {
                    throw new InvalidOperationException($"Collateral code {dto.ColCode} already exists.");
                }

                byte[]? photoBytes = null;
                string? photoContentType = null;

                // Process photo upload
                if (dto.PhotoFile != null && dto.PhotoFile.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await dto.PhotoFile.CopyToAsync(memoryStream);
                    photoBytes = memoryStream.ToArray();
                    photoContentType = dto.PhotoFile.ContentType;

                    _logger.LogInformation($"Photo uploaded for collateral {dto.ColCode}: {photoBytes.Length} bytes, {photoContentType}");
                }

                var collateral = new Collateral
                {
                    ColCode = dto.ColCode,
                    Coldescription = dto.Coldescription,
                    Percentage = dto.Percentage,
                    MemberNo = !string.IsNullOrEmpty(dto.MemberNo) ? dto.MemberNo : null,
                    CompanyCode = dto.CompanyCode,
                    Photo = photoBytes,
                    PhotoContentType = photoContentType
                };

                _context.Collaterals.Add(collateral);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        Action = "CREATE",
                        CollateralId = collateral.Id,
                        ColCode = collateral.ColCode,
                        Coldescription = collateral.Coldescription,
                        Percentage = collateral.Percentage,
                        MemberNo = collateral.MemberNo,
                        HasPhoto = collateral.Photo != null,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "COLLATERAL_CREATE",
                        dto.MemberNo,
                        dto.CompanyCode,
                        (decimal)collateral.Percentage,
                        collateral.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        collateral.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for collateral");
                }

                await transaction.CommitAsync();

                // Get member name for response
                string? memberName = null;
                if (!string.IsNullOrEmpty(collateral.MemberNo))
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == collateral.MemberNo && m.CompanyCode == dto.CompanyCode);
                    if (member != null)
                    {
                        memberName = $"{member.Surname} {member.OtherNames}".Trim();
                    }
                }

                // Generate photo base64 for response
                string? photoBase64 = null;
                if (collateral.Photo != null && collateral.Photo.Length > 0)
                {
                    photoBase64 = Convert.ToBase64String(collateral.Photo);
                }

                return new CollateralResponseDTO
                {
                    Id = collateral.Id,
                    ColCode = collateral.ColCode,
                    Coldescription = collateral.Coldescription,
                    Percentage = collateral.Percentage,
                    MemberNo = collateral.MemberNo,
                    MemberName = memberName,
                    CompanyCode = collateral.CompanyCode,
                    BlockchainTxId = collateral.BlockchainTxId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId,
                    PhotoBase64 = photoBase64,
                    PhotoContentType = collateral.PhotoContentType,
                    HasPhoto = collateral.Photo != null && collateral.Photo.Length > 0
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating collateral");
                throw;
            }
        }

        public async Task<CollateralResponseDTO> UpdateAsync(long id, CollateralDTO dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var collateral = await _context.Collaterals.FindAsync(id);
                if (collateral == null)
                {
                    throw new InvalidOperationException($"Collateral with ID {id} not found.");
                }

                // Get CompanyCode from collateral if not provided in DTO
                var companyCode = !string.IsNullOrEmpty(dto.CompanyCode) ? dto.CompanyCode : collateral.CompanyCode;

                // Validate member exists if changed or if new member is provided
                if (!string.IsNullOrEmpty(dto.MemberNo))
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == dto.MemberNo && m.CompanyCode == companyCode);

                    if (member == null)
                    {
                        throw new InvalidOperationException($"Member {dto.MemberNo} not found in company {companyCode}.");
                    }
                }

                // Store old values for blockchain
                var oldValues = new
                {
                    collateral.ColCode,
                    collateral.Coldescription,
                    collateral.Percentage,
                    collateral.MemberNo,
                    HasPhoto = collateral.Photo != null
                };

                // Process photo upload (replace existing photo if new one is provided)
                byte[]? photoBytes = null;
                string? photoContentType = null;

                if (dto.PhotoFile != null && dto.PhotoFile.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await dto.PhotoFile.CopyToAsync(memoryStream);
                    photoBytes = memoryStream.ToArray();
                    photoContentType = dto.PhotoFile.ContentType;
                    _logger.LogInformation($"Photo updated for collateral {dto.ColCode}: {photoBytes.Length} bytes");
                }

                // Update collateral
                collateral.ColCode = dto.ColCode;
                collateral.Coldescription = dto.Coldescription;
                collateral.Percentage = dto.Percentage;
                collateral.MemberNo = !string.IsNullOrEmpty(dto.MemberNo) ? dto.MemberNo : null;
                if (!string.IsNullOrEmpty(companyCode))
                {
                    collateral.CompanyCode = companyCode;
                }

                // Only update photo if a new one was uploaded
                if (photoBytes != null && photoBytes.Length > 0)
                {
                    collateral.Photo = photoBytes;
                    collateral.PhotoContentType = photoContentType;
                }

                await _context.SaveChangesAsync();

                // Record blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        Action = "UPDATE",
                        CollateralId = collateral.Id,
                        OldValues = oldValues,
                        NewValues = new
                        {
                            collateral.ColCode,
                            collateral.Coldescription,
                            collateral.Percentage,
                            collateral.MemberNo,
                            HasPhoto = collateral.Photo != null
                        },
                        ModifiedBy = userId,
                        ModifiedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "COLLATERAL_UPDATE",
                        collateral.MemberNo,
                        collateral.CompanyCode,
                        (decimal)collateral.Percentage,
                        collateral.Id.ToString(),
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        collateral.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for collateral update");
                }

                await transaction.CommitAsync();

                // Get member name
                string? memberName = null;
                if (!string.IsNullOrEmpty(collateral.MemberNo))
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == collateral.MemberNo && m.CompanyCode == collateral.CompanyCode);
                    if (member != null)
                    {
                        memberName = $"{member.Surname} {member.OtherNames}".Trim();
                    }
                }

                // Generate photo base64 for response
                string? photoBase64 = null;
                if (collateral.Photo != null && collateral.Photo.Length > 0)
                {
                    photoBase64 = Convert.ToBase64String(collateral.Photo);
                }

                return new CollateralResponseDTO
                {
                    Id = collateral.Id,
                    ColCode = collateral.ColCode,
                    Coldescription = collateral.Coldescription,
                    Percentage = collateral.Percentage,
                    MemberNo = collateral.MemberNo,
                    MemberName = memberName,
                    CompanyCode = collateral.CompanyCode,
                    BlockchainTxId = collateral.BlockchainTxId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId,
                    PhotoBase64 = photoBase64,
                    PhotoContentType = collateral.PhotoContentType,
                    HasPhoto = collateral.Photo != null && collateral.Photo.Length > 0
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating collateral");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(long id, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var collateral = await _context.Collaterals.FindAsync(id);
                if (collateral == null)
                {
                    throw new InvalidOperationException($"Collateral with ID {id} not found.");
                }

                // Check if collateral is linked to any loans
                try
                {
                    var linkedLoans = await _context.ColloanGuars
                        .Where(lg => lg.ColCode == collateral.ColCode)
                        .AnyAsync();

                    if (linkedLoans)
                    {
                        throw new InvalidOperationException("Cannot delete collateral because it is linked to one or more loans.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check loan links - ColloanGuar table may not exist yet");
                }

                var collateralDetails = new
                {
                    collateral.Id,
                    collateral.ColCode,
                    collateral.Coldescription,
                    collateral.Percentage,
                    collateral.MemberNo
                };

                _context.Collaterals.Remove(collateral);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        Action = "DELETE",
                        CollateralDetails = collateralDetails,
                        DeletedBy = userId,
                        DeletedAt = DateTime.Now
                    };

                    await _blockchainService.CreateAndAddTransactionAsync(
                        "COLLATERAL_DELETE",
                        collateral.MemberNo,
                        collateral.CompanyCode,
                        (decimal)collateral.Percentage,
                        collateral.Id.ToString(),
                        blockchainData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for collateral deletion");
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting collateral");
                throw;
            }
        }

        public async Task<CollateralResponseDTO> GetByIdAsync(long id)
        {
            try
            {
                var collateral = await _context.Collaterals
                    .Include(c => c.Member)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (collateral == null) return null;

                string? memberName = null;
                if (collateral.Member != null)
                {
                    memberName = $"{collateral.Member.Surname} {collateral.Member.OtherNames}".Trim();
                }

                string? photoBase64 = null;
                if (collateral.Photo != null && collateral.Photo.Length > 0)
                {
                    photoBase64 = Convert.ToBase64String(collateral.Photo);
                }

                return new CollateralResponseDTO
                {
                    Id = collateral.Id,
                    ColCode = collateral.ColCode,
                    Coldescription = collateral.Coldescription,
                    Percentage = collateral.Percentage,
                    MemberNo = collateral.MemberNo,
                    MemberName = memberName,
                    CompanyCode = collateral.CompanyCode,
                    BlockchainTxId = collateral.BlockchainTxId,
                    PhotoBase64 = photoBase64,
                    PhotoContentType = collateral.PhotoContentType,
                    HasPhoto = collateral.Photo != null && collateral.Photo.Length > 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting collateral by ID {id}");
                throw;
            }
        }

        public async Task<List<CollateralResponseDTO>> GetAllAsync(string companyCode)
        {
            try
            {
                var collaterals = await _context.Collaterals
                    .Include(c => c.Member)
                    .Where(c => c.CompanyCode == companyCode)
                    .OrderByDescending(c => c.Id)
                    .ToListAsync();

                return collaterals.Select(c => new CollateralResponseDTO
                {
                    Id = c.Id,
                    ColCode = c.ColCode,
                    Coldescription = c.Coldescription,
                    Percentage = c.Percentage,
                    MemberNo = c.MemberNo,
                    MemberName = c.Member != null ? $"{c.Member.Surname} {c.Member.OtherNames}".Trim() : null,
                    CompanyCode = c.CompanyCode,
                    BlockchainTxId = c.BlockchainTxId,
                    PhotoBase64 = c.Photo != null && c.Photo.Length > 0 ? Convert.ToBase64String(c.Photo) : null,
                    PhotoContentType = c.PhotoContentType,
                    HasPhoto = c.Photo != null && c.Photo.Length > 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all collaterals");
                throw;
            }
        }

        // NEW: Get collaterals for a specific member only
        public async Task<List<CollateralResponseDTO>> GetMemberCollateralsAsync(string memberNo, string companyCode)
        {
            try
            {
                var collaterals = await _context.Collaterals
                    .Include(c => c.Member)
                    .Where(c => c.MemberNo == memberNo && c.CompanyCode == companyCode)
                    .OrderByDescending(c => c.Id)
                    .ToListAsync();

                return collaterals.Select(c => new CollateralResponseDTO
                {
                    Id = c.Id,
                    ColCode = c.ColCode,
                    Coldescription = c.Coldescription,
                    Percentage = c.Percentage,
                    MemberNo = c.MemberNo,
                    MemberName = c.Member != null ? $"{c.Member.Surname} {c.Member.OtherNames}".Trim() : null,
                    CompanyCode = c.CompanyCode,
                    BlockchainTxId = c.BlockchainTxId,
                    PhotoBase64 = c.Photo != null && c.Photo.Length > 0 ? Convert.ToBase64String(c.Photo) : null,
                    PhotoContentType = c.PhotoContentType,
                    HasPhoto = c.Photo != null && c.Photo.Length > 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting member collaterals for {memberNo}");
                return new List<CollateralResponseDTO>();
            }
        }

        // NEW: Get available collaterals for loan guarantee (member-specific)
        public async Task<List<MemberCollateralDTO>> GetAvailableMemberCollateralsForGuaranteeAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Getting available collaterals for member: {memberNo}");

                // Get all collaterals owned by this member
                var memberCollaterals = await _context.Collaterals
                    .Where(c => c.MemberNo == memberNo && c.CompanyCode == companyCode)
                    .ToListAsync();

                if (!memberCollaterals.Any())
                {
                    _logger.LogInformation($"No collaterals found for member {memberNo}");
                    return new List<MemberCollateralDTO>();
                }

                // Get all active collateral guarantees for this member
                var activeGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.MemberNo == memberNo && cg.CompanyCode == companyCode && cg.Balance > 0)
                    .ToListAsync();

                // Get loans that are still active for these guarantees
                var activeLoanNos = activeGuarantees.Select(cg => cg.LoanNo).Distinct().ToList();
                var activeLoans = await _context.Loans
                    .Where(l => activeLoanNos.Contains(l.LoanNo) &&
                               l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                // Filter only guarantees for active loans
                var activeGuaranteesForActiveLoans = activeGuarantees
                    .Where(cg => activeLoans.Contains(cg.LoanNo))
                    .ToList();

                // Group by collateral to calculate used amounts
                var usedAmountByCollateral = activeGuaranteesForActiveLoans
                    .GroupBy(cg => cg.ColCode)
                    .ToDictionary(g => g.Key, g => g.Sum(cg => cg.Balance));

                var result = new List<MemberCollateralDTO>();

                foreach (var collateral in memberCollaterals)
                {
                    // Calculate max guarantee amount based on percentage
                    decimal maxGuaranteeAmount = 0;
                    decimal currentUsed = 0;
                    decimal marketValue = 0;

                    // Find if this collateral is already used
                    if (usedAmountByCollateral.ContainsKey(collateral.ColCode))
                    {
                        currentUsed = usedAmountByCollateral[collateral.ColCode];
                    }

                    // Try to get from ColloanGuar if exists
                    var existingGuarantee = activeGuaranteesForActiveLoans
                        .FirstOrDefault(cg => cg.ColCode == collateral.ColCode);

                    if (existingGuarantee != null)
                    {
                        marketValue = existingGuarantee.Mktvalue;
                        maxGuaranteeAmount = marketValue * (decimal)(collateral.Percentage / 100);
                    }
                    else
                    {
                        // If no existing guarantee, use a default market value
                        // In a real system, you'd have a MemberCollateral table with actual values
                        // For now, we'll assume the collateral value is 100,000 KES
                        marketValue = 100000;
                        maxGuaranteeAmount = marketValue * (decimal)(collateral.Percentage / 100);
                    }

                    decimal availableAmount = Math.Max(0, maxGuaranteeAmount - currentUsed);

                    result.Add(new MemberCollateralDTO
                    {
                        Id = collateral.Id,
                        ColCode = collateral.ColCode,
                        Coldescription = collateral.Coldescription,
                        Percentage = collateral.Percentage,
                        MemberNo = collateral.MemberNo,
                        PhotoBase64 = collateral.Photo != null && collateral.Photo.Length > 0
                            ? Convert.ToBase64String(collateral.Photo)
                            : null,
                        HasPhoto = collateral.Photo != null && collateral.Photo.Length > 0,
                        MarketValue = marketValue,
                        MaxGuaranteeAmount = maxGuaranteeAmount,
                        CurrentlyUsedAmount = currentUsed,
                        AvailableAmount = availableAmount,
                        IsActive = availableAmount > 0,
                        LoanNoGuaranteeing = existingGuarantee?.LoanNo
                    });
                }

                // Return only active collaterals with available amount > 0
                var availableCollaterals = result.Where(c => c.AvailableAmount > 0).ToList();

                _logger.LogInformation($"Found {availableCollaterals.Count} available collaterals for member {memberNo}");

                return availableCollaterals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting available collaterals for member {memberNo}");
                return new List<MemberCollateralDTO>();
            }
        }

        public async Task<string> GenerateColCodeAsync(string companyCode)
        {
            try
            {
                var prefix = "COL";
                var date = DateTime.Now.ToString("yyyyMMdd");
                var sequence = 1;

                var lastCollateral = await _context.Collaterals
                    .Where(c => c.CompanyCode == companyCode && c.ColCode.StartsWith(prefix + date))
                    .OrderByDescending(c => c.ColCode)
                    .FirstOrDefaultAsync();

                if (lastCollateral != null && lastCollateral.ColCode.Length >= prefix.Length + date.Length + 3)
                {
                    var seqStr = lastCollateral.ColCode.Substring(lastCollateral.ColCode.Length - 3);
                    if (int.TryParse(seqStr, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }

                return $"{prefix}{date}{sequence:D3}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating collateral code");
                throw;
            }
        }

        public async Task<bool> IsColCodeUniqueAsync(string colCode, string companyCode, long? excludeId = null)
        {
            try
            {
                var query = _context.Collaterals
                    .Where(c => c.ColCode == colCode && c.CompanyCode == companyCode);

                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.Id != excludeId.Value);
                }

                return !await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking collateral code uniqueness");
                throw;
            }
        }

        public async Task<List<CollateralReportDTO>> GetCollateralReportAsync(string companyCode)
        {
            try
            {
                var query = from cg in _context.ColloanGuars
                            join c in _context.Collaterals on cg.ColCode equals c.ColCode
                            join m in _context.Members on cg.MemberNo equals m.MemberNo
                            where cg.CompanyCode == companyCode && c.CompanyCode == companyCode
                            select new CollateralReportDTO
                            {
                                MemberNo = cg.MemberNo,
                                Names = m.FullName ?? m.Surname + " " + m.OtherNames,
                                LoanNo = cg.LoanNo,
                                Coldescription = c.Coldescription,
                                ColCode = c.ColCode,
                                Mktvalue = cg.Mktvalue,
                                Balance = cg.Balance,
                                Percentage = c.Percentage / 100
                            };

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating collateral report");
                throw;
            }
        }

        public async Task<MemberModel?> GetMemberByMemberNoAsync(string memberNo, string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo) || string.IsNullOrEmpty(companyCode))
                    return null;

                return await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting member {memberNo}");
                return null;
            }
        }

        public async Task<List<MemberModel>> SearchMembersAsync(string searchTerm, string companyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return new List<MemberModel>();

                searchTerm = searchTerm.Trim();

                return await _context.Members
                    .Where(m => m.CompanyCode == companyCode &&
                                (m.MemberNo.Contains(searchTerm) ||
                                 m.Surname.Contains(searchTerm) ||
                                 m.OtherNames.Contains(searchTerm) ||
                                 m.Idno.Contains(searchTerm) ||
                                 m.PhoneNo.Contains(searchTerm) ||
                                 m.Email.Contains(searchTerm)))
                    .Take(20)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching members with term: {searchTerm}");
                return new List<MemberModel>();
            }
        }
    }
}