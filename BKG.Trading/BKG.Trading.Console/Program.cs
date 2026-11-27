using BKG.Trading.Adapters;
using BKG.Trading.Applications.StockSwing;
using BKG.Trading.Domain.Constants;
using BKG.Trading.Domain.Contract;
using BKG.Trading.Logging;
using BKG.Trading.Repository.SQL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal class Program
{
    static async Task Main(string[] args)
    {
        string logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "Logs");

        using ILoggerFactory loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = TradingConstants.Logging.ConsoleTimestampFormat;
            });
            logging.AddProvider(new TextFileLoggerProvider(logDirectoryPath, "SwingStockTrading"));
        });

        ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

        Console.WriteLine("=================================================================");
        Console.WriteLine("     BKG.TRADING CORE QUANT SYSTEM ENGINE INITIALIZATION          ");
        Console.WriteLine("=================================================================");

        logger.LogInformation("Trading engine initialization started.");

        try
        {
            IServiceProvider serviceProvider = await BuildServiceProvider(loggerFactory);

            // Future Parallel Execution
            await Task.WhenAll(
                //RunEtfAveragingAsync(serviceProvider),
                RunStockSwingAsync(serviceProvider));

            logger.LogInformation("Trading engine execution completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical system pipeline termination.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine($"❌ Critical System Pipeline Termination Exception: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task<IServiceProvider> BuildServiceProvider(ILoggerFactory loggerFactory)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string dbConnectionString =
            configuration.GetConnectionString("TradingDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'TradingDatabase' not found.");

        string kiteApiKey =
            configuration[TradingConstants.ConfigKeys.KiteApiKey]
            ?? throw new InvalidOperationException(
                "Kite ApiKey configuration is missing.");

        string kiteAccessToken =
            configuration[TradingConstants.ConfigKeys.KiteAccessToken]
            ?? throw new InvalidOperationException(
                "Kite AccessToken configuration is missing.");

        decimal minimumFundAllotment =
            configuration.GetValue<decimal?>(TradingConstants.ConfigKeys.MinimumFundAllotment)
            ?? TradingConstants.Defaults.MinimumFundAllotment;

        int maximumStockPoolSize =
            configuration.GetValue<int?>(TradingConstants.ConfigKeys.MaximumStockPoolSize)
            ?? TradingConstants.Defaults.MaximumStockPoolSize;

        string marketOrderType =
            configuration[TradingConstants.ConfigKeys.MarketOrderType]
            ?? TradingConstants.Defaults.MarketOrderType;

        ILogger<KiteConnectClient> kiteLogger = loggerFactory.CreateLogger<KiteConnectClient>();
        IBroker kiteBroker = await KiteConnectClient.CreateAsync(kiteApiKey, kiteAccessToken, kiteLogger);

        return new ServiceCollection()
            .AddSingleton(loggerFactory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddHttpClient()
            .AddSingleton<ITradingRepository>(_ => new DapperTradingRepository(dbConnectionString))
            .AddSingleton<ILTPProvider>(sp => new YahooClient(new HttpClient(), sp.GetRequiredService<ILogger<YahooClient>>()))
            .AddSingleton<IScreener>(sp => new TradingViewClient(new HttpClient(), sp.GetRequiredService<ILogger<TradingViewClient>>()))
            .AddSingleton<IBroker>(kiteBroker)
            .AddTransient<ETFPurchaseService>()
            .AddTransient<StockPurchaseService>(sp => new StockPurchaseService(
                sp.GetRequiredService<IScreener>(),
                sp.GetRequiredService<ILTPProvider>(),
                sp.GetRequiredService<IBroker>(),
                sp.GetRequiredService<ILogger<StockPurchaseService>>(),
                minimumFundAllotment,
                maximumStockPoolSize,
                marketOrderType))
            .BuildServiceProvider();
    }

    private static async Task RunEtfAveragingAsync(IServiceProvider serviceProvider)
    {
        ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        Console.WriteLine();
        Console.WriteLine("Running ETF Averaging Strategy...");
        logger.LogInformation("Starting ETF averaging strategy execution.");

        var purchaseService =
            serviceProvider.GetRequiredService<ETFPurchaseService>();

        var result =
            await purchaseService.ExecuteAsync();

        if (!result.AnyPurchase)
        {
            logger.LogInformation("ETF averaging finished with no purchases.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No ETF purchases triggered today.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine();
        Console.WriteLine("ETF Purchase Summary");
        Console.WriteLine("--------------------");

        foreach (var purchase in result.Purchases)
        {
            Console.WriteLine(
                $"{purchase.Symbol,-10} " +
                $"Price: {purchase.Ltp:N2} " +
                $"Units: {purchase.UnitsPurchased}");
        }

        logger.LogInformation("ETF averaging finished with {PurchaseCount} purchases.", result.Purchases.Count);

        Console.ResetColor();

        await Task.CompletedTask;
    }

    private static async Task RunStockSwingAsync(IServiceProvider serviceProvider)
    {
        ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        Console.WriteLine();
        Console.WriteLine("Running Stock Swing Strategy...");
        logger.LogInformation("Starting stock swing strategy execution.");

        var purchaseService = serviceProvider.GetRequiredService<StockPurchaseService>();

        var result = await purchaseService.ExecuteAsync();

        if (!result.AnyPurchase)
        {
            logger.LogInformation("Stock swing finished with no purchases.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No Stock purchases triggered today.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine();
        Console.WriteLine("Stock Purchase Summary");
        Console.WriteLine("--------------------");

        foreach (var purchase in result.Purchases)
        {
            Console.WriteLine(
                $"{purchase.Symbol,-10} " +
                $"Price: {purchase.Ltp:N2} " +
                $"Units: {purchase.UnitsPurchased}");
        }

        logger.LogInformation("Stock swing finished with {PurchaseCount} purchases.", result.Purchases.Count);

        Console.ResetColor();

        await Task.CompletedTask;
    }
}