using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Controllers
{
	[Authorize]
	public class GIGReportController : Controller
	{
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GIGReportController> _logger;
        private readonly ICompanyContextService _companyContextService;
        private readonly IMemberService _memberService;

        public GIGReportController(
            ApplicationDbContext context,
            ILogger<GIGReportController> logger,
            ICompanyContextService companyContextService,
            IMemberService memberService)
        {
            _context = context;
            _logger = logger;
            _companyContextService = companyContextService;
            _memberService = memberService;
        }

        private string GetUserCompanyCode()
        {
            var companyCode = _companyContextService.GetCurrentCompanyCode();
            if (string.IsNullOrEmpty(companyCode))
            {
                companyCode = HttpContext.Session.GetString("CompanyCode");
            }

            if (string.IsNullOrEmpty(companyCode))
            {
                throw new Exception("Company code not found. Please log in again.");
            }

            return companyCode;
        }

        private string GetCompanyNameFromCode(string companyCode)
        {
            if (string.IsNullOrEmpty(companyCode))
                return null;

            var company = _context.Companies
                .FirstOrDefault(c => c.CompanyCode == companyCode);

            return company?.CompanyName;
        }

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        [HttpGet]
		public IActionResult Index()
		{
			var companyCode = _companyContextService.GetCurrentCompanyCode();
			// FIXED: Get company name from the Companies table using the company code
			var companyName = GetCompanyNameFromCode(companyCode) ?? "";
			var endDate = DateTime.Now;
			var startDate = endDate.AddMonths(-1);

			var viewModel = new GIGReportIndexViewModel
			{
				GIGs = new List<GIGReportViewModel>(),
				StartDate = startDate,
				EndDate = endDate,
				HasData = false,
				UserCompanyCode = companyCode,
				CompanyName = companyName,
				TotalGIGs = 0,
				TotalGIGMembers = 0,
				TotalMaleMembers = 0,
				TotalFemaleMembers = 0,
				TotalYouthMembers = 0,
				TotalShareCapitalAllGIGs = 0,
				TotalShareDepositsAllGIGs = 0,
				TotalRegFeeAllGIGs = 0,
				TotalLoansAllGIGs = 0
			};

			ViewBag.StartDate = startDate;
			ViewBag.EndDate = endDate;
			ViewBag.CompanyName = companyName;

			return View("~/Views/Reports/GIGReport.cshtml", viewModel);
		}

        [HttpPost]
        public async Task<IActionResult> GenerateReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var companyName = GetCompanyNameFromCode(companyCode) ?? "";
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                var gigs = await _context.CIGs
                    .Where(g => g.CompanyCode == companyCode && g.Status == "Active")
                    .OrderBy(g => g.GigName)
                    .ToListAsync();

                var gigReports = new List<GIGReportViewModel>();
                int totalGIGs = 0, totalGIGMembers = 0, totalMaleMembers = 0, totalFemaleMembers = 0, totalYouthMembers = 0;
                decimal totalShareCapitalAllGIGs = 0, totalShareDepositsAllGIGs = 0, totalRegFeeAllGIGs = 0, totalLoansAllGIGs = 0;

                foreach (var gig in gigs)
                {
                    var members = await _context.Members
                        .Where(m => m.CompanyCode == companyCode && m.Cigcode == gig.GigCode
                            && (m.Withdrawn == false || m.Withdrawn == null)
                            && (m.Archived == false || m.Archived == null))
                        .ToListAsync();

                    if (!members.Any()) continue;

                    var memberNos = members.Select(m => m.MemberNo.Trim()).ToList();

                    // STEP 1: Get raw contributions WITHOUT Trim in SQL
                    var contributionsRaw = await _context.ContribShares
                        .Where(c => c.CompanyCode == companyCode
                            && c.MemberNo != null
                            && c.ContrDate >= startDate
                            && c.ContrDate <= endDateAdjusted)
                        .Select(c => new
                        {
                            MemberNo = c.MemberNo,
                            c.ShareCapitalAmount,
                            c.DepositsAmount,
                            c.RegFeeAmount
                        })
                        .ToListAsync();

                    // STEP 2: Normalize in memory (SAFE)
                    var contributions = contributionsRaw
                        .GroupBy(c => c.MemberNo.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => new
                        {
                            TotalShareCapital = g.Sum(x => x.ShareCapitalAmount ?? 0),
                            TotalDeposits = g.Sum(x => x.DepositsAmount ?? 0),
                            TotalRegFee = g.Sum(x => x.RegFeeAmount ?? 0)
                        }, StringComparer.OrdinalIgnoreCase);

                    // IMPORTANT FIX: Get SUM of AmtRecommended from Appraisal for each member
                    // This handles multiple loans per member by summing all AmtRecommended
                    var recommendedLoans = await _context.Appraisal
                        .Where(a => memberNos.Contains(a.MemberNo.Trim())
                            && a.CompanyCode == companyCode
                            && a.AmtRecommended.HasValue
                            && a.AmtRecommended > 0)
                        .GroupBy(a => a.MemberNo.Trim())
                        .Select(g => new
                        {
                            MemberNo = g.Key,
                            TotalAmtRecommended = g.Sum(a => a.AmtRecommended ?? 0)
                        })
                        .ToDictionaryAsync(a => a.MemberNo, a => a.TotalAmtRecommended);

                    var memberDetails = new List<GIGReportMemberDetail>();
                    int maleCount = 0, femaleCount = 0, youthCount = 0;
                    decimal totalShareCapital = 0, totalShareDepositAmount = 0, totalRegFeeAmount = 0, totalLoanAmount = 0;

                    foreach (var member in members)
                    {
                        string trimmedMemberNo = member.MemberNo?.Trim() ?? "";

                        string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                        int? age = null;
                        if (member.Dob.HasValue)
                        {
                            age = DateTime.Today.Year - member.Dob.Value.Year;
                            if (member.Dob.Value.Date > DateTime.Today.AddYears(-age.Value)) age--;
                            if (age >= 18 && age <= 35) youthCount++;
                        }

                        if (member.Sex?.ToUpper() == "MALE" || member.Sex?.ToUpper() == "M") maleCount++;
                        else if (member.Sex?.ToUpper() == "FEMALE" || member.Sex?.ToUpper() == "F") femaleCount++;

                        // Get values from ContribShare
                        decimal shareCapital = contributions.ContainsKey(trimmedMemberNo)
                             ? contributions[trimmedMemberNo].TotalShareCapital : 0;

                        decimal shareDeposit = contributions.ContainsKey(trimmedMemberNo)
                            ? contributions[trimmedMemberNo].TotalDeposits : 0;

                        decimal regFee = contributions.ContainsKey(trimmedMemberNo)
                            ? contributions[trimmedMemberNo].TotalRegFee : 0;

                        // IMPORTANT FIX: Get SUM of AmtRecommended from Appraisal
                        decimal recommendedLoanAmt = recommendedLoans.ContainsKey(trimmedMemberNo)
                            ? recommendedLoans[trimmedMemberNo] : 0;

                        // Debug logging to see what's happening
                        if (recommendedLoanAmt > 0)
                        {
                            _logger.LogInformation($"Member {trimmedMemberNo} has recommended loan amount: {recommendedLoanAmt}");
                        }
                        else
                        {
                            // Check if member has appraisals but maybe AmtRecommended is null
                            var hasAppraisal = await _context.Appraisal
                                .AnyAsync(a => a.MemberNo.Trim() == trimmedMemberNo && a.CompanyCode == companyCode);
                            if (hasAppraisal)
                            {
                                _logger.LogWarning($"Member {trimmedMemberNo} has appraisal record but AmtRecommended is null or zero");
                            }
                        }

                        totalShareCapital += shareCapital;
                        totalShareDepositAmount += shareDeposit;
                        totalRegFeeAmount += regFee;
                        totalLoanAmount += recommendedLoanAmt; // Using recommended loan amount for totals

                        memberDetails.Add(new GIGReportMemberDetail
                        {
                            MemberNo = member.MemberNo,
                            Names = fullName,
                            Sex = member.Sex ?? "Not Specified",
                            PhoneNo = member.PhoneNo ?? member.MobileNo ?? "-",
                            IDNo = member.Idno ?? "-",
                            Age = age,
                            CIGCode = gig.GigCode,
                            CIGName = gig.GigName,
                            ShareCapital = shareCapital,
                            ShareDeposits = shareDeposit,
                            RegFee = regFee,
                            LoanAmt = recommendedLoanAmt // This is the AmtRecommended sum
                        });
                    }

                    totalGIGs++;
                    totalGIGMembers += memberDetails.Count;
                    totalMaleMembers += maleCount;
                    totalFemaleMembers += femaleCount;
                    totalYouthMembers += youthCount;
                    totalShareCapitalAllGIGs += totalShareCapital;
                    totalShareDepositsAllGIGs += totalShareDepositAmount;
                    totalRegFeeAllGIGs += totalRegFeeAmount;
                    totalLoansAllGIGs += totalLoanAmount;

                    gigReports.Add(new GIGReportViewModel
                    {
                        CIGCode = gig.GigCode,
                        CIGName = gig.GigName,
                        TotalMembers = memberDetails.Count,
                        MaleCount = maleCount,
                        FemaleCount = femaleCount,
                        YouthCount = youthCount,
                        TotalShareCapital = totalShareCapital,
                        TotalShareDeposits = totalShareDepositAmount,
                        TotalRegFee = totalRegFeeAmount,
                        TotalLoans = totalLoanAmount, // This is the sum of recommended loan amounts
                        Members = memberDetails,
                        CompanyCode = companyCode,
                        CompanyName = companyName
                    });
                }

                var viewModel = new GIGReportIndexViewModel
                {
                    GIGs = gigReports,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = gigReports.Any(),
                    UserCompanyCode = companyCode,
                    CompanyName = companyName,
                    TotalGIGs = totalGIGs,
                    TotalGIGMembers = totalGIGMembers,
                    TotalMaleMembers = totalMaleMembers,
                    TotalFemaleMembers = totalFemaleMembers,
                    TotalYouthMembers = totalYouthMembers,
                    TotalShareCapitalAllGIGs = totalShareCapitalAllGIGs,
                    TotalShareDepositsAllGIGs = totalShareDepositsAllGIGs,
                    TotalRegFeeAllGIGs = totalRegFeeAllGIGs,
                    TotalLoansAllGIGs = totalLoansAllGIGs
                };

                return View("~/Views/Reports/GIGReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating GIG report");
                TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ExportToExcel(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var companyName = GetCompanyNameFromCode(companyCode) ?? "";
                var printedBy = User.Identity?.Name ?? "System";
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // ============================================================
                // STEP 1: Get all active ShareTypes for this company
                // ============================================================
                var shareTypes = await _context.Sharetypes
                    .Where(s => s.CompanyCode == companyCode)
                    .OrderBy(s => s.Priority)
                    .ThenBy(s => s.SharesType)
                    .ToListAsync();

                _logger.LogInformation($"Found {shareTypes.Count} ShareTypes");

                // ============================================================
                // STEP 2: Get all active GIGs
                // ============================================================
                var gigs = await _context.CIGs
                    .Where(g => g.CompanyCode == companyCode && g.Status == "Active")
                    .OrderBy(g => g.GigName)
                    .ToListAsync();

                // Get all months between start and end date
                var months = new List<DateTime>();
                var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
                var endMonth = new DateTime(endDate.Year, endDate.Month, 1);
                while (currentMonth <= endMonth)
                {
                    months.Add(currentMonth);
                    currentMonth = currentMonth.AddMonths(1);
                }

                using var workbook = new XLWorkbook();

                // ============================================================
                // SHEET 1: GIG SUMMARY
                // ============================================================
                var summarySheet = workbook.Worksheets.Add("GIG Summary");
                int summaryRow = 1;

                // Company Header
                summarySheet.Cell(summaryRow, 1).Value = companyName.ToUpper();
                summarySheet.Range(summaryRow, 1, summaryRow, 15).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                // Printed By
                summarySheet.Cell(summaryRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                summarySheet.Range(summaryRow, 1, summaryRow, 15).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetItalic();
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                // Report Title
                summarySheet.Cell(summaryRow, 1).Value = $"GIGs SUMMARY REPORT - {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";
                summarySheet.Range(summaryRow, 1, summaryRow, 15).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                // Build Summary Headers with ShareTypes
                var summaryHeaders = new List<string> { "GIG Code", "GIG Name", "Total Members", "Male", "Female", "Other", "Youth" };
                foreach (var st in shareTypes)
                {
                    summaryHeaders.Add(st.SharesType ?? st.SharesCode);
                }
                summaryHeaders.Add("GRAND TOTAL");

                for (int i = 0; i < summaryHeaders.Count; i++)
                {
                    summarySheet.Cell(summaryRow, i + 1).Value = summaryHeaders[i];
                    summarySheet.Cell(summaryRow, i + 1).Style.Font.SetBold();
                    summarySheet.Cell(summaryRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    summarySheet.Cell(summaryRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    summarySheet.Cell(summaryRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                summaryRow++;

                // Process each GIG for Summary
                decimal grandTotalAllGIGs = 0;
                int grandTotalMembers = 0;
                int grandTotalMale = 0;
                int grandTotalFemale = 0;
                int grandTotalOther = 0;
                int grandTotalYouth = 0;
                var grandTotalsPerShareType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                foreach (var shareType in shareTypes)
                {
                    grandTotalsPerShareType[shareType.SharesCode] = 0;
                }

                foreach (var gig in gigs)
                {
                    // Get members in this GIG
                    var members = await _context.Members
                        .Where(m => m.CompanyCode == companyCode
                            && m.Cigcode == gig.GigCode
                            && (m.Withdrawn == false || m.Withdrawn == null)
                            && (m.Archived == false || m.Archived == null))
                        .ToListAsync();

                    if (!members.Any()) continue;

                    var memberNos = members.Select(m => m.MemberNo.Trim()).ToList();

                    // Get contributions for this GIG's members
                    var contributionsRaw = await _context.Contribs
                        .Where(c => c.CompanyCode == companyCode
                            && c.MemberNo != null
                            && c.ContrDate >= startDate
                            && c.ContrDate <= endDateAdjusted
                            && c.Sharescode != null
                            && c.Amount > 0)
                        .Select(c => new
                        {
                            MemberNo = c.MemberNo,
                            SharesCode = c.Sharescode,
                            Amount = c.Amount ?? 0
                        })
                        .ToListAsync();

                    // Group contributions by Member and ShareType
                    var memberTotals = contributionsRaw
                        .Where(c => memberNos.Contains(c.MemberNo.Trim(), StringComparer.OrdinalIgnoreCase))
                        .GroupBy(c => new { MemberNo = c.MemberNo.Trim(), SharesCode = c.SharesCode?.Trim() ?? "" })
                        .Select(g => new
                        {
                            g.Key.MemberNo,
                            g.Key.SharesCode,
                            TotalAmount = g.Sum(x => x.Amount)
                        })
                        .GroupBy(x => x.MemberNo)
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(x => x.SharesCode, x => x.TotalAmount),
                            StringComparer.OrdinalIgnoreCase
                        );

                    // Calculate GIG totals
                    var gigTotalsPerShareType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    foreach (var st in shareTypes)
                    {
                        gigTotalsPerShareType[st.SharesCode] = 0;
                    }

                    foreach (var member in members)
                    {
                        string trimmedMemberNo = member.MemberNo?.Trim() ?? "";
                        if (memberTotals.ContainsKey(trimmedMemberNo))
                        {
                            foreach (var st in shareTypes)
                            {
                                if (memberTotals[trimmedMemberNo].ContainsKey(st.SharesCode))
                                {
                                    gigTotalsPerShareType[st.SharesCode] += memberTotals[trimmedMemberNo][st.SharesCode];
                                }
                            }
                        }
                    }

                    // Calculate demographic counts - INCLUDING OTHER
                    int maleCount = members.Count(m => m.Sex?.ToUpper() == "MALE" || m.Sex?.ToUpper() == "M");
                    int femaleCount = members.Count(m => m.Sex?.ToUpper() == "FEMALE" || m.Sex?.ToUpper() == "F");
                    int otherCount = members.Count - maleCount - femaleCount; // Any gender not Male or Female
                    int youthCount = members.Count(m => m.Dob.HasValue && CalculateAge(m.Dob.Value) >= 18 && CalculateAge(m.Dob.Value) <= 35);

                    decimal gigGrandTotal = gigTotalsPerShareType.Values.Sum();

                    // Write GIG summary row
                    int col = 1;
                    summarySheet.Cell(summaryRow, col++).Value = gig.GigCode;
                    summarySheet.Cell(summaryRow, col++).Value = gig.GigName;
                    summarySheet.Cell(summaryRow, col++).Value = members.Count;
                    summarySheet.Cell(summaryRow, col++).Value = maleCount;
                    summarySheet.Cell(summaryRow, col++).Value = femaleCount;
                    summarySheet.Cell(summaryRow, col++).Value = otherCount;
                    summarySheet.Cell(summaryRow, col++).Value = youthCount;

                    foreach (var st in shareTypes)
                    {
                        decimal amount = gigTotalsPerShareType.ContainsKey(st.SharesCode) ? gigTotalsPerShareType[st.SharesCode] : 0;
                        summarySheet.Cell(summaryRow, col).Value = amount;
                        summarySheet.Cell(summaryRow, col).Style.NumberFormat.Format = "#,##0.00";
                        col++;
                        // Add to grand totals
                        grandTotalsPerShareType[st.SharesCode] += amount;
                    }

                    summarySheet.Cell(summaryRow, col).Value = gigGrandTotal;
                    summarySheet.Cell(summaryRow, col).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(summaryRow, col).Style.Font.SetBold();

                    grandTotalAllGIGs += gigGrandTotal;
                    grandTotalMembers += members.Count;
                    grandTotalMale += maleCount;
                    grandTotalFemale += femaleCount;
                    grandTotalOther += otherCount;
                    grandTotalYouth += youthCount;

                    summaryRow++;
                }

                // Grand Total Row
                summaryRow++;
                summarySheet.Cell(summaryRow, 2).Value = "GRAND TOTAL";
                summarySheet.Cell(summaryRow, 2).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 3).Value = grandTotalMembers;
                summarySheet.Cell(summaryRow, 3).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 4).Value = grandTotalMale;
                summarySheet.Cell(summaryRow, 4).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 5).Value = grandTotalFemale;
                summarySheet.Cell(summaryRow, 5).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 6).Value = grandTotalOther;
                summarySheet.Cell(summaryRow, 6).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 7).Value = grandTotalYouth;
                summarySheet.Cell(summaryRow, 7).Style.Font.SetBold();

                int grandCol = 8;
                foreach (var st in shareTypes)
                {
                    summarySheet.Cell(summaryRow, grandCol).Value = grandTotalsPerShareType[st.SharesCode];
                    summarySheet.Cell(summaryRow, grandCol).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(summaryRow, grandCol).Style.Font.SetBold();
                    grandCol++;
                }

                summarySheet.Cell(summaryRow, grandCol).Value = grandTotalAllGIGs;
                summarySheet.Cell(summaryRow, grandCol).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(summaryRow, grandCol).Style.Font.SetBold();

                summarySheet.Columns().AdjustToContents();

                // ============================================================
                // SHEET 2: MONTHLY CONTRIBUTIONS (One page, totals at bottom)
                // ============================================================
                var detailSheet = workbook.Worksheets.Add("Monthly Contributions");
                int detailRow = 1;

                // Global Header
                detailSheet.Cell(detailRow, 1).Value = companyName.ToUpper();
                int totalColumns = 7 + (months.Count * shareTypes.Count) + shareTypes.Count + 1;
                detailSheet.Range(detailRow, 1, detailRow, totalColumns).Merge();
                detailSheet.Cell(detailRow, 1).Style.Font.SetBold().Font.SetFontSize(16);
                detailSheet.Cell(detailRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                detailRow += 2;

                detailSheet.Cell(detailRow, 1).Value = $"MONTHLY CONTRIBUTIONS REPORT - {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";
                detailSheet.Range(detailRow, 1, detailRow, totalColumns).Merge();
                detailSheet.Cell(detailRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                detailSheet.Cell(detailRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                detailRow += 2;

                detailSheet.Cell(detailRow, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm} | Printed By: {printedBy}";
                detailSheet.Range(detailRow, 1, detailRow, totalColumns).Merge();
                detailSheet.Cell(detailRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                detailRow += 2;

                // Process each GIG
                foreach (var gig in gigs)
                {
                    // Get members in this GIG
                    var members = await _context.Members
                        .Where(m => m.CompanyCode == companyCode
                            && m.Cigcode == gig.GigCode
                            && (m.Withdrawn == false || m.Withdrawn == null)
                            && (m.Archived == false || m.Archived == null))
                        .ToListAsync();

                    if (!members.Any()) continue;

                    _logger.LogInformation($"Processing GIG: {gig.GigName} with {members.Count} members");

                    var memberNos = members.Select(m => m.MemberNo.Trim()).ToList();

                    // ============================================================
                    // GET CONTRIBUTIONS
                    // ============================================================
                    var contributionsRaw = await _context.Contribs
                        .Where(c => c.CompanyCode == companyCode
                            && c.MemberNo != null
                            && c.ContrDate >= startDate
                            && c.ContrDate <= endDateAdjusted
                            && c.Sharescode != null
                            && c.Amount > 0)
                        .Select(c => new
                        {
                            MemberNo = c.MemberNo,
                            SharesCode = c.Sharescode,
                            Amount = c.Amount ?? 0,
                            ContrDate = c.ContrDate ?? DateTime.Now
                        })
                        .ToListAsync();

                    // Group contributions by Member, ShareType, Month
                    var memberMonthlyData = contributionsRaw
                        .Where(c => memberNos.Contains(c.MemberNo.Trim(), StringComparer.OrdinalIgnoreCase))
                        .GroupBy(c => new
                        {
                            MemberNo = c.MemberNo.Trim(),
                            SharesCode = c.SharesCode?.Trim() ?? "",
                            YearMonth = new DateTime(c.ContrDate.Year, c.ContrDate.Month, 1)
                        })
                        .Select(g => new
                        {
                            g.Key.MemberNo,
                            g.Key.SharesCode,
                            g.Key.YearMonth,
                            TotalAmount = g.Sum(x => x.Amount)
                        })
                        .ToList();

                    // Create lookup for monthly data
                    var contributionLookup = memberMonthlyData
                        .GroupBy(x => x.MemberNo)
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(
                                x => $"{x.YearMonth:MMM yyyy}_{x.SharesCode}",
                                x => x.TotalAmount
                            ),
                            StringComparer.OrdinalIgnoreCase
                        );

                    // ============================================================
                    // CIG HEADER
                    // ============================================================
                    detailSheet.Cell(detailRow, 1).Value = $"CIG: {gig.GigCode} - {gig.GigName}";
                    detailSheet.Range(detailRow, 1, detailRow, totalColumns).Merge();
                    detailSheet.Cell(detailRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                    detailSheet.Cell(detailRow, 1).Style.Fill.SetBackgroundColor(XLColor.FromArgb(0, 52, 73, 94));
                    detailSheet.Cell(detailRow, 1).Style.Font.FontColor = XLColor.White;
                    detailSheet.Cell(detailRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    detailRow++;

                    // CIG Contact Details
                    detailSheet.Cell(detailRow, 1).Value = "Contact:";
                    detailSheet.Cell(detailRow, 2).Value = gig.ContactPhone ?? "N/A";
                    detailSheet.Cell(detailRow, 3).Value = "Email:";
                    detailSheet.Cell(detailRow, 4).Value = gig.ContactEmail ?? "N/A";
                    detailSheet.Cell(detailRow, 5).Value = "Chairperson:";
                    detailSheet.Cell(detailRow, 6).Value = gig.Chairperson ?? "N/A";
                    detailSheet.Cell(detailRow, 7).Value = $"Total Members: {members.Count}";
                    detailSheet.Range(detailRow, 1, detailRow, totalColumns).Style.Font.SetFontSize(9);
                    detailRow += 2;

                    // ============================================================
                    // TABLE HEADERS - Month Headers Row (merged across share types)
                    // ============================================================
                    int headerRow = detailRow;
                    int headerCol = 1;

                    // Fixed headers
                    string[] fixedHeaders = { "#", "MemberNo", "Names", "Sex", "Phone", "IDNo", "Age" };
                    foreach (var header in fixedHeaders)
                    {
                        detailSheet.Cell(headerRow, headerCol).Value = header;
                        detailSheet.Cell(headerRow, headerCol).Style.Font.SetBold();
                        detailSheet.Cell(headerRow, headerCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        detailSheet.Cell(headerRow, headerCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        detailSheet.Cell(headerRow, headerCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        headerCol++;
                    }

                    // Month headers (merged across share types)
                    foreach (var month in months)
                    {
                        string monthLabel = month.ToString("MMM yyyy");
                        int shareTypeCount = shareTypes.Count;
                        if (shareTypeCount > 0)
                        {
                            detailSheet.Cell(headerRow, headerCol).Value = monthLabel;
                            detailSheet.Range(headerRow, headerCol, headerRow, headerCol + shareTypeCount - 1).Merge();
                            detailSheet.Cell(headerRow, headerCol).Style.Font.SetBold();
                            detailSheet.Cell(headerRow, headerCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                            detailSheet.Cell(headerRow, headerCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                            detailSheet.Cell(headerRow, headerCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            headerCol += shareTypeCount;
                        }
                    }

                    // Total headers
                    foreach (var st in shareTypes)
                    {
                        detailSheet.Cell(headerRow, headerCol).Value = "TOTAL";
                        detailSheet.Cell(headerRow, headerCol).Style.Font.SetBold();
                        detailSheet.Cell(headerRow, headerCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        detailSheet.Cell(headerRow, headerCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        detailSheet.Cell(headerRow, headerCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        headerCol++;
                    }

                    // Grand Total header
                    detailSheet.Cell(headerRow, headerCol).Value = "GRAND TOTAL";
                    detailSheet.Cell(headerRow, headerCol).Style.Font.SetBold();
                    detailSheet.Cell(headerRow, headerCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    detailSheet.Cell(headerRow, headerCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    detailSheet.Cell(headerRow, headerCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    detailRow++;

                    // ============================================================
                    // SHARETYPE HEADERS ROW
                    // ============================================================
                    int shareTypeRow = detailRow;
                    int shareTypeCol = 1;

                    // Fixed headers (empty for alignment)
                    foreach (var header in fixedHeaders)
                    {
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Value = "";
                        shareTypeCol++;
                    }

                    // ShareType headers below each month
                    foreach (var month in months)
                    {
                        foreach (var st in shareTypes)
                        {
                            string displayName = st.SharesType ?? st.SharesCode;
                            detailSheet.Cell(shareTypeRow, shareTypeCol).Value = displayName;
                            detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Font.SetBold().Font.SetFontSize(7);
                            detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                            detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                            detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            shareTypeCol++;
                        }
                    }

                    // Total ShareType headers
                    foreach (var st in shareTypes)
                    {
                        string displayName = st.SharesType ?? st.SharesCode;
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Value = displayName;
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Font.SetBold().Font.SetFontSize(7);
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        shareTypeCol++;
                    }

                    // Grand Total header
                    detailSheet.Cell(shareTypeRow, shareTypeCol).Value = "TOTAL";
                    detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Font.SetBold().Font.SetFontSize(7);
                    detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    detailSheet.Cell(shareTypeRow, shareTypeCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    detailRow++;

                    // ============================================================
                    // MEMBER DATA
                    // ============================================================
                    int serialNo = 1;
                    var cigTotalsPerMonthShareType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                    // Initialize totals dictionary
                    foreach (var month in months)
                    {
                        string monthKey = month.ToString("MMM yyyy");
                        foreach (var st in shareTypes)
                        {
                            cigTotalsPerMonthShareType[$"{monthKey}_{st.SharesCode}"] = 0;
                        }
                    }

                    var cigTotalsPerShareType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    foreach (var st in shareTypes)
                    {
                        cigTotalsPerShareType[st.SharesCode] = 0;
                    }

                    // Process each member
                    foreach (var member in members)
                    {
                        string trimmedMemberNo = member.MemberNo?.Trim() ?? "";
                        string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                        int? age = member.Dob.HasValue ? CalculateAge(member.Dob.Value) : (int?)null;

                        int dataCol = 1;

                        // Fixed columns
                        detailSheet.Cell(detailRow, dataCol++).Value = serialNo++;
                        detailSheet.Cell(detailRow, dataCol++).Value = member.MemberNo;
                        detailSheet.Cell(detailRow, dataCol++).Value = fullName;
                        detailSheet.Cell(detailRow, dataCol++).Value = member.Sex ?? "-";
                        detailSheet.Cell(detailRow, dataCol++).Value = member.PhoneNo ?? member.MobileNo ?? "-";
                        detailSheet.Cell(detailRow, dataCol++).Value = member.Idno ?? "-";
                        detailSheet.Cell(detailRow, dataCol++).Value = age ?? 0;

                        // Monthly data
                        var memberData = contributionLookup.ContainsKey(trimmedMemberNo)
                            ? contributionLookup[trimmedMemberNo]
                            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                        var totalsPerShareType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                        foreach (var st in shareTypes)
                        {
                            totalsPerShareType[st.SharesCode] = 0;
                        }

                        foreach (var month in months)
                        {
                            string monthKey = month.ToString("MMM yyyy");
                            foreach (var st in shareTypes)
                            {
                                string lookupKey = $"{monthKey}_{st.SharesCode}";
                                decimal amount = memberData.ContainsKey(lookupKey) ? memberData[lookupKey] : 0;

                                detailSheet.Cell(detailRow, dataCol++).Value = amount;
                                detailSheet.Cell(detailRow, dataCol - 1).Style.NumberFormat.Format = "#,##0.00";

                                // Add to member total
                                totalsPerShareType[st.SharesCode] += amount;

                                // Add to CIG totals per month-sharetype
                                cigTotalsPerMonthShareType[lookupKey] += amount;
                            }
                        }

                        // Totals per ShareType for this member
                        foreach (var st in shareTypes)
                        {
                            decimal total = totalsPerShareType.ContainsKey(st.SharesCode) ? totalsPerShareType[st.SharesCode] : 0;
                            detailSheet.Cell(detailRow, dataCol++).Value = total;
                            detailSheet.Cell(detailRow, dataCol - 1).Style.NumberFormat.Format = "#,##0.00";

                            // Add to CIG totals per sharetype
                            cigTotalsPerShareType[st.SharesCode] += total;
                        }

                        // Grand Total for this member
                        decimal grandTotal = totalsPerShareType.Values.Sum();
                        detailSheet.Cell(detailRow, dataCol++).Value = grandTotal;
                        detailSheet.Cell(detailRow, dataCol - 1).Style.NumberFormat.Format = "#,##0.00";

                        detailRow++;
                    }

                    // ============================================================
                    // TOTALS ROW AT THE BOTTOM
                    // ============================================================
                    int totalRow = detailRow;

                    // Fixed columns (empty for alignment)
                    int totalCol = 1;
                    detailSheet.Cell(totalRow, totalCol++).Value = "";
                    detailSheet.Cell(totalRow, totalCol++).Value = "";
                    detailSheet.Cell(totalRow, totalCol++).Value = "CIG TOTALS:";
                    detailSheet.Cell(totalRow, totalCol - 1).Style.Font.SetBold();
                    detailSheet.Cell(totalRow, totalCol - 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    detailSheet.Cell(totalRow, totalCol++).Value = "";
                    detailSheet.Cell(totalRow, totalCol++).Value = "";
                    detailSheet.Cell(totalRow, totalCol++).Value = "";
                    detailSheet.Cell(totalRow, totalCol++).Value = "";

                    // Monthly totals
                    foreach (var month in months)
                    {
                        string monthKey = month.ToString("MMM yyyy");
                        foreach (var st in shareTypes)
                        {
                            string lookupKey = $"{monthKey}_{st.SharesCode}";
                            decimal amount = cigTotalsPerMonthShareType.ContainsKey(lookupKey) ? cigTotalsPerMonthShareType[lookupKey] : 0;
                            detailSheet.Cell(totalRow, totalCol++).Value = amount;
                            detailSheet.Cell(totalRow, totalCol - 1).Style.NumberFormat.Format = "#,##0.00";
                            detailSheet.Cell(totalRow, totalCol - 1).Style.Font.SetBold();
                            detailSheet.Cell(totalRow, totalCol - 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                        }
                    }

                    // Totals per ShareType
                    foreach (var st in shareTypes)
                    {
                        decimal amount = cigTotalsPerShareType.ContainsKey(st.SharesCode) ? cigTotalsPerShareType[st.SharesCode] : 0;
                        detailSheet.Cell(totalRow, totalCol++).Value = amount;
                        detailSheet.Cell(totalRow, totalCol - 1).Style.NumberFormat.Format = "#,##0.00";
                        detailSheet.Cell(totalRow, totalCol - 1).Style.Font.SetBold();
                        detailSheet.Cell(totalRow, totalCol - 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    }

                    // Grand Total for CIG
                    decimal cigGrandTotal = cigTotalsPerShareType.Values.Sum();
                    detailSheet.Cell(totalRow, totalCol++).Value = cigGrandTotal;
                    detailSheet.Cell(totalRow, totalCol - 1).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(totalRow, totalCol - 1).Style.Font.SetBold();
                    detailSheet.Cell(totalRow, totalCol - 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);

                    detailRow += 2; // Add blank row after totals
                }

                // ============================================================
                // FORMAT THE DETAIL SHEET - No frozen panes
                // ============================================================
                detailSheet.Columns().AdjustToContents();
                detailSheet.Rows().AdjustToContents();

                // ============================================================
                // SHEET 3: SHARE TYPE SUMMARY (Keep as is)
                // ============================================================
                var summary2Sheet = workbook.Worksheets.Add("ShareType Summary");
                int summary2Row = 1;

                summary2Sheet.Cell(summary2Row, 1).Value = "SHARETYPE SUMMARY";
                summary2Sheet.Range(summary2Row, 1, summary2Row, 6).Merge();
                summary2Sheet.Cell(summary2Row, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summary2Sheet.Cell(summary2Row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summary2Row += 2;

                string[] stHeaders = { "ShareType Code", "ShareType Name", "Is Main Shares", "Used To Guarantee", "Used To Offset", "Withdrawable" };
                for (int i = 0; i < stHeaders.Length; i++)
                {
                    summary2Sheet.Cell(summary2Row, i + 1).Value = stHeaders[i];
                    summary2Sheet.Cell(summary2Row, i + 1).Style.Font.SetBold();
                    summary2Sheet.Cell(summary2Row, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                }
                summary2Row++;

                foreach (var st in shareTypes)
                {
                    summary2Sheet.Cell(summary2Row, 1).Value = st.SharesCode;
                    summary2Sheet.Cell(summary2Row, 2).Value = st.SharesType;
                    summary2Sheet.Cell(summary2Row, 3).Value = st.IsMainShares ? "Yes" : "No";
                    summary2Sheet.Cell(summary2Row, 4).Value = st.UsedToGuarantee ? "Yes" : "No";
                    summary2Sheet.Cell(summary2Row, 5).Value = st.UsedToOffset ? "Yes" : "No";
                    summary2Sheet.Cell(summary2Row, 6).Value = st.Withdrawable ? "Yes" : "No";
                    summary2Row++;
                }

                summary2Sheet.Columns().AdjustToContents();

                // ============================================================
                // SAVE AND RETURN
                // ============================================================
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"GIG_Report_Monthly_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting GIG report to Excel");
                TempData["ErrorMessage"] = $"Error exporting to Excel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ExportToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var companyName = GetCompanyNameFromCode(companyCode) ?? "";
                var printedBy = User.Identity?.Name ?? "System";
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Get all active GIGs for this company
                var gigs = await _context.CIGs
                    .Where(g => g.CompanyCode == companyCode && g.Status == "Active")
                    .OrderBy(g => g.GigName)
                    .ToListAsync();

                var gigReports = new List<GIGReportViewModel>();

                foreach (var gig in gigs)
                {
                    var members = await _context.Members
                        .Where(m => m.CompanyCode == companyCode
                            && m.Cigcode == gig.GigCode
                            && (m.Withdrawn == false || m.Withdrawn == null)
                            && (m.Archived == false || m.Archived == null))
                        .ToListAsync();

                    if (!members.Any()) continue;

                    var memberNos = members.Select(m => m.MemberNo?.Trim() ?? "").ToList();

                    // STEP 1: Get raw contributions WITHOUT Trim in SQL (same as GenerateReport)
                    var contributionsRaw = await _context.ContribShares
                        .Where(c => c.CompanyCode == companyCode
                            && c.MemberNo != null
                            && c.ContrDate >= startDate
                            && c.ContrDate <= endDateAdjusted)
                        .Select(c => new
                        {
                            MemberNo = c.MemberNo,
                            c.ShareCapitalAmount,
                            c.DepositsAmount,
                            c.RegFeeAmount
                        })
                        .ToListAsync();

                    // STEP 2: Normalize in memory (same as GenerateReport)
                    var contributions = contributionsRaw
                        .Where(c => memberNos.Contains(c.MemberNo?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                        .GroupBy(c => c.MemberNo.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => new
                        {
                            TotalShareCapital = g.Sum(x => x.ShareCapitalAmount ?? 0),
                            TotalDeposits = g.Sum(x => x.DepositsAmount ?? 0),
                            TotalRegFee = g.Sum(x => x.RegFeeAmount ?? 0)
                        }, StringComparer.OrdinalIgnoreCase);

                    // Get recommended loans from Appraisal table (same as GenerateReport)
                    var recommendedLoans = await _context.Appraisal
                        .Where(a => memberNos.Contains(a.MemberNo.Trim())
                            && a.CompanyCode == companyCode
                            && a.AmtRecommended.HasValue
                            && a.AmtRecommended > 0)
                        .GroupBy(a => a.MemberNo.Trim())
                        .Select(g => new
                        {
                            MemberNo = g.Key,
                            TotalAmtRecommended = g.Sum(a => a.AmtRecommended ?? 0)
                        })
                        .ToDictionaryAsync(a => a.MemberNo, a => a.TotalAmtRecommended, StringComparer.OrdinalIgnoreCase);

                    var memberDetails = new List<GIGReportMemberDetail>();
                    int maleCount = 0, femaleCount = 0, youthCount = 0;
                    decimal totalShareCapital = 0, totalShareDepositAmount = 0, totalRegFeeAmount = 0, totalLoanAmount = 0;

                    foreach (var member in members)
                    {
                        string trimmedMemberNo = member.MemberNo?.Trim() ?? "";
                        string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                        int? age = null;
                        if (member.Dob.HasValue)
                        {
                            age = CalculateAge(member.Dob.Value);
                            if (age >= 18 && age <= 35) youthCount++;
                        }

                        if (member.Sex?.ToUpper() == "MALE" || member.Sex?.ToUpper() == "M")
                            maleCount++;
                        else if (member.Sex?.ToUpper() == "FEMALE" || member.Sex?.ToUpper() == "F")
                            femaleCount++;

                        decimal shareCapital = contributions.ContainsKey(trimmedMemberNo)
                            ? contributions[trimmedMemberNo].TotalShareCapital : 0;
                        decimal shareDeposit = contributions.ContainsKey(trimmedMemberNo)
                            ? contributions[trimmedMemberNo].TotalDeposits : 0;
                        decimal regFee = contributions.ContainsKey(trimmedMemberNo)
                            ? contributions[trimmedMemberNo].TotalRegFee : 0;
                        decimal loanAmt = recommendedLoans.ContainsKey(trimmedMemberNo)
                            ? recommendedLoans[trimmedMemberNo] : 0;

                        totalShareCapital += shareCapital;
                        totalShareDepositAmount += shareDeposit;
                        totalRegFeeAmount += regFee;
                        totalLoanAmount += loanAmt;

                        memberDetails.Add(new GIGReportMemberDetail
                        {
                            MemberNo = member.MemberNo,
                            Names = fullName,
                            Sex = member.Sex ?? "Not Specified",
                            PhoneNo = member.PhoneNo ?? member.MobileNo ?? "-",
                            IDNo = member.Idno ?? "-",
                            Age = age,
                            CIGCode = gig.GigCode,
                            CIGName = gig.GigName,
                            ShareCapital = shareCapital,
                            ShareDeposits = shareDeposit,
                            RegFee = regFee,
                            LoanAmt = loanAmt
                        });
                    }

                    gigReports.Add(new GIGReportViewModel
                    {
                        CIGCode = gig.GigCode,
                        CIGName = gig.GigName,
                        CompanyCode = companyCode,
                        CompanyName = companyName,
                        TotalMembers = memberDetails.Count,
                        MaleCount = maleCount,
                        FemaleCount = femaleCount,
                        YouthCount = youthCount,
                        TotalShareCapital = totalShareCapital,
                        TotalShareDeposits = totalShareDepositAmount,
                        TotalRegFee = totalRegFeeAmount,
                        TotalLoans = totalLoanAmount,
                        Members = memberDetails
                    });
                }

                using var stream = new MemoryStream();

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.MarginTop(1.5f, Unit.Centimetre);
                        page.MarginBottom(1.5f, Unit.Centimetre);
                        page.MarginLeft(1.2f, Unit.Centimetre);
                        page.MarginRight(1.2f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.5f, Unit.Centimetre);
                            header.Item().AlignCenter().Text($"GIGs REPORT - {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}").FontSize(12).Bold();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            foreach (var gig in gigReports)
                            {
                                // GIG Header
                                contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                                contentCol.Item().Text($"{gig.CIGName} ({gig.CIGCode})").FontSize(11).Bold();

                                // Summary Table
                                contentCol.Item().Table(summaryTable =>
                                {
                                    summaryTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    summaryTable.Cell().Border(0.2f).Padding(4).Text($"Total Members: {gig.TotalMembers}").Bold();
                                    summaryTable.Cell().Border(0.2f).Padding(4).Text($"Male: {gig.MaleCount}");
                                    summaryTable.Cell().Border(0.2f).Padding(4).Text($"Female: {gig.FemaleCount}");
                                    summaryTable.Cell().Border(0.2f).Padding(4).Text($"Youth: {gig.YouthCount}");
                                });

                                // Finance Summary Table
                                contentCol.Item().Table(financeTable =>
                                {
                                    financeTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    financeTable.Cell().Border(0.2f).Padding(4).Text($"Share Capital: {gig.TotalShareCapital:N0}").Bold();
                                    financeTable.Cell().Border(0.2f).Padding(4).Text($"Savings: {gig.TotalShareDeposits:N0}").Bold();
                                    financeTable.Cell().Border(0.2f).Padding(4).Text($"Reg Fee: {gig.TotalRegFee:N0}").Bold();
                                    financeTable.Cell().Border(0.2f).Padding(4).Text($"Loans: {gig.TotalLoans:N0}").Bold();
                                });

                                // Member Details Table with Aging Analysis styling
                                contentCol.Item().Table(memberTable =>
                                {
                                    memberTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.5f);
                                        cols.RelativeColumn(0.9f);
                                        cols.RelativeColumn(1.6f);
                                        cols.RelativeColumn(0.6f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(0.9f);
                                        cols.RelativeColumn(0.5f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.2f);
                                    });

                                    memberTable.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("No.").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sex").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Age").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Share Cap").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Savings").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reg Fee").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loans").Bold().FontSize(8);
                                    });

                                    int seqNo = 1;
                                    foreach (var member in gig.Members)
                                    {
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(seqNo++.ToString()).FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).Text(member.Names ?? "").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Sex ?? "-").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).Text(member.PhoneNo ?? "-").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).Text(member.IDNo ?? "-").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Age?.ToString() ?? "-").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.ShareCapital:N0}").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.ShareDeposits:N0}").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.RegFee:N0}").FontSize(8);
                                        memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.LoanAmt:N0}").FontSize(8);
                                    }
                                });

                                contentCol.Item().PaddingBottom(0.5f, Unit.Centimetre);
                            }
                        });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.DefaultTextStyle(t => t.FontSize(8));
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                                x.Span($" | Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                            });
                    });
                }).GeneratePdf(stream);

                var content = stream.ToArray();
                return File(content, "application/pdf", $"GIG_Report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting GIG report to PDF");
                TempData["ErrorMessage"] = $"Error exporting to PDF: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private string DetermineContributionTypeFromShareAndType(Sharetype shareType, Share share)
        {
            // Get searchable text from ShareType
            var shareTypeName = (shareType.SharesType ?? shareType.SharesCode ?? "").ToLower();
            var shareTypeCode = (shareType.SharesCode ?? "").ToLower();

            // Check for DEPOSIT/SAVINGS keywords
            string[] depositKeywords = { "deposit", "savings", "saving", "deposits", "share deposit", "voluntary", "welfare" };
            foreach (var keyword in depositKeywords)
            {
                if (shareTypeName.Contains(keyword))
                    return "DEPOSIT";
            }

            // Check for REGISTRATION FEE keywords
            string[] regFeeKeywords = { "reg fee", "fee", "registration", "registration fee", "entry fee", "joining fee", "membership fee" };
            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeName.Contains(keyword))
                    return "REGISTRATION_FEE";
            }

            // Check for SHARE CAPITAL keywords
            string[] shareCapitalKeywords = { "share capital", "share", "shares", "capital", "main shares", "equity" };
            foreach (var keyword in shareCapitalKeywords)
            {
                if (shareTypeName.Contains(keyword))
                    return "SHARE_CAPITAL";
            }

            // Check ShareType code as fallback
            foreach (var keyword in depositKeywords)
            {
                if (shareTypeCode.Contains(keyword))
                    return "DEPOSIT";
            }

            foreach (var keyword in regFeeKeywords)
            {
                if (shareTypeCode.Contains(keyword))
                    return "REGISTRATION_FEE";
            }

            // Check boolean flags
            if (shareType.IsMainShares == true || shareType.Issharecapital == true)
                return "SHARE_CAPITAL";

            if (shareType.Withdrawable == true && (shareType.UsedToGuarantee == true || shareType.UsedToOffset == true))
                return "DEPOSIT";

            if (shareType.Issharecapital == false && shareType.UsedToGuarantee == false &&
                shareType.UsedToOffset == false && shareType.Withdrawable == false)
                return "REGISTRATION_FEE";

            // Default
            return "SHARE_CAPITAL";
        }

        
        [HttpGet]
        public async Task<IActionResult> MembersPerCIG(string? searchTerm = null, string? statusFilter = null)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var report = await _memberService.GetMembersPerCIGReportAsync(companyCode, searchTerm, statusFilter);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.StatusFilter = statusFilter;
                ViewBag.CompanyCode = companyCode;

                return View("~/Views/Reports/MembersPerCIG.cshtml", report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Members per CIG report");
                TempData["ErrorMessage"] = $"Error loading report: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportMembersPerCIGToExcel(string? searchTerm = null, string? statusFilter = null)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var report = await _memberService.GetMembersPerCIGReportAsync(companyCode, searchTerm, statusFilter);

                using var workbook = new XLWorkbook();

                // 1. Summary Sheet
                var summarySheet = workbook.Worksheets.Add("Summary");
                int row = 1;

                summarySheet.Cell(row, 1).Value = report.CompanyName.ToUpper();
                summarySheet.Range(row, 1, row, 6).Merge();
                summarySheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(18);
                summarySheet.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row += 2;

                summarySheet.Cell(row, 1).Value = "MEMBERS PER CIG REPORT";
                summarySheet.Range(row, 1, row, 6).Merge();
                summarySheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summarySheet.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row += 2;

                summarySheet.Cell(row, 1).Value = $"Generated: {report.ReportDate:dd/MM/yyyy HH:mm}";
                summarySheet.Range(row, 1, row, 6).Merge();
                summarySheet.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row += 2;

                summarySheet.Cell(row, 1).Value = $"Printed By: {report.PrintedBy}";
                summarySheet.Range(row, 1, row, 6).Merge();
                summarySheet.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row += 2;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    summarySheet.Cell(row, 1).Value = $"Search Term: {searchTerm}";
                    row++;
                }
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    summarySheet.Cell(row, 1).Value = $"Status Filter: {statusFilter}";
                    row++;
                }
                row += 2;

                // Summary Statistics
                string[] summaryHeaders = { "Metric", "Count" };
                for (int i = 0; i < summaryHeaders.Length; i++)
                {
                    summarySheet.Cell(row, i + 1).Value = summaryHeaders[i];
                    summarySheet.Cell(row, i + 1).Style.Font.SetBold();
                    summarySheet.Cell(row, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                }
                row++;

                var stats = new (string Label, int Value)[]
                {
                    ("Total CIGs", report.TotalCIGs),
                    ("Total Members", report.TotalMembers),
                    ("Active Members", report.ActiveMembers),
                    ("Inactive Members", report.InactiveMembers)
                };

                foreach (var stat in stats)
                {
                    summarySheet.Cell(row, 1).Value = stat.Label;
                    summarySheet.Cell(row, 2).Value = stat.Value;
                    row++;
                }

                summarySheet.Columns().AdjustToContents();

                // 2. CIG Members Details Sheet
                var detailSheet = workbook.Worksheets.Add("CIG Members");
                int detailRow = 1;
                int cigCounter = 1;

                foreach (var cig in report.CIGs)
                {
                    // CIG Header
                    detailSheet.Cell(detailRow, 1).Value = $"CIG #{cigCounter}";
                    detailSheet.Cell(detailRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                    detailRow++;

                    detailSheet.Cell(detailRow, 1).Value = $"CIG Code: {cig.CIGCode}";
                    detailSheet.Cell(detailRow, 2).Value = $"CIG Name: {cig.CIGName}";
                    detailRow++;

                    detailSheet.Cell(detailRow, 1).Value = $"Contact Phone: {cig.ContactPhone ?? "N/A"}";
                    detailSheet.Cell(detailRow, 2).Value = $"Contact Email: {cig.ContactEmail ?? "N/A"}";
                    detailSheet.Cell(detailRow, 3).Value = $"Chairperson: {cig.Chairperson ?? "N/A"}";
                    detailRow++;

                    detailSheet.Cell(detailRow, 1).Value = $"Total Members: {cig.TotalMembers}";
                    detailSheet.Cell(detailRow, 1).Style.Font.SetBold();
                    detailRow += 2;

                    // Member Table Headers
                    string[] memberHeaders = { "#", "Member No", "Full Name", "ID No", "Phone", "Email", "Gender", "Status", "Joined Date" };
                    for (int i = 0; i < memberHeaders.Length; i++)
                    {
                        detailSheet.Cell(detailRow, i + 1).Value = memberHeaders[i];
                        detailSheet.Cell(detailRow, i + 1).Style.Font.SetBold();
                        detailSheet.Cell(detailRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        detailSheet.Cell(detailRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    detailRow++;

                    // Member Data - FIXED: Using FontColor instead of SetColor
                    int memberCounter = 1;
                    foreach (var member in cig.Members)
                    {
                        detailSheet.Cell(detailRow, 1).Value = memberCounter++;
                        detailSheet.Cell(detailRow, 2).Value = member.MemberNo;
                        detailSheet.Cell(detailRow, 3).Value = member.FullName;
                        detailSheet.Cell(detailRow, 4).Value = member.IdNo ?? "-";
                        detailSheet.Cell(detailRow, 5).Value = member.PhoneNo ?? "-";
                        detailSheet.Cell(detailRow, 6).Value = member.Email ?? "-";
                        detailSheet.Cell(detailRow, 7).Value = member.Gender ?? "-";
                        detailSheet.Cell(detailRow, 8).Value = member.Status;
                        // FIXED: Use FontColor instead of SetColor
                        detailSheet.Cell(detailRow, 8).Style.Font.FontColor = member.Status == "Active" ? XLColor.Green : XLColor.Red;
                        detailSheet.Cell(detailRow, 9).Value = member.JoinedDate?.ToString("dd/MM/yyyy") ?? "-";
                        detailRow++;
                    }

                    detailRow += 2;
                    cigCounter++;
                }

                detailSheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"MembersPerCIG_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Members per CIG to Excel");
                TempData["ErrorMessage"] = $"Error exporting to Excel: {ex.Message}";
                return RedirectToAction("MembersPerCIG");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportMembersPerCIGToPdf(string? searchTerm = null, string? statusFilter = null)
        {
            try
            {
                var companyCode = GetUserCompanyCode();
                var report = await _memberService.GetMembersPerCIGReportAsync(companyCode, searchTerm, statusFilter);

                using var stream = new MemoryStream();

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Portrait());
                        page.MarginTop(1.5f, Unit.Centimetre);
                        page.MarginBottom(1.5f, Unit.Centimetre);
                        page.MarginLeft(1.2f, Unit.Centimetre);
                        page.MarginRight(1.2f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(report.CompanyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text("MEMBERS PER CIG REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Generated: {report.ReportDate:dd/MM/yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().AlignCenter().Text($"Printed By: {report.PrintedBy}").FontSize(9).Italic();
                            if (!string.IsNullOrEmpty(searchTerm))
                            {
                                header.Item().AlignCenter().Text($"Search: {searchTerm}").FontSize(9);
                            }
                            if (!string.IsNullOrEmpty(statusFilter))
                            {
                                header.Item().AlignCenter().Text($"Status Filter: {statusFilter}").FontSize(9);
                            }
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total CIGs:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(report.TotalCIGs.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Members:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(report.TotalMembers.ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Active Members:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(report.ActiveMembers.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Inactive Members:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(report.InactiveMembers.ToString());
                            });

                            // CIGs Details
                            int cigCounter = 1;
                            foreach (var cig in report.CIGs)
                            {
                                contentCol.Item().PaddingTop(1, Unit.Centimetre);
                                contentCol.Item().Table(cigTable =>
                                {
                                    cigTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(2);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    // FIXED: Use QuestPDF.Infrastructure.Color.FromHex instead of FromRgb
                                    cigTable.Cell().ColumnSpan(3).Border(0.2f).Background("#2c3e50").Padding(4).Text($"CIG #{cigCounter}: {cig.CIGName} ({cig.CIGCode})").FontColor(QuestPDF.Infrastructure.Color.FromHex("#FFFFFF")).Bold().FontSize(10);
                                    cigTable.Cell().Border(0.2f).Padding(4).Text($"Contact: {cig.ContactPhone ?? "N/A"}").FontSize(8);
                                    cigTable.Cell().Border(0.2f).Padding(4).Text($"Email: {cig.ContactEmail ?? "N/A"}").FontSize(8);
                                    cigTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"Members: {cig.TotalMembers}").FontSize(8);

                                    cigTable.Cell().ColumnSpan(3).Border(0.2f).Padding(2);
                                });

                                // Member List
                                if (cig.Members.Any())
                                {
                                    contentCol.Item().Table(memberTable =>
                                    {
                                        memberTable.ColumnsDefinition(cols =>
                                        {
                                            cols.RelativeColumn(0.5f);
                                            cols.RelativeColumn(1.2f);
                                            cols.RelativeColumn(2.0f);
                                            cols.RelativeColumn(1.0f);
                                            cols.RelativeColumn(1.5f);
                                            cols.RelativeColumn(1.0f);
                                            cols.RelativeColumn(0.8f);
                                        });

                                        memberTable.Header(header =>
                                        {
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Full Name").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Joined").Bold().FontSize(8);
                                        });

                                        int memberCounter = 1;
                                        foreach (var member in cig.Members)
                                        {
                                            string statusColor = member.Status == "Active" ? "#d4edda" : "#f8d7da";
                                            memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(memberCounter.ToString()).FontSize(8);
                                            memberTable.Cell().Border(0.2f).Padding(4).Text(member.MemberNo).FontSize(8);
                                            memberTable.Cell().Border(0.2f).Padding(4).Text(member.FullName).FontSize(8);
                                            memberTable.Cell().Border(0.2f).Padding(4).Text(member.IdNo ?? "-").FontSize(8);
                                            memberTable.Cell().Border(0.2f).Padding(4).Text(member.PhoneNo ?? "-").FontSize(8);
                                            memberTable.Cell().Border(0.2f).Background(statusColor).Padding(4).AlignCenter().Text(member.Status).FontSize(8);
                                            memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.JoinedDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                            memberCounter++;
                                        }
                                    });
                                }
                                else
                                {
                                    contentCol.Item().Text("No members found in this CIG.").FontSize(9).Italic();
                                }

                                cigCounter++;
                            }
                        });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.DefaultTextStyle(t => t.FontSize(8));
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                                x.Span($" | Generated: {report.ReportDate:dd/MM/yyyy HH:mm:ss}");
                            });
                    });
                }).GeneratePdf(stream);

                var content = stream.ToArray();
                return File(content, "application/pdf", $"MembersPerCIG_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Members per CIG to PDF");
                TempData["ErrorMessage"] = $"Error exporting to PDF: {ex.Message}";
                return RedirectToAction("MembersPerCIG");
            }
        }
    }
}

