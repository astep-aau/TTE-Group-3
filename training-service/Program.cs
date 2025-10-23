using Microsoft.OpenApi.Models;
using TrainingService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Required for Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Training Service API",
        Description = "API for managing and training LSTM models"
    });
});

builder.Services.AddScoped<TrainingService.Services.TrainingService>();

var app = builder.Build();

// ✅ Enable Swagger for all environments (not just Development)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Training Service API v1");
    c.RoutePrefix = string.Empty; // Optional: make Swagger available at http://localhost:5000/
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run(); 