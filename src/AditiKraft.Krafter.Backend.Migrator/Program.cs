using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Backend.Common.Context.Tenants;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Migrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<ApiDbInitializer>();
builder.Services.AddDatabaseConfiguration(builder.Configuration);
builder.Services.AddCurrentUserServices();
builder.Services.AddTenantServices();

IHost app = builder.Build();

await app.RunAsync();
