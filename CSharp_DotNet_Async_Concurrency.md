# Async .NET: concurrency without confusion

Imagine an order screen with a "Get price" button. The price service takes two seconds to answer. You want the screen to remain responsive, and the user may click Cancel before the answer arrives.

Start with that familiar problem. Then add two more: fetching several prices without overwhelming the service, and avoiding incorrect shared updates.

This article covers questions 026-038 in the [calendar](./LinkedIn_Content_Plan.md). Each answer begins with a simple explanation and practical example, followed by a short interview answer. **Going deeper** sections are optional on a first read: they contain the extra detail useful for follow-up questions.

## Start here: waiting is not the same as working

- **A thread** executes program instructions. Think of it as a worker.
- **Blocking** keeps that worker occupied waiting, unable to continue other work.
- **Asynchronous waiting** lets an incomplete operation remain pending without keeping the waiting thread blocked.
- **A Task** represents an operation's outcome. It is not the worker itself.

For genuinely asynchronous network or database operations, awaiting completion can avoid blocking a thread while the result is pending. The work still takes time; the difference is how the application waits.

The worker analogy is only a starting point. Not every task owns a thread, and not every operation named Async is implemented without blocking.

## Reading map

| Post | Questions | Focus |
|---|---|---|
| [Part 1/4: Execution and workloads](./CSharp_DotNet_Async_Concurrency_Part_1_of_4_Execution_And_Workloads.md) | 026-028 | Awaiting is not the same as starting a thread |
| [Part 2/4: Blocking, context, and coordination](./CSharp_DotNet_Async_Concurrency_Part_2_of_4_Blocking_Context_And_Coordination.md) | 029-031 | Avoid freezing a screen and wait for several results |
| [Part 3/4: Cancellation and return types](./CSharp_DotNet_Async_Concurrency_Part_3_of_4_Cancellation_And_Return_Types.md) | 032-034 | Stop unwanted work and choose useful return types |
| [Part 4/4: Shared state and limits](./CSharp_DotNet_Async_Concurrency_Part_4_of_4_Shared_State_And_Limits.md) | 035-038 | Prevent lost updates and too many simultaneous calls |

## Optional: run the examples

You do not need to run anything to follow the explanations. Read the answers first if async is new to you, then return here when you want to experiment.

Use the **.NET 10 SDK**, the Software Development Kit containing the compiler and command-line tools. From the repository folder:

```powershell
dotnet run --project .\examples\AsyncConcurrency\AsyncConcurrency.csproj --configuration Release
```

- [PriceBatch.cs](./examples/AsyncConcurrency/PriceBatch.cs): the bounded lookup component and its contracts.
- [Program.cs](./examples/AsyncConcurrency/Program.cs): executable checks for async behavior, failures, cancellation, races, and limits.
- [Project settings](./examples/AsyncConcurrency/AsyncConcurrency.csproj): .NET 10 with nullable-reference analysis and warnings treated as errors.

There are no external packages or real HTTP requests. **HTTP** is the request/response protocol used to call web services. Tests use controlled substitutes for the price provider, so they check scheduling and coordination without depending on network timing.

This is a self-checking console program rather than a dedicated testing-framework project. A failed assertion throws an exception; a successful run prints a PASS line for each check. Every check has a ten-second safety timeout, not a performance target.

Validation: all 16 checks passed locally on Windows with .NET 10 in Release configuration, including four additional repeat runs. The batch-method excerpt was compared with the runnable implementation.

## 026. What do async and await change about a method's execution?

**The simple answer.** `async` lets a method use `await`. If an awaited operation is incomplete, the method can pause and return control to its caller, then continue when the operation finishes.

**Example.** Instead of freezing the order screen while waiting for a price, await the asynchronous price request.

Here is a complete console example. `Task.Delay` simulates a pending response without blocking a thread; it does not actually contact a service.

```csharp
using System;
using System.Threading.Tasks;

Console.WriteLine("Requesting price...");
decimal price = await GetPriceAsync();
Console.WriteLine($"Price received: {price}");

static async Task<decimal> GetPriceAsync()
{
    await Task.Delay(100);
    return 19.95m;
}
```

Expected output:

```text
Requesting price...
Price received: 19.95
```

Your locale may display a comma instead of a decimal point. `decimal` is a base-10 numeric type often used for prices; `19.95m` is a decimal value. `Task<decimal>` means the operation will provide a decimal result. These are top-level statements: C# supplies the entry method for this small program.

