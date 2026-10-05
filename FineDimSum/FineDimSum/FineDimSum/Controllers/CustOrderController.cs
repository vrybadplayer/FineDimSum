using System.Globalization;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace FineDimSum.Controllers;

//No Auth, as all user can access include guest user, but if not set table cannot access the page
public class CustOrderController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public CustOrderController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }


    public IActionResult Index(int orderId)
    {
        // Retrieve the current table ID and table number from the session
        var restTableIdString = HttpContext.Session.GetString("TableId");
        var tableNo = HttpContext.Session.GetString("TableNo");

        if (string.IsNullOrEmpty(restTableIdString) || !int.TryParse(restTableIdString, out int restTableId))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Fetch the order with the given ID, ensure it is active, and matches the table ID
        var order = db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefault(o => o.Id == orderId && o.Status == "Active" && o.RestTableId == restTableId);

        if (order == null)
        {
            TempData["error"] = $"No active order found for the selected table ({tableNo ?? "Unknown"}).";
            return RedirectToAction("Index", "CustProduct");
        }

        // Retrieve the current user from the session
        var userIdString = HttpContext.Session.GetString("UserId");
        int userId = string.IsNullOrEmpty(userIdString) ? 8 : int.Parse(userIdString);
        var user = db.Users.FirstOrDefault(u => u.Id == userId);

        // Determine if the user is a member and active
        bool isMember = user != null && user.Role == "Member" && user.Status == "Active" && user.Id != 8;
        int memberPoints = 0;

        if (isMember)
        {
            // Fetch member points only if the user is a valid member
            memberPoints = db.Members.Where(m => m.Id == userId).Select(m => m.Points).FirstOrDefault();
        }

        // Prepare the view model
        var viewModel = new OrderDetailsViewModel
        {
            Order = order,
            OrderItems = order.OrderItems.ToList(),
            IsMember = isMember,
            MemberPoints = memberPoints
        };

        return View(viewModel);
    }




    [HttpPost]
    public IActionResult CreateOrder()
    {
        // Retrieve RestTableId and UserId from session
        var restTableId = GetRestTableIdFromSession();
        if (restTableId == null)
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("Index", "CustCart");
        }

        var userId = GetUserIdFromSession();
        var cartItems = GetCartItemsForTable(restTableId.Value);
        if (!cartItems.Any())
        {
            TempData["error"] = "Your cart is empty.";
            return RedirectToAction("Index", "CustCart");
        }

        // **Loop to Detect All Inactive Items**
        var inactiveItems = new List<CartItem>();
        foreach (var cartItem in cartItems)
        {
            if (cartItem.ProductVariationOption.Status != "Active")
            {
                inactiveItems.Add(cartItem); // Add to list of inactive items
            }
        }

        if (inactiveItems.Any())
        {
            // Construct an error message listing all invalid items
            var errorMessage = "The following items are unavailable and cannot be ordered:<br>" +
                               string.Join("<br>", inactiveItems.Select(ci =>
                                   $"{ci.ProductVariationOption.Product.Name} ({ci.ProductVariationOption.Name})"));

            TempData["error"] = errorMessage;

            // Remove all inactive items from the cart
            db.CartItems.RemoveRange(inactiveItems);
            db.SaveChanges();

            return RedirectToAction("Index", "CustCart");
        }

        // Validate Stock
        var (isValid, stockErrorMessage) = ValidateStock(cartItems);
        if (!isValid)
        {
            TempData["error"] = stockErrorMessage;
            return RedirectToAction("Index", "CustCart");
        }

        // Check for an active order
        var existingOrder = GetActiveOrderForTable(restTableId.Value);
        if (existingOrder != null)
        {
            UpdateExistingOrder(existingOrder, cartItems);
            UpdateStock(cartItems); // Update stock only after order items are added

            // Set the existing order ID to session
            HttpContext.Session.SetString("OrderId", existingOrder.Id.ToString());

            TempData["success"] = "Order updated successfully!";
            if (HttpContext.Session.GetString("AdmAddOrder") == "true")
            {
                return RedirectToAction("GoBack", "AdmOrder");
            }
            return RedirectToAction("Index", new { orderId = existingOrder.Id });
        }

        // Create a new order
        var newOrder = CreateNewOrder(userId, restTableId.Value, cartItems);

        if (newOrder == null)
        {
            TempData["error"] = "Failed to create order. Please try again.";
            return RedirectToAction("Index", "CustCart");
        }

        // Set the new order ID to session
        HttpContext.Session.SetString("OrderId", newOrder.Id.ToString());

        UpdateStock(cartItems); // Update stock only after order and order items are successfully created
        TempData["success"] = "Order created successfully!";

        if(HttpContext.Session.GetString("AdmAddOrder") == "true")
        {
            return RedirectToAction("GoBack", "AdmOrder");
        }

        return RedirectToAction("Index", new { orderId = newOrder.Id });
    }

    private (bool isValid, string errorMessage) ValidateStock(List<CartItem> cartItems)
    {
        foreach (var cartItem in cartItems)
        {
            var productVariation = cartItem.ProductVariationOption;

            // Check if the requested quantity exceeds the available stock
            if (productVariation.StockQuantity < cartItem.Quantity)
            {
                string errorMessage = $"Insufficient stock for '{productVariation.Product.Name}' " +
                                      $"(Variation: {productVariation.Name}). " +
                                      $"Available: {productVariation.StockQuantity}, Requested: {cartItem.Quantity}.";
                return (false, errorMessage); // Return false with a specific error message
            }
        }

        return (true, string.Empty); // Return true if all items have sufficient stock
    }

    private void UpdateStock(List<CartItem> cartItems)
    {
        foreach (var cartItem in cartItems)
        {
            // Ensure the ProductVariationOption is tracked
            var productVariation = db.ProductVariationOptions.FirstOrDefault(pvo => pvo.Id == cartItem.ProductVariationOptionId);

            if (productVariation == null)
            {
                throw new InvalidOperationException($"Product variation with ID {cartItem.ProductVariationOptionId} not found.");
            }

            // Deduct stock
            productVariation.StockQuantity -= cartItem.Quantity;

            if (productVariation.StockQuantity < 0)
            {
                throw new InvalidOperationException("Stock quantity cannot be negative.");
            }
        }

        db.SaveChanges(); // Save stock updates to the database
    }

    private int? GetRestTableIdFromSession()
    {
        var restTableIdString = HttpContext.Session.GetString("TableId");

        if (!int.TryParse(restTableIdString, out var restTableId))
        {
            return null; // Invalid or missing TableId in the session
        }

        // Ensure the table exists and is not "Unavailable"
        var table = db.RestTables.FirstOrDefault(rt => rt.Id == restTableId && rt.Status != "Unavailable");
        if (table == null)
        {
            return null;
        }

        return restTableId;
    }


    private int GetUserIdFromSession()
    {
        var userIdString = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var parsedUserId))
        {
            return 8; // Default to guest user ID
        }

        var user = db.Users.FirstOrDefault(u => u.Id == parsedUserId && u.Status == "Active");
        return user?.Role == "Member" ? parsedUserId : 8;
    }

    private List<CartItem> GetCartItemsForTable(int restTableId)
    {
        return db.CartItems
            .Include(ci => ci.ProductVariationOption)
            .ThenInclude(pvo => pvo.Product)
            .Where(ci => ci.RestTableId == restTableId)
            .ToList();
    }

    private Order? GetActiveOrderForTable(int restTableId)
    {
        return db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefault(o => o.RestTableId == restTableId && o.Status == "Active");
    }

    private void UpdateExistingOrder(Order existingOrder, List<CartItem> cartItems)
    {
        foreach (var cartItem in cartItems)
        {
            // Add a new order item regardless of whether the product variation already exists in the order
            AddOrderItem(existingOrder.Id, cartItem);
        }

        // Recalculate the totals for the updated order
        decimal additionalSubtotal = cartItems.Sum(ci => ci.Quantity * ci.ProductVariationOption.UnitPrice);
        existingOrder.Subtotal = Math.Round(existingOrder.Subtotal + additionalSubtotal, 2);
        existingOrder.SST = Math.Round(existingOrder.Subtotal * 0.06M, 2);
        existingOrder.ServiceCharge = Math.Round(existingOrder.Subtotal * 0.10M, 2);
        existingOrder.Total = Math.Round(existingOrder.Subtotal + existingOrder.SST + existingOrder.ServiceCharge, 2);

        // Remove the cart items after processing
        db.CartItems.RemoveRange(cartItems);
        db.SaveChanges();
    }

    private Order? CreateNewOrder(int userId, int restTableId, List<CartItem> cartItems)
    {
        try
        {
            // Update the table status to "In-Use"
            var restTable = db.RestTables.FirstOrDefault(rt => rt.Id == restTableId);
            if (restTable != null)
            {
                restTable.Status = "In-Use";
            }

            decimal subtotal = Math.Round(cartItems.Sum(ci => ci.Quantity * ci.ProductVariationOption.UnitPrice), 2);
            decimal sst = Math.Round(subtotal * 0.06M, 2);
            decimal serviceCharge = Math.Round(subtotal * 0.10M, 2);
            decimal total = Math.Round(subtotal + sst + serviceCharge, 2);

            var newOrder = new Order
            {
                CreatedDateTime = DateTime.Now,
                Subtotal = subtotal,
                SST = sst,
                ServiceCharge = serviceCharge,
                DiscountAmount = 0,
                Total = total,
                Status = "Active",
                UserId = userId,
                RestTableId = restTableId
            };

            db.Orders.Add(newOrder);
            db.SaveChanges(); // Save the new order to generate its ID

            foreach (var cartItem in cartItems)
            {
                AddOrderItem(newOrder.Id, cartItem);
            }

            db.CartItems.RemoveRange(cartItems); // Clear the cart items after order creation
            db.SaveChanges();

            return newOrder;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating order: {ex.Message}");
            return null;
        }
    }



    private void AddOrderItem(int orderId, CartItem cartItem)
    {
        string imageFileName;

        // Generate a unique file name and check for uniqueness
        do
        {
            string uniqueString = $"{orderId}_{Guid.NewGuid().ToString("N")}";
            imageFileName = $"{uniqueString}.jpg";
        } while (db.OrderItems.Any(oi => oi.OrderId == orderId && oi.Image == imageFileName));

        CopyImage(
            sourceDirectory: "images/AdmProduct/ProductVariationOption",
            destinationDirectory: "images/Order",
            sourceFileName: cartItem.ProductVariationOption.Image,
            destinationFileName: imageFileName
        );

        var newOrderItem = new OrderItem
        {
            ProductName = cartItem.ProductVariationOption.Product.Name,
            VariationOptionName = cartItem.ProductVariationOption.Name,
            UnitPrice = cartItem.ProductVariationOption.UnitPrice,
            Quantity = cartItem.Quantity,
            Remark = cartItem.Remark,
            Image = imageFileName,
            Status = "Preparing",
            ProductVariationOptionId = cartItem.ProductVariationOptionId,
            OrderId = orderId
        };

        db.OrderItems.Add(newOrderItem);
    }

    private void CopyImage(string sourceDirectory, string destinationDirectory, string sourceFileName, string destinationFileName)
    {
        string sourcePath = Path.Combine("wwwroot", sourceDirectory, sourceFileName);
        string destinationPath = Path.Combine("wwwroot", destinationDirectory, destinationFileName);

        try
        {
            string destinationDir = Path.GetDirectoryName(destinationPath);
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            if (System.IO.File.Exists(sourcePath))
            {
                System.IO.File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            else
            {
                // Log and skip copying
                Console.WriteLine($"Source file not found: {sourcePath}. Skipping image copy.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error copying file from {sourcePath} to {destinationPath}: {ex.Message}");
        }
    }


    [HttpPost]
    public IActionResult ConfirmPayment(int orderId, string paymentMethod, int? pointsUsed)
    {
        // Validate paymentMethod
        if (string.IsNullOrEmpty(paymentMethod))
        {
            TempData["error"] = "Please select a payment method before proceeding.";
            return RedirectToAction("Index", new { orderId });
        }

        // Retrieve the active order
        var order = db.Orders.Include(o => o.OrderItems).FirstOrDefault(o => o.Id == orderId && o.Status == "Active");
        if (order == null)
        {
            TempData["error"] = "Order not found or not active.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Handle undelivered items
        if (order.OrderItems.Any(item => item.Status != "Delivered"))
        {
            var undeliveredItems = order.OrderItems
                .Where(item => item.Status != "Delivered")
                .Select(item => $"{item.ProductName} ({item.VariationOptionName}) - {item.Status}")
                .ToList();

            TempData["error"] = "The following order items are not yet delivered:<br>" +
                                string.Join("<br>", undeliveredItems) +
                                ".<br>Please ask staff to cancel undelivered items before proceeding with payment.";
            return RedirectToAction("Index", new { orderId });
        }

        // Handle payment with points
        var member = db.Members.FirstOrDefault(m => m.Id == order.UserId);
        if (pointsUsed.HasValue && member != null && member.Status == "Active" && member.Id != 8)
        {
            // Condition: Insufficient points
            if (pointsUsed > member.Points)
            {
                TempData["error"] = "Insufficient points.";
                return RedirectToAction("Index", new { orderId });
            }

            // Condition: Points usage minimum value
            if (pointsUsed.Value < 100)
            {
                TempData["error"] = "A minimum of 100 points is required to apply.";
                return RedirectToAction("Index", new { orderId });
            }

            // Condition: Minimum spend of RM50
            if (order.Subtotal < 50M)
            {
                TempData["error"] = "Points can only be applied for orders with a minimum subtotal of RM50.";
                return RedirectToAction("Index", new { orderId });
            }

            // Calculate the discount based on points
            decimal discount = pointsUsed.Value / 100M; // 100 points = RM1

            // If points cover the total order amount
            if (discount >= order.Total)
            {
                member.Points -= (int)(order.Total * 100); // Deduct only the points needed to cover the total
                order.DiscountAmount = order.Total; // Full discount applied
                order.Total = 0; // Set total to 0
                order.Status = "Paid"; // Mark the order as Paid

                // Update table status
                var restTable = db.RestTables.FirstOrDefault(t => t.Orders.Any(o => o.Id == orderId));
                if (restTable != null)
                {
                    restTable.Status = "Available";
                }

                // Add payment record
                var payment = new Payment
                {
                    Total = 0,
                    PaymentDateTime = DateTime.Now,
                    PaymentMethod = "Pay at Counter",
                    OrderId = order.Id
                };
                db.Payments.Add(payment);

                // Send email receipt
                try
                {
                    var items = db.OrderItems.Where(oi => oi.OrderId == orderId).ToList();
                    var mail = GenerateReceiptEmailWithAttachments(member, order, items);

                    hp.SendEmail(mail);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending receipt email: {ex.Message}");
                }

                db.SaveChanges();

                TempData["success"] = "Order successfully paid with points. Thank you!";

                // Perform sign-out actions
                hp.SignOut();
                HttpContext.Session.Remove("UserId"); // Remove UserId from session

                return RedirectToAction("Index", "CustProduct");
            }
            else
            {
                // Apply partial discount if points do not cover the total
                member.Points -= pointsUsed.Value;
                order.DiscountAmount = discount;
                order.Total = Math.Round(order.Subtotal + order.SST + order.ServiceCharge - discount, 2);
            }
        }

        if (paymentMethod == "Online")
        {
            // Save the pointsUsed in session
            HttpContext.Session.SetInt32("PointsUsed", pointsUsed ?? 0); // Store points used

            // Calculate the total amount as a string
            decimal orderTotal = order.Total; // Assuming order.Total is a decimal like 16.82M

            // Multiply by 100 and round to ensure valid smallest unit
            int smallestUnitAmount = (int)(orderTotal * 100);

            // Convert to string
            string totalAmount = smallestUnitAmount.ToString();

            return RedirectToAction("CreateCheckoutSession", "CustPayment", new { amount = totalAmount, orderId });
        }


        // Handle Pay at Counter
        var table = db.RestTables.FirstOrDefault(t => t.Orders.Any(o => o.Id == orderId));
        if (paymentMethod == "Counter" && table != null)
        {
            table.Status = "In-Use";
        }

        order.Status = "Unpaid";
        db.SaveChanges();

        TempData["success"] = "Order updated. Please proceed to pay at the counter.";

        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId"); // Remove UserId from session

        return RedirectToAction("Index", "CustProduct");
    }


    private MailMessage GenerateReceiptEmailWithAttachments(Member member, Order order, List<OrderItem> items)
    {
        var mail = new MailMessage
        {
            Subject = "E-Receipt",
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(member.Email, member.Username));

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine(@$"
    <h1>Fine Dim Sum Receipt</h1>
    <p>Dear {member.Username},</p>
    <br>
    <p>Thank you for dining in our restaurant.</p>
    <p>Here is your receipt:</p>
    <br>");

        foreach (var item in items)
        {
            decimal total = item.UnitPrice * item.Quantity;

            // Attach item image
            var filePath = Path.Combine(en.WebRootPath, "images/Order", item.Image);
            if (!string.IsNullOrEmpty(filePath))
            {
                var attachment = new Attachment(filePath);
                string contentId = Guid.NewGuid().ToString();
                attachment.ContentId = contentId;
                mail.Attachments.Add(attachment);

                bodyBuilder.AppendLine(@$"
            <div>
                <img src=""cid:{contentId}"" alt=""Item Image"" style=""width: 100px; height: 100px;"">
                <p>Name: {item.ProductName}, Unit Price: {item.UnitPrice}, Quantity: {item.Quantity}, Total: {total}</p>
            </div>");
            }
            else
            {
                bodyBuilder.AppendLine(@$"
            <div>
                <p>Name: {item.ProductName}, Unit Price: {item.UnitPrice}, Quantity: {item.Quantity}, Total: {total}</p>
            </div>");
            }
        }

        bodyBuilder.AppendLine(@$"
    <br>
    <p>Date: {DateTime.Now}</p>
    <br>
    <p>Subtotal: {order.Subtotal}</p>
    <p>SST: {order.SST}</p>
    <p>Discount Amount: {order.DiscountAmount}</p>
    <p>Service Charge: {order.ServiceCharge}</p>
    <p>Total: {order.Total}</p>
    <p>Status: {order.Status}</p>
    <br>
    <p>We hope to see you again!</p>");

        mail.Body = bodyBuilder.ToString();
        return mail;
    }

    //GET: CustOrder/PastOrders
    public IActionResult PastOrders(string? search, int page = 1)
    {
        // Retrieve the current user ID from session
        var userIdString = HttpContext.Session.GetString("UserId");
        int userId = string.IsNullOrEmpty(userIdString) ? 8 : int.Parse(userIdString);

        // Ensure the user is a valid member
        var user = db.Users.FirstOrDefault(u => u.Id == userId);

        if (user == null || user.Id == 8 || user.Role != "Member" || user.Status != "Active")
        {
            TempData["error"] = "You need to be a member to view past orders.";
            return RedirectToAction("Index", "CustProduct");
        }

        // (1) Searching ------------------------
        ViewBag.Search = search = search?.Trim() ?? "";

        // Fetch past orders for the user
        var pastOrdersQuery = db.Orders
            .Where(o => o.UserId == userId && o.Status == "Paid");

        // Apply search filter if search is not empty
        if (!string.IsNullOrEmpty(search))
        {
            pastOrdersQuery = pastOrdersQuery.Where(o =>
                o.Id.ToString().Contains(search) || // Search by Order ID
                o.OrderItems.Any(oi => oi.ProductName.Contains(search)) // Search by Order Item Name
            );
        }

        var pastOrders = pastOrdersQuery
            .OrderByDescending(o => o.CreatedDateTime)
            .Select(o => new PastOrderViewModel
            {
                OrderId = o.Id,
                OrderDate = o.CreatedDateTime.ToString("dd-MM-yyyy"),
                FirstItemImage = o.OrderItems.FirstOrDefault().Image,
                FirstItemName = o.OrderItems.FirstOrDefault().ProductName,
                FirstItemQuantity = o.OrderItems.FirstOrDefault().Quantity,
                FirstItemPrice = o.OrderItems.FirstOrDefault().UnitPrice,
                OrderTotal = o.Total
            })
            .ToList();

        // (2) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { search, page = 1 });
        }

        var m = pastOrders.ToPagedList(page, 6);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { search, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_PastOrders", m);
        }

        return View(m);
    }


    // GET: CustOrder/OrderDetails
    public IActionResult PastOrderDetails(int orderId)
    {
        // Retrieve the current user ID from session
        var userIdString = HttpContext.Session.GetString("UserId");
        int userId = string.IsNullOrEmpty(userIdString) ? 8 : int.Parse(userIdString);

        // Ensure the user is valid and logged in
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null || user.Id == 8 || user.Role != "Member" || user.Status != "Active")
        {
            TempData["error"] = "You need to be an active member to view order details.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Fetch the order and related details
        var order = db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefault(o => o.Id == orderId && o.UserId == userId && o.Status == "Paid");

        if (order == null)
        {
            TempData["error"] = "Order not found or you don't have access.";
            return RedirectToAction("PastOrders", "CustOrder");
        }

        // Map the order and its items to the view model
        var orderDetails = new PastOrderDetailsViewModel
        {
            OrderId = order.Id,
            OrderDate = order.CreatedDateTime.ToString("dd-MM-yyyy"),
            Subtotal = order.Subtotal,
            Tax = order.SST,
            ServiceCharge = order.ServiceCharge,
            Discount = order.DiscountAmount,
            Total = order.Total,
            Items = order.OrderItems.Select(oi => new PastOrderItemViewModel
            {
                ProductImage = oi.Image,
                ProductName = oi.ProductName,
                VariationName = oi.VariationOptionName,
                Remark = oi.Remark,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                FinalPrice = oi.Quantity * oi.UnitPrice
            }).ToList()
        };

        return View(orderDetails);
    }
}