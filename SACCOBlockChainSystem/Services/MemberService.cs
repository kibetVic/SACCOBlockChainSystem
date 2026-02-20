using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
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

        //public async Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration)
        //{
        //    _logger.LogInformation($"Starting member registration for: {registration.Surname} {registration.OtherNames}");

        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        // Validate required fields
        //        if (string.IsNullOrEmpty(registration.Surname) || string.IsNullOrEmpty(registration.OtherNames))
        //        {
        //            throw new ValidationException("Surname and Other Names are required.");
        //        }

        //        if (string.IsNullOrEmpty(registration.IdNo))
        //        {
        //            throw new ValidationException("ID Number is required.");
        //        }

        //        // Get current user's company code
        //        var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
        //        _logger.LogInformation($"Using company code from current user context: {currentCompanyCode}");

        //        // Check for duplicate ID number within the same company
        //        var existingById = await _context.Members
        //            .FirstOrDefaultAsync(m => m.Idno == registration.IdNo && m.CompanyCode == currentCompanyCode);

        //        if (existingById != null)
        //        {
        //            throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists in company {currentCompanyCode}.");
        //        }

        //        // Generate unique member number based on current company
        //        string memberNo;

        //        // Get the last member number for this company
        //        var lastMember = await _context.Members
        //            .Where(m => m.CompanyCode == currentCompanyCode)
        //            .OrderByDescending(m => m.MemberNo)
        //            .FirstOrDefaultAsync();

        //        if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
        //        {
        //            // First member for this company
        //            memberNo = $"{currentCompanyCode}00001";
        //            _logger.LogInformation($"First member for company {currentCompanyCode}, number: {memberNo}");
        //        }
        //        else
        //        {
        //            // Extract numeric part from last member number
        //            var lastMemberNo = lastMember.MemberNo;

        //            // Find the numeric part at the end
        //            var numericPart = new string(lastMemberNo.Where(char.IsDigit).ToArray());
        //            var prefix = new string(lastMemberNo.Where(c => !char.IsDigit(c)).ToArray());

        //            if (int.TryParse(numericPart, out int lastNumber))
        //            {
        //                // Increment the number
        //                var nextNumber = lastNumber + 1;
        //                memberNo = $"{prefix}{nextNumber:00000}";
        //                _logger.LogInformation($"Incremented member number: {memberNo} from last: {lastMemberNo}");
        //            }
        //            else
        //            {
        //                // Fallback: use timestamp
        //                memberNo = $"{currentCompanyCode}{DateTime.Now:yyyyMMddHHmmss}";
        //                _logger.LogWarning($"Could not parse numeric part, using timestamp: {memberNo}");
        //            }
        //        }

        //        // Final safety check for duplicates
        //        var existingByMemberNo = await _context.Members
        //            .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

        //        if (existingByMemberNo != null)
        //        {
        //            // If duplicate found, append random number
        //            var random = new Random();
        //            memberNo = $"{memberNo}_{random.Next(1000, 9999)}";
        //            _logger.LogWarning($"Duplicate member number detected, using: {memberNo}");
        //        }

        //        // Get current user info
        //        var currentUserId = _companyContextService.GetCurrentUserId();
        //        var currentUserName = _companyContextService.GetCurrentUserName();

        //        // Create Member record with current company code
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
        //            CompanyCode = currentCompanyCode, // Use current company code
        //            Cigcode = currentCompanyCode, // Assuming CIG code is same as company code
        //            ApplicDate = DateTime.Now,
        //            EffectDate = DateTime.Now,
        //            ShareCap = registration.InitialShares,
        //            Status = 1, // Active
        //            Mstatus = true,
        //            Posted = "Y",
        //            AuditId = currentUserName, // Use current logged in user
        //            AuditTime = DateTime.Now,
        //            AuditDateTime = DateTime.Now,
        //            BlockchainTxId = null // Will be set later
        //        };

        //        _logger.LogInformation($"Adding member to database: {memberNo} for company: {currentCompanyCode}");
        //        _context.Members.Add(member);
        //        await _context.SaveChangesAsync();
        //        _logger.LogInformation($"Member saved to database successfully");

        //        try
        //        {
        //            // Create blockchain data
        //            var blockchainData = new
        //            {
        //                MemberNo = memberNo,
        //                FullName = $"{registration.Surname} {registration.OtherNames}",
        //                IDNo = registration.IdNo,
        //                Phone = registration.PhoneNo,
        //                Email = registration.Email,
        //                DateOfBirth = registration.DateOfBirth?.ToString("yyyy-MM-dd"),
        //                Gender = registration.Gender,
        //                InitialShares = registration.InitialShares,
        //                CompanyCode = currentCompanyCode,
        //                RegistrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        //                CreatedBy = currentUserName,
        //                CreatedById = currentUserId
        //            };

        //            _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

        //            // Create and add transaction to blockchain
        //            var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
        //                "MEMBER_REGISTRATION",
        //                memberNo,
        //                currentCompanyCode,
        //                registration.InitialShares,
        //                memberNo,
        //                blockchainData
        //            );

        //            // Check if blockchain transaction was created successfully
        //            if (blockchainTx == null)
        //            {
        //                _logger.LogWarning("Blockchain transaction creation returned null");
        //                // Continue without blockchain - member is already saved
        //            }
        //            else
        //            {
        //                // Update member with blockchain transaction ID
        //                member.BlockchainTxId = blockchainTx.TransactionId;
        //                await _context.SaveChangesAsync();
        //                _logger.LogInformation($"Blockchain transaction ID saved: {blockchainTx.TransactionId}");
        //            }

        //            // Commit the transaction
        //            await transaction.CommitAsync();
        //            _logger.LogInformation($"Transaction committed successfully for member: {memberNo}");

        //            return new MemberResponseDTO
        //            {
        //                MemberNo = memberNo,
        //                FullName = $"{registration.Surname} {registration.OtherNames}",
        //                Status = "ACTIVE",
        //                RegistrationDate = DateTime.Now,
        //                BlockchainTxId = member.BlockchainTxId,
        //                ShareBalance = registration.InitialShares,
        //                Email = registration.Email,
        //                Phone = registration.PhoneNo,
        //                CompanyCode = currentCompanyCode
        //            };
        //        }
        //        catch (Exception blockchainEx)
        //        {
        //            _logger.LogError(blockchainEx, "Error with blockchain transaction, but member was saved to database");

        //            await transaction.CommitAsync();

        //            return new MemberResponseDTO
        //            {
        //                MemberNo = memberNo,
        //                FullName = $"{registration.Surname} {registration.OtherNames}",
        //                Status = "ACTIVE",
        //                RegistrationDate = DateTime.Now,
        //                BlockchainTxId = null,
        //                ShareBalance = registration.InitialShares,
        //                Email = registration.Email,
        //                Phone = registration.PhoneNo,
        //                CompanyCode = currentCompanyCode
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        try
        //        {
        //            await transaction.RollbackAsync();
        //            _logger.LogError(ex, "Transaction rolled back due to error");
        //        }
        //        catch (Exception rollbackEx)
        //        {
        //            _logger.LogError(rollbackEx, "Error rolling back transaction");
        //        }

        //        if (ex is ValidationException)
        //        {
        //            throw new Exception($"Validation error: {ex.Message}");
        //        }
        //        else if (ex is InvalidOperationException)
        //        {
        //            throw new Exception(ex.Message);
        //        }
        //        else if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE KEY constraint"))
        //        {
        //            throw new Exception("Member with this ID number already exists. Please use a different ID number.");
        //        }
        //        else if (ex.InnerException != null && ex.InnerException.Message.Contains("PRIMARY KEY"))
        //        {
        //            throw new Exception("Duplicate member number detected. Please try again or contact administrator.");
        //        }

        //        throw new Exception($"Error registering member: {ex.Message}");
        //    }
        //}

        //public async Task<Member> GetMemberByMemberNoAsync(string memberNo)
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    return await _context.Members
        //        .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);
        //}

        //public async Task<List<Member>> SearchMembersAsync(string searchTerm)
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    return await _context.Members
        //        .Where(m => m.CompanyCode == currentCompanyCode &&
        //                   (m.MemberNo.Contains(searchTerm) ||
        //                    m.Surname.Contains(searchTerm) ||
        //                    m.OtherNames.Contains(searchTerm) ||
        //                    m.Idno.Contains(searchTerm)))
        //        .Take(50)
        //        .ToListAsync();
        //}

        //public async Task<decimal> GetMemberShareBalanceAsync(string memberNo)
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    var share = await _context.Shares
        //        .FirstOrDefaultAsync(s => s.MemberNo == memberNo && s.CompanyCode == currentCompanyCode);

        //    return share?.TotalShares ?? 0;
        //}

        //public async Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember)
        //{
        //    var member = await GetMemberByMemberNoAsync(memberNo);
        //    if (member == null) return false;

        //    // Update editable fields
        //    member.Surname = updatedMember.Surname ?? member.Surname;
        //    member.OtherNames = updatedMember.OtherNames ?? member.OtherNames;
        //    member.PhoneNo = updatedMember.PhoneNo ?? member.PhoneNo;
        //    member.Email = updatedMember.Email ?? member.Email;
        //    member.Sex = updatedMember.Sex ?? member.Sex;
        //    member.Dob = updatedMember.Dob ?? member.Dob;

        //    // Update address and other fields if provided
        //    if (!string.IsNullOrEmpty(updatedMember.PresentAddr))
        //    {
        //        member.PresentAddr = updatedMember.PresentAddr;
        //    }

        //    if (!string.IsNullOrEmpty(updatedMember.Employer))
        //    {
        //        member.Employer = updatedMember.Employer;
        //    }

        //    if (!string.IsNullOrEmpty(updatedMember.Dept))
        //    {
        //        member.Dept = updatedMember.Dept;
        //    }

        //    // Update audit fields
        //    member.AuditId = updatedMember.AuditId ?? member.AuditId;
        //    member.AuditTime = DateTime.Now;
        //    member.AuditDateTime = DateTime.Now;

        //    // Create blockchain transaction for update
        //    var updateData = new
        //    {
        //        MemberNo = memberNo,
        //        UpdatedFields = new
        //        {
        //            Phone = updatedMember.PhoneNo,
        //            Email = updatedMember.Email,
        //            Address = updatedMember.PresentAddr,
        //            Surname = updatedMember.Surname,
        //            OtherNames = updatedMember.OtherNames
        //        },
        //        UpdateTime = DateTime.Now,
        //        UpdatedBy = member.AuditId
        //    };

        //    var blockchainTx = await _blockchainService.CreateTransaction(
        //        "MEMBER_UPDATE",
        //        memberNo,
        //        member.CompanyCode,
        //        0,
        //        memberNo,
        //        updateData
        //    );

        //    member.BlockchainTxId = blockchainTx.TransactionId;

        //    await _context.SaveChangesAsync();
        //    await _blockchainService.AddToBlockchain(blockchainTx);

        //    return true;
        //}

        //public async Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo)
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    return await _blockchainService.GetMemberTransactions(memberNo, currentCompanyCode);
        //}

        //public async Task<List<Member>> GetAllMembersAsync()
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    return await _context.Members
        //        .Where(m => m.CompanyCode == currentCompanyCode && m.Status == 1)
        //        .OrderBy(m => m.Surname)
        //        .ToListAsync();
        //}
        public async Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration)
        {
            _logger.LogInformation($"Starting member registration for: {registration.Surname} {registration.OtherNames}");

            // Age validation
            if (registration.DateOfBirth.HasValue)
            {
                var age = CalculateAge(registration.DateOfBirth.Value);
                if (age < 18)
                {
                    throw new ValidationException("Member must be at least 18 years old.");
                }
                registration.Age = age;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate required fields
                ValidateRegistration(registration);

                // Get company info
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == registration.CompanyCode);

                if (company == null)
                {
                    throw new ValidationException($"Company {registration.CompanyCode} not found.");
                }

                // Check for duplicate ID within the company
                var existingById = await _context.Members
                    .FirstOrDefaultAsync(m => m.Idno == registration.IdNo
                        && m.CompanyCode == registration.CompanyCode);

                if (existingById != null)
                {
                    throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists in this company.");
                }

                // Generate or use provided member number
                string memberNo;
                if (string.IsNullOrEmpty(registration.MemberNo))
                {
                    memberNo = await GenerateMemberNumberAsync(registration.CompanyCode);
                }
                else
                {
                    // Check if provided member number is available
                    var existingMemberNo = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == registration.MemberNo
                            && m.CompanyCode == registration.CompanyCode);

                    if (existingMemberNo != null)
                    {
                        throw new InvalidOperationException($"Member number {registration.MemberNo} already exists.");
                    }
                    memberNo = registration.MemberNo;
                }

                // Validate CIG if selected
                string? cigCode = null;
                if (!string.IsNullOrEmpty(registration.CigCode))
                {
                    var cig = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Cigcode == registration.CigCode
                            && c.CompanyCode == registration.CompanyCode);

                    if (cig == null)
                    {
                        throw new ValidationException($"Selected CIG/Group not found.");
                    }
                    cigCode = registration.CigCode;
                }

                // Get current user info
                var currentUserId = _companyContextService.GetCurrentUserId();
                var currentUserName = _companyContextService.GetCurrentUserName();

                // Create Member record
                var member = new Member
                {
                    MemberNo = memberNo,
                    Surname = registration.Surname,
                    OtherNames = registration.OtherNames,
                    Idno = registration.IdNo,
                    PhoneNo = registration.PhoneNo,
                    Email = registration.Email,
                    PresentAddr = registration.PresentAddr,
                    Dob = registration.DateOfBirth,
                    Age = registration.Age,
                    Sex = registration.Gender,
                    MembershipType = registration.MembershipType,
                    RegistrationType = registration.RegistrationType,
                    CompanyCode = registration.CompanyCode,
                    Cigcode = cigCode,
                    Employer = registration.Employer,
                    Dept = registration.Dept,
                    InitShares = registration.InitialShares,
                    RegFee = registration.RegFee,
                    ShareCap = registration.InitialShares,
                    Status = 1, // Active
                    Mstatus = registration.Mstatus,
                    ApplicDate = DateTime.Now,
                    EffectDate = DateTime.Now,
                    Posted = "Y",
                    AuditId = currentUserName,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                _logger.LogInformation($"Adding member to database: {memberNo} for company: {registration.CompanyCode}");
                _context.Members.Add(member);
                await _context.SaveChangesAsync();

                // Create blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        IDNo = registration.IdNo,
                        Phone = registration.PhoneNo,
                        Email = registration.Email,
                        MembershipType = registration.MembershipType,
                        RegistrationType = registration.RegistrationType,
                        CigCode = cigCode,
                        DateOfBirth = registration.DateOfBirth?.ToString("yyyy-MM-dd"),
                        Age = registration.Age,
                        Gender = registration.Gender,
                        InitialShares = registration.InitialShares,
                        CompanyCode = registration.CompanyCode,
                        CompanyName = company.CompanyName,
                        RegistrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        CreatedBy = currentUserName,
                        CreatedById = currentUserId
                    };

                    _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "MEMBER_REGISTRATION",
                        memberNo,
                        registration.CompanyCode,
                        registration.InitialShares,
                        memberNo,
                        blockchainData
                    );

                    if (blockchainTx != null)
                    {
                        member.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception blockchainEx)
                {
                    _logger.LogError(blockchainEx, "Error with blockchain transaction, but member was saved");
                    // Continue without failing the registration
                }

                await transaction.CommitAsync();
                _logger.LogInformation($"Member {memberNo} registered successfully");

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
                    CompanyCode = registration.CompanyCode,
                    MembershipType = registration.MembershipType,
                    RegistrationType = registration.RegistrationType
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error registering member");

                if (ex is ValidationException || ex is InvalidOperationException)
                {
                    throw;
                }

                throw new Exception($"Error registering member: {ex.Message}");
            }
        }

        private void ValidateRegistration(MemberRegistrationDTO registration)
        {
            if (string.IsNullOrEmpty(registration.Surname))
                throw new ValidationException("Surname is required.");

            if (string.IsNullOrEmpty(registration.OtherNames))
                throw new ValidationException("Other names are required.");

            if (string.IsNullOrEmpty(registration.IdNo))
                throw new ValidationException("ID Number is required.");

            if (string.IsNullOrEmpty(registration.PhoneNo))
                throw new ValidationException("Phone number is required.");

            if (string.IsNullOrEmpty(registration.MembershipType))
                throw new ValidationException("Membership type is required.");

            if (string.IsNullOrEmpty(registration.RegistrationType))
                throw new ValidationException("Registration type is required.");

            if (registration.MembershipType == "Corporate")
            {
                // Additional validation for corporate members
                if (string.IsNullOrEmpty(registration.Employer))
                {
                    throw new ValidationException("Employer/Company name is required for corporate members.");
                }
            }
        }

        private async Task<string> GenerateMemberNumberAsync(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode) || companyCode.Length < 2)
                throw new ArgumentException("Company code must have at least 2 characters.");

            // Take first 2 letters of companyCode
            var prefix = companyCode.Substring(0, 2).ToUpper();

            var lastMember = await _context.Members
                .Where(m => m.MemberNo.StartsWith(prefix))
                .OrderByDescending(m => m.MemberNo)
                .FirstOrDefaultAsync();

            if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
            {
                return $"{prefix}000000001";
            }

            // Extract numeric part (after first 2 letters)
            var numericPart = lastMember.MemberNo.Substring(2);

            if (long.TryParse(numericPart, out long lastNumber))
            {
                var nextNumber = lastNumber + 1;
                return $"{prefix}{nextNumber:000000000}";
            }

            // Fallback
            return $"{prefix}{DateTime.Now.Ticks.ToString().Substring(0, 9)}";
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        public async Task<MemberRegistrationVm> GetRegistrationViewModelAsync()
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
            var currentUser = _companyContextService.GetCurrentUserName();

            var viewModel = new MemberRegistrationVm
            {
                CompanyCode = currentCompanyCode,
                MembershipType = "Individual",
                RegistrationType = "Regular",
                Mstatus = true
            };

            // Load companies for dropdown (if needed)
            viewModel.Companies = await _context.Companies
                .Where(c => c.CompanyCode == currentCompanyCode)
                .Select(c => new CompanySelectItem
                {
                    CompanyCode = c.CompanyCode,
                    CompanyName = c.CompanyName ?? c.CompanyCode
                })
                .ToListAsync();

            // Load CIGs for the current company
            viewModel.CigList = await _context.Companies
                .Where(c => !string.IsNullOrEmpty(c.Cigcode)
                    && c.CompanyCode == currentCompanyCode)
                .Select(c => new CigSelectItem
                {
                    CigCode = c.Cigcode ?? string.Empty,
                    CigName = c.CompanyName ?? c.Cigcode ?? string.Empty,
                    CompanyCode = c.CompanyCode
                })
                .ToListAsync();

            // Auto-generate member number for display
            viewModel.MemberNo = await GenerateMemberNumberAsync(currentCompanyCode);

            return viewModel;
        }

        // Other interface implementations...
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
            var member = await GetMemberByMemberNoAsync(memberNo);
            if (member == null) return false;

            member.Surname = updatedMember.Surname ?? member.Surname;
            member.OtherNames = updatedMember.OtherNames ?? member.OtherNames;
            member.PhoneNo = updatedMember.PhoneNo ?? member.PhoneNo;
            member.Email = updatedMember.Email ?? member.Email;
            member.PresentAddr = updatedMember.PresentAddr ?? member.PresentAddr;
            member.Employer = updatedMember.Employer ?? member.Employer;
            member.Dept = updatedMember.Dept ?? member.Dept;
            member.Sex = updatedMember.Sex ?? member.Sex;
            member.Dob = updatedMember.Dob ?? member.Dob;

            member.AuditId = _companyContextService.GetCurrentUserName();
            member.AuditTime = DateTime.Now;
            member.AuditDateTime = DateTime.Now;

            await _context.SaveChangesAsync();
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
                .Where(m => m.CompanyCode == currentCompanyCode)
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


        public async Task<ContributionResponseDTO> AddContributionAsync(ContributionDTO contributionDto)
        {
            _logger.LogInformation($"Starting contribution addition for member: {contributionDto.MemberNo}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate member exists
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == contributionDto.MemberNo &&
                                             m.CompanyCode == contributionDto.CompanyCode);

                if (member == null)
                {
                    throw new ValidationException($"Member {contributionDto.MemberNo} not found");
                }

                // Validate share type exists
                var shareType = await _context.Sharetypes
                    .FirstOrDefaultAsync(st => st.SharesCode == contributionDto.SharesCode &&
                                              st.CompanyCode == contributionDto.CompanyCode);

                if (shareType == null)
                {
                    throw new ValidationException($"Share type {contributionDto.SharesCode} not found");
                }

                // Validate amount against share type limits
                if (contributionDto.Amount < shareType.MinAmount)
                {
                    throw new ValidationException($"Amount cannot be less than minimum of {shareType.MinAmount}");
                }

                if (shareType.MaxAmount.HasValue && contributionDto.Amount > shareType.MaxAmount.Value)
                {
                    throw new ValidationException($"Amount cannot exceed maximum of {shareType.MaxAmount}");
                }

                // Generate receipt number if not provided
                var receiptNo = contributionDto.ReceiptNo ??
                    GenerateReceiptNumber(contributionDto.CompanyCode);

                // Create Contrib record
                var contrib = new Contrib
                {
                    MemberNo = contributionDto.MemberNo,
                    ContrDate = contributionDto.TransactionDate,
                    Amount = contributionDto.Amount,
                    CompanyCode = contributionDto.CompanyCode,
                    ReceiptNo = receiptNo,
                    Remarks = contributionDto.Remarks,
                    AuditId = contributionDto.CreatedBy,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    Sharescode = contributionDto.SharesCode,
                    TransactionNo = Guid.NewGuid().ToString().Substring(0, 20),
                    Posted = "Y",
                    Locked = "N"
                };

                _context.Contribs.Add(contrib);

                // Create ContribShare record if this is a share capital contribution
                if (shareType.Issharecapital == 1)
                {
                    var contribShare = new ContribShare
                    {
                        MemberNo = contributionDto.MemberNo,
                        ContrDate = contributionDto.TransactionDate,
                        ShareCapitalAmount = contributionDto.Amount,
                        CompanyCode = contributionDto.CompanyCode,
                        ReceiptNo = receiptNo,
                        Remarks = contributionDto.Remarks,
                        AuditId = contributionDto.CreatedBy,
                        AuditTime = DateTime.Now,
                        AuditDateTime = DateTime.Now,
                        Sharescode = contributionDto.SharesCode,
                        TransactionNo = contrib.TransactionNo
                    };

                    _context.ContribShares.Add(contribShare);

                    // Update or create Share record
                    await UpdateShareBalanceAsync(contributionDto.MemberNo,
                        contributionDto.SharesCode,
                        contributionDto.Amount,
                        contributionDto.CompanyCode);
                }

                // Create blockchain transaction
                var blockchainData = new
                {
                    MemberNo = contributionDto.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    TransactionType = "CONTRIBUTION",
                    ShareType = shareType.SharesType,
                    Amount = contributionDto.Amount,
                    ReceiptNo = receiptNo,
                    TransactionDate = contributionDto.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    PaymentMethod = contributionDto.PaymentMethod,
                    ReferenceNo = contributionDto.ReferenceNo,
                    Remarks = contributionDto.Remarks,
                    CompanyCode = contributionDto.CompanyCode,
                    CreatedBy = contributionDto.CreatedBy
                };

                _logger.LogInformation($"Creating blockchain transaction for contribution: {receiptNo}");

                var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                    "CONTRIBUTION",
                    contributionDto.MemberNo,
                    contributionDto.CompanyCode,
                    contributionDto.Amount,
                    receiptNo,
                    blockchainData
                );

                // Update blockchain transaction ID
                if (blockchainTx != null)
                {
                    contrib.BlockchainTxId = blockchainTx.TransactionId;

                    var contribShare = await _context.ContribShares
                        .FirstOrDefaultAsync(cs => cs.TransactionNo == contrib.TransactionNo);
                    if (contribShare != null)
                    {
                        contribShare.BlockchainTxId = blockchainTx.TransactionId;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Contribution {receiptNo} added successfully for member {contributionDto.MemberNo}");

                // Get current share balance
                var shareBalance = await GetMemberShareBalanceAsync(contributionDto.MemberNo);

                return new ContributionResponseDTO
                {
                    Id = contrib.Id,
                    MemberNo = contributionDto.MemberNo,
                    MemberName = $"{member.Surname} {member.OtherNames}",
                    TransactionDate = contributionDto.TransactionDate,
                    SharesCode = contributionDto.SharesCode,
                    ShareTypeName = shareType.SharesType ?? shareType.SharesCode,
                    Amount = contributionDto.Amount,
                    TotalSharesAfter = shareBalance,
                    ReceiptNo = receiptNo,
                    Remarks = contributionDto.Remarks ?? string.Empty,
                    BlockchainTxId = contrib.BlockchainTxId ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    CreatedBy = contributionDto.CreatedBy,
                    CompanyCode = contributionDto.CompanyCode
                };
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

                throw new Exception($"Error adding contribution: {ex.Message}");
            }
        }

        private async Task UpdateShareBalanceAsync(string memberNo, string sharesCode, decimal amount, string companyCode)
        {
            // Find existing share record
            var existingShare = await _context.Shares
                .FirstOrDefaultAsync(s => s.MemberNo == memberNo &&
                                         s.Sharescode == sharesCode &&
                                         s.CompanyCode == companyCode);

            if (existingShare != null)
            {
                // Update existing share
                existingShare.TotalShares += amount;
                existingShare.TransDate = DateTime.Now;
                //existingShare.AuditId = User.Identity?.Name ?? "SYSTEM";
                existingShare.AuditTime = DateTime.Now;
                existingShare.AuditDateTime = DateTime.Now;
            }
            else
            {
                // Create new share record
                var newShare = new Share
                {
                    MemberNo = memberNo,
                    Sharescode = sharesCode,
                    TotalShares = amount,
                    Initshares = amount,
                    CompanyCode = companyCode,
                    TransDate = DateTime.Now,
                    //AuditId = cre ?? "SYSTEM",
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now
                };

                _context.Shares.Add(newShare);
            }
        }

        private string GenerateReceiptNumber(string companyCode)
        {
            var datePrefix = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random();
            var randomSuffix = random.Next(1000, 9999);
            return $"{companyCode}-CONT-{datePrefix}-{randomSuffix}";
        }

        public async Task<List<ContributionResponseDTO>> GetMemberContributionsAsync(string memberNo)
        {
            var contributions = await _context.Contribs
                .Include(c => c.SharescodeNavigation)
                .Where(c => c.MemberNo == memberNo)
                .OrderByDescending(c => c.ContrDate)
                .Take(100)
                .ToListAsync();

            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            return contributions.Select(c => new ContributionResponseDTO
            {
                Id = c.Id,
                MemberNo = c.MemberNo,
                MemberName = member != null ? $"{member.Surname} {member.OtherNames}" : c.MemberNo,
                TransactionDate = c.ContrDate ?? DateTime.MinValue,
                SharesCode = c.Sharescode ?? string.Empty,
                ShareTypeName = c.SharescodeNavigation?.SharesType ?? c.Sharescode ?? "Unknown",
                Amount = c.Amount ?? 0,
                ReceiptNo = c.ReceiptNo ?? string.Empty,
                Remarks = c.Remarks ?? string.Empty,
                BlockchainTxId = c.BlockchainTxId ?? string.Empty,
                CreatedAt = c.AuditTime,
                CreatedBy = c.AuditId ?? string.Empty,
                CompanyCode = c.CompanyCode ?? string.Empty
            }).ToList();
        }

        public async Task<List<ShareTypeDTO>> GetShareTypesAsync(string companyCode)
        {
            return await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .Select(st => new ShareTypeDTO
                {
                    SharesCode = st.SharesCode,
                    SharesType = st.SharesType ?? st.SharesCode,
                    SharesAcc = st.SharesAcc,
                    IsMainShares = st.IsMainShares,
                    UsedToGuarantee = st.UsedToGuarantee,
                    Withdrawable = st.Withdrawable,
                    MinAmount = st.MinAmount,
                    MaxAmount = st.MaxAmount ?? 0,
                    CompanyCode = st.CompanyCode ?? companyCode
                })
                .ToListAsync();
        }

        public async Task<MemberContributionHistoryDTO> GetMemberContributionHistoryAsync(string memberNo)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo);

            if (member == null)
            {
                throw new ValidationException($"Member {memberNo} not found");
            }

            var contributions = await GetMemberContributionsAsync(memberNo);
            var shareBalance = await GetMemberShareBalanceAsync(memberNo);

            return new MemberContributionHistoryDTO
            {
                MemberNo = memberNo,
                MemberName = $"{member.Surname} {member.OtherNames}",
                Contributions = contributions.Select(c => new ContributionDetailDTO
                {
                    TransactionDate = c.TransactionDate,
                    SharesCode = c.SharesCode,
                    ShareTypeName = c.ShareTypeName,
                    Amount = c.Amount,
                    ReceiptNo = c.ReceiptNo,
                    Remarks = c.Remarks,
                    BlockchainTxId = c.BlockchainTxId,
                    CreatedBy = c.CreatedBy
                }).ToList(),
                TotalContributions = contributions.Sum(c => c.Amount),
                CurrentShareBalance = shareBalance,
                CompanyCode = member.CompanyCode ?? string.Empty
            };
        }

        public async Task<List<ContributionResponseDTO>> SearchContributionsAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? memberNo = null,
            string? shareType = null)
        {
            var query = _context.Contribs
                .Include(c => c.SharescodeNavigation)
                .Include(c => c.MemberNoNavigation)
                .AsQueryable();

            // Apply filters
            if (fromDate.HasValue)
            {
                query = query.Where(c => c.ContrDate >= fromDate);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c => c.ContrDate <= toDate);
            }

            if (!string.IsNullOrEmpty(memberNo))
            {
                query = query.Where(c => c.MemberNo.Contains(memberNo));
            }

            if (!string.IsNullOrEmpty(shareType))
            {
                query = query.Where(c => c.Sharescode == shareType);
            }

            // Execute query
            var contributions = await query
                .OrderByDescending(c => c.ContrDate)
                .Take(200)
                .ToListAsync();

            return contributions.Select(c => new ContributionResponseDTO
            {
                Id = c.Id,
                MemberNo = c.MemberNo,
                MemberName = c.MemberNoNavigation != null ?
                $"{c.MemberNoNavigation.Surname} {c.MemberNoNavigation.OtherNames}" :
                c.MemberNo,
                TransactionDate = c.ContrDate ?? DateTime.MinValue,
                SharesCode = c.Sharescode ?? string.Empty,
                ShareTypeName = c.SharescodeNavigation?.SharesType ?? c.Sharescode ?? "Unknown",
                Amount = c.Amount ?? 0,
                ReceiptNo = c.ReceiptNo ?? string.Empty,
                Remarks = c.Remarks ?? string.Empty,
                BlockchainTxId = c.BlockchainTxId ?? string.Empty,
                CreatedAt = c.AuditTime,
                CreatedBy = c.AuditId ?? string.Empty,
                CompanyCode = c.CompanyCode ?? string.Empty
            }).ToList();
        }
        public Task<MemberDTO> GetMemberDetailsAsync(string memberNo)
        {
            throw new NotImplementedException();
        }
    }
}