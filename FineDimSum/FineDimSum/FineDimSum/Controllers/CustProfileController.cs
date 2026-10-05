using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FineDimSum.Controllers;

public class CustProfileController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public CustProfileController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: CustProfile/Index
    [Authorize(Roles = "Member")]
    public IActionResult CusProfile(int id)
    {
        // Fetch the specific Member by ID
        var member = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);

        if (member == null)
        {
            return NotFound("Member not found.");
        }

        // Fetch inviter details if available
        string inviterName = null;
        if (member.InvitedBy.HasValue)
        {
            var inviter = db.Users.FirstOrDefault(u => u.Id == member.InvitedBy.Value);
            inviterName = inviter?.Username;
        }

        var vm = new CusProfileVM
        {
            Id = member.Id,
            RegisterDate = member.RegistrationDate,
            OldPassword = member.Password,
            Username = member.Username,
            Email = member.Email,
            Gender = member.Gender,
            PhoneNumber = member.PhoneNo,
            Points = member.Points,
            Status = member.Status,
            IsEditable = User.IsInRole("Manager") || User.IsInRole("Root"),
            QRCodeImage = member.QRCodeImage,
            //QRCodeImage = string.IsNullOrEmpty(member.QRCodeImage)
            //? null
            //: $"/images/QrCode/{member.QRCodeImage}",
            InvitedBy = member.InvitedBy,
            InvitedByName = inviterName
        };

        vm.IsEditable = true;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int id, string fieldName, string fieldValue)
    {

        bool isEditable = true;

        if (!isEditable)
        {
            return Forbid();
        }

        // Find the member by ID
        var member = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);

        if (member == null)
        {
            return NotFound("Member not found.");
        }

        // Update member data
        switch (fieldName)
        {
            case "Username":
                member.Username = fieldValue;
                break;
            case "Email":
                if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(fieldValue))
                {
                    return BadRequest("Invalid email format.");
                }
                // Check for duplicate email
                if (db.Users.Any(a => a.Email == fieldValue && a.Id != id))
                {
                    ModelState.AddModelError("", "Email is already in use by another user.");
                    return BadRequest("Email is already in use by another user.");
                }
                member.Email = fieldValue;
                break;
            case "Gender":
                if (new[] { "M", "F" }.Contains(fieldValue))
                {
                    member.Gender = fieldValue;
                }
                else
                {
                    return BadRequest("Invalid Gender");
                }
                break;
            case "PhoneNumber":
                member.PhoneNo = fieldValue;
                break;
            default:
                return BadRequest("Invalid field name.");
        }
        db.SaveChanges();

        var updatedMember = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);
        if (updatedMember == null)
        {
            return NotFound("Member not found.");
        }

        var vm = new MemberDetailsVM
        {
            Id = updatedMember.Id,
            Username = updatedMember.Username,
            Email = updatedMember.Email,
            Gender = updatedMember.Gender,
            PhoneNumber = updatedMember.PhoneNo,
            Points = updatedMember.Points,  // Ensure Points is included here
            Status = updatedMember.Status,
        };

        TempData["success"] = "Member details updated successfully!";
        return RedirectToAction(nameof(CusProfile), new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(CusProfileVM model)
    {
        Console.WriteLine($"Id: {model.Id}, OldPassword: {model.OldPassword}, NewPassword: {model.NewPassword}");

        var member = db.Users.FirstOrDefault(u => u.Id == model.Id);
        if (member == null)
        {
            return NotFound();
        }



        if (!hp.VerifyPassword(member.Password, model.OldPassword))
        {
            Console.WriteLine($"Stored: {member.Password}, Entered: {model.OldPassword}");
            TempData["Error"] = "Old password is incorrect.";

            return RedirectToAction("CusProfile", new { id = model.Id });
        }

        try
        {

            // Update Password
            member.Password = hp.HashPassword(model.NewPassword);
            db.SaveChanges();
            TempData["Success"] = "Password changed successfully!";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving to database: {ex.Message}");
            TempData["Error"] = "An error occurred while updating the password.";
            return RedirectToAction("CusProfile", new { id = model.Id });
        }

        return RedirectToAction("CusProfile", new { id = model.Id });
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> SaveQRCode(int id, string qrUrl)
    {
        var member = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);
        if (member == null)
        {
            return Json(new { success = false, message = "Member not found." });
        }


        try
        {
            // Step 1: Download the QR code image from the API
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(qrUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Failed to download QR Code image." });
                }

                var qrImageBytes = await response.Content.ReadAsByteArrayAsync();

                // Step 2: Define the path to save the QR code image
                var fileName = $"QR_{id}_{DateTime.Now:yyyyMMddHHmmss}.png";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "QrCode", fileName);

                // Step 3: Save the image to the /images/QrCode folder
                await System.IO.File.WriteAllBytesAsync(savePath, qrImageBytes);

                // Step 4: Save the path in the database
                member.QRCodeImage = $"/images/QrCode/{fileName}";
                db.SaveChanges();
            }

            return Json(new { success = true, message = "QR Code generated and saved successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    public IActionResult SendQRCodeEmail(int id)
    {
        // Fetch the member by ID
        var member = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);
        if (member == null)
        {
            return NotFound("Member not found.");
        }

        if (string.IsNullOrEmpty(member.Email))
        {
            return BadRequest("Member email is not available.");
        }

        // Check if the QR Code image exists
        if (string.IsNullOrEmpty(member.QRCodeImage))
        {
            return BadRequest("QR Code has not been generated. Please generate it first.");
        }

        try
        {
            // Prepare email details
            string qrImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", member.QRCodeImage.TrimStart('/'));
            string qrImageUrl = $"{Request.Scheme}://{Request.Host}/images/QrCode/{member.QRCodeImage}"; // Assuming images are stored in /wwwroot/qrcodes

            if (!System.IO.File.Exists(qrImagePath))
            {
                return BadRequest("QR Code file not found on the server.");
            }

            string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; color: #333;'>
                <h2>Hello, {member.Username}!</h2>
                <p>Your Qr Code is attached below</p>


                <p>If you have any questions, feel free to contact us.</p>
                <p>Best regards,<br/>Fine Dim Sum</p>
            </div>";

            var mail = new MailMessage
            {
                Subject = "Your QR Code",
                Body = htmlBody,
                IsBodyHtml = true // Enable HTML body
            };

            // Add recipient
            mail.To.Add(new MailAddress(member.Email));

            mail.Attachments.Add(new Attachment(qrImagePath));

            // Use the provided SendEmail helper
            hp.SendEmail(mail);

            TempData["success"] = "QR Code email sent successfully.";
            return RedirectToAction("CusProfile", new { id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Failed to send email: " + ex.Message);
        }
    }
    }