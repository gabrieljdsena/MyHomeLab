using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Http.Features;
using MyHomeLab.Api.Auth;
using MyHomeLab.Api.Endpoints;
using MyHomeLab.Api.Middleware;
using MyHomeLab.Api.Services;
using MyHomeLab.Application.Services;
using MyHomeLab.Infrastructure;
using MyHomeLab.Infrastructure.Logging;
using MyHomeLab.Infrastructure.Migrations;
using Npgsql;

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
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    // Every Error/Fatal also lands in the logs table (application='myhomelab') so the
    // /logs page shows hub failures. The sink swallows its own failures, so a down
    // database can never break the path that is trying to report the error.
    .WriteTo.Sink(
        new PostgresLogSink(services.GetRequiredService<NpgsqlDataSource>()),
        restrictedToMinimumLevel: LogEventLevel.Error));

builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(options =>
{
    // Single-file uploads above this size must use the chunked session protocol
    // (POST /api/files/uploads/*); chunks are 8 MB, well under this cap.
    options.MultipartBodyLengthLimit = 32 * 1024 * 1024;
    options.MemoryBufferThreshold = 64 * 1024;
});
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
builder.Services.AddMyHomeLabAuth(builder.Configuration);
builder.Services.AddScoped<AppService>();
builder.Services.AddScoped<MachineService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SqlWorkbenchService>();
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddSingleton<SystemSensorService>();
builder.Services.AddSingleton<StorageSmartService>();
builder.Services.AddSingleton<NetworkThroughputService>();
builder.Services.AddSingleton<PostgresMetricsService>();
builder.Services.AddHostedService<HealthCheckBackgroundService>();
builder.Services.AddHostedService<MachineReachabilityService>();
builder.Services.AddHostedService<LanDiscoveryBackgroundService>();

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
    try
    {
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        await runner.ApplyAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration failed; MyHomeLab cannot start.");
        throw;
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
// The spec is an endpoint (not middleware) so the global auth policy covers it.
// The UI shell stays public; it renders nothing useful without the spec.
app.MapSwagger().RequireAuthorization();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyHomeLab API v1"));
app.UseCors("dev");
app.UseWebSockets();
app.UseStaticFiles();

app.UseMyHomeLabAuth();

app.MapControllers();
app.MapTerminalPty();
app.MapFallbackToFile("index.html").AllowAnonymous();

TryLogStartupUrls(app);
TryLogToolchain(app);

app.Run();

static void TryLogStartupUrls(WebApplication app)
{
    foreach (var address in app.Urls)
    {
        app.Logger.LogInformation("Now listening on {Address}", address);
    }
}

static void TryLogToolchain(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var toolchain = scope.ServiceProvider.GetRequiredService<MyHomeLab.Application.Abstractions.IToolchainService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tools = toolchain.ProbeAllAsync(cts.Token).GetAwaiter().GetResult();
        app.Logger.LogInformation(
            "Toolchain: {Summary}",
            string.Join(", ", tools.Select(t => t.Found ? $"{t.Name}={t.Path} ({t.Version})" : $"{t.Name}=missing")));
        if (tools.Any(t => t.Name == "node" && !t.Found))
        {
            app.Logger.LogWarning(
                "Node.js was not found on PATH. Interactive terminal sessions will fail. Run scripts/install-tools.ps1 elevated, then restart the service.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Toolchain probe failed; interactive features may be degraded.");
    }
}

public partial class Program;