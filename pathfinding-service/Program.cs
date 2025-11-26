using System;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PathfindingService.Domain.Entities.Events;
using PathfindingService.Features.CreateRoute;
using Serilog;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("Starting host");

    await Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((_, services) =>
        {
            services.AddScoped<CreateRouteConsumer>();
            services.AddScoped<CreateRouteHandler>();
            services.AddScoped<IValidator<CreateProcessEvent>, CreateRouteValidator>();

            services.AddMassTransit(x =>
            {
                x.AddConsumer<CreateRouteConsumer>();
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
                    cfg.ReceiveEndpoint("pathfinding-create-route", e => e.ConfigureConsumer<CreateRouteConsumer>(ctx));
                });
            });
        })
        .RunConsoleAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
