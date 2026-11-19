# Designing a production-ready REST endpoint | Part 3/4

A customer refreshes an order page ten times. Must the API resend the same data ten times?

70\. What are ETags and conditional requests?

An ETag is a server-provided label for a representation's version. It is not necessarily a hash.

📝 Example:
The first GET returns the order and ETag: "v1".

The next GET sends If-None-Match: "v1".
If the representation is unchanged, the API can return 304 Not Modified without resending the body.

The client reuses its saved representation.

For updates, If-Match solves a different problem:
"Apply my change only if this is still the version I read."

An outdated version normally gets 412 Precondition Failed. The version check and database update must happen atomically, not as separate unprotected steps.

ETags do not replace authentication or access checks.

71\. Why rate-limit an endpoint?

To control how much traffic callers can send over time.

📝 Example:
Allow a caller 100 requests per minute. Once that allowance is used, return 429 Too Many Requests, with Retry-After when a useful wait can be given.

Choose limits from real workload and capacity. Separate callers so one noisy account does not use everyone's allowance.

An IP-only limit may unfairly group people sharing a network.

Rate limits count traffic over time. Concurrency limits cap work happening at once. You may need both.

A limit stored inside one server does not automatically cover all your running servers.

72\. Why use IHttpClientFactory?

Your API may call another API, such as a product catalog.

IHttpClientFactory creates configured HttpClient instances while managing reuse of the underlying connections through handlers.

📝 Example:
A catalog client has its base address, timeout, and logging configured in one place.

Then handle results deliberately:
• 404 might mean the product is absent.
• 503 means the dependency is unavailable, not "product not found."
• A cancelled call should not become a fake successful result.

Dispose responses you own. A factory is useful, but a correctly configured long-lived HttpClient is also a valid design.

📌 Interview takeaway:
"I use validators to avoid unnecessary transfers, limits to protect capacity, and configured HTTP clients that preserve failures."

➡️ Next: handling slow services and testing the complete request.

Examples and optional deeper explanations, questions 070-072:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Rest_Endpoint.md

#AspNetCore #DotNet #REST
