-- ============================================================================
-- ERP DATABASE - DOANH NGHIỆP THƯƠNG MẠI TRUNG GIAN (PHỤC VỤ KHÁCH SẠN)
-- Platform: SQL Server 2019+
-- Version:  1.3
-- Date:     2026-04-10
-- Changelog v1.3:
--   [!] XÓA PurchaseOrders + PurchaseOrderItems — gộp vào SalesOrders
--       Lý do: Mô hình trung gian mua thẳng từ PO khách hàng, không cần PO nội bộ
--   [~] SalesOrder — thêm luồng mua hàng: VendorId, BuyingAt, ReceivedAt...
--       Status mới: DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED
--   [~] VendorInvoices, StockTransactions, VendorPayments — FK chuyển sang SalesOrderId
--   [-] Bỏ index PO, bỏ seed/view liên quan PO
-- ============================================================================

-- Tạo database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ERP_ThuongMaiTrungGian')
    CREATE DATABASE ERP_ThuongMaiTrungGian;
GO

USE ERP_ThuongMaiTrungGian;
GO

-- ============================================================================
-- MODULE 1: QUẢN LÝ USER & PHÂN QUYỀN (RBAC)
-- ============================================================================

-- 1.1 Bảng Module hệ thống
CREATE TABLE Modules (
    ModuleId        INT IDENTITY(1,1) PRIMARY KEY,
    ModuleCode      NVARCHAR(50)    NOT NULL UNIQUE,      -- VD: 'CUSTOMER', 'VENDOR', 'PRODUCT'...
    ModuleName      NVARCHAR(100)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);

-- 1.2 Bảng Vai trò (Roles)
CREATE TABLE Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleCode        NVARCHAR(50)    NOT NULL UNIQUE,      -- VD: 'ADMIN', 'MANAGER', 'SALES'...
    RoleName        NVARCHAR(100)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    IsSystem        BIT             NOT NULL DEFAULT 0,   -- Role hệ thống không được xóa
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);

-- 1.3 Bảng Quyền (Permissions)
CREATE TABLE Permissions (
    PermissionId    INT IDENTITY(1,1) PRIMARY KEY,
    PermissionCode  NVARCHAR(100)   NOT NULL UNIQUE,      -- VD: 'CUSTOMER.VIEW', 'CUSTOMER.CREATE'
    PermissionName  NVARCHAR(200)   NOT NULL,
    ModuleId        INT             NOT NULL,
    ActionType      NVARCHAR(20)    NOT NULL,              -- VIEW, CREATE, EDIT, DELETE, EXPORT
    Description     NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Permissions_Module FOREIGN KEY (ModuleId) REFERENCES Modules(ModuleId)
);

-- 1.4 Bảng Role ↔ Permission (ma trận phân quyền)
CREATE TABLE RolePermissions (
    RolePermissionId INT IDENTITY(1,1) PRIMARY KEY,
    RoleId          INT             NOT NULL,
    PermissionId    INT             NOT NULL,
    -- Quyền phạm vi dữ liệu: ALL = toàn bộ, OWN = chỉ của mình
    DataScope       NVARCHAR(10)    NOT NULL DEFAULT 'ALL' CHECK (DataScope IN ('ALL', 'OWN', 'NONE')),
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_RolePerm_Role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_RolePerm_Perm FOREIGN KEY (PermissionId) REFERENCES Permissions(PermissionId),
    CONSTRAINT UQ_Role_Permission UNIQUE (RoleId, PermissionId)
);

-- 1.5 Bảng Người dùng (Users)
CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    UserCode        NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã nhân viên
    FullName        NVARCHAR(200)   NOT NULL,
    Email           NVARCHAR(200)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(500)   NOT NULL,              -- bcrypt hash
    Phone           NVARCHAR(20)    NULL,
    AvatarUrl       NVARCHAR(500)   NULL,
    RoleId          INT             NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    IsLocked        BIT             NOT NULL DEFAULT 0,
    LastLoginAt     DATETIME2       NULL,
    PasswordChangedAt DATETIME2     NULL,
    TwoFactorEnabled BIT            NOT NULL DEFAULT 0,
    TwoFactorSecret NVARCHAR(200)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NULL,
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

-- 1.6 Bảng Log đăng nhập
CREATE TABLE LoginHistory (
    LoginHistoryId  BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT             NOT NULL,
    LoginAt         DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    IpAddress       NVARCHAR(50)    NULL,
    UserAgent       NVARCHAR(500)   NULL,
    DeviceInfo      NVARCHAR(200)   NULL,
    IsSuccess       BIT             NOT NULL DEFAULT 1,
    FailReason      NVARCHAR(200)   NULL,
    CONSTRAINT FK_LoginHistory_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

-- 1.7 Bảng Session
CREATE TABLE UserSessions (
    SessionId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          INT             NOT NULL,
    Token           NVARCHAR(500)   NOT NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt       DATETIME2       NOT NULL,
    LastActivityAt  DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    IpAddress       NVARCHAR(50)    NULL,
    IsRevoked       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT FK_Session_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE 2: QUẢN LÝ KHÁCH HÀNG (CUSTOMER MANAGEMENT)
-- ============================================================================

-- 2.1 Bảng Nhóm khách hàng
CREATE TABLE CustomerGroups (
    CustomerGroupId INT IDENTITY(1,1) PRIMARY KEY,
    GroupCode       NVARCHAR(20)    NOT NULL UNIQUE,
    GroupName       NVARCHAR(100)   NOT NULL,              -- VD: VIP, Thường, Mới
    Description     NVARCHAR(500)   NULL,
    PriorityLevel   INT             NOT NULL DEFAULT 0,    -- Cấp độ ưu tiên
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);

-- 2.2 Bảng Khách hàng (Khách sạn)
CREATE TABLE Customers (
    CustomerId      INT IDENTITY(1,1) PRIMARY KEY,
    CustomerCode    NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã KH tự sinh: KH-0001
    CustomerName    NVARCHAR(300)   NOT NULL,              -- Tên khách sạn
    TaxCode         NVARCHAR(20)    NULL,                  -- Mã số thuế
    Address         NVARCHAR(500)   NULL,
    City            NVARCHAR(100)   NULL,
    District        NVARCHAR(100)   NULL,
    Ward            NVARCHAR(100)   NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(200)   NULL,
    Website         NVARCHAR(200)   NULL,
    CustomerGroupId INT             NULL,
    SalesPersonId   INT             NULL,                  -- Nhân viên kinh doanh phụ trách
    PaymentTermDays INT             NOT NULL DEFAULT 30,   -- Hạn thanh toán mặc định (ngày)
    CreditLimit     DECIMAL(18,2)   NULL,                  -- Hạn mức công nợ
    InternalNotes   NVARCHAR(MAX)   NULL,                  -- Ghi chú nội bộ
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NULL,
    UpdatedBy       INT             NULL,
    CONSTRAINT FK_Customer_Group FOREIGN KEY (CustomerGroupId) REFERENCES CustomerGroups(CustomerGroupId),
    CONSTRAINT FK_Customer_SalesPerson FOREIGN KEY (SalesPersonId) REFERENCES Users(UserId)
);

-- 2.3 Bảng Liên hệ khách hàng (nhiều contact/khách hàng)
CREATE TABLE CustomerContacts (
    ContactId       INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT             NOT NULL,
    ContactName     NVARCHAR(200)   NOT NULL,
    JobTitle        NVARCHAR(100)   NULL,                  -- VD: Bếp trưởng, QL Mua hàng
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(200)   NULL,
    IsPrimary       BIT             NOT NULL DEFAULT 0,    -- Liên hệ chính
    Notes           NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Contact_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);

-- 2.4 Bảng Ghi chú nội bộ khách hàng (timeline)
CREATE TABLE CustomerNotes (
    NoteId          INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId      INT             NOT NULL,
    NoteContent     NVARCHAR(MAX)   NOT NULL,
    CreatedBy       INT             NOT NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Note_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Note_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE 3: QUẢN LÝ NHÀ CUNG CẤP (VENDOR MANAGEMENT)
-- ============================================================================

-- 3.1 Bảng Nhà cung cấp
CREATE TABLE Vendors (
    VendorId        INT IDENTITY(1,1) PRIMARY KEY,
    VendorCode      NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã NCC: NCC-0001
    VendorName      NVARCHAR(300)   NOT NULL,
    TaxCode         NVARCHAR(20)    NULL,
    Address         NVARCHAR(500)   NULL,
    City            NVARCHAR(100)   NULL,
    District        NVARCHAR(100)   NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(200)   NULL,
    Website         NVARCHAR(200)   NULL,
    BankAccountNo   NVARCHAR(30)    NULL,
    BankName        NVARCHAR(200)   NULL,
    BankBranch      NVARCHAR(200)   NULL,
    ContactPerson   NVARCHAR(200)   NULL,
    ContactPhone    NVARCHAR(20)    NULL,
    ContactEmail    NVARCHAR(200)   NULL,
    PaymentTermDays INT             NOT NULL DEFAULT 30,
    -- Đánh giá NCC
    AvgDeliveryDays DECIMAL(5,1)    NULL,                  -- Thời gian giao hàng TB
    QualityRating   DECIMAL(3,1)    NULL,                  -- Đánh giá chất lượng (1-5)
    OnTimeRate      DECIMAL(5,2)    NULL,                  -- Tỉ lệ giao đúng hạn (%)
    InternalNotes   NVARCHAR(MAX)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NULL,
    UpdatedBy       INT             NULL
);

-- 3.2 Bảng Liên hệ nhà cung cấp
CREATE TABLE VendorContacts (
    ContactId       INT IDENTITY(1,1) PRIMARY KEY,
    VendorId        INT             NOT NULL,
    ContactName     NVARCHAR(200)   NOT NULL,
    JobTitle        NVARCHAR(100)   NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(200)   NULL,
    IsPrimary       BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_VContact_Vendor FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId)
);


-- ============================================================================
-- MODULE 4: QUẢN LÝ SẢN PHẨM (PRODUCT MANAGEMENT)
-- ============================================================================

-- 4.1 Bảng Danh mục sản phẩm
CREATE TABLE ProductCategories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    CategoryCode    NVARCHAR(20)    NOT NULL UNIQUE,
    CategoryName    NVARCHAR(200)   NOT NULL,
    ParentCategoryId INT            NULL,                  -- Danh mục cha (hỗ trợ cây phân cấp)
    Description     NVARCHAR(500)   NULL,
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Category_Parent FOREIGN KEY (ParentCategoryId) REFERENCES ProductCategories(CategoryId)
);

-- 4.2 Bảng Đơn vị tính
CREATE TABLE Units (
    UnitId          INT IDENTITY(1,1) PRIMARY KEY,
    UnitCode        NVARCHAR(10)    NOT NULL UNIQUE,       -- VD: KG, LIT, CAI, HOP
    UnitName        NVARCHAR(50)    NOT NULL,              -- VD: Kilogram, Lít, Cái, Hộp
    IsActive        BIT             NOT NULL DEFAULT 1
);

-- 4.3 Bảng Sản phẩm
CREATE TABLE Products (
    ProductId       INT IDENTITY(1,1) PRIMARY KEY,
    ProductCode     NVARCHAR(30)    NOT NULL UNIQUE,       -- Mã SP: SP-00001
    ProductName     NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(MAX)   NULL,
    CategoryId      INT             NULL,
    UnitId          INT             NOT NULL,
    Barcode         NVARCHAR(50)    NULL,                  -- Mã barcode (EAN-8, EAN-13, Code 128...)
    BarcodeType     NVARCHAR(20)    NULL,                  -- EAN8, EAN13, QR, CODE128
    ImageUrl        NVARCHAR(500)   NULL,
    DefaultSalePrice  DECIMAL(18,2) NULL,                  -- Giá bán mặc định
    DefaultPurchasePrice DECIMAL(18,2) NULL,               -- Giá mua mặc định
    MinStockLevel   INT             NOT NULL DEFAULT 0,    -- Tồn kho tối thiểu (ngưỡng cảnh báo)
    Weight          DECIMAL(10,3)   NULL,                  -- Khối lượng
    WeightUnit      NVARCHAR(10)    NULL,                  -- kg, g...
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NULL,
    UpdatedBy       INT             NULL,
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES ProductCategories(CategoryId),
    CONSTRAINT FK_Product_Unit FOREIGN KEY (UnitId) REFERENCES Units(UnitId)
);

-- 4.4 Hình ảnh sản phẩm (nhiều ảnh)
CREATE TABLE ProductImages (
    ImageId         INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    ImageUrl        NVARCHAR(500)   NOT NULL,
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    IsPrimary       BIT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ProdImage_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);

-- 4.5 Bảng Giá mua theo NCC (một SP mua từ nhiều NCC với giá khác nhau)
CREATE TABLE VendorPrices (
    VendorPriceId   INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    VendorId        INT             NOT NULL,
    PurchasePrice   DECIMAL(18,2)   NOT NULL,
    Currency        NVARCHAR(3)     NOT NULL DEFAULT 'VND',
    MinOrderQty     INT             NULL,                  -- Số lượng đặt tối thiểu
    LeadTimeDays    INT             NULL,                  -- Thời gian giao hàng (ngày)
    EffectiveFrom   DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    EffectiveTo     DATE            NULL,
    Notes           NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_VPrice_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_VPrice_Vendor FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId)
);

