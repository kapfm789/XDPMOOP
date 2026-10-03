# frontend/admin — kế hoạch folder

Trang quản trị cho Owner và Staff: chi nhánh, sản phẩm, kho, đơn hàng, báo cáo, đề xuất nhập hàng. Owner: Dev C; Dev A nhận hai màn hình chuyển kho và kiểm kê ở phase 8. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
admin/
└─ src/
   ├─ pages/       route và màn hình: branches, catalog, inventory, orders, reports, forecast
   ├─ features/    logic theo nghiệp vụ: form phiếu nhập, bảng ledger, duyệt đơn, bộ lọc báo cáo
   └─ app/         layout, menu theo vai trò, router
```

`pages` gọi `features`, `features` gọi `frontend/shared`. Không có logic nghiệp vụ nào quyết định ở giao diện; backend là nơi quyết định.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 2 | W1-10 | admin: màn hình chi nhánh, người dùng, danh mục, thương hiệu | C | FR-AUTH-03, FR-AUTH-04, FR-PROD-01 | CRUD từ giao diện |
| 3 | W2-07 | admin: sản phẩm, SKU, mã vạch, giá | C | FR-PROD-02..04 | Tạo sản phẩm có biến thể, mã vạch, giá lẻ và sỉ từ giao diện |
| 4 | W2-08 | admin: phiếu nhập, tồn theo chi nhánh, xem ledger | C | FR-INV-01, FR-INV-02 | Nhập hàng từ giao diện, thấy dòng ledger và giá vốn mới |
| 6 | W3-08 | admin: danh sách đơn đa kênh, tạo đơn thủ công, duyệt, hủy, in phiếu giao | C | FR-ORD-01, FR-ORD-03 | Duyệt được đơn từ simulator trên giao diện |
| 7 | W4-08 | admin và pos: nhận thông báo SignalR (âm thanh, popup); PWA manifest và service worker | C | FR-SIM-02, FR-POS-01 | Đơn mới hiện không cần tải lại; POS cài được lên màn hình chính |
| 8 | W4-06 | admin: màn hình chuyển kho, kiểm kê | A | FR-INV-03, FR-INV-04 | Thao tác trọn luồng từ giao diện |
| 8 | W4-07 | admin: dashboard báo cáo, cảnh báo tồn, đề xuất nhập hàng | C | FR-REP-01..03, FR-AI-02 | Lọc đủ thời gian, chi nhánh, kênh, SKU |

## Quy tắc riêng

- Ẩn báo cáo lợi nhuận và giá vốn với Staff (D-15); backend vẫn kiểm quyền.
- Màn hình ledger chỉ xem, không có nút sửa hay xóa.
- Thông báo đơn mới và cảnh báo tồn nhận qua SignalR, không tải lại trang (FR-SIM-02).