**Interview answer.** "Async and await let me wait for asynchronous operations without blocking the calling thread during the wait. They do not automatically create a thread or make the underlying work faster."

### Going deeper: what the compiler does

The compiler constructs a **state machine**, code that stores progress and resumes execution. The method starts synchronously on its calling thread. An already-completed await may not pause at all.

A long calculation before the first incomplete await still runs on that calling thread. The executable `AsyncExecution` check demonstrates this using a manually controlled task.

**Concurrency** means operations overlap in progress. **Parallelism** means work runs simultaneously. One does not automatically imply the other.

## 027. How do Task, Thread, the thread pool, and Task.Run differ?

**The simple answer.** A Task describes an operation. A thread executes instructions. The thread pool reuses workers, and Task.Run schedules work on those workers.

| Term | Meaning | Practical decision |
|---|---|---|
| `Task` | Represents an operation's completion, failure, or cancellation | Return it so callers can await and observe the outcome |
| `Task<T>` | A task with a result of type `T` | Use for a lookup returning a quote |
| Thread | An operating-system-scheduled execution resource | Do not assume every task has a dedicated one |
| Thread pool | Runtime-managed reusable worker threads | Avoid blocking them unnecessarily |
| `Task.Run` | Queues work to the thread pool and returns its task | Useful for offloading desktop computation, not a universal async wrapper |

**Example.** A desktop application can call `await Task.Run(() => CalculateReport())` to move an expensive calculation off its interface thread, the thread used to handle the screen. `CalculateReport` represents your calculation method; this line is a usage example, not a complete program.

A naturally asynchronous network call should normally be awaited directly instead of being wrapped in `Task.Run`.

**Interview answer.** "A Task represents completion, not a dedicated thread. I use Task.Run when I deliberately want to run work on a pool worker, not to wrap every asynchronous call."

### Going deeper: scheduling has a cost

Task.Run does not guarantee a dedicated thread, shorter total execution time, or extra processor capacity. The calculation occupies a pool worker while it runs. Manually creating a Thread needs explicit lifetime and failure management and is rarely the starting point for ordinary application code.

A cancellation token passed to Task.Run may prevent work from starting. It does not interrupt an already-running calculation; that code must check cancellation.

## 028. How should CPU-bound work and I/O-bound work be handled differently?

**The simple answer.** Ask: "Is the application busy calculating, or mostly waiting?"

- **CPU-bound** means mostly computing: producing a complex report or processing an image.
- **I/O-bound** means mostly waiting for input/output: a database query or a network response.

**Example.** Await a price service's asynchronous call directly. For a desktop report calculation, consider Task.Run so the user can still interact with the screen.

**Interview answer.** "I await asynchronous I/O directly. For CPU work, I decide where it should run based on responsiveness and available capacity."

### Going deeper: desktop and server decisions differ

ASP.NET Core request code already uses pool workers. Task.Run usually moves work between those workers, not adds capacity.

Heavy server computation may need bounded processing, limiting how much runs at once, or a separately managed background job. **Throughput**, work completed per unit of time, can worsen if each request starts too much parallel work.

## 029. Why can Result, Wait, and other sync-over-async patterns cause problems?

**The simple answer.** On an incomplete task, Result and Wait keep the calling thread blocked. Prefer await when the calling method can be asynchronous.

**Example.** Compare these alternative lines in a method that calls your `GetPriceAsync` operation:

```csharp
decimal blockingPrice = GetPriceAsync().Result;
decimal awaitedPrice = await GetPriceAsync();
```

They illustrate two choices, not a recommendation to make the request twice. The first can freeze a desktop interface. If the awaited operation needs that same interface thread to continue, it can **deadlock**: each side waits for the other.

**Interview answer.** "I use await through the call chain because blocking can freeze a UI, deadlock, or waste server workers."

### Going deeper: not every application deadlocks the same way

**Sync-over-async** is the name for blocking on asynchronous work. ASP.NET Core normally lacks the desktop-style context responsible for this particular deadlock, but blocked workers can still cause thread-pool starvation, covered in question 038.

`.GetAwaiter().GetResult()` still blocks. Wrapping a blocking wait in Task.Run does not make the underlying operation nonblocking. The runnable tests do not deliberately hang a thread or starve the machine.

