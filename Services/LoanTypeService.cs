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
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Services
{
    public class LoanTypeService : ILoanTypeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanTypeService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly ICompanyContextService _companyContextService;
        private readonly AuditTrailService _auditService;

        public LoanTypeService(
            ApplicationDbContext context,
            ILogger<LoanTypeService> logger,
            IBlockchainService blockchainService,
            AuditTrailService auditService,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _auditService = auditService;
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

                // Validate accounts exist
                await ValidateAccountsExist(loanTypeDto);

                // Create new loan type with Pending status
                var loanType = new Loantype
                {
                    LoanCode = loanTypeDto.LoanCode,
                    LoanType1 = loanTypeDto.LoanType,
                    ValueChain = loanTypeDto.ValueChain,
                    LoanProduct = loanTypeDto.LoanType,
                    LoanAcc = loanTypeDto.LoanAcc,
                    InterestAcc = loanTypeDto.InterestAcc,
                    PenaltyAcc = loanTypeDto.PenaltyAcc,
                    RepayPeriod = loanTypeDto.RepayPeriod,
                    Interest = loanTypeDto.Interest,
                    MaxAmount = loanTypeDto.MaxAmount,
                    Guarantor = loanTypeDto.Guarantor,
                    UseintRange = loanTypeDto.UseIntRange,
                    EarningRation = loanTypeDto.EarningRatio,
                    Penalty = loanTypeDto.Penalty ? 1 : 0,
                    Processingfee = loanTypeDto.ProcessingFee,
                    GracePeriod = loanTypeDto.GracePeriod,
                    Repaymethod = loanTypeDto.RepayMethod,
                    Bridging = loanTypeDto.Bridging ? 1 : 0,
                    SelfGuarantee = loanTypeDto.SelfGuarantee,
                    MobileLoan = loanTypeDto.MobileLoan,
                    IsProject = loanTypeDto.IsProject,
                    MobileLoanApproval = loanTypeDto.MobileLoanApproval,
                    Ppacc = loanTypeDto.Ppacc ?? string.Empty,
                    ContraAccount = loanTypeDto.ContraAccount ?? string.Empty,
                    Priority = loanTypeDto.Priority,
                    MaxLoans = loanTypeDto.MaxLoans,
                    CompanyCode = loanTypeDto.CompanyCode,
                    AuditId = loanTypeDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    AccruedAcc = "000000",
                    Mdtei = 0,
                    Intrecovery = "000000",
                    IsMain = true,
                    ReceivableAcc = "000000",
                    MinimumPaidForBridging = 0,
                    MinimumPaidForTopup = 0,
                    ApprovalStatus = "Pending"
                };

                _context.Loantypes.Add(loanType);
                await _context.SaveChangesAsync();

                // ========== SAVE PENALTY CONFIGURATION ==========
                bool shouldSavePenalty = loanTypeDto.Penalty || (loanTypeDto.PenaltyMode != null && loanTypeDto.PenaltyValue > 0);
                if (shouldSavePenalty)
                {
                    var penalty = new Penalties
                    {
                        LoanCode = loanType.LoanCode,
                        CompanyCode = loanTypeDto.CompanyCode,
                        Penalty = 1,
                        Mode = loanTypeDto.PenaltyMode ?? "Percentage",
                        Rate = loanTypeDto.PenaltyRateType ?? "Monthly",
                        Value = loanTypeDto.PenaltyValue,
                        ChargeItem = loanTypeDto.PenaltyChargeItem
                    };
                    _context.Penalties.Add(penalty);
                    await _context.SaveChangesAsync();
                }


                // ========== CREATE BLOCK AND BLOCKCHAIN TRANSACTION ==========
                string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    TransactionType = "LOAN_TYPE_CREATE",
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = loanType.CompanyCode,
                    Properties = new
                    {
                        loanType.MaxAmount,
                        loanType.RepayPeriod,
                        loanType.Interest,
                        loanType.Bridging,
                        loanType.LoanProduct,
                        loanType.MobileLoan,
                        loanType.IsProject,
                        loanType.Priority,
                        loanType.Guarantor,
                        loanType.SelfGuarantee,
                        loanType.Processingfee,
                        loanType.GracePeriod,
                        loanType.Repaymethod,
                        loanType.ApprovalStatus,
                        loanType.LoanAcc,
                        loanType.InterestAcc,
                        loanType.PenaltyAcc,
                        loanType.Ppacc,
                        loanType.ContraAccount
                    },
                    PenaltyConfiguration = loanTypeDto.Penalty ? new
                    {
                        Mode = loanTypeDto.PenaltyMode,
                        Rate = loanTypeDto.PenaltyRateType,
                        Value = loanTypeDto.PenaltyValue,
                        ChargeItem = loanTypeDto.PenaltyChargeItem
                    } : null,
                    CreatedBy = loanTypeDto.CreatedBy,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    BlockHash = blockHash
                };

                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_TYPE_CREATE",
                    MemberNo = null,
                    CompanyCode = loanType.CompanyCode,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanType.LoanCode,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                loanType.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // ========== SAVE AUDIT TRAIL ==========
                var auditExtraData = new
                {
                    loanTypeCode = loanType.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    maxAmount = loanType.MaxAmount,
                    repayPeriod = loanType.RepayPeriod,
                    loanproduct = loanType.LoanProduct,
                    interest = loanType.Interest,
                    guarantor = loanType.Guarantor,
                    selfGuarantee = loanType.SelfGuarantee,
                    processingFee = loanType.Processingfee,
                    gracePeriod = loanType.GracePeriod,
                    repayMethod = loanType.Repaymethod,
                    bridging = loanType.Bridging,
                    mobileLoan = loanType.MobileLoan,
                    isProject = loanType.IsProject,
                    priority = loanType.Priority,
                    maxLoans = loanType.MaxLoans,
                    loanAccount = loanType.LoanAcc,
                    interestAccount = loanType.InterestAcc,
                    penaltyAccount = loanType.PenaltyAcc,
                    ppAccount = loanType.Ppacc,
                    contraAccount = loanType.ContraAccount,
                    approvalStatus = loanType.ApprovalStatus,
                    penaltyConfiguration = loanTypeDto.Penalty ? new
                    {
                        loanTypeDto.PenaltyMode,
                        loanTypeDto.PenaltyRateType,
                        loanTypeDto.PenaltyValue,
                        loanTypeDto.PenaltyChargeItem
                    } : null,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanTypeForAudit = new
                {
                    loanType.LoanCode,
                    loanType.LoanType1,
                    loanType.ValueChain,
                    loanType.LoanProduct,
                    loanType.LoanAcc,
                    loanType.InterestAcc,
                    loanType.PenaltyAcc,
                    loanType.RepayPeriod,
                    loanType.Interest,
                    loanType.MaxAmount,
                    loanType.Guarantor,
                    loanType.Processingfee,
                    loanType.GracePeriod,
                    loanType.Repaymethod,
                    loanType.Bridging,
                    loanType.SelfGuarantee,
                    loanType.MobileLoan,
                    loanType.IsProject,
                    loanType.Ppacc,
                    loanType.ContraAccount,
                    loanType.Priority,
                    loanType.MaxLoans,
                    loanType.CompanyCode,
                    BlockchainTxId = blockchainTx.TransactionId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = loanTypeDto.CreatedBy,
                    PenaltyConfiguration = loanTypeDto.Penalty ? new
                    {
                        loanTypeDto.PenaltyMode,
                        loanTypeDto.PenaltyRateType,
                        loanTypeDto.PenaltyValue,
                        loanTypeDto.PenaltyChargeItem
                    } : null
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: loanTypeForAudit,
                    tableName: "Loantypes",
                    recordId: loanType.LoanCode,
                    userId: loanTypeDto.CreatedBy,
                    userName: loanTypeDto.CreatedBy,
                    companyCode: loanTypeDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();
                _logger.LogInformation($"Loan type {loanType.LoanCode} created successfully with Pending status, BlockchainTxId: {blockchainTx.TransactionId}");

                return await GetLoanTypeResponseDto(loanType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating loan type {loanTypeDto.LoanCode}");
                throw;
            }
        }

        private async Task SavePenaltyConfigurationAsync( string loanCode,string companyCode, bool attractsPenalty,string penaltyMode,string penaltyRateType, decimal penaltyValue, short penaltyChargeItem)
        {
            _logger.LogInformation($"Saving penalty configuration for loan type: {loanCode}");
            _logger.LogInformation($"AttractsPenalty: {attractsPenalty}, Mode: {penaltyMode}, Rate: {penaltyRateType}, Value: {penaltyValue}, ChargeItem: {penaltyChargeItem}");

            // Check if penalty configuration already exists
            var existingPenalty = await _context.Penalties
                .FirstOrDefaultAsync(p => p.LoanCode == loanCode && p.CompanyCode == companyCode);

            if (existingPenalty != null)
            {
                _logger.LogInformation($"Updating existing penalty configuration for {loanCode}");

                if (attractsPenalty)
                {
                    // Update existing
                    existingPenalty.Penalty = 1;
                    existingPenalty.Mode = penaltyMode;
                    existingPenalty.Rate = penaltyRateType;
                    existingPenalty.Value = penaltyValue;
                    existingPenalty.ChargeItem = penaltyChargeItem;
                    _context.Penalties.Update(existingPenalty);
                }
                else
                {
                    // Deactivate penalty
                    existingPenalty.Penalty = 0;
                    _context.Penalties.Update(existingPenalty);
                }
            }
            else if (attractsPenalty)
            {
                _logger.LogInformation($"Creating new penalty configuration for {loanCode}");

                // Create new
                var penalty = new Penalties
                {
                    LoanCode = loanCode,
                    CompanyCode = companyCode,
                    Penalty = 1,
                    Mode = penaltyMode,
                    Rate = penaltyRateType,
                    Value = penaltyValue,
                    ChargeItem = penaltyChargeItem
                };
                await _context.Penalties.AddAsync(penalty);
            }
            else
            {
                _logger.LogInformation($"No penalty configuration to save for {loanCode} (attractsPenalty = false)");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Penalty configuration saved successfully for {loanCode}");
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

                // Check if loan type has been used (has any loans)
                var usageCount = await GetLoanTypeUsageCountAsync(loanCode, loanTypeDto.CompanyCode);
                bool isUsed = usageCount > 0;

                // Get existing penalty configuration for old values
                var existingPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.LoanCode == loanCode && p.CompanyCode == loanTypeDto.CompanyCode);

                // ============================================================
                // LOG THE INCOMING DTO VALUES FOR DEBUGGING
                // ============================================================
                _logger.LogInformation($"=== UPDATE LOAN TYPE DTO VALUES ===");
                _logger.LogInformation($"LoanCode: {loanCode}");
                _logger.LogInformation($"Penalty (bool): {loanTypeDto.Penalty}");
                _logger.LogInformation($"AttractsPenalty: {loanTypeDto.AttractsPenalty}");
                _logger.LogInformation($"PenaltyMode: {loanTypeDto.PenaltyMode}");
                _logger.LogInformation($"PenaltyRateType: {loanTypeDto.PenaltyRateType}");
                _logger.LogInformation($"PenaltyValue: {loanTypeDto.PenaltyValue}");
                _logger.LogInformation($"PenaltyChargeItem: {loanTypeDto.PenaltyChargeItem}");
                _logger.LogInformation($"=====================================");

                // Store old values for audit and blockchain
                var oldValues = new
                {
                    loanType.LoanCode,
                    loanType.LoanType1,
                    loanType.ValueChain,
                    loanType.LoanProduct,
                    loanType.LoanAcc,
                    loanType.InterestAcc,
                    loanType.PenaltyAcc,
                    loanType.RepayPeriod,
                    loanType.Interest,
                    loanType.MaxAmount,
                    loanType.Guarantor,
                    loanType.UseintRange,
                    loanType.EarningRation,
                    loanType.Penalty,
                    loanType.Processingfee,
                    loanType.GracePeriod,
                    loanType.Repaymethod,
                    loanType.Bridging,
                    loanType.SelfGuarantee,
                    loanType.MobileLoan,
                    loanType.IsProject,
                    loanType.Ppacc,
                    loanType.ContraAccount,
                    loanType.Priority,
                    loanType.MaxLoans,
                    loanType.ApprovalStatus,
                    // Old penalty values
                    PenaltyMode = existingPenalty?.Mode,
                    PenaltyRateType = existingPenalty?.Rate,
                    PenaltyValue = existingPenalty?.Value,
                    PenaltyChargeItem = existingPenalty?.ChargeItem,
                    AttractsPenalty = existingPenalty?.Penalty == 1
                };

                // CRITICAL: LoanCode CANNOT be changed
                if (!string.IsNullOrEmpty(loanTypeDto.LoanCode) && loanTypeDto.LoanCode != loanCode)
                {
                    _logger.LogWarning($"Attempted to change LoanCode from {loanCode} to {loanTypeDto.LoanCode} - ignored");
                }

                // Update loan type fields (all fields are updatable)
                loanType.LoanType1 = loanTypeDto.LoanType;
                loanType.ValueChain = loanTypeDto.ValueChain;
                loanType.LoanProduct = loanTypeDto.LoanType;
                loanType.LoanAcc = loanTypeDto.LoanAcc;
                loanType.InterestAcc = loanTypeDto.InterestAcc;
                loanType.PenaltyAcc = loanTypeDto.PenaltyAcc;
                loanType.RepayPeriod = loanTypeDto.RepayPeriod;
                loanType.Interest = loanTypeDto.Interest;
                loanType.MaxAmount = loanTypeDto.MaxAmount;
                loanType.Guarantor = loanTypeDto.Guarantor;
                loanType.UseintRange = loanTypeDto.UseIntRange;
                loanType.EarningRation = loanTypeDto.EarningRatio;
                bool attractsPenaltyFlag = loanTypeDto.AttractsPenalty || loanTypeDto.Penalty;
                loanType.Penalty = attractsPenaltyFlag ? 1 : 0;
                loanType.Processingfee = loanTypeDto.ProcessingFee;
                loanType.GracePeriod = loanTypeDto.GracePeriod;
                loanType.Repaymethod = loanTypeDto.RepayMethod;
                loanType.Bridging = loanTypeDto.Bridging ? 1 : 0;
                loanType.SelfGuarantee = loanTypeDto.SelfGuarantee;
                loanType.MobileLoan = loanTypeDto.MobileLoan;
                loanType.IsProject = loanTypeDto.IsProject;
                loanType.MobileLoanApproval = loanTypeDto.MobileLoanApproval;
                loanType.Ppacc = loanTypeDto.Ppacc ?? string.Empty;
                loanType.ContraAccount = loanTypeDto.ContraAccount ?? string.Empty;
                loanType.Priority = loanTypeDto.Priority;
                loanType.MaxLoans = loanTypeDto.MaxLoans;

                // Update audit fields
                loanType.AuditId = loanTypeDto.UpdatedBy;
                loanType.AuditTime = DateTime.Now;
                loanType.AuditDateTime = DateTime.Now;

                // Handle approval status
                string oldApprovalStatus = loanType.ApprovalStatus;
                bool approvalStatusChanged = false;

                if (!isUsed && loanType.ApprovalStatus == "Approved")
                {
                    loanType.ApprovalStatus = "Pending";
                    approvalStatusChanged = true;
                    _logger.LogInformation($"Loan type {loanCode} reset to Pending status for re-approval (unused type)");
                }
                else if (isUsed)
                {
                    _logger.LogInformation($"Loan type {loanCode} is in use - keeping status as {loanType.ApprovalStatus}");
                }

                await _context.SaveChangesAsync();

                // ========== SAVE PENALTY CONFIGURATION ==========
                // Use AttractsPenalty from DTO to determine if penalty should be saved
                bool attractsPenalty = loanTypeDto.AttractsPenalty || loanTypeDto.Penalty;
                string penaltyMode = loanTypeDto.PenaltyMode ?? "Percentage";
                string penaltyRateType = loanTypeDto.PenaltyRateType ?? "Monthly";
                decimal penaltyValue = loanTypeDto.PenaltyValue;
                short penaltyChargeItem = loanTypeDto.PenaltyChargeItem;

                _logger.LogInformation($"Calling SavePenaltyConfigurationAsync with: attractsPenalty={attractsPenalty}, penaltyMode={penaltyMode}, penaltyRateType={penaltyRateType}, penaltyValue={penaltyValue}, penaltyChargeItem={penaltyChargeItem}");

                await SavePenaltyConfigurationAsync(
                    loanCode,
                    loanTypeDto.CompanyCode,
                    attractsPenalty,
                    penaltyMode,
                    penaltyRateType,
                    penaltyValue,
                    penaltyChargeItem
                );

                // ============================================================
                // CREATE BLOCK AND BLOCKCHAIN TRANSACTION FOR UPDATE
                // ============================================================
                string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();
                string previousHash = lastBlock?.BlockHash ?? "0".PadLeft(64, '0');

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();

                // Get updated penalty config for new values
                var updatedPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.LoanCode == loanCode && p.CompanyCode == loanTypeDto.CompanyCode);

                var newValues = new
                {
                    loanType.LoanType1,
                    loanType.ValueChain,
                    loanType.LoanProduct,
                    loanType.LoanAcc,
                    loanType.InterestAcc,
                    loanType.PenaltyAcc,
                    loanType.RepayPeriod,
                    loanType.Interest,
                    loanType.MaxAmount,
                    loanType.Guarantor,
                    loanType.UseintRange,
                    loanType.EarningRation,
                    loanType.Penalty,
                    loanType.Processingfee,
                    loanType.GracePeriod,
                    loanType.Repaymethod,
                    loanType.Bridging,
                    loanType.SelfGuarantee,
                    loanType.MobileLoan,
                    loanType.IsProject,
                    loanType.Ppacc,
                    loanType.ContraAccount,
                    loanType.Priority,
                    loanType.MaxLoans,
                    loanType.ApprovalStatus,
                    // New penalty values
                    PenaltyMode = updatedPenalty?.Mode,
                    PenaltyRateType = updatedPenalty?.Rate,
                    PenaltyValue = updatedPenalty?.Value,
                    PenaltyChargeItem = updatedPenalty?.ChargeItem,
                    AttractsPenalty = updatedPenalty?.Penalty == 1
                };

                var blockchainData = new
                {
                    TransactionType = "LOAN_TYPE_UPDATE",
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = loanType.CompanyCode,
                    UsageCount = usageCount,
                    IsUsed = isUsed,
                    OldValues = oldValues,
                    NewValues = newValues,
                    UpdatedBy = loanTypeDto.UpdatedBy,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    OldApprovalStatus = oldApprovalStatus,
                    NewApprovalStatus = loanType.ApprovalStatus,
                    ApprovalStatusChanged = approvalStatusChanged,
                    BlockHash = blockHash
                };

                string dataHash = await GenerateTransactionHashAsync(blockchainData);

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_TYPE_UPDATE",
                    MemberNo = null,
                    CompanyCode = loanType.CompanyCode,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = dataHash,
                    PayloadJson = JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanType.LoanCode,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                loanType.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // ========== SAVE AUDIT TRAIL ==========
                var auditExtraData = new
                {
                    loanTypeCode = loanType.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    Loanproduct = loanType.LoanProduct,
                    usageCount = usageCount,
                    isUsed = isUsed,
                    note = isUsed ? "Changes will only affect NEW loan applications" : "Full update allowed",
                    maxAmount = loanType.MaxAmount,
                    repayPeriod = loanType.RepayPeriod,
                    interest = loanType.Interest,
                    guarantor = loanType.Guarantor,
                    selfGuarantee = loanType.SelfGuarantee,
                    processingFee = loanType.Processingfee,
                    gracePeriod = loanType.GracePeriod,
                    repayMethod = loanType.Repaymethod,
                    bridging = loanType.Bridging,
                    mobileLoan = loanType.MobileLoan,
                    isProject = loanType.IsProject,
                    priority = loanType.Priority,
                    maxLoans = loanType.MaxLoans,
                    oldApprovalStatus = oldApprovalStatus,
                    newApprovalStatus = loanType.ApprovalStatus,
                    approvalStatusChanged = approvalStatusChanged,
                    penaltyConfiguration = new
                    {
                        attractsPenalty = attractsPenalty,
                        penaltyMode = penaltyMode,
                        penaltyRateType = penaltyRateType,
                        penaltyValue = penaltyValue,
                        penaltyChargeItem = penaltyChargeItem,
                        oldPenaltyMode = existingPenalty?.Mode,
                        newPenaltyMode = updatedPenalty?.Mode,
                        oldPenaltyValue = existingPenalty?.Value,
                        newPenaltyValue = updatedPenalty?.Value,
                        penaltyChanged = existingPenalty?.Mode != updatedPenalty?.Mode ||
                                        existingPenalty?.Value != updatedPenalty?.Value
                    },
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanTypeForAudit = new
                {
                    loanType.LoanCode,
                    loanType.LoanType1,
                    loanType.ValueChain,
                    loanType.LoanProduct,
                    loanType.LoanAcc,
                    loanType.InterestAcc,
                    loanType.PenaltyAcc,
                    loanType.RepayPeriod,
                    loanType.Interest,
                    loanType.MaxAmount,
                    loanType.Guarantor,
                    loanType.Processingfee,
                    loanType.GracePeriod,
                    loanType.Repaymethod,
                    loanType.Bridging,
                    loanType.SelfGuarantee,
                    loanType.MobileLoan,
                    loanType.IsProject,
                    loanType.Ppacc,
                    loanType.ContraAccount,
                    loanType.Priority,
                    loanType.MaxLoans,
                    loanType.ApprovalStatus,
                    loanType.CompanyCode,
                    BlockchainTxId = blockchainTx.TransactionId,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = loanTypeDto.UpdatedBy,
                    IsUsed = isUsed,
                    PenaltyConfiguration = updatedPenalty != null ? new
                    {
                        updatedPenalty.Mode,
                        updatedPenalty.Rate,
                        updatedPenalty.Value,
                        updatedPenalty.ChargeItem,
                        IsActive = updatedPenalty.Penalty == 1
                    } : null
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: oldValues,
                    newModel: loanTypeForAudit,
                    tableName: "Loantypes",
                    recordId: loanType.LoanCode,
                    userId: loanTypeDto.UpdatedBy,
                    userName: loanTypeDto.UpdatedBy,
                    companyCode: loanTypeDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                string updateMessage = isUsed
                    ? $"Loan type {loanCode} updated successfully. Changes will only affect NEW loan applications. Existing loans remain unchanged."
                    : $"Loan type {loanCode} updated successfully! It has been reset to PENDING status and needs to be approved again.";

                _logger.LogInformation(updateMessage);
                _logger.LogInformation($"BlockchainTxId: {blockchainTx.TransactionId}");

                return await GetLoanTypeResponseDto(loanType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating loan type {loanCode}");
                throw;
            }
        }


        private async Task<string> GenerateTransactionHashAsync(object data)
        {
            try
            {
                var jsonData = JsonSerializer.Serialize(data);
                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(jsonData);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hash).ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transaction hash");
                return Guid.NewGuid().ToString().Replace("-", "");
            }
        }        


        public async Task<LoanTypeResponseDTO> ApproveLoanTypeAsync( string loanCode, string companyCode,string approvedBy)
        {
            _logger.LogInformation($"Approving loan type: {loanCode}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt =>
                        lt.LoanCode == loanCode &&
                        lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    throw new KeyNotFoundException(
                        $"Loan type '{loanCode}' not found");
                }

                // CHECK IF ALREADY APPROVED
                if (loanType.ApprovalStatus == "Approved")
                {
                    throw new ValidationException(
                        $"Loan type '{loanCode}' is already approved");
                }

                // MAKER-CHECKER CONTROL
                // USER WHO CREATED/UPDATED CANNOT APPROVE
                if (!string.IsNullOrWhiteSpace(loanType.AuditId) &&
                    loanType.AuditId.Trim().ToLower() ==
                    approvedBy.Trim().ToLower())
                {
                    throw new ValidationException(
                        "You cannot approve a loan type you created or updated. Please request another user to approve it.");
                }

                // STORE APPROVER
                var previousUser = loanType.AuditId;

                // UPDATE STATUS
                loanType.ApprovalStatus = "Approved";

                // OPTIONAL:
                // STORE APPROVER SEPARATELY
                loanType.UserName = approvedBy;

                loanType.AuditTime = DateTime.Now;
                loanType.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                var blockchainData = new
                {
                    LoanTypeCode = loanType.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    CompanyCode = loanType.CompanyCode,
                    CreatedOrUpdatedBy = previousUser,
                    ApprovedBy = approvedBy,
                    ApprovedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PreviousStatus = "Pending",
                    NewStatus = "Approved"
                };

                var blockchainTx =
                    await _blockchainService.CreateAndAddTransactionAsync(
                        "LOAN_TYPE_APPROVE",
                        approvedBy ?? "SYSTEM",
                        loanType.CompanyCode,
                        0,
                        loanType.LoanCode,
                        blockchainData
                    );

                await transaction.CommitAsync();

                _logger.LogInformation(
                    $"Loan type {loanCode} approved successfully");

                return await GetLoanTypeResponseDto(loanType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(ex,
                    $"Error approving loan type {loanCode}");

                throw;
            }
        }

        public async Task<List<LoanTypeResponseDTO>> GetActiveLoanTypesAsync(string companyCode)
        {
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode &&
                             lt.ApprovalStatus == "Approved")
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
                await CreateBlockchainTransaction("LOAN_TYPE_DELETE", loanType, "SYSTEM");

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

        public async Task<List<LoanTypeResponseDTO>> SearchLoanTypesAsync(string searchTerm, string companyCode)
        {
            var query = _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(lt =>
                    lt.LoanCode.Contains(searchTerm) ||
                    lt.LoanType1.Contains(searchTerm) ||
                    (lt.LoanProduct != null && lt.LoanProduct.Contains(searchTerm)) ||
                    (lt.ValueChain != null && lt.ValueChain.Contains(searchTerm)));
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

            // Remove PP and Contra account required validation
            // if (string.IsNullOrWhiteSpace(loanTypeDto.Ppacc))
            //     throw new ValidationException("PP Account is required");

            // if (string.IsNullOrWhiteSpace(loanTypeDto.ContraAccount))
            //     throw new ValidationException("Contra account is required");

            if (loanTypeDto.Priority < 1 || loanTypeDto.Priority > 10)
                throw new ValidationException("Priority must be between 1 and 10");

            if (loanTypeDto.GracePeriod < 0)
                throw new ValidationException("Grace period cannot be negative");

            if (loanTypeDto.RepayPeriod.HasValue && loanTypeDto.RepayPeriod <= 0)
                throw new ValidationException("Repayment period must be greater than 0");

            if (loanTypeDto.MaxAmount.HasValue && loanTypeDto.MaxAmount <= 0)
                throw new ValidationException("Maximum amount must be greater than 0");

            if (loanTypeDto.ProcessingFee.HasValue && loanTypeDto.ProcessingFee < 0)
                throw new ValidationException("Processing fee cannot be negative");

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
            // Get member details
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo &&
                                         m.CompanyCode == companyCode);

            if (member == null)
            {
                throw new KeyNotFoundException($"Member '{memberNo}' not found");
            }

            // Get member's total shares
            var totalShares = await _context.Shares
                .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                .SumAsync(s => s.TotalShares ?? 0);

            // Get member's existing loans
            var activeStatuses = new[] { (int)Status.Approved, (int)Status.Endorsed, (int)Status.Disbursed };
            var existingLoans = await _context.Loans
                .Where(l => l.MemberNo == memberNo &&
                           l.CompanyCode == companyCode &&
                           activeStatuses.Contains(l.Status ?? 0))
                .ToListAsync();

            var existingLoanCount = existingLoans.Count;

            // Get all Approved loan types
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode &&
                            lt.ApprovalStatus == "Approved")
                .OrderBy(lt => lt.Priority)
                .ToListAsync();

            // Calculate eligibility for each loan type
            var result = new List<LoanTypeSimpleDTO>();
            foreach (var loanType in loanTypes)
            {
                var isEligible = true;
                var reason = string.Empty;
                decimal eligibleAmount = 0;

                // Check maximum number of loans
                if (loanType.MaxLoans.HasValue && existingLoanCount >= loanType.MaxLoans)
                {
                    isEligible = false;
                    reason = $"Maximum number of loans ({loanType.MaxLoans}) reached";
                }

                // Check if member has existing loan of this type (if bridging not allowed)
                if (isEligible && loanType.Bridging != 1)
                {
                    var hasExistingType = existingLoans.Any(l => l.LoanCode == loanType.LoanCode);
                    if (hasExistingType)
                    {
                        isEligible = false;
                        reason = "You already have an Approved loan of this type";
                    }
                }

                // Calculate based on earning ratio (shares)
                if (isEligible && loanType.EarningRation.HasValue && loanType.EarningRation > 0)
                {
                    var calculatedAmount = totalShares * (decimal)loanType.EarningRation;
                    if (loanType.MaxAmount.HasValue)
                    {
                        eligibleAmount = Math.Min(calculatedAmount, loanType.MaxAmount.Value);
                    }
                    else
                    {
                        eligibleAmount = calculatedAmount;
                    }
                }
                else if (loanType.MaxAmount.HasValue)
                {
                    eligibleAmount = loanType.MaxAmount.Value;
                }

                result.Add(new LoanTypeSimpleDTO
                {
                    LoanCode = loanType.LoanCode,
                    LoanType = loanType.LoanType1,
                    MaxAmount = loanType.MaxAmount,
                    RepayPeriod = loanType.RepayPeriod,
                    Interest = loanType.Interest,
                    Guarantor = loanType.Guarantor,
                    ProcessingFee = loanType.Processingfee?.ToString(),
                    Bridging = loanType.Bridging == 0,
                    MobileLoan = loanType.MobileLoan ?? false,
                    IsProject = loanType.IsProject ?? false,
                    SelfGuarantee = loanType.SelfGuarantee?.ToString(),
                    Priority = loanType.Priority ?? 1,
                    IsEligible = isEligible,
                    EligibleAmount = eligibleAmount,
                    Reason = reason
                });
            }

            return result;
        }

        public async Task<LoanTypeStatisticsDTO> GetLoanTypeStatisticsAsync(string companyCode)
        {
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == companyCode)
                .ToListAsync();

            var totalLoanTypes = loanTypes.Count;
            var activeLoanTypes = loanTypes.Count(lt => lt.ApprovalStatus == "Approved");
            var pendingLoanTypes = loanTypes.Count(lt => lt.ApprovalStatus == "Pending");

            var totalLoans = await _context.Loans
                .Where(l => l.CompanyCode == companyCode)
                .CountAsync();

            var totalLoanAmount = await _context.Loans
                .Where(l => l.CompanyCode == companyCode)
                .SumAsync(l => l.LoanAmt ?? 0);

            var totalDisbursed = await _context.Loans
                .Where(l => l.CompanyCode == companyCode && l.AuditDateTime != null)
                .SumAsync(l => l.LoanAmt ?? 0);

            var activeStatuses = new[] { (int)Status.Approved, (int)Status.Endorsed, (int)Status.Disbursed };
            var totalOutstanding = await _context.Loans
                .Where(l => l.CompanyCode == companyCode && activeStatuses.Contains(l.Status ?? 0))
                .SumAsync(l => l.LoanAmt ?? 0);

            return new LoanTypeStatisticsDTO
            {
                TotalLoanTypes = totalLoanTypes,
                ActiveLoanTypes = activeLoanTypes,
                TotalLoans = totalLoans,
                TotalLoanAmount = totalLoanAmount,
                TotalDisbursed = totalDisbursed,
                TotalOutstanding = totalOutstanding,
                AverageLoanAmount = totalLoans > 0 ? totalLoanAmount / totalLoans : 0,
                LoanTypesByStatus = loanTypes
                    .GroupBy(lt => lt.ApprovalStatus ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        private async Task<LoanTypeResponseDTO> GetLoanTypeResponseDto(Loantype loanType)
        {
            // Get penalty configuration from Penalty table
            var penaltyConfig = await _context.Penalties
                .FirstOrDefaultAsync(p => p.LoanCode == loanType.LoanCode &&
                                          p.CompanyCode == loanType.CompanyCode);

            // Get usage statistics
            var totalLoans = await _context.Loans
                .CountAsync(l => l.LoanCode == loanType.LoanCode &&
                               l.CompanyCode == loanType.CompanyCode);

            var activeStatuses = new[] { (int)Status.Approved, (int)Status.Endorsed, (int)Status.Disbursed };
            var activeLoans = await _context.Loans
                .CountAsync(l => l.LoanCode == loanType.LoanCode &&
                               l.CompanyCode == loanType.CompanyCode &&
                               activeStatuses.Contains(l.Status ?? 0));

            var totalLoanAmount = await _context.Loans
                .Where(l => l.LoanCode == loanType.LoanCode &&
                          l.CompanyCode == loanType.CompanyCode)
                .SumAsync(l => l.LoanAmt ?? 0);

            var totalDisbursed = await _context.Loans
                .Where(l => l.LoanCode == loanType.LoanCode &&
                          l.CompanyCode == loanType.CompanyCode &&
                          l.AuditDateTime != null)
                .SumAsync(l => l.LoanAmt ?? 0);

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
                Penalty = loanType.Penalty == 1 ? 1 : 0,  // Returns true if penalty is Approved
                ProcessingFee = loanType.Processingfee,
                GracePeriod = loanType.GracePeriod,
                RepayMethod = loanType.Repaymethod,
                Bridging = loanType.Bridging == 1,  // Returns true if bridging is allowed
                SelfGuarantee = loanType.SelfGuarantee ?? false,
                MobileLoan = loanType.MobileLoan ?? false,
                IsProject = loanType.IsProject ?? false,
                MobileLoanApproval = loanType.MobileLoanApproval ?? false,
                Ppacc = loanType.Ppacc,
                ContraAccount = loanType.ContraAccount,
                MaxLoans = loanType.MaxLoans,
                Priority = loanType.Priority ?? 1,
                CompanyCode = loanType.CompanyCode,
                CreatedBy = loanType.AuditId,
                CreatedAt = loanType.AuditDateTime,
                UpdatedAt = loanType.AuditDateTime,
                TotalLoans = totalLoans,
                TotalLoanAmount = totalLoanAmount,
                ActiveLoans = activeLoans,
                TotalDisbursed = totalDisbursed,
                ApprovalStatus = loanType.ApprovalStatus,

                // Penalty configuration from Penalty table
                AttractsPenalty = penaltyConfig != null && penaltyConfig.Penalty == 1,
                PenaltyMode = penaltyConfig?.Mode ?? "Percentage",
                PenaltyRateType = penaltyConfig?.Rate ?? "Monthly",
                PenaltyValue = penaltyConfig?.Value ?? 0,
                PenaltyChargeItem = penaltyConfig?.ChargeItem ?? 0
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
                        lt.LoanCode,
                        lt.LoanType1,
                        LoanName = lt.LoanType1 ?? lt.LoanCode,
                        lt.MaxAmount,
                        lt.RepayPeriod,
                        lt.Interest,
                        lt.Bridging,
                        lt.MobileLoan,
                        lt.IsProject,
                        lt.Priority,
                        lt.Guarantor,
                        lt.SelfGuarantee,
                        lt.Processingfee,
                        lt.GracePeriod,
                        lt.Repaymethod,
                        lt.CompanyCode,
                        lt.ApprovalStatus,
                        TotalLoans = _context.Loans.Count(l => l.LoanCode == lt.LoanCode &&
                                                              l.CompanyCode == lt.CompanyCode),
                        ActiveLoans = _context.Loans.Count(l => l.LoanCode == lt.LoanCode &&
                                       l.CompanyCode == lt.CompanyCode &&
                                       (l.Status == (int)Status.Approved ||
                                        l.Status == (int)Status.Endorsed ||
                                        l.Status == (int)Status.Disbursed))
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

        private async Task ValidateAccountsExist(LoanTypeCreateDTO loanTypeDto)
        {
            // Check if loan account exists (still required)
            if (!string.IsNullOrEmpty(loanTypeDto.LoanAcc))
            {
                var loanAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(a => a.Glaccname == loanTypeDto.LoanAcc &&
                                             a.CompanyCode == loanTypeDto.CompanyCode);
                if (loanAccount == null)
                    throw new ValidationException($"Loan account '{loanTypeDto.LoanAcc}' does not exist");
            }

            // Check if interest account exists (if provided)
            if (!string.IsNullOrEmpty(loanTypeDto.InterestAcc))
            {
                var interestAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(a => a.Glaccname == loanTypeDto.InterestAcc &&
                                             a.CompanyCode == loanTypeDto.CompanyCode);
                if (interestAccount == null)
                    throw new ValidationException($"Interest account '{loanTypeDto.InterestAcc}' does not exist");
            }

            // Check if penalty account exists (if provided)
            if (!string.IsNullOrEmpty(loanTypeDto.PenaltyAcc))
            {
                var penaltyAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(a => a.Glaccname == loanTypeDto.PenaltyAcc &&
                                             a.CompanyCode == loanTypeDto.CompanyCode);
                if (penaltyAccount == null)
                    throw new ValidationException($"Penalty account '{loanTypeDto.PenaltyAcc}' does not exist");
            }

            // Check if PP account exists (if provided) - NOW OPTIONAL
            if (!string.IsNullOrEmpty(loanTypeDto.Ppacc))
            {
                var ppAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(a => a.Glaccname == loanTypeDto.Ppacc &&
                                             a.CompanyCode == loanTypeDto.CompanyCode);
                if (ppAccount == null)
                    throw new ValidationException($"PP account '{loanTypeDto.Ppacc}' does not exist");
            }

            // Check if contra account exists (if provided) - NOW OPTIONAL
            if (!string.IsNullOrEmpty(loanTypeDto.ContraAccount))
            {
                var contraAccount = await _context.GlSetup
                    .FirstOrDefaultAsync(a => a.Glaccname == loanTypeDto.ContraAccount &&
                                             a.CompanyCode == loanTypeDto.CompanyCode);
                if (contraAccount == null)
                    throw new ValidationException($"Contra account '{loanTypeDto.ContraAccount}' does not exist");
            }
        }

        public async Task<RepaymentScheduleDTO> CalculateRepaymentScheduleAsync(
            string loanCode,
            decimal principal,
            int termMonths,
            decimal annualInterestRate,
            string repaymentMethod,
            string companyCode)
        {
            var schedule = new RepaymentScheduleDTO
            {
                LoanCode = loanCode,
                Principal = principal,
                TermMonths = termMonths,
                AnnualInterestRate = annualInterestRate,
                RepaymentMethod = repaymentMethod,
                Installments = new List<InstallmentDTO>()
            };

            decimal monthlyInterestRate = annualInterestRate / 12 / 100;
            decimal outstandingBalance = principal;

            if (repaymentMethod == "STL")
            {
                // STL Calculation - Fixed Principal
                decimal monthlyPrincipal = principal / termMonths;

                for (int month = 1; month <= termMonths; month++)
                {
                    decimal interest = outstandingBalance * monthlyInterestRate;
                    decimal totalPayment = monthlyPrincipal + interest;

                    schedule.Installments.Add(new InstallmentDTO
                    {
                        InstallmentNumber = month,
                        PrincipalPayment = monthlyPrincipal,
                        InterestPayment = interest,
                        TotalPayment = totalPayment,
                        OutstandingBalance = outstandingBalance - monthlyPrincipal
                    });

                    outstandingBalance -= monthlyPrincipal;
                }
            }
            else if (repaymentMethod == "AMT")
            {
                // AMT Calculation - Equal Installments
                decimal emi = principal * monthlyInterestRate *
                              (decimal)Math.Pow((double)(1 + monthlyInterestRate), termMonths) /
                              ((decimal)Math.Pow((double)(1 + monthlyInterestRate), termMonths) - 1);

                for (int month = 1; month <= termMonths; month++)
                {
                    decimal interest = outstandingBalance * monthlyInterestRate;
                    decimal principalPayment = emi - interest;

                    schedule.Installments.Add(new InstallmentDTO
                    {
                        InstallmentNumber = month,
                        PrincipalPayment = principalPayment,
                        InterestPayment = interest,
                        TotalPayment = emi,
                        OutstandingBalance = outstandingBalance - principalPayment
                    });

                    outstandingBalance -= principalPayment;
                }
            }
            else if (repaymentMethod == "RBAL")
            {
                // RBAL Calculation - Flexible, returns schedule showing interest only
                // Actual payment amounts will be determined during application
                for (int month = 1; month <= termMonths; month++)
                {
                    decimal interest = outstandingBalance * monthlyInterestRate;

                    schedule.Installments.Add(new InstallmentDTO
                    {
                        InstallmentNumber = month,
                        PrincipalPayment = 0, // To be determined during payment
                        InterestPayment = interest,
                        TotalPayment = interest, // Minimum payment (interest only)
                        OutstandingBalance = outstandingBalance,
                        IsFlexible = true,
                        MinimumPayment = interest
                    });

                    // Note: Balance doesn't reduce until principal is paid
                }
            }

            schedule.TotalInterest = schedule.Installments.Sum(i => i.InterestPayment);
            schedule.TotalRepayment = principal + schedule.TotalInterest;

            return schedule;
        }

        public async Task<decimal> CalculateMonthlyPaymentAsync(
            decimal principal,
            int termMonths,
            decimal annualInterestRate,
            string repaymentMethod)
        {
            decimal monthlyInterestRate = annualInterestRate / 12 / 100;

            if (repaymentMethod == "STL")
            {
                // For STL, payment varies each month, return average or first month
                decimal monthlyPrincipal = principal / termMonths;
                decimal firstMonthInterest = principal * monthlyInterestRate;
                return monthlyPrincipal + firstMonthInterest;
            }
            else if (repaymentMethod == "AMT")
            {
                // EMI calculation
                return principal * monthlyInterestRate *
                       (decimal)Math.Pow((double)(1 + monthlyInterestRate), termMonths) /
                       ((decimal)Math.Pow((double)(1 + monthlyInterestRate), termMonths) - 1);
            }
            else if (repaymentMethod == "RBAL")
            {
                // Minimum payment is interest only
                return principal * monthlyInterestRate;
            }

            return 0;
        }

        public async Task<decimal> CalculateOutstandingBalanceAsync(
            decimal principal,
            int termMonths,
            decimal annualInterestRate,
            string repaymentMethod,
            int monthsPaid)
        {
            decimal monthlyInterestRate = annualInterestRate / 12 / 100;
            decimal outstandingBalance = principal;

            if (repaymentMethod == "STL")
            {
                decimal monthlyPrincipal = principal / termMonths;
                outstandingBalance = principal - (monthlyPrincipal * monthsPaid);
            }
            else if (repaymentMethod == "AMT")
            {
                decimal emi = await CalculateMonthlyPaymentAsync(principal, termMonths, annualInterestRate, repaymentMethod);

                for (int i = 1; i <= monthsPaid; i++)
                {
                    decimal interest = outstandingBalance * monthlyInterestRate;
                    decimal principalPaid = emi - interest;
                    outstandingBalance -= principalPaid;
                }
            }
            else if (repaymentMethod == "RBAL")
            {
                // For RBAL, balance only reduces when principal is paid
                // This would need actual payment history
                return principal; // Placeholder
            }

            return Math.Max(0, outstandingBalance);
        }

        public async Task<decimal> CalculateInterestForPeriodAsync(
    decimal outstandingBalance,
    decimal annualInterestRate,
    int days)
        {
            // Simple daily interest calculation
            // This works for STL, AMT, and RBAL
            decimal dailyInterestRate = annualInterestRate / 365 / 100; // Convert percentage to decimal
            decimal interest = outstandingBalance * dailyInterestRate * days;

            return Math.Round(interest, 2);
        }

        private async Task CreateBlockchainTransaction(string transactionType, Loantype loanType, string user, object oldValues = null)
        {
            var blockchainData = new
            {
                LoanTypeCode = loanType.LoanCode,
                LoanTypeName = loanType.LoanType1,
                CompanyCode = loanType.CompanyCode,
                User = user,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Properties = new
                {
                    loanType.MaxAmount,
                    loanType.RepayPeriod,
                    loanType.Interest,
                    loanType.Bridging,
                    loanType.MobileLoan,
                    loanType.IsProject,
                    loanType.Priority,
                    loanType.Guarantor,
                    loanType.SelfGuarantee,
                    loanType.Processingfee,
                    loanType.GracePeriod,
                    loanType.Repaymethod,
                    loanType.ApprovalStatus
                },
                OldValues = oldValues
            };

            var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                transactionType,
                user ?? "SYSTEM",
                loanType.CompanyCode,
                0,
                loanType.LoanCode,
                blockchainData
            );

            if (blockchainTx != null)
            {
                _logger.LogInformation($"Blockchain transaction created: {blockchainTx.TransactionId}");
            }
        }
    }
}