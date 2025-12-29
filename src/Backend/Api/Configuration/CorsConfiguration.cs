namespace Backend.Api.Configuration;

public static class CorsConfiguration
{
    private const string PolicyName = "AllowSpecificOrigins";

    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string allowedCorsDomains = configuration["AllowedCorsDomains"]
                                    ?? throw new InvalidOperationException(
                                        "Configuration 'AllowedCorsDomains' not found");

        string[] allowedDomains = allowedCorsDomains.Split(",", StringSplitOptions.RemoveEmptyEntries);

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policyBuilder =>
            {
                policyBuilder.SetIsOriginAllowed(origin =>
                    {
                        if (environment.IsDevelopment())
                        {
                            return true; // Allow all in development
                        }

                        var uri = new Uri(origin);
                        return allowedDomains.Any(domain =>
                            uri.Host == domain || uri.Host.EndsWith($".{domain}"));
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) => app.UseCors(PolicyName);
}
