using FineDimSum.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FineDimSum.Models;


// View Models ----------------------------------------------------------------

#nullable disable warnings

public class LoginVM
{
    private readonly IConfiguration configuration;

    public LoginVM()
    {
    }

    public LoginVM(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task OnPost()
    {
        // Verify the token
        string secretKey = configuration["ReCaptchaSettings:SecretKey"];
        bool success = await ReCaptchaService.verifyReCaptchaV2(RecaptchaToken, secretKey);
    }

    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    [MaxLength(100)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }

    [BindProperty]
    [Required]
    public string? RecaptchaToken { get; set; }
}


public class ResetVM
{
    [Required]
    [StringLength(100)]
    public string? Password { get; set; }

    [Required]
    [StringLength(100)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    [Required]
    [StringLength(100)]
    public string Token { get; set; }
}


public class ForgotPasswordVM
{
    [Required]
    [StringLength(100)]
    [EmailAddress]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

}

public class ResendVerificationVM
{
    [Required]
    [StringLength(100)]
    [EmailAddress]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

}

public class TokenVM
{
    [Required]
    [StringLength(100)]
    public string token { get; set; }

    [Required]
    [Column(TypeName = "DATETIME")]
    public DateTime expiry { get; set; }

    [Required]
    public int userId { get; set; }
}

public class RegisterVM
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; }

    [Required]
    [StringLength(100)]
    public string Password { get; set; }

    [Required]
    [StringLength(100)]
    public string ConfirmPassword { get; set; }

    [Required]
    [StringLength(100)]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    [Required]
    [StringLength(11)]
    [RegularExpression(@"^\d{10,11}$")]
    public string PhoneNumber { get; set; }

    [Required]
    [StringLength(1)]
    [RegularExpression(@"[MF]$")]
    public string Gender { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Value must be a non-negative number.")]
    public int? InvitationCode { get; set; }
}

public class ReferralVM
{
    [Required]
    [StringLength(100)]
    [RegularExpression(@"^\d+$", ErrorMessage = "The code must be an integer.")]
    [Range(0, int.MaxValue, ErrorMessage = "Value must be a non-negative number.")]
    public int InvitationCode { get; set; }

}

public class OrderTableVM
{
    // For GET
    public string? OrderID { get; set; }
    public string TableNo { get; set; }
    public string Status { get; set; }
}

public class ProfileVM
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; }

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    public string Image { get; set; }
}

public class DetailsVM
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; }

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    [StringLength(50)]
    public string Role { get; set; }

    public string Status { get; set; }
    public string Image { get; set; }
    public bool IsEditable { get; set; }
}

public class AdminCreate
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [MaxLength(100)]
    public string Password { get; set; }

    [Required]
    [MaxLength(100)]
    public string ConfirmPassword { get; set; }

    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    [Required]
    public string Role { get; set; }
}

public class MemberDetailsVM
{

    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    public string Gender { get; set; }

    [RegularExpression(@"^\d{10,11}$")]
    public string PhoneNumber { get; set; }

    [Range(0, int.MaxValue)]
    public int Points { get; set; }

    public int InvitedBy { get; set; }  // Read-only

    public string Status { get; set; }

    [Display(Name = "Register Date")]
    [DataType(DataType.Date)]
    public DateTime RegisterDate { get; set; }  // Read-only

    public bool IsEditable { get; set; }
}

// For Manage Category Page
public class CategoryVM
{
    public int? Id { get; set; }

    [StringLength(100, ErrorMessage = "Category Name should be between 1 - 100 characters.")]
    [Display(Name = "Category Name")]
    public string? Name { get; set; }

    [Required]
    [StringLength(200, ErrorMessage = "Category Description should be between 1 - 200 characters.")]
    [Display(Name = "Category Description")]
    public string Description { get; set; }

    [Required]
    [StringLength(15)]
    [RegularExpression("^(?i)(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; }
}

// For Manage Product Page
public class ProductVariationOptionCreateVM
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} must be between 1 - 100 characters.")]
    [Display(Name = "Option")]
    public string Name { get; set; }

