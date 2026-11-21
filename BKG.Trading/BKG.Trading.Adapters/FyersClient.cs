using BKG.Trading.Domain.Contract;
using FyersCSharpSDK;
using Newtonsoft.Json.Linq;

namespace BKG.Trading.Adapters;

public sealed class FyersClient : ILTPProvider
{
    private const string TokenFileName = "access_token.txt";
    private const int MaxSymbolsPerRequest = 50;
    private static readonly TimeSpan ApiDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly string _appId;
    private readonly string _secretKey;
    private readonly string _redirectUri;

    private string _accessToken = string.Empty;

    private FyersClient(HttpClient httpClient, string appId, string secretKey, string redirectUri)
    {
        _httpClient = httpClient;
        _appId = appId;
        _secretKey = secretKey;
        _redirectUri = redirectUri;
    }

    public static async Task<FyersClient> CreateAsync(HttpClient httpClient, string appId, string secretKey, string redirectUri)
    {
        var client = new FyersClient(httpClient, appId, secretKey, redirectUri);
        await client.AuthenticateAsync();
        return client;
    }

    public async Task<IDictionary<string, decimal>> FetchLTPAsync(
    IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbolList.Count == 0)
        {
            return new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, decimal>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var batch in Batch(symbolList, MaxSymbolsPerRequest))
        {
            List<StockModel> quotes =
                await FetchLTPBatchAsync(batch);

            foreach (var quote in quotes)
            {
                Console.WriteLine(quote);
                string symbol = NormalizeSymbol(quote.Symbol);
                // TT appears to be the traded price in FYERS SDK
                if (decimal.TryParse(quote.TT, out decimal ltp))
                {
                    result[symbol] = ltp;
                }
            }
        }

        return result;
    }

    // ==========================================================
    // AUTHENTICATION
    // ==========================================================

    private async Task AuthenticateAsync()
    {
        _accessToken = await GetAccessTokenAsync();

        var fyers = FyersClass.Instance;

        fyers.ClientId = _appId;
        fyers.AccessToken = _accessToken;

        var profile = await fyers.GetProfile();

        if (profile?.Item1 == null)
        {
            throw new InvalidOperationException(
                $"Authentication validation failed: {profile?.Item2}");
        }

        Console.WriteLine(
            $"Connected to FYERS account: {profile.Item1.Name}");
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (File.Exists(TokenFileName))
        {
            var tokenDate = File.GetLastWriteTime(TokenFileName).Date;

            if (tokenDate == DateTime.Today)
            {
                string token = await File.ReadAllTextAsync(TokenFileName);

                if (!string.IsNullOrWhiteSpace(token))
                    return token.Trim();
            }
        }

        var fyers = FyersClass.Instance;

        fyers.GetGenerateCode(
            _appId,
            _secretKey,
            _redirectUri);

        Console.WriteLine();
        Console.Write("Paste auth_code from redirected URL: ");

        string authCode = Console.ReadLine()?.Trim()
            ?? throw new InvalidOperationException(
                "Auth code cannot be empty.");

        string appHashId =
            Utility.GenerateAppHashID(
                _appId,
                _secretKey);

        JObject tokenResponse =
            await fyers.GenerateToken(
                _secretKey,
                _redirectUri,
                authCode,
                appHashId);

        string? accessToken =
            tokenResponse?["TOKEN"]?.ToString();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                $"Unable to retrieve access token. Response: {tokenResponse}");
        }

        await File.WriteAllTextAsync(
            TokenFileName,
            accessToken);

        return accessToken;
    }

    // ==========================================================
    // COMMON API METHOD
    // ==========================================================

    private async Task<List<StockModel>> FetchLTPBatchAsync(
    IEnumerable<string> symbols)
    {
        var fyersSymbols = symbols
            .Select(s => $"NSE:{s}-EQ")
            .ToList();

        var fyers = FyersClass.Instance;

        Tuple<List<StockModel>, JObject> response =
            await fyers.GetStockQuotesV2(fyersSymbols);

        await Task.Delay(ApiDelay);

        if (response?.Item1 == null)
        {
            throw new InvalidOperationException(
                $"FYERS quote request failed. {response?.Item2}");
        }

        return response.Item1;
    }

    // ==========================================================
    // HELPERS
    // ==========================================================

    private static string NormalizeSymbol(string fyersSymbol)
    {
        return fyersSymbol
            .Replace("NSE:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-EQ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static IEnumerable<List<string>> Batch(List<string> source, int batchSize)
    {
        for (int i = 0; i < source.Count; i += batchSize)
        {
            yield return source
                .Skip(i)
                .Take(batchSize)
                .ToList();
        }
    }

    public Task<decimal> FetchLTPAsync(string symbol)
    {
        throw new NotImplementedException("FyersClient.FetchLTPAsync");
    }
}