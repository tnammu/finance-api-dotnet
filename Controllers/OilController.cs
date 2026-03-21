using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/oil")]
    public class OilController : ControllerBase
    {
        private readonly OilSentimentService    _oilService;
        private readonly SmsNotificationService _smsService;
        private readonly IConfiguration         _config;

        public OilController(OilSentimentService oilService, SmsNotificationService smsService, IConfiguration config)
        {
            _oilService = oilService;
            _smsService = smsService;
            _config     = config;
        }

        /// <summary>
        /// Returns WTI and Brent sentiment signal with technical indicators and suggested order levels.
        /// Result is cached for 30 minutes.
        /// GET: /api/oil/sentiment
        /// </summary>
        [HttpGet("sentiment")]
        public async Task<ActionResult> GetSentiment()
        {
            var result = await _oilService.GetSentimentAsync();
            if (result == null)
                return StatusCode(500, new { error = "Failed to run oil sentiment analysis. Check logs." });

            return Ok(result.RootElement);
        }

        /// <summary>
        /// Sends the oil signal as a push notification via ntfy.
        /// POST: /api/oil/send-sms
        /// </summary>
        [HttpPost("send-sms")]
        public async Task<ActionResult> SendSms()
        {
            var result = await _oilService.GetSentimentAsync();
            if (result == null)
                return StatusCode(500, new { error = "Could not retrieve oil sentiment data." });

            var message   = _oilService.FormatSmsMessage(result);
            var oilTopic  = _config["Sms:OilNtfyTopic"] ?? "finance-oil-chara";
            var smsResult = await _smsService.SendSmsAsync(message, topic: oilTopic, title: "Oil Market Signal");

            if (!smsResult.Success)
                return StatusCode(500, new { error = smsResult.Error });

            return Ok(new { success = true, preview = message });
        }
    }
}
