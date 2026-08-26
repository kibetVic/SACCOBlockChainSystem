// Controllers/LoanApplicationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("Member/Loan")]
    public class LoanApplicationController : Controller
    {
        private readonly ISelfServiceLoanService _selfServiceLoanService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanApplicationController> _logger;

        public LoanApplicationController(
            ISelfServiceLoanService selfServiceLoanService,
            ApplicationDbContext context,
            ILogger<LoanApplicationController> logger)
        {
            _selfServiceLoanService = selfServiceLoanService;
            _context = context;
            _logger = logger;
        }

        private string GetCurrentMemberNo()
        {
            var memberNo = User.FindFirstValue("MemberNo") ??
                          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          User.Identity?.Name;

            if (string.IsNullOrEmpty(memberNo))
            {
                throw new UnauthorizedAccessException("Member not authenticated");
            }

            return memberNo;
        }
        private string GetCurrentCompanyCode()
        {
            return User.FindFirstValue("CompanyCode") ?? "DEFAULT";
        }

        #region Dashboard

        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var dashboard = await _selfServiceLoanService.GetMemberDashboardAsync(memberNo, companyCode);
                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan dashboard");
                TempData["Error"] = "Unable to load dashboard. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

        #region Loan Products

        [HttpGet("Products")]
        public async Task<IActionResult> Products()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var products = await _selfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode);
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan products");
                TempData["Error"] = "Unable to load loan products.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet("ProductDetails/{loanCode}")]
        public async Task<IActionResult> ProductDetails(string loanCode)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var product = (await _selfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode))
                    .FirstOrDefault(p => p.LoanCode == loanCode);

                if (product == null)
                {
                    TempData["Error"] = "Loan product not found.";
                    return RedirectToAction("Products");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details");
                TempData["Error"] = "Unable to load product details.";
                return RedirectToAction("Products");
            }
        }

        #endregion

        #region Apply for Loan

        [HttpGet("Apply/{loanCode}")]
        public async Task<IActionResult> Apply(string loanCode)
        {
            try
            {
                _logger.LogInformation($"=== APPLY ACTION CALLED ===");
                _logger.LogInformation($"LoanCode: {loanCode}");

                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    TempData["Error"] = "Loan product not found.";
                    return RedirectToAction("Products");
                }

                // ============================================================
                // CHECK FOR EXISTING ACTIVE LOANS
                // ============================================================
                var activeStatuses = new[] { 2, 3, 4, 5, 6 }; // Submitted, UnderAppraisal, Approved, Endorsed, Disbursed
                var existingActiveLoan = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo &&
                                  l.CompanyCode == companyCode &&
                                  activeStatuses.Contains(l.Status ?? 0));

                if (existingActiveLoan)
                {
                    TempData["Error"] = "You already have an active loan. Please clear your existing loan before applying for a new one.";
                    return RedirectToAction("MyLoans");
                }

                // ============================================================
                // CHECK MINIMUM SHARES REQUIREMENT (if applicable)
                // ============================================================
                if (loanType.SelfGuarantee == true)
                {
                    var totalShares = await _context.ContribShares
                        .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                    decimal minimumSharesRequired = 1000; // Minimum shares required - adjust as needed

                    if (totalShares < minimumSharesRequired)
                    {
                        TempData["Error"] = $"You need at least {minimumSharesRequired:C} in shares to apply for this loan. Your current shares: {totalShares:C}";
                        return RedirectToAction("Products");
                    }
                }

                var eligibility = await _selfServiceLoanService.CheckLoanEligibilityAsync(memberNo, loanCode, companyCode);

                if (!eligibility.IsEligible)
                {
                    TempData["Error"] = eligibility.Message;
                    return RedirectToAction("Products");
                }

                var viewModel = new LoanApplicationViewModel
                {
                    LoanCode = loanCode,
                    LoanName = loanType.LoanType1 ?? loanCode,
                    MaxAmount = eligibility.MaxAmount,
                    MinAmount = eligibility.MinAmount > 0 ? eligibility.MinAmount : 500,
                    EligibleAmount = eligibility.EligibleAmount,
                    InterestRate = eligibility.InterestRate,
                    MaxRepayPeriod = eligibility.RepaymentPeriodMonths,
                    EstimatedMonthlyInstallment = eligibility.EstimatedMonthlyInstallment,
                    IsMobileLoan = eligibility.IsMobileLoan,
                    RequiresGuarantor = eligibility.RequiresGuarantor,
                    SelfGuarantee = eligibility.SelfGuarantee,
                    AvailableSharesForGuarantee = eligibility.AvailableSharesForGuarantee,
                    CompanyCode = companyCode,
                    PrincipalAmount = Math.Min(eligibility.MinAmount > 0 ? eligibility.MinAmount : 500, eligibility.EligibleAmount),
                    RepayPeriod = Math.Min(12, eligibility.RepaymentPeriodMonths)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application form for loan {LoanCode}", loanCode);
                TempData["Error"] = "Unable to load application form. " + ex.Message;
                return RedirectToAction("Products");
            }
        }

        [HttpPost("ApplyLoan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLoan([FromForm] LoanApplicationViewModel model)
        {
            // Log the incoming request for debugging
            _logger.LogInformation("=== APPLYLOAN POST RECEIVED ===");
            _logger.LogInformation($"LoanCode: {model.LoanCode}");
            _logger.LogInformation($"PrincipalAmount: {model.PrincipalAmount}");
            _logger.LogInformation($"RepayPeriod: {model.RepayPeriod}");
            _logger.LogInformation($"Purpose: {model.Purpose}");
            _logger.LogInformation($"IsMobileLoan: {model.IsMobileLoan}");

            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                _logger.LogInformation($"MemberNo: {memberNo}, CompanyCode: {companyCode}");

                // Get loan type first for self-guarantee validation
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == model.LoanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    TempData["Error"] = "Loan product not found.";
                    return RedirectToAction("Products");
                }

                // ============================================================
                // CHECK FOR EXISTING ACTIVE LOANS
                // ============================================================
                var activeStatuses = new[] { 2, 3, 4, 5, 6 }; // Submitted, UnderAppraisal, Approved, Endorsed, Disbursed
                var existingActiveLoan = await _context.Loans
                    .AnyAsync(l => l.MemberNo == memberNo &&
                                  l.CompanyCode == companyCode &&
                                  activeStatuses.Contains(l.Status ?? 0));

                if (existingActiveLoan)
                {
                    _logger.LogWarning($"Member {memberNo} already has an active loan. Cannot apply for new loan.");
                    TempData["Error"] = "You already have an active loan. Please clear your existing loan before applying for a new one.";
                    return RedirectToAction("MyLoans");
                }

                // Get eligibility data for validation
                var eligibility = await _selfServiceLoanService.CheckLoanEligibilityAsync(memberNo, model.LoanCode, companyCode);

                // MANUAL VALIDATION
                var errors = new List<string>();

                if (string.IsNullOrEmpty(model.LoanCode))
                    errors.Add("Loan code is required");

                if (model.PrincipalAmount <= 0)
                    errors.Add("Loan amount must be greater than 0");

                if (model.RepayPeriod <= 0)
                    errors.Add("Repayment period must be greater than 0");

                // Validate against eligibility
                if (model.PrincipalAmount < (eligibility.MinAmount > 0 ? eligibility.MinAmount : 500))
                    errors.Add($"Minimum loan amount is {(eligibility.MinAmount > 0 ? eligibility.MinAmount : 500):C}");

                if (model.PrincipalAmount > eligibility.EligibleAmount)
                    errors.Add($"Maximum eligible amount is {eligibility.EligibleAmount:C}");

                if (model.RepayPeriod > eligibility.RepaymentPeriodMonths)
                    errors.Add($"Maximum repayment period is {eligibility.RepaymentPeriodMonths} months");

                // ============================================================
                // CHECK SHARES FOR SELF-GUARANTEE
                // ============================================================
                decimal totalShares = 0;
                if (loanType.SelfGuarantee == true)
                {
                    totalShares = await _context.ContribShares
                        .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                        .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                    _logger.LogInformation($"Self-guarantee validation: Loan Amount={model.PrincipalAmount:C}, Total Shares={totalShares:C}");

                    // Minimum shares requirement (can be configured)
                    decimal minimumSharesRequired = 1000;

                    if (totalShares < minimumSharesRequired)
                    {
                        errors.Add($"You need at least {minimumSharesRequired:C} in shares to apply for this loan. Your current shares: {totalShares:C}");
                        _logger.LogWarning($"Insufficient shares: {totalShares:C} < {minimumSharesRequired:C}");
                    }

                    // For mobile loans, loan amount cannot exceed shares
                    if (loanType.MobileLoan == true && model.PrincipalAmount > totalShares)
                    {
                        errors.Add($"Your loan amount ({model.PrincipalAmount:C}) cannot exceed your shares ({totalShares:C}) which serve as self-guarantee.");
                        _logger.LogWarning($"Self-guarantee failed: Loan amount {model.PrincipalAmount:C} exceeds shares {totalShares:C}");
                    }

                    // For regular loans with self-guarantee, check if shares are sufficient for the guarantee percentage
                    if (loanType.MobileLoan != true)
                    {
                        decimal guaranteePercentage = 0.05m; // 5% of loan amount
                        decimal requiredGuaranteeAmount = model.PrincipalAmount * guaranteePercentage;
                        if (requiredGuaranteeAmount < 500) requiredGuaranteeAmount = 500;

                        if (totalShares < requiredGuaranteeAmount)
                        {
                            errors.Add($"Insufficient shares for self-guarantee. Required: {requiredGuaranteeAmount:C}, Available: {totalShares:C}");
                            _logger.LogWarning($"Insufficient shares for guarantee: {totalShares:C} < {requiredGuaranteeAmount:C}");
                        }
                    }
                }

                // If there are validation errors, return to the view
                if (errors.Any())
                {
                    _logger.LogWarning($"Validation errors: {string.Join(", ", errors)}");
                    foreach (var error in errors)
                    {
                        ModelState.AddModelError("", error);
                    }

                    // Reload the view with eligibility data
                    model.MaxAmount = eligibility.MaxAmount;
                    model.MinAmount = eligibility.MinAmount > 0 ? eligibility.MinAmount : 500;
                    model.EligibleAmount = loanType.SelfGuarantee == true && loanType.MobileLoan == true
                        ? Math.Min(eligibility.EligibleAmount, totalShares)
                        : eligibility.EligibleAmount;
                    model.InterestRate = eligibility.InterestRate;
                    model.MaxRepayPeriod = eligibility.RepaymentPeriodMonths;
                    model.IsMobileLoan = eligibility.IsMobileLoan;
                    model.RequiresGuarantor = eligibility.RequiresGuarantor;
                    model.SelfGuarantee = loanType.SelfGuarantee == true;
                    model.AvailableSharesForGuarantee = totalShares;
                    model.EstimatedMonthlyInstallment = CalculateMonthlyInstallment(model.PrincipalAmount, eligibility.InterestRate, model.RepayPeriod);

                    return View("Apply", model);
                }

                // ============================================================
                // CHECK MAX LOANS ALLOWED
                // ============================================================
                if (loanType.MaxLoans.HasValue && loanType.MaxLoans.Value > 0)
                {
                    // Count closed/completed loans (excluding Draft, Rejected, WrittenOff)
                    var completedLoansCount = await _context.Loans
                        .CountAsync(l => l.MemberNo == memberNo &&
                                        l.CompanyCode == companyCode &&
                                        l.Status == 7); // Closed status

                    if (completedLoansCount >= loanType.MaxLoans.Value)
                    {
                        _logger.LogWarning($"Member {memberNo} has reached maximum number of loans ({loanType.MaxLoans.Value})");
                        TempData["Error"] = $"You have reached the maximum number of loans ({loanType.MaxLoans.Value}) for this product.";
                        return RedirectToAction("MyLoans");
                    }
                }

                // Get IP address for audit
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ??
                               HttpContext.Request.Headers["X-Forwarded-For"].ToString() ??
                               "Unknown";

                // Create the application DTO
                var application = new SelfLoanApplicationDTO
                {
                    LoanCode = model.LoanCode,
                    PrincipalAmount = model.PrincipalAmount,
                    RepayPeriod = model.RepayPeriod,
                    Purpose = string.IsNullOrEmpty(model.Purpose) ? "General purpose" : model.Purpose,
                    Remarks = string.IsNullOrEmpty(model.Remarks) ? "Applied via self-service portal" : model.Remarks,
                    CompanyCode = companyCode,
                    IpAddress = ipAddress
                };

                _logger.LogInformation($"Calling ApplyForLoanAsync with: LoanCode={application.LoanCode}, Amount={application.PrincipalAmount}, Period={application.RepayPeriod}");

                // Submit the application
                var result = await _selfServiceLoanService.ApplyForLoanAsync(application, memberNo);

                _logger.LogInformation($"ApplyForLoanAsync result: Success={result.Success}, LoanNo={result.LoanNo}, Message={result.Message}");

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    _logger.LogInformation("Loan application successful for member {MemberNo}, LoanNo: {LoanNo}", memberNo, result.LoanNo);

                    if (result.CanWithdrawNow)
                    {
                        return RedirectToAction("Withdraw", new { loanNo = result.LoanNo });
                    }

                    return RedirectToAction("Status", new { loanNo = result.LoanNo });
                }
                else
                {
                    TempData["Error"] = result.Message;
                    _logger.LogWarning("Loan application failed for member {MemberNo}: {Message}", memberNo, result.Message);

                    // Reload the Apply view with the original data
                    model.MaxAmount = eligibility.MaxAmount;
                    model.MinAmount = eligibility.MinAmount > 0 ? eligibility.MinAmount : 500;
                    model.EligibleAmount = loanType.SelfGuarantee == true && loanType.MobileLoan == true
                        ? Math.Min(eligibility.EligibleAmount, totalShares)
                        : eligibility.EligibleAmount;
                    model.InterestRate = eligibility.InterestRate;
                    model.MaxRepayPeriod = eligibility.RepaymentPeriodMonths;
                    model.IsMobileLoan = eligibility.IsMobileLoan;
                    model.RequiresGuarantor = eligibility.RequiresGuarantor;
                    model.SelfGuarantee = loanType.SelfGuarantee == true;
                    model.AvailableSharesForGuarantee = totalShares;
                    model.EstimatedMonthlyInstallment = CalculateMonthlyInstallment(model.PrincipalAmount, eligibility.InterestRate, model.RepayPeriod);

                    return View("Apply", model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application for member {MemberNo}", GetCurrentMemberNo());
                TempData["Error"] = $"Unable to submit application: {ex.Message}";

                // Still return the view so user can try again
                return View("Apply", model);
            }
        }

        private decimal CalculateMonthlyInstallment(decimal principal, decimal annualRate, int months)
        {
            if (principal <= 0 || months <= 0) return 0;

            decimal monthlyRate = (annualRate / 100) / 12;

            if (monthlyRate > 0)
            {
                double factor = Math.Pow((double)(1 + monthlyRate), months);
                decimal monthlyPayment = principal * monthlyRate * (decimal)factor / ((decimal)factor - 1);
                return Math.Round(monthlyPayment, 2);
            }

            return Math.Round(principal / months, 2);
        }

        #endregion

        #region Loan Status

        [HttpGet("Status/{loanNo}")]
        public async Task<IActionResult> Status(string loanNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var result = await _selfServiceLoanService.GetLoanStatusAsync(loanNo, memberNo);

                if (!result.Success)
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("MyLoans");
                }

                var schedule = await _selfServiceLoanService.GetLoanRepaymentScheduleAsync(loanNo, memberNo);

                var viewModel = new LoanStatusViewModel
                {
                    LoanNo = result.LoanNo,
                    Status = result.Status,
                    Message = result.Message,
                    IsMobileLoan = result.IsMobileLoan,
                    CanWithdraw = result.CanWithdrawNow,
                    Amount = result.Amount ?? 0,
                    ApplicationDate = result.ApplicationDate ?? DateTime.Now,
                    RepaymentSchedule = schedule
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan status for {LoanNo}", loanNo);
                TempData["Error"] = "Unable to load loan status.";
                return RedirectToAction("MyLoans");
            }
        }

        #endregion


        #region Withdraw Loan

        [HttpGet("Withdraw")]
        public IActionResult Withdraw()
        {
            TempData["Error"] = "Please select a specific loan to withdraw.";
            return RedirectToAction("MyLoans");
        }

        [HttpGet("Withdraw/{loanNo}")]
        public async Task<IActionResult> Withdraw(string loanNo)
        {
            try
            {
                _logger.LogInformation($"=== WITHDRAW GET CALLED ===");
                _logger.LogInformation($"LoanNo: {loanNo}");

                if (string.IsNullOrEmpty(loanNo))
                {
                    TempData["Error"] = "No loan specified.";
                    return RedirectToAction("MyLoans");
                }

                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                var loanEntity = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loanEntity == null)
                {
                    TempData["Error"] = "Loan not found.";
                    return RedirectToAction("MyLoans");
                }

                // ============================================================
                // GET LOAN TYPE TO CHECK APPROVAL REQUIREMENT
                // ============================================================
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanEntity.LoanCode && lt.CompanyCode == companyCode);

                bool requiresApproval = loanType?.MobileLoanApproval == true;
                bool isApproved = loanEntity.Status == 5; // Endorsed
                bool isPending = loanEntity.Status == 2 && loanEntity.Posted == "PENDING_APPROVAL";
                bool isRejected = loanEntity.Status == 10;

                _logger.LogInformation($"Loan Status: {loanEntity.Status}, Posted: {loanEntity.Posted}, RequiresApproval: {requiresApproval}, IsApproved: {isApproved}");

                // ============================================================
                // CHECK APPROVAL STATUS AND SHOW APPROPRIATE MESSAGE
                // ============================================================
                if (isRejected)
                {
                    TempData["Error"] = "This loan has been rejected. Please contact support for more information.";
                    return RedirectToAction("MyLoans");
                }

                if (isPending)
                {
                    TempData["Error"] = "⚠️ This loan is waiting for approval. You will be notified once approved.";
                    return RedirectToAction("MyLoans");
                }

                if (loanEntity.Status == 6)
                {
                    TempData["Error"] = "This loan has already been disbursed.";
                    return RedirectToAction("MyLoans");
                }

                if (!isApproved && requiresApproval)
                {
                    TempData["Error"] = "This loan requires approval before withdrawal. Please wait for approval.";
                    return RedirectToAction("MyLoans");
                }

                if (loanEntity.Status != 5)
                {
                    TempData["Error"] = $"Loan is not ready for withdrawal. Current status: {GetStatusString(loanEntity.Status)}";
                    return RedirectToAction("MyLoans");
                }

                // ============================================================
                // CONTINUE WITH WITHDRAWAL LOGIC
                // ============================================================
                var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
                var cheque = await _context.Cheques.FirstOrDefaultAsync(c => c.LoanNo == loanNo);

                decimal processingFeePercentage = loanType?.Processingfee ?? 0;
                decimal grossAmount = loanEntity.LoanAmt ?? 0;
                decimal processingFeeAmount = (grossAmount * processingFeePercentage) / 100;
                decimal netAmount = grossAmount - processingFeeAmount;

                _logger.LogInformation($"Withdrawal calculation: Gross={grossAmount:C}, Fee%={processingFeePercentage}%, FeeAmount={processingFeeAmount:C}, Net={netAmount:C}");

                string memberPhone = member?.PhoneNo ?? member?.MobileNo ?? "";
                string displayPhone = memberPhone;
                if (displayPhone.StartsWith("254") && displayPhone.Length == 12)
                {
                    displayPhone = "0" + displayPhone.Substring(3);
                }

                var viewModel = new LoanWithdrawalViewModel
                {
                    LoanNo = loanNo,
                    Amount = grossAmount,
                    ProcessingFee = processingFeeAmount,
                    ProcessingFeePercentage = processingFeePercentage,
                    MemberPhone = displayPhone,
                    MpesaPhoneNumber = displayPhone,
                    MemberAccount = member?.Accno ?? "",
                    WithdrawalMethods = GetAvailableWithdrawalMethods(),
                    WithdrawalMethod = "MPESA",
                    // SET APPROVAL PROPERTIES
                    IsApproved = isApproved,
                    RequiresApproval = requiresApproval,
                    Status = GetStatusString(loanEntity.Status),
                    Posted = loanEntity.Posted ?? ""
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdrawal page");
                TempData["Error"] = "Unable to process withdrawal request.";
                return RedirectToAction("MyLoans");
            }
        }


        //[HttpGet("Withdraw/{loanNo}")]
        //public async Task<IActionResult> Withdraw(string loanNo)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"=== WITHDRAW GET CALLED ===");
        //        _logger.LogInformation($"LoanNo: {loanNo}");

        //        if (string.IsNullOrEmpty(loanNo))
        //        {
        //            TempData["Error"] = "No loan specified.";
        //            return RedirectToAction("MyLoans");
        //        }

        //        var memberNo = GetCurrentMemberNo();
        //        var companyCode = GetCurrentCompanyCode();

        //        var loanEntity = await _context.Loans
        //            .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

        //        if (loanEntity == null)
        //        {
        //            TempData["Error"] = "Loan not found.";
        //            return RedirectToAction("MyLoans");
        //        }

        //        // ============================================================
        //        // CHECK: If loan is pending approval
        //        // ============================================================
        //        if (loanEntity.Status == 2 && loanEntity.Posted == "PENDING_APPROVAL")
        //        {
        //            TempData["Error"] = "⚠️ This loan is waiting for approval. You will be notified once approved.";
        //            return RedirectToAction("MyLoans");
        //        }

        //        // ============================================================
        //        // CHECK: If loan is rejected
        //        // ============================================================
        //        if (loanEntity.Status == 10)
        //        {
        //            TempData["Error"] = "This loan has been rejected. Please contact support for more information.";
        //            return RedirectToAction("MyLoans");
        //        }

        //        // ============================================================
        //        // CHECK: If loan is already disbursed
        //        // ============================================================
        //        if (loanEntity.Status == 6)
        //        {
        //            TempData["Error"] = "This loan has already been disbursed.";
        //            return RedirectToAction("MyLoans");
        //        }

        //        // ============================================================
        //        // CHECK: If loan is ready for withdrawal (Endorsed status = 5)
        //        // ============================================================
        //        if (loanEntity.Status != 5)
        //        {
        //            TempData["Error"] = $"Loan is not ready for withdrawal. Current status: {GetStatusString(loanEntity.Status)}";
        //            return RedirectToAction("MyLoans");
        //        }

        //        // ============================================================
        //        // CONTINUE WITH THE REST OF THE WITHDRAW LOGIC
        //        // ============================================================
        //        var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
        //        var loanType = await _context.Loantypes.FirstOrDefaultAsync(lt => lt.LoanCode == loanEntity.LoanCode);

        //        // Get the cheque record to get the correct amounts
        //        var cheque = await _context.Cheques.FirstOrDefaultAsync(c => c.LoanNo == loanNo);

        //        // CORRECT: Calculate processing fee as PERCENTAGE
        //        decimal processingFeePercentage = loanType?.Processingfee ?? 0;
        //        decimal grossAmount = loanEntity.LoanAmt ?? 0;
        //        decimal processingFeeAmount = (grossAmount * processingFeePercentage) / 100;
        //        decimal netAmount = grossAmount - processingFeeAmount;

        //        _logger.LogInformation($"Withdrawal calculation: Gross={grossAmount:C}, Fee%={processingFeePercentage}%, FeeAmount={processingFeeAmount:C}, Net={netAmount:C}");

        //        string memberPhone = member?.PhoneNo ?? member?.MobileNo ?? "";

        //        // Remove 254 prefix for display
        //        string displayPhone = memberPhone;
        //        if (displayPhone.StartsWith("254") && displayPhone.Length == 12)
        //        {
        //            displayPhone = "0" + displayPhone.Substring(3);
        //        }

        //        var viewModel = new LoanWithdrawalViewModel
        //        {
        //            LoanNo = loanNo,
        //            Amount = grossAmount,
        //            ProcessingFee = processingFeeAmount,
        //            ProcessingFeePercentage = processingFeePercentage,
        //            MemberPhone = displayPhone,
        //            MpesaPhoneNumber = displayPhone,
        //            MemberAccount = member?.Accno ?? "",
        //            WithdrawalMethods = GetAvailableWithdrawalMethods(),
        //            WithdrawalMethod = "MPESA"
        //        };

        //        return View(viewModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading withdrawal page");
        //        TempData["Error"] = "Unable to process withdrawal request.";
        //        return RedirectToAction("MyLoans");
        //    }
        //}

        [HttpPost("Withdraw/{loanNo}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(string loanNo, LoanWithdrawalViewModel model)
        {
            _logger.LogInformation($"=== WITHDRAW POST CALLED ===");
            _logger.LogInformation($"LoanNo: {loanNo}, Method: {model.WithdrawalMethod}");

            try
            {
                // Validate
                if (model.WithdrawalMethod == "MPESA" && string.IsNullOrWhiteSpace(model.MpesaPhoneNumber))
                {
                    TempData["Error"] = "M-Pesa phone number is required.";
                    return RedirectToAction("Withdraw", new { loanNo });
                }

                var memberNo = GetCurrentMemberNo();

                // Format phone number for M-Pesa
                string cleanPhoneNumber = null;
                if (model.WithdrawalMethod == "MPESA" && !string.IsNullOrEmpty(model.MpesaPhoneNumber))
                {
                    cleanPhoneNumber = FormatPhoneNumber(model.MpesaPhoneNumber);
                }

                var result = await _selfServiceLoanService.WithdrawLoanAsync(loanNo, memberNo, model.WithdrawalMethod, cleanPhoneNumber);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("DisbursementConfirmation", new { loanNo });
                }
                else
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("Withdraw", new { loanNo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal");
                TempData["Error"] = $"Withdrawal failed: {ex.Message}";
                return RedirectToAction("Withdraw", new { loanNo });
            }
        }

        [HttpGet("DisbursementConfirmation/{loanNo}")]
        public async Task<IActionResult> DisbursementConfirmation(string loanNo)
        {
            try
            {
                _logger.LogInformation($"=== DISBURSEMENT CONFIRMATION CALLED ===");
                _logger.LogInformation($"LoanNo: {loanNo}");

                var memberNo = GetCurrentMemberNo();
                var loan = await _context.Loans.FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return RedirectToAction("MyLoans");
                }

                var cheque = await _context.Cheques.FirstOrDefaultAsync(c => c.LoanNo == loanNo);
                var loanType = await _context.Loantypes.FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode);

                // CORRECT: Calculate processing fee as PERCENTAGE
                decimal processingFeePercentage = loanType?.Processingfee ?? 0;
                decimal grossAmount = cheque?.AmountIssued ?? cheque?.Amount ?? loan.LoanAmt ?? 0;
                decimal processingFeeAmount = (grossAmount * processingFeePercentage) / 100;
                decimal netAmount = grossAmount - processingFeeAmount;

                _logger.LogInformation($"Confirmation calculation: Gross={grossAmount:C}, Fee%={processingFeePercentage}%, FeeAmount={processingFeeAmount:C}, Net={netAmount:C}");

                var viewModel = new DisbursementConfirmationViewModel
                {
                    LoanNo = loanNo,
                    Amount = grossAmount,
                    NetAmount = netAmount,
                    ProcessingFee = processingFeeAmount,
                    ProcessingFeePercentage = processingFeePercentage,
                    DisbursementDate = cheque?.DateIssued ?? DateTime.Now,
                    ChequeNo = cheque?.ChequeNo,
                    WithdrawalMethod = "MPESA"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading confirmation");
                return RedirectToAction("MyLoans");
            }
        }

        #endregion


        private string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber)) return phoneNumber;

            // Remove any non-digit characters
            phoneNumber = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"\D", "");

            // Format to 254XXXXXXXXX
            if (phoneNumber.StartsWith("0"))
            {
                phoneNumber = "254" + phoneNumber.Substring(1);
            }
            else if (phoneNumber.StartsWith("254") && phoneNumber.Length == 12)
            {
                // Already in correct format
            }
            else if (phoneNumber.Length == 9)
            {
                phoneNumber = "254" + phoneNumber;
            }
            else if (phoneNumber.Length == 10 && phoneNumber.StartsWith("07"))
            {
                phoneNumber = "254" + phoneNumber.Substring(1);
            }

            return phoneNumber;
        }

        private List<WithdrawalMethodDTO> GetAvailableWithdrawalMethods()
        {
            return new List<WithdrawalMethodDTO>
            {
                new WithdrawalMethodDTO { Code = "MPESA", Name = "M-Pesa", RequiresPhoneNumber = true, Icon = "fa-mobile-alt" },
                new WithdrawalMethodDTO { Code = "FOSA", Name = "FOSA Account", RequiresPhoneNumber = false, Icon = "fa-university" },
                new WithdrawalMethodDTO { Code = "CHEQUE", Name = "Cheque", RequiresPhoneNumber = false, Icon = "fa-file-alt" }
            };
        }




        #region My Loans

        [HttpGet("MyLoans")]
        public async Task<IActionResult> MyLoans()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                _logger.LogInformation($"=== MY LOANS CALLED ===");
                _logger.LogInformation($"MemberNo: {memberNo}");
                _logger.LogInformation($"CompanyCode from claim: {companyCode}");

                // DIRECT DATABASE QUERY - Skip the service for now to debug
                var loanEntities = await _context.Loans
                    .Where(l => l.MemberNo == memberNo)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                _logger.LogInformation($"Found {loanEntities.Count} loans in database for member {memberNo}");

                if (!loanEntities.Any())
                {
                    _logger.LogWarning($"No loans found for member {memberNo}");
                    return View(new List<MemberLoanSummaryDTO>());
                }

                var loans = new List<MemberLoanSummaryDTO>();

                foreach (var loanEntity in loanEntities)
                {
                    _logger.LogInformation($"Processing loan: {loanEntity.LoanNo}, Status={loanEntity.Status}");

                    // Get loan balance
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loanEntity.LoanNo);

                    // Get loan type name
                    var loanType = await _context.Loantypes
                        .Where(lt => lt.LoanCode == loanEntity.LoanCode)
                        .Select(lt => lt.LoanType1)
                        .FirstOrDefaultAsync();

                    // Map status to string
                    string statusString = GetStatusString(loanEntity.Status);
                    _logger.LogInformation($"Loan {loanEntity.LoanNo} status mapped to: {statusString}");

                    var dto = new MemberLoanSummaryDTO
                    {
                        LoanNo = loanEntity.LoanNo,
                        LoanType = loanType ?? loanEntity.LoanCode ?? "Unknown",
                        PrincipalAmount = loanEntity.LoanAmt ?? 0,
                        Status = statusString,
                        ApplicationDate = loanEntity.ApplicDate,
                        DisbursementDate = loanEntity.AuditDateTime,
                        OutstandingBalance = loanbal?.Balance ?? 0,
                        NextPaymentDate = loanbal?.Nextduedate,
                        MonthlyInstallment = loanEntity.Repayrate ?? 0
                    };

                    loans.Add(dto);
                    _logger.LogInformation($"Added loan to DTO: {dto.LoanNo}, Status={dto.Status}, Amount={dto.PrincipalAmount}");
                }

                _logger.LogInformation($"Returning {loans.Count} loans to view");

                // Log the actual data being returned
                foreach (var loan in loans)
                {
                    _logger.LogInformation($"Final loan data: {loan.LoanNo} - {loan.Status} - {loan.PrincipalAmount:C}");
                }

                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member loans");
                TempData["Error"] = "Unable to load your loans: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet("Details/{loanNo}")]
        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                _logger.LogInformation($"=== LOAN DETAILS CALLED ===");
                _logger.LogInformation($"LoanNo: {loanNo}");

                if (string.IsNullOrEmpty(loanNo))
                {
                    TempData["Error"] = "Invalid loan number.";
                    return RedirectToAction("MyLoans");
                }

                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                _logger.LogInformation($"MemberNo: {memberNo}, CompanyCode: {companyCode}");

                // Get the specific loan from the database directly
                var loanEntity = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loanEntity == null)
                {
                    _logger.LogWarning($"Loan {loanNo} not found for member {memberNo}");
                    TempData["Error"] = "Loan not found.";
                    return RedirectToAction("MyLoans");
                }

                _logger.LogInformation($"Loan found: Status={loanEntity.Status}, Posted={loanEntity.Posted}, Amount={loanEntity.LoanAmt}");

                // Get loan type details
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanEntity.LoanCode);

                // Get loan balance
                var loanBalance = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                // Get repayment schedule
                var schedule = await _selfServiceLoanService.GetLoanRepaymentScheduleAsync(loanNo, memberNo);

                // Get status string
                string statusString = GetStatusString(loanEntity.Status);
                _logger.LogInformation($"Status mapped to: {statusString}");

                // Calculate outstanding balance
                decimal outstandingBalance = 0;
                if (loanBalance != null)
                {
                    decimal balance = loanBalance.Balance;
                    decimal intrOwed = 0;
                    if (loanBalance.IntrOwed != null)
                    {
                        intrOwed = Convert.ToDecimal(loanBalance.IntrOwed);
                    }
                    outstandingBalance = balance + intrOwed;
                    _logger.LogInformation($"Outstanding balance: {outstandingBalance:C} (Balance={balance:C}, Interest={intrOwed:C})");
                }

                var viewModel = new LoanDetailsViewModel
                {
                    LoanNo = loanEntity.LoanNo,
                    LoanType = loanType?.LoanType1 ?? loanEntity.LoanCode ?? "Unknown",
                    PrincipalAmount = loanEntity.LoanAmt ?? 0,
                    OutstandingBalance = outstandingBalance,
                    MonthlyInstallment = loanEntity.Repayrate ?? 0,
                    NextPaymentDate = loanBalance?.Nextduedate,
                    Status = statusString,
                    ApplicationDate = loanEntity.ApplicDate,
                    DisbursementDate = loanEntity.AuditDateTime,
                    RepaymentSchedule = schedule ?? new List<LoanScheduleDTO>()
                };

                _logger.LogInformation($"Loan details loaded for {loanNo}: Status={statusString}, Outstanding={outstandingBalance:C}");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan details for {LoanNo}", loanNo);
                TempData["Error"] = "Unable to load loan details.";
                return RedirectToAction("MyLoans");
            }
        }

        #endregion


        #region Loan Repayment

        [HttpGet("Repay/{loanNo}")]
        public async Task<IActionResult> Repay(string loanNo)
        {
            try
            {
                _logger.LogInformation($"=== REPAY GET CALLED ===");
                _logger.LogInformation($"LoanNo: {loanNo}");

                if (string.IsNullOrEmpty(loanNo))
                {
                    TempData["Error"] = "No loan specified.";
                    return RedirectToAction("MyLoans");
                }

                var memberNo = GetCurrentMemberNo();
                var viewModel = await _selfServiceLoanService.GetRepaymentDetailsAsync(loanNo, memberNo);

                if (viewModel == null)
                {
                    TempData["Error"] = "Loan not found.";
                    return RedirectToAction("MyLoans");
                }

                if (viewModel.TotalOutstanding <= 0)
                {
                    TempData["Success"] = "This loan has already been fully paid.";
                    return RedirectToAction("Details", new { loanNo });
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repayment page");
                TempData["Error"] = "Unable to load repayment page.";
                return RedirectToAction("MyLoans");
            }
        }

        [HttpPost("Repay/{loanNo}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Repay(string loanNo, LoanRepaymentViewModel model)
        {
            _logger.LogInformation($"=== REPAY POST CALLED ===");
            _logger.LogInformation($"LoanNo: {loanNo}, Amount: {model.PaymentAmount}, Method: {model.PaymentMethod}");

            model.LoanNo = loanNo;

            // Validate based on payment method
            if (model.PaymentMethod == "MPESA" && string.IsNullOrWhiteSpace(model.MpesaPhoneNumber))
            {
                ModelState.AddModelError("MpesaPhoneNumber", "M-Pesa phone number is required");
                TempData["Error"] = "M-Pesa phone number is required";
                return RedirectToAction("Repay", new { loanNo });
            }

            if (model.PaymentMethod == "CHEQUE" && string.IsNullOrWhiteSpace(model.ChequeNumber))
            {
                ModelState.AddModelError("ChequeNumber", "Cheque number is required");
                TempData["Error"] = "Cheque number is required";
                return RedirectToAction("Repay", new { loanNo });
            }

            if (model.PaymentAmount <= 0)
            {
                TempData["Error"] = "Payment amount must be greater than 0";
                return RedirectToAction("Repay", new { loanNo });
            }

            try
            {
                var memberNo = GetCurrentMemberNo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var repayment = new LoanRepaymentDTO
                {
                    LoanNo = model.LoanNo,
                    Amount = model.PaymentAmount,
                    PaymentMethod = model.PaymentMethod,
                    MpesaPhoneNumber = FormatPhoneNumber(model.MpesaPhoneNumber),
                    ChequeNumber = model.ChequeNumber,
                    ReferenceNumber = model.ReferenceNumber,
                    Remarks = model.Remarks,
                    IpAddress = ipAddress
                };

                var result = await _selfServiceLoanService.MakeRepaymentAsync(repayment, memberNo);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("RepaymentConfirmation", new { loanNo = model.LoanNo, receiptNo = result.ReceiptNo });
                }
                else
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("Repay", new { loanNo = model.LoanNo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing repayment");
                TempData["Error"] = $"Unable to process repayment: {ex.Message}";
                return RedirectToAction("Repay", new { loanNo = model.LoanNo });
            }
        }

        [HttpGet("RepaymentConfirmation/{loanNo}/{receiptNo}")]
        public async Task<IActionResult> RepaymentConfirmation(string loanNo, string receiptNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var repayment = await _context.Repay
                    .FirstOrDefaultAsync(r => r.LoanNo == loanNo && r.ReceiptNo == receiptNo && r.MemberNo == memberNo);

                if (repayment == null)
                {
                    return RedirectToAction("MyLoans");
                }

                var loanBalance = await _context.Loanbal
                    .FirstOrDefaultAsync(lb => lb.LoanNo == loanNo);

                var viewModel = new RepaymentConfirmationViewModel
                {
                    LoanNo = loanNo,
                    AmountPaid = repayment.Amount ?? 0,
                    PrincipalPaid = repayment.Principal ?? 0,
                    InterestPaid = repayment.Interest ?? 0,
                    NewBalance = loanBalance?.Balance ?? 0,
                    PaymentDate = repayment.DateReceived ?? DateTime.Now,
                    PaymentMethod = GetPaymentMethodFromString(repayment.Chequeno),
                    ReceiptNo = receiptNo,
                    IsFullyPaid = (loanBalance?.Balance ?? 0) <= 0 && (loanBalance?.IntrOwed ?? 0) <= 0,
                    TransactionReference = repayment.TransactionNo,
                    BlockchainTxId = repayment.BlockchainTxId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repayment confirmation");
                return RedirectToAction("MyLoans");
            }
        }

        [HttpGet("RepaymentHistory/{loanNo}")]
        public async Task<IActionResult> RepaymentHistory(string loanNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var history = await _selfServiceLoanService.GetRepaymentHistoryAsync(loanNo, memberNo);
                ViewBag.LoanNo = loanNo;
                return View(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading repayment history");
                TempData["Error"] = "Unable to load repayment history.";
                return RedirectToAction("Details", new { loanNo });
            }
        }

        private string GetPaymentMethodFromString(string chequeNo)
        {
            if (string.IsNullOrEmpty(chequeNo)) return "CASH";
            if (chequeNo.StartsWith("MPESA")) return "MPESA";
            return "CHEQUE";
        }

        private List<PaymentMethodDTO> GetPaymentMethods()
        {
            return new List<PaymentMethodDTO>
            {
                new PaymentMethodDTO { Code = "MPESA", Name = "M-Pesa", Icon = "fa-mobile-alt", RequiresPhoneNumber = true, RequiresChequeNumber = false },
                new PaymentMethodDTO { Code = "CASH", Name = "Cash", Icon = "fa-money-bill-wave", RequiresPhoneNumber = false, RequiresChequeNumber = false },
                new PaymentMethodDTO { Code = "CHEQUE", Name = "Cheque", Icon = "fa-file-alt", RequiresPhoneNumber = false, RequiresChequeNumber = true },
                new PaymentMethodDTO { Code = "FOSA", Name = "FOSA Transfer", Icon = "fa-university", RequiresPhoneNumber = false, RequiresChequeNumber = false }
            };
        }

        #endregion


        #region Mobile Loan 

        [HttpGet("MobileLoan")]
        public async Task<IActionResult> MobileLoan()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                _logger.LogInformation($"Loading mobile loan page for member {memberNo}");

                var eligibility = await _selfServiceLoanService.CheckMobileLoanEligibilityAsync(memberNo, companyCode);

                if (!eligibility.IsEligible)
                {
                    _logger.LogWarning($"Member {memberNo} not eligible for mobile loan: {eligibility.Message}");
                    TempData["Error"] = eligibility.Message;
                    return RedirectToAction("Products");
                }

                // Get mobile loan product
                var allProducts = await _selfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode);
                var mobileLoanType = allProducts.FirstOrDefault(p => p.IsMobileLoan);

                if (mobileLoanType == null)
                {
                    _logger.LogWarning($"No mobile loan product found for company {companyCode}");
                    TempData["Error"] = "Mobile loan product not available.";
                    return RedirectToAction("Products");
                }

                // Get the actual Loantype from database to get RepayPeriod
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == mobileLoanType.LoanCode && lt.CompanyCode == companyCode);

                // Get member's total shares for self-guarantee display
                var totalShares = await _context.ContribShares
                    .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                    .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

                // FIXED: Use RepayPeriod from Loantype, not hardcoded 12
                int maxRepayPeriod = loanType?.RepayPeriod ?? 12;

                _logger.LogInformation($"Mobile loan max repay period: {maxRepayPeriod} months (from Loantype)");

                var viewModel = new MobileLoanViewModel
                {
                    LoanCode = mobileLoanType.LoanCode,
                    LoanName = mobileLoanType.LoanName,
                    EligibleAmount = Math.Min(eligibility.EligibleAmount, totalShares > 0 ? totalShares : eligibility.EligibleAmount),
                    MaxAmount = Math.Min(eligibility.MaxAmount, totalShares > 0 ? totalShares : eligibility.MaxAmount),
                    MinAmount = eligibility.MinAmount > 0 ? eligibility.MinAmount : 500,
                    InterestRate = eligibility.InterestRate,
                    MaxRepayPeriod = maxRepayPeriod,  // FIXED: Now from database
                    EstimatedMonthlyInstallment = eligibility.EstimatedMonthlyInstallment,
                    CurrentDeposits = eligibility.CurrentDeposits,
                    Multiplier = eligibility.Multiplier,
                    PrincipalAmount = Math.Min(eligibility.MinAmount > 0 ? eligibility.MinAmount : 500,
                        Math.Min(eligibility.EligibleAmount, totalShares > 0 ? totalShares : eligibility.EligibleAmount)),
                    RepayPeriod = Math.Min(6, maxRepayPeriod),  // Default to 6 or max if less
                    AvailableShares = totalShares,
                    IsSelfGuarantee = true
                };

                _logger.LogInformation($"Mobile loan page loaded: Eligible Amount={viewModel.EligibleAmount:C}, Shares={totalShares:C}, MaxRepayPeriod={maxRepayPeriod}");
                //ViewBag.MaxRepayPeriod = loanType?.RepayPeriod ?? 12;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mobile loan page");
                TempData["Error"] = "Unable to load mobile loan service.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost("MobileLoan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MobileLoan(MobileLoanViewModel model)
        {
            _logger.LogInformation("=== MOBILE LOAN POST RECEIVED ===");
            _logger.LogInformation($"LoanCode: {model.LoanCode}, Amount: {model.PrincipalAmount}, Period: {model.RepayPeriod}");

            // Additional validation for self-guarantee
            var memberNo = GetCurrentMemberNo();
            var companyCode = GetCurrentCompanyCode();

            // Check shares for self-guarantee
            var totalShares = await _context.ContribShares
                .Where(cs => cs.MemberNo == memberNo && cs.CompanyCode == companyCode)
                .SumAsync(cs => cs.ShareCapitalAmount ?? 0);

            if (model.PrincipalAmount > totalShares)
            {
                ModelState.AddModelError("PrincipalAmount", $"Loan amount cannot exceed your shares ({totalShares:C}) which serve as self-guarantee.");
                _logger.LogWarning($"Mobile loan amount {model.PrincipalAmount:C} exceeds shares {totalShares:C}");

                // Reload eligibility data
                var eligibility = await _selfServiceLoanService.CheckMobileLoanEligibilityAsync(memberNo, companyCode);
                model.EligibleAmount = Math.Min(eligibility.EligibleAmount, totalShares);
                model.MaxAmount = Math.Min(eligibility.MaxAmount, totalShares);
                model.AvailableShares = totalShares;

                // Reload max repay period from database
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == model.LoanCode && lt.CompanyCode == companyCode);
                model.MaxRepayPeriod = loanType?.RepayPeriod ?? 12;

                return View(model);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning($"ModelState invalid: {string.Join(", ", errors)}");

                // Reload max repay period from database
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == model.LoanCode && lt.CompanyCode == companyCode);
                model.MaxRepayPeriod = loanType?.RepayPeriod ?? 12;

                return View(model);
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var application = new SelfLoanApplicationDTO
                {
                    LoanCode = model.LoanCode,
                    PrincipalAmount = model.PrincipalAmount,
                    RepayPeriod = model.RepayPeriod,
                    Purpose = "Mobile Loan - " + (string.IsNullOrEmpty(model.Purpose) ? "Quick Access" : model.Purpose),
                    Remarks = "Mobile loan requested via self-service portal",
                    CompanyCode = companyCode,
                    IpAddress = ipAddress
                };

                _logger.LogInformation($"Calling ApplyForLoanAsync for mobile loan with amount {model.PrincipalAmount:C}");

                var result = await _selfServiceLoanService.ApplyForLoanAsync(application, memberNo);

                _logger.LogInformation($"Result: Success={result.Success}, LoanNo={result.LoanNo}, Message={result.Message}, CanWithdraw={result.CanWithdrawNow}");

                if (result.Success && result.CanWithdrawNow)
                {
                    TempData["Success"] = result.Message;
                    _logger.LogInformation($"Redirecting to withdraw for loan {result.LoanNo}");
                    return RedirectToAction("Withdraw", new { loanNo = result.LoanNo });
                }
                else if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("Status", new { loanNo = result.LoanNo });
                }
                else
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("MobileLoan");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mobile loan");
                TempData["Error"] = $"Unable to process mobile loan: {ex.Message}";
                return RedirectToAction("MobileLoan");
            }
        }

        #endregion

        private string GetStatusString(int? status)
        {
            return status switch
            {
                1 => "Draft",
                2 => "Submitted",
                3 => "UnderAppraisal",
                4 => "Approved",
                5 => "Endorsed",
                6 => "Disbursed",
                7 => "Closed",
                8 => "Defaulted",
                9 => "WrittenOff",
                10 => "Rejected",
                _ => "Unknown"
            };
        }
    }
}