using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Finance;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Purchases;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.System;
using EV_ERP.Models.Entities.Templates;
using EV_ERP.Models.Entities.Vendors;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── RBAC & Auth (7 bảng) ─────────────────────────
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<User> Users => Set<User>();
        public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();

        // ── Khách hàng (4 bảng) ──────────────────────────
        public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
        public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

        // ── Nhà cung cấp (2 bảng) ────────────────────────
        public DbSet<Vendor> Vendors => Set<Vendor>();
        public DbSet<VendorContact> VendorContacts => Set<VendorContact>();

        // ── Sản phẩm (6 bảng) ────────────────────────────
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<VendorPrice> VendorPrices => Set<VendorPrice>();
        public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();

        // ── Bán hàng (7 bảng) ────────────────────────────
        public DbSet<RFQ> RFQs => Set<RFQ>();
        public DbSet<Quotation> Quotations => Set<Quotation>();
        public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
        public DbSet<QuotationEmailHistory> QuotationEmailHistories => Set<QuotationEmailHistory>();
        public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
        public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

        // ── Mua hàng (3 bảng) ────────────────────────────
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<VendorInvoice> VendorInvoices => Set<VendorInvoice>();

        // ── Kho (7 bảng) ─────────────────────────────────
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
        public DbSet<InventoryRecord> Inventory => Set<InventoryRecord>();
        public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
        public DbSet<StockTransactionItem> StockTransactionItems => Set<StockTransactionItem>();
        public DbSet<StockCheck> StockChecks => Set<StockCheck>();
        public DbSet<StockCheckItem> StockCheckItems => Set<StockCheckItem>();

        // ── Công nợ & Tạm ứng (3 bảng) ──────────────────
        public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
        public DbSet<VendorPayment> VendorPayments => Set<VendorPayment>();
        public DbSet<AdvanceRequest> AdvanceRequests => Set<AdvanceRequest>();

        // ── PDF Template (3 bảng) ────────────────────────
        public DbSet<PdfTemplate> PdfTemplates => Set<PdfTemplate>();
        public DbSet<TemplateAssignment> TemplateAssignments => Set<TemplateAssignment>();
        public DbSet<GeneratedPdf> GeneratedPdfs => Set<GeneratedPdf>();

        // ── Hệ thống (3 bảng) ────────────────────────────
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        // =================================================================
        // FLUENT API CONFIGURATIONS
        // =================================================================
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // ─────────────────────────────────────────────
            // AUTH & RBAC
            // ─────────────────────────────────────────────
            mb.Entity<Module>(e =>
            {
                e.HasKey(x => x.ModuleId);
                e.HasIndex(x => x.ModuleCode).IsUnique();
                e.Property(x => x.ModuleCode).HasMaxLength(50);
                e.Property(x => x.ModuleName).HasMaxLength(100);
            });

            mb.Entity<Role>(e =>
            {
                e.HasKey(x => x.RoleId);
                e.HasIndex(x => x.RoleCode).IsUnique();
                e.Property(x => x.RoleCode).HasMaxLength(50);
                e.Property(x => x.RoleName).HasMaxLength(100);
            });

            mb.Entity<Permission>(e =>
            {
                e.HasKey(x => x.PermissionId);
                e.HasIndex(x => x.PermissionCode).IsUnique();
                e.Property(x => x.PermissionCode).HasMaxLength(100);
                e.Property(x => x.ActionType).HasMaxLength(20);
                e.HasOne(x => x.Module).WithMany(m => m.Permissions).HasForeignKey(x => x.ModuleId);
            });

            mb.Entity<RolePermission>(e =>
            {
                e.HasKey(x => x.RolePermissionId);
                e.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
                e.Property(x => x.DataScope).HasMaxLength(10).HasDefaultValue("ALL");
                e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
                e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId);
            });

            mb.Entity<User>(e =>
            {
                e.HasKey(x => x.UserId);
                e.HasIndex(x => x.UserCode).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.UserCode).HasMaxLength(20);
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.PasswordHash).HasMaxLength(500);
                e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
            });

            mb.Entity<LoginHistory>(e =>
            {
                e.ToTable("LoginHistory");
                e.HasKey(x => x.LoginHistoryId);
                e.HasIndex(x => new { x.UserId, x.LoginAt });
                e.HasOne(x => x.User).WithMany(u => u.LoginHistories).HasForeignKey(x => x.UserId);
            });

            mb.Entity<UserSession>(e =>
            {
                e.HasKey(x => x.SessionId);
                e.HasOne(x => x.User).WithMany(u => u.Sessions).HasForeignKey(x => x.UserId);
            });

            // ─────────────────────────────────────────────
            // CUSTOMERS
            // ─────────────────────────────────────────────
            mb.Entity<CustomerGroup>(e =>
            {
                e.HasKey(x => x.CustomerGroupId);
                e.HasIndex(x => x.GroupCode).IsUnique();
                e.Property(x => x.GroupCode).HasMaxLength(20);
            });

            mb.Entity<Customer>(e =>
            {
                e.HasKey(x => x.CustomerId);
                e.HasIndex(x => x.CustomerCode).IsUnique();
                e.HasIndex(x => x.CustomerName);
                e.HasIndex(x => x.Phone);
                e.Property(x => x.CustomerCode).HasMaxLength(20);
                e.Property(x => x.CustomerName).HasMaxLength(300);
                e.Property(x => x.TaxCode).HasMaxLength(20);
                e.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.CustomerGroup).WithMany(g => g.Customers).HasForeignKey(x => x.CustomerGroupId);
                e.HasOne(x => x.SalesPerson).WithMany().HasForeignKey(x => x.SalesPersonId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<CustomerContact>(e =>
            {
                e.HasKey(x => x.ContactId);
                e.HasOne(x => x.Customer).WithMany(c => c.Contacts).HasForeignKey(x => x.CustomerId);
            });

            mb.Entity<CustomerNote>(e =>
            {
                e.HasKey(x => x.NoteId);
                e.HasOne(x => x.Customer).WithMany(c => c.Notes).HasForeignKey(x => x.CustomerId);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // VENDORS
            // ─────────────────────────────────────────────
            mb.Entity<Vendor>(e =>
            {
                e.HasKey(x => x.VendorId);
                e.HasIndex(x => x.VendorCode).IsUnique();
                e.HasIndex(x => x.VendorName);
                e.Property(x => x.VendorCode).HasMaxLength(20);
                e.Property(x => x.VendorName).HasMaxLength(300);
                e.Property(x => x.AvgDeliveryDays).HasColumnType("decimal(5,1)");
                e.Property(x => x.QualityRating).HasColumnType("decimal(3,1)");
                e.Property(x => x.OnTimeRate).HasColumnType("decimal(5,2)");
            });

            mb.Entity<VendorContact>(e =>
            {
                e.HasKey(x => x.ContactId);
                e.HasOne(x => x.Vendor).WithMany(v => v.Contacts).HasForeignKey(x => x.VendorId);
            });

            // ─────────────────────────────────────────────
            // PRODUCTS
            // ─────────────────────────────────────────────
            mb.Entity<ProductCategory>(e =>
            {
                e.HasKey(x => x.CategoryId);
                e.HasIndex(x => x.CategoryCode).IsUnique();
                e.Property(x => x.CategoryCode).HasMaxLength(20);
                e.HasOne(x => x.ParentCategory).WithMany(c => c.SubCategories)
                 .HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<Unit>(e =>
            {
                e.HasKey(x => x.UnitId);
                e.HasIndex(x => x.UnitCode).IsUnique();
                e.Property(x => x.UnitCode).HasMaxLength(10);
                e.Property(x => x.UnitName).HasMaxLength(50);
            });

            mb.Entity<Product>(e =>
            {
                e.HasKey(x => x.ProductId);
                e.HasIndex(x => x.ProductCode).IsUnique();
                e.HasIndex(x => x.Barcode);
                e.Property(x => x.ProductCode).HasMaxLength(30);
                e.Property(x => x.ProductName).HasMaxLength(300);
                e.Property(x => x.Barcode).HasMaxLength(50);
                e.Property(x => x.DefaultSalePrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.DefaultPurchasePrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.Weight).HasColumnType("decimal(10,3)");
                e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId);
                e.HasOne(x => x.Unit).WithMany(u => u.Products).HasForeignKey(x => x.UnitId);
            });

            mb.Entity<ProductImage>(e =>
            {
                e.HasKey(x => x.ImageId);
                e.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId);
            });

            mb.Entity<VendorPrice>(e =>
            {
                e.HasKey(x => x.VendorPriceId);
                e.HasIndex(x => new { x.ProductId, x.VendorId });
                e.Property(x => x.PurchasePrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("VND");
                e.HasOne(x => x.Product).WithMany(p => p.VendorPrices).HasForeignKey(x => x.ProductId);
                e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId);
            });

            mb.Entity<CustomerPrice>(e =>
            {
                e.HasKey(x => x.CustomerPriceId);
                e.HasIndex(x => x.ProductId);
                e.Property(x => x.SalePrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("VND");
                e.HasOne(x => x.Product).WithMany(p => p.CustomerPrices).HasForeignKey(x => x.ProductId);
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CustomerGroup).WithMany().HasForeignKey(x => x.CustomerGroupId).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // SALES
            // ─────────────────────────────────────────────
            mb.Entity<RFQ>(e =>
            {
                e.ToTable("RFQs");
                e.HasKey(x => x.RfqId);
                e.HasIndex(x => x.RfqNo).IsUnique();
                e.HasIndex(x => x.CustomerId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.AssignedTo);
                e.Property(x => x.RfqNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("INPROGRESS");
                e.Property(x => x.Priority).HasMaxLength(10).HasDefaultValue("NORMAL");
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
                e.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<Quotation>(e =>
            {
                e.HasKey(x => x.QuotationId);
                e.HasIndex(x => x.QuotationNo).IsUnique();
                e.HasIndex(x => x.RfqId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.QuotationDate);
                e.Property(x => x.QuotationNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT");
                e.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TaxRate).HasColumnType("decimal(5,2)");
                e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("VND");
                e.HasOne(x => x.Rfq).WithMany(r => r.Quotations).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
                e.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SalesPerson).WithMany().HasForeignKey(x => x.SalesPersonId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.AmendFrom).WithMany().HasForeignKey(x => x.AmendFromId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<QuotationItem>(e =>
            {
                e.HasKey(x => x.QuotationItemId);
                e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
                e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.Quotation).WithMany(q => q.Items).HasForeignKey(x => x.QuotationId);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<QuotationEmailHistory>(e =>
            {
                e.ToTable("QuotationEmailHistory");
                e.HasKey(x => x.EmailHistoryId);
                e.HasOne(x => x.Quotation).WithMany(q => q.EmailHistories).HasForeignKey(x => x.QuotationId);
                e.HasOne(x => x.SentByUser).WithMany().HasForeignKey(x => x.SentBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<SalesOrder>(e =>
            {
                e.HasKey(x => x.SalesOrderId);
                e.HasIndex(x => x.SalesOrderNo).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.OrderDate);
                e.Property(x => x.SalesOrderNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(25).HasDefaultValue("DRAFT");
                e.Property(x => x.AdvanceStatus).HasMaxLength(20);
                e.Property(x => x.AdvanceAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");
                e.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TaxRate).HasColumnType("decimal(5,2)");
                e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("VND");
                e.HasOne(x => x.Rfq).WithMany(r => r.SalesOrders).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Quotation).WithOne(q => q.SalesOrder)
                 .HasForeignKey<SalesOrder>(x => x.QuotationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SalesPerson).WithMany().HasForeignKey(x => x.SalesPersonId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<SalesOrderItem>(e =>
            {
                e.HasKey(x => x.SOItemId);
                e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
                e.Property(x => x.DeliveredQty).HasColumnType("decimal(18,3)");
                e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.SalesOrder).WithMany(s => s.Items).HasForeignKey(x => x.SalesOrderId);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // PURCHASES
            // ─────────────────────────────────────────────
            mb.Entity<PurchaseOrder>(e =>
            {
                e.HasKey(x => x.PurchaseOrderId);
                e.HasIndex(x => x.PurchaseOrderNo).IsUnique();
                e.HasIndex(x => x.Status);
                e.Property(x => x.PurchaseOrderNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(25).HasDefaultValue("DRAFT");
                e.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
                e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TaxRate).HasColumnType("decimal(5,2)");
                e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("VND");
                e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DropshipCustomer).WithMany().HasForeignKey(x => x.DropshipCustomerId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<PurchaseOrderItem>(e =>
            {
                e.HasKey(x => x.POItemId);
                e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
                e.Property(x => x.ReceivedQty).HasColumnType("decimal(18,3)");
                e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.PurchaseOrder).WithMany(p => p.Items).HasForeignKey(x => x.PurchaseOrderId);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SalesOrderItem).WithMany().HasForeignKey(x => x.SOItemId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<VendorInvoice>(e =>
            {
                e.HasKey(x => x.VendorInvoiceId);
                e.Property(x => x.InvoiceNo).HasMaxLength(50);
                e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("UNPAID");
                e.HasOne(x => x.PurchaseOrder).WithMany(p => p.Invoices).HasForeignKey(x => x.PurchaseOrderId);
                e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // INVENTORY
            // ─────────────────────────────────────────────
            mb.Entity<Warehouse>(e =>
            {
                e.HasKey(x => x.WarehouseId);
                e.HasIndex(x => x.WarehouseCode).IsUnique();
                e.Property(x => x.WarehouseCode).HasMaxLength(20);
                e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<WarehouseLocation>(e =>
            {
                e.HasKey(x => x.LocationId);
                e.HasIndex(x => new { x.WarehouseId, x.LocationCode }).IsUnique();
                e.HasIndex(x => new { x.WarehouseId, x.Zone });
                e.Property(x => x.LocationCode).HasMaxLength(30);
                e.Property(x => x.LocationName).HasMaxLength(200);
                e.Property(x => x.Zone).HasMaxLength(50);
                e.Property(x => x.Aisle).HasMaxLength(50);
                e.Property(x => x.Rack).HasMaxLength(50);
                e.Property(x => x.Shelf).HasMaxLength(50);
                e.Property(x => x.Bin).HasMaxLength(50);
                e.Property(x => x.MaxCapacity).HasColumnType("decimal(18,3)");
                e.HasOne(x => x.Warehouse).WithMany(w => w.Locations).HasForeignKey(x => x.WarehouseId);
            });

            mb.Entity<InventoryRecord>(e =>
            {
                e.ToTable("Inventory");
                e.HasKey(x => x.InventoryId);
                e.HasIndex(x => new { x.ProductId, x.WarehouseId, x.LocationId }).IsUnique();
                e.Property(x => x.QuantityOnHand).HasColumnType("decimal(18,3)");
                e.Property(x => x.QuantityReserved).HasColumnType("decimal(18,3)");
                // Computed column — DB tự tính, EF chỉ đọc
                e.Property(x => x.QuantityAvailable)
                 .HasColumnType("decimal(21,3)")
                 .HasComputedColumnSql("[QuantityOnHand] - [QuantityReserved]", stored: false);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
                e.HasOne(x => x.Warehouse).WithMany(w => w.InventoryRecords).HasForeignKey(x => x.WarehouseId);
                e.HasOne(x => x.Location).WithMany(l => l.InventoryRecords).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<StockTransaction>(e =>
            {
                e.HasKey(x => x.TransactionId);
                e.HasIndex(x => x.TransactionNo).IsUnique();
                e.HasIndex(x => new { x.TransactionType, x.TransactionDate });
                e.Property(x => x.TransactionNo).HasMaxLength(20);
                e.Property(x => x.TransactionType).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT");
                e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
                e.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.DeliveryPerson).WithMany().HasForeignKey(x => x.DeliveryPersonId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.ConfirmedByUser).WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<StockTransactionItem>(e =>
            {
                e.HasKey(x => x.TransItemId);
                e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
                e.HasOne(x => x.Transaction).WithMany(t => t.Items).HasForeignKey(x => x.TransactionId);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<StockCheck>(e =>
            {
                e.HasKey(x => x.StockCheckId);
                e.HasIndex(x => x.StockCheckNo).IsUnique();
                e.Property(x => x.StockCheckNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT");
                e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<StockCheckItem>(e =>
            {
                e.HasKey(x => x.SCItemId);
                e.Property(x => x.SystemQty).HasColumnType("decimal(18,3)");
                e.Property(x => x.ActualQty).HasColumnType("decimal(18,3)");
                e.Property(x => x.Difference)
                 .HasColumnType("decimal(19,3)")
                 .HasComputedColumnSql("ISNULL([ActualQty], 0) - [SystemQty]", stored: false);
                e.HasOne(x => x.StockCheck).WithMany(s => s.Items).HasForeignKey(x => x.StockCheckId);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // FINANCE
            // ─────────────────────────────────────────────
            mb.Entity<CustomerPayment>(e =>
            {
                e.HasKey(x => x.PaymentId);
                e.HasIndex(x => x.PaymentNo).IsUnique();
                e.Property(x => x.PaymentNo).HasMaxLength(20);
                e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("CONFIRMED");
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
                e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<VendorPayment>(e =>
            {
                e.HasKey(x => x.PaymentId);
                e.HasIndex(x => x.PaymentNo).IsUnique();
                e.Property(x => x.PaymentNo).HasMaxLength(20);
                e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("CONFIRMED");
                e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId);
                e.HasOne(x => x.VendorInvoice).WithMany().HasForeignKey(x => x.VendorInvoiceId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // TEMPLATES
            // ─────────────────────────────────────────────
            mb.Entity<PdfTemplate>(e =>
            {
                e.HasKey(x => x.TemplateId);
                e.HasIndex(x => x.TemplateCode).IsUnique();
                e.HasIndex(x => new { x.TemplateType, x.IsActive });
                e.Property(x => x.TemplateCode).HasMaxLength(50);
                e.Property(x => x.TemplateType).HasMaxLength(20);
                e.Property(x => x.Language).HasMaxLength(5).HasDefaultValue("vi");
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            mb.Entity<TemplateAssignment>(e =>
            {
                e.HasKey(x => x.AssignmentId);
                e.HasIndex(x => new { x.TemplateId, x.TargetType, x.TargetId }).IsUnique();
                e.HasIndex(x => new { x.TargetType, x.TargetId, x.IsActive });
                e.Property(x => x.TargetType).HasMaxLength(20);
                e.HasOne(x => x.Template).WithMany(t => t.Assignments).HasForeignKey(x => x.TemplateId);
            });

            mb.Entity<GeneratedPdf>(e =>
            {
                e.HasKey(x => x.GeneratedPdfId);
                e.Property(x => x.ReferenceType).HasMaxLength(20);
                e.HasOne(x => x.Template).WithMany(t => t.GeneratedPdfs).HasForeignKey(x => x.TemplateId);
                e.HasOne(x => x.GeneratedByUser).WithMany().HasForeignKey(x => x.GeneratedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // SYSTEM
            // ─────────────────────────────────────────────
            mb.Entity<AuditLog>(e =>
            {
                e.HasKey(x => x.AuditLogId);
                e.HasIndex(x => new { x.TableName, x.RecordId });
                e.HasIndex(x => x.CreatedAt);
                e.Property(x => x.TableName).HasMaxLength(100);
                e.Property(x => x.ActionType).HasMaxLength(10);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<Notification>(e =>
            {
                e.HasKey(x => x.NotificationId);
                e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
                e.Property(x => x.NotificationType).HasMaxLength(30);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            });

            // ─────────────────────────────────────────────
            // ADVANCE REQUESTS
            // ─────────────────────────────────────────────
            mb.Entity<AdvanceRequest>(e =>
            {
                e.HasKey(x => x.AdvanceRequestId);
                e.HasIndex(x => x.RequestNo).IsUnique();
                e.HasIndex(x => x.SalesOrderId);
                e.HasIndex(x => x.Status);
                e.Property(x => x.RequestNo).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("PENDING");
                e.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.ActualSpent).HasColumnType("decimal(18,2)");
                e.Property(x => x.RefundAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.AdditionalAmount).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.SettledByUser).WithMany().HasForeignKey(x => x.SettledBy).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.RejectedByUser).WithMany().HasForeignKey(x => x.RejectedBy).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            // ─────────────────────────────────────────────
            // ATTACHMENTS
            // ─────────────────────────────────────────────
            mb.Entity<Attachment>(e =>
            {
                e.HasKey(x => x.AttachmentId);
                e.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.IsActive });
                e.HasIndex(x => x.FileCategory);
                e.Property(x => x.ReferenceType).HasMaxLength(30);
                e.Property(x => x.FileName).HasMaxLength(300);
                e.Property(x => x.FileUrl).HasMaxLength(500);
                e.Property(x => x.ContentType).HasMaxLength(100);
                e.Property(x => x.FileCategory).HasMaxLength(50);
                e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
