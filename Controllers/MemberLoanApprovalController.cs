// Controllers/MemberLoanApprovalController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Authorize(Roles = "Finance Officer, Super Admin, Admin")]
    [Route("Finance/MemberLoanApproval")]
    public class MemberLoanApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoanService _loanService;
        private readonly IContributionService _contributionService;
        private readonly ILoanTypeService _loanTypeService;
        private readonly ILogger<MemberLoanApprovalController> _logger;
        private readonly AuditTrailService _auditService;

        public MemberLoanApprovalController(
            ApplicationDbContext context,
            ILoanService loanService,
            IContributionService contributionService,
            ILoanTypeService loanTypeService,
            ILogger<MemberLoanApprovalController> logger,
            AuditTrailService auditService)
        {
            _context = context;
            _loanService = loanService;
            _contributionService = contributionService;
            _loanTypeService = loanTypeService;
            _logger = logger;
            _auditService = auditService;
        }

        private string GetUserCompanyCode()
        {
            return User.FindFirstValue("CompanyCode") ?? "DEFAULT";
        }

        #region Pending Approvals Dashboard

        [HttpGet("PendingApprovals")]
        public async Task<IActionResult> PendingApprovals()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation($"PendingApprovals: UserId={userId}");

                // ============================================================
                // CHECK OTP VALIDATION (Same as PendingDisbursement)
                // ============================================================
                var otpValidated = HttpContext.Session.GetString($"OtpValidated_{userId}");
                var otpValidatedAt = HttpContext.Session.GetString($"OtpValidatedAt_{userId}");

                bool isValidated = false;
                if (otpValidated == "true" && !string.IsNullOrEmpty(otpValidatedAt))
                {
                    if (DateTime.TryParse(otpValidatedAt, out var validatedAt))
                    {
                        var timeSinceValidation = DateTime.UtcNow - validatedAt;
                        isValidated = timeSinceValidation.TotalMinutes <= 5;

                        _logger.LogInformation($"PendingApprovals: Validation check - IsValidated: {isValidated}, TimeSince: {timeSinceValidation.TotalMinutes:F2} minutes");

                        if (!isValidated)
                        {
                            _logger.LogInformation($"PendingApprovals: OTP validation expired for user {userId}");
                        }
                    }
                }

                if (!isValidated)
                {
                    // Clear invalid session
                    HttpContext.Session.Remove($"OtpValidated_{userId}");
                    HttpContext.Session.Remove($"OtpValidatedAt_{userId}");

                    _logger.LogInformation($"PendingApprovals: OTP not validated, redirecting to verification for user {userId}");

                    TempData["ErrorMessage"] = "Please verify your identity with OTP to access pending approvals.";
                    return RedirectToAction("OtpVerification", "Account", new { returnUrl = Url.Action("PendingApprovals", "MemberLoanApproval") });
                }

                var companyCode = GetUserCompanyCode();
                _logger.LogInformation($"PendingApprovals: User {userId} accessing pending approvals for company {companyCode}");

                // Get loans with status Submitted (2) that need approval
                var pendingLoans = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode && l.Status == 2 && l.Posted == "PENDING_APPROVAL")
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                var pendingList = new List<PendingLoanApprovalDTO>();

                foreach (var loan in pendingLoans)
                {
                    var member = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                    var loanType = await _context.Loantypes
                        .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                    // Get member's contribution summary
                    var contributions = await GetMemberContributionSummaryAsync(loan.MemberNo, companyCode);
                    var repaymentCapacity = await CalculateRepaymentCapacityAsync(loan.MemberNo, companyCode);

                    // Check if member has existing active loans
                    var existingLoans = await _context.Loans
                        .Where(l => l.MemberNo == loan.MemberNo && l.CompanyCode == companyCode &&
                                    l.Status != 7 && l.Status != 10 && l.Status != 9)
                        .CountAsync();

                    // Get guarantors if any
                    var guarantors = await _context.Loanguar
                        .Where(g => g.LoanNo == loan.LoanNo && g.Transfered == false)
                        .Select(g => new
                        {
                            g.MemberNo,
                            g.Amount,
                            FullName = _context.Members.Where(m => m.MemberNo == g.MemberNo).Select(m => m.FullName).FirstOrDefault() ?? g.MemberNo
                        })
                        .ToListAsync();

                    pendingList.Add(new PendingLoanApprovalDTO
                    {
                        LoanNo = loan.LoanNo,
                        MemberNo = loan.MemberNo,
                        MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                        IdNo = member?.Idno ?? "N/A",
                        PhoneNo = member?.PhoneNo ?? member?.MobileNo ?? "N/A",
                        LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                        LoanAmount = loan.LoanAmt ?? 0,
                        ApplicationDate = loan.ApplicDate,
                        IsMobileLoan = loanType?.MobileLoan == true,
                        RequiresGuarantor = !string.IsNullOrEmpty(loanType?.Guarantor) && loanType.Guarantor != "No" && loanType.Guarantor != "N",
                        HasGuarantors = guarantors.Any(),
                        GuarantorDetails = guarantors,
                        TotalDeposits = contributions.TotalDeposits,
                        TotalShares = contributions.TotalShares,
                        MonthlyIncome = contributions.MonthlyIncome,
                        RepaymentCapacity = repaymentCapacity.RepaymentCapacity,
                        MonthlyInstallment = loan.Repayrate ?? 0,
                        CapacityRatio = repaymentCapacity.CapacityRatio,
                        ExistingLoansCount = existingLoans,
                        IsSelfGuarantee = loanType?.SelfGuarantee == true,
                        Purpose = loan.Purpose ?? "Not specified",
                        RepayPeriod = loan.RepayPeriod ?? 0,
                        InterestRate = loan.Interest ?? 0
                    });
                }

                ViewBag.Count = pendingList.Count;
                ViewBag.CompanyCode = companyCode;
                ViewBag.OtpValidated = true;

                return View(pendingList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending member loan approvals");
                TempData["ErrorMessage"] = "Error loading pending approvals";
                return View(new List<PendingLoanApprovalDTO>());
            }
        }


        [HttpGet("PendingApprovalDetails/{loanNo}")]
        public async Task<IActionResult> PendingApprovalDetails(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                _logger.LogInformation($"Loading details for pending approval: {loanNo}");

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingApprovals");
                }

                if (loan.Status != 2 || loan.Posted != "PENDING_APPROVAL")
                {
                    TempData["ErrorMessage"] = "This loan is not pending approval";
                    return RedirectToAction("PendingApprovals");
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get member's contribution history
                var contributionHistory = await _context.ContribShares
                    .Where(cs => cs.MemberNo == loan.MemberNo && cs.CompanyCode == companyCode)
                    .OrderByDescending(cs => cs.AuditTime)
                    .ToListAsync();

                // Get member's repayment history (if any)
                var previousRepayments = await _context.Repay
                    .Where(r => r.MemberNo == loan.MemberNo && r.CompanyCode == companyCode && r.Posted == true)
                    .OrderByDescending(r => r.DateReceived)
                    .ToListAsync();

                // Get existing loans
                var existingLoans = await _context.Loans
                    .Where(l => l.MemberNo == loan.MemberNo && l.CompanyCode == companyCode && l.LoanNo != loanNo)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                // Get guarantors
                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .ToListAsync();

                var guarantorDetails = new List<object>();
                foreach (var g in guarantors)
                {
                    var guarantorMember = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == g.MemberNo && m.CompanyCode == companyCode);
                    guarantorDetails.Add(new
                    {
                        g.MemberNo,
                        g.Amount,
                        g.FullNames,
                        IdNo = guarantorMember?.Idno,
                        Phone = guarantorMember?.PhoneNo ?? guarantorMember?.MobileNo
                    });
                }

                // Calculate repayment capacity
                var contributions = await GetMemberContributionSummaryAsync(loan.MemberNo, companyCode);
                var repaymentCapacity = await CalculateRepaymentCapacityAsync(loan.MemberNo, companyCode);

                var viewModel = new PendingApprovalDetailViewModel
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = member != null ? $"{member.Surname} {member.OtherNames}".Trim() : loan.MemberNo,
                    IdNo = member?.Idno ?? "N/A",
                    PhoneNo = member?.PhoneNo ?? member?.MobileNo ?? "N/A",
                    Email = member?.Email ?? member?.EmailAddress ?? "N/A",
                    LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                    LoanAmount = loan.LoanAmt ?? 0,
                    ApplicationDate = loan.ApplicDate,
                    Purpose = loan.Purpose ?? "Not specified",
                    RepayPeriod = loan.RepayPeriod ?? 0,
                    InterestRate = loan.Interest ?? 0,
                    MonthlyInstallment = loan.Repayrate ?? 0,
                    IsMobileLoan = loanType?.MobileLoan == true,
                    RequiresGuarantor = !string.IsNullOrEmpty(loanType?.Guarantor) && loanType.Guarantor != "No" && loanType.Guarantor != "N",
                    IsSelfGuarantee = loanType?.SelfGuarantee == true,
                    TotalDeposits = contributions.TotalDeposits,
                    TotalShares = contributions.TotalShares,
                    MonthlyIncome = contributions.MonthlyIncome,
                    RepaymentCapacity = repaymentCapacity.RepaymentCapacity,
                    CapacityRatio = repaymentCapacity.CapacityRatio,
                    ExistingLoansCount = existingLoans.Count,
                    ExistingLoans = existingLoans.Select(l => new
                    {
                        l.LoanNo,
                        l.LoanAmt,
                        Status = ((Status)(l.Status ?? 0)).ToString(),
                        ApplicDate = l.ApplicDate
                    }).ToList(),
                    Guarantors = guarantorDetails,
                    ContributionHistory = contributionHistory.Select(cs => new
                    {
                        cs.AuditTime,
                        cs.DepositsAmount,
                        cs.ShareCapitalAmount,
                        cs.TransactionNo
                    }).ToList(),
                    PreviousRepayments = previousRepayments.Select(r => new
                    {
                        r.ReceiptNo,
                        r.Amount,
                        r.DateReceived,
                        r.Principal,
                        r.Interest,
                        r.LoanBalance
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading pending approval details for {loanNo}");
                TempData["ErrorMessage"] = "Error loading loan details";
                return RedirectToAction("PendingApprovals");
            }
        }

        [HttpPost("ApproveLoan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLoan(string loanNo, string approvalNotes)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var approvedBy = User.Identity?.Name ?? "SYSTEM";

                _logger.LogInformation($"Approving member loan {loanNo} by {approvedBy}");

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingApprovals");
                }

                if (loan.Status != 2 || loan.Posted != "PENDING_APPROVAL")
                {
                    TempData["ErrorMessage"] = "This loan is not pending approval";
                    return RedirectToAction("PendingApprovals");
                }

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                // Get member data
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                // Get appraisal data for this loan
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo && a.CompanyCode == companyCode);

                // Get existing loans for this member (excluding this loan)
                var existingLoans = await _context.Loans
                    .Where(l => l.MemberNo == loan.MemberNo && l.CompanyCode == companyCode && l.LoanNo != loanNo && l.Status == 5)
                    .ToListAsync();

                // Get total guarantor amount for this loan
                var totalGuarantorAmount = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .SumAsync(g => g.Amount ?? 0);

                // ============================================================
                // APPROVE THE LOAN - Move to Endorsed (5) for withdrawal
                // ============================================================
                loan.Status = 5; // Endorsed
                loan.Posted = "APPROVED";
                loan.UserName = approvedBy;
                loan.AuditDateTime = DateTime.Now;
                loan.AddSecurity = $"Approved by {approvedBy}. Notes: {approvalNotes ?? "Approved"}";

                await _context.SaveChangesAsync();

                // ============================================================
                // UPDATE OR CREATE APPRAISAL
                // ============================================================
                if (appraisal == null)
                {
                    // Create new appraisal if it doesn't exist
                    appraisal = new Appraisal
                    {
                        LoanNo = loanNo,
                        CompanyCode = companyCode,
                        MemberNo = loan.MemberNo,
                        AppraisDate = DateTime.Now,
                        AuditID = approvedBy,
                        AuditTime = DateTime.Now,
                        OfficerNames = approvedBy
                    };
                    _context.Appraisal.Add(appraisal);
                }

                // Update ALL appraisal columns
                if (appraisal != null)
                {
                    // Basic fields
                    appraisal.LoanNo = loanNo;
                    appraisal.CompanyCode = companyCode;
                    appraisal.MemberNo = loan.MemberNo;
                    appraisal.AppraisDate = DateTime.Now;
                    appraisal.OfficerNames = approvedBy;

                    // Audit fields
                    appraisal.AuditID = approvedBy;
                    appraisal.AuditTime = DateTime.Now;
                    appraisal.Reason = $"Loan approved. Notes: {approvalNotes ?? "Approved"}";

                    // Transaction reference
                    string transactionNo = Guid.NewGuid().ToString().Substring(0, 15);
                    appraisal.TransactionNo = transactionNo;

                    // Loan details
                    appraisal.AmtRecommended = loan.LoanAmt ?? 0;
                    appraisal.Interest = loan.Interest ?? 0;
                    appraisal.Principal = loan.LoanAmt ?? 0;
                    appraisal.RepayRate = loan.Repayrate ?? 0;
                    appraisal.TInterest = loan.Interest ?? 0;
                    appraisal.TotalInterest = loan.Interest ?? 0;
                    appraisal.TotalDeductions = 0;

                    // Repayment method
                    appraisal.RepayMethod = loan.RepayMethod ?? "PAYROLL_DEDUCTION";

                    // Member financial data - use available properties
                    if (member != null)
                    {
                        appraisal.Salary = 0;
                        appraisal.Allowances = 0; 
                        appraisal.Shares = member.ShareCap ?? 0;
                        appraisal.CoopShares = member.ShareCap ?? 0;
                        appraisal.CoopLoans = 0; 
                        appraisal.Loans = existingLoans.Sum(l => l.LoanAmt ?? 0);
                        appraisal.Deductions = 0; 
                        appraisal.NetMonthlySalary =  0;
                        appraisal.Nssf = 0; 
                        appraisal.SocietyPayment = 0; 
                        appraisal.BankLoan = 0; 
                        appraisal.StatutoryDed = 0;
                        appraisal.OtherDed = 0; 
                    }

                    // Get loan guarantor info
                    appraisal.LoanGuarantor = totalGuarantorAmount;

                    // Number of existing loans
                    appraisal.NoOfLoans = existingLoans.Count + 1; // Include current loan

                    // Calculate Total Deductions
                    appraisal.TotalDeductions = (appraisal.Deductions ?? 0) +
                                                (appraisal.OtherDed ?? 0) +
                                                (appraisal.StatutoryDed ?? 0);

                    // Calculate Expected Net Salary
                    appraisal.ExpectedNetSalary = (appraisal.Salary ?? 0) - appraisal.TotalDeductions;

                    // Calculate all ratio fields
                    if (appraisal.Salary > 0 && appraisal.Salary > 0)
                    {
                        // Deduction To Gross Ratio
                        appraisal.DeductionToGross = appraisal.TotalDeductions > 0
                            ? (appraisal.TotalDeductions / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Statutory Ded To Gross Ratio
                        appraisal.StatutoryDedToGross = (appraisal.StatutoryDed ?? 0) > 0
                            ? ((appraisal.StatutoryDed ?? 0) / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Total Ded New Loan To Gross Ratio
                        appraisal.TotalDedNewLoanToGross = (appraisal.TotalDeductions + (appraisal.Principal ?? 0)) > 0
                            ? ((appraisal.TotalDeductions + (appraisal.Principal ?? 0)) / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Net Salary To Gross Ratio
                        appraisal.NetSalaryToGross = appraisal.ExpectedNetSalary > 0
                            ? (appraisal.ExpectedNetSalary / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Total Loan To Gross Ratio
                        appraisal.TotalLoanToGross = (appraisal.Principal ?? 0) > 0
                            ? ((appraisal.Principal ?? 0) / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Total Coop Ded To Gross Ratio
                        appraisal.TotalCoopDedToGross = (appraisal.CoopLoans ?? 0) > 0
                            ? ((appraisal.CoopLoans ?? 0) / (appraisal.Salary ?? 1)) * 100
                            : 0;

                        // Total Ded To Gross Less Statutory
                        appraisal.TotalDedToGrossLessStatutory = (appraisal.TotalDeductions - (appraisal.StatutoryDed ?? 0)) > 0
                            ? ((appraisal.TotalDeductions - (appraisal.StatutoryDed ?? 0)) / (appraisal.Salary ?? 1)) * 100
                            : 0;
                    }

                    // Cop Loanded (same as Principal)
                    appraisal.CopLoanded = appraisal.Principal ?? 0;

                    // Interest
                    appraisal.Interest = loan.Interest ?? 0;
                    appraisal.TInterest = loan.Interest ?? 0;

                    // Set blockchain transaction ID (will be updated after blockchain creation)
                    appraisal.BlockchainTxId = null;

                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // CREATE AUTO-ENDORSEMENT RECORD (ENDURE)
                // ============================================================
                var endmain = new Endmain
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    MinuteNo = Guid.NewGuid().ToString().Substring(0, 10),
                    MeetingDate = DateTime.Now,
                    AmtApproved = loan.LoanAmt ?? 0,
                    Accepted = "1",
                    ChairSigned = approvedBy,
                    SecSigned = approvedBy,
                    MembSigned = loan.MemberNo,
                    Reasons = $"Approved via member loan approval. Notes: {approvalNotes ?? "Approved"}",
                    Remarks = "Member loan approved",
                    AuditId = approvedBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15)
                };
                _context.Endmain.Add(endmain);
                await _context.SaveChangesAsync();

                // ============================================================
                // CREATE CHEQUE FOR DISBURSEMENT
                // ============================================================
                var cheque = new Cheque
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    CompanyCode = companyCode,
                    Amount = loan.LoanAmt ?? 0,
                    AmountIssued = loan.LoanAmt ?? 0,
                    DateIssued = DateTime.Now,
                    Status = "Pending",
                    AuditId = approvedBy,
                    AuditTime = DateTime.Now,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 15),
                    Voucherno = Guid.NewGuid().ToString().Substring(0, 10),
                    Voucheramount = loan.LoanAmt ?? 0,
                    Paymethod = "BANK",
                    Amountinword = NumberToWords(loan.LoanAmt ?? 0),
                    Refloan = true,
                    Dregard = 0,
                    PaidBf = 0,
                    OrgAmt = loan.LoanAmt ?? 0,
                    LoanAcc = loanType?.LoanAcc ?? "LOAN_ASSET_ACCOUNT",
                    ContraAcc = "BANK_ACCOUNT",
                    PremiumAcc = "PREMIUM_ACCOUNT",
                    Offsetamount = 0,
                    IntrOwed = 0,
                    BlockchainTxId = null
                };

                _context.Cheques.Add(cheque);
                await _context.SaveChangesAsync();

                // ============================================================
                // UPDATE APPRAISAL WITH CHEQUE INFO
                // ============================================================
                if (appraisal != null)
                {
                    appraisal.TransactionNo = cheque.TransactionNo;
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "MEMBER_LOAN_APPROVED",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    ApprovedBy = approvedBy,
                    ApprovalDate = DateTime.Now,
                    ApprovalNotes = approvalNotes,
                    PreviousStatus = "Pending Approval",
                    NewStatus = "Endorsed",
                    LoanAmount = loan.LoanAmt ?? 0,
                    AppraisalData = appraisal != null ? new
                    {
                        appraisal.Id,
                        appraisal.Salary,
                        appraisal.Allowances,
                        appraisal.AmtRecommended,
                        appraisal.TotalDeductions,
                        appraisal.Shares,
                        appraisal.Loans,
                        appraisal.Interest,
                        appraisal.Principal,
                        appraisal.RepayRate,
                        appraisal.TotalInterest,
                        appraisal.NetMonthlySalary,
                        appraisal.SocietyPayment,
                        appraisal.RepayMethod,
                        appraisal.CoopShares,
                        appraisal.CoopLoans,
                        appraisal.Deductions,
                        appraisal.ExpectedNetSalary,
                        appraisal.DeductionToGross,
                        appraisal.StatutoryDedToGross,
                        appraisal.TotalDedNewLoanToGross,
                        appraisal.NetSalaryToGross,
                        appraisal.TotalLoanToGross,
                        appraisal.TotalCoopDedToGross,
                        appraisal.TotalDedToGrossLessStatutory,
                        appraisal.BankLoan,
                        appraisal.CopLoanded,
                        appraisal.NoOfLoans,
                        appraisal.LoanGuarantor,
                        appraisal.StatutoryDed
                    } : null
                };

                // Create blockchain transaction
                string blockHash = GenerateBlockHash();
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

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "MEMBER_LOAN_APPROVED",
                    MemberNo = loan.MemberNo,
                    CompanyCode = companyCode,
                    Amount = loan.LoanAmt ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await HashBlockchainDataAsync(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };
                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update Loan with BlockchainTxId
                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // Update Cheque with BlockchainTxId
                cheque.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // Update Appraisal with BlockchainTxId
                if (appraisal != null)
                {
                    appraisal.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNo = loan.MemberNo,
                    approvedBy = approvedBy,
                    approvalDate = DateTime.Now,
                    approvalNotes = approvalNotes,
                    previousStatus = "Pending Approval",
                    newStatus = "Endorsed",
                    loanAmount = loan.LoanAmt ?? 0,
                    blockchainTxId = blockchainTx.TransactionId,
                    appraisalId = appraisal?.Id,
                    chequeNo = cheque.TransactionNo,
                    endmainNo = endmain.TransactionNo
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Status = 2, Posted = "PENDING_APPROVAL" },
                    newModel: new { loan.LoanNo, loan.Status, loan.Posted, loan.UserName, loan.AuditDateTime },
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: approvedBy,
                    userName: approvedBy,
                    companyCode: companyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                TempData["SuccessMessage"] = $"Loan {loanNo} has been approved successfully! Member can now withdraw the funds.";
                return RedirectToAction("PendingApprovals");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving loan {loanNo}");
                TempData["ErrorMessage"] = $"Error approving loan: {ex.Message}";
                return RedirectToAction("PendingApprovals");
            }
        }

        [HttpPost("RejectLoan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLoan(string loanNo, string rejectionReason)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var rejectedBy = User.Identity?.Name ?? "SYSTEM";

                _logger.LogInformation($"Rejecting member loan {loanNo} by {rejectedBy}");

                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    TempData["ErrorMessage"] = "Loan not found";
                    return RedirectToAction("PendingApprovals");
                }

                if (loan.Status != 2 || loan.Posted != "PENDING_APPROVAL")
                {
                    TempData["ErrorMessage"] = "This loan is not pending approval";
                    return RedirectToAction("PendingApprovals");
                }

                // Get appraisal data for this loan
                var appraisal = await _context.Appraisal
                    .FirstOrDefaultAsync(a => a.LoanNo == loanNo && a.CompanyCode == companyCode);

                // Get total guarantor amount for this loan
                var totalGuarantorAmount = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .SumAsync(g => g.Amount ?? 0);

                // ============================================================
                // REJECT THE LOAN
                // ============================================================
                loan.Status = 10; // Rejected
                loan.Posted = "REJECTED";
                loan.UserName = rejectedBy;
                loan.AuditDateTime = DateTime.Now;
                loan.AddSecurity = $"Rejected by {rejectedBy}. Reason: {rejectionReason ?? "No reason provided"}";

                await _context.SaveChangesAsync();

                // ============================================================
                // UPDATE APPRAISAL
                // ============================================================
                if (appraisal != null)
                {
                    // Audit fields
                    appraisal.AuditID = rejectedBy;
                    appraisal.AuditTime = DateTime.Now;
                    appraisal.Reason = $"Loan rejected. Reason: {rejectionReason ?? "No reason provided"}";

                    // Update with loan rejection details
                    appraisal.AppraisDate = DateTime.Now;
                    appraisal.TransactionNo = Guid.NewGuid().ToString().Substring(0, 15);
                    appraisal.OfficerNames = rejectedBy;

                    // Reset/update financial details
                    appraisal.AmtRecommended = 0;
                    appraisal.Interest = 0;
                    appraisal.Principal = 0;
                    appraisal.RepayRate = 0;
                    appraisal.TInterest = 0;
                    appraisal.TotalInterest = 0;
                    appraisal.LoanGuarantor = totalGuarantorAmount;

                    // Reset derived fields
                    appraisal.ExpectedNetSalary = 0;
                    appraisal.DeductionToGross = 0;
                    appraisal.StatutoryDedToGross = 0;
                    appraisal.TotalDedNewLoanToGross = 0;
                    appraisal.NetSalaryToGross = 0;
                    appraisal.TotalLoanToGross = 0;
                    appraisal.TotalCoopDedToGross = 0;
                    appraisal.TotalDedToGrossLessStatutory = 0;
                    appraisal.CopLoanded = 0;

                    // Set blockchain transaction ID (will be updated after blockchain creation)
                    appraisal.BlockchainTxId = null;

                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // RELEASE ANY GUARANTEES
                // ============================================================
                var guarantors = await _context.Loanguar
                    .Where(g => g.LoanNo == loanNo && g.Transfered == false)
                    .ToListAsync();

                foreach (var g in guarantors)
                {
                    g.Transfered = true;
                    g.Description = $"Loan rejected. Released by {rejectedBy}";
                    g.Transdate = DateTime.Now;
                    g.Balance = 0;
                    g.BlockchainTxId = null;
                }
                await _context.SaveChangesAsync();

                // ============================================================
                // CREATE BLOCKCHAIN TRANSACTION
                // ============================================================
                var blockchainData = new
                {
                    TransactionType = "MEMBER_LOAN_REJECTED",
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    RejectedBy = rejectedBy,
                    RejectionDate = DateTime.Now,
                    RejectionReason = rejectionReason,
                    PreviousStatus = "Pending Approval",
                    NewStatus = "Rejected",
                    AppraisalData = appraisal != null ? new
                    {
                        appraisal.Id,
                        appraisal.Salary,
                        appraisal.Allowances,
                        appraisal.AmtRecommended,
                        appraisal.TotalDeductions,
                        appraisal.Shares,
                        appraisal.Loans,
                        appraisal.Interest,
                        appraisal.Principal,
                        appraisal.RepayRate,
                        appraisal.TotalInterest,
                        appraisal.NetMonthlySalary,
                        appraisal.SocietyPayment,
                        appraisal.RepayMethod,
                        appraisal.CoopShares,
                        appraisal.CoopLoans,
                        appraisal.Deductions,
                        appraisal.ExpectedNetSalary,
                        appraisal.DeductionToGross,
                        appraisal.StatutoryDedToGross,
                        appraisal.TotalDedNewLoanToGross,
                        appraisal.NetSalaryToGross,
                        appraisal.TotalLoanToGross,
                        appraisal.TotalCoopDedToGross,
                        appraisal.TotalDedToGrossLessStatutory,
                        appraisal.BankLoan,
                        appraisal.CopLoanded,
                        appraisal.NoOfLoans,
                        appraisal.LoanGuarantor,
                        appraisal.StatutoryDed
                    } : null
                };

                string blockHash = GenerateBlockHash();
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

                var blockchainTx = new BlockchainTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    TransactionType = "MEMBER_LOAN_REJECTED",
                    MemberNo = loan.MemberNo,
                    CompanyCode = companyCode,
                    Amount = loan.LoanAmt ?? 0,
                    Timestamp = DateTime.Now,
                    DataHash = await HashBlockchainDataAsync(blockchainData),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(blockchainData),
                    OffChainReferenceId = loanNo,
                    Status = "CONFIRMED",
                    BlockHash = block.BlockHash,
                    CreatedAt = DateTime.Now
                };
                _context.BlockchainTransactions.Add(blockchainTx);
                await _context.SaveChangesAsync();

                // Update Loan with BlockchainTxId
                loan.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();

                // Update Appraisal with BlockchainTxId
                if (appraisal != null)
                {
                    appraisal.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }

                // ============================================================
                // SAVE AUDIT TRAIL
                // ============================================================
                var auditExtraData = new
                {
                    loanNo = loanNo,
                    memberNo = loan.MemberNo,
                    rejectedBy = rejectedBy,
                    rejectionDate = DateTime.Now,
                    rejectionReason = rejectionReason,
                    previousStatus = "Pending Approval",
                    newStatus = "Rejected",
                    blockchainTxId = blockchainTx.TransactionId,
                    appraisalId = appraisal?.Id
                };

                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Update,
                    oldModel: new { Status = 2, Posted = "PENDING_APPROVAL" },
                    newModel: new { loan.LoanNo, loan.Status, loan.Posted, loan.UserName, loan.AuditDateTime },
                    tableName: "Loans",
                    recordId: loanNo,
                    userId: rejectedBy,
                    userName: rejectedBy,
                    companyCode: companyCode,
                    module: "LoanManagement",
                    extraData: System.Text.Json.JsonSerializer.Serialize(auditExtraData),
                    blockchainTxId: blockchainTx.TransactionId
                );

                TempData["SuccessMessage"] = $"Loan {loanNo} has been rejected.";
                return RedirectToAction("PendingApprovals");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting loan {loanNo}");
                TempData["ErrorMessage"] = $"Error rejecting loan: {ex.Message}";
                return RedirectToAction("PendingApprovals");
            }
        }


        [HttpGet("CheckApprovalStatus/{loanNo}")]
        public async Task<IActionResult> CheckApprovalStatus(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Loan not found" });
                }

                bool canWithdraw = loan.Status == 5; // Endorsed
                string status = ((Status)(loan.Status ?? 0)).ToString();

                return Json(new
                {
                    success = true,
                    status = status,
                    canWithdraw = canWithdraw,
                    isPending = loan.Status == 2 && loan.Posted == "PENDING_APPROVAL",
                    isApproved = loan.Status == 5,
                    isRejected = loan.Status == 10
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking approval status for {loanNo}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<(decimal TotalDeposits, decimal TotalShares, decimal MonthlyIncome)> GetMemberContributionSummaryAsync(string memberNo, string companyCode)
        {
            try
            {
                var totalDeposits = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.DepositsAmount ?? 0);

                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                // Get last 6 months average monthly contribution
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var monthlyContributions = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode &&
                                 cs.AuditTime >= sixMonthsAgo)
                    .GroupBy(cs => new { cs.AuditDateTime.Value.Year, cs.AuditDateTime.Value.Month })
                    .Select(g => g.Sum(cs => cs.DepositsAmount ?? 0))
                    .ToListAsync();

                decimal monthlyIncome = monthlyContributions.Any()
                    ? monthlyContributions.Average()
                    : totalDeposits / 12; // Fallback to annual average

                return (totalDeposits, totalShares, monthlyIncome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting contribution summary for {memberNo}");
                return (0, 0, 0);
            }
        }

        private async Task<(decimal RepaymentCapacity, decimal CapacityRatio)> CalculateRepaymentCapacityAsync(string memberNo, string companyCode)
        {
            try
            {
                var contributions = await GetMemberContributionSummaryAsync(memberNo, companyCode);
                decimal monthlyIncome = contributions.MonthlyIncome;

                if (monthlyIncome <= 0)
                {
                    return (0, 0);
                }

                // Get existing loan payments
                var existingLoanPayments = await _context.Repay
                    .Where(r => r.MemberNo == memberNo && r.CompanyCode == companyCode && r.Posted == true)
                    .SumAsync(r => r.Amount ?? 0) / 12; // Average monthly payment

                // Repayment capacity = 60% of net income after existing obligations
                decimal netIncome = monthlyIncome - existingLoanPayments;
                decimal repaymentCapacity = netIncome * 0.6m; // 60% of net income

                return (Math.Max(0, repaymentCapacity), repaymentCapacity / (monthlyIncome > 0 ? monthlyIncome : 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating repayment capacity for {memberNo}");
                return (0, 0);
            }
        }

        private string GenerateBlockHash()
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var input = $"{Guid.NewGuid()}{DateTime.Now.Ticks}{new Random().Next()}";
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        private async Task<string> HashBlockchainDataAsync(object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(bytes);
        }

        private string NumberToWords(decimal number)
        {
            if (number == 0) return "ZERO";

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
            if (number == 0) return "ZERO";

            var units = new[] { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
            var tens = new[] { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

            if (number < 20) return units[number];

            if (number < 100) return tens[number / 10] + (number % 10 > 0 ? " " + units[number % 10] : "");

            if (number < 1000) return units[number / 100] + " HUNDRED" + (number % 100 > 0 ? " " + ConvertIntegerToWords(number % 100) : "");

            if (number < 1000000) return ConvertIntegerToWords(number / 1000) + " THOUSAND" + (number % 1000 > 0 ? " " + ConvertIntegerToWords(number % 1000) : "");

            return ConvertIntegerToWords(number / 1000000) + " MILLION" + (number % 1000000 > 0 ? " " + ConvertIntegerToWords(number % 1000000) : "");
        }

        #endregion

        [HttpGet("GetPendingCount")]
        public async Task<IActionResult> GetPendingCount()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var count = await _context.Loans
                    .Where(l => l.CompanyCode == companyCode && l.Status == 2 && l.Posted == "PENDING_APPROVAL")
                    .CountAsync();

                return Json(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending count");
                return Json(new { count = 0 });
            }
        }
    }
}