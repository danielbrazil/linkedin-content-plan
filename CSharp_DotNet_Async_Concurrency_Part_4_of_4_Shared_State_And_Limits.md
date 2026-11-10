# Async .NET: concurrency without confusion | Part 4/4

Two customers buy an item. Your stock count drops by only one. What went wrong?

35\. How do race conditions arise?

A race condition is a bug where the result depends on how operations interleave.

📝 Example:
Both sales read 10. Each calculates 9. Both save 9.

The read-and-update steps need protection as one operation. This can happen across awaits, not just on threads running simultaneously.

36\. lock, SemaphoreSlim, or concurrent collection?

Use lock for a short block of shared work that must not overlap. You cannot await inside a lock block.

Use `SemaphoreSlim(1, 1)` for one-at-a-time access that includes await. WaitAsync obtains permission; Release returns it.

Release in finally after successful entry, so permission is returned even if the protected work fails. Finally is the cleanup block of a try statement.

A concurrent collection supports safe individual operations from several threads. Combining "check" and "update" yourself is not automatically safe.

📝 Example:
Protect an in-memory stock update. Stock in a database needs database-level protection too: a local lock cannot coordinate other servers.

37\. How would you limit outbound calls?

Do not start 100 price requests at once just because you can.

Process them with at most four active calls. Parallel.ForEachAsync supports this through MaxDegreeOfParallelism. Pass cancellation to each call.

📝 Example:
As one price request finishes, the next can take its place.

This limits active calls in that batch, not requests per second or all calls across all servers.

38\. How diagnose thread-pool starvation?

The thread pool reuses worker threads. Starvation means work waits too long for an available worker.

📝 Example:
Many requests block workers with Result or Wait. New requests queue even though the processor may not be fully busy.

Use dotnet-counters to inspect runtime measurements, and dotnet-stack to see what threads are doing. Confirm blocking under realistic traffic; other bottlenecks can look similar.

Do not simply raise thread counts and assume the cause is fixed.

📌 Interview takeaway:
"I protect shared updates, limit simultaneous calls, and diagnose blocked workers instead of assuming every slowdown needs more threads."

Simple examples and optional deeper explanations, questions 035-038:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Async_Concurrency.md

#CSharp #DotNet #AsyncAwait
