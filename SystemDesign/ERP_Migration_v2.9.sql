-- ============================================================================
-- ERP DATABASE — MIGRATION: Current purchase-price breakdown on SalesOrderItems
-- Version:  2.8 → 2.9
-- Date:     2026-06-01
-- Mô tả:
--   [+] Thêm 13 cột vào SalesOrderItems để lưu "Giá nhập hiện tại" + breakdown
--       (giống QuotationItems). Cho phép nhập lại giá nhập tại màn hình SO Detail
--       (cột "Giá hiện tại") khi giá mua thực tế thay đổi so với lúc báo giá.
--       Các cột này được copy từ QuotationItems khi tạo SO từ báo giá.
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

-- Helper: add column only if it does not already exist.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'PurchaseMode')
    ALTER TABLE dbo.SalesOrderItems ADD PurchaseMode VARCHAR(20) NOT NULL CONSTRAINT DF_SOItem_PurchaseMode DEFAULT('OFFICIAL');
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'PurchaseQuantity')
    ALTER TABLE dbo.SalesOrderItems ADD PurchaseQuantity DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'BasePrice')
    ALTER TABLE dbo.SalesOrderItems ADD BasePrice DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'PurchaseTax')
    ALTER TABLE dbo.SalesOrderItems ADD PurchaseTax DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'InspectionFee')
    ALTER TABLE dbo.SalesOrderItems ADD InspectionFee DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'BankingFee')
    ALTER TABLE dbo.SalesOrderItems ADD BankingFee DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'OtherCosts')
    ALTER TABLE dbo.SalesOrderItems ADD OtherCosts DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'OfficialShipping')
    ALTER TABLE dbo.SalesOrderItems ADD OfficialShipping DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialDomesticShipping')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialDomesticShipping DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialWeightKg')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialWeightKg DECIMAL(18,3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialCostPerKg')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialCostPerKg DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialHandCarryFee')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialHandCarryFee DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalesOrderItems') AND name = 'UnofficialW2WShipping')
    ALTER TABLE dbo.SalesOrderItems ADD UnofficialW2WShipping DECIMAL(18,2) NULL;
GO

PRINT 'Migration v2.9 applied: SalesOrderItems purchase-price breakdown columns added.';
GO
