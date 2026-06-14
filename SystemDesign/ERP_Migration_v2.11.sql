-- ============================================================================
-- ERP DATABASE — MIGRATION: Advance "self-paid" status
-- Version:  2.10 → 2.11
-- Date:     2026-06-14
-- Mô tả:
--   [~] Thêm mã trạng thái 'SELF_PAID' (người tạo tự thanh toán — nằm NGOÀI
--       quy trình duyệt: không lên workspace kế toán/giám đốc, nhưng vẫn hiển
--       thị trong bảng tạm ứng và được tính là "đã chi/đã cấp").
--   [~] Nới CHECK constraint CK_AdvanceRequests_Status để chấp nhận mã mới.
-- ============================================================================

-- DB thực tế app đang dùng (theo appsettings.json: DefaultConnection).
USE ERP_ThuongMaiTrungGian_1_6;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. DROP CHECK constraint hiện tại trên AdvanceRequests.Status
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
-- 2. ADD CHECK constraint mới (bổ sung 'SELF_PAID')
-- ══════════════════════════════════════════════════════════════════
ALTER TABLE dbo.AdvanceRequests WITH NOCHECK ADD CONSTRAINT CK_AdvanceRequests_Status
    CHECK (Status IN (
        -- ── Quy trình duyệt (4 bước) ──
        'WAIT_ACCOUNTANT','WAIT_DIRECTOR','WAIT_DISBURSE','DISBURSED','REJECTED',
        -- ── Tự thanh toán (ngoài quy trình duyệt) ──
        'SELF_PAID',
        -- ── Quyết toán ──
        'SETTLING','SETTLED',
        -- ── Mã cũ (dữ liệu hiện có — không migrate) ──
        'PENDING','APPROVED','RECEIVED'
    ));
PRINT '  +ADD CHECK CK_AdvanceRequests_Status (+SELF_PAID)';
GO

PRINT 'Migration v2.11 hoàn tất: AdvanceRequests.Status đã chấp nhận SELF_PAID.';
GO
