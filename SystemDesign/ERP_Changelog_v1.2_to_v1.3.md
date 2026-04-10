# ERP Thương Mại Trung Gian — Changelog v1.3

> **Ngày cập nhật:** 2026-04-10  
> **Thay đổi chính:** Xóa PO nội bộ, gộp toàn bộ luồng mua hàng vào Sales Order  
> **Lý do:** Mô hình trung gian mua thẳng từ PO khách hàng — không cần tạo PO nội bộ riêng, giảm nhập liệu không cần thiết

---

## 1. Quy Trình Mới (Đơn Giản Hóa)

### 1.1 So sánh luồng cũ vs mới

**v1.2 (cũ):**
```
RFQ → Quotation → SO → PO nội bộ → Nhập kho → Giao hàng → Quyết toán
                       ↑ thêm 1 bước nhập liệu
```

**v1.3 (mới):**
```
RFQ → Quotation → SO (gộp mua + bán + giao) → Nhập kho → Giao hàng → Quyết toán
```

Bỏ hoàn toàn bước tạo PO nội bộ. NV mua hàng thao tác trên cùng một SO từ đầu đến cuối.

### 1.2 Luồng trạng thái SO mới

```
  Quotation: APPROVED
        │
        ▼
  SO: DRAFT ─────── NV nhập mã PO KH, upload file PO
        │
        ▼
  SO: WAIT ──────── Đề nghị tạm ứng, chờ phê duyệt + nhận tiền
        │
        ▼
  SO: BUYING ────── Chọn NCC, nhập giá mua, theo dõi đơn hàng
        │
        ▼
  SO: RECEIVED ──── Hàng về kho → tự động tạo StockTx IN
        │
        ▼
  SO: DELIVERING ── Giao vận chuyển → tự động tạo StockTx OUT
        │
        ▼
  SO: DELIVERED ─── KH nhận hàng & ký biên bản
        │
        ▼
  SO: COMPLETED ─── Quyết toán (hoàn ứng hoặc thanh toán bổ sung)
        │               → RFQ tự động chuyển COMPLETED
        │
   (bất kỳ bước nào)
        └── CANCELLED
```

### 1.3 Chi tiết từng bước

| Bước | SO Status | Hành động NV | Hệ thống tự động |
|------|-----------|-------------|-------------------|
| 1 | `DRAFT` | Nhập `CustomerPoNo`, upload file PO, tạo đề nghị tạm ứng | Tạo SO từ Quotation APPROVED |
| 2 | `WAIT` | Chờ | — |
| 3 | `BUYING` | Chọn `VendorId`, nhập `PurchasePrice` cho từng dòng SP, cập nhật `ExpectedReceiveDate` | Chuyển status khi tạm ứng được duyệt + nhận tiền |
| 4 | `RECEIVED` | Thủ kho kiểm tra, xếp vào vị trí kho | Tạo StockTx IN (DRAFT → CONFIRMED) |
| 5 | `DELIVERING` | Bàn giao cho đơn vị vận chuyển, kèm biên bản | Tạo StockTx OUT (DRAFT → DELIVERING) |
| 6 | `DELIVERED` | NV giao hàng xác nhận KH đã nhận & ký | StockTx OUT → DELIVERED |
| 7 | `COMPLETED` | Hoàn ứng hoặc đề nghị thanh toán bổ sung | RFQ → COMPLETED |

---

## 2. Thay Đổi Database

### 2.1 Tổng quan

| Hạng mục | v1.2 | v1.3 | Thay đổi |
|----------|------|------|----------|
| Tổng bảng | 44 | **42** | -2 (xóa PO + POItems) |
| Tổng views | 6 | **6** | Không đổi (sửa nội dung vw_AccountsPayable) |

### 2.2 Bảng bị xóa

| Bảng | Lý do |
|------|-------|
| `PurchaseOrders` | Gộp vào SalesOrders — SO kiêm vai trò PO nội bộ |
| `PurchaseOrderItems` | Thay bằng `PurchasePrice` + `LineCost` trên SalesOrderItems |

### 2.3 Bảng SalesOrders — Trường mới

#### Nhóm: Thông tin mua hàng (thay thế PO nội bộ)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `VendorId` | INT, FK → Vendors | NCC mua hàng |
| `ExpectedReceiveDate` | DATE | Dự kiến hàng về kho |
| `BuyingNotes` | NVARCHAR(MAX) | Ghi chú quá trình mua |
| `BuyingAt` | DATETIME2 | Thời điểm bắt đầu mua |
| `ReceivedAt` | DATETIME2 | Thời điểm hàng về kho |

#### Nhóm: Chi phí mua (giá vốn)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `PurchaseCost` | DECIMAL(18,2) | Tổng chi phí mua từ NCC |
| `ProfitAmount` | COMPUTED | `TotalAmount - ISNULL(PurchaseCost, 0)` — lợi nhuận |

#### Nhóm: Dropshipping

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `IsDropship` | BIT | Giao thẳng từ NCC đến KH |
| `DropshipAddress` | NVARCHAR(500) | Địa chỉ giao nếu dropship |

#### Nhóm: Timestamps bổ sung

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `DeliveringAt` | DATETIME2 | Thời điểm giao cho vận chuyển |

#### Status mới

```
v1.2: DRAFT → WAIT → PROCESSING → DELIVERED → COMPLETED → CANCELLED
v1.3: DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED → CANCELLED
                      ▲          ▲           ▲
                      │          │           └── mới (tách từ PROCESSING)
                      │          └────────────── mới (tách từ PROCESSING)
                      └───────────────────────── đổi tên (PROCESSING → BUYING)
```

### 2.4 Bảng SalesOrderItems — Trường mới

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `PurchasePrice` | DECIMAL(18,2) | Giá mua từ NCC (giá vốn dòng) |
| `LineCost` | DECIMAL(18,2) | Chi phí dòng = Quantity × PurchasePrice |

Mỗi dòng SO giờ có đầy đủ: **giá bán** (`UnitPrice`, `LineTotal`) + **giá mua** (`PurchasePrice`, `LineCost`) → tính lợi nhuận trên từng sản phẩm.

### 2.5 Bảng VendorInvoices — Đổi FK

| Trường | v1.2 | v1.3 |
|--------|------|------|
| FK chính | `PurchaseOrderId → PurchaseOrders` | `SalesOrderId → SalesOrders` |

Hóa đơn NCC giờ gắn trực tiếp vào SO thay vì PO.

### 2.6 Bảng VendorPayments — Đổi FK

| Trường | v1.2 | v1.3 |
|--------|------|------|
| FK chính | `PurchaseOrderId → PurchaseOrders` | `SalesOrderId → SalesOrders` |

Thanh toán cho NCC giờ gắn trực tiếp vào SO thay vì PO.

### 2.7 Bảng StockTransactions — Bỏ FK

| Trường | v1.2 | v1.3 |
|--------|------|------|
| `PurchaseOrderId` | FK → PurchaseOrders | **Đã xóa** |
| `SalesOrderId` | FK → SalesOrders | Giữ nguyên — là nguồn duy nhất cho cả IN lẫn OUT |

### 2.8 Bảng PdfTemplates — Bỏ giá trị enum

```sql
-- v1.2
CHECK (TemplateType IN ('QUOTATION','SALES_ORDER','PURCHASE_ORDER','DELIVERY_NOTE'))

-- v1.3
CHECK (TemplateType IN ('QUOTATION','SALES_ORDER','DELIVERY_NOTE'))
-- Bỏ PURCHASE_ORDER vì không còn PO
```

### 2.9 View vw_AccountsPayable — Sửa nguồn dữ liệu

```
v1.2: Tính từ SUM(PurchaseOrders.TotalAmount)
v1.3: Tính từ SUM(VendorInvoices.TotalAmount)
```

### 2.10 Indexes

| Thay đổi | Chi tiết |
|----------|---------|
| **Xóa** | `IX_PO_No`, `IX_PO_VendorId`, `IX_PO_Status`, `IX_PO_SOId`, `IX_PO_OrderDate`, `IX_POItems_POId` |
| **Thêm** | `IX_SO_VendorId` — tìm SO theo NCC |
| **Thêm** | `IX_VInvoices_SOId` — tìm hóa đơn NCC theo SO |
| **Thêm** | `IX_VPayments_SOId` — tìm thanh toán NCC theo SO |
| **Bỏ** | `IX_VInvoices_VendorId` → chuyển vào nhóm VendorInvoices mới |

---

## 3. Danh Sách Bảng v1.3 (42 bảng)

| # | Module | Bảng |
|---|--------|------|
| 1 | Auth | `Modules` |
| 2 | Auth | `Roles` |
| 3 | Auth | `Permissions` |
| 4 | Auth | `RolePermissions` |
| 5 | Auth | `Users` |
| 6 | Auth | `LoginHistory` |
| 7 | Auth | `UserSessions` |
| 8 | Khách hàng | `CustomerGroups` |
| 9 | Khách hàng | `Customers` |
| 10 | Khách hàng | `CustomerContacts` |
| 11 | Khách hàng | `CustomerNotes` |
| 12 | NCC | `Vendors` |
| 13 | NCC | `VendorContacts` |
| 14 | Sản phẩm | `ProductCategories` |
| 15 | Sản phẩm | `Units` |
| 16 | Sản phẩm | `Products` |
| 17 | Sản phẩm | `ProductImages` |
| 18 | Sản phẩm | `VendorPrices` |
| 19 | Sản phẩm | `CustomerPrices` |
| 20 | Đơn hàng | `RFQs` |
| 21 | Đơn hàng | `Quotations` |
| 22 | Đơn hàng | `QuotationItems` |
| 23 | Đơn hàng | `QuotationEmailHistory` |
| 24 | Đơn hàng | `SalesOrders` |
| 25 | Đơn hàng | `SalesOrderItems` |
| 26 | Đơn hàng | `VendorInvoices` |
| 27 | Kho | `Warehouses` |
| 28 | Kho | `WarehouseLocations` |
| 29 | Kho | `Inventory` |
| 30 | Kho | `StockTransactions` |
| 31 | Kho | `StockTransactionItems` |
| 32 | Kho | `StockChecks` |
| 33 | Kho | `StockCheckItems` |
| 34 | Công nợ | `CustomerPayments` |
| 35 | Công nợ | `VendorPayments` |
| 36 | Công nợ | `AdvanceRequests` |
| 37 | Template | `PdfTemplates` |
| 38 | Template | `TemplateAssignments` |
| 39 | Template | `GeneratedPdfs` |
| 40 | Hệ thống | `AuditLogs` |
| 41 | Hệ thống | `Notifications` |
| 42 | Hệ thống | `Attachments` |
