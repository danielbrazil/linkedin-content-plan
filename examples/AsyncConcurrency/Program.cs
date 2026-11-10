using AsyncConcurrency;

var checks = new (string Name, Func<Task> Run)[]
{
    ("Async runs synchronously until an incomplete await", AsyncExecution),
    ("WhenAll waits for every task and retains both failures", WhenAllFailures),
    ("WhenAny returns a completed task without observing its failure", WhenAnyFailure),
    ("Linked cancellation reaches the operation", LinkedCancellation),
    ("An immediate ValueTask result can be consumed once", ImmediateValueTask),
    ("Interleaved updates can deterministically lose a change", LostUpdate),
    ("SemaphoreSlim protects an update across an await", ProtectedUpdate),
    ("Batch concurrency stays within four active calls and preserves order", BoundedBatch),
    ("Batch limit one allows only sequential calls", SequentialBatch),
    ("Empty batch returns no results and makes no calls", EmptyBatch),
    ("Pre-cancelled batch makes no calls", PreCancelledBatch),
    ("Cancellation reaches every active call", ActiveCancellation),
    ("Client failure is propagated, not returned as partial success", ClientFailure),
    ("A failing call cancels and drains its active siblings", SiblingFailure),
    ("Exactly 1,000 products are accepted", ExactBatchLimit),
    ("Invalid batch input is rejected before calling client", InvalidInput)
};

foreach (var check in checks)
{
    await check.Run().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS: {check.Name}");
}
Console.WriteLine($"All {checks.Length} checks passed.");

static async Task AsyncExecution()
{
    var events = new List<string>();
    var gate = Signal();
    Task pending = Work();
    Require(events.SequenceEqual(new[] { "before" }) && !pending.IsCompleted,
        "Expected synchronous work before suspension.");
    gate.SetResult(true);
    await pending;
    Require(events.SequenceEqual(new[] { "before", "after" }), "Continuation did not finish.");

    async Task Work()
    {
        events.Add("before");
        await gate.Task;
        events.Add("after");
    }
}

static async Task WhenAllFailures()
{
    var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<int[]> combined = Task.WhenAll(first.Task, second.Task);
    first.SetException(new IOException("First provider failed."));
    Require(!combined.IsCompleted, "WhenAll finished before every input completed.");
    second.SetException(new InvalidOperationException("Second provider failed."));
    await ExpectThrows<Exception>(async () => { await combined; });
    Require(combined.Exception?.InnerExceptions.Count == 2, "Combined failures were lost.");
}

static async Task WhenAnyFailure()
{
    var expected = new IOException("Provider failed.");
    var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    Task<int> failed = Task.FromException<int>(expected);
    Task<int> winner = await Task.WhenAny(failed, pending.Task);
    Require(ReferenceEquals(winner, failed) && !pending.Task.IsCompleted,
        "WhenAny did not preserve the pending operation.");
    var observed = await ExpectThrows<IOException>(async () => { await winner; });
    Require(ReferenceEquals(observed, expected), "Expected failure from the returned task.");
    pending.SetResult(2);
    await pending.Task;
}

static async Task LinkedCancellation()
{
    using var caller = new CancellationTokenSource();
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
    linked.CancelAfter(TimeSpan.FromSeconds(30));
    Task operation = Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
    caller.Cancel();
    await ExpectThrows<OperationCanceledException>(async () => { await operation; });
    Require(linked.IsCancellationRequested, "Caller signal did not propagate.");
}

static async Task ImmediateValueTask()
{
    ValueTask<decimal> lookup = ValueTask.FromResult(19.95m);
    Require(lookup.IsCompletedSuccessfully, "Expected immediate completion.");
    Require(await lookup == 19.95m, "Unexpected lookup result.");
}

static async Task LostUpdate()
{
    int stock = 10;
    int readers = 0;
    var bothRead = Signal();
    await Task.WhenAll(Sell(), Sell());
    Require(stock == 9, "Expected the deliberately unprotected update to lose one sale.");

    async Task Sell()
    {
        int snapshot = stock;
        if (Interlocked.Increment(ref readers) == 2)
        {
            bothRead.SetResult(true);
        }
        await bothRead.Task;
        Interlocked.Exchange(ref stock, snapshot - 1);
    }
}

static async Task ProtectedUpdate()
{
    int stock = 10;
    using var gate = new SemaphoreSlim(1, 1);
    await Task.WhenAll(Sell(), Sell());
    Require(stock == 8, "Protected update lost a sale.");
    Require(gate.CurrentCount == 1, "Semaphore permit was not returned.");

    async Task Sell()
    {
        await gate.WaitAsync();
        try
        {
            int snapshot = stock;
            await Task.Yield();
            stock = snapshot - 1;
        }
        finally
        {
            gate.Release();
        }
    }
}

static async Task BoundedBatch()
{
    var client = new GatedClient(4);
    Task<PriceQuote[]> pending = new PriceBatch(client).FetchAsync(
        Enumerable.Range(1, 100).ToArray(), 4);
    await client.TargetReached.Task;
    Require(!pending.IsCompleted && client.Active == 4, "Expected four blocked calls.");
    client.Release.SetResult(true);
    PriceQuote[] results = await pending;
    Require(client.Peak == 4 && client.Active == 0 && client.Calls == 100,
        "Concurrency limit or completion count failed.");
    Require(results.Select(q => q.ProductId).SequenceEqual(Enumerable.Range(1, 100)),
        "Results did not preserve input order.");
}

