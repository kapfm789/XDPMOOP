# Bộ test cách ly tenant toàn hệ thống

Bảng đối chiếu của task W5-02 (NFR-TENANT-01, kịch bản T14 và T15): mỗi endpoint, event, job và hub của 5 service được chứng minh cách ly bằng test nào. Quy tắc cách ly ở [architecture/multi-tenancy.md](../architecture/multi-tenancy.md); cách viết test cách ly ở [strategy.md](strategy.md) mục "Mẫu test cách ly tenant".

Kỳ vọng chung: tenant A dùng ID của tenant B thì nhận 404 `not_found`, hoặc 409 `reference_not_ready` khi chi nhánh, SKU của B nằm trong body gửi tới `core` (quy tắc 3); danh sách và báo cáo của A không có dòng nào của B; dữ liệu của B không đổi và không quan hệ chéo nào được tạo. Vai trò không đủ quyền nhận 403, kiểm trong cùng các test hoặc test phân quyền của từng use case.

Tên test ghi theo dạng `Lớp.Phương thức`; lớp nằm trong `backend/services/<service>/tests/Oism.<Service>.IntegrationTests/`.

## Nền chung

| Cơ chế | Test |
| --- | --- |
| Global Query Filter và interceptor khi ghi | `Oism.BuildingBlocks.IntegrationTests` `TenantQueryFilterTests`: đọc chỉ thấy tenant hiện tại, ID của tenant khác không tìm thấy, không có tenant context thì không thấy gì, ghi bản ghi của tenant khác hoặc ghi khi không có tenant context bị chặn |
| Mọi entity nghiệp vụ cài `ITenantOwned` | `SkeletonTests.DbContext_EveryEntity_ImplementsITenantOwned` ở cả 5 service |
| Bản ghi mới mang tenant của token | `Inventory/InventoryDocumentTests.InventoryDocuments_CreatedThroughApi_StampedWithTheTenantOfTheToken` (core) |

## identity

| Đường vào | Test |
| --- | --- |
| `GET`, `POST /users`, `PUT /users/{id}` | `Tenancy/TenantIsolationTests.Users_OfAnotherTenant_AreNeitherListedNorUpdatable` |
| `GET`, `POST /branches`, `PUT /branches/{id}`, `PATCH /branches/{id}/active`; gắn người dùng vào chi nhánh của B; event `BranchUpserted` chỉ phát cho tenant sở hữu | `Tenancy/TenantIsolationTests.Branches_OfAnotherTenant_AreNeitherListedNorUpdatableNorAttachable` |
| `POST /auth/logout` với refresh token của người khác | `Auth/SessionTests.Logout_WithoutAccessTokenOrWithSomeoneElsesRefreshToken_LeavesThatTokenUsable` |

`POST /tenants`, `/auth/login`, `/auth/refresh` là endpoint công khai: tenant lấy từ tài khoản hoặc token tìm được, không từ dữ liệu client gửi. `GET /me` không nhận ID.

## catalog

| Đường vào | Test |
| --- | --- |
| `GET`, `POST /categories`, `PUT`, `DELETE /categories/{id}` | `Tenancy/TenantIsolationTests.Categories_IdOfAnotherTenant_Returns404AndLeavesItUnchanged` |
| `GET`, `POST /brands`, `PUT /brands/{id}` | `Tenancy/TenantIsolationTests.Brands_IdOfAnotherTenant_Returns404AndLeavesItUnchanged` |
| `GET /products`, `GET`, `PUT /products/{id}`, `POST /products/{id}/skus`, `PUT /skus/{id}`, `PUT /skus/{id}/prices`, `POST /skus/{id}/barcodes`, `DELETE /skus/{id}/barcodes/{barcodeId}`; gắn sản phẩm vào danh mục, thương hiệu của B; event `SkuUpserted` không phát khi bị từ chối | `Tenancy/TenantIsolationTests.Products_IdOfAnotherTenant_Returns404AndLeavesThemUnchanged` |

## core

