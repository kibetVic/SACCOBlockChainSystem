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

        // ============== ADD THIS METHOD ==============
        // GET: /Blockchain/Status (called from JavaScript)
        [HttpGet("Blockchain/Status")]
        public async Task<IActionResult> Status()
        {
            try
            {
                _logger.LogInformation("Getting blockchain status");

                var status = await _blockchainService.GetBlockchainStatus();

                return Json(new
                {
                    success = true,
                    totalBlocks = status.TotalBlocks,
                    totalTransactions = status.TotalTransactions,
                    pendingTransactions = status.PendingTransactions,
                    latestBlockHash = status.LatestBlockHash,
                    latestBlockTimestamp = status.LatestBlockTimestamp,
                    //isChainValid = status.IsChainValid,
                    //blockchainHeight = status.BlockchainHeight
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blockchain status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Blockchain/Stats (optional, for additional data)
        [HttpGet("Blockchain/Stats")]
        public async Task<IActionResult> Stats()
        {
            try
            {
                var stats = await _blockchainService.GetBlockchainStatistics();
                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blockchain stats");
                return Json(new { success = false, message = ex.Message });
            }
        }
        // Add this to your BlockchainController.cs
        //[HttpGet("Status")]
        //public async Task<IActionResult> Status()
        //{
        //    try
        //    {
        //        var status = await _blockchainService.GetBlockchainStatus();

        //        return Json(new
        //        {
        //            success = true,
        //            status = status.IsValid ? "Connected" : "Issues Detected",
        //            totalTransactions = status.TotalTransactions,
        //            totalBlocks = status.TotalBlocks,
        //            pendingTransactions = status.PendingTransactions,
        //            lastSync = DateTime.Now,
        //            pendingLoans = 0 // You can calculate this from your loan service if needed
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting blockchain status");
        //        return Json(new
        //        {
        //            success = false,
        //            status = "Error",
        //            totalTransactions = 0,
        //            totalBlocks = 0,
        //            pendingTransactions = 0,
        //            lastSync = DateTime.Now
        //        });
        //    }
        //}
        // GET: /Blockchain/Verify/{transactionId}
        [HttpGet("Blockchain/Verify/{transactionId}")]
        public async Task<IActionResult> Verify(string transactionId)
        {
            try
            {
                _logger.LogInformation($"Verifying blockchain transaction: {transactionId}");

                var transaction = await _blockchainService.GetTransactionAsync(transactionId);

                if (transaction == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Transaction not found in blockchain"
                    });
                }

                var isVerified = await _blockchainService.VerifyTransactionAsync(transactionId);

                return Json(new
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
                    return Json(new
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
                    return Json(new
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