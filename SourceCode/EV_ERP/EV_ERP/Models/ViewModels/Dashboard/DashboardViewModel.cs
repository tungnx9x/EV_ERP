using EV_ERP.Helpers;

namespace EV_ERP.Models.ViewModels.Dashboard
{
    public class DashboardViewModel
    {
        public CurrentUser CurrentUser { get; set; } = null!;

        // ── Summary cards ────────────────────────────
        public int TotalCustomers { get; set; }
        public int TotalUsers { get; set; }

        // ── Chart: Customers by group ────────────────
        public List<string> CustomerGroupLabels { get; set; } = [];
        public List<int> CustomerGroupCounts { get; set; } = [];

        // ── Chart: New customers per month (last 6) ──
        public List<string> MonthLabels { get; set; } = [];
        public List<int> NewCustomerCounts { get; set; } = [];

        // ── Chart: Users by role ─────────────────────
        public List<string> RoleLabels { get; set; } = [];
        public List<int> RoleCounts { get; set; } = [];
    }
}
