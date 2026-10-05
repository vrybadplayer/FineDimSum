using Microsoft.AspNetCore.Mvc;

namespace FineDimSum.Controllers;

public class ErrorController : Controller
{
    [Route("Error/{statusCode?}")]
    public IActionResult HandleError(int? statusCode = null)
    {
        if (statusCode != null)
        {
            TempData["error"] = statusCode switch
            {
                404 => "The page is not found.",
                500 => "An internal server error occurred. Please try again later.",
                _ => "An unexpected error occurred. Please try again."
            };
        }
        else
        {
            TempData["error"] = "An unexpected error occurred. Please try again.";
        }

        return View("Error"); // Return the Error view
    }

    [Route("Error")]
    public IActionResult Error()
    {
        ViewBag.ErrorMessage = "An unexpected error occurred.";
        return View("Error");
    }
}
