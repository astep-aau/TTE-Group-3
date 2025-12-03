using System.Net;
using Microsoft.OpenApi.Models;
using TrainingService.Configuration;
using TrainingService.Services;
using TrainingService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings from parent directory
string parentDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName 
                   ?? Directory.GetCurrentDirectory();

builder.Configuration
    .SetBasePath(parentDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.Configure<PythonBackendSettings>(builder.Configuration.GetSection("PythonBackend"));

// Register HttpClient for Python backend
builder.Services.AddHttpClient("PythonBackend", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
});

// Register DI services
builder.Services.AddSingleton<Service>();
builder.Services.AddSingleton<TrainingQueue>();
builder.Services.AddSingleton<ITrainingQueue>(sp => sp.GetRequiredService<TrainingQueue>());
builder.Services.AddHostedService<TrainingWorker>();
builder.Services.AddSingleton<ApiKeyAuthFilter>();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "customFormatter";
});

builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. Enter your API key in the text input below.",
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();