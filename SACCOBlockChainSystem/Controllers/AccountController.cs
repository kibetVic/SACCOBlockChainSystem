using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SACCOBlockChainSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Blockchain");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Hash the password for comparison
                var hashedPassword = HashPassword(model.Password);

                // Find user by Username and password
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.Username && u.Password == hashedPassword);

                if (user == null)
                {
                    // Update failed attempts for the username
                    var failedUser = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.UserName == model.Username);

                    if (failedUser != null)
                    {
                        failedUser.FailedAttempts = (failedUser.FailedAttempts ?? 0) + 1;

                        // Lock account after 5 failed attempts
                        if (failedUser.FailedAttempts >= 5)
                        {
                            failedUser.IsLocked = true;
                            _logger.LogWarning($"Account locked for username: {model.Username}");
                        }

                        await _context.SaveChangesAsync();
                    }

                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                // Check if account is locked
                if (user.IsLocked == true)
                {
                    ModelState.AddModelError(string.Empty, "Account is locked. Please contact administrator.");
                    return View(model);
                }

                // Check if account is active
                if (user.Status?.ToLower() != "active" && user.Userstatus?.ToLower() != "active")
                {
                    ModelState.AddModelError(string.Empty, "Account is not active. Please contact administrator.");
                    return View(model);
                }

                // Check if user has a company code
                if (string.IsNullOrEmpty(user.CompanyCode))
                {
                    ModelState.AddModelError(string.Empty, "User account is not associated with any company. Please contact administrator.");
                    return View(model);
                }

                // Get company name for the user's company code
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

                var companyName = company?.CompanyName ?? "Unknown Company";

                // Reset failed attempts on successful login
                user.FailedAttempts = 0;
                await _context.SaveChangesAsync();

                // Create claims - INCLUDING COMPANY CODE AND COMPANY NAME
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim("FullName", user.UserName ?? string.Empty),
                    new Claim("Email", user.Email ?? string.Empty),
                    new Claim("UserId", user.UserId.ToString()),
                    new Claim("CompanyCode", user.CompanyCode ?? "000"),
                    new Claim("CompanyName", companyName), // ADD COMPANY NAME CLAIM
                    new Claim("UserLoginId", user.UserLoginId ?? string.Empty)
                };

                // Add role claim if UserGroup exists
                if (!string.IsNullOrEmpty(user.UserGroup))
                {
                    claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
                }

                // Add additional user info claims
                if (!string.IsNullOrEmpty(user.Department))
                {
                    claims.Add(new Claim("Department", user.Department));
                }

                if (!string.IsNullOrEmpty(user.MemberNo))
                {
                    claims.Add(new Claim("MemberNo", user.MemberNo));
                }

                //CompanyCode claim
                if (!string.IsNullOrEmpty(user.CompanyCode))
                {
                    claims.Add(new Claim("CompanyCode", user.CompanyCode));
                }

                // Branch code Claims
                if (!string.IsNullOrEmpty(user.Branchcode))
                {
                    claims.Add(new Claim("BranchCode", user.Branchcode));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2),
                    RedirectUri = returnUrl ?? "/Home/Index"
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation($"User {user.UserName} (Company: {companyName} - {user.CompanyCode}) logged in successfully.");

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        // GET: /Account/Signup
        [HttpGet]
        public async Task<IActionResult> Signup()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Blockchain");
            }

            await LoadCompanies();
            return View();
        }

        // POST: /Account/Signup - SIMPLIFIED AND FIXED
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(SignupVm model)
        {
            // Debug: Log what's being received
            _logger.LogInformation("=== SIGNUP ATTEMPT ===");
            _logger.LogInformation($"Username: {model.UserName}");
            _logger.LogInformation($"Email: {model.Email}");
            _logger.LogInformation($"CompanyCode: {model.CompanyCode}");
            _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed!");
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    if (errors.Any())
                    {
                        _logger.LogWarning($"  {key}: {string.Join(", ", errors.Select(e => e.ErrorMessage))}");
                    }
                }

                await LoadCompanies();
                return View(model);
            }

            try
            {
                // Validate company exists
                var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == model.CompanyCode && c.Project == true);

                if (company == null)
                {
                    ModelState.AddModelError("CompanyCode", "Selected company is not available.");
                    await LoadCompanies();
                    return View(model);
                }

                // Check for duplicate username
                var existingUser = await _context.UserAccounts1
                .FirstOrDefaultAsync(u => u.UserName == model.UserName);

                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "Username already exists.");
                    await LoadCompanies();
                    return View(model);
                }

                // Check for duplicate email
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                    if (existingEmail != null)
                    {
                        ModelState.AddModelError("Email", "Email already registered.");
                        await LoadCompanies();
                        return View(model);
                    }
                }

                // Check for duplicate member number if provided
                if (!string.IsNullOrEmpty(model.MemberNo))
                {
                    var existingMemberNo = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.MemberNo == model.MemberNo);

                    if (existingMemberNo != null)
                    {
                        ModelState.AddModelError("MemberNo", "Member number already registered.");
                        await LoadCompanies();
                        return View(model);
                    }
                }

                // Create the new user
                var user = new UserAccounts1
                {
                    UserName = model.UserName.Trim(),
                    UserLoginId = GenerateUserLoginId(model.UserName),
                    Password = HashPassword(model.Password),
                    Email = model.Email?.Trim(),
                    Phone = model.Phone?.Trim(),
                    PhoneNo = model.Phone?.Trim(),
                    MemberNo = model.MemberNo?.Trim(),
                    Department = model.Department?.Trim(),
                    SubCounty = model.SubCounty?.Trim(),
                    Ward = model.Ward?.Trim(),
                    DateCreated = DateTime.Now,
                    Status = "Pending",
                    Userstatus = "Pending",
                    ApprovalStatus = "Pending",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = string.IsNullOrEmpty(model.UserGroup) ? "Member" : model.UserGroup,
                    Cigcode = company.Cigcode,
                    CompanyCode = model.CompanyCode,
                    Branchcode = company.Cigcode ?? model.CompanyCode,
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                // Save to database
                _context.UserAccounts1.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"SUCCESS: User '{user.UserName}' created with ID: {user.UserId}");

                TempData["SuccessMessage"] = $"Registration successful! Your account is pending approval for {company.CompanyName}. You will be notified once approved.";
                return RedirectToAction("Login");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error during signup");
                ModelState.AddModelError(string.Empty, "A database error occurred. Please try again.");

                if (dbEx.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {dbEx.InnerException.Message}");
                }

                await LoadCompanies();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during signup");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                await LoadCompanies();
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TestSignup()
        {
            try
            {
                // Create a test user directly
                var testUser = new UserAccounts1
                {
                    UserName = "testuser_" + DateTime.Now.Ticks,
                    UserLoginId = "TEST" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Password = HashPassword("Test123!"),
                    Email = "test@example.com",
                    Phone = "1234567890",
                    PhoneNo = "1234567890",
                    MemberNo = "TEST001",
                    Department = "Test",
                    DateCreated = DateTime.Now,
                    Status = "Pending",
                    Userstatus = "Pending",
                    ApprovalStatus = "Pending",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = "Member",
                    CompanyCode = "001", // Use an existing company code from your database
                    Branchcode = "001",
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                _context.UserAccounts1.Add(testUser);
                var result = await _context.SaveChangesAsync();

                return Content($"Test user created successfully! Rows affected: {result}, UserId: {testUser.UserId}");
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}\n\n{ex.StackTrace}");
            }
        }


        // Helper method to load companies
        private async Task LoadCompanies()
        {
            try
            {
                var companies = await _context.Companies
                .Where(c => c.Project == true)
                .OrderBy(c => c.CompanyName)
                .Select(c => new
                {
                    c.CompanyCode,
                    DisplayText = $"{c.CompanyCode} - {c.CompanyName}"
                })
                .ToListAsync();

                ViewBag.Companies = companies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load companies");
                ViewBag.Companies = new List<dynamic>();
            }
        }


        // GET: /Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.UserAccounts1
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound();
            }

            // Get company name
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == user.CompanyCode);

            var profile = new ProfileVm
            {
                UserId = user.UserId,
                UserName = user.UserName,
                UserLoginId = user.UserLoginId,
                Email = user.Email,
                Phone = user.Phone,
                MemberNo = user.MemberNo,
                Department = user.Department,
                SubCounty = user.SubCounty,
                Ward = user.Ward,
                UserGroup = user.UserGroup,
                Status = user.Status,
                DateCreated = user.DateCreated
            };

            ViewBag.CompanyName = company?.CompanyName ?? "Unknown Company";
            ViewBag.CompanyCode = user.CompanyCode;

            return View(profile);
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            var companyName = User.FindFirstValue("CompanyName");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation($"User {userName} (Company: {companyName}) logged out.");
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /Account/CompanySwitch (Optional - for admins to switch companies)
        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CompanySwitch(string companyCode)
        {
            var user = await _context.UserAccounts1
                .FirstOrDefaultAsync(u => u.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)));

            if (user != null)
            {
                // Validate new company code
                var newCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode);

                if (newCompany == null)
                {
                    TempData["ErrorMessage"] = $"Company with code {companyCode} not found.";
                    return RedirectToAction("Index", "Blockchain");
                }

                // Update user's company code
                user.CompanyCode = companyCode;
                await _context.SaveChangesAsync();

                // Update claims by re-authenticating
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim("FullName", user.UserName ?? string.Empty),
                    new Claim("Email", user.Email ?? string.Empty),
                    new Claim("UserId", user.UserId.ToString()),
                    new Claim("CompanyCode", user.CompanyCode ?? "000"),
                    new Claim("CompanyName", newCompany.CompanyName ?? "Unknown Company"),
                };

                if (!string.IsNullOrEmpty(user.UserGroup))
                {
                    claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                _logger.LogInformation($"User {user.UserName} switched to company: {newCompany.CompanyName} ({companyCode})");
                TempData["SuccessMessage"] = $"Switched to company: {newCompany.CompanyName}";
            }

            return RedirectToAction("Index", "Blockchain");
        }

        // Helper method to generate UserLoginId
        private string GenerateUserLoginId(string userName)
        {
            var prefix = userName.Length >= 3
            ? userName.Substring(0, 3).ToUpper()
            : userName.ToUpper();

            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            return $"{prefix}{timestamp}";
        }

        // Helper method to hash password
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

    }
}
