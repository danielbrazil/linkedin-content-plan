namespace AsyncConcurrency;

public sealed record PriceQuote(int ProductId, decimal Price);

public interface IPriceClient
{
    Task<PriceQuote> GetAsync(int productId, CancellationToken cancellationToken);
}

public sealed class PriceBatch(IPriceClient client)
{
    private readonly IPriceClient client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<PriceQuote[]> FetchAsync(
        IReadOnlyList<int> productIds,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        if (productIds.Count > 1000)
        {
            throw new ArgumentException("A batch cannot exceed 1,000 products.", nameof(productIds));
        }
        if (productIds.Any(id => id <= 0))
        {
            throw new ArgumentException("Product IDs must be positive.", nameof(productIds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        int[] ids = productIds.ToArray();
        var quotes = new PriceQuote[ids.Length];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ids.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                quotes[index] = await client.GetAsync(ids[index], token);
            });

        return quotes;
    }
}
