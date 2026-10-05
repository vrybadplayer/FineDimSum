using FineDimSum.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net.Mail;

namespace FineDimSum.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AccountController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: Account/Login
    public IActionResult Login(string? ReturnURL)
    {
        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId"); // Remove UserId from session

        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public IActionResult Login(LoginVM vm, string? ReturnURL)
    {
        // Get Guest Record
        Member guest = db.Members.Where(m => m.Username == "Guest").First();

        // Get user (admin or member) record based on email (PK)
        var u = db.Users.FirstOrDefault(u => u.Email == vm.Email && vm.Email != guest.Email);

        // Custom validation -> verify password

        if (u == null || !hp.VerifyPassword(u.Password, vm.Password))
        {
            ModelState.AddModelError("", "Login credentials not matched.");
            TempData["error"] = "Login Credentials Not Matched";
        }

        if (u != null && !hp.VerifyPassword(u.Password, vm.Password))
        {
            u.LoginAttempts -= 1;
            db.SaveChanges();
            // Lock Account
            if (u.LoginAttempts <= 0)
            {
                u.Status = "Inactive";
                db.SaveChanges();
                TempData["error"] = "Account Locked! Check Email To Unblock.";

                // If previous tokens exist, remove them.
                if (db.Tokens.Any(t => t.UserId == u.Id))
                {
                    var removeToken = db.Tokens.FirstOrDefault(t => t.UserId == u.Id);
                    db.Tokens.Remove(removeToken);
                }

                // Send Unblock Email
                // Generate Token Link
                string token = hp.GenerateToken();

                // Store Token Link In DB
                db.Tokens.Add(new()
                {
                    Code = token,
                    Expire = DateTime.Now.AddMinutes(+5),
                    UserId = u.Id,
                });
                db.SaveChanges();


                // Generate full URL that points to /Account/Unblock
                int userId = u.Id;
                var url = Url.Action("Unblock", "Account", new { token, userId }, "https");

                // Email Verification Link To User
                // Construct Email
                var mail = new MailMessage();
                mail.To.Add(new MailAddress(vm.Email, "Fine Dim Sum"));
                mail.Subject = "Account Unblock";
                mail.IsBodyHtml = true;

                mail.Body = $@"
                    <h1>Account Unblock Link<h1>
                    <p>Dear user {u.Username},</p>
                    <br>
                    <p>Your account has been blocked for multiple false login attempts.</p>
                    <p>Click the link below to unblock your accouont:</p>
                    <p>{url}</p>
                    <br>
                    <p>The link will expire at 
                    {DateTime.Now.AddMinutes(+5)}, 
                    so please verify your accounr before the expiry date.</p>
                        ";

                // Send email
                hp.SendEmail(mail);

                return View(vm);
            }

        }

        if (u != null && u.Status == "Inactive")
        {
            ModelState.AddModelError("", "Account Not Active");
            TempData["error"] = "Account Is Not Active";
        }

        if (u != null && u.Username == "Guest")
        {
            ModelState.AddModelError("", "Account Unavailable.");
            TempData["error"] = "Account Unavailable";
        }


        if (ModelState.IsValid)
        {
            TempData["Info"] = "Login successfully.";

            // Sign in
            hp.SignIn(u!.Email, u.Role, vm.RememberMe);
            HttpContext.Session.SetString("UserId", u.Id.ToString());
            HttpContext.Session.SetString("Username", u.Username.ToString());

            // Reset Attempts
            u.LoginAttempts = 3;

            // Settings
            if (u.Role != "Member")
            {
                HttpContext.Session.Remove("TableNo");
                HttpContext.Session.Remove("TableId");
                HttpContext.Session.Remove("OrderId");
            }

            // Check if a member is logged in
            var userIdString = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int userId))
            {
                // Check if the user is a member
                var user = db.Users.FirstOrDefault(u => u.Id == userId);
                if (user != null && user.Role == "Member")
                {
                    // Link CartItem and Order to the login user
                    LinkCartItemsToLoggedInUser();
                    UpdateOrderForLoggedInUser();
                }
            }

            // Save changes to the database
            db.SaveChanges();


            // Handle return URL
            if (!string.IsNullOrEmpty(ReturnURL))
            {
                return Redirect(ReturnURL);
            }


            // RTA based on Role 
            if (u.Role == "Root" || u.Role == "Manager")
            {
                return RedirectToAction("Dashboard", "AdmDashboard");
            }
            else if (u.Role == "Staff")
            {
                return RedirectToAction("OrderTable", "AdmOrder");
            } 
            else if (u.Role == "Chef")
            {
                return RedirectToAction("OrderItemList", "AdmOrder");
            }
            else
            {
                return RedirectToAction("Index", "CustProduct");
            }

        }

        return View(vm);
    }

    // GET: Account/ResendVerification
    public IActionResult ResendVerification()
    {
        return View();
    }

    // POST: Account/ResendVerification
    [HttpPost]
    public IActionResult ResendVerification(ResendVerificationVM vm)
    {

        if (vm == null)
        {
            TempData["error"] = "Empty Email";
            return View();
        }

        Member member = db.Members.Where(m => m.Email == vm.Email).FirstOrDefault();

        if (member == null)
        {
            TempData["error"] = "User does not exist";
            return View();
        }

        // Remove previous tokens
        var tokenRecord = db.Tokens.Where(t => t.UserId == member.Id).FirstOrDefault();

        if (tokenRecord != null)
        {
            db.Tokens.Remove(tokenRecord);
        }

        // Send Verification Email
        // Generate Token Link
        string token = hp.GenerateToken();

        // Store Token Link In DB
        db.Tokens.Add(new()
        {
            Code = token,
            Expire = DateTime.Now.AddYears(+10),
            UserId = member.Id,
        });
        db.SaveChanges();


        // Generate full URL that points to /Account/Login
        int userId = member.Id;
        var url = Url.Action("VerifyAccount", "Account", new { token, userId }, "https");

        // Email Verification Link To User
        // Construct Email
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(vm.Email, "Fine Dim Sum"));
        mail.Subject = "Account Verification";
        mail.IsBodyHtml = true;

        mail.Body = $@"
                    <h1>Account Verification Link<h1>
                    <p>Dear new user {member.Username},</p>
                    <br>
                    <p>In order to access additional features of the system, 
                    please click the link below to verify your account:</p>
                    <p>{url}</p>
                    <br>
                    <p>The link will expire at 
                    {DateTime.Now.AddYears(+10)}, 
                    so please verify your accounr before the expiry date.</p>
                        ";

        // Send email
        hp.SendEmail(mail);

        TempData["success"] = "Verification Link Sent";

        return RedirectToAction("Login", "Account");
    }


    // GET: Account/Unblock
    public IActionResult Unblock(string token, int userId)
    {
        // If no token || Token not in DB, kick out
        if (token == null || !db.Tokens.Any(t => t.Code == token))
        {
            TempData["error"] = "Unauthorized Access";
            return RedirectToAction("Login");
        }

        // If Time > Expiry, kick out & remove record
        var selectToken = db.Tokens.FirstOrDefault(t => t.Code == token);
        if (selectToken != null && selectToken.Expire < DateTime.Now)
        {
            TempData["error"] = "Token link expired.";

            var removeToken = db.Tokens.First(t => t.Code == token);
            db.Tokens.Remove(removeToken);
            db.SaveChanges();

            return RedirectToAction("Login");
        }

        // Change User Status
        User user = db.Users.First(u => u.Id == userId);
        user.Status = "Active";

        // Change User Attempts
        user.LoginAttempts = 3;

        db.SaveChanges();

        // Pop-Up Message
        TempData["success"] = "Account Unblocked";
        return RedirectToAction("Login");
    }

    // GET: Account/Guest
    public IActionResult Guest()
    {
        // Retrieve the table ID from the session
        var tableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(tableIdString) || !int.TryParse(tableIdString, out int currentTableId))
        {
            TempData["error"] = "No table selected. Please select a valid table.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Fetch the table
        var table = db.RestTables.FirstOrDefault(t => t.Id == currentTableId);
        if (table == null)
        {
            TempData["error"] = "The selected table does not exist. Please choose a valid table.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Check if the table has an active order
        var activeOrder = db.Orders.FirstOrDefault(o => o.RestTableId == currentTableId && o.Status == "Active");
        if (activeOrder != null)
        {
            // Update the UserId for the active order to 8
            activeOrder.UserId = 8;
            HttpContext.Session.SetString("OrderId", activeOrder.Id.ToString());
        }

        // Update the UserId for all cart items associated with the table
        var cartItems = db.CartItems.Where(ci => ci.RestTableId == currentTableId).ToList();
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
            // Log exception details (if a logging framework is available)
            Console.WriteLine($"Error saving changes: {ex.Message}");
            TempData["error"] = "Failed to update user information for the selected table.";
            return RedirectToAction("TableManagement", "AdmTable");
        }

        // Set TableNo and Id to session
        HttpContext.Session.SetString("TableId", table.Id.ToString());
        HttpContext.Session.SetString("TableNo", table.TableNo);

        // Perform sign-out actions
        hp.SignOut();
        HttpContext.Session.Remove("UserId"); // Remove UserId from session
        HttpContext.Session.SetString("Username", "Guest");

        TempData["success"] = "Continue as Guest.";

        // Redirect to the customer product page
        return RedirectToAction("Index", "CustProduct");
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {

        // Retrieve UserId from session
        var userIdString = HttpContext.Session.GetString("UserId");
        if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int userId))
        {
            // Retrieve the user by ID
            var user = db.Users.FirstOrDefault(u => u.Id == userId && u.Status == "Active");

            if (user != null && user.Role == "Member")
            {
                // Retrieve TableId from session
                var tableIdString = HttpContext.Session.GetString("TableId");
                if (!string.IsNullOrEmpty(tableIdString) && int.TryParse(tableIdString, out int tableId))
                {
                    // Remove all cart items for the member and table
                    var cartItems = db.CartItems.Where(ci => ci.UserId == userId && ci.RestTableId == tableId).ToList();
                    if (cartItems.Any())
                    {
                        db.CartItems.RemoveRange(cartItems);
                        db.SaveChanges();
                    }

                    UpdateActiveOrderToGuest(tableId);
                }
            }
        }

        TempData["Info"] = "Logout successfully.";

        // Sign out
        hp.SignOut();
        HttpContext.Session.Remove("UserId");

        return RedirectToAction("Login", "Account");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? ReturnURL)
    {
        return RedirectToAction("Login", new { ReturnURL });
    }


    // GET: Account/Register
    public IActionResult Register(string? InvitationCode)
    {
        RegisterVM vm = new RegisterVM();

        if (InvitationCode != null)
        {
            // Check if Integer
            if (int.TryParse(InvitationCode, out int parsedCode) && parsedCode >= 1 && parsedCode != 8)
            {
                // Check if user exists
                var member = db.Members
                    .Where(m => m.Id == parsedCode)
                    .FirstOrDefault();

                // User exists
                if(member != null)
                {
                    vm.InvitationCode = parsedCode;
                } else
                {
                    // Error Handling
                    TempData["error"] = "Invalid User Code.";
                }

            }
            else
            {
                // Error Handling
                TempData["error"] = "Invalid Invitation Code.";
            }
        }

        return View(vm);
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm, int? InvitationCode)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("PhoneNumber"))
        {
            bool err = hp.VerifyPhoneNumber(vm.PhoneNumber);
            if (err == false) ModelState.AddModelError("PhoneNumber", "Invalid Phone Number Format.");
        }


        if (ModelState.IsValid)
        {
            // Insert member
            db.Members.Add(new()
            {
                Username = vm.Username,
                Email = vm.Email,
                Password = hp.HashPassword(vm.Password),
                PhoneNo = vm.PhoneNumber,
                Gender = vm.Gender,
                InvitedBy = vm.InvitationCode ?? null,
                InvitationCount = 0,
                LoginAttempts = 3,
                RegistrationDate = DateTime.Now,
                Points = 0,
                QRCodeImage = null,
                Status = "Inactive"
            });

            // If has Invitation Code, modify the Invitee's invitation count
            if (InvitationCode != null)
            {
                Member member = db.Members.Where(m => m.Id == InvitationCode).First();
                member.InvitationCount += 1;
            }

            db.SaveChanges();


            // Send Verification Email
            // Generate Token Link
            string token = hp.GenerateToken();

            // Get User (For User ID)
            var user = db.Users
                .Where(u => u.Email == vm.Email)
                .FirstOrDefault();

            // Store Token Link In DB
            db.Tokens.Add(new()
            {
                Code = token,
                Expire = DateTime.Now.AddYears(+10),
                UserId = user.Id,
            });
            db.SaveChanges();


            // Generate full URL that points to /Account/Login
            int userId = user.Id;
            var url = Url.Action("VerifyAccount", "Account", new { token, userId }, "https");

            // Email Verification Link To User
            // Construct Email
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(vm.Email, "Fine Dim Sum"));
            mail.Subject = "Account Verification";
            mail.IsBodyHtml = true;

            mail.Body = $@"
                    <h1>Account Verification Link<h1>
                    <p>Dear new user {user.Username},</p>
                    <br>
                    <p>In order to access additional features of the system, 
                    please click the link below to verify your account:</p>
                    <p>{url}</p>
                    <br>
                    <p>The link will expire at 
                    {DateTime.Now.AddYears(+10)}, 
                    so please verify your accounr before the expiry date.</p>
                        ";

            // Send email
            hp.SendEmail(mail);

            TempData["Info"] = "Register successfully. Please check email to verify account.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/VerifyAccount
    public IActionResult VerifyAccount(string token, int userId)
    {

        // If no token || Token not in DB, kick out
        if (token == null || !(db.Tokens.Any(t => t.Code == token)))
        {
            TempData["error"] = "Unauthorized Access";
            return RedirectToAction("Login");
        }

        // If Time > Expiry, kick out & remove record
        var getToken = db.Tokens.FirstOrDefault(t => t.Code == token);
        if (getToken != null && getToken.Expire < DateTime.Now)
        {
            TempData["error"] = "Token link expired.";

            var removeToken = db.Tokens.First(t => t.Code == token);
            db.Tokens.Remove(removeToken);
            db.SaveChanges();

            return RedirectToAction("Login");
        }

        // Change User Status
        User user = db.Users.First(u => u.Id == userId);
        user.Status = "Active";
        db.SaveChanges();

        // Pop-Up Message
        TempData["success"] = "Account Verified";
        return RedirectToAction("Login");
    }


    // GET: Account/Referral
    public IActionResult Referral()
    {
        string? invitationCode = HttpContext.Request.Query["InvitationCode"];

        if (string.IsNullOrEmpty(invitationCode))
        {
            return View();
        }
        ViewBag.InvitationCode = invitationCode;
        return View();
    }

    //POST: Account/Referral
    [HttpPost]
    public IActionResult Referral(string InvitationCode)
    {
        if (!string.IsNullOrEmpty(InvitationCode))
        {
            TempData["succecss"] = "Valid Invitation Code";
            return RedirectToAction("Register", new { InvitationCode });
        }
        else
        {
            TempData["error"] = "Invidation Code does not exist";
            return View();
        }
    }

    // GET: Account/ForgotPassword
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // POST: Account/ForgotPassword
    [HttpPost]
    public IActionResult ForgotPassword(ForgotPasswordVM vm)
    {
        var u = db.Users.FirstOrDefault(u => u.Email == vm.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if(u != null && u.Status != "Active")
        {
            ModelState.AddModelError("Email", "Account is not active.");
        }

        if (ModelState.IsValid)
        {
            // If previous tokens exist, remove them.
            if (db.Tokens.Any(t => t.UserId == u.Id))
            {
                var removeToken = db.Tokens.FirstOrDefault(t => t.UserId == u.Id);
                db.Tokens.Remove(removeToken);
                db.SaveChanges();
            }

            // Generate Token Link
            string token = hp.GenerateToken();

            // Get User
            var user = db.Users
                .Where(u => u.Email == vm.Email)
                .FirstOrDefault();

            // Store Token Link In DB
            db.Tokens.Add(new()
            {
                Code = token,
                Expire = DateTime.Now.AddMinutes(+5),
                UserId = user.Id,
            });
            db.SaveChanges();

            // Generate full URL that points to /Account/Login
            var url = Url.Action("ResetPassword", "Account", new {token}, "https");

            // Email Token to User
            // Construct email
            var mail = new MailMessage();
            // "My Lovely" is an optional display name for recipient
            mail.To.Add(new MailAddress(vm.Email, "Fine Dim Sum"));
            mail.Subject = "Password Reset Link";
            mail.IsBodyHtml = true;

            mail.Body = $@"
                    <h1>Password Reset Link<h1>
                    <p>Dear {user.Username},</p>
                    <br>
                    <p>To reset your password, click the link below:</p>
                    <p>{url}</p>
                    <br>
                    <p>The link will expire at {DateTime.Now.AddMinutes(+5)}, so please change password before expiry.</p>
                        ";

            // Send email
            hp.SendEmail(mail);


            // Redirect Message
            TempData["Info"] = $"Email Sent</b>.";
            return RedirectToAction("Login");
        }

        return View();

    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword(string token)
    {

        // If no token || Token not in DB, kick out
        if (token == null || !db.Tokens.Any(t => t.Code == token))
        {
            TempData["error"] = "Unauthorized Access";
            return RedirectToAction("ForgotPassword");
        }

        // If Time > Expiry, kick out & remove record
        var getToken = db.Tokens.FirstOrDefault(t => t.Code == token);
        if (getToken != null && getToken.Expire < DateTime.Now)
        {
            TempData["error"] = "Token link expired.";

            var removeToken = db.Tokens.First(t => t.Code == token);
            db.Tokens.Remove(removeToken);
            db.SaveChanges();

            return RedirectToAction("ForgotPassword");
        }

        ResetVM vm = new()
        {
            Token = token,
        };

        // Show the page if no error
        return View(vm);
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetVM vm)
    {
        // Validate passwords match
        if (vm.Password != vm.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(vm); // Pass the same view model to show validation error
        }

        // Ensure token exists
        var tokenRecord = db.Tokens.FirstOrDefault(t => t.Code == vm.Token);
        if (tokenRecord == null)
        {
            ModelState.AddModelError("", "Invalid or expired token.");
            return View(vm);
        }

        // Find user
        var user = db.Users.FirstOrDefault(u => u.Id == tokenRecord.UserId);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View(vm);
        }

        // Hash and update password
        user.Password = hp.HashPassword(vm.Password);

        // Remove token
        db.Tokens.Remove(tokenRecord);

        // Save changes
        db.SaveChanges();

        // Redirect to login
        return RedirectToAction("Login", "Account");
    }


    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }


    // ------------------------------------------------------------------------
    // Link CartItem and Order
    // ------------------------------------------------------------------------

    [HttpPost]

    public void LinkCartItemsToLoggedInUser()
    {
        // Retrieve UserId from session
        var userIdString = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            // No logged-in user; no action needed
            return;
        }

        // Retrieve the logged-in user and ensure they are an active member
        var user = db.Users.FirstOrDefault(u => u.Id == userId && u.Status == "Active");
        if (user == null || user.Role != "Member")
        {
            // User is not valid or not a member; no action needed
            return;
        }

        // Retrieve the RestTableId from the session
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString) || !int.TryParse(restTableIdString, out int restTableId))
        {
            // No table ID available; no action needed
            return;
        }

        // Find all guest cart items (UserId = 8) for the current table
        var guestCartItems = db.CartItems
            .Where(ci => ci.UserId == 8 && ci.RestTableId == restTableId)
            .ToList();

        if (!guestCartItems.Any())
        {
            // No guest cart items to link; no action needed
            return;
        }

        // Update the UserId of the guest cart items to the logged-in user's ID
        foreach (var cartItem in guestCartItems)
        {
            cartItem.UserId = userId;
        }

        // Save changes to the database
        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log the error for debugging purposes
            Console.WriteLine($"Error linking cart items to logged-in user: {ex.Message}");
        }
    }


    [HttpPost]
    public IActionResult UpdateOrderForLoggedInUser()
    {
        // Retrieve the UserId from the session
        var userIdString = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            TempData["error"] = "User session not found or invalid.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Retrieve the user from the database
        var user = db.Users.FirstOrDefault(u => u.Id == userId && u.Status == "Active");

        if (user == null)
        {
            TempData["error"] = "Active user not found.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Check if the user is a member
        if (user.Role != "Member")
        {
            TempData["info"] = "Only members can update their orders.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Retrieve the current RestTableId from the session
        var restTableIdString = HttpContext.Session.GetString("TableId");
        if (string.IsNullOrEmpty(restTableIdString) || !int.TryParse(restTableIdString, out int restTableId))
        {
            TempData["error"] = "Please select a table first.";
            return RedirectToAction("Index", "CustProduct");
        }

        // Find any active orders placed by the guest user for this table
        var guestOrder = db.Orders
            .FirstOrDefault(o => o.RestTableId == restTableId && o.UserId == 8 && o.Status == "Active");

        if (guestOrder != null)
        {
            // Update the order to be associated with the logged-in user
            guestOrder.UserId = userId;
            db.SaveChanges();

            TempData["success"] = "Your previous order has been linked to your account.";
        }

        return RedirectToAction("Index", "CustProduct");
    }

    private void UpdateActiveOrderToGuest(int restTableId)
    {
        // Find the active order for the given table
        var activeOrder = db.Orders
            .FirstOrDefault(o => o.RestTableId == restTableId && o.Status == "Active");

        if (activeOrder != null)
        {
            // Update the order to be associated with the guest user (UserId = 8)
            activeOrder.UserId = 8;
            db.SaveChanges();
        }
    }


}