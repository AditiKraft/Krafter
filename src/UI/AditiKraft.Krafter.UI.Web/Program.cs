using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
    .ValidateDataAnnotations()
    .Validate<IHostEnvironment>((settings, environment) =>
            environment.IsDevelopment() || settings.HasPrivateSigningKey(),
        "Configure a private SecuritySettings:JwtSettings:Key of at least 32 UTF-8 bytes outside Development; the public example key is not allowed.")
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureBlazorJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

builder.Services.AddUiHostServices(builder.Configuration);
WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.MapDefaultEndpoints();
app.MapUiUrlConfiguration();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Intercept auth responses (login, refresh, external-auth) to set HttpOnly cookies
app.UseMiddleware<AuthCookieMiddleware>();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);

app.MapAuthTokenBffEndpoints();
app.MapTwoFactorBffEndpoints();
app.MapGoogleOAuthEndpoints();

app.Run();
