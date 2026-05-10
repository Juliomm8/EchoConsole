using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class LiveSessionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
