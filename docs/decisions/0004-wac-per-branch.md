# ADR-0004: Giá vốn bình quân tính theo từng chi nhánh và SKU

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-04
- Ngày: 2026-10-03

## Bối cảnh

Tồn kho quản lý theo chi nhánh, nhưng công thức của FR-COST-01 chỉ nói "giá vốn SKU" và "tồn cũ". Đề không nói tồn cũ là tồn của một chi nhánh hay của cả tenant, cũng không nói hàng đang giữ và hàng đang vận chuyển có vào mẫu số hay không.

## Quyết định

- Giá vốn bình quân (`avg_cost`) lưu trên dòng số dư, tức theo từng tenant, chi nhánh, SKU.
- Công thức khi nhập mua: `avg_cost mới = (on_hand × avg_cost + SL nhập × đơn giá nhập) / (on_hand + SL nhập)`.
- "Tồn cũ" là `on_hand` của chi nhánh nhận hàng, gồm cả phần đang giữ, vì giữ hàng không làm hàng rời kho.
- Chuyển kho: nơi gửi xuất theo `avg_cost` của mình; nơi nhận coi đó là đơn giá nhập và tính lại bình quân bằng cùng công thức.
- Kiểm kê điều chỉnh số lượng, giữ nguyên `avg_cost`.
- Đơn giá nhập là giá trên phiếu, chưa gồm thuế, chiết khấu hay phí vận chuyển.
- Tiền lưu `numeric(18,4)`, làm tròn 4 chữ số thập phân, nửa lên.

## Hệ quả

- Khóa một dòng số dư là đủ để vừa đổi tồn vừa đổi giá vốn; không cần khóa thêm dòng nào.
- Cùng một SKU có thể có giá vốn khác nhau giữa các chi nhánh.
- Giá trị tồn của tenant là tổng `on_hand × avg_cost` trên các chi nhánh, cộng hàng đang vận chuyển theo đơn giá ghi trên phiếu chuyển kho.
- Khi `on_hand` bằng 0, giá vốn mới bằng đúng đơn giá nhập.

## Phương án đã loại

- Giá vốn theo tenant và SKU: mỗi lần nhập phải khóa thêm một dòng giá vốn chung và cộng tồn mọi chi nhánh; chuyển kho không đổi giá vốn nhưng mọi luồng khác phức tạp hơn.

## Nếu bị đổi

Sang giá vốn theo tenant: tách bảng `sku_costs`, thêm bước khóa trong thứ tự khóa, sửa W2-02, W3-05 và công thức giá trị tồn ở `insights`.
