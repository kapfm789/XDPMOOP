# Tài liệu OISM

Thư mục `docs/` là nguồn chuẩn cho mọi quyết định về OISM. Code phải khớp tài liệu; khi hai bên lệch nhau thì sửa cho khớp ngay trong cùng PR, không để lệch.

## Bản đồ

| Thư mục | Trả lời câu hỏi | Bắt đầu từ |
| --- | --- | --- |
| [requirements/](requirements/) | Đề yêu cầu gì, mang mã nào? | [functional.md](requirements/functional.md); bản đặc tả liền mạch kèm sơ đồ use case và ERD: [srs.md](requirements/srs.md) |
| [usecase-userstory/](usecase-userstory/) | Ai làm gì với hệ thống, thế nào là đạt? | [README.md](usecase-userstory/README.md) |
| [architecture/](architecture/) | Hệ thống chia thế nào, vì sao, code đặt ở đâu? | [README.md](architecture/README.md) |
| [decisions/](decisions/) | Những điểm đề chưa chốt được quyết ra sao? | [README.md](decisions/README.md) |
| [design/](design/) | Bảng, API, event, trạng thái, luồng cụ thể ra sao? | [README.md](design/README.md) |
| [testing/](testing/) | Kiểm thử gì, viết ở đâu, thế nào là qua? | [strategy.md](testing/strategy.md); kết quả ở [test-report.md](testing/test-report.md) |
| [handover/](handover/) | Cài, dùng, vận hành, trình diễn hệ thống thế nào? | [README.md](handover/README.md) |
| [conventions/](conventions/) | Viết code, commit, làm việc với AI theo kiểu nào? | [backend.md](conventions/backend.md) |
| [plans/](plans/) và [PLAN.md](PLAN.md) | Ai làm, phase nào, xong khi nào? | [PLAN.md](PLAN.md) |

## Thứ tự đọc cho một task

1. Tìm mã task ở [PLAN.md](PLAN.md) mục 10: phase, owner, mã yêu cầu, điều kiện xong.
2. Mở plan của folder sẽ sửa trong [plans/](plans/).
3. Đọc use case mang mã yêu cầu đó trong [usecase-userstory/](usecase-userstory/): tiêu chí chấp nhận là đích phải đạt.
4. Đọc thiết kế của phần sẽ sửa trong [design/](design/): data model, API, luồng, event.
5. Kiểm ADR liên quan trong [decisions/](decisions/) và các bất biến ở [architecture/transactions-and-concurrency.md](architecture/transactions-and-concurrency.md).
6. Viết code đúng lớp theo [architecture/source-tree.md](architecture/source-tree.md) và [conventions/](conventions/).
7. Viết test theo [testing/](testing/).

## Khi tài liệu mâu thuẫn

Áp theo thứ tự, tài liệu đứng trên thắng:

1. Đề bài gốc (Phần I của [file phân tích](../OISM-ban-dich-va-phan-tich-tieng-Viet.md)) và [decisions/](decisions/), nơi ghi cách nhóm hiểu những điểm đề chưa chốt.
2. [requirements/](requirements/) và [usecase-userstory/](usecase-userstory/): cái phải làm.
3. [architecture/](architecture/) và [design/](design/): cách làm.
4. [PLAN.md](PLAN.md) và [plans/](plans/): bản tóm tắt và lịch. Nếu lệch với các mục trên thì sửa PLAN.

Thấy mâu thuẫn thì dừng lại, nêu rõ hai chỗ lệch nhau và sửa tài liệu trước khi viết code.

## Từ source tree tới tài liệu

