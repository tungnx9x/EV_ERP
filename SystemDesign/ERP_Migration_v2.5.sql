-- ============================================================================
-- ERP DATABASE — MIGRATION: QuotationItem.RequiredDescription
-- Version:  2.4 → 2.5
-- Date:     2026-05-21
-- Mô tả:
--   [+] QuotationItems.RequiredDescription — mô tả yêu cầu của khách hàng
--       (hiển thị ngay sau ô ảnh KH yêu cầu trong form Báo giá)
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. QUOTATION ITEMS — thêm cột RequiredDescription
-- ══════════════════════════════════════════════════════════════════

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.QuotationItems')
      AND name = 'RequiredDescription'
)
BEGIN
    ALTER TABLE QuotationItems ADD RequiredDescription NVARCHAR(2000) NULL;
END
GO
