using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class NextOfKeenReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NextOfKeenReportController> _logger;

        public NextOfKeenReportController(
            ApplicationDbContext context,
            ILogger<NextOfKeenReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var currentUser = User.Identity?.Name ?? "System";

                // Get all active members with their next of kin
                var membersQuery = await _context.Members
                    .Where(m => m.CompanyCode == companyCode
                        && (m.Withdrawn == null || m.Withdrawn == false)
                        && (m.Archived == null || m.Archived == false))
                    .Select(m => new
                    {
                        Member = m,
                        NextOfKeens = _context.NextOfKeens
                            .Where(n => n.MemberNo == m.MemberNo && n.CompanyCode == companyCode && n.Status == "Active")
                            .Select(n => new NextOfKinDetailDto
                            {
                                FullName = n.FullName,
                                Relationship = n.Relationship,
                                PhoneNo = n.PhoneNo,
                                BenefitPercentage = n.BenefitPercentage,
                                IsPrimary = n.IsPrimary,
                                Email = n.Email ?? "",
                                PhysicalAddress = n.PhysicalAddress ?? ""
                            })
                            .ToList()
                    })
                    .ToListAsync();

                // Filter to ONLY members with at least one next of kin
                var membersWithNextOfKin = membersQuery
                    .Where(x => x.NextOfKeens.Any())
                    .Select(x => new MemberNextOfKinReportDto
                    {
                        MemberNo = x.Member.MemberNo,
                        FullName = !string.IsNullOrEmpty(x.Member.FullName)
                            ? x.Member.FullName
                            : $"{x.Member.Surname ?? ""} {x.Member.OtherNames ?? ""}".Trim(),
                        IdNumber = x.Member.Idno ?? "-",
                        PhoneNo = x.Member.PhoneNo ?? x.Member.MobileNo ?? "-",
                        Status = x.Member.Withdrawn == true ? "WITHDRAWN"
                            : x.Member.Archived == true ? "ARCHIVED"
                            : "ACTIVE",
                        TotalNextOfKeens = x.NextOfKeens.Count,
                        TotalBenefitPercentage = x.NextOfKeens.Sum(n => n.BenefitPercentage ?? 0),
                        NextOfKeens = x.NextOfKeens
                    })
                    .OrderBy(m => m.MemberNo)
                    .ToList();

                // Calculate summary statistics
                var summary = new ReportSummary
                {
                    TotalMembers = membersWithNextOfKin.Count,
                    TotalNextOfKeens = membersWithNextOfKin.Sum(m => m.TotalNextOfKeens),
                    MembersWithCompleteBenefit = membersWithNextOfKin.Count(m => m.HasValidBenefit && m.TotalBenefitPercentage > 0),
                    MembersWithInvalidBenefit = membersWithNextOfKin.Count(m => !m.HasValidBenefit),
                    MembersWithNoNextOfKin = 0 // Not needed since we filter them out
                };

                var viewModel = new NextOfKinReportViewModel
                {
                    CompanyName = companyName,
                    CompanyAddress = User.FindFirstValue("CompanyAddress") ?? "",
                    CompanyPhone = User.FindFirstValue("CompanyPhone") ?? "",
                    CompanyEmail = User.FindFirstValue("CompanyEmail") ?? "",
                    ReportTitle = "NEXT OF KIN REPORT (Members with Next of Kin)",
                    GeneratedDate = DateTime.Now,
                    GeneratedBy = currentUser,
                    Summary = summary,
                    MembersWithNextOfKin = membersWithNextOfKin
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Next of Kin report");
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportExcel()
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";

                var membersWithNextOfKin = await GetMembersWithNextOfKinData(companyCode);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Next of Kin Report");
                var currentRow = 1;

                // Company Header
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 12).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Report Title
                worksheet.Cell(currentRow, 1).Value = "NEXT OF KIN REPORT (Members with Next of Kin)";
                worksheet.Range(currentRow, 1, currentRow, 12).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow += 2;

                // Summary Statistics
                worksheet.Cell(currentRow, 1).Value = "SUMMARY";
                worksheet.Range(currentRow, 1, currentRow, 4).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Total Members with Next of Kin:";
                worksheet.Cell(currentRow, 2).Value = membersWithNextOfKin.Count;
                worksheet.Cell(currentRow, 3).Value = "Total Next of Kin Records:";
                worksheet.Cell(currentRow, 4).Value = membersWithNextOfKin.Sum(m => m.TotalNextOfKeens);
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Members with Complete Benefit (≤100%):";
                worksheet.Cell(currentRow, 2).Value = membersWithNextOfKin.Count(m => m.HasValidBenefit && m.TotalBenefitPercentage > 0);
                worksheet.Cell(currentRow, 3).Value = "Members with Invalid Benefit (>100%):";
                worksheet.Cell(currentRow, 4).Value = membersWithNextOfKin.Count(m => !m.HasValidBenefit);
                currentRow += 2;

                // Headers
                string[] headers = { "Member No", "Member Name", "ID Number", "Phone", "Status",
                                   "Total NOK", "Benefit %", "NOK Name", "Relationship", "NOK Phone",
                                   "Benefit %", "Primary" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                currentRow++;

                // Data rows
                foreach (var member in membersWithNextOfKin)
                {
                    if (member.NextOfKeens.Any())
                    {
                        foreach (var nok in member.NextOfKeens)
                        {
                            int col = 1;
                            worksheet.Cell(currentRow, col++).Value = member.MemberNo;
                            worksheet.Cell(currentRow, col++).Value = member.FullName;
                            worksheet.Cell(currentRow, col++).Value = member.IdNumber;
                            worksheet.Cell(currentRow, col++).Value = member.PhoneNo;
                            worksheet.Cell(currentRow, col++).Value = member.Status;
                            worksheet.Cell(currentRow, col++).Value = member.TotalNextOfKeens;
                            worksheet.Cell(currentRow, col++).Value = member.TotalBenefitPercentage;
                            worksheet.Cell(currentRow, col++).Value = nok.FullName;
                            worksheet.Cell(currentRow, col++).Value = nok.Relationship;
                            worksheet.Cell(currentRow, col++).Value = nok.PhoneNo;
                            worksheet.Cell(currentRow, col++).Value = nok.BenefitPercentage ?? 0;
                            worksheet.Cell(currentRow, col++).Value = nok.IsPrimary ? "Yes" : "No";

                            // Highlight invalid benefit rows
                            if (!member.HasValidBenefit)
                            {
                                worksheet.Range(currentRow, 1, currentRow, 12).Style.Fill.BackgroundColor = XLColor.LightPink;
                            }

                            currentRow++;
                        }
                    }
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"NextOfKinReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Next of Kin report to Excel");
                TempData["ErrorMessage"] = "Error exporting to Excel: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPdf()
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var currentUser = User.Identity?.Name ?? "System";

                var membersWithNextOfKin = await GetMembersWithNextOfKinData(companyCode);

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
                            header.Item().AlignCenter().Text("NEXT OF KIN REPORT (Members with Next of Kin)").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Generated By: {currentUser} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
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

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text($"Total Members: {membersWithNextOfKin.Count}").Bold();
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text($"Total NOK: {membersWithNextOfKin.Sum(m => m.TotalNextOfKeens)}").Bold();
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text($"Complete Benefit (≤100%): {membersWithNextOfKin.Count(m => m.HasValidBenefit && m.TotalBenefitPercentage > 0)}").Bold();
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text($"Invalid Benefit (>100%): {membersWithNextOfKin.Count(m => !m.HasValidBenefit)}").Bold();
                            });

                            contentCol.Item().PaddingTop(1, Unit.Centimetre);

                            // Member Details Table
                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.8f);  // Member No
                                    cols.RelativeColumn(1.5f);  // Member Name
                                    cols.RelativeColumn(0.8f);  // ID Number
                                    cols.RelativeColumn(0.8f);  // Phone
                                    cols.RelativeColumn(0.8f);  // Status
                                    cols.RelativeColumn(0.5f);  // Total NOK
                                    cols.RelativeColumn(0.8f);  // Benefit %
                                    cols.RelativeColumn(1.5f);  // NOK Name
                                    cols.RelativeColumn(0.8f);  // Relationship
                                    cols.RelativeColumn(0.8f);  // NOK Phone
                                    cols.RelativeColumn(0.5f);  // Benefit %
                                    cols.RelativeColumn(0.5f);  // Primary
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ID").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Benefit %").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("NOK Name").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Relationship").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("NOK Phone").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Benefit %").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Primary").Bold().FontSize(7);
                                });

                                foreach (var member in membersWithNextOfKin)
                                {
                                    if (member.NextOfKeens.Any())
                                    {
                                        foreach (var nok in member.NextOfKeens)
                                        {
                                            var rowBg = !member.HasValidBenefit ? "#f8d7da" : "#ffffff";

                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(member.MemberNo).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(member.FullName).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(member.IdNumber).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(member.PhoneNo).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(member.Status).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).AlignCenter().Text(member.TotalNextOfKeens.ToString()).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).AlignRight().Text($"{member.TotalBenefitPercentage:F1}%").FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(nok.FullName).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(nok.Relationship).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).Text(nok.PhoneNo).FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).AlignRight().Text($"{nok.BenefitPercentage:F1}%").FontSize(7);
                                            table.Cell().Border(0.2f).Background(rowBg).Padding(4).AlignCenter().Text(nok.IsPrimary ? "Yes" : "No").FontSize(7);
                                        }
                                    }
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
                    $"NextOfKinReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Next of Kin report to PDF");
                TempData["ErrorMessage"] = "Error exporting to PDF: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Helper method to get members with next of kin data
        private async Task<List<MemberNextOfKinReportDto>> GetMembersWithNextOfKinData(string companyCode)
        {
            var membersQuery = await _context.Members
                .Where(m => m.CompanyCode == companyCode
                    && (m.Withdrawn == null || m.Withdrawn == false)
                    && (m.Archived == null || m.Archived == false))
                .Select(m => new
                {
                    Member = m,
                    NextOfKeens = _context.NextOfKeens
                        .Where(n => n.MemberNo == m.MemberNo && n.CompanyCode == companyCode && n.Status == "Active")
                        .Select(n => new NextOfKinDetailDto
                        {
                            FullName = n.FullName,
                            Relationship = n.Relationship,
                            PhoneNo = n.PhoneNo,
                            BenefitPercentage = n.BenefitPercentage,
                            IsPrimary = n.IsPrimary,
                            Email = n.Email ?? "",
                            PhysicalAddress = n.PhysicalAddress ?? ""
                        })
                        .ToList()
                })
                .ToListAsync();

            // Filter to ONLY members with at least one next of kin
            return membersQuery
                .Where(x => x.NextOfKeens.Any())
                .Select(x => new MemberNextOfKinReportDto
                {
                    MemberNo = x.Member.MemberNo,
                    FullName = !string.IsNullOrEmpty(x.Member.FullName)
                        ? x.Member.FullName
                        : $"{x.Member.Surname ?? ""} {x.Member.OtherNames ?? ""}".Trim(),
                    IdNumber = x.Member.Idno ?? "-",
                    PhoneNo = x.Member.PhoneNo ?? x.Member.MobileNo ?? "-",
                    Status = x.Member.Withdrawn == true ? "WITHDRAWN"
                        : x.Member.Archived == true ? "ARCHIVED"
                        : "ACTIVE",
                    TotalNextOfKeens = x.NextOfKeens.Count,
                    TotalBenefitPercentage = x.NextOfKeens.Sum(n => n.BenefitPercentage ?? 0),
                    NextOfKeens = x.NextOfKeens
                })
                .OrderBy(m => m.MemberNo)
                .ToList();
        }
    }
}