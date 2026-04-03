# Tài liệu Phân tích Nghiệp vụ ERP
## Doanh nghiệp Thương mại Trung gian — Phục vụ Khách sạn

> **Phiên bản:** 1.0  
> **Loại tài liệu:** Business Requirements Document (BRD)  
> **Trạng thái:** Bản nháp — chờ xác nhận  

---

## 1. Tổng quan doanh nghiệp

### 1.1 Mô hình kinh doanh

Doanh nghiệp hoạt động theo mô hình **thương mại trung gian (Trading Intermediary)**:

```
Khách sạn (Khách hàng)
  → Gửi yêu cầu mua hàng
    → Doanh nghiệp nhận order & báo giá
      → Mua hàng từ Nhà cung cấp
        → Giao hàng cho Khách sạn
```

### 1.2 Đặc điểm nghiệp vụ

- Quy mô: Doanh nghiệp nhỏ (SME)
- Khách hàng chính: Các khách sạn
- Không nhất thiết giữ hàng tồn kho lớn — hỗ trợ cả mô hình **dropshipping** (giao thẳng từ nhà cung cấp đến khách hàng)
- Nhân viên có vai trò chuyên biệt: kinh doanh, thủ kho, kế toán

### 1.3 Phạm vi hệ thống ERP (giai đoạn 1)

Hệ thống tập trung vào 7 module chính + 2 module nền tảng:

| STT | Module | Mức độ ưu tiên |
|-----|--------|----------------|
| 1 | Quản lý Khách hàng | Cao |
| 2 | Quản lý Nhà cung cấp | Cao |
| 3 | Quản lý Sản phẩm | Cao |
| 4 | Báo giá / Sales Order | Cao |
| 5 | Mua hàng / Purchase Order | Cao |
| 6 | Quản lý Kho | Cao |
| 7 | Dashboard & Báo cáo | Cao |
| 8 | Quản lý User & Phân quyền | Cao (nền tảng) |
| 9 | Xuất PDF theo mẫu | Cao (tích hợp vào Báo giá) |

---

## 2. Phân tích chi tiết từng module

---

### 2.1 Quản lý Khách hàng (Customer Management)

**Mục tiêu:** Lưu trữ và tra cứu toàn bộ thông tin khách hàng khách sạn, hỗ trợ chăm sóc và ra quyết định bán hàng.

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Hồ sơ khách hàng | Tên khách sạn, địa chỉ, mã số thuế, người liên hệ, SĐT, email |
| Phân loại khách hàng | Nhóm (VIP, thường, mới) hoặc cấp độ ưu tiên |
| Lịch sử giao dịch | Xem toàn bộ đơn hàng, báo giá đã gửi |
| Ghi chú nội bộ | Ghi chú riêng về khách hàng không hiển thị ra ngoài |
| Quản lý công nợ phải thu | Tổng nợ hiện tại, hạn thanh toán, lịch sử thanh toán |
| Liên kết nhân viên phụ trách | Gán nhân viên kinh doanh cho từng khách hàng |

#### Yêu cầu nghiệp vụ đặc thù

- Một khách sạn có thể có nhiều người liên hệ (bếp trưởng, quản lý mua hàng...) — cần hỗ trợ nhiều contact trên một hồ sơ khách hàng.
- Hỗ trợ tìm kiếm nhanh theo tên, SĐT, mã khách hàng.

---

### 2.2 Quản lý Nhà cung cấp (Vendor Management)

**Mục tiêu:** Quản lý danh sách nhà cung cấp, so sánh và lựa chọn khi cần mua hàng.

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Hồ sơ nhà cung cấp | Tên, mã số thuế, tài khoản ngân hàng, người liên hệ |
| Danh sách sản phẩm cung cấp | Sản phẩm nào, giá bán theo từng NCC |
| Lịch sử mua hàng | Toàn bộ PO đã đặt với NCC này |
| Đánh giá nhà cung cấp | Thời gian giao hàng trung bình, chất lượng, tỉ lệ đúng hàng |
| Quản lý công nợ phải trả | Số tiền đang nợ NCC, hạn thanh toán |

---

### 2.3 Quản lý Sản phẩm (Product Management)

**Mục tiêu:** Quản lý danh mục hàng hóa phục vụ cả báo giá lẫn mua hàng.

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Thông tin cơ bản | Mã sản phẩm, tên, mô tả, hình ảnh, đơn vị tính |
| Phân loại | Danh mục, nhóm sản phẩm |
| Mã barcode | Gen mã, quét mã |
| Giá bán | Theo từng khách hàng hoặc nhóm khách hàng |
| Giá mua | Theo từng nhà cung cấp (có thể khác nhau) |
| Tồn kho tối thiểu | Ngưỡng cảnh báo khi tồn kho xuống thấp |

