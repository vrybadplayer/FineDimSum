global using FineDimSum.Models;
using Stripe;
using FineDimSum;
using FineDimSum.Controllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

builder.Services.AddAuthentication().AddCookie();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Helper>();
builder.Services.AddSession();




var app = builder.Build();

// Add exception handling and status code middleware here
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseExceptionHandler("/Error");


app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllerRoute
(
    name: "default",
    pattern: "{controller=Account}/{action=Login}"
);
app.Run();