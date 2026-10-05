using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace FineDimSum.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options) { }

    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Root> Roots { get; set; }
    public DbSet<Manager> Managers { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Chef> Chefs { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariationOption> ProductVariationOptions { get; set; }
    public DbSet<RestTable> RestTables { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
}

// Entity Classes

#nullable disable warnings

public class User
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Username { get; set; }

    [MaxLength(100)]
    public string Password { get; set; }

    [MaxLength(100)]
    public string Email { get; set; }

    public string Role => GetType().Name;

    [MaxLength(15)]
    public string Status { get; set; }

    public int LoginAttempts { get; set; }

    // List
    public List<Token> Tokens { get; set; } = [];
}

public class Member : User
{
    // Column
    [MaxLength(1)]
    public string Gender { get; set; }

    [MaxLength(11)]
    public string PhoneNo { get; set; }

    public int Points { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime? RegistrationDate { get; set; }

    [MaxLength(100)]
    public string? QRCodeImage { get; set; }

    public int? InvitedBy { get; set; }

    public int? InvitationCount { get; set; }

    // List
    public List<CartItem> CartItems { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}

public class Root : User
{
    // Column
    [MaxLength(100)]
    public string? Image { get; set; }
}

public class Manager : User
{
    // Column
    [MaxLength(100)]
    public string? Image { get; set; }
}

public class Staff : User
{
    // Column
    [MaxLength(100)]
    public string? Image { get; set; }
}

public class Chef : User
{
    // Column
    [MaxLength(100)]
    public string? Image { get; set; }
}

public class Token
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    public string Code { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime Expire { get; set; }

    // FK
    public int UserId { get; set; }

    // Navigation
    public User User { get; set; }
}

public class Category
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(200)]
    public string Description { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // List
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(200)]
    public string Description { get; set; }

    [MaxLength(100)]
    public string Image { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // FK
    public int CategoryId { get; set; }

    // Navigation
    public Category Category { get; set; }

    // List
    public List<ProductVariationOption> ProductVariationOptions { get; set; } = [];
}

public class ProductVariationOption
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    public int PreStockQuantity { get; set; }

    public int StockQuantity { get; set; }

    public int MinStockLevel { get; set; }

    [MaxLength(100)]
    public string Image { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // FK
    public int ProductId { get; set; }

    // Navigation 
    public Product Product { get; set; }

    // List
    public List<OrderItem> OrderItems { get; set; } = [];
    public List<CartItem> CartItems { get; set; } = [];
}

public class RestTable
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(5)]
    public string TableNo { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // List
    public List<Order> Orders { get; set; } = [];
    public List<CartItem> CartItems { get; set; } = [];
}

public class CartItem
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int Quantity { get; set; }

    [MaxLength(50)]
    public string Remark { get; set; }

    // FK: UserId
    public int? UserId { get; set; }

    // FK: ProductVariationOptionId
    public int ProductVariationOptionId { get; set; }

    // FK: RestTableId
    public int RestTableId { get; set; }

    // Navigation Properties
    public User User { get; set; }
    public ProductVariationOption ProductVariationOption { get; set; }
    public RestTable RestTable { get; set; }
}


public class Order
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedDateTime { get; set; }

    [Precision(10, 2)]
    public decimal Subtotal { get; set; }

    [Precision(10, 2)]
    public decimal SST { get; set; }

    [Precision(10, 2)]
    public decimal DiscountAmount { get; set; }

    [Precision(10, 2)]
    public decimal ServiceCharge { get; set; }

    [Precision(10, 2)]
    public decimal Total { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // FK
    public int UserId { get; set; }
    public int RestTableId { get; set; }

    // Navigation
    public User User { get; set; }
    public RestTable RestTable { get; set; }

    // List
    public List<OrderItem> OrderItems { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

public class OrderItem
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(100)]
    public string ProductName { get; set; }

    [MaxLength(100)]
    public string VariationOptionName { get; set; }

    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    [MaxLength(50)]
    public string Remark { get; set; }

    [MaxLength(100)]
    public string Image { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    // FK
    public int ProductVariationOptionId { get; set; }
    public int OrderId { get; set; }

    // Navigation
    public ProductVariationOption ProductVariationOption { get; set; }
    public Order Order { get; set; }
}

public class Payment
{
    // Column
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Precision(10, 2)]
    public decimal Total { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime PaymentDateTime { get; set; }

    [MaxLength(30)]
    public string PaymentMethod { get; set; }

    // FK
    public int OrderId { get; set; }

    // Navigation 
    public Order Order { get; set; }
}