using EV_ERP.Data;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Data.Seed
{
    /// <summary>
    /// Khởi tạo dữ liệu mẫu — gọi 1 lần khi DB trống.
    /// Sử dụng: trong Program.cs thêm await DbSeeder.SeedAsync(scope.ServiceProvider);
    /// Hoặc chạy file SQL đã có sẵn seed data.
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Chỉ seed nếu bảng Roles chưa có dữ liệu
            if (await context.Roles.AnyAsync()) return;

            // ── Roles ────────────────────────────────────
            var roles = new[]
            {
            new Role { RoleCode = "ADMIN",     RoleName = "Quản trị viên",        IsSystem = true },
            new Role { RoleCode = "MANAGER",   RoleName = "Quản lý",              IsSystem = true },
            new Role { RoleCode = "SALES",     RoleName = "Nhân viên kinh doanh", IsSystem = true },
            new Role { RoleCode = "WAREHOUSE", RoleName = "Thủ kho",              IsSystem = true },
            new Role { RoleCode = "ACCOUNTANT",RoleName = "Kế toán",              IsSystem = true },
        };
            context.Roles.AddRange(roles);
            await context.SaveChangesAsync();

            // ── Modules ──────────────────────────────────
            var modules = new[]
            {
            new Module { ModuleCode = "DASHBOARD",   ModuleName = "Dashboard & Báo cáo",        DisplayOrder = 1 },
            new Module { ModuleCode = "CUSTOMER",    ModuleName = "Quản lý Khách hàng",          DisplayOrder = 2 },
            new Module { ModuleCode = "VENDOR",      ModuleName = "Quản lý Nhà cung cấp",        DisplayOrder = 3 },
            new Module { ModuleCode = "PRODUCT",     ModuleName = "Quản lý Sản phẩm",            DisplayOrder = 4 },
            new Module { ModuleCode = "QUOTATION",   ModuleName = "Báo giá",                     DisplayOrder = 5 },
            new Module { ModuleCode = "SALES_ORDER", ModuleName = "Đơn bán hàng",                DisplayOrder = 6 },
            new Module { ModuleCode = "PURCHASE",    ModuleName = "Mua hàng",                    DisplayOrder = 7 },
            new Module { ModuleCode = "INVENTORY",   ModuleName = "Quản lý Kho",                 DisplayOrder = 8 },
            new Module { ModuleCode = "REPORT",      ModuleName = "Báo cáo cá nhân",             DisplayOrder = 9 },
            new Module { ModuleCode = "USER_MGMT",   ModuleName = "Quản lý User & Phân quyền",  DisplayOrder = 10 },
            new Module { ModuleCode = "TEMPLATE",    ModuleName = "Quản lý mẫu PDF",            DisplayOrder = 11 },
        };
            context.Modules.AddRange(modules);

            // ── Units ────────────────────────────────────
            var units = new[]
            {
            new Unit { UnitCode = "KG",    UnitName = "Kilogram" },
            new Unit { UnitCode = "G",     UnitName = "Gram" },
            new Unit { UnitCode = "LIT",   UnitName = "Lít" },
            new Unit { UnitCode = "CAI",   UnitName = "Cái" },
            new Unit { UnitCode = "HOP",   UnitName = "Hộp" },
            new Unit { UnitCode = "CHAI",  UnitName = "Chai" },
            new Unit { UnitCode = "GOI",   UnitName = "Gói" },
            new Unit { UnitCode = "THUNG", UnitName = "Thùng" },
            new Unit { UnitCode = "BO",    UnitName = "Bộ" },
        };
            context.Units.AddRange(units);

            // ── Customer Groups ──────────────────────────
            context.CustomerGroups.AddRange(
                new CustomerGroup { GroupCode = "VIP", GroupName = "Khách hàng VIP", PriorityLevel = 3 },
                new CustomerGroup { GroupCode = "REGULAR", GroupName = "Khách hàng thường", PriorityLevel = 2 },
                new CustomerGroup { GroupCode = "NEW", GroupName = "Khách hàng mới", PriorityLevel = 1 }
            );

            // ── Warehouses ───────────────────────────────
            var mainWarehouse = new Warehouse { WarehouseCode = "WH-MAIN", WarehouseName = "Kho chính", IsVirtual = false };
            var virtualWh = new Warehouse { WarehouseCode = "WH-VIRTUAL", WarehouseName = "Kho ảo (Dropship)", IsVirtual = true };
            context.Warehouses.AddRange(mainWarehouse, virtualWh);
            await context.SaveChangesAsync();

            // ── Warehouse Locations (kho chính) ──────────
            context.WarehouseLocations.AddRange(
                new WarehouseLocation { WarehouseId = mainWarehouse.WarehouseId, LocationCode = "A-01-01-01", LocationName = "Khu A > Dãy 1 > Kệ 1 > Tầng 1", Zone = "A", Aisle = "01", Rack = "01", Shelf = "01" },
                new WarehouseLocation { WarehouseId = mainWarehouse.WarehouseId, LocationCode = "A-01-01-02", LocationName = "Khu A > Dãy 1 > Kệ 1 > Tầng 2", Zone = "A", Aisle = "01", Rack = "01", Shelf = "02" },
                new WarehouseLocation { WarehouseId = mainWarehouse.WarehouseId, LocationCode = "A-01-02-01", LocationName = "Khu A > Dãy 1 > Kệ 2 > Tầng 1", Zone = "A", Aisle = "01", Rack = "02", Shelf = "01" },
                new WarehouseLocation { WarehouseId = mainWarehouse.WarehouseId, LocationCode = "B-01-01-01", LocationName = "Khu B > Dãy 1 > Kệ 1 > Tầng 1", Zone = "B", Aisle = "01", Rack = "01", Shelf = "01" },
                new WarehouseLocation { WarehouseId = mainWarehouse.WarehouseId, LocationCode = "C-TEMP-01", LocationName = "Khu C > Khu vực tạm", Zone = "C" }
            );

            // ── Admin User (password cần hash bằng BCrypt trong code) ──
            var adminRole = roles.First(r => r.RoleCode == "ADMIN");
            context.Users.Add(new User
            {
                UserCode = "NV-0001",
                FullName = "Quản trị viên",
                Email = "admin@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                RoleId = adminRole.RoleId
            });

            await context.SaveChangesAsync();
        }
    }
}
