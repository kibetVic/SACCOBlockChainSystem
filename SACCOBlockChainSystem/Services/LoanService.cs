using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Helpers;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public class LoanService : ILoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IMemberService _memberService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<LoanService> _logger;
        private readonly IAuditService _auditService;

        public LoanService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IMemberService memberService,
            ICompanyContextService companyContextService,
            ILogger<LoanService> logger,
            IAuditService auditService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _memberService = memberService;
            _companyContextService = companyContextService;
            _logger = logger;
            _auditService = auditService;
        }

        public async Task<LoanResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Starting loan application for member: {application.MemberNo}");

                // Validate member exists and is active
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == application.MemberNo &&
                                             m.CompanyCode == currentCompanyCode);

                if (member == null)
                    throw new Exception($"Member {application.MemberNo} not found in company {currentCompanyCode}");

                if (member.Status != 1)
                    throw new Exception($"Member {application.MemberNo} is not active");

                // Get loan type details
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == application.LoanCode &&
                                              lt.CompanyCode == currentCompanyCode);

                if (loanType == null)
                    throw new Exception($"Loan type {application.LoanCode} not found");

                // Check member eligibility
                var eligibility = await GetMemberLoanEligibilityAsync(application.MemberNo);
                if (eligibility < application.LoanAmount)
                    throw new Exception($"Loan amount exceeds eligibility. Maximum: {eligibility}");

                // Check if member has active loans
                var activeLoans = await _context.Loans
                    .Where(l => l.MemberNo == application.MemberNo &&
                               l.CompanyCode == currentCompanyCode &&
                               l.Status < 6 && l.Status != 7) // Not disbursed or rejected
                    .ToListAsync();

                if (activeLoans.Any())
                    throw new Exception("Member has active loan applications");

                // Generate loan number
                var loanNo = GenerateLoanNumber(currentCompanyCode);

                // Calculate insurance if applicable
                decimal? insurance = null;
                if (loanType.Insurance != null)
                {
                    insurance = application.LoanAmount * (decimal.Parse(loanType.Insurance) / 100);
                }

                // Create loan record
                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanCode,
                    ApplicDate = DateTime.Now,
                    LoanAmt = application.LoanAmount,
                    RepayPeriod = application.RepayPeriod,
                    Purpose = application.Purpose,
                    Sourceofrepayment = application.SourceOfRepayment,
                    WitMemberNo = application.WitMemberNo,
                    SupMemberNo = application.SupMemberNo,
                    CompanyCode = currentCompanyCode,
                    Status = 1, // Application
                    StatusDescription = LoanStatusHelper.GetStatusDescription(1),
                    PreparedBy = application.CreatedBy,
                    AuditId = application.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    Insurance = insurance,
                    RepayMethod = loanType.Repaymethod ?? "Monthly",
                    Interest = loanType.Interest != null ? decimal.Parse(loanType.Interest) : 12.0m // Default 12%
                };

                _context.Loans.Add(loan);
                await _context.SaveChangesAsync();

                // Add guarantors if provided
                if (application.Guarantors.Any())
                {
                    foreach (var guarantorDto in application.Guarantors)
                    {
                        var guarantorMember = await _context.Members
                            .FirstOrDefaultAsync(m => m.MemberNo == guarantorDto.MemberNo &&
                                                     m.CompanyCode == currentCompanyCode);

                        if (guarantorMember == null)
                            throw new Exception($"Guarantor member {guarantorDto.MemberNo} not found");

                        var loanGuar = new Loanguar
                        {
                            LoanNo = loanNo,
                            MemberNo = guarantorDto.MemberNo,
                            Amount = guarantorDto.GuaranteedAmount,
                            Balance = guarantorDto.GuaranteedAmount,
                            Collateral = guarantorDto.Collateral,
                            Description = guarantorDto.Description,
                            FullNames = guarantorDto.FullNames,
                            CompanyCode = currentCompanyCode,
                            AuditId = application.CreatedBy,
                            AuditTime = DateTime.Now,
                            Transfered = false
                        };

                        _context.Loanguars.Add(loanGuar);
                    }
                    await _context.SaveChangesAsync();
                }

                // Create initial loan balance record
                var loanBal = new Loanbal
                {
                    LoanNo = loanNo,
                    LoanCode = application.LoanCode,
                    MemberNo = application.MemberNo,
                    Balance = application.LoanAmount,
                    IntrOwed = 0,
                    Installments = application.LoanAmount / application.RepayPeriod,
                    IntrOwed2 = 0,
                    FirstDate = DateTime.Now,
                    RepayRate = application.LoanAmount / application.RepayPeriod,
                    LastDate = DateTime.Now,
                    Duedate = DateTime.Now.AddMonths(1),
                    IntrCharged = 0,
                    Interest = loan.Interest ?? 12.0m,
                    Companycode = currentCompanyCode,
                    Penalty = 0,
                    RepayMethod = loanType.Repaymethod ?? "Monthly",
                    Cleared = false,
                    AutoCalc = true,
                    IntrAmount = 0,
                    RepayPeriod = application.RepayPeriod,
                    AuditId = application.CreatedBy,
                    AuditTime = DateTime.Now,
                    IntBalance = 0,
                    InterestAccrued = 0,
                    RepayMode = 1, // Monthly
                    Nextduedate = DateTime.Now.AddMonths(1),
                    Processdate = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                _context.Loanbals.Add(loanBal);
                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    LoanType = application.LoanCode,
                    LoanAmount = application.LoanAmount,
                    RepayPeriod = application.RepayPeriod,
                    Purpose = application.Purpose,
                    ApplicationDate = DateTime.Now,
                    CompanyCode = currentCompanyCode,
                    CreatedBy = application.CreatedBy,
                    Guarantors = application.Guarantors.Select(g => new
                    {
                        g.MemberNo,
                        g.FullNames,
                        g.GuaranteedAmount
                    })
                };

                _logger.LogInformation($"Creating blockchain transaction for loan: {loanNo}");

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_APPLICATION",
                    application.MemberNo,
                    currentCompanyCode,
                    application.LoanAmount,
                    loanNo,
                    blockchainData
                );

                if (blockchainTx != null)
                {
                    loan.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "CREATE",
                    null,
                    $"Loan application created for {application.LoanAmount}",
                    application.CreatedBy,
                    application.CreatedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan application {loanNo} created successfully");

                return await GetLoanResponseDTO(loanNo);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating loan application");
                throw;
            }
        }

        public async Task<LoanResponseDTO> GetLoanAsync(string loanNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var loan = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Include(l => l.LoanGuarantors)
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                         l.CompanyCode == currentCompanyCode);

            if (loan == null)
                throw new Exception($"Loan {loanNo} not found");

            return await MapToLoanResponseDTO(loan);
        }

        public async Task<List<LoanResponseDTO>> GetLoansByMemberAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var loans = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Where(l => l.MemberNo == memberNo &&
                           l.CompanyCode == currentCompanyCode)
                .OrderByDescending(l => l.ApplicDate)
                .ToListAsync();

            var result = new List<LoanResponseDTO>();

            foreach (var loan in loans)
            {
                result.Add(await MapToLoanResponseDTO(loan));
            }

            return result;
        }

        public async Task<List<LoanResponseDTO>> SearchLoansAsync(LoanSearchDTO search)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var query = _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Where(l => l.CompanyCode == currentCompanyCode)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(search.MemberNo))
                query = query.Where(l => l.MemberNo.Contains(search.MemberNo));

            if (!string.IsNullOrEmpty(search.LoanNo))
                query = query.Where(l => l.LoanNo.Contains(search.LoanNo));

            if (search.Status.HasValue)
                query = query.Where(l => l.Status == search.Status);

            if (search.FromDate.HasValue)
                query = query.Where(l => l.ApplicDate >= search.FromDate);

            if (search.ToDate.HasValue)
                query = query.Where(l => l.ApplicDate <= search.ToDate);

            if (!string.IsNullOrEmpty(search.LoanCode))
                query = query.Where(l => l.LoanCode == search.LoanCode);

            var loans = await query
                .OrderByDescending(l => l.ApplicDate)
                .Take(100)
                .ToListAsync();

            var result = new List<LoanResponseDTO>();

            foreach (var loan in loans)
            {
                result.Add(await MapToLoanResponseDTO(loan));
            }

            return result;
        }

        public async Task<LoanResponseDTO> UpdateLoanStatusAsync(string loanNo, LoanUpdateDTO update)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == currentCompanyCode);

                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                // Validate status transition
                if (!LoanStatusHelper.IsValidTransition(loan.Status ?? 1, update.Status))
                    throw new Exception($"Invalid status transition from {loan.StatusDescription} to {LoanStatusHelper.GetStatusDescription(update.Status)}");

                // Update loan status
                var oldStatus = loan.StatusDescription;
                loan.Status = update.Status;
                loan.StatusDescription = update.StatusDescription ?? LoanStatusHelper.GetStatusDescription(update.Status);
                loan.AuditId = update.UpdatedBy;
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                // Update additional fields based on status
                switch (update.Status)
                {
                    case 2: // Guarantors
                        loan.GuarantorDate = DateTime.Now;
                        loan.GuarantorBy = update.UpdatedBy;
                        break;
                    case 3: // Appraisal
                        loan.AppraisalDate = DateTime.Now;
                        loan.AppraisalBy = update.UpdatedBy;
                        if (update.AppraisedAmount.HasValue)
                            loan.Aamount = update.AppraisedAmount;
                        break;
                    case 4: // Endorsement
                        loan.EndorsementDate = DateTime.Now;
                        loan.EndorsementBy = update.UpdatedBy;
                        break;
                    case 5: // Approved
                        loan.AuditDateTime = DateTime.Now;
                        loan.PreparedBy = update.UpdatedBy;
                        if (update.ApprovedAmount.HasValue)
                            loan.Aamount = update.ApprovedAmount;
                        if (update.InterestRate.HasValue)
                            loan.Interest = update.InterestRate;
                        break;
                    case 6: // Disbursed
                        loan.DisbursementDate = DateTime.Now;
                        loan.DisbursementBy = update.UpdatedBy;
                        break;
                    case 7: // Rejected
                        loan.RejectionReason = update.RejectionReason;
                        break;
                }

                await _context.SaveChangesAsync();

                // Create blockchain transaction for status update
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    OldStatus = oldStatus,
                    NewStatus = loan.StatusDescription,
                    UpdatedBy = update.UpdatedBy,
                    UpdateTime = DateTime.Now,
                    Remarks = update.Remarks,
                    ApprovedAmount = update.ApprovedAmount,
                    InterestRate = update.InterestRate,
                    CompanyCode = currentCompanyCode
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_STATUS_UPDATE",
                    loan.MemberNo,
                    currentCompanyCode,
                    0,
                    loanNo,
                    blockchainData
                );

                if (blockchainTx != null)
                {
                    loan.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "UPDATE",
                    oldStatus,
                    loan.StatusDescription,
                    update.UpdatedBy,
                    update.UpdatedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {loanNo} status updated to {loan.StatusDescription}");

                return await GetLoanResponseDTO(loanNo);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating loan status for {loanNo}");
                throw;
            }
        }

        public async Task<bool> DeleteLoanAsync(string loanNo, string deletedBy)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == currentCompanyCode);

                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                // Only allow deletion of applications in early stages
                if (!LoanStatusHelper.CanBeModified(loan.Status ?? 1))
                    throw new Exception($"Cannot delete loan in {loan.StatusDescription} status");

                // Remove related records
                var guarantors = await _context.Loanguars
                    .Where(g => g.LoanNo == loanNo)
                    .ToListAsync();

                var loanBal = await _context.Loanbals
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                if (guarantors.Any())
                    _context.Loanguars.RemoveRange(guarantors);

                if (loanBal != null)
                    _context.Loanbals.Remove(loanBal);

                // Create blockchain transaction before deletion
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    LoanAmount = loan.LoanAmt,
                    Status = loan.StatusDescription,
                    DeletedBy = deletedBy,
                    DeletionTime = DateTime.Now,
                    CompanyCode = currentCompanyCode,
                    Reason = "Loan application deleted"
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_DELETION",
                    loan.MemberNo,
                    currentCompanyCode,
                    0,
                    loanNo,
                    blockchainData
                );

                // Remove loan
                _context.Loans.Remove(loan);
                await _context.SaveChangesAsync();

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "DELETE",
                    loan.StatusDescription,
                    "DELETED",
                    deletedBy,
                    deletedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan {loanNo} deleted successfully");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting loan {loanNo}");
                throw;
            }
        }

        // Workflow Methods
        public async Task<LoanResponseDTO> SubmitForGuarantorsAsync(string loanNo, string submittedBy)
        {
            var update = new LoanUpdateDTO
            {
                Status = 2,
                UpdatedBy = submittedBy,
                StatusDescription = "Submitted for guarantor approval"
            };

            return await UpdateLoanStatusAsync(loanNo, update);
        }

        public async Task<LoanResponseDTO> SubmitForAppraisalAsync(string loanNo, string submittedBy)
        {
            var update = new LoanUpdateDTO
            {
                Status = 3,
                UpdatedBy = submittedBy,
                StatusDescription = "Submitted for appraisal"
            };

            return await UpdateLoanStatusAsync(loanNo, update);
        }

        public async Task<LoanResponseDTO> SubmitForEndorsementAsync(string loanNo, string submittedBy)
        {
            var update = new LoanUpdateDTO
            {
                Status = 4,
                UpdatedBy = submittedBy,
                StatusDescription = "Submitted for endorsement"
            };

            return await UpdateLoanStatusAsync(loanNo, update);
        }

        public async Task<LoanResponseDTO> ApproveLoanAsync(string loanNo, LoanUpdateDTO approval)
        {
            approval.Status = 5;
            approval.StatusDescription = approval.StatusDescription ?? "Loan Approved";

            return await UpdateLoanStatusAsync(loanNo, approval);
        }

        public async Task<LoanResponseDTO> RejectLoanAsync(string loanNo, LoanUpdateDTO rejection)
        {
            rejection.Status = 7;
            rejection.StatusDescription = rejection.StatusDescription ?? "Loan Rejected";

            return await UpdateLoanStatusAsync(loanNo, rejection);
        }

        public async Task<LoanResponseDTO> DisburseLoanAsync(string loanNo, LoanDisbursementDTO disbursement)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await _context.Loans
                    .Include(l => l.Member)
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == currentCompanyCode);

                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                if (loan.Status != 5) // Must be approved
                    throw new Exception("Loan must be approved before disbursement");

                // Update loan for disbursement
                var update = new LoanUpdateDTO
                {
                    Status = 6,
                    UpdatedBy = disbursement.DisbursedBy,
                    StatusDescription = "Loan Disbursed"
                };

                var result = await UpdateLoanStatusAsync(loanNo, update);

                // Update loan with disbursement details
                loan.DisbursementDate = disbursement.DisbursementDate;
                loan.DisbursementBy = disbursement.DisbursedBy;

                // Create blockchain transaction for disbursement
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = $"{loan.Member.Surname} {loan.Member.OtherNames}",
                    DisbursedAmount = disbursement.DisbursedAmount,
                    DisbursementMethod = disbursement.DisbursementMethod,
                    BankAccount = disbursement.BankAccount,
                    ChequeNo = disbursement.ChequeNo,
                    ReferenceNo = disbursement.ReferenceNo,
                    DisbursedBy = disbursement.DisbursedBy,
                    DisbursementDate = disbursement.DisbursementDate,
                    CompanyCode = currentCompanyCode
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_DISBURSEMENT",
                    loan.MemberNo,
                    currentCompanyCode,
                    disbursement.DisbursedAmount,
                    loanNo,
                    blockchainData
                );

                if (blockchainTx != null)
                {
                    loan.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "DISBURSE",
                    "Approved",
                    $"Disbursed {disbursement.DisbursedAmount} via {disbursement.DisbursementMethod}",
                    disbursement.DisbursedBy,
                    disbursement.DisbursedBy
                );

                await transaction.CommitAsync();

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error disbursing loan {loanNo}");
                throw;
            }
        }

        public async Task<LoanResponseDTO> CloseLoanAsync(string loanNo, string closedBy)
        {
            var update = new LoanUpdateDTO
            {
                Status = 8,
                UpdatedBy = closedBy,
                StatusDescription = "Loan Closed"
            };

            return await UpdateLoanStatusAsync(loanNo, update);
        }

        // Guarantor Operations
        public async Task<bool> AddGuarantorAsync(string loanNo, LoanGuarantorDTO guarantor)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == currentCompanyCode);

                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                if (loan.Status != 2) // Must be in guarantors stage
                    throw new Exception("Can only add guarantors during guarantors stage");

                // Check if guarantor already exists
                var existing = await _context.Loanguars
                    .FirstOrDefaultAsync(g => g.LoanNo == loanNo &&
                                             g.MemberNo == guarantor.MemberNo);

                if (existing != null)
                    throw new Exception($"Member {guarantor.MemberNo} is already a guarantor");

                // Verify guarantor member exists
                var guarantorMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == guarantor.MemberNo &&
                                             m.CompanyCode == currentCompanyCode);

                if (guarantorMember == null)
                    throw new Exception($"Guarantor member {guarantor.MemberNo} not found");

                // Add guarantor
                var loanGuar = new Loanguar
                {
                    LoanNo = loanNo,
                    MemberNo = guarantor.MemberNo,
                    Amount = guarantor.GuaranteedAmount,
                    Balance = guarantor.GuaranteedAmount,
                    Collateral = guarantor.Collateral,
                    Description = guarantor.Description,
                    FullNames = guarantor.FullNames,
                    CompanyCode = currentCompanyCode,
                    AuditId = "SYSTEM", // Will be updated by blockchain
                    AuditTime = DateTime.Now,
                    Transfered = false
                };

                _context.Loanguars.Add(loanGuar);
                await _context.SaveChangesAsync();

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    PrincipalMemberNo = loan.MemberNo,
                    GuarantorMemberNo = guarantor.MemberNo,
                    GuarantorName = guarantor.FullNames,
                    GuaranteedAmount = guarantor.GuaranteedAmount,
                    Collateral = guarantor.Collateral,
                    AddedDate = DateTime.Now,
                    CompanyCode = currentCompanyCode
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_GUARANTOR_ADDED",
                    loan.MemberNo,
                    currentCompanyCode,
                    0,
                    loanNo,
                    blockchainData
                );

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "LoanGuarantors",
                    loanNo,
                    "ADD",
                    null,
                    $"Added guarantor {guarantor.MemberNo} for amount {guarantor.GuaranteedAmount}",
                    "SYSTEM",
                    "SYSTEM"
                );

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error adding guarantor to loan {loanNo}");
                throw;
            }
        }

        public async Task<bool> RemoveGuarantorAsync(string loanNo, string guarantorMemberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo &&
                                             l.CompanyCode == currentCompanyCode);

                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                if (loan.Status != 2) // Must be in guarantors stage
                    throw new Exception("Can only remove guarantors during guarantors stage");

                var guarantor = await _context.Loanguars
                    .FirstOrDefaultAsync(g => g.LoanNo == loanNo &&
                                             g.MemberNo == guarantorMemberNo);

                if (guarantor == null)
                    throw new Exception($"Guarantor {guarantorMemberNo} not found for loan {loanNo}");

                // Create blockchain transaction before removal
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    PrincipalMemberNo = loan.MemberNo,
                    GuarantorMemberNo = guarantorMemberNo,
                    GuarantorName = guarantor.FullNames,
                    GuaranteedAmount = guarantor.Amount,
                    RemovedDate = DateTime.Now,
                    CompanyCode = currentCompanyCode,
                    Reason = "Guarantor removed from application"
                };

                await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_GUARANTOR_REMOVED",
                    loan.MemberNo,
                    currentCompanyCode,
                    0,
                    loanNo,
                    blockchainData
                );

                // Remove guarantor
                _context.Loanguars.Remove(guarantor);
                await _context.SaveChangesAsync();

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "LoanGuarantors",
                    loanNo,
                    "REMOVE",
                    $"Guarantor {guarantorMemberNo} with amount {guarantor.Amount}",
                    "REMOVED",
                    "SYSTEM",
                    "SYSTEM"
                );

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error removing guarantor from loan {loanNo}");
                throw;
            }
        }

        public async Task<List<LoanGuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var guarantors = await _context.Loanguars
                .Where(g => g.LoanNo == loanNo &&
                           g.CompanyCode == currentCompanyCode)
                .ToListAsync();

            var result = new List<LoanGuarantorResponseDTO>();

            foreach (var guarantor in guarantors)
            {
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == guarantor.MemberNo);

                // Calculate available balance (simplified)
                var totalGuaranteed = await _context.Loanguars
                    .Where(g => g.MemberNo == guarantor.MemberNo &&
                               g.CompanyCode == currentCompanyCode)
                    .SumAsync(g => g.Amount ?? 0);

                var shareBalance = await _memberService.GetShareBalanceAsync(guarantor.MemberNo);
                var availableBalance = shareBalance - totalGuaranteed;

                result.Add(new LoanGuarantorResponseDTO
                {
                    MemberNo = guarantor.MemberNo,
                    FullNames = guarantor.FullNames ??
                               (member != null ? $"{member.Surname} {member.OtherNames}" : "Unknown"),
                    GuaranteedAmount = guarantor.Amount ?? 0,
                    AvailableBalance = availableBalance > 0 ? availableBalance : 0,
                    Collateral = guarantor.Collateral,
                    Status = guarantor.Transfered ? "Approved" : "Pending"
                });
            }

            return result;
        }

        // Reports and Calculations
        public async Task<decimal> GetMemberLoanEligibilityAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            // Get member's total share value
            var shareValue = await _memberService.GetShareBalanceAsync(memberNo);

            // Get loan-to-share ratio from loan types
            var loanTypes = await _context.Loantypes
                .Where(lt => lt.CompanyCode == currentCompanyCode)
                .ToListAsync();

            var maxRatio = loanTypes.Any() ?
                loanTypes.Max(lt => lt.EarningRation ?? 3) : 3; // Default 3:1 ratio

            var eligibility = shareValue * maxRatio;

            // Deduct existing loan balances
            var existingLoans = await _context.Loanbals
                .Where(lb => lb.MemberNo == memberNo &&
                            lb.Companycode == currentCompanyCode &&
                            !lb.Cleared)
                .SumAsync(lb => lb.Balance);

            return eligibility - existingLoans;
        }

        public async Task<List<LoanResponseDTO>> GetPendingLoansAsync()
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var loans = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Where(l => l.CompanyCode == currentCompanyCode &&
                           l.Status < 5 && l.Status != 7) // Pending but not rejected
                .OrderByDescending(l => l.ApplicDate)
                .Take(50)
                .ToListAsync();

            var result = new List<LoanResponseDTO>();

            foreach (var loan in loans)
            {
                result.Add(await MapToLoanResponseDTO(loan));
            }

            return result;
        }

        public async Task<List<LoanResponseDTO>> GetLoansByStatusAsync(int status)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var loans = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Where(l => l.CompanyCode == currentCompanyCode &&
                           l.Status == status)
                .OrderByDescending(l => l.ApplicDate)
                .Take(100)
                .ToListAsync();

            var result = new List<LoanResponseDTO>();

            foreach (var loan in loans)
            {
                result.Add(await MapToLoanResponseDTO(loan));
            }

            return result;
        }

        // Helper Methods
        private string GenerateLoanNumber(string companyCode)
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"{companyCode}-LN-{date}-{random}";
        }

        private async Task<LoanResponseDTO> GetLoanResponseDTO(string loanNo)
        {
            var loan = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            if (loan == null)
                throw new Exception($"Loan {loanNo} not found");

            return await MapToLoanResponseDTO(loan);
        }

        private async Task<LoanResponseDTO> MapToLoanResponseDTO(Loan loan)
        {
            var guarantors = await GetLoanGuarantorsAsync(loan.LoanNo);

            // Get loan balance
            var loanBal = await _context.Loanbals
                .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo);

            return new LoanResponseDTO
            {
                Id = loan.Id,
                LoanNo = loan.LoanNo,
                MemberNo = loan.MemberNo,
                MemberName = loan.Member != null ?
                    $"{loan.Member.Surname} {loan.Member.OtherNames}" :
                    loan.MemberNo,
                LoanCode = loan.LoanCode ?? "Unknown",
                LoanType = loan.LoanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                LoanAmount = loan.LoanAmt ?? 0,
                ApprovedAmount = loan.Aamount,
                DisbursedAmount = loan.Status >= 6 ? loan.LoanAmt ?? 0 : 0,
                RepayPeriod = loan.RepayPeriod ?? 12,
                ApplicDate = loan.ApplicDate,
                Status = loan.Status ?? 1,
                StatusDescription = loan.StatusDescription ?? LoanStatusHelper.GetStatusDescription(loan.Status ?? 1),
                BlockchainTxId = loan.BlockchainTxId,
                Guarantors = guarantors,
                CompanyCode = loan.CompanyCode
            };
        }
    }
}