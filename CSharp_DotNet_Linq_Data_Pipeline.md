# From LINQ to an asynchronous data pipeline

You need to export active products and attach a price to each one.

With ten products, a list and a loop may be enough. With a paginated feed, the next products might need another network request. The price service may allow only a few active calls, and the user may cancel halfway through.

We will build up to that scenario, one decision at a time:

1. Choose how to read the products.
2. Filter and transform only what you need.
3. Decide whether to store the results or process them incrementally.
4. Limit downstream work and measure the result.

**LINQ** means Language Integrated Query: C# features and APIs for querying data. A **pipeline** is a sequence of processing steps, such as read, filter, enrich, and save. **Enrich** means add information, such as a product's price.

Each answer starts simply, includes a practical example, and ends with an interview-ready explanation. **Going deeper** sections are optional on a first read.

This continues [Async .NET: concurrency without confusion](./CSharp_DotNet_Async_Concurrency.md) and covers questions 039-050 in the [calendar](./LinkedIn_Content_Plan.md).

## Reading map

| Post | Questions | Focus |
|---|---|---|
| [Part 1/4: Sequences and execution](./CSharp_DotNet_Linq_Data_Pipeline_Part_1_of_4_Sequences_And_Execution.md) | 039-041 | Where and when the query runs |
| [Part 2/4: Choosing operators](./CSharp_DotNet_Linq_Data_Pipeline_Part_2_of_4_Choosing_Operators.md) | 042-044 | Expressing shape, existence, and expected matches |
| [Part 3/4: Memory, streaming, and cancellation](./CSharp_DotNet_Linq_Data_Pipeline_Part_3_of_4_Memory_Streaming_And_Cancellation.md) | 045-047 | Storing results or processing them as they arrive |
| [Part 4/4: Channels, backpressure, and measurement](./CSharp_DotNet_Linq_Data_Pipeline_Part_4_of_4_Channels_Backpressure_And_Measurement.md) | 048-050 | Keeping work bounded and testing assumptions |

## 039. How do IEnumerable, IQueryable, and IAsyncEnumerable differ?

**The simple answer.** They describe different ways of accessing a sequence of items.

| Type | Plain-language meaning | Common use |
|---|---|---|
| `IEnumerable<T>` | Read items synchronously with foreach | A list or an iterator producing items on demand |
| `IQueryable<T>` | Describe a query for a provider to interpret | Let an Entity Framework Core provider translate supported operations into SQL |
| `IAsyncEnumerable<T>` | Await the next item with await foreach | Read products while new pages arrive asynchronously |

`T` is the item type, such as Product. **SQL** is the language used to query relational databases.

**Example.** Filtering a local list usually runs C# code in your application. Filtering an EF Core query can run in the database, before rows are transferred. Reading a web feed might await a page, then produce its products.

**Interview answer.** "IEnumerable is synchronous enumeration, IQueryable describes provider-interpreted queries, and IAsyncEnumerable supports asynchronous enumeration. I check the source rather than assuming these names guarantee storage or performance."

### Going deeper: an interface is not a storage guarantee

- An enumerable may run code when read; it need not already be in memory.
- IQueryable is not inherently a database query. The provider determines execution, translation, and supported operations.
- `AsEnumerable()` changes subsequent operators to Enumerable operators; it does not fetch everything immediately. For a database source, later filters may then run in the application instead of the database.
- `AsQueryable()` on a list does not move the list into a database.
- Async enumeration is not automatic parallel processing. A normal await foreach asks for one next item at a time.
- These interfaces do not promise immutability, bounded memory, or successful cancellation.

## 040. What is deferred execution, and when does a LINQ query actually run?

**The simple answer.** Defining a query can describe work without doing it yet. Reading its results triggers that work.

**Example.** This is a complete console program:

```csharp
using System;
using System.Linq;

int[] prices = { 10, 25, 30 };
var affordable = prices.Where(price => price <= 25);
Console.WriteLine("Filter defined");

foreach (int price in affordable)
{
    Console.WriteLine(price);
}
```

Output:

```text
Filter defined
10
25
```

