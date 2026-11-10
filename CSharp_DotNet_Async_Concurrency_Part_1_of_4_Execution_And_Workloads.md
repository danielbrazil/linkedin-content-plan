# Async .NET: concurrency without confusion | Part 1/4

Your app asks a service for a product price. The answer takes two seconds.

Does your app have to freeze while it waits?

26\. What do async and await do?

`async` lets a method use `await`. With a genuinely asynchronous call, await lets the method pause without blocking its thread while the result is pending.

A thread is a path through which your program executes instructions.

📝 Example:
`decimal price = await GetPriceAsync();`

Code using price continues after the result arrives. On a desktop screen, this can keep the interface responsive.

It does not make the service faster or automatically create a thread. Code before the wait still runs normally; if the result is already available, there may be no pause.

27\. Task, Thread, the thread pool, and Task.Run?

A Task represents an operation you can wait for and observe: did it finish, fail, or get cancelled? `Task<decimal>` also carries a decimal result.

A Task is not a thread. Think "operation" versus "execution resource."

The thread pool is a set of reusable worker threads. Task.Run schedules work on that pool.

📝 Example:
On a desktop app, `await Task.Run(() => CalculateReport())` can move a long calculation off the interface thread.

Do not wrap an already-asynchronous price request in Task.Run just to make it "more async."

28\. CPU-bound or I/O-bound?

CPU-bound means mostly computing: generating a complex report.

I/O-bound means mostly waiting for input/output: reading a database or calling a service.

Await asynchronous I/O directly. For desktop computation, consider Task.Run to preserve responsiveness.

In ASP.NET Core, request code already uses pool threads. Adding Task.Run usually moves work between workers, not adds server capacity.

📌 Interview takeaway:
"I use async to avoid blocking during waits. I choose Task.Run based on the workload, not simply because a method is slow."

➡️ Next: why Result can freeze a screen, and how to wait for several tasks.

Simple examples and optional deeper explanations, questions 026-028:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Async_Concurrency.md

#CSharp #DotNet #AsyncAwait
