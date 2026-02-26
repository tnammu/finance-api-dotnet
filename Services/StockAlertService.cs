using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceApi.Services
{
    /// <summary>
    /// Analyzes top 5 Canadian stocks per strategy (RSI, Swing, Seasonal)
    /// and optionally sends results via SMS.
    /// </summary>
    public class StockAlertService
    {
        private readonly SmsNotificationService _smsService;
        private readonly ILogger<StockAlertService> _logger;
        private readonly string _scriptsPath;

        public StockAlertService(
            SmsNotificationService smsService,
            ILogger<StockAlertService> logger)
        {
            _smsService = smsService;
            _logger = logger;

            // Same path resolution pattern as StrategyService
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot = Directory.GetParent(appDirectory)?.Parent?.Parent?.Parent?.FullName;

            if (projectRoot != null && Directory.Exists(Path.Combine(projectRoot, "scripts")))
                _scriptsPath = Path.Combine(projectRoot, "scripts");
            else
                _scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts");

            _logger.LogInformation($"StockAlertService initialized. Scripts path: {_scriptsPath}");
        }

        /// <summary>
        /// Runs Python analysis and returns top picks as structured data.
        /// </summary>
        public async Task<TopPicksResult?> GetTopPicksAsync()
        {
            var pythonScript = Path.Combine(_scriptsPath, "top_canadian_picks.py");

            if (!File.Exists(pythonScript))
            {
                _logger.LogError($"Python script not found: {pythonScript}");
                return null;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pythonScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _scriptsPath
            };

            _logger.LogInformation("Running top_canadian_picks.py...");

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(errors))
                _logger.LogInformation($"Python output:\n{errors}");

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Python script failed with exit code {process.ExitCode}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogError("No output from Python script");
                return null;
            }

            try
            {
                var result = JsonSerializer.Deserialize<TopPicksResult>(output, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to parse Python output: {ex.Message}");
                _logger.LogDebug($"Raw output: {output[..Math.Min(500, output.Length)]}");
                return null;
            }
        }

        /// <summary>
        /// Analyzes stocks and sends 3 separate notifications (RSI, Swing, Seasonal).
        /// </summary>
        public async Task<AlertSendResult> GetTopPicksAndSendSmsAsync(string? toPhoneNumber = null)
        {
            var picks = await GetTopPicksAsync();

            if (picks == null)
            {
                return new AlertSendResult
                {
                    Success = false,
                    Error = "Could not analyze stocks. Check Python script and database."
                };
            }

            var r1 = await _smsService.SendSmsAsync(FormatRsiMessage(picks), toPhoneNumber);
            var r2 = await _smsService.SendSmsAsync(FormatSwingMessage(picks), toPhoneNumber);
            var r3 = await _smsService.SendSmsAsync(FormatSeasonalMessage(picks), toPhoneNumber);

            var success = r1.Success && r2.Success && r3.Success;
            var errors  = string.Join("; ", new[] { r1.Error, r2.Error, r3.Error }.Where(e => e != null));

            return new AlertSendResult
            {
                Success       = success,
                Error         = string.IsNullOrEmpty(errors) ? null : errors,
                SmsStatus     = success ? "3 notifications sent" : "Some notifications failed",
                MessageSent   = $"[1/3] RSI\n[2/3] Swing\n[3/3] Seasonal — {picks.TotalAnalyzed} stocks analyzed",
                GeneratedAt   = picks.GeneratedAt,
                TotalAnalyzed = picks.TotalAnalyzed
            };
        }

        private string FormatRsiMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[1/3] RSI OVERSOLD — {DateTime.Now:MMM dd} ({picks.TotalAnalyzed} stocks)");
            sb.AppendLine("#  Stock     Price    Score  RSI        Trend");
            sb.AppendLine("-- --------- -------- ------ ---------- ---------");
            if (picks.Rsi?.Any() == true)
            {
                for (int i = 0; i < picks.Rsi.Count; i++)
                    FormatStockRow(sb, i + 1, picks.Rsi[i]);
            }
            else
            {
                sb.AppendLine("None found");
            }
            sb.Append("RSI<30=Strong Buy | RSI<40=Oversold");
            return sb.ToString().TrimEnd();
        }

        private string FormatSwingMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[2/3] SWING TRADE — {DateTime.Now:MMM dd}");
            sb.AppendLine("#  Stock     Price    Score  RSI        Trend");
            sb.AppendLine("-- --------- -------- ------ ---------- ---------");
            if (picks.Swing?.Any() == true)
            {
                for (int i = 0; i < picks.Swing.Count; i++)
                    FormatStockRow(sb, i + 1, picks.Swing[i]);
            }
            else
            {
                sb.AppendLine("None found");
            }
            sb.Append("ATR 2-5% ideal | Near lower BB = buy zone");
            return sb.ToString().TrimEnd();
        }

        private string FormatSeasonalMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[3/3] SEASONAL ({picks.Month ?? "Month"}) — {DateTime.Now:MMM dd}");
            sb.AppendLine("#  Stock     Price    AvgRet  Win%  Trend");
            sb.AppendLine("-- --------- -------- ------- ----- ---------");
            if (picks.Seasonal?.Any() == true)
            {
                for (int i = 0; i < picks.Seasonal.Count; i++)
                    FormatSeasonalRow(sb, i + 1, picks.Seasonal[i]);
            }
            else
            {
                sb.AppendLine("None found");
            }
            sb.Append("Min 60% win rate | Min 3yr history");
            return sb.ToString().TrimEnd();
        }

        private void FormatStockRow(StringBuilder sb, int rank, StockPickItem s)
        {
            var sym    = s.Symbol.Replace(".TO", "");
            var rsiVal = s.Rsi.HasValue ? s.Rsi.Value.ToString("F0") : "--";
            var rsiCol = $"{rsiVal} {GetRsiLabel(s.Rsi)}";
            var score  = $"{s.SwingScore:F0}";

            sb.AppendLine($"{rank,-2} {sym,-9} ${s.Price,-7:F2} {score,-6} {rsiCol,-10} {s.Trend ?? "N/A"}");
        }

        private void FormatSeasonalRow(StringBuilder sb, int rank, StockPickItem s)
        {
            var sym    = s.Symbol.Replace(".TO", "");
            var avgRet = s.MonthAvgReturn.HasValue
                ? $"{(s.MonthAvgReturn.Value >= 0 ? "+" : "")}{s.MonthAvgReturn.Value:F1}%"
                : "N/A";
            var winRate = $"{s.SeasonalWinRate:F0}%";

            sb.AppendLine($"{rank,-2} {sym,-9} ${s.Price,-7:F2} {avgRet,-7} {winRate,-5} {s.Trend ?? "N/A"}");
        }

        private static string GetRsiLabel(double? rsi)
        {
            if (!rsi.HasValue) return "";
            if (rsi.Value < 30) return "Strong Buy";
            if (rsi.Value < 40) return "Oversold";
            if (rsi.Value > 70) return "Overbought";
            return "Neutral";
        }
    }

    // --- DTOs ---

    public class TopPicksResult
    {
        [JsonPropertyName("generated_at")]
        public string? GeneratedAt { get; set; }

        [JsonPropertyName("total_analyzed")]
        public int TotalAnalyzed { get; set; }

        [JsonPropertyName("month")]
        public string? Month { get; set; }

        [JsonPropertyName("rsi")]
        public List<StockPickItem>? Rsi { get; set; }

        [JsonPropertyName("swing")]
        public List<StockPickItem>? Swing { get; set; }

        [JsonPropertyName("seasonal")]
        public List<StockPickItem>? Seasonal { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class StockPickItem
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("avg_volume")]
        public long AvgVolume { get; set; }

        [JsonPropertyName("rsi")]
        public double? Rsi { get; set; }

        [JsonPropertyName("rsi_signal")]
        public string? RsiSignal { get; set; }

        [JsonPropertyName("trend")]
        public string? Trend { get; set; }

        [JsonPropertyName("sma50")]
        public double? Sma50 { get; set; }

        [JsonPropertyName("sma200")]
        public double? Sma200 { get; set; }

        [JsonPropertyName("swing_score")]
        public double SwingScore { get; set; }

        [JsonPropertyName("atr_pct")]
        public double AtrPct { get; set; }

        [JsonPropertyName("bb_position")]
        public string? BbPosition { get; set; }

        [JsonPropertyName("vol_ratio")]
        public double VolRatio { get; set; }

        [JsonPropertyName("ret_1m")]
        public double Ret1m { get; set; }

        [JsonPropertyName("ret_3m")]
        public double Ret3m { get; set; }

        [JsonPropertyName("pct_from_52w_high")]
        public double PctFrom52wHigh { get; set; }

        [JsonPropertyName("pct_from_52w_low")]
        public double PctFrom52wLow { get; set; }

        [JsonPropertyName("month_avg_return")]
        public double? MonthAvgReturn { get; set; }

        [JsonPropertyName("seasonal_years")]
        public int SeasonalYears { get; set; }

        [JsonPropertyName("seasonal_win_rate")]
        public double SeasonalWinRate { get; set; }

        [JsonPropertyName("seasonal_best")]
        public double? SeasonalBest { get; set; }

        [JsonPropertyName("seasonal_worst")]
        public double? SeasonalWorst { get; set; }

        [JsonPropertyName("month")]
        public string? Month { get; set; }
    }

    public class AlertSendResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? SmsStatus { get; set; }
        public string? MessageSent { get; set; }
        public string? GeneratedAt { get; set; }
        public int TotalAnalyzed { get; set; }
    }

    public class SmsSendResult
    {
        public bool Success { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
        public string? MessageSid { get; set; }
    }
}
