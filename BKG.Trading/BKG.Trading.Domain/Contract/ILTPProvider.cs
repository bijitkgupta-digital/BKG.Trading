namespace BKG.Trading.Domain.Contract
{
    public interface ILTPProvider
    {
        Task<decimal> FetchLTPAsync(string symbol);
    }
}