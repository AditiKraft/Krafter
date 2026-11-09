using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Migrator;
using AditiKraft.Krafter.Backend.Web.Authentication;
using AditiKraft.Krafter.Backend.Web.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddTransient<ApiDbInitializer>();
builder.Services.AddDatabaseConfiguration(builder.Configuration);
builder.Services.AddCurrentUserServices();
builder.Services.AddTenantServices();

using IHost app = builder.Build();
return await MigrationHost.RunAsync(app,
    cancellationToken => app.Services.GetRequiredService<ApiDbInitializer>().InitializeAsync(cancellationToken));
