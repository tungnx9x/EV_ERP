-- ============================================================================
-- ERP DATABASE — MIGRATION: SourceUrl 1000, Currency cho giá nhập, Tỉ giá, RequiredImageUrl, Cities/Districts/Wards
-- Version:  2.0 → 2.1
-- Date:     2026-04-15
-- Mô tả:
--   [~] QuotationItems  — SourceUrl: 500 → 1000, +RequiredImageUrl, +PurchaseCurrency, +PurchaseExchangeRate
--   [~] SalesOrderItems — SourceUrl: 500 → 1000, +PurchaseCurrency, +PurchaseExchangeRate
--   [~] Products        — +SourceUrl(1000), +DefaultPurchaseCurrency
--   [~] SalesOrders     — +PurchaseCostCurrency
--   [+] Currencies      — danh mục đơn vị tiền tệ (VND, USD, JPY, CNY...)
--   [+] ExchangeRates   — tỉ giá hối đoái theo ngày (job tự update)
--   [+] Cities          — 63 tỉnh/thành VN
--   [+] Districts       — 709 quận/huyện
--   [+] Wards           — 11.162 phường/xã
--
-- Lưu ý: Sau khi chạy file này, chạy thêm 3 file seed:
--   - seed_cities.sql
--   - seed_districts.sql
--   - seed_wards.sql
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. NÂNG ĐỘ DÀI SourceUrl: 500 → 1000
-- ══════════════════════════════════════════════════════════════════

ALTER TABLE QuotationItems  ALTER COLUMN SourceUrl NVARCHAR(1000) NULL;
GO
ALTER TABLE SalesOrderItems ALTER COLUMN SourceUrl NVARCHAR(1000) NULL;
GO


-- ══════════════════════════════════════════════════════════════════
-- 2. BẢNG CURRENCIES (danh mục đơn vị tiền tệ)
-- ══════════════════════════════════════════════════════════════════

