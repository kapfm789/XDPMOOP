# Phân tích phương án kiến trúc

Nhóm chọn phương án B: 5 service và một gateway, trong đó đơn hàng và tồn kho chung service `core`. Đây là cách chia nhiều service nhất mà vẫn giữ được yêu cầu "một transaction" của đề, với chi phí ước lượng khoảng 22 trên 75 ngày công.

## 1. Tám vùng nghiệp vụ ứng viên

Bước đầu là liệt kê mọi vùng có thể thành một service, chưa xét chi phí.

| # | Vùng | Yêu cầu | Dữ liệu sở hữu | Đặc điểm tải |
| --- | --- | --- | --- | --- |
| 1 | Identity và tenant | FR-AUTH | Tenant, User, Branch | Ít ghi, mọi request cần token |
| 2 | Catalog | FR-PROD | Danh mục, sản phẩm, SKU, mã vạch, giá | Đọc nhiều, ghi ít |
| 3 | Inventory | FR-INV, FR-COST-01, FR-RSE-01 | Ledger, số dư, phiếu nhập, chuyển kho, kiểm kê | Ghi nhiều, cần khóa dòng |
| 4 | Orders | FR-ORD, FR-RSE-02, FR-RSE-03, FR-COST-02, FR-POS-04 | Đơn, dòng đơn, phần giữ hàng, thanh toán | Ghi nhiều, tranh chấp cao |
| 5 | Channel ingestion | FR-SIM-01, adapter của FR-ORD-01 | Webhook đã nhận | Tải giật khi flash sale |
| 6 | Notification | FR-SIM-02 | Không có | Kết nối WebSocket giữ lâu |
| 7 | Reporting | FR-REP | Bảng đọc dựng từ event | Truy vấn nặng, trễ được |
| 8 | Forecast | FR-AI | Kết quả dự báo | Chạy theo lô ban đêm |

## 2. Mức ghép giữa các vùng

Hai vùng chỉ tách được thành hai service khi chúng chịu được việc không commit cùng nhau.

| Cặp | Mức ghép | Lý do |
| --- | --- | --- |
| Inventory và Orders | Phải chung transaction | NFR-SEC-02 đòi tạo đơn, giữ, trừ trong một transaction; FR-POS-04 đòi checkout nguyên tử; FR-COST-02 đòi đọc giá vốn dưới cùng khóa với lúc trừ tồn |
| Catalog tới Inventory và Orders | Dữ liệu tham chiếu, trễ được | Đơn chỉ cần mã, tên, giá của SKU; SKU mới tới trễ một nhịp không sai nghiệp vụ |
| Identity tới mọi vùng | Token tự kiểm, chi nhánh trễ được | JWT kiểm bằng khóa công khai, không cần gọi identity |
| Channel tới Orders | Một chiều, bất đồng bộ được | Sàn không chờ kết quả giữ hàng trong lời gọi webhook |
| Orders và Inventory tới Reporting, Forecast, Notification | Một chiều, trễ được | Ba vùng sau chỉ đọc |
| Reporting và Forecast | Cùng nguồn dữ liệu bán | Gộp được mà không mất gì |
| Notification và Reporting | Cùng là bên nhận event của `core` | Gộp được; tách khi cần scale WebSocket |

Kết luận của bước này: chỉ có một cặp không tách được là Inventory và Orders. Mọi cặp còn lại nối được bằng event.

## 3. Bốn phương án

| Tiêu chí | A. Modular monolith | B-gọn. 3 service | B. 5 service | C. 8 service |
| --- | --- | --- | --- | --- |
| Thành phần backend | Một API | identity, core (gộp catalog, channel), insights, gateway | identity, catalog, core, channel, insights, gateway | Mỗi vùng một service, gateway |
| Giữ đúng chữ NFR-SEC-02 | Có | Có | Có | Không: Orders và Inventory phải dùng saga |
| Chi phí phân tán (ước lượng) | 0 ngày công | Khoảng 16 | Khoảng 22 | Khoảng 36 |
| Tỷ lệ trên quỹ 75 ngày công | 0% | 21% | 29% | 48% |
| Cô lập tải flash sale | Không | Không | Có: `channel` hứng webhook, đệm qua hàng đợi | Có |
| Mỗi dev sở hữu trọn service | Không | Một phần | Có | Có, nhưng 3 dev giữ 8 service |
| Rủi ro trễ lõi kho | Thấp nhất | Thấp | Trung bình | Cao |

## 4. Cách tính chi phí phân tán

Các con số là ước lượng của nhóm, không phải số đo.

| Khoản | Ngày công | Áp cho |
| --- | --- | --- |
| Gateway YARP, định tuyến, kiểm JWT | 1,5 | B-gọn, B, C |
| RabbitMQ, outbox, inbox, hợp đồng event | 2,5 | B-gọn, B, C |
| Thư viện dùng chung: tenant, JWT, lỗi chuẩn, log | 2 | B-gọn, B, C |
| Compose nhiều database, health check | 1 | B-gọn, B, C |
| CI nhiều project | 1 | B-gọn, B, C |
| Mỗi service thêm ngoài service đầu tiên | 1,5 mỗi service | 2 service ở B-gọn, 4 ở B, 7 ở C |
| Điểm tích hợp bằng event | 1 đến 2,5 mỗi điểm | Tổng 5 ở B-gọn, 8 ở B, 17,5 ở C |
| Saga giữa Orders và Inventory, kèm bù trừ và đối soát | 6 | Chỉ C, đã tính trong 17,5 |

Tổng: B-gọn 8 + 3 + 5 = 16; B 8 + 6 + 8 = 22; C 8 + 10,5 + 17,5 = 36.

## 5. Quyết định

Chọn B. Lý do theo thứ tự:

1. B là phương án nhiều service nhất mà chưa phải bỏ transaction chung giữa đơn và kho.
2. `channel` tách riêng cho thấy lợi ích thật của microservice: webhook giật tải không chiếm tài nguyên của `core`.
3. Ba dev chia được ranh giới sở hữu rõ: A giữ identity và kho, B giữ catalog, đơn, channel, C giữ frontend.

Cái giá chấp nhận: nhóm việc ưu tiên P2 chỉ làm mức tối thiểu ([PLAN.md](../PLAN.md) mục 11), và SKU hoặc chi nhánh vừa tạo tới `core` trễ một nhịp event.

## 6. Đường lui

| Dấu hiệu | Hành động | Công lấy lại |
| --- | --- | --- |
| Cuối phase 4 chưa qua cổng | Gộp `catalog` và `channel` vào `core`, thành B-gọn | Khoảng 6 ngày |
| Giảng viên không nhận microservice | Gộp cả 5 service thành một API, bỏ gateway và RabbitMQ, thành A | Khoảng 22 ngày |

Đường lui khả thi vì `core` đã chia module và mỗi service đã theo cùng một khuôn bốn lớp: gộp là chuyển project, không viết lại nghiệp vụ.

## 7. Khi nào xem lại quyết định này

- Cần scale riêng kết nối WebSocket: tách notification khỏi `insights`.
- Báo cáo cần mô hình dự báo nặng hơn hoặc viết bằng ngôn ngữ khác: tách forecast khỏi `insights`.
- Không bao giờ tách Orders khỏi Inventory khi NFR-SEC-02 còn hiệu lực.
