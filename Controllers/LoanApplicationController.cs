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
        private readonly ISelfServiceLoanService _SelfServiceLoanService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanApplicationController> _logger;

        public LoanApplicationController(
            ISelfServiceLoanService SelfServiceLoanService,
            ApplicationDbContext context,
            ILogger<LoanApplicationController> logger)
        {
            _SelfServiceLoanService = SelfServiceLoanService;
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
                var dashboard = await _SelfServiceLoanService.GetMemberDashboardAsync(memberNo, companyCode);
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
                var products = await _SelfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode);
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
                var product = (await _SelfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode))
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

        // GET: Member/Loan/Apply/{loanCode}
        [HttpGet("Apply/{loanCode}")]
        public async Task<IActionResult> Apply(string loanCode)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                // Get loan type details first
                var loanType = await _context.Loantypes
                    .FirstOrDefaultAsync(lt => lt.LoanCode == loanCode && lt.CompanyCode == companyCode);

                if (loanType == null)
                {
                    TempData["Error"] = "Loan product not found.";
                    return RedirectToAction("Products");
                }

                // Check eligibility
                var eligibility = await _SelfServiceLoanService.CheckLoanEligibilityAsync(memberNo, loanCode, companyCode);

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
                    MinAmount = eligibility.MinAmount > 0 ? eligibility.MinAmount : 1000,
                    EligibleAmount = eligibility.EligibleAmount,
                    InterestRate = eligibility.InterestRate,
                    MaxRepayPeriod = eligibility.RepaymentPeriodMonths,
                    EstimatedMonthlyInstallment = eligibility.EstimatedMonthlyInstallment,
                    IsMobileLoan = eligibility.IsMobileLoan,
                    RequiresGuarantor = eligibility.RequiresGuarantor,
                    CompanyCode = companyCode,
                    PrincipalAmount = Math.Min(eligibility.MinAmount > 0 ? eligibility.MinAmount : 1000, eligibility.EligibleAmount),
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

        // POST: Member/Loan/ApplyLoan
        [HttpPost("ApplyLoan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLoan([FromForm] LoanApplicationViewModel model)
        {
            try
            {
                _logger.LogInformation("ApplyLoan POST called for LoanCode: {LoanCode}, Amount: {Amount}",
                    model.LoanCode, model.PrincipalAmount);

                // Get member number once at the beginning
                var memberNo = GetCurrentMemberNo();

                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Model state invalid: {Errors}", string.Join(", ", errors));

                    // Reload eligibility data for the view - use existing memberNo
                    var companyCode = GetCurrentCompanyCode();
                    var eligibility = await _SelfServiceLoanService.CheckLoanEligibilityAsync(memberNo, model.LoanCode, companyCode);

                    model.MaxAmount = eligibility.MaxAmount;
                    model.MinAmount = eligibility.MinAmount;
                    model.EligibleAmount = eligibility.EligibleAmount;
                    model.EstimatedMonthlyInstallment = CalculateMonthlyInstallment(model.PrincipalAmount, eligibility.InterestRate, model.RepayPeriod);
                    model.InterestRate = eligibility.InterestRate;
                    model.MaxRepayPeriod = eligibility.RepaymentPeriodMonths;
                    model.IsMobileLoan = eligibility.IsMobileLoan;
                    model.RequiresGuarantor = eligibility.RequiresGuarantor;

                    return View("Apply", model);
                }

                // Validate amount
                if (model.PrincipalAmount < model.MinAmount)
                {
                    ModelState.AddModelError("PrincipalAmount", $"Minimum loan amount is {model.MinAmount:C}");
                    return View("Apply", model);
                }

                if (model.PrincipalAmount > model.EligibleAmount)
                {
                    ModelState.AddModelError("PrincipalAmount", $"Maximum eligible amount is {model.EligibleAmount:C}");
                    return View("Apply", model);
                }

                // Validate repayment period
                if (model.RepayPeriod > model.MaxRepayPeriod)
                {
                    ModelState.AddModelError("RepayPeriod", $"Maximum repayment period is {model.MaxRepayPeriod} months");
                    return View("Apply", model);
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? HttpContext.Request.Headers["X-Forwarded-For"].ToString() ?? "Unknown";

                var application = new SelfLoanApplicationDTO
                {
                    LoanCode = model.LoanCode,
                    PrincipalAmount = model.PrincipalAmount,
                    RepayPeriod = model.RepayPeriod,
                    Purpose = model.Purpose ?? "General purpose",
                    Remarks = model.Remarks ?? "Applied via self-service portal",
                    CompanyCode = GetCurrentCompanyCode(),
                    IpAddress = ipAddress
                };

                var result = await _SelfServiceLoanService.ApplyForLoanAsync(application, memberNo);

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
                    // Display the actual error message to the user
                    TempData["Error"] = result.Message;
                    _logger.LogWarning("Loan application failed for member {MemberNo}: {Message}", memberNo, result.Message);

                    // Reload the Apply view with the model so user can try again
                    var companyCode = GetCurrentCompanyCode();
                    var eligibility = await _SelfServiceLoanService.CheckLoanEligibilityAsync(memberNo, model.LoanCode, companyCode);

                    model.MaxAmount = eligibility.MaxAmount;
                    model.MinAmount = eligibility.MinAmount;
                    model.EligibleAmount = eligibility.EligibleAmount;
                    model.InterestRate = eligibility.InterestRate;
                    model.MaxRepayPeriod = eligibility.RepaymentPeriodMonths;
                    model.IsMobileLoan = eligibility.IsMobileLoan;
                    model.RequiresGuarantor = eligibility.RequiresGuarantor;

                    return View("Apply", model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application for member {MemberNo}", GetCurrentMemberNo());
                TempData["Error"] = "Unable to submit application: " + ex.Message;

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
                var result = await _SelfServiceLoanService.GetLoanStatusAsync(loanNo, memberNo);

                if (!result.Success)
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("MyLoans");
                }

                var schedule = await _SelfServiceLoanService.GetLoanRepaymentScheduleAsync(loanNo, memberNo);

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

        [HttpGet("Withdraw/{loanNo}")]
        public async Task<IActionResult> Withdraw(string loanNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var result = await _SelfServiceLoanService.GetLoanStatusAsync(loanNo, memberNo);

                if (!result.Success || !result.CanWithdrawNow)
                {
                    TempData["Error"] = result.Message ?? "Loan is not ready for withdrawal.";
                    return RedirectToAction("MyLoans");
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                var viewModel = new LoanWithdrawalViewModel
                {
                    LoanNo = loanNo,
                    Amount = result.Amount ?? 0,
                    MemberPhone = member?.PhoneNo ?? member?.MobileNo ?? "",
                    WithdrawalMethods = GetAvailableWithdrawalMethods()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdrawal page for {LoanNo}", loanNo);
                TempData["Error"] = "Unable to process withdrawal request.";
                return RedirectToAction("MyLoans");
            }
        }

        [HttpPost("Withdraw")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(LoanWithdrawalViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.WithdrawalMethods = GetAvailableWithdrawalMethods();
                return View(model);
            }

            try
            {
                var memberNo = GetCurrentMemberNo();

                var result = await _SelfServiceLoanService.WithdrawLoanAsync(
                    model.LoanNo,
                    memberNo,
                    model.WithdrawalMethod,
                    model.MpesaPhoneNumber);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("DisbursementConfirmation", new { loanNo = model.LoanNo });
                }
                else
                {
                    TempData["Error"] = result.Message;
                    return RedirectToAction("Withdraw", new { loanNo = model.LoanNo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal for {LoanNo}", model.LoanNo);
                TempData["Error"] = "Unable to process withdrawal. Please try again.";
                return RedirectToAction("Withdraw", new { loanNo = model.LoanNo });
            }
        }

        [HttpGet("DisbursementConfirmation/{loanNo}")]
        public async Task<IActionResult> DisbursementConfirmation(string loanNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var loan = await _context.Loans
                    .FirstOrDefaultAsync(l => l.LoanNo == loanNo && l.MemberNo == memberNo);

                if (loan == null)
                {
                    return RedirectToAction("MyLoans");
                }

                var viewModel = new DisbursementConfirmationViewModel
                {
                    LoanNo = loanNo,
                    Amount = loan.LoanAmt ?? 0,
                    DisbursementDate = DateTime.Now
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading confirmation for {LoanNo}", loanNo);
                return RedirectToAction("MyLoans");
            }
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

        #endregion

        #region My Loans

        [HttpGet("MyLoans")]
        public async Task<IActionResult> MyLoans()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var loans = await _SelfServiceLoanService.GetMemberLoansAsync(memberNo, companyCode);
                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member loans");
                TempData["Error"] = "Unable to load your loans.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet("Details/{loanNo}")]
        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var loans = await _SelfServiceLoanService.GetMemberLoansAsync(memberNo, GetCurrentCompanyCode());
                var loan = loans.FirstOrDefault(l => l.LoanNo == loanNo);

                if (loan == null)
                {
                    TempData["Error"] = "Loan not found.";
                    return RedirectToAction("MyLoans");
                }

                var schedule = await _SelfServiceLoanService.GetLoanRepaymentScheduleAsync(loanNo, memberNo);

                var viewModel = new LoanDetailsViewModel
                {
                    LoanNo = loan.LoanNo,
                    LoanType = loan.LoanType,
                    PrincipalAmount = loan.PrincipalAmount,
                    OutstandingBalance = loan.OutstandingBalance,
                    MonthlyInstallment = loan.MonthlyInstallment,
                    NextPaymentDate = loan.NextPaymentDate,
                    Status = loan.Status,
                    ApplicationDate = loan.ApplicationDate,
                    DisbursementDate = loan.DisbursementDate,
                    RepaymentSchedule = schedule
                };

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

        #region Mobile Loan Quick Actions

        [HttpGet("MobileLoan")]
        public async Task<IActionResult> MobileLoan()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var eligibility = await _SelfServiceLoanService.CheckMobileLoanEligibilityAsync(memberNo, companyCode);

                if (!eligibility.IsEligible)
                {
                    TempData["Error"] = eligibility.Message;
                    return RedirectToAction("Products");
                }

                var mobileLoanType = (await _SelfServiceLoanService.GetAvailableLoanProductsAsync(memberNo, companyCode))
                    .FirstOrDefault(p => p.IsMobileLoan);

                if (mobileLoanType == null)
                {
                    TempData["Error"] = "Mobile loan product not available.";
                    return RedirectToAction("Products");
                }

                var viewModel = new MobileLoanViewModel
                {
                    LoanCode = mobileLoanType.LoanCode,
                    LoanName = mobileLoanType.LoanName,
                    EligibleAmount = eligibility.EligibleAmount,
                    MaxAmount = eligibility.MaxAmount,
                    MinAmount = eligibility.MinAmount,
                    InterestRate = eligibility.InterestRate,
                    MaxRepayPeriod = 12,
                    EstimatedMonthlyInstallment = eligibility.EstimatedMonthlyInstallment,
                    CurrentDeposits = eligibility.CurrentDeposits,
                    Multiplier = eligibility.Multiplier
                };

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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var memberNo = GetCurrentMemberNo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var application = new SelfLoanApplicationDTO
                {
                    LoanCode = model.LoanCode,
                    PrincipalAmount = model.PrincipalAmount,
                    RepayPeriod = model.RepayPeriod,
                    Purpose = "Mobile Loan - " + (model.Purpose ?? "Quick Access"),
                    Remarks = "Mobile loan requested via self-service portal",
                    CompanyCode = GetCurrentCompanyCode(),
                    IpAddress = ipAddress
                };

                var result = await _SelfServiceLoanService.ApplyForLoanAsync(application, memberNo);

                if (result.Success && result.CanWithdrawNow)
                {
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
                TempData["Error"] = "Unable to process mobile loan. Please try again.";
                return RedirectToAction("MobileLoan");
            }
        }

        #endregion
    }
}