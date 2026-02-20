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
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;

namespace SACCOBlockChainSystem.Controllers
{
	[Authorize]
	public class MemberReportController : Controller
	{
		private readonly ApplicationDbContext _context;
		 

		public MemberReportController(ApplicationDbContext context)
		{
			_context = context;
		}

		// Active Members Report
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

			// Get active members (not withdrawn, not archived, status active)
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false)
					&& (m.Status == 1 || m.Status == null))
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

			var reportData = new List<MemberReportViewModel>();

			foreach (var m in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

				int? age = null;
				if (m.Dob.HasValue)
				{
					age = DateTime.Now.Year - m.Dob.Value.Year;
					if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
				}

				// Handle FullName safely
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

				// Handle sex mapping
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
					ShareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0,
					SavingsDeposits = memberContrib?.TotalDeposits ?? 0,
					RegFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0,
					LoanBalance = m.LoanBalance ?? 0,
					PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
					Email = m.Email ?? m.EmailAddress,
					Station = m.Station ?? "-",
					Status = "ACTIVE"
				});
			}

			// Calculate statistics
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

			return View("~/Views/Reports/ActiveMembers.cshtml", viewModel);
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

			var report = members.Select(m =>
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

			string[] headers =
				{ "MemberNo", "Names", "Sex", "Share Capital", "Deposits", "Reg Fee" };

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

			var report = members.Select(m =>
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

				return new
				{
					m.MemberNo,
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
			var pdf = new PdfDocument(new PdfWriter(stream));
			var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4.Rotate());

			var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
			var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

			doc.Add(new Paragraph(companyName.ToUpper())
				.SetFont(bold).SetFontSize(18)
				.SetTextAlignment(TextAlignment.CENTER));

			doc.Add(new Paragraph($"ACTIVE MEMBERS AS AT {reportDate:dd/MM/yyyy}")
				.SetFont(bold).SetTextAlignment(TextAlignment.CENTER));

			doc.Add(new Paragraph("\n"));

			var stats = new Table(4).UseAllAvailableWidth();
			stats.AddCell(new Paragraph($"TOTAL: {report.Count}").SetFont(bold));
			stats.AddCell(new Paragraph($"MALE: {male}").SetFont(bold));
			stats.AddCell(new Paragraph($"FEMALE: {female}").SetFont(bold));
			stats.AddCell(new Paragraph($"GENERATED: {DateTime.Now:dd/MM/yyyy}").SetFont(normal));
			doc.Add(stats);

			doc.Add(new Paragraph("\n"));

			var table = new Table(6).UseAllAvailableWidth();
			string[] headers =
				{ "MemberNo", "Names", "Sex", "Share", "Deposits", "Reg Fee" };

			foreach (var h in headers)
				table.AddHeaderCell(new Paragraph(h).SetFont(bold).SetFontSize(9));

			foreach (var m in report)
			{
				table.AddCell(new Paragraph(m.MemberNo ?? "").SetFontSize(8));
				table.AddCell(new Paragraph(m.Name).SetFontSize(8));
				table.AddCell(new Paragraph(m.Sex).SetFontSize(8));
				table.AddCell(new Paragraph($"{m.Share:N0}").SetFontSize(8));
				table.AddCell(new Paragraph($"{m.Deposits:N0}").SetFontSize(8));
				table.AddCell(new Paragraph($"{m.RegFee:N0}").SetFontSize(8));
			}

			doc.Add(table);

			doc.Close();

			return File(stream.ToArray(),
				"application/pdf",
				$"ActiveMembers_{reportDate:yyyyMMdd}.pdf");
		}


		// Inactive Members Report
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

			// Get inactive members (withdrawn, archived, or status inactive)
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

			var reportData = new List<MemberReportViewModel>();

			foreach (var m in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

				int? age = null;
				if (m.Dob.HasValue)
				{
					age = DateTime.Now.Year - m.Dob.Value.Year;
					if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
				}

				// Handle FullName safely
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

				// Handle sex mapping
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
					ShareCapital = memberContrib?.TotalShareCapital ?? m.ShareCap ?? 0,
					SavingsDeposits = memberContrib?.TotalDeposits ?? 0,
					RegFee = memberContrib?.TotalRegFee ?? m.RegFee ?? 0,
					LoanBalance = m.LoanBalance ?? 0,
					PhoneNo = m.PhoneNo ?? m.MobileNo ?? "-",
					Email = m.Email ?? m.EmailAddress,
					Station = m.Station ?? "-",
					Status = status
				});
			}

			// Calculate statistics
			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE"
												&& !string.IsNullOrEmpty(m.Sex) && m.Sex != "NOT SPECIFIED");

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

			return View("~/Views/Reports/InactiveMembers.cshtml", viewModel);
		}

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

				// Handle FullName safely
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

				// Handle sex mapping
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

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title
				worksheet.Cell(currentRow, 1).Value = $"INACTIVE SACCO MEMBERS AS AT {reportDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Statistics Section
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

				// Headers
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

				// Data
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

				// Grand Total
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

				// Report Generation Date
				currentRow += 3;
				worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=InactiveMembers_{reportDate:yyyyMMdd}.xlsx");
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

			var reportData = new List<dynamic>();

			foreach (var m in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == m.MemberNo);

				// Handle FullName safely
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

				// Handle sex mapping
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

			using (var stream = new MemoryStream())
			{
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				// Use PageSize.A4.Rotate() for landscape
				var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4.Rotate());

				var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				// Title
				document.Add(new Paragraph(companyName.ToUpper())
					.SetFont(boldFont)
					.SetFontSize(18)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph($"INACTIVE MEMBERS AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Statistics
				var statsTable = new Table(6);
				statsTable.SetWidth(UnitValue.CreatePercentValue(100));

				statsTable.AddCell(new Cell().Add(new Paragraph("TOTAL MEMBERS:")).SetFont(boldFont));
				statsTable.AddCell(new Cell().Add(new Paragraph(reportData.Count.ToString())).SetFont(boldFont));
				statsTable.AddCell(new Cell().Add(new Paragraph("MALE:")).SetFont(boldFont));
				statsTable.AddCell(new Cell().Add(new Paragraph(maleCount.ToString())).SetFont(boldFont));
				statsTable.AddCell(new Cell().Add(new Paragraph("FEMALE:")).SetFont(boldFont));
				statsTable.AddCell(new Cell().Add(new Paragraph(femaleCount.ToString())).SetFont(boldFont));

				document.Add(statsTable);
				document.Add(new Paragraph("\n"));

				// Members Table
				var table = new Table(7);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers
				var headers = new[] { "MemberNo", "Names", "Sex", "Share Capital", "Savings/Deposits", "Reg Fee", "Status" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(9)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				foreach (var member in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.FullName ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.Sex ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.ShareCapital))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.SavingsDeposits))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.RegFee))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(member.Status ?? "")).SetFontSize(8));
				}

				document.Add(table);

				// Footer
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=InactiveMembers_{reportDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"InactiveMembers_{reportDate:yyyyMMdd}.pdf");
			}
		}

		// Members Per SACCO Report
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

			// Get ALL members registered in the system
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode)
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var reportData = new List<MemberPerSaccoReportVM>();

			foreach (var m in members)
			{
				// Handle FullName safely
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

				// Handle sex mapping
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

				// Calculate age
				int? age = null;
				if (m.Dob.HasValue)
				{
					age = DateTime.Now.Year - m.Dob.Value.Year;
					if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
				}

				// Determine status
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

			// Calculate statistics
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
		[ValidateAntiForgeryToken]
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
					// Handle FullName safely
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

					// Handle sex mapping
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

					// Calculate age
					int? age = null;
					if (m.Dob.HasValue)
					{
						age = DateTime.Now.Year - m.Dob.Value.Year;
						if (DateTime.Now < m.Dob.Value.AddYears(age.Value)) age--;
					}

					// Determine status
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
				// FIX: Use != null instead of HasValue for dynamic object
				int youthCount = reportData.Count(m => m.Age != null && m.Age >= 18 && m.Age <= 35);

				using (var workbook = new XLWorkbook())
				{
					var worksheet = workbook.Worksheets.Add("Members Per SACCO");
					var currentRow = 1;

					// SACCO Name Header
					worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
					worksheet.Range(currentRow, 1, currentRow, 6).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					currentRow += 2;

					// Report Date
					worksheet.Cell(currentRow, 1).Value = $"AS AT {reportDate:dd/MM/yyyy}";
					worksheet.Range(currentRow, 1, currentRow, 6).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
					currentRow += 2;

					// Statistics Section
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

					// Table Headers
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

					// Data Rows
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

					// Grand Total Row
					currentRow += 2;
					worksheet.Cell(currentRow, 1).Value = "GRAND TOTAL MEMBERS:";
					worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 2).Value = reportData.Count;
					worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

					// Report Generation Date
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
				// Log error
				TempData["Error"] = "Failed to export Excel: " + ex.Message;
				return RedirectToAction("MembersPerSacco", new { reportDate });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ExportMembersPerSaccoToPdf(DateTime reportDate)
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

				int maleCount = reportData.Count(x => x.Sex == "MALE");
				int femaleCount = reportData.Count(x => x.Sex == "FEMALE");
				// FIX: Use != null instead of HasValue for dynamic object
				int youthCount = reportData.Count(x => x.Age != null && x.Age >= 18 && x.Age <= 35);

				using var stream = new MemoryStream();
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

				var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				doc.Add(new Paragraph(companyName.ToUpper())
					.SetFont(bold).SetFontSize(18).SetTextAlignment(TextAlignment.CENTER));

				doc.Add(new Paragraph($"MEMBERS REGISTER AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(bold).SetTextAlignment(TextAlignment.CENTER));

				doc.Add(new Paragraph("\n"));

				var stats = new Table(2).UseAllAvailableWidth();
				stats.AddCell(new Cell().Add(new Paragraph($"TOTAL: {reportData.Count}")).SetFont(bold));
				stats.AddCell(new Cell().Add(new Paragraph($"MALE: {maleCount}")).SetFont(bold));
				stats.AddCell(new Cell().Add(new Paragraph($"FEMALE: {femaleCount}")).SetFont(bold));
				stats.AddCell(new Cell().Add(new Paragraph($"YOUTH: {youthCount}")).SetFont(bold));
				doc.Add(stats);

				doc.Add(new Paragraph("\n"));

				var table = new Table(7).UseAllAvailableWidth();
				string[] headers =
				{
			"MemberNo","Names","Sex","Phone","ID","Date","Status"
		};

				foreach (var h in headers)
					table.AddHeaderCell(new Cell().Add(new Paragraph(h))
						.SetFont(bold).SetFontSize(9));

				foreach (var m in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(m.MemberNo ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.FullName)).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.Sex)).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.PhoneNo)).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.IDNo)).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.ApplicDate?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(m.Status)).SetFontSize(8));
				}

				doc.Add(table);

				doc.Add(new Paragraph($"\nGenerated: {DateTime.Now}")
					.SetFont(normal).SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				doc.Close();

				return File(stream.ToArray(),
					"application/pdf",
					$"MembersPerSacco_{reportDate:yyyyMMdd}.pdf");
			}
			catch (Exception ex)
			{
				// Log error
				TempData["Error"] = "Failed to export PDF: " + ex.Message;
				return RedirectToAction("MembersPerSacco", new { reportDate });
			}
		}


		//Fully Paid Shares Report

		public IActionResult FullyPaidShares()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new FullyPaidSharesIndexViewModel
			{
				Members = new List<FullyPaidSharesReportViewModel>(),
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
				MinimumShareRequirement = 0, // Default value
				RegistrationFeeRequirement = 0 // Default registration fee
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.HasData = false;

			return View("~/Views/Reports/FullyPaidShares.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> FullyPaidShares(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get the minimum share requirement from Sharetype where IsMainShares = true
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0; // Default to 1000 if not set
			decimal registrationFeeRequirement = 0; // Default registration fee - you can adjust this or get from settings

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			// Get share capital, deposits, and registration fee from ContribShare
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

			// Get shares from Share table as fallback
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

			var reportData = new List<FullyPaidSharesReportViewModel>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				// Calculate total share capital from multiple sources
				decimal shareCapital = 0;

				if (memberContrib != null)
				{
					shareCapital = memberContrib.TotalShareCapital;
				}
				else if (memberShare != null)
				{
					shareCapital = memberShare.TotalShares;
				}
				else
				{
					shareCapital = member.ShareCap ?? 0;
				}

				// Get savings/deposits and registration fee
				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Check if member is fully paid
				bool isShareCapitalFullyPaid = shareCapital >= minimumShareRequirement;
				bool isRegistrationFeePaid = registrationFee >= registrationFeeRequirement;
				bool hasSavingsDeposits = savingsDeposits > 0;

				// Only include members who have met ALL requirements:
				// 1. Paid minimum share capital
				// 2. Paid registration fee
				// 3. Has savings/deposits
				if (isShareCapitalFullyPaid && isRegistrationFeePaid && hasSavingsDeposits)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
					{
						fullName = member.FullName.ToString();
					}
					else
					{
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
						if (string.IsNullOrWhiteSpace(fullName))
							fullName = "N/A";
					}

					// Handle sex mapping
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

					reportData.Add(new FullyPaidSharesReportViewModel
					{
						MemberNo = member.MemberNo,
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee,
						IsFullyPaid = true,
						MinimumShareRequirement = minimumShareRequirement,
						MinimumSavingsRequirement = 0,
						RegistrationFeeRequirement = registrationFeeRequirement
					});
				}
			}

			// Order by MemberNo
			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			// Calculate statistics
			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

			var viewModel = new FullyPaidSharesIndexViewModel
			{
				Members = reportData,
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				OtherCount = otherCount,
				TotalShareCapital = reportData.Sum(m => m.ShareCapital),
				TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits),
				TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee),
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName,
				MinimumShareRequirement = minimumShareRequirement,
				RegistrationFeeRequirement = registrationFeeRequirement
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital);
			ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits);
			ViewBag.TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee);
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.OtherCount = otherCount;
			ViewBag.MinimumShareRequirement = minimumShareRequirement;
			ViewBag.RegistrationFeeRequirement = registrationFeeRequirement;
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/FullyPaidShares.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportFullyPaidSharesToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get the minimum share requirement
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
			decimal registrationFeeRequirement = 0;

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Only include fully paid members
				if (shareCapital >= minimumShareRequirement && registrationFee >= registrationFeeRequirement && savingsDeposits > 0)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
						fullName = member.FullName.ToString();
					else
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

					// Handle sex mapping
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
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee
					});
				}
			}

			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Fully Paid Shares");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title
				worksheet.Cell(currentRow, 1).Value = $"FULLY PAID SACCO SHARES AS AT {reportDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Headers - Exactly as in the image
				var headers = new[] { "#", "MEMBERNO", "NAMES", "SEX", "SHARE CAPITAL", "SAVINGS/DEPOSITS", "REGISTRATION FEE" };

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
				int serialNo = 1;
				foreach (var member in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = serialNo++;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

					worksheet.Cell(currentRow, 2).Value = member.MemberNo;
					worksheet.Cell(currentRow, 3).Value = member.FullName;
					worksheet.Cell(currentRow, 4).Value = member.Sex;
					worksheet.Cell(currentRow, 5).Value = member.ShareCapital;
					worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 6).Value = member.SavingsDeposits;
					worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 7).Value = member.RegistrationFee;
					worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				// Grand Total Row - Exactly as in the image
				currentRow += 2;
				worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
				worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

				worksheet.Cell(currentRow, 5).Value = reportData.Sum(m => (decimal)m.ShareCapital);
				worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.SavingsDeposits);
				worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.RegistrationFee);
				worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

				// Statistics Section - Exactly as in the image
				currentRow += 3;
				worksheet.Cell(currentRow, 1).Value = "TOTAL MEMBERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "MALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "MALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "FEMALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "FEMALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "OTHERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				// Requirements Summary
				currentRow += 2;
				worksheet.Cell(currentRow, 1).Value = "Minimum Share Requirement:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = minimumShareRequirement.ToString("N0");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "Registration Fee Requirement:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = registrationFeeRequirement.ToString("N0");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				// Report Generation Date
				currentRow += 2;
				worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=FullyPaidShares_{reportDate:yyyyMMdd}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"FullyPaidShares_{reportDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportFullyPaidSharesToPdf(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get the minimum share requirement
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0;
			decimal registrationFeeRequirement = 0;

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Only include fully paid members
				if (shareCapital >= minimumShareRequirement && registrationFee >= registrationFeeRequirement && savingsDeposits > 0)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
						fullName = member.FullName.ToString();
					else
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

					// Handle sex mapping
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
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee
					});
				}
			}

			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

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

				// Title
				document.Add(new Paragraph($"FULLY PAID SACCO SHARES AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Members Table
				var table = new Table(7);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers
				var headers = new[] { "#", "MEMBERNO", "NAMES", "SEX", "SHARE CAPITAL", "SAVINGS/DEPOSITS", "REG FEE" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(9)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				int serialNo = 1;
				foreach (var member in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(serialNo++.ToString())).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.FullName ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.Sex ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.ShareCapital))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.SavingsDeposits))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.RegistrationFee))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				}

				document.Add(table);

				// Grand Total
				document.Add(new Paragraph("\n"));
				var grandTotalTable = new Table(4);
				grandTotalTable.SetWidth(UnitValue.CreatePercentValue(50));
				grandTotalTable.SetHorizontalAlignment(HorizontalAlignment.RIGHT);

				grandTotalTable.AddCell(new Cell().Add(new Paragraph("GRAND TOTAL:")).SetFont(boldFont).SetBorder(null));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.ShareCapital)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.SavingsDeposits)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.RegistrationFee)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));

				document.Add(grandTotalTable);

				// Statistics
				document.Add(new Paragraph("\n"));
				var statsTable = new Table(2);
				statsTable.SetWidth(UnitValue.CreatePercentValue(30));

				statsTable.AddCell(new Cell().Add(new Paragraph("TOTAL MEMBERS:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(reportData.Count.ToString())).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("MALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(maleCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("FEMALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(femaleCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("OTHERS:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(otherCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				document.Add(statsTable);

				// Footer
				 
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=FullyPaidShares_{reportDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"FullyPaidShares_{reportDate:yyyyMMdd}.pdf");
			}
		}
		//Partially Paid Shares Report

		public IActionResult PartiallyPaidShares()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			var viewModel = new PartiallyPaidSharesIndexViewModel
			{
				Members = new List<PartiallyPaidSharesReportViewModel>(),
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
				MembersMissingShareCapital = 0,
				MembersMissingRegistrationFee = 0,
				MembersMissingSavings = 0
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.HasData = false;

			return View("~/Views/Reports/PartiallyPaidShares.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> PartiallyPaidShares(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get the minimum share requirement from Sharetype where IsMainShares = true
			// Default to 0 if not set - this value is used ONLY for logic, NOT displayed
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0; // Default to 0 if not set
			decimal registrationFeeRequirement = 0; // Default registration fee - used ONLY for logic

			// Get ALL active members - not withdrawn, not archived
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			// Get share capital, deposits, and registration fee from ContribShare
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

			// Get shares from Share table as fallback
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

			var reportData = new List<PartiallyPaidSharesReportViewModel>();
			int missingShareCapitalCount = 0;
			int missingRegistrationFeeCount = 0;
			int missingSavingsCount = 0;

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				// Calculate total share capital from multiple sources
				decimal shareCapital = 0;

				if (memberContrib != null)
				{
					shareCapital = memberContrib.TotalShareCapital;
				}
				else if (memberShare != null)
				{
					shareCapital = memberShare.TotalShares;
				}
				else
				{
					shareCapital = member.ShareCap ?? 0;
				}

				// Get savings/deposits and registration fee
				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Check if requirements are met (using minimum requirement values, default 0 if not set)
				bool hasPaidShareCapital = minimumShareRequirement == 0 ? true : shareCapital >= minimumShareRequirement;
				bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : registrationFee >= registrationFeeRequirement;
				bool hasSavingsDeposits = savingsDeposits > 0;

				// Determine what's missing
				bool missingShareCapital = !hasPaidShareCapital && minimumShareRequirement > 0;
				bool missingRegistrationFee = !hasPaidRegistrationFee && registrationFeeRequirement > 0;
				bool missingSavings = !hasSavingsDeposits;

				// Count missing requirements
				if (missingShareCapital) missingShareCapitalCount++;
				if (missingRegistrationFee) missingRegistrationFeeCount++;
				if (missingSavings) missingSavingsCount++;

				// Include ALL members who have NOT paid ALL three requirements
				// This includes members who have paid NONE, ONE, or TWO requirements
				if (!hasPaidShareCapital || !hasPaidRegistrationFee || !hasSavingsDeposits)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
					{
						fullName = member.FullName.ToString();
					}
					else
					{
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
						if (string.IsNullOrWhiteSpace(fullName))
							fullName = "N/A";
					}

					// Handle sex mapping
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

					reportData.Add(new PartiallyPaidSharesReportViewModel
					{
						MemberNo = member.MemberNo,
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee,
						HasPaidShareCapital = hasPaidShareCapital,
						HasPaidRegistrationFee = hasPaidRegistrationFee,
						HasSavingsDeposits = hasSavingsDeposits,
						MissingShareCapital = missingShareCapital,
						MissingRegistrationFee = missingRegistrationFee,
						MissingSavingsDeposits = missingSavings
					});
				}
			}

			// Order by MemberNo
			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			// Calculate statistics
			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

			var viewModel = new PartiallyPaidSharesIndexViewModel
			{
				Members = reportData,
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				OtherCount = otherCount,
				TotalShareCapital = reportData.Sum(m => m.ShareCapital),
				TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits),
				TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee),
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName,
				MembersMissingShareCapital = missingShareCapitalCount,
				MembersMissingRegistrationFee = missingRegistrationFeeCount,
				MembersMissingSavings = missingSavingsCount
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital);
			ViewBag.TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits);
			ViewBag.TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee);
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.OtherCount = otherCount;
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/PartiallyPaidShares.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportPartiallyPaidSharesToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get the minimum share requirement - used ONLY for logic, NOT displayed
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0; // Default to 0 if not set
			decimal registrationFeeRequirement = 0; // Default registration fee - used ONLY for logic

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Check if requirements are met
				bool hasPaidShareCapital = minimumShareRequirement == 0 ? true : shareCapital >= minimumShareRequirement;
				bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : registrationFee >= registrationFeeRequirement;
				bool hasSavingsDeposits = savingsDeposits > 0;

				// Include ALL members who have NOT paid ALL three requirements
				if (!hasPaidShareCapital || !hasPaidRegistrationFee || !hasSavingsDeposits)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
						fullName = member.FullName.ToString();
					else
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

					// Handle sex mapping
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
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee
					});
				}
			}

			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Partially Paid Shares");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title
				worksheet.Cell(currentRow, 1).Value = $"PARTIALLY PAID SACCO SHARES AS AT {reportDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 6).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Headers - Exactly as in the image - NO requirement values displayed
				var headers = new[] { "#", "MEMBERNO", "NAMES", "SEX", "SHARE CAPITAL", "SAVINGS/DEPOSITS", "REGISTRATION FEE" };

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
				int serialNo = 1;
				foreach (var member in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = serialNo++;
					worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

					worksheet.Cell(currentRow, 2).Value = member.MemberNo;
					worksheet.Cell(currentRow, 3).Value = member.FullName;
					worksheet.Cell(currentRow, 4).Value = member.Sex;
					worksheet.Cell(currentRow, 5).Value = member.ShareCapital;
					worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 6).Value = member.SavingsDeposits;
					worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 7).Value = member.RegistrationFee;
					worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Range(currentRow, 1, currentRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				// Grand Total Row - Exactly as in the image
				currentRow += 2;
				worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
				worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

				worksheet.Cell(currentRow, 5).Value = reportData.Sum(m => (decimal)m.ShareCapital);
				worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.SavingsDeposits);
				worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.RegistrationFee);
				worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

				// Statistics Section - Exactly as in the image - NO requirement values displayed
				currentRow += 3;
				worksheet.Cell(currentRow, 1).Value = "TOTAL MEMBERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "MALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "MALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "FEMALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "FEMALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "OTHERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";

				// Report Generation Date
				currentRow += 2;
				worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 7).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=PartiallyPaidShares_{reportDate:yyyyMMdd}.xlsx");
					return File(content,
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

			// Get the minimum share requirement - used ONLY for logic, NOT displayed
			var mainShareType = await _context.Sharetypes
				.Where(st => st.CompanyCode == companyCode && st.IsMainShares == true)
				.FirstOrDefaultAsync();

			decimal minimumShareRequirement = mainShareType?.MinAmount ?? 0; // Default to 0 if not set
			decimal registrationFeeRequirement = 0; // Default registration fee - used ONLY for logic

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;

				// Check if requirements are met
				bool hasPaidShareCapital = minimumShareRequirement == 0 ? true : shareCapital >= minimumShareRequirement;
				bool hasPaidRegistrationFee = registrationFeeRequirement == 0 ? true : registrationFee >= registrationFeeRequirement;
				bool hasSavingsDeposits = savingsDeposits > 0;

				// Include ALL members who have NOT paid ALL three requirements
				if (!hasPaidShareCapital || !hasPaidRegistrationFee || !hasSavingsDeposits)
				{
					// Handle FullName safely
					string fullName = "";
					if (member.FullName != null)
						fullName = member.FullName.ToString();
					else
						fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

					// Handle sex mapping
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
						FullName = fullName,
						Sex = sex,
						ShareCapital = shareCapital,
						SavingsDeposits = savingsDeposits,
						RegistrationFee = registrationFee
					});
				}
			}

			reportData = reportData.OrderBy(m => m.MemberNo).ToList();

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

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

				// Title - NO requirement values displayed
				document.Add(new Paragraph($"PARTIALLY PAID SACCO SHARES AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Members Table
				var table = new Table(7);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers - NO requirement values displayed
				var headers = new[] { "#", "MEMBERNO", "NAMES", "SEX", "SHARE CAPITAL", "SAVINGS/DEPOSITS", "REG FEE" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(9)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				int serialNo = 1;
				foreach (var member in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(serialNo++.ToString())).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.FullName ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.Sex ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.ShareCapital))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.SavingsDeposits))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.RegistrationFee))).SetFontSize(8).SetTextAlignment(TextAlignment.RIGHT));
				}

				document.Add(table);

				// Grand Total
				document.Add(new Paragraph("\n"));
				var grandTotalTable = new Table(4);
				grandTotalTable.SetWidth(UnitValue.CreatePercentValue(50));
				grandTotalTable.SetHorizontalAlignment(HorizontalAlignment.RIGHT);

				grandTotalTable.AddCell(new Cell().Add(new Paragraph("GRAND TOTAL:")).SetFont(boldFont).SetBorder(null));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.ShareCapital)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.SavingsDeposits)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.RegistrationFee)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));

				document.Add(grandTotalTable);

				// Statistics - NO requirement values displayed
				document.Add(new Paragraph("\n"));
				var statsTable = new Table(2);
				statsTable.SetWidth(UnitValue.CreatePercentValue(30));

				statsTable.AddCell(new Cell().Add(new Paragraph("TOTAL MEMBERS:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(reportData.Count.ToString())).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("MALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(maleCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("FEMALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(femaleCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("OTHERS:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(otherCount.ToString("N0"))).SetFont(boldFont).SetBorder(null));

				document.Add(statsTable);

				// Footer - NO requirement values displayed
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=PartiallyPaidShares_{reportDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"PartiallyPaidShares_{reportDate:yyyyMMdd}.pdf");
			}
		}

		//Shares and Loans Report
			public IActionResult SharesAndLoans()
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
				TotalOutstandingBalance = 0,
				MembersByCIG = new Dictionary<string, int>(),
				ShareCapitalByCIG = new Dictionary<string, decimal>()
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.HasData = false;

			return View("~/Views/Reports/SharesLoansPERSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> SharesAndLoans(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get ALL active members (not withdrawn, not archived)
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
				.OrderBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = members.Select(m => m.MemberNo).ToList();

			// Get share capital, deposits, reg fee, and passbook from ContribShare
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

			// Get shares from Share table as fallback for share capital
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

			// Get all loans for members
			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo)
					&& l.CompanyCode == companyCode)
				.Select(l => new
				{
					l.MemberNo,
					l.LoanNo,
					l.LoanAmt,
					l.Status
				})
				.ToListAsync();

			// Get latest loan balances from Repay table
			var loanNos = loans.Select(l => l.LoanNo).ToList();
			var loanBalances = await _context.Repays
				.Where(r => loanNos.Contains(r.LoanNo)
					&& r.CompanyCode == companyCode)
				.GroupBy(r => r.LoanNo)
				.Select(g => new
				{
					LoanNo = g.Key,
					LatestBalance = g.OrderByDescending(r => r.DateReceived)
									.Select(r => r.LoanBalance)
									.FirstOrDefault() ?? 0
				})
				.ToDictionaryAsync(g => g.LoanNo, g => g.LatestBalance);

			var reportData = new List<SharesAndLoansReportViewModel>();
			var cigCounts = new Dictionary<string, int>();
			var cigShareCapital = new Dictionary<string, decimal>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				// Calculate share capital from multiple sources
				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				// Get deposits, reg fee, and passbook
				decimal deposits = memberContrib?.TotalDeposits ?? 0;
				decimal regFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbook = memberContrib?.TotalPassbook ?? 0;

				// Calculate total loans and outstanding balance for this member
				var memberLoans = loans.Where(l => l.MemberNo == member.MemberNo).ToList();
				decimal totalLoans = memberLoans.Sum(l => l.LoanAmt ?? 0);

				decimal outstandingBalance = 0;
				foreach (var loan in memberLoans)
				{
					if (loanBalances.ContainsKey(loan.LoanNo))
					{
						outstandingBalance += loanBalances[loan.LoanNo];
					}
					else
					{
						// If no repayments found, full loan amount is outstanding
						outstandingBalance += loan.LoanAmt ?? 0;
					}
				}

				// Calculate age from DOB
				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
				}

				// Determine CIG/Group name (using Station, Province, District, or default)
				string cigName = "INDIVIDUAL";
				if (!string.IsNullOrEmpty(member.Station))
					cigName = member.Station.ToUpper();
				else if (!string.IsNullOrEmpty(member.Province))
					cigName = member.Province.ToUpper();
				else if (!string.IsNullOrEmpty(member.District))
					cigName = member.District.ToUpper();

				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
				{
					fullName = member.FullName.ToString();
				}
				else
				{
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				// Handle sex mapping
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

				// Track CIG statistics
				if (cigCounts.ContainsKey(cigName))
					cigCounts[cigName]++;
				else
					cigCounts[cigName] = 1;

				if (cigShareCapital.ContainsKey(cigName))
					cigShareCapital[cigName] += shareCapital;
				else
					cigShareCapital[cigName] = shareCapital;

				reportData.Add(new SharesAndLoansReportViewModel
				{
					MemberNo = member.MemberNo,
					FullName = fullName,
					Age = age,
					CIGName = cigName,
					ShareCapital = shareCapital,
					Deposits = deposits,
					RegFee = regFee,
					Passbook = passbook,
					TotalLoans = totalLoans,
					OutstandingBalance = outstandingBalance,
					ActiveLoansCount = memberLoans.Count(l => l.Status != 6 && l.Status != 7 && l.Status != 8), // Not completed, rejected, cancelled
					DateRegistered = member.ApplicDate ?? member.EffectDate ?? member.AuditTime
				});
			}

			 
			var viewModel = new SharesAndLoansIndexViewModel
			{
				Members = reportData,
				TotalMembers = reportData.Count,
				TotalShareCapital = reportData.Sum(m => m.ShareCapital),
				TotalDeposits = reportData.Sum(m => m.Deposits),
				TotalRegFee = reportData.Sum(m => m.RegFee),
				TotalPassbook = reportData.Sum(m => m.Passbook),
				TotalLoans = reportData.Sum(m => m.TotalLoans),
				TotalOutstandingBalance = reportData.Sum(m => m.OutstandingBalance),
				MembersByCIG = cigCounts,
				ShareCapitalByCIG = cigShareCapital,
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.ReportDate = reportDate;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.TotalShareCapital = reportData.Sum(m => m.ShareCapital);
			ViewBag.TotalDeposits = reportData.Sum(m => m.Deposits);
			ViewBag.TotalRegFee = reportData.Sum(m => m.RegFee);
			ViewBag.TotalPassbook = reportData.Sum(m => m.Passbook);
			ViewBag.TotalLoans = reportData.Sum(m => m.TotalLoans);
			ViewBag.HasData = reportData.Any();

			return View("~/Views/Reports/SharesLoansPERSacco.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportSharesAndLoansToExcel(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			// Get shares
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

			// Get loans
			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo)
					&& l.CompanyCode == companyCode)
				.Select(l => new
				{
					l.MemberNo,
					l.LoanAmt
				})
				.ToListAsync();

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal deposits = memberContrib?.TotalDeposits ?? 0;
				decimal regFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbook = memberContrib?.TotalPassbook ?? 0;

				decimal totalLoans = loans.Where(l => l.MemberNo == member.MemberNo).Sum(l => l.LoanAmt ?? 0);

				// Calculate age
				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
				}

				// Determine CIG name
				string cigName = "INDIVIDUAL";
				if (!string.IsNullOrEmpty(member.Station))
					cigName = member.Station.ToUpper();
				else if (!string.IsNullOrEmpty(member.Province))
					cigName = member.Province.ToUpper();
				else if (!string.IsNullOrEmpty(member.District))
					cigName = member.District.ToUpper();

				// Handle FullName
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				reportData.Add(new
				{
					member.MemberNo,
					Names = fullName,
					Age = age.HasValue ? age.Value.ToString() : "-",
					CIGName = cigName,
					ShareCapital = shareCapital,
					Deposits = deposits,
					RegFee = regFee,
					Passbook = passbook,
					Loans = totalLoans,
					DateRegistered = member.ApplicDate ?? member.EffectDate ?? member.AuditTime
				});
			}

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Shares and Loans");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 10).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title
				worksheet.Cell(currentRow, 1).Value = $"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 10).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Headers - Exactly as in the image
				var headers = new[] { "MemberNo", "Names", "Age", "CIGName", "Share Capital", "Deposits", "Regfee", "passbook", "Loans", "Date Registered" };

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
				foreach (var member in reportData.OrderBy(m => m.MemberNo))
				{
					worksheet.Cell(currentRow, 1).Value = member.MemberNo;
					worksheet.Cell(currentRow, 2).Value = member.Names;
					worksheet.Cell(currentRow, 3).Value = member.Age;
					worksheet.Cell(currentRow, 4).Value = member.CIGName;
					worksheet.Cell(currentRow, 5).Value = member.ShareCapital;
					worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 6).Value = member.Deposits;
					worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 7).Value = member.RegFee;
					worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 8).Value = member.Passbook;
					worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 9).Value = member.Loans;
					worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
					worksheet.Cell(currentRow, 10).Value = member.DateRegistered?.ToString("dd/MM/yyyy");

					worksheet.Range(currentRow, 1, currentRow, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				// Grand Total Row - Exactly as in the image
				currentRow += 2;
				worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
				worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

				worksheet.Cell(currentRow, 5).Value = reportData.Sum(m => (decimal)m.ShareCapital);
				worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 6).Value = reportData.Sum(m => (decimal)m.Deposits);
				worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 7).Value = reportData.Sum(m => (decimal)m.RegFee);
				worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 8).Value = reportData.Sum(m => (decimal)m.Passbook);
				worksheet.Cell(currentRow, 8).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Cell(currentRow, 9).Value = reportData.Sum(m => (decimal)m.Loans);
				worksheet.Cell(currentRow, 9).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00";

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=SharesAndLoans_{reportDate:yyyyMMdd}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"SharesAndLoans_{reportDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportSharesAndLoansToPdf(DateTime reportDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Get all active members
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& (m.Withdrawn == null || m.Withdrawn == false)
					&& (m.Archived == null || m.Archived == false))
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

			// Get shares
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

			// Get loans
			var loans = await _context.Loans
				.Where(l => memberNos.Contains(l.MemberNo)
					&& l.CompanyCode == companyCode)
				.Select(l => new
				{
					l.MemberNo,
					l.LoanAmt
				})
				.ToListAsync();

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal deposits = memberContrib?.TotalDeposits ?? 0;
				decimal regFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbook = memberContrib?.TotalPassbook ?? 0;

				decimal totalLoans = loans.Where(l => l.MemberNo == member.MemberNo).Sum(l => l.LoanAmt ?? 0);

				// Calculate age
				int? age = null;
				if (member.Dob.HasValue)
				{
					age = DateTime.Now.Year - member.Dob.Value.Year;
					if (DateTime.Now < member.Dob.Value.AddYears(age.Value)) age--;
				}

				// Determine CIG name
				string cigName = "INDIVIDUAL";
				if (!string.IsNullOrEmpty(member.Station))
					cigName = member.Station.ToUpper();
				else if (!string.IsNullOrEmpty(member.Province))
					cigName = member.Province.ToUpper();
				else if (!string.IsNullOrEmpty(member.District))
					cigName = member.District.ToUpper();

				// Handle FullName
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				reportData.Add(new
				{
					member.MemberNo,
					Names = fullName,
					Age = age.HasValue ? age.Value.ToString() : "-",
					CIGName = cigName,
					ShareCapital = shareCapital,
					Deposits = deposits,
					RegFee = regFee,
					Passbook = passbook,
					Loans = totalLoans,
					DateRegistered = member.ApplicDate ?? member.EffectDate ?? member.AuditTime
				});
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

				// Title
				document.Add(new Paragraph($"SHARES AND LOANS REPORT AS AT {reportDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Members Table
				var table = new Table(10);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers
				var headers = new[] { "MemberNo", "Names", "Age", "CIGName", "Share Capital", "Deposits", "Regfee", "Passbook", "Loans", "Date Registered" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(8)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				foreach (var member in reportData.OrderBy(m => m.MemberNo))
				{
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(member.Names ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(member.Age ?? "")).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(member.CIGName ?? "")).SetFontSize(7));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.ShareCapital))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.Deposits))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.RegFee))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.Passbook))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", member.Loans))).SetFontSize(7).SetTextAlignment(TextAlignment.RIGHT));
					table.AddCell(new Cell().Add(new Paragraph(member.DateRegistered?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(7).SetTextAlignment(TextAlignment.CENTER));
				}

				document.Add(table);

				// Grand Total
				document.Add(new Paragraph("\n"));
				var grandTotalTable = new Table(6);
				grandTotalTable.SetWidth(UnitValue.CreatePercentValue(60));
				grandTotalTable.SetHorizontalAlignment(HorizontalAlignment.RIGHT);

				grandTotalTable.AddCell(new Cell().Add(new Paragraph("GRAND TOTAL:")).SetFont(boldFont).SetBorder(null));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.ShareCapital)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.Deposits)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.RegFee)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.Passbook)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));
				grandTotalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N0}", reportData.Sum(m => (decimal)m.Loans)))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));

				document.Add(grandTotalTable);

				// Footer
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=SharesAndLoans_{reportDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"SharesAndLoans_{reportDate:yyyyMMdd}.pdf");
			}
		}

	//Periodic Registered Members Report

		public IActionResult PeriodicRegisteredMembers()
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			// Default to last 30 days
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

			return View("~/Views/Reports/PeriodicRegisteredMembers.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> PeriodicRegisteredMembers(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			// Adjust end date to include the entire day
			var adjustedEndDate = endDate.Date.AddDays(1).AddSeconds(-1);

			// Get members registered within the date range
			// Using ApplicDate as the registration date (fallback to EffectDate or AuditTime)
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.ApplicDate >= startDate.Date
					&& m.ApplicDate <= adjustedEndDate)
				.OrderBy(m => m.ApplicDate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var reportData = new List<PeriodicRegisteredMembersViewModel>();

			foreach (var member in members)
			{
				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
				{
					fullName = member.FullName.ToString();
				}
				else
				{
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				// Handle sex mapping
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

				// Get mobile number (prioritize PhoneNo, then MobileNo)
				string mobileNo = "";
				if (!string.IsNullOrEmpty(member.PhoneNo))
					mobileNo = member.PhoneNo;
				else if (!string.IsNullOrEmpty(member.MobileNo))
					mobileNo = member.MobileNo;
				else
					mobileNo = "-";

				reportData.Add(new PeriodicRegisteredMembersViewModel
				{
					MemberNo = member.MemberNo,
					FullName = fullName,
					Sex = sex,
					RegistrationDate = member.ApplicDate ?? member.EffectDate ?? member.AuditTime,
					MobileNo = mobileNo,
					IdNo = member.Idno ?? "-",
					Email = member.Email ?? member.EmailAddress,
					Station = member.Station ?? "-",
					MembershipType = member.MembershipType ?? "Individual"
				});
			}

			// Calculate statistics
			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

			var viewModel = new PeriodicRegisteredMembersIndexViewModel
			{
				Members = reportData,
				StartDate = startDate,
				EndDate = endDate,
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				OtherCount = otherCount,
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.StartDate = startDate;
			ViewBag.EndDate = endDate;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.OtherCount = otherCount;
			ViewBag.HasData = reportData.Any();
			ViewBag.ReportDate = reportDate;

			return View("~/Views/Reports/PeriodicRegisteredMembers.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportPeriodicRegisteredMembersToExcel(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Adjust end date to include the entire day
			var adjustedEndDate = endDate.Date.AddDays(1).AddSeconds(-1);

			// Get members registered within the date range
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.ApplicDate >= startDate.Date
					&& m.ApplicDate <= adjustedEndDate)
				.OrderBy(m => m.ApplicDate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				// Handle sex mapping
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

				// Get mobile number
				string mobileNo = "";
				if (!string.IsNullOrEmpty(member.PhoneNo))
					mobileNo = member.PhoneNo;
				else if (!string.IsNullOrEmpty(member.MobileNo))
					mobileNo = member.MobileNo;
				else
					mobileNo = "-";

				reportData.Add(new
				{
					member.MemberNo,
					Names = fullName,
					Sex = sex,
					RegistrationDate = member.ApplicDate ?? member.EffectDate ?? member.AuditTime,
					MobileNo = mobileNo
				});
			}

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Registered Members");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 5).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title - Exactly as in the image
				worksheet.Cell(currentRow, 1).Value = $"MEMBERS REGISTERED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}";
				worksheet.Range(currentRow, 1, currentRow, 5).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Headers - Exactly as in the image
				var headers = new[] { "MemberNo", "Names", "Sex", "Registration Date", "MobileNo" };

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
				foreach (var member in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = member.MemberNo;
					worksheet.Cell(currentRow, 2).Value = member.Names;
					worksheet.Cell(currentRow, 3).Value = member.Sex;
					worksheet.Cell(currentRow, 4).Value = member.RegistrationDate?.ToString("dd/MM/yyyy");
					worksheet.Cell(currentRow, 5).Value = member.MobileNo;

					worksheet.Range(currentRow, 1, currentRow, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				// Statistics Section - Exactly as in the image
				currentRow += 2;
				worksheet.Cell(currentRow, 1).Value = "Total Members:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count;
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "MALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "MALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "FEMALE:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex == "FEMALE");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				currentRow++;
				worksheet.Cell(currentRow, 1).Value = "OTHERS:";
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 2).Value = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");
				worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

				// Report Generation Date
				currentRow += 2;
				worksheet.Cell(currentRow, 1).Value = $"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
				worksheet.Range(currentRow, 1, currentRow, 5).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Italic = true;

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();
					Response.Headers.Add("Content-Disposition", $"attachment; filename=RegisteredMembers_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"RegisteredMembers_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportPeriodicRegisteredMembersToPdf(DateTime startDate, DateTime endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Adjust end date to include the entire day
			var adjustedEndDate = endDate.Date.AddDays(1).AddSeconds(-1);

			// Get members registered within the date range
			var members = await _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.ApplicDate >= startDate.Date
					&& m.ApplicDate <= adjustedEndDate)
				.OrderBy(m => m.ApplicDate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var reportData = new List<dynamic>();

			foreach (var member in members)
			{
				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				// Handle sex mapping
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

				// Get mobile number
				string mobileNo = "";
				if (!string.IsNullOrEmpty(member.PhoneNo))
					mobileNo = member.PhoneNo;
				else if (!string.IsNullOrEmpty(member.MobileNo))
					mobileNo = member.MobileNo;
				else
					mobileNo = "-";

				reportData.Add(new
				{
					member.MemberNo,
					Names = fullName,
					Sex = sex,
					RegistrationDate = member.ApplicDate ?? member.EffectDate ?? member.AuditTime,
					MobileNo = mobileNo
				});
			}

			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

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

				// Title - Exactly as in the image
				document.Add(new Paragraph($"MEMBERS REGISTERED BETWEEN {startDate:dd/MM/yyyy} AND {endDate:dd/MM/yyyy}")
					.SetFont(boldFont)
					.SetFontSize(14)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Members Table
				var table = new Table(5);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers - Exactly as in the image
				var headers = new[] { "MemberNo", "Names", "Sex", "Registration Date", "MobileNo" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(9)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				foreach (var member in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.Names ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.Sex ?? "")).SetFontSize(8));
					table.AddCell(new Cell().Add(new Paragraph(member.RegistrationDate?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(8).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(member.MobileNo ?? "")).SetFontSize(8));
				}

				document.Add(table);

				// Statistics Section - Exactly as in the image
				document.Add(new Paragraph("\n"));
				var statsTable = new Table(2);
				statsTable.SetWidth(UnitValue.CreatePercentValue(30));

				statsTable.AddCell(new Cell().Add(new Paragraph("Total Members:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(reportData.Count.ToString())).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("MALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(maleCount.ToString())).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("FEMALE:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(femaleCount.ToString())).SetFont(boldFont).SetBorder(null));

				statsTable.AddCell(new Cell().Add(new Paragraph("OTHERS:")).SetFont(boldFont).SetBorder(null));
				statsTable.AddCell(new Cell().Add(new Paragraph(otherCount.ToString())).SetFont(boldFont).SetBorder(null));

				document.Add(statsTable);

				// Footer
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();
				Response.Headers.Add("Content-Disposition", $"attachment; filename=RegisteredMembers_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");
				return File(content, "application/pdf", $"RegisteredMembers_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");
			}
		}


		//Withdrawn Members Report

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

			return View("~/Views/Reports/WithdrawnMembers.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> WithdrawnMembers(DateTime? startDate, DateTime? endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";
			var reportDate = DateTime.Now;

			// Build query for withdrawn members
			var query = _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.Withdrawn == true);

			// Apply date filter if provided
			if (startDate.HasValue)
			{
				var start = startDate.Value.Date;
				query = query.Where(m => m.Memberwitrawaldate >= start);
			}

			if (endDate.HasValue)
			{
				var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
				query = query.Where(m => m.Memberwitrawaldate <= end);
			}

			var withdrawnMembers = await query
				.OrderBy(m => m.Memberwitrawaldate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = withdrawnMembers.Select(m => m.MemberNo).ToList();

			// Get share capital, deposits, reg fee, and passbook from ContribShare
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

			// Get shares from Share table as fallback for share capital
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

			foreach (var member in withdrawnMembers)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				// Calculate share capital from multiple sources
				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				// Get deposits, reg fee, and passbook
				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;

				// Calculate total amount (what the member had contributed)
				decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

				// Calculate membership duration (from join date to withdrawal date)
				int? membershipDuration = null;
				DateTime? joinDate = member.ApplicDate ?? member.EffectDate ?? member.AuditTime;
				if (joinDate.HasValue && member.Memberwitrawaldate.HasValue)
				{
					membershipDuration = ((member.Memberwitrawaldate.Value.Year - joinDate.Value.Year) * 12) +
										 member.Memberwitrawaldate.Value.Month - joinDate.Value.Month;
					if (membershipDuration < 0) membershipDuration = 0;
				}

				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
				{
					fullName = member.FullName.ToString();
				}
				else
				{
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();
					if (string.IsNullOrWhiteSpace(fullName))
						fullName = "N/A";
				}

				// Handle sex mapping
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

				// Get phone number
				string phoneNo = "";
				if (!string.IsNullOrEmpty(member.PhoneNo))
					phoneNo = member.PhoneNo;
				else if (!string.IsNullOrEmpty(member.MobileNo))
					phoneNo = member.MobileNo;
				else
					phoneNo = "-";

				reportData.Add(new WithdrawnMembersViewModel
				{
					MemberNo = member.MemberNo,
					FullName = fullName,
					WithdrawalDate = member.Memberwitrawaldate,
					ShareCapital = shareCapital,
					SavingsDeposits = savingsDeposits,
					RegistrationFee = registrationFee,
					PassbookAmount = passbookAmount,
					TotalAmount = totalAmount,
					IdNo = member.Idno ?? "-",
					Sex = sex,
					PhoneNo = phoneNo,
					DateJoined = member.ApplicDate ?? member.EffectDate ?? member.AuditTime,
					MembershipDuration = membershipDuration
				});
			}

			// Calculate statistics
			int maleCount = reportData.Count(m => m.Sex == "MALE");
			int femaleCount = reportData.Count(m => m.Sex == "FEMALE");
			int otherCount = reportData.Count(m => m.Sex != "MALE" && m.Sex != "FEMALE" && m.Sex != "NOT SPECIFIED");

			var viewModel = new WithdrawnMembersIndexViewModel
			{
				Members = reportData,
				StartDate = startDate,
				EndDate = endDate,
				TotalMembers = reportData.Count,
				MaleCount = maleCount,
				FemaleCount = femaleCount,
				OtherCount = otherCount,
				TotalShareCapital = reportData.Sum(m => m.ShareCapital),
				TotalSavingsDeposits = reportData.Sum(m => m.SavingsDeposits),
				TotalRegistrationFee = reportData.Sum(m => m.RegistrationFee),
				TotalPassbookAmount = reportData.Sum(m => m.PassbookAmount),
				GrandTotalAmount = reportData.Sum(m => m.TotalAmount),
				ReportDate = reportDate,
				HasData = reportData.Any(),
				UserCompanyCode = companyCode,
				CompanyName = companyName
			};

			ViewBag.StartDate = startDate;
			ViewBag.EndDate = endDate;
			ViewBag.TotalMembers = reportData.Count;
			ViewBag.MaleCount = maleCount;
			ViewBag.FemaleCount = femaleCount;
			ViewBag.OtherCount = otherCount;
			ViewBag.GrandTotalAmount = reportData.Sum(m => m.TotalAmount);
			ViewBag.HasData = reportData.Any();
			ViewBag.ReportDate = reportDate;

			return View("~/Views/Reports/WithdrawnMembers.cshtml", viewModel);
		}

		[HttpPost]
		public async Task<IActionResult> ExportWithdrawnMembersToExcel(DateTime? startDate, DateTime? endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Build query for withdrawn members
			var query = _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.Withdrawn == true);

			// Apply date filter if provided
			if (startDate.HasValue)
			{
				var start = startDate.Value.Date;
				query = query.Where(m => m.Memberwitrawaldate >= start);
			}

			if (endDate.HasValue)
			{
				var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
				query = query.Where(m => m.Memberwitrawaldate <= end);
			}

			var withdrawnMembers = await query
				.OrderBy(m => m.Memberwitrawaldate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = withdrawnMembers.Select(m => m.MemberNo).ToList();

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

			// Get shares
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

			var reportData = new List<dynamic>();

			foreach (var member in withdrawnMembers)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;

				decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				reportData.Add(new
				{
					member.MemberNo,
					Name = fullName,
					WithdrawalDate = member.Memberwitrawaldate,
					Amount = totalAmount
				});
			}

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Withdrawn Members");
				var currentRow = 1;

				// Company Header
				worksheet.Cell(currentRow, 1).Value = companyName.ToUpper();
				worksheet.Range(currentRow, 1, currentRow, 4).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 18;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Report Date
				worksheet.Cell(currentRow, 1).Value = DateTime.Now.ToString("dd/MM/yyyy");
				worksheet.Range(currentRow, 1, currentRow, 4).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Title - Exactly as in the image
				worksheet.Cell(currentRow, 1).Value = "WITHDRAWN MEMBERS";
				worksheet.Range(currentRow, 1, currentRow, 4).Merge();
				worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
				worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
				worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
				currentRow += 2;

				// Date Range (if specified)
				if (startDate.HasValue && endDate.HasValue)
				{
					worksheet.Cell(currentRow, 1).Value = $"Period: {startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
					worksheet.Range(currentRow, 1, currentRow, 4).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
					currentRow += 2;
				}
				else if (startDate.HasValue)
				{
					worksheet.Cell(currentRow, 1).Value = $"From: {startDate.Value:dd/MM/yyyy}";
					worksheet.Range(currentRow, 1, currentRow, 4).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
					currentRow += 2;
				}
				else if (endDate.HasValue)
				{
					worksheet.Cell(currentRow, 1).Value = $"Up to: {endDate.Value:dd/MM/yyyy}";
					worksheet.Range(currentRow, 1, currentRow, 4).Merge();
					worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
					currentRow += 2;
				}

				// Headers - Exactly as in the image
				var headers = new[] { "MemberNo", "Name", "Date Withdrawn", "Amount" };

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
				foreach (var member in reportData)
				{
					worksheet.Cell(currentRow, 1).Value = member.MemberNo;
					worksheet.Cell(currentRow, 2).Value = member.Name;
					worksheet.Cell(currentRow, 3).Value = member.WithdrawalDate?.ToString("dd/MM/yyyy");
					worksheet.Cell(currentRow, 4).Value = member.Amount;
					worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";

					worksheet.Range(currentRow, 1, currentRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					currentRow++;
				}

				// Grand Total
				if (reportData.Any())
				{
					currentRow++;
					worksheet.Cell(currentRow, 3).Value = "GRAND TOTAL:";
					worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
					worksheet.Cell(currentRow, 4).Value = reportData.Sum(m => (decimal)m.Amount);
					worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
					worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
				}

				worksheet.Columns().AdjustToContents();

				using (var stream = new MemoryStream())
				{
					workbook.SaveAs(stream);
					var content = stream.ToArray();

					string filename = "WithdrawnMembers";
					if (startDate.HasValue && endDate.HasValue)
						filename += $"_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}";
					else if (startDate.HasValue)
						filename += $"_{startDate:yyyyMMdd}_onwards";
					else if (endDate.HasValue)
						filename += $"_upto_{endDate:yyyyMMdd}";
					else
						filename += $"_All_{DateTime.Now:yyyyMMdd}";

					Response.Headers.Add("Content-Disposition", $"attachment; filename={filename}.xlsx");
					return File(content,
						"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
						$"{filename}.xlsx");
				}
			}
		}

		[HttpPost]
		public async Task<IActionResult> ExportWithdrawnMembersToPdf(DateTime? startDate, DateTime? endDate)
		{
			var companyCode = User.FindFirstValue("CompanyCode");
			var companyName = User.FindFirstValue("CompanyName") ?? "";

			// Build query for withdrawn members
			var query = _context.Members
				.Where(m => m.CompanyCode == companyCode
					&& m.Withdrawn == true);

			// Apply date filter if provided
			if (startDate.HasValue)
			{
				var start = startDate.Value.Date;
				query = query.Where(m => m.Memberwitrawaldate >= start);
			}

			if (endDate.HasValue)
			{
				var end = endDate.Value.Date.AddDays(1).AddSeconds(-1);
				query = query.Where(m => m.Memberwitrawaldate <= end);
			}

			var withdrawnMembers = await query
				.OrderBy(m => m.Memberwitrawaldate)
				.ThenBy(m => m.MemberNo)
				.ToListAsync();

			var memberNos = withdrawnMembers.Select(m => m.MemberNo).ToList();

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

			// Get shares
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

			var reportData = new List<dynamic>();

			foreach (var member in withdrawnMembers)
			{
				var memberContrib = contribShares.FirstOrDefault(c => c.MemberNo == member.MemberNo);
				var memberShare = shares.FirstOrDefault(s => s.MemberNo == member.MemberNo);

				decimal shareCapital = 0;
				if (memberContrib != null)
					shareCapital = memberContrib.TotalShareCapital;
				else if (memberShare != null)
					shareCapital = memberShare.TotalShares;
				else
					shareCapital = member.ShareCap ?? 0;

				decimal savingsDeposits = memberContrib?.TotalDeposits ?? 0;
				decimal registrationFee = memberContrib?.TotalRegFee ?? member.RegFee ?? 0;
				decimal passbookAmount = memberContrib?.TotalPassbook ?? 0;

				decimal totalAmount = shareCapital + savingsDeposits + registrationFee + passbookAmount;

				// Handle FullName safely
				string fullName = "";
				if (member.FullName != null)
					fullName = member.FullName.ToString();
				else
					fullName = $"{member.Surname ?? ""} {member.OtherNames ?? ""}".Trim();

				reportData.Add(new
				{
					member.MemberNo,
					Name = fullName,
					WithdrawalDate = member.Memberwitrawaldate,
					Amount = totalAmount
				});
			}

			decimal grandTotal = reportData.Sum(m => (decimal)m.Amount);

			using (var stream = new MemoryStream())
			{
				var writer = new PdfWriter(stream);
				var pdf = new PdfDocument(writer);
				var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

				var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
				var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

				// Company Name
				document.Add(new Paragraph(companyName.ToUpper())
					.SetFont(boldFont)
					.SetFontSize(18)
					.SetTextAlignment(TextAlignment.CENTER));

				// Report Date - Exactly as in the image
				document.Add(new Paragraph(DateTime.Now.ToString("dd/MM/yyyy"))
					.SetFont(normalFont)
					.SetFontSize(12)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Add(new Paragraph("\n"));

				// Title - Exactly as in the image
				document.Add(new Paragraph("WITHDRAWN MEMBERS")
					.SetFont(boldFont)
					.SetFontSize(16)
					.SetTextAlignment(TextAlignment.CENTER));

				document.Add(new Paragraph("\n"));

				// Date Range (if specified)
				if (startDate.HasValue || endDate.HasValue)
				{
					string periodText = "";
					if (startDate.HasValue && endDate.HasValue)
						periodText = $"Period: {startDate.Value:dd/MM/yyyy} - {endDate.Value:dd/MM/yyyy}";
					else if (startDate.HasValue)
						periodText = $"From: {startDate.Value:dd/MM/yyyy}";
					else if (endDate.HasValue)
						periodText = $"Up to: {endDate.Value:dd/MM/yyyy}";

					document.Add(new Paragraph(periodText)
						.SetFont(normalFont)
						.SetFontSize(10)
						.SetTextAlignment(TextAlignment.CENTER));
					document.Add(new Paragraph("\n"));
				}

				// Members Table
				var table = new Table(4);
				table.SetWidth(UnitValue.CreatePercentValue(100));

				// Headers - Exactly as in the image
				var headers = new[] { "MemberNo", "Name", "Date Withdrawn", "Amount" };
				foreach (var header in headers)
				{
					table.AddHeaderCell(new Cell().Add(new Paragraph(header))
						.SetFont(boldFont)
						.SetFontSize(10)
						.SetBackgroundColor(ColorConstants.LIGHT_GRAY)
						.SetTextAlignment(TextAlignment.CENTER));
				}

				// Data
				foreach (var member in reportData)
				{
					table.AddCell(new Cell().Add(new Paragraph(member.MemberNo ?? "")).SetFontSize(9));
					table.AddCell(new Cell().Add(new Paragraph(member.Name ?? "")).SetFontSize(9));
					table.AddCell(new Cell().Add(new Paragraph(member.WithdrawalDate?.ToString("dd/MM/yyyy") ?? "")).SetFontSize(9).SetTextAlignment(TextAlignment.CENTER));
					table.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N2}", member.Amount))).SetFontSize(9).SetTextAlignment(TextAlignment.RIGHT));
				}

				document.Add(table);

				// Grand Total
				if (reportData.Any())
				{
					document.Add(new Paragraph("\n"));
					var totalTable = new Table(2);
					totalTable.SetWidth(UnitValue.CreatePercentValue(50));
					totalTable.SetHorizontalAlignment(HorizontalAlignment.RIGHT);

					totalTable.AddCell(new Cell().Add(new Paragraph("GRAND TOTAL:")).SetFont(boldFont).SetBorder(null));
					totalTable.AddCell(new Cell().Add(new Paragraph(string.Format("{0:N2}", grandTotal))).SetFont(boldFont).SetBorder(null).SetTextAlignment(TextAlignment.RIGHT));

					document.Add(totalTable);
				}

				// Footer
				document.Add(new Paragraph("\n"));
				document.Add(new Paragraph($"Report Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
					.SetFont(normalFont)
					.SetFontSize(8)
					.SetTextAlignment(TextAlignment.RIGHT));

				document.Close();

				var content = stream.ToArray();

				string filename = "WithdrawnMembers";
				if (startDate.HasValue && endDate.HasValue)
					filename += $"_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}";
				else if (startDate.HasValue)
					filename += $"_{startDate:yyyyMMdd}_onwards";
				else if (endDate.HasValue)
					filename += $"_upto_{endDate:yyyyMMdd}";
				else
					filename += $"_All_{DateTime.Now:yyyyMMdd}";

				Response.Headers.Add("Content-Disposition", $"attachment; filename={filename}.pdf");
				return File(content, "application/pdf", $"{filename}.pdf");
			}
		}

		

	}
}