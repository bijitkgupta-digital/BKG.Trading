using BKG.Trading.Domain.Contract;
using KiteConnect;
using Microsoft.Extensions.Logging;
using System.Globalization;
using TradingConstants = BKG.Trading.Domain.Constants.TradingConstants;

namespace BKG.Trading.Adapters;

public sealed class KiteConnectClient : IBroker
{
    private const string TokenFileName = "kite_access_token.txt";
    private static readonly TimeSpan DefaultOrderCompletionTimeout = TradingConstants.Broker.DefaultOrderCompletionTimeout;
    private static readonly TimeSpan DefaultOrderStatusPollInterval = TradingConstants.Broker.DefaultOrderStatusPollInterval;

    private readonly Kite _kite;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger<KiteConnectClient>? _logger;

    private KiteConnectClient(string apiKey, string apiSecret, ILogger<KiteConnectClient>? logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _logger = logger;

        _kite = new Kite(apiKey, Debug: true);
        try
        {
            _kite.SetSessionExpiryHook(OnTokenExpired);
        }
        catch (InvalidOperationException)
        {
            _logger?.LogWarning("Kite session hook setup encountered an invalid operation. Reinitializing session.");
            CreateAsync(_apiKey, _apiSecret, _logger).Wait();
        }
    }

    public static async Task<KiteConnectClient> CreateAsync(string apiKey, string apiSecret, ILogger<KiteConnectClient>? logger = null)
    {
        var client = new KiteConnectClient(apiKey, apiSecret, logger);

        await client.InitializeSessionAsync();

        client._logger?.LogInformation("Kite client initialized successfully.");

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
                {
                    _logger?.LogInformation("Using cached Kite access token from token file.");
                    return cachedToken.Trim();
                }
            }
        }

        _logger?.LogInformation("Kite access token unavailable for today. Interactive login required.");
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

        _logger?.LogInformation("Kite access token generated and stored successfully.");

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
                Price: Math.Ceiling((triggerPrice * TradingConstants.Broker.LimitOrderPriceBufferMultiplier) / TradingConstants.Broker.LimitOrderTickSize) * TradingConstants.Broker.LimitOrderTickSize,
                Product: Constants.Product.CNC,
                OrderType: Constants.OrderType.Limit,
                Variety: Constants.Variety.Regular
                //Variety: Constants.Variety.AMO
            ));


    }

    private void OnTokenExpired()
    {
        if (File.Exists(TokenFileName))
        {
            File.Delete(TokenFileName);
        }

        _logger?.LogWarning("Kite session expired. Deleted local token cache.");
        Console.WriteLine("Kite session expired. Login again.");
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

    public async Task<CompletedOrderExecution> PlaceMarketBuyOrderAndWaitForCompletionAsync(
        string symbol,
        int unitsToBuy,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (unitsToBuy <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsToBuy), "Quantity should be greater than zero.");

        OrderResponse orderResponse = await Task.Run(() =>
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
            ), cancellationToken);

        _logger?.LogInformation("Placed market buy order. Symbol={Symbol}, Quantity={Quantity}, OrderId={OrderId}", symbol, unitsToBuy, orderResponse.OrderId);

        Order completedOrder = await WaitForOrderCompletionAsync(
            orderResponse.OrderId,
            timeout,
            pollInterval,
            cancellationToken);

        return MapCompletedOrder(completedOrder);
    }

    public async Task<CompletedOrderExecution> PlaceLimitBuyOrderAndWaitForCompletionAsync(
        string symbol,
        int unitsToBuy,
        decimal triggerPrice,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (unitsToBuy <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsToBuy), "Quantity should be greater than zero.");

        OrderResponse orderResponse = await Task.Run(() =>
            _kite.PlaceOrder(
                Exchange: Constants.Exchange.NSE,
                TradingSymbol: symbol,
                TransactionType: Constants.Transaction.Buy,
                Quantity: unitsToBuy,
                TriggerPrice: triggerPrice,
                Price: Math.Ceiling((triggerPrice * TradingConstants.Broker.LimitOrderPriceBufferMultiplier) / TradingConstants.Broker.LimitOrderTickSize) * TradingConstants.Broker.LimitOrderTickSize,
                Product: Constants.Product.CNC,
                OrderType: Constants.OrderType.Limit,
                Variety: Constants.Variety.Regular
            ), cancellationToken);

        _logger?.LogInformation("Placed limit buy order. Symbol={Symbol}, Quantity={Quantity}, TriggerPrice={TriggerPrice}, OrderId={OrderId}", symbol, unitsToBuy, triggerPrice, orderResponse.OrderId);

        Order completedOrder = await WaitForOrderCompletionAsync(
            orderResponse.OrderId,
            timeout,
            pollInterval,
            cancellationToken);

        return MapCompletedOrder(completedOrder);
    }

    public async Task<string> PlaceSingleSellGttAfterCompletionAsync(
        string buyOrderId,
        decimal sellLimitPrice,
        decimal? triggerPrice = null,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(buyOrderId))
            throw new ArgumentException("OrderId is required.", nameof(buyOrderId));

        if (sellLimitPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(sellLimitPrice), "Sell price should be greater than zero.");

        Order completedOrder = await WaitForOrderCompletionAsync(
            buyOrderId,
            timeout,
            pollInterval,
            cancellationToken);

        decimal completedQuantity = completedOrder.FilledQuantity;
        if (completedQuantity <= 0)
            throw new InvalidOperationException($"Completed order '{buyOrderId}' has zero filled quantity.");

        decimal effectiveTriggerPrice = triggerPrice.GetValueOrDefault(sellLimitPrice);

        var gttParams = new GTTParams
        {
            Exchange = completedOrder.Exchange,
            TradingSymbol = completedOrder.Tradingsymbol,
            InstrumentToken = completedOrder.InstrumentToken,
            LastPrice = completedOrder.AveragePrice > 0 ? completedOrder.AveragePrice : sellLimitPrice,
            TriggerType = Constants.GTT.Trigger.Single,
            TriggerPrices = new List<decimal> { effectiveTriggerPrice },
            Orders = new List<GTTOrderParams>
            {
                new()
                {
                    TransactionType = Constants.Transaction.Sell,
                    Quantity = completedQuantity,
                    Product = string.IsNullOrWhiteSpace(completedOrder.Product)
                        ? Constants.Product.CNC
                        : completedOrder.Product,
                    OrderType = Constants.OrderType.Limit,
                    Price = sellLimitPrice
                }
            }
        };

        Dictionary<string, object> response = await Task.Run(
            () => _kite.PlaceGTT(gttParams),
            cancellationToken);

        if (!TryExtractTriggerId(response, out string triggerId))
        {
            throw new InvalidOperationException("GTT order placed, but trigger_id was not returned in the response.");
        }

        _logger?.LogInformation("Placed single sell GTT. BuyOrderId={BuyOrderId}, TriggerId={TriggerId}, SellLimitPrice={SellLimitPrice}", buyOrderId, triggerId, sellLimitPrice);

        return triggerId;
    }

    private async Task<Order> WaitForOrderCompletionAsync(
        string orderId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));

        TimeSpan effectiveTimeout = timeout ?? DefaultOrderCompletionTimeout;
        TimeSpan effectivePollInterval = pollInterval ?? DefaultOrderStatusPollInterval;
        DateTime deadlineUtc = DateTime.UtcNow.Add(effectiveTimeout);

        Order? latestOrder = null;

        while (DateTime.UtcNow <= deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Order> orderHistory = await Task.Run(() => _kite.GetOrderHistory(orderId), cancellationToken);

            if (orderHistory.Count > 0)
            {
                Order currentOrder = orderHistory
                    .OrderByDescending(x => x.ExchangeTimestamp ?? x.OrderTimestamp ?? DateTime.MinValue)
                    .First();

                latestOrder = currentOrder;

                _logger?.LogDebug("Order status poll. OrderId={OrderId}, Status={Status}, FilledQuantity={FilledQuantity}, AveragePrice={AveragePrice}", orderId, currentOrder.Status, currentOrder.FilledQuantity, currentOrder.AveragePrice);

                if (string.Equals(currentOrder.Status, Constants.OrderStatus.Complete, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogInformation("Order completed. OrderId={OrderId}, FilledQuantity={FilledQuantity}, AveragePrice={AveragePrice}", orderId, currentOrder.FilledQuantity, currentOrder.AveragePrice);
                    return currentOrder;
                }

                if (string.Equals(currentOrder.Status, Constants.OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentOrder.Status, Constants.OrderStatus.Rejected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Order '{orderId}' ended with status '{currentOrder.Status}'. Message: {currentOrder.StatusMessage}");
                }
            }

            await Task.Delay(effectivePollInterval, cancellationToken);
        }

        string latestStatus = latestOrder?.Status ?? "UNKNOWN";
        _logger?.LogWarning("Order completion timeout. OrderId={OrderId}, TimeoutSeconds={TimeoutSeconds}, LatestStatus={LatestStatus}", orderId, effectiveTimeout.TotalSeconds, latestStatus);
        throw new TimeoutException(
            $"Order '{orderId}' was not completed within {effectiveTimeout.TotalSeconds:N0} seconds. Latest status: {latestStatus}.");
    }

    private static CompletedOrderExecution MapCompletedOrder(Order completedOrder)
    {
        return new CompletedOrderExecution(
            completedOrder.OrderId,
            completedOrder.Tradingsymbol,
            completedOrder.AveragePrice,
            completedOrder.FilledQuantity,
            completedOrder.Product);
    }

    private static bool TryExtractTriggerId(IDictionary<string, object> response, out string triggerId)
    {
        triggerId = string.Empty;

        if (TryExtractTriggerIdValue(response, out triggerId))
            return true;

        if (response.TryGetValue("data", out object? dataObject)
            && dataObject is IDictionary<string, object> dataDictionary
            && TryExtractTriggerIdValue(dataDictionary, out triggerId))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractTriggerIdValue(IDictionary<string, object> dictionary, out string triggerId)
    {
        triggerId = string.Empty;

        if (!dictionary.TryGetValue("trigger_id", out object? triggerIdValue) || triggerIdValue is null)
            return false;

        triggerId = Convert.ToString(triggerIdValue, CultureInfo.InvariantCulture) ?? string.Empty;

        return !string.IsNullOrWhiteSpace(triggerId);
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