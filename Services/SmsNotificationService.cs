using System.Text;
using System.Text.Json;

namespace FinanceApi.Services
{
    /// <summary>
    /// Sends push notifications via ntfy.sh (https://ntfy.sh).
    /// Free, no account needed. Works globally including Canada.
    ///
    /// Setup:
    ///   1. Install ntfy app on your phone (Android/iOS)
    ///   2. Subscribe to the topic configured in appsettings Sms:NtfyTopic
    ///   3. Done — no SMS fees, no API keys
    ///
    /// Required appsettings.json:
    ///   "Sms": {
    ///     "NtfyTopic": "finance-picks-chara",
    ///     "NtfyServer": "https://ntfy.sh"  (optional, defaults to ntfy.sh)
    ///   }
    /// </summary>
    public class SmsNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<SmsNotificationService> _logger;

        public SmsNotificationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<SmsNotificationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Sends a push notification via ntfy.sh.
        /// </summary>
        /// <param name="message">Notification body.</param>
        /// <param name="topic">Override ntfy topic. Defaults to Sms:NtfyTopic from config.</param>
        /// <param name="title">Override notification title. Defaults to "Top 5 Canadian Stock Picks".</param>
        public async Task<SmsSendResult> SendSmsAsync(string message, string? topic = null, string? title = null)
        {
            var resolvedTopic = topic ?? _config["Sms:NtfyTopic"] ?? "finance-picks-chara";
            var server = _config["Sms:NtfyServer"] ?? "https://ntfy.sh";

            var url = $"{server.TrimEnd('/')}/{resolvedTopic}";

            try
            {
                var client = _httpClientFactory.CreateClient();

                // ntfy supports a title via header
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(message, Encoding.UTF8, "text/plain")
                };
                request.Headers.Add("Title", title ?? "Top 5 Canadian Stock Picks");
                request.Headers.Add("Priority", "high");
                request.Headers.Add("Tags", "chart_increasing,canada");

                _logger.LogInformation($"Sending notification to ntfy topic: {resolvedTopic}");

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"ntfy response [{response.StatusCode}]: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    return new SmsSendResult
                    {
                        Success    = true,
                        NtfyTopic  = resolvedTopic,
                        Status     = $"Notification sent to topic '{resolvedTopic}'"
                    };
                }
                else
                {
                    var errMsg = $"ntfy returned {(int)response.StatusCode}: {responseBody}";
                    _logger.LogError(errMsg);
                    return new SmsSendResult { Success = false, Error = errMsg };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Notification send failed: {ex.Message}");
                return new SmsSendResult { Success = false, Error = ex.Message };
            }
        }
    }
}
