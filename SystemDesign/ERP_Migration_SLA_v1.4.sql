-- ============================================================================
-- ERP DATABASE — MIGRATION: SLA (Service Level Agreement)
-- Version:  1.3 → 1.4
-- Date:     2026-04-11
-- Mô tả:
--   [+] SlaConfigs    — Cấu hình thời gian cho từng bước quy trình
--   [+] SlaTracking   — Theo dõi deadline realtime cho từng bản ghi
--   [~] Notifications — Thêm Severity + ActionUrl
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ============================================================================
-- BẢNG 1: SLA CONFIGS (Cấu hình SLA — Admin setup 1 lần)
-- ============================================================================
-- Mỗi dòng = 1 rule: "Khi entity X ở trạng thái Y, phải chuyển sang bước tiếp
-- trong vòng N giờ. Cảnh báo vàng khi còn M% thời gian. Đỏ khi hết."
--
-- Ví dụ cấu hình thực tế:
-- ┌──────────────┬────────────┬──────────┬────────────┬────────────┐
-- │ EntityType   │ FromStatus │ Duration │ WarningPct │ Ý nghĩa    │
-- ├──────────────┼────────────┼──────────┼────────────┼────────────┤
-- │ RFQ          │ INPROGRESS │ 4h       │ 75%        │ 4h tạo QT  │
-- │ QUOTATION    │ DRAFT      │ 8h       │ 75%        │ 8h soạn BG │
-- │ QUOTATION    │ SENT       │ 48h      │ 80%        │ 48h chờ KH │
-- │ SALES_ORDER  │ DRAFT      │ 4h       │ 75%        │ 4h nhập PO │
-- │ SALES_ORDER  │ WAIT       │ 24h      │ 80%        │ 24h tạm ứng│
-- │ SALES_ORDER  │ BUYING     │ 72h      │ 80%        │ 72h mua    │
-- │ SALES_ORDER  │ RECEIVED   │ 8h       │ 75%        │ 8h nhập kho│
-- │ SALES_ORDER  │ DELIVERING │ 24h      │ 80%        │ 24h giao   │
-- │ SALES_ORDER  │ DELIVERED  │ 48h      │ 80%        │ 48h q.toán │
-- └──────────────┴────────────┴──────────┴────────────┴────────────┘

CREATE TABLE SlaConfigs (
    SlaConfigId     INT IDENTITY(1,1) PRIMARY KEY,

    -- Áp dụng cho đối tượng + trạng thái nào
    EntityType      NVARCHAR(20)    NOT NULL,              -- RFQ, QUOTATION, SALES_ORDER
                    CHECK (EntityType IN ('RFQ','QUOTATION','SALES_ORDER')),
    FromStatus      NVARCHAR(25)    NOT NULL,              -- Status đang theo dõi

    -- Thời gian cho phép
    DurationHours   DECIMAL(10,2)   NOT NULL,              -- Số giờ làm việc cho phép (VD: 8, 24, 72)
    DurationCalendar BIT            NOT NULL DEFAULT 0,    -- 0 = chỉ tính giờ làm việc, 1 = tính 24/7

    -- Ngưỡng cảnh báo
    WarningPercent  DECIMAL(5,2)    NOT NULL DEFAULT 80,   -- Cảnh báo VÀNG khi đã dùng N% thời gian
    -- Đỏ = 100% (hết thời gian) — không cần cấu hình riêng

    -- Ai nhận cảnh báo
    NotifyAssignee  BIT             NOT NULL DEFAULT 1,    -- Gửi cho người được gán (AssignedTo / SalesPersonId)
    NotifyManager   BIT             NOT NULL DEFAULT 0,    -- Gửi cho quản lý
    EscalateOnOverdue BIT           NOT NULL DEFAULT 0,    -- Khi quá hạn: gửi thêm cho cấp trên

    -- Mô tả
    ConfigName      NVARCHAR(200)   NOT NULL,              -- VD: "Thời gian soạn báo giá"
    Description     NVARCHAR(500)   NULL,

    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy       INT             NOT NULL,

    CONSTRAINT FK_SlaConfig_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT UQ_SlaConfig UNIQUE (EntityType, FromStatus)    -- 1 config / entity+status
);
GO