The Where filter runs as the foreach reads. It is not a separate list of answers created at the declaration.

**Interview answer.** "A deferred query runs when consumed. Foreach enumerates it, while operations such as ToList materialize the result immediately."

### Going deeper: deferred does not always mean streaming

Where and Select over ordinary in-memory sequences can process items incrementally. Enumerating an OrderBy result normally requires buffering and sorting the input before returning sorted items.

Terminal operations such as Count, Any, First, and Single request an answer immediately, though they need not all read the same amount of input. Providers can execute their own equivalent query plans.

Avoid side effects, such as saving records, inside predicates and projections. Deferred execution can repeat those effects or run them at an unexpected time.

## 041. How can multiple enumeration introduce unexpected work or inconsistent results?

**The simple answer.** Reading a deferred query twice may do the work twice.

**Example.** This usage fragment assumes an enumerable of products with an IsActive property:

```csharp
var active = products.Where(product => product.IsActive);
int count = active.Count();
foreach (var product in active)
{
    Console.WriteLine(product.Id);
}
```

For an ordinary iterator, the filter is visited once for Count and again for the loop. A remote query can issue two database commands. If data changes between commands, the count and exported rows can differ.

If you deliberately need reusable results and they fit in memory, call ToList once and reuse the list. If you only need one pass, keep it as one pass.

**Interview answer.** "I watch for repeated enumeration because it can repeat expensive work and observe different data. I materialize only when that reuse and memory cost are appropriate."

### Going deeper: do not promise a universal second pass

Some sequences are single-use or depend on external state. A second enumeration might fail rather than simply repeat.

Materialization captures the items observed during that read; it does not create database transaction isolation, clone objects, or make them immutable. Strong database consistency requires an appropriate database policy too.

## 042. How do Select and SelectMany differ when transforming nested data?

**The simple answer.** Select produces one result per input. SelectMany combines the inner sequences into one sequence.

**Example.** This complete console program represents two orders, each containing product IDs:

```csharp
using System;
using System.Linq;

int[][] orders = { new[] { 10, 20 }, new[] { 30 } };
var groups = orders.Select(order => order);
var productIds = orders.SelectMany(order => order);

Console.WriteLine(groups.Count());
Console.WriteLine(string.Join(", ", productIds));
```

Output:

```text
2
10, 20, 30
```

Select kept two inner groups. SelectMany flattened them.

**Interview answer.** "I use Select for a projection, such as product to ID. I use SelectMany when each item contains another sequence and I want one combined sequence."

### Going deeper: shape is not uniqueness or concurrency

SelectMany does not remove duplicates. The same product ID can appear in several orders; deduplication is a separate choice.

For ordinary in-memory LINQ, `Select(async item => ...)` produces a sequence of tasks, not automatically awaited results or bounded work. Materializing those tasks and calling WhenAll may start too much work for a large feed. The bounded example below avoids that pattern.

## 043. When should you use Any rather than Count to test for existence?

**The simple answer.** Use Any when you need yes or no; use Count when you need the exact number.

**Example.** This fragment asks whether any product is active:

```csharp
bool hasActiveProducts = products.Any(product => product.IsActive);
```

For an ordinary iterator, Any can stop at the first match. Count with a predicate normally examines all items to calculate the total.

**Interview answer.** "Any expresses existence and can short-circuit. I use Count when the total itself matters, not as a roundabout existence check."

### Going deeper: do not turn guidance into a blanket speed claim

A list's Count property is already available without walking the list. Enumerable methods can also use collection-specific optimizations.

A database provider may translate Any into an existence query, but translation and query plans depend on the provider. Inspect generated SQL and measure the real query before making performance claims.

## 044. How do First, Single, and their OrDefault variants express different expectations?

**The simple answer.** Choose the method that states how many matches are valid.

| Method | No matches | One match | More than one match |
|---|---|---|---|
| First | Throws | Returns it | Returns the first |
| FirstOrDefault | Returns default | Returns it | Returns the first |
| Single | Throws | Returns it | Throws |
| SingleOrDefault | Returns default | Returns it | Throws |

