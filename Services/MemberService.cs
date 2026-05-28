using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Services
{
    public class MemberService : IMemberService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<MemberService> _logger;
        private readonly ICompanyContextService _companyContextService;
        private readonly IHttpContextAccessor _httpContextAccesso;
        private readonly AuditTrailService _auditService;
        private readonly ICryptoService _cryptoService;
        private WalletService _walletService;
        // private readonly UserManager<IdentityUser> _userManager;

        public MemberService(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            ILogger<MemberService> logger,
            IHttpContextAccessor httpContextAccessor,
            AuditTrailService auditService,
            ICryptoService cryptoService,
            WalletService walletService,
            //UserManager<IdentityUser> userManager,
            ICompanyContextService companyContextService)
        {
            _context = context;
            _blockchainService = blockchainService;
            _httpContextAccessor = httpContextAccessor;
            _auditService = auditService;
            _walletService = walletService;
            _logger = logger;
            _cryptoService = cryptoService;
            //_userManager = userManager;
            _companyContextService = companyContextService;
        }

        public string GetCurrentCompanyCode()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null || !user.Identity.IsAuthenticated)
                {
                    _logger.LogWarning("No authenticated user found");
                    return null;
                }

                // Get company code from claims only
                var companyCodeClaim = user.FindFirst("CompanyCode")?.Value;
                if (!string.IsNullOrEmpty(companyCodeClaim))
                {
                    _logger.LogInformation($"Company code from claim: '{companyCodeClaim}'");
                    return companyCodeClaim.Trim();
                }

                _logger.LogWarning("No company code claim found for user");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current company code");
                return null;
            }
        }

        public string GetCurrentUserName()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }

        public ClaimsPrincipal GetCurrentUserPrincipal()
        {
            return _httpContextAccessor.HttpContext?.User;
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

                // AGE VALIDATION BASED ON MEMBERSHIP TYPE
                if (!string.IsNullOrEmpty(registration.MembershipType))
                {
                    if (registration.MembershipType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!registration.DateOfBirth.HasValue)
                        {
                            throw new ValidationException("Date of Birth is required for Individual members.");
                        }

                        var age = registration.Age ?? CalculateAge(registration.DateOfBirth.Value);

                        if (age < 18)
                        {
                            throw new ValidationException("Individual members must be 18 years or older.");
                        }
                    }
                }

                // Get current user's company code
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                _logger.LogInformation($"Using company code from current user context: {currentCompanyCode}");

                // Get company name for Employer field
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == currentCompanyCode);
                registration.Employer = company?.CompanyName ?? currentCompanyCode;

                // Check for duplicate ID number within the same company
                var existingById = await _context.Members
                    .FirstOrDefaultAsync(m => m.Idno == registration.IdNo && m.CompanyCode == currentCompanyCode);

                if (existingById != null)
                {
                    throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists in company {currentCompanyCode}.");
                }

                // Check for duplicate phone number if provided
                if (!string.IsNullOrEmpty(registration.PhoneNo))
                {
                    var existingByPhone = await _context.Members
                        .FirstOrDefaultAsync(m => m.PhoneNo == registration.PhoneNo && m.CompanyCode == currentCompanyCode);

                    if (existingByPhone != null)
                    {
                        throw new InvalidOperationException($"Phone number {registration.PhoneNo} is already registered to another member.");
                    }
                }

                // Check for duplicate email if provided
                if (!string.IsNullOrEmpty(registration.Email))
                {
                    var existingByEmail = await _context.Members
                        .FirstOrDefaultAsync(m => m.Email == registration.Email && m.CompanyCode == currentCompanyCode);

                    if (existingByEmail != null)
                    {
                        throw new InvalidOperationException($"Email {registration.Email} is already registered to another member.");
                    }
                }

                // Determine member number
                string memberNo;

                if (!string.IsNullOrEmpty(registration.MemberNo))
                {
                    var existingByMemberNo = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == registration.MemberNo && m.CompanyCode == currentCompanyCode);

                    if (existingByMemberNo == null)
                    {
                        memberNo = registration.MemberNo;
                        _logger.LogInformation($"Using member number from view: {memberNo}");
                    }
                    else
                    {
                        _logger.LogWarning($"Member number from view ({registration.MemberNo}) already exists. Generating new number.");
                        memberNo = await GenerateUniqueMemberNumberAsync(currentCompanyCode, registration);
                    }
                }
                else
                {
                    _logger.LogInformation("No member number provided in DTO. Auto-generating new number.");
                    memberNo = await GenerateUniqueMemberNumberAsync(currentCompanyCode, registration);
                }

                // Validate CIG group if provided
                if (!string.IsNullOrEmpty(registration.Cigcode))
                {
                    var cigExists = await _context.CIGs
                        .AnyAsync(c => c.GigCode == registration.Cigcode && c.CompanyCode == currentCompanyCode);

                    if (!cigExists)
                    {
                        throw new ValidationException($"Selected CIG group {registration.Cigcode} is not valid for this company.");
                    }
                }

                // Get current user info
                var currentUserId = _companyContextService.GetCurrentUserId();
                var currentUserName = _companyContextService.GetCurrentUserName();

                // Create Member record with all fields from DTO
                var member = new Member
                {
                    MemberNo = memberNo,
                    Surname = registration.Surname,
                    OtherNames = registration.OtherNames,
                    FullName = $"{registration.Surname} {registration.OtherNames}".Trim(),
                    Idno = registration.IdNo,
                    PhoneNo = registration.PhoneNo,
                    HomeTelNo = registration.LandLine,
                    Email = registration.Email,
                    EmailAddress = registration.Email,
                    Sex = registration.Gender,
                    Dob = registration.DateOfBirth,
                    Age = registration.Age ?? (registration.DateOfBirth.HasValue ? CalculateAge(registration.DateOfBirth.Value) : (int?)null),
                    Station = registration.Station,
                    Dept = registration.Department,
                    PresentAddr = registration.PresentAddress,
                    Employer = registration.Employer,
                    CompanyCode = currentCompanyCode,
                    Cigcode = registration.Cigcode ?? currentCompanyCode,
                    MembershipType = registration.MembershipType,
                    MemberDescription = registration.RegistrationType,
                    ShareCap = registration.InitialShares,
                    InitShares = registration.InitialShares,
                    LoanBalance = 0,
                    InterestBalance = 0,
                    Status = 1,  // Active
                    Mstatus = true,
                    Archived = false,
                    Withdrawn = false,
                    Dormant = 0,
                    ApplicDate = registration.RegistrationDate,
                    EffectDate = DateTime.Now,
                    AsAtDate = DateTime.Now,
                    EDate = DateTime.Now,
                    Posted = "Y",
                    AuditId = currentUserName,
                    AuditTime = DateTime.Now,
                    AuditDateTime = DateTime.Now,
                    BlockchainTxId = null
                };

                _logger.LogInformation($"Adding member to database with MemberNo: {memberNo}");
                _context.Members.Add(member);

                // IMPORTANT: Save changes and capture the result
                var saveResult = await _context.SaveChangesAsync();
                _logger.LogInformation($"SaveChangesAsync returned {saveResult} rows affected. Member ID: {member.Id}, MemberNo: {member.MemberNo}");

                // CRITICAL FIX: Check if SaveChangesAsync affected any rows
                if (saveResult <= 0)
                {
                    _logger.LogError("SaveChangesAsync returned 0 rows affected. Member was not saved!");
                    throw new Exception("Failed to save member properly - no rows affected");
                }

                // CRITICAL FIX: After SaveChangesAsync, refresh the entity to ensure we have the generated ID
                if (member.Id == 0)
                {
                    _logger.LogWarning("Member Id is 0 after SaveChanges. Attempting to refresh entity...");

                    // Try to reload the entity from database
                    await _context.Entry(member).ReloadAsync();

                    // If still 0, query for it directly
                    if (member.Id == 0)
                    {
                        var savedMember = await _context.Members
                            .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                        if (savedMember != null)
                        {
                            member = savedMember;
                            _logger.LogInformation($"Found member via query with ID: {member.Id}");
                        }
                        else
                        {
                            _logger.LogError($"Cannot find member with MemberNo: {memberNo} after save!");
                            throw new Exception("Failed to save member properly - cannot retrieve saved member");
                        }
                    }
                }

                _logger.LogInformation($"Member verified with ID: {member.Id}, MemberNo: {member.MemberNo}");

                // Create wallet for the new member (ONLY ONCE)
                try
                {
                    _logger.LogInformation($"Creating wallet for member ID={member.Id}, MemberNo={member.MemberNo}");
                    var walletResult = await _cryptoService.CreateWalletForMemberAsync(member.Id, member.MemberNo, currentCompanyCode);

                    if (walletResult.Success)
                    {
                        _logger.LogInformation($"Wallet created for new member {memberNo}: {walletResult.WalletAddress}");
                        // Refresh member to get updated WalletAddress
                        await _context.Entry(member).ReloadAsync();
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to create wallet for member {memberNo}: {walletResult.Message}");
                    }
                }
                catch (Exception walletEx)
                {
                    _logger.LogError(walletEx, $"Error creating wallet for member {memberNo}");
                    // Don't throw - wallet creation shouldn't block member registration
                }

                // Create blockchain transaction
                try
                {
                    var blockchainData = new
                    {
                        MemberNo = memberNo,
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        IDNo = registration.IdNo,
                        Phone = registration.PhoneNo,
                        LandLine = registration.LandLine,
                        Email = registration.Email,
                        DateOfBirth = registration.DateOfBirth?.ToString("yyyy-MM-dd"),
                        Age = registration.Age,
                        Gender = registration.Gender,
                        Employer = registration.Employer,
                        Station = registration.Station,
                        Department = registration.Department,
                        PresentAddress = registration.PresentAddress,
                        CompanyCode = currentCompanyCode,
                        GroupCig = registration.Cigcode,
                        MembershipType = registration.MembershipType,
                        RegistrationType = registration.RegistrationType,
                        InitialShares = registration.InitialShares,
                        RegistrationDate = registration.RegistrationDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        CreatedBy = currentUserName,
                        CreatedById = currentUserId,
                        Status = "ACTIVE"
                    };

                    _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "MEMBER_REGISTRATION",
                        memberNo,
                        currentCompanyCode,
                        registration.InitialShares,
                        memberNo,
                        blockchainData
                    );

                    if (blockchainTx == null)
                    {
                        _logger.LogWarning("Blockchain transaction creation returned null");
                    }
                    else
                    {
                        member.BlockchainTxId = blockchainTx.TransactionId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain transaction ID saved: {blockchainTx.TransactionId}");
                    }
                    WalletConfig walletConfig = await _walletService.GetConfigurations(company.CompanyCode);
                    if (walletConfig.EnableWallets == true)
                    {
                        if(walletConfig.AutoAssignWalletOnRegistration == true)
                        {
                            await _walletService.RegisterMemberAsync(member);
                        }
                    }

                    await transaction.CommitAsync();
                    _logger.LogInformation($"Transaction committed successfully for member: {memberNo}");

                    return new MemberResponseDTO
                    {
                        MemberNo = memberNo, // Return the SAME member number
                        FullName = $"{registration.Surname} {registration.OtherNames}",
                        Status = "ACTIVE",
                        RegistrationDate = registration.RegistrationDate,
                        BlockchainTxId = member.BlockchainTxId,
                        ShareBalance = registration.InitialShares,
                        Email = registration.Email,
                        Phone = registration.PhoneNo,
                        CompanyCode = currentCompanyCode,
                        MembershipType = registration.MembershipType,
                        RegistrationType = registration.RegistrationType
                    };
                }
                catch (Exception blockchainEx)
                {
                    _logger.LogError(blockchainEx, "Error with blockchain transaction, but member was saved to database");
                    // Don't throw - blockchain failure shouldn't rollback member registration
                }

                await transaction.CommitAsync();
                _logger.LogInformation($"Transaction committed successfully for member: {memberNo}");

                return new MemberResponseDTO
                {
                    MemberNo = memberNo,
                    FullName = $"{registration.Surname} {registration.OtherNames}",
                    Status = "ACTIVE",
                    RegistrationDate = registration.RegistrationDate,
                    BlockchainTxId = member.BlockchainTxId,
                    ShareBalance = registration.InitialShares,
                    Email = registration.Email,
                    Phone = registration.PhoneNo,
                    CompanyCode = currentCompanyCode,
                    MembershipType = registration.MembershipType,
                    RegistrationType = registration.RegistrationType
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

                // Handle specific exceptions
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
                    if (ex.InnerException.Message.Contains("IX_Members_Idno"))
                        throw new Exception("Member with this ID number already exists. Please use a different ID number.");
                    else if (ex.InnerException.Message.Contains("IX_Members_PhoneNo"))
                        throw new Exception("Phone number is already registered to another member.");
                    else if (ex.InnerException.Message.Contains("IX_Members_Email"))
                        throw new Exception("Email address is already registered to another member.");
                    else
                        throw new Exception("A member with this information already exists.");
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

        //        // =====================================================
        //        // AGE VALIDATION BASED ON MEMBERSHIP TYPE
        //        // =====================================================
        //        if (!string.IsNullOrEmpty(registration.MembershipType))
        //        {
        //            if (registration.MembershipType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (!registration.DateOfBirth.HasValue)
        //                {
        //                    throw new ValidationException("Date of Birth is required for Individual members.");
        //                }

        //                var age = registration.Age ??
        //                          CalculateAge(registration.DateOfBirth.Value);

        //                if (age < 18)
        //                {
        //                    throw new ValidationException("Individual members must be 18 years or older.");
        //                }
        //            }
        //            // Corporate → no restriction (allow any or null age)
        //        }

        //        // Get current user's company code
        //        var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
        //        _logger.LogInformation($"Using company code from current user context: {currentCompanyCode}");

        //        // Get company name for Employer field (MOVED HERE - AFTER currentCompanyCode is declared)
        //        var company = await _context.Companies
        //            .FirstOrDefaultAsync(c => c.CompanyCode == currentCompanyCode);
        //        registration.Employer = company?.CompanyName ?? currentCompanyCode;

        //        // Check for duplicate ID number within the same company
        //        var existingById = await _context.Members
        //            .FirstOrDefaultAsync(m => m.Idno == registration.IdNo && m.CompanyCode == currentCompanyCode);

        //        if (existingById != null)
        //        {
        //            throw new InvalidOperationException($"A member with ID number {registration.IdNo} already exists in company {currentCompanyCode}.");
        //        }

        //        // Check for duplicate phone number if provided
        //        if (!string.IsNullOrEmpty(registration.PhoneNo))
        //        {
        //            var existingByPhone = await _context.Members
        //                .FirstOrDefaultAsync(m => m.PhoneNo == registration.PhoneNo && m.CompanyCode == currentCompanyCode);

        //            if (existingByPhone != null)
        //            {
        //                throw new InvalidOperationException($"Phone number {registration.PhoneNo} is already registered to another member.");
        //            }
        //        }

        //        // Check for duplicate email if provided
        //        if (!string.IsNullOrEmpty(registration.Email))
        //        {
        //            var existingByEmail = await _context.Members
        //                .FirstOrDefaultAsync(m => m.Email == registration.Email && m.CompanyCode == currentCompanyCode);

        //            if (existingByEmail != null)
        //            {
        //                throw new InvalidOperationException($"Email {registration.Email} is already registered to another member.");
        //            }
        //        }

        //        // Determine member number - PRIORITIZE the one from the DTO
        //        string memberNo;

        //        // FIRST: Check if user provided a member number via the DTO (from the view)
        //        if (!string.IsNullOrEmpty(registration.MemberNo))
        //        {
        //            // Check if the provided member number is available
        //            var existingByMemberNo = await _context.Members
        //                .FirstOrDefaultAsync(m => m.MemberNo == registration.MemberNo && m.CompanyCode == currentCompanyCode);

        //            if (existingByMemberNo == null)
        //            {
        //                // Use the member number from the view/DTO
        //                memberNo = registration.MemberNo;
        //                _logger.LogInformation($"Using member number from view: {memberNo}");
        //            }
        //            else
        //            {
        //                // The number from view already exists - generate a new one
        //                _logger.LogWarning($"Member number from view ({registration.MemberNo}) already exists. Generating new number.");
        //                memberNo = await GenerateUniqueMemberNumberAsync(currentCompanyCode, registration);
        //            }
        //        }
        //        else
        //        {
        //            // No member number provided - auto-generate
        //            _logger.LogInformation("No member number provided in DTO. Auto-generating new number.");
        //            memberNo = await GenerateUniqueMemberNumberAsync(currentCompanyCode, registration);
        //        }

        //        // Validate CIG group if provided
        //        if (!string.IsNullOrEmpty(registration.Cigcode))
        //        {
        //            var cigExists = await _context.CIGs
        //                .AnyAsync(c => c.GigCode == registration.Cigcode && c.CompanyCode == currentCompanyCode);

        //            if (!cigExists)
        //            {
        //                throw new ValidationException($"Selected CIG group {registration.Cigcode} is not valid for this company.");
        //            }
        //        }

        //        // Get current user info
        //        var currentUserId = _companyContextService.GetCurrentUserId();
        //        var currentUserName = _companyContextService.GetCurrentUserName();

        //        // Create Member record with all fields from DTO
        //        var member = new Member
        //        {
        //            // Core Fields
        //            MemberNo = memberNo, // USE THE DETERMINED member number
        //            Surname = registration.Surname,
        //            OtherNames = registration.OtherNames,
        //            FullName = $"{registration.Surname} {registration.OtherNames}".Trim(),

        //            // Identification
        //            Idno = registration.IdNo,

        //            // Contact Information
        //            PhoneNo = registration.PhoneNo,
        //            HomeTelNo = registration.LandLine,
        //            Email = registration.Email,
        //            EmailAddress = registration.Email,

        //            // Personal Details
        //            Sex = registration.Gender,
        //            Dob = registration.DateOfBirth,
        //            Age = registration.Age ?? (registration.DateOfBirth.HasValue ?
        //                  CalculateAge(registration.DateOfBirth.Value) : (int?)null),

        //            // Employment & Location
        //            Station = registration.Station,
        //            Dept = registration.Department,
        //            PresentAddr = registration.PresentAddress,
        //            Employer = registration.Employer,

        //            // Company Information
        //            CompanyCode = currentCompanyCode,
        //            Cigcode = registration.Cigcode ?? currentCompanyCode,

        //            // Membership Details
        //            MembershipType = registration.MembershipType,
        //            MemberDescription = registration.RegistrationType,

        //            // Financial Information
        //            ShareCap = registration.InitialShares,
        //            InitShares = registration.InitialShares,
        //            LoanBalance = 0,
        //            InterestBalance = 0,

        //            // Status Flags
        //            Status = 1,
        //            Mstatus = true,
        //            Archived = false,
        //            Withdrawn = false,
        //            Dormant = 0,

        //            // Dates
        //            ApplicDate = registration.RegistrationDate,
        //            EffectDate = DateTime.Now,
        //            AsAtDate = DateTime.Now,
        //            EDate = DateTime.Now,

        //            // Audit Fields
        //            Posted = "Y",
        //            AuditId = currentUserName,
        //            AuditTime = DateTime.Now,
        //            AuditDateTime = DateTime.Now,

        //            // Blockchain
        //            BlockchainTxId = null
        //        };

        //        _logger.LogInformation($"Adding member to database with MemberNo: {memberNo} (from view: {registration.MemberNo}) for company: {currentCompanyCode}");
        //        _context.Members.Add(member);
        //        await _context.SaveChangesAsync();
        //        _logger.LogInformation($"Member saved to database successfully with MemberNo: {member.MemberNo}");


        //        // CRITICAL: Verify member.Id is not 0
        //        _logger.LogInformation($"Member saved with ID: {member.Id}, MemberNo: {member.MemberNo}");

        //        if (member.Id == 0)
        //        {
        //            _logger.LogError("Member Id is 0 after SaveChanges! This should not happen.");
        //            throw new Exception("Failed to save member properly");
        //        }

        //        // Create wallet for the new member
        //        try
        //        {
        //            _logger.LogInformation($"Calling CreateWalletForMemberAsync with ID={member.Id}, MemberNo={member.MemberNo}, Company={currentCompanyCode}");
        //            var walletResult = await _cryptoService.CreateWalletForMemberAsync(member.Id, member.MemberNo, currentCompanyCode);
        //            if (walletResult.Success)
        //            {
        //                _logger.LogInformation($"Wallet created for new member {memberNo}: {walletResult.WalletAddress}");

        //                // Refresh member to get updated WalletAddress
        //                await _context.Entry(member).ReloadAsync();
        //            }
        //            else
        //            {
        //                _logger.LogWarning($"Failed to create wallet for member {memberNo}: {walletResult.Message}");
        //            }
        //        }
        //        catch (Exception walletEx)
        //        {
        //            _logger.LogError(walletEx, $"Error creating wallet for member {memberNo}");
        //        }
        //        try
        //        {
        //            var blockchainData = new
        //            {
        //                MemberNo = memberNo,
        //                FullName = $"{registration.Surname} {registration.OtherNames}",
        //                IDNo = registration.IdNo,
        //                Phone = registration.PhoneNo,
        //                LandLine = registration.LandLine,
        //                Email = registration.Email,
        //                DateOfBirth = registration.DateOfBirth?.ToString("yyyy-MM-dd"),
        //                Age = registration.Age,
        //                Gender = registration.Gender,
        //                Employer = registration.Employer,
        //                Station = registration.Station,
        //                Department = registration.Department,
        //                PresentAddress = registration.PresentAddress,
        //                CompanyCode = currentCompanyCode,
        //                GroupCig = registration.Cigcode,
        //                MembershipType = registration.MembershipType,
        //                RegistrationType = registration.RegistrationType,
        //                InitialShares = registration.InitialShares,
        //                RegistrationDate = registration.RegistrationDate.ToString("yyyy-MM-dd HH:mm:ss"),
        //                CreatedBy = currentUserName,
        //                CreatedById = currentUserId,
        //                Status = "ACTIVE"
        //            };

        //            _logger.LogInformation($"Creating blockchain transaction for member: {memberNo}");

        //            var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
        //                "MEMBER_REGISTRATION",
        //                memberNo,
        //                currentCompanyCode,
        //                registration.InitialShares,
        //                memberNo,
        //                blockchainData
        //            );

        //            if (blockchainTx == null)
        //            {
        //                _logger.LogWarning("Blockchain transaction creation returned null");
        //            }
        //            else
        //            {
        //                member.BlockchainTxId = blockchainTx.TransactionId;
        //                await _context.SaveChangesAsync();
        //                _logger.LogInformation($"Blockchain transaction ID saved: {blockchainTx.TransactionId}");
        //            }

        //            await transaction.CommitAsync();
        //            _logger.LogInformation($"Transaction committed successfully for member: {memberNo}");

        //            return new MemberResponseDTO
        //            {
        //                MemberNo = memberNo, // Return the SAME member number
        //                FullName = $"{registration.Surname} {registration.OtherNames}",
        //                Status = "ACTIVE",
        //                RegistrationDate = registration.RegistrationDate,
        //                BlockchainTxId = member.BlockchainTxId,
        //                ShareBalance = registration.InitialShares,
        //                Email = registration.Email,
        //                Phone = registration.PhoneNo,
        //                CompanyCode = currentCompanyCode,
        //                MembershipType = registration.MembershipType,
        //                RegistrationType = registration.RegistrationType
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
        //                RegistrationDate = registration.RegistrationDate,
        //                BlockchainTxId = null,
        //                ShareBalance = registration.InitialShares,
        //                Email = registration.Email,
        //                Phone = registration.PhoneNo,
        //                CompanyCode = currentCompanyCode,
        //                MembershipType = registration.MembershipType,
        //                RegistrationType = registration.RegistrationType
        //            };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Rollback and error handling remains the same...
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
        //            if (ex.InnerException.Message.Contains("IX_Members_Idno"))
        //                throw new Exception("Member with this ID number already exists. Please use a different ID number.");
        //            else if (ex.InnerException.Message.Contains("IX_Members_PhoneNo"))
        //                throw new Exception("Phone number is already registered to another member.");
        //            else if (ex.InnerException.Message.Contains("IX_Members_Email"))
        //                throw new Exception("Email address is already registered to another member.");
        //            else
        //                throw new Exception("A member with this information already exists.");
        //        }
        //        else if (ex.InnerException != null && ex.InnerException.Message.Contains("PRIMARY KEY"))
        //        {
        //            throw new Exception("Duplicate member number detected. Please try again or contact administrator.");
        //        }

        //        throw new Exception($"Error registering member: {ex.Message}");
        //    }
        //}

        public async Task<MemberResponseDTO> UpdateMemberAsync(string memberNo, MemberUpdateDTO updateDto)
        {
            _logger.LogInformation($"Starting member update for: {memberNo}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var currentUserName = _companyContextService.GetCurrentUserName() ?? "SYSTEM";

                // Find the existing member
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (member == null)
                {
                    throw new InvalidOperationException($"Member {memberNo} not found.");
                }

                // =====================================================
                // AGE VALIDATION FOR UPDATE
                // =====================================================
                if (updateDto.DateOfBirth.HasValue || !string.IsNullOrEmpty(updateDto.MembershipType))
                {
                    string membershipType = updateDto.MembershipType ?? member.MembershipType;
                    DateTime? dob = updateDto.DateOfBirth.HasValue ? updateDto.DateOfBirth : member.Dob;

                    if (!string.IsNullOrEmpty(membershipType) && membershipType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!dob.HasValue || dob.Value == default(DateTime))
                        {
                            throw new ValidationException("Date of Birth is required for Individual members.");
                        }

                        int age = CalculateAge(dob.Value);
                        if (age < 18)
                        {
                            throw new ValidationException("Individual members must be 18 years or older.");
                        }

                        // Update age if dob was changed
                        if (updateDto.DateOfBirth.HasValue)
                        {
                            member.Age = age;
                        }
                    }
                }

                // Store old values for blockchain record
                var oldValues = new
                {
                    member.Idno,
                    member.ApplicDate,
                    member.Surname,
                    member.OtherNames,
                    member.PhoneNo,
                    member.HomeTelNo,
                    member.Email,
                    member.Sex,
                    member.Dob,
                    member.Station,
                    member.Dept,
                    member.PresentAddr,
                    member.Cigcode,
                    member.MembershipType,
                    member.MemberDescription,
                    member.Status,
                    member.Mstatus,
                    member.Employer  // Add employer to old values
                };

                // Update ID Number if provided and different
                if (!string.IsNullOrEmpty(updateDto.IdNo) && updateDto.IdNo != member.Idno)
                {
                    // Check if the new ID number already exists for another member
                    var existingWithId = await _context.Members
                        .FirstOrDefaultAsync(m => m.Idno == updateDto.IdNo &&
                                                 m.CompanyCode == currentCompanyCode &&
                                                 m.MemberNo != memberNo);

                    if (existingWithId != null)
                    {
                        throw new InvalidOperationException($"ID Number {updateDto.IdNo} is already registered to another member.");
                    }

                    member.Idno = updateDto.IdNo;
                }

                // Update Registration Date if provided - FIXED: RegistrationDate is DateTime, not nullable
                // Check if it's a valid date (not default/min value)
                if (updateDto.RegistrationDate != default(DateTime) && updateDto.RegistrationDate != DateTime.MinValue)
                {
                    member.ApplicDate = updateDto.RegistrationDate;
                }

                // Update employer if provided
                if (!string.IsNullOrEmpty(updateDto.Employer))
                {
                    member.Employer = updateDto.Employer;
                }

                // Update other fields
                if (!string.IsNullOrEmpty(updateDto.Surname))
                    member.Surname = updateDto.Surname;

                if (!string.IsNullOrEmpty(updateDto.OtherNames))
                    member.OtherNames = updateDto.OtherNames;

                member.FullName = $"{member.Surname} {member.OtherNames}".Trim();

                if (!string.IsNullOrEmpty(updateDto.PhoneNo))
                    member.PhoneNo = updateDto.PhoneNo;

                if (updateDto.LandLine != null)
                    member.HomeTelNo = updateDto.LandLine;

                if (updateDto.Email != null)
                {
                    member.Email = updateDto.Email;
                    member.EmailAddress = updateDto.Email;
                }

                if (updateDto.Gender != null)
                    member.Sex = updateDto.Gender;

                if (updateDto.DateOfBirth.HasValue)
                {
                    member.Dob = updateDto.DateOfBirth;
                    member.Age = CalculateAge(updateDto.DateOfBirth.Value);
                }

                if (updateDto.Station != null)
                    member.Station = updateDto.Station;

                if (updateDto.Department != null)
                    member.Dept = updateDto.Department;

                if (updateDto.PresentAddress != null)
                    member.PresentAddr = updateDto.PresentAddress;

                if (updateDto.Cigcode != null)
                    member.Cigcode = updateDto.Cigcode;

                if (updateDto.MembershipType != null)
                    member.MembershipType = updateDto.MembershipType;

                if (updateDto.RegistrationType != null)
                    member.MemberDescription = updateDto.RegistrationType;

                // Update marital status
                if (!string.IsNullOrEmpty(updateDto.MaritalStatus))
                {
                    member.Mstatus = updateDto.MaritalStatus.ToUpper() == "MARRIED" ? true :
                                     updateDto.MaritalStatus.ToUpper() == "SINGLE" ? false : member.Mstatus;
                }

                // Update status
                if (!string.IsNullOrEmpty(updateDto.Status))
                {
                    member.Status = updateDto.Status switch
                    {
                        "Active" => 1,
                        "Withdrawn" => 2,
                        "Deceased" => 3,
                        "Dormant" => 4,
                        "Suspended" => 5,
                        _ => member.Status
                    };
                }

                // Update audit fields
                member.AuditId = currentUserName;
                member.AuditTime = DateTime.Now;
                member.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Member {memberNo} updated in database");

                // Create blockchain transaction for the update
                string blockchainTxId = null;
                try
                {
                    var blockchainData = new
                    {
                        MemberNo = memberNo,
                        Action = "UPDATE",
                        OldValues = oldValues,
                        NewValues = new
                        {
                            member.Idno,
                            member.ApplicDate,
                            member.Surname,
                            member.OtherNames,
                            member.PhoneNo,
                            member.HomeTelNo,
                            member.Email,
                            member.Sex,
                            member.Dob,
                            member.Station,
                            member.Dept,
                            member.PresentAddr,
                            member.Cigcode,
                            member.MembershipType,
                            member.MemberDescription,
                            member.Status,
                            member.Mstatus,
                            member.Employer 
                        },
                        UpdatedBy = currentUserName,
                        UpdatedAt = DateTime.Now,
                        UpdateType = "MEMBER_UPDATE"
                    };

                    var blockchainTx = await _blockchainService.CreateAndAddTransactionAsync(
                        "MEMBER_UPDATE",
                        memberNo,
                        currentCompanyCode,
                        0, // No amount for update
                        memberNo,
                        blockchainData
                    );

                    if (blockchainTx != null)
                    {
                        blockchainTxId = blockchainTx.TransactionId;
                        member.BlockchainTxId = blockchainTxId;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Blockchain update transaction recorded: {blockchainTxId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording blockchain update transaction, but member was updated");
                    // Don't throw - member update succeeded, blockchain recording failed
                }

                await transaction.CommitAsync();

                // Return response DTO
                return new MemberResponseDTO
                {
                    MemberNo = member.MemberNo,
                    FullName = $"{member.Surname} {member.OtherNames}".Trim(),
                    Status = member.Status switch
                    {
                        1 => "Active",
                        2 => "Withdrawn",
                        3 => "Deceased",
                        4 => "Dormant",
                        5 => "Suspended",
                        _ => "Unknown"
                    },
                    RegistrationDate = member.ApplicDate ?? DateTime.Now,
                    BlockchainTxId = blockchainTxId ?? member.BlockchainTxId,
                    ShareBalance = member.ShareCap ?? 0,
                    Email = member.Email,
                    Phone = member.PhoneNo,
                    CompanyCode = member.CompanyCode,
                    MembershipType = member.MembershipType,
                    RegistrationType = member.MemberDescription,
                    IsActive = member.Status == 1,
                    Employer = member.Employer  
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating member {memberNo}");
                throw new Exception($"Error updating member: {ex.Message}");
            }
        }

        private async Task<string> GenerateUniqueMemberNumberAsync(string companyCode, MemberRegistrationDTO registration)
        {
            string memberNo = string.Empty;
            bool isUnique = false;
            int attempts = 0;
            const int maxAttempts = 5;

            do
            {
                attempts++;

                // Get month initial (first letter of current month in uppercase)
                string monthInitial = DateTime.Now.ToString("MMM").Substring(0, 1).ToUpper();

                // Generate 11 unique digits: yyMMddHHmmss (12 digits) but we'll take last 11 or generate differently
                // Option 1: Use timestamp (yyMMddHHmmss) - 12 digits, take last 11
                string timestamp = DateTime.Now.ToString("yyMMddHHmmss");
                string uniqueDigits = timestamp.Length > 11 ? timestamp.Substring(timestamp.Length - 11) : timestamp.PadLeft(11, '0');

                // Alternative Option 2: Use combination of date + random for more uniqueness
                // string datePart = DateTime.Now.ToString("yyMMdd"); // 6 digits
                // string randomPart = new Random().Next(10000, 99999).ToString(); // 5 digits
                // string uniqueDigits = $"{datePart}{randomPart}"; // 11 digits

                // Combine: Month Initial + 11 digits = 12 characters total
                memberNo = $"{monthInitial}{uniqueDigits}";

                // Ensure exact length (should be 12 characters)
                if (memberNo.Length > 12)
                {
                    memberNo = memberNo.Substring(0, 12);
                }
                else if (memberNo.Length < 12)
                {
                    // Pad with random numbers if too short
                    Random rand = new Random();
                    memberNo = memberNo.PadRight(12, (char)('0' + rand.Next(0, 9)));
                }

                // Check uniqueness
                var existing = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                isUnique = existing == null;

                if (!isUnique && attempts < maxAttempts)
                {
                    // Small delay to ensure different timestamp
                    await Task.Delay(100);
                }

            } while (!isUnique && attempts < maxAttempts);

            // If still not unique after max attempts
            if (!isUnique)
            {
                // Use GUID for guaranteed uniqueness
                string monthInitial = DateTime.Now.ToString("MMM").Substring(0, 1).ToUpper();
                string guidDigits = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 11);
                memberNo = $"{monthInitial}{guidDigits}";

                // Final check
                var finalCheck = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (finalCheck != null)
                {
                    // Last resort: add random suffix
                    Random random = new Random();
                    string suffix = random.Next(100, 999).ToString();
                    memberNo = $"{memberNo.Substring(0, Math.Min(9, memberNo.Length))}{suffix}";
                }
            }

            _logger.LogInformation($"Generated member number: {memberNo} after {attempts} attempt(s)");
            return memberNo;
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
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

        /// <summary>
        /// Gets the total contributions for a specific member and share type based on the share type configuration
        /// </summary>
        public async Task<decimal> GetMemberShareTypeTotalAsync(string memberNo, string shareTypeCode, string companyCode)
        {
            var contribShare = await _context.ContribShares
                .FirstOrDefaultAsync(cs => cs.MemberNo == memberNo &&
                                           cs.Sharescode == shareTypeCode &&
                                           cs.CompanyCode == companyCode);

            if (contribShare == null)
            {
                return 0;
            }

            // Get share type details
            var shareType = await _context.Sharetypes
                .FirstOrDefaultAsync(st => st.SharesCode == shareTypeCode && st.CompanyCode == companyCode);

            if (shareType == null)
            {
                return 0;
            }

            // Determine which column to return based on share type characteristics
            string shareTypeName = (shareType.SharesType ?? shareType.SharesCode ?? "").ToLower();

            // Check for REGISTRATION FEE first
            if (shareTypeName.Contains("reg") ||
                shareTypeName.Contains("fee") ||
                shareTypeName.Contains("registration") ||
                (shareType.Issharecapital == 0 && shareType.UsedToGuarantee == false && shareType.UsedToOffset == false && shareType.Withdrawable == false))
            {
                return contribShare.RegFeeAmount ?? 0;
            }
            // Check for DEPOSIT/SAVINGS
            else if (shareTypeName.Contains("deposit") ||
                     shareTypeName.Contains("savings") ||
                     shareTypeName.Contains("saving") ||
                     (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true)))
            {
                return contribShare.DepositsAmount ?? 0;
            }
            // Check for DONOR
            else if (shareTypeName.Contains("donor") || shareTypeName.Contains("donation") || shareTypeName.Contains("gift"))
            {
                return contribShare.Donor ?? 0;
            }
            // Check for LOAN REPAYMENT
            else if (shareTypeName.Contains("loan") || shareTypeName.Contains("repayment"))
            {
                return contribShare.LoanAmount ?? 0;
            }
            // Check for PASSBOOK
            else if (shareTypeName.Contains("passbook") || shareTypeName.Contains("pass book"))
            {
                return contribShare.PassBookAmount ?? 0;
            }
            // Default to SHARE CAPITAL
            else
            {
                return contribShare.ShareCapitalAmount ?? 0;
            }
        }
           

        /// <summary>
        /// Get default debit account from GlSetup (usually Cash or Bank account)
        /// </summary>
        private string GetDefaultDrAccount(string companyCode)
        {
            // Try to find a default Cash account first
            var cashAccount = _context.GlSetup
                .FirstOrDefault(g => g.CompanyCode == companyCode &&
                                    g.Status == true &&
                                    (g.Glaccname != null && g.Glaccname.ToLower().Contains("cash")) &&
                                    g.Type == "ASSET");

            if (cashAccount != null)
            {
                return cashAccount.AccNo;
            }

            // If no cash account, try any asset account
            var assetAccount = _context.GlSetup
                .FirstOrDefault(g => g.CompanyCode == companyCode &&
                                    g.Status == true &&
                                    g.Type == "ASSET");

            if (assetAccount != null)
            {
                return assetAccount.AccNo;
            }

            // Last resort - return a placeholder (but this will fail validation)
            throw new Exception($"No valid debit account found in GL Setup for company {companyCode}. Please configure a cash or asset account.");
        }

        /// <summary>
        /// Validates if a GL account exists in the system
        /// </summary>
        private async Task<bool> ValidateGlAccountExistsAsync(string accountNo, string companyCode)
        {
            return await _context.GlSetup
                .AnyAsync(g => g.AccNo == accountNo &&
                              g.CompanyCode == companyCode &&
                              g.Status == true);
        }

        public async Task<List<GlSetup>> GetGlAccountsAsync(string companyCode)
        {
            return await _context.GlSetup
                .Where(g => g.CompanyCode == companyCode && g.Status == true)
                .ToListAsync();
        }

        public async Task<MemberDTO> GetMemberDetailsAsync(string memberNo)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            var member = await _context.Members
                .Where(m => m.CompanyCode == currentCompanyCode &&
                           m.MemberNo == memberNo)
                .FirstOrDefaultAsync();
            return new MemberDTO { 
                Id = member.Id,
                CurrentBalance = 0,
                
            FullName = member.FullName,
            LandLine = "",
            PhoneNo = member.PhoneNo,
            Idno = member.Idno,
            DateJoined = member.EffectDate,
            RegistrationType = member.MembershipType,
            ShareBalance = 0,
            DateOfBirth = member.Dob,
            Department = member.Dept,
            IsDormant = false,
            LastTransactionDate = DateTime.Now,
            CompanyCode = member.CompanyCode,
            Age = member.Age,
            HomeAddress = member.HomeAddr,
            Email = member.Email,
            Station = member.Station,
            Status = member.Status.ToString(),
            MaritalStatus = member.Mstatus.ToString(),
            MemberNo = member.MemberNo,
            IsActive = true,
            Surname = member.Surname,
            GroupCig = member.Cigcode,
            Gender = member.Sex,
            MembershipType = member.MembershipType,
            
            };
        }
    }
}