# ASP.NET Core: from request to response | Part 4/4

A token, a browser setting, and a background worker solve different problems.

Here is a simple way to tell them apart.

61\. How should an API validate a JWT and enforce permissions?

A JWT, JSON Web Token, can carry claims: statements such as who issued it, who it identifies, and its permissions.

Reading its contents is not proof that it is trustworthy.

📝 Example:
A caller sends an access token to change a product price.

The API's configured token handler must check its signature against trusted keys, issuer, intended API audience, and validity period.

Then an authorization policy checks the required permission. A policy is a named access rule.

Use an established identity provider and supported JWT bearer middleware. Do not accept a token just because its JSON looks right, or use an ID token as an API access token.

62\. What does CORS do?

CORS means Cross-Origin Resource Sharing. It controls whether a browser lets one origin read a response from another.

An origin is the scheme, host, and port, such as https://shop.example.

📝 Example:
Your React app runs at one origin; your API runs at another. The API can allow that browser origin through a CORS policy.

For some requests, the browser first asks permission with a preflight request.

CORS is not a login check or firewall. A script outside a browser can still call the API, and some disallowed browser requests can still reach it.

Keep authentication and authorization. CORS is not a replacement for protection against unwanted cookie-authenticated actions either.

63\. How should BackgroundService use scoped services and stop?

A BackgroundService runs work outside individual HTTP requests.

📝 Example:
A worker updates a catalog every minute.

The worker is long-lived. A database context should not be kept in its constructor for the whole application lifetime.

Instead, create a DI scope for each work unit, resolve the scoped service inside it, await the work, then dispose the scope.

Pass the shutdown token to waits and calls so they can stop when the app shuts down.

Do not start a task and forget it. The host needs to track its completion and failures.

Graceful shutdown has a time limit and is not guaranteed after a crash. Important unfinished jobs need persistent storage, not just a worker in memory.

📌 Interview takeaway:
"I validate the token before trusting claims, use CORS only for browser sharing rules, and give background work its own scope and cancellation."

Examples and optional deeper explanations, questions 061-063:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Request_Response.md

#AspNetCore #DotNet #WebAPI