**Example.** Looking up a supposedly unique product code? SingleOrDefault allows "not found" but exposes duplicates. FirstOrDefault would choose one, potentially hiding invalid data.

For a reference type, the default is usually null. For int, it is 0. Check absence explicitly; an OrDefault method does not prove that a match existed.

**Interview answer.** "First asks for one available match. Single asserts uniqueness. OrDefault permits absence, but SingleOrDefault still rejects duplicates."

### Going deeper: application checks do not replace database rules

Single must establish that another match does not exist. Do not select First just to avoid that work if duplicates violate your contract.

Use database uniqueness constraints for stored uniqueness. Also apply an explicit order when "first" has a business meaning: a relational result has no guaranteed order without an ordering clause.

## 045. How does materializing with ToList change execution timing and memory usage?

**The simple answer.** ToList reads the sequence now and keeps its results in a list.

**Example.** Materialize active products once if you need to show their count and then display the same captured members:

```csharp
var active = products.Where(product => product.IsActive).ToList();
Console.WriteLine(active.Count);
```

Later loops over active reuse that list. Memory grows with the retained results.

**Interview answer.** "ToList changes a deferred sequence into stored results. It avoids rerunning the source for later reads but costs memory for the list."

### Going deeper: a list is not a deep snapshot

ToList does not clone referenced objects or make them immutable. Adding a product to the original collection does not add it to the new list, but changing an object referenced by both can be visible through both.

For EF Core queries, `ToListAsync(cancellationToken)` supports asynchronous materialization through that provider; it still stores the full result. Filter and select needed columns before materializing where the provider supports it.

Do not treat ToList as an automatic fix for every multiple-enumeration warning. A very large feed might be better processed once.

## 046. How do iterator methods and yield return support streaming?

**The simple answer.** An iterator produces an item, pauses, and continues when the caller asks for another.

**Example.** This complete console program does not build a list of all IDs:

```csharp
using System;
using System.Collections.Generic;

foreach (int id in ProductIds())
{
    Console.WriteLine(id);
    break;
}

static IEnumerable<int> ProductIds()
{
    yield return 10;
    yield return 20;
}
```

Output is just 10. The consumer stopped before asking for 20.

**Streaming** means processing items incrementally as they become available. An async iterator can await a page request and yield the products from that page.

**Interview answer.** "Yield return supports on-demand production instead of requiring a complete result list. I still check what the source and later operators buffer."

### Going deeper: memory and errors still have a location

Iterator bodies run during enumeration. Validation or I/O inside them can therefore fail when the consumer asks for an item, not when it calls the iterator method.

Streaming does not mean zero buffering. Our feed holds the current page; a database provider may buffer internally; OrderBy or ToList can add more buffering.

When an iterator owns a resource, using, await using, or finally can release it when enumeration ends or the enumerator is disposed.

## 047. How should cancellation and disposal work when consuming an asynchronous stream?

**The simple answer.** Pass cancellation to the actual work, and let structured iteration clean up its enumerator.

**Example.** This usage fragment assumes a ProductPipeline and a caller-provided token:

```csharp
await foreach (var product in pipeline.ReadActiveAsync().WithCancellation(cancellationToken))
{
    Console.WriteLine(product.Id);
}
```

WithCancellation supplies the enumeration token. The async iterator below uses `[EnumeratorCancellation]` on its token parameter, then forwards it into GetPageAsync.

Await foreach disposes its enumerator asynchronously when the loop completes, breaks, or throws. Resource-owning iterator code must actually implement cleanup. If you acquire an enumerator manually, use await using or an equivalent finally that awaits DisposeAsync.

**Interview answer.** "I propagate cancellation through the stream to I/O, and I use await foreach so early exits dispose the enumerator. Cancellation is cooperative, not rollback."

### Going deeper: lifetime is part of the contract

An async iterator can receive a token at method call time and another at enumeration time. EnumeratorCancellation lets generated iterator code select or combine them as appropriate.

Passing a token does not interrupt a producer that ignores it. The producer's underlying operations must participate.

Dispose resources the iterator owns, not borrowed clients. A production HTTP feed must dispose its owned responses correctly. Streaming an EF Core query can keep its database context and connection busy during consumption; avoid concurrent operations on that same context.

