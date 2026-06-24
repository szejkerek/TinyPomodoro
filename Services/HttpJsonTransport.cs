using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Pomodoro.Services
{
    /// <summary>
    /// Resilient HTTP-and-JSON transport shared by the task gateways. Owns the one policy that was
    /// duplicated across them: retry a transient 5xx/429 a few times with a linear backoff, then
    /// surface the response. Callers supply a request factory (so each gateway keeps its own auth
    /// header shape) and the type to deserialize.
    /// </summary>
    public sealed class HttpJsonTransport
    {
        private readonly HttpClient httpClient;
        private readonly int maxAttempts;
        private readonly int retryBackoffMs;

        public HttpJsonTransport(HttpClient httpClient, int maxAttempts, int retryBackoffMs)
        {
            this.httpClient = httpClient;
            this.maxAttempts = maxAttempts;
            this.retryBackoffMs = retryBackoffMs;
        }

        public async Task<T> GetJsonAsync<T>(Func<HttpRequestMessage> buildRequest) where T : new()
        {
            using HttpResponseMessage response = await SendWithRetryAsync(buildRequest);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            T? parsed = JsonSerializer.Deserialize<T>(json);
            return parsed ?? new T();
        }

        public async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> buildRequest)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using HttpRequestMessage request = buildRequest();
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    if (attempt >= maxAttempts || IsTransient(response.StatusCode) == false)
                    {
                        return response;
                    }

                    response.Dispose();
                }
                catch (HttpRequestException) when (attempt < maxAttempts)
                {
                }
                catch (TaskCanceledException) when (attempt < maxAttempts)
                {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(retryBackoffMs * attempt));
            }
        }

        private static bool IsTransient(HttpStatusCode status)
        {
            return status == HttpStatusCode.InternalServerError
                || status == HttpStatusCode.BadGateway
                || status == HttpStatusCode.ServiceUnavailable
                || status == HttpStatusCode.GatewayTimeout
                || status == HttpStatusCode.TooManyRequests;
        }
    }
}
