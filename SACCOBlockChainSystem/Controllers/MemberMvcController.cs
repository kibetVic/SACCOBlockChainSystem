// Controllers/MemberMvcController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System;
using SelectListItem = SACCOBlockChainSystem.Models.ViewModels.SelectListItem;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class MemberMvcController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MemberMvcController> _logger;
        private readonly object currentCompanyCode;

        public MemberMvcController(
            IMemberService memberService,
            ICompanyContextService companyContextService,
            ApplicationDbContext context,
            ILogger<MemberMvcController> logger)
        {
            _memberService = memberService;
            _companyContextService = companyContextService;
            _context = context;
            _logger = logger;
        }


        // Update the Index method in MemberMvcController.cs
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get all active members
                var members = await _memberService.GetAllMembersAsync();

                // Create a view model that includes both members and dashboard data
                var viewModel = new MembersIndexViewModel
                {
                    Members = members.Take(10).ToList(), // Top 10 members
                    AllMembers = members, // All members for the blockchain visualization
                    TotalMembers = members.Count,
                    ActiveMembers = members.Count(m => m.Status == 1),
                    // Add other required properties as needed
                };

                return View(viewModel); // Will look for Views/MemberMvc/Index.cshtml
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members");
                return View("Error");
            }
        }

        // GET: /MemberMvc/Register
        public async Task<IActionResult> Register()
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var currentUserName = _companyContextService.GetCurrentUserName();

                // Get company details
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == currentCompanyCode);

                // Get available CIGs (Common Interest Groups)
                var cigs = await _context.Companies
                    .Where(c => !string.IsNullOrEmpty(c.Cigcode)
                        && c.CompanyCode == currentCompanyCode)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Cigcode ?? string.Empty,
                        Text = $"{c.Cigcode} - {c.CompanyName}"
                    })
                    .ToListAsync();

                // Auto-generate member number
                var memberNo = await GenerateMemberNumberAsync(currentCompanyCode);

                var viewModel = new MemberRegistrationVm
                {
                    CompanyCode = currentCompanyCode,
                    CompanyName = company?.CompanyName ?? currentCompanyCode,
                    MemberNo = memberNo,
                    MembershipType = "Individual",
                    RegistrationType = "Regular",
                    Mstatus = true,
                    CigList = cigs.Select(c => new CigSelectItem
                    {
                        CigCode = c.Value,
                        CigName = c.Text
                    }).ToList(),
                    RegistrationTypes = GetRegistrationTypes()
                };

                ViewBag.Cigs = new SelectList(cigs, "Value", "Text");
                ViewBag.RegistrationTypesList = GetRegistrationTypes();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading registration form");
                return View("Error");
            }
        }



        // POST: /MemberMvc/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(MemberRegistrationVm viewModel)
        {
            try
            {
                _logger.LogInformation("Register POST action called");

                // Calculate age from DOB if provided
                if (viewModel.DateOfBirth.HasValue)
                {
                    viewModel.Age = CalculateAge(viewModel.DateOfBirth.Value);

                    // Validate age
                    if (viewModel.Age < 18)
                    {
                        ModelState.AddModelError("DateOfBirth", "Member must be at least 18 years old.");
                    }
                }

                // Validate based on membership type
                if (viewModel.MembershipType == "Corporate" && string.IsNullOrEmpty(viewModel.Employer))
                {
                    ModelState.AddModelError("Employer", "Employer/Company name is required for corporate members.");
                }

                if (!ModelState.IsValid)
                {
                    // Reload dropdowns
                    await LoadDropdownsAsync(viewModel);
                    return View(viewModel);
                }

                // Map ViewModel to DTO
                var registrationDto = new MemberRegistrationDTO
                {
                    CompanyCode = viewModel.CompanyCode,
                    MemberNo = viewModel.MemberNo,
                    Surname = viewModel.Surname,
                    OtherNames = viewModel.OtherNames,
                    IdNo = viewModel.IdNo,
                    PhoneNo = viewModel.PhoneNo,
                    Email = viewModel.Email,
                    PresentAddr = viewModel.PresentAddr,
                    MembershipType = viewModel.MembershipType,
                    RegistrationType = viewModel.RegistrationType,
                    CigCode = viewModel.CigCode,
                    DateOfBirth = viewModel.DateOfBirth,
                    Age = viewModel.Age,
                    Gender = viewModel.Gender,
                    Employer = viewModel.Employer,
                    Dept = viewModel.Dept,
                    InitialShares = viewModel.InitialShares,
                    RegFee = viewModel.RegFee,
                    Mstatus = viewModel.Mstatus,
                    CreatedBy = User.Identity?.Name ?? "SYSTEM"
                };

                var result = await _memberService.RegisterMemberAsync(registrationDto);

                TempData["SuccessMessage"] = $"Member {result.MemberNo} registered successfully!";
                //return RedirectToAction("Details", new { memberNo = result.MemberNo });
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member");
                ModelState.AddModelError("", ex.Message);

                // Reload dropdowns
                await LoadDropdownsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /MemberMvc/Details/{memberNo}
        public async Task<IActionResult> Details(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
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

        // GET: MemberMvc/Edit/5
        public async Task<IActionResult> Edit(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }

                var viewModel = new MemberRegistrationVm
                {
                    CompanyCode = member.CompanyCode ?? string.Empty,
                    CompanyName = (await _context.Companies.FirstOrDefaultAsync(c => c.CompanyCode == member.CompanyCode))?.CompanyName ?? member.CompanyCode,
                    MemberNo = member.MemberNo,
                    Surname = member.Surname ?? string.Empty,
                    OtherNames = member.OtherNames ?? string.Empty,
                    IdNo = member.Idno ?? string.Empty,
                    PhoneNo = member.PhoneNo ?? string.Empty,
                    Email = member.Email,
                    PresentAddr = member.PresentAddr,
                    MembershipType = member.MembershipType ?? "Individual",
                    RegistrationType = member.RegistrationType ?? "Regular",
                    CigCode = member.Cigcode,
                    DateOfBirth = member.Dob,
                    Age = member.Age,
                    Gender = member.Sex,
                    Employer = member.Employer,
                    Dept = member.Dept,
                    InitialShares = member.InitShares ?? 0,
                    RegFee = member.RegFee,
                    Mstatus = member.Mstatus ?? true
                };

                await LoadDropdownsAsync(viewModel);
                ViewBag.MemberNo = memberNo;
                ViewBag.FullName = $"{member.Surname} {member.OtherNames}";

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading member for editing");
                return View("Error");
            }
        }

        // POST: MemberMvc/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string memberNo, MemberRegistrationVm viewModel)
        {
            try
            {
                if (memberNo != viewModel.MemberNo)
                {
                    return BadRequest();
                }

                // Calculate age from DOB if provided
                if (viewModel.DateOfBirth.HasValue)
                {
                    viewModel.Age = CalculateAge(viewModel.DateOfBirth.Value);
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropdownsAsync(viewModel);
                    ViewBag.MemberNo = memberNo;
                    ViewBag.FullName = $"{viewModel.Surname} {viewModel.OtherNames}";
                    return View(viewModel);
                }

                var existingMember = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (existingMember == null)
                {
                    return NotFound();
                }

                // Update member with values from ViewModel
                existingMember.Surname = viewModel.Surname;
                existingMember.OtherNames = viewModel.OtherNames;
                existingMember.PhoneNo = viewModel.PhoneNo;
                existingMember.Email = viewModel.Email;
                existingMember.PresentAddr = viewModel.PresentAddr;
                existingMember.MembershipType = viewModel.MembershipType;
                existingMember.RegistrationType = viewModel.RegistrationType;
                existingMember.Cigcode = viewModel.CigCode;
                existingMember.Dob = viewModel.DateOfBirth;
                existingMember.Age = viewModel.Age;
                existingMember.Sex = viewModel.Gender;
                existingMember.Employer = viewModel.Employer;
                existingMember.Dept = viewModel.Dept;
                existingMember.Mstatus = viewModel.Mstatus;
                existingMember.RegFee = viewModel.RegFee;

                var success = await _memberService.UpdateMemberAsync(memberNo, existingMember);

                if (!success)
                {
                    ModelState.AddModelError("", "Failed to update member.");
                    await LoadDropdownsAsync(viewModel);
                    ViewBag.MemberNo = memberNo;
                    ViewBag.FullName = $"{viewModel.Surname} {viewModel.OtherNames}";
                    return View(viewModel);
                }

                TempData["SuccessMessage"] = $"Member {memberNo} updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member");
                ModelState.AddModelError("", ex.Message);
                await LoadDropdownsAsync(viewModel);
                ViewBag.MemberNo = memberNo;
                ViewBag.FullName = $"{viewModel.Surname} {viewModel.OtherNames}";
                return View(viewModel);
            }
        }

        // POST: MemberMvc/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string memberNo)
        {
            try
            {
                var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
                if (member == null)
                {
                    return NotFound();
                }

                // Soft delete - mark as inactive/withdrawn
                member.Status = 0; // Inactive
                member.Withdrawn = true;
                member.Mstatus = false;
                member.Memberwitrawaldate = DateTime.Now;
                member.AuditId = _companyContextService.GetCurrentUserName();
                member.AuditTime = DateTime.Now;
                member.AuditDateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Member {memberNo} has been deactivated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting member");
                return View("Error");
            }
        }



        // AJAX endpoint to validate member number
        [HttpGet]
        public async Task<IActionResult> ValidateMemberNo(string memberNo)
        {
            try
            {
                var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();
                var exists = await _context.Members
                    .AnyAsync(m => m.MemberNo == memberNo && m.CompanyCode == currentCompanyCode);

                return Ok(new { valid = !exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating member number");
                return Ok(new { valid = false, error = ex.Message });
            }
        }

        // AJAX endpoint to generate member number
        [HttpGet]
        public async Task<IActionResult> GenerateMemberNo(string companyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(companyCode) || companyCode.Length < 2)
                    return BadRequest("Company code must have at least 2 characters.");

                // Take first 2 letters of companyCode
                var prefix = companyCode.Substring(0, 2).ToUpper();

                var lastMember = await _context.Members
                    .Where(m => m.MemberNo.StartsWith(prefix))
                    .OrderByDescending(m => m.MemberNo)
                    .FirstOrDefaultAsync();

                string newMemberNo;

                if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
                {
                    newMemberNo = $"{prefix}000000001";
                }
                else
                {
                    // Extract numeric part (after first 2 letters)
                    var numericPart = lastMember.MemberNo.Substring(2);

                    if (long.TryParse(numericPart, out long lastNumber))
                    {
                        var nextNumber = lastNumber + 1;
                        newMemberNo = $"{prefix}{nextNumber:000000000}";
                    }
                    else
                    {
                        // Fallback
                        newMemberNo = $"{prefix}{DateTime.Now.Ticks.ToString().Substring(0, 9)}";
                    }
                }

                return Ok(new { memberNo = newMemberNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating member number");
                return StatusCode(500, "An error occurred while generating member number.");
            }
        }


        // Helper methods
        private async Task LoadDropdownsAsync(MemberRegistrationVm viewModel)
        {
            var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

            // Get CIGs as your custom SelectListItem
            var cigs = await _context.Companies
                .Where(c => !string.IsNullOrEmpty(c.Cigcode) && c.CompanyCode == currentCompanyCode)
                .Select(c => new SACCOBlockChainSystem.Models.ViewModels.SelectListItem
                {
                    Value = c.Cigcode ?? string.Empty,
                    Text = $"{c.Cigcode} - {c.CompanyName}"
                })
                .ToListAsync();

            // Convert to Microsoft format for the dropdown helper
            var microsoftCigs = cigs.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Value,
                Text = c.Text
            }).ToList();

            ViewBag.Cigs = new SelectList(microsoftCigs, "Value", "Text", viewModel.CigCode);

            // Get Registration Types as Microsoft SelectListItem for the view
            var registrationTypes = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
            {
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Regular", Text = "Ordinary Member" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "BoardMember", Text = "Board Member" },
            };

            ViewBag.RegistrationTypesList = registrationTypes;
            ViewBag.SelectedRegistrationType = viewModel.RegistrationType;
        }
        //private async Task LoadDropdownsAsync(MemberRegistrationVm viewModel)
        //{
        //    var currentCompanyCode = _companyContextService.GetCurrentCompanyCode();

        //    var cigs = await _context.Companies
        //        .Where(c => !string.IsNullOrEmpty(c.Cigcode)
        //            && c.CompanyCode == currentCompanyCode)
        //        .Select(c => new SelectListItem
        //        {
        //            Value = c.Cigcode ?? string.Empty,
        //            Text = $"{c.Cigcode} - {c.CompanyName}"
        //        })
        //        .ToListAsync();

        //    ViewBag.Cigs = new SelectList(cigs, "Value", "Text", viewModel.CigCode);
        //    ViewBag.RegistrationTypesList = GetRegistrationTypes();
        //    ViewBag.SelectedRegistrationType = viewModel.RegistrationType;
        //}

        private List<SelectListItem> GetRegistrationTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Regular", Text = "Ordinary Member" },
                new SelectListItem { Value = "BoardMember", Text = "Board Member" },
            };
        }

        private async Task<string> GenerateMemberNumberAsync(string companyCode)
        {
            var lastMember = await _context.Members
                .Where(m => m.CompanyCode == companyCode)
                .OrderByDescending(m => m.MemberNo)
                .FirstOrDefaultAsync();

            if (lastMember == null || string.IsNullOrEmpty(lastMember.MemberNo))
            {
                return $"{companyCode}00001";
            }

            var numericPart = new string(lastMember.MemberNo.Where(char.IsDigit).ToArray());
            var prefix = new string(lastMember.MemberNo.Where(c => !char.IsDigit(c)).ToArray());

            if (int.TryParse(numericPart, out int lastNumber))
            {
                var nextNumber = lastNumber + 1;
                return $"{prefix}{nextNumber:00000}";
            }

            return $"{companyCode}{DateTime.Now:yyyyMMddHHmmss}";
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }


        //// POST: /MemberMvc/Register
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Register(MemberRegistrationDTO registration)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Register POST action called");
        //        _logger.LogInformation($"Model State IsValid: {ModelState.IsValid}");

        //        if (!ModelState.IsValid)
        //        {
        //            _logger.LogWarning("Model state is invalid");
        //            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
        //            {
        //                _logger.LogWarning($"Model error: {error.ErrorMessage}");
        //            }
        //            return View(registration);
        //        }

        //        _logger.LogInformation($"Registering member: {registration.Surname} {registration.OtherNames}");
        //        var result = await _memberService.RegisterMemberAsync(registration);

        //        TempData["SuccessMessage"] = $"Member {result.MemberNo} registered successfully!";
        //        return RedirectToAction("Details", new { memberNo = result.MemberNo });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error registering member");

        //        // Check for specific error types
        //        if (ex.Message.Contains("already exists") ||
        //            ex.Message.Contains("Validation error") ||
        //            ex.Message.Contains("Duplicate") ||
        //            ex.Message.Contains("Phone number") ||
        //            ex.Message.Contains("ID number") ||
        //            ex.Message.Contains("Email"))
        //        {
        //            // Add error to ModelState instead of redirecting to error view
        //            ModelState.AddModelError("", ex.Message);

        //            // Also add to specific field if it's a field-specific error
        //            if (ex.Message.Contains("ID number"))
        //            {
        //                ModelState.AddModelError("IdNo", ex.Message);
        //            }
        //            if (ex.Message.Contains("Phone number"))
        //            {
        //                ModelState.AddModelError("PhoneNo", ex.Message);
        //            }
        //            if (ex.Message.Contains("Email"))
        //            {
        //                ModelState.AddModelError("Email", ex.Message);
        //            }
        //        }
        //        else
        //        {
        //            // For other errors, add to ModelState
        //            ModelState.AddModelError("", $"An error occurred: {ex.Message}");
        //        }

        //        // Return to the same registration view with errors
        //        return View(registration);
        //    }
        //}


        //// GET: /MemberMvc/Edit/{memberNo}
        //public async Task<IActionResult> Edit(string memberNo)
        //{
        //    try
        //    {
        //        var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
        //        if (member == null)
        //        {
        //            return NotFound();
        //        }

        //        // Convert Member to MemberRegistrationDTO for the edit form
        //        var editDto = new MemberRegistrationDTO
        //        {
        //            Surname = member.Surname,
        //            OtherNames = member.OtherNames,
        //            IdNo = member.Idno,
        //            PhoneNo = member.PhoneNo,
        //            Email = member.Email,
        //            DateOfBirth = member.Dob,
        //            Gender = member.Sex,
        //            CompanyCode = member.CompanyCode,
        //            // Note: InitialShares might not be editable via this form
        //            CreatedBy = member.AuditId
        //        };

        //        ViewBag.MemberNo = memberNo;
        //        ViewBag.FullName = $"{member.Surname} {member.OtherNames}";

        //        return View(editDto);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading member for editing");
        //        return View("Error");
        //    }
        //}

        //// POST: /MemberMvc/Edit/{memberNo}
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(string memberNo, MemberRegistrationDTO editDto)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Edit POST action called for member: {memberNo}");

        //        if (!ModelState.IsValid)
        //        {
        //            _logger.LogWarning("Model state is invalid for edit");
        //            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
        //            {
        //                _logger.LogWarning($"Model error: {error.ErrorMessage}");
        //            }

        //            ViewBag.MemberNo = memberNo;
        //            ViewBag.FullName = $"{editDto.Surname} {editDto.OtherNames}";
        //            return View(editDto);
        //        }

        //        // Get the existing member
        //        var existingMember = await _memberService.GetMemberByMemberNoAsync(memberNo);
        //        if (existingMember == null)
        //        {
        //            return NotFound();
        //        }

        //        // Create a Member object with updated values
        //        var updatedMember = new Member
        //        {
        //            // Keep the original member number and immutable fields
        //            MemberNo = memberNo,
        //            Idno = existingMember.Idno, // ID number shouldn't be changed
        //            CompanyCode = existingMember.CompanyCode,

        //            // Update editable fields
        //            Surname = editDto.Surname,
        //            OtherNames = editDto.OtherNames,
        //            PhoneNo = editDto.PhoneNo,
        //            Email = editDto.Email,
        //            Dob = editDto.DateOfBirth,
        //            Sex = editDto.Gender,

        //            // Keep original values for other fields
        //            PresentAddr = existingMember.PresentAddr,
        //            Employer = existingMember.Employer,
        //            Dept = existingMember.Dept,
        //            ShareCap = existingMember.ShareCap,

        //            // Update audit fields
        //            AuditId = User.Identity?.Name ?? "SYSTEM",
        //            AuditTime = DateTime.Now,
        //            AuditDateTime = DateTime.Now
        //        };

        //        // Call the service to update the member
        //        var success = await _memberService.UpdateMemberAsync(memberNo, updatedMember);

        //        if (!success)
        //        {
        //            ModelState.AddModelError("", "Failed to update member. Please try again.");
        //            ViewBag.MemberNo = memberNo;
        //            ViewBag.FullName = $"{editDto.Surname} {editDto.OtherNames}";
        //            return View(editDto);
        //        }

        //        TempData["SuccessMessage"] = $"Member {memberNo} updated successfully!";
        //        return RedirectToAction("Details", new { memberNo });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error updating member {memberNo}");

        //        if (ex.Message.Contains("already exists") ||
        //            ex.Message.Contains("Validation error") ||
        //            ex.Message.Contains("Duplicate"))
        //        {
        //            ModelState.AddModelError("", ex.Message);
        //        }
        //        else
        //        {
        //            ModelState.AddModelError("", $"An error occurred: {ex.Message}");
        //        }

        //        ViewBag.MemberNo = memberNo;
        //        ViewBag.FullName = $"{editDto.Surname} {editDto.OtherNames}";
        //        return View(editDto);
        //    }
        //}


        //// GET: /MemberMvc/Details/{memberNo}
        //public async Task<IActionResult> Details(string memberNo)
        //{
        //    try
        //    {
        //        var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
        //        if (member == null)
        //        {
        //            return NotFound();
        //        }
        //        return View(member);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading member details");
        //        return View("Error");
        //    }
        //}

        //// GET: /MemberMvc/Search
        //public IActionResult Search()
        //{
        //    return View();
        //}

        //[HttpGet]
        //public async Task<IActionResult> Transactions(string memberNo = null)
        //{
        //    if (string.IsNullOrEmpty(memberNo))
        //    {
        //        // Show search form when no member number provided
        //        return View("TransactionsSearch");
        //    }

        //    try
        //    {
        //        var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
        //        if (member == null)
        //        {
        //            ViewBag.ErrorMessage = "Member not found";
        //            return View("TransactionsSearch");
        //        }

        //        var viewModel = new MemberTransactionsViewModel
        //        {
        //            Member = member,
        //            Transactions = await GetMemberTransactionsAsync(memberNo),
        //            LoanHistory = await GetMemberLoanHistoryAsync(memberNo),
        //            TotalShares = await _memberService.GetMemberShareBalanceAsync(memberNo),
        //        };

        //        viewModel.LastTransactionDate = viewModel.Transactions.FirstOrDefault()?.ContributionDate;

        //        return View("Transactions", viewModel); // Specify view name explicitly
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading member transactions");
        //        return View("Error");
        //    }
        //}

        //// GET: /MemberMvc/Transactions/{memberNo} (for direct links)
        //[HttpGet("Transactions/{memberNo}")]
        //public async Task<IActionResult> TransactionsWithMemberNo(string memberNo)
        //{
        //    try
        //    {
        //        var member = await _memberService.GetMemberByMemberNoAsync(memberNo);
        //        if (member == null)
        //        {
        //            return NotFound();
        //        }

        //        var viewModel = new MemberTransactionsViewModel
        //        {
        //            Member = member,
        //            Transactions = await GetMemberTransactionsAsync(memberNo),
        //            LoanHistory = await GetMemberLoanHistoryAsync(memberNo),
        //            TotalShares = await _memberService.GetMemberShareBalanceAsync(memberNo),
        //        };

        //        viewModel.LastTransactionDate = viewModel.Transactions.FirstOrDefault()?.ContributionDate;

        //        return View("Transactions", viewModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading member transactions");
        //        return View("Error");
        //    }
        //}

        // GET: /MemberMvc/SearchMembers (for AJAX search)
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
    }
}