using System.Runtime.CompilerServices;

namespace LinqDataPipeline;

public sealed record Product(int Id, bool IsActive);
public sealed record ProductPage(IReadOnlyList<Product> Items, int? NextPage);
public sealed record PricedProduct(int Id, decimal Price);

public interface IProductFeed
{
    Task<ProductPage> GetPageAsync(int pageNumber, CancellationToken cancellationToken);
}

public interface IPriceClient
{
    Task<decimal> GetPriceAsync(int productId, CancellationToken cancellationToken);
}

public sealed class ProductPipeline(IProductFeed feed, IPriceClient prices)
{
    private readonly IProductFeed feed = feed ?? throw new ArgumentNullException(nameof(feed));
    private readonly IPriceClient prices = prices ?? throw new ArgumentNullException(nameof(prices));

    public async IAsyncEnumerable<Product> ReadActiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int? nextPage = 1;
        while (nextPage is int pageNumber)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProductPage page = await feed.GetPageAsync(pageNumber, cancellationToken);
            if (page.Items.Count > 100)
            {
                throw new InvalidDataException("A feed page cannot exceed 100 products.");
            }
            if (page.NextPage is int next && next <= pageNumber)
            {
                throw new InvalidDataException("The next page number must increase.");
            }
            foreach (Product product in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (product.Id <= 0)
                {
                    throw new InvalidDataException("Product IDs must be positive.");
                }
                if (product.IsActive)
                {
                    yield return product;
                }
            }
            nextPage = page.NextPage;
        }
    }

    public async Task ProcessAsync(
        int maxConcurrency,
        Func<PricedProduct, CancellationToken, Task> saveAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        ArgumentNullException.ThrowIfNull(saveAsync);

        await Parallel.ForEachAsync(
            ReadActiveAsync(cancellationToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            },
            async (product, token) =>
            {
                decimal price = await prices.GetPriceAsync(product.Id, token);
                await saveAsync(new PricedProduct(product.Id, price), token);
            });
    }
}
