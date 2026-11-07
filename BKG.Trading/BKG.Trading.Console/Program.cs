using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BKG.Trading.Domain.Interfaces;
using BKG.Trading.Application.Services;
using BKG.Trading.Repository.SQL;
using BKG.Trading.APIAdapter.Fyers;

namespace BKG.Trading.ConsoleApp;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("     BKG.TRADING CORE QUANT SYSTEM ENGINE INITIALIZATION         ");
        Console.WriteLine("=================================================================");

        // 1. Build the Configuration Engine Root
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 2. Extract Values Securely from JSON Sections
        string dbConnectionString = configuration.GetConnectionString("TradingDatabase")
            ?? throw new InvalidOperationException("Connection string 'TradingDatabase' not found in config.");

        string fyersClientId = configuration["FyersConfig:ClientId"]
            ?? throw new InvalidOperationException("Fyers ClientId configuration is missing.");

        string fyersAccessToken = configuration["FyersConfig:AccessToken"]
            ?? throw new InvalidOperationException("Fyers AccessToken configuration is missing.");

        string fyersRedirectUri = configuration["FyersConfig:RedirectUri"]
            ?? throw new InvalidOperationException("Fyers RedirectUri configuration is missing.");

        DateTime processingDate = DateTime.Today;
        //DateTime processingDate = new DateTime(2026, 11, 06);

        // 3. Setup Dependency Injection IoC Container (Error Resolved via Microsoft.Extensions.Http)
        var serviceProvider = new ServiceCollection()
            .AddHttpClient()
            .AddTransient<ITradingRepository>(sp => new DapperTradingRepository(dbConnectionString))
            .AddTransient<IMarketDataService>(sp => new FyersDataClient(fyersClientId, fyersAccessToken, fyersRedirectUri)) // Streamlined instantiation
            .AddTransient<TradingEngineService>()
            .BuildServiceProvider();

        try
        {
            // 4. Resolve the Engine Orchestrator
            var engine = serviceProvider.GetRequiredService<TradingEngineService>();

            // STEP 1: Execute Independent Candle Sync
            Console.WriteLine($"\n[STEP 1] Initializing remote OHLCV sync for {processingDate:yyyy-MM-dd}...");
            int syncedCount = engine.SyncCandleData(processingDate);
            Console.WriteLine($"✅ Sync complete. Successfully mirrored {syncedCount} asset sessions.");

            // STEP 2: Execute Indicator Computations
            Console.WriteLine("\n[STEP 2] Launching database analytical indicator computation processing...");
            engine.CalculateIndicators(processingDate);
            Console.WriteLine("✅ Math calculations and structural window sets updated.");

            // STEP 3: Stream Strategy Matrix Output
            Console.WriteLine("\n[STEP 3] Fetching top actionable momentum signals...");
            var optimalSignals = engine.GetTop5DailySignals().ToList();

            if (!optimalSignals.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⏸️ No active ETFs matched entry breakout and volume confirmation targets today.");
                Console.ResetColor();
                return;
            }

            // Print Output Visualization Matrix
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n================================================================================================");
            Console.WriteLine("🎯 TOP 5 SYSTEMATIC ACTIVE SWING TRADING BLUEPRINTS FOR TOMORROW'S SESSION:");
            Console.WriteLine("==================================================================================================");
            Console.ResetColor();
            Console.WriteLine($"{"SYMBOL",-15} | {"CLOSE PRICE",-11} | {"RSI TODAY",-9} | {"RSI YEST.",-9} | {"TARGET CHG",-10} | {"TRIGGER BUY PRICE",-15} | {"LIMIT BUY PRICE",-15}");
            Console.WriteLine(new string('-', 82));

            foreach (var sig in optimalSignals)
            {
                Console.WriteLine($"{sig.SYMBOL,-15} | {sig.Today_Close,11:F2} | {sig.RSI_Today,9:F2} | {sig.RSI_Yesterday,9:F2} | {sig.SellTarget,10:P1} | {sig.TriggerBuyPrice,15:F2} | {sig.LimitBuyPrice,15:F2}");
            }
            Console.WriteLine("==================================================================================================\n");

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Critical System Pipeline Termination Exception: {ex.Message}");
            Console.ResetColor();
        }
    }
}
