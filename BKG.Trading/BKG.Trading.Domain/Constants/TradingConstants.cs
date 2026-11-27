namespace BKG.Trading.Domain.Constants;

public static class TradingConstants
{
    public static class Defaults
    {
        public const decimal MinimumFundAllotment = 200_000m;
        public const int MaximumStockPoolSize = 10;
        public const string MarketOrderType = "Market";
        public const string LimitOrderType = "Limit";
        public static readonly TimeSpan BuyOrderPlacementDelay = TimeSpan.FromSeconds(1);
    }

    public static class ConfigKeys
    {
        public const string KiteApiKey = "KiteConfig:ApiKey";
        public const string KiteAccessToken = "KiteConfig:AccessToken";
        public const string DefaultBuyOrderType = "Trading:DefaultBuyOrderType";
        public const string MinimumFundAllotment = "Trading:MinimumFundAllotment";
        public const string MaximumStockPoolSize = "Trading:MaximumStockPoolSize";
        public const string MarketOrderType = "Trading:MarketOrderType";
        public const string LimitOrderType = "Trading:LimitOrderType";
    }

    public static class Logging
    {
        public const string ConsoleTimestampFormat = "HH:mm:ss ";
    }

    public static class SellGtt
    {
        public const decimal MinimumSellMarkupPercentage = 1.57m;
        public const decimal PriceTickSize = 0.05m;
    }

    public static class TradingView
    {
        public const string ScannerEndpointUrl = "https://scanner.tradingview.com/india/scan";
        public const string ExchangeNse = "NSE";
        public const decimal AtrPercentMinimum = 1.6m;
        public const decimal AtrPercentMaximum = 3.8m;
        public const decimal ChangePercentMinimum = -3.5m;
        public const decimal ChangePercentMaximum = 1m;
        public const decimal PriceAboveSma200PercentMinimum = 3m;
        public const decimal PriceAboveSma200PercentMaximum = 20m;
        public const long MarketCapMinimum = 200_000_000_000;
    }

    public static class Broker
    {
        public const decimal LimitOrderPriceBufferMultiplier = 1.0025m;
        public const decimal LimitOrderTickSize = 0.10m;
        public static readonly TimeSpan DefaultOrderCompletionTimeout = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan DefaultOrderStatusPollInterval = TimeSpan.FromSeconds(2);
    }
}
