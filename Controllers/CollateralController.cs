// Controllers/CollateralController.cs
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class CollateralController : Controller
    {
        private readonly ICollateralService _collateralService;
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CollateralController> _logger;

        public CollateralController(ICollateralService collateralService, IUserService userService, ILogger<CollateralController> logger, ApplicationDbContext context)
        {
            _collateralService = collateralService;
            _userService = userService;
            _context = context;
            _logger = logger;
        }

        private async Task<string> GetCurrentUserCompanyCodeAsync()
        {
            var claim = User.FindFirst("CompanyCode")?.Value ?? User.FindFirst("companyCode")?.Value;
            if (!string.IsNullOrEmpty(claim)) return claim;

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user != null && !string.IsNullOrEmpty(user.CompanyCode))
                    return user.CompanyCode;
            }
            return string.Empty;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "Unknown";
        }

        public async Task<IActionResult> Index()
        {
            var companyCode = await GetCurrentUserCompanyCodeAsync();
            var collaterals = await _collateralService.GetAllAsync(companyCode);
            ViewBag.NewColCode = await _collateralService.GenerateColCodeAsync(companyCode);
            return View(collaterals);
        }

        // GET: Search members
        [HttpGet]
        public async Task<IActionResult> SearchMembers(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Json(new { success = true, data = new List<object>() });
                }

                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var members = await _collateralService.SearchMembersAsync(term, companyCode);

                var results = members.Select(m => new
                {
                    m.MemberNo,
                    FullName = $"{m.Surname} {m.OtherNames}".Trim(),
                    m.Idno,
                    m.PhoneNo,
                    m.Email
                }).ToList();

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CollateralDTO dto)
        {
            try
            {
                if (dto == null)
                    return Json(new { success = false, message = "Invalid data" });

                dto.CompanyCode = await GetCurrentUserCompanyCodeAsync();
                var result = await _collateralService.CreateAsync(dto, GetCurrentUserId());
                return Json(new { success = true, message = "Collateral created successfully", collateral = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating collateral");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [FromForm] CollateralDTO dto)
        {
            try
            {
                if (dto == null)
                    return Json(new { success = false, message = "Invalid data" });

                if (string.IsNullOrEmpty(dto.CompanyCode))
                {
                    dto.CompanyCode = await GetCurrentUserCompanyCodeAsync();
                }

                var result = await _collateralService.UpdateAsync(id, dto, GetCurrentUserId());
                return Json(new { success = true, message = "Collateral updated successfully", collateral = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating collateral {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _collateralService.DeleteAsync(id, GetCurrentUserId());
                return Json(new { success = true, message = "Collateral deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Controllers/CollateralController.cs - Add these new endpoints

        [HttpGet]
        public async Task<IActionResult> GetMemberCollaterals(string memberNo)
        {
            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var collaterals = await _collateralService.GetMemberCollateralsAsync(memberNo, companyCode);
                return Json(new { success = true, data = collaterals });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member collaterals");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableCollateralsForGuarantee(string memberNo)
        {
            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var collaterals = await _collateralService.GetAvailableMemberCollateralsForGuaranteeAsync(memberNo, companyCode);
                return Json(new { success = true, data = collaterals });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available collaterals for guarantee");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(long id)
        {
            try
            {
                var collateral = await _collateralService.GetByIdAsync(id);
                if (collateral == null)
                    return Json(new { success = false, message = "Collateral not found" });
                return Json(new { success = true, collateral });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateCode()
        {
            try
            {
                var code = await _collateralService.GenerateColCodeAsync(await GetCurrentUserCompanyCodeAsync());
                return Json(new { success = true, colCode = code });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Collateral Report

        [HttpGet]
        public async Task<IActionResult> Report()
        {
            var companyCode = await GetCurrentUserCompanyCodeAsync();
            var companyName = await GetCompanyNameAsync(companyCode);
            var reportData = await _collateralService.GetCollateralReportAsync(companyCode);

            ViewBag.CompanyName = companyName;
            ViewBag.GeneratedDate = DateTime.Now;
            ViewBag.HasData = reportData.Any();

            return View(reportData);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCollateralToExcel()
        {
            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var companyName = await GetCompanyNameAsync(companyCode);
                var reportData = await _collateralService.GetCollateralReportAsync(companyCode);

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found to export";
                    return RedirectToAction("Report");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Collateral Report");
                var currentRow = 1;

                // Header - Company Name
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                // Report Title
                worksheet.Cell(currentRow, 1).Value = "COLLATERAL REPORT";
                worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                // Printed By and Date
                worksheet.Cell(currentRow, 1).Value = $"Printed By: {User.Identity?.Name ?? "System"} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                worksheet.Range(currentRow, 1, currentRow, 8).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
                currentRow += 2;

                // Headers
                string[] headers = { "Member No", "Names", "Loan No", "Collateral Code", "Collateral Description", "Market Value (KES)", "Balance (KES)", "Percentage" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                currentRow++;

                // Data rows
                decimal totalBalance = 0;
                decimal totalMarketValue = 0;

                foreach (var item in reportData)
                {
                    worksheet.Cell(currentRow, 1).Value = item.MemberNo;
                    worksheet.Cell(currentRow, 2).Value = item.Names;
                    worksheet.Cell(currentRow, 3).Value = item.LoanNo;
                    worksheet.Cell(currentRow, 4).Value = item.ColCode;
                    worksheet.Cell(currentRow, 5).Value = item.Coldescription;
                    worksheet.Cell(currentRow, 6).Value = item.Mktvalue;
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 7).Value = item.Balance;
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 8).Value = item.Percentage;
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "0.00%";

                    totalBalance += item.Balance;
                    totalMarketValue += item.Mktvalue;
                    currentRow++;
                }

                // Totals row
                currentRow++;
                worksheet.Cell(currentRow, 5).Value = "TOTALS:";
                worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                worksheet.Cell(currentRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                worksheet.Cell(currentRow, 6).Value = totalMarketValue;
                worksheet.Cell(currentRow, 6).Style.Font.SetBold();
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = totalBalance;
                worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

                // Summary statistics
                currentRow += 2;
                worksheet.Cell(currentRow, 1).Value = "SUMMARY STATISTICS:";
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = "Total Collaterals:";
                worksheet.Cell(currentRow, 2).Value = reportData.Count;
                worksheet.Cell(currentRow, 3).Value = "Total Market Value:";
                worksheet.Cell(currentRow, 4).Value = totalMarketValue;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Total Outstanding Balance:";
                worksheet.Cell(currentRow, 2).Value = totalBalance;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 3).Value = "Coverage Ratio:";
                worksheet.Cell(currentRow, 4).Value = totalBalance > 0 ? (totalMarketValue / totalBalance) * 1 : 0;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "0.00%";

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"CollateralReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error exporting to Excel: {ex.Message}";
                return RedirectToAction("Report");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCollateralToPdf()
        {
            try
            {
                var companyCode = await GetCurrentUserCompanyCodeAsync();
                var companyName = await GetCompanyNameAsync(companyCode);
                var reportData = await _collateralService.GetCollateralReportAsync(companyCode);
                var printedBy = User.Identity?.Name ?? "System";

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data found to export";
                    return RedirectToAction("Report");
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
                            header.Item().AlignCenter().Text("COLLATERAL REPORT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Statistics
                            decimal totalBalance = reportData.Sum(x => x.Balance);
                            decimal totalMarketValue = reportData.Sum(x => x.Mktvalue);

                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Collaterals:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Market Value:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalMarketValue:N0}");

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Outstanding:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalBalance:N0}");
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Coverage Ratio:").Bold();
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text(totalBalance > 0 ? $"{(totalMarketValue / totalBalance) * 100:F2}%" : "N/A");
                            });

                            // Collateral Details Table
                            contentCol.Item().PaddingTop(1, QuestPDF.Infrastructure.Unit.Centimetre);
                            contentCol.Item().Text("COLLATERAL DETAILS").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.8f);  // Member No
                                    cols.RelativeColumn(1.5f);  // Names
                                    cols.RelativeColumn(1.0f);  // Loan No
                                    cols.RelativeColumn(0.8f);  // Col Code
                                    cols.RelativeColumn(1.8f);  // Description
                                    cols.RelativeColumn(1.0f);  // Market Value
                                    cols.RelativeColumn(1.0f);  // Balance
                                    cols.RelativeColumn(0.6f);  // Percentage
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Col Code").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Description").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Market Value").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("%").Bold().FontSize(8);
                                });

                                foreach (var item in reportData)
                                {
                                    table.Cell().Border(0.2f).Padding(4).Text(item.MemberNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.Names ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.LoanNo ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.ColCode ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.Coldescription ?? "").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Mktvalue:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Balance:N0}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text($"{item.Percentage:F2}%").FontSize(8);
                                }

                                // Totals row
                                table.Cell().ColumnSpan(5).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTALS:").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalMarketValue:N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalBalance:N0}").Bold().FontSize(9);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4);
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

                var content = stream.ToArray();
                return File(content, "application/pdf", $"CollateralReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error exporting to PDF: {ex.Message}";
                return RedirectToAction("Report");
            }
        }

        private async Task<string> GetCompanyNameAsync(string companyCode)
        {
            if (string.IsNullOrEmpty(companyCode))
                return "SACCO BlockChain System";

            try
            {
                var company = await _context.Companies
                    .Where(c => c.CompanyCode == companyCode)
                    .Select(c => c.CompanyName)
                    .FirstOrDefaultAsync();
                return company ?? "SACCO BlockChain System";
            }
            catch
            {
                return "SACCO BlockChain System";
            }
        }

        #endregion
    }
}