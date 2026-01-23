// Controllers/AccountController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SACCOBlockChainSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private object _userManager;

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
                return RedirectToAction("Index", "Home");
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
                var hashedPassword = HashPassword(model.Password);
                var user = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.Username && u.Password == hashedPassword);

                if (user == null)
                {
                    // ... existing failed login logic ...
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                // ... existing account status checks ...

                // Reset failed attempts
                user.FailedAttempts = 0;
                await _context.SaveChangesAsync();

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim("FullName", user.UserName ?? string.Empty),
                    new Claim("Email", user.Email ?? string.Empty),
                    new Claim("UserId", user.UserId.ToString()),
                    new Claim("UserGroup", user.UserGroup ?? "Member") // Add UserGroup as a separate claim
                };

                // Add UserGroup as a Role claim
                if (!string.IsNullOrEmpty(user.UserGroup))
                {
                    claims.Add(new Claim(ClaimTypes.Role, user.UserGroup));
                }

                // Add MemberNo if exists
                if (!string.IsNullOrEmpty(user.MemberNo))
                {
                    claims.Add(new Claim("MemberNo", user.MemberNo));
                }

                // Add additional claims for specific user groups
                switch (user.UserGroup?.ToLower())
                {
                    case "admin":
                    case "superadmin":
                        // Add all admin roles
                        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                        claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
                        break;

                    case "teller":
                        claims.Add(new Claim(ClaimTypes.Role, "Teller"));
                        break;

                    case "loanofficer":
                        claims.Add(new Claim(ClaimTypes.Role, "LoanOfficer"));
                        break;

                    case "auditor":
                        claims.Add(new Claim(ClaimTypes.Role, "Auditor"));
                        break;

                    case "boardmember":
                        claims.Add(new Claim(ClaimTypes.Role, "BoardMember"));
                        break;

                    default:
                        // For regular members, add Member role
                        claims.Add(new Claim(ClaimTypes.Role, "Member"));
                        break;
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

                _logger.LogInformation($"User {user.UserName} with group {user.UserGroup} logged in successfully.");

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
        // GET: /Account/Signup
        [HttpGet]
        public IActionResult Signup()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new SignupVm
            {
                AvailableUserGroups = new List<string>
        {
            "Member",
            "Teller",
            "LoanOfficer",
            "Auditor",
            "BoardMember",
            "Staff"
        }
            };

            return View(model);
        }

        // POST: /Account/Signup
        // POST: /Account/Signup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(SignupVm model)
        {
            // Initialize AvailableUserGroups if null
            model.AvailableUserGroups ??= new List<string>
            {
                "Member",
                "Teller",
                "LoanOfficer",
                "Auditor",
                "BoardMember",
                "Staff"
            };

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Check if Username already exists
                var existingUsername = await _context.UserAccounts1
                    .FirstOrDefaultAsync(u => u.UserName == model.UserName);

                if (existingUsername != null)
                {
                    ModelState.AddModelError(nameof(model.UserName), "Username already exists. Please choose a different one.");
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
                        return View(model);
                    }
                }

                // Generate UserLoginId (combine first letter of username with timestamp)
                var userLoginId = GenerateUserLoginId(model.UserName);

                // Determine UserGroup based on input or default
                string userGroup = "Member"; // Default

                if (!string.IsNullOrEmpty(model.UserGroup))
                {
                    userGroup = model.UserGroup;
                }
                else if (!string.IsNullOrEmpty(model.MemberNo))
                {
                    userGroup = "Member";
                }
                else if (!string.IsNullOrEmpty(model.Department))
                {
                    userGroup = "Staff";
                }

                // Create new user
                var user = new UserAccounts1
                {
                    UserName = model.UserName,
                    UserLoginId = userLoginId, // Auto-generated
                    Password = HashPassword(model.Password),
                    Email = model.Email,
                    Phone = model.Phone,
                    PhoneNo = model.Phone, // Set both Phone and PhoneNo
                    MemberNo = model.MemberNo,
                    Department = model.Department,
                    SubCounty = model.SubCounty,
                    Ward = model.Ward,
                    DateCreated = DateTime.Now,
                    Status = "Active",
                    Userstatus = "Active",
                    ApprovalStatus = "Active",
                    FailedAttempts = 0,
                    IsLocked = false,
                    PasswordStatus = "Active",
                    PassExpire = "No",
                    UserGroup = userGroup,
                    Cigcode = "001",
                    CompanyCode = "001",
                    Branchcode = "001",
                    Superuser = 0,
                    Authorize = false,
                    Count = 0
                };

                _context.UserAccounts1.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"New user registered: {model.UserName} with UserGroup: {userGroup}");

                TempData["SuccessMessage"] = "Registration successful! Your account is pending approval. You will be notified once approved.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
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