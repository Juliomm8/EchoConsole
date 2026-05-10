using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class ReportsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
