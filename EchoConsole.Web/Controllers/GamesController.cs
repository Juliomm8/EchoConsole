using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers
{
    public class GamesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