## 030. What role do SynchronizationContext and ConfigureAwait play in asynchronous code?

**The simple answer.** Some code must continue in a particular place. Updating desktop controls normally requires the interface thread.

A **SynchronizationContext** helps arrange where code continues after an await. That remaining code is called a **continuation**.

**Example.** A desktop button handler downloads a price, then updates a label. Returning to the interface thread matters there. A reusable networking library may not need that same context.

`ConfigureAwait(false)` says that this await does not require returning through the captured context. It does not force a new thread.

**Interview answer.** "I preserve the context when the next code needs it, such as a UI update. I do not add ConfigureAwait(false) everywhere without considering the caller."

### Going deeper: what it does not change

- Task awaiting can also consider a custom **TaskScheduler**, a component that schedules task execution. ConfigureAwait(false) avoids requesting resumption through it too.
- **ExecutionContext**, runtime state carried with execution, is separate. This setting does not remove all that state.
- ASP.NET Core normally has no custom synchronization context to return to.
- The setting affects one await, not the entire call chain, and is not a universal fix for blocking.

The console checks do not simulate a desktop framework or prove UI-thread behavior.

## 031. How do Task.WhenAll and Task.WhenAny differ in coordination and failure handling?

**The simple answer.** Use WhenAll when you need every operation to finish. Use WhenAny when you need to react to the first completion.

**Example.** Your screen needs independent price and stock results:

```csharp
Task<decimal> priceTask = GetPriceAsync();
Task<int> stockTask = GetStockAsync();
await Task.WhenAll(priceTask, stockTask);

decimal price = await priceTask;
int stock = await stockTask;
```

This usage fragment assumes price and stock methods returning the shown Task types. Calling them starts the operations; WhenAll waits for both. The two results are used only if the combined wait succeeds.

For WhenAny, imagine asking two providers for a price. The first completion might be an error, not a usable price. Await the returned task to find out.

**Interview answer.** "WhenAll waits for every supplied task. WhenAny gives me the first completed task, which I must still await. Neither automatically cancels the other work."

### Going deeper: multiple failures and shared dependencies

- WhenAll completes only after all inputs finish. Its results preserve input order, not completion order.
- If an input fails, the combined task fails. With no failures but at least one cancellation, it is cancelled.
- Awaiting a failed combined task usually throws one exception. Its `Exception` property holds an `AggregateException`, a container retaining multiple failures.
- WhenAny's outer task succeeds in returning a completed input task, even if that input failed or was cancelled.
- Decide whether to cancel and observe slower operations that your code owns; do not leave their outcomes unhandled.
- Do not overlap operations on one Entity Framework Core `DbContext`, a database session object that does not support concurrent operations.

The executable checks demonstrate multiple WhenAll failures and the need to observe the task returned by WhenAny.

## 032. How should CancellationToken propagate through an application?

**The simple answer.** Cancellation asks work to stop; it does not forcibly kill execution. A **CancellationTokenSource** sends the signal, and its **CancellationToken** carries that signal to the work.

**Example.** A user cancels the order screen's price lookup. Pass the token from the screen through your service method to the actual network call. The same idea applies to database calls and delays.

**Interview answer.** "I pass the caller's cancellation token through every participating layer. Cancellation is cooperative, and it does not undo work already accepted by another service."

### Going deeper: timeouts and business decisions

Code doing a long calculation must periodically check cancellation, for example with `ThrowIfCancellationRequested()`.

In ASP.NET Core, the request-abort token can signal a disconnected caller. Use it for work that should end with that request; do not abandon a committed business operation merely because a browser disconnects.

To keep caller cancellation and add a timeout, use a linked source, which reacts to either signal:

```csharp
using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
linked.CancelAfter(TimeSpan.FromSeconds(5));
await client.GetAsync(productId, linked.Token);
```

This is a usage fragment: `callerToken`, `client`, and `productId` are supplied by the calling operation. The runnable check uses the same mechanism with a controlled delay.

Dispose sources you create after their users finish, but do not dispose a borrowed source. A timeout does not prove a remote payment was never accepted. Define how you will confirm its outcome before retrying or reporting that nothing happened.

An `OperationCanceledException` expresses cancellation. Do not broadly catch it and report a format or storage error instead.

## 033. When is async void appropriate, and why is it risky elsewhere?

