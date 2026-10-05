using Microsoft.AspNetCore.Mvc;
using FineDimSum.Models;
using Microsoft.AspNetCore.Authorization;

namespace FineDimSum.Controllers;

public class AdmProfileController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AdmProfileController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmProfile/Profile
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult Profile(int id)
    {

        var a = db.Users.FirstOrDefault(a => a.Id == id);
        if (a == null)
        {
            RedirectToAction();
        }

        var image = a switch
        {
            Root r => r.Image,
            Manager m => m.Image,
            Staff s => s.Image,
            Chef c => c.Image,
            _ => "",
        };
        // Populate the model with existing user details
        var vm = new AdminProfileVM
        {
            Id = a.Id,
            OldPassword = a.Password,
            Username = a.Username,
            Email = a.Email,
            Role = a.Role,
            Image = image,
            Status = a.Status,
        };

        return View(vm);
    }

    // POST: AdmProfile/Profile/Update
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult AdminEdit(int id, string fieldName, string fieldValue)
    {
        var admin = db.Users.FirstOrDefault(a => a.Id == id);
        if (admin == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            RedirectToAction();
        }

        // Update the appropriate field
        switch (fieldName)
        {
            case "Username":
                admin.Username = fieldValue;
                break;
            case "Email":
                // Check for duplicate email
                if (db.Users.Any(a => a.Email == fieldValue && a.Id != id))
                {
                    ModelState.AddModelError("", "Email is already in use by another admin.");
                    return BadRequest("Email is already in use by another admin.");
                }
                admin.Email = fieldValue;
                break;

            default:
                return BadRequest("Invalid field name.");
        }
        db.SaveChanges();

        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction("Profile", new { id });

    }

    // POST: AdmProfile/Profile/ChangePassword
    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult ChangePassword(AdminProfileVM model)
    {
        var user = db.Users.FirstOrDefault(u => u.Id == model.Id);
        if (user == null)
        {
            return NotFound();
        }


        if (!hp.VerifyPassword(user.Password, model.OldPassword))
        {
            TempData["Error"] = "Old password is incorrect.";

            return RedirectToAction("Profile", new { id = model.Id });
        }

        // Update Password
        user.Password = hp.HashPassword(model.NewPassword);

        db.SaveChanges();

        TempData["Success"] = "Password changed successfully!";
        return RedirectToAction("Profile", new { id = model.Id });
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager, Staff, Chef")]
    public IActionResult ChangeProfilePicture(int id, IFormFile ProfilePicture)
    {
        var user = db.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        if (ProfilePicture == null || ProfilePicture.Length == 0)
        {
            TempData["Error"] = "No file selected. Please choose a valid image.";
            return RedirectToAction(nameof(Profile), new { id });
        }


        var validationError = hp.ValidatePhoto(ProfilePicture);

        if (!string.IsNullOrEmpty(validationError))
        {
            TempData["Error"] = validationError;
            return RedirectToAction(nameof(Profile), new { id });
        }

        // Determine the role and handle image storage
        string currentImage = user switch
        {
            Root r => r.Image,
            Manager m => m.Image,
            Staff s => s.Image,
            Chef c => c.Image,
            _ => null
        };


        // Delete the existing profile picture if any
        if (!string.IsNullOrEmpty(currentImage))
        {
            hp.DeletePhoto(currentImage, "images/Profile");
        }

        // Save the new profile picture
        var newImageName = hp.SaveImage(ProfilePicture, "images/Profile");


        switch (user)
        {
            case Root r:
                r.Image = newImageName;
                break;
            case Manager m:
                m.Image = newImageName;
                break;
            case Staff s:
                s.Image = newImageName;
                break;
            case Chef c:
                c.Image = newImageName;
                break;
        }

        // Save the changes to the database
        db.SaveChanges();

        TempData["Success"] = "Profile picture updated successfully.";
        return RedirectToAction(nameof(Profile), new { id });
    }



}
