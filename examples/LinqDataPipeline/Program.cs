using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LinqDataPipeline;

if (args.Length != 0)
{
    if (args is not ["--measure"])
    {
        throw new ArgumentException("Use no arguments for checks, or --measure for a measurement exercise.");
    }
    await Measure();
    return;
}

var checks = new (string Name, Func<Task> Run)[]
{
    ("A deferred query re-executes on each enumeration", DeferredQuery),
    ("Select preserves nesting and SelectMany flattens it", NestedData),
    ("Any stops after finding a match in an iterator", Existence),
    ("First and Single express different expectations", Cardinality),
    ("ToList captures membership without deep-copying objects", Materialization),
    ("Yield produces on demand and cleans up on break", IteratorCleanup),
    ("Pages are fetched in order and inactive products are skipped", PagedFiltering),
    ("Breaking early does not fetch the next page", EarlyExit),
    ("Async iterator cleanup runs on break, failure, and cancellation", AsyncCleanup),
    ("Pre-cancellation makes no feed or price calls", PreCancellation),
    ("Cancellation reaches an in-flight page request", PageCancellation),
    ("Three active calls bound enrichment and stop feed advancement", BoundedProcessing),
    ("An inactive feed makes no price calls", InactiveFeed),
    ("In-flight cancellation drains every active price call", ActiveCancellation),
    ("Price failure cancels and drains other calls", PriceFailure),
    ("Save failure is propagated", SaveFailure),
    ("Feed failure is propagated", FeedFailure),
    ("Invalid settings make no feed calls", InvalidSettings),
    ("Invalid page contracts are rejected", InvalidPages),
    ("Exactly 100 products fit in one page", ExactPageLimit),
    ("A full channel waits for space instead of dropping data", ChannelBackpressure),
    ("Channel completion carries a producer failure to its reader", ChannelFailure)
};

foreach (var check in checks)
{
    await check.Run().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS: {check.Name}");
}
Console.WriteLine($"All {checks.Length} checks passed.");

static Task DeferredQuery()
{
    var values = new List<int> { 1, 2, 3 };
    int visits = 0;
    IEnumerable<int> query = values.Where(value =>
    {
        visits++;
        return value > 1;
    });
    Require(visits == 0, "Query ran during construction.");
    Require(query.SequenceEqual(new[] { 2, 3 }) && visits == 3, "First pass differs.");
    values.Add(4);
    Require(query.SequenceEqual(new[] { 2, 3, 4 }) && visits == 7, "Second pass did not re-execute.");
    return Task.CompletedTask;
}

static Task NestedData()
{
    int[][] orders = [[10, 20], [30]];
    Require(orders.Select(order => order).Count() == 2, "Select changed the outer shape.");
    Require(orders.SelectMany(order => order).SequenceEqual(new[] { 10, 20, 30 }),
        "SelectMany did not flatten the orders.");
    return Task.CompletedTask;
}

static Task Existence()
{
    int visited = 0;
    Require(Items().Any(value => value > 0) && visited == 1, "Any scanned beyond its match.");
    return Task.CompletedTask;

    IEnumerable<int> Items()
    {
        visited++;
        yield return 1;
        visited++;
        yield return 2;
    }
}

static async Task Cardinality()
{
    int[] duplicates = [7, 7];
    Require(duplicates.First() == 7, "First result differs.");
    await ExpectThrows<InvalidOperationException>(() => Task.FromResult(duplicates.Single()));
    await ExpectThrows<InvalidOperationException>(() => Task.FromResult(duplicates.SingleOrDefault()));
    await ExpectThrows<InvalidOperationException>(() => Task.FromResult(Array.Empty<int>().First()));
    Require(Array.Empty<int>().FirstOrDefault() == 0 && Array.Empty<int>().SingleOrDefault() == 0,
        "Expected the default value for an empty sequence.");
}

static Task Materialization()
{
    var item = new MutableItem { Name = "Original" };
    var source = new List<MutableItem> { item };
    List<MutableItem> snapshot = source.ToList();
    source.Add(new MutableItem { Name = "New" });
    item.Name = "Changed";
    Require(snapshot.Count == 1 && snapshot[0].Name == "Changed",
        "ToList should capture membership, not clone objects.");
    return Task.CompletedTask;
}

