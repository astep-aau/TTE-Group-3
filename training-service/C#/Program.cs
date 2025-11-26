using TrainingService.Services;
using TrainingService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register DI services
builder.Services.AddSingleton<Service>();
builder.Services.AddSingleton<TrainingQueue>();
builder.Services.AddHostedService<TrainingWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();