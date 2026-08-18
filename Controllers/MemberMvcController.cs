// Controllers/MemberMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
// Controllers/MemberMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;
using System.Text.Json;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class MemberMvcController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly ApplicationDbContext _context;
        private readonly IContributionService _contributionService;
        private readonly AuditTrailService _auditService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<MemberMvcController> _logger;

        public MemberMvcController(
            IMemberService memberService,
            ApplicationDbContext context,
            IContributionService contributionService,
            ICompanyContextService companyContextService,
            ILogger<MemberMvcController> logger,
            AuditTrailService auditService)
        {
            _memberService = memberService;
            _context = context;
            _contributionService = contributionService;
            _companyContextService = companyContextService;
            _auditService = auditService;
            _logger = logger;
        }


        // GET: MemberMvc (Index with all CRUD operations)
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // ============================================================
                // ONLY LOAD MEMBERS DATA - NOTHING ELSE
                // ============================================================
                var allMembersQuery = _context.Members
                    .Where(m => m.CompanyCode == currentCompanyCode && (m.Archived == false || m.Archived == null));

                // Get total counts for statistics (these are just COUNT queries - fast)
                var totalMembers = await allMembersQuery.CountAsync();
                var activeMembers = await allMembersQuery.CountAsync(m => m.Status == 1);
                var blockchainVerifiedCount = await allMembersQuery.CountAsync(m => !string.IsNullOrEmpty(m.BlockchainTxId));
                var totalShareCapital = await allMembersQuery.SumAsync(m => m.ShareCap ?? 0);

                // Get only the TOP 20 most recent members for display
                var recentMembers = await allMembersQuery
                    .OrderByDescending(m => m.AuditDateTime)
                    .Take(20)
                    .AsNoTracking() // Add this for read-only queries
                    .ToListAsync();

                // Get CIGs for dropdown (small table - OK)
                var cigs = await _context.CIGs
                    .Where(c => c.CompanyCode == currentCompanyCode && c.Status == "Active")
                    .OrderBy(c => c.GigName)
                    .AsNoTracking()
                    .ToListAsync();

                // Get Counties for dropdown (small table - OK)
                var counties = await _context.Counties
                    .Where(c => c.Status == "Active")
                    .OrderBy(c => c.CountyName)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.CIGs = cigs;
                ViewBag.Counties = counties;
                ViewBag.TotalMembersCount = totalMembers;

                var viewModel = new MembersIndexViewModel
                {
                    AllMembers = recentMembers,
                    TotalMembers = totalMembers,
                    ActiveMembers = activeMembers,
                    TotalShareCapital = totalShareCapital,
                    BlockchainVerifiedCount = blockchainVerifiedCount,
                    UserCompanyCode = currentCompanyCode
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members index");
                TempData["ErrorMessage"] = "Error loading members list: " + ex.Message;

                var emptyViewModel = new MembersIndexViewModel
                {
                    AllMembers = new List<Member>(),
                    TotalMembers = 0,
                    ActiveMembers = 0,
                    TotalShareCapital = 0,
                    BlockchainVerifiedCount = 0,
                    UserCompanyCode = _companyContextService.GetCurrentCompanyCode()
                };

                return View(emptyViewModel);
            }
        }

        // GET: MemberMvc/SearchMembers
        [HttpGet]
        public async Task<IActionResult> SearchMembers(string searchTerm, string statusFilter = "all")
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Start with all members
                var query = _context.Members
                    .Where(m => m.CompanyCode == currentCompanyCode && (m.Archived == false || m.Archived == null))
                    .AsNoTracking();

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(m =>
                        m.MemberNo.ToLower().Contains(searchTerm) ||
                        m.Surname.ToLower().Contains(searchTerm) ||
                        m.OtherNames.ToLower().Contains(searchTerm) ||
                        (m.Surname + " " + m.OtherNames).ToLower().Contains(searchTerm) ||
                        m.Idno.ToLower().Contains(searchTerm) ||
                        m.PhoneNo.ToLower().Contains(searchTerm) ||
                        (m.Email != null && m.Email.ToLower().Contains(searchTerm))
                    );
                }

                // Apply status filter
                if (statusFilter == "active")
                {
                    query = query.Where(m => m.Status == 1);
                }
                else if (statusFilter == "inactive")
                {
                    query = query.Where(m => m.Status != 1);
                }

                // Order by registration date descending (most recent first)
                var members = await query
                    .OrderByDescending(m => m.ApplicDate)
                    .Select(m => new
                    {
                        memberNo = m.MemberNo,
                        fullName = (m.Surname + " " + m.OtherNames).Trim(),
                        surname = m.Surname,
                        otherNames = m.OtherNames,
                        idNo = m.Idno,
                        phoneNo = m.PhoneNo,
                        email = m.Email,
                        landLine = m.HomeTelNo,
                        gender = m.Sex,
                        dateOfBirth = m.Dob.HasValue ? m.Dob.Value.ToString("yyyy-MM-dd") : "",
                        age = m.Age,
                        maritalStatus = m.Mstatus == true ? "Married" : m.Mstatus == false ? "Single" : "",
                        station = m.Station,
                        department = m.Dept,
                        presentAddress = m.PresentAddr,
                        cigcode = m.Cigcode,
                        membershipType = m.MembershipType,
                        registrationType = m.MemberDescription,
                        status = m.Status,
                        statusText = m.Status == 1 ? "Active" : "Inactive",
                        registrationDate = m.ApplicDate.HasValue ? m.ApplicDate.Value.ToString("yyyy-MM-dd") : "",
                        initialShares = m.InitShares,
                        photo = m.Photo,
                        idFrontImage = m.IdFrontImage,
                        idBackImage = m.IdBackImage
                    })
                    .ToListAsync();

                return Json(new { success = true, data = members, count = members.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: MemberMvc/Create (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] MemberRegistrationDTO model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                model.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                model.CreatedBy = User.Identity?.Name ?? "SYSTEM";
                model.RegistrationDate = DateTime.Now;

                // Validate age based on registration type
                if (model.DateOfBirth.HasValue && model.RegistrationType == "Individual")
                {
                    var age = CalculateAge(model.DateOfBirth.Value);
                    if (age < 18)
                    {
                        return Json(new { success = false, message = "Individual members must be 18 years or older." });
                    }
                }

                // Check for duplicates
                var existingIdNo = await _context.Members
                    .AnyAsync(m => m.Idno == model.IdNo && m.CompanyCode == model.CompanyCode);
                if (existingIdNo)
                {
                    return Json(new { success = false, message = $"ID Number '{model.IdNo}' is already registered." });
                }

                var existingPhone = await _context.Members
                    .AnyAsync(m => m.PhoneNo == model.PhoneNo && m.CompanyCode == model.CompanyCode);
                if (existingPhone)
                {
                    return Json(new { success = false, message = $"Phone Number '{model.PhoneNo}' is already registered." });
                }

                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.Members
                        .AnyAsync(m => m.Email == model.Email && m.CompanyCode == model.CompanyCode);
                    if (existingEmail)
                    {
                        return Json(new { success = false, message = $"Email '{model.Email}' is already registered." });
                    }
                }

               
                // MEMBER NUMBER 
                if (string.IsNullOrEmpty(model.MemberNo))
                {
                    // Generate new unique member number
                    model.MemberNo = await GenerateUniqueMemberNumberAsync(model.CompanyCode);
                    _logger.LogInformation($"Generated new member number: {model.MemberNo}");
                }
                else
                {
                    // Validate the user-provided number is unique
                    var existingMemberNo = await _context.Members
                        .AnyAsync(m => m.MemberNo == model.MemberNo && m.CompanyCode == model.CompanyCode);

                    if (existingMemberNo)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Member Number '{model.MemberNo}' already exists. Please enter a different number."
                        });
                    }

                    // It's unique - use it as-is (THIS IS WHAT YOU WANT)
                    _logger.LogInformation($"Using user-provided member number: {model.MemberNo}");
                }


                // Register member
                var memberResponse = await _memberService.RegisterMemberAsync(model);

                return Json(new
                {
                    success = true,
                    message = "Member registered successfully!",
                    member = memberResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating member");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: MemberMvc/GetMember/{memberNo}
        [HttpGet]
        [Route("MemberMvc/GetMember/{memberNo}")]
        [Route("MemberMvc/GetMember")]
        public async Task<IActionResult> GetMember(string memberNo)
        {
            try
            {
                if (string.IsNullOrEmpty(memberNo))
                {
                    memberNo = Request.Query["memberNo"].ToString();
                }

                _logger.LogInformation($"GetMember called with memberNo: '{memberNo}'");

                if (string.IsNullOrEmpty(memberNo))
                {
                    return Json(new { success = false, message = "Member number is required" });
                }

                memberNo = memberNo.Trim();

                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (member == null)
                {
                    _logger.LogWarning($"Member not found: MemberNo='{memberNo}'");
                    return Json(new { success = false, message = $"Member '{memberNo}' not found" });
                }

                // Map status to string
                string statusText = member.Status switch
                {
                    1 => "Active",
                    2 => "Withdrawn",
                    3 => "Deceased",
                    4 => "Dormant",
                    5 => "Suspended",
                    _ => "Active"
                };

                // Calculate full name
                string fullName = $"{member.Surname} {member.OtherNames}".Trim();

                _logger.LogInformation($"Member found: {member.MemberNo} - {member.Surname} {member.OtherNames}");

                return Json(new
                {
                    success = true,
                    member = new
                    {
                        // Basic Info
                        MemberNo = member.MemberNo ?? "",
                        Surname = member.Surname ?? "",
                        OtherNames = member.OtherNames ?? "",
                        FullName = fullName,
                        IdNo = member.Idno ?? "",

                        // Contact Info
                        PhoneNo = member.PhoneNo ?? member.MobileNo ?? "",
                        LandLine = member.HomeTelNo ?? member.OfficeTelNo ?? "",
                        Email = member.Email ?? member.EmailAddress ?? "",

                        // Personal Info
                        Gender = member.Sex ?? "",
                        DateOfBirth = member.Dob?.ToString("yyyy-MM-dd") ?? "",
                        Age = member.Age?.ToString() ?? "",
                        MaritalStatus = member.Mstatus == true ? "Married" : member.Mstatus == false ? "Single" : "",

                        // Employment & Location
                        Employer = member.Employer ?? "",
                        Department = member.Dept ?? "",
                        Station = member.Station ?? "",
                        PresentAddress = member.PresentAddr ?? "",
                        HomeAddress = member.HomeAddr ?? "",

                        // Membership Settings
                        Cigcode = member.Cigcode ?? "",
                        GroupCig = member.Cigcode ?? "",
                        MembershipType = member.MembershipType ?? "Individual",
                        RegistrationType = member.MemberDescription ?? "Ordinary Member",
                        Status = statusText,
                        RegistrationDate = member.ApplicDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),

                        // Financial
                        InitialShares = member.InitShares ?? 0,
                        ShareBalance = member.ShareCap ?? 0,
                        CurrentBalance = 0,
                        LoanBalance = member.LoanBalance ?? 0,
                        TotalBalance = (member.ShareCap ?? 0) - (member.LoanBalance ?? 0),

                        // Company
                        CompanyCode = member.CompanyCode ?? "",

                        // Flags
                        IsActive = member.Status == 1,
                        IsDormant = member.Dormant == 1,

                        // Blockchain
                        BlockchainTxId = member.BlockchainTxId ?? "",

                        // ============================================================
                        // ADD ID IMAGES AND PHOTO
                        // ============================================================
                        Photo = member.Photo ?? "",
                        IdFrontImage = member.IdFrontImage ?? "",
                        IdBackImage = member.IdBackImage ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member {MemberNo}", memberNo);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: MemberMvc/DebugMembers
        [HttpGet]
        public async Task<IActionResult> DebugMembers()
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
            var allMembers = await _context.Members
                .Where(m => m.CompanyCode == currentCompanyCode)
                .Select(m => new { m.MemberNo, m.Surname, m.OtherNames, m.CompanyCode })
                .ToListAsync();

            return Json(new
            {
                currentCompanyCode = currentCompanyCode,
                totalMembers = allMembers.Count,
                members = allMembers
            });
        }

        // POST: MemberMvc/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] MemberUpdateDTO model)
        {
            try
            {
                _logger.LogInformation("=== UPDATE METHOD CALLED ===");

                if (model == null)
                {
                    _logger.LogWarning("Model is NULL - binding failed");
                    return Json(new { success = false, message = "Model is NULL. Please check the data format." });
                }

                _logger.LogInformation($"Update model received: MemberNo={model.MemberNo}, Surname={model.Surname}, IdNo={model.IdNo}");
                _logger.LogInformation($"Photo: {(string.IsNullOrEmpty(model.Photo) ? "Empty" : "Has data")}");
                _logger.LogInformation($"IdFrontImage: {(string.IsNullOrEmpty(model.IdFrontImage) ? "Empty" : "Has data")}");
                _logger.LogInformation($"IdBackImage: {(string.IsNullOrEmpty(model.IdBackImage) ? "Empty" : "Has data")}");

                if (string.IsNullOrEmpty(model.MemberNo))
                {
                    return Json(new { success = false, message = "Member number is required." });
                }

                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == model.MemberNo && m.CompanyCode == currentCompanyCode);

                if (existingMember == null)
                {
                    return Json(new { success = false, message = "Member not found." });
                }

                // Update the member using the service
                var result = await _memberService.UpdateMemberAsync(model.MemberNo, model);

                if (result == null)
                {
                    return Json(new { success = false, message = "Member update failed." });
                }

                _logger.LogInformation($"Member {model.MemberNo} updated successfully");

                return Json(new
                {
                    success = true,
                    message = "Member updated successfully!",
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


        // DELETE: MemberMvc/Delete/{memberNo}
        [HttpDelete]
        public async Task<IActionResult> Delete(string memberNo)
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (member == null)
                {
                    return Json(new { success = false, message = "Member not found" });
                }

                // Soft delete - archive the member
                member.Archived = true;
                member.Status = 0; // Inactive
                member.AuditTime = DateTime.Now;
                member.AuditId = User.Identity?.Name ?? "SYSTEM";

                await _context.SaveChangesAsync();

                // Log audit
                await _auditService.SaveLogAsync(
                    actionType: AuditActionType.Delete,
                    oldModel: member,
                    tableName: "Members",
                    recordId: member.MemberNo,
                    userId: User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM",
                    userName: User.Identity?.Name ?? "SYSTEM",
                    companyCode: currentCompanyCode,
                    module: "MemberManagement"
                );

                return Json(new { success = true, message = "Member archived successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting member");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<string> GenerateUniqueMemberNumberAsync(string companyCode)
        {
            string monthInitial = DateTime.Now.ToString("MMM").Substring(0, 1).ToUpper();
            string datePart = DateTime.Now.ToString("yyMMdd");
            string timePart = DateTime.Now.ToString("HHmmss");
            string combined = $"{datePart}{timePart}";
            string uniqueDigits = combined.Length > 11 ? combined.Substring(0, 11) : combined.PadLeft(11, '0');

            var lastMember = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo.StartsWith(monthInitial))
                .OrderByDescending(m => m.MemberNo)
                .FirstOrDefaultAsync();

            if (lastMember != null && lastMember.MemberNo.Length >= 12)
            {
                string lastNumericPart = lastMember.MemberNo.Substring(1, 11);
                if (long.TryParse(lastNumericPart, out long lastNumber))
                {
                    long newNumber = lastNumber + 1;
                    uniqueDigits = newNumber.ToString().PadLeft(11, '0');
                    if (uniqueDigits.Length > 11) uniqueDigits = uniqueDigits.Substring(0, 11);
                }
            }

            string memberNo = $"{monthInitial}{uniqueDigits}";
            if (memberNo.Length > 12) memberNo = memberNo.Substring(0, 12);
            else if (memberNo.Length < 12) memberNo = memberNo.PadRight(12, '0');

            return memberNo;
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }


        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            // Get CIGs for dropdown
            var cigs = await _context.CIGs
                .Where(c => c.CompanyCode == currentCompanyCode && c.Status == "Active")
                .OrderBy(c => c.GigName)
                .ToListAsync();

            // Get Counties for dropdown
            var counties = await _context.Counties
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.CountyName)
                .ToListAsync();

            ViewBag.CIGs = cigs;
            ViewBag.Counties = counties;

            // Generate a preview member number for display
            var previewMemberNo = await GeneratePreviewMemberNumberAsync(currentCompanyCode);

            var model = new MemberRegistrationDTO
            {
                MemberNo = previewMemberNo,  // Show generated number
                RegistrationDate = DateTime.Now,
                Status = "Active",
                CompanyCode = currentCompanyCode  // Set the company code
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(MemberRegistrationDTO model)
        {
            try
            {
                model.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                model.CreatedBy = User.Identity?.Name ?? "SYSTEM";
                model.RegistrationDate = DateTime.Now;

                // Clear ModelState to re-validate with our custom rules
                ModelState.Clear();

                // Validate required fields
                if (string.IsNullOrEmpty(model.Surname))
                {
                    ModelState.AddModelError("Surname", "Surname is required");
                }

                if (string.IsNullOrEmpty(model.OtherNames))
                {
                    ModelState.AddModelError("OtherNames", "Other Names are required");
                }

                // Check for duplicate ID Number
                if (!string.IsNullOrEmpty(model.IdNo))
                {
                    var existingIdNo = await _context.Members
                        .AnyAsync(m => m.Idno == model.IdNo && m.CompanyCode == model.CompanyCode);

                    if (existingIdNo)
                    {
                        ModelState.AddModelError("IdNo", $"ID Number '{model.IdNo}' is already registered to another member");
                    }
                }
                else
                {
                    ModelState.AddModelError("IdNo", "ID Number is required");
                }

                // Check for duplicate Phone Number
                if (!string.IsNullOrEmpty(model.PhoneNo))
                {
                    var existingPhone = await _context.Members
                        .AnyAsync(m => m.PhoneNo == model.PhoneNo && m.CompanyCode == model.CompanyCode);

                    if (existingPhone)
                    {
                        ModelState.AddModelError("PhoneNo", $"Phone Number '{model.PhoneNo}' is already registered to another member");
                    }
                }
                else
                {
                    ModelState.AddModelError("PhoneNo", "Phone Number is required");
                }

                // Check for duplicate Email (if provided)
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.Members
                        .AnyAsync(m => m.Email == model.Email && m.CompanyCode == model.CompanyCode);

                    if (existingEmail)
                    {
                        ModelState.AddModelError("Email", $"Email '{model.Email}' is already registered to another member");
                    }
                }

                // ============================================================
                // MEMBER NUMBER - CONTROLLER HANDLES ALL GENERATION
                // ============================================================
                if (string.IsNullOrEmpty(model.MemberNo))
                {
                    // Generate new unique member number
                    model.MemberNo = await GenerateUniqueMemberNumberAsync(model.CompanyCode);
                    _logger.LogInformation($"Generated new member number: {model.MemberNo}");
                }
                else
                {
                    // Check for duplicate Member Number
                    var existingMemberNo = await _context.Members
                        .AnyAsync(m => m.MemberNo == model.MemberNo && m.CompanyCode == model.CompanyCode);

                    if (existingMemberNo)
                    {
                        ModelState.AddModelError("MemberNo", $"Member Number '{model.MemberNo}' already exists. Please use a different number or let the system generate one.");
                    }
                    else
                    {
                        // It's unique - use it as-is
                        _logger.LogInformation($"Using user-provided member number: {model.MemberNo}");
                    }
                }

                // If any validation errors, return to form with errors
                if (!ModelState.IsValid)
                {
                    // Reload dropdown data
                    var cigs = await _context.CIGs
                        .Where(c => c.CompanyCode == model.CompanyCode && c.Status == "Active")
                        .OrderBy(c => c.GigName)
                        .ToListAsync();

                    var counties = await _context.Counties
                        .Where(c => c.Status == "Active")
                        .OrderBy(c => c.CountyName)
                        .ToListAsync();

                    ViewBag.CIGs = cigs;
                    ViewBag.Counties = counties;

                    return View(model);
                }

                // Calculate age from date of birth
                if (model.DateOfBirth.HasValue)
                {
                    var today = DateTime.Today;
                    var age = today.Year - model.DateOfBirth.Value.Year;
                    if (model.DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                    model.Age = age;
                }

                // Register member - service will use the MemberNo from the model
                var memberResponse = await _memberService.RegisterMemberAsync(model);

                if (memberResponse == null || string.IsNullOrEmpty(memberResponse.MemberNo))
                {
                    throw new Exception("Member registration failed - no member number returned");
                }

                // Verify the member number used is the one from the view
                if (memberResponse.MemberNo != model.MemberNo)
                {
                    _logger.LogWarning($"Member number changed from view ({model.MemberNo}) to saved ({memberResponse.MemberNo})");
                }

                // Rest of your success handling remains the same...
                var fullMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberResponse.MemberNo && m.CompanyCode == model.CompanyCode);

                if (fullMember != null)
                {
                    var auditExtraData = new
                    {
                        amount = 0m,
                        memberName = $"{fullMember.Surname} {fullMember.OtherNames}",
                        memberNumber = fullMember.MemberNo,
                        idNumber = fullMember.Idno,
                        phoneNumber = fullMember.PhoneNo,
                        email = fullMember.Email ?? "",
                        dateOfBirth = fullMember.Dob?.ToString("yyyy-MM-dd"),
                        registrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    await _auditService.SaveLogAsync(
                        actionType: AuditActionType.Insert,
                        newModel: fullMember,
                        tableName: "Members",
                        recordId: fullMember.MemberNo,
                        userId: User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM",
                        userName: User.Identity?.Name ?? "SYSTEM",
                        companyCode: model.CompanyCode,
                        module: "MemberManagement",
                        extraData: JsonSerializer.Serialize(auditExtraData)
                    );

                    _logger.LogInformation($"Member registered successfully with MemberNo: {fullMember.MemberNo} (from view: {model.MemberNo})");
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, data = memberResponse });
                }

                TempData["SuccessMessage"] = $"Member {fullMember?.Surname} {fullMember?.OtherNames} (ID: {memberResponse.MemberNo}) registered successfully!";
                return RedirectToAction("Details", new { memberNo = memberResponse.MemberNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member");
                ModelState.AddModelError("", $"Error registering member: {ex.Message}");

                try
                {
                    var cigs = await _context.CIGs
                        .Where(c => c.CompanyCode == model.CompanyCode && c.Status == "Active")
                        .OrderBy(c => c.GigName)
                        .ToListAsync();

                    var counties = await _context.Counties
                        .Where(c => c.Status == "Active")
                        .OrderBy(c => c.CountyName)
                        .ToListAsync();

                    ViewBag.CIGs = cigs;
                    ViewBag.Counties = counties;
                }
                catch (Exception reloadEx)
                {
                    _logger.LogError(reloadEx, "Error reloading dropdown data");
                }

                return View(model);
            }
        }

        private async Task<string> GeneratePreviewMemberNumberAsync(string companyCode)
        {
            string monthInitial = DateTime.Now.ToString("MMM").Substring(0, 1).ToUpper();

            // Generate 11 unique digits using timestamp + sequence
            string datePart = DateTime.Now.ToString("yyMMdd"); // 6 digits: 211223 (year,month,day)
            string timePart = DateTime.Now.ToString("HHmmss"); // 6 digits: 145530
            string combined = $"{datePart}{timePart}"; // 12 digits
            string uniqueDigits = combined.Length > 11 ? combined.Substring(0, 11) : combined.PadLeft(11, '0');

            // Try to get the last member number for today to maintain sequence
            var lastMember = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo.StartsWith(monthInitial))
                .OrderByDescending(m => m.MemberNo)
                .FirstOrDefaultAsync();

            if (lastMember != null && lastMember.MemberNo.Length >= 12)
            {
                // Extract the numeric part (last 11 digits)
                string lastNumericPart = lastMember.MemberNo.Substring(1, 11);
                if (long.TryParse(lastNumericPart, out long lastNumber))
                {
                    // Increment by 1
                    long newNumber = lastNumber + 1;
                    uniqueDigits = newNumber.ToString().PadLeft(11, '0');

                    // Ensure it doesn't exceed 11 digits
                    if (uniqueDigits.Length > 11)
                    {
                        uniqueDigits = uniqueDigits.Substring(0, 11);
                    }
                }
            }

            string memberNo = $"{monthInitial}{uniqueDigits}";

            // Ensure exact length
            if (memberNo.Length > 12)
            {
                memberNo = memberNo.Substring(0, 12);
            }
            else if (memberNo.Length < 12)
            {
                Random rand = new Random();
                memberNo = memberNo.PadRight(12, (char)('0' + rand.Next(0, 9)));
            }

            // Check if this preview number already exists (unlikely but possible)
            var existing = await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

            if (existing != null)
            {
                // If exists, generate a random one for preview using long for 11 digits
                Random random = new Random();
                // Generate 11-digit number (10,000,000,000 to 99,999,999,999)
                long randomDigitsLong = (long)(random.NextDouble() * 90000000000) + 10000000000;
                string randomDigits = randomDigitsLong.ToString(); // 11 digits
                memberNo = $"{monthInitial}{randomDigits}";
            }

            _logger.LogInformation($"Generated preview member number: {memberNo}");
            return memberNo;
        }


        public async Task<IActionResult> Edit(string memberNo)
        {
            if (string.IsNullOrEmpty(memberNo))
            {
                return NotFound();
            }

            try
            {
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }

                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Get CIGs for dropdown
                var cigs = await _context.CIGs
                    .Where(c => c.CompanyCode == currentCompanyCode && c.Status == "Active")
                    .OrderBy(c => c.GigName)
                    .ToListAsync();

                // Get Counties for dropdown
                var counties = await _context.Counties
                    .Where(c => c.Status == "Active")
                    .OrderBy(c => c.CountyName)
                    .ToListAsync();

                ViewBag.CIGs = cigs;
                ViewBag.Counties = counties;
                ViewBag.MemberNo = member.MemberNo;
                ViewBag.FullName = $"{member.Surname} {member.OtherNames}".Trim();

                var updateDto = new MemberUpdateDTO
                {
                    // Editable fields
                    IdNo = member.Idno,  // Now editable
                    RegistrationDate = member.ApplicDate ?? DateTime.Now,  // Now editable

                    // Other fields
                    Surname = member.Surname,
                    OtherNames = member.OtherNames,
                    PhoneNo = member.PhoneNo,
                    LandLine = member.HomeTelNo,
                    Email = member.Email,
                    Gender = member.Sex,
                    DateOfBirth = member.Dob,
                    Age = member.Age?.ToString(),
                    Station = member.Station,
                    Department = member.Dept,
                    PresentAddress = member.PresentAddr,
                    Cigcode = member.Cigcode,
                    MembershipType = member.MembershipType,
                    RegistrationType = member.MemberDescription,
                    MaritalStatus = member.Mstatus == true ? "Married" : member.Mstatus == false ? "Single" : null,
                    Status = member.Status switch
                    {
                        1 => "Active",
                        2 => "Withdrawn",
                        3 => "Deceased",
                        4 => "Dormant",
                        5 => "Suspended",
                        _ => "Active"
                    },
                    CompanyCode = member.CompanyCode,
                    CreatedBy = member.AuditId
                };

                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for member {memberNo}");
                TempData["ErrorMessage"] = "Error loading member data";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string memberNo, MemberUpdateDTO model)
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

                // Get the existing member
                var existingMember = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                if (existingMember == null)
                {
                    TempData["ErrorMessage"] = "Member not found";
                    return RedirectToAction("Index");
                }

                // Clear ModelState for custom validation
                ModelState.Clear();

                // Validate required fields
                if (string.IsNullOrEmpty(model.Surname))
                {
                    ModelState.AddModelError("Surname", "Surname is required");
                }

                if (string.IsNullOrEmpty(model.OtherNames))
                {
                    ModelState.AddModelError("OtherNames", "Other Names are required");
                }

                // Check for duplicate ID Number (excluding current member)
                if (!string.IsNullOrEmpty(model.IdNo))
                {
                    var existingIdNo = await _context.Members
                        .AnyAsync(m => m.Idno == model.IdNo &&
                                      m.CompanyCode == currentCompanyCode &&
                                      m.MemberNo != memberNo);

                    if (existingIdNo)
                    {
                        ModelState.AddModelError("IdNo", $"ID Number '{model.IdNo}' is already registered to another member");
                    }
                }

                // Check for duplicate Phone Number (excluding current member)
                if (!string.IsNullOrEmpty(model.PhoneNo))
                {
                    var existingPhone = await _context.Members
                        .AnyAsync(m => m.PhoneNo == model.PhoneNo &&
                                      m.CompanyCode == currentCompanyCode &&
                                      m.MemberNo != memberNo);

                    if (existingPhone)
                    {
                        ModelState.AddModelError("PhoneNo", $"Phone Number '{model.PhoneNo}' is already registered to another member");
                    }
                }

                // Check for duplicate Email (if provided, excluding current member)
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.Members
                        .AnyAsync(m => m.Email == model.Email &&
                                      m.CompanyCode == currentCompanyCode &&
                                      m.MemberNo != memberNo);

                    if (existingEmail)
                    {
                        ModelState.AddModelError("Email", $"Email '{model.Email}' is already registered to another member");
                    }
                }

                // If any validation errors, return to form with errors
                if (!ModelState.IsValid)
                {
                    // Reload dropdown data
                    var cigs = await _context.CIGs
                        .Where(c => c.CompanyCode == currentCompanyCode && c.Status == "Active")
                        .OrderBy(c => c.GigName)
                        .ToListAsync();

                    var counties = await _context.Counties
                        .Where(c => c.Status == "Active")
                        .OrderBy(c => c.CountyName)
                        .ToListAsync();

                    ViewBag.CIGs = cigs;
                    ViewBag.Counties = counties;
                    ViewBag.MemberNo = memberNo;
                    ViewBag.FullName = $"{existingMember.Surname} {existingMember.OtherNames}";

                    return View(model);
                }

                // Update member
                await _memberService.UpdateMemberAsync(memberNo, model);

                TempData["SuccessMessage"] = $"Member updated successfully!";
                return RedirectToAction("Index");
                //return RedirectToAction("Details", new { memberNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member");
                TempData["ErrorMessage"] = $"Error updating member: {ex.Message}";

                // Reload dropdown data
                var cigs = await _context.CIGs
                    .Where(c => c.CompanyCode == _companyContextService.GetCurrentCompanyCode() && c.Status == "Active")
                    .OrderBy(c => c.GigName)
                    .ToListAsync();

                var counties = await _context.Counties
                    .Where(c => c.Status == "Active")
                    .OrderBy(c => c.CountyName)
                    .ToListAsync();

                ViewBag.CIGs = cigs;
                ViewBag.Counties = counties;
                ViewBag.MemberNo = memberNo;

                return View(model);
            }
        }
        public async Task<IActionResult> Details(string memberNo)
        {
            try
            {
                var member = await _contributionService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }
                return View(member);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member details");
                return View("Error");
            }
        }

        // GET: /MemberMvc/Search
        public IActionResult Search()
        {
            return View();
        }

        [HttpGet("SearchMembers")]
        public async Task<IActionResult> SearchMembersAjax(string searchTerm)
        {
            try
            {
                var members = await _memberService.SearchMembersAsync(searchTerm);
                return Ok(new { success = true, data = members });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching members");
                return Ok(new { success = false, message = "Error searching members" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchMember(string memberNo, string idNo, string fullName)
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var query = _context.Members.AsQueryable();

                query = query.Where(m => m.CompanyCode == currentCompanyCode);

                // Status check - assuming Active status is 1 or 0
                // Check your Member model to see what values represent "Active"
                // Common: 1 = Active, 0 = Inactive, or use m.Withdrawn == false
                query = query.Where(m => m.Withdrawn != true && m.Archived != true);

                if (!string.IsNullOrEmpty(memberNo))
                {
                    query = query.Where(m => m.MemberNo.Contains(memberNo));
                }
                else if (!string.IsNullOrEmpty(idNo))
                {
                    query = query.Where(m => m.Idno.Contains(idNo));
                }
                else if (!string.IsNullOrEmpty(fullName))
                {
                    query = query.Where(m => (m.Surname + " " + m.OtherNames).Contains(fullName));
                }
                else
                {
                    return Json(new { success = false, message = "Please provide a search value" });
                }

                var member = await query.Select(m => new
                {
                    m.MemberNo,
                    FullName = m.Surname + " " + m.OtherNames,
                    m.Idno,
                    m.PhoneNo,
                    m.Email,
                    Status = m.Withdrawn == true ? "Withdrawn" : (m.Archived == true ? "Archived" : "Active")
                }).FirstOrDefaultAsync();

                if (member == null)
                {
                    return Json(new { success = false, message = "Member not found" });
                }

                return Json(new { success = true, member });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching member");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /MemberMvc/MyProfile
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            try
            {
                // Get logged-in member number
                var memberNo = GetLoggedInMemberNumber();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["ErrorMessage"] = "Member not found. Please login again.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                var companyCode = GetUserCompanyCode();

                // Get member details
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);

                if (member == null)
                {
                    TempData["ErrorMessage"] = "Member profile not found.";
                    return RedirectToAction("MemberLogin", "Account");
                }

                // Create DTO for the view
                var profileDto = new MemberUpdateDTO
                {
                    MemberNo = member.MemberNo,
                    Surname = member.Surname,
                    OtherNames = member.OtherNames,
                    IdNo = member.Idno,
                    PhoneNo = member.PhoneNo,
                    LandLine = member.HomeTelNo,
                    Email = member.Email,
                    Gender = member.Sex,
                    DateOfBirth = member.Dob,
                    Age = member.Age?.ToString(),
                    Station = member.Station,
                    Department = member.Dept,
                    PresentAddress = member.PresentAddr,
                    Employer = member.Employer,
                    MembershipType = member.MembershipType,
                    RegistrationType = member.MemberDescription,
                    Photo = member.Photo,
                    MaritalStatus = member.Mstatus == true ? "Married" : member.Mstatus == false ? "Single" : "",
                    Status = member.Status switch
                    {
                        1 => "Active",
                        2 => "Withdrawn",
                        3 => "Deceased",
                        4 => "Dormant",
                        5 => "Suspended",
                        _ => "Active"
                    },
                    RegistrationDate = member.ApplicDate ?? DateTime.Now
                };

                // Get member stats
                var totalContributions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo && c.CompanyCode == companyCode)
                    .SumAsync(c => c.Amount ?? 0);

                var shareBalance = await _context.Shares
                    .Where(s => s.MemberNo == memberNo && s.CompanyCode == companyCode)
                    .SumAsync(s => s.TotalShares ?? 0);

                var lastTransaction = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo && c.CompanyCode == companyCode)
                    .OrderByDescending(c => c.ContrDate)
                    .FirstOrDefaultAsync();

                ViewBag.TotalContributions = totalContributions;
                ViewBag.ShareBalance = shareBalance;
                ViewBag.LastTransactionDate = lastTransaction?.ContrDate;
                ViewBag.LastTransactionAmount = lastTransaction?.Amount;
                ViewBag.MemberName = $"{member.Surname} {member.OtherNames}";
                ViewBag.MemberNo = memberNo;

                return View(profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member profile");
                TempData["ErrorMessage"] = "Error loading profile";
                return RedirectToAction("MemberContribute", "ContributionMvc");
            }
        }

        // POST: /MemberMvc/MyProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyProfile([FromBody] MemberUpdateDTO model)
        {
            try
            {
                var memberNo = GetLoggedInMemberNumber();

                if (string.IsNullOrEmpty(memberNo))
                {
                    return Json(new { success = false, message = "Member not found. Please login again." });
                }

                // Validate required fields
                if (string.IsNullOrEmpty(model.Surname))
                {
                    return Json(new { success = false, message = "Surname is required" });
                }
                if (string.IsNullOrEmpty(model.OtherNames))
                {
                    return Json(new { success = false, message = "Other Names are required" });
                }
                if (string.IsNullOrEmpty(model.IdNo))
                {
                    return Json(new { success = false, message = "ID Number is required" });
                }
                if (string.IsNullOrEmpty(model.PhoneNo))
                {
                    return Json(new { success = false, message = "Phone Number is required" });
                }

                // Validate photo if provided (optional)
                if (!string.IsNullOrEmpty(model.Photo))
                {
                    // Validate it's a valid base64 image
                    if (!model.Photo.StartsWith("data:image/"))
                    {
                        return Json(new { success = false, message = "Invalid image format" });
                    }
                }

                // Update member using service
                var result = await _memberService.UpdateMemberAsync(memberNo, model);

                return Json(new { success = true, message = "Your profile has been updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member profile");
                return Json(new { success = false, message = ex.Message });
            }
        }


        #region Helper Methods

        private string GetUserCompanyCode()
        {
            try
            {
                // First try to get from claims
                var companyCodeClaim = User.FindFirst("CompanyCode")?.Value;
                if (!string.IsNullOrEmpty(companyCodeClaim))
                {
                    _logger.LogDebug($"Company code from claim: '{companyCodeClaim}'");
                    return companyCodeClaim.Trim();
                }

                // Try from session
                var sessionCompanyCode = HttpContext.Session.GetString("CompanyCode");
                if (!string.IsNullOrEmpty(sessionCompanyCode))
                {
                    _logger.LogDebug($"Company code from session: '{sessionCompanyCode}'");
                    return sessionCompanyCode;
                }

                // Try from company context service
                var contextCompanyCode = _companyContextService.GetCurrentCompanyCode();
                if (!string.IsNullOrEmpty(contextCompanyCode))
                {
                    _logger.LogDebug($"Company code from context service: '{contextCompanyCode}'");
                    return contextCompanyCode;
                }

                _logger.LogWarning("No company code found in claims, session, or context");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company code");
                return null;
            }
        }

        private string GetLoggedInMemberNumber()
        {
            try
            {
                // First try to get from claims
                var memberNoClaim = User.FindFirst("MemberNo")?.Value;
                if (!string.IsNullOrEmpty(memberNoClaim))
                {
                    _logger.LogDebug($"Member number from claim: '{memberNoClaim}'");
                    return memberNoClaim;
                }

                // Try from Name claim (for member login)
                var nameClaim = User.Identity?.Name;
                if (!string.IsNullOrEmpty(nameClaim))
                {
                    // Check if this username exists in Members table
                    var member = _context.Members.FirstOrDefault(m => m.MemberNo == nameClaim || m.UserName == nameClaim);
                    if (member != null)
                    {
                        _logger.LogDebug($"Member number from Name claim: '{member.MemberNo}'");
                        return member.MemberNo;
                    }
                }

                // Try from session
                var sessionMemberNo = HttpContext.Session.GetString("MemberNo");
                if (!string.IsNullOrEmpty(sessionMemberNo))
                {
                    _logger.LogDebug($"Member number from session: '{sessionMemberNo}'");
                    return sessionMemberNo;
                }

                _logger.LogWarning("Could not retrieve logged-in member number");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logged-in member number");
                return null;
            }
        }

        #endregion

    }
}