// Controllers/BlockchainController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SACCOBlockChainSystem.Services;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class BlockchainController : Controller
    {
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<BlockchainController> _logger;

        public BlockchainController(
            IBlockchainService blockchainService,
            ILogger<BlockchainController> logger)
        {
            _blockchainService = blockchainService;
            _logger = logger;
        }

        // GET: /Blockchain/Verify/{transactionId}
        [HttpGet("Blockchain/Verify/{transactionId}")]
        public async Task<IActionResult> Verify(string transactionId)
        {
            try
            {
                _logger.LogInformation($"Verifying blockchain transaction: {transactionId}");

                // Get transaction from blockchain service
                var transaction = await _blockchainService.GetTransactionAsync(transactionId);

                if (transaction == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Transaction not found in blockchain"
                    });
                }

                // Verify the transaction
                var isVerified = await _blockchainService.VerifyTransactionAsync(transactionId);

                return Ok(new
                {
                    success = true,
                    transaction = new
                    {
                        transactionId = transaction.TransactionId,
                        transactionType = transaction.TransactionType,
                        memberNo = transaction.MemberNo,
                        amount = transaction.Amount,
                        timestamp = transaction.Timestamp,
                        status = transaction.Status,
                        dataHash = transaction.DataHash,
                        blockHash = transaction.BlockHash,
                        verified = isVerified
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying transaction: {transactionId}");
                return BadRequest(new
                {
                    success = false,
                    message = "Error verifying transaction",
                    error = ex.Message
                });
            }
        }

        // GET: /Blockchain/Blocks
        public async Task<IActionResult> Blocks()
        {
            try
            {
                var blocks = await _blockchainService.GetAllBlocksAsync();
                return View(blocks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading blocks");
                return View("Error");
            }
        }

        // GET: /Blockchain/Transaction/{transactionId}
        [HttpGet("Blockchain/Transaction/{transactionId}")]
        public async Task<IActionResult> TransactionDetails(string transactionId)
        {
            try
            {
                var transaction = await _blockchainService.GetTransactionAsync(transactionId);

                if (transaction == null)
                {
                    return NotFound();
                }

                // Get the block containing this transaction
                var block = transaction.BlockHash != null
                    ? await _blockchainService.GetBlockAsync(transaction.BlockHash)
                    : null;

                var viewModel = new
                {
                    Transaction = transaction,
                    Block = block,
                    Verified = block != null && block.Confirmed
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction details");
                return View("Error");
            }
        }

        // GET: /Blockchain/VerifyBlock/{blockHash}
        [HttpGet("Blockchain/VerifyBlock/{blockHash}")]
        public async Task<IActionResult> VerifyBlock(string blockHash)
        {
            try
            {
                var isValid = await _blockchainService.VerifyBlockchainAsync();

                if (isValid)
                {
                    var block = await _blockchainService.GetBlockAsync(blockHash);
                    return Ok(new
                    {
                        success = true,
                        message = "Blockchain is valid",
                        block = block?.BlockHash,
                        blockId = block?.BlockId,
                        confirmed = block?.Confirmed ?? false
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Blockchain integrity check failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying blockchain");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}