The runnable checks verify cleanup on early break, consumer failure, and cancellation. The paginated implementation itself owns no long-lived network response: that would be the adapter's responsibility.

## A small pipeline: read, filter, enrich, save

Start with the direct design before adding a queue:

```text
One page of products
        |
        v
Keep active products
        |
        v
At most N price-and-save operations
        |
        v
Finish, fail, or cancel
```

The **feed** supplies pages of up to 100 products. Pagination starts at page 1; each next-page number must increase, and null marks the end. These are sample contracts, not assumptions about every web API.

Here is the async iterator from [ProductPipeline.cs](./examples/LinqDataPipeline/ProductPipeline.cs):

```csharp
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
```

`int?` permits a page number or null. The pattern `nextPage is int pageNumber` reads a non-null number into pageNumber.

Filtering within the loop keeps this example focused on iterator behavior without requiring async LINQ operators. Ordinary Enumerable operators such as Where are not interchangeable with asynchronous sequence operators.

The same component bounds the full price-and-save operation:

```csharp
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
```

`Func<PricedProduct, CancellationToken, Task>` means the caller supplies an asynchronous save function taking a priced product and token. It may be invoked concurrently, so its dependencies must support that use. Do not share one EF Core DbContext across those invocations.

Important behavior:

- Input pages are read sequentially. The loop can process several yielded products concurrently.
- The maximum covers each complete price-and-save iteration, not just its network wait.
- The component does not keep every result. A save implementation that accumulates everything would reintroduce growing memory use.
- Completion order is not guaranteed. Preserving source order would need an explicit policy and possibly extra buffering.
- Feed, price, and save errors propagate. On failure, Parallel.ForEachAsync signals sibling cancellation and waits for active operations to finish; those operations must cooperate.
- Earlier saves are not rolled back. Retries need a policy for duplicate effects, not an assumption of all-or-nothing processing.
- This per-invocation limit is neither a global limit across servers nor requests per second.
- The page-size check happens after a page is returned. A real adapter also needs response-size limits; this check cannot prevent an oversized HTTP response from first being allocated.
- The sample requires a stable page contract and does not deduplicate products across pages. Real changing datasets may need cursor/snapshot pagination or idempotent downstream writes.

## 048. When is a Channel appropriate for a producer-consumer pipeline?

**The simple answer.** Use a Channel when one stage produces work and another consumes it, and they need an explicit queue between them.

**Example.** A feed reader produces products while price workers consume them. A bounded Channel can let these stages progress independently without an unlimited queue.

The direct pipeline above does not need this extra stage. Start with it when asynchronous enumeration and a bounded processing loop are sufficient.

**Interview answer.** "A Channel connects producers and consumers with an in-memory queue. I add it for independent stages and explicit buffering, not just because the code is asynchronous."

### Going deeper: completion and failure need an owner

The stage owning writes must signal completion when no more items will arrive. It can complete the channel with an exception so readers observe the failure after buffered work is handled.

Consumer failure is different: a producer waiting to write into a full queue may otherwise wait forever. A coordinating owner should signal cancellation to the other stages and await their termination. Simply awaiting WhenAll is not enough if one stage fails and another remains blocked.

Pass tokens to WriteAsync, reads, and downstream work. Decide whether failure drains queued work or abandons it; do not silently treat either outcome as success.

A Channel is not durable storage. Pending items can be lost when the process stops. Work requiring persistence, cross-process delivery, or replay may need a durable queue and idempotent processing.

## 049. How can backpressure prevent a fast producer from overwhelming downstream work?

**The simple answer.** Backpressure slows the producer when the consumer cannot keep up.

**Example.** A queue holds 20 waiting products. When it fills, a waiting-mode WriteAsync pauses production until a consumer frees space. It does not keep a thread blocked during that wait.

This complete console program demonstrates the same behavior with capacity 1:

