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
        private readonly ICompanyContextService _companyContextService;

        public MemberService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<MemberService> logger,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
            _companyContextService = companyContextService;
        }

        public async Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration)
        {
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

                // Get current user's company code
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                _logger.LogInformation($"Using company code from current user context: {currentCompanyCode}");

                // Check for duplicate ID number within the same company
                var existingById = await _context.Members
                    .FirstOrDefaultAsync(m => m.Idno == registration.IdNo && m.CompanyCode == currentCompanyCode);

                if (existingById != null)
                {
                    throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists in company {currentCompanyCode}.");
                }

                // Generate unique member number based on current company
                string memberNo;

                // Get the last member number for this company
                var lastMember = await _context.Members
                    .Where(m => m.CompanyCode == currentCompanyCode)
                    .OrderByDescending(m => m.MemberNo)
                    .FirstOrDefaultAsync();

                if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
                {
                    // First member for this company
                    memberNo = $"{currentCompanyCode}00001";
                    _logger.LogInformation($"First member for company {currentCompanyCode}, number: {memberNo}");
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
                        memberNo = $"{currentCompanyCode}{DateTime.Now:yyyyMMddHHmmss}";
                        _logger.LogWarning($"Could not parse numeric part, using timestamp: {memberNo}");
                    }
                }

                // Final safety check for duplicates
                var existingByMemberNo = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (existingByMemberNo != null)
                {
                    // If duplicate found, append random number
                    var random = new Random();
                    memberNo = $"{memberNo}_{random.Next(1000, 9999)}";
                    _logger.LogWarning($"Duplicate member number detected, using: {memberNo}");
                }

                // Get current user info
                var currentUserId = _companyContextService.GetCurrentUserId();
                var currentUserName = _companyContextService.GetCurrentUserName();

                // Create Member record with current company code
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
                    CompanyCode = currentCompanyCode, // Use current company code
                    Cigcode = currentCompanyCode, // Assuming CIG code is same as company code
                    ApplicDate = DateTime.Now,
                    EffectDate = DateTime.Now,
                    ShareCap = registration.InitialShares,
                    Status = 1, // Active
                    Mstatus = true,
                    Posted = "Y",
                    AuditId = currentUserName, // Use current logged in user
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null // Will be set later
                };

                _logger.LogInformation($"Adding member to database: {memberNo} for company: {currentCompanyCode}");
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
                        CompanyCode = currentCompanyCode,
                        RegistrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        CreatedBy = currentUserName,
                        CreatedById = currentUserId
                    };

                    _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

                    // Create and add transaction to blockchain
                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "MEMBER_REGISTRATION",
                        memberNo,
                        currentCompanyCode,
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
                        CompanyCode = currentCompanyCode
                    };
                }
                catch (Exception blockchainEx)
                {
                    _logger.LogError(blockchainEx, "Error with blockchain transaction, but member was saved to database");

                    await transaction.CommitAsync();

                    return new MemberResponseDTO
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        Status = "ACTIVE",
                        RegistrationDate = DateTime.Now,
                        BlockchainTxId = null,
                        ShareBalance = registration.InitialShares,
                        Email = registration.Email,
                        Phone = registration.PhoneNo,
                        CompanyCode = currentCompanyCode
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

        public async Task<Member> GetMemberByMemberNoAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            return await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);
        }

        public async Task<List<Member>> SearchMembersAsync(string searchTerm)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            return await _context.Members
                .Where(m => m.CompanyCode == currentCompanyCode &&
                           (m.MemberNo.Contains(searchTerm) ||
                            m.Surname.Contains(searchTerm) ||
                            m.OtherNames.Contains(searchTerm) ||
                            m.Idno.Contains(searchTerm)))
                .Take(50)
                .ToListAsync();
        }

        public async Task<decimal> GetMemberShareBalanceAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var share = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo && s.CompanyCode == currentCompanyCode);

            return share?.TotalShares ?? 0;
        }

        public async Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
            var currentUserName = _companyContextService.GetCurrentUserName();

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

            if (member == null) return false;

            // Update fields
            member.Surname = updatedMember.Surname ?? member.Surname;
            member.OtherNames = updatedMember.OtherNames ?? member.OtherNames;
            member.PhoneNo = updatedMember.PhoneNo ?? member.PhoneNo;
            member.Email = updatedMember.Email ?? member.Email;
            member.PresentAddr = updatedMember.PresentAddr ?? member.PresentAddr;
            member.AuditId = currentUserName;
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
                UpdatedBy = currentUserName,
                UpdateTime = DateTime.Now,
                CompanyCode = currentCompanyCode
            };

            var blockchainTx = await _blockchainService.CreateTransaction(
                "MEMBER_UPDATE",
                memberNo,
                currentCompanyCode,
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
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            return await _blockchainService.GetMemberTransactions(memberNo, currentCompanyCode);
        }

        public async Task<List<Member>> GetAllMembersAsync()
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            return await _context.Members
                .Where(m => m.CompanyCode == currentCompanyCode && m.Status == 1)
                .OrderBy(m => m.Surname)
                .ToListAsync();
        }

        public async Task<decimal> GetShareBalanceAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var share = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo && s.CompanyCode == currentCompanyCode);

            return share?.TotalShares ?? 0;
        }

        public Task<MemberDTO> GetMemberDetailsAsync(string memberNo)
        {
            throw new NotImplementedException();
        }
    }
}