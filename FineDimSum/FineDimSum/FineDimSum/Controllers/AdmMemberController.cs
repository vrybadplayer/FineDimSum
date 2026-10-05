using FineDimSum.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FineDimSum.Controllers;

public class AdmMemberController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AdmMemberController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: AdmMember/Index
    [Authorize(Roles = "Root, Manager")]
    public IActionResult Index(string searchQuery, string filterGender, string filterStatus)
    {
        // Base query
        var members = db.Users.OfType<Member>().Where(m => m.Id != 8).AsQueryable();

        // Search by username or email
        if (!string.IsNullOrEmpty(searchQuery))
        {
            members = members.Where(m => m.Username.Contains(searchQuery) || m.Email.Contains(searchQuery));
        }

        // Filter by Gender
        if (!string.IsNullOrEmpty(filterGender) && filterGender != "All")
        {
            members = members.Where(m => m.Gender == filterGender);
        }

        // Filter by Status
        if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "All")
        {
            members = members.Where(m => m.Status == filterStatus);
        }

        var result = members.Select(m=> new
           {
               m.Id,
               m.Username,
               m.Email,
               m.Gender,
               m.PhoneNo,
               m.InvitedBy,
               m.Status,
               IsEditable = User.IsInRole("Manager") || User.IsInRole("Root")
           }).ToList();

        ViewData["SearchQuery"] = searchQuery;
        ViewData["FilterGender"] = filterGender;
        ViewData["FilterStatus"] = filterStatus;

        return View(result);
        
    }

    // GET: AdmMember/Details/{id}
    [Authorize(Roles = "Root, Manager")]
    public IActionResult MemberDetails(int id)
    {
        // Fetch the specific Member by ID
        var member = db.Users.OfType<Member>().FirstOrDefault(m => m.Id == id);

        if (member == null)
        {
            return NotFound("Member not found.");
        }


        var vm = new MemberDetailsVM
        {
            Id = member.Id,
            Username = member.Username,
            Email = member.Email,
            Gender = member.Gender,
            PhoneNumber = member.PhoneNo,
            Points = member.Points,
            Status = member.Status,
            IsEditable = User.IsInRole("Manager") || User.IsInRole("Root")
        };

        vm.IsEditable = true;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Root, Manager")]
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
                
                
            case "Points":
                if (!int.TryParse(fieldValue, out int points) || points < 0)
                {
                    return BadRequest("Points must be a valid non-negative integer.");
                }
                member.Points = points;
                break;
            case "Status":
                if (new[] { "Active", "Inactive", "Terminated" }.Contains(fieldValue))
                {
                    member.Status = fieldValue;
                }
                else
                {
                    return BadRequest("Invalid status value.");
                }
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
            IsEditable = User.IsInRole("Manager") || User.IsInRole("Root")
        };

        TempData["success"] = "Member details updated successfully!";
        return RedirectToAction("MemberDetails", vm);
    }


    //Need to change auth after this
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


    [HttpGet]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult CreateMember()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Root, Manager")]
    public IActionResult CreateMember(CreateMemberVM model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        // Initialize a new Member
        var newMember = new Member
        {
            Username = model.Username,
            Email = model.Email,
            PhoneNo = model.PhoneNumber,
            Gender = model.Gender,
            Password = hp.HashPassword(model.Password), // Hash the password
            Status = "Active",
            LoginAttempts = 3,
            Points = 0,
            InvitedBy = null,
            QRCodeImage = null,
            InvitationCount = 0,
            RegistrationDate = DateTime.Now
        };

        // Add the new member to the database
        db.Users.Add(newMember);
        db.SaveChanges();

        TempData["Success"] = "Member created successfully!";
        return RedirectToAction("Index", "AdmMember");
    }
}
    