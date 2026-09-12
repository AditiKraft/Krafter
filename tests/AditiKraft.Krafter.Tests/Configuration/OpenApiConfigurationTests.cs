using System.Text.Json;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class OpenApiConfigurationTests
{
    [Fact]
    public async Task OpenApiEndpointGeneratesRequestSchemaWithConfiguredDocumentMetadata()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSwaggerConfiguration();
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapPost("/roles", (CreateOrUpdateRoleRequest request) => Results.Ok(request));
        app.UseSwaggerConfiguration();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        Assert.StartsWith("3.0.", root.GetProperty("openapi").GetString());
        Assert.Equal("Krafter API", root.GetProperty("info").GetProperty("title").GetString());
        JsonElement requestSchema = root.GetProperty("paths").GetProperty("/roles").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        string schemaReference = Assert.IsType<string>(requestSchema.GetProperty("$ref").GetString());
        string schemaName = schemaReference.Split('/')[^1];
        JsonElement properties = root.GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties");
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("description", out _));
    }
}
