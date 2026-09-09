using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Blazored.FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Refit;

namespace AditiKraft.Krafter.Tests.Validation;

public sealed class ContractsFormValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoleFormDiscoversSharedValidatorAndDisplaysFieldErrors(bool tooLong)
    {
        await using var editor = new Editor();
        HtmlRootComponent root = await editor.RenderAsync<CreateOrUpdateRole>();

        await editor.RunAsync(async () =>
        {
            CreateOrUpdateRoleRequest model = editor.Form<CreateOrUpdateRoleRequest>().Data;
            model.Name = tooLong ? new string('n', 14) : "";
            model.Description = tooLong ? new string('d', 101) : "";

            Assert.False(await editor.Validator.ValidateAsync());
            string html = root.ToHtmlString();
            Assert.Contains(tooLong ? "Name cannot be longer than 13 characters" : "You must enter Name", html);
            Assert.Contains(tooLong ? "Description cannot be longer than 100 characters" : "You must enter Description", html);

            model.Name = "Editor";
            model.Description = "Can edit users";
            Assert.True(await editor.Validator.ValidateAsync());
            Assert.Empty(editor.Form<CreateOrUpdateRoleRequest>().EditContext.GetValidationMessages());
            Assert.DoesNotContain("validation-message", root.ToHtmlString());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TenantFormDiscoversSharedValidatorAndDisplaysFieldErrors(bool tooLong)
    {
        await using var editor = new Editor();
        HtmlRootComponent root = await editor.RenderAsync<CreateOrUpdateTenant>();

        await editor.RunAsync(async () =>
        {
            CreateOrUpdateTenantRequest model = editor.Form<CreateOrUpdateTenantRequest>().Data;
            model.Name = tooLong ? new string('n', 41) : "";
            model.Identifier = tooLong ? new string('i', 11) : "";
            model.AdminEmail = tooLong ? "invalid-email" : "";
            model.IsActive = null;
            model.ValidUpto = null;

            Assert.False(await editor.Validator.ValidateAsync());
            string html = root.ToHtmlString();
            Assert.Contains(tooLong ? "Name cannot be longer than 40 characters" : "You must enter Name", html);
            Assert.Contains(tooLong ? "Identifier cannot be longer than 10 characters" : "Identifier is required", html);
            Assert.Contains(tooLong ? "Invalid email format" : "Admin email is required", html);
            Assert.Contains("IsActive is required", html);
            Assert.Contains("ValidUpto is required", html);

            model.Name = "Example tenant";
            model.Identifier = "example";
            model.AdminEmail = "admin@example.com";
            model.IsActive = true;
            model.ValidUpto = new DateTime(2030, 1, 1);
            Assert.True(await editor.Validator.ValidateAsync());
            Assert.Empty(editor.Form<CreateOrUpdateTenantRequest>().EditContext.GetValidationMessages());
            Assert.DoesNotContain("validation-message", root.ToHtmlString());
        });
    }

    private sealed class Editor : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CapturingActivator components;
        private readonly HtmlRenderer renderer;

        public Editor()
        {
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<NavigationManager, TestNavigationManager>();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>();
            registrations.AddSingleton<DialogService>();
            registrations.AddSingleton<NotificationService>();
            registrations.AddSingleton<ApiCallService>();
            registrations.AddSingleton(_ => new HttpClient(new NoApiRequests()) { BaseAddress = new Uri("https://localhost/") });
            registrations.AddSingleton(provider => RestService.For<IRolesApi>(provider.GetRequiredService<HttpClient>()));
            registrations.AddSingleton(provider => RestService.For<ITenantsApi>(provider.GetRequiredService<HttpClient>()));
            // Match UI discovery: the forms find the Contracts validators without explicit validator registration.
            registrations.AddSingleton<CapturingActivator>();
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturingActivator>());
            services = registrations.BuildServiceProvider();
            components = services.GetRequiredService<CapturingActivator>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public FluentValidationValidator Validator => components.All.OfType<FluentValidationValidator>().Single();

        public RadzenTemplateForm<T> Form<T>() => components.All.OfType<RadzenTemplateForm<T>>().Single();

        public Task<HtmlRootComponent> RenderAsync<T>() where T : IComponent =>
            renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<T>());

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
        public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not call JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not call JavaScript.");
    }

    private sealed class NoApiRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Client validation must not call the API.");
    }
}