    [Range(0.01, 99999999.99, ErrorMessage = "{0} must be between 0.01 - 99999999.99.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int PreStockQuantity { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int StockQuantity { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int MinStockLevel { get; set; }

    public IFormFile Photo { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(15)]
    [RegularExpression("^(?i)(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; }
}

public class ProductVariationOptionUpdateVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} must be between 1 - 100 characters.")]
    [Display(Name = "Option")]
    public string Name { get; set; }

    [Range(0.01, 99999999.99, ErrorMessage = "{0} must be between 0.01 - 99999999.99.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int PreStockQuantity { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int StockQuantity { get; set; }

    [Required]
    [Range(0, 100000, ErrorMessage = "{0} must be between 0 - 100000.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Invalid {0} format.")]
    public int MinStockLevel { get; set; }

    public string? Image { get; set; }

    [Display(Name = "Image")]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(15)]
    [RegularExpression("^(?i)(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; }
}

public class ProductCreateVM
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} must be between 1 - 100 characters.")]
    [Remote("CheckName", "AdmProduct", ErrorMessage = "Duplicated {0}.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(200, ErrorMessage = "{0} must be between 1 - 200 characters.")]
    [Display(Name = "Product Description")]
    public string Description { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Display(Name = "Product Image")]
    public IFormFile Photo { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(15)]
    [RegularExpression("^(?i)(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Remote("CheckCategoryId", "AdmProduct", ErrorMessage = "Invalid {0}.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public List<ProductVariationOptionCreateVM> ProductVariationOptions { get; set; } = new List<ProductVariationOptionCreateVM>();
}

public class ProductUpdateVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(100, ErrorMessage = "{0} must be between 1 - 100 characters.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(200, ErrorMessage = "{0} must be between 1 - 200 characters.")]
    [Display(Name = "Product Description")]
    public string Description { get; set; }

    public string? Image { get; set; }

    [Display(Name = "Product Image")]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(15)]
    [RegularExpression("^(?i)(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Remote("CheckCategoryId", "AdmProduct", ErrorMessage = "Invalid {0}.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public List<ProductVariationOptionUpdateVM> ProductVariationOptions { get; set; } = new List<ProductVariationOptionUpdateVM>();
}

// For Order Item List Page 
public class OrderItemListVM
{
    [Required]
    public int OrderItemId { get; set; }

    [Required, MaxLength(100)]
    public string ProductName { get; set; }

    [Required, MaxLength(100)]
    public string VariationOptionName { get; set; }

    [Required, Range(1, 100)]
    public int Quantity { get; set; }

    [MaxLength(100)]
    public string Remark { get; set; }

    [Required]
    [MaxLength(100)]
    public string Image { get; set; }

    [Required]
    [RegularExpression("^(Preparing|Ready|Delivered)$", ErrorMessage = "Invalid Status")]
    public string Status { get; set; }

    [Required]
    public string TableNo { get; set; }
}

// For Order Management Page
public class OrderItemVM
{
    // For GET

    [Required]
    public int OrderItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Image { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; }

    [Required]
    public string VariationOptionName { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public string Status { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Remark { get; set; }

}

public class CreateMemberVM
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }

    [Required]
    [RegularExpression(@"^\d{10,11}$")]
    public string PhoneNumber { get; set; }

    [Required]
    public string Gender { get; set; }

    [Required]
    [MaxLength(100)]
    public string Password { get; set; }

    [Required]
    [MaxLength(100)]
    public string ConfirmPassword { get; set; }

}

public class TableVM
{

    public int TableId { get; set; }

    [Required]
    [MaxLength(5, ErrorMessage = "Table Number cannot exceed 5 characters.")]
    public string TableNo { get; set; }

    [Required]
    [RegularExpression("^(Available|Unavailable)$", ErrorMessage = "Invalid status selected")]
    public string Status { get; set; }
}

public class AdminProfileVM
{
    public int Id { get; set; } // User ID (Hidden in the form)

    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }

    public string Role { get; set; } // Role Name (e.g., Root, Manager)

    public string Image { get; set; } // Profile picture path (based on role)

    public string Status { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } // Required for password updates

    [Required]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } // New password (optional)

    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } // Confirm new password
}

public class CusProfileVM
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    [Required]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }

    [Required]
    [RegularExpression(@"^\d{10,11}$")]
    public string PhoneNumber { get; set; }

    [Required]
    public string Gender { get; set; }

    public string Status { get; set; }

    [Display(Name = "Register Date")]
    [DataType(DataType.Date)]
    public DateTime? RegisterDate { get; set; }

    public string QRCodeImage { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } // Required for password updates

    [Required]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } // New password (optional)

    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } // Confirm new password

    public int Points { get; set; }

    public bool IsEditable { get; set; }


    public int? InvitedBy { get; set; }
    public string InvitedByName { get; set; }


}

public class OrderHistoryVM
{
    public int OrderId { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime? DateTime { get; set; }

    public string TableNo { get; set; }

    public int CustomerId { get; set; }
}

public class OrderHistoryDetailsVM1
{
    [MaxLength(100)]
    public string ImageURL { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(100)]
    public string Options { get; set; }

    [Range(0, 100)]
    public int Quantity { get; set; }

    // The product of UnitPrice * Quantity
    [Precision(10, 2)]
    public decimal Price { get; set; }
}


public class OrderHistoryDetailsVM2
{
    [MaxLength(50)]
    public string PaymentMethod { get; set; }

    public DateTime DateTime { get; set; }

    [Precision(10, 2)]
    public decimal Subtotal { get; set; }

    [Precision(10, 2)]
    public decimal Tax { get; set; }

    [Precision(10, 2)]
    public decimal ServiceCharge { get; set; }

    [Precision(10, 2)]
    public decimal Promotion { get; set; }

    [Precision(10, 2)]
    public decimal Total { get; set; }

}

public class OrderHistoryDetailsVM
{
    public List<OrderHistoryDetailsVM1> vm1 { get; set; }
    public OrderHistoryDetailsVM2 vm2 { get; set; }
}

public class CartItemVM
{
    public int ProductVariationOptionId { get; set; }
    public int Quantity { get; set; }
    public string Remark { get; set; }
    public int RestTableId { get; set; }
}

public class OrderDetailsViewModel
{
    public Order Order { get; set; }
    public List<OrderItem> OrderItems { get; set; }
    public bool IsMember { get; set; }
    public int MemberPoints { get; set; }
}



public class PastOrderViewModel
{
    public int OrderId { get; set; }
    public string OrderDate { get; set; } // Formatted date
    public string? FirstItemImage { get; set; }
    public string FirstItemName { get; set; }
    public int FirstItemQuantity { get; set; }
    public decimal FirstItemPrice { get; set; }
    public decimal OrderTotal { get; set; }
}

public class PastOrderDetailsViewModel
{
    public int OrderId { get; set; }
    public string OrderDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public List<PastOrderItemViewModel> Items { get; set; }
}

public class PastOrderItemViewModel
{
    public string ProductImage { get; set; }
    public string ProductName { get; set; }
    public string VariationName { get; set; }
    public string Remark { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal FinalPrice { get; set; }
}
