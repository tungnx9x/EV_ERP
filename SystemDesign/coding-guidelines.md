# Các Lưu Ý Khi Code

> Đọc file này trước khi bắt đầu code bất kỳ task nào.

---

## 1. Luôn đảm bảo Responsive cho Mobile

Khi code giao diện (UI/View), **bắt buộc** phải đảm bảo responsive trên các thiết bị mobile.

- Sử dụng đơn vị linh hoạt: `%`, `rem`, `em`, `vw`, `vh` thay vì `px` cố định cho layout.
- Các breakpoint tham khảo:
  - `< 480px` — điện thoại nhỏ
  - `480px – 768px` — điện thoại lớn / tablet dọc
  - `768px – 1024px` — tablet ngang
  - `> 1024px` — desktop
- Kiểm tra giao diện trên ít nhất 3 kích thước màn hình trước khi hoàn thành.
- Không để nội dung bị tràn ngang (horizontal scroll) trên mobile.
- Các nút bấm và vùng tương tác phải đủ lớn (tối thiểu `44x44px`) để dễ thao tác trên màn hình cảm ứng.
- Sử dụng Flexbox hoặc CSS Grid thay vì float hay position cố định cho layout.

---

## 2. Luôn đảm bảo Performance

Nên:
- Luôn sử dụng async cho các thao tác I/O (database, HTTP, file)
- Luôn sử dụng projection khi query database (chỉ lấy các field cần thiết)
- Luôn tránh N+1 query
- Ưu tiên sử dụng caching nếu dữ liệu được dùng lại
- Ưu tiên giảm thiểu việc cấp phát bộ nhớ (allocation)
Không nên:
- Không sử dụng .Result hoặc .Wait()
- Không load toàn bộ entity nếu không cần thiết
- AKhông gọi ToList() quá sớm
- Không tự tạo mới HttpClient (new HttpClient)
Chú ý:
- Dữ liệu Products, Quotation rất lớn và tăng trưởng nhanh

---

