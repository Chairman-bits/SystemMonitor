using System.Net;
using System.Net.Http;
using System.Text.Json;
using StealthStockOverlay.Models;

namespace StealthStockOverlay.Services;

public class YahooFinanceService
{
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<string, string> JapaneseNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1301.T"] = "極洋",
        ["1605.T"] = "INPEX",
        ["1928.T"] = "積水ハウス",
        ["2502.T"] = "アサヒグループHD",
        ["2914.T"] = "日本たばこ産業",
        ["3003.T"] = "ヒューリック",
        ["3231.T"] = "野村不動産HD",
        ["4063.T"] = "信越化学工業",
        ["4452.T"] = "花王",
        ["4502.T"] = "武田薬品工業",
        ["4661.T"] = "オリエンタルランド",
        ["6501.T"] = "日立製作所",
        ["6758.T"] = "ソニーグループ",
        ["6902.T"] = "デンソー",
        ["7164.T"] = "全国保証",
        ["7203.T"] = "トヨタ自動車",
        ["8001.T"] = "伊藤忠商事",
        ["8002.T"] = "丸紅",
        ["8031.T"] = "三井物産",
        ["8053.T"] = "住友商事",
        ["8058.T"] = "三菱商事",
        ["8306.T"] = "三菱UFJFG",
        ["8316.T"] = "三井住友FG",
        ["8591.T"] = "オリックス",
        ["8725.T"] = "MS&AD",
        ["8766.T"] = "東京海上HD",
        ["8801.T"] = "三井不動産",
        ["9020.T"] = "JR東日本",
        ["9432.T"] = "日本電信電話",
        ["9433.T"] = "KDDI",
        ["9434.T"] = "ソフトバンク"
    };

    public YahooFinanceService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja,en-US;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public async Task<List<QuoteItem>> GetQuotesAsync(IEnumerable<string> rawSymbols)
    {
        var normalized = rawSymbols
            .Select(NormalizeSymbol)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (normalized.Count == 0)
        {
            return new List<QuoteItem>();
        }

        var bulkQuotes = await TryBulkQuoteAsync(normalized);
        if (bulkQuotes.Count > 0)
        {
            return OrderByInput(normalized, bulkQuotes);
        }

        var fallbackQuotes = new List<QuoteItem>();
        foreach (var symbol in normalized)
        {
            var quote = await TryChartQuoteAsync(symbol);
            if (quote is not null)
            {
                fallbackQuotes.Add(quote);
            }
        }

        if (fallbackQuotes.Count == 0)
        {
            throw new Exception("Yahoo側で株価取得が拒否されました（401/403/429 の可能性）。");
        }

        return OrderByInput(normalized, fallbackQuotes);
    }

    private async Task<List<QuoteItem>> TryBulkQuoteAsync(List<string> normalized)
    {
        var symbolsParam = string.Join(",", normalized);
        var urls = new[]
        {
            $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(symbolsParam)}",
            $"https://query2.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(symbolsParam)}"
        };

        foreach (var url in urls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var results = new List<QuoteItem>();
                if (!doc.RootElement.TryGetProperty("quoteResponse", out var quoteResponse))
                {
                    continue;
                }

                if (!quoteResponse.TryGetProperty("result", out var resultArray) || resultArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in resultArray.EnumerateArray())
                {
                    var symbol = GetString(item, "symbol");
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        continue;
                    }

                    var shortName = GetString(item, "shortName");
                    var longName = GetString(item, "longName");
                    var mappedName = GetJapaneseName(symbol);
                    var displayName = !string.IsNullOrWhiteSpace(mappedName) ? mappedName
                        : !string.IsNullOrWhiteSpace(shortName) ? shortName
                        : !string.IsNullOrWhiteSpace(longName) ? longName
                        : DenormalizeDisplaySymbol(symbol);

                    results.Add(new QuoteItem
                    {
                        Symbol = symbol,
                        DisplaySymbol = DenormalizeDisplaySymbol(symbol),
                        CompanyName = displayName,
                        Price = GetDecimal(item, "regularMarketPrice"),
                        Change = GetDecimal(item, "regularMarketChange"),
                        ChangePercent = GetDecimal(item, "regularMarketChangePercent")
                    });
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch
            {
            }
        }

        return new List<QuoteItem>();
    }

    private async Task<QuoteItem?> TryChartQuoteAsync(string symbol)
    {
        var urls = new[]
        {
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d",
            $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d"
        };

        foreach (var url in urls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("chart", out var chart))
                {
                    continue;
                }

                if (!chart.TryGetProperty("result", out var resultArray) || resultArray.ValueKind != JsonValueKind.Array || resultArray.GetArrayLength() == 0)
                {
                    continue;
                }

                var item = resultArray[0];
                var meta = item.TryGetProperty("meta", out var metaEl) ? metaEl : default;
                if (meta.ValueKind == JsonValueKind.Undefined)
                {
                    continue;
                }

                var returnedSymbol = GetString(meta, "symbol");
                var shortName = GetString(meta, "shortName");
                var targetSymbol = string.IsNullOrWhiteSpace(returnedSymbol) ? symbol : returnedSymbol;
                var mappedName = GetJapaneseName(targetSymbol);
                var displayName = !string.IsNullOrWhiteSpace(mappedName) ? mappedName
                    : !string.IsNullOrWhiteSpace(shortName) ? shortName
                    : DenormalizeDisplaySymbol(targetSymbol);

                var price = GetDecimal(meta, "regularMarketPrice");
                var previousClose = GetDecimal(meta, "chartPreviousClose");
                if (previousClose == 0)
                {
                    previousClose = GetDecimal(meta, "previousClose");
                }

                var change = price - previousClose;
                var changePercent = previousClose == 0 ? 0 : (change / previousClose) * 100m;

                return new QuoteItem
                {
                    Symbol = targetSymbol,
                    DisplaySymbol = DenormalizeDisplaySymbol(targetSymbol),
                    CompanyName = displayName,
                    Price = price,
                    Change = change,
                    ChangePercent = changePercent
                };
            }
            catch
            {
            }
        }

        return null;
    }

    private static string GetJapaneseName(string symbol)
    {
        return JapaneseNameMap.TryGetValue(NormalizeSymbol(symbol), out var name) ? name : "";
    }

    private static List<QuoteItem> OrderByInput(List<string> normalized, List<QuoteItem> quotes)
    {
        return quotes
            .OrderBy(q => normalized.FindIndex(x => x.Equals(q.Symbol, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static string NormalizeSymbol(string raw)
    {
        var value = (raw ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (value.All(char.IsDigit) && value.Length == 4)
        {
            return value + ".T";
        }

        return value;
    }

    public static string DenormalizeDisplaySymbol(string value)
    {
        if (value.EndsWith(".T", StringComparison.OrdinalIgnoreCase) && value.Length == 6)
        {
            return value[..4];
        }

        return value;
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? ""
            : "";
    }

    private static decimal GetDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        try
        {
            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(prop.GetString(), out var d) => d,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }
}
