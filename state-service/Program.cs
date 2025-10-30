using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using StateService.API;
using StateService.Features.EstimationRequested;
using StateService.Features.ProcessFinished;
using StateService.Features.RouteCalculated;
using StateService.Features.TimeEstimated;
using StateService.Infrastructure.Messaging;
using StateService.Infrastructure.Observability;
using StateService.Infrastructure.Persistence;
using StateService.Infrastructure.Services;
using StateService.Infrastructure.Workers;

namespace StateService;

public class Program
{
    public static async System.Threading.Tasks.Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

        var serviceName = "StateService";
        var serviceVersion = "1.0.0";
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.ResourceAttributes = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.version"] = serviceVersion
                };
            })
            .CreateLogger();

        builder.Services.AddLogging(lb => lb.ClearProviders().AddSerilog());
        builder.Services.AddSingleton(MetricsRegistry.Meter);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(t => t
                .AddSource(ActivitySourceHolder.Source.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddMeter(MetricsRegistry.Meter.Name)
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        var connString = builder.Configuration.GetConnectionString("StateDb");
        builder.Services.AddDbContext<StateDbContext>(opt => opt.UseNpgsql(connString));
        builder.Services.AddScoped<IStateMachineService, StateMachineService>();
        builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();

        AsyncRetryPolicy retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt), (ex, ts, attempt, ctx) =>
            {
                Log.Warning(ex, "Transient failure attempt={Attempt} delay={Delay}ms", attempt, ts.TotalMilliseconds);
            });
        builder.Services.AddSingleton(retryPolicy);

        builder.Services.AddScoped<IValidator<EstimationRequestedMessage>, EstimationRequestedMessageValidator>();
        builder.Services.AddScoped<IValidator<RouteCalculatedMessage>, RouteCalculatedMessageValidator>();
        builder.Services.AddScoped<IValidator<TimeEstimatedMessage>, TimeEstimatedMessageValidator>();
        builder.Services.AddScoped<IValidator<ProcessFinishedMessage>, ProcessFinishedMessageValidator>();

        builder.Services.AddScoped<EstimationRequestedHandler>();
        builder.Services.AddHostedService<EstimationRequestedConsumer>();
        builder.Services.AddScoped<RouteCalculatedHandler>();
        builder.Services.AddHostedService<RouteCalculatedConsumer>();
        builder.Services.AddScoped<TimeEstimatedHandler>();
        builder.Services.AddHostedService<TimeEstimatedConsumer>();
        builder.Services.AddScoped<ProcessFinishedHandler>();
        builder.Services.AddHostedService<ProcessFinishedConsumer>();
        builder.Services.AddHostedService<OutboxDispatcher>();

        builder.Services.AddRouting();
        builder.Services.AddEndpointsApiExplorer();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("State Service starting...");

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StateDbContext>();
            await retryPolicy.ExecuteAsync(() => db.Database.MigrateAsync());
        }

        var webAppBuilder = WebApplication.CreateBuilder();
        webAppBuilder.Configuration.AddConfiguration(builder.Configuration);
        webAppBuilder.Services.AddDbContext<StateDbContext>(opt => opt.UseNpgsql(connString));
        webAppBuilder.Services.AddSingleton(app.Services.GetRequiredService<IRabbitMqConnection>());

        webAppBuilder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(t => t
                .AddSource(ActivitySourceHolder.Source.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddMeter(MetricsRegistry.Meter.Name)
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        webAppBuilder.Logging.ClearProviders();
        webAppBuilder.Logging.AddSerilog();

        var healthApp = webAppBuilder.Build();
        healthApp.MapHealth();
        _ = healthApp.RunAsync();

        logger.LogInformation("State Service initialized.");
        await app.RunAsync();
    }
}