-- ============================================================================
-- BẢNG 2: SLA TRACKING (Theo dõi runtime cho từng bản ghi cụ thể)
-- ============================================================================
-- Mỗi khi SO/Quotation/RFQ chuyển status → hệ thống tự động:
--   1. Đóng dòng tracking cũ (Status = COMPLETED hoặc SKIPPED)
--   2. Tạo dòng tracking mới cho status mới (nếu có SlaConfig tương ứng)
--   3. Tính DeadlineAt = StartedAt + DurationHours
--   4. Tính WarningAt = StartedAt + (DurationHours * WarningPercent / 100)
--
-- Background job (chạy mỗi 1 phút) kiểm tra:
--   - Nếu NOW >= WarningAt && WarningNotifiedAt IS NULL → gửi cảnh báo VÀNG
--   - Nếu NOW >= DeadlineAt && OverdueNotifiedAt IS NULL → gửi cảnh báo ĐỎ

CREATE TABLE SlaTracking (
    SlaTrackingId   BIGINT IDENTITY(1,1) PRIMARY KEY,
    SlaConfigId     INT             NOT NULL,              -- Rule SLA nào

    -- Bản ghi đang theo dõi
    EntityType      NVARCHAR(20)    NOT NULL,
    EntityId        INT             NOT NULL,              -- RfqId, QuotationId, hoặc SalesOrderId
    TrackedStatus   NVARCHAR(25)    NOT NULL,              -- Status đang theo dõi

    -- Ai chịu trách nhiệm
    AssigneeId      INT             NULL,                  -- NV được gán (để gửi notification)

    -- Mốc thời gian
    StartedAt       DATETIME2       NOT NULL,              -- Thời điểm vào status này
    WarningAt       DATETIME2       NOT NULL,              -- Thời điểm cảnh báo vàng
    DeadlineAt      DATETIME2       NOT NULL,              -- Thời điểm hết hạn (đỏ)
    CompletedAt     DATETIME2       NULL,                  -- Thời điểm hoàn thành (chuyển status tiếp)

    -- Trạng thái tracking
    -- ACTIVE:    đang theo dõi
    -- WARNING:   đã vàng, chưa quá hạn
    -- OVERDUE:   đã quá hạn
    -- COMPLETED: đã chuyển status tiếp (hoàn thành đúng hạn hoặc trễ)
    -- SKIPPED:   bỏ qua (VD: SO bị CANCELLED khi đang BUYING)
    Status          NVARCHAR(15)    NOT NULL DEFAULT 'ACTIVE'
                    CHECK (Status IN ('ACTIVE','WARNING','OVERDUE','COMPLETED','SKIPPED')),

    -- Đã gửi cảnh báo chưa (tránh gửi lặp)
    WarningNotifiedAt  DATETIME2    NULL,                  -- Thời điểm đã gửi cảnh báo vàng
    OverdueNotifiedAt  DATETIME2    NULL,                  -- Thời điểm đã gửi cảnh báo đỏ

    -- Thống kê
    ElapsedHours    AS (DATEDIFF(MINUTE, StartedAt, ISNULL(CompletedAt, SYSDATETIME())) / 60.0),  -- Giờ đã dùng
    IsOnTime        AS (CASE WHEN CompletedAt IS NOT NULL AND CompletedAt <= DeadlineAt THEN 1
                              WHEN CompletedAt IS NOT NULL AND CompletedAt > DeadlineAt THEN 0
                              ELSE NULL END),              -- Đúng hạn hay trễ

    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_SlaTrack_Config FOREIGN KEY (SlaConfigId) REFERENCES SlaConfigs(SlaConfigId),
    CONSTRAINT FK_SlaTrack_Assignee FOREIGN KEY (AssigneeId) REFERENCES Users(UserId)
);
GO


-- ============================================================================
-- SỬA BẢNG NOTIFICATIONS — Thêm Severity + ActionUrl
-- ============================================================================

ALTER TABLE Notifications ADD
    Severity    NVARCHAR(10)    NOT NULL DEFAULT 'INFO'
                CONSTRAINT CK_Notif_Severity CHECK (Severity IN ('INFO','WARNING','DANGER')),
    ActionUrl   NVARCHAR(500)   NULL;           -- Link trực tiếp: /SalesOrder/Detail/123
GO


-- ============================================================================
-- INDEXES
-- ============================================================================

-- SlaConfigs
CREATE INDEX IX_SlaConfig_Entity ON SlaConfigs(EntityType, FromStatus, IsActive);

