using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
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

        public LoanController(ILoanService loanService, ILogger<LoanController> logger)
        {
            _loanService = loanService;
            _logger = logger;
        }

        // POST: api/loan/apply
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyForLoan([FromBody] LoanApplicationDTO application)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _loanService.ApplyForLoanAsync(application);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan application submitted successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying for loan");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // GET: api/loan/{loanNo}
        [HttpGet("{loanNo}")]
        public async Task<IActionResult> GetLoan(string loanNo)
        {
            try
            {
                var loan = await _loanService.GetLoanAsync(loanNo);

                return Ok(new
                {
                    Success = true,
                    Data = loan,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loan {loanNo}");
                return NotFound(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // GET: api/loan/member/{memberNo}
        [HttpGet("member/{memberNo}")]
        public async Task<IActionResult> GetMemberLoans(string memberNo)
        {
            try
            {
                var loans = await _loanService.GetLoansByMemberAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loans for member {memberNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // POST: api/loan/search
        [HttpPost("search")]
        public async Task<IActionResult> SearchLoans([FromBody] LoanSearchDTO search)
        {
            try
            {
                var loans = await _loanService.SearchLoansAsync(search);

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching loans");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // PUT: api/loan/{loanNo}/status
        [HttpPut("{loanNo}/status")]
        public async Task<IActionResult> UpdateLoanStatus(string loanNo, [FromBody] LoanUpdateDTO update)
        {
            try
            {
                var result = await _loanService.UpdateLoanStatusAsync(loanNo, update);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan status updated successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating loan status for {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // DELETE: api/loan/{loanNo}
        [HttpDelete("{loanNo}")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> DeleteLoan(string loanNo, [FromQuery] string deletedBy)
        {
            try
            {
                var success = await _loanService.DeleteLoanAsync(loanNo, deletedBy);

                if (!success)
                    return NotFound(new { Success = false, Message = "Loan not found" });

                return Ok(new
                {
                    Success = true,
                    Message = "Loan deleted successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // Workflow Endpoints
        [HttpPost("{loanNo}/submit-guarantors")]
        public async Task<IActionResult> SubmitForGuarantors(string loanNo, [FromQuery] string submittedBy)
        {
            try
            {
                var result = await _loanService.SubmitForGuarantorsAsync(loanNo, submittedBy);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan submitted for guarantor approval",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan {loanNo} for guarantors");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/submit-appraisal")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> SubmitForAppraisal(string loanNo, [FromQuery] string submittedBy)
        {
            try
            {
                var result = await _loanService.SubmitForAppraisalAsync(loanNo, submittedBy);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan submitted for appraisal",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan {loanNo} for appraisal");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/submit-endorsement")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> SubmitForEndorsement(string loanNo, [FromQuery] string submittedBy)
        {
            try
            {
                var result = await _loanService.SubmitForEndorsementAsync(loanNo, submittedBy);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan submitted for endorsement",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error submitting loan {loanNo} for endorsement");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/approve")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> ApproveLoan(string loanNo, [FromBody] LoanUpdateDTO approval)
        {
            try
            {
                var result = await _loanService.ApproveLoanAsync(loanNo, approval);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan approved successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/reject")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> RejectLoan(string loanNo, [FromBody] LoanUpdateDTO rejection)
        {
            try
            {
                var result = await _loanService.RejectLoanAsync(loanNo, rejection);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan rejected",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/disburse")]
        [Authorize(Roles = "Admin,Teller")]
        public async Task<IActionResult> DisburseLoan(string loanNo, [FromBody] LoanDisbursementDTO disbursement)
        {
            try
            {
                var result = await _loanService.DisburseLoanAsync(loanNo, disbursement);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan disbursed successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disbursing loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost("{loanNo}/close")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> CloseLoan(string loanNo, [FromQuery] string closedBy)
        {
            try
            {
                var result = await _loanService.CloseLoanAsync(loanNo, closedBy);

                return Ok(new
                {
                    Success = true,
                    Message = "Loan closed successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // Guarantor Management
        [HttpPost("{loanNo}/guarantors")]
        public async Task<IActionResult> AddGuarantor(string loanNo, [FromBody] LoanGuarantorDTO guarantor)
        {
            try
            {
                var success = await _loanService.AddGuarantorAsync(loanNo, guarantor);

                return Ok(new
                {
                    Success = success,
                    Message = "Guarantor added successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding guarantor to loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpDelete("{loanNo}/guarantors/{guarantorMemberNo}")]
        public async Task<IActionResult> RemoveGuarantor(string loanNo, string guarantorMemberNo)
        {
            try
            {
                var success = await _loanService.RemoveGuarantorAsync(loanNo, guarantorMemberNo);

                return Ok(new
                {
                    Success = success,
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
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpGet("{loanNo}/guarantors")]
        public async Task<IActionResult> GetGuarantors(string loanNo)
        {
            try
            {
                var guarantors = await _loanService.GetLoanGuarantorsAsync(loanNo);

                return Ok(new
                {
                    Success = true,
                    Data = guarantors,
                    Count = guarantors.Count,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting guarantors for loan {loanNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        // Reports
        [HttpGet("member/{memberNo}/eligibility")]
        public async Task<IActionResult> GetMemberEligibility(string memberNo)
        {
            try
            {
                var eligibility = await _loanService.GetMemberLoanEligibilityAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = new { MemberNo = memberNo, EligibilityAmount = eligibility },
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting eligibility for member {memberNo}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin,LoanOfficer")]
        public async Task<IActionResult> GetPendingLoans()
        {
            try
            {
                var loans = await _loanService.GetPendingLoansAsync();

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending loans");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetLoansByStatus(int status)
        {
            try
            {
                var loans = await _loanService.GetLoansByStatusAsync(status);

                return Ok(new
                {
                    Success = true,
                    Data = loans,
                    Count = loans.Count,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting loans by status {status}");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }
    }
}