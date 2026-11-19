using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Notifications;

public sealed class EmailTemplateEncodingTests
{
    private const string FirstName = "<a href='https://attacker.example'>Verify</a>";
    private const string LastName = "O'Neil & Sons <img src=x>";
    private const string TenantName = "<a href='https://e.test'>Team</a>";

    [Fact]
    public async Task AccountEmailTreatsValidMarkupNamesAsTextAndKeepsItsLoginLinkAndPassword()
    {
        await using var context = new IdentityTestContext();
        await context.AddRoleAsync(RoleConstants.Basic);
        context.Tenant.Tenant.Name = TenantName;
        var request = new CreateUserRequest
        {
            FirstName = FirstName, LastName = LastName,
            Email = "recipient@example.test", UserName = "recipient@example.test"
        };
        Assert.True(new CreateUserRequestValidator().Validate(request).IsValid);
        Assert.True(new CreateOrUpdateTenantRequestValidator().Validate(new CreateOrUpdateTenantRequest
        {
            Identifier = "blue", Name = TenantName, AdminEmail = request.Email,
            IsActive = true, ValidUpto = DateTime.UtcNow.AddDays(1)
        }).IsValid);

        var result = await context.Mutations.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsError);
        SendEmailRequestInput message = Assert.Single(context.Jobs.Emails);
        Assert.Equal(request.Email, message.Email);
        AssertEncodedNames(message.HtmlMessage);
        Assert.Equal(context.Tenant.Tenant.TenantLink + "/login", ReadOnlyAnchor(message.HtmlMessage));
        string password = ReadTextAfter(message.HtmlMessage, "Your password is:<br/>");
        Assert.Equal(16, password.Length);
        Assert.True(await context.Users.CheckPasswordAsync(await context.Db.Users.SingleAsync(), password));
    }

    [Fact]
    public async Task ForgotPasswordEmailEncodesStoredNamesAndRetainsAUsableResetLink()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        user.FirstName = FirstName;
        user.LastName = LastName;
        Assert.True((await scope.Users.UpdateAsync(user)).Succeeded);
        ITenantGetterService tenant = scope.Services.GetRequiredService<ITenantGetterService>();
        tenant.Tenant.Name = TenantName;
        var jobs = new RecordingJobs();
        var handler = new ForgotPassword.Handler(scope.Users, tenant, jobs, NullLogger<ForgotPassword>.Instance);

        Assert.False((await handler.ForgotPasswordAsync(new() { Email = user.Email! })).IsError);

        SendEmailRequestInput message = Assert.Single(jobs.Emails);
        AssertEncodedNames(message.HtmlMessage);
        var link = new Uri(ReadOnlyAnchor(message.HtmlMessage));
        Assert.Equal(new Uri(tenant.Tenant.TenantLink).Host, link.Host);
        Assert.Equal("/account/reset-password", link.AbsolutePath);
        string proof = QueryHelpers.ParseQuery(link.Query)["Token"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(proof));
        var reset = await new ResetPassword.Handler(scope.Users, scope.Db).ResetPasswordAsync(new()
        { Email = user.Email, Token = proof, Password = "ChangedPassword1!" });
        Assert.False(reset.IsError);
    }

    [Fact]
    public async Task ChangedPasswordEmailEncodesStoredNamesWithoutAddingAttackerMarkup()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        user.FirstName = FirstName;
        user.LastName = LastName;
        Assert.True((await scope.Users.UpdateAsync(user)).Succeeded);
        var current = (CurrentUser)scope.Services.GetRequiredService<ICurrentUser>();
        current.SetCurrentUserId(user.Id);
        ITenantGetterService tenant = scope.Services.GetRequiredService<ITenantGetterService>();
        tenant.Tenant.Name = TenantName;
        var jobs = new RecordingJobs();

        var result = await new ChangePassword.Handler(scope.Users, scope.Db, current, tenant, jobs).ChangePasswordAsync(new()
        { Password = MfaHarness.Password, NewPassword = "ChangedPassword1!", ConfirmNewPassword = "ChangedPassword1!" });

        Assert.False(result.IsError);
        SendEmailRequestInput message = Assert.Single(jobs.Emails);
        AssertEncodedNames(message.HtmlMessage);
        Assert.Empty(Regex.Matches(message.HtmlMessage, "<a\\b", RegexOptions.IgnoreCase).Cast<Match>());
        Assert.Contains("<p>Your password has been successfully changed.", message.HtmlMessage);
    }

    [Fact]
    public async Task WelcomeEmailEncodesIdentityTextAndKeepsItsLoginLinkAndDeliveredPassword()
    {
        var services = new ServiceCollection();
        DomainTestServices.Register(services, "mail-tenant");
        // Apostrophes are valid in mail addresses when the configured username policy permits them.
        services.Configure<IdentityOptions>(options => options.User.AllowedUserNameCharacters += "'");
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ITenantGetterService tenant = scope.ServiceProvider.GetRequiredService<ITenantGetterService>();
        tenant.Tenant.AdminEmail = "o'brien@example.test";

        await scope.ServiceProvider.GetRequiredService<DataSeedService>().SeedAsync();

        SendEmailRequestInput message = Assert.Single(provider.GetRequiredService<DomainJobs>().Emails);
        Assert.Equal(tenant.Tenant.AdminEmail, message.Email);
        Assert.Contains(HtmlEncoder.Default.Encode(tenant.Tenant.AdminEmail), message.HtmlMessage);
        Assert.DoesNotContain(tenant.Tenant.AdminEmail, message.HtmlMessage);
        Assert.Equal(tenant.Tenant.TenantLink + "/login", ReadOnlyAnchor(message.HtmlMessage));
        string password = ReadTextAfter(message.HtmlMessage, "Password: ");
        Assert.Equal(16, password.Length);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByEmailAsync(tenant.Tenant.AdminEmail))!, password));
    }

    private static void AssertEncodedNames(string html)
    {
        foreach (string value in new[] { FirstName, LastName, TenantName })
        {
            Assert.Contains(HtmlEncoder.Default.Encode(value), html);
            Assert.DoesNotContain(value, html);
        }
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadOnlyAnchor(string html)
    {
        Assert.Single(Regex.Matches(html, "<a\\b", RegexOptions.IgnoreCase).Cast<Match>());
        Match anchor = Assert.Single(Regex.Matches(html, "<a href='([^']+)'>([^<]*)</a>").Cast<Match>());
        return WebUtility.HtmlDecode(anchor.Groups[1].Value);
    }

    private static string ReadTextAfter(string html, string prefix)
    {
        int start = html.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += prefix.Length;
        int end = html.IndexOf("<br", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return WebUtility.HtmlDecode(html[start..end]);
    }

    private sealed class RecordingJobs : IJobService
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
