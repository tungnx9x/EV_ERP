-- ============================================================================
-- ERP DATABASE — MIGRATION: Tăng số lượng dòng SO (khách mua thêm)
-- Version:  2.14 → 2.15
-- Date:     2026-06-21
-- Mô tả:
--   Hiện tại SOItem chỉ giảm SL qua hủy (CancelledQty). Thực tế khách có thể
--   mua NHIỀU HƠN số lượng đã báo giá. Bổ sung cơ chế "tăng SL" đối xứng với hủy.
--
--   [~] SalesOrderItems — thêm AddedQty, AddReason, AddedAt, AddedBy
--   [~] SalesOrderItems — drop & recreate computed cols để CỘNG AddedQty
--                          (SL hiệu lực = Quantity - CancelledQty + AddedQty)
--   [~] SalesOrderItems — thêm computed col OrderedQty = Quantity - CancelledQty + AddedQty
--                          (SL "đặt hàng thực tế" — dùng cho mọi nghiệp vụ downstream;
--                           Quantity giữ làm SL báo giá gốc, chỉ để tham chiếu)
--   [~] vw_OrderItemProgress / vw_OrderProgress — EffectiveQty dùng công thức mới
-- ============================================================================

USE ERP_ThuongMaiTrungGian_1_6;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. SALES ORDER ITEMS — thêm trường tăng SL
-- ══════════════════════════════════════════════════════════════════

ALTER TABLE SalesOrderItems ADD
    AddedQty  DECIMAL(18,3) NOT NULL DEFAULT 0,
    AddReason NVARCHAR(500) NULL,
    AddedAt   DATETIME2     NULL,
    AddedBy   INT           NULL;
GO

-- Optional FK to Users (mềm — không ép buộc cascade)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SOItem_AddedBy')
BEGIN
    ALTER TABLE SalesOrderItems
        ADD CONSTRAINT FK_SOItem_AddedBy
            FOREIGN KEY (AddedBy) REFERENCES Users(UserId);
END
GO


-- ══════════════════════════════════════════════════════════════════
-- 2. Drop & recreate computed columns — cộng thêm AddedQty
-- ══════════════════════════════════════════════════════════════════
-- Cũ: RemainingReceiveQty = (Quantity - CancelledQty) - ReceivedQty
-- Mới: RemainingReceiveQty = (Quantity - CancelledQty + AddedQty) - ReceivedQty
-- (Tương tự cho RemainingDeliverQty — InStockQty giữ nguyên)

ALTER TABLE SalesOrderItems DROP COLUMN RemainingReceiveQty;
GO
ALTER TABLE SalesOrderItems DROP COLUMN RemainingDeliverQty;
GO

ALTER TABLE SalesOrderItems ADD
    RemainingReceiveQty AS ((Quantity - CancelledQty + AddedQty) - ReceivedQty);
GO

ALTER TABLE SalesOrderItems ADD
    RemainingDeliverQty AS ((Quantity - CancelledQty + AddedQty) - DeliveredQty);
GO

-- SL đặt hàng thực tế (dùng cho mọi nghiệp vụ downstream)
ALTER TABLE SalesOrderItems ADD
    OrderedQty AS (Quantity - CancelledQty + AddedQty);
GO


-- ══════════════════════════════════════════════════════════════════
-- 3. VIEW: vw_OrderItemProgress — EffectiveQty = Quantity - CancelledQty + AddedQty
-- ══════════════════════════════════════════════════════════════════

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

    soi.Quantity                  AS OrderedQty,          -- SL báo giá gốc (tham chiếu)
    soi.CancelledQty,
    soi.AddedQty,
    (soi.Quantity - soi.CancelledQty + soi.AddedQty)  AS EffectiveQty,
    soi.ReceivedQty,
    soi.InStockQty,
    soi.DeliveredQty,
    soi.RemainingReceiveQty,
    soi.RemainingDeliverQty,

    CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) = 0 THEN 100
         ELSE CAST(soi.ReceivedQty * 100.0 / (soi.Quantity - soi.CancelledQty + soi.AddedQty) AS DECIMAL(5,1))
    END AS ReceiveProgressPct,
    CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) = 0 THEN 100
         ELSE CAST(soi.DeliveredQty * 100.0 / (soi.Quantity - soi.CancelledQty + soi.AddedQty) AS DECIMAL(5,1))
    END AS DeliverProgressPct,

    soi.ExpectedReceiveDate,
    soi.ExpectedDeliveryDate,

    -- LineStatus: CANCELLED ưu tiên cao nhất (chỉ khi không có phần tăng thêm còn lại)
    CASE
        WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) <= 0              THEN 'CANCELLED'
        WHEN soi.DeliveredQty >= (soi.Quantity - soi.CancelledQty + soi.AddedQty) THEN 'DELIVERED'
        WHEN soi.DeliveredQty > 0                                               THEN 'PARTIAL_DELIVERED'
        WHEN soi.ReceivedQty  >= (soi.Quantity - soi.CancelledQty + soi.AddedQty) THEN 'RECEIVED_FULL'
        WHEN soi.ReceivedQty  > 0                                               THEN 'PARTIAL'
        ELSE 'NOT_STARTED'
    END AS LineStatus,

    CASE
        WHEN soi.ReceivedQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
         AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
        THEN 1 ELSE 0
    END AS IsReceiveOverdue,

    CASE
        WHEN soi.DeliveredQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
         AND soi.ExpectedDeliveryDate < CAST(GETDATE() AS DATE)
        THEN 1 ELSE 0
    END AS IsDeliverOverdue,

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
-- 4. VIEW: vw_OrderProgress — EffectiveQty = Quantity - CancelledQty + AddedQty
-- ══════════════════════════════════════════════════════════════════

