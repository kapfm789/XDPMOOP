using Oism.BuildingBlocks.Web;
using Oism.Catalog.Application;
using Oism.Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
// Mỗi use case là một lớp *Handler ở Application, đăng ký thẳng vào DI.
foreach (var handler in typeof(IUnitOfWork).Assembly.GetTypes().Where(type => type.Name.EndsWith("Handler")))
    builder.Services.AddScoped(handler);
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
