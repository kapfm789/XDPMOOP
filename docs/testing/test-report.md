# Tài liệu kiểm thử

Tổng hợp các ca kiểm thử đơn vị, tích hợp và đồng thời của OISM cùng kết quả chạy ở ngày bàn giao (mục e.5 của đề, task W5-08). Cách viết và đặt test ở [strategy.md](strategy.md); 23 kịch bản bắt buộc ở [scenarios.md](scenarios.md). Tài liệu này không lặp lại chi tiết của các báo cáo riêng mà trỏ tới chúng.

## 1. Phạm vi và môi trường

| Tầng | Công cụ | Chạy trên |
| --- | --- | --- |
| Đơn vị backend | xUnit | Container `mcr.microsoft.com/dotnet/sdk:8.0` (service `tests` của Compose dev), cũng chạy trong CI |
| Tích hợp và đồng thời | xUnit, `WebApplicationFactory`, Testcontainers | Mỗi lớp test dựng PostgreSQL 17 và, khi cần, RabbitMQ 4 riêng trong Docker |
| Đơn vị frontend | Vitest | Node 22 trên máy, Node 24 trong CI |
| API theo tài liệu | Swagger sinh từ code, Postman collection chạy bằng newman | Gateway của Compose dev |
| Tải | k6 | Gói demo, [load-test.md](load-test.md) |

Máy chạy ngày 2026-10-18: laptop Intel Core i5-10200H, 4 nhân 8 luồng, RAM 7,8 GB, Windows 10; Docker Desktop 29.8.2 với máy ảo WSL 2 được cấp 8 luồng và khoảng 3,7 GB RAM.

Lệnh chạy toàn bộ test backend khi máy chỉ có Docker (máy có .NET 8 thì `dotnet test backend/Oism.sln`):

```bash
docker compose -f deploy/docker-compose.yml run --rm tests bash -c "cp -r /repo/. /src && dotnet test Oism.sln -m:1"
```

`-m:1` chạy lần lượt từng project test. Trên máy ảo Docker chỉ có khoảng 4 GB RAM, để các project chạy song song (mặc định) thì vài chục container PostgreSQL dựng cùng lúc làm Docker quá tải: test lỗi ở bước tạo container hoặc ở bước mở kết nối, không phải do nghiệp vụ. Nên tắt Compose dev trước khi chạy để test đồng thời có đủ tài nguyên. CI trên GitHub Actions không gặp giới hạn này.

## 2. Kết quả

### Backend

| Project | Tầng | Số test | Kết quả |
| --- | --- | --- | --- |
| `Oism.Identity.UnitTests` | Đơn vị | 12 | Đạt |
| `Oism.Identity.IntegrationTests` | Tích hợp | 37 | Đạt |
| `Oism.Catalog.UnitTests` | Đơn vị | 24 | Đạt |
| `Oism.Catalog.IntegrationTests` | Tích hợp, đồng thời | 43 | Đạt |
| `Oism.Core.UnitTests` | Đơn vị | 43 | Đạt |
| `Oism.Core.IntegrationTests` | Tích hợp, đồng thời, thông điệp | 175 | Đạt |
| `Oism.Channel.UnitTests` | Đơn vị | 31 | Đạt |
| `Oism.Channel.IntegrationTests` | Tích hợp, thông điệp | 17 | Đạt |
| `Oism.Insights.UnitTests` | Đơn vị | 35 | Đạt |
| `Oism.Insights.IntegrationTests` | Tích hợp, thông điệp, realtime | 55 | Đạt |
| `Oism.BuildingBlocks.IntegrationTests` | Tích hợp hạ tầng chung | 16 | Đạt |
| **Tổng** | | **488** | **Đạt** |

Số test tính cả từng bộ tham số của `[Theory]`: mỗi test đồng thời chạy lặp 20 vòng nên tính 20.

### Frontend

| Gói | Số test | Nội dung | Kết quả |
| --- | --- | --- | --- |
| `shared` | 30 | Định dạng tiền và ngày, lọc menu theo vai trò, client API (làm mới token một lần, thử lại `reference_not_ready`), kết nối realtime, đếm mã quét bằng camera, thông báo lỗi theo `code`, danh sách ngân hàng VietQR, phiên đăng nhập (access token trong bộ nhớ, refresh token ở `localStorage`) | Đạt |
| `admin` | 16 | Khớp route với mục menu, menu theo vai trò, dựng 7 ngày của biểu đồ doanh thu, vẽ mã vạch EAN-13 và Code128, nhãn SKU của báo cáo | Đạt |
| `pos` | 6 | Giỏ hàng: thêm SKU không vượt tồn khả dụng, tổng tiền, xử lý 409 `insufficient_stock`, chọn SKU từ mã quét; chuỗi VietQR và mã kiểm CRC | Đạt |