static Task IteratorCleanup()
{
    int generated = 0;
    bool disposed = false;
    foreach (int value in Items())
    {
        Require(value == 1, "Unexpected first item.");
        break;
    }
    Require(generated == 1 && disposed, "Iterator read ahead or missed cleanup.");
    return Task.CompletedTask;

    IEnumerable<int> Items()
    {
        try
        {
            generated++;
            yield return 1;
            generated++;
            yield return 2;
        }
        finally
        {
            disposed = true;
        }
    }
}

static async Task PagedFiltering()
{
    var feed = new RecordingFeed(page => page switch
    {
        1 => new ProductPage([new(1, true), new(2, false)], 2),
        2 => new ProductPage([new(3, true)], null),
        _ => throw new InvalidOperationException("Unexpected page.")
    });
    var ids = new List<int>();
    await foreach (Product product in new ProductPipeline(feed, new ImmediatePrices()).ReadActiveAsync())
    {
        ids.Add(product.Id);
    }
    Require(ids.SequenceEqual(new[] { 1, 3 }) && feed.Pages.SequenceEqual(new[] { 1, 2 }),
        "Pagination or filtering differs.");
}

static async Task EarlyExit()
{
    var feed = new RecordingFeed(_ => new ProductPage([new(1, true)], 2));
    var pipeline = new ProductPipeline(feed, new ImmediatePrices());
    Require(feed.Pages.Count == 0, "Construction performed I/O.");
    await foreach (Product product in pipeline.ReadActiveAsync())
    {
        Require(product.Id == 1, "Wrong first product.");
        break;
    }
    Require(feed.Pages.SequenceEqual(new[] { 1 }), "Early exit fetched another page.");
}

