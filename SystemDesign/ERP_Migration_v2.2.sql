-- ============================================================================
-- ERP DATABASE — MIGRATION: Per-item delivery, Order progress, Multi-advance
-- Version:  2.1 → 2.2
-- Date:     2026-04-15
-- Mô tả:
--   [~] SalesOrderItems   — thêm ExpectedReceiveDate, ExpectedDeliveryDate, ReceivedQty
--   [~] StockTransactionItems — thêm SOItemId (FK) để biết đợt nhập/xuất thuộc dòng nào
--   [+] AdvanceRequestItems — chi tiết tạm ứng theo dòng SO (hybrid: NULL = tạm ứng cả đơn)
--   [~] SalesOrders       — đánh dấu DEPRECATED các cột tạm ứng cũ (giữ lại không xóa)
--   [+] vw_OrderItemProgress — tiến độ từng dòng trong đơn
--   [+] vw_OrderProgress    — tiến độ tổng đơn
--   [+] vw_AdvanceProgress  — tổng hợp tạm ứng theo SO/SOItem
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. SALES ORDER ITEMS — Quản lý ngày dự kiến + số lượng theo dòng
-- ══════════════════════════════════════════════════════════════════

ALTER TABLE SalesOrderItems ADD
    -- Mỗi dòng có ngày dự kiến riêng (đợt về trước, đợt sau)
    ExpectedReceiveDate  DATE           NULL,
    ExpectedDeliveryDate DATE           NULL,
    -- Số lượng đã nhập kho (khác DeliveredQty: đã giao cho KH)
    ReceivedQty          DECIMAL(18,3)  NOT NULL DEFAULT 0;
GO

-- Computed columns — tự động tính realtime, không cần update tay
-- Số lượng còn cần nhập về kho
ALTER TABLE SalesOrderItems ADD
    RemainingReceiveQty AS (Quantity - ReceivedQty);
GO

-- Số lượng đã nhập kho nhưng chưa giao cho KH (đang nằm trong kho)
ALTER TABLE SalesOrderItems ADD
    InStockQty AS (ReceivedQty - DeliveredQty);
GO

-- Số lượng còn phải giao
ALTER TABLE SalesOrderItems ADD
    RemainingDeliverQty AS (Quantity - DeliveredQty);
GO

-- Backfill ReceivedQty cho dữ liệu cũ
-- Giả định: SO ở status RECEIVED trở đi → ReceivedQty = Quantity
UPDATE soi SET soi.ReceivedQty = soi.Quantity
FROM SalesOrderItems soi
INNER JOIN SalesOrders so ON soi.SalesOrderId = so.SalesOrderId
WHERE so.Status IN ('RECEIVED','DELIVERING','DELIVERED','COMPLETED','RETURNED','REPORTED');
GO


-- ══════════════════════════════════════════════════════════════════
-- 2. STOCK TRANSACTION ITEMS — Liên kết đợt nhập/xuất với dòng SO
-- ══════════════════════════════════════════════════════════════════
-- Mỗi đợt nhập/xuất biết thuộc dòng nào của đơn nào → tổng hợp tiến độ chính xác

ALTER TABLE StockTransactionItems ADD
    SOItemId INT NULL;
GO

ALTER TABLE StockTransactionItems ADD CONSTRAINT FK_STItem_SOItem
    FOREIGN KEY (SOItemId) REFERENCES SalesOrderItems(SOItemId);
GO

CREATE INDEX IX_STItems_SOItem ON StockTransactionItems(SOItemId);
GO


-- ══════════════════════════════════════════════════════════════════
-- 3. ADVANCE REQUEST ITEMS — Tạm ứng theo dòng (hybrid model)
-- ══════════════════════════════════════════════════════════════════
-- Một AdvanceRequest có thể:
--   (A) Tạm ứng cho cả đơn → KHÔNG INSERT AdvanceRequestItems
--                              hoặc 1 dòng với SOItemId = NULL + Amount
--   (B) Tạm ứng chi tiết theo dòng → INSERT N dòng AdvanceRequestItems
--                              mỗi dòng gắn 1 SOItemId + Amount
--
-- Logic kiểm tra:
--   Tổng AdvanceRequestItems.Amount phải = AdvanceRequests.RequestedAmount
--   (validate trong code, không enforce ở DB cho linh hoạt)

