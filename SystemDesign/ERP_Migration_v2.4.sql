-- ============================================================================
-- ERP DATABASE — MIGRATION: Quotation Item Purchase Calculator
-- Version:  2.3 → 2.4
-- Date:     2026-05-13
-- Mô tả:
--   [+] QuotationItems — popup máy tính Giá nhập:
--       - PurchaseMode (OFFICIAL/UNOFFICIAL)
--       - PurchaseQuantity, BasePrice (foreign-currency total)
--       - PurchaseTax, InspectionFee, BankingFee, OtherCosts (VND)
--       - OfficialShipping (VND) — mode chính thức
--       - UnofficialDomesticShipping (ngoại tệ), UnofficialWeightKg,
--         UnofficialCostPerKg, UnofficialHandCarryFee, UnofficialW2WShipping
--   Formula: Giá nhập = ((BasePrice × Rate) + Shipping + Tax + Inspection +
--                       Banking + Other) / PurchaseQuantity
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. QUOTATION ITEMS — thêm trường tính Giá nhập
-- ══════════════════════════════════════════════════════════════════

ALTER TABLE QuotationItems ADD
    PurchaseMode                   NVARCHAR(20)   NOT NULL CONSTRAINT DF_QItem_PurchaseMode DEFAULT('OFFICIAL'),
    PurchaseQuantity               DECIMAL(18,3)  NULL,
    BasePrice                      DECIMAL(18,2)  NULL,
    PurchaseTax                    DECIMAL(18,2)  NULL,
    InspectionFee                  DECIMAL(18,2)  NULL,
    BankingFee                     DECIMAL(18,2)  NULL,
    OtherCosts                     DECIMAL(18,2)  NULL,
    OfficialShipping               DECIMAL(18,2)  NULL,
    UnofficialDomesticShipping     DECIMAL(18,2)  NULL,
    UnofficialWeightKg             DECIMAL(18,3)  NULL,
    UnofficialCostPerKg            DECIMAL(18,2)  NULL,
    UnofficialHandCarryFee         DECIMAL(18,2)  NULL,
    UnofficialW2WShipping          DECIMAL(18,2)  NULL;
GO

-- Check constraint cho PurchaseMode
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QItem_PurchaseMode')
BEGIN
    ALTER TABLE QuotationItems
        ADD CONSTRAINT CK_QItem_PurchaseMode
            CHECK (PurchaseMode IN ('OFFICIAL', 'UNOFFICIAL'));
END
GO