static async Task AsyncCleanup()
{
    int cleanups = 0;
    await foreach (int _ in Items())
    {
        break;
    }
    await ExpectThrows<IOException>(async () =>
    {
        await foreach (int _ in Items())
        {
            throw new IOException("Consumer failed.");
        }
    });
    using var source = new CancellationTokenSource();
    await ExpectThrows<OperationCanceledException>(async () =>
    {
        await foreach (int _ in Items().WithCancellation(source.Token))
        {
            source.Cancel();
        }
    });
    Require(cleanups == 3, "Async iterator was not disposed on every exit.");

    async IAsyncEnumerable<int> Items([EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            yield return 1;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        finally
        {
            cleanups++;
        }
    }
}

static async Task PreCancellation()
{
    var feed = SinglePage();
    var prices = new ImmediatePrices();
    using var source = new CancellationTokenSource();
    source.Cancel();
    await ExpectThrows<OperationCanceledException>(() =>
        new ProductPipeline(feed, prices).ProcessAsync(3, Ignore, source.Token));
    Require(feed.Pages.Count == 0 && prices.Calls == 0, "Cancelled pipeline started work.");
}

static async Task PageCancellation()
{
    var feed = new WaitingFeed();
    using var source = new CancellationTokenSource();
    Task pending = new ProductPipeline(feed, new ImmediatePrices()).ProcessAsync(3, Ignore, source.Token);
    await feed.Started.Task;
    source.Cancel();
    await ExpectThrows<OperationCanceledException>(() => pending);
    Require(feed.Cancelled, "Page call did not observe cancellation.");
}

static async Task BoundedProcessing()
{
    var feed = SinglePage(3, 2);
    var prices = new GatedPrices(3);
    var results = new System.Collections.Concurrent.ConcurrentBag<PricedProduct>();
    Task pending = new ProductPipeline(feed, prices).ProcessAsync(3, (product, _) =>
    {
        results.Add(product);
        return Task.CompletedTask;
    });
    await prices.Started.Task;
    Require(prices.Active == 3 && !pending.IsCompleted && feed.Pages.Count == 1,
        "Processing exceeded its bound or advanced the feed while all workers were busy.");
    prices.Release.SetResult(true);
    await pending;
    Require(prices.Peak == 3 && prices.Active == 0 && results.Count == 6,
        "Calls were lost or exceeded the bound.");
    Require(results.OrderBy(p => p.Id).SequenceEqual(
        Enumerable.Range(1, 6).Select(id => new PricedProduct(id, id * 2m))), "Results differ.");
}

static async Task InactiveFeed()
{
    var feed = new RecordingFeed(_ => new ProductPage([new(1, false)], null));
    var prices = new ImmediatePrices();
    await new ProductPipeline(feed, prices).ProcessAsync(3, Ignore);
    Require(prices.Calls == 0, "Inactive product was enriched.");
}

static async Task ActiveCancellation()
{
    var prices = new GatedPrices(3);
    using var source = new CancellationTokenSource();
    Task pending = new ProductPipeline(SinglePage(), prices).ProcessAsync(3, Ignore, source.Token);
    await prices.Started.Task;
    source.Cancel();
    await ExpectThrows<OperationCanceledException>(() => pending);
    Require(prices.Active == 0 && prices.Cancelled == 3, "Active operations did not drain.");
}

static async Task PriceFailure()
{
    var prices = new GatedPrices(3, failFirst: true);
    Task pending = new ProductPipeline(SinglePage(), prices).ProcessAsync(3, Ignore);
    await prices.Started.Task;
    prices.Release.SetResult(true);
    await ExpectThrows<IOException>(() => pending);
    Require(prices.Active == 0 && prices.Cancelled == 2, "Failure left sibling calls running.");
}

static async Task SaveFailure()
{
    var expected = new IOException("Save failed.");
    var observed = await ExpectThrows<IOException>(() =>
        new ProductPipeline(SinglePage(), new ImmediatePrices()).ProcessAsync(
            1, (_, _) => Task.FromException(expected)));
    Require(ReferenceEquals(expected, observed), "Save failure was replaced.");
}

static async Task FeedFailure()
{
    var expected = new IOException("Feed failed.");
    var feed = new RecordingFeed(_ => throw expected);
    var observed = await ExpectThrows<IOException>(() =>
        new ProductPipeline(feed, new ImmediatePrices()).ProcessAsync(3, Ignore));
    Require(ReferenceEquals(expected, observed), "Feed failure was replaced.");
}

static async Task InvalidSettings()
{
    var feed = SinglePage();
    var pipeline = new ProductPipeline(feed, new ImmediatePrices());
    await ExpectThrows<ArgumentOutOfRangeException>(() => pipeline.ProcessAsync(0, Ignore));
    await ExpectThrows<ArgumentNullException>(() => pipeline.ProcessAsync(1, null!));
    Require(feed.Pages.Count == 0, "Invalid settings triggered I/O.");
}

static async Task InvalidPages()
{
    ProductPage[] invalid =
    [
        new([new(1, true)], 1),
        new([new(1, true)], 0),
        new([new(0, true)], null),
        new(Enumerable.Range(1, 101).Select(id => new Product(id, true)).ToArray(), null)
    ];
    foreach (ProductPage page in invalid)
    {
        var feed = new RecordingFeed(_ => page);
        await ExpectThrows<InvalidDataException>(() =>
            new ProductPipeline(feed, new ImmediatePrices()).ProcessAsync(1, Ignore));
    }
}

static async Task ExactPageLimit()
{
    var prices = new ImmediatePrices();
    await new ProductPipeline(SinglePage(100), prices).ProcessAsync(3, Ignore);
    Require(prices.Calls == 100, "Exact page boundary rejected valid products.");
}

static async Task ChannelBackpressure()
{
    var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
    await channel.Writer.WriteAsync(1);
    Task pendingWrite = channel.Writer.WriteAsync(2).AsTask();
    Require(!pendingWrite.IsCompleted && !channel.Writer.TryWrite(3), "Full channel did not wait.");
    Require(await channel.Reader.ReadAsync() == 1, "Wrong first item.");
    await pendingWrite;
    channel.Writer.Complete();
    var remaining = new List<int>();
    await foreach (int value in channel.Reader.ReadAllAsync())
    {
        remaining.Add(value);
    }
    Require(remaining.SequenceEqual(new[] { 2 }), "Buffered data was lost.");
}

static async Task ChannelFailure()
{
    var channel = Channel.CreateBounded<int>(1);
    var expected = new IOException("Producer failed.");
    channel.Writer.Complete(expected);
    var observed = await ExpectThrows<IOException>(async () =>
    {
        await foreach (int _ in channel.Reader.ReadAllAsync())
        {
            throw new InvalidOperationException("Failed empty channel yielded a value.");
        }
    });
    Require(ReferenceEquals(expected, observed), "Reader lost the producer failure.");
    await ExpectThrows<IOException>(async () => await channel.Reader.Completion);
}

static async Task Measure()
{
    var pipeline = new ProductPipeline(SinglePage(100, 10), new ImmediatePrices());
    for (int i = 0; i < 3; i++)
    {
        await pipeline.ProcessAsync(4, Ignore);
    }
    int processed = 0;
    long before = GC.GetTotalAllocatedBytes(precise: true);
    var watch = Stopwatch.StartNew();
    for (int i = 0; i < 20; i++)
    {
        await pipeline.ProcessAsync(4, (_, _) =>
        {
            Interlocked.Increment(ref processed);
            return Task.CompletedTask;
        });
    }
    watch.Stop();
    long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
    Console.WriteLine("Synthetic in-memory exercise; not an HTTP or database benchmark.");
    Console.WriteLine($"Processed: {processed}; elapsed: {watch.Elapsed.TotalMilliseconds:F2} ms");
    Console.WriteLine($"Throughput: {processed / watch.Elapsed.TotalSeconds:F0} products/second");
    Console.WriteLine($"Process-wide allocated bytes during interval: {allocated}");
}

static RecordingFeed SinglePage(int size = 3, int pages = 1) => new(page =>
    new ProductPage(
        Enumerable.Range((page - 1) * size + 1, size).Select(id => new Product(id, true)).ToArray(),
        page < pages ? page + 1 : null));

static Task Ignore(PricedProduct _, CancellationToken token)
{
    token.ThrowIfCancellationRequested();
    return Task.CompletedTask;
}

static async Task<TException> ExpectThrows<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class MutableItem
{
    public required string Name { get; set; }
}

sealed class RecordingFeed(Func<int, ProductPage> getPage) : IProductFeed
{
    public List<int> Pages { get; } = [];

    public Task<ProductPage> GetPageAsync(int pageNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Pages.Add(pageNumber);
        return Task.FromResult(getPage(pageNumber));
    }
}

sealed class ImmediatePrices : IPriceClient
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);

    public Task<decimal> GetPriceAsync(int productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref calls);
        return Task.FromResult(productId * 2m);
    }
}

