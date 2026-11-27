using BKG.Trading.Domain.Contract;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BKG.Trading.Adapters
{
    public class YahooClient : ILTPProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<YahooClient>? _logger;

        public YahooClient(HttpClient httpClient, ILogger<YahooClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        public async Task<decimal> FetchLTPAsync(string symbol)
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            string yahooSymbol = $"{symbol}.NS";

            string url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}" +
                "?interval=1m&range=1d";

            string json = await _httpClient.GetStringAsync(url);

            using JsonDocument doc = JsonDocument.Parse(json);

            return doc.RootElement
                      .GetProperty("chart")
                      .GetProperty("result")[0]
                      .GetProperty("meta")
                      .GetProperty("regularMarketPrice")
                      .GetDecimal();
        }
        public async Task<IDictionary<string, decimal>> FetchLTPAsync(IEnumerable<string> symbols)
        {
            ArgumentNullException.ThrowIfNull(symbols);

            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols.Distinct())
            {
                result[symbol] = await FetchLTPAsync(symbol);
            }

            _logger?.LogInformation("Yahoo LTP fetch completed. SymbolCount={SymbolCount}", result.Count);

            return result;
        }
    }
}
