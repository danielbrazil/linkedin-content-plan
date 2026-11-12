# From LINQ to an asynchronous data pipeline | Part 1/4

You need to export active products. Some are in a list; others arrive from a database or a web service.

The filtering may look similar, but when and where does the work happen?

39\. IEnumerable, IQueryable, or IAsyncEnumerable?

Think of the same job: reading active products. Here, T means the item type, such as Product.

`IEnumerable<Product>`: read items with foreach. Getting the next item is synchronous.

📝 Example:
`var active = productList.Where(p => p.IsActive);`
`foreach (var p in active) { Console.WriteLine(p.Id); }`

Here, productList is a list already in memory. Your application checks each product.

`IQueryable<Product>`: describe a query that a provider interprets.

📝 Example:
`var active = db.Products.Where(p => p.IsActive);`

Here, db is an Entity Framework Core database context. When executed, a supported filter can become SQL, so the database selects active rows before sending them back. Defining this query does not execute it yet.

`IAsyncEnumerable<Product>`: read items with await foreach, allowing an asynchronous wait for the next item.

📝 Example:
`await foreach (var p in ReadActiveAsync()) { Console.WriteLine(p.Id); }`

Here, ReadActiveAsync is our paginated feed method. It can await another web-service page, then yield its products. Not every item needs a separate request.

These fragments need the sources described above. The interfaces alone do not guarantee parallelism or bounded memory.

40\. What is deferred execution?

It means the work happens later, when the sequence is consumed, rather than when the query is defined.

📝 Example:
`var active = products.Where(p => p.IsActive);`

For a list, this defines a filter. A foreach actually reads the matching products. ToList reads them now and stores the results.

Not every operation streams: sorting with OrderBy must inspect the input before producing the first sorted result.

41\. Why can reading a query twice cause problems?

Imagine counting the active products and then looping over them.

A deferred query may run twice. With a remote query, that can mean two database calls. If the underlying data changes, the results may also differ.

📌 Practical rule:
Reuse a materialized list when you deliberately need the same captured results and they fit in memory. Otherwise, try to process the sequence once.

A list captures membership, not a deep copy of its objects.

📌 Interview takeaway:
"I check where a query executes, when it runs, and whether I am accidentally running it more than once."

➡️ Next: choosing LINQ operators that express what you actually need.

More examples, questions 039-041:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Linq_Data_Pipeline.md

#CSharp #DotNet #LINQ
