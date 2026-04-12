-- ============================================================================
-- ERP DATABASE — MIGRATION: Task Comments (Trao đổi trong quy trình)
-- Version:  1.4 → 1.5
-- Date:     2026-04-11
-- Mô tả:
--   [+] TaskComments — Bảng comment/note dùng chung cho RFQ, Quotation, SO
--       Hỗ trợ: mention (@user), reply thread, soft delete, edit history
--       Thông báo: tự động gửi Notification + SignalR push khi có comment mới
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ============================================================================
-- BẢNG: TASK COMMENTS (Trao đổi trong task — polymorphic)
-- ============================================================================
-- Thiết kế giống Attachments: 1 bảng dùng chung cho mọi entity.
-- Mỗi comment = 1 dòng → tạo thành timeline trao đổi trong task.
--
-- Ví dụ dữ liệu:
-- ┌────┬──────────────┬────────┬────────┬────────────────────────────────────┬──────────────┐
-- │ Id │ EntityType   │ Entity │ Parent │ Content                            │ MentionedIds │
-- │    │              │ Id     │ Id     │                                    │              │
-- ├────┼──────────────┼────────┼────────┼────────────────────────────────────┼──────────────┤
-- │ 1  │ SALES_ORDER  │ 3      │ NULL   │ @NV KH yêu cầu giao trước thứ 6  │ [5]          │
-- │ 2  │ SALES_ORDER  │ 3      │ 1      │ Em đã liên hệ NCC, hàng về mai   │ []           │
-- │ 3  │ SALES_ORDER  │ 3      │ 1      │ OK. @ThủKho chuẩn bị vị trí kho  │ [8]          │
-- │ 4  │ QUOTATION    │ 12     │ NULL   │ Giá cần điều chỉnh theo bảng mới │ [5]          │
-- └────┴──────────────┴────────┴────────┴────────────────────────────────────┴──────────────┘

