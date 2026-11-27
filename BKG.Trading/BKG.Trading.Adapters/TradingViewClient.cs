using BKG.Trading.Domain.Contract;
using BKG.Trading.Domain.Constants;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed record TradingViewStock
{
    public required string Symbol { get; init; }
    public decimal? ChangePercent { get; init; }
    public decimal? ClosePrice { get; init; }
    public decimal? Atr { get; init; }
    public decimal? Rsi { get; init; }
    public decimal? Sma20 { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma100 { get; init; }
    public decimal? Sma200 { get; init; }
}

public sealed class TradingViewClient : IScreener
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TradingViewClient>? _logger;

    public TradingViewClient(HttpClient httpClient, ILogger<TradingViewClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IDictionary<string, decimal>> GetSwingStockListAsync()
    {
        var payload = BuildSwingStocksV2Payload();

        var response = await _httpClient.PostAsJsonAsync(TradingConstants.TradingView.ScannerEndpointUrl, payload);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        IDictionary<string, decimal> stocks = TopRankedStocks(json);
        _logger?.LogInformation("TradingView screener completed. SelectedCount={SelectedCount}", stocks.Count);

        return stocks;
    }    

    private IDictionary<string, decimal> TopRankedStocks(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement data = document.RootElement.GetProperty("data");

        List<TradingViewStock> stocks = new();

        foreach (JsonElement row in data.EnumerateArray())
        {
            string tradingViewSymbol = row.GetProperty("s").GetString() ?? string.Empty;

            string symbol =
                tradingViewSymbol.Contains(':')
                    ? tradingViewSymbol.Split(':')[1]
                    : tradingViewSymbol;

            JsonElement values = row.GetProperty("d");

            stocks.Add(new TradingViewStock
            {
                Symbol = symbol,

                ChangePercent = GetDecimalOrNull(values, 1),
                ClosePrice = GetDecimalOrNull(values, 2),
                Atr = GetDecimalOrNull(values, 3),
                Rsi = GetDecimalOrNull(values, 4),
                Sma20 = GetDecimalOrNull(values, 5),
                Sma50 = GetDecimalOrNull(values, 6),
                Sma100 = GetDecimalOrNull(values, 7),
                Sma200 = GetDecimalOrNull(values, 8)
            });
        }

        //Filtering
        var filteredStocks = stocks
            .Where(x =>
                x.ClosePrice.HasValue &&
                x.Sma20.HasValue &&
                x.ClosePrice > x.Sma20)

            .Where(x =>
                x.Sma20.HasValue &&
                x.Sma50.HasValue &&
                x.Sma20 > x.Sma50)

            .Where(x =>
                x.Sma50.HasValue &&
                x.Sma100.HasValue &&
                x.Sma50 > x.Sma100)

            .Where(x =>
                x.Sma100.HasValue &&
                x.Sma200.HasValue &&
                x.Sma100 > x.Sma200)

            .Where(x =>
                x.Rsi.HasValue &&
                x.Rsi < 70)

            .Where(x =>
            {
                if (!x.Atr.HasValue || !x.ClosePrice.HasValue)
                    return false;

                decimal atrPct =
                    (x.Atr.Value / x.ClosePrice.Value) * 100m;

                return atrPct >= TradingConstants.TradingView.AtrPercentMinimum &&
                       atrPct <= TradingConstants.TradingView.AtrPercentMaximum;
            })
            .Where(x =>
                x.ChangePercent.HasValue &&
                x.ChangePercent >= TradingConstants.TradingView.ChangePercentMinimum &&
                x.ChangePercent <= TradingConstants.TradingView.ChangePercentMaximum)

            .Where(x =>
            {
                if (!x.ClosePrice.HasValue ||
                    !x.Sma200.HasValue)
                    return false;

                decimal priceAboveSma200Pct =
                    ((x.ClosePrice.Value - x.Sma200.Value)
                        / x.Sma200.Value) * 100m;

                return priceAboveSma200Pct >= TradingConstants.TradingView.PriceAboveSma200PercentMinimum &&
                       priceAboveSma200Pct <= TradingConstants.TradingView.PriceAboveSma200PercentMaximum;
            });

        return filteredStocks.ToDictionary(
            x => x.Symbol,
            x => x.ChangePercent ?? 0m,
            StringComparer.OrdinalIgnoreCase);
    }
    private static decimal? GetDecimalOrNull(JsonElement values, int index)
    {
        if (values.GetArrayLength() <= index)
            return null;

        JsonElement value = values[index];

        if (value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number)
            return value.GetDecimal();

        return decimal.TryParse(value.ToString(), out decimal parsed) ? parsed : null;
    }
    private static object BuildSwingStocksV2Payload()
    {
        return new
        {
            symbols = new
            {
                query = new { types = Array.Empty<string>() },
                tickers = Array.Empty<string>()
            },
            columns = new[]
            {
                "name",
                "change",
                "close",
                "ATR",
                "RSI",
                "SMA20",
                "SMA50",
                "SMA100",
                "SMA200"
            },
            filter = new object[]
            {
                // NSE only
                new
                {
                    left = "exchange",
                    operation = "equal",
                    right = TradingConstants.TradingView.ExchangeNse
                },
                // Market Cap > 200B INR
                new
                {
                    left = "market_cap_basic",
                    operation = "greater",
                    right = TradingConstants.TradingView.MarketCapMinimum
                }
            }
        };
    }
}