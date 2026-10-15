using Oism.Seed;

// Seed data của gói demo (W4-09). Cách chạy và tài khoản: README.md cùng thư mục.
var gatewayUrl = new Uri(Setting("SEED_GATEWAY_URL", "http://localhost:8080"));
var rabbitMq = Setting("SEED_RABBITMQ", "amqp://oism:oism@localhost:5672");
var password = Setting("SEED_PASSWORD", "Demo@2026");

// Chỉ chạy trên máy dev hoặc mạng Compose (.claude/rules/deploy.md): gateway phải là localhost hoặc service `gateway`.
if (gatewayUrl.Host is not ("localhost" or "127.0.0.1" or "gateway"))
{
    Console.Error.WriteLine($"Refusing to seed {gatewayUrl}: only localhost or the Compose gateway are allowed");
    return 1;
}

// Hôm nay theo giờ Việt Nam: lịch sử bán dừng ở hết ngày hôm qua.
var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
using var http = new HttpClient { BaseAddress = gatewayUrl, Timeout = TimeSpan.FromMinutes(2) };
await using var history = new HistoryPublisher(rabbitMq);

foreach (var tenant in SeedCatalog.Tenants)
    await new TenantSeeder(new GatewayClient(http), history, tenant, password, today).RunAsync();

Console.WriteLine("Seed done. Accounts (password from SEED_PASSWORD):");
foreach (var tenant in SeedCatalog.Tenants)
    Console.WriteLine($"  {tenant.TenantName}: {tenant.OwnerEmail} (Owner), {tenant.StaffEmail} (Staff), {tenant.CashierEmail} (Cashier)");
return 0;

static string Setting(string name, string fallback) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