| Sắp sửa | Đọc trước |
| --- | --- |
| `backend/shared/**` | [plans/backend-shared.md](plans/backend-shared.md), [architecture/multi-tenancy.md](architecture/multi-tenancy.md), [architecture/messaging.md](architecture/messaging.md) |
| `backend/gateway/**` | [plans/backend-gateway.md](plans/backend-gateway.md), [architecture/security.md](architecture/security.md) |
| `backend/services/identity/**` | [plans/backend-services-identity.md](plans/backend-services-identity.md), [usecase-userstory/auth.md](usecase-userstory/auth.md), [design/data-model/identity.md](design/data-model/identity.md), [design/api/identity.md](design/api/identity.md) |
| `backend/services/catalog/**` | [plans/backend-services-catalog.md](plans/backend-services-catalog.md), [usecase-userstory/catalog.md](usecase-userstory/catalog.md), [design/data-model/catalog.md](design/data-model/catalog.md), [design/api/catalog.md](design/api/catalog.md) |
| `backend/services/core/**` | [plans/backend-services-core.md](plans/backend-services-core.md), [architecture/transactions-and-concurrency.md](architecture/transactions-and-concurrency.md), [design/flows/](design/flows/), [design/data-model/core.md](design/data-model/core.md), [design/api/core.md](design/api/core.md) |
| `backend/services/channel/**` | [plans/backend-services-channel.md](plans/backend-services-channel.md), [design/flows/webhook-ingestion.md](design/flows/webhook-ingestion.md), [design/api/channel.md](design/api/channel.md) |
| `backend/services/insights/**` | [plans/backend-services-insights.md](plans/backend-services-insights.md), [usecase-userstory/reports.md](usecase-userstory/reports.md), [design/api/insights.md](design/api/insights.md), [design/forecast.md](design/forecast.md) |
| `frontend/**` | [plans/frontend-admin.md](plans/frontend-admin.md), [plans/frontend-pos.md](plans/frontend-pos.md), [design/ui/](design/ui/), [conventions/frontend.md](conventions/frontend.md) |
| `deploy/**`, `.github/**` | [plans/deploy.md](plans/deploy.md) |
| `tools/k6/**` | [plans/tools-k6.md](plans/tools-k6.md), [testing/strategy.md](testing/strategy.md) |
| Bất kỳ event nào | [design/events.md](design/events.md) |

## Trạng thái