#### Yêu cầu nghiệp vụ đặc thù

- Một sản phẩm có thể mua từ nhiều nhà cung cấp khác nhau với giá khác nhau — hệ thống cần lưu được bảng giá theo NCC.
- Một sản phẩm có thể bán với giá khác nhau cho từng khách hàng/nhóm khách hàng.

---

### 2.4 Báo giá / Sales Order

**Mục tiêu:** Tiếp nhận yêu cầu từ khách sạn, tạo báo giá, chuyển thành đơn bán hàng khi được xác nhận.

#### Luồng nghiệp vụ

```
Yêu cầu từ khách sạn
  → Tạo báo giá (chọn sản phẩm, số lượng, giá)
    → Xuất PDF theo mẫu
      → Gửi email cho khách hàng
        → Khách hàng xác nhận
          → Chuyển thành Sales Order (SO)
            → Kích hoạt lệnh Mua hàng (PO)
```

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Tạo báo giá | Chọn khách hàng, thêm sản phẩm từ danh mục |
| Áp dụng chiết khấu | Chiết khấu theo dòng hoặc toàn đơn |
| Xuất PDF theo mẫu | Xem mục 3.1 — yêu cầu đặc thù |
| Gửi email tự động | Đính kèm PDF, ghi lại lịch sử gửi |
| Theo dõi trạng thái | Nháp → Đã gửi → Xác nhận → Hủy |
| Chuyển sang SO | Chuyển đổi báo giá thành đơn bán hàng khi KH đồng ý |
| Liên kết PO | Tự động hoặc thủ công tạo lệnh mua hàng từ SO |

#### Trạng thái vòng đời báo giá

```
[Nháp] → [Đã gửi] → [Xác nhận] → [Đã tạo SO]
                   ↘ [Hủy]
```

---

### 2.5 Mua hàng / Purchase Order (PO)

**Mục tiêu:** Tạo lệnh mua hàng từ nhà cung cấp sau khi có đơn bán hàng xác nhận.

#### Luồng nghiệp vụ

```
Sales Order xác nhận
  → Tạo PO (chọn NCC, sản phẩm, số lượng, giá)
    → Gửi PO cho nhà cung cấp
      → Nhà cung cấp giao hàng
        → Xác nhận nhận hàng (toàn bộ hoặc một phần)
          → Cập nhật tồn kho
            → Ghi nhận hóa đơn NCC
```

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Tạo PO thủ công hoặc từ SO | Linh hoạt tạo mới hoặc generate từ đơn bán hàng |
| So sánh giá NCC | Xem giá của nhiều NCC trước khi chọn |
| Theo dõi trạng thái PO | Đặt hàng → Đang giao → Nhận một phần → Đã nhận |
| Xác nhận nhận hàng | Nhập số lượng thực nhận (có thể khác số đặt) |
| Liên kết hóa đơn NCC | Ghi nhận invoice, số tiền, hạn thanh toán |
| Hỗ trợ Dropshipping | Đánh dấu PO giao thẳng đến địa chỉ khách hàng |

#### Trạng thái vòng đời PO

```
[Nháp] → [Đã gửi NCC] → [Nhận một phần] → [Đã nhận đủ]
                        ↘ [Hủy]
```

---

### 2.6 Quản lý Kho (Inventory Management)

**Mục tiêu:** Theo dõi hàng hóa từ lúc nhập từ nhà cung cấp đến khi xuất giao cho khách sạn.

#### Đặc điểm mô hình kho

Hệ thống cần hỗ trợ cả hai mô hình:
- **Kho thực:** Hàng về kho trước, sau đó xuất giao cho khách hàng
- **Kho ảo / Dropshipping:** Hàng giao thẳng từ NCC đến khách hàng, kho chỉ ghi nhận luồng ảo

#### Chức năng cần có

| Chức năng | Mô tả |
|-----------|-------|
| Nhập kho | Liên kết từ PO, nhập số lượng thực nhận |
| Xuất kho | Liên kết từ SO/Delivery Order |
| Tồn kho thời gian thực | Xem số lượng hiện tại theo từng sản phẩm |
| Lịch sử nhập xuất | Audit trail toàn bộ giao dịch kho |
| Cảnh báo tồn kho thấp | Thông báo khi xuống dưới mức tối thiểu |
| Kiểm kê | Đối chiếu tồn kho thực tế và hệ thống |
| Quét barcode | Xem mục 3.2 — yêu cầu đặc thù |

