using Microsoft.AspNetCore.Mvc;
using FineDimSum.Models;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace FineDimSum.Controllers;

public class AdmAdminController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AdmAdminController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmAdmin/Index
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Index(string searchQuery, string filterRole, string filterStatus)
    {
        var admins = db.Users
            .AsEnumerable()
            .Where(a => a.Role != "Root" && a.Role != "Member");


        // Apply search
        if (!string.IsNullOrEmpty(searchQuery))
        {
            admins = admins.Where(a =>
                a.Username.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                a.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        // Apply role filter
        if (!string.IsNullOrEmpty(filterRole) && filterRole != "All")
        {
            admins = admins.Where(a => a.Role == filterRole);
        }

        // Apply status filter
        if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "All")
        {
            admins = admins.Where(a => a.Status == filterStatus);
        }

        var adminList = admins
        .Select(a => new
            {
                a.Id,
                a.Username,
                a.Email,
                a.Role,
                a.Status,
                IsEditable = User.IsInRole("Root")
            }).ToList();
        return View(adminList);
    }

    // GET: AdmAdmin/Details
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Details(int id)
    {
        var a = db.Users
            .FirstOrDefault(a => a.Id == id);
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

        // Fetch the admin details based on the given id
        var vm = new DetailsVM
        {
            Id = a.Id,
            Username = a.Username,
            Email = a.Email,
            Role = a.Role,
            Status = a.Status,
            Image = image,
            IsEditable = User.IsInRole("Root") 
        };
            //.FirstOrDefault();
        // Mock as Root role for debugging
        // If no admin is found, return 404


        // Check if the current user has permission to edit the admin's details
        

        // Pass additional info to the view

        vm.IsEditable = true; // Allow editing if Role is Root

        //admin.IsEditable = isEditable; Change this back after debugging

        return View(vm);
    
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult SaveFieldChanges(int id, string fieldName, string fieldValue)
    {
        // Fetch the admin by ID

        // Temporarily bypass authorization for debugging
        // Remove this for production
        bool isEditable = true; // Replace with `User.IsInRole("Root")`

        if (!isEditable)
        {
            return Forbid();
        }

        //here

        var admin = db.Users.FirstOrDefault(a => a.Id == id);
        if (admin == null)
        {
            return NotFound(); // Return 404 if the admin is not found
        }

        // Check if the user has permissions (mocked for now as Role = Root)
        //if (User.IsInRole("Root")) Add this back after debug
        //{
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
                case "Status":
                    if (new[] { "Active", "Inactive", "Terminated" }.Contains(fieldValue))
                    {
                        admin.Status = fieldValue;
                    }
                    else
                    {
                        return BadRequest("Invalid status value.");
                    }
                    break;
                default:
                    return BadRequest("Invalid field name.");
            }

            // Save changes to the database
            db.SaveChanges();

        TempData["success"] = "Profile updated.";

        return RedirectToAction(nameof(Details), new { id });
    }


    //} Add this back after debug

    [HttpGet]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create()
    {

        var isRoot = User.IsInRole("Root");
        if (!isRoot)
        {
            // For debugging, temporarily mock the role check
            isRoot = true;
        }
        //temporary debugging
        //if (!User.IsInRole("Root"))
        //{
          //  return Forbid();
        //}

        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Create(AdminCreate model, IFormFile? imageFile)
    {

        var isRoot = User.IsInRole("Root");
        if (!isRoot)
        {
            // For debugging, temporarily mock the role check
            isRoot = true;
        }
        //temporary debugging
        //if (!User.IsInRole("Root"))
        //{
        //  return Forbid();
        //}

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        string imagePath = null;
        if (imageFile != null)
        {
            string validationError = hp.ValidatePhoto(imageFile);
            if (!string.IsNullOrEmpty(validationError))
            {
                ModelState.AddModelError("Image", validationError);
                return View(model);
            }

            // Save the image
            imagePath = hp.SaveImage(imageFile, Path.Combine("images", "Profile"));
        }


        // Create a new admin user based on the selected role
        User newAdmin;
        switch (model.Role)
        {
            case "Staff":
                newAdmin = new Staff { Image = imagePath };
                break;
            case "Manager":
                newAdmin = new Manager { Image = imagePath };
                break;
            case "Chef":
                newAdmin = new Chef { Image = imagePath };
                break;
            case "Root":
                newAdmin = new Root { Image = imagePath };
                break;
            default:
                ModelState.AddModelError("Role", "Invalid role selected.");
                return View(model);
        }

        // Set common properties
        newAdmin.Username = model.Username;
        newAdmin.Password = hp.HashPassword(model.Password);
        newAdmin.Email = model.Email;
        newAdmin.Status = "Active";
        newAdmin.LoginAttempts = 3;

        // Add to the database
        db.Users.Add(newAdmin);
        db.SaveChanges();

        TempData["Success"] = "Admin created successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
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
            return RedirectToAction(nameof(Details), new { id });
        }


        var validationError = hp.ValidatePhoto(ProfilePicture);

        if (!string.IsNullOrEmpty(validationError))
        {
            TempData["Error"] = validationError;
            return RedirectToAction(nameof(Details), new { id });
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
        return RedirectToAction(nameof(Details), new { id });
    }

}