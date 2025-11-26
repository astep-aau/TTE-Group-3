using Microsoft.OpenApi.Models;
using translator_service.Features.GetRoute;
using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using translator_service;
using translator_service.API;
using translator_service.Features.CreateProcess;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RouteService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/route-service-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add database configuration

builder.Services.AddDbContext<TranslatorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// Add controller services
builder.Services.AddControllers();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Route Service API",
        Version = "v1",
        Description = "API for retrieving and storing routes"
    });
});

// Add MassTransit for service bus communication
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GetRouteConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMQ");
        var host = rabbit.GetValue<string>("Host", "localhost");
        var port = rabbit.GetValue<ushort>("Port", 5672);
        var user = rabbit.GetValue<string>("Username", "guest");
        var pass = rabbit.GetValue<string>("Password", "guest");
        var routeMadeQueue = rabbit.GetValue<string>("RouteMadeQueue", "route-made");

        cfg.Host(host, port, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ReceiveEndpoint(routeMadeQueue, e =>
        {
            e.ConfigureConsumer<GetRouteConsumer>(context);
        });
    });
});

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy  =>
        {
            // Allow your frontend's origin
            policy.WithOrigins("http://localhost:3000",
                    "https://cs-astep02.srv.aau.dk",
                    "http://cs-astep02.srv.aau.dk",
                    "http://cs-25-sw-5-03.cs-astep02.srv.aau.dk",
                    "https://cs-25-sw-5-03.cs-astep02.srv.aau.dk")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


// Register your handler and repository
builder.Services.AddScoped<GetRouteHandler>();
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<RouteDeliveredEmitter>();

// CreateProcess registrations (required for endpoint to resolve handler/repo)
builder.Services.AddScoped<CreateProcessHandler>();
builder.Services.AddScoped<ICreateProcessRepository, CreateProcessRepository>();
builder.Services.AddScoped<CreateProcessEmitter>();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TranslatorDbContext>();
    dbContext.Database.EnsureCreated();
}

// Add request logging middleware
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{ 
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Route Service API v1");
    options.RoutePrefix = string.Empty;
});

app.UseCors(myAllowSpecificOrigins);

// Map controllers
app.MapControllers();

try
{
    Log.Information("Starting Route Service API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

