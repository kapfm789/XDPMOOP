using Oism.BuildingBlocks.Web;
using Oism.Core.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
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