**The simple answer.** Ordinary async methods should return Task or `Task<T>`, so callers can await completion and handle errors. An async void method gives the caller no Task.

**Example.** A desktop button-click event may require a void-returning handler. An **event handler** is the method invoked when the click happens. That is a common valid place for async void.

Keep the handler small, await Task-returning helpers, and handle expected errors inside it. A normal SaveOrderAsync method should return Task instead.

**Interview answer.** "I reserve async void for signatures that require void, usually event handlers. Normal async methods return Task so callers can observe their outcome."

### Going deeper: background work needs an owner

Starting an unawaited email during a web request can outlive objects created only for that request and lose failure reporting. Async void is not a job-processing mechanism.

Use framework-managed background processing or a durable job system according to the required lifetime. **Durable** means stored work can survive process termination; not every in-memory queue has that guarantee.

The sample avoids async void entirely and awaits all work it starts.

## 034. When might ValueTask be preferable to Task?

**The simple answer.** Start with Task. ValueTask is an optimization to consider after measurements, not the next step every async method needs.

`ValueTask<T>` can hold an immediate result or represent an operation that finishes later.

**Example.** A price lookup often finds its answer already in memory, in a **cache**. Returning that immediate answer through ValueTask may avoid creating some Task objects.

**Interview answer.** "I use Task by default. I consider ValueTask for a frequently called operation that often completes immediately, if profiling shows a benefit."

### Going deeper: an optimization with rules

**Profiling** means measuring actual behavior. **Allocation** means reserving memory for an object. ValueTask is a value type that can contain the immediate result, a Task, or a reference to a reusable asynchronous-operation source.

- Start with Task unless measurements justify the extra complexity.
- Some completed tasks can be cached or reused, so synchronous completion does not automatically imply a ValueTask benefit.
- A ValueTask may copy more state than a Task reference.
- Treat a returned ValueTask as single-consumption: do not generally await it repeatedly or concurrently.
- If callers need a Task, call `AsTask()` once and reuse that Task. Repeated conversions or combining many operations may erase any benefit.

The runnable check consumes one immediate ValueTask result once. It does not claim that every possible ValueTask source has the same reuse behavior.

## 035. How do race conditions arise when asynchronous operations share mutable state?

**The simple answer.** A race condition is a bug where the result depends on the ordering of operations. Mutable state just means data that can change.

**Example.** Stock starts at 10:

1. Sale A reads 10.
2. Sale B also reads 10.
3. Each calculates 9.
4. Both save 9, although two items were sold.

This can happen across awaits even without simultaneous execution. Both operations can read before either writes.

**Interview answer.** "I avoid shared updates where possible. If an update must be shared, I protect the complete read-modify-write operation, not just each step."

### Going deeper: protection has a scope

An in-process lock coordinates code within that running application, not other servers or database clients. Database stock requires database-level protection, such as a conditional update or suitable transaction/concurrency policy.

The executable `LostUpdate` check deliberately arranges both reads before either write, reliably demonstrating the bug.

## 036. When should you use lock, SemaphoreSlim, or a concurrent collection?

**The simple answer.** Choose based on what you are protecting:

- **lock:** a short block of synchronous shared work. You cannot await inside it.
- **SemaphoreSlim:** permission to enter protected work, including waiting asynchronously with WaitAsync. For one-at-a-time access, configure one permit.
- **Concurrent collection:** a collection with documented safe individual operations from multiple threads.

**Example.** Protect a short in-memory stock update with lock. If the protected sequence includes await, an appropriately shared SemaphoreSlim can provide an asynchronous gate.

**Interview answer.** "I use lock for short synchronous sections, SemaphoreSlim for asynchronous access control, and concurrent collections for their supported thread-safe operations."

### Going deeper: safe operations do not make a whole workflow safe

| Mechanism | Fits | Does not guarantee |
|---|---|---|
| `lock` | Short synchronous critical sections | Async suspension inside the block |
| `SemaphoreSlim` | Limiting entrants, including asynchronous waiting | Cross-process coordination or a rate limit |
| Concurrent collection | Documented safe operations from multiple threads | Atomicity of every combination of operations |

A **critical section** is code restricted to controlled access. **Atomic** means indivisible with respect to the relevant concurrent operations.

For an update that needs an asynchronous gate:

```csharp
await gate.WaitAsync(cancellationToken);
try
{
    await UpdateAsync(cancellationToken);
}
finally
{
    gate.Release();
}
```