```csharp
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
{
    FullMode = BoundedChannelFullMode.Wait
});
await channel.Writer.WriteAsync(10);
Task pending = channel.Writer.WriteAsync(20).AsTask();

Console.WriteLine(pending.IsCompleted);
Console.WriteLine(await channel.Reader.ReadAsync());
await pending;
channel.Writer.Complete();

await foreach (int value in channel.Reader.ReadAllAsync())
{
    Console.WriteLine(value);
}
```

Output:

```text
False
10
20
```

The second write cannot complete until the first item is removed. This small mechanics demonstration is not a complete supervised producer-consumer service.

**Interview answer.** "I bound the buffer and await writes when it is full. I also limit active consumers, because queue capacity alone does not bound total work."

### Going deeper: capacity is not the whole memory budget

Channel capacity counts queued items, not bytes, active consumers, pending writes, or producer-side pages. Producers must await writes rather than launching an unlimited number of pending WriteAsync calls.

Wait preserves queued work while slowing the producer. Drop modes deliberately discard items; they may fit replaceable updates but not an order-processing policy that requires every item.

Pull-based async enumeration also provides demand control: the source advances as its consumer asks for another item. In the direct example, workers request more products as processing capacity becomes available. The current page still occupies memory, and filtering may read several pages to find an active item.

## 050. How would you measure allocations and throughput before optimizing a data pipeline?

**The simple answer.** Define the workload, collect a baseline, change one thing, and repeat the same test.

**Example.** For a product export, measure:

- **Throughput:** successfully processed products per second.
- **Latency:** total duration and time to the first saved product.
- **Allocations:** newly allocated memory during processing.
- Retained memory, garbage collections, processor use, queue growth, and errors.

Keep product count, page size, active-product ratio, service behavior, concurrency, and save behavior comparable. Warm up before timing and run repeated trials.

**Interview answer.** "I measure end-to-end work with a representative workload, distinguish allocation from retained memory, and compare the same conditions before and after a change."

### Going deeper: async measurements can cross threads

`GC.GetAllocatedBytesForCurrentThread()` measures one thread, so reading it around an await can miss work or compare different threads.

The sample's optional measurement mode uses Stopwatch and a delta from `GC.GetTotalAllocatedBytes(precise: true)`. That process-wide delta can include runtime and other background allocations. It is not live memory, retained memory, or a method-specific allocation total.

The exercise uses immediate in-memory providers, a no-retention save callback, warm-up, and 20 measured exports of 1,000 products each. Feed construction, task creation, and measurement bookkeeping contribute to the interval. This is a way to practice measurement, not an HTTP/database benchmark or proof of production capacity.

Use dotnet-counters for runtime trends and profiling/benchmark tools when you need attribution and controlled comparisons. For production design, include representative remote latency, payload sizes, cancellation, failures, and retry behavior. Avoid selecting an impressive synthetic number and calling it service capacity.

## Optional: run the examples

Use the **.NET 10 SDK**, which supplies the compiler and command-line tools. From the repository folder:

```powershell
dotnet run --project .\examples\LinqDataPipeline\LinqDataPipeline.csproj --configuration Release
```

The sample has no external packages or real HTTP/database calls:

- [ProductPipeline.cs](./examples/LinqDataPipeline/ProductPipeline.cs): page contracts, async iterator, and bounded processing.
- [Program.cs](./examples/LinqDataPipeline/Program.cs): executable checks and the measurement exercise.
- [Project settings](./examples/LinqDataPipeline/LinqDataPipeline.csproj): .NET 10, nullable analysis, and warnings treated as errors.

To try the separate measurement exercise:

```powershell
dotnet run --project .\examples\LinqDataPipeline\LinqDataPipeline.csproj --configuration Release -- --measure
```

It prints processed count, elapsed time, products per second, and process-wide allocated bytes. Values vary by machine and run. There is deliberately no claimed performance threshold.

## Executable checks and limitations

The self-checking console program prints PASS for each successful check; a failed check throws. Each check has a ten-second safety timeout, not a performance target.

