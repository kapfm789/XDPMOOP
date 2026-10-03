# ADR-0012: Một workspace npm chứa admin, pos và phần dùng chung

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-16
- Ngày: 2026-10-03

## Bối cảnh

Gói công việc 4 yêu cầu ReactJS cho cả trang quản trị và PWA POS. Mục sản phẩm bàn giao liệt kê "Web Admin" và "POS App" như hai thứ riêng trong gói Docker Compose. Đề không nói hai giao diện chung một dự án hay tách hai.

## Quyết định

- Thư mục `frontend/` là một workspace npm gồm ba gói: `admin`, `pos`, `shared`.
- `admin` và `pos` là hai ứng dụng Vite, React, TypeScript, build và đóng gói thành hai image riêng.
- `shared` chứa API client, xác thực, kết nối SignalR và kiểu dữ liệu. `admin` và `pos` tham chiếu `shared`, không tham chiếu lẫn nhau.
- Chỉ `pos` là PWA: có manifest và service worker cache app shell.

## Hệ quả

- Gói demo có đủ hai ứng dụng như đề liệt kê.
- Logic đăng nhập, làm mới token và đọc lỗi viết một lần.
- POS nhẹ vì không kéo theo màn hình quản trị.

## Phương án đã loại

- Một ứng dụng duy nhất, phân màn hình theo vai trò: POS phải tải cả mã của trang quản trị, và gói demo chỉ có một ứng dụng.
- Hai repo hoặc hai dự án độc lập: phải sao chép phần dùng chung.

## Nếu bị đổi

Gộp thành một ứng dụng: chuyển `pos` thành một nhóm route của `admin`, bỏ một Dockerfile; phần `shared` giữ nguyên.
