# Kiến trúc OISM

OISM là 5 service .NET 8 sau một API gateway, hai ứng dụng React, một database PostgreSQL với 5 schema và một RabbitMQ. Ranh giới service đi theo ranh giới transaction: đơn hàng và tồn kho phải commit cùng nhau nên nằm chung service `core`.

## Động lực kiến trúc

Xếp theo ưu tiên; khi hai động lực xung đột, cái đứng trên thắng.

| # | Động lực | Nguồn | Chiến thuật |
| --- | --- | --- | --- |
| 1 | Đúng số lượng khi nhiều đơn tranh nhau | NFR-PERF-02, NFR-SEC-02, FR-RSE | Một transaction cho mỗi use case, khóa bi quan trên dòng số dư, CHECK ở database |
| 2 | Truy vết được mọi biến động tồn | FR-INV-01, NFR-SEC-03 | Ledger chỉ thêm mới, trigger chặn sửa và xóa |
| 3 | Không rò dữ liệu giữa các tenant | NFR-TENANT-01 | `TenantId` trên mọi bảng, Global Query Filter, event mang `TenantId` |
| 4 | Giá vốn lịch sử không đổi | FR-COST-02 | Chốt `CostPrice` trên dòng đơn đúng lúc xuất kho |
| 5 | POS phản hồi nhanh | NFR-PERF-01 | `core` giữ bản sao SKU, một lần gọi trả SKU kèm tồn |
| 6 | 3 dev, 5 tuần | Ràng buộc dự án | Số service ít nhất mà vẫn tách được; có đường lui về 3 service |

## Ràng buộc

- Đề chỉ định công nghệ: .NET 8, EF Core, PostgreSQL, SignalR, Hangfire, ReactJS, Docker Compose, GitHub Actions.
- Không kết nối sàn thật, không cổng thanh toán, không bán offline. Lý do nằm ở [decisions/](../decisions/README.md).
- Đề ghi "Clean Architecture" và ".NET 8 Web API", không yêu cầu microservice. Việc tách service là quyết định của nhóm, đã được giảng viên xác nhận ngày 2026-10-04 ([ADR-0001](../decisions/0001-microservices-by-transaction-boundary.md)).

## Tài liệu trong thư mục

| File | Trả lời câu hỏi |
| --- | --- |
| [options-analysis.md](options-analysis.md) | Đã cân nhắc những cách chia nào, vì sao chọn 5 service? |
| [context-and-containers.md](context-and-containers.md) | Ai dùng hệ thống, hệ thống gồm những khối chạy nào, địa chỉ ra sao? |
| [layering.md](layering.md) | Có những tầng và lớp nào, cái gì đặt ở đâu, cái gì bị cấm? |
| [source-tree.md](source-tree.md) | Cây thư mục đích và tài liệu chi phối từng folder |
| [multi-tenancy.md](multi-tenancy.md) | `TenantId` đi từ JWT tới database, event, job thế nào? |
| [messaging.md](messaging.md) | Service nói chuyện với nhau qua event ra sao, lỗi thì thế nào? |
| [transactions-and-concurrency.md](transactions-and-concurrency.md) | Bất biến tồn kho, ranh giới transaction, thứ tự khóa |
| [security.md](security.md) | Xác thực, phân quyền theo vai trò, bí mật, TLS |

Thiết kế chi tiết đi kèm kiến trúc nằm ở [design/](../design/README.md): sơ đồ lớp UML của Domain `core` ([class-diagram.md](../design/class-diagram.md)), ERD từng schema ([data-model/](../design/data-model/core.md)), sơ đồ tuần tự 8 luồng lõi ([flows/](../design/flows/)). Bản đặc tả liền mạch để nộp, kèm sơ đồ use case UML và ERD liên service: [requirements/srs.md](../requirements/srs.md).

## Ba quy tắc tóm gọn cả kiến trúc

1. Thứ gì phải commit cùng nhau thì ở chung một service và một schema.
2. Giữa các service chỉ có event qua outbox; không gọi HTTP chéo, không đọc schema của nhau.
3. Trong một service, phụ thuộc chỉ hướng vào Domain; nghiệp vụ không nằm ở controller hay ở EF Core.

## Kiến trúc được giữ bằng gì

Mỗi quy tắc trên có một chỗ trong code hoặc test giữ nó, để không phải trông vào việc đọc review.

| Quy tắc | Giữ bằng |
| --- | --- |
| Đơn và kho chung một transaction | Test tích hợp T01 đến T13 trên PostgreSQL thật ([testing/scenarios.md](../testing/scenarios.md)); coverage 100% dòng lệnh cho Inventory, Reservation, Costing, CI chặn dưới 80% ([testing/coverage.md](../testing/coverage.md)) |
| Sổ chỉ thêm mới | Trigger ở database chặn UPDATE và DELETE, test T16 |
| Không rò dữ liệu giữa tenant | Global Query Filter, test mỗi `DbContext` chỉ có entity `ITenantOwned`, bộ test cách ly toàn hệ thống T14, T15 ([testing/tenant-isolation.md](../testing/tenant-isolation.md)) |
| Event qua outbox, consumer có inbox | Test T22, T23 ([architecture/messaging.md](messaging.md)) |
| Phân lớp | Project reference một chiều: Domain không tham chiếu project nào ngoài `Oism.SharedKernel`; Application không tham chiếu Infrastructure ([layering.md](layering.md)) |
| Quyền theo vai trò | Fallback policy từ chối mọi endpoint không khai báo policy; Swagger ghi vai trò của từng endpoint và có test cho từng service |

## Vòng đời nền tảng

Đề chỉ định .NET 8, nên mọi service, gateway, seed và test đều nhắm `net8.0`. Theo lịch của Microsoft, .NET 8 (LTS) hết hỗ trợ ngày 2026-11-10, khoảng ba tuần sau ngày bàn giao. Hệ thống bàn giao giữ .NET 8 theo đề; ai vận hành tiếp sau mốc đó nên nâng lên .NET 10 LTS theo các bước ở [handover/deployment-plan.md](../handover/deployment-plan.md) mục "Nâng cấp nền tảng". PostgreSQL 17, RabbitMQ 4 và Node 22 còn trong thời gian hỗ trợ ở ngày bàn giao.
