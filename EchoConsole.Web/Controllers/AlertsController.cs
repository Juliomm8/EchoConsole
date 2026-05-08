using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class AlertsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
