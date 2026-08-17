using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanService
    {
        Task<bool> DeleteLoanAsync(string loanNo, string companyCode, string deletedBy, string reason);

        #region Loan Application
        Task<Loan> ApplyForLoanAsync(LoanApplicationDTO application);
        Task<(bool IsEligible, string Message, bool HasValidShares, decimal TotalEligibleShares, decimal MaxLoanAmount)> CheckMemberEligibilityWithContributionsAsync(string memberNo, string companyCode);
        Task<bool> HasActiveLoansAsync(string memberNo, string companyCode);
        Task<decimal> GetGuarantorTotalGuaranteesAsync(string memberNo, string companyCode);
        Task<(bool HasExistingLoan, string Message, List<LoanSummaryDTO> ExistingLoans)> CheckExistingLoansAsync(string memberNo, string companyCode);
        Task<DateTime?> GetLastRepaymentDateAsync(string loanNo);
        Task<Loan> GetLoanByNoAsync(string loanNo, string companyCode);
        Task<List<LoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode);
        Task<List<LoanSummaryDTO>> SearchLoansAsync(LoanSearchDTO searchDto);
        Task<LoanDashboardDTO> GetLoanDashboardAsync(string companyCode);
        #endregion

        #region Guarantor Management
        Task<Loan> GetLoanByNoForDisplayAsync(string loanNo, string companyCode);
        Task<Loanguar> AssignGuarantorAsync(string loanNo, GuarantorAssignmentDTO guarantor, string assignedBy);
        Task<List<GuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo);
        Task<bool> ReleaseGuarantorAsync(int guarantorId, string releasedBy);
        Task<bool> ValidateGuarantorEligibilityAsync(string memberNo, decimal guaranteeAmount, string companyCode);
        #endregion


        #region Collateral Guarantee Management
        Task<List<MemberCollateralDTO>> GetMemberAvailableCollateralsAsync(string memberNo, string companyCode);
        Task<ColloanGuar> AssignCollateralGuaranteeAsync(CollateralGuaranteeDTO guaranteeDto, string assignedBy);
        Task<List<CollateralGuaranteeResponseDTO>> GetLoanCollateralGuaranteesAsync(string loanNo);
        Task<bool> ReleaseCollateralGuaranteeAsync(long collateralGuaranteeId, string releasedBy, string reason);
        Task<decimal> GetTotalCollateralGuaranteeAmountAsync(string loanNo);
        Task<decimal> GetTotalGuaranteeForLoanAsync(string loanNo, string companyCode);
        Task<(bool IsValid, string Message, AvailableCollateralDTO? Data)> ValidateCollateralForLoanAsync(
            string memberNo, string colCode, string loanNo, string companyCode);


        #endregion

        #region Loan Appraisal
        Task<Appraisal> AppraiseLoanAsync(LoanAppraisalDTO appraisalDto);
        Task<Appraisal?> GetLoanAppraisalAsync(string loanNo);
        #endregion

        #region Loan Approval
        Task<Endmain> ApproveLoanAsync(LoanApprovalDTO approvalDto);
        Task<List<Endmain>> GetLoanApprovalsAsync(string loanNo);
        Task<bool> IsLoanApprovedAsync(string loanNo);
        #endregion

        #region Loan Endorsement/Deduction
        Task<Endmain> CreateEndorsementAsync(LoanEndorsementDTO endorsementDto);
        Task<Endmain> GetEndorsementByLoanNoAsync(string loanNo, string companyCode);
        Task<Endmain> GetEndorsementByMinuteNoAsync(string minuteNo, string companyCode);
        Task<List<Endmain>> GetEndorsementsByLoanNoAsync(string loanNo, string companyCode);
        Task<List<LoanDeductionDTO>> GetAvailableDeductionsAsync(string companyCode);
        Task<decimal> CalculateTotalDeductionsAsync(string loanNo, List<LoanDeductionDTO> deductions);
        Task<bool> HasEndorsementAsync(string loanNo, string companyCode);
        Task<LoanEndorsementDTO> GetEndorsementForEditAsync(string loanNo, string companyCode);
        Task<Endmain> UpdateEndorsementAsync(LoanEndorsementDTO endorsementDto);
        Task<EndorsementDetailsDTO> GetEndorsementDetailsAsync(string loanNo, string companyCode);
        #endregion

        #region Disbursement
        Task<Cheque> DisburseLoanAsync(LoanDisbursementDTO disbursementDto);
        Task<Cheque> GetLoanDisbursementAsync(string loanNo);
        Task<Loanbal?> GetLoanBalanceAsync(string loanNo);
        Task<Loanbal?> GetLoanBalanceAsync(string loanNo, string companyCode);
        #endregion

        #region Schedule Generation
        Task<List<LoanSchedule>> GenerateLoanScheduleAsync(string loanNo);
        Task GenerateLoanScheduleAsync(string loanNo, decimal principalAmount, decimal interestRate,
         int repaymentPeriod, DateTime disbursementDate, string companyCode, string repayMethod, bool isUpfrontInterest = false);
        Task<List<LoanScheduleDTO>> GetLoanScheduleAsync(string loanNo);
        Task UpdateOverdueStatusesAsync(string companyCode);
        Task<LoanSchedule> GetCurrentInstallmentAsync(string loanNo);
        Task RecalculateRbalScheduleAsync(string loanNo, decimal newOutstandingBalance);
        #endregion

        #region Repayments
        Task<Repay> ProcessRepaymentAsync(LoanRepaymentDTO repaymentDto);
        Task<List<Repay>> GetLoanRepaymentsAsync(string loanNo);
        Task<Repay> ReverseRepaymentAsync(int repaymentId, string reason, string reversedBy);
        #endregion

        #region Loan Offset with Shares
        Task<List<AvailableSharesDTO>> GetAvailableSharesForOffsetAsync(string memberNo, string companyCode);
        Task<decimal> GetSharesLockedForGuaranteeAsync(string memberNo, string sharesCode, string companyCode);
        Task<LoanOffsetResponseDTO> OffsetLoanWithSharesAsync(LoanOffsetDTO offsetDto);
        #endregion

        #region State Management
        Task<bool> UpdateLoanStatusAsync(string loanNo, string newStatus, string performedBy, string? remarks = null);
        Task<bool> CanTransitionAsync(string loanNo, int targetStatus);
        #endregion

        #region Validation
        Task<(bool IsValid, string Message)> ValidateLoanApplicationAsync(LoanApplicationDTO application);
        Task<(bool IsEligible, string Message)> CheckMemberEligibilityAsync(string memberNo, string loanCode, string companyCode);
        Task<decimal> CalculateMaximumLoanAmountAsync(string memberNo, string loanCode, string companyCode);
        Task<bool> RejectGuarantorAsync(int guarantorId, string remarks, string rejectedBy);
        Task<(bool CanApply, string Message, int ExistingCount)> CanApplyForLoanTypeAsync(string memberNo, string loanCode, string companyCode);
        Task<(bool IsBridgingAllowed, int ExistingLoansCount, string Message)> GetBridgingStatusAsync(string memberNo, string loanCode, string companyCode);

        #endregion

        #region Audit
        Task CreateAuditTrailAsync(string loanNo, string? previousStatus, string? newStatus, string action, string description, string performedBy, string companyCode);
        Task<List<AuditTrail>> GetLoanAuditTrailAsync(string loanNo);
        //Task<decimal> GetMemberAvailableDepositsForGuaranteeAsync(string guarantorMemberNo, string companyCode);
        #endregion

        Task<List<GuarantorsPerLoanReportDTO>> GetGuarantorsPerLoanReportAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<AllGuarantorsReportDTO>> GetAllGuarantorsReportAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null);
    }
    public class LoanService : ILoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly AppDbContext _appDbContext;
        private readonly ILogger<LoanService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly IMemberService _memberService;
        private readonly ILoanTypeService _loanTypeService;
        private readonly IShareService _shareService;
        private readonly AuditTrailService _auditService;
        private readonly IHttpContextAccessor _httpContextAccessor; 

        public LoanService(
            ApplicationDbContext context,
            AppDbContext appDbContext,
            ILogger<LoanService> logger,
            IBlockchainService blockchainService,
            IMemberService memberService,
            ILoanTypeService loanTypeService,
            AuditTrailService auditService,
            IShareService shareService,
            IHttpContextAccessor httpContextAccessor) 
        {
            _context = context;
            _appDbContext = appDbContext;
            _logger = logger;
            _blockchainService = blockchainService;
            _memberService = memberService;
            _loanTypeService = loanTypeService;
            _shareService = shareService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor; 
        }

        #region Loan Deletion - Permanent Delete

        public async Task<bool> DeleteLoanAsync(string loanNo, string companyCode, string deletedBy, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Permanently deleting loan {loanNo} for company {companyCode}. Reason: {reason}");

                // 1. Get the loan to verify it exists and get member info for blockchain
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    throw new InvalidOperationException($"Loan {loanNo} not found");
                }

                _logger.LogInformation($"Found loan - Status: {loan.Status}, Amount: {loan.LoanAmt:C}");

                // Store loan info for blockchain before deletion
                var loanInfo = new
                {
                    loan.LoanNo,
                    loan.MemberNo,
                    loan.LoanAmt,
                    loan.Status,
                    loan.ApplicDate,
                    DeletedBy = deletedBy,
                    Reason = reason,
                    DeletedAt = DateTime.Now
                };

                // ============================================================
                // 2. DELETE FROM LOANGUAR (Member Guarantors)
                // ============================================================
                var memberGuarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo)
                    .ToListAsync();

                if (memberGuarantors.Any())
                {
                    _logger.LogInformation($"Deleting {memberGuarantors.Count} member guarantor records");
                    _context.Loanguar.RemoveRange(memberGuarantors);
                }

                // ============================================================
                // 3. DELETE FROM COLLOANGUAR (Collateral Guarantees)
                // ============================================================
                var collateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo)
                    .ToListAsync();

                if (collateralGuarantees.Any())
                {
                    _logger.LogInformation($"Deleting {collateralGuarantees.Count} collateral guarantee records");
                    _context.ColloanGuars.RemoveRange(collateralGuarantees);
                }

                // ============================================================
                // 4. DELETE FROM APPRAISAL
                // ============================================================
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo);

                if (appraisal != null)
                {
                    _logger.LogInformation($"Deleting appraisal record for loan {loanNo}");
                    _context.Appraisal.Remove(appraisal);
                }

                // ============================================================
                // 5. DELETE FROM ENDMAIN (Endorsement)
                // ============================================================
                var endorsement = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                if (endorsement != null)
                {
                    _logger.LogInformation($"Deleting endorsement record for loan {loanNo}");
                    _context.Endmain.Remove(endorsement);
                }

                // ============================================================
                // 6. DELETE FROM CHEQUES
                // ============================================================
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                if (cheque != null)
                {
                    _logger.LogInformation($"Deleting cheque record for loan {loanNo}");
                    _context.Cheques.Remove(cheque);
                }

                // ============================================================
                // 7. DELETE FROM LOANBAL (Loan Balance)
                // ============================================================
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);

                if (loanbal != null)
                {
                    _logger.LogInformation($"Deleting loan balance record for loan {loanNo}");
                    _context.Loanbal.Remove(loanbal);
                }

                // ============================================================
                // 8. DELETE FROM LOANSCHEDULE (Repayment Schedule)
                // ============================================================
                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .ToListAsync();

                if (schedules.Any())
                {
                    _logger.LogInformation($"Deleting {schedules.Count} schedule records for loan {loanNo}");
                    _context.LoanSchedules.RemoveRange(schedules);
                }

                // ============================================================
                // 9. DELETE FROM REPAY (Repayment Records)
                // ============================================================
                var repayments = await _context.Repay
                    .Where(r => r.LoanNo == loanNo)
                    .ToListAsync();

                if (repayments.Any())
                {
                    _logger.LogInformation($"Deleting {repayments.Count} repayment records for loan {loanNo}");
                    _context.Repay.RemoveRange(repayments);
                }

                // ============================================================
                // 10. DELETE ANY GLTRANSACTIONS related to this loan
                // ============================================================
                // Get voucher numbers from cheque if exists
                string voucherNo = cheque?.Voucherno;

                var glTransactions = await _context.Gltransactions
                    .Where(g => g.DocumentNo == voucherNo || g.TransDescript.Contains(loanNo))
                    .ToListAsync();

                if (glTransactions.Any())
                {
                    _logger.LogInformation($"Deleting {glTransactions.Count} GL transaction records for loan {loanNo}");
                    _context.Gltransactions.RemoveRange(glTransactions);
                }

                // ============================================================
                // 11. RECORD BLOCKCHAIN TRANSACTION BEFORE DELETING THE LOAN
                // ============================================================
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_PERMANENTLY_DELETED",
                    MemberNo = loan.MemberNo,
                    CompanyCode = companyCode,
                    Amount = loan.LoanAmt ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(loanInfo),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(loanInfo),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // ============================================================
                // 12. FINALLY, DELETE THE LOAN ITSELF
                // ============================================================
                _logger.LogInformation($"Deleting loan {loanNo} from Loans table");
                _context.Loans.Remove(loan);

                // Save all changes
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {loanNo} PERMANENTLY DELETED successfully. " +
                    $"Removed: {memberGuarantors.Count} guarantors, " +
                    $"{collateralGuarantees.Count} collateral, " +
                    $"{schedules.Count} schedule entries, " +
                    $"{repayments.Count} repayments, " +
                    $"{glTransactions.Count} GL transactions");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error permanently deleting loan {loanNo}");
                throw;
            }
        }

        #endregion


        #region Loan Application
        public async Task<Loan> ApplyForLoanAsync(LoanApplicationDTO application)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var validation = await ValidateLoanApplicationAsync(application);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException($"Loan validation failed: {validation.Message}");
                }

                var eligibility = await CheckMemberEligibilityAsync(application.MemberNo, application.LoanCode, application.CompanyCode);
                if (!eligibility.IsEligible)
                {
                    throw new InvalidOperationException($"Member not eligible: {eligibility.Message}");
                }

                // Fetch the member to get IdNo and other details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == application.MemberNo && m.CompanyCode == application.CompanyCode);

                if (member == null)
                {
                    throw new InvalidOperationException($"Member {application.MemberNo} not found");
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(l => l.LoanCode == application.LoanCode && l.CompanyCode == application.CompanyCode);

                if (loanType == null)
                {
                    throw new InvalidOperationException($"Loan type {application.LoanCode} not found");
                }

                // Get penalty configuration if it applies
                Penalties? penaltyConfig = null;
                bool attractsPenalty = false;
                string? penaltyMode = null;
                string? penaltyRate = null;
                decimal? penaltyValue = null;
                short? penaltyChargeItem = null;

                if (loanType.Penalty == 1)
                {
                    // Get penalty configuration from Penalties table
                    penaltyConfig = await _context.Penalties
                        .FirstOrDefaultAsync(p => p.LoanCode == application.LoanCode && p.CompanyCode == application.CompanyCode && p.Penalty == 1);

                    if (penaltyConfig != null)
                    {
                        attractsPenalty = true;
                        penaltyMode = penaltyConfig.Mode;
                        penaltyRate = penaltyConfig.Rate;
                        penaltyValue = penaltyConfig.Value;
                        penaltyChargeItem = penaltyConfig.ChargeItem;
                    }
                }

                // Validate that the requested repayment period does not exceed the loan type's maximum
                var maxRepayPeriod = loanType.RepayPeriod ?? 360; // Default to 360 months (30 years) if not set
                if (application.RepayPeriod > maxRepayPeriod)
                {
                    throw new InvalidOperationException($"Repayment period of {application.RepayPeriod} months exceeds the maximum allowed of {maxRepayPeriod} months for this loan type.");
                }

                if (application.RepayPeriod < 1)
                {
                    throw new InvalidOperationException("Repayment period must be at least 1 month.");
                }

                var loanNo = await GenerateLoanNumberAsync(loanType.LoanCode, application.MemberNo, application.CompanyCode);

                decimal interestRate = 0;
                if (!string.IsNullOrEmpty(loanType.Interest) && decimal.TryParse(loanType.Interest, out interestRate))
                {
                    interestRate = Math.Round(interestRate, 4, MidpointRounding.AwayFromZero);
                }

                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanCode,
                    CompanyCode = application.CompanyCode,
                    LoanAmt = application.PrincipalAmount,
                    MaxLoanamt = application.PrincipalAmount,
                    IdNo = member.Idno,
                    Interest = interestRate,
                    RepayPeriod = application.RepayPeriod,
                    ApplicDate = application.ApplicationDate,
                    Status = (int)Status.Draft,
                    Purpose = application.Purpose,
                    AddSecurity = application.Remarks,
                    Guaranteed = ParseRequiredGuarantors(loanType.Guarantor).ToString(),
                    RepayMethod = loanType.Repaymethod ?? "AMT",
                    Gperiod = (int)loanType.GracePeriod,
                    Bridging = loanType.Bridging == 1,
                    InterestUpront = loanType.InterestUpront,
                    AuditId = application.CreatedBy,
                    BasicSalary = 0,
                    Repayrate = 0,
                    Sharecapital = 0,
                    Run = 0,
                    Run2 = 0,
                    AuditTime = DateTime.Now,
                    Posted = "Draft",
                    UserName = application.CreatedBy,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null,
                    // Penalty configuration snapshot
                    AttractsPenalty = attractsPenalty,
                    PenaltyMode = penaltyMode,
                    PenaltyRate = penaltyRate,
                    PenaltyValue = penaltyValue,
                    PenaltyChargeItem = penaltyChargeItem,
                };

                _context.Loans.Add(loan);
                await _context.SaveChangesAsync();

                // Store guarantors data for audit
                var guarantorsList = new List<object>();
                if (application.Guarantors != null && application.Guarantors.Any())
                {
                    foreach (var guarantor in application.Guarantors)
                    {
                        var loanGuarantor = new Loanguar
                        {
                            LoanNo = loanNo,
                            MemberNo = guarantor.GuarantorMemberNo,
                            Amount = guarantor.GuaranteeAmount,
                            Balance = guarantor.GuaranteeAmount,
                            CompanyCode = application.CompanyCode,
                            AuditTime = DateTime.Now,
                            Transfered = false
                        };
                        _context.Loanguar.Add(loanGuarantor);

                        guarantorsList.Add(new
                        {
                            guarantor.GuarantorMemberNo,
                            guarantor.GuaranteeAmount,
                            guarantor.GuarantorName
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    MemberIdNo = member.Idno,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    LoanCode = application.LoanCode,
                    LoanTypeName = loanType.LoanType1,
                    PrincipalAmount = application.PrincipalAmount,
                    InterestRate = interestRate,
                    RepayPeriod = application.RepayPeriod,
                    MaxRepayPeriodAllowed = maxRepayPeriod,
                    RepayMethod = loanType.Repaymethod ?? "AMT",
                    ApplicationDate = application.ApplicationDate,
                    Purpose = application.Purpose,
                    Remarks = application.Remarks,
                    Guarantors = guarantorsList,
                    CreatedBy = application.CreatedBy,
                    Status = "Draft",
                    // Include penalty configuration in blockchain
                    AttractsPenalty = attractsPenalty,
                    PenaltyMode = penaltyMode,
                    PenaltyRate = penaltyRate,
                    PenaltyValue = penaltyValue,
                    PenaltyChargeItem = penaltyChargeItem,
                    GracePeriod = loanType.GracePeriod
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_APPLICATION",
                    MemberNo = application.MemberNo,
                    CompanyCode = application.CompanyCode,
                    Amount = application.PrincipalAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // ============================================================
                // SAVE AUDIT TRAIL
                // ============================================================

                // Create audit extra data
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNumber = application.MemberNo,
                    memberName = $"{member.Surname} {member.OtherNames}",
                    memberIdNo = member.Idno,
                    loanCode = application.LoanCode,
                    loanTypeName = loanType.LoanType1,
                    principalAmount = application.PrincipalAmount,
                    interestRate = interestRate,
                    repayPeriod = application.RepayPeriod,
                    maxRepayPeriodAllowed = maxRepayPeriod,
                    repayMethod = loanType.Repaymethod ?? "AMT",
                    applicationDate = application.ApplicationDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    purpose = application.Purpose ?? "",
                    remarks = application.Remarks ?? "",
                    numberOfGuarantors = guarantorsList.Count,
                    guarantors = guarantorsList,
                    status = "Draft",
                    blockchainTxId = blockchainTx.TransactionId,
                    // Include penalty configuration in audit
                    attractsPenalty = attractsPenalty,
                    penaltyMode = penaltyMode,
                    penaltyRate = penaltyRate,
                    penaltyValue = penaltyValue,
                    penaltyChargeItem = penaltyChargeItem,
                    gracePeriod = loanType.GracePeriod
                };

                // Create a copy of the loan object for NewValue (what was just saved)
                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.MemberNo,
                    loan.LoanCode,
                    loan.LoanAmt,
                    loan.MaxLoanamt,
                    loan.IdNo,
                    loan.Interest,
                    loan.RepayPeriod,
                    loan.ApplicDate,
                    loan.Status,
                    loan.Purpose,
                    loan.AddSecurity,
                    loan.Guaranteed,
                    loan.RepayMethod,
                    loan.Posted,
                    loan.UserName,
                    loan.CompanyCode,
                    loan.AttractsPenalty,
                    loan.PenaltyMode,
                    loan.PenaltyRate,
                    loan.PenaltyValue,
                    loan.PenaltyChargeItem,
                    BlockchainTxId = blockchainTx.TransactionId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = application.CreatedBy
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,  // For Insert, OldValue is null (nothing existed before)
                    newModel: loanForAudit,  // This will be serialized to NewValue column
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: application.CreatedBy,
                    userName: application.CreatedBy,
                    companyCode: application.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan application {loanNo} created successfully for member {application.MemberNo}");

                return loan;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating loan application");
                throw;
            }
        }

        public async Task<(bool IsEligible, string Message, bool HasValidShares, decimal TotalEligibleShares, decimal MaxLoanAmount)>
 CheckMemberEligibilityWithContributionsAsync(string memberNo, string companyCode)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                return (false, "Member not found", false, 0, 0);
            }

            _logger.LogInformation($"=== Checking loan eligibility for member: {memberNo} ===");

            // Get SACCO parameters
            var saccoParams = await _context.SaccoParram
                .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

            // 1. CHECK MEMBERSHIP MATURITY
            if (saccoParams != null && saccoParams.MembershipMaturityMonths > 0)
            {
                var effectDate = member.EffectDate ?? member.ApplicDate ?? DateTime.Now;
                var membershipMonths = ((DateTime.Now - effectDate).Days) / 30;

                if (membershipMonths < saccoParams.MembershipMaturityMonths)
                {
                    return (false, $"Member must be active for {saccoParams.MembershipMaturityMonths} months before applying for a loan. Current membership: {membershipMonths} months.", false, 0, 0);
                }
            }

            // ============================================================
            // CHECK EXISTING LOANS AND BRIDGING ELIGIBILITY
            // ============================================================

            // Get all active loans for this member
            var existingLoans = await _context.Loans
                .Where(l => l.MemberNo == memberNo &&
                            l.CompanyCode == companyCode &&
                            l.Status != (int)Status.Closed &&
                            l.Status != (int)Status.Rejected &&
                            l.Status != (int)Status.WrittenOff)
                .ToListAsync();

            bool hasExistingLoan = existingLoans.Any();
            bool hasBridgingEligibleLoan = false;
            string bridgingLoanTypeCode = null;
            string bridgingLoanTypeName = null;

            if (hasExistingLoan)
            {
                _logger.LogInformation($"Member has {existingLoans.Count} existing loan(s)");

                // Step 1: Check if member has paid at least 50% of each existing loan
                foreach (var loan in existingLoans)
                {
                    // Get the loan balance to determine how much is outstanding
                    var loanBalance = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                    if (loanBalance != null)
                    {
                        decimal originalPrincipal = loan.LoanAmt ?? 0;
                        decimal outstandingBalance = loanBalance.Balance;

                        // Calculate how much has been paid
                        decimal amountPaid = originalPrincipal - outstandingBalance;
                        decimal percentagePaid = originalPrincipal > 0 ? (amountPaid / originalPrincipal) * 100 : 0;

                        _logger.LogInformation($"Loan {loan.LoanNo}: Original: {originalPrincipal:C}, Outstanding: {outstandingBalance:C}, Paid: {percentagePaid:F2}%");

                        // Check if at least 50% has been paid
                        if (percentagePaid < 50)
                        {
                            return (false,
                                $"Member has not paid at least 50% of loan {loan.LoanNo}. Current repayment: {percentagePaid:F2}%. " +
                                $"Please continue repaying the loan before applying for a top-up loan.",
                                false, 0, 0);
                        }

                        // Step 2: Check if the loan allows bridging
                        if (loan.Bridging == true)
                        {
                            hasBridgingEligibleLoan = true;
                            bridgingLoanTypeCode = loan.LoanCode;

                            var loanType = await _context.Loantypes
                                .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);
                            bridgingLoanTypeName = loanType?.LoanType1 ?? loan.LoanCode;

                            _logger.LogInformation($"Loan {loan.LoanNo} allows bridging. Member can apply for a top-up.");
                        }
                        else
                        {
                            _logger.LogWarning($"Loan {loan.LoanNo} does NOT allow bridging");
                            return (false,
                                $"The existing loan '{loan.LoanNo}' does not allow bridging/top-up. " +
                                $"Please clear the existing loan first before applying for a new loan.",
                                false, 0, 0);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No loan balance found for loan {loan.LoanNo}");
                        return (false, $"Cannot verify loan repayment status for loan {loan.LoanNo}. Please contact support.", false, 0, 0);
                    }
                }

                // If we get here, at least one existing loan meets the 50% repayment threshold and allows bridging
                _logger.LogInformation($"Member qualifies for top-up loan. Existing loan allows bridging and has >=50% repayment.");
            }

            // ============================================================
            // GET TOTAL share capital across ALL share types
            // ============================================================

            // Get the main share type to get the minimum requirement
            var mainShareType = await _context.Sharetypes
                .FirstOrDefaultAsync(s => s.CompanyCode == companyCode && s.IsMainShares == true);

            if (mainShareType != null)
            {
                var memberShareCapital = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo &&
                                cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                _logger.LogInformation($"Member's Total ShareCapitalAmount (all share types): {memberShareCapital:N0}, Minimum Required: {mainShareType.MinAmount:N0}");

                if (memberShareCapital < mainShareType.MinAmount)
                {
                    return (false,
                        $"Member has not met the minimum shares requirement. " +
                        $"Current Share Capital: {memberShareCapital:N0}, " +
                        $"Minimum required: {mainShareType.MinAmount:N0}. " +
                        $"Please deposit additional share capital to qualify for a loan.",
                        false, 0, 0);
                }

                _logger.LogInformation($"✓ Member has met minimum share capital requirement: {memberShareCapital:N0} >= {mainShareType.MinAmount:N0}");
            }
            else
            {
                _logger.LogWarning($"No main share type (IsMainShares = true) found for company {companyCode}. Skipping minimum share capital check.");
            }

            // 2. GET MINIMUM LOAN AMOUNT FROM SACCO PARAMETERS
            decimal minLoanAmount = saccoParams?.SignificantLoanBalance ?? 0;
            _logger.LogInformation($"Minimum loan amount from SaccoParram: {minLoanAmount:C}");

            // 3. CALCULATE TOTAL ELIGIBLE SHARES/DEPOSITS
            decimal totalEligibleAmount = 0;
            var eligibleBreakdown = new List<string>();

            // Get all share types that can be used for loan guarantee/offset
            var validShareTypes = await _context.Sharetypes
                .Where(s => s.CompanyCode == companyCode &&
                           (s.UsedToGuarantee == true || s.UsedToOffset == true) &&
                           s.Withdrawable == true)
                .ToListAsync();

            if (validShareTypes.Any())
            {
                _logger.LogInformation($"Found {validShareTypes.Count} valid share types for loan eligibility");

                foreach (var shareType in validShareTypes)
                {
                    // Get member's deposits from ContribShares table
                    var contribDeposits = await _context.ContribShares
                        .Where(cs => cs.MemberNo == memberNo &&
                                    cs.Sharescode == shareType.SharesCode &&
                                    cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    // Use the deposit amount directly
                    decimal shareValue = contribDeposits;

                    if (shareValue > 0)
                    {
                        totalEligibleAmount += shareValue;
                        eligibleBreakdown.Add($"{shareType.SharesType ?? shareType.SharesCode}: {shareValue:C}");
                        _logger.LogInformation($"Share type {shareType.SharesCode}: {shareValue:C}");
                    }
                }
            }
            else
            {
                // FALLBACK: If no valid share types configured, use all deposits
                _logger.LogWarning("No valid share types found with UsedToGuarantee/UsedToOffset and Withdrawable=true");

                var allDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                if (allDeposits > 0)
                {
                    totalEligibleAmount = allDeposits;
                    eligibleBreakdown.Add($"Deposits (fallback): {allDeposits:C}");
                }
            }

            // 4. CHECK FOR LOCKED SHARES (used as guarantor for other loans)
            var lockedShares = await GetSharesLockedForGuaranteeAsync(memberNo, null, companyCode);
            var availableAmount = totalEligibleAmount - lockedShares;

            _logger.LogInformation($"Total Eligible: {totalEligibleAmount:C}, Locked: {lockedShares:C}, Available: {availableAmount:C}");

            if (availableAmount <= 0)
            {
                if (totalEligibleAmount > 0)
                {
                    return (false, $"All eligible shares/deposits ({totalEligibleAmount:C}) are locked as guarantees for other loans. Available: {availableAmount:C}", true, totalEligibleAmount, 0);
                }
                return (false, $"Member has no eligible shares/deposits. Please make a deposit/savings contribution first before applying for a loan.", false, 0, 0);
            }

            // 5. CHECK MINIMUM LOAN REQUIREMENT
            if (minLoanAmount > 0 && availableAmount < minLoanAmount)
            {
                return (false, $"Member's eligible shares/deposits ({availableAmount:C}) is below minimum loan requirement of {minLoanAmount:C}. Please increase savings/deposits.", true, availableAmount, 0);
            }

            // 6. GET LOAN-TO-SHARE RATIO FROM SHARETYPES
            var shareTypeRatios = await _context.Sharetypes
                .Where(s => s.CompanyCode == companyCode &&
                           s.LoanToShareRatio.HasValue &&
                           s.LoanToShareRatio.Value > 0)
                .Select(s => (decimal)s.LoanToShareRatio.Value)
                .ToListAsync();

            if (!shareTypeRatios.Any())
            {
                _logger.LogError($"No LoanToShareRatio configured in Sharetypes for company {companyCode}.");
                return (false, "System configuration error: No loan-to-share ratio configured. Please contact administrator.", false, 0, 0);
            }

            decimal loanToShareRatio = shareTypeRatios.Max();
            _logger.LogInformation($"Using highest LoanToShareRatio from Sharetypes: {loanToShareRatio}:1");

            // 7. CALCULATE MAX LOAN AMOUNT
            decimal maxLoanAmount = availableAmount * loanToShareRatio;

            // 8. GET LOANTYPE MAXIMUM LIMITS AND APPLY THEM
            var loanTypes = await _context.Loantypes
                .Where(l => l.CompanyCode == companyCode && l.MaxAmount.HasValue)
                .ToListAsync();

            decimal maxLoanTypeLimit = decimal.MaxValue;
            if (loanTypes.Any())
            {
                maxLoanTypeLimit = loanTypes.Max(l => l.MaxAmount.Value);
                if (maxLoanAmount > maxLoanTypeLimit)
                {
                    _logger.LogInformation($"Capping loan amount at max loan type limit: {maxLoanTypeLimit:C}");
                    maxLoanAmount = maxLoanTypeLimit;
                }
            }

            // 9. REDUCE MAX LOAN AMOUNT BY EXISTING LOAN OUTSTANDING BALANCE
            if (hasExistingLoan)
            {
                var totalOutstandingBalance = await _context.Loanbal
                    .Where(lb => existingLoans.Select(l => l.LoanNo).Contains(lb.LoanNo) && lb.Companycode == companyCode)
                    .SumAsync(lb => lb.Balance);

                _logger.LogInformation($"Total outstanding balance on existing loans: {totalOutstandingBalance:C}");

                // Reduce max loan amount by outstanding balance
                maxLoanAmount = Math.Max(0, maxLoanAmount - totalOutstandingBalance);

                if (maxLoanAmount <= 0)
                {
                    return (false,
                        $"Member's outstanding loan balance of {totalOutstandingBalance:C} exceeds the maximum eligible loan amount. " +
                        $"Please reduce outstanding balance further before applying for a top-up loan.",
                        true, availableAmount, 0);
                }
            }

            // 10. FINAL VALIDATION: Ensure max loan meets minimum requirement
            if (maxLoanAmount < minLoanAmount && maxLoanAmount > 0)
            {
                _logger.LogWarning($"Max loan amount ({maxLoanAmount:C}) is below minimum loan requirement ({minLoanAmount:C})");
                return (false, $"Maximum eligible loan amount ({maxLoanAmount:C}) is below the SACCO's minimum loan amount of {minLoanAmount:C}.", true, availableAmount, 0);
            }

            // 11. BUILD SUCCESS MESSAGE
            var breakdownMessage = string.Join(", ", eligibleBreakdown);
            var successMessage = $"Member is eligible for a loan up to {maxLoanAmount:C}. " +
                                $"Eligible shares/deposits breakdown: {breakdownMessage}. " +
                                $"Available shares: {availableAmount:C}. " +
                                $"Loan-to-share ratio: {loanToShareRatio}:1";

            if (hasExistingLoan && hasBridgingEligibleLoan)
            {
                successMessage += $" ✓ Top-up loan allowed. Existing loan '{bridgingLoanTypeName}' allows top-up.";
            }

            _logger.LogInformation($"✓ Member {memberNo} is eligible. Max Loan: {maxLoanAmount:C}");

            return (true,
                    successMessage,
                    true,
                    availableAmount,
                    maxLoanAmount);
        }


        public async Task<bool> HasActiveLoansAsync(string memberNo, string companyCode)
        {
            return await _context.Loans
                .AnyAsync(l => l.MemberNo == memberNo &&
                               l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff);
        }

        public async Task<decimal> GetGuarantorTotalGuaranteesAsync(string memberNo, string companyCode)
        {
            return await _context.Loanguar
                .Where(g => g.MemberNo == memberNo &&
                            g.CompanyCode == companyCode &&
                            g.Transfered == false &&
                            (g.Balance > 0))
                .SumAsync(g => g.Amount ?? 0);
        }

        public async Task<(bool HasExistingLoan, string Message, List<LoanSummaryDTO> ExistingLoans)> CheckExistingLoansAsync(string memberNo, string companyCode)
        {
            var existingLoans = await _context.Loans
                .Where(l => l.MemberNo == memberNo &&
                            l.CompanyCode == companyCode &&
                            l.Status != (int)Status.Closed &&
                            l.Status != (int)Status.Rejected &&
                            l.Status != (int)Status.WrittenOff)
                .OrderByDescending(l => l.ApplicDate)
                .Select(l => new LoanSummaryDTO
                {
                    LoanNo = l.LoanNo,
                    MemberNo = l.MemberNo,
                    MemberName = "",
                    LoanType = "",
                    PrincipalAmount = l.LoanAmt ?? 0,
                    ApprovedAmount = 0,
                    DisbursedAmount = 0,
                    OutstandingBalance = 0,
                    ArrearsAmount = 0,
                    LoanStatus = l.Status.ToString(),
                    ApplicationDate = l.ApplicDate,
                    DisbursementDate = null,
                    MaturityDate = null,
                    DaysOverdue = 0,
                    InterestRate = l.Interest ?? 0,
                    MonthlyInstallment = 0,
                    InstallmentsPaid = 0,
                    TotalInstallments = 0
                })
                .ToListAsync();

            if (existingLoans.Any())
            {
                var statusList = string.Join(", ", existingLoans.Select(l => $"{l.LoanNo} ({l.LoanStatus})"));
                var message = $"Member has existing loan(s) that are still active: {statusList}. " +
                             "Please clear existing loans before applying for a new loan.";

                return (true, message, existingLoans);
            }

            return (false, "No existing active loans", new List<LoanSummaryDTO>());
        }

        private int ParseRequiredGuarantors(string guarantorValue)
        {
            if (string.IsNullOrEmpty(guarantorValue))
                return 0;

            if (new[] { "Yes", "Y", "1" }.Contains(guarantorValue, StringComparer.OrdinalIgnoreCase))
                return 1;

            if (new[] { "No", "N", "0" }.Contains(guarantorValue, StringComparer.OrdinalIgnoreCase))
                return 0;

            if (int.TryParse(guarantorValue, out int count))
                return count;

            return 0;
        }

        public async Task<DateTime?> GetLastRepaymentDateAsync(string loanNo)
        {
            try
            {
                var lastRepayment = await _context.Repay
                    .Where(r => r.LoanNo == loanNo)
                    .OrderByDescending(r => r.AuditTime)
                    .FirstOrDefaultAsync();

                return lastRepayment?.AuditTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting last repayment date for loan {loanNo}");
                return null;
            }
        }

        public async Task<Loan> GetLoanByNoAsync(string loanNo, string companyCode)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            // DON'T MODIFY THE INTEREST RATE - just return as stored
            // Remove these lines:
            // var loanBalance = await _context.Loanbal
            //     .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);
            // if (loanBalance != null)
            // {
            //     loan.LoanAmt = loanBalance.Balance;
            //     loan.Interest = loanBalance.IntrOwed;  // THIS IS WRONG - IntrOwed is interest owed, not interest rate
            // }

            return loan;
        }

        public async Task<List<LoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode)
        {
            try
            {
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .Select(l => new LoanSummaryDTO
                    {
                        LoanNo = l.LoanNo,
                        MemberNo = l.MemberNo,
                        MemberName = "",
                        LoanType = "",
                        PrincipalAmount = l.LoanAmt ?? 0,
                        ApprovedAmount = 0,
                        DisbursedAmount = l.LoanAmt ?? 0,
                        OutstandingBalance = 0,
                        ArrearsAmount = 0,
                        LoanStatus = l.Status.ToString(),
                        ApplicationDate = l.ApplicDate,
                        DisbursementDate = null,
                        MaturityDate = null,
                        DaysOverdue = 0,
                        InterestRate = l.Interest ?? 0,
                        MonthlyInstallment = 0,
                        InstallmentsPaid = 0,
                        TotalInstallments = 0
                    })
                    .ToListAsync();

                return loans;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting member loans for {memberNo}");
                return new List<LoanSummaryDTO>();
            }
        }

        public async Task<List<LoanSummaryDTO>> SearchLoansAsync(LoanSearchDTO searchDto)
        {
            var query = _context.Loans
                .Where(l => l.CompanyCode == searchDto.CompanyCode);

            if (!string.IsNullOrEmpty(searchDto.MemberNo))
            {
                query = query.Where(l => l.MemberNo == searchDto.MemberNo);
            }

            if (!string.IsNullOrEmpty(searchDto.LoanNo))
            {
                query = query.Where(l => l.LoanNo.Contains(searchDto.LoanNo));
            }

            if (!string.IsNullOrEmpty(searchDto.LoanStatus) && int.TryParse(searchDto.LoanStatus, out int status))
            {
                query = query.Where(l => l.Status == status);
            }
            else if (!string.IsNullOrEmpty(searchDto.LoanStatus))
            {
                var statusEnum = Enum.Parse<Status>(searchDto.LoanStatus);
                query = query.Where(l => l.Status == (int)statusEnum);
            }

            if (!string.IsNullOrEmpty(searchDto.LoanCode))
            {
                query = query.Where(l => l.LoanCode == searchDto.LoanCode);
            }

            if (searchDto.FromDate.HasValue)
            {
                query = query.Where(l => l.ApplicDate >= searchDto.FromDate.Value);
            }

            if (searchDto.ToDate.HasValue)
            {
                query = query.Where(l => l.ApplicDate <= searchDto.ToDate.Value);
            }

            if (searchDto.MinAmount.HasValue)
            {
                query = query.Where(l => l.LoanAmt >= searchDto.MinAmount.Value);
            }

            if (searchDto.MaxAmount.HasValue)
            {
                query = query.Where(l => l.LoanAmt <= searchDto.MaxAmount.Value);
            }

            var loans = await query
                .OrderByDescending(l => l.ApplicDate)
                .ToListAsync();

            // ============================================================
            // OPTIMIZATION: Get all related data in parallel
            // ============================================================
            var loanNos = loans.Select(l => l.LoanNo).ToList();

            // Get all members in one query
            var memberNos = loans.Select(l => l.MemberNo).Distinct().ToList();
            var membersDict = await _context.Members
                .Where(m => memberNos.Contains(m.MemberNo) && m.CompanyCode == searchDto.CompanyCode)
                .ToDictionaryAsync(m => m.MemberNo, m => m);

            // Get all loan types in one query
            var loanCodes = loans.Select(l => l.LoanCode).Distinct().ToList();
            var loanTypesDict = await _context.Loantypes
                .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == searchDto.CompanyCode)
                .ToDictionaryAsync(lt => lt.LoanCode, lt => lt);

            // Get all appraisals in one query
            var appraisalsDict = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo))
                .ToDictionaryAsync(a => a.LoanNo, a => a);

            // Get all endmain (approvals) in one query
            var endmainDict = await _context.Endmain
                .Where(e => loanNos.Contains(e.LoanNo) && e.CompanyCode == searchDto.CompanyCode)
                .ToDictionaryAsync(e => e.LoanNo, e => e);

            // Get all loan balances in one query
            var loanbalDict = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == searchDto.CompanyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb);

            // Get all cheques in one query
            var chequeDict = await _context.Cheques
                .Where(c => loanNos.Contains(c.LoanNo) && c.CompanyCode == searchDto.CompanyCode)
                .ToDictionaryAsync(c => c.LoanNo, c => c);

            // Get repayment counts in one query (grouped)
            var repaymentCounts = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new { LoanNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.LoanNo, g => g.Count);

            // Get total guarantees in one query
            var memberGuarantees = await _context.Loanguar
                .Where(g => loanNos.Contains(g.LoanNo) && g.Transfered == false)
                .GroupBy(g => g.LoanNo)
                .Select(g => new { LoanNo = g.Key, Total = g.Sum(x => x.Amount ?? 0) })
                .ToDictionaryAsync(g => g.LoanNo, g => g.Total);

            var collateralGuarantees = await _context.ColloanGuars
                .Where(cg => loanNos.Contains(cg.LoanNo) && cg.Balance > 0)
                .GroupBy(cg => cg.LoanNo)
                .Select(g => new { LoanNo = g.Key, Total = g.Sum(x => x.Balance) })
                .ToDictionaryAsync(g => g.LoanNo, g => g.Total);

            var result = new List<LoanSummaryDTO>();

            foreach (var loan in loans)
            {
                var member = membersDict.GetValueOrDefault(loan.MemberNo);
                var memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;
                if (string.IsNullOrEmpty(memberName)) memberName = loan.MemberNo;

                var loanType = loanTypesDict.GetValueOrDefault(loan.LoanCode);
                var loanTypeName = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown";

                var appraisal = appraisalsDict.GetValueOrDefault(loan.LoanNo);
                var principalAmount = appraisal?.AmtRecommended ?? loan.LoanAmt ?? 0;

                var endmain = endmainDict.GetValueOrDefault(loan.LoanNo);
                var approvedAmount = endmain?.AmtApproved ?? 0;

                var loanbal = loanbalDict.GetValueOrDefault(loan.LoanNo);
                var outstandingBalance = loanbal?.Balance ?? 0;

                var cheque = chequeDict.GetValueOrDefault(loan.LoanNo);
                var disbursedAmount = cheque?.AmountIssued ?? 0;

                var memberGuarantee = memberGuarantees.GetValueOrDefault(loan.LoanNo, 0);
                var collateralGuarantee = collateralGuarantees.GetValueOrDefault(loan.LoanNo, 0);
                var totalGuarantee = memberGuarantee + collateralGuarantee;

                var installmentPaid = repaymentCounts.GetValueOrDefault(loan.LoanNo, 0);

                var statusName = ((Status)(loan.Status ?? 0)).ToString();

                result.Add(new LoanSummaryDTO
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = memberName,
                    LoanType = loanTypeName,
                    PrincipalAmount = principalAmount,
                    MaxLoanamt = loan.MaxLoanamt ?? 0,
                    ApprovedAmount = approvedAmount,
                    DisbursedAmount = disbursedAmount,
                    OutstandingBalance = outstandingBalance,
                    TotalGuarantee = totalGuarantee,
                    ArrearsAmount = 0,
                    LoanStatus = statusName,
                    ApplicationDate = loan.ApplicDate,
                    DisbursementDate = cheque?.DateIssued ?? loan.AuditDateTime,
                    MaturityDate = loanbal?.LastDate,
                    DaysOverdue = 0,
                    InterestRate = loan.Interest ?? 0,
                    MonthlyInstallment = loanbal?.RepayRate ?? 0,
                    InstallmentsPaid = installmentPaid,
                    TotalInstallments = loan.RepayPeriod ?? 0,
                    RequiredGuarantors = 0
                });
            }

            return result;
        }

        public async Task<LoanDashboardDTO> GetLoanDashboardAsync(string companyCode)
        {
            var loans = await _context.Loans
                .Where(l => l.CompanyCode == companyCode)
                .ToListAsync();

            var dashboard = new LoanDashboardDTO
            {
                TotalLoans = loans.Count,
                TotalLoanAmount = loans.Sum(l => l.LoanAmt ?? 0),
                TotalDisbursed = loans.Where(l => l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed)
                    .Sum(l => l.LoanAmt ?? 0),
                TotalOutstanding = 0,
                TotalRepaid = 0,
                TotalArrears = 0,
                PendingApplications = loans.Count(l => l.Status == (int)Status.Draft),
                UnderAppraisal = loans.Count(l => l.Status == (int)Status.UnderAppraisal),
                PendingApproval = loans.Count(l => l.Status == (int)Status.Submitted),
                PendingFinalApproval = loans.Count(l => l.Status == (int)Status.Approved),
                ApprovedPendingDisbursement = loans.Count(l => l.Status == (int)Status.Approved),
                ActiveLoans = loans.Count(l => l.Status == (int)Status.Disbursed),
                OverdueLoans = 0,
                DefaultedLoans = loans.Count(l => l.Status == (int)Status.Defaulted)
            };

            dashboard.LoansByStatus = loans
                .GroupBy(l => l.Status ?? 0)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            dashboard.LoanPortfolioByType = new Dictionary<string, decimal>();

            dashboard.RecentLoans = await SearchLoansAsync(new LoanSearchDTO
            {
                CompanyCode = companyCode
            });

            dashboard.RecentLoans = dashboard.RecentLoans.Take(10).ToList();

            return dashboard;
        }

        public async Task<string> GenerateLoanNumberAsync(string loanCode, string memberNo, string companyCode)
        {
            // Get the count of existing loans for this member and loan code
            int loanCount = await GetLoanCountAsync(loanCode, memberNo, companyCode);

            // Base loan number without suffix
            string baseLoanNumber = $"{loanCode}{memberNo}";

            // If it's the first loan (count = 0), return just the base
            if (loanCount == 0)
            {
                return baseLoanNumber;
            }

            // For subsequent loans, add -2, -3, -4, etc.
            // The suffix should be (loanCount + 1) because we're creating a new loan
            int suffix = loanCount + 1;
            return $"{baseLoanNumber}-{suffix}";
        }

        private async Task<int> GetLoanCountAsync(string loanCode, string memberNo, string companyCode)
        {
            // Count loans where LoanCode = loanCode AND MemberNo = memberNo AND CompanyCode = companyCode
            return await _context.Loans
                .CountAsync(l => l.LoanCode == loanCode
                    && l.MemberNo == memberNo
                    && l.CompanyCode == companyCode);
        }

        #endregion


        #region Guarantor Management

        public async Task<Loan> GetLoanByNoForDisplayAsync(string loanNo, string companyCode)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            return loan;
        }


        public async Task<Loanguar> AssignGuarantorAsync(string loanNo, GuarantorAssignmentDTO guarantor, string assignedBy)
        {
            var loan = await GetLoanByNoForDisplayAsync(loanNo, guarantor.CompanyCode);

            _logger.LogInformation($"Assigning guarantor to loan {loanNo}. Current status: {loan.Status}");

            if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
            {
                throw new InvalidOperationException($"Cannot assign guarantors to loan in status '{loan.Status}'. Loan must be in Draft or Submitted status.");
            }

            // Get loan type to check if self guarantee is allowed
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == loan.LoanCode && l.CompanyCode == loan.CompanyCode);

            bool isSelfGuarantee = loanType?.SelfGuarantee ?? false;

            int maxGuarantors = 5;

            try
            {
                var saccoParams = await _context.SaccoParram
                    .FirstOrDefaultAsync(s => s.CompanyCode == guarantor.CompanyCode);

                if (saccoParams != null)
                {
                    maxGuarantors = saccoParams.MaxGuarantor;
                    _logger.LogInformation($"Max guarantors from SACCO parameters: {maxGuarantors}");
                }
                else
                {
                    _logger.LogWarning($"No SACCO parameters found for company {guarantor.CompanyCode}. Using default: {maxGuarantors}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting SACCO parameters for company {guarantor.CompanyCode}. Using default: {maxGuarantors}");
            }

            var currentGuarantors = await _context.Loanguar
                .CountAsync(g => g.LoanNo == loanNo && g.Transfered == false);

            if (currentGuarantors >= maxGuarantors)
            {
                throw new InvalidOperationException($"Maximum number of guarantors ({maxGuarantors}) already assigned");
            }

            // Check self guarantee logic
            bool isSelfGuarantor = guarantor.GuarantorMemberNo == loan.MemberNo;

            if (isSelfGuarantor && !isSelfGuarantee)
            {
                _logger.LogWarning($"Self guarantee blocked: isSelfGuarantee={isSelfGuarantee}, Guarantor={guarantor.GuarantorMemberNo}, Applicant={loan.MemberNo}");
                throw new InvalidOperationException("Self guarantee is not allowed for this loan type. Please add another member as guarantor.");
            }

            if (isSelfGuarantor && isSelfGuarantee)
            {
                _logger.LogInformation($"Self guarantee is allowed for this loan type. Applicant {loan.MemberNo} can guarantee their own loan.");
            }

            // Get guarantor member details for FullNames
            var guarantorMember = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == guarantor.GuarantorMemberNo && m.CompanyCode == loan.CompanyCode);

            string guarantorFullName = "";
            if (guarantorMember != null)
            {
                guarantorFullName = $"{guarantorMember.Surname ?? ""} {guarantorMember.OtherNames ?? ""}".Trim();
                if (string.IsNullOrEmpty(guarantorFullName))
                {
                    guarantorFullName = guarantor.GuarantorMemberNo;
                }
            }

            // Validate eligibility (skip for self guarantee)
            if (!isSelfGuarantor)
            {
                var eligibility = await ValidateGuarantorEligibilityAsync(
                    guarantor.GuarantorMemberNo,
                    guarantor.GuaranteeAmount,
                    loan.CompanyCode);

                if (!eligibility)
                {
                    throw new InvalidOperationException("Guarantor is not eligible. Ensure member is active and has sufficient shares.");
                }
            }

            var existing = await _context.Loanguar
                .FirstOrDefaultAsync(g => g.LoanNo == loanNo && g.MemberNo == guarantor.GuarantorMemberNo && g.Transfered == false);

            if (existing != null)
            {
                throw new InvalidOperationException($"Guarantor {guarantor.GuarantorMemberNo} is already assigned to this loan");
            }

            // Store the old loan status for audit (before any changes)
            int oldLoanStatus = (int)loan.Status;
            string oldLoanPosted = loan.Posted ?? "";

            // Create and save the guarantor with ALL fields
            var loanGuarantor = new Loanguar
            {
                LoanNo = loanNo,
                MemberNo = guarantor.GuarantorMemberNo,
                CompanyCode = loan.CompanyCode,
                Amount = guarantor.GuaranteeAmount,
                Balance = guarantor.GuaranteeAmount,
                AuditTime = DateTime.Now,
                AuditId = assignedBy,
                Transfered = false,
                FullNames = guarantorFullName,
                Description = guarantor.Remarks,
                Tguaranto = guarantor.GuaranteeAmount,
                Transdate = DateTime.Now,
                Collateral = isSelfGuarantor ? "Self Guarantee" : "Member Guarantee"
            };

            _context.Loanguar.Add(loanGuarantor);
            await _context.SaveChangesAsync();

            var updatedGuarantorCount = await _context.Loanguar
                .CountAsync(g => g.LoanNo == loanNo && g.Transfered == false);

            bool loanStatusChanged = false;

            // UPDATE LOAN STATUS to Submitted when guarantors are added
            if (loan.Status == (int)Status.Draft && updatedGuarantorCount > 0)
            {
                loan.Status = (int)Status.Submitted;
                loan.Posted = "SUBMIT";
                loan.UserName = assignedBy;
                loan.AuditDateTime = DateTime.Now;
                loanStatusChanged = true;

                _context.Loans.Update(loan);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Loan {loanNo} status updated from Draft to Submitted. Total guarantors: {updatedGuarantorCount}");
            }

            // Create blockchain data (use shorter property names to avoid truncation)
            var blockchainData = new
            {
                Id = loanGuarantor.Id,
                LoanNo = loanNo,
                G = guarantor.GuarantorMemberNo,
                GName = guarantorFullName,
                Amt = guarantor.GuaranteeAmount,
                By = assignedBy,
                Date = DateTime.Now,
                Remarks = guarantor.Remarks ?? "",
                LoanAmt = loan.LoanAmt,
                TotalG = updatedGuarantorCount,
                Status = loan.Status,
                IsSelf = isSelfGuarantor
            };

            var blockchainTx = new BlockchainTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                TransactionType = "LOAN_GUARANTOR_ASSIGNED",
                MemberNo = loan.MemberNo,
                CompanyCode = loan.CompanyCode,
                Amount = guarantor.GuaranteeAmount,
                Timestamp = DateTime.Now,
                DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                OffChainReferenceId = $"{loanNo}-{guarantor.GuarantorMemberNo}",
                Status = "PENDING",
                CreatedAt = DateTime.Now
            };

            _context.BlockchainTransactions.Add(blockchainTx);
            await _context.SaveChangesAsync();

            loanGuarantor.BlockchainTxId = blockchainTx.TransactionId;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Blockchain transaction recorded for guarantor assignment: {blockchainTx.TransactionId}");

            // ============================================================
            // SAVE AUDIT TRAIL FOR GUARANTOR ASSIGNMENT
            // ============================================================

            // Create audit extra data
            var auditExtraData = new
            {
                loanNo = loanNo,
                applicantMemberNo = loan.MemberNo,
                guarantorMemberNo = guarantor.GuarantorMemberNo,
                guarantorName = guarantorFullName,
                guaranteeAmount = guarantor.GuaranteeAmount,
                isSelfGuarantee = isSelfGuarantor,
                remarks = guarantor.Remarks ?? "",
                assignedBy = assignedBy,
                assignedDate = DateTime.Now,
                totalGuarantorsAfter = updatedGuarantorCount,
                maxGuarantorsAllowed = maxGuarantors,
                loanStatusBefore = oldLoanStatus,
                loanStatusAfter = loan.Status,
                loanStatusChanged = loanStatusChanged,
                currentLoanAmount = loan.LoanAmt,
                blockchainTxId = blockchainTx.TransactionId
            };

            // Create a copy of the guarantor object for NewValue
            var guarantorForAudit = new
            {
                loanGuarantor.Id,
                loanGuarantor.LoanNo,
                loanGuarantor.MemberNo,
                loanGuarantor.Amount,
                loanGuarantor.Balance,
                loanGuarantor.FullNames,
                loanGuarantor.Description,
                loanGuarantor.Collateral,
                loanGuarantor.Transfered,
                AssignedBy = assignedBy,
                AssignedDate = DateTime.Now,
                BlockchainTxId = blockchainTx.TransactionId
            };

            await _auditService.SaveLogAsync(
                actionType: AuditActionType.Insert,
                oldModel: null,  // For Insert, OldValue is null (no previous guarantor record)
                newModel: guarantorForAudit,  // This will be serialized to NewValue column
                tableName: "Loanguar",
                recordId: loanGuarantor.Id.ToString(),
                userId: assignedBy,
                userName: assignedBy,
                companyCode: loan.CompanyCode,
                module: "LoanManagement",
                extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                blockchainTxId: blockchainTx.TransactionId
            );

            // If loan status changed, also audit the loan status change
            if (loanStatusChanged)
            {
                var loanAuditExtraData = new
                {
                    loanNo = loanNo,
                    statusChangedFrom = oldLoanStatus,
                    statusChangedTo = loan.Status,
                    reason = $"First guarantor assigned. Total guarantors: {updatedGuarantorCount}",
                    triggeredBy = assignedBy,
                    triggeredDate = DateTime.Now,
                    guarantorId = loanGuarantor.Id,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.Status,
                    loan.Posted,
                    loan.UserName,
                    loan.AuditDateTime,
                    UpdatedBy = assignedBy,
                    UpdateReason = "Guarantor assigned - loan status changed to Submitted"
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Status = oldLoanStatus, Posted = oldLoanPosted },
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: assignedBy,
                    userName: assignedBy,
                    companyCode: loan.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                _logger.LogInformation($"Loan status change audited for {loanNo}");
            }

            return loanGuarantor;
        }

        public async Task<List<GuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo)
        {
            var guarantors = await _context.Loanguar
                .Where(g => g.LoanNo == loanNo)
                .Select(g => new GuarantorResponseDTO
                {
                    Id = g.Id,
                    LoanNo = g.LoanNo,
                    GuarantorMemberNo = g.MemberNo,
                    GuarantorName = g.FullNames ?? "",
                    IdNo = _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.Idno).FirstOrDefault() ?? "",
                    PhoneNo = _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.PhoneNo).FirstOrDefault() ?? "",
                    GuaranteeAmount = g.Amount ?? 0,
                    AvailableShares = 0,
                    Status = g.Transfered == false ? "Pending" : "Released",
                    AssignedDate = g.AuditTime ?? DateTime.Now,
                    ApprovedDate = null,
                    ApprovedBy = null,
                    Remarks = g.Description
                })
                .ToListAsync();

            return guarantors;
        }

        public async Task<bool> ReleaseGuarantorAsync(int guarantorId, string releasedBy)
        {
            var guarantor = await _context.Loanguar
                .FirstOrDefaultAsync(g => g.Id == guarantorId);

            if (guarantor == null)
            {
                throw new InvalidOperationException("Guarantor not found");
            }

            if (guarantor.Transfered == true)
            {
                throw new InvalidOperationException($"Cannot release guarantor that is already transferred");
            }

            guarantor.Transfered = true;
            guarantor.Transdate = DateTime.Now;
            await _context.SaveChangesAsync();

            var blockchainData = new
            {
                Id = guarantor.Id,
                LoanNo = guarantor.LoanNo,
                GuarantorMemberNo = guarantor.MemberNo,
                GuaranteeAmount = guarantor.Amount,
                ReleasedBy = releasedBy,
                ReleasedDate = DateTime.Now
            };

            var blockchainTx = new BlockchainTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                TransactionType = "LOAN_GUARANTOR_RELEASED",
                MemberNo = guarantor.MemberNo,
                CompanyCode = guarantor.CompanyCode,
                Amount = guarantor.Amount ?? 0,
                Timestamp = DateTime.Now,
                DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                OffChainReferenceId = $"{guarantor.LoanNo}-{guarantor.MemberNo}-released",
                Status = "PENDING",
                CreatedAt = DateTime.Now
            };

            _context.BlockchainTransactions.Add(blockchainTx);
            await _context.SaveChangesAsync();

            guarantor.BlockchainTxId = blockchainTx.TransactionId;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Blockchain transaction recorded for guarantor release: {blockchainTx.TransactionId}");

            return true;
        }

        public async Task<bool> ValidateGuarantorEligibilityAsync(string memberNo, decimal guaranteeAmount, string companyCode)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                _logger.LogWarning($"Guarantor {memberNo} not found");
                return false;
            }

            // Check if member is active
            if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
            {
                _logger.LogWarning($"Guarantor {memberNo} is not active");
                return false;
            }

            // 1. GET TOTAL DEPOSITSAMOUNT FROM CONTRIBSHARE TABLE
            var totalDeposits = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                .SumAsync(cs => cs.DepositsAmount ?? 0);

            if (totalDeposits <= 0)
            {
                _logger.LogWarning($"Guarantor {memberNo} has no deposits. Total: {totalDeposits:C}");
                return false;
            }

            _logger.LogInformation($"Guarantor {memberNo}: Total Deposits = {totalDeposits:C}");

            // 2. GET ACTIVE LOANS (NOT closed/rejected/written off)
            var activeLoanNos = await _context.Loans
                .Where(l => l.CompanyCode == companyCode &&
                           l.Status != (int)Status.Closed &&
                           l.Status != (int)Status.Rejected &&
                           l.Status != (int)Status.WrittenOff)
                .Select(l => l.LoanNo)
                .ToListAsync();

            // 3. GET AMOUNT LOCKED FOR ACTIVE LOANS ONLY
            var lockedAmount = await _context.Loanguar
                .Where(g => g.MemberNo == memberNo &&
                           g.CompanyCode == companyCode &&
                           g.Transfered == false &&
                           activeLoanNos.Contains(g.LoanNo))
                .SumAsync(g => g.Balance ?? g.Amount ?? 0);

            var availableDeposits = totalDeposits - lockedAmount;

            _logger.LogInformation($"Guarantor {memberNo}: Total: {totalDeposits:C}, Locked: {lockedAmount:C}, Available: {availableDeposits:C}");

            // 4. CHECK IF GUARANTEE AMOUNT CAN BE COVERED
            if (guaranteeAmount > availableDeposits)
            {
                _logger.LogWarning($"Guarantor {memberNo} cannot guarantee {guaranteeAmount:C}. Available: {availableDeposits:C}");
                return false;
            }

            // 5. CHECK MINIMUM REQUIREMENT
            var saccoParams = await _context.SaccoParram
                .FirstOrDefaultAsync(s => s.CompanyCode == companyCode);

            var minDepositRequirement = saccoParams?.MinGuarantor ?? 0;

            if (minDepositRequirement > 0 && availableDeposits < minDepositRequirement)
            {
                _logger.LogWarning($"Guarantor {memberNo} available deposits ({availableDeposits:C}) below minimum requirement ({minDepositRequirement:C})");
                return false;
            }

            _logger.LogInformation($"Guarantor {memberNo} is eligible. Available: {availableDeposits:C}, Requested: {guaranteeAmount:C}");

            return true;
        }

        public async Task<decimal> GetMemberEligibleSharesForGuaranteeAsync(string memberNo, string companyCode)
        {
            try
            {
                // Get total deposits from ContribShares
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                if (totalDeposits <= 0)
                    return 0;

                // Get active loans only
                var activeLoanNos = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                // Get locked amount for active loans only
                var lockedAmount = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo &&
                               g.CompanyCode == companyCode &&
                               g.Transfered == false &&
                               activeLoanNos.Contains(g.LoanNo))
                    .SumAsync(g => g.Balance ?? g.Amount ?? 0);

                var availableAmount = totalDeposits - lockedAmount;

                _logger.LogInformation($"Member {memberNo}: Total Deposits: {totalDeposits:C}, Locked: {lockedAmount:C}, Available: {availableAmount:C}");

                return Math.Max(0, availableAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting eligible shares for {memberNo}");
                return 0;
            }
        }


        #endregion


        #region Collateral Guarantee Management

        public async Task<List<MemberCollateralDTO>> GetMemberAvailableCollateralsAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Getting available collaterals for member: {memberNo}");

                // Step 1: Get ALL collaterals belonging to this member from Collateral table
                // This matches: WHERE c.MemberNo = 'J260721020650' AND c.CompanyCode = 'SACCO145111624-001'
                var memberCollaterals = await _context.Collaterals
                    .Where(c => c.CompanyCode == companyCode && c.MemberNo == memberNo)
                    .ToListAsync();

                if (!memberCollaterals.Any())
                {
                    _logger.LogInformation($"No collaterals found for member {memberNo}");
                    return new List<MemberCollateralDTO>();
                }

                _logger.LogInformation($"Found {memberCollaterals.Count} collaterals for member {memberNo}");

                // Step 2: Get all used collateral types (across ALL active loans)
                // This matches: WHERE cg.Balance > 0
                var usedCollateralTypes = await _context.ColloanGuars
                    .Where(cg => cg.CompanyCode == companyCode && cg.Balance > 0)
                    .Select(cg => cg.ColCode)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation($"Found {usedCollateralTypes.Count} collateral types already in use");

                // Step 3: Filter to ONLY collaterals that are NOT used
                // This matches: AND cg.ColCode IS NULL
                var availableMemberCollaterals = memberCollaterals
                    .Where(c => !usedCollateralTypes.Contains(c.ColCode))
                    .ToList();

                _logger.LogInformation($"Found {availableMemberCollaterals.Count} available collaterals for member {memberNo}");

                var result = new List<MemberCollateralDTO>();

                foreach (var collateral in availableMemberCollaterals)
                {
                    // Get used collaterals for this type (if any)
                    var usedForThisType = await _context.ColloanGuars
                        .Where(cg => cg.ColCode == collateral.ColCode && cg.CompanyCode == companyCode && cg.Balance > 0)
                        .ToListAsync();

                    var usedCount = usedForThisType.Count;
                    var totalUsedAmount = usedForThisType.Sum(cg => cg.Balance);

                    // Get market value from first used collateral or use default
                    var firstUsed = usedForThisType.FirstOrDefault();
                    var marketValue = firstUsed?.Mktvalue ?? 0;

                    // Calculate max guarantee based on percentage
                    var maxGuaranteeAmount = marketValue * (decimal)(collateral.Percentage / 100);

                    // If no market value available, use a reasonable default
                    if (maxGuaranteeAmount == 0 && collateral.Percentage > 0)
                    {
                        maxGuaranteeAmount = 100000m * (decimal)(collateral.Percentage / 100);
                    }

                    var availableAmount = Math.Max(0, maxGuaranteeAmount - totalUsedAmount);

                    // Get document numbers used for this collateral type
                    var documentNumbers = usedForThisType.Select(cg => cg.DocNo).ToList();

                    result.Add(new MemberCollateralDTO
                    {
                        ColCode = collateral.ColCode,
                        Coldescription = collateral.Coldescription,
                        Percentage = collateral.Percentage,
                        MarketValue = marketValue,
                        MaxGuaranteeAmount = maxGuaranteeAmount,
                        CurrentlyUsedAmount = totalUsedAmount,
                        AvailableAmount = availableAmount,
                        IsActive = true,
                        IsUsed = usedCount > 0,
                        UsedCount = usedCount,
                        TotalUsedAmount = totalUsedAmount,
                        DocumentNumbers = documentNumbers,
                        IsAvailable = !usedCollateralTypes.Contains(collateral.ColCode),
                        MemberNo = collateral.MemberNo
                    });

                    _logger.LogInformation($"Collateral {collateral.ColCode}: IsUsed={usedCount > 0}, Available={availableAmount:C}");
                }

                _logger.LogInformation($"Returning {result.Count} available collaterals for member {memberNo}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting available collaterals for member {memberNo}: {ex.Message}");
                return new List<MemberCollateralDTO>();
            }
        }
        public async Task<ColloanGuar> AssignCollateralGuaranteeAsync(CollateralGuaranteeDTO guaranteeDto, string assignedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Assigning collateral {guaranteeDto.ColCode} to loan {guaranteeDto.LoanNo}");

                // Get the loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == guaranteeDto.LoanNo && l.CompanyCode == guaranteeDto.CompanyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {guaranteeDto.LoanNo} not found");

                // Store old loan status for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                bool loanStatusChanged = false;

                // Check loan status
                if (loan.Status != (int)Status.Draft && loan.Status != (int)Status.Submitted)
                    throw new InvalidOperationException($"Cannot assign collateral to loan in status '{loan.Status}'");

                // Get collateral type
                var collateral = await _context.Collaterals
                    .FirstOrDefaultAsync(c => c.ColCode == guaranteeDto.ColCode && c.CompanyCode == guaranteeDto.CompanyCode);

                if (collateral == null)
                    throw new InvalidOperationException($"Collateral type {guaranteeDto.ColCode} not found");

                // ============================================================
                // SECURITY CHECK: this collateral must belong to the LOAN'S member (the loanee),
                // not just to "guaranteeDto.MemberNo" as submitted, and not to any other member
                // in the company. This is enforced here, server-side, regardless of what the
                // dropdown showed, so a tampered/replayed form can't pledge someone else's asset.
                // ============================================================
                if (!string.Equals(collateral.MemberNo, loan.MemberNo, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Collateral {guaranteeDto.ColCode} does not belong to member {loan.MemberNo} and cannot be used to guarantee this loan");

                // Get member details for audit
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == guaranteeDto.MemberNo && m.CompanyCode == guaranteeDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : guaranteeDto.MemberNo;

                // Check if this document is already used for an active loan
                var existingGuarantee = await _context.ColloanGuars
                    .FirstOrDefaultAsync(cg => cg.ColCode == guaranteeDto.ColCode &&
                                               cg.DocNo == guaranteeDto.DocNo &&
                                               cg.MemberNo == guaranteeDto.MemberNo &&
                                               cg.Balance > 0 &&
                                               cg.CompanyCode == guaranteeDto.CompanyCode);

                if (existingGuarantee != null)
                    throw new InvalidOperationException($"This collateral (DocNo: {guaranteeDto.DocNo}) is already used to guarantee loan {existingGuarantee.LoanNo}");

                // Calculate maximum allowed guarantee amount based on collateral percentage
                decimal maxGuaranteeAmount = guaranteeDto.MarketValue * (decimal)(collateral.Percentage / 100);

                if (guaranteeDto.GuaranteeAmount > maxGuaranteeAmount)
                    throw new InvalidOperationException($"Guarantee amount {guaranteeDto.GuaranteeAmount:C} exceeds maximum allowed {maxGuaranteeAmount:C} ({collateral.Percentage}% of market value)");

                // Check if loan amount is covered
                var existingCollateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == guaranteeDto.LoanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);

                var existingLoanguar = await _context.Loanguar
                    .Where(lg => lg.LoanNo == guaranteeDto.LoanNo && lg.Transfered == false)
                    .SumAsync(lg => lg.Amount ?? 0);

                var totalGuaranteeBefore = existingCollateralGuarantees + existingLoanguar;
                var totalGuaranteeAfter = totalGuaranteeBefore + guaranteeDto.GuaranteeAmount;
                var loanAmount = loan.LoanAmt ?? 0;

                if (totalGuaranteeAfter > loanAmount)
                    throw new InvalidOperationException($"Total guarantee amount ({totalGuaranteeAfter:C}) exceeds loan amount ({loanAmount:C})");

                // Create the collateral guarantee record
                var colloanGuar = new ColloanGuar
                {
                    ColCode = guaranteeDto.ColCode,
                    MemberNo = guaranteeDto.MemberNo,
                    DocNo = guaranteeDto.DocNo,
                    Mktvalue = guaranteeDto.MarketValue,
                    LoanNo = guaranteeDto.LoanNo,
                    Balance = guaranteeDto.GuaranteeAmount,
                    AuditId = assignedBy,
                    CompanyCode = guaranteeDto.CompanyCode
                };

                _context.ColloanGuars.Add(colloanGuar);
                await _context.SaveChangesAsync();

                // ============================================================
                // UPDATE LOAN STATUS AFTER ADDING COLLATERAL
                // ============================================================
                // Check if loan is now fully guaranteed or has any guarantee
                var updatedCollateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == guaranteeDto.LoanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);

                var updatedMemberGuarantees = await _context.Loanguar
                    .Where(lg => lg.LoanNo == guaranteeDto.LoanNo && lg.Transfered == false)
                    .SumAsync(lg => lg.Amount ?? 0);

                var newTotalGuarantee = updatedCollateralGuarantees + updatedMemberGuarantees;

                // If loan is fully guaranteed or has any guarantee, update status to Submitted
                if (loan.Status == (int)Status.Draft && newTotalGuarantee > 0)
                {
                    loan.Status = (int)Status.Submitted;
                    loan.Posted = "SUBMIT";
                    loan.UserName = assignedBy;
                    loan.AuditDateTime = DateTime.Now;
                    loanStatusChanged = true;
                    _context.Loans.Update(loan);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Loan {loan.LoanNo} status updated from Draft to Submitted. Total guarantees: {newTotalGuarantee:C}");
                }
                else if (newTotalGuarantee >= loanAmount && loan.Status != (int)Status.Closed)
                {
                    // If fully guaranteed, ensure status is Submitted (or Approved if you want auto-approve)
                    if (loan.Status == (int)Status.Draft)
                    {
                        loan.Status = (int)Status.Submitted;
                        loan.Posted = "SUBMIT";
                        loan.UserName = assignedBy;
                        loan.AuditDateTime = DateTime.Now;
                        loanStatusChanged = true;
                        _context.Loans.Update(loan);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Loan {loan.LoanNo} is now fully guaranteed! Status: Submitted");
                    }
                }

                // Record blockchain transaction
                var blockchainData = new
                {
                    Action = "COLLATERAL_GUARANTEE_ASSIGN",
                    CollateralGuaranteeId = colloanGuar.Id,
                    LoanNo = guaranteeDto.LoanNo,
                    MemberNo = guaranteeDto.MemberNo,
                    MemberName = memberName,
                    ColCode = guaranteeDto.ColCode,
                    ColDescription = collateral.Coldescription,
                    DocNo = guaranteeDto.DocNo,
                    MarketValue = guaranteeDto.MarketValue,
                    GuaranteeAmount = guaranteeDto.GuaranteeAmount,
                    Percentage = collateral.Percentage,
                    TotalGuaranteeBefore = totalGuaranteeBefore,
                    TotalGuaranteeAfter = newTotalGuarantee,
                    LoanAmount = loanAmount,
                    LoanStatusBefore = oldLoanStatus,
                    LoanStatusAfter = loan.Status,
                    AssignedBy = assignedBy,
                    AssignedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "COLLATERAL_GUARANTEE_ASSIGN",
                    MemberNo = guaranteeDto.MemberNo,
                    CompanyCode = guaranteeDto.CompanyCode,
                    Amount = guaranteeDto.GuaranteeAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = guaranteeDto.LoanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                colloanGuar.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Collateral guarantee assigned successfully. Id: {colloanGuar.Id}, Loan Status: {loan.Status}");

                // ============================================================
                // SAVE AUDIT TRAIL FOR COLLATERAL GUARANTEE ASSIGNMENT
                // ============================================================

                // Create audit extra data
                var auditExtraData = new
                {
                    loanNo = guaranteeDto.LoanNo,
                    applicantMemberNo = loan.MemberNo,
                    collateralOwnerMemberNo = guaranteeDto.MemberNo,
                    collateralOwnerName = memberName,
                    colCode = guaranteeDto.ColCode,
                    colDescription = collateral.Coldescription,
                    documentNo = guaranteeDto.DocNo,
                    marketValue = guaranteeDto.MarketValue,
                    guaranteeAmount = guaranteeDto.GuaranteeAmount,
                    percentageUsed = collateral.Percentage,
                    maxGuaranteeAllowed = maxGuaranteeAmount,
                    totalGuaranteeBefore = totalGuaranteeBefore,
                    totalGuaranteeAfter = newTotalGuarantee,
                    loanAmount = loanAmount,
                    isFullyGuaranteed = newTotalGuarantee >= loanAmount,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanStatusChanged = loanStatusChanged,
                    assignedBy = assignedBy,
                    assignedDate = DateTime.Now,
                    blockchainTxId = blockchainTx.TransactionId
                };

                // Create a copy of the collateral guarantee object for NewValue
                var collateralForAudit = new
                {
                    colloanGuar.Id,
                    colloanGuar.ColCode,
                    colloanGuar.MemberNo,
                    colloanGuar.DocNo,
                    colloanGuar.Mktvalue,
                    colloanGuar.LoanNo,
                    colloanGuar.Balance,
                    colloanGuar.AuditId,
                    colloanGuar.CompanyCode,
                    AssignedBy = assignedBy,
                    AssignedDate = DateTime.Now,
                    CollateralDescription = collateral.Coldescription,
                    PercentageUsed = collateral.Percentage,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,  // For Insert, OldValue is null (no previous collateral record)
                    newModel: collateralForAudit,  // This will be serialized to NewValue column
                    tableName: "ColloanGuar",
                    recordId: colloanGuar.Id.ToString(),
                    userId: assignedBy,
                    userName: assignedBy,
                    companyCode: guaranteeDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // If loan status changed, also audit the loan status change
                if (loanStatusChanged)
                {
                    var loanAuditExtraData = new
                    {
                        loanNo = guaranteeDto.LoanNo,
                        statusChangedFrom = oldLoanStatus,
                        statusChangedTo = loan.Status,
                        reason = $"Collateral guarantee assigned. Total guarantees: {newTotalGuarantee:C}",
                        triggeredBy = assignedBy,
                        triggeredDate = DateTime.Now,
                        collateralId = colloanGuar.Id,
                        collateralDocNo = guaranteeDto.DocNo,
                        collateralAmount = guaranteeDto.GuaranteeAmount,
                        blockchainTxId = blockchainTx.TransactionId
                    };

                    var loanForAudit = new
                    {
                        loan.LoanNo,
                        loan.Status,
                        loan.Posted,
                        loan.UserName,
                        loan.AuditDateTime,
                        UpdatedBy = assignedBy,
                        UpdateReason = "Collateral guarantee assigned - loan status changed to Submitted"
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new { Status = oldLoanStatus, Posted = oldLoanPosted },
                        newModel: loanForAudit,
                        tableName: "Loans",
                        recordId: guaranteeDto.LoanNo,
                        userId: assignedBy,
                        userName: assignedBy,
                        companyCode: guaranteeDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                        blockchainTxId: blockchainTx.TransactionId
                    );

                    _logger.LogInformation($"Loan status change audited for {guaranteeDto.LoanNo}");
                }

                await transaction.CommitAsync();

                return colloanGuar;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error assigning collateral guarantee for loan {guaranteeDto.LoanNo}");
                throw;
            }
        }

        public async Task<List<CollateralGuaranteeResponseDTO>> GetLoanCollateralGuaranteesAsync(string loanNo)
        {
            try
            {
                _logger.LogInformation($"Getting collateral guarantees for loan: {loanNo}");

                var guarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .ToListAsync();

                if (!guarantees.Any())
                    return new List<CollateralGuaranteeResponseDTO>();

                // Get the loan to get member number
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null)
                    return new List<CollateralGuaranteeResponseDTO>();

                var colCodes = guarantees.Select(g => g.ColCode).Distinct().ToList();

                // Filter by MemberNo as well
                var collaterals = await _context.Collaterals
                    .Where(c => colCodes.Contains(c.ColCode) && c.MemberNo == loan.MemberNo)
                    .ToDictionaryAsync(c => c.ColCode, c => c);

                var result = new List<CollateralGuaranteeResponseDTO>();

                foreach (var guarantee in guarantees)
                {
                    var collateral = collaterals.GetValueOrDefault(guarantee.ColCode);

                    result.Add(new CollateralGuaranteeResponseDTO
                    {
                        Id = guarantee.Id,
                        ColCode = guarantee.ColCode,
                        Coldescription = collateral?.Coldescription ?? guarantee.ColCode,
                        DocNo = guarantee.DocNo,
                        MarketValue = guarantee.Mktvalue,
                        GuaranteeAmount = guarantee.Balance,
                        RemainingBalance = guarantee.Balance,
                        AssignedDate = DateTime.Now,
                        BlockchainTxId = guarantee.BlockchainTxId
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting collateral guarantees for loan {loanNo}");
                return new List<CollateralGuaranteeResponseDTO>();
            }
        }

        public async Task<bool> ReleaseCollateralGuaranteeAsync(long collateralGuaranteeId, string releasedBy, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var guarantee = await _context.ColloanGuars
                    .FirstOrDefaultAsync(cg => cg.Id == collateralGuaranteeId);

                if (guarantee == null)
                    throw new InvalidOperationException($"Collateral guarantee with ID {collateralGuaranteeId} not found");

                if (guarantee.Balance <= 0)
                    throw new InvalidOperationException("Collateral guarantee already released");

                var originalBalance = guarantee.Balance;

                // Get the loan before releasing
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == guarantee.LoanNo && l.CompanyCode == guarantee.CompanyCode);

                // Release by setting balance to 0
                guarantee.Balance = 0;
                guarantee.AuditId = releasedBy;

                await _context.SaveChangesAsync();

                // ============================================================
                // FIX: RECALCULATE LOAN STATUS AFTER RELEASING COLLATERAL
                // ============================================================
                if (loan != null)
                {
                    // Get remaining guarantees
                    var remainingCollateralGuarantees = await _context.ColloanGuars
                        .Where(cg => cg.LoanNo == guarantee.LoanNo && cg.Balance > 0)
                        .SumAsync(cg => cg.Balance);

                    var remainingMemberGuarantees = await _context.Loanguar
                        .Where(lg => lg.LoanNo == guarantee.LoanNo && lg.Transfered == false)
                        .SumAsync(lg => lg.Amount ?? 0);

                    var totalRemainingGuarantee = remainingCollateralGuarantees + remainingMemberGuarantees;
                    var loanAmount = loan.LoanAmt ?? 0;

                    // If no guarantees left, revert to Draft
                    if (totalRemainingGuarantee <= 0 && loan.Status == (int)Status.Submitted)
                    {
                        loan.Status = (int)Status.Draft;
                        loan.Posted = "Draft";
                        loan.UserName = releasedBy;
                        loan.AuditDateTime = DateTime.Now;
                        _context.Loans.Update(loan);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Loan {loan.LoanNo} reverted to Draft - no guarantees remaining");
                    }
                    // If still has guarantees but not fully covered, keep as Submitted
                    else if (totalRemainingGuarantee < loanAmount && loan.Status == (int)Status.Submitted)
                    {
                        // Keep as Submitted, no change needed
                        _logger.LogInformation($"Loan {loan.LoanNo} remains in Submitted status with {totalRemainingGuarantee:C} guarantee remaining");
                    }
                }

                // Record blockchain transaction for release
                var blockchainData = new
                {
                    Action = "COLLATERAL_GUARANTEE_RELEASE",
                    CollateralGuaranteeId = guarantee.Id,
                    LoanNo = guarantee.LoanNo,
                    MemberNo = guarantee.MemberNo,
                    ColCode = guarantee.ColCode,
                    DocNo = guarantee.DocNo,
                    OriginalBalance = originalBalance,
                    ReleasedBy = releasedBy,
                    Reason = reason,
                    ReleasedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "COLLATERAL_GUARANTEE_RELEASE",
                    MemberNo = guarantee.MemberNo,
                    CompanyCode = guarantee.CompanyCode,
                    Amount = originalBalance,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = guarantee.LoanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                guarantee.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Collateral guarantee {collateralGuaranteeId} released. Amount: {originalBalance:C}, Reason: {reason}");

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error releasing collateral guarantee {collateralGuaranteeId}");
                throw;
            }
        }
        public async Task<decimal> GetTotalCollateralGuaranteeAmountAsync(string loanNo)
        {
            try
            {
                return await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting total collateral guarantee for loan {loanNo}");
                return 0;
            }
        }
        public async Task<decimal> GetTotalGuaranteeForLoanAsync(string loanNo, string companyCode)
        {
            try
            {
                // Get member guarantors total
                var memberGuarantee = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .SumAsync(g => g.Amount ?? 0);

                // Get collateral guarantees total
                var collateralGuarantee = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);

                var total = memberGuarantee + collateralGuarantee;

                _logger.LogInformation($"Loan {loanNo} - Member Guarantee: {memberGuarantee:C}, Collateral Guarantee: {collateralGuarantee:C}, Total: {total:C}");

                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting total guarantee for loan {loanNo}");
                return 0;
            }
        }
        public async Task<(bool IsValid, string Message, AvailableCollateralDTO? Data)> ValidateCollateralForLoanAsync(
            string memberNo, string colCode, string loanNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Validating collateral {colCode} for member {memberNo} on loan {loanNo}");

                // Get the loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                    return (false, "Loan not found", null);

                // Get the collateral type
                var collateral = await _context.Collaterals
                    .FirstOrDefaultAsync(c => c.ColCode == colCode && c.CompanyCode == companyCode);

                if (collateral == null)
                    return (false, $"Collateral type {colCode} not found", null);

                // Check if this member owns this collateral (this would come from a MemberCollateral table)
                // For now, we assume the member can use any collateral type
                // In a real system, you'd have a MemberCollateral table linking members to their collaterals

                // Get existing collateral guarantees for this loan
                var existingCollateralGuarantee = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .SumAsync(cg => cg.Balance);

                // Get existing member guarantees
                var existingMemberGuarantee = await _context.Loanguar
                    .Where(lg => lg.LoanNo == loanNo && lg.Transfered == false)
                    .SumAsync(lg => lg.Amount ?? 0);

                var totalExistingGuarantee = existingCollateralGuarantee + existingMemberGuarantee;
                var remainingLoanAmount = (loan.LoanAmt ?? 0) - totalExistingGuarantee;

                if (remainingLoanAmount <= 0)
                    return (false, "Loan is already fully guaranteed", null);

                // Maximum guarantee from this collateral
                decimal maxGuaranteeAmount = 0;
                // This would need the member's specific collateral market value
                // For now, return the collateral type info without specific amount

                var availableCollateral = new AvailableCollateralDTO
                {
                    ColCode = collateral.ColCode,
                    Coldescription = collateral.Coldescription,
                    Percentage = collateral.Percentage,
                    MaxGuaranteeAmount = 0, // Would need member's specific collateral value
                    IsAvailable = true,
                    OriginalMarketValue = 0,
                    ExistingGuaranteeBalance = 0
                };

                return (true, $"Collateral {collateral.Coldescription} can be used up to {collateral.Percentage}% of its market value", availableCollateral);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating collateral for loan {loanNo}");
                return (false, $"Error validating collateral: {ex.Message}", null);
            }
        }

        #endregion


        #region Loan Appraisal

        public async Task<Appraisal> AppraiseLoanAsync(LoanAppraisalDTO appraisalDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Starting appraisal for loan {appraisalDto.LoanNo}");

                var loan = await GetLoanByNoForDisplayAsync(appraisalDto.LoanNo, appraisalDto.CompanyCode);

                if (loan == null)
                {
                    throw new InvalidOperationException($"Loan {appraisalDto.LoanNo} not found");
                }

                _logger.LogInformation($"Current loan status: {loan.Status}");
                _logger.LogInformation($"Recommended amount: {appraisalDto.RecommendedAmount}");
                _logger.LogInformation($"Appraisal decision: {appraisalDto.AppraisalDecision}");

                if (loan.Status != (int)Status.Submitted)
                {
                    throw new InvalidOperationException($"Loan cannot be appraised. Current status: {loan.Status}. Expected: Submitted");
                }

                var existingAppraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == appraisalDto.LoanNo);

                if (existingAppraisal != null)
                {
                    throw new InvalidOperationException("This loan has already been appraised.");
                }

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == appraisalDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;

                // Get loan type to get interest rate
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == appraisalDto.CompanyCode);

                // ============================================================
                // STORE OLD VALUES FOR AUDIT
                // ============================================================
                decimal oldLoanAmount = loan.LoanAmt ?? 0;
                decimal oldInterestRate = loan.Interest ?? 0;
                int oldRepayPeriod = loan.RepayPeriod ?? 0;
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";

                // ============================================================
                // GET GUARANTEES AND CHECK SELF-GUARANTEE
                // ============================================================
                var memberGuarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == appraisalDto.LoanNo && g.Transfered == false)
                    .ToListAsync();
                var totalMemberGuarantee = memberGuarantors.Sum(g => g.Amount ?? 0);

                var collateralGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == appraisalDto.LoanNo && cg.Balance > 0)
                    .ToListAsync();
                var totalCollateralGuarantee = collateralGuarantees.Sum(cg => cg.Balance);

                var totalGuarantee = totalMemberGuarantee + totalCollateralGuarantee;
                var isSelfGuarantee = loanType?.SelfGuarantee ?? false;
                var isApplicantGuarantor = memberGuarantors.Any(g => g.MemberNo == loan.MemberNo);

                // Check if loan requires guarantors
                var requiresGuarantor = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                        loanType.Guarantor != "No" &&
                                        loanType.Guarantor != "N";

                bool canProceedToAppraisal = false;

                if (requiresGuarantor)
                {
                    if (isSelfGuarantee && isApplicantGuarantor)
                    {
                        canProceedToAppraisal = true;
                        _logger.LogInformation($"Self-guarantee enabled - Applicant is guarantor. Proceeding with appraisal even with partial guarantee. Total: {totalGuarantee:C}, Loan: {loan.LoanAmt:C}");
                    }
                    else if (totalGuarantee >= (loan.LoanAmt ?? 0))
                    {
                        canProceedToAppraisal = true;
                        _logger.LogInformation($"Loan fully guaranteed: {totalGuarantee:C} >= {loan.LoanAmt:C}");
                    }
                    else if (totalGuarantee > 0)
                    {
                        if (isSelfGuarantee)
                        {
                            canProceedToAppraisal = true;
                            _logger.LogInformation($"Partial guarantee ({totalGuarantee:C}) allowed due to self-guarantee enabled");
                        }
                        else
                        {
                            throw new InvalidOperationException($"Loan is not fully guaranteed. Total guarantee: {totalGuarantee:C}, Loan amount: {loan.LoanAmt:C}. Remaining: {(loan.LoanAmt ?? 0) - totalGuarantee:C}. Please add more guarantees or enable self-guarantee.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Cannot appraise loan. No guarantees found. Please add member guarantors or collateral guarantees.");
                    }
                }
                else
                {
                    canProceedToAppraisal = true;
                }

                if (!canProceedToAppraisal)
                {
                    throw new InvalidOperationException("Cannot proceed with appraisal. Please ensure guarantees are in place.");
                }

                // Calculate values
                decimal memberShares = member?.ShareCap ?? 0;
                decimal monthlyIncome = (decimal)(member?.MonthlyContr ?? 0);
                decimal existingLoans = appraisalDto.ExistingLoanObligations;
                decimal recommendedAmount = appraisalDto.RecommendedAmount;

                // INTEREST RATE - USE AS-IS FROM LOAN TYPE (store as percentage, e.g., 15 for 15%)
                decimal interestRatePercent = 0;
                if (loanType != null && !string.IsNullOrEmpty(loanType.Interest))
                {
                    string interestStr = loanType.Interest.ToString().Replace("%", "");
                    if (decimal.TryParse(interestStr, out interestRatePercent))
                    {
                        if (interestRatePercent < 1 && interestRatePercent > 0)
                        {
                            interestRatePercent = interestRatePercent * 100;
                        }
                    }
                }

                decimal interestRateDecimal = interestRatePercent / 100;
                decimal monthlyInterestRate = interestRateDecimal / 12;
                int repayPeriod = appraisalDto.RecommendedPeriod;

                decimal monthlyPayment = 0;
                decimal totalInterest = 0;

                if (monthlyInterestRate > 0 && repayPeriod > 0)
                {
                    decimal factor = (decimal)Math.Pow((double)(1 + monthlyInterestRate), repayPeriod);
                    monthlyPayment = recommendedAmount * monthlyInterestRate * factor / (factor - 1);
                    totalInterest = (monthlyPayment * repayPeriod) - recommendedAmount;
                }
                else
                {
                    monthlyPayment = recommendedAmount / repayPeriod;
                }

                var appraisal = new Appraisal
                {
                    LoanNo = appraisalDto.LoanNo,
                    CompanyCode = appraisalDto.CompanyCode,
                    MemberNo = loan.MemberNo,
                    AppraisDate = DateTime.Now,
                    AuditTime = DateTime.Now,
                    AuditID = appraisalDto.AppraisedBy,
                    OfficerNames = appraisalDto.AppraisedBy,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                    Salary = monthlyIncome,
                    Allowances = 0,
                    Shares = memberShares,
                    Loans = existingLoans,
                    Deductions = 0,
                    AmtRecommended = recommendedAmount,
                    TotalDeductions = 0,
                    Principal = recommendedAmount,
                    Interest = interestRatePercent,
                    TotalInterest = totalInterest,
                    RepayMethod = loan?.RepayMethod ?? "STL",
                    RepayRate = monthlyPayment,
                    Reason = appraisalDto.AppraisalNotes,
                    TInterest = interestRatePercent,
                    NetMonthlySalary = monthlyIncome - existingLoans,
                    SocietyPayment = monthlyPayment,
                    ExpectedNetSalary = monthlyIncome - existingLoans - monthlyPayment,
                    DeductionToGross = 0,
                    TotalDedNewLoanToGross = 0,
                    NetSalaryToGross = 0,
                    TotalLoanToGross = 0,
                    TotalCoopDedToGross = 0,
                    BankLoan = 0,
                    Nssf = 0,
                    CopLoanded = 0,
                    OtherDed = 0,
                    StatutoryDed = 0,
                    StatutoryDedToGross = 0,
                    TotalDedToGrossLessStatutory = 0,
                    NoOfLoans = await _context.Loans.CountAsync(l => l.MemberNo == loan.MemberNo && l.CompanyCode == appraisalDto.CompanyCode),
                    LoanGuarantor = 0
                };

                _logger.LogInformation($"Created appraisal record with decision: {appraisalDto.AppraisalDecision}");
                _logger.LogInformation($"Interest Rate: {interestRatePercent}%");

                string oldStatus = loan.Status.ToString();
                bool loanStatusChanged = false;

                if (appraisalDto.AppraisalDecision == "Recommend")
                {
                    loan.LoanAmt = recommendedAmount;
                    loan.Interest = interestRatePercent;
                    loan.RepayPeriod = repayPeriod;
                    loan.Status = (int)Status.Approved;
                    loan.Posted = "APPROVED";
                    loan.UserName = appraisalDto.AppraisedBy;
                    loan.AuditDateTime = DateTime.Now;
                    loanStatusChanged = true;

                    _logger.LogInformation($"Loan status updated from {oldStatus} to Approved");
                }
                else if (appraisalDto.AppraisalDecision == "NotRecommend")
                {
                    loan.Status = (int)Status.Rejected;
                    loan.Posted = "REJECTED";
                    loan.AddSecurity = $"Rejected at appraisal: {appraisalDto.AppraisalNotes}";
                    loan.UserName = appraisalDto.AppraisedBy;
                    loan.AuditDateTime = DateTime.Now;
                    loanStatusChanged = true;

                    _logger.LogInformation($"Loan status updated from {oldStatus} to Rejected");
                }
                else
                {
                    loan.Status = (int)Status.Approved;
                    loan.Posted = "APPROVED";
                    loan.UserName = appraisalDto.AppraisedBy;
                    loan.AuditDateTime = DateTime.Now;
                    loanStatusChanged = true;
                }

                _context.Appraisal.Add(appraisal);
                _context.Loans.Update(loan);
                await _context.SaveChangesAsync();

                // Blockchain transaction
                var blockchainData = new
                {
                    LoanNo = appraisalDto.LoanNo,
                    Amt = recommendedAmount,
                    Rate = interestRatePercent,
                    Period = repayPeriod,
                    Decision = appraisalDto.AppraisalDecision,
                    TotalMemberGuarantee = totalMemberGuarantee,
                    TotalCollateralGuarantee = totalCollateralGuarantee,
                    TotalGuarantee = totalGuarantee,
                    IsSelfGuarantee = isSelfGuarantee,
                    IsApplicantGuarantor = isApplicantGuarantor,
                    By = appraisalDto.AppraisedBy,
                    Date = DateTime.Now,
                    MemberName = memberName,
                    MonthlyIncome = monthlyIncome,
                    ExistingLoans = existingLoans,
                    MonthlyPayment = monthlyPayment,
                    TotalInterest = totalInterest
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_APPRAISAL",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = recommendedAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loan.LoanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                appraisal.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Blockchain transaction recorded for loan appraisal: {blockchainTx.TransactionId}");

                // ============================================================
                // SAVE AUDIT TRAIL FOR APPRAISAL
                // ============================================================

                // Create audit extra data
                var auditExtraData = new
                {
                    loanNo = appraisalDto.LoanNo,
                    applicantMemberNo = loan.MemberNo,
                    applicantName = memberName,
                    appraisalDecision = appraisalDto.AppraisalDecision,
                    recommendedAmount = recommendedAmount,
                    originalLoanAmount = oldLoanAmount,
                    amountChanged = oldLoanAmount != recommendedAmount,
                    recommendedPeriod = repayPeriod,
                    originalRepayPeriod = oldRepayPeriod,
                    periodChanged = oldRepayPeriod != repayPeriod,
                    interestRate = interestRatePercent,
                    originalInterestRate = oldInterestRate,
                    interestRateChanged = oldInterestRate != interestRatePercent,
                    monthlyPayment = monthlyPayment,
                    totalInterest = totalInterest,
                    totalMemberGuarantee = totalMemberGuarantee,
                    totalCollateralGuarantee = totalCollateralGuarantee,
                    totalGuarantee = totalGuarantee,
                    isSelfGuarantee = isSelfGuarantee,
                    isApplicantGuarantor = isApplicantGuarantor,
                    requiresGuarantor = requiresGuarantor,
                    memberMonthlyIncome = monthlyIncome,
                    memberExistingLoans = existingLoans,
                    memberNetIncome = monthlyIncome - existingLoans,
                    expectedNetAfterLoan = monthlyIncome - existingLoans - monthlyPayment,
                    appraisalNotes = appraisalDto.AppraisalNotes ?? "",
                    appraisedBy = appraisalDto.AppraisedBy,
                    appraisedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanStatusChanged = loanStatusChanged,
                    blockchainTxId = blockchainTx.TransactionId
                };

                // Create a copy of the appraisal object for NewValue
                var appraisalForAudit = new
                {
                    appraisal.Id,
                    appraisal.LoanNo,
                    appraisal.MemberNo,
                    appraisal.AppraisDate,
                    appraisal.AmtRecommended,
                    appraisal.Principal,
                    appraisal.Interest,
                    appraisal.TInterest,
                   // appraisal.RepayPeriod = repayPeriod,
                    appraisal.RepayRate,
                    appraisal.TotalInterest,
                    appraisal.Reason,
                    appraisal.OfficerNames,
                    appraisal.Salary,
                    appraisal.Loans,
                    appraisal.Shares,
                    appraisal.NetMonthlySalary,
                    appraisal.SocietyPayment,
                    appraisal.ExpectedNetSalary,
                    AppraisedBy = appraisalDto.AppraisedBy,
                    AppraisalDecision = appraisalDto.AppraisalDecision,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,  // For Insert, OldValue is null (no previous appraisal record)
                    newModel: appraisalForAudit,  // This will be serialized to NewValue column
                    tableName: "Appraisal",
                    recordId: appraisal.Id.ToString(),
                    userId: appraisalDto.AppraisedBy,
                    userName: appraisalDto.AppraisedBy,
                    companyCode: appraisalDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN STATUS CHANGE
                // ============================================================
                if (loanStatusChanged)
                {
                    var loanAuditExtraData = new
                    {
                        loanNo = appraisalDto.LoanNo,
                        statusChangedFrom = oldLoanStatus,
                        statusChangedTo = loan.Status,
                        reason = $"Loan appraised with decision: {appraisalDto.AppraisalDecision}",
                        appraisalNotes = appraisalDto.AppraisalNotes ?? "",
                        recommendedAmount = recommendedAmount,
                        originalLoanAmount = oldLoanAmount,
                        recommendedPeriod = repayPeriod,
                        originalRepayPeriod = oldRepayPeriod,
                        interestRate = interestRatePercent,
                        triggeredBy = appraisalDto.AppraisedBy,
                        triggeredDate = DateTime.Now,
                        appraisalId = appraisal.Id,
                        blockchainTxId = blockchainTx.TransactionId
                    };

                    var loanForAudit = new
                    {
                        loan.LoanNo,
                        loan.LoanAmt,
                        loan.Interest,
                        loan.RepayPeriod,
                        loan.Status,
                        loan.Posted,
                        loan.UserName,
                        loan.AuditDateTime,
                        loan.AddSecurity,
                        UpdatedBy = appraisalDto.AppraisedBy,
                        UpdateReason = $"Loan {appraisalDto.AppraisalDecision.ToLower()} during appraisal"
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new
                        {
                            Status = oldLoanStatus,
                            Posted = oldLoanPosted,
                            LoanAmt = oldLoanAmount,
                            Interest = oldInterestRate,
                            RepayPeriod = oldRepayPeriod
                        },
                        newModel: loanForAudit,
                        tableName: "Loans",
                        recordId: appraisalDto.LoanNo,
                        userId: appraisalDto.AppraisedBy,
                        userName: appraisalDto.AppraisedBy,
                        companyCode: appraisalDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                        blockchainTxId: blockchainTx.TransactionId
                    );

                    _logger.LogInformation($"Loan status change audited for {appraisalDto.LoanNo}");
                }

                await transaction.CommitAsync();

                return appraisal;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in AppraiseLoanAsync for loan {appraisalDto.LoanNo}");
                throw;
            }
        }


        public async Task<Appraisal?> GetLoanAppraisalAsync(string loanNo)
        {
            return await _context.Appraisal
                .FirstOrDefaultAsync(a => a.LoanNo == loanNo);
        }

        public async Task<Endmain> ApproveLoanAsync(LoanApprovalDTO approvalDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await GetLoanByNoForDisplayAsync(approvalDto.LoanNo, approvalDto.CompanyCode);

                if (loan == null)
                {
                    throw new InvalidOperationException($"Loan {approvalDto.LoanNo} not found");
                }

                // Store old loan values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                string oldAddSecurity = loan.AddSecurity ?? "";
                decimal oldLoanAmount = loan.LoanAmt ?? 0;

                var appraisal = await GetLoanAppraisalAsync(approvalDto.LoanNo);

                if (appraisal == null)
                {
                    throw new InvalidOperationException("Loan must be appraised before approval");
                }

                // Get member details for audit
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == approvalDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;

                bool isApproved = approvalDto.ApprovalStatus == "Approved";
                bool isRejected = approvalDto.ApprovalStatus == "Rejected";
                bool loanStatusChanged = true;
                Cheque? cheque = null;

                var endmain = new Endmain
                {
                    LoanNo = approvalDto.LoanNo,
                    CompanyCode = approvalDto.CompanyCode,
                    MinuteNo = Guid.NewGuid().ToString().Substring(0, 10),
                    MeetingDate = DateTime.Now,
                    AmtApproved = approvalDto.ApprovedAmount ?? appraisal.AmtRecommended ?? 0,
                    Accepted = approvalDto.ApprovalStatus,
                    ChairSigned = approvalDto.ApprovedBy,
                    SecSigned = approvalDto.ApprovedBy,
                    MembSigned = loan.MemberNo,
                    Reasons = approvalDto.ApprovalComments,
                    Remarks = approvalDto.RejectionReason,
                    AuditId = approvalDto.ApprovedBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15)
                };

                _context.Endmain.Add(endmain);
                await _context.SaveChangesAsync();

                if (isApproved)
                {
                    loan.LoanAmt = endmain.AmtApproved;
                    loan.Status = (int)Status.Approved;
                    loan.Posted = "Approved";
                    loan.UserName = approvalDto.ApprovedBy;
                    loan.AuditDateTime = DateTime.Now;

                    await _context.SaveChangesAsync();

                    cheque = new Cheque
                    {
                        LoanNo = approvalDto.LoanNo,
                        MemberNo = loan.MemberNo,
                        CompanyCode = approvalDto.CompanyCode,
                        Amount = endmain.AmtApproved,
                        AmountIssued = endmain.AmtApproved,
                        DateIssued = DateTime.Now,
                        Status = "Pending",
                        AuditId = approvalDto.ApprovedBy,
                        AuditTime = DateTime.Now,
                        TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                        Voucherno = Guid.NewGuid().ToString().Substring(0, 10),
                        Voucheramount = endmain.AmtApproved,
                        Paymethod = "BANK",
                        Amountinword = endmain.AmtApproved.ToString(),
                        Refloan = true,
                        Dregard = 0,
                        PaidBf = 0,
                        OrgAmt = endmain.AmtApproved,
                        LoanAcc = "LOAN_ASSET_ACCOUNT",
                        ContraAcc = "BANK_ACCOUNT",
                        PremiumAcc = "PREMIUM_ACCOUNT",
                        Offsetamount = 0,
                        IntrOwed = 0
                    };

                    _context.Cheques.Add(cheque);
                    await _context.SaveChangesAsync();

                    // Blockchain transaction for Cheque
                    var blockchainChequeData = new
                    {
                        ChequeId = cheque.Id,
                        LoanNo = approvalDto.LoanNo,
                        Amount = cheque.Amount,
                        DateIssued = cheque.DateIssued,
                        Status = cheque.Status,
                        VoucherNo = cheque.Voucherno,
                        MemberName = memberName,
                        ApprovedBy = approvalDto.ApprovedBy
                    };

                    var blockchainChequeTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "CHEQUE_CREATED",
                        MemberNo = loan.MemberNo,
                        CompanyCode = loan.CompanyCode,
                        Amount = cheque.Amount ?? 0,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainChequeData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainChequeData),
                        OffChainReferenceId = cheque.Voucherno,
                        Status = "PENDING",
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainChequeTx);
                    await _context.SaveChangesAsync();

                    cheque.BlockchainTxId = blockchainChequeTx.TransactionId;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Cheque created for loan {approvalDto.LoanNo}: {cheque.Voucherno}");
                }
                else if (isRejected)
                {
                    loan.Status = (int)Status.Rejected;
                    loan.AddSecurity = $"Rejected: {approvalDto.RejectionReason}";
                    loan.UserName = approvalDto.ApprovedBy;
                    loan.AuditDateTime = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                // Blockchain transaction for Loan Approval
                var blockchainData = new
                {
                    EndmainId = endmain.Id,
                    LoanNo = approvalDto.LoanNo,
                    ApprovalStatus = approvalDto.ApprovalStatus,
                    ApprovedAmount = endmain.AmtApproved,
                    AppraisedAmount = appraisal.AmtRecommended,
                    ApprovalComments = approvalDto.ApprovalComments,
                    RejectionReason = approvalDto.RejectionReason,
                    ApprovedBy = approvalDto.ApprovedBy,
                    ApprovalDate = DateTime.Now,
                    IsFinalApproval = approvalDto.IsFinalApproval,
                    LoanStatusAfter = loan.Status,
                    MemberName = memberName,
                    ChequeCreated = cheque != null,
                    ChequeVoucherNo = cheque?.Voucherno,
                    ChequeAmount = cheque?.Amount
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_APPROVAL",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = endmain.AmtApproved,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loan.LoanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                endmain.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Blockchain transaction recorded for loan approval: {blockchainTx.TransactionId}");

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN APPROVAL (ENDMAIN)
                // ============================================================

                // Create audit extra data for Endmain
                var endmainAuditExtraData = new
                {
                    loanNo = approvalDto.LoanNo,
                    applicantMemberNo = loan.MemberNo,
                    applicantName = memberName,
                    approvalStatus = approvalDto.ApprovalStatus,
                    approvedAmount = endmain.AmtApproved,
                    appraisedAmount = appraisal.AmtRecommended,
                    amountDifference = endmain.AmtApproved - (appraisal.AmtRecommended ?? 0),
                    approvalComments = approvalDto.ApprovalComments ?? "",
                    rejectionReason = approvalDto.RejectionReason ?? "",
                    minuteNo = endmain.MinuteNo,
                    meetingDate = endmain.MeetingDate,
                    chairSigned = endmain.ChairSigned,
                    secSigned = endmain.SecSigned,
                    isFinalApproval = approvalDto.IsFinalApproval,
                    approvedBy = approvalDto.ApprovedBy,
                    approvedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanStatusChanged = loanStatusChanged,
                    loanAmountBefore = oldLoanAmount,
                    loanAmountAfter = loan.LoanAmt,
                    amountChanged = oldLoanAmount != loan.LoanAmt,
                    chequeCreated = cheque != null,
                    chequeVoucherNo = cheque?.Voucherno,
                    chequeAmount = cheque?.Amount,
                    blockchainTxId = blockchainTx.TransactionId
                };

                // Create a copy of the Endmain object for NewValue
                var endmainForAudit = new
                {
                    endmain.Id,
                    endmain.LoanNo,
                    endmain.MinuteNo,
                    endmain.MeetingDate,
                    endmain.AmtApproved,
                    endmain.Accepted,
                    endmain.ChairSigned,
                    endmain.SecSigned,
                    endmain.MembSigned,
                    endmain.Reasons,
                    endmain.Remarks,
                    ApprovedBy = approvalDto.ApprovedBy,
                    ApprovalDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,  // For Insert, OldValue is null (no previous endmain record)
                    newModel: endmainForAudit,  // This will be serialized to NewValue column
                    tableName: "Endmain",
                    recordId: endmain.Id.ToString(),
                    userId: approvalDto.ApprovedBy,
                    userName: approvalDto.ApprovedBy,
                    companyCode: approvalDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(endmainAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN STATUS CHANGE
                // ============================================================
                var loanAuditExtraData = new
                {
                    loanNo = approvalDto.LoanNo,
                    statusChangedFrom = oldLoanStatus,
                    statusChangedTo = loan.Status,
                    reason = isApproved ? "Loan approved by committee" : "Loan rejected by committee",
                    approvalComments = approvalDto.ApprovalComments ?? "",
                    rejectionReason = approvalDto.RejectionReason ?? "",
                    approvedAmount = endmain.AmtApproved,
                    appraisedAmount = appraisal.AmtRecommended,
                    minuteNo = endmain.MinuteNo,
                    approvedBy = approvalDto.ApprovedBy,
                    approvedDate = DateTime.Now,
                    chequeCreated = cheque != null,
                    chequeVoucherNo = cheque?.Voucherno,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.LoanAmt,
                    loan.Status,
                    loan.Posted,
                    loan.UserName,
                    loan.AuditDateTime,
                    loan.AddSecurity,
                    UpdatedBy = approvalDto.ApprovedBy,
                    UpdateReason = isApproved ? "Loan approved" : "Loan rejected"
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new
                    {
                        Status = oldLoanStatus,
                        Posted = oldLoanPosted,
                        LoanAmt = oldLoanAmount,
                        AddSecurity = oldAddSecurity
                    },
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: approvalDto.LoanNo,
                    userId: approvalDto.ApprovedBy,
                    userName: approvalDto.ApprovedBy,
                    companyCode: approvalDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR CHEQUE (IF CREATED)
                // ============================================================
                if (cheque != null)
                {
                    var chequeAuditExtraData = new
                    {
                        loanNo = approvalDto.LoanNo,
                        applicantMemberNo = loan.MemberNo,
                        applicantName = memberName,
                        chequeId = cheque.Id,
                        voucherNo = cheque.Voucherno,
                        chequeAmount = cheque.Amount,
                        amountInWords = cheque.Amountinword,
                        dateIssued = cheque.DateIssued,
                        status = cheque.Status,
                        paymentMethod = cheque.Paymethod,
                        loanAccount = cheque.LoanAcc,
                        contraAccount = cheque.ContraAcc,
                        premiumAccount = cheque.PremiumAcc,
                        createdBy = approvalDto.ApprovedBy,
                        createdDate = DateTime.Now,
                        blockchainTxId = cheque.BlockchainTxId  
                    };

                    var chequeForAudit = new
                    {
                        cheque.Id,
                        cheque.LoanNo,
                        cheque.MemberNo,
                        cheque.Amount,
                        cheque.AmountIssued,
                        cheque.DateIssued,
                        cheque.Status,
                        cheque.Voucherno,
                        cheque.Voucheramount,
                        cheque.Paymethod,
                        cheque.Amountinword,
                        cheque.LoanAcc,
                        cheque.ContraAcc,
                        cheque.PremiumAcc,
                        CreatedBy = approvalDto.ApprovedBy,
                        CreatedDate = DateTime.Now,
                        BlockchainTxId = cheque.BlockchainTxId  
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: chequeForAudit,
                        tableName: "Cheques",
                        recordId: cheque.Id.ToString(),
                        userId: approvalDto.ApprovedBy,
                        userName: approvalDto.ApprovedBy,
                        companyCode: approvalDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(chequeAuditExtraData),
                        blockchainTxId: cheque.BlockchainTxId  
                    );

                    _logger.LogInformation($"Cheque audit recorded for {cheque.Voucherno}");
                }

                _logger.LogInformation($"Loan approval audit completed for {approvalDto.LoanNo}");

                await transaction.CommitAsync();

                return endmain;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in ApproveLoanAsync for loan {approvalDto.LoanNo}");
                throw;
            }
        }

        public async Task<List<Endmain>> GetLoanApprovalsAsync(string loanNo)
        {
            var approvals = await _context.Endmain
                .Where(a => a.LoanNo == loanNo)
                .OrderByDescending(a => a.MeetingDate)
                .ToListAsync();

            return approvals;
        }

        public async Task<bool> IsLoanApprovedAsync(string loanNo)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            return loan != null && loan.Status == (int)Status.Approved;
        }

        #endregion


        #region Loan Endorsement/Deduction

        public async Task<Endmain> CreateEndorsementAsync(LoanEndorsementDTO endorsementDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Creating endorsement for loan {endorsementDto.LoanNo}");
                _logger.LogInformation($"IsAccepted: {endorsementDto.IsAccepted}");

                // Get loan data
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == endorsementDto.LoanNo && l.CompanyCode == endorsementDto.CompanyCode);

                if (loan == null)
                {
                    throw new InvalidOperationException($"Loan {endorsementDto.LoanNo} not found");
                }

                // Store old loan values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                decimal oldLoanAamount = loan.Aamount ?? 0;

                // Get member details for audit
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == endorsementDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;

                // Check existing endorsement
                var existingEndmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == endorsementDto.LoanNo && e.CompanyCode == endorsementDto.CompanyCode);

                if (existingEndmain != null)
                {
                    throw new InvalidOperationException($"Endorsement already exists for loan {endorsementDto.LoanNo}");
                }

                // Get the approved amount from Appraisal
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == endorsementDto.LoanNo);
                decimal approvedAmount = appraisal?.AmtRecommended ?? loan.LoanAmt ?? 0;

                var appraisalReason = appraisal?.Reason ?? "APPROVED";

                var minuteNo = await GenerateMinuteNumberAsync(endorsementDto.CompanyCode);
                var auditTransactionNo = Guid.NewGuid().ToString().Substring(0, 15);

                decimal totalDeductions = 0;
                decimal netAmount = approvedAmount;
                string voucherNo = null;
                string chequeNo = null;
                string transactionNo = null;
                Cheque? cheque = null;
                List<Gltransaction> glTransactions = new List<Gltransaction>();
                string loanAcc = null;

                // ============================================================
                // CHECK FOR UPFRONT INTEREST
                // ============================================================
                bool isInterestUpfront = loan.InterestUpront ?? false;
                decimal upfrontInterestAmount = 0;
                decimal monthlyPayment = 0;
                decimal totalRepayable = 0;

                // Get LoanType to retrieve LoanAcc and InterestAcc
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == endorsementDto.CompanyCode);

                // Get GL accounts from Loantype (same as repayment)
                string loanReceivableAccount = loanType?.LoanAcc ?? "LOAN_RECEIVABLE_ACCOUNT";
                string interestIncomeAccount = loanType?.InterestAcc ?? "INTEREST_INCOME_ACCOUNT";

                if (isInterestUpfront && endorsementDto.IsAccepted)
                {
                    // Calculate upfront interest
                    var interestResult = CalculateUpfrontInterest(
                        approvedAmount,
                        loan.Interest ?? 0,
                        loan.RepayPeriod ?? 12,
                        loan.RepayMethod ?? "AMT"
                    );
                    upfrontInterestAmount = interestResult.TotalInterest;
                    monthlyPayment = interestResult.MonthlyPayment;
                    totalRepayable = interestResult.TotalRepayable;

                    _logger.LogInformation($"Upfront interest calculated: {upfrontInterestAmount:C} for loan {endorsementDto.LoanNo}");
                    _logger.LogInformation($"Interest Rate: {loan.Interest}%, Period: {loan.RepayPeriod} months, Method: {loan.RepayMethod}");

                    // Check if upfront interest deduction already exists in the DTO
                    var existingInterestDeduction = endorsementDto.Deductions
                        .FirstOrDefault(d => d.DeductionCode == "UPFRONT_INTEREST");

                    if (existingInterestDeduction == null)
                    {
                        // Add upfront interest as a mandatory deduction
                        endorsementDto.Deductions.Add(new LoanDeductionDTO
                        {
                            DeductionCode = "UPFRONT_INTEREST",
                            DeductionName = "Upfront Interest",
                            GlAccountNo = interestIncomeAccount, // Use InterestAcc from LoanType
                            GlAccountName = "",
                            Amount = upfrontInterestAmount,
                            Description = $"Upfront interest for {loan.RepayPeriod} months at {loan.Interest}% p.a.",
                            IsMandatory = true,
                            IsPercentage = false,
                            PercentageValue = 0
                        });
                    }
                    else
                    {
                        // Update existing interest deduction
                        existingInterestDeduction.Amount = upfrontInterestAmount;
                        existingInterestDeduction.GlAccountNo = interestIncomeAccount;
                        existingInterestDeduction.Description = $"Upfront interest for {loan.RepayPeriod} months at {loan.Interest}% p.a.";
                    }
                }

                // ============================================================
                // ONLY PROCESS DEDUCTIONS AND GL TRANSACTIONS IF ACCEPTED
                // ============================================================
                if (endorsementDto.IsAccepted)
                {
                    loanAcc = loanType?.LoanAcc ?? "LOAN_ASSET_ACCOUNT";

                    if (loan.Status != (int)Status.Approved)
                    {
                        throw new InvalidOperationException($"Cannot create endorsement for loan in status '{loan.Status}'. Loan must be Approved.");
                    }

                    if (string.IsNullOrEmpty(endorsementDto.SourceAccountNo))
                    {
                        throw new InvalidOperationException("Please select a Source Bank/Account for disbursement.");
                    }

                    // Calculate percentage-based deductions
                    foreach (var deduction in endorsementDto.Deductions)
                    {
                        if (deduction.IsPercentage && deduction.PercentageValue.HasValue && deduction.PercentageValue.Value > 0)
                        {
                            deduction.Amount = (approvedAmount * deduction.PercentageValue.Value) / 100;
                            _logger.LogInformation($"Calculated {deduction.DeductionName}: {deduction.PercentageValue}% of {approvedAmount:C} = {deduction.Amount:C}");
                        }
                    }

                    var validDeductions = endorsementDto.Deductions
                        .Where(d => d.Amount > 0 && !string.IsNullOrEmpty(d.GlAccountNo))
                        .ToList();

                    totalDeductions = validDeductions.Sum(d => d.Amount);
                    netAmount = approvedAmount - totalDeductions;

                    if (netAmount < 0)
                    {
                        throw new InvalidOperationException($"Total deductions ({totalDeductions:C}) cannot exceed approved amount ({approvedAmount:C})");
                    }

                    voucherNo = await GenerateVoucherNumberAsync(endorsementDto.CompanyCode);
                    chequeNo = await GenerateChequeNumberAsync(endorsementDto.CompanyCode);
                    transactionNo = $"EDM{DateTime.Now:ddMMyyyyHHmmss}";

                    // Get PremiumAcc
                    var premiumDeduction = validDeductions.FirstOrDefault(d => d.DeductionCode == "INSURANCE" || d.DeductionName.Contains("Insurance"));
                    var premiumAcc = premiumDeduction?.GlAccountNo ?? null;

                    // CREATE CHEQUE RECORD
                    cheque = new Cheque
                    {
                        LoanNo = endorsementDto.LoanNo,
                        MemberNo = loan.MemberNo,
                        CompanyCode = endorsementDto.CompanyCode,
                        Amount = approvedAmount,
                        AmountIssued = netAmount,
                        ChequeNo = chequeNo,
                        Voucherno = voucherNo,
                        Voucheramount = netAmount,
                        DateIssued = endorsementDto.EndorsementDate,
                        Status = "Pending",
                        AuditId = endorsementDto.EndorsedBy ?? "SYSTEM",
                        AuditTime = DateTime.Now,
                        Remarks = endorsementDto.Remarks ?? "",
                        Firstdate = endorsementDto.EndorsementDate,
                        Balance = netAmount,
                        TransactionNo = transactionNo,
                        OrgAmt = approvedAmount,
                        LoanAcc = loanAcc,
                        ContraAcc = endorsementDto.SourceAccountNo,
                        PremiumAcc = premiumAcc ?? "PREMIUM_ACCOUNT",
                        UserName = endorsementDto.EndorsedBy ?? "SYSTEM",
                        AuditDateTime = DateTime.Now,
                        BalForward = 0,
                        ProcessingFee = validDeductions.FirstOrDefault(d => d.DeductionCode == "PROC_FEE")?.Amount ?? 0,
                        IntAmount = isInterestUpfront ? upfrontInterestAmount : (validDeductions.FirstOrDefault(d => d.DeductionCode == "INTEREST")?.Amount ?? 0),
                        IntrOwed = 0,
                        CollectorId = null,
                        CollectorName = null,
                        ClerkStaffNo = null,
                        ClerkName = null,
                        Reasons = appraisalReason,
                        Premium = 0,
                        Offsetamount = 0,
                        Amountinword = NumberToWords(netAmount),
                        Refloan = true,
                        Paymethod = "",
                        Dregard = 0,
                        PaidBf = 0,
                        ApiKey = null,
                        SerialNo = null
                    };

                    _context.Cheques.Add(cheque);
                    await _context.SaveChangesAsync();

                    // ============================================================
                    // RECORD GL TRANSACTIONS FOR EACH DEDUCTION
                    // ============================================================
                    foreach (var deduction in validDeductions)
                    {
                        bool isUpfrontInterest = deduction.DeductionCode == "UPFRONT_INTEREST";

                        var glTransaction = new Gltransaction
                        {
                            TransDate = DateTime.Now,
                            Amount = deduction.Amount,
                            DrAccNo = deduction.GlAccountNo,
                            CrAccNo = endorsementDto.SourceAccountNo,
                            Temp = isUpfrontInterest ? "UPFRONT_INTEREST" : "ENDORSEMENT",
                            DocumentNo = voucherNo,
                            Source = isUpfrontInterest ? "LOAN_ENDORSEMENT_INTEREST" : "LOAN_ENDORSEMENT",
                            CompanyCode = endorsementDto.CompanyCode,
                            TransDescript = isUpfrontInterest
                                ? $"Upfront Interest for Loan {endorsementDto.LoanNo} - {loan.Interest}% p.a. on {approvedAmount:C} for {loan.RepayPeriod} months"
                                : $"{deduction.DeductionName} for Loan {endorsementDto.LoanNo}",
                            AuditTime = DateTime.Now,
                            AuditId = endorsementDto.EndorsedBy ?? "SYSTEM",
                            Cash = 0,
                            DocPosted = 1,
                            ChequeNo = chequeNo,
                            Dregard = false,
                            Recon = false,
                            TransactionNo = transactionNo,
                            Module = "LOAN",
                            ReconId = 0,
                            AuditDateTime = DateTime.Now
                        };

                        _context.Gltransactions.Add(glTransaction);
                        glTransactions.Add(glTransaction);
                    }

                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // CREATE ENDMAIN RECORD
                // ============================================================
                var endmain = new Endmain
                {
                    LoanNo = endorsementDto.LoanNo,
                    CompanyCode = endorsementDto.CompanyCode,
                    MinuteNo = minuteNo,
                    MeetingDate = endorsementDto.EndorsementDate,
                    AmtApproved = approvedAmount,
                    Accepted = endorsementDto.IsAccepted ? "1" : "0",
                    ChairSigned = endorsementDto.IsAccepted ? endorsementDto.EndorsedBy : null,
                    SecSigned = endorsementDto.IsAccepted ? endorsementDto.EndorsedBy : null,
                    MembSigned = endorsementDto.IsAccepted ? loan.MemberNo : null,
                    Reasons = appraisalReason,
                    Remarks = endorsementDto.IsAccepted
                        ? $"Total deductions: {totalDeductions:C}. Net amount: {netAmount:C}" +
                          (isInterestUpfront ? $" (Upfront Interest: {upfrontInterestAmount:C})" : "")
                        : $"REJECTED: {endorsementDto.Remarks}",
                    AuditId = endorsementDto.EndorsedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    TransactionNo = auditTransactionNo
                };

                _context.Endmain.Add(endmain);
                await _context.SaveChangesAsync();

                // ============================================================
                // UPDATE LOAN STATUS
                // ============================================================
                if (endorsementDto.IsAccepted)
                {
                    loan.Status = (int)Status.Endorsed;
                    loan.Posted = "Endorsed";
                    loan.Aamount = netAmount;
                    _logger.LogInformation($"Loan {loan.LoanNo} status updated from {oldLoanStatus} to Endorsed");
                }
                else
                {
                    loan.Status = (int)Status.Rejected;
                    loan.Posted = "Rejected";
                    loan.AddSecurity = $"Endorsement rejected: {endorsementDto.Remarks ?? "No reason provided"}";
                    _logger.LogInformation($"Loan {loan.LoanNo} status updated from {oldLoanStatus} to Rejected");
                }

                loan.UserName = endorsementDto.EndorsedBy ?? "SYSTEM";
                loan.AuditDateTime = DateTime.Now;

                _context.Loans.Update(loan);
                await _context.SaveChangesAsync();

                string? blockchainTxId = null;

                // ============================================================
                // RECORD BLOCKCHAIN TRANSACTION
                // ============================================================
                try
                {
                    var blockchainData = new
                    {
                        EndmainId = endmain.Id,
                        LoanNo = endorsementDto.LoanNo,
                        MemberNo = loan.MemberNo,
                        MemberName = memberName,
                        IsAccepted = endorsementDto.IsAccepted,
                        ApprovedAmount = approvedAmount,
                        UpfrontInterest = isInterestUpfront ? upfrontInterestAmount : 0,
                        TotalDeductions = totalDeductions,
                        NetAmount = netAmount,
                        RejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                        VoucherNo = voucherNo,
                        ChequeNo = chequeNo,
                        SourceAccount = endorsementDto.SourceAccountNo,
                        Remarks = endorsementDto.Remarks,
                        LoanStatusAfter = endorsementDto.IsAccepted ? (int)Status.Endorsed : (int)Status.Rejected,
                        EndorsedBy = endorsementDto.EndorsedBy,
                        EndorsementDate = endorsementDto.EndorsementDate
                    };

                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = endorsementDto.IsAccepted ? "LOAN_ENDORSEMENT" : "LOAN_ENDORSEMENT_REJECTED",
                        MemberNo = loan.MemberNo,
                        CompanyCode = loan.CompanyCode,
                        Amount = netAmount,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                        OffChainReferenceId = endmain.MinuteNo,
                        Status = "CONFIRMED",
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    await _context.SaveChangesAsync();

                    blockchainTxId = blockchainTx.TransactionId;

                    endmain.BlockchainTxId = blockchainTx.TransactionId;
                    if (cheque != null) cheque.BlockchainTxId = blockchainTx.TransactionId;
                    foreach (var glTxn in glTransactions)
                    {
                        glTxn.BlockchainTxId = blockchainTx.TransactionId;
                    }
                    loan.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to record blockchain transaction for endorsement");
                }

                // ============================================================
                // SAVE AUDIT TRAIL FOR ENDORSEMENT (ENDMAIN)
                // ============================================================
                var endmainAuditExtraData = new
                {
                    loanNo = endorsementDto.LoanNo,
                    applicantMemberNo = loan.MemberNo,
                    applicantName = memberName,
                    minuteNo = minuteNo,
                    meetingDate = endorsementDto.EndorsementDate,
                    isAccepted = endorsementDto.IsAccepted,
                    approvedAmount = approvedAmount,
                    totalDeductions = totalDeductions,
                    netAmount = netAmount,
                    rejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                    voucherNo = voucherNo,
                    chequeNo = chequeNo,
                    sourceAccountNo = endorsementDto.SourceAccountNo,
                    appraisalReason = appraisalReason,
                    remarks = endorsementDto.Remarks ?? "",
                    endorsedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    endorsedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanAmountBefore = oldLoanAamount,
                    loanAmountAfter = loan.Aamount,
                    blockchainTxId = blockchainTxId
                };

                var endmainForAudit = new
                {
                    endmain.Id,
                    endmain.LoanNo,
                    endmain.MinuteNo,
                    endmain.MeetingDate,
                    endmain.AmtApproved,
                    endmain.Accepted,
                    endmain.Reasons,
                    endmain.Remarks,
                    endmain.TransactionNo,
                    IsAccepted = endorsementDto.IsAccepted,
                    EndorsedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    EndorsementDate = endorsementDto.EndorsementDate,
                    BlockchainTxId = blockchainTxId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: endmainForAudit,
                    tableName: "Endmain",
                    recordId: endmain.Id.ToString(),
                    userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                    userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                    companyCode: endorsementDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(endmainAuditExtraData),
                    blockchainTxId: blockchainTxId
                );

                // Only save cheque audit if accepted
                if (endorsementDto.IsAccepted && cheque != null)
                {
                    var chequeAuditExtraData = new
                    {
                        loanNo = endorsementDto.LoanNo,
                        applicantMemberNo = loan.MemberNo,
                        applicantName = memberName,
                        chequeId = cheque.Id,
                        chequeNo = chequeNo,
                        voucherNo = voucherNo,
                        amount = cheque.Amount,
                        amountIssued = cheque.AmountIssued,
                        netAmount = netAmount,
                        totalDeductions = totalDeductions,
                        dateIssued = cheque.DateIssued,
                        status = cheque.Status,
                        sourceAccount = endorsementDto.SourceAccountNo,
                        loanAccount = loanAcc,
                        premiumAccount = cheque.PremiumAcc,
                        processingFee = cheque.ProcessingFee,
                        intamount = cheque.IntAmount,
                        interestAmount = cheque.IntAmount,
                        amountInWords = cheque.Amountinword,
                        createdBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        createdDate = DateTime.Now,
                        blockchainTxId = blockchainTxId
                    };

                    var chequeForAudit = new
                    {
                        cheque.Id,
                        cheque.LoanNo,
                        cheque.MemberNo,
                        cheque.Amount,
                        cheque.AmountIssued,
                        cheque.ChequeNo,
                        cheque.Voucherno,
                        cheque.Voucheramount,
                        cheque.DateIssued,
                        cheque.Status,
                        cheque.LoanAcc,
                        cheque.ContraAcc,
                        cheque.PremiumAcc,
                        cheque.ProcessingFee,
                        cheque.IntAmount,
                        cheque.Remarks,
                        cheque.Amountinword,
                        CreatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        CreatedDate = DateTime.Now,
                        BlockchainTxId = blockchainTxId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: chequeForAudit,
                        tableName: "Cheques",
                        recordId: cheque.Id.ToString(),
                        userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                        userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                        companyCode: endorsementDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(chequeAuditExtraData),
                        blockchainTxId: blockchainTxId
                    );
                }

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN STATUS CHANGE
                // ============================================================
                var loanAuditExtraData = new
                {
                    loanNo = endorsementDto.LoanNo,
                    statusChangedFrom = oldLoanStatus,
                    statusChangedTo = loan.Status,
                    reason = endorsementDto.IsAccepted ? "Loan endorsed and ready for disbursement" : "Loan endorsement rejected",
                    approvedAmount = approvedAmount,
                    totalDeductions = totalDeductions,
                    netDisbursedAmount = netAmount,
                    rejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                    voucherNo = voucherNo,
                    chequeNo = chequeNo,
                    sourceAccount = endorsementDto.SourceAccountNo,
                    minuteNo = minuteNo,
                    endorsedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    endorsedDate = DateTime.Now,
                    blockchainTxId = blockchainTxId
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.LoanAmt,
                    loan.Aamount,
                    loan.Status,
                    loan.Posted,
                    loan.UserName,
                    loan.AuditDateTime,
                    UpdatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    UpdateReason = endorsementDto.IsAccepted ? "Loan endorsed - ready for disbursement" : "Loan endorsement rejected",
                    NetDisbursedAmount = netAmount,
                    IsAccepted = endorsementDto.IsAccepted,
                    RejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new
                    {
                        Status = oldLoanStatus,
                        Posted = oldLoanPosted,
                        Aamount = oldLoanAamount
                    },
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: endorsementDto.LoanNo,
                    userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                    userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                    companyCode: endorsementDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                    blockchainTxId: blockchainTxId
                );

                // Only save GL audit if accepted and transactions exist
                if (endorsementDto.IsAccepted && glTransactions.Any())
                {
                    var glAuditExtraData = new
                    {
                        loanNo = endorsementDto.LoanNo,
                        voucherNo = voucherNo,
                        chequeNo = chequeNo,
                        transactionNo = transactionNo,
                        totalTransactions = glTransactions.Count,
                        createdBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        createdDate = DateTime.Now,
                        blockchainTxId = blockchainTxId
                    };

                    var glForAudit = new
                    {
                        LoanNo = endorsementDto.LoanNo,
                        VoucherNo = voucherNo,
                        ChequeNo = chequeNo,
                        TransactionNo = transactionNo,
                        CreatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        CreatedDate = DateTime.Now,
                        BlockchainTxId = blockchainTxId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: glForAudit,
                        tableName: "Gltransactions",
                        recordId: voucherNo,
                        userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                        userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                        companyCode: endorsementDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(glAuditExtraData),
                        blockchainTxId: blockchainTxId
                    );
                }

                _logger.LogInformation($"Endorsement audit completed for loan {endorsementDto.LoanNo}");

                await transaction.CommitAsync();

                if (endorsementDto.IsAccepted)
                {
                    _logger.LogInformation($"Endorsement created successfully for loan {endorsementDto.LoanNo}. " +
                        $"Approved Amount: {approvedAmount:C}, Total Deductions: {totalDeductions:C}, Net Amount: {netAmount:C}" +
                        (isInterestUpfront ? $", Upfront Interest: {upfrontInterestAmount:C}" : ""));
                }
                else
                {
                    _logger.LogInformation($"Endorsement REJECTED for loan {endorsementDto.LoanNo}. Reason: {endorsementDto.Remarks}");
                }

                return endmain;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating endorsement for loan {endorsementDto.LoanNo}");
                throw;
            }
        }

        /// <summary>
        /// Calculates total interest for a loan based on principal, rate, period and repayment method
        /// </summary>
        private (decimal TotalInterest, decimal MonthlyPayment, decimal TotalRepayable) CalculateUpfrontInterest(
            decimal principalAmount, decimal annualInterestRate, int repaymentPeriod,string repayMethod)
        {
            decimal monthlyInterestRate = (annualInterestRate / 100) / 12;
            decimal totalInterest = 0;
            decimal monthlyPayment = 0;
            decimal totalRepayable = 0;

            if (repayMethod == "AMT")
            {
                if (monthlyInterestRate > 0 && repaymentPeriod > 0)
                {
                    decimal factor = (decimal)Math.Pow((double)(1 + monthlyInterestRate), repaymentPeriod);
                    monthlyPayment = principalAmount * monthlyInterestRate * factor / (factor - 1);
                    totalInterest = (monthlyPayment * repaymentPeriod) - principalAmount;
                }
                else
                {
                    monthlyPayment = principalAmount / (repaymentPeriod > 0 ? repaymentPeriod : 1);
                    totalInterest = 0;
                }
                totalRepayable = principalAmount + totalInterest;
            }
            else if (repayMethod == "STL")
            {
                // Simple interest: P * R * T
                totalInterest = principalAmount * (annualInterestRate / 100) * (repaymentPeriod / 12m);
                monthlyPayment = repaymentPeriod > 0 ? (principalAmount + totalInterest) / repaymentPeriod : principalAmount;
                totalRepayable = principalAmount + totalInterest;
            }
            else if (repayMethod == "RBAL")
            {
                // Reducing balance interest
                decimal remainingBalance = principalAmount;
                decimal totalMinimumInterest = 0;

                if (monthlyInterestRate > 0)
                {
                    for (int i = 1; i <= repaymentPeriod; i++)
                    {
                        decimal interestForMonth = remainingBalance * monthlyInterestRate;
                        totalMinimumInterest += interestForMonth;
                        // For RBAL, principal is not reduced in the schedule (interest only minimum)
                        // So remainingBalance stays the same
                    }
                }

                totalInterest = totalMinimumInterest;
                monthlyPayment = repaymentPeriod > 0 ? totalInterest / repaymentPeriod : 0;
                totalRepayable = principalAmount + totalInterest;
            }

            return (totalInterest, monthlyPayment, totalRepayable);
        }


        public async Task<LoanEndorsementDTO> GetEndorsementForEditAsync(string loanNo, string companyCode)
        {
            try
            {
                // Get the loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {loanNo} not found");

                // Get endorsement (Endmain)
                var endmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                if (endmain == null)
                    throw new InvalidOperationException($"Endorsement not found for loan {loanNo}");

                // Get cheque
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                if (cheque == null)
                    throw new InvalidOperationException($"Cheque not found for loan {loanNo}");

                // Get GL transactions for deductions
                var glTransactions = await _context.Gltransactions
                    .Where(g => g.DocumentNo == cheque.Voucherno && g.Source == "LOAN_ENDORSEMENT")
                    .ToListAsync();

                // ============================================================
                // CHECK FOR UPFRONT INTEREST
                // ============================================================
                bool isUpfrontInterest = loan.InterestUpront ?? false;
                decimal upfrontInterestAmount = 0;
                string interestIncomeAccount = null;

                // Get loan type for interest account
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                if (isUpfrontInterest)
                {
                    // Calculate upfront interest from the loan data
                    var interestResult = CalculateUpfrontInterest(
                        loan.LoanAmt ?? 0,
                        loan.Interest ?? 0,
                        loan.RepayPeriod ?? 12,
                        loan.RepayMethod ?? "AMT"
                    );
                    upfrontInterestAmount = interestResult.TotalInterest;
                    interestIncomeAccount = loanType?.InterestAcc ?? "INTEREST_INCOME_ACCOUNT";

                    _logger.LogInformation($"Upfront interest calculated for edit: {upfrontInterestAmount:C} for loan {loanNo}");
                }

                // Build deductions list from GL transactions
                var deductions = new List<LoanDeductionDTO>();

                // ============================================================
                // ADD UPFRONT INTEREST AS FIRST DEDUCTION (if enabled)
                // ============================================================
                if (isUpfrontInterest && upfrontInterestAmount > 0)
                {
                    // Check if upfront interest already exists in GL transactions
                    var existingUpfrontInterest = glTransactions
                        .FirstOrDefault(g => g.Temp == "UPFRONT_INTEREST" ||
                                            (g.TransDescript != null && g.TransDescript.Contains("Upfront Interest")) ||
                                            (g.TransDescript != null && g.TransDescript.Contains("UPFRONT_INTEREST")));

                    if (existingUpfrontInterest != null)
                    {
                        // Use existing GL transaction data
                        deductions.Add(new LoanDeductionDTO
                        {
                            DeductionCode = "UPFRONT_INTEREST",
                            DeductionName = "Upfront Interest",
                            GlAccountNo = existingUpfrontInterest.DrAccNo,
                            GlAccountName = existingUpfrontInterest.DrAccNo,
                            Amount = existingUpfrontInterest.Amount,
                            Description = existingUpfrontInterest.TransDescript ?? $"Upfront interest for {loan.RepayPeriod} months",
                            IsMandatory = true,
                            IsPercentage = false,
                            PercentageValue = 0
                        });
                    }
                    else
                    {
                        // Add it as a new deduction (for backward compatibility)
                        deductions.Add(new LoanDeductionDTO
                        {
                            DeductionCode = "UPFRONT_INTEREST",
                            DeductionName = "Upfront Interest",
                            GlAccountNo = interestIncomeAccount,
                            GlAccountName = interestIncomeAccount,
                            Amount = upfrontInterestAmount,
                            Description = $"Upfront interest for {loan.RepayPeriod ?? 12} months at {loan.Interest ?? 0}% p.a.",
                            IsMandatory = true,
                            IsPercentage = false,
                            PercentageValue = 0
                        });
                    }
                }

                // Add other GL transactions as deductions (skip upfront interest if already added)
                foreach (var gl in glTransactions)
                {
                    // Skip if this is upfront interest and we already added it
                    if (gl.Temp == "UPFRONT_INTEREST" ||
                        (gl.TransDescript != null && gl.TransDescript.Contains("Upfront Interest")) ||
                        (gl.TransDescript != null && gl.TransDescript.Contains("UPFRONT_INTEREST")))
                    {
                        continue;
                    }

                    // Skip net disbursement GL
                    if (gl.Source == "LOAN_DISBURSEMENT")
                        continue;

                    string deductionName = "Deduction";
                    string deductionCode = "UNKNOWN";
                    decimal percentageValue = 0;
                    bool isPercentage = false;

                    var descParts = gl.TransDescript?.Split(' ') ?? new string[0];
                    if (descParts.Length > 0)
                    {
                        deductionName = descParts[0];
                        deductionCode = deductionName.Replace(" ", "_").ToUpper();
                    }

                    if (gl.TransDescript != null && gl.TransDescript.Contains("%"))
                    {
                        isPercentage = true;
                        var percentMatch = System.Text.RegularExpressions.Regex.Match(gl.TransDescript, @"(\d+\.?\d*)%");
                        if (percentMatch.Success && decimal.TryParse(percentMatch.Groups[1].Value, out decimal pct))
                        {
                            percentageValue = pct;
                        }
                    }

                    // Handle registration fee
                    if (deductionCode == "REG_FEE")
                    {
                        deductionName = "Registration Fee";
                        deductionCode = "REG_FEE";
                    }

                    deductions.Add(new LoanDeductionDTO
                    {
                        DeductionCode = deductionCode,
                        DeductionName = deductionName,
                        GlAccountNo = gl.DrAccNo,
                        GlAccountName = gl.DrAccNo,
                        Amount = gl.Amount,
                        Description = gl.TransDescript,
                        IsPercentage = isPercentage,
                        PercentageValue = percentageValue,
                        IsMandatory = deductionCode == "UPFRONT_INTEREST" || deductionCode == "REG_FEE"
                    });
                }

                var grossAmount = loan.LoanAmt ?? 0;

                return new LoanEndorsementDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    EndorsementDate = endmain.MeetingDate ?? DateTime.Now,
                    EndorsedBy = endmain.AuditId,
                    Remarks = endmain.Remarks,
                    SourceAccountNo = cheque.ContraAcc,
                    IsAccepted = endmain.Accepted == "1",
                    TotalDeductions = deductions.Sum(d => d.Amount),
                    GrossAmount = grossAmount,
                    NetDisbursementAmount = grossAmount - deductions.Sum(d => d.Amount),
                    Deductions = deductions,
                    EndmainId = endmain.Id,
                    VoucherNo = cheque.Voucherno,
                    ChequeNo = cheque.ChequeNo,
                    MinuteNo = endmain.MinuteNo,
                    IsEndorsed = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting endorsement for edit: {loanNo}");
                throw;
            }
        }

        public async Task<Endmain> UpdateEndorsementAsync(LoanEndorsementDTO endorsementDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Updating endorsement for loan {endorsementDto.LoanNo}");
                _logger.LogInformation($"IsAccepted: {endorsementDto.IsAccepted}");
                _logger.LogInformation($"GrossAmount: {endorsementDto.GrossAmount}");

                // Get existing endorsement
                var endmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == endorsementDto.LoanNo && e.CompanyCode == endorsementDto.CompanyCode);

                if (endmain == null)
                    throw new InvalidOperationException($"Endorsement not found for loan {endorsementDto.LoanNo}");

                // Get existing cheque
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == endorsementDto.LoanNo && c.CompanyCode == endorsementDto.CompanyCode);

                if (cheque == null)
                    throw new InvalidOperationException($"Cheque not found for loan {endorsementDto.LoanNo}");

                // Get loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == endorsementDto.LoanNo && l.CompanyCode == endorsementDto.CompanyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {endorsementDto.LoanNo} not found");

                // Store old values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                decimal oldLoanAamount = loan.Aamount ?? 0;
                decimal oldEndmainAmtApproved = endmain.AmtApproved;
                string oldEndmainAccepted = endmain.Accepted;
                string oldEndmainRemarks = endmain.Remarks;
                decimal? oldChequeAmountIssued = cheque.AmountIssued;
                decimal? oldChequeBalance = cheque.Balance;
                decimal? oldChequeIntAmount = cheque.IntAmount;
                decimal? oldChequeProcessingFee = cheque.ProcessingFee;
                decimal? oldChequePremium = cheque.Premium;

                // Get member details for audit
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == endorsementDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;

                // Get the approved amount from DTO or loan
                var grossAmount = endorsementDto.GrossAmount > 0 ? endorsementDto.GrossAmount : (loan.LoanAmt ?? 0);

                _logger.LogInformation($"Using GrossAmount: {grossAmount}");

                // Get LoanType for LoanAcc
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == endorsementDto.CompanyCode);
                string loanAcc = loanType?.LoanAcc ?? "LOAN_ASSET_ACCOUNT";

                // Get existing GL transactions (for audit and deletion)
                var oldGLTransactions = await _context.Gltransactions
                    .Where(g => g.DocumentNo == cheque.Voucherno && (g.Source == "LOAN_ENDORSEMENT" || g.Source == "LOAN_DISBURSEMENT"))
                    .ToListAsync();

                decimal oldTotalDeductions = oldGLTransactions
                    .Where(g => g.Source == "LOAN_ENDORSEMENT")
                    .Sum(g => g.Amount);

                // ============================================================
                // DECLARE validDeductions OUTSIDE THE IF BLOCK
                // ============================================================
                List<LoanDeductionDTO> validDeductions = new List<LoanDeductionDTO>();

                // ============================================================
                // CALCULATE NEW TOTALS
                // ============================================================
                decimal totalDeductions = 0;
                decimal netAmount = grossAmount;
                string transactionNo = $"EDM{DateTime.Now:ddMMyyyyHHmmss}";
                string voucherNo = cheque.Voucherno;
                List<Gltransaction> newGlTransactions = new List<Gltransaction>();

                if (endorsementDto.IsAccepted)
                {
                    // Calculate percentage-based deductions
                    foreach (var deduction in endorsementDto.Deductions)
                    {
                        if (deduction.IsPercentage && deduction.PercentageValue.HasValue && deduction.PercentageValue.Value > 0)
                        {
                            deduction.Amount = (grossAmount * deduction.PercentageValue.Value) / 100;
                            _logger.LogInformation($"Calculated {deduction.DeductionName}: {deduction.PercentageValue}% of {grossAmount:C} = {deduction.Amount:C}");
                        }
                    }

                    // ============================================================
                    // ASSIGN validDeductions HERE
                    // ============================================================
                    validDeductions = endorsementDto.Deductions
                        .Where(d => d.Amount > 0 && !string.IsNullOrEmpty(d.GlAccountNo))
                        .ToList();

                    totalDeductions = validDeductions.Sum(d => d.Amount);
                    netAmount = grossAmount - totalDeductions;

                    if (netAmount < 0)
                    {
                        throw new InvalidOperationException($"Total deductions ({totalDeductions:C}) cannot exceed gross amount ({grossAmount:C})");
                    }

                    if (string.IsNullOrEmpty(endorsementDto.SourceAccountNo))
                    {
                        throw new InvalidOperationException("Please select a Source Bank/Account for disbursement.");
                    }

                    // ============================================================
                    // DELETE OLD GL TRANSACTIONS
                    // ============================================================
                    if (oldGLTransactions.Any())
                    {
                        _logger.LogInformation($"Deleting {oldGLTransactions.Count} old GL transactions");
                        _context.Gltransactions.RemoveRange(oldGLTransactions);
                    }

                    // ============================================================
                    // CREATE NEW GL TRANSACTIONS FOR EACH DEDUCTION
                    // ============================================================
                    foreach (var deduction in validDeductions)
                    {
                        bool isUpfrontInterest = deduction.DeductionCode == "UPFRONT_INTEREST";

                        var glTransaction = new Gltransaction
                        {
                            TransDate = DateTime.Now,
                            Amount = deduction.Amount,
                            DrAccNo = deduction.GlAccountNo,
                            CrAccNo = endorsementDto.SourceAccountNo,
                            Temp = isUpfrontInterest ? "UPFRONT_INTEREST" : "ENDORSEMENT",
                            DocumentNo = voucherNo,
                            Source = isUpfrontInterest ? "LOAN_ENDORSEMENT_INTEREST" : "LOAN_ENDORSEMENT",
                            CompanyCode = endorsementDto.CompanyCode,
                            TransDescript = isUpfrontInterest
                                ? $"Upfront Interest for Loan {endorsementDto.LoanNo} - {loan.Interest}% p.a. on {grossAmount:C} for {loan.RepayPeriod} months"
                                : $"{deduction.DeductionName} - {(deduction.IsPercentage ? $"{deduction.PercentageValue}% of {grossAmount:C}" : "Fixed")}",
                            AuditTime = DateTime.Now,
                            AuditId = endorsementDto.EndorsedBy ?? "SYSTEM",
                            Cash = 0,
                            DocPosted = 1,
                            ChequeNo = cheque.ChequeNo,
                            Dregard = false,
                            Recon = false,
                            TransactionNo = transactionNo,
                            Module = "LOAN",
                            ReconId = 0,
                            AuditDateTime = DateTime.Now
                        };

                        _context.Gltransactions.Add(glTransaction);
                        newGlTransactions.Add(glTransaction);
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    // If rejected, delete all old GL transactions
                    if (oldGLTransactions.Any())
                    {
                        _logger.LogInformation($"Deleting {oldGLTransactions.Count} old GL transactions (rejected)");
                        _context.Gltransactions.RemoveRange(oldGLTransactions);
                        await _context.SaveChangesAsync();
                    }

                    // No deductions or GL transactions for rejected
                    totalDeductions = 0;
                    netAmount = 0;
                    validDeductions = new List<LoanDeductionDTO>(); // Empty list for rejected
                }

                // ============================================================
                // UPDATE ENDMAIN
                // ============================================================
                endmain.AmtApproved = grossAmount;
                endmain.Remarks = endorsementDto.Remarks;
                endmain.Accepted = endorsementDto.IsAccepted ? "1" : "0";
                endmain.AuditTime = DateTime.Now;
                endmain.AuditId = endorsementDto.EndorsedBy ?? "SYSTEM";

                // ============================================================
                // UPDATE CHEQUE - USING validDeductions (NOW ACCESSIBLE)
                // ============================================================
                cheque.AmountIssued = netAmount;
                cheque.Voucheramount = netAmount;
                cheque.Balance = netAmount;
                cheque.Remarks = endorsementDto.Remarks;
                cheque.UserName = endorsementDto.EndorsedBy ?? "SYSTEM";
                cheque.AuditDateTime = DateTime.Now;
                cheque.ContraAcc = endorsementDto.SourceAccountNo;
                cheque.Amount = grossAmount;
                cheque.OrgAmt = grossAmount;

                // ============================================================
                // UPDATE INTEREST AMOUNT - FROM validDeductions
                // ============================================================
                var interestDeduction = validDeductions
                    .FirstOrDefault(d => d.DeductionCode == "UPFRONT_INTEREST" ||
                                        d.DeductionCode == "INTEREST" ||
                                        (d.DeductionName != null && d.DeductionName.Contains("Interest")));

                if (interestDeduction != null)
                {
                    cheque.IntAmount = interestDeduction.Amount;
                    _logger.LogInformation($"✅ Interest updated: {cheque.IntAmount:C} (from {interestDeduction.DeductionCode})");
                }
                else
                {
                    cheque.IntAmount = 0;
                    _logger.LogInformation($"ℹ️ No interest deduction found");
                }

                // ============================================================
                // UPDATE PROCESSING FEE - FROM validDeductions
                // ============================================================
                var processingFeeDeduction = validDeductions
                    .FirstOrDefault(d => d.DeductionCode == "PROC_FEE" ||
                                        d.DeductionCode == "REG_FEE" ||
                                        d.DeductionCode == "PROCESSING_FEE" ||
                                        (d.DeductionName != null && d.DeductionName.Contains("Processing")) ||
                                        (d.DeductionName != null && d.DeductionName.Contains("Registration")));

                if (processingFeeDeduction != null)
                {
                    cheque.ProcessingFee = processingFeeDeduction.Amount;
                    _logger.LogInformation($"✅ Processing Fee updated: {cheque.ProcessingFee:C} (from {processingFeeDeduction.DeductionCode})");
                }
                else
                {
                    cheque.ProcessingFee = 0;
                    _logger.LogInformation($"ℹ️ No processing fee deduction found");
                }

                // ============================================================
                // UPDATE INSURANCE PREMIUM - FROM validDeductions
                // ============================================================
                var insuranceDeduction = validDeductions
                    .FirstOrDefault(d => d.DeductionCode == "INSURANCE" ||
                                        (d.DeductionName != null && d.DeductionName.Contains("Insurance")));

                if (insuranceDeduction != null)
                {
                    cheque.Premium = insuranceDeduction.Amount;
                    _logger.LogInformation($"✅ Insurance Premium updated: {cheque.Premium:C}");
                }
                else
                {
                    cheque.Premium = 0;
                }

                // ============================================================
                // LOG FINAL CHEQUE VALUES
                // ============================================================
                _logger.LogInformation($"=== CHEQUE FINAL VALUES ===");
                _logger.LogInformation($"Amount: {cheque.Amount:C}");
                _logger.LogInformation($"AmountIssued: {cheque.AmountIssued:C}");
                _logger.LogInformation($"IntAmount: {cheque.IntAmount:C}");
                _logger.LogInformation($"ProcessingFee: {cheque.ProcessingFee:C}");
                _logger.LogInformation($"Premium: {cheque.Premium:C}");
                _logger.LogInformation($"Balance: {cheque.Balance:C}");

                // ============================================================
                // UPDATE LOAN STATUS
                // ============================================================
                if (endorsementDto.IsAccepted)
                {
                    loan.Status = (int)Status.Endorsed;
                    loan.Posted = "Endorsed";
                    loan.Aamount = netAmount;
                    _logger.LogInformation($"Loan {loan.LoanNo} status updated from {oldLoanStatus} to Endorsed");
                }
                else
                {
                    loan.Status = (int)Status.Rejected;
                    loan.Posted = "Rejected";
                    loan.AddSecurity = $"Endorsement rejected: {endorsementDto.Remarks ?? "No reason provided"}";
                    _logger.LogInformation($"Loan {loan.LoanNo} status updated from {oldLoanStatus} to Rejected");
                }

                loan.UserName = endorsementDto.EndorsedBy ?? "SYSTEM";
                loan.AuditDateTime = DateTime.Now;

                // Update loan amount if needed
                if (loan.LoanAmt != grossAmount)
                {
                    loan.LoanAmt = grossAmount;
                }

                await _context.SaveChangesAsync();

                string? blockchainTxId = null;

                // ============================================================
                // RECORD BLOCKCHAIN TRANSACTION
                // ============================================================
                try
                {
                    var blockchainData = new
                    {
                        Action = "ENDORSEMENT_UPDATED",
                        EndmainId = endmain.Id,
                        LoanNo = endorsementDto.LoanNo,
                        MemberNo = loan.MemberNo,
                        MemberName = memberName,
                        IsAccepted = endorsementDto.IsAccepted,
                        GrossAmount = grossAmount,
                        TotalDeductions = totalDeductions,
                        NetAmount = netAmount,
                        RejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                        VoucherNo = voucherNo,
                        ChequeNo = cheque.ChequeNo,
                        SourceAccount = endorsementDto.SourceAccountNo,
                        Remarks = endorsementDto.Remarks,
                        LoanStatusAfter = endorsementDto.IsAccepted ? (int)Status.Endorsed : (int)Status.Rejected,
                        UpdatedBy = endorsementDto.EndorsedBy,
                        UpdatedAt = DateTime.Now
                    };

                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = endorsementDto.IsAccepted ? "LOAN_ENDORSEMENT_UPDATED" : "LOAN_ENDORSEMENT_REJECTED",
                        MemberNo = loan.MemberNo,
                        CompanyCode = endorsementDto.CompanyCode,
                        Amount = netAmount,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                        OffChainReferenceId = endmain.MinuteNo,
                        Status = "CONFIRMED",
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    await _context.SaveChangesAsync();

                    blockchainTxId = blockchainTx.TransactionId;

                    endmain.BlockchainTxId = blockchainTx.TransactionId;
                    cheque.BlockchainTxId = blockchainTx.TransactionId;
                    loan.BlockchainTxId = blockchainTx.TransactionId;

                    foreach (var glTxn in newGlTransactions)
                    {
                        glTxn.BlockchainTxId = blockchainTx.TransactionId;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to record blockchain transaction for endorsement update");
                }

                // ============================================================
                // SAVE AUDIT TRAIL FOR ENDORSEMENT UPDATE (ENDMAIN)
                // ============================================================
                var endmainAuditExtraData = new
                {
                    loanNo = endorsementDto.LoanNo,
                    applicantMemberNo = loan.MemberNo,
                    applicantName = memberName,
                    minuteNo = endmain.MinuteNo,
                    meetingDate = endorsementDto.EndorsementDate,
                    isAccepted = endorsementDto.IsAccepted,
                    approvedAmount = grossAmount,
                    oldApprovedAmount = oldEndmainAmtApproved,
                    totalDeductions = totalDeductions,
                    oldTotalDeductions = oldTotalDeductions,
                    netAmount = netAmount,
                    rejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                    oldRejectionReason = oldEndmainAccepted == "0" ? oldEndmainRemarks : null,
                    voucherNo = voucherNo,
                    chequeNo = cheque.ChequeNo,
                    sourceAccountNo = endorsementDto.SourceAccountNo,
                    remarks = endorsementDto.Remarks ?? "",
                    oldRemarks = oldEndmainRemarks,
                    endorsedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    updatedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanAmountBefore = oldLoanAamount,
                    loanAmountAfter = loan.Aamount,
                    blockchainTxId = blockchainTxId
                };

                var endmainForAudit = new
                {
                    endmain.Id,
                    endmain.LoanNo,
                    endmain.MinuteNo,
                    endmain.MeetingDate,
                    endmain.AmtApproved,
                    endmain.Accepted,
                    endmain.Reasons,
                    endmain.Remarks,
                    endmain.TransactionNo,
                    IsAccepted = endorsementDto.IsAccepted,
                    UpdatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    UpdatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTxId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new
                    {
                        AmtApproved = oldEndmainAmtApproved,
                        Accepted = oldEndmainAccepted,
                        Remarks = oldEndmainRemarks
                    },
                    newModel: endmainForAudit,
                    tableName: "Endmain",
                    recordId: endmain.Id.ToString(),
                    userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                    userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                    companyCode: endorsementDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(endmainAuditExtraData),
                    blockchainTxId: blockchainTxId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR CHEQUE UPDATE
                // ============================================================
                if (cheque != null)
                {
                    var chequeAuditExtraData = new
                    {
                        loanNo = endorsementDto.LoanNo,
                        applicantMemberNo = loan.MemberNo,
                        applicantName = memberName,
                        chequeId = cheque.Id,
                        chequeNo = cheque.ChequeNo,
                        voucherNo = voucherNo,
                        amount = cheque.Amount,
                        amountIssued = cheque.AmountIssued,
                        oldAmountIssued = oldChequeAmountIssued,
                        netAmount = netAmount,
                        totalDeductions = totalDeductions,
                        dateIssued = cheque.DateIssued,
                        status = cheque.Status,
                        sourceAccount = endorsementDto.SourceAccountNo,
                        loanAccount = loanAcc,
                        remarks = cheque.Remarks,
                        updatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        updatedDate = DateTime.Now,
                        blockchainTxId = blockchainTxId
                    };

                    var chequeForAudit = new
                    {
                        cheque.Id,
                        cheque.LoanNo,
                        cheque.MemberNo,
                        cheque.Amount,
                        cheque.AmountIssued,
                        cheque.ChequeNo,
                        cheque.Voucherno,
                        cheque.Voucheramount,
                        cheque.DateIssued,
                        cheque.Status,
                        cheque.LoanAcc,
                        cheque.ContraAcc,
                        cheque.PremiumAcc,
                        cheque.Remarks,
                        UpdatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        UpdatedDate = DateTime.Now,
                        BlockchainTxId = blockchainTxId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new
                        {
                            AmountIssued = oldChequeAmountIssued,
                            Balance = oldChequeBalance
                        },
                        newModel: chequeForAudit,
                        tableName: "Cheques",
                        recordId: cheque.Id.ToString(),
                        userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                        userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                        companyCode: endorsementDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(chequeAuditExtraData),
                        blockchainTxId: blockchainTxId
                    );
                }

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN STATUS CHANGE
                // ============================================================
                var loanAuditExtraData = new
                {
                    loanNo = endorsementDto.LoanNo,
                    statusChangedFrom = oldLoanStatus,
                    statusChangedTo = loan.Status,
                    reason = endorsementDto.IsAccepted ? "Loan endorsement updated" : "Loan endorsement rejected",
                    approvedAmount = grossAmount,
                    oldApprovedAmount = oldEndmainAmtApproved,
                    totalDeductions = totalDeductions,
                    oldTotalDeductions = oldTotalDeductions,
                    netDisbursedAmount = netAmount,
                    rejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks,
                    voucherNo = voucherNo,
                    chequeNo = cheque.ChequeNo,
                    sourceAccount = endorsementDto.SourceAccountNo,
                    minuteNo = endmain.MinuteNo,
                    updatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    updatedDate = DateTime.Now,
                    blockchainTxId = blockchainTxId
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.LoanAmt,
                    loan.Aamount,
                    loan.Status,
                    loan.Posted,
                    loan.UserName,
                    loan.AuditDateTime,
                    UpdatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                    UpdateReason = endorsementDto.IsAccepted ? "Loan endorsement updated" : "Loan endorsement rejected",
                    NetDisbursedAmount = netAmount,
                    IsAccepted = endorsementDto.IsAccepted,
                    RejectionReason = endorsementDto.IsAccepted ? null : endorsementDto.Remarks
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new
                    {
                        Status = oldLoanStatus,
                        Posted = oldLoanPosted,
                        Aamount = oldLoanAamount
                    },
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: endorsementDto.LoanNo,
                    userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                    userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                    companyCode: endorsementDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                    blockchainTxId: blockchainTxId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR GL TRANSACTIONS (if accepted)
                // ============================================================
                if (endorsementDto.IsAccepted && newGlTransactions.Any())
                {
                    var glAuditExtraData = new
                    {
                        loanNo = endorsementDto.LoanNo,
                        voucherNo = voucherNo,
                        chequeNo = cheque.ChequeNo,
                        transactionNo = transactionNo,
                        totalTransactions = newGlTransactions.Count,
                        oldTotalTransactions = oldGLTransactions.Count,
                        createdBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        createdDate = DateTime.Now,
                        blockchainTxId = blockchainTxId
                    };

                    var glForAudit = new
                    {
                        LoanNo = endorsementDto.LoanNo,
                        VoucherNo = voucherNo,
                        ChequeNo = cheque.ChequeNo,
                        TransactionNo = transactionNo,
                        TotalDeductions = totalDeductions,
                        NetDisbursement = netAmount,
                        CreatedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        CreatedDate = DateTime.Now,
                        BlockchainTxId = blockchainTxId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new
                        {
                            TotalTransactions = oldGLTransactions.Count,
                            TotalAmount = oldTotalDeductions
                        },
                        newModel: glForAudit,
                        tableName: "Gltransactions",
                        recordId: voucherNo,
                        userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                        userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                        companyCode: endorsementDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(glAuditExtraData),
                        blockchainTxId: blockchainTxId
                    );
                }
                // If rejected, audit the deletion of GL transactions
                else if (!endorsementDto.IsAccepted && oldGLTransactions.Any())
                {
                    var glAuditExtraData = new
                    {
                        loanNo = endorsementDto.LoanNo,
                        voucherNo = voucherNo,
                        chequeNo = cheque.ChequeNo,
                        deletedTransactions = oldGLTransactions.Count,
                        deletedAmount = oldTotalDeductions,
                        reason = "Endorsement rejected - GL transactions removed",
                        deletedBy = endorsementDto.EndorsedBy ?? "SYSTEM",
                        deletedDate = DateTime.Now,
                        blockchainTxId = blockchainTxId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Delete,
                        oldModel: new
                        {
                            TotalTransactions = oldGLTransactions.Count,
                            TotalAmount = oldTotalDeductions
                        },
                        newModel: null,
                        tableName: "Gltransactions",
                        recordId: voucherNo,
                        userId: endorsementDto.EndorsedBy ?? "SYSTEM",
                        userName: endorsementDto.EndorsedBy ?? "SYSTEM",
                        companyCode: endorsementDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(glAuditExtraData),
                        blockchainTxId: blockchainTxId
                    );
                }

                _logger.LogInformation($"Endorsement update audit completed for loan {endorsementDto.LoanNo}");

                await transaction.CommitAsync();

                if (endorsementDto.IsAccepted)
                {
                    _logger.LogInformation($"Endorsement updated successfully for loan {endorsementDto.LoanNo}. " +
                        $"Gross Amount: {grossAmount:C}, Total Deductions: {totalDeductions:C}, Net Amount: {netAmount:C}");
                }
                else
                {
                    _logger.LogInformation($"Endorsement REJECTED for loan {endorsementDto.LoanNo}. Reason: {endorsementDto.Remarks}");
                }

                return endmain;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating endorsement for loan {endorsementDto.LoanNo}");
                throw;
            }
        }

        public async Task<EndorsementDetailsDTO> GetEndorsementDetailsAsync(string loanNo, string companyCode)
        {
            try
            {
                // Get loan
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {loanNo} not found");

                // Get member
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get endorsement
                var endmain = await _context.Endmain
                    .FirstOrDefaultAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);

                if (endmain == null)
                    throw new InvalidOperationException($"Endorsement not found for loan {loanNo}");

                // Get cheque
                var cheque = await _context.Cheques
                    .FirstOrDefaultAsync(c => c.LoanNo == loanNo && c.CompanyCode == companyCode);

                if (cheque == null)
                    throw new InvalidOperationException($"Cheque not found for loan {loanNo}");

                // Get GL transactions
                var glTransactions = await _context.Gltransactions
                    .Where(g => g.DocumentNo == cheque.Voucherno && g.Source == "LOAN_ENDORSEMENT")
                    .ToListAsync();

                // Get GL account names
                var glAccountNos = glTransactions.Select(g => g.DrAccNo).Distinct().ToList();
                glAccountNos.Add(cheque.ContraAcc);
                glAccountNos.Add(cheque.LoanAcc);

                var glAccounts = await _context.GlSetup
                    .Where(g => glAccountNos.Contains(g.AccNo) && g.CompanyCode == companyCode)
                    .ToDictionaryAsync(g => g.AccNo, g => g.Glaccname);

                var grossAmount = loan.LoanAmt ?? 0;
                var totalDeductions = glTransactions.Sum(g => g.Amount);
                var netAmount = grossAmount - totalDeductions;

                // Build deduction details
                var deductionDetails = new List<EndorsementDeductionDetailDTO>();
                foreach (var gl in glTransactions)
                {
                    string deductionName = "Deduction";
                    string deductionCode = "UNKNOWN";
                    bool isPercentage = false;
                    decimal percentageValue = 0;

                    var descParts = gl.TransDescript?.Split(' ') ?? new string[0];
                    if (descParts.Length > 0)
                    {
                        deductionName = descParts[0];
                        deductionCode = deductionName.Replace(" ", "_").ToUpper();
                    }

                    if (gl.TransDescript?.Contains("%") == true)
                    {
                        isPercentage = true;
                        var percentMatch = System.Text.RegularExpressions.Regex.Match(gl.TransDescript, @"(\d+\.?\d*)%");
                        if (percentMatch.Success && decimal.TryParse(percentMatch.Groups[1].Value, out decimal pct))
                        {
                            percentageValue = pct;
                        }
                    }

                    deductionDetails.Add(new EndorsementDeductionDetailDTO
                    {
                        DeductionCode = deductionCode,
                        DeductionName = deductionName,
                        Amount = gl.Amount,
                        GlAccountNo = gl.DrAccNo,
                        GlAccountName = glAccounts.GetValueOrDefault(gl.DrAccNo) ?? gl.DrAccNo,
                        Description = gl.TransDescript,
                        IsPercentage = isPercentage,
                        PercentageValue = percentageValue
                    });
                }

                // Build GL transaction details
                var glTransactionDetails = new List<EndorsementGLTransactionDTO>();
                foreach (var gl in glTransactions)
                {
                    glTransactionDetails.Add(new EndorsementGLTransactionDTO
                    {
                        Id = gl.Id,
                        Amount = gl.Amount,
                        DrAccNo = gl.DrAccNo,
                        CrAccNo = gl.CrAccNo,
                        DrAccountName = glAccounts.GetValueOrDefault(gl.DrAccNo) ?? gl.DrAccNo,
                        CrAccountName = glAccounts.GetValueOrDefault(gl.CrAccNo) ?? gl.CrAccNo,
                        Description = gl.TransDescript ?? "",
                        DocumentNo = gl.DocumentNo ?? ""
                    });
                }

                // Add net disbursement GL
                var netDisbursementGL = await _context.Gltransactions
                    .FirstOrDefaultAsync(g => g.DocumentNo == cheque.Voucherno && g.Source == "LOAN_DISBURSEMENT");

                if (netDisbursementGL != null)
                {
                    glTransactionDetails.Add(new EndorsementGLTransactionDTO
                    {
                        Id = netDisbursementGL.Id,
                        Amount = netDisbursementGL.Amount,
                        DrAccNo = netDisbursementGL.DrAccNo,
                        CrAccNo = netDisbursementGL.CrAccNo,
                        DrAccountName = glAccounts.GetValueOrDefault(netDisbursementGL.DrAccNo) ?? netDisbursementGL.DrAccNo,
                        CrAccountName = glAccounts.GetValueOrDefault(netDisbursementGL.CrAccNo) ?? netDisbursementGL.CrAccNo,
                        Description = netDisbursementGL.TransDescript ?? "Net Disbursement",
                        DocumentNo = netDisbursementGL.DocumentNo ?? ""
                    });
                }

                // Get member phone number
                string phoneNo = "N/A";
                if (member != null)
                {
                    phoneNo = member.PhoneNo ?? member.MobileNo ?? "N/A";
                }

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;
                if (string.IsNullOrEmpty(memberName)) memberName = loan.MemberNo;

                return new EndorsementDetailsDTO
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = memberName,
                    PhoneNo = phoneNo,  
                    LoanTypeName = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                    GrossAmount = grossAmount,
                    NetAmount = netAmount,
                    TotalDeductions = totalDeductions,
                    Status = endmain.Accepted == "1" ? "Endorsed" : "Rejected",
                    EndorsementDate = endmain.MeetingDate ?? DateTime.Now,
                    EndorsedBy = endmain.ChairSigned ?? endmain.AuditId ?? "SYSTEM",
                    Remarks = endmain.Remarks,
                    MinuteNo = endmain.MinuteNo,
                    VoucherNo = cheque.Voucherno,
                    ChequeNo = cheque.ChequeNo,
                    SourceAccountNo = cheque.ContraAcc,
                    Deductions = deductionDetails,
                    GLTransactions = glTransactionDetails
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting endorsement details for loan {loanNo}");
                throw;
            }
        }
        private async Task<string> GenerateChequeNumberAsync(string companyCode)
        {
            var prefix = "CHQ";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            try
            {
                var lastCheque = await _context.Cheques
                    .Where(c => c.CompanyCode == companyCode && c.ChequeNo != null && c.ChequeNo.StartsWith($"{prefix}{date}"))
                    .OrderByDescending(c => c.ChequeNo)
                    .Select(c => c.ChequeNo)
                    .FirstOrDefaultAsync();

                if (lastCheque != null && lastCheque.Length > 11)
                {
                    var sequenceStr = lastCheque.Substring(11);
                    if (int.TryParse(sequenceStr, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating cheque number, using default sequence");
            }

            return $"{prefix}{date}{sequence:D4}";
        }

        private async Task<string> GenerateVoucherNumberAsync(string companyCode)
        {
            var prefix = "VNO";
            var date = DateTime.Now.ToString("ddMMyyyy");
            var sequence = 1;

            try
            {
                var lastVoucher = await _context.Cheques
                    .Where(c => c.CompanyCode == companyCode && c.Voucherno != null && c.Voucherno.StartsWith($"{prefix}{date}"))
                    .OrderByDescending(c => c.Voucherno)
                    .Select(c => c.Voucherno)
                    .FirstOrDefaultAsync();

                if (lastVoucher != null && lastVoucher.Length > 11)
                {
                    var sequenceStr = lastVoucher.Substring(11);
                    if (int.TryParse(sequenceStr, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating voucher number, using default sequence");
            }

            return $"{prefix}{date}{sequence:D3}";
        }

        private async Task<string> GenerateMinuteNumberAsync(string companyCode)
        {
            var prefix = "MIN";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            try
            {
                var lastMinute = await _context.Endmain
                    .Where(e => e.CompanyCode == companyCode && e.MinuteNo != null && e.MinuteNo.StartsWith($"{prefix}{date}"))
                    .OrderByDescending(e => e.MinuteNo)
                    .Select(e => e.MinuteNo)
                    .FirstOrDefaultAsync();

                if (lastMinute != null && lastMinute.Length > 11)
                {
                    var sequenceStr = lastMinute.Substring(11);
                    if (int.TryParse(sequenceStr, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating minute number, using default sequence");
            }

            return $"{prefix}{date}{sequence:D4}";
        }

        private string NumberToWords(decimal number)
        {
            if (number == 0)
                return "ZERO";

            var integerPart = (int)Math.Floor(number);
            var fractionPart = (int)((number - integerPart) * 100);

            var words = ConvertIntegerToWords(integerPart);
            words += " SHILLINGS";

            if (fractionPart > 0)
            {
                words += $" AND {ConvertIntegerToWords(fractionPart)} CENTS";
            }

            return words.ToUpper();
        }

        private string ConvertIntegerToWords(int number)
        {
            if (number == 0)
                return "ZERO";

            var units = new[] { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
            var tens = new[] { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

            if (number < 20)
                return units[number];

            if (number < 100)
                return tens[number / 10] + (number % 10 > 0 ? " " + units[number % 10] : "");

            if (number < 1000)
                return units[number / 100] + " HUNDRED" + (number % 100 > 0 ? " " + ConvertIntegerToWords(number % 100) : "");

            if (number < 1000000)
                return ConvertIntegerToWords(number / 1000) + " THOUSAND" + (number % 1000 > 0 ? " " + ConvertIntegerToWords(number % 1000) : "");

            return ConvertIntegerToWords(number / 1000000) + " MILLION" + (number % 1000000 > 0 ? " " + ConvertIntegerToWords(number % 1000000) : "");
        }

        public async Task<Endmain> GetEndorsementByLoanNoAsync(string loanNo, string companyCode)
        {
            return await _context.Endmain
                .Where(e => e.LoanNo == loanNo && e.CompanyCode == companyCode)
                .Select(e => new Endmain
                {
                    Id = e.Id,
                    LoanNo = e.LoanNo,
                    CompanyCode = e.CompanyCode,
                    MinuteNo = e.MinuteNo,
                    MeetingDate = e.MeetingDate,
                    AmtApproved = e.AmtApproved,
                    Accepted = e.Accepted,
                    ChairSigned = e.ChairSigned,
                    SecSigned = e.SecSigned,
                    MembSigned = e.MembSigned,
                    Reasons = e.Reasons,
                    Remarks = e.Remarks,
                    AuditId = e.AuditId,
                    AuditTime = e.AuditTime,
                    TransactionNo = e.TransactionNo,
                    BlockchainTxId = e.BlockchainTxId
                })
                .FirstOrDefaultAsync();
        }
        public async Task<Endmain> GetEndorsementByMinuteNoAsync(string minuteNo, string companyCode)
        {
            return await _context.Endmain
                .FirstOrDefaultAsync(e => e.MinuteNo == minuteNo && e.CompanyCode == companyCode);
        }

        public async Task<List<Endmain>> GetEndorsementsByLoanNoAsync(string loanNo, string companyCode)
        {
            return await _context.Endmain
                .Where(e => e.LoanNo == loanNo && e.CompanyCode == companyCode)
                .OrderByDescending(e => e.MeetingDate)
                .ToListAsync();
        }

        public async Task<List<LoanDeductionDTO>> GetAvailableDeductionsAsync(string companyCode)
        {
            return new List<LoanDeductionDTO>
            {
                new LoanDeductionDTO
                {
                    DeductionCode = "PROC_FEE",
                    DeductionName = "Processing Fee",
                    GlAccountNo = "",
                    GlAccountName = "",
                    IsMandatory = false,
                    Description = "Loan processing fee (enter amount)",
                    IsPercentage = false,
                    PercentageValue = null,
                    Amount = 0
                },
                new LoanDeductionDTO
                {
                    DeductionCode = "INSURANCE",
                    DeductionName = "Insurance Premium",
                    GlAccountNo = "",
                    GlAccountName = "",
                    IsMandatory = false,
                    Description = "Loan insurance premium (enter amount)",
                    IsPercentage = false,
                    PercentageValue = null,
                    Amount = 0
                },
                new LoanDeductionDTO
                {
                    DeductionCode = "INTEREST",
                    DeductionName = "Interest",
                    GlAccountNo = "",
                    GlAccountName = "",
                    IsMandatory = false,
                    Description = "Loan interest (can be percentage or fixed amount)",
                    IsPercentage = true,  // Default to percentage mode
                    PercentageValue = 0,
                    Amount = 0
                },
                new LoanDeductionDTO
                {
                    DeductionCode = "LEGAL",
                    DeductionName = "Legal Fees",
                    GlAccountNo = "",
                    GlAccountName = "",
                    IsMandatory = false,
                    Description = "Legal fees (enter amount)",
                    IsPercentage = false,
                    PercentageValue = null,
                    Amount = 0
                },
                new LoanDeductionDTO
                {
                    DeductionCode = "OTHER",
                    DeductionName = "Other Charges",
                    GlAccountNo = "",
                    GlAccountName = "",
                    IsMandatory = false,
                    Description = "Other miscellaneous charges (enter amount)",
                    IsPercentage = false,
                    PercentageValue = null,
                    Amount = 0
                }
            };
        }

        public async Task<decimal> CalculateTotalDeductionsAsync(string loanNo, List<LoanDeductionDTO> deductions)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            var loanAmount = loan.LoanAmt ?? 0;
            var total = 0m;

            foreach (var deduction in deductions)
            {
                if (deduction.IsPercentage && deduction.PercentageValue.HasValue)
                {
                    deduction.Amount = loanAmount * (deduction.PercentageValue.Value / 100);
                    total += deduction.Amount;
                }
                else
                {
                    total += deduction.Amount;
                }
            }

            return total;
        }

        public async Task<bool> HasEndorsementAsync(string loanNo, string companyCode)
        {
            try
            {
                return await _context.Endmain
                    .AnyAsync(e => e.LoanNo == loanNo && e.CompanyCode == companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking endorsement for loan {loanNo}");
                return false;
            }
        }


        #endregion


        #region Disbursement
        public async Task<Cheque> DisburseLoanAsync(LoanDisbursementDTO disbursementDto)
        {
            var currentUserRole = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

            if (currentUserRole != "Finance Officer" && currentUserRole != "Super Admin" && currentUserRole != "Admin")
            {
                throw new UnauthorizedAccessException("Only Finance Officers can disburse loans");
            }

            // Generate base transaction numbers (same for all records)
            string baseTransactionNo = $"DTRS{DateTime.Now:yyyyMMddHHmmss}";
            string initialReceiptNo = $"RCP-{DateTime.Now:yyyyMMddHHmmss}";
            string mpesaReceiptNo = null; // Will be updated from API response

            // Get loan details
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == disbursementDto.LoanNo && l.CompanyCode == disbursementDto.CompanyCode);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {disbursementDto.LoanNo} not found");
            }

            // Get member details
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == disbursementDto.CompanyCode);

            string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : loan.MemberNo;
            string memberPhone = member?.PhoneNo ?? member?.MobileNo ?? "";

            // Clean phone number
            if (!string.IsNullOrEmpty(memberPhone))
            {
                memberPhone = memberPhone.Replace("+", "").Replace(" ", "").Replace("-", "");
                if (memberPhone.StartsWith("0"))
                    memberPhone = "254" + memberPhone.Substring(1);
                if (!memberPhone.StartsWith("254") && memberPhone.Length == 9)
                    memberPhone = "254" + memberPhone;
            }

            // Get endorsement and cheque
            var endmain = await _context.Endmain
                .FirstOrDefaultAsync(e => e.LoanNo == disbursementDto.LoanNo && e.CompanyCode == disbursementDto.CompanyCode);

            if (endmain == null)
            {
                throw new InvalidOperationException($"Endorsement not found for loan {disbursementDto.LoanNo}");
            }

            var existingCheque = await _context.Cheques
                .FirstOrDefaultAsync(c => c.LoanNo == disbursementDto.LoanNo && c.CompanyCode == disbursementDto.CompanyCode);

            if (existingCheque == null)
            {
                throw new InvalidOperationException($"Cheque record not found for loan {disbursementDto.LoanNo}");
            }

            decimal approvedAmount = endmain.AmtApproved;
            decimal netDisbursedAmount = existingCheque.AmountIssued ?? approvedAmount;

            // Check if already disbursed
            var existingLoanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == disbursementDto.LoanNo && lb.Companycode == disbursementDto.CompanyCode);

            if (existingLoanbal != null)
            {
                throw new InvalidOperationException($"Loan already disbursed. Loan balance record exists.");
            }

            // ============================================================
            // CHECK B2C SETUP AND CREATE RECORDS FIRST
            // ============================================================
            var apiSettings = await _appDbContext.ApiTable
                .FirstOrDefaultAsync(a => a.CompanyCode == disbursementDto.CompanyCode && a.Status == "Active");

            bool isB2CEnabled = apiSettings != null && !string.IsNullOrEmpty(apiSettings.ConsumerKey) && !string.IsNullOrEmpty(apiSettings.ConsumerSecret);

            ApiTransaction? apiTransaction = null;
            TransactionDetail? transactionDetail = null;
            Transaction? transactionRecord = null;
            Transactions2? transaction2Record = null;
            bool b2cPaymentSuccess = false;
            string b2cResponseMessage = "";
            string conversationId = "";
            string checkoutId = "";
            string mpesaTransactionId = ""; // Store M-Pesa transaction ID from response

            // ============================================================
            // CREATE TRANSACTION RECORDS FIRST (BEFORE MAIN DB TRANSACTION)
            // ============================================================
            using (var b2cScope = await _appDbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. CREATE TRANSACTION RECORD
                    transactionRecord = new Transaction
                    {
                        TransactionNo = baseTransactionNo,
                        Amount = netDisbursedAmount,
                        TransDate = DateTime.Now,
                        AuditId = disbursementDto.DisbursedBy ?? "SYSTEM",
                        AuditTime = DateTime.Now,
                        TransDescription = $"Loan Disbursement - {loan.LoanNo} - Member: {memberName}",
                        Status = "Pending",
                        CompanyCode = disbursementDto.CompanyCode,
                        Channel = "B2C",
                        AuditDateTime = DateTime.Now
                    };
                    _appDbContext.Transactions.Add(transactionRecord);
                    await _appDbContext.SaveChangesAsync();
                    _logger.LogInformation($"Transaction record created - TransactionNo: {baseTransactionNo}, ReceiptNo: {initialReceiptNo}");

                    // 2. CREATE TRANSACTIONS2 RECORD
                    transaction2Record = new Transactions2
                    {
                        MemberNo = loan.MemberNo,
                        Companycode = disbursementDto.CompanyCode,
                        TransactionNo = baseTransactionNo,
                        ReceiptNo = initialReceiptNo, 
                        PaymentMode = memberPhone,
                        TransactionType = "Withdraw",
                        Amount = netDisbursedAmount,
                        ContributionDate = DateTime.Now,
                        DepositedDate = DateTime.Now,
                        AuditId = disbursementDto.DisbursedBy ?? "SYSTEM",
                        AuditTime = DateTime.Now,
                        Status = "Pending",
                        RunE = 0,
                        SessionId = Guid.NewGuid().ToString(),
                        Contact = memberPhone,
                        AuditDateTime = DateTime.Now,
                    };
                    _appDbContext.Transactions2.Add(transaction2Record);
                    await _appDbContext.SaveChangesAsync();
                    _logger.LogInformation($"Transactions2 record created - TransactionNo: {baseTransactionNo}, ReceiptNo: {initialReceiptNo}");

                    // Only create B2C specific records if B2C is enabled
                    if (isB2CEnabled && netDisbursedAmount > 0 && !string.IsNullOrEmpty(memberPhone))
                    {
                        conversationId = DateTime.Now.ToString("yyyyMMddHHmmss") + Guid.NewGuid().ToString().Substring(0, 8);
                        checkoutId = Guid.NewGuid().ToString();

                        // 3. CREATE APITRANSACTION RECORD
                        apiTransaction = new ApiTransaction
                        {
                            CompanyCode = disbursementDto.CompanyCode,
                            ApiUser = disbursementDto.DisbursedBy ?? "SYSTEM",
                            ShortCode = apiSettings.ShortCode,
                            TransactionCode = $"DISP-{disbursementDto.LoanNo}",
                            CheckoutId = checkoutId,
                            ConversationId = conversationId,
                            Amount = netDisbursedAmount,
                            Recipient = memberPhone,
                            StatusCode = 1, // 1 = Pending
                            ResultDescription = "B2C payment initiated - pending processing",
                            created_at = DateTime.Now,
                            updated_at = DateTime.Now,
                            LoanNo = disbursementDto.LoanNo,
                            AuditDateTime = DateTime.Now,
                        };
                        _appDbContext.ApiTransactions.Add(apiTransaction);
                        await _appDbContext.SaveChangesAsync();
                        _logger.LogInformation($"ApiTransaction record created - ConversationId: {conversationId}");

                        // 4. CREATE TRANSACTIONDETAIL RECORD
                        transactionDetail = new TransactionDetail
                        {
                            CompanyCode = disbursementDto.CompanyCode,
                            TransactionId = Guid.NewGuid().ToString(),
                            TransactionCode = $"DISP-{disbursementDto.LoanNo}",
                            ResultCode = 1,
                            ResultMessage = "B2C payment initiated - pending",
                            Amount = netDisbursedAmount,
                            Status = "Pending",
                            ConversationId = conversationId,
                            ShortCode = apiSettings.ShortCode ?? 0,
                            UpdatedAt = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            MemberNo = loan.MemberNo,
                            Phone = memberPhone,
                            OriginatorConversationId = conversationId,
                            MerchantRequestId = conversationId,
                            CheckoutRequestId = checkoutId,
                            AuditDateTime = DateTime.Now,
                        };
                        _appDbContext.Transaction_detail.Add(transactionDetail);
                        await _appDbContext.SaveChangesAsync();
                        _logger.LogInformation($"TransactionDetail record created");

                        await b2cScope.CommitAsync();
                        _logger.LogInformation("All B2C records created successfully before sending payment request");
                    }
                    else
                    {
                        await b2cScope.CommitAsync();
                        _logger.LogInformation($"Transaction records created. B2C not enabled. Phone: {(string.IsNullOrEmpty(memberPhone) ? "missing" : "present")}");
                    }
                }
                catch (Exception ex)
                {
                    await b2cScope.RollbackAsync();
                    _logger.LogError(ex, "Failed to create transaction records before B2C");
                    isB2CEnabled = false;
                    b2cResponseMessage = $"Failed to create transaction records: {ex.Message}";
                }
            }

            // ============================================================
            // SEND B2C PAYMENT REQUEST (if enabled)
            // ============================================================
            if (isB2CEnabled && apiTransaction != null && transactionDetail != null)
            {
                try
                {
                    _logger.LogInformation($"Sending B2C payment of {netDisbursedAmount:C} to {memberPhone}");

                    string postdataurl = "https://easysacco.amtech.co.ke:9090/Apis/Simulate";

                    WebRequest request = WebRequest.Create(postdataurl);
                    request.Method = "POST";
                    request.Timeout = 60000;

                    var apiSimulate = new
                    {
                        reference = loan.MemberNo,
                        amount = netDisbursedAmount,
                        companycode = disbursementDto.CompanyCode,
                        phone = memberPhone,
                        action = "b2c",
                        ApiKey = "BVmY1Ufl8FeazdlKnWQ5e/hgUN8p/+dapzDagzNL1eCRAOhW67X0risDPOxZdVv+pVHKB7Oi3vsb/skOlxDZjPaW36i6A8n9+xleI6zsyNO1jT0SO9+h5mtZNK5ur7NeZK0gUdJfAGCANbCxzeuZo5PcAfPVfdhFUSuGvfU2nPxpD2dREAE/xuA85XVBdwlwRKCteNbpnLABgaHhfJPYwgTBu+aqLYYNZODBegwyHthTauvCSKVnb1BYgbrsrf34GlrcV7jEZQBMxFoiN2E4n72Mm/z2SeVGhCFGml0bOq8WQTlBC8p9ifT2TcmfFPZ3bv+sd8U0niRGKYfyw9BATg==",
                    };

                    string postData = System.Text.Json.JsonSerializer.Serialize(apiSimulate);
                    byte[] byteArray = Encoding.UTF8.GetBytes(postData);

                    request.ContentType = "application/json";
                    request.ContentLength = byteArray.Length;

                    using (Stream dataStream = await request.GetRequestStreamAsync())
                    {
                        await dataStream.WriteAsync(byteArray, 0, byteArray.Length);
                    }

                    using (WebResponse response = await request.GetResponseAsync())
                    {
                        HttpWebResponse httpResponse = (HttpWebResponse)response;

                        if (httpResponse.StatusCode == HttpStatusCode.OK)
                        {
                            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                            {
                                string responseFromServer = await reader.ReadToEndAsync();
                                _logger.LogInformation($"B2C Response received: {responseFromServer}");

                                // ============================================================
                                // PARSE M-PESA TRANSACTION ID FROM RESPONSE
                                // ============================================================
                                // Try to extract M-Pesa receipt number from response
                                // The actual response format may vary - adjust parsing based on your API response
                                try
                                {
                                    var jsonResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseFromServer);

                                    // Common field names where M-Pesa transaction ID might be
                                    if (jsonResponse != null)
                                    {
                                        // Try different possible field names
                                        if (jsonResponse.ContainsKey("TransactionID"))
                                            mpesaTransactionId = jsonResponse["TransactionID"]?.ToString();
                                        else if (jsonResponse.ContainsKey("MpesaReceiptNumber"))
                                            mpesaTransactionId = jsonResponse["MpesaReceiptNumber"]?.ToString();
                                        else if (jsonResponse.ContainsKey("ReceiptNumber"))
                                            mpesaTransactionId = jsonResponse["ReceiptNumber"]?.ToString();
                                        else if (jsonResponse.ContainsKey("OriginatorConversationID"))
                                            mpesaTransactionId = jsonResponse["OriginatorConversationID"]?.ToString();
                                        else if (jsonResponse.ContainsKey("ConversationID"))
                                            mpesaTransactionId = jsonResponse["ConversationID"]?.ToString();

                                        // If found, update receipt number
                                        if (!string.IsNullOrEmpty(mpesaTransactionId))
                                        {
                                            _logger.LogInformation($"M-Pesa Transaction ID received: {mpesaTransactionId}");
                                        }
                                        else
                                        {
                                            _logger.LogWarning("No M-Pesa transaction ID found in response");
                                            // Use conversation ID as fallback
                                            mpesaTransactionId = conversationId;
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning(parseEx, "Could not parse M-Pesa transaction ID from response");
                                    mpesaTransactionId = conversationId; // Fallback to conversation ID
                                }

                                // Update records to success
                                b2cPaymentSuccess = true;
                                b2cResponseMessage = "Payment processed successfully";

                                // ============================================================
                                // UPDATE RECEIPT NUMBERS WITH MPESA TRANSACTION ID
                                // ============================================================
                                string finalReceiptNo = !string.IsNullOrEmpty(mpesaTransactionId)
                                    ? mpesaTransactionId
                                    : initialReceiptNo;

                                // Update ApiTransaction
                                apiTransaction.StatusCode = 0; // Success
                                apiTransaction.ResultDescription = responseFromServer.Length > 500 ? responseFromServer.Substring(0, 500) : responseFromServer;
                                apiTransaction.updated_at = DateTime.Now;
                                _appDbContext.ApiTransactions.Update(apiTransaction);

                                // Update TransactionDetail
                                transactionDetail.ResultCode = 0;
                                transactionDetail.ResultMessage = "Payment successful";
                                transactionDetail.Status = "Completed";
                                transactionDetail.UpdatedAt = DateTime.Now;
                                _appDbContext.Transaction_detail.Update(transactionDetail);

                                // Update Transaction with M-Pesa receipt number
                                if (transactionRecord != null)
                                {
                                    transactionRecord.Status = "Completed";
                                    //transactionRecord.ReceiptNo = finalReceiptNo; // Update to M-Pesa receipt number
                                    _appDbContext.Transactions.Update(transactionRecord);
                                    _logger.LogInformation($"Transaction record updated - ReceiptNo: {finalReceiptNo}");
                                }

                                // Update Transactions2 with M-Pesa receipt number
                                if (transaction2Record != null)
                                {
                                    transaction2Record.Status = "Completed";
                                    transaction2Record.ReceiptNo = finalReceiptNo; // Update to M-Pesa receipt number
                                    _appDbContext.Transactions2.Update(transaction2Record);
                                    _logger.LogInformation($"Transactions2 record updated - ReceiptNo: {finalReceiptNo}");
                                }

                                await _appDbContext.SaveChangesAsync();
                                _logger.LogInformation($"✅ B2C Payment successful - Receipt updated to: {finalReceiptNo}");
                            }
                        }
                        else
                        {
                            b2cPaymentSuccess = false;
                            b2cResponseMessage = $"HTTP Error: {httpResponse.StatusCode}";

                            // Update records to failed
                            apiTransaction.StatusCode = 2;
                            apiTransaction.ResultDescription = $"HTTP Error: {httpResponse.StatusCode}";
                            apiTransaction.updated_at = DateTime.Now;
                            _appDbContext.ApiTransactions.Update(apiTransaction);

                            transactionDetail.ResultCode = 2;
                            transactionDetail.ResultMessage = $"HTTP Error: {httpResponse.StatusCode}";
                            transactionDetail.Status = "Failed";
                            transactionDetail.UpdatedAt = DateTime.Now;
                            _appDbContext.Transaction_detail.Update(transactionDetail);

                            // Keep initial receipt number for failed transactions
                            if (transactionRecord != null)
                            {
                                transactionRecord.Status = "Failed";
                                _appDbContext.Transactions.Update(transactionRecord);
                            }
                            if (transaction2Record != null)
                            {
                                transaction2Record.Status = "Failed";
                                _appDbContext.Transactions2.Update(transaction2Record);
                            }

                            await _appDbContext.SaveChangesAsync();
                            _logger.LogWarning($"❌ B2C HTTP Error: {httpResponse.StatusCode}");
                        }
                    }
                }
                catch (WebException webEx)
                {
                    _logger.LogError(webEx, "WebException during B2C payment");
                    b2cPaymentSuccess = false;
                    b2cResponseMessage = $"Network error: {webEx.Message}";

                    if (apiTransaction != null)
                    {
                        apiTransaction.StatusCode = 2;
                        apiTransaction.ResultDescription = $"Network error: {webEx.Message}";
                        apiTransaction.updated_at = DateTime.Now;
                        _appDbContext.ApiTransactions.Update(apiTransaction);
                    }

                    if (transactionDetail != null)
                    {
                        transactionDetail.ResultCode = 2;
                        transactionDetail.ResultMessage = $"Network error: {webEx.Message}";
                        transactionDetail.Status = "Failed";
                        transactionDetail.UpdatedAt = DateTime.Now;
                        _appDbContext.Transaction_detail.Update(transactionDetail);
                    }

                    // Keep initial receipt number for failed transactions
                    if (transactionRecord != null)
                    {
                        transactionRecord.Status = "Failed";
                        _appDbContext.Transactions.Update(transactionRecord);
                    }
                    if (transaction2Record != null)
                    {
                        transaction2Record.Status = "Failed";
                        _appDbContext.Transactions2.Update(transaction2Record);
                    }

                    await _appDbContext.SaveChangesAsync();
                    _logger.LogWarning($"B2C failed but continuing with loan disbursement");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during B2C payment");
                    b2cPaymentSuccess = false;
                    b2cResponseMessage = $"Error: {ex.Message}";

                    if (apiTransaction != null)
                    {
                        apiTransaction.StatusCode = 2;
                        apiTransaction.ResultDescription = $"Error: {ex.Message}";
                        apiTransaction.updated_at = DateTime.Now;
                        _appDbContext.ApiTransactions.Update(apiTransaction);
                    }

                    if (transactionDetail != null)
                    {
                        transactionDetail.ResultCode = 2;
                        transactionDetail.ResultMessage = $"Error: {ex.Message}";
                        transactionDetail.Status = "Failed";
                        transactionDetail.UpdatedAt = DateTime.Now;
                        _appDbContext.Transaction_detail.Update(transactionDetail);
                    }

                    // Keep initial receipt number for failed transactions
                    if (transactionRecord != null)
                    {
                        transactionRecord.Status = "Failed";
                        _appDbContext.Transactions.Update(transactionRecord);
                    }
                    if (transaction2Record != null)
                    {
                        transaction2Record.Status = "Failed";
                        _appDbContext.Transactions2.Update(transaction2Record);
                    }

                    await _appDbContext.SaveChangesAsync();
                    _logger.LogWarning($"B2C failed but continuing with loan disbursement");
                }
            }
            else
            {
                // B2C not enabled - update transaction records to completed
                if (transactionRecord != null)
                {
                    transactionRecord.Status = "Completed";
                    _appDbContext.Transactions.Update(transactionRecord);
                }
                if (transaction2Record != null)
                {
                    transaction2Record.Status = "Completed";
                    _appDbContext.Transactions2.Update(transaction2Record);
                }
                await _appDbContext.SaveChangesAsync();
            }

            // ============================================================
            // NOW PROCEED WITH MAIN LOAN DISBURSEMENT TRANSACTION
            // ============================================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Proceeding with loan disbursement for {disbursementDto.LoanNo}");

                // Determine the final receipt number to use
                string finalReceiptNumber = b2cPaymentSuccess && !string.IsNullOrEmpty(mpesaTransactionId)
                    ? mpesaTransactionId
                    : initialReceiptNo;

                // Store old values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                decimal oldLoanAmt = loan.LoanAmt ?? 0;
                decimal oldAamount = loan.Aamount ?? 0;
                string oldChequeStatus = existingCheque.Status ?? "";
                decimal? oldChequeAmountIssued = existingCheque.AmountIssued;
                decimal? oldChequeBalance = existingCheque.Balance;

                // Get LoanType for repayment method
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == disbursementDto.CompanyCode);

                // Calculate total interest
                decimal annualInterestRate = loan.Interest ?? 0;
                decimal monthlyInterestRate = (annualInterestRate / 100) / 12;
                int repaymentPeriod = loan.RepayPeriod ?? 12;
                string repayMethod = loan.RepayMethod ?? loanType?.Repaymethod ?? "AMT";

                bool isUpfrontInterest = loan.InterestUpront ?? false;
                decimal totalInterest = 0;
                decimal monthlyPayment = 0;
                decimal totalRepayable = 0;

                if (repayMethod == "AMT")
                {
                    if (monthlyInterestRate > 0)
                    {
                        decimal factor = (decimal)Math.Pow((double)(1 + monthlyInterestRate), repaymentPeriod);
                        monthlyPayment = approvedAmount * monthlyInterestRate * factor / (factor - 1);
                        totalInterest = (monthlyPayment * repaymentPeriod) - approvedAmount;
                    }
                    else
                    {
                        monthlyPayment = approvedAmount / repaymentPeriod;
                        totalInterest = 0;
                    }
                    totalRepayable = approvedAmount + totalInterest;
                }
                else if (repayMethod == "STL")
                {
                    totalInterest = approvedAmount * (annualInterestRate / 100) * (repaymentPeriod / 12m);
                    monthlyPayment = (approvedAmount + totalInterest) / repaymentPeriod;
                    totalRepayable = approvedAmount + totalInterest;
                }
                else if (repayMethod == "RBAL")
                {
                    decimal remainingBalance = approvedAmount;
                    decimal totalMinimumInterest = 0;

                    for (int i = 1; i <= repaymentPeriod; i++)
                    {
                        decimal interestForMonth = remainingBalance * monthlyInterestRate;
                        totalMinimumInterest += interestForMonth;
                    }

                    totalInterest = totalMinimumInterest;
                    monthlyPayment = remainingBalance * monthlyInterestRate;
                    totalRepayable = approvedAmount + totalInterest;
                }

                // Create Block record
                string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = await GetLastBlockHashAsync(),
                    Timestamp = DateTime.Now,
                    Nonce = 0,
                    MerkleRoot = Guid.NewGuid().ToString(),
                    Confirmed = true,
                    CreatedAt = DateTime.Now
                };

                _context.Blocks.Add(block);
                await _context.SaveChangesAsync();

                // Prepare block data
                var blockData = new
                {
                    TransactionType = "LOAN_DISBURSEMENT",
                    LoanNo = disbursementDto.LoanNo,
                    MemberNo = loan.MemberNo,
                    ApprovedAmount = approvedAmount,
                    NetDisbursedAmount = netDisbursedAmount,
                    TotalInterest = totalInterest,
                    MonthlyPayment = monthlyPayment,
                    RepaymentPeriod = repaymentPeriod,
                    RepaymentMethod = repayMethod,
                    DisbursementDate = disbursementDto.DisbursementDate,
                    DisbursementMethod = disbursementDto.DisbursementMethod,
                    SourceBankId = disbursementDto.BankId,
                    GlAccountNo = disbursementDto.GlAccountNo,
                    ChequeNo = existingCheque.ChequeNo,
                    VoucherNo = existingCheque.Voucherno,
                    TransactionNo = baseTransactionNo,
                    ReceiptNo = finalReceiptNumber, // Store final receipt number (M-Pesa code or system code)
                    B2CEnabled = isB2CEnabled,
                    B2CSuccess = b2cPaymentSuccess,
                    B2CConversationId = conversationId,
                    MpesaTransactionId = mpesaTransactionId
                };

                // Record GL transactions
                string loanAssetAccount = existingCheque.LoanAcc ?? loanType?.LoanAcc ?? "LOAN_ASSET_ACCOUNT";
                string sourceAccount = existingCheque.ContraAcc ?? disbursementDto.GlAccountNo ?? "BANK_ACCOUNT";

                var endorsementGLTransactions = await _context.Gltransactions
                    .Where(gl => gl.DocumentNo == existingCheque.Voucherno && gl.Source == "LOAN_ENDORSEMENT")
                    .ToListAsync();

                var totalDeductions = endorsementGLTransactions.Sum(gl => gl.Amount);

                var netDisbursementGL = new Gltransaction
                {
                    TransDate = disbursementDto.DisbursementDate,
                    Amount = netDisbursedAmount,
                    DrAccNo = loanAssetAccount,
                    CrAccNo = sourceAccount,
                    Temp = "DISBURSEMENT",
                    DocumentNo = existingCheque.Voucherno,
                    Source = "LOAN_DISBURSEMENT",
                    CompanyCode = disbursementDto.CompanyCode,
                    TransDescript = $"Loan Disbursement - Net Amount - Loan {disbursementDto.LoanNo}",
                    AuditTime = DateTime.Now,
                    AuditId = disbursementDto.DisbursedBy,
                    Cash = 0,
                    DocPosted = 1,
                    ChequeNo = existingCheque.ChequeNo,
                    Dregard = false,
                    Recon = false,
                    TransactionNo = baseTransactionNo,
                    Module = "LOAN",
                    ReconId = 0,
                    AuditDateTime = DateTime.Now
                };

                _context.Gltransactions.Add(netDisbursementGL);
                await _context.SaveChangesAsync();

                // Create Loanbal record
                var loanbal = new Loanbal
                {
                    LoanNo = disbursementDto.LoanNo,
                    LoanCode = loan.LoanCode ?? "",
                    MemberNo = loan.MemberNo,
                    Balance = approvedAmount,
                    IntrOwed = isUpfrontInterest ? 0 : totalInterest,
                    //IntrOwed = totalInterest,
                    Installments = repaymentPeriod,
                    IntrOwed2 = 0,
                    FirstDate = disbursementDto.DisbursementDate,
                    RepayRate = monthlyPayment,
                    LastDate = disbursementDto.DisbursementDate.AddMonths(repaymentPeriod),
                    Duedate = disbursementDto.DisbursementDate.AddMonths(1),
                    IntrCharged = isUpfrontInterest ? totalInterest : totalInterest,
                   // IntrCharged = totalInterest,
                    Interest = annualInterestRate,
                    Companycode = disbursementDto.CompanyCode,
                    Penalty = 0,
                    RepayRate2 = monthlyPayment,
                    RepayMethod = repayMethod,
                    Cleared = false,
                    AutoCalc = true,
                    IntrAmount = 0,
                    RepayPeriod = repaymentPeriod,
                    Remarks = disbursementDto.Remarks + (isB2CEnabled ? $" | B2C Payment: {(b2cPaymentSuccess ? "Success" : "Failed")} - {b2cResponseMessage}" : ""),
                    AuditId = disbursementDto.DisbursedBy,
                    AuditTime = DateTime.Now,
                    IntBalance = isUpfrontInterest ? 0 : totalInterest,
                    //IntBalance = totalInterest,
                    CategoryCode = null,
                    InterestAccrued = 0,
                    Defaulter = "N",
                    Processdate = DateTime.Now,
                    Receiptno = finalReceiptNumber, // Store final receipt number
                    Cease = "N",
                    Nextduedate = disbursementDto.DisbursementDate.AddMonths(1),
                    TransactionNo = baseTransactionNo,
                    Year = DateTime.Now.Year.ToString(),
                    Month = DateTime.Now.Month.ToString(),
                    RepayMode = 1,
                    Gperiod = null,
                    ApiKey = apiTransaction?.ConversationId,
                    UserName = disbursementDto.DisbursedBy,
                    Run = 0,
                    SerialNo = null,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Loanbal.Add(loanbal);
                await _context.SaveChangesAsync();

                // Update Cheque record
                existingCheque.Status = (isB2CEnabled && !b2cPaymentSuccess) ? "B2C_Pending" : "Disbursed";
                existingCheque.DateIssued = disbursementDto.DisbursementDate;
                existingCheque.AmountIssued = netDisbursedAmount;
                existingCheque.Balance = netDisbursedAmount;
                existingCheque.AuditDateTime = DateTime.Now;
                existingCheque.UserName = disbursementDto.DisbursedBy;
                existingCheque.TransactionNo = baseTransactionNo;
                existingCheque.Voucherno = finalReceiptNumber; // Store final receipt number
                if (apiTransaction != null)
                {
                    existingCheque.ApiKey = apiTransaction.ConversationId;
                }
                _context.Cheques.Update(existingCheque);
                await _context.SaveChangesAsync();

                // Update Loan table
                loan.Status = (int)Status.Disbursed;
                loan.Posted = "ACTIVE";
                loan.Aamount = approvedAmount;
                loan.AuditTime = disbursementDto.DisbursementDate;
                loan.UserName = disbursementDto.DisbursedBy;
                loan.AuditDateTime = DateTime.Now;
                loan.TransactionNo = baseTransactionNo;
                if (apiTransaction != null)
                {
                    loan.ApiKey = apiTransaction.ConversationId;
                }
                _context.Loans.Update(loan);
                await _context.SaveChangesAsync();

                // Create Blockchain Transaction
                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_DISBURSEMENT",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = netDisbursedAmount,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockData),
                    OffChainReferenceId = existingCheque.Voucherno,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update ALL records with BlockchainTxId
                loanbal.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                netDisbursementGL.BlockchainTxId = blockchainTx.TransactionId;
                existingCheque.BlockchainTxId = blockchainTx.TransactionId;

                //if (apiTransaction != null)
                //{
                //    apiTransaction.BlockchainTxId = blockchainTx.TransactionId;
                //    _appDbContext.ApiTransactions.Update(apiTransaction);
                //}
                //if (transactionDetail != null)
                //{
                //    transactionDetail.BlockchainTxId = blockchainTx.TransactionId;
                //    _appDbContext.TransactionDetail.Update(transactionDetail);
                //}
                //if (transactionRecord != null)
                //{
                //    transactionRecord.BlockchainTxId = blockchainTx.TransactionId;
                //    _appDbContext.Transactions.Update(transactionRecord);
                //}
                //if (transaction2Record != null)
                //{
                //    transaction2Record.BlockchainTxId = blockchainTx.TransactionId;
                //    _appDbContext.Transactions2.Update(transaction2Record);
                //}

                await _context.SaveChangesAsync();
                await _appDbContext.SaveChangesAsync();

                // Generate Loan Schedule
                await GenerateLoanScheduleAsync(disbursementDto.LoanNo, approvedAmount, annualInterestRate,
                    repaymentPeriod, disbursementDto.DisbursementDate, disbursementDto.CompanyCode, repayMethod, isUpfrontInterest);

                await transaction.CommitAsync();

                _logger.LogInformation($"Disbursement successful for loan {disbursementDto.LoanNo}");

                string successMessage = $"Loan {loan.LoanNo} disbursed successfully. " +
                    $"Approved: {approvedAmount:C}, Net Disbursed: {netDisbursedAmount:C}, " +
                    $"Total Interest: {totalInterest:C}, Monthly Payment: {monthlyPayment:C}";

                if (isB2CEnabled)
                {
                    if (b2cPaymentSuccess)
                    {
                        successMessage += $" M-Pesa payment of {netDisbursedAmount:C} sent to {memberPhone} successfully. M-Pesa Receipt: {finalReceiptNumber}";
                    }
                    else
                    {
                        successMessage += $" M-Pesa payment failed: {b2cResponseMessage}. Please process manual payment. Reference: {initialReceiptNo}";
                    }
                }

                _logger.LogInformation(successMessage);

                return existingCheque;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error disbursing loan {disbursementDto.LoanNo}");
                throw;
            }
        }

        public async Task GenerateLoanScheduleAsync(string loanNo, decimal principalAmount, decimal interestRate,
            int repaymentPeriod, DateTime disbursementDate, string companyCode, string repayMethod, bool isUpfrontInterest = false)
        {
            try
            {
                _logger.LogInformation($"Generating loan schedule for loan {loanNo} with method {repayMethod}");
                _logger.LogInformation($"Principal: {principalAmount:C}, Interest Rate: {interestRate}%, Period: {repaymentPeriod} months");
                _logger.LogInformation($"Upfront Interest: {isUpfrontInterest}");

                var existingSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .ToListAsync();

                if (existingSchedule.Any())
                {
                    _context.LoanSchedules.RemoveRange(existingSchedule);
                    await _context.SaveChangesAsync();
                }

                decimal monthlyRate = (interestRate / 100) / 12;
                int totalPayments = repaymentPeriod;

                _logger.LogInformation($"Monthly Interest Rate: {monthlyRate:P4}");

                var scheduleEntries = new List<LoanSchedule>();

                // ============================================================
                // IF UPFRONT INTEREST, ALL INTEREST AMOUNTS ARE 0
                // ============================================================
                if (isUpfrontInterest)
                {
                    _logger.LogInformation($"Upfront interest enabled - Generating principal-only schedule");

                    decimal monthlyPrincipal = principalAmount / totalPayments;
                    decimal remainingBalance = principalAmount;

                    for (int i = 1; i <= totalPayments; i++)
                    {
                        decimal principalForMonth = (i == totalPayments) ? remainingBalance : monthlyPrincipal;

                        scheduleEntries.Add(new LoanSchedule
                        {
                            LoanNo = loanNo,
                            CompanyCode = companyCode,
                            InstallmentNo = i,
                            DueDate = disbursementDate.AddMonths(i),
                            PrincipalAmount = principalForMonth,
                            InterestAmount = 0,
                            TotalInstallment = principalForMonth,
                            BalancePrincipal = remainingBalance - principalForMonth,
                            BalanceInterest = 0,
                            BalanceTotal = remainingBalance - principalForMonth,
                            PaidPrincipal = 0,
                            PaidInterest = 0,
                            PaidTotal = 0,
                            OutstandingPrincipal = principalForMonth,
                            OutstandingInterest = 0,
                            OutstandingTotal = principalForMonth,
                            PenaltyAmount = 0,
                            Status = "Pending",
                            IsFlexible = false,
                            MinimumPayment = principalForMonth,
                            DaysOverdue = 0
                        });

                        remainingBalance -= principalForMonth;
                    }
                }
                else
                {
                    // ============================================================
                    // STL: Straight Line (Flat Rate) - Interest on Original Principal
                    // ============================================================
                    if (repayMethod == "STL")
                    {
                        _logger.LogInformation($"STL method - Generating flat rate schedule");

                        decimal monthlyPrincipal = principalAmount / totalPayments;

                        // ✅ IMPORTANT: Interest is calculated on ORIGINAL principal, NOT reducing balance
                        decimal totalInterest = principalAmount * (interestRate / 100) * (totalPayments / 12m);
                        decimal monthlyInterest = totalInterest / totalPayments;
                        decimal totalInstallment = monthlyPrincipal + monthlyInterest;

                        decimal remainingBalance = principalAmount;
                        decimal remainingInterest = totalInterest;

                        for (int i = 1; i <= totalPayments; i++)
                        {
                            decimal principalBalance = remainingBalance - monthlyPrincipal;
                            decimal interestBalance = remainingInterest - monthlyInterest;
                            decimal totalBalance = principalBalance + interestBalance;

                            scheduleEntries.Add(new LoanSchedule
                            {
                                LoanNo = loanNo,
                                CompanyCode = companyCode,
                                InstallmentNo = i,
                                DueDate = disbursementDate.AddMonths(i),
                                PrincipalAmount = monthlyPrincipal,
                                InterestAmount = monthlyInterest,
                                TotalInstallment = totalInstallment,
                                BalancePrincipal = principalBalance,
                                BalanceInterest = interestBalance,
                                BalanceTotal = totalBalance,
                                PaidPrincipal = 0,
                                PaidInterest = 0,
                                PaidTotal = 0,
                                OutstandingPrincipal = monthlyPrincipal,
                                OutstandingInterest = monthlyInterest,
                                OutstandingTotal = totalInstallment,
                                PenaltyAmount = 0,
                                Status = "Pending",
                                IsFlexible = false,
                                MinimumPayment = totalInstallment,
                                DaysOverdue = 0
                            });

                            remainingBalance -= monthlyPrincipal;
                            remainingInterest -= monthlyInterest;
                        }

                        _logger.LogInformation($"STL schedule generated: Monthly Principal={monthlyPrincipal:C}, Monthly Interest={monthlyInterest:C}, Total Payment={totalInstallment:C}");
                    }

                    // ============================================================
                    // AMT: Amortized (Equal Monthly Installments) - Reducing Balance
                    // ============================================================
                    else if (repayMethod == "AMT")
                    {
                        _logger.LogInformation($"AMT method - Generating amortized schedule");

                        decimal monthlyPayment;
                        if (monthlyRate > 0)
                        {
                            double factor = Math.Pow((double)(1 + monthlyRate), totalPayments);
                            monthlyPayment = principalAmount * monthlyRate * (decimal)factor / ((decimal)factor - 1);
                        }
                        else
                        {
                            monthlyPayment = principalAmount / totalPayments;
                        }

                        decimal remainingBalance = principalAmount;
                        decimal totalInterestAccumulated = 0;

                        for (int i = 1; i <= totalPayments; i++)
                        {
                            decimal interestAmount = remainingBalance * monthlyRate;
                            decimal principalAmountPayment = monthlyPayment - interestAmount;
                            totalInterestAccumulated += interestAmount;

                            // For the last payment, adjust to clear remaining balance
                            if (i == totalPayments)
                            {
                                principalAmountPayment = remainingBalance;
                                monthlyPayment = principalAmountPayment + interestAmount;
                            }

                            decimal balancePrincipal = remainingBalance - principalAmountPayment;
                            decimal balanceInterest = totalInterestAccumulated - interestAmount;
                            decimal balanceTotal = balancePrincipal + balanceInterest;

                            scheduleEntries.Add(new LoanSchedule
                            {
                                LoanNo = loanNo,
                                CompanyCode = companyCode,
                                InstallmentNo = i,
                                DueDate = disbursementDate.AddMonths(i),
                                PrincipalAmount = principalAmountPayment,
                                InterestAmount = interestAmount,
                                TotalInstallment = monthlyPayment,
                                BalancePrincipal = balancePrincipal,
                                BalanceInterest = balanceInterest,
                                BalanceTotal = balanceTotal,
                                PaidPrincipal = 0,
                                PaidInterest = 0,
                                PaidTotal = 0,
                                OutstandingPrincipal = principalAmountPayment,
                                OutstandingInterest = interestAmount,
                                OutstandingTotal = monthlyPayment,
                                PenaltyAmount = 0,
                                Status = "Pending",
                                IsFlexible = false,
                                MinimumPayment = monthlyPayment,
                                DaysOverdue = 0
                            });

                            remainingBalance -= principalAmountPayment;

                            _logger.LogInformation($"AMT Month {i}: Principal={principalAmountPayment:C}, Interest={interestAmount:C}, Total={monthlyPayment:C}, Balance={balancePrincipal:C}");
                        }
                    }
                    else if (repayMethod == "RBAL")
                    {
                        _logger.LogInformation($"RBAL method - Fixed principal with reducing interest");

                        decimal remainingBalance = principalAmount;
                        decimal totalInterestAccumulated = 0;

                        // ✅ RBAL: Fixed principal payment each period
                        decimal principalPerPeriod = principalAmount / totalPayments;

                        for (int i = 1; i <= totalPayments; i++)
                        {
                            // ✅ Calculate interest on the current remaining balance
                            decimal interestAmount = remainingBalance * monthlyRate;
                            totalInterestAccumulated += interestAmount;

                            // ✅ Total payment = Fixed Principal + Interest
                            decimal totalPayment = principalPerPeriod + interestAmount;

                            // ✅ For the last payment, adjust to clear any rounding differences
                            decimal principalPayment = principalPerPeriod;
                            if (i == totalPayments)
                            {
                                principalPayment = remainingBalance;  // Pay off remaining principal
                                totalPayment = principalPayment + interestAmount;
                            }

                            // Calculate balance after this payment
                            decimal balanceAfterPrincipal = remainingBalance - principalPayment;
                            decimal balanceAfterInterest = 0; // Interest is fully paid each period

                            // ✅ Fix: Calculate correct outstanding amounts
                            decimal outstandingPrincipal = Math.Max(0, balanceAfterPrincipal);
                            decimal outstandingInterest = 0; // All interest is paid each period in RBAL
                            decimal outstandingTotal = outstandingPrincipal + outstandingInterest;

                            // Add penalty if overdue (handled separately)
                            decimal penaltyAmount = 0;

                            scheduleEntries.Add(new LoanSchedule
                            {
                                LoanNo = loanNo,
                                CompanyCode = companyCode,
                                InstallmentNo = i,
                                DueDate = disbursementDate.AddMonths(i),

                                // ✅ FIXED: Principal amount being paid in this installment
                                PrincipalAmount = principalPayment,

                                // ✅ Interest amount for this period
                                InterestAmount = interestAmount,

                                // ✅ Total installment = Principal + Interest
                                TotalInstallment = totalPayment,

                                // ✅ Balance after this payment (Principal remaining)
                                BalancePrincipal = Math.Max(0, balanceAfterPrincipal),

                                // ✅ Interest balance (should be 0 as interest is paid each period)
                                BalanceInterest = 0,

                                // ✅ Total outstanding balance = Principal remaining
                                BalanceTotal = Math.Max(0, balanceAfterPrincipal),

                                // Paid amounts (initially 0, updated when payments are made)
                                PaidPrincipal = 0,
                                PaidInterest = 0,
                                PaidTotal = 0,

                                // Outstanding amounts before payment
                                OutstandingPrincipal = remainingBalance,
                                OutstandingInterest = interestAmount,
                                OutstandingTotal = remainingBalance + interestAmount,

                                PenaltyAmount = 0,
                                Status = "Pending",
                                IsFlexible = false,  // RBAL has fixed principal payments
                                MinimumPayment = totalPayment,  // Minimum is the full installment
                                DaysOverdue = 0
                            });

                            // ✅ Update remaining balance for next period
                            remainingBalance = Math.Max(0, balanceAfterPrincipal);

                            _logger.LogInformation($"RBAL Month {i}: Principal={principalPayment:C}, Interest={interestAmount:C}, Total={totalPayment:C}, Remaining={remainingBalance:C}");
                        }

                        _logger.LogInformation($"RBAL schedule generated: Total Principal={principalAmount:C}, Total Interest={totalInterestAccumulated:C}, Total Repayable={principalAmount + totalInterestAccumulated:C}");
                    }

                    //else if (repayMethod == "RBAL")
                    //{
                    //    _logger.LogInformation($"RBAL method - Generating interest-only minimum schedule");

                    //    decimal remainingBalance = principalAmount;  // Principal stays the same in RBAL
                    //    decimal totalInterestAccumulated = 0;

                    //    for (int i = 1; i <= totalPayments; i++)
                    //    {
                    //        // ✅ RBAL: Interest is calculated on current balance
                    //        decimal interestAmount = remainingBalance * monthlyRate;
                    //        totalInterestAccumulated += interestAmount;

                    //        // ✅ RBAL: NO mandatory principal - minimum payment is interest only
                    //        // The member can optionally pay extra principal

                    //        scheduleEntries.Add(new LoanSchedule
                    //        {
                    //            LoanNo = loanNo,
                    //            CompanyCode = companyCode,
                    //            InstallmentNo = i,
                    //            DueDate = disbursementDate.AddMonths(i),
                    //            PrincipalAmount = 0,  // ← ZERO - no mandatory principal
                    //            InterestAmount = interestAmount,
                    //            TotalInstallment = interestAmount,  // ← Minimum payment is interest only
                    //            BalancePrincipal = remainingBalance,  // ← Principal stays the same
                    //            BalanceInterest = 0,
                    //            BalanceTotal = remainingBalance,  // ← Outstanding is the principal
                    //            PaidPrincipal = 0,
                    //            PaidInterest = 0,
                    //            PaidTotal = 0,
                    //            OutstandingPrincipal = 0,
                    //            OutstandingInterest = interestAmount,
                    //            OutstandingTotal = interestAmount,
                    //            PenaltyAmount = 0,
                    //            Status = "Pending",
                    //            IsFlexible = true,  // ← Flexible - can pay extra principal
                    //            MinimumPayment = interestAmount,  // ← Minimum is interest only
                    //            DaysOverdue = 0
                    //        });

                    //        _logger.LogInformation($"RBAL Month {i}: Interest={interestAmount:C}, Balance={remainingBalance:C}");
                    //    }

                    //    _logger.LogInformation($"RBAL schedule generated: Total Interest={totalInterestAccumulated:C}, Principal remains={principalAmount:C}");
                    //}
                }

                await _context.LoanSchedules.AddRangeAsync(scheduleEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Generated {scheduleEntries.Count} schedule entries for loan {loanNo} (Upfront Interest: {isUpfrontInterest})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating loan schedule for loan {loanNo}");
                throw;
            }
        }



        private async Task<string> GetLastBlockHashAsync()
        {
            try
            {
                var lastBlock = await _context.Blocks
                    .OrderByDescending(b => b.BlockId)
                    .FirstOrDefaultAsync();

                return lastBlock?.BlockHash ?? "0".PadLeft(64, '0');
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting last block hash, using default");
                return "0".PadLeft(64, '0');
            }
        }

        public async Task<Cheque> GetLoanDisbursementAsync(string loanNo)
        {
            return await _context.Cheques
                .FirstOrDefaultAsync(c => c.LoanNo == loanNo);
        }

        public async Task<Loanbal> GetLoanBalanceAsync(string loanNo)
        {
            return await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);
        }

        #endregion


        #region Repayments
        public async Task<Repay> ProcessRepaymentAsync(LoanRepaymentDTO repaymentDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Processing repayment for loan {repaymentDto.LoanNo}, Amount: {repaymentDto.AmountPaid:C}");

                // 1. GET LOAN DATA
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == repaymentDto.LoanNo && l.CompanyCode == repaymentDto.CompanyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {repaymentDto.LoanNo} not found");

                // ============================================================
                // GET REPAYMENT METHOD - DIRECTLY FROM LOANS TABLE
                // ============================================================
                string repayMethod = (loan.RepayMethod ?? "AMT").ToUpper();

                bool isRBAL = repayMethod == "RBAL";
                bool isAMT = repayMethod == "AMT";
                bool isSTL = repayMethod == "STL";

                _logger.LogInformation($"Loan Repayment Method: {repayMethod}");

                // Store old loan values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                decimal oldLoanAamount = loan.Aamount ?? 0;

                if (loan.Status != (int)Status.Disbursed && loan.Status != (int)Status.Endorsed)
                    throw new InvalidOperationException($"Cannot process repayment for loan in status '{loan.Status}'");

                // 2. GET LOAN BALANCE
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == repaymentDto.LoanNo && lb.Companycode == repaymentDto.CompanyCode);

                if (loanbal == null)
                    throw new InvalidOperationException($"Loan balance record not found");

                // ============================================================
                // VALIDATE PAYMENT AMOUNT AGAINST OUTSTANDING BALANCE
                // ============================================================
                decimal totalOutstandingBalance = loanbal.Balance + loanbal.IntrOwed + loanbal.Penalty;

                if (repaymentDto.AmountPaid > totalOutstandingBalance + 0.01m) // Allow 0.01 rounding tolerance
                {
                    throw new InvalidOperationException(
                        $"Payment amount ({repaymentDto.AmountPaid:C}) exceeds total outstanding balance ({totalOutstandingBalance:C}). " +
                        $"Please enter an amount less than or equal to the outstanding balance.");
                }

                // Store old loanbal values for audit
                decimal oldBalance = loanbal.Balance;
                decimal oldIntrOwed = loanbal.IntrOwed;
                decimal oldPenalty = loanbal.Penalty;
                decimal oldIntBalance = loanbal.IntBalance;

                // 3. GET ALL REMAINING SCHEDULES
                var remainingSchedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();

                if (!remainingSchedules.Any())
                {
                    if (loanbal.Balance <= 0.01m && loanbal.IntrOwed <= 0.01m)
                    {
                        throw new InvalidOperationException("Loan is already fully paid.");
                    }
                    throw new InvalidOperationException("No active schedule found for this loan.");
                }

                // Get the current schedule (first unpaid)
                var currentSchedule = remainingSchedules.First();

                // Store old schedule values for audit
                decimal oldScheduleOutstandingPrincipal = currentSchedule.OutstandingPrincipal;
                decimal oldScheduleOutstandingInterest = currentSchedule.OutstandingInterest;
                decimal oldScheduleOutstandingTotal = currentSchedule.OutstandingTotal;
                string oldScheduleStatus = currentSchedule.Status;

                _logger.LogInformation($"Current Schedule - Installment {currentSchedule.InstallmentNo}, " +
                    $"Outstanding Principal: {currentSchedule.OutstandingPrincipal:C}, " +
                    $"Outstanding Interest: {currentSchedule.OutstandingInterest:C}");

                // ============================================================
                // GET PENALTY CONFIGURATION - DIRECTLY FROM LOANS TABLE
                // ============================================================
                // These fields should exist in the Loans table:
                // Penalty (bool/int) - whether penalty applies
                // Gperiod (string) - grace period in days
                // PenaltyMode (string) - "Percentage" or "Fixed"
                // PenaltyRate (string) - "Daily", "Weekly", "Monthly", "Yearly"
                // PenaltyValue (decimal) - penalty percentage or fixed amount
                // PenaltyChargeItem (short) - 0=Principal, 1=Interest, 2=Both

                bool attractsPenalty = loan.PenaltyValue == 1; // Assuming Penalty is int/byte
                string penaltyMode = !string.IsNullOrEmpty(loan.PenaltyMode) ? loan.PenaltyMode : "Percentage";
                string penaltyRateType = !string.IsNullOrEmpty(loan.PenaltyRate) ? loan.PenaltyRate : "Monthly";
                decimal penaltyValue = loan.PenaltyValue ?? 0;
                short penaltyChargeItem = loan.PenaltyChargeItem ?? 0;
                int gracePeriodDays = loan.Gperiod ?? 0;

                _logger.LogInformation($"Penalty Configuration from Loans table: AttractsPenalty={attractsPenalty}, Mode={penaltyMode}, Rate={penaltyRateType}, Value={penaltyValue}, ChargeItem={penaltyChargeItem}, GracePeriod={gracePeriodDays}");

                // 4. CALCULATE TOTAL REMAINING BALANCE (for early settlement detection)
                decimal totalRemainingPrincipal = loanbal.Balance;
                decimal totalRemainingInterest = loanbal.IntrOwed;
                decimal totalRemainingPenalty = loanbal.Penalty;
                decimal totalFullBalance = totalRemainingPrincipal + totalRemainingInterest + totalRemainingPenalty;

                // ============================================================
                // VALIDATE: Amount cannot exceed total outstanding balance
                // ============================================================
                if (repaymentDto.AmountPaid > totalFullBalance + 0.01m)
                {
                    throw new InvalidOperationException(
                        $"Payment amount ({repaymentDto.AmountPaid:C}) exceeds total outstanding balance ({totalFullBalance:C}).");
                }

                // 5. CHECK IF THIS IS AN EARLY FULL SETTLEMENT
                bool isEarlyFullSettlement = repaymentDto.AmountPaid >= totalFullBalance - 0.01m;

                decimal penaltyAllocated = 0;
                decimal interestAllocated = 0;
                decimal principalAllocated = 0;
                decimal overpaymentAmount = 0;

                if (isEarlyFullSettlement)
                {
                    _logger.LogInformation($"EARLY FULL SETTLEMENT detected! Amount: {repaymentDto.AmountPaid:C}, Total Due: {totalFullBalance:C}");

                    // ALLOCATE FULL BALANCE
                    penaltyAllocated = totalRemainingPenalty;
                    interestAllocated = totalRemainingInterest;
                    principalAllocated = totalRemainingPrincipal;
                    overpaymentAmount = repaymentDto.AmountPaid - totalFullBalance;

                    _logger.LogInformation($"Full Settlement Allocation: Principal={principalAllocated:C}, Interest={interestAllocated:C}, Penalty={penaltyAllocated:C}, Overpayment={overpaymentAmount:C}");
                }
                else
                {
                    // Regular installment payment - calculate penalty if configured
                    decimal penaltyAmount = 0;
                    int daysOverdue = 0;

                    // ============================================================
                    // CALCULATE PENALTY BASED ON LOANS TABLE CONFIGURATION
                    // ============================================================
                    if (attractsPenalty && repaymentDto.PaymentDate > currentSchedule.DueDate)
                    {
                        daysOverdue = (repaymentDto.PaymentDate - currentSchedule.DueDate).Days;

                        if (daysOverdue > gracePeriodDays)
                        {
                            int overdueDaysAfterGrace = daysOverdue - gracePeriodDays;

                            // Calculate number of penalty periods based on Rate type
                            int numberOfPeriods = 1;
                            switch (penaltyRateType?.ToLower())
                            {
                                case "daily":
                                    numberOfPeriods = overdueDaysAfterGrace;
                                    break;
                                case "weekly":
                                    numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 7.0);
                                    break;
                                case "monthly":
                                    numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);
                                    break;
                                case "yearly":
                                    numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 365.0);
                                    break;
                                default:
                                    numberOfPeriods = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);
                                    break;
                            }

                            // Determine what amount to charge penalty on (ChargeItem)
                            decimal penaltyBaseAmount = 0;
                            switch (penaltyChargeItem)
                            {
                                case 0: // Principal Only
                                    penaltyBaseAmount = currentSchedule.OutstandingPrincipal;
                                    break;
                                case 1: // Interest Only
                                    penaltyBaseAmount = currentSchedule.OutstandingInterest;
                                    break;
                                case 2: // Both Principal & Interest
                                default:
                                    penaltyBaseAmount = currentSchedule.OutstandingTotal;
                                    break;
                            }

                            if (penaltyMode?.ToLower() == "percentage")
                            {
                                // PERCENTAGE MODE: Value is percentage rate
                                penaltyAmount = penaltyBaseAmount * (penaltyValue / 100) * numberOfPeriods;
                                _logger.LogInformation($"Penalty calculated (Percentage): Base={penaltyBaseAmount:C}, Rate={penaltyValue}%, Periods={numberOfPeriods}, Penalty={penaltyAmount:C}");
                            }
                            else if (penaltyMode?.ToLower() == "fixed")
                            {
                                // FIXED AMOUNT MODE: Value is fixed amount per period
                                penaltyAmount = penaltyValue * numberOfPeriods;

                                // Cap penalty at 50% of the base amount as a reasonable limit
                                decimal maxPenalty = penaltyBaseAmount * 0.5m;
                                if (penaltyAmount > maxPenalty)
                                {
                                    penaltyAmount = maxPenalty;
                                    _logger.LogInformation($"Penalty capped at 50% of base: {maxPenalty:C}");
                                }
                                _logger.LogInformation($"Penalty calculated (Fixed): Fixed={penaltyValue:C}, Periods={numberOfPeriods}, Penalty={penaltyAmount:C}");
                            }

                            _logger.LogInformation($"Final Penalty Amount: {penaltyAmount:C} (Days Overdue: {daysOverdue}, Grace Period: {gracePeriodDays})");
                        }
                    }

                    decimal remainingAmount = repaymentDto.AmountPaid;

                    // ============================================================
                    // RBAL REPAYMENT METHOD
                    // ============================================================
                    if (isRBAL)
                    {
                        decimal minimumRequiredPayment =
                            currentSchedule.MinimumPayment > 0
                            ? currentSchedule.MinimumPayment.Value
                            : currentSchedule.OutstandingInterest;

                        if (remainingAmount < minimumRequiredPayment - 0.01m)
                        {
                            throw new InvalidOperationException($"RBAL loan requires at least {minimumRequiredPayment:C} interest payment.");
                        }

                        // Apply penalty first
                        if (remainingAmount > 0 && penaltyAmount > 0)
                        {
                            penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
                            remainingAmount -= penaltyAllocated;
                        }

                        // Apply interest
                        if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
                        {
                            interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
                            remainingAmount -= interestAllocated;
                        }

                        // Optional principal reduction
                        if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
                        {
                            principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
                            remainingAmount -= principalAllocated;

                            if (principalAllocated > 0)
                            {
                                decimal newBalance = loanbal.Balance - principalAllocated;
                                await RecalculateRbalScheduleAsync(repaymentDto.LoanNo, newBalance);
                                _logger.LogInformation($"RBAL future schedules recalculated. New balance: {newBalance:C}");
                            }
                        }

                        overpaymentAmount = remainingAmount;
                    }

                    // ============================================================
                    // STL REPAYMENT METHOD
                    // ============================================================
                    else if (isSTL)
                    {
                        // Apply penalty first
                        if (remainingAmount > 0 && penaltyAmount > 0)
                        {
                            penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
                            remainingAmount -= penaltyAllocated;
                        }

                        // Apply interest
                        if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
                        {
                            interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
                            remainingAmount -= interestAllocated;
                        }

                        // Apply principal
                        if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
                        {
                            principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
                            remainingAmount -= principalAllocated;
                        }

                        overpaymentAmount = remainingAmount;
                    }

                    // ============================================================
                    // AMT REPAYMENT METHOD
                    // ============================================================
                    else
                    {
                        // Apply penalty first
                        if (remainingAmount > 0 && penaltyAmount > 0)
                        {
                            penaltyAllocated = Math.Min(remainingAmount, penaltyAmount);
                            remainingAmount -= penaltyAllocated;
                        }

                        // Apply interest
                        if (remainingAmount > 0 && currentSchedule.OutstandingInterest > 0)
                        {
                            interestAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingInterest);
                            remainingAmount -= interestAllocated;
                        }

                        // Apply principal
                        if (remainingAmount > 0 && currentSchedule.OutstandingPrincipal > 0)
                        {
                            principalAllocated = Math.Min(remainingAmount, currentSchedule.OutstandingPrincipal);
                            remainingAmount -= principalAllocated;
                        }

                        overpaymentAmount = remainingAmount;
                    }
                }

                // ============================================================
                // FINAL VALIDATION: Ensure we don't allocate more than paid
                // ============================================================
                decimal totalAllocated = penaltyAllocated + interestAllocated + principalAllocated;
                if (totalAllocated > repaymentDto.AmountPaid + 0.01m)
                {
                    _logger.LogWarning($"Allocation total ({totalAllocated:C}) exceeds payment amount ({repaymentDto.AmountPaid:C}). Adjusting...");
                    // Adjust principal to match
                    decimal adjustment = repaymentDto.AmountPaid - (penaltyAllocated + interestAllocated);
                    if (adjustment < 0)
                    {
                        // Reduce interest first, then principal
                        decimal interestReduction = Math.Min(interestAllocated, Math.Abs(adjustment));
                        interestAllocated -= interestReduction;
                        adjustment += interestReduction;

                        if (adjustment < 0)
                        {
                            principalAllocated += adjustment; 
                        }
                    }
                    else
                    {
                        principalAllocated += adjustment;
                    }
                    // Ensure no negative values
                    principalAllocated = Math.Max(0, principalAllocated);
                    interestAllocated = Math.Max(0, interestAllocated);
                    penaltyAllocated = Math.Max(0, penaltyAllocated);
                }

                // 6. GENERATE NUMBERS
                string receiptNo = await GenerateReceiptNumberAsync(repaymentDto.CompanyCode);
                string transactionNo = $"REP{DateTime.Now:yyyyMMddHHmmssfff}";
                int repaymentCount = await _context.Repay.CountAsync(r => r.LoanNo == repaymentDto.LoanNo && r.Posted == true);
                int paymentNo = repaymentCount + 1;

                // 7. CREATE REPAY RECORD
                string remarksText = $"{repaymentDto.Remarks ?? ""}" +
                    (overpaymentAmount > 0.01m ? $" (Overpayment: KES {overpaymentAmount:N2})" : "") +
                    (isEarlyFullSettlement ? " - EARLY FULL SETTLEMENT" : "") +
                    (penaltyAllocated > 0.01m ? $" - PENALTY: KES {penaltyAllocated:N2} ({penaltyMode} at {penaltyValue}{(penaltyMode == "Percentage" ? "%" : " KES")} per {penaltyRateType})" : "");

                // Calculate breakdown for remarks
                string breakdownText = $"Breakdown: Principal KES {principalAllocated:N2}, Interest KES {interestAllocated:N2}, Penalty KES {penaltyAllocated:N2}";
                remarksText = $"{remarksText}\n{breakdownText}";

                var repayment = new Repay
                {
                    LoanNo = repaymentDto.LoanNo,
                    MemberNo = repaymentDto.MemberNo,
                    CompanyCode = repaymentDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    PaymentNo = paymentNo,
                    DateReceived = repaymentDto.PaymentDate,
                    Amount = repaymentDto.AmountPaid,
                    Principal = principalAllocated,
                    Interest = interestAllocated,
                    Penalty = penaltyAllocated,
                    IntrCharged = interestAllocated,
                    IntrOwed = Math.Max(0, loanbal.IntrOwed - interestAllocated),
                    IntrAccrued = currentSchedule.InterestAmount,
                    LoanBalance = Math.Max(0, loanbal.Balance - principalAllocated),
                    RepayRate = currentSchedule.TotalInstallment,
                    Locked = false,
                    Posted = true,
                    Accrued = true,
                    Remarks = remarksText,
                    AuditId = repaymentDto.ReceivedBy,
                    AuditTime = DateTime.Now,
                    Transby = repaymentDto.ReceivedBy,
                    IntBalance = Math.Max(0, loanbal.IntBalance - interestAllocated),
                    Loancode = loan.LoanCode,
                    Interestaccrued = currentSchedule.InterestAmount,
                    Transno = transactionNo,
                    TransDate = repaymentDto.PaymentDate,
                    TransactionNo = transactionNo,
                    ApiKey = repaymentDto.ReferenceNo,
                    UserName = repaymentDto.ReceivedBy,
                    Run = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Repay.Add(repayment);
                await _context.SaveChangesAsync();

                // 8. UPDATE ALL REMAINING SCHEDULES
                if (isEarlyFullSettlement)
                {
                    // Mark ALL remaining schedules as PAID
                    foreach (var schedule in remainingSchedules)
                    {
                        schedule.PaidPrincipal = schedule.PrincipalAmount;
                        schedule.PaidInterest = schedule.InterestAmount;
                        schedule.PaidTotal = schedule.TotalInstallment;
                        schedule.OutstandingPrincipal = 0;
                        schedule.OutstandingInterest = 0;
                        schedule.OutstandingTotal = 0;
                        schedule.Status = "Paid";
                        schedule.PaidDate = repaymentDto.PaymentDate;
                        schedule.PenaltyAmount = schedule.PenaltyAmount + (schedule == currentSchedule ? penaltyAllocated : 0);

                        _logger.LogInformation($"Schedule {schedule.InstallmentNo} marked as PAID (early settlement)");
                    }
                }
                else
                {
                    // Update only the current schedule (regular payment)
                    bool isCurrentInstallmentFullyPaid;

                    if (isRBAL)
                    {
                        isCurrentInstallmentFullyPaid = interestAllocated >= currentSchedule.OutstandingInterest - 0.01m;
                    }
                    else
                    {
                        isCurrentInstallmentFullyPaid = (principalAllocated >= currentSchedule.OutstandingPrincipal - 0.01m) &&
                                                        (interestAllocated >= currentSchedule.OutstandingInterest - 0.01m);
                    }

                    if (isCurrentInstallmentFullyPaid)
                    {
                        currentSchedule.PaidPrincipal = currentSchedule.PrincipalAmount;
                        currentSchedule.PaidInterest = currentSchedule.InterestAmount;
                        currentSchedule.PaidTotal = currentSchedule.TotalInstallment;
                        currentSchedule.OutstandingPrincipal = 0;
                        currentSchedule.OutstandingInterest = 0;
                        currentSchedule.OutstandingTotal = 0;
                        currentSchedule.Status = "Paid";
                        currentSchedule.PaidDate = repaymentDto.PaymentDate;
                        currentSchedule.PenaltyAmount = currentSchedule.PenaltyAmount + penaltyAllocated;

                        _logger.LogInformation($"Installment {currentSchedule.InstallmentNo} marked as PAID");
                    }
                    else
                    {
                        currentSchedule.PaidPrincipal += principalAllocated;
                        currentSchedule.PaidInterest += interestAllocated;
                        currentSchedule.PaidTotal = currentSchedule.PaidPrincipal + currentSchedule.PaidInterest;
                        currentSchedule.OutstandingPrincipal = currentSchedule.PrincipalAmount - currentSchedule.PaidPrincipal;
                        currentSchedule.OutstandingInterest = currentSchedule.InterestAmount - currentSchedule.PaidInterest;
                        currentSchedule.OutstandingTotal = currentSchedule.OutstandingPrincipal + currentSchedule.OutstandingInterest;
                        currentSchedule.Status = "Partial";
                        currentSchedule.PenaltyAmount = currentSchedule.PenaltyAmount + penaltyAllocated;

                        _logger.LogInformation($"Installment {currentSchedule.InstallmentNo} marked as PARTIAL");
                    }
                }

                // 9. UPDATE LOANBAL RECORD
                loanbal.Balance = Math.Max(0, loanbal.Balance - principalAllocated);
                loanbal.IntrOwed = Math.Max(0, loanbal.IntrOwed - interestAllocated);
                loanbal.Penalty = Math.Max(0, loanbal.Penalty - penaltyAllocated);
                loanbal.IntBalance = Math.Max(0, loanbal.IntBalance - interestAllocated);
                loanbal.LastDate = repaymentDto.PaymentDate;
                loanbal.Processdate = DateTime.Now;

                // Update next due date
                if (!isEarlyFullSettlement)
                {
                    var nextSchedule = await _context.LoanSchedules
                        .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                        .OrderBy(s => s.InstallmentNo)
                        .FirstOrDefaultAsync();

                    if (nextSchedule != null)
                    {
                        loanbal.Nextduedate = nextSchedule.DueDate;
                        loanbal.Duedate = nextSchedule.DueDate;
                        loanbal.RepayRate = nextSchedule.TotalInstallment;
                    }
                    else if (loanbal.Balance > 0.01m || loanbal.IntrOwed > 0.01m)
                    {
                        loanbal.Nextduedate = repaymentDto.PaymentDate.AddMonths(1);
                        loanbal.Duedate = repaymentDto.PaymentDate.AddMonths(1);
                    }
                }


                // ============================================================
                // ✅ INSERT PROGRESSIVE GUARANTOR RELEASE HERE
                // ============================================================
                // Store principal before for progressive release calculation
                decimal principalBefore = oldBalance; // oldBalance captured earlier
                decimal principalAfter = loanbal.Balance;

                // Release guarantors proportionally based on principal reduction
                if (principalAfter < principalBefore && principalBefore > 0)
                {
                    await ReleaseGuarantorsProportionallyAsync(
                        repaymentDto.LoanNo,
                        principalBefore,
                        principalAfter,
                        repaymentDto.ReceivedBy
                    );
                }

                // 10. CHECK IF LOAN IS FULLY PAID
                bool isFullyPaid = loanbal.Balance <= 0.01m && loanbal.IntrOwed <= 0.01m && loanbal.Penalty <= 0.01m;

                if (isFullyPaid)
                {
                    loanbal.Cleared = true;
                    loan.Status = (int)Status.Closed;
                    loan.Posted = "Closed";
                    loan.Aamount = 0;

                    await ReleaseCollateralGuaranteesForLoanAsync(repaymentDto.LoanNo, repaymentDto.ReceivedBy);
                    await ReleaseMemberGuarantorsForLoanAsync(repaymentDto.LoanNo, repaymentDto.ReceivedBy);

                    _logger.LogInformation($"Loan {loan.LoanNo} fully paid and closed. Released collateral and member guarantors.");
                }
                else if (loan.Status == (int)Status.Disbursed)
                {
                    loan.Status = (int)Status.Endorsed;
                    loan.Posted = "Active";
                }

                loan.Aamount = loanbal.Balance;

                if (string.IsNullOrEmpty(loan.TransactionNo))
                {
                    loan.TransactionNo = transactionNo;
                }

                await _context.SaveChangesAsync();


                // 11. CREATE GL TRANSACTIONS 
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == repaymentDto.CompanyCode);

                if (loanType == null)
                {
                    throw new InvalidOperationException($"Loan type not found for loan code: {loan.LoanCode}");
                }

                // ✅ Get the Bank GL Account from the Banks table (just like disbursement)
                var bank = await _context.Banks
                    .FirstOrDefaultAsync(b => b.CompanyCode == repaymentDto.CompanyCode && b.IsActive == true);

                if (bank == null || string.IsNullOrEmpty(bank.GlAccountNo))
                {
                    throw new InvalidOperationException($"No active bank with GL Account found for company: {repaymentDto.CompanyCode}. Please configure a bank with GL Account first.");
                }

                string bankAccount = bank.GlAccountNo;
                _logger.LogInformation($"Bank GL Account found: {bankAccount} from bank {bank.BankName}");

                // ✅ Get accounts from Loantype
                string loanReceivableAccount = loanType?.LoanAcc ?? "LOAN_RECEIVABLE_ACCOUNT";      // Dr: Loan Receivable (for interest & penalty) / Cr: Loan Receivable (for payment)
                string interestIncomeAccount = loanType?.InterestAcc ?? "INTEREST_INCOME_ACCOUNT";   // Cr: Interest Income
                string penaltyIncomeAccount = loanType?.PenaltyAcc ?? "PENALTY_INCOME_ACCOUNT";     // Cr: Penalty Income
                string overpaymentLiabilityAccount = loanType?.OverpaymentAcc ?? "OVERPAYMENT_LIABILITY_ACCOUNT"; // Cr: Overpayment

                _logger.LogInformation($"GL Accounts - Bank: {bankAccount}, LoanReceivable: {loanReceivableAccount}, Interest: {interestIncomeAccount}, Penalty: {penaltyIncomeAccount}");

                // ✅ Create a list to track all GL transactions
                List<Gltransaction> createdGLTransactions = new List<Gltransaction>();

                // ============================================================
                // 1. GL TRANSACTION FOR TOTAL PAYMENT (Dr: Bank, Cr: Loan Receivable)
                // This records the full payment received
                // ============================================================
                if (repaymentDto.AmountPaid > 0.01m)
                {
                    var paymentGL = new Gltransaction
                    {
                        TransDate = repaymentDto.PaymentDate,
                        Amount = repaymentDto.AmountPaid,           // Total amount paid
                        DrAccNo = bankAccount,                      // Debit: Bank Account
                        CrAccNo = loanReceivableAccount,            // Credit: Loan Receivable Account
                        Temp = "REPAYMENT_PAYMENT",
                        DocumentNo = receiptNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = repaymentDto.CompanyCode,
                        TransDescript = $"Loan repayment payment for loan {loan.LoanNo} - Receipt: {receiptNo}",
                        AuditTime = DateTime.Now,
                        AuditId = repaymentDto.ReceivedBy,
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repaymentDto.ReferenceNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };

                    _context.Gltransactions.Add(paymentGL);
                    createdGLTransactions.Add(paymentGL);
                    _logger.LogInformation($"Payment GL Entry: Dr {bankAccount}, Cr {loanReceivableAccount}, Amount: {repaymentDto.AmountPaid:C}");
                }

                // ============================================================
                // 2. GL TRANSACTION FOR INTEREST (Dr: Loan Receivable, Cr: Interest Income)
                // This recognizes interest income and reduces the loan receivable
                // ============================================================
                if (interestAllocated > 0.01m)
                {
                    var interestGL = new Gltransaction
                    {
                        TransDate = repaymentDto.PaymentDate,
                        Amount = interestAllocated,
                        DrAccNo = loanReceivableAccount,            // Debit: Loan Receivable Account
                        CrAccNo = interestIncomeAccount,            // Credit: Interest Income Account
                        Temp = "REPAYMENT_INTEREST",
                        DocumentNo = receiptNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = repaymentDto.CompanyCode,
                        TransDescript = $"Interest payment for loan {loan.LoanNo} - Receipt: {receiptNo}",
                        AuditTime = DateTime.Now,
                        AuditId = repaymentDto.ReceivedBy,
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repaymentDto.ReferenceNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };

                    _context.Gltransactions.Add(interestGL);
                    createdGLTransactions.Add(interestGL);
                    _logger.LogInformation($"Interest GL Entry: Dr {loanReceivableAccount}, Cr {interestIncomeAccount}, Amount: {interestAllocated:C}");
                }

                // ============================================================
                // 3. GL TRANSACTION FOR PENALTY (Dr: Loan Receivable, Cr: Penalty Income)
                // This recognizes penalty income and reduces the loan receivable
                // ============================================================
                if (penaltyAllocated > 0.01m)
                {
                    var penaltyGL = new Gltransaction
                    {
                        TransDate = repaymentDto.PaymentDate,
                        Amount = penaltyAllocated,
                        DrAccNo = loanReceivableAccount,            // Debit: Loan Receivable Account
                        CrAccNo = penaltyIncomeAccount,             // Credit: Penalty Income Account
                        Temp = "REPAYMENT_PENALTY",
                        DocumentNo = receiptNo,
                        Source = "LOAN_REPAYMENT",
                        CompanyCode = repaymentDto.CompanyCode,
                        TransDescript = $"Penalty payment for loan {loan.LoanNo} - Receipt: {receiptNo}",
                        AuditTime = DateTime.Now,
                        AuditId = repaymentDto.ReceivedBy,
                        Cash = 0,
                        DocPosted = 1,
                        ChequeNo = repaymentDto.ReferenceNo,
                        Dregard = false,
                        Recon = false,
                        TransactionNo = transactionNo,
                        Module = "LOAN",
                        ReconId = 0,
                        AuditDateTime = DateTime.Now
                    };

                    _context.Gltransactions.Add(penaltyGL);
                    createdGLTransactions.Add(penaltyGL);
                    _logger.LogInformation($"Penalty GL Entry: Dr {loanReceivableAccount}, Cr {penaltyIncomeAccount}, Amount: {penaltyAllocated:C}");
                }

                // ============================================================
                // 4. OPTIONAL: OVERPAYMENT GL TRANSACTION (Dr: Bank, Cr: Overpayment Liability)
                // ============================================================
                // After the regular payment allocation (after penaltyAllocated, interestAllocated, principalAllocated are set)

                // ============================================================
                // HANDLE OVERPAYMENT - APPLY TO FUTURE INSTALLMENTS
                // WITH CORRECT PRIORITY: PENALTY → INTEREST → PRINCIPAL
                // ============================================================
                if (overpaymentAmount > 0.01m)
                {
                    _logger.LogInformation($"Overpayment of {overpaymentAmount:C} detected. Applying to future installments...");

                    // Get all remaining schedules (excluding the current one if it's fully paid)
                    var futureSchedules = await _context.LoanSchedules
                        .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                        .OrderBy(s => s.InstallmentNo)
                        .ToListAsync();

                    decimal remainingOverpayment = overpaymentAmount;

                    foreach (var schedule in futureSchedules)
                    {
                        if (remainingOverpayment <= 0.01m) break;

                        // Skip if schedule is already fully paid
                        if (schedule.OutstandingTotal <= 0.01m) continue;

                        decimal scheduleOutstanding = schedule.OutstandingTotal;

                        if (remainingOverpayment >= scheduleOutstanding)
                        {
                            // Fully pay this schedule
                            schedule.PaidPrincipal = schedule.PrincipalAmount;
                            schedule.PaidInterest = schedule.InterestAmount;
                            schedule.PaidTotal = schedule.TotalInstallment;
                            schedule.OutstandingPrincipal = 0;
                            schedule.OutstandingInterest = 0;
                            schedule.OutstandingTotal = 0;
                            schedule.Status = "Paid";
                            schedule.PaidDate = repaymentDto.PaymentDate;
                            remainingOverpayment -= scheduleOutstanding;

                            _logger.LogInformation($"Schedule {schedule.InstallmentNo} fully paid using overpayment");
                        }
                        else
                        {
                            // ============================================================
                            // ✅ CORRECT PRIORITY: Penalty → Interest → Principal
                            // ============================================================

                            // 1. FIRST: Apply to Penalty (Highest Priority)
                            if (remainingOverpayment > 0.01m && schedule.PenaltyAmount > 0)
                            {
                                decimal penaltyToPay = Math.Min(remainingOverpayment, schedule.PenaltyAmount);
                                // Penalty is stored in the schedule's PenaltyAmount field
                                // We need to reduce the penalty amount
                                schedule.PenaltyAmount -= penaltyToPay;
                                remainingOverpayment -= penaltyToPay;
                                _logger.LogInformation($"Applied {penaltyToPay:C} to penalty. Remaining penalty: {schedule.PenaltyAmount:C}");
                            }

                            // 2. SECOND: Apply to Interest
                            if (remainingOverpayment > 0.01m && schedule.OutstandingInterest > 0)
                            {
                                decimal interestToPay = Math.Min(remainingOverpayment, schedule.OutstandingInterest);
                                schedule.PaidInterest += interestToPay;
                                schedule.OutstandingInterest = schedule.InterestAmount - schedule.PaidInterest;
                                remainingOverpayment -= interestToPay;
                                _logger.LogInformation($"Applied {interestToPay:C} to interest. Remaining interest: {schedule.OutstandingInterest:C}");
                            }

                            // 3. THIRD: Apply to Principal (Lowest Priority - only after penalty and interest are cleared)
                            if (remainingOverpayment > 0.01m && schedule.OutstandingPrincipal > 0)
                            {
                                decimal principalToPay = Math.Min(remainingOverpayment, schedule.OutstandingPrincipal);
                                schedule.PaidPrincipal += principalToPay;
                                schedule.OutstandingPrincipal = schedule.PrincipalAmount - schedule.PaidPrincipal;
                                remainingOverpayment -= principalToPay;
                                _logger.LogInformation($"Applied {principalToPay:C} to principal. Remaining principal: {schedule.OutstandingPrincipal:C}");
                            }

                            // Update totals
                            schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                            schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;

                            // Status: Only "Paid" if both principal and interest are fully paid
                            schedule.Status = (schedule.OutstandingPrincipal <= 0.01m && schedule.OutstandingInterest <= 0.01m) ? "Paid" : "Partial";
                            remainingOverpayment = 0;

                            _logger.LogInformation($"Schedule {schedule.InstallmentNo} updated. Penalty: {schedule.PenaltyAmount:C}, Interest: {schedule.OutstandingInterest:C}, Principal: {schedule.OutstandingPrincipal:C}, Total: {schedule.OutstandingTotal:C}");
                        }
                    }

                    // If there's still overpayment after all schedules, record it as credit
                    if (remainingOverpayment > 0.01m)
                    {
                        _logger.LogInformation($"Overpayment of {remainingOverpayment:C} remains after applying to all schedules. Creating credit memo.");
                        loanbal.Remarks = $"Overpayment credit: {remainingOverpayment:C}. {loanbal.Remarks ?? ""}";
                    }

                    // Update loan balance to reflect overpayment applied to future schedules
                    var updatedLoanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == repaymentDto.LoanNo && lb.Companycode == repaymentDto.CompanyCode);

                    if (updatedLoanbal != null)
                    {
                        // Recalculate from schedules
                        decimal totalPrincipalOutstanding = await _context.LoanSchedules
                            .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                            .SumAsync(s => s.OutstandingPrincipal);

                        decimal totalInterestOutstanding = await _context.LoanSchedules
                            .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                            .SumAsync(s => s.OutstandingInterest);

                        decimal totalPenaltyOutstanding = await _context.LoanSchedules
                            .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                            .SumAsync(s => s.PenaltyAmount);

                        updatedLoanbal.Balance = totalPrincipalOutstanding;
                        updatedLoanbal.IntrOwed = totalInterestOutstanding;
                        updatedLoanbal.IntBalance = totalInterestOutstanding;
                        updatedLoanbal.Penalty = totalPenaltyOutstanding;

                        // Update next due date
                        var nextSchedule = await _context.LoanSchedules
                            .Where(s => s.LoanNo == repaymentDto.LoanNo && s.Status != "Paid")
                            .OrderBy(s => s.InstallmentNo)
                            .FirstOrDefaultAsync();

                        if (nextSchedule != null)
                        {
                            updatedLoanbal.Nextduedate = nextSchedule.DueDate;
                            updatedLoanbal.Duedate = nextSchedule.DueDate;
                            updatedLoanbal.RepayRate = nextSchedule.TotalInstallment;
                        }

                        await _context.SaveChangesAsync();
                    }
                }


                //if (overpaymentAmount > 0.01m)
                //{
                //    var overpaymentGL = new Gltransaction
                //    {
                //        TransDate = repaymentDto.PaymentDate,
                //        Amount = overpaymentAmount,
                //        DrAccNo = bankAccount,                      // Debit: Bank Account
                //        CrAccNo = overpaymentLiabilityAccount,      // Credit: Overpayment Liability Account
                //        Temp = "REPAYMENT_OVERPAYMENT",
                //        DocumentNo = receiptNo,
                //        Source = "LOAN_REPAYMENT",
                //        CompanyCode = repaymentDto.CompanyCode,
                //        TransDescript = $"Overpayment for loan {loan.LoanNo} - Receipt: {receiptNo}",
                //        AuditTime = DateTime.Now,
                //        AuditId = repaymentDto.ReceivedBy,
                //        Cash = 0,
                //        DocPosted = 1,
                //        ChequeNo = repaymentDto.ReferenceNo,
                //        Dregard = false,
                //        Recon = false,
                //        TransactionNo = transactionNo,
                //        Module = "LOAN",
                //        ReconId = 0,
                //        AuditDateTime = DateTime.Now
                //    };

                //    _context.Gltransactions.Add(overpaymentGL);
                //    createdGLTransactions.Add(overpaymentGL);
                //    _logger.LogInformation($"Overpayment GL Entry: Dr {bankAccount}, Cr {overpaymentLiabilityAccount}, Amount: {overpaymentAmount:C}");
                //}

                await _context.SaveChangesAsync();

                // 12. CREATE BLOCK AND BLOCKCHAIN TRANSACTION
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
                    TransactionType = "LOAN_REPAYMENT",
                    LoanNo = repaymentDto.LoanNo,
                    MemberNo = repaymentDto.MemberNo,
                    ReceiptNo = receiptNo,
                    PaymentNo = paymentNo,
                    Amount = repaymentDto.AmountPaid,
                    InstallmentNo = currentSchedule.InstallmentNo,
                    PenaltyAllocated = penaltyAllocated,
                    InterestAllocated = interestAllocated,
                    PrincipalAllocated = principalAllocated,
                    Overpayment = overpaymentAmount,
                    BalanceAfter = loanbal.Balance,
                    InterestAfter = loanbal.IntrOwed,
                    PaymentDate = repaymentDto.PaymentDate,
                    IsEarlyFullSettlement = isEarlyFullSettlement,
                    BlockHash = blockHash
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_REPAYMENT",
                    MemberNo = repaymentDto.MemberNo,
                    CompanyCode = repaymentDto.CompanyCode,
                    Amount = repaymentDto.AmountPaid,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = receiptNo,
                    Status = "CONFIRMED",
                    BlockHash = blockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update records with BlockchainTxId
                repayment.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                loanbal.BlockchainTxId = blockchainTx.TransactionId;
                foreach (var glTxn in createdGLTransactions)
                {
                    glTxn.BlockchainTxId = blockchainTx.TransactionId;
                }
                foreach (var schedule in remainingSchedules.Where(s => s.Status == "Paid"))
                {
                    schedule.BlockchainTxId = blockchainTx.TransactionId;
                }
                await _context.SaveChangesAsync();

                // ============================================================
                // SAVE AUDIT TRAIL FOR REPAYMENT
                // ============================================================

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == repaymentDto.MemberNo && m.CompanyCode == repaymentDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : repaymentDto.MemberNo;

                var auditExtraData = new
                {
                    loanNo = repaymentDto.LoanNo,
                    memberNo = repaymentDto.MemberNo,
                    memberName = memberName,
                    receiptNo = receiptNo,
                    paymentNo = paymentNo,
                    amountPaid = repaymentDto.AmountPaid,
                    paymentDate = repaymentDto.PaymentDate,
                    referenceNo = repaymentDto.ReferenceNo ?? "",
                    //glAccountNo = repaymentDto.GlAccountNo,
                    remarks = repaymentDto.Remarks ?? "",
                    principalAllocated = principalAllocated,
                    interestAllocated = interestAllocated,
                    penaltyAllocated = penaltyAllocated,
                    overpaymentAmount = overpaymentAmount,
                    isEarlyFullSettlement = isEarlyFullSettlement,
                    totalFullBalanceBefore = totalFullBalance,
                    installmentNo = currentSchedule.InstallmentNo,
                    dueDate = currentSchedule.DueDate,
                    daysOverdue = repaymentDto.PaymentDate > currentSchedule.DueDate ? (repaymentDto.PaymentDate - currentSchedule.DueDate).Days : 0,
                    receivedBy = repaymentDto.ReceivedBy,
                    processedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanAmountBefore = oldLoanAamount,
                    loanAmountAfter = loan.Aamount,
                    balanceBefore = oldBalance,
                    balanceAfter = loanbal.Balance,
                    interestOwedBefore = oldIntrOwed,
                    interestOwedAfter = loanbal.IntrOwed,
                    penaltyBefore = oldPenalty,
                    penaltyAfter = loanbal.Penalty,
                    scheduleStatusBefore = oldScheduleStatus,
                    scheduleStatusAfter = currentSchedule.Status,
                    isLoanFullyPaid = isFullyPaid,
                    blockchainTxId = blockchainTx.TransactionId,
                    // Include penalty config from Loans table for audit
                    penaltyAttracts = attractsPenalty,
                    penaltyMode = penaltyMode,
                    penaltyRateType = penaltyRateType,
                    penaltyValue = penaltyValue,
                    penaltyChargeItem = penaltyChargeItem,
                    gracePeriodDays = gracePeriodDays
                };

                var repaymentForAudit = new
                {
                    repayment.Id,
                    repayment.LoanNo,
                    repayment.MemberNo,
                    repayment.ReceiptNo,
                    repayment.PaymentNo,
                    repayment.DateReceived,
                    repayment.Amount,
                    repayment.Principal,
                    repayment.Interest,
                    repayment.Penalty,
                    repayment.LoanBalance,
                    repayment.IntrOwed,
                    repayment.Remarks,
                    repayment.Transby,
                    repayment.TransactionNo,
                    repayment.ApiKey,
                    CreatedBy = repaymentDto.ReceivedBy,
                    CreatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: repaymentForAudit,
                    tableName: "Repay",
                    recordId: receiptNo,
                    userId: repaymentDto.ReceivedBy,
                    userName: repaymentDto.ReceivedBy,
                    companyCode: repaymentDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // Save Audit for LoanBal Update
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Balance = oldBalance, IntrOwed = oldIntrOwed, Penalty = oldPenalty },
                    newModel: new { loanbal.LoanNo, loanbal.Balance, loanbal.IntrOwed, loanbal.Penalty, loanbal.LastDate, loanbal.Nextduedate, loanbal.Cleared, UpdatedBy = repaymentDto.ReceivedBy, UpdatedDate = DateTime.Now, BlockchainTxId = blockchainTx.TransactionId },
                    tableName: "Loanbal",
                    recordId: loanbal.Id.ToString(),
                    userId: repaymentDto.ReceivedBy,
                    userName: repaymentDto.ReceivedBy,
                    companyCode: repaymentDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(new { loanNo = repaymentDto.LoanNo, balanceBefore = oldBalance, balanceAfter = loanbal.Balance, interestReduction = interestAllocated, penaltyReduction = penaltyAllocated, isFullyPaid = isFullyPaid, blockchainTxId = blockchainTx.TransactionId }),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // Save Audit for Loan Status Change
                if (oldLoanStatus != loan.Status)
                {
                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new { Status = oldLoanStatus, Posted = oldLoanPosted, Aamount = oldLoanAamount },
                        newModel: new { loan.LoanNo, loan.Status, loan.Posted, loan.Aamount, loan.UserName, loan.AuditDateTime, UpdatedBy = repaymentDto.ReceivedBy, UpdateReason = isFullyPaid ? "Loan fully paid" : "Status updated after repayment", BlockchainTxId = blockchainTx.TransactionId },
                        tableName: "Loans",
                        recordId: repaymentDto.LoanNo,
                        userId: repaymentDto.ReceivedBy,
                        userName: repaymentDto.ReceivedBy,
                        companyCode: repaymentDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(new { loanNo = repaymentDto.LoanNo, statusChangedFrom = oldLoanStatus, statusChangedTo = loan.Status, reason = isFullyPaid ? "Loan fully paid and closed" : "Loan status updated after repayment", paymentNo = paymentNo, receiptNo = receiptNo, amountPaid = repaymentDto.AmountPaid, balanceAfter = loanbal.Balance, blockchainTxId = blockchainTx.TransactionId }),
                        blockchainTxId: blockchainTx.TransactionId
                    );
                }

                // Save Audit for Schedule Update
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Status = oldScheduleStatus, OutstandingPrincipal = oldScheduleOutstandingPrincipal, OutstandingInterest = oldScheduleOutstandingInterest, OutstandingTotal = oldScheduleOutstandingTotal },
                    newModel: new { currentSchedule.Id, currentSchedule.LoanNo, currentSchedule.InstallmentNo, currentSchedule.Status, currentSchedule.OutstandingPrincipal, currentSchedule.OutstandingInterest, currentSchedule.OutstandingTotal, currentSchedule.PaidPrincipal, currentSchedule.PaidInterest, currentSchedule.PaidTotal, currentSchedule.PaidDate, currentSchedule.PenaltyAmount, UpdatedBy = repaymentDto.ReceivedBy, UpdatedDate = DateTime.Now, BlockchainTxId = blockchainTx.TransactionId },
                    tableName: "LoanSchedules",
                    recordId: currentSchedule.Id.ToString(),
                    userId: repaymentDto.ReceivedBy,
                    userName: repaymentDto.ReceivedBy,
                    companyCode: repaymentDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(new { loanNo = repaymentDto.LoanNo, installmentNo = currentSchedule.InstallmentNo, statusBefore = oldScheduleStatus, statusAfter = currentSchedule.Status, isEarlyFullSettlement = isEarlyFullSettlement, blockchainTxId = blockchainTx.TransactionId }),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR GL TRANSACTIONS
                // ============================================================
                foreach (var glTxn in createdGLTransactions)
                {
                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: new
                        {
                            glTxn.Id, glTxn.TransDate, glTxn.Amount, glTxn.DrAccNo, glTxn.CrAccNo, glTxn.DocumentNo, glTxn.Source, glTxn.TransDescript, glTxn.ChequeNo, glTxn.TransactionNo, CreatedBy = repaymentDto.ReceivedBy, CreatedDate = DateTime.Now, BlockchainTxId = blockchainTx.TransactionId
                        },
                        tableName: "Gltransactions",
                        recordId: glTxn.Id.ToString(),
                        userId: repaymentDto.ReceivedBy,
                        userName: repaymentDto.ReceivedBy,
                        companyCode: repaymentDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            loanNo = repaymentDto.LoanNo,
                            receiptNo = receiptNo,
                            amount = glTxn.Amount,
                            drAccount = glTxn.DrAccNo,
                            crAccount = glTxn.CrAccNo,
                            transactionType = glTxn.Temp,
                            isEarlyFullSettlement = isEarlyFullSettlement,
                            blockchainTxId = blockchainTx.TransactionId
                        }),
                        blockchainTxId: blockchainTx.TransactionId
                    );
                }

                _logger.LogInformation($"Repayment audit completed for loan {repaymentDto.LoanNo}, Receipt: {receiptNo}");

                await transaction.CommitAsync();

                _logger.LogInformation($"Repayment #{paymentNo} - {receiptNo} processed successfully. " +
                    $"Principal: {principalAllocated:C}, Interest: {interestAllocated:C}, Penalty: {penaltyAllocated:C}, " +
                    $"New Balance: {loanbal.Balance:C}, New Interest Owed: {loanbal.IntrOwed:C}, " +
                    $"IsFullSettlement: {isEarlyFullSettlement}");

                return repayment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing repayment for loan {repaymentDto.LoanNo}");
                throw;
            }
        }
        private async Task<string> GenerateReceiptNumberAsync(string companyCode)
        {
            string prefix = $"RCPT{DateTime.Now:yyyyMMdd}";

            var lastReceipt = await _context.Repay
                .Where(r =>
                    r.CompanyCode == companyCode &&
                    r.ReceiptNo != null &&
                    r.ReceiptNo.StartsWith(prefix))
                .OrderByDescending(r => r.ReceiptNo)
                .Select(r => r.ReceiptNo)
                .FirstOrDefaultAsync();

            int nextSequence = 1;

            if (!string.IsNullOrWhiteSpace(lastReceipt) &&
                lastReceipt.Length >= 4)
            {
                // Get ONLY last 4 digits
                string seqPart = lastReceipt.Substring(lastReceipt.Length - 4);

                if (int.TryParse(seqPart, out int lastSequence))
                {
                    nextSequence = lastSequence + 1;
                }
            }

            return $"{prefix}{nextSequence:D4}";
        }
        public async Task<List<Repay>> GetLoanRepaymentsAsync(string loanNo)
        {
            return await _context.Repay
                .Where(r => r.LoanNo == loanNo)
                .OrderByDescending(r => r.AuditTime)
                .ToListAsync();
        }
        public async Task<Repay> ReverseRepaymentAsync(int repaymentId, string reason, string reversedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var repayment = await _context.Repay
                    .FirstOrDefaultAsync(r => r.Id == repaymentId);

                if (repayment == null)
                {
                    throw new InvalidOperationException("Repayment record not found");
                }

                if (repayment.Posted == false)
                {
                    throw new InvalidOperationException("Repayment has already been reversed");
                }

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == repayment.LoanNo && l.CompanyCode == repayment.CompanyCode);

                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == repayment.LoanNo && lb.Companycode == repayment.CompanyCode);

                // Reverse the allocations
                decimal principalToAddBack = repayment.Principal ?? 0;
                decimal interestToAddBack = repayment.Interest ?? 0;
                decimal penaltyToAddBack = repayment.Penalty ?? 0;

                if (loanbal != null)
                {
                    loanbal.Balance += principalToAddBack;
                    loanbal.IntrOwed += interestToAddBack;
                    loanbal.Penalty += penaltyToAddBack;
                    loanbal.IntBalance += interestToAddBack;
                    loanbal.LastDate = repayment.AuditDateTime ?? DateTime.Now;
                    loanbal.Processdate = DateTime.Now;
                }

                if (loan != null && loan.Status == (int)Status.Closed)
                {
                    loan.Status = (int)Status.Disbursed;
                    loan.Posted = "Active";
                }

                // Reverse schedule allocations
                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repayment.LoanNo)
                    .OrderByDescending(s => s.InstallmentNo)
                    .ToListAsync();

                decimal remainingPrincipal = principalToAddBack;
                decimal remainingInterest = interestToAddBack;

                foreach (var schedule in schedules)
                {
                    if (remainingPrincipal <= 0 && remainingInterest <= 0) break;

                    if (schedule.Status == "Paid" || schedule.Status == "Partial")
                    {
                        if (remainingPrincipal > 0 && schedule.PaidPrincipal > 0)
                        {
                            decimal principalToReverse = Math.Min(remainingPrincipal, schedule.PaidPrincipal);
                            schedule.PaidPrincipal -= principalToReverse;
                            schedule.OutstandingPrincipal += principalToReverse;
                            remainingPrincipal -= principalToReverse;
                        }

                        if (remainingInterest > 0 && schedule.PaidInterest > 0)
                        {
                            decimal interestToReverse = Math.Min(remainingInterest, schedule.PaidInterest);
                            schedule.PaidInterest -= interestToReverse;
                            schedule.OutstandingInterest += interestToReverse;
                            remainingInterest -= interestToReverse;
                        }

                        schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                        schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;

                        if (schedule.PaidPrincipal <= 0 && schedule.PaidInterest <= 0)
                        {
                            schedule.Status = "Pending";
                            schedule.PaidDate = null;
                        }
                        else if (schedule.PaidPrincipal < schedule.PrincipalAmount || schedule.PaidInterest < schedule.InterestAmount)
                        {
                            schedule.Status = "Partial";
                        }
                    }
                }

                // Mark repayment as reversed
                repayment.Posted = false;
                repayment.Remarks = $"Reversed: {reason}";
                repayment.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Record blockchain reversal
                var reversalData = new
                {
                    RepaymentId = repaymentId,
                    LoanNo = repayment.LoanNo,
                    ReceiptNo = repayment.ReceiptNo,
                    AmountReversed = repayment.Amount,
                    PrincipalReversed = principalToAddBack,
                    InterestReversed = interestToAddBack,
                    PenaltyReversed = penaltyToAddBack,
                    Reason = reason,
                    ReversedBy = reversedBy,
                    ReversalDate = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_REPAYMENT_REVERSED",
                    MemberNo = repayment.MemberNo,
                    CompanyCode = repayment.CompanyCode,
                    Amount = repayment.Amount ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(reversalData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(reversalData),
                    OffChainReferenceId = repayment.ReceiptNo,
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                repayment.BlockchainTxId = blockchainTx.TransactionId;
                if (loanbal != null) loanbal.BlockchainTxId = blockchainTx.TransactionId;
                if (loan != null) loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation($"Repayment {repayment.ReceiptNo} reversed successfully");

                return repayment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error reversing repayment ID {repaymentId}");
                throw;
            }
        }

        /// Updates loan schedules after a repayment - Works for AMT and STL loans
         
        private async Task UpdateLoanSchedulesAfterRepaymentAsync(string loanNo, decimal amountPaid, DateTime paymentDate, string repaymentMethod = "AMT")
        {
            var schedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo && s.Status != "Paid" && s.OutstandingTotal > 0.01m)
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            if (!schedules.Any())
            {
                _logger.LogWarning($"No unpaid schedules found for loan {loanNo}");
                return;
            }

            decimal remainingAmount = amountPaid;
            bool isSTL = repaymentMethod == "STL";

            _logger.LogInformation($"Updating schedules for loan {loanNo}: Amount={amountPaid:C}, Method={repaymentMethod}, Schedules found={schedules.Count}");

            foreach (var schedule in schedules)
            {
                if (remainingAmount <= 0.01m) break;

                decimal scheduleOutstanding = schedule.OutstandingPrincipal + schedule.OutstandingInterest;

                if (scheduleOutstanding <= 0.01m)
                {
                    // Already paid, skip
                    continue;
                }

                if (remainingAmount >= scheduleOutstanding - 0.01m)
                {
                    // Fully pay this schedule
                    schedule.PaidPrincipal = schedule.PrincipalAmount;
                    schedule.PaidInterest = schedule.InterestAmount;
                    schedule.PaidTotal = schedule.TotalInstallment;
                    schedule.OutstandingPrincipal = 0;
                    schedule.OutstandingInterest = 0;
                    schedule.OutstandingTotal = 0;
                    schedule.Status = "Paid";
                    schedule.PaidDate = paymentDate;
                    remainingAmount -= scheduleOutstanding;

                    _logger.LogInformation($"Schedule {schedule.InstallmentNo} for loan {loanNo} fully paid");
                }
                else
                {
                    // Partial payment - allocate based on method
                    if (isSTL)
                    {
                        // STL: Pay interest first, then principal
                        if (remainingAmount <= schedule.OutstandingInterest)
                        {
                            // Only paying interest
                            schedule.PaidInterest += remainingAmount;
                            schedule.OutstandingInterest = schedule.InterestAmount - schedule.PaidInterest;
                            schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                            schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;
                            remainingAmount = 0;
                        }
                        else
                        {
                            // Pay all interest + some principal
                            decimal interestToPay = schedule.OutstandingInterest;
                            schedule.PaidInterest = schedule.InterestAmount;
                            schedule.OutstandingInterest = 0;
                            remainingAmount -= interestToPay;

                            decimal principalToPay = Math.Min(remainingAmount, schedule.OutstandingPrincipal);
                            schedule.PaidPrincipal += principalToPay;
                            schedule.OutstandingPrincipal = schedule.PrincipalAmount - schedule.PaidPrincipal;

                            schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                            schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;
                            remainingAmount -= principalToPay;
                        }
                    }
                    else
                    {
                        // AMT: Proportional allocation
                        decimal ratio = remainingAmount / scheduleOutstanding;
                        decimal principalToAllocate = schedule.OutstandingPrincipal * ratio;
                        decimal interestToAllocate = schedule.OutstandingInterest * ratio;

                        schedule.PaidPrincipal += principalToAllocate;
                        schedule.PaidInterest += interestToAllocate;
                        schedule.OutstandingPrincipal = schedule.PrincipalAmount - schedule.PaidPrincipal;
                        schedule.OutstandingInterest = schedule.InterestAmount - schedule.PaidInterest;
                        schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                        schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;
                        remainingAmount = 0;
                    }

                    schedule.Status = "Partial";
                    _logger.LogInformation($"Schedule {schedule.InstallmentNo} for loan {loanNo} partially paid: Principal Paid={schedule.PaidPrincipal:C}, Interest Paid={schedule.PaidInterest:C}");
                }
            }

            // Update the loan balance record
            var loanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

            if (loanbal != null)
            {
                // Find next unpaid schedule to update due date
                var nextUnpaid = schedules.FirstOrDefault(s => s.Status != "Paid" && s.OutstandingTotal > 0.01m);
                if (nextUnpaid != null)
                {
                    loanbal.Nextduedate = nextUnpaid.DueDate;
                    loanbal.Duedate = nextUnpaid.DueDate;
                    loanbal.RepayRate = nextUnpaid.TotalInstallment;
                }
                else
                {
                    // All schedules are paid
                    loanbal.Nextduedate = null;
                    loanbal.Cleared = loanbal.Balance <= 0.01m;
                }
                loanbal.Processdate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Schedule update completed for loan {loanNo}. Remaining amount: {remainingAmount:C}");
        }

        /// <summary>
        /// Gets the current unpaid schedule for a loan
        /// </summary>
        private async Task<LoanSchedule?> GetCurrentScheduleAsync(string loanNo)
        {
            try
            {
                var schedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo && s.Status != "Paid" && s.OutstandingTotal > 0.01m)
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    _logger.LogWarning($"No current schedule found for loan {loanNo}");
                }

                return schedule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting current schedule for loan {loanNo}");
                return null;
            }
        }

        /// <summary>
        /// Updates RBAL schedule after extra principal payment - Recalculates future interest
        /// </summary>
        private async Task UpdateRbalScheduleAsync(string loanNo, decimal principalPaid, DateTime paymentDate)
        {
            var schedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            if (!schedules.Any())
            {
                _logger.LogWarning($"No schedules found for RBAL loan {loanNo}");
                return;
            }

            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == loan.CompanyCode);

            if (loan == null || loanType == null)
            {
                _logger.LogError($"Loan or LoanType not found for RBAL update on loan {loanNo}");
                return;
            }

            decimal remainingPrincipal = principalPaid;
            decimal monthlyInterestRate = (loan.Interest ?? 0) / 100 / 12;

            _logger.LogInformation($"Updating RBAL schedule for loan {loanNo}: Principal paid extra={principalPaid:C}, Interest Rate={monthlyInterestRate:P}");

            foreach (var schedule in schedules)
            {
                if (remainingPrincipal <= 0.01m) break;

                decimal principalToAllocate = Math.Min(remainingPrincipal, schedule.OutstandingPrincipal);

                // Apply principal payment
                schedule.PaidPrincipal += principalToAllocate;
                schedule.OutstandingPrincipal = schedule.PrincipalAmount - schedule.PaidPrincipal;

                // Recalculate interest based on new outstanding principal (RBAL feature)
                if (schedule.OutstandingPrincipal > 0)
                {
                    // Recalculate remaining interest for this schedule based on outstanding principal
                    decimal newInterestForSchedule = schedule.OutstandingPrincipal * monthlyInterestRate;
                    schedule.InterestAmount = newInterestForSchedule;
                    schedule.TotalInstallment = schedule.OutstandingPrincipal + newInterestForSchedule;
                    schedule.MinimumPayment = newInterestForSchedule; // RBAL minimum is interest only
                }

                schedule.OutstandingInterest = schedule.InterestAmount - schedule.PaidInterest;
                schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                schedule.OutstandingTotal = schedule.OutstandingPrincipal + Math.Max(0, schedule.OutstandingInterest);

                remainingPrincipal -= principalToAllocate;

                // Update status
                if (schedule.OutstandingPrincipal <= 0.01m && schedule.OutstandingInterest <= 0.01m)
                {
                    schedule.Status = "Paid";
                    schedule.PaidDate = paymentDate;
                    _logger.LogInformation($"RBAL schedule {schedule.InstallmentNo} fully paid after extra principal payment");
                }
                else if (schedule.PaidTotal > 0)
                {
                    schedule.Status = "Partial";
                    _logger.LogInformation($"RBAL schedule {schedule.InstallmentNo} updated: New Interest={schedule.InterestAmount:C}, Outstanding={schedule.OutstandingTotal:C}");
                }
            }

            // Update loan balance record
            var loanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

            if (loanbal != null)
            {
                var nextSchedule = schedules.FirstOrDefault(s => s.Status != "Paid");
                if (nextSchedule != null)
                {
                    loanbal.Nextduedate = nextSchedule.DueDate;
                    loanbal.Duedate = nextSchedule.DueDate;
                    loanbal.RepayRate = nextSchedule.MinimumPayment ?? nextSchedule.TotalInstallment;
                }
                loanbal.Processdate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"RBAL schedule update completed for loan {loanNo}. Remaining principal to allocate: {remainingPrincipal:C}");
        }

        /// <summary>
        /// Updates next due date for a loan based on current schedule status
        /// </summary>
        private async Task UpdateNextDueDateAsync(string loanNo)
        {
            try
            {
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                if (loanbal == null) return;

                // Find the next unpaid schedule
                var nextSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo && s.Status != "Paid" && s.OutstandingTotal > 0.01m)
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                if (nextSchedule != null)
                {
                    loanbal.Nextduedate = nextSchedule.DueDate;
                    loanbal.Duedate = nextSchedule.DueDate;
                    loanbal.RepayRate = nextSchedule.TotalInstallment;
                    _logger.LogInformation($"Next due date for loan {loanNo} updated to {nextSchedule.DueDate:yyyy-MM-dd}");
                }
                else
                {
                    // Check if there's still balance without schedule
                    if (loanbal.Balance > 0.01m || loanbal.IntrOwed > 0.01m)
                    {
                        // Calculate next due date from last payment
                        var lastRepayment = await _context.Repay
                            .Where(r => r.LoanNo == loanNo && r.Posted == true)
                            .OrderByDescending(r => r.DateReceived)
                            .FirstOrDefaultAsync();

                        if (lastRepayment?.DateReceived != null)
                        {
                            loanbal.Nextduedate = lastRepayment.DateReceived.Value.AddMonths(1);
                            loanbal.Duedate = lastRepayment.DateReceived.Value.AddMonths(1);
                            _logger.LogWarning($"No schedule found but balance exists. Set next due date to {loanbal.Nextduedate:yyyy-MM-dd}");
                        }
                    }
                    else
                    {
                        loanbal.Nextduedate = null;
                        loanbal.Cleared = true;
                        _logger.LogInformation($"Loan {loanNo} has no remaining balance. Due dates cleared.");
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating next due date for loan {loanNo}");
            }
        }
        private async Task ReleaseCollateralGuaranteesForLoanAsync(string loanNo, string releasedBy)
        {
            try
            {
                // Get all active collateral guarantees for this loan
                var activeGuarantees = await _context.ColloanGuars
                    .Where(cg => cg.LoanNo == loanNo && cg.Balance > 0)
                    .ToListAsync();

                if (!activeGuarantees.Any())
                {
                    _logger.LogInformation($"No active collateral guarantees found for loan {loanNo}");
                    return;
                }

                _logger.LogInformation($"Releasing {activeGuarantees.Count} collateral guarantee(s) for loan {loanNo}");

                foreach (var guarantee in activeGuarantees)
                {
                    var originalBalance = guarantee.Balance;

                    // Release by setting balance to 0
                    guarantee.Balance = 0;
                    guarantee.AuditId = releasedBy;

                    // Record blockchain transaction for release
                    var blockchainData = new
                    {
                        Action = "COLLATERAL_GUARANTEE_AUTO_RELEASE",
                        CollateralGuaranteeId = guarantee.Id,
                        LoanNo = guarantee.LoanNo,
                        MemberNo = guarantee.MemberNo,
                        ColCode = guarantee.ColCode,
                        DocNo = guarantee.DocNo,
                        OriginalBalance = originalBalance,
                        ReleasedBy = releasedBy,
                        Reason = "Loan fully repaid",
                        ReleasedAt = DateTime.Now
                    };

                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "COLLATERAL_GUARANTEE_AUTO_RELEASE",
                        MemberNo = guarantee.MemberNo,
                        CompanyCode = guarantee.CompanyCode,
                        Amount = originalBalance,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                        OffChainReferenceId = guarantee.LoanNo,
                        Status = "CONFIRMED",
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    guarantee.BlockchainTxId = blockchainTx.TransactionId;

                    _logger.LogInformation($"Released collateral guarantee {guarantee.Id}: {guarantee.ColCode} - Doc: {guarantee.DocNo}, Amount: {originalBalance:C}");
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error releasing collateral guarantees for loan {loanNo}");
                throw;
            }
        }

        private async Task ReleaseMemberGuarantorsForLoanAsync(string loanNo, string releasedBy)
        {
            try
            {
                // Get the loan to identify the loanee
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null)
                {
                    _logger.LogWarning($"Loan {loanNo} not found for full release");
                    return;
                }

                string loaneeMemberNo = loan.MemberNo;

                // Get active guarantors
                var activeGuarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .ToListAsync();

                if (!activeGuarantors.Any())
                {
                    _logger.LogInformation($"No active guarantors found for loan {loanNo}");
                    return;
                }

                // Separate self and other guarantors
                var selfGuarantors = activeGuarantors.Where(g => g.MemberNo == loaneeMemberNo).ToList();
                var otherGuarantors = activeGuarantors.Where(g => g.MemberNo != loaneeMemberNo).ToList();

                // Release OTHER guarantors first
                foreach (var guarantor in otherGuarantors)
                {
                    guarantor.Transfered = true;
                    guarantor.Transdate = DateTime.Now;
                    guarantor.Balance = 0;
                    guarantor.AuditTime = DateTime.Now;
                    guarantor.AuditId = releasedBy;
                    guarantor.Description = $"Released due to loan {loanNo} being fully paid";

                    _logger.LogInformation($"Released other guarantor {guarantor.MemberNo} for loan {loanNo}");
                }

                // Then release SELF-guarantors
                foreach (var selfGuarantor in selfGuarantors)
                {
                    selfGuarantor.Transfered = true;
                    selfGuarantor.Transdate = DateTime.Now;
                    selfGuarantor.Balance = 0;
                    selfGuarantor.AuditTime = DateTime.Now;
                    selfGuarantor.AuditId = releasedBy;
                    selfGuarantor.Description = $"Self-guarantee released due to loan {loanNo} being fully paid";

                    _logger.LogInformation($"Released self-guarantor {selfGuarantor.MemberNo} for loan {loanNo}");
                }

                // Record blockchain transaction
                var blockchainData = new
                {
                    Action = "FULL_GUARANTOR_RELEASE",
                    LoanNo = loanNo,
                    LoaneeMemberNo = loaneeMemberNo,
                    OtherGuarantorsReleased = otherGuarantors.Count,
                    SelfGuarantorsReleased = selfGuarantors.Count,
                    ReleasedBy = releasedBy,
                    ReleasedAt = DateTime.Now
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "FULL_GUARANTOR_RELEASE",
                    MemberNo = loaneeMemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = activeGuarantors.Sum(g => g.Amount ?? 0),
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                foreach (var guarantor in activeGuarantors)
                {
                    guarantor.BlockchainTxId = blockchainTx.TransactionId;
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Released {activeGuarantors.Count} guarantors for loan {loanNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error releasing member guarantors for loan {loanNo}");
                throw;
            }
        }


        private async Task ReleaseGuarantorsProportionallyAsync(string loanNo, decimal principalBefore, decimal principalAfter, string releasedBy)
        {
            try
            {
                // Only proceed if there's actual principal reduction
                if (principalBefore <= 0 || principalAfter >= principalBefore)
                {
                    _logger.LogInformation($"No principal reduction for loan {loanNo}. Before: {principalBefore:C}, After: {principalAfter:C}");
                    return;
                }

                // Calculate percentage of loan repaid
                var percentageRepaid = 1 - (principalAfter / principalBefore);
                if (percentageRepaid <= 0.001m) // Less than 0.1% - skip
                {
                    _logger.LogInformation($"Percentage repaid too small for loan {loanNo}: {percentageRepaid:P2}");
                    return;
                }

                // Get the loan to identify the loanee
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                if (loan == null)
                {
                    _logger.LogWarning($"Loan {loanNo} not found for progressive release");
                    return;
                }

                string loaneeMemberNo = loan.MemberNo;

                // Get all active guarantors for this loan
                var allActiveGuarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .ToListAsync();

                if (!allActiveGuarantors.Any())
                {
                    _logger.LogInformation($"No active guarantors found for loan {loanNo}");
                    return;
                }

                // ============================================================
                // SEPARATE: Self-guarantor vs Other guarantors
                // ============================================================
                var selfGuarantors = allActiveGuarantors
                    .Where(g => g.MemberNo == loaneeMemberNo)
                    .ToList();

                var otherGuarantors = allActiveGuarantors
                    .Where(g => g.MemberNo != loaneeMemberNo)
                    .ToList();

                decimal totalOtherGuaranteeBefore = otherGuarantors.Sum(g => g.Balance ?? g.Amount ?? 0);
                decimal totalSelfGuaranteeBefore = selfGuarantors.Sum(g => g.Balance ?? g.Amount ?? 0);
                decimal totalGuaranteeBefore = totalOtherGuaranteeBefore + totalSelfGuaranteeBefore;

                if (totalGuaranteeBefore <= 0.01m)
                {
                    _logger.LogInformation($"Total guarantee amount is zero for loan {loanNo}");
                    return;
                }

                // ============================================================
                // Calculate how much to release from OTHER guarantors first
                // ============================================================
                decimal totalToRelease = totalGuaranteeBefore * percentageRepaid;
                decimal totalReleasedFromOthers = 0;
                decimal totalReleasedFromSelf = 0;

                _logger.LogInformation($"Progressive guarantor release for loan {loanNo}: " +
                    $"Total Guarantee: {totalGuaranteeBefore:C}, " +
                    $"Other Guarantors: {totalOtherGuaranteeBefore:C}, " +
                    $"Self Guarantee: {totalSelfGuaranteeBefore:C}, " +
                    $"Percentage Repaid: {percentageRepaid:P2}, " +
                    $"Total to Release: {totalToRelease:C}");

                var releasedGuarantors = new List<object>();

                // ============================================================
                // STEP 1: Release OTHER GUARANTORS first
                // ============================================================
                if (otherGuarantors.Any())
                {
                    decimal releaseAmountForOthers = totalToRelease;

                    // If there's not enough other guarantee to cover the release, 
                    // release all other guarantors and the remainder will come from self-guarantee
                    if (releaseAmountForOthers > totalOtherGuaranteeBefore)
                    {
                        releaseAmountForOthers = totalOtherGuaranteeBefore;
                    }

                    decimal percentageToReleaseOthers = totalOtherGuaranteeBefore > 0
                        ? releaseAmountForOthers / totalOtherGuaranteeBefore
                        : 0;

                    _logger.LogInformation($"Releasing {releaseAmountForOthers:C} from OTHER guarantors " +
                        $"({percentageToReleaseOthers:P2} of their total)");

                    foreach (var guarantor in otherGuarantors)
                    {
                        decimal individualBalance = guarantor.Balance ?? guarantor.Amount ?? 0;
                        if (individualBalance <= 0.01m) continue;

                        decimal releaseAmount = individualBalance * percentageToReleaseOthers;
                        decimal newBalance = Math.Max(0, individualBalance - releaseAmount);

                        // Update the guarantor balance
                        guarantor.Balance = newBalance;

                        if (newBalance <= 0.01m)
                        {
                            guarantor.Transfered = true;
                            guarantor.Transdate = DateTime.Now;
                            guarantor.Balance = 0;
                            _logger.LogInformation($"Guarantor {guarantor.MemberNo} FULLY released. " +
                                $"Original: {individualBalance:C}, Released: {releaseAmount:C}");
                        }
                        else
                        {
                            _logger.LogInformation($"Guarantor {guarantor.MemberNo} partially released. " +
                                $"Original: {individualBalance:C}, Released: {releaseAmount:C}, New Balance: {newBalance:C}");
                        }

                        // Update audit fields
                        guarantor.AuditTime = DateTime.Now;
                        guarantor.AuditId = releasedBy;
                        guarantor.Description = $"Progressive release: {percentageRepaid:P2} of loan repaid. " +
                            $"Released: {releaseAmount:C}, Remaining: {newBalance:C}";

                        totalReleasedFromOthers += releaseAmount;

                        releasedGuarantors.Add(new
                        {
                            guarantor.Id,
                            guarantor.MemberNo,
                            IsSelfGuarantor = false,
                            OriginalBalance = individualBalance,
                            ReleaseAmount = releaseAmount,
                            NewBalance = newBalance,
                            IsFullyReleased = newBalance <= 0.01m
                        });
                    }
                }

                // ============================================================
                // STEP 2: If OTHER guarantors are fully released AND there's still 
                // release amount remaining, THEN release SELF-GUARANTOR
                // ============================================================
                decimal remainingToRelease = totalToRelease - totalReleasedFromOthers;

                if (remainingToRelease > 0.01m && selfGuarantors.Any())
                {
                    // Check if all OTHER guarantors are now released
                    var remainingOtherGuarantors = await _context.Loanguar
                        .Where(g => g.LoanNo == loanNo &&
                                   g.MemberNo != loaneeMemberNo &&
                                   g.Transfered == false &&
                                   (g.Balance ?? 0) > 0.01m)
                        .ToListAsync();

                    bool allOthersReleased = !remainingOtherGuarantors.Any();

                    if (allOthersReleased)
                    {
                        _logger.LogInformation($"All OTHER guarantors released. Now releasing SELF-GUARANTOR: {remainingToRelease:C}");

                        foreach (var selfGuarantor in selfGuarantors)
                        {
                            decimal individualBalance = selfGuarantor.Balance ?? selfGuarantor.Amount ?? 0;
                            if (individualBalance <= 0.01m) continue;

                            decimal releaseAmount = Math.Min(remainingToRelease, individualBalance);
                            decimal newBalance = Math.Max(0, individualBalance - releaseAmount);

                            selfGuarantor.Balance = newBalance;

                            if (newBalance <= 0.01m)
                            {
                                selfGuarantor.Transfered = true;
                                selfGuarantor.Transdate = DateTime.Now;
                                selfGuarantor.Balance = 0;
                                _logger.LogInformation($"Self-guarantor {selfGuarantor.MemberNo} FULLY released. " +
                                    $"Original: {individualBalance:C}, Released: {releaseAmount:C}");
                            }
                            else
                            {
                                _logger.LogInformation($"Self-guarantor {selfGuarantor.MemberNo} partially released. " +
                                    $"Original: {individualBalance:C}, Released: {releaseAmount:C}, New Balance: {newBalance:C}");
                            }

                            selfGuarantor.AuditTime = DateTime.Now;
                            selfGuarantor.AuditId = releasedBy;
                            selfGuarantor.Description = $"Progressive release (self): {percentageRepaid:P2} of loan repaid. " +
                                $"Released: {releaseAmount:C}, Remaining: {newBalance:C}";

                            totalReleasedFromSelf += releaseAmount;
                            remainingToRelease -= releaseAmount;

                            releasedGuarantors.Add(new
                            {
                                selfGuarantor.Id,
                                selfGuarantor.MemberNo,
                                IsSelfGuarantor = true,
                                OriginalBalance = individualBalance,
                                ReleaseAmount = releaseAmount,
                                NewBalance = newBalance,
                                IsFullyReleased = newBalance <= 0.01m
                            });

                            if (remainingToRelease <= 0.01m) break;
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Cannot release self-guarantee yet. " +
                            $"{remainingOtherGuarantors.Count} other guarantors still have balances.");
                    }
                }

                await _context.SaveChangesAsync();

                // ============================================================
                // RECORD BLOCKCHAIN TRANSACTION
                // ============================================================
                try
                {
                    var blockchainData = new
                    {
                        Action = "PROGRESSIVE_GUARANTOR_RELEASE",
                        LoanNo = loanNo,
                        LoaneeMemberNo = loaneeMemberNo,
                        PrincipalBefore = principalBefore,
                        PrincipalAfter = principalAfter,
                        PercentageRepaid = percentageRepaid,
                        TotalGuaranteeBefore = totalGuaranteeBefore,
                        TotalReleased = totalReleasedFromOthers + totalReleasedFromSelf,
                        ReleasedFromOthers = totalReleasedFromOthers,
                        ReleasedFromSelf = totalReleasedFromSelf,
                        ReleasedBy = releasedBy,
                        ReleasedAt = DateTime.Now,
                        GuarantorsReleased = releasedGuarantors
                    };

                    var blockchainTx = new BlockchainTransaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        TransactionType = "PROGRESSIVE_GUARANTOR_RELEASE",
                        MemberNo = loaneeMemberNo,
                        CompanyCode = loan.CompanyCode,
                        Amount = totalReleasedFromOthers + totalReleasedFromSelf,
                        Timestamp = DateTime.Now,
                        DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                        OffChainReferenceId = loanNo,
                        Status = "CONFIRMED",
                        CreatedAt = DateTime.Now
                    };

                    _context.BlockchainTransactions.Add(blockchainTx);
                    await _context.SaveChangesAsync();

                    // Update all guarantors with the blockchain transaction ID
                    foreach (var guarantor in allActiveGuarantors.Where(g => g.BlockchainTxId == null))
                    {
                        guarantor.BlockchainTxId = blockchainTx.TransactionId;
                    }
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Blockchain transaction recorded for progressive release: {blockchainTx.TransactionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to record blockchain transaction for progressive release on loan {loanNo}");
                }

                // ============================================================
                // SAVE AUDIT TRAIL
                // ============================================================
                try
                {
                    var auditExtraData = new
                    {
                        loanNo = loanNo,
                        loaneeMemberNo = loaneeMemberNo,
                        principalBefore = principalBefore,
                        principalAfter = principalAfter,
                        percentageRepaid = percentageRepaid,
                        totalGuaranteeBefore = totalGuaranteeBefore,
                        totalReleasedFromOthers = totalReleasedFromOthers,
                        totalReleasedFromSelf = totalReleasedFromSelf,
                        totalReleased = totalReleasedFromOthers + totalReleasedFromSelf,
                        numberOfGuarantorsReleased = releasedGuarantors.Count,
                        releasedBy = releasedBy,
                        releasedDate = DateTime.Now
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new { TotalGuaranteeBefore = totalGuaranteeBefore, LoanNo = loanNo },
                        newModel: new
                        {
                            LoanNo = loanNo,
                            TotalReleased = totalReleasedFromOthers + totalReleasedFromSelf,
                            GuarantorsReleased = releasedGuarantors.Count,
                            PercentageRepaid = percentageRepaid,
                            ReleasedBy = releasedBy,
                            ReleasedDate = DateTime.Now
                        },
                        tableName: "Loanguar",
                        recordId: loanNo,
                        userId: releasedBy,
                        userName: releasedBy,
                        companyCode: loan.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                        blockchainTxId: null
                    );

                    _logger.LogInformation($"Audit trail recorded for progressive release on loan {loanNo}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to record audit trail for progressive release on loan {loanNo}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in progressive guarantor release for loan {loanNo}");
                // Don't throw - this should not block the repayment
            }
        }


        #endregion


        #region Schedule Generation

        public async Task<List<LoanScheduleDTO>> GetLoanScheduleAsync(string loanNo)
        {
            try
            {
                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == loanNo)
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();

                if (!schedules.Any())
                {
                    _logger.LogWarning($"No schedule found for loan {loanNo}");
                    return new List<LoanScheduleDTO>();
                }

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

                // Get ALL repayments for this loan, ordered by date
                var repayments = await _context.Repay
                    .Where(r => r.LoanNo == loanNo && r.Posted == true)
                    .OrderBy(r => r.DateReceived)
                    .ToListAsync();

                var scheduleDTOs = new List<LoanScheduleDTO>();
                bool isRBAL = loan?.RepayMethod == "RBAL";
                bool isSTL = loan?.RepayMethod == "STL";

                // Track remaining amounts to allocate across schedules
                decimal remainingPrincipalToAllocate = repayments.Sum(r => r.Principal ?? 0);
                decimal remainingInterestToAllocate = repayments.Sum(r => r.Interest ?? 0);
                decimal remainingPenaltyToAllocate = repayments.Sum(r => r.Penalty ?? 0);

                _logger.LogInformation($"Allocating payments for loan {loanNo}: " +
                    $"Total Principal Paid={remainingPrincipalToAllocate:C}, " +
                    $"Total Interest Paid={remainingInterestToAllocate:C}, " +
                    $"Total Penalty Paid={remainingPenaltyToAllocate:C}");

                for (int i = 0; i < schedules.Count; i++)
                {
                    var schedule = schedules[i];
                    decimal paidPrincipal = 0;
                    decimal paidInterest = 0;
                    decimal paidPenalty = 0;
                    string status = schedule.Status;

                    // First, allocate penalty (always applies to current overdue)
                    if (remainingPenaltyToAllocate > 0 && schedule.PenaltyAmount > 0)
                    {
                        paidPenalty = Math.Min(remainingPenaltyToAllocate, schedule.PenaltyAmount);
                        remainingPenaltyToAllocate -= paidPenalty;
                    }

                    if (isRBAL)
                    {
                        // RBAL: Interest only is mandatory
                        if (remainingInterestToAllocate > 0 && schedule.OutstandingInterest > 0)
                        {
                            paidInterest = Math.Min(remainingInterestToAllocate, schedule.InterestAmount);
                            remainingInterestToAllocate -= paidInterest;
                        }

                        // RBAL: Principal is optional (any extra goes to principal)
                        if (remainingPrincipalToAllocate > 0 && schedule.OutstandingPrincipal > 0)
                        {
                            paidPrincipal = Math.Min(remainingPrincipalToAllocate, schedule.PrincipalAmount);
                            remainingPrincipalToAllocate -= paidPrincipal;
                        }

                        // Determine status for RBAL
                        if (paidInterest >= schedule.InterestAmount - 0.01m)
                        {
                            status = "Paid";
                        }
                        else if (paidInterest > 0)
                        {
                            status = "Partial";
                        }
                        else if (schedule.DueDate < DateTime.Now && paidInterest == 0)
                        {
                            status = "Overdue";
                        }
                        else
                        {
                            status = "Pending";
                        }
                    }
                    else if (isSTL)
                    {
                        // STL: Pay interest first, then principal
                        if (remainingInterestToAllocate > 0 && schedule.OutstandingInterest > 0)
                        {
                            paidInterest = Math.Min(remainingInterestToAllocate, schedule.InterestAmount);
                            remainingInterestToAllocate -= paidInterest;
                        }

                        // After interest is paid, pay principal
                        if (remainingPrincipalToAllocate > 0 && schedule.OutstandingPrincipal > 0)
                        {
                            paidPrincipal = Math.Min(remainingPrincipalToAllocate, schedule.PrincipalAmount);
                            remainingPrincipalToAllocate -= paidPrincipal;
                        }

                        // Determine status for STL
                        bool isFullyPaid = (paidPrincipal >= schedule.PrincipalAmount - 0.01m) &&
                                           (paidInterest >= schedule.InterestAmount - 0.01m);

                        if (isFullyPaid)
                        {
                            status = "Paid";
                        }
                        else if (paidPrincipal > 0 || paidInterest > 0)
                        {
                            status = "Partial";
                        }
                        else if (schedule.DueDate < DateTime.Now)
                        {
                            status = "Overdue";
                        }
                        else
                        {
                            status = "Pending";
                        }
                    }
                    else // AMT
                    {
                        // AMT: Proportional allocation (standard)
                        // Calculate ratio for this schedule based on original amounts
                        decimal scheduleTotal = schedule.PrincipalAmount + schedule.InterestAmount;
                        decimal totalRemainingAllocation = remainingPrincipalToAllocate + remainingInterestToAllocate;

                        if (totalRemainingAllocation > 0 && scheduleTotal > 0)
                        {
                            decimal allocationRatio = Math.Min(1, totalRemainingAllocation / scheduleTotal);

                            paidPrincipal = schedule.PrincipalAmount * allocationRatio;
                            paidInterest = schedule.InterestAmount * allocationRatio;

                            // Deduct from remaining
                            remainingPrincipalToAllocate -= paidPrincipal;
                            remainingInterestToAllocate -= paidInterest;
                        }

                        // Determine status for AMT
                        bool isFullyPaid = (paidPrincipal >= schedule.PrincipalAmount - 0.01m) &&
                                           (paidInterest >= schedule.InterestAmount - 0.01m);

                        if (isFullyPaid)
                        {
                            status = "Paid";
                        }
                        else if (paidPrincipal > 0 || paidInterest > 0)
                        {
                            status = "Partial";
                        }
                        else if (schedule.DueDate < DateTime.Now)
                        {
                            status = "Overdue";
                        }
                        else
                        {
                            status = "Pending";
                        }
                    }

                    // Calculate outstanding amounts
                    decimal outstandingPrincipal = schedule.PrincipalAmount - paidPrincipal;
                    decimal outstandingInterest = schedule.InterestAmount - paidInterest;
                    decimal outstandingTotal = outstandingPrincipal + outstandingInterest + (schedule.PenaltyAmount - paidPenalty);

                    // Get paid date if fully paid
                    DateTime? paidDate = null;
                    if (status == "Paid")
                    {
                        // Find the repayment that completed this schedule
                        var relevantRepayment = repayments
                            .FirstOrDefault(r => (r.Principal ?? 0) + (r.Interest ?? 0) >= schedule.TotalInstallment);
                        paidDate = relevantRepayment?.DateReceived ?? DateTime.Now;
                    }

                    scheduleDTOs.Add(new LoanScheduleDTO
                    {
                        InstallmentNo = schedule.InstallmentNo,
                        DueDate = schedule.DueDate,
                        PrincipalAmount = schedule.PrincipalAmount,
                        InterestAmount = schedule.InterestAmount,
                        TotalInstallment = schedule.TotalInstallment,
                        PaidAmount = paidPrincipal + paidInterest + paidPenalty,
                        OutstandingAmount = outstandingTotal,
                        PenaltyAmount = schedule.PenaltyAmount - paidPenalty,
                        Status = status,
                        PaidDate = paidDate,
                        OutstandingPrincipal = outstandingPrincipal.ToString("N2"),
                        OutstandingInterest = outstandingInterest.ToString("N2"),
                        OutstandingTotal = outstandingTotal.ToString("N2"),
                        IsFlexible = schedule.IsFlexible,
                        MinimumPayment = schedule.MinimumPayment
                    });

                    _logger.LogInformation($"Schedule {schedule.InstallmentNo}: Status={status}, " +
                        $"Paid Principal={paidPrincipal:C}, Paid Interest={paidInterest:C}, " +
                        $"Remaining Principal={remainingPrincipalToAllocate:C}, " +
                        $"Remaining Interest={remainingInterestToAllocate:C}");
                }

                return scheduleDTOs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan schedule for {loanNo}");
                throw;
            }
        }
        public async Task<List<LoanSchedule>> GenerateLoanScheduleAsync(string loanNo)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            var loanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

            if (loanbal == null)
            {
                throw new InvalidOperationException($"Loan balance record not found for loan {loanNo}");
            }

            // Get the approved amount from Endmain (endorsement)
            var endmain = await _context.Endmain
                .FirstOrDefaultAsync(e => e.LoanNo == loanNo);

            // Principal amount is the APPROVED amount from appraisal
            decimal principalAmount = endmain?.AmtApproved ?? loan.LoanAmt ?? 0;

            if (principalAmount <= 0)
            {
                throw new InvalidOperationException("Cannot generate schedule. No approved amount found for this loan.");
            }

            _logger.LogInformation($"Generating schedule for loan {loanNo} with approved principal: {principalAmount:C}");
            _logger.LogInformation($"Repayment Method: {loan.RepayMethod}, Interest Rate: {loan.Interest}%, Period: {loan.RepayPeriod} months");

            var existingSchedule = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .ToListAsync();

            if (existingSchedule.Any())
            {
                _context.LoanSchedules.RemoveRange(existingSchedule);
                await _context.SaveChangesAsync();
            }

            var schedules = new List<LoanSchedule>();
            var annualInterestRate = loan.Interest ?? 0;
            var monthlyInterestRate = annualInterestRate / 100 / 12; // Convert percentage to decimal (e.g., 12% = 0.12, then /12 = 0.01)
            var months = loan.RepayPeriod ?? 12;
            var dueDate = loanbal.FirstDate.AddMonths(1);
            var repaymentMethod = loan.RepayMethod ?? "AMT";

            decimal totalInterestForLoan = 0;

            if (repaymentMethod == "STL")
            {
                // STL: Fixed Principal + Interest on ORIGINAL balance (not reducing)
                // In STL, interest is calculated on the original principal amount for each period
                decimal monthlyPrincipal = principalAmount / months;
                decimal monthlyInterest = principalAmount * monthlyInterestRate; // Interest on FULL principal
                decimal totalInstallment = monthlyPrincipal + monthlyInterest;
                totalInterestForLoan = monthlyInterest * months;

                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = monthlyPrincipal,
                        InterestAmount = monthlyInterest,
                        TotalInstallment = totalInstallment,
                        BalancePrincipal = remainingBalance - monthlyPrincipal,
                        BalanceInterest = totalInterestForLoan - (monthlyInterest * i),
                        BalanceTotal = (remainingBalance - monthlyPrincipal) + (totalInterestForLoan - (monthlyInterest * i)),
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = monthlyPrincipal,
                        OutstandingInterest = monthlyInterest,
                        OutstandingTotal = totalInstallment,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = false
                    };

                    schedules.Add(schedule);
                    remainingBalance -= monthlyPrincipal;
                }
            }
            else if (repaymentMethod == "AMT")
            {
                // AMT: Equal Monthly Installments (EMI)
                decimal monthlyPayment;
                if (monthlyInterestRate > 0)
                {
                    decimal factor = (decimal)Math.Pow((double)(1 + monthlyInterestRate), months);
                    monthlyPayment = principalAmount * monthlyInterestRate * factor / (factor - 1);
                }
                else
                {
                    monthlyPayment = principalAmount / months;
                }

                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    decimal interestAmount = remainingBalance * monthlyInterestRate;
                    decimal principalAmountPayment = monthlyPayment - interestAmount;
                    totalInterestForLoan += interestAmount;

                    if (i == months)
                    {
                        principalAmountPayment = remainingBalance;
                        monthlyPayment = principalAmountPayment + interestAmount;
                    }

                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = principalAmountPayment,
                        InterestAmount = interestAmount,
                        TotalInstallment = monthlyPayment,
                        BalancePrincipal = remainingBalance - principalAmountPayment,
                        BalanceInterest = totalInterestForLoan - (interestAmount * i),
                        BalanceTotal = (remainingBalance - principalAmountPayment) + (totalInterestForLoan - (interestAmount * i)),
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = principalAmountPayment,
                        OutstandingInterest = interestAmount,
                        OutstandingTotal = monthlyPayment,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = false
                    };

                    schedules.Add(schedule);
                    remainingBalance -= principalAmountPayment;
                }
            }
            else if (repaymentMethod == "RBAL")
            {
                // RBAL: Interest only minimum, principal flexible
                decimal remainingBalance = principalAmount;

                for (int i = 1; i <= months; i++)
                {
                    decimal interestAmount = remainingBalance * monthlyInterestRate;
                    totalInterestForLoan += interestAmount;

                    var schedule = new LoanSchedule
                    {
                        LoanNo = loanNo,
                        CompanyCode = loan.CompanyCode,
                        InstallmentNo = i,
                        DueDate = dueDate.AddMonths(i - 1),
                        PrincipalAmount = 0,
                        InterestAmount = interestAmount,
                        TotalInstallment = interestAmount,
                        BalancePrincipal = remainingBalance,
                        BalanceInterest = totalInterestForLoan - (interestAmount * i),
                        BalanceTotal = remainingBalance,
                        PaidPrincipal = 0,
                        PaidInterest = 0,
                        PaidTotal = 0,
                        OutstandingPrincipal = 0,
                        OutstandingInterest = interestAmount,
                        OutstandingTotal = interestAmount,
                        PenaltyAmount = 0,
                        Status = "Pending",
                        DaysOverdue = 0,
                        IsFlexible = true,
                        MinimumPayment = interestAmount
                    };

                    schedules.Add(schedule);
                }
            }

            // Update loanbal with correct total interest
            loanbal.IntrOwed = totalInterestForLoan;
            loanbal.IntBalance = totalInterestForLoan;
            await _context.SaveChangesAsync();

            await _context.LoanSchedules.AddRangeAsync(schedules);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Generated {schedules.Count} schedule entries for loan {loanNo}. Total Interest: {totalInterestForLoan:C}");

            return schedules;
        }

        public async Task UpdateOverdueStatusesAsync(string companyCode)
        {
            var today = DateTime.Now.Date;

            var overdueSchedules = await _context.LoanSchedules
                .Include(s => s.Loan)
                .Where(s => s.Loan != null && s.Loan.CompanyCode == companyCode &&
                           s.Status != "Paid" &&
                           s.DueDate.Date < today)
                .ToListAsync();

            foreach (var schedule in overdueSchedules)
            {
                schedule.Status = "Overdue";
                schedule.DaysOverdue = (today - schedule.DueDate.Date).Days;

                if (schedule.IsFlexible)
                {
                    decimal minimumDue = schedule.MinimumPayment ?? schedule.InterestAmount;
                    schedule.PenaltyAmount = CalculatePenalty(minimumDue, schedule.DaysOverdue);
                }
                else
                {
                    schedule.PenaltyAmount = CalculatePenalty(schedule.OutstandingTotal, schedule.DaysOverdue);
                }
            }

            var overdueLoans = overdueSchedules
                .GroupBy(s => s.LoanNo)
                .Select(g => new { LoanNo = g.Key, Penalty = g.Sum(s => s.PenaltyAmount) });

            foreach (var loanPenalty in overdueLoans)
            {
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanPenalty.LoanNo);

                if (loanbal != null)
                {
                    loanbal.Penalty = loanPenalty.Penalty;
                }
            }

            await _context.SaveChangesAsync();
        }

        private decimal CalculatePenalty(decimal amount, int daysOverdue)
        {
            if (daysOverdue <= 0) return 0;

            decimal penaltyRate = 0.01m; // 1% per month
            decimal penalty = amount * penaltyRate * (daysOverdue / 30m);

            return Math.Round(penalty, 2);
        }

        #endregion


        #region Loan Offset with Shares

        public async Task<List<AvailableSharesDTO>> GetAvailableSharesForOffsetAsync(string memberNo, string companyCode)
        {
            try
            {
                _logger.LogInformation($"Getting available shares for offset for member: {memberNo}");

                // Get all share types where UsedToOffset = true
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode && s.UsedToOffset == true)
                    .ToListAsync();

                if (!shareTypes.Any())
                {
                    _logger.LogWarning($"No share types with UsedToOffset=true found for company {companyCode}");
                    return new List<AvailableSharesDTO>();
                }

                var availableShares = new List<AvailableSharesDTO>();

                foreach (var shareType in shareTypes)
                {
                    // ✅ GET DEPOSITSAMOUNT FROM CONTRIBSHARE
                    var totalDeposits = await _context.ContribShares
                        .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.DepositsAmount ?? 0);

                    _logger.LogInformation($"Member {memberNo} - ShareType: {shareType.SharesCode}, Total Deposits: {totalDeposits:C}");

                    if (totalDeposits <= 0) continue;

                    // For offset, member can use ALL deposits (locked guarantee doesn't matter for their own loan)
                    var availableAmount = totalDeposits;

                    if (availableAmount > 0)
                    {
                        availableShares.Add(new AvailableSharesDTO
                        {
                            SharesCode = shareType.SharesCode,
                            SharesType = shareType.SharesType ?? shareType.SharesCode,
                            AvailableAmount = availableAmount,
                            TotalShares = totalDeposits,
                            LockedForGuarantee = 0,
                            IsMainShares = shareType.IsMainShares,
                            UsedToOffset = shareType.UsedToOffset,
                            Withdrawable = shareType.Withdrawable,
                            MinAmount = shareType.MinAmount
                        });

                        _logger.LogInformation($"Available for offset: {shareType.SharesType} - KES {availableAmount:N0}");
                    }
                }

                return availableShares;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting available shares for offset for member {memberNo}");
                throw;
            }
        }

        public async Task<decimal> GetSharesLockedForGuaranteeAsync(string memberNo, string sharesCode, string companyCode)
        {
            try
            {
                // Get all approved guarantor commitments where shares are locked
                var lockedAmount = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo &&
                               g.CompanyCode == companyCode &&
                               g.Transfered == false &&
                               (g.Balance > 0 || (g.Amount > 0 && g.Balance == null)))
                    .SumAsync(g => g.Amount ?? 0);

                return lockedAmount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting locked shares for guarantee for member {memberNo}");
                return 0;
            }
        }

        public async Task<LoanOffsetResponseDTO> OffsetLoanWithSharesAsync(LoanOffsetDTO offsetDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Processing loan offset for loan {offsetDto.LoanNo}, Amount: {offsetDto.AmountToOffset:C}");

                // 1. GET LOAN DATA
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == offsetDto.LoanNo && l.CompanyCode == offsetDto.CompanyCode);

                if (loan == null)
                    throw new InvalidOperationException($"Loan {offsetDto.LoanNo} not found");

                // Store old loan values for audit
                int oldLoanStatus = (int)loan.Status;
                string oldLoanPosted = loan.Posted ?? "";
                decimal oldLoanAamount = loan.Aamount ?? 0;

                if (loan.Status != (int)Status.Disbursed && loan.Status != (int)Status.Endorsed)
                    throw new InvalidOperationException($"Cannot offset loan in status '{loan.Status}'");

                // 2. GET LOAN BALANCE
                var loanbal = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == offsetDto.LoanNo && lb.Companycode == offsetDto.CompanyCode);

                if (loanbal == null)
                    throw new InvalidOperationException($"Loan balance record not found");

                // Store old loanbal values for audit
                decimal oldBalance = loanbal.Balance;
                decimal oldIntrOwed = loanbal.IntrOwed;
                decimal oldPenalty = loanbal.Penalty;
                decimal oldIntBalance = loanbal.IntBalance;

                // 3. GET CURRENT SCHEDULE
                var currentSchedule = await _context.LoanSchedules
                    .Where(s => s.LoanNo == offsetDto.LoanNo && s.Status != "Paid")
                    .OrderBy(s => s.InstallmentNo)
                    .FirstOrDefaultAsync();

                if (currentSchedule == null)
                {
                    if (loanbal.Balance <= 0.01m && loanbal.IntrOwed <= 0.01m)
                        throw new InvalidOperationException("Loan is already fully paid.");
                    throw new InvalidOperationException("No active schedule found for this loan.");
                }

                // Store old schedule values for audit
                decimal oldScheduleOutstandingPrincipal = currentSchedule.OutstandingPrincipal;
                decimal oldScheduleOutstandingInterest = currentSchedule.OutstandingInterest;
                decimal oldScheduleOutstandingTotal = currentSchedule.OutstandingTotal;
                string oldScheduleStatus = currentSchedule.Status;

                // 4. GET LOAN TYPE
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == offsetDto.CompanyCode);

                // 5. VALIDATE SHARE TYPE
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(s => s.SharesCode == offsetDto.SharesCode && s.CompanyCode == offsetDto.CompanyCode);

                if (shareType == null)
                    throw new InvalidOperationException($"Share type {offsetDto.SharesCode} not found");

                if (!shareType.UsedToOffset)
                    throw new InvalidOperationException($"Share type {shareType.SharesType} cannot be used for loan offset");

                // 6. GET DEPOSITS FROM CONTRIBSHARE
                var contribShare = await _context.ContribShares
                    .FirstOrDefaultAsync(cs => cs.MemberNo == offsetDto.MemberNo && cs.CompanyCode == offsetDto.CompanyCode);

                if (contribShare == null || (contribShare.DepositsAmount ?? 0) <= 0)
                    throw new InvalidOperationException($"Member has no deposits available for offset.");

                // Store old contribshare values for audit
                decimal oldDepositsAmount = contribShare.DepositsAmount ?? 0;

                decimal currentDeposits = contribShare.DepositsAmount ?? 0;

                _logger.LogInformation($"Member {offsetDto.MemberNo} - Current Deposits: {currentDeposits:C}");

                if (currentDeposits < offsetDto.AmountToOffset)
                    throw new InvalidOperationException($"Insufficient deposits. Available: {currentDeposits:C}, Requested: {offsetDto.AmountToOffset:C}");

                // 7. CALCULATE PENALTY
                decimal penaltyAmount = 0;
                int daysOverdue = 0;

                if (loanType != null && loanType.Penalty == 1 && DateTime.Now > currentSchedule.DueDate)
                {
                    daysOverdue = (DateTime.Now - currentSchedule.DueDate).Days;
                    int gracePeriodDays = loanType.GracePeriod > 0 ? loanType.GracePeriod : 0;

                    if (daysOverdue > gracePeriodDays)
                    {
                        int overdueDaysAfterGrace = daysOverdue - gracePeriodDays;
                        int overdueMonths = (int)Math.Ceiling(overdueDaysAfterGrace / 30.0);
                        decimal monthlyPenaltyRate = (loanType.Penalty) / 100;
                        penaltyAmount = currentSchedule.OutstandingTotal * monthlyPenaltyRate * overdueMonths;
                        _logger.LogInformation($"Penalty calculated: {penaltyAmount:C}");
                    }
                }

                // 8. ALLOCATE OFFSET AMOUNT - Apply to current AND future installments
                decimal remainingAmount = offsetDto.AmountToOffset;
                decimal penaltyAllocated = 0;
                decimal interestAllocated = 0;
                decimal principalAllocated = 0;
                decimal overpaymentAmount = 0;

                // Get ALL remaining schedules (not just current)
                var allRemainingSchedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == offsetDto.LoanNo && s.Status != "Paid")
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();

                foreach (var schedule in allRemainingSchedules)
                {
                    if (remainingAmount <= 0) break;

                    // Calculate penalty for this schedule if overdue
                    decimal schedulePenalty = 0;
                    if (loanType != null && loanType.Penalty == 1 && DateTime.Now > schedule.DueDate)
                    {
                        int gracePeriodDays = loanType.GracePeriod > 0 ? loanType.GracePeriod : 0;
                        if ((DateTime.Now - schedule.DueDate).Days > gracePeriodDays)
                        {
                            int overdueMonths = (int)Math.Ceiling(((DateTime.Now - schedule.DueDate).Days - gracePeriodDays) / 30.0);
                            decimal monthlyPenaltyRate = (loanType.Penalty) / 100;
                            schedulePenalty = schedule.OutstandingTotal * monthlyPenaltyRate * overdueMonths;
                        }
                    }

                    decimal scheduleTotalDue = schedule.OutstandingPrincipal + schedule.OutstandingInterest + schedulePenalty;

                    if (remainingAmount >= scheduleTotalDue)
                    {
                        // Fully pay this schedule
                        penaltyAllocated += schedulePenalty;
                        interestAllocated += schedule.OutstandingInterest;
                        principalAllocated += schedule.OutstandingPrincipal;
                        remainingAmount -= scheduleTotalDue;

                        // Mark schedule as paid
                        schedule.PaidPrincipal = schedule.PrincipalAmount;
                        schedule.PaidInterest = schedule.InterestAmount;
                        schedule.PaidTotal = schedule.TotalInstallment;
                        schedule.OutstandingPrincipal = 0;
                        schedule.OutstandingInterest = 0;
                        schedule.OutstandingTotal = 0;
                        schedule.Status = "Paid";
                        schedule.PaidDate = DateTime.Now;
                        schedule.PenaltyAmount = (schedule.PenaltyAmount) + schedulePenalty;

                        _logger.LogInformation($"Schedule {schedule.InstallmentNo} fully paid via offset");
                    }
                    else
                    {
                        // Partially pay this schedule
                        // Apply to penalty first
                        if (remainingAmount > 0 && schedulePenalty > 0)
                        {
                            decimal penaltyPart = Math.Min(remainingAmount, schedulePenalty);
                            penaltyAllocated += penaltyPart;
                            remainingAmount -= penaltyPart;
                        }

                        // Apply to interest
                        if (remainingAmount > 0 && schedule.OutstandingInterest > 0)
                        {
                            decimal interestPart = Math.Min(remainingAmount, schedule.OutstandingInterest);
                            interestAllocated += interestPart;
                            schedule.PaidInterest += interestPart;
                            schedule.OutstandingInterest = schedule.InterestAmount - schedule.PaidInterest;
                            remainingAmount -= interestPart;
                        }

                        // Apply to principal
                        if (remainingAmount > 0 && schedule.OutstandingPrincipal > 0)
                        {
                            decimal principalPart = Math.Min(remainingAmount, schedule.OutstandingPrincipal);
                            principalAllocated += principalPart;
                            schedule.PaidPrincipal += principalPart;
                            schedule.OutstandingPrincipal = schedule.PrincipalAmount - schedule.PaidPrincipal;
                            remainingAmount -= principalPart;
                        }

                        schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                        schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;
                        schedule.Status = "Partial";
                        schedule.PenaltyAmount = (schedule.PenaltyAmount) + schedulePenalty;

                        _logger.LogInformation($"Schedule {schedule.InstallmentNo} partially paid via offset");
                        break; // No more money left
                    }
                }

                overpaymentAmount = remainingAmount;

                bool isCurrentInstallmentFullyPaid = (principalAllocated >= currentSchedule.OutstandingPrincipal - 0.01m) &&
                                                      (interestAllocated >= currentSchedule.OutstandingInterest - 0.01m);

                // 9. GENERATE NUMBERS
                string receiptNo = GenerateOffsetReceiptNumber(offsetDto.CompanyCode);
                string transactionNo = DateTime.Now.ToString("yyyyMMddHHmmss") + Guid.NewGuid().ToString().Substring(0, 8);
                int offsetCount = await _context.Repay.CountAsync(r => r.LoanNo == offsetDto.LoanNo && r.Posted == true);
                int paymentNo = offsetCount + 1;

                // 10. CREATE REPAY RECORD
                var offsetRepayment = new Repay
                {
                    LoanNo = offsetDto.LoanNo,
                    MemberNo = offsetDto.MemberNo,
                    CompanyCode = offsetDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    PaymentNo = paymentNo,
                    DateReceived = DateTime.Now,
                    Amount = offsetDto.AmountToOffset,
                    Principal = principalAllocated,
                    Interest = interestAllocated,
                    Penalty = penaltyAllocated,
                    IntrCharged = interestAllocated,
                    IntrOwed = Math.Max(0, loanbal.IntrOwed - interestAllocated),
                    IntrAccrued = currentSchedule.InterestAmount,
                    LoanBalance = Math.Max(0, loanbal.Balance - principalAllocated),
                    RepayRate = currentSchedule.TotalInstallment,
                    Locked = false,
                    Posted = true,
                    Accrued = true,
                    Remarks = $"Loan offset using shares: {shareType.SharesType} - {offsetDto.Remarks}" +
                              (overpaymentAmount > 0 ? $" (Overpayment: KES {overpaymentAmount:N2})" : ""),
                    AuditId = offsetDto.ProcessedBy,
                    AuditTime = DateTime.Now,
                    Transby = offsetDto.ProcessedBy,
                    IntBalance = Math.Max(0, loanbal.IntBalance - interestAllocated),
                    Loancode = loan.LoanCode,
                    Interestaccrued = currentSchedule.InterestAmount,
                    Transno = transactionNo,
                    TransDate = DateTime.Now,
                    TransactionNo = transactionNo,
                    ApiKey = offsetDto.SharesCode,
                    UserName = offsetDto.ProcessedBy,
                    Run = 0,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _context.Repay.Add(offsetRepayment);
                await _context.SaveChangesAsync();

                // 11. UPDATE DEPOSITSAMOUNT
                contribShare.DepositsAmount = currentDeposits - offsetDto.AmountToOffset;
                contribShare.AuditDateTime = DateTime.Now;
                _logger.LogInformation($"Deposits reduced from {currentDeposits:C} to {contribShare.DepositsAmount:C}");

                // 12. UPDATE CURRENT SCHEDULE
                if (isCurrentInstallmentFullyPaid)
                {
                    currentSchedule.PaidPrincipal = currentSchedule.PrincipalAmount;
                    currentSchedule.PaidInterest = currentSchedule.InterestAmount;
                    currentSchedule.PaidTotal = currentSchedule.TotalInstallment;
                    currentSchedule.OutstandingPrincipal = 0;
                    currentSchedule.OutstandingInterest = 0;
                    currentSchedule.OutstandingTotal = 0;
                    currentSchedule.Status = "Paid";
                    currentSchedule.PaidDate = DateTime.Now;
                    currentSchedule.PenaltyAmount = (currentSchedule.PenaltyAmount) + penaltyAllocated;
                }
                else
                {
                    currentSchedule.PaidPrincipal += principalAllocated;
                    currentSchedule.PaidInterest += interestAllocated;
                    currentSchedule.PaidTotal = currentSchedule.PaidPrincipal + currentSchedule.PaidInterest;
                    currentSchedule.OutstandingPrincipal = currentSchedule.PrincipalAmount - currentSchedule.PaidPrincipal;
                    currentSchedule.OutstandingInterest = currentSchedule.InterestAmount - currentSchedule.PaidInterest;
                    currentSchedule.OutstandingTotal = currentSchedule.OutstandingPrincipal + currentSchedule.OutstandingInterest;
                    currentSchedule.Status = "Partial";
                    currentSchedule.PenaltyAmount = (currentSchedule.PenaltyAmount) + penaltyAllocated;
                }

                // 13. UPDATE LOANBAL
                loanbal.Balance = Math.Max(0, loanbal.Balance - principalAllocated);
                loanbal.IntrOwed = Math.Max(0, loanbal.IntrOwed - interestAllocated);
                loanbal.Penalty = Math.Max(0, loanbal.Penalty - penaltyAllocated);
                loanbal.IntBalance = Math.Max(0, loanbal.IntBalance - interestAllocated);
                loanbal.LastDate = DateTime.Now;
                loanbal.Processdate = DateTime.Now;

                if (isCurrentInstallmentFullyPaid)
                {
                    var nextSchedule = await _context.LoanSchedules
                        .Where(s => s.LoanNo == offsetDto.LoanNo && s.InstallmentNo == currentSchedule.InstallmentNo + 1)
                        .FirstOrDefaultAsync();
                    if (nextSchedule != null)
                    {
                        loanbal.Nextduedate = nextSchedule.DueDate;
                        loanbal.Duedate = nextSchedule.DueDate;
                        loanbal.RepayRate = nextSchedule.TotalInstallment;
                    }
                }

                // 14. CHECK IF LOAN IS FULLY PAID
                bool isFullyPaid = loanbal.Balance <= 0.01m && loanbal.IntrOwed <= 0.01m;

                if (isFullyPaid)
                {
                    loanbal.Cleared = true;
                    loan.Status = (int)Status.Closed;
                    loan.Posted = "Closed";
                    loan.Aamount = 0;
                    await ReleaseCollateralGuaranteesForLoanAsync(offsetDto.LoanNo, offsetDto.ProcessedBy);
                    await ReleaseMemberGuarantorsForLoanAsync(offsetDto.LoanNo, offsetDto.ProcessedBy);  // ← NEW: Release member guarantors

                    _logger.LogInformation($"Loan {loan.LoanNo} fully paid via offset and closed. Released collateral and member guarantors.");
                }
                else if (loan.Status == (int)Status.Disbursed)
                {
                    loan.Status = (int)Status.Endorsed;
                    loan.Posted = "Active";
                }

                loan.Aamount = loanbal.Balance;
                loan.UserName = offsetDto.ProcessedBy;
                loan.AuditDateTime = DateTime.Now;
                await _context.SaveChangesAsync();

                // 15. CREATE GL TRANSACTION
                Gltransaction glTransaction = null;  // ✅ DECLARE OUTSIDE THE TRY BLOCK
                try
                {
                    var shareAccount = shareType.SharesAcc;
                    var loanReceivableAccount = loanType?.LoanAcc ?? "LOAN_RECEIVABLE_ACCOUNT";

                    if (!string.IsNullOrEmpty(shareAccount))
                    {
                        glTransaction = new Gltransaction  // ✅ REMOVE 'var' - use existing variable
                        {
                            TransDate = DateTime.Now,
                            Amount = offsetDto.AmountToOffset,
                            DrAccNo = shareAccount,
                            CrAccNo = loanReceivableAccount,
                            Temp = "OFFSET",
                            DocumentNo = receiptNo,
                            Source = "LOAN_OFFSET",
                            CompanyCode = offsetDto.CompanyCode,
                            TransDescript = $"Loan offset using shares - {shareType.SharesType} - Loan {offsetDto.LoanNo}",
                            AuditTime = DateTime.Now,
                            AuditId = offsetDto.ProcessedBy,
                            Cash = 0,
                            DocPosted = 1,
                            TransactionNo = transactionNo,
                            Module = "LOAN",
                            ReconId = 0,
                            AuditDateTime = DateTime.Now
                        };
                        _context.Gltransactions.Add(glTransaction);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error creating GL transaction for loan offset {receiptNo}");
                }

                // 16. CREATE BLOCK AND BLOCKCHAIN TRANSACTION
                string blockHash = Guid.NewGuid().ToString().Replace("-", "");
                if (blockHash.Length < 64) blockHash = blockHash.PadRight(64, '0');
                else if (blockHash.Length > 64) blockHash = blockHash.Substring(0, 64);

                var block = new Block
                {
                    BlockHash = blockHash,
                    PreviousHash = await GetLastBlockHashAsync(),
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
                    TransactionType = "LOAN_OFFSET",
                    LoanNo = offsetDto.LoanNo,
                    MemberNo = offsetDto.MemberNo,
                    ReceiptNo = receiptNo,
                    PaymentNo = paymentNo,
                    Amount = offsetDto.AmountToOffset,
                    InstallmentNo = currentSchedule.InstallmentNo,
                    PenaltyAllocated = penaltyAllocated,
                    InterestAllocated = interestAllocated,
                    PrincipalAllocated = principalAllocated,
                    Overpayment = overpaymentAmount,
                    BalanceAfter = loanbal.Balance,
                    DepositsBefore = currentDeposits,
                    DepositsAfter = contribShare.DepositsAmount,
                    BlockHash = blockHash
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_OFFSET",
                    MemberNo = offsetDto.MemberNo,
                    CompanyCode = offsetDto.CompanyCode,
                    Amount = offsetDto.AmountToOffset,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = receiptNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update all records with BlockchainTxId
                offsetRepayment.BlockchainTxId = blockchainTx.TransactionId;
                loan.BlockchainTxId = blockchainTx.TransactionId;
                loanbal.BlockchainTxId = blockchainTx.TransactionId;
                glTransaction.BlockchainTxId = blockchainTx.TransactionId;
                currentSchedule.BlockchainTxId = blockchainTx.TransactionId;
                contribShare.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN OFFSET
                // ============================================================

                // Get member details for audit
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == offsetDto.MemberNo && m.CompanyCode == offsetDto.CompanyCode);

                string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : offsetDto.MemberNo;

                // Audit Extra Data
                var auditExtraData = new
                {
                    loanNo = offsetDto.LoanNo,
                    memberNo = offsetDto.MemberNo,
                    memberName = memberName,
                    shareTypeCode = offsetDto.SharesCode,
                    shareTypeName = shareType.SharesType,
                    amountOffset = offsetDto.AmountToOffset,
                    depositsBefore = currentDeposits,
                    depositsAfter = contribShare.DepositsAmount,
                    receiptNo = receiptNo,
                    paymentNo = paymentNo,
                    penaltyAllocated = penaltyAllocated,
                    interestAllocated = interestAllocated,
                    principalAllocated = principalAllocated,
                    overpaymentAmount = overpaymentAmount,
                    daysOverdue = daysOverdue,
                    penaltyCalculated = penaltyAmount,
                    remarks = offsetDto.Remarks ?? "",
                    processedBy = offsetDto.ProcessedBy,
                    processedDate = DateTime.Now,
                    loanStatusBefore = oldLoanStatus,
                    loanStatusAfter = loan.Status,
                    loanAmountBefore = oldLoanAamount,
                    loanAmountAfter = loan.Aamount,
                    balanceBefore = oldBalance,
                    balanceAfter = loanbal.Balance,
                    interestOwedBefore = oldIntrOwed,
                    interestOwedAfter = loanbal.IntrOwed,
                    penaltyBefore = oldPenalty,
                    penaltyAfter = loanbal.Penalty,
                    scheduleStatusBefore = oldScheduleStatus,
                    scheduleStatusAfter = currentSchedule.Status,
                    isLoanFullyPaid = isFullyPaid,
                    shareAccount = shareType.SharesAcc,
                    loanReceivableAccount = loanType?.LoanAcc ?? "LOAN_RECEIVABLE_ACCOUNT",
                    blockchainTxId = blockchainTx.TransactionId
                };

                // Repayment Record for Audit (NewValue)
                var offsetRepaymentForAudit = new
                {
                    offsetRepayment.Id,
                    offsetRepayment.LoanNo,
                    offsetRepayment.MemberNo,
                    offsetRepayment.ReceiptNo,
                    offsetRepayment.PaymentNo,
                    offsetRepayment.DateReceived,
                    offsetRepayment.Amount,
                    offsetRepayment.Principal,
                    offsetRepayment.Interest,
                    offsetRepayment.Penalty,
                    offsetRepayment.LoanBalance,
                    offsetRepayment.IntrOwed,
                    offsetRepayment.Remarks,
                    offsetRepayment.Transby,
                    offsetRepayment.TransactionNo,
                    offsetRepayment.ApiKey,
                    CreatedBy = offsetDto.ProcessedBy,
                    CreatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                // Save Audit for Offset Repayment (INSERT)
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Insert,
                    oldModel: null,
                    newModel: offsetRepaymentForAudit,
                    tableName: "Repay",
                    recordId: receiptNo,
                    userId: offsetDto.ProcessedBy,
                    userName: offsetDto.ProcessedBy,
                    companyCode: offsetDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR CONTRIBSHARE UPDATE
                // ============================================================
                var contribshareAuditExtraData = new
                {
                    loanNo = offsetDto.LoanNo,
                    memberNo = offsetDto.MemberNo,
                    memberName = memberName,
                    shareTypeCode = offsetDto.SharesCode,
                    shareTypeName = shareType.SharesType,
                    depositsBefore = oldDepositsAmount,
                    depositsAfter = contribShare.DepositsAmount,
                    amountReduced = offsetDto.AmountToOffset,
                    reason = $"Loan offset - Loan {offsetDto.LoanNo}",
                    processedBy = offsetDto.ProcessedBy,
                    processedDate = DateTime.Now,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var contribshareForAudit = new
                {
                    contribShare.Id,
                    contribShare.MemberNo,
                    contribShare.Sharescode,
                    contribShare.DepositsAmount,
                    contribShare.CompanyCode,
                    UpdatedBy = offsetDto.ProcessedBy,
                    UpdatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { DepositsAmount = oldDepositsAmount },
                    newModel: contribshareForAudit,
                    tableName: "ContribShares",
                    recordId: contribShare.Id.ToString(),
                    userId: offsetDto.ProcessedBy,
                    userName: offsetDto.ProcessedBy,
                    companyCode: offsetDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(contribshareAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOANBAL UPDATE
                // ============================================================
                var loanbalAuditExtraData = new
                {
                    loanNo = offsetDto.LoanNo,
                    balanceBefore = oldBalance,
                    balanceAfter = loanbal.Balance,
                    principalReduction = principalAllocated,
                    interestOwedBefore = oldIntrOwed,
                    interestOwedAfter = loanbal.IntrOwed,
                    interestReduction = interestAllocated,
                    penaltyBefore = oldPenalty,
                    penaltyAfter = loanbal.Penalty,
                    penaltyReduction = penaltyAllocated,
                    nextDueDate = loanbal.Nextduedate,
                    isCleared = loanbal.Cleared,
                    isFullyPaid = isFullyPaid,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanbalForAudit = new
                {
                    loanbal.LoanNo,
                    loanbal.Balance,
                    loanbal.IntrOwed,
                    loanbal.Penalty,
                    loanbal.LastDate,
                    loanbal.Nextduedate,
                    loanbal.Cleared,
                    loanbal.Processdate,
                    UpdatedBy = offsetDto.ProcessedBy,
                    UpdatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Balance = oldBalance, IntrOwed = oldIntrOwed, Penalty = oldPenalty },
                    newModel: loanbalForAudit,
                    tableName: "Loanbal",
                    recordId: loanbal.Id.ToString(),
                    userId: offsetDto.ProcessedBy,
                    userName: offsetDto.ProcessedBy,
                    companyCode: offsetDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanbalAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN STATUS CHANGE (if changed)
                // ============================================================
                if (oldLoanStatus != loan.Status)
                {
                    var loanAuditExtraData = new
                    {
                        loanNo = offsetDto.LoanNo,
                        statusChangedFrom = oldLoanStatus,
                        statusChangedTo = loan.Status,
                        reason = isFullyPaid ? "Loan fully paid via share offset and closed" : "Loan status updated after share offset",
                        receiptNo = receiptNo,
                        paymentNo = paymentNo,
                        amountOffset = offsetDto.AmountToOffset,
                        balanceAfter = loanbal.Balance,
                        interestAfter = loanbal.IntrOwed,
                        shareTypeUsed = shareType.SharesType,
                        blockchainTxId = blockchainTx.TransactionId
                    };

                    var loanForAudit = new
                    {
                        loan.LoanNo,
                        loan.Status,
                        loan.Posted,
                        loan.Aamount,
                        loan.UserName,
                        loan.AuditDateTime,
                        UpdatedBy = offsetDto.ProcessedBy,
                        UpdateReason = isFullyPaid ? "Loan fully paid via share offset" : "Status updated after share offset",
                        BlockchainTxId = blockchainTx.TransactionId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Update,
                        oldModel: new { Status = oldLoanStatus, Posted = oldLoanPosted, Aamount = oldLoanAamount },
                        newModel: loanForAudit,
                        tableName: "Loans",
                        recordId: offsetDto.LoanNo,
                        userId: offsetDto.ProcessedBy,
                        userName: offsetDto.ProcessedBy,
                        companyCode: offsetDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                        blockchainTxId: blockchainTx.TransactionId
                    );
                }

                // ============================================================
                // SAVE AUDIT TRAIL FOR LOAN SCHEDULE UPDATE
                // ============================================================
                var scheduleAuditExtraData = new
                {
                    loanNo = offsetDto.LoanNo,
                    installmentNo = currentSchedule.InstallmentNo,
                    dueDate = currentSchedule.DueDate,
                    principalAmount = currentSchedule.PrincipalAmount,
                    interestAmount = currentSchedule.InterestAmount,
                    totalInstallment = currentSchedule.TotalInstallment,
                    statusBefore = oldScheduleStatus,
                    statusAfter = currentSchedule.Status,
                    outstandingPrincipalBefore = oldScheduleOutstandingPrincipal,
                    outstandingPrincipalAfter = currentSchedule.OutstandingPrincipal,
                    outstandingInterestBefore = oldScheduleOutstandingInterest,
                    outstandingInterestAfter = currentSchedule.OutstandingInterest,
                    outstandingTotalBefore = oldScheduleOutstandingTotal,
                    outstandingTotalAfter = currentSchedule.OutstandingTotal,
                    paidPrincipal = currentSchedule.PaidPrincipal,
                    paidInterest = currentSchedule.PaidInterest,
                    penaltyAmount = currentSchedule.PenaltyAmount,
                    paidDate = currentSchedule.PaidDate,
                    isFullPayment = isCurrentInstallmentFullyPaid,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var scheduleForAudit = new
                {
                    currentSchedule.Id,
                    currentSchedule.LoanNo,
                    currentSchedule.InstallmentNo,
                    currentSchedule.Status,
                    currentSchedule.OutstandingPrincipal,
                    currentSchedule.OutstandingInterest,
                    currentSchedule.OutstandingTotal,
                    currentSchedule.PaidPrincipal,
                    currentSchedule.PaidInterest,
                    currentSchedule.PaidTotal,
                    currentSchedule.PaidDate,
                    currentSchedule.PenaltyAmount,
                    UpdatedBy = offsetDto.ProcessedBy,
                    UpdatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new
                    {
                        Status = oldScheduleStatus,
                        OutstandingPrincipal = oldScheduleOutstandingPrincipal,
                        OutstandingInterest = oldScheduleOutstandingInterest,
                        OutstandingTotal = oldScheduleOutstandingTotal
                    },
                    newModel: scheduleForAudit,
                    tableName: "LoanSchedules",
                    recordId: currentSchedule.Id.ToString(),
                    userId: offsetDto.ProcessedBy,
                    userName: offsetDto.ProcessedBy,
                    companyCode: offsetDto.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(scheduleAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                // ============================================================
                // SAVE AUDIT TRAIL FOR GL TRANSACTION (if created)
                // ============================================================
                if (glTransaction != null)
                {
                    var glAuditExtraData = new
                    {
                        loanNo = offsetDto.LoanNo,
                        receiptNo = receiptNo,
                        paymentNo = paymentNo,
                        amount = offsetDto.AmountToOffset,
                        drAccount = shareType.SharesAcc,
                        crAccount = loanType?.LoanAcc ?? "LOAN_RECEIVABLE_ACCOUNT",
                        transactionType = "LOAN_OFFSET",
                        shareTypeUsed = shareType.SharesType,
                        blockchainTxId = blockchainTx.TransactionId
                    };

                    var glForAudit = new
                    {
                        glTransaction.Id,
                        glTransaction.TransDate,
                        glTransaction.Amount,
                        glTransaction.DrAccNo,
                        glTransaction.CrAccNo,
                        glTransaction.DocumentNo,
                        glTransaction.Source,
                        glTransaction.TransDescript,
                        glTransaction.TransactionNo,
                        CreatedBy = offsetDto.ProcessedBy,
                        CreatedDate = DateTime.Now,
                        BlockchainTxId = blockchainTx.TransactionId
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        oldModel: null,
                        newModel: glForAudit,
                        tableName: "Gltransactions",
                        recordId: glTransaction.Id.ToString(),
                        userId: offsetDto.ProcessedBy,
                        userName: offsetDto.ProcessedBy,
                        companyCode: offsetDto.CompanyCode,
                        module: "LoanManagement",
                        extraData: System.Text.Json.JsonSerializer.Serialize(glAuditExtraData),
                        blockchainTxId: blockchainTx.TransactionId
                    );
                }

                _logger.LogInformation($"Loan offset audit completed for loan {offsetDto.LoanNo}, Receipt: {receiptNo}");

                await transaction.CommitAsync();

                return new LoanOffsetResponseDTO
                {
                    Success = true,
                    Message = $"Successfully offset KES {offsetDto.AmountToOffset:N0} using {shareType.SharesType}",
                    ReceiptNo = receiptNo,
                    PenaltyAllocated = penaltyAllocated,
                    InterestAllocated = interestAllocated,
                    PrincipalAllocated = principalAllocated,
                    BalanceAfter = loanbal.Balance,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing loan offset for loan {offsetDto.LoanNo}");
                throw;
            }
        }

        private string GenerateOffsetReceiptNumber(string companyCode)
        {
            var prefix = "OFF";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            var lastOffset = _context.Repay
                .Where(r => r.CompanyCode == companyCode && r.ReceiptNo != null && r.ReceiptNo.StartsWith($"{prefix}{date}"))
                .OrderByDescending(r => r.ReceiptNo)
                .FirstOrDefault();

            if (lastOffset != null && lastOffset.ReceiptNo != null && lastOffset.ReceiptNo.Length > 11)
            {
                if (int.TryParse(lastOffset.ReceiptNo.Substring(11), out int lastSeq))
                    sequence = lastSeq + 1;
            }

            return $"{prefix}{date}{sequence:D4}";
        }

        private async Task<string> GenerateOffsetReceiptNumberAsync(string companyCode)
        {
            var prefix = "OFF";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            var lastOffset = await _context.Repay
                .Where(r => r.CompanyCode == companyCode &&
                           r.ReceiptNo.StartsWith($"{prefix}{date}") &&
                           r.TransactionNo == "SHARE_OFFSET")
                .OrderByDescending(r => r.ReceiptNo)
                .FirstOrDefaultAsync();

            if (lastOffset != null && lastOffset.ReceiptNo.Length > 11)
            {
                var lastSequence = int.Parse(lastOffset.ReceiptNo.Substring(11));
                sequence = lastSequence + 1;
            }

            return $"{prefix}{date}{sequence:D4}";
        }

        #endregion


        #region State Management

        public async Task<bool> UpdateLoanStatusAsync(string loanNo, string newStatus, string performedBy, string? remarks = null)
        {
            // IMPORTANT: Use GetLoanByNoForDisplayAsync to get the original status without recalculation
            var loan = await GetLoanByNoForDisplayAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);
            var oldStatus = loan.Status;

            _logger.LogInformation($"UpdateLoanStatusAsync - Loan {loanNo}: {oldStatus} -> {newStatus}");

            // Convert string status to int for validation
            int newStatusValue = newStatus switch
            {
                "Draft" => (int)Status.Draft,
                "Submitted" => (int)Status.Submitted,
                "UnderAppraisal" => (int)Status.UnderAppraisal,
                "Approved" => (int)Status.Approved,
                "Endorsed" => (int)Status.Endorsed,
                "Disbursed" => (int)Status.Disbursed,
                "Closed" => (int)Status.Closed,
                "Rejected" => (int)Status.Rejected,
                "WrittenOff" => (int)Status.WrittenOff,
                "Defaulted" => (int)Status.Defaulted,
                _ => throw new InvalidOperationException($"Invalid status: {newStatus}")
            };

            // Validate the transition is allowed
            if (!await CanTransitionAsync(loanNo, newStatusValue))
            {
                throw new InvalidOperationException($"Cannot transition from {oldStatus} to {newStatus}");
            }

            loan.Status = newStatusValue;
            loan.UserName = performedBy;
            loan.AuditDateTime = DateTime.Now;

            await _context.SaveChangesAsync();

            // Record audit trail (comment out if CreateAuditTrailAsync doesn't exist)
            // await CreateAuditTrailAsync(loanNo, oldStatus.ToString(), newStatus.ToString(), "STATUS_CHANGE",
            //     remarks ?? $"Status changed from {oldStatus} to {newStatus}", performedBy, loan.CompanyCode);

            // Record blockchain transaction for status change
            try
            {
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    ChangedBy = performedBy,
                    ChangedAt = DateTime.Now,
                    Remarks = remarks
                };

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "LOAN_STATUS_CHANGE",
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    Amount = 0,
                    Timestamp = DateTime.Now,
                    DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };

                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to record blockchain transaction for status change");
            }

            return true;
        }


        public async Task<bool> CanTransitionAsync(string loanNo, int targetStatus)
        {
            // Use GetLoanByNoForDisplayAsync to get original status
            var loan = await GetLoanByNoForDisplayAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);
            var currentStatus = loan.Status ?? 0;

            // Define valid transitions using Status enum values
            var validTransitions = new Dictionary<int, List<int>>
            {
                { (int)Status.Draft, new List<int> { (int)Status.Submitted, (int)Status.Rejected } },
                { (int)Status.Submitted, new List<int> { (int)Status.UnderAppraisal, (int)Status.Rejected } },
                { (int)Status.UnderAppraisal, new List<int> { (int)Status.Approved, (int)Status.Rejected } },
                { (int)Status.Approved, new List<int> { (int)Status.Endorsed, (int)Status.Rejected } },
                { (int)Status.Endorsed, new List<int> { (int)Status.Disbursed, (int)Status.Rejected } },
                { (int)Status.Rejected, new List<int>() },
                { (int)Status.Disbursed, new List<int> { (int)Status.Endorsed, (int)Status.Closed, (int)Status.WrittenOff } },
                { (int)Status.Endorsed, new List<int> { (int)Status.Closed, (int)Status.WrittenOff } },
                { (int)Status.Closed, new List<int>() },
                { (int)Status.WrittenOff, new List<int>() },
                { (int)Status.Defaulted, new List<int>() }
            };

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(targetStatus);
        }

        #endregion

        #region Validation
        public async Task<(bool IsValid, string Message)> ValidateLoanApplicationAsync(LoanApplicationDTO application)
        {
            // Check if loan type exists
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == application.LoanCode && l.CompanyCode == application.CompanyCode);

            if (loanType == null)
            {
                return (false, "Loan type not found");
            }

            // Check maximum loan amount
            if (application.PrincipalAmount > (loanType.MaxAmount ?? decimal.MaxValue))
            {
                return (false, $"Loan amount exceeds maximum allowed of {loanType.MaxAmount:C}");
            }

            // ============================================================
            // NEW: Check if member has existing loan of same type and if bridging is allowed
            // ============================================================

            bool hasExistingLoanOfType = await _context.Loans
                .AnyAsync(l => l.MemberNo == application.MemberNo &&
                               l.CompanyCode == application.CompanyCode &&
                               l.LoanCode == application.LoanCode &&
                               l.Status != (int)Status.Closed &&
                               l.Status != (int)Status.Rejected &&
                               l.Status != (int)Status.WrittenOff);

            if (hasExistingLoanOfType && loanType.Bridging == 0)
            {
                return (false, $"You already have an active {loanType.LoanType1} loan. This loan type does not allow bridging/refinancing.");
            }

            // Check max number of loans (regardless of type)
            int maxLoansAllowed = loanType.MaxLoans ?? int.MaxValue;

            // Count active loans (not closed/rejected/written off)
            var activeLoansCount = await _context.Loans
                .CountAsync(l => l.MemberNo == application.MemberNo &&
                                l.CompanyCode == application.CompanyCode &&
                                l.Status != (int)Status.Closed &&
                                l.Status != (int)Status.Rejected &&
                                l.Status != (int)Status.WrittenOff);

            if (activeLoansCount >= maxLoansAllowed)
            {
                return (false, $"Member has reached maximum number of active loans ({maxLoansAllowed})");
            }

            return (true, "Validation passed");
        }

        public async Task<(bool IsEligible, string Message)> CheckMemberEligibilityAsync(string memberNo, string loanCode, string companyCode)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                return (false, "Member not found");
            }

            // Check if member is active
            if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
            {
                return (false, "Member is not active");
            }

            // Get the loan type being applied for
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == loanCode && l.CompanyCode == companyCode);

            if (loanType == null)
            {
                return (false, "Loan type not found");
            }

            // ============================================================
            // Check for existing loans of the SAME type - get bridging from the LOAN table
            // ============================================================
            var existingLoanOfType = await _context.Loans
                .FirstOrDefaultAsync(l => l.MemberNo == memberNo &&
                                          l.CompanyCode == companyCode &&
                                          l.LoanCode == loanCode &&
                                          l.Status != (int)Status.Closed &&
                                          l.Status != (int)Status.Rejected &&
                                          l.Status != (int)Status.WrittenOff);

            if (existingLoanOfType != null)
            {
                // If bridging is NOT allowed on the existing loan, block
                if (existingLoanOfType.Bridging != true)
                {
                    return (false, $"You already have an active {loanType.LoanType1} loan. Bridging/refinancing is not allowed for this loan. Please clear your existing loan first.");
                }
                else
                {
                    // Bridging IS allowed - log and allow
                    _logger.LogInformation($"Member {memberNo} has existing {loanCode} loan and bridging is allowed (Bridging={existingLoanOfType.Bridging})");
                }
            }

            // Check existing loan defaults
            var hasDefaulted = await HasPreviousDefaultAsync(memberNo, companyCode);
            if (hasDefaulted)
            {
                return (false, "Member has previous loan defaults");
            }

            return (true, "Member is eligible");
        }

        //public async Task<(bool IsValid, string Message)> ValidateLoanApplicationAsync(LoanApplicationDTO application)
        //{
        //    // Check if loan type exists
        //    var loanType = await _context.Loantypes
        //        .FirstOrDefaultAsync(l => l.LoanCode == application.LoanCode && l.CompanyCode == application.CompanyCode);

        //    if (loanType == null)
        //    {
        //        return (false, "Loan type not found");
        //    }

        //    // Check maximum loan amount
        //    if (application.PrincipalAmount > (loanType.MaxAmount ?? decimal.MaxValue))
        //    {
        //        return (false, $"Loan amount exceeds maximum allowed of {loanType.MaxAmount:C}");
        //    }

        //    // Check if member has existing active loans that exceed limit
        //    var activeLoans = await _context.Loans
        //        .CountAsync(l => l.MemberNo == application.MemberNo &&
        //                        l.CompanyCode == application.CompanyCode &&
        //                        (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed));

        //    if (activeLoans >= (loanType.MaxLoans ?? int.MaxValue))
        //    {
        //        return (false, $"Member has reached maximum number of active loans ({loanType.MaxLoans})");
        //    }

        //    return (true, "Validation passed");
        //}


        //public async Task<(bool IsEligible, string Message)> CheckMemberEligibilityAsync(string memberNo, string loanCode, string companyCode)
        //{
        //    var member = await _context.Members
        //        .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

        //    if (member == null)
        //    {
        //        return (false, "Member not found");
        //    }

        //    // Check if member is active
        //    if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
        //    {
        //        return (false, "Member is not active");
        //    }

        //    // Check minimum contribution period (if applicable)
        //    // This would check how long the member has been contributing

        //    // Check existing loan defaults
        //    var hasDefaulted = await HasPreviousDefaultAsync(memberNo, companyCode);
        //    if (hasDefaulted)
        //    {
        //        return (false, "Member has previous loan defaults");
        //    }

        //    return (true, "Member is eligible");
        //}

        public async Task<decimal> CalculateMaximumLoanAmountAsync(string memberNo, string loanCode, string companyCode)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == loanCode && l.CompanyCode == companyCode);

            if (member == null || loanType == null)
            {
                return 0;
            }

            // Get member's shares value
            var shareValue = await _shareService.GetTotalSharesValueAsync(memberNo);

            // Calculate based on shares (typical SACCO rule: 3x shares or up to max amount)
            var maxByShares = shareValue * 3;

            // Apply loan type maximum
            var maxAmount = Math.Min(maxByShares, loanType.MaxAmount ?? decimal.MaxValue);

            // Consider existing loan balances
            var existingLoans = await _context.Loans
                .Where(l => l.MemberNo == memberNo &&
                           l.CompanyCode == companyCode &&
                           (l.Status == (int)Status.Disbursed || l.Status == (int)Status.Endorsed))
                .SumAsync(l => l.LoanAmt ?? 0);

            maxAmount -= existingLoans;

            return Math.Max(0, maxAmount);
        }

        /// <summary>
        /// Checks if a member can apply for a new loan based on bridging rules from the Loan table
        /// </summary>
        public async Task<(bool CanApply, string Message, int ExistingCount)> CanApplyForLoanTypeAsync(string memberNo, string loanCode, string companyCode)
        {
            // Get the loan type being applied for
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == loanCode && l.CompanyCode == companyCode);

            if (loanType == null)
            {
                return (false, "Loan type not found", 0);
            }

            // Count existing active loans of THIS type
            var existingCount = await _context.Loans
                .CountAsync(l => l.MemberNo == memberNo &&
                                l.CompanyCode == companyCode &&
                                l.LoanCode == loanCode &&
                                l.Status != (int)Status.Closed &&
                                l.Status != (int)Status.Rejected &&
                                l.Status != (int)Status.WrittenOff);

            _logger.LogInformation($"Member {memberNo} has {existingCount} active loan(s) of type {loanCode}");

            if (existingCount > 0)
            {
                // ============================================================
                // KEY FIX: Check the Bridging field on the EXISTING LOAN
                // ============================================================
                var existingLoan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.MemberNo == memberNo &&
                                              l.CompanyCode == companyCode &&
                                              l.LoanCode == loanCode &&
                                              l.Status != (int)Status.Closed &&
                                              l.Status != (int)Status.Rejected &&
                                              l.Status != (int)Status.WrittenOff);

                // Check if bridging is allowed on the existing loan
                bool isBridgingAllowed = existingLoan != null && existingLoan.Bridging == true;

                if (!isBridgingAllowed)
                {
                    return (false,
                        $"You already have an active {loanType.LoanType1} loan. " +
                        $"Bridging/refinancing is not allowed for this loan. " +
                        $"Please clear your existing {loanType.LoanType1} loan before applying for a new one.",
                        existingCount);
                }
                else
                {
                    // Bridging IS allowed on the existing loan - check max loans
                    int maxLoans = loanType.MaxLoans ?? 5;

                    // Count total active loans (all types) for this member
                    var totalActiveLoans = await _context.Loans
                        .CountAsync(l => l.MemberNo == memberNo &&
                                        l.CompanyCode == companyCode &&
                                        l.Status != (int)Status.Closed &&
                                        l.Status != (int)Status.Rejected &&
                                        l.Status != (int)Status.WrittenOff);

                    if (totalActiveLoans >= maxLoans)
                    {
                        return (false,
                            $"You have reached the maximum number of active loans ({maxLoans}). " +
                            $"Please clear some existing loans before applying for a new one.",
                            existingCount);
                    }

                    return (true,
                        $"Bridging is allowed for your existing {loanType.LoanType1} loan. " +
                        $"You have {existingCount} existing loan(s) of this type. " +
                        $"Maximum active loans allowed: {maxLoans}",
                        existingCount);
                }
            }

            // No existing loan of this type - allowed
            return (true, "No existing loan of this type found", 0);
        }

        /// <summary>
        /// Gets the bridging status for a loan type, including existing loans count
        /// </summary>
        public async Task<(bool IsBridgingAllowed, int ExistingLoansCount, string Message)> GetBridgingStatusAsync(
            string memberNo, string loanCode, string companyCode)
        {
            var loanType = await _context.Loantypes
                .FirstOrDefaultAsync(l => l.LoanCode == loanCode && l.CompanyCode == companyCode);

            if (loanType == null)
            {
                return (false, 0, "Loan type not found");
            }

            var existingCount = await _context.Loans
                .CountAsync(l => l.MemberNo == memberNo &&
                                l.CompanyCode == companyCode &&
                                l.LoanCode == loanCode &&
                                l.Status != (int)Status.Closed &&
                                l.Status != (int)Status.Rejected &&
                                l.Status != (int)Status.WrittenOff);

            bool isBridgingAllowed = loanType.Bridging == 1;

            string message;
            if (existingCount > 0 && !isBridgingAllowed)
            {
                message = $"You have {existingCount} existing {loanType.LoanType1} loan(s). " +
                          $"Bridging is not allowed for this loan type. " +
                          $"Please clear your existing loan(s) before applying for a new one.";
            }
            else if (existingCount > 0 && isBridgingAllowed)
            {
                message = $"You have {existingCount} existing {loanType.LoanType1} loan(s). " +
                          $"Bridging is allowed for this loan type.";
            }
            else
            {
                message = "No existing loans of this type found.";
            }

            return (isBridgingAllowed, existingCount, message);
        }

        #endregion

        #region Audit

        //public async Task<List<LoanAuditTrail>> GetLoanAuditTrailAsync(string loanNo)
        //{
        //    var auditTrails = await _context.LoanAuditTrails
        //        .Where(a => a.LoanNo == loanNo)
        //        .OrderByDescending(a => a.PerformedDate)
        //        .ToListAsync();

        //    return auditTrails;
        //}

        #endregion

        #region Private Helper Methods

        private async Task<int> CalculateCreditScore(string memberNo, string companyCode)
        {
            // Simplified credit scoring
            int score = 600; // Base score

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null) return score;

            // Length of membership
            if (member.EffectDate.HasValue)
            {
                var years = (DateTime.Now - member.EffectDate.Value).TotalDays / 365;
                score += (int)(years * 10); // 10 points per year
            }

            // Share capital
            if (member.ShareCap.HasValue)
            {
                if (member.ShareCap > 100000) score += 50;
                else if (member.ShareCap > 50000) score += 30;
                else if (member.ShareCap > 10000) score += 20;
            }

            // Previous loan history
            var previousLoans = await _context.Loans
                .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                .ToListAsync();

            if (previousLoans.Any())
            {
                // Check if any defaults
                var hasDefault = previousLoans.Any(l => l.Status == (int)Status.WrittenOff);
                if (hasDefault) score -= 100;

                // Check repayment history
                var closedLoans = previousLoans.Count(l => l.Status == (int)Status.Closed);
                score += closedLoans * 20;
            }

            return Math.Clamp(score, 300, 850);
        }

        private async Task<bool> HasPreviousDefaultAsync(string memberNo, string companyCode)
        {
            return await _context.Loans
                .AnyAsync(l => l.MemberNo == memberNo &&
                              l.CompanyCode == companyCode &&
                              (l.Status == (int)Status.WrittenOff || l.Status == (int)Status.Defaulted));
        }

        private async Task<int> CalculateLoanHistoryRatingAsync(string memberNo, string companyCode)
        {
            var loans = await _context.Loans
                .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                .ToListAsync();

            if (!loans.Any()) return 3; // No history - average

            var totalLoans = loans.Count;
            var closedLoans = loans.Count(l => l.Status == (int)Status.Closed);

            var repayments = await _context.Repay
                .Where(r => r.MemberNo == memberNo && r.CompanyCode == companyCode && r.Posted == true)
                .ToListAsync();

            var onTimePayments = repayments.Count;

            var rating = 3; // Base

            if (closedLoans == totalLoans && totalLoans > 0) rating += 1;
            if (onTimePayments > 10) rating += 1;

            return Math.Clamp(rating, 1, 5);
        }

        private string GetUserRole(string username)
        {
            // This would fetch user role from your identity system
            return "LoanOfficer";
        }

        private async Task RecalculateLoanBalancesAsync(string loanNo)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            if (loan == null) return;

            var loanbal = await _context.Loanbal
                .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

            if (loanbal == null) return;

            var schedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            // Reset loan balances
            loanbal.Balance = loan.LoanAmt ?? 0;
            loanbal.IntrOwed = 0;
            loanbal.Penalty = 0;

            // Reset schedule balances
            foreach (var schedule in schedules)
            {
                schedule.PaidPrincipal = 0;
                schedule.PaidInterest = 0;
                schedule.PaidTotal = 0;
                schedule.OutstandingPrincipal = schedule.PrincipalAmount;
                schedule.OutstandingInterest = schedule.InterestAmount;
                schedule.OutstandingTotal = schedule.TotalInstallment;
                schedule.Status = schedule.DueDate < DateTime.Now ? "Overdue" : "Pending";
            }

            await _context.SaveChangesAsync();

            // Reapply all completed repayments in order
            var repayments = await _context.Repay
                .Where(r => r.LoanNo == loanNo && r.Posted == true)
                .OrderBy(r => r.DateReceived)
                .ToListAsync();

            foreach (var repayment in repayments)
            {
                // Apply repayment to loanbal
                loanbal.Balance -= repayment.Principal ?? 0;
                loanbal.IntrOwed = Math.Max(0, loanbal.IntrOwed - (repayment.Interest ?? 0));
                loanbal.Penalty = Math.Max(0, loanbal.Penalty - (repayment.Penalty ?? 0));

                // Apply to schedules
                decimal remainingForSchedule = (repayment.Principal ?? 0) + (repayment.Interest ?? 0);

                foreach (var schedule in schedules.Where(s => s.Status != "Paid"))
                {
                    if (remainingForSchedule <= 0) break;

                    decimal scheduleOutstanding = schedule.OutstandingPrincipal + schedule.OutstandingInterest;

                    if (remainingForSchedule >= scheduleOutstanding)
                    {
                        schedule.PaidPrincipal = schedule.PrincipalAmount;
                        schedule.PaidInterest = schedule.InterestAmount;
                        schedule.PaidTotal = schedule.TotalInstallment;
                        schedule.OutstandingPrincipal = 0;
                        schedule.OutstandingInterest = 0;
                        schedule.OutstandingTotal = 0;
                        schedule.Status = "Paid";
                        schedule.PaidDate = repayment.DateReceived;
                        remainingForSchedule -= scheduleOutstanding;
                    }
                    else
                    {
                        if (remainingForSchedule <= schedule.OutstandingInterest)
                        {
                            schedule.PaidInterest = (schedule.PaidInterest) + remainingForSchedule;
                            schedule.OutstandingInterest = schedule.InterestAmount - (schedule.PaidInterest);
                        }
                        else
                        {
                            schedule.PaidInterest = schedule.InterestAmount;
                            schedule.OutstandingInterest = 0;
                            remainingForSchedule -= schedule.OutstandingInterest;

                            schedule.PaidPrincipal = (schedule.PaidPrincipal) + remainingForSchedule;
                            schedule.OutstandingPrincipal = schedule.PrincipalAmount - (schedule.PaidPrincipal);
                        }

                        schedule.PaidTotal = (schedule.PaidTotal) + remainingForSchedule;
                        schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest;
                        schedule.Status = "Partial";
                        remainingForSchedule = 0;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditTrail>> GetLoanAuditTrailAsync(string loanNo)
        {
            try
            {
                return await _context.AuditTrails
                    .Where(a => a.RecordId == loanNo && a.TableName == "Loans")
                    .OrderByDescending(a => a.AuditTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting audit trail for loan {loanNo}");
                return new List<AuditTrail>();
            }
        }

        public async Task<Loanbal?> GetLoanBalanceAsync(string loanNo, string companyCode)
        {
            try
            {
                return await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo && lb.Companycode == companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan balance for loan {loanNo} and company {companyCode}");
                return null;
            }
        }

        public async Task<bool> RejectGuarantorAsync(int guarantorId, string remarks, string rejectedBy)
        {
            var guarantor = await _context.Loanguar
                .FirstOrDefaultAsync(g => g.Id == guarantorId);

            if (guarantor == null)
            {
                throw new InvalidOperationException("Guarantor not found");
            }

            // Store old values for audit
            bool oldTransfered = guarantor.Transfered;
            string oldDescription = guarantor.Description ?? "";
            string oldAuditId = guarantor.AuditId ?? "";
            DateTime? oldAuditTime = guarantor.AuditTime;
            decimal? oldAmount = guarantor.Amount;
            string oldMemberNo = guarantor.MemberNo;
            string oldLoanNo = guarantor.LoanNo;

            if (guarantor.Transfered == true)
            {
                throw new InvalidOperationException($"Cannot reject guarantor that is already transferred");
            }

            guarantor.Transfered = true;
            guarantor.Description = remarks;
            guarantor.AuditTime = DateTime.Now;
            guarantor.AuditId = rejectedBy;
            await _context.SaveChangesAsync();

            // Get member details for audit
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == guarantor.MemberNo && m.CompanyCode == guarantor.CompanyCode);

            string memberName = member != null ? $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim() : guarantor.MemberNo;

            // Get loan details for audit
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo && l.CompanyCode == guarantor.CompanyCode);

            string loanStatus = loan != null ? loan.Status.ToString() : "Unknown";

            var blockchainData = new
            {
                Id = guarantor.Id,
                LoanNo = guarantor.LoanNo,
                GuarantorMemberNo = guarantor.MemberNo,
                GuarantorName = memberName,
                GuaranteeAmount = guarantor.Amount,
                RejectionReason = remarks,
                RejectedBy = rejectedBy,
                RejectionDate = DateTime.Now,
                LoanStatus = loanStatus
            };

            var blockchainTx = new BlockchainTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                TransactionType = "LOAN_GUARANTOR_REJECTED",
                MemberNo = guarantor.MemberNo,
                CompanyCode = guarantor.CompanyCode,
                Amount = guarantor.Amount ?? 0,
                Timestamp = DateTime.Now,
                DataHash = await _blockchainService.GenerateTransactionHash(blockchainData),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                OffChainReferenceId = $"{guarantor.LoanNo}-{guarantor.MemberNo}-rejected",
                Status = "PENDING",
                CreatedAt = DateTime.Now
            };

            _context.BlockchainTransactions.Add(blockchainTx);
            await _context.SaveChangesAsync();

            guarantor.BlockchainTxId = blockchainTx.TransactionId;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Blockchain transaction recorded for guarantor rejection: {blockchainTx.TransactionId}");

            // ============================================================
            // SAVE AUDIT TRAIL FOR GUARANTOR REJECTION
            // ============================================================

            var auditExtraData = new
            {
                guarantorId = guarantor.Id,
                loanNo = guarantor.LoanNo,
                guarantorMemberNo = guarantor.MemberNo,
                guarantorName = memberName,
                guaranteeAmount = oldAmount,
                rejectionReason = remarks,
                rejectedBy = rejectedBy,
                rejectionDate = DateTime.Now,
                loanStatus = loanStatus,
                transferedBefore = oldTransfered,
                transferedAfter = guarantor.Transfered,
                descriptionBefore = oldDescription,
                descriptionAfter = guarantor.Description,
                auditIdBefore = oldAuditId,
                auditIdAfter = guarantor.AuditId,
                auditTimeBefore = oldAuditTime,
                auditTimeAfter = guarantor.AuditTime,
                blockchainTxId = blockchainTx.TransactionId
            };

            var guarantorForAudit = new
            {
                guarantor.Id,
                guarantor.LoanNo,
                guarantor.MemberNo,
                guarantor.Amount,
                guarantor.Transfered,
                guarantor.Description,
                guarantor.AuditId,
                guarantor.AuditTime,
                RejectedBy = rejectedBy,
                RejectionDate = DateTime.Now,
                RejectionReason = remarks,
                BlockchainTxId = blockchainTx.TransactionId
            };

            await _auditService.SaveLogAsync(
                actionType: AuditActionType.Update,
                oldModel: new
                {
                    Transfered = oldTransfered,
                    Description = oldDescription,
                    AuditId = oldAuditId,
                    AuditTime = oldAuditTime
                },
                newModel: guarantorForAudit,
                tableName: "Loanguar",
                recordId: guarantor.Id.ToString(),
                userId: rejectedBy,
                userName: rejectedBy,
                companyCode: guarantor.CompanyCode,
                module: "LoanManagement",
                extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                blockchainTxId: blockchainTx.TransactionId
            );

            // ============================================================
            // ALSO UPDATE LOAN STATUS IF NEEDED (check if all guarantors rejected?)
            // ============================================================

            // Check if this loan has any remaining active guarantors
            var remainingActiveGuarantors = await _context.Loanguar
                .CountAsync(g => g.LoanNo == guarantor.LoanNo && g.Transfered == false && g.CompanyCode == guarantor.CompanyCode);

            _logger.LogInformation($"Remaining active guarantors for loan {guarantor.LoanNo}: {remainingActiveGuarantors}");

            // If loan exists and has no active guarantors, log this for awareness
            if (loan != null && remainingActiveGuarantors == 0)
            {
                var loanAuditExtraData = new
                {
                    loanNo = guarantor.LoanNo,
                    message = "All guarantors have been rejected for this loan",
                    remainingGuarantors = remainingActiveGuarantors,
                    lastRejectedGuarantorId = guarantor.Id,
                    lastRejectedBy = rejectedBy,
                    lastRejectionDate = DateTime.Now,
                    blockchainTxId = blockchainTx.TransactionId
                };

                var loanForAudit = new
                {
                    loan.LoanNo,
                    loan.Status,
                    loan.Posted,
                    Note = "All guarantors have been rejected. Loan may need new guarantors or may be declined.",
                    UpdatedBy = rejectedBy,
                    UpdatedDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: null,
                    newModel: loanForAudit,
                    tableName: "Loans",
                    recordId: guarantor.LoanNo,
                    userId: rejectedBy,
                    userName: rejectedBy,
                    companyCode: guarantor.CompanyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(loanAuditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                _logger.LogWarning($"Loan {guarantor.LoanNo} has no active guarantors after rejection of guarantor {guarantor.Id}");
            }

            _logger.LogInformation($"Guarantor rejection audit completed for guarantor ID: {guarantor.Id}, Loan: {guarantor.LoanNo}");

            return true;
        }

        private async Task CreateAuditTrailAsync(
           string loanNo,
           string? previousStatus,
           string? newStatus,
           string action,
           string description,
           string performedBy,
           string companyCode)
        {
            var audit = new AuditTrail
            {
                CompanyCode = companyCode,
                UserId = performedBy,
                UserName = performedBy,
                ActionType = action,
                ActionDescription = description,
                TableName = "Loans",
                RecordId = loanNo,
                OldValue = previousStatus,
                NewValue = newStatus,
                AuditTime = DateTime.Now,
                Module = "LOAN_MANAGEMENT",
                CorrelationId = Guid.NewGuid().ToString(),
                ExtraData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    LoanNo = loanNo,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus,
                    Action = action
                })
            };

            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();
        }

        public async Task<LoanSchedule> GetCurrentInstallmentAsync(string loanNo)
        {
            return await _context.LoanSchedules
       .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
       .OrderBy(s => s.InstallmentNo)
       .FirstOrDefaultAsync();
        }

        public async Task RecalculateRbalScheduleAsync(string loanNo, decimal newOutstandingBalance)
        {
            var futureSchedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo && s.Status != "Paid")
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            if (!futureSchedules.Any()) return;

            decimal remainingBalance = newOutstandingBalance;
            var loan = await _context.Loans.FirstOrDefaultAsync(l => l.LoanNo == loanNo);
            decimal monthlyRate = (loan.Interest ?? 0) / 100 / 12;

            foreach (var schedule in futureSchedules)
            {
                // Recalculate interest based on new remaining balance
                decimal newInterest = remainingBalance * monthlyRate;

                // Update schedule
                schedule.InterestAmount = newInterest;
                schedule.TotalInstallment = newInterest;
                schedule.MinimumPayment = newInterest;
                schedule.BalancePrincipal = remainingBalance;

                // Balance doesn't reduce in RBAL unless principal is paid
                // (Principal reduction is handled separately)

                remainingBalance = schedule.BalancePrincipal;
            }

            await _context.SaveChangesAsync();
        }

        Task ILoanService.CreateAuditTrailAsync(string loanNo, string? previousStatus, string? newStatus, string action, string description, string performedBy, string companyCode)
        {
            return CreateAuditTrailAsync(loanNo, previousStatus, newStatus, action, description, performedBy, companyCode);
        }

        #endregion



        #region Guarantor Reports

        public async Task<List<GuarantorsPerLoanReportDTO>> GetGuarantorsPerLoanReportAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Build query for loans with guarantors
                var loansQuery = _context.Loans
                    .Where(l => l.CompanyCode == companyCode)
                    .Where(l => _context.Loanguar.Any(g => g.LoanNo == l.LoanNo && g.CompanyCode == companyCode));

                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    var start = startDate.Value.Date;
                    loansQuery = loansQuery.Where(l => l.ApplicDate >= start);
                }

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                    loansQuery = loansQuery.Where(l => l.ApplicDate <= end);
                }

                // Get all data in one go with joins - MUCH MORE EFFICIENT
                var query = from loan in loansQuery
                            join member in _context.Members on new { loan.MemberNo, loan.CompanyCode } equals new { MemberNo = member.MemberNo, CompanyCode = member.CompanyCode } into memberJoin
                            from member in memberJoin.DefaultIfEmpty()
                            join guarantor in _context.Loanguar on new { loan.LoanNo, loan.CompanyCode } equals new { guarantor.LoanNo, guarantor.CompanyCode } into guarantorJoin
                            from guarantor in guarantorJoin.DefaultIfEmpty()
                            join guarantorMember in _context.Members on new { MemberNo = guarantor.MemberNo, CompanyCode = guarantor.CompanyCode } equals new { MemberNo = guarantorMember.MemberNo, CompanyCode = guarantorMember.CompanyCode } into guarantorMemberJoin
                            from guarantorMember in guarantorMemberJoin.DefaultIfEmpty()
                            select new
                            {
                                Loan = loan,
                                Member = member,
                                Guarantor = guarantor,
                                GuarantorMember = guarantorMember
                            };

                var results = await query.ToListAsync();

                // Group by loan and build DTOs
                var groupedResults = results
                    .GroupBy(x => x.Loan.LoanNo)
                    .Select(group => new
                    {
                        Loan = group.First().Loan,
                        Member = group.First().Member,
                        Guarantors = group.Where(x => x.Guarantor != null)
                            .Select(x => new GuarantorDetailDTO
                            {
                                Id = x.Guarantor.Id,
                                GuarantorMemberNo = x.Guarantor.MemberNo ?? "",
                                GuarantorName = GetMemberFullName(x.GuarantorMember),
                                IdNo = x.GuarantorMember?.Idno,
                                PhoneNo = x.GuarantorMember?.PhoneNo ?? x.GuarantorMember?.MobileNo,
                                GuaranteeAmount = x.Guarantor.Amount ?? 0,
                                Balance = x.Guarantor.Balance,
                                Collateral = x.Guarantor.Collateral,
                                Description = x.Guarantor.Description,
                                Transfered = x.Guarantor.Transfered,
                                Transdate = x.Guarantor.Transdate,
                                AuditTime = x.Guarantor.AuditTime,
                                AuditId = x.Guarantor.AuditId
                            }).ToList()
                    }).ToList();

                var result = new List<GuarantorsPerLoanReportDTO>();

                foreach (var item in groupedResults)
                {
                    decimal totalGuaranteeAmount = item.Guarantors.Sum(g => g.GuaranteeAmount);
                    decimal loanAmount = item.Loan.LoanAmt ?? item.Loan.Aamount ?? 0;
                    bool isFullyGuaranteed = totalGuaranteeAmount >= loanAmount;

                    result.Add(new GuarantorsPerLoanReportDTO
                    {
                        LoanNo = item.Loan.LoanNo,
                        MemberNo = item.Loan.MemberNo,
                        MemberName = GetMemberFullName(item.Member),
                        LoanAmount = loanAmount,
                        LoanStatus = GetLoanStatusString(item.Loan.Status),
                        ApplicationDate = item.Loan.ApplicDate,
                        DisbursementDate = item.Loan.AuditTime,
                        TotalGuaranteeAmount = totalGuaranteeAmount,
                        IsFullyGuaranteed = isFullyGuaranteed,
                        Guarantors = item.Guarantors
                    });
                }

                return result.OrderByDescending(r => r.ApplicationDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guarantors per loan report");
                throw;
            }
        }


        public async Task<List<AllGuarantorsReportDTO>> GetAllGuarantorsReportAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Build query for all guarantors
                var guarantorsQuery = _context.Loanguar
                    .Where(g => g.CompanyCode == companyCode);

                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    var start = startDate.Value.Date;
                    guarantorsQuery = guarantorsQuery.Where(g => g.AuditTime >= start);
                }

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                    guarantorsQuery = guarantorsQuery.Where(g => g.AuditTime <= end);
                }

                var guarantors = await guarantorsQuery
                    .OrderByDescending(g => g.AuditTime)
                    .ThenBy(g => g.LoanNo)
                    .ToListAsync();

                if (!guarantors.Any())
                {
                    return new List<AllGuarantorsReportDTO>();
                }

                var result = new List<AllGuarantorsReportDTO>();

                // Get all unique loan numbers to fetch loan details
                var loanNos = guarantors
                    .Where(g => !string.IsNullOrEmpty(g.LoanNo))
                    .Select(g => g.LoanNo)
                    .Distinct()
                    .ToList();

                // Get all unique member numbers (both borrowers and guarantors)
                var memberNos = new List<string>();
                var loans = new Dictionary<string, Loan>();

                if (loanNos.Any())
                {
                    // Get loans
                    var loanList = await _context.Loans
                        .Where(l => loanNos.Contains(l.LoanNo) && l.CompanyCode == companyCode)
                        .ToListAsync();

                    loans = loanList.ToDictionary(l => l.LoanNo, l => l);

                    // Get borrower member numbers
                    var borrowerNos = loanList
                        .Where(l => !string.IsNullOrEmpty(l.MemberNo))
                        .Select(l => l.MemberNo)
                        .Distinct()
                        .ToList();

                    memberNos.AddRange(borrowerNos);
                }

                // Get guarantor member numbers
                var guarantorNos = guarantors
                    .Where(g => !string.IsNullOrEmpty(g.MemberNo))
                    .Select(g => g.MemberNo)
                    .Distinct()
                    .ToList();

                memberNos.AddRange(guarantorNos);

                // Get all members
                var membersDict = new Dictionary<string, Models.Member>();
                if (memberNos.Any())
                {
                    var members = await _context.Members
                        .Where(m => memberNos.Contains(m.MemberNo) && m.CompanyCode == companyCode)
                        .ToListAsync();

                    membersDict = members.ToDictionary(m => m.MemberNo, m => m);
                }

                // Process each guarantor record
                foreach (var g in guarantors)
                {
                    try
                    {
                        // Get loan details
                        Loan loan = null;
                        if (!string.IsNullOrEmpty(g.LoanNo) && loans.ContainsKey(g.LoanNo))
                        {
                            loan = loans[g.LoanNo];
                        }

                        // Get borrower details
                        string borrowerName = "N/A";
                        string borrowerNo = "";
                        decimal loanAmount = 0;
                        string loanStatus = "Unknown";

                        if (loan != null)
                        {
                            borrowerNo = loan.MemberNo ?? "";
                            loanAmount = loan.LoanAmt ?? loan.Aamount ?? 0;
                            loanStatus = GetLoanStatusString(loan.Status);

                            if (!string.IsNullOrEmpty(loan.MemberNo) && membersDict.ContainsKey(loan.MemberNo))
                            {
                                borrowerName = GetMemberFullName(membersDict[loan.MemberNo]);
                            }
                        }

                        // Get guarantor details
                        string guarantorName = "N/A";
                        string guarantorIdNo = null;
                        string guarantorPhone = null;

                        if (!string.IsNullOrEmpty(g.MemberNo) && membersDict.ContainsKey(g.MemberNo))
                        {
                            var guarantor = membersDict[g.MemberNo];
                            guarantorName = GetMemberFullName(guarantor);
                            guarantorIdNo = guarantor.Idno;
                            guarantorPhone = guarantor.PhoneNo ?? guarantor.MobileNo;
                        }

                        result.Add(new AllGuarantorsReportDTO
                        {
                            LoanNo = g.LoanNo ?? "",
                            MemberNo = borrowerNo,
                            MemberName = borrowerName,
                            GuarantorMemberNo = g.MemberNo ?? "",
                            GuarantorName = guarantorName,
                            GuarantorIdNo = guarantorIdNo,
                            GuarantorPhone = guarantorPhone,
                            GuaranteeAmount = g.Amount ?? 0,
                            OutstandingBalance = g.Balance,
                            Collateral = g.Collateral,
                            Description = g.Description,
                            Transfered = g.Transfered,
                            AssignedDate = g.AuditTime,
                            LoanAmount = loanAmount,
                            LoanStatus = loanStatus
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing guarantor record for Loan: {g.LoanNo}, Member: {g.MemberNo}");
                        // Continue processing other records
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all guarantors report");
                throw;
            }
        }

        private string GetMemberFullName(Models.Member member)
        {
            if (member == null) return "N/A";

            try
            {
                // Try multiple possible property names
                string surname = member.Surname ?? "";
                string otherNames = member.OtherNames ?? "";
                string fullName = "";

                // Try different combinations
                if (!string.IsNullOrWhiteSpace(surname) && !string.IsNullOrWhiteSpace(otherNames))
                {
                    fullName = $"{surname} {otherNames}".Trim();
                }
                else if (!string.IsNullOrWhiteSpace(surname))
                {
                    fullName = surname;
                }
                else if (!string.IsNullOrWhiteSpace(otherNames))
                {
                    fullName = otherNames;
                }
                else
                {
                    // Try to get FullName property if it exists
                    var fullNameProp = member.GetType().GetProperty("FullName");
                    if (fullNameProp != null)
                    {
                        var fullNameValue = fullNameProp.GetValue(member) as string;
                        if (!string.IsNullOrWhiteSpace(fullNameValue))
                        {
                            fullName = fullNameValue;
                        }
                    }
                }

                return string.IsNullOrWhiteSpace(fullName) ? member.MemberNo ?? "N/A" : fullName;
            }
            catch
            {
                return member.MemberNo ?? "N/A";
            }
        }

        //private string GetLoanStatusString(int? status)
        //{
        //    if (!status.HasValue) return "Unknown";

        //    return status switch
        //    {
        //        0 => "Draft",
        //        1 => "Submitted",
        //        2 => "Under Appraisal",
        //        3 => "Approved",
        //        4 => "Endorsed",
        //        5 => "Disbursed",
        //        6 => "Closed",
        //        7 => "Defaulted",
        //        8 => "Written Off",
        //        9 => "Rejected",
        //        _ => "Unknown"
        //    };
        //}

        //public async Task<List<AllGuarantorsReportDTO>> GetAllGuarantorsReportAsync(string companyCode, DateTime? startDate = null, DateTime? endDate = null)
        //{
        //    try
        //    {
        //        // Build query for all guarantors
        //        var guarantorsQuery = _context.Loanguar
        //            .Where(g => g.CompanyCode == companyCode);

        //        // Apply date filters if provided
        //        if (startDate.HasValue)
        //        {
        //            var start = startDate.Value.Date;
        //            guarantorsQuery = guarantorsQuery.Where(g => g.AuditTime >= start);
        //        }

        //        if (endDate.HasValue)
        //        {
        //            var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
        //            guarantorsQuery = guarantorsQuery.Where(g => g.AuditTime <= end);
        //        }

        //        var guarantors = await guarantorsQuery
        //            .OrderByDescending(g => g.AuditTime)
        //            .ThenBy(g => g.LoanNo)
        //            .ToListAsync();

        //        var result = new List<AllGuarantorsReportDTO>();

        //        // Get all unique loan numbers to fetch loan details
        //        var loanNos = guarantors.Select(g => g.LoanNo).Distinct().ToList();

        //        var loans = await _context.Loans
        //            .Where(l => loanNos.Contains(l.LoanNo) && l.CompanyCode == companyCode)
        //            .ToDictionaryAsync(l => l.LoanNo, l => l);

        //        var members = await _context.Members
        //            .Where(m => m.CompanyCode == companyCode)
        //            .ToDictionaryAsync(m => m.MemberNo, m => m);

        //        foreach (var g in guarantors)
        //        {
        //            // Get borrower (loan applicant) details
        //            var loan = loans.ContainsKey(g.LoanNo) ? loans[g.LoanNo] : null;
        //            var borrower = loan != null && members.ContainsKey(loan.MemberNo) ? members[loan.MemberNo] : null;
        //            string borrowerName = GetMemberFullName(borrower);

        //            // Get guarantor details
        //            var guarantor = members.ContainsKey(g.MemberNo) ? members[g.MemberNo] : null;
        //            string guarantorName = GetMemberFullName(guarantor);

        //            result.Add(new AllGuarantorsReportDTO
        //            {
        //                LoanNo = g.LoanNo ?? "",
        //                MemberNo = loan?.MemberNo ?? "",
        //                MemberName = borrowerName,
        //                GuarantorMemberNo = g.MemberNo ?? "",
        //                GuarantorName = guarantorName,
        //                GuarantorIdNo = guarantor?.Idno,
        //                GuarantorPhone = guarantor?.PhoneNo ?? guarantor?.MobileNo,
        //                GuaranteeAmount = g.Amount ?? 0,
        //                OutstandingBalance = g.Balance,
        //                Collateral = g.Collateral,
        //                Description = g.Description,
        //                Transfered = g.Transfered,
        //                AssignedDate = g.AuditTime,
        //                LoanAmount = loan?.LoanAmt ?? loan?.Aamount ?? 0,
        //                LoanStatus = GetLoanStatusString(loan?.Status)
        //            });
        //        }

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting all guarantors report");
        //        throw;
        //    }
        //}

        //private string GetMemberFullName(SACCOBlockChainSystem.Models.Member? member)
        //{
        //    if (member == null) return "N/A";

        //    // Use Surname and OtherNames since FullName is not a database column
        //    string surname = member.Surname ?? "";
        //    string otherNames = member.OtherNames ?? "";
        //    string fullName = $"{surname} {otherNames}".Trim();

        //    if (string.IsNullOrWhiteSpace(fullName))
        //        return "N/A";

        //    return fullName;
        //}

        private string GetLoanStatusString(int? status)
        {
            if (!status.HasValue) return "Unknown";

            return status switch
            {
                (int)Status.Draft => "Draft",
                (int)Status.Submitted => "Submitted",
                (int)Status.UnderAppraisal => "Under Appraisal",
                (int)Status.Approved => "Approved",
                (int)Status.Endorsed => "Endorsed",
                (int)Status.Disbursed => "Disbursed",
                (int)Status.Closed => "Closed",
                (int)Status.Defaulted => "Defaulted",
                (int)Status.WrittenOff => "Written Off",
                (int)Status.Rejected => "Rejected",
                _ => "Unknown"
            };
        }

        #endregion
    }
}