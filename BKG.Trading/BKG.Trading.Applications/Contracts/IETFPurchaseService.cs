namespace BKG.Trading.Applications.Contracts
{
    public interface IETFPurchaseService
    {
        Task<ETFPurchaseResult> ExecuteAsync();
    }
}
