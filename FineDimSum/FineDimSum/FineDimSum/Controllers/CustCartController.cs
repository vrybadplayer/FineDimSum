using FineDimSum.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineDimSum.Controllers;

//No Auth, as all user can access include guest user, but if not set table cannot access the page
public class CustCartController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;

    public CustCartController(DB db, IWebHostEnvironment en)
    {
        this.db = db;
        this.en = en;
    }

    [HttpPost]
    public IActionResult AddToCart(
        int productVariationOptionId,
        int quantity,
        string? remark,
        int restTableId
        )
    {
        // Retrieve UserId from session
        var userIdString = HttpContext.Session.GetString("UserId");
        int? userId = null;

        if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int parsedUserId))
        {
            // Check if the user is in the "Member" role
            var user = db.Users.FirstOrDefault(u => u.Id == parsedUserId);
            if (user != null && user.Role == "Member" && user.Status == "Active")
            {
                userId = parsedUserId; // Logged-in member's UserId
            }
            else
            {
                userId = 8; // Set to guest UserId
            }
        }
        else
        {
            userId = 8; // Default to guest UserId
        }

        // Validate RestTableId
        var restTable = db.RestTables.FirstOrDefault(rt => rt.Id == restTableId && rt.Status != "Unavailable");
        if (restTable == null)
        {
            TempData["error"] = "Invalid table. Please select an valid table.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Validate ProductVariationOptionId
        var productVariationOption = db.ProductVariationOptions.FirstOrDefault(pvo =>
            pvo.Id == productVariationOptionId && pvo.Status == "Active");
        if (productVariationOption == null)
        {
            TempData["error"] = "Invalid product. Please select a valid product.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Validate Quantity
        if (productVariationOption.StockQuantity == 0)
        {
            TempData["error"] = $"The product is out of stock.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Validate Quantity
        if (quantity < 1 || quantity > productVariationOption.StockQuantity)
        {
            TempData["error"] = $"Quantity must be between 1 and {productVariationOption.StockQuantity}.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Set default remark if null or empty
        remark = string.IsNullOrWhiteSpace(remark) ? "-" : remark;

        // Validate Remark length
        if (remark.Length > 50)
        {
            TempData["error"] = "Remark cannot exceed 50 characters.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Check if the current cart items on the same table exceed stock quantity
        var currentCartItems = db.CartItems
            .Where(ci => ci.RestTableId == restTableId && ci.ProductVariationOptionId == productVariationOptionId)
            .ToList();

        var currentTotalQuantity = currentCartItems.Sum(ci => ci.Quantity);
        if (currentTotalQuantity + quantity > productVariationOption.StockQuantity)
        {
            TempData["error"] = "Adding this quantity exceeds the available stock.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Add or Update CartItem
        var existingCartItem = currentCartItems.FirstOrDefault(ci => ci.UserId == userId);
        if (existingCartItem != null)
        {
            // Update Quantity
            existingCartItem.Quantity += quantity;

            // Overwrite the remark only if a new remark is provided
            if (!string.IsNullOrWhiteSpace(remark) && remark != "-")
            {
                existingCartItem.Remark = remark;
            }
        }
        else
        {
            // Create a new CartItem
            var newCartItem = new CartItem
            {
                ProductVariationOptionId = productVariationOptionId,
                Quantity = quantity,
                Remark = remark,
                RestTableId = restTableId,
                UserId = userId // Guest or logged-in user
            };
            db.CartItems.Add(newCartItem);
        }

        // Save changes to the database
        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            TempData["error"] = "Failed to add item to cart.";
            Console.WriteLine($"Error: {ex.Message}");
            return RedirectToAction("Index", "CustProduct");
        }
        TempData["success"] = "Item added to cart successfully.";

        // Get the count of cart items for the specified RestTableId
        int totalCartItemCount = db.CartItems.Count(ci => ci.RestTableId == restTableId);

        // Set the total cart item count to a session
        HttpContext.Session.SetString("TotalCartItemCount", totalCartItemCount.ToString());

        return RedirectToAction("Index", "CustProduct");
    }

    // GET: CustCart/Index
    public IActionResult Index()
    {
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        int restTableId = int.Parse(restTableIdString);

        var cartItems = db.CartItems
            .Where(ci => ci.RestTableId == restTableId)
            .Include(ci => ci.ProductVariationOption)
            .Include(ci => ci.ProductVariationOption.Product)
            .ToList();

        return View(cartItems.Where(ci => ci.ProductVariationOption.Status == "Active"));
    }

    // POST: CustCart/RemoveFromCart
    [HttpPost]
    public IActionResult RemoveFromCart(int cartItemId)
    {
        // Retrieve the RestTableId from the session
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString) || !int.TryParse(restTableIdString, out int restTableId))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Check if the cart item exists and matches the RestTableId
        var cartItem = db.CartItems
            .Include(ci => ci.ProductVariationOption)
            .Include(ci => ci.ProductVariationOption.Product)
            .FirstOrDefault(ci => ci.Id == cartItemId && ci.RestTableId == restTableId);

        if (cartItem == null)
        {
            // If the item does not exist, return the partial view with the current cart items for the table
            TempData["error"] = "Cart item not found.";
            var updatedCartItems = db.CartItems
                .Where(ci => ci.RestTableId == restTableId)
                .Include(ci => ci.ProductVariationOption)
                .Include(ci => ci.ProductVariationOption.Product)
                .ToList();
            return PartialView("_CartItemList", updatedCartItems);
        }

        // Remove the cart item from the database
        db.CartItems.Remove(cartItem);
        db.SaveChanges();

        // Fetch updated cart items for the partial view
        var remainingCartItems = db.CartItems
            .Where(ci => ci.RestTableId == restTableId)
            .Include(ci => ci.ProductVariationOption)
            .Include(ci => ci.ProductVariationOption.Product)
            .ToList();

        return PartialView("_CartItemList", remainingCartItems);
    }


    // POST: CustCart/ClearCart
    [HttpPost]
    public IActionResult ClearCart()
    {
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString))
        {
            if (Request.IsAjax())
                return BadRequest("Please select a table first.");

            TempData["error"] = "Please select a table first.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        int restTableId = int.Parse(restTableIdString);
        var cartItems = db.CartItems.Where(ci => ci.RestTableId == restTableId).ToList();

        db.CartItems.RemoveRange(cartItems);
        db.SaveChanges();

        if (Request.IsAjax())
        {
            var updatedCartItems = db.CartItems
                .Where(ci => ci.RestTableId == restTableId)
                .Include(ci => ci.ProductVariationOption)
                .Include(ci => ci.ProductVariationOption.Product)
                .ToList();

            return PartialView("_CartItemList", updatedCartItems); // Ensure you have a partial view named _CartItemList
        }
        return RedirectToAction("Index");
    }

    // POST: CustCart/UpdateQuantity
    [HttpPost]
    public IActionResult UpdateQuantity(int cartItemId, int quantity)
    {
        // Check if the cart item exists
        var cartItem = db.CartItems.FirstOrDefault(ci => ci.Id == cartItemId);
        if (cartItem == null)
        {
            TempData["error"] = "The specified cart item does not exist.";
            return RedirectToAction("Index");
        }

        // Retrieve the associated product variation option
        var productVariationOption = db.ProductVariationOptions.FirstOrDefault(pvo => pvo.Id == cartItem.ProductVariationOptionId);
        if (productVariationOption == null)
        {
            TempData["error"] = "The associated product variation option could not be found.";
            return RedirectToAction("Index");
        }

        // Validate the quantity
        if (quantity < 1)
        {
            TempData["error"] = "Quantity must be at least 1.";
            return RedirectToAction("Index");
        }

        if (quantity > productVariationOption.StockQuantity)
        {
            TempData["error"] = $"Quantity exceeds the available stock. Only {productVariationOption.StockQuantity} items are in stock.";
            return RedirectToAction("Index");
        }

        // Check if the quantity is unchanged
        if (cartItem.Quantity == quantity)
        {
            TempData["error"] = "The quantity remains unchanged.";
            return RedirectToAction("Index");
        }

        // Update the quantity
        cartItem.Quantity = quantity;
        db.SaveChanges();

        TempData["success"] = "Quantity updated successfully.";

        // Return the updated cart items as a partial view
        return RedirectToAction("Index");
    }


    // POST: CustCart/UpdateRemark
    [HttpPost]
    public IActionResult UpdateRemark(int cartItemId, string remark)
    {
        // Validate the remark length
        if (remark.Length > 50)
        {
            TempData["error"] = "Remark must not exceed 50 characters.";
            return RedirectToAction("Index");
        }

        // Set default remark if null or empty
        remark = string.IsNullOrWhiteSpace(remark) ? "-" : remark;

        // Retrieve the cart item
        var cartItem = db.CartItems.FirstOrDefault(ci => ci.Id == cartItemId);
        if (cartItem == null)
        {
            return NotFound("The specified cart item does not exist.");
        }

        // Update the remark
        cartItem.Remark = remark.Trim();
        db.SaveChanges();

        TempData["success"] = "Remark updated successfully.";

        return RedirectToAction("Index");
    }
}