static async Task SequentialBatch()
{
    var client = new GatedClient(1);
    Task<PriceQuote[]> pending = new PriceBatch(client).FetchAsync(new[] { 1, 2, 3 }, 1);
    await client.TargetReached.Task;
    Require(client.Calls == 1, "Sequential batch started more than one call.");
    client.Release.SetResult(true);
    await pending;
    Require(client.Peak == 1 && client.Calls == 3 && client.Active == 0,
        "Sequential limit failed.");
}

static async Task EmptyBatch()
{
    var client = new GatedClient(1);
    PriceQuote[] results = await new PriceBatch(client).FetchAsync(Array.Empty<int>(), 4);
    Require(results.Length == 0 && client.Calls == 0, "Empty batch made a call.");
}

static async Task PreCancelledBatch()
{
    var client = new GatedClient(1);
    using var source = new CancellationTokenSource();
    source.Cancel();
    await ExpectThrows<OperationCanceledException>(() =>
        new PriceBatch(client).FetchAsync(new[] { 1 }, 4, source.Token));
    Require(client.Calls == 0, "Pre-cancelled batch called client.");
}

static async Task ActiveCancellation()
{
    var client = new GatedClient(4);
    using var source = new CancellationTokenSource();
    Task<PriceQuote[]> pending = new PriceBatch(client).FetchAsync(
        Enumerable.Range(1, 100).ToArray(), 4, source.Token);
    await client.TargetReached.Task;
    source.Cancel();
    await ExpectThrows<OperationCanceledException>(async () => { await pending; });
    Require(client.Calls == 4 && client.Cancelled == 4 && client.Active == 0,
        "Cancellation failed to drain all active calls.");
}

static async Task ClientFailure()
{
    var client = new FailingClient();
    await ExpectThrows<IOException>(() => new PriceBatch(client).FetchAsync(new[] { 1 }, 4));
}

static async Task SiblingFailure()
{
    var client = new FailingGroupClient();
    Task<PriceQuote[]> pending = new PriceBatch(client).FetchAsync(new[] { 1, 2, 3, 4 }, 4);
    await client.AllStarted.Task;
    client.Fail.SetResult(true);
    await ExpectThrows<IOException>(async () => { await pending; });
    Require(client.Active == 0 && client.Cancelled == 3,
        "Failure did not cancel and drain the other three calls.");
}

static async Task ExactBatchLimit()
{
    var client = new GatedClient(1);
    Task<PriceQuote[]> pending = new PriceBatch(client).FetchAsync(
        Enumerable.Range(1, 1000).ToArray(), 4);
    await client.TargetReached.Task;
    client.Release.SetResult(true);
    PriceQuote[] results = await pending;
    Require(results.Length == 1000 && client.Calls == 1000 && client.Peak <= 4,
        "Exact batch-size boundary failed.");
}

static async Task InvalidInput()
{
    var client = new GatedClient(1);
    var batch = new PriceBatch(client);
    await ExpectThrows<ArgumentOutOfRangeException>(() => batch.FetchAsync(new[] { 1 }, 0));
    await ExpectThrows<ArgumentException>(() => batch.FetchAsync(new[] { 0 }, 4));
    await ExpectThrows<ArgumentException>(() =>
        batch.FetchAsync(Enumerable.Range(1, 1001).ToArray(), 4));
    Require(client.Calls == 0, "Invalid input called client.");
}

static TaskCompletionSource<bool> Signal() =>
    new(TaskCreationOptions.RunContinuationsAsynchronously);

static async Task<TException> ExpectThrows<TException>(Func<Task> action)
    where TException : Exception
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

sealed class FailingClient : IPriceClient
{
    public Task<PriceQuote> GetAsync(int productId, CancellationToken cancellationToken) =>
        Task.FromException<PriceQuote>(new IOException("Price provider unavailable."));
}

sealed class FailingGroupClient : IPriceClient
{
    private int active;
    private int cancelled;
    public int Active => Volatile.Read(ref active);
    public int Cancelled => Volatile.Read(ref cancelled);
    public TaskCompletionSource<bool> AllStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Fail { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<PriceQuote> GetAsync(int productId, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref active) == 4)
        {
            AllStarted.TrySetResult(true);
        }
        try
        {
            if (productId == 1)
            {
                await Fail.Task.WaitAsync(cancellationToken);
                throw new IOException("Price provider unavailable.");
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Infinite delay unexpectedly completed.");
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

sealed class GatedClient(int target) : IPriceClient
{
    private int active;
    private int peak;
    private int calls;
    private int cancelled;

    public TaskCompletionSource<bool> TargetReached { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Active => Volatile.Read(ref active);
    public int Peak => Volatile.Read(ref peak);
    public int Calls => Volatile.Read(ref calls);
    public int Cancelled => Volatile.Read(ref cancelled);

    public async Task<PriceQuote> GetAsync(int productId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
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
            TargetReached.TrySetResult(true);
        }

        try
        {
            await Release.Task.WaitAsync(cancellationToken);
            return new PriceQuote(productId, productId * 1.25m);
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
