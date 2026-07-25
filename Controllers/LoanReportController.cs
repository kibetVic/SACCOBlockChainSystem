using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class LoanReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoanReportController> _logger;

        public LoanReportController(ApplicationDbContext context, ILogger<LoanReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Loans Issued Report

        [HttpGet]
        public IActionResult LoansIssued()
        {
            ViewBag.StartDate = DateTime.Now.AddMonths(-1);
            ViewBag.EndDate = DateTime.Now;
            ViewBag.HasData = false;

            var emptyList = new List<LoanIssuedReportViewModel>();
            return View("~/Views/Reports/LoansIssued.cshtml", emptyList);
        }

        [HttpPost]
        public async Task<IActionResult> LoansIssued(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            endDate = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data using proper JOIN
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              && loan.AuditTime >= startDate
                                              && loan.AuditTime <= endDate
                                              //&& loan.Status == (int)Status.Disbursed
                                          orderby loan.AuditTime
                                          select new
                                          {
                                              loan.Id,
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              LoanAmt = loan.LoanAmt ?? 0,
                                              ApprovedAmount = loan.Aamount ?? loan.LoanAmt ?? 0,
                                              loan.Interest,
                                              loan.LoanCode
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                var emptyList = new List<LoanIssuedReportViewModel>();
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.TotalLoanApplied = 0;
                ViewBag.TotalApprovedAmount = 0;
                ViewBag.RecordCount = 0;
                ViewBag.HasData = false;
                ViewBag.CompanyName = companyName;
                return View("~/Views/Reports/LoansIssued.cshtml", emptyList);
            }

            // Get all loan numbers for fetching appraisal and endorsement data
            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            // Get Appraisal dates for each loan
            var appraisals = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                .ToDictionaryAsync(a => a.LoanNo, a => a.AppraisDate);

            // Get Endorsement dates (Endmain table)
            var endorsements = await _context.Endmain
                .Where(e => loanNos.Contains(e.LoanNo) && e.CompanyCode == companyCode)
                .ToDictionaryAsync(e => e.LoanNo, e => e.MeetingDate);

            var reportData = loansWithMembers.Select((l, index) => new LoanIssuedReportViewModel
            {
                No = index + 1,
                MemberNo = l.MemberNo,
                LoanNo = l.LoanNo,
                Name = !string.IsNullOrEmpty(l.MemberFullName)
                    ? l.MemberFullName
                    : $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim(),
                ApplicationDate = l.ApplicDate,
                AppraisalDate = appraisals.ContainsKey(l.LoanNo) ? appraisals[l.LoanNo] : null,
                EndorsementDate = endorsements.ContainsKey(l.LoanNo) ? endorsements[l.LoanNo] : null,
                DateIssued = l.AuditTime,
                LoanPeriodMonths = l.RepayPeriod ?? 0,
                LoanApplied = l.LoanAmt,
                ApprovedAmount = l.ApprovedAmount,
                InterestRate = l.Interest,
                LoanType = l.LoanCode
            }).ToList();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.TotalLoanApplied = reportData.Sum(l => l.LoanApplied);
            ViewBag.TotalApprovedAmount = reportData.Sum(l => l.ApprovedAmount);
            ViewBag.RecordCount = reportData.Count;
            ViewBag.HasData = reportData.Any();
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/LoansIssued.cshtml", reportData);
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            endDate = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              && loan.AuditTime >= startDate
                                              && loan.AuditTime <= endDate
                                              && loan.Status == (int)Status.Disbursed
                                          orderby loan.AuditTime
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              LoanAmt = loan.LoanAmt ?? 0,
                                              ApprovedAmount = loan.Aamount ?? loan.LoanAmt ?? 0,
                                              loan.LoanCode
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansIssued");
            }

            // Get appraisal and endorsement dates
            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            var appraisals = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                .ToDictionaryAsync(a => a.LoanNo, a => a.AppraisDate);

            var endorsements = await _context.Endmain
                .Where(e => loanNos.Contains(e.LoanNo) && e.CompanyCode == companyCode)
                .ToDictionaryAsync(e => e.LoanNo, e => e.MeetingDate);

            var reportData = loansWithMembers.Select((l, index) => new LoanIssuedReportViewModel
            {
                No = index + 1,
                MemberNo = l.MemberNo,
                LoanNo = l.LoanNo,
                Name = !string.IsNullOrEmpty(l.MemberFullName)
                    ? l.MemberFullName
                    : $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim(),
                ApplicationDate = l.ApplicDate,
                AppraisalDate = appraisals.ContainsKey(l.LoanNo) ? appraisals[l.LoanNo] : null,
                EndorsementDate = endorsements.ContainsKey(l.LoanNo) ? endorsements[l.LoanNo] : null,
                DateIssued = l.AuditTime,
                LoanPeriodMonths = l.RepayPeriod ?? 0,
                LoanApplied = l.LoanAmt,
                ApprovedAmount = l.ApprovedAmount
            }).ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loans Issued");
            var currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {User.Identity?.Name ?? "System"} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOANS ISSUED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            // Updated headers to include Appraisal Date and Endorsement Date
            string[] headers = { "No.", "MemberNo", "LoanNo", "MemberName", "App.Date", "Appraisal Date", "Endorsement", "Date Issued", "Period (Months)", "Loan Applied (KES)", "Approved Amt (KES)" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            if (reportData.Any())
            {
                int serialNo = 1;
                foreach (var loan in reportData)
                {
                    worksheet.Cell(currentRow, 1).Value = serialNo++;
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    worksheet.Cell(currentRow, 2).Value = loan.MemberNo;
                    worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
                    worksheet.Cell(currentRow, 4).Value = loan.Name;
                    worksheet.Cell(currentRow, 5).Value = loan.ApplicationDate?.ToString("dd/MM/yyyy");
                    worksheet.Cell(currentRow, 6).Value = loan.AppraisalDate?.ToString("dd/MM/yyyy") ?? "-";
                    worksheet.Cell(currentRow, 7).Value = loan.EndorsementDate?.ToString("dd/MM/yyyy") ?? "-";
                    worksheet.Cell(currentRow, 8).Value = loan.DateIssued?.ToString("dd/MM/yyyy");
                    worksheet.Cell(currentRow, 9).Value = loan.LoanPeriodMonths;
                    worksheet.Cell(currentRow, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    worksheet.Cell(currentRow, 10).Value = loan.LoanApplied;
                    worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    worksheet.Cell(currentRow, 11).Value = loan.ApprovedAmount;
                    worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                    for (int i = 1; i <= headers.Length; i++)
                    {
                        worksheet.Cell(currentRow, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    currentRow++;
                }

                currentRow++;
                worksheet.Cell(currentRow, 9).Value = "GRAND TOTALS:";
                worksheet.Cell(currentRow, 9).Style.Font.SetBold();
                worksheet.Cell(currentRow, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                worksheet.Cell(currentRow, 10).Value = reportData.Sum(l => l.LoanApplied);
                worksheet.Cell(currentRow, 10).Style.Font.SetBold();
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                worksheet.Cell(currentRow, 11).Value = reportData.Sum(l => l.ApprovedAmount);
                worksheet.Cell(currentRow, 11).Style.Font.SetBold();
                worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            }
            else
            {
                for (int i = 1; i <= headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i).Value = "-";
                    worksheet.Cell(currentRow, i).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    worksheet.Cell(currentRow, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoansIssued_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }
        [HttpPost]
        public async Task<IActionResult> ExportToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            endDate = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              && loan.AuditTime >= startDate
                                              && loan.AuditTime <= endDate
                                              && loan.Status == (int)Status.Disbursed
                                          orderby loan.AuditTime
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              LoanAmt = loan.LoanAmt ?? 0,
                                              ApprovedAmount = loan.Aamount ?? loan.LoanAmt ?? 0
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansIssued");
            }

            // Get appraisal and endorsement dates
            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            var appraisals = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                .ToDictionaryAsync(a => a.LoanNo, a => a.AppraisDate);

            var endorsements = await _context.Endmain
                .Where(e => loanNos.Contains(e.LoanNo) && e.CompanyCode == companyCode)
                .ToDictionaryAsync(e => e.LoanNo, e => e.MeetingDate);

            var reportData = loansWithMembers.Select((l, index) => new LoanIssuedReportViewModel
            {
                No = index + 1,
                MemberNo = l.MemberNo,
                LoanNo = l.LoanNo,
                Name = !string.IsNullOrEmpty(l.MemberFullName)
                    ? l.MemberFullName
                    : $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim(),
                ApplicationDate = l.ApplicDate,
                AppraisalDate = appraisals.ContainsKey(l.LoanNo) ? appraisals[l.LoanNo] : null,
                EndorsementDate = endorsements.ContainsKey(l.LoanNo) ? endorsements[l.LoanNo] : null,
                DateIssued = l.AuditTime,
                LoanPeriodMonths = l.RepayPeriod ?? 0,
                LoanApplied = l.LoanAmt,
                ApprovedAmount = l.ApprovedAmount
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
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.5f, Unit.Centimetre);
                        header.Item().AlignCenter().Text($"LOANS ISSUED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.4f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.6f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.5f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("No").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Name").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("App.Date").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Appraisal").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Endorsement").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Issued").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Period").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Applied (KES)").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Approved (KES)").Bold().FontSize(8);
                        });

                        int serialNo = 1;
                        foreach (var loan in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(serialNo++.ToString()).FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.Name ?? "").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicationDate?.ToString("dd/MM/yyyy") ?? "").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.AppraisalDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.EndorsementDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DateIssued?.ToString("dd/MM/yyyy") ?? "").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.LoanPeriodMonths.ToString()).FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanApplied:N0}").FontSize(8);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.ApprovedAmount:N0}").FontSize(8);
                        }

                        if (reportData.Any())
                        {
                            table.Cell().ColumnSpan(9).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(9);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(l => l.LoanApplied):N0}").Bold().FontSize(9);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{reportData.Sum(l => l.ApprovedAmount):N0}").Bold().FontSize(9);
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
                        });
                });
            }).GeneratePdf(stream);

            var content = stream.ToArray();
            return File(content, "application/pdf", $"LoansIssued_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }
        #endregion

        #region Loans Per SACCO Report

        public IActionResult LoansPerSacco()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;
            var startDate = reportDate.AddMonths(-1);
            var endDate = reportDate;

            var viewModel = new LoansPerSaccoIndexViewModel
            {
                CompletedLoans = new List<LoansPerSaccoReportViewModel>(),
                IncompleteLoans = new List<LoansPerSaccoReportViewModel>(),
                GigGroups = new List<CIGLoanSummary>(),
                ReportDate = reportDate,
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalCompletedLoans = 0,
                TotalIncompleteLoans = 0,
                TotalLoans = 0,
                TotalCompletedLoanAmount = 0,
                TotalIncompleteLoanAmount = 0,
                TotalOutstandingBalance = 0,
                TotalLoanAmount = 0
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoansPerSacco.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoansPerSacco(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var reportDate = DateTime.Now;

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data - DO NOT filter by ApplicDate for active loans
            // Instead, get all disbursed/endorsed loans regardless of application date
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              //&& (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= endDateAdjusted
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.CompanyCode,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberGigCode = member.Cigcode,
                                              MemberFullName = member.FullName
                                          }).ToListAsync();

            // If no loans found with Disbursed/Endorsed status, try to get all loans with positive balance
            if (!loansWithMembers.Any())
            {
                // Try to get loans from Loanbals table with positive balance
                var loansWithBalance = await (from lb in _context.Loanbal
                                              join loan in _context.Loans on lb.LoanNo equals loan.LoanNo
                                              join member in _context.Members on loan.MemberNo equals member.MemberNo
                                              where lb.Companycode == companyCode && lb.Balance > 0
                                              select new
                                              {
                                                  loan.MemberNo,
                                                  loan.LoanNo,
                                                  loan.LoanCode,
                                                  loan.ApplicDate,
                                                  loan.RepayPeriod,
                                                  loan.LoanAmt,
                                                  loan.Aamount,
                                                  loan.AuditTime,
                                                  loan.CompanyCode,
                                                  MemberSurname = member.Surname,
                                                  MemberOtherNames = member.OtherNames,
                                                  MemberGigCode = member.Cigcode,
                                                  MemberFullName = member.FullName
                                              }).ToListAsync();
                loansWithMembers = loansWithBalance;
            }

            if (!loansWithMembers.Any())
            {
                var emptyViewModel = new LoansPerSaccoIndexViewModel
                {
                    CompletedLoans = new List<LoansPerSaccoReportViewModel>(),
                    IncompleteLoans = new List<LoansPerSaccoReportViewModel>(),
                    GigGroups = new List<CIGLoanSummary>(),
                    ReportDate = reportDate,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    UserCompanyCode = companyCode,
                    CompanyName = companyName,
                    TotalCompletedLoans = 0,
                    TotalIncompleteLoans = 0,
                    TotalLoans = 0,
                    TotalCompletedLoanAmount = 0,
                    TotalIncompleteLoanAmount = 0,
                    TotalOutstandingBalance = 0,
                    TotalLoanAmount = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found for the selected period.";

                return View("~/Views/Reports/LoansPerSacco.cshtml", emptyViewModel);
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            // Get repayment data for balances
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LatestBalance = g.OrderByDescending(r => r.DateReceived)
                                    .Select(r => r.LoanBalance)
                                    .FirstOrDefault() ?? 0,
                    TotalPaid = g.Sum(r => r.Amount) ?? 0,
                    TotalPrincipal = g.Sum(r => r.Principal) ?? 0,
                    TotalInterest = g.Sum(r => r.Interest) ?? 0,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get loan balances from Loanbals table as primary source
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var completedLoans = new List<LoansPerSaccoReportViewModel>();
            var incompleteLoans = new List<LoansPerSaccoReportViewModel>();

            // Process each loan and build the full name
            foreach (var loan in loansWithMembers)
            {
                // Get current balance - prioritize Loanbals table
                decimal currentBalance = 0;
                decimal totalPaid = 0;
                decimal principalPaid = 0;
                decimal interestPaid = 0;
                DateTime? lastPaymentDate = null;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo];
                }
                else if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    var repayment = latestRepayments[loan.LoanNo];
                    currentBalance = repayment.LatestBalance;
                    totalPaid = repayment.TotalPaid;
                    principalPaid = repayment.TotalPrincipal;
                    interestPaid = repayment.TotalInterest;
                    lastPaymentDate = repayment.LastPaymentDate;
                }
                else
                {
                    // Fallback to loan amount
                    currentBalance = loan.LoanAmt ?? loan.Aamount ?? 0;
                }

                // Skip if loan is fully paid (balance <= 0)
                if (currentBalance <= 0 && currentBalance > -0.01m)
                {
                    continue;
                }

                // Build full name from Surname and OtherNames
                string fullName = "N/A";
                var surname = loan.MemberSurname ?? "";
                var otherNames = loan.MemberOtherNames ?? "";

                if (!string.IsNullOrWhiteSpace(surname) || !string.IsNullOrWhiteSpace(otherNames))
                {
                    fullName = $"{surname} {otherNames}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                // Get the amount paid (original loan amount - current balance)
                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                decimal amountPaid = amountIssued - currentBalance;
                if (amountPaid < 0) amountPaid = 0;

                var loanViewModel = new LoansPerSaccoReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    LoanNo = loan.LoanNo,
                    FullName = fullName,
                    GigCode = loan.MemberGigCode ?? "",
                    LoanCode = loan.LoanCode ?? "-",
                    ApplicDate = loan.ApplicDate,
                    RepayPeriod = loan.RepayPeriod,
                    LoanAmt = amountIssued,
                    Balance = currentBalance,
                    PrincipalPaid = principalPaid,
                    InterestPaid = interestPaid,
                    TotalPaid = totalPaid,
                    LastPaymentDate = lastPaymentDate,
                    LoanStatus = currentBalance == 0 ? "COMPLETED" : "ACTIVE"
                };

                if (currentBalance == 0)
                {
                    completedLoans.Add(loanViewModel);
                }
                else
                {
                    incompleteLoans.Add(loanViewModel);
                }
            }

            // Group loans by GIG - Get GIG names from GIGs table using GigCode
            var gigGroups = new List<CIGLoanSummary>();

            // Get all unique GIG codes from the loans data (from member's Cigcode field)
            var gigCodes = loansWithMembers
                .Where(l => !string.IsNullOrEmpty(l.MemberGigCode))
                .Select(l => l.MemberGigCode)
                .Distinct()
                .ToList();

            // Get GIG details (names) from GIGs table using GigCode
            var gigDetails = await _context.CIGs
                .Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode && g.Status == "Active")
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            foreach (var gigCode in gigCodes)
            {
                // Get GIG name from the GIGs table, fallback to code if not found
                string gigName = gigDetails.ContainsKey(gigCode) ? gigDetails[gigCode] : gigCode;

                var gig = new CIGLoanSummary
                {
                    GigCode = gigCode,
                    GigName = gigName,
                    Loans = new List<CIGLoanDetail>(),
                    CompletedLoans = new List<CIGLoanDetail>(),
                    IncompleteLoans = new List<CIGLoanDetail>()
                };

                // Get loans for members in this GIG
                var gigLoans = loansWithMembers.Where(l => l.MemberGigCode == gigCode).ToList();

                foreach (var loan in gigLoans)
                {
                    decimal balance = 0;
                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        balance = loanBalances[loan.LoanNo];
                    }
                    else if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        balance = latestRepayments[loan.LoanNo].LatestBalance;
                    }
                    else
                    {
                        balance = loan.LoanAmt ?? loan.Aamount ?? 0;
                    }

                    // Build member name for GIG detail
                    string memberName = "N/A";
                    var surname = loan.MemberSurname ?? "";
                    var otherNames = loan.MemberOtherNames ?? "";

                    if (!string.IsNullOrWhiteSpace(surname) || !string.IsNullOrWhiteSpace(otherNames))
                    {
                        memberName = $"{surname} {otherNames}".Trim();
                        if (string.IsNullOrWhiteSpace(memberName))
                            memberName = "N/A";
                    }

                    var detail = new CIGLoanDetail
                    {
                        LoanNo = loan.LoanNo,
                        MemberNo = loan.MemberNo,
                        MemberName = memberName,
                        ApplicationDate = loan.ApplicDate,
                        LoanAmount = loan.LoanAmt ?? loan.Aamount ?? 0,
                        OutstandingBalance = balance,
                        Status = balance == 0 ? "COMPLETED" : "ACTIVE"
                    };

                    gig.Loans.Add(detail);

                    if (balance == 0)
                    {
                        gig.CompletedLoans.Add(detail);
                    }
                    else
                    {
                        gig.IncompleteLoans.Add(detail);
                    }
                }

                gig.CompletedLoanCount = gig.CompletedLoans.Count;
                gig.IncompleteLoanCount = gig.IncompleteLoans.Count;
                gig.TotalLoanAmount = gig.Loans.Sum(l => l.LoanAmount);
                gig.TotalOutstandingBalance = gig.Loans.Sum(l => l.OutstandingBalance);

                if (gig.Loans.Any())
                {
                    gigGroups.Add(gig);
                }
            }

            // Add unassigned members (no GIG code)
            var unassignedLoans = loansWithMembers.Where(l => string.IsNullOrEmpty(l.MemberGigCode)).ToList();

            if (unassignedLoans.Any())
            {
                var unassignedGig = new CIGLoanSummary
                {
                    GigCode = "UNASSIGNED",
                    GigName = "Unassigned Members",
                    Loans = new List<CIGLoanDetail>(),
                    CompletedLoans = new List<CIGLoanDetail>(),
                    IncompleteLoans = new List<CIGLoanDetail>()
                };

                foreach (var loan in unassignedLoans)
                {
                    decimal balance = 0;
                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        balance = loanBalances[loan.LoanNo];
                    }
                    else if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        balance = latestRepayments[loan.LoanNo].LatestBalance;
                    }
                    else
                    {
                        balance = loan.LoanAmt ?? loan.Aamount ?? 0;
                    }

                    // Build member name
                    string memberName = "N/A";
                    var surname = loan.MemberSurname ?? "";
                    var otherNames = loan.MemberOtherNames ?? "";

                    if (!string.IsNullOrWhiteSpace(surname) || !string.IsNullOrWhiteSpace(otherNames))
                    {
                        memberName = $"{surname} {otherNames}".Trim();
                        if (string.IsNullOrWhiteSpace(memberName))
                            memberName = "N/A";
                    }

                    var detail = new CIGLoanDetail
                    {
                        LoanNo = loan.LoanNo,
                        MemberNo = loan.MemberNo,
                        MemberName = memberName,
                        ApplicationDate = loan.ApplicDate,
                        LoanAmount = loan.LoanAmt ?? loan.Aamount ?? 0,
                        OutstandingBalance = balance,
                        Status = balance == 0 ? "COMPLETED" : "ACTIVE"
                    };

                    unassignedGig.Loans.Add(detail);

                    if (balance == 0)
                    {
                        unassignedGig.CompletedLoans.Add(detail);
                    }
                    else
                    {
                        unassignedGig.IncompleteLoans.Add(detail);
                    }
                }

                unassignedGig.CompletedLoanCount = unassignedGig.CompletedLoans.Count;
                unassignedGig.IncompleteLoanCount = unassignedGig.IncompleteLoans.Count;
                unassignedGig.TotalLoanAmount = unassignedGig.Loans.Sum(l => l.LoanAmount);
                unassignedGig.TotalOutstandingBalance = unassignedGig.Loans.Sum(l => l.OutstandingBalance);
                gigGroups.Add(unassignedGig);
            }

            gigGroups = gigGroups.OrderBy(g => g.GigCode).ToList();

            int totalCompletedLoans = completedLoans.Count;
            int totalIncompleteLoans = incompleteLoans.Count;

            decimal totalCompletedLoanAmount = completedLoans.Sum(l => l.LoanAmt);
            decimal totalIncompleteLoanAmount = incompleteLoans.Sum(l => l.LoanAmt);
            decimal totalOutstandingBalance = incompleteLoans.Sum(l => l.Balance);
            decimal totalLoanAmount = loansWithMembers.Sum(l => l.LoanAmt ?? l.Aamount ?? 0);

            var viewModel = new LoansPerSaccoIndexViewModel
            {
                CompletedLoans = completedLoans,
                IncompleteLoans = incompleteLoans,
                GigGroups = gigGroups,
                TotalCompletedLoans = totalCompletedLoans,
                TotalIncompleteLoans = totalIncompleteLoans,
                TotalLoans = totalCompletedLoans + totalIncompleteLoans,
                TotalCompletedLoanAmount = totalCompletedLoanAmount,
                TotalIncompleteLoanAmount = totalIncompleteLoanAmount,
                TotalOutstandingBalance = totalOutstandingBalance,
                TotalLoanAmount = totalLoanAmount,
                ReportDate = reportDate,
                StartDate = startDate,
                EndDate = endDate,
                HasData = completedLoans.Any() || incompleteLoans.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName
            };

            ViewBag.ReportDate = reportDate;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.TotalCompletedLoans = totalCompletedLoans;
            ViewBag.TotalIncompleteLoans = totalIncompleteLoans;
            ViewBag.TotalCompletedLoanAmount = totalCompletedLoanAmount;
            ViewBag.TotalIncompleteLoanAmount = totalIncompleteLoanAmount;
            ViewBag.TotalOutstandingBalance = totalOutstandingBalance;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoansPerSacco.cshtml", viewModel);
        }
        [HttpPost]
        public async Task<IActionResult> ExportLoansPerSaccoToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data using proper JOIN
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              && loan.ApplicDate >= startDate
                                              && loan.ApplicDate <= endDateAdjusted
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.CompanyCode,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberCigcode = member.Cigcode
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansPerSacco");
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            // Get repayment data for balances
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LatestBalance = g.OrderByDescending(r => r.DateReceived)
                                    .Select(r => r.LoanBalance)
                                    .FirstOrDefault() ?? 0
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get loan balances for loans without repayments
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            // Get GIG names
            var cigCodes = loansWithMembers
                .Where(l => !string.IsNullOrEmpty(l.MemberCigcode))
                .Select(l => l.MemberCigcode)
                .Distinct()
                .ToList();

            var gigDetails = await _context.CIGs
                .Where(g => cigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode && g.Status == "Active")
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            // Prepare data for the report (same structure as original)
            var reportData = loansWithMembers.Select(loan =>
            {
                // Get member name
                string fullName = "N/A";
                var surname = loan.MemberSurname ?? "";
                var otherNames = loan.MemberOtherNames ?? "";

                if (!string.IsNullOrWhiteSpace(surname) || !string.IsNullOrWhiteSpace(otherNames))
                {
                    fullName = $"{surname} {otherNames}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                // Get balance
                decimal balance = 0;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    balance = latestRepayments[loan.LoanNo].LatestBalance;
                }
                else if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    balance = loanBalances[loan.LoanNo];
                }

                // Get GIG name
                string gigCode = loan.MemberCigcode ?? "UNASSIGNED";
                string gigName = gigCode == "UNASSIGNED" ? "Unassigned" :
                                 (gigDetails.ContainsKey(gigCode) ? gigDetails[gigCode] : gigCode);

                return new
                {
                    loan.MemberNo,
                    loan.LoanNo,
                    FullName = fullName,
                    loan.LoanCode,
                    loan.ApplicDate,
                    loan.RepayPeriod,
                    loan.LoanAmt,
                    Balance = balance,
                    Status = balance == 0 ? "COMPLETED" : "ACTIVE",
                    GigCode = gigCode,
                    GigName = gigName
                };
            }).ToList();

            var completedLoans = reportData.Where(l => l.Status == "COMPLETED").ToList();
            var incompleteLoans = reportData.Where(l => l.Status == "ACTIVE").ToList();

            using var stream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Same page settings as original
                    page.Size(PageSizes.A4.Landscape());
                    page.MarginTop(1.5f, Unit.Centimetre);
                    page.MarginBottom(1.5f, Unit.Centimetre);
                    page.MarginLeft(1.2f, Unit.Centimetre);
                    page.MarginRight(1.2f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                    // Header - Same as original
                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.5f, Unit.Centimetre);
                        header.Item().AlignCenter().Text($"LOANS PER SACCO REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    // Content - Same structure as original
                    page.Content().Column(contentCol =>
                    {
                        // Summary Statistics - Same as original
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            summaryTable.Cell().Border(0.2f).Padding(4).Text("Total Loans:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                            summaryTable.Cell().Border(0.2f).Padding(4).Text("Completed Loans:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(completedLoans.Count.ToString());

                            summaryTable.Cell().Border(0.2f).Padding(4).Text("Loans in Progress:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(incompleteLoans.Count.ToString());
                            summaryTable.Cell().Border(0.2f).Padding(4).Text("Total Amount:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{reportData.Sum(l => l.LoanAmt ?? 0):N0}");

                            summaryTable.Cell().Border(0.2f).Padding(4).Text("Outstanding Balance:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{incompleteLoans.Sum(l => l.Balance):N0}");
                        });

                        // Completed Loans Section - Same as original
                        if (completedLoans.Any())
                        {
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("COMPLETED LOANS").FontSize(11).Bold();

                            contentCol.Item().Table(completedTable =>
                            {
                                completedTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.8f);
                                    cols.RelativeColumn(0.9f);
                                    cols.RelativeColumn(0.6f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.0f);
                                });

                                completedTable.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanCode").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Application Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Period").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(8);
                                });

                                foreach (var loan in completedLoans)
                                {
                                    completedTable.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).Text(loan.LoanCode ?? "-").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).Text(loan.FullName ?? "N/A").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicDate.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.RepayPeriod?.ToString() ?? "0").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanAmt:N0}").FontSize(7);
                                    completedTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Balance:N0}").FontSize(7);
                                }

                                completedTable.Cell().ColumnSpan(6).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(8);
                                completedTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{completedLoans.Sum(l => l.LoanAmt ?? 0):N0}").Bold().FontSize(8);
                                completedTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"0").Bold().FontSize(8);
                            });
                        }

                        // Incomplete Loans Section - Same as original
                        if (incompleteLoans.Any())
                        {
                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text("LOANS IN PROGRESS").FontSize(11).Bold();

                            contentCol.Item().Table(incompleteTable =>
                            {
                                incompleteTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.8f);
                                    cols.RelativeColumn(0.9f);
                                    cols.RelativeColumn(0.6f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.0f);
                                });

                                incompleteTable.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanCode").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Application Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Period").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(8);
                                });

                                foreach (var loan in incompleteLoans)
                                {
                                    incompleteTable.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).Text(loan.LoanCode ?? "-").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).Text(loan.FullName ?? "N/A").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicDate.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.RepayPeriod?.ToString() ?? "0").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanAmt:N0}").FontSize(7);
                                    incompleteTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Balance:N0}").FontSize(7);
                                }

                                incompleteTable.Cell().ColumnSpan(6).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(8);
                                incompleteTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{incompleteLoans.Sum(l => l.LoanAmt ?? 0):N0}").Bold().FontSize(8);
                                incompleteTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{incompleteLoans.Sum(l => l.Balance):N0}").Bold().FontSize(8);
                            });
                        }
                    });

                    // Footer - Same as original
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
            return File(content, "application/pdf", $"LoansPerSacco_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoansPerSaccoToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member data using proper JOIN
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          where loan.CompanyCode == companyCode
                                              && loan.ApplicDate >= startDate
                                              && loan.ApplicDate <= endDateAdjusted
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.CompanyCode,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansPerSacco");
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            // Get repayment data for balances
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LatestBalance = g.OrderByDescending(r => r.DateReceived)
                                    .Select(r => r.LoanBalance)
                                    .FirstOrDefault() ?? 0
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get loan balances for loans without repayments
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            // Prepare data for Excel (same structure as original)
            var completedLoans = new List<dynamic>();
            var incompleteLoans = new List<dynamic>();

            foreach (var loan in loansWithMembers)
            {
                decimal currentBalance = 0;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    currentBalance = latestRepayments[loan.LoanNo].LatestBalance;
                }
                else if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo];
                }

                // Build full name from Surname and OtherNames
                string fullName = "N/A";
                var surname = loan.MemberSurname ?? "";
                var otherNames = loan.MemberOtherNames ?? "";

                if (!string.IsNullOrWhiteSpace(surname) || !string.IsNullOrWhiteSpace(otherNames))
                {
                    fullName = $"{surname} {otherNames}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                        fullName = "N/A";
                }

                var loanData = new
                {
                    loan.MemberNo,
                    loan.LoanNo,
                    FullName = fullName,
                    LoanCode = loan.LoanCode ?? "-",
                    ApplicDate = loan.ApplicDate,
                    RepayPeriod = loan.RepayPeriod,
                    LoanAmt = loan.LoanAmt ?? 0,
                    Balance = currentBalance
                };

                if (currentBalance == 0)
                {
                    completedLoans.Add(loanData);
                }
                else
                {
                    incompleteLoans.Add(loanData);
                }
            }

            using (var workbook = new XLWorkbook())
            {
                // Summary Sheet
                var summarySheet = workbook.Worksheets.Add("Summary");
                int summaryRow = 1;

                summarySheet.Cell(summaryRow, 1).Value = companyName.ToUpper();
                summarySheet.Range(summaryRow, 1, summaryRow, 8).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                summarySheet.Range(summaryRow, 1, summaryRow, 8).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetItalic();
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"LOANS PER SACCO REPORT";
                summarySheet.Range(summaryRow, 1, summaryRow, 8).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                summarySheet.Range(summaryRow, 1, summaryRow, 8).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                // Summary Statistics
                summarySheet.Cell(summaryRow, 1).Value = "TOTAL LOANS:";
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Value = loansWithMembers.Count;
                summaryRow++;

                summarySheet.Cell(summaryRow, 1).Value = "COMPLETED LOANS:";
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Value = completedLoans.Count;
                summaryRow++;

                summarySheet.Cell(summaryRow, 1).Value = "LOANS IN PROGRESS:";
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Value = incompleteLoans.Count;
                summaryRow++;

                summarySheet.Cell(summaryRow, 1).Value = "TOTAL LOAN AMOUNT:";
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Value = loansWithMembers.Sum(l => l.LoanAmt ?? 0);
                summarySheet.Cell(summaryRow, 2).Style.NumberFormat.Format = "#,##0.00";
                summaryRow++;

                summarySheet.Cell(summaryRow, 1).Value = "OUTSTANDING BALANCE:";
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Value = incompleteLoans.Sum(l => (decimal)l.Balance);
                summarySheet.Cell(summaryRow, 2).Style.NumberFormat.Format = "#,##0.00";

                summarySheet.Columns().AdjustToContents();

                // Completed Loans Worksheet
                var completedWorksheet = workbook.Worksheets.Add("Completed Loans");
                var currentRow = 1;

                completedWorksheet.Cell(currentRow, 1).Value = $"COMPLETED LOANS";
                completedWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
                completedWorksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(16);
                completedWorksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                completedWorksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                completedWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
                completedWorksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                var headers = new[] { "MemberNo", "LoanNo", "Names", "LoanCode", "ApplicDate", "RepayPeriod", "LoanAmt", "Balance" };

                for (int i = 0; i < headers.Length; i++)
                {
                    completedWorksheet.Cell(currentRow, i + 1).Value = headers[i];
                    completedWorksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    completedWorksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    completedWorksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    completedWorksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                currentRow++;

                foreach (var loan in completedLoans)
                {
                    completedWorksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                    completedWorksheet.Cell(currentRow, 2).Value = loan.LoanNo;
                    completedWorksheet.Cell(currentRow, 3).Value = loan.FullName;
                    completedWorksheet.Cell(currentRow, 4).Value = loan.LoanCode;
                    completedWorksheet.Cell(currentRow, 5).Value = loan.ApplicDate?.ToString("dd/MM/yyyy");
                    completedWorksheet.Cell(currentRow, 6).Value = loan.RepayPeriod;
                    completedWorksheet.Cell(currentRow, 7).Value = loan.LoanAmt;
                    completedWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    completedWorksheet.Cell(currentRow, 8).Value = loan.Balance;
                    completedWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

                    completedWorksheet.Range(currentRow, 1, currentRow, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    currentRow++;
                }

                if (completedLoans.Any())
                {
                    currentRow += 2;
                    completedWorksheet.Cell(currentRow, 6).Value = "GRAND TOTAL:";
                    completedWorksheet.Cell(currentRow, 6).Style.Font.SetBold();
                    completedWorksheet.Cell(currentRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    completedWorksheet.Cell(currentRow, 7).Value = completedLoans.Sum(l => (decimal)l.LoanAmt);
                    completedWorksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    completedWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    completedWorksheet.Cell(currentRow, 8).Value = completedLoans.Sum(l => (decimal)l.Balance);
                    completedWorksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    completedWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                }

                completedWorksheet.Columns().AdjustToContents();

                // Incomplete Loans Worksheet
                var incompleteWorksheet = workbook.Worksheets.Add("Incomplete Loans");
                currentRow = 1;

                incompleteWorksheet.Cell(currentRow, 1).Value = $"LOANS IN PROGRESS";
                incompleteWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
                incompleteWorksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(16);
                incompleteWorksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                incompleteWorksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                incompleteWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
                incompleteWorksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                for (int i = 0; i < headers.Length; i++)
                {
                    incompleteWorksheet.Cell(currentRow, i + 1).Value = headers[i];
                    incompleteWorksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    incompleteWorksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    incompleteWorksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    incompleteWorksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                currentRow++;

                foreach (var loan in incompleteLoans)
                {
                    incompleteWorksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                    incompleteWorksheet.Cell(currentRow, 2).Value = loan.LoanNo;
                    incompleteWorksheet.Cell(currentRow, 3).Value = loan.FullName;
                    incompleteWorksheet.Cell(currentRow, 4).Value = loan.LoanCode;
                    incompleteWorksheet.Cell(currentRow, 5).Value = loan.ApplicDate?.ToString("dd/MM/yyyy");
                    incompleteWorksheet.Cell(currentRow, 6).Value = loan.RepayPeriod;
                    incompleteWorksheet.Cell(currentRow, 7).Value = loan.LoanAmt;
                    incompleteWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    incompleteWorksheet.Cell(currentRow, 8).Value = loan.Balance;
                    incompleteWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

                    incompleteWorksheet.Range(currentRow, 1, currentRow, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    currentRow++;
                }

                if (incompleteLoans.Any())
                {
                    currentRow += 2;
                    incompleteWorksheet.Cell(currentRow, 6).Value = "GRAND TOTAL:";
                    incompleteWorksheet.Cell(currentRow, 6).Style.Font.SetBold();
                    incompleteWorksheet.Cell(currentRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    incompleteWorksheet.Cell(currentRow, 7).Value = incompleteLoans.Sum(l => (decimal)l.LoanAmt);
                    incompleteWorksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    incompleteWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    incompleteWorksheet.Cell(currentRow, 8).Value = incompleteLoans.Sum(l => (decimal)l.Balance);
                    incompleteWorksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    incompleteWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                }

                incompleteWorksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"LoansPerSacco_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
                }
            }
        }
        #endregion

        #region Loans Issued Per Product Report

        [HttpGet]
        public IActionResult LoansIssuedPerProduct()
        {
            try
            {
                ViewBag.StartDate = DateTime.Now.AddMonths(-1);
                ViewBag.EndDate = DateTime.Now;
                ViewBag.HasData = false;

                var viewModel = new LoanIssuedPerProductIndexViewModel
                {
                    Groups = new List<LoanIssuedPerProductGroupViewModel>(),
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now
                };

                return View("~/Views/Reports/LoansIssuedPerProduct.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Loans Issued Per Product Report");
                TempData["ErrorMessage"] = "Error loading report: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoansIssuedPerProduct(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                // Validate dates
                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "Start date cannot be greater than end date.";
                    return RedirectToAction("LoansIssuedPerProduct");
                }

                DateTime endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Get loans with member and appraisal data using proper JOINs
                var loansWithDetails = await (from loan in _context.Loans
                                              join member in _context.Members
                                                  on loan.MemberNo equals member.MemberNo into memberJoin
                                              from m in memberJoin.DefaultIfEmpty()
                                              join loantype in _context.Loantypes
                                                  on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                              from lt in loanTypeJoin.DefaultIfEmpty()
                                              where loan.CompanyCode == companyCode
                                                  && loan.AuditTime >= startDate
                                                  && loan.AuditTime <= endDateAdjusted
                                              select new
                                              {
                                                  loan.MemberNo,
                                                  loan.LoanNo,
                                                  loan.LoanCode,
                                                  loan.ApplicDate,
                                                  loan.AuditTime,
                                                  loan.RepayPeriod,
                                                  loan.LoanAmt,
                                                  loan.Aamount,
                                                  loan.Interest,
                                                  loan.Status,
                                                  MemberSurname = m != null ? m.Surname : null,
                                                  MemberOtherNames = m != null ? m.OtherNames : null,
                                                  MemberFullName = m != null ? m.FullName : null,
                                                  LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                              }).ToListAsync();

                if (!loansWithDetails.Any())
                {
                    var emptyViewModel = new LoanIssuedPerProductIndexViewModel
                    {
                        Groups = new List<LoanIssuedPerProductGroupViewModel>(),
                        ValueChainGroups = new List<ValueChainSummaryViewModel>(),
                        StartDate = startDate,
                        EndDate = endDate,
                        HasData = false,
                        CompanyName = companyName,
                        PrintedBy = printedBy,
                        GeneratedOn = DateTime.Now,
                        TotalLoans = 0,
                        TotalLoanApplied = 0,
                        TotalApprovedAmount = 0,
                        TotalLoanTypes = 0,
                        TotalValueChains = 0
                    };

                    ViewBag.StartDate = startDate;
                    ViewBag.EndDate = endDate;
                    ViewBag.HasData = false;
                    TempData["InfoMessage"] = "No loans found for the selected date range.";

                    return View("~/Views/Reports/LoansIssuedPerProduct.cshtml", emptyViewModel);
                }

                // Get appraisal dates for each loan
                var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();
                var appraisals = new Dictionary<string, (DateTime? AppraisDate, string AuditID)>();
                try
                {
                    appraisals = await _context.Appraisal
                        .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                        .Select(a => new { a.LoanNo, a.AppraisDate, a.AuditID })
                        .ToDictionaryAsync(a => a.LoanNo, a => (a.AppraisDate, a.AuditID));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading appraisals, continuing without them");
                }

                // Build member names using LoanNo as key to avoid duplicates
                var memberNames = loansWithDetails
                    .GroupBy(l => l.LoanNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.LoanNo,
                        l => !string.IsNullOrEmpty(l.MemberFullName)
                            ? l.MemberFullName
                            : $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Also create a dictionary for MemberNo to Name for display
                var memberNameByMemberNo = loansWithDetails
                    .GroupBy(l => l.MemberNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.MemberNo,
                        l => !string.IsNullOrEmpty(l.MemberFullName)
                            ? l.MemberFullName
                            : $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Group by LoanTypeName (Loan Name from Loantype1)
                var groupedByLoanType = loansWithDetails
                    .GroupBy(l => l.LoanTypeName)
                    .OrderBy(g => g.Key)
                    .Select((g, groupIndex) => new LoanIssuedPerProductGroupViewModel
                    {
                        ValueChain = "",
                        LoanType = g.Key,
                        LoanCode = g.First().LoanCode ?? "",
                        Loans = g.Select((l, index) => new LoanIssuedPerProductReportViewModel
                        {
                            No = index + 1,
                            MemberNo = l.MemberNo,
                            LoanNo = l.LoanNo,
                            Name = memberNameByMemberNo.ContainsKey(l.MemberNo) ? memberNameByMemberNo[l.MemberNo] : "N/A",
                            ApplicationDate = l.ApplicDate,
                            AppraisalDate = appraisals.ContainsKey(l.LoanNo) ? appraisals[l.LoanNo].AppraisDate : null,
                            EndorsementDate = l.AuditTime,
                            DateIssued = l.AuditTime,
                            LoanPeriodMonths = l.RepayPeriod ?? 0,
                            LoanApplied = l.LoanAmt ?? 0,
                            ApprovedAmount = l.Aamount ?? l.LoanAmt ?? 0,
                            InterestRate = l.Interest,
                            LoanType = l.LoanTypeName,
                            LoanCode = l.LoanCode ?? ""
                        }).ToList(),
                        Count = g.Count(),
                        TotalLoanApplied = g.Sum(x => x.LoanAmt ?? 0),
                        TotalApprovedAmount = g.Sum(x => x.Aamount ?? x.LoanAmt ?? 0)
                    })
                    .ToList();

                // Calculate overall totals
                int totalDisbursedLoans = loansWithDetails.Count(l => l.Status == (int)Status.Disbursed);
                int totalApprovedLoans = loansWithDetails.Count(l => l.Status == (int)Status.Approved || l.Status == (int)Status.Endorsed);
                decimal totalDisbursedAmount = loansWithDetails.Where(l => l.Status == (int)Status.Disbursed).Sum(l => l.Aamount ?? l.LoanAmt ?? 0);

                var viewModel = new LoanIssuedPerProductIndexViewModel
                {
                    Groups = groupedByLoanType,
                    ValueChainGroups = new List<ValueChainSummaryViewModel>(),
                    TotalLoans = loansWithDetails.Count,
                    TotalLoanApplied = loansWithDetails.Sum(l => l.LoanAmt ?? 0),
                    TotalApprovedAmount = loansWithDetails.Sum(l => l.Aamount ?? l.LoanAmt ?? 0),
                    TotalLoanTypes = groupedByLoanType.Count,
                    TotalValueChains = 0,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = loansWithDetails.Any(),
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalDisbursedLoans = totalDisbursedLoans,
                    TotalApprovedLoans = totalApprovedLoans,
                    TotalDisbursedAmount = totalDisbursedAmount
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = loansWithDetails.Any();

                return View("~/Views/Reports/LoansIssuedPerProduct.cshtml", viewModel);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL Error in LoansIssuedPerProduct");
                TempData["ErrorMessage"] = "Database connection error. Please try again.";
                return RedirectToAction("LoansIssuedPerProduct");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoansIssuedPerProduct");
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;
                return RedirectToAction("LoansIssuedPerProduct");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoansIssuedPerProductToExcel(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";
                DateTime endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Get loans with member data
                var loansWithMembers = await (from loan in _context.Loans
                                              join member in _context.Members
                                                  on loan.MemberNo equals member.MemberNo into memberJoin
                                              from m in memberJoin.DefaultIfEmpty()
                                              where loan.CompanyCode == companyCode
                                                  && loan.AuditTime >= startDate
                                                  && loan.AuditTime <= endDateAdjusted
                                              select new
                                              {
                                                  loan.MemberNo,
                                                  loan.LoanNo,
                                                  loan.LoanCode,
                                                  loan.ApplicDate,
                                                  loan.AuditTime,
                                                  loan.RepayPeriod,
                                                  loan.LoanAmt,
                                                  loan.Aamount,
                                                  loan.Interest,
                                                  loan.Status,
                                                  MemberSurname = m != null ? m.Surname : null,
                                                  MemberOtherNames = m != null ? m.OtherNames : null
                                              }).ToListAsync();

                if (!loansWithMembers.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("LoansIssuedPerProduct");
                }

                // Get loan types with LoanType1 information
                var loanCodes = loansWithMembers.Select(l => l.LoanCode).Distinct().ToList();
                var loanTypes = new Dictionary<string, string>();
                try
                {
                    loanTypes = await _context.Loantypes
                        .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                        .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1 ?? (lt.LoanCode ?? "Unknown"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading loan types");
                }

                // Build member names using LoanNo as key
                var memberNames = loansWithMembers
                    .GroupBy(l => l.LoanNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.LoanNo,
                        l => $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Also create a dictionary for MemberNo to Name
                var memberNameByMemberNo = loansWithMembers
                    .GroupBy(l => l.MemberNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.MemberNo,
                        l => $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Group by LoanType1 only
                var groupedData = loansWithMembers
                    .GroupBy(l => new
                    {
                        LoanCode = l.LoanCode,
                        LoanTypeName = loanTypes.ContainsKey(l.LoanCode) ? loanTypes[l.LoanCode] : (l.LoanCode ?? "Unknown")
                    })
                    .OrderBy(g => g.Key.LoanTypeName)
                    .Select(g => new
                    {
                        LoanType = g.Key.LoanTypeName,
                        LoanCode = g.Key.LoanCode,
                        Loans = g.Select((l, index) => new
                        {
                            No = index + 1,
                            l.MemberNo,
                            l.LoanNo,
                            Name = memberNameByMemberNo.ContainsKey(l.MemberNo) ? memberNameByMemberNo[l.MemberNo] : "N/A",
                            l.ApplicDate,
                            l.AuditTime,
                            l.RepayPeriod,
                            l.LoanAmt,
                            l.Aamount,
                            l.Interest,
                            l.Status
                        }).ToList(),
                        TotalLoanApplied = g.Sum(x => x.LoanAmt ?? 0),
                        TotalApprovedAmount = g.Sum(x => x.Aamount ?? x.LoanAmt ?? 0),
                        Count = g.Count()
                    })
                    .ToList();

                using var workbook = new XLWorkbook();

                // Summary Sheet
                var summarySheet = workbook.Worksheets.Add("Summary by Loan Type");
                int summaryRow = 1;

                summarySheet.Cell(summaryRow, 1).Value = companyName.ToUpper();
                summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetItalic();
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"LOANS ISSUED PER PRODUCT";
                summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                summarySheet.Cell(summaryRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                summarySheet.Range(summaryRow, 1, summaryRow, 7).Merge();
                summarySheet.Cell(summaryRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                summarySheet.Cell(summaryRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summaryRow += 2;

                string[] summaryHeaders = { "Loan Type", "Loan Code", "Count", "Total Applied (KES)", "Total Approved (KES)", "Status", "Disbursed (KES)" };
                for (int i = 0; i < summaryHeaders.Length; i++)
                {
                    summarySheet.Cell(summaryRow, i + 1).Value = summaryHeaders[i];
                    summarySheet.Cell(summaryRow, i + 1).Style.Font.SetBold();
                    summarySheet.Cell(summaryRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    summarySheet.Cell(summaryRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                summaryRow++;

                foreach (var group in groupedData)
                {
                    int disbursedCount = group.Loans.Count(l => l.Status == (int)Status.Disbursed);
                    decimal disbursedAmount = group.Loans.Where(l => l.Status == (int)Status.Disbursed).Sum(l => l.Aamount ?? l.LoanAmt ?? 0);

                    summarySheet.Cell(summaryRow, 1).Value = group.LoanType;
                    summarySheet.Cell(summaryRow, 2).Value = group.LoanCode;
                    summarySheet.Cell(summaryRow, 3).Value = group.Count;
                    summarySheet.Cell(summaryRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    summarySheet.Cell(summaryRow, 4).Value = group.TotalLoanApplied;
                    summarySheet.Cell(summaryRow, 4).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(summaryRow, 5).Value = group.TotalApprovedAmount;
                    summarySheet.Cell(summaryRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(summaryRow, 6).Value = $"{disbursedCount}/{group.Count}";
                    summarySheet.Cell(summaryRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    summarySheet.Cell(summaryRow, 7).Value = disbursedAmount;
                    summarySheet.Cell(summaryRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    summaryRow++;
                }

                // Grand Total
                summaryRow++;
                summarySheet.Cell(summaryRow, 2).Value = "GRAND TOTAL:";
                summarySheet.Cell(summaryRow, 2).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                summarySheet.Cell(summaryRow, 3).Value = groupedData.Sum(g => g.Count);
                summarySheet.Cell(summaryRow, 3).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 4).Value = groupedData.Sum(g => g.TotalLoanApplied);
                summarySheet.Cell(summaryRow, 4).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 4).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(summaryRow, 5).Value = groupedData.Sum(g => g.TotalApprovedAmount);
                summarySheet.Cell(summaryRow, 5).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 5).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(summaryRow, 7).Value = groupedData.Sum(g => g.Loans.Where(l => l.Status == (int)Status.Disbursed).Sum(l => l.Aamount ?? l.LoanAmt ?? 0));
                summarySheet.Cell(summaryRow, 7).Style.Font.SetBold();
                summarySheet.Cell(summaryRow, 7).Style.NumberFormat.Format = "#,##0.00";

                summarySheet.Columns().AdjustToContents();

                // Detailed Worksheet - All Loans
                string[] detailHeaders = { "No.", "MemberNo", "LoanNo", "MemberName", "Applic Date", "Date Issued", "Period", "Loan Applied (KES)", "Approved Amt (KES)", "Interest Rate", "Loan Type", "Status" };

                var detailSheet = workbook.Worksheets.Add("Detailed Loans");
                int currentRow = 1;

                detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                detailSheet.Range(currentRow, 1, currentRow, detailHeaders.Length).Merge();
                detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                detailSheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                detailSheet.Range(currentRow, 1, currentRow, detailHeaders.Length).Merge();
                detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                for (int i = 0; i < detailHeaders.Length; i++)
                {
                    detailSheet.Cell(currentRow, i + 1).Value = detailHeaders[i];
                    detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    detailSheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                currentRow++;

                int globalSerialNo = 1;
                foreach (var group in groupedData)
                {
                    // Group header row
                    detailSheet.Cell(currentRow, 1).Value = $">>> {group.LoanType} ({group.LoanCode})";
                    detailSheet.Range(currentRow, 1, currentRow, detailHeaders.Length).Merge();
                    detailSheet.Cell(currentRow, 1).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, 1).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    currentRow++;

                    foreach (var loan in group.Loans)
                    {
                        string statusText = GetLoanStatusString(loan.Status);

                        detailSheet.Cell(currentRow, 1).Value = globalSerialNo++;
                        detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        detailSheet.Cell(currentRow, 2).Value = loan.MemberNo;
                        detailSheet.Cell(currentRow, 3).Value = loan.LoanNo;
                        detailSheet.Cell(currentRow, 4).Value = loan.Name;
                        detailSheet.Cell(currentRow, 5).Value = loan.ApplicDate.ToString("dd/MM/yyyy");
                        detailSheet.Cell(currentRow, 6).Value = loan.AuditTime.ToString("dd/MM/yyyy") ?? "";
                        detailSheet.Cell(currentRow, 7).Value = loan.RepayPeriod;
                        detailSheet.Cell(currentRow, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        detailSheet.Cell(currentRow, 8).Value = loan.LoanAmt ?? 0;
                        detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                        detailSheet.Cell(currentRow, 9).Value = loan.Aamount ?? loan.LoanAmt ?? 0;
                        detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                        detailSheet.Cell(currentRow, 10).Value = loan.Interest > 0 ? $"{loan.Interest}%" : "";
                        detailSheet.Cell(currentRow, 11).Value = group.LoanType;
                        detailSheet.Cell(currentRow, 12).Value = statusText;

                        for (int i = 1; i <= detailHeaders.Length; i++)
                        {
                            detailSheet.Cell(currentRow, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        }
                        currentRow++;
                    }

                    // Subtotal row for this loan type
                    int disbursedCount = group.Loans.Count(l => l.Status == (int)Status.Disbursed);
                    decimal disbursedAmount = group.Loans.Where(l => l.Status == (int)Status.Disbursed).Sum(l => l.Aamount ?? l.LoanAmt ?? 0);

                    detailSheet.Cell(currentRow, 7).Value = $"{group.LoanType} SUBTOTAL:";
                    detailSheet.Cell(currentRow, 7).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    detailSheet.Cell(currentRow, 8).Value = group.TotalLoanApplied;
                    detailSheet.Cell(currentRow, 8).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 9).Value = group.TotalApprovedAmount;
                    detailSheet.Cell(currentRow, 9).Style.Font.SetBold();
                    detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 12).Value = $"Disbursed: {disbursedCount} / {group.Count}";
                    currentRow++;
                }

                // Grand Total
                currentRow++;
                detailSheet.Cell(currentRow, 7).Value = "GRAND TOTAL:";
                detailSheet.Cell(currentRow, 7).Style.Font.SetBold();
                detailSheet.Cell(currentRow, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                detailSheet.Cell(currentRow, 8).Value = groupedData.Sum(g => g.TotalLoanApplied);
                detailSheet.Cell(currentRow, 8).Style.Font.SetBold();
                detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 9).Value = groupedData.Sum(g => g.TotalApprovedAmount);
                detailSheet.Cell(currentRow, 9).Style.Font.SetBold();
                detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

                detailSheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"LoansIssuedPerProduct_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Loans Issued Per Product to Excel");
                TempData["ErrorMessage"] = "Error exporting to Excel: " + ex.Message;
                return RedirectToAction("LoansIssuedPerProduct");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoansIssuedPerProductToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";
                DateTime endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Get loans with member data
                var loansWithMembers = await (from loan in _context.Loans
                                              join member in _context.Members
                                                  on loan.MemberNo equals member.MemberNo into memberJoin
                                              from m in memberJoin.DefaultIfEmpty()
                                              where loan.CompanyCode == companyCode
                                                  && loan.AuditTime >= startDate
                                                  && loan.AuditTime <= endDateAdjusted
                                              select new
                                              {
                                                  loan.MemberNo,
                                                  loan.LoanNo,
                                                  loan.LoanCode,
                                                  loan.ApplicDate,
                                                  loan.AuditTime,
                                                  loan.RepayPeriod,
                                                  loan.LoanAmt,
                                                  loan.Aamount,
                                                  loan.Interest,
                                                  loan.Status,
                                                  MemberSurname = m != null ? m.Surname : null,
                                                  MemberOtherNames = m != null ? m.OtherNames : null
                                              }).ToListAsync();

                if (!loansWithMembers.Any())
                {
                    TempData["Error"] = "No data found for the selected date range";
                    return RedirectToAction("LoansIssuedPerProduct");
                }

                // Get loan types with LoanType1 information
                var loanCodes = loansWithMembers.Select(l => l.LoanCode).Distinct().ToList();
                var loanTypes = new Dictionary<string, string>();
                try
                {
                    loanTypes = await _context.Loantypes
                        .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                        .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1 ?? (lt.LoanCode ?? "Unknown"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading loan types");
                }

                // Build member names using LoanNo as key
                var memberNames = loansWithMembers
                    .GroupBy(l => l.LoanNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.LoanNo,
                        l => $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Also create a dictionary for MemberNo to Name
                var memberNameByMemberNo = loansWithMembers
                    .GroupBy(l => l.MemberNo)
                    .Select(g => g.First())
                    .ToDictionary(
                        l => l.MemberNo,
                        l => $"{l.MemberSurname ?? ""} {l.MemberOtherNames ?? ""}".Trim()
                    );

                // Group by LoanType1 only
                var groupedData = loansWithMembers
                    .GroupBy(l => new
                    {
                        LoanCode = l.LoanCode,
                        LoanTypeName = loanTypes.ContainsKey(l.LoanCode) ? loanTypes[l.LoanCode] : (l.LoanCode ?? "Unknown")
                    })
                    .OrderBy(g => g.Key.LoanTypeName)
                    .Select(g => new
                    {
                        LoanType = g.Key.LoanTypeName,
                        LoanCode = g.Key.LoanCode,
                        Loans = g.Select((l, index) => new LoanIssuedPerProductReportViewModel
                        {
                            No = index + 1,
                            MemberNo = l.MemberNo,
                            LoanNo = l.LoanNo,
                            Name = memberNameByMemberNo.ContainsKey(l.MemberNo) ? memberNameByMemberNo[l.MemberNo] : "N/A",
                            ApplicationDate = l.ApplicDate,
                            DateIssued = l.AuditTime,
                            LoanPeriodMonths = l.RepayPeriod ?? 0,
                            LoanApplied = l.LoanAmt ?? 0,
                            ApprovedAmount = l.Aamount ?? l.LoanAmt ?? 0,
                            InterestRate = l.Interest,
                            LoanType = g.Key.LoanTypeName,
                            LoanCode = g.Key.LoanCode,
                            Status = GetLoanStatusString(l.Status)
                        }).ToList(),
                        TotalLoanApplied = g.Sum(x => x.LoanAmt ?? 0),
                        TotalApprovedAmount = g.Sum(x => x.Aamount ?? x.LoanAmt ?? 0),
                        Count = g.Count()
                    })
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
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.5f, Unit.Centimetre);
                            header.Item().AlignCenter().Text($"LOANS ISSUED PER PRODUCT").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Column(contentCol =>
                        {
                            // Summary Table by Loan Type
                            contentCol.Item().Table(summaryTable =>
                            {
                                summaryTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.5f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(0.6f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                });

                                summaryTable.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Type").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Code").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Count").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Applied").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Approved").Bold().FontSize(8);
                                });

                                foreach (var group in groupedData)
                                {
                                    summaryTable.Cell().Border(0.2f).Padding(4).Text(group.LoanType).FontSize(8);
                                    summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(group.LoanCode).FontSize(8);
                                    summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(group.Count.ToString()).FontSize(8);
                                    summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{group.TotalLoanApplied:N0}").FontSize(8);
                                    summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{group.TotalApprovedAmount:N0}").FontSize(8);
                                }

                                // Grand Total
                                summaryTable.Cell().ColumnSpan(2).Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(9);
                                summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignCenter().Text(groupedData.Sum(g => g.Count).ToString()).Bold().FontSize(9);
                                summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{groupedData.Sum(g => g.TotalLoanApplied):N0}").Bold().FontSize(9);
                                summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{groupedData.Sum(g => g.TotalApprovedAmount):N0}").Bold().FontSize(9);
                            });

                            // Detailed tables by Loan Type
                            foreach (var group in groupedData)
                            {
                                contentCol.Item().PaddingTop(1, Unit.Centimetre);
                                contentCol.Item().Text($"{group.LoanType} ({group.LoanCode})").FontSize(11).Bold();

                                contentCol.Item().Table(detailTable =>
                                {
                                    detailTable.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(0.4f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.2f);
                                        cols.RelativeColumn(1.6f);
                                        cols.RelativeColumn(0.9f);
                                        cols.RelativeColumn(0.9f);
                                        cols.RelativeColumn(0.5f);
                                        cols.RelativeColumn(1.0f);
                                        cols.RelativeColumn(1.0f);
                                    });

                                    detailTable.Header(header =>
                                    {
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("No").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Name").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Applic Date").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Issued").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Period").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Applied (KES)").Bold().FontSize(8);
                                        header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Approved (KES)").Bold().FontSize(8);
                                    });

                                    int seqNo = 1;
                                    foreach (var loan in group.Loans)
                                    {
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text((seqNo++).ToString()).FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).Text(loan.Name ?? "").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplicationDate?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DateIssued?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.LoanPeriodMonths.ToString()).FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanApplied:N0}").FontSize(7);
                                        detailTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.ApprovedAmount:N0}").FontSize(7);
                                    }

                                    detailTable.Cell().ColumnSpan(7).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(8);
                                    detailTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalLoanApplied:N0}").Bold().FontSize(8);
                                    detailTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalApprovedAmount:N0}").Bold().FontSize(8);
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

                var content = stream.ToArray();
                return File(content, "application/pdf", $"LoansIssuedPerProduct_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Loans Issued Per Product to PDF");
                TempData["ErrorMessage"] = "Error exporting to PDF: " + ex.Message;
                return RedirectToAction("LoansIssuedPerProduct");
            }
        }


        #endregion

        #region Aging Analysis Report

        [HttpGet]
        public IActionResult AgingAnalysis()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new AgingAnalysisIndexViewModel
            {
                Loans = new List<AgingAnalysisViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalLoans = 0,
                TotalLoanBalance = 0,
                TotalPerforming = 0,
                TotalSpecialMention = 0,
                TotalWatchful = 0,
                TotalSubstandard = 0,
                TotalDoubtful = 0,
                TotalLoss = 0,
                TotalLossOver365 = 0,
                PerformingCount = 0,
                SpecialMentionCount = 0,
                WatchfulCount = 0,
                SubstandardCount = 0,
                DoubtfulCount = 0,
                LossCount = 0,
                LossOver365Count = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AgingAnalysis(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                          && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                          && loan.AuditTime <= asAtDateEnd
                                          && (loan.LoanAmt > 0 || loan.Aamount > 0)
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.Interest,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                              InterestRateFromType = lt != null ? lt.Interest : null
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                var emptyViewModel = CreateEmptyViewModel(asAtDate, companyCode, companyName);
                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                return View("~/Views/Reports/AgingAnalysis.cshtml", emptyViewModel);
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            // Get latest repayment for each loan
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get loan balances from Loanbal table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var agingLoans = new List<AgingAnalysisViewModel>();
            decimal totalLoanBalance = 0;
            decimal totalPerforming = 0, totalSpecialMention = 0, totalWatchful = 0;
            decimal totalSubstandard = 0, totalDoubtful = 0, totalLoss = 0, totalLossOver365 = 0;
            int performingCount = 0, specialMentionCount = 0, watchfulCount = 0;
            int substandardCount = 0, doubtfulCount = 0, lossCount = 0, lossOver365Count = 0;

            foreach (var loan in loansWithMembers)
            {
                // Get current balance
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo)
                    ? loanBalances[loan.LoanNo]
                    : (loan.Aamount ?? loan.LoanAmt ?? 0);

                // Skip if loan is fully paid
                if (currentBalance <= 0) continue;

                // Get last payment date
                DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo)
                    ? latestRepayments[loan.LoanNo].LastPaymentDate
                    : null;

                // ========== CORRECTED DAYS IN ARREARS CALCULATION ==========
                int daysInArrears = CalculateDaysInArrears(loan, lastPaymentDate, asAtDate);

                // ========== CORRECTED: ALLOCATE THE FULL BALANCE TO ONE CATEGORY ==========
                int category = GetCategoryFromDays(daysInArrears);

                // Initialize all category amounts to 0
                decimal performingAmt = 0;
                decimal specialMentionAmt = 0;
                decimal watchfulAmt = 0;
                decimal substandardAmt = 0;
                decimal doubtfulAmt = 0;
                decimal lossAmt = 0;
                decimal lossOver365Amt = 0;

                // Put the ENTIRE LOAN BALANCE into the appropriate category ONLY
                switch (category)
                {
                    case 1: // Performing (0 days)
                        performingAmt = currentBalance;
                        performingCount++;
                        break;
                    case 2: // Special Mention (1-30 days)
                        specialMentionAmt = currentBalance;
                        specialMentionCount++;
                        break;
                    case 3: // Watchful (31-60 days)
                        watchfulAmt = currentBalance;
                        watchfulCount++;
                        break;
                    case 4: // Substandard (61-90 days)
                        substandardAmt = currentBalance;
                        substandardCount++;
                        break;
                    case 5: // Doubtful (91-180 days)
                        doubtfulAmt = currentBalance;
                        doubtfulCount++;
                        break;
                    case 6: // Loss (181-365 days)
                        lossAmt = currentBalance;
                        lossCount++;
                        break;
                    case 7: // Loss Over 365 days
                        lossOver365Amt = currentBalance;
                        lossOver365Count++;
                        break;
                }

                // Add to totals
                totalPerforming += performingAmt;
                totalSpecialMention += specialMentionAmt;
                totalWatchful += watchfulAmt;
                totalSubstandard += substandardAmt;
                totalDoubtful += doubtfulAmt;
                totalLoss += lossAmt;
                totalLossOver365 += lossOver365Amt;

                // Calculate next due date
                DateTime? nextDueDate = CalculateNextDueDate(loan, lastPaymentDate, asAtDate);

                // Calculate date of completion
                DateTime? dateOfCompletion = null;
                int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;
                if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0)
                {
                    dateOfCompletion = loan.AuditTime.AddMonths(repaymentPeriodMonths.Value);
                }

                // Build member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                var agingItem = new AgingAnalysisViewModel
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    FullName = fullName,
                    LoanBalance = currentBalance,
                    RepayPeriod = repaymentPeriodMonths,
                    NextDueDate = nextDueDate,
                    DateIssued = loan.AuditTime,
                    DaysInArrears = daysInArrears,
                    LastRepayDate = lastPaymentDate,
                    DateOfCompletion = dateOfCompletion,
                    ValueChain = loan.LoanName ?? "Unknown",
                    Performing = performingAmt,
                    SpecialMention = specialMentionAmt,
                    Watchful = watchfulAmt,
                    Substandard = substandardAmt,
                    Doubtful = doubtfulAmt,
                    Loss = lossAmt,
                    LossOver365 = lossOver365Amt,
                    Classification = GetCategoryName(category),
                    ArrearsCategory = category
                };

                totalLoanBalance += currentBalance;
                agingLoans.Add(agingItem);
            }

            var viewModel = new AgingAnalysisIndexViewModel
            {
                Loans = agingLoans.OrderBy(l => l.DaysInArrears).ToList(),
                AsAtDate = asAtDate,
                HasData = agingLoans.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalLoans = agingLoans.Count,
                TotalLoanBalance = totalLoanBalance,
                TotalPerforming = totalPerforming,
                TotalSpecialMention = totalSpecialMention,
                TotalWatchful = totalWatchful,
                TotalSubstandard = totalSubstandard,
                TotalDoubtful = totalDoubtful,
                TotalLoss = totalLoss,
                TotalLossOver365 = totalLossOver365,
                PerformingCount = performingCount,
                SpecialMentionCount = specialMentionCount,
                WatchfulCount = watchfulCount,
                SubstandardCount = substandardCount,
                DoubtfulCount = doubtfulCount,
                LossCount = lossCount,
                LossOver365Count = lossOver365Count
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
        }

        // Helper method to calculate days in arrears
        private int CalculateDaysInArrears(dynamic loan, DateTime? lastPaymentDate, DateTime asAtDate)
        {
            int daysInArrears = 0;

            if (lastPaymentDate.HasValue)
            {
                var nextDueDate = lastPaymentDate.Value.AddMonths(1);
                if (asAtDate > nextDueDate)
                {
                    daysInArrears = (asAtDate - nextDueDate).Days;
                }
            }
            else
            {
                // No payments made - check when first payment was due
                DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                if (asAtDate > firstDueDate)
                {
                    daysInArrears = (asAtDate - firstDueDate).Days;
                }
            }

            return daysInArrears < 0 ? 0 : daysInArrears;
        }

        // Helper method to calculate next due date
        private DateTime? CalculateNextDueDate(dynamic loan, DateTime? lastPaymentDate, DateTime asAtDate)
        {
            if (lastPaymentDate.HasValue)
            {
                return lastPaymentDate.Value.AddMonths(1);
            }
            else
            {
                // Calculate the expected due date based on issuance date
                DateTime firstDueDate = loan.AuditTime.AddMonths(1);

                // If we're past the first due date, calculate how many months have passed
                if (asAtDate > firstDueDate)
                {
                    int monthsPassed = ((asAtDate.Year - loan.AuditTime.Year) * 12) +
                                       (asAtDate.Month - loan.AuditTime.Month);
                    if (monthsPassed > 0)
                    {
                        return loan.AuditTime.AddMonths(monthsPassed + 1);
                    }
                }

                return firstDueDate;
            }
        }

        private int GetCategoryFromDays(int daysInArrears)
        {
            if (daysInArrears <= 0) return 1;      // Performing
            if (daysInArrears <= 30) return 2;     // Special Mention
            if (daysInArrears <= 60) return 3;     // Watchful
            if (daysInArrears <= 90) return 4;     // Substandard
            if (daysInArrears <= 180) return 5;    // Doubtful
            if (daysInArrears <= 365) return 6;    // Loss
            return 7;                               // Loss Over 365
        }

        private string GetCategoryName(int category)
        {
            return category switch
            {
                1 => "Performing",
                2 => "Special Mention",
                3 => "Watchful",
                4 => "Substandard",
                5 => "Doubtful",
                6 => "Loss",
                7 => "Loss Over 365",
                _ => "Unknown"
            };
        }

        private AgingAnalysisIndexViewModel CreateEmptyViewModel(DateTime asAtDate, string companyCode, string companyName)
        {
            return new AgingAnalysisIndexViewModel
            {
                Loans = new List<AgingAnalysisViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalLoans = 0,
                TotalLoanBalance = 0,
                TotalPerforming = 0,
                TotalSpecialMention = 0,
                TotalWatchful = 0,
                TotalSubstandard = 0,
                TotalDoubtful = 0,
                TotalLoss = 0,
                TotalLossOver365 = 0,
                PerformingCount = 0,
                SpecialMentionCount = 0,
                WatchfulCount = 0,
                SubstandardCount = 0,
                DoubtfulCount = 0,
                LossCount = 0,
                LossOver365Count = 0
            };
        }

        //[HttpPost]
        //public async Task<IActionResult> AgingAnalysis(DateTime asAtDate)
        //{
        //    var companyCode = User.FindFirstValue("CompanyCode");
        //    var companyName = User.FindFirstValue("CompanyName") ?? "";

        //    // Adjust to end of day
        //    var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

        //    // Get all active/disbursed loans with member and loan type data
        //    var loansWithMembers = await (from loan in _context.Loans
        //                                  join member in _context.Members
        //                                  on loan.MemberNo equals member.MemberNo
        //                                  join loantype in _context.Loantypes
        //                                  on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
        //                                  from lt in loanTypeJoin.DefaultIfEmpty()
        //                                  where loan.CompanyCode == companyCode
        //                                  && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
        //                                  && loan.AuditTime <= asAtDateEnd
        //                                  && (loan.LoanAmt > 0 || loan.Aamount > 0)
        //                                  select new
        //                                  {
        //                                      loan.MemberNo,
        //                                      loan.LoanNo,
        //                                      loan.LoanCode,
        //                                      loan.ApplicDate,
        //                                      loan.AuditTime,
        //                                      loan.RepayPeriod,
        //                                      loan.LoanAmt,
        //                                      loan.Aamount,
        //                                      loan.Interest,
        //                                      MemberSurname = member.Surname,
        //                                      MemberOtherNames = member.OtherNames,
        //                                      LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
        //                                      LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
        //                                      InterestRateFromType = lt != null ? lt.Interest : null
        //                                  }).ToListAsync();

        //    if (!loansWithMembers.Any())
        //    {
        //        var emptyViewModel = new AgingAnalysisIndexViewModel
        //        {
        //            Loans = new List<AgingAnalysisViewModel>(),
        //            AsAtDate = asAtDate,
        //            HasData = false,
        //            UserCompanyCode = companyCode,
        //            CompanyName = companyName,
        //            TotalLoans = 0,
        //            TotalLoanBalance = 0,
        //            TotalPerforming = 0,
        //            TotalSpecialMention = 0,
        //            TotalWatchful = 0,
        //            TotalSubstandard = 0,
        //            TotalDoubtful = 0,
        //            TotalLoss = 0,
        //            TotalLossOver365 = 0,
        //            PerformingCount = 0,
        //            SpecialMentionCount = 0,
        //            WatchfulCount = 0,
        //            SubstandardCount = 0,
        //            DoubtfulCount = 0,
        //            LossCount = 0,
        //            LossOver365Count = 0
        //        };

        //        ViewBag.AsAtDate = asAtDate;
        //        ViewBag.CompanyName = companyName;
        //        ViewBag.HasData = false;
        //        ViewBag.Message = "No active/disbursed loans found as at the selected date.";

        //        return View("~/Views/Reports/AgingAnalysis.cshtml", emptyViewModel);
        //    }

        //    var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

        //    // Get latest repayment for each loan (to get last payment date)
        //    var latestRepayments = await _context.Repay
        //    .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
        //    .GroupBy(r => r.LoanNo)
        //    .Select(g => new
        //    {
        //        LoanNo = g.Key,
        //        LastPaymentDate = g.Max(r => r.DateReceived),
        //        TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0)
        //    })
        //    .ToDictionaryAsync(g => g.LoanNo, g => g);

        //    // Get the principal amount per payment from the Repay table
        //    var principalPerPayment = new Dictionary<string, decimal>();

        //    foreach (var loan in loansWithMembers)
        //    {
        //        decimal monthlyPrincipal = 0;

        //        // Try to get principal from repayments (most accurate)
        //        var loanRepayments = await _context.Repay
        //        .Where(r => r.LoanNo == loan.LoanNo && r.CompanyCode == companyCode && r.Posted == true)
        //        .OrderBy(r => r.DateReceived)
        //        .ToListAsync();

        //        if (loanRepayments.Any())
        //        {
        //            var firstRepayment = loanRepayments.FirstOrDefault();
        //            if (firstRepayment != null && firstRepayment.Principal.HasValue && firstRepayment.Principal.Value > 0)
        //            {
        //                monthlyPrincipal = firstRepayment.Principal.Value;
        //            }
        //        }

        //        // If not found in repayments, calculate from loan amount and repayment period
        //        if (monthlyPrincipal <= 0)
        //        {
        //            decimal amountIssued = loan.Aamount ?? loan.LoanAmt ?? 0;
        //            int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;

        //            if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0 && amountIssued > 0)
        //            {
        //                monthlyPrincipal = amountIssued / repaymentPeriodMonths.Value;
        //            }
        //        }

        //        principalPerPayment[loan.LoanNo] = monthlyPrincipal;
        //    }

        //    // Get loan balances from Loanbal table as primary source
        //    var loanBalances = await _context.Loanbal
        //    .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
        //    .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

        //    var agingLoans = new List<AgingAnalysisViewModel>();
        //    decimal totalLoanBalance = 0;
        //    decimal totalPerforming = 0, totalSpecialMention = 0, totalWatchful = 0;
        //    decimal totalSubstandard = 0, totalDoubtful = 0, totalLoss = 0, totalLossOver365 = 0;
        //    int performingCount = 0, specialMentionCount = 0, watchfulCount = 0;
        //    int substandardCount = 0, doubtfulCount = 0, lossCount = 0, lossOver365Count = 0;

        //    foreach (var loan in loansWithMembers)
        //    {
        //        // Get current balance
        //        decimal currentBalance = 0;
        //        if (loanBalances.ContainsKey(loan.LoanNo))
        //        {
        //            currentBalance = loanBalances[loan.LoanNo];
        //        }
        //        else
        //        {
        //            currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
        //        }

        //        // Skip if loan is fully paid (balance <= 0)
        //        if (currentBalance <= 0) continue;

        //        // Get monthly principal amount for this loan (from Repay table)
        //        decimal monthlyPrincipal = principalPerPayment.ContainsKey(loan.LoanNo)
        //        ? principalPerPayment[loan.LoanNo]
        //        : 0;

        //        if (monthlyPrincipal <= 0) continue;

        //        // Get last payment date
        //        DateTime? lastPaymentDate = null;
        //        if (latestRepayments.ContainsKey(loan.LoanNo))
        //        {
        //            lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
        //        }

        //        // ========== ORIGINAL DAYS IN ARREARS CALCULATION (RESTORED) ==========
        //        int daysInArrears = 0;

        //        if (lastPaymentDate.HasValue)
        //        {
        //            var calculatedNextDueDate = lastPaymentDate.Value.AddMonths(1);

        //            if (asAtDate > calculatedNextDueDate)
        //            {
        //                daysInArrears = (asAtDate - calculatedNextDueDate).Days;
        //            }
        //            else
        //            {
        //                daysInArrears = 0;
        //            }
        //        }
        //        else
        //        {
        //            // No payments made yet - check if first payment is due
        //            DateTime firstDueDate = loan.AuditTime.AddMonths(1);
        //            if (asAtDate > firstDueDate)
        //            {
        //                daysInArrears = (asAtDate - firstDueDate).Days;
        //            }
        //            else
        //            {
        //                daysInArrears = 0;
        //            }
        //        }

        //        // Ensure days in arrears is not negative
        //        if (daysInArrears < 0) daysInArrears = 0;

        //        // Calculate months overdue based on days in arrears
        //        int monthsOverdue = (int)Math.Floor(daysInArrears / 30.0);
        //        if (monthsOverdue < 0) monthsOverdue = 0;
        //        int missedPayments = monthsOverdue;

        //        // ========== PRINCIPAL AMOUNT ALLOCATION TO EACH CATEGORY ==========
        //        decimal performingAmt = 0;        // 0 days (current month's due)
        //        decimal specialMentionAmt = 0;    // 1-30 days (1 month defaulted)
        //        decimal watchfulAmt = 0;          // 31-60 days (2 months defaulted)
        //        decimal substandardAmt = 0;       // 61-90 days (3 months defaulted)
        //        decimal doubtfulAmt = 0;          // 91-180 days (4-6 months defaulted)
        //        decimal lossAmt = 0;              // 181-365 days (7-12 months defaulted)
        //        decimal lossOver365Amt = 0;       // 365+ days (12+ months defaulted)

        //        // Allocate monthly principal to each category based on missed payments
        //        if (missedPayments >= 0)
        //        {
        //            performingAmt = monthlyPrincipal;  // Current month's due principal
        //        }

        //        if (missedPayments >= 1)
        //        {
        //            specialMentionAmt = monthlyPrincipal;
        //        }

        //        if (missedPayments >= 2)
        //        {
        //            watchfulAmt = monthlyPrincipal;
        //        }

        //        if (missedPayments >= 3)
        //        {
        //            substandardAmt = monthlyPrincipal;
        //        }

        //        if (missedPayments >= 4)
        //        {
        //            int doubtfulMonths = Math.Min(missedPayments - 3, 3);
        //            doubtfulAmt = monthlyPrincipal * doubtfulMonths;
        //        }

        //        if (missedPayments >= 7)
        //        {
        //            int lossMonths = Math.Min(missedPayments - 6, 6);
        //            lossAmt = monthlyPrincipal * lossMonths;
        //        }

        //        if (missedPayments >= 13)
        //        {
        //            int lossOverMonths = missedPayments - 12;
        //            lossOver365Amt = monthlyPrincipal * lossOverMonths;
        //        }

        //        // Determine primary category based on days in arrears
        //        int category = GetCategoryFromDays(daysInArrears);
        //        string classification = GetCategoryName(category);

        //        // Update counts and totals based on category
        //        switch (category)
        //        {
        //            case 1: performingCount++; break;
        //            case 2: specialMentionCount++; break;
        //            case 3: watchfulCount++; break;
        //            case 4: substandardCount++; break;
        //            case 5: doubtfulCount++; break;
        //            case 6: lossCount++; break;
        //            case 7: lossOver365Count++; break;
        //        }

        //        totalPerforming += performingAmt;
        //        totalSpecialMention += specialMentionAmt;
        //        totalWatchful += watchfulAmt;
        //        totalSubstandard += substandardAmt;
        //        totalDoubtful += doubtfulAmt;
        //        totalLoss += lossAmt;
        //        totalLossOver365 += lossOver365Amt;

        //        // Calculate next due date
        //        DateTime? nextDueDate = null;
        //        if (lastPaymentDate.HasValue)
        //        {
        //            nextDueDate = lastPaymentDate.Value.AddMonths(1);
        //        }
        //        else
        //        {
        //            nextDueDate = loan.AuditTime.AddMonths(1);
        //        }

        //        // Calculate date of completion
        //        DateTime? dateOfCompletion = null;
        //        int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;
        //        if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0)
        //        {
        //            dateOfCompletion = loan.AuditTime.AddMonths(repaymentPeriodMonths.Value);
        //        }

        //        // Build member name
        //        string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
        //        if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

        //        var agingItem = new AgingAnalysisViewModel
        //        {
        //            LoanNo = loan.LoanNo,
        //            MemberNo = loan.MemberNo,
        //            FullName = fullName,
        //            LoanBalance = currentBalance,
        //            RepayPeriod = repaymentPeriodMonths,
        //            NextDueDate = nextDueDate,
        //            DateIssued = loan.AuditTime,
        //            DaysInArrears = daysInArrears,
        //            LastRepayDate = lastPaymentDate,
        //            DateOfCompletion = dateOfCompletion,
        //            ValueChain = loan.LoanName ?? "Unknown",
        //            MonthlyInstallment = monthlyPrincipal,
        //            MonthsInArrears = monthsOverdue,
        //            MissedPayments = missedPayments,
        //            MonthlyPrincipalDue = monthlyPrincipal,
        //            Performing = performingAmt,
        //            SpecialMention = specialMentionAmt,
        //            Watchful = watchfulAmt,
        //            Substandard = substandardAmt,
        //            Doubtful = doubtfulAmt,
        //            Loss = lossAmt,
        //            LossOver365 = lossOver365Amt,
        //            Classification = classification,
        //            ArrearsCategory = category
        //        };

        //        totalLoanBalance += currentBalance;
        //        agingLoans.Add(agingItem);
        //    }

        //    var viewModel = new AgingAnalysisIndexViewModel
        //    {
        //        Loans = agingLoans.OrderBy(l => l.DaysInArrears).ToList(),
        //        AsAtDate = asAtDate,
        //        HasData = agingLoans.Any(),
        //        UserCompanyCode = companyCode,
        //        CompanyName = companyName,
        //        TotalLoans = agingLoans.Count,
        //        TotalLoanBalance = totalLoanBalance,
        //        TotalPerforming = totalPerforming,
        //        TotalSpecialMention = totalSpecialMention,
        //        TotalWatchful = totalWatchful,
        //        TotalSubstandard = totalSubstandard,
        //        TotalDoubtful = totalDoubtful,
        //        TotalLoss = totalLoss,
        //        TotalLossOver365 = totalLossOver365,
        //        PerformingCount = performingCount,
        //        SpecialMentionCount = specialMentionCount,
        //        WatchfulCount = watchfulCount,
        //        SubstandardCount = substandardCount,
        //        DoubtfulCount = doubtfulCount,
        //        LossCount = lossCount,
        //        LossOver365Count = lossOver365Count
        //    };

        //    ViewBag.AsAtDate = asAtDate;
        //    ViewBag.CompanyName = companyName;
        //    ViewBag.HasData = viewModel.HasData;

        //    return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
        //}

        //private int GetCategoryFromDays(int daysInArrears)
        //{
        //    if (daysInArrears <= 0) return 1;      // Performing
        //    if (daysInArrears <= 30) return 2;     // Special Mention
        //    if (daysInArrears <= 60) return 3;     // Watchful
        //    if (daysInArrears <= 90) return 4;     // Substandard
        //    if (daysInArrears <= 180) return 5;    // Doubtful
        //    if (daysInArrears <= 365) return 6;    // Loss
        //    return 7;                               // Loss Over 365
        //}

        //private string GetCategoryName(int category)
        //{
        //    return category switch
        //    {
        //        1 => "Performing",
        //        2 => "Special Mention",
        //        3 => "Watchful",
        //        4 => "Substandard",
        //        5 => "Doubtful",
        //        6 => "Loss",
        //        7 => "Loss Over 365",
        //        _ => "Unknown"
        //    };
        //}

        //[HttpPost]
        //public async Task<IActionResult> AgingAnalysis(DateTime asAtDate)
        //{
        //    var companyCode = User.FindFirstValue("CompanyCode");
        //    var companyName = User.FindFirstValue("CompanyName") ?? "";

        //    // Adjust to end of day
        //    var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

        //    // Get all active/disbursed loans with member and loan type data
        //    var loansWithMembers = await (from loan in _context.Loans
        //                                  join member in _context.Members
        //                                      on loan.MemberNo equals member.MemberNo
        //                                  join loantype in _context.Loantypes
        //                                      on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
        //                                  from lt in loanTypeJoin.DefaultIfEmpty()
        //                                  where loan.CompanyCode == companyCode
        //                                      && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
        //                                      && loan.AuditTime <= asAtDateEnd
        //                                  select new
        //                                  {
        //                                      loan.MemberNo,
        //                                      loan.LoanNo,
        //                                      loan.LoanCode,
        //                                      loan.ApplicDate,
        //                                      loan.AuditTime,
        //                                      loan.RepayPeriod,
        //                                      loan.LoanAmt,
        //                                      loan.Aamount,
        //                                      loan.Interest,
        //                                      MemberSurname = member.Surname,
        //                                      MemberOtherNames = member.OtherNames,
        //                                      LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
        //                                      LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
        //                                      InterestRateFromType = lt != null ? lt.Interest : null
        //                                  }).ToListAsync();

        //    // If no loans found with Disbursed/Endorsed status, try to get all loans with positive balance
        //    if (!loansWithMembers.Any())
        //    {
        //        var loansWithBalance = await (from lb in _context.Loanbal
        //                                      join loan in _context.Loans on lb.LoanNo equals loan.LoanNo
        //                                      join member in _context.Members on loan.MemberNo equals member.MemberNo
        //                                      join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
        //                                      from lt in loanTypeJoin.DefaultIfEmpty()
        //                                      where lb.Companycode == companyCode && lb.Balance > 0
        //                                      select new
        //                                      {
        //                                          loan.MemberNo,
        //                                          loan.LoanNo,
        //                                          loan.LoanCode,
        //                                          loan.ApplicDate,
        //                                          loan.AuditTime,
        //                                          loan.RepayPeriod,
        //                                          loan.LoanAmt,
        //                                          loan.Aamount,
        //                                          loan.Interest,
        //                                          MemberSurname = member.Surname,
        //                                          MemberOtherNames = member.OtherNames,
        //                                          LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? ""),
        //                                          LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
        //                                          InterestRateFromType = lt != null ? lt.Interest : null
        //                                      }).ToListAsync();
        //        loansWithMembers = loansWithBalance;
        //    }

        //    if (!loansWithMembers.Any())
        //    {
        //        var emptyViewModel = new AgingAnalysisIndexViewModel
        //        {
        //            Loans = new List<AgingAnalysisViewModel>(),
        //            AsAtDate = asAtDate,
        //            HasData = false,
        //            UserCompanyCode = companyCode,
        //            CompanyName = companyName,
        //            TotalLoans = 0,
        //            TotalLoanBalance = 0,
        //            TotalPerforming = 0,
        //            TotalSpecialMention = 0,
        //            TotalWatchful = 0,
        //            TotalSubstandard = 0,
        //            TotalDoubtful = 0,
        //            TotalLoss = 0,
        //            TotalLossOver365 = 0,
        //            PerformingCount = 0,
        //            SpecialMentionCount = 0,
        //            WatchfulCount = 0,
        //            SubstandardCount = 0,
        //            DoubtfulCount = 0,
        //            LossCount = 0,
        //            LossOver365Count = 0
        //        };

        //        ViewBag.AsAtDate = asAtDate;
        //        ViewBag.CompanyName = companyName;
        //        ViewBag.HasData = false;
        //        ViewBag.Message = "No active/disbursed loans found as at the selected date.";

        //        return View("~/Views/Reports/AgingAnalysis.cshtml", emptyViewModel);
        //    }

        //    var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

        //    // Get latest repayment for each loan (to get last payment date)
        //    var latestRepayments = await _context.Loanbal
        //        .Where(r => loanNos.Contains(r.LoanNo) && r.Companycode == companyCode && r.LastDate <= asAtDateEnd /*&& r.Posted == true*/)
        //        .GroupBy(r => r.LoanNo)
        //        .Select(g => new
        //        {
        //            LoanNo = g.Key,
        //            LastPaymentDate = g.Max(r => r.LastDate),
        //            TotalPrincipalPaid = g.Sum(r => r.Balance)
        //        })
        //        .ToDictionaryAsync(g => g.LoanNo, g => g);

        //    // Get loan balances from Loanbal table as primary source
        //    var loanBalances = await _context.Loanbal
        //        .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
        //        .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

        //    var agingLoans = new List<AgingAnalysisViewModel>();
        //    decimal totalLoanBalance = 0;
        //    decimal totalPerforming = 0, totalSpecialMention = 0, totalWatchful = 0;
        //    decimal totalSubstandard = 0, totalDoubtful = 0, totalLoss = 0, totalLossOver365 = 0;
        //    int performingCount = 0, specialMentionCount = 0, watchfulCount = 0;
        //    int substandardCount = 0, doubtfulCount = 0, lossCount = 0, lossOver365Count = 0;

        //    foreach (var loan in loansWithMembers)
        //    {
        //        // Get current balance
        //        decimal currentBalance = 0;
        //        if (loanBalances.ContainsKey(loan.LoanNo))
        //        {
        //            currentBalance = loanBalances[loan.LoanNo];
        //        }
        //        else
        //        {
        //            currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
        //        }

        //        // Skip if loan is fully paid (balance <= 0)
        //        if (currentBalance <= 0) continue;

        //        // Get last payment date
        //        DateTime? lastPaymentDate = null;
        //        if (latestRepayments.ContainsKey(loan.LoanNo))
        //        {
        //            lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
        //        }

        //        // Calculate Days In Arrears (keeping your existing logic)
        //        int daysInArrears = 0;

        //        if (lastPaymentDate.HasValue)
        //        {
        //            var calculatedNextDueDate = lastPaymentDate.Value.AddMonths(1);

        //            if (asAtDate > calculatedNextDueDate)
        //            {
        //                daysInArrears = (asAtDate - calculatedNextDueDate).Days;
        //            }
        //            else
        //            {
        //                daysInArrears = 0;
        //            }
        //        }
        //        else
        //        {
        //            // No payments made yet - check if first payment is due
        //            DateTime firstDueDate = loan.AuditTime.AddMonths(1);
        //            if (asAtDate > firstDueDate)
        //            {
        //                daysInArrears = (asAtDate - firstDueDate).Days;
        //            }
        //            else
        //            {
        //                daysInArrears = 0;
        //            }
        //        }

        //        // Ensure days in arrears is not negative
        //        if (daysInArrears < 0) daysInArrears = 0;

        //        // Calculate months overdue based on days in arrears
        //        int monthsOverdue = (int)Math.Floor(daysInArrears / 30.0);
        //        if (monthsOverdue < 0) monthsOverdue = 0;

        //        // Calculate monthly installment amount
        //        decimal amountIssued = loan.Aamount ?? loan.LoanAmt ?? 0;
        //        int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;

        //        decimal monthlyInstallment = 0;

        //        // Calculate monthly installment including interest
        //        if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0 && amountIssued > 0)
        //        {
        //            // Get interest rate
        //            decimal interestRate = 0;
        //            if (loan.Interest.HasValue && loan.Interest.Value > 0)
        //            {
        //                interestRate = loan.Interest.Value / 100;
        //            }
        //            else if (loan.InterestRateFromType != null && !string.IsNullOrEmpty(loan.InterestRateFromType))
        //            {
        //                string interestStr = loan.InterestRateFromType.ToString().Replace("%", "");
        //                if (decimal.TryParse(interestStr, out decimal parsedRate))
        //                {
        //                    interestRate = parsedRate / 100;
        //                }
        //            }

        //            if (interestRate > 0)
        //            {
        //                decimal r = interestRate / 12;
        //                int n = repaymentPeriodMonths.Value;
        //                if (r > 0)
        //                {
        //                    double factor = Math.Pow((double)(1 + r), n);
        //                    monthlyInstallment = amountIssued * r * (decimal)factor / (decimal)(factor - 1);
        //                }
        //                else
        //                {
        //                    monthlyInstallment = amountIssued / n;
        //                }
        //            }
        //            else
        //            {
        //                monthlyInstallment = amountIssued / repaymentPeriodMonths.Value;
        //            }
        //        }

        //        if (monthlyInstallment <= 0) continue;
        //        // ========== Calculate amounts based on days in arrears ==========
        //        decimal performingAmt = 0;        // 0 days (current month's due)
        //        decimal specialMentionAmt = 0;    // 1-30 days (1 month defaulted)
        //        decimal watchfulAmt = 0;          // 31-60 days (2 months defaulted)
        //        decimal substandardAmt = 0;       // 61-90 days (3 months defaulted)
        //        decimal doubtfulAmt = 0;          // 91-180 days (4-6 months defaulted)
        //        decimal lossAmt = 0;              // 181-365 days (7-12 months defaulted)
        //        decimal lossOver365Amt = 0;       // 365+ days (12+ months defaulted)

        //        if (monthlyInstallment > 0)
        //        {
        //            // Get the category based on days in arrears
        //            int arrearsCategory = AgingCategories.GetCategoryFromDays(daysInArrears);

        //            switch (arrearsCategory)
        //            {
        //                case AgingCategories.PERFORMING:  // 0 days
        //                    performingAmt = monthlyInstallment;
        //                    break;
        //                case AgingCategories.SPECIAL_MENTION:  // 1-30 days
        //                    specialMentionAmt = monthlyInstallment * 2;
        //                    break;
        //                case AgingCategories.WATCHFUL:  // 31-60 days
        //                    watchfulAmt = monthlyInstallment * 3;
        //                    break;
        //                case AgingCategories.SUBSTANDARD:  // 61-90 days
        //                    substandardAmt = monthlyInstallment * 4;
        //                    break;
        //                case AgingCategories.DOUBTFUL:  // 91-180 days
        //                    doubtfulAmt = monthlyInstallment * 6;
        //                    break;
        //                case AgingCategories.LOSS:  // 181-365 days
        //                    lossAmt = monthlyInstallment * 12;
        //                    break;
        //                case AgingCategories.LOSS_OVER_365:  // 365+ days
        //                    lossOver365Amt = currentBalance;
        //                    break;
        //            }
        //        }

        //        // Determine category based on days in arrears (keeping your existing logic)
        //        int category = AgingCategories.GetCategoryFromDays(daysInArrears);
        //        string classification = AgingCategories.GetCategoryName(category);

        //        // Update counts and totals based on category
        //        switch (category)
        //        {
        //            case AgingCategories.PERFORMING:
        //                performingCount++;
        //                totalPerforming += performingAmt;
        //                break;
        //            case AgingCategories.SPECIAL_MENTION:
        //                specialMentionCount++;
        //                totalSpecialMention += specialMentionAmt;
        //                break;
        //            case AgingCategories.WATCHFUL:
        //                watchfulCount++;
        //                totalWatchful += watchfulAmt;
        //                break;
        //            case AgingCategories.SUBSTANDARD:
        //                substandardCount++;
        //                totalSubstandard += substandardAmt;
        //                break;
        //            case AgingCategories.DOUBTFUL:
        //                doubtfulCount++;
        //                totalDoubtful += doubtfulAmt;
        //                break;
        //            case AgingCategories.LOSS:
        //                lossCount++;
        //                totalLoss += lossAmt;
        //                break;
        //            case AgingCategories.LOSS_OVER_365:
        //                lossOver365Count++;
        //                totalLossOver365 += lossOver365Amt;
        //                break;
        //        }

        //        // Calculate next due date
        //        DateTime? nextDueDate = null;
        //        if (lastPaymentDate.HasValue)
        //        {
        //            nextDueDate = lastPaymentDate.Value.AddMonths(1);
        //        }
        //        else
        //        {
        //            nextDueDate = loan.AuditTime.AddMonths(1);
        //        }

        //        // Calculate date of completion
        //        DateTime? dateOfCompletion = null;
        //        if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0)
        //        {
        //            dateOfCompletion = loan.AuditTime.AddMonths(repaymentPeriodMonths.Value);
        //        }

        //        // Build member name
        //        string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
        //        if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

        //        var agingItem = new AgingAnalysisViewModel
        //        {
        //            LoanNo = loan.LoanNo,
        //            MemberNo = loan.MemberNo,
        //            FullName = fullName,
        //            LoanBalance = currentBalance,
        //            RepayPeriod = repaymentPeriodMonths,
        //            NextDueDate = nextDueDate,
        //            DateIssued = loan.AuditTime,
        //            DaysInArrears = daysInArrears,
        //            LastRepayDate = lastPaymentDate,
        //            DateOfCompletion = dateOfCompletion,
        //            ValueChain = loan.LoanName ?? "Unknown",
        //            MonthlyInstallment = monthlyInstallment,
        //            MonthsInArrears = monthsOverdue,
        //            Performing = performingAmt,
        //            SpecialMention = specialMentionAmt,
        //            Watchful = watchfulAmt,
        //            Substandard = substandardAmt,
        //            Doubtful = doubtfulAmt,
        //            Loss = lossAmt,
        //            LossOver365 = lossOver365Amt,
        //            Classification = classification,
        //            ArrearsCategory = category
        //        };

        //        totalLoanBalance += currentBalance;
        //        agingLoans.Add(agingItem);
        //    }

        //    var viewModel = new AgingAnalysisIndexViewModel
        //    {
        //        Loans = agingLoans.OrderBy(l => l.DaysInArrears).ToList(),
        //        AsAtDate = asAtDate,
        //        HasData = agingLoans.Any(),
        //        UserCompanyCode = companyCode,
        //        CompanyName = companyName,
        //        TotalLoans = agingLoans.Count,
        //        TotalLoanBalance = totalLoanBalance,
        //        TotalPerforming = totalPerforming,
        //        TotalSpecialMention = totalSpecialMention,
        //        TotalWatchful = totalWatchful,
        //        TotalSubstandard = totalSubstandard,
        //        TotalDoubtful = totalDoubtful,
        //        TotalLoss = totalLoss,
        //        TotalLossOver365 = totalLossOver365,
        //        PerformingCount = performingCount,
        //        SpecialMentionCount = specialMentionCount,
        //        WatchfulCount = watchfulCount,
        //        SubstandardCount = substandardCount,
        //        DoubtfulCount = doubtfulCount,
        //        LossCount = lossCount,
        //        LossOver365Count = lossOver365Count
        //    };

        //    ViewBag.AsAtDate = asAtDate;
        //    ViewBag.CompanyName = companyName;
        //    ViewBag.HasData = viewModel.HasData;

        //    return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportAgingAnalysisToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                              && (loan.LoanAmt > 0 || loan.Aamount > 0)
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? ""),
                                              LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("AgingAnalysis");
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LatestBalance = g.OrderByDescending(r => r.DateReceived)
                                    .Select(r => r.LoanBalance)
                                    .FirstOrDefault() ?? 0,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var agingData = new List<AgingAnalysisViewModel>();
            decimal totalBalance = 0;

            foreach (var loan in loansWithMembers)
            {
                decimal currentBalance = 0;
                DateTime? lastPaymentDate = null;

                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    var repayment = latestRepayments[loan.LoanNo];
                    currentBalance = repayment.LatestBalance;
                    lastPaymentDate = repayment.LastPaymentDate;
                }
                else if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo];
                }

                if (currentBalance <= 0) continue;

                int daysInArrears = 0;
                DateTime? nextDueDate = null;

                int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;

                if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0)
                {
                    DateTime auditTimeValue = loan.AuditTime;

                    var monthsSinceIssued = ((asAtDate.Year - auditTimeValue.Year) * 12) +
                                            (asAtDate.Month - auditTimeValue.Month);

                    if (monthsSinceIssued >= 0)
                    {
                        nextDueDate = auditTimeValue.AddMonths(monthsSinceIssued + 1);

                        if (lastPaymentDate.HasValue && lastPaymentDate.Value > auditTimeValue)
                        {
                            daysInArrears = (asAtDate - lastPaymentDate.Value).Days;
                            if (daysInArrears < 0) daysInArrears = 0;
                        }
                        else if (nextDueDate.HasValue && nextDueDate.Value < asAtDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate.Value).Days;
                            if (daysInArrears < 0) daysInArrears = 0;
                        }
                    }
                }

                int category = AgingCategories.GetCategoryFromDays(daysInArrears);
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                // Calculate Date of Completion
                DateTime? dateOfCompletion = null;
                if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0 && loan.AuditTime != default)
                {
                    dateOfCompletion = loan.AuditTime.AddMonths(repaymentPeriodMonths.Value);
                }

                agingData.Add(new AgingAnalysisViewModel
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    FullName = fullName,
                    LoanBalance = currentBalance,
                    RepayPeriod = repaymentPeriodMonths,
                    NextDueDate = nextDueDate,
                    DateIssued = loan.AuditTime,
                    DaysInArrears = daysInArrears,
                    LastRepayDate = lastPaymentDate,
                    DateOfCompletion = dateOfCompletion,
                    ValueChain = loan.LoanName ?? "",
                    Classification = AgingCategories.GetCategoryName(category),
                    Performing = category == AgingCategories.PERFORMING ? currentBalance : 0,
                    SpecialMention = category == AgingCategories.SPECIAL_MENTION ? currentBalance : 0,
                    Watchful = category == AgingCategories.WATCHFUL ? currentBalance : 0,
                    Substandard = category == AgingCategories.SUBSTANDARD ? currentBalance : 0,
                    Doubtful = category == AgingCategories.DOUBTFUL ? currentBalance : 0,
                    Loss = category == AgingCategories.LOSS ? currentBalance : 0,
                    LossOver365 = category == AgingCategories.LOSS_OVER_365 ? currentBalance : 0
                });

                totalBalance += currentBalance;
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
                        header.Item().AlignCenter().Text($"AGING ANALYSIS REPORT - AS AT {asAtDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.6f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Name").Bold().FontSize(7); // Changed from Value Chain
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Issued").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Days").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Last Repay").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Completion").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Performing 0 Days").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Special").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Watchful").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Substd").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Doubtful").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loss").Bold().FontSize(6);
                        });

                        foreach (var loan in agingData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.FullName ?? "N/A").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanBalance:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.ValueChain ?? "-").FontSize(7); // Now shows Loan Name
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DateIssued?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DaysInArrears.ToString()).FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.LastRepayDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DateOfCompletion?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Performing:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.SpecialMention:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Watchful:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Substandard:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Doubtful:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Loss:N0}").FontSize(7);
                        }

                        table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalBalance:N0}").Bold().FontSize(8);
                        table.Cell().ColumnSpan(10).Border(0.2f).Background("#f9f9f9").Padding(4);
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
            return File(content, "application/pdf", $"AgingAnalysis_{asAtDate:yyyyMMdd}.pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportAgingAnalysisToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithMembers = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                              && (loan.LoanAmt > 0 || loan.Aamount > 0)
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.ApplicDate,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? ""),
                                              LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null
                                          }).ToListAsync();

            if (!loansWithMembers.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("AgingAnalysis");
            }

            var loanNos = loansWithMembers.Select(l => l.LoanNo).ToList();

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LatestBalance = g.OrderByDescending(r => r.DateReceived)
                                    .Select(r => r.LoanBalance)
                                    .FirstOrDefault() ?? 0,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var agingData = new List<dynamic>();

            foreach (var loan in loansWithMembers)
            {
                decimal currentBalance = 0;
                DateTime? lastPaymentDate = null;

                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    var repayment = latestRepayments[loan.LoanNo];
                    currentBalance = repayment.LatestBalance;
                    lastPaymentDate = repayment.LastPaymentDate;
                }
                else if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo];
                }

                if (currentBalance <= 0) continue;

                int daysInArrears = 0;
                DateTime? nextDueDate = null;

                int? repaymentPeriodMonths = loan.RepayPeriod ?? loan.LoanTypeRepayPeriod;

                if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0)
                {
                    DateTime auditTimeValue = loan.AuditTime;

                    var monthsSinceIssued = ((asAtDate.Year - auditTimeValue.Year) * 12) +
                                            (asAtDate.Month - auditTimeValue.Month);

                    if (monthsSinceIssued >= 0)
                    {
                        nextDueDate = auditTimeValue.AddMonths(monthsSinceIssued + 1);

                        if (lastPaymentDate.HasValue && lastPaymentDate.Value > auditTimeValue)
                        {
                            daysInArrears = (asAtDate - lastPaymentDate.Value).Days;
                            if (daysInArrears < 0) daysInArrears = 0;
                        }
                        else if (nextDueDate.HasValue && nextDueDate.Value < asAtDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate.Value).Days;
                            if (daysInArrears < 0) daysInArrears = 0;
                        }
                    }
                }

                int category = AgingCategories.GetCategoryFromDays(daysInArrears);
                string classification = AgingCategories.GetCategoryName(category);

                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                // Calculate Date of Completion
                DateTime? dateOfCompletion = null;
                if (repaymentPeriodMonths.HasValue && repaymentPeriodMonths.Value > 0 && loan.AuditTime != default)
                {
                    dateOfCompletion = loan.AuditTime.AddMonths(repaymentPeriodMonths.Value);
                }

                agingData.Add(new
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    MemberName = fullName,
                    LoanBalance = currentBalance,
                    LoanName = loan.LoanName ?? "",
                    DateIssued = loan.AuditTime,
                    DaysInArrears = daysInArrears,
                    LastRepayDate = lastPaymentDate,
                    DateOfCompletion = dateOfCompletion,
                    Classification = classification,
                    Performing = category == AgingCategories.PERFORMING ? currentBalance : 0,
                    SpecialMention = category == AgingCategories.SPECIAL_MENTION ? currentBalance : 0,
                    Watchful = category == AgingCategories.WATCHFUL ? currentBalance : 0,
                    Substandard = category == AgingCategories.SUBSTANDARD ? currentBalance : 0,
                    Doubtful = category == AgingCategories.DOUBTFUL ? currentBalance : 0,
                    Loss = category == AgingCategories.LOSS ? currentBalance : 0,
                    LossOver365 = category == AgingCategories.LOSS_OVER_365 ? currentBalance : 0
                });
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Aging Analysis");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 17).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 17).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"AGING ANALYSIS REPORT - AS AT {asAtDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 17).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            // Headers - Updated to show Loan Name instead of Value Chain
            string[] headers = { "LoanNo", "MemberNo", "Member Name", "Loan Balance", "Loan Name", "Date Issued",
                     "Days In Arrears", "Last Repay Date", "Date of Completion", "Classification",
                     "Performing 0 Days", "Special Mention", "Watchful", "Substandard", "Doubtful", "Loss", "Loss Over 365" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalBalance = 0;
            decimal totalPerforming = 0, totalSpecialMention = 0, totalWatchful = 0;
            decimal totalSubstandard = 0, totalDoubtful = 0, totalLoss = 0, totalLossOver365 = 0;

            foreach (var loan in agingData)
            {
                worksheet.Cell(currentRow, 1).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 2).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 3).Value = loan.MemberName;
                worksheet.Cell(currentRow, 4).Value = loan.LoanBalance;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Value = loan.LoanName;
                worksheet.Cell(currentRow, 6).Value = loan.DateIssued.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 7).Value = loan.DaysInArrears;
                worksheet.Cell(currentRow, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Cell(currentRow, 8).Value = loan.LastRepayDate?.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 9).Value = loan.DateOfCompletion?.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 10).Value = loan.Classification;
                worksheet.Cell(currentRow, 11).Value = loan.Performing;
                worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 12).Value = loan.SpecialMention;
                worksheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 13).Value = loan.Watchful;
                worksheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 14).Value = loan.Substandard;
                worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 15).Value = loan.Doubtful;
                worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 16).Value = loan.Loss;
                worksheet.Cell(currentRow, 16).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 17).Value = loan.LossOver365;
                worksheet.Cell(currentRow, 17).Style.NumberFormat.Format = "#,##0.00";

                totalBalance += (decimal)loan.LoanBalance;
                totalPerforming += (decimal)loan.Performing;
                totalSpecialMention += (decimal)loan.SpecialMention;
                totalWatchful += (decimal)loan.Watchful;
                totalSubstandard += (decimal)loan.Substandard;
                totalDoubtful += (decimal)loan.Doubtful;
                totalLoss += (decimal)loan.Loss;
                totalLossOver365 += (decimal)loan.LossOver365;
                currentRow++;
            }

            currentRow++;
            worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
            worksheet.Cell(currentRow, 3).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Value = totalBalance;
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 11).Value = totalPerforming;
            worksheet.Cell(currentRow, 11).Style.Font.SetBold();
            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 12).Value = totalSpecialMention;
            worksheet.Cell(currentRow, 12).Style.Font.SetBold();
            worksheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 13).Value = totalWatchful;
            worksheet.Cell(currentRow, 13).Style.Font.SetBold();
            worksheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 14).Value = totalSubstandard;
            worksheet.Cell(currentRow, 14).Style.Font.SetBold();
            worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 15).Value = totalDoubtful;
            worksheet.Cell(currentRow, 15).Style.Font.SetBold();
            worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 16).Value = totalLoss;
            worksheet.Cell(currentRow, 16).Style.Font.SetBold();
            worksheet.Cell(currentRow, 16).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 17).Value = totalLossOver365;
            worksheet.Cell(currentRow, 17).Style.Font.SetBold();
            worksheet.Cell(currentRow, 17).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AgingAnalysis_{asAtDate:yyyyMMdd}.xlsx");
        }

        #endregion


        #region Loan Appraisal Report
        [HttpGet]
        public IActionResult LoanAppraisalReport()
        {
            try
            {
                ViewBag.StartDate = DateTime.Now.AddMonths(-1);
                ViewBag.EndDate = DateTime.Now;
                ViewBag.HasData = false;

                var viewModel = new LoanAppraisalIndexViewModel
                {
                    Appraisals = new List<LoanAppraisalReportViewModel>(),
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now,
                    TotalAmountRecommended = 0,
                    TotalAppraisals = 0,
                    ApprovedCount = 0,
                    DeclinedCount = 0
                };

                return View("~/Views/Reports/LoanAppraisalReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoanAppraisalReport GET: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading report: " + ex.Message;

                var viewModel = new LoanAppraisalIndexViewModel
                {
                    Appraisals = new List<LoanAppraisalReportViewModel>(),
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now
                };

                return View("~/Views/Reports/LoanAppraisalReport.cshtml", viewModel);
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoanAppraisalReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                // Validate dates
                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "Start date cannot be greater than end date.";
                    return RedirectToAction("LoanAppraisalReport");
                }

                // Adjust end date to include the entire day
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Log the query parameters
                Console.WriteLine($"CompanyCode: {companyCode}, StartDate: {startDate}, EndDate: {endDateAdjusted}");

                // Get appraisals with member and loan data using proper JOINs
                var appraisalsQuery = from appraisal in _context.Appraisal
                                      join member in _context.Members
                                          on appraisal.MemberNo equals member.MemberNo into memberJoin
                                      from m in memberJoin.DefaultIfEmpty()
                                      join loan in _context.Loans
                                          on appraisal.LoanNo equals loan.LoanNo into loanJoin
                                      from l in loanJoin.DefaultIfEmpty()
                                      where appraisal.CompanyCode == companyCode
                                          && appraisal.AppraisDate >= startDate
                                          && appraisal.AppraisDate <= endDateAdjusted
                                      select new
                                      {
                                          appraisal.LoanNo,
                                          appraisal.MemberNo,
                                          appraisal.AmtRecommended,
                                          appraisal.AppraisDate,
                                          ApplicDate = l != null ? l.ApplicDate : (DateTime?)null,
                                          LoanStatus = l != null ? l.Status : (int?)null,
                                          LoanPosted = l != null ? l.Posted : null,
                                          MemberSurname = m != null ? m.Surname : null,
                                          MemberOtherNames = m != null ? m.OtherNames : null,
                                          MemberIdNo = m != null ? m.Idno : null,
                                          MemberDob = m != null ? m.Dob : null
                                      };

                var appraisalsWithDetails = await appraisalsQuery.ToListAsync();

                // Log the count of appraisals found
                Console.WriteLine($"Found {appraisalsWithDetails.Count} appraisals");

                // Create empty view model if no data
                if (!appraisalsWithDetails.Any())
                {
                    var emptyViewModel = new LoanAppraisalIndexViewModel
                    {
                        Appraisals = new List<LoanAppraisalReportViewModel>(),
                        StartDate = startDate,
                        EndDate = endDate,
                        HasData = false,
                        CompanyName = companyName,
                        PrintedBy = printedBy,
                        GeneratedOn = DateTime.Now,
                        TotalAmountRecommended = 0,
                        TotalAppraisals = 0,
                        ApprovedCount = 0,
                        DeclinedCount = 0
                    };

                    ViewBag.StartDate = startDate;
                    ViewBag.EndDate = endDate;
                    ViewBag.HasData = false;
                    TempData["InfoMessage"] = "No appraisals found for the selected date range.";

                    return View("~/Views/Reports/LoanAppraisalReport.cshtml", emptyViewModel);
                }

                var appraisalReports = new List<LoanAppraisalReportViewModel>();
                decimal totalAmountRecommended = 0;
                int approvedCount = 0;
                int declinedCount = 0;

                foreach (var item in appraisalsWithDetails)
                {
                    try
                    {
                        // Build member name from Surname and OtherNames
                        string fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName))
                        {
                            fullName = item.MemberNo;
                        }

                        // Determine status (Approved or Declined based on loan status)
                        string status = GetLoanStatusString(item.LoanStatus, item.LoanPosted);

                        // Count approved and declined
                        if (item.LoanStatus == 4 || item.LoanStatus == 5 || item.LoanStatus == 6)
                        {
                            approvedCount++;
                        }
                        else if (item.LoanStatus == 10)
                        {
                            declinedCount++;
                        }

                        // Log for debugging
                        Console.WriteLine($"Appraisal {item.LoanNo}: Status={item.LoanStatus}, Posted={item.LoanPosted}, StatusString={status}");

                        appraisalReports.Add(new LoanAppraisalReportViewModel
                        {
                            MemberNo = item.MemberNo,
                            Names = fullName,
                            LoanNo = item.LoanNo,
                            IDNo = item.MemberIdNo ?? "-",
                            AmtRecommended = item.AmtRecommended ?? 0,
                            AppraisDate = item.AppraisDate,
                            ApplicDate = item.ApplicDate ?? item.AppraisDate,
                            Status = status
                        });

                        totalAmountRecommended += item.AmtRecommended ?? 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing appraisal {item.LoanNo}: {ex.Message}");
                    }
                }

                var viewModel = new LoanAppraisalIndexViewModel
                {
                    Appraisals = appraisalReports.OrderByDescending(a => a.AppraisDate).ToList(),
                    TotalAmountRecommended = totalAmountRecommended,
                    TotalAppraisals = appraisalReports.Count,
                    ApprovedCount = approvedCount,
                    DeclinedCount = declinedCount,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = appraisalReports.Any(),
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = viewModel.HasData;

                return View("~/Views/Reports/LoanAppraisalReport.cshtml", viewModel);
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"SQL Error in LoanAppraisalReport: {ex.Message}");
                TempData["ErrorMessage"] = "Database connection error. Please try again.";

                var emptyViewModel = new LoanAppraisalIndexViewModel
                {
                    Appraisals = new List<LoanAppraisalReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now,
                    TotalAmountRecommended = 0,
                    TotalAppraisals = 0,
                    ApprovedCount = 0,
                    DeclinedCount = 0
                };

                return View("~/Views/Reports/LoanAppraisalReport.cshtml", emptyViewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoanAppraisalReport: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;

                var emptyViewModel = new LoanAppraisalIndexViewModel
                {
                    Appraisals = new List<LoanAppraisalReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now,
                    TotalAmountRecommended = 0,
                    TotalAppraisals = 0,
                    ApprovedCount = 0,
                    DeclinedCount = 0
                };

                return View("~/Views/Reports/LoanAppraisalReport.cshtml", emptyViewModel);
            }
        }

        //[HttpGet]
        //public IActionResult LoanAppraisalReport()
        //{
        //    ViewBag.StartDate = DateTime.Now.AddMonths(-1);
        //    ViewBag.EndDate = DateTime.Now;
        //    ViewBag.HasData = false;

        //    var viewModel = new LoanAppraisalIndexViewModel
        //    {
        //        Appraisals = new List<LoanAppraisalReportViewModel>(),
        //        StartDate = DateTime.Now.AddMonths(-1),
        //        EndDate = DateTime.Now,
        //        HasData = false,
        //        CompanyName = User.FindFirstValue("CompanyName") ?? "",
        //        PrintedBy = User.Identity?.Name ?? "System",
        //        GeneratedOn = DateTime.Now,
        //        TotalAmountRecommended = 0,
        //        TotalAppraisals = 0,
        //        ApprovedCount = 0,
        //        DeclinedCount = 0
        //    };

        //    return View("~/Views/Reports/LoanAppraisalReport.cshtml", viewModel);
        //}

        //[HttpPost]
        //public async Task<IActionResult> LoanAppraisalReport(DateTime startDate, DateTime endDate)
        //{
        //    var companyCode = User.FindFirstValue("CompanyCode");
        //    var companyName = User.FindFirstValue("CompanyName") ?? "";
        //    var printedBy = User.Identity?.Name ?? "System";

        //    // Adjust end date to include the entire day
        //    var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

        //    // Get appraisals with member and loan data using proper JOINs
        //    var appraisalsWithDetails = await (from appraisal in _context.Appraisal
        //                                       join member in _context.Members
        //                                           on appraisal.MemberNo equals member.MemberNo
        //                                       join loan in _context.Loans
        //                                           on appraisal.LoanNo equals loan.LoanNo
        //                                       where appraisal.CompanyCode == companyCode
        //                                           && appraisal.AppraisDate >= startDate
        //                                           && appraisal.AppraisDate <= endDateAdjusted
        //                                       select new
        //                                       {
        //                                           appraisal.LoanNo,
        //                                           appraisal.MemberNo,
        //                                           appraisal.AmtRecommended,
        //                                           appraisal.AppraisDate,
        //                                           loan.ApplicDate,
        //                                           LoanStatus = loan.Status,  // Use loan.Status directly, not as a type
        //                                           MemberSurname = member.Surname,
        //                                           MemberOtherNames = member.OtherNames,
        //                                           MemberIdNo = member.Idno
        //                                       }).ToListAsync();

        //    if (!appraisalsWithDetails.Any())
        //    {
        //        var emptyViewModel = new LoanAppraisalIndexViewModel
        //        {
        //            Appraisals = new List<LoanAppraisalReportViewModel>(),
        //            StartDate = startDate,
        //            EndDate = endDate,
        //            HasData = false,
        //            CompanyName = companyName,
        //            PrintedBy = printedBy,
        //            GeneratedOn = DateTime.Now,
        //            TotalAmountRecommended = 0,
        //            TotalAppraisals = 0,
        //            ApprovedCount = 0,
        //            DeclinedCount = 0
        //        };

        //        ViewBag.StartDate = startDate;
        //        ViewBag.EndDate = endDate;
        //        ViewBag.HasData = false;

        //        return View("~/Views/Reports/LoanAppraisalReport.cshtml", emptyViewModel);
        //    }

        //    var appraisalReports = new List<LoanAppraisalReportViewModel>();
        //    decimal totalAmountRecommended = 0;
        //    int approvedCount = 0;
        //    int declinedCount = 0;

        //    foreach (var item in appraisalsWithDetails)
        //    {
        //        // Build member name from Surname and OtherNames
        //        string fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
        //        if (string.IsNullOrWhiteSpace(fullName))
        //        {
        //            fullName = item.MemberNo;
        //        }

        //        // Determine status (Approved or Declined based on loan status)
        //        string status = "Approved";
        //        if (item.LoanStatus == (int)Status.Rejected)
        //        {
        //            status = "Declined";
        //            declinedCount++;
        //        }
        //        else if (item.LoanStatus == (int)Status.Disbursed ||
        //                 item.LoanStatus == (int)Status.Approved ||
        //                 item.LoanStatus == (int)Status.Endorsed)
        //        {
        //            status = "Approved";
        //            approvedCount++;
        //        }
        //        else
        //        {
        //            // Default to the loan status string
        //            status = item.LoanStatus?.ToString() ?? "Pending";
        //        }

        //        appraisalReports.Add(new LoanAppraisalReportViewModel
        //        {
        //            MemberNo = item.MemberNo,
        //            Names = fullName,
        //            LoanNo = item.LoanNo,
        //            IDNo = item.MemberIdNo ?? "-",
        //            AmtRecommended = item.AmtRecommended ?? 0,
        //            AppraisDate = item.AppraisDate,
        //            ApplicDate = item.ApplicDate,
        //            Status = status
        //        });

        //        totalAmountRecommended += item.AmtRecommended ?? 0;
        //    }

        //    var viewModel = new LoanAppraisalIndexViewModel
        //    {
        //        Appraisals = appraisalReports.OrderByDescending(a => a.AppraisDate).ToList(),
        //        TotalAmountRecommended = totalAmountRecommended,
        //        TotalAppraisals = appraisalReports.Count,
        //        ApprovedCount = approvedCount,
        //        DeclinedCount = declinedCount,
        //        StartDate = startDate,
        //        EndDate = endDate,
        //        HasData = appraisalReports.Any(),
        //        CompanyName = companyName,
        //        PrintedBy = printedBy,
        //        GeneratedOn = DateTime.Now
        //    };

        //    ViewBag.StartDate = startDate;
        //    ViewBag.EndDate = endDate;
        //    ViewBag.HasData = viewModel.HasData;

        //    return View("~/Views/Reports/LoanAppraisalReport.cshtml", viewModel);
        //}

        [HttpPost]
        public async Task<IActionResult> ExportLoanAppraisalToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get appraisals with member and loan data
            var appraisalsWithDetails = await (from appraisal in _context.Appraisal
                                               join member in _context.Members
                                                   on appraisal.MemberNo equals member.MemberNo
                                               join loan in _context.Loans
                                                   on appraisal.LoanNo equals loan.LoanNo
                                               where appraisal.CompanyCode == companyCode
                                                   && appraisal.AppraisDate >= startDate
                                                   && appraisal.AppraisDate <= endDateAdjusted
                                               select new
                                               {
                                                   appraisal.LoanNo,
                                                   appraisal.MemberNo,
                                                   appraisal.AmtRecommended,
                                                   appraisal.AppraisDate,
                                                   loan.ApplicDate,
                                                   LoanStatus = loan.Status,
                                                   MemberSurname = member.Surname,
                                                   MemberOtherNames = member.OtherNames,
                                                   MemberIdNo = member.Idno
                                               }).ToListAsync();

            if (!appraisalsWithDetails.Any())
            {
                TempData["Error"] = "No appraisal data found for the selected date range";
                return RedirectToAction("LoanAppraisalReport");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loan Appraisal Report");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 8).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 8).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN APPRAISAL REPORT";
            worksheet.Range(currentRow, 1, currentRow, 8).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 8).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Names", "LoanNo", "IDNo", "AmtRecommended", "AppraisDate", "ApplicDate", "Approved/Declined" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalAmount = 0;

            foreach (var item in appraisalsWithDetails)
            {
                // Build member name
                string fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = item.MemberNo;
                }

                // Determine status
                string status = "Approved";
                if (item.LoanStatus == (int)Status.Rejected)
                {
                    status = "Declined";
                }
                else if (item.LoanStatus == (int)Status.Disbursed ||
                         item.LoanStatus == (int)Status.Approved ||
                         item.LoanStatus == (int)Status.Endorsed)
                {
                    status = "Approved";
                }
                else
                {
                    status = item.LoanStatus?.ToString() ?? "Pending";
                }

                worksheet.Cell(currentRow, 1).Value = item.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = item.LoanNo;
                worksheet.Cell(currentRow, 4).Value = item.MemberIdNo ?? "-";
                worksheet.Cell(currentRow, 5).Value = item.AmtRecommended ?? 0;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                worksheet.Cell(currentRow, 6).Value = item.AppraisDate?.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 7).Value = item.ApplicDate.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 8).Value = status;

                totalAmount += item.AmtRecommended ?? 0;
                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 4).Value = "Total Amount:";
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 5).Value = totalAmount;
            worksheet.Cell(currentRow, 5).Style.Font.SetBold();
            worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanAppraisalReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanAppraisalToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get appraisals with member and loan data
            var appraisalsWithDetails = await (from appraisal in _context.Appraisal
                                               join member in _context.Members
                                                   on appraisal.MemberNo equals member.MemberNo
                                               join loan in _context.Loans
                                                   on appraisal.LoanNo equals loan.LoanNo
                                               where appraisal.CompanyCode == companyCode
                                                   && appraisal.AppraisDate >= startDate
                                                   && appraisal.AppraisDate <= endDateAdjusted
                                               select new
                                               {
                                                   appraisal.LoanNo,
                                                   appraisal.MemberNo,
                                                   appraisal.AmtRecommended,
                                                   appraisal.AppraisDate,
                                                   loan.ApplicDate,
                                                   LoanStatus = loan.Status,
                                                   MemberSurname = member.Surname,
                                                   MemberOtherNames = member.OtherNames,
                                                   MemberIdNo = member.Idno
                                               }).ToListAsync();

            if (!appraisalsWithDetails.Any())
            {
                TempData["Error"] = "No appraisal data found for the selected date range";
                return RedirectToAction("LoanAppraisalReport");
            }

            var reportData = appraisalsWithDetails.Select(item => new
            {
                MemberNo = item.MemberNo,
                Names = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim(),
                LoanNo = item.LoanNo,
                IDNo = item.MemberIdNo ?? "-",
                AmtRecommended = item.AmtRecommended ?? 0,
                AppraisDate = item.AppraisDate,
                ApplicDate = item.ApplicDate,
                Status = item.LoanStatus == (int)Status.Rejected ? "Declined" : "Approved"
            }).ToList();

            decimal totalAmount = reportData.Sum(r => r.AmtRecommended);

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
                        header.Item().AlignCenter().Text($"LOAN APPRAISAL REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("IDNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("AmtRecommended").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("AppraisDate").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("ApplicDate").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(8);
                        });

                        foreach (var item in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(item.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(item.Names ?? "N/A").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(item.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(item.IDNo).FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.AmtRecommended:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.AppraisDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.ApplicDate.ToString("dd/MM/yyyy")).FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.Status).FontSize(7);
                        }

                        // Totals row
                        table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("Total Amount:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmount:N0}").Bold().FontSize(8);
                        table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4);
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
            return File(content, "application/pdf", $"LoanAppraisalReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Loan Application & Disbursement Report

        [HttpGet]
        public IActionResult LoanApplicationDisbursementReport()
        {
            try
            {
                ViewBag.StartDate = DateTime.Now.AddMonths(-1);
                ViewBag.EndDate = DateTime.Now;
                ViewBag.HasData = false;

                var viewModel = new LoanApplicationDisbursementIndexViewModel
                {
                    Loans = new List<LoanApplicationDisbursementReportViewModel>(),
                    StartDate = DateTime.Now.AddMonths(-1),
                    EndDate = DateTime.Now,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now,
                    TotalLoanApplications = 0,
                    ApprovedCount = 0,
                    DisbursedCount = 0,
                    ApprovedRate = 0,
                    DisbursementRate = 0,
                    TotalAppliedAmount = 0,
                    TotalAppraisedAmount = 0,
                    TotalDisbursedAmount = 0,
                    TotalLoanBalance = 0
                };

                return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Loan Application Disbursement Report GET");
                TempData["ErrorMessage"] = "Error loading report: " + ex.Message;
                return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml",
                    new LoanApplicationDisbursementIndexViewModel { HasData = false });
            }
        }
        [HttpPost]
        public async Task<IActionResult> LoanApplicationDisbursementReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                // Validate dates
                if (startDate > endDate)
                {
                    TempData["ErrorMessage"] = "Start date cannot be greater than end date.";
                    return RedirectToAction("LoanApplicationDisbursementReport");
                }

                // Adjust end date to include the entire day
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Log the query parameters
                Console.WriteLine($"CompanyCode: {companyCode}, StartDate: {startDate}, EndDate: {endDateAdjusted}");

                // Get loans with member and loan type data
                var loansQuery = from loan in _context.Loans
                                 join member in _context.Members
                                     on loan.MemberNo equals member.MemberNo into memberJoin
                                 from m in memberJoin.DefaultIfEmpty()
                                 join loantype in _context.Loantypes
                                     on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                 from lt in loanTypeJoin.DefaultIfEmpty()
                                 where loan.CompanyCode == companyCode
                                     && loan.ApplicDate >= startDate
                                     && loan.ApplicDate <= endDateAdjusted
                                 select new
                                 {
                                     loan.MemberNo,
                                     loan.LoanNo,
                                     loan.LoanCode,
                                     loan.LoanAmt,
                                     loan.RepayPeriod,
                                     loan.ApplicDate,
                                     loan.Aamount,
                                     loan.AuditTime,
                                     loan.Status,
                                     loan.Posted,
                                     MemberSurname = m != null ? m.Surname : null,
                                     MemberOtherNames = m != null ? m.OtherNames : null,
                                     MemberDob = m != null ? m.Dob : null,
                                     LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                 };

                var loansWithDetails = await loansQuery.ToListAsync();

                // Log the count of loans found
                Console.WriteLine($"Found {loansWithDetails.Count} loans");

                // Create empty view model if no data
                if (!loansWithDetails.Any())
                {
                    var emptyViewModel = new LoanApplicationDisbursementIndexViewModel
                    {
                        Loans = new List<LoanApplicationDisbursementReportViewModel>(),
                        StartDate = startDate,
                        EndDate = endDate,
                        HasData = false,
                        CompanyName = companyName,
                        PrintedBy = printedBy,
                        GeneratedOn = DateTime.Now,
                        TotalLoanApplications = 0,
                        ApprovedCount = 0,
                        DisbursedCount = 0,
                        ApprovedRate = 0,
                        DisbursementRate = 0,
                        TotalAppliedAmount = 0,
                        TotalAppraisedAmount = 0,
                        TotalDisbursedAmount = 0,
                        TotalLoanBalance = 0
                    };

                    ViewBag.StartDate = startDate;
                    ViewBag.EndDate = endDate;
                    ViewBag.HasData = false;
                    TempData["InfoMessage"] = "No loans found for the selected date range.";

                    return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml", emptyViewModel);
                }

                var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

                // Get appraisals
                var appraisals = new Dictionary<string, (decimal? AmtRecommended, DateTime? AppraisDate, string AuditID)>();
                try
                {
                    appraisals = await _context.Appraisal
                        .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                        .Select(a => new { a.LoanNo, a.AmtRecommended, a.AppraisDate, a.AuditID })
                        .ToDictionaryAsync(a => a.LoanNo, a => (a.AmtRecommended, a.AppraisDate, a.AuditID));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading appraisals: {ex.Message}");
                }

                // Get loan balances
                var loanBalances = new Dictionary<string, decimal>();
                try
                {
                    loanBalances = await _context.Loanbal
                        .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                        .Select(lb => new { lb.LoanNo, lb.Balance })
                        .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading loan balances: {ex.Message}");
                }

                // Get guarantor counts
                var guarantorCounts = new Dictionary<string, int>();
                try
                {
                    guarantorCounts = await _context.Loanguar
                        .Where(g => loanNos.Contains(g.LoanNo) && g.CompanyCode == companyCode && g.Transfered == false)
                        .GroupBy(g => g.LoanNo)
                        .Select(g => new { LoanNo = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(g => g.LoanNo, g => g.Count);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading guarantor counts: {ex.Message}");
                }

                var reportData = new List<LoanApplicationDisbursementReportViewModel>();
                int approvedCount = 0;
                int disbursedCount = 0;
                decimal totalAppliedAmount = 0;
                decimal totalAppraisedAmount = 0;
                decimal totalDisbursedAmount = 0;
                decimal totalLoanBalance = 0;

                foreach (var loan in loansWithDetails)
                {
                    try
                    {
                        // Get age group
                        string ageGroup = "N/A";
                        if (loan.MemberDob.HasValue)
                        {
                            int age = DateTime.Today.Year - loan.MemberDob.Value.Year;
                            if (loan.MemberDob.Value.Date > DateTime.Today.AddYears(-age)) age--;

                            if (age <= 35) ageGroup = "Youth (18-35)";
                            else if (age <= 50) ageGroup = "Adult (36-50)";
                            else ageGroup = "Senior (50+)";
                        }

                        // Get member name
                        string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                        if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                        // Get appraisal details
                        decimal? appraisedAmount = null;
                        DateTime? appraisedDate = null;
                        string appraisedBy = null;

                        if (appraisals.ContainsKey(loan.LoanNo))
                        {
                            var appraisal = appraisals[loan.LoanNo];
                            appraisedAmount = appraisal.AmtRecommended;
                            appraisedDate = appraisal.AppraisDate;
                            appraisedBy = appraisal.AuditID;
                        }

                        // Get loan balance
                        decimal loanBalance = 0;
                        if (loanBalances.ContainsKey(loan.LoanNo))
                        {
                            loanBalance = loanBalances[loan.LoanNo];
                        }

                        // Get guarantor count
                        int guarantorCount = 0;
                        if (guarantorCounts.ContainsKey(loan.LoanNo))
                        {
                            guarantorCount = guarantorCounts[loan.LoanNo];
                        }

                        // Determine loan status - FIXED: Use the actual Status enum values
                        string loanStatus = GetLoanStatusString(loan.Status, loan.Posted);

                        // Log status for debugging
                        Console.WriteLine($"Loan {loan.LoanNo}: Status={loan.Status}, Posted={loan.Posted}, StatusString={loanStatus}");

                        // Count approved and disbursed - FIXED: Use correct status values
                        // Status 4 = Approved, 5 = Endorsed, 6 = Disbursed
                        if (loan.Status == 4 || loan.Status == 5 || loan.Status == 6)
                        {
                            approvedCount++;
                        }

                        if (loan.Status == 6) // Disbursed
                        {
                            disbursedCount++;
                            totalDisbursedAmount += loan.Aamount ?? loan.LoanAmt ?? 0;
                        }

                        // Get disbursement amount and date
                        decimal? disbursementAmount = null;
                        DateTime? disbursementDate = null;

                        if (loan.Status == 6) // Disbursed
                        {
                            disbursementAmount = loan.Aamount ?? loan.LoanAmt ?? 0;
                            disbursementDate = loan.AuditTime;
                        }

                        totalAppliedAmount += loan.LoanAmt ?? 0;
                        totalAppraisedAmount += appraisedAmount ?? 0;
                        totalLoanBalance += loanBalance;

                        reportData.Add(new LoanApplicationDisbursementReportViewModel
                        {
                            MemberNo = loan.MemberNo,
                            Name = fullName,
                            LoanNo = loan.LoanNo,
                            AgeGroup = ageGroup,
                            LoanType = loan.LoanTypeName,
                            AppliedAmount = loan.LoanAmt ?? 0,
                            Period = loan.RepayPeriod ?? 0,
                            ApplyDate = loan.ApplicDate,
                            AppraisedAmount = appraisedAmount,
                            AppraisedDate = appraisedDate,
                            AppraisedBy = appraisedBy,
                            LoanStatus = loanStatus,
                            DisbursementAmount = disbursementAmount,
                            DisbursementDate = disbursementDate,
                            LoanBalance = loanBalance,
                            GuarantorCount = guarantorCount
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing loan {loan.LoanNo}: {ex.Message}");
                    }
                }

                // Calculate rates
                decimal approvedRate = totalAppliedAmount > 0 ? (totalAppraisedAmount / totalAppliedAmount) * 100 : 0;
                decimal disbursementRate = totalAppraisedAmount > 0 ? (totalDisbursedAmount / totalAppraisedAmount) * 100 : 0;

                var viewModel = new LoanApplicationDisbursementIndexViewModel
                {
                    Loans = reportData.OrderByDescending(l => l.ApplyDate).ToList(),
                    TotalLoanApplications = reportData.Count,
                    ApprovedCount = approvedCount,
                    DisbursedCount = disbursedCount,
                    ApprovedRate = approvedRate,
                    DisbursementRate = disbursementRate,
                    TotalAppliedAmount = totalAppliedAmount,
                    TotalAppraisedAmount = totalAppraisedAmount,
                    TotalDisbursedAmount = totalDisbursedAmount,
                    TotalLoanBalance = totalLoanBalance,
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = reportData.Any(),
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = viewModel.HasData;

                return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml", viewModel);
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"SQL Error: {ex.Message}");
                TempData["ErrorMessage"] = "Database connection error. Please try again.";

                var emptyViewModel = new LoanApplicationDisbursementIndexViewModel
                {
                    Loans = new List<LoanApplicationDisbursementReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now
                };

                return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml", emptyViewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;

                var emptyViewModel = new LoanApplicationDisbursementIndexViewModel
                {
                    Loans = new List<LoanApplicationDisbursementReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = User.FindFirstValue("CompanyName") ?? "",
                    PrintedBy = User.Identity?.Name ?? "System",
                    GeneratedOn = DateTime.Now
                };

                return View("~/Views/Reports/LoanApplicationDisbursementReport.cshtml", emptyViewModel);
            }
        }

        // Helper method to get loan status string
        private string GetLoanStatusString(int? status, string posted = null)
        {
            if (status == null) return "Unknown";

            // Check posted status first for pending approval
            if (posted == "PENDING_APPROVAL" || posted == "PENDING")
                return "Pending Approval";

            // Use the actual status values
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
                _ => $"Status {status}"
            };
        }



        [HttpPost]
        public async Task<IActionResult> ExportLoanApplicationDisbursementToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member and appraisal data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && loan.ApplicDate >= startDate
                                              && loan.ApplicDate <= endDateAdjusted
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.LoanAmt,
                                              loan.RepayPeriod,
                                              loan.ApplicDate,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.Status,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberDob = member.Dob,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No loan data found for the selected date range";
                return RedirectToAction("LoanApplicationDisbursementReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var appraisals = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                .ToDictionaryAsync(a => a.LoanNo, a => new { a.AmtRecommended, a.AppraisDate, a.AuditID });

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var guarantorCounts = await _context.Loanguar
                .Where(g => loanNos.Contains(g.LoanNo) && g.CompanyCode == companyCode && g.Transfered == false)
                .GroupBy(g => g.LoanNo)
                .Select(g => new { LoanNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.LoanNo, g => g.Count);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loan Application & Disbursement");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 14).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 14).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN APPLICATION & DISBURSEMENT REPORT";
            worksheet.Range(currentRow, 1, currentRow, 14).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 14).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Name", "LoanNo", "Age", "LoanType", "Applied Amount", "Period", "Apply Date",
                         "Appraised Amount", "Appraised Date", "Appraised By", "LoanStatus", "Disbursement Date",
                         "Disbursement Amount", "Loan Balance", "Guarantor Count" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalApplied = 0, totalAppraised = 0, totalDisbursed = 0, totalBalance = 0;
            int approvedCount = 0, disbursedCount = 0;

            foreach (var loan in loansWithDetails)
            {
                // Get age group
                string ageGroup = "N/A";
                if (loan.MemberDob.HasValue)
                {
                    int age = DateTime.Today.Year - loan.MemberDob.Value.Year;
                    if (loan.MemberDob.Value.Date > DateTime.Today.AddYears(-age)) age--;
                    if (age <= 35) ageGroup = "Youth (18-35)";
                    else if (age <= 50) ageGroup = "Adult (36-50)";
                    else ageGroup = "Senior (50+)";
                }

                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal? appraisedAmount = null;
                DateTime? appraisedDate = null;
                string appraisedBy = null;

                if (appraisals.ContainsKey(loan.LoanNo))
                {
                    var appraisal = appraisals[loan.LoanNo];
                    appraisedAmount = appraisal.AmtRecommended;
                    appraisedDate = appraisal.AppraisDate;
                    appraisedBy = appraisal.AuditID;
                }

                decimal loanBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : 0;
                int guarantorCount = guarantorCounts.ContainsKey(loan.LoanNo) ? guarantorCounts[loan.LoanNo] : 0;
                string loanStatus = GetLoanStatusString(loan.Status);

                if (loan.Status == (int)Status.Approved || loan.Status == (int)Status.Endorsed || loan.Status == (int)Status.Disbursed)
                    approvedCount++;

                if (loan.Status == (int)Status.Disbursed)
                    disbursedCount++;

                totalApplied += loan.LoanAmt ?? 0;
                totalAppraised += appraisedAmount ?? 0;
                totalDisbursed += (loan.Status == (int)Status.Disbursed ? (loan.Aamount ?? loan.LoanAmt ?? 0) : 0);
                totalBalance += loanBalance;

                worksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 4).Value = ageGroup;
                worksheet.Cell(currentRow, 5).Value = loan.LoanTypeName;
                worksheet.Cell(currentRow, 6).Value = loan.LoanAmt ?? 0;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = loan.RepayPeriod ?? 0;
                worksheet.Cell(currentRow, 8).Value = loan.ApplicDate.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 9).Value = appraisedAmount ?? 0;
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 10).Value = appraisedDate?.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 11).Value = appraisedBy;
                worksheet.Cell(currentRow, 12).Value = loanStatus;
                worksheet.Cell(currentRow, 13).Value = loan.AuditTime.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 14).Value = (loan.Status == (int)Status.Disbursed ? (loan.Aamount ?? loan.LoanAmt ?? 0) : 0);
                worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 15).Value = loanBalance;
                worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 16).Value = guarantorCount;

                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 5).Value = "TOTALS:";
            worksheet.Cell(currentRow, 5).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Value = totalApplied;
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 9).Value = totalAppraised;
            worksheet.Cell(currentRow, 9).Style.Font.SetBold();
            worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 14).Value = totalDisbursed;
            worksheet.Cell(currentRow, 14).Style.Font.SetBold();
            worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 15).Value = totalBalance;
            worksheet.Cell(currentRow, 15).Style.Font.SetBold();
            worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";

            // Statistics section
            currentRow += 3;
            worksheet.Cell(currentRow, 1).Value = "STATISTICS:";
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = "Total Loan Applications:";
            worksheet.Cell(currentRow, 2).Value = loansWithDetails.Count;
            worksheet.Cell(currentRow, 3).Value = "Approved Count:";
            worksheet.Cell(currentRow, 4).Value = approvedCount;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Disbursed Count:";
            worksheet.Cell(currentRow, 2).Value = disbursedCount;
            worksheet.Cell(currentRow, 3).Value = "Approved Rate (%):";
            worksheet.Cell(currentRow, 4).Value = totalApplied > 0 ? (totalAppraised / totalApplied) * 100 : 0;
            worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Disbursement Rate (%):";
            worksheet.Cell(currentRow, 2).Value = totalAppraised > 0 ? (totalDisbursed / totalAppraised) * 100 : 0;
            worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanApplicationDisbursementReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanApplicationDisbursementToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loans with member and appraisal data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && loan.ApplicDate >= startDate
                                              && loan.ApplicDate <= endDateAdjusted
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.LoanAmt,
                                              loan.RepayPeriod,
                                              loan.ApplicDate,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.Status,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberDob = member.Dob,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No loan data found for the selected date range";
                return RedirectToAction("LoanApplicationDisbursementReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var appraisals = await _context.Appraisal
                .Where(a => loanNos.Contains(a.LoanNo) && a.CompanyCode == companyCode)
                .ToDictionaryAsync(a => a.LoanNo, a => new { a.AmtRecommended, a.AppraisDate, a.AuditID });

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var guarantorCounts = await _context.Loanguar
                .Where(g => loanNos.Contains(g.LoanNo) && g.CompanyCode == companyCode && g.Transfered == false)
                .GroupBy(g => g.LoanNo)
                .Select(g => new { LoanNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.LoanNo, g => g.Count);

            // Create a strongly-typed list
            var reportData = new List<LoanApplicationDisbursementReportViewModel>();
            decimal totalApplied = 0, totalAppraised = 0, totalDisbursed = 0, totalBalance = 0;

            foreach (var loan in loansWithDetails)
            {
                // Calculate age group
                string ageGroup = "N/A";
                if (loan.MemberDob.HasValue)
                {
                    int age = DateTime.Today.Year - loan.MemberDob.Value.Year;
                    if (loan.MemberDob.Value.Date > DateTime.Today.AddYears(-age)) age--;
                    if (age <= 35) ageGroup = "Youth (18-35)";
                    else if (age <= 50) ageGroup = "Adult (36-50)";
                    else ageGroup = "Senior (50+)";
                }

                // Build member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                // Get appraisal details
                decimal? appraisedAmount = null;
                DateTime? appraisedDate = null;
                string appraisedBy = null;

                if (appraisals.ContainsKey(loan.LoanNo))
                {
                    var appraisal = appraisals[loan.LoanNo];
                    appraisedAmount = appraisal.AmtRecommended;
                    appraisedDate = appraisal.AppraisDate;
                    appraisedBy = appraisal.AuditID;
                }

                // Get loan balance
                decimal loanBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : 0;

                // Get guarantor count
                int guarantorCount = guarantorCounts.ContainsKey(loan.LoanNo) ? guarantorCounts[loan.LoanNo] : 0;

                // Get loan status string
                string loanStatus = GetLoanStatusString(loan.Status);

                totalApplied += loan.LoanAmt ?? 0;
                totalAppraised += appraisedAmount ?? 0;
                totalDisbursed += (loan.Status == (int)Status.Disbursed ? (loan.Aamount ?? loan.LoanAmt ?? 0) : 0);
                totalBalance += loanBalance;

                reportData.Add(new LoanApplicationDisbursementReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Name = fullName,
                    LoanNo = loan.LoanNo,
                    AgeGroup = ageGroup,
                    LoanType = loan.LoanTypeName,
                    AppliedAmount = loan.LoanAmt ?? 0,
                    Period = loan.RepayPeriod ?? 0,
                    ApplyDate = loan.ApplicDate,
                    AppraisedAmount = appraisedAmount,
                    AppraisedDate = appraisedDate,
                    AppraisedBy = appraisedBy ?? "-",
                    LoanStatus = loanStatus,
                    DisbursementAmount = (loan.Status == (int)Status.Disbursed ? (loan.Aamount ?? loan.LoanAmt ?? 0) : 0),
                    DisbursementDate = loan.AuditTime,
                    LoanBalance = loanBalance,
                    GuarantorCount = guarantorCount
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
                        header.Item().AlignCenter().Text($"LOAN APPLICATION & DISBURSEMENT REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.5f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.6f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Name").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Age").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanType").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Applied").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Period").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("App.Date").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Appraised Amt").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Appraised Date").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Appraised By").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Status").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Disb.Date").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Disb.Amt").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Guarantors").Bold().FontSize(7);
                        });

                        foreach (var loan in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.Name ?? "").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.AgeGroup).FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanType ?? "").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AppliedAmount:N0}").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.Period.ToString()).FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.ApplyDate.ToString("dd/MM/yyyy")).FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AppraisedAmount:N0}").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.AppraisedDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.AppraisedBy ?? "-").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.LoanStatus).FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DisbursementDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.DisbursementAmount:N0}").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanBalance:N0}").FontSize(6);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.GuarantorCount.ToString()).FontSize(6);
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
            return File(content, "application/pdf", $"LoanApplicationDisbursementReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }
        #endregion

        #region Loan Balance Report

        [HttpGet]
        public IActionResult LoanBalanceReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new LoanBalanceIndexViewModel
            {
                Loans = new List<LoanBalanceReportViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalUnpaidInterest = 0,
                TotalPaidInterest = 0,
                TotalAmountIssued = 0,
                TotalLoanBalance = 0,
                TotalAmountPaid = 0,
                TotalArrears = 0,
                TotalLoans = 0,
                ActiveLoansCount = 0,
                ClosedLoansCount = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanBalanceReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanBalanceReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with balances (disbursed or endorsed)
            var loansWithBalances = await (from loan in _context.Loans
                                           join member in _context.Members
                                               on loan.MemberNo equals member.MemberNo
                                           join loantype in _context.Loantypes
                                               on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                           from lt in loanTypeJoin.DefaultIfEmpty()
                                           where loan.CompanyCode == companyCode
                                               && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                               && loan.AuditTime <= asAtDateEnd
                                           select new
                                           {
                                               loan.MemberNo,
                                               loan.LoanNo,
                                               loan.LoanCode,
                                               loan.LoanAmt,
                                               loan.Aamount,
                                               loan.AuditTime,
                                               loan.Status,
                                               MemberSurname = member.Surname,
                                               MemberOtherNames = member.OtherNames,
                                               LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                           }).ToListAsync();

            if (!loansWithBalances.Any())
            {
                var emptyViewModel = new LoanBalanceIndexViewModel
                {
                    Loans = new List<LoanBalanceReportViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalUnpaidInterest = 0,
                    TotalPaidInterest = 0,
                    TotalAmountIssued = 0,
                    TotalLoanBalance = 0,
                    TotalAmountPaid = 0,
                    TotalArrears = 0,
                    TotalLoans = 0,
                    ActiveLoansCount = 0,
                    ClosedLoansCount = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanBalanceReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithBalances.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbals table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.IntrAmount,
                    lb.Penalty,
                    lb.IntBalance
                });

            // Get repayments (total paid principal and interest)
            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new
                {
                    r.TotalPrincipalPaid,
                    r.TotalInterestPaid,
                    r.TotalPenaltyPaid
                });

            var reportData = new List<LoanBalanceReportViewModel>();
            decimal totalUnpaidInterest = 0;
            decimal totalPaidInterest = 0;
            decimal totalAmountIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalAmountPaid = 0;
            decimal totalArrears = 0;
            int activeLoansCount = 0;

            foreach (var loan in loansWithBalances)
            {
                // Get member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                // Get loan balance details
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal interestAccrued = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    interestAccrued = lb.IntrAmount;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Get paid amounts
                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                // Amount issued (original loan amount)
                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;

                // Amount paid (principal only)
                decimal amountPaid = paidPrincipal;

                // Arrears (overdue amount - could be calculated from schedule or penalty)
                decimal arrears = penalty;

                // If loan has positive balance but no payments, it's active
                if (currentBalance > 0 || unpaidInterest > 0)
                {
                    activeLoansCount++;
                }

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                reportData.Add(new LoanBalanceReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Names = fullName,
                    LoanNo = loan.LoanNo,
                    LoanType = loan.LoanTypeName,
                    UnpaidInterest = unpaidInterest,
                    PaidInterest = paidInterest,
                    AmountIssued = amountIssued,
                    LoanBalance = currentBalance,
                    AmountPaid = amountPaid,
                    Arrears = arrears
                });
            }

            var viewModel = new LoanBalanceIndexViewModel
            {
                Loans = reportData.OrderBy(l => l.MemberNo).ToList(),
                TotalUnpaidInterest = totalUnpaidInterest,
                TotalPaidInterest = totalPaidInterest,
                TotalAmountIssued = totalAmountIssued,
                TotalLoanBalance = totalLoanBalance,
                TotalAmountPaid = totalAmountPaid,
                TotalArrears = totalArrears,
                TotalLoans = reportData.Count,
                ActiveLoansCount = activeLoansCount,
                ClosedLoansCount = reportData.Count - activeLoansCount,
                AsAtDate = asAtDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanBalanceReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalanceToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with balances
            var loansWithBalances = await (from loan in _context.Loans
                                           join member in _context.Members
                                               on loan.MemberNo equals member.MemberNo
                                           join loantype in _context.Loantypes
                                               on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                           from lt in loanTypeJoin.DefaultIfEmpty()
                                           where loan.CompanyCode == companyCode
                                               && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                               && loan.AuditTime <= asAtDateEnd
                                           select new
                                           {
                                               loan.MemberNo,
                                               loan.LoanNo,
                                               loan.LoanCode,
                                               loan.LoanAmt,
                                               loan.Aamount,
                                               loan.AuditTime,
                                               loan.Status,
                                               MemberSurname = member.Surname,
                                               MemberOtherNames = member.OtherNames,
                                               LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                           }).ToListAsync();

            if (!loansWithBalances.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalanceReport");
            }

            var loanNos = loansWithBalances.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.IntrAmount, lb.Penalty, lb.IntBalance });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid, r.TotalPenaltyPaid });

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loan Balances");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN BALANCES AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Names", "LoanNo", "LoanType", "Unpaid Interest", "Paid Interest",
                         "Amount Issued", "Loan Balance", "Amount Paid", "Arrears" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalUnpaidInterest = 0;
            decimal totalPaidInterest = 0;
            decimal totalAmountIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalAmountPaid = 0;
            decimal totalArrears = 0;

            foreach (var loan in loansWithBalances)
            {
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                decimal amountPaid = paidPrincipal;
                decimal arrears = penalty;

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                worksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 4).Value = loan.LoanTypeName;
                worksheet.Cell(currentRow, 5).Value = unpaidInterest;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 6).Value = paidInterest;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = amountIssued;
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 8).Value = currentBalance;
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 9).Value = amountPaid;
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 10).Value = arrears;
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 5).Value = totalUnpaidInterest;
            worksheet.Cell(currentRow, 5).Style.Font.SetBold();
            worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 6).Value = totalPaidInterest;
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 7).Value = totalAmountIssued;
            worksheet.Cell(currentRow, 7).Style.Font.SetBold();
            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 8).Value = totalLoanBalance;
            worksheet.Cell(currentRow, 8).Style.Font.SetBold();
            worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 9).Value = totalAmountPaid;
            worksheet.Cell(currentRow, 9).Style.Font.SetBold();
            worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 10).Value = totalArrears;
            worksheet.Cell(currentRow, 10).Style.Font.SetBold();
            worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanBalanceReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalanceToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with balances
            var loansWithBalances = await (from loan in _context.Loans
                                           join member in _context.Members
                                               on loan.MemberNo equals member.MemberNo
                                           join loantype in _context.Loantypes
                                               on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                           from lt in loanTypeJoin.DefaultIfEmpty()
                                           where loan.CompanyCode == companyCode
                                               && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                               && loan.AuditTime <= asAtDateEnd
                                           select new
                                           {
                                               loan.MemberNo,
                                               loan.LoanNo,
                                               loan.LoanCode,
                                               loan.LoanAmt,
                                               loan.Aamount,
                                               loan.AuditTime,
                                               loan.Status,
                                               MemberSurname = member.Surname,
                                               MemberOtherNames = member.OtherNames,
                                               LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                           }).ToListAsync();

            if (!loansWithBalances.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalanceReport");
            }

            var loanNos = loansWithBalances.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.IntrAmount, lb.Penalty, lb.IntBalance });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid, r.TotalPenaltyPaid });

            // Use strongly-typed LoanBalanceReportViewModel instead of dynamic
            var reportData = new List<LoanBalanceReportViewModel>();
            decimal totalUnpaidInterest = 0, totalPaidInterest = 0, totalAmountIssued = 0;
            decimal totalLoanBalance = 0, totalAmountPaid = 0, totalArrears = 0;

            foreach (var loan in loansWithBalances)
            {
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                decimal amountPaid = paidPrincipal;
                decimal arrears = penalty;

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                reportData.Add(new LoanBalanceReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Names = fullName,
                    LoanNo = loan.LoanNo,
                    LoanType = loan.LoanTypeName,
                    UnpaidInterest = unpaidInterest,
                    PaidInterest = paidInterest,
                    AmountIssued = amountIssued,
                    LoanBalance = currentBalance,
                    AmountPaid = amountPaid,
                    Arrears = arrears
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
                        header.Item().AlignCenter().Text($"LOAN BALANCES AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
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
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanType").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Unpaid Int").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Paid Int").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount Issued").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount Paid").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Arrears").Bold().FontSize(7);
                        });

                        // Use strongly-typed LoanBalanceReportViewModel
                        foreach (var loan in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.Names ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanType ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.UnpaidInterest:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.PaidInterest:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AmountIssued:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanBalance:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AmountPaid:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Arrears:N0}").FontSize(7);
                        }

                        // Totals row
                        table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalUnpaidInterest:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalPaidInterest:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmountIssued:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalLoanBalance:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmountPaid:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalArrears:N0}").Bold().FontSize(8);
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
            return File(content, "application/pdf", $"LoanBalanceReport_{asAtDate:yyyyMMdd}.pdf");
        }
        #endregion

        private string GetLoanStatusString(int? status)
        {
            if (!status.HasValue) return "Unknown";

            return status switch
            {
                (int)Status.Draft => "Draft",
                (int)Status.Submitted => "Submitted",
                (int)Status.UnderAppraisal => "Under Appraisal",
                (int)Status.Approved => "Approved",
                (int)Status.Endorsed => "Endorsed",
                (int)Status.Disbursed => "Disbursed",
                (int)Status.Closed => "Closed",
                (int)Status.Defaulted => "Defaulted",
                (int)Status.WrittenOff => "Written Off",
                (int)Status.Rejected => "Rejected",
                _ => "Unknown"
            };
        }

        #region Loan Balance Per Loan Report

        [HttpGet]
        public IActionResult LoanBalancePerLoanReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new LoanBalancePerLoanIndexViewModel
            {
                Loans = new List<LoanBalancePerLoanReportViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalUnpaidInterest = 0,
                TotalPaidInterest = 0,
                TotalAmountIssued = 0,
                TotalLoanBalance = 0,
                TotalAmountPaid = 0,
                TotalArrears = 0,
                TotalLoans = 0,
                ActiveLoansCount = 0,
                ClosedLoansCount = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanBalancePerLoanReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanBalancePerLoanReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with member and loan type data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              //&& (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.Status,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              ValueChain = lt != null ? lt.ValueChain : null
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                var emptyViewModel = new LoanBalancePerLoanIndexViewModel
                {
                    Loans = new List<LoanBalancePerLoanReportViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalUnpaidInterest = 0,
                    TotalPaidInterest = 0,
                    TotalAmountIssued = 0,
                    TotalLoanBalance = 0,
                    TotalAmountPaid = 0,
                    TotalArrears = 0,
                    TotalLoans = 0,
                    ActiveLoansCount = 0,
                    ClosedLoansCount = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanBalancePerLoanReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbals table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.IntrAmount,
                    lb.Penalty,
                    lb.IntBalance
                });

            // Get repayments (total paid principal and interest)
            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new
                {
                    r.TotalPrincipalPaid,
                    r.TotalInterestPaid,
                    r.TotalPenaltyPaid
                });

            var reportData = new List<LoanBalancePerLoanReportViewModel>();
            decimal totalUnpaidInterest = 0;
            decimal totalPaidInterest = 0;
            decimal totalAmountIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalAmountPaid = 0;
            decimal totalArrears = 0;
            int activeLoansCount = 0;

            foreach (var loan in loansWithDetails)
            {
                // Get member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                // Get loan balance details
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal interestAccrued = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    interestAccrued = lb.IntrAmount;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Get paid amounts
                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                // Amount issued (original loan amount)
                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;

                // Amount paid (principal only)
                decimal amountPaid = paidPrincipal;

                // Arrears (overdue amount - penalty)
                decimal arrears = penalty;

                // If loan has positive balance or unpaid interest, it's active
                if (currentBalance > 0 || unpaidInterest > 0)
                {
                    activeLoansCount++;
                }

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                reportData.Add(new LoanBalancePerLoanReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Names = fullName,
                    LoanNo = loan.LoanNo,
                    LoanName = loan.LoanTypeName,
                    LoanCode = loan.LoanCode ?? "-",
                    UnpaidInterest = unpaidInterest,
                    PaidInterest = paidInterest,
                    AmountIssued = amountIssued,
                    LoanBalance = currentBalance,
                    AmountPaid = amountPaid,
                    Arrears = arrears
                });
            }

            var viewModel = new LoanBalancePerLoanIndexViewModel
            {
                Loans = reportData.OrderBy(l => l.LoanName).ThenBy(l => l.MemberNo).ToList(),
                TotalUnpaidInterest = totalUnpaidInterest,
                TotalPaidInterest = totalPaidInterest,
                TotalAmountIssued = totalAmountIssued,
                TotalLoanBalance = totalLoanBalance,
                TotalAmountPaid = totalAmountPaid,
                TotalArrears = totalArrears,
                TotalLoans = reportData.Count,
                ActiveLoansCount = activeLoansCount,
                ClosedLoansCount = reportData.Count - activeLoansCount,
                AsAtDate = asAtDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanBalancePerLoanReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalancePerLoanToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with member and loan type data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.Status,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalancePerLoanReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.IntrAmount, lb.Penalty, lb.IntBalance });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid, r.TotalPenaltyPaid });

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loan Balances Per Loan");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN BALANCES PER LOAN AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Names", "LoanNo", "Loan Name", "Loan Code", "Unpaid Interest",
                         "Paid Interest", "Amount Issued", "Loan Balance", "Amount Paid", "Arrears" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalUnpaidInterest = 0;
            decimal totalPaidInterest = 0;
            decimal totalAmountIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalAmountPaid = 0;
            decimal totalArrears = 0;

            foreach (var loan in loansWithDetails)
            {
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                decimal amountPaid = paidPrincipal;
                decimal arrears = penalty;

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                worksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 4).Value = loan.LoanTypeName;
                worksheet.Cell(currentRow, 5).Value = loan.LoanCode ?? "-";
                worksheet.Cell(currentRow, 6).Value = unpaidInterest;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = paidInterest;
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 8).Value = amountIssued;
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 9).Value = currentBalance;
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 10).Value = amountPaid;
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 11).Value = arrears;
                worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";

                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 6).Value = totalUnpaidInterest;
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 7).Value = totalPaidInterest;
            worksheet.Cell(currentRow, 7).Style.Font.SetBold();
            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 8).Value = totalAmountIssued;
            worksheet.Cell(currentRow, 8).Style.Font.SetBold();
            worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 9).Value = totalLoanBalance;
            worksheet.Cell(currentRow, 9).Style.Font.SetBold();
            worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 10).Value = totalAmountPaid;
            worksheet.Cell(currentRow, 10).Style.Font.SetBold();
            worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 11).Value = totalArrears;
            worksheet.Cell(currentRow, 11).Style.Font.SetBold();
            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanBalancePerLoanReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalancePerLoanToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans with balances
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.AuditTime,
                                              loan.Status,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalancePerLoanReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.IntrAmount, lb.Penalty, lb.IntBalance });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid, r.TotalPenaltyPaid });

            var reportData = new List<LoanBalancePerLoanReportViewModel>();
            decimal totalUnpaidInterest = 0, totalPaidInterest = 0, totalAmountIssued = 0;
            decimal totalLoanBalance = 0, totalAmountPaid = 0, totalArrears = 0;

            foreach (var loan in loansWithDetails)
            {
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                decimal paidPrincipal = 0;
                decimal paidInterest = 0;

                if (repayments.ContainsKey(loan.LoanNo))
                {
                    var rp = repayments[loan.LoanNo];
                    paidPrincipal = rp.TotalPrincipalPaid;
                    paidInterest = rp.TotalInterestPaid;
                }

                decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                decimal amountPaid = paidPrincipal;
                decimal arrears = penalty;

                totalUnpaidInterest += unpaidInterest;
                totalPaidInterest += paidInterest;
                totalAmountIssued += amountIssued;
                totalLoanBalance += currentBalance;
                totalAmountPaid += amountPaid;
                totalArrears += arrears;

                reportData.Add(new LoanBalancePerLoanReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Names = fullName,
                    LoanNo = loan.LoanNo,
                    LoanName = loan.LoanTypeName,
                    LoanCode = loan.LoanCode ?? "-",
                    UnpaidInterest = unpaidInterest,
                    PaidInterest = paidInterest,
                    AmountIssued = amountIssued,
                    LoanBalance = currentBalance,
                    AmountPaid = amountPaid,
                    Arrears = arrears
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
                        header.Item().AlignCenter().Text($"LOAN BALANCES PER LOAN AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Name").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Code").Bold().FontSize(7);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Unpaid Int").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Paid Int").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount Issued").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount Paid").Bold().FontSize(6);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Arrears").Bold().FontSize(6);
                        });

                        foreach (var loan in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.Names ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanName ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanCode ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.UnpaidInterest:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.PaidInterest:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AmountIssued:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanBalance:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AmountPaid:N0}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.Arrears:N0}").FontSize(7);
                        }

                        // Totals row
                        table.Cell().ColumnSpan(5).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalUnpaidInterest:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalPaidInterest:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmountIssued:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalLoanBalance:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmountPaid:N0}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalArrears:N0}").Bold().FontSize(8);
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
            return File(content, "application/pdf", $"LoanBalancePerLoanReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Loan Balance Per Member Report

        [HttpGet]
        public IActionResult LoanBalancePerMemberReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new LoanBalancePerMemberIndexViewModel
            {
                Members = new List<LoanBalancePerMemberReportViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalMembers = 0,
                TotalActiveMembers = 0,
                TotalAmountIssued = 0,
                TotalLoanBalance = 0,
                TotalAmountPaid = 0,
                TotalUnpaidInterest = 0,
                TotalPaidInterest = 0,
                TotalArrears = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanBalancePerMemberReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanBalancePerMemberReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all members with their loans
            var membersWithLoans = await (from member in _context.Members
                                          join loan in _context.Loans on member.MemberNo equals loan.MemberNo into memberLoans
                                          from ml in memberLoans.DefaultIfEmpty()
                                          where member.CompanyCode == companyCode
                                              //&& (ml == null || (ml.Status == (int)Status.Disbursed || ml.Status == (int)Status.Endorsed))
                                              && (ml == null || ml.AuditTime <= asAtDateEnd)
                                          select new
                                          {
                                              member.MemberNo,
                                              member.Surname,
                                              member.OtherNames,
                                              member.FullName,
                                              member.Idno,
                                              member.PhoneNo,
                                              member.Cigcode,
                                              Loan = ml != null ? new
                                              {
                                                  ml.LoanNo,
                                                  ml.LoanCode,
                                                  ml.LoanAmt,
                                                  ml.Aamount,
                                                  ml.AuditTime,
                                                  ml.RepayPeriod,
                                                  ml.Status
                                              } : null
                                          }).ToListAsync();

            // Get all members with active loans
            var membersWithActiveLoans = membersWithLoans
                .Where(m => m.Loan != null)
                .GroupBy(m => m.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    Surname = g.First().Surname,
                    OtherNames = g.First().OtherNames,
                    FullName = g.First().FullName,
                    Idno = g.First().Idno,
                    PhoneNo = g.First().PhoneNo,
                    Cigcode = g.First().Cigcode,
                    Loans = g.Where(x => x.Loan != null).Select(x => x.Loan).ToList()
                })
                .ToList();

            if (!membersWithActiveLoans.Any())
            {
                var emptyViewModel = new LoanBalancePerMemberIndexViewModel
                {
                    Members = new List<LoanBalancePerMemberReportViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalMembers = 0,
                    TotalActiveMembers = 0,
                    TotalAmountIssued = 0,
                    TotalLoanBalance = 0,
                    TotalAmountPaid = 0,
                    TotalUnpaidInterest = 0,
                    TotalPaidInterest = 0,
                    TotalArrears = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanBalancePerMemberReport.cshtml", emptyViewModel);
            }

            // Get all loan numbers
            var allLoanNos = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanNo).ToList();

            // Get loan types for loan names
            var loanCodes = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanCode).Distinct().ToList();
            var loanTypes = await _context.Loantypes
                .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

            // Get loan balances from Loanbals table
            var loanBalances = await _context.Loanbal
                .Where(lb => allLoanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.IntrAmount,
                    lb.Penalty,
                    lb.IntBalance
                });

            // Get repayments (total paid principal and interest)
            var repayments = await _context.Repay
                .Where(r => allLoanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    TotalPenaltyPaid = g.Sum(r => r.Penalty ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new
                {
                    r.TotalPrincipalPaid,
                    r.TotalInterestPaid,
                    r.TotalPenaltyPaid
                });

            // Get GIG names
            var gigCodes = membersWithActiveLoans.Select(m => m.Cigcode).Distinct().ToList();
            var gigDetails = await _context.CIGs
                .Where(g => gigCodes.Contains(g.GigCode) && g.CompanyCode == companyCode && g.Status == "Active")
                .ToDictionaryAsync(g => g.GigCode, g => g.GigName);

            var memberReports = new List<LoanBalancePerMemberReportViewModel>();
            decimal totalAmountIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalAmountPaid = 0;
            decimal totalUnpaidInterest = 0;
            decimal totalPaidInterest = 0;
            decimal totalArrears = 0;
            int totalActiveMembers = 0;

            foreach (var member in membersWithActiveLoans)
            {
                // Build member name
                string fullName = member.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = member.MemberNo;

                // Get GIG name
                string gigName = member.Cigcode != null && gigDetails.ContainsKey(member.Cigcode)
                    ? gigDetails[member.Cigcode]
                    : (member.Cigcode ?? "Unassigned");

                var memberLoans = new List<MemberLoanDetail>();
                decimal memberAmountIssued = 0;
                decimal memberLoanBalance = 0;
                decimal memberAmountPaid = 0;
                decimal memberUnpaidInterest = 0;
                decimal memberPaidInterest = 0;
                decimal memberArrears = 0;
                int activeLoanCount = 0;
                int completedLoanCount = 0;

                foreach (var loan in member.Loans)
                {
                    // Get loan name from loantype
                    string loanName = loan.LoanCode != null && loanTypes.ContainsKey(loan.LoanCode)
                        ? loanTypes[loan.LoanCode]
                        : (loan.LoanCode ?? "");

                    // Get loan balance details
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;
                    decimal penalty = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                        penalty = lb.Penalty;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Get paid amounts
                    decimal paidPrincipal = 0;
                    decimal paidInterest = 0;

                    if (repayments.ContainsKey(loan.LoanNo))
                    {
                        var rp = repayments[loan.LoanNo];
                        paidPrincipal = rp.TotalPrincipalPaid;
                        paidInterest = rp.TotalInterestPaid;
                    }

                    // Amount issued (original loan amount)
                    decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                    decimal amountPaid = paidPrincipal;
                    decimal arrears = penalty;

                    // Determine loan status
                    string loanStatus = currentBalance <= 0 ? "COMPLETED" : "ACTIVE";

                    if (loanStatus == "ACTIVE")
                    {
                        activeLoanCount++;
                    }
                    else
                    {
                        completedLoanCount++;
                    }

                    memberAmountIssued += amountIssued;
                    memberLoanBalance += currentBalance;
                    memberAmountPaid += amountPaid;
                    memberUnpaidInterest += unpaidInterest;
                    memberPaidInterest += paidInterest;
                    memberArrears += arrears;

                    memberLoans.Add(new MemberLoanDetail
                    {
                        LoanNo = loan.LoanNo,
                        LoanName = loanName,
                        LoanCode = loan.LoanCode ?? "-",
                        DateIssued = loan.AuditTime,
                        RepayPeriod = loan.RepayPeriod,
                        AmountIssued = amountIssued,
                        LoanBalance = currentBalance,
                        AmountPaid = amountPaid,
                        UnpaidInterest = unpaidInterest,
                        PaidInterest = paidInterest,
                        Arrears = arrears,
                        Status = loanStatus
                    });
                }

                // Only include members with active loans (balance > 0)
                if (memberLoanBalance > 0 || memberUnpaidInterest > 0)
                {
                    totalActiveMembers++;
                    totalAmountIssued += memberAmountIssued;
                    totalLoanBalance += memberLoanBalance;
                    totalAmountPaid += memberAmountPaid;
                    totalUnpaidInterest += memberUnpaidInterest;
                    totalPaidInterest += memberPaidInterest;
                    totalArrears += memberArrears;

                    memberReports.Add(new LoanBalancePerMemberReportViewModel
                    {
                        MemberNo = member.MemberNo,
                        Names = fullName,
                        IDNo = member.Idno ?? "-",
                        PhoneNo = member.PhoneNo ?? "-",
                        GigCode = member.Cigcode ?? "-",
                        GigName = gigName,
                        TotalLoans = memberLoans.Count,
                        ActiveLoans = activeLoanCount,
                        CompletedLoans = completedLoanCount,
                        TotalAmountIssued = memberAmountIssued,
                        TotalLoanBalance = memberLoanBalance,
                        TotalAmountPaid = memberAmountPaid,
                        TotalUnpaidInterest = memberUnpaidInterest,
                        TotalPaidInterest = memberPaidInterest,
                        TotalArrears = memberArrears,
                        Loans = memberLoans.OrderByDescending(l => l.DateIssued).ToList()
                    });
                }
            }

            var viewModel = new LoanBalancePerMemberIndexViewModel
            {
                Members = memberReports.OrderBy(m => m.Names).ToList(),
                TotalMembers = memberReports.Count,
                TotalActiveMembers = totalActiveMembers,
                TotalAmountIssued = totalAmountIssued,
                TotalLoanBalance = totalLoanBalance,
                TotalAmountPaid = totalAmountPaid,
                TotalUnpaidInterest = totalUnpaidInterest,
                TotalPaidInterest = totalPaidInterest,
                TotalArrears = totalArrears,
                AsAtDate = asAtDate,
                HasData = memberReports.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanBalancePerMemberReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalancePerMemberToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all members with their loans
            var membersWithLoans = await (from member in _context.Members
                                          join loan in _context.Loans on member.MemberNo equals loan.MemberNo into memberLoans
                                          from ml in memberLoans.DefaultIfEmpty()
                                          where member.CompanyCode == companyCode
                                              && (ml == null || (ml.Status == (int)Status.Disbursed || ml.Status == (int)Status.Endorsed))
                                              && (ml == null || ml.AuditTime <= asAtDateEnd)
                                          select new
                                          {
                                              member.MemberNo,
                                              member.Surname,
                                              member.OtherNames,
                                              member.FullName,
                                              member.Idno,
                                              member.PhoneNo,
                                              member.Cigcode,
                                              Loan = ml != null ? new
                                              {
                                                  ml.LoanNo,
                                                  ml.LoanCode,
                                                  ml.LoanAmt,
                                                  ml.Aamount,
                                                  ml.AuditTime,
                                                  ml.RepayPeriod,
                                                  ml.Status
                                              } : null
                                          }).ToListAsync();

            var membersWithActiveLoans = membersWithLoans
                .Where(m => m.Loan != null)
                .GroupBy(m => m.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    Surname = g.First().Surname,
                    OtherNames = g.First().OtherNames,
                    FullName = g.First().FullName,
                    Idno = g.First().Idno,
                    PhoneNo = g.First().PhoneNo,
                    Cigcode = g.First().Cigcode,
                    Loans = g.Where(x => x.Loan != null).Select(x => x.Loan).ToList()
                })
                .ToList();

            if (!membersWithActiveLoans.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalancePerMemberReport");
            }

            var allLoanNos = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanNo).ToList();
            var loanCodes = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanCode).Distinct().ToList();

            var loanTypes = await _context.Loantypes
                .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

            var loanBalances = await _context.Loanbal
                .Where(lb => allLoanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var repayments = await _context.Repay
                .Where(r => allLoanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid });

            using var workbook = new XLWorkbook();

            // Summary Worksheet
            var summarySheet = workbook.Worksheets.Add("Summary");
            int currentRow = 1;

            summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            summarySheet.Range(currentRow, 1, currentRow, 10).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"LOAN BALANCES PER MEMBER AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            summarySheet.Range(currentRow, 1, currentRow, 10).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            summarySheet.Range(currentRow, 1, currentRow, 10).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Summary Headers
            string[] summaryHeaders = { "MemberNo", "Names", "IDNo", "PhoneNo", "GIG", "Total Loans", "Active Loans",
                                "Completed", "Total Issued", "Balance", "Amount Paid", "Unpaid Int", "Paid Int", "Arrears" };

            for (int i = 0; i < summaryHeaders.Length; i++)
            {
                summarySheet.Cell(currentRow, i + 1).Value = summaryHeaders[i];
                summarySheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                summarySheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                summarySheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            currentRow++;

            decimal totalIssued = 0, totalBalance = 0, totalPaid = 0, totalUnpaidInt = 0, totalPaidInt = 0, totalArrears = 0;

            foreach (var member in membersWithActiveLoans)
            {
                string fullName = member.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = member.MemberNo;

                decimal memberIssued = 0, memberBalance = 0, memberPaid = 0, memberUnpaidInt = 0, memberPaidInt = 0, memberArrears = 0;
                int activeCount = 0, completedCount = 0;

                foreach (var loan in member.Loans)
                {
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal penalty = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Penalty : 0;
                    decimal paidPrincipal = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalPrincipalPaid : 0;
                    decimal paidInterest = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalInterestPaid : 0;
                    decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;

                    if (currentBalance > 0) activeCount++; else completedCount++;

                    memberIssued += amountIssued;
                    memberBalance += currentBalance;
                    memberPaid += paidPrincipal;
                    memberUnpaidInt += unpaidInterest;
                    memberPaidInt += paidInterest;
                    memberArrears += penalty;
                }

                if (memberBalance > 0 || memberUnpaidInt > 0)
                {
                    totalIssued += memberIssued;
                    totalBalance += memberBalance;
                    totalPaid += memberPaid;
                    totalUnpaidInt += memberUnpaidInt;
                    totalPaidInt += memberPaidInt;
                    totalArrears += memberArrears;

                    summarySheet.Cell(currentRow, 1).Value = member.MemberNo;
                    summarySheet.Cell(currentRow, 2).Value = fullName;
                    summarySheet.Cell(currentRow, 3).Value = member.Idno ?? "-";
                    summarySheet.Cell(currentRow, 4).Value = member.PhoneNo ?? "-";
                    summarySheet.Cell(currentRow, 5).Value = member.Cigcode ?? "-";
                    summarySheet.Cell(currentRow, 6).Value = member.Loans.Count;
                    summarySheet.Cell(currentRow, 7).Value = activeCount;
                    summarySheet.Cell(currentRow, 8).Value = completedCount;
                    summarySheet.Cell(currentRow, 9).Value = memberIssued;
                    summarySheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(currentRow, 10).Value = memberBalance;
                    summarySheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(currentRow, 11).Value = memberPaid;
                    summarySheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(currentRow, 12).Value = memberUnpaidInt;
                    summarySheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(currentRow, 13).Value = memberPaidInt;
                    summarySheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";
                    summarySheet.Cell(currentRow, 14).Value = memberArrears;
                    summarySheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
                    currentRow++;
                }
            }

            // Summary Totals
            currentRow++;
            summarySheet.Cell(currentRow, 8).Value = "GRAND TOTAL:";
            summarySheet.Cell(currentRow, 8).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 9).Value = totalIssued;
            summarySheet.Cell(currentRow, 9).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 10).Value = totalBalance;
            summarySheet.Cell(currentRow, 10).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 11).Value = totalPaid;
            summarySheet.Cell(currentRow, 11).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 12).Value = totalUnpaidInt;
            summarySheet.Cell(currentRow, 12).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 13).Value = totalPaidInt;
            summarySheet.Cell(currentRow, 13).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 14).Value = totalArrears;
            summarySheet.Cell(currentRow, 14).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";

            summarySheet.Columns().AdjustToContents();

            // Detailed Loans Worksheet
            var detailSheet = workbook.Worksheets.Add("Loan Details");
            currentRow = 1;

            detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            detailSheet.Range(currentRow, 1, currentRow, 12).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            detailSheet.Cell(currentRow, 1).Value = $"LOAN DETAILS PER MEMBER AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            detailSheet.Range(currentRow, 1, currentRow, 12).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] detailHeaders = { "MemberNo", "Member Name", "LoanNo", "Loan Name", "Loan Code", "Date Issued",
                               "Period", "Amount Issued", "Loan Balance", "Amount Paid", "Unpaid Interest", "Status" };

            for (int i = 0; i < detailHeaders.Length; i++)
            {
                detailSheet.Cell(currentRow, i + 1).Value = detailHeaders[i];
                detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            currentRow++;

            foreach (var member in membersWithActiveLoans)
            {
                string fullName = member.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = member.MemberNo;

                foreach (var loan in member.Loans)
                {
                    string loanName = loan.LoanCode != null && loanTypes.ContainsKey(loan.LoanCode) ? loanTypes[loan.LoanCode] : (loan.LoanCode ?? "");
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal paidPrincipal = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalPrincipalPaid : 0;
                    decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;
                    string status = currentBalance <= 0 ? "COMPLETED" : "ACTIVE";

                    detailSheet.Cell(currentRow, 1).Value = member.MemberNo;
                    detailSheet.Cell(currentRow, 2).Value = fullName;
                    detailSheet.Cell(currentRow, 3).Value = loan.LoanNo;
                    detailSheet.Cell(currentRow, 4).Value = loanName;
                    detailSheet.Cell(currentRow, 5).Value = loan.LoanCode ?? "-";
                    detailSheet.Cell(currentRow, 6).Value = loan.AuditTime.ToString("dd/MM/yyyy");
                    detailSheet.Cell(currentRow, 7).Value = loan.RepayPeriod ?? 0;
                    detailSheet.Cell(currentRow, 8).Value = amountIssued;
                    detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 9).Value = currentBalance;
                    detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 10).Value = paidPrincipal;
                    detailSheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 11).Value = unpaidInterest;
                    detailSheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 12).Value = status;
                    currentRow++;
                }
            }

            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanBalancePerMemberReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanBalancePerMemberToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all members with their loans
            var membersWithLoans = await (from member in _context.Members
                                          join loan in _context.Loans on member.MemberNo equals loan.MemberNo into memberLoans
                                          from ml in memberLoans.DefaultIfEmpty()
                                          where member.CompanyCode == companyCode
                                              && (ml == null || (ml.Status == (int)Status.Disbursed || ml.Status == (int)Status.Endorsed))
                                              && (ml == null || ml.AuditTime <= asAtDateEnd)
                                          select new
                                          {
                                              member.MemberNo,
                                              member.Surname,
                                              member.OtherNames,
                                              member.FullName,
                                              member.Idno,
                                              member.PhoneNo,
                                              member.Cigcode,
                                              Loan = ml != null ? new
                                              {
                                                  ml.LoanNo,
                                                  ml.LoanCode,
                                                  ml.LoanAmt,
                                                  ml.Aamount,
                                                  ml.AuditTime,
                                                  ml.RepayPeriod,
                                                  ml.Status
                                              } : null
                                          }).ToListAsync();

            var membersWithActiveLoans = membersWithLoans
                .Where(m => m.Loan != null)
                .GroupBy(m => m.MemberNo)
                .Select(g => new
                {
                    MemberNo = g.Key,
                    Surname = g.First().Surname,
                    OtherNames = g.First().OtherNames,
                    FullName = g.First().FullName,
                    Idno = g.First().Idno,
                    PhoneNo = g.First().PhoneNo,
                    Cigcode = g.First().Cigcode,
                    Loans = g.Where(x => x.Loan != null).Select(x => x.Loan).ToList()
                })
                .ToList();

            if (!membersWithActiveLoans.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanBalancePerMemberReport");
            }

            var allLoanNos = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanNo).ToList();
            var loanCodes = membersWithActiveLoans.SelectMany(m => m.Loans).Select(l => l.LoanCode).Distinct().ToList();

            var loanTypes = await _context.Loantypes
                .Where(lt => loanCodes.Contains(lt.LoanCode) && lt.CompanyCode == companyCode)
                .ToDictionaryAsync(lt => lt.LoanCode, lt => lt.LoanType1);

            var loanBalances = await _context.Loanbal
                .Where(lb => allLoanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var repayments = await _context.Repay
                .Where(r => allLoanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(r => r.LoanNo, r => new { r.TotalPrincipalPaid, r.TotalInterestPaid });

            var reportData = new List<LoanBalancePerMemberReportViewModel>();
            decimal totalIssued = 0, totalBalance = 0, totalPaid = 0, totalUnpaidInt = 0, totalPaidInt = 0, totalArrears = 0;

            foreach (var member in membersWithActiveLoans)
            {
                string fullName = member.FullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = member.MemberNo;

                var memberLoans = new List<MemberLoanDetail>();
                decimal memberIssued = 0, memberBalance = 0, memberPaid = 0, memberUnpaidInt = 0, memberPaidInt = 0, memberArrears = 0;
                int activeCount = 0, completedCount = 0;

                foreach (var loan in member.Loans)
                {
                    string loanName = loan.LoanCode != null && loanTypes.ContainsKey(loan.LoanCode) ? loanTypes[loan.LoanCode] : (loan.LoanCode ?? "");
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal penalty = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Penalty : 0;
                    decimal paidPrincipal = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalPrincipalPaid : 0;
                    decimal paidInterest = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalInterestPaid : 0;
                    decimal amountIssued = loan.LoanAmt ?? loan.Aamount ?? 0;

                    if (currentBalance > 0) activeCount++; else completedCount++;

                    memberIssued += amountIssued;
                    memberBalance += currentBalance;
                    memberPaid += paidPrincipal;
                    memberUnpaidInt += unpaidInterest;
                    memberPaidInt += paidInterest;
                    memberArrears += penalty;

                    memberLoans.Add(new MemberLoanDetail
                    {
                        LoanNo = loan.LoanNo,
                        LoanName = loanName,
                        LoanCode = loan.LoanCode ?? "-",
                        DateIssued = loan.AuditTime,
                        RepayPeriod = loan.RepayPeriod,
                        AmountIssued = amountIssued,
                        LoanBalance = currentBalance,
                        AmountPaid = paidPrincipal,
                        UnpaidInterest = unpaidInterest,
                        PaidInterest = paidInterest,
                        Arrears = penalty,
                        Status = currentBalance <= 0 ? "COMPLETED" : "ACTIVE"
                    });
                }

                if (memberBalance > 0 || memberUnpaidInt > 0)
                {
                    totalIssued += memberIssued;
                    totalBalance += memberBalance;
                    totalPaid += memberPaid;
                    totalUnpaidInt += memberUnpaidInt;
                    totalPaidInt += memberPaidInt;
                    totalArrears += memberArrears;

                    reportData.Add(new LoanBalancePerMemberReportViewModel
                    {
                        MemberNo = member.MemberNo,
                        Names = fullName,
                        IDNo = member.Idno ?? "-",
                        PhoneNo = member.PhoneNo ?? "-",
                        GigCode = member.Cigcode ?? "-",
                        TotalLoans = memberLoans.Count,
                        ActiveLoans = activeCount,
                        CompletedLoans = completedCount,
                        TotalAmountIssued = memberIssued,
                        TotalLoanBalance = memberBalance,
                        TotalAmountPaid = memberPaid,
                        TotalUnpaidInterest = memberUnpaidInt,
                        TotalPaidInterest = memberPaidInt,
                        TotalArrears = memberArrears,
                        Loans = memberLoans
                    });
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
                        header.Item().AlignCenter().Text($"LOAN BALANCES PER MEMBER AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    // Summary Statistics
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

                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Members:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loan Balance:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalBalance:N0}");

                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount Issued:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalIssued:N0}");
                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Arrears:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalArrears:N0}");
                        });

                        // Member Summary Table
                        contentCol.Item().PaddingTop(1, Unit.Centimetre);
                        contentCol.Item().Text("MEMBER SUMMARY").FontSize(11).Bold();

                        contentCol.Item().Table(memberTable =>
                        {
                            memberTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(0.8f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(0.6f);
                                cols.RelativeColumn(0.6f);
                                cols.RelativeColumn(0.6f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                            });

                            memberTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Name").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Loans").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Active").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Completed").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount Issued").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Arrears").Bold().FontSize(8);
                            });

                            foreach (var member in reportData)
                            {
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member.MemberNo ?? "").FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).Text(member.Names ?? "").FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.TotalLoans.ToString()).FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.ActiveLoans.ToString()).FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(member.CompletedLoans.ToString()).FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalAmountIssued:N0}").FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalLoanBalance:N0}").FontSize(7);
                                memberTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{member.TotalArrears:N0}").FontSize(7);
                            }

                            memberTable.Cell().ColumnSpan(5).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(8);
                            memberTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalIssued:N0}").Bold().FontSize(8);
                            memberTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalBalance:N0}").Bold().FontSize(8);
                            memberTable.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalArrears:N0}").Bold().FontSize(8);
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
            return File(content, "application/pdf", $"LoanBalancePerMemberReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion


        #region Loans Due Report

        [HttpGet]
        public IActionResult LoansDueReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now;
            var endDate = DateTime.Now.AddMonths(1);

            var viewModel = new LoanDueIndexViewModel
            {
                Loans = new List<LoanDueReportViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalIntrOwed = 0,
                TotalAmount = 0,
                TotalLoanBalance = 0,
                TotalRepayRate = 0,
                TotalLoans = 0,
                OverdueLoansCount = 0,
                TotalOverdueAmount = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoansDueReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoansDueReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loan balances from Loanbal table with due dates in range
            var loanBalances = await (from lb in _context.Loanbal
                                      join member in _context.Members
                                          on lb.MemberNo equals member.MemberNo
                                      join loantype in _context.Loantypes
                                          on lb.LoanCode equals loantype.LoanCode into loanTypeJoin
                                      from lt in loanTypeJoin.DefaultIfEmpty()
                                      where lb.Companycode == companyCode
                                          && lb.Duedate >= startDate
                                          && lb.Duedate <= endDateAdjusted
                                          && lb.Balance > 0
                                      select new
                                      {
                                          lb.MemberNo,
                                          lb.LoanNo,
                                          lb.LoanCode,
                                          lb.Balance,
                                          lb.IntrOwed,
                                          lb.RepayRate,
                                          lb.Duedate,
                                          lb.Penalty,
                                          MemberSurname = member.Surname,
                                          MemberOtherNames = member.OtherNames,
                                          MemberFullName = member.FullName,
                                          LoanName = lt != null ? lt.LoanType1 : (lb.LoanCode ?? "Unknown")
                                      }).ToListAsync();

            if (!loanBalances.Any())
            {
                var emptyViewModel = new LoanDueIndexViewModel
                {
                    Loans = new List<LoanDueReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalIntrOwed = 0,
                    TotalAmount = 0,
                    TotalLoanBalance = 0,
                    TotalRepayRate = 0,
                    TotalLoans = 0,
                    OverdueLoansCount = 0,
                    TotalOverdueAmount = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No loans due found for the selected date range.";

                return View("~/Views/Reports/LoansDueReport.cshtml", emptyViewModel);
            }

            var reportData = new List<LoanDueReportViewModel>();
            decimal totalIntrOwed = 0;
            decimal totalAmount = 0;
            decimal totalLoanBalance = 0;
            decimal totalRepayRate = 0;
            int overdueCount = 0;
            decimal totalOverdueAmount = 0;
            var today = DateTime.Today;

            foreach (var loan in loanBalances)
            {
                // Build member name
                string fullName = loan.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                // Calculate total (IntrOwed + current installment or balance)
                decimal total = loan.IntrOwed + (loan.RepayRate > 0 ? loan.RepayRate : 0);

                // Calculate days overdue
                int daysOverdue = 0;
                if (loan.Duedate < today)
                {
                    daysOverdue = (today - loan.Duedate).Days;
                    overdueCount++;
                    totalOverdueAmount += loan.Balance;
                }

                totalIntrOwed += loan.IntrOwed;
                totalAmount += total;
                totalLoanBalance += loan.Balance;
                totalRepayRate += loan.RepayRate;

                reportData.Add(new LoanDueReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    LoanNo = loan.LoanNo,
                    Names = fullName,
                    RepayRate = loan.RepayRate,
                    IntrOwed = loan.IntrOwed,
                    Total = total,
                    DueDate = loan.Duedate,
                    LoanBalance = loan.Balance,
                    LoanName = loan.LoanName,
                    Penalty = loan.Penalty,
                    DaysOverdue = daysOverdue
                });
            }

            var viewModel = new LoanDueIndexViewModel
            {
                Loans = reportData.OrderBy(l => l.DueDate).ThenBy(l => l.MemberNo).ToList(),
                TotalIntrOwed = totalIntrOwed,
                TotalAmount = totalAmount,
                TotalLoanBalance = totalLoanBalance,
                TotalRepayRate = totalRepayRate,
                TotalLoans = reportData.Count,
                OverdueLoansCount = overdueCount,
                TotalOverdueAmount = totalOverdueAmount,
                StartDate = startDate,
                EndDate = endDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoansDueReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoansDueToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loan balances from Loanbal table
            var loanBalances = await (from lb in _context.Loanbal
                                      join member in _context.Members
                                          on lb.MemberNo equals member.MemberNo
                                      join loantype in _context.Loantypes
                                          on lb.LoanCode equals loantype.LoanCode into loanTypeJoin
                                      from lt in loanTypeJoin.DefaultIfEmpty()
                                      where lb.Companycode == companyCode
                                          && lb.Duedate >= startDate
                                          && lb.Duedate <= endDateAdjusted
                                          && lb.Balance > 0
                                      select new
                                      {
                                          lb.MemberNo,
                                          lb.LoanNo,
                                          lb.LoanCode,
                                          lb.Balance,
                                          lb.IntrOwed,
                                          lb.RepayRate,
                                          lb.Duedate,
                                          lb.Penalty,
                                          MemberSurname = member.Surname,
                                          MemberOtherNames = member.OtherNames,
                                          MemberFullName = member.FullName,
                                          LoanName = lt != null ? lt.LoanType1 : (lb.LoanCode ?? "Unknown")
                                      }).ToListAsync();

            if (!loanBalances.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansDueReport");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loans Due");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOANS DUE REPORT";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 10).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            // Headers - matching the image format
            string[] headers = { "Member No", "Loan No", "Names", "RepayRate", "IntrOwed", "Total", "Due Date", "Loan Balance", "Loan Name", "Days Overdue" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalIntrOwed = 0;
            decimal totalAmount = 0;
            decimal totalLoanBalance = 0;
            decimal totalRepayRate = 0;
            var today = DateTime.Today;

            foreach (var loan in loanBalances)
            {
                // Build member name
                string fullName = loan.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal total = loan.IntrOwed + (loan.RepayRate > 0 ? loan.RepayRate : 0);
                int daysOverdue = loan.Duedate < today ? (today - loan.Duedate).Days : 0;

                totalIntrOwed += loan.IntrOwed;
                totalAmount += total;
                totalLoanBalance += loan.Balance;
                totalRepayRate += loan.RepayRate;

                worksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 2).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 3).Value = fullName;
                worksheet.Cell(currentRow, 4).Value = loan.RepayRate;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Value = loan.IntrOwed;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 6).Value = total;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = loan.Duedate.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 8).Value = loan.Balance;
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 9).Value = loan.LoanName;
                worksheet.Cell(currentRow, 10).Value = daysOverdue;
                worksheet.Cell(currentRow, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 3).Value = "TOTAL:";
            worksheet.Cell(currentRow, 3).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Value = totalRepayRate;
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 5).Value = totalIntrOwed;
            worksheet.Cell(currentRow, 5).Style.Font.SetBold();
            worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 6).Value = totalAmount;
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 8).Value = totalLoanBalance;
            worksheet.Cell(currentRow, 8).Style.Font.SetBold();
            worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoansDueReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoansDueToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get loan balances from Loanbal table
            var loanBalances = await (from lb in _context.Loanbal
                                      join member in _context.Members
                                          on lb.MemberNo equals member.MemberNo
                                      join loantype in _context.Loantypes
                                          on lb.LoanCode equals loantype.LoanCode into loanTypeJoin
                                      from lt in loanTypeJoin.DefaultIfEmpty()
                                      where lb.Companycode == companyCode
                                          && lb.Duedate >= startDate
                                          && lb.Duedate <= endDateAdjusted
                                          && lb.Balance > 0
                                      select new
                                      {
                                          lb.MemberNo,
                                          lb.LoanNo,
                                          lb.LoanCode,
                                          lb.Balance,
                                          lb.IntrOwed,
                                          lb.RepayRate,
                                          lb.Duedate,
                                          lb.Penalty,
                                          MemberSurname = member.Surname,
                                          MemberOtherNames = member.OtherNames,
                                          MemberFullName = member.FullName,
                                          LoanName = lt != null ? lt.LoanType1 : (lb.LoanCode ?? "Unknown")
                                      }).ToListAsync();

            if (!loanBalances.Any())
            {
                TempData["Error"] = "No data found for the selected date range";
                return RedirectToAction("LoansDueReport");
            }

            var reportData = new List<LoanDueReportViewModel>();
            decimal totalIntrOwed = 0, totalAmount = 0, totalLoanBalance = 0, totalRepayRate = 0;
            var today = DateTime.Today;

            foreach (var loan in loanBalances)
            {
                string fullName = loan.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                decimal total = loan.IntrOwed + (loan.RepayRate > 0 ? loan.RepayRate : 0);
                int daysOverdue = loan.Duedate < today ? (today - loan.Duedate).Days : 0;

                totalIntrOwed += loan.IntrOwed;
                totalAmount += total;
                totalLoanBalance += loan.Balance;
                totalRepayRate += loan.RepayRate;

                reportData.Add(new LoanDueReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    LoanNo = loan.LoanNo,
                    Names = fullName,
                    RepayRate = loan.RepayRate,
                    IntrOwed = loan.IntrOwed,
                    Total = total,
                    DueDate = loan.Duedate,
                    LoanBalance = loan.Balance,
                    LoanName = loan.LoanName,
                    DaysOverdue = daysOverdue
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
                        header.Item().AlignCenter().Text($"LOANS DUE REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    // Summary Statistics
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

                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loans Due:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Overdue Loans:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count(l => l.DaysOverdue > 0).ToString());

                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Due Amount:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalAmount:N0}");
                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loan Balance:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalLoanBalance:N0}");
                        });

                        // Loans Due Table
                        contentCol.Item().PaddingTop(1, Unit.Centimetre);
                        contentCol.Item().Text("LOANS DUE DETAILS").FontSize(11).Bold();

                        contentCol.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(0.8f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(0.9f);
                                cols.RelativeColumn(0.9f);
                                cols.RelativeColumn(0.9f);
                                cols.RelativeColumn(0.9f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(0.6f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan No").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("RepayRate").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("IntrOwed").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Due Date").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Balance").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Name").Bold().FontSize(7);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Overdue").Bold().FontSize(7);
                            });

                            foreach (var loan in reportData.OrderBy(l => l.DueDate))
                            {
                                string rowBg = loan.DaysOverdue > 0 ? "#ffe6e6" : "white";

                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).Text(loan.MemberNo ?? "").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).Text(loan.LoanNo ?? "").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).Text(loan.Names ?? "").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignRight().Text($"{loan.RepayRate:N0}").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignRight().Text($"{loan.IntrOwed:N0}").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignRight().Text($"{loan.Total:N0}").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignCenter().Text(loan.DueDate.ToString("dd/MM/yyyy")).FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignRight().Text($"{loan.LoanBalance:N0}").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).Text(loan.LoanName ?? "").FontSize(7);
                                table.Cell().Border(0.2f).Padding(4).Background(rowBg).AlignCenter().Text(loan.DaysOverdue > 0 ? $"{loan.DaysOverdue}d" : "-").FontSize(7);
                            }

                            // Totals row
                            table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(8);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalRepayRate:N0}").Bold().FontSize(8);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalIntrOwed:N0}").Bold().FontSize(8);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmount:N0}").Bold().FontSize(8);
                            table.Cell().ColumnSpan(1).Border(0.2f).Background("#f9f9f9").Padding(4);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalLoanBalance:N0}").Bold().FontSize(8);
                            table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4);
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
            return File(content, "application/pdf", $"LoansDueReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Interest Control Listings Report

        [HttpGet]
        public IActionResult InterestListingReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            var viewModel = new InterestListingIndexViewModel
            {
                Interests = new List<InterestListingReportViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalInterest = 0,
                TotalTransactions = 0,
                UniqueMembers = 0
            };

            return View("~/Views/Reports/InterestListingReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> InterestListingReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // First get all potential interest transactions
            var interestTransactions = await (from gt in _context.Gltransactions
                                              join gs in _context.GlSetup on gt.DrAccNo equals gs.AccNo into drJoin
                                              from dr in drJoin.DefaultIfEmpty()
                                              join gs2 in _context.GlSetup on gt.CrAccNo equals gs2.AccNo into crJoin
                                              from cr in crJoin.DefaultIfEmpty()
                                              where gt.CompanyCode == companyCode
                                                  && gt.AuditTime >= startDate
                                                  && gt.AuditTime <= endDateAdjusted
                                                  && (dr != null || cr != null)
                                                  && ((dr != null && dr.GlAccMainGroup == "INCOME" && dr.Glaccname != null &&
                                                      (dr.Glaccname.ToLower().Contains("interest") || dr.Glaccname.ToLower().Contains("intrest")))
                                                      || (cr != null && cr.GlAccMainGroup == "INCOME" && cr.Glaccname != null &&
                                                      (cr.Glaccname.ToLower().Contains("interest") || cr.Glaccname.ToLower().Contains("intrest"))))
                                              select new
                                              {
                                                  gt.Source,
                                                  gt.Amount,
                                                  gt.TransDescript,
                                                  gt.AuditTime,
                                                  gt.DocumentNo,
                                                  gt.TransactionNo
                                              }).ToListAsync();

            if (!interestTransactions.Any())
            {
                var emptyViewModel = new InterestListingIndexViewModel
                {
                    Interests = new List<InterestListingReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalInterest = 0,
                    TotalTransactions = 0,
                    UniqueMembers = 0
                };

                ViewBag.Message = "No interest transactions found for the selected date range.";
                return View("~/Views/Reports/InterestListingReport.cshtml", emptyViewModel);
            }

            // Extract loan numbers from descriptions and get member info
            var reportData = new List<InterestListingReportViewModel>();
            decimal totalInterest = 0;
            var uniqueMembers = new HashSet<string>();

            foreach (var transaction in interestTransactions)
            {
                // Extract Loan Number from description
                string loanNo = ExtractLoanNumberFromDescription(transaction.TransDescript);

                string memberNo = null;
                string memberName = null;

                // Try to get member info from Loan table using extracted loan number
                if (!string.IsNullOrEmpty(loanNo))
                {
                    var loanInfo = await (from loan in _context.Loans
                                          join member in _context.Members on loan.MemberNo equals member.MemberNo
                                          where loan.LoanNo == loanNo && loan.CompanyCode == companyCode
                                          select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                    if (loanInfo != null)
                    {
                        memberNo = loanInfo.MemberNo;
                        memberName = $"{loanInfo.Surname ?? ""} {loanInfo.OtherNames ?? ""}".Trim();
                    }
                }

                // If still no member found, try to get from Repay table
                if (string.IsNullOrEmpty(memberNo) && !string.IsNullOrEmpty(loanNo))
                {
                    var repayInfo = await (from repay in _context.Repay
                                           join member in _context.Members on repay.MemberNo equals member.MemberNo
                                           where repay.LoanNo == loanNo && repay.CompanyCode == companyCode
                                           select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                    if (repayInfo != null)
                    {
                        memberNo = repayInfo.MemberNo;
                        memberName = $"{repayInfo.Surname ?? ""} {repayInfo.OtherNames ?? ""}".Trim();
                    }
                }

                // If still no member found, try to get from Loanbal table
                if (string.IsNullOrEmpty(memberNo) && !string.IsNullOrEmpty(loanNo))
                {
                    var loanbalInfo = await (from lb in _context.Loanbal
                                             join member in _context.Members on lb.MemberNo equals member.MemberNo
                                             where lb.LoanNo == loanNo && lb.Companycode == companyCode
                                             select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                    if (loanbalInfo != null)
                    {
                        memberNo = loanbalInfo.MemberNo;
                        memberName = $"{loanbalInfo.Surname ?? ""} {loanbalInfo.OtherNames ?? ""}".Trim();
                    }
                }

                // Final fallback
                if (string.IsNullOrEmpty(memberNo))
                {
                    memberNo = "Unknown";
                    memberName = transaction.Source ?? "System Transaction";
                }

                string description = transaction.TransDescript ?? "Interest Charged on Loan";
                decimal interestAmount = transaction.Amount;
                totalInterest += interestAmount;

                // Track unique members
                if (!string.IsNullOrEmpty(memberNo) && memberNo != "Unknown")
                {
                    uniqueMembers.Add(memberNo);
                }

                reportData.Add(new InterestListingReportViewModel
                {
                    MemberNo = memberNo,
                    Names = memberName ?? "Unknown Member",
                    Interest = interestAmount,
                    Description = description,
                    TransactionDate = transaction.AuditTime,
                    DocumentNo = transaction.DocumentNo,
                    TransactionNo = transaction.TransactionNo,
                    LoanNo = loanNo
                });
            }

            var viewModel = new InterestListingIndexViewModel
            {
                Interests = reportData.OrderByDescending(i => i.TransactionDate).ThenBy(i => i.MemberNo).ToList(),
                TotalInterest = totalInterest,
                TotalTransactions = reportData.Count,
                UniqueMembers = uniqueMembers.Count,
                StartDate = startDate,
                EndDate = endDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            return View("~/Views/Reports/InterestListingReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> InterestListingReportOptimized(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all interest transactions with their descriptions
            var transactions = await (from gt in _context.Gltransactions
                                      join gs in _context.GlSetup on gt.DrAccNo equals gs.AccNo into drJoin
                                      from dr in drJoin.DefaultIfEmpty()
                                      join gs2 in _context.GlSetup on gt.CrAccNo equals gs2.AccNo into crJoin
                                      from cr in crJoin.DefaultIfEmpty()
                                      where gt.CompanyCode == companyCode
                                          && gt.AuditTime >= startDate
                                          && gt.AuditTime <= endDateAdjusted
                                          && (dr != null || cr != null)
                                          && ((dr != null && dr.GlAccMainGroup == "INCOME" && dr.Glaccname != null &&
                                              (dr.Glaccname.ToLower().Contains("interest") || dr.Glaccname.ToLower().Contains("intrest")))
                                              || (cr != null && cr.GlAccMainGroup == "INCOME" && cr.Glaccname != null &&
                                              (cr.Glaccname.ToLower().Contains("interest") || cr.Glaccname.ToLower().Contains("intrest"))))
                                      select new
                                      {
                                          gt.Source,
                                          gt.Amount,
                                          gt.TransDescript,
                                          gt.AuditTime,
                                          gt.DocumentNo,
                                          gt.TransactionNo
                                      }).ToListAsync();

            if (!transactions.Any())
            {
                var emptyViewModel = new InterestListingIndexViewModel
                {
                    Interests = new List<InterestListingReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalInterest = 0,
                    TotalTransactions = 0,
                    UniqueMembers = 0
                };

                ViewBag.Message = "No interest transactions found for the selected date range.";
                return View("~/Views/Reports/InterestListingReport.cshtml", emptyViewModel);
            }

            // Extract all loan numbers from descriptions
            var loanNumbers = new HashSet<string>();
            var transactionLoanMap = new Dictionary<object, string>();

            foreach (var trans in transactions)
            {
                var loanNo = ExtractLoanNumberFromDescription(trans.TransDescript);
                if (!string.IsNullOrEmpty(loanNo))
                {
                    loanNumbers.Add(loanNo);
                    transactionLoanMap[trans] = loanNo;
                }
            }

            // Get all member info for these loan numbers in one query
            var loanMemberMap = new Dictionary<string, (string MemberNo, string MemberName)>();

            if (loanNumbers.Any())
            {
                // Query Loans table
                var loanMembers = await (from loan in _context.Loans
                                         join member in _context.Members on loan.MemberNo equals member.MemberNo
                                         where loanNumbers.Contains(loan.LoanNo) && loan.CompanyCode == companyCode
                                         select new { loan.LoanNo, member.MemberNo, member.Surname, member.OtherNames })
                                        .ToDictionaryAsync(k => k.LoanNo, v => (v.MemberNo, $"{v.Surname ?? ""} {v.OtherNames ?? ""}".Trim()));

                foreach (var kvp in loanMembers)
                {
                    loanMemberMap[kvp.Key] = kvp.Value;
                }

                // For loans not found in Loans table, try Repay table
                var missingLoans = loanNumbers.Where(ln => !loanMemberMap.ContainsKey(ln)).ToList();
                if (missingLoans.Any())
                {
                    var repayMembers = await (from repay in _context.Repay
                                              join member in _context.Members on repay.MemberNo equals member.MemberNo
                                              where missingLoans.Contains(repay.LoanNo) && repay.CompanyCode == companyCode
                                              select new { repay.LoanNo, member.MemberNo, member.Surname, member.OtherNames })
                                             .ToDictionaryAsync(k => k.LoanNo, v => (v.MemberNo, $"{v.Surname ?? ""} {v.OtherNames ?? ""}".Trim()));

                    foreach (var kvp in repayMembers)
                    {
                        loanMemberMap[kvp.Key] = kvp.Value;
                    }
                }

                // For remaining loans, try Loanbal table
                var stillMissing = loanNumbers.Where(ln => !loanMemberMap.ContainsKey(ln)).ToList();
                if (stillMissing.Any())
                {
                    var loanbalMembers = await (from lb in _context.Loanbal
                                                join member in _context.Members on lb.MemberNo equals member.MemberNo
                                                where stillMissing.Contains(lb.LoanNo) && lb.Companycode == companyCode
                                                select new { lb.LoanNo, member.MemberNo, member.Surname, member.OtherNames })
                                               .ToDictionaryAsync(k => k.LoanNo, v => (v.MemberNo, $"{v.Surname ?? ""} {v.OtherNames ?? ""}".Trim()));

                    foreach (var kvp in loanbalMembers)
                    {
                        loanMemberMap[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Build report data
            var reportData = new List<InterestListingReportViewModel>();
            decimal totalInterest = 0;
            var uniqueMembers = new HashSet<string>();

            foreach (var transaction in transactions)
            {
                string loanNo = transactionLoanMap.ContainsKey(transaction) ? transactionLoanMap[transaction] : null;

                string memberNo = "Unknown";
                string memberName = transaction.Source ?? "System Transaction";

                if (!string.IsNullOrEmpty(loanNo) && loanMemberMap.ContainsKey(loanNo))
                {
                    var memberInfo = loanMemberMap[loanNo];
                    memberNo = memberInfo.MemberNo;
                    memberName = memberInfo.MemberName;
                }

                decimal interestAmount = transaction.Amount;
                totalInterest += interestAmount;

                if (!string.IsNullOrEmpty(memberNo) && memberNo != "Unknown")
                {
                    uniqueMembers.Add(memberNo);
                }

                reportData.Add(new InterestListingReportViewModel
                {
                    MemberNo = memberNo,
                    Names = memberName,
                    Interest = interestAmount,
                    Description = transaction.TransDescript ?? "Interest Charged on Loan",
                    TransactionDate = transaction.AuditTime,
                    DocumentNo = transaction.DocumentNo,
                    TransactionNo = transaction.TransactionNo,
                    LoanNo = loanNo
                });
            }

            var viewModel = new InterestListingIndexViewModel
            {
                Interests = reportData.OrderByDescending(i => i.TransactionDate).ThenBy(i => i.MemberNo).ToList(),
                TotalInterest = totalInterest,
                TotalTransactions = reportData.Count,
                UniqueMembers = uniqueMembers.Count,
                StartDate = startDate,
                EndDate = endDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            return View("~/Views/Reports/InterestListingReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportInterestListingToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get transactions with member info
            var transactions = await (from gt in _context.Gltransactions
                                      join gs in _context.GlSetup on gt.DrAccNo equals gs.AccNo into drJoin
                                      from dr in drJoin.DefaultIfEmpty()
                                      join gs2 in _context.GlSetup on gt.CrAccNo equals gs2.AccNo into crJoin
                                      from cr in crJoin.DefaultIfEmpty()
                                      where gt.CompanyCode == companyCode
                                          && gt.AuditTime >= startDate
                                          && gt.AuditTime <= endDateAdjusted
                                          && (dr != null || cr != null)
                                          && ((dr != null && dr.GlAccMainGroup == "INCOME" && dr.Glaccname != null &&
                                              (dr.Glaccname.ToLower().Contains("interest") || dr.Glaccname.ToLower().Contains("intrest")))
                                              || (cr != null && cr.GlAccMainGroup == "INCOME" && cr.Glaccname != null &&
                                              (cr.Glaccname.ToLower().Contains("interest") || cr.Glaccname.ToLower().Contains("intrest"))))
                                      select new
                                      {
                                          gt.Source,
                                          gt.Amount,
                                          gt.TransDescript,
                                          gt.AuditTime,
                                          gt.DocumentNo,
                                          gt.TransactionNo
                                      }).ToListAsync();

            if (!transactions.Any())
            {
                TempData["Error"] = "No interest transactions found for the selected date range";
                return RedirectToAction("InterestListingReport");
            }

            // Process data similar to the main report
            var reportData = new List<InterestListingReportViewModel>();
            decimal totalInterest = 0;

            foreach (var transaction in transactions)
            {
                string loanNo = ExtractLoanNumberFromDescription(transaction.TransDescript);
                string memberNo = "Unknown";
                string memberName = transaction.Source ?? "System Transaction";

                if (!string.IsNullOrEmpty(loanNo))
                {
                    var loanInfo = await (from loan in _context.Loans
                                          join member in _context.Members on loan.MemberNo equals member.MemberNo
                                          where loan.LoanNo == loanNo && loan.CompanyCode == companyCode
                                          select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                    if (loanInfo != null)
                    {
                        memberNo = loanInfo.MemberNo;
                        memberName = $"{loanInfo.Surname ?? ""} {loanInfo.OtherNames ?? ""}".Trim();
                    }
                }

                decimal interestAmount = transaction.Amount;
                totalInterest += interestAmount;

                reportData.Add(new InterestListingReportViewModel
                {
                    MemberNo = memberNo,
                    Names = memberName,
                    Interest = interestAmount,
                    Description = transaction.TransDescript ?? "Interest Charged on Loan",
                    TransactionDate = transaction.AuditTime,
                    DocumentNo = transaction.DocumentNo,
                    TransactionNo = transaction.TransactionNo
                });
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Interest Listings");
            int currentRow = 1;

            // Header section
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 5).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = "INTEREST CONTROL LISTINGS";
            worksheet.Range(currentRow, 1, currentRow, 5).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 5).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 5).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "Member No", "Member Names", "Interest (KES)", "Description", "Transaction Date" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var item in reportData)
            {
                worksheet.Cell(currentRow, 1).Value = item.MemberNo;
                worksheet.Cell(currentRow, 2).Value = item.Names;
                worksheet.Cell(currentRow, 3).Value = item.Interest;
                worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 4).Value = item.Description;
                worksheet.Cell(currentRow, 5).Value = item.TransactionDate?.ToString("dd/MM/yyyy") ?? "-";
                currentRow++;
            }

            currentRow++;
            worksheet.Cell(currentRow, 2).Value = "TOTAL:";
            worksheet.Cell(currentRow, 2).Style.Font.SetBold();
            worksheet.Cell(currentRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 3).Value = totalInterest;
            worksheet.Cell(currentRow, 3).Style.Font.SetBold();
            worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"InterestListingReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportInterestListingToPdf(DateTime startDate, DateTime endDate)
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";
                var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

                // Get interest transactions
                var transactions = await (from gt in _context.Gltransactions
                                          join gs in _context.GlSetup on gt.DrAccNo equals gs.AccNo into drJoin
                                          from dr in drJoin.DefaultIfEmpty()
                                          join gs2 in _context.GlSetup on gt.CrAccNo equals gs2.AccNo into crJoin
                                          from cr in crJoin.DefaultIfEmpty()
                                          where gt.CompanyCode == companyCode
                                              && gt.AuditTime >= startDate
                                              && gt.AuditTime <= endDateAdjusted
                                              && (dr != null || cr != null)
                                              && ((dr != null && dr.GlAccMainGroup == "INCOME" && dr.Glaccname != null &&
                                                  (dr.Glaccname.ToLower().Contains("interest") || dr.Glaccname.ToLower().Contains("intrest")))
                                                  || (cr != null && cr.GlAccMainGroup == "INCOME" && cr.Glaccname != null &&
                                                  (cr.Glaccname.ToLower().Contains("interest") || cr.Glaccname.ToLower().Contains("intrest"))))
                                          select new
                                          {
                                              gt.Source,
                                              gt.Amount,
                                              gt.TransDescript,
                                              gt.AuditTime,
                                              gt.DocumentNo,
                                              gt.TransactionNo
                                          }).ToListAsync();

                if (!transactions.Any())
                {
                    TempData["Error"] = "No interest transactions found for the selected date range";
                    return RedirectToAction("InterestListingReport");
                }

                // Process data to get member info
                var reportData = new List<InterestListingReportViewModel>();
                decimal totalInterest = 0;

                foreach (var transaction in transactions)
                {
                    string loanNo = ExtractLoanNumberFromDescription(transaction.TransDescript);
                    string memberNo = "Unknown";
                    string memberName = transaction.Source ?? "System Transaction";

                    if (!string.IsNullOrEmpty(loanNo))
                    {
                        var loanInfo = await (from loan in _context.Loans
                                              join member in _context.Members on loan.MemberNo equals member.MemberNo
                                              where loan.LoanNo == loanNo && loan.CompanyCode == companyCode
                                              select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                        if (loanInfo != null)
                        {
                            memberNo = loanInfo.MemberNo;
                            memberName = $"{loanInfo.Surname ?? ""} {loanInfo.OtherNames ?? ""}".Trim();
                        }
                        else
                        {
                            var repayInfo = await (from repay in _context.Repay
                                                   join member in _context.Members on repay.MemberNo equals member.MemberNo
                                                   where repay.LoanNo == loanNo && repay.CompanyCode == companyCode
                                                   select new { member.MemberNo, member.Surname, member.OtherNames }).FirstOrDefaultAsync();

                            if (repayInfo != null)
                            {
                                memberNo = repayInfo.MemberNo;
                                memberName = $"{repayInfo.Surname ?? ""} {repayInfo.OtherNames ?? ""}".Trim();
                            }
                        }
                    }

                    totalInterest += transaction.Amount;

                    reportData.Add(new InterestListingReportViewModel
                    {
                        MemberNo = memberNo,
                        Names = string.IsNullOrWhiteSpace(memberName) ? "Unknown Member" : memberName,
                        Interest = transaction.Amount,
                        Description = transaction.TransDescript ?? "Interest Charged on Loan",
                        TransactionDate = transaction.AuditTime,
                        DocumentNo = transaction.DocumentNo,
                        TransactionNo = transaction.TransactionNo
                    });
                }

                // Generate PDF
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
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                        page.Header().Column(header =>
                        {
                            header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                            header.Item().AlignCenter().Text("INTEREST CONTROL LISTINGS").FontSize(12).Bold();
                            header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                            header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                            header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                            header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                        });

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.0f);  // MemberNo
                                cols.RelativeColumn(2.0f);  // Names
                                cols.RelativeColumn(1.0f);  // Interest
                                cols.RelativeColumn(2.2f);  // Description
                                cols.RelativeColumn(1.2f);  // Transaction Date
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member No").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Member Names").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Interest (KES)").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Description").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Transaction Date").Bold().FontSize(9);
                            });

                            foreach (var item in reportData)
                            {
                                table.Cell().Border(0.2f).Padding(4).Text(item.MemberNo ?? "").FontSize(9);
                                table.Cell().Border(0.2f).Padding(4).Text(item.Names ?? "").FontSize(9);
                                table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Interest:N2}").FontSize(9);
                                table.Cell().Border(0.2f).Padding(4).Text(item.Description ?? "").FontSize(9);
                                table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.TransactionDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(9);
                            }

                            // Total row
                            table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(10);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalInterest:N2}").Bold().FontSize(10);
                            table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4);
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

                var fileName = $"InterestListingReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
                var content = stream.ToArray();

                return File(content, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error generating PDF report: {ex.Message}";
                return RedirectToAction("InterestListingReport");
            }
        }

        private string ExtractLoanNumberFromDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return null;

            // Pattern: "Loan repayment #1 - SKW 2K422314109 - Installment 1"
            var match = System.Text.RegularExpressions.Regex.Match(description, @"(SKW\s*\w+)");
            if (match.Success)
            {
                return match.Value.Trim();
            }

            // Alternative pattern for other loan number formats
            match = System.Text.RegularExpressions.Regex.Match(description, @"(LN\s*\w+)");
            if (match.Success)
            {
                return match.Value.Trim();
            }

            return null;
        }

        #endregion

        #region Rejected Loans Report

        [HttpGet]
        public IActionResult RejectedLoansReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            var viewModel = new RejectedLoansIndexViewModel
            {
                RejectedLoans = new List<RejectedLoansReportViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalAmountRejected = 0,
                TotalRejectedLoans = 0,
                UniqueMembers = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/RejectedLoansReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RejectedLoansReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get rejected loans from Loans table (Status = Rejected = 10)
            var rejectedLoans = await (from loan in _context.Loans
                                       join member in _context.Members
                                           on loan.MemberNo equals member.MemberNo
                                       join appraisal in _context.Appraisal
                                           on loan.LoanNo equals appraisal.LoanNo into appraisalJoin
                                       from a in appraisalJoin.DefaultIfEmpty()
                                       join loantype in _context.Loantypes
                                           on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                       from lt in loanTypeJoin.DefaultIfEmpty()
                                       where loan.CompanyCode == companyCode
                                           && loan.Status == (int)Status.Rejected
                                           && loan.AuditTime >= startDate
                                           && loan.AuditTime <= endDateAdjusted
                                       select new
                                       {
                                           loan.MemberNo,
                                           loan.LoanNo,
                                           loan.LoanCode,
                                           loan.AuditTime,
                                           MemberSurname = member.Surname,
                                           MemberOtherNames = member.OtherNames,
                                           MemberFullName = member.FullName,
                                           AmtRecommended = a != null ? a.AmtRecommended : loan.LoanAmt,
                                           Reason = a != null ? a.Reason : "Loan application rejected",
                                           AppraisDate = a != null ? a.AppraisDate : loan.AuditTime,
                                           LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                           AuditID = a != null ? a.AuditID : null
                                       }).ToListAsync();

            if (!rejectedLoans.Any())
            {
                var emptyViewModel = new RejectedLoansIndexViewModel
                {
                    RejectedLoans = new List<RejectedLoansReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalAmountRejected = 0,
                    TotalRejectedLoans = 0,
                    UniqueMembers = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No rejected loans found for the selected date range.";

                return View("~/Views/Reports/RejectedLoansReport.cshtml", emptyViewModel);
            }

            var reportData = new List<RejectedLoansReportViewModel>();
            decimal totalAmountRejected = 0;
            var uniqueMembers = new HashSet<string>();

            foreach (var item in rejectedLoans)
            {
                string fullName = item.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = item.MemberNo;
                }

                decimal rejectedAmount = item.AmtRecommended ?? 0;
                totalAmountRejected += rejectedAmount;

                if (!string.IsNullOrEmpty(item.MemberNo))
                {
                    uniqueMembers.Add(item.MemberNo);
                }

                string reason = item.Reason;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "Loan application did not meet approval criteria";
                }

                reportData.Add(new RejectedLoansReportViewModel
                {
                    MemberNo = item.MemberNo,
                    Names = fullName,
                    LoanNo = item.LoanNo,
                    AmtRejected = rejectedAmount,
                    RejectedDate = item.AppraisDate ?? item.AuditTime,
                    Reasons = reason,
                    LoanCode = item.LoanCode,
                    LoanName = item.LoanTypeName,
                    AppraisedBy = item.AuditID
                });
            }

            var viewModel = new RejectedLoansIndexViewModel
            {
                RejectedLoans = reportData.OrderByDescending(r => r.RejectedDate).ThenBy(r => r.MemberNo).ToList(),
                TotalAmountRejected = totalAmountRejected,
                TotalRejectedLoans = reportData.Count,
                UniqueMembers = uniqueMembers.Count,
                StartDate = startDate,
                EndDate = endDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/RejectedLoansReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportRejectedLoansToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            var rejectedLoans = await (from loan in _context.Loans
                                       join member in _context.Members
                                           on loan.MemberNo equals member.MemberNo
                                       join appraisal in _context.Appraisal
                                           on loan.LoanNo equals appraisal.LoanNo into appraisalJoin
                                       from a in appraisalJoin.DefaultIfEmpty()
                                       join loantype in _context.Loantypes
                                           on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                       from lt in loanTypeJoin.DefaultIfEmpty()
                                       where loan.CompanyCode == companyCode
                                           && loan.Status == (int)Status.Rejected
                                           && loan.AuditTime >= startDate
                                           && loan.AuditTime <= endDateAdjusted
                                       select new
                                       {
                                           loan.MemberNo,
                                           loan.LoanNo,
                                           loan.AuditTime,
                                           MemberSurname = member.Surname,
                                           MemberOtherNames = member.OtherNames,
                                           MemberFullName = member.FullName,
                                           AmtRecommended = a != null ? a.AmtRecommended : loan.LoanAmt,
                                           Reason = a != null ? a.Reason : "Loan application rejected",
                                           AppraisDate = a != null ? a.AppraisDate : loan.AuditTime,
                                           LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                       }).ToListAsync();

            if (!rejectedLoans.Any())
            {
                TempData["Error"] = "No rejected loans found for the selected date range";
                return RedirectToAction("RejectedLoansReport");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Rejected Loans");
            int currentRow = 1;

            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"REJECTED LOANS REPORT";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 7).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            string[] headers = { "MemberNo", "Names", "LoanNo", "Amt Rejected", "Rejected Date", "Loan Name", "Reasons" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalAmountRejected = 0;

            foreach (var item in rejectedLoans)
            {
                string fullName = item.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = item.MemberNo;
                }

                decimal rejectedAmount = item.AmtRecommended ?? 0;
                totalAmountRejected += rejectedAmount;

                string reason = item.Reason;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "Loan application did not meet approval criteria";
                }

                worksheet.Cell(currentRow, 1).Value = item.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = item.LoanNo;
                worksheet.Cell(currentRow, 4).Value = rejectedAmount;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Value = (item.AppraisDate ?? item.AuditTime).ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 6).Value = item.LoanTypeName;
                worksheet.Cell(currentRow, 7).Value = reason;

                currentRow++;
            }

            currentRow++;
            worksheet.Cell(currentRow, 3).Value = "TOTAL:";
            worksheet.Cell(currentRow, 3).Style.Font.SetBold();
            worksheet.Cell(currentRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 4).Value = totalAmountRejected;
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RejectedLoansReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportRejectedLoansToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            var rejectedLoans = await (from loan in _context.Loans
                                       join member in _context.Members
                                           on loan.MemberNo equals member.MemberNo
                                       join appraisal in _context.Appraisal
                                           on loan.LoanNo equals appraisal.LoanNo into appraisalJoin
                                       from a in appraisalJoin.DefaultIfEmpty()
                                       join loantype in _context.Loantypes
                                           on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                       from lt in loanTypeJoin.DefaultIfEmpty()
                                       where loan.CompanyCode == companyCode
                                           && loan.Status == (int)Status.Rejected
                                           && loan.AuditTime >= startDate
                                           && loan.AuditTime <= endDateAdjusted
                                       select new
                                       {
                                           loan.MemberNo,
                                           loan.LoanNo,
                                           loan.AuditTime,
                                           MemberSurname = member.Surname,
                                           MemberOtherNames = member.OtherNames,
                                           MemberFullName = member.FullName,
                                           AmtRecommended = a != null ? a.AmtRecommended : loan.LoanAmt,
                                           Reason = a != null ? a.Reason : "Loan application rejected",
                                           AppraisDate = a != null ? a.AppraisDate : loan.AuditTime,
                                           LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                       }).ToListAsync();

            if (!rejectedLoans.Any())
            {
                TempData["Error"] = "No rejected loans found for the selected date range";
                return RedirectToAction("RejectedLoansReport");
            }

            var reportData = new List<RejectedLoansReportViewModel>();
            decimal totalAmountRejected = 0;

            foreach (var item in rejectedLoans)
            {
                string fullName = item.MemberFullName;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"{item.MemberSurname ?? ""} {item.MemberOtherNames ?? ""}".Trim();
                }
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = item.MemberNo;
                }

                decimal rejectedAmount = item.AmtRecommended ?? 0;
                totalAmountRejected += rejectedAmount;

                string reason = item.Reason;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "Loan application did not meet approval criteria";
                }

                reportData.Add(new RejectedLoansReportViewModel
                {
                    MemberNo = item.MemberNo,
                    Names = fullName,
                    LoanNo = item.LoanNo,
                    AmtRejected = rejectedAmount,
                    RejectedDate = item.AppraisDate ?? item.AuditTime,
                    Reasons = reason,
                    LoanName = item.LoanTypeName
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
                        header.Item().AlignCenter().Text($"REJECTED LOANS REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
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
                            });

                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Rejected Loans:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).Text(reportData.Count.ToString());
                            summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount Rejected:").Bold();
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalAmountRejected:N0}");
                        });

                        contentCol.Item().PaddingTop(1, Unit.Centimetre);
                        contentCol.Item().Text("REJECTED LOANS DETAILS").FontSize(11).Bold();

                        contentCol.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(2.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amt Rejected").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Rejected Date").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Name").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Reasons").Bold().FontSize(8);
                            });

                            foreach (var loan in reportData.OrderBy(l => l.RejectedDate))
                            {
                                table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text(loan.Names ?? "").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.AmtRejected:N0}").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.RejectedDate?.ToString("dd/MM/yyyy") ?? "-").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text(loan.LoanName ?? "").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text(loan.Reasons ?? "").FontSize(8);
                            }

                            table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTAL:").Bold().FontSize(9);
                            table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalAmountRejected:N0}").Bold().FontSize(9);
                            table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4);
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
            return File(content, "application/pdf", $"RejectedLoansReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Portfolio At Risk Report

        [HttpGet]
        public IActionResult PortfolioAtRiskReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new PortfolioAtRiskIndexViewModel
            {
                Loans = new List<PortfolioAtRiskReportViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalLoanIssued = 0,
                TotalUnpaidInterest = 0,
                TotalLoanBalance = 0,
                TotalArrears = 0,
                ParPercentage = 0,
                TotalLoans = 0,
                ActiveLoans = 0,
                OverdueLoans = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/PortfolioAtRiskReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> PortfolioAtRiskReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                var emptyViewModel = new PortfolioAtRiskIndexViewModel
                {
                    Loans = new List<PortfolioAtRiskReportViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalLoanIssued = 0,
                    TotalUnpaidInterest = 0,
                    TotalLoanBalance = 0,
                    TotalArrears = 0,
                    ParPercentage = 0,
                    TotalLoans = 0,
                    ActiveLoans = 0,
                    OverdueLoans = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/PortfolioAtRiskReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbal table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.Penalty
                });

            // Get latest repayments for arrears calculation
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var reportData = new List<PortfolioAtRiskReportViewModel>();
            decimal totalLoanIssued = 0;
            decimal totalUnpaidInterest = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int activeLoans = 0;
            int overdueLoans = 0;

            foreach (var loan in loansWithDetails)
            {
                // Get balance details
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Skip if loan is fully paid
                if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                // Calculate loan issued amount
                decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                // Calculate arrears (overdue amount - using penalty as indicator)
                decimal arrears = penalty;

                // Check if loan is overdue
                DateTime? lastPaymentDate = null;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                }

                int daysOverdue = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        daysOverdue = (asAtDate - nextDueDate).Days;
                        if (daysOverdue > 0)
                        {
                            overdueLoans++;
                            // For PAR calculation, we consider any loan with days overdue > 0
                            arrears = currentBalance; // PAR uses the full outstanding balance for overdue loans
                        }
                    }
                }
                else
                {
                    // No payments made - check if first payment is overdue
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        daysOverdue = (asAtDate - firstDueDate).Days;
                        if (daysOverdue > 0)
                        {
                            overdueLoans++;
                            arrears = currentBalance;
                        }
                    }
                }

                if (currentBalance > 0)
                {
                    activeLoans++;
                }

                totalLoanIssued += loanIssued;
                totalUnpaidInterest += unpaidInterest;
                totalLoanBalance += currentBalance;
                totalArrears += arrears;

                // Build member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                reportData.Add(new PortfolioAtRiskReportViewModel
                {
                    MemberNo = loan.MemberNo,
                    Names = fullName,
                    LoanNo = loan.LoanNo,
                    LoanName = loan.LoanName ?? "Unknown",
                    LoanIssued = loanIssued,
                    UnpaidInterest = unpaidInterest,
                    LoanBalance = currentBalance,
                    Arrears = arrears,
                    ParPercentage = 0, // Will calculate overall after
                    PrincipalPaid = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo].TotalPrincipalPaid : 0,
                    InterestPaid = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo].TotalInterestPaid : 0,
                    DaysOverdue = daysOverdue
                });
            }

            // Calculate Portfolio at Risk Percentage
            // PAR % = (Total Outstanding Balance of Overdue Loans) / (Total Outstanding Balance of All Loans) * 100
            decimal parPercentage = 0;
            if (totalLoanBalance > 0)
            {
                parPercentage = (totalArrears / totalLoanBalance) * 100;
            }

            var viewModel = new PortfolioAtRiskIndexViewModel
            {
                Loans = reportData.OrderBy(l => l.DaysOverdue).ThenBy(l => l.MemberNo).ToList(),
                TotalLoanIssued = totalLoanIssued,
                TotalUnpaidInterest = totalUnpaidInterest,
                TotalLoanBalance = totalLoanBalance,
                TotalArrears = totalArrears,
                ParPercentage = parPercentage,
                TotalLoans = reportData.Count,
                ActiveLoans = activeLoans,
                OverdueLoans = overdueLoans,
                AsAtDate = asAtDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/PortfolioAtRiskReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportPortfolioAtRiskToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("PortfolioAtRiskReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            using var workbook = new XLWorkbook();

            // Summary Worksheet
            var summarySheet = workbook.Worksheets.Add("PAR Summary");
            int currentRow = 1;

            summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"PORTFOLIO AT RISK REPORT - AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Summary Statistics
            decimal totalLoanIssued = 0;
            decimal totalUnpaidInterest = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int totalLoans = 0;
            int overdueCount = 0;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;
                decimal arrears = penalty;

                // Check if overdue
                DateTime? lastPaymentDate = null;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                }

                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        overdueCount++;
                        arrears = currentBalance;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        overdueCount++;
                        arrears = currentBalance;
                    }
                }

                totalLoanIssued += loanIssued;
                totalUnpaidInterest += unpaidInterest;
                totalLoanBalance += currentBalance;
                totalArrears += arrears;
                totalLoans++;
            }

            decimal parPercentage = totalLoanBalance > 0 ? (totalArrears / totalLoanBalance) * 100 : 0;

            // Summary Cards
            string[] summaryLabels = { "Total Loans", "Total Loan Issued", "Total Unpaid Interest", "Total Loan Balance", "Total Arrears", "PAR %" };
            decimal[] summaryValues = { totalLoans, totalLoanIssued, totalUnpaidInterest, totalLoanBalance, totalArrears, parPercentage };
            string[] summaryFormats = { "N0", "N2", "N2", "N2", "N2", "N2" };

            for (int i = 0; i < summaryLabels.Length; i++)
            {
                summarySheet.Cell(currentRow, 1).Value = summaryLabels[i];
                summarySheet.Cell(currentRow, 1).Style.Font.SetBold();
                summarySheet.Cell(currentRow, 2).Value = summaryValues[i];
                if (i > 0)
                {
                    summarySheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                }
                else
                {
                    summarySheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";
                }
                currentRow++;
            }

            summarySheet.Columns().AdjustToContents();

            // Detailed Loans Worksheet
            var detailSheet = workbook.Worksheets.Add("Loan Details");
            currentRow = 1;

            detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            detailSheet.Cell(currentRow, 1).Value = $"PORTFOLIO AT RISK - LOAN DETAILS AS AT {asAtDate:dd/MM/yyyy}";
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] headers = { "MemberNo", "Names", "LoanNo", "Loan Name", "Loan Issued", "Unpaid Interest", "Loan Balance", "Arrears" };

            for (int i = 0; i < headers.Length; i++)
            {
                detailSheet.Cell(currentRow, i + 1).Value = headers[i];
                detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                detailSheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;
                decimal arrears = penalty;

                // Check if overdue for arrears calculation
                DateTime? lastPaymentDate = null;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                }

                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        arrears = currentBalance;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        arrears = currentBalance;
                    }
                }

                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                detailSheet.Cell(currentRow, 1).Value = loan.MemberNo;
                detailSheet.Cell(currentRow, 2).Value = fullName;
                detailSheet.Cell(currentRow, 3).Value = loan.LoanNo;
                detailSheet.Cell(currentRow, 4).Value = loan.LoanName ?? "Unknown";
                detailSheet.Cell(currentRow, 5).Value = loanIssued;
                detailSheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 6).Value = unpaidInterest;
                detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 7).Value = currentBalance;
                detailSheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 8).Value = arrears;
                detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

                currentRow++;
            }

            // Totals row
            currentRow++;
            detailSheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
            detailSheet.Cell(currentRow, 4).Style.Font.SetBold();
            detailSheet.Cell(currentRow, 5).Value = totalLoanIssued;
            detailSheet.Cell(currentRow, 5).Style.Font.SetBold();
            detailSheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            detailSheet.Cell(currentRow, 6).Value = totalUnpaidInterest;
            detailSheet.Cell(currentRow, 6).Style.Font.SetBold();
            detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            detailSheet.Cell(currentRow, 7).Value = totalLoanBalance;
            detailSheet.Cell(currentRow, 7).Style.Font.SetBold();
            detailSheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
            detailSheet.Cell(currentRow, 8).Value = totalArrears;
            detailSheet.Cell(currentRow, 8).Style.Font.SetBold();
            detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PortfolioAtRiskReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportPortfolioAtRiskToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("PortfolioAtRiskReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            decimal totalLoanIssued = 0;
            decimal totalUnpaidInterest = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int totalLoans = 0;
            int overdueCount = 0;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = 0;
                decimal unpaidInterest = 0;
                decimal penalty = 0;

                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    var lb = loanBalances[loan.LoanNo];
                    currentBalance = lb.Balance;
                    unpaidInterest = lb.IntrOwed;
                    penalty = lb.Penalty;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;
                decimal arrears = penalty;

                DateTime? lastPaymentDate = null;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                }

                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        overdueCount++;
                        arrears = currentBalance;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        overdueCount++;
                        arrears = currentBalance;
                    }
                }

                totalLoanIssued += loanIssued;
                totalUnpaidInterest += unpaidInterest;
                totalLoanBalance += currentBalance;
                totalArrears += arrears;
                totalLoans++;
            }

            decimal parPercentage = totalLoanBalance > 0 ? (totalArrears / totalLoanBalance) * 100 : 0;

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
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                        header.Item().AlignCenter().Text($"PORTFOLIO AT RISK REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"As At: {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(10);
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        // Summary Table (Like the image)
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                            });

                            summaryTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("LOAN ISSUED").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("UNPAID INTEREST").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("LOAN BALANCE").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("ARREARS").Bold().FontSize(9);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("PAR %").Bold().FontSize(9);
                            });

                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text("TOTALS:").Bold().FontSize(10);
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalLoanIssued:N2}").FontSize(10);
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalUnpaidInterest:N2}").FontSize(10);
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalLoanBalance:N2}").FontSize(10);
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalArrears:N2}").FontSize(10);
                            summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{parPercentage:N2}%").FontSize(10);
                        });

                        // Statistics Cards
                        contentCol.Item().PaddingTop(1, Unit.Centimetre);
                        contentCol.Item().Table(statsTable =>
                        {
                            statsTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            statsTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Loans:").Bold();
                            statsTable.Cell().Border(0.2f).Padding(4).Text(totalLoans.ToString());
                            statsTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Overdue Loans:").Bold();
                            statsTable.Cell().Border(0.2f).Padding(4).Text(overdueCount.ToString());
                            statsTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Active Loans:").Bold();
                            statsTable.Cell().Border(0.2f).Padding(4).Text((totalLoans - overdueCount).ToString());
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
            return File(content, "application/pdf", $"PortfolioAtRiskReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion


        #region Loan Type Performance Report

        [HttpGet]
        public IActionResult LoanTypePerformanceReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new LoanTypePerformanceIndexViewModel
            {
                LoanTypes = new List<LoanTypePerformanceReportViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalAmount = 0,
                TotalPrincipalBalance = 0,
                TotalArrears = 0,
                OverallParPercentage = 0,
                TotalLoanTypes = 0,
                TotalLoans = 0,
                TotalActiveLoans = 0,
                TotalOverdueLoans = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanTypePerformanceReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanTypePerformanceReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans grouped by loan type
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              LoanCodeValue = loan.LoanCode ?? ""
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                var emptyViewModel = new LoanTypePerformanceIndexViewModel
                {
                    LoanTypes = new List<LoanTypePerformanceReportViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalAmount = 0,
                    TotalPrincipalBalance = 0,
                    TotalArrears = 0,
                    OverallParPercentage = 0,
                    TotalLoanTypes = 0,
                    TotalLoans = 0,
                    TotalActiveLoans = 0,
                    TotalOverdueLoans = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanTypePerformanceReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbal table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.Penalty
                });

            // Get latest repayments for arrears calculation (same logic as Portfolio At Risk)
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Group by Loan Type
            var groupedByLoanType = loansWithDetails
                .GroupBy(l => new { l.LoanTypeName, l.LoanCodeValue })
                .Select(g => new
                {
                    LoanTypeName = g.Key.LoanTypeName,
                    LoanCode = g.Key.LoanCodeValue,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            var reportData = new List<LoanTypePerformanceReportViewModel>();
            decimal totalAmount = 0;
            decimal totalPrincipalBalance = 0;
            decimal totalArrears = 0;
            int totalLoansCount = 0;
            int totalActiveLoansCount = 0;
            int totalOverdueLoansCount = 0;

            foreach (var group in groupedByLoanType)
            {
                decimal loanTypeAmount = 0;
                decimal loanTypePrincipalBalance = 0;
                decimal loanTypeArrears = 0;
                int loanTypeLoansCount = 0;
                int loanTypeActiveLoans = 0;
                int loanTypeOverdueLoans = 0;
                decimal loanTypeUnpaidInterest = 0;

                foreach (var loan in group.Loans)
                {
                    // Get balance details
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;
                    decimal penalty = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                        penalty = lb.Penalty;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Skip if loan is fully paid
                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    // Calculate loan issued amount
                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // ========== CORRECT ARREARS CALCULATION (Same as Portfolio At Risk) ==========
                    decimal arrears = 0;
                    DateTime? lastPaymentDate = null;
                    int daysOverdue = 0;

                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                    }

                    if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysOverdue = (asAtDate - nextDueDate).Days;
                            if (daysOverdue > 0)
                            {
                                loanTypeOverdueLoans++;
                                // For PAR calculation, use the full outstanding balance for overdue loans
                                arrears = currentBalance;
                            }
                        }
                    }
                    else
                    {
                        // No payments made - check if first payment is overdue
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysOverdue = (asAtDate - firstDueDate).Days;
                            if (daysOverdue > 0)
                            {
                                loanTypeOverdueLoans++;
                                arrears = currentBalance;
                            }
                        }
                    }

                    if (currentBalance > 0 || unpaidInterest > 0)
                    {
                        loanTypeActiveLoans++;
                    }

                    loanTypeAmount += loanIssued;
                    loanTypePrincipalBalance += currentBalance;
                    loanTypeArrears += arrears;
                    loanTypeUnpaidInterest += unpaidInterest;
                    loanTypeLoansCount++;
                }

                // Calculate PAR % for this loan type (using same logic as Portfolio At Risk)
                decimal parPercentage = 0;
                if (loanTypePrincipalBalance > 0)
                {
                    parPercentage = (loanTypeArrears / loanTypePrincipalBalance) * 100;
                }

                totalAmount += loanTypeAmount;
                totalPrincipalBalance += loanTypePrincipalBalance;
                totalArrears += loanTypeArrears;
                totalLoansCount += loanTypeLoansCount;
                totalActiveLoansCount += loanTypeActiveLoans;
                totalOverdueLoansCount += loanTypeOverdueLoans;

                reportData.Add(new LoanTypePerformanceReportViewModel
                {
                    LoanType = group.LoanTypeName,
                    LoanCode = group.LoanCode,
                    Amount = loanTypeAmount,
                    TotalPrincipalBalance = loanTypePrincipalBalance,
                    TotalArrears = loanTypeArrears,
                    ParPercentage = parPercentage,
                    TotalLoans = loanTypeLoansCount,
                    ActiveLoans = loanTypeActiveLoans,
                    OverdueLoans = loanTypeOverdueLoans,
                    UnpaidInterest = loanTypeUnpaidInterest
                });
            }

            // Calculate Overall PAR % (same logic as Portfolio At Risk)
            decimal overallParPercentage = 0;
            if (totalPrincipalBalance > 0)
            {
                overallParPercentage = (totalArrears / totalPrincipalBalance) * 100;
            }

            var viewModel = new LoanTypePerformanceIndexViewModel
            {
                LoanTypes = reportData.OrderBy(l => l.LoanType).ToList(),
                TotalAmount = totalAmount,
                TotalPrincipalBalance = totalPrincipalBalance,
                TotalArrears = totalArrears,
                OverallParPercentage = overallParPercentage,
                TotalLoanTypes = reportData.Count,
                TotalLoans = totalLoansCount,
                TotalActiveLoans = totalActiveLoansCount,
                TotalOverdueLoans = totalOverdueLoansCount,
                AsAtDate = asAtDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanTypePerformanceReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanTypePerformanceToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans grouped by loan type
            var loansByLoanType = await (from loan in _context.Loans
                                         join member in _context.Members
                                             on loan.MemberNo equals member.MemberNo
                                         join loantype in _context.Loantypes
                                             on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                         from lt in loanTypeJoin.DefaultIfEmpty()
                                         where loan.CompanyCode == companyCode
                                             && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                             && loan.AuditTime <= asAtDateEnd
                                         select new
                                         {
                                             loan.MemberNo,
                                             loan.LoanNo,
                                             loan.AuditTime,
                                             loan.LoanAmt,
                                             loan.Aamount,
                                             MemberSurname = member.Surname,
                                             MemberOtherNames = member.OtherNames,
                                             LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                             LoanCodeValue = loan.LoanCode ?? ""
                                         }).ToListAsync();

            if (!loansByLoanType.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanTypePerformanceReport");
            }

            var loanNos = loansByLoanType.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            var currentSchedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo) && s.Status != "Paid")
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    DueDate = g.OrderBy(s => s.InstallmentNo).Select(s => s.DueDate).FirstOrDefault()
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.DueDate);

            // Group by Loan Type
            var groupedByLoanType = loansByLoanType
                .GroupBy(l => new { l.LoanTypeName, l.LoanCodeValue })
                .Select(g => new
                {
                    LoanTypeName = g.Key.LoanTypeName,
                    LoanCode = g.Key.LoanCodeValue,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            using var workbook = new XLWorkbook();

            // Main Summary Worksheet
            var summarySheet = workbook.Worksheets.Add("Loan Type Performance");
            int currentRow = 1;

            // Header
            summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"LOAN TYPE PERFORMANCE ANALYSIS AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            summarySheet.Range(currentRow, 1, currentRow, 6).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "Loan Types", "Amount", "Total PrinBal", "Total PrinBal Arrears", "PAR %" };
            for (int i = 0; i < headers.Length; i++)
            {
                summarySheet.Cell(currentRow, i + 1).Value = headers[i];
                summarySheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                summarySheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                summarySheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                summarySheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalAmount = 0;
            decimal totalPrincipalBalance = 0;
            decimal totalArrears = 0;

            foreach (var group in groupedByLoanType)
            {
                decimal loanTypeAmount = 0;
                decimal loanTypePrincipalBalance = 0;
                decimal loanTypeArrears = 0;

                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;
                    decimal penalty = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                        penalty = lb.Penalty;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (currentSchedules.ContainsKey(loan.LoanNo) && currentSchedules[loan.LoanNo] != default)
                    {
                        if (asAtDate > currentSchedules[loan.LoanNo])
                        {
                            daysInArrears = (asAtDate - currentSchedules[loan.LoanNo]).Days;
                        }
                    }
                    else if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }

                    if (daysInArrears < 0) daysInArrears = 0;

                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    loanTypeAmount += loanIssued;
                    loanTypePrincipalBalance += currentBalance;
                    loanTypeArrears += arrears;
                }

                decimal parPercentage = loanTypePrincipalBalance > 0 ? (loanTypeArrears / loanTypePrincipalBalance) * 100 : 0;

                summarySheet.Cell(currentRow, 1).Value = group.LoanTypeName;
                summarySheet.Cell(currentRow, 2).Value = loanTypeAmount;
                summarySheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 3).Value = loanTypePrincipalBalance;
                summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 4).Value = loanTypeArrears;
                summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 5).Value = $"{parPercentage:N2}%";

                totalAmount += loanTypeAmount;
                totalPrincipalBalance += loanTypePrincipalBalance;
                totalArrears += loanTypeArrears;
                currentRow++;
            }

            // Grand Total Row
            currentRow++;
            decimal overallParPercentage = totalPrincipalBalance > 0 ? (totalArrears / totalPrincipalBalance) * 100 : 0;

            summarySheet.Cell(currentRow, 1).Value = "Overall Grand Total for the all Sacco:";
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 2).Value = totalAmount;
            summarySheet.Cell(currentRow, 2).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 3).Value = totalPrincipalBalance;
            summarySheet.Cell(currentRow, 3).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 4).Value = totalArrears;
            summarySheet.Cell(currentRow, 4).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 5).Value = $"{overallParPercentage:N2}%";
            summarySheet.Cell(currentRow, 5).Style.Font.SetBold();

            summarySheet.Columns().AdjustToContents();

            // Detailed Worksheet (Loans per Loan Type)
            var detailSheet = workbook.Worksheets.Add("Loan Details");
            currentRow = 1;

            detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            detailSheet.Cell(currentRow, 1).Value = $"LOAN DETAILS BY LOAN TYPE AS AT {asAtDate:dd/MM/yyyy}";
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] detailHeaders = { "Loan Type", "MemberNo", "Names", "LoanNo", "Amount", "Principal Balance", "Arrears", "Days Overdue" };
            for (int i = 0; i < detailHeaders.Length; i++)
            {
                detailSheet.Cell(currentRow, i + 1).Value = detailHeaders[i];
                detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                detailSheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var group in groupedByLoanType)
            {
                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (currentSchedules.ContainsKey(loan.LoanNo) && currentSchedules[loan.LoanNo] != default)
                    {
                        if (asAtDate > currentSchedules[loan.LoanNo])
                        {
                            daysInArrears = (asAtDate - currentSchedules[loan.LoanNo]).Days;
                        }
                    }
                    else if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }

                    if (daysInArrears < 0) daysInArrears = 0;
                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    detailSheet.Cell(currentRow, 1).Value = group.LoanTypeName;
                    detailSheet.Cell(currentRow, 2).Value = loan.MemberNo;
                    detailSheet.Cell(currentRow, 3).Value = fullName;
                    detailSheet.Cell(currentRow, 4).Value = loan.LoanNo;
                    detailSheet.Cell(currentRow, 5).Value = loanIssued;
                    detailSheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 6).Value = currentBalance;
                    detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 7).Value = arrears;
                    detailSheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 8).Value = daysInArrears;

                    currentRow++;
                }
            }

            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanTypePerformanceReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanTypePerformanceToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans grouped by loan type
            var loansByLoanType = await (from loan in _context.Loans
                                         join member in _context.Members
                                             on loan.MemberNo equals member.MemberNo
                                         join loantype in _context.Loantypes
                                             on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                         from lt in loanTypeJoin.DefaultIfEmpty()
                                         where loan.CompanyCode == companyCode
                                             && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                             && loan.AuditTime <= asAtDateEnd
                                         select new
                                         {
                                             loan.MemberNo,
                                             loan.LoanNo,
                                             loan.AuditTime,
                                             loan.LoanAmt,
                                             loan.Aamount,
                                             MemberSurname = member.Surname,
                                             MemberOtherNames = member.OtherNames,
                                             LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                         }).ToListAsync();

            if (!loansByLoanType.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanTypePerformanceReport");
            }

            var loanNos = loansByLoanType.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            var currentSchedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo) && s.Status != "Paid")
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    DueDate = g.OrderBy(s => s.InstallmentNo).Select(s => s.DueDate).FirstOrDefault()
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.DueDate);

            // Group by Loan Type
            var groupedByLoanType = loansByLoanType
                .GroupBy(l => l.LoanTypeName)
                .Select(g => new
                {
                    LoanTypeName = g.Key,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            var reportData = new List<LoanTypePerformanceReportViewModel>();
            decimal totalAmount = 0;
            decimal totalPrincipalBalance = 0;
            decimal totalArrears = 0;

            foreach (var group in groupedByLoanType)
            {
                decimal loanTypeAmount = 0;
                decimal loanTypePrincipalBalance = 0;
                decimal loanTypeArrears = 0;

                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (currentSchedules.ContainsKey(loan.LoanNo) && currentSchedules[loan.LoanNo] != default)
                    {
                        if (asAtDate > currentSchedules[loan.LoanNo])
                        {
                            daysInArrears = (asAtDate - currentSchedules[loan.LoanNo]).Days;
                        }
                    }
                    else if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }

                    if (daysInArrears < 0) daysInArrears = 0;
                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    loanTypeAmount += loanIssued;
                    loanTypePrincipalBalance += currentBalance;
                    loanTypeArrears += arrears;
                }

                decimal parPercentage = loanTypePrincipalBalance > 0 ? (loanTypeArrears / loanTypePrincipalBalance) * 100 : 0;

                totalAmount += loanTypeAmount;
                totalPrincipalBalance += loanTypePrincipalBalance;
                totalArrears += loanTypeArrears;

                reportData.Add(new LoanTypePerformanceReportViewModel
                {
                    LoanType = group.LoanTypeName,
                    Amount = loanTypeAmount,
                    TotalPrincipalBalance = loanTypePrincipalBalance,
                    TotalArrears = loanTypeArrears,
                    ParPercentage = parPercentage
                });
            }

            decimal overallParPercentage = totalPrincipalBalance > 0 ? (totalArrears / totalPrincipalBalance) * 100 : 0;

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
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text(companyName.ToUpper()).FontSize(16).Bold();
                        header.Item().AlignCenter().Text($"LOAN TYPE PERFORMANCE ANALYSIS AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Loan Types").Bold().FontSize(9);
                            header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Amount").Bold().FontSize(9);
                            header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Total PrinBal").Bold().FontSize(9);
                            header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Total PrinBal Arrears").Bold().FontSize(9);
                            header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("PAR %").Bold().FontSize(9);
                        });

                        foreach (var item in reportData)
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(item.LoanType).FontSize(9);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.Amount:N2}").FontSize(9);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalPrincipalBalance:N2}").FontSize(9);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalArrears:N2}").FontSize(9);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.ParPercentage:N2}%").FontSize(9);
                        }

                        // Grand Total Row
                        table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).Text("Overall Grand Total for the all Sacco:").Bold().FontSize(9);
                        table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalAmount:N2}").Bold().FontSize(9);
                        table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalPrincipalBalance:N2}").Bold().FontSize(9);
                        table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalArrears:N2}").Bold().FontSize(9);
                        table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{overallParPercentage:N2}%").Bold().FontSize(9);
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
            return File(content, "application/pdf", $"LoanTypePerformanceReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Loan Recovery Report

        [HttpGet]
        public IActionResult LoanRecoveryReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            var viewModel = new LoanRecoveryIndexViewModel
            {
                Groups = new List<LoanRecoveryGroupViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalPrincipal = 0,
                TotalInterest = 0,
                TotalAmount = 0,
                TotalAccrued = 0,
                TotalTransactions = 0,
                TotalLoanTypes = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanRecoveryReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanRecoveryReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all repayments within the date range
            var repayments = await (from r in _context.Repay
                                    join loan in _context.Loans on r.LoanNo equals loan.LoanNo
                                    join member in _context.Members on loan.MemberNo equals member.MemberNo
                                    join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                    from lt in loanTypeJoin.DefaultIfEmpty()
                                    where r.CompanyCode == companyCode
                                        && r.DateReceived >= startDate
                                        && r.DateReceived <= endDateAdjusted
                                        && r.Posted == true
                                    orderby r.DateReceived
                                    select new
                                    {
                                        r.DateReceived,
                                        r.LoanNo,
                                        r.MemberNo,
                                        r.Principal,
                                        r.Interest,
                                        r.Amount,
                                        r.IntrAccrued,
                                        r.LoanBalance,
                                        r.ReceiptNo,
                                        r.TransactionNo,
                                        loan.LoanCode,
                                        loan.ApplicDate,
                                        MemberSurname = member.Surname,
                                        MemberOtherNames = member.OtherNames,
                                        MemberFullName = member.FullName,
                                        LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                        LoanCodeValue = loan.LoanCode ?? ""
                                    }).ToListAsync();

            if (!repayments.Any())
            {
                var emptyViewModel = new LoanRecoveryIndexViewModel
                {
                    Groups = new List<LoanRecoveryGroupViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalPrincipal = 0,
                    TotalInterest = 0,
                    TotalAmount = 0,
                    TotalAccrued = 0,
                    TotalTransactions = 0,
                    TotalLoanTypes = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No repayments found for the selected date range.";

                return View("~/Views/Reports/LoanRecoveryReport.cshtml", emptyViewModel);
            }

            // Group by Loan Type
            var groupedByLoanType = repayments
                .GroupBy(r => new { r.LoanTypeName, r.LoanCodeValue })
                .Select(g => new LoanRecoveryGroupViewModel
                {
                    LoanType = g.Key.LoanTypeName,
                    LoanCode = g.Key.LoanCodeValue,
                    Repayments = g.Select(r => new LoanRecoveryReportViewModel
                    {
                        LoanType = r.LoanTypeName,
                        LoanCode = r.LoanCodeValue,
                        TransactionDate = r.DateReceived,
                        MemberNo = r.MemberNo ?? r.MemberNo,
                        LoanNo = r.LoanNo,
                        Names = !string.IsNullOrEmpty(r.MemberFullName)
                            ? r.MemberFullName
                            : $"{r.MemberSurname ?? ""} {r.MemberOtherNames ?? ""}".Trim(),
                        Principal = r.Principal ?? 0,
                        Interest = r.Interest ?? 0,
                        Amount = r.Amount ?? 0,
                        Accrued = r.IntrAccrued ?? 0,
                        LoanBalance = r.LoanBalance ?? 0,
                        ReceiptNo = r.ReceiptNo,
                        DueDate = null,
                        InstallmentNo = 0
                    }).OrderBy(r => r.TransactionDate).ToList(),
                    TotalPrincipal = g.Sum(r => r.Principal ?? 0),
                    TotalInterest = g.Sum(r => r.Interest ?? 0),
                    TotalAmount = g.Sum(r => r.Amount ?? 0),
                    TotalAccrued = g.Sum(r => r.IntrAccrued ?? 0),
                    TotalTransactions = g.Count()
                })
                .OrderBy(g => g.LoanType)
                .ToList();

            // Calculate overall totals
            decimal totalPrincipal = groupedByLoanType.Sum(g => g.TotalPrincipal);
            decimal totalInterest = groupedByLoanType.Sum(g => g.TotalInterest);
            decimal totalAmount = groupedByLoanType.Sum(g => g.TotalAmount);
            decimal totalAccrued = groupedByLoanType.Sum(g => g.TotalAccrued);
            int totalTransactions = groupedByLoanType.Sum(g => g.TotalTransactions);

            var viewModel = new LoanRecoveryIndexViewModel
            {
                Groups = groupedByLoanType,
                TotalPrincipal = totalPrincipal,
                TotalInterest = totalInterest,
                TotalAmount = totalAmount,
                TotalAccrued = totalAccrued,
                TotalTransactions = totalTransactions,
                TotalLoanTypes = groupedByLoanType.Count,
                StartDate = startDate,
                EndDate = endDate,
                HasData = repayments.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanRecoveryReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanRecoveryToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all repayments within the date range
            var repayments = await (from r in _context.Repay
                                    join loan in _context.Loans on r.LoanNo equals loan.LoanNo
                                    join member in _context.Members on loan.MemberNo equals member.MemberNo
                                    join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                    from lt in loanTypeJoin.DefaultIfEmpty()
                                    where r.CompanyCode == companyCode
                                        && r.DateReceived >= startDate
                                        && r.DateReceived <= endDateAdjusted
                                        && r.Posted == true
                                    orderby r.DateReceived
                                    select new
                                    {
                                        r.DateReceived,
                                        r.LoanNo,
                                        r.MemberNo,
                                        r.Principal,
                                        r.Interest,
                                        r.Amount,
                                        r.IntrAccrued,
                                        r.LoanBalance,
                                        MemberSurname = member.Surname,
                                        MemberOtherNames = member.OtherNames,
                                        MemberFullName = member.FullName,
                                        LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                    }).ToListAsync();

            if (!repayments.Any())
            {
                TempData["Error"] = "No repayments found for the selected date range";
                return RedirectToAction("LoanRecoveryReport");
            }

            using var workbook = new XLWorkbook();

            // Main Worksheet
            var worksheet = workbook.Worksheets.Add("Loan Recovery");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN RECOVERY BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}";
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Group by Loan Type
            var groupedByLoanType = repayments
                .GroupBy(r => r.LoanTypeName)
                .OrderBy(g => g.Key);

            string[] headers = { "LoanType", "Transaction Date", "MemberNo", "LoanNo", "Names", "Principal", "Interest", "Amount", "Loan Balance" };

            foreach (var group in groupedByLoanType)
            {
                // Loan Type Header
                worksheet.Cell(currentRow, 1).Value = group.Key.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 9).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                worksheet.Cell(currentRow, 1).Style.Fill.SetBackgroundColor(XLColor.LightBlue);
                currentRow++;

                // Headers
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }
                currentRow++;

                decimal groupPrincipal = 0;
                decimal groupInterest = 0;
                decimal groupAmount = 0;

                foreach (var repayment in group)
                {
                    string fullName = !string.IsNullOrEmpty(repayment.MemberFullName)
                        ? repayment.MemberFullName
                        : $"{repayment.MemberSurname ?? ""} {repayment.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = repayment.MemberNo;

                    decimal principal = repayment.Principal ?? 0;
                    decimal interest = repayment.Interest ?? 0;
                    decimal amount = repayment.Amount ?? 0;

                    groupPrincipal += principal;
                    groupInterest += interest;
                    groupAmount += amount;

                    worksheet.Cell(currentRow, 1).Value = "";
                    worksheet.Cell(currentRow, 2).Value = repayment.DateReceived?.ToString("dd/MM/yyyy");
                    worksheet.Cell(currentRow, 3).Value = repayment.MemberNo;
                    worksheet.Cell(currentRow, 4).Value = repayment.LoanNo;
                    worksheet.Cell(currentRow, 5).Value = fullName;
                    worksheet.Cell(currentRow, 6).Value = principal;
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 7).Value = interest;
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 8).Value = amount;
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 9).Value = repayment.LoanBalance ?? 0;
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

                    currentRow++;
                }

                // Subtotal row
                worksheet.Cell(currentRow, 5).Value = "SUBTOTAL:";
                worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                worksheet.Cell(currentRow, 6).Value = groupPrincipal;
                worksheet.Cell(currentRow, 6).Style.Font.SetBold();
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = groupInterest;
                worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 8).Value = groupAmount;
                worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                currentRow += 2;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanRecoveryReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanRecoveryToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all repayments within the date range
            var repayments = await (from r in _context.Repay
                                    join loan in _context.Loans on r.LoanNo equals loan.LoanNo
                                    join member in _context.Members on loan.MemberNo equals member.MemberNo
                                    join loantype in _context.Loantypes on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                    from lt in loanTypeJoin.DefaultIfEmpty()
                                    where r.CompanyCode == companyCode
                                        && r.DateReceived >= startDate
                                        && r.DateReceived <= endDateAdjusted
                                        && r.Posted == true
                                    orderby r.DateReceived
                                    select new
                                    {
                                        r.DateReceived,
                                        r.LoanNo,
                                        r.MemberNo,
                                        r.Principal,
                                        r.Interest,
                                        r.Amount,
                                        r.LoanBalance,
                                        MemberSurname = member.Surname,
                                        MemberOtherNames = member.OtherNames,
                                        MemberFullName = member.FullName,
                                        LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                    }).ToListAsync();

            if (!repayments.Any())
            {
                TempData["Error"] = "No repayments found for the selected date range";
                return RedirectToAction("LoanRecoveryReport");
            }

            // Group by Loan Type
            var groupedByLoanType = repayments
                .GroupBy(r => r.LoanTypeName)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    LoanType = g.Key,
                    Repayments = g.OrderBy(r => r.DateReceived).ToList(),
                    TotalPrincipal = g.Sum(r => r.Principal ?? 0),
                    TotalInterest = g.Sum(r => r.Interest ?? 0),
                    TotalAmount = g.Sum(r => r.Amount ?? 0)
                })
                .ToList();

            decimal grandTotalPrincipal = groupedByLoanType.Sum(g => g.TotalPrincipal);
            decimal grandTotalInterest = groupedByLoanType.Sum(g => g.TotalInterest);
            decimal grandTotalAmount = groupedByLoanType.Sum(g => g.TotalAmount);

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
                        header.Item().AlignCenter().Text($"LOAN RECOVERY BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        foreach (var group in groupedByLoanType)
                        {
                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);
                            contentCol.Item().Text(group.LoanType).FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.5f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(1.2f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Principal").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Interest").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Amount").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(8);
                                });

                                foreach (var repayment in group.Repayments)
                                {
                                    string fullName = !string.IsNullOrEmpty(repayment.MemberFullName)
                                        ? repayment.MemberFullName
                                        : $"{repayment.MemberSurname ?? ""} {repayment.MemberOtherNames ?? ""}".Trim();
                                    if (string.IsNullOrWhiteSpace(fullName)) fullName = repayment.MemberNo;

                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(repayment.DateReceived?.ToString("dd/MM/yyyy") ?? "-").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(repayment.MemberNo ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(repayment.LoanNo ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(fullName).FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Principal:N2}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Interest:N2}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.Amount:N2}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{repayment.LoanBalance:N2}").FontSize(7);
                                }

                                // Subtotal row
                                table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("Subtotal:").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalPrincipal:N2}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalInterest:N2}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{group.TotalAmount:N2}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4);
                            });
                        }

                        // Grand Total
                        contentCol.Item().PaddingTop(1, Unit.Centimetre);
                        contentCol.Item().Table(totalTable =>
                        {
                            totalTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(4f);
                                cols.RelativeColumn(1f);
                                cols.RelativeColumn(1f);
                                cols.RelativeColumn(1f);
                                cols.RelativeColumn(1f);
                            });

                            totalTable.Cell().ColumnSpan(4).Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(9);
                            totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandTotalPrincipal:N2}").Bold().FontSize(9);
                            totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandTotalInterest:N2}").Bold().FontSize(9);
                            totalTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandTotalAmount:N2}").Bold().FontSize(9);
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
            return File(content, "application/pdf", $"LoanRecoveryReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Loan Provision Report

        [HttpGet]
        public IActionResult LoanProvisionReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new AgingAnalysisIndexViewModel
            {
                Loans = new List<AgingAnalysisViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalLoans = 0,
                TotalLoanBalance = 0,
                TotalRequiredProvision = 0,
                TotalLoanAmount = 0,
                TotalPerforming = 0,
                TotalSpecialMention = 0,
                TotalWatchful = 0,
                TotalSubstandard = 0,
                TotalDoubtful = 0,
                TotalLoss = 0,
                TotalLossOver365 = 0,
                PerformingCount = 0,
                SpecialMentionCount = 0,
                WatchfulCount = 0,
                SubstandardCount = 0,
                DoubtfulCount = 0,
                LossCount = 0,
                LossOver365Count = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/LoanProvisionReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanProvisionReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.AuditTime,
                                              loan.RepayPeriod,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              loan.Interest,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              LoanTypeRepayPeriod = lt != null ? lt.RepayPeriod : (int?)null,
                                              InterestRateFromType = lt != null ? lt.Interest : null
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                var emptyViewModel = new AgingAnalysisIndexViewModel
                {
                    Loans = new List<AgingAnalysisViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    UserCompanyCode = companyCode,
                    CompanyName = companyName,
                    TotalLoans = 0,
                    TotalLoanBalance = 0,
                    TotalRequiredProvision = 0,
                    TotalLoanAmount = 0,
                    TotalPerforming = 0,
                    TotalSpecialMention = 0,
                    TotalWatchful = 0,
                    TotalSubstandard = 0,
                    TotalDoubtful = 0,
                    TotalLoss = 0,
                    TotalLossOver365 = 0,
                    PerformingCount = 0,
                    SpecialMentionCount = 0,
                    WatchfulCount = 0,
                    SubstandardCount = 0,
                    DoubtfulCount = 0,
                    LossCount = 0,
                    LossOver365Count = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanProvisionReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbal table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.Penalty
                });

            // Get latest repayments for arrears calculation
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var provisionLoans = new List<AgingAnalysisViewModel>();
            decimal totalLoanBalance = 0;
            decimal totalRequiredProvision = 0;
            decimal totalLoanAmount = 0;
            int performingCount = 0, specialMentionCount = 0, watchfulCount = 0;
            int substandardCount = 0, doubtfulCount = 0, lossCount = 0, lossOver365Count = 0;

            foreach (var loan in loansWithDetails)
            {
                // Get current balance
                decimal currentBalance = 0;
                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo].Balance;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Skip if loan is fully paid (balance <= 0)
                if (currentBalance <= 0) continue;

                // Get original loan amount issued
                decimal loanAmount = loan.Aamount ?? loan.LoanAmt ?? 0;

                // Get last payment date
                DateTime? lastPaymentDate = null;
                if (latestRepayments.ContainsKey(loan.LoanNo))
                {
                    lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                }

                // Calculate Days In Arrears
                int daysInArrears = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        daysInArrears = (asAtDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        daysInArrears = (asAtDate - firstDueDate).Days;
                    }
                }
                if (daysInArrears < 0) daysInArrears = 0;

                // Determine aging category
                int category = AgingCategories.GetCategoryFromDays(daysInArrears);
                string classification = AgingCategories.GetCategoryName(category);
                string provisionGrade = AgingCategories.GetProvisionGrade(category);
                decimal provisionRate = AgingCategories.GetProvisionRate(category);
                decimal requiredProvision = currentBalance * provisionRate;

                // Count by category
                switch (category)
                {
                    case AgingCategories.PERFORMING:
                        performingCount++;
                        break;
                    case AgingCategories.SPECIAL_MENTION:
                        specialMentionCount++;
                        break;
                    case AgingCategories.WATCHFUL:
                        watchfulCount++;
                        break;
                    case AgingCategories.SUBSTANDARD:
                        substandardCount++;
                        break;
                    case AgingCategories.DOUBTFUL:
                        doubtfulCount++;
                        break;
                    case AgingCategories.LOSS:
                        lossCount++;
                        break;
                    case AgingCategories.LOSS_OVER_365:
                        lossOver365Count++;
                        break;
                }

                totalLoanBalance += currentBalance;
                totalRequiredProvision += requiredProvision;
                totalLoanAmount += loanAmount;

                // Build member name
                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "N/A";

                provisionLoans.Add(new AgingAnalysisViewModel
                {
                    LoanNo = loan.LoanNo,
                    MemberNo = loan.MemberNo,
                    FullName = fullName,
                    LoanBalance = currentBalance,
                    LoanAmount = loanAmount,
                    DateIssued = loan.AuditTime,
                    DaysInArrears = daysInArrears,
                    LastRepayDate = lastPaymentDate,
                    ValueChain = loan.LoanName ?? "Unknown",
                    Classification = classification,
                    ArrearsCategory = category,
                    ProvisionGrade = provisionGrade,
                    ProvisionRate = provisionRate,
                    RequiredProvision = requiredProvision
                });
            }

            var viewModel = new AgingAnalysisIndexViewModel
            {
                Loans = provisionLoans.OrderBy(l => l.DaysInArrears).ToList(),
                AsAtDate = asAtDate,
                HasData = provisionLoans.Any(),
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalLoans = provisionLoans.Count,
                TotalLoanBalance = totalLoanBalance,
                TotalRequiredProvision = totalRequiredProvision,
                TotalLoanAmount = totalLoanAmount,
                PerformingCount = performingCount,
                SpecialMentionCount = specialMentionCount,
                WatchfulCount = watchfulCount,
                SubstandardCount = substandardCount,
                DoubtfulCount = doubtfulCount,
                LossCount = lossCount,
                LossOver365Count = lossOver365Count
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanProvisionReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanProvisionToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanProvisionReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Loan Provision");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"LOAN PROVISIONING REPORT";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"As At: {asAtDate:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 11).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Names", "LoanNo", "Loan Amount", "Date Issued", "Loan Balance",
                         "Days In Arrears", "Provision Grade", "Provision Rate", "Required Provision" };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalLoanAmount = 0;
            decimal totalLoanBalance = 0;
            decimal totalRequiredProvision = 0;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal loanAmount = loan.Aamount ?? loan.LoanAmt ?? 0;

                DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                int daysInArrears = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        daysInArrears = (asAtDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        daysInArrears = (asAtDate - firstDueDate).Days;
                    }
                }
                if (daysInArrears < 0) daysInArrears = 0;

                int category = AgingCategories.GetCategoryFromDays(daysInArrears);
                string provisionGrade = AgingCategories.GetProvisionGrade(category);
                decimal provisionRate = AgingCategories.GetProvisionRate(category);
                decimal requiredProvision = currentBalance * provisionRate;

                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                totalLoanAmount += loanAmount;
                totalLoanBalance += currentBalance;
                totalRequiredProvision += requiredProvision;

                worksheet.Cell(currentRow, 1).Value = loan.MemberNo;
                worksheet.Cell(currentRow, 2).Value = fullName;
                worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
                worksheet.Cell(currentRow, 4).Value = loanAmount;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Value = loan.AuditTime.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 6).Value = currentBalance;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = daysInArrears;
                worksheet.Cell(currentRow, 8).Value = provisionGrade;
                worksheet.Cell(currentRow, 9).Value = provisionRate.ToString("P2");
                worksheet.Cell(currentRow, 10).Value = requiredProvision;
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

                currentRow++;
            }

            // Totals row
            currentRow++;
            worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
            worksheet.Cell(currentRow, 3).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Value = totalLoanAmount;
            worksheet.Cell(currentRow, 4).Style.Font.SetBold();
            worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 6).Value = totalLoanBalance;
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(currentRow, 10).Value = totalRequiredProvision;
            worksheet.Cell(currentRow, 10).Style.Font.SetBold();
            worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanProvisionReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanProvisionToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              LoanName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanProvisionReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            var reportData = new List<AgingAnalysisViewModel>();
            decimal totalLoanAmount = 0;
            decimal totalLoanBalance = 0;
            decimal totalRequiredProvision = 0;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal loanAmount = loan.Aamount ?? loan.LoanAmt ?? 0;

                DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                int daysInArrears = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (asAtDate > nextDueDate)
                    {
                        daysInArrears = (asAtDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (asAtDate > firstDueDate)
                    {
                        daysInArrears = (asAtDate - firstDueDate).Days;
                    }
                }
                if (daysInArrears < 0) daysInArrears = 0;

                int category = AgingCategories.GetCategoryFromDays(daysInArrears);
                string provisionGrade = AgingCategories.GetProvisionGrade(category);
                decimal provisionRate = AgingCategories.GetProvisionRate(category);
                decimal requiredProvision = currentBalance * provisionRate;

                string fullName = $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                totalLoanAmount += loanAmount;
                totalLoanBalance += currentBalance;
                totalRequiredProvision += requiredProvision;

                reportData.Add(new AgingAnalysisViewModel
                {
                    MemberNo = loan.MemberNo,
                    FullName = fullName,
                    LoanNo = loan.LoanNo,
                    LoanAmount = loanAmount,
                    DateIssued = loan.AuditTime,
                    LoanBalance = currentBalance,
                    DaysInArrears = daysInArrears,
                    ProvisionGrade = provisionGrade,
                    ProvisionRate = provisionRate,
                    RequiredProvision = requiredProvision,
                    ValueChain = loan.LoanName ?? "Unknown"
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
                        header.Item().AlignCenter().Text($"LOAN PROVISIONING REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"As At: {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(10);
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.9f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.7f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.0f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Amount").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Date Issued").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Days").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Provision Grade").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Provision Rate").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Required Provision").Bold().FontSize(8);
                        });

                        foreach (var loan in reportData.OrderBy(l => l.DaysInArrears))
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(loan.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.FullName ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanAmount:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DateIssued?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.LoanBalance:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(loan.DaysInArrears.ToString()).FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(loan.ProvisionGrade ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.ProvisionRate:P2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{loan.RequiredProvision:N2}").FontSize(7);
                        }

                        // Totals row
                        table.Cell().ColumnSpan(3).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalLoanAmount:N2}").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalLoanBalance:N2}").Bold().FontSize(8);
                        table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalRequiredProvision:N2}").Bold().FontSize(8);
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
            return File(content, "application/pdf", $"LoanProvisionReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Loan Product Analysis Report

        [HttpGet]
        public IActionResult LoanProductAnalysisReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            var viewModel = new LoanProductAnalysisIndexViewModel
            {
                LoanProducts = new List<LoanProductAnalysisGroupViewModel>(),
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalNoOfLoanees = 0,
                TotalUnpaidInterest = 0,
                TotalLoanIssued = 0,
                TotalLoanBalance = 0,
                TotalArrears = 0,
                OverallParPercentage = 0,
                TotalLoanTypes = 0
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/LoanProductAnalysisReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> LoanProductAnalysisReport(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans with member and loan type data
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.LoanCode,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              LoanCodeValue = loan.LoanCode ?? ""
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                var emptyViewModel = new LoanProductAnalysisIndexViewModel
                {
                    LoanProducts = new List<LoanProductAnalysisGroupViewModel>(),
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalNoOfLoanees = 0,
                    TotalUnpaidInterest = 0,
                    TotalLoanIssued = 0,
                    TotalLoanBalance = 0,
                    TotalArrears = 0,
                    OverallParPercentage = 0,
                    TotalLoanTypes = 0
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found as at the selected date.";

                return View("~/Views/Reports/LoanProductAnalysisReport.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances from Loanbal table
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new
                {
                    lb.Balance,
                    lb.IntrOwed,
                    lb.Penalty
                });

            // Get latest repayments for arrears calculation
            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Group by Loan Type
            var groupedByLoanType = loansWithDetails
                .GroupBy(l => new { l.LoanTypeName, l.LoanCodeValue })
                .Select(g => new
                {
                    LoanTypeName = g.Key.LoanTypeName,
                    LoanCode = g.Key.LoanCodeValue,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            var reportData = new List<LoanProductAnalysisGroupViewModel>();
            decimal totalUnpaidInterest = 0;
            decimal totalLoanIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int totalNoOfLoanees = 0;
            int totalLoanTypes = 0;

            foreach (var group in groupedByLoanType)
            {
                var groupDetails = new List<LoanProductAnalysisDetailViewModel>();
                decimal groupUnpaidInterest = 0;
                decimal groupLoanIssued = 0;
                decimal groupLoanBalance = 0;
                decimal groupArrears = 0;
                var uniqueMembers = new HashSet<string>();

                foreach (var loan in group.Loans)
                {
                    // Get balance details
                    decimal currentBalance = 0;
                    decimal unpaidInterest = 0;
                    decimal penalty = 0;

                    if (loanBalances.ContainsKey(loan.LoanNo))
                    {
                        var lb = loanBalances[loan.LoanNo];
                        currentBalance = lb.Balance;
                        unpaidInterest = lb.IntrOwed;
                        penalty = lb.Penalty;
                    }
                    else
                    {
                        currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                    }

                    // Skip if loan is fully paid
                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    // Calculate loan issued amount
                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = null;

                    if (latestRepayments.ContainsKey(loan.LoanNo))
                    {
                        lastPaymentDate = latestRepayments[loan.LoanNo].LastPaymentDate;
                    }

                    if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }
                    if (daysInArrears < 0) daysInArrears = 0;

                    // Calculate arrears (overdue amount)
                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    // Build member name
                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    groupUnpaidInterest += unpaidInterest;
                    groupLoanIssued += loanIssued;
                    groupLoanBalance += currentBalance;
                    groupArrears += arrears;
                    uniqueMembers.Add(loan.MemberNo);

                    groupDetails.Add(new LoanProductAnalysisDetailViewModel
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        UnpaidInterest = unpaidInterest,
                        LoanIssued = loanIssued,
                        DateIssued = loan.AuditTime,
                        LoanBalance = currentBalance,
                        Arrears = arrears,
                        ParPercentage = 0,
                        LoanNo = loan.LoanNo
                    });
                }

                // Calculate PAR % for this loan type
                decimal groupParPercentage = 0;
                if (groupLoanBalance > 0)
                {
                    groupParPercentage = (groupArrears / groupLoanBalance) * 100;
                }

                totalUnpaidInterest += groupUnpaidInterest;
                totalLoanIssued += groupLoanIssued;
                totalLoanBalance += groupLoanBalance;
                totalArrears += groupArrears;
                totalNoOfLoanees += uniqueMembers.Count;
                totalLoanTypes++;

                reportData.Add(new LoanProductAnalysisGroupViewModel
                {
                    LoanType = group.LoanTypeName,
                    LoanCode = group.LoanCode,
                    NoOfLoanees = uniqueMembers.Count,
                    TotalUnpaidInterest = groupUnpaidInterest,
                    TotalLoanIssued = groupLoanIssued,
                    TotalLoanBalance = groupLoanBalance,
                    TotalArrears = groupArrears,
                    ParPercentage = groupParPercentage,
                    Loans = groupDetails
                });
            }

            // Calculate Overall PAR %
            decimal overallParPercentage = 0;
            if (totalLoanBalance > 0)
            {
                overallParPercentage = (totalArrears / totalLoanBalance) * 100;
            }

            var viewModel = new LoanProductAnalysisIndexViewModel
            {
                LoanProducts = reportData,
                TotalNoOfLoanees = totalNoOfLoanees,
                TotalUnpaidInterest = totalUnpaidInterest,
                TotalLoanIssued = totalLoanIssued,
                TotalLoanBalance = totalLoanBalance,
                TotalArrears = totalArrears,
                OverallParPercentage = overallParPercentage,
                TotalLoanTypes = totalLoanTypes,
                AsAtDate = asAtDate,
                HasData = reportData.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/LoanProductAnalysisReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanProductAnalysisToExcel(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                              LoanCodeValue = loan.LoanCode ?? ""
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanProductAnalysisReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            // Group by Loan Type
            var groupedByLoanType = loansWithDetails
                .GroupBy(l => new { l.LoanTypeName, l.LoanCodeValue })
                .Select(g => new
                {
                    LoanTypeName = g.Key.LoanTypeName,
                    LoanCode = g.Key.LoanCodeValue,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            using var workbook = new XLWorkbook();

            // Summary Worksheet
            var summarySheet = workbook.Worksheets.Add("Loan Product Analysis");
            int currentRow = 1;

            // Header
            summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            summarySheet.Range(currentRow, 1, currentRow, 8).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"LOAN PRODUCTS ANALYSIS AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}";
            summarySheet.Range(currentRow, 1, currentRow, 8).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            summarySheet.Range(currentRow, 1, currentRow, 8).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "Loan Type", "No of Loanees", "Unpaid Interest", "Loan Issued", "Loan Balance", "Arrears", "PAR %" };
            for (int i = 0; i < headers.Length; i++)
            {
                summarySheet.Cell(currentRow, i + 1).Value = headers[i];
                summarySheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                summarySheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                summarySheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                summarySheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            decimal totalUnpaidInterest = 0;
            decimal totalLoanIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int totalNoOfLoanees = 0;

            foreach (var group in groupedByLoanType)
            {
                decimal groupUnpaidInterest = 0;
                decimal groupLoanIssued = 0;
                decimal groupLoanBalance = 0;
                decimal groupArrears = 0;
                var uniqueMembers = new HashSet<string>();

                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;
                    decimal penalty = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Penalty : 0;

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    // Calculate days in arrears
                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }
                    if (daysInArrears < 0) daysInArrears = 0;

                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    groupUnpaidInterest += unpaidInterest;
                    groupLoanIssued += loanIssued;
                    groupLoanBalance += currentBalance;
                    groupArrears += arrears;
                    uniqueMembers.Add(loan.MemberNo);
                }

                decimal groupParPercentage = groupLoanBalance > 0 ? (groupArrears / groupLoanBalance) * 100 : 0;

                summarySheet.Cell(currentRow, 1).Value = group.LoanTypeName;
                summarySheet.Cell(currentRow, 2).Value = uniqueMembers.Count;
                summarySheet.Cell(currentRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summarySheet.Cell(currentRow, 3).Value = groupUnpaidInterest;
                summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 4).Value = groupLoanIssued;
                summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 5).Value = groupLoanBalance;
                summarySheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 6).Value = groupArrears;
                summarySheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 7).Value = $"{groupParPercentage:N2}%";

                totalUnpaidInterest += groupUnpaidInterest;
                totalLoanIssued += groupLoanIssued;
                totalLoanBalance += groupLoanBalance;
                totalArrears += groupArrears;
                totalNoOfLoanees += uniqueMembers.Count;
                currentRow++;
            }

            // Totals row
            currentRow++;
            decimal overallParPercentage = totalLoanBalance > 0 ? (totalArrears / totalLoanBalance) * 100 : 0;

            summarySheet.Cell(currentRow, 1).Value = "TOTALS:";
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 2).Value = totalNoOfLoanees;
            summarySheet.Cell(currentRow, 2).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 3).Value = totalUnpaidInterest;
            summarySheet.Cell(currentRow, 3).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 4).Value = totalLoanIssued;
            summarySheet.Cell(currentRow, 4).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 5).Value = totalLoanBalance;
            summarySheet.Cell(currentRow, 5).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 6).Value = totalArrears;
            summarySheet.Cell(currentRow, 6).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 7).Value = $"{overallParPercentage:N2}%";
            summarySheet.Cell(currentRow, 7).Style.Font.SetBold();

            summarySheet.Columns().AdjustToContents();

            // Detailed Worksheet (Loans per Product)
            var detailSheet = workbook.Worksheets.Add("Loan Details");
            currentRow = 1;

            detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            detailSheet.Cell(currentRow, 1).Value = $"LOAN DETAILS BY PRODUCT AS AT {asAtDate:dd/MM/yyyy}";
            detailSheet.Range(currentRow, 1, currentRow, 8).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] detailHeaders = { "Loan Type", "MemberNo", "Names", "LoanNo", "Unpaid Interest", "Loan Issued", "Date Issued", "Loan Balance", "Arrears" };
            for (int i = 0; i < detailHeaders.Length; i++)
            {
                detailSheet.Cell(currentRow, i + 1).Value = detailHeaders[i];
                detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                detailSheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var group in groupedByLoanType)
            {
                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }
                    if (daysInArrears < 0) daysInArrears = 0;

                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    detailSheet.Cell(currentRow, 1).Value = group.LoanTypeName;
                    detailSheet.Cell(currentRow, 2).Value = loan.MemberNo;
                    detailSheet.Cell(currentRow, 3).Value = fullName;
                    detailSheet.Cell(currentRow, 4).Value = loan.LoanNo;
                    detailSheet.Cell(currentRow, 5).Value = unpaidInterest;
                    detailSheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 6).Value = loanIssued;
                    detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 7).Value = loan.AuditTime.ToString("dd/MM/yyyy");
                    detailSheet.Cell(currentRow, 8).Value = currentBalance;
                    detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(currentRow, 9).Value = arrears;
                    detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

                    currentRow++;
                }
            }

            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LoanProductAnalysisReport_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportLoanProductAnalysisToPdf(DateTime asAtDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var loansWithDetails = await (from loan in _context.Loans
                                          join member in _context.Members
                                              on loan.MemberNo equals member.MemberNo
                                          join loantype in _context.Loantypes
                                              on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                          from lt in loanTypeJoin.DefaultIfEmpty()
                                          where loan.CompanyCode == companyCode
                                              && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                              && loan.AuditTime <= asAtDateEnd
                                          select new
                                          {
                                              loan.MemberNo,
                                              loan.LoanNo,
                                              loan.AuditTime,
                                              loan.LoanAmt,
                                              loan.Aamount,
                                              MemberSurname = member.Surname,
                                              MemberOtherNames = member.OtherNames,
                                              MemberFullName = member.FullName,
                                              LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                          }).ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No active loans found for the selected date";
                return RedirectToAction("LoanProductAnalysisReport");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty });

            var latestRepayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g.LastPaymentDate);

            var groupedByLoanType = loansWithDetails
                .GroupBy(l => l.LoanTypeName)
                .Select(g => new
                {
                    LoanTypeName = g.Key,
                    Loans = g.ToList()
                })
                .OrderBy(g => g.LoanTypeName)
                .ToList();

            var reportData = new List<LoanProductAnalysisGroupViewModel>();
            decimal totalUnpaidInterest = 0;
            decimal totalLoanIssued = 0;
            decimal totalLoanBalance = 0;
            decimal totalArrears = 0;
            int totalNoOfLoanees = 0;

            foreach (var group in groupedByLoanType)
            {
                var groupDetails = new List<LoanProductAnalysisDetailViewModel>();
                decimal groupUnpaidInterest = 0;
                decimal groupLoanIssued = 0;
                decimal groupLoanBalance = 0;
                decimal groupArrears = 0;
                var uniqueMembers = new HashSet<string>();

                foreach (var loan in group.Loans)
                {
                    decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                    decimal unpaidInterest = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].IntrOwed : 0;

                    if (currentBalance <= 0 && unpaidInterest <= 0) continue;

                    decimal loanIssued = loan.Aamount ?? loan.LoanAmt ?? 0;

                    int daysInArrears = 0;
                    DateTime? lastPaymentDate = latestRepayments.ContainsKey(loan.LoanNo) ? latestRepayments[loan.LoanNo] : null;

                    if (lastPaymentDate.HasValue)
                    {
                        DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                        if (asAtDate > nextDueDate)
                        {
                            daysInArrears = (asAtDate - nextDueDate).Days;
                        }
                    }
                    else
                    {
                        DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                        if (asAtDate > firstDueDate)
                        {
                            daysInArrears = (asAtDate - firstDueDate).Days;
                        }
                    }
                    if (daysInArrears < 0) daysInArrears = 0;

                    decimal arrears = daysInArrears > 0 ? currentBalance : 0;

                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    groupUnpaidInterest += unpaidInterest;
                    groupLoanIssued += loanIssued;
                    groupLoanBalance += currentBalance;
                    groupArrears += arrears;
                    uniqueMembers.Add(loan.MemberNo);

                    groupDetails.Add(new LoanProductAnalysisDetailViewModel
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        UnpaidInterest = unpaidInterest,
                        LoanIssued = loanIssued,
                        DateIssued = loan.AuditTime,
                        LoanBalance = currentBalance,
                        Arrears = arrears,
                        LoanNo = loan.LoanNo
                    });
                }

                decimal groupParPercentage = groupLoanBalance > 0 ? (groupArrears / groupLoanBalance) * 100 : 0;

                totalUnpaidInterest += groupUnpaidInterest;
                totalLoanIssued += groupLoanIssued;
                totalLoanBalance += groupLoanBalance;
                totalArrears += groupArrears;
                totalNoOfLoanees += uniqueMembers.Count;

                reportData.Add(new LoanProductAnalysisGroupViewModel
                {
                    LoanType = group.LoanTypeName,
                    NoOfLoanees = uniqueMembers.Count,
                    TotalUnpaidInterest = groupUnpaidInterest,
                    TotalLoanIssued = groupLoanIssued,
                    TotalLoanBalance = groupLoanBalance,
                    TotalArrears = groupArrears,
                    ParPercentage = groupParPercentage,
                    Loans = groupDetails
                });
            }

            decimal overallParPercentage = totalLoanBalance > 0 ? (totalArrears / totalLoanBalance) * 100 : 0;

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
                        header.Item().AlignCenter().Text($"LOAN PRODUCTS ANALYSIS AS AT {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        // Summary Table by Loan Type
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(0.5f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(0.8f);
                            });

                            summaryTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Loan Type").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("No of Loanees").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Unpaid Interest").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Loan Issued").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Loan Balance").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("Arrears").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#17a2b8").Padding(4).AlignCenter().Text("PAR %").Bold().FontSize(8);
                            });

                            foreach (var item in reportData)
                            {
                                summaryTable.Cell().Border(0.2f).Padding(4).Text(item.LoanType).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.NoOfLoanees.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalUnpaidInterest:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalLoanIssued:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalLoanBalance:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalArrears:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.ParPercentage:N2}%").FontSize(8);
                            }

                            // Totals row
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).Text("TOTALS:").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text(totalNoOfLoanees.ToString()).Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalUnpaidInterest:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalLoanIssued:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalLoanBalance:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{totalArrears:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{overallParPercentage:N2}%").Bold().FontSize(9);
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
            return File(content, "application/pdf", $"LoanProductAnalysisReport_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Delinquent Loans Report

        [HttpGet]
        public IActionResult DelinquentLoansReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now.AddMonths(-3);
            var endDate = DateTime.Now;

            var viewModel = new DelinquentLoansIndexViewModel
            {
                DelinquentLoans = new List<DelinquentLoansReportViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalDefaulters = 0,
                TotalExpectedAmount = 0,
                TotalCollectedAmount = 0,
                TotalDefaultedAmount = 0,
                OverallRatePercentage = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/DelinquentLoansReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> DelinquentLoansReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all loans that are overdue (delinquent) - loans with days in arrears > 30 days
            // A loan is considered delinquent if it has overdue payments

            // First, get all active/disbursed loans
            var activeLoans = await (from loan in _context.Loans
                                     join member in _context.Members
                                         on loan.MemberNo equals member.MemberNo
                                     join loantype in _context.Loantypes
                                         on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                     from lt in loanTypeJoin.DefaultIfEmpty()
                                     where loan.CompanyCode == companyCode
                                         && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                         && loan.AuditTime <= endDateAdjusted
                                     select new
                                     {
                                         loan.MemberNo,
                                         loan.LoanNo,
                                         loan.LoanCode,
                                         loan.AuditTime,
                                         loan.LoanAmt,
                                         loan.Aamount,
                                         loan.Interest,
                                         MemberSurname = member.Surname,
                                         MemberOtherNames = member.OtherNames,
                                         MemberFullName = member.FullName,
                                         LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                                         GracePeriod = lt != null ? lt.GracePeriod : 30
                                     }).ToListAsync();

            if (!activeLoans.Any())
            {
                var emptyViewModel = new DelinquentLoansIndexViewModel
                {
                    DelinquentLoans = new List<DelinquentLoansReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalDefaulters = 0,
                    TotalExpectedAmount = 0,
                    TotalCollectedAmount = 0,
                    TotalDefaultedAmount = 0,
                    OverallRatePercentage = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No active loans found for the selected date range.";

                return View("~/Views/Reports/DelinquentLoansReport.cshtml", emptyViewModel);
            }

            var loanNos = activeLoans.Select(l => l.LoanNo).ToList();

            // Get loan balances
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty, lb.RepayRate });

            // Get repayment history for each loan
            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= endDateAdjusted && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalCollected = g.Sum(r => r.Amount ?? 0),
                    TotalPrincipalCollected = g.Sum(r => r.Principal ?? 0),
                    TotalInterestCollected = g.Sum(r => r.Interest ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    PaymentCount = g.Count()
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get current schedule to determine expected payments
            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment),
                    PaidAmount = g.Sum(s => s.PaidTotal),
                    OverdueAmount = g.Where(s => s.DueDate < DateTime.Now && s.Status != "Paid").Sum(s => s.OutstandingTotal)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Identify delinquent loans (loans with overdue payments)
            var delinquentLoans = new List<DelinquentLoanDetail>();
            var defaultersBySacco = new Dictionary<string, List<DelinquentLoanDetail>>();

            var currentDate = endDate;

            foreach (var loan in activeLoans)
            {
                decimal currentBalance = 0;
                decimal expectedRepayment = 0;
                decimal amountCollected = 0;
                decimal defaultedAmount = 0;
                DateTime? lastPaymentDate = null;
                int daysOverdue = 0;

                // Get balance
                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo].Balance;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Skip fully paid loans
                if (currentBalance <= 0) continue;

                // Get collected amount
                if (repayments.ContainsKey(loan.LoanNo))
                {
                    amountCollected = repayments[loan.LoanNo].TotalCollected;
                    lastPaymentDate = repayments[loan.LoanNo].LastPaymentDate;
                }

                // Get expected repayment
                if (schedules.ContainsKey(loan.LoanNo))
                {
                    expectedRepayment = schedules[loan.LoanNo].TotalExpected;
                }
                else
                {
                    // Fallback: calculate expected based on loan amount and period
                    expectedRepayment = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Calculate days overdue
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                // Consider loan delinquent if days overdue > 30
                if (daysOverdue > 30)
                {
                    defaultedAmount = currentBalance; // Outstanding balance is considered defaulted
                    decimal totalExpectedForPeriod = expectedRepayment;

                    // Build member name
                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    var detail = new DelinquentLoanDetail
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        LoanType = loan.LoanTypeName,
                        DateIssued = loan.AuditTime,
                        LoanAmount = loan.Aamount ?? loan.LoanAmt ?? 0,
                        OutstandingBalance = currentBalance,
                        ExpectedRepayment = expectedRepayment,
                        AmountCollected = amountCollected,
                        DefaultedAmount = defaultedAmount,
                        DaysOverdue = daysOverdue,
                        LastPaymentDate = lastPaymentDate ?? loan.AuditTime
                    };

                    delinquentLoans.Add(detail);

                    // Group by Sacco (Company)
                    if (!defaultersBySacco.ContainsKey(companyName))
                    {
                        defaultersBySacco[companyName] = new List<DelinquentLoanDetail>();
                    }
                    defaultersBySacco[companyName].Add(detail);
                }
            }

            if (!delinquentLoans.Any())
            {
                var emptyViewModel = new DelinquentLoansIndexViewModel
                {
                    DelinquentLoans = new List<DelinquentLoansReportViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalDefaulters = 0,
                    TotalExpectedAmount = 0,
                    TotalCollectedAmount = 0,
                    TotalDefaultedAmount = 0,
                    OverallRatePercentage = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No delinquent loans found for the selected date range.";

                return View("~/Views/Reports/DelinquentLoansReport.cshtml", emptyViewModel);
            }

            var reportData = new List<DelinquentLoansReportViewModel>();
            decimal totalExpectedAmount = 0;
            decimal totalCollectedAmount = 0;
            decimal totalDefaultedAmount = 0;
            int totalDefaulters = 0;

            foreach (var sacco in defaultersBySacco)
            {
                var saccoDefaulters = sacco.Value;
                var uniqueMembers = saccoDefaulters.Select(d => d.MemberNo).Distinct().Count();
                var saccoExpected = saccoDefaulters.Sum(d => d.ExpectedRepayment);
                var saccoCollected = saccoDefaulters.Sum(d => d.AmountCollected);
                var saccoDefaulted = saccoDefaulters.Sum(d => d.DefaultedAmount);
                var saccoRate = saccoExpected > 0 ? (saccoDefaulted / saccoExpected) * 100 : 0;

                totalExpectedAmount += saccoExpected;
                totalCollectedAmount += saccoCollected;
                totalDefaultedAmount += saccoDefaulted;
                totalDefaulters += uniqueMembers;

                reportData.Add(new DelinquentLoansReportViewModel
                {
                    SaccoName = sacco.Key,
                    NoOfDefaulters = uniqueMembers,
                    ExpectedAmount = saccoExpected,
                    CollectedAmount = saccoCollected,
                    DefaultedAmount = saccoDefaulted,
                    RatePercentage = saccoRate,
                    Defaulters = saccoDefaulters
                });
            }

            decimal overallRate = totalExpectedAmount > 0 ? (totalDefaultedAmount / totalExpectedAmount) * 100 : 0;

            var viewModel = new DelinquentLoansIndexViewModel
            {
                DelinquentLoans = reportData,
                TotalDefaulters = totalDefaulters,
                TotalExpectedAmount = totalExpectedAmount,
                TotalCollectedAmount = totalCollectedAmount,
                TotalDefaultedAmount = totalDefaultedAmount,
                OverallRatePercentage = overallRate,
                StartDate = startDate,
                EndDate = endDate,
                HasData = delinquentLoans.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/DelinquentLoansReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportDelinquentLoansToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var activeLoans = await (from loan in _context.Loans
                                     join member in _context.Members
                                         on loan.MemberNo equals member.MemberNo
                                     join loantype in _context.Loantypes
                                         on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                     from lt in loanTypeJoin.DefaultIfEmpty()
                                     where loan.CompanyCode == companyCode
                                         && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                         && loan.AuditTime <= endDateAdjusted
                                     select new
                                     {
                                         loan.MemberNo,
                                         loan.LoanNo,
                                         loan.AuditTime,
                                         loan.LoanAmt,
                                         loan.Aamount,
                                         MemberSurname = member.Surname,
                                         MemberOtherNames = member.OtherNames,
                                         MemberFullName = member.FullName,
                                         LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                     }).ToListAsync();

            if (!activeLoans.Any())
            {
                TempData["Error"] = "No active loans found for the selected date range";
                return RedirectToAction("DelinquentLoansReport");
            }

            var loanNos = activeLoans.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= endDateAdjusted && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalCollected = g.Sum(r => r.Amount ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var delinquentDetails = new List<DelinquentLoanDetail>();
            var currentDate = endDate;

            foreach (var loan in activeLoans)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal amountCollected = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalCollected : 0;
                decimal expectedRepayment = schedules.ContainsKey(loan.LoanNo) ? schedules[loan.LoanNo].TotalExpected : (loan.Aamount ?? loan.LoanAmt ?? 0);
                DateTime? lastPaymentDate = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].LastPaymentDate : null;

                int daysOverdue = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                if (daysOverdue > 30)
                {
                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    delinquentDetails.Add(new DelinquentLoanDetail
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        LoanType = loan.LoanTypeName,
                        DateIssued = loan.AuditTime,
                        LoanAmount = loan.Aamount ?? loan.LoanAmt ?? 0,
                        OutstandingBalance = currentBalance,
                        ExpectedRepayment = expectedRepayment,
                        AmountCollected = amountCollected,
                        DefaultedAmount = currentBalance,
                        DaysOverdue = daysOverdue,
                        LastPaymentDate = lastPaymentDate ?? loan.AuditTime
                    });
                }
            }

            using var workbook = new XLWorkbook();

            // Summary Worksheet
            var summarySheet = workbook.Worksheets.Add("Delinquent Loans Summary");
            int currentRow = 1;

            summarySheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            summarySheet.Range(currentRow, 1, currentRow, 7).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"DELINQUENT LOANS REPORT";
            summarySheet.Range(currentRow, 1, currentRow, 7).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
            summarySheet.Range(currentRow, 1, currentRow, 7).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            summarySheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            summarySheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            summarySheet.Range(currentRow, 1, currentRow, 7).Merge();
            summarySheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "Sacco", "No of Defaulters", "Expected Amount", "Collected Amount", "Defaulted Amount", "Rate %" };
            for (int i = 0; i < headers.Length; i++)
            {
                summarySheet.Cell(currentRow, i + 1).Value = headers[i];
                summarySheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                summarySheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                summarySheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                summarySheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            // Group by Sacco
            var groupedBySacco = delinquentDetails
                .GroupBy(d => companyName)
                .Select(g => new
                {
                    SaccoName = g.Key,
                    Defaulters = g.ToList(),
                    UniqueMembers = g.Select(d => d.MemberNo).Distinct().Count(),
                    TotalExpected = g.Sum(d => d.ExpectedRepayment),
                    TotalCollected = g.Sum(d => d.AmountCollected),
                    TotalDefaulted = g.Sum(d => d.DefaultedAmount)
                })
                .ToList();

            decimal grandExpected = 0;
            decimal grandCollected = 0;
            decimal grandDefaulted = 0;
            int grandDefaulters = 0;

            foreach (var sacco in groupedBySacco)
            {
                decimal rate = sacco.TotalExpected > 0 ? (sacco.TotalDefaulted / sacco.TotalExpected) * 100 : 0;

                summarySheet.Cell(currentRow, 1).Value = sacco.SaccoName;
                summarySheet.Cell(currentRow, 2).Value = sacco.UniqueMembers;
                summarySheet.Cell(currentRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                summarySheet.Cell(currentRow, 3).Value = sacco.TotalExpected;
                summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 4).Value = sacco.TotalCollected;
                summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 5).Value = sacco.TotalDefaulted;
                summarySheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                summarySheet.Cell(currentRow, 6).Value = $"{rate:N2}%";

                grandExpected += sacco.TotalExpected;
                grandCollected += sacco.TotalCollected;
                grandDefaulted += sacco.TotalDefaulted;
                grandDefaulters += sacco.UniqueMembers;
                currentRow++;
            }

            // Totals row
            currentRow++;
            decimal overallRate = grandExpected > 0 ? (grandDefaulted / grandExpected) * 100 : 0;

            summarySheet.Cell(currentRow, 1).Value = "TOTALS:";
            summarySheet.Cell(currentRow, 1).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 2).Value = grandDefaulters;
            summarySheet.Cell(currentRow, 2).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 3).Value = grandExpected;
            summarySheet.Cell(currentRow, 3).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 4).Value = grandCollected;
            summarySheet.Cell(currentRow, 4).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 5).Value = grandDefaulted;
            summarySheet.Cell(currentRow, 5).Style.Font.SetBold();
            summarySheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
            summarySheet.Cell(currentRow, 6).Value = $"{overallRate:N2}%";
            summarySheet.Cell(currentRow, 6).Style.Font.SetBold();

            summarySheet.Columns().AdjustToContents();

            // Detailed Worksheet
            var detailSheet = workbook.Worksheets.Add("Defaulters Details");
            currentRow = 1;

            detailSheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            detailSheet.Range(currentRow, 1, currentRow, 11).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            detailSheet.Cell(currentRow, 1).Value = $"DEFAULTING MEMBERS DETAILS - {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";
            detailSheet.Range(currentRow, 1, currentRow, 11).Merge();
            detailSheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            detailSheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string[] detailHeaders = { "MemberNo", "Names", "LoanNo", "Loan Type", "Date Issued", "Loan Amount", "Outstanding Balance", "Expected", "Collected", "Defaulted", "Days Overdue" };
            for (int i = 0; i < detailHeaders.Length; i++)
            {
                detailSheet.Cell(currentRow, i + 1).Value = detailHeaders[i];
                detailSheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                detailSheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                detailSheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                detailSheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var detail in delinquentDetails.OrderBy(d => d.MemberNo))
            {
                detailSheet.Cell(currentRow, 1).Value = detail.MemberNo;
                detailSheet.Cell(currentRow, 2).Value = detail.Names;
                detailSheet.Cell(currentRow, 3).Value = detail.LoanNo;
                detailSheet.Cell(currentRow, 4).Value = detail.LoanType;
                detailSheet.Cell(currentRow, 5).Value = detail.DateIssued.ToString("dd/MM/yyyy");
                detailSheet.Cell(currentRow, 6).Value = detail.LoanAmount;
                detailSheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 7).Value = detail.OutstandingBalance;
                detailSheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 8).Value = detail.ExpectedRepayment;
                detailSheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 9).Value = detail.AmountCollected;
                detailSheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 10).Value = detail.DefaultedAmount;
                detailSheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
                detailSheet.Cell(currentRow, 11).Value = detail.DaysOverdue;
                detailSheet.Cell(currentRow, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                currentRow++;
            }

            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DelinquentLoansReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportDelinquentLoansToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans
            var activeLoans = await (from loan in _context.Loans
                                     join member in _context.Members
                                         on loan.MemberNo equals member.MemberNo
                                     join loantype in _context.Loantypes
                                         on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                                     from lt in loanTypeJoin.DefaultIfEmpty()
                                     where loan.CompanyCode == companyCode
                                         && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                                         && loan.AuditTime <= endDateAdjusted
                                     select new
                                     {
                                         loan.MemberNo,
                                         loan.LoanNo,
                                         loan.AuditTime,
                                         loan.LoanAmt,
                                         loan.Aamount,
                                         MemberSurname = member.Surname,
                                         MemberOtherNames = member.OtherNames,
                                         MemberFullName = member.FullName,
                                         LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                                     }).ToListAsync();

            if (!activeLoans.Any())
            {
                TempData["Error"] = "No active loans found for the selected date range";
                return RedirectToAction("DelinquentLoansReport");
            }

            var loanNos = activeLoans.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => lb.Balance);

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= endDateAdjusted && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalCollected = g.Sum(r => r.Amount ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var delinquentDetails = new List<DelinquentLoanDetail>();
            var currentDate = endDate;

            foreach (var loan in activeLoans)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo] : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal amountCollected = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalCollected : 0;
                decimal expectedRepayment = schedules.ContainsKey(loan.LoanNo) ? schedules[loan.LoanNo].TotalExpected : (loan.Aamount ?? loan.LoanAmt ?? 0);
                DateTime? lastPaymentDate = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].LastPaymentDate : null;

                int daysOverdue = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                if (daysOverdue > 30)
                {
                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    delinquentDetails.Add(new DelinquentLoanDetail
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        LoanType = loan.LoanTypeName,
                        DateIssued = loan.AuditTime,
                        LoanAmount = loan.Aamount ?? loan.LoanAmt ?? 0,
                        OutstandingBalance = currentBalance,
                        ExpectedRepayment = expectedRepayment,
                        AmountCollected = amountCollected,
                        DefaultedAmount = currentBalance,
                        DaysOverdue = daysOverdue,
                        LastPaymentDate = lastPaymentDate ?? loan.AuditTime
                    });
                }
            }

            var groupedBySacco = delinquentDetails
                .GroupBy(d => companyName)
                .Select(g => new
                {
                    SaccoName = g.Key,
                    UniqueMembers = g.Select(d => d.MemberNo).Distinct().Count(),
                    TotalExpected = g.Sum(d => d.ExpectedRepayment),
                    TotalCollected = g.Sum(d => d.AmountCollected),
                    TotalDefaulted = g.Sum(d => d.DefaultedAmount),
                    Defaulters = g.ToList()
                })
                .ToList();

            decimal grandExpected = groupedBySacco.Sum(s => s.TotalExpected);
            decimal grandCollected = groupedBySacco.Sum(s => s.TotalCollected);
            decimal grandDefaulted = groupedBySacco.Sum(s => s.TotalDefaulted);
            int grandDefaulters = groupedBySacco.Sum(s => s.UniqueMembers);
            decimal overallRate = grandExpected > 0 ? (grandDefaulted / grandExpected) * 100 : 0;

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
                        header.Item().AlignCenter().Text($"DELINQUENT LOANS REPORT").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        // Summary Table
                        contentCol.Item().Table(summaryTable =>
                        {
                            summaryTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1.2f);
                                cols.RelativeColumn(0.6f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(1.0f);
                                cols.RelativeColumn(0.6f);
                            });

                            summaryTable.Header(header =>
                            {
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Sacco").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("No of Defaulters").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Expected Amount").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Collected Amount").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Defaulted Amount").Bold().FontSize(8);
                                header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Rate %").Bold().FontSize(8);
                            });

                            foreach (var sacco in groupedBySacco)
                            {
                                decimal rate = sacco.TotalExpected > 0 ? (sacco.TotalDefaulted / sacco.TotalExpected) * 100 : 0;

                                summaryTable.Cell().Border(0.2f).Padding(4).Text(sacco.SaccoName).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(sacco.UniqueMembers.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalExpected:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalCollected:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{sacco.TotalDefaulted:N2}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{rate:N2}%").FontSize(8);
                            }

                            // Totals row
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).Text("TOTALS:").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignCenter().Text(grandDefaulters.ToString()).Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandExpected:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandCollected:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{grandDefaulted:N2}").Bold().FontSize(9);
                            summaryTable.Cell().Border(0.2f).Background("#d0d0d0").Padding(4).AlignRight().Text($"{overallRate:N2}%").Bold().FontSize(9);
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
            return File(content, "application/pdf", $"DelinquentLoansReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region Periodic Loan Defaulters Report

        [HttpGet]
        public async Task<IActionResult> PeriodicLoanDefaulters()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var asAtDate = DateTime.Now;

            // Get available regions from members table
            var regions = await _context.Members
                .Where(m => m.CompanyCode == companyCode && !string.IsNullOrEmpty(m.Province))
                .Select(m => m.Province)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            var viewModel = new PeriodicLoanDefaultersIndexViewModel
            {
                Defaulters = new List<PeriodicLoanDefaulterViewModel>(),
                ReportType = "General",
                AsAtDate = asAtDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                GrandDefaultTotal = 0,
                TotalRepayRate = 0,
                TotalPaid = 0,
                TotalExpected = 0,
                TotalDefaulters = 0,
                Regions = regions
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/PeriodicLoanDefaulters.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> PeriodicLoanDefaulters(DateTime asAtDate, string reportType, string selectedRegion)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust to end of day
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans that are overdue (defaulters)
            var query = from loan in _context.Loans
                        join member in _context.Members
                            on loan.MemberNo equals member.MemberNo
                        join loantype in _context.Loantypes
                            on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                        from lt in loanTypeJoin.DefaultIfEmpty()
                        where loan.CompanyCode == companyCode
                            && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                            && loan.AuditTime <= asAtDateEnd
                        select new
                        {
                            loan.MemberNo,
                            loan.LoanNo,
                            loan.LoanCode,
                            loan.AuditTime,
                            loan.LoanAmt,
                            loan.Aamount,
                            loan.Interest,
                            loan.RepayPeriod,
                            loan.Repayrate,
                            MemberSurname = member.Surname,
                            MemberOtherNames = member.OtherNames,
                            MemberFullName = member.FullName,
                            MemberPhoneNo = member.PhoneNo ?? member.MobileNo,
                            MemberProvince = member.Province,
                            MemberDistrict = member.District,
                            MemberStation = member.Station,
                            LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown"),
                            GracePeriod = lt != null ? lt.GracePeriod : 30
                        };

            // Apply region filter if Region report type is selected
            if (reportType == "Region" && !string.IsNullOrEmpty(selectedRegion))
            {
                query = query.Where(q => q.MemberProvince == selectedRegion);
            }

            var loansWithDetails = await query.ToListAsync();

            if (!loansWithDetails.Any())
            {
                // FIX: Rename this variable to avoid conflict
                var regionList = await _context.Members
                    .Where(m => m.CompanyCode == companyCode && !string.IsNullOrEmpty(m.Province))
                    .Select(m => m.Province)
                    .Distinct()
                    .OrderBy(r => r)
                    .ToListAsync();

                var emptyViewModel = new PeriodicLoanDefaultersIndexViewModel
                {
                    Defaulters = new List<PeriodicLoanDefaulterViewModel>(),
                    ReportType = reportType,
                    SelectedRegion = selectedRegion,
                    AsAtDate = asAtDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    GrandDefaultTotal = 0,
                    TotalRepayRate = 0,
                    TotalPaid = 0,
                    TotalExpected = 0,
                    TotalDefaulters = 0,
                    Regions = regionList  // Use the renamed variable
                };

                ViewBag.AsAtDate = asAtDate;
                ViewBag.CompanyName = companyName;
                ViewBag.HasData = false;
                ViewBag.Message = "No defaulters found as at the selected date.";

                return View("~/Views/Reports/PeriodicLoanDefaulters.cshtml", emptyViewModel);
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            // Get loan balances
            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.IntrOwed, lb.Penalty, lb.RepayRate });

            // Get repayment history
            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPaid = g.Sum(r => r.Amount ?? 0),
                    TotalPrincipalPaid = g.Sum(r => r.Principal ?? 0),
                    TotalInterestPaid = g.Sum(r => r.Interest ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived),
                    PaymentCount = g.Count()
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            // Get expected amounts from schedule
            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment),
                    OverdueAmount = g.Where(s => s.DueDate < DateTime.Now && s.Status != "Paid").Sum(s => s.OutstandingTotal)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var defaultersList = new List<PeriodicLoanDefaulterViewModel>();
            decimal grandDefaultTotal = 0;
            decimal totalRepayRate = 0;
            decimal totalPaid = 0;
            decimal totalExpected = 0;
            var currentDate = asAtDate;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = 0;
                decimal repayRate = 0;
                decimal paidAmount = 0;
                decimal expectedAmount = 0;
                decimal defaultedAmount = 0;
                DateTime? lastPaymentDate = null;
                int daysOverdue = 0;
                string overduePeriod = "";

                // Get balance
                if (loanBalances.ContainsKey(loan.LoanNo))
                {
                    currentBalance = loanBalances[loan.LoanNo].Balance;
                    repayRate = loanBalances[loan.LoanNo].RepayRate;
                }
                else
                {
                    currentBalance = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Skip fully paid loans
                if (currentBalance <= 0) continue;

                // Use loan's repayrate if available
                if (repayRate == 0 && loan.Repayrate.HasValue)
                {
                    repayRate = loan.Repayrate.Value;
                }

                // Get paid amount
                if (repayments.ContainsKey(loan.LoanNo))
                {
                    paidAmount = repayments[loan.LoanNo].TotalPaid;
                    lastPaymentDate = repayments[loan.LoanNo].LastPaymentDate;
                }

                // Get expected amount
                if (schedules.ContainsKey(loan.LoanNo))
                {
                    expectedAmount = schedules[loan.LoanNo].TotalExpected;
                }
                else
                {
                    // Fallback: calculate expected based on loan amount and period
                    expectedAmount = loan.Aamount ?? loan.LoanAmt ?? 0;
                }

                // Calculate days overdue and determine default period
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                // Determine default period (DPeriod)
                if (daysOverdue <= 0)
                {
                    overduePeriod = "Current";
                }
                else if (daysOverdue <= 30)
                {
                    overduePeriod = "1-30 days";
                }
                else if (daysOverdue <= 60)
                {
                    overduePeriod = "31-60 days";
                }
                else if (daysOverdue <= 90)
                {
                    overduePeriod = "61-90 days";
                }
                else if (daysOverdue <= 180)
                {
                    overduePeriod = "91-180 days";
                }
                else if (daysOverdue <= 365)
                {
                    overduePeriod = "181-365 days";
                }
                else
                {
                    overduePeriod = "365+ days";
                }

                // Only include if days overdue > 0 (actual defaulters)
                if (daysOverdue > 0)
                {
                    defaultedAmount = currentBalance;

                    // Build member name
                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    grandDefaultTotal += defaultedAmount;
                    totalRepayRate += repayRate;
                    totalPaid += paidAmount;
                    totalExpected += expectedAmount;

                    defaultersList.Add(new PeriodicLoanDefaulterViewModel
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        RepayRate = repayRate,
                        Paid = paidAmount,
                        Expected = expectedAmount,
                        DefAmount = defaultedAmount,
                        DPeriod = overduePeriod,
                        PhoneNo = loan.MemberPhoneNo ?? "N/A",
                        LoanType = loan.LoanTypeName,
                        DateIssued = loan.AuditTime,
                        LastPaymentDate = lastPaymentDate,
                        DaysOverdue = daysOverdue
                    });
                }
            }

            // Get regions for dropdown - FIX: Use a different variable name
            var availableRegions = await _context.Members
                .Where(m => m.CompanyCode == companyCode && !string.IsNullOrEmpty(m.Province))
                .Select(m => m.Province)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            var viewModel = new PeriodicLoanDefaultersIndexViewModel
            {
                Defaulters = defaultersList.OrderByDescending(d => d.DaysOverdue).ThenBy(d => d.MemberNo).ToList(),
                ReportType = reportType,
                SelectedRegion = selectedRegion,
                GrandDefaultTotal = grandDefaultTotal,
                TotalRepayRate = totalRepayRate,
                TotalPaid = totalPaid,
                TotalExpected = totalExpected,
                TotalDefaulters = defaultersList.Count,
                AsAtDate = asAtDate,
                HasData = defaultersList.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now,
                Regions = availableRegions
            };

            ViewBag.AsAtDate = asAtDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/PeriodicLoanDefaulters.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExportPeriodicLoanDefaultersToExcel(DateTime asAtDate, string reportType, string selectedRegion)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans that are overdue
            var query = from loan in _context.Loans
                        join member in _context.Members
                            on loan.MemberNo equals member.MemberNo
                        join loantype in _context.Loantypes
                            on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                        from lt in loanTypeJoin.DefaultIfEmpty()
                        where loan.CompanyCode == companyCode
                            && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                            && loan.AuditTime <= asAtDateEnd
                        select new
                        {
                            loan.MemberNo,
                            loan.LoanNo,
                            loan.AuditTime,
                            loan.LoanAmt,
                            loan.Aamount,
                            loan.Repayrate,
                            MemberSurname = member.Surname,
                            MemberOtherNames = member.OtherNames,
                            MemberFullName = member.FullName,
                            MemberPhoneNo = member.PhoneNo ?? member.MobileNo,
                            MemberProvince = member.Province,
                            LoanTypeName = lt != null ? lt.LoanType1 : (loan.LoanCode ?? "Unknown")
                        };

            // Apply region filter if Region report type is selected
            if (reportType == "Region" && !string.IsNullOrEmpty(selectedRegion))
            {
                query = query.Where(q => q.MemberProvince == selectedRegion);
            }

            var loansWithDetails = await query.ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No defaulters found for the selected date";
                return RedirectToAction("PeriodicLoanDefaulters");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.RepayRate });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPaid = g.Sum(r => r.Amount ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var defaultersList = new List<PeriodicLoanDefaulterViewModel>();
            decimal grandDefaultTotal = 0;
            var currentDate = asAtDate;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal repayRate = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].RepayRate : (loan.Repayrate ?? 0);
                decimal paidAmount = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalPaid : 0;
                decimal expectedAmount = schedules.ContainsKey(loan.LoanNo) ? schedules[loan.LoanNo].TotalExpected : (loan.Aamount ?? loan.LoanAmt ?? 0);
                DateTime? lastPaymentDate = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].LastPaymentDate : null;

                int daysOverdue = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                if (daysOverdue > 0)
                {
                    string overduePeriod = daysOverdue <= 30 ? "1-30 days" :
                                          daysOverdue <= 60 ? "31-60 days" :
                                          daysOverdue <= 90 ? "61-90 days" :
                                          daysOverdue <= 180 ? "91-180 days" :
                                          daysOverdue <= 365 ? "181-365 days" : "365+ days";

                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    grandDefaultTotal += currentBalance;

                    defaultersList.Add(new PeriodicLoanDefaulterViewModel
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        RepayRate = repayRate,
                        Paid = paidAmount,
                        Expected = expectedAmount,
                        DefAmount = currentBalance,
                        DPeriod = overduePeriod,
                        PhoneNo = loan.MemberPhoneNo ?? "N/A",
                        LoanType = loan.LoanTypeName,
                        DaysOverdue = daysOverdue
                    });
                }
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Periodic Loan Defaulters");
            int currentRow = 1;

            // Header
            worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            string reportTitle = reportType == "Region" ? $"PERIODIC LOAN DEFAULTERS BY REGION - {selectedRegion}" : "PERIODIC LOAN DEFAULTERS - GENERAL";
            worksheet.Cell(currentRow, 1).Value = reportTitle;
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"As At: {asAtDate:dd/MM/yyyy HH:mm:ss}";
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;

            worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            worksheet.Range(currentRow, 1, currentRow, 9).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
            currentRow += 2;

            // Headers
            string[] headers = { "MemberNo", "Names", "LoanNo", "RepayRate", "Paid", "Expected", "DefAmount", "DPeriod", "PhoneNo" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Cell(currentRow, i + 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            currentRow++;

            foreach (var defaulter in defaultersList.OrderBy(d => d.MemberNo))
            {
                worksheet.Cell(currentRow, 1).Value = defaulter.MemberNo;
                worksheet.Cell(currentRow, 2).Value = defaulter.Names;
                worksheet.Cell(currentRow, 3).Value = defaulter.LoanNo;
                worksheet.Cell(currentRow, 4).Value = defaulter.RepayRate;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 5).Value = defaulter.Paid;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 6).Value = defaulter.Expected;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = defaulter.DefAmount;
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 8).Value = defaulter.DPeriod;
                worksheet.Cell(currentRow, 9).Value = defaulter.PhoneNo;

                currentRow++;
            }

            // Grand Total row
            currentRow++;
            worksheet.Cell(currentRow, 6).Value = "Grand Default Total:";
            worksheet.Cell(currentRow, 6).Style.Font.SetBold();
            worksheet.Cell(currentRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            worksheet.Cell(currentRow, 7).Value = grandDefaultTotal;
            worksheet.Cell(currentRow, 7).Style.Font.SetBold();
            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PeriodicLoanDefaulters_{reportType}_{asAtDate:yyyyMMdd}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ExportPeriodicLoanDefaultersToPdf(DateTime asAtDate, string reportType, string selectedRegion)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var asAtDateEnd = asAtDate.Date.AddDays(1).AddSeconds(-1);

            // Get all active/disbursed loans that are overdue
            var query = from loan in _context.Loans
                        join member in _context.Members
                            on loan.MemberNo equals member.MemberNo
                        join loantype in _context.Loantypes
                            on loan.LoanCode equals loantype.LoanCode into loanTypeJoin
                        from lt in loanTypeJoin.DefaultIfEmpty()
                        where loan.CompanyCode == companyCode
                            && (loan.Status == (int)Status.Disbursed || loan.Status == (int)Status.Endorsed)
                            && loan.AuditTime <= asAtDateEnd
                        select new
                        {
                            loan.MemberNo,
                            loan.LoanNo,
                            loan.AuditTime,
                            loan.LoanAmt,
                            loan.Aamount,
                            loan.Repayrate,
                            MemberSurname = member.Surname,
                            MemberOtherNames = member.OtherNames,
                            MemberFullName = member.FullName,
                            MemberPhoneNo = member.PhoneNo ?? member.MobileNo,
                            MemberProvince = member.Province
                        };

            if (reportType == "Region" && !string.IsNullOrEmpty(selectedRegion))
            {
                query = query.Where(q => q.MemberProvince == selectedRegion);
            }

            var loansWithDetails = await query.ToListAsync();

            if (!loansWithDetails.Any())
            {
                TempData["Error"] = "No defaulters found for the selected date";
                return RedirectToAction("PeriodicLoanDefaulters");
            }

            var loanNos = loansWithDetails.Select(l => l.LoanNo).ToList();

            var loanBalances = await _context.Loanbal
                .Where(lb => loanNos.Contains(lb.LoanNo) && lb.Companycode == companyCode)
                .ToDictionaryAsync(lb => lb.LoanNo, lb => new { lb.Balance, lb.RepayRate });

            var repayments = await _context.Repay
                .Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode && r.DateReceived <= asAtDateEnd && r.Posted == true)
                .GroupBy(r => r.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalPaid = g.Sum(r => r.Amount ?? 0),
                    LastPaymentDate = g.Max(r => r.DateReceived)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var schedules = await _context.LoanSchedules
                .Where(s => loanNos.Contains(s.LoanNo))
                .GroupBy(s => s.LoanNo)
                .Select(g => new
                {
                    LoanNo = g.Key,
                    TotalExpected = g.Sum(s => s.TotalInstallment)
                })
                .ToDictionaryAsync(g => g.LoanNo, g => g);

            var defaultersList = new List<PeriodicLoanDefaulterViewModel>();
            decimal grandDefaultTotal = 0;
            var currentDate = asAtDate;

            foreach (var loan in loansWithDetails)
            {
                decimal currentBalance = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].Balance : (loan.Aamount ?? loan.LoanAmt ?? 0);
                if (currentBalance <= 0) continue;

                decimal repayRate = loanBalances.ContainsKey(loan.LoanNo) ? loanBalances[loan.LoanNo].RepayRate : (loan.Repayrate ?? 0);
                decimal paidAmount = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].TotalPaid : 0;
                decimal expectedAmount = schedules.ContainsKey(loan.LoanNo) ? schedules[loan.LoanNo].TotalExpected : (loan.Aamount ?? loan.LoanAmt ?? 0);
                DateTime? lastPaymentDate = repayments.ContainsKey(loan.LoanNo) ? repayments[loan.LoanNo].LastPaymentDate : null;

                int daysOverdue = 0;
                if (lastPaymentDate.HasValue)
                {
                    DateTime nextDueDate = lastPaymentDate.Value.AddMonths(1);
                    if (currentDate > nextDueDate)
                    {
                        daysOverdue = (currentDate - nextDueDate).Days;
                    }
                }
                else
                {
                    DateTime firstDueDate = loan.AuditTime.AddMonths(1);
                    if (currentDate > firstDueDate)
                    {
                        daysOverdue = (currentDate - firstDueDate).Days;
                    }
                }

                if (daysOverdue > 0)
                {
                    string overduePeriod = daysOverdue <= 30 ? "1-30 days" :
                                          daysOverdue <= 60 ? "31-60 days" :
                                          daysOverdue <= 90 ? "61-90 days" :
                                          daysOverdue <= 180 ? "91-180 days" :
                                          daysOverdue <= 365 ? "181-365 days" : "365+ days";

                    string fullName = !string.IsNullOrEmpty(loan.MemberFullName)
                        ? loan.MemberFullName
                        : $"{loan.MemberSurname ?? ""} {loan.MemberOtherNames ?? ""}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = loan.MemberNo;

                    grandDefaultTotal += currentBalance;

                    defaultersList.Add(new PeriodicLoanDefaulterViewModel
                    {
                        MemberNo = loan.MemberNo,
                        Names = fullName,
                        LoanNo = loan.LoanNo,
                        RepayRate = repayRate,
                        Paid = paidAmount,
                        Expected = expectedAmount,
                        DefAmount = currentBalance,
                        DPeriod = overduePeriod,
                        PhoneNo = loan.MemberPhoneNo ?? "N/A",
                        DaysOverdue = daysOverdue
                    });
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

                        string title = reportType == "Region" ? $"PERIODIC LOAN DEFAULTERS BY REGION - {selectedRegion}" : "PERIODIC LOAN DEFAULTERS - GENERAL";
                        header.Item().AlignCenter().Text(title).FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"As At: {asAtDate:dd/MM/yyyy HH:mm:ss}").FontSize(10);
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.0f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("MemberNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Names").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("LoanNo").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("RepayRate").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Paid").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Expected").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("DefAmount").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("DPeriod").Bold().FontSize(8);
                            header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("PhoneNo").Bold().FontSize(8);
                        });

                        foreach (var defaulter in defaultersList.OrderBy(d => d.MemberNo))
                        {
                            table.Cell().Border(0.2f).Padding(4).Text(defaulter.MemberNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(defaulter.Names ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(defaulter.LoanNo ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{defaulter.RepayRate:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{defaulter.Paid:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{defaulter.Expected:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{defaulter.DefAmount:N2}").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(defaulter.DPeriod ?? "").FontSize(7);
                            table.Cell().Border(0.2f).Padding(4).Text(defaulter.PhoneNo ?? "").FontSize(7);
                        }

                        // Grand Total row
                        table.Cell().ColumnSpan(6).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("Grand Default Total:").Bold().FontSize(8);
                        table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{grandDefaultTotal:N2}").Bold().FontSize(8);
                        table.Cell().ColumnSpan(2).Border(0.2f).Background("#f9f9f9").Padding(4);
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
            return File(content, "application/pdf", $"PeriodicLoanDefaulters_{reportType}_{asAtDate:yyyyMMdd}.pdf");
        }

        #endregion

        #region General Ledgers Report

        [HttpGet]
        public IActionResult GeneralLedgerReport()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var startDate = DateTime.Now.AddMonths(-1);
            var endDate = DateTime.Now;

            var viewModel = new GeneralLedgerIndexViewModel
            {
                Groups = new List<GeneralLedgerGroupViewModel>(),
                StartDate = startDate,
                EndDate = endDate,
                HasData = false,
                CompanyName = companyName,
                PrintedBy = User.Identity?.Name ?? "System",
                GeneratedOn = DateTime.Now,
                TotalDebits = 0,
                TotalCredits = 0,
                TotalTransactions = 0,
                TotalAccounts = 0
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.CompanyName = companyName;
            ViewBag.HasData = false;

            return View("~/Views/Reports/GeneralLedgerReport.cshtml", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> GeneralLedgerReport(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";

            // Adjust end date to include the entire day
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get GL transactions within the date range
            var glTransactions = await _context.Gltransactions
                .Where(gt => gt.CompanyCode == companyCode
                    && gt.TransDate >= startDate
                    && gt.TransDate <= endDateAdjusted
                    && gt.DocPosted == 1)
                .OrderBy(gt => gt.TransDate)
                .ThenBy(gt => gt.Id)
                .Select(gt => new
                {
                    gt.TransDate,
                    gt.Source,
                    gt.TransDescript,
                    gt.ChequeNo,
                    gt.Amount,
                    gt.DrAccNo,
                    gt.CrAccNo,
                    gt.DocumentNo,
                    gt.TransactionNo
                })
                .ToListAsync();

            if (!glTransactions.Any())
            {
                var emptyViewModel = new GeneralLedgerIndexViewModel
                {
                    Groups = new List<GeneralLedgerGroupViewModel>(),
                    StartDate = startDate,
                    EndDate = endDate,
                    HasData = false,
                    CompanyName = companyName,
                    PrintedBy = printedBy,
                    GeneratedOn = DateTime.Now,
                    TotalDebits = 0,
                    TotalCredits = 0,
                    TotalTransactions = 0,
                    TotalAccounts = 0
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.HasData = false;
                ViewBag.Message = "No GL transactions found for the selected date range.";

                return View("~/Views/Reports/GeneralLedgerReport.cshtml", emptyViewModel);
            }

            // Get GL accounts for account names
            var glAccounts = await _context.GlSetup
                .Where(gs => gs.CompanyCode == companyCode)
                .ToDictionaryAsync(gs => gs.AccNo, gs => gs.Glaccname ?? gs.AccNo);

            // Group transactions by account (DrAccNo and CrAccNo)
            var transactionsByAccount = new Dictionary<string, List<GeneralLedgerReportViewModel>>();

            foreach (var trans in glTransactions)
            {
                // Process Debit account
                if (!string.IsNullOrEmpty(trans.DrAccNo) && trans.DrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.DrAccNo))
                    {
                        transactionsByAccount[trans.DrAccNo] = new List<GeneralLedgerReportViewModel>();
                    }

                    // Get opening balance for this account before start date
                    var openingBalance = await GetAccountOpeningBalanceAsync(trans.DrAccNo, startDate, companyCode);
                    var runningBalance = openingBalance;

                    // For debit entries, add to balance
                    runningBalance += trans.Amount;

                    transactionsByAccount[trans.DrAccNo].Add(new GeneralLedgerReportViewModel
                    {
                        TransDate = trans.TransDate,
                        Source = trans.Source ?? "-",
                        Description = trans.TransDescript ?? $"DR - {trans.DrAccNo}",
                        ChequeNo = trans.ChequeNo ?? "-",
                        Debits = trans.Amount,
                        Credits = 0,
                        AccBal = runningBalance,
                        DocumentNo = trans.DocumentNo,
                        TransactionNo = trans.TransactionNo,
                        AccountNo = trans.DrAccNo,
                        AccountName = glAccounts.ContainsKey(trans.DrAccNo) ? glAccounts[trans.DrAccNo] : trans.DrAccNo
                    });
                }

                // Process Credit account
                if (!string.IsNullOrEmpty(trans.CrAccNo) && trans.CrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.CrAccNo))
                    {
                        transactionsByAccount[trans.CrAccNo] = new List<GeneralLedgerReportViewModel>();
                    }

                    // Get opening balance for this account before start date
                    var openingBalance = await GetAccountOpeningBalanceAsync(trans.CrAccNo, startDate, companyCode);
                    var runningBalance = openingBalance;

                    // For credit entries, subtract from balance
                    runningBalance -= trans.Amount;

                    transactionsByAccount[trans.CrAccNo].Add(new GeneralLedgerReportViewModel
                    {
                        TransDate = trans.TransDate,
                        Source = trans.Source ?? "-",
                        Description = trans.TransDescript ?? $"CR - {trans.CrAccNo}",
                        ChequeNo = trans.ChequeNo ?? "-",
                        Debits = 0,
                        Credits = trans.Amount,
                        AccBal = runningBalance,
                        DocumentNo = trans.DocumentNo,
                        TransactionNo = trans.TransactionNo,
                        AccountNo = trans.CrAccNo,
                        AccountName = glAccounts.ContainsKey(trans.CrAccNo) ? glAccounts[trans.CrAccNo] : trans.CrAccNo
                    });
                }
            }

            // Build grouped data with running balances
            var groups = new List<GeneralLedgerGroupViewModel>();
            decimal totalDebits = 0;
            decimal totalCredits = 0;
            int totalTransactions = 0;

            foreach (var account in transactionsByAccount.OrderBy(a => a.Key))
            {
                var transactions = account.Value.OrderBy(t => t.TransDate).ToList();

                // Calculate running balance for this account
                decimal runningBalance = await GetAccountOpeningBalanceAsync(account.Key, startDate, companyCode);
                decimal groupDebits = 0;
                decimal groupCredits = 0;

                foreach (var trans in transactions)
                {
                    if (trans.Debits > 0)
                    {
                        runningBalance += trans.Debits;
                        groupDebits += trans.Debits;
                    }
                    if (trans.Credits > 0)
                    {
                        runningBalance -= trans.Credits;
                        groupCredits += trans.Credits;
                    }
                    trans.AccBal = runningBalance;
                }

                totalDebits += groupDebits;
                totalCredits += groupCredits;
                totalTransactions += transactions.Count;

                groups.Add(new GeneralLedgerGroupViewModel
                {
                    AccountNo = account.Key,
                    AccountName = glAccounts.ContainsKey(account.Key) ? glAccounts[account.Key] : account.Key,
                    Transactions = transactions,
                    TotalDebits = groupDebits,
                    TotalCredits = groupCredits,
                    ClosingBalance = runningBalance,
                    TransactionCount = transactions.Count
                });
            }

            var viewModel = new GeneralLedgerIndexViewModel
            {
                Groups = groups,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                TotalTransactions = totalTransactions,
                TotalAccounts = groups.Count,
                StartDate = startDate,
                EndDate = endDate,
                HasData = groups.Any(),
                CompanyName = companyName,
                PrintedBy = printedBy,
                GeneratedOn = DateTime.Now
            };

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.HasData = viewModel.HasData;

            return View("~/Views/Reports/GeneralLedgerReport.cshtml", viewModel);
        }

        private async Task<decimal> GetAccountOpeningBalanceAsync(string accountNo, DateTime startDate, string companyCode)
        {
            var transactionsBeforeStart = await _context.Gltransactions
                .Where(gt => gt.CompanyCode == companyCode
                    && gt.TransDate < startDate
                    && gt.DocPosted == 1
                    && (gt.DrAccNo == accountNo || gt.CrAccNo == accountNo))
                .ToListAsync();

            decimal balance = 0;
            foreach (var trans in transactionsBeforeStart)
            {
                if (trans.DrAccNo == accountNo)
                    balance += trans.Amount;
                if (trans.CrAccNo == accountNo)
                    balance -= trans.Amount;
            }
            return balance;
        }

        [HttpPost]
        public async Task<IActionResult> ExportGeneralLedgerToExcel(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            // Get GL transactions
            var glTransactions = await _context.Gltransactions
                .Where(gt => gt.CompanyCode == companyCode
                    && gt.TransDate >= startDate
                    && gt.TransDate <= endDateAdjusted
                    && gt.DocPosted == 1)
                .OrderBy(gt => gt.TransDate)
                .Select(gt => new
                {
                    gt.TransDate,
                    gt.Source,
                    gt.TransDescript,
                    gt.ChequeNo,
                    gt.Amount,
                    gt.DrAccNo,
                    gt.CrAccNo
                })
                .ToListAsync();

            if (!glTransactions.Any())
            {
                TempData["Error"] = "No GL transactions found for the selected date range";
                return RedirectToAction("GeneralLedgerReport");
            }

            // Get GL accounts
            var glAccounts = await _context.GlSetup
                .Where(gs => gs.CompanyCode == companyCode)
                .ToDictionaryAsync(gs => gs.AccNo, gs => gs.Glaccname ?? gs.AccNo);

            // Group by account
            var transactionsByAccount = new Dictionary<string, List<dynamic>>();

            foreach (var trans in glTransactions)
            {
                if (!string.IsNullOrEmpty(trans.DrAccNo) && trans.DrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.DrAccNo))
                        transactionsByAccount[trans.DrAccNo] = new List<dynamic>();

                    transactionsByAccount[trans.DrAccNo].Add(new
                    {
                        trans.TransDate,
                        trans.Source,
                        trans.TransDescript,
                        trans.ChequeNo,
                        Debits = trans.Amount,
                        Credits = 0m,
                        AccountNo = trans.DrAccNo
                    });
                }

                if (!string.IsNullOrEmpty(trans.CrAccNo) && trans.CrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.CrAccNo))
                        transactionsByAccount[trans.CrAccNo] = new List<dynamic>();

                    transactionsByAccount[trans.CrAccNo].Add(new
                    {
                        trans.TransDate,
                        trans.Source,
                        trans.TransDescript,
                        trans.ChequeNo,
                        Debits = 0m,
                        Credits = trans.Amount,
                        AccountNo = trans.CrAccNo
                    });
                }
            }

            using var workbook = new XLWorkbook();

            foreach (var account in transactionsByAccount.OrderBy(a => a.Key))
            {
                var accountName = glAccounts.ContainsKey(account.Key) ? glAccounts[account.Key] : account.Key;
                var sheetName = account.Key.Length > 31 ? account.Key.Substring(0, 31) : account.Key;
                sheetName = new string(sheetName.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
                if (string.IsNullOrWhiteSpace(sheetName)) sheetName = "Ledger";

                var worksheet = workbook.Worksheets.Add(sheetName);
                int currentRow = 1;

                // Header
                worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"GENERAL LEDGER - {accountName} ({account.Key})";
                worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(12);
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                currentRow += 2;

                worksheet.Cell(currentRow, 1).Value = $"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}";
                worksheet.Range(currentRow, 1, currentRow, 7).Merge();
                worksheet.Cell(currentRow, 1).Style.Font.SetItalic();
                currentRow += 2;

                // Headers
                string[] headers = { "Transdate", "Source", "Description", "Chequeno", "Debits", "Credits", "AccBal" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(currentRow, i + 1).Value = headers[i];
                    worksheet.Cell(currentRow, i + 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                    worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;

                // Calculate opening balance
                decimal openingBalance = await GetAccountOpeningBalanceAsync(account.Key, startDate, companyCode);
                decimal runningBalance = openingBalance;
                decimal totalDebits = 0;
                decimal totalCredits = 0;

                // Opening balance row
                worksheet.Cell(currentRow, 1).Value = startDate.AddDays(-1).ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 2).Value = "Opening Balance";
                worksheet.Cell(currentRow, 3).Value = "Balance B/F";
                worksheet.Cell(currentRow, 4).Value = "-";
                worksheet.Cell(currentRow, 5).Value = openingBalance > 0 ? openingBalance : 0;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 6).Value = openingBalance < 0 ? Math.Abs(openingBalance) : 0;
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = openingBalance;
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                currentRow++;

                var sortedTransactions = account.Value.OrderBy(t => t.TransDate).ToList();

                foreach (var trans in sortedTransactions)
                {
                    if (trans.Debits > 0)
                    {
                        runningBalance += trans.Debits;
                        totalDebits += trans.Debits;
                    }
                    if (trans.Credits > 0)
                    {
                        runningBalance -= trans.Credits;
                        totalCredits += trans.Credits;
                    }

                    string description = trans.TransDescript ?? (trans.Debits > 0 ? $"DR - {account.Key}" : $"CR - {account.Key}");

                    worksheet.Cell(currentRow, 1).Value = trans.TransDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(currentRow, 2).Value = trans.Source ?? "-";
                    worksheet.Cell(currentRow, 3).Value = description;
                    worksheet.Cell(currentRow, 4).Value = trans.ChequeNo ?? "-";
                    worksheet.Cell(currentRow, 5).Value = trans.Debits;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 6).Value = trans.Credits;
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 7).Value = runningBalance;
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

                    currentRow++;
                }

                // Totals row
                currentRow++;
                worksheet.Cell(currentRow, 4).Value = "TOTALS:";
                worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                worksheet.Cell(currentRow, 5).Value = totalDebits;
                worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 6).Value = totalCredits;
                worksheet.Cell(currentRow, 6).Style.Font.SetBold();
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(currentRow, 7).Value = runningBalance;
                worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

                worksheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"GeneralLedger_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
        }
        [HttpPost]
        public async Task<IActionResult> ExportGeneralLedgerToPdf(DateTime startDate, DateTime endDate)
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";
            var printedBy = User.Identity?.Name ?? "System";
            var endDateAdjusted = endDate.Date.AddDays(1).AddSeconds(-1);

            var glTransactions = await _context.Gltransactions
                .Where(gt => gt.CompanyCode == companyCode
                    && gt.TransDate >= startDate
                    && gt.TransDate <= endDateAdjusted
                    && gt.DocPosted == 1)
                .OrderBy(gt => gt.TransDate)
                .Select(gt => new
                {
                    gt.TransDate,
                    gt.Source,
                    gt.TransDescript,
                    gt.ChequeNo,
                    gt.Amount,
                    gt.DrAccNo,
                    gt.CrAccNo
                })
                .ToListAsync();

            if (!glTransactions.Any())
            {
                TempData["Error"] = "No GL transactions found for the selected date range";
                return RedirectToAction("GeneralLedgerReport");
            }

            var glAccounts = await _context.GlSetup
                .Where(gs => gs.CompanyCode == companyCode)
                .ToDictionaryAsync(gs => gs.AccNo, gs => gs.Glaccname ?? gs.AccNo);

            // Create strongly typed list instead of dynamic
            var transactionsByAccount = new Dictionary<string, List<TransactionEntry>>();

            foreach (var trans in glTransactions)
            {
                if (!string.IsNullOrEmpty(trans.DrAccNo) && trans.DrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.DrAccNo))
                        transactionsByAccount[trans.DrAccNo] = new List<TransactionEntry>();

                    transactionsByAccount[trans.DrAccNo].Add(new TransactionEntry
                    {
                        TransDate = trans.TransDate,
                        Source = trans.Source ?? "-",
                        TransDescript = trans.TransDescript,
                        ChequeNo = trans.ChequeNo ?? "-",
                        Debits = trans.Amount,
                        Credits = 0m,
                        AccountNo = trans.DrAccNo
                    });
                }

                if (!string.IsNullOrEmpty(trans.CrAccNo) && trans.CrAccNo != "0")
                {
                    if (!transactionsByAccount.ContainsKey(trans.CrAccNo))
                        transactionsByAccount[trans.CrAccNo] = new List<TransactionEntry>();

                    transactionsByAccount[trans.CrAccNo].Add(new TransactionEntry
                    {
                        TransDate = trans.TransDate,
                        Source = trans.Source ?? "-",
                        TransDescript = trans.TransDescript,
                        ChequeNo = trans.ChequeNo ?? "-",
                        Debits = 0m,
                        Credits = trans.Amount,
                        AccountNo = trans.CrAccNo
                    });
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
                        header.Item().AlignCenter().Text($"GENERAL LEDGER").FontSize(12).Bold();
                        header.Item().AlignCenter().Text($"Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}").FontSize(10);
                        header.Item().AlignCenter().Text($"Printed By: {printedBy} On: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(9).Italic();
                        header.Item().PaddingTop(0.3f, Unit.Centimetre).LineHorizontal(0.5f);
                        header.Item().PaddingBottom(0.5f, Unit.Centimetre);
                    });

                    page.Content().Column(contentCol =>
                    {
                        foreach (var account in transactionsByAccount.OrderBy(a => a.Key))
                        {
                            var accountName = glAccounts.ContainsKey(account.Key) ? glAccounts[account.Key] : account.Key;
                            decimal openingBalance = GetAccountOpeningBalanceAsync(account.Key, startDate, companyCode).Result;
                            decimal runningBalance = openingBalance;
                            decimal totalDebits = 0;
                            decimal totalCredits = 0;

                            contentCol.Item().PaddingTop(1, Unit.Centimetre);
                            contentCol.Item().Text($"Account: {accountName} ({account.Key})").FontSize(11).Bold();

                            contentCol.Item().Table(table =>
                            {
                                // Adjusted column widths: increased Source and ChequeNo, reduced Debits, Credits, AccBal
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.7f);  // Transdate
                                    cols.RelativeColumn(1.2f);  // Source (increased)
                                    cols.RelativeColumn(2.2f);  // Description
                                    cols.RelativeColumn(1.0f);  // Chequeno (increased)
                                    cols.RelativeColumn(0.8f);  // Debits (reduced)
                                    cols.RelativeColumn(0.8f);  // Credits (reduced)
                                    cols.RelativeColumn(0.9f);  // AccBal (reduced)
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Transdate").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Source").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Description").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Chequeno").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Debits").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Credits").Bold().FontSize(8);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("AccBal").Bold().FontSize(8);
                                });

                                // Opening balance row
                                table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(startDate.AddDays(-1).ToString("dd/MM/yyyy")).FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text("Opening Balance").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text("Balance B/F").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).Text("-").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{openingBalance:N2}").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{(openingBalance < 0 ? Math.Abs(openingBalance) : 0):N2}").FontSize(8);
                                table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{openingBalance:N2}").FontSize(8);

                                var sortedTransactions = account.Value.OrderBy(t => t.TransDate).ToList();

                                foreach (var trans in sortedTransactions)
                                {
                                    if (trans.Debits > 0)
                                    {
                                        runningBalance += trans.Debits;
                                        totalDebits += trans.Debits;
                                    }
                                    if (trans.Credits > 0)
                                    {
                                        runningBalance -= trans.Credits;
                                        totalCredits += trans.Credits;
                                    }

                                    string description = trans.TransDescript ?? (trans.Debits > 0 ? $"DR - {account.Key}" : $"CR - {account.Key}");

                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(trans.TransDate.ToString("dd/MM/yyyy")).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(trans.Source).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(description).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).Text(trans.ChequeNo).FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{trans.Debits:N2}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{trans.Credits:N2}").FontSize(8);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{runningBalance:N2}").FontSize(8);
                                }

                                // Totals row
                                table.Cell().ColumnSpan(4).Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text("TOTALS:").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalDebits:N2}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{totalCredits:N2}").Bold().FontSize(8);
                                table.Cell().Border(0.2f).Background("#f9f9f9").Padding(4).AlignRight().Text($"{runningBalance:N2}").Bold().FontSize(8);
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

            var content = stream.ToArray();
            return File(content, "application/pdf", $"GeneralLedger_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
        }

        // Helper class for strongly typed transactions
        public class TransactionEntry
        {
            public DateTime TransDate { get; set; }
            public string Source { get; set; }
            public string TransDescript { get; set; }
            public string ChequeNo { get; set; }
            public decimal Debits { get; set; }
            public decimal Credits { get; set; }
            public string AccountNo { get; set; }
        }
        #endregion

    }
}