CREATE TABLE AdvanceRequestItems (
    AdvanceRequestItemId INT IDENTITY(1,1) PRIMARY KEY,
    AdvanceRequestId     INT             NOT NULL,
    SOItemId             INT             NULL,              -- NULL = tạm ứng cả đơn không gắn dòng cụ thể
    Amount               DECIMAL(18,2)   NOT NULL,          -- Số tiền tạm ứng cho dòng này
    Purpose              NVARCHAR(500)   NULL,              -- Mục đích chi tiết cho dòng (VD: "Mua dầu olive")
    Notes                NVARCHAR(500)   NULL,
    CreatedAt            DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AdvItem_Request FOREIGN KEY (AdvanceRequestId) REFERENCES AdvanceRequests(AdvanceRequestId),
    CONSTRAINT FK_AdvItem_SOItem FOREIGN KEY (SOItemId) REFERENCES SalesOrderItems(SOItemId)
);
GO

CREATE INDEX IX_AdvItems_Request ON AdvanceRequestItems(AdvanceRequestId);
CREATE INDEX IX_AdvItems_SOItem ON AdvanceRequestItems(SOItemId) WHERE SOItemId IS NOT NULL;
GO


-- ══════════════════════════════════════════════════════════════════
-- 4. SALESORDERS — Đánh dấu các cột tạm ứng cũ là DEPRECATED
-- ══════════════════════════════════════════════════════════════════
-- KHÔNG XÓA — giữ tương thích dữ liệu cũ.
-- Code mới phải dùng AdvanceRequests + AdvanceRequestItems.

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'[DEPRECATED v2.2] Dùng bảng AdvanceRequests + AdvanceRequestItems thay thế. Cột này chỉ giữ để tương thích dữ liệu cũ.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'SalesOrders',
    @level2type = N'COLUMN', @level2name = N'AdvanceAmount';

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'[DEPRECATED v2.2] Dùng AdvanceRequests.Status thay thế.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'SalesOrders',
    @level2type = N'COLUMN', @level2name = N'AdvanceStatus';

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'[DEPRECATED v2.2] Dùng AdvanceRequests.ApprovedAt thay thế.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'SalesOrders',
    @level2type = N'COLUMN', @level2name = N'AdvanceApprovedAt';

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'[DEPRECATED v2.2] Dùng AdvanceRequests.ReceivedAt thay thế.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'SalesOrders',
    @level2type = N'COLUMN', @level2name = N'AdvanceReceivedAt';
GO


-- ══════════════════════════════════════════════════════════════════
-- 5. VIEW: TIẾN ĐỘ TỪNG DÒNG TRONG ĐƠN
-- ══════════════════════════════════════════════════════════════════
-- Người dùng mở 1 SO → thấy mỗi dòng với cột "đã về / đang trong kho /
-- đã giao / còn thiếu" + ngày dự kiến + ngày đợt nhập/xuất gần nhất.

CREATE OR ALTER VIEW vw_OrderItemProgress AS
SELECT
    soi.SOItemId,
    soi.SalesOrderId,
    so.SalesOrderNo,
    so.CustomerPoNo,
    so.Status                     AS SOStatus,
    soi.ProductId,
    soi.ProductName,
    soi.UnitName,

    -- Số lượng (cần / đã về / trong kho / đã giao / còn thiếu)
    soi.Quantity                  AS OrderedQty,
    soi.ReceivedQty,
    soi.InStockQty,
    soi.DeliveredQty,
    soi.RemainingReceiveQty,
    soi.RemainingDeliverQty,

    -- Phần trăm tiến độ
    CASE WHEN soi.Quantity = 0 THEN 0
         ELSE CAST(soi.ReceivedQty * 100.0 / soi.Quantity AS DECIMAL(5,1))
    END AS ReceiveProgressPct,
    CASE WHEN soi.Quantity = 0 THEN 0
         ELSE CAST(soi.DeliveredQty * 100.0 / soi.Quantity AS DECIMAL(5,1))
    END AS DeliverProgressPct,

    -- Ngày dự kiến
    soi.ExpectedReceiveDate,
    soi.ExpectedDeliveryDate,

    -- Trạng thái dòng (NOT_STARTED → PARTIAL → COMPLETED → DELIVERED)
    CASE
        WHEN soi.DeliveredQty >= soi.Quantity                THEN 'DELIVERED'
        WHEN soi.ReceivedQty >= soi.Quantity                 THEN 'RECEIVED_FULL'
        WHEN soi.ReceivedQty > 0 AND soi.ReceivedQty < soi.Quantity THEN 'PARTIAL'
        WHEN soi.ReceivedQty = 0                             THEN 'NOT_STARTED'
    END AS LineStatus,

    -- Cảnh báo trễ
    CASE
        WHEN soi.ReceivedQty < soi.Quantity
         AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
        THEN 1 ELSE 0
    END AS IsReceiveOverdue,

    CASE
        WHEN soi.DeliveredQty < soi.Quantity
         AND soi.ExpectedDeliveryDate < CAST(GETDATE() AS DATE)
        THEN 1 ELSE 0
    END AS IsDeliverOverdue,

    -- Lần nhập kho gần nhất cho dòng này
    (SELECT MAX(st.TransactionDate)
     FROM StockTransactionItems sti
     INNER JOIN StockTransactions st ON sti.TransactionId = st.TransactionId
     WHERE sti.SOItemId = soi.SOItemId
       AND st.TransactionType = 'INBOUND'
       AND st.Status = 'CONFIRMED'
    ) AS LastInboundDate,

    (SELECT MAX(st.TransactionDate)
     FROM StockTransactionItems sti
     INNER JOIN StockTransactions st ON sti.TransactionId = st.TransactionId
     WHERE sti.SOItemId = soi.SOItemId
       AND st.TransactionType = 'OUTBOUND'
       AND st.Status = 'DELIVERED'
    ) AS LastOutboundDate,

    soi.SortOrder
