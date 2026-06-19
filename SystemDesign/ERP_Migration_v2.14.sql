-- ============================================================================
-- ERP DATABASE — MIGRATION: Nới rộng cột AdvanceRequests.RequestNo
-- Version:  2.13 → 2.14
-- Date:     2026-06-19
-- Mô tả:
--   [~] RequestNo NVARCHAR(20) → NVARCHAR(50).
--       Số phiếu tạm ứng có dạng TU-{yyyyMM}-{UserName/UserCode}-{SEQ}
--       (vd: TU-202606-NV-0001-001). Khi UserName/UserCode dài, chuỗi vượt
--       quá 20 ký tự gây lỗi "String or binary data would be truncated".
--   Cột có ràng buộc UNIQUE (đặt tên hệ thống) nên phải DROP trước khi
--   ALTER COLUMN, sau đó tạo lại UNIQUE.
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

-- 1) Drop ràng buộc/chỉ mục UNIQUE đang nằm trên cột RequestNo (tên do hệ thống đặt).
DECLARE @uq sysname;
SELECT @uq = i.name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('dbo.AdvanceRequests')
  AND c.name = 'RequestNo'
  AND i.is_unique = 1;

IF @uq IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID('dbo.AdvanceRequests') AND name = @uq)
        EXEC('ALTER TABLE dbo.AdvanceRequests DROP CONSTRAINT [' + @uq + ']');
    ELSE
        EXEC('DROP INDEX [' + @uq + '] ON dbo.AdvanceRequests');
    PRINT '  -DROP UNIQUE (' + @uq + ') trên AdvanceRequests.RequestNo';
END
ELSE
    PRINT '  =Không tìm thấy UNIQUE trên AdvanceRequests.RequestNo';
GO

-- 2) Nới rộng cột.
ALTER TABLE dbo.AdvanceRequests ALTER COLUMN RequestNo NVARCHAR(50) NOT NULL;
PRINT '  ~ALTER COLUMN AdvanceRequests.RequestNo -> NVARCHAR(50)';
GO

-- 3) Tạo lại ràng buộc UNIQUE.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.AdvanceRequests')
                 AND name = 'UQ_AdvanceRequests_RequestNo')
BEGIN
    ALTER TABLE dbo.AdvanceRequests
        ADD CONSTRAINT UQ_AdvanceRequests_RequestNo UNIQUE (RequestNo);
    PRINT '  +ADD UNIQUE UQ_AdvanceRequests_RequestNo';
END
ELSE
    PRINT '  =UQ_AdvanceRequests_RequestNo already exists';
GO

PRINT 'Migration v2.14 hoàn tất: AdvanceRequests.RequestNo nới rộng -> NVARCHAR(50).';
GO