CREATE TABLE TaskComments (
    CommentId       BIGINT IDENTITY(1,1) PRIMARY KEY,

    -- ── Gắn vào đối tượng nào ──
    EntityType      NVARCHAR(20)    NOT NULL,              -- RFQ, QUOTATION, SALES_ORDER
                    CHECK (EntityType IN ('RFQ','QUOTATION','SALES_ORDER')),
    EntityId        INT             NOT NULL,              -- RfqId / QuotationId / SalesOrderId

    -- ── Reply thread ──
    ParentCommentId BIGINT          NULL,                  -- Reply comment nào (NULL = comment gốc)

    -- ── Nội dung ──
    Content         NVARCHAR(MAX)   NOT NULL,              -- Nội dung (hỗ trợ markdown cơ bản)
    MentionedUserIds NVARCHAR(500)  NULL,                  -- JSON array: [1, 5, 12]

    -- ── Phân loại ──
    IsInternal      BIT             NOT NULL DEFAULT 0,    -- 1 = ghi chú nội bộ (NV thấy), 0 = chung

    -- ── Chỉnh sửa ──
    IsEdited        BIT             NOT NULL DEFAULT 0,
    EditedAt        DATETIME2       NULL,

    -- ── Soft delete ──
    IsActive        BIT             NOT NULL DEFAULT 1,

    -- ── Metadata ──
    CreatedBy       INT             NOT NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_TComment_Parent FOREIGN KEY (ParentCommentId) REFERENCES TaskComments(CommentId),
    CONSTRAINT FK_TComment_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
GO


-- ============================================================================
-- INDEXES
-- ============================================================================

-- Query chính: lấy tất cả comment của 1 task, sắp theo thời gian
CREATE INDEX IX_TComment_Entity ON TaskComments(EntityType, EntityId, CreatedAt)
    WHERE IsActive = 1;

-- Reply thread: tìm comment con của 1 comment cha
CREATE INDEX IX_TComment_Parent ON TaskComments(ParentCommentId)
    WHERE ParentCommentId IS NOT NULL;

-- Tìm comment của 1 user (lịch sử comment)
CREATE INDEX IX_TComment_User ON TaskComments(CreatedBy, CreatedAt DESC);
GO


-- ============================================================================
-- VIEW: Đếm comment chưa đọc theo entity (hiện badge trên card workspace)
-- ============================================================================

-- Dùng kết hợp với Notifications:
-- Khi user mở tab comment của SO-003 → mark tất cả Notification
-- có ReferenceType='TASK_COMMENT' AND ReferenceId=CommentId là đã đọc.
-- View này đếm comment mới (sau lần đọc cuối) cho mỗi entity.

CREATE OR ALTER VIEW vw_TaskCommentCounts AS
SELECT
    tc.EntityType,
    tc.EntityId,
    COUNT(*)                AS TotalComments,
    MAX(tc.CreatedAt)       AS LastCommentAt,
    -- Lấy mã đơn để hiện trên UI
    CASE tc.EntityType
        WHEN 'RFQ'          THEN r.RfqNo
        WHEN 'QUOTATION'    THEN q.QuotationNo
        WHEN 'SALES_ORDER'  THEN s.SalesOrderNo
    END AS EntityNo
FROM TaskComments tc
LEFT JOIN RFQs r ON tc.EntityType = 'RFQ' AND tc.EntityId = r.RfqId
LEFT JOIN Quotations q ON tc.EntityType = 'QUOTATION' AND tc.EntityId = q.QuotationId
LEFT JOIN SalesOrders s ON tc.EntityType = 'SALES_ORDER' AND tc.EntityId = s.SalesOrderId
WHERE tc.IsActive = 1
GROUP BY tc.EntityType, tc.EntityId,
         CASE tc.EntityType
             WHEN 'RFQ'          THEN r.RfqNo
             WHEN 'QUOTATION'    THEN q.QuotationNo
             WHEN 'SALES_ORDER'  THEN s.SalesOrderNo
         END;
GO


-- ============================================================================
-- GHI CHÚ IMPLEMENTATION (.NET Core)
-- ============================================================================
/*
    ┌─────────────────────────────────────────────────────────────┐
    │               LUỒNG KHI USER GỬI COMMENT                    │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  1. User gõ comment, mention @UserB, @UserC, bấm "Gửi"    │
    │                                                             │
    │  2. Frontend gửi POST /api/comments                         │
    │     {                                                       │
    │       entityType: "SALES_ORDER",                            │
    │       entityId: 3,                                          │
    │       parentCommentId: null,                                │
    │       content: "@UserB KH yêu cầu giao trước thứ 6",      │
    │       mentionedUserIds: [5, 8],                             │
    │       isInternal: false                                     │
    │     }                                                       │
    │                                                             │
    │  3. Backend: CommentService.CreateAsync()                   │
    │     a) Insert TaskComments                                  │
    │     b) Xác định danh sách người nhận notification:          │
    │        - MentionedUserIds: [5, 8]                           │
    │        - SalesPersonId của SO (nếu chưa trong list)         │
    │        - AssignedTo của RFQ (nếu entity là RFQ)             │
    │        - Loại bỏ người gửi (không tự thông báo cho mình)   │
    │     c) Insert Notifications cho từng người nhận:            │
    │        NotificationType = 'TASK_COMMENT'                    │
    │        Severity = 'INFO'                                    │
    │        ReferenceType = 'TASK_COMMENT'                       │
    │        ReferenceId = CommentId                              │
    │        ActionUrl = '/SalesOrder/Detail/3#comments'          │
    │     d) SignalR push cho từng người nhận:                    │
    │        await _hubContext.Clients                            │
    │          .Users(recipientIds)                               │
    │          .SendAsync("NewComment", new {                     │
    │            commentId, entityType, entityId,                 │
    │            senderName, content, createdAt                   │
    │          });                                                │
    │                                                             │
    │  4. Frontend nhận SignalR event "NewComment":               │
    │     a) Icon chuông: badge count +1                         │
    │     b) Nếu đang mở SO-003: append comment vào timeline    │
    │     c) Nếu đang ở workspace: hiện dot trên card SO-003    │
    │                                                             │
    ├─────────────────────────────────────────────────────────────┤
    │                  KHI USER MỞ COMMENT PANEL                  │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  GET /api/comments?entityType=SALES_ORDER&entityId=3       │
    │  → Trả về danh sách comment + thông tin user (avatar, tên) │
    │  → Mark tất cả Notification liên quan là đã đọc            │
    │                                                             │
    ├─────────────────────────────────────────────────────────────┤
    │                    MENTION / AUTO-COMPLETE                   │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  Khi user gõ "@" trong ô comment:                          │
    │  → Frontend gọi GET /api/users/search?q=...               │
    │  → Hiện dropdown danh sách user (FullName, UserCode)       │
    │  → Chọn → chèn @FullName vào content                      │
    │  → Lưu UserId vào MentionedUserIds                         │
    │                                                             │
    └─────────────────────────────────────────────────────────────┘
*/


PRINT '═══ TaskComments Migration completed successfully ═══';
PRINT 'New table: TaskComments';
PRINT 'New view: vw_TaskCommentCounts';
PRINT 'No existing tables modified';
GO
