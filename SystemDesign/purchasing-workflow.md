# Quy Trình Mua Hàng & Quản Lý Đơn Hàng

> Tài liệu mô tả toàn bộ luồng xử lý từ khi nhận yêu cầu từ khách sạn đến khi hoàn tất thanh toán.

---

## Tổng Quan Luồng Quy Trình

```
Yêu cầu từ KS → RFQ → Quotation → SO → PO nội bộ → Nhập kho → Giao hàng → Hoàn tất
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

| Tình huống | Trạng thái Quotation |
|------------|----------------------|
| Khách sạn gửi PO xác nhận | `Approved` |
| Khách sạn từ chối | `Rejected` |
| Khách sạn yêu cầu chỉnh sửa | `Amend` |
| Không có phản hồi | `Expired` |

> **Lưu ý:** Luồng tiếp tục khi Quotation đạt trạng thái `Approved`.

---

### Bước 4 – Tạo Sale Order (SO)

- Hệ thống **tự động tạo Sale Order** khi Quotation được duyệt.
- `SO: Draft`

Nhân viên mua hàng thực hiện:
- Nhập **mã PO của khách sạn**
- **Upload file PO** của khách sạn
- Làm **đề nghị tạm ứng**

→ `SO: Wait` *(chờ phê duyệt và nhận tiền tạm ứng)*

---

### Bước 5 – Thực Hiện Mua Hàng

Sau khi có phản hồi phê duyệt và nhận được tiền tạm ứng:

- Nhân viên mua hàng tiến hành **mua hàng thực tế**.
- `SO: Processing`
- Hệ thống **tự động tạo Purchase Order nội bộ**.

| Đối tượng | Trạng thái |
|-----------|-----------|
| PO nội bộ | `Draft` |

---

### Bước 6 – Theo Dõi Đơn Hàng Mua

Nhân viên mua hàng cập nhật:
- Thông tin chi tiết về đơn hàng
- **Thời gian nhận hàng dự kiến**

→ `PO nội bộ: Buying`

Khi hàng về kho:

→ `PO nội bộ: Received`

---

### Bước 7 – Nhập Kho

- Hệ thống **tự động tạo phiếu nhập kho**.
- `StockTransaction IN: Draft`

Nhân viên kho tiến hành nhận hàng, kiểm tra và xếp vào kho:

→ `StockTransaction IN: Confirmed`

---

### Bước 8 – Giao Hàng Cho Khách

**Tạo phiếu giao hàng:**

- `StockTransaction OUT: Draft`

**Nhân viên kho bàn giao cho đơn vị vận chuyển** (kèm biên bản giao nhận):

- `StockTransaction OUT: Delivering`

**Khách hàng nhận hàng & ký biên bản giao nhận**, nhân viên giao hàng xác nhận:

- `StockTransaction OUT: Delivered`
- Hệ thống **tự động chuyển SO** từ `Processing` → `Delivered`

---

### Bước 9 – Hoàn Tất Thanh Toán

Nhân viên mua hàng thực hiện:
- **Hoàn ứng** (nếu chi thực tế < tạm ứng), hoặc
- **Đề nghị thanh toán** (nếu chi thực tế > tạm ứng)

→ `SO: Completed`
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
      ├─ Amend (quay lại chỉnh sửa)                         │
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
                                                   SO: Processing
                                                  PO nội bộ: Draft
                                                            │
                                                            ▼
                                                  PO nội bộ: Buying
                                                            │
                                                            ▼
                                                  PO nội bộ: Received
                                              StockTransaction IN: Draft
                                                            │
                                                            ▼
                                            StockTransaction IN: Confirmed
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
                                                    SO: Completed
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
| Quotation | `Amend` | Yêu cầu chỉnh sửa |
| Quotation | `Expired` | Hết hạn, không có phản hồi |
| SO | `Draft` | Tạo tự động sau khi duyệt |
| SO | `Wait` | Chờ phê duyệt tạm ứng |
| SO | `Processing` | Đang thực hiện mua hàng |
| SO | `Delivered` | Đã giao hàng xong |
| SO | `Completed` | Hoàn tất, đã quyết toán |
| PO nội bộ | `Draft` | Tạo tự động khi SO Processing |
| PO nội bộ | `Buying` | Đang đặt/theo dõi đơn hàng |
| PO nội bộ | `Received` | Hàng đã về kho |
| StockTransaction IN | `Draft` | Phiếu nhập kho tự động tạo |
| StockTransaction IN | `Confirmed` | Nhân viên kho xác nhận nhập |
| StockTransaction OUT | `Draft` | Phiếu xuất kho (giao hàng) |
| StockTransaction OUT | `Delivering` | Đang giao cho vận chuyển |
| StockTransaction OUT | `Delivered` | Khách hàng đã nhận & ký |
