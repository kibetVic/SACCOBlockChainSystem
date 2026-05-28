using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MemberController : ControllerBase
    {
        private readonly IMemberService _memberService;
        private readonly IContributionService _contributionService;
        private readonly ILogger<MemberController> _logger;
        private readonly ApplicationDbContext _context;
        private WalletService _walletService;
        public MemberController(IMemberService memberService, IContributionService contributionService, WalletService walletService, ApplicationDbContext context, ILogger<MemberController> logger)
        {
            _memberService = memberService;
            _contributionService = contributionService;
            _walletService = walletService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterMember([FromBody] MemberRegistrationDTO registration)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
                var companyCode = User.FindFirst("CompanyCode")?.Value;
                
                var result = await _memberService.RegisterMemberAsync(registration);

                return Ok(new
                {
                    Success = true,
                    Message = "Member registered successfully",
                    Data = result,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while registering member",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("{memberNo}")]
        public async Task<IActionResult> GetMember(string memberNo)
        {
            try
            {
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);

                if (member == null)
                    return NotFound(new { Success = false, Message = "Member not found" });

                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        member.MemberNo,
                        member.Surname,
                        member.OtherNames,
                        member.Idno,
                        member.PhoneNo,
                        member.Email,
                        member.CompanyCode,
                        member.Status,
                        member.ShareCap,
                        member.BlockchainTxId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching member");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching member",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchMembers([FromQuery] string searchTerm)
        {
            try
            {
                var members = await _memberService.SearchMembersAsync(searchTerm);

                return Ok(new
                {
                    Success = true,
                    Data = members.Select(m => new
                    {
                        m.MemberNo,
                        m.Surname,
                        m.OtherNames,
                        m.Idno,
                        m.PhoneNo,
                        m.CompanyCode,
                        m.Status
                    }),
                    Count = members.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while searching members",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("{memberNo}/shares")]
        public async Task<IActionResult> GetMemberShares(string memberNo)
        {
            try
            {
                var shareBalance = await _contributionService.GetMemberShareBalanceAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        MemberNo = memberNo,
                        ShareBalance = shareBalance,
                        LastUpdated = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching member shares");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching member shares",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("{memberNo}/blockchain-history")]
        public async Task<IActionResult> GetBlockchainHistory(string memberNo)
        {
            try
            {
                var transactions = await _memberService.GetMemberBlockchainHistoryAsync(memberNo);

                return Ok(new
                {
                    Success = true,
                    Data = transactions.Select(t => new
                    {
                        t.TransactionId,
                        t.TransactionType,
                        t.Amount,
                        t.Timestamp,
                        t.DataHash,
                        t.Status
                    }),
                    Count = transactions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching blockchain history");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching blockchain history",
                    Error = ex.Message
                });
            }
        }

        [HttpPut("{memberNo}")]
        public async Task<IActionResult> UpdateMember(string memberNo, [FromBody] Member updatedMember)
        {
            try
            {
                var success = await _contributionService.UpdateMemberAsync(memberNo, updatedMember);

                if (!success)
                    return NotFound(new { Success = false, Message = "Member not found" });

                return Ok(new
                {
                    Success = true,
                    Message = "Member updated successfully",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while updating member",
                    Error = ex.Message
                });
            }
        }

        // GET: /api/Member/wallet
        [HttpGet("wallet")]
        public async Task<IActionResult> GetMemberWallet()
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return Unauthorized(new { success = false, message = "Member not found" });
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (member == null)
                {
                    return NotFound(new { success = false, message = "Member not found" });
                }

                return Ok(new
                {
                    success = true,
                    walletAddress = member.WalletAddress ?? "Not created",
                    hasWallet = !string.IsNullOrEmpty(member.WalletAddress)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member wallet");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: /api/Member/blockchain-status
        [HttpGet("blockchain-status")]
        public async Task<IActionResult> GetBlockchainStatus()
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();
                if (string.IsNullOrEmpty(memberNo))
                {
                    return Unauthorized(new { success = false, message = "Member not found" });
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                var transactionCount = await _context.Contribs
                    .CountAsync(c => c.MemberNo == memberNo && !string.IsNullOrEmpty(c.BlockchainTxId));

                var hasBlockchainRecord = !string.IsNullOrEmpty(member?.BlockchainTxId);

                return Ok(new
                {
                    success = true,
                    hasBlockchainRecord = hasBlockchainRecord,
                    transactionCount = transactionCount,
                    lastTxId = member?.BlockchainTxId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blockchain status");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private string GetLoggedInMemberNumber()
        {
            var memberNoClaim = User.FindFirst("MemberNo")?.Value;
            if (!string.IsNullOrEmpty(memberNoClaim))
            {
                return memberNoClaim;
            }

            var nameClaim = User.Identity?.Name;
            if (!string.IsNullOrEmpty(nameClaim))
            {
                var member = _context.Members.FirstOrDefault(m => m.MemberNo == nameClaim);
                return member?.MemberNo;
            }

            return null;
        }
    }
}