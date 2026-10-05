using FineDimSum.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace FineDimSum.Controllers;

public class AdmDashboardController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;

    public AdmDashboardController(DB db, IWebHostEnvironment en)
    {
        this.db = db;
        this.en = en;
    }

    // GET: AdmDashboard/Index
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Dashboard(string? product, string? category, DateTime? startDate, DateTime? endDate, string? sort, string? dir, int page = 1)
    {
        var today = DateTime.Now;

        // Get last week
        var startOfLastWeek = today.AddDays(-(int)today.DayOfWeek - 7);
        var endOfLastWeek = startOfLastWeek.AddDays(6);

        // Get last month
        var startOfLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var endOfLastMonth = startOfLastMonth.AddMonths(1).AddDays(-1);

        // Get last year
        var startOfLastYear = new DateTime(today.Year - 1, 1, 1);
        var endOfLastYear = new DateTime(today.Year - 1, 12, 31);

        // Get members joined last month
        var membersJoinedLastMonth = db.Members
            .Where(m => m.RegistrationDate >= startOfLastMonth && m.RegistrationDate <= endOfLastMonth)
            .Count();

        ViewBag.MembersJoinedLastMonth = membersJoinedLastMonth;

        // Get members joined last year
        var membersJoinedLastYear = db.Members
            .Where(m => m.RegistrationDate >= startOfLastYear && m.RegistrationDate <= endOfLastYear)
            .Count();

        ViewBag.MembersJoinedLastYear = membersJoinedLastYear;

        // Get sales last week
        var lastWeekSales = db.Payments
            .Where(p => p.PaymentDateTime >= startOfLastWeek && p.PaymentDateTime <= endOfLastWeek)
            .Sum(p => p.Total);

        ViewBag.LastWeekSales = lastWeekSales.ToString("N2");

        // Get sales last year
        var lastYearSales = db.Payments
            .Where(p => p.PaymentDateTime >= startOfLastYear && p.PaymentDateTime <= endOfLastYear)
            .Sum(p => p.Total);

        ViewBag.LastYearSales = lastYearSales.ToString("N2");

        // Get best-selling items
        var bestSellingItem = db.OrderItems
        .Where(oi => oi.Order.Status == "Paid" &&
                     oi.Order.CreatedDateTime >= startOfLastWeek &&
                     oi.Order.CreatedDateTime <= endOfLastWeek)
        .GroupBy(oi => oi.ProductVariationOption.Product)
        .Select(group => new
        {
            Id = group.Key.Id,
            Name = group.Key.Name,
            Image = group.Key.Image,
            QuantitySold = group.Sum(oi => oi.Quantity)
        })
        .OrderByDescending(product => product.QuantitySold)
        .FirstOrDefault();

        // No best-selling items
        if (bestSellingItem == null)
        {
            var defaultItem = db.Products
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name ?? "N/A",
                    Image = p.Image ?? "N/A",
                    QuantitySold = 0
                })
                .FirstOrDefault();

            if (defaultItem == null)
            {
                defaultItem = new
                {
                    Id = 0,
                    Name = "N/A",
                    Image = "N/A",
                    QuantitySold = 0
                };
            }

            bestSellingItem = defaultItem;
        }

        ViewBag.BestSellingItem = bestSellingItem;

        // Get top member purchase
        var topMemberPurchase = db.Orders
        .Where(o => o.Status == "Paid" &&
                    o.Payments.Any(p => p.PaymentDateTime >= startOfLastMonth && p.PaymentDateTime <= endOfLastMonth) &&
                    o.User.Username != "Guest")
        .GroupBy(o => new { o.UserId, o.User.Username })
        .Select(g => new
        {
            Id = g.Key.UserId,
            Name = g.Key.Username,
            TotalPurchase = g.Sum(o => o.Payments
                .Where(p => p.PaymentDateTime >= startOfLastMonth && p.PaymentDateTime <= endOfLastMonth)
                .Sum(p => p.Total))
        })
        .OrderByDescending(m => m.TotalPurchase)
        .FirstOrDefault();

        // No top member purchase
        if (topMemberPurchase == null)
        {
            var defaultMember = db.Members
                .Where(m => m.Username != "Guest")
                .Select(m => new
                {
                    Id = m.Id,
                    Name = m.Username ?? "N/A",
                    TotalPurchase = 0m
                })
                .FirstOrDefault();

            if (defaultMember == null)
            {
                defaultMember = new
                {
                    Id = 0,
                    Name = "N/A",
                    TotalPurchase = 0m,
                };
            }

            topMemberPurchase = defaultMember;
        }

        ViewBag.TopMemberPurchase = topMemberPurchase;

        // (1) Searching ------------------------
        ViewBag.Name = product = product?.Trim() ?? "";
        ViewBag.Category = category;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active").Select(c => c.Name));

        if (startDate.HasValue)
        {
            startDate = startDate.Value.Date;
        }

        if (endDate.HasValue)
        {
            endDate = endDate.Value.Date.AddDays(1).AddSeconds(-1);
        }

        var searched = db.Products
        .Include(p => p.ProductVariationOptions)
        .Select(p => new
        {
            p.Id,
            p.Image,
            p.Name,
            Category = p.Category.Name,
            QuantitySold = p.ProductVariationOptions
                .SelectMany(pvo => pvo.OrderItems)
                .Where(oi => oi.Order.Status == "Paid" &&
                             (!startDate.HasValue || oi.Order.CreatedDateTime >= startDate) &&
                             (!endDate.HasValue || oi.Order.CreatedDateTime <= endDate))
                .Sum(oi => oi.Quantity),
            TotalSales = p.ProductVariationOptions
                .SelectMany(pvo => pvo.OrderItems)
                .Where(oi => oi.Order.Status == "Paid" &&
                             (!startDate.HasValue || oi.Order.CreatedDateTime >= startDate) &&
                             (!endDate.HasValue || oi.Order.CreatedDateTime <= endDate))
                .Sum(oi => oi.Order.Total),
        })
        .Where(p =>
            (p.Id.ToString().Contains(product) ||
             p.Name.Contains(product) ||
             p.QuantitySold.ToString().Contains(product) ||
             p.TotalSales.ToString().Contains(product)) &&
            (string.IsNullOrEmpty(category) || p.Category == category))
        .ToList();

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<dynamic, object> fn = sort switch
        {
            "Product Id" => p => p.Id,
            "Image" => p => p.Image,
            "Product Name" => p => p.Name,
            "Category" => p => p.Category,
            "Quantity Sold" => p => p.QuantitySold,
            "Total Sales (RM)" => p => p.TotalSales,
            _ => p => p.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { product, category, startDate, endDate, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { product, category, startDate, endDate, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_Dashboard", m);
        }

        return View(m);
    }
}