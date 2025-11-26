using System;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RouteEstimationService.Features.CreateRoute;
using RouteEstimationService.Features.EstimateTime;
using RouteEstimationService.Domain.Entities.Events;
using Serilog;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("Starting host");

    var hostBuilder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddScoped<CreateRouteConsumer>();
            services.AddScoped<ICreateRouteHandler, CreateRouteHandler>();
            services.AddScoped<IRouteMadeEmitter, RouteMadeEmitter>();
            services.AddScoped<IValidator<CreateProcessEvent>, CreateRouteValidator>();
            services.AddScoped<EstimateTimeHandler>();

            services.AddMassTransit(x =>
            {
                var rabbit = hostContext.Configuration.GetSection("RabbitMQ");
                var host = rabbit.GetValue("Host", "localhost");
                var port = rabbit.GetValue<ushort>("Port", 5672);
                var user = rabbit.GetValue("Username", "guest");
                var pass = rabbit.GetValue("Password", "guest");

                x.AddConsumer<CreateRouteConsumer>();
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    // Use host + host-port (host is usually 'localhost' for local Docker)
                    cfg.Host(host, port, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });

                    cfg.ReceiveEndpoint("estimation-requested", e => e.ConfigureConsumer<CreateRouteConsumer>(ctx));
                });
            });
        });
    using var host = hostBuilder.Build();
    
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
