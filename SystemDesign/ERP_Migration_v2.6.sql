-- ============================================================================
-- ERP DATABASE — MIGRATION: Advance approval 4-step workflow
-- Version:  2.5 → 2.6
-- Date:     2026-05-30
-- Mô tả:
--   Quy trình duyệt tạm ứng mới (4 bước):
--     WAIT_ACCOUNTANT → WAIT_DIRECTOR → WAIT_DISBURSE → DISBURSED
--   [~] Nới CHECK constraint trên AdvanceRequests.Status để chấp nhận các mã
--       trạng thái mới (vẫn giữ các mã cũ để dữ liệu hiện có không bị lỗi —
--       KHÔNG migrate dữ liệu cũ).
--   [~] Đổi DEFAULT của AdvanceRequests.Status → 'WAIT_ACCOUNTANT'.
-- ============================================================================

-- DB thực tế app đang dùng (theo appsettings.json: DefaultConnection).
USE ERP_ThuongMaiTrungGian_1_6;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. DROP CHECK constraint cũ trên AdvanceRequests.Status (tên do hệ thống đặt)
-- ══════════════════════════════════════════════════════════════════
DECLARE @ckName SYSNAME;

SELECT @ckName = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.AdvanceRequests')
  AND cc.definition LIKE '%[[]Status]%';   -- constraint tham chiếu cột Status

IF @ckName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE dbo.AdvanceRequests DROP CONSTRAINT [' + @ckName + ']');
    PRINT '  -DROP CHECK ' + @ckName;
END
GO

-- ══════════════════════════════════════════════════════════════════
-- 2. ADD CHECK constraint mới (mã mới + mã cũ để tương thích)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.AdvanceRequests')
      AND name = 'CK_AdvanceRequests_Status'
)
BEGIN
    ALTER TABLE dbo.AdvanceRequests WITH NOCHECK ADD CONSTRAINT CK_AdvanceRequests_Status
        CHECK (Status IN (
            -- ── Quy trình mới (4 bước) ──
            'WAIT_ACCOUNTANT','WAIT_DIRECTOR','WAIT_DISBURSE','DISBURSED','REJECTED',
            -- ── Quyết toán ──
            'SETTLING','SETTLED',
            -- ── Mã cũ (dữ liệu hiện có — không migrate) ──
            'PENDING','APPROVED','RECEIVED'
        ));
    PRINT '  +ADD CHECK CK_AdvanceRequests_Status';
END
GO

-- ══════════════════════════════════════════════════════════════════
-- 3. Đổi DEFAULT của Status → 'WAIT_ACCOUNTANT'
-- ══════════════════════════════════════════════════════════════════
DECLARE @dfName SYSNAME;

SELECT @dfName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.AdvanceRequests')
  AND c.name = 'Status';

IF @dfName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE dbo.AdvanceRequests DROP CONSTRAINT [' + @dfName + ']');
    PRINT '  -DROP DEFAULT ' + @dfName;
END

ALTER TABLE dbo.AdvanceRequests
    ADD CONSTRAINT DF_AdvanceRequests_Status DEFAULT 'WAIT_ACCOUNTANT' FOR Status;
PRINT '  +ADD DEFAULT DF_AdvanceRequests_Status (WAIT_ACCOUNTANT)';
GO

PRINT 'Migration v2.6 hoàn tất: AdvanceRequests.Status đã chấp nhận quy trình 4 bước.';
GO
