using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    [Route("Journal")]
    public class JournalPostingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JournalPostingController> _logger;

        public JournalPostingController(
            ApplicationDbContext context,
            ILogger<JournalPostingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===============================
        // GET: /Journal
        // ===============================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Accounts = await _context.GlSetup
                .Where(x => x.Status)
                .OrderBy(x => x.AccNo)
                .ToListAsync();

            ViewBag.CompanyCode = User.FindFirst("CompanyCode")?.Value ?? "001";

            var model = new JournalEntryViewModel
            {
                VoucherNo = await GenerateVoucherNumberAsync(),
                VoucherDate = DateTime.Now,
                CompanyCode = ViewBag.CompanyCode,
                AuditId = User.Identity?.Name ?? "SYSTEM",
                AuditDate = DateTime.Now,
                Posted = false,
                PostedDate = DateTime.MinValue
            };

            return View(model);
        }

        // ===============================
        // POST: /Journal/Save
        // ===============================
        // ===============================
        // POST: /Journal/Save
        // ===============================
        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] JournalEntryViewModel model)
        {
            try
            {
                #region Validation
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new
                    {
                        success = false,
                        message = "Invalid journal data.",
                        errors = errors
                    });
                }

                if (model.Details == null || !model.Details.Any())
                    return Json(new { success = false, message = "Journal must have at least one line." });

                if (model.Details.Count < 2)
                    return Json(new { success = false, message = "Journal must have at least two lines (Debit and Credit)." });

                // Validate each line has either debit or credit
                foreach (var detail in model.Details)
                {
                    if (string.IsNullOrEmpty(detail.AccountNo))
                        return Json(new { success = false, message = "Account number is required for all lines." });

                    if (detail.Debit < 0 || detail.Credit < 0)
                        return Json(new { success = false, message = "Debit and Credit amounts cannot be negative." });

                    if (detail.Debit == 0 && detail.Credit == 0)
                        return Json(new { success = false, message = "Each line must have either Debit or Credit amount." });

                    if (detail.Debit > 0 && detail.Credit > 0)
                        return Json(new { success = false, message = "A line cannot have both Debit and Credit amounts." });
                }
                #endregion

                #region Check for Existing Journal
                // Check if journal already exists
                var existingJournal = await _context.Journals
                    .AnyAsync(x => x.VNO == model.VoucherNo);

                if (existingJournal)
                {
                    // If editing existing journal, delete old entries first
                    var oldEntries = await _context.Journals
                        .Where(x => x.VNO == model.VoucherNo)
                        .ToListAsync();

                    if (oldEntries.Any())
                    {
                        if (oldEntries.First().POSTED)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Cannot edit a posted journal. Please create a new one."
                            });
                        }

                        _context.Journals.RemoveRange(oldEntries);
                        await _context.SaveChangesAsync();
                    }
                }
                #endregion

                #region Prepare Journal Entries
                var companyCode = model.CompanyCode ?? User.FindFirst("CompanyCode")?.Value ?? "001";
                var auditUser = User.Identity?.Name ?? "SYSTEM";
                var now = DateTime.Now;

                // Generate voucher number if not provided
                if (string.IsNullOrEmpty(model.VoucherNo))
                {
                    model.VoucherNo = await GenerateVoucherNumberAsync();
                }

                var journals = new List<Journal>();
                var detailList = model.Details.Where(d => d.Debit > 0 || d.Credit > 0).ToList();

                foreach (var d in detailList)
                {
                    // Get account name from GL Setup if not provided
                    if (string.IsNullOrEmpty(d.AccountName))
                    {
                        var account = await _context.GlSetup
                            .FirstOrDefaultAsync(x => x.AccNo == d.AccountNo);
                        d.AccountName = account?.Glaccname;
                    }

                    journals.Add(new Journal
                    {
                        VNO = model.VoucherNo,
                        ACCNO = d.AccountNo,
                        NAME = d.AccountName,
                        NARATION = d.Narration ?? model.Description,
                        MEMBERNO = model.MemberNo ?? "SYSTEM",
                        SHARETYPE = d.ShareType ?? "CASH",
                        Loanno = model.LoanNo ?? "0",
                        AMOUNT = d.Debit > 0 ? d.Debit : d.Credit,
                        TRANSTYPE = d.Debit > 0 ? "DR" : "CR",
                        TRANSDATE = null,
                        AUDITID = auditUser,
                        AUDITDATE = now,
                        POSTED = false,
                        POSTEDDATE = null,   // ✅ THIS FIXES YOUR ERROR
                        Transactionno = model.TransactionNo,
                        CompanyCode = companyCode
                    });
                }
                #endregion

                #region Validate Balance
                decimal totalDr = journals.Where(x => x.TRANSTYPE == "DR").Sum(x => x.AMOUNT ?? 0);
                decimal totalCr = journals.Where(x => x.TRANSTYPE == "CR").Sum(x => x.AMOUNT ?? 0);

                if (Math.Abs(totalDr - totalCr) > 0.01m)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Journal is not balanced! Debit: {totalDr:N2}, Credit: {totalCr:N2}",
                        totalDebit = totalDr,
                        totalCredit = totalCr,
                        difference = totalDr - totalCr
                    });
                }
                #endregion

                #region Save to Database
                await _context.Journals.AddRangeAsync(journals);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Journal saved successfully. Voucher No: {model.VoucherNo}, Entries: {journals.Count}, DR: {totalDr:N2}, CR: {totalCr:N2}");
                #endregion

                return Json(new
                {
                    success = true,
                    message = "Journal saved successfully!",
                    voucherNo = model.VoucherNo,
                    entries = journals.Count,
                    totalDebit = totalDr,
                    totalCredit = totalCr,
                    isBalanced = true
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, $"Database error saving journal {model.VoucherNo}");

                // Check for specific SQL errors
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                if (innerMessage.Contains("FOREIGN KEY"))
                {
                    return Json(new { success = false, message = "Invalid account number or reference." });
                }
                else if (innerMessage.Contains("duplicate"))
                {
                    return Json(new { success = false, message = "Duplicate entry detected. Please try again." });
                }

                return Json(new { success = false, message = $"Database error: {innerMessage}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving journal {model.VoucherNo}");
                return Json(new
                {
                    success = false,
                    message = $"Error saving journal: {ex.Message}",
                    stackTrace = ex.StackTrace
                });
            }
        }
        
       
        
        [HttpPost("Post/{voucherNo}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Accountant")]
        public async Task<IActionResult> Post(string voucherNo)
        {
            if (string.IsNullOrWhiteSpace(voucherNo))
                return Json(new { success = false, message = "Voucher number is required." });

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var journals = await _context.Journals
                    .Where(x => x.VNO == voucherNo && !x.POSTED)
                    .OrderBy(x => x.TRANSTYPE)
                    .ToListAsync();

                if (!journals.Any())
                    return Json(new { success = false, message = "Journal already posted or not found." });

                decimal totalDr = journals.Where(x => x.TRANSTYPE == "DR").Sum(x => x.AMOUNT ?? 0);
                decimal totalCr = journals.Where(x => x.TRANSTYPE == "CR").Sum(x => x.AMOUNT ?? 0);

                if (Math.Abs(totalDr - totalCr) > 0.01m)
                    return Json(new { success = false, message = $"Journal is not balanced. DR: {totalDr:N2}, CR: {totalCr:N2}" });

                string companyCode = journals.First().CompanyCode ?? "001";
                string auditUser = User.Identity?.Name ?? "SYSTEM";
                DateTime now = DateTime.Now;
                string txnNo = GenerateTransactionNumber();

                var glTransactions = new List<Gltransaction>();

                foreach (var j in journals)
                {
                    var accountExists = await _context.GlSetup.AnyAsync(x => x.AccNo == j.ACCNO);
                    if (!accountExists)
                        throw new Exception($"GL Account {j.ACCNO} not found.");

                    // Get cash/bank account for contra entry
                    string cashAccount = GetCashOrBankAccount();

                    glTransactions.Add(new Gltransaction
                    {
                        TransDate = j.TRANSDATE ?? now,
                        Amount = j.AMOUNT ?? 0,
                        DrAccNo = j.TRANSTYPE == "DR" ? j.ACCNO : cashAccount,
                        CrAccNo = j.TRANSTYPE == "CR" ? j.ACCNO : cashAccount,
                        DocumentNo = voucherNo,
                        Source = "JOURNAL",
                        Temp = "JV",
                        CompanyCode = companyCode,
                        TransDescript = j.NARATION ?? $"Journal Entry {voucherNo}",
                        AuditId = auditUser,
                        AuditTime = now,
                        TransactionNo = txnNo,
                        DocPosted = 1,
                        Module = "GL",
                        Cash = 0,
                        Recon = false,
                        Dregard = false,
                        ReconId = 0
                    });
                }

                await _context.Gltransactions.AddRangeAsync(glTransactions);

                foreach (var j in journals)
                {
                    j.POSTED = true;
                    j.POSTEDDATE = now;
                    j.AUDITID = auditUser;
                    j.AUDITDATE = now;
                    j.Transactionno = txnNo;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Journal POSTED: {voucherNo}, TXN: {txnNo}, DR: {totalDr:N2}, CR: {totalCr:N2}");

                return Json(new
                {
                    success = true,
                    message = "Journal posted successfully",
                    voucherNo,
                    transactionNo = txnNo,
                    totalDebit = totalDr,
                    totalCredit = totalCr,
                    entries = journals.Count
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var error = ex.InnerException?.InnerException?.Message ??
                           ex.InnerException?.Message ??
                           ex.Message ??
                           "Unknown posting error";

                _logger.LogError(ex, $"Journal posting failed for {voucherNo}");

                return Json(new
                {
                    success = false,
                    message = error
                });
            }
        }
        // ===============================
        // Helper: Generate Transaction Number
        // ===============================
        private string GenerateTransactionNumber()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random();
            var randomPart = random.Next(1000, 9999);
            return $"TXN-{date}-{randomPart}";
        }

        // ===============================
        // Helper: Get Cash/Bank Account
        // ===============================
        private string GetCashOrBankAccount()
        {
            try
            {
                // Try to get default cash account from GL Setup
                var cashAccount = _context.GlSetup
                    .FirstOrDefault(x => x.AccNo == "1010" && x.Status == true);

                return cashAccount?.AccNo ?? "1010"; // Default to Cash in Bank
            }
            catch
            {
                return "1010"; // Default fallback
            }
        }
        // ===============================
        // GET: /Journal/List
        // ===============================
        [HttpGet("List")]
        public async Task<IActionResult> List(
    DateTime? fromDate,
    DateTime? toDate,
    string? memberNo,
    string? loanNo)
        {
            try
            {
                var query = _context.Journals
                    .Where(x => x.CompanyCode == User.FindFirst("CompanyCode")!.Value)
                    .GroupBy(x => x.VNO)
                    .Select(g => new
                    {
                        VoucherNo = g.Key,

                        VoucherDate = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.TRANSDATE)
                            .FirstOrDefault(),

                        Description = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.NARATION)
                            .FirstOrDefault(),

                        MemberNo = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.MEMBERNO)
                            .FirstOrDefault(),

                        LoanNo = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.Loanno)
                            .FirstOrDefault(),

                        TotalDebit = g
                            .Where(x => x.TRANSTYPE == "DR")
                            .Sum(x => x.AMOUNT ?? 0),

                        TotalCredit = g
                            .Where(x => x.TRANSTYPE == "CR")
                            .Sum(x => x.AMOUNT ?? 0),

                        EntryCount = g.Count(),

                        Posted = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.POSTED)
                            .FirstOrDefault(),

                        PostedDate = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.POSTEDDATE)
                            .FirstOrDefault(),

                        CreatedBy = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.AUDITID)
                            .FirstOrDefault(),

                        CreatedDate = g
                            .OrderBy(x => x.JVID)
                            .Select(x => x.AUDITDATE)
                            .FirstOrDefault()
                    });

                // Filters
                if (fromDate.HasValue)
                    query = query.Where(x => x.VoucherDate >= fromDate);

                if (toDate.HasValue)
                    query = query.Where(x => x.VoucherDate <= toDate);

                if (!string.IsNullOrEmpty(memberNo))
                    query = query.Where(x => x.MemberNo == memberNo);

                if (!string.IsNullOrEmpty(loanNo))
                    query = query.Where(x => x.LoanNo == loanNo);

                var vouchers = await query
                    .OrderByDescending(x => x.VoucherDate)
                    .ThenByDescending(x => x.VoucherNo)
                    .ToListAsync();

                ViewBag.CompanyCode = User.FindFirst("CompanyCode")?.Value ?? "001";
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "Main SACCO";

                return View(vouchers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading journal list");
                TempData["Error"] = $"Error loading journals: {ex.Message}";
                return View(new List<dynamic>());
            }
        }
        // ===============================
        // GET: /Journal/Edit
        // GET: /Journal/Edit/{voucherNo}
        // ===============================
        [HttpGet("Edit")]
        [HttpGet("Edit/{voucherNo}")]
        public async Task<IActionResult> Edit(string voucherNo)
        {
            if (string.IsNullOrEmpty(voucherNo))
            {
                return NotFound("Voucher number is required.");
            }

            try
            {
                var journals = await _context.Journals
                    .Where(x => x.VNO == voucherNo)
                    .OrderBy(x => x.TRANSTYPE)
                    .ToListAsync();

                if (!journals.Any())
                {
                    return NotFound($"Journal with voucher number {voucherNo} not found.");
                }

                var firstJournal = journals.First();

                // Check if journal is posted
                if (firstJournal.POSTED)
                {
                    TempData["Info"] = "This journal is already posted and cannot be edited.";
                    return RedirectToAction("View", new { voucherNo });
                }

                ViewBag.Accounts = await _context.GlSetup
                    .Where(x => x.Status == true)
                    .OrderBy(x => x.AccNo)
                    .ToListAsync();

                ViewBag.CompanyCode = User.FindFirst("CompanyCode")?.Value ?? "001";
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "Main SACCO";

                var model = new JournalEntryViewModel
                {
                    VoucherNo = voucherNo,
                    VoucherDate = firstJournal.TRANSDATE ?? DateTime.Now,
                    Description = firstJournal.NARATION,
                    MemberNo = firstJournal.MEMBERNO,
                    LoanNo = firstJournal.Loanno,
                    CompanyCode = firstJournal.CompanyCode,
                    TransactionNo = firstJournal.Transactionno,
                    AuditId = firstJournal.AUDITID,
                    AuditDate = firstJournal.AUDITDATE,
                    Posted = firstJournal.POSTED,
                    PostedDate = firstJournal.POSTEDDATE ?? DateTime.MinValue,
                    Details = journals.Select(j => new JournalDetailViewModel
                    {
                        AccountNo = j.ACCNO ?? "",
                        AccountName = j.NAME,
                        Narration = j.NARATION,
                        ShareType = j.SHARETYPE,
                        Debit = j.TRANSTYPE == "DR" ? j.AMOUNT ?? 0 : 0,
                        Credit = j.TRANSTYPE == "CR" ? j.AMOUNT ?? 0 : 0
                    }).ToList()
                };

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error editing journal {voucherNo}");
                TempData["Error"] = $"Error loading journal: {ex.Message}";
                return RedirectToAction("List");
            }
        }
        // ===============================
        // GET: /Journal/View
        // GET: /Journal/View/{voucherNo}
        // ===============================
        [HttpGet("View")]
        [HttpGet("View/{voucherNo}")]
        public async Task<IActionResult> View(string voucherNo)
        {
            if (string.IsNullOrEmpty(voucherNo))
            {
                return NotFound("Voucher number is required.");
            }

            try
            {
                var journals = await _context.Journals
                    .Where(x => x.VNO == voucherNo)
                    .OrderBy(x => x.TRANSTYPE)
                    .ToListAsync();

                if (!journals.Any())
                {
                    return NotFound($"Journal with voucher number {voucherNo} not found.");
                }

                ViewBag.Accounts = await _context.GlSetup
                    .Where(x => x.Status == true)
                    .OrderBy(x => x.AccNo)
                    .ToListAsync();

                ViewBag.CompanyCode = User.FindFirst("CompanyCode")?.Value ?? "001";
                ViewBag.CompanyName = User.FindFirst("CompanyName")?.Value ?? "Main SACCO";

                var firstJournal = journals.First();
                var model = new JournalEntryViewModel
                {
                    VoucherNo = voucherNo,
                    VoucherDate = firstJournal.TRANSDATE ?? DateTime.Now,
                    Description = firstJournal.NARATION,
                    MemberNo = firstJournal.MEMBERNO,
                    LoanNo = firstJournal.Loanno,
                    CompanyCode = firstJournal.CompanyCode,
                    TransactionNo = firstJournal.Transactionno,
                    AuditId = firstJournal.AUDITID,
                    AuditDate = firstJournal.AUDITDATE,
                    Posted = firstJournal.POSTED,
                    PostedDate = firstJournal.POSTEDDATE ?? DateTime.MinValue,
                    Details = journals.Select(j => new JournalDetailViewModel
                    {
                        AccountNo = j.ACCNO ?? "",
                        AccountName = j.NAME,
                        Narration = j.NARATION,
                        ShareType = j.SHARETYPE,
                        Debit = j.TRANSTYPE == "DR" ? j.AMOUNT ?? 0 : 0,
                        Credit = j.TRANSTYPE == "CR" ? j.AMOUNT ?? 0 : 0
                    }).ToList()
                };

                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error viewing journal {voucherNo}");
                TempData["Error"] = $"Error loading journal: {ex.Message}";
                return RedirectToAction("List");
            }
        }
        // ===============================
