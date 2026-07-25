using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Models.ViewModels;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class CountyReportController : Controller
    {
        private readonly ICountyReportService _countyReportService;
        private readonly ILogger<CountyReportController> _logger;

        public CountyReportController(ICountyReportService countyReportService, ILogger<CountyReportController> logger)
        {
            _countyReportService = countyReportService;
            _logger = logger;
        }

        // ============================================================
        // COUNTY CONTRIBUTION REPORT - AUTO-DETECT USER'S COUNTY
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> CountyContribReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Get the logged-in user's company code
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found. Please contact administrator.";
                    return RedirectToAction("Index", "Home");
                }

                // Get the user's company details
                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found for your company. Please contact administrator.";
                    return RedirectToAction("Index", "Home");
                }

                // Set default date range (last 12 months)
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddMonths(-12);
                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                // Get the county name
                var countyName = userCompany.County;

                // Get the report data for this county
                var report = await _countyReportService.GetCountyReportAsync(countyName, startDate, endDate);

                // Set additional properties for the view
                report.SelectedCompanyName = $"County: {countyName}";
                report.IsCountyView = true;
                report.CountyName = countyName;

                // Store the county name in ViewBag for the view
                ViewBag.CountyName = countyName;
                ViewBag.ReportDate = report.ReportDate;
                ViewBag.HasData = report.CompanySummaries.Any();

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating county report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CountyContribReport(DateTime reportDate)
        {
            try
            {
                // Get the logged-in user's company code
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Get the user's company details
                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found for your company.";
                    return RedirectToAction("Index", "Home");
                }

                // Set date range (12 months before the selected date)
                var endDate = reportDate;
                var startDate = reportDate.AddMonths(-12);

                var countyName = userCompany.County;

                var report = await _countyReportService.GetCountyReportAsync(countyName, startDate, endDate);

                report.SelectedCompanyName = $"County: {countyName}";
                report.IsCountyView = true;
                report.CountyName = countyName;

                ViewBag.CountyName = countyName;
                ViewBag.ReportDate = report.ReportDate;
                ViewBag.HasData = report.CompanySummaries.Any();

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating county report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return View("Error");
            }
        }

        // ============================================================
        // EXPORT TO EXCEL - FIXED
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> ExportCountyReportToExcel(DateTime reportDate)
        {
            try
            {
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("CountyContribReport");
                }

                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found.";
                    return RedirectToAction("CountyContribReport");
                }

                var endDate = reportDate;
                var startDate = reportDate.AddMonths(-12);

                var report = await _countyReportService.GetCountyReportAsync(userCompany.County, startDate, endDate);

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("County Report");

                int r = 1;

                // Title
                ws.Cell(r, 1).Value = $"{userCompany.County.ToUpper()} COUNTY - CONTRIBUTION REPORT";
                ws.Range(r, 1, r, 12).Merge().Style.Font.SetBold().Font.SetFontSize(16)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                r += 2;

                ws.Cell(r, 1).Value = $"As At: {reportDate:dd/MM/yyyy}";
                ws.Range(r, 1, r, 4).Merge().Style.Font.SetBold();
                ws.Cell(r, 6).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                ws.Range(r, 6, r, 9).Merge().Style.Font.SetBold();
                r += 2;

                // Summary Statistics
                ws.Cell(r, 1).Value = "SUMMARY STATISTICS";
                ws.Range(r, 1, r, 12).Merge().Style.Font.SetBold().Font.SetFontSize(12);
                r++;

                ws.Cell(r, 1).Value = "Total Companies:";
                ws.Cell(r, 2).Value = report.TotalCompanies;
                ws.Cell(r, 3).Value = "Total Members:";
                ws.Cell(r, 4).Value = report.TotalMembers;
                ws.Cell(r, 5).Value = "Total Contributions:";
                ws.Cell(r, 6).Value = report.TotalContributions;
                ws.Range(r, 5, r, 6).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 7).Value = "Avg Per Member:";
                ws.Cell(r, 8).Value = report.TotalMembers > 0 ? report.TotalContributions / report.TotalMembers : 0;
                ws.Range(r, 7, r, 8).Style.NumberFormat.Format = "#,##0.00";
                r += 2;

                // Gender Summary
                ws.Cell(r, 1).Value = "GENDER DISTRIBUTION";
                ws.Range(r, 1, r, 12).Merge().Style.Font.SetBold().Font.SetFontSize(12);
                r++;

                ws.Cell(r, 1).Value = "Women:";
                ws.Cell(r, 2).Value = report.GenderSummary.FemaleCount;
                ws.Cell(r, 3).Value = report.GenderSummary.FemaleTotal;
                ws.Range(r, 3, r, 3).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 4).Value = report.GenderSummary.FemalePercentage;  // Already a percentage
                ws.Range(r, 4, r, 4).Style.NumberFormat.Format = "0.00";  // FIX: Use "0.00" not "0.00%"

                ws.Cell(r, 5).Value = "Men:";
                ws.Cell(r, 6).Value = report.GenderSummary.MaleCount;
                ws.Cell(r, 7).Value = report.GenderSummary.MaleTotal;
                ws.Range(r, 7, r, 7).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 8).Value = report.GenderSummary.MalePercentage;  // Already a percentage
                ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";  // FIX: Use "0.00" not "0.00%"

                ws.Cell(r, 9).Value = "Others:";
                ws.Cell(r, 10).Value = report.GenderSummary.OtherCount;
                ws.Cell(r, 11).Value = report.GenderSummary.OtherPercentage;  // Already a percentage
                ws.Range(r, 11, r, 11).Style.NumberFormat.Format = "0.00";  // FIX: Use "0.00" not "0.00%"
                r += 2;

                // Headers
                string[] headers = { "#", "Company Name", "Members", "Share Capital", "Deposits", "Reg Fees",
                             "Total", "% of Total", "Avg/Member", "Women", "Men", "Others" };

                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(r, i + 1).Value = headers[i];
                    ws.Cell(r, i + 1).Style.Font.SetBold();
                    ws.Cell(r, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                r++;

                int counter = 1;
                foreach (var company in report.CompanySummaries)
                {
                    ws.Cell(r, 1).Value = counter++;
                    ws.Cell(r, 2).Value = company.CompanyName;
                    ws.Cell(r, 3).Value = company.MemberCount;
                    ws.Cell(r, 4).Value = company.TotalShareCapital;
                    ws.Cell(r, 5).Value = company.TotalDeposits;
                    ws.Cell(r, 6).Value = company.TotalRegistrationFees;
                    ws.Cell(r, 7).Value = company.TotalContributions;

                    // FIX: PercentageOfTotal is already a percentage (e.g., 25.5 = 25.5%)
                    ws.Cell(r, 8).Value = company.PercentageOfTotal;
                    ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";  // FIX: Use "0.00" not "0.00%"

                    ws.Cell(r, 9).Value = company.AverageContributionPerMember;
                    ws.Range(r, 9, r, 9).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(r, 10).Value = company.FemaleCount;
                    ws.Cell(r, 11).Value = company.MaleCount;
                    ws.Cell(r, 12).Value = company.OtherCount;

                    ws.Range(r, 4, r, 7).Style.NumberFormat.Format = "#,##0.00";
                    r++;
                }

                // Totals
                ws.Cell(r, 2).Value = "TOTALS";
                ws.Range(r, 2, r, 2).Style.Font.SetBold();
                ws.Cell(r, 3).Value = report.TotalMembers;
                ws.Cell(r, 4).Value = report.TotalShareCapital;
                ws.Cell(r, 5).Value = report.TotalDeposits;
                ws.Cell(r, 6).Value = report.TotalRegistrationFees;
                ws.Cell(r, 7).Value = report.TotalContributions;
                ws.Cell(r, 8).Value = 100.00m;  // 100% as a number
                ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";  // FIX: Use "0.00" not "0.00%"
                ws.Cell(r, 9).Value = report.TotalMembers > 0 ? report.TotalContributions / report.TotalMembers : 0;
                ws.Range(r, 9, r, 9).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 10).Value = report.GenderSummary.FemaleCount;
                ws.Cell(r, 11).Value = report.GenderSummary.MaleCount;
                ws.Cell(r, 12).Value = report.GenderSummary.OtherCount;

                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"CountyReport_{userCompany.County}_{reportDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting county report to Excel");
                TempData["ErrorMessage"] = "Error exporting report";
                return RedirectToAction("CountyContribReport");
            }
        }

        // ============================================================
        // EXPORT TO PDF - UPDATED WITH LOANS REPORT STYLE
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> ExportCountyReportToPdf(DateTime reportDate)
        {
            try
            {
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("CountyContribReport");
                }

                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found.";
                    return RedirectToAction("CountyContribReport");
                }

                var endDate = reportDate;
                var startDate = reportDate.AddMonths(-12);

                var report = await _countyReportService.GetCountyReportAsync(userCompany.County, startDate, endDate);

                using var stream = new MemoryStream();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                        // Header
                        page.Header()
                            .AlignCenter()
                            .Column(column =>
                            {
                                column.Item().Text($"{userCompany.County.ToUpper()} COUNTY").FontSize(18).Bold();
                                column.Item().Text($"CONTRIBUTION REPORT AS AT {reportDate:dd/MM/yyyy}").FontSize(14).Bold();
                                column.Item().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                                column.Item().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(9).Italic();
                                column.Item().PaddingTop(0.5f, Unit.Centimetre).LineHorizontal(0.5f);
                            });

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                // Summary Statistics with Border
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

                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Companies: {report.TotalCompanies}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Members: {report.TotalMembers}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Total: {report.TotalContributions:N0}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Avg: {(report.TotalMembers > 0 ? (report.TotalContributions / report.TotalMembers).ToString("N0") : "0")}");
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Women: {report.GenderSummary.FemaleCount}");
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Men: {report.GenderSummary.MaleCount}");
                                });

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Gender Summary with Borders
                                column.Item().Table(genderTable =>
                                {
                                    genderTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    genderTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("Women:").Bold();
                                    genderTable.Cell().Border(0.2f).Padding(4).Text($"{report.GenderSummary.FemaleCount} members");
                                    genderTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{report.GenderSummary.FemaleTotal:N0}");
                                    genderTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text("Men:").Bold();
                                    genderTable.Cell().Border(0.2f).Padding(4).Text($"{report.GenderSummary.MaleCount} members");
                                    genderTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{report.GenderSummary.MaleTotal:N0}");
                                });

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Main Data Table with Borders
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.5f);
                                        cols.RelativeColumn(2.2f);
                                        cols.RelativeColumn(0.8f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(0.8f);
                                        cols.RelativeColumn(0.8f);
                                        cols.RelativeColumn(0.8f);
                                    });

                                    // Header
                                    table.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Company Name").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Members").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Share").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Deposits").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reg Fees").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("%").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Avg/Mem").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("W").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("M").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("O").Bold().FontSize(8);
                                    });

                                    int counter = 1;
                                    foreach (var company in report.CompanySummaries)
                                    {
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(counter++.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).Text(company.CompanyName).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(company.MemberCount.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.TotalShareCapital:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.TotalDeposits:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.TotalRegistrationFees:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.TotalContributions:N0}").FontSize(8);

                                        // Percentage - color code based on value
                                        var pctColor = company.PercentageOfTotal > 20 ? Colors.Green.Darken1 :
                                                       company.PercentageOfTotal > 10 ? Colors.Orange.Darken1 :
                                                       Colors.Grey.Darken1;
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.PercentageOfTotal:F1}%").FontColor(pctColor).FontSize(8);

                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{company.AverageContributionPerMember:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(company.FemaleCount.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(company.MaleCount.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(company.OtherCount.ToString()).FontSize(8);
                                    }

                                    // Footer Statistics - Totals Row with background
                                    table.Cell().ColumnSpan(2)
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .Text("TOTALS")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignCenter()
                                        .Text(report.TotalMembers.ToString())
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text($"{report.TotalShareCapital:N0}")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text($"{report.TotalDeposits:N0}")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text($"{report.TotalRegistrationFees:N0}")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text($"{report.TotalContributions:N0}")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text("100%")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignRight()
                                        .Text($"{(report.TotalMembers > 0 ? (report.TotalContributions / report.TotalMembers).ToString("N0") : "0")}")
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignCenter()
                                        .Text(report.GenderSummary.FemaleCount.ToString())
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignCenter()
                                        .Text(report.GenderSummary.MaleCount.ToString())
                                        .FontSize(10)
                                        .Bold();

                                    table.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(4)
                                        .AlignCenter()
                                        .Text(report.GenderSummary.OtherCount.ToString())
                                        .FontSize(10)
                                        .Bold();
                                });

                                // Footer Statistics - Additional Info
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Table(footerTable =>
                                {
                                    footerTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    var totalWomenPct = report.TotalContributions > 0 ? (report.GenderSummary.FemaleTotal / report.TotalContributions) * 100 : 0;
                                    var totalMenPct = report.TotalContributions > 0 ? (report.GenderSummary.MaleTotal / report.TotalContributions) * 100 : 0;

                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Women Contribution: {report.GenderSummary.FemaleTotal:N0} ({totalWomenPct:F1}%)").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Men Contribution: {report.GenderSummary.MaleTotal:N0} ({totalMenPct:F1}%)").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total Members: {report.TotalMembers}").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total Companies: {report.TotalCompanies}").Bold().FontSize(9);
                                });
                            });

                        // Footer
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

                return File(stream.ToArray(), "application/pdf", $"CountyReport_{userCompany.County}_{reportDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting county report to PDF");
                TempData["ErrorMessage"] = "Error exporting report";
                return RedirectToAction("CountyContribReport");
            }
        }

        
        // ============================================================
        // COUNTY LOANS REPORT - AUTO-DETECT USER'S COUNTY
        // ============================================================

        [HttpGet("CountyLoansReport")]
        public async Task<IActionResult> CountyLoansReport(DateTime? asAtDate)
        {
            try
            {
                // Get the logged-in user's company code
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found. Please contact administrator.";
                    return RedirectToAction("Index", "Home");
                }

                // Get the user's company details
                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found for your company. Please contact administrator.";
                    return RedirectToAction("Index", "Home");
                }

                if (!asAtDate.HasValue)
                    asAtDate = DateTime.Now.Date;

                var countyName = userCompany.County;

                // Get the report data for this county
                var report = await _countyReportService.GetCountyLoansReportAsync(countyName, asAtDate);

                // Set additional properties for the view
                report.SelectedCompanyName = $"County: {countyName}";
                report.IsCountyView = true;
                report.CountyName = countyName;

                ViewBag.CountyName = countyName;
                ViewBag.AsAtDate = asAtDate;
                ViewBag.HasData = report.SaccoSummaries.Any();

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating county loans report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return View("Error");
            }
        }

        [HttpPost("CountyLoansReport")]
        public async Task<IActionResult> CountyLoansReport(DateTime asAtDate)
        {
            try
            {
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("Index", "Home");
                }

                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found for your company.";
                    return RedirectToAction("Index", "Home");
                }

                var countyName = userCompany.County;

                var report = await _countyReportService.GetCountyLoansReportAsync(countyName, asAtDate);

                report.SelectedCompanyName = $"County: {countyName}";
                report.IsCountyView = true;
                report.CountyName = countyName;

                ViewBag.CountyName = countyName;
                ViewBag.AsAtDate = asAtDate;
                ViewBag.HasData = report.SaccoSummaries.Any();

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating county loans report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return View("Error");
            }
        }

        // ============================================================
        // EXPORT COUNTY LOANS TO EXCEL - FIXED
        // ============================================================

        [HttpPost("ExportCountyLoansToExcel")]
        public async Task<IActionResult> ExportCountyLoansToExcel(DateTime asAtDate)
        {
            try
            {
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("CountyLoansReport");
                }

                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found.";
                    return RedirectToAction("CountyLoansReport");
                }

                var report = await _countyReportService.GetCountyLoansReportAsync(userCompany.County, asAtDate);

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("County Loans Report");
                int r = 1;

                // Title
                ws.Cell(r, 1).Value = $"{userCompany.County.ToUpper()} COUNTY - LOANS REPORT";
                ws.Range(r, 1, r, 11).Merge().Style.Font.SetBold().Font.SetFontSize(16)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                r += 2;

                ws.Cell(r, 1).Value = $"As At: {asAtDate:dd/MM/yyyy}";
                ws.Range(r, 1, r, 3).Merge().Style.Font.SetBold();
                ws.Cell(r, 5).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                ws.Range(r, 5, r, 7).Merge().Style.Font.SetBold();
                r += 2;

                // Summary
                ws.Cell(r, 1).Value = "SUMMARY STATISTICS";
                ws.Range(r, 1, r, 11).Merge().Style.Font.SetBold().Font.SetFontSize(12);
                r++;

                ws.Cell(r, 1).Value = "Total SACCOs:";
                ws.Cell(r, 2).Value = report.TotalCompanies;
                ws.Cell(r, 3).Value = "Total Loans:";
                ws.Cell(r, 4).Value = report.TotalLoans;
                ws.Cell(r, 5).Value = "Total Balance:";
                ws.Cell(r, 6).Value = report.TotalLoanBalance;
                ws.Range(r, 5, r, 6).Style.NumberFormat.Format = "#,##0.00";

                // FIX: PAR values are already in percentage (e.g., 5.5 means 5.5%)
                // Use "0.00" format instead of "0.00%"
                ws.Cell(r, 7).Value = "PAR > 30:";
                ws.Cell(r, 8).Value = report.OverallPAR30;
                ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 9).Value = "PAR > 60:";
                ws.Cell(r, 10).Value = report.OverallPAR60;
                ws.Range(r, 10, r, 10).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 11).Value = "PAR > 90:";
                ws.Cell(r, 12).Value = report.OverallPAR90;
                ws.Range(r, 12, r, 12).Style.NumberFormat.Format = "0.00";
                r += 2;

                // Headers
                string[] headers = { "#", "SACCO Name", "Loans", "Loan Amount", "Balance",
                             "Arrears >30", "PAR >30", "PAR >60", "PAR >90", "Health", "Loans Paid" };

                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(r, i + 1).Value = headers[i];
                    ws.Cell(r, i + 1).Style.Font.SetBold();
                    ws.Cell(r, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                r++;

                int counter = 1;
                foreach (var sacco in report.SaccoSummaries)
                {
                    ws.Cell(r, 1).Value = counter++;
                    ws.Cell(r, 2).Value = sacco.CompanyName;
                    ws.Cell(r, 3).Value = sacco.NumberOfLoans;
                    ws.Cell(r, 4).Value = sacco.TotalLoanAmount;
                    ws.Range(r, 4, r, 4).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(r, 5).Value = sacco.TotalLoanBalance;
                    ws.Range(r, 5, r, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(r, 6).Value = sacco.TotalArrears30;
                    ws.Range(r, 6, r, 6).Style.NumberFormat.Format = "#,##0.00";

                    // FIX: Use "0.00" format for PAR values (already in percentage)
                    ws.Cell(r, 7).Value = sacco.PAR30;
                    ws.Range(r, 7, r, 7).Style.NumberFormat.Format = "0.00";

                    ws.Cell(r, 8).Value = sacco.PAR60;
                    ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";

                    ws.Cell(r, 9).Value = sacco.PAR90;
                    ws.Range(r, 9, r, 9).Style.NumberFormat.Format = "0.00";

                    ws.Cell(r, 10).Value = sacco.PortfolioHealth;
                    ws.Cell(r, 11).Value = sacco.TotalLoansPaid;
                    ws.Range(r, 11, r, 11).Style.NumberFormat.Format = "#,##0.00";

                    // Color code PAR > 30
                    if (sacco.PAR30 < 5)
                        ws.Cell(r, 7).Style.Font.FontColor = XLColor.Green;
                    else if (sacco.PAR30 < 10)
                        ws.Cell(r, 7).Style.Font.FontColor = XLColor.Orange;
                    else
                        ws.Cell(r, 7).Style.Font.FontColor = XLColor.Red;

                    r++;
                }

                // Totals
                ws.Cell(r, 2).Value = "TOTALS";
                ws.Range(r, 2, r, 2).Style.Font.SetBold();
                ws.Cell(r, 3).Value = report.TotalLoans;
                ws.Cell(r, 4).Value = report.TotalLoanAmount;
                ws.Range(r, 4, r, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 5).Value = report.TotalLoanBalance;
                ws.Range(r, 5, r, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 6).Value = report.SaccoSummaries.Sum(s => s.TotalArrears30);
                ws.Range(r, 6, r, 6).Style.NumberFormat.Format = "#,##0.00";

                // FIX: Use "0.00" format for totals
                ws.Cell(r, 7).Value = report.OverallPAR30;
                ws.Range(r, 7, r, 7).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 8).Value = report.OverallPAR60;
                ws.Range(r, 8, r, 8).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 9).Value = report.OverallPAR90;
                ws.Range(r, 9, r, 9).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 11).Value = report.SaccoSummaries.Sum(s => s.TotalLoansPaid);
                ws.Range(r, 11, r, 11).Style.NumberFormat.Format = "#,##0.00";

                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"CountyLoansReport_{userCompany.County}_{asAtDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting county loans report to Excel");
                TempData["ErrorMessage"] = "Error exporting report";
                return RedirectToAction("CountyLoansReport");
            }
        }

        // ============================================================
        // EXPORT COUNTY LOANS TO PDF
        // ============================================================

        [HttpPost("ExportCountyLoansToPdf")]
        public async Task<IActionResult> ExportCountyLoansToPdf(DateTime asAtDate)
        {
            try
            {
                var userCompanyCode = User.FindFirst("CompanyCode")?.Value;

                if (string.IsNullOrEmpty(userCompanyCode))
                {
                    TempData["ErrorMessage"] = "User company not found.";
                    return RedirectToAction("CountyLoansReport");
                }

                var userCompany = await _countyReportService.GetCompanyByCodeAsync(userCompanyCode);

                if (userCompany == null || string.IsNullOrEmpty(userCompany.County))
                {
                    TempData["ErrorMessage"] = "County not found.";
                    return RedirectToAction("CountyLoansReport");
                }

                var report = await _countyReportService.GetCountyLoansReportAsync(userCompany.County, asAtDate);

                using var stream = new MemoryStream();

                Document.Create(container =>
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
                                column.Item().Text($"{userCompany.County.ToUpper()} COUNTY - LOANS REPORT").FontSize(18).Bold();
                                column.Item().Text($"As At: {asAtDate:dd/MM/yyyy}").FontSize(14).Bold();
                                column.Item().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(9).Italic();
                                column.Item().PaddingTop(0.5f, Unit.Centimetre).LineHorizontal(0.5f);
                            });

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                // Summary Statistics
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

                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"SACCOs: {report.TotalCompanies}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Loans: {report.TotalLoans}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"Balance: {report.TotalLoanBalance:N0}").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"PAR>30: {report.OverallPAR30:F1}%").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"PAR>60: {report.OverallPAR60:F1}%").Bold();
                                    statsTable.Cell().Border(0.2f).Padding(4).Text($"PAR>90: {report.OverallPAR90:F1}%").Bold();
                                });

                                column.Item().PaddingTop(1, Unit.Centimetre);

                                // Main Data Table
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.5f);
                                        cols.RelativeColumn(2.0f);
                                        cols.RelativeColumn(0.8f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.2f);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("#").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("SACCO Name").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loans").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Arrears>30").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("PAR>30").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("PAR>60").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("PAR>90").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Health").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loans Paid").Bold().FontSize(8);
                                    });

                                    int counter = 1;
                                    foreach (var sacco in report.SaccoSummaries)
                                    {
                                        var parColor = sacco.PAR30 < 5 ? Colors.Green.Darken1 :
                                                       sacco.PAR30 < 10 ? Colors.Orange.Darken1 :
                                                       Colors.Red.Darken1;

                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(counter++.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).Text(sacco.CompanyName).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(sacco.NumberOfLoans.ToString()).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalLoanAmount:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalLoanBalance:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalArrears30:N0}").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.PAR30:F1}%").FontColor(parColor).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.PAR60:F1}%").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.PAR90:F1}%").FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(sacco.PortfolioHealth).FontSize(8);
                                        table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalLoansPaid:N0}").FontSize(8);
                                    }
                                });

                                // Footer Statistics
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Table(footerTable =>
                                {
                                    footerTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });

                                    var totalArrears30 = report.SaccoSummaries.Sum(s => s.TotalArrears30);
                                    var totalLoansPaid = report.SaccoSummaries.Sum(s => s.TotalLoansPaid);

                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total Arrears>30: {totalArrears30:N0}").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total PAR>30: {report.OverallPAR30:F1}%").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total PAR>60: {report.OverallPAR60:F1}%").Bold().FontSize(9);
                                    footerTable.Cell().Border(0.2f).Background("#e7f3ff").Padding(4).Text($"Total Loans Paid: {totalLoansPaid:N0}").Bold().FontSize(9);
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

                return File(stream.ToArray(), "application/pdf", $"CountyLoansReport_{userCompany.County}_{asAtDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting county loans report to PDF");
                TempData["ErrorMessage"] = "Error exporting report";
                return RedirectToAction("CountyLoansReport");
            }
        }
    }
}

