-- ============================================================================
-- ERP DATABASE — MIGRATION: Domestic purchase mode for Quotation items
-- Version:  2.6 → 2.7
-- Date:     2026-05-31
-- Mô tả:
--   [~] Nới CHECK constraint CK_QItem_PurchaseMode để chấp nhận 'DOMESTIC'
--       (Mua trong nước) bên cạnh 'OFFICIAL' / 'UNOFFICIAL'.
--   Công thức Mua trong nước: Giá nhập = Đơn giá gốc + (Phí vận chuyển / SL mua)
--   - Tiền tệ luôn là VND (rate = 1).
--   - Phí vận chuyển lưu chung cột OfficialShipping (cùng ngữ nghĩa 1 dòng phí).
-- ============================================================================

-- DB thực tế app đang dùng (theo appsettings.json: DefaultConnection).
USE ERP_ThuongMaiTrungGian_1_6;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. DROP CHECK constraint cũ (OFFICIAL/UNOFFICIAL)
-- ══════════════════════════════════════════════════════════════════
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QItem_PurchaseMode')
BEGIN
    ALTER TABLE dbo.QuotationItems DROP CONSTRAINT CK_QItem_PurchaseMode;
    PRINT '  -DROP CHECK CK_QItem_PurchaseMode';
END
GO

-- ══════════════════════════════════════════════════════════════════
-- 2. ADD CHECK constraint mới (thêm DOMESTIC)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QItem_PurchaseMode')
BEGIN
    ALTER TABLE dbo.QuotationItems
        ADD CONSTRAINT CK_QItem_PurchaseMode
            CHECK (PurchaseMode IN ('OFFICIAL', 'UNOFFICIAL', 'DOMESTIC'));
    PRINT '  +ADD CHECK CK_QItem_PurchaseMode (OFFICIAL/UNOFFICIAL/DOMESTIC)';
END
GO