-- SlaTracking — quan trọng nhất, background job query liên tục
CREATE INDEX IX_SlaTrack_Active ON SlaTracking(Status, WarningAt, DeadlineAt)
    WHERE Status IN ('ACTIVE','WARNING');                  -- Filtered index: chỉ track dòng đang active

CREATE INDEX IX_SlaTrack_Entity ON SlaTracking(EntityType, EntityId, Status);
CREATE INDEX IX_SlaTrack_Assignee ON SlaTracking(AssigneeId, Status);
CREATE INDEX IX_SlaTrack_Config ON SlaTracking(SlaConfigId);

-- Notifications — thêm index cho Severity (UI filter "chỉ xem cảnh báo")
CREATE INDEX IX_Notif_Severity ON Notifications(UserId, Severity, IsRead);
GO


-- ============================================================================
-- SEED DATA — Cấu hình SLA mặc định
-- ============================================================================

-- Lấy UserId admin (NV-0001)
DECLARE @AdminId INT = (SELECT TOP 1 UserId FROM Users WHERE UserCode = 'NV-0001');

INSERT INTO SlaConfigs (EntityType, FromStatus, DurationHours, DurationCalendar, WarningPercent, NotifyAssignee, NotifyManager, EscalateOnOverdue, ConfigName, Description, CreatedBy) VALUES
-- RFQ
('RFQ',         'INPROGRESS',  4,    0, 75, 1, 0, 0, N'Xử lý RFQ',             N'Thời gian từ nhận yêu cầu đến tạo báo giá',             @AdminId),

-- Quotation
('QUOTATION',   'DRAFT',       8,    0, 75, 1, 0, 0, N'Soạn báo giá',           N'Thời gian soạn báo giá sau khi nhận RFQ',                @AdminId),
('QUOTATION',   'SENT',        48,   1, 80, 1, 1, 0, N'Chờ phản hồi KH',       N'Thời gian chờ KH phản hồi báo giá (tính 24/7)',          @AdminId),

-- Sales Order
('SALES_ORDER', 'DRAFT',       4,    0, 75, 1, 0, 0, N'Nhập PO khách sạn',     N'Thời gian nhập mã PO, upload file, tạo đề nghị tạm ứng', @AdminId),
('SALES_ORDER', 'WAIT',        24,   0, 80, 1, 1, 1, N'Chờ duyệt tạm ứng',    N'Thời gian chờ phê duyệt và nhận tiền tạm ứng',           @AdminId),
('SALES_ORDER', 'BUYING',      72,   0, 80, 1, 1, 1, N'Mua hàng từ NCC',       N'Thời gian mua hàng và chờ NCC giao',                     @AdminId),
('SALES_ORDER', 'RECEIVED',    8,    0, 75, 1, 0, 0, N'Nhập kho',               N'Thời gian kiểm tra và nhập kho sau khi hàng về',          @AdminId),
('SALES_ORDER', 'DELIVERING',  24,   0, 80, 1, 1, 0, N'Giao hàng cho KH',      N'Thời gian vận chuyển đến khách hàng',                    @AdminId),
('SALES_ORDER', 'DELIVERED',   48,   0, 80, 1, 1, 1, N'Quyết toán',             N'Thời gian hoàn ứng hoặc thanh toán bổ sung sau giao hàng',@AdminId);
GO


-- ============================================================================
-- VIEW: Dashboard SLA (hiện danh sách task đang warning/overdue)
-- ============================================================================

CREATE OR ALTER VIEW vw_SlaAlerts AS
SELECT
    t.SlaTrackingId,
    t.EntityType,
    t.EntityId,
    t.TrackedStatus,
    t.AssigneeId,
    u.FullName              AS AssigneeName,
    c.ConfigName,
    t.StartedAt,
    t.WarningAt,
    t.DeadlineAt,
    t.Status                AS SlaStatus,

    -- Tính toán realtime
    DATEDIFF(MINUTE, t.StartedAt, SYSDATETIME()) / 60.0   AS ElapsedHours,
    c.DurationHours,
    DATEDIFF(MINUTE, SYSDATETIME(), t.DeadlineAt) / 60.0  AS RemainingHours,

    -- % tiến trình thời gian (0-100+)
    CAST(DATEDIFF(MINUTE, t.StartedAt, SYSDATETIME()) * 100.0
         / NULLIF(DATEDIFF(MINUTE, t.StartedAt, t.DeadlineAt), 0)
         AS DECIMAL(5,1))   AS TimeUsedPercent,

    -- Màu hiển thị UI
    CASE
        WHEN SYSDATETIME() >= t.DeadlineAt THEN 'DANGER'       -- Đỏ: quá hạn
        WHEN SYSDATETIME() >= t.WarningAt  THEN 'WARNING'      -- Vàng: sắp hết
        ELSE 'NORMAL'                                           -- Bình thường
    END AS DisplaySeverity,

    -- Thông tin bổ sung để hiện trên workspace
    CASE t.EntityType
        WHEN 'RFQ'          THEN r.RfqNo
        WHEN 'QUOTATION'    THEN q.QuotationNo
        WHEN 'SALES_ORDER'  THEN s.SalesOrderNo
    END AS EntityNo,

    CASE t.EntityType
        WHEN 'RFQ'          THEN rc.CustomerName
        WHEN 'QUOTATION'    THEN qc.CustomerName
        WHEN 'SALES_ORDER'  THEN sc.CustomerName
    END AS CustomerName

