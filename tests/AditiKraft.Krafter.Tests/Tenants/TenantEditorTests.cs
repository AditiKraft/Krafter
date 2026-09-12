using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Features.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class TenantEditorTests
{
    [Fact]
    public async Task NewTenantKeepsLocalInputForRefitSerialization()
    {
        await using var editor = new Editor();
        await editor.RenderAsync(new TenantDto());
        await editor.RunAsync(async () =>
        {
            await editor.Date.ValueChanged.InvokeAsync(new DateTime(2030, 6, 15, 12, 0, 0));
            editor.Form.Data.Identifier = "blue";
            editor.Form.Data.Name = "Blue";
            editor.Form.Data.AdminEmail = "admin@example.com";
            editor.Form.Data.IsActive = true;
            Assert.True(new CreateOrUpdateTenantRequestValidator().Validate(editor.Form.Data).IsValid);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });

        CreateOrUpdateTenantRequest saved = Assert.Single(editor.Api.Saved);
        Assert.Equal(DateTimeKind.Unspecified, saved.ValidUpto!.Value.Kind);
        Assert.Equal(new DateTime(2030, 6, 15, 12, 0, 0), saved.ValidUpto);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 34)]
    public async Task UnrelatedEditPreservesExistingExpiryExactly(int hour, int minute)
    {
        TenantDto tenant = ExistingTenant();
        tenant.ValidUpto = new DateTime(2030, 6, 15, hour, minute, 0, DateTimeKind.Utc).ToLocalTime();
        await using var editor = new Editor();
        await editor.RenderAsync(tenant);
        await editor.RunAsync(async () =>
        {
            editor.Form.Data.Name = "Changed name";
            await editor.Date.ValueChanged.InvokeAsync((DateTime?)editor.Date.Value);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });
        Assert.Equal(tenant.ValidUpto, Assert.Single(editor.Api.Saved).ValidUpto);
    }

    [Fact]
    public async Task ExistingExpiryDisplaysInBrowserLocalTime()
    {
        TenantDto tenant = ExistingTenant();
        tenant.ValidUpto = new DateTime(2030, 6, 16, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
        await using var editor = new Editor();
        await editor.RenderAsync(tenant);
        await editor.RunAsync(async () =>
        {
            Assert.Equal(tenant.ValidUpto.ToLocalTime(), editor.Date.Value);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });
        Assert.Equal(tenant.ValidUpto, Assert.Single(editor.Api.Saved).ValidUpto);
    }

    [Fact]
    public async Task RootTenantKeepsUnlimitedValidity()
    {
        TenantDto tenant = ExistingTenant();
        tenant.Id = tenant.Identifier = "root";
        tenant.ValidUpto = DateTime.MaxValue;
        await using var editor = new Editor();
        await editor.RenderAsync(tenant);
        await editor.RunAsync(async () =>
        {
            Assert.True(new CreateOrUpdateTenantRequestValidator().Validate(editor.Form.Data).IsValid);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });
        Assert.Equal(DateTime.MaxValue, Assert.Single(editor.Api.Saved).ValidUpto);
    }

    private static TenantDto ExistingTenant() => new()
    {
        Id = "blue-id", Identifier = "blue", Name = "Blue", AdminEmail = "admin@example.com", IsActive = true,
        ValidUpto = new DateTime(2030, 6, 16, 0, 0, 0, DateTimeKind.Utc)
    };

    private sealed class Editor : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CapturingActivator components;
        private readonly HtmlRenderer renderer;
        public TenantsApi Api { get; } = new();

        public Editor()
        {
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<NavigationManager, TestNavigationManager>();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>();
            registrations.AddSingleton<DialogService>();
            registrations.AddSingleton<NotificationService>();
            registrations.AddSingleton<ApiCallService>();
            registrations.AddSingleton<ITenantsApi>(Api);
            registrations.AddSingleton<CapturingActivator>();
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturingActivator>());
            services = registrations.BuildServiceProvider();
            components = services.GetRequiredService<CapturingActivator>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public RadzenTemplateForm<CreateOrUpdateTenantRequest> Form => components.All.OfType<RadzenTemplateForm<CreateOrUpdateTenantRequest>>().Single();
        public RadzenDatePicker<DateTime?> Date => components.All.OfType<RadzenDatePicker<DateTime?>>().Single();

        public Task RenderAsync(TenantDto tenant) => renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.RenderComponentAsync<CreateOrUpdateTenant>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CreateOrUpdateTenant.TenantInput)] = tenant })));

        public Task RunAsync(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class CapturingActivator(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
            All.Add(component);
            return component;
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/tenants");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("Date conversion must not call JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private sealed class TenantsApi : ITenantsApi
    {
        public List<CreateOrUpdateTenantRequest> Saved { get; } = [];
        public Task<Response> CreateTenantAsync(CreateOrUpdateTenantRequest request, CancellationToken cancellationToken = default)
        {
            Saved.Add(request);
            return Task.FromResult(new Response());
        }
        public Task<Response> UpdateTenantAsync(string id, CreateOrUpdateTenantRequest request, CancellationToken cancellationToken = default) =>
            CreateTenantAsync(request, cancellationToken);
        public Task<Response<PaginationResponse<TenantDto>>> GetTenantsAsync(GetRequestInput request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Response> DeleteTenantAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Response> SeedDataAsync(SeedDataRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
