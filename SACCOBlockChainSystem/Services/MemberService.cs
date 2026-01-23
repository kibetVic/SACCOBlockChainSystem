using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;

namespace SACCOBlockChainSystem.Services
{
    public class MemberService : IMemberService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<MemberService> _logger;

        public MemberService(ApplicationDbContext context, IBlockchainService blockchainService, ILogger<MemberService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        //public async Task<string> GenerateMemberNumber(string companyCode)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        // Check if counter exists for this company
        //        var counter = await _context.MemberNumberCounters
        //            .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

        //        if (counter == null)
        //        {
        //            // Create new counter
        //            counter = new MemberNumberCounter
        //            {
        //                CompanyCode = companyCode,
        //                LastNumber = 0,
        //                LastUpdated = DateTime.Now
        //            };
        //            _context.MemberNumberCounters.Add(counter);
        //        }

        //        // Increment the counter
        //        counter.LastNumber++;
        //        counter.LastUpdated = DateTime.Now;

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        // Format the member number
        //        return $"{companyCode}{counter.LastNumber:00000}";
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, $"Error generating member number for company {companyCode}");

        //        // Fallback: use timestamp
        //        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        //        return $"{companyCode}{timestamp.Substring(timestamp.Length - 8)}";
        //    }
        //}

        public async Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration)
        {
            // Log detailed information for debugging
            _logger.LogInformation($"Starting member registration for: {registration.Surname} {registration.OtherNames}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate required fields
                if (string.IsNullOrEmpty(registration.Surname) || string.IsNullOrEmpty(registration.OtherNames))
                {
                    throw new ValidationException("Surname and Other Names are required.");
                }

                if (string.IsNullOrEmpty(registration.IdNo))
                {
                    throw new ValidationException("ID Number is required.");
                }

                // Check for duplicate ID number
                var existingById = await _context.Members
                    .FirstOrDefaultAsync(m => m.Idno == registration.IdNo);

                if (existingById != null)
                {
                    throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists.");
                }

                // Generate unique member number
                var companyCode = registration.CompanyCode ?? "DEFAULT";
                _logger.LogInformation($"Using company code: {companyCode}");

                // Get the last member number for this company
                var lastMember = await _context.Members
                    .Where(m => m.CompanyCode == companyCode)
                    .OrderByDescending(m => m.MemberNo)
                    .FirstOrDefaultAsync();

                // Generate next member number
                string memberNo;
                if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
                {
                    // First member for this company
                    memberNo = $"{companyCode}00001";
                    _logger.LogInformation($"First member for company {companyCode}, number: {memberNo}");
                }
                else
                {
                    // Extract numeric part from last member number
                    var lastMemberNo = lastMember.MemberNo;

                    // Find the numeric part at the end
                    var numericPart = new string(lastMemberNo.Where(char.IsDigit).ToArray());
                    var prefix = new string(lastMemberNo.Where(c => !char.IsDigit(c)).ToArray());

                    if (int.TryParse(numericPart, out int lastNumber))
                    {
                        // Increment the number
                        var nextNumber = lastNumber + 1;
                        memberNo = $"{prefix}{nextNumber:00000}";
                        _logger.LogInformation($"Incremented member number: {memberNo} from last: {lastMemberNo}");
                    }
                    else
                    {
                        // Fallback: use timestamp
                        memberNo = $"{companyCode}{DateTime.Now:yyyyMMddHHmmss}";
                        _logger.LogWarning($"Could not parse numeric part, using timestamp: {memberNo}");
                    }
                }

                // Final safety check for duplicates
                var existingByMemberNo = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

                if (existingByMemberNo != null)
                {
                    // If duplicate found, append random number
                    var random = new Random();
                    memberNo = $"{memberNo}_{random.Next(1000, 9999)}";
                    _logger.LogWarning($"Duplicate member number detected, using: {memberNo}");
                }

                // Create Member record
                var member = new Member
                {
                    MemberNo = memberNo,
                    Surname = registration.Surname,
                    OtherNames = registration.OtherNames,
                    Idno = registration.IdNo,
                    PhoneNo = registration.PhoneNo,
                    Email = registration.Email,
                    Dob = registration.DateOfBirth,
                    Sex = registration.Gender,
                    CompanyCode = companyCode,
                    ApplicDate = DateTime.Now,
                    EffectDate = DateTime.Now,
                    ShareCap = registration.InitialShares,
                    Status = 1, // Active
                    Mstatus = true,
                    Posted = "Y",
                    AuditId = registration.CreatedBy ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null // Will be set later
                };

                _logger.LogInformation($"Adding member to database: {memberNo}");
                _context.Members.Add(member);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Member saved to database successfully");

                try
                {
                    // Create blockchain data
                    var blockchainData = new
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        IDNo = registration.IdNo,
                        Phone = registration.PhoneNo,
                        Email = registration.Email,
                        DateOfBirth = registration.DateOfBirth?.ToString("yyyy-MM-dd"),
                        Gender = registration.Gender,
                        InitialShares = registration.InitialShares,
                        CompanyCode = companyCode,
                        RegistrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        CreatedBy = registration.CreatedBy ?? "SYSTEM"
                    };

                    _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

                    // Create and add transaction to blockchain WITHOUT starting a new transaction
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "MEMBER_REGISTRATION",
                        memberNo,
                        companyCode,
                        registration.InitialShares,
                        memberNo,
                        blockchainData
                    );

                    // Check if blockchain transaction was created successfully
                    if (blockchainTx == null)
                    {
                        _logger.LogWarning("Blockchain transaction creation returned null");
                        // Continue without blockchain - member is already saved
                    }
                    else
                    {
                        // Update member with blockchain transaction ID
                        member.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction ID saved: {blockchainTx.TransactionId}");
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Transaction committed successfully for member: {memberNo}");

                    return new MemberResponseDTO
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        Status = "ACTIVE",
                        RegistrationDate = DateTime.Now,
                        BlockchainTxId = member.BlockchainTxId,
                        ShareBalance = registration.InitialShares,
                        Email = registration.Email,
                        Phone = registration.PhoneNo,
                        CompanyCode = companyCode
                    };
                }
                catch (Exception blockchainEx)
                {
                    // Log blockchain error but continue with member registration
                    _logger.LogError(blockchainEx, "Error with blockchain transaction, but member was saved to database");

                    // Commit anyway since member is saved
                    await transaction.CommitAsync();

                    return new MemberResponseDTO
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        Status = "ACTIVE",
                        RegistrationDate = DateTime.Now,
                        BlockchainTxId = null, // No blockchain transaction
                        ShareBalance = registration.InitialShares,
                        Email = registration.Email,
                        Phone = registration.PhoneNo,
                        CompanyCode = companyCode
                    };
                }
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction rolled back due to error");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error rolling back transaction");
                }

                // Provide more specific error messages
                if (ex is ValidationException)
                {
                    throw new Exception($"Validation error: {ex.Message}");
                }
                else if (ex is InvalidOperationException)
                {
                    throw new Exception(ex.Message);
                }
                else if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE KEY constraint"))
                {
                    throw new Exception("Member with this ID number already exists. Please use a different ID number.");
                }
                else if (ex.InnerException != null && ex.InnerException.Message.Contains("PRIMARY KEY"))
                {
                    throw new Exception("Duplicate member number detected. Please try again or contact administrator.");
                }

                throw new Exception($"Error registering member: {ex.Message}");
            }
        }

        //public async Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration)
        //{
        //    try
        //    {
        //        // Generate unique member number
        //        var companyCode = registration.CompanyCode ?? "MENO";
        //        var year = DateTime.Now.Year.ToString().Substring(2);
        //        var lastMember = await _context.Members
        //            .Where(m => m.CompanyCode == companyCode)
        //            .OrderByDescending(m => m.MemberNo)
        //            .FirstOrDefaultAsync();

        //        int nextNumber = 1;
        //        if (lastMember != null && lastMember.MemberNo != null)
        //        {
        //            var parts = lastMember.MemberNo.Split('-');
        //            if (parts.Length > 1 && int.TryParse(parts[1], out int lastNum))
        //            {
        //                nextNumber = lastNum + 1;
        //            }
        //        }

        //        var memberNo = $"{companyCode}{nextNumber:00000}";

        //        // Create Member record
        //        var member = new Member
        //        {
        //            MemberNo = memberNo,
        //            Surname = registration.Surname,
        //            OtherNames = registration.OtherNames,
        //            Idno = registration.IdNo,
        //            PhoneNo = registration.PhoneNo,
        //            Email = registration.Email,
        //            Dob = registration.DateOfBirth,
        //            Sex = registration.Gender,
        //            CompanyCode = companyCode,
        //            ApplicDate = DateTime.Now,
        //            EffectDate = DateTime.Now,
        //            ShareCap = registration.InitialShares,
        //            Status = 1, // Active
        //            Mstatus = true,
        //            Posted = "Y",
        //            AuditId = registration.CreatedBy ?? "SYSTEM",
        //            AuditTime = DateTime.Now,
        //            AuditDateTime = DateTime.Now
        //        };

        //        _context.Members.Add(member);

        //        // Create initial share contribution
        //        //if (registration.InitialShares > 0)
        //        //{
        //        //    var contribShare = new ContribShare
        //        //    {
        //        //        MemberNo = memberNo,
        //        //        ContrDate = DateTime.Now,
        //        //        ShareCapitalAmount = registration.InitialShares,
        //        //        CompanyCode = companyCode,
        //        //        ReceiptNo = $"INIT-{memberNo}",
        //        //        Remarks = "Initial Share Purchase",
        //        //        AuditId = registration.CreatedBy ?? "SYSTEM",
        //        //        AuditTime = DateTime.Now,
        //        //        Sharescode = "SH01", // Default share code
        //        //        TransactionNo = Guid.NewGuid().ToString(),
        //        //        AuditDateTime = DateTime.Now
        //        //    };

        //        //    _context.ContribShares.Add(contribShare);

        //        //    // Create share record
        //        //    var share = new Share
        //        //    {
        //        //        MemberNo = memberNo,
        //        //        TotalShares = registration.InitialShares,
        //        //        TransDate = DateTime.Now,
        //        //        Sharescode = "SH01",
        //        //        Initshares = registration.InitialShares,
        //        //        CompanyCode = companyCode,
        //        //        AuditId = registration.CreatedBy ?? "SYSTEM",
        //        //        AuditTime = DateTime.Now,
        //        //        AuditDateTime = DateTime.Now
        //        //    };

        //        //    _context.Shares.Add(share);
        //        //}

        //        // Create blockchain transaction
        //        var blockchainData = new
        //        {
        //            MemberNo = memberNo,
        //            FullName = $"{registration.Surname} {registration.OtherNames}",
        //            IDNo = registration.IdNo,
        //            Phone = registration.PhoneNo,
        //            InitialShares = registration.InitialShares,
        //            RegistrationDate = DateTime.Now
        //        };

        //        var blockchainTx = await _blockchainService.CreateTransaction(
        //            "MEMBER_REGISTRATION",
        //            memberNo,
        //            companyCode,
        //            registration.InitialShares,
        //            memberNo, // Using MemberNo as off-chain reference
        //            blockchainData
        //        );

        //        member.BlockchainTxId = blockchainTx.TransactionId;

        //        // Save all changes
        //        await _context.SaveChangesAsync();

        //        // Add to blockchain
        //        await _blockchainService.AddToBlockchain(blockchainTx);

        //        return new MemberResponseDTO
        //        {
        //            MemberNo = memberNo,
        //            FullName = $"{registration.Surname} {registration.OtherNames}",
        //            Status = "ACTIVE",
        //            RegistrationDate = DateTime.Now,
        //            BlockchainTxId = blockchainTx.TransactionId,
        //            ShareBalance = registration.InitialShares
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error registering member");
        //        throw;
        //    }
        //}

        public async Task<Member> GetMemberByMemberNoAsync(string memberNo)
        {
            return await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);
        }

        public async Task<List<Member>> SearchMembersAsync(string searchTerm)
        {
            return await _context.Members
                .Where(m => m.MemberNo.Contains(searchTerm) ||
                           m.Surname.Contains(searchTerm) ||
                           m.OtherNames.Contains(searchTerm) ||
                           m.Idno.Contains(searchTerm))
                .Take(50)
                .ToListAsync();
        }

        public async Task<decimal> GetMemberShareBalanceAsync(string memberNo)
        {
            var share = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo);

            return share?.TotalShares ?? 0;
        }

        public async Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember)
        {
            var member = await GetMemberByMemberNoAsync(memberNo);
            if (member == null) return false;

            // Update fields (excluding sensitive/immutable fields)
            member.Surname = updatedMember.Surname ?? member.Surname;
            member.OtherNames = updatedMember.OtherNames ?? member.OtherNames;
            member.PhoneNo = updatedMember.PhoneNo ?? member.PhoneNo;
            member.Email = updatedMember.Email ?? member.Email;
            member.PresentAddr = updatedMember.PresentAddr ?? member.PresentAddr;
            member.AuditTime = DateTime.Now;
            member.AuditDateTime = DateTime.Now;

            // Create blockchain transaction for update
            var updateData = new
            {
                MemberNo = memberNo,
                UpdatedFields = new
                {
                    Phone = updatedMember.PhoneNo,
                    Email = updatedMember.Email,
                    Address = updatedMember.PresentAddr
                },
                UpdateTime = DateTime.Now
            };

            var blockchainTx = await _blockchainService.CreateTransaction(
                "MEMBER_UPDATE",
                memberNo,
                member.CompanyCode,
                0,
                memberNo,
                updateData
            );

            member.BlockchainTxId = blockchainTx.TransactionId;

            await _context.SaveChangesAsync();
            await _blockchainService.AddToBlockchain(blockchainTx);

            return true;
        }

        public async Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo)
        {
            return await _blockchainService.GetMemberTransactions(memberNo);
        }

        public async Task<List<Member>> GetAllMembersAsync()
        {
            return await _context.Members
                .Where(m => m.Status == 1) // Active members only
                .OrderBy(m => m.Surname)
                .ToListAsync();
        }

        public async Task<decimal> GetShareBalanceAsync(string memberNo)
        {
            // Assuming there's a Share model/entity
            var share = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo);

            return share?.TotalShares ?? 0;
        }
    }
}