# frontend/shared — kế hoạch folder

Phần dùng chung của hai ứng dụng React: API client, đăng nhập, kiểu dữ liệu, kết nối SignalR. Owner: Dev C. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Thuộc tầng nào

Tầng giao diện, lớp dưới cùng (mục 4.2 của kế hoạch tổng). `admin` và `pos` tham chiếu `shared`; `shared` không tham chiếu hai ứng dụng.

## Cấu trúc

```text
shared/
└─ src/
   ├─ api/         client gọi gateway, tự gắn JWT, tự refresh, đọc ProblemDetails
   ├─ auth/        AuthProvider, lưu token, chặn route theo vai trò
   ├─ realtime/    kết nối SignalR, hook nhận thông báo
   └─ types/       kiểu dữ liệu theo Swagger của từng service
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-09 | frontend: npm workspaces, API client, đăng ký tenant, login và refresh, layout, chặn route theo vai trò | C | FR-AUTH-01, FR-AUTH-02, FR-AUTH-03 | Đăng ký tenant rồi đăng nhập được trên admin và pos |
| 7 | W4-08 | admin và pos: nhận thông báo SignalR (âm thanh, popup); PWA manifest và service worker | C | FR-SIM-02, FR-POS-01 | Đơn mới hiện không cần tải lại; POS cài được lên màn hình chính |

## Quy tắc riêng

- Mọi lời gọi API đi qua `api/`; không ứng dụng nào tự gọi `fetch` tới gateway.
- Gặp 401 thì thử refresh một lần rồi mới đưa người dùng về trang đăng nhập.
- Gặp 409 do event trễ (SKU hoặc chi nhánh vừa tạo chưa tới `core`) thì tự thử lại vài lần trước khi báo lỗi.
