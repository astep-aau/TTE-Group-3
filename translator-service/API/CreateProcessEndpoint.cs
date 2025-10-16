using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace translator_service.API;

public static class CreateProcessEndpoint
{
    public static void MapCreateProcessEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/processes");

        group.MapPost("/", ([FromBody] JsonElement payload) =>
            {
                if (!TryGetName(payload, out var name))
                    return Results.BadRequest(new
                        { error = "The 'name' field is required and must be a non-empty string." });

                var id = Guid.NewGuid();
                var location = $"/api/processes/{id}";
                return Results.Created(location, new { id, name });
            })
            .WithName("CreateProcess")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static bool TryGetName(JsonElement payload, out string name)
    {
        name = string.Empty;

        if (!payload.TryGetProperty("name", out var nameProp))
            return false;

        if (nameProp.ValueKind != JsonValueKind.String)
            return false;

        var raw = nameProp.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        name = raw!;
        return true;
    }
}