---

### 2.7 Dashboard & Báo cáo

**Mục tiêu:** Cung cấp cái nhìn tổng quan để ra quyết định nhanh và theo dõi hiệu suất.

#### Dashboard tổng quan (Admin / Quản lý)

- Doanh thu theo ngày/tuần/tháng/quý
- Số đơn hàng theo trạng thái (đang xử lý, đã hoàn thành, bị hủy)
- Công nợ phải thu và phải trả
- Top khách hàng theo doanh số
- Top sản phẩm bán chạy
- Tình trạng tồn kho (các mặt hàng sắp hết)

#### Báo cáo kết quả kinh doanh cá nhân (xem mục 3.3)

#### Các báo cáo nghiệp vụ khác

| Báo cáo | Mô tả |
|---------|-------|
| Báo cáo doanh thu | Theo kỳ, theo khách hàng, theo sản phẩm |
| Báo cáo mua hàng | Chi phí theo kỳ, theo nhà cung cấp |
| Báo cáo tồn kho | Tồn kho hiện tại, biến động trong kỳ |
| Báo cáo công nợ | Phải thu (AR), phải trả (AP), danh sách quá hạn |
| Báo cáo hiệu suất NCC | Tỉ lệ giao đúng hạn, chất lượng |

---

## 3. Yêu cầu đặc thù

---

### 3.1 Xuất báo giá PDF theo mẫu

#### Yêu cầu nghiệp vụ

Hệ thống **phải** xuất báo giá PDF theo đúng format mẫu sẵn có của doanh nghiệp (logo, font, bố cục, màu sắc). Không được dùng layout PDF mặc định của hệ thống.

#### Giải pháp kỹ thuật đề xuất

**Template Engine dạng HTML/CSS:**

1. Lấy file mẫu gốc của doanh nghiệp (Word hoặc PDF)
2. Đội kỹ thuật convert sang **HTML/CSS template** — thực hiện **1 lần duy nhất**
3. Trong template, các vị trí dữ liệu động được đánh dấu bằng placeholder tag:
   - `{{ten_khach_hang}}`, `{{dia_chi_khach_hang}}`
   - `{{so_bao_gia}}`, `{{ngay_tao}}`, `{{ngay_het_han}}`
   - `{{danh_sach_san_pham}}` (vòng lặp dòng sản phẩm)
   - `{{tong_tien_truoc_vat}}`, `{{vat}}`, `{{tong_tien}}`
   - `{{ghi_chu}}`, `{{dieu_khoan_thanh_toan}}`
4. Khi xuất, hệ thống render HTML → PDF bằng Puppeteer hoặc wkhtmltopdf

#### Luồng xuất PDF

```
Tạo báo giá (nhập dữ liệu)
  → Chọn mẫu PDF từ kho template
    → Hệ thống tự động điền dữ liệu vào đúng vị trí
      → Preview trên màn hình trước khi xuất
        → Xác nhận → Xuất file PDF
          → Tự động đính kèm vào email gửi khách hàng
            → Lưu bản PDF vào lịch sử giao dịch
```

#### Yêu cầu bổ sung về template

- Hỗ trợ **nhiều mẫu template** (ví dụ: mẫu tiếng Việt, mẫu tiếng Anh, mẫu theo loại dịch vụ)
- Giao diện quản lý kho template cho Admin
- Cho phép xem trước (preview) template trước khi áp dụng

---

### 3.2 Quét barcode trong quản lý kho

#### Yêu cầu nghiệp vụ

Hệ thống phải hỗ trợ **2 loại thiết bị quét barcode** đồng thời, cùng một luồng xử lý:

#### A. Camera điện thoại (Web-based scanner)

| Hạng mục | Mô tả |
|----------|-------|
| Công nghệ | Thư viện JavaScript: ZXing-js hoặc QuaggaJS |
| Cách hoạt động | Nhân viên mở trình duyệt trên điện thoại → bấm nút "Quét" → camera bật → quét mã → dữ liệu điền tự động |
| Ưu điểm | Không cần cài app, hoạt động trên mọi điện thoại có camera và trình duyệt |
| Yêu cầu | Giao diện web phải responsive (tương thích mobile) |

#### B. Máy quét barcode chuyên dụng (USB/Bluetooth)

