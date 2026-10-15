using System.Text;
using System.Text.Json;

namespace Oism.Seed;

// Dựng dữ liệu của một tenant qua gateway API, như người dùng thao tác trên admin và pos: mọi quy tắc nghiệp vụ,
// sổ kho, outbox và event đi đúng đường của hệ thống. Mỗi bước tra trước rồi mới tạo, nên chạy lại không sinh bản trùng.
internal sealed class TenantSeeder(GatewayClient api, HistoryPublisher history, TenantSeed tenant, string password, DateOnly today)
{
    private const string OpeningNote = "Nhập đầu kỳ (dữ liệu demo)";
    private const string PhoneOrderNote = "Khách đặt qua điện thoại (dữ liệu demo)";

    public async Task RunAsync()
    {
        var tenantId = await SignInOwnerAsync();
        var branches = await EnsureBranchesAsync();
        await EnsureUsersAsync(branches);
        var skus = await EnsureCatalogAsync();
        var supplierId = await EnsureSupplierAsync();
        await EnsureOpeningStockAsync(branches, skus, supplierId);
        await EnsureThresholdsAsync(branches, skus);
        await EnsureTodayActivityAsync(branches, skus);
        await EnsureChannelShopAsync(branches);

        var orders = SalesHistory.Generate(tenant, branches, skus, today).ToList();
        var sent = await history.PublishAsync(tenantId, orders);
        Log($"published {sent} historical OrderConfirmed events for {SalesHistory.Days} days");
        await WaitForHistoryAsync(orders.Sum(order => order.Order.Lines.Sum(line => line.Quantity)));
        await RunForecastAsync();
    }

    private async Task<Guid> SignInOwnerAsync()
    {
        api.AccessToken = null;
        LoginResponse login;
        try
        {
            login = await LoginAsync();
        }
        catch (SeedApiException exception) when (exception.Status == 401)
        {
            await api.PostAsync<JsonElement>("/api/identity/tenants", new
            {
                tenantName = tenant.TenantName,
                ownerName = tenant.OwnerName,
                email = tenant.OwnerEmail,
                password,
            });
            Log($"registered tenant {tenant.TenantName}");
            login = await LoginAsync();
        }

        api.AccessToken = login.AccessToken;
        return TenantIdOf(login.AccessToken);
    }

    private Task<LoginResponse> LoginAsync() =>
        api.PostAsync<LoginResponse>("/api/identity/auth/login", new { identifier = tenant.OwnerEmail, password });

    // Claim tenant_id của access token (docs/architecture/security.md).
    private static Guid TenantIdOf(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return json.RootElement.GetProperty("tenant_id").GetGuid();
    }

    private async Task<Dictionary<string, Guid>> EnsureBranchesAsync()
    {
        var existing = (await api.GetAsync<List<BranchDto>>("/api/identity/branches")).ToDictionary(branch => branch.Code, branch => branch.Id);
        foreach (var branch in tenant.Branches.Where(branch => !existing.ContainsKey(branch.Code)))
        {
            var created = await api.PostAsync<BranchDto>("/api/identity/branches", new
            {
                code = branch.Code,
                name = branch.Name,
                type = branch.Type,
                address = branch.Address,
            });
            existing[branch.Code] = created.Id;
            Log($"created branch {branch.Code}");
        }

        return existing;
    }

