using BKG.Trading.Domain.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BKG.Trading.Adapters
{
    public class YahooClient : ILTPProvider
    {
        private readonly HttpClient _httpClient;

        public YahooClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
    }
}
