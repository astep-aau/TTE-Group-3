using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StateService.Infrastructure.Persistence;
using StateService.Infrastructure.Messaging;
using System.Diagnostics;
using StateService.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace StateService.API
{
    public static class HealthEndpoint
    {
        public static void MapHealth(this WebApplication app)
        {
            app.MapGet("/health", async (HttpContext ctx, ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("HealthEndpoint");
                using var activity = ActivitySourceHolder.Source.StartActivity("http.health", ActivityKind.Server);

                var db = ctx.RequestServices.GetRequiredService<StateDbContext>();
                var rabbit = ctx.RequestServices.GetRequiredService<IRabbitMqConnection>();
                var dbHealthy = await db.Database.CanConnectAsync();
                var rabbitHealthy = rabbit.IsConnected;
                var status = dbHealthy && rabbitHealthy ? "healthy" : "degraded";

                activity?.SetTag("service.name", "StateService");
                activity?.SetTag("health.status", status);
                activity?.SetTag("db.healthy", dbHealthy);
                activity?.SetTag("rabbit.healthy", rabbitHealthy);

                logger.LogInformation("Health check completed. Status: {Status}, DbHealthy: {DbHealthy}, RabbitHealthy: {RabbitHealthy}", status, dbHealthy, rabbitHealthy);

                var result = new { status, dbHealthy, rabbitHealthy };
                await ctx.Response.WriteAsJsonAsync(result);
            });

            app.MapGet("/ready", async (HttpContext ctx, ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("HealthEndpoint");
                using var activity = ActivitySourceHolder.Source.StartActivity("http.ready", ActivityKind.Server);

                var db = ctx.RequestServices.GetRequiredService<StateDbContext>();
                var rabbit = ctx.RequestServices.GetRequiredService<IRabbitMqConnection>();
                var ready = await db.Database.CanConnectAsync() && rabbit.IsConnected;

                activity?.SetTag("service.name", "StateService");
                activity?.SetTag("ready", ready);

                logger.LogInformation("Readiness check completed. Ready: {Ready}", ready);

                var result = new { ready };
                await ctx.Response.WriteAsJsonAsync(result);
            });
        }
    }
}
