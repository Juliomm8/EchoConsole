using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
