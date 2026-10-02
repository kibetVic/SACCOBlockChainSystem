using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ClosedXML.Excel;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SACCOBlockChainSystem.Controllers
{
	[Authorize]
	public class MemberReportController : Controller
	{
		private readonly ApplicationDbContext _context;
        private readonly ILogger<MemberReportController> _logger;

        public MemberReportController(ApplicationDbContext context, ILogger<MemberReportController> logger)  
        {
            _context = context;
            _logger = logger;  
        }

        #region Active Members Report

        public IActionResult ActiveMembers()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            var viewModel = new ActiveMembersIndexViewModel
            {
                Members = new List<MemberReportViewModel>(),
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0,
                TotalShareCapital = 0,
                TotalSavingsDeposits = 0,
                TotalRegFee = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = false;

            return View("~/Views/Reports/ActiveMembers.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ActiveMembers(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // Get share type requirements
            var mainShareType = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
                .FirstOrDefaultAsync();

            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
            decimal registrationFeeRequirement = 0;

            // Get all members (not withdrawn, not archived)
            var allMembers = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = allMembers.Select(m => m.MemberNo).ToList();

            // Get contributions from ContribShares table
            var contribShares = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                })
                .ToListAsync();

            // Get shares from Shares table as fallback
            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo)
                    && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShares = g.Sum(s => s.TotalShares ?? 0)
                })
                .ToListAsync();

            // ============================================================
            // FIXED: Get ALL deposit dates for ALL members in ONE query
            // This matches the dashboard logic exactly
            // ============================================================
            var allDeposits = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode
                    && cs.DepositsAmount.HasValue
                    && cs.DepositsAmount.Value > 0)
                .Select(cs => new { cs.MemberNo, Date = cs.ContrDate /*?? cs.DepositedDate ?? cs.ReceiptDate ?? cs.AuditTime*/ })
                .ToListAsync();

            // Group deposit dates by member
            var memberDeposits = allDeposits
                .Where(d => d.Date != null && d.Date != DateTime.MinValue)
                .GroupBy(d => d.MemberNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => d.Date.Value).ToList()
                );

            // Determine active members using the same logic as dashboard
            var activeMemberNos = new HashSet<string>();

            foreach (var member in allMembers)
            {
                if (memberDeposits.TryGetValue(member.MemberNo, out var depositDates))
                {
                    if (IsMemberActive(depositDates))
                    {
                        activeMemberNos.Add(member.MemberNo);
                    }
                }
            }

            var reportData = new List<MemberReportViewModel>();

            foreach (var m in allMembers)
            {
                var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);
                var memberShare = shares.FirstOrDefault(s => s.MemberNo == m.MemberNo);

                // Calculate total share capital
                decimal totalShareCapital = 0;
                if (memberContrib != null)
                    totalShareCapital = memberContrib.TotalShareCapital;
                else if (memberShare != null)
                    totalShareCapital = memberShare.TotalShares;
                else
                    totalShareCapital = m.ShareCap ?? 0;

                decimal totalSavingsDeposits = memberContrib?.TotalDeposits ?? 0;
                decimal totalRegistrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;

                // Check if member meets the active criteria:
                // 1. Has paid minimum share capital requirement
                // 2. Has paid registration fee requirement
                // 3. Has savings/deposits
                // 4. Has 3 consecutive months of deposits (using dashboard logic)

                bool hasMetShareRequirement = minimumShareRequirement == 0 ? true : totalShareCapital >= minimumShareRequirement;
                bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : totalRegistrationFee >= registrationFeeRequirement;
                bool hasSavingsDeposits = totalSavingsDeposits > 0;

                // IMPORTANT: Must have 3 consecutive months of deposits (dashboard logic)
                bool hasRegularContributions = activeMemberNos.Contains(m.MemberNo);

                // Only include if ALL criteria are met
                if (hasMetShareRequirement && hasPaidRegistrationFee && hasSavingsDeposits && hasRegularContributions)
                {
                    int? age = null;
                    if (m.Dob.HasValue)
                    {
                        age = DateTime.Now.Year - m.Dob.Value.Year;
                        if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
                    }

                    string fullName = "";
                    if (m.FullName != null)
                    {
                        fullName = m.FullName.ToString();
                    }
                    else
                    {
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName))
                            fullName = "N/A";
                    }

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        string sexUpper = m.Sex.ToUpper();
                        if (sexUpper == "M" || sexUpper == "MALE")
                            sex = "MALE";
                        else if (sexUpper == "F" || sexUpper == "FEMALE")
                            sex = "FEMALE";
                        else
                            sex = sexUpper;
                    }

                    reportData.Add(new MemberReportViewModel
                    {
                        MemberNo = m.MemberNo,
                        FullName = fullName,
                        IdNo = m.Idno ?? "-",
                        Sex = sex,
                        Age = age,
                        MembershipType = m.MembershipType ?? "Individual",
                        ApplicDate = m.ApplicDate,
                        EffectDate = m.EffectDate,
                        ShareCapital = totalShareCapital,
                        SavingsDeposits = totalSavingsDeposits,
                        RegFee = totalRegistrationFee,
                        LoanBalance = m.LoanBalance ?? 0,
                        PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
                        Email = m.Email ?? m.EmailAddress,
                        Station = m.Station ?? "-",
                        Status = "ACTIVE"
                    });
                }
            }

            // Sort by member number
            reportData = reportData.OrderBy(m => m.MemberNo).ToList();

            int maleCount = reportData.Count(m => m.Sex == "MALE");
            int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
            int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE"
                                                && !string.IsNullOrEmpty(m.Sex) && m.Sex != "NOT SPECIFIED");

            var viewModel = new ActiveMembersIndexViewModel
            {
                Members = reportData,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0),
                TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0),
                TotalRegFee = reportData.Sum(m => m.RegFee ?? 0),
                ReportDate = reportDate,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.TotalMembers = reportData.Count;
            ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0);
            ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0);
            ViewBag.TotalRegFee = reportData.Sum(m => m.RegFee ?? 0);
            ViewBag.MaleCount = maleCount;
            ViewBag.FemaleCount = femaleCount;
            ViewBag.OtherCount = otherCount;
            ViewBag.HasData = reportData.Any();
            ViewBag.ActiveMembersCount = activeMemberNos.Count;

            return View("~/Views/Reports/ActiveMembers.cshtml", viewModel);
        }

        /// <summary>
        /// Determines if a member is active based on having 3 consecutive months of deposits
        /// This matches the dashboard logic exactly
        /// </summary>
        private bool IsMemberActive(List<DateTime> depositDates)
        {
            if (depositDates == null || depositDates.Count < 3)
                return false;

            // Sort dates ascending
            var sortedDates = depositDates.OrderBy(d => d).ToList();

            // Group by year-month to check consecutive months
            var monthGroups = sortedDates
                .Select(d => new { Year = d.Year, Month = d.Month })
                .Distinct()
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            // Check for 3 consecutive months
            int consecutiveCount = 1;
            for (int i = 1; i < monthGroups.Count; i++)
            {
                var current = monthGroups[i];
                var previous = monthGroups[i - 1];

                // Check if months are consecutive
                bool isConsecutive = false;

                // Same year, next month
                if (current.Year == previous.Year && current.Month == previous.Month + 1)
                    isConsecutive = true;
                // Year boundary (Dec to Jan)
                else if (current.Year == previous.Year + 1 && current.Month == 1 && previous.Month == 12)
                    isConsecutive = true;

                if (isConsecutive)
                {
                    consecutiveCount++;
                    if (consecutiveCount >= 3)
                        return true;
                }
                else
                {
                    consecutiveCount = 1;
                }
            }

            return false;
        }

        [HttpPost]
        public async Task<IActionResult> ExportActiveMembersToExcel(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn != true)
                    && (m.Archived != true)
                    && (m.Status == null || m.Status == 1))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // Get all deposit dates for active check (same as dashboard)
            var allDeposits = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode
                    && cs.DepositsAmount.HasValue
                    && cs.DepositsAmount.Value > 0)
                .Select(cs => new { cs.MemberNo, Date = cs.ContrDate /*?? cs.DepositedDate ?? cs.ReceiptDate ?? cs.AuditTime*/ })
                .ToListAsync();

            var memberDeposits = allDeposits
                .Where(d => d.Date != null && d.Date != DateTime.MinValue)
                .GroupBy(d => d.MemberNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => d.Date.Value).ToList()
                );

            var activeMemberNos = new HashSet<string>();
            foreach (var member in members)
            {
                if (memberDeposits.TryGetValue(member.MemberNo, out var depositDates))
                {
                    if (IsMemberActive(depositDates))
                    {
                        activeMemberNos.Add(member.MemberNo);
                    }
                }
            }

            var contribLookup = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    g.Key,
                    Share = g.Sum(x => x.ShareCapitalAmount ?? 0),
                    Deposits = g.Sum(x => x.DepositsAmount ?? 0),
                    RegFee = g.Sum(x => x.RegFeeAmount ?? 0)
                })
                .ToDictionaryAsync(x => x.Key);

            // Only include active members
            var report = members
                .Where(m => activeMemberNos.Contains(m.MemberNo))
                .Select(m =>
                {
                    contribLookup.TryGetValue(m.MemberNo, out var contrib);

                    string name = !string.IsNullOrWhiteSpace(m.FullName?.ToString())
                        ? m.FullName.ToString()
                        : $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    if (string.IsNullOrWhiteSpace(name)) name = "N/A";

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        var s = m.Sex.ToUpper();
                        sex = (s == "M" || s == "MALE") ? "MALE"
                             : (s == "F" || s == "FEMALE") ? "FEMALE"
                             : s;
                    }

                    return new
                    {
                        m.MemberNo,
                        Name = name,
                        Sex = sex,
                        Share = contrib?.Share ?? m.ShareCap ?? 0,
                        Deposits = contrib?.Deposits ?? 0,
                        RegFee = contrib?.RegFee ?? m.RegFee ?? 0
                    };
                }).ToList();

            int male = report.Count(x => x.Sex == "MALE");
            int female = report.Count(x => x.Sex == "FEMALE");
            int other = report.Count(x => x.Sex != "MALE" && x.Sex != "FEMALE" && x.Sex != "NOT SPECIFIED");

            decimal totalShare = report.Sum(x => x.Share);
            decimal totalDeposits = report.Sum(x => x.Deposits);
            decimal totalReg = report.Sum(x => x.RegFee);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Active Members");
            int r = 1;

            ws.Cell(r, 1).Value = companyName.ToUpper();
            ws.Range(r, 1, r, 6).Merge().Style.Font.SetBold().Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r += 2;

            ws.Cell(r, 1).Value = $"ACTIVE MEMBERS AS AT {reportDate:dd/MM/yyyy}";
            ws.Range(r, 1, r, 6).Merge().Style.Font.SetBold()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r += 2;

            ws.Cell(r++, 1).Value = $"TOTAL: {report.Count}";
            ws.Cell(r++, 1).Value = $"MALE: {male}";
            ws.Cell(r++, 1).Value = $"FEMALE: {female}";
            ws.Cell(r++, 1).Value = $"OTHERS: {other}";
            r++;

            string[] headers = { "MemberNo", "Names", "Sex", "Share Capital", "Deposits", "Reg Fee" };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(r, i + 1).Value = headers[i];
                ws.Cell(r, i + 1).Style.Font.SetBold();
            }

            r++;

            foreach (var m in report)
            {
                ws.Cell(r, 1).Value = m.MemberNo;
                ws.Cell(r, 2).Value = m.Name;
                ws.Cell(r, 3).Value = m.Sex;
                ws.Cell(r, 4).Value = m.Share;
                ws.Cell(r, 5).Value = m.Deposits;
                ws.Cell(r, 6).Value = m.RegFee;

                ws.Range(r, 4, r, 6).Style.NumberFormat.Format = "#,##0.00";
                r++;
            }

            r++;

            ws.Cell(r, 3).Value = "TOTAL:";
            ws.Cell(r, 3).Style.Font.SetBold();

            ws.Cell(r, 4).Value = totalShare;
            ws.Cell(r, 5).Value = totalDeposits;
            ws.Cell(r, 6).Value = totalReg;

            ws.Range(r, 4, r, 6).Style.Font.SetBold()
                .NumberFormat.SetFormat("#,##0.00");

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ActiveMembers_{reportDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportActiveMembersToPdf(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn != true)
                    && (m.Archived != true)
                    && (m.Status == null || m.Status == 1))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // Get all deposit dates for active check (same as dashboard)
            var allDeposits = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode
                    && cs.DepositsAmount.HasValue
                    && cs.DepositsAmount.Value > 0)
                .Select(cs => new { cs.MemberNo, Date = cs.ContrDate /*?? cs.DepositedDate ?? cs.ReceiptDate ?? cs.AuditTime*/ })
                .ToListAsync();

            var memberDeposits = allDeposits
                .Where(d => d.Date != null && d.Date != DateTime.MinValue)
                .GroupBy(d => d.MemberNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => d.Date.Value).ToList()
                );

            var activeMemberNos = new HashSet<string>();
            foreach (var member in members)
            {
                if (memberDeposits.TryGetValue(member.MemberNo, out var depositDates))
                {
                    if (IsMemberActive(depositDates))
                    {
                        activeMemberNos.Add(member.MemberNo);
                    }
                }
            }

            var contribLookup = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    g.Key,
                    Share = g.Sum(x => x.ShareCapitalAmount ?? 0),
                    Deposits = g.Sum(x => x.DepositsAmount ?? 0),
                    RegFee = g.Sum(x => x.RegFeeAmount ?? 0)
                })
                .ToDictionaryAsync(x => x.Key);

            // Only include active members
            var report = members
                .Where(m => activeMemberNos.Contains(m.MemberNo))
                .Select(m =>
                {
                    contribLookup.TryGetValue(m.MemberNo, out var c);

                    string name = !string.IsNullOrWhiteSpace(m.FullName?.ToString())
                        ? m.FullName.ToString()
                        : $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    if (string.IsNullOrWhiteSpace(name)) name = "N/A";

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        var s = m.Sex.ToUpper();
                        sex = (s == "M" || s == "MALE") ? "MALE"
                             : (s == "F" || s == "FEMALE") ? "FEMALE"
                             : s;
                    }

                    return new ActiveMemberPdfData
                    {
                        MemberNo = m.MemberNo,
                        Name = name,
                        Sex = sex,
                        Share = c?.Share ?? m.ShareCap ?? 0,
                        Deposits = c?.Deposits ?? 0,
                        RegFee = c?.RegFee ?? m.RegFee ?? 0
                    };
                }).ToList();

            int male = report.Count(x => x.Sex == "MALE");
            int female = report.Count(x => x.Sex == "FEMALE");

            using var stream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header()
                        .AlignCenter()
                        .Column(column =>
                        {
                            column.Item().Text(companyName.ToUpper()).FontSize(18).Bold();
                            column.Item().Text($"ACTIVE MEMBERS AS AT {reportDate:dd/MM/yyyy}").FontSize(14).Bold();
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Item().Table(statsTable =>
                            {
                                statsTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                statsTable.Cell().Element(c => c.Text($"TOTAL: {report.Count}").Bold());
                                statsTable.Cell().Element(c => c.Text($"MALE: {male}").Bold());
                                statsTable.Cell().Element(c => c.Text($"FEMALE: {female}").Bold());
                                statsTable.Cell().Element(c => c.Text($"GENERATED: {DateTime.Now:dd/MM/yyyy}"));
                            });

                            column.Item().PaddingTop(1, Unit.Centimetre);

                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(c => c.Text("MemberNo").Bold());
                                    header.Cell().Element(c => c.Text("Names").Bold());
                                    header.Cell().Element(c => c.Text("Sex").Bold());
                                    header.Cell().Element(c => c.Text("Share").Bold());
                                    header.Cell().Element(c => c.Text("Deposits").Bold());
                                    header.Cell().Element(c => c.Text("Reg Fee").Bold());
                                });

                                foreach (var m in report)
                                {
                                    table.Cell().Element(c => c.Text(m.MemberNo ?? ""));
                                    table.Cell().Element(c => c.Text(m.Name));
                                    table.Cell().Element(c => c.Text(m.Sex));
                                    table.Cell().Element(c => c.AlignRight().Text($"{m.Share:N0}"));
                                    table.Cell().Element(c => c.AlignRight().Text($"{m.Deposits:N0}"));
                                    table.Cell().Element(c => c.AlignRight().Text($"{m.RegFee:N0}"));
                                }
                            });
                        });

                    page.Footer()
                        .AlignRight()
                        .Text(x => x.Span($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}"));
                });
            }).GeneratePdf(stream);

            return File(stream.ToArray(), "application/pdf", $"ActiveMembers_{reportDate:yyyyMMdd}.pdf");
        }

        #endregion

		#region Inactive Members Report

		public IActionResult InactiveMembers()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new InactiveMembersIndexViewModel
			{
				Members = new List<MemberReportViewModel>(),
				ReportDate = reportDate,
				HasData = false,
				UserCompanyCode = companyCode,
				CompanyName = companyName,
				TotalMembers = 0,
				MaleCount = 0,
				FemaleCount = 0,
				OtherCount = 0,
				TotalShareCapital = 0,
				TotalSavingsDeposits = 0,
				TotalRegFee = 0
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.HasData = false;

			return View("~/Views/Reports/InactiveMembers.cshtml", viewModel);
		}

        [HttpPost]
        public async Task<IActionResult> InactiveMembers(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // Get share type requirements
            var mainShareType = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
                .FirstOrDefaultAsync();

            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
            decimal registrationFeeRequirement = 0;

            // Get all members (including withdrawn/archived/inactive)
            var allMembers = await _context.Members
                .Where(m => m.CompanyCode == companyCode)
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = allMembers.Select(m => m.MemberNo).ToList();

            // Get contributions from ContribShares table
            var contribShares = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                })
                .ToListAsync();

            // Get shares from Shares table as fallback
            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo)
                    && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShares = g.Sum(s => s.TotalShares ?? 0)
                })
                .ToListAsync();

            // Calculate the date range for last 3 months (EXCLUDING current month)
            var currentMonthStart = new DateTime(reportDate.Year, reportDate.Month, 1);
            var threeMonthsAgoStart = currentMonthStart.AddMonths(-3);

            // Get contributions in the last 3 full months (excluding current month)
            var recentContributions = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode
                    && cs.ContrDate >= threeMonthsAgoStart
                    && cs.ContrDate < currentMonthStart)  // Exclude current month
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    RecentShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    RecentDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    RecentRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    ContributionCount = g.Count(),
                    LastContributionDate = g.Max(cs => cs.ContrDate)
                })
                .ToListAsync();

            var reportData = new List<MemberReportViewModel>();

            foreach (var m in allMembers)
            {
                var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);
                var memberShare = shares.FirstOrDefault(s => s.MemberNo == m.MemberNo);
                var recentContrib = recentContributions.FirstOrDefault(r => r.MemberNo == m.MemberNo);

                // Calculate total share capital
                decimal totalShareCapital = 0;
                if (memberContrib != null)
                    totalShareCapital = memberContrib.TotalShareCapital;
                else if (memberShare != null)
                    totalShareCapital = memberShare.TotalShares;
                else
                    totalShareCapital = m.ShareCap ?? 0;

                decimal totalSavingsDeposits = memberContrib?.TotalDeposits ?? 0;
                decimal totalRegistrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;

                // Check if member meets the active criteria:
                // 1. Has paid minimum share capital requirement
                // 2. Has paid registration fee requirement
                // 3. Has savings/deposits
                // 4. Has made at least ONE contribution in the last 3 FULL months (excluding current month)

                bool hasMetShareRequirement = minimumShareRequirement == 0 ? true : totalShareCapital >= minimumShareRequirement;
                bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : totalRegistrationFee >= registrationFeeRequirement;
                bool hasSavingsDeposits = totalSavingsDeposits > 0;
                bool hasRegularContributions = recentContrib != null && recentContrib.ContributionCount >= 1;

                // Determine if member is fully active
                bool isFullyActive = hasMetShareRequirement && hasPaidRegistrationFee && hasSavingsDeposits && hasRegularContributions;

                // Only include if NOT fully active (inactive)
                if (!isFullyActive)
                {
                    int? age = null;
                    if (m.Dob.HasValue)
                    {
                        age = DateTime.Now.Year - m.Dob.Value.Year;
                        if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
                    }

                    string fullName = "";
                    if (m.FullName != null)
                    {
                        fullName = m.FullName.ToString();
                    }
                    else
                    {
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName))
                            fullName = "N/A";
                    }

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        string sexUpper = m.Sex.ToUpper();
                        if (sexUpper == "M" || sexUpper == "MALE")
                            sex = "MALE";
                        else if (sexUpper == "F" || sexUpper == "FEMALE")
                            sex = "FEMALE";
                        else
                            sex = sexUpper;
                    }

                    // Determine specific status based on what's missing
                    string inactiveReason = "";
                    if (m.Withdrawn == true)
                        inactiveReason = "WITHDRAWN";
                    else if (m.Archived == true)
                        inactiveReason = "ARCHIVED";
                    else if (m.Status == 0)
                        inactiveReason = "INACTIVE";
                    else
                    {
                        // Determine the reason for inactivity
                        var missingReasons = new List<string>();
                        if (!hasMetShareRequirement && minimumShareRequirement > 0)
                            missingReasons.Add($"Share Capital (Min: {minimumShareRequirement:N0})");
                        if (!hasPaidRegistrationFee && registrationFeeRequirement > 0)
                            missingReasons.Add("Registration Fee");
                        if (!hasSavingsDeposits)
                            missingReasons.Add("Savings/Deposits");
                        if (!hasRegularContributions)
                        {
                            int monthsWithoutContrib = 3;
                            if (recentContrib != null && recentContrib.LastContributionDate.HasValue)
                            {
                                monthsWithoutContrib = ((currentMonthStart.Year - recentContrib.LastContributionDate.Value.Year) * 12) +
                                                        (currentMonthStart.Month - recentContrib.LastContributionDate.Value.Month);
                            }
                            missingReasons.Add($"No contributions in last 3 months");
                        }

                        inactiveReason = string.Join(", ", missingReasons);
                        if (string.IsNullOrEmpty(inactiveReason))
                            inactiveReason = "INACTIVE";
                    }

                    reportData.Add(new MemberReportViewModel
                    {
                        MemberNo = m.MemberNo,
                        FullName = fullName,
                        IdNo = m.Idno ?? "-",
                        Sex = sex,
                        Age = age,
                        MembershipType = m.MembershipType ?? "Individual",
                        ApplicDate = m.ApplicDate,
                        EffectDate = m.EffectDate,
                        ShareCapital = totalShareCapital,
                        SavingsDeposits = totalSavingsDeposits,
                        RegFee = totalRegistrationFee,
                        LoanBalance = m.LoanBalance ?? 0,
                        PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
                        Email = m.Email ?? m.EmailAddress,
                        Station = m.Station ?? "-",
                        Status = inactiveReason
                    });
                }
            }

            // Sort by member number
            reportData = reportData.OrderBy(m => m.MemberNo).ToList();

            // Calculate statistics
            int maleCount = reportData.Count(m => m.Sex == "MALE");
            int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
            int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE"
                                                && !string.IsNullOrEmpty(m.Sex) && m.Sex != "NOT SPECIFIED");

            // Count by inactivity reason
            int withdrawnCount = reportData.Count(m => m.Status == "WITHDRAWN");
            int archivedCount = reportData.Count(m => m.Status == "ARCHIVED");
            int shareCapitalMissingCount = reportData.Count(m => m.Status.Contains("Share Capital"));
            int regFeeMissingCount = reportData.Count(m => m.Status.Contains("Registration Fee"));
            int savingsMissingCount = reportData.Count(m => m.Status.Contains("Savings/Deposits"));
            int noContributionsCount = reportData.Count(m => m.Status.Contains("No contributions"));

            var viewModel = new InactiveMembersIndexViewModel
            {
                Members = reportData,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0),
                TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0),
                TotalRegFee = reportData.Sum(m => m.RegFee ?? 0),
                ReportDate = reportDate,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.TotalMembers = reportData.Count;
            ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0);
            ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0);
            ViewBag.TotalRegFee = reportData.Sum(m => m.RegFee ?? 0);
            ViewBag.MaleCount = maleCount;
            ViewBag.FemaleCount = femaleCount;
            ViewBag.OtherCount = otherCount;
            ViewBag.HasData = reportData.Any();
            ViewBag.WithdrawnCount = withdrawnCount;
            ViewBag.ArchivedCount = archivedCount;
            ViewBag.ShareCapitalMissingCount = shareCapitalMissingCount;
            ViewBag.RegistrationFeeMissingCount = regFeeMissingCount;
            ViewBag.SavingsMissingCount = savingsMissingCount;
            ViewBag.NoRegularContributionsCount = noContributionsCount;
            ViewBag.LastThreeMonthsStart = threeMonthsAgoStart.ToString("dd/MM/yyyy");
            ViewBag.CurrentMonthStart = currentMonthStart.ToString("dd/MM/yyyy");

            return View("~/Views/Reports/InactiveMembers.cshtml", viewModel);
        }

        //[HttpPost]
		//public async Task<IActionResult> InactiveMembers(DateTime reportDate)
		//{
		//	var companyCode = User.FindFirstValue("CompanyCode");
		//	var companyName = User.FindFirstValue("CompanyName") ?? "";

		//	// Get share type requirements
		//	var mainShareType = await _context.Sharetypes
		//		.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
		//		.FirstOrDefaultAsync();

		//	decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
		//	decimal registrationFeeRequirement = 0; // Set your registration fee requirement here

		//	// Get all members (including active ones that don't meet criteria)
		//	var allMembers = await _context.Members
		//		.Where(m => m.CompanyCode == companyCode)
		//		.OrderBy(m => m.MemberNo)
		//		.ToListAsync();

		//	var memberNos = allMembers.Select(m => m.MemberNo).ToList();

		//	// Get contributions (shares, deposits, reg fees)
		//	var contribShares = await _context.ContribShares
		//		.Where(cs => memberNos.Contains(cs.MemberNo)
		//			&& cs.CompanyCode == companyCode)
		//		.GroupBy(cs => cs.MemberNo)
		//		.Select(g => new
		//		{
		//			MemberNo = g.Key,
		//			TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
		//			TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
		//			TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
		//		})
		//		.ToListAsync();

		//	// Get shares from Shares table as fallback
		//	var shares = await _context.Shares
		//		.Where(s => memberNos.Contains(s.MemberNo)
		//			&& s.CompanyCode == companyCode)
		//		.GroupBy(s => s.MemberNo)
		//		.Select(g => new
		//		{
		//			MemberNo = g.Key,
		//			TotalShares = g.Sum(s => s.TotalShares ?? 0)
		//		})
		//		.ToListAsync();

		//	// Get the date 3 months ago (to check regular contributions)
		//	var threeMonthsAgo = reportDate.AddMonths(-5);

		//	// Get contributions in the last 3 months to check regular activity
		//	var recentContributions = await _context.ContribShares
		//		.Where(cs => memberNos.Contains(cs.MemberNo)
		//			&& cs.CompanyCode == companyCode
		//			&& cs.ContrDate >= threeMonthsAgo
		//			&& cs.ContrDate <= reportDate)
		//		.GroupBy(cs => cs.MemberNo)
		//		.Select(g => new
		//		{
		//			MemberNo = g.Key,
		//			RecentShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
		//			RecentDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
		//			RecentRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
		//			ContributionCount = g.Count(),
		//			LastContributionDate = g.Max(cs => cs.ContrDate)
		//		})
		//		.ToListAsync();

		//	var reportData = new List<MemberReportViewModel>();

		//	foreach (var m in allMembers)
		//	{
		//		var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);
		//		var memberShare = shares.FirstOrDefault(s => s.MemberNo == m.MemberNo);
		//		var recentContrib = recentContributions.FirstOrDefault(r => r.MemberNo == m.MemberNo);

		//		// Calculate total share capital
		//		decimal totalShareCapital = 0;
		//		if (memberContrib != null)
		//			totalShareCapital = memberContrib.TotalShareCapital;
		//		else if (memberShare != null)
		//			totalShareCapital = memberShare.TotalShares;
		//		else
		//			totalShareCapital = m.ShareCap ?? 0;

		//		decimal totalSavingsDeposits = memberContrib?.TotalDeposits ?? 0;
		//		decimal totalRegistrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;

		//		// Check if member meets the active criteria:
		//		// 1. Has paid minimum share capital requirement
		//		// 2. Has paid registration fee requirement
		//		// 3. Has savings/deposits
		//		// 4. Has made regular contributions in the last 3 months

		//		bool hasMetShareRequirement = minimumShareRequirement == 0 ? true : totalShareCapital >= minimumShareRequirement;
		//		bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : totalRegistrationFee >= registrationFeeRequirement;
		//		bool hasSavingsDeposits = totalSavingsDeposits > 0;
		//		bool hasRegularContributions = recentContrib != null && recentContrib.ContributionCount > 0;

		//		// Determine why the member is inactive
		//		bool isFullyActive = hasMetShareRequirement && hasPaidRegistrationFee && hasSavingsDeposits && hasRegularContributions;

		//		// Only include if NOT fully active (inactive by our new definition)
		//		if (!isFullyActive)
		//		{
		//			int? age = null;
		//			if (m.Dob.HasValue)
		//			{
		//				age = DateTime.Now.Year - m.Dob.Value.Year;
		//				if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
		//			}

		//			string fullName = "";
		//			if (m.FullName != null)
		//			{
		//				fullName = m.FullName.ToString();
		//			}
		//			else
		//			{
		//				fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
		//				if (string.IsNullOrWhiteSpace(fullName))
		//					fullName = "N/A";
		//			}

		//			string sex = "NOT SPECIFIED";
		//			if (!string.IsNullOrEmpty(m.Sex))
		//			{
		//				string sexUpper = m.Sex.ToUpper();
		//				if (sexUpper == "M" || sexUpper == "MALE")
		//					sex = "MALE";
		//				else if (sexUpper == "F" || sexUpper == "FEMALE")
		//					sex = "FEMALE";
		//				else
		//					sex = sexUpper;
		//			}

		//			// Determine specific status based on what's missing
		//			string inactiveReason = "";
		//			if (m.Withdrawn == true)
		//				inactiveReason = "WITHDRAWN";
		//			else if (m.Archived == true)
		//				inactiveReason = "ARCHIVED";
		//			else if (m.Status == 0)
		//				inactiveReason = "INACTIVE";
		//			else
		//			{
		//				// Determine the reason for inactivity
		//				var missingReasons = new List<string>();
		//				if (!hasMetShareRequirement && minimumShareRequirement > 0)
		//					missingReasons.Add($"Share Capital (Min: {minimumShareRequirement:N0})");
		//				if (!hasPaidRegistrationFee && registrationFeeRequirement > 0)
		//					missingReasons.Add("Registration Fee");
		//				if (!hasSavingsDeposits)
		//					missingReasons.Add("Savings/Deposits");
		//				if (!hasRegularContributions)
		//					missingReasons.Add($"No contributions in last 3 months");

		//				inactiveReason = string.Join(", ", missingReasons);
		//				if (string.IsNullOrEmpty(inactiveReason))
		//					inactiveReason = "INACTIVE";
		//			}

		//			reportData.Add(new MemberReportViewModel
		//			{
		//				MemberNo = m.MemberNo,
		//				FullName = fullName,
		//				IdNo = m.Idno ?? "-",
		//				Sex = sex,
		//				Age = age,
		//				MembershipType = m.MembershipType ?? "Individual",
		//				ApplicDate = m.ApplicDate,
		//				EffectDate = m.EffectDate,
		//				ShareCapital = totalShareCapital,
		//				SavingsDeposits = totalSavingsDeposits,
		//				RegFee = totalRegistrationFee,
		//				LoanBalance = m.LoanBalance ?? 0,
		//				PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
		//				Email = m.Email ?? m.EmailAddress,
		//				Station = m.Station ?? "-",
		//				Status = inactiveReason,
		//				// Optional: Add these properties to your ViewModel if needed
		//				// HasMetShareRequirement = hasMetShareRequirement,
		//				// HasPaidRegistrationFee = hasPaidRegistrationFee,
		//				// HasSavingsDeposits = hasSavingsDeposits,
		//				// HasRegularContributions = hasRegularContributions,
		//				// LastContributionDate = recentContrib?.LastContributionDate,
		//				// MonthsSinceLastContribution = recentContrib != null && recentContrib.LastContributionDate.HasValue 
		//				//     ? (reportDate - recentContrib.LastContributionDate.Value).Days / 30 
		//				//     : null
		//			});
		//		}
		//	}

		//	// Sort by member number
		//	reportData = reportData.OrderBy(m => m.MemberNo).ToList();

		//	// Calculate statistics
		//	int maleCount = reportData.Count(m => m.Sex == "MALE");
		//	int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
		//	int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE"
		//										&& !string.IsNullOrEmpty(m.Sex) && m.Sex != "NOT SPECIFIED");

		//	// Count by inactivity reason
		//	int withdrawnCount = reportData.Count(m => m.Status == "WITHDRAWN");
		//	int archivedCount = reportData.Count(m => m.Status == "ARCHIVED");
		//	int shareCapitalMissingCount = reportData.Count(m => m.Status.Contains("Share Capital"));
		//	int regFeeMissingCount = reportData.Count(m => m.Status.Contains("Registration Fee"));
		//	int savingsMissingCount = reportData.Count(m => m.Status.Contains("Savings/Deposits"));
		//	int noContributionsCount = reportData.Count(m => m.Status.Contains("No contributions"));

		//	var viewModel = new InactiveMembersIndexViewModel
		//	{
		//		Members = reportData,
		//		TotalMembers = reportData.Count,
		//		MaleCount = maleCount,
		//		FemaleCount = femaleCount,
		//		OtherCount = otherCount,
		//		TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0),
		//		TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0),
		//		TotalRegFee = reportData.Sum(m => m.RegFee ?? 0),
		//		ReportDate = reportDate,
		//		HasData = reportData.Any(),
		//		UserCompanyCode = companyCode,
		//		CompanyName = companyName,
		//		// Add these properties to your ViewModel if needed
		//		// WithdrawnCount = withdrawnCount,
		//		// ArchivedCount = archivedCount,
		//		// ShareCapitalMissingCount = shareCapitalMissingCount,
		//		// RegistrationFeeMissingCount = regFeeMissingCount,
		//		// SavingsMissingCount = savingsMissingCount,
		//		// NoRegularContributionsCount = noContributionsCount,
		//		// MinimumShareRequirement = minimumShareRequirement,
		//		// RegistrationFeeRequirement = registrationFeeRequirement,
		//		// ActiveContributionPeriodMonths = 3
		//	};

		//	ViewBag.ReportDate = reportDate;
		//	ViewBag.TotalMembers = reportData.Count;
		//	ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital ?? 0);
		//	ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits ?? 0);
		//	ViewBag.TotalRegFee = reportData.Sum(m => m.RegFee ?? 0);
		//	ViewBag.MaleCount = maleCount;
		//	ViewBag.FemaleCount = femaleCount;
		//	ViewBag.OtherCount = otherCount;
		//	ViewBag.HasData = reportData.Any();
		//	ViewBag.WithdrawnCount = withdrawnCount;
		//	ViewBag.ArchivedCount = archivedCount;
		//	ViewBag.ShareCapitalMissingCount = shareCapitalMissingCount;
		//	ViewBag.RegistrationFeeMissingCount = regFeeMissingCount;
		//	ViewBag.SavingsMissingCount = savingsMissingCount;
		//	ViewBag.NoRegularContributionsCount = noContributionsCount;

		//	return View("~/Views/Reports/InactiveMembers.cshtml", viewModel);
		//}

		[HttpPost]
		public async Task<IActionResult> ExportInactiveMembersToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& ((m.Withdrawn == true) || (m.Archived == true) || m.Status == 0))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			var contribShares = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo)
					&& cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
					TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
					TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
				})
				.ToListAsync();

			var reportData = new List<dynamic>();

			foreach (var m in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

				string fullName = "";
				if (m.FullName != null)
				{
					fullName = m.FullName.ToString();
				}
				else
				{
					fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				string sex = "NOT SPECIFIED";
				if (!string.IsNullOrEmpty(m.Sex))
				{
					string sexUpper = m.Sex.ToUpper();
					if (sexUpper == "M" || sexUpper == "MALE")
						sex = "MALE";
					else if (sexUpper == "F" || sexUpper == "FEMALE")
						sex = "FEMALE";
					else
						sex = sexUpper;
				}

				string status = "INACTIVE";
				if (m.Withdrawn == true) status = "WITHDRAWN";
				if (m.Archived == true) status = "ARCHIVED";

				reportData.Add(new
				{
					m.MemberNo,
					FullName = fullName,
					Sex = sex,
					ShareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0,
					SavingsDeposits = memberContrib?.TotalDeposits ?? 0,
					RegFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0,
					Status = status
				});
			}

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Inactive Members");
				var currentRow = 1;

				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				worksheet.Cell(currentRow, 1).Value = $"INACTIVE SACCO MEMBERS AS AT {reportDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				worksheet.Cell(currentRow, 1).Value = "TOTAL MEMBERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow++;

				worksheet.Cell(currentRow, 1).Value = "MALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = maleCount;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow++;

				worksheet.Cell(currentRow, 1).Value = "FEMALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = femaleCount;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow++;

				worksheet.Cell(currentRow, 1).Value = "OTHERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = otherCount;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow += 2;

				var headers = new[] { "MemberNo", "Names", "Sex", "Share Capital", "Savings/Deposits", "Reg Fee", "Status" };

				for (int i = 0; i < headers.Length; i++)
				{
					worksheet.Cell(currentRow, i + 1).Value = headers[i];
					worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
					worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					worksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				currentRow++;

				foreach (var member in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = member.MemberNo;
					worksheet.Cell(currentRow, 2).Value = member.FullName;
					worksheet.Cell(currentRow, 3).Value = member.Sex;
					worksheet.Cell(currentRow, 4).Value = member.ShareCapital;
					worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 5).Value = member.SavingsDeposits;
					worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 6).Value = member.RegFee;
					worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 7).Value = member.Status;

					worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				currentRow++;
				worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
				worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

				worksheet.Cell(currentRow, 4).Value = reportData.Sum(m => (decimal)m.ShareCapital);
				worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 5).Value = reportData.Sum(m => (decimal)m.SavingsDeposits);
				worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.RegFee);
				worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";

				currentRow += 3;
				worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"InactiveMembers_{reportDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportInactiveMembersToPdf(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& ((m.Withdrawn == true) || (m.Archived == true) || m.Status == 0))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			var contribShares = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo)
					&& cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
					TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
					TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
				})
				.ToListAsync();

			var reportData = new List<InactiveMemberPdfData>();

			foreach (var m in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

				string fullName = "";
				if (m.FullName != null)
				{
					fullName = m.FullName.ToString();
				}
				else
				{
					fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				string sex = "NOT SPECIFIED";
				if (!string.IsNullOrEmpty(m.Sex))
				{
					string sexUpper = m.Sex.ToUpper();
					if (sexUpper == "M" || sexUpper == "MALE")
						sex = "MALE";
					else if (sexUpper == "F" || sexUpper == "FEMALE")
						sex = "FEMALE";
					else
						sex = sexUpper;
				}

				string status = "INACTIVE";
				if (m.Withdrawn == true) status = "WITHDRAWN";
				if (m.Archived == true) status = "ARCHIVED";

				reportData.Add(new InactiveMemberPdfData
				{
					MemberNo = m.MemberNo,
					FullName = fullName,
					Sex = sex,
					ShareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0,
					SavingsDeposits = memberContrib?.TotalDeposits ?? 0,
					RegFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0,
					Status = status
				});
			}

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");

			using var stream = new MemoryStream();

			QuestPDF.Fluent.Document.Create(container =>
			{
				container.Page(page =>
				{
					page.Size(PageSizes.A4.Landscape());
					page.Margin(1.5f, Unit.Centimetre);
					page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

					page.Header()
						.AlignCenter()
						.Column(column =>
						{
							column.Item().Text(companyName.ToUpper()).FontSize(18).Bold();
							column.Item().Text($"INACTIVE MEMBERS AS AT {reportDate:dd/MM/yyyy}").FontSize(14).Bold();
						});

					page.Content()
						.PaddingVertical(1, Unit.Centimetre)
						.Column(column =>
						{
							column.Item().Table(statsTable =>
							{
								statsTable.ColumnsDefinition(cols =>
								{
									cols.RelativeColumn(1);
									cols.RelativeColumn(1);
									cols.RelativeColumn(1);
									cols.RelativeColumn(1);
									cols.RelativeColumn(1);
									cols.RelativeColumn(1);
								});

								statsTable.Cell().Element(c => c.Text("TOTAL MEMBERS:").Bold());
								statsTable.Cell().Element(c => c.Text(reportData.Count.ToString()).Bold());
								statsTable.Cell().Element(c => c.Text("MALE:").Bold());
								statsTable.Cell().Element(c => c.Text(maleCount.ToString()).Bold());
								statsTable.Cell().Element(c => c.Text("FEMALE:").Bold());
								statsTable.Cell().Element(c => c.Text(femaleCount.ToString()).Bold());
							});

							column.Item().PaddingTop(1, Unit.Centimetre);

							column.Item().Table(table =>
							{
								table.ColumnsDefinition(cols =>
								{
									cols.RelativeColumn(1);
									cols.RelativeColumn(2);
									cols.RelativeColumn(0.8f);
									cols.RelativeColumn(1.2f);
									cols.RelativeColumn(1.2f);
									cols.RelativeColumn(1.2f);
									cols.RelativeColumn(1);
								});

								table.Header(header =>
								{
									header.Cell().Element(c => c.Text("MemberNo").Bold());
									header.Cell().Element(c => c.Text("Names").Bold());
									header.Cell().Element(c => c.Text("Sex").Bold());
									header.Cell().Element(c => c.Text("Share Capital").Bold());
									header.Cell().Element(c => c.Text("Savings/Deposits").Bold());
									header.Cell().Element(c => c.Text("Reg Fee").Bold());
									header.Cell().Element(c => c.Text("Status").Bold());
								});

								foreach (var member in reportData)
								{
									table.Cell().Element(c => c.Text(member.MemberNo ?? ""));
									table.Cell().Element(c => c.Text(member.FullName ?? ""));
									table.Cell().Element(c => c.Text(member.Sex ?? ""));
									table.Cell().Element(c => c.AlignRight().Text($"{member.ShareCapital:N0}"));
									table.Cell().Element(c => c.AlignRight().Text($"{member.SavingsDeposits:N0}"));
									table.Cell().Element(c => c.AlignRight().Text($"{member.RegFee:N0}"));
									table.Cell().Element(c => c.Text(member.Status ?? ""));
								}
							});
						});

					page.Footer()
						.AlignRight()
						.Text(x => x.Span($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}"));
				});
			}).GeneratePdf(stream);

			var content = stream.ToArray();
			return File(content, "application/pdf", $"InactiveMembers_{reportDate:yyyyMMdd}.pdf");
		}

		#endregion

		#region Members Per SACCO Report

		public IActionResult MembersPerSacco()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new MembersPerSaccoIndexViewModel
			{
				Members = new List<MemberPerSaccoReportVM>(),
				ReportDate = reportDate,
				SaccoName = companyName,
				HasData = false,
				UserCompanyCode = companyCode,
				TotalMembers = 0,
				MaleCount = 0,
				FemaleCount = 0,
				YouthCount = 0
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.SaccoName = companyName;
			ViewBag.HasData = false;

			return View("~/Views/Reports/MembersPerSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> MembersPerSacco(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode)
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var reportData = new List<MemberPerSaccoReportVM>();

			foreach (var m in members)
			{
				string fullName = "";
				if (m.FullName != null)
				{
					fullName = m.FullName.ToString();
				}
				else
				{
					fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				string sex = "NOT SPECIFIED";
				if (!string.IsNullOrEmpty(m.Sex))
				{
					string sexUpper = m.Sex.ToUpper();
					if (sexUpper == "M" || sexUpper == "MALE")
						sex = "MALE";
					else if (sexUpper == "F" || sexUpper == "FEMALE")
						sex = "FEMALE";
					else
						sex = sexUpper;
				}

				int? age = null;
				if (m.Dob.HasValue)
				{
					age = DateTime.Now.Year - m.Dob.Value.Year;
					if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
				}

				string status = "ACTIVE";
				if (m.Withdrawn == true) status = "WITHDRAWN";
				else if (m.Archived == true) status = "ARCHIVED";
				else if (m.Status == 0) status = "INACTIVE";

				reportData.Add(new MemberPerSaccoReportVM
				{
					MemberNo = m.MemberNo,
					FullName = fullName,
					Sex = sex,
					PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
					IDNo = m.Idno ?? "-",
					ApplicDate = m.ApplicDate,
					EffectDate = m.EffectDate,
					MembershipType = m.MembershipType ?? "Individual",
					Station = m.Station ?? "-",
					Age = age,
					Status = status,
					SaccoName = companyName
				});
			}

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int youthCount = reportData.Count(m => m.Age.HasValue && m.Age >= 18 && m.Age <= 35);

			var viewModel = new MembersPerSaccoIndexViewModel
			{
				Members = reportData,
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				YouthCount = youthCount,
				SaccoName = companyName,
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.SaccoName = companyName;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.YouthCount = youthCount;
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/MembersPerSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportMembersPerSaccoToExcel(DateTime reportDate)
		{
			try
			{
				var companyCode = User.FindFirstValue("CompanyCode");
				var companyName = User.FindFirstValue("CompanyName") ?? "";

				var members = await _context.Members
					.Where(m => m.CompanyCode == companyCode)
					.OrderBy(m => m.MemberNo)
					.ToListAsync();

				var reportData = new List<dynamic>();

				foreach (var m in members)
				{
					string fullName = "";
					if (m.FullName != null)
					{
						fullName = m.FullName.ToString();
					}
					else
					{
						fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
						if (string.IsNullOrWhiteSpace(fullName))
							fullName = "N/A";
					}

					string sex = "NOT SPECIFIED";
					if (!string.IsNullOrEmpty(m.Sex))
					{
						string sexUpper = m.Sex.ToUpper();
						if (sexUpper == "M" || sexUpper == "MALE")
							sex = "MALE";
						else if (sexUpper == "F" || sexUpper == "FEMALE")
							sex = "FEMALE";
						else
							sex = sexUpper;
					}

					int? age = null;
					if (m.Dob.HasValue)
					{
						age = DateTime.Now.Year - m.Dob.Value.Year;
						if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
					}

					string status = "ACTIVE";
					if (m.Withdrawn == true) status = "WITHDRAWN";
					else if (m.Archived == true) status = "ARCHIVED";
					else if (m.Status == 0) status = "INACTIVE";

					reportData.Add(new
					{
						m.MemberNo,
						FullName = fullName,
						Sex = sex,
						PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
						IDNo = m.Idno ?? "-",
						ApplicDate = m.ApplicDate,
						Age = age,
						Status = status
					});
				}

				int maleCount = reportData.Count(m => m.Sex == "MALE");
				int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
				int youthCount = reportData.Count(m => m.Age != null && m.Age >= 18 && m.Age <= 35);

				using (var workbook = new XLWorkbook())
				{
					var worksheet = workbook.Worksheets.Add("Members Per SACCO");
					var currentRow = 1;

					worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
					worksheet.Range(currentRow, 1, currentRow, 7).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					currentRow += 2;

					worksheet.Cell(currentRow, 1).Value = $"AS AT {reportDate:dd/MM/yyyy}";
					worksheet.Range(currentRow, 1, currentRow, 7).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					currentRow += 2;

					worksheet.Cell(currentRow, 1).Value = "TOTAL MEMBERS:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = reportData.Count;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
					currentRow++;

					worksheet.Cell(currentRow, 1).Value = "MALE:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = maleCount;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
					currentRow++;

					worksheet.Cell(currentRow, 1).Value = "FEMALE:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = femaleCount;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
					currentRow++;

					worksheet.Cell(currentRow, 1).Value = "YOUTH:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = youthCount;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
					currentRow += 2;

					var headers = new[] { "MemberNo", "Names", "Sex", "PhoneNo", "IDNo", "ApplicDate", "Status" };

					for (int i = 0; i < headers.Length; i++)
					{
						worksheet.Cell(currentRow, i + 1).Value = headers[i];
						worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
						worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
						worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						worksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					}
					currentRow++;

					foreach (var member in reportData)
					{
						worksheet.Cell(currentRow, 1).Value = member.MemberNo;
						worksheet.Cell(currentRow, 2).Value = member.FullName;
						worksheet.Cell(currentRow, 3).Value = member.Sex;
						worksheet.Cell(currentRow, 4).Value = member.PhoneNo;
						worksheet.Cell(currentRow, 5).Value = member.IDNo;
						worksheet.Cell(currentRow, 6).Value = member.ApplicDate?.ToString("dd/MM/yyyy");
						worksheet.Cell(currentRow, 7).Value = member.Status;

						worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						currentRow++;
					}

					currentRow += 2;
					worksheet.Cell(currentRow, 1).Value = "GRAND TOTAL MEMBERS:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = reportData.Count;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

					currentRow += 2;
					worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
					worksheet.Range(currentRow, 1, currentRow, 7).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

					worksheet.Columns().AdjustToContents();

					using (var stream = new MemoryStream())
					{
						workbook.SaveAs(stream);
						var content = stream.ToArray();
						return File(
							content,
							"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
							$"MembersPerSacco_{reportDate:yyyyMMdd}.xlsx"
						);
					}
				}
			}
			catch (Exception ex)
			{
				TempData["Error"] = "Failed to export Excel: " + ex.Message;
				return RedirectToAction("MembersPerSacco", new { reportDate });
			}
		}

        [HttpPost]
        public async Task<IActionResult> ExportMembersPerSaccoToPdf(DateTime reportDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var members = await _context.Members
                    .Where(m => m.CompanyCode == companyCode)
                    .OrderBy(m => m.MemberNo)
                    .ToListAsync();

                var reportData = new List<MemberPerSaccoPdfData>();

                foreach (var m in members)
                {
                    string fullName = !string.IsNullOrWhiteSpace(m.FullName?.ToString())
                        ? m.FullName.ToString()
                        : $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        var s = m.Sex.ToUpper();
                        sex = (s == "M" || s == "MALE") ? "MALE"
                             : (s == "F" || s == "FEMALE") ? "FEMALE"
                             : s;
                    }

                    int? age = null;
                    if (m.Dob.HasValue)
                    {
                        age = DateTime.Now.Year - m.Dob.Value.Year;
                        if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
                    }

                    string status = "ACTIVE";
                    if (m.Withdrawn == true) status = "WITHDRAWN";
                    else if (m.Archived == true) status = "ARCHIVED";
                    else if (m.Status == 0) status = "INACTIVE";

                    reportData.Add(new MemberPerSaccoPdfData
                    {
                        MemberNo = m.MemberNo,
                        FullName = fullName,
                        Sex = sex,
                        PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
                        IDNo = m.Idno ?? "-",
                        ApplicDate = m.ApplicDate,
                        Status = status,
                        Age = age
                    });
                }

                int maleCount = reportData.Count(x => x.Sex == "MALE");
                int femaleCount = reportData.Count(x => x.Sex == "FEMALE");
                int youthCount = reportData.Count(x => x.Age.HasValue && x.Age >= 18 && x.Age <= 35);

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
                            header.Item().AlignCenter().Text("MEMBERS REGISTER").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"As At: {reportDate:dd/MM/yyyy}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics Table
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Members:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Male:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(maleCount.ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Female:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(femaleCount.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Youth (18-35):").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(youthCount.ToString());
                            });

                            // Member Details Table
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("MEMBER DETAILS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // MemberNo
                                    cols.RelativeColumn(2.0f);   // Names
                                    cols.RelativeColumn(0.8f);   // Sex
                                    cols.RelativeColumn(1.2f);   // Phone
                                    cols.RelativeColumn(1.0f);   // ID
                                    cols.RelativeColumn(1.0f);   // Date Registered
                                    cols.RelativeColumn(1.0f);   // Status
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sex").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Registered").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                                });

                                foreach (var member in reportData)
                                {
                                    table.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.FullName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Sex ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.PhoneNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.IDNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.ApplicDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Status ?? "").FontSize(8);
                                }
                            });
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

                return File(stream.ToArray(),
                    "application/pdf",
                    $"MembersPerSacco_{reportDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to export PDF: " + ex.Message;
                return RedirectToAction("MembersPerSacco", new { reportDate });
            }
        }

        //      [HttpPost]
        //public async Task<IActionResult> ExportMembersPerSaccoToPdf(DateTime reportDate)
        //{
        //	try
        //	{
        //		var companyCode = User.FindFirstValue("CompanyCode");
        //		var companyName = User.FindFirstValue("CompanyName") ?? "";

        //		var members = await _context.Members
        //			.Where(m => m.CompanyCode == companyCode)
        //			.OrderBy(m => m.MemberNo)
        //			.ToListAsync();

        //		var reportData = new List<MemberPerSaccoPdfData>();

        //		foreach (var m in members)
        //		{
        //			string fullName = !string.IsNullOrWhiteSpace(m.FullName?.ToString())
        //				? m.FullName.ToString()
        //				: $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

        //			if (string.IsNullOrWhiteSpace(fullName))
        //				fullName = "N/A";

        //			string sex = "NOT SPECIFIED";
        //			if (!string.IsNullOrEmpty(m.Sex))
        //			{
        //				var s = m.Sex.ToUpper();
        //				sex = (s == "M" || s == "MALE") ? "MALE"
        //					 : (s == "F" || s == "FEMALE") ? "FEMALE"
        //					 : s;
        //			}

        //			int? age = null;
        //			if (m.Dob.HasValue)
        //			{
        //				age = DateTime.Now.Year - m.Dob.Value.Year;
        //				if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
        //			}

        //			string status = "ACTIVE";
        //			if (m.Withdrawn == true) status = "WITHDRAWN";
        //			else if (m.Archived == true) status = "ARCHIVED";
        //			else if (m.Status == 0) status = "INACTIVE";

        //			reportData.Add(new MemberPerSaccoPdfData
        //			{
        //				MemberNo = m.MemberNo,
        //				FullName = fullName,
        //				Sex = sex,
        //				PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
        //				IDNo = m.Idno ?? "-",
        //				ApplicDate = m.ApplicDate,
        //				Status = status
        //			});
        //		}

        //		int maleCount = reportData.Count(x => x.Sex == "MALE");
        //		int femaleCount = reportData.Count(x => x.Sex == "FEMALE");
        //		int youthCount = reportData.Count(x => x.Age.HasValue && x.Age >= 18 && x.Age <= 35);

        //		using var stream = new MemoryStream();

        //		QuestPDF.Fluent.Document.Create(container =>
        //		{
        //			container.Page(page =>
        //			{
        //				page.Size(PageSizes.A4.Landscape());
        //				page.Margin(1.5f, Unit.Centimetre);
        //				page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

        //				page.Header()
        //					.AlignCenter()
        //					.Column(column =>
        //					{
        //						column.Item().Text(companyName.ToUpper()).FontSize(18).Bold();
        //						column.Item().Text($"MEMBERS REGISTER AS AT {reportDate:dd/MM/yyyy}").FontSize(14).Bold();
        //					});

        //				page.Content()
        //					.PaddingVertical(1, Unit.Centimetre)
        //					.Column(column =>
        //					{
        //						column.Item().Table(statsTable =>
        //						{
        //							statsTable.ColumnsDefinition(cols =>
        //							{
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(1);
        //							});

        //							statsTable.Cell().Element(c => c.Text($"TOTAL: {reportData.Count}").Bold());
        //							statsTable.Cell().Element(c => c.Text($"MALE: {maleCount}").Bold());
        //							statsTable.Cell().Element(c => c.Text($"FEMALE: {femaleCount}").Bold());
        //							statsTable.Cell().Element(c => c.Text($"YOUTH: {youthCount}").Bold());
        //						});

        //						column.Item().PaddingTop(1, Unit.Centimetre);

        //						column.Item().Table(table =>
        //						{
        //							table.ColumnsDefinition(cols =>
        //							{
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(2);
        //								cols.RelativeColumn(0.8f);
        //								cols.RelativeColumn(1.2f);
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(1);
        //								cols.RelativeColumn(1);
        //							});

        //							table.Header(header =>
        //							{
        //								header.Cell().Element(c => c.Text("MemberNo").Bold());
        //								header.Cell().Element(c => c.Text("Names").Bold());
        //								header.Cell().Element(c => c.Text("Sex").Bold());
        //								header.Cell().Element(c => c.Text("Phone").Bold());
        //								header.Cell().Element(c => c.Text("ID").Bold());
        //								header.Cell().Element(c => c.Text("Date").Bold());
        //								header.Cell().Element(c => c.Text("Status").Bold());
        //							});

        //							foreach (var m in reportData)
        //							{
        //								table.Cell().Element(c => c.Text(m.MemberNo ?? ""));
        //								table.Cell().Element(c => c.Text(m.FullName));
        //								table.Cell().Element(c => c.Text(m.Sex));
        //								table.Cell().Element(c => c.Text(m.PhoneNo));
        //								table.Cell().Element(c => c.Text(m.IDNo));
        //								table.Cell().Element(c => c.Text(m.ApplicDate?.ToString("dd/MM/yyyy") ?? ""));
        //								table.Cell().Element(c => c.Text(m.Status));
        //							}
        //						});
        //					});

        //				page.Footer()
        //					.AlignRight()
        //					.Text(x => x.Span($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}"));
        //			});
        //		}).GeneratePdf(stream);

        //		return File(stream.ToArray(),
        //			"application/pdf",
        //			$"MembersPerSacco_{reportDate:yyyyMMdd}.pdf");
        //	}
        //	catch (Exception ex)
        //	{
        //		TempData["Error"] = "Failed to export PDF: " + ex.Message;
        //		return RedirectToAction("MembersPerSacco", new { reportDate });
        //	}
        //}

        #endregion

        #region Fully Paid Shares Report

        public IActionResult FullyPaidShares()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            // Get share types for the view
            var shareTypes = _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToList();

            var viewModel = new FullyPaidSharesDynamicViewModel
            {
                Members = new List<Dictionary<string, object>>(),
                ShareTypes = shareTypes,
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0,
                TotalShareCapital = 0,
                TotalSavingsDeposits = 0,
                TotalRegistrationFee = 0,
                MinimumShareRequirement = 0,
                RegistrationFeeRequirement = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = false;
            ViewBag.ShareTypes = shareTypes;

            return View("~/Views/Reports/FullyPaidShares.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> FullyPaidShares(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
            decimal registrationFeeRequirement = 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table ONLY (grouped by MemberNo and Sharescode)
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build dynamic report data
            var reportData = new List<Dictionary<string, object>>();
            int maleCount = 0, femaleCount = 0, otherCount = 0;

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullName(member);
                memberData["Sex"] = GetSex(member, ref maleCount, ref femaleCount, ref otherCount);
                memberData["IsFullyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    // Get data from Contrib table for this member and share type
                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    // Store the amount for this share type
                    memberData[$"{prefix}_Amount"] = amount;

                    totalAmount += amount;
                }

                // 7. ONLY include members with at least one contribution (amount > 0)
                if (hasAnyData)
                {
                    // Determine if fully paid based on main share requirements
                    // Get the amount from the main share type
                    decimal mainShareAmount = 0;
                    if (mainShareType != null)
                    {
                        string mainPrefix = mainShareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        mainShareAmount = memberData.ContainsKey($"{mainPrefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{mainPrefix}_Amount"])
                            : 0;
                    }

                    bool isFullyPaid = mainShareAmount >= minimumShareRequirement;

                    memberData["IsFullyPaid"] = isFullyPaid;
                    memberData["TotalAmount"] = totalAmount;
                    memberData["MinimumShareRequirement"] = minimumShareRequirement;
                    memberData["RegistrationFeeRequirement"] = registrationFeeRequirement;

                    reportData.Add(memberData);
                }
            }

            // 8. Create ViewModel
            var viewModel = new FullyPaidSharesDynamicViewModel
            {
                Members = reportData,
                ShareTypes = shareTypes,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                TotalShareCapital = reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"])),
                TotalSavingsDeposits = 0, // Not using this anymore
                TotalRegistrationFee = 0, // Not using this anymore
                ReportDate = reportDate,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                MinimumShareRequirement = minimumShareRequirement,
                RegistrationFeeRequirement = registrationFeeRequirement
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = reportData.Any();
            ViewBag.ShareTypes = shareTypes;

            return View("~/Views/Reports/FullyPaidShares.cshtml", viewModel);
        }

        // Helper methods
        private string GetFullName(Member member)
        {
            if (!string.IsNullOrEmpty(member.FullName))
                return member.FullName;

            var name = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "N/A" : name;
        }

        private string GetSex(Member member, ref int maleCount, ref int femaleCount, ref int otherCount)
        {
            if (string.IsNullOrEmpty(member.Sex))
            {
                otherCount++;
                return "NOT SPECIFIED";
            }

            string sexUpper = member.Sex.ToUpper();
            if (sexUpper == "M" || sexUpper == "MALE")
            {
                maleCount++;
                return "MALE";
            }
            else if (sexUpper == "F" || sexUpper == "FEMALE")
            {
                femaleCount++;
                return "FEMALE";
            }
            else
            {
                otherCount++;
                return sexUpper;
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportFullyPaidSharesToExcel(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table ONLY (grouped by MemberNo and Sharescode)
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build report data
            var reportData = new List<Dictionary<string, object>>();

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullName(member);
                memberData["Sex"] = GetSexForExport(member);
                memberData["IsFullyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    // Get data from Contrib table for this member and share type
                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    memberData[$"{prefix}_Amount"] = amount;
                    totalAmount += amount;
                }

                // 7. ONLY include members with at least one contribution
                if (hasAnyData)
                {
                    // Determine if fully paid based on main share requirements
                    decimal mainShareAmount = 0;
                    if (mainShareType != null)
                    {
                        string mainPrefix = mainShareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        mainShareAmount = memberData.ContainsKey($"{mainPrefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{mainPrefix}_Amount"])
                            : 0;
                    }

                    bool isFullyPaid = mainShareAmount >= minimumShareRequirement;

                    memberData["IsFullyPaid"] = isFullyPaid;
                    memberData["TotalAmount"] = totalAmount;

                    reportData.Add(memberData);
                }
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Fully Paid Shares Report");
                var currentRow = 1;

                // Company Header
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Report Title
                worksheet.Cell(currentRow, 1).Value = $"FULLY PAID SHARES REPORT AS AT {reportDate:dd/MM/yyyy}";
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Headers - Dynamic columns based on share types
                int colIndex = 1;

                // Fixed columns
                worksheet.Cell(currentRow, colIndex++).Value = "#";
                worksheet.Cell(currentRow, colIndex).Value = "MEMBERNO";
                worksheet.Cell(currentRow, colIndex++).Style.Font.Bold = true;
                worksheet.Cell(currentRow, colIndex).Value = "NAMES";
                worksheet.Cell(currentRow, colIndex++).Style.Font.Bold = true;
                worksheet.Cell(currentRow, colIndex).Value = "SEX";
                worksheet.Cell(currentRow, colIndex++).Style.Font.Bold = true;

                // Dynamic columns for each share type
                foreach (var shareType in shareTypes)
                {
                    worksheet.Cell(currentRow, colIndex).Value = shareType.SharesType;
                    worksheet.Cell(currentRow, colIndex).Style.Font.Bold = true;
                    colIndex++;
                }

                // Total and Status columns
                worksheet.Cell(currentRow, colIndex).Value = "TOTAL";
                worksheet.Cell(currentRow, colIndex++).Style.Font.Bold = true;
                worksheet.Cell(currentRow, colIndex).Value = "STATUS";
                worksheet.Cell(currentRow, colIndex++).Style.Font.Bold = true;

                // Apply header styling
                var headerRange = worksheet.Range(currentRow, 1, currentRow, colIndex - 1);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                currentRow++;

                // Data rows
                int serialNo = 1;
                foreach (var member in reportData)
                {
                    colIndex = 1;
                    var isFullyPaid = Convert.ToBoolean(member["IsFullyPaid"]);

                    worksheet.Cell(currentRow, colIndex++).Value = serialNo++;
                    worksheet.Cell(currentRow, colIndex++).Value = member["MemberNo"]?.ToString();
                    worksheet.Cell(currentRow, colIndex++).Value = member["FullName"]?.ToString();
                    worksheet.Cell(currentRow, colIndex++).Value = member["Sex"]?.ToString();

                    // Dynamic data for each share type
                    foreach (var shareType in shareTypes)
                    {
                        var prefix = shareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        var amount = member.ContainsKey($"{prefix}_Amount") ? Convert.ToDecimal(member[$"{prefix}_Amount"]) : 0;
                        worksheet.Cell(currentRow, colIndex++).Value = amount;
                    }

                    // Total and Status
                    worksheet.Cell(currentRow, colIndex++).Value = Convert.ToDecimal(member["TotalAmount"]);
                    worksheet.Cell(currentRow, colIndex++).Value = isFullyPaid ? "FULLY PAID" : "NOT FULLY PAID";

                    // Color code the status
                    var statusCell = worksheet.Cell(currentRow, colIndex - 1);
                    if (isFullyPaid)
                    {
                        statusCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                    }
                    else
                    {
                        statusCell.Style.Fill.BackgroundColor = XLColor.LightPink;
                    }

                    // Apply number format to amount columns
                    var amountStartCol = 5; // After MemberNo, Names, Sex
                    var amountEndCol = 4 + shareTypes.Count + 1; // Share type columns + Total
                    worksheet.Range(currentRow, amountStartCol, currentRow, amountEndCol).Style.NumberFormat.Format = "#,##0.00";

                    currentRow++;
                }

                // Summary section
                currentRow += 2;
                int totalMembers = reportData.Count;
                int fullyPaidCount = reportData.Count(m => Convert.ToBoolean(m["IsFullyPaid"]));
                int notFullyPaidCount = totalMembers - fullyPaidCount;
                decimal totalAmount = reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"]));

                worksheet.Cell(currentRow, 1).Value = "SUMMARY";
                worksheet.Range(currentRow, 1, currentRow, 4).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Total Members:";
                worksheet.Cell(currentRow, 2).Value = totalMembers;
                worksheet.Cell(currentRow, 3).Value = "Fully Paid Members:";
                worksheet.Cell(currentRow, 4).Value = fullyPaidCount;
                worksheet.Cell(currentRow, 5).Value = "Not Fully Paid:";
                worksheet.Cell(currentRow, 6).Value = notFullyPaidCount;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Total Contributions:";
                worksheet.Cell(currentRow, 2).Value = totalAmount;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 3).Value = "Min Share Requirement:";
                worksheet.Cell(currentRow, 4).Value = minimumShareRequirement;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

                // Footer
                currentRow += 2;
                worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"FullyPaidSharesReport_{reportDate:yyyyMMdd}.xlsx");
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportFullyPaidSharesToPdf(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table ONLY
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build report data
            var reportData = new List<Dictionary<string, object>>();
            int maleCount = 0, femaleCount = 0, otherCount = 0;

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullName(member);
                memberData["Sex"] = GetSexWithCount(member, ref maleCount, ref femaleCount, ref otherCount);
                memberData["IsFullyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    memberData[$"{prefix}_Amount"] = amount;
                    totalAmount += amount;
                }

                // 7. ONLY include members with at least one contribution
                if (hasAnyData)
                {
                    decimal mainShareAmount = 0;
                    if (mainShareType != null)
                    {
                        string mainPrefix = mainShareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        mainShareAmount = memberData.ContainsKey($"{mainPrefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{mainPrefix}_Amount"])
                            : 0;
                    }

                    bool isFullyPaid = mainShareAmount >= minimumShareRequirement;

                    memberData["IsFullyPaid"] = isFullyPaid;
                    memberData["TotalAmount"] = totalAmount;

                    reportData.Add(memberData);
                }
            }

            int fullyPaidCount = reportData.Count(m => Convert.ToBoolean(m["IsFullyPaid"]));
            int notFullyPaidCount = reportData.Count - fullyPaidCount;

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
                        header.Item().AlignCenter().Text($"FULLY PAID SHARES REPORT AS AT {reportDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        // Summary Section
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Total Members: {reportData.Count}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Fully Paid: {fullyPaidCount}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Not Fully Paid: {notFullyPaidCount}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Compliance: {(reportData.Count > 0 ? ((decimal)fullyPaidCount / reportData.Count * 100) : 0):F1}%").Bold();
                        });

                        // Gender Summary
                        contentCol.Item().Table(genderTable =>
                        {
                            genderTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Male: {maleCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Female: {femaleCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Others: {otherCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Gender Ratio: {(maleCount + femaleCount > 0 ? ((decimal)maleCount / (maleCount + femaleCount) * 100) : 0):F1}% M / {(maleCount + femaleCount > 0 ? ((decimal)femaleCount / (maleCount + femaleCount) * 100) : 0):F1}% F").Bold();
                        });

                        // Financial Summary
                        contentCol.Item().Table(financeTable =>
                        {
                            financeTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            financeTable.Cell().Border(0.2f).Padding(4).Text($"Total Contributions: {reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"])):N0}").Bold();
                            financeTable.Cell().Border(0.2f).Padding(4).Text($"Min Share Requirement: {minimumShareRequirement:N0}").Bold();
                            financeTable.Cell().Border(0.2f).Padding(4).Text($"Average Contribution: {(reportData.Any() ? reportData.Average(m => Convert.ToDecimal(m["TotalAmount"])) : 0):N0}").Bold();
                            financeTable.Cell().Border(0.2f).Padding(4).Text($"Share Types: {shareTypes.Count}").Bold();
                        });

                        contentCol.Item().PaddingBottom(0.5f, Unit.Centimetre);

                        // Member Details Table with Dynamic Columns
                        contentCol.Item().Table(memberTable =>
                        {
                            // Define columns: #, MemberNo, Names, Sex, then each share type, then Total, Status
                            var colCount = 4 + shareTypes.Count + 2; // 4 fixed + dynamic + 2 (Total, Status)

                            memberTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(0.3f);  // #
                                cols.RelativeColumn(0.8f);  // MemberNo
                                cols.RelativeColumn(1.5f);  // Names
                                cols.RelativeColumn(0.5f);  // Sex

                                foreach (var shareType in shareTypes)
                                {
                                    cols.RelativeColumn(0.8f); // Each share type amount
                                }

                                cols.RelativeColumn(0.8f);  // Total
                                cols.RelativeColumn(0.8f);  // Status
                            });

                            // Header
                            memberTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MEMBER NO").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MEMBER NAME").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("SEX").Bold().FontSize(8);

                                foreach (var shareType in shareTypes)
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text(shareType.SharesType).Bold().FontSize(7);
                                }

                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("TOTAL").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("STATUS").Bold().FontSize(8);
                            });

                            int serialNo = 1;
                            foreach (var member in reportData)
                            {
                                var isFullyPaid = Convert.ToBoolean(member["IsFullyPaid"]);
                                var statusColor = isFullyPaid ? "#d4edda" : "#f8d7da";

                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(serialNo++.ToString()).FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member["MemberNo"]?.ToString() ?? "").FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member["FullName"]?.ToString() ?? "").FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member["Sex"]?.ToString() ?? "-").FontSize(8);

                                // Dynamic share type amounts
                                foreach (var shareType in shareTypes)
                                {
                                    var prefix = shareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                                    var amount = member.ContainsKey($"{prefix}_Amount") ? Convert.ToDecimal(member[$"{prefix}_Amount"]) : 0;
                                    memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{amount:N0}").FontSize(8);
                                }

                                // Total and Status
                                memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{Convert.ToDecimal(member["TotalAmount"]):N0}").FontSize(8).Bold();
                                memberTable.Cell().Border(0.2f).Background(statusColor).Padding(4).AlignCenter()
                                    .Text(isFullyPaid ? "FULLY PAID" : "NOT FULLY PAID").FontSize(8).Bold();
                            }
                        });

                        // Statistics Section
                        contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                        contentCol.Item().Table(statsTable =>
                        {
                            statsTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            var fullyPaidAmount = reportData.Where(m => Convert.ToBoolean(m["IsFullyPaid"])).Sum(m => Convert.ToDecimal(m["TotalAmount"]));
                            var notFullyPaidAmount = reportData.Where(m => !Convert.ToBoolean(m["IsFullyPaid"])).Sum(m => Convert.ToDecimal(m["TotalAmount"]));

                            statsTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("STATISTICS").Bold().FontSize(9).AlignCenter();
                            statsTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("").Bold();
                            statsTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("FULLY PAID").Bold().FontSize(9).AlignCenter();
                            statsTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("NOT FULLY PAID").Bold().FontSize(9).AlignCenter();

                            statsTable.Cell().Border(0.2f).Padding(4).Text("Contributions").FontSize(8);
                            statsTable.Cell().Border(0.2f).Padding(4).Text("");
                            statsTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{fullyPaidAmount:N0}").FontSize(8);
                            statsTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{notFullyPaidAmount:N0}").FontSize(8);

                            statsTable.Cell().Border(0.2f).Padding(4).Text("Members").FontSize(8);
                            statsTable.Cell().Border(0.2f).Padding(4).Text("");
                            statsTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(fullyPaidCount.ToString()).FontSize(8);
                            statsTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(notFullyPaidCount.ToString()).FontSize(8);
                        });
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
                            x.Span($" | Min Share Requirement: {minimumShareRequirement:N0}");
                        });
                });
            }).GeneratePdf(stream);

            return File(stream.ToArray(), "application/pdf", $"FullyPaidSharesReport_{reportDate:yyyyMMdd}.pdf");
        }

        private string GetSexForExport(Member member)
        {
            if (string.IsNullOrEmpty(member.Sex))
                return "NOT SPECIFIED";

            string sexUpper = member.Sex.ToUpper();
            if (sexUpper == "M" || sexUpper == "MALE")
                return "MALE";
            else if (sexUpper == "F" || sexUpper == "FEMALE")
                return "FEMALE";
            else
                return sexUpper;
        }

        private string GetSexWithCount(Member member, ref int maleCount, ref int femaleCount, ref int otherCount)
        {
            if (string.IsNullOrEmpty(member.Sex))
            {
                otherCount++;
                return "NOT SPECIFIED";
            }

            string sexUpper = member.Sex.ToUpper();
            if (sexUpper == "M" || sexUpper == "MALE")
            {
                maleCount++;
                return "MALE";
            }
            else if (sexUpper == "F" || sexUpper == "FEMALE")
            {
                femaleCount++;
                return "FEMALE";
            }
            else
            {
                otherCount++;
                return sexUpper;
            }
        }

        #endregion


        #region Partially Paid Shares Report

        public IActionResult PartiallyPaidShares()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            // Get share types for the view
            var shareTypes = _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToList();

            var viewModel = new PartiallyPaidSharesDynamicViewModel
            {
                Members = new List<Dictionary<string, object>>(),
                ShareTypes = shareTypes,
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0,
                TotalAmount = 0,
                MinimumShareRequirement = 0,
                MembersWithZeroShareCapital = 0,
                MembersWithZeroSavings = 0,
                MembersWithZeroRegFee = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = false;
            ViewBag.ShareTypes = shareTypes;

            return View("~/Views/Reports/PartiallyPaidShares.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> PartiallyPaidShares(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table (grouped by MemberNo and Sharescode)
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build dynamic report data
            var reportData = new List<Dictionary<string, object>>();
            int maleCount = 0, femaleCount = 0, otherCount = 0;
            int zeroShareCapitalCount = 0, zeroSavingsCount = 0, zeroRegFeeCount = 0;

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullNames(member);
                memberData["Sex"] = GetSexWithCounts(member, ref maleCount, ref femaleCount, ref otherCount);
                memberData["IsPartiallyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;
                decimal mainShareAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    memberData[$"{prefix}_Amount"] = amount;
                    totalAmount += amount;

                    // Track main share amount
                    if (shareType.IsMainShares)
                    {
                        mainShareAmount = amount;
                    }
                }

                // 7. Check if member is partially paid
                // A member is partially paid if:
                // - They have at least one contribution (hasAnyData = true)
                // - AND their main share amount is less than the minimum requirement
                // - OR they have zero main share but some other contributions
                bool isFullyPaid = mainShareAmount >= minimumShareRequirement;
                bool isPartiallyPaid = hasAnyData && !isFullyPaid;

                // ALSO include members who have NO main share but have other contributions
                if (hasAnyData && mainShareAmount == 0 && totalAmount > 0)
                {
                    isPartiallyPaid = true;
                }

                if (isPartiallyPaid)
                {
                    memberData["IsPartiallyPaid"] = true;
                    memberData["TotalAmount"] = totalAmount;
                    memberData["MainShareAmount"] = mainShareAmount;
                    memberData["MinimumShareRequirement"] = minimumShareRequirement;

                    // Track zero balances for statistics
                    if (mainShareAmount == 0) zeroShareCapitalCount++;

                    // Check other share types for zero balances
                    bool hasSavings = false;
                    bool hasRegFee = false;
                    foreach (var shareType in shareTypes)
                    {
                        string prefix = shareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        var amount = memberData.ContainsKey($"{prefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{prefix}_Amount"])
                            : 0;

                        if (amount > 0)
                        {
                            string shareTypeLower = shareType.SharesType?.ToLower() ?? "";
                            if (shareTypeLower.Contains("saving") ||
                                shareTypeLower.Contains("deposit") ||
                                shareTypeLower.Contains("passbook"))
                                hasSavings = true;
                            if (shareTypeLower.Contains("registration") ||
                                shareTypeLower.Contains("reg fee") ||
                                shareTypeLower.Contains("regfee"))
                                hasRegFee = true;
                        }
                    }

                    if (!hasSavings) zeroSavingsCount++;
                    if (!hasRegFee) zeroRegFeeCount++;

                    reportData.Add(memberData);
                }
            }

            // 8. Create ViewModel
            var viewModel = new PartiallyPaidSharesDynamicViewModel
            {
                Members = reportData,
                ShareTypes = shareTypes,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                TotalAmount = reportData.Any() ? reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"])) : 0,
                ReportDate = reportDate,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                MinimumShareRequirement = minimumShareRequirement,
                MembersWithZeroShareCapital = zeroShareCapitalCount,
                MembersWithZeroSavings = zeroSavingsCount,
                MembersWithZeroRegFee = zeroRegFeeCount
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = reportData.Any();
            ViewBag.ShareTypes = shareTypes;
            ViewBag.MinimumShareRequirement = minimumShareRequirement;

            return View("~/Views/Reports/PartiallyPaidShares.cshtml", viewModel);
        }

        private string GetFullNames(Member member)
        {
            if (!string.IsNullOrEmpty(member.FullName))
                return member.FullName;

            var name = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "N/A" : name;
        }

        private string GetSexWithCounts(Member member, ref int maleCount, ref int femaleCount, ref int otherCount)
        {
            if (string.IsNullOrEmpty(member.Sex))
            {
                otherCount++;
                return "NOT SPECIFIED";
            }

            string sexUpper = member.Sex.ToUpper();
            if (sexUpper == "M" || sexUpper == "MALE")
            {
                maleCount++;
                return "MALE";
            }
            else if (sexUpper == "F" || sexUpper == "FEMALE")
            {
                femaleCount++;
                return "FEMALE";
            }
            else
            {
                otherCount++;
                return sexUpper;
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPartiallyPaidSharesToExcel(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table ONLY
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build report data
            var reportData = new List<Dictionary<string, object>>();

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullNames(member);
                memberData["Sex"] = GetSexForExport(member);
                memberData["IsPartiallyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    memberData[$"{prefix}_Amount"] = amount;
                    totalAmount += amount;
                }

                // 7. ONLY include members with at least one contribution that are partially paid
                if (hasAnyData)
                {
                    decimal mainShareAmount = 0;
                    if (mainShareType != null)
                    {
                        string mainPrefix = mainShareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        mainShareAmount = memberData.ContainsKey($"{mainPrefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{mainPrefix}_Amount"])
                            : 0;
                    }

                    bool isFullyPaid = mainShareAmount >= minimumShareRequirement;
                    bool isPartiallyPaid = !isFullyPaid && totalAmount > 0;

                    if (isPartiallyPaid)
                    {
                        memberData["IsPartiallyPaid"] = true;
                        memberData["TotalAmount"] = totalAmount;
                        memberData["MainShareAmount"] = mainShareAmount;
                        reportData.Add(memberData);
                    }
                }
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Partially Paid Shares");
                var currentRow = 1;

                // Company Header
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Report Title
                worksheet.Cell(currentRow, 1).Value = $"PARTIALLY PAID SHARES REPORT AS AT {reportDate:dd/MM/yyyy}";
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Headers - Dynamic columns based on share types
                int colIndex = 1;

                // Fixed columns
                worksheet.Cell(currentRow, colIndex++).Value = "#";
                worksheet.Cell(currentRow, colIndex++).Value = "MEMBERNO";
                worksheet.Cell(currentRow, colIndex++).Value = "NAMES";
                worksheet.Cell(currentRow, colIndex++).Value = "SEX";

                // Dynamic columns for each share type
                foreach (var shareType in shareTypes)
                {
                    worksheet.Cell(currentRow, colIndex).Value = shareType.SharesType;
                    colIndex++;
                }

                // Total column
                worksheet.Cell(currentRow, colIndex).Value = "TOTAL";
                colIndex++;

                // Apply header styling
                var headerRange = worksheet.Range(currentRow, 1, currentRow, colIndex - 1);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                currentRow++;

                // Data rows
                int serialNo = 1;
                foreach (var member in reportData)
                {
                    colIndex = 1;

                    worksheet.Cell(currentRow, colIndex++).Value = serialNo++;
                    worksheet.Cell(currentRow, colIndex++).Value = member["MemberNo"]?.ToString();
                    worksheet.Cell(currentRow, colIndex++).Value = member["FullName"]?.ToString();
                    worksheet.Cell(currentRow, colIndex++).Value = member["Sex"]?.ToString();

                    // Dynamic data for each share type
                    foreach (var shareType in shareTypes)
                    {
                        var prefix = shareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        var amount = member.ContainsKey($"{prefix}_Amount") ? Convert.ToDecimal(member[$"{prefix}_Amount"]) : 0;
                        worksheet.Cell(currentRow, colIndex++).Value = amount;
                    }

                    // Total
                    worksheet.Cell(currentRow, colIndex++).Value = Convert.ToDecimal(member["TotalAmount"]);

                    // Apply number format to amount columns
                    var amountStartCol = 5; // After MemberNo, Names, Sex
                    var amountEndCol = 4 + shareTypes.Count + 1; // Share type columns + Total
                    worksheet.Range(currentRow, amountStartCol, currentRow, amountEndCol).Style.NumberFormat.Format = "#,##0.00";

                    currentRow++;
                }

                // Summary section
                currentRow += 2;
                int totalMembers = reportData.Count;
                decimal totalAmount = reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"]));

                worksheet.Cell(currentRow, 1).Value = "SUMMARY";
                worksheet.Range(currentRow, 1, currentRow, 4).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Total Partially Paid Members:";
                worksheet.Cell(currentRow, 2).Value = totalMembers;
                worksheet.Cell(currentRow, 3).Value = "Total Contributions:";
                worksheet.Cell(currentRow, 4).Value = totalAmount;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Min Share Requirement:";
                worksheet.Cell(currentRow, 2).Value = minimumShareRequirement;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";

                // Footer
                currentRow += 2;
                worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Range(currentRow, 1, currentRow, 4 + shareTypes.Count).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"PartiallyPaidShares_{reportDate:yyyyMMdd}.xlsx");
                }
            }
        }


        [HttpPost]
        public async Task<IActionResult> ExportPartiallyPaidSharesToPdf(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // 1. Get ALL share types for this company
            var shareTypes = await _context.Sharetypes
                .Where(st => st.CompanyCode == companyCode)
                .OrderBy(st => st.Priority)
                .ThenBy(st => st.SharesCode)
                .ToListAsync();

            // 2. Get minimum share requirement from main shares
            var mainShareType = shareTypes.FirstOrDefault(st => st.IsMainShares == true);
            decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;

            // 3. Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // 4. Get ALL contributions from Contrib table ONLY
            var contribData = await _context.Contribs
                .Where(c => memberNos.Contains(c.MemberNo)
                    && c.CompanyCode == companyCode
                    && c.Sharescode != null)
                .GroupBy(c => new { c.MemberNo, c.Sharescode })
                .Select(g => new
                {
                    g.Key.MemberNo,
                    g.Key.Sharescode,
                    TotalAmount = g.Sum(c => c.Amount ?? 0)
                })
                .ToListAsync();

            // 5. Build report data
            var reportData = new List<Dictionary<string, object>>();
            int maleCount = 0, femaleCount = 0, otherCount = 0;

            foreach (var member in members)
            {
                var memberData = new Dictionary<string, object>();

                // Basic member info
                memberData["MemberNo"] = member.MemberNo;
                memberData["FullName"] = GetFullNames(member);
                memberData["Sex"] = GetSexWithCounts(member, ref maleCount, ref femaleCount, ref otherCount);
                memberData["IsPartiallyPaid"] = false;
                memberData["TotalAmount"] = 0m;

                bool hasAnyData = false;
                decimal totalAmount = 0;

                // 6. For each share type, get the contribution amount
                foreach (var shareType in shareTypes)
                {
                    string shareCode = shareType.SharesCode;
                    string prefix = shareCode.Replace(" ", "_").Replace("-", "_").ToUpper();

                    var contribForMember = contribData
                        .FirstOrDefault(c => c.MemberNo == member.MemberNo && c.Sharescode == shareCode);

                    decimal amount = contribForMember?.TotalAmount ?? 0;

                    if (amount > 0)
                    {
                        hasAnyData = true;
                    }

                    memberData[$"{prefix}_Amount"] = amount;
                    totalAmount += amount;
                }

                // 7. ONLY include partially paid members
                if (hasAnyData)
                {
                    decimal mainShareAmount = 0;
                    if (mainShareType != null)
                    {
                        string mainPrefix = mainShareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                        mainShareAmount = memberData.ContainsKey($"{mainPrefix}_Amount")
                            ? Convert.ToDecimal(memberData[$"{mainPrefix}_Amount"])
                            : 0;
                    }

                    bool isFullyPaid = mainShareAmount >= minimumShareRequirement;
                    bool isPartiallyPaid = !isFullyPaid && totalAmount > 0;

                    if (isPartiallyPaid)
                    {
                        memberData["IsPartiallyPaid"] = true;
                        memberData["TotalAmount"] = totalAmount;
                        reportData.Add(memberData);
                    }
                }
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
                        header.Item().AlignCenter().Text($"PARTIALLY PAID SHARES REPORT AS AT {reportDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        // Summary Section
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Total Partially Paid: {reportData.Count}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Min Share Requirement: {minimumShareRequirement:N0}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Total Contributions: {reportData.Sum(m => Convert.ToDecimal(m["TotalAmount"])):N0}").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text($"Share Types: {shareTypes.Count}").Bold();
                        });

                        // Gender Summary
                        contentCol.Item().Table(genderTable =>
                        {
                            genderTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Male: {maleCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Female: {femaleCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Others: {otherCount}").Bold();
                            genderTable.Cell().Border(0.2f).Padding(4).Text($"Gender Ratio: {(maleCount + femaleCount > 0 ? ((decimal)maleCount / (maleCount + femaleCount) * 100) : 0):F1}% M / {(maleCount + femaleCount > 0 ? ((decimal)femaleCount / (maleCount + femaleCount) * 100) : 0):F1}% F").Bold();
                        });

                        contentCol.Item().PaddingBottom(0.5f, Unit.Centimetre);

                        // Member Details Table with Dynamic Columns
                        contentCol.Item().Table(memberTable =>
                        {
                            var colCount = 4 + shareTypes.Count + 1; // 4 fixed + dynamic + Total

                            memberTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(0.3f);  // #
                                cols.RelativeColumn(0.8f);  // MemberNo
                                cols.RelativeColumn(1.5f);  // Names
                                cols.RelativeColumn(0.5f);  // Sex

                                foreach (var shareType in shareTypes)
                                {
                                    cols.RelativeColumn(0.8f); // Each share type amount
                                }

                                cols.RelativeColumn(0.8f);  // Total
                            });

                            // Header
                            memberTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MEMBER NO").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MEMBER NAME").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("SEX").Bold().FontSize(8);

                                foreach (var shareType in shareTypes)
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text(shareType.SharesType).Bold().FontSize(7);
                                }

                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("TOTAL").Bold().FontSize(8);
                            });

                            int serialNo = 1;
                            foreach (var member in reportData)
                            {
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(serialNo++.ToString()).FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member["MemberNo"]?.ToString() ?? "").FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member["FullName"]?.ToString() ?? "").FontSize(8);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member["Sex"]?.ToString() ?? "-").FontSize(8);

                                // Dynamic share type amounts
                                foreach (var shareType in shareTypes)
                                {
                                    var prefix = shareType.SharesCode.Replace(" ", "_").Replace("-", "_").ToUpper();
                                    var amount = member.ContainsKey($"{prefix}_Amount") ? Convert.ToDecimal(member[$"{prefix}_Amount"]) : 0;
                                    memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{amount:N0}").FontSize(8);
                                }

                                // Total
                                memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{Convert.ToDecimal(member["TotalAmount"]):N0}").FontSize(8).Bold();
                            }
                        });
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
                            x.Span($" | Min Share Requirement: {minimumShareRequirement:N0}");
                        });
                });
            }).GeneratePdf(stream);

            return File(stream.ToArray(), "application/pdf", $"PartiallyPaidShares_{reportDate:yyyyMMdd}.pdf");
        }

		#endregion

		#region Helper Classes

		public class ActiveMemberPdfData
		{
			public string MemberNo { get; set; }
			public string Name { get; set; }
			public string Sex { get; set; }
			public decimal Share { get; set; }
			public decimal Deposits { get; set; }
			public decimal RegFee { get; set; }
		}

		public class InactiveMemberPdfData
		{
			public string MemberNo { get; set; }
			public string FullName { get; set; }
			public string Sex { get; set; }
			public decimal ShareCapital { get; set; }
			public decimal SavingsDeposits { get; set; }
			public decimal RegFee { get; set; }
			public string Status { get; set; }
		}

		public class MemberPerSaccoPdfData
		{
			public string MemberNo { get; set; }
			public string FullName { get; set; }
			public string Sex { get; set; }
			public string PhoneNo { get; set; }
			public string IDNo { get; set; }
			public DateTime? ApplicDate { get; set; }
			public int? Age { get; set; }
			public string Status { get; set; }
		}

		public class FullyPaidMemberData
		{
			public string MemberNo { get; set; }
			public string FullName { get; set; }
			public string Sex { get; set; }
			public decimal ShareCapital { get; set; }
			public decimal SavingsDeposits { get; set; }
			public decimal RegistrationFee { get; set; }
            public bool IsFullyPaid { get; set; }
        }

		public class PartiallyPaidMemberData
		{
			public string MemberNo { get; set; }
			public string FullName { get; set; }
			public string Sex { get; set; }
			public decimal ShareCapital { get; set; }
			public decimal SavingsDeposits { get; set; }
			public decimal RegistrationFee { get; set; }
		}

		#endregion

		#region Shares and Loans Report

		[HttpGet]
		public IActionResult SharesLoansMember()
		{
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            var viewModel = new SharesAndLoansIndexViewModel
            {
                Members = new List<SharesAndLoansReportViewModel>(),
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0,
                YouthCount = 0,
                TotalShareCapital = 0,
                TotalDeposits = 0,
                TotalRegFee = 0,
                TotalPassbook = 0,
                TotalLoans = 0,
                TotalOutstandingBalance = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;
            // return RedirectToAction("SharesLoansPERSacco", "MemberReport");

            return View( viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SharesLoansMember(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var mno = User.FindFirstValue("MemberNo");
            // Get all active members
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo == mno
                    && (m.Withdrawn == false || m.Withdrawn == null)
                    && (m.Archived == false || m.Archived == null))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // Get SHARE CAPITAL from Shares table (sum of TotalShares for each member)
            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShareCapital = g.Sum(s => s.TotalShares ?? 0)
                })
                .ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

            // Get DEPOSITS/SAVINGS from Contribs table
            var savings = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalCapital = g.Sum(c => c.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(c => c.DepositsAmount ?? 0)
                })
                .ToDictionaryAsync(c => c.MemberNo, c => c.TotalCapital + c.TotalDeposits);

            // Get REGISTRATION FEE from ContribShares and Member table
            var regFees = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

            // Get LOANS (total disbursed loan amount) from Loans table
            var loans = await _context.Loans
                .Where(l => memberNos.Contains(l.MemberNo)
                    && l.CompanyCode == companyCode
                    && l.Status == (int)Status.Disbursed)
                .GroupBy(l => l.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalLoans = g.Sum(l => l.LoanAmt ?? 0)
                })
                .ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

            // Get PASSBOOK amount (if you have a passbook table, otherwise use 0)
            // Passbook typically represents statement balance or special savings
            var passbook = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
                })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

            // Get CIG/GIG names from Member's Cigcode
            var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode))
                .Select(m => m.Cigcode)
                .Distinct()
                .ToList();

            var gigDetails = await _context.CIGs
                .Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            var reportData = new List<SharesAndLoansReportViewModel>();
            int maleCount = 0, femaleCount = 0, otherCount = 0, youthCount = 0;
            decimal totalShareCapital = 0, totalDeposits = 0, totalRegFee = 0, totalPassbook = 0, totalLoans = 0;

            foreach (var member in members)
            {
                // Calculate age
                int? age = null;
                if (member.Dob.HasValue)
                {
                    age = DateTime.Now.Year - member.Dob.Value.Year;
                    if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
                    if (age >= 18 && age <= 35) youthCount++;
                }

                // Build full name
                string fullName = "N/A";
                if (!string.IsNullOrWhiteSpace(member.Surname) || !string.IsNullOrWhiteSpace(member.OtherNames))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                // Get GIG Name
                string gigName = "UNASSIGNED";
                if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
                {
                    gigName = gigDetails[member.Cigcode];
                }
                else if (!string.IsNullOrEmpty(member.Cigcode))
                {
                    gigName = member.Cigcode;
                }

                // Count gender
                if (member.Sex?.ToUpper() == "MALE" || member.Sex?.ToUpper() == "M")
                {
                    maleCount++;
                }
                else if (member.Sex?.ToUpper() == "FEMALE" || member.Sex?.ToUpper() == "F")
                {
                    femaleCount++;
                }
                else
                {
                    otherCount++;
                }

                // Get financial values
                decimal shareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0);
                decimal deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0;
                decimal regFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0);
                decimal passbookAmount = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0;
                decimal loanAmount = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0;

                totalShareCapital += shareCapital;
                totalDeposits += deposits;
                totalRegFee += regFee;
                totalPassbook += passbookAmount;
                totalLoans += loanAmount;

                reportData.Add(new SharesAndLoansReportViewModel
                {
                    MemberNo = member.MemberNo,
                    FullName = fullName,
                    Age = age,
                    CIGName = gigName,
                    ShareCapital = shareCapital,
                    Deposits = deposits,
                    RegFee = regFee,
                    Passbook = passbookAmount,
                    TotalLoans = loanAmount,
                    DateRegistered = member.ApplicDate,
                    Sex = member.Sex ?? "Not Specified"
                });
            }

            var viewModel = new SharesAndLoansIndexViewModel
            {
                Members = reportData.OrderBy(m => m.MemberNo).ToList(),
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                YouthCount = youthCount,
                TotalShareCapital = totalShareCapital,
                TotalDeposits = totalDeposits,
                TotalRegFee = totalRegFee,
                TotalPassbook = totalPassbook,
                TotalLoans = totalLoans,
                ReportDate = reportDate,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.CompanyName = companyName;
            ViewBag.TotalMembers = reportData.Count;
            ViewBag.TotalShareCapital = totalShareCapital;
            ViewBag.TotalDeposits = totalDeposits;
            ViewBag.TotalRegFee = totalRegFee;
            ViewBag.TotalPassbook = totalPassbook;
            ViewBag.TotalLoans = totalLoans;
            ViewBag.MaleCount = maleCount;
            ViewBag.FemaleCount = femaleCount;
            ViewBag.YouthCount = youthCount;
            ViewBag.HasData = reportData.Any();

            return View("~/Views/Reports/SharesLoansPERSacco.cshtml", viewModel);
        }

        [HttpGet]
		public IActionResult SharesLoansPERSacco()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new SharesAndLoansIndexViewModel
			{
				Members = new List<SharesAndLoansReportViewModel>(),
				ReportDate = reportDate,
				HasData = false,
				UserCompanyCode = companyCode,
				CompanyName = companyName,
				TotalMembers = 0,
				MaleCount = 0,
				FemaleCount = 0,
				OtherCount = 0,
				YouthCount = 0,
				TotalShareCapital = 0,
				TotalDeposits = 0,
				TotalRegFee = 0,
				TotalPassbook = 0,
				TotalLoans = 0,
				TotalOutstandingBalance = 0
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.CompanyName = companyName;
			ViewBag.HasData = false;
            // return RedirectToAction("SharesLoansPERSacco", "MemberReport");

            return View("~/Views/Reports/SharesLoansPERSacco.cshtml", viewModel);
		}

        [HttpPost]
		public async Task<IActionResult> SharesLoansPERSacco(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == false || m.Withdrawn == null)
					&& (m.Archived == false || m.Archived == null))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			// Get SHARE CAPITAL from Shares table (sum of TotalShares for each member)
			var shares = await _context.Shares
				.Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
				.GroupBy(s => s.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalShareCapital = g.Sum(s => s.TotalShares ?? 0)
				})
				.ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

			// Get DEPOSITS/SAVINGS from Contribs table
			var savings = await _context.Contribs
				.Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
				.GroupBy(c => c.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalSavings = g.Sum(c => c.Amount ?? 0)
				})
				.ToDictionaryAsync(c => c.MemberNo, c => c.TotalSavings);

			// Get REGISTRATION FEE from ContribShares and Member table
			var regFees = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
				})
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

			// Get LOANS (total disbursed loan amount) from Loans table
			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo)
					&& l.CompanyCode == companyCode
					&& l.Status == (int)Status.Disbursed)
				.GroupBy(l => l.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalLoans = g.Sum(l => l.LoanAmt ?? 0)
				})
				.ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

			// Get PASSBOOK amount (if you have a passbook table, otherwise use 0)
			// Passbook typically represents statement balance or special savings
			var passbook = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new
				{
					MemberNo = g.Key,
					TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
				})
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

			// Get CIG/GIG names from Member's Cigcode
			var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode))
				.Select(m => m.Cigcode)
				.Distinct()
				.ToList();

			var gigDetails = await _context.CIGs
				.Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
				.ToDictionaryAsync(g => g.GigCode, g => g.GigName);

			var reportData = new List<SharesAndLoansReportViewModel>();
			int maleCount = 0, femaleCount = 0, otherCount = 0, youthCount = 0;
			decimal totalShareCapital = 0, totalDeposits = 0, totalRegFee = 0, totalPassbook = 0, totalLoans = 0;

			foreach (var member in members)
			{
				// Calculate age
				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
					if (age >= 18 && age <= 35) youthCount++;
				}

				// Build full name
				string fullName = "N/A";
				if (!string.IsNullOrWhiteSpace(member.Surname) || !string.IsNullOrWhiteSpace(member.OtherNames))
				{
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				// Get GIG Name
				string gigName = "UNASSIGNED";
				if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
				{
					gigName = gigDetails[member.Cigcode];
				}
				else if (!string.IsNullOrEmpty(member.Cigcode))
				{
					gigName = member.Cigcode;
				}

				// Count gender
				if (member.Sex?.ToUpper() == "MALE" || member.Sex?.ToUpper() == "M")
				{
					maleCount++;
				}
				else if (member.Sex?.ToUpper() == "FEMALE" || member.Sex?.ToUpper() == "F")
				{
					femaleCount++;
				}
				else
				{
					otherCount++;
				}

				// Get financial values
				decimal shareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0);
				decimal deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0;
				decimal regFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0);
				decimal passbookAmount = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0;
				decimal loanAmount = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0;

				totalShareCapital += shareCapital;
				totalDeposits += deposits;
				totalRegFee += regFee;
				totalPassbook += passbookAmount;
				totalLoans += loanAmount;

				reportData.Add(new SharesAndLoansReportViewModel
				{
					MemberNo = member.MemberNo,
					FullName = fullName,
					Age = age,
					CIGName = gigName,
					ShareCapital = shareCapital,
					Deposits = deposits,
					RegFee = regFee,
					Passbook = passbookAmount,
					TotalLoans = loanAmount,
					DateRegistered = member.ApplicDate,
					Sex = member.Sex ?? "Not Specified"
				});
			}

			var viewModel = new SharesAndLoansIndexViewModel
			{
				Members = reportData.OrderBy(m => m.MemberNo).ToList(),
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				OtherCount = otherCount,
				YouthCount = youthCount,
				TotalShareCapital = totalShareCapital,
				TotalDeposits = totalDeposits,
				TotalRegFee = totalRegFee,
				TotalPassbook = totalPassbook,
				TotalLoans = totalLoans,
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.CompanyName = companyName;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.TotalShareCapital = totalShareCapital;
			ViewBag.TotalDeposits = totalDeposits;
			ViewBag.TotalRegFee = totalRegFee;
			ViewBag.TotalPassbook = totalPassbook;
			ViewBag.TotalLoans = totalLoans;
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.YouthCount = youthCount;
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/SharesLoansPERSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportSharesAndLoansToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == false || m.Withdrawn == null)
					&& (m.Archived == false || m.Archived == null))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			var shares = await _context.Shares
				.Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
				.GroupBy(s => s.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalShareCapital = g.Sum(s => s.TotalShares ?? 0) })
				.ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

			var savings = await _context.Contribs
				.Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
				.GroupBy(c => c.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalSavings = g.Sum(c => c.Amount ?? 0) })
				.ToDictionaryAsync(c => c.MemberNo, c => c.TotalSavings);

			var regFees = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0) })
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo) && l.CompanyCode == companyCode && l.Status == (int)Status.Disbursed)
				.GroupBy(l => l.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalLoans = g.Sum(l => l.LoanAmt ?? 0) })
				.ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

			var passbook = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0) })
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

			var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode)).Select(m => m.Cigcode).Distinct().ToList();
			var gigDetails = await _context.CIGs
				.Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
				.ToDictionaryAsync(g => g.GigCode, g => g.GigName);

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
				if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
				}

				string gigName = "UNASSIGNED";
				if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
					gigName = gigDetails[member.Cigcode];
				else if (!string.IsNullOrEmpty(member.Cigcode))
					gigName = member.Cigcode;

				string sex = "NOT SPECIFIED";
				if (!string.IsNullOrEmpty(member.Sex))
				{
					string sexUpper = member.Sex.ToUpper();
					if (sexUpper == "M" || sexUpper == "MALE")
						sex = "MALE";
					else if (sexUpper == "F" || sexUpper == "FEMALE")
						sex = "FEMALE";
					else
						sex = sexUpper;
				}

				reportData.Add(new
				{
					member.MemberNo,
					Names = fullName,
					Age = age,
					CIGName = gigName,
					ShareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0),
					Deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0,
					RegFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0),
					Passbook = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0,
					Loans = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0,
					DateRegistered = member.ApplicDate?.ToString("dd/MM/yyyy") ?? "-",
					Sex = sex
				});
			}

			using var workbook = new XLWorkbook();
			var worksheet = workbook.Worksheets.Add("Shares and Loans Report");
			int currentRow = 1;

			worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
			worksheet.Range(currentRow, 1, currentRow, 11).Merge();
			worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
			worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
			currentRow += 2;

			worksheet.Cell(currentRow, 1).Value = $"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}";
			worksheet.Range(currentRow, 1, currentRow, 11).Merge();
			worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
			worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
			currentRow += 2;

			string[] headers = { "MemberNo", "Names", "Age", "CIGName", "Sex", "Share Capital", "Deposits", "Reg Fee", "Passbook", "Loans", "Date Registered" };
			for (int i = 0; i < headers.Length; i++)
			{
				worksheet.Cell(currentRow, i + 1).Value = headers[i];
				worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
				worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
				worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
			}
			currentRow++;

			foreach (var member in reportData)
			{
				worksheet.Cell(currentRow, 1).Value = member.MemberNo;
				worksheet.Cell(currentRow, 2).Value = member.Names;
				worksheet.Cell(currentRow, 3).Value = member.Age;
				worksheet.Cell(currentRow, 4).Value = member.CIGName;
				worksheet.Cell(currentRow, 5).Value = member.Sex;
				worksheet.Cell(currentRow, 6).Value = member.ShareCapital;
				worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
				worksheet.Cell(currentRow, 7).Value = member.Deposits;
				worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
				worksheet.Cell(currentRow, 8).Value = member.RegFee;
				worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
				worksheet.Cell(currentRow, 9).Value = member.Passbook;
				worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
				worksheet.Cell(currentRow, 10).Value = member.Loans;
				worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
				worksheet.Cell(currentRow, 11).Value = member.DateRegistered;
				currentRow++;
			}

			currentRow++;
			worksheet.Cell(currentRow, 5).Value = "GRAND TOTAL:";
			worksheet.Cell(currentRow, 5).Style.Font.SetBold();
			worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.ShareCapital);
			worksheet.Cell(currentRow, 6).Style.Font.SetBold();
			worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
			worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.Deposits);
			worksheet.Cell(currentRow, 7).Style.Font.SetBold();
			worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
			worksheet.Cell(currentRow, 8).Value = reportData.Sum(m => (decimal)m.RegFee);
			worksheet.Cell(currentRow, 8).Style.Font.SetBold();
			worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
			worksheet.Cell(currentRow, 9).Value = reportData.Sum(m => (decimal)m.Passbook);
			worksheet.Cell(currentRow, 9).Style.Font.SetBold();
			worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
			worksheet.Cell(currentRow, 10).Value = reportData.Sum(m => (decimal)m.Loans);
			worksheet.Cell(currentRow, 10).Style.Font.SetBold();
			worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

			worksheet.Columns().AdjustToContents();

			using var stream = new MemoryStream();
			workbook.SaveAs(stream);
			return File(stream.ToArray(),
				"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				$"SharesAndLoansReport_{reportDate:yyyyMMdd}.xlsx");
		}

		[HttpPost]
		public async Task<IActionResult> ExportSharesAndLoansToPdf(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == false || m.Withdrawn == null)
					&& (m.Archived == false || m.Archived == null))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			var shares = await _context.Shares
				.Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
				.GroupBy(s => s.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalShareCapital = g.Sum(s => s.TotalShares ?? 0) })
				.ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

			var savings = await _context.Contribs
				.Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
				.GroupBy(c => c.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalSavings = g.Sum(c => c.Amount ?? 0) })
				.ToDictionaryAsync(c => c.MemberNo, c => c.TotalSavings);

			var regFees = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0) })
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo) && l.CompanyCode == companyCode && l.Status == (int)Status.Disbursed)
				.GroupBy(l => l.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalLoans = g.Sum(l => l.LoanAmt ?? 0) })
				.ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

			var passbook = await _context.ContribShares
				.Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
				.GroupBy(cs => cs.MemberNo)
				.Select(g => new { MemberNo = g.Key, TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0) })
				.ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

			var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode)).Select(m => m.Cigcode).Distinct().ToList();
			var gigDetails = await _context.CIGs
				.Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
				.ToDictionaryAsync(g => g.GigCode, g => g.GigName);

			var reportData = new List<SharesAndLoansReportViewModel>();

			foreach (var member in members)
			{
				string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
				if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
				}

				string gigName = "UNASSIGNED";
				if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
					gigName = gigDetails[member.Cigcode];
				else if (!string.IsNullOrEmpty(member.Cigcode))
					gigName = member.Cigcode;

				reportData.Add(new SharesAndLoansReportViewModel
				{
					MemberNo = member.MemberNo,
					FullName = fullName,
					Age = age,
					CIGName = gigName,
					Sex = member.Sex ?? "Not Specified",
					ShareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0),
					Deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0,
					RegFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0),
					Passbook = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0,
					TotalLoans = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0,
					DateRegistered = member.ApplicDate
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
						header.Item().AlignCenter().Text($"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}").FontSize(12).Bold();
						header.Item().AlignCenter().Text($"Generated By: {User.Identity?.Name ?? "System"} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
						header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
						header.Item().PaddingBottom(0.5f, Unit.Centimetre);
					});

					page.Content().Table(table =>
					{
						table.ColumnsDefinition(cols =>
						{
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.5f);
							cols.RelativeColumn(0.5f);
							cols.RelativeColumn(1.2f);
							cols.RelativeColumn(0.8f);
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.0f);
							cols.RelativeColumn(1.0f);
						});

						table.Header(header =>
						{
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Age").Bold().FontSize(8);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("CIGName").Bold().FontSize(8);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sex").Bold().FontSize(8);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Share Capital").Bold().FontSize(7);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Deposits").Bold().FontSize(7);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reg Fee").Bold().FontSize(7);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Passbook").Bold().FontSize(7);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loans").Bold().FontSize(7);
							header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Registered").Bold().FontSize(7);
						});

						foreach (var member in reportData)
						{
							table.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).Text(member.FullName ?? "N/A").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Age?.ToString() ?? "-").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).Text(member.CIGName ?? "Unassigned").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).Text(member.Sex ?? "-").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.ShareCapital:N0}").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.Deposits:N0}").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.RegFee:N0}").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.Passbook:N0}").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalLoans:N0}").FontSize(7);
							table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.DateRegistered?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
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
			return File(content, "application/pdf", $"SharesAndLoansReport_{reportDate:yyyyMMdd}.pdf");
		}
        [HttpPost]
        public async Task<IActionResult> ExportSharesAndLoansMemberToExcel(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
			var mno = User.FindFirstValue("MemberNo") ?? "";
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo == mno
                    && (m.Withdrawn == false || m.Withdrawn == null)
                    && (m.Archived == false || m.Archived == null))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalShareCapital = g.Sum(s => s.TotalShares ?? 0) })
                .ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

            //var savings = await _context.Contribs
            //    .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
            //    .GroupBy(c => c.MemberNo)
            //    .Select(g => new { MemberNo = g.Key, TotalSavings = g.Sum(c => c.Amount ?? 0) })
            //    .ToDictionaryAsync(c => c.MemberNo, c => c.TotalSavings);
            var savings = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalCapital = g.Sum(c => c.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(c => c.DepositsAmount ?? 0)
                })
                .ToDictionaryAsync(c => c.MemberNo, c => c.TotalCapital + c.TotalDeposits);

            var regFees = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0) })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

            var loans = await _context.Loans
                .Where(l => memberNos.Contains(l.MemberNo) && l.CompanyCode == companyCode && l.Status == (int)Status.Disbursed)
                .GroupBy(l => l.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalLoans = g.Sum(l => l.LoanAmt ?? 0) })
                .ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

            var passbook = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0) })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

            var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode)).Select(m => m.Cigcode).Distinct().ToList();
            var gigDetails = await _context.CIGs
                .Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            var reportData = new List<dynamic>();

            foreach (var member in members)
            {
                string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                int? age = null;
                if (member.Dob.HasValue)
                {
                    age = DateTime.Now.Year - member.Dob.Value.Year;
                    if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
                }

                string gigName = "UNASSIGNED";
                if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
                    gigName = gigDetails[member.Cigcode];
                else if (!string.IsNullOrEmpty(member.Cigcode))
                    gigName = member.Cigcode;

                string sex = "NOT SPECIFIED";
                if (!string.IsNullOrEmpty(member.Sex))
                {
                    string sexUpper = member.Sex.ToUpper();
                    if (sexUpper == "M" || sexUpper == "MALE")
                        sex = "MALE";
                    else if (sexUpper == "F" || sexUpper == "FEMALE")
                        sex = "FEMALE";
                    else
                        sex = sexUpper;
                }

                reportData.Add(new
                {
                    member.MemberNo,
                    Names = fullName,
                    Age = age,
                    CIGName = gigName,
                    ShareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0),
                    Deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0,
                    RegFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0),
                    Passbook = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0,
                    Loans = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0,
                    DateRegistered = member.ApplicDate?.ToString("dd/MM/yyyy") ?? "-",
                    Sex = sex
                });
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Shares and Loans Report");
            int currentRow = 1;

            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] headers = { "MemberNo", "Names", "Age", "CIGName", "Sex", "Share Capital", "Deposits", "Reg Fee", "Passbook", "Loans", "Date Registered" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var member in reportData)
            {
                worksheet.Cell(currentRow, 1).Value = member.MemberNo;
                worksheet.Cell(currentRow, 2).Value = member.Names;
                worksheet.Cell(currentRow, 3).Value = member.Age;
                worksheet.Cell(currentRow, 4).Value = member.CIGName;
                worksheet.Cell(currentRow, 5).Value = member.Sex;
                worksheet.Cell(currentRow, 6).Value = member.ShareCapital;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = member.Deposits;
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 8).Value = member.RegFee;
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 9).Value = member.Passbook;
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 10).Value = member.Loans;
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 11).Value = member.DateRegistered;
                currentRow++;
            }

            currentRow++;
            worksheet.Cell(currentRow, 5).Value = "GRAND TOTAL:";
            worksheet.Cell(currentRow, 5).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.ShareCapital);
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.Deposits);
            worksheet.Cell(currentRow, 7).Style.Font.SetBold();
            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 8).Value = reportData.Sum(m => (decimal)m.RegFee);
            worksheet.Cell(currentRow, 8).Style.Font.SetBold();
            worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 9).Value = reportData.Sum(m => (decimal)m.Passbook);
            worksheet.Cell(currentRow, 9).Style.Font.SetBold();
            worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 10).Value = reportData.Sum(m => (decimal)m.Loans);
            worksheet.Cell(currentRow, 10).Style.Font.SetBold();
            worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"SharesAndLoansReport_{reportDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportSharesAndLoansMemberToPdf(DateTime reportDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
			var mno = User.FindFirstValue("MemberNo") ?? "";
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo == mno
                    && (m.Withdrawn == false || m.Withdrawn == null)
                    && (m.Archived == false || m.Archived == null))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalShareCapital = g.Sum(s => s.TotalShares ?? 0) })
                .ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

            //var savings = await _context.Contribs
            //    .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
            //    .GroupBy(c => c.MemberNo)
            //    .Select(g => new { MemberNo = g.Key, TotalSavings = g.Sum(c => c.Amount ?? 0) })
            //    .ToDictionaryAsync(c => c.MemberNo, c => c.TotalSavings);

            var savings = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalCapital = g.Sum(c => c.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(c => c.DepositsAmount ?? 0)
                })
                .ToDictionaryAsync(c => c.MemberNo, c => c.TotalCapital + c.TotalDeposits);

            var regFees = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0) })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

            var loans = await _context.Loans
                .Where(l => memberNos.Contains(l.MemberNo) && l.CompanyCode == companyCode && l.Status == (int)Status.Disbursed)
                .GroupBy(l => l.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalLoans = g.Sum(l => l.LoanAmt ?? 0) })
                .ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

            var passbook = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new { MemberNo = g.Key, TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0) })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

            var gigCodes = members.Where(m => !string.IsNullOrEmpty(m.Cigcode)).Select(m => m.Cigcode).Distinct().ToList();
            var gigDetails = await _context.CIGs
                .Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode)
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            var reportData = new List<SharesAndLoansReportViewModel>();

            foreach (var member in members)
            {
                string fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                int? age = null;
                if (member.Dob.HasValue)
                {
                    age = DateTime.Now.Year - member.Dob.Value.Year;
                    if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
                }

                string gigName = "UNASSIGNED";
                if (!string.IsNullOrEmpty(member.Cigcode) && gigDetails.ContainsKey(member.Cigcode))
                    gigName = gigDetails[member.Cigcode];
                else if (!string.IsNullOrEmpty(member.Cigcode))
                    gigName = member.Cigcode;

                reportData.Add(new SharesAndLoansReportViewModel
                {
                    MemberNo = member.MemberNo,
                    FullName = fullName,
                    Age = age,
                    CIGName = gigName,
                    Sex = member.Sex ?? "Not Specified",
                    ShareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0),
                    Deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0,
                    RegFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0),
                    Passbook = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0,
                    TotalLoans = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0,
                    DateRegistered = member.ApplicDate
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
                        header.Item().AlignCenter().Text($"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Generated By: {User.Identity?.Name ?? "System"} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(0.5f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Age").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("CIGName").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sex").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Share Capital").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Deposits").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reg Fee").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Passbook").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loans").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Registered").Bold().FontSize(7);
                        });

                        foreach (var member in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(member.FullName ?? "N/A").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Age?.ToString() ?? "-").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(member.CIGName ?? "Unassigned").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(member.Sex ?? "-").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.ShareCapital:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.Deposits:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.RegFee:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.Passbook:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalLoans:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.DateRegistered?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
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
            return File(content, "application/pdf", $"SharesAndLoansReport_{reportDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Periodic Registered Members Report

        [HttpGet]
        public IActionResult PeriodicRegisteredMembers()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;
            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            var viewModel = new PeriodicRegisteredMembersIndexViewModel
            {
                Members = new List<PeriodicRegisteredMembersViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = false;
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/PeriodicRegisteredMembers.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> PeriodicRegisteredMembers(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // Validate dates
            if (startDate > endDate)
            {
                TempData["ErrorMessage"] = "Start date cannot be greater than end date";
                return RedirectToAction("PeriodicRegisteredMembers");
            }

            // Get members registered within the date range
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && m.ApplicDate.HasValue
                    && m.ApplicDate.Value.Date >= startDate.Date
                    && m.ApplicDate.Value.Date <= endDate.Date
                    && (m.Withdrawn == false || m.Withdrawn == null)
                    && (m.Archived == false || m.Archived == null))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var reportData = new List<PeriodicRegisteredMembersViewModel>();

            foreach (var m in members)
            {
                string fullName = "";
                if (m.FullName != null)
                {
                    fullName = m.FullName.ToString();
                }
                else
                {
                    fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                string sex = "NOT SPECIFIED";
                if (!string.IsNullOrEmpty(m.Sex))
                {
                    string sexUpper = m.Sex.ToUpper();
                    if (sexUpper == "M" || sexUpper == "MALE")
                        sex = "MALE";
                    else if (sexUpper == "F" || sexUpper == "FEMALE")
                        sex = "FEMALE";
                    else
                        sex = sexUpper;
                }

                reportData.Add(new PeriodicRegisteredMembersViewModel
                {
                    MemberNo = m.MemberNo,
                    FullName = fullName,
                    Sex = sex,
                    RegistrationDate = m.ApplicDate,
                    MobileNo = m.PhoneNo ?? m.MobileNo ?? "-",
                    IdNo = m.Idno ?? "-",
                    Email = m.Email ?? m.EmailAddress,
                    Station = m.Station ?? "-",
                    MembershipType = m.MembershipType ?? "Individual"
                });
            }

            int maleCount = reportData.Count(m => m.Sex == "MALE");
            int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
            int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

            var viewModel = new PeriodicRegisteredMembersIndexViewModel
            {
                Members = reportData,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                StartDate = startDate,
                EndDate = endDate,
                ReportDate = DateTime.Now,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.ReportDate = DateTime.Now;
            ViewBag.HasData = reportData.Any();
            ViewBag.CompanyName = companyName;
            ViewBag.TotalMembers = reportData.Count;
            ViewBag.MaleCount = maleCount;
            ViewBag.FemaleCount = femaleCount;
            ViewBag.OtherCount = otherCount;

            return View("~/Views/Reports/PeriodicRegisteredMembers.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportPeriodicRegisteredMembersToExcel(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";

                var members = await _context.Members
                    .Where(m => m.CompanyCode == companyCode
                        && m.ApplicDate.HasValue
                        && m.ApplicDate.Value.Date >= startDate.Date
                        && m.ApplicDate.Value.Date <= endDate.Date
                        && (m.Withdrawn == false || m.Withdrawn == null)
                        && (m.Archived == false || m.Archived == null))
                    .OrderBy(m => m.MemberNo)
                    .ToListAsync();

                var reportData = new List<dynamic>();

                foreach (var m in members)
                {
                    string fullName = "";
                    if (m.FullName != null)
                        fullName = m.FullName.ToString();
                    else
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        string sexUpper = m.Sex.ToUpper();
                        if (sexUpper == "M" || sexUpper == "MALE")
                            sex = "MALE";
                        else if (sexUpper == "F" || sexUpper == "FEMALE")
                            sex = "FEMALE";
                        else
                            sex = sexUpper;
                    }

                    reportData.Add(new
                    {
                        m.MemberNo,
                        FullName = fullName,
                        Sex = sex,
                        RegistrationDate = m.ApplicDate?.ToString("dd/MM/yyyy") ?? "-",
                        MobileNo = m.PhoneNo ?? m.MobileNo ?? "-",
                        IDNo = m.Idno ?? "-",
                        Email = m.Email ?? m.EmailAddress,
                        Station = m.Station ?? "-",
                        MembershipType = m.MembershipType ?? "Individual"
                    });
                }

                int maleCount = reportData.Count(m => m.Sex == "MALE");
                int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
                int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Periodic Registered Members");
                    int currentRow = 1;

                    // Header
                    worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                    worksheet.Range(currentRow, 1, currentRow, 9).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    worksheet.Cell(currentRow, 1).Value = $"MEMBERS REGISTERED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}";
                    worksheet.Range(currentRow, 1, currentRow, 9).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Statistics
                    worksheet.Cell(currentRow, 1).Value = "TOTAL MEMBERS:";
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 2).Value = reportData.Count;
                    worksheet.Cell(currentRow, 2).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 3).Value = "MALE:";
                    worksheet.Cell(currentRow, 3).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 4).Value = maleCount;
                    worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 5).Value = "FEMALE:";
                    worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 6).Value = femaleCount;
                    worksheet.Cell(currentRow, 6).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 7).Value = "OTHERS:";
                    worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Value = otherCount;
                    worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    currentRow += 2;

                    // Headers
                    string[] headers = { "MemberNo", "Names", "Sex", "Registration Date", "Mobile No", "ID No", "Email", "Station", "Membership Type" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(currentRow, i + 1).Value = headers[i];
                        worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                        worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    }
                    currentRow++;

                    foreach (var member in reportData)
                    {
                        worksheet.Cell(currentRow, 1).Value = member.MemberNo;
                        worksheet.Cell(currentRow, 2).Value = member.FullName;
                        worksheet.Cell(currentRow, 3).Value = member.Sex;
                        worksheet.Cell(currentRow, 4).Value = member.RegistrationDate;
                        worksheet.Cell(currentRow, 5).Value = member.MobileNo;
                        worksheet.Cell(currentRow, 6).Value = member.IDNo;
                        worksheet.Cell(currentRow, 7).Value = member.Email;
                        worksheet.Cell(currentRow, 8).Value = member.Station;
                        worksheet.Cell(currentRow, 9).Value = member.MembershipType;
                        worksheet.Range(currentRow, 1, currentRow, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                    }

                    currentRow += 2;
                    worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Range(currentRow, 1, currentRow, 9).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"PeriodicRegisteredMembers_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting periodic registered members to Excel");
                TempData["ErrorMessage"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("PeriodicRegisteredMembers");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPeriodicRegisteredMembersToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var members = await _context.Members
                    .Where(m => m.CompanyCode == companyCode
                        && m.ApplicDate.HasValue
                        && m.ApplicDate.Value.Date >= startDate.Date
                        && m.ApplicDate.Value.Date <= endDate.Date
                        && (m.Withdrawn == false || m.Withdrawn == null)
                        && (m.Archived == false || m.Archived == null))
                    .OrderBy(m => m.MemberNo)
                    .ToListAsync();

                var reportData = new List<PeriodicRegisteredMemberPdfData>();

                foreach (var m in members)
                {
                    string fullName = "";
                    if (m.FullName != null)
                        fullName = m.FullName.ToString();
                    else
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    string sex = "NOT SPECIFIED";
                    if (!string.IsNullOrEmpty(m.Sex))
                    {
                        string sexUpper = m.Sex.ToUpper();
                        if (sexUpper == "M" || sexUpper == "MALE")
                            sex = "MALE";
                        else if (sexUpper == "F" || sexUpper == "FEMALE")
                            sex = "FEMALE";
                        else
                            sex = sexUpper;
                    }

                    reportData.Add(new PeriodicRegisteredMemberPdfData
                    {
                        MemberNo = m.MemberNo,
                        FullName = fullName,
                        Sex = sex,
                        RegistrationDate = m.ApplicDate,
                        MobileNo = m.PhoneNo ?? m.MobileNo ?? "-",
                        IDNo = m.Idno ?? "-"
                    });
                }

                int maleCount = reportData.Count(x => x.Sex == "MALE");
                int femaleCount = reportData.Count(x => x.Sex == "FEMALE");
                int otherCount = reportData.Count(x => x.Sex != "MALE" && x.Sex != "FEMALE" && x.Sex != "NOT SPECIFIED");

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
                            header.Item().AlignCenter().Text($"MEMBERS REGISTERED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Members:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Male:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(maleCount.ToString());

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Female:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(femaleCount.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Others:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(otherCount.ToString());
                            });

                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("REGISTERED MEMBERS DETAILS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(2.0f);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sex").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Registration Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Mobile No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID No").Bold().FontSize(8);
                                });

                                foreach (var member in reportData)
                                {
                                    table.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.FullName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.Sex ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.RegistrationDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.MobileNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.IDNo ?? "").FontSize(8);
                                }
                            });
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

                return File(stream.ToArray(), "application/pdf", $"PeriodicRegisteredMembers_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting periodic registered members to PDF");
                TempData["ErrorMessage"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("PeriodicRegisteredMembers");
            }
        }

        public class PeriodicRegisteredMemberPdfData
        {
            public string MemberNo { get; set; }
            public string FullName { get; set; }
            public string Sex { get; set; }
            public DateTime? RegistrationDate { get; set; }
            public string MobileNo { get; set; }
            public string IDNo { get; set; }
        }

        #endregion


        #region Withdrawn Members Report

        [HttpGet]
        public IActionResult WithdrawnMembers()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            var viewModel = new WithdrawnMembersIndexViewModel
            {
                Members = new List<WithdrawnMembersViewModel>(),
                ReportDate = reportDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalMembers = 0,
                MaleCount = 0,
                FemaleCount = 0,
                OtherCount = 0,
                TotalShareCapital = 0,
                TotalSavingsDeposits = 0,
                TotalRegistrationFee = 0,
                TotalPassbookAmount = 0,
                GrandTotalAmount = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.HasData = false;
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/WithdrawnMembers.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> WithdrawnMembers(DateTime? startDate, DateTime? endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // Build query for withdrawn members
            var query = _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && m.Withdrawn == true);

            // Apply date filters if provided
            if (startDate.HasValue)
            {
                query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date <= endDate.Value.Date);
            }

            var members = await query
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // Get contributions
            var contribShares = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo)
                    && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                    TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
                })
                .ToListAsync();

            // Get shares from Shares table as fallback
            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo)
                    && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShares = g.Sum(s => s.TotalShares ?? 0)
                })
                .ToListAsync();

            var reportData = new List<WithdrawnMembersViewModel>();

            foreach (var m in members)
            {
                var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);
                var memberShare = shares.FirstOrDefault(s => s.MemberNo == m.MemberNo);

                decimal shareCapital = 0;
                if (memberContrib != null)
                    shareCapital = memberContrib.TotalShareCapital;
                else if (memberShare != null)
                    shareCapital = memberShare.TotalShares;
                else
                    shareCapital = m.ShareCap ?? 0;

                decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
                decimal registrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;
                decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;

                decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

                string fullName = "";
                if (m.FullName != null)
                    fullName = m.FullName.ToString();
                else
                    fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                string sex = "NOT SPECIFIED";
                if (!string.IsNullOrEmpty(m.Sex))
                {
                    string sexUpper = m.Sex.ToUpper();
                    if (sexUpper == "M" || sexUpper == "MALE")
                        sex = "MALE";
                    else if (sexUpper == "F" || sexUpper == "FEMALE")
                        sex = "FEMALE";
                    else
                        sex = sexUpper;
                }

                int? membershipDuration = null;
                if (m.ApplicDate.HasValue && m.AuditDateTime.HasValue)
                {
                    membershipDuration = (int)((m.AuditDateTime.Value - m.ApplicDate.Value).TotalDays / 30);
                }

                reportData.Add(new WithdrawnMembersViewModel
                {
                    MemberNo = m.MemberNo,
                    FullName = fullName,
                    WithdrawalDate = m.AuditDateTime,
                    ShareCapital = shareCapital,
                    SavingsDeposits = savingsDeposits,
                    RegistrationFee = registrationFee,
                    PassbookAmount = passbookAmount,
                    TotalAmount = totalAmount,
                    IdNo = m.Idno ?? "-",
                    Sex = sex,
                    PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
                    DateJoined = m.ApplicDate,
                    MembershipDuration = membershipDuration
                });
            }

            // Sort by member number
            reportData = reportData.OrderBy(m => m.MemberNo).ToList();

            int maleCount = reportData.Count(m => m.Sex == "MALE");
            int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
            int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

            var viewModel = new WithdrawnMembersIndexViewModel
            {
                Members = reportData,
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                TotalShareCapital = reportData.Sum(m => m.ShareCapital),
                TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits),
                TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee),
                TotalPassbookAmount = reportData.Sum(m => m.PassbookAmount),
                GrandTotalAmount = reportData.Sum(m => m.TotalAmount),
                StartDate = startDate,
                EndDate = endDate,
                ReportDate = DateTime.Now,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.ReportDate = DateTime.Now;
            ViewBag.HasData = reportData.Any();
            ViewBag.CompanyName = companyName;
            ViewBag.TotalMembers = reportData.Count;
            ViewBag.MaleCount = maleCount;
            ViewBag.FemaleCount = femaleCount;
            ViewBag.OtherCount = otherCount;
            ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital);
            ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits);
            ViewBag.TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee);
            ViewBag.TotalPassbookAmount = reportData.Sum(m => m.PassbookAmount);
            ViewBag.GrandTotalAmount = reportData.Sum(m => m.TotalAmount);

            return View("~/Views/Reports/WithdrawnMembers.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportWithdrawnMembersToExcel(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";

                var query = _context.Members
                    .Where(m => m.CompanyCode == companyCode
                        && m.Withdrawn == true);

                if (startDate.HasValue)
                    query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date >= startDate.Value.Date);

                if (endDate.HasValue)
                    query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date <= endDate.Value.Date);

                var members = await query.OrderBy(m => m.MemberNo).ToListAsync();

                var memberNos = members.Select(m => m.MemberNo).ToList();

                var contribShares = await _context.ContribShares
                    .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                    .GroupBy(cs => cs.MemberNo)
                    .Select(g => new
                    {
                        MemberNo = g.Key,
                        TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                        TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                        TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                        TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
                    })
                    .ToListAsync();

                var reportData = new List<dynamic>();

                foreach (var m in members)
                {
                    var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

                    decimal shareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0;
                    decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
                    decimal registrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;
                    decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;
                    decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

                    string fullName = "";
                    if (m.FullName != null)
                        fullName = m.FullName.ToString();
                    else
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    reportData.Add(new
                    {
                        m.MemberNo,
                        FullName = fullName,
                        WithdrawalDate = m.AuditDateTime?.ToString("dd/MM/yyyy") ?? "-",
                        ShareCapital = shareCapital,
                        SavingsDeposits = savingsDeposits,
                        RegistrationFee = registrationFee,
                        PassbookAmount = passbookAmount,
                        TotalAmount = totalAmount
                    });
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Withdrawn Members");
                    int currentRow = 1;

                    // Header
                    worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                    worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    string dateRange = "ALL TIME";
                    if (startDate.HasValue && endDate.HasValue)
                        dateRange = $"{startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
                    else if (startDate.HasValue)
                        dateRange = $"From {startDate.Value:dd/MM/yyyy}";
                    else if (endDate.HasValue)
                        dateRange = $"Up to {endDate.Value:dd/MM/yyyy}";

                    worksheet.Cell(currentRow, 1).Value = $"WITHDRAWN MEMBERS - {dateRange}";
                    worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Headers
                    string[] headers = { "MemberNo", "Names", "Withdrawal Date", "Share Capital", "Savings/Deposits", "Reg Fee", "Passbook", "Total Amount" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(currentRow, i + 1).Value = headers[i];
                        worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                        worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                        worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    }
                    currentRow++;

                    foreach (var member in reportData)
                    {
                        worksheet.Cell(currentRow, 1).Value = member.MemberNo;
                        worksheet.Cell(currentRow, 2).Value = member.FullName;
                        worksheet.Cell(currentRow, 3).Value = member.WithdrawalDate;
                        worksheet.Cell(currentRow, 4).Value = member.ShareCapital;
                        worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 5).Value = member.SavingsDeposits;
                        worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 6).Value = member.RegistrationFee;
                        worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 7).Value = member.PassbookAmount;
                        worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 8).Value = member.TotalAmount;
                        worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                        worksheet.Range(currentRow, 1, currentRow, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                    }

                    // Grand Total
                    currentRow++;
                    worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
                    worksheet.Cell(currentRow, 3).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                    worksheet.Cell(currentRow, 4).Value = reportData.Sum(m => (decimal)m.ShareCapital);
                    worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(currentRow, 5).Value = reportData.Sum(m => (decimal)m.SavingsDeposits);
                    worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.RegistrationFee);
                    worksheet.Cell(currentRow, 6).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.PassbookAmount);
                    worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(currentRow, 8).Value = reportData.Sum(m => (decimal)m.TotalAmount);
                    worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 8).Style.Fill.SetBackgroundColor(XLColor.LightYellow);

                    currentRow += 2;
                    worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"WithdrawnMembers_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting withdrawn members to Excel");
                TempData["ErrorMessage"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("WithdrawnMembers");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportWithdrawnMembersToPdf(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                var query = _context.Members
                    .Where(m => m.CompanyCode == companyCode
                        && m.Withdrawn == true);

                if (startDate.HasValue)
                    query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date >= startDate.Value.Date);

                if (endDate.HasValue)
                    query = query.Where(m => m.AuditDateTime.HasValue && m.AuditDateTime.Value.Date <= endDate.Value.Date);

                var members = await query.OrderBy(m => m.MemberNo).ToListAsync();

                var memberNos = members.Select(m => m.MemberNo).ToList();

                var contribShares = await _context.ContribShares
                    .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                    .GroupBy(cs => cs.MemberNo)
                    .Select(g => new
                    {
                        MemberNo = g.Key,
                        TotalShareCapital = g.Sum(cs => cs.ShareCapitalAmount ?? 0),
                        TotalDeposits = g.Sum(cs => cs.DepositsAmount ?? 0),
                        TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0),
                        TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
                    })
                    .ToListAsync();

                var reportData = new List<WithdrawnMemberPdfData>();

                foreach (var m in members)
                {
                    var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

                    decimal shareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0;
                    decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
                    decimal registrationFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0;
                    decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;
                    decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

                    string fullName = "";
                    if (m.FullName != null)
                        fullName = m.FullName.ToString();
                    else
                        fullName = $"{m.Surname ?? ""} {m.OtherNames ?? ""}".Trim();

                    reportData.Add(new WithdrawnMemberPdfData
                    {
                        MemberNo = m.MemberNo,
                        FullName = fullName,
                        WithdrawalDate = m.AuditDateTime,
                        ShareCapital = shareCapital,
                        SavingsDeposits = savingsDeposits,
                        RegistrationFee = registrationFee,
                        PassbookAmount = passbookAmount,
                        TotalAmount = totalAmount
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
                            header.Item().AlignCenter().Text("WITHDRAWN MEMBERS").FontSize(12).Bold();

                            string dateRange = "ALL TIME";
                            if (startDate.HasValue && endDate.HasValue)
                                dateRange = $"{startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
                            else if (startDate.HasValue)
                                dateRange = $"From {startDate.Value:dd/MM/yyyy}";
                            else if (endDate.HasValue)
                                dateRange = $"Up to {endDate.Value:dd/MM/yyyy}";

                            header.Item().AlignCenter().Text($"Period: {dateRange}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Withdrawn:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(x => x.TotalAmount):N0}");
                            });

                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("WITHDRAWN MEMBERS DETAILS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(2.0f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Withdrawal Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Share Capital").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Savings/Deposits").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reg Fee").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Passbook").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Amount").Bold().FontSize(7);
                                });

                                foreach (var member in reportData)
                                {
                                    table.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(member.FullName ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.WithdrawalDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.ShareCapital:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.SavingsDeposits:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.RegistrationFee:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.PassbookAmount:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalAmount:N0}").FontSize(8).Bold();
                                }

                                // Grand Total Row
                                table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(x => x.ShareCapital):N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(x => x.SavingsDeposits):N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(x => x.RegistrationFee):N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(x => x.PassbookAmount):N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(x => x.TotalAmount):N0}").Bold().FontSize(9).FontColor(QuestPDF.Infrastructure.Color.FromHex("#dc3545"));
                            });
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

                return File(stream.ToArray(), "application/pdf", $"WithdrawnMembers_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting withdrawn members to PDF");
                TempData["ErrorMessage"] = $"Error exporting: {ex.Message}";
                return RedirectToAction("WithdrawnMembers");
            }
        }

        public class WithdrawnMemberPdfData
        {
            public string MemberNo { get; set; }
            public string FullName { get; set; }
            public DateTime? WithdrawalDate { get; set; }
            public decimal ShareCapital { get; set; }
            public decimal SavingsDeposits { get; set; }
            public decimal RegistrationFee { get; set; }
            public decimal PassbookAmount { get; set; }
            public decimal TotalAmount { get; set; }
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// Gets the logged-in member's number from claims
        /// </summary>
        private string GetCurrentMemberNo()
        {
            return User.FindFirstValue("MemberNo") ?? "";
        }

        /// <summary>
        /// Gets the logged-in member's company code from claims
        /// </summary>
        private string GetCurrentCompanyCode()
        {
            return User.FindFirstValue("CompanyCode") ?? "";
        }

        /// <summary>
        /// Gets the current member's details
        /// </summary>
        private async Task<Member> GetCurrentMemberAsync()
        {
            var memberNo = GetCurrentMemberNo();
            var companyCode = GetCurrentCompanyCode();
            return await _context.Members
                .FirstOrDefaultAsync(m => m.MemberNo == memberNo && m.CompanyCode == companyCode);
        }

        /// <summary>
        /// Gets the loan status string from the status code
        /// </summary>
        private string GetLoanStatusString(int? status)
        {
            return status switch
            {
                1 => "Draft",
                2 => "Submitted",
                3 => "Under Appraisal",
                4 => "Approved",
                5 => "Endorsed",
                6 => "Disbursed",
                7 => "Closed",
                8 => "Defaulted",
                9 => "Written Off",
                10 => "Rejected",
                _ => "Unknown"
            };
        }

        #endregion

        #region Report 1: Member Share Contributions Statement (Using Contrib Table)

        /// <summary>
        /// GET: Displays the member's share contributions statement
        /// Uses Contrib table grouped by Share Type
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ShareContributionsStatement()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get all contributions for this member from Contrib table
                var contributions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo
                        && c.CompanyCode == companyCode
                        && c.Amount > 0) // Only include positive amounts
                    .OrderByDescending(c => c.ContrDate)
                    .ThenBy(c => c.Sharescode)
                    .ToListAsync();

                // Get all share types for this company
                var shareTypes = await _context.Sharetypes
                    .Where(st => st.CompanyCode == companyCode)
                    .ToDictionaryAsync(st => st.SharesCode, st => st);

                // Group contributions by Share Type
                var groupedContributions = contributions
                    .GroupBy(c => c.Sharescode)
                    .Where(g => shareTypes.ContainsKey(g.Key) && g.Any(x => x.Amount > 0))
                    .Select(g => new ShareTypeGroupDto
                    {
                        ShareCode = g.Key,
                        ShareTypeName = shareTypes.ContainsKey(g.Key) ? shareTypes[g.Key].SharesType : g.Key,
                        IsMainShares = shareTypes.ContainsKey(g.Key) && shareTypes[g.Key].IsMainShares,
                        IsShareCapital = shareTypes.ContainsKey(g.Key) && shareTypes[g.Key].Issharecapital,
                        Priority = shareTypes.ContainsKey(g.Key) ? shareTypes[g.Key].Priority : 999,
                        Contributions = g.Select(c => new ShareContributionDetailDto
                        {
                            Id = c.Id,
                            TransactionDate = c.ContrDate ?? c.AuditDateTime ?? c.AuditTime,
                            Amount = c.Amount ?? 0,
                            Description = c.Remarks ?? c.TransferDesc ?? "Share Contribution",
                            ReceiptNo = c.ReceiptNo,
                            TransactionNo = c.TransactionNo ?? c.TransNo,
                            ChequeNo = c.ChequeNo,
                            RefNo = c.RefNo,
                            Posted = c.Posted
                        }).ToList(),
                        TotalAmount = g.Sum(c => c.Amount ?? 0),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(g => g.Priority)
                    .ThenBy(g => g.ShareTypeName)
                    .ToList();

                // Calculate overall totals
                decimal totalShareCapital = groupedContributions
                    .Where(g => g.IsShareCapital)
                    .Sum(g => g.TotalAmount);

                decimal totalDeposits = groupedContributions
                    .Where(g => !g.IsShareCapital)
                    .Sum(g => g.TotalAmount);

                var viewModel = new ShareContributionsStatementViewModel
                {
                    MemberNo = member.MemberNo,
                    MemberName = GetFullName(member),
                    MemberPhone = member.PhoneNo ?? member.MobileNo ?? "N/A",
                    ReportDate = DateTime.Now,
                    ShareTypeGroups = groupedContributions,
                    TotalShareCapital = totalShareCapital,
                    TotalDeposits = totalDeposits,
                    TotalAmount = contributions.Sum(c => c.Amount ?? 0),
                    TotalTransactions = contributions.Count,
                    ActiveShareTypes = groupedContributions.Count,
                    HasData = groupedContributions.Any()
                };

                ViewBag.MemberName = viewModel.MemberName;
                ViewBag.MemberNo = viewModel.MemberNo;
                ViewBag.CompanyName = User.FindFirstValue("CompanyName") ?? "";

                return View("~/Views/MemberPortal/ShareContributionsStatement.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Share Contributions Statement");
                TempData["Error"] = "Error loading report: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: Export Share Contributions Statement to PDF
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportShareContributionsToPdf()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get all contributions
                var contributions = await _context.Contribs
                    .Where(c => c.MemberNo == memberNo
                        && c.CompanyCode == companyCode
                        && c.Amount > 0)
                    .OrderByDescending(c => c.ContrDate)
                    .ThenBy(c => c.Sharescode)
                    .ToListAsync();

                if (!contributions.Any())
                {
                    TempData["Error"] = "No share contributions found for this member.";
                    return RedirectToAction("ShareContributionsStatement");
                }

                // Get share types
                var shareTypes = await _context.Sharetypes
                    .Where(st => st.CompanyCode == companyCode)
                    .ToDictionaryAsync(st => st.SharesCode, st => st);

                // Group by Share Type
                var groupedData = contributions
                    .GroupBy(c => c.Sharescode)
                    .Where(g => shareTypes.ContainsKey(g.Key) && g.Any(x => x.Amount > 0))
                    .Select(g => new
                    {
                        ShareCode = g.Key,
                        ShareTypeName = shareTypes.ContainsKey(g.Key) ? shareTypes[g.Key].SharesType : g.Key,
                        IsShareCapital = shareTypes.ContainsKey(g.Key) && shareTypes[g.Key].Issharecapital,
                        Priority = shareTypes.ContainsKey(g.Key) ? shareTypes[g.Key].Priority : 999,
                        Contributions = g.Select(c => new
                        {
                            TransactionDate = c.ContrDate ?? c.AuditDateTime ?? c.AuditTime,
                            Amount = c.Amount ?? 0,
                            Description = c.Remarks ?? c.TransferDesc ?? "Share Contribution",
                            ReceiptNo = c.ReceiptNo,
                            TransactionNo = c.TransactionNo ?? c.TransNo,
                            ChequeNo = c.ChequeNo,
                            RefNo = c.RefNo,
                            Posted = c.Posted
                        }).OrderByDescending(x => x.TransactionDate).ToList(),
                        TotalAmount = g.Sum(c => c.Amount ?? 0),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(g => g.Priority)
                    .ThenBy(g => g.ShareTypeName)
                    .ToList();

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
                            header.Item().AlignCenter().Text("SHARE CONTRIBUTIONS STATEMENT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Member: {GetFullName(member)} ({member.MemberNo})").FontSize(10);
                            header.Item().AlignCenter().Text($"Phone: {member.PhoneNo ?? member.MobileNo ?? "N/A"}").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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
                                    cols.RelativeColumn(1);
                                });

                                var totalShareCapital = groupedData
                                    .Where(g => g.IsShareCapital)
                                    .Sum(g => g.TotalAmount);

                                var totalDeposits = groupedData
                                    .Where(g => !g.IsShareCapital)
                                    .Sum(g => g.TotalAmount);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Share Capital:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalShareCapital:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Deposits/Savings:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalDeposits:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{groupedData.Sum(g => g.TotalAmount):N0}").FontSize(8);
                            });

                            // Per Share Type Tables
                            foreach (var group in groupedData)
                            {
                                contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Group Header with color based on type
                                string headerBg = group.IsShareCapital ? "#28a745" : "#17a2b8";
                                string headerText = group.IsShareCapital ? "SHARE CAPITAL" : "DEPOSITS/SAVINGS";

                                contentCol.Item().Table(groupHeaderTable =>
                                {
                                    groupHeaderTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(4);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    groupHeaderTable.Cell().ColumnSpan(3).Border(0.2f).Background(headerBg).Padding(4)
                                        .Text($"{group.ShareTypeName} ({group.ShareCode})")
                                        .FontColor(Colors.White).Bold().FontSize(10);

                                    groupHeaderTable.Cell().Border(0.2f).Background(headerBg).Padding(4).AlignRight()
                                        .Text($"Transactions: {group.TransactionCount}")
                                        .FontColor(Colors.White).FontSize(8);

                                    groupHeaderTable.Cell().Border(0.2f).Background(headerBg).Padding(4).AlignRight()
                                        .Text($"Total: {group.TotalAmount:N0}")
                                        .FontColor(Colors.White).Bold().FontSize(9);
                                });

                                // Contributions Table for this Share Type - REMOVED Balance and Status
                                contentCol.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.9f);   // Date
                                        cols.RelativeColumn(0.9f);   // Amount
                                        cols.RelativeColumn(1.8f);   // Description
                                        cols.RelativeColumn(0.9f);   // Receipt No
                                        cols.RelativeColumn(0.9f);   // Ref No
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Description").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Receipt No").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Ref No").Bold().FontSize(7);
                                    });

                                    foreach (var item in group.Contributions)
                                    {
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.TransactionDate.ToString("dd/MM/yyyy")).FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Amount:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).Text(item.Description ?? "").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).Text(item.ReceiptNo ?? "-").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).Text(item.RefNo ?? "-").FontSize(7);
                                    }

                                    // Subtotal row
                                    table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("SUBTOTAL:").Bold().FontSize(8);
                                    table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalAmount:N0}").Bold().FontSize(8);
                                    table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4);
                                });
                            }

                            // Grand Total
                            if (groupedData.Count > 1)
                            {
                                contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                                contentCol.Item().Table(totalTable =>
                                {
                                    totalTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(2);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    totalTable.Cell().ColumnSpan(5).Border(0.2f).Background("#d0d0d0").Padding(4)
                                        .Text("GRAND TOTAL").Bold().FontSize(10).AlignCenter();

                                    var totalShareCapital = groupedData
                                        .Where(g => g.IsShareCapital)
                                        .Sum(g => g.TotalAmount);

                                    var totalDeposits = groupedData
                                        .Where(g => !g.IsShareCapital)
                                        .Sum(g => g.TotalAmount);

                                    totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"Share Capital: {totalShareCapital:N0}").Bold().FontSize(9);
                                    totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"Deposits: {totalDeposits:N0}").Bold().FontSize(9);
                                    totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"Total: {groupedData.Sum(g => g.TotalAmount):N0}").Bold().FontSize(9);
                                    totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4);
                                });
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

                return File(stream.ToArray(), "application/pdf", $"ShareContributions_{memberNo}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Share Contributions to PDF");
                TempData["Error"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("ShareContributionsStatement");
            }
        }

        #endregion

        #region Report 2: Member Guarantor Loans Report

        /// <summary>
        /// GET: Displays the member's guarantor loans (loans they have guaranteed)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GuarantorLoansReport()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get ALL guarantor records for this member
                var guarantorRecords = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo
                        && g.CompanyCode == companyCode
                        && g.Transfered == false)
                    .ToListAsync();

                var guaranteedLoans = new List<MemberGuarantorLoanDetailDto>();

                foreach (var guarantor in guarantorRecords)
                {
                    // Get the loan that was guaranteed
                    var loan = await _context.Loans
                        .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo && l.CompanyCode == companyCode);

                    if (loan == null) continue;

                    // Get the loanee (borrower) details
                    var loanee = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);

                    // Get loan type
                    var loanType = await _context.Loantypes
                        .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);

                    // Get loan balance
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                    decimal outstandingBalance = loanbal?.Balance ?? 0;
                    decimal outstandingInterest = loanbal?.IntrOwed ?? 0;
                    decimal outstandingPenalty = loanbal?.Penalty ?? 0;
                    decimal totalOutstanding = outstandingBalance + outstandingInterest + outstandingPenalty;

                    bool isActive = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;

                    bool isOverdue = false;
                    int daysOverdue = 0;
                    if (loanbal?.Nextduedate != null && loanbal.Nextduedate < DateTime.Now && outstandingBalance > 0)
                    {
                        isOverdue = true;
                        daysOverdue = (DateTime.Now - loanbal.Nextduedate.Value).Days;
                    }

                    // Calculate expected completion date
                    DateTime? expectedCompletionDate = null;
                    if (loanbal?.FirstDate != null && loan.RepayPeriod.HasValue)
                    {
                        expectedCompletionDate = loanbal.FirstDate.AddMonths(loan.RepayPeriod.Value);
                    }
                    else if (loan.ApplicDate != null && loan.RepayPeriod.HasValue)
                    {
                        expectedCompletionDate = loan.ApplicDate.AddMonths(loan.RepayPeriod.Value);
                    }

                    guaranteedLoans.Add(new MemberGuarantorLoanDetailDto
                    {
                        LoanNo = loan.LoanNo,
                        LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                        LoaneeMemberNo = loan.MemberNo,
                        LoaneeName = loanee != null ? GetFullName(loanee) : loan.MemberNo,
                        LoaneePhone = loanee?.PhoneNo ?? loanee?.MobileNo ?? "N/A",
                        PrincipalAmount = loan.LoanAmt ?? 0,
                        OutstandingBalance = totalOutstanding,
                        GuaranteeAmount = guarantor.Amount ?? 0,
                        RemainingGuarantee = guarantor.Balance ?? 0,
                        ApplicationDate = loan.ApplicDate,
                        ExpectedCompletionDate = expectedCompletionDate,
                        IsActive = isActive,
                        IsOverdue = isOverdue,
                        DaysOverdue = daysOverdue,
                        LoanStatus = GetLoanStatusString(loan.Status),
                        GuaranteeDate = guarantor.AuditTime ?? DateTime.Now,
                        GuaranteeStatus = guarantor.Transfered ? "Released" : "Active"
                    });
                }

                var viewModel = new MemberGuarantorLoansViewModel
                {
                    MemberNo = member.MemberNo,
                    MemberName = GetFullName(member),
                    MemberPhone = member.PhoneNo ?? member.MobileNo ?? "N/A",
                    TotalGuaranteedLoans = guaranteedLoans.Count,
                    ActiveGuarantees = guaranteedLoans.Count(g => g.IsActive),
                    OverdueGuarantees = guaranteedLoans.Count(g => g.IsOverdue),
                    TotalGuaranteeAmount = guaranteedLoans.Sum(g => g.GuaranteeAmount),
                    TotalRemainingGuarantee = guaranteedLoans.Sum(g => g.RemainingGuarantee),
                    GuaranteedLoans = guaranteedLoans.OrderByDescending(g => g.ApplicationDate).ToList(),
                    ReportDate = DateTime.Now,
                    HasData = guaranteedLoans.Any()
                };

                ViewBag.MemberName = viewModel.MemberName;
                ViewBag.MemberNo = viewModel.MemberNo;
                ViewBag.CompanyName = User.FindFirstValue("CompanyName") ?? "";

                return View("~/Views/MemberPortal/GuarantorLoansReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Guarantor Loans Report");
                TempData["Error"] = "Error loading report: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: Export Guarantor Loans to PDF
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportGuarantorLoansReportToPdf()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var guarantorRecords = await _context.Loanguar
                    .Where(g => g.MemberNo == memberNo
                        && g.CompanyCode == companyCode
                        && g.Transfered == false)
                    .ToListAsync();

                if (!guarantorRecords.Any())
                {
                    TempData["Error"] = "No guaranteed loans found for this member.";
                    return RedirectToAction("GuarantorLoansReport");
                }

                // Build report data (same as GET action)
                var guaranteedLoans = new List<MemberGuarantorLoanDetailDto>();

                foreach (var guarantor in guarantorRecords)
                {
                    var loan = await _context.Loans
                        .FirstOrDefaultAsync(l => l.LoanNo == guarantor.LoanNo && l.CompanyCode == companyCode);
                    if (loan == null) continue;

                    var loanee = await _context.Members
                        .FirstOrDefaultAsync(m => m.MemberNo == loan.MemberNo && m.CompanyCode == companyCode);
                    var loanType = await _context.Loantypes
                        .FirstOrDefaultAsync(lt => lt.LoanCode == loan.LoanCode && lt.CompanyCode == companyCode);
                    var loanbal = await _context.Loanbal
                        .FirstOrDefaultAsync(lb => lb.LoanNo == loan.LoanNo && lb.Companycode == companyCode);

                    decimal outstandingBalance = loanbal?.Balance ?? 0;
                    bool isActive = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;
                    bool isOverdue = false;
                    int daysOverdue = 0;
                    if (loanbal?.Nextduedate != null && loanbal.Nextduedate < DateTime.Now && outstandingBalance > 0)
                    {
                        isOverdue = true;
                        daysOverdue = (DateTime.Now - loanbal.Nextduedate.Value).Days;
                    }

                    DateTime? expectedCompletionDate = null;
                    if (loanbal?.FirstDate != null && loan.RepayPeriod.HasValue)
                        expectedCompletionDate = loanbal.FirstDate.AddMonths(loan.RepayPeriod.Value);
                    else if (loan.ApplicDate != null && loan.RepayPeriod.HasValue)
                        expectedCompletionDate = loan.ApplicDate.AddMonths(loan.RepayPeriod.Value);

                    guaranteedLoans.Add(new MemberGuarantorLoanDetailDto
                    {
                        LoanNo = loan.LoanNo,
                        LoanType = loanType?.LoanType1 ?? loan.LoanCode ?? "Unknown",
                        LoaneeMemberNo = loan.MemberNo,
                        LoaneeName = loanee != null ? GetFullName(loanee) : loan.MemberNo,
                        LoaneePhone = loanee?.PhoneNo ?? loanee?.MobileNo ?? "N/A",
                        PrincipalAmount = loan.LoanAmt ?? 0,
                        OutstandingBalance = outstandingBalance,
                        GuaranteeAmount = guarantor.Amount ?? 0,
                        RemainingGuarantee = guarantor.Balance ?? 0,
                        ApplicationDate = loan.ApplicDate,
                        ExpectedCompletionDate = expectedCompletionDate,
                        IsActive = isActive,
                        IsOverdue = isOverdue,
                        DaysOverdue = daysOverdue,
                        LoanStatus = GetLoanStatusString(loan.Status),
                        GuaranteeDate = guarantor.AuditTime ?? DateTime.Now
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
                            header.Item().AlignCenter().Text("GUARANTOR LOANS REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Member: {GetFullName(member)} ({member.MemberNo})").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Guarantees:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(guaranteedLoans.Count.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Active:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(guaranteedLoans.Count(g => g.IsActive).ToString()).FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Overdue:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(guaranteedLoans.Count(g => g.IsOverdue).ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Guarantee:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{guaranteedLoans.Sum(g => g.GuaranteeAmount):N0}").FontSize(8);
                            });

                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);

                            // Detailed Table
                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // Loan No
                                    cols.RelativeColumn(1.2f);   // Loan Type
                                    cols.RelativeColumn(1.2f);   // Loanee
                                    cols.RelativeColumn(0.8f);   // Principal
                                    cols.RelativeColumn(0.8f);   // Outstanding
                                    cols.RelativeColumn(0.8f);   // Guarantee Amt
                                    cols.RelativeColumn(0.8f);   // Remaining
                                    cols.RelativeColumn(0.8f);   // App Date
                                    cols.RelativeColumn(0.8f);   // Expected Completion
                                    cols.RelativeColumn(0.8f);   // Status
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Type").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loanee").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Principal").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Outstanding").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantee Amt").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Remaining").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("App Date").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Expected Completion").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(7);
                                });

                                foreach (var loan in guaranteedLoans.OrderByDescending(l => l.ApplicationDate))
                                {
                                    string statusText = loan.IsOverdue ? "OVERDUE" :
                                                        (loan.IsActive ? "ACTIVE" : loan.LoanStatus.ToUpper());
                                    var statusColor = loan.IsOverdue ? "#dc3545" :
                                                      (loan.IsActive ? "#28a745" : "#6c757d");

                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanType ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoaneeName ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.PrincipalAmount:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.OutstandingBalance:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.GuaranteeAmount:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.RemainingGuarantee:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicationDate?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ExpectedCompletionDate?.ToString("dd/MM/yyyy") ?? "N/A").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Background(QuestPDF.Infrastructure.Color.FromHex(statusColor))
                                        .Text(statusText)
                                        .FontColor(QuestPDF.Helpers.Colors.White)
                                        .FontSize(7);
                                }
                            });
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

                return File(stream.ToArray(), "application/pdf", $"GuarantorLoans_{memberNo}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Guarantor Loans to PDF");
                TempData["Error"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("GuarantorLoansReport");
            }
        }

        #endregion

        #region Report 3: Member Loans Report

        /// <summary>
        /// GET: Displays the member's own loans
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MemberLoansReport()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get all loans for this member
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                var loanNos = loans.Select(l => l.LoanNo).ToList();

                // Get loan types
                var loanCodes = loans.Select(l => l.LoanCode).Distinct().ToList();
                var loanTypes = await _context.Loantypes
                    .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                    .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

                // Get loan balances
                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

                // Get loan repayments count
                var repaymentCounts = await _context.Repay
                    .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new { LoanNo = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.LoanNo, g => g.Count);

                var loanDetails = new List<MemberLoanDetailDto>();

                foreach (var loan in loans)
                {
                    decimal balance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : 0;
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal penalty = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Penalty : 0;
                    int repaymentCount = repaymentCounts.ContainsKey(loan.LoanNo) ? repaymentCounts[loan.LoanNo] : 0;

                    string loanType = loanTypes.ContainsKey(loan.LoanCode) ? loanTypes[loan.LoanCode] : loan.LoanCode ?? "Unknown";

                    bool isActive = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;

                    DateTime? expectedCompletionDate = null;
                    if (loan.AuditDateTime.HasValue && loan.RepayPeriod.HasValue)
                    {
                        expectedCompletionDate = loan.AuditDateTime.Value.AddMonths(loan.RepayPeriod.Value);
                    }
                    else if
                        (loan.ApplicDate != null && loan.RepayPeriod.HasValue)
                        expectedCompletionDate = loan.ApplicDate.AddMonths(loan.RepayPeriod.Value);

                    loanDetails.Add(new MemberLoanDetailDto
                    {
                        LoanNo = loan.LoanNo,
                        LoanType = loanType,
                        PrincipalAmount = loan.LoanAmt ?? 0,
                        ApprovedAmount = loan.Aamount ?? 0,
                        OutstandingBalance = balance,
                        UnpaidInterest = unpaidInterest,
                        Penalty = penalty,
                        TotalOutstanding = balance + unpaidInterest + penalty,
                        ApplicationDate = loan.ApplicDate,
                        DisbursementDate = loan.AuditDateTime,
                        ExpectedCompletionDate = expectedCompletionDate,
                        RepaymentPeriod = loan.RepayPeriod ?? 0,
                        InterestRate = loan.Interest ?? 0,
                        Status = GetLoanStatusString(loan.Status),
                        IsActive = isActive,
                        RepaymentCount = repaymentCount,
                        IsFullyPaid = balance <= 0 && unpaidInterest <= 0 && penalty <= 0
                    });
                }

                var viewModel = new MemberLoansViewModel
                {
                    MemberNo = member.MemberNo,
                    MemberName = GetFullName(member),
                    MemberPhone = member.PhoneNo ?? member.MobileNo ?? "N/A",
                    TotalLoans = loanDetails.Count,
                    ActiveLoans = loanDetails.Count(l => l.IsActive),
                    ClosedLoans = loanDetails.Count(l => !l.IsActive && l.IsFullyPaid),
                    TotalLoanAmount = loanDetails.Sum(l => l.PrincipalAmount),
                    TotalOutstanding = loanDetails.Sum(l => l.TotalOutstanding),
                    Loans = loanDetails.OrderByDescending(l => l.ApplicationDate).ToList(),
                    ReportDate = DateTime.Now,
                    HasData = loanDetails.Any()
                };

                ViewBag.MemberName = viewModel.MemberName;
                ViewBag.MemberNo = viewModel.MemberNo;
                ViewBag.CompanyName = User.FindFirstValue("CompanyName") ?? "";

                return View("~/Views/MemberPortal/MemberLoansReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Member Loans Report");
                TempData["Error"] = "Error loading report: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: Export Member Loans to PDF
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportMemberLoansToPdf()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .OrderByDescending(l => l.ApplicDate)
                    .ToListAsync();

                if (!loans.Any())
                {
                    TempData["Error"] = "No loans found for this member.";
                    return RedirectToAction("MemberLoansReport");
                }

                var loanNos = loans.Select(l => l.LoanNo).ToList();
                var loanCodes = loans.Select(l => l.LoanCode).Distinct().ToList();
                var loanTypes = await _context.Loantypes
                    .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                    .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

                var loanBalances = await _context.Loanbal
                    .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                    .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

                var reportData = loans.Select(loan =>
                {
                    decimal balance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : 0;
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal penalty = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Penalty : 0;
                    string loanType = loanTypes.ContainsKey(loan.LoanCode) ? loanTypes[loan.LoanCode] : loan.LoanCode ?? "Unknown";

                    bool isActive = loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed;
                    string statusText = isActive ? "ACTIVE" : GetLoanStatusString(loan.Status);

                    return new
                    {
                        loan.LoanNo,
                        LoanType = loanType,
                        Principal = loan.LoanAmt ?? 0,
                        Approved = loan.Aamount ?? 0,
                        Balance = balance,
                        UnpaidInterest = unpaidInterest,
                        Penalty = penalty,
                        TotalOutstanding = balance + unpaidInterest + penalty,
                        ApplicationDate = loan.ApplicDate,
                        DisbursementDate = loan.AuditDateTime,
                        Status = statusText,
                        IsActive = isActive
                    };
                }).ToList();

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
                            header.Item().AlignCenter().Text("MEMBER LOANS REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Member: {GetFullName(member)} ({member.MemberNo})").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loans:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Active Loans:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count(l => l.IsActive).ToString()).FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Principal:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(l => l.Principal):N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Outstanding:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(l => l.TotalOutstanding):N0}").FontSize(8);
                            });

                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.0f);   // Loan No
                                    cols.RelativeColumn(1.2f);   // Loan Type
                                    cols.RelativeColumn(0.8f);   // Principal
                                    cols.RelativeColumn(0.8f);   // Approved
                                    cols.RelativeColumn(0.8f);   // Balance
                                    cols.RelativeColumn(0.8f);   // Unpaid Interest
                                    cols.RelativeColumn(0.8f);   // Penalty
                                    cols.RelativeColumn(0.8f);   // Total Outstanding
                                    cols.RelativeColumn(0.8f);   // App Date
                                    cols.RelativeColumn(0.8f);   // Disbursed
                                    cols.RelativeColumn(0.8f);   // Status
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Type").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Principal").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Approved").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Unpaid Int").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Penalty").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Outstanding").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("App Date").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Disbursed").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(7);
                                });

                                foreach (var loan in reportData)
                                {
                                    string statusBg = loan.IsActive ? "#28a745" : "#6c757d";

                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(loan.LoanType ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Principal:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Approved:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Balance:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.UnpaidInterest:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Penalty:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.TotalOutstanding:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicationDate.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DisbursementDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Background(statusBg)
                                        .Text(loan.Status)
                                        .FontColor(QuestPDF.Helpers.Colors.White)
                                        .FontSize(7);
                                }
                            });
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

                return File(stream.ToArray(), "application/pdf", $"MemberLoans_{memberNo}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Member Loans to PDF");
                TempData["Error"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("MemberLoansReport");
            }
        }

        #endregion

        #region Report 4: Loan Repayments Report

        /// <summary>
        /// GET: Displays the member's loan repayments for all loans
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LoanRepaymentsReport()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get all loans for this member
                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                // Get all repayments for all loans of this member
                var repayments = await _context.Repay
                    .Where(r => loans.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                    .OrderByDescending(r => r.DateReceived)
                    .ToListAsync();

                // Get loan types for display
                var loanNos = repayments.Select(r => r.LoanNo).Distinct().ToList();
                var loanTypes = await _context.Loantypes
                    .Where(lt => loanNos.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                    .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

                var groupedRepayments = repayments
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new LoanRepaymentGroupDto
                    {
                        LoanNo = g.Key,
                        LoanType = loanTypes.ContainsKey(g.First().Loancode) ? loanTypes[g.First().Loancode] : g.First().Loancode ?? "Unknown",
                        TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                        TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                        TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0),
                        TotalAmountPaid = g.Sum(r => r.Amount ?? 0),
                        RepaymentCount = g.Count(),
                        Repayments = g.Select(r => new LoanRepaymentDetailDto
                        {
                            TransactionDate = r.DateReceived,
                            Amount = r.Amount ?? 0,
                            Principal = r.Principal ?? 0,
                            Interest = r.Interest ?? 0,
                            Penalty = r.Penalty ?? 0,
                            LoanBalance = r.LoanBalance ?? 0,
                            ReceiptNo = r.ReceiptNo,
                            TransactionNo = r.TransactionNo,
                            Description = r.Remarks ?? "Loan Repayment"
                        }).OrderByDescending(r => r.TransactionDate).ToList()
                    })
                    .OrderByDescending(g => g.Repayments.FirstOrDefault()?.TransactionDate)
                    .ToList();

                var viewModel = new LoanRepaymentsViewModel
                {
                    MemberNo = member.MemberNo,
                    MemberName = GetFullName(member),
                    MemberPhone = member.PhoneNo ?? member.MobileNo ?? "N/A",
                    TotalLoans = groupedRepayments.Count,
                    TotalRepayments = repayments.Count,
                    TotalAmountPaid = repayments.Sum(r => r.Amount ?? 0),
                    TotalPrincipalPaid = repayments.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = repayments.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = repayments.Sum(r => r.Penalty ?? 0),
                    LoanGroups = groupedRepayments,
                    ReportDate = DateTime.Now,
                    HasData = repayments.Any()
                };

                ViewBag.MemberName = viewModel.MemberName;
                ViewBag.MemberNo = viewModel.MemberNo;
                ViewBag.CompanyName = User.FindFirstValue("CompanyName") ?? "";

                return View("~/Views/MemberPortal/LoanRepaymentsReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Loan Repayments Report");
                TempData["Error"] = "Error loading report: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: Export Loan Repayments to PDF
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportLoanRepaymentsToPdf()
        {
            try
            {
                var memberNo = GetCurrentMemberNo();
                var companyCode = GetCurrentCompanyCode();
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                if (string.IsNullOrEmpty(memberNo))
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var member = await GetCurrentMemberAsync();
                if (member == null)
                {
                    TempData["Error"] = "Member not found.";
                    return RedirectToAction("Login", "Account");
                }

                var loans = await _context.Loans
                    .Where(l => l.MemberNo == memberNo && l.CompanyCode == companyCode)
                    .Select(l => l.LoanNo)
                    .ToListAsync();

                var repayments = await _context.Repay
                    .Where(r => loans.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                    .OrderByDescending(r => r.DateReceived)
                    .ToListAsync();

                if (!repayments.Any())
                {
                    TempData["Error"] = "No repayments found for this member.";
                    return RedirectToAction("LoanRepaymentsReport");
                }

                // Build report data grouped by loan
                var loanNos = repayments.Select(r => r.LoanNo).Distinct().ToList();
                var loanTypes = await _context.Loantypes
                    .Where(lt => loanNos.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                    .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

                var groupedData = repayments
                    .GroupBy(r => r.LoanNo)
                    .Select(g => new
                    {
                        LoanNo = g.Key,
                        LoanType = loanTypes.ContainsKey(g.First().Loancode) ? loanTypes[g.First().Loancode] : g.First().Loancode ?? "Unknown",
                        TotalAmount = g.Sum(r => r.Amount ?? 0),
                        Repayments = g.Select(r => new
                        {
                            r.DateReceived,
                            r.Amount,
                            r.Principal,
                            r.Interest,
                            r.Penalty,
                            r.LoanBalance,
                            r.ReceiptNo,
                            r.TransactionNo,
                            Description = r.Remarks ?? "Loan Repayment"
                        }).OrderByDescending(r => r.DateReceived).ToList()
                    })
                    .OrderByDescending(g => g.Repayments.FirstOrDefault()?.DateReceived)
                    .ToList();

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
                            header.Item().AlignCenter().Text("LOAN REPAYMENTS REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Member: {GetFullName(member)} ({member.MemberNo})").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Repayments:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(repayments.Count.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayments.Sum(r => r.Amount ?? 0):N0}").FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Principal:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayments.Sum(r => r.Principal ?? 0):N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Interest:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayments.Sum(r => r.Interest ?? 0):N0}").FontSize(8);
                            });

                            // Per Loan Repayment Tables
                            foreach (var group in groupedData)
                            {
                                contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                                contentCol.Item().Text($"Loan: {group.LoanNo} - {group.LoanType} (Total: {group.TotalAmount:N0})").FontSize(10).Bold();

                                contentCol.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.8f);   // Date
                                        cols.RelativeColumn(0.8f);   // Amount
                                        cols.RelativeColumn(0.8f);   // Principal
                                        cols.RelativeColumn(0.8f);   // Interest
                                        cols.RelativeColumn(0.8f);   // Penalty
                                        cols.RelativeColumn(0.8f);   // Balance
                                        cols.RelativeColumn(0.8f);   // Receipt No
                                        cols.RelativeColumn(1.2f);   // Description
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Principal").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Interest").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Penalty").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Receipt No").Bold().FontSize(7);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Description").Bold().FontSize(7);
                                    });

                                    foreach (var repayment in group.Repayments)
                                    {
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(repayment.DateReceived?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Amount:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Principal:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Interest:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Penalty:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.LoanBalance:N0}").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).Text(repayment.ReceiptNo ?? "-").FontSize(7);
                                        table.Cell().Border(0.2f).Padding(4).Text(repayment.Description ?? "").FontSize(7);
                                    }
                                });
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

                return File(stream.ToArray(), "application/pdf", $"LoanRepayments_{memberNo}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Loan Repayments to PDF");
                TempData["Error"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("LoanRepaymentsReport");
            }
        }

        #endregion

        #region Combined Member Dashboard Report (Original)

        /// <summary>
        /// Helper method to get member shares and loans data
        /// </summary>
        private async Task<SharesAndLoansIndexViewModel> GetMemberSharesAndLoansData(string memberNo, string companyCode)
        {
            var members = await _context.Members
                .Where(m => m.CompanyCode == companyCode && m.MemberNo == memberNo
                    && (m.Withdrawn == false || m.Withdrawn == null)
                    && (m.Archived == false || m.Archived == null))
                .OrderBy(m => m.MemberNo)
                .ToListAsync();

            var memberNos = members.Select(m => m.MemberNo).ToList();

            // Get SHARE CAPITAL from Shares table
            var shares = await _context.Shares
                .Where(s => memberNos.Contains(s.MemberNo) && s.CompanyCode == companyCode)
                .GroupBy(s => s.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalShareCapital = g.Sum(s => s.TotalShares ?? 0)
                })
                .ToDictionaryAsync(s => s.MemberNo, s => s.TotalShareCapital);

            // Get DEPOSITS/SAVINGS from ContribShares table
            var savings = await _context.ContribShares
                .Where(c => memberNos.Contains(c.MemberNo) && c.CompanyCode == companyCode)
                .GroupBy(c => c.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalCapital = g.Sum(c => c.ShareCapitalAmount ?? 0),
                    TotalDeposits = g.Sum(c => c.DepositsAmount ?? 0)
                })
                .ToDictionaryAsync(c => c.MemberNo, c => c.TotalCapital + c.TotalDeposits);

            // Get REGISTRATION FEE
            var regFees = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalRegFee = g.Sum(cs => cs.RegFeeAmount ?? 0)
                })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalRegFee);

            // Get LOANS
            var loans = await _context.Loans
                .Where(l => memberNos.Contains(l.MemberNo)
                    && l.CompanyCode == companyCode
                    && l.Status == (int)Status.Disbursed)
                .GroupBy(l => l.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalLoans = g.Sum(l => l.LoanAmt ?? 0)
                })
                .ToDictionaryAsync(l => l.MemberNo, l => l.TotalLoans);

            // Get PASSBOOK amount
            var passbook = await _context.ContribShares
                .Where(cs => memberNos.Contains(cs.MemberNo) && cs.CompanyCode == companyCode)
                .GroupBy(cs => cs.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    TotalPassbook = g.Sum(cs => cs.PassBookAmount ?? 0)
                })
                .ToDictionaryAsync(cs => cs.MemberNo, cs => cs.TotalPassbook);

            var reportData = new List<SharesAndLoansReportViewModel>();
            int maleCount = 0, femaleCount = 0, otherCount = 0, youthCount = 0;
            decimal totalShareCapital = 0, totalDeposits = 0, totalRegFee = 0, totalPassbook = 0, totalLoans = 0;

            foreach (var member in members)
            {
                // Calculate age
                int? age = null;
                if (member.Dob.HasValue)
                {
                    age = DateTime.Now.Year - member.Dob.Value.Year;
                    if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
                    if (age >= 18 && age <= 35) youthCount++;
                }

                // Build full name
                string fullName = "N/A";
                if (!string.IsNullOrWhiteSpace(member.Surname) || !string.IsNullOrWhiteSpace(member.OtherNames))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                // Count gender
                if (member.Sex?.ToUpper() == "MALE" || member.Sex?.ToUpper() == "M")
                    maleCount++;
                else if (member.Sex?.ToUpper() == "FEMALE" || member.Sex?.ToUpper() == "F")
                    femaleCount++;
                else
                    otherCount++;

                // Get financial values
                decimal shareCapital = shares.ContainsKey(member.MemberNo) ? shares[member.MemberNo] : (member.ShareCap ?? 0);
                decimal deposits = savings.ContainsKey(member.MemberNo) ? savings[member.MemberNo] : 0;
                decimal regFee = regFees.ContainsKey(member.MemberNo) ? regFees[member.MemberNo] : (member.RegFee ?? 0);
                decimal passbookAmount = passbook.ContainsKey(member.MemberNo) ? passbook[member.MemberNo] : 0;
                decimal loanAmount = loans.ContainsKey(member.MemberNo) ? loans[member.MemberNo] : 0;

                totalShareCapital += shareCapital;
                totalDeposits += deposits;
                totalRegFee += regFee;
                totalPassbook += passbookAmount;
                totalLoans += loanAmount;

                reportData.Add(new SharesAndLoansReportViewModel
                {
                    MemberNo = member.MemberNo,
                    FullName = fullName,
                    Age = age,
                    CIGName = "N/A",
                    ShareCapital = shareCapital,
                    Deposits = deposits,
                    RegFee = regFee,
                    Passbook = passbookAmount,
                    TotalLoans = loanAmount,
                    DateRegistered = member.ApplicDate,
                    Sex = member.Sex ?? "Not Specified"
                });
            }

            return new SharesAndLoansIndexViewModel
            {
                Members = reportData.OrderBy(m => m.MemberNo).ToList(),
                TotalMembers = reportData.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                OtherCount = otherCount,
                YouthCount = youthCount,
                TotalShareCapital = totalShareCapital,
                TotalDeposits = totalDeposits,
                TotalRegFee = totalRegFee,
                TotalPassbook = totalPassbook,
                TotalLoans = totalLoans,
                ReportDate = DateTime.Now,
                HasData = reportData.Any(),
                UserCompanyCode = companyCode,
                CompanyName = User.FindFirstValue("CompanyName") ?? ""
            };
        }

        #endregion
    }
}