This usage fragment assumes the operation shares a `SemaphoreSlim` named `gate` and supplies `UpdateAsync`. Acquire before entering the try: if acquisition is cancelled, you must not release a permit you never obtained. The runnable `ProtectedUpdate` check uses this structure.

**Trade-off.** Holding a gate during slow external I/O serializes other entrants; sometimes a different design is preferable. Dispose a semaphore only after its users have completed.

`ConcurrentDictionary.GetOrAdd` safely establishes an entry, but its **value factory**, the function producing a candidate value, can run multiple times. Do not put an irreversible external action in that factory and assume exactly-once execution.

## 037. How would you limit concurrency when calling an external API?

**The simple answer.** Set a maximum number of active calls instead of starting everything at once.

An **API**, Application Programming Interface, is how your code accesses another component or service.

**Example.** You need 100 prices, but allow only four requests at a time:

1. Start up to four.
2. When one finishes, start the next.
3. Continue until the batch finishes, fails, or is cancelled.

`Parallel.ForEachAsync` supports this with `MaxDegreeOfParallelism = 4`. Pass cancellation into each service call.

**Interview answer.** "I limit active operations and propagate cancellation. I also check whether the provider's limit is per batch, across the application, or requests per second."

### Going deeper: runnable batch implementation

You can skip this implementation on a first read. The essential idea is four active calls, not 100 simultaneous calls.

This method is copied from [PriceBatch.cs](./examples/AsyncConcurrency/PriceBatch.cs). `IPriceClient` returns a `PriceQuote` and accepts cancellation; the component receives that client through its constructor.

```csharp
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
```

Terms in the implementation:

- `IReadOnlyList<int>` provides indexed reading but does not guarantee the caller's underlying list is immutable.
- `ToArray()` creates a snapshot for the batch. The caller must not mutate the input concurrently while it is being validated/copied.
- `Enumerable.Range` produces the result indexes. Each operation writes a different array element, so there is no shared list being appended to concurrently.
- The result array preserves input order even if calls complete out of order.
- `ParallelOptions` supplies the limit and caller cancellation.
- The loop's body receives its own token. Pass that token onward: it also reflects the loop's internal cancellation when another body fails.
- Returning only after awaiting the loop prevents incomplete arrays from being returned as success.

**Failure policy.** Exceptions propagate; the sample does not return a partial array as success. The loop may stop scheduling more work and signals other bodies to stop after a failure, but already-started operations must cooperate. It does not roll back remote side effects. This differs from plain Task.WhenAll, which does not itself cancel sibling operations.

**Trade-offs and scope.**

- This limits simultaneous calls, not calls per second. A **rate limit** restricts requests over time.
- Four active operations in each of 100 batches can still produce much more than four active calls.
- A **replica** is another running copy of the application. An in-process limiter does not coordinate all replicas.
- A real HTTP adapter should reuse appropriately managed HttpClient connections, propagate cancellation, and dispose response objects it owns. The demo has no real HTTP adapter and does not claim to test one.
- An empty batch succeeds with an empty result. Invalid IDs and batches above 1,000 items are rejected before calling the client.

## 038. How would you diagnose thread-pool starvation in an ASP.NET Core service?

**The simple answer.** Thread-pool starvation means work waits too long for an available worker. Workers may be stuck waiting rather than using the processor.

**Example.** Many requests block on Result. More requests arrive, but workers are still waiting for the earlier calls. Response times rise even if processor use is not especially high.

Start by checking runtime measurements with dotnet-counters, then inspect what workers are doing with dotnet-stack. Do not diagnose starvation from slow responses alone.

**Interview answer.** "I compare worker and queue measurements with blocked thread stacks under realistic load, remove unnecessary blocking, then repeat the same test."

### Going deeper: diagnose before tuning

A **stack** shows the method calls active on a thread. Queues growing alongside workers blocked in `.Result`, `.Wait()`, synchronous I/O, or locks are useful evidence.

Use this sequence:

1. Reproduce with representative traffic in a safe test environment.
2. Observe latency, request throughput, processor usage, thread count, and queued/completed pool work.
3. Use `dotnet-counters` to inspect runtime metrics, numeric measurements over time.
4. Use `dotnet-stack` for thread stacks or `dotnet-trace` for an execution trace, a time-ordered record of runtime events.
5. Correlate blocked stacks with metrics; investigate slow dependencies, processor saturation, and lock contention too.
6. Remove unnecessary blocking, bound work, then repeat the same load test.

