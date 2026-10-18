using Oism.BuildingBlocks.Web;
using Oism.Gateway.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddSwaggerAggregation();

var app = builder.Build();

app.UseCors();
// Giống từng service: Swagger chỉ bật ở Development và đứng trước xác thực.
if (app.Environment.IsDevelopment())
    app.UseSwaggerAggregation();
app.UseOismWeb();
app.MapReverseProxy();

app.Run();