    private async Task EnsureUsersAsync(Dictionary<string, Guid> branches)
    {
        var emails = (await api.GetAsync<Paged<UserDto>>("/api/identity/users?page=1&pageSize=100")).Items
            .Select(user => user.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = new[]
        {
            (Name: tenant.StaffName, Email: tenant.StaffEmail, Role: "Staff", BranchId: (Guid?)null),
            (Name: tenant.CashierName, Email: tenant.CashierEmail, Role: "Cashier", BranchId: (Guid?)branches[tenant.Branches[0].Code]),
        };
        foreach (var user in users.Where(user => !emails.Contains(user.Email)))
        {
            await api.PostAsync<UserDto>("/api/identity/users", new
            {
                fullName = user.Name,
                email = user.Email,
                password,
                role = user.Role,
                branchId = user.BranchId,
            });
            Log($"created {user.Role} {user.Email}");
        }
    }

    // Trả SKU theo mã, kèm giá lẻ và giá nhập của dữ liệu demo.
    private async Task<Dictionary<string, SkuInfo>> EnsureCatalogAsync()
    {
        var categories = (await api.GetAsync<List<CategoryDto>>("/api/catalog/categories")).ToDictionary(category => category.Name, category => category.Id);
        foreach (var name in tenant.Products.Select(product => product.Category).Distinct().Where(name => !categories.ContainsKey(name)))
            categories[name] = (await api.PostAsync<CategoryDto>("/api/catalog/categories", new { name })).Id;

        var brands = (await api.GetAsync<List<BrandDto>>("/api/catalog/brands")).ToDictionary(brand => brand.Name, brand => brand.Id);
        foreach (var name in tenant.Products.Select(product => product.Brand).OfType<string>().Distinct().Where(name => !brands.ContainsKey(name)))
            brands[name] = (await api.PostAsync<BrandDto>("/api/catalog/brands", new { name })).Id;

        var skus = new Dictionary<string, SkuInfo>();
        var createdProducts = 0;
        foreach (var product in tenant.Products)
        {
            var found = (await api.GetAsync<Paged<ProductRow>>($"/api/catalog/products?query={Uri.EscapeDataString(product.Name)}&page=1&pageSize=50"))
                .Items.FirstOrDefault(row => row.Name == product.Name);
            ProductDto saved;
            if (found is null)
            {
                saved = await api.PostAsync<ProductDto>("/api/catalog/products", new
                {
                    name = product.Name,
                    categoryId = categories[product.Category],
                    brandId = product.Brand is null ? (Guid?)null : brands[product.Brand],
                    hasVariants = product.HasVariants,
                    skus = product.Variants.Select(variant => SkuRequest(product, variant)).ToList(),
                });
                createdProducts++;
            }
            else
            {
                saved = await api.GetAsync<ProductDto>($"/api/catalog/products/{found.Id}");
                var missing = product.Variants.Where(variant => saved.Skus.All(sku => sku.SkuCode != product.SkuCode(variant))).ToList();
                foreach (var variant in missing)
                    await api.PostAsync<SkuDto>($"/api/catalog/products/{saved.Id}/skus", SkuRequest(product, variant));
                if (missing.Count > 0)
                    saved = await api.GetAsync<ProductDto>($"/api/catalog/products/{saved.Id}");
            }

            foreach (var sku in saved.Skus)
            {
                // Mỗi SKU một mã vạch EAN-13 do catalog tự sinh, để POS quét được.
                if (sku.Barcodes.Count == 0)
                    await api.PostAsync<BarcodeDto>($"/api/catalog/skus/{sku.Id}/barcodes", new { symbology = "EAN13" });
                skus[sku.SkuCode] = new SkuInfo(sku.Id, sku.SkuCode, product.RetailPrice, product.UnitCost);
            }
        }

        Log($"catalog ready: {skus.Count} SKUs, {createdProducts} products created");
        return skus;
    }

    // Giá sỉ thấp hơn giá lẻ 10%, làm tròn tới nghìn đồng.
    private static object SkuRequest(ProductSeed product, Variant variant) => new
    {
        skuCode = product.SkuCode(variant),
        attributes = variant.Attributes,
        retailPrice = product.RetailPrice,
        wholesalePrice = Math.Round(product.RetailPrice * 0.9m / 1000) * 1000,
    };

    private async Task<Guid> EnsureSupplierAsync()
    {
        var supplier = (await api.GetAsync<List<SupplierDto>>("/api/core/suppliers")).FirstOrDefault(row => row.Name == tenant.SupplierName);
        return supplier?.Id ?? (await api.PostAsync<SupplierDto>("/api/core/suppliers", new { name = tenant.SupplierName })).Id;
    }

    // Mỗi chi nhánh một phiếu nhập đầu kỳ đã xác nhận: tồn khoảng 18 ngày bán; SKU đánh dấu LowStock chỉ nhập 2.
    private async Task EnsureOpeningStockAsync(Dictionary<string, Guid> branches, Dictionary<string, SkuInfo> skus, Guid supplierId)
    {
        var receipts = (await api.GetAsync<Paged<ReceiptDto>>("/api/core/purchase-receipts?page=1&pageSize=100")).Items
            .Where(receipt => receipt.Note == OpeningNote).ToList();
        foreach (var branch in tenant.Branches)
        {
            var branchId = branches[branch.Code];
            var receipt = receipts.FirstOrDefault(row => row.BranchId == branchId)
                ?? await api.PostAsync<ReceiptDto>("/api/core/purchase-receipts", new
                {
                    branchId,
                    supplierId,
                    note = OpeningNote,
                    items = tenant.Products.SelectMany(product => product.Variants.Select(variant => new
                    {
                        skuId = skus[product.SkuCode(variant)].SkuId,
                        quantity = OpeningQuantity(product, branch),
                        unitCost = product.UnitCost,
                    })).ToList(),
                });
            if (receipt.Status != "Confirmed")
            {
                await api.PostAsync<ReceiptDto>($"/api/core/purchase-receipts/{receipt.Id}/confirm");
                Log($"opening stock confirmed at {branch.Code}");
            }
        }
    }

    private static int OpeningQuantity(ProductSeed product, BranchSeed branch) => product switch
    {
        { LowStock: true } => 2,
        { Demand: Demand.None } => 25,
        { Demand: Demand.New } => 12,
        _ => (int)Math.Ceiling(MeanPerSku(product, branch) * 18) + 4,
    };

    private static double MeanPerSku(ProductSeed product, BranchSeed branch) =>
        product.DailyDemand / product.Variants.Count * branch.DemandFactor;

    // Ngưỡng khoảng 4 ngày bán; SKU LowStock có ngưỡng 6 nên vào ngay danh sách cảnh báo.
    private async Task EnsureThresholdsAsync(Dictionary<string, Guid> branches, Dictionary<string, SkuInfo> skus)
    {
        foreach (var branch in tenant.Branches)
        {
            foreach (var product in tenant.Products)
            {
                var threshold = product.LowStock ? 6 : Math.Max(2, (int)Math.Ceiling(MeanPerSku(product, branch) * 4));
                foreach (var variant in product.Variants)
                {
                    await api.PutAsync<JsonElement>("/api/core/stock/threshold", new
                    {
                        branchId = branches[branch.Code],
                        skuId = skus[product.SkuCode(variant)].SkuId,
                        threshold,
                    });
                }
            }
        }

        Log("thresholds set");
    }

    // Hoạt động trong ngày đi qua đúng luồng thật: vài đơn POS, hai đơn thủ công chờ duyệt, một phiếu chuyển đang đi đường.
    private async Task EnsureTodayActivityAsync(Dictionary<string, Guid> branches, Dictionary<string, SkuInfo> skus)
    {
        var bestSellers = tenant.Products
            .Where(product => product is { Demand: Demand.Steady, LowStock: false })
            .OrderByDescending(product => product.DailyDemand)
            .Select(product => skus[product.SkuCode(product.Variants[0])])
            .Take(4)
            .ToList();

        foreach (var branch in tenant.Branches)
        {
            for (var index = 0; index < 3; index++)
            {
                var lines = new[] { bestSellers[index], bestSellers[index + 1] };
                // Cùng Idempotency-Key mỗi lần chạy: core trả lại đúng đơn đã tạo, không tạo đơn mới (docs/design/api/README.md).
                await api.PostAsync<OrderDto>(
                    "/api/core/pos/checkout",
                    new
                    {
                        branchId = branches[branch.Code],
                        items = lines.Select(sku => new { skuId = sku.SkuId, quantity = 1 }).ToList(),
                        payment = new { method = index % 2 == 0 ? "Cash" : "QR", amount = lines.Sum(sku => sku.RetailPrice) },
                    },
                    idempotencyKey: DeterministicGuid.From($"{tenant.Key}:{branch.Code}:pos:{index}").ToString());
            }
        }

        var phoneOrders = (await api.GetAsync<Paged<OrderDto>>("/api/core/orders?channel=Admin&page=1&pageSize=100")).Items
            .Count(order => order.Note == PhoneOrderNote);
        for (var index = phoneOrders; index < 2; index++)
        {
            await api.PostAsync<OrderDto>("/api/core/orders", new
            {
                branchId = branches[tenant.Branches[0].Code],
                note = PhoneOrderNote,
                items = new[] { new { skuId = bestSellers[index].SkuId, quantity = 2 } },
            });
        }

        if ((await api.GetAsync<Paged<TransferDto>>("/api/core/transfers?page=1&pageSize=1")).Total == 0)
        {
            var transfer = await api.PostAsync<TransferDto>("/api/core/transfers", new
            {
                fromBranchId = branches[tenant.Branches[0].Code],
                toBranchId = branches[tenant.Branches[1].Code],
                items = bestSellers.Take(2).Select(sku => new { skuId = sku.SkuId, quantity = 3 }).ToList(),
            });
            await api.PostAsync<TransferDto>($"/api/core/transfers/{transfer.Id}/ship");
        }

        Log("today's POS sales, phone orders and transfer ready");
    }

    // Shop Shopee gắn với chi nhánh đầu tiên, để bắn tải đơn sàn từ màn hình kênh sàn chạy được ngay.
    private async Task EnsureChannelShopAsync(Dictionary<string, Guid> branches)
    {
        var shops = await api.GetAsync<List<ChannelShopDto>>("/api/channel/channel-shops");
        if (shops.Any(shop => shop.ShopId == tenant.ShopId))
            return;

        await api.PostAsync<ChannelShopDto>("/api/channel/channel-shops", new
        {
            channel = "Shopee",
            shopId = tenant.ShopId,
            branchId = branches[tenant.Branches[0].Code],
        });
        Log($"created Shopee shop {tenant.ShopId}");
    }

    // insights nhận event bất đồng bộ: chờ tới khi báo cáo 60 ngày đã đủ số lượng của lịch sử vừa phát.
    private async Task WaitForHistoryAsync(int expectedQuantity)
    {
        var path = $"/api/insights/reports/gross-profit?from={today.AddDays(-SalesHistory.Days):yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}";
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (true)
        {
            var quantity = (await api.GetAsync<GrossProfitReport>(path)).Totals.Quantity;
            if (quantity >= expectedQuantity)
            {
                Log($"insights has the sales history ({quantity} units)");
                return;
            }

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"insights only has {quantity} of {expectedQuantity} historical units after 10 minutes");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    // Chạy dự báo một lần để màn hình đề xuất nhập có số ngay, không phải chờ job đêm.
    private async Task RunForecastAsync()
    {
        await api.PostAsync<JsonElement>("/api/insights/forecast/run");
        var run = (await api.GetAsync<ReorderSuggestions>("/api/insights/forecast/reorder-suggestions?page=1&pageSize=1")).Run;
        Log(run is null
            ? "forecast did not record a run"
            : $"forecast {run.Status}: WAPE forecast {run.ForecastWape?.ToString("0.0000") ?? "n/a"}, baseline {run.BaselineWape?.ToString("0.0000") ?? "n/a"} {run.Error}");
    }

    private void Log(string message) => Console.WriteLine($"[{tenant.Key}] {message}");
}
