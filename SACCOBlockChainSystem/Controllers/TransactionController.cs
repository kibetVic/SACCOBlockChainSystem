using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize(Policy = "MemberOnly")]
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
        {
            _transactionService = transactionService;
            _logger = logger;
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositDTO deposit)
        {
            try
            {
                var result = await _transactionService.ProcessDepositAsync(deposit);
                return Ok(new
                {
                    Success = true,
                    Message = "Deposit processed successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deposit");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawalDTO withdrawal)
        {
            try
            {
                var result = await _transactionService.ProcessWithdrawalAsync(withdrawal);
                return Ok(new
                {
                    Success = true,
                    Message = "Withdrawal processed successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal");
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("member/{memberNo}")]
        public async Task<IActionResult> GetMemberTransactions(string memberNo, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var transactions = await _transactionService.GetMemberTransactionsAsync(memberNo, fromDate, toDate);
                return Ok(new
                {
                    Success = true,
                    Data = transactions,
                    Count = transactions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transactions");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error fetching transactions"
                });
            }
        }

        [HttpGet("balance/{memberNo}")]
        public async Task<IActionResult> GetMemberBalance(string memberNo)
        {
            try
            {
                var balance = await _transactionService.GetMemberBalanceAsync(memberNo);
                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        MemberNo = memberNo,
                        Balance = balance,
                        LastUpdated = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching balance");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error fetching balance"
                });
            }
        }
    }
}