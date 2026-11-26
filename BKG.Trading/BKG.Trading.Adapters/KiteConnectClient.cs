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
        try
        {
            _kite.SetSessionExpiryHook(OnTokenExpired);
        }
        catch (InvalidOperationException)
        {
            // Handle the Token expiration exception in the OnTokenExpired method and reinitiate the login process to get a new token.
            CreateAsync(_apiKey, _apiSecret).Wait();
        }
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
                Exchange: Constants.Exchange.NSE,
                TradingSymbol: symbol,
                TransactionType: Constants.Transaction.Buy,
                Quantity: unitsToBuy,
                TriggerPrice: triggerPrice,
                Price: Math.Ceiling((triggerPrice * 1.0025m) / 0.10m) * 0.10m,
                Product: Constants.Product.CNC,
                OrderType: Constants.OrderType.Limit,
                Variety: Constants.Variety.Regular
                //Variety: Constants.Variety.AMO
            ));


    }

    private void OnTokenExpired()
    {
        // Here I want to delete the token file so that next time user can login again and get new token
        if (File.Exists(TokenFileName))
        {
            File.Delete(TokenFileName);
        }
        Console.WriteLine("Kite session expired. Login again.");
        // Then rather than below Exception throwing, I want to reinitiate the Login process to get new token and set it again.
        // But since this is a callback method, I cannot make it async.
        // So I will just throw an exception here and handle it in the calling method to reinitiate the login process.
        throw new InvalidOperationException("Kite session expired. Login again.");
    }

    public async Task PlaceMarketBuyOrderAsync(string symbol, int unitsToBuy)
    {
        if (unitsToBuy <= 0)
            return;

        await Task.Run(() =>
            _kite.PlaceOrder(
                Exchange: Constants.Exchange.NSE,
                TradingSymbol: symbol,
                TransactionType: Constants.Transaction.Buy,
                Quantity: unitsToBuy,
                Product: Constants.Product.CNC,
                OrderType: Constants.OrderType.Market,
                MarketProtection: -1,
                Validity: Constants.Validity.Day,
                Variety: Constants.Variety.Regular
            ));
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