# Chiến lược kiểm thử

Mọi luồng đụng tới tồn kho phải có test tích hợp chạy trên PostgreSQL thật; test đơn vị một mình không chứng minh được việc khóa dòng. Các kịch bản bắt buộc nằm ở [scenarios.md](scenarios.md); tiêu chí chấp nhận của từng use case nằm ở [usecase-userstory/](../usecase-userstory/README.md).

## Các tầng test

| Tầng | Kiểm gì | Công cụ | Đặt ở | Chạy khi |
| --- | --- | --- | --- | --- |
| Đơn vị | Quy tắc ở Domain; handler với repository giả | xUnit | `tests/Oism.<Service>.UnitTests` | Mỗi lần build và trong CI |
| Tích hợp | Use case qua API thật của service, trên PostgreSQL thật | xUnit, Testcontainers, `WebApplicationFactory` | `tests/Oism.<Service>.IntegrationTests` | CI |
| Đồng thời | Nhiều request song song vào cùng dữ liệu | Như tích hợp | `IntegrationTests/Concurrency` | CI, mỗi test lặp 20 lần |
| Thông điệp | Outbox, inbox, giao lại, RabbitMQ tắt | Testcontainers RabbitMQ | `IntegrationTests/Messaging` | CI |
| Tải | p95 theo NFR-PERF-01, flash sale | k6 | `tools/k6` | Phase 9, trên gói Compose |
| Giao diện | Thao tác tay theo use case | Danh sách kiểm | Kịch bản demo | Cuối mỗi phase |

## Quy tắc

- Không dùng database in-memory hay SQLite cho bất kỳ test nào đụng tới `core`. Chúng không có `FOR UPDATE`, CHECK và trigger như PostgreSQL.
- Mỗi test tích hợp tự tạo một tenant mới và chỉ đọc dữ liệu của tenant đó. Nhờ vậy test chạy song song được và không cần dọn dữ liệu chung.
- Thời gian trong test đi qua `IClock` giả; không ngủ chờ đồng hồ thật, trừ test của job chạy thật.
- So sánh bằng `Assert` của xUnit. Không thêm thư viện assert khác.
- Mỗi test của `core` kết thúc bằng lời gọi kiểm bất biến tồn kho (xem dưới).
- Test không được phụ thuộc thứ tự chạy.

## Đặt tên và gắn nhãn

Tên test theo dạng `<Use case>_<Điều kiện>_<Kết quả>`. Mỗi test gắn mã kịch bản hoặc mã tiêu chí chấp nhận để RTM truy được.

```csharp
[Fact]
[Trait("Scenario", "T09")]
[Trait("UseCase", "UC-INV-02 AC-3")]
public async Task ConfirmPurchaseReceipt_TenAtHundredThenFiveAtOneThirty_AverageCostIs110000()
```

## Kiểm bất biến sau mỗi test của core

Một hàm trợ giúp dùng chung, chạy trên tenant của test:

| Kiểm | Điều kiện |
| --- | --- |
| Số dư khớp sổ | Với mỗi dòng số dư: `on_hand` bằng tổng IN trừ tổng OUT của sổ |
| Phần giữ khớp số dư | `reserved` bằng tổng số lượng các phần giữ Active |
| Không âm | `on_hand >= 0`, `reserved >= 0`, `reserved <= on_hand` |
| Sổ liền mạch | `balance_after` của mỗi dòng bằng `balance_after` dòng trước cộng hoặc trừ `quantity` |

## Mẫu test đồng thời

Mọi request được giữ lại sau một cổng rồi thả cùng lúc, để chúng thật sự tranh nhau. Tên các lớp trợ giúp dưới đây chỉ để minh họa.

```csharp
[Fact]
[Trait("Scenario", "T01")]
public async Task Reserve_FiftyRequestsForLastUnit_ExactlyOneSucceeds()
{
    var sku = await Seed.SkuWithStockAsync(onHand: 1);
    var gate = new TaskCompletionSource();

    var tasks = Enumerable.Range(0, 50).Select(async i =>
    {
        await gate.Task;
        return await Api.SubmitOrderAsync(sku, quantity: 1, externalOrderId: $"T01-{i}");
    }).ToArray();

    gate.SetResult();
    var results = await Task.WhenAll(tasks);

    Assert.Equal(1, results.Count(r => r.Reserved));
    Assert.Equal(49, results.Count(r => r.Rejected));
    var balance = await Db.BalanceAsync(sku);
    Assert.Equal(1, balance.OnHand);
    Assert.Equal(1, balance.Reserved);
    await Invariants.AssertHoldAsync();
}
```

## Mẫu test rollback

Để chứng minh một use case nguyên tử, cắm một điểm gây lỗi vào giữa transaction (ví dụ một `IEventPublisher` ném lỗi ở lần gọi đầu), chạy use case, rồi kiểm mọi bảng liên quan giữ nguyên như trước: đơn, số dư, phần giữ hàng, sổ, outbox.

## Mẫu test cách ly tenant

Tạo dữ liệu ở tenant B, đăng nhập tenant A, rồi gọi mọi endpoint đọc và ghi với ID của B. Kỳ vọng 404 cho mọi lời gọi, và sau đó dữ liệu của B không đổi. Mỗi service có thêm một test duyệt mọi entity của DbContext và báo lỗi nếu entity nghiệp vụ nào không cài `ITenantOwned`.

## Độ bao phủ

- Mục tiêu: ít nhất 80% dòng lệnh, gộp test đơn vị và tích hợp, cho ba phần của `core`: Inventory, Reservation (giữ hàng), Costing (giá vốn) (NFR-MAINT-02).
- Đo bằng coverlet trong CI; báo cáo đính kèm ở phase 9 (W5-01).
- Độ bao phủ cao không thay cho các kịch bản đồng thời, rollback, chống trùng và cách ly tenant.

## Đo tải

- Chạy trên gói Compose đã nạp dữ liệu seed, không chạy trên database trống.
- Báo cáo ghi: cấu hình máy, lượng dữ liệu, số người dùng ảo, thời gian chạy, p95 từng kịch bản.
- Ba ngưỡng đo theo p95: tìm SKU dưới 200 ms, tạo đơn và checkout dưới 500 ms, báo cáo doanh thu dưới 2 giây với 100.000 dòng bán.
- Kịch bản flash sale kiểm cả tính đúng: sau khi chạy, đúng 1 đơn giữ được hàng.
