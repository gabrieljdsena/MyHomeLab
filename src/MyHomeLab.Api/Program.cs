using Serilog;
using Serilog.Events;
using MyHomeLab.Api.Middleware;
using MyHomeLab.Api.Services;
using MyHomeLab.Application.Services;
using MyHomeLab.Infrastructure;
using MyHomeLab.Infrastructure.Migrations;

var builder = WebApplication.CreateBuilder(args);

if (!Environment.UserInteractive)
{
    builder.Host.UseWindowsService();
    builder.WebHost.UseContentRoot(AppContext.BaseDirectory);
    builder.Configuration["Serilog:WriteTo:1:Args:path"] =
        Path.Combine(AppContext.BaseDirectory, "logs", "myhomelab-.log");
}

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "MyHomeLab API",
        Version = "v1",
        Description = "Hub registry for applications running on this lab server.",
    });
});

builder.Services.AddMyHomeLabInfrastructure(builder.Configuration);
builder.Services.AddScoped<AppService>();
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddSingleton<SystemSensorService>();
builder.Services.AddSingleton<StorageSmartService>();
builder.Services.AddHostedService<HealthCheckBackgroundService>();

var devOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty;
builder.Services.AddCors(options => options.AddPolicy("dev", policy =>
{
    var origins = devOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length > 0)
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    await runner.ApplyAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyHomeLab API v1"));
app.UseCors("dev");
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

TryLogStartupUrls(app);

app.Run();

static void TryLogStartupUrls(WebApplication app)
{
    foreach (var address in app.Urls)
    {
        app.Logger.LogInformation("Now listening on {Address}", address);
    }
}

public partial class Program;