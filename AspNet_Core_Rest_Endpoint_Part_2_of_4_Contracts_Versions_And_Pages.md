# Designing a production-ready REST endpoint | Part 2/4

Your database changes. Must every frontend change too?

A clear API contract helps keep those changes separate.

67\. Why separate DTOs from database entities?

A DTO, Data Transfer Object, describes what crosses the API boundary.

A database entity describes how your app stores data. They have different jobs.

📝 Example:
CreateOrder input:
{"productId":1,"quantity":2}

Order output:
{"id":"...","productId":1,"quantity":2,"total":25}

The server calculates the total and chooses the owner from the authenticated identity.

The caller cannot set internal fields such as Owner, IsApproved, or the database version just because the entity contains them.

DTOs still need validation and deliberate mapping.

68\. How should an API evolve?

Avoid surprising existing callers.

📝 Example:
Renaming total to amount can break a React app reading total. Returning text where a number used to be can break it too.

Prefer compatible changes when possible, and test them against clients. Even adding a field can affect a strict client.

For a breaking change, introduce a deliberate version, such as /api/v2/orders, while supporting v1 for an announced period.

Document the change, migration path, and retirement date. A version number alone does not make a change safe.

69\. How should filtering, sorting, and pagination work?

Filtering chooses which records to return.
Sorting chooses their order.
Pagination divides them into manageable pages.

📝 Example:
GET /api/v1/orders?productId=1&limit=10&after=25

In our example, return this caller's orders for product 1, sorted by an increasing sequence, after position 25.

The response contains items and a nextAfter value. Send that cursor with the next request, keeping the same filters.

Always use a stable order with a unique tie-breaker, and cap the page size. Do not fetch every row and only then throw most of them away.

Choose supported filter and sort fields explicitly. Do not treat arbitrary client text as SQL.

A cursor avoids some problems of skipping rows, but it does not freeze a changing dataset.

📌 Interview takeaway:
"I expose deliberate DTOs, evolve the contract carefully, and return bounded pages in a predictable order."

➡️ Next: avoiding unnecessary reads and too much traffic.

Examples and optional deeper explanations, questions 067-069:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Rest_Endpoint.md

#AspNetCore #DotNet #REST
