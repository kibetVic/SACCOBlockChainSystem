using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models.DTOs;
using SACCOBlockChainSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Controllers
{
    [Authorize]
    public class AssetsRegisterController : Controller
    {
        private readonly IAssetsRegisterService _assetService;
        private readonly ICompanyContextService _companyContextService;
        private readonly ILogger<AssetsRegisterController> _logger;
        private readonly ApplicationDbContext _context;

        public AssetsRegisterController(
            IAssetsRegisterService assetService,
            ICompanyContextService companyContextService,
            ILogger<AssetsRegisterController> logger,
            ApplicationDbContext context)
        {
            _assetService = assetService;
            _companyContextService = companyContextService;
            _logger = logger;
            _context = context;
        }

        // GET: AssetsRegister/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var assets = await _assetService.GetAllAssetsAsync(companyCode);

                // Get summary statistics
                var totalAssets = assets.Count;
                var totalValue = assets.Sum(a => a.TotalValue ?? 0);
                var postedCount = assets.Count(a => a.Posted == true);
                var blockchainVerifiedCount = assets.Count(a => !string.IsNullOrEmpty(a.BlockchainTxId));

                // Get unique asset types for filter
                var assetTypes = assets.Select(a => a.AssetType).Distinct().ToList();

                ViewBag.TotalAssets = totalAssets;
                ViewBag.TotalValue = totalValue;
                ViewBag.PostedCount = postedCount;
                ViewBag.BlockchainVerifiedCount = blockchainVerifiedCount;
                ViewBag.AssetTypes = assetTypes;
                ViewBag.CompanyCode = companyCode;

                return View(assets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assets index");
                TempData["ErrorMessage"] = "Error loading assets list: " + ex.Message;
                return View(new List<AssetsRegisterResponseDTO>());
            }
        }

        // GET: AssetsRegister/Create
        public IActionResult Create()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                ViewBag.CompanyCode = companyCode;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create asset form");
                TempData["ErrorMessage"] = "Error loading form: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AssetsRegister/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssetsRegisterDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    _logger.LogWarning($"ModelState invalid: {errors}");
                    ViewBag.CompanyCode = dto.CompanyCode;
                    return View(dto);
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var createdBy = User.Identity?.Name ?? "SYSTEM";

                var asset = await _assetService.CreateAssetAsync(dto, createdBy);

                TempData["SuccessMessage"] = $"Asset '{asset.AssetName}' registered successfully!";
                return RedirectToAction("Details", new { id = asset.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating asset");
                ModelState.AddModelError("", ex.Message);
                ViewBag.CompanyCode = dto.CompanyCode;
                return View(dto);
            }
        }

        // GET: AssetsRegister/Edit/{id}
        public async Task<IActionResult> Edit(long id)
        {
            try
            {
                var asset = await _assetService.GetAssetByIdAsync(id);
                if (asset == null)
                {
                    TempData["ErrorMessage"] = "Asset not found";
                    return RedirectToAction("Index");
                }

                var dto = new AssetsRegisterDTO
                {
                    Id = asset.Id,
                    Class = asset.Class,
                    AssetType = asset.AssetType,
                    AssetName = asset.AssetName,
                    TagNo = asset.TagNo,
                    SerialNo = asset.SerialNo,
                    Quantity = asset.Quantity,
                    ActualValue = asset.ActualValue,
                    MarketValue = asset.MarketValue,
                    TotalValue = asset.TotalValue,
                    DateOfManufacture = asset.DateOfManufacture,
                    DatePurchased = asset.DatePurchased,
                    TransactionNo = asset.TransactionNo,
                    CompanyCode = asset.CompanyCode,
                    Location = asset.Location,
                    Posted = asset.Posted
                };

                ViewBag.CompanyCode = asset.CompanyCode;
                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for asset {id}");
                TempData["ErrorMessage"] = "Error loading edit form: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AssetsRegister/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, AssetsRegisterDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.CompanyCode = dto.CompanyCode;
                    return View(dto);
                }

                dto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var updatedBy = User.Identity?.Name ?? "SYSTEM";

                var asset = await _assetService.UpdateAssetAsync(id, dto, updatedBy);

                TempData["SuccessMessage"] = $"Asset '{asset.AssetName}' updated successfully!";
                return RedirectToAction("Details", new { id = asset.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating asset {id}");
                ModelState.AddModelError("", ex.Message);
                ViewBag.CompanyCode = dto.CompanyCode;
                return View(dto);
            }
        }

        // GET: AssetsRegister/Details/{id}
        public async Task<IActionResult> Details(long id)
        {
            try
            {
                var asset = await _assetService.GetAssetByIdAsync(id);
                if (asset == null)
                {
                    TempData["ErrorMessage"] = "Asset not found";
                    return RedirectToAction("Index");
                }

                return View(asset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading asset details for {id}");
                TempData["ErrorMessage"] = "Error loading details: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: AssetsRegister/Delete/{id}
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var asset = await _assetService.GetAssetByIdAsync(id);
                if (asset == null)
                {
                    TempData["ErrorMessage"] = "Asset not found";
                    return RedirectToAction("Index");
                }

                return View(asset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading delete confirmation for asset {id}");
                TempData["ErrorMessage"] = "Error loading delete confirmation: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AssetsRegister/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            try
            {
                var deletedBy = User.Identity?.Name ?? "SYSTEM";
                await _assetService.DeleteAssetAsync(id, deletedBy);

                TempData["SuccessMessage"] = "Asset deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting asset {id}");
                TempData["ErrorMessage"] = "Error deleting asset: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AssetsRegister/Post/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Post(long id)
        {
            try
            {
                var postedBy = User.Identity?.Name ?? "SYSTEM";
                await _assetService.PostAssetAsync(id, postedBy);

                TempData["SuccessMessage"] = "Asset posted successfully!";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting asset {id}");
                TempData["ErrorMessage"] = "Error posting asset: " + ex.Message;
                return RedirectToAction("Details", new { id });
            }
        }

        // GET: AssetsRegister/Search
        public IActionResult Search()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                ViewBag.CompanyCode = companyCode;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading search page");
                TempData["ErrorMessage"] = "Error loading search page: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AssetsRegister/Search
        [HttpPost]
        public async Task<IActionResult> Search(AssetsRegisterSearchDTO searchDto)
        {
            try
            {
                searchDto.CompanyCode = _companyContextService.GetCurrentCompanyCode();
                var results = await _assetService.SearchAssetsAsync(searchDto);

                ViewBag.SearchCriteria = searchDto;
                ViewBag.ResultCount = results.Count;

                return View("SearchResults", results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching assets");
                TempData["ErrorMessage"] = "Error searching assets: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: AssetsRegister/SearchResults
        public async Task<IActionResult> SearchResults(
            string? assetName,
            string? assetType,
            string? classType,
            string? tagNo,
            string? serialNo,
            string? location,
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var searchDto = new AssetsRegisterSearchDTO
                {
                    AssetName = assetName,
                    AssetType = assetType,
                    Class = classType,
                    TagNo = tagNo,
                    SerialNo = serialNo,
                    Location = location,
                    FromDate = fromDate,
                    ToDate = toDate,
                    CompanyCode = _companyContextService.GetCurrentCompanyCode()
                };

                var results = await _assetService.SearchAssetsAsync(searchDto);
                ViewBag.SearchCriteria = searchDto;
                ViewBag.ResultCount = results.Count;

                return View(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading search results");
                TempData["ErrorMessage"] = "Error loading search results: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: AssetsRegister/Export
        public async Task<IActionResult> Export()
        {
            try
            {
                var companyCode = _companyContextService.GetCurrentCompanyCode();
                var assets = await _assetService.GetAllAssetsAsync(companyCode);

                // Build CSV content
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("ID,Asset Name,Asset Type,Class,Tag No,Serial No,Quantity,Actual Value,Market Value,Total Value,Location,Date Purchased,Date Manufactured,Posted,Blockchain Tx");

                foreach (var asset in assets)
                {
                    csv.AppendLine($"\"{asset.Id}\",\"{asset.AssetName}\",\"{asset.AssetType}\",\"{asset.Class}\",\"{asset.TagNo}\",\"{asset.SerialNo}\",{asset.Quantity},{asset.ActualValue},{asset.MarketValue},{asset.TotalValue},\"{asset.Location}\",\"{asset.DatePurchased:dd/MM/yyyy}\",\"{asset.DateOfManufacture:dd/MM/yyyy}\",\"{(asset.Posted == true ? "Yes" : "No")}\",\"{asset.BlockchainTxId}\"");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"AssetsRegister_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting assets");
                TempData["ErrorMessage"] = "Error exporting assets: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}