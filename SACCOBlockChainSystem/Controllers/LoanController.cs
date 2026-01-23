using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize(Policy = "MemberOnly")]
    [Route("api/[controller]")]
    [ApiController]
    public class LoanController : ControllerBase
    {
        private readonly ILoanService _loanService;
        private readonly ILogger<LoanController> _logger;

        public LoanController(ILoanService loanService, ILogger<LoanController> logger)
        {
            _loanService = loanService;
            _logger = logger;
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyForLoan([FromBody] LoanApplicationDTO application)
        {
            try
            {
                var result = await _loanService.ApplyForLoanAsync(application);
                return Ok(new
                {
                    Success = true,
                    Message = "Loan application submitted successfully",
                    Data = result
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

        [HttpPost("approve/{loanNo}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ApproveLoan(string loanNo, [FromQuery] string approvedBy)
        {
            try
            {
                var success = await _loanService.ApproveLoanAsync(loanNo, approvedBy);
                return Ok(new
                {
                    Success = true,
                    Message = $"Loan {loanNo} approved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("repay")]
        public async Task<IActionResult> RepayLoan([FromBody] LoanRepaymentDTO repayment)
        {
            try
            {
                var result = await _loanService.ProcessRepaymentAsync(repayment);
                return Ok(new
                {
                    Success = true,
                    Message = "Loan repayment processed successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing loan repayment");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

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
                _logger.LogError(ex, "Error fetching loans");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error fetching loans"
                });
            }
        }

        [HttpGet("eligibility/{memberNo}")]
        public async Task<IActionResult> CheckEligibility(string memberNo)
        {
            try
            {
                var eligibility = await _loanService.CalculateLoanEligibilityAsync(memberNo);
                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        MemberNo = memberNo,
                        MaximumEligibleAmount = eligibility,
                        CheckDate = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking eligibility");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error checking eligibility"
                });
            }
        }
    }
}