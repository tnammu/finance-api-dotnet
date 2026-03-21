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

        private TopPicksResult? _cachedPicks;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(4);

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

            if (_cachedPicks != null && DateTime.Now < _cacheExpiry)
            {
                _logger.LogInformation("Returning cached top picks (expires {Expiry:HH:mm})", _cacheExpiry);
                return _cachedPicks;
            }

            _logger.LogInformation("Running top_canadian_picks.py...");

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
                if (result != null)
                {
                    _cachedPicks = result;
                    _cacheExpiry = DateTime.Now + _cacheTtl;
                    _logger.LogInformation("Top picks cached until {Expiry:HH:mm}", _cacheExpiry);
                }
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
        /// Analyzes stocks and sends 4 separate notifications (RSI, Swing, Seasonal, News).
        /// </summary>
        public async Task<AlertSendResult> GetTopPicksAndSendSmsAsync()
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

            var smsResults = await Task.WhenAll(
                _smsService.SendSmsAsync(FormatRsiMessage(picks)),
                _smsService.SendSmsAsync(FormatSwingMessage(picks)),
                _smsService.SendSmsAsync(FormatSeasonalMessage(picks)),
                _smsService.SendSmsAsync(FormatNewsMessage(picks))
            );

            var success = smsResults.All(r => r.Success);
            var errors  = string.Join("; ", smsResults.Select(r => r.Error).Where(e => e != null));

            return new AlertSendResult
            {
                Success       = success,
                Error         = string.IsNullOrEmpty(errors) ? null : errors,
                SmsStatus     = success ? "4 notifications sent" : "Some notifications failed",
                MessageSent   = $"[1/4] RSI\n[2/4] Swing\n[3/4] Seasonal\n[4/4] News — {picks.TotalAnalyzed} stocks analyzed",
                GeneratedAt   = picks.GeneratedAt,
                TotalAnalyzed = picks.TotalAnalyzed
            };
        }

        private string FormatRsiMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[1/4] RSI OVERSOLD — {DateTime.Now:MMM dd} ({picks.TotalAnalyzed} stocks)");
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
            sb.AppendLine($"[2/4] SWING TRADE — {DateTime.Now:MMM dd}");
            sb.AppendLine("#  Stock     Price    Score  RSI   Div  Entry Zone        SL       TP1      TP2      R:R");
            sb.AppendLine("-- --------- -------- ------ ----- ---- ----------------- -------- -------- -------- ----");
            if (picks.Swing?.Any() == true)
            {
                for (int i = 0; i < picks.Swing.Count; i++)
                    FormatSwingRow(sb, i + 1, picks.Swing[i]);
            }
            else
            {
                sb.AppendLine("None found");
            }
            sb.Append("Entry=lower 40% of zone | SL=E-2xATR | TP1=E+2xATR | TP2=E+4xATR");
            return sb.ToString().TrimEnd();
        }

        private string FormatSeasonalMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[3/4] SEASONAL ({picks.Month ?? "Month"}) — {DateTime.Now:MMM dd}");
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

        private void FormatSwingRow(StringBuilder sb, int rank, StockPickItem s)
        {
            var sym    = s.Symbol.Replace(".TO", "");
            var rsi    = s.Rsi.HasValue ? s.Rsi.Value.ToString("F0") : "--";
            var div    = s.HasDivergence ? "DIV✓" : "    ";
            var score  = s.SwingScore.ToString("F0");
            var zone   = (s.EntryLow.HasValue && s.EntryHigh.HasValue)
                ? $"${s.EntryLow.Value:F2}-${s.EntryHigh.Value:F2}"
                : "N/A";
            var sl     = s.StopLoss.HasValue  ? $"${s.StopLoss.Value:F2}"  : "N/A";
            var tp1    = s.Tp1.HasValue       ? $"${s.Tp1.Value:F2}"       : "N/A";
            var tp2    = s.Tp2.HasValue       ? $"${s.Tp2.Value:F2}"       : "N/A";
            var rr     = s.RrRatio.HasValue   ? $"{s.RrRatio.Value:F1}x"   : "N/A";
            sb.AppendLine($"{rank,-2} {sym,-9} ${s.Price,-7:F2} {score,-6} {rsi,-5} {div,-4} {zone,-17} {sl,-8} {tp1,-8} {tp2,-8} {rr}");
        }

        private void FormatStockRow(StringBuilder sb, int rank, StockPickItem s)
        {
            var sym    = s.Symbol.Replace(".TO", "");
            var rsiVal = s.Rsi.HasValue ? s.Rsi.Value.ToString("F0") : "--";
            var rsiCol = $"{rsiVal} {GetRsiLabel(s.Rsi)}";
            var score  = $"{s.SwingScore:F0}";
            var sent   = s.SentimentLabel switch
            {
                "Bullish" => "📈",
                "Bearish" => "📉",
                _         => "  "
            };

            sb.AppendLine($"{rank,-2} {sym,-9} ${s.Price,-7:F2} {score,-6} {rsiCol,-10} {s.Trend ?? "N/A"} {sent}");
        }

        private void FormatSeasonalRow(StringBuilder sb, int rank, StockPickItem s)
        {
            var sym    = s.Symbol.Replace(".TO", "");
            var avgRet = s.MonthAvgReturn.HasValue
                ? $"{(s.MonthAvgReturn.Value >= 0 ? "+" : "")}{s.MonthAvgReturn.Value:F1}%"
                : "N/A";
            var winRate  = $"{s.SeasonalWinRate:F0}%";
            var sellMonth = s.BestSellMonth != null
                ? $"Sell→{s.BestSellMonth[..3]}" + (s.BestSellMonthReturn.HasValue
                    ? $"({(s.BestSellMonthReturn.Value >= 0 ? "+" : "")}{s.BestSellMonthReturn.Value:F1}%)"
                    : "")
                : "";

            sb.AppendLine($"{rank,-2} {sym,-9} ${s.Price,-7:F2} {avgRet,-7} {winRate,-5} {sellMonth}");
        }

        private string FormatNewsMessage(TopPicksResult picks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[4/4] NEWS SENTIMENT — {DateTime.Now:MMM dd} ({picks.TotalAnalyzed} stocks)");
            sb.AppendLine("#  Stock     Price    Score  Headline");
            sb.AppendLine("-- --------- -------- ------ ----------------------------------------");

            if (picks.News?.Any() == true)
            {
                for (int i = 0; i < picks.News.Count; i++)
                {
                    var s       = picks.News[i];
                    var sym     = s.Symbol.Replace(".TO", "");
                    var score   = s.NewsScore.HasValue
                        ? $"{(s.NewsScore.Value >= 0 ? "+" : "")}{s.NewsScore.Value:F2}"
                        : "N/A";
                    var headline = (s.TopHeadline ?? "No headline").Length > 42
                        ? (s.TopHeadline ?? "")[..42] + "…"
                        : (s.TopHeadline ?? "No headline");

                    sb.AppendLine($"{i + 1,-2} {sym,-9} ${s.Price,-7:F2} {score,-6} {headline}");
                }
            }
            else
            {
                sb.AppendLine("No bullish news signals found today");
            }

            // Macro sector summary
            if (picks.MacroSentiment?.Any() == true)
            {
                sb.AppendLine();
                sb.Append("Macro: ");
                foreach (var (sector, score) in picks.MacroSentiment)
                {
                    var arrow = score > 0.15 ? "📈" : score < -0.15 ? "📉" : "➡️";
                    sb.Append($"{char.ToUpper(sector[0])}{sector[1..]} {arrow}  ");
                }
            }

            return sb.ToString().TrimEnd();
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

        [JsonPropertyName("news")]
        public List<StockPickItem>? News { get; set; }

        [JsonPropertyName("macro_sentiment")]
        public Dictionary<string, double>? MacroSentiment { get; set; }

        [JsonPropertyName("macro_overall")]
        public double MacroOverall { get; set; }

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

        [JsonPropertyName("best_sell_month")]
        public string? BestSellMonth { get; set; }

        [JsonPropertyName("best_sell_month_return")]
        public double? BestSellMonthReturn { get; set; }

        [JsonPropertyName("news_score")]
        public double? NewsScore { get; set; }

        [JsonPropertyName("sentiment_label")]
        public string? SentimentLabel { get; set; }

        [JsonPropertyName("sentiment_boost")]
        public int SentimentBoost { get; set; }

        [JsonPropertyName("top_headline")]
        public string? TopHeadline { get; set; }

        [JsonPropertyName("has_divergence")]
        public bool HasDivergence { get; set; }

        [JsonPropertyName("entry_low")]
        public double? EntryLow { get; set; }

        [JsonPropertyName("entry_high")]
        public double? EntryHigh { get; set; }

        [JsonPropertyName("entry_mid")]
        public double? EntryMid { get; set; }

        [JsonPropertyName("stop_loss")]
        public double? StopLoss { get; set; }

        [JsonPropertyName("tp1")]
        public double? Tp1 { get; set; }

        [JsonPropertyName("tp2")]
        public double? Tp2 { get; set; }

        [JsonPropertyName("rr_ratio")]
        public double? RrRatio { get; set; }
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
        public string? NtfyTopic { get; set; }
    }
}