Số test frontend chạy lại ngày 2026-10-23 bằng `npm --prefix frontend test`, sau khi các task W5-12 đến W5-15 thêm test và bổ sung test cho phần logic thuần còn thiếu.

### Kiểm tra khác

| Kiểm tra | Kết quả | Chi tiết |
| --- | --- | --- |
| Độ bao phủ Inventory, Reservation, Costing của `core` | 100% dòng lệnh, ngưỡng 80% | [coverage.md](coverage.md) |
| Cách ly tenant toàn hệ thống: API, event, job, hub | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 | [tenant-isolation.md](tenant-isolation.md) |
| Đo tải k6 | Tìm SKU p95 44 ms, POS checkout 141 ms, tạo đơn 121 ms, báo cáo 293 ms; flash sale 50 đơn đúng 1 thành công | [load-test.md](load-test.md) |
| Backtest dự báo trên seed | Có WAPE của dự báo và của phương án cơ sở | [forecast-backtest.md](forecast-backtest.md) |
| Postman collection theo tài liệu API | 86 request, 141 assertion đạt | [design/api/postman/](../design/api/postman/README.md) |
| Kịch bản demo, phần API, hai vòng liên tiếp | Đạt | [handover/demo-script.md](../handover/demo-script.md) |

## 3. Ca kiểm thử đơn vị

Test đơn vị kiểm quy tắc nằm ở Domain và handler với repository giả, không đụng database.

| Service | Nhóm ca | Ví dụ test |
| --- | --- | --- |
| `identity` | Chi nhánh, người dùng và vai trò, refresh token xoay vòng | `BranchTests`, `UserTests`, `RefreshTokenTests` |
| `catalog` | Số kiểm tra và sinh mã EAN-13, cây danh mục không vòng lặp, quy tắc sản phẩm đơn và có biến thể | `Ean13Tests`, `CategoryTests`, `ProductTests` |
| `core` | Số dư không âm và chỉ đổi qua bút toán, giá vốn bình quân, máy trạng thái đơn, giữ và xuất hàng, phiếu nhập, chuyển kho, kiểm kê, bản sao tham chiếu theo `version` | `InventoryBalanceTests`, `WeightedAverageCostTests`, `OrderTests`, `OrderFulfilmentTests`, `PurchaseReceiptTests`, `StockTransferTests`, `StocktakeTests`, `ReferenceTests` |
| `channel` | Adapter ba sàn về Canonical Order, trạng thái bản ghi webhook | `ChannelAdapterTests`, `WebhookEventTests` |
| `insights` | Dự báo san bằng hàm mũ, backtest không dùng dữ liệu sau mốc cắt, dựng bảng đọc từ event, ngày theo giờ Việt Nam | `DemandForecasterTests`, `ProjectionTests`, `ReportCalendarTests` |

Mỗi project đơn vị còn có `LayeringTests`: Domain chỉ tham chiếu thư viện chuẩn và `Oism.SharedKernel`, Application không tham chiếu Infrastructure, Api hay EF Core (bất biến 11 ở [AGENTS.md](../../AGENTS.md)).

## 4. Ca kiểm thử tích hợp

Test tích hợp gọi API thật của service qua `WebApplicationFactory`, trên PostgreSQL thật; mỗi test tự tạo tenant mới. Mỗi test của `core` kết thúc bằng kiểm bất biến tồn kho: số dư khớp sổ, `reserved` khớp phần giữ Active, không âm, sổ liền mạch ([strategy.md](strategy.md)).

| Nhóm | Service | Nội dung chính |
| --- | --- | --- |
| Xác thực và phiên | `identity` | Đăng ký, đăng nhập bằng email hoặc số điện thoại, refresh xoay vòng, dùng lại token đã xoay thu hồi cả chuỗi, đăng xuất |
| Người dùng, chi nhánh | `identity` | CRUD, phát `BranchUpserted`, quyền Owner |
| Danh mục, sản phẩm, mã vạch, giá | `catalog` | Trùng mã 409, EAN-13 sai 400, Staff đặt giá bị bỏ qua, phát `SkuUpserted` |
| Sổ kho, nhập hàng, chuyển kho, kiểm kê | `core` | Trigger chặn sửa sổ, giá vốn 110.000, xác nhận lại không tác động, hàng đang chuyển, kiểm kê thấp hơn lượng giữ bị chặn |
| Đơn, giữ hàng, POS | `core` | Giữ toàn bộ hoặc không, duyệt chốt giá vốn, hủy hai lần, hủy sau duyệt 409, `Idempotency-Key`, lỗi giữa chừng rollback hết |
| Job hết hạn | `core` | Hủy đơn hết hạn mỗi tenant một lần, chạy lại không tác động |
| Webhook, simulator | `channel` | Chữ ký sai 401, gửi lại cùng sự kiện không tạo lệnh thứ hai, bắn tải |
| Báo cáo, cảnh báo, dự báo | `insights` | Lợi nhuận gộp 300.000 − 220.000 = 80.000, lọc bốn chiều, tổng hợp ngày, cảnh báo tồn, lần chạy dự báo lỗi giữ kết quả cũ |
| Realtime | `insights` | Phiên của tenant khác không nhận thông báo |
| Hạ tầng chung | `BuildingBlocks` | Kiểm JWT, policy theo vai trò, Global Query Filter, outbox và inbox |
| Swagger | Cả 5 service | Tài liệu sinh được, endpoint có policy mang yêu cầu Bearer và vai trò khớp tài liệu API |

