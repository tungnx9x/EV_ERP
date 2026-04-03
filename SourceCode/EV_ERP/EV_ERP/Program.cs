using EV_ERP.Data;
using EV_ERP.Repositories;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Middleware;
using Serilog;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
// LOGGING — Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// DATABASE — Entity Framework Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }
    ));

// Add services to the container.
builder.Services.AddControllersWithViews();
// REPOSITORIES — Generic Repository + Unit of Work
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// SERVICES — Đăng ký thêm khi implement từng module
// builder.Services.AddScoped<ICustomerService, CustomerService>();
// builder.Services.AddScoped<IVendorService, VendorService>();
// builder.Services.AddScoped<IProductService, ProductService>();
// builder.Services.AddScoped<IQuotationService, QuotationService>();
// builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
// builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
// builder.Services.AddScoped<IInventoryService, InventoryService>();

// SESSION
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("AppSettings:SessionTimeoutMinutes", 30));
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".ERP.Session";
});

// HTTP CONTEXT
builder.Services.AddHttpContextAccessor();
// MVC
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;   // giữ PascalCase
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// BUILD APP
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── Start ────────────────────────────────────────────
Log.Information("═══ ERP EV — Starting on {Env} ═══",
    app.Environment.EnvironmentName);
app.Run();
