# From LINQ to an asynchronous data pipeline | Part 3/4

Imagine exporting 10,000 products. Do you need to keep them all in a new list before you start?

Here are two ways to work, and how to stop safely.

45\. What changes when you call ToList?

ToList reads all results now and puts them into a list. The technical name is materialization.

📝 Example:
`var active = products.Where(p => p.IsActive).ToList();`

Here, products is your source, and IsActive identifies the products to keep.

You can count active and loop over it again without rerunning the filter. That is useful when you need the results several times.

The cost: enough memory to hold the whole result list. It copies references to product objects, not separate copies of those objects.

With EF Core database queries, ToListAsync can wait asynchronously, but still stores all returned results.

46\. What does yield return do?

It lets a method hand back one item at a time, instead of building a complete result list first. This kind of method is called an iterator.

📝 Example:
`IEnumerable<int> GetIds()`
`{`
`    yield return 10;`
`    yield return 20;`
`}`

In a C# program with System.Collections.Generic available, foreach can read these IDs. The first request gets 10. The next gets 20. If the loop breaks after 10, the method does not reach the second yield.

This is the idea behind streaming: process items as they arrive. A web feed may still hold one page in memory; streaming does not mean zero buffering.

47\. How do cancellation and cleanup work with an async stream?

Now imagine the next page needs a web request. Await foreach lets your loop wait asynchronously for more items.

📝 Example:
The user clicks Cancel during the export:

1. Your code signals a cancellation token: a shared request to stop.
2. The feed passes that token to the page request.
3. If the operation supports cancellation, it stops with a cancellation exception.

The token does not forcibly kill a thread.

Cleanup matters too. Await foreach disposes the object tracking your position in the stream when the loop finishes, breaks, or throws.

The feed must release resources it owns, such as a file, through using, await using, or finally. These arrange cleanup when control leaves the protected code.

Stopping and cleaning up do not undo products already saved. Token wiring details are in the article.

📌 Interview takeaway:
"I use a list for reusable results, stream for incremental processing, and pass cancellation through to the actual work."

➡️ Next: keeping a fast producer from overwhelming a slower consumer.

Examples and optional deeper explanations, questions 045-047:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Linq_Data_Pipeline.md

#CSharp #DotNet #LINQ