## 5. Ca kiểm thử đồng thời

Mọi request được giữ sau một cổng rồi thả cùng lúc để chúng thật sự tranh nhau; test chạy 20 vòng.

| Test | Kịch bản | Kiểm |
| --- | --- | --- |
| `ReserveStockConcurrencyTests.SubmitOrder_FiftyOnlineOrdersForTheLastUnit_ExactlyOneIsReserved` | T01 | 50 đơn online tranh 1 sản phẩm: đúng 1 giữ được, 49 bị từ chối, tồn không âm |
| `ReserveStockConcurrencyTests.CreateOrder_FiftyRequestsForTheLastUnit_ExactlyOneGets201` | T01 | Cùng điều trên qua API tạo đơn thủ công |
| `PosCheckoutConcurrencyTests.Checkout_PosAndAnOnlineOrderForTheLastUnit_ExactlyOneGetsIt` | T02 | POS và đơn online tranh đơn vị cuối: tổng nhận không vượt khả dụng |
| `PosCheckoutConcurrencyTests.Checkout_SameKeySentTenTimesAtOnceAndManyTillsForTheLastUnits_OneOrderPerKeyAndNoOversell` | T03 | Cùng `Idempotency-Key` gửi 10 lần một lúc: một đơn |
| `ExpireReservationsConcurrencyTests.ConfirmAndExpiryJob_SameExpiredOrderAtTheSameTime_ExactlyOneWins` | T07 | Duyệt đúng lúc job hết hạn chạy: chỉ một kết quả thắng |
| `PostLedgerConcurrencyTests.PostLedger_TwentyTransactionsOnOneBalanceAtOnce_NoChangeIsLostAndTheLedgerStaysContinuous` | | 20 bút toán cùng lúc trên một số dư: không mất thay đổi, sổ liền mạch |
| `PurchaseReceiptTests.ConfirmPurchaseReceipt_TenRequestsAtOnce_PostsTheLedgerOnce` | T11 | Xác nhận một phiếu 10 lần cùng lúc: sổ ghi một lần |
| `ReceiveWebhookTests.ReceiveWebhook_SameEventSentAgainAndTenTimesAtOnce_Returns202EachTimeButStoresOneEventAndOneCommand` | T03 | Cùng webhook 10 lần cùng lúc: một bản ghi, một lệnh |
| `BarcodeConcurrencyTests.AddBarcode_TenGeneratedAtOnce_AllSucceedWithConsecutiveCodes` | | 10 mã EAN-13 sinh cùng lúc cho một tenant: đều thành công, không trùng |

## 6. Kịch bản bắt buộc và test tương ứng

