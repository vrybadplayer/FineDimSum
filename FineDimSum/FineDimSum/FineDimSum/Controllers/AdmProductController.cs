using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using X.PagedList.Extensions;

namespace FineDimSum.Controllers;

public class AdmProductController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    // Status
    private readonly string[] productStatus = { "Active", "Inactive" };

    public AdmProductController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmProduct/CheckName
    public bool CheckName(string name, int? currentId = null)
    {
        return !db.Products
            .Where(p => p.Status != "Terminated")
            .Any(p => p.Name == name && (!currentId.HasValue || p.Id != currentId));
    }

    // GET: AdmProduct/CheckDescription
    public bool CheckDescription(string description)
    {
        return (description.Length >= 1 && description.Length <= 200);
    }

    // GET: AdmProduct/CheckCategoryId
    public bool CheckCategoryId(int categoryId)
    {
        return db.Categories.Any(c => c.Id == categoryId);
    }

    // GET: AdmProduct/CheckStatus
    public bool CheckStatus(string status)
    {
        return productStatus.Contains(status);
    }

    // GET: AdmProduct/CheckOptionName
    public bool CheckOptionName(string name)
    {
        return name.Length >= 1 && name.Length <= 100;
    }

    // GET: AdmProduct/CheckUnitPrice
    public bool CheckUnitPrice(decimal price)
    {
        return price >= 0.01m && price <= 99999999.99m;
    }

    // GET: AdmProduct/CheckUnitPrice
    public bool CheckQuantity(int quantity)
    {
        return quantity >= 0 && quantity <= 100000;
    }

    // GET: AdmProduct/ProductManagement
    [Authorize(Roles = "Root, Manager")]
    public IActionResult ProductManagement(string? product, string? category, string? status, string? sort, string? dir, int page = 1)
    {
        // Detect if got variation that is "Inactive" OR "Terminated", Delete related OrderItems (Preparing) with Order (Active)
        var orderItemsToRemove = db.OrderItems
        .Where(oi => (oi.ProductVariationOption.Status == "Inactive" || oi.ProductVariationOption.Status == "Terminated")
                     && oi.Status == "Preparing"
                     && oi.Order.Status == "Active")
        .ToList();

        db.OrderItems.RemoveRange(orderItemsToRemove);
        db.SaveChanges();

        ViewBag.StatusList = new SelectList(productStatus);
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active").Select(c => c.Name));
        ViewBag.NumOfProducts = db.Products.Count(c => c.Status != "Terminated");

        // Status colours
        var statusColors = new Dictionary<string, string>
        {
            { "Active", "status-active" },
            { "Inactive", "status-inactive" },
            { "Terminated", "status-terminated" }
        };

        ViewBag.StatusColor = statusColors.ContainsKey(status ?? "") ? statusColors[status ?? ""] : "";

        // (1) Searching ------------------------
        ViewBag.Name = product = product?.Trim() ?? "";
        ViewBag.Status = status;
        ViewBag.Category = category;

        int.TryParse(product, out int productId);

        var searched = db.Products
        .Include(p => p.ProductVariationOptions)
        .Select(p => new
        {
            p.Id,
            p.Image,
            p.Name,
            Category = p.Category.Name,
            CategoryStatus = p.Category.Status,
            ProductVariationOptions = p.ProductVariationOptions
            .Where(pvo => pvo.Status != "Terminated")
            .Select(pvo => new
            {
                pvo.Name,
                pvo.MinStockLevel,
                pvo.StockQuantity,
                pvo.Status,
            }).ToList(),
            TotalMinStockLevel = p.ProductVariationOptions.Sum(pvo => pvo.MinStockLevel),
            TotalStockQuantity = p.ProductVariationOptions.Sum(pvo => pvo.StockQuantity),
            p.Status,
        })
        .Where(p =>
        (p.Id == productId ||
         p.Name.Contains(product) ||
         p.ProductVariationOptions.Any(pvo =>
             pvo.Name.Contains(product) ||
             pvo.MinStockLevel.ToString().Contains(product) ||
             pvo.StockQuantity.ToString().Contains(product))) &&
        (string.IsNullOrEmpty(category) || p.Category == category) &&
         p.CategoryStatus == "Active" &&
         (string.IsNullOrEmpty(status) || p.Status == status) &&
         p.Status != "Terminated")
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
            "Min. Stock Level" => p => p.TotalMinStockLevel,
            "Stock Quantity" => p => p.TotalStockQuantity,
            "Status" => p => p.Status,
            _ => p => p.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { product, status, category, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { product, status, category, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_ProductManagement", m);
        }

        return View(m);
    }

    // POST: AdmProduct/Restock
    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Restock()
    {
        var productVariations = db.ProductVariationOptions
                                  .Where(pvo => pvo.Status == "Active" && pvo.Product.Status == "Active")
                                  .ToList();

        foreach (var variation in productVariations)
        {
            variation.StockQuantity = variation.PreStockQuantity;
        }

        db.SaveChanges();

        TempData["success"] = "All products' stock quantity updated successfully.";
        return RedirectToAction("ProductManagement");
    }

    // GET: AdmProduct/Create
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create()
    {
        var vm = new ProductCreateVM
        {
            ProductVariationOptions = new List<ProductVariationOptionCreateVM>
            {
                new ProductVariationOptionCreateVM() // Add one empty row by default
            }
        };

        ViewBag.StatusList = new SelectList(productStatus);
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active"), "Id", "Name");
        return View(vm);
    }

    // POST: AdmProduct/Create
    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create(ProductCreateVM vm)
    {
        if (ModelState.IsValid("Name") && !CheckName(vm.Name))
        {
            ModelState.AddModelError("Name", "Duplicated Product Name.");
        }

        if (ModelState.IsValid("Description") && !CheckDescription(vm.Description))
        {
            ModelState.AddModelError("Description", "Product Description must be between 1 - 200 characters.");
        }

        if (ModelState.IsValid("CategoryId") && !CheckCategoryId(vm.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Invalid Category.");
        }

        if (ModelState.IsValid("Status") && !CheckStatus(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid Status.");
        }

        if (ModelState.IsValid("Photo"))
        {
            var e = hp.ValidatePhoto(vm.Photo);
            if (e != "") ModelState.AddModelError("Photo", e);
        }

        if (vm.ProductVariationOptions != null)
        {
            foreach (var option in vm.ProductVariationOptions)
            {
                if (string.IsNullOrEmpty(option.Name))
                {
                    ModelState.AddModelError("ProductVariationOption.Name", "The Option field is required.");
                }
                else if (!CheckOptionName(option.Name))
                {
                    ModelState.AddModelError("ProductVariationOption.Name", "Product Variation Name must be between 1 - 100 characters.");
                }

                if (!CheckUnitPrice(option.UnitPrice))
                {
                    ModelState.AddModelError("ProductVariationOption.UnitPrice", "Unit Price must be between 0.01 - 99999999.99.");
                }

                if (!CheckQuantity(option.PreStockQuantity))
                {
                    ModelState.AddModelError("ProductVariationOption.PreStockQuantity", "Predefined Stock Quantity must be between 0 - 100000.");
                }

                if (!CheckQuantity(option.StockQuantity))
                {
                    ModelState.AddModelError("ProductVariationOption.StockQuantity", "Stock Quantity must be between 0 - 100000.");
                }

                if (!CheckQuantity(option.MinStockLevel))
                {
                    ModelState.AddModelError("ProductVariationOption.MinStockLevel", "Minumum Stock Level must be between 0 - 100000.");
                }

                if (!CheckStatus(option.Status))
                {
                    ModelState.AddModelError("ProductVariationOption.Status", "Invalid Status.");
                }

                if (option.Photo != null)
                {
                    var e = hp.ValidatePhoto(option.Photo);
                    if (e != "") ModelState.AddModelError("ProductVariationOption.Photo", e);
                }
            }
        }

        if (vm.ProductVariationOptions.Count == 0)
        {
            TempData["error"] = "Must have at least one variation.";
        }

        if (ModelState.IsValid && vm.ProductVariationOptions.Count > 0)
        {
            var product = new Product
            {
                Name = vm.Name,
                Description = vm.Description,
                CategoryId = vm.CategoryId,
                Status = vm.Status,
                Image = hp.SaveImage(vm.Photo, "images/AdmProduct/Product"),
                ProductVariationOptions = new List<ProductVariationOption>(),
            };

            foreach (var v in vm.ProductVariationOptions)
            {
                var variation = new ProductVariationOption
                {
                    Name = v.Name,
                    UnitPrice = v.UnitPrice,
                    PreStockQuantity = v.PreStockQuantity,
                    StockQuantity = v.StockQuantity,
                    MinStockLevel = v.MinStockLevel,
                    Status = vm.Status == "Inactive" ? "Inactive" : v.Status,
                    Image = hp.SaveImage(v.Photo, "images/AdmProduct/ProductVariationOption"),
                };

                product.ProductVariationOptions.Add(variation);
            }

            db.Products.Add(product);
            db.SaveChanges();

            TempData["success"] = "New product added successfully.";
            return RedirectToAction("ProductManagement");
        }

        TempData["error"] = "Error(s) found in the form.";
        ViewBag.StatusList = new SelectList(productStatus);
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active"), "Id", "Name");
        return View(vm);
    }

    // GET: AdmProduct/Update
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Update(int? id)
    {
        var p = db.Products
          .Include(p => p.ProductVariationOptions)
          .FirstOrDefault(p => p.Id == id);

        if (p == null)
        {
            TempData["error"] = "Id not found.";
            return RedirectToAction("ProductManagement");
        }

        var vm = new ProductUpdateVM
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            CategoryId = p.CategoryId,
            Status = p.Status,
            Image = p.Image,
            ProductVariationOptions = p.ProductVariationOptions
            .Where(option => option.Status != "Terminated")
            .Select(option => new ProductVariationOptionUpdateVM
            {
                Id = option.Id,
                Name = option.Name,
                UnitPrice = option.UnitPrice,
                PreStockQuantity = option.PreStockQuantity,
                StockQuantity = option.StockQuantity,
                MinStockLevel = option.MinStockLevel,
                Status = option.Status,
                Image = option.Image,
            }).ToList()
        };

        ViewBag.StatusList = new SelectList(productStatus);
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active"), "Id", "Name");
        return View(vm);
    }

    // POST: AdmProduct/Update
    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Update(ProductUpdateVM vm)
    {
        var p = db.Products.Find(vm.Id);

        if (p == null)
        {
            TempData["error"] = "Id not found.";
            return RedirectToAction("ProductManagement");
        }

        if (ModelState.IsValid("Name") && !CheckName(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Duplicated Product Name.");
        }

        if (ModelState.IsValid("Description") && !CheckDescription(vm.Description))
        {
            ModelState.AddModelError("Description", "Product Description must be between 1 - 200 characters.");
        }

        if (ModelState.IsValid("CategoryId") && !CheckCategoryId(vm.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Invalid Category.");
        }

        if (ModelState.IsValid("Status") && !CheckStatus(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid Status.");
        }

        if (vm.Photo != null)
        {
            var e = hp.ValidatePhoto(vm.Photo);
            if (e != "") ModelState.AddModelError("Photo", e);
        }

        if (vm.ProductVariationOptions != null)
        {
            foreach (var option in vm.ProductVariationOptions)
            {
                if (string.IsNullOrEmpty(option.Name))
                {
                    ModelState.AddModelError("ProductVariationOption.Name", "The Option field is required.");
                }
                else if (!CheckOptionName(option.Name))
                {
                    ModelState.AddModelError("ProductVariationOption.Name", "Product Variation Name must be between 1 - 100 characters.");
                }

                if (!CheckUnitPrice(option.UnitPrice))
                {
                    ModelState.AddModelError("ProductVariationOption.UnitPrice", "Unit Price must be between 0.01 - 99999999.99.");
                }

                if (!CheckQuantity(option.PreStockQuantity))
                {
                    ModelState.AddModelError("ProductVariationOption.PreStockQuantity", "Predefined Stock Quantity must be between 0 - 100000.");
                }

                if (!CheckQuantity(option.StockQuantity))
                {
                    ModelState.AddModelError("ProductVariationOption.StockQuantity", "Stock Quantity must be between 0 - 100000.");
                }

                if (!CheckQuantity(option.MinStockLevel))
                {
                    ModelState.AddModelError("ProductVariationOption.MinStockLevel", "Minumum Stock Level must be between 0 - 100000.");
                }

                if (!CheckStatus(option.Status))
                {
                    ModelState.AddModelError("ProductVariationOption.Status", "Invalid Status.");
                }

                if (option.Photo != null)
                {
                    var e = hp.ValidatePhoto(option.Photo);
                    if (e != "") ModelState.AddModelError("ProductVariationOption.Photo", e);
                }
            }
        }

        if (vm.ProductVariationOptions.Count == 0)
        {
            TempData["error"] = "Must have at least one variation.";
        }

        if (ModelState.IsValid && vm.ProductVariationOptions.Count > 0)
        {
            p.Name = vm.Name.Trim();
            p.Description = vm.Description.Trim();
            p.CategoryId = vm.CategoryId;
            p.Status = vm.Status;

            if (vm.Photo != null)
            {
                p.Image = hp.SaveImage(vm.Photo, "images/AdmProduct/Product");
            }

            // Existing & Submitted Options
            var existingOptions = db.ProductVariationOptions.Where(p => p.ProductId == vm.Id).ToList();
            var submittedOptions = vm.ProductVariationOptions;

            // Variations to be inserted, updated & deleted
            var optionsToUpdate = new List<ProductVariationOption>();
            var optionsToInsert = new List<ProductVariationOption>();
            var optionsToDelete = new List<ProductVariationOption>();

            // Set all option to "Inactive" if Product is "Inactive"
            if (p.Status == "Inactive")
            {
                foreach (var option in existingOptions)
                {
                    option.Status = "Inactive";

                    // Remove related OrderItems (Preparing) with Order (Active)
                    var orderItemsToRemove = option.OrderItems
                        .Where(oi => oi.Status == "Preparing" && oi.Order.Status == "Active")
                        .ToList();
                    db.OrderItems.RemoveRange(orderItemsToRemove);
                }
            }

            // Compare exiting with submitted options
            foreach (var submitted in submittedOptions)
            {
                var existingOption = existingOptions.FirstOrDefault(o => o.Id == submitted.Id);

                if (existingOption != null)
                {
                    // If got existing, check if need to update
                    if (existingOption.Name != submitted.Name ||
                        existingOption.UnitPrice != submitted.UnitPrice ||
                        existingOption.PreStockQuantity != submitted.PreStockQuantity ||
                        existingOption.StockQuantity != submitted.StockQuantity ||
                        existingOption.MinStockLevel != submitted.MinStockLevel ||
                        existingOption.Status != submitted.Status ||
                        existingOption.Image != submitted.Image)
                    {
                        existingOption.ProductId = vm.Id;
                        existingOption.Name = submitted.Name;
                        existingOption.UnitPrice = submitted.UnitPrice;
                        existingOption.PreStockQuantity = submitted.PreStockQuantity;
                        existingOption.StockQuantity = submitted.StockQuantity;
                        existingOption.MinStockLevel = submitted.MinStockLevel;
                        existingOption.Status = p.Status == "Inactive" ? "Inactive" : submitted.Status;

                        if (submitted.Photo != null)
                        {
                            existingOption.Image = hp.SaveImage(submitted.Photo, "images/AdmProduct/ProductVariationOption");
                        }

                        // Add to update list
                        optionsToUpdate.Add(existingOption);
                    }

                    // Remove related OrderItems (Preparing) with Order (Active)
                    if (existingOption.Status == "Inactive" || existingOption.Status == "Terminated")
                    {
                        var orderItemsToRemove = existingOption.OrderItems
                            .Where(oi => oi.Status == "Preparing" && oi.Order.Status == "Active")
                            .ToList();
                        db.OrderItems.RemoveRange(orderItemsToRemove);
                    }

                    // Remove from deletion list
                    existingOptions.Remove(existingOption);
                }
                else
                {
                    // If not found, add to insert list
                    optionsToInsert.Add(new ProductVariationOption
                    {
                        ProductId = vm.Id,
                        Name = submitted.Name,
                        UnitPrice = submitted.UnitPrice,
                        PreStockQuantity = submitted.PreStockQuantity,
                        StockQuantity = submitted.StockQuantity,
                        MinStockLevel = submitted.MinStockLevel,
                        Status = submitted.Status,
                        Image = hp.SaveImage(submitted.Photo, "images/AdmProduct/ProductVariationOption"),
                    });
                }
            }

            // Delete list
            foreach (var optionToDelete in existingOptions)
            {
                optionToDelete.Status = "Terminated";
                optionsToDelete.Add(optionToDelete);

                // Remove related OrderItems (Preparing) with Order (Active)
                var orderItemsToRemove = optionToDelete.OrderItems
                    .Where(oi => oi.Status == "Preparing" && oi.Order.Status == "Active")
                    .ToList();
                db.OrderItems.RemoveRange(orderItemsToRemove);
            }

            // Perform database operations
            if (optionsToUpdate.Any())
            {
                db.ProductVariationOptions.UpdateRange(optionsToUpdate);
            }

            if (optionsToInsert.Any())
            {
                db.ProductVariationOptions.AddRange(optionsToInsert);
            }

            if (optionsToDelete.Any())
            {
                db.ProductVariationOptions.UpdateRange(optionsToDelete);
            }

            db.SaveChanges();

            TempData["success"] = "Changes saved successfully.";
            return RedirectToAction("ProductManagement");
        }

        TempData["error"] = "Error(s) found in the form.";
        ViewBag.StatusList = new SelectList(productStatus);
        ViewBag.CategoryList = new SelectList(db.Categories.Where(c => c.Status == "Active"), "Id", "Name");
        return View(vm);
    }

    // POST: AdmProduct/Delete
    [Authorize(Roles = "Root, Manager")]
    [HttpPost("/AdmProduct/Delete/{id}")]
    public IActionResult Delete(int? id)
    {
        var product = db.Products
        .Include(p => p.ProductVariationOptions)
        .ThenInclude(pvo => pvo.OrderItems)
        .ThenInclude(oi => oi.Order)
        .FirstOrDefault(p => p.Id == id);

        if (product != null)
        {
            product.Status = "Terminated";

            foreach (var option in product.ProductVariationOptions)
            {
                option.Status = "Terminated";

                // Delete related OrderItems (Preparing) with Order (Active)
                var orderItemsToRemove = option.OrderItems
                    .Where(oi => oi.Status == "Preparing" && oi.Order.Status == "Active")
                    .ToList();

                db.OrderItems.RemoveRange(orderItemsToRemove);
            }

            db.SaveChanges();

            TempData["success"] = "(Id: " + id + ", " + product.Name + ") deleted successfully.";
        }
        else
        {
            TempData["error"] = "(Id: " + id + ") not found.";
        }

        return RedirectToAction("ProductManagement");
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult DeleteMany(int[] ids)
    {
        var products = db.Products
            .Include(p => p.ProductVariationOptions)
            .ThenInclude(pvo => pvo.OrderItems)
            .ThenInclude(oi => oi.Order)
            .Where(p => ids.Contains(p.Id))
            .ToList();

        foreach (var product in products)
        {
            product.Status = "Terminated";

            foreach (var option in product.ProductVariationOptions)
            {
                option.Status = "Terminated";

                // Delete related OrderItems (Preparing) with Order (Active)
                var orderItemsToRemove = option.OrderItems
                    .Where(oi => oi.Status == "Preparing" && oi.Order.Status == "Active")
                    .ToList();
                db.OrderItems.RemoveRange(orderItemsToRemove);
            }
        }

        db.SaveChanges();

        TempData["success"] = $"{products.Count} record(s) deleted successfully.";
        return Redirect(Request.Headers.Referer.ToString());
    }
}