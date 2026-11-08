using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Notifications;

public sealed class EmailServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task EachScopeOwnsAndDisposesItsSmtpClient()
    {
        using ServiceProvider provider = Provider(port: 2525);
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();
        SmtpClient firstClient = first.ServiceProvider.GetRequiredService<SmtpClient>();
        SmtpClient secondClient = second.ServiceProvider.GetRequiredService<SmtpClient>();

        Assert.NotSame(first.ServiceProvider.GetRequiredService<IEmailService>(),
            second.ServiceProvider.GetRequiredService<IEmailService>());
        Assert.Same(firstClient, first.ServiceProvider.GetRequiredService<SmtpClient>());
        Assert.NotSame(firstClient, secondClient);

        first.Dispose();
        using var message = new MailMessage("sender@example.test", "recipient@example.test", "Disposed", "Test");
        await Assert.ThrowsAsync<ObjectDisposedException>(() => firstClient.SendMailAsync(message));
    }

    [Fact]
    public async Task ConcurrentJobScopesCanBothDeliverWhileTheFirstSendIsStillInProgress()
    {
        await using var smtp = new ControlledLoopbackSmtpServer();
        using ServiceProvider provider = Provider(smtp.Port);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        IEmailService first = ConfigureLocalSmtp(firstScope);
        IEmailService second = ConfigureLocalSmtp(secondScope);

        Task firstSend = first.SendEmailAsync("first@example.test", "First test", "<p>First</p>", CancellationToken.None);
        Assert.Contains("first@example.test", await smtp.WaitForDataAsync().WaitAsync(Timeout));
        Task secondSend = second.SendEmailAsync("second@example.test", "Second test", "<p>Second</p>", CancellationToken.None);
        Assert.Contains("second@example.test", await smtp.WaitForDataAsync().WaitAsync(Timeout));

        // Both connections reached DATA before either message was allowed to complete.
        Assert.False(firstSend.IsCompleted);
        Assert.False(secondSend.IsCompleted);
        Assert.Equal(0, smtp.AcceptedMessages);
        smtp.ReleaseData();
        await Task.WhenAll(firstSend, secondSend).WaitAsync(Timeout);

        Assert.Equal(2, smtp.AcceptedMessages);
    }

    [Fact]
    public async Task CancelingAnInFlightSendStopsWithoutWaitingForTheSmtpServer()
    {
        await using var smtp = new ControlledLoopbackSmtpServer();
        using ServiceProvider provider = Provider(smtp.Port);
        using IServiceScope scope = provider.CreateScope();
        IEmailService email = ConfigureLocalSmtp(scope);
        using var cancellation = new CancellationTokenSource();

        Task send = email.SendEmailAsync("recipient@example.test", "Canceled test", "<p>Test</p>", cancellation.Token);
        await smtp.WaitForDataAsync().WaitAsync(Timeout);
        Assert.False(send.IsCompleted);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(Timeout));
        Assert.True(send.IsCanceled);
        Assert.Equal(0, smtp.AcceptedMessages);
        // DATA remains withheld until server disposal, so completion proves cancellation propagation.
    }

    private static IEmailService ConfigureLocalSmtp(IServiceScope scope)
    {
        SmtpClient client = scope.ServiceProvider.GetRequiredService<SmtpClient>();
        // Protocol fixture only; production TLS/credentials remain configured by the real registration.
        client.EnableSsl = false;
        client.Credentials = null;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        return scope.ServiceProvider.GetRequiredService<IEmailService>();
    }

    private static ServiceProvider Provider(int port)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["SMTPEmailSettings:Host"] = IPAddress.Loopback.ToString(),
                ["SMTPEmailSettings:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["SMTPEmailSettings:UserName"] = string.Empty,
                ["SMTPEmailSettings:Password"] = string.Empty,
                ["SMTPEmailSettings:SenderEmail"] = "sender@example.test",
                ["SMTPEmailSettings:SenderName"] = "Isolated SMTP test"
            }).Build();
        var services = new ServiceCollection();
        services.AddNotificationServices(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // This listener only binds IPv4 loopback and never forwards or sends any email.
    private sealed class ControlledLoopbackSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new(TimeSpan.FromSeconds(30));
        private readonly Channel<string> dataRequests = Channel.CreateUnbounded<string>();
        private readonly TaskCompletionSource releaseData = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Task> connections = [];
        private readonly Task accepting;
        private int acceptedMessages;

        public ControlledLoopbackSmtpServer()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            accepting = AcceptAsync();
        }

        public int Port { get; }
        public int AcceptedMessages => Volatile.Read(ref acceptedMessages);
        public Task<string> WaitForDataAsync() => dataRequests.Reader.ReadAsync(stopping.Token).AsTask();
        public void ReleaseData() => releaseData.TrySetResult();

        private async Task AcceptAsync()
        {
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    TcpClient connection = await listener.AcceptTcpClientAsync(stopping.Token);
                    connections.Add(ServeAsync(connection));
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (SocketException) when (stopping.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stopping.IsCancellationRequested) { }
        }

        private async Task ServeAsync(TcpClient connection)
        {
            using var client = connection;
            try
            {
                using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
                {
                    NewLine = "\r\n", AutoFlush = true
                };
                Task ReplyAsync(string response) => writer.WriteLineAsync(response.AsMemory(), stopping.Token);
                await ReplyAsync("220 localhost isolated-test ESMTP");
                string recipient = string.Empty;

                while (await reader.ReadLineAsync(stopping.Token) is { } command)
                {
                    if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))
                    {
                        await ReplyAsync("250-localhost");
                        await ReplyAsync("250 HELP");
                    }
                    else if (command.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                    {
                        recipient = command;
                        await ReplyAsync("250 OK");
                    }
                    else if (command.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase) ||
                             command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase) ||
                             command.Equals("RSET", StringComparison.OrdinalIgnoreCase) ||
                             command.Equals("NOOP", StringComparison.OrdinalIgnoreCase))
                    {
                        await ReplyAsync("250 OK");
                    }
                    else if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await dataRequests.Writer.WriteAsync(recipient, stopping.Token);
                        await releaseData.Task.WaitAsync(stopping.Token);
                        await ReplyAsync("354 End data with <CRLF>.<CRLF>");
                        while (true)
                        {
                            string? line = await reader.ReadLineAsync(stopping.Token);
                            if (line is null) throw new IOException("SMTP client closed before the message terminator.");
                            if (line == ".") break;
                        }
                        Interlocked.Increment(ref acceptedMessages);
                        await ReplyAsync("250 Accepted by isolated test");
                    }
                    else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await ReplyAsync("221 Bye");
                        return;
                    }
                    else
                    {
                        await ReplyAsync("500 Unsupported test command");
                    }
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (IOException) when (stopping.IsCancellationRequested) { }
            catch (SocketException) when (stopping.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stopping.IsCancellationRequested) { }
            catch (Exception exception)
            {
                dataRequests.Writer.TryComplete(exception);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await stopping.CancelAsync();
            listener.Stop();
            try
            {
                await accepting;
                await Task.WhenAll(connections);
            }
            finally
            {
                dataRequests.Writer.TryComplete();
                stopping.Dispose();
            }
        }
    }
}
