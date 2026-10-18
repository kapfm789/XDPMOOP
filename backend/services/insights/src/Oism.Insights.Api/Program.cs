using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Web;
using Oism.Contracts;
using Oism.Insights.Api.Consumers;
using Oism.Insights.Api.Hubs;
using Oism.Insights.Application;
using Oism.Insights.Application.Notifications;
using Oism.Insights.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
// Mỗi use case là một lớp *Handler ở Application, đăng ký thẳng vào DI.
foreach (var handler in typeof(IClock).Assembly.GetTypes().Where(type => type.Name.EndsWith("Handler")))
    builder.Services.AddScoped(handler);
builder.Services.AddEventConsumer<OrderConfirmedConsumer, OrderConfirmed>();
builder.Services.AddEventConsumer<OrderReservedConsumer, OrderReserved>();
builder.Services.AddEventConsumer<OrderCancelledConsumer, OrderCancelled>();
builder.Services.AddEventConsumer<OrderRejectedConsumer, OrderRejected>();
builder.Services.AddEventConsumer<StockChangedConsumer, StockChanged>();
builder.Services.AddEventConsumer<SkuUpsertedConsumer, SkuUpserted>();
// Thông báo SignalR gửi sau khi consumer commit: cùng một instance cho mỗi thông điệp.
builder.Services.AddSignalR();
builder.Services.AddScoped<SignalRNotificationPublisher>();
builder.Services.AddScoped<INotificationPublisher>(provider => provider.GetRequiredService<SignalRNotificationPublisher>());
builder.Services.AddScoped<IAfterCommitAction>(provider => provider.GetRequiredService<SignalRNotificationPublisher>());
builder.Services.AddControllers();
builder.Services.AddOismSwagger("insights");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.Services.MigrateDatabaseAsync();
}

app.UseOismWeb();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

public partial class Program;
