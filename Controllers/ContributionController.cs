// Controllers/ContributionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ContributionController : ControllerBase
    {
        private readonly IContributionService _contributionService;
        private readonly IMemberService _memberService;
        private readonly ILogger<ContributionController> _logger;

        public ContributionController(IContributionService contributionService, IMemberService memberService, ILogger<ContributionController> logger)
        {
            _contributionService = contributionService;
            _memberService = memberService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> AddContribution([FromBody] ContributionDTO contributionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Get company code from user claims (assuming it's stored there)
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Company code not found in user claims" });
                }

                contributionDto.CompanyCode = companyCode;
                contributionDto.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                var result = await _contributionService.AddContributionAsync(contributionDto);

                return Ok(new
                {
                    Success = true,
                    Message = "Contribution added successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contribution");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while adding contribution",
                    Error = ex.Message
                });
            }
        }



        [HttpPost("BulkAdd")]
        public async Task<IActionResult> BulkAddContributions([FromBody] BulkContributionRequestDTO request)
        {
            try
            {
                _logger.LogInformation($"Bulk add contributions for member: {request.MemberNo}, Count: {request.Contributions.Count}");

                if (!ModelState.IsValid)
                {
                    return BadRequest(new BulkContributionResponseDTO
                    {
                        Success = false,
                        Message = "Invalid request data",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                // Get company code from claims or use request
                var companyCode = User.FindFirst("CompanyCode")?.Value ?? request.CompanyCode;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new BulkContributionResponseDTO
                    {
                        Success = false,
                        Message = "Company code not found"
                    });
                }

                request.CompanyCode = companyCode;
                request.CreatedBy = User.Identity?.Name ?? "SYSTEM";

                // Process all contributions in a transaction
                var result = await _contributionService.BulkAddContributionsAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk contribution");
                return StatusCode(500, new BulkContributionResponseDTO
                {
                    Success = false,
                    Message = "An error occurred while processing contributions",
                    Errors = new List<string> { ex.Message }
                });
            }
        }


        [HttpGet("member/{memberNo}/sharetype-totals")]
        public async Task<IActionResult> GetMemberShareTypeTotals(string memberNo)
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Company code not found" });
                }

                var totals = await _contributionService.GetMemberShareTypeTotalsAsync(memberNo, companyCode);

                return Ok(new
                {
                    Success = true,
                    Data = totals,
                    Message = "Share type totals retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member share type totals");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching share type totals",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("member/{memberNo}/sharetype/{shareTypeCode}/total")]
        public async Task<IActionResult> GetMemberShareTypeTotal(string memberNo, string shareTypeCode)
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Company code not found" });
                }

                var total = await _memberService.GetMemberShareTypeTotalAsync(memberNo, shareTypeCode, companyCode);

                return Ok(new
                {
                    Success = true,
                    CurrentTotal = total,
                    Message = "Total retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member share type total");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching member total",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("member/{memberNo}")]
        public async Task<IActionResult> GetMemberContributions(string memberNo)
        {
            try
            {
                var contributions = await _contributionService.GetMemberContributionsAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = contributions,
                    Count = contributions.Count,
                    Total = contributions.Sum(c => c.Amount)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching member contributions");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching contributions",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("history/{memberNo}")]
        public async Task<IActionResult> GetMemberContributionHistory(string memberNo)
        {
            try
            {
                var history = await _contributionService.GetMemberContributionHistoryAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = history
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching contribution history");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching contribution history",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("sharetypes")]
        public async Task<IActionResult> GetShareTypes()
        {
            try
            {
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                if (string.IsNullOrEmpty(companyCode))
                {
                    return BadRequest(new { Success = false, Message = "Company code not found" });
                }

                var shareTypes = await _contributionService.GetShareTypesAsync(companyCode);

                return Ok(new
                {
                    Success = true,
                    Data = shareTypes,
                    Count = shareTypes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching share types");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching share types",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchContributions(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? memberNo,
            [FromQuery] string? shareType)
        {
            try
            {
                var contributions = await _contributionService.SearchContributionsAsync(fromDate, toDate, memberNo, shareType);

                return Ok(new
                {
                    Success = true,
                    Data = contributions,
                    Count = contributions.Count,
                    Total = contributions.Sum(c => c.Amount)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contributions");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while searching contributions",
                    Error = ex.Message
                });
            }
        }
    }
}