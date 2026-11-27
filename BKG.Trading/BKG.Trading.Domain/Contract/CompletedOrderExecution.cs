namespace BKG.Trading.Domain.Contract;

public sealed record CompletedOrderExecution(
    string OrderId,
    string TradingSymbol,
    decimal AveragePrice,
    decimal FilledQuantity,
    string Product);