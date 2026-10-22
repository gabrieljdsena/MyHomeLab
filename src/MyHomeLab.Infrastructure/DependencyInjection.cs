using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Infrastructure.Auth;
using MyHomeLab.Infrastructure.Deploy;
using MyHomeLab.Infrastructure.Docker;
using MyHomeLab.Infrastructure.FileServer;
using MyHomeLab.Infrastructure.Health;
using MyHomeLab.Infrastructure.Lan;
using MyHomeLab.Infrastructure.Migrations;
using MyHomeLab.Infrastructure.Persistence;
using MyHomeLab.Infrastructure.System;
using MyHomeLab.Infrastructure.Terminal;
using MyHomeLab.Infrastructure.Toolchain;
using Npgsql;

namespace MyHomeLab.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMyHomeLabInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyHomeLab");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'MyHomeLab' is not configured.");
        }

        var password = Environment.GetEnvironmentVariable("MYHOMELAB_DB_PASSWORD")
                       ?? Environment.GetEnvironmentVariable("DB_PASS");

        if (!string.IsNullOrWhiteSpace(password))
        {
            var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString) { Password = password };
            connectionString = connectionBuilder.ConnectionString;
        }

        var dataSource = NpgsqlDataSource.Create(connectionString);

        services.Configure<HealthOptions>(configuration.GetSection("Health"));
        services.Configure<TerminalOptions>(configuration.GetSection("Terminal"));
        services.Configure<ToolchainOptions>(configuration.GetSection("Toolchain"));
        services.Configure<FileServerOptions>(configuration.GetSection("FileServer"));
        services.Configure<DockerOptions>(configuration.GetSection("Docker"));
        services.Configure<DeployOptions>(configuration.GetSection("Deploy"));
        services.Configure<WindowsLanOptions>(configuration.GetSection("Machines"));
        services.Configure<PostgresWorkbenchOptions>(configuration.GetSection("PostgresWorkbench"));

        var authOptions = AuthOptions.Load(configuration);
        services.AddSingleton(authOptions);
        services.AddSingleton(new AuthPolicy(
            TimeSpan.FromDays(authOptions.SessionDays),
            TimeSpan.FromMinutes(authOptions.TouchIntervalMinutes),
            TimeSpan.FromHours(authOptions.ReissueAfterHours)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuthTokenService, JwtTokenService>();
        services.AddSingleton<IAuthRepository, DapperAuthRepository>();
        services.AddHttpClient("health")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Homelab probes hit local services with self-signed/dev certs.
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        services.AddSingleton(dataSource);
        services.AddSingleton(new NpgsqlConnectionSource(connectionString));
        services.AddSingleton<IMigrationRunner, SqlMigrationRunner>();
        services.AddSingleton<IAppRepository, DapperAppRepository>();
        services.AddSingleton<IHealthHistoryRepository, DapperHealthHistoryRepository>();
        services.AddSingleton<IHealthChecker, HttpHealthChecker>();
        services.AddSingleton<ITerminalService, TerminalService>();
        services.AddSingleton<ITerminalPtyService, TerminalPtyService>();
        services.AddSingleton<IToolchainService, ToolchainService>();
        services.AddSingleton<IFileServerService, FileSystemFileService>();
        services.AddSingleton<IPowerService, PowerService>();
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IDeployService, DeployService>();
        services.AddSingleton<IMachineRepository, DapperMachineRepository>();
        services.AddSingleton<ILogRepository, DapperLogRepository>();
        services.AddSingleton<IMachineReachabilityProbe, WindowsLanReachabilityProbe>();
        services.AddSingleton<ILanDiscoveryService, LanDiscoveryService>();
        services.AddSingleton<IPostgresMetricsReader, DapperPostgresMetricsReader>();
        services.AddSingleton<ISqlExecutor, NpgsqlSqlExecutor>();
        services.AddSingleton<IPostgresCatalogReader, NpgsqlPostgresCatalogReader>();

        return services;
    }
}