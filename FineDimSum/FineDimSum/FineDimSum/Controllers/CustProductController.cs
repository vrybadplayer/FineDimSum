using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure;
using FineDimSum.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace FineDimSum.Controllers;

public class CustProductController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;

    // Status
    private readonly string[] status = { "Active", "Inactive" };
    private readonly string[] sortType = { "Product Name", "Price" };

    public CustProductController(DB db, IWebHostEnvironment en)
    {
        this.db = db;
        this.en = en;
    }

    // GET: CustProduct/Index
    public IActionResult Index(string? search, decimal? minPrice, decimal? maxPrice, string? sort, string? dir, string? category = "All", int page = 1)
    {
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString) || !int.TryParse(restTableIdString, out int restTableId))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Get the count of cart items for the specified RestTableId
        int totalCartItemCount = db.CartItems.Count(ci => ci.RestTableId == restTableId);

        // Set the total cart item count to a session
        HttpContext.Session.SetString("TotalCartItemCount", totalCartItemCount.ToString());

        ViewBag.CategoryList = db.Categories.Where(c => c.Status == "Active").Select(c => c.Name).ToList();
        ViewBag.SortList = new SelectList(sortType);

        // (1) Searching ------------------------
        ViewBag.Name = search = search?.Trim() ?? "";
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;

        if (category == "All")
        {
            category = "";
            ViewBag.Category = "All";
            ViewBag.CategoryDescription = "Explore all of the buns, steamed and fried dishes, together with dessert and drinks.";
        }
        else
        {
            ViewBag.Category = category;
            ViewBag.CategoryDescription = db.Categories
                                            .Where(c => c.Name == category)
                                            .Select(c => c.Description)
                                            .FirstOrDefault();
        }

        minPrice = minPrice ?? 0m;
        maxPrice = maxPrice ?? 99999999.99m;

        var searched = db.Products
        .Include(p => p.ProductVariationOptions)
        .Select(p => new
        {
            p.Id,
            p.Image,
            p.Name,
            Category = p.Category.Name,
            CategoryDescription = p.Category.Description,
            FirstVariationOption = p.ProductVariationOptions
            .Where(pvo => pvo.Status == "Active")
            .OrderBy(pvo => pvo.UnitPrice)
            .FirstOrDefault(),
            p.Status,
        })
        .Where(p =>
        (p.Name.Contains(search)) &&
        (string.IsNullOrEmpty(category) || p.Category == category) &&
        (p.Status == "Active") &&
        (p.FirstVariationOption == null ||
         (p.FirstVariationOption.UnitPrice >= minPrice &&
          p.FirstVariationOption.UnitPrice <= maxPrice))
        )
        .ToList();

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<dynamic, object> fn = sort switch
        {
            "Product Name" => p => p.Name,
            "Price" => p => p.FirstVariationOption.UnitPrice,
            _ => p => p.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { search, category, minPrice, maxPrice, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 9);
        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { search, category, minPrice, maxPrice, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_IndexA", m);
        }

        return View(m);
    }

    // GET: CustProduct/Detail
    public IActionResult Detail(int id)
    {
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Get selected product and include related variation data
        var selectedProduct = db.Products
            .Include(p => p.ProductVariationOptions)
            .Where(p => p.Status == "Active" && p.Id == id)
            .FirstOrDefault();

        if (selectedProduct != null)
        {
            selectedProduct.ProductVariationOptions = selectedProduct.ProductVariationOptions
                .Where(pvo => pvo.Status == "Active" && pvo.StockQuantity > 0)
                .ToList();

            if (!selectedProduct.ProductVariationOptions.Any())
            {
                TempData["error"] = "Not Active/Out of Stock.";
                return RedirectToAction("Index");
            }

            var relatedProducts = db.Products
            .Where(p => p.Status == "Active" &&
                        p.CategoryId == selectedProduct.CategoryId &&
                        p.Id != selectedProduct.Id &&
                        p.ProductVariationOptions.Any(pvo => pvo.Status == "Active" && pvo.StockQuantity > 0))
            .Take(4) // Limit to 4 products
            .ToList();

            foreach (var product in relatedProducts)
            {
                product.ProductVariationOptions = product.ProductVariationOptions
                    .Where(pvo => pvo.Status == "Active" && pvo.StockQuantity > 0)
                    .ToList();
            }

            var firstVariationPrices = relatedProducts
                .Where(p => p.ProductVariationOptions.Any())
                .Select(p => p.ProductVariationOptions.First().UnitPrice)
                .ToList();

            ViewBag.RelatedProducts = relatedProducts;
            ViewBag.FirstVariationPrices = firstVariationPrices;
        }
        else
        {
            TempData["error"] = "Product Not Found/Active.";
            return RedirectToAction("Index");
        }

        return View(selectedProduct);
    }
}