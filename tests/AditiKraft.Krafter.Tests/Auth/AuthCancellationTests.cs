using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class AuthCancellationTests
{
    [Fact]
    public async Task CallerCancellationIsPreservedAndDependencyTimeoutGetsAnEnvelopeAsync()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AuthApiErrors.CallAsync<bool>(
            () => Task.FromCanceled<Response<bool>>(caller.Token), caller.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ServerAuthApiErrors.CallAsync(
            () => Task.FromCanceled<Response>(caller.Token), caller.Token));
        Response<bool> timeout = await AuthApiErrors.CallAsync<bool>(() =>
            Task.FromException<Response<bool>>(new OperationCanceledException("Dependency timeout")));
        Assert.Equal(503, timeout.StatusCode);
    }

    [Fact]
    public async Task AbortedApiRequestDoesNotWriteAnErrorResponseAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Request.Path = "/api/two-factor/login";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(new CurrentUser(), NullLogger<ExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context, _ => Task.FromCanceled(cancellation.Token));
        Assert.Equal(0, context.Response.Body.Length);
        Assert.False(context.Response.HasStarted);
    }
}
