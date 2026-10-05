using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FineDimSum.Controllers;

[Authorize(Roles = "Root, Manager")]
public class CommonElementController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