| Mã | Test (project tích hợp trừ khi ghi khác) | Kết quả |
| --- | --- | --- |
| T01 | `ReserveStockConcurrencyTests` (hai test trên) | Đạt |
| T02 | `PosCheckoutConcurrencyTests.Checkout_PosAndAnOnlineOrderForTheLastUnit_ExactlyOneGetsIt` | Đạt |
| T03 | `ReceiveWebhookTests.ReceiveWebhook_SameEventSentAgainAndTenTimesAtOnce...`, `PosCheckoutConcurrencyTests.Checkout_SameKeySentTenTimesAtOnce...`, `OrderEventsTests.SubmitOrder_DeliveredThroughTheBrokerMoreThanOnce...` | Đạt |
| T04 | `ReserveStockTests.SubmitOrder_SecondSkuShort_RejectsTheWholeOrderAndReservesNothing`, `OrderEventsTests.SubmitOrder_DeliveredThroughTheBrokerWithoutEnoughStock...` | Đạt |
| T05 | `ConfirmOrderTests.ConfirmOrder_LedgerWriteFails_LeavesOrderStockHoldsCostLedgerAndOutboxAsBefore`, `PosCheckoutTests.Checkout_LedgerWriteFails_RollsBackTheOrderTheHoldsAndThePayment` | Đạt |
| T06 | `CancelOrderTests.CancelOrder_Twice_ReleasesTheHoldOnceAndReturnsTheSameOrder`, `ExpireReservationsTests.ExpireReservations_ExpiredOrdersOfTwoTenants...` | Đạt |
| T07 | `ExpireReservationsConcurrencyTests.ConfirmAndExpiryJob_SameExpiredOrderAtTheSameTime_ExactlyOneWins` | Đạt |
| T08 | `CancelOrderTests.CancelOrder_AfterConfirmedOrCompleted_Returns409AndStockIsUnchanged` | Đạt |
| T09 | `PurchaseReceiptTests.ConfirmPurchaseReceipt_TenAtHundredThenFiveAtOneThirty_AverageCostIs110000`; đơn vị: `WeightedAverageCostTests`, `InventoryBalanceTests` | Đạt |
| T10 | `ConfirmOrderTests.ConfirmOrder_ThenReceiveANewLotAtAnotherPrice_CostPriceOfTheOrderStays`; `insights`: `GrossProfitReportTests.GrossProfit_NewPurchaseRaisesAvgCost_OldOrderProfitUnchanged` | Đạt |
| T11 | `PurchaseReceiptTests.ConfirmPurchaseReceipt_AlreadyConfirmed...`, `...TenRequestsAtOnce_PostsTheLedgerOnce` | Đạt |
| T12 | `StockTransferTests.Transfer_ShipThenReceive_GoodsAreInTransitBetweenTheTwoStepsAndCarryTheSenderCost` | Đạt |
| T13 | `StocktakeTests.PostStocktake_CountBelowTheHeldQuantity_Returns409WithTheHoldingOrdersAndAdjustsNoSku` | Đạt |
| T14 | 41 test gắn nhãn T14 ở cả 5 service và `BuildingBlocks`; bảng đối chiếu ở [tenant-isolation.md](tenant-isolation.md) | Đạt |
| T15 | `NotificationsHubTests.OrderCreated_OrderOfTenantA_OnlySessionOfTenantAReceivesIt`, `...LowStock_AvailableCrossesThreshold_PushedOncePerCrossingToTenantOnly` | Đạt |
| T16 | `PostLedgerTests.Ledger_DirectUpdateOrDelete_DatabaseRejectsBothAndTheLineIsUnchanged` | Đạt |
| T17 | Đo tải k6 | Đạt, [load-test.md](load-test.md) |
| T18 | `ForecastTests.RunForecast_60DaysOfSales_ResultPerBranchAndSkuWithBacktest`; đơn vị: `DemandForecasterTests` (backtest, không dùng dữ liệu sau mốc cắt) | Đạt |
| T19 | `ForecastTests.RunForecast_SkuWithFewDaysOrNoSales...`, `...FailingRun_RecordedFailedAndPreviousResultsStillShown`, `...FailureMidTransaction_NoPartialResults` | Đạt |
| T20 | Kiểm tay theo [handover/install-guide.md](../handover/install-guide.md) | Xem mục 7 |
| T21 | `SkeletonTests.Swagger_Document_MarksRolesAndPublicEndpoints` ở 5 service; Postman collection | Đạt |
| T22 | `insights`: `ProjectionConsumerTests.OrderConfirmed_SameEventDeliveredTwice_OneSalesFactPerLine`, `...ReplayedWithNewEventId_SalesFactsUnchanged` | Đạt |
| T23 | `OutboxWithoutBrokerTests.CreateOrder_BrokerDownAtCommit_CommitsAndSendsEachMessageOnceWhenTheBrokerIsBack` | Đạt |

## 7. Giới hạn và điểm chưa kiểm

| Điểm | Ghi chú |
| --- | --- |
| Giao diện | Không có test tự động end-to-end cho `admin` và `pos`; luồng màn hình kiểm tay theo [handover/demo-script.md](../handover/demo-script.md) |
| T20 trên máy thật sự sạch | Gói demo đã dựng và chạy kịch bản demo trên máy dev; chưa chạy trên một máy chưa từng có image nền |
| Tài nguyên khi chạy test | Máy ảo Docker 4 GB RAM cần chạy test tuần tự (`-m:1`) và tắt Compose dev; chạy song song thì test đồng thời có thể lỗi timeout khi mở kết nối |
| Ghi đơn ở mức bão hòa | p95 khoảng 800 ms khi 41 đơn mỗi giây trên 6 SKU không nghỉ, vượt ngưỡng 500 ms; tải theo mô tả của đề vẫn đạt ([load-test.md](load-test.md)) |
