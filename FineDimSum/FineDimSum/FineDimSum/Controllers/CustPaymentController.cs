using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

using Stripe.Checkout;
using FineDimSum.Models;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text;
using Stripe.V2;

namespace FineDimSum.Controllers;


public class StripeSettings
{
    public string SecretKey { get; set; }
    public string PublicKey { get; set; }
}

public class CustPaymentController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;
    private readonly StripeSettings _stripeSettings;


    public CustPaymentController(IOptions<StripeSettings> stripeSettings, DB db, IWebHostEnvironment en , Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
        _stripeSettings = stripeSettings.Value;
    }

    // GET: CustPayment/Index
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult CreateCheckoutSession(String amount, int orderId)
    {

        try
        {
            // Stripe configuration
            var currency = "myr";
            var successUrl = Url.Action("ProcessPaymentSuccess", "CustPayment", new { orderId }, Request.Scheme);
            var cancelUrl = Url.Action("ProcessPaymentFail", "CustPayment", new { orderId }, Request.Scheme);
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = Convert.ToInt32(amount),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Order Payment",
                            Description = $"Payment for Order ID: {orderId}"
                        }
                    },
                    Quantity = 1
                }
            },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }
        catch (Exception ex)
        {
            // Log error and redirect back
            Debug.WriteLine($"Stripe Session Creation Error: {ex.Message}");
            TempData["error"] = "Payment session creation failed. Please try again.";
            return RedirectToAction("Index", "CustOrder", new { orderId });
        }
    }


    public IActionResult ProcessPaymentSuccess(int orderId)
    {
        // Retrieve the active order
        var order = db.Orders
            .Include(o => o.RestTable)
            .FirstOrDefault(o => o.Id == orderId);

        if (order == null)
        {
            TempData["error"] = "Order not found or already processed.";
            return RedirectToAction("Index", "CustOrder", new { orderId });
        }

        // Retrieve points used from session and apply discount
        int pointsUsed = HttpContext.Session.GetInt32("PointsUsed") ?? 0;
        HttpContext.Session.Remove("PointsUsed");
        ApplyPointsDiscount(order, pointsUsed);

        // Mark order as paid and update table status
        order.Status = "Paid";
        if (order.RestTable != null)
        {
            order.RestTable.Status = "Available";
        }

        // Save payment details
        AddPaymentRecord(order);

        // Reward points to the member and send receipt email
        RewardMemberPointsAndSendEmailWithAttachments(order, orderId);

        // Save changes to the database
        db.SaveChanges();

        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId");

        // Inform the user
        TempData["success"] = "Thank you for your payment! Your order has been completed successfully.";
        return RedirectToAction("Index", "CustProduct");
    }

    private void ApplyPointsDiscount(Order order, int pointsUsed)
    {
        if (pointsUsed > 0)
        {
            var member = db.Members.FirstOrDefault(m => m.Id == order.UserId && m.Status == "Active");
            if (member != null)
            {
                decimal discount = pointsUsed / 100M;
                member.Points -= pointsUsed;
                order.DiscountAmount = discount;
                order.Total = Math.Round(order.Subtotal + order.SST + order.ServiceCharge - discount, 2);
            }
        }
    }

    private void AddPaymentRecord(Order order)
    {
        var onlinePayment = new Payment
        {
            Total = order.Total,
            PaymentDateTime = DateTime.Now,
            PaymentMethod = "Online Payment",
            OrderId = order.Id
        };
        db.Payments.Add(onlinePayment);
    }

    private void RewardMemberPointsAndSendEmailWithAttachments(Order order, int orderId)
    {
        var member = db.Members.FirstOrDefault(m => m.Id == order.UserId && m.Status == "Active");
        if (member != null && member.Id != 8) // Exclude guests
        {
            // Reward points
            int earnedPoints = (int)(order.Total * 10);
            member.Points += earnedPoints;

            // Send receipt email with attachments
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
        }
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



    public IActionResult ProcessPaymentFail(int orderId)
    {
        // Retrieve the order
        var order = db.Orders.FirstOrDefault(o => o.Id == orderId);

        if (order == null)
        {
            TempData["error"] = "Order not found. Unable to process payment failure.";
            return RedirectToAction("Index", "CustOrder", new { orderId });
        }


            order.Status = "Active";
            db.SaveChanges(); // Save changes to the database
        

        // Inform the user about the payment failure
        TempData["error"] = "Payment failed. Your order has been set back to active. Please try again.";
        return RedirectToAction("Index", "CustOrder", new { orderId });
    }

}