| Đường vào | Test |
| --- | --- |
| `GET /stock`, `GET /ledger` | `Tenancy/TenantIsolationTests.StockAndLedger_IdsOfAnotherTenant_ShowNothingAndLeaveItsBalanceUnchanged` |
| `PUT /stock/threshold` | `Inventory/SetThresholdTests.SetThreshold_BranchOrSkuOfAnotherTenant_IsRefusedAndItsBalanceIsUntouched` |
| `GET`, `POST /suppliers`, `GET`, `POST /purchase-receipts`, `PUT /purchase-receipts/{id}`, `POST /purchase-receipts/{id}/confirm` | `Inventory/PurchaseReceiptTests.PurchaseReceipts_IdsOfAnotherTenant_Return404AndLeaveItsReceiptUntouched` |
| `POST /transfers`, `POST /transfers/{id}/ship`, `POST /transfers/{id}/receive`, `GET /transfers`, `DELETE /transfers/{id}` | `Inventory/StockTransferTests.Transfer_OfAnotherTenantOrByCashier_IsRefusedAndTheTransferIsUnchanged`, `Inventory/StockTransferTests.DeleteTransfer_OnlyWhileDraft_RemovesItWithoutTouchingStock` |
| `GET`, `POST /stocktakes`, `PUT /stocktakes/{id}/counts`, `POST /stocktakes/{id}/post` | `Inventory/StocktakeTests.Stocktake_CashierOrAnotherTenant_CannotOpenCountOrPost`, `Inventory/StocktakeTests.ListStocktakes_ByBranchAndStatus_ShowsOpenSessionsFirstWithTheirLinesAndNothingOfAnotherTenant` |
| `GET /orders`, `GET /orders/{id}` | `Orders/ListOrdersTests.GetOrder_ById_ReturnsLinesHoldsAndPaymentAndHidesCostFromStaffAndOtherTenants` |
| `POST /orders` | `Orders/ReserveStockTests.CreateOrder_BranchOrSkuOfAnotherTenant_IsRefusedAndItsStockIsNotReserved` |
| `POST /orders/{id}/confirm`, `/complete`, `/cancel` | `Orders/ConfirmOrderTests.ConfirmCompleteCancel_OrderOfAnotherTenantOrCashier_IsRefusedAndTheOrderIsUnchanged` |
| `GET /pos/skus`, `POST /pos/checkout` | `Tenancy/TenantIsolationTests.Pos_BranchOrSkuOfAnotherTenant_FindsNothingAndCheckoutIsRefused` |
| Event `SkuUpserted`, `BranchUpserted` | `Messaging/ReferenceConsumerTests.Upserted_FromTwoTenants_EachTenantSeesOnlyItsOwnCopies` |
| Event `SubmitOrder` | `Tenancy/TenantIsolationTests.SubmitOrder_FromTwoTenantsWithTheSameCodes_EachReservesFromItsOwnStockOnly` |
| Job hết hạn giữ hàng | `Orders/ExpireReservationsTests.ExpireReservations_ExpiredOrdersOfTwoTenants_CancelsEachInItsOwnTenantOnceAndLeavesFreshAndConfirmedOrdersAlone` |

## channel

| Đường vào | Test |
| --- | --- |
| `GET`, `POST /channel-shops`, `PATCH /channel-shops/{id}/active` | `Shops/ChannelShopTests.ChannelShops_DuplicateBadInputOtherRolesOrAnotherTenant_AreRefused` |
| `POST /webhooks/{channel}`, `GET /webhook-events`, command `SubmitOrder` ghi vào outbox | `Tenancy/TenantIsolationTests.Webhook_ToTheShopOfAnotherTenant_IsRecordedAndQueuedOnlyInThatTenant` |
| `POST /simulator/burst` | `Simulator/SimulateBurstTests.SimulateBurst_BadInputAnotherTenantsShopOrNotOwner_IsRefusedAndSendsNothing` |
| Event `OrderReserved`, `OrderRejected`, kể cả của tenant khác trùng mã đơn sàn | `Messaging/OrderResultConsumerTests.OrderReservedAndRejected_ForSubmittedWebhooks_MoveThemToReservedWithTheOrderOrRejectedWithTheReason` |

## insights

| Đường vào | Test |
| --- | --- |
| `GET /reports/gross-profit` | `Reports/GrossProfitReportTests.GrossProfit_OtherTenantSales_NotIncluded` |
| `GET /reports/inventory-value`, `/reports/top-sellers`, `/reports/slow-movers` | `Reports/StockReportsTests.StockReports_OtherTenantData_NotIncluded` |
| `GET /alerts/low-stock` | `Alerts/LowStockAlertTests.LowStock_OtherTenantAlert_NotListed` |
| `GET /forecast/reorder-suggestions`, `POST /forecast/run`, job dự báo đêm | `Forecasting/ForecastTests.ForecastJob_TwoTenants_EachUsesOnlyItsOwnSales` |
| Job tổng hợp ngày | `Tenancy/TenantIsolationTests.DailyAggregation_TwoTenantsSellingTheSameSkuOnTheSameDay_EachGetsOnlyItsOwnTotals` |
| Event `OrderConfirmed`, `StockChanged`, `SkuUpserted` | `Messaging/ProjectionConsumerTests.Projections_FromTwoTenants_EachTenantSeesOnlyItsOwnRows` |
| Hub `/hubs/notifications`: `OrderCreated`, `LowStock` (T15) | `Realtime/NotificationsHubTests.OrderCreated_OrderOfTenantA_OnlySessionOfTenantAReceivesIt`, `Realtime/NotificationsHubTests.LowStock_AvailableCrossesThreshold_PushedOncePerCrossingToTenantOnly` |

## Chạy

Lọc theo nhãn kịch bản:

```bash
dotnet test backend/Oism.sln --filter "Scenario=T14|Scenario=T15"
```

Lần chạy 2026-10-18 trên nhánh phase 9, trong image `mcr.microsoft.com/dotnet/sdk:8.0`: 43 test mang nhãn T14 hoặc T15 đều xanh (catalog 4, channel 5, core 16, identity 4, insights 9, `Oism.BuildingBlocks` 5).
