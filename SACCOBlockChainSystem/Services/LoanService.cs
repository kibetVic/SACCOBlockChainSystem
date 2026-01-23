using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Repositories;

namespace SACCOBlockChainSystem.Services
{
    public class LoanService : ILoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IMemberRepository _memberRepository;
        private readonly ILoanRepository _loanRepository;
        private readonly IAuditService _auditService;
        private readonly ILogger<LoanService> _logger;

        public LoanService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IMemberRepository memberRepository,
            ILoanRepository loanRepository,
            IAuditService auditService,
            ILogger<LoanService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _memberRepository = memberRepository;
            _loanRepository = loanRepository;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<LoanApplicationResponseDTO> ApplyForLoanAsync(LoanApplicationDTO application)
        {
            try
            {
                // Validate member exists
                var member = await _memberRepository.GetByMemberNoAsync(application.MemberNo);
                if (member == null)
                    throw new Exception($"Member {application.MemberNo} not found");

                // Check loan eligibility
                var eligibility = await CalculateLoanEligibilityAsync(application.MemberNo);
                if (eligibility < application.Amount)
                    throw new Exception($"Loan amount exceeds eligibility. Maximum: {eligibility}");

                // Generate loan number
                var loanNo = GenerateLoanNumber();

                // Create loan record
                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanType,
                    ApplicDate = DateTime.Now,
                    LoanAmt = application.Amount,
                    RepayPeriod = application.RepaymentPeriod,
                    Purpose = application.Purpose,
                    CompanyCode = member.CompanyCode ?? "DEFAULT",
                    IdNo = member.Idno,
                    Interest = application.InterestRate,
                    Status = 0, // Pending
                    Sourceofrepayment = application.SourceOfRepayment,
                    Repayrate = application.MonthlyRepayment,
                    AuditId = application.AppliedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                // Create loan balance record
                var loanBal = new Loanbal
                {
                    LoanNo = loanNo,
                    LoanCode = application.LoanType,
                    MemberNo = application.MemberNo,
                    Balance = application.Amount,
                    Interest = application.InterestRate ?? 12.5m, // Default interest
                    FirstDate = DateTime.Now,
                    LastDate = DateTime.Now,
                    Duedate = DateTime.Now.AddMonths(application.RepaymentPeriod),
                    Companycode = member.CompanyCode ?? "DEFAULT",
                    RepayMethod = "MONTHLY",
                    Cleared = false,
                    AutoCalc = true,
                    RepayPeriod = application.RepaymentPeriod,
                    RepayRate = application.MonthlyRepayment ?? 0,
                    AuditId = application.AppliedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                // Create blockchain transaction for loan application
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    Amount = application.Amount,
                    Purpose = application.Purpose,
                    InterestRate = application.InterestRate,
                    RepaymentPeriod = application.RepaymentPeriod,
                    AppliedDate = DateTime.Now,
                    Status = "PENDING"
                };

                var blockchainTx = await _blockchainService.CreateTransaction(
                    "LOAN_APPLICATION",
                    application.MemberNo,
                    member.CompanyCode,
                    application.Amount,
                    loanNo,
                    blockchainData
                );

                loan.BlockchainTxId = blockchainTx.TransactionId;

                // Save all changes
                await _loanRepository.AddAsync(loan);
                _context.Loanbals.Add(loanBal);
                await _context.SaveChangesAsync();

                // Add to blockchain
                await _blockchainService.AddToBlockchain(blockchainTx);

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "INSERT",
                    null,
                    $"Loan application for {application.Amount} by {application.MemberNo}",
                    application.AppliedBy ?? "SYSTEM",
                    application.AppliedBy ?? "SYSTEM"
                );

                return new LoanApplicationResponseDTO
                {
                    Success = true,
                    LoanNumber = loanNo,
                    Amount = application.Amount,
                    Status = "PENDING",
                    ApplicationDate = DateTime.Now,
                    BlockchainTxId = blockchainTx.TransactionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing loan application");
                throw;
            }
        }

