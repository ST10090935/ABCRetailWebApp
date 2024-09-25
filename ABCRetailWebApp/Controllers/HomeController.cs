using Microsoft.AspNetCore.Mvc;

namespace ABCRetailWebApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
