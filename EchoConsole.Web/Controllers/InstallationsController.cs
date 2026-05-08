using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class InstallationsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
