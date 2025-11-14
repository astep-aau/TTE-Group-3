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
    int exitCode = OSMNodeExtractor.OSMNodeExtractor.Extract(args.Skip(1).ToArray());
    Environment.Exit(exitCode);
}

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("Starting host");

    var hostbuilder = Host.CreateDefaultBuilder(args)
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
                    cfg.ReceiveEndpoint("estimation-requested", e => e.ConfigureConsumer<CreateRouteConsumer>(ctx));
                });
            });
            services.AddTransient<CreateRouteConsumer>();
        });
    using var host = hostbuilder.Build();
    
    // Create a scope and simulate a CreateProcessEvent once on startup.
    using var scope = host.Services.CreateScope();
    var consumer = scope.ServiceProvider.GetRequiredService<CreateRouteConsumer>();

    var testEvent = new CreateProcessEvent
    {
        ProcessId = 123,
        CorrelationId = Guid.NewGuid(),
        Origin = "45.7821345,126.5570674",
        Destination = "45.7683933,126.5753343",
        TimeOfTravel = TimeOnly.FromDateTime(DateTime.UtcNow),
        CreatedAt = DateTime.UtcNow,
        ModelVersion = "test-v1"
    };

    // Call the public handler to simulate an incoming message
    await consumer.HandleEventAsync(testEvent);
    
    // Run the host (console lifetime) so the app keeps running as before
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "[Program]Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
