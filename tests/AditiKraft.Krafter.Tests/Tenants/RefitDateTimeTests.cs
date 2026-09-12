using System.Net;
using System.Text.Json;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using Refit;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class RefitDateTimeTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public async Task RefitSendsUtcAndReadsLocalWithoutChangingInput(DateTimeKind kind)
    {
        DateTime input = new(2030, 6, 15, 12, 34, 56, kind);
        var handler = new EchoHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        IDateApi api = CreateApi(http, localizeResponses: true);
        var request = new DatePayload { Timestamp = input, CalendarDate = new DateOnly(2030, 6, 15) };

        DatePayload response = await api.RoundTripAsync(request);

        using JsonDocument json = JsonDocument.Parse(handler.Body!);
        DateTime sent = json.RootElement.GetProperty(nameof(DatePayload.Timestamp)).GetDateTime();
        Assert.Equal(DateTimeKind.Utc, sent.Kind);
        Assert.Equal(input.ToUniversalTime(), sent);
        Assert.Equal(DateTimeKind.Local, response.Timestamp!.Value.Kind);
        Assert.Equal(input.ToUniversalTime().ToLocalTime(), response.Timestamp);
        Assert.Equal(kind, request.Timestamp!.Value.Kind);
        Assert.Equal(request.CalendarDate, response.CalendarDate);
        Assert.Equal("2030-06-15", json.RootElement.GetProperty(nameof(DatePayload.CalendarDate)).GetString());
    }

    [Fact]
    public async Task NullAndUnlimitedExpirySurviveRefitRoundTrip()
    {
        using var http = new HttpClient(new EchoHandler()) { BaseAddress = new Uri("https://example.test") };
        IDateApi api = CreateApi(http, localizeResponses: true);
        foreach (DateTime? value in new DateTime?[] { null, DateTime.MinValue, DateTime.MaxValue })
        {
            DatePayload response = await api.RoundTripAsync(new DatePayload { Timestamp = value });
            Assert.Equal(value, response.Timestamp);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResponseOffsetsPreserveTheInstantAndServerReadsStayUtc(bool localizeResponses)
    {
        var handler = new EchoHandler { ResponseBody = "{\"Timestamp\":\"2030-06-15T12:00:00+05:45\"}" };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        DatePayload response = await CreateApi(http, localizeResponses).RoundTripAsync(new DatePayload());
        DateTime expected = new(2030, 6, 15, 6, 15, 0, DateTimeKind.Utc);
        Assert.Equal(localizeResponses ? expected.ToLocalTime() : expected, response.Timestamp);
        Assert.Equal(localizeResponses ? DateTimeKind.Local : DateTimeKind.Utc, response.Timestamp!.Value.Kind);
    }

    [Fact]
    public async Task ResponseWithoutOffsetIsTreatedAsUtc()
    {
        var handler = new EchoHandler { ResponseBody = "{\"Timestamp\":\"2030-06-15T12:00:00\"}" };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        DatePayload response = await CreateApi(http, localizeResponses: true).RoundTripAsync(new DatePayload());
        Assert.Equal(new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc).ToLocalTime(), response.Timestamp);
    }

    private static IDateApi CreateApi(HttpClient http, bool localizeResponses)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UiDateTimeJsonConverter(localizeResponses));
        return RestService.For<IDateApi>(http,
            new RefitSettings { ContentSerializer = new SystemTextJsonContentSerializer(options) });
    }

    public interface IDateApi
    {
        [Post("/dates")]
        Task<DatePayload> RoundTripAsync([Body] DatePayload request);
    }

    public sealed class DatePayload
    {
        public DateTime? Timestamp { get; set; }
        public DateOnly CalendarDate { get; set; }
    }

    private sealed class EchoHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? ResponseBody { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody ?? Body, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
