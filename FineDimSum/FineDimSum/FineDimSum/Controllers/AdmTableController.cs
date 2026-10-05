using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;

namespace FineDimSum.Controllers;

[Authorize(Roles = "Root, Manager")]
public class AdmTableController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AdmTableController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;   
    }

    // GET: AdmTable/TableManagement
    public IActionResult TableManagement(string? search, string? status, string? sort, string? dir, int page = 1)
    {
        //Count table number
        ViewBag.TotalTables = db.RestTables.Count(t => t.Status != "Terminated");

        // (1) Searching ------------------------
        ViewBag.Search = search = search?.Trim() ?? "";
        var searched = db.RestTables.Where(t => t.TableNo.Contains(search) && t.Status != "Terminated");

        // (2) Filtering ------------------------
        ViewBag.Status = status;

        // Check if status is null or empty, if so display all records
        var filtered = string.IsNullOrEmpty(status)
            ? searched
            : searched.Where(t => t.Status == status && t.Status != "Terminated");



        // (3) Sorting ------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<RestTable, object> fn = sort switch
        {
            "Id" => t => t.Id,
            "TableNo" => t => t.TableNo,
            "Status" => t => t.Status,
            _ => t => t.Id,
        };

        var sorted = dir == "des" ?
             filtered.OrderByDescending(fn) :
             filtered.OrderBy(fn);

        // (4) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { search, status, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { search, status, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_TableManagement", m);
        }

        return View(m);
    }

    [HttpPost]
    public IActionResult SetTable(int? tableId)
    {
        // Fetch the table based on the given ID
        var table = db.RestTables.FirstOrDefault(t => t.Id == tableId);

        if (table == null)
        {
            TempData["error"] = "The selected table does not exist. Please choose a valid table.";
            return RedirectToAction("TableManagement");
        }

        // Check if the table is In-Use or Unavailable and display appropriate messages
        if (table.Status == "In-Use")
        {
            TempData["error"] = "The selected table is currently In-Use. Please choose another table.";
            return RedirectToAction("TableManagement");
        }

        if (table.Status != "Available")
        {
            TempData["error"] = "The selected table is Unavailable. Please choose another table.";
            return RedirectToAction("TableManagement");
        }

        // Remove all CartItems under the table
        var cartItems = db.CartItems.Where(ci => ci.RestTableId == tableId).ToList();
        if (cartItems.Any())
        {
            db.CartItems.RemoveRange(cartItems);
            db.SaveChanges();
        }

        // Set TableNo and Id to session
        HttpContext.Session.SetString("TableId", table.Id.ToString());
        HttpContext.Session.SetString("TableNo", table.TableNo);

        TempData["success"] = "Table has been set successfully.";

        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId"); // Remove UserId from session
        HttpContext.Session.SetString("Username", "Guest");


        // Redirect to another controller/action
        return RedirectToAction("Index", "CustProduct");
    }

    // POST: AdmTable/ViewCustomerSide
    [HttpPost]
    public IActionResult ViewCustomerSide(int? tableId)
    {
        // Fetch the table based on the given ID
        var table = db.RestTables.FirstOrDefault(t => t.Id == tableId);

        if (table == null)
        {
            TempData["error"] = "The selected table does not exist. Please choose a valid table.";
            return RedirectToAction("TableManagement");
        }

        // Ensure the table is "In-Use"
        if (table.Status != "In-Use")
        {
            TempData["error"] = "The selected table is not currently In-Use. Please choose a valid table.";
            return RedirectToAction("TableManagement");
        }

        // Check if the table has an active order
        var activeOrder = db.Orders.FirstOrDefault(o => o.RestTableId == tableId && o.Status == "Active");
        if (activeOrder == null)
        {
            TempData["error"] = "The selected table does not have an active order. Please choose another table.";
            return RedirectToAction("TableManagement");
        }

        // Update the UserId for the active order to 8
        activeOrder.UserId = 8;

        // Update the UserId for all cart items associated with the table
        var cartItems = db.CartItems.Where(ci => ci.RestTableId == tableId).ToList();
        foreach (var cartItem in cartItems)
        {
            cartItem.UserId = 8;
        }

        // Save changes to the database
        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            TempData["error"] = "Failed to update user information for the selected table.";
            return RedirectToAction("TableManagement");
        }        

        // Set TableNo and Id to session
        HttpContext.Session.SetString("TableId", table.Id.ToString());
        HttpContext.Session.SetString("TableNo", table.TableNo);
        HttpContext.Session.SetString("OrderId", activeOrder.Id.ToString());

        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId"); // Remove UserId from session
        HttpContext.Session.SetString("Username", "Guest");
        TempData["success"] = "Redirecting to the customer view.";



        // Redirect to the customer product page
        return RedirectToAction("Index", "CustProduct");
    }

    // POST: AdmTable/BatchDelete
    [HttpPost]
    public IActionResult BatchDelete(List<int> selectedTableIds)
    {
        if (selectedTableIds == null || !selectedTableIds.Any())
        {
            TempData["error"] = "No tables selected for deletion.";
            return RedirectToAction("TableManagement");
        }

        var tablesToDelete = db.RestTables.Where(t => selectedTableIds.Contains(t.Id) && t.Status != "Terminated").ToList();

        if (!tablesToDelete.Any())
        {
            TempData["error"] = "No valid tables found to delete.";
            return RedirectToAction("TableManagement");
        }

        // Check for tables that are "In-Use"
        if (tablesToDelete.Any(t => t.Status == "In-Use"))
        {
            TempData["error"] = "Some selected tables are In-Use and cannot be deleted.";
            return RedirectToAction("TableManagement");
        }

        // Update status to "Terminated"
        foreach (var table in tablesToDelete)
        {
            table.Status = "Terminated";
        }

        db.SaveChanges();
        TempData["success"] = $"{tablesToDelete.Count} table(s) successfully deleted.";
        return RedirectToAction("TableManagement");
    }

    // POST: AdmTable/Delete
    [HttpPost]
    public IActionResult Delete(int? tableId)
    {
        var table = db.RestTables.FirstOrDefault(t => t.Id == tableId);

        if (table == null)
        {
            TempData["error"] = "The table is not valid.";
            return RedirectToAction("TableManagement");
        }

        // Prevent deleting tables that are "In-Use"
        if (table.Status == "In-Use")
        {
            TempData["error"] = "Cannot delete a table that is currently In-Use.";
            return RedirectToAction("TableManagement");
        }

        // Update table status
        table.Status = "Terminated";
        db.SaveChanges();

        TempData["success"] = $"Table '{table.TableNo}' successfully deleted.";
        return RedirectToAction("TableManagement");
    }

    // GET: AdmTable/Insert
    public IActionResult Insert()
    {
        return View();
    }

    // POST: AdmTable/Insert
    [HttpPost]
    public IActionResult Insert(TableVM vm)
    {
        //Check for the validation error
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        if(db.RestTables.Any(t => t.TableNo == vm.TableNo))
        {
            ModelState.AddModelError("TableNo", "Table Number already exists.");
            return View(vm);
        }

        var newTable = new RestTable
        {
            TableNo = vm.TableNo,
            Status = vm.Status,
        };

        db.RestTables.Add(newTable);
        db.SaveChanges();

        TempData["success"] = "New table has been added successfully.";
        return RedirectToAction("TableManagement");
    }

    // GET: AdmTable/Update
    public IActionResult Update(int tableId)
    {
        var table = db.RestTables.Find(tableId);

        if (table == null)
        {
            TempData["error"] = "Table not found";
            return RedirectToAction("TableManagement");
        }

        // Prevent updating tables that are "In-Use"
        if (table.Status == "In-Use")
        {
            TempData["error"] = "Cannot update a table that is currently In-Use.";
            return RedirectToAction("TableManagement");
        }

        var vm = new TableVM
        {
            TableId = table.Id,
            TableNo = table.TableNo,
            Status = table.Status,
        };

        ViewBag.TableId = tableId;

        return View(vm);
    }

    // POST: AdmTable/Update
    [HttpPost]
    public IActionResult Update(TableVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm); // Return the view with validation errors
        }

        // Find the table to update using the TableId from the model
        var tableToUpdate = db.RestTables.Find(vm.TableId);

        if (tableToUpdate == null)
        {
            TempData["error"] = "Table not found.";
            return RedirectToAction("TableManagement");
        }

        // Prevent updates to tables that are "In-Use"
        if (tableToUpdate.Status == "In-Use")
        {
            TempData["error"] = "Cannot update a table that is currently In-Use.";
            return RedirectToAction("Update", new { tableId = vm.TableId });
        }

        // Check for unique TableNo (excluding current table)
        if (db.RestTables.Any(t => t.TableNo == vm.TableNo && t.Id != vm.TableId))
        {
            ModelState.AddModelError("TableNo", "Table Number already exists.");
            return View(vm);
        }

        // Check if no changes were made
        if (tableToUpdate.TableNo == vm.TableNo && tableToUpdate.Status == vm.Status)
        {
            TempData["error"] = "No changes were detected. Please update the details before submitting.";
            return RedirectToAction("Update", new { tableId = vm.TableId });
        }

        // Update table details
        tableToUpdate.TableNo = vm.TableNo;
        tableToUpdate.Status = vm.Status;

        db.SaveChanges();

        TempData["success"] = "Table updated successfully.";
        return RedirectToAction("TableManagement");
    }

}