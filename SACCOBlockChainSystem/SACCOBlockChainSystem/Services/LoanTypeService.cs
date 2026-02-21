using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SACCOBlockChainSystem.Services
{
    public class LoanTypeService : ILoanTypeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanTypeService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly ICompanyContextService _companyContextService;

        public LoanTypeService(
            ApplicationDbContext context,
            ILogger<LoanTypeService> logger,
            IBlockchainService blockchainService,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _companyContextService = companyContextService;
        }

        public async Task<LoanTypeResponseDTO> CreateLoanTypeAsync(LoanTypeCreateDTO loanTypeDto)
        {
            _logger.LogInformation($"Creating loan type: {loanTypeDto.LoanCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate DTO
                await ValidateLoanTypeAsync(loanTypeDto);

                // Check if loan type already exists
                var existingLoanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanTypeDto.LoanCode &&
                                              lt.CompanyCode == loanTypeDto.CompanyCode);

                if (existingLoanType != null)
                {
                    throw new ValidationException($"Loan type with code '{loanTypeDto.LoanCode}' already exists");
                }

                // Create new loan type
                var loanType = new Loantype
                {
                    LoanCode = loanTypeDto.LoanCode,
                    LoanType1 = loanTypeDto.LoanType,
                    ValueChain = loanTypeDto.ValueChain,
                    LoanProduct = loanTypeDto.LoanProduct,
                    LoanAcc = loanTypeDto.LoanAcc,
                    InterestAcc = loanTypeDto.InterestAcc,
                    PenaltyAcc = loanTypeDto.PenaltyAcc,
                    RepayPeriod = loanTypeDto.RepayPeriod,
                    Interest = loanTypeDto.Interest,
                    MaxAmount = loanTypeDto.MaxAmount,
                    Guarantor = loanTypeDto.Guarantor,
                    UseintRange = loanTypeDto.UseIntRange,
                    EarningRation = loanTypeDto.EarningRatio,
                    Penalty = loanTypeDto.Penalty,
                    Processingfee = loanTypeDto.ProcessingFee,
                    GracePeriod = loanTypeDto.GracePeriod,
                    Repaymethod = loanTypeDto.RepayMethod,
                    Bridging = loanTypeDto.Bridging,
                    SelfGuarantee = loanTypeDto.SelfGuarantee,
                    MobileLoan = loanTypeDto.MobileLoan,
                    Ppacc = loanTypeDto.Ppacc,
                    ContraAccount = loanTypeDto.ContraAccount,
                    Priority = loanTypeDto.Priority,
                    MaxLoans = loanTypeDto.MaxLoans,
                    CompanyCode = loanTypeDto.CompanyCode,
                    AuditId = loanTypeDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    // Set default values for required fields from model
                    AccruedAcc = "000000", // Default, should be configured
                    Mdtei = 0,
                    Intrecovery = "000000", // Default, should be configured
                    IsMain = true,
                    ReceivableAcc = "000000" // Default, should be configured
                };

                _context.Loantypes.Add(loanType);
                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = loanType.CompanyCode,
                    CreatedBy = loanTypeDto.CreatedBy,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Properties = new
                    {
                        loanType.MaxAmount,
                        loanType.RepayPeriod,
                        loanType.Interest,
                        loanType.Bridging,
                        loanType.MobileLoan,
                        loanType.Priority
                    }
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_TYPE_CREATE",
                    "SYSTEM",
                    loanTypeDto.CompanyCode,
                    0,
                    loanType.LoanCode,
                    blockchainData
                );

                if (blockchainTx != null)
                {
                    _logger.LogInformation($"Blockchain transaction created: {blockchainTx.TransactionId}");
                }

                await transaction.CommitAsync();
                _logger.LogInformation($"Loan type {loanType.LoanCode} created successfully");

                // Return response DTO
                return await GetLoanTypeResponseDto(loanType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating loan type {loanTypeDto.LoanCode}");
                throw;
            }
        }

        public async Task<LoanTypeResponseDTO> UpdateLoanTypeAsync(string loanCode, LoanTypeUpdateDTO loanTypeDto)
        {
            _logger.LogInformation($"Updating loan type: {loanCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get existing loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode &&
                                              lt.CompanyCode == loanTypeDto.CompanyCode);

                if (loanType == null)
                {
                    throw new KeyNotFoundException($"Loan type '{loanCode}' not found");
                }

                // Check if loan type is in use
                var usageCount = await GetLoanTypeUsageCountAsync(loanCode, loanTypeDto.CompanyCode);
                if (usageCount > 0)
                {
                    // Validate that critical fields aren't being changed when in use
                    if (loanType.Bridging != loanTypeDto.Bridging ||
                        loanType.MobileLoan != loanTypeDto.MobileLoan ||
                        loanType.SelfGuarantee != loanTypeDto.SelfGuarantee)
                    {
                        throw new ValidationException(
                            "Cannot change critical properties when loan type is in use by members");
                    }
                }

                // Update fields
                loanType.LoanType1 = loanTypeDto.LoanType;
                loanType.ValueChain = loanTypeDto.ValueChain;
                loanType.LoanProduct = loanTypeDto.LoanProduct;
                loanType.LoanAcc = loanTypeDto.LoanAcc;
                loanType.InterestAcc = loanTypeDto.InterestAcc;
                loanType.PenaltyAcc = loanTypeDto.PenaltyAcc;
                loanType.RepayPeriod = loanTypeDto.RepayPeriod;
                loanType.Interest = loanTypeDto.Interest;
                loanType.MaxAmount = loanTypeDto.MaxAmount;
                loanType.Guarantor = loanTypeDto.Guarantor;
                loanType.UseintRange = loanTypeDto.UseIntRange;
                loanType.EarningRation = loanTypeDto.EarningRatio;
                loanType.Penalty = loanTypeDto.Penalty;
                loanType.Processingfee = loanTypeDto.ProcessingFee;
                loanType.GracePeriod = loanTypeDto.GracePeriod;
                loanType.Repaymethod = loanTypeDto.RepayMethod;
                loanType.Bridging = loanTypeDto.Bridging;
                loanType.SelfGuarantee = loanTypeDto.SelfGuarantee;
                loanType.MobileLoan = loanTypeDto.MobileLoan;
                loanType.Ppacc = loanTypeDto.Ppacc;
                loanType.ContraAccount = loanTypeDto.ContraAccount;
                loanType.Priority = loanTypeDto.Priority;
                loanType.MaxLoans = loanTypeDto.MaxLoans;
                loanType.AuditId = loanTypeDto.UpdatedBy;
                loanType.AuditTime = DateTime.Now;
                loanType.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Create blockchain transaction for update
                var blockchainData = new
                {
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = loanType.CompanyCode,
                    UpdatedBy = loanTypeDto.UpdatedBy,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UpdatedFields = new
                    {
                        OldName = loanType.LoanType1,
                        NewName = loanTypeDto.LoanType,
                        OldMaxAmount = loanType.MaxAmount,
                        NewMaxAmount = loanTypeDto.MaxAmount
                    }
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_TYPE_UPDATE",
                    "SYSTEM",
                    loanTypeDto.CompanyCode,
                    0,
                    loanType.LoanCode,
                    blockchainData
                );

                await transaction.CommitAsync();
                _logger.LogInformation($"Loan type {loanCode} updated successfully");

                return await GetLoanTypeResponseDto(loanType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating loan type {loanCode}");
                throw;
            }
        }

        public async Task<bool> DeleteLoanTypeAsync(string loanCode, string companyCode)
        {
            _logger.LogInformation($"Deleting loan type: {loanCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode &&
                                              lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    throw new KeyNotFoundException($"Loan type '{loanCode}' not found");
                }

                // Check if loan type is in use
                var usageCount = await GetLoanTypeUsageCountAsync(loanCode, companyCode);
                if (usageCount > 0)
                {
                    throw new ValidationException(
                        $"Cannot delete loan type '{loanCode}' because it's used by {usageCount} loan(s)");
                }

                // Create blockchain transaction before deletion
                var blockchainData = new
                {
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = companyCode,
                    DeletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DeletedBy = "SYSTEM"
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_TYPE_DELETE",
                    "SYSTEM",
                    companyCode,
                    0,
                    loanCode,
                    blockchainData
                );

                // Delete from database
                _context.Loantypes.Remove(loanType);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation($"Loan type {loanCode} deleted successfully");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting loan type {loanCode}");
                throw;
            }
        }

        public async Task<LoanTypeResponseDTO> GetLoanTypeByCodeAsync(string loanCode, string companyCode)
        {
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode &&
                                          lt.CompanyCode == companyCode);

            if (loanType == null)
            {
                throw new KeyNotFoundException($"Loan type '{loanCode}' not found");
            }

            return await GetLoanTypeResponseDto(loanType);
        }

        public async Task<List<LoanTypeResponseDTO>> GetLoanTypesByCompanyAsync(string companyCode)
        {
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode)
                .OrderBy(lt => lt.Priority)
                .ThenBy(lt => lt.LoanType1)
                .ToListAsync();

            var result = new List<LoanTypeResponseDTO>();
            foreach (var loanType in loanTypes)
            {
                result.Add(await GetLoanTypeResponseDto(loanType));
            }

            return result;
        }

        public async Task<List<LoanTypeSimpleDTO>> GetActiveLoanTypesAsync(string companyCode)
        {
            return await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode)
                .OrderBy(lt => lt.Priority)
                .Select(lt => new LoanTypeSimpleDTO
                {
                    LoanCode = lt.LoanCode,
                    LoanType = lt.LoanType1,
                    MaxAmount = lt.MaxAmount,
                    RepayPeriod = lt.RepayPeriod,
                    Interest = lt.Interest,
                    Bridging = lt.Bridging,
                    MobileLoan = lt.MobileLoan ?? false,
                    Priority = lt.Priority ?? 1
                })
                .ToListAsync();
        }

        public async Task<List<LoanTypeResponseDTO>> SearchLoanTypesAsync(string searchTerm, string companyCode)
        {
            var query = _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(lt =>
                    lt.LoanCode.Contains(searchTerm) ||
                    lt.LoanType1.Contains(searchTerm) ||
                    (lt.LoanProduct != null && lt.LoanProduct.Contains(searchTerm)));
            }

            var loanTypes = await query
                .OrderBy(lt => lt.Priority)
                .ToListAsync();

            var result = new List<LoanTypeResponseDTO>();
            foreach (var loanType in loanTypes)
            {
                result.Add(await GetLoanTypeResponseDto(loanType));
            }

            return result;
        }

        public async Task<bool> ValidateLoanTypeAsync(LoanTypeCreateDTO loanTypeDto)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(loanTypeDto.LoanCode))
                throw new ValidationException("Loan code is required");

            if (string.IsNullOrWhiteSpace(loanTypeDto.LoanType))
                throw new ValidationException("Loan type name is required");

            if (string.IsNullOrWhiteSpace(loanTypeDto.LoanAcc))
                throw new ValidationException("Loan account is required");

            if (string.IsNullOrWhiteSpace(loanTypeDto.Ppacc))
                throw new ValidationException("PP Account is required");

            if (string.IsNullOrWhiteSpace(loanTypeDto.ContraAccount))
                throw new ValidationException("Contra account is required");

            if (loanTypeDto.Priority < 1 || loanTypeDto.Priority > 10)
                throw new ValidationException("Priority must be between 1 and 10");

            if (loanTypeDto.GracePeriod < 0)
                throw new ValidationException("Grace period cannot be negative");

            // Check for duplicate loan code in the same company
            var existing = await _context.Loantypes
                .FirstOrDefaultAsync(lt => lt.LoanCode == loanTypeDto.LoanCode &&
                                          lt.CompanyCode == loanTypeDto.CompanyCode);
            if (existing != null)
            {
                throw new ValidationException($"Loan code '{loanTypeDto.LoanCode}' already exists");
            }

            return true;
        }

        public async Task<int> GetLoanTypeUsageCountAsync(string loanCode, string companyCode)
        {
            // Count loans using this loan type
            return await _context.Loans
                .CountAsync(l => l.LoanCode == loanCode &&
                               l.CompanyCode == companyCode);
        }

        public async Task<List<LoanTypeSimpleDTO>> GetLoanTypesForMemberAsync(string memberNo, string companyCode)
        {
            // Get member's total shares
            var totalShares = await _context.Shares
                .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                .SumAsync(s => s.TotalShares ?? 0);

            // Get all active loan types
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode &&
                            (lt.MobileLoan == null || lt.MobileLoan == true))
                .OrderBy(lt => lt.Priority)
                .ToListAsync();

            // Calculate eligibility for each loan type
            var result = new List<LoanTypeSimpleDTO>();
            foreach (var loanType in loanTypes)
            {
                // Check if member is eligible based on shares
                decimal? maxEligibleAmount = null;

                if (loanType.MaxAmount.HasValue)
                {
                    maxEligibleAmount = loanType.MaxAmount;

                    // If there's an earning ratio, calculate based on shares
                    if (loanType.EarningRation.HasValue && loanType.EarningRation > 0)
                    {
                        var calculatedAmount = totalShares * (decimal?)loanType.EarningRation;
                        if (calculatedAmount.HasValue && calculatedAmount < maxEligibleAmount)
                        {
                            maxEligibleAmount = calculatedAmount;
                        }
                    }
                }

                result.Add(new LoanTypeSimpleDTO
                {
                    LoanCode = loanType.LoanCode,
                    LoanType = loanType.LoanType1,
                    MaxAmount = maxEligibleAmount,
                    RepayPeriod = loanType.RepayPeriod,
                    Interest = loanType.Interest,
                    Bridging = loanType.Bridging,
                    MobileLoan = loanType.MobileLoan ?? false,
                    Priority = loanType.Priority ?? 1
                });
            }

            return result;
        }

        private async Task<LoanTypeResponseDTO> GetLoanTypeResponseDto(Loantype loanType)
        {
            // Get usage statistics
            var totalLoans = await _context.Loans
                .CountAsync(l => l.LoanCode == loanType.LoanCode &&
                               l.CompanyCode == loanType.CompanyCode);

            var activeLoans = await _context.Loans
                .CountAsync(l => l.LoanCode == loanType.LoanCode &&
                               l.CompanyCode == loanType.CompanyCode &&
                               (l.LoanStatus == "Approved" || l.LoanStatus == "Disbursed")); // Active or approved status

            var totalLoanAmount = await _context.Loans
                .Where(l => l.LoanCode == loanType.LoanCode &&
                          l.CompanyCode == loanType.CompanyCode)
                .SumAsync(l => l.ApprovedAmount);

            return new LoanTypeResponseDTO
            {
                LoanCode = loanType.LoanCode,
                LoanType = loanType.LoanType1,
                ValueChain = loanType.ValueChain,
                LoanProduct = loanType.LoanProduct,
                LoanAcc = loanType.LoanAcc,
                InterestAcc = loanType.InterestAcc,
                PenaltyAcc = loanType.PenaltyAcc,
                RepayPeriod = loanType.RepayPeriod,
                Interest = loanType.Interest,
                MaxAmount = loanType.MaxAmount,
                Guarantor = loanType.Guarantor,
                UseIntRange = loanType.UseintRange,
                EarningRatio = loanType.EarningRation,
                Penalty = loanType.Penalty,
                ProcessingFee = loanType.Processingfee,
                GracePeriod = loanType.GracePeriod,
                RepayMethod = loanType.Repaymethod,
                Bridging = loanType.Bridging,
                SelfGuarantee = loanType.SelfGuarantee ?? false,
                MobileLoan = loanType.MobileLoan ?? false,
                Ppacc = loanType.Ppacc,
                ContraAccount = loanType.ContraAccount,
                MinLoanAmount = null, // Not in model, can be added if needed
                MaxLoanAmount = loanType.MaxAmount,
                MaxLoans = loanType.MaxLoans,
                Priority = loanType.Priority ?? 1,
                CompanyCode = loanType.CompanyCode,
                CreatedBy = loanType.AuditId,
                CreatedAt = loanType.AuditDateTime,
                UpdatedAt = loanType.AuditDateTime,
                TotalLoans = totalLoans,
                TotalLoanAmount = totalLoanAmount,
                ActiveLoans = activeLoans
            };
        }

        public async Task<dynamic> GetAllLoanTypesAsync(string companyCode)
        {
            try
            {
                var loanTypes = await _context.Loantypes
                    .Where(lt => lt.CompanyCode == companyCode)
                    .OrderBy(lt => lt.Priority)
                    .ThenBy(lt => lt.LoanType1)
                    .Select(lt => new
                    {
                        LoanCode = lt.LoanCode,
                        LoanType = lt.LoanType1,
                        LoanName = lt.LoanType1 ?? lt.LoanCode,
                        MaxAmount = lt.MaxAmount,
                        RepayPeriod = lt.RepayPeriod,
                        Interest = lt.Interest,
                        Bridging = lt.Bridging,
                        MobileLoan = lt.MobileLoan,
                        Priority = lt.Priority,
                        Guarantor = lt.Guarantor,
                        SelfGuarantee = lt.SelfGuarantee,
                        ProcessingFee = lt.Processingfee,
                        GracePeriod = lt.GracePeriod,
                        RepayMethod = lt.Repaymethod,
                        CompanyCode = lt.CompanyCode
                    })
                    .ToListAsync();

                return loanTypes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting all loan types for company {companyCode}");
                throw;
            }
        }
    }
}