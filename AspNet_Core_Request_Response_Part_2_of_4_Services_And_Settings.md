# ASP.NET Core: from request to response | Part 2/4

Two requests arrive at your API. Should they share the same service object?

That depends on the object's lifetime.

54\. Singleton, scoped, or transient?

Dependency Injection, or DI, supplies the objects your code needs instead of making it construct them directly.

Register a service with a lifetime:

• Singleton: one instance per DI container.
• Scoped: one instance per scope, normally one HTTP request.
• Transient: a new instance whenever DI resolves that registration.

📝 Example:
`builder.Services.AddScoped<ProductService>();`

In a typical API request, code using that registration shares the same ProductService. The next request gets another instance.

A singleton shared by requests must be safe for concurrent use. Scoped does not mean safe for parallel operations.

55\. What is a captive dependency?

It is a dependency kept longer than its intended lifetime.

📝 Example:
A singleton report service receives a request-scoped database context through its constructor.

The report service lives much longer than one request, so it must not hold that context.

Scope validation can reject this mistake. In background work, create a scope for each work unit instead.

56\. Which configuration value wins?

With the standard WebApplication.CreateBuilder setup, application settings normally layer like this, from lower to higher priority:

appsettings.json → environment-specific JSON → development User Secrets → environment variables → command-line arguments.

Later sources override matching keys; they do not replace all earlier settings.

📝 Example:
appsettings.json says Catalog:Currency = USD.
An environment variable Catalog__Currency = CAD overrides it.

The double underscore represents a nested key.

Keep passwords out of committed JSON. User Secrets are a development convenience, not an encrypted production vault.

57\. IOptions, IOptionsSnapshot, or IOptionsMonitor?

These give your code settings as a typed object instead of repeated string-key lookups.

• IOptions: a cached value, not automatic live updates.
• IOptionsSnapshot: a scoped value, normally stable for that request.
• IOptionsMonitor: current values and change notifications; usable by singletons.

Updates depend on the configuration source supporting reload. Editing settings does not guarantee every running app will detect it.

📌 Interview takeaway:
"I choose service and settings lifetimes deliberately, and validate important configuration before accepting traffic."

➡️ Next: choosing endpoints and checking their input.

Examples and optional deeper explanations, questions 054-057:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Request_Response.md

#AspNetCore #DotNet #DependencyInjection
