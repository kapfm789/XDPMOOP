using Oism.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOismWeb(builder.Configuration);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();
app.UseOismWeb();
app.MapReverseProxy();

app.Run();
