namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public sealed class AccountSecurityNavigationGuard
{
    private readonly List<Func<string?>> blockers = [];

    public string? BlockingMessage => blockers.Select(blocker => blocker()).FirstOrDefault(message => message is not null);

    // Registrations live only as long as their page. This service never stores credentials or recovery codes.
    public IDisposable Register(Func<string?> blockingMessage)
    {
        blockers.Add(blockingMessage);
        return new Registration(() => blockers.Remove(blockingMessage));
    }

    private sealed class Registration(Action remove) : IDisposable
    {
        private Action? removeRegistration = remove;

        public void Dispose() => Interlocked.Exchange(ref removeRegistration, null)?.Invoke();
    }
}