        public async Task<bool> ApproveLoanAsync(string loanNo, string approvedBy)
        {
            try
            {
                var loan = await _loanRepository.GetByLoanNoAsync(loanNo);
                if (loan == null)
                    throw new Exception($"Loan {loanNo} not found");

                // Update loan status
                loan.Status = 1; // Approved
                loan.Guaranteed = "Y";
                loan.AuditId = approvedBy;
                loan.AuditTime = DateTime.Now;
                loan.AuditDateTime = DateTime.Now;

                // Create blockchain transaction for loan approval
                var blockchainData = new
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    Amount = loan.LoanAmt,
                    ApprovedBy = approvedBy,
                    ApprovalDate = DateTime.Now,
                    Status = "APPROVED"
                };

                var blockchainTx = await _blockchainService.CreateTransaction(
                    "LOAN_APPROVAL",
                    loan.MemberNo,
                    loan.CompanyCode,
                    loan.LoanAmt ?? 0,
                    loanNo,
                    blockchainData
                );

                var existingTxId = loan.BlockchainTxId;
                loan.BlockchainTxId = blockchainTx.TransactionId;

                // Save changes
                await _loanRepository.UpdateAsync(loan);

                // Add to blockchain
                await _blockchainService.AddToBlockchain(blockchainTx);

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Loans",
                    loanNo,
                    "UPDATE",
                    $"Status: PENDING, BlockchainTxId: {existingTxId}",
                    $"Status: APPROVED, BlockchainTxId: {blockchainTx.TransactionId}",
                    approvedBy,
                    approvedBy
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving loan");
                throw;
            }
        }

        public async Task<LoanRepaymentResponseDTO> ProcessRepaymentAsync(LoanRepaymentDTO repayment)
        {
            try
            {
                var loan = await _loanRepository.GetByLoanNoAsync(repayment.LoanNo);
                if (loan == null)
                    throw new Exception($"Loan {repayment.LoanNo} not found");

                var loanBal = await _loanRepository.GetLoanBalanceAsync(repayment.LoanNo);
                if (loanBal == null)
                    throw new Exception($"Loan balance record not found for {repayment.LoanNo}");

                // Calculate principal and interest
                decimal interestRate = loan.Interest ?? 12.5m;
                decimal monthlyInterest = (loanBal.Balance * interestRate / 100) / 12;
                decimal principal = repayment.Amount - monthlyInterest;

                // Update loan balance
                loanBal.Balance -= principal;
                loanBal.LastDate = DateTime.Now;
                loanBal.AuditTime = DateTime.Now;
                loanBal.AuditDateTime = DateTime.Now;

                // Create repayment record
                var repay = new Repay
                {
                    LoanNo = repayment.LoanNo,
                    MemberNo = loan.MemberNo,
                    CompanyCode = loan.CompanyCode,
                    DateReceived = DateTime.Now,
                    Amount = repayment.Amount,
                    Principal = principal,
                    Interest = monthlyInterest,
                    LoanBalance = loanBal.Balance,
                    ReceiptNo = repayment.ReceiptNo ?? GenerateReceiptNumber(),
                    Remarks = repayment.Remarks,
                    AuditId = repayment.ProcessedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    TransDate = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                // Create blockchain transaction
                var blockchainData = new
                {
                    LoanNo = repayment.LoanNo,
                    MemberNo = loan.MemberNo,
                    Amount = repayment.Amount,
                    Principal = principal,
                    Interest = monthlyInterest,
                    NewBalance = loanBal.Balance,
                    ReceiptNo = repay.ReceiptNo,
                    RepaymentDate = DateTime.Now
                };

                var blockchainTx = await _blockchainService.CreateTransaction(
                    "LOAN_REPAYMENT",
                    loan.MemberNo,
                    loan.CompanyCode,
                    repayment.Amount,
                    repay.Id.ToString(),
                    blockchainData
                );

                repay.BlockchainTxId = blockchainTx.TransactionId;

                // Save changes
                _context.Repays.Add(repay);
                await _context.SaveChangesAsync();

                // Add to blockchain
                await _blockchainService.AddToBlockchain(blockchainTx);

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Repays",
                    repay.Id.ToString(),
                    "INSERT",
                    null,
                    $"Loan repayment of {repayment.Amount} for loan {repayment.LoanNo}",
                    repayment.ProcessedBy ?? "SYSTEM",
                    repayment.ProcessedBy ?? "SYSTEM"
                );

                return new LoanRepaymentResponseDTO
                {
                    Success = true,
                    ReceiptNo = repay.ReceiptNo,
                    Amount = repayment.Amount,
                    Principal = principal,
                    Interest = monthlyInterest,
                    NewBalance = loanBal.Balance,
                    BlockchainTxId = blockchainTx.TransactionId,
                    RepaymentDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing loan repayment");
                throw;
            }
        }

        public async Task<List<Loan>> GetMemberLoansAsync(string memberNo)
        {
            return (await _loanRepository.GetMemberLoansAsync(memberNo)).ToList();
        }

        public async Task<Loan> GetLoanByLoanNoAsync(string loanNo)
        {
            return await _loanRepository.GetByLoanNoAsync(loanNo);
        }

        public async Task<decimal> CalculateLoanEligibilityAsync(string memberNo)
        {
            var shareBalance = await _memberRepository.GetShareBalanceAsync(memberNo);
            // Typically, loan eligibility is 2-3 times share balance
            return shareBalance * 3;
        }

        private string GenerateLoanNumber()
        {
            return $"LN{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
        }

        private string GenerateReceiptNumber()
        {
            return $"LR{DateTime.Now:yyyyMMdd}{new Random().Next(10000, 99999)}";
        }
    }
}