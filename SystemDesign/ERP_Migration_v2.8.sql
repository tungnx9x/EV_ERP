-- ============================================================================
-- ERP DATABASE — MIGRATION: Customer PO received date on SalesOrders
-- Version:  2.7 → 2.8
-- Date:     2026-06-01
-- Mô tả:
--   [+] Thêm cột CustomerPoDate (DATETIME, NULL) vào bảng SalesOrders để lưu
--       ngày nhận PO của khách hàng. Hiển thị / nhập tại màn hình SO Detail,
--       mục "PO Khách hàng".
-- ============================================================================

-- DB thực tế app đang dùng (theo appsettings.json: DefaultConnection).
USE ERP_ThuongMaiTrungGian_1_6;
GO

-- ══════════════════════════════════════════════════════════════════
-- ADD column CustomerPoDate
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SalesOrders') AND name = 'CustomerPoDate'
)
BEGIN
    ALTER TABLE dbo.SalesOrders ADD CustomerPoDate DATETIME NULL;
    PRINT '  +ADD COLUMN dbo.SalesOrders.CustomerPoDate';
END
GO