CREATE TABLE Currencies (
    CurrencyCode    NVARCHAR(3)     NOT NULL PRIMARY KEY,  -- VND, USD, JPY, CNY, EUR, KRW, THB, GBP...
    CurrencyName    NVARCHAR(100)   NOT NULL,              -- Việt Nam Đồng, US Dollar...
    Symbol          NVARCHAR(10)    NULL,                  -- ₫, $, ¥, ¥, €
    DecimalPlaces   TINYINT         NOT NULL DEFAULT 2,    -- VND=0, USD=2, JPY=0
    IsActive        BIT             NOT NULL DEFAULT 1,
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO

-- Seed các đơn vị tiền tệ thông dụng
INSERT INTO Currencies (CurrencyCode, CurrencyName, Symbol, DecimalPlaces, DisplayOrder) VALUES
('VND', N'Việt Nam Đồng', N'₫', 0, 1),
('USD', N'US Dollar',     N'$', 2, 2),
('EUR', N'Euro',          N'€', 2, 3),
('JPY', N'Japanese Yen',  N'¥', 0, 4),
('CNY', N'Chinese Yuan',  N'¥', 2, 5),
('KRW', N'Korean Won',    N'₩', 0, 6),
('THB', N'Thai Baht',     N'฿', 2, 7),
('GBP', N'British Pound', N'£', 2, 8),
('AUD', N'Australian Dollar', N'A$', 2, 9),
('SGD', N'Singapore Dollar', N'S$', 2, 10);
GO


-- ══════════════════════════════════════════════════════════════════
-- 3. BẢNG EXCHANGE RATES (tỉ giá theo ngày — job tự cập nhật)
-- ══════════════════════════════════════════════════════════════════

CREATE TABLE ExchangeRates (
    ExchangeRateId  INT IDENTITY(1,1) PRIMARY KEY,
    FromCurrency    NVARCHAR(3)     NOT NULL,              -- USD
    ToCurrency      NVARCHAR(3)     NOT NULL,              -- VND
    Rate            DECIMAL(18,6)   NOT NULL,              -- 1 USD = 25,400.500000 VND
    EffectiveDate   DATE            NOT NULL,              -- Ngày tỉ giá có hiệu lực
    Source          NVARCHAR(100)   NULL,                  -- Vietcombank, ExchangeRateAPI, OpenExchange...
    FetchedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ExchRate_From FOREIGN KEY (FromCurrency) REFERENCES Currencies(CurrencyCode),
    CONSTRAINT FK_ExchRate_To FOREIGN KEY (ToCurrency) REFERENCES Currencies(CurrencyCode),
    CONSTRAINT UQ_ExchRate UNIQUE (FromCurrency, ToCurrency, EffectiveDate)
);
GO

-- Index tra cứu nhanh tỉ giá hôm nay
CREATE INDEX IX_ExchRate_Date ON ExchangeRates(EffectiveDate DESC, FromCurrency, ToCurrency);
GO


-- ══════════════════════════════════════════════════════════════════
-- 4. THÊM CỘT CURRENCY CHO GIÁ NHẬP (PurchasePrice / PurchaseCost)
-- ══════════════════════════════════════════════════════════════════

-- QuotationItems — giá nhập có thể từ Shopee VN (VND), Amazon (USD), Taobao (CNY)...
ALTER TABLE QuotationItems ADD
    PurchaseCurrency      NVARCHAR(3)     NULL DEFAULT 'VND',
    PurchaseExchangeRate  DECIMAL(18,6)   NULL DEFAULT 1,  -- Tỉ giá quy đổi sang VND tại thời điểm báo giá
    RequiredImageUrl      NVARCHAR(1000)  NULL;            -- Ảnh hàng KH yêu cầu (so sánh với SP NV mua)
GO

ALTER TABLE QuotationItems ADD CONSTRAINT FK_QuotItem_Currency
    FOREIGN KEY (PurchaseCurrency) REFERENCES Currencies(CurrencyCode);
GO

-- SalesOrderItems — giá mua thực tế cũng có thể bằng ngoại tệ
ALTER TABLE SalesOrderItems ADD
    PurchaseCurrency      NVARCHAR(3)     NULL DEFAULT 'VND',
    PurchaseExchangeRate  DECIMAL(18,6)   NULL DEFAULT 1;  -- Tỉ giá quy đổi sang VND tại thời điểm mua
GO

ALTER TABLE SalesOrderItems ADD CONSTRAINT FK_SOItem_Currency
    FOREIGN KEY (PurchaseCurrency) REFERENCES Currencies(CurrencyCode);
GO

-- SalesOrders — tổng chi phí mua có thể quy về 1 tiền tệ chung
ALTER TABLE SalesOrders ADD
    PurchaseCostCurrency NVARCHAR(3)    NULL DEFAULT 'VND';
GO

ALTER TABLE SalesOrders ADD CONSTRAINT FK_SO_PurchaseCurrency
    FOREIGN KEY (PurchaseCostCurrency) REFERENCES Currencies(CurrencyCode);
GO

-- Products — giá mua mặc định cũng có currency
ALTER TABLE Products ADD
    DefaultPurchaseCurrency NVARCHAR(3) NULL DEFAULT 'VND',
    SourceUrl               NVARCHAR(1000) NULL;       -- Link nguồn mua mặc định
GO

ALTER TABLE Products ADD CONSTRAINT FK_Product_Currency
    FOREIGN KEY (DefaultPurchaseCurrency) REFERENCES Currencies(CurrencyCode);
GO


-- ══════════════════════════════════════════════════════════════════
-- 5. BẢNG CITIES (Tỉnh / Thành phố — 63 records)
-- ══════════════════════════════════════════════════════════════════

CREATE TABLE Cities (
    CityId          INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(5)     NOT NULL UNIQUE,        -- "01" Hà Nội, "79" HCM, "48" Đà Nẵng
    Name            NVARCHAR(50)    NOT NULL,               -- "Hà Nội"
    NameWithType    NVARCHAR(60)    NOT NULL,               -- "Thành phố Hà Nội"
    Slug            NVARCHAR(50)    NOT NULL,               -- "ha-noi"
    Type            NVARCHAR(15)    NOT NULL,               -- thanh-pho, tinh
    IsActive        BIT             NOT NULL DEFAULT 1
);
GO

CREATE INDEX IX_Cities_Slug ON Cities(Slug);
GO


-- ══════════════════════════════════════════════════════════════════
-- 6. BẢNG DISTRICTS (Quận / Huyện — 709 records)
-- ══════════════════════════════════════════════════════════════════

CREATE TABLE Districts (
    DistrictId      INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(5)     NOT NULL UNIQUE,        -- "001" Ba Đình, "883" Long Xuyên
    Name            NVARCHAR(60)    NOT NULL,               -- "Long Xuyên"
    NameWithType    NVARCHAR(80)    NOT NULL,               -- "Thành phố Long Xuyên"
    Slug            NVARCHAR(60)    NOT NULL,
    Type            NVARCHAR(15)    NOT NULL,               -- huyen, quan, thanh-pho, thi-xa
    CityCode        NVARCHAR(5)     NOT NULL,               -- FK → Cities.Code
    Path            NVARCHAR(80)    NULL,                   -- "Long Xuyên, An Giang"
    PathWithType    NVARCHAR(120)   NULL,                   -- "Thành phố Long Xuyên, Tỉnh An Giang"
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT FK_District_City FOREIGN KEY (CityCode) REFERENCES Cities(Code)
);
GO

CREATE INDEX IX_Districts_CityCode ON Districts(CityCode);
CREATE INDEX IX_Districts_Slug ON Districts(Slug);
GO


-- ══════════════════════════════════════════════════════════════════
-- 7. BẢNG WARDS (Phường / Xã — 11.162 records)
-- ══════════════════════════════════════════════════════════════════

CREATE TABLE Wards (
    WardId          INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(7)     NOT NULL UNIQUE,        -- "30280", "00004"
    Name            NVARCHAR(60)    NOT NULL,
    NameWithType    NVARCHAR(80)    NOT NULL,
    Slug            NVARCHAR(60)    NOT NULL,
    Type            NVARCHAR(15)    NOT NULL,               -- phuong, thi-tran, xa
    DistrictCode    NVARCHAR(5)     NOT NULL,               -- FK → Districts.Code
    Path            NVARCHAR(120)   NULL,                   -- "Mỹ Bình, Long Xuyên, An Giang"
    PathWithType    NVARCHAR(180)   NULL,                   -- "Phường Mỹ Bình, Thành phố..."
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT FK_Ward_District FOREIGN KEY (DistrictCode) REFERENCES Districts(Code)
);
GO

CREATE INDEX IX_Wards_DistrictCode ON Wards(DistrictCode);
CREATE INDEX IX_Wards_Slug ON Wards(Slug);
GO


-- ══════════════════════════════════════════════════════════════════
-- 8. (TÙY CHỌN) Liên kết Customers với Cities/Districts/Wards
-- ══════════════════════════════════════════════════════════════════
-- Customers hiện tại chỉ có City/District/Ward dạng NVARCHAR text.
-- Có thể cân nhắc thêm FK xuống bảng địa danh, nhưng để tránh phá vỡ
-- dữ liệu cũ, KHUYẾN NGHỊ giữ trường text + thêm CityCode/DistrictCode/WardCode
-- nullable để dropdown chọn từ master data.

ALTER TABLE Customers ADD
    CityCode        NVARCHAR(5)     NULL,
    DistrictCode    NVARCHAR(5)     NULL,
    WardCode        NVARCHAR(7)     NULL;
GO

ALTER TABLE Customers ADD CONSTRAINT FK_Customer_City
    FOREIGN KEY (CityCode) REFERENCES Cities(Code);
ALTER TABLE Customers ADD CONSTRAINT FK_Customer_District
    FOREIGN KEY (DistrictCode) REFERENCES Districts(Code);
ALTER TABLE Customers ADD CONSTRAINT FK_Customer_Ward
    FOREIGN KEY (WardCode) REFERENCES Wards(Code);
GO


PRINT '═══ Migration v2.1 — Schema completed ═══';
PRINT '';
PRINT 'CHẠY TIẾP các file seed theo thứ tự:';
PRINT '  1. seed_cities.sql    (63 records)';
PRINT '  2. seed_districts.sql (709 records)';
PRINT '  3. seed_wards.sql     (11.162 records)';
PRINT '';
PRINT 'Sau đó job .NET sẽ tự cập nhật ExchangeRates hằng ngày.';
GO