// POST: /Journal/Delete
// POST: /Journal/Delete/{voucherNo}
// ===============================
[HttpPost("Delete")]
[HttpPost("Delete/{voucherNo}")]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Admin,SuperAdmin")]
public async Task<IActionResult> Delete(string voucherNo)
{
    if (string.IsNullOrEmpty(voucherNo))
    {
        return Json(new { success = false, message = "Voucher number is required." });
    }
    
    try
    {
        var journals = await _context.Journals
            .Where(x => x.VNO == voucherNo)
            .ToListAsync();

        if (!journals.Any())
        {
            return Json(new { success = false, message = "Journal not found." });
        }

        if (journals.First().POSTED)
        {
            return Json(new { success = false, message = "Cannot delete posted journal!" });
        }

        _context.Journals.RemoveRange(journals);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Journal deleted successfully. Voucher No: {voucherNo}");
        return Json(new { success = true, message = "Journal deleted successfully!" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error deleting journal {voucherNo}");
        return Json(new { success = false, message = $"Error deleting journal: {ex.Message}" });
    }
}
        // ===============================
        // Helpers
        // ===============================
        private async Task<string> GenerateVoucherNumberAsync()
        {
            string companyCode = User.FindFirst("CompanyCode")?.Value ?? "001";
            string date = DateTime.Now.ToString("yyyyMMdd");

            var last = await _context.Journals
                .Where(x => x.VNO!.StartsWith($"JV-{companyCode}-{date}"))
                .OrderByDescending(x => x.VNO)
                .Select(x => x.VNO)
                .FirstOrDefaultAsync();

            int seq = 1;
            if (last != null && int.TryParse(last.Split('-').Last(), out int n))
                seq = n + 1;

            return $"JV-{companyCode}-{date}-{seq:D4}";
        }
    }
}