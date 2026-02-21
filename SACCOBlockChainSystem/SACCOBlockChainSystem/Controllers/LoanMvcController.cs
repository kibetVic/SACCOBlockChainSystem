using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LoanMvcController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly ILoanTypeService _loanTypeService;
        private readonly IMemberService _memberService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<LoanMvcController> _logger;

        public LoanMvcController(
            ILoanService loanService,
            ILoanTypeService loanTypeService,
            IMemberService memberService,
            ICompanyContextService companyContextService,
            ILogger<LoanMvcController> logger)
        {
            _loanService = loanService;
            _loanTypeService = loanTypeService;
            _memberService = memberService;
            _companyContextService = companyContextService;
            _logger = logger;
        }

        private bool IsAdminUser()
        {
            return User.IsInRole("Admin") ||
                   User.HasClaim(c => c.Type == "UserGroup" && c.Value == "Admin");
        }

        #region Dashboard

        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var dashboard = await _loanService.GetLoanDashboardAsync(companyCode);

                ViewBag.CompanyCode = companyCode;
                ViewBag.UserName = User.Identity?.Name;


                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan dashboard");
                return View("Error");
            }
        }

        #endregion

        #region Loan Application

        public async Task<IActionResult> Apply()
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var loanTypes = await _loanTypeService.GetLoanTypesByCompanyAsync(companyCode);

                ViewBag.LoanTypes = loanTypes;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loan application form");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(LoanApplicationDTO application)
        {
            try
            {
                application.CompanyCode = GetUserCompanyCode();
                application.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var loan = await _loanService.ApplyForLoanAsync(application);

                ViewBag.SuccessMessage = $"Loan application {loan.LoanNo} submitted successfully!";
                return RedirectToAction("Details", new { loanNo = loan.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan application");

                var loanTypes = await _loanTypeService.GetLoanTypesByCompanyAsync(GetUserCompanyCode());
                ViewBag.LoanTypes = loanTypes;
                return View(application);
            }
        }

        public async Task<IActionResult> Details(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Fetch loan type separately (no relationship needed)
                var loanType = await _loanTypeService.GetLoanTypeByCodeAsync(loan.LoanCode, loan.CompanyCode);

                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);
                var appraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                var approvals = await _loanService.GetLoanApprovalsAsync(loanNo);
                var disbursement = await _loanService.GetLoanDisbursementAsync(loanNo);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);
                var auditTrail = await _loanService.GetLoanAuditTrailAsync(loanNo);

                ViewBag.Guarantors = guarantors;
                ViewBag.Appraisal = appraisal;
                ViewBag.Approvals = approvals;
                ViewBag.Disbursement = disbursement;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;
                ViewBag.AuditTrail = auditTrail;
                ViewBag.LoanTypeName = loanType?.LoanType ?? "Unknown";
                ViewBag.CanEdit = loan.LoanStatus == "Draft" || loan.LoanStatus == "Submitted";
                ViewBag.CanAppraise = loan.LoanStatus == "Submitted" && loan.AppraisalCompleted == false;
                ViewBag.CanApprove = loan.LoanStatus == "Submitted" && loan.AppraisalCompleted == true;
                ViewBag.CanDisburse = loan.LoanStatus == "Approved";
                ViewBag.CanRepay = loan.LoanStatus == "Disbursed" || loan.LoanStatus == "Active";


                return View(loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loan details for {loanNo}");
                return View("Error");
            }
        }

        #endregion

        #region Guarantor Management

        [HttpGet]
        public async Task<IActionResult> AssignGuarantor(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.LoanStatus != "Draft" && loan.LoanStatus != "Submitted")
                {
                    ViewBag.ErrorMessage = "Cannot assign guarantors at this stage";
                    return RedirectToAction("Details", new { loanNo });
                }

                ViewBag.LoanNo = loanNo;
                ViewBag.LoanAmount = loan.PrincipalAmount;
                ViewBag.RequiredGuarantors = loan.RequiredGuarantors;
                ViewBag.AssignedGuarantors = loan.AssignedGuarantors;


                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading guarantor assignment for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignGuarantor(string loanNo, GuarantorAssignmentDTO guarantor)
        {
            try
            {
                guarantor.CompanyCode = GetUserCompanyCode();

                var result = await _loanService.AssignGuarantorAsync(
                    loanNo,
                    guarantor,
                    User.Identity?.Name ?? "SYSTEM");

                ViewBag.SuccessMessage = $"Guarantor {guarantor.GuarantorMemberNo} assigned successfully";
                return RedirectToAction("Details", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning guarantor for {loanNo}");

                ViewBag.LoanNo = loanNo;

                return View(guarantor);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGuarantor(int guarantorId)
        {
            try
            {
                await _loanService.ApproveGuarantorAsync(guarantorId, User.Identity?.Name ?? "SYSTEM");

                ViewBag.SuccessMessage = "Guarantor approved successfully";
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving guarantor");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGuarantor(int guarantorId, string remarks)
        {
            try
            {
                await _loanService.RejectGuarantorAsync(guarantorId, remarks, User.Identity?.Name ?? "SYSTEM");

                ViewBag.SuccessMessage = "Guarantor rejected";
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting guarantor");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseGuarantor(int guarantorId)
        {
            try
            {
                await _loanService.ReleaseGuarantorAsync(guarantorId, User.Identity?.Name ?? "SYSTEM");

                ViewBag.SuccessMessage = "Guarantor released successfully";
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing guarantor");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
        }

        #endregion

        #region Loan Appraisal


        #region All Loans View

        [HttpGet]
        public async Task<IActionResult> AllLoans(int page = 1, int pageSize = 10)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var searchDto = new LoanSearchDTO
                {
                    CompanyCode = companyCode
                };

                var allLoans = await _loanService.SearchLoansAsync(searchDto);

                // Load loan types for filter dropdown
                ViewBag.LoanTypes = await _loanTypeService.GetLoanTypesByCompanyAsync(companyCode);

                // Calculate pagination
                var totalItems = allLoans.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var loans = allLoans
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = totalItems;

                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all loans");
                ViewBag.ErrorMessage = "Error loading loans";
                return View(new List<LoanSummaryDTO>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportAllLoans(LoanSearchDTO searchDto)
        {
            try
            {
                searchDto.CompanyCode = GetUserCompanyCode();
                var loans = await _loanService.SearchLoansAsync(searchDto);

                // Build CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Loan No,Member No,Member Name,Loan Type,Principal Amount,Approved Amount,Disbursed Amount,Outstanding Balance,Application Date,Status");

                foreach (var loan in loans)
                {
                    csv.AppendLine($"\"{loan.LoanNo}\",\"{loan.MemberNo}\",\"{loan.MemberName}\",\"{loan.LoanType}\",{loan.PrincipalAmount},{loan.ApprovedAmount},{loan.DisbursedAmount},{loan.OutstandingBalance},\"{loan.ApplicationDate:dd/MM/yyyy}\",\"{loan.LoanStatus}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"AllLoans_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting all loans");
                ViewBag.ErrorMessage = "Error exporting data";
                return RedirectToAction("AllLoans");
            }
        }

        #endregion


        [HttpGet]
        public async Task<IActionResult> Appraise(string loanNo)
        {
            try
            {
                var existingAppraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                if (existingAppraisal != null)
                {
                    ViewBag.ErrorMessage = "This loan has already been appraised.";
                    return RedirectToAction("Details", new { loanNo = loanNo });
                }

                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.LoanStatus != "Submitted")
                {
                    ViewBag.ErrorMessage = "Loan is not ready for appraisal";
                    return RedirectToAction("Details", new { loanNo });
                }

                if (loan.AppraisalCompleted)
                {
                    ViewBag.ErrorMessage = "Loan has already been appraised";
                    return RedirectToAction("Details", new { loanNo });
                }

                var member = await _memberService.GetMemberByMemberNoAsync(loan.MemberNo);
                var maxLoanAmount = await _loanService.CalculateMaximumLoanAmountAsync(
                    loan.MemberNo, loan.LoanCode, companyCode);

                ViewBag.Loan = loan;
                ViewBag.Member = member;
                ViewBag.MaxLoanAmount = maxLoanAmount;
                ViewBag.CompanyCode = companyCode;


                var appraisalDto = new LoanAppraisalDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    AppraisedBy = User.Identity?.Name ?? "SYSTEM",
                    MemberSharesValue = member?.ShareCap ?? 0,
                    MonthlyIncome = (decimal?)(member?.MonthlyContr ?? 0) ?? 0
                };

                return View(appraisalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading appraisal form for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Appraise(LoanAppraisalDTO appraisalDto)
        {
            try
            {
                appraisalDto.CompanyCode = GetUserCompanyCode();
                appraisalDto.AppraisedBy = User.Identity?.Name ?? "SYSTEM";

                var appraisal = await _loanService.AppraiseLoanAsync(appraisalDto);

                ViewBag.SuccessMessage = $"Loan appraisal completed with decision: {appraisalDto.AppraisalDecision}";
                return RedirectToAction("Details", new { loanNo = appraisalDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan appraisal");

                return View(appraisalDto);
            }
        }

        #endregion

        #region Loan Approval

        [HttpGet]
        public async Task<IActionResult> Approve(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.LoanStatus != "Submitted" && loan.LoanStatus != "PendingFinalApproval")
                {
                    ViewBag.ErrorMessage = "Loan is not ready for approval";
                    return RedirectToAction("Details", new { loanNo });
                }

                if (!loan.AppraisalCompleted)
                {
                    ViewBag.ErrorMessage = "Loan must be appraised before approval";
                    return RedirectToAction("Details", new { loanNo });
                }

                var appraisal = await _loanService.GetLoanAppraisalAsync(loanNo);
                var approvals = await _loanService.GetLoanApprovalsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Appraisal = appraisal;
                ViewBag.PreviousApprovals = approvals;
                ViewBag.ApprovalLevel = approvals.Count + 1;


                var approvalDto = new LoanApprovalDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    ApprovedBy = User.Identity?.Name ?? "SYSTEM",
                    ApprovalLevel = approvals.Count + 1,
                    IsFinalApproval = (approvals.Count + 1) >= GetRequiredApprovalLevels()
                };

                return View(approvalDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading approval form for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(LoanApprovalDTO approvalDto)
        {
            try
            {
                approvalDto.CompanyCode = GetUserCompanyCode();
                approvalDto.ApprovedBy = User.Identity?.Name ?? "SYSTEM";

                var approval = await _loanService.ApproveLoanAsync(approvalDto);

                ViewBag.SuccessMessage = $"Loan {approvalDto.ApprovalStatus} successfully";
                return RedirectToAction("Details", new { loanNo = approvalDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting loan approval");

                return View(approvalDto);
            }
        }

        #endregion

        #region Loan Disbursement

        [HttpGet]
        public async Task<IActionResult> Disburse(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.LoanStatus != "Approved")
                {
                    ViewBag.ErrorMessage = "Loan is not approved for disbursement";
                    return RedirectToAction("Details", new { loanNo });
                }

                ViewBag.Loan = loan;


                var disbursementDto = new LoanDisbursementDTO
                {
                    LoanNo = loanNo,
                    CompanyCode = companyCode,
                    DisbursementDate = DateTime.Now,
                    DisbursedBy = User.Identity?.Name ?? "SYSTEM",
                    AuthorizedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(disbursementDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading disbursement form for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disburse(LoanDisbursementDTO disbursementDto)
        {
            try
            {
                disbursementDto.CompanyCode = GetUserCompanyCode();
                disbursementDto.DisbursedBy = User.Identity?.Name ?? "SYSTEM";
                disbursementDto.AuthorizedBy = User.Identity?.Name ?? "SYSTEM";

                var disbursement = await _loanService.DisburseLoanAsync(disbursementDto);

                ViewBag.SuccessMessage = $"Loan disbursed successfully. Disbursement No: {disbursement.DisbursementNo}";
                return RedirectToAction("Details", new { loanNo = disbursementDto.LoanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disbursing loan");

                return View(disbursementDto);
            }
        }

        #endregion

        #region Loan Repayments

        [HttpGet]
        public async Task<IActionResult> Repay(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                if (loan.LoanStatus != "Disbursed" && loan.LoanStatus != "Active")
                {
                    ViewBag.ErrorMessage = "Loan is not active for repayments";
                    return RedirectToAction("Details", new { loanNo });
                }

                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var totalOutstanding = schedule.Where(s => s.Status != "Paid").Sum(s => s.OutstandingAmount);
                var nextInstallment = schedule.FirstOrDefault(s => s.Status == "Pending" || s.Status == "Overdue");

                ViewBag.Loan = loan;
                ViewBag.Schedule = schedule;
                ViewBag.TotalOutstanding = totalOutstanding;
                ViewBag.NextInstallment = nextInstallment;


                var repaymentDto = new LoanRepaymentDTO
                {
                    LoanNo = loanNo,
                    MemberNo = loan.MemberNo,
                    CompanyCode = companyCode,
                    PaymentDate = DateTime.Now,
                    ReceivedBy = User.Identity?.Name ?? "SYSTEM"
                };

                return View(repaymentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading repayment form for {loanNo}");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Repay(LoanRepaymentDTO repaymentDto)
        {
            try
            {
                repaymentDto.CompanyCode = GetUserCompanyCode();
                repaymentDto.ReceivedBy = User.Identity?.Name ?? "SYSTEM";

                var repayment = await _loanService.ProcessRepaymentAsync(repaymentDto);

                ViewBag.SuccessMessage = $"Repayment of {repaymentDto.AmountPaid:C} processed successfully. Receipt: {repayment.ReceiptNo}";
                return RedirectToAction("Details", new { loanNo = repaymentDto.LoanNo });
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error processing repayment";
                _logger.LogError(ex, "Error processing repayment");

                return View(repaymentDto);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverseRepayment(int repaymentId, string reason)
        {
            try
            {
                var repayment = await _loanService.ReverseRepaymentAsync(
                    repaymentId,
                    reason,
                    User.Identity?.Name ?? "SYSTEM");

                ViewBag.SuccessMessage = $"Repayment {repayment.ReceiptNo} reversed successfully";
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reversing repayment");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo = Request.Form["loanNo"].ToString() });
            }
        }

        #endregion

        #region Loan Status Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLoanStatus(string loanNo, string newStatus)
        {
            try
            {

                var companyCode = GetUserCompanyCode();

                // Verify the loan exists and user has access
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    newStatus,
                    User.Identity?.Name ?? "SYSTEM",
                    $"Status updated to {newStatus}");

                ViewBag.SuccessMessage = $"Loan status updated to {newStatus} successfully";
                return RedirectToAction("Details", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan status for {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLoan(string loanNo, string rejectionReason)
        {
            try
            {

                var companyCode = GetUserCompanyCode();

                // Verify the loan exists and user has access
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status to Rejected with reason
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    "Rejected",
                    User.Identity?.Name ?? "SYSTEM",
                    $"Loan rejected: {rejectionReason}");

                ViewBag.SuccessMessage = "Loan rejected successfully";
                return RedirectToAction("Details", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting loan {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteOffLoan(string loanNo, string writeOffReason)
        {
            try
            {

                var companyCode = GetUserCompanyCode();
                var isAdmin = IsAdminUser();

                // Only admins can write off loans
                if (!isAdmin)
                {
                    ViewBag.ErrorMessage = "You don't have permission to write off loans";
                    return RedirectToAction("Details", new { loanNo });
                }

                // Verify the loan exists
                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);

                // Update the status to WrittenOff
                await _loanService.UpdateLoanStatusAsync(
                    loanNo,
                    "WrittenOff",
                    User.Identity?.Name ?? "SYSTEM",
                    $"Loan written off: {writeOffReason}");

                ViewBag.SuccessMessage = "Loan written off successfully";
                return RedirectToAction("Details", new { loanNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing off loan {loanNo}");
                ViewBag.ErrorMessage = ex.Message;
                return RedirectToAction("Details", new { loanNo });
            }
        }

        #endregion

        #region Loan Search

        [HttpGet]
        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchResults(LoanSearchDTO searchDto)
        {
            try
            {
                searchDto.CompanyCode = GetUserCompanyCode();

                var results = await _loanService.SearchLoansAsync(searchDto);

                ViewBag.SearchCriteria = searchDto;
                ViewBag.ResultCount = results.Count;


                return View(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return View("Error");
            }
        }

        #endregion

        #region Member Loans

        [HttpGet]
        public async Task<IActionResult> MemberLoans(string memberNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                var loans = await _loanService.GetMemberLoansAsync(memberNo, companyCode);

                ViewBag.Member = member;
                ViewBag.LoanCount = loans.Count;
                ViewBag.TotalLoanAmount = loans.Sum(l => l.PrincipalAmount);
                ViewBag.TotalOutstanding = loans.Sum(l => l.OutstandingBalance);


                return View(loans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading loans for member {memberNo}");
                return View("Error");
            }
        }

        #endregion

        #region Loan Schedule

        [HttpGet]
        public async Task<IActionResult> Schedule(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.TotalPrincipal = schedule.Sum(s => s.PrincipalAmount);
                ViewBag.TotalInterest = schedule.Sum(s => s.InterestAmount);
                ViewBag.TotalRepayable = schedule.Sum(s => s.TotalInstallment);
                ViewBag.TotalPaid = schedule.Sum(s => s.PaidAmount);
                ViewBag.TotalOutstanding = schedule.Sum(s => s.OutstandingAmount);


                return View(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading schedule for {loanNo}");
                return View("Error");
            }
        }

        #endregion

        #region Reports

        [HttpGet]
        public async Task<IActionResult> Statement(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;


                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating loan statement for {loanNo}");
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintStatement(string loanNo)
        {
            try
            {
                var companyCode = GetUserCompanyCode();

                var loan = await _loanService.GetLoanByNoAsync(loanNo, companyCode);
                var schedule = await _loanService.GetLoanScheduleAsync(loanNo);
                var repayments = await _loanService.GetLoanRepaymentsAsync(loanNo);

                ViewBag.Loan = loan;
                ViewBag.Schedule = schedule;
                ViewBag.Repayments = repayments;

                ViewBag.PrintMode = true;

                return View("Statement", loan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing loan statement for {loanNo}");
                return View("Error");
            }
        }

        #endregion

        #region Helper Methods

        private string GetUserCompanyCode()
        {
            var companyCode = _companyContextService.GetCurrentCompanyCode();
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }

        private int GetRequiredApprovalLevels()
        {
            // This could be configured in system settings
            return 2; // Two-level approval: Loan Officer -> Manager
        }

        #endregion


        [HttpGet]
        public async Task<IActionResult> GetWorkflowCounts(string companyCode)
        {
            try
            {
                if (string.IsNullOrEmpty(companyCode))
                {
                    companyCode = GetUserCompanyCode();
                }

                var dashboard = await _loanService.GetLoanDashboardAsync(companyCode);

                var counts = new
                {
                    success = true,
                    data = new
                    {
                        underAppraisal = dashboard.UnderAppraisal,
                        pendingApproval = dashboard.PendingApproval,
                        pendingFinalApproval = dashboard.PendingFinalApproval,
                        approvedPendingDisbursement = dashboard.ApprovedPendingDisbursement,
                        pendingApplications = dashboard.PendingApplications,
                        activeLoans = dashboard.ActiveLoans,
                        overdueLoans = dashboard.OverdueLoans
                    }
                };

                return Json(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow counts");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}