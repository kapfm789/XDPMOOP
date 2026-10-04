# Source tree đích

Đây là cây thư mục mà repo phải có khi dựng xong skeleton (task W1-02). Tên folder, tên project và namespace dùng đúng như dưới đây; đừng đặt tên khác dù thấy hợp lý hơn, vì tài liệu, rule và CI đều trỏ theo các đường dẫn này.

Backend, `frontend/`, `deploy/` và `.github/` đã dựng; `deploy/seed/` và `tools/k6/` chưa có. `catalog` là service đầu tiên có nghiệp vụ: phần mới của service khác chép khuôn từ đó.

## Cây thư mục

```text
XDPMOOP/
├─ AGENTS.md                       quy tắc cho mọi công cụ AI
├─ CLAUDE.md                       nạp AGENTS.md cho Claude Code
├─ .claude/
│  ├─ rules/                       rule tự nạp theo đường dẫn file
│  └─ skills/                      quy trình gọi bằng /tên-skill
├─ backend/
│  ├─ Oism.sln
│  ├─ Directory.Build.props        phiên bản .NET, nullable, cảnh báo thành lỗi
│  ├─ Directory.Packages.props     phiên bản package dùng chung
│  ├─ shared/
│  │  ├─ Oism.SharedKernel/        ITenantOwned, exception gốc có mã; không phụ thuộc gì, Domain được tham chiếu
│  │  ├─ Oism.BuildingBlocks/      Tenancy, Auth, Messaging, Persistence, Web
│  │  ├─ Oism.Contracts/           event và command giữa các service
│  │  └─ tests/
│  │     └─ Oism.BuildingBlocks.IntegrationTests/
│  ├─ gateway/
│  │  └─ Oism.Gateway/
│  └─ services/
│     └─ <service>/                identity, catalog, core, channel, insights
│        ├─ src/
│        │  ├─ Oism.<Service>.Domain/
│        │  ├─ Oism.<Service>.Application/
│        │  ├─ Oism.<Service>.Infrastructure/
│        │  │  └─ Migrations/
│        │  └─ Oism.<Service>.Api/
│        │     ├─ Controllers/
│        │     ├─ Consumers/
│        │     └─ Dockerfile
│        └─ tests/
│           ├─ Oism.<Service>.UnitTests/
│           └─ Oism.<Service>.IntegrationTests/
├─ frontend/
│  ├─ package.json                 npm workspaces: admin, pos, shared
│  ├─ shared/src/                  api, auth, realtime, types
│  ├─ admin/src/                   pages, features, app
│  └─ pos/
│     ├─ public/                   manifest, service worker
│     └─ src/                      pages, features, app
├─ deploy/
│  ├─ docker-compose.yml
│  ├─ .env.example                 giá trị mẫu; .env thật bị git bỏ qua
│  ├─ postgres/init-schemas.sql
│  └─ seed/
├─ tools/k6/
├─ docs/
└─ .github/workflows/ci.yml
```

## Bố cục bên trong một use case

Mỗi use case là một thư mục trong Application, đặt theo module rồi theo tên use case.

```text
Oism.Core.Application/
└─ Orders/
   └─ ConfirmOrder/
      ├─ ConfirmOrderCommand.cs
      ├─ ConfirmOrderHandler.cs
      └─ ConfirmOrderValidator.cs
```

Test của nó nằm cùng tên ở project test: `Oism.Core.UnitTests/Orders/ConfirmOrderHandlerTests.cs` và `Oism.Core.IntegrationTests/Orders/ConfirmOrderTests.cs`.

## Quy ước đặt tên

| Thứ | Quy ước | Ví dụ |
| --- | --- | --- |
| Project và namespace | `Oism.<Service>.<Lớp>` | `Oism.Core.Application` |
| Thư mục service | Chữ thường | `backend/services/core` |
| Handler | `<Động từ><Danh từ>Handler` | `ReserveStockHandler` |
| Command, query | `<Tên use case>Command`, `<Tên>Query` | `PosCheckoutCommand` |
| Controller | Danh từ số nhiều | `OrdersController` |
| Consumer | `<Tên event>Consumer` | `SkuUpsertedConsumer` |
| Bảng và cột database | snake_case, bảng số nhiều | `inventory_balances.on_hand` |
| Database | Một database cho cả hệ thống | `oism` |
| Schema | Tên service, chữ thường | `core` |
| Event | Danh từ kèm quá khứ phân từ | `OrderConfirmed` |
| Component React | PascalCase, một component một file | `CartPanel.tsx` |
| Hook React | `use` kèm tên | `usePosSearch.ts` |

## Folder nào theo tài liệu nào

| Đường dẫn | Đọc trước khi sửa |
| --- | --- |
| `backend/shared/**` | [plans/backend-shared.md](../plans/backend-shared.md), [multi-tenancy.md](multi-tenancy.md), [messaging.md](messaging.md) |
| `backend/gateway/**` | [plans/backend-gateway.md](../plans/backend-gateway.md), [security.md](security.md), [context-and-containers.md](context-and-containers.md) |
| `backend/services/identity/**` | [plans/backend-services-identity.md](../plans/backend-services-identity.md), [usecase-userstory/auth.md](../usecase-userstory/auth.md), [design/data-model/identity.md](../design/data-model/identity.md), [design/api/identity.md](../design/api/identity.md) |
| `backend/services/catalog/**` | [plans/backend-services-catalog.md](../plans/backend-services-catalog.md), [usecase-userstory/catalog.md](../usecase-userstory/catalog.md), [design/data-model/catalog.md](../design/data-model/catalog.md), [design/api/catalog.md](../design/api/catalog.md) |
| `backend/services/core/**` | [plans/backend-services-core.md](../plans/backend-services-core.md), [transactions-and-concurrency.md](transactions-and-concurrency.md), [design/flows/](../design/flows/), [design/data-model/core.md](../design/data-model/core.md), [design/api/core.md](../design/api/core.md), [design/state-machines.md](../design/state-machines.md) |
| `backend/services/channel/**` | [plans/backend-services-channel.md](../plans/backend-services-channel.md), [design/flows/webhook-ingestion.md](../design/flows/webhook-ingestion.md), [design/data-model/channel.md](../design/data-model/channel.md), [design/api/channel.md](../design/api/channel.md) |
| `backend/services/insights/**` | [plans/backend-services-insights.md](../plans/backend-services-insights.md), [usecase-userstory/reports.md](../usecase-userstory/reports.md), [design/data-model/insights.md](../design/data-model/insights.md), [design/api/insights.md](../design/api/insights.md), [design/forecast.md](../design/forecast.md) |
| `backend/**/tests/**` | [testing/strategy.md](../testing/strategy.md), [testing/scenarios.md](../testing/scenarios.md) |
| `frontend/shared/**` | [plans/frontend-shared.md](../plans/frontend-shared.md), [conventions/frontend.md](../conventions/frontend.md) |
| `frontend/admin/**` | [plans/frontend-admin.md](../plans/frontend-admin.md), [design/ui/admin.md](../design/ui/admin.md) |
| `frontend/pos/**` | [plans/frontend-pos.md](../plans/frontend-pos.md), [design/ui/pos.md](../design/ui/pos.md), [usecase-userstory/pos.md](../usecase-userstory/pos.md) |
| `deploy/**`, `.github/**` | [plans/deploy.md](../plans/deploy.md), [context-and-containers.md](context-and-containers.md) |
| `tools/k6/**` | [plans/tools-k6.md](../plans/tools-k6.md), [testing/strategy.md](../testing/strategy.md) |

Rule của Claude Code trong `.claude/rules/` tự nạp các điểm chính của những tài liệu này khi mở file ở đường dẫn tương ứng.
