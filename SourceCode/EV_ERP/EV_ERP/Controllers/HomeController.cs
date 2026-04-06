using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.ViewModels.Dashboard;
using EV_ERP.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _uow;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork uow)
        {
            _logger = logger;
            _uow = uow;
        }

        public async Task<IActionResult> Index()
        {
            var user = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!;
            ViewData["Title"] = "Dashboard";

            var vm = new DashboardViewModel { CurrentUser = user };

            // ── Summary counts ───────────────────────
            vm.TotalCustomers = await _uow.Repository<Customer>().Query()
                .CountAsync(c => c.IsActive);

            vm.TotalUsers = await _uow.Repository<User>().Query()
                .CountAsync(u => u.IsActive && !u.IsLocked);

            // ── Customers by group ───────────────────
            var customersByGroup = await _uow.Repository<Customer>().Query()
                .Where(c => c.IsActive)
                .Include(c => c.CustomerGroup)
                .GroupBy(c => c.CustomerGroup == null ? "Chưa phân nhóm" : c.CustomerGroup.GroupName)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            vm.CustomerGroupLabels = customersByGroup.Select(g => g.Label).ToList();
            vm.CustomerGroupCounts = customersByGroup.Select(g => g.Count).ToList();

            // ── New customers per month (last 6) ─────
            var sixMonthsAgo = DateTime.Now.AddMonths(-5);
            var startMonth = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

            var customersByMonth = await _uow.Repository<Customer>().Query()
                .Where(c => c.CreatedAt >= startMonth)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            for (int i = 0; i < 6; i++)
            {
                var m = startMonth.AddMonths(i);
                vm.MonthLabels.Add(m.ToString("MM/yyyy"));
                var found = customersByMonth.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                vm.NewCustomerCounts.Add(found?.Count ?? 0);
            }

            // ── Users by role ────────────────────────
            var usersByRole = await _uow.Repository<User>().Query()
                .Where(u => u.IsActive)
                .Include(u => u.Role)
                .GroupBy(u => u.Role.RoleName)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            vm.RoleLabels = usersByRole.Select(g => g.Label).ToList();
            vm.RoleCounts = usersByRole.Select(g => g.Count).ToList();

            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
