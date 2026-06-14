-- ============================================================================
-- ERP DATABASE — MIGRATION: Output invoice number on settlement
-- Version:  2.11 → 2.12
-- Date:     2026-06-14
-- Mô tả:
--   [+] Thêm cột SalesOrders.OutputInvoiceNo (NVARCHAR(50), NULL) — số hóa đơn
--       đầu ra do kế toán nhập khi duyệt quyết toán (DELIVERED/RETURNED → COMPLETED).
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SalesOrders') AND name = 'OutputInvoiceNo'
)
BEGIN
    ALTER TABLE dbo.SalesOrders ADD OutputInvoiceNo NVARCHAR(50) NULL;
    PRINT '  +ADD COLUMN SalesOrders.OutputInvoiceNo';
END
ELSE
    PRINT '  =SalesOrders.OutputInvoiceNo already exists';
GO

PRINT 'Migration v2.12 hoàn tất.';
GO
