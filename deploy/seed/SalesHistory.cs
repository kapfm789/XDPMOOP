using Oism.Contracts;

namespace Oism.Seed;

// Một đơn lịch sử đã xác nhận, kèm id của event chở nó.
internal sealed record HistoricalOrder(Guid EventId, OrderConfirmed Order);

// Sinh 60 ngày lịch sử bán tính tới hết hôm qua (giờ Việt Nam) cho mỗi chi nhánh. Ngẫu nhiên nhưng cố định theo
// (tenant, chi nhánh, ngày), nên chạy lại cho ra đúng các đơn đó với đúng id: insights bỏ qua bản trùng.
internal static class SalesHistory
{
    public const int Days = 60;

    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    // POS chiếm phần lớn; còn lại chia cho ba sàn và đơn thủ công.
    private static readonly (string Channel, int Weight)[] Channels = [("POS", 50), ("Shopee", 20), ("TikTok", 14), ("Lazada", 10), ("Admin", 6)];

    // branchIds: id của chi nhánh theo mã; skus: SKU theo mã.
    public static IEnumerable<HistoricalOrder> Generate(
        TenantSeed tenant, IReadOnlyDictionary<string, Guid> branchIds, IReadOnlyDictionary<string, SkuInfo> skus, DateOnly today)
    {
        foreach (var branch in tenant.Branches)
        {
            for (var offset = Days; offset >= 1; offset--)
            {
                var day = today.AddDays(-offset);
                foreach (var order in OrdersOfDay(tenant, branch, branchIds[branch.Code], skus, day, Days - offset))
                    yield return order;
            }
        }
    }

    private static IEnumerable<HistoricalOrder> OrdersOfDay(
        TenantSeed tenant, BranchSeed branch, Guid branchId, IReadOnlyDictionary<string, SkuInfo> skus, DateOnly day, int dayIndex)
    {
        var key = $"{tenant.Key}:{branch.Code}:{day:yyyy-MM-dd}";
        var random = new Random(BitConverter.ToInt32(DeterministicGuid.From(key).ToByteArray(), 0));
        var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.35 : 1.0;

        // Số bán của từng SKU trong ngày, tách thành các dòng 1 đến 2 cái.
        var lines = new List<(SkuInfo Sku, int Quantity)>();
        foreach (var product in tenant.Products)
        {
            var trend = Trend(product.Demand, dayIndex);
            foreach (var variant in product.Variants)
            {
                var mean = product.DailyDemand / product.Variants.Count * branch.DemandFactor * weekend * trend;
                var quantity = Poisson(random, mean);
                while (quantity > 0)
                {
                    var take = Math.Min(quantity, random.Next(1, 3));
                    lines.Add((skus[product.SkuCode(variant)], take));
                    quantity -= take;
                }
            }
        }

        // Gom các dòng đã xáo thành đơn 1 đến 3 dòng, giờ bán từ 8:00 tới 22:00.
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines));
        var orderIndex = 0;
        for (var start = 0; start < lines.Count; orderIndex++)
        {
            var size = Math.Min(random.Next(1, 4), lines.Count - start);
            var orderKey = $"{key}:{orderIndex}";
            var confirmedAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), Vietnam)
                .AddMinutes(random.Next(0, 14 * 60))
                .ToUniversalTime();
            var orderLines = lines.Skip(start).Take(size)
                .Select((line, index) => new OrderConfirmedLine(
                    DeterministicGuid.From($"{orderKey}:line:{index}"), line.Sku.SkuId, line.Quantity, line.Sku.RetailPrice, 0,
                    line.Sku.UnitCost))
                .ToList();
            var order = new OrderConfirmed(
                DeterministicGuid.From($"{orderKey}:order"), PickChannel(random), branchId, confirmedAt, orderLines);
            yield return new HistoricalOrder(DeterministicGuid.From($"{orderKey}:event"), order);
            start += size;
        }
    }

    // Hệ số nhân của nhu cầu theo ngày thứ dayIndex (0 là ngày cũ nhất).
    private static double Trend(Demand demand, int dayIndex) => demand switch
    {
        Demand.Growing => 0.6 + 0.8 * dayIndex / (Days - 1),
        Demand.Declining => 1.4 - 0.8 * dayIndex / (Days - 1),
        Demand.New => dayIndex >= Days - 5 ? 1 : 0,
        Demand.None => 0,
        _ => 1,
    };

    private static string PickChannel(Random random)
    {
        var roll = random.Next(Channels.Sum(channel => channel.Weight));
        foreach (var (channel, weight) in Channels)
        {
            if (roll < weight)
                return channel;
            roll -= weight;
        }

        return "POS";
    }

    // Phân phối Poisson theo Knuth; đủ dùng cho trung bình nhỏ của dữ liệu demo.
    private static int Poisson(Random random, double mean)
    {
        if (mean <= 0)
            return 0;

        var limit = Math.Exp(-mean);
        var product = random.NextDouble();
        var count = 0;
        while (product > limit)
        {
            product *= random.NextDouble();
            count++;
        }

        return count;
    }
}

// SKU đã có trong catalog, kèm giá lẻ dùng làm giá bán lịch sử và giá nhập dùng làm giá vốn đã chốt.
internal sealed record SkuInfo(Guid SkuId, string SkuCode, decimal RetailPrice, decimal UnitCost);
