// LocationService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface ILocationService
    {
        // County Methods
        Task<CountyListResponseDTO> GetAllCountiesAsync();
        Task<CountyDTO?> GetCountyByIdAsync(int id);
        Task<CountyDTO?> GetCountyByCodeAsync(string countyCode);
        Task<LocationResponseDTO> CreateCountyAsync(CreateCountyDTO dto);
        Task<LocationResponseDTO> UpdateCountyAsync(UpdateCountyDTO dto);
        Task<LocationResponseDTO> DeleteCountyAsync(int id, string deletedBy);

        // SubCounty Methods
        Task<SubCountyListResponseDTO> GetAllSubCountiesAsync(int? countyId = null);
        Task<SubCountyDTO?> GetSubCountyByIdAsync(int id);
        Task<List<SubCountyDTO>> GetSubCountiesByCountyIdAsync(int countyId);
        Task<LocationResponseDTO> CreateSubCountyAsync(CreateSubCountyDTO dto);
        Task<LocationResponseDTO> UpdateSubCountyAsync(UpdateSubCountyDTO dto);
        Task<LocationResponseDTO> DeleteSubCountyAsync(int id, string deletedBy);

        // Ward Methods
        Task<WardListResponseDTO> GetAllWardsAsync(int? subCountyId = null);
        Task<WardDTO?> GetWardByIdAsync(int id);
        Task<List<WardDTO>> GetWardsBySubCountyIdAsync(int subCountyId);
        Task<LocationResponseDTO> CreateWardAsync(CreateWardDTO dto);
        Task<LocationResponseDTO> UpdateWardAsync(UpdateWardDTO dto);
        Task<LocationResponseDTO> DeleteWardAsync(int id, string deletedBy);
    }


    public class LocationService : ILocationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<LocationService> _logger;

        public LocationService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<LocationService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        #region Helper Methods for Code Generation

        /// <summary>
        /// Generates SubCounty code based on County code
        /// Format: {CountyCode}-{SequentialNumber}
        /// Example: 047-01, 047-02
        /// </summary>
        private async Task<string> GenerateSubCountyCodeAsync(int countyId)
        {
            // Get county code
            var county = await _context.Counties.FindAsync(countyId);
            if (county == null)
                throw new Exception($"County with ID {countyId} not found");

            var countyCode = county.CountyCode;

            // Get existing sub-counties in this county
            var existingSubCounties = await _context.SubCounties
                .Where(s => s.CountyId == countyId)
                .ToListAsync();

            // Find the highest sequence number
            int maxSequence = 0;
            var prefix = countyCode + "-";

            foreach (var subCounty in existingSubCounties)
            {
                if (subCounty.SubCountyCode.StartsWith(prefix))
                {
                    var numberPart = subCounty.SubCountyCode.Substring(prefix.Length);
                    if (int.TryParse(numberPart, out int seq) && seq > maxSequence)
                    {
                        maxSequence = seq;
                    }
                }
            }

            // Generate new code with next sequence (padded to 2 digits)
            int nextSequence = maxSequence + 1;
            return $"{countyCode}-{nextSequence:D2}";
        }

        /// <summary>
        /// Generates Ward code based on SubCounty code
        /// Format: {SubCountyCode}-{SequentialNumber}
        /// Example: 047-01-01, 047-01-02
        /// </summary>
        private async Task<string> GenerateWardCodeAsync(int subCountyId)
        {
            // Get sub-county code
            var subCounty = await _context.SubCounties.FindAsync(subCountyId);
            if (subCounty == null)
                throw new Exception($"SubCounty with ID {subCountyId} not found");

            var subCountyCode = subCounty.SubCountyCode;

            // Get existing wards in this sub-county
            var existingWards = await _context.Wards
                .Where(w => w.SubCountyId == subCountyId)
                .ToListAsync();

            // Find the highest sequence number
            int maxSequence = 0;
            var prefix = subCountyCode + "-";

            foreach (var ward in existingWards)
            {
                if (ward.WardCode.StartsWith(prefix))
                {
                    var numberPart = ward.WardCode.Substring(prefix.Length);
                    if (int.TryParse(numberPart, out int seq) && seq > maxSequence)
                    {
                        maxSequence = seq;
                    }
                }
            }

            // Generate new code with next sequence (padded to 2 digits)
            int nextSequence = maxSequence + 1;
            return $"{subCountyCode}-{nextSequence:D2}";
        }

        /// <summary>
        /// Validates that SubCounty code follows the expected format
        /// </summary>
        private bool ValidateSubCountyCodeFormat(string subCountyCode, string countyCode)
        {
            if (string.IsNullOrEmpty(subCountyCode))
                return false;

            var expectedPrefix = countyCode + "-";
            if (!subCountyCode.StartsWith(expectedPrefix))
                return false;

            var numberPart = subCountyCode.Substring(expectedPrefix.Length);
            return int.TryParse(numberPart, out _);
        }

        /// <summary>
        /// Validates that Ward code follows the expected format
        /// </summary>
        private bool ValidateWardCodeFormat(string wardCode, string subCountyCode)
        {
            if (string.IsNullOrEmpty(wardCode))
                return false;

            var expectedPrefix = subCountyCode + "-";
            if (!wardCode.StartsWith(expectedPrefix))
                return false;

            var numberPart = wardCode.Substring(expectedPrefix.Length);
            return int.TryParse(numberPart, out _);
        }

        #endregion

        #region County Methods

        public async Task<CountyListResponseDTO> GetAllCountiesAsync()
        {
            try
            {
                var counties = await _context.Counties
                    .Include(c => c.SubCounties)
                    .OrderBy(c => c.CountyCode)
                    .Select(c => new CountyDTO
                    {
                        Id = c.Id,
                        CountyCode = c.CountyCode,
                        CountyName = c.CountyName,
                        Headquarters = c.Headquarters,
                        Region = c.Region,
                        Status = c.Status,
                        CreatedBy = c.CreatedBy,
                        CreatedAt = c.CreatedAt,
                        ModifiedBy = c.ModifiedBy,
                        ModifiedAt = c.ModifiedAt,
                        BlockchainTxId = c.BlockchainTxId,
                        SubCountyCount = c.SubCounties.Count
                    })
                    .ToListAsync();

                return new CountyListResponseDTO
                {
                    Counties = counties,
                    TotalCount = counties.Count,
                    ActiveCount = counties.Count(c => c.Status == "Active")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all counties");
                return new CountyListResponseDTO();
            }
        }

        public async Task<CountyDTO?> GetCountyByIdAsync(int id)
        {
            try
            {
                var county = await _context.Counties
                    .Include(c => c.SubCounties)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (county == null) return null;

                return new CountyDTO
                {
                    Id = county.Id,
                    CountyCode = county.CountyCode,
                    CountyName = county.CountyName,
                    Headquarters = county.Headquarters,
                    Region = county.Region,
                    Status = county.Status,
                    CreatedBy = county.CreatedBy,
                    CreatedAt = county.CreatedAt,
                    ModifiedBy = county.ModifiedBy,
                    ModifiedAt = county.ModifiedAt,
                    BlockchainTxId = county.BlockchainTxId,
                    SubCountyCount = county.SubCounties?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting county by id {id}");
                return null;
            }
        }

        public async Task<CountyDTO?> GetCountyByCodeAsync(string countyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(countyCode))
                    return null;

                var county = await _context.Counties
                    .Include(c => c.SubCounties)
                    .FirstOrDefaultAsync(c => c.CountyCode == countyCode);

                if (county == null)
                    return null;

                return new CountyDTO
                {
                    Id = county.Id,
                    CountyCode = county.CountyCode,
                    CountyName = county.CountyName,
                    Headquarters = county.Headquarters,
                    Region = county.Region,
                    Status = county.Status,
                    CreatedBy = county.CreatedBy,
                    CreatedAt = county.CreatedAt,
                    ModifiedBy = county.ModifiedBy,
                    ModifiedAt = county.ModifiedAt,
                    BlockchainTxId = county.BlockchainTxId,
                    SubCountyCount = county.SubCounties?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting county by code {countyCode}");
                return null;
            }
        }

        public async Task<LocationResponseDTO> CreateCountyAsync(CreateCountyDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating new county: {dto.CountyName}");

                // Check if county code already exists
                var existingByCode = await _context.Counties
                    .FirstOrDefaultAsync(c => c.CountyCode == dto.CountyCode);

                if (existingByCode != null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with code {dto.CountyCode} already exists"
                    };
                }

                // Check if county name already exists
                var existingByName = await _context.Counties
                    .FirstOrDefaultAsync(c => c.CountyName == dto.CountyName);

                if (existingByName != null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with name {dto.CountyName} already exists"
                    };
                }

                var county = new County
                {
                    CountyCode = dto.CountyCode,
                    CountyName = dto.CountyName,
                    Headquarters = dto.Headquarters,
                    Region = dto.Region,
                    Status = dto.Status ?? "Active",
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now,
                    ModifiedBy = dto.CreatedBy,
                    ModifiedAt = DateTime.Now
                };

                _context.Counties.Add(county);
                await _context.SaveChangesAsync();

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "CREATE_COUNTY",
                    CountyId = county.Id,
                    CountyCode = county.CountyCode,
                    CountyName = county.CountyName,
                    Headquarters = county.Headquarters,
                    Region = county.Region,
                    Status = county.Status,
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "COUNTY_CREATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = county.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                county.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"County '{county.CountyName}' created successfully",
                    Data = new CountyDTO
                    {
                        Id = county.Id,
                        CountyCode = county.CountyCode,
                        CountyName = county.CountyName,
                        Headquarters = county.Headquarters,
                        Region = county.Region,
                        Status = county.Status,
                        BlockchainTxId = blockchainTx.TransactionId
                    },
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating county {dto.CountyName}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error creating county: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> UpdateCountyAsync(UpdateCountyDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating county {dto.Id}");

                var county = await _context.Counties.FindAsync(dto.Id);
                if (county == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with ID {dto.Id} not found"
                    };
                }

                // Store old values for blockchain
                var oldValues = new
                {
                    county.CountyCode,
                    county.CountyName,
                    county.Headquarters,
                    county.Region,
                    county.Status
                };

                county.CountyName = dto.CountyName;
                county.Headquarters = dto.Headquarters;
                county.Region = dto.Region;
                county.Status = dto.Status ?? "Active";
                county.ModifiedBy = dto.ModifiedBy;
                county.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "UPDATE_COUNTY",
                    CountyId = county.Id,
                    OldValues = oldValues,
                    NewValues = new
                    {
                        county.CountyName,
                        county.Headquarters,
                        county.Region,
                        county.Status
                    },
                    ModifiedBy = dto.ModifiedBy,
                    ModifiedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "COUNTY_UPDATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = county.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                county.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"County '{county.CountyName}' updated successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating county {dto.Id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error updating county: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> DeleteCountyAsync(int id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Deleting county {id}");

                var county = await _context.Counties
                    .Include(c => c.SubCounties)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (county == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with ID {id} not found"
                    };
                }

                if (county.SubCounties.Any())
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"Cannot delete county '{county.CountyName}' because it has {county.SubCounties.Count} sub-counties. Delete sub-counties first."
                    };
                }

                var oldValues = new
                {
                    county.Id,
                    county.CountyCode,
                    county.CountyName,
                    county.Headquarters,
                    county.Region,
                    county.Status
                };

                _context.Counties.Remove(county);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    Action = "DELETE_COUNTY",
                    DeletedRecord = oldValues,
                    DeletedBy = deletedBy,
                    DeletedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "COUNTY_DELETED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"County '{county.CountyName}' deleted successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting county {id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error deleting county: {ex.Message}"
                };
            }
        }

        #endregion

        #region SubCounty Methods

        public async Task<SubCountyListResponseDTO> GetAllSubCountiesAsync(int? countyId = null)
        {
            try
            {
                var query = _context.SubCounties
                    .Include(s => s.County)
                    .Include(s => s.Wards)
                    .AsQueryable();

                if (countyId.HasValue)
                {
                    query = query.Where(s => s.CountyId == countyId.Value);
                }

                var subCounties = await query
                    .OrderBy(s => s.SubCountyName)
                    .Select(s => new SubCountyDTO
                    {
                        Id = s.Id,
                        SubCountyCode = s.SubCountyCode,
                        SubCountyName = s.SubCountyName,
                        CountyId = s.CountyId,
                        CountyName = s.County != null ? s.County.CountyName : null,
                        Headquarters = s.Headquarters,
                        Status = s.Status,
                        CreatedBy = s.CreatedBy,
                        CreatedAt = s.CreatedAt,
                        ModifiedBy = s.ModifiedBy,
                        ModifiedAt = s.ModifiedAt,
                        BlockchainTxId = s.BlockchainTxId,
                        WardCount = s.Wards.Count
                    })
                    .ToListAsync();

                return new SubCountyListResponseDTO
                {
                    SubCounties = subCounties,
                    TotalCount = subCounties.Count,
                    ActiveCount = subCounties.Count(s => s.Status == "Active")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all sub-counties");
                return new SubCountyListResponseDTO();
            }
        }

        public async Task<SubCountyDTO?> GetSubCountyByIdAsync(int id)
        {
            try
            {
                var subCounty = await _context.SubCounties
                    .Include(s => s.County)
                    .Include(s => s.Wards)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (subCounty == null) return null;

                return new SubCountyDTO
                {
                    Id = subCounty.Id,
                    SubCountyCode = subCounty.SubCountyCode,
                    SubCountyName = subCounty.SubCountyName,
                    CountyId = subCounty.CountyId,
                    CountyName = subCounty.County?.CountyName,
                    Headquarters = subCounty.Headquarters,
                    Status = subCounty.Status,
                    CreatedBy = subCounty.CreatedBy,
                    CreatedAt = subCounty.CreatedAt,
                    ModifiedBy = subCounty.ModifiedBy,
                    ModifiedAt = subCounty.ModifiedAt,
                    BlockchainTxId = subCounty.BlockchainTxId,
                    WardCount = subCounty.Wards?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sub-county by id {id}");
                return null;
            }
        }

        public async Task<List<SubCountyDTO>> GetSubCountiesByCountyIdAsync(int countyId)
        {
            try
            {
                var subCounties = await _context.SubCounties
                    .Where(s => s.CountyId == countyId && s.Status == "Active")
                    .OrderBy(s => s.SubCountyCode)
                    .Select(s => new SubCountyDTO
                    {
                        Id = s.Id,
                        SubCountyCode = s.SubCountyCode,
                        SubCountyName = s.SubCountyName,
                        CountyId = s.CountyId,
                        Headquarters = s.Headquarters,
                        Status = s.Status,
                        BlockchainTxId = s.BlockchainTxId
                    })
                    .ToListAsync();

                return subCounties;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sub-counties by county {countyId}");
                return new List<SubCountyDTO>();
            }
        }

        public async Task<LocationResponseDTO> CreateSubCountyAsync(CreateSubCountyDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating new sub-county: {dto.SubCountyName}");

                // Verify county exists
                var county = await _context.Counties.FindAsync(dto.CountyId);
                if (county == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with ID {dto.CountyId} not found"
                    };
                }

                // Auto-generate SubCounty code
                var generatedCode = await GenerateSubCountyCodeAsync(dto.CountyId);
                dto.SubCountyCode = generatedCode;

                // Check if sub-county name already exists in this county
                var existingByName = await _context.SubCounties
                    .FirstOrDefaultAsync(s => s.SubCountyName == dto.SubCountyName && s.CountyId == dto.CountyId);

                if (existingByName != null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"SubCounty with name '{dto.SubCountyName}' already exists in this county"
                    };
                }

                var subCounty = new SubCounty
                {
                    SubCountyCode = dto.SubCountyCode,
                    SubCountyName = dto.SubCountyName,
                    CountyId = dto.CountyId,
                    Headquarters = dto.Headquarters,
                    Status = dto.Status ?? "Active",
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now,
                    ModifiedBy = dto.CreatedBy,
                    ModifiedAt = DateTime.Now
                };

                _context.SubCounties.Add(subCounty);
                await _context.SaveChangesAsync();

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "CREATE_SUBCOUNTY",
                    SubCountyId = subCounty.Id,
                    SubCountyCode = subCounty.SubCountyCode,
                    SubCountyName = subCounty.SubCountyName,
                    CountyId = subCounty.CountyId,
                    CountyName = county.CountyName,
                    CountyCode = county.CountyCode,
                    Headquarters = subCounty.Headquarters,
                    Status = subCounty.Status,
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUBCOUNTY_CREATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = subCounty.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                subCounty.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"SubCounty '{subCounty.SubCountyName}' created successfully with code '{subCounty.SubCountyCode}'",
                    Data = new SubCountyDTO
                    {
                        Id = subCounty.Id,
                        SubCountyCode = subCounty.SubCountyCode,
                        SubCountyName = subCounty.SubCountyName,
                        CountyId = subCounty.CountyId,
                        CountyName = county.CountyName,
                        Headquarters = subCounty.Headquarters,
                        Status = subCounty.Status,
                        BlockchainTxId = blockchainTx.TransactionId
                    },
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating sub-county {dto.SubCountyName}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error creating sub-county: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> UpdateSubCountyAsync(UpdateSubCountyDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating sub-county {dto.Id}");

                var subCounty = await _context.SubCounties
                    .Include(s => s.County)
                    .FirstOrDefaultAsync(s => s.Id == dto.Id);

                if (subCounty == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"SubCounty with ID {dto.Id} not found"
                    };
                }

                // Verify county exists if changed
                var county = await _context.Counties.FindAsync(dto.CountyId);
                if (county == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"County with ID {dto.CountyId} not found"
                    };
                }

                // Store old values for blockchain
                var oldValues = new
                {
                    subCounty.SubCountyCode,
                    subCounty.SubCountyName,
                    subCounty.CountyId,
                    subCounty.Headquarters,
                    subCounty.Status
                };

                // Check if sub-county name already exists in the new county (if county changed)
                if (subCounty.CountyId != dto.CountyId)
                {
                    var existingByName = await _context.SubCounties
                        .FirstOrDefaultAsync(s => s.SubCountyName == dto.SubCountyName && s.CountyId == dto.CountyId && s.Id != dto.Id);

                    if (existingByName != null)
                    {
                        return new LocationResponseDTO
                        {
                            Success = false,
                            Message = $"SubCounty with name '{dto.SubCountyName}' already exists in the selected county"
                        };
                    }
                }
                else
                {
                    var existingByName = await _context.SubCounties
                        .FirstOrDefaultAsync(s => s.SubCountyName == dto.SubCountyName && s.CountyId == dto.CountyId && s.Id != dto.Id);

                    if (existingByName != null)
                    {
                        return new LocationResponseDTO
                        {
                            Success = false,
                            Message = $"SubCounty with name '{dto.SubCountyName}' already exists in this county"
                        };
                    }
                }

                // If county changed, regenerate code
                if (subCounty.CountyId != dto.CountyId)
                {
                    var newCode = await GenerateSubCountyCodeAsync(dto.CountyId);
                    dto.SubCountyCode = newCode;
                }
                else
                {
                    // Keep existing code
                    dto.SubCountyCode = subCounty.SubCountyCode;
                }

                subCounty.SubCountyCode = dto.SubCountyCode;
                subCounty.SubCountyName = dto.SubCountyName;
                subCounty.CountyId = dto.CountyId;
                subCounty.Headquarters = dto.Headquarters;
                subCounty.Status = dto.Status ?? "Active";
                subCounty.ModifiedBy = dto.ModifiedBy;
                subCounty.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "UPDATE_SUBCOUNTY",
                    SubCountyId = subCounty.Id,
                    OldValues = oldValues,
                    NewValues = new
                    {
                        subCounty.SubCountyCode,
                        subCounty.SubCountyName,
                        subCounty.CountyId,
                        CountyName = county.CountyName,
                        CountyCode = county.CountyCode,
                        subCounty.Headquarters,
                        subCounty.Status
                    },
                    ModifiedBy = dto.ModifiedBy,
                    ModifiedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUBCOUNTY_UPDATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = subCounty.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                subCounty.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"SubCounty '{subCounty.SubCountyName}' updated successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating sub-county {dto.Id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error updating sub-county: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> DeleteSubCountyAsync(int id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Deleting sub-county {id}");

                var subCounty = await _context.SubCounties
                    .Include(s => s.Wards)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (subCounty == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"SubCounty with ID {id} not found"
                    };
                }

                if (subCounty.Wards.Any())
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"Cannot delete sub-county '{subCounty.SubCountyName}' because it has {subCounty.Wards.Count} wards. Delete wards first."
                    };
                }

                var oldValues = new
                {
                    subCounty.Id,
                    subCounty.SubCountyCode,
                    subCounty.SubCountyName,
                    subCounty.CountyId,
                    subCounty.Headquarters,
                    subCounty.Status
                };

                _context.SubCounties.Remove(subCounty);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    Action = "DELETE_SUBCOUNTY",
                    DeletedRecord = oldValues,
                    DeletedBy = deletedBy,
                    DeletedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "SUBCOUNTY_DELETED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"SubCounty '{subCounty.SubCountyName}' deleted successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting sub-county {id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error deleting sub-county: {ex.Message}"
                };
            }
        }

        #endregion

        #region Ward Methods

        public async Task<WardListResponseDTO> GetAllWardsAsync(int? subCountyId = null)
        {
            try
            {
                var query = _context.Wards
                    .Include(w => w.SubCounty)
                        .ThenInclude(s => s != null ? s.County : null)
                    .AsQueryable();

                if (subCountyId.HasValue)
                {
                    query = query.Where(w => w.SubCountyId == subCountyId.Value);
                }

                var wards = await query
                    .OrderBy(w => w.WardCode)
                    .Select(w => new WardDTO
                    {
                        Id = w.Id,
                        WardCode = w.WardCode,
                        WardName = w.WardName,
                        SubCountyId = w.SubCountyId,
                        SubCountyName = w.SubCounty != null ? w.SubCounty.SubCountyName : null,
                        CountyName = w.SubCounty != null && w.SubCounty.County != null ? w.SubCounty.County.CountyName : null,
                        Constituency = w.Constituency,
                        Status = w.Status,
                        CreatedBy = w.CreatedBy,
                        CreatedAt = w.CreatedAt,
                        ModifiedBy = w.ModifiedBy,
                        ModifiedAt = w.ModifiedAt,
                        BlockchainTxId = w.BlockchainTxId
                    })
                    .ToListAsync();

                return new WardListResponseDTO
                {
                    Wards = wards,
                    TotalCount = wards.Count,
                    ActiveCount = wards.Count(w => w.Status == "Active")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all wards");
                return new WardListResponseDTO();
            }
        }

        public async Task<WardDTO?> GetWardByIdAsync(int id)
        {
            try
            {
                var ward = await _context.Wards
                    .Include(w => w.SubCounty)
                        .ThenInclude(s => s != null ? s.County : null)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (ward == null) return null;

                return new WardDTO
                {
                    Id = ward.Id,
                    WardCode = ward.WardCode,
                    WardName = ward.WardName,
                    SubCountyId = ward.SubCountyId,
                    SubCountyName = ward.SubCounty?.SubCountyName,
                    CountyName = ward.SubCounty?.County?.CountyName,
                    Constituency = ward.Constituency,
                    Status = ward.Status,
                    CreatedBy = ward.CreatedBy,
                    CreatedAt = ward.CreatedAt,
                    ModifiedBy = ward.ModifiedBy,
                    ModifiedAt = ward.ModifiedAt,
                    BlockchainTxId = ward.BlockchainTxId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ward by id {id}");
                return null;
            }
        }

        public async Task<List<WardDTO>> GetWardsBySubCountyIdAsync(int subCountyId)
        {
            try
            {
                var wards = await _context.Wards
                    .Where(w => w.SubCountyId == subCountyId && w.Status == "Active")
                    .OrderBy(w => w.WardCode)
                    .Select(w => new WardDTO
                    {
                        Id = w.Id,
                        WardCode = w.WardCode,
                        WardName = w.WardName,
                        SubCountyId = w.SubCountyId,
                        Constituency = w.Constituency,
                        Status = w.Status,
                        BlockchainTxId = w.BlockchainTxId
                    })
                    .ToListAsync();

                return wards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting wards by sub-county {subCountyId}");
                return new List<WardDTO>();
            }
        }

        public async Task<LocationResponseDTO> CreateWardAsync(CreateWardDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating new ward: {dto.WardName}");

                // Verify sub-county exists
                var subCounty = await _context.SubCounties
                    .Include(s => s.County)
                    .FirstOrDefaultAsync(s => s.Id == dto.SubCountyId);

                if (subCounty == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"SubCounty with ID {dto.SubCountyId} not found"
                    };
                }

                // Auto-generate Ward code
                var generatedCode = await GenerateWardCodeAsync(dto.SubCountyId);
                dto.WardCode = generatedCode;

                // Check if ward name already exists in this sub-county
                var existingByName = await _context.Wards
                    .FirstOrDefaultAsync(w => w.WardName == dto.WardName && w.SubCountyId == dto.SubCountyId);

                if (existingByName != null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"Ward with name '{dto.WardName}' already exists in this sub-county"
                    };
                }

                var ward = new Ward
                {
                    WardCode = dto.WardCode,
                    WardName = dto.WardName,
                    SubCountyId = dto.SubCountyId,
                    Constituency = dto.Constituency,
                    Status = dto.Status ?? "Active",
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now,
                    ModifiedBy = dto.CreatedBy,
                    ModifiedAt = DateTime.Now
                };

                _context.Wards.Add(ward);
                await _context.SaveChangesAsync();

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "CREATE_WARD",
                    WardId = ward.Id,
                    WardCode = ward.WardCode,
                    WardName = ward.WardName,
                    SubCountyId = ward.SubCountyId,
                    SubCountyName = subCounty.SubCountyName,
                    SubCountyCode = subCounty.SubCountyCode,
                    CountyName = subCounty.County?.CountyName,
                    CountyCode = subCounty.County?.CountyCode,
                    Constituency = ward.Constituency,
                    Status = ward.Status,
                    CreatedBy = dto.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "WARD_CREATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = ward.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                ward.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"Ward '{ward.WardName}' created successfully with code '{ward.WardCode}'",
                    Data = new WardDTO
                    {
                        Id = ward.Id,
                        WardCode = ward.WardCode,
                        WardName = ward.WardName,
                        SubCountyId = ward.SubCountyId,
                        SubCountyName = subCounty.SubCountyName,
                        CountyName = subCounty.County?.CountyName,
                        Constituency = ward.Constituency,
                        Status = ward.Status,
                        BlockchainTxId = blockchainTx.TransactionId
                    },
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating ward {dto.WardName}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error creating ward: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> UpdateWardAsync(UpdateWardDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating ward {dto.Id}");

                var ward = await _context.Wards
                    .Include(w => w.SubCounty)
                        .ThenInclude(s => s != null ? s.County : null)
                    .FirstOrDefaultAsync(w => w.Id == dto.Id);

                if (ward == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"Ward with ID {dto.Id} not found"
                    };
                }

                // Verify sub-county exists if changed
                if (ward.SubCountyId != dto.SubCountyId)
                {
                    var newSubCounty = await _context.SubCounties.FindAsync(dto.SubCountyId);
                    if (newSubCounty == null)
                    {
                        return new LocationResponseDTO
                        {
                            Success = false,
                            Message = $"SubCounty with ID {dto.SubCountyId} not found"
                        };
                    }
                }

                // Store old values for blockchain
                var oldValues = new
                {
                    ward.WardCode,
                    ward.WardName,
                    ward.SubCountyId,
                    ward.Constituency,
                    ward.Status
                };

                // Check if ward name already exists in the new sub-county (if sub-county changed)
                if (ward.SubCountyId != dto.SubCountyId)
                {
                    var existingByName = await _context.Wards
                        .FirstOrDefaultAsync(w => w.WardName == dto.WardName && w.SubCountyId == dto.SubCountyId && w.Id != dto.Id);

                    if (existingByName != null)
                    {
                        return new LocationResponseDTO
                        {
                            Success = false,
                            Message = $"Ward with name '{dto.WardName}' already exists in the selected sub-county"
                        };
                    }
                }
                else
                {
                    var existingByName = await _context.Wards
                        .FirstOrDefaultAsync(w => w.WardName == dto.WardName && w.SubCountyId == dto.SubCountyId && w.Id != dto.Id);

                    if (existingByName != null)
                    {
                        return new LocationResponseDTO
                        {
                            Success = false,
                            Message = $"Ward with name '{dto.WardName}' already exists in this sub-county"
                        };
                    }
                }

                // If sub-county changed, regenerate code
                if (ward.SubCountyId != dto.SubCountyId)
                {
                    var newCode = await GenerateWardCodeAsync(dto.SubCountyId);
                    dto.WardCode = newCode;
                }
                else
                {
                    // Keep existing code
                    dto.WardCode = ward.WardCode;
                }

                ward.WardCode = dto.WardCode;
                ward.WardName = dto.WardName;
                ward.SubCountyId = dto.SubCountyId;
                ward.Constituency = dto.Constituency;
                ward.Status = dto.Status ?? "Active";
                ward.ModifiedBy = dto.ModifiedBy;
                ward.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var subCounty = await _context.SubCounties
                    .Include(s => s.County)
                    .FirstOrDefaultAsync(s => s.Id == dto.SubCountyId);

                // Create blockchain data
                var blockchainData = new
                {
                    Action = "UPDATE_WARD",
                    WardId = ward.Id,
                    OldValues = oldValues,
                    NewValues = new
                    {
                        ward.WardCode,
                        ward.WardName,
                        ward.SubCountyId,
                        SubCountyName = subCounty?.SubCountyName,
                        SubCountyCode = subCounty?.SubCountyCode,
                        CountyName = subCounty?.County?.CountyName,
                        CountyCode = subCounty?.County?.CountyCode,
                        ward.Constituency,
                        ward.Status
                    },
                    ModifiedBy = dto.ModifiedBy,
                    ModifiedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "WARD_UPDATED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = ward.Id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                ward.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"Ward '{ward.WardName}' updated successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating ward {dto.Id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error updating ward: {ex.Message}"
                };
            }
        }

        public async Task<LocationResponseDTO> DeleteWardAsync(int id, string deletedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Deleting ward {id}");

                var ward = await _context.Wards
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (ward == null)
                {
                    return new LocationResponseDTO
                    {
                        Success = false,
                        Message = $"Ward with ID {id} not found"
                    };
                }

                var oldValues = new
                {
                    ward.Id,
                    ward.WardCode,
                    ward.WardName,
                    ward.SubCountyId,
                    ward.Constituency,
                    ward.Status
                };

                _context.Wards.Remove(ward);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    Action = "DELETE_WARD",
                    DeletedRecord = oldValues,
                    DeletedBy = deletedBy,
                    DeletedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "WARD_DELETED",
                    MemberNo = null,
                    CompanyCode = null,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = id.ToString(),
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new LocationResponseDTO
                {
                    Success = true,
                    Message = $"Ward '{ward.WardName}' deleted successfully",
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting ward {id}");
                return new LocationResponseDTO
                {
                    Success = false,
                    Message = $"Error deleting ward: {ex.Message}"
                };
            }
        }

        #endregion
    }
}