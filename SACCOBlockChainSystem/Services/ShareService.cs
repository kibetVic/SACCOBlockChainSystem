using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Repositories;

namespace SACCOBlockChainSystem.Services
{
    public class ShareService : IShareService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IMemberRepository _memberRepository;
        private readonly IAuditService _auditService;
        private readonly ILogger<ShareService> _logger;

        public ShareService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IMemberRepository memberRepository,
            IAuditService auditService,
            ILogger<ShareService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _memberRepository = memberRepository;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<SharePurchaseResponseDTO> PurchaseSharesAsync(SharePurchaseDTO purchase)
        {
            try
            {
                // Validate member exists
                var member = await _memberRepository.GetByMemberNoAsync(purchase.MemberNo);
                if (member == null)
                    throw new Exception($"Member {purchase.MemberNo} not found");

                // Get or create share record
                var share = await _context.Shares
                    .FirstOrDefaultAsync(s => s.MemberNo == purchase.MemberNo && s.Sharescode == purchase.ShareType);

                if (share == null)
                {
                    share = new Share
                    {
                        MemberNo = purchase.MemberNo,
                        Sharescode = purchase.ShareType,
                        TotalShares = purchase.Amount,
                        TransDate = DateTime.Now,
                        LastDivDate = null,
                        AuditId = purchase.ProcessedBy ?? "SYSTEM",
                        AuditTime = DateTime.Now,
                        Initshares = purchase.Amount,
                        CompanyCode = member.CompanyCode,
                        AuditDateTime = DateTime.Now
                    };
                    _context.Shares.Add(share);
                }
                else
                {
                    share.TotalShares += purchase.Amount;
                    share.AuditTime = DateTime.Now;
                    share.AuditDateTime = DateTime.Now;
                }

                // Create ContribShare record
                var contribShare = new ContribShare
                {
                    MemberNo = purchase.MemberNo,
                    ContrDate = DateTime.Now,
                    ShareCapitalAmount = purchase.Amount,
                    CompanyCode = member.CompanyCode,
                    ReceiptNo = purchase.ReceiptNo ?? GenerateReceiptNumber(),
                    Remarks = purchase.Remarks ?? "Share purchase",
                    AuditId = purchase.ProcessedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    Sharescode = purchase.ShareType,
                    TransactionNo = GenerateTransactionNumber(),
                    AuditDateTime = DateTime.Now
                };

                // Create blockchain transaction
                var blockchainData = new
                {
                    MemberNo = purchase.MemberNo,
                    ShareType = purchase.ShareType,
                    Amount = purchase.Amount,
                    ReceiptNo = contribShare.ReceiptNo,
                    TotalSharesAfter = share.TotalShares,
                    PurchaseDate = DateTime.Now
                };

                var blockchainTx = await _blockchainService.CreateTransaction(
                    "SHARE_PURCHASE",
                    purchase.MemberNo,
                    member.CompanyCode,
                    purchase.Amount,
                    contribShare.Id.ToString(),
                    blockchainData
                );

                contribShare.BlockchainTxId = blockchainTx.TransactionId;

                // Update member's total share capital
                member.ShareCap = (member.ShareCap ?? 0) + purchase.Amount;
                member.AuditTime = DateTime.Now;
                member.AuditDateTime = DateTime.Now;

                // Save all changes
                _context.ContribShares.Add(contribShare);
                await _context.SaveChangesAsync();

                // Add to blockchain
                await _blockchainService.AddToBlockchain(blockchainTx);

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Shares",
                    purchase.MemberNo,
                    "UPDATE",
                    $"Previous shares: {(share.TotalShares - purchase.Amount)}",
                    $"New shares: {share.TotalShares}",
                    purchase.ProcessedBy ?? "SYSTEM",
                    purchase.ProcessedBy ?? "SYSTEM"
                );

                return new SharePurchaseResponseDTO
                {
                    Success = true,
                    ReceiptNo = contribShare.ReceiptNo,
                    Amount = purchase.Amount,
                    ShareType = purchase.ShareType,
                    TotalShares = share.TotalShares ?? 0,
                    BlockchainTxId = blockchainTx.TransactionId,
                    PurchaseDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing share purchase");
                throw;
            }
        }

