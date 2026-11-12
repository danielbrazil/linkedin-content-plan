# From LINQ to an asynchronous data pipeline | Part 4/4

Imagine one part of your app reads products quickly, but another takes longer to fetch their prices and save them.

Where should products wait, and what happens if too many are waiting?

48\. When would you use a Channel?

A Channel is a queue inside your running app. One piece of code puts items in; another takes them out.

Think of a tray between two workers.

📝 Example:
A reader finds products A, B, and C.
It puts them in the queue.
A price worker takes a product, fetches its price, and saves it.

The reader is called the producer. The price worker is the consumer.

Use a Channel when you want these two pieces to work independently, with a place for items to wait.

If a simple loop can read and process each product, you may not need a Channel.

The queue lives in memory, not permanent storage. Pending items can be lost if the app stops.

49\. What is backpressure?

It means asking the reader to slow down because the worker cannot keep up.

📝 Example:
Give the queue room for only two waiting products:

1. A and B fill the queue.
2. The reader tries to add C.
3. With the queue configured to wait when full, adding C waits.
4. The worker takes A, making room for C.

That slowing-down signal is backpressure. In C#, a bounded Channel with FullMode = Wait provides this through await WriteAsync.

The wait does not block a thread. Other full modes can drop items, so the setting matters.

Two waiting items does not mean two products total: a worker may already be processing another, and the reader may hold a page.

Limit both waiting items and active workers. Also arrange for both sides to stop on cancellation or failure, so neither waits forever.

50\. How would you measure before optimizing?

Before changing the code, record how it behaves.

📝 Example:
An export saves 100 products in 10 seconds.
That is 10 products per second: its throughput.

Then check:
• How long until the first product is saved?
• How many bytes does processing allocate, meaning request for new objects?
• Does memory keep growing?
• Did every product save successfully?

Allocated bytes are not the same as memory still in use.

Run a warm-up, then repeat before and after the change with the same products and conditions. A faster local demo does not prove a real service will be faster.

📌 Interview takeaway:
"I use a queue when separate stages need it, slow the reader when the queue is full, and measure before claiming an improvement."

Examples and optional deeper explanations, questions 048-050:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Linq_Data_Pipeline.md

#CSharp #DotNet #LINQ
