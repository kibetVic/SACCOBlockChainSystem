// Controllers/AccountController.cs
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
                return RedirectToAction("Signup", "Account");
            }

            // Get list of available companies for dropdown
            var companies = await _context.Companies
                .Where(c => c.Project == true) // Only active companies/projects
                .OrderBy(c => c.CompanyName)
                .Select(c => new
                {
                    c.CompanyCode,
                    DisplayText = $"{c.CompanyCode} - {c.CompanyName}"
                })
                .ToListAsync();

            ViewBag.Companies = companies;
            return View();
        }

        // POST: /Account/Signup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(SignupVm model)
        {
            if (!ModelState.IsValid)
            {
                // Reload companies for dropdown
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
                return View(model);
            }

            try
            {
                // Validate company code exists
                var companyExists = await _context.Companies
                    .AnyAsync(c => c.CompanyCode == model.CompanyCode && c.Project == true);

                if (!companyExists)
                {
                    ModelState.AddModelError("CompanyCode", "Invalid company code or company is not active.");

                    // Reload companies for dropdown
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
                    return View(model);
                }

                // Check if Username already exists
                var existingUsername = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.UserName);

                if (existingUsername != null)
                {
                    ModelState.AddModelError(nameof(model.UserName), "Username already exists. Please choose a different one.");

                    // Reload companies for dropdown
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
                    return View(model);
                }

                // Check if Email already exists
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.Email == model.Email);

                    if (existingEmail != null)
                    {
                        ModelState.AddModelError(nameof(model.Email), "Email already registered.");

                        // Reload companies for dropdown
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
                        return View(model);
                    }
                }

                // Check if MemberNo already exists (if provided)
                if (!string.IsNullOrEmpty(model.MemberNo))
                {
                    var existingMemberNo = await _context.UserAccounts1
                        .FirstOrDefaultAsync(u => u.MemberNo == model.MemberNo);

                    if (existingMemberNo != null)
                    {
                        ModelState.AddModelError(nameof(model.MemberNo), "Member number already registered.");

                        // Reload companies for dropdown
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
                        return View(model);
                    }
                }

                // Generate UserLoginId (combine first letter of username with timestamp)
                var userLoginId = GenerateUserLoginId(model.UserName);

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == model.CompanyCode);

                // Create new user
                var user = new UserAccounts1
                {
                    UserName = model.UserName,
                    UserLoginId = userLoginId,
                    Password = HashPassword(model.Password),
                    Email = model.Email,
                    Phone = model.Phone,
                    PhoneNo = model.Phone,
                    MemberNo = model.MemberNo,
                    Department = model.Department,
                    SubCounty = model.SubCounty,
                    Ward = model.Ward,
                    DateCreated = DateTime.Now,
                    Status = "Pending",
                    Userstatus = "Pending",
                    ApprovalStatus = "Pending",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = "Member",
                    Cigcode = company?.Cigcode, // Get from company
                    CompanyCode = (string)model.CompanyCode,
                    Branchcode = (string)(company?.Cigcode ?? model.CompanyCode), // Use CIG code or company code
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                _context.UserAccounts1.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"New user registered: {model.UserName} with Company: {company?.CompanyName} ({model.CompanyCode})");

                TempData["SuccessMessage"] = $"Registration successful! Your account is pending approval for {company?.CompanyName}. You will be notified once approved.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");

                // Reload companies for dropdown
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
                return View(model);
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
            // Extract first 3 letters of username (uppercase)
            var prefix = userName.Length >= 3
                ? userName.Substring(0, 3).ToUpper()
                : userName.ToUpper();

            // Add timestamp for uniqueness
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