using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class UsersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
