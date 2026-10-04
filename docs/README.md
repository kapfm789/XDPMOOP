# Tài liệu OISM

Thư mục `docs/` là nguồn chuẩn cho mọi quyết định về OISM. Code phải khớp tài liệu; khi hai bên lệch nhau thì sửa cho khớp ngay trong cùng PR, không để lệch.

## Bản đồ

| Thư mục | Trả lời câu hỏi | Bắt đầu từ |
| --- | --- | --- |
| [requirements/](requirements/) | Đề yêu cầu gì, mang mã nào? | [functional.md](requirements/functional.md) |
| [usecase-userstory/](usecase-userstory/) | Ai làm gì với hệ thống, thế nào là đạt? | [README.md](usecase-userstory/README.md) |
| [architecture/](architecture/) | Hệ thống chia thế nào, vì sao, code đặt ở đâu? | [README.md](architecture/README.md) |
| [decisions/](decisions/) | Những điểm đề chưa chốt được quyết ra sao? | [README.md](decisions/README.md) |
| [design/](design/) | Bảng, API, event, trạng thái, luồng cụ thể ra sao? | [README.md](design/README.md) |
| [testing/](testing/) | Kiểm thử gì, viết ở đâu, thế nào là qua? | [strategy.md](testing/strategy.md) |
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

- Đây là bản thiết kế v0, viết ngày 2026-10-03. Khi code thật khác tài liệu, người sửa code cập nhật tài liệu trong cùng PR.
- Đã có code: skeleton backend (W1-02), Compose dev (W1-03), outbox và inbox (W1-13), danh mục và thương hiệu ở `catalog` (W1-06), workspace frontend với đăng ký, đăng nhập và layout (W1-09), workflow CI (W1-11).
- Đã có của phase 2: đăng ký tenant, đăng nhập, làm mới, đăng xuất và quản lý người dùng ở `identity` (W1-04); chi nhánh và `BranchUpserted` (W1-05); sản phẩm, SKU, mã vạch, giá và `SkuUpserted` ở `catalog` (W1-07); consumer dựng `sku_refs`, `branch_refs` ở `core` (W1-08); màn hình chi nhánh, người dùng, danh mục, thương hiệu trên `admin` (W1-10); ERD đã rà theo migration và test cách ly tenant của `identity`, `catalog` (W1-12).
- Chưa có: kho, đơn hàng và giữ hàng ở `core`; nghiệp vụ của `channel`, `insights`; màn hình sản phẩm trên `admin`; seed.
- Mọi quyết định trong [decisions/](decisions/) đã được giảng viên xác nhận ngày 2026-10-04 (task W1-01), đúng như nhóm đề xuất.
- Quy tắc cho công cụ AI nằm ở [AGENTS.md](../AGENTS.md) tại gốc repo; rule và skill riêng của Claude Code nằm trong `.claude/`.
