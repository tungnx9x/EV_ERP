-- ============================================================================
-- ERP DATABASE — MIGRATION: Thêm bước báo cáo kết quả (REPORTED)
-- Version:  1.7 → 1.8
-- Date:     2026-04-15
-- Mô tả:
--   [~] SalesOrders — thêm REPORTED vào Status, thêm ReportedAt
--   Luồng mới: ... → COMPLETED → REPORTED
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- 1. Bỏ CHECK constraint cũ
--ALTER TABLE SalesOrders DROP CONSTRAINT CK__SalesOrde__Statu__XXXXXXXX;
-- Lưu ý: tên constraint tự gen bởi SQL Server. Chạy query bên dưới để lấy tên chính xác:
-- SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('SalesOrders') AND definition LIKE '%DRAFT%';
--GO

-- Hoặc dùng cách an toàn hơn (tự tìm + drop):
DECLARE @ckName NVARCHAR(200);
SELECT @ckName = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('SalesOrders')
  AND definition LIKE '%BUYING%';

IF @ckName IS NOT NULL
    EXEC('ALTER TABLE SalesOrders DROP CONSTRAINT ' + @ckName);
GO

-- 2. Thêm CHECK constraint mới (có REPORTED)
ALTER TABLE SalesOrders ADD CONSTRAINT CK_SO_Status
    CHECK (Status IN ('DRAFT','WAIT','BUYING','RECEIVED','DELIVERING','DELIVERED','COMPLETED','REPORTED','CANCELLED'));
GO

-- 3. Thêm cột ReportedAt
ALTER TABLE SalesOrders ADD
    ReportedAt      DATETIME2       NULL;
GO

PRINT '═══ Migration REPORTED completed ═══';
PRINT 'SO status flow: DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED → REPORTED';
GO
