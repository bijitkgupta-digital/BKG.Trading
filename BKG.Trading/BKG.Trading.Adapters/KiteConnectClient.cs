using BKG.Trading.Domain.Contract;
using KiteConnect;

namespace BKG.Trading.Adapters;

public sealed class KiteConnectClient : IBroker
{
    private const string TokenFileName = "kite_access_token.txt";

    private readonly Kite _kite;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    private KiteConnectClient(string apiKey, string apiSecret)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;

        _kite = new Kite(apiKey, Debug: true);
        _kite.SetSessionExpiryHook(OnTokenExpired);
    }

    public static async Task<KiteConnectClient> CreateAsync(string apiKey, string apiSecret)
    {
        var client = new KiteConnectClient(apiKey, apiSecret);

        await client.InitializeSessionAsync();

        return client;
    }

    private async Task InitializeSessionAsync()
    {
        string accessToken = await GetAccessTokenAsync();

        _kite.SetAccessToken(accessToken);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (File.Exists(TokenFileName))
        {
            if (File.GetLastWriteTime(TokenFileName).Date == DateTime.Today)
            {
                string cachedToken = await File.ReadAllTextAsync(TokenFileName);
                if (!string.IsNullOrWhiteSpace(cachedToken))
                    return cachedToken.Trim();
            }
        }

        Console.WriteLine();
        Console.WriteLine("Open the URL below in a browser:");
        Console.WriteLine(_kite.GetLoginURL());
        Console.WriteLine();

        Console.Write("Paste request_token: ");

        string requestToken =
            Console.ReadLine()?.Trim()
            ?? throw new InvalidOperationException(
                "request_token cannot be empty.");

        User user = await Task.Run(
            () => _kite.GenerateSession(
                requestToken,
                _apiSecret));

        string accessToken = user.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Failed to generate Kite access token.");
        }

        await File.WriteAllTextAsync(TokenFileName, accessToken);

        return accessToken;
    }

    public async Task<decimal> GetAvailableMarginAsync()
    {
        UserMargin margin = await Task.Run(() => _kite.GetMargins("equity"));
        return margin.Net;
    }

    public async Task PlaceLimitBuyOrderAsync(string symbol, int unitsToBuy, decimal triggerPrice)
    {
        if (unitsToBuy <= 0)
            return;

        await Task.Run(() =>
            _kite.PlaceOrder(
                Exchange: Constants.EXCHANGE_NSE,
                TradingSymbol: symbol,
                TransactionType: Constants.TRANSACTION_TYPE_BUY,
                Quantity: unitsToBuy,
                TriggerPrice: triggerPrice,
                Price: Math.Ceiling((triggerPrice * 1.0025m) / 0.10m) * 0.10m,
                Product: Constants.PRODUCT_CNC,
                OrderType: Constants.ORDER_TYPE_LIMIT,
                Variety: Constants.VARIETY_REGULAR
                //Variety: Constants.VARIETY_AMO
            ));


    }

    private void OnTokenExpired()
    {
        throw new InvalidOperationException("Kite session expired. Login again.");
    }

    public async Task PlaceMarketBuyOrderAsync(string symbol, int unitsToBuy)
    {
        if (unitsToBuy <= 0)
            return;

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Add("X-Kite-Version", "3");
        var _token = await GetAccessTokenAsync();
        client.DefaultRequestHeaders.Add("Authorization", $"token {_apiKey}:{_token}");

        var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("exchange", "NSE"),
            new KeyValuePair<string, string>("tradingsymbol", symbol),
            new KeyValuePair<string, string>("transaction_type", "BUY"),
            new KeyValuePair<string, string>("quantity", unitsToBuy.ToString()),
            new KeyValuePair<string, string>("product", "CNC"),
            new KeyValuePair<string, string>("order_type", "MARKET"),
            new KeyValuePair<string, string>("validity", "DAY"),
            new KeyValuePair<string, string>("market_protection", "-1")
        ]);

        using HttpResponseMessage response = await client.PostAsync("https://api.kite.trade/orders/regular", content);

        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kite market order failed: {responseBody}");
        }
    }

    public async Task<IEnumerable<string>> GetExistingHoldings()
    {
        var holdings = await Task.Run(() => _kite.GetHoldings());

        return holdings
            //.Where(h => h.Quantity > 0)
            .Select(h => h.TradingSymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}