Exact metric names and displays depend on runtime/tool versions. See the [official starvation diagnostic tutorial](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-threadpool-starvation).

**Trade-off.** Increasing minimum pool threads can affect recovery from bursts but can also increase memory and scheduling overhead. It is not a general fix for blocking or unbounded work.

The executable sample does not run load against a server or install diagnostic tools. Its passing checks do not establish production throughput or prove that a real service is free from starvation.

## Executable checks and limitations

| Check | What it establishes |
|---|---|
| Synchronous prefix | Code runs before the first incomplete await |
| WhenAll failures | Every task is awaited and both faults are retained |
| WhenAny failure | The returned task must still be observed; other work remains pending |
| Linked cancellation | Caller cancellation reaches a linked operation |
| Immediate ValueTask | One immediate result is consumed once |
| Lost update | Controlled interleaving reproduces incorrect shared state |
| Semaphore gate | The protected read-modify-write preserves both sales |
| Four-call bound | Exactly four calls can be active; 100 results preserve input order |
| Single-call bound | A concurrency limit of one is respected |
| Empty batch | No provider calls |
| Pre-cancellation | No provider calls |
| In-flight cancellation | Every active controlled call observes cancellation and exits |
| Provider failure | Failure is propagated, not returned as partial success |
| Failure with active siblings | The loop signals cancellation and waits for the other three calls to exit |
| Exact batch-size boundary | All 1,000 products accepted without exceeding the concurrency bound |
| Invalid input | Zero limit, invalid IDs, and oversized input are rejected |

Test synchronization uses `TaskCompletionSource<T>`, an object allowing test code to complete a task explicitly. `RunContinuationsAsynchronously` prevents completion from running attached continuations inline on the completing call. `Interlocked` performs atomic updates, and `Volatile.Read` ensures reads follow the required memory-visibility rules.

The fake provider uses gates rather than sleep-based timing assertions. These checks cover operation coordination, not network performance. No real UI context, HTTP server, database, production profiler, retry system, or durable queue is included.

## Interview recap

| Question | Practical answer | Watch out for |
|---|---|---|
| What do async and await change? | Suspend at an incomplete await and resume later | No automatic thread creation |
| Task, Thread, thread pool, or Task.Run? | Completion representation versus execution resources | A task is not a dedicated thread |
| CPU-bound or I/O-bound? | Offload desktop computation; await asynchronous I/O directly | Task.Run is not additional server capacity |
| Why avoid Result and Wait? | Blocking can deadlock or occupy scarce workers | GetAwaiter().GetResult still blocks |
| SynchronizationContext and ConfigureAwait? | Control continuation scheduling where applicable | Not a forced thread switch or universal cure |
| WhenAll or WhenAny? | Await all outcomes or select the first completed task | No automatic loser cancellation from these combinators |
| How propagate cancellation? | Pass the token and cooperate through every layer | Cancellation is not rollback |
| When use async void? | Required void-returning event handlers | No task for callers to await or observe |
| When choose ValueTask? | Measured benefit for frequent immediate results | Consume once; conversions may erase savings |
| How do race conditions arise? | Interleaved updates share stale state | Can occur across awaits, not just parallel execution |
| Which synchronization primitive? | Choose by synchronous, asynchronous, or collection needs | Individual safe operations do not make workflows atomic |
| How bound external calls? | Limit active operations and propagate cancellation | Batch limits are neither global limits nor rate limits |
| How diagnose starvation? | Correlate metrics with blocked worker stacks under load | Symptoms alone do not prove the cause |

## References

- [Microsoft Learn: Async scenarios](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios)
- [Microsoft Learn: Async return types](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types)
- [Microsoft .NET blog: ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
- [Microsoft Learn: Task.WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall)
- [Microsoft Learn: Task.WhenAny](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenany)
- [Microsoft Learn: Cancellation in managed threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [Microsoft Learn: ValueTask documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1)
- [Microsoft Learn: SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim)
- [Microsoft Learn: Parallel.ForEachAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.foreachasync)
- [Microsoft Learn: ConcurrentDictionary.GetOrAdd](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd)
- [Microsoft Learn: Diagnose thread-pool starvation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-threadpool-starvation)
