using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Web;
using Oism.Contracts;
using Oism.Core.Api.Consumers;
using Oism.Core.Application.References;
using Oism.Core.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
// Mỗi use case là một lớp *Handler ở Application, đăng ký thẳng vào DI.
foreach (var handler in typeof(IReferenceRepository).Assembly.GetTypes().Where(type => type.Name.EndsWith("Handler")))
    builder.Services.AddScoped(handler);
builder.Services.AddEventConsumer<SkuUpsertedConsumer, SkuUpserted>();
builder.Services.AddEventConsumer<BranchUpsertedConsumer, BranchUpserted>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.Services.MigrateDatabaseAsync();
}

app.UseOismWeb();
app.MapControllers();

app.Run();

public partial class Program;
