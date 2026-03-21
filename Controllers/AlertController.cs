using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/alerts")]
    public class AlertController : ControllerBase
    {
        private readonly StockAlertService _alertService;
        private readonly ILogger<AlertController> _logger;

        public AlertController(
            StockAlertService alertService,
            ILogger<AlertController> logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        /// <summary>
        /// Returns top 5 Canadian stocks per strategy without sending SMS.
        /// GET: /api/alerts/top-picks
        /// </summary>
        [HttpGet("top-picks")]
        public async Task<ActionResult<object>> GetTopPicks()
        {
            try
            {
                var picks = await _alertService.GetTopPicksAsync();

                if (picks == null)
                    return StatusCode(500, new { error = "Could not analyze stocks. Check logs." });

                if (!string.IsNullOrEmpty(picks.Error))
                    return StatusCode(500, new { error = picks.Error });

                return Ok(new
                {
                    success = true,
                    generatedAt = picks.GeneratedAt,
                    totalAnalyzed = picks.TotalAnalyzed,
                    month = picks.Month,
                    strategies = new
                    {
                        rsi = picks.Rsi,
                        swing = picks.Swing,
                        seasonal = picks.Seasonal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetTopPicks: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve top picks" });
            }
        }

        /// <summary>
        /// Analyzes top 5 Canadian stocks and sends results via ntfy.
        /// POST: /api/alerts/send-sms
        /// </summary>
        [HttpPost("send-sms")]
        public async Task<ActionResult<object>> SendSmsAlert()
        {
            try
            {
                _logger.LogInformation("Stock alert notification requested");

                var result = await _alertService.GetTopPicksAndSendSmsAsync();

                if (!result.Success)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        error = result.Error
                    });
                }

                return Ok(new
                {
                    success = true,
                    smsStatus = result.SmsStatus,
                    totalAnalyzed = result.TotalAnalyzed,
                    generatedAt = result.GeneratedAt,
                    messageSent = result.MessageSent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending SMS alert: {ex.Message}");
                return StatusCode(500, new { error = "Failed to send SMS alert" });
            }
        }
    }
}
