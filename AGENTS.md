# OISM: hướng dẫn cho công cụ AI

OISM là hệ thống quản lý bán hàng và tồn kho đa kênh, multi-tenant: 5 service .NET 8 sau một API gateway, hai ứng dụng React, PostgreSQL và RabbitMQ. Đây là đồ án của 3 dev trong 5 tuần, chia 10 phase.

## Trạng thái repo

Repo hiện chỉ có tài liệu trong `docs/`. Code chưa tồn tại. Khi tạo file code, đặt đúng đường dẫn và tên ở `docs/architecture/source-tree.md`; không tự nghĩ ra cấu trúc khác.

## Nguyên tắc: tài liệu trước, code sau

1. `docs/` là nguồn chuẩn. Code phải khớp tài liệu.
2. Không tự thêm yêu cầu, endpoint, bảng, trường hay event mà tài liệu không có. Cần thêm thì sửa tài liệu trước, trong cùng thay đổi.
3. Tài liệu thiếu hoặc mâu thuẫn: dừng lại, nêu rõ chỗ thiếu hoặc hai chỗ lệch nhau, đề xuất cách sửa. Không đoán rồi viết tiếp.
4. Quyết định trong `docs/decisions/` là ràng buộc. Không làm khác đi vì thấy cách khác hay hơn; muốn đổi thì đề xuất ADR mới.

## Quy trình cho mọi việc viết code

1. Tìm mã task ở `docs/PLAN.md` mục 10: phase, owner, mã yêu cầu, cột "Xong khi".
2. Đọc plan của folder sẽ sửa ở `docs/plans/`.
3. Đọc use case mang mã yêu cầu đó ở `docs/usecase-userstory/`. Tiêu chí chấp nhận là đích.
4. Đọc thiết kế của phần sẽ sửa ở `docs/design/`: data model, API, luồng, event, trạng thái.
5. Viết test cho tiêu chí chấp nhận, rồi viết code đúng lớp: Domain, Application, Infrastructure, Api.
6. Chạy build và test. Không báo "xong" khi chưa chạy; không chạy được thì nói rõ vì sao.
7. Bảng, API hoặc event thay đổi thì sửa `docs/design/` trong cùng thay đổi.
8. Báo cáo theo mục "Báo cáo sau khi làm".

Quy trình chi tiết từng bước: `.claude/skills/implement-task/SKILL.md`.

## Bản đồ tài liệu

| Cần biết | Đọc |
| --- | --- |
| Bản đồ toàn bộ tài liệu | `docs/README.md` |
| Ai làm gì, phase nào, xong khi nào | `docs/PLAN.md`, `docs/plans/` |
| Đề yêu cầu gì | `docs/requirements/` |
| Hành vi mong muốn và tiêu chí chấp nhận | `docs/usecase-userstory/` |
| Chia service, phân lớp, source tree | `docs/architecture/` |
| Lý do của các lựa chọn | `docs/decisions/` |
| Bảng, API, event, trạng thái, luồng | `docs/design/` |
| Cách viết và đặt test | `docs/testing/` |
| Quy ước code, git | `docs/conventions/` |

## Từ folder tới tài liệu

| Sắp sửa | Đọc trước |
| --- | --- |
| `backend/shared/**` | `docs/plans/backend-shared.md`, `docs/architecture/multi-tenancy.md`, `docs/architecture/messaging.md` |
| `backend/gateway/**` | `docs/plans/backend-gateway.md`, `docs/architecture/security.md` |
| `backend/services/identity/**` | `docs/plans/backend-services-identity.md`, `docs/usecase-userstory/auth.md`, `docs/design/data-model/identity.md`, `docs/design/api/identity.md` |
| `backend/services/catalog/**` | `docs/plans/backend-services-catalog.md`, `docs/usecase-userstory/catalog.md`, `docs/design/data-model/catalog.md`, `docs/design/api/catalog.md` |
| `backend/services/core/**` | `docs/plans/backend-services-core.md`, `docs/architecture/transactions-and-concurrency.md`, `docs/design/flows/`, `docs/design/data-model/core.md`, `docs/design/api/core.md`, `docs/design/state-machines.md` |
| `backend/services/channel/**` | `docs/plans/backend-services-channel.md`, `docs/design/flows/webhook-ingestion.md`, `docs/design/api/channel.md` |
| `backend/services/insights/**` | `docs/plans/backend-services-insights.md`, `docs/usecase-userstory/reports.md`, `docs/design/api/insights.md`, `docs/design/forecast.md` |
| `frontend/**` | `docs/plans/frontend-*.md`, `docs/design/ui/`, `docs/conventions/frontend.md` |
| `deploy/**`, `.github/**` | `docs/plans/deploy.md`, `docs/architecture/context-and-containers.md` |
| Bất kỳ event nào | `docs/design/events.md` |
| Bất kỳ test nào | `docs/testing/strategy.md`, `docs/testing/scenarios.md` |

