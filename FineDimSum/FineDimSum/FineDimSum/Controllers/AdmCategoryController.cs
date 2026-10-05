using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using X.PagedList.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace FineDimSum.Controllers;

public class AdmCategoryController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    // Status
    private readonly string[] categoryStatus = { "Active", "Inactive" };

    public AdmCategoryController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmCategory/CheckName
    [Authorize(Roles = "Root, Manager")]
    public bool CheckName(string name, int? currentId = null)
    {
        return !db.Categories
            .Where(c => c.Status != "Terminated")
            .Any(c => c.Name == name && (!currentId.HasValue || c.Id != currentId));
    }

    // GET: AdmCategory/CheckNameLength
    [Authorize(Roles = "Root, Manager")]
    public bool CheckNameLength(string name)
    {
        return (name != null && name.Length >= 1 && name.Length <= 200);
    }

    // GET: AdmCategory/CheckDescription
    [Authorize(Roles = "Root, Manager")]
    public bool CheckDescription(string description)
    {
        return (description.Length >= 1 && description.Length <= 200);
    }

    // GET: AdmCategory/CheckStatus
    [Authorize(Roles = "Root, Manager")]
    public bool CheckStatus(string status)
    {
        return categoryStatus.Contains(status);
    }

    // GET: AdmCategory/CategoryManagement
    [Authorize(Roles = "Root, Manager")]
    public IActionResult CategoryManagement(string? category, string? status, string? sort, string? dir, int page = 1)
    {
        ViewBag.StatusList = new SelectList(categoryStatus);
        ViewBag.NumOfCategories = db.Categories.Count(c => c.Status != "Terminated");

        // Status colours
        var statusColors = new Dictionary<string, string>
        {
            { "Active", "status-active" },
            { "Inactive", "status-inactive" },
            { "Terminated", "status-terminated" }
        };

        ViewBag.StatusColor = statusColors.ContainsKey(status ?? "") ? statusColors[status ?? ""] : "";

        // (1) Searching ------------------------
        ViewBag.Name = category = category?.Trim() ?? "";
        ViewBag.Status = status;

        int.TryParse(category, out int categoryId);

        var searched = db.Categories.Where(c =>
            (c.Id == categoryId ||
            c.Name.Contains(category) ||
            c.Description.Contains(category)) &&
            (string.IsNullOrEmpty(status) || c.Status == status) &&
            c.Status != "Terminated"
        );

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Category, object> fn = sort switch
        {
            "Category Id" => c => c.Id,
            "Category Name" => c => c.Name,
            "Category Description" => c => c.Description,
            "Status" => c => c.Status,
            _ => c => c.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { category, status, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { category, status, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_CategoryManagement", m);
        }

        return View(m);
    }

    // GET: AdmCategory/Create
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create()
    {
        ViewBag.StatusList = new SelectList(categoryStatus);
        return View();
    }

    // POST: AdmCategory/Create
    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create(CategoryVM vm)
    {
        if (vm.Name == null)
        {
            ModelState.AddModelError("Name", "Category Name must be between 1 - 100 characters.");
        }
        else
        {
            if (ModelState.IsValid("Name") && !CheckName(vm.Name))
            {
                ModelState.AddModelError("Name", "Duplicated Category Name.");
            }

            if (ModelState.IsValid("Name") && !CheckNameLength(vm.Name))
            {
                ModelState.AddModelError("Name", "Category Name must be between 1 - 100 characters.");
            }
        }

        if (ModelState.IsValid("Description") && !CheckDescription(vm.Description))
        {
            ModelState.AddModelError("Description", "Category Description must be between 1 - 200 characters.");
        }

        if (ModelState.IsValid("Status") && !CheckStatus(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid Status.");
        }

        if (ModelState.IsValid)
        {
            db.Categories.Add(new()
            {
                Name = vm.Name!.Trim(),
                Description = vm.Description.Trim(),
                Status = vm.Status,
            });
            db.SaveChanges();

            TempData["success"] = "New category added successfully.";
            return RedirectToAction("CategoryManagement");
        }

        TempData["error"] = "Error(s) found in the form.";
        ViewBag.StatusList = new SelectList(categoryStatus);
        return View();
    }

    // GET: AdmCategory/Update
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Update(int? id)
    {
        var c = db.Categories.Find(id);

        if (c == null)
        {
            TempData["error"] = "Id not found.";
            return RedirectToAction("CategoryManagement");
        }

        var vm = new CategoryVM
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Status = c.Status,
        };

        ViewBag.StatusList = new SelectList(categoryStatus);
        return View(vm);
    }

    // POST: AdmCategory/Update
    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Update(CategoryVM vm)
    {
        var c = db.Categories.Find(vm.Id);

        if (c == null)
        {
            TempData["error"] = "Id not found.";
            return RedirectToAction("CategoryManagement");
        }

        if (c.Name != "Others")
        {
            if (ModelState.IsValid("Name") && !CheckName(vm.Name!, vm.Id))
            {
                ModelState.AddModelError("Name", "Duplicated Category Name.");
            }

            if (ModelState.IsValid("Name") && !CheckNameLength(vm.Name!))
            {
                ModelState.AddModelError("Name", "Category Name must be between 1 - 100 characters.");
            }
        }

        if (ModelState.IsValid("Description") && !CheckDescription(vm.Description))
        {
            ModelState.AddModelError("Description", "Category Description must be between 1 - 200 characters.");
        }

        if (ModelState.IsValid("Status") && !CheckStatus(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid Status.");
        }

        if (ModelState.IsValid)
        {
            if (c.Name != "Others")
            {
                c.Name = vm.Name!.Trim();
            }
            c.Description = vm.Description.Trim();
            c.Status = vm.Status;
            db.SaveChanges();

            TempData["success"] = "Changes saved successfully.";
            return RedirectToAction("CategoryManagement");
        }

        TempData["error"] = "Error(s) found in the form.";
        ViewBag.StatusList = new SelectList(categoryStatus);
        return View(vm);
    }

    // POST: AdmCategory/Delete
    [HttpPost("/AdmCategory/Delete/{id}")]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Delete(int? id)
    {
        var c = db.Categories.Find(id);

        if (c != null)
        {
            // Others can't be deleted
            if (String.Equals(c.Name, "Others", StringComparison.OrdinalIgnoreCase))
            {
                TempData["error"] = "(Id: " + id + ", " + c.Name + ") cannot be deleted.";
                return RedirectToAction("CategoryManagement");
            }

            c.Status = "Terminated";
            db.SaveChanges();

            TempData["success"] = "(Id: " + id + ", " + c.Name + ") deleted successfully.";
        }

        return RedirectToAction("CategoryManagement");
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult DeleteMany(int[] ids)
    {
        var recordsToUpdate = db.Categories
                            .Where(c => ids.Contains(c.Id) && c.Name != "Others")
                            .ToList();

        int n = recordsToUpdate.Count;

        if (n > 0)
        {
            db.Categories
                  .Where(c => recordsToUpdate.Contains(c))
                  .ExecuteUpdate(s => s.SetProperty(c => c.Status, "Terminated"));

            TempData["success"] = $"{n} record(s) deleted successfully.";
        }
        else
        {
            TempData["error"] = "\"Others\" cannot be deleted.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }
}