| Hạng mục | Mô tả |
|----------|-------|
| Cơ chế hoạt động | Máy quét USB/Bluetooth hoạt động như bàn phím — gõ chuỗi mã + Enter vào ô input |
| Xử lý phía frontend | Input field luôn trong trạng thái "lắng nghe"; khi phát hiện chuỗi kết thúc bằng Enter trong vòng < 100ms → xử lý như barcode (phân biệt với người gõ tay thông thường) |
| Yêu cầu | Không cần driver hay cài đặt thêm — plug and play |

#### Các màn hình tích hợp quét barcode

- Nhập kho (xác nhận hàng về từ PO)
- Xuất kho (xác nhận hàng đi theo SO/Delivery)
- Kiểm kê (đếm tồn kho thực tế)
- Tìm kiếm sản phẩm nhanh

#### Yêu cầu dữ liệu

Mỗi sản phẩm trong module Quản lý Sản phẩm cần có trường lưu **mã barcode** (hỗ trợ: EAN-8, EAN-13, QR Code, Code 128).

---

### 3.3 Báo cáo kết quả kinh doanh cá nhân

#### Yêu cầu nghiệp vụ

Module báo cáo cần phân tầng quyền xem rõ ràng:

| Vai trò | Quyền xem báo cáo |
|---------|-------------------|
| Admin / Quản lý | Xem báo cáo của tất cả nhân viên + so sánh |
| Nhân viên kinh doanh | Chỉ xem báo cáo của bản thân |
| Kế toán | Chỉ xem báo cáo tài chính của bản thân (nếu áp dụng) |

#### Các chỉ số cần có (cho từng nhân viên)

| Chỉ số | Mô tả |
|--------|-------|
| Doanh số theo kỳ | Lọc theo ngày / tuần / tháng / quý / năm |
| Số báo giá tạo ra | Tổng báo giá trong kỳ |
| Tỉ lệ chuyển đổi | Số báo giá → đơn hàng xác nhận / tổng báo giá |
| Top khách hàng | Khách hàng mang lại doanh số cao nhất |
| Top sản phẩm | Sản phẩm bán nhiều nhất |
| Đơn hàng đang xử lý | Danh sách các SO chưa hoàn thành |

#### Màn hình so sánh nhân viên (chỉ Admin / Quản lý)

- Bảng xếp hạng doanh số theo nhân viên trong kỳ
- Biểu đồ so sánh hiệu suất theo tháng
- Xuất báo cáo ra Excel / PDF

---

## 4. Quản lý User & Phân quyền (RBAC)

### 4.1 Kiến trúc phân quyền

Hệ thống sử dụng mô hình **RBAC (Role-Based Access Control)**:

```
User (Tài khoản)
  → được gán 1 Role (Vai trò)
    → Role có tập hợp Permissions (Quyền)
      → Permissions áp dụng trên từng Module
```

> **Lý do chọn RBAC:** Khi thêm nhân viên mới, chỉ cần gán role — không cần cấu hình quyền lại từ đầu.

### 4.2 Danh sách vai trò (Roles)

| Role | Mô tả |
|------|-------|
| Admin | Toàn quyền hệ thống, quản lý user |
| Quản lý | Xem và quản lý toàn bộ nghiệp vụ, không quản lý user |
| Kinh doanh | Tạo báo giá, theo dõi đơn hàng của mình |
| Thủ kho | Quản lý nhập xuất kho, xem PO |
| Kế toán | Quản lý công nợ, hóa đơn, xem báo cáo tài chính |

### 4.3 Ma trận phân quyền

| Module | Admin | Quản lý | Kinh doanh | Thủ kho | Kế toán |
|--------|-------|---------|------------|---------|---------|
| Dashboard | Toàn quyền | Toàn quyền | Của mình | Chỉ xem | Chỉ xem |
| Khách hàng | Toàn quyền | Toàn quyền | Xem & sửa | Chỉ xem | Chỉ xem |
| Nhà cung cấp | Toàn quyền | Toàn quyền | Chỉ xem | Chỉ xem | Xem & sửa |
| Sản phẩm | Toàn quyền | Toàn quyền | Chỉ xem | Chỉ xem | Chỉ xem |
| Báo giá / Sales | Toàn quyền | Toàn quyền | Toàn quyền | — | Chỉ xem |
| Mua hàng / PO | Toàn quyền | Toàn quyền | Chỉ xem | Xem & sửa | Toàn quyền |
| Quản lý kho | Toàn quyền | Toàn quyền | Chỉ xem | Toàn quyền | Chỉ xem |
| Báo cáo cá nhân | Toàn quyền | Toàn quyền | Của mình | — | Của mình |
| Quản lý user | Toàn quyền | — | — | — | — |

> **Ghi chú:**  
> - `Toàn quyền` = Xem + Tạo + Sửa + Xóa  
> - `Xem & sửa` = Xem + Tạo + Sửa (không xóa)  
> - `Chỉ xem` = Chỉ đọc dữ liệu  
> - `Của mình` = Chỉ xem/thao tác dữ liệu do chính mình tạo  
> - `—` = Không có quyền truy cập  

### 4.4 Chức năng quản lý user

| Chức năng | Mô tả |
|-----------|-------|
| Tạo / sửa / khóa tài khoản | Quản lý vòng đời tài khoản nhân viên |
| Gán Role | Gán một vai trò cho mỗi tài khoản |
| Đặt lại mật khẩu | Admin có thể reset mật khẩu cho user |
| Xác thực đăng nhập | Email + mật khẩu (bcrypt hash) |
| Session timeout | Tự động đăng xuất sau thời gian không hoạt động |
| Log lịch sử đăng nhập | Ghi nhận thời gian, IP, thiết bị |
| 2FA (tùy chọn giai đoạn sau) | Xác thực 2 bước qua OTP |

---

## 5. Lộ trình triển khai đề xuất

### Giai đoạn 1 — Core Data (Tuần 1–3)

Mục tiêu: Có đủ dữ liệu nền để chạy nghiệp vụ.

- [ ] Quản lý User & Phân quyền (RBAC)
- [ ] Quản lý Sản phẩm (bao gồm trường barcode)
- [ ] Quản lý Khách hàng
- [ ] Quản lý Nhà cung cấp

### Giai đoạn 2 — Nghiệp vụ chính (Tuần 4–7)

Mục tiêu: Vận hành được luồng chính từ báo giá đến giao hàng.

- [ ] Báo giá / Sales Order
- [ ] Xuất PDF theo mẫu (template engine)
- [ ] Mua hàng / Purchase Order
- [ ] Quản lý Kho (nhập/xuất + barcode scanner)

### Giai đoạn 3 — Insights & Tối ưu (Tuần 8–10)

Mục tiêu: Hỗ trợ ra quyết định và quản lý hiệu suất.

- [ ] Dashboard tổng quan
- [ ] Báo cáo kết quả kinh doanh cá nhân
- [ ] Báo cáo công nợ (AR/AP)
- [ ] Cảnh báo tồn kho thấp
- [ ] Xuất báo cáo Excel/PDF

---

## 6. Tổng hợp yêu cầu kỹ thuật

| Hạng mục | Yêu cầu | Độ phức tạp |
|----------|---------|-------------|
| PDF theo mẫu | Template HTML/CSS + Puppeteer hoặc wkhtmltopdf | Cao |
| Barcode camera | ZXing-js hoặc QuaggaJS (chạy trên browser) | Trung bình |
| Barcode máy quét | Input listener phân biệt scanner vs gõ tay (< 100ms) | Thấp |
| Báo cáo cá nhân | Filter dữ liệu theo `user_id`, phân tầng quyền xem | Trung bình |
| RBAC | Mô hình User → Role → Permission trên từng module | Trung bình |
| Giao diện responsive | Mobile-friendly cho màn hình quét kho | Trung bình |
| Gửi email tự động | SMTP integration, đính kèm PDF | Thấp |

---

## 7. Các điểm cần xác nhận với stakeholder

- [ ] Có bao nhiêu mẫu báo giá PDF cần hỗ trợ? Cung cấp file mẫu gốc.
- [ ] Danh sách đầy đủ các loại barcode đang dùng (EAN-13, QR, Code128...)?
- [ ] Cần bao nhiêu tài khoản user dự kiến ban đầu?
- [ ] Cần tích hợp phần mềm kế toán bên ngoài (MISA, Fast...) không?
- [ ] Dữ liệu khách hàng và sản phẩm hiện có ở đâu? (Excel, phần mềm cũ...) — cần migration không?
- [ ] Ma trận phân quyền đề xuất (mục 4.3) có cần điều chỉnh không?
- [ ] Có yêu cầu về ngôn ngữ giao diện (Tiếng Việt / Tiếng Anh)?

---

*Tài liệu này được tạo dựa trên buổi phân tích nghiệp vụ với doanh nghiệp. Vui lòng xác nhận và bổ sung trước khi bàn giao sang giai đoạn thiết kế kỹ thuật.*