| Area | What the checks establish |
|---|---|
| Deferred execution | Construction does no filtering; two enumerations re-execute and can see changed data |
| Operator choices | SelectMany flattens; Any stops at a match; First and Single differ on absence/duplicates |
| Materialization | A list captures membership but still references the same mutable objects |
| Iterators | Yield avoids producing the next item after break; cleanup runs |
| Async iteration | Break, consumer failure, and cancellation dispose the iterator |
| Pagination | Pages advance in order; inactive products are skipped; early exit avoids fetching another page |
| Bounded processing | Exactly three gated calls are active; no next page is fetched while those workers are occupied |
| Cancellation | Pre-cancellation prevents calls; page and price operations observe in-flight cancellation |
| Failure | Feed, price, and save errors propagate; a price failure cancels and drains other active calls |
| Boundaries | Invalid settings/pages fail; 100 products fit and 101 are rejected |
| Channels | A full waiting-mode channel pauses a write; completion preserves buffered items; producer failure reaches the reader |

Validation: all 22 checks passed locally in Release configuration on Windows with .NET 10. The measurement exercise processed its expected 20,000 products.

Tests coordinate through manually completed tasks, not sleep-based timing assumptions. No actual database provider, SQL translation, network adapter, remote rate limit, global sort benchmark, durable queue, or production load test is included. Passing these checks does not prove those external behaviors.

## Interview recap

| Question | Practical answer | Watch out for |
|---|---|---|
| How do IEnumerable, IQueryable, and IAsyncEnumerable differ? | Synchronous enumeration, provider-interpreted query, or asynchronous enumeration | None inherently guarantees storage, parallelism, or bounded memory |
| What is deferred execution, and when does a LINQ query actually run? | Work is delayed until consumption | Deferred operators can still buffer |
| How can multiple enumeration introduce unexpected work or inconsistent results? | It can rerun work and observe changed data | A sequence is not always reusable |
| How do Select and SelectMany differ when transforming nested data? | Transform one item or flatten its inner sequence | Flattening does not deduplicate |
| When should you use Any rather than Count to test for existence? | Use Any for existence and Count for totals | Collection/provider optimizations vary |
| How do First, Single, and their OrDefault variants express different expectations? | First selects; Single asserts one match; OrDefault permits none | SingleOrDefault still rejects duplicates |
| How does materializing with ToList change execution timing and memory usage? | Read now and keep the result list | Not a deep copy or immutable snapshot |
| How do iterator methods and yield return support streaming? | Produce items on demand | Sources and later operators may buffer |
| How should cancellation and disposal work when consuming an asynchronous stream? | Pass tokens to actual work and dispose the enumerator | Cancellation is cooperative and not rollback |
| When is a Channel appropriate for a producer-consumer pipeline? | Independent stages need an explicit in-memory queue | Completion, failure, and durability need a policy |
| How can backpressure prevent a fast producer from overwhelming downstream work? | Slow production when bounded capacity is exhausted | Bound active work and producer buffers too |
| How would you measure allocations and throughput before optimizing a data pipeline? | Compare representative work under the same conditions | Synthetic throughput and allocated bytes are not production capacity or retained memory |

## References

- [Microsoft Learn: Introduction to LINQ queries](https://learn.microsoft.com/en-us/dotnet/csharp/linq/get-started/introduction-to-linq-queries)
- [Microsoft Learn: Queryable](https://learn.microsoft.com/en-us/dotnet/api/system.linq.queryable)
- [Microsoft Learn: Deferred execution and lazy evaluation](https://learn.microsoft.com/en-us/dotnet/standard/linq/deferred-execution-lazy-evaluation)
- [Microsoft Learn: SelectMany](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.selectmany)
- [Microsoft Learn: Any](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.any)
- [Microsoft Learn: SingleOrDefault](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.singleordefault)
- [Microsoft Learn: ToList](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.tolist)
- [Microsoft Learn: yield](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield)
- [Microsoft Learn: Generate and consume async streams](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)
- [Microsoft Learn: EF Core efficient querying, buffering and streaming](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying#buffering-and-streaming)
- [Microsoft Learn: Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Microsoft Learn: Parallel.ForEachAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.foreachasync)
- [Microsoft Learn: GC.GetTotalAllocatedBytes](https://learn.microsoft.com/en-us/dotnet/api/system.gc.gettotalallocatedbytes)
- [Microsoft Learn: dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
