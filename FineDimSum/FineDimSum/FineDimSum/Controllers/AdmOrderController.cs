using FineDimSum.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using System.Net.Mail;
using System;
using System.Text;
using System.Collections.Generic;
using X.PagedList.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;
using Microsoft.AspNetCore.Authorization;
using Stripe.Climate;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.Eventing.Reader;
using System.IO;



namespace FineDimSum.Controllers;

public class AdmOrderController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AdmOrderController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmOrder/OrderTable
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult OrderTable()
    {

        List<OrderTableVM> vm = db.Orders
            .Include(o => o.RestTable)
            .Where(o => o.RestTable != null) // Ensure only orders with a linked table are included
            .OrderBy(o => o.RestTableId)
            .Select(o => new OrderTableVM
            {
                OrderID = (o.Id == 0 ? "-" : o.Id.ToString()),
                TableNo = o.RestTable.TableNo,
                Status = o.Status, // Use the order status here
            }).ToList();

        List<OrderTableVM> tables = db.RestTables
            .OrderBy(o => o.Id)
            .Select(o => new OrderTableVM
            {
                OrderID = "-",
                TableNo = o.TableNo,
                Status = o.Status,
            }).ToList();

        // Replace OrderID only when there is a matching active/unpaid order
        foreach (var table in tables)
        {
            var matchingOrder = vm
                .FirstOrDefault(order => order.TableNo == table.TableNo && (order.Status == "Active" || order.Status == "Unpaid"));

            if (matchingOrder != null)
            {
                table.OrderID = matchingOrder.OrderID;
            }
        }

        return View(tables);
    }

    // GET: AdmOrder/CreateOrderForTable
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult CreateOrderForTable(string tableNo)
    {
        // Validate tableNo
        if (string.IsNullOrWhiteSpace(tableNo))
        {
            TempData["error"] = "Table number is required. Please provide a valid table number.";
            return RedirectToAction("TableManagement");
        }

        // Fetch the table based on the given table number
        var table = db.RestTables.FirstOrDefault(t => t.TableNo == tableNo);

        if (table == null)
        {
            TempData["error"] = "The selected table does not exist. Please choose a valid table.";
            return RedirectToAction("TableManagement");
        }

        // Set TableNo, Id, detect Adm Add Order to session
        HttpContext.Session.SetString("TableId", table.Id.ToString());
        HttpContext.Session.SetString("TableNo", table.TableNo);
        HttpContext.Session.SetString("AdmAddOrder", "true");

        // Redirect to another controller/action
        return RedirectToAction("Index", "CustProduct");
    }

    // GET Go to Menu Page
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult AddItem(int orderId, int tableId)
    {
        // Validate the orderId and check if it's active
        var order = db.Orders
            .Include(o => o.RestTable)
            .FirstOrDefault(o => o.Id == orderId); // Ensure the order is active

        if (order == null)
        {
            TempData["error"] = "Order not found or not active.";
            return RedirectToAction("OrderTable");
        }

        if (order.Status != "Active")
        {
            TempData["error"] = "Only active orders can add items.";
            return RedirectToAction("OrderManagement", new { orderId = orderId, tableNo = order.RestTable.TableNo });
        }

        // Validate the tableId matches the order's table
        if (order.RestTable == null || order.RestTable.Id != tableId)
        {
            TempData["error"] = "Order and Table do not match.";
            return RedirectToAction("OrderTable");
        }

        // Set the values to Session
        HttpContext.Session.SetString("TableId", tableId.ToString());
        HttpContext.Session.SetString("TableNo", order.RestTable.TableNo);
        HttpContext.Session.SetString("OrderId", orderId.ToString());
        HttpContext.Session.SetString("AdmAddOrder", "true");

        // Redirect to CustProduct page
        return RedirectToAction("Index", "CustProduct");
    }


    // GET: AdmOrder/GoBack
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult GoBack(string from)
    {
        // Retrieve TableId from session
        string tableIdString = HttpContext.Session.GetString("TableId");

        // Remove session values immediately after retrieval
        HttpContext.Session.Remove("TableId");
        HttpContext.Session.Remove("TableNo");
        HttpContext.Session.Remove("AdmAddOrder");

        // Check if TableId is valid
        if (!string.IsNullOrEmpty(tableIdString) && int.TryParse(tableIdString, out int tableId))
        {
            // Check if there is an active order for the given TableId
            var activeOrder = db.Orders
                .Include(o => o.RestTable)
                .FirstOrDefault(o => o.RestTableId == tableId && o.Status == "Active");

            if (activeOrder != null)
            {
                // Redirect to OrderManagement if an active order exists
                return RedirectToAction("OrderManagement", new { orderId = activeOrder.Id, tableNo = activeOrder.RestTable.TableNo });
            }
        }

        // Default fallback redirection to OrderTable if no active order
        return RedirectToAction("OrderTable");
    }


    // POST: AdmOrder/OrderTable
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult OrderTable(string? tableNo, string? orderId)
    {

        // Check for null or whitespace
        if (string.IsNullOrWhiteSpace(tableNo))
        {
            TempData["error"] = "Input cannot be empty.";
            return RedirectToAction("OrderTable");
        }

        // Post to search Table
        if (tableNo != null && orderId == null)
        {
            var select = db.RestTables.Where(rt => rt.TableNo == tableNo).FirstOrDefault();

            if (select != null)
            {
                List<OrderTableVM> vm = db.Orders
            .Include(o => o.RestTable)
            .Where(o => o.Status != "Unavailable" && o.RestTable.TableNo == tableNo)
            .OrderBy(o => o.RestTableId)
            .Select(o => new OrderTableVM
            {
                OrderID = (o.Id == 0 ? "-" : o.Id.ToString()),
                TableNo = o.RestTable.TableNo,
                Status = o.RestTable.Status,
            }).ToList();

                List<OrderTableVM> tables = db.RestTables
                .Where(o => o.Status != "Unavailable" && o.TableNo == tableNo)
                .OrderBy(o => o.Id)
                .Select(o => new OrderTableVM
                {
                    OrderID = "-",
                    TableNo = o.TableNo,
                    Status = o.Status,
                }).ToList();

                // Replace 
                foreach (var table in tables)
                {
                    var matchingOrder = vm.FirstOrDefault(order => order.TableNo == table.TableNo);
                    if (matchingOrder != null)
                    {
                        table.OrderID = matchingOrder.OrderID;
                    }
                }

                return View(tables);
            }
            else
            {
                ModelState.AddModelError("Table", "Table does not exist.");
                return RedirectToAction("OrderTable");
            }
        }
        return View();

    }

    // GET: AdmOrder/OrderManagement
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult OrderManagement(int? orderId, string? tableNo)
    {

        // Check if orderId and tableNo matches
        if (orderId != null && tableNo != null)
        {
            Models.Order order = db.Orders
                .Include(o => o.RestTable)
                .Where(o => o.Id == orderId)
                .First();

            if (order != null && order.RestTable.TableNo != tableNo)
            {
                TempData["error"] = "Order and Table does not match";
                return RedirectToAction("OrderTable");
            }

        }

        if (orderId != null)
        {
            // Get Order using orderId
            Models.Order ord = db.Orders
                .Where(o => o.Id == orderId)
                .FirstOrDefault();

            // Check if order is active                
            if (ord == null || (ord.Status != "Active" && ord.Status != "Unpaid"))
            {
                // If not, jump back
                TempData["error"] = "Order is not active";
                return RedirectToAction("OrderTable");
            }


            var order = db.Orders.Include(r => r.RestTable).FirstOrDefault(o => o.Id == orderId);
            hp.SetOrderId(orderId);
            ViewBag.Status = order?.Status;
            ViewBag.TableNo = order?.RestTable.TableNo;
            ViewBag.TableId = order?.RestTable.Id;
            ViewBag.TableId = order?.RestTable.Id;
            ViewBag.OrderId = orderId;
        }
        else
        {
            return RedirectToAction("OrderTable");
        }

        List<OrderItemVM> vm = db.OrderItems
            .Include(o => o.Order)
            .Where(o => o.OrderId == orderId)
            .Select(o => new OrderItemVM
            {
                OrderItemId = o.Id,
                Image = o.Image,
                ProductName = o.ProductName,
                VariationOptionName = o.VariationOptionName,
                Quantity = o.Quantity,
                Status = o.Status,
                Remark = o.Remark
            })
            .ToList();

       
        var items = db.OrderItems.Where(oi => oi.Order.Id == orderId);

        if (items.Any())
        {
            // Check if any items are "Delivered"
            ViewBag.HasDelivered = "False";
            foreach (var i in items)
            {
                if (i.Status == "Delivered")
                {
                    ViewBag.HasDelivered = "True";
                }
            }
        }

        // Check if all items are delivered
        ViewBag.AllDelivered = items.All(i => i.Status == "Delivered") ? "True" : "False";


        return View(vm);

    }

    // POST: AdmOrder/OrderManagement
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult OrderManagement(int? orderItemId, string? selectedStatus, string status, int? orderId, string? tableNo)
    {

        // Check if orderId and tableNo matches
        if (orderId != null && tableNo != null)
        {
            Models.Order order = db.Orders
                .Include(o => o.RestTable)
                .Where(o => o.Id == orderId)
                .First();
            
            if(order.RestTable.TableNo != tableNo)
            {
                TempData["error"] = "Order and Table does not match";
                return RedirectToAction("OrderTable"); 
            }

        }

        // Change Status Here
        // ITEM status is changed
        if (orderItemId != null && selectedStatus != null)
        {

            OrderItem? item = db.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);

            // Update DB
            if (item != null)
            {
                item.Status = selectedStatus;
                db.SaveChanges();
            }

        }

        var items = db.OrderItems.Where(oi => oi.Order.Id == orderId);

        if (items.Any())
        {
            // Check if any items are "Delivered"
            ViewBag.HasDelivered = "False";
            foreach (var i in items)
            {
                if (i.Status == "Delivered")
                {
                    ViewBag.HasDelivered = "True";
                }
            }
        }

        // Check if all items are delivered
        ViewBag.AllDelivered = items.All(i => i.Status == "Delivered") ? "True" : "False";

        return RedirectToAction("OrderManagement", new { orderId, tableNo });
    }


    // POST (Cancels Order, Remove OrderItem and Order from DB, then set Table status)
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult CancelOrder(int orderId)
    {
        // Get Order using OrderId
        var order = db.Orders
            .Include(o => o.RestTable)
            .FirstOrDefault(o => o.Id == orderId);


        if (order != null)
        {

            // Get all Order Items
            var items = db.OrderItems
                .Include(oi => oi.ProductVariationOption)
                .Where(oi => oi.OrderId == orderId).ToList();

            // Add stock back
            if (items.Any())
            {
                foreach(var item in items)
                {
                    item.ProductVariationOption.StockQuantity += item.Quantity;
                }
            }


            // Change Order Status
            order.Status = "Cancelled";

            // Change Table Status
            order.RestTable.Status = "Available";

            // Remove OrderItem from DB
            if (items.Any())
            {
                db.OrderItems.RemoveRange(items);
            }

            db.SaveChanges();
            TempData["success"] = "Order Cancelled";

        } else
        {
            TempData["error"] = "Order Cancellation Failure";
        }


        return RedirectToAction("OrderTable");
    }

    // POST: /AdmOrder/CancelItem 
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult CancelItem(int OrderItemId, int orderId)
    {
        // Get the item
        var item = db.OrderItems
            .Where(o => o.Id == OrderItemId && o.OrderId == orderId)
            .FirstOrDefault();

        if (item == null)
        {
            TempData["error"] = "Invalid Item!";
            return RedirectToAction("OrderManagement", new { orderId });
        }

        // Remove Image
        string relativePath = Path.Combine("images/Order", item.Image);
        string filePath = Path.Combine(en.WebRootPath, relativePath);

        // Check if the file exists and delete it
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }


        // Remove from Database
        if (item != null)
        {

            // Add back the stock to DB
            var prodVarOp = db.ProductVariationOptions
                .Where(p => p.Id == item.ProductVariationOptionId)
                .First();

            prodVarOp.StockQuantity += item.Quantity;

            var remove = db.OrderItems
                .First(oi => oi.Id == OrderItemId);
            db.OrderItems.Remove(remove);
            db.SaveChanges();

            TempData["success"] = "Item Cancelled";
        }
        else
        {
            TempData["error"] = "Item Does Not Exist";
        }

        return RedirectToAction("OrderManagement", new { orderId });
    }

    // POST (Change Table status and Order status. Also add History and Receipt)
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff")]
    public IActionResult ConfirmPayment(int orderId)
    {
        // Get Order using OrderId
        Models.Order order = db.Orders
            .Include(o => o.RestTable)
            .First(o => o.Id == orderId);

        // Change Order Status
        order.Status = "Paid";

        // Change Table Status
        order.RestTable.Status = "Available";

        // Add Payment
        Payment payment = new Payment
        {
            Total = order.Total,
            PaymentDateTime = DateTime.Now,
            PaymentMethod = "Pay at Counter",
            OrderId = order.Id
        };

        db.Payments.Add(payment);
        db.SaveChanges();

        // Get UserId
        int userId = db.Orders
            .Where(o => o.Id == orderId)
            .Select(o => o.UserId)
            .FirstOrDefault();

        // Get User
        Member member = db.Members.Where(u => u.Id == userId).First();

        
        if (member.Username != "Guest")
        {

            // Reward points
            int earnedPoints = (int)(order.Total * 10);
            member.Points += earnedPoints;
            db.SaveChanges();

            // Get Order Items
            var items = db.OrderItems.Where(oi => oi.OrderId == orderId);

            // Send Receipt if not Guest
            // Construct Email
            var mail = new MailMessage
            {
                Subject = "Fine Dim Sum Receipt",
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(member.Email, "Fine Dim Sum"));
            mail.Subject = "E-Receipt";
            mail.IsBodyHtml = true;

            var bodyBuilder = new StringBuilder();

            bodyBuilder.AppendLine(@"
                <h1>Fine Dim Sum Receipt</h1>
                <p>Dear " + member.Username + @",</p>
                <br>
                <p>Thank you for dining in our restaurant.</p>
                <p>Here is your receipt:</p>
                <br> ");

            foreach (var item in items)
            {
                decimal total = item.UnitPrice * item.Quantity;

                var path = Path.Combine(en.WebRootPath, "images/Order", item.Image);
                var att = new Attachment(path);

                // Generate a unique ContentId for each image
                string contentId = Guid.NewGuid().ToString();
                att.ContentId = contentId;

                mail.Attachments.Add(att);

                bodyBuilder.AppendLine($@"
                    <div>
                    <img src='cid:{contentId}' alt=""Item Image"" style='width: 100px; height: 100px;'>
                    <p>Name: {item.ProductName}, Unit Price: {item.UnitPrice}, Quantity: {item.Quantity}, Total: {total}</p>
                    </div>  
                ");
            }


            bodyBuilder.AppendLine($@"

                <br>
                <p>Date: {DateTime.Now}<p>
                <br>
                <p>Subtotal: {order.Subtotal}</p>
                <p>SST: {order.SST}</p>
                <p>Discount Amount: {order.DiscountAmount}</p>
                <p>Service Charge: {order.ServiceCharge}</p>
                <p>Total: {order.Total}</p>
                <p>Status: {order.Status}</p>

            ");

            bodyBuilder.AppendLine(@"
                <br>
                <p>We hope to see you again!</p>
            ");


            mail.Body = bodyBuilder.ToString();

            // Send email
            hp.SendEmail(mail);


        }

        TempData["success"] = "Order Completed";

        return RedirectToAction("OrderTable");
    }


    // GET: AdmOrder/OrderItemList
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult OrderItemList()
    {


        string orderStatus = "Preparing";
        ViewBag.OrderStatus = orderStatus;

        ViewBag.CurrentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm");

        List<OrderItemListVM> vm = db.OrderItems
            .Include(o => o.Order)
            .ThenInclude(o => o.RestTable)
           .Where(o => o.Status == orderStatus && o.Order.Status == "Active")
           .OrderBy(o => o.Id)
           .Select(o => new OrderItemListVM
           {
               OrderItemId = o.Id,
               ProductName = o.ProductName,
               VariationOptionName = o.VariationOptionName,
               Quantity = o.Quantity,
               Remark = o.Remark,
               Image = o.Image,
               Status = o.Status,
               TableNo = o.Order.RestTable.TableNo,
           })
           .ToList();

        // Count the items
        ViewBag.PreparingCount = db.OrderItems
        .Where(o => o.Status == "Preparing" && o.Order.Status == "Active")
        .Count();

        ViewBag.ReadyCount = db.OrderItems
        .Where(o => o.Status == "Ready" && o.Order.Status == "Active")
        .Count();

        ViewBag.DeliveredCount = db.OrderItems
        .Where(o => o.Status == "Delivered" && o.Order.Status == "Active")
        .Count();

        return View(vm);
    }

    // POST: AdmOrder/OrderItemList
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult OrderItemList(string? orderStatus, string? selectedStatus, int? orderItemId, string? tableNo)
    {

        if (orderStatus == null)
        {
            orderStatus = "Preparing";
            ViewBag.OrderStatus = orderStatus;
        }
        else
        {
            ViewBag.OrderStatus = orderStatus;
        }

        ViewBag.CurrentDateTime = DateTime.Now.ToString("dd-MM-yyyy HH:mm");

        // ITEM status is changed
        if (orderItemId != null && selectedStatus != null)
        {

            // Check selectedStatus is one of the three
            if (!(selectedStatus == "Preparing" || selectedStatus  == "Ready" || selectedStatus == "Delivered"))
            {
                TempData["error"] = "Invalid Status";
                return RedirectToAction("OrderItemList");
            }

            // Check orderItemId linked with tableNo
            OrderItem? item = db.OrderItems
                .Include(o => o.Order)
                .ThenInclude(o => o.RestTable)
                .Where(o => o.Order.RestTable.TableNo == tableNo && o.Id == orderItemId)
                .FirstOrDefault();

            // Update DB
            if (item != null)
            {
                item.Status = selectedStatus;
                db.SaveChanges();
            } else
            {
                TempData["error"] = "Invalid Order Item";
                return RedirectToAction("OrderItemList");
            }

        }

        // Change VM according to Filter
        // If orderStatus == "Delivered", Only show Today's orders
        List<OrderItemListVM> vm = db.OrderItems
            .Where(o => o.Status == orderStatus && o.Order.Status == "Active" 
            //&& (orderStatus != "Delivered" || o.Order.CreatedDateTime == DateTime.Today)
            )
            .OrderBy(o => o.Id)
            .Select(o => new OrderItemListVM
            {
                OrderItemId = o.Id,
                ProductName = o.ProductName,
                VariationOptionName = o.VariationOptionName,
                Quantity = o.Quantity,
                Remark = o.Remark,
                Image = o.Image,
                Status = o.Status,
                TableNo = o.Order.RestTable.TableNo,
            })
            .ToList();

        // Count the items
        ViewBag.PreparingCount = db.OrderItems
        .Where(o => o.Status == "Preparing" && o.Order.Status == "Active")
        .Count();

        ViewBag.ReadyCount = db.OrderItems
        .Where(o => o.Status == "Ready" && o.Order.Status == "Active")
        .Count();

        ViewBag.DeliveredCount = db.OrderItems
        .Where(o => o.Status == "Delivered" && o.Order.Status == "Active")
        .Count();

        if (Request.IsAjax())
        {
            return PartialView("_OrderItemList", vm);
        }

        // Returns filtered VM
        return View(vm);
    }

    // GET: AdmOrder/OrderHistory 
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult OrderHistory(int page = 1)
    {
        if (page < 1)
        {
            return RedirectToAction(null, new { page = 1 });
        }

        var vm = GetOrderHistory().ToPagedList(page, 4);

        if (page > vm.PageCount && vm.PageCount > 0)
        {
            return RedirectToAction(null, new { page = vm.PageCount });
        }

        return View(vm);
    }

    // POST: AdmOrder/OrderHistory
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult OrderHistory(string orderId)
    {
        int intOrderId = int.Parse(orderId);
        return RedirectToAction("OrderHistoryDetails", new {intOrderId} );
    }

    // GET: AdmOrder/OrderHistoryDetails
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult OrderHistoryDetails(int? intOrderId)
    {
        // no orderId
        if (intOrderId == null)
        {
            TempData["error"] = "Order History Does Not Exist";
            return RedirectToAction("OrderHistory");
        }

        List<OrderHistoryVM> vm = GetOrderHistory();
        var order = db.Orders.Where(o => o.Id == intOrderId).FirstOrDefault();

        // Check if the order exists
        if (order == null)
        {
            TempData["error"] = "Order History Does Not Exist";
            return RedirectToAction("OrderHistory");
        }

        // Check if the order has payment record
        bool orderExistsInVm = vm.Any(record => record.OrderId == intOrderId);

        if (!orderExistsInVm)
        {
            TempData["error"] = "Order History  Does Not Exist";
            return RedirectToAction("OrderHistory");
        }

        // Proceed with further logic if the order exists in vm
        ViewBag.OrderId = intOrderId;  

        List<OrderHistoryDetailsVM1> vm1 = db.OrderItems
            .Where(oi => oi.OrderId == intOrderId)
            .Select(oi => new OrderHistoryDetailsVM1
            {
                ImageURL = oi.Image,
                Name = oi.ProductName,
                Options = oi.VariationOptionName,
                Quantity = oi.Quantity,
                Price = oi.UnitPrice * oi.Quantity,
            })
            .ToList();

        // Get Order Details
        OrderHistoryDetailsVM2 vm2 = db.Payments
            .Where(p => p.OrderId == intOrderId)
            .Select(p => new OrderHistoryDetailsVM2
            {
                PaymentMethod = p.PaymentMethod,
                DateTime = p.PaymentDateTime,
                Subtotal = p.Order.Subtotal,
                Tax = p.Order.SST,
                ServiceCharge = p.Order.ServiceCharge,
                Promotion = p.Order.DiscountAmount,
                Total = p.Order.Total,
            })
            .First();

        OrderHistoryDetailsVM groupedVM = new OrderHistoryDetailsVM
        {
            vm1 = vm1,
            vm2 = vm2,
        };

        return View(groupedVM);
    }

    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public List<OrderHistoryVM> GetOrderHistory() 
    {
        List<OrderHistoryVM> all = db.Orders
            .Include(o => o.RestTable)
            .Include(o => o.Payments)
            .Select(o => new OrderHistoryVM
            {
                OrderId = o.Id,
                DateTime = o.Payments
                    .Where(p => p.OrderId == o.Id)
                    .First().PaymentDateTime,
                TableNo = o.RestTable.TableNo,
                CustomerId = o.User.Id,
            })
            .ToList();

        List<OrderHistoryVM> vm = all
            .Where(record => record.DateTime != null)
            .ToList();

        return vm;
    }
}