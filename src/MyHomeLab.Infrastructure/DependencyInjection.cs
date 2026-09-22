using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Infrastructure.Docker;
using MyHomeLab.Infrastructure.Health;
using MyHomeLab.Infrastructure.Migrations;
using MyHomeLab.Infrastructure.Persistence;
using MyHomeLab.Infrastructure.System;
using MyHomeLab.Infrastructure.Terminal;
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
        services.Configure<DockerOptions>(configuration.GetSection("Docker"));
        services.AddHttpClient("health")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Homelab probes hit local services with self-signed/dev certs.
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        services.AddSingleton(dataSource);
        services.AddSingleton<IMigrationRunner, SqlMigrationRunner>();
        services.AddSingleton<IAppRepository, DapperAppRepository>();
        services.AddSingleton<IHealthHistoryRepository, DapperHealthHistoryRepository>();
        services.AddSingleton<IHealthChecker, HttpHealthChecker>();
        services.AddSingleton<ITerminalService, TerminalService>();
        services.AddSingleton<IPowerService, PowerService>();
        services.AddSingleton<IDockerService, DockerService>();

        return services;
    }
}