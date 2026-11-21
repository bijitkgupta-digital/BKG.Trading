namespace BKG.Trading.Domain.Contract
{
    public interface ILTPProvider
    {
        Task<decimal> FetchLTPAsync(string symbol);
        Task<IDictionary<string, decimal>> FetchLTPAsync(IEnumerable<string> symbols);
    }
}