-- 4.6 Bảng Giá bán theo khách hàng / nhóm KH
CREATE TABLE CustomerPrices (
    CustomerPriceId INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    CustomerId      INT             NULL,                  -- Giá riêng cho KH cụ thể
    CustomerGroupId INT             NULL,                  -- Hoặc giá theo nhóm KH
    SalePrice       DECIMAL(18,2)   NOT NULL,
    Currency        NVARCHAR(3)     NOT NULL DEFAULT 'VND',
    MinQty          INT             NULL,                  -- Số lượng tối thiểu để áp giá
    EffectiveFrom   DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    EffectiveTo     DATE            NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_CPrice_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_CPrice_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_CPrice_Group FOREIGN KEY (CustomerGroupId) REFERENCES CustomerGroups(CustomerGroupId),
    -- Phải có ít nhất 1 trong 2: CustomerId hoặc CustomerGroupId
    CONSTRAINT CK_CPrice_Target CHECK (CustomerId IS NOT NULL OR CustomerGroupId IS NOT NULL)
);


-- ============================================================================
-- MODULE 5: BÁO GIÁ / SALES ORDER
-- ============================================================================

-- 5.0 Bảng Yêu cầu Báo giá (RFQ — Request For Quotation)
--     Điểm bắt đầu của toàn bộ quy trình: KH gửi yêu cầu → NV mua hàng tiếp nhận
CREATE TABLE RFQs (
    RfqId           INT IDENTITY(1,1) PRIMARY KEY,
    RfqNo           NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã RFQ: RFQ-202604-001
    CustomerId      INT             NOT NULL,
    ContactId       INT             NULL,
    RequestDate     DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    Description     NVARCHAR(MAX)   NULL,                  -- Mô tả yêu cầu từ KH
    -- INPROGRESS → COMPLETED (tự động khi SO completed)
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'INPROGRESS'
                    CHECK (Status IN ('INPROGRESS','COMPLETED','CANCELLED')),
    AssignedTo      INT             NULL,                  -- NV mua hàng được phân công
    Priority        NVARCHAR(10)    NOT NULL DEFAULT 'NORMAL'
                    CHECK (Priority IN ('LOW','NORMAL','HIGH','URGENT')),
    Notes           NVARCHAR(MAX)   NULL,
    CompletedAt     DATETIME2       NULL,
    CancelledAt     DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_RFQ_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_RFQ_Contact FOREIGN KEY (ContactId) REFERENCES CustomerContacts(ContactId),
    CONSTRAINT FK_RFQ_AssignedTo FOREIGN KEY (AssignedTo) REFERENCES Users(UserId),
    CONSTRAINT FK_RFQ_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 5.1 Bảng Báo giá (Quotation)
CREATE TABLE Quotations (
    QuotationId     INT IDENTITY(1,1) PRIMARY KEY,
    QuotationNo     NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã báo giá: BG-202604-001
    RfqId           INT             NULL,                  -- Liên kết RFQ gốc
    CustomerId      INT             NOT NULL,
    ContactId       INT             NULL,                  -- Người liên hệ phía KH
    QuotationDate   DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    ExpiryDate      DATE            NULL,                  -- Ngày hết hạn báo giá
    -- Trạng thái: DRAFT → SENT → APPROVED / REJECTED / AMEND / EXPIRED
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'DRAFT'
                    CHECK (Status IN ('DRAFT','SENT','APPROVED','REJECTED','AMEND','EXPIRED')),
    SubTotal        DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Tổng trước chiết khấu & VAT
    DiscountType    NVARCHAR(10)    NULL,                  -- PERCENT hoặc AMOUNT
    DiscountValue   DECIMAL(18,2)   NULL DEFAULT 0,        -- Giá trị chiết khấu toàn đơn
    DiscountAmount  DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Số tiền chiết khấu tính được
    TaxRate         DECIMAL(5,2)    NOT NULL DEFAULT 10,   -- % VAT (mặc định 10%)
    TaxAmount       DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Tiền VAT
    TotalAmount     DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Tổng cộng sau CK + VAT
    Currency        NVARCHAR(3)     NOT NULL DEFAULT 'VND',
    PaymentTerms    NVARCHAR(500)   NULL,                  -- Điều khoản thanh toán
    Notes           NVARCHAR(MAX)   NULL,                  -- Ghi chú
    InternalNotes   NVARCHAR(MAX)   NULL,
    TemplateId      INT             NULL,                  -- Mẫu PDF sử dụng
    SalesPersonId   INT             NOT NULL,              -- NV mua hàng tạo
    AmendFromId     INT             NULL,                  -- Nếu AMEND: link đến Quotation gốc
    SentAt          DATETIME2       NULL,                  -- Thời điểm gửi
    ApprovedAt      DATETIME2       NULL,                  -- Thời điểm KH duyệt
    RejectedAt      DATETIME2       NULL,
    RejectReason    NVARCHAR(500)   NULL,
    ExpiredAt       DATETIME2       NULL,
    CancelledAt     DATETIME2       NULL,
    CancelReason    NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    UpdatedBy       INT             NULL,
    CONSTRAINT FK_Quot_RFQ FOREIGN KEY (RfqId) REFERENCES RFQs(RfqId),
    CONSTRAINT FK_Quot_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Quot_Contact FOREIGN KEY (ContactId) REFERENCES CustomerContacts(ContactId),
    CONSTRAINT FK_Quot_SalesPerson FOREIGN KEY (SalesPersonId) REFERENCES Users(UserId),
    CONSTRAINT FK_Quot_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Quot_AmendFrom FOREIGN KEY (AmendFromId) REFERENCES Quotations(QuotationId)
);

-- 5.2 Chi tiết báo giá
CREATE TABLE QuotationItems (
    QuotationItemId INT IDENTITY(1,1) PRIMARY KEY,
    QuotationId     INT             NOT NULL,
    ProductId       INT             NOT NULL,
    ProductName     NVARCHAR(300)   NOT NULL,              -- Snapshot tên SP tại thời điểm báo giá
    UnitName        NVARCHAR(50)    NOT NULL,              -- Snapshot đơn vị
    Quantity        DECIMAL(18,3)   NOT NULL,
    UnitPrice       DECIMAL(18,2)   NOT NULL,              -- Đơn giá
    DiscountType    NVARCHAR(10)    NULL,                  -- PERCENT hoặc AMOUNT (chiết khấu dòng)
    DiscountValue   DECIMAL(18,2)   NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    LineTotal       DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- = Qty * UnitPrice - DiscountAmount
    SortOrder       INT             NOT NULL DEFAULT 0,
    Notes           NVARCHAR(500)   NULL,
    CONSTRAINT FK_QuotItem_Quotation FOREIGN KEY (QuotationId) REFERENCES Quotations(QuotationId),
    CONSTRAINT FK_QuotItem_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);

-- 5.3 Lịch sử gửi email báo giá
CREATE TABLE QuotationEmailHistory (
    EmailHistoryId  INT IDENTITY(1,1) PRIMARY KEY,
    QuotationId     INT             NOT NULL,
    SentTo          NVARCHAR(200)   NOT NULL,              -- Email người nhận
    SentCc          NVARCHAR(500)   NULL,
    Subject         NVARCHAR(500)   NOT NULL,
    Body            NVARCHAR(MAX)   NULL,
    PdfFileUrl      NVARCHAR(500)   NULL,                  -- Link file PDF đính kèm
    SentAt          DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    SentBy          INT             NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'SENT' CHECK (Status IN ('SENT','FAILED','BOUNCED')),
    ErrorMessage    NVARCHAR(500)   NULL,
    CONSTRAINT FK_QEmail_Quot FOREIGN KEY (QuotationId) REFERENCES Quotations(QuotationId),
    CONSTRAINT FK_QEmail_User FOREIGN KEY (SentBy) REFERENCES Users(UserId)
);

-- 5.4 Bảng Đơn bán hàng (Sales Order)
CREATE TABLE SalesOrders (
    SalesOrderId    INT IDENTITY(1,1) PRIMARY KEY,
    SalesOrderNo    NVARCHAR(20)    NOT NULL UNIQUE,       -- Mã SO: SO-202604-001
    QuotationId     INT             NULL,                  -- Liên kết báo giá gốc
    RfqId           INT             NULL,                  -- Liên kết RFQ gốc (để tự động complete)
    CustomerId      INT             NOT NULL,
    ContactId       INT             NULL,
    OrderDate       DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    ExpectedDeliveryDate DATE       NULL,

    -- ══════════════════════════════════════════════════
    -- Luồng trạng thái đơn giản hóa (gộp SO + PO):
    --   DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED
    --   Bất kỳ lúc nào cũng có thể → CANCELLED
    -- ══════════════════════════════════════════════════
    -- DRAFT:      SO vừa tạo từ Quotation APPROVED, NV nhập mã PO KH
    -- WAIT:       Đã gửi đề nghị tạm ứng, chờ phê duyệt + nhận tiền
    -- BUYING:     Đang mua hàng từ NCC (= PO nội bộ cũ)
    -- RECEIVED:   Hàng đã về kho, chờ xuất giao
    -- DELIVERING: Đã giao cho vận chuyển
    -- DELIVERED:  KH đã nhận hàng & ký biên bản
    -- COMPLETED:  Đã quyết toán xong (hoàn ứng hoặc thanh toán bổ sung)
    -- ══════════════════════════════════════════════════
    Status          NVARCHAR(25)    NOT NULL DEFAULT 'DRAFT'
                    CHECK (Status IN ('DRAFT','WAIT','BUYING','RECEIVED','DELIVERING','DELIVERED','COMPLETED','CANCELLED')),

    -- ── Thông tin PO phía khách sạn ──
    CustomerPoNo    NVARCHAR(50)    NULL,                  -- Mã PO mà KH gửi
    CustomerPoFile  NVARCHAR(500)   NULL,                  -- URL file PO upload

    -- ── Thông tin mua hàng (thay thế PO nội bộ) ──
    VendorId        INT             NULL,                  -- NCC mua hàng
    ExpectedReceiveDate DATE        NULL,                  -- Dự kiến hàng về kho
    BuyingNotes     NVARCHAR(MAX)   NULL,                  -- Ghi chú quá trình mua
    BuyingAt        DATETIME2       NULL,                  -- Thời điểm bắt đầu mua
    ReceivedAt      DATETIME2       NULL,                  -- Thời điểm hàng về kho

    -- ── Tạm ứng ──
    AdvanceAmount   DECIMAL(18,2)   NULL DEFAULT 0,        -- Số tiền đề nghị tạm ứng
    AdvanceStatus   NVARCHAR(20)    NULL                   -- PENDING, APPROVED, RECEIVED, SETTLED
                    CHECK (AdvanceStatus IN ('PENDING','APPROVED','RECEIVED','SETTLED')),
    AdvanceApprovedAt DATETIME2     NULL,
    AdvanceReceivedAt DATETIME2     NULL,

    -- ── Giá trị đơn hàng (bán cho KH) ──
    SubTotal        DECIMAL(18,2)   NOT NULL DEFAULT 0,
    DiscountType    NVARCHAR(10)    NULL,
    DiscountValue   DECIMAL(18,2)   NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    TaxRate         DECIMAL(5,2)    NOT NULL DEFAULT 10,
    TaxAmount       DECIMAL(18,2)   NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Tổng bán cho KH
    Currency        NVARCHAR(3)     NOT NULL DEFAULT 'VND',
    PaymentTermDays INT             NOT NULL DEFAULT 30,
    PaymentDueDate  DATE            NULL,

    -- ── Chi phí mua (giá vốn) ──
    PurchaseCost    DECIMAL(18,2)   NULL DEFAULT 0,        -- Tổng chi phí mua từ NCC
    ProfitAmount    AS (TotalAmount - ISNULL(PurchaseCost, 0)),  -- Lợi nhuận (computed)

    ShippingAddress NVARCHAR(500)   NULL,
    Notes           NVARCHAR(MAX)   NULL,
    InternalNotes   NVARCHAR(MAX)   NULL,
    SalesPersonId   INT             NOT NULL,

    -- ── Quyết toán ──
    ActualCost      DECIMAL(18,2)   NULL,                  -- Chi phí thực tế cuối cùng
    SettlementNotes NVARCHAR(500)   NULL,

    -- ── Dropshipping ──
    IsDropship      BIT             NOT NULL DEFAULT 0,
    DropshipAddress NVARCHAR(500)   NULL,

    -- ── Timestamps ──
    DeliveringAt    DATETIME2       NULL,                  -- Thời điểm giao vận chuyển
    DeliveredAt     DATETIME2       NULL,                  -- Thời điểm KH nhận
    CompletedAt     DATETIME2       NULL,
    CancelledAt     DATETIME2       NULL,
    CancelReason    NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    UpdatedBy       INT             NULL,
    CONSTRAINT FK_SO_Quotation FOREIGN KEY (QuotationId) REFERENCES Quotations(QuotationId),
    CONSTRAINT FK_SO_RFQ FOREIGN KEY (RfqId) REFERENCES RFQs(RfqId),
    CONSTRAINT FK_SO_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_SO_Contact FOREIGN KEY (ContactId) REFERENCES CustomerContacts(ContactId),
    CONSTRAINT FK_SO_Vendor FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId),
    CONSTRAINT FK_SO_SalesPerson FOREIGN KEY (SalesPersonId) REFERENCES Users(UserId),
    CONSTRAINT FK_SO_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 5.6 Chi tiết đơn hàng (bán + mua gộp)
CREATE TABLE SalesOrderItems (
    SOItemId        INT IDENTITY(1,1) PRIMARY KEY,
    SalesOrderId    INT             NOT NULL,
    ProductId       INT             NOT NULL,
    ProductName     NVARCHAR(300)   NOT NULL,
    UnitName        NVARCHAR(50)    NOT NULL,
    Quantity        DECIMAL(18,3)   NOT NULL,
    DeliveredQty    DECIMAL(18,3)   NOT NULL DEFAULT 0,    -- Số lượng đã giao
    UnitPrice       DECIMAL(18,2)   NOT NULL,              -- Giá bán cho KH
    PurchasePrice   DECIMAL(18,2)   NULL,                  -- Giá mua từ NCC (giá vốn)
    DiscountType    NVARCHAR(10)    NULL,
    DiscountValue   DECIMAL(18,2)   NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    LineTotal       DECIMAL(18,2)   NOT NULL DEFAULT 0,    -- Doanh thu dòng
    LineCost        DECIMAL(18,2)   NULL DEFAULT 0,        -- Chi phí dòng = Qty * PurchasePrice
    SortOrder       INT             NOT NULL DEFAULT 0,
    Notes           NVARCHAR(500)   NULL,
    CONSTRAINT FK_SOItem_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_SOItem_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);


-- ============================================================================
-- MODULE 6: HÓA ĐƠN NHÀ CUNG CẤP (không còn PO — gắn trực tiếp vào SO)
-- ============================================================================

-- 6.1 Bảng Hóa đơn nhà cung cấp (gắn với SO thay vì PO)
CREATE TABLE VendorInvoices (
    VendorInvoiceId INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNo       NVARCHAR(50)    NOT NULL,              -- Số hóa đơn NCC
    SalesOrderId    INT             NOT NULL,              -- Gắn trực tiếp vào SO
    VendorId        INT             NOT NULL,
    InvoiceDate     DATE            NOT NULL,
    DueDate         DATE            NOT NULL,
    TotalAmount     DECIMAL(18,2)   NOT NULL,
    PaidAmount      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    Currency        NVARCHAR(3)     NOT NULL DEFAULT 'VND',
    -- UNPAID → PARTIALLY_PAID → PAID
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'UNPAID'
                    CHECK (Status IN ('UNPAID','PARTIALLY_PAID','PAID','CANCELLED')),
    Notes           NVARCHAR(500)   NULL,
    AttachmentUrl   NVARCHAR(500)   NULL,                  -- Scan hóa đơn
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_VInv_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_VInv_Vendor FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId),
    CONSTRAINT FK_VInv_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE 7: QUẢN LÝ KHO (INVENTORY MANAGEMENT)
-- ============================================================================

-- 7.1 Bảng Kho
CREATE TABLE Warehouses (
    WarehouseId     INT IDENTITY(1,1) PRIMARY KEY,
    WarehouseCode   NVARCHAR(20)    NOT NULL UNIQUE,
    WarehouseName   NVARCHAR(200)   NOT NULL,
    Address         NVARCHAR(500)   NULL,
    ManagerId       INT             NULL,                  -- Thủ kho phụ trách
    IsVirtual       BIT             NOT NULL DEFAULT 0,    -- Kho ảo (dropshipping)
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Warehouse_Manager FOREIGN KEY (ManagerId) REFERENCES Users(UserId)
);

-- 7.2 Bảng Vị trí trong kho (Khu vực → Dãy → Kệ → Tầng → Ô)
--     Thiết kế phẳng (flat) — mỗi vị trí là một bản ghi đầy đủ đường dẫn,
--     không dùng self-referencing tree vì vị trí kho ít thay đổi & cần query nhanh.
CREATE TABLE WarehouseLocations (
    LocationId      INT IDENTITY(1,1) PRIMARY KEY,
    WarehouseId     INT             NOT NULL,
    LocationCode    NVARCHAR(30)    NOT NULL,              -- Mã vị trí: A-01-02-03 (Khu A, Dãy 1, Kệ 2, Tầng 3)
    LocationName    NVARCHAR(200)   NOT NULL,              -- Tên hiển thị: Khu A > Dãy 1 > Kệ 2 > Tầng 3
    Zone            NVARCHAR(50)    NULL,                  -- Khu vực (A, B, C hoặc Khu Khô, Khu Lạnh...)
    Aisle           NVARCHAR(50)    NULL,                  -- Dãy / Lối đi
    Rack            NVARCHAR(50)    NULL,                  -- Kệ / Giá
    Shelf           NVARCHAR(50)    NULL,                  -- Tầng trên kệ
    Bin             NVARCHAR(50)    NULL,                  -- Ô / Ngăn cụ thể
    MaxCapacity     DECIMAL(18,3)   NULL,                  -- Sức chứa tối đa (theo đơn vị chung)
    Description     NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_WLoc_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(WarehouseId),
    CONSTRAINT UQ_WLoc_Code UNIQUE (WarehouseId, LocationCode)
);

-- 7.3 Bảng Tồn kho (realtime) — theo sản phẩm + kho + VỊ TRÍ
CREATE TABLE Inventory (
    InventoryId     INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    WarehouseId     INT             NOT NULL,
    LocationId      INT             NULL,                  -- Vị trí cụ thể trong kho (NULL = chưa xếp vị trí)
    QuantityOnHand  DECIMAL(18,3)   NOT NULL DEFAULT 0,    -- Tồn kho thực
    QuantityReserved DECIMAL(18,3)  NOT NULL DEFAULT 0,    -- Đã đặt trước (SO chưa xuất)
    QuantityAvailable AS (QuantityOnHand - QuantityReserved), -- Khả dụng (computed)
    LastUpdatedAt   DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Inv_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_Inv_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(WarehouseId),
    CONSTRAINT FK_Inv_Location FOREIGN KEY (LocationId) REFERENCES WarehouseLocations(LocationId),
    CONSTRAINT UQ_Inv_Product_Warehouse_Location UNIQUE (ProductId, WarehouseId, LocationId)
);

-- 7.4 Bảng Phiếu nhập/xuất kho
--     Status tách theo TransactionType:
--       INBOUND:  DRAFT → CONFIRMED → CANCELLED
--       OUTBOUND: DRAFT → DELIVERING → DELIVERED → CANCELLED
CREATE TABLE StockTransactions (
    TransactionId   BIGINT IDENTITY(1,1) PRIMARY KEY,
    TransactionNo   NVARCHAR(20)    NOT NULL UNIQUE,       -- NK-202604-001, XK-202604-001
    TransactionType NVARCHAR(20)    NOT NULL,              -- INBOUND, OUTBOUND, ADJUSTMENT, RETURN
    WarehouseId     INT             NOT NULL,
    -- Liên kết nguồn gốc (SO là trung tâm, không còn PO)
    SalesOrderId    INT             NULL,                  -- Nhập kho / Xuất kho đều gắn SO
    TransactionDate DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),

    -- Status chung (DELIVERING & DELIVERED chỉ dùng cho OUTBOUND)
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'DRAFT'
                    CHECK (Status IN ('DRAFT','CONFIRMED','DELIVERING','DELIVERED','CANCELLED')),

    Notes           NVARCHAR(MAX)   NULL,
    IsDropship      BIT             NOT NULL DEFAULT 0,    -- Ghi nhận ảo (dropship)

    -- ── Thông tin giao hàng (chỉ dùng cho OUTBOUND) ──
    DeliveryPersonId INT            NULL,                  -- Nhân viên / đơn vị vận chuyển
    DeliveryNote    NVARCHAR(MAX)   NULL,                  -- Biên bản giao nhận
    ReceiverName    NVARCHAR(200)   NULL,                  -- Người nhận hàng phía KH
    ReceiverPhone   NVARCHAR(20)    NULL,
    ReceivedSignatureUrl NVARCHAR(500) NULL,               -- Ảnh/scan chữ ký xác nhận
    DeliveredAt     DATETIME2       NULL,                  -- KH nhận & ký

    ConfirmedAt     DATETIME2       NULL,
    ConfirmedBy     INT             NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_STrans_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(WarehouseId),
    CONSTRAINT FK_STrans_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_STrans_DeliveryPerson FOREIGN KEY (DeliveryPersonId) REFERENCES Users(UserId),
    CONSTRAINT FK_STrans_ConfBy FOREIGN KEY (ConfirmedBy) REFERENCES Users(UserId),
    CONSTRAINT FK_STrans_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 7.4 Chi tiết phiếu nhập/xuất
CREATE TABLE StockTransactionItems (
    TransItemId     BIGINT IDENTITY(1,1) PRIMARY KEY,
    TransactionId   BIGINT          NOT NULL,
    ProductId       INT             NOT NULL,
    LocationId      INT             NULL,                  -- Vị trí nhập vào / xuất từ
    Barcode         NVARCHAR(50)    NULL,                  -- Barcode quét được
    Quantity        DECIMAL(18,3)   NOT NULL,              -- Số lượng (dương = nhập, âm = xuất)
    UnitName        NVARCHAR(50)    NOT NULL,
    Notes           NVARCHAR(500)   NULL,
    CONSTRAINT FK_STItem_Trans FOREIGN KEY (TransactionId) REFERENCES StockTransactions(TransactionId),
    CONSTRAINT FK_STItem_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_STItem_Location FOREIGN KEY (LocationId) REFERENCES WarehouseLocations(LocationId)
);

-- 7.5 Bảng Kiểm kê (Stock Check / Inventory Count)
CREATE TABLE StockChecks (
    StockCheckId    INT IDENTITY(1,1) PRIMARY KEY,
    StockCheckNo    NVARCHAR(20)    NOT NULL UNIQUE,       -- KK-202604-001
    WarehouseId     INT             NOT NULL,
    CheckDate       DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'DRAFT'
                    CHECK (Status IN ('DRAFT','IN_PROGRESS','COMPLETED','CANCELLED')),
    Notes           NVARCHAR(MAX)   NULL,
    CompletedAt     DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_SC_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(WarehouseId),
    CONSTRAINT FK_SC_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 7.6 Chi tiết kiểm kê
CREATE TABLE StockCheckItems (
    SCItemId        INT IDENTITY(1,1) PRIMARY KEY,
    StockCheckId    INT             NOT NULL,
    ProductId       INT             NOT NULL,
    LocationId      INT             NULL,                  -- Kiểm kê theo vị trí cụ thể
    SystemQty       DECIMAL(18,3)   NOT NULL,              -- Tồn kho trên hệ thống
    ActualQty       DECIMAL(18,3)   NULL,                  -- Tồn kho thực tế (nhập khi kiểm)
    Difference      AS (ISNULL(ActualQty, 0) - SystemQty), -- Chênh lệch (computed)
    Notes           NVARCHAR(500)   NULL,
    CONSTRAINT FK_SCItem_SC FOREIGN KEY (StockCheckId) REFERENCES StockChecks(StockCheckId),
    CONSTRAINT FK_SCItem_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_SCItem_Location FOREIGN KEY (LocationId) REFERENCES WarehouseLocations(LocationId)
);


-- ============================================================================
-- MODULE 8: CÔNG NỢ (ACCOUNTS RECEIVABLE / PAYABLE)
-- ============================================================================

-- 8.1 Bảng Thanh toán từ khách hàng (Accounts Receivable - Phải thu)
CREATE TABLE CustomerPayments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    PaymentNo       NVARCHAR(20)    NOT NULL UNIQUE,       -- TT-KH-202604-001
    CustomerId      INT             NOT NULL,
    SalesOrderId    INT             NULL,
    PaymentDate     DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    Amount          DECIMAL(18,2)   NOT NULL,
    PaymentMethod   NVARCHAR(50)    NOT NULL,              -- CASH, TRANSFER, CHECK
    BankReference   NVARCHAR(100)   NULL,                  -- Số tham chiếu chuyển khoản
    Notes           NVARCHAR(500)   NULL,
    AttachmentUrl   NVARCHAR(500)   NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'CONFIRMED'
                    CHECK (Status IN ('CONFIRMED','CANCELLED')),
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_CPay_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_CPay_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_CPay_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 8.2 Bảng Thanh toán cho NCC (Accounts Payable - Phải trả)
CREATE TABLE VendorPayments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    PaymentNo       NVARCHAR(20)    NOT NULL UNIQUE,       -- TT-NCC-202604-001
    VendorId        INT             NOT NULL,
    VendorInvoiceId INT             NULL,
    SalesOrderId    INT             NULL,                  -- Gắn với SO (thay vì PO)
    PaymentDate     DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    Amount          DECIMAL(18,2)   NOT NULL,
    PaymentMethod   NVARCHAR(50)    NOT NULL,
    BankReference   NVARCHAR(100)   NULL,
    Notes           NVARCHAR(500)   NULL,
    AttachmentUrl   NVARCHAR(500)   NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'CONFIRMED'
                    CHECK (Status IN ('CONFIRMED','CANCELLED')),
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_VPay_Vendor FOREIGN KEY (VendorId) REFERENCES Vendors(VendorId),
    CONSTRAINT FK_VPay_VInv FOREIGN KEY (VendorInvoiceId) REFERENCES VendorInvoices(VendorInvoiceId),
    CONSTRAINT FK_VPay_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_VPay_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE 9: XUẤT PDF THEO MẪU (PDF TEMPLATE)
-- ============================================================================

-- 9.1 Bảng mẫu PDF (Template)
CREATE TABLE PdfTemplates (
    TemplateId      INT IDENTITY(1,1) PRIMARY KEY,
    TemplateCode    NVARCHAR(50)    NOT NULL UNIQUE,
    TemplateName    NVARCHAR(200)   NOT NULL,              -- VD: Mẫu báo giá VIP tiếng Anh
    TemplateType    NVARCHAR(20)    NOT NULL,              -- QUOTATION, SALES_ORDER, DELIVERY_NOTE
                    CHECK (TemplateType IN ('QUOTATION','SALES_ORDER','DELIVERY_NOTE')),
    Language        NVARCHAR(5)     NOT NULL DEFAULT 'vi', -- vi, en
    HtmlContent     NVARCHAR(MAX)   NOT NULL,              -- Nội dung HTML/CSS template
    Description     NVARCHAR(500)   NULL,
    PreviewImageUrl NVARCHAR(500)   NULL,
    IsDefault       BIT             NOT NULL DEFAULT 0,    -- Template mặc định cho TemplateType này
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_Template_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- 9.2 Bảng gán Template cho đối tượng cụ thể
--     Logic khi nhấn "Xuất PDF" trên Báo giá cho KH X (nhóm VIP):
--       1. Tìm template gán riêng cho KH X          (TargetType = 'CUSTOMER',  TargetId = X.CustomerId)
--       2. Không có → tìm template gán cho nhóm VIP  (TargetType = 'CUSTOMER_GROUP', TargetId = VIP.GroupId)
--       3. Không có → dùng template IsDefault = 1 cho TemplateType = 'QUOTATION'
--       4. Không có default → hiện danh sách tất cả template QUOTATION cho user chọn
CREATE TABLE TemplateAssignments (
    AssignmentId    INT IDENTITY(1,1) PRIMARY KEY,
    TemplateId      INT             NOT NULL,
    TargetType      NVARCHAR(20)    NOT NULL,              -- CUSTOMER, CUSTOMER_GROUP
                    CHECK (TargetType IN ('CUSTOMER','CUSTOMER_GROUP')),
    TargetId        INT             NOT NULL,              -- CustomerId hoặc CustomerGroupId
    Priority        INT             NOT NULL DEFAULT 0,    -- Ưu tiên cao hơn = dùng trước (CUSTOMER=10 > GROUP=5)
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_TAssign_Template FOREIGN KEY (TemplateId) REFERENCES PdfTemplates(TemplateId),
    CONSTRAINT UQ_TAssign UNIQUE (TemplateId, TargetType, TargetId)
);

-- 9.3 Bảng lưu file PDF đã xuất
CREATE TABLE GeneratedPdfs (
    GeneratedPdfId  INT IDENTITY(1,1) PRIMARY KEY,
    TemplateId      INT             NOT NULL,
    ReferenceType   NVARCHAR(20)    NOT NULL,              -- QUOTATION, SALES_ORDER
    ReferenceId     INT             NOT NULL,              -- ID của Quotation/SO
    FileUrl         NVARCHAR(500)   NOT NULL,
    FileSize        INT             NULL,                  -- bytes
    GeneratedAt     DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    GeneratedBy     INT             NOT NULL,
    CONSTRAINT FK_GenPdf_Template FOREIGN KEY (TemplateId) REFERENCES PdfTemplates(TemplateId),
    CONSTRAINT FK_GenPdf_User FOREIGN KEY (GeneratedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE 8.5: TẠM ỨNG / HOÀN ỨNG (ADVANCE MANAGEMENT)
-- ============================================================================

-- Bảng Đề nghị tạm ứng (gắn với SO)
-- Luồng: NV mua hàng đề nghị → Quản lý phê duyệt → Nhận tiền → Quyết toán
CREATE TABLE AdvanceRequests (
    AdvanceRequestId INT IDENTITY(1,1) PRIMARY KEY,
    RequestNo       NVARCHAR(20)    NOT NULL UNIQUE,       -- TU-202604-001
    SalesOrderId    INT             NOT NULL,
    RequestDate     DATE            NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    RequestedAmount DECIMAL(18,2)   NOT NULL,              -- Số tiền đề nghị tạm ứng
    Purpose         NVARCHAR(500)   NOT NULL,              -- Mục đích tạm ứng

    -- PENDING → APPROVED → RECEIVED → SETTLING → SETTLED → REJECTED
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'PENDING'
                    CHECK (Status IN ('PENDING','APPROVED','RECEIVED','SETTLING','SETTLED','REJECTED')),

    ApprovedBy      INT             NULL,
    ApprovedAt      DATETIME2       NULL,
    ApprovedAmount  DECIMAL(18,2)   NULL,                  -- Số tiền được duyệt (có thể khác đề nghị)
    ReceivedAt      DATETIME2       NULL,                  -- Thời điểm nhận tiền mặt/CK

    -- ── Quyết toán ──
    ActualSpent     DECIMAL(18,2)   NULL,                  -- Chi thực tế
    RefundAmount    DECIMAL(18,2)   NULL,                  -- Hoàn ứng (nếu chi < tạm ứng)
    AdditionalAmount DECIMAL(18,2)  NULL,                  -- Thanh toán thêm (nếu chi > tạm ứng)
    SettledAt       DATETIME2       NULL,
    SettledBy       INT             NULL,

    RejectedBy      INT             NULL,
    RejectedAt      DATETIME2       NULL,
    RejectReason    NVARCHAR(500)   NULL,
    Notes           NVARCHAR(MAX)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT FK_Adv_SO FOREIGN KEY (SalesOrderId) REFERENCES SalesOrders(SalesOrderId),
    CONSTRAINT FK_Adv_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Adv_SettledBy FOREIGN KEY (SettledBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Adv_RejectedBy FOREIGN KEY (RejectedBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Adv_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE BỔ SUNG: ATTACHMENTS (Đính kèm file dùng chung)
-- ============================================================================

-- Bảng Attachments — polymorphic (1 bảng chung cho mọi đối tượng)
-- Thay vì mỗi bảng tự có 1 cột AttachmentUrl, bảng này cho phép đính kèm
-- nhiều file cho bất kỳ bản ghi nào: RFQ, Quotation, SO, PO, StockTransaction...
CREATE TABLE Attachments (
    AttachmentId    INT IDENTITY(1,1) PRIMARY KEY,
    ReferenceType   NVARCHAR(30)    NOT NULL,              -- RFQ, QUOTATION, SALES_ORDER, PURCHASE_ORDER, STOCK_TRANSACTION, ADVANCE_REQUEST, VENDOR_INVOICE
    ReferenceId     INT             NOT NULL,              -- ID của bản ghi tương ứng
    FileName        NVARCHAR(300)   NOT NULL,              -- Tên file gốc: PO_KhachSan_ABC.pdf
    FileUrl         NVARCHAR(500)   NOT NULL,              -- URL lưu trữ
    FileSize        BIGINT          NULL,                  -- Bytes
    ContentType     NVARCHAR(100)   NULL,                  -- MIME type: application/pdf, image/jpeg
    FileCategory    NVARCHAR(50)    NULL,                  -- Phân loại: CUSTOMER_PO, DELIVERY_RECEIPT, ADVANCE_DOC, INVOICE, OTHER
    Description     NVARCHAR(500)   NULL,
    UploadedAt      DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UploadedBy      INT             NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT FK_Attach_User FOREIGN KEY (UploadedBy) REFERENCES Users(UserId)
);


-- ============================================================================
-- MODULE BỔ SUNG: AUDIT LOG & NOTIFICATIONS
-- ============================================================================

-- Bảng Audit Log (theo dõi mọi thay đổi)
CREATE TABLE AuditLogs (
    AuditLogId      BIGINT IDENTITY(1,1) PRIMARY KEY,
    TableName       NVARCHAR(100)   NOT NULL,
    RecordId        INT             NOT NULL,
    ActionType      NVARCHAR(10)    NOT NULL,              -- INSERT, UPDATE, DELETE
    OldValues       NVARCHAR(MAX)   NULL,                  -- JSON
    NewValues       NVARCHAR(MAX)   NULL,                  -- JSON
    ChangedFields   NVARCHAR(MAX)   NULL,                  -- JSON: danh sách cột thay đổi
    UserId          INT             NULL,
    IpAddress       NVARCHAR(50)    NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Audit_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

-- Bảng Thông báo (cảnh báo tồn kho, nhắc công nợ...)
CREATE TABLE Notifications (
    NotificationId  BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT             NOT NULL,              -- Người nhận thông báo
    Title           NVARCHAR(200)   NOT NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    NotificationType NVARCHAR(30)   NOT NULL,              -- LOW_STOCK, PAYMENT_DUE, ORDER_UPDATE...
    ReferenceType   NVARCHAR(50)    NULL,                  -- Bảng liên quan
    ReferenceId     INT             NULL,                  -- ID bản ghi liên quan
    IsRead          BIT             NOT NULL DEFAULT 0,
    ReadAt          DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Notif_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);


-- ============================================================================
-- INDEXES (tối ưu hiệu năng)
-- ============================================================================

-- Users & Auth
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_RoleId ON Users(RoleId);
CREATE INDEX IX_LoginHistory_UserId ON LoginHistory(UserId, LoginAt DESC);
CREATE INDEX IX_Sessions_UserId ON UserSessions(UserId, ExpiresAt);

-- Customers
CREATE INDEX IX_Customers_Code ON Customers(CustomerCode);
CREATE INDEX IX_Customers_Name ON Customers(CustomerName);
CREATE INDEX IX_Customers_Phone ON Customers(Phone);
CREATE INDEX IX_Customers_GroupId ON Customers(CustomerGroupId);
CREATE INDEX IX_Customers_SalesPersonId ON Customers(SalesPersonId);
CREATE INDEX IX_CustomerContacts_CustomerId ON CustomerContacts(CustomerId);

-- Vendors
CREATE INDEX IX_Vendors_Code ON Vendors(VendorCode);
CREATE INDEX IX_Vendors_Name ON Vendors(VendorName);

-- Products
CREATE INDEX IX_Products_Code ON Products(ProductCode);
CREATE INDEX IX_Products_Name ON Products(ProductName);
CREATE INDEX IX_Products_Barcode ON Products(Barcode);
CREATE INDEX IX_Products_CategoryId ON Products(CategoryId);
CREATE INDEX IX_VendorPrices_Product ON VendorPrices(ProductId, VendorId);
CREATE INDEX IX_CustomerPrices_Product ON CustomerPrices(ProductId);

-- RFQs
CREATE INDEX IX_RFQ_No ON RFQs(RfqNo);
CREATE INDEX IX_RFQ_CustomerId ON RFQs(CustomerId);
CREATE INDEX IX_RFQ_Status ON RFQs(Status);
CREATE INDEX IX_RFQ_AssignedTo ON RFQs(AssignedTo);

-- Quotations
CREATE INDEX IX_Quotations_No ON Quotations(QuotationNo);
CREATE INDEX IX_Quotations_RfqId ON Quotations(RfqId);
CREATE INDEX IX_Quotations_CustomerId ON Quotations(CustomerId);
CREATE INDEX IX_Quotations_Status ON Quotations(Status);
CREATE INDEX IX_Quotations_SalesPersonId ON Quotations(SalesPersonId);
CREATE INDEX IX_Quotations_Date ON Quotations(QuotationDate DESC);
CREATE INDEX IX_QuotItems_QuotId ON QuotationItems(QuotationId);

-- Sales Orders (gộp cả mua hàng)
CREATE INDEX IX_SO_No ON SalesOrders(SalesOrderNo);
CREATE INDEX IX_SO_CustomerId ON SalesOrders(CustomerId);
CREATE INDEX IX_SO_VendorId ON SalesOrders(VendorId);
CREATE INDEX IX_SO_Status ON SalesOrders(Status);
CREATE INDEX IX_SO_SalesPersonId ON SalesOrders(SalesPersonId);
CREATE INDEX IX_SO_OrderDate ON SalesOrders(OrderDate DESC);
CREATE INDEX IX_SOItems_SOId ON SalesOrderItems(SalesOrderId);

-- Vendor Invoices (gắn SO)
CREATE INDEX IX_VInvoices_SOId ON VendorInvoices(SalesOrderId);
CREATE INDEX IX_VInvoices_VendorId ON VendorInvoices(VendorId, Status);

-- Inventory
CREATE INDEX IX_Inventory_ProductId ON Inventory(ProductId);
CREATE INDEX IX_Inventory_LocationId ON Inventory(LocationId);
CREATE INDEX IX_WarehouseLocations_WarehouseId ON WarehouseLocations(WarehouseId);
CREATE INDEX IX_WarehouseLocations_Zone ON WarehouseLocations(WarehouseId, Zone);
CREATE INDEX IX_StockTrans_Type ON StockTransactions(TransactionType, TransactionDate DESC);
CREATE INDEX IX_StockTrans_WarehouseId ON StockTransactions(WarehouseId);
CREATE INDEX IX_StockTransItems_TransId ON StockTransactionItems(TransactionId);
CREATE INDEX IX_StockTransItems_LocationId ON StockTransactionItems(LocationId);

-- Payments & Invoices
CREATE INDEX IX_CPayments_CustomerId ON CustomerPayments(CustomerId);
CREATE INDEX IX_VPayments_VendorId ON VendorPayments(VendorId);
CREATE INDEX IX_VPayments_SOId ON VendorPayments(SalesOrderId);

-- Audit & Notifications
CREATE INDEX IX_AuditLog_Table ON AuditLogs(TableName, RecordId);
CREATE INDEX IX_AuditLog_CreatedAt ON AuditLogs(CreatedAt DESC);
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId, IsRead, CreatedAt DESC);

-- Templates
CREATE INDEX IX_PdfTemplates_Type ON PdfTemplates(TemplateType, IsActive);
CREATE INDEX IX_TemplateAssignments_Target ON TemplateAssignments(TargetType, TargetId, IsActive);
CREATE INDEX IX_TemplateAssignments_TemplateId ON TemplateAssignments(TemplateId);

-- Advance Requests
CREATE INDEX IX_AdvReq_SOId ON AdvanceRequests(SalesOrderId);
CREATE INDEX IX_AdvReq_Status ON AdvanceRequests(Status);

-- Attachments (polymorphic lookup)
CREATE INDEX IX_Attach_Ref ON Attachments(ReferenceType, ReferenceId, IsActive);
CREATE INDEX IX_Attach_Category ON Attachments(FileCategory);


-- ============================================================================
-- DỮ LIỆU KHỞI TẠO (SEED DATA)
-- ============================================================================

-- Modules
INSERT INTO Modules (ModuleCode, ModuleName, DisplayOrder) VALUES
('DASHBOARD',   N'Dashboard & Báo cáo',       1),
('CUSTOMER',    N'Quản lý Khách hàng',         2),
('VENDOR',      N'Quản lý Nhà cung cấp',       3),
('PRODUCT',     N'Quản lý Sản phẩm',           4),
('QUOTATION',   N'Báo giá',                    5),
('SALES_ORDER', N'Đơn bán hàng',               6),
('PURCHASE',    N'Mua hàng / PO',              7),
('INVENTORY',   N'Quản lý Kho',                8),
('REPORT',      N'Báo cáo cá nhân',            9),
('USER_MGMT',   N'Quản lý User & Phân quyền', 10),
('TEMPLATE',    N'Quản lý mẫu PDF',           11);

-- Roles
INSERT INTO Roles (RoleCode, RoleName, Description, IsSystem) VALUES
('ADMIN',       N'Quản trị viên',       N'Toàn quyền hệ thống, quản lý user',                      1),
('MANAGER',     N'Quản lý',             N'Xem và quản lý toàn bộ nghiệp vụ, không quản lý user',   1),
('SALES',       N'Nhân viên kinh doanh', N'Tạo báo giá, theo dõi đơn hàng của mình',               1),
('WAREHOUSE',   N'Thủ kho',             N'Quản lý nhập xuất kho, xem PO',                          1),
('ACCOUNTANT',  N'Kế toán',             N'Quản lý công nợ, hóa đơn, xem báo cáo tài chính',       1);

-- Đơn vị tính phổ biến
INSERT INTO Units (UnitCode, UnitName) VALUES
('KG',   N'Kilogram'),
('G',    N'Gram'),
('LIT',  N'Lít'),
('ML',   N'Mililít'),
('CAI',  N'Cái'),
('HOP',  N'Hộp'),
('CHAI', N'Chai'),
('GOI',  N'Gói'),
('THUNG',N'Thùng'),
('BO',   N'Bộ'),
('MET',  N'Mét'),
('TAM',  N'Tấm'),
('CUON', N'Cuộn'),
('DOI',  N'Đôi');

-- Nhóm khách hàng mặc định
INSERT INTO CustomerGroups (GroupCode, GroupName, PriorityLevel) VALUES
('VIP',     N'Khách hàng VIP',     3),
('REGULAR', N'Khách hàng thường',  2),
('NEW',     N'Khách hàng mới',     1);

-- Kho mặc định
INSERT INTO Warehouses (WarehouseCode, WarehouseName, IsVirtual) VALUES
('WH-MAIN',    N'Kho chính',            0),
('WH-VIRTUAL', N'Kho ảo (Dropship)',    1);

-- Vị trí mẫu trong Kho chính (WarehouseId = 1)
INSERT INTO WarehouseLocations (WarehouseId, LocationCode, LocationName, Zone, Aisle, Rack, Shelf) VALUES
(1, 'A-01-01-01', N'Khu A > Dãy 1 > Kệ 1 > Tầng 1', N'A', N'01', N'01', N'01'),
(1, 'A-01-01-02', N'Khu A > Dãy 1 > Kệ 1 > Tầng 2', N'A', N'01', N'01', N'02'),
(1, 'A-01-01-03', N'Khu A > Dãy 1 > Kệ 1 > Tầng 3', N'A', N'01', N'01', N'03'),
(1, 'A-01-02-01', N'Khu A > Dãy 1 > Kệ 2 > Tầng 1', N'A', N'01', N'02', N'01'),
(1, 'A-01-02-02', N'Khu A > Dãy 1 > Kệ 2 > Tầng 2', N'A', N'01', N'02', N'02'),
(1, 'B-01-01-01', N'Khu B > Dãy 1 > Kệ 1 > Tầng 1', N'B', N'01', N'01', N'01'),
(1, 'B-01-01-02', N'Khu B > Dãy 1 > Kệ 1 > Tầng 2', N'B', N'01', N'01', N'02'),
(1, 'C-TEMP-01',  N'Khu C > Khu vực tạm',              N'C', NULL,  NULL,  NULL);

-- Tạo tài khoản Admin mặc định (password: Admin@123 — cần đổi ngay)
-- Lưu ý: PasswordHash bên dưới chỉ là placeholder, cần hash bằng bcrypt trong code
INSERT INTO Users (UserCode, FullName, Email, PasswordHash, RoleId, CreatedBy)
VALUES ('NV-0001', N'Quản trị viên', 'admin@company.com',
        '$2a$11$4uZKGa.KPzDhP1vB4o25K.GU8TTlWZVPU6nzqNVgfcVqD.kdZ5iQW', 1, NULL);


-- ============================================================================
-- VIEW HỮU ÍCH (DASHBOARD & BÁO CÁO)
-- ============================================================================

-- View: Tổng hợp công nợ phải thu (AR) theo khách hàng
GO
CREATE OR ALTER VIEW vw_AccountsReceivable AS
SELECT
    c.CustomerId,
    c.CustomerCode,
    c.CustomerName,
    c.CustomerGroupId,
    c.SalesPersonId,
    ISNULL(so.TotalSales, 0)        AS TotalSales,
    ISNULL(cp.TotalPaid, 0)         AS TotalPaid,
    ISNULL(so.TotalSales, 0) - ISNULL(cp.TotalPaid, 0) AS OutstandingAmount
FROM Customers c
LEFT JOIN (
    SELECT CustomerId, SUM(TotalAmount) AS TotalSales
    FROM SalesOrders
    WHERE Status NOT IN ('CANCELLED')
    GROUP BY CustomerId
) so ON c.CustomerId = so.CustomerId
LEFT JOIN (
    SELECT CustomerId, SUM(Amount) AS TotalPaid
    FROM CustomerPayments
    WHERE Status = 'CONFIRMED'
    GROUP BY CustomerId
) cp ON c.CustomerId = cp.CustomerId
WHERE c.IsActive = 1;
GO

-- View: Tổng hợp công nợ phải trả (AP) theo NCC
CREATE OR ALTER VIEW vw_AccountsPayable AS
SELECT
    v.VendorId,
    v.VendorCode,
    v.VendorName,
    ISNULL(vi.TotalInvoiced, 0)     AS TotalPurchases,
    ISNULL(vp.TotalPaid, 0)         AS TotalPaid,
    ISNULL(vi.TotalInvoiced, 0) - ISNULL(vp.TotalPaid, 0) AS OutstandingAmount
FROM Vendors v
LEFT JOIN (
    SELECT VendorId, SUM(TotalAmount) AS TotalInvoiced
    FROM VendorInvoices
    WHERE Status NOT IN ('CANCELLED')
    GROUP BY VendorId
) vi ON v.VendorId = vi.VendorId
LEFT JOIN (
    SELECT VendorId, SUM(Amount) AS TotalPaid
    FROM VendorPayments
    WHERE Status = 'CONFIRMED'
    GROUP BY VendorId
) vp ON v.VendorId = vp.VendorId
WHERE v.IsActive = 1;
GO

-- View: Tồn kho cảnh báo thấp
CREATE OR ALTER VIEW vw_LowStockAlert AS
SELECT
    p.ProductId,
    p.ProductCode,
    p.ProductName,
    p.MinStockLevel,
    ISNULL(i.TotalOnHand, 0)    AS TotalOnHand,
    ISNULL(i.TotalReserved, 0)  AS TotalReserved,
    ISNULL(i.TotalOnHand, 0) - ISNULL(i.TotalReserved, 0) AS AvailableQty,
    pc.CategoryName
FROM Products p
LEFT JOIN (
    SELECT ProductId,
           SUM(QuantityOnHand) AS TotalOnHand,
           SUM(QuantityReserved) AS TotalReserved
    FROM Inventory
    GROUP BY ProductId
) i ON p.ProductId = i.ProductId
LEFT JOIN ProductCategories pc ON p.CategoryId = pc.CategoryId
WHERE p.IsActive = 1
  AND ISNULL(i.TotalOnHand, 0) - ISNULL(i.TotalReserved, 0) <= p.MinStockLevel;
GO

-- View: Tồn kho chi tiết theo vị trí (thủ kho dùng để tra cứu "SP này nằm ở đâu?")
CREATE OR ALTER VIEW vw_InventoryByLocation AS
SELECT
    i.InventoryId,
    w.WarehouseCode,
    w.WarehouseName,
    wl.LocationCode,
    wl.LocationName,
    wl.Zone,
    wl.Aisle,
    wl.Rack,
    wl.Shelf,
    p.ProductId,
    p.ProductCode,
    p.ProductName,
    p.Barcode,
    i.QuantityOnHand,
    i.QuantityReserved,
    (i.QuantityOnHand - i.QuantityReserved) AS QuantityAvailable,
    u.UnitName
FROM Inventory i
INNER JOIN Products p ON i.ProductId = p.ProductId
INNER JOIN Warehouses w ON i.WarehouseId = w.WarehouseId
INNER JOIN Units u ON p.UnitId = u.UnitId
LEFT JOIN WarehouseLocations wl ON i.LocationId = wl.LocationId
WHERE p.IsActive = 1 AND w.IsActive = 1;
GO

-- View: Doanh số theo nhân viên (phục vụ báo cáo KPI)
CREATE OR ALTER VIEW vw_SalesPerformance AS
SELECT
    u.UserId,
    u.UserCode,
    u.FullName,
    -- Báo giá
    COUNT(DISTINCT q.QuotationId)   AS TotalQuotations,
    COUNT(DISTINCT CASE WHEN q.Status = 'APPROVED' THEN q.QuotationId END) AS ApprovedQuotations,
    -- Tỉ lệ chuyển đổi
    CASE
        WHEN COUNT(DISTINCT q.QuotationId) = 0 THEN 0
        ELSE CAST(COUNT(DISTINCT CASE WHEN q.Status = 'APPROVED' THEN q.QuotationId END) AS DECIMAL(5,2))
             / COUNT(DISTINCT q.QuotationId) * 100
    END AS ConversionRate,
    -- Doanh số
    COUNT(DISTINCT so.SalesOrderId) AS TotalSalesOrders,
    ISNULL(SUM(CASE WHEN so.Status NOT IN ('CANCELLED') THEN so.TotalAmount ELSE 0 END), 0) AS TotalRevenue
FROM Users u
LEFT JOIN Quotations q ON u.UserId = q.SalesPersonId
LEFT JOIN SalesOrders so ON u.UserId = so.SalesPersonId
WHERE u.IsActive = 1
GROUP BY u.UserId, u.UserCode, u.FullName;
GO

-- View: Top sản phẩm bán chạy
CREATE OR ALTER VIEW vw_TopSellingProducts AS
SELECT
    p.ProductId,
    p.ProductCode,
    p.ProductName,
    pc.CategoryName,
    SUM(soi.Quantity)           AS TotalQtySold,
    SUM(soi.LineTotal)          AS TotalRevenue,
    COUNT(DISTINCT so.SalesOrderId) AS OrderCount
FROM SalesOrderItems soi
INNER JOIN SalesOrders so ON soi.SalesOrderId = so.SalesOrderId
INNER JOIN Products p ON soi.ProductId = p.ProductId
LEFT JOIN ProductCategories pc ON p.CategoryId = pc.CategoryId
WHERE so.Status NOT IN ('CANCELLED')
GROUP BY p.ProductId, p.ProductCode, p.ProductName, pc.CategoryName;
GO


-- ============================================================================
-- GHI CHÚ THIẾT KẾ
-- ============================================================================
/*
    ╔══════════════════════════════════════════════════════════════╗
    ║                    TỔNG QUAN THIẾT KẾ v1.3                   ║
    ╠══════════════════════════════════════════════════════════════╣
    ║                                                              ║
    ║  Tổng số bảng:  42 bảng (-2 so với v1.2: bỏ PO + POItems)  ║
    ║  Tổng số views:  6 views                                     ║
    ║                                                              ║
    ║  PHÂN NHÓM:                                                  ║
    ║  ─────────────────────────────────────────────────────────    ║
    ║  RBAC & Auth:     7 bảng                                     ║
    ║  Khách hàng:      4 bảng                                     ║
    ║  Nhà cung cấp:    2 bảng                                     ║
    ║  Sản phẩm:        6 bảng                                     ║
    ║  Đơn hàng:        7 bảng (RFQ, Quot, QuotItems, QuotEmail,  ║
    ║                    SO, SOItems, VendorInvoice)               ║
    ║  Kho:             7 bảng                                     ║
    ║  Công nợ:         3 bảng (CustPayment, VendPayment, AdvReq) ║
    ║  PDF Template:    3 bảng                                     ║
    ║  Hệ thống:        3 bảng (AuditLog, Notification, Attach)   ║
    ║                                                              ║
    ╠══════════════════════════════════════════════════════════════╣
    ║               THAY ĐỔI v1.3 (so với v1.2)                   ║
    ╠══════════════════════════════════════════════════════════════╣
    ║                                                              ║
    ║  [!] XÓA PurchaseOrders + PurchaseOrderItems                 ║
    ║      Lý do: Mô hình trung gian mua thẳng từ PO khách hàng  ║
    ║      Không cần tạo PO nội bộ → giảm nhập liệu              ║
    ║                                                              ║
    ║  [~] SalesOrder — gộp luồng mua hàng:                       ║
    ║      Thêm: VendorId, ExpectedReceiveDate, BuyingAt,         ║
    ║            ReceivedAt, PurchaseCost, IsDropship               ║
    ║      Status: DRAFT → WAIT → BUYING → RECEIVED               ║
    ║              → DELIVERING → DELIVERED → COMPLETED            ║
    ║      Computed: ProfitAmount = TotalAmount - PurchaseCost     ║
    ║                                                              ║
    ║  [~] SalesOrderItems — thêm PurchasePrice, LineCost          ║
    ║                                                              ║
    ║  [~] VendorInvoices — FK đổi từ PurchaseOrderId → SOId      ║
    ║  [~] VendorPayments — FK đổi từ PurchaseOrderId → SOId      ║
    ║  [~] StockTransactions — bỏ PurchaseOrderId, giữ SOId       ║
    ║  [~] PdfTemplates — bỏ PURCHASE_ORDER khỏi TemplateType     ║
    ║                                                              ║
    ╠══════════════════════════════════════════════════════════════╣
    ║             LUỒNG QUY TRÌNH (ĐƠN GIẢN HÓA)                  ║
    ╠══════════════════════════════════════════════════════════════╣
    ║                                                              ║
    ║  KH gửi yêu cầu                                             ║
    ║    → RFQ (INPROGRESS)                                        ║
    ║      → Quotation (DRAFT → SENT → APPROVED)                  ║
    ║        → SO (DRAFT)         NV nhập mã PO KH, tạm ứng      ║
    ║          → SO (WAIT)        Chờ phê duyệt tạm ứng          ║
    ║            → SO (BUYING)    Mua hàng từ NCC                 ║
    ║              → SO (RECEIVED)  Hàng về kho                   ║
    ║                → StockTx IN (DRAFT → CONFIRMED)             ║
    ║                  → SO (DELIVERING)  Giao vận chuyển         ║
    ║                    → StockTx OUT (DRAFT → DELIVERING)       ║
    ║                      → SO (DELIVERED)  KH nhận & ký         ║
    ║                        → StockTx OUT (DELIVERED)            ║
    ║                          → SO (COMPLETED)  Quyết toán      ║
    ║                            → RFQ (COMPLETED) ← auto        ║
    ║                                                              ║
    ╚══════════════════════════════════════════════════════════════╝
*/
