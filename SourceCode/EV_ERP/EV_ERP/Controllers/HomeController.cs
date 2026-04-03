using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var user = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!;
            ViewData["Title"] = "Dashboard";
            return View(user);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