FROM SalesOrderItems soi
INNER JOIN SalesOrders so ON soi.SalesOrderId = so.SalesOrderId;
GO


-- ══════════════════════════════════════════════════════════════════
-- 6. VIEW: TIẾN ĐỘ TỔNG ĐƠN
-- ══════════════════════════════════════════════════════════════════

CREATE OR ALTER VIEW vw_OrderProgress AS
SELECT
    so.SalesOrderId,
    so.SalesOrderNo,
    so.CustomerPoNo,
    so.Status,
    c.CustomerName,

    -- Tổng SL
    SUM(soi.Quantity)        AS TotalQty,
    SUM(soi.ReceivedQty)     AS TotalReceived,
    SUM(soi.DeliveredQty)    AS TotalDelivered,
    SUM(soi.InStockQty)      AS TotalInStock,

    -- Số dòng theo trạng thái
    COUNT(soi.SOItemId)                                                          AS TotalLines,
    SUM(CASE WHEN soi.ReceivedQty = 0 THEN 1 ELSE 0 END)                         AS NotStartedLines,
    SUM(CASE WHEN soi.ReceivedQty > 0 AND soi.ReceivedQty < soi.Quantity
             THEN 1 ELSE 0 END)                                                   AS PartialLines,
    SUM(CASE WHEN soi.ReceivedQty >= soi.Quantity AND soi.DeliveredQty < soi.Quantity
             THEN 1 ELSE 0 END)                                                   AS ReadyToDeliverLines,
    SUM(CASE WHEN soi.DeliveredQty >= soi.Quantity THEN 1 ELSE 0 END)            AS DeliveredLines,

    -- % tiến độ tổng
    CASE WHEN SUM(soi.Quantity) = 0 THEN 0
         ELSE CAST(SUM(soi.ReceivedQty) * 100.0 / SUM(soi.Quantity) AS DECIMAL(5,1))
    END AS OverallReceivePct,
    CASE WHEN SUM(soi.Quantity) = 0 THEN 0
         ELSE CAST(SUM(soi.DeliveredQty) * 100.0 / SUM(soi.Quantity) AS DECIMAL(5,1))
    END AS OverallDeliverPct,

    -- Số dòng trễ hạn
    SUM(CASE WHEN soi.ReceivedQty < soi.Quantity
              AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END)                                                   AS OverdueReceiveLines,
    SUM(CASE WHEN soi.DeliveredQty < soi.Quantity
              AND soi.ExpectedDeliveryDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END)                                                   AS OverdueDeliverLines,

    so.OrderDate,
    so.ExpectedReceiveDate    AS SO_ExpectedReceiveDate,
    so.ExpectedDeliveryDate   AS SO_ExpectedDeliveryDate
FROM SalesOrders so
INNER JOIN Customers c ON so.CustomerId = c.CustomerId
LEFT JOIN SalesOrderItems soi ON so.SalesOrderId = soi.SalesOrderId
WHERE so.Status NOT IN ('CANCELLED')
GROUP BY so.SalesOrderId, so.SalesOrderNo, so.CustomerPoNo, so.Status,
         c.CustomerName, so.OrderDate, so.ExpectedReceiveDate, so.ExpectedDeliveryDate;
GO


-- ══════════════════════════════════════════════════════════════════
-- 7. VIEW: TỔNG HỢP TẠM ỨNG THEO SO / SOItem
-- ══════════════════════════════════════════════════════════════════