sealed class WaitingFeed : IProductFeed
{
    public TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Cancelled { get; private set; }

    public async Task<ProductPage> GetPageAsync(int pageNumber, CancellationToken cancellationToken)
    {
        Started.SetResult(true);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Infinite delay unexpectedly completed.");
        }
        catch (OperationCanceledException)
        {
            Cancelled = true;
            throw;
        }
    }
}

sealed class GatedPrices(int target, bool failFirst = false) : IPriceClient
{
    private int active;
    private int peak;
    private int cancelled;
    public int Active => Volatile.Read(ref active);
    public int Peak => Volatile.Read(ref peak);
    public int Cancelled => Volatile.Read(ref cancelled);
    public TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<decimal> GetPriceAsync(int productId, CancellationToken cancellationToken)
    {
        int current = Interlocked.Increment(ref active);
        int observed = Volatile.Read(ref peak);
        while (current > observed)
        {
            int previous = Interlocked.CompareExchange(ref peak, current, observed);
            if (previous == observed)
            {
                break;
            }
            observed = previous;
        }
        if (current == target)
        {
            Started.TrySetResult(true);
        }
        try
        {
            if (failFirst)
            {
                if (productId == 1)
                {
                    await Release.Task.WaitAsync(cancellationToken);
                    throw new IOException("Price provider failed.");
                }
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Infinite delay unexpectedly completed.");
            }
            await Release.Task.WaitAsync(cancellationToken);
            return productId * 2m;
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref cancelled);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }
}
