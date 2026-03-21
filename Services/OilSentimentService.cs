using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FinanceApi.Services
{
    /// <summary>
    /// Runs oil_sentiment.py and returns structured signal data for WTI and Brent crude.
    /// Caches the result for 30 minutes (news and prices don't change that fast).
    /// </summary>
    public class OilSentimentService
    {
        private readonly ILogger<OilSentimentService> _logger;
        private readonly string _scriptsPath;

        private JsonDocument? _cachedResult;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(30);

        public OilSentimentService(ILogger<OilSentimentService> logger)
        {
            _logger = logger;

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot = Directory.GetParent(appDirectory)?.Parent?.Parent?.Parent?.FullName;

            if (projectRoot != null && Directory.Exists(Path.Combine(projectRoot, "scripts")))
                _scriptsPath = Path.Combine(projectRoot, "scripts");
            else
                _scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
        }

        public async Task<JsonDocument?> GetSentimentAsync()
        {
            if (_cachedResult != null && DateTime.Now < _cacheExpiry)
            {
                _logger.LogInformation("Returning cached oil sentiment (expires {Expiry:HH:mm})", _cacheExpiry);
                return _cachedResult;
            }

            var script = Path.Combine(_scriptsPath, "oil_sentiment.py");
            if (!File.Exists(script))
            {
                _logger.LogError("oil_sentiment.py not found at {Path}", script);
                return null;
            }

            _logger.LogInformation("Running oil_sentiment.py...");

            var startInfo = new ProcessStartInfo
            {
                FileName               = "python",
                Arguments              = $"\"{script}\" --json",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = _scriptsPath
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(errors))
                _logger.LogInformation("oil_sentiment.py log:\n{Errors}", errors);

            if (process.ExitCode != 0)
            {
                _logger.LogError("oil_sentiment.py exited with code {Code}", process.ExitCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogError("No output from oil_sentiment.py");
                return null;
            }

            try
            {
                var doc = JsonDocument.Parse(output);
                _cachedResult = doc;
                _cacheExpiry  = DateTime.Now + _cacheTtl;
                _logger.LogInformation("Oil sentiment cached until {Expiry:HH:mm}", _cacheExpiry);
                return doc;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to parse oil_sentiment.py output: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Formats the oil signal as a casual, plain-English message suitable for SMS or Telegram.
        /// </summary>
        public string FormatSmsMessage(JsonDocument doc)
        {
            var sb   = new StringBuilder();
            var root = doc.RootElement;

            var newsDir   = root.TryGetProperty("news_direction", out var nd) ? nd.GetString() : "Neutral";
            var newsScore = root.TryGetProperty("news_sentiment", out var ns) ? ns.GetDouble() : 0;

            sb.AppendLine($"⛽ OIL MARKET UPDATE — {DateTime.Now:MMM dd, h:mm tt}");
            sb.AppendLine();

            // News summary in plain English
            var newsSummary = newsDir switch
            {
                "Bullish" => $"📰 News is leaning BULLISH — mostly supply-risk/conflict headlines pushing prices up.",
                "Bearish" => $"📰 News is leaning BEARISH — mostly demand-concern or supply-increase headlines.",
                _         => $"📰 News is MIXED — no strong directional headline theme today."
            };
            sb.AppendLine(newsSummary);
            sb.AppendLine();

            if (root.TryGetProperty("instruments", out var instruments))
            {
                foreach (var inst in instruments.EnumerateObject())
                {
                    var name = inst.Name;
                    if (!inst.Value.TryGetProperty("tech",   out var tech)) continue;
                    if (!inst.Value.TryGetProperty("signal", out var sig))  continue;

                    var price  = tech.TryGetProperty("price",          out var p)  ? p.GetDouble()  : 0;
                    var rsi    = tech.TryGetProperty("rsi",            out var r)  ? r.GetDouble()  : 50;
                    var dayChg = tech.TryGetProperty("day_chg_pct",    out var dc) ? dc.GetDouble() : 0;
                    var wkChg  = tech.TryGetProperty("week_chg_pct",   out var wc) ? wc.GetDouble() : 0;
                    var trend  = tech.TryGetProperty("trend",          out var tr) ? tr.GetString() : "Mixed";
                    var dir    = sig.TryGetProperty("direction",       out var d)  ? d.GetString()  : "NEUTRAL / WAIT";
                    var conf   = sig.TryGetProperty("confidence",      out var c)  ? c.GetInt32()   : 50;

                    var dayWord  = dayChg >= 0 ? $"up {dayChg:F1}%" : $"down {Math.Abs(dayChg):F1}%";
                    var wkWord   = wkChg  >= 0 ? $"+{wkChg:F1}%"   : $"{wkChg:F1}%";

                    // RSI plain English
                    var rsiNote = rsi >= 80 ? "extremely overbought (very high, pullback likely)" :
                                  rsi >= 70 ? "overbought (getting stretched)" :
                                  rsi <= 25 ? "extremely oversold (very low, bounce possible)" :
                                  rsi <= 35 ? "oversold (looks cheap technically)" :
                                  "neutral range";

                    // Signal plain English
                    var signalNote = dir switch
                    {
                        "BUY"  => $"🟢 LEAN BUY — technicals suggest a dip-buy opportunity.",
                        "SELL" => $"🔴 LEAN SELL — price looks extended, shorting could work.",
                        _      => $"🟡 WAIT — price has moved too far too fast. Better to wait for a pullback or clearer setup."
                    };

                    sb.AppendLine($"━━ {name} — ${price:F2} ({dayWord} today, {wkWord} this week)");
                    sb.AppendLine($"Trend: {trend} | RSI: {rsi:F0} ({rsiNote})");
                    sb.AppendLine(signalNote);
                    sb.AppendLine($"Confidence in this signal: {conf}%");

                    // Forecast in plain English
                    if (tech.TryGetProperty("forecast", out var fc))
                    {
                        var probUp = fc.TryGetProperty("prob_up_pct",            out var pu) ? pu.GetInt32()  : 50;
                        var mrTgt  = fc.TryGetProperty("mean_reversion_target",  out var mt) ? mt.GetDouble() : 0;
                        var mrPct  = fc.TryGetProperty("mean_reversion_chg_pct", out var mp) ? mp.GetDouble() : 0;

                        double f1Lo = 0, f1Hi = 0, f5Lo = 0, f5Hi = 0;
                        if (fc.TryGetProperty("forecast_1d", out var f1))
                        {
                            f1.TryGetProperty("low",  out var fl); f1Lo = fl.GetDouble();
                            f1.TryGetProperty("high", out var fh); f1Hi = fh.GetDouble();
                        }
                        if (fc.TryGetProperty("forecast_5d", out var f5))
                        {
                            f5.TryGetProperty("low",  out var fl); f5Lo = fl.GetDouble();
                            f5.TryGetProperty("high", out var fh); f5Hi = fh.GetDouble();
                        }

                        var dirBias  = probUp >= 50 ? $"{probUp}% chance it goes up" : $"{100 - probUp}% chance it goes down";
                        var mrNote   = mrPct < -5 ? $"If the market cools off, it would normally fall back to ${mrTgt:F2} ({mrPct:F1}% from here)."
                                     : mrPct >  5 ? $"If the market catches up, it could rise to ${mrTgt:F2} ({mrPct:+F1}% from here)."
                                     : $"Price is near its average — ${mrTgt:F2} is the fair-value reference.";

                        sb.AppendLine();
                        sb.AppendLine($"📊 Forecast: Based on history, {dirBias}.");
                        sb.AppendLine($"   Tomorrow likely between ${f1Lo:F2} – ${f1Hi:F2}");
                        sb.AppendLine($"   Next 5 days likely between ${f5Lo:F2} – ${f5Hi:F2}");
                        sb.AppendLine($"   {mrNote}");

                        // Order suggestions in plain English
                        if (sig.TryGetProperty("orders", out var orders))
                        {
                            if (orders.TryGetProperty("buy", out var buy) && orders.TryGetProperty("sell", out var sell))
                            {
                                var bMod = buy.TryGetProperty("moderate",  out var bm)  ? bm.GetDouble()  : 0;
                                var bSl  = buy.TryGetProperty("stop_loss", out var bs)  ? bs.GetDouble()  : 0;
                                var bTp1 = buy.TryGetProperty("tp1",       out var bt1) ? bt1.GetDouble() : 0;
                                var sAgg = sell.TryGetProperty("aggressive",out var sa) ? sa.GetDouble()  : 0;
                                var sSl  = sell.TryGetProperty("stop_loss", out var ss) ? ss.GetDouble()  : 0;
                                var sTp1 = sell.TryGetProperty("tp1",       out var st1)? st1.GetDouble() : 0;

                                sb.AppendLine();
                                sb.AppendLine($"💡 If buying a dip: enter near ${bMod:F2}, stop-loss at ${bSl:F2}, take profit at ${bTp1:F2}");
                                sb.AppendLine($"💡 If shorting: enter near ${sAgg:F2}, stop-loss at ${sSl:F2}, take profit at ${sTp1:F2}");
                            }
                        }
                    }

                    sb.AppendLine();
                }
            }

            // Top headline
            if (root.TryGetProperty("top_headlines", out var headlines))
            {
                var count = 0;
                sb.AppendLine("🗞 Key Headlines:");
                foreach (var h in headlines.EnumerateArray())
                {
                    if (count >= 2) break;
                    var title = h.TryGetProperty("title", out var ht) ? ht.GetString() : "";
                    sb.AppendLine($"  • {title?[..Math.Min(75, title?.Length ?? 0)]}");
                    count++;
                }
                sb.AppendLine();
            }

            sb.Append("⚠️ This is a signal tool, not advice. Always set a stop-loss.");
            return sb.ToString().TrimEnd();
        }
    }
}
