namespace BKG.Trading.Domain.Contract
{
    public interface IScreener
    {
        Task<IDictionary<string, decimal>> GetSwingStockListAsync();
    }
}
