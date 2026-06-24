using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class HttpJsonTransportTests
    {
        private const int MaxAttempts = 4;
        private const int NoBackoff = 0;
        private const string Url = "https://example.test/thing";

        private sealed class Thing
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private static HttpJsonTransport TransportOver(QueuedHandler handler)
        {
            return new HttpJsonTransport(new HttpClient(handler), MaxAttempts, NoBackoff);
        }

        private static HttpRequestMessage Get()
        {
            return new HttpRequestMessage(HttpMethod.Get, Url);
        }

        [Fact]
        public async Task Parses_a_successful_json_response()
        {
            QueuedHandler handler = new QueuedHandler();
            handler.Enqueue(HttpStatusCode.OK, "{\"name\":\"hello\"}");
            HttpJsonTransport transport = TransportOver(handler);

            Thing thing = await transport.GetJsonAsync<Thing>(Get);

            Assert.Equal("hello", thing.Name);
        }

        [Fact]
        public async Task Retries_a_transient_failure_then_succeeds()
        {
            QueuedHandler handler = new QueuedHandler();
            handler.Enqueue(HttpStatusCode.ServiceUnavailable);
            handler.Enqueue(HttpStatusCode.OK, "{\"name\":\"after-retry\"}");
            HttpJsonTransport transport = TransportOver(handler);

            Thing thing = await transport.GetJsonAsync<Thing>(Get);

            Assert.Equal("after-retry", thing.Name);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task Gives_up_after_the_attempt_cap_on_persistent_transient_failures()
        {
            QueuedHandler handler = new QueuedHandler();
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                handler.Enqueue(HttpStatusCode.TooManyRequests);
            }

            HttpJsonTransport transport = TransportOver(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetJsonAsync<Thing>(Get));
            Assert.Equal(MaxAttempts, handler.CallCount);
        }

        [Fact]
        public async Task Does_not_retry_a_non_transient_failure()
        {
            QueuedHandler handler = new QueuedHandler();
            handler.Enqueue(HttpStatusCode.NotFound);
            HttpJsonTransport transport = TransportOver(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => transport.GetJsonAsync<Thing>(Get));
            Assert.Equal(1, handler.CallCount);
        }
    }

    /// <summary>Test <see cref="HttpMessageHandler"/>: replies with queued responses, counts calls.</summary>
    public sealed class QueuedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new Queue<HttpResponseMessage>();

        public int CallCount { get; private set; }

        public void Enqueue(HttpStatusCode status, string body = "")
        {
            responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responses.Dequeue());
        }
    }
}
