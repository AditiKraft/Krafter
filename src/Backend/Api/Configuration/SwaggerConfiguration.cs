using Scalar.AspNetCore;

namespace Backend.Api.Configuration;

/// <summary>
/// Swagger/OpenAPI configuration
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi(options =>
        {
            options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Krafter API";
                document.Info.Version = "v1";
                document.Info.Description = "Krafter Backend API with VSA (Vertical Slice Architecture)";
                return Task.CompletedTask;
            });
        });
        return services;
    }

    public static IEndpointRouteBuilder UseSwaggerConfiguration(this IEndpointRouteBuilder app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }
}