CREATE OR ALTER VIEW vw_OrderProgress AS
SELECT
    so.SalesOrderId,
    so.SalesOrderNo,
    so.CustomerPoNo,
    so.Status,
    c.CustomerName,

    SUM(soi.Quantity)        AS TotalQty,
    SUM(soi.CancelledQty)    AS TotalCancelled,
    SUM(soi.AddedQty)        AS TotalAdded,
    SUM(soi.Quantity - soi.CancelledQty + soi.AddedQty) AS TotalEffectiveQty,
    SUM(soi.ReceivedQty)     AS TotalReceived,
    SUM(soi.DeliveredQty)    AS TotalDelivered,
    SUM(soi.InStockQty)      AS TotalInStock,

    COUNT(soi.SOItemId)                                                            AS TotalLines,
    SUM(CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) <= 0 THEN 1 ELSE 0 END) AS CancelledLines,
    SUM(CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) > 0 AND soi.ReceivedQty = 0 THEN 1 ELSE 0 END) AS NotStartedLines,
    SUM(CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) > 0
              AND soi.ReceivedQty > 0
              AND soi.ReceivedQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
             THEN 1 ELSE 0 END)                                                     AS PartialLines,
    SUM(CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) > 0
              AND soi.ReceivedQty >= (soi.Quantity - soi.CancelledQty + soi.AddedQty)
              AND soi.DeliveredQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
             THEN 1 ELSE 0 END)                                                     AS ReadyToDeliverLines,
    SUM(CASE WHEN (soi.Quantity - soi.CancelledQty + soi.AddedQty) > 0
              AND soi.DeliveredQty >= (soi.Quantity - soi.CancelledQty + soi.AddedQty)
             THEN 1 ELSE 0 END)                                                     AS DeliveredLines,

    CASE WHEN SUM(soi.Quantity - soi.CancelledQty + soi.AddedQty) = 0 THEN 0
         ELSE CAST(SUM(soi.ReceivedQty) * 100.0 / SUM(soi.Quantity - soi.CancelledQty + soi.AddedQty) AS DECIMAL(5,1))
    END AS OverallReceivePct,
    CASE WHEN SUM(soi.Quantity - soi.CancelledQty + soi.AddedQty) = 0 THEN 0
         ELSE CAST(SUM(soi.DeliveredQty) * 100.0 / SUM(soi.Quantity - soi.CancelledQty + soi.AddedQty) AS DECIMAL(5,1))
    END AS OverallDeliverPct,

    SUM(CASE WHEN soi.ReceivedQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
              AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END)                                                     AS OverdueReceiveLines,
    SUM(CASE WHEN soi.DeliveredQty < (soi.Quantity - soi.CancelledQty + soi.AddedQty)
              AND soi.ExpectedDeliveryDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END)                                                     AS OverdueDeliverLines,

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


PRINT '═══ Migration v2.15 completed ═══';
PRINT 'SOItem +AddedQty/AddReason/AddedAt/AddedBy';
PRINT 'RemainingReceiveQty/RemainingDeliverQty +AddedQty; +OrderedQty computed col';
PRINT 'vw_OrderItemProgress + vw_OrderProgress refresh (EffectiveQty = Quantity - CancelledQty + AddedQty)';
GO
