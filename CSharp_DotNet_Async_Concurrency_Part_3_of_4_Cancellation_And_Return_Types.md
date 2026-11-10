# Async .NET: concurrency without confusion | Part 3/4

A user clicks Cancel. What should happen to the work already running?

32\. How should CancellationToken propagate?

A CancellationToken carries a request to stop. A CancellationTokenSource lets the owner send that request.

📝 Example:
`await GetPriceAsync(productId, cancellationToken);`

Pass the same token through the methods doing the work, including database and service calls. Work must check or support cancellation; the token does not kill a thread.

On a desktop screen, Cancel can stop an unwanted lookup. In ASP.NET Core, the request-abort token can signal that the caller has disconnected.

Cancellation is not undo: a payment may already have reached the other service. Define what must happen before stopping business-critical work.

33\. When is async void appropriate?

An async void method returns no Task. Its caller cannot await its completion or receive failure through a returned Task.

Use it when an event requires a void-returning handler, such as a desktop button click. An event handler is the method called when that event happens.

Keep that handler small, await ordinary Task-returning methods, and handle expected errors inside it.

For a normal SaveOrderAsync method, return Task or `Task<T>` so its caller can wait and react to errors.

Starting an email and forgetting it during a web request is not reliable background processing. It needs a separately managed lifetime and error handling.

34\. When choose ValueTask over Task?

Start with Task.

`ValueTask<T>` can hold an immediate result or represent an operation that finishes later.

📝 Example:
A frequently used price lookup often finds its answer already in memory. ValueTask may avoid creating some Task objects in that path.

That is a measured optimization, not a rule that ValueTask is faster. Consume a returned ValueTask once; repeated awaits are not generally safe.

📌 Interview takeaway:
"I pass cancellation through the call chain, return Task from ordinary async methods, and choose ValueTask only when measurements justify it."

➡️ Next: avoiding lost updates and too many simultaneous calls.

Simple examples and optional deeper explanations, questions 032-034:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Async_Concurrency.md

#CSharp #DotNet #AsyncAwait
