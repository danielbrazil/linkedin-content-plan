# Designing a production-ready REST endpoint | Part 1/4

A customer clicks "Place order." The server saves it, but the response never reaches their screen.

They try again. Should that create another order?

64\. Which HTTP method and status code should you use?

HTTP methods describe the operation:
• GET reads.
• POST commonly creates a resource or submits work.
• PUT creates or replaces the state at a known address.
• PATCH applies a partial change.
• DELETE removes the resource's association with its address.

📝 Example:
POST /api/v1/orders
{"productId":1,"quantity":2}

If creation succeeds, return 201 Created and a Location header pointing to the new order.

Use 400 for bad input, 404 when a resource cannot be found, and 409 for a conflict with current state. Status codes should explain the outcome, not always say 200.

65\. What is idempotency, and why use a key?

An idempotent operation has the same intended effect when the same request is repeated.

📝 Example:
Send an Idempotency-Key header with the order request. If the response is lost, retry the same input with the same key.

The server stores that key and the order together. It returns the stored result instead of creating another order.

Same key, different input? Reject it, for example with 409.

The server must enforce this safely when requests arrive together, usually with a database uniqueness rule. A quick "does this key exist?" check alone is not enough.

Scope keys to the caller and operation. An idempotency key is not a password or permission check.

66\. How should API errors be returned?

Use a consistent shape instead of unrelated strings from every endpoint.

Problem Details is a standard JSON error format. Validation responses can include named field errors.

📝 Example:
Quantity is 0 → 400 with a quantity error.
An unexpected failure → centralized error handling logs the details and sends a safe 500 response.

Do not send stack traces, database details, or secrets to the caller.

📌 Interview takeaway:
"I express outcomes with HTTP, make retries safe with a stored key and database rule, and return predictable errors."

➡️ Next: keeping the API contract clear as it grows.

Examples and optional deeper explanations, questions 064-066:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Rest_Endpoint.md

#AspNetCore #DotNet #REST