## Bất biến không được phá

Vi phạm bất kỳ điều nào dưới đây là lỗi, kể cả khi test hiện có vẫn xanh.

1. Đơn hàng và tồn kho ở chung service `core`, chung một transaction. Không tách.
2. Service không gọi HTTP sang service khác và không đọc schema của service khác. Dữ liệu dùng chung đi bằng event.
3. Event ghi vào outbox trong cùng transaction với thay đổi nghiệp vụ. Không gửi RabbitMQ bên trong transaction. Consumer ghi inbox trước khi xử lý.
4. `on_hand` chỉ đổi qua `PostLedger`; mỗi lần đổi để lại đúng một dòng sổ. `reserved` chỉ đổi qua `IStockService`.
5. Sổ `inventory_transactions` chỉ thêm mới. Sửa sai bằng dòng `Reversal`.
6. Thứ tự khóa: dòng chứng từ trước, rồi các dòng số dư theo `branch_id`, `sku_id` tăng dần, bằng `SELECT ... FOR UPDATE`.
7. Một use case là một transaction. Lỗi thì rollback toàn bộ; không commit nửa chừng.
8. `cost_price` của dòng đơn ghi một lần lúc xác nhận và không bao giờ đổi.
9. Mọi entity nghiệp vụ có `TenantId` và đi qua Global Query Filter. `IgnoreQueryFilters` chỉ dùng ở các chỗ liệt kê trong `docs/architecture/multi-tenancy.md`.
10. Dữ liệu của tenant khác trả 404. Không tin `tenantId` trong body request.
11. Domain không tham chiếu EF Core hay ASP.NET Core. Application không tham chiếu Infrastructure. Controller không chứa nghiệp vụ.
12. Hợp đồng event chỉ được thêm trường; không đổi tên, không xóa, không đổi kiểu.
13. Mọi endpoint khai báo quyền theo vai trò; quyền kiểm ở backend.
14. Luồng đụng tới tồn kho phải có test tích hợp trên PostgreSQL thật. Không dùng database in-memory hay SQLite.

## Quy ước tối thiểu

- Backend: C# 12, .NET 8, nullable bật. Use case là một handler thường, đăng ký thẳng vào DI. Chi tiết và danh sách package được phép: `docs/conventions/backend.md`.
- Frontend: React, Vite, TypeScript strict. Mọi lời gọi API đi qua `frontend/shared`. Chi tiết: `docs/conventions/frontend.md`.
- Tên bảng và cột snake_case; tên project `Oism.<Service>.<Lớp>`; tên event là danh từ kèm quá khứ phân từ.
- Không thêm package ngoài danh sách khi chưa được đồng ý. Không dùng MediatR, AutoMapper, MassTransit, FluentAssertions.

## Lệnh

Các lệnh dưới là quy ước cho skeleton. Trước khi task W1-02 và W1-03 xong, chúng chưa chạy được.

```bash
dotnet build backend/Oism.sln
dotnet test backend/Oism.sln
npm --prefix frontend install
npm --prefix frontend run build --workspaces
docker compose -f deploy/docker-compose.yml up -d
```

Test tích hợp cần Docker đang chạy vì dùng Testcontainers.

## Việc không tự làm

- Không commit, không push, không mở PR khi chưa được yêu cầu.
- Khi được yêu cầu commit: tác giả duy nhất là người dùng theo cấu hình git của máy. Không thêm dòng `Co-Authored-By`, không thêm chữ ký hay ghi chú "Generated with" của bất kỳ công cụ AI nào vào commit message hay mô tả PR.
- Không sửa ADR đã có; đề xuất ADR mới.
- Không đưa bí mật hay dữ liệu thật vào repo.
- Không làm nhiều task trong một lượt khi chỉ được giao một task.
- Không xóa hay viết lại tài liệu ngoài phạm vi việc đang làm.

## Ngôn ngữ

- Tài liệu, commit message, chuỗi hiển thị cho người dùng cuối và câu trả lời: tiếng Việt.
- Tên định danh trong code, tên file code, thông điệp log: tiếng Anh.

## Báo cáo sau khi làm

Kết thúc mỗi việc bằng một báo cáo ngắn gồm:

1. Mã task và mã yêu cầu.
2. File đã tạo hoặc sửa, nhóm theo lớp.
3. Từng tiêu chí chấp nhận: đạt bằng test nào, hoặc chưa đạt vì sao.
4. Kết quả build và test đã chạy, kèm lệnh.
5. Tài liệu đã sửa.
6. Việc còn dở, giả định đã đặt, và điểm nào chưa kiểm được.
