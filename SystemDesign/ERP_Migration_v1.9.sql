-- ============================================================================
-- ERP DATABASE — MIGRATION: UserName, Deadline, RETURNED
-- Version:  1.8 → 1.9
-- Date:     2026-04-15
-- Mô tả:
--   [~] Users       — thêm UserName (tên ngắn gọn dùng gen mã)
--   [~] RFQs        — thêm Deadline (bắt buộc)
--   [~] Quotations  — thêm Deadline (bắt buộc)
--   [~] SalesOrders — thêm RETURNED vào Status, thêm ReturnedAt, ReturnReason
--   Luồng: ... → DELIVERED → COMPLETED hoặc RETURNED → REPORTED
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════
-- 1. USERS — thêm UserName
-- ══════════════════════════════════════════════════════

ALTER TABLE Users ADD
    UserName        NVARCHAR(50)    NULL;           -- Tên ngắn gọn: "hoangnv", "linhtt"
GO

-- Tạo unique index (cho phép NULL nhưng không trùng khi có giá trị)
CREATE UNIQUE INDEX IX_Users_UserName ON Users(UserName) WHERE UserName IS NOT NULL;
GO

-- Backfill: tạm gán UserName = UserCode cho user hiện có (sửa lại sau)
UPDATE Users SET UserName = UserCode WHERE UserName IS NULL;
GO

-- Đổi thành NOT NULL sau khi backfill
ALTER TABLE Users ALTER COLUMN UserName NVARCHAR(50) NOT NULL;
GO

-- ══════════════════════════════════════════════════════
-- 2. RFQs — thêm Deadline (bắt buộc)
-- ══════════════════════════════════════════════════════

ALTER TABLE RFQs ADD
    Deadline        DATETIME2       NULL;           -- Thêm NULL trước
GO

-- Backfill: RFQ cũ chưa có deadline → gán = RequestDate + 7 ngày
UPDATE RFQs SET Deadline = DATEADD(DAY, 7, RequestDate) WHERE Deadline IS NULL;
GO

-- Đổi thành NOT NULL
ALTER TABLE RFQs ALTER COLUMN Deadline DATETIME2 NOT NULL;
GO

-- ══════════════════════════════════════════════════════
-- 3. QUOTATIONS — thêm Deadline (bắt buộc)
-- ══════════════════════════════════════════════════════

-- Quotations đã có ExpiryDate (DATE, nullable) nhưng đó là "hết hạn báo giá" cho KH.
-- Deadline mới = thời hạn nội bộ mà NV phải hoàn thành bước hiện tại.

ALTER TABLE Quotations ADD
    Deadline        DATETIME2       NULL;
GO

UPDATE Quotations SET Deadline = DATEADD(DAY, 3, QuotationDate) WHERE Deadline IS NULL;
GO

ALTER TABLE Quotations ALTER COLUMN Deadline DATETIME2 NOT NULL;
GO

-- ══════════════════════════════════════════════════════
-- 4. SALES ORDERS — thêm RETURNED + ReturnedAt + ReturnReason
-- ══════════════════════════════════════════════════════

-- Drop constraint cũ (tự tìm tên)
DECLARE @ckName NVARCHAR(200);
SELECT @ckName = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('SalesOrders')
  AND definition LIKE '%BUYING%';

IF @ckName IS NOT NULL
    EXEC('ALTER TABLE SalesOrders DROP CONSTRAINT ' + @ckName);
GO

-- Thêm constraint mới có RETURNED
ALTER TABLE SalesOrders ADD CONSTRAINT CK_SO_Status
    CHECK (Status IN ('DRAFT','WAIT','BUYING','RECEIVED','DELIVERING','DELIVERED','COMPLETED','RETURNED','REPORTED','CANCELLED'));
GO

-- Thêm cột trả hàng
ALTER TABLE SalesOrders ADD
    ReturnedAt      DATETIME2       NULL,
    ReturnReason    NVARCHAR(500)   NULL;
GO


PRINT '═══ Migration v1.9 completed ═══';
PRINT 'Users: +UserName (NOT NULL, unique)';
PRINT 'RFQs: +Deadline (NOT NULL)';
PRINT 'Quotations: +Deadline (NOT NULL)';
PRINT 'SO status: +RETURNED, +ReturnedAt, +ReturnReason';
PRINT 'Flow: DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED|RETURNED → REPORTED';
GO
