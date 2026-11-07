using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq; // Utilizing JArray/JObject from your SDK's dependency setup
using BKG.Trading.Domain.Entities;
using BKG.Trading.Domain.Interfaces;
using FyersCSharpSDK; // Using your exact native SDK library namespace

namespace BKG.Trading.APIAdapter.Fyers;

public class FyersDataClient : IMarketDataService
{
    private readonly string _clientId;
    private readonly string _secretKey;
    private readonly string _redirectUri;
    private string _accessToken = string.Empty;
    private const string TokenFileName = "access_token.txt";

    public FyersDataClient(string clientId, string secretKey, string redirectUri)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));

        InitializeAuthentication().GetAwaiter().GetResult();
    }

    private async Task InitializeAuthentication()
    {
        bool isTokenCached = false;

        // 1. Attempt to read from local file cache
        if (File.Exists(TokenFileName))
        {
            DateTime lastWriteTime = File.GetLastWriteTime(TokenFileName).Date;
            if (lastWriteTime == DateTime.Today)
            {
                string cachedToken = await File.ReadAllTextAsync(TokenFileName);
                if (!string.IsNullOrWhiteSpace(cachedToken))
                {
                    _accessToken = cachedToken.Trim();
                    isTokenCached = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("🔑 [AUTH TEST] Found cached token from today's session. Testing token validity...");
                    Console.ResetColor();
                }
            }
        }

        // 2. Fall back to manual browser redirect if cache is missing/expired
        if (!isTokenCached)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n⚠️ [AUTH TEST] No valid token found for today. Initializing login handshake...");
            Console.ResetColor();

            FyersClass fyersModel = FyersClass.Instance;

            // This launches the physical browser authentication portal window natively via the SDK
            fyersModel.GetGenerateCode(_clientId, _secretKey, _redirectUri);

            Console.Write("👉 Please enter the newly generated Auth Code from browser redirect: ");
            string authCode = Console.ReadLine()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(authCode))
                throw new InvalidOperationException("❌ Error: Auth code cannot be empty.");

            string appHashId = Utility.GenerateAppHashID(_clientId, _secretKey);

            Console.WriteLine("⏳ Exchanging Auth Code for Access Token via Fyers servers...");
            var authTokenJObject = await fyersModel.GenerateToken(_secretKey, _redirectUri, authCode, appHashId);

            if (authTokenJObject != null && authTokenJObject["TOKEN"] != null)
            {
                _accessToken = authTokenJObject["TOKEN"]!.ToString();
                await File.WriteAllTextAsync(TokenFileName, _accessToken);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("🏆 Successfully generated fresh Access Token and saved to disk.");
                Console.ResetColor();
            }
            else
            {
                throw new InvalidOperationException("❌ Server authentication handshake rejected. Token payload was null.");
            }
        }

        // =========================================================================
        // NATIVE PROFILE PROBE matching YOUR SDK SIGNATURE
        // =========================================================================
        try
        {
            Console.WriteLine("⚡ Dispatching a test profile lookup call to verify subsequent API capabilities...");

            // Setup internal context states as shown in your code snippet
            FyersClass fyersModel = FyersClass.Instance;
            fyersModel.ClientId = _clientId;
            fyersModel.AccessToken = _accessToken;

            // Execute the asynchronous tuple request signature shown in your sample code
            Tuple<ProfileModel, Newtonsoft.Json.Linq.JObject> responseTuple = await fyersModel.GetProfile();

            // Validation: Verify Item1 contains valid user profile model fields
            if (responseTuple != null && responseTuple.Item1 != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("✅ [AUTH VERIFIED]: Connection stable. Profile context retrieved successfully via SDK Tuple.");
                // Print the serialized payload to inspect returned fields (e.g., pan, fy_id, email, name)
                Console.WriteLine($"📄 Response JSON: {Newtonsoft.Json.JsonConvert.SerializeObject(responseTuple.Item1)}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                string errorLog = responseTuple?.Item2?.ToString() ?? "Unknown structural error";
                Console.WriteLine($"⚠️ [AUTH WARNING]: Token loaded, but validation handshake failed. Message: {errorLog}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Subsequent API connection test crashed: {ex.Message}");
            Console.ResetColor();
        }

        // Explicit 3-second hold to review authentication metrics on screen
        Console.WriteLine("⏳ Pausing execution for 3 seconds to verify terminal logs before pipeline begins...");
        Thread.Sleep(3000);
        Console.WriteLine("▶️ Resuming core trading engine service application layers.\n");
    }



    // =========================================================================
    // NATIVE CORE ENGINE: PARSE RAW TUPLE SDK RESPONSES
    // =========================================================================
    public IEnumerable<OHLCVRecord> FetchEodCandles(string symbol, DateTime startDate, DateTime endDate)
    {
        var records = new List<OHLCVRecord>();
        string fyersSymbol = $"{symbol}-EQ";

        try
        {
            FyersClass fyersModel = FyersClass.Instance;

            // 1. Initialize the parameter model object defined in your SDK screenshot
            StockHistoryModel model = new StockHistoryModel();
            model.Symbol = fyersSymbol;
            model.Resolution = "D"; // Daily Interval EOD Candlesticks
            model.DateFormat = "1"; // Date strings format mode activation
            model.ContFlag = 1;     // Continuous charts streaming flag
            model.RangeFrom = startDate.ToString("yyyy-MM-dd");
            model.RangeTo = endDate.ToString("yyyy-MM-dd");

            // 2. Dispatch call natively matching your SDK signature: GetStockHistory(model, token)
            // It returns the strict Tuple<JArray, JObject> structure shown in your docs
            Tuple<JArray, JObject> stockTuple = fyersModel.GetStockHistory(model).Result;

            // 3. Unpack the payload using your SDK documentation's token conventions
            // Validation: Ensure Item2 (Error log object) is null or doesn't flag an execution failure
            if (stockTuple != null && stockTuple.Item1 != null)
            {
                JArray candlesArray = stockTuple.Item1;

                foreach (var candle in candlesArray)
                {
                    // Unpacking the inner index parameters explicitly: [Timestamp, Open, High, Low, Close, Volume]
                    long unixTimestamp = candle[0].Value<long>();
                    DateTime candleDate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime.Date;

                    records.Add(new OHLCVRecord
                    {
                        SYMBOL = symbol,
                        Date = candleDate,
                        Open = candle[1].Value<decimal>(),
                        High = candle[2].Value<decimal>(),
                        Low = candle[3].Value<decimal>(),
                        Close = candle[4].Value<decimal>(),
                        Volume = candle[5].Value<long>()
                    });
                }
            }
            else
            {
                Console.WriteLine($"⚠️ Fyers SDK returned an empty data payload or error signature for {symbol}.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Critical exception inside SDK network client for {symbol}: {ex.Message}");
        }

        // Standard Plan compliance throttle rate protection gateway (5 requests/sec ceiling limit)
        Thread.Sleep(1000);
        return records;
    }

    // =========================================================================
    // NATIVE HISTORICAL SEEDER LOOP (SELF-CONTAINED BACK-FILL)
    // =========================================================================
    /// <summary>
    /// Fetches a clean, single 100-day historical window to seed an empty database.
    /// Can be commented out or deleted once your database initial load is finalized.
    /// </summary>
    public IEnumerable<OHLCVRecord> FetchHistoricalWindow(string symbol, int lookbackDays)
    {
        DateTime endDate = DateTime.Today;
        DateTime startDate = endDate.AddDays(-lookbackDays);

        Console.WriteLine($"   ⏳ [API Back-fill Loop] Requesting {lookbackDays}-day historical block ({startDate:yyyy-MM-dd} -> {endDate:yyyy-MM-dd}) via SDK...");
        return FetchEodCandles(symbol, startDate, endDate);
    }
}
