using ClosedXML.Excel;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
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

		public LoanReportController(ApplicationDbContext context)
		{
			_context = context;
		}

		public IActionResult LoansIssued()
		{
			ViewBag.StartDate = DateTime.Now.AddMonths(-1);
			ViewBag.EndDate = DateTime.Now;
			ViewBag.HasData = false;

			// Return an empty list instead of null
			var emptyList = new List<LoanIssuedReportViewModel>();
			return View("~/Views/Reports/LoansIssued.cshtml", emptyList);
		}
		[HttpPost]
		public async Task<IActionResult> LoansIssued(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");

			// Add one day to endDate to include the entire end date
			endDate = endDate.Date.AddDays(1).AddSeconds(-1);

			var loans = await _context.Loans
				.Include(l => l.Member)
				.Include(l => l.LoanType)
				.Where(l => l.CompanyCode == companyCode
					&& l.AuditTime >= startDate
					&& l.AuditTime <= endDate
					&& l.Status == 6) // Disbursed status
				.OrderBy(l => l.AuditTime)
				.Select(l => new
				{
					l.Id,
					l.MemberNo,
					l.LoanNo,
					Member = l.Member,
					l.ApplicDate,
					l.AppraisalDate,
					l.EndorsementDate,
					AuditTime = l.AuditTime,
					l.RepayPeriod,
					LoanAmt = l.LoanAmt ?? 0,
					Aamount = l.Aamount ?? 0,
					l.Interest,
					l.LoanCode,
					LoanType = l.LoanType != null ? l.LoanType.LoanType1 : null
				})
				.ToListAsync();

			// Process the data in memory - ALWAYS return a list, even if empty
			var reportData = loans.Select(l => new LoanIssuedReportViewModel
			{
				No = l.Id,
				MemberNo = l.MemberNo,
				LoanNo = l.LoanNo,
				Name = l.Member != null
					? (!string.IsNullOrEmpty(l.Member.FullName)
						? l.Member.FullName
						: (l.Member.Surname + " " + l.Member.OtherNames).Trim())
					: "",
				ApplicationDate = l.ApplicDate,
				AppraisalDate = l.AppraisalDate,
				EndorsementDate = l.EndorsementDate,
				DateIssued = l.AuditTime,
				LoanPeriodMonths = l.RepayPeriod ?? 0,
				LoanApplied = l.LoanAmt,
				ApprovedAmount = l.Aamount,
				InterestRate = l.Interest,
				LoanType = l.LoanType ?? l.LoanCode
			}).ToList(); // This will be an empty list if no data

			ViewBag.StartDate = startDate;
			ViewBag.EndDate = endDate;

			ViewBag.TotalLoanApplied = reportData.Sum(l => l.LoanApplied);
			ViewBag.TotalApprovedAmount = reportData.Sum(l => l.ApprovedAmount);
			ViewBag.RecordCount = reportData.Count;
			ViewBag.HasData = reportData.Any(); // Flag to check if data exists

			return View("~/Views/Reports/LoansIssued.cshtml", reportData); // Always pass the list (even if empty)
		}

		[HttpPost]
		public async Task<IActionResult> ExportToExcel(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			endDate = endDate.Date.AddDays(1).AddSeconds(-1);

			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode
					&& l.AuditTime >= startDate
					&& l.AuditTime <= endDate
					&& l.Status == 6)
				.OrderBy(l => l.AuditTime)
				.Select(l => new
				{
					l.MemberNo,
					l.LoanNo,
					Member = l.Member,
					l.ApplicDate,
					l.AppraisalDate,
					l.EndorsementDate,
					AuditTime = l.AuditTime,
					l.RepayPeriod,
					LoanAmt = l.LoanAmt ?? 0,
					Aamount = l.Aamount ?? 0
				})
				.ToListAsync();

			var reportData = loans.Select(l => new LoanIssuedReportViewModel
			{
				MemberNo = l.MemberNo,
				LoanNo = l.LoanNo,
				Name = l.Member != null
					? (!string.IsNullOrEmpty(l.Member.FullName?.ToString())
						? l.Member.FullName.ToString()
						: (l.Member.Surname + " " + l.Member.OtherNames).Trim())
					: "",
				ApplicationDate = l.ApplicDate,
				AppraisalDate = l.AppraisalDate,
				EndorsementDate = l.EndorsementDate,
				DateIssued = l.AuditTime,
				LoanPeriodMonths = l.RepayPeriod ?? 0,
				LoanApplied = l.LoanAmt,
				ApprovedAmount = l.Aamount
			}).ToList();

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Loans Issued");
				var currentRow = 1;

				// Title
				worksheet.Cell(currentRow, 1).Value = "LOANS ISSUED REPORT";
				worksheet.Range(currentRow, 1, currentRow, 11).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Company Info
				worksheet.Cell(currentRow, 1).Value = $"Company: {User.FindFirstValue("CompanyName")}";
				worksheet.Range(currentRow, 1, currentRow, 11).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				currentRow++;

				// Period
				worksheet.Cell(currentRow, 1).Value = $"Period From: {startDate:dd/MM/yyyy} To: {endDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 11).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				currentRow++;

				// Generated Date
				worksheet.Cell(currentRow, 1).Value = $"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 11).Merge();
				currentRow += 2;

				// Headers - ALWAYS show headers even if no data
				var headers = new[] { "No.", "MemberNo", "LoanNo", "MemberName", "App.Date",
					  "Appraisal Date", "Endorsement Date", "Date Issued", "Period",
					  "Loan Applied (KES)", "Approved Amt (KES)" };

				for (int i = 0; i < headers.Length; i++)
				{
					worksheet.Cell(currentRow, i + 1).Value = headers[i];
					worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
					worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					worksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				currentRow++;

				// Data - Always show at least one row with dashes if no data
				if (reportData.Any())
				{
					int serialNo = 1;
					foreach (var loan in reportData)
					{
						worksheet.Cell(currentRow, 1).Value = serialNo++;
						worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 2).Value = loan.MemberNo;
						worksheet.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 3).Value = loan.LoanNo;
						worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 4).Value = loan.Name;

						worksheet.Cell(currentRow, 5).Value = loan.ApplicationDate?.ToString("dd/MM/yyyy");
						worksheet.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 6).Value = loan.AppraisalDate?.ToString("dd/MM/yyyy");
						worksheet.Cell(currentRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 7).Value = loan.EndorsementDate?.ToString("dd/MM/yyyy");
						worksheet.Cell(currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 8).Value = loan.DateIssued?.ToString("dd/MM/yyyy");
						worksheet.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 9).Value = loan.LoanPeriodMonths;
						worksheet.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

						worksheet.Cell(currentRow, 10).Value = loan.LoanApplied;
						worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
						worksheet.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

						worksheet.Cell(currentRow, 11).Value = loan.ApprovedAmount;
						worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
						worksheet.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

						for (int i = 1; i <= 11; i++)
						{
							worksheet.Cell(currentRow, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						}
						currentRow++;
					}

					// Totals row - Only show if there is data
					currentRow++;
					worksheet.Cell(currentRow, 9).Value = "GRAND TOTALS:";
					worksheet.Cell(currentRow, 9).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

					worksheet.Cell(currentRow, 10).Value = reportData.Sum(l => l.LoanApplied);
					worksheet.Cell(currentRow, 10).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

					worksheet.Cell(currentRow, 11).Value = reportData.Sum(l => l.ApprovedAmount);
					worksheet.Cell(currentRow, 11).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
				}
				else
				{
					// Show one empty row with dashes to indicate no data
					worksheet.Cell(currentRow, 1).Value = "-";
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					worksheet.Cell(currentRow, 2).Value = "-";
					worksheet.Cell(currentRow, 3).Value = "-";
					worksheet.Cell(currentRow, 4).Value = "-";
					worksheet.Cell(currentRow, 5).Value = "-";
					worksheet.Cell(currentRow, 6).Value = "-";
					worksheet.Cell(currentRow, 7).Value = "-";
					worksheet.Cell(currentRow, 8).Value = "-";
					worksheet.Cell(currentRow, 9).Value = "-";
					worksheet.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					worksheet.Cell(currentRow, 10).Value = "-";
					worksheet.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
					worksheet.Cell(currentRow, 11).Value = "-";
					worksheet.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

					for (int i = 1; i <= 11; i++)
					{
						worksheet.Cell(currentRow, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					}
				}

				// Auto-fit columns
				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"LoansIssued_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportToPdf(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			endDate = endDate.Date.AddDays(1).AddSeconds(-1);

			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode
					&& l.AuditTime >= startDate
					&& l.AuditTime <= endDate
					&& l.Status == 6)
				.OrderBy(l => l.AuditTime)
				.Select(l => new
				{
					l.MemberNo,
					l.LoanNo,
					Member = l.Member,
					l.ApplicDate,
					l.AppraisalDate,
					l.EndorsementDate,
					AuditTime = l.AuditTime,
					l.RepayPeriod,
					LoanAmt = l.LoanAmt ?? 0,
					Aamount = l.Aamount ?? 0
				})
				.ToListAsync();

			var reportData = loans.Select(l => new LoanIssuedReportViewModel
			{
				MemberNo = l.MemberNo,
				LoanNo = l.LoanNo,
				Name = l.Member != null
					? (!string.IsNullOrEmpty(l.Member.FullName?.ToString())
						? l.Member.FullName.ToString()
						: (l.Member.Surname + " " + l.Member.OtherNames).Trim())
					: "",
				ApplicationDate = l.ApplicDate,
				AppraisalDate = l.AppraisalDate,
				EndorsementDate = l.EndorsementDate,
				DateIssued = l.AuditTime,
				LoanPeriodMonths = l.RepayPeriod ?? 0,
				LoanApplied = l.LoanAmt,
				ApprovedAmount = l.Aamount
			}).ToList();

			using (var stream = new MemoryStream())
			{
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				var document = new Document(pdf);

				// Set fonts
				var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				// Title
				document.Add(new Paragraph("LOANS ISSUED REPORT")
					.SetFont(boldFont)
					.SetFontSize(18)
					.SetTextAlignment(TextAlignment.CENTER));

				// Company Name
				document.Add(new Paragraph(User.FindFirstValue("CompanyName"))
					.SetFont(boldFont)
					.SetFontSize(12)
					.SetTextAlignment(TextAlignment.CENTER));

				// Period
				document.Add(new Paragraph($"Period From: {startDate:dd/MM/yyyy} To: {endDate:dd/MM/yyyy}")
					.SetFont(normalFont)
					.SetFontSize(11)
					.SetTextAlignment(TextAlignment.CENTER));

				// Generated On
				document.Add(new Paragraph($"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(10)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Create table
				var table = new Table(11, false);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Add headers - ALWAYS show headers even if no data
				var headers = new[] { "No.", "Member No", "Loan No", "Name", "App Date", "Appraisal",
					  "Endorse", "Issued", "Period", "Applied", "Approved" };

				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Add data - Always show at least one row with dashes if no data
				if (reportData.Any())
				{
					int serialNo = 1;
					foreach (var loan in reportData)
					{
						table.AddCell(new Cell().Add(new Paragraph(serialNo++.ToString()))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.MemberNo ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.LoanNo ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.Name ?? ""))
							.SetFontSize(8));

						table.AddCell(new Cell().Add(new Paragraph(loan.ApplicationDate?.ToString("dd/MM/yy") ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.AppraisalDate?.ToString("dd/MM/yy") ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.EndorsementDate?.ToString("dd/MM/yy") ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.DateIssued?.ToString("dd/MM/yy") ?? ""))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.LoanPeriodMonths.ToString()))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.CENTER));

						table.AddCell(new Cell().Add(new Paragraph(loan.LoanApplied.ToString("N0")))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.RIGHT));

						table.AddCell(new Cell().Add(new Paragraph(loan.ApprovedAmount.ToString("N0")))
							.SetFontSize(8)
							.SetTextAlignment(TextAlignment.RIGHT));
					}

					// Totals row - Only if there is data
					table.AddCell(new Cell(1, 9).Add(new Paragraph("GRAND TOTALS:"))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));

					table.AddCell(new Cell().Add(new Paragraph(reportData.Sum(l => l.LoanApplied).ToString("N0")))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));

					table.AddCell(new Cell().Add(new Paragraph(reportData.Sum(l => l.ApprovedAmount).ToString("N0")))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));
				}
				else
				{
					// Add a row with dashes to show structure when no data
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph("-")).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				}

				document.Add(table);

				document.Close();

				var content = stream.ToArray();
				return File(content, "application/pdf",
					$"LoansIssued_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf");
			}
		}

		//Loans Per SACCO Report

		public IActionResult LoansPerSacco()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new LoansPerSaccoIndexViewModel
			{
				CompletedLoans = new List<LoansPerSaccoReportViewModel>(),
				IncompleteLoans = new List<LoansPerSaccoReportViewModel>(),
				ReportDate = reportDate,
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
			ViewBag.CompanyName = companyName;
			ViewBag.HasData = false;

			return View("~/Views/Reports/LoansPerSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> LoansPerSacco(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all loans for the company
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode)
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			// Get the latest repayment balance for each loan
			var loanNos = loans.Select(l => l.LoanNo).ToList();

			var latestRepayments = await _context.Repays
				.Where(r => loanNos.Contains(r.LoanNo) && r.CompanyCode == companyCode)
				.GroupBy(r => r.LoanNo)
				.Select(g => new
				{
					LoanNo = g.Key,
					LatestBalance = g.OrderByDescending(r => r.DateReceived)
									.Select(r => r.LoanBalance)
									.FirstOrDefault() ?? 0,
					TotalPaid = g.Sum(r => r.Amount ?? 0),
					TotalPrincipal = g.Sum(r => r.Principal ?? 0),
					TotalInterest = g.Sum(r => r.Interest ?? 0),
					LastPaymentDate = g.Max(r => r.DateReceived)
				})
				.ToDictionaryAsync(g => g.LoanNo, g => g);

			var completedLoans = new List<LoansPerSaccoReportViewModel>();
			var incompleteLoans = new List<LoansPerSaccoReportViewModel>();

			foreach (var loan in loans)
			{
				// Get current balance from loan record or from repayments
				decimal currentBalance = 0;
				decimal totalPaid = 0;
				decimal principalPaid = 0;
				decimal interestPaid = 0;
				DateTime? lastPaymentDate = null;

				if (latestRepayments.ContainsKey(loan.LoanNo))
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
					// If no repayments found, balance is the full loan amount
					currentBalance = loan.LoanAmt ?? 0;
				}

				// Handle FullName safely
				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
					{
						fullName = loan.Member.FullName.ToString();
					}
					else
					{
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
					}
				}

				if (string.IsNullOrWhiteSpace(fullName))
					fullName = "N/A";

				var loanViewModel = new LoansPerSaccoReportViewModel
				{
					MemberNo = loan.MemberNo,
					LoanNo = loan.LoanNo,
					FullName = fullName,
					LoanCode = loan.LoanCode ?? "-",
					ApplicDate = loan.ApplicDate,
					RepayPeriod = loan.RepayPeriod,
					LoanAmt = loan.LoanAmt ?? 0,
					Balance = currentBalance,
					PrincipalPaid = principalPaid,
					InterestPaid = interestPaid,
					TotalPaid = totalPaid,
					LastPaymentDate = lastPaymentDate,
					LoanStatus = currentBalance == 0 ? "COMPLETED" : "ACTIVE"
				};

				// Separate completed loans (zero balance) from incomplete loans (with balance)
				if (currentBalance == 0)
				{
					completedLoans.Add(loanViewModel);
				}
				else
				{
					incompleteLoans.Add(loanViewModel);
				}
			}

			// Calculate statistics
			int totalCompletedLoans = completedLoans.Count;
			int totalIncompleteLoans = incompleteLoans.Count;

			decimal totalCompletedLoanAmount = completedLoans.Sum(l => l.LoanAmt ?? 0);
			decimal totalIncompleteLoanAmount = incompleteLoans.Sum(l => l.LoanAmt ?? 0);
			decimal totalOutstandingBalance = incompleteLoans.Sum(l => l.Balance ?? 0);
			decimal totalLoanAmount = loans.Sum(l => l.LoanAmt ?? 0);

			var viewModel = new LoansPerSaccoIndexViewModel
			{
				CompletedLoans = completedLoans,
				IncompleteLoans = incompleteLoans,
				TotalCompletedLoans = totalCompletedLoans,
				TotalIncompleteLoans = totalIncompleteLoans,
				TotalLoans = totalCompletedLoans + totalIncompleteLoans,
				TotalCompletedLoanAmount = totalCompletedLoanAmount,
				TotalIncompleteLoanAmount = totalIncompleteLoanAmount,
				TotalOutstandingBalance = totalOutstandingBalance,
				TotalLoanAmount = totalLoanAmount,
				ReportDate = reportDate,
				HasData = completedLoans.Any() || incompleteLoans.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.ReportDate = reportDate;
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
		public async Task<IActionResult> ExportLoansPerSaccoToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all loans for the company
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode)
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			var loanNos = loans.Select(l => l.LoanNo).ToList();

			var latestRepayments = await _context.Repays
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

			var completedLoans = new List<dynamic>();
			var incompleteLoans = new List<dynamic>();

			foreach (var loan in loans)
			{
				decimal currentBalance = 0;
				if (latestRepayments.ContainsKey(loan.LoanNo))
				{
					currentBalance = latestRepayments[loan.LoanNo].LatestBalance;
				}
				else
				{
					currentBalance = loan.LoanAmt ?? 0;
				}

				// Handle FullName safely
				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
					{
						fullName = loan.Member.FullName.ToString();
					}
					else
					{
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
					}
				}

				if (string.IsNullOrWhiteSpace(fullName))
					fullName = "N/A";

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
				// Completed Loans Worksheet
				var completedWorksheet = workbook.Worksheets.Add("Completed Loans");
				var currentRow = 1;

				// Title
				completedWorksheet.Cell(currentRow, 1).Value = $"LOANS COMPLETED FOR {companyName.ToUpper()}";
				completedWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
				completedWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				completedWorksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Report Date
				completedWorksheet.Cell(currentRow, 1).Value = $"AS AT {reportDate:dd/MM/yyyy}";
				completedWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
				completedWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
				completedWorksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Summary
				completedWorksheet.Cell(currentRow, 1).Value = "Completed Loans:";
				completedWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 2).Value = completedLoans.Count;
				completedWorksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow += 2;

				// Headers
				var headers = new[] { "MemberNo", "LoanNo", "Names", "LoanCode", "ApplicDate", "RepayPeriod", "LoanAmt", "Balance" };

				for (int i = 0; i < headers.Length; i++)
				{
					completedWorksheet.Cell(currentRow, i + 1).Value = headers[i];
					completedWorksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
					completedWorksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
					completedWorksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					completedWorksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				currentRow++;

				// Data
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

				// Grand Total
				currentRow += 2;
				completedWorksheet.Cell(currentRow, 6).Value = "GRAND TOTAL:";
				completedWorksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
				completedWorksheet.Cell(currentRow, 7).Value = completedLoans.Sum(l => (decimal)l.LoanAmt);
				completedWorksheet.Cell(currentRow, 7).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
				completedWorksheet.Cell(currentRow, 8).Value = completedLoans.Sum(l => (decimal)l.Balance);
				completedWorksheet.Cell(currentRow, 8).Style.Font.Bold = true;
				completedWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

				completedWorksheet.Columns().AdjustToContents();

				// Incomplete Loans Worksheet
				var incompleteWorksheet = workbook.Worksheets.Add("Incomplete Loans");
				currentRow = 1;

				// Title
				incompleteWorksheet.Cell(currentRow, 1).Value = $"LOANS IN PROGRESS FOR {companyName.ToUpper()}";
				incompleteWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				incompleteWorksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Report Date
				incompleteWorksheet.Cell(currentRow, 1).Value = $"AS AT {reportDate:dd/MM/yyyy}";
				incompleteWorksheet.Range(currentRow, 1, currentRow, 8).Merge();
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
				incompleteWorksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Summary
				incompleteWorksheet.Cell(currentRow, 1).Value = "Loans in Progress:";
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 2).Value = incompleteLoans.Count;
				incompleteWorksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				currentRow++;
				incompleteWorksheet.Cell(currentRow, 1).Value = "Outstanding Balance:";
				incompleteWorksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 2).Value = incompleteLoans.Sum(l => (decimal)l.Balance);
				incompleteWorksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
				currentRow += 2;

				// Headers
				for (int i = 0; i < headers.Length; i++)
				{
					incompleteWorksheet.Cell(currentRow, i + 1).Value = headers[i];
					incompleteWorksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
					incompleteWorksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
					incompleteWorksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					incompleteWorksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				currentRow++;

				// Data
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

				// Grand Total
				currentRow += 2;
				incompleteWorksheet.Cell(currentRow, 6).Value = "GRAND TOTAL:";
				incompleteWorksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
				incompleteWorksheet.Cell(currentRow, 7).Value = incompleteLoans.Sum(l => (decimal)l.LoanAmt);
				incompleteWorksheet.Cell(currentRow, 7).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
				incompleteWorksheet.Cell(currentRow, 8).Value = incompleteLoans.Sum(l => (decimal)l.Balance);
				incompleteWorksheet.Cell(currentRow, 8).Style.Font.Bold = true;
				incompleteWorksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

				incompleteWorksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=LoansPerSacco_{reportDate:yyyyMMdd}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"LoansPerSacco_{reportDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportLoansPerSaccoToPdf(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all loans for the company
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode)
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			var loanNos = loans.Select(l => l.LoanNo).ToList();

			var latestRepayments = await _context.Repays
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

			var completedLoans = new List<dynamic>();
			var incompleteLoans = new List<dynamic>();

			foreach (var loan in loans)
			{
				decimal currentBalance = 0;
				if (latestRepayments.ContainsKey(loan.LoanNo))
				{
					currentBalance = latestRepayments[loan.LoanNo].LatestBalance;
				}
				else
				{
					currentBalance = loan.LoanAmt ?? 0;
				}

				// Handle FullName safely
				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
					{
						fullName = loan.Member.FullName.ToString();
					}
					else
					{
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
					}
				}

				if (string.IsNullOrWhiteSpace(fullName))
					fullName = "N/A";

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

			using (var stream = new MemoryStream())
			{
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4.Rotate());

				var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				// Company Name
				document.Add(new Paragraph(companyName.ToUpper())
					.SetFont(boldFont)
					.SetFontSize(18)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph($"LOANS REPORT AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Summary Statistics
				var summaryTable = new Table(4);
				summaryTable.SetWidth(UnitValue.CreatePercentValue(80));
				summaryTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);

				summaryTable.AddCell(new Cell().Add(new Paragraph("Completed Loans:")).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph(completedLoans.Count.ToString())).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph("Loans in Progress:")).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph(incompleteLoans.Count.ToString())).SetFont(boldFont));

				summaryTable.AddCell(new Cell().Add(new Paragraph("Completed Amount:")).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", completedLoans.Sum(l => (decimal)l.LoanAmt)))).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph("Outstanding Balance:")).SetFont(boldFont));
				summaryTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", incompleteLoans.Sum(l => (decimal)l.Balance)))).SetFont(boldFont));

				document.Add(summaryTable);
				document.Add(new Paragraph("\n"));

				// Completed Loans Section
				if (completedLoans.Any())
				{
					document.Add(new Paragraph("COMPLETED LOANS")
						.SetFont(boldFont)
						.SetFontSize(12)
						.SetTextAlignment(TextAlignment.LEFT));
					document.Add(new Paragraph("\n"));

					var completedTable = new Table(8);
					completedTable.SetWidth(UnitValue.CreatePercentValue(100));

					var headers = new[] { "MemberNo", "LoanNo", "Names", "LoanCode", "ApplicDate", "Period", "LoanAmt", "Balance" };
					foreach (var header in headers)
					{
						completedTable.AddHeaderCell(new Cell().Add(new Paragraph(header))
							.SetFont(boldFont)
							.SetFontSize(9)
							.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
							.SetTextAlignment(TextAlignment.CENTER));
					}

					foreach (var loan in completedLoans)
					{
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.MemberNo ?? "")).SetFontSize(8));
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.LoanNo ?? "")).SetFontSize(8));
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.FullName ?? "")).SetFontSize(8));
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.LoanCode ?? "")).SetFontSize(8));
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.ApplicDate?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(8));
						completedTable.AddCell(new Cell().Add(new Paragraph(loan.RepayPeriod?.ToString() ?? "0")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
						completedTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.LoanAmt))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
						completedTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Balance))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					}

					// Grand Total for Completed Loans
					completedTable.AddCell(new Cell(1, 6).Add(new Paragraph("GRAND TOTAL:"))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));
					completedTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", completedLoans.Sum(l => (decimal)l.LoanAmt))))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));
					completedTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", completedLoans.Sum(l => (decimal)l.Balance))))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));

					document.Add(completedTable);
					document.Add(new Paragraph("\n\n"));
				}

				// Incomplete Loans Section
				if (incompleteLoans.Any())
				{
					document.Add(new Paragraph("LOANS IN PROGRESS")
						.SetFont(boldFont)
						.SetFontSize(12)
						.SetTextAlignment(TextAlignment.LEFT));
					document.Add(new Paragraph("\n"));

					var incompleteTable = new Table(8);
					incompleteTable.SetWidth(UnitValue.CreatePercentValue(100));

					var headers = new[] { "MemberNo", "LoanNo", "Names", "LoanCode", "ApplicDate", "Period", "LoanAmt", "Balance" };
					foreach (var header in headers)
					{
						incompleteTable.AddHeaderCell(new Cell().Add(new Paragraph(header))
							.SetFont(boldFont)
							.SetFontSize(9)
							.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
							.SetTextAlignment(TextAlignment.CENTER));
					}

					foreach (var loan in incompleteLoans)
					{
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.MemberNo ?? "")).SetFontSize(8));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.LoanNo ?? "")).SetFontSize(8));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.FullName ?? "")).SetFontSize(8));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.LoanCode ?? "")).SetFontSize(8));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.ApplicDate?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(8));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(loan.RepayPeriod?.ToString() ?? "0")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.LoanAmt))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
						incompleteTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Balance))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					}

					// Grand Total for Incomplete Loans
					incompleteTable.AddCell(new Cell(1, 6).Add(new Paragraph("GRAND TOTAL:"))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));
					incompleteTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", incompleteLoans.Sum(l => (decimal)l.LoanAmt))))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));
					incompleteTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", incompleteLoans.Sum(l => (decimal)l.Balance))))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetTextAlignment(TextAlignment.RIGHT));

					document.Add(incompleteTable);
				}

				// Footer
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=LoansPerSacco_{reportDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"LoansPerSacco_{reportDate:yyyyMMdd}.pdf");
			}
		}

		//Aging Analysis Report

		public IActionResult AgingAnalysis()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;
			var asAtDate = DateTime.Now; // Default to current date

			var viewModel = new AgingAnalysisIndexViewModel
			{
				Loans = new List<AgingAnalysisViewModel>(),
				ReportDate = reportDate,
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

			ViewBag.ReportDate = reportDate;
			ViewBag.AsAtDate = asAtDate;
			ViewBag.HasData = false;

			return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> AgingAnalysis(DateTime asAtDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			// Get all active/disbursed loans (status 6 = Disbursed)
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode
					&& l.Status == 6) // Disbursed loans only
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			var loanNos = loans.Select(l => l.LoanNo).ToList();

			// Get loan balances from Loanbal table
			var loanBalances = await _context.Loanbals
				.Where(lb => loanNos.Contains(lb.LoanNo)
					&& lb.Companycode == companyCode)
				.ToDictionaryAsync(lb => lb.LoanNo, lb => lb);

			// Get last repayment dates from Repay table
			var lastRepayments = await _context.Repays
				.Where(r => loanNos.Contains(r.LoanNo)
					&& r.CompanyCode == companyCode)
				.GroupBy(r => r.LoanNo)
				.Select(g => new
				{
					LoanNo = g.Key,
					LastRepayDate = g.Max(r => r.DateReceived),
					TotalPaid = g.Sum(r => r.Amount ?? 0)
				})
				.ToDictionaryAsync(g => g.LoanNo, g => g);

			var reportData = new List<AgingAnalysisViewModel>();

			foreach (var loan in loans)
			{
				// Get loan balance
				decimal currentBalance = loan.LoanAmt ?? 0;
				DateTime? dueDate = null;
				DateTime? lastRepayDate = null;

				// Fix: Handle dateIssued properly - AuditTime is non-nullable in Loan.cs
				// But we can still use it directly since it's always has a value
				DateTime? dateIssued = loan.AuditTime; // This is fine since it's a non-nullable DateTime

				// Optionally, if you want to use ApplicDate as fallback:
				// if (loan.ApplicDate.HasValue) dateIssued = loan.ApplicDate.Value;

				if (loanBalances.ContainsKey(loan.LoanNo))
				{
					var lb = loanBalances[loan.LoanNo];
					currentBalance = lb.Balance;
					dueDate = lb.Duedate;
				}

				if (lastRepayments.ContainsKey(loan.LoanNo))
				{
					lastRepayDate = lastRepayments[loan.LoanNo].LastRepayDate;
				}

				// Calculate days in arrears
				int daysInArrears = 0;
				if (dueDate.HasValue && dueDate.Value < asAtDate)
				{
					daysInArrears = (asAtDate - dueDate.Value).Days;
				}

				// Determine aging category
				int category = AgingCategories.GetCategoryFromDays(daysInArrears);

				// Get member name
				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
					{
						fullName = loan.Member.FullName.ToString();
					}
					else
					{
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
					}
				}
				if (string.IsNullOrWhiteSpace(fullName))
					fullName = "N/A";

				// Create loan record with aging amounts
				var loanVM = new AgingAnalysisViewModel
				{
					LoanNo = loan.LoanNo,
					MemberNo = loan.MemberNo,
					FullName = fullName,
					LoanBalance = currentBalance,
					RepayPeriod = dueDate,
					DateIssued = dateIssued,
					DaysInArrears = daysInArrears,
					LastRepayDate = lastRepayDate,
					DateOfCompletion = currentBalance == 0 ? asAtDate : (DateTime?)null,

					// Initialize all categories to 0
					Performing = 0,
					SpecialMention = 0,
					Watchful = 0,
					Substandard = 0,
					Doubtful = 0,
					Loss = 0,
					LossOver365 = 0,

					Classification = AgingCategories.GetCategoryName(category),
					ArrearsCategory = category
				};

				// Assign balance to appropriate category
				switch (category)
				{
					case AgingCategories.PERFORMING:
						loanVM.Performing = currentBalance;
						break;
					case AgingCategories.SPECIAL_MENTION:
						loanVM.SpecialMention = currentBalance;
						break;
					case AgingCategories.WATCHFUL:
						loanVM.Watchful = currentBalance;
						break;
					case AgingCategories.SUBSTANDARD:
						loanVM.Substandard = currentBalance;
						break;
					case AgingCategories.DOUBTFUL:
						loanVM.Doubtful = currentBalance;
						break;
					case AgingCategories.LOSS:
						loanVM.Loss = currentBalance;
						break;
					case AgingCategories.LOSS_OVER_365:
						loanVM.LossOver365 = currentBalance;
						break;
				}

				reportData.Add(loanVM);
			}

			// Calculate totals
			var viewModel = new AgingAnalysisIndexViewModel
			{
				Loans = reportData,
				TotalLoans = reportData.Count,
				TotalLoanBalance = reportData.Sum(l => l.LoanBalance),
				TotalPerforming = reportData.Sum(l => l.Performing),
				TotalSpecialMention = reportData.Sum(l => l.SpecialMention),
				TotalWatchful = reportData.Sum(l => l.Watchful),
				TotalSubstandard = reportData.Sum(l => l.Substandard),
				TotalDoubtful = reportData.Sum(l => l.Doubtful),
				TotalLoss = reportData.Sum(l => l.Loss),
				TotalLossOver365 = reportData.Sum(l => l.LossOver365),

				PerformingCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.PERFORMING),
				SpecialMentionCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.SPECIAL_MENTION),
				WatchfulCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.WATCHFUL),
				SubstandardCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.SUBSTANDARD),
				DoubtfulCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.DOUBTFUL),
				LossCount = reportData.Count(l => l.ArrearsCategory == AgingCategories.LOSS),
				LossOver365Count = reportData.Count(l => l.ArrearsCategory == AgingCategories.LOSS_OVER_365),

				ReportDate = reportDate,
				AsAtDate = asAtDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.AsAtDate = asAtDate;
			ViewBag.TotalLoans = reportData.Count;
			ViewBag.TotalLoanBalance = reportData.Sum(l => l.LoanBalance);
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/AgingAnalysis.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportAgingAnalysisToExcel(DateTime asAtDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all active/disbursed loans
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode
					&& l.Status == 6)
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			var loanNos = loans.Select(l => l.LoanNo).ToList();

			// Get loan balances
			var loanBalances = await _context.Loanbals
				.Where(lb => loanNos.Contains(lb.LoanNo)
					&& lb.Companycode == companyCode)
				.ToDictionaryAsync(lb => lb.LoanNo, lb => lb);

			// Get last repayments
			var lastRepayments = await _context.Repays
				.Where(r => loanNos.Contains(r.LoanNo)
					&& r.CompanyCode == companyCode)
				.GroupBy(r => r.LoanNo)
				.Select(g => new
				{
					LoanNo = g.Key,
					LastRepayDate = g.Max(r => r.DateReceived)
				})
				.ToDictionaryAsync(g => g.LoanNo, g => g);

			var reportData = new List<dynamic>();

			foreach (var loan in loans)
			{
				decimal currentBalance = loan.LoanAmt ?? 0;
				DateTime? dueDate = null;
				DateTime? lastRepayDate = null;

				// DateIssued - AuditTime is non-nullable in Loan.cs
				DateTime dateIssued = loan.AuditTime; // Direct assignment since it's non-nullable

				if (loanBalances.ContainsKey(loan.LoanNo))
				{
					var lb = loanBalances[loan.LoanNo];
					currentBalance = lb.Balance;
					dueDate = lb.Duedate;
				}

				if (lastRepayments.ContainsKey(loan.LoanNo))
				{
					lastRepayDate = lastRepayments[loan.LoanNo].LastRepayDate;
				}

				int daysInArrears = 0;
				if (dueDate.HasValue && dueDate.Value < asAtDate)
				{
					daysInArrears = (asAtDate - dueDate.Value).Days;
				}

				int category = AgingCategories.GetCategoryFromDays(daysInArrears);

				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
						fullName = loan.Member.FullName.ToString();
					else
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
				}

				// Create a dynamic object with all fields
				var loanData = new
				{
					loan.LoanNo,
					loan.MemberNo,
					Name = fullName,
					LoanBalance = currentBalance,
					RepayPeriod = dueDate?.ToString("dd/MM/yyyy"),
					DateIssued = dateIssued.ToString("dd/MM/yyyy"), // No null check needed
					DaysInArrears = daysInArrears,
					LastRepayDate = lastRepayDate?.ToString("dd/MM/yyyy"),
					DateOfCompletion = currentBalance == 0 ? asAtDate.ToString("dd/MM/yyyy") : "",
					Performing = category == AgingCategories.PERFORMING ? currentBalance : 0,
					SpecialMention = category == AgingCategories.SPECIAL_MENTION ? currentBalance : 0,
					Watchful = category == AgingCategories.WATCHFUL ? currentBalance : 0,
					Substandard = category == AgingCategories.SUBSTANDARD ? currentBalance : 0,
					Doubtful = category == AgingCategories.DOUBTFUL ? currentBalance : 0,
					Loss = category == AgingCategories.LOSS ? currentBalance : 0,
					LossOver365 = category == AgingCategories.LOSS_OVER_365 ? currentBalance : 0
				};

				reportData.Add(loanData);
			}

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Aging Analysis");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 16).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title
				worksheet.Cell(currentRow, 1).Value = "AGING ANALYSIS";
				worksheet.Range(currentRow, 1, currentRow, 16).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// As At Date
				worksheet.Cell(currentRow, 1).Value = $"As At: {asAtDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 16).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Headers - Exactly as in the image
				var headers = new[] {
			"LoanNo", "MemberNo", "Name", "Loan Balance", "Repay Period",
			"Date Issued", "Days In Arrears", "Last Repay Date", "Date of Completion",
			"Performing 0 Days", "Special Mention 1-30 Days", "Watchful 31-60 Days",
			"Substandard 61-90 Days", "Doubtful 91-180 Days", "Loss 181-365 Days", "Loss Over365 Days"
		};

				for (int i = 0; i < headers.Length; i++)
				{
					worksheet.Cell(currentRow, i + 1).Value = headers[i];
					worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
					worksheet.Cell(currentRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					worksheet.Cell(currentRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				}
				currentRow++;

				// Data
				int startDataRow = currentRow;
				foreach (var loan in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = loan.LoanNo;
					worksheet.Cell(currentRow, 2).Value = loan.MemberNo;
					worksheet.Cell(currentRow, 3).Value = loan.Name;
					worksheet.Cell(currentRow, 4).Value = loan.LoanBalance;
					worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 5).Value = loan.RepayPeriod;
					worksheet.Cell(currentRow, 6).Value = loan.DateIssued;
					worksheet.Cell(currentRow, 7).Value = loan.DaysInArrears;
					worksheet.Cell(currentRow, 8).Value = loan.LastRepayDate;
					worksheet.Cell(currentRow, 9).Value = loan.DateOfCompletion;
					worksheet.Cell(currentRow, 10).Value = loan.Performing;
					worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 11).Value = loan.SpecialMention;
					worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 12).Value = loan.Watchful;
					worksheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 13).Value = loan.Substandard;
					worksheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 14).Value = loan.Doubtful;
					worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 15).Value = loan.Loss;
					worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 16).Value = loan.LossOver365;
					worksheet.Cell(currentRow, 16).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Range(currentRow, 1, currentRow, 16).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}
				int endDataRow = currentRow - 1;

				// Totals Row
				currentRow++;
				worksheet.Cell(currentRow, 3).Value = "Total";
				worksheet.Cell(currentRow, 3).Style.Font.Bold = true;

				if (reportData.Any())
				{
					worksheet.Cell(currentRow, 4).FormulaA1 = $"SUM(D{startDataRow}:D{endDataRow})";
					worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 10).FormulaA1 = $"SUM(J{startDataRow}:J{endDataRow})";
					worksheet.Cell(currentRow, 10).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 11).FormulaA1 = $"SUM(K{startDataRow}:K{endDataRow})";
					worksheet.Cell(currentRow, 11).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 12).FormulaA1 = $"SUM(L{startDataRow}:L{endDataRow})";
					worksheet.Cell(currentRow, 12).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 12).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 13).FormulaA1 = $"SUM(M{startDataRow}:M{endDataRow})";
					worksheet.Cell(currentRow, 13).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 13).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 14).FormulaA1 = $"SUM(N{startDataRow}:N{endDataRow})";
					worksheet.Cell(currentRow, 14).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 15).FormulaA1 = $"SUM(O{startDataRow}:O{endDataRow})";
					worksheet.Cell(currentRow, 15).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Cell(currentRow, 16).FormulaA1 = $"SUM(P{startDataRow}:P{endDataRow})";
					worksheet.Cell(currentRow, 16).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 16).Style.NumberFormat.Format = "#,##0.00";
				}

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=AgingAnalysis_{asAtDate:yyyyMMdd}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"AgingAnalysis_{asAtDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportAgingAnalysisToPdf(DateTime asAtDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "JUHUDI SACCO";

			// Get all active/disbursed loans
			var loans = await _context.Loans
				.Include(l => l.Member)
				.Where(l => l.CompanyCode == companyCode
					&& l.Status == 6)
				.OrderBy(l => l.MemberNo)
				.ThenBy(l => l.LoanNo)
				.ToListAsync();

			var loanNos = loans.Select(l => l.LoanNo).ToList();

			// Get loan balances
			var loanBalances = await _context.Loanbals
				.Where(lb => loanNos.Contains(lb.LoanNo)
					&& lb.Companycode == companyCode)
				.ToDictionaryAsync(lb => lb.LoanNo, lb => lb);

			// Get last repayments
			var lastRepayments = await _context.Repays
				.Where(r => loanNos.Contains(r.LoanNo)
					&& r.CompanyCode == companyCode)
				.GroupBy(r => r.LoanNo)
				.Select(g => new
				{
					LoanNo = g.Key,
					LastRepayDate = g.Max(r => r.DateReceived)
				})
				.ToDictionaryAsync(g => g.LoanNo, g => g);

			var reportData = new List<dynamic>();

			foreach (var loan in loans)
			{
				decimal currentBalance = loan.LoanAmt ?? 0;
				DateTime? dueDate = null;
				DateTime? lastRepayDate = null;

				DateTime dateIssued = loan.AuditTime;

				if (loanBalances.ContainsKey(loan.LoanNo))
				{
					var lb = loanBalances[loan.LoanNo];
					currentBalance = lb.Balance;
					dueDate = lb.Duedate;
				}

				if (lastRepayments.ContainsKey(loan.LoanNo))
				{
					lastRepayDate = lastRepayments[loan.LoanNo].LastRepayDate;
				}

				int daysInArrears = 0;
				if (dueDate.HasValue && dueDate.Value < asAtDate)
				{
					daysInArrears = (asAtDate - dueDate.Value).Days;
				}

				int category = AgingCategories.GetCategoryFromDays(daysInArrears);

				string fullName = "";
				if (loan.Member != null)
				{
					if (loan.Member.FullName != null)
						fullName = loan.Member.FullName.ToString();
					else
						fullName = $"{loan.Member.Surname ?? ""} {loan.Member.OtherNames ?? ""}".Trim();
				}

				var loanData = new
				{
					loan.LoanNo,
					loan.MemberNo,
					Name = fullName,
					LoanBalance = currentBalance,
					RepayPeriod = dueDate,
					DateIssued = dateIssued,
					DaysInArrears = daysInArrears,
					LastRepayDate = lastRepayDate,
					DateOfCompletion = currentBalance == 0 ? asAtDate : (DateTime?)null,
					Performing = category == AgingCategories.PERFORMING ? currentBalance : 0,
					SpecialMention = category == AgingCategories.SPECIAL_MENTION ? currentBalance : 0,
					Watchful = category == AgingCategories.WATCHFUL ? currentBalance : 0,
					Substandard = category == AgingCategories.SUBSTANDARD ? currentBalance : 0,
					Doubtful = category == AgingCategories.DOUBTFUL ? currentBalance : 0,
					Loss = category == AgingCategories.LOSS ? currentBalance : 0,
					LossOver365 = category == AgingCategories.LOSS_OVER_365 ? currentBalance : 0
				};

				reportData.Add(loanData);
			}

			using (var stream = new MemoryStream())
			{
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4.Rotate());

				// Set margins
				document.SetMargins(20, 20, 20, 20);

				var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				// Company Name - Large and Bold
				document.Add(new Paragraph(companyName.ToUpper())
					.SetFont(boldFont)
					.SetFontSize(18)
					.SetTextAlignment(TextAlignment.CENTER));

				// Title
				document.Add(new Paragraph("AGING ANALYSIS")
					.SetFont(boldFont)
					.SetFontSize(16)
					.SetTextAlignment(TextAlignment.CENTER));

				// As At Date
				document.Add(new Paragraph($"As At: {asAtDate:dd/MM/yyyy}")
					.SetFont(normalFont)
					.SetFontSize(12)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Create table with 16 columns - set specific column widths
				float[] columnWidths = { 4f, 4f, 5f, 3.5f, 3f, 3f, 2.5f, 3f, 3f, 3f, 3f, 3f, 3f, 3f, 3f, 3f };
				var table = new Table(columnWidths);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// First row headers - Main categories
				table.AddHeaderCell(new Cell(1, 3).Add(new Paragraph("Loan Details"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Balance"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Due Date"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Issued"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Days"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Last Repay"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Completed"))
					.SetFont(boldFont)
					.SetFontSize(9)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Perf 0"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Spec 1-30"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Watch 31-60"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Sub 61-90"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Doubt 91-180"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Loss 181-365"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell(1, 1).Add(new Paragraph("Loss >365"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				// Second row headers - Sub-categories
				table.AddHeaderCell(new Cell().Add(new Paragraph("LoanNo"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("MemberNo"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("Name"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("Amount"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph(""))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph(""))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("In Arrears"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("Date"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				table.AddHeaderCell(new Cell().Add(new Paragraph("Date"))
					.SetFont(boldFont)
					.SetFontSize(8)
					.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
					.SetTextAlignment(TextAlignment.CENTER));

				for (int i = 0; i < 8; i++)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(""))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data rows
				foreach (var loan in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(loan.LoanNo ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(loan.MemberNo ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(loan.Name ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.LoanBalance))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(loan.RepayPeriod?.ToString("dd/MM/yy") ?? "")).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(loan.DateIssued.ToString("dd/MM/yy"))).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(loan.DaysInArrears.ToString())).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(loan.LastRepayDate?.ToString("dd/MM/yy") ?? "")).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(loan.DateOfCompletion?.ToString("dd/MM/yy") ?? "")).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Performing))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.SpecialMention))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Watchful))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Substandard))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Doubtful))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.Loss))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", loan.LossOver365))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
				}

				// Totals row
				table.AddCell(new Cell(1, 3).Add(new Paragraph("TOTAL")).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.LoanBalance)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph("")).SetFontSize(7));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.Performing)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.SpecialMention)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.Watchful)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.Substandard)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.Doubtful)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.Loss)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(l => (decimal)l.LossOver365)))).SetFont(boldFont).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));

				document.Add(table);

				// Footer
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=AgingAnalysis_{asAtDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"AgingAnalysis_{asAtDate:yyyyMMdd}.pdf");
			}
		}


	}
}