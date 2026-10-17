# Báo cáo độ bao phủ test của core

Báo cáo coverlet cho NFR-MAINT-02 (task W5-01): ít nhất 80% dòng lệnh, gộp test đơn vị và test tích hợp, cho ba module Inventory, Reservation, Costing của `core`. Mục tiêu và cách đo chung ở [strategy.md](strategy.md) mục "Độ bao phủ".

## Ba module gồm những file nào

Đường dẫn tính từ `backend/services/core/src/`. Một file chỉ thuộc một module; Costing và Reservation được xét trước, phần còn lại của thư mục Inventory tính cho Inventory. Script [tools/coverage/core-coverage.ps1](../../tools/coverage/core-coverage.ps1) dùng đúng danh sách này.

| Module | File | Vì sao |
| --- | --- | --- |
| Costing | `Oism.Core.Domain/Inventory/WeightedAverageCost.cs`, `Oism.Core.Application/Inventory/ConfirmPurchaseReceipt/`, `Oism.Core.Application/Orders/ConfirmOrder/` | Tính lại giá vốn bình quân khi nhập (FR-COST-01) và chốt `cost_price` khi xác nhận đơn (FR-COST-02) |
| Reservation | `Oism.Core.Domain/Orders/Reservation.cs`, `Oism.Core.Application/Inventory/IStockService.cs`, `Oism.Core.Application/Orders/ReserveStock/`, `Oism.Core.Application/Orders/CancelOrder/`, `Oism.Core.Infrastructure/Jobs/ExpireReservationsJob.cs` | Giữ, trừ và nhả hàng (FR-RSE-01..03): mọi chỗ đổi `reserved` |
| Inventory | Phần còn lại của `Oism.Core.Domain/Inventory/` và `Oism.Core.Application/Inventory/`; các repository `InventoryRepository`, `PurchaseReceiptRepository`, `StockTransferRepository`, `StocktakeRepository`, `StockQueries` ở `Oism.Core.Infrastructure/Repositories/` | Sổ kho, số dư, phiếu nhập, chuyển kho, kiểm kê, ngưỡng tồn (FR-INV-01..04) |

Migration, controller và phần của module Orders không thuộc ba module nên không vào số đo.

## Cách chạy

Máy có .NET 8, từ gốc repo:

```bash
dotnet test backend/services/core/tests/Oism.Core.UnitTests --collect:"XPlat Code Coverage" --results-directory coverage
dotnet test backend/services/core/tests/Oism.Core.IntegrationTests --collect:"XPlat Code Coverage" --results-directory coverage
pwsh tools/coverage/core-coverage.ps1 -ResultsDir coverage
```

Máy chỉ có Docker:

```bash
docker compose -f deploy/docker-compose.yml run --rm tests bash -c "cp -r /repo/. /src && cd /src && dotnet test services/core/tests/Oism.Core.UnitTests --collect:'XPlat Code Coverage' --results-directory /coverage && dotnet test services/core/tests/Oism.Core.IntegrationTests --collect:'XPlat Code Coverage' --results-directory /coverage && pwsh /tools/coverage/core-coverage.ps1 -ResultsDir /coverage"
```

Script in bảng như dưới, liệt kê các dòng chưa chạy, và thoát mã 1 khi module nào dưới 80%. CI chạy đúng các bước này ở mỗi lần đẩy lên `main` và đính kèm bảng vào phần tóm tắt của lần chạy.

## Kết quả lần đo 2026-10-18

43 test đơn vị và 172 test tích hợp của `core`, trên nhánh phase 9, đo bằng coverlet trong image `mcr.microsoft.com/dotnet/sdk:8.0`; máy Intel Core i5-10200H, 8 luồng, 7,8 GB RAM.

| Module | Số file | Dòng đã chạy | Tổng dòng | Tỉ lệ |
| --- | --- | --- | --- | --- |
| Costing | 3 | 40 | 40 | 100,0% |
| Reservation | 7 | 241 | 241 | 100,0% |
| Inventory | 33 | 986 | 986 | 100,0% |

Trước W5-01 Inventory ở 99,2%; 8 dòng còn hở là getter `TenantId` của các chứng từ kho và câu chặn khóa chứng từ ngoài transaction. `InventoryDocumentTests` thêm hai test cho đúng hai điều đó: chứng từ tạo qua API mang tenant của token, và khóa chứng từ ngoài transaction bị chặn.

Chạy kèm coverlet làm code chậm đi nhiều lần, nên test đồng thời 50 transaction trên cùng một dòng khóa có lúc vượt 30 giây chờ mặc định của Npgsql. Test tích hợp của `core` vì vậy đặt `Command Timeout=180` trong chuỗi kết nối.