FROM SlaTracking t
INNER JOIN SlaConfigs c ON t.SlaConfigId = c.SlaConfigId
LEFT JOIN Users u ON t.AssigneeId = u.UserId
-- Join để lấy mã đơn + tên KH
LEFT JOIN RFQs r ON t.EntityType = 'RFQ' AND t.EntityId = r.RfqId
LEFT JOIN Customers rc ON r.CustomerId = rc.CustomerId
LEFT JOIN Quotations q ON t.EntityType = 'QUOTATION' AND t.EntityId = q.QuotationId
LEFT JOIN Customers qc ON q.CustomerId = qc.CustomerId
LEFT JOIN SalesOrders s ON t.EntityType = 'SALES_ORDER' AND t.EntityId = s.SalesOrderId
LEFT JOIN Customers sc ON s.CustomerId = sc.CustomerId
WHERE t.Status IN ('ACTIVE','WARNING','OVERDUE');
GO


-- ============================================================================
-- VIEW: Thống kê SLA performance (báo cáo tuân thủ SLA theo nhân viên)
-- ============================================================================

CREATE OR ALTER VIEW vw_SlaPerformance AS
SELECT
    t.AssigneeId,
    u.FullName,
    u.UserCode,
    t.EntityType,
    t.TrackedStatus,
    c.ConfigName,
    COUNT(*)                                                        AS TotalTasks,
    COUNT(CASE WHEN t.Status = 'COMPLETED' AND t.IsOnTime = 1 THEN 1 END) AS OnTimeTasks,
    COUNT(CASE WHEN t.Status = 'COMPLETED' AND t.IsOnTime = 0 THEN 1 END) AS LateTasks,
    COUNT(CASE WHEN t.Status IN ('ACTIVE','WARNING') THEN 1 END)   AS ActiveTasks,
    COUNT(CASE WHEN t.Status = 'OVERDUE' THEN 1 END)               AS OverdueTasks,
    -- Tỉ lệ tuân thủ SLA (%)
    CASE
        WHEN COUNT(CASE WHEN t.Status = 'COMPLETED' THEN 1 END) = 0 THEN NULL
        ELSE CAST(COUNT(CASE WHEN t.Status = 'COMPLETED' AND t.IsOnTime = 1 THEN 1 END) * 100.0
                  / COUNT(CASE WHEN t.Status = 'COMPLETED' THEN 1 END) AS DECIMAL(5,1))
    END AS SlaComplianceRate,
    -- Thời gian xử lý trung bình (giờ)
    AVG(CASE WHEN t.Status = 'COMPLETED'
             THEN DATEDIFF(MINUTE, t.StartedAt, t.CompletedAt) / 60.0 END) AS AvgCompletionHours
FROM SlaTracking t
INNER JOIN SlaConfigs c ON t.SlaConfigId = c.SlaConfigId
LEFT JOIN Users u ON t.AssigneeId = u.UserId
GROUP BY t.AssigneeId, u.FullName, u.UserCode, t.EntityType, t.TrackedStatus, c.ConfigName;
GO


PRINT '═══ SLA Migration completed successfully ═══';
PRINT 'New tables: SlaConfigs, SlaTracking';
PRINT 'Modified: Notifications (+Severity, +ActionUrl)';
PRINT 'New views: vw_SlaAlerts, vw_SlaPerformance';
PRINT 'Seed data: 9 SLA rules configured';
GO
