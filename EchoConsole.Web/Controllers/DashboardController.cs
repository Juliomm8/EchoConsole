using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
