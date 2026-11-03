# C# and .NET: beyond the definitions - Part 4/4

C# and .NET: beyond the definitions | Part 4/4
"The garbage collector handles it" is not a resource-management strategy.

11  How does generational garbage collection work?

The garbage collector, or GC, reclaims managed memory when objects are no longer reachable.

Reachable means the runtime can follow references to an object from a root: a starting point such as a static field or a reference held by executing code.

The GC groups objects into generations 0, 1, and 2. Most small objects start in generation 0; survivors can move to older generations. Younger generations are collected more frequently because many objects are short-lived. Large objects are typically allocated on the Large Object Heap (LOH), collected as part of generation 2 garbage collections.

The trade-off: automatic memory management reduces manual work, but garbage collection consumes processor resources and may temporarily suspend managed application threads.

Scenario: an order service creates temporary objects per request. At high traffic, these can increase collection work. Measure before reducing allocations; routine GC.Collect calls do not fix retention.

12\. Managed vs. unmanaged resources?

Managed memory is tracked by the GC. Operating-system resources, such as open file handles, need explicit release. A handle is an identifier used to access a resource outside your managed objects.

Scenario: a worker imports thousands of files. Each managed FileStream object wraps an operating-system file handle. Use using to release it after each import, not wait for GC.

IDisposable defines Dispose, the cleanup method. A using statement calls it when execution leaves the block, including when an exception interrupts the code.

IAsyncDisposable and await using allow cleanup that completes asynchronously, without blocking a thread while waiting.

Disposal does not force collection, and the GC does not automatically call Dispose. Finalization, a runtime fallback cleanup mechanism, is not prompt or guaranteed at process exit.

Dispose what you own; do not dispose a borrowed dependency unless its ownership contract says you should.

13\. Can managed code still leak memory?

Yes. The GC cannot reclaim objects your application still references, even if they are no longer useful.

Scenario: a price cache stores every product ever requested and never removes entries. A cache holds data for reuse; add size limits or expiration so old entries can leave.

A long-lived event publisher can retain subscribers. Remove stale references and end subscriptions. A memory profiler helps identify retained objects and inspect reference paths preventing their collection.

The GC manages reachability; developers manage ownership, retention, and resource lifetimes.

More scenarios and code: article sections 011-013.
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Beyond_The_Definitions.md

#CSharp #DotNet #InterviewPreparation