        public async Task<List<Share>> GetMemberSharesAsync(string memberNo)
        {
            return await _context.Shares
                .Where(s => s.MemberNo == memberNo)
                .OrderBy(s => s.Sharescode)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalSharesValueAsync(string memberNo)
        {
            return await _context.Shares
                .Where(s => s.MemberNo == memberNo)
                .SumAsync(s => s.TotalShares ?? 0);
        }

        public async Task<bool> TransferSharesAsync(ShareTransferDTO transfer)
        {
            try
            {
                // Validate source member has enough shares
                var sourceShares = await GetTotalSharesValueAsync(transfer.FromMemberNo);
                if (sourceShares < transfer.Amount)
                    throw new Exception($"Insufficient shares. Available: {sourceShares}, Requested: {transfer.Amount}");

                // Validate destination member exists
                var destMember = await _memberRepository.GetByMemberNoAsync(transfer.ToMemberNo);
                if (destMember == null)
                    throw new Exception($"Destination member {transfer.ToMemberNo} not found");

                // Get share records
                var sourceShare = await _context.Shares
                    .FirstOrDefaultAsync(s => s.MemberNo == transfer.FromMemberNo && s.Sharescode == transfer.ShareType);

                var destShare = await _context.Shares
                    .FirstOrDefaultAsync(s => s.MemberNo == transfer.ToMemberNo && s.Sharescode == transfer.ShareType);

                // Update source shares
                if (sourceShare != null)
                {
                    sourceShare.TotalShares -= transfer.Amount;
                    sourceShare.AuditTime = DateTime.Now;
                    sourceShare.AuditDateTime = DateTime.Now;
                }

                // Update or create destination shares
                if (destShare == null)
                {
                    destShare = new Share
                    {
                        MemberNo = transfer.ToMemberNo,
                        Sharescode = transfer.ShareType,
                        TotalShares = transfer.Amount,
                        TransDate = DateTime.Now,
                        AuditId = transfer.ProcessedBy ?? "SYSTEM",
                        AuditTime = DateTime.Now,
                        Initshares = transfer.Amount,
                        CompanyCode = destMember.CompanyCode ?? "DEFAULT",
                        AuditDateTime = DateTime.Now
                    };
                    _context.Shares.Add(destShare);
                }
                else
                {
                    destShare.TotalShares += transfer.Amount;
                    destShare.AuditTime = DateTime.Now;
                    destShare.AuditDateTime = DateTime.Now;
                }

                // Create blockchain transaction
                var blockchainData = new
                {
                    FromMemberNo = transfer.FromMemberNo,
                    ToMemberNo = transfer.ToMemberNo,
                    ShareType = transfer.ShareType,
                    Amount = transfer.Amount,
                    Reason = transfer.Reason,
                    TransferDate = DateTime.Now
                };

                var blockchainTx = await _blockchainService.CreateTransaction(
                    "SHARE_TRANSFER",
                    transfer.FromMemberNo,
                    destMember.CompanyCode,
                    transfer.Amount,
                    $"{transfer.FromMemberNo}_{transfer.ToMemberNo}_{DateTime.Now:yyyyMMdd}",
                    blockchainData
                );

                // Save changes
                await _context.SaveChangesAsync();

                // Add to blockchain
                await _blockchainService.AddToBlockchain(blockchainTx);

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Shares",
                    $"{transfer.FromMemberNo}->{transfer.ToMemberNo}",
                    "TRANSFER",
                    null,
                    $"Share transfer of {transfer.Amount} ({transfer.ShareType})",
                    transfer.ProcessedBy ?? "SYSTEM",
                    transfer.ProcessedBy ?? "SYSTEM"
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring shares");
                throw;
            }
        }

        public async Task<DividendDistributionResponseDTO> DistributeDividendsAsync(DividendDistributionDTO distribution)
        {
            try
            {
                var totalDividends = 0m;
                var membersProcessed = 0;

                // Get all active members with shares
                var membersWithShares = await _context.Members
                    .Where(m => m.Status == 1)
                    .Join(_context.Shares,
                        m => m.MemberNo,
                        s => s.MemberNo,
                        (m, s) => new { Member = m, Share = s })
                    .ToListAsync();

                // Group by member
                var memberGroups = membersWithShares
                    .GroupBy(x => x.Member.MemberNo)
                    .Select(g => new
                    {
                        MemberNo = g.Key,
                        Member = g.First().Member,
                        TotalShares = g.Sum(x => x.Share.TotalShares ?? 0)
                    })
                    .Where(x => x.TotalShares > 0)
                    .ToList();

                foreach (var memberGroup in memberGroups)
                {
                    // Calculate dividend for this member
                    var dividend = memberGroup.TotalShares * (distribution.DividendRate / 100);
                    totalDividends += dividend;
                    membersProcessed++;

                    // Update member's share record (add dividend as additional shares)
                    var share = await _context.Shares
                        .FirstOrDefaultAsync(s => s.MemberNo == memberGroup.MemberNo && s.Sharescode == "DIV01");

                    if (share == null)
                    {
                        share = new Share
                        {
                            MemberNo = memberGroup.MemberNo,
                            Sharescode = "DIV01",
                            TotalShares = dividend,
                            TransDate = DateTime.Now,
                            LastDivDate = DateTime.Now,
                            AuditId = distribution.ProcessedBy ?? "SYSTEM",
                            AuditTime = DateTime.Now,
                            Initshares = dividend,
                            CompanyCode = memberGroup.Member.CompanyCode ?? "DEFAULT",
                            AuditDateTime = DateTime.Now
                        };
                        _context.Shares.Add(share);
                    }
                    else
                    {
                        share.TotalShares += dividend;
                        share.LastDivDate = DateTime.Now;
                        share.AuditTime = DateTime.Now;
                        share.AuditDateTime = DateTime.Now;
                    }

                    // Create blockchain transaction for each member
                    var blockchainData = new
                    {
                        MemberNo = memberGroup.MemberNo,
                        DividendRate = distribution.DividendRate,
                        TotalShares = memberGroup.TotalShares,
                        DividendAmount = dividend,
                        DistributionDate = DateTime.Now
                    };

                    var blockchainTx = await _blockchainService.CreateTransaction(
                        "DIVIDEND_DISTRIBUTION",
                        memberGroup.MemberNo,
                        memberGroup.Member.CompanyCode,
                        dividend,
                        $"DIV_{DateTime.Now:yyyyMMdd}_{memberGroup.MemberNo}",
                        blockchainData
                    );

                    // Add to blockchain
                    await _blockchainService.AddToBlockchain(blockchainTx);
                }

                await _context.SaveChangesAsync();

                // Log audit trail
                await _auditService.LogActivityAsync(
                    "Shares",
                    "ALL",
                    "DIVIDEND_DISTRIBUTION",
                    null,
                    $"Dividend distribution at {distribution.DividendRate}% to {membersProcessed} members. Total: {totalDividends}",
                    distribution.ProcessedBy ?? "SYSTEM",
                    distribution.ProcessedBy ?? "SYSTEM"
                );

                return new DividendDistributionResponseDTO
                {
                    Success = true,
                    TotalDividends = totalDividends,
                    MembersProcessed = membersProcessed,
                    DividendRate = distribution.DividendRate,
                    DistributionDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error distributing dividends");
                throw;
            }
        }

        private string GenerateTransactionNumber()
        {
            return $"SH{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private string GenerateReceiptNumber()
        {
            // Method 1: Using GUID hash for guaranteed uniqueness
            var guid = Guid.NewGuid().ToString("N");

            // Take first 4 letters (A-F from hex) and ensure they're uppercase letters
            var letters = guid.Substring(0, 4)
                .Select(c => (char)('A' + (c % 26)))
                .ToArray();

            // Take next 6 digits
            var digits = guid.Substring(4, 8)
                .Select(c => (char)('0' + (c % 12)))
                .ToArray();

            // Combine: 4 letters + 8 digits
            var receiptNumber = new string(letters) + new string(digits);

            // Optional: Add a check digit or prefix if needed
            // return $"SR{receiptNumber}";

            return receiptNumber;
        }
    }
}