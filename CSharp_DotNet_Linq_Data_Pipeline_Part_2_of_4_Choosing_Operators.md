# From LINQ to an asynchronous data pipeline | Part 2/4

Two LINQ expressions can look similar but tell very different stories.

Start with what the application needs, not which method name you remember.

42\. Select or SelectMany?

Select transforms each item into one result.

📝 Example:
`products.Select(p => p.Id)`

Each product becomes its ID.

If each order contains several product IDs, Select keeps those inner groups:
`orders.Select(o => o.ProductIds)`

SelectMany combines the inner sequences into one:
`orders.SelectMany(o => o.ProductIds)`

Think "a list of lists" versus "one combined sequence." SelectMany does not automatically remove duplicates.

43\. Any or Count for existence?

If the question is "Are there any active products?", say that directly:
`products.Any(p => p.IsActive)`

With an ordinary iterator, Any can stop after its first match. Count with a condition normally examines the whole sequence to calculate the exact total.

Use Count when you need the number. For an existing list, its Count property already has that information.

A database provider decides how to translate the query; inspect the generated SQL rather than assuming it behaves like a list.

44\. First, Single, or an OrDefault variant?

First means "give me the first match." It throws if there is none.

Single means "there must be exactly one match." It throws for zero or more than one.

FirstOrDefault permits no match. SingleOrDefault permits no match but still rejects multiple matches.

📝 Example:
For a supposedly unique product code, SingleOrDefault can expose duplicate data. FirstOrDefault would quietly choose one match.

The default might be null for a reference type or 0 for an int. Neither automatically proves an item existed.

🔎 The trade-off:
Choose by the rule you need to express. Single is not just a slower spelling of First, and it does not replace a database uniqueness constraint.

📌 Interview takeaway:
"Select transforms, SelectMany flattens, Any checks existence, and First versus Single expresses how many matches I expect."

➡️ Next: storing all results or processing them as they arrive.

Examples and optional deeper explanations, questions 042-044:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Linq_Data_Pipeline.md

#CSharp #DotNet #LINQ
