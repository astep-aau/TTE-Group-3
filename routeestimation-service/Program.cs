using System;
using System.Linq;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RouteEstimationService.Features.CreateRoute;
using RouteEstimationService.Domain.Entities.Events;
using Serilog;

// allow running the OSM extractor directly via: dotnet run -- extract <pbf> <json> <csv>
if (args.Length > 0 && string.Equals(args[0], "extract", StringComparison.OrdinalIgnoreCase))
{
    var exitCode = OSMNodeExtractor.OSMNodeExtractor.Extract(args.Skip(1).ToArray());
    Environment.Exit(exitCode);
}

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
                    cfg.ReceiveEndpoint("routeestimation-create-route", e => e.ConfigureConsumer<CreateRouteConsumer>(ctx));
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
