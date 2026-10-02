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
    public class SupplierPaymentReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SupplierPaymentReportController> _logger;

        public SupplierPaymentReportController(ApplicationDbContext context, ILogger<SupplierPaymentReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Supplier Payment Summary Report

        /// <summary>
        /// GET: Supplier Payment Summary Report
        /// </summary>
        [HttpGet]
        public IActionResult SupplierPaymentSummary()
        {
            var companyCode = User.FindFirstValue("CompanyCode");
            var companyName = User.FindFirstValue("CompanyName") ?? "";

            var viewModel = new SupplierPaymentSummaryViewModel
            {
                SupplierReports = new List<SupplierPaymentSummaryDto>(),
                ReportDate = DateTime.Now,
                HasData = false,
                UserCompanyCode = companyCode,
                CompanyName = companyName,
                TotalSuppliers = 0,
                TotalInvoices = 0,
                TotalInvoiceAmount = 0,
                TotalPaidAmount = 0,
                TotalOutstanding = 0,
                PaidSuppliers = 0,
                PartiallyPaidSuppliers = 0,
                UnpaidSuppliers = 0
            };

            ViewBag.ReportDate = DateTime.Now;
            ViewBag.HasData = false;
            ViewBag.CompanyName = companyName;

            return View("~/Views/Reports/SupplierPaymentSummary.cshtml", viewModel);
        }

        /// <summary>
        /// POST: Generate Supplier Payment Summary Report
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SupplierPaymentSummary(DateTime? startDate, DateTime? endDate, string paymentStatus = "all")
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";

                // Get all suppliers
                var suppliers = await _context.Suppliers
                    .Where(s => s.CompanyCode == companyCode)
                    .OrderBy(s => s.SupplierName)
                    .ToListAsync();

                var supplierCodes = suppliers.Select(s => s.SupplierCode).ToList();

                // Build invoice query with date filters
                var invoiceQuery = _context.InvoiceReceive
                    .Where(i => i.CompanyCode == companyCode && supplierCodes.Contains(i.SupplierCode));

                if (startDate.HasValue)
                {
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    var endDateFilter = endDate.Value.Date.AddDays(1);
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= endDateFilter);
                }

                // Get all invoices with payments
                var invoices = await invoiceQuery
                    .Include(i => i.Payments)
                    .ToListAsync();

                // Group by supplier
                var supplierData = suppliers.Select(supplier =>
                {
                    var supplierInvoices = invoices.Where(i => i.SupplierCode == supplier.SupplierCode).ToList();

                    var totalInvoiceAmount = supplierInvoices.Sum(i => i.InvoiceAmount);
                    var totalPaid = supplierInvoices.Sum(i => i.AmountPaid ?? 0);
                    var totalOutstanding = supplierInvoices.Sum(i => i.Balance ?? 0);
                    var invoiceCount = supplierInvoices.Count;

                    // Determine payment status for this supplier
                    string status;
                    if (totalOutstanding == 0 && totalPaid > 0)
                        status = "FULLY PAID";
                    else if (totalPaid > 0 && totalOutstanding > 0)
                        status = "PARTIALLY PAID";
                    else if (totalPaid == 0 && totalOutstanding > 0)
                        status = "UNPAID";
                    else
                        status = "NO INVOICES";

                    // Get last payment date
                    var lastPaymentDate = supplierInvoices
                        .SelectMany(i => i.Payments ?? new List<InvoicePayment>())
                        .OrderByDescending(p => p.TransDate)
                        .FirstOrDefault()?.TransDate;

                    return new SupplierPaymentSummaryDto
                    {
                        SupplierCode = supplier.SupplierCode,
                        SupplierName = supplier.SupplierName,
                        ContactPerson = supplier.ContactPerson,
                        PhoneNo = supplier.PhoneNo,
                        Email = supplier.Email,
                        InvoiceCount = invoiceCount,
                        TotalInvoiceAmount = totalInvoiceAmount,
                        TotalPaid = totalPaid,
                        TotalOutstanding = totalOutstanding,
                        PaymentStatus = status,
                        LastPaymentDate = lastPaymentDate,
                        GlAccountNo = supplier.GlAccountNo,
                        GlAccountName = supplier.GlAccountName
                    };
                })
                .Where(s => s.InvoiceCount > 0) // Only suppliers with invoices
                .ToList();

                // Apply payment status filter
                if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "all")
                {
                    supplierData = supplierData.Where(s => s.PaymentStatus == paymentStatus.ToUpper()).ToList();
                }

                // Order by supplier name
                supplierData = supplierData.OrderBy(s => s.SupplierName).ToList();

                // Calculate totals
                var viewModel = new SupplierPaymentSummaryViewModel
                {
                    SupplierReports = supplierData,
                    TotalSuppliers = supplierData.Count,
                    TotalInvoices = supplierData.Sum(s => s.InvoiceCount),
                    TotalInvoiceAmount = supplierData.Sum(s => s.TotalInvoiceAmount),
                    TotalPaidAmount = supplierData.Sum(s => s.TotalPaid),
                    TotalOutstanding = supplierData.Sum(s => s.TotalOutstanding),
                    PaidSuppliers = supplierData.Count(s => s.PaymentStatus == "FULLY PAID"),
                    PartiallyPaidSuppliers = supplierData.Count(s => s.PaymentStatus == "PARTIALLY PAID"),
                    UnpaidSuppliers = supplierData.Count(s => s.PaymentStatus == "UNPAID"),
                    StartDate = startDate,
                    EndDate = endDate,
                    PaymentStatusFilter = paymentStatus,
                    ReportDate = DateTime.Now,
                    HasData = supplierData.Any(),
                    UserCompanyCode = companyCode,
                    CompanyName = companyName
                };

                ViewBag.ReportDate = DateTime.Now;
                ViewBag.HasData = supplierData.Any();
                ViewBag.CompanyName = companyName;
                ViewBag.TotalSuppliers = supplierData.Count;
                ViewBag.TotalInvoices = supplierData.Sum(s => s.InvoiceCount);
                ViewBag.TotalInvoiceAmount = supplierData.Sum(s => s.TotalInvoiceAmount);
                ViewBag.TotalPaidAmount = supplierData.Sum(s => s.TotalPaid);
                ViewBag.TotalOutstanding = supplierData.Sum(s => s.TotalOutstanding);

                return View("~/Views/Reports/SupplierPaymentSummary.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating supplier payment summary");
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;
                return RedirectToAction("SupplierPaymentSummary");
            }
        }

        #endregion

        #region Export to Excel

        [HttpPost]
        public async Task<IActionResult> ExportSupplierPaymentSummaryToExcel(DateTime? startDate, DateTime? endDate, string paymentStatus = "all")
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";

                // Get all suppliers
                var suppliers = await _context.Suppliers
                    .Where(s => s.CompanyCode == companyCode)
                    .OrderBy(s => s.SupplierName)
                    .ToListAsync();

                var supplierCodes = suppliers.Select(s => s.SupplierCode).ToList();

                // Build invoice query with date filters
                var invoiceQuery = _context.InvoiceReceive
                    .Where(i => i.CompanyCode == companyCode && supplierCodes.Contains(i.SupplierCode));

                if (startDate.HasValue)
                {
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    var endDateFilter = endDate.Value.Date.AddDays(1);
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= endDateFilter);
                }

                var invoices = await invoiceQuery
                    .Include(i => i.Payments)
                    .ToListAsync();

                // Build report data
                var reportData = suppliers
                    .Select(supplier =>
                    {
                        var supplierInvoices = invoices.Where(i => i.SupplierCode == supplier.SupplierCode).ToList();

                        var totalInvoiceAmount = supplierInvoices.Sum(i => i.InvoiceAmount);
                        var totalPaid = supplierInvoices.Sum(i => i.AmountPaid ?? 0);
                        var totalOutstanding = supplierInvoices.Sum(i => i.Balance ?? 0);
                        var invoiceCount = supplierInvoices.Count;

                        string status;
                        if (totalOutstanding == 0 && totalPaid > 0)
                            status = "FULLY PAID";
                        else if (totalPaid > 0 && totalOutstanding > 0)
                            status = "PARTIALLY PAID";
                        else if (totalPaid == 0 && totalOutstanding > 0)
                            status = "UNPAID";
                        else
                            status = "NO INVOICES";

                        var lastPaymentDate = supplierInvoices
                            .SelectMany(i => i.Payments ?? new List<InvoicePayment>())
                            .OrderByDescending(p => p.TransDate)
                            .FirstOrDefault()?.TransDate;

                        return new
                        {
                            supplier.SupplierCode,
                            supplier.SupplierName,
                            supplier.ContactPerson,
                            supplier.PhoneNo,
                            supplier.Email,
                            InvoiceCount = invoiceCount,
                            TotalInvoiceAmount = totalInvoiceAmount,
                            TotalPaid = totalPaid,
                            TotalOutstanding = totalOutstanding,
                            PaymentStatus = status,
                            LastPaymentDate = lastPaymentDate,
                            supplier.GlAccountNo,
                            supplier.GlAccountName
                        };
                    })
                    .Where(s => s.InvoiceCount > 0)
                    .ToList();

                // Apply payment status filter
                if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "all")
                {
                    reportData = reportData.Where(s => s.PaymentStatus == paymentStatus.ToUpper()).ToList();
                }

                reportData = reportData.OrderBy(s => s.SupplierName).ToList();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Supplier Payment Summary");
                    int currentRow = 1;

                    // Header
                    worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
                    worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(18);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Report Title
                    string dateRange = "ALL TIME";
                    if (startDate.HasValue && endDate.HasValue)
                        dateRange = $"{startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
                    else if (startDate.HasValue)
                        dateRange = $"From {startDate.Value:dd/MM/yyyy}";
                    else if (endDate.HasValue)
                        dateRange = $"Up to {endDate.Value:dd/MM/yyyy}";

                    worksheet.Cell(currentRow, 1).Value = $"SUPPLIER PAYMENT SUMMARY - {dateRange}";
                    worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    currentRow += 2;

                    // Filter info
                    string filterText = paymentStatus == "all" ? "All Status" : paymentStatus.ToUpper();
                    worksheet.Cell(currentRow, 1).Value = $"Filter: {filterText}";
                    worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                    currentRow += 2;

                    // Statistics
                    worksheet.Cell(currentRow, 1).Value = "TOTAL SUPPLIERS:";
                    worksheet.Cell(currentRow, 1).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 2).Value = reportData.Count;
                    worksheet.Cell(currentRow, 2).Style.Font.SetBold();

                    worksheet.Cell(currentRow, 4).Value = "TOTAL INVOICES:";
                    worksheet.Cell(currentRow, 4).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 5).Value = reportData.Sum(s => s.InvoiceCount);
                    worksheet.Cell(currentRow, 5).Style.Font.SetBold();

                    worksheet.Cell(currentRow, 7).Value = "TOTAL AMOUNT:";
                    worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Value = reportData.Sum(s => s.TotalInvoiceAmount);
                    worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    currentRow += 2;

                    // Headers
                    string[] headers = { "Supplier Code", "Supplier Name", "Contact Person", "Phone", "Email", "Invoice Count", "Total Amount", "Total Paid", "Outstanding", "Payment Status", "Last Payment Date" };
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
                    foreach (var item in reportData)
                    {
                        worksheet.Cell(currentRow, 1).Value = item.SupplierCode;
                        worksheet.Cell(currentRow, 2).Value = item.SupplierName;
                        worksheet.Cell(currentRow, 3).Value = item.ContactPerson ?? "-";
                        worksheet.Cell(currentRow, 4).Value = item.PhoneNo ?? "-";
                        worksheet.Cell(currentRow, 5).Value = item.Email ?? "-";
                        worksheet.Cell(currentRow, 6).Value = item.InvoiceCount;
                        worksheet.Cell(currentRow, 7).Value = item.TotalInvoiceAmount;
                        worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 8).Value = item.TotalPaid;
                        worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 9).Value = item.TotalOutstanding;
                        worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

                        // Color code status
                        var statusCell = worksheet.Cell(currentRow, 10);
                        statusCell.Value = item.PaymentStatus;
                        if (item.PaymentStatus == "FULLY PAID")
                            statusCell.Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                        else if (item.PaymentStatus == "PARTIALLY PAID")
                            statusCell.Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                        else if (item.PaymentStatus == "UNPAID")
                            statusCell.Style.Fill.SetBackgroundColor(XLColor.LightPink);

                        worksheet.Cell(currentRow, 11).Value = item.LastPaymentDate?.ToString("dd/MM/yyyy") ?? "-";

                        worksheet.Range(currentRow, 1, currentRow, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                    }

                    // Grand Total Row
                    if (reportData.Any())
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 5).Value = "GRAND TOTAL:";
                        worksheet.Cell(currentRow, 5).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                        worksheet.Cell(currentRow, 6).Value = reportData.Sum(s => s.InvoiceCount);
                        worksheet.Cell(currentRow, 6).Style.Font.SetBold();

                        worksheet.Cell(currentRow, 7).Value = reportData.Sum(s => s.TotalInvoiceAmount);
                        worksheet.Cell(currentRow, 7).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

                        worksheet.Cell(currentRow, 8).Value = reportData.Sum(s => s.TotalPaid);
                        worksheet.Cell(currentRow, 8).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

                        worksheet.Cell(currentRow, 9).Value = reportData.Sum(s => s.TotalOutstanding);
                        worksheet.Cell(currentRow, 9).Style.Font.SetBold();
                        worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(currentRow, 9).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    }

                    currentRow += 2;
                    worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Range(currentRow, 1, currentRow, 11).Merge();
                    worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"SupplierPaymentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting supplier payment summary to Excel");
                TempData["ErrorMessage"] = "Error exporting: " + ex.Message;
                return RedirectToAction("SupplierPaymentSummary");
            }
        }

        #endregion

        #region Export to PDF

        [HttpPost]
        public async Task<IActionResult> ExportSupplierPaymentSummaryToPdf(DateTime? startDate, DateTime? endDate, string paymentStatus = "all")
        {
            try
            {
                var companyCode = User.FindFirstValue("CompanyCode");
                var companyName = User.FindFirstValue("CompanyName") ?? "";
                var printedBy = User.Identity?.Name ?? "System";

                // Get all suppliers
                var suppliers = await _context.Suppliers
                    .Where(s => s.CompanyCode == companyCode)
                    .OrderBy(s => s.SupplierName)
                    .ToListAsync();

                var supplierCodes = suppliers.Select(s => s.SupplierCode).ToList();

                // Build invoice query with date filters
                var invoiceQuery = _context.InvoiceReceive
                    .Where(i => i.CompanyCode == companyCode && supplierCodes.Contains(i.SupplierCode));

                if (startDate.HasValue)
                {
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    var endDateFilter = endDate.Value.Date.AddDays(1);
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= endDateFilter);
                }

                var invoices = await invoiceQuery
                    .Include(i => i.Payments)
                    .ToListAsync();

                // Build report data
                var reportData = suppliers
                    .Select(supplier =>
                    {
                        var supplierInvoices = invoices.Where(i => i.SupplierCode == supplier.SupplierCode).ToList();

                        var totalInvoiceAmount = supplierInvoices.Sum(i => i.InvoiceAmount);
                        var totalPaid = supplierInvoices.Sum(i => i.AmountPaid ?? 0);
                        var totalOutstanding = supplierInvoices.Sum(i => i.Balance ?? 0);
                        var invoiceCount = supplierInvoices.Count;

                        string status;
                        if (totalOutstanding == 0 && totalPaid > 0)
                            status = "FULLY PAID";
                        else if (totalPaid > 0 && totalOutstanding > 0)
                            status = "PARTIALLY PAID";
                        else if (totalPaid == 0 && totalOutstanding > 0)
                            status = "UNPAID";
                        else
                            status = "NO INVOICES";

                        var lastPaymentDate = supplierInvoices
                            .SelectMany(i => i.Payments ?? new List<InvoicePayment>())
                            .OrderByDescending(p => p.TransDate)
                            .FirstOrDefault()?.TransDate;

                        return new SupplierPaymentSummaryDto
                        {
                            SupplierCode = supplier.SupplierCode,
                            SupplierName = supplier.SupplierName,
                            ContactPerson = supplier.ContactPerson,
                            PhoneNo = supplier.PhoneNo,
                            Email = supplier.Email,
                            InvoiceCount = invoiceCount,
                            TotalInvoiceAmount = totalInvoiceAmount,
                            TotalPaid = totalPaid,
                            TotalOutstanding = totalOutstanding,
                            PaymentStatus = status,
                            LastPaymentDate = lastPaymentDate,
                            GlAccountNo = supplier.GlAccountNo,
                            GlAccountName = supplier.GlAccountName
                        };
                    })
                    .Where(s => s.InvoiceCount > 0)
                    .ToList();

                // Apply payment status filter
                if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "all")
                {
                    reportData = reportData.Where(s => s.PaymentStatus == paymentStatus.ToUpper()).ToList();
                }

                reportData = reportData.OrderBy(s => s.SupplierName).ToList();

                if (!reportData.Any())
                {
                    TempData["ErrorMessage"] = "No data found for the selected filters.";
                    return RedirectToAction("SupplierPaymentSummary");
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
                            header.Item().AlignCenter().Text("SUPPLIER PAYMENT SUMMARY").FontSize(12).Bold();

                            string dateRange = "ALL TIME";
                            if (startDate.HasValue && endDate.HasValue)
                                dateRange = $"{startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
                            else if (startDate.HasValue)
                                dateRange = $"From {startDate.Value:dd/MM/yyyy}";
                            else if (endDate.HasValue)
                                dateRange = $"Up to {endDate.Value:dd/MM/yyyy}";

                            header.Item().AlignCenter().Text($"Period: {dateRange}").FontSize(10).Bold();
                            header.Item().AlignCenter().Text($"Filter: {(paymentStatus == "all" ? "All Status" : paymentStatus.ToUpper())}").FontSize(10);
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
                                    cols.RelativeColumn(1);
                                });

                                var totalInvoiceAmount = reportData.Sum(s => s.TotalInvoiceAmount);
                                var totalPaid = reportData.Sum(s => s.TotalPaid);
                                var totalOutstanding = reportData.Sum(s => s.TotalOutstanding);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Suppliers:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count.ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Invoices:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Sum(s => s.InvoiceCount).ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Amount:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalInvoiceAmount:N0}").FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Fully Paid:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count(s => s.PaymentStatus == "FULLY PAID").ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Partially Paid:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count(s => s.PaymentStatus == "PARTIALLY PAID").ToString()).FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Unpaid:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignCenter().Text(reportData.Count(s => s.PaymentStatus == "UNPAID").ToString()).FontSize(8);

                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Paid:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalPaid:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Total Outstanding:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{totalOutstanding:N0}").FontSize(8);
                                summaryTable.Cell().Border(0.2f).Background("#e8f4f8").Padding(4).Text("Collection Rate:").Bold().FontSize(8);
                                summaryTable.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{(totalInvoiceAmount > 0 ? (totalPaid / totalInvoiceAmount * 100) : 0):F1}%").FontSize(8);
                            });

                            contentCol.Item().PaddingTop(0.5f, Unit.Centimetre);

                            // Detailed Table
                            contentCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(0.9f);
                                    cols.RelativeColumn(1.5f);
                                    cols.RelativeColumn(1.0f);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(0.5f);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(0.8f);
                                    cols.RelativeColumn(0.8f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Supplier Code").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Supplier Name").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Contact Person").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Phone").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Invoices").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Amount").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Total Paid").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Outstanding").Bold().FontSize(7);
                                    header.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignCenter().Text("Payment Status").Bold().FontSize(7);
                                });

                                foreach (var item in reportData)
                                {
                                    string statusColor = item.PaymentStatus == "FULLY PAID" ? "#d4edda" :
                                                        item.PaymentStatus == "PARTIALLY PAID" ? "#fff3cd" :
                                                        item.PaymentStatus == "UNPAID" ? "#f8d7da" : "#e2e3e5";

                                    table.Cell().Border(0.2f).Padding(4).Text(item.SupplierCode ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.SupplierName ?? "").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.ContactPerson ?? "-").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).Text(item.PhoneNo ?? "-").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignCenter().Text(item.InvoiceCount.ToString()).FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalInvoiceAmount:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalPaid:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Padding(4).AlignRight().Text($"{item.TotalOutstanding:N0}").FontSize(7);
                                    table.Cell().Border(0.2f).Background(statusColor).Padding(4).AlignCenter().Text(item.PaymentStatus).FontSize(7);
                                }

                                // Grand Total Row
                                if (reportData.Any())
                                {
                                    table.Cell().ColumnSpan(5).Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text("GRAND TOTAL:").Bold().FontSize(8);
                                    table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{reportData.Sum(s => s.TotalInvoiceAmount):N0}").Bold().FontSize(8);
                                    table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{reportData.Sum(s => s.TotalPaid):N0}").Bold().FontSize(8);
                                    table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4).AlignRight().Text($"{reportData.Sum(s => s.TotalOutstanding):N0}").Bold().FontSize(8);
                                    table.Cell().Border(0.2f).Background("#f0f0f0").Padding(4);
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

                return File(stream.ToArray(), "application/pdf", $"SupplierPaymentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting supplier payment summary to PDF");
                TempData["ErrorMessage"] = "Error exporting: " + ex.Message;
                return RedirectToAction("SupplierPaymentSummary");
            }
        }

        #endregion
    }

    #region View Models

    public class SupplierPaymentSummaryDto
    {
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? ContactPerson { get; set; }
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public string? GlAccountNo { get; set; }
        public string? GlAccountName { get; set; }
    }

    public class SupplierPaymentSummaryViewModel
    {
        public List<SupplierPaymentSummaryDto> SupplierReports { get; set; } = new();
        public int TotalSuppliers { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int PaidSuppliers { get; set; }
        public int PartiallyPaidSuppliers { get; set; }
        public int UnpaidSuppliers { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? PaymentStatusFilter { get; set; }
        public DateTime ReportDate { get; set; }
        public bool HasData { get; set; }
        public string? UserCompanyCode { get; set; }
        public string? CompanyName { get; set; }
    }

    #endregion
}