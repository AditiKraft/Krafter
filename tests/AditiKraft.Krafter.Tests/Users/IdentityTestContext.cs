using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Users;

internal sealed class IdentityTestContext : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    public IdentityTestContext()
    {
        var services = new ServiceCollection();
        string databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddSingleton<ICurrentUser, CurrentUser>();
        services.AddSingleton<CurrentTenantService>();
        services.AddSingleton<ITenantGetterService>(p => p.GetRequiredService<CurrentTenantService>());
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContext<TenantDbContext>(options => options.UseInMemoryDatabase($"{databaseName}-tenants"));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<RecordingJobService>();
        services.AddSingleton<IJobService>(p => p.GetRequiredService<RecordingJobService>());
        services.AddScoped<IUserMutationService, UserMutationService>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        SetTenant("tenant-a");
    }

    public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    public TenantDbContext TenantDb => _scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    public UserManager<ApplicationUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    public RoleManager<ApplicationRole> Roles => _scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    public IUserMutationService Mutations => _scope.ServiceProvider.GetRequiredService<IUserMutationService>();
    public CurrentTenantService Tenant => _provider.GetRequiredService<CurrentTenantService>();
    public RecordingJobService Jobs => _provider.GetRequiredService<RecordingJobService>();

    public void SetTenant(string id)
        => Tenant.SetTenant(new CurrentTenantDetails { Id = id, Name = id, TenantLink = $"https://{id}.example.com" });

    public async Task<ApplicationRole> AddRoleAsync(string name)
    {
        var role = new ApplicationRole(name) { Id = Guid.NewGuid().ToString() };
        IdentityResult result = await Roles.CreateAsync(role);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return role;
    }

    public async Task<ApplicationUser> AddUserAsync(string email = "user@example.com")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "First",
            LastName = "Last",
            PhoneNumber = "123456",
            UserName = email,
            Email = email,
            IsActive = true
        };
        IdentityResult result = await Users.CreateAsync(user, "TestPassword1!");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    internal sealed class RecordingJobService : IJobService
    {
        public List<SendEmailRequestInput> Emails { get; } = [];

        public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Emails.Add(Assert.IsType<SendEmailRequestInput>(requestInput));
            return Task.CompletedTask;
        }
    }
}
