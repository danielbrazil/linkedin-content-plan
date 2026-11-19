# Designing a production-ready REST endpoint | Part 4/4

Your API calls a slow service. Should it wait forever, try again, or give up?

73\. Timeout, retry, or circuit breaker?

They solve different problems.

• Timeout: stop waiting after a time budget.
• Retry: try again after a failure that may be temporary.
• Circuit breaker: temporarily stop calling a dependency after enough failures indicate trouble.

📝 Example:
A catalog GET fails briefly. A small, bounded retry may help.

A payment POST times out. Retrying blindly may charge twice: the payment might already have succeeded.

Put dependency-specific policies around the outbound HTTP call. Keep the total deadline across attempts, not just a fresh timeout for each retry.

Use delays with jitter, meaning small random variation, so callers do not all retry together. Respect useful Retry-After guidance.

A breaker is not a retry loop, and retries do not add server capacity.

74\. How should request cancellation propagate?

When a caller disconnects, ASP.NET Core can signal RequestAborted. Endpoint CancellationToken parameters bind to that request token.

📝 Example:
Pass the token from the handler into:
`await db.SaveChangesAsync(cancellationToken);`
and into HttpClient calls.

Those operations must support cancellation. A token is a request to stop, not a command that kills execution.

Cancellation does not undo a committed order or prove that a remote payment failed. A lost response may still require an idempotent retry to learn the outcome.

75\. Why use WebApplicationFactory for integration tests?

A unit test checks a small piece. An integration test checks pieces working together.

WebApplicationFactory starts your app in a test host, so requests pass through routing, middleware, binding, authorization, and the handler.

📝 Example:
POST an invalid order → 400 and no database row.
POST a valid order → 201 and a saved row.
Repeat its key → same order, not another row.

Also test missing identity, missing permission, and access to another caller's order.

Our checks use SQLite and a test-only identity handler. That proves policy behavior, not real JWT signature validation.

Test real token validation and your production database separately. An in-process test host does not reproduce a reverse proxy or production load.

📌 Interview takeaway:
"I bound dependency calls, make retries safe, propagate cancellation, and test outcomes through the HTTP pipeline."

Examples and optional deeper explanations, questions 073-075:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Rest_Endpoint.md

#AspNetCore #DotNet #REST
