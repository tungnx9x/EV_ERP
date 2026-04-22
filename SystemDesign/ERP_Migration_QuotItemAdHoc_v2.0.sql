-- ============================================================================
-- ERP DATABASE — MIGRATION: QuotationItems cho sản phẩm chưa có trong hệ thống
-- Version:  1.9 → 2.0
-- Date:     2026-04-15
-- Mô tả:
--   [~] QuotationItems — ProductId nullable, thêm các trường nhập tay
--   [~] SalesOrderItems — ProductId nullable tương tự (copy từ QuotationItems)
--   Logic:
--     ProductId có giá trị  → SP đã có trong hệ thống (chọn từ danh mục)
--     ProductId = NULL       → SP mới, NV nhập tay thông tin, chưa tạo Product
--     Khi KH gửi PO xác nhận → NV nhập bù Product → cập nhật ProductId
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════
-- 1. QUOTATION ITEMS — ProductId nullable
-- ══════════════════════════════════════════════════════

-- Drop FK cũ
DECLARE @fk1 NVARCHAR(200);
SELECT @fk1 = name FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('QuotationItems')
  AND referenced_object_id = OBJECT_ID('Products');
IF @fk1 IS NOT NULL EXEC('ALTER TABLE QuotationItems DROP CONSTRAINT ' + @fk1);
GO

-- Cho phép NULL
ALTER TABLE QuotationItems ALTER COLUMN ProductId INT NULL;
GO

-- Tạo lại FK (cho phép NULL)
ALTER TABLE QuotationItems ADD CONSTRAINT FK_QuotItem_Product
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId);
GO

-- Thêm các trường mới
ALTER TABLE QuotationItems ADD
    ProductDescription NVARCHAR(MAX) NULL,             -- Mô tả sản phẩm (nhập tay)
    ImageUrl        NVARCHAR(500)   NULL,              -- Hình ảnh sản phẩm
    PurchasePrice   DECIMAL(18,2)   NULL,              -- Giá nhập (giá vốn dự kiến)
    ShippingFee     DECIMAL(18,2)   NULL DEFAULT 0,    -- Phí vận chuyển
    Coefficient     DECIMAL(10,4)   NULL DEFAULT 1,    -- Hệ số (markup / quy đổi)
    TaxRate         DECIMAL(5,2)    NULL DEFAULT 10,   -- % VAT dòng
    TaxAmount       DECIMAL(18,2)   NULL DEFAULT 0,    -- Tiền VAT dòng
    LineTotalWithTax DECIMAL(18,2)  NULL DEFAULT 0,    -- Thành tiền có VAT
    IsProductMapped BIT             NOT NULL DEFAULT 0; -- 0 = chưa gắn SP, 1 = đã gắn
GO

-- Backfill: dòng cũ đã có ProductId → mapped
UPDATE QuotationItems SET IsProductMapped = 1 WHERE ProductId IS NOT NULL;
GO

-- Index: tìm dòng chưa gắn SP
CREATE INDEX IX_QuotItems_Unmapped ON QuotationItems(QuotationId, IsProductMapped)
    WHERE IsProductMapped = 0;
GO


-- ══════════════════════════════════════════════════════
-- 2. SALES ORDER ITEMS — ProductId nullable tương tự
-- ══════════════════════════════════════════════════════
-- SOItems copy từ QuotationItems khi tạo SO, nên cũng cần nullable.
-- NV bắt buộc phải nhập bù Product trước khi SO chuyển sang RECEIVED.

DECLARE @fk2 NVARCHAR(200);
SELECT @fk2 = name FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('SalesOrderItems')
  AND referenced_object_id = OBJECT_ID('Products');
IF @fk2 IS NOT NULL EXEC('ALTER TABLE SalesOrderItems DROP CONSTRAINT ' + @fk2);
GO

ALTER TABLE SalesOrderItems ALTER COLUMN ProductId INT NULL;
GO

ALTER TABLE SalesOrderItems ADD CONSTRAINT FK_SOItem_Product
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId);
GO

-- Thêm các trường tương tự (copy từ QuotationItems khi tạo SO)
ALTER TABLE SalesOrderItems ADD
    ProductDescription NVARCHAR(MAX) NULL,
    ImageUrl        NVARCHAR(500)   NULL,
    ShippingFee     DECIMAL(18,2)   NULL DEFAULT 0,
    Coefficient     DECIMAL(10,4)   NULL DEFAULT 1,
    TaxRate         DECIMAL(5,2)    NULL DEFAULT 10,
    TaxAmount       DECIMAL(18,2)   NULL DEFAULT 0,
    LineTotalWithTax DECIMAL(18,2)  NULL DEFAULT 0,
    IsProductMapped BIT             NOT NULL DEFAULT 0;
GO

UPDATE SalesOrderItems SET IsProductMapped = 1 WHERE ProductId IS NOT NULL;
GO

CREATE INDEX IX_SOItems_Unmapped ON SalesOrderItems(SalesOrderId, IsProductMapped)
    WHERE IsProductMapped = 0;
GO


PRINT '═══ Migration v2.0 completed ═══';
PRINT '';
PRINT 'QuotationItems + SalesOrderItems:';
PRINT '  ProductId → NULLABLE';
PRINT '  +ProductDescription, +ImageUrl, +PurchasePrice, +ShippingFee';
PRINT '  +Coefficient, +TaxRate, +TaxAmount, +LineTotalWithTax, +IsProductMapped';
PRINT '';
PRINT 'Luồng:';
PRINT '  Tạo báo giá → SP có sẵn: chọn từ danh mục (ProductId NOT NULL)';
PRINT '               → SP mới: nhập tay (ProductId NULL, IsProductMapped=0)';
PRINT '  KH duyệt → Tạo SO (copy dòng, giữ nguyên ProductId NULL/NOT NULL)';
PRINT '  NV nhập bù SP → UPDATE ProductId + IsProductMapped=1';
PRINT '  Chặn SO → RECEIVED nếu còn dòng IsProductMapped=0';
GO
