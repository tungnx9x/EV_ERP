-- ============================================================================
-- ERP DATABASE — MIGRATION: Cho phép hủy báo giá (Quotations.Status = CANCELLED)
-- Version:  2.15 → 2.16
-- Date:     2026-10-24
-- Mô tả:
--   [~] Quotations — CHECK constraint của Status bổ sung 'CANCELLED'.
--       Code (QuotationService.CancelAsync) đã ghi Status = 'CANCELLED' nhưng
--       constraint gốc (v1.6) chỉ cho phép DRAFT/SENT/APPROVED/REJECTED/AMEND/EXPIRED
--       → nút "Hủy báo giá" báo lỗi "An error occurred while saving the entity changes".
--   Constraint gốc do SQL Server tự đặt tên → tìm động rồi DROP, tạo lại với tên cố định.
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

-- 1) Drop CHECK constraint cũ trên Quotations.Status (tên do hệ thống đặt)
DECLARE @ckName sysname;
SELECT @ckName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.Quotations')
  AND c.name = 'Status';

IF @ckName IS NOT NULL
    EXEC('ALTER TABLE dbo.Quotations DROP CONSTRAINT [' + @ckName + ']');
GO

-- 2) Tạo lại constraint có CANCELLED
ALTER TABLE dbo.Quotations ADD CONSTRAINT CK_Quotation_Status
    CHECK (Status IN ('DRAFT','SENT','APPROVED','REJECTED','AMEND','EXPIRED','CANCELLED'));
GO

PRINT '═══ Migration v2.16 completed: Quotations.Status += CANCELLED ═══';
