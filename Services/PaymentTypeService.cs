// Services/PaymentTypeService.cs
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface IPaymentTypeService
    {
        Task<PaymentTypeResponseDTO> CreateAsync(PaymentTypeDTO dto, string UserName, string companyCode);
        Task<PaymentTypeResponseDTO> UpdateAsync(int id, PaymentTypeDTO dto, string UserName, string companyCode);
        Task<bool> DeleteAsync(int id, string UserName, string companyCode);
        Task<PaymentTypeResponseDTO> GetByIdAsync(int id);
        Task<List<PaymentTypeResponseDTO>> GetAllAsync(string companyCode, bool includeInactive = false);
        Task<List<PaymentTypeSimpleDTO>> GetActivePaymentTypesAsync(string companyCode);
        Task<bool> IsCodeUniqueAsync(string code, string companyCode, int? excludeId = null);
        Task<bool> ToggleStatusAsync(int id, string UserName, string companyCode);
        Task<Dictionary<int, string>> GetPaymentTypeDictionaryAsync(string companyCode);
    }

    public class PaymentTypeService : IPaymentTypeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<PaymentTypeService> _logger;

        public PaymentTypeService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<PaymentTypeService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        public async Task<PaymentTypeResponseDTO> CreateAsync(PaymentTypeDTO dto, string UserName, string companyCode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate unique code
                if (!await IsCodeUniqueAsync(dto.Code, companyCode))
                {
                    throw new InvalidOperationException($"Payment type code '{dto.Code}' already exists.");
                }

                // Validate default expense account if provided
                string expenseAccountName = null;
                if (!string.IsNullOrEmpty(dto.DefaultExpenseAccountNo))
                {
                    var expenseAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.DefaultExpenseAccountNo && g.CompanyCode == companyCode);

                    if (expenseAccount == null)
                    {
                        throw new InvalidOperationException($"Expense account '{dto.DefaultExpenseAccountNo}' not found.");
                    }
                    expenseAccountName = expenseAccount.Glaccname;
                }

                var paymentType = new PaymentType
                {
                    Code = dto.Code?.ToUpper(),
                    Name = dto.Name,
                    Description = dto.Description,
                    IsActive = dto.IsActive,
                    DisplayOrder = dto.DisplayOrder,
                    DefaultExpenseAccountNo = dto.DefaultExpenseAccountNo,
                    CompanyCode = companyCode,
                    AuditId = UserName,
                    AuditTime = DateTime.Now
                };

                _context.PaymentTypes.Add(paymentType);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        Action = "CREATE_PAYMENT_TYPE",
                        PaymentType = new
                        {
                            paymentType.Code,
                            paymentType.Name,
                            paymentType.Description,
                            paymentType.IsActive,
                            paymentType.DisplayOrder,
                            DefaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo,
                            DefaultExpenseAccountName = expenseAccountName
                        },
                        CreatedBy = UserName,
                        CreatedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "PAYMENT_TYPE_CREATE",
                        null,
                        companyCode,
                        0,
                        "SYSTEM",
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        paymentType.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for payment type creation");
                }

                await transaction.CommitAsync();

                return new PaymentTypeResponseDTO
                {
                    Id = paymentType.Id,
                    Code = paymentType.Code,
                    Name = paymentType.Name,
                    Description = paymentType.Description,
                    IsActive = paymentType.IsActive,
                    DisplayOrder = paymentType.DisplayOrder,
                    DefaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo,
                    DefaultExpenseAccountName = expenseAccountName,
                    BlockchainTxId = blockchainTxId,
                    CreatedAt = paymentType.AuditTime,
                    CreatedBy = paymentType.AuditId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating payment type");
                throw;
            }
        }

        public async Task<PaymentTypeResponseDTO> UpdateAsync(int id, PaymentTypeDTO dto, string UserName, string companyCode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var paymentType = await _context.PaymentTypes
                    .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == companyCode);

                if (paymentType == null)
                {
                    throw new InvalidOperationException($"Payment type with ID {id} not found.");
                }

                // Validate unique code (excluding current)
                if (!await IsCodeUniqueAsync(dto.Code, companyCode, id))
                {
                    throw new InvalidOperationException($"Payment type code '{dto.Code}' already exists.");
                }

                // Store old values for audit
                var oldValues = new
                {
                    paymentType.Code,
                    paymentType.Name,
                    paymentType.Description,
                    paymentType.IsActive,
                    paymentType.DisplayOrder,
                    paymentType.DefaultExpenseAccountNo
                };

                // Validate default expense account if changed
                string expenseAccountName = null;
                if (!string.IsNullOrEmpty(dto.DefaultExpenseAccountNo))
                {
                    var expenseAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == dto.DefaultExpenseAccountNo && g.CompanyCode == companyCode);

                    if (expenseAccount == null)
                    {
                        throw new InvalidOperationException($"Expense account '{dto.DefaultExpenseAccountNo}' not found.");
                    }
                    expenseAccountName = expenseAccount.Glaccname;
                }

                // Update fields
                paymentType.Code = dto.Code?.ToUpper();
                paymentType.Name = dto.Name;
                paymentType.Description = dto.Description;
                paymentType.IsActive = dto.IsActive;
                paymentType.DisplayOrder = dto.DisplayOrder;
                paymentType.DefaultExpenseAccountNo = dto.DefaultExpenseAccountNo;
                paymentType.AuditId = UserName;
                paymentType.AuditTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Record blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        Action = "UPDATE_PAYMENT_TYPE",
                        PaymentTypeId = paymentType.Id,
                        OldValues = oldValues,
                        NewValues = new
                        {
                            paymentType.Code,
                            paymentType.Name,
                            paymentType.Description,
                            paymentType.IsActive,
                            paymentType.DisplayOrder,
                            paymentType.DefaultExpenseAccountNo,
                            DefaultExpenseAccountName = expenseAccountName
                        },
                        ModifiedBy = UserName,
                        ModifiedAt = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "PAYMENT_TYPE_UPDATE",
                        null,
                        companyCode,
                        0,
                        "SYSTEM",
                        blockchainData);

                    if (blockchainTx != null)
                    {
                        paymentType.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for payment type update");
                }

                await transaction.CommitAsync();

                return new PaymentTypeResponseDTO
                {
                    Id = paymentType.Id,
                    Code = paymentType.Code,
                    Name = paymentType.Name,
                    Description = paymentType.Description,
                    IsActive = paymentType.IsActive,
                    DisplayOrder = paymentType.DisplayOrder,
                    DefaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo,
                    DefaultExpenseAccountName = expenseAccountName,
                    BlockchainTxId = paymentType.BlockchainTxId,
                    CreatedAt = paymentType.AuditTime,
                    CreatedBy = paymentType.AuditId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating payment type {id}");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id, string UserName, string companyCode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var paymentType = await _context.PaymentTypes
                    .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == companyCode);

                if (paymentType == null)
                {
                    throw new InvalidOperationException($"Payment type with ID {id} not found.");
                }

                // Check if payment type is in use
                var isInUse = await _context.Journals
                    .AnyAsync(j => j.SHARETYPE == paymentType.Name && j.CompanyCode == companyCode);

                if (isInUse)
                {
                    throw new InvalidOperationException($"Cannot delete payment type '{paymentType.Name}' as it is being used in existing payments. Consider deactivating it instead.");
                }

                var paymentTypeDetails = new
                {
                    paymentType.Id,
                    paymentType.Code,
                    paymentType.Name,
                    paymentType.DefaultExpenseAccountNo
                };

                _context.PaymentTypes.Remove(paymentType);
                await _context.SaveChangesAsync();

                // Record blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        Action = "DELETE_PAYMENT_TYPE",
                        PaymentTypeDetails = paymentTypeDetails,
                        DeletedBy = UserName,
                        DeletedAt = DateTime.Now
                    };

                    await _blockchainService.CreateAndAddTransactionAsync(
                        "PAYMENT_TYPE_DELETE",
                        null,
                        companyCode,
                        0,
                        "SYSTEM",
                        blockchainData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record blockchain transaction for payment type deletion");
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting payment type {id}");
                throw;
            }
        }

        public async Task<PaymentTypeResponseDTO> GetByIdAsync(int id)
        {
            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Id == id);

            if (paymentType == null) return null;

            string expenseAccountName = null;
            if (!string.IsNullOrEmpty(paymentType.DefaultExpenseAccountNo))
            {
                var expenseAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(g => g.AccNo == paymentType.DefaultExpenseAccountNo);
                expenseAccountName = expenseAccount?.Glaccname;
            }

            return new PaymentTypeResponseDTO
            {
                Id = paymentType.Id,
                Code = paymentType.Code,
                Name = paymentType.Name,
                Description = paymentType.Description,
                IsActive = paymentType.IsActive,
                DisplayOrder = paymentType.DisplayOrder,
                DefaultExpenseAccountNo = paymentType.DefaultExpenseAccountNo,
                DefaultExpenseAccountName = expenseAccountName,
                BlockchainTxId = paymentType.BlockchainTxId,
                CreatedAt = paymentType.AuditTime,
                CreatedBy = paymentType.AuditId
            };
        }

        public async Task<List<PaymentTypeResponseDTO>> GetAllAsync(string companyCode, bool includeInactive = false)
        {
            var query = _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode);

            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive == true);
            }

            var paymentTypes = await query
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var result = new List<PaymentTypeResponseDTO>();
            foreach (var pt in paymentTypes)
            {
                string expenseAccountName = null;
                if (!string.IsNullOrEmpty(pt.DefaultExpenseAccountNo))
                {
                    var expenseAccount = await _context.GlSetup
                        .FirstOrDefaultAsync(g => g.AccNo == pt.DefaultExpenseAccountNo);
                    expenseAccountName = expenseAccount?.Glaccname;
                }

                result.Add(new PaymentTypeResponseDTO
                {
                    Id = pt.Id,
                    Code = pt.Code,
                    Name = pt.Name,
                    Description = pt.Description,
                    IsActive = pt.IsActive,
                    DisplayOrder = pt.DisplayOrder,
                    DefaultExpenseAccountNo = pt.DefaultExpenseAccountNo,
                    DefaultExpenseAccountName = expenseAccountName,
                    BlockchainTxId = pt.BlockchainTxId,
                    CreatedAt = pt.AuditTime,
                    CreatedBy = pt.AuditId
                });
            }

            return result;
        }

        public async Task<List<PaymentTypeSimpleDTO>> GetActivePaymentTypesAsync(string companyCode)
        {
            return await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Name)
                .Select(p => new PaymentTypeSimpleDTO
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    DefaultExpenseAccountNo = p.DefaultExpenseAccountNo
                })
                .ToListAsync();
        }

        public async Task<bool> IsCodeUniqueAsync(string code, string companyCode, int? excludeId = null)
        {
            var query = _context.PaymentTypes
                .Where(p => p.Code == code.ToUpper() && p.CompanyCode == companyCode);

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task<bool> ToggleStatusAsync(int id, string UserName, string companyCode)
        {
            var paymentType = await _context.PaymentTypes
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyCode == companyCode);

            if (paymentType == null)
            {
                throw new InvalidOperationException($"Payment type with ID {id} not found.");
            }

            paymentType.IsActive = !paymentType.IsActive;
            paymentType.AuditId = UserName;
            paymentType.AuditTime = DateTime.Now;

            await _context.SaveChangesAsync();

            // Record blockchain transaction
            try
            {
                var blockchainData = new
                {
                    Action = paymentType.IsActive ? "ACTIVATE_PAYMENT_TYPE" : "DEACTIVATE_PAYMENT_TYPE",
                    PaymentTypeId = paymentType.Id,
                    PaymentTypeCode = paymentType.Code,
                    PaymentTypeName = paymentType.Name,
                    ModifiedBy = UserName,
                    ModifiedAt = DateTime.Now
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    paymentType.IsActive ? "PAYMENT_TYPE_ACTIVATE" : "PAYMENT_TYPE_DEACTIVATE",
                    null,
                    companyCode,
                    0,
                    "SYSTEM",
                    blockchainData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to record blockchain transaction for payment type {(paymentType.IsActive ? "activation" : "deactivation")}");
            }

            return paymentType.IsActive;
        }

        public async Task<Dictionary<int, string>> GetPaymentTypeDictionaryAsync(string companyCode)
        {
            return await _context.PaymentTypes
                .Where(p => p.CompanyCode == companyCode && p.IsActive == true)
                .OrderBy(p => p.DisplayOrder)
                .ToDictionaryAsync(p => p.Id, p => p.Name);
        }
    }
}