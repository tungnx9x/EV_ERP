# Quy Trình Mua Hàng & Quản Lý Đơn Hàng (v1.3)

> Tài liệu mô tả toàn bộ luồng xử lý từ khi nhận yêu cầu từ khách sạn đến khi hoàn tất thanh toán.  
> **Thay đổi chính so với v1.2:** Xóa PO nội bộ — Sales Order kiêm luôn vai trò quản lý mua hàng.

---

## Tổng Quan Luồng Quy Trình

```
Yêu cầu từ KS → RFQ → Quotation → SO (mua + bán + giao) → Nhập kho → Giao hàng → Quyết toán → Báo cáo
```

---

## Chi Tiết Các Bước

### Bước 1 – Nhận Yêu Cầu Từ Khách Sạn

- Bộ phận tiếp nhận nhận yêu cầu mua hàng từ phía khách sạn.
- Tạo **Yêu Cầu Báo Giá (RFQ)** và phân công cho nhân viên mua hàng phụ trách.

| Đối tượng | Trạng thái |
|-----------|-----------|
| RFQ | `Inprogress` |

---

### Bước 2 – Tạo & Gửi Báo Giá (Quotation)

Nhân viên mua hàng thực hiện:

1. **Tạo báo giá** → `Quotation: Draft`
2. **Gửi báo giá cho khách sạn** → `Quotation: Sent`

---

### Bước 3 – Đợi Phản Hồi Từ Khách Sạn

Khách sạn gửi lại PO (mỗi khách sạn có mẫu PO riêng). Nhân viên mua hàng xác nhận và cập nhật trạng thái:

| Tình huống | Trạng thái Quotation | Tiếp theo |
|------------|---------------------|-----------|
| Khách sạn gửi PO xác nhận | `Approved` | → Tạo SO |
| Khách sạn từ chối | `Rejected` | Kết thúc |
| Khách sạn yêu cầu chỉnh sửa | `Amend` | → Tạo Quotation mới (link `AmendFromId`) |
| Không có phản hồi | `Expired` | Kết thúc |

> **Lưu ý:** Luồng tiếp tục khi Quotation đạt trạng thái `Approved`.

---

### Bước 4 – Tạo Sale Order (SO)

- Hệ thống **tự động tạo Sale Order** khi Quotation được duyệt.
- `SO: Draft`

Nhân viên mua hàng thực hiện:
- Nhập **mã PO của khách sạn** (`CustomerPoNo`)
- **Upload file PO** của khách sạn (`CustomerPoFile`)
- Làm **đề nghị tạm ứng** (`AdvanceAmount`)

→ `SO: Wait` *(chờ phê duyệt và nhận tiền tạm ứng)*

---

### Bước 5 – Mua Hàng

Sau khi có phản hồi phê duyệt và nhận được tiền tạm ứng:

- Nhân viên mua hàng tiến hành **mua hàng thực tế** trên chính SO.
- Chọn **nhà cung cấp** (`VendorId`)
- Nhập **giá mua** (`PurchasePrice`) cho từng dòng sản phẩm
- Cập nhật **thời gian nhận hàng dự kiến** (`ExpectedReceiveDate`)

→ `SO: Buying`

> **Khác v1.2:** Không tạo PO nội bộ. Mọi thông tin mua hàng nằm trực tiếp trên SO.

---

### Bước 6 – Nhận Hàng Vào Kho

Khi hàng về kho:

→ `SO: Received`

- Hệ thống **tự động tạo phiếu nhập kho**.
- `StockTransaction IN: Draft`

Nhân viên kho tiến hành nhận hàng, kiểm tra và xếp vào vị trí kho:

→ `StockTransaction IN: Confirmed`

---

### Bước 7 – Giao Hàng Cho Khách

**Tạo phiếu giao hàng:**

- `StockTransaction OUT: Draft`

→ `SO: Delivering`

**Nhân viên kho bàn giao cho đơn vị vận chuyển** (kèm biên bản giao nhận):

- `StockTransaction OUT: Delivering`

**Khách hàng nhận hàng & ký biên bản giao nhận**, nhân viên giao hàng xác nhận:

- `StockTransaction OUT: Delivered`

→ `SO: Delivered`

**Nếu khách hàng không nhận hàng do có vấn đề**, sau đó trả lại hàng:

- `StockTransaction OUT: Delivered`

→ `SO: Returned`

---

### Bước 8 – Hoàn Tất Quyết Toán

Nhân viên mua hàng thực hiện:
- **Hoàn ứng** (nếu chi thực tế < tạm ứng), hoặc
- **Đề nghị thanh toán** (nếu chi thực tế > tạm ứng)

→ `SO: Completed`

---

### Bước 9 – Báo cáo kết quả kinh doanh

Nhân viên mua hàng thực hiện làm báo cáo theo mẫu và gửi cho quản lý

→ `SO: Reported`
→ Hệ thống **tự động chuyển RFQ** sang `Completed`

---

## Sơ Đồ Trạng Thái Tóm Tắt

```
[Yêu cầu KS]
      │
      ▼
  RFQ: Inprogress
      │
      ▼
  Quotation: Draft → Sent
      │
      ├─ Approved ──────────────────────────────────────────┐
      ├─ Rejected (kết thúc)                                │
      ├─ Amend (tạo bản báo giá mới)                        │
      └─ Expired (kết thúc)                                 │
                                                            ▼
                                                      SO: Draft
                                                  (nhập PO KS, tạm ứng)
                                                            │
                                                            ▼
                                                       SO: Wait
                                                            │
                                               (phê duyệt + nhận tạm ứng)
                                                            │
                                                            ▼
                                                      SO: Buying
                                              (chọn NCC, nhập giá mua)
                                                            │
                                                            ▼
                                                    SO: Received
                                            StockTransaction IN: Draft
                                                            │
                                                            ▼
                                          StockTransaction IN: Confirmed
                                                   SO: Delivering
                                           StockTransaction OUT: Draft
                                                            │
                                                            ▼
                                         StockTransaction OUT: Delivering
                                                            │
                                                            ▼
                                          StockTransaction OUT: Delivered
                                                   SO: Delivered 
                                                            │
                                                            ▼
                                                   SO: Completed or Returned
														    │		
                                                            ▼
                                                   SO: Reported
                                                 RFQ: Completed (tự động)



```

---

## Bảng Tổng Hợp Trạng Thái

| Đối tượng | Trạng thái | Mô tả |
|-----------|-----------|-------|
| RFQ | `Inprogress` | Đang xử lý yêu cầu báo giá |
| RFQ | `Completed` | Tự động hoàn tất khi SO Completed |
| Quotation | `Draft` | Báo giá đang được soạn |
| Quotation | `Sent` | Đã gửi cho khách sạn |
| Quotation | `Approved` | Khách sạn xác nhận (có PO) |
| Quotation | `Rejected` | Khách sạn từ chối |
| Quotation | `Amend` | Yêu cầu chỉnh sửa → tạo bản mới |
| Quotation | `Expired` | Hết hạn, không có phản hồi |
| SO | `Draft` | Tạo tự động từ Quotation Approved |
| SO | `Wait` | Chờ phê duyệt tạm ứng |
| SO | `Buying` | Đang mua hàng từ NCC |
| SO | `Received` | Hàng đã về kho |
| SO | `Delivering` | Đang giao cho vận chuyển |
| SO | `Delivered` | KH đã nhận hàng & ký biên bản |
| SO | `Returned` | KH không nhận hàng & hoàn trả do có lỗi |
| SO | `Completed` | Đã quyết toán |
| SO | `Reported` | Hoàn tất, đã báo cáo |
| StockTransaction IN | `Draft` | Phiếu nhập kho tự động tạo |
| StockTransaction IN | `Confirmed` | Nhân viên kho xác nhận nhập |
| StockTransaction OUT | `Draft` | Phiếu xuất kho (giao hàng) |
| StockTransaction OUT | `Delivering` | Đang giao cho vận chuyển |
| StockTransaction OUT | `Delivered` | Khách hàng đã nhận & ký |
