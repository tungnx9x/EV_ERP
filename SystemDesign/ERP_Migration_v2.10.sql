-- ============================================================================
-- ERP DATABASE — MIGRATION: Volume (CBM) cost on QuotationItems
-- Version:  2.9 → 2.10
-- Date:     2026-06-14
-- Mô tả:
--   [+] Thêm 4 cột thể tích (CBM) vào QuotationItems cho chế độ "Nhập tiểu ngạch"
--       (UNOFFICIAL), song song với UnofficialWeightKg × UnofficialCostPerKg:
--         - UnofficialLength, UnofficialWidth, UnofficialHeight  (đơn vị: mét)
--         - UnofficialCostPerCbm                                  (VND / m³)
--   [+] Thêm 4 cột tương ứng vào SalesOrderItems — khi tạo SO từ báo giá, giá trị
--       CBM được copy sang; popup tính giá nhập tại SO Detail cũng dùng các cột này.
--   Công thức (đơn vị mét, không chia):
--       CBM       = UnofficialLength × UnofficialWidth × UnofficialHeight   (m³)
--       Chi phí   = CBM × UnofficialCostPerCbm   → cộng vào phí vận chuyển tiểu ngạch
--       Giá nhập  = (BasePrice × Rate)
--                 + (Nội địa + Kg×Phí/kg + CBM×Phí/CBM + Xách tay + Kho→Kho
--                    + Thuế + Kiểm định + Ngân hàng + Khác) / PurchaseQuantity
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

-- Helper: add column only if it does not already exist.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuotationItems') AND name = 'UnofficialLength')
    ALTER TABLE dbo.QuotationItems ADD UnofficialLength DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuotationItems') AND name = 'UnofficialWidth')
    ALTER TABLE dbo.QuotationItems ADD UnofficialWidth DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuotationItems') AND name = 'UnofficialHeight')
    ALTER TABLE dbo.QuotationItems ADD UnofficialHeight DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuotationItems') AND name = 'UnofficialCostPerCbm')
    ALTER TABLE dbo.QuotationItems ADD UnofficialCostPerCbm DECIMAL(18,2) NULL;
GO

-- ── SalesOrderItems: mirror the CBM columns (copied from QuotationItems on SO creation) ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialLength')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialLength DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialWidth')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialWidth DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialHeight')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialHeight DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialCostPerCbm')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialCostPerCbm DECIMAL(18,2) NULL;
GO

PRINT 'Migration v2.10 applied: QuotationItems + SalesOrderItems CBM (volume) cost columns added.';
GO
