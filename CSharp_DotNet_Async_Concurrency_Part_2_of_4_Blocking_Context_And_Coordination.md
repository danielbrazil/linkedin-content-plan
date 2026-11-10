# Async .NET: concurrency without confusion | Part 2/4

You started using async. Why does the screen still freeze?

29\. Why avoid Result and Wait?

These block the calling thread when the task is incomplete.

📝 Example:
`decimal price = GetPriceAsync().Result;`

The interface thread can be stuck waiting. If GetPriceAsync also needs that thread to continue, neither side can proceed. That is a deadlock.

Prefer `decimal price = await GetPriceAsync();` and make the calling methods asynchronous too.

On a server, blocking can leave too few workers available for other requests, even without that desktop deadlock.

30\. SynchronizationContext and ConfigureAwait?

Some code must run in a particular place: desktop controls normally belong to the interface thread.

A SynchronizationContext helps arrange where code continues after an await.

`ConfigureAwait(false)` says this await does not need to return through the captured context.

📝 Example:
A reusable download library may not care where it resumes. A desktop button handler updating a label does care.

Do not add it everywhere blindly. It does not force a new thread or fix all blocking problems. ASP.NET Core normally has no custom context to return to.

31\. Task.WhenAll or Task.WhenAny?

WhenAll waits until every supplied task finishes.

📝 Example:
Start independent price and stock requests, then await WhenAll before displaying both.

WhenAny returns the first task to finish. Await that returned task too: finishing first does not mean succeeding.

📝 Example:
Ask two price providers and examine the first response. The other request keeps running unless you separately arrange cancellation.

Neither method starts the tasks or automatically cancels the others. WhenAll reports failure if any input fails; the article shows how to inspect multiple failures.

🔎 The trade-off:
Independent calls can overlap and reduce total waiting. But extra simultaneous calls also increase load. Do not run overlapping database operations on one EF Core DbContext, a database session that does not support concurrent operations.

📌 Interview takeaway:
"I await instead of blocking, and I choose WhenAll or WhenAny based on whether I need every result or the first completion."

➡️ Next: stopping unwanted work and choosing return types.

Simple examples and optional deeper explanations, questions 029-031:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Async_Concurrency.md

#CSharp #DotNet #AsyncAwait
