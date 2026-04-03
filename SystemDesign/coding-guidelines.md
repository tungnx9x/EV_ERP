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

