-- ============================================================================
-- ERP DATABASE — MIGRATION: VAT (%) cho chế độ "Mua trong nước" (DOMESTIC)
-- Version:  2.12 → 2.13
-- Date:     2026-06-16
-- Mô tả:
--   [+] Thêm cột PurchaseVatPercent DECIMAL(9,4) NULL vào QuotationItems —
--       thuế VAT (%) nhập trong popup tính Giá nhập cho chế độ "Mua trong nước".
--   [+] Thêm cột tương ứng vào SalesOrderItems — khi tạo SO từ báo giá, giá trị
--       VAT được copy sang; popup tính Giá nhập tại SO Detail cũng dùng cột này.
--   Công thức (chế độ DOMESTIC, đơn vị VND, rate = 1):
--       Giá nhập = Đơn giá gốc × (1 + PurchaseVatPercent / 100)
--                + (Phí vận chuyển / Số lượng mua)
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuotationItems') AND name = 'PurchaseVatPercent')
BEGIN
    ALTER TABLE dbo.QuotationItems ADD PurchaseVatPercent DECIMAL(9,4) NULL;
    PRINT '  +ADD COLUMN QuotationItems.PurchaseVatPercent';
END
ELSE
    PRINT '  =QuotationItems.PurchaseVatPercent already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'PurchaseVatPercent')
BEGIN
    ALTER TABLE dbo.SalesOrderItems ADD PurchaseVatPercent DECIMAL(9,4) NULL;
    PRINT '  +ADD COLUMN SalesOrderItems.PurchaseVatPercent';
END
ELSE
    PRINT '  =SalesOrderItems.PurchaseVatPercent already exists';
GO

PRINT 'Migration v2.13 hoàn tất: PurchaseVatPercent thêm vào QuotationItems + SalesOrderItems.';
GO
