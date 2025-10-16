using Microsoft.AspNetCore.Builder;
using translator_service.API;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//app.MapGet("/", () => "Hello, world from .NET 9!");

//Link til CreateProcessEndpoint
app.MapCreateProcessEndpoint();

app.Run();