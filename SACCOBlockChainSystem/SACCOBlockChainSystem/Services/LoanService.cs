using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Text.Json;

namespace SACCOBlockChainSystem.Services
{
    public class LoanService : ILoanService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanService> _logger;
        private readonly IBlockchainService _blockchainService;
        private readonly IMemberService _memberService;
        private readonly IShareService _shareService;

        public LoanService(
            ApplicationDbContext context,
            ILogger<LoanService> logger,
            IBlockchainService blockchainService,
            IMemberService memberService,
            IShareService shareService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _memberService = memberService;
            _shareService = shareService;
        }

        #region Loan Application

        public async Task<Loan> ApplyForLoanAsync(LoanApplicationDTO application)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate application
                var validation = await ValidateLoanApplicationAsync(application);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException($"Loan validation failed: {validation.Message}");
                }

                // Check member eligibility
                var eligibility = await CheckMemberEligibilityAsync(application.MemberNo, application.LoanCode, application.CompanyCode);
                if (!eligibility.IsEligible)
                {
                    throw new InvalidOperationException($"Member not eligible: {eligibility.Message}");
                }

                // Get loan type
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(l => l.LoanCode == application.LoanCode && l.CompanyCode == application.CompanyCode);

                if (loanType == null)
                {
                    throw new InvalidOperationException($"Loan type {application.LoanCode} not found");
                }

                // Generate loan number
                var loanNo = await GenerateLoanNumberAsync(application.CompanyCode);

                decimal interestRate = 0;

                if (!string.IsNullOrEmpty(loanType.Interest) && decimal.TryParse(loanType.Interest, out interestRate))
                {
                    interestRate = interestRate > 1
                        ? interestRate / 100m
                        : interestRate;

                    interestRate = Math.Round(interestRate, 2, MidpointRounding.AwayFromZero);
                }

                var processingFee = loanType.Processingfee ?? 0;
                var insuranceFee = loanType.Insurance != null &&
                                  (loanType.Insurance == "Yes" || loanType.Insurance == "Y")
                    ? Math.Round(0.01m * application.PrincipalAmount, 2)
                    : 0;
                var totalFees = Math.Round(processingFee + insuranceFee, 2);
                var netDisbursement = Math.Round(application.PrincipalAmount - totalFees, 2);

                // Create loan record
                var loan = new Loan
                {
                    LoanNo = loanNo,
                    MemberNo = application.MemberNo,
                    LoanCode = application.LoanCode,
                    LoanTypeId = loanType.Id,
                    CompanyCode = application.CompanyCode,
                    PrincipalAmount = application.PrincipalAmount,
                    ApprovedAmount = 0,
                    DisbursedAmount = 0,
                    OutstandingPrincipal = 0,
                    OutstandingInterest = 0,
                    OutstandingPenalty = 0,
                    TotalOutstanding = 0,
                    InterestRate = Math.Round(interestRate, 4),
                    RepaymentPeriod = loanType.RepayPeriod ?? 12,
                    RepaymentFrequency = 1,
                    ApplicationDate = DateTime.Now,
                    LoanStatus = "Draft",
                    Purpose = application.Purpose,
                    Remarks = application.Remarks,
                    RequiredGuarantors = ParseRequiredGuarantors(loanType.Guarantor),
                    AssignedGuarantors = 0,
                    GuarantorsApproved = false,
                    HasGuarantors = !string.IsNullOrEmpty(loanType.Guarantor) &&
                                    loanType.Guarantor != "No" &&
                                    loanType.Guarantor != "N",
                    AppraisalCompleted = false,
                    ProcessingFee = processingFee,
                    InsuranceFee = insuranceFee,
                    LegalFees = 0,
                    OtherFees = 0,
                    TotalFees = totalFees,
                    NetDisbursement = netDisbursement,
                    CreatedBy = application.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                _context.Loans.Add(loan);
                await _context.SaveChangesAsync();

                // Assign guarantors if provided
                if (application.Guarantors != null && application.Guarantors.Any())
                {
                    foreach (var guarantor in application.Guarantors)
                    {
                        await AssignGuarantorAsync(loanNo, guarantor, application.CreatedBy!);
                    }
                }

                // Create audit trail
                await CreateAuditTrailAsync(loanNo, null, "Draft", "CREATE",
                    $"Loan application created with principal amount {application.PrincipalAmount:C}",
                    application.CreatedBy!, application.CompanyCode);

                await transaction.CommitAsync();

                _logger.LogInformation($"Loan application {loanNo} created successfully");
                return loan;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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

        public async Task<Loan> GetLoanByNoAsync(string loanNo, string companyCode)
        {
            var loan = await _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Include(l => l.LoanGuarantors)
                    .ThenInclude(g => g.GuarantorMember)
                .Include(l => l.LoanAppraisals)
                .Include(l => l.LoanApprovals)
                .Include(l => l.LoanDisbursements)
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.CompanyCode == companyCode);

            if (loan == null)
            {
                throw new InvalidOperationException($"Loan {loanNo} not found");
            }

            return loan;
        }

        public async Task<List<LoanSummaryDTO>> GetMemberLoansAsync(string memberNo, string companyCode)
        {
            var loans = await _context.Loans
                .Include(l => l.LoanType)
                .Include(l => l.LoanSchedules)
                .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                .OrderByDescending(l => l.ApplicationDate)
                .Select(l => new LoanSummaryDTO
                {
                    LoanNo = l.LoanNo,
                    MemberNo = l.MemberNo,
                    MemberName = l.Member != null ? l.Member.Surname + " " + l.Member.OtherNames : "",
                    LoanType = l.LoanType != null ? l.LoanType.LoanType1! : "",
                    PrincipalAmount = l.PrincipalAmount,
                    ApprovedAmount = l.ApprovedAmount,
                    DisbursedAmount = l.DisbursedAmount,
                    OutstandingBalance = l.TotalOutstanding,
                    ArrearsAmount = l.LoanSchedules
                        .Where(s => s.Status == "Overdue" && s.DueDate < DateTime.Now)
                        .Sum(s => s.OutstandingTotal + s.PenaltyAmount),
                    LoanStatus = l.LoanStatus,
                    ApplicationDate = l.ApplicationDate,
                    DisbursementDate = l.DisbursementDate,
                    MaturityDate = l.MaturityDate,
                    DaysOverdue = l.LoanSchedules
                        .Where(s => s.Status == "Overdue")
                        .Max(s => (int?)s.DaysOverdue) ?? 0,
                    InterestRate = l.InterestRate,
                    MonthlyInstallment = l.InstallmentAmount,
                    InstallmentsPaid = l.LoanSchedules.Count(s => s.Status == "Paid"),
                    TotalInstallments = l.LoanSchedules.Count
                })
                .ToListAsync();

            return loans;
        }

        public async Task<List<LoanSummaryDTO>> SearchLoansAsync(LoanSearchDTO searchDto)
        {
            var query = _context.Loans
                .Include(l => l.Member)
                .Include(l => l.LoanType)
                .Where(l => l.CompanyCode == searchDto.CompanyCode);

            if (!string.IsNullOrEmpty(searchDto.MemberNo))
            {
                query = query.Where(l => l.MemberNo == searchDto.MemberNo);
            }

            if (!string.IsNullOrEmpty(searchDto.LoanNo))
            {
                query = query.Where(l => l.LoanNo.Contains(searchDto.LoanNo));
            }

            if (!string.IsNullOrEmpty(searchDto.LoanStatus))
            {
                query = query.Where(l => l.LoanStatus == searchDto.LoanStatus);
            }

            if (!string.IsNullOrEmpty(searchDto.LoanCode))
            {
                query = query.Where(l => l.LoanCode == searchDto.LoanCode);
            }

            if (searchDto.FromDate.HasValue)
            {
                query = query.Where(l => l.ApplicationDate >= searchDto.FromDate.Value);
            }

            if (searchDto.ToDate.HasValue)
            {
                query = query.Where(l => l.ApplicationDate <= searchDto.ToDate.Value);
            }

            if (searchDto.MinAmount.HasValue)
            {
                query = query.Where(l => l.PrincipalAmount >= searchDto.MinAmount.Value);
            }

            if (searchDto.MaxAmount.HasValue)
            {
                query = query.Where(l => l.PrincipalAmount <= searchDto.MaxAmount.Value);
            }

            if (!string.IsNullOrEmpty(searchDto.MemberName))
            {
                query = query.Where(l =>
                    (l.Member != null &&
                     (l.Member.Surname + " " + l.Member.OtherNames).Contains(searchDto.MemberName)));
            }

            var loans = await query
                .OrderByDescending(l => l.ApplicationDate)
                .Select(l => new LoanSummaryDTO
                {
                    LoanNo = l.LoanNo,
                    MemberNo = l.MemberNo,
                    MemberName = l.Member != null ? l.Member.Surname + " " + l.Member.OtherNames : "",
                    LoanType = l.LoanType != null ? l.LoanType.LoanType1! : "",
                    PrincipalAmount = l.PrincipalAmount,
                    ApprovedAmount = l.ApprovedAmount,
                    DisbursedAmount = l.DisbursedAmount,
                    OutstandingBalance = l.TotalOutstanding,
                    LoanStatus = l.LoanStatus,
                    ApplicationDate = l.ApplicationDate,
                    DisbursementDate = l.DisbursementDate,
                    MaturityDate = l.MaturityDate
                })
                .ToListAsync();

            return loans;
        }

        public async Task<LoanDashboardDTO> GetLoanDashboardAsync(string companyCode)
        {
            var loans = await _context.Loans
                .Include(l => l.LoanType)
                .Include(l => l.LoanSchedules)
                .Where(l => l.CompanyCode == companyCode)
                .ToListAsync();

            var dashboard = new LoanDashboardDTO
            {
                TotalLoans = loans.Count,
                TotalLoanAmount = loans.Sum(l => l.PrincipalAmount),
                TotalDisbursed = loans.Where(l => l.LoanStatus == "Disbursed" || l.LoanStatus == "Active")
                    .Sum(l => l.DisbursedAmount),
                TotalOutstanding = loans.Sum(l => l.TotalOutstanding),
                TotalRepaid = loans.Sum(l => l.LoanRepayments?.Sum(r => r.PrincipalAllocated) ?? 0),
                TotalArrears = loans
                    .SelectMany(l => l.LoanSchedules ?? new List<LoanSchedule>())
                    .Where(s => s.Status == "Overdue")
                    .Sum(s => s.OutstandingTotal + s.PenaltyAmount),
                PendingApplications = loans.Count(l => l.LoanStatus == "Draft"),
                UnderAppraisal = loans.Count(l => l.LoanStatus == "UnderAppraisal"),
                PendingApproval = loans.Count(l => l.LoanStatus == "Submitted"),
                ApprovedPendingDisbursement = loans.Count(l => l.LoanStatus == "Approved"),
                ActiveLoans = loans.Count(l => l.LoanStatus == "Disbursed"),
                OverdueLoans = loans.Count(l => l.LoanSchedules != null &&
                    l.LoanSchedules.Any(s => s.Status == "Overdue" && s.DueDate < DateTime.Now)),
                DefaultedLoans = loans.Count(l => l.LoanSchedules != null &&
                    l.LoanSchedules.Any(s => s.Status == "Defaulted"))
            };

            // Loans by status
            dashboard.LoansByStatus = loans
                .GroupBy(l => l.LoanStatus)
                .ToDictionary(g => g.Key, g => g.Count());

            // Loan portfolio by type
            dashboard.LoanPortfolioByType = loans
                .Where(l => l.LoanType != null)
                .GroupBy(l => l.LoanType!.LoanType1 ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Sum(l => l.OutstandingPrincipal));

            // Recent loans
            dashboard.RecentLoans = await SearchLoansAsync(new LoanSearchDTO
            {
                CompanyCode = companyCode
            });

            dashboard.RecentLoans = dashboard.RecentLoans.Take(10).ToList();

            return dashboard;
        }

        #endregion

        #region Guarantor Management

        public async Task<LoanGuarantor> AssignGuarantorAsync(string loanNo, GuarantorAssignmentDTO guarantor, string assignedBy)
        {
            var loan = await GetLoanByNoAsync(loanNo, guarantor.CompanyCode);

            // Validate loan can accept guarantors
            if (loan.LoanStatus != "Draft" && loan.LoanStatus != "Submitted")
            {
                throw new InvalidOperationException($"Cannot assign guarantors to loan in {loan.LoanStatus} status");
            }

            // Check if maximum guarantors reached
            if (loan.AssignedGuarantors >= loan.RequiredGuarantors)
            {
                throw new InvalidOperationException($"Maximum number of guarantors ({loan.RequiredGuarantors}) already assigned");
            }

            // Validate guarantor eligibility
            var eligibility = await ValidateGuarantorEligibilityAsync(
                guarantor.GuarantorMemberNo,
                guarantor.GuaranteeAmount,
                loan.CompanyCode);

            if (!eligibility)
            {
                throw new InvalidOperationException("Guarantor is not eligible");
            }

            // Check if already assigned
            var existing = await _context.LoanGuarantors
                .FirstOrDefaultAsync(g => g.LoanNo == loanNo &&
                    g.GuarantorMemberNo == guarantor.GuarantorMemberNo);

            if (existing != null)
            {
                throw new InvalidOperationException("Guarantor already assigned to this loan");
            }

            // Get member's available shares
            var memberShares = await _shareService.GetTotalSharesValueAsync(guarantor.GuarantorMemberNo);

            var loanGuarantor = new LoanGuarantor
            {
                LoanNo = loanNo,
                GuarantorMemberNo = guarantor.GuarantorMemberNo,
                CompanyCode = loan.CompanyCode,
                GuaranteeAmount = guarantor.GuaranteeAmount,
                AvailableShares = memberShares,
                LockedAmount = 0,
                Status = "Pending",
                AssignedDate = DateTime.Now,
                Remarks = guarantor.Remarks,
                IsActive = true
            };

            _context.LoanGuarantors.Add(loanGuarantor);

            // Update loan guarantor count
            loan.AssignedGuarantors = await _context.LoanGuarantors
                .CountAsync(g => g.LoanNo == loanNo && g.IsActive);

            await _context.SaveChangesAsync();

            await CreateAuditTrailAsync(loanNo, null, loan.LoanStatus, "GUARANTOR_ASSIGNED",
                $"Guarantor {guarantor.GuarantorMemberNo} assigned with amount {guarantor.GuaranteeAmount:C}",
                assignedBy, loan.CompanyCode);

            return loanGuarantor;
        }

        public async Task<List<GuarantorResponseDTO>> GetLoanGuarantorsAsync(string loanNo)
        {
            var guarantors = await _context.LoanGuarantors
                .Include(g => g.GuarantorMember)
                .Where(g => g.LoanNo == loanNo)
                .Select(g => new GuarantorResponseDTO
                {
                    Id = g.Id,
                    LoanNo = g.LoanNo,
                    GuarantorMemberNo = g.GuarantorMemberNo,
                    GuarantorName = g.GuarantorMember != null ?
                        g.GuarantorMember.Surname + " " + g.GuarantorMember.OtherNames : "",
                    GuaranteeAmount = g.GuaranteeAmount,
                    AvailableShares = g.AvailableShares,
                    Status = g.Status,
                    AssignedDate = g.AssignedDate,
                    ApprovedDate = g.ApprovedDate,
                    ApprovedBy = g.ApprovedBy,
                    Remarks = g.Remarks
                })
                .ToListAsync();

            return guarantors;
        }

        public async Task<bool> ApproveGuarantorAsync(int guarantorId, string approvedBy)
        {
            var guarantor = await _context.LoanGuarantors
                .FirstOrDefaultAsync(g => g.Id == guarantorId);

            if (guarantor == null)
            {
                throw new InvalidOperationException("Guarantor not found");
            }

            if (guarantor.Status != "Pending")
            {
                throw new InvalidOperationException($"Guarantor is already {guarantor.Status}");
            }

            // Lock the guarantee amount (shares)
            // This would interact with share service to lock shares
            guarantor.Status = "Approved";
            guarantor.ApprovedDate = DateTime.Now;
            guarantor.ApprovedBy = approvedBy;
            guarantor.LockedAmount = guarantor.GuaranteeAmount;

            await _context.SaveChangesAsync();

            // Check if all guarantors are approved
            var loan = await GetLoanByNoAsync(guarantor.LoanNo, guarantor.CompanyCode);
            var allGuarantors = await _context.LoanGuarantors
                .Where(g => g.LoanNo == guarantor.LoanNo && g.IsActive)
                .ToListAsync();

            if (allGuarantors.All(g => g.Status == "Approved"))
            {
                loan.GuarantorsApproved = true;
                await _context.SaveChangesAsync();
            }

            await CreateAuditTrailAsync(guarantor.LoanNo, null, null, "GUARANTOR_APPROVED",
                $"Guarantor {guarantor.GuarantorMemberNo} approved", approvedBy, guarantor.CompanyCode);

            return true;
        }

        public async Task<bool> RejectGuarantorAsync(int guarantorId, string remarks, string rejectedBy)
        {
            var guarantor = await _context.LoanGuarantors
                .FirstOrDefaultAsync(g => g.Id == guarantorId);

            if (guarantor == null)
            {
                throw new InvalidOperationException("Guarantor not found");
            }

            guarantor.Status = "Rejected";
            guarantor.Remarks = remarks;
            guarantor.IsActive = false;

            await _context.SaveChangesAsync();

            await CreateAuditTrailAsync(guarantor.LoanNo, null, null, "GUARANTOR_REJECTED",
                $"Guarantor {guarantor.GuarantorMemberNo} rejected: {remarks}", rejectedBy, guarantor.CompanyCode);

            return true;
        }

        public async Task<bool> ReleaseGuarantorAsync(int guarantorId, string releasedBy)
        {
            var guarantor = await _context.LoanGuarantors
                .FirstOrDefaultAsync(g => g.Id == guarantorId);

            if (guarantor == null)
            {
                throw new InvalidOperationException("Guarantor not found");
            }

            if (guarantor.Status != "Approved")
            {
                throw new InvalidOperationException($"Cannot release guarantor in {guarantor.Status} status");
            }

            guarantor.Status = "Released";
            guarantor.ReleasedDate = DateTime.Now;
            guarantor.ReleasedBy = releasedBy;
            guarantor.IsActive = false;
            guarantor.LockedAmount = 0;

            await _context.SaveChangesAsync();

            await CreateAuditTrailAsync(guarantor.LoanNo, null, null, "GUARANTOR_RELEASED",
                $"Guarantor {guarantor.GuarantorMemberNo} released", releasedBy, guarantor.CompanyCode);

            return true;
        }

        public async Task<bool> ValidateGuarantorEligibilityAsync(string memberNo, decimal guaranteeAmount, string companyCode)
        {
            // Check if member exists
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (member == null)
            {
                return false;
            }

            // Check if member is active
            if (member.Withdrawn == true || member.Archived == true || member.Dormant == 1)
            {
                return false;
            }

            // Get member's shares value
            var shareValue = await _shareService.GetTotalSharesValueAsync(memberNo);

            // Guarantor must have shares at least equal to guarantee amount
            if (shareValue < guaranteeAmount)
            {
                return false;
            }

            // Check existing guarantees
            var existingGuarantees = await _context.LoanGuarantors
                .Where(g => g.GuarantorMemberNo == memberNo &&
                           g.CompanyCode == companyCode &&
                           g.IsActive &&
                           g.Status == "Approved")
                .SumAsync(g => g.GuaranteeAmount);

            // Total guarantees should not exceed shares value
            if (existingGuarantees + guaranteeAmount > shareValue)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Loan Appraisal

        public async Task<LoanAppraisal> AppraiseLoanAsync(LoanAppraisalDTO appraisalDto)
        {
            var loan = await GetLoanByNoAsync(appraisalDto.LoanNo, appraisalDto.CompanyCode);

            // Check if guarantors are assigned (if required)
            if (loan.RequiredGuarantors > 0 && loan.AssignedGuarantors < loan.RequiredGuarantors)
            {
                throw new InvalidOperationException($"Loan requires {loan.RequiredGuarantors} guarantors, but only {loan.AssignedGuarantors} assigned");
            }

            // Check if guarantors are approved (if required)
            if (loan.RequiredGuarantors > 0 && !loan.GuarantorsApproved)
            {
                throw new InvalidOperationException("All assigned guarantors must be approved before appraisal");
            }

            // Calculate DTI ratio
            var dtiRatio = appraisalDto.ExistingLoanObligations > 0 ?
                (appraisalDto.ExistingLoanObligations / appraisalDto.MonthlyIncome) * 100 : 0;

            var appraisal = new LoanAppraisal
            {
                LoanNo = appraisalDto.LoanNo,
                CompanyCode = appraisalDto.CompanyCode,
                AppliedAmount = loan.PrincipalAmount,
                RecommendedAmount = appraisalDto.RecommendedAmount > 0 ?
                    appraisalDto.RecommendedAmount : loan.PrincipalAmount,
                RecommendedInterestRate = appraisalDto.RecommendedInterestRate > 0 ?
                    appraisalDto.RecommendedInterestRate / 100 : loan.InterestRate,
                RecommendedPeriod = appraisalDto.RecommendedPeriod > 0 ?
                    appraisalDto.RecommendedPeriod : loan.RepaymentPeriod,
                MemberSharesValue = appraisalDto.MemberSharesValue,
                MemberDepositsValue = appraisalDto.MemberDepositsValue,
                MonthlyIncome = appraisalDto.MonthlyIncome,
                ExistingLoanObligations = appraisalDto.ExistingLoanObligations,
                DisposableIncome = appraisalDto.MonthlyIncome - appraisalDto.ExistingLoanObligations,
                DebtToIncomeRatio = dtiRatio,
                CreditScore = CalculateCreditScore(loan.MemberNo, appraisalDto.CompanyCode).Result,
                ExistingLoanDefault = await HasPreviousDefaultAsync(loan.MemberNo, appraisalDto.CompanyCode),
                LoanHistoryRating = await CalculateLoanHistoryRatingAsync(loan.MemberNo, appraisalDto.CompanyCode),
                AppraisalDecision = appraisalDto.AppraisalDecision,
                AppraisalNotes = appraisalDto.AppraisalNotes,
                RiskFactors = appraisalDto.RiskFactors,
                MitigationFactors = appraisalDto.MitigationFactors,
                AppraisedBy = appraisalDto.AppraisedBy,
                AppraisalDate = DateTime.Now,
                IsFinal = true
            };

            // Update loan status based on appraisal decision
            string newStatus;
            string statusRemarks;

            if (appraisalDto.AppraisalDecision == "Recommend" || appraisalDto.AppraisalDecision == "CounterOffer")
            {
                newStatus = "Submitted";
                statusRemarks = $"Appraisal completed - {appraisalDto.AppraisalDecision}";
            }
            else if (appraisalDto.AppraisalDecision == "NotRecommend")
            {
                newStatus = "Rejected";
                statusRemarks = $"Rejected at appraisal: {appraisalDto.AppraisalNotes}";
                loan.Remarks = $"Rejected at appraisal: {appraisalDto.AppraisalNotes}";
            }
            else
            {
                // Default case - keep in UnderAppraisal if decision is unclear
                newStatus = "UnderAppraisal";
                statusRemarks = $"Appraisal in progress - {appraisalDto.AppraisalDecision}";
            }

            // Only update status if it's different from current status
            if (newStatus != null)
            {
                loan.LoanStatus = newStatus;
                loan.ModifiedBy = appraisalDto.AppraisedBy;
                loan.ModifiedAt = DateTime.Now;
                loan.AppraisalCompleted = true;
                loan.AppraisalDate = DateTime.Now;
                loan.AppraisedBy = appraisalDto.AppraisedBy;
            }

            _context.Loans.Update(loan);
            _context.LoanAppraisals.Add(appraisal);
            await _context.SaveChangesAsync();

            await CreateAuditTrailAsync(loan.LoanNo, loan.LoanStatus, loan.LoanStatus, "APPRAISAL_COMPLETED",
                $"Loan appraised with decision: {appraisalDto.AppraisalDecision}, recommended amount: {appraisalDto.RecommendedAmount:C}",
                appraisalDto.AppraisedBy, appraisalDto.CompanyCode);

            return appraisal;
        }

        public async Task<LoanAppraisal?> GetLoanAppraisalAsync(string loanNo)
        {
            return await _context.LoanAppraisals
                .FirstOrDefaultAsync(a => a.LoanNo == loanNo);

            // No exception thrown - just return null if not found
        }

        #endregion

        #region Loan Approval

        public async Task<LoanApproval> ApproveLoanAsync(LoanApprovalDTO approvalDto)
        {
            var loan = await GetLoanByNoAsync(approvalDto.LoanNo, approvalDto.CompanyCode);

            // Check if appraisal completed
            if (!loan.AppraisalCompleted)
            {
                throw new InvalidOperationException("Loan must be appraised before approval");
            }

            // Get latest appraisal
            var appraisal = await GetLoanAppraisalAsync(approvalDto.LoanNo);

            var loanApproval = new LoanApproval
            {
                LoanNo = approvalDto.LoanNo,
                CompanyCode = approvalDto.CompanyCode,
                ApprovalLevel = approvalDto.ApprovalLevel,
                ApprovalStatus = approvalDto.ApprovalStatus,
                ApprovedAmount = approvalDto.ApprovedAmount ?? appraisal.RecommendedAmount,
                ApprovedInterestRate = approvalDto.ApprovedInterestRate.HasValue ?
                    approvalDto.ApprovedInterestRate.Value / 100 : appraisal.RecommendedInterestRate,
                ApprovedPeriod = approvalDto.ApprovedPeriod ?? appraisal.RecommendedPeriod,
                ApprovalComments = approvalDto.ApprovalComments,
                ApprovedBy = approvalDto.ApprovedBy,
                ApprovalDate = DateTime.Now,
                ApprovalRole = GetUserRole(approvalDto.ApprovedBy),
                IsFinalApproval = approvalDto.IsFinalApproval
            };

            _context.LoanApprovals.Add(loanApproval);
            await _context.SaveChangesAsync();

            if (approvalDto.ApprovalStatus == "Approved")
            {
                // Update loan with approved terms
                loan.ApprovedAmount = loanApproval.ApprovedAmount;
                loan.InterestRate = loanApproval.ApprovedInterestRate;
                loan.RepaymentPeriod = loanApproval.ApprovedPeriod;
                loan.LoanStatus = "Approved";
                loan.ApprovalDate = DateTime.Now;

                await _context.SaveChangesAsync();
            }
            else if (approvalDto.ApprovalStatus == "Rejected")
            {
                await UpdateLoanStatusAsync(approvalDto.LoanNo, "Rejected", approvalDto.ApprovedBy,
                    $"Loan rejected: {approvalDto.RejectionReason}");
                loan.Remarks = $"Rejected: {approvalDto.RejectionReason}";
                await _context.SaveChangesAsync();
            }

            await CreateAuditTrailAsync(loan.LoanNo, loan.LoanStatus, loan.LoanStatus, "APPROVAL_PROCESSED",
                $"Loan approval level {approvalDto.ApprovalLevel}: {approvalDto.ApprovalStatus}",
                approvalDto.ApprovedBy, approvalDto.CompanyCode);

            return loanApproval;
        }

        public async Task<List<LoanApproval>> GetLoanApprovalsAsync(string loanNo)
        {
            var approvals = await _context.LoanApprovals
                .Where(a => a.LoanNo == loanNo)
                .OrderBy(a => a.ApprovalLevel)
                .ToListAsync();

            return approvals;
        }

        public async Task<bool> IsLoanApprovedAsync(string loanNo)
        {
            var loan = await _context.Loans
                .FirstOrDefaultAsync(l => l.LoanNo == loanNo);

            return loan != null && loan.LoanStatus == "Approved";
        }

        #endregion

        #region Disbursement

        public async Task<LoanDisbursement> DisburseLoanAsync(LoanDisbursementDTO disbursementDto)
        {
            var loan = await GetLoanByNoAsync(disbursementDto.LoanNo, disbursementDto.CompanyCode);

            // Check if fully approved
            if (loan.LoanStatus != "Approved")
            {
                throw new InvalidOperationException("Loan must be fully approved before disbursement");
            }

            // Calculate fees and net amount
            var processingFee = disbursementDto.ProcessingFee ?? loan.ProcessingFee;
            var insuranceFee = disbursementDto.InsuranceFee ?? loan.InsuranceFee;
            var legalFees = disbursementDto.LegalFees ?? 0;
            var otherFees = disbursementDto.OtherFees ?? 0;
            var totalDeductions = processingFee + insuranceFee + legalFees + otherFees;
            var netAmount = loan.ApprovedAmount - totalDeductions;

            // Generate disbursement number
            var disbursementNo = await GenerateDisbursementNumberAsync(disbursementDto.CompanyCode);

            var disbursement = new LoanDisbursement
            {
                LoanNo = disbursementDto.LoanNo,
                MemberNo = loan.MemberNo,
                CompanyCode = disbursementDto.CompanyCode,
                DisbursementNo = disbursementNo,
                DisbursedAmount = loan.ApprovedAmount,
                ProcessingFee = processingFee,
                InsuranceFee = insuranceFee,
                LegalFees = legalFees,
                OtherFees = otherFees,
                TotalDeductions = totalDeductions,
                NetAmount = netAmount,
                DisbursementDate = disbursementDto.DisbursementDate,
                DisbursementMethod = disbursementDto.DisbursementMethod,
                BankName = disbursementDto.BankName,
                BankAccountNo = disbursementDto.BankAccountNo,
                ChequeNo = disbursementDto.ChequeNo,
                MobileNo = disbursementDto.MobileNo,
                DisbursedBy = disbursementDto.DisbursedBy,
                AuthorizedBy = disbursementDto.AuthorizedBy,
                AuthorizationDate = DateTime.Now,
                Remarks = disbursementDto.Remarks,
                Status = "Completed"
            };

            _context.LoanDisbursements.Add(disbursement);
            await _context.SaveChangesAsync();

            // Update loan
            loan.DisbursedAmount = loan.ApprovedAmount;
            loan.DisbursementDate = disbursementDto.DisbursementDate;
            loan.OutstandingPrincipal = loan.ApprovedAmount;
            loan.TotalOutstanding = loan.ApprovedAmount;
            loan.FirstPaymentDate = disbursementDto.DisbursementDate.AddMonths(1);
            loan.MaturityDate = disbursementDto.DisbursementDate.AddMonths(loan.RepaymentPeriod);

            // Generate loan schedule
            var schedule = await GenerateLoanScheduleAsync(loan.LoanNo);

            // Update loan status
            await UpdateLoanStatusAsync(disbursementDto.LoanNo, "Disbursed", disbursementDto.DisbursedBy,
                $"Loan disbursed on {disbursementDto.DisbursementDate:d} with net amount {netAmount:C}");

            // Record blockchain transaction
            // Record blockchain transaction
            try
            {
                var transactionData = new
                {
                    LoanNo = disbursementDto.LoanNo,
                    DisbursementNo = disbursementNo,
                    DisbursementDate = disbursementDto.DisbursementDate,
                    DisbursedAmount = disbursementDto.DisbursedAmount,
                    NetAmount = netAmount,
                    DisbursementMethod = disbursementDto.DisbursementMethod,
                    AuthorizedBy = disbursementDto.AuthorizedBy
                };

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "LOAN_DISBURSEMENT",
                    loan.MemberNo,
                    disbursementDto.CompanyCode,
                    disbursementDto.DisbursedAmount,
                    disbursementNo,
                    transactionData);

                loan.BlockchainTxId = blockchainTx.TransactionId;
                disbursement.BlockchainTxId = blockchainTx.TransactionId;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to record blockchain transaction for loan disbursement {disbursementDto.LoanNo}");
                // Continue processing - blockchain record failure shouldn't stop disbursement
            }

            await CreateAuditTrailAsync(loan.LoanNo, "Approved", "Disbursed", "DISBURSEMENT_COMPLETED",
                $"Loan disbursed: Amount {loan.ApprovedAmount:C}, Net {netAmount:C}, Method {disbursementDto.DisbursementMethod}",
                disbursementDto.DisbursedBy, disbursementDto.CompanyCode);

            return disbursement;
        }

        public async Task<LoanDisbursement> GetLoanDisbursementAsync(string loanNo)
        {
            return await _context.LoanDisbursements
                .FirstOrDefaultAsync(d => d.LoanNo == loanNo);
        }

        #endregion

        #region Schedule Generation

        public async Task<List<LoanSchedule>> GenerateLoanScheduleAsync(string loanNo)
        {
            var loan = await GetLoanByNoAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);

            if (loan.DisbursedAmount <= 0)
            {
                throw new InvalidOperationException("Cannot generate schedule for undisbursed loan");
            }

            // Remove existing schedule if any
            var existingSchedule = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .ToListAsync();

            if (existingSchedule.Any())
            {
                _context.LoanSchedules.RemoveRange(existingSchedule);
            }

            var schedules = new List<LoanSchedule>();
            var principal = loan.DisbursedAmount;
            var monthlyRate = loan.InterestRate / 12;
            var months = loan.RepaymentPeriod;

            // Calculate EMI (Equal Monthly Installment)
            var emi = principal * monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), months) /
                     (decimal)(Math.Pow((double)(1 + monthlyRate), months) - 1);

            loan.InstallmentAmount = emi;

            var balance = principal;
            var dueDate = loan.FirstPaymentDate ?? loan.DisbursementDate?.AddMonths(1) ?? DateTime.Now.AddMonths(1);

            for (int i = 1; i <= months; i++)
            {
                var interest = balance * monthlyRate;
                var principalComponent = emi - interest;

                if (i == months)
                {
                    // Adjust last payment to clear balance
                    principalComponent = balance;
                }

                var schedule = new LoanSchedule
                {
                    LoanNo = loanNo,
                    CompanyCode = loan.CompanyCode,
                    InstallmentNo = i,
                    DueDate = dueDate,
                    PrincipalAmount = principalComponent,
                    InterestAmount = interest,
                    TotalInstallment = principalComponent + interest,
                    BalancePrincipal = balance - principalComponent,
                    BalanceInterest = 0,
                    BalanceTotal = balance - principalComponent,
                    PaidPrincipal = 0,
                    PaidInterest = 0,
                    PaidTotal = 0,
                    OutstandingPrincipal = principalComponent,
                    OutstandingInterest = interest,
                    OutstandingTotal = principalComponent + interest,
                    PenaltyAmount = 0,
                    Status = "Pending",
                    DaysOverdue = 0
                };

                schedules.Add(schedule);
                balance -= principalComponent;
                dueDate = dueDate.AddMonths(1);
            }

            _context.LoanSchedules.AddRange(schedules);
            await _context.SaveChangesAsync();

            return schedules;
        }

        public async Task<List<LoanScheduleDTO>> GetLoanScheduleAsync(string loanNo)
        {
            var schedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .OrderBy(s => s.InstallmentNo)
                .Select(s => new LoanScheduleDTO
                {
                    InstallmentNo = s.InstallmentNo,
                    DueDate = s.DueDate,
                    PrincipalAmount = s.PrincipalAmount,
                    InterestAmount = s.InterestAmount,
                    TotalInstallment = s.TotalInstallment,
                    PaidAmount = s.PaidTotal,
                    OutstandingAmount = s.OutstandingTotal,
                    PenaltyAmount = s.PenaltyAmount,
                    Status = s.Status,
                    PaidDate = s.PaidDate
                })
                .ToListAsync();

            return schedules;
        }

        public async Task UpdateOverdueStatusesAsync(string companyCode)
        {
            var today = DateTime.Now.Date;

            var overdueSchedules = await _context.LoanSchedules
                .Include(s => s.Loan)
                .Where(s => s.Loan.CompanyCode == companyCode &&
                           s.Status == "Pending" &&
                           s.DueDate.Date < today)
                .ToListAsync();

            foreach (var schedule in overdueSchedules)
            {
                schedule.Status = "Overdue";
                schedule.DaysOverdue = (today - schedule.DueDate.Date).Days;
                schedule.PenaltyAmount = CalculatePenalty(schedule.OutstandingTotal, schedule.DaysOverdue);
            }

            // Update loan outstanding penalty
            var overdueLoans = overdueSchedules
                .GroupBy(s => s.LoanNo)
                .Select(g => new { LoanNo = g.Key, Penalty = g.Sum(s => s.PenaltyAmount) });

            foreach (var loanPenalty in overdueLoans)
            {
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanPenalty.LoanNo);

                if (loan != null)
                {
                    loan.OutstandingPenalty = loanPenalty.Penalty;
                    loan.TotalOutstanding = loan.OutstandingPrincipal + loan.OutstandingInterest + loan.OutstandingPenalty;
                }
            }

            await _context.SaveChangesAsync();
        }

        #endregion

        #region Repayments

        public async Task<LoanRepayment> ProcessRepaymentAsync(LoanRepaymentDTO repaymentDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var loan = await GetLoanByNoAsync(repaymentDto.LoanNo, repaymentDto.CompanyCode);

                if (loan.LoanStatus != "Disbursed" && loan.LoanStatus != "Active")
                {
                    throw new InvalidOperationException($"Cannot process repayment for loan in {loan.LoanStatus} status");
                }

                // Get overdue schedules first (penalty priority)
                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repaymentDto.LoanNo &&
                               s.Status != "Paid")
                    .OrderBy(s => s.DueDate)
                    .ToListAsync();

                var remainingAmount = repaymentDto.AmountPaid;
                var penaltyAllocated = 0m;
                var interestAllocated = 0m;
                var principalAllocated = 0m;
                var overpaymentAmount = 0m;

                // Generate receipt number
                var receiptNo = await GenerateReceiptNumberAsync(repaymentDto.CompanyCode);

                // Apply payment in order: Penalty -> Interest -> Principal
                foreach (var schedule in schedules)
                {
                    if (remainingAmount <= 0) break;

                    // 1. Pay penalty
                    if (schedule.PenaltyAmount > 0 && remainingAmount > 0)
                    {
                        var penaltyPayment = Math.Min(schedule.PenaltyAmount, remainingAmount);
                        schedule.PenaltyAmount -= penaltyPayment;
                        penaltyAllocated += penaltyPayment;
                        remainingAmount -= penaltyPayment;
                    }

                    // 2. Pay interest
                    if (schedule.OutstandingInterest > 0 && remainingAmount > 0)
                    {
                        var interestPayment = Math.Min(schedule.OutstandingInterest, remainingAmount);
                        schedule.PaidInterest += interestPayment;
                        schedule.OutstandingInterest -= interestPayment;
                        interestAllocated += interestPayment;
                        remainingAmount -= interestPayment;
                    }

                    // 3. Pay principal
                    if (schedule.OutstandingPrincipal > 0 && remainingAmount > 0)
                    {
                        var principalPayment = Math.Min(schedule.OutstandingPrincipal, remainingAmount);
                        schedule.PaidPrincipal += principalPayment;
                        schedule.OutstandingPrincipal -= principalPayment;
                        principalAllocated += principalPayment;
                        remainingAmount -= principalPayment;
                    }

                    schedule.PaidTotal = schedule.PaidPrincipal + schedule.PaidInterest;
                    schedule.OutstandingTotal = schedule.OutstandingPrincipal + schedule.OutstandingInterest + schedule.PenaltyAmount;

                    // Check if schedule is fully paid
                    if (schedule.OutstandingTotal <= 0)
                    {
                        schedule.Status = "Paid";
                        schedule.PaidDate = repaymentDto.PaymentDate;
                    }
                    else if (schedule.PaidTotal > 0)
                    {
                        schedule.Status = "Partial";
                    }
                }

                // Handle overpayment
                if (remainingAmount > 0)
                {
                    overpaymentAmount = remainingAmount;
                }

                // Create repayment record
                var repayment = new LoanRepayment
                {
                    LoanNo = repaymentDto.LoanNo,
                    MemberNo = repaymentDto.MemberNo,
                    CompanyCode = repaymentDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    PaymentDate = repaymentDto.PaymentDate,
                    AmountPaid = repaymentDto.AmountPaid,
                    PenaltyAllocated = penaltyAllocated,
                    InterestAllocated = interestAllocated,
                    PrincipalAllocated = principalAllocated,
                    OverpaymentAmount = overpaymentAmount,
                    BalanceAfterPayment = loan.TotalOutstanding - (penaltyAllocated + interestAllocated + principalAllocated),
                    PaymentMethod = repaymentDto.PaymentMethod,
                    ReferenceNo = repaymentDto.ReferenceNo,
                    ReceivedBy = repaymentDto.ReceivedBy,
                    Remarks = repaymentDto.Remarks,
                    Status = "Completed"
                };

                _context.LoanRepayments.Add(repayment);
                await _context.SaveChangesAsync();

                // Update loan balances
                loan.OutstandingPrincipal -= principalAllocated;
                loan.OutstandingInterest -= interestAllocated;
                loan.OutstandingPenalty -= penaltyAllocated;
                loan.TotalOutstanding = loan.OutstandingPrincipal + loan.OutstandingInterest + loan.OutstandingPenalty;

                // Check if loan is fully paid
                if (loan.TotalOutstanding <= 0)
                {
                    await UpdateLoanStatusAsync(loan.LoanNo, "Closed", repaymentDto.ReceivedBy, "Loan fully paid");
                }

                await _context.SaveChangesAsync();

                // Record blockchain transaction
                // Record blockchain transaction
                try
                {
                    var transactionData = new
                    {
                        LoanNo = repaymentDto.LoanNo,
                        ReceiptNo = receiptNo,
                        PaymentDate = repaymentDto.PaymentDate,
                        AmountPaid = repaymentDto.AmountPaid,
                        PenaltyAllocated = penaltyAllocated,
                        InterestAllocated = interestAllocated,
                        PrincipalAllocated = principalAllocated,
                        OverpaymentAmount = overpaymentAmount,
                        PaymentMethod = repaymentDto.PaymentMethod,
                        ReferenceNo = repaymentDto.ReferenceNo
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "LOAN_REPAYMENT",
                        repaymentDto.MemberNo,
                        repaymentDto.CompanyCode,
                        repaymentDto.AmountPaid,
                        receiptNo,
                        transactionData);

                    repayment.BlockchainTxId = blockchainTx.TransactionId;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to record blockchain transaction for repayment {receiptNo}");
                }

                await transaction.CommitAsync();

                await CreateAuditTrailAsync(loan.LoanNo, null, null, "REPAYMENT_PROCESSED",
                    $"Repayment of {repaymentDto.AmountPaid:C} processed. " +
                    $"Allocation: Penalty {penaltyAllocated:C}, Interest {interestAllocated:C}, Principal {principalAllocated:C}",
                    repaymentDto.ReceivedBy, repaymentDto.CompanyCode);

                return repayment;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<LoanRepayment>> GetLoanRepaymentsAsync(string loanNo)
        {
            var repayments = await _context.LoanRepayments
                .Where(r => r.LoanNo == loanNo)
                .OrderByDescending(r => r.PaymentDate)
                .ToListAsync();

            return repayments;
        }

        public async Task<LoanRepayment> ReverseRepaymentAsync(int repaymentId, string reason, string reversedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var repayment = await _context.LoanRepayments
                    .FirstOrDefaultAsync(r => r.Id == repaymentId);

                if (repayment == null)
                {
                    throw new InvalidOperationException("Repayment record not found");
                }

                if (repayment.Status == "Reversed")
                {
                    throw new InvalidOperationException("Repayment has already been reversed");
                }

                // Get the schedules that were affected by this payment
                var schedules = await _context.LoanSchedules
                    .Where(s => s.LoanNo == repayment.LoanNo)
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();

                // Reverse the allocations
                // This is complex - we need to reverse the last payment
                // For simplicity, we're marking the repayment as reversed and recalculating all allocations
                repayment.Status = "Reversed";
                repayment.ReversedDate = DateTime.Now;
                repayment.ReversedBy = reversedBy;
                repayment.ReversalReason = reason;

                await _context.SaveChangesAsync();

                // Recalculate all repayments to restore correct balances
                await RecalculateLoanBalancesAsync(repayment.LoanNo);

                await transaction.CommitAsync();

                await CreateAuditTrailAsync(repayment.LoanNo, null, null, "REPAYMENT_REVERSED",
                    $"Repayment {repayment.ReceiptNo} reversed: {reason}", reversedBy, repayment.CompanyCode);

                return repayment;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region State Management

        public async Task<bool> UpdateLoanStatusAsync(string loanNo, string newStatus, string performedBy, string? remarks = null)
        {
            var loan = await GetLoanByNoAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);
            var oldStatus = loan.LoanStatus;

            if (!await CanTransitionAsync(loanNo, newStatus))
            {
                throw new InvalidOperationException($"Cannot transition from {oldStatus} to {newStatus}");
            }

            loan.LoanStatus = newStatus;
            loan.ModifiedBy = performedBy;
            loan.ModifiedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await CreateAuditTrailAsync(loanNo, oldStatus, newStatus, "STATUS_CHANGE",
                remarks ?? $"Status changed from {oldStatus} to {newStatus}", performedBy, loan.CompanyCode);

            return true;
        }

        public async Task<bool> CanTransitionAsync(string loanNo, string targetStatus)
        {
            var loan = await GetLoanByNoAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);
            var currentStatus = loan.LoanStatus;

            // Define valid transitions
            var validTransitions = new Dictionary<string, List<string>>
            {
                { "Draft", new List<string> { "Submitted", "Rejected" } },
                { "Submitted", new List<string> { "UnderAppraisal", "Rejected" } },
                { "UnderAppraisal", new List<string> { "Submitted", "Rejected" } },
                { "Approved", new List<string> { "Disbursed", "Rejected" } },
                { "Rejected", new List<string>() },
                { "Disbursed", new List<string> { "Active", "Closed", "WrittenOff" } },
                { "Active", new List<string> { "Closed", "WrittenOff" } },
                { "Closed", new List<string>() },
                { "WrittenOff", new List<string>() }
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

            // Check if member has existing active loans that exceed limit
            var activeLoans = await _context.Loans
                .CountAsync(l => l.MemberNo == application.MemberNo &&
                                l.CompanyCode == application.CompanyCode &&
                                (l.LoanStatus == "Disbursed" || l.LoanStatus == "Active"));

            if (activeLoans >= (loanType.MaxLoans ?? int.MaxValue))
            {
                return (false, $"Member has reached maximum number of active loans ({loanType.MaxLoans})");
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

            // Check minimum contribution period (if applicable)
            // This would check how long the member has been contributing

            // Check existing loan defaults
            var hasDefaulted = await HasPreviousDefaultAsync(memberNo, companyCode);
            if (hasDefaulted)
            {
                return (false, "Member has previous loan defaults");
            }

            return (true, "Member is eligible");
        }

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
                           (l.LoanStatus == "Disbursed" || l.LoanStatus == "Active"))
                .SumAsync(l => l.OutstandingPrincipal);

            maxAmount -= existingLoans;

            return Math.Max(0, maxAmount);
        }

        #endregion

        #region Audit

        public async Task<List<LoanAuditTrail>> GetLoanAuditTrailAsync(string loanNo)
        {
            var auditTrails = await _context.LoanAuditTrails
                .Where(a => a.LoanNo == loanNo)
                .OrderByDescending(a => a.PerformedDate)
                .ToListAsync();

            return auditTrails;
        }

        #endregion

        #region Private Helper Methods

        private async Task<string> GenerateLoanNumberAsync(string companyCode)
        {
            var prefix = "LN";
            var date = DateTime.Now.ToString("yyyyMM");
            var sequence = 1;

            var lastLoan = await _context.Loans
                .Where(l => l.CompanyCode == companyCode && l.LoanNo.StartsWith($"{prefix}{date}"))
                .OrderByDescending(l => l.LoanNo)
                .FirstOrDefaultAsync();

            if (lastLoan != null)
            {
                var lastSequence = int.Parse(lastLoan.LoanNo.Substring(10));
                sequence = lastSequence + 1;
            }

            return $"{prefix}{date}{sequence:D6}";
        }

        private async Task<string> GenerateDisbursementNumberAsync(string companyCode)
        {
            var prefix = "DISB";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            var lastDisbursement = await _context.LoanDisbursements
                .Where(d => d.CompanyCode == companyCode && d.DisbursementNo.StartsWith($"{prefix}{date}"))
                .OrderByDescending(d => d.DisbursementNo)
                .FirstOrDefaultAsync();

            if (lastDisbursement != null)
            {
                var lastSequence = int.Parse(lastDisbursement.DisbursementNo.Substring(11));
                sequence = lastSequence + 1;
            }

            return $"{prefix}{date}{sequence:D4}";
        }

        private async Task<string> GenerateReceiptNumberAsync(string companyCode)
        {
            var prefix = "RCPT";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = 1;

            var lastReceipt = await _context.LoanRepayments
                .Where(r => r.CompanyCode == companyCode && r.ReceiptNo.StartsWith($"{prefix}{date}"))
                .OrderByDescending(r => r.ReceiptNo)
                .FirstOrDefaultAsync();

            if (lastReceipt != null)
            {
                var lastSequence = int.Parse(lastReceipt.ReceiptNo.Substring(11));
                sequence = lastSequence + 1;
            }

            return $"{prefix}{date}{sequence:D4}";
        }

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
                var hasDefault = previousLoans.Any(l => l.LoanStatus == "WrittenOff");
                if (hasDefault) score -= 100;

                // Check repayment history
                var closedLoans = previousLoans.Count(l => l.LoanStatus == "Closed");
                score += closedLoans * 20;
            }

            return Math.Clamp(score, 300, 850);
        }

        private async Task<bool> HasPreviousDefaultAsync(string memberNo, string companyCode)
        {
            return await _context.Loans
                .AnyAsync(l => l.MemberNo == memberNo &&
                              l.CompanyCode == companyCode &&
                              l.LoanStatus == "WrittenOff");
        }

        private async Task<int> CalculateLoanHistoryRatingAsync(string memberNo, string companyCode)
        {
            var loans = await _context.Loans
                .Include(l => l.LoanRepayments)
                .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                .ToListAsync();

            if (!loans.Any()) return 3; // No history - average

            var totalLoans = loans.Count;
            var closedLoans = loans.Count(l => l.LoanStatus == "Closed");
            var onTimePayments = loans.SelectMany(l => l.LoanRepayments ?? new List<LoanRepayment>())
                .Count(r => r.Status == "Completed");

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

        private decimal CalculatePenalty(decimal amount, int daysOverdue)
        {
            // 2% per month on overdue amount, prorated
            var monthlyRate = 0.02m;
            var dailyRate = monthlyRate / 30;
            return amount * dailyRate * daysOverdue;
        }

        private async Task RecalculateLoanBalancesAsync(string loanNo)
        {
            var loan = await GetLoanByNoAsync(loanNo, (await _context.Loans.FirstAsync(l => l.LoanNo == loanNo)).CompanyCode);
            var schedules = await _context.LoanSchedules
                .Where(s => s.LoanNo == loanNo)
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            // Reset loan balances
            loan.OutstandingPrincipal = loan.DisbursedAmount;
            loan.OutstandingInterest = 0;
            loan.OutstandingPenalty = 0;

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
            var repayments = await _context.LoanRepayments
                .Where(r => r.LoanNo == loanNo && r.Status == "Completed")
                .OrderBy(r => r.PaymentDate)
                .ToListAsync();

            foreach (var repayment in repayments)
            {
                // Reprocess repayment (simplified - you'd call ProcessRepaymentAsync here)
                // This is a placeholder for the full recalculation logic
            }
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
            var audit = new LoanAuditTrail
            {
                LoanNo = loanNo,
                CompanyCode = companyCode,
                Action = action,
                PreviousStatus = previousStatus ?? "",
                NewStatus = newStatus ?? "",
                Description = description,
                PerformedBy = performedBy,
                PerformedDate = DateTime.Now,
                IpAddress = null // Would get from HttpContext
            };

            _context.LoanAuditTrails.Add(audit);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}