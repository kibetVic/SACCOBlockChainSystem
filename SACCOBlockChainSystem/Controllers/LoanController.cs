using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LoanController : ControllerBase
    {
        private readonly ILoanService _loanService;
        private readonly ILogger<LoanController> _logger;
        private readonly ICompanyContextService _companyContextService;

        public LoanController(
            ILoanService loanService,
            ILogger<LoanController> logger,
            ICompanyContextService companyContextService)
        {
            _loanService = loanService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        // POST: /api/Loan/apply
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyForLoan([FromBody] LoanApplicationDTO application)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Set created by from current user
                application.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _loanService.ApplyForLoanAsync(application);

                return Ok(new
                {
                    Success = true,
                    Message = result.Message,
                    Data = new
                    {
                        result.LoanNo,
                        result.EligibleAmount,
                        result.BlockchainTxId
                    },
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying for loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/eligibility/{memberNo}/{loanCode}/{amount}
        [HttpGet("eligibility/{memberNo}/{loanCode}/{amount}")]
        public async Task<IActionResult> CheckEligibility(string memberNo, string loanCode, decimal amount)
        {
            try
            {
                var eligibility = await _loanService.CheckLoanEligibilityAsync(memberNo, loanCode, amount);

                return Ok(new
                {
                    Success = true,
                    Data = eligibility
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking eligibility for member {memberNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: /api/Loan/guarantor
        [HttpPost("guarantor")]
        public async Task<IActionResult> AddGuarantor([FromBody] LoanGuarantorDTO guarantor)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                guarantor.ActionBy = User.Identity?.Name ?? "SYSTEM";

                var success = await _loanService.AddGuarantorAsync(guarantor);

                return Ok(new
                {
                    Success = true,
                    Message = "Guarantor added successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding guarantor");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: /api/Loan/appraise
        [HttpPost("appraise")]
        [Authorize(Policy = "LoanOfficer")]
        public async Task<IActionResult> AppraiseLoan([FromBody] LoanAppraisalDTO appraisal)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var success = await _loanService.AppraiseLoanAsync(appraisal);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan appraised successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appraising loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: /api/Loan/endorse
        [HttpPost("endorse")]
        [Authorize(Policy = "LoanCommittee")]
        public async Task<IActionResult> EndorseLoan([FromBody] LoanEndorsementDTO endorsement)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var success = await _loanService.EndorseLoanAsync(endorsement);

                return Ok(new
                {
                    Success = true,
                    Message = $"Loan {endorsement.Decision.ToLower()} successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error endorsing loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: /api/Loan/disburse
        [HttpPost("disburse")]
        [Authorize(Policy = "Treasurer")]
        public async Task<IActionResult> DisburseLoan([FromBody] LoanDisbursementDTO disbursement)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                disbursement.ProcessedBy = User.Identity?.Name ?? "SYSTEM";

                var success = await _loanService.DisburseLoanAsync(disbursement);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan disbursed successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disbursing loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: /api/Loan/repay
        [HttpPost("repay")]
        public async Task<IActionResult> MakeRepayment([FromBody] LoanRepaymentDTO repayment)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                repayment.ProcessedBy = User.Identity?.Name ?? "SYSTEM";

                var success = await _loanService.MakeRepaymentAsync(repayment);

                return Ok(new
                {
                    Success = true,
                    Message = "Repayment processed successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing repayment");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/{loanNo}
        [HttpGet("{loanNo}")]
        public async Task<IActionResult> GetLoanDetails(string loanNo)
        {
            try
            {
                var loanDetails = await _loanService.GetLoanDetailsAsync(loanNo);

                return Ok(new
                {
                    Success = true,
                    Data = loanDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan details for {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/member/{memberNo}
        [HttpGet("member/{memberNo}")]
        public async Task<IActionResult> GetMemberLoans(string memberNo)
        {
            try
            {
                var loans = await _loanService.GetMemberLoansAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loans for member {memberNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/search
        [HttpGet("search")]
        [Authorize(Policy = "LoanOfficer")]
        public async Task<IActionResult> SearchLoans([FromQuery] string? memberNo = null,
                                                   [FromQuery] string? loanNo = null,
                                                   [FromQuery] string? loanCode = null,
                                                   [FromQuery] int? status = null,
                                                   [FromQuery] DateTime? fromDate = null,
                                                   [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var searchCriteria = new LoanSearchDTO
                {
                    MemberNo = memberNo,
                    LoanNo = loanNo,
                    LoanCode = loanCode,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var loans = await _loanService.SearchLoansAsync(searchCriteria);

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/portfolio
        [HttpGet("portfolio")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetLoanPortfolio()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var portfolio = await _loanService.GetLoanPortfolioReportAsync(companyCode);

                return Ok(new
                {
                    Success = true,
                    Data = portfolio
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting loan portfolio");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: /api/Loan/{loanNo}/guarantors
        [HttpGet("{loanNo}/guarantors")]
        public async Task<IActionResult> GetLoanGuarantors(string loanNo)
        {
            try
            {
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                return Ok(new
                {
                    Success = true,
                    Data = guarantors,
                    Count = guarantors.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting guarantors for loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // DELETE: /api/Loan/{loanNo}/guarantor/{guarantorMemberNo}
        [HttpDelete("{loanNo}/guarantor/{guarantorMemberNo}")]
        public async Task<IActionResult> RemoveGuarantor(string loanNo, string guarantorMemberNo)
        {
            try
            {
                var success = await _loanService.RemoveGuarantorAsync(loanNo, guarantorMemberNo);

                return Ok(new
                {
                    Success = true,
                    Message = "Guarantor removed successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing guarantor from loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}