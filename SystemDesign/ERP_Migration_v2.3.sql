-- ============================================================================
-- ERP DATABASE — MIGRATION: Per-line cancellation
-- Version:  2.2 → 2.3
-- Date:     2026-05-11
-- Mô tả:
--   [~] SalesOrderItems — thêm CancelledQty, CancelReason, CancelledAt, CancelledBy
--   [~] SalesOrderItems — drop & recreate computed cols để trừ CancelledQty
--                          (EffectiveQty = Quantity - CancelledQty)
--   [~] vw_OrderItemProgress — bổ sung CancelledQty + LineStatus 'CANCELLED'
--   [~] vw_OrderProgress     — đếm dòng đã hủy, dùng EffectiveQty cho tiến độ
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ══════════════════════════════════════════════════════════════════
-- 1. SALES ORDER ITEMS — thêm trường hủy dòng
-- ══════════════════════════════════════════════════════════════════

ALTER TABLE SalesOrderItems ADD
    CancelledQty  DECIMAL(18,3) NOT NULL DEFAULT 0,
    CancelReason  NVARCHAR(500) NULL,
    CancelledAt   DATETIME2     NULL,
    CancelledBy   INT           NULL;
GO

-- Optional FK to Users (mềm — không ép buộc cascade)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SOItem_CancelledBy')
BEGIN
    ALTER TABLE SalesOrderItems
        ADD CONSTRAINT FK_SOItem_CancelledBy
            FOREIGN KEY (CancelledBy) REFERENCES Users(UserId);
END
GO


-- ══════════════════════════════════════════════════════════════════
-- 2. Drop & recreate computed columns — dùng EffectiveQty
-- ══════════════════════════════════════════════════════════════════
-- Cũ: RemainingReceiveQty = Quantity - ReceivedQty
-- Mới: RemainingReceiveQty = (Quantity - CancelledQty) - ReceivedQty
-- (Tương tự cho RemainingDeliverQty — InStockQty giữ nguyên vì
--  InStockQty = ReceivedQty - DeliveredQty không phụ thuộc CancelledQty)

ALTER TABLE SalesOrderItems DROP COLUMN RemainingReceiveQty;
GO
ALTER TABLE SalesOrderItems DROP COLUMN RemainingDeliverQty;
GO

ALTER TABLE SalesOrderItems ADD
    RemainingReceiveQty AS ((Quantity - CancelledQty) - ReceivedQty);
GO

ALTER TABLE SalesOrderItems ADD
    RemainingDeliverQty AS ((Quantity - CancelledQty) - DeliveredQty);
GO


-- ══════════════════════════════════════════════════════════════════
-- 3. VIEW: vw_OrderItemProgress — bổ sung CancelledQty + LineStatus CANCELLED
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

    soi.Quantity                  AS OrderedQty,
    soi.CancelledQty,
    (soi.Quantity - soi.CancelledQty)  AS EffectiveQty,
    soi.ReceivedQty,
    soi.InStockQty,
    soi.DeliveredQty,
    soi.RemainingReceiveQty,
    soi.RemainingDeliverQty,

    CASE WHEN (soi.Quantity - soi.CancelledQty) = 0 THEN 100
         ELSE CAST(soi.ReceivedQty * 100.0 / (soi.Quantity - soi.CancelledQty) AS DECIMAL(5,1))
    END AS ReceiveProgressPct,
    CASE WHEN (soi.Quantity - soi.CancelledQty) = 0 THEN 100
         ELSE CAST(soi.DeliveredQty * 100.0 / (soi.Quantity - soi.CancelledQty) AS DECIMAL(5,1))
    END AS DeliverProgressPct,

    soi.ExpectedReceiveDate,
    soi.ExpectedDeliveryDate,

    -- LineStatus: CANCELLED ưu tiên cao nhất, kế đến mới tới DELIVERED / RECEIVED_FULL / PARTIAL / NOT_STARTED
    CASE
        WHEN soi.CancelledQty >= soi.Quantity                                 THEN 'CANCELLED'
        WHEN soi.DeliveredQty >= (soi.Quantity - soi.CancelledQty)            THEN 'DELIVERED'
        WHEN soi.DeliveredQty > 0                                             THEN 'PARTIAL_DELIVERED'
        WHEN soi.ReceivedQty  >= (soi.Quantity - soi.CancelledQty)            THEN 'RECEIVED_FULL'
        WHEN soi.ReceivedQty  > 0                                             THEN 'PARTIAL'
        ELSE 'NOT_STARTED'
    END AS LineStatus,

    CASE
        WHEN soi.ReceivedQty < (soi.Quantity - soi.CancelledQty)
         AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
        THEN 1 ELSE 0
    END AS IsReceiveOverdue,

    CASE
        WHEN soi.DeliveredQty < (soi.Quantity - soi.CancelledQty)
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
-- 4. VIEW: vw_OrderProgress — dùng EffectiveQty, đếm dòng đã hủy
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
    SUM(soi.Quantity - soi.CancelledQty) AS TotalEffectiveQty,
    SUM(soi.ReceivedQty)     AS TotalReceived,
    SUM(soi.DeliveredQty)    AS TotalDelivered,
    SUM(soi.InStockQty)      AS TotalInStock,

    COUNT(soi.SOItemId)                                                            AS TotalLines,
    SUM(CASE WHEN soi.CancelledQty >= soi.Quantity THEN 1 ELSE 0 END)              AS CancelledLines,
    SUM(CASE WHEN soi.CancelledQty < soi.Quantity AND soi.ReceivedQty = 0 THEN 1 ELSE 0 END) AS NotStartedLines,
    SUM(CASE WHEN soi.CancelledQty < soi.Quantity
              AND soi.ReceivedQty > 0
              AND soi.ReceivedQty < (soi.Quantity - soi.CancelledQty)
             THEN 1 ELSE 0 END)                                                     AS PartialLines,
    SUM(CASE WHEN soi.CancelledQty < soi.Quantity
              AND soi.ReceivedQty >= (soi.Quantity - soi.CancelledQty)
              AND soi.DeliveredQty < (soi.Quantity - soi.CancelledQty)
             THEN 1 ELSE 0 END)                                                     AS ReadyToDeliverLines,
    SUM(CASE WHEN soi.CancelledQty < soi.Quantity
              AND soi.DeliveredQty >= (soi.Quantity - soi.CancelledQty)
             THEN 1 ELSE 0 END)                                                     AS DeliveredLines,

    CASE WHEN SUM(soi.Quantity - soi.CancelledQty) = 0 THEN 0
         ELSE CAST(SUM(soi.ReceivedQty) * 100.0 / SUM(soi.Quantity - soi.CancelledQty) AS DECIMAL(5,1))
    END AS OverallReceivePct,
    CASE WHEN SUM(soi.Quantity - soi.CancelledQty) = 0 THEN 0
         ELSE CAST(SUM(soi.DeliveredQty) * 100.0 / SUM(soi.Quantity - soi.CancelledQty) AS DECIMAL(5,1))
    END AS OverallDeliverPct,

    SUM(CASE WHEN soi.ReceivedQty < (soi.Quantity - soi.CancelledQty)
              AND soi.ExpectedReceiveDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END)                                                     AS OverdueReceiveLines,
    SUM(CASE WHEN soi.DeliveredQty < (soi.Quantity - soi.CancelledQty)
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


PRINT '═══ Migration v2.3 completed ═══';
PRINT 'SOItem +CancelledQty/CancelReason/CancelledAt/CancelledBy';
PRINT 'RemainingReceiveQty/RemainingDeliverQty recompute với EffectiveQty';
PRINT 'vw_OrderItemProgress + vw_OrderProgress refresh';
GO
