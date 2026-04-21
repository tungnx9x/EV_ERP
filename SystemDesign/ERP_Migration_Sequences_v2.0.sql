-- ============================================================================
-- ERP DATABASE — MIGRATION: Sequences for code generation
-- Version:  1.9 → 2.0
-- Date:     2026-04-21
-- Mô tả:
--   [+] RfqSequence         — Sequence cho mã RFQ (RFQ-yyyyMMdd-NNN)
--   [+] QuotationSequence   — Sequence cho mã Báo giá (BG-yyyyMMdd-NNN)
--   [+] SalesOrderSequence  — Sequence cho mã SO (SO-yyyyMMdd-NNN)
--   Thay thế logic Query + OrderByDescending bằng NEXT VALUE FOR
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════
-- 1. RfqSequence
-- ══════════════════════════════════════════════════════

-- Tìm giá trị lớn nhất hiện tại để seed sequence
DECLARE @rfqMax INT = 0;
SELECT @rfqMax = ISNULL(MAX(
    TRY_CAST(RIGHT(RfqNo, CHARINDEX('-', REVERSE(RfqNo)) - 1) AS INT)
), 0) FROM RFQs;

-- Tạo sequence bắt đầu từ giá trị tiếp theo
DECLARE @rfqSql NVARCHAR(200) = N'CREATE SEQUENCE RfqSequence AS INT START WITH '
    + CAST(@rfqMax + 1 AS NVARCHAR) + N' INCREMENT BY 1 NO CACHE';
EXEC sp_executesql @rfqSql;
GO

-- ══════════════════════════════════════════════════════
-- 2. QuotationSequence
-- ══════════════════════════════════════════════════════

DECLARE @bgMax INT = 0;
SELECT @bgMax = ISNULL(MAX(
    TRY_CAST(RIGHT(QuotationNo, CHARINDEX('-', REVERSE(QuotationNo)) - 1) AS INT)
), 0) FROM Quotations;

DECLARE @bgSql NVARCHAR(200) = N'CREATE SEQUENCE QuotationSequence AS INT START WITH '
    + CAST(@bgMax + 1 AS NVARCHAR) + N' INCREMENT BY 1 NO CACHE';
EXEC sp_executesql @bgSql;
GO

-- ══════════════════════════════════════════════════════
-- 3. SalesOrderSequence
-- ══════════════════════════════════════════════════════

DECLARE @soMax INT = 0;
SELECT @soMax = ISNULL(MAX(
    TRY_CAST(RIGHT(SalesOrderNo, CHARINDEX('-', REVERSE(SalesOrderNo)) - 1) AS INT)
), 0) FROM SalesOrders;

DECLARE @soSql NVARCHAR(200) = N'CREATE SEQUENCE SalesOrderSequence AS INT START WITH '
    + CAST(@soMax + 1 AS NVARCHAR) + N' INCREMENT BY 1 NO CACHE';
EXEC sp_executesql @soSql;
GO


PRINT '═══ Migration v2.0 completed ═══';
PRINT 'Sequences created: RfqSequence, QuotationSequence, SalesOrderSequence';
PRINT 'Seeded from max existing code numbers';
GO