CREATE OR ALTER VIEW vw_AdvanceProgress AS
SELECT
    so.SalesOrderId,
    so.SalesOrderNo,
    so.TotalAmount               AS OrderTotal,
    so.PurchaseCost,

    -- Tổng đề nghị tạm ứng
    ISNULL(SUM(CASE WHEN ar.Status NOT IN ('REJECTED','CANCELLED')
                    THEN ar.RequestedAmount ELSE 0 END), 0)        AS TotalRequested,

    -- Tổng đã được duyệt
    ISNULL(SUM(CASE WHEN ar.Status IN ('APPROVED','RECEIVED','SETTLING','SETTLED')
                    THEN ISNULL(ar.ApprovedAmount, ar.RequestedAmount) ELSE 0 END), 0) AS TotalApproved,

    -- Tổng đã nhận tiền
    ISNULL(SUM(CASE WHEN ar.Status IN ('RECEIVED','SETTLING','SETTLED')
                    THEN ISNULL(ar.ApprovedAmount, ar.RequestedAmount) ELSE 0 END), 0) AS TotalReceived,

    -- Tổng đã quyết toán
    ISNULL(SUM(CASE WHEN ar.Status = 'SETTLED'
                    THEN ar.ActualSpent ELSE 0 END), 0)            AS TotalSettled,

    -- Số lần tạm ứng
    COUNT(ar.AdvanceRequestId)                                     AS RequestCount,
    SUM(CASE WHEN ar.Status = 'PENDING' THEN 1 ELSE 0 END)        AS PendingCount,
    SUM(CASE WHEN ar.Status = 'RECEIVED' THEN 1 ELSE 0 END)       AS ActiveCount,
    SUM(CASE WHEN ar.Status = 'SETTLED' THEN 1 ELSE 0 END)        AS SettledCount
FROM SalesOrders so
LEFT JOIN AdvanceRequests ar ON so.SalesOrderId = ar.SalesOrderId
GROUP BY so.SalesOrderId, so.SalesOrderNo, so.TotalAmount, so.PurchaseCost;
GO


-- ══════════════════════════════════════════════════════════════════
-- 8. VIEW: TẠM ỨNG THEO TỪNG DÒNG SO (chi tiết)
-- ══════════════════════════════════════════════════════════════════

CREATE OR ALTER VIEW vw_AdvanceByItem AS
SELECT
    soi.SOItemId,
    soi.SalesOrderId,
    soi.ProductName,
    soi.Quantity,
    soi.PurchasePrice,
    (soi.Quantity * ISNULL(soi.PurchasePrice, 0))                  AS EstimatedCost,

    -- Tổng tạm ứng cho dòng này (chỉ tính lần đã APPROVED trở đi)
    ISNULL((
        SELECT SUM(ari.Amount)
        FROM AdvanceRequestItems ari
        INNER JOIN AdvanceRequests ar ON ari.AdvanceRequestId = ar.AdvanceRequestId
        WHERE ari.SOItemId = soi.SOItemId
          AND ar.Status IN ('APPROVED','RECEIVED','SETTLING','SETTLED')
    ), 0) AS AdvancedAmount,

    -- Số lần tạm ứng riêng cho dòng này
    ISNULL((
        SELECT COUNT(*)
        FROM AdvanceRequestItems ari
        INNER JOIN AdvanceRequests ar ON ari.AdvanceRequestId = ar.AdvanceRequestId
        WHERE ari.SOItemId = soi.SOItemId
          AND ar.Status NOT IN ('REJECTED','CANCELLED')
    ), 0) AS AdvanceTimes
FROM SalesOrderItems soi;
GO


PRINT '═══ Migration v2.2 completed ═══';
PRINT '';
PRINT 'CẬP NHẬT 1 — SOItems theo dõi đợt nhập/giao:';
PRINT '  +ExpectedReceiveDate, +ExpectedDeliveryDate, +ReceivedQty';
PRINT '  +Computed: RemainingReceiveQty, InStockQty, RemainingDeliverQty';
PRINT '  StockTransactionItems +SOItemId';
PRINT '';
PRINT 'CẬP NHẬT 2 — Tra cứu tiến độ:';
PRINT '  vw_OrderItemProgress (chi tiết dòng)';
PRINT '  vw_OrderProgress (tổng đơn)';
PRINT '';
PRINT 'CẬP NHẬT 3 — Tạm ứng nhiều lần (hybrid):';
PRINT '  +AdvanceRequestItems (SOItemId nullable)';
PRINT '    SOItemId NULL  → tạm ứng cho cả đơn';
PRINT '    SOItemId NOT NULL → tạm ứng theo dòng';
PRINT '  vw_AdvanceProgress (tổng đơn)';
PRINT '  vw_AdvanceByItem (theo dòng)';
PRINT '  SO.AdvanceAmount/Status/... → DEPRECATED (giữ lại tương thích)';
GO
