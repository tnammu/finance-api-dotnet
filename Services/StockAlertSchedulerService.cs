namespace FinanceApi.Services
{
    /// <summary>
    /// Background service that sends stock pick notifications at scheduled times.
    /// Default: 9:30 AM and 2:00 PM Eastern Time, weekdays only.
    /// Configure via appsettings "AlertSchedule" section.
    /// </summary>
    public class StockAlertSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<StockAlertSchedulerService> _logger;
        private readonly HashSet<string> _sentToday = new();
        private DateOnly _lastDate = DateOnly.MinValue;

        public StockAlertSchedulerService(
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<StockAlertSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _config.GetValue("AlertSchedule:Enabled", true);
            if (!enabled)
            {
                _logger.LogInformation("Alert scheduler is disabled via config.");
                return;
            }

            var timeStrings = _config.GetSection("AlertSchedule:Times").Get<string[]>()
                              ?? new[] { "09:30", "14:00" };
            var tzId = _config["AlertSchedule:Timezone"] ?? "Eastern Standard Time";

            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                // Fallback for Linux timezone IDs
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                catch { tz = TimeZoneInfo.Local; }
            }

            var scheduledTimes = new List<TimeOnly>();
            foreach (var ts in timeStrings)
            {
                if (TimeOnly.TryParse(ts, out var t))
                    scheduledTimes.Add(t);
            }

            _logger.LogInformation(
                "Alert scheduler started. Schedule: {Times} {Tz} (weekdays only)",
                string.Join(", ", scheduledTimes), tz.DisplayName);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var utcNow = DateTime.UtcNow;
                    var estNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
                    var today = DateOnly.FromDateTime(estNow);
                    var currentTime = TimeOnly.FromDateTime(estNow);

                    // Reset sent tracker on new day
                    if (today != _lastDate)
                    {
                        _sentToday.Clear();
                        _lastDate = today;
                    }

                    // Skip weekends
                    if (estNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;

                    foreach (var scheduled in scheduledTimes)
                    {
                        var key = scheduled.ToString("HH:mm");

                        // Already sent for this time slot today
                        if (_sentToday.Contains(key))
                            continue;

                        // Check if we're within the 2-minute window after scheduled time
                        var diff = currentTime.ToTimeSpan() - scheduled.ToTimeSpan();
                        if (diff.TotalMinutes >= 0 && diff.TotalMinutes < 2)
                        {
                            _sentToday.Add(key);
                            _logger.LogInformation(
                                "Scheduled alert triggered for {Time} EST", key);

                            await SendScheduledAlertAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in alert scheduler loop");
                }
            }
        }

        private async Task SendScheduledAlertAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var alertService = scope.ServiceProvider.GetRequiredService<StockAlertService>();

                var result = await alertService.GetTopPicksAndSendSmsAsync();

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Scheduled alert sent. Analyzed {Count} stocks. Status: {Status}",
                        result.TotalAnalyzed, result.SmsStatus);
                }
                else
                {
                    _logger.LogWarning(
                        "Scheduled alert failed: {Error}", result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scheduled alert");
            }
        }
    }
}
