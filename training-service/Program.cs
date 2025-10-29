using TrainingService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to DI container
builder.Services.AddControllers();

// ✅ Register your TrainingService
builder.Services.AddSingleton<TrainingService.Services.TrainingService>();
builder.Services.AddSingleton<VectorEmbeddingService.Services.VectorEmbeddingService>();

// Add Swagger if needed
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Use Swagger if you want
app.UseSwagger();
app.UseSwaggerUI();

// Map controllers
app.MapControllers();

app.Run();