- Bản thiết kế đầu tiên viết ngày 2026-10-03 và đã được rà theo code qua từng phase; cập nhật gần nhất ngày 2026-10-18. Khi code thật khác tài liệu, người sửa code cập nhật tài liệu trong cùng PR.
- Đã có của phase 1: skeleton backend (W1-02), Compose dev (W1-03), outbox và inbox (W1-13), danh mục và thương hiệu ở `catalog` (W1-06), workspace frontend với đăng ký, đăng nhập và layout (W1-09), workflow CI (W1-11).
- Đã có của phase 2: đăng ký tenant, đăng nhập, làm mới, đăng xuất và quản lý người dùng ở `identity` (W1-04); chi nhánh và `BranchUpserted` (W1-05); sản phẩm, SKU, mã vạch, giá và `SkuUpserted` ở `catalog` (W1-07); consumer dựng `sku_refs`, `branch_refs` ở `core` (W1-08); màn hình chi nhánh, người dùng, danh mục, thương hiệu trên `admin` (W1-10); ERD đã rà theo migration và test cách ly tenant của `identity`, `catalog` (W1-12).
- Đã có của phase 3: sổ kho chỉ thêm mới, số dư, `PostLedger` và API xem tồn, xem sổ ở `core` (W2-01); mô hình đơn, máy trạng thái và API tạo đơn thủ công ở `core` (W2-04); màn hình sản phẩm, SKU, mã vạch, giá trên `admin` (W2-07).
- Đã có của phase 4: nhà cung cấp, phiếu nhập nháp rồi xác nhận và giá vốn bình quân ở `core` (W2-02); test `EXPLAIN` cho chỉ mục của sổ và đơn (W2-03); engine giữ hàng, `IStockService` và consumer `SubmitOrder` (W2-05); outbox của `core` với `OrderReserved`, `OrderRejected`, `StockChanged` (W2-06); API đặt ngưỡng tồn ở `core` và màn hình tồn, sổ giao dịch, phiếu nhập trên `admin` (W2-08); sơ đồ luồng nhập hàng và giữ hàng đã rà theo code (W2-09).
- Đã có của phase 5: duyệt, hoàn tất và hủy đơn với `OrderConfirmed`, `OrderCancelled` ở `core` (W3-01); POS checkout một transaction với `Idempotency-Key` và API tìm SKU kèm tồn khả dụng (W3-02); phiếu chuyển kho hai bước mang giá vốn, kèm API xem danh sách và xóa phiếu Draft (W3-05); API xem danh sách và chi tiết đơn (W3-01); màn hình bán hàng và hóa đơn in qua trình duyệt trên `pos` (W3-07).
- Đã có của phase 6: phiên kiểm kê với bút toán điều chỉnh, chặn khi số đếm thấp hơn lượng đang giữ và API xem danh sách phiên ở `core` (W3-06); cấu hình shop, nhận webhook ba sàn, chống trùng, adapter về Canonical Order, phát `SubmitOrder`, nhận `OrderReserved` và `OrderRejected`, công cụ bắn tải ở `channel` (W3-03); job Hangfire hủy đơn Reserved hết hạn ở `core` (W3-04); màn hình danh sách đơn đa kênh, tạo đơn thủ công, chi tiết đơn với duyệt, hoàn tất, hủy và in phiếu giao, cùng màn hình kênh sàn (shop, bắn đơn giả lập, nhật ký webhook) trên `admin` (W3-08); sơ đồ luồng POS checkout, xác nhận đơn, hủy và hết hạn, chuyển kho đã rà theo code (W3-09).
- Đã có của phase 7: consumer `OrderConfirmed`, `StockChanged`, `SkuUpserted` dựng `sales_facts`, `stock_snapshots`, `sku_names` ở `insights`, phát lại không cộng trùng (W4-01); API báo cáo doanh thu, giá vốn, lợi nhuận gộp lọc bốn chiều và job Hangfire dựng `daily_aggregates` (W4-02); hub `/hubs/notifications` với group theo tenant và chi nhánh, đẩy `OrderCreated` và `LowStock` sau khi consumer commit (W4-04); popup kèm âm thanh trên `admin` và `pos`, băng báo mất mạng, manifest và service worker của `pos` (W4-08).
- Đã có của phase 8: API giá trị tồn theo giá vốn, bán chạy, bán chậm và cảnh báo tồn ở `insights` (W4-03); dự báo san bằng hàm mũ, bảng `forecast_runs` và `forecast_results`, job dự báo đêm, API chạy dự báo và xem đề xuất nhập, backtest WAPE (W4-05); màn hình chuyển kho và kiểm kê trên `admin` (W4-06); trang tổng quan với cảnh báo tồn và đơn chờ duyệt, báo cáo lợi nhuận gộp lọc bốn chiều, báo cáo tồn và màn hình đề xuất nhập hàng trên `admin` (W4-07); seed data ở `deploy/seed` chạy lặp lại được, với 60 ngày lịch sử bán phát thẳng `OrderConfirmed` cho `insights` (W4-09). Kết quả backtest trên seed: [testing/forecast-backtest.md](testing/forecast-backtest.md).
- Đã có của phase 10: Swagger của từng service ghi vai trò và yêu cầu Bearer, gộp ở `/swagger` của gateway, cùng Postman collection gọi trọn API theo tài liệu (W5-06, [design/api/postman/](design/api/postman/README.md)); SRS 1.2 là bản chốt URS, RTM ở [PLAN.md](PLAN.md) và SRS đủ 44 mã, sơ đồ lớp UML của Domain `core` ([design/class-diagram.md](design/class-diagram.md)), ERD đã đối chiếu với migration của 5 service, tài liệu kiến trúc thêm phần giữ kiến trúc bằng test và vòng đời .NET 8 (W5-07); tài liệu kiểm thử ([testing/test-report.md](testing/test-report.md)), hướng dẫn cài đặt, hướng dẫn sử dụng, kế hoạch triển khai và vận hành ([handover/](handover/README.md)) (W5-08); kịch bản demo kèm collection chạy tự động, đã chạy hai lần liên tiếp không lỗi trên gói demo (W5-09, [handover/demo-script.md](handover/demo-script.md)); giao diện `admin` theo mục "Bố cục và phong cách" của [design/ui/admin.md](design/ui/admin.md) (W5-10). Giao diện `pos` cùng phong cách với `admin`: thanh trên có logo, tên cửa hàng, chi nhánh, trạng thái mạng và thu ngân; kết quả tìm dạng lưới ô; giỏ và thanh toán dạng thẻ ([design/ui/pos.md](design/ui/pos.md) mục "Bố cục và phong cách", W5-11); đã kiểm luồng quét rồi Enter trên desktop, tablet, điện thoại, chưa chạy lại kịch bản demo. Tài khoản nhận chuyển khoản theo chi nhánh, mã VietQR và phiếu thanh toán tạm tính trên `pos` (W5-12): đã kiểm trên Compose dev, chưa quét thử bằng app ngân hàng với tài khoản thật. Quét mã vạch bằng camera và bằng ảnh trên `pos` (W5-13): đã kiểm quét bằng ảnh và bằng luồng video giả trên Compose dev, file `.wasm` qua nginx của gói demo; chưa quét bằng camera thật. Hình mã vạch tải về dạng PNG cho từng SKU, nút tạo mã một chạm và tìm sản phẩm bằng ảnh mã vạch trên `admin`, ô tìm sản phẩm của `catalog` khớp cả mã vạch (W5-14): đã kiểm trên Compose dev, PNG của EAN-13 và Code128 đọc lại được ở admin và POS; chưa in thử ra giấy để quét bằng máy quét thật. Điện thoại làm máy quét cho màn hình bán hàng trên máy tính qua hub SignalR, hai máy đăng nhập cùng tài khoản (W5-15): đã kiểm qua gateway trên Compose dev với một phiên đóng vai điện thoại; chưa kiểm hai thiết bị thật cùng lúc.
- Đã có của phase 9: báo cáo coverlet cho ba module Inventory, Reservation, Costing của `core`, đều 100% dòng lệnh, và script chặn dưới 80% ở `tools/coverage` (W5-01, [testing/coverage.md](testing/coverage.md)); bộ test cách ly tenant toàn hệ thống với bảng đối chiếu mọi endpoint, event, job, hub (W5-02, [testing/tenant-isolation.md](testing/tenant-isolation.md)); đo tải k6 trên gói demo, đạt cả ba ngưỡng p95 và flash sale (W5-03, [testing/load-test.md](testing/load-test.md)); gói Compose demo `deploy/docker-compose.demo.yml` build image thật, tự sinh khóa JWT và chứng chỉ TLS, gateway HTTPS 8443 chỉ nhận TLS 1.3, seed tự chạy (W5-04, [deploy/README.md](../deploy/README.md)); CI chạy test kèm coverage và build mọi image của gói demo (W5-05).
- Chỗ chưa kiểm được: kịch bản T20 mới chạy trên máy dev đã có sẵn image nền, chưa chạy trên một máy thật sự sạch, nên điều kiện "người ngoài nhóm cài được theo hướng dẫn" của W5-08 cần một người ngoài nhóm thử; giao diện mới chưa qua buổi kiểm tay của cả nhóm; ghi đơn ở mức bão hòa (41 đơn mỗi giây trên 6 SKU, không nghỉ) có p95 khoảng 800 ms, vượt ngưỡng 500 ms (chi tiết ở [testing/load-test.md](testing/load-test.md)).
- Mọi quyết định trong [decisions/](decisions/) đã được giảng viên xác nhận ngày 2026-10-04 (task W1-01), đúng như nhóm đề xuất.
- Quy tắc cho công cụ AI nằm ở [AGENTS.md](../AGENTS.md) tại gốc repo; rule và skill riêng của Claude Code nằm trong `.claude/`.
