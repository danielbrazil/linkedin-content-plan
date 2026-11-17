# ASP.NET Core: from request to response

Imagine a React or Angular screen asking your API for a product:

```http
GET /products/1
```

The response might look simple:

```json
{ "id": 1, "name": "Notebook", "price": 12.50, "currency": "USD" }
```

But several decisions sit between the request and that JSON:

- Which server receives the request?
- Which checks run before your handler?
- Where do its services and settings come from?
- Who may call a protected endpoint?
- What happens to work that runs outside a request?

We will follow that path using small examples. Each numbered answer begins with the practical idea, followed by a short interview answer. **Going deeper** sections are optional on a first read.

An **API**, Application Programming Interface, is how other code interacts with your application. **HTTP** is the request-and-response protocol used here. An **endpoint** is the code selected to handle a particular route and method.

This article covers questions 051-063 in the [calendar](./LinkedIn_Content_Plan.md).

## Reading map

| Post | Questions | Focus |
|---|---|---|
| [Part 1/4: Servers and middleware](./AspNet_Core_Request_Response_Part_1_of_4_Servers_And_Middleware.md) | 051-053 | How a request reaches its handler |
| [Part 2/4: Services and settings](./AspNet_Core_Request_Response_Part_2_of_4_Services_And_Settings.md) | 054-057 | Object lifetimes and configuration |
| [Part 3/4: Endpoints, input, and access](./AspNet_Core_Request_Response_Part_3_of_4_Endpoints_Input_And_Access.md) | 058-060 | Handler style, input rules, identity, and permissions |
| [Part 4/4: Tokens, browsers, and workers](./AspNet_Core_Request_Response_Part_4_of_4_Tokens_Browsers_And_Workers.md) | 061-063 | Token trust, browser sharing, and background work |

## 051. What roles do Kestrel and a reverse proxy play in hosting ASP.NET Core?

**The simple answer.** Kestrel is the web server that accepts HTTP requests for your ASP.NET Core application. A reverse proxy is another server in front of it that forwards requests.

**Example.** A browser might reach your application like this:

```text
Browser → reverse proxy → Kestrel → request pipeline → endpoint
```

The proxy can handle HTTPS connections, route requests, and distribute traffic among several running copies of your app. Kestrel then serves the application.

Kestrel can also face clients directly. A reverse proxy is an architectural choice, not a universal prerequisite.

**Interview answer.** "Kestrel is the application's HTTP server. A reverse proxy can sit in front for traffic routing, TLS termination, and load balancing, depending on deployment."

### Going deeper: trust the deployment boundary deliberately

**TLS termination** means the proxy handles the encrypted client connection. The app must know whether the original request was HTTPS and which client address was observed.

Forwarded headers can carry that information, but configure which proxies or networks are trusted. Do not accept client-supplied forwarding information indiscriminately.

Hosting arrangements differ. For example, IIS supports different hosting models, including an in-process model that does not use Kestrel for that application's request serving. Azure hosting choices also differ; do not memorize one diagram as the only deployment.

Our sample uses Kestrel directly over loopback HTTP. It does not configure a production proxy, public TLS, or forwarded headers.

## 052. How does middleware ordering affect the request pipeline?

**The simple answer.** Middleware is an ordered set of steps around your endpoint. A step can call the next one, perform work before or after it, or stop processing early.

**Example.** Picture two middleware components, A and B:

```text
A before → B before → endpoint → B after → A after
```

This describes normal completion when both call the next step. An exception or an early response can change the path.

An error handler must be placed before the downstream code whose errors it should handle. Authentication should establish identity before authorization checks access.

**Interview answer.** "Middleware order changes behavior because components wrap later components. I put prerequisite steps before the code that depends on them and error handling around the work it should catch."

### Going deeper: do not copy an order without understanding it

A common ordering for an API with the corresponding services configured is:

```text
Trusted forwarded headers, if applicable
Error handling
HTTPS handling, as appropriate for the deployment
Routing
CORS
Authentication
Authorization
Endpoint
```

This is a guide, not a complete production recipe. Static files, branches, caching, rate limiting, and endpoint-specific policies can change requirements.

Minimal hosting can insert some middleware automatically. Explicit calls are useful when their relative position matters.

Our sample adds a response tracking header, then exception handling, routing, and CORS. It has no authentication handler and does not claim that its endpoints are protected.

Use the framework error middleware rather than catching every exception and returning a success-looking response. Once a response has started, replacing it with a new error response may no longer be possible.

## 053. When should cross-cutting behavior use middleware, an MVC filter, or an endpoint filter?

**The simple answer.** Choose the hook closest to the behavior's required scope.

**Cross-cutting** means behavior relevant to several operations, such as logging or access checks.

| Mechanism | Where it fits | Example |
|---|---|---|
| Middleware | Around downstream HTTP processing | Request tracking or centralized errors |
| MVC filter | Inside the controller/action pipeline | A controller action filter using action arguments |
| Endpoint filter | Around selected Minimal API handlers | Check a bound product request before its handler runs |

**Example.** Our API uses middleware to add a tracking header to responses. A ProductValidationFilter checks the preview request's name and price, then calls the handler only when they are acceptable.

**Interview answer.** "I use middleware for HTTP-wide behavior, MVC filters for controller-pipeline hooks, and endpoint filters around Minimal API handlers."

### Going deeper: filters have different stages

MVC has authorization, resource, action, exception, and result filters. They do not all see the same data or run at the same time. For example, an MVC action filter runs after model binding; a resource filter can run earlier.

Endpoint filters can inspect bound handler arguments. Malformed binding can fail before the filter executes. That is why our sample tests malformed JSON separately from a negative price.

Do not make an ad hoc input filter responsible for implementing authentication. Use supported authentication and authorization mechanisms for those checks.

## 054. How do singleton, scoped, and transient dependency-injection lifetimes differ?

**The simple answer.** A lifetime tells the DI container how long to reuse an object.

**Dependency Injection**, DI, means your class receives its dependencies rather than constructing every dependency itself. The **container** holds service registrations and creates/resolves objects according to them.

| Lifetime | Reuse rule | Typical consideration |
|---|---|---|
| Singleton | One instance per container | Shared mutable state needs concurrency protection |
| Scoped | One instance per scope; normally per HTTP request | Useful for request-specific services |
| Transient | A new instance whenever that registration is resolved | Suitable for lightweight independent objects |

**Example.** This registration fragment assumes your ProductService class exists:

```csharp
builder.Services.AddScoped<ProductService>();
```

Within one normal request scope, resolutions of this registration share its instance. Another request normally has a different scope and instance.

**Interview answer.** "I choose lifetimes based on ownership and sharing: singleton per container, scoped per scope, and transient per resolution."

### Going deeper: lifetimes do not grant thread safety

Scoped services can still be used by concurrent operations within one request. A scoped EF Core DbContext is not safe for overlapping database operations.

The container disposes disposable objects it owns when their scope or container ends. Avoid resolving scoped or disposable transient objects from the root provider and accidentally keeping them until application shutdown.

The sample's /lifetimes endpoint returns generated identifiers. Repeated HTTP requests demonstrate the singleton staying the same, scoped objects matching within a request but differing across requests, and transient resolutions differing even within a request.

## 055. What is a captive dependency, and why is injecting a scoped service into a singleton dangerous?

**The simple answer.** A captive dependency is an object retained longer than its intended lifetime.

**Example.** A singleton report service takes a request-scoped database context in its constructor. The singleton survives many requests, but its context was meant for a shorter scope.

With scope validation enabled, the container can reject this lifetime mismatch. Without it, you can get unintended sharing, delayed disposal, and concurrency problems.

**Interview answer.** "A singleton must not capture request-scoped state. If long-lived work needs a scoped service, I create a scope for the work and resolve that service inside it."

### Going deeper: a factory is not a magic exemption

Resolving a scoped object through a factory and then storing it in a singleton can create the same problem. Keep the scope alive only for its intended work unit; do not return an object whose scope is already disposed.

Captivity can also involve a transient held by a singleton when its design assumes short-lived use. The problem is the effective lifetime and sharing, not just registration labels.

Our API enables ValidateScopes and ValidateOnBuild. A separate check verifies that a singleton capturing a scoped service is rejected; the invalid registration is not part of the serving API.

## 056. How do appsettings.json, environment-specific configuration, environment variables, and secrets interact?

**The simple answer.** Configuration combines sources. Under the standard WebApplication.CreateBuilder application configuration, later sources normally override earlier values for matching keys.

From lower to higher priority:

```text
appsettings.json
appsettings.{Environment}.json
User Secrets in Development, when configured
Environment variables
Command-line arguments
```

**Example.** Our file contains a non-secret setting:

```json
{
  "Catalog": {
    "Currency": "USD"
  }
}
```

The nested configuration key is Catalog:Currency. An environment variable named Catalog__Currency can override it with CAD. Double underscores represent the nesting separator in environment variable names.

**Interview answer.** "I use layered configuration, know which provider wins, and keep secrets out of committed configuration files."

### Going deeper: configuration order is configurable

You can add, remove, or reorder providers. Host settings such as the environment name have their own initialization rules; this application-key example is not a universal rule for all bootstrap settings.

User Secrets are a development convenience, not encrypted production storage. Environment variables are also not automatically a secure vault: access and operational controls still matter. Use deployment-appropriate secret management and avoid logging sensitive configuration.

For invalid required settings, fail explicitly instead of silently choosing a value that makes startup look successful. Our currency configuration is bound to an options class and validated at startup.

The sample loads content from its output directory, where the Web SDK copies its JSON configuration. This keeps its run instructions independent of the shell's working directory.

## 057. When should you use IOptions, IOptionsSnapshot, or IOptionsMonitor?

**The simple answer.** Options let a service receive related settings as a typed object instead of repeatedly looking up string keys.

**Example.** A product response needs a currency setting:

| Interface | Practical behavior | Suitable consumer |
|---|---|---|
| `IOptions<T>` | Caches its value; not automatic live updates | Code that does not need reloads |
| `IOptionsSnapshot<T>` | Caches values per scope, normally a request | A request service wanting a stable view |
| `IOptionsMonitor<T>` | Current values and change notifications | A singleton that needs supported updates |

If a reloadable setting changes from USD to CAD, a snapshot already read in the current request remains unchanged. A new request can see the new value. A monitor can see the changed value when the configuration provider signals a reload.

**Interview answer.** "IOptions is the simple cached view, Snapshot is scoped, and Monitor supports current values and notifications in long-lived services."

### Going deeper: reload is not guaranteed by the interface

The configuration provider must support or signal changes. A process generally does not detect an edited environment variable as a live reload. File providers can support reload, but deployment and file-watching conditions matter.

Snapshots are scoped and should not be constructor-injected into a singleton. Validate required options; do not assume configuration binding verifies business rules.

Our checks explicitly trigger an in-memory provider reload. They verify the distinctions above, not live reload behavior for every operating system, file watcher, or hosting environment.

## 058. What trade-offs distinguish controllers from Minimal APIs?

**The simple answer.** Both define HTTP endpoints. They organize handlers and related conventions differently.

**Example.** This usage fragment maps a Minimal API:

```csharp
app.MapGet("/hello", () => "Hello");
```

Controllers place actions in classes and use MVC's conventions, attributes, and filter pipeline. Minimal APIs map handlers more directly and can organize larger APIs through route groups and extension methods.

**Interview answer.** "I choose based on required features, team conventions, and maintainability. Minimal APIs are not restricted to tiny applications, and controllers do not automatically guarantee better architecture."

### Going deeper: choose features, not slogans

Controllers with ApiController conventions offer established binding and validation behavior. Minimal APIs can provide focused handlers and endpoint filters. Validation support and configuration differ by framework version and selected packages.

Business logic does not need to live inside either a large controller or a large endpoint delegate. Keep handlers understandable and reuse services where responsibilities warrant them.

The sample uses Minimal APIs to expose the request mechanics without introducing an MVC application.

## 059. How do model binding, validation, and serialization shape an API boundary?

**The simple answer.** These are three separate jobs:

1. **Binding:** read route, query, header, or body values into C# values.
2. **Validation:** check whether those values satisfy your rules.
3. **Serialization:** turn response values into a format such as JSON.

**Example.** Our preview endpoint accepts this JSON:

```json
{ "name": "Pencil", "price": 2 }
```

It binds to a ProductRequest. A filter rejects blank names, names above 80 characters, and non-positive prices. The handler returns a ProductPreview, which is serialized to JSON.

The endpoint only previews a product. It does not persist data or claim to create a database record.

**Interview answer.** "Successful parsing is not business validation. I define explicit request and response contracts, validate input, and control what is serialized back."

### Going deeper: different failures occur at different points

- /products/{id:int} uses a route constraint. A non-integer path does not match that route, so the sample returns 404.
- /search?limit=five matches its route but fails integer query binding, producing 400.
- A negative product price can bind successfully as a decimal, but fails our filter's business rule with 400.
- Malformed JSON fails before the filter.

The sample deliberately validates Minimal API input explicitly. It does not assume that declaring a record or adding attributes alone enables automatic validation for every endpoint style.

Use **DTOs**, Data Transfer Objects, to state allowed input and output. A database entity may contain fields the caller must not set, such as internal approval state. DTOs also prevent accidentally exposing internal data when an entity changes.

Limit request sizes and validate nested inputs appropriately in real applications. These examples are not a complete input-security checklist.

## 060. How do authentication and authorization differ?

**The simple answer.** Authentication establishes who is calling. Authorization decides whether that caller may perform this operation.

**Example.** Alice presents a valid access token. The API recognizes her identity, but a price-edit endpoint can still reject her because she lacks the necessary permission.

| Situation | Typical protected API outcome |
|---|---|
| No acceptable identity | 401 Unauthorized |
| Valid identity but insufficient permission | 403 Forbidden |
| Valid identity and required permission | Continue to the handler |

**Interview answer.** "Authentication establishes identity. Authorization checks access. Being signed in does not grant every permission."

### Going deeper: access can depend on the resource

A general "read orders" permission does not automatically grant access to another customer's order. Resource ownership, tenant boundaries, and application policy still matter.

Use supported schemes and authorization policies. Some applications deliberately return 404 rather than disclose the existence of an inaccessible resource; that is an explicit design decision, not a change to the meaning of authentication.

Our runnable product API is intentionally unauthenticated and local-only by default. The identity discussion is explanatory; its HTTP checks do not prove real authentication or authorization behavior.

## 061. How should a JWT-based API validate tokens and enforce policy-based authorization?

**The simple answer.** Validate the token before trusting its contents, then evaluate the required access rule.

A **JWT**, JSON Web Token, can carry **claims**, statements about an identity or permissions. Reading its JSON is not the same as proving it was issued by a trusted authority.

**Example.** A client calls a price-edit endpoint with an access token:

1. Supported JWT bearer authentication validates the signature with trusted signing keys.
2. It verifies the issuer, intended API audience, and validity period.
3. An authorization policy checks the permission required by this endpoint.
4. Resource-specific checks enforce any further ownership or tenant rules.

The **issuer** is the authority that issued the token. The **audience** identifies the intended recipient. A **policy** is a named collection of access requirements.

**Interview answer.** "I configure supported JWT bearer validation against a trusted identity provider, verify issuer, audience, signature, and lifetime, and enforce policies rather than merely decoding claims."

### Going deeper: do not improvise token trust

Use an established identity provider and its recommended configuration. Standard discovery can supply signing keys and support key rotation. Require appropriate HTTPS and trusted metadata; do not disable checks to make a token pass.

Use access tokens intended for this API, not an OpenID Connect ID token intended for a client application. Restrict acceptable algorithms and claim interpretation according to the provider and application contract. A bearer token grants access to whoever possesses it, so do not log raw tokens.

Permission claims vary. A space-separated scope claim must be interpreted as scopes, not tested with an unsafe substring comparison. Map claims deliberately, define the policy, and attach it to the relevant endpoints or a suitable default policy.

JWT payloads are not necessarily encrypted. Signing protects integrity, not confidentiality. Token expiration and revocation also require an explicit design; a valid signature alone does not answer every access question.

The sample deliberately includes no token issuer, JWT handler package, or fake authentication shortcut. A real integration needs provider-specific configuration and dedicated tests for invalid signature, issuer, audience, expiry, missing permission, and record access.

## 062. What problem does CORS solve, and what does it not protect against?

**The simple answer.** CORS controls browser permission to share responses across origins. It does not decide who may use your API.

**CORS** means Cross-Origin Resource Sharing. An **origin** includes scheme, host, and port.

**Example.** A React app at https://shop.example calls an API at https://api.example. The API can allow that browser origin through its CORS policy.

For some requests the browser sends a **preflight**, an OPTIONS request asking whether the actual request is permitted. Other requests can be sent without preflight.

**Interview answer.** "CORS is a browser cross-origin response-sharing mechanism. It is not authentication, authorization, a firewall, or complete CSRF protection."

### Going deeper: no CORS header does not mean no server work

Some disallowed browser requests can still reach and affect the server, even when the browser refuses to expose their response to JavaScript. Non-browser callers are not constrained by browser CORS enforcement.

For cookie-authenticated operations, design protection against **CSRF**, Cross-Site Request Forgery: unwanted authenticated actions triggered through the browser. Do not treat an origin allowlist as the only control.

Allow only required origins, methods, and headers. Credentialed CORS needs additional care and cannot use a wildcard origin with credentials.

Our sample allows GET requests from https://learning.example. Checks verify allowed-origin headers, preflight behavior, and the fact that a disallowed origin can still receive a server-side 200 without an allow-origin header. HttpClient checks the headers; it does not simulate browser enforcement.

## 063. How should BackgroundService manage scoped dependencies and graceful shutdown?

**The simple answer.** A background worker lives outside an individual HTTP request. Give each work unit its own DI scope and pass shutdown cancellation into the work.

**Example.** A catalog worker wakes once a minute. It should not hold a request-scoped database context in its constructor for the application's whole lifetime.

This class is copied from [CatalogWorker.cs](./examples/AspNetRequestPipeline/CatalogWorker.cs):

```csharp
public sealed class CatalogWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<CatalogJob>();
            await job.RunAsync(stoppingToken);
        }
    }
}
```

The scope is disposed after each awaited job, including exceptional exits. No next job starts until the current one finishes. The demo CatalogJob only checks cancellation and logs that it ran; it does not pretend to synchronize a real catalog.

**Interview answer.** "I create a scope per work unit, await its work, propagate the stopping token, and let the host observe completion and failures."

### Going deeper: graceful shutdown is not durable delivery

Register the worker with AddHostedService. Hosted workers are long-lived; inject IServiceScopeFactory rather than capturing a scoped dependency in the worker constructor.

The stopping token is signaled during shutdown. Pass it to delays, queue reads, and I/O. StopAsync has a shutdown budget; cancellation cannot forcibly stop code that ignores it.

Do not start untracked tasks or swallow all exceptions. Define whether failures stop the host or are handled through a deliberate, logged retry policy. A crash or forceful termination may skip cleanup entirely.

Important unfinished work may need durable storage, acknowledgments, and safe retries. BackgroundService by itself is not a durable job system.

The sample starts this worker through the host. A focused check creates another instance and confirms it stops while waiting for its first timer tick. It does not claim to test a real database job, crash recovery, or processing under load.

## Optional: run the local API and checks

Use the **.NET 10 SDK**, which supplies the compiler and command-line tools. No external packages, database, identity provider, or cloud account are required.

From the repository folder, run the self-checking mode:

```powershell
dotnet run --project .\examples\AspNetRequestPipeline\AspNetRequestPipeline.csproj --configuration Release -- --check
```

The checks start Kestrel on an operating-system-selected loopback port, send real HTTP requests, and stop the app before returning. A deliberate /failure request produces an expected server error log while verifying the safe error response. A failed assertion throws; successful checks print PASS.

To explore manually:

```powershell
dotnet run --project .\examples\AspNetRequestPipeline\AspNetRequestPipeline.csproj --configuration Release
```

The default address is http://127.0.0.1:5085. In another terminal:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5085/products/1'
Invoke-RestMethod 'http://127.0.0.1:5085/lifetimes'
Invoke-RestMethod 'http://127.0.0.1:5085/products/preview' -Method Post -ContentType 'application/json' -Body '{"name":"Pencil","price":2}'
```

Press Ctrl+C in the server terminal to stop it. Do not expose this teaching app as a protected public API: it deliberately has no authentication, persistence, TLS configuration, or production hosting policy.

| File | Purpose |
|---|---|
| [Program.cs](./examples/AspNetRequestPipeline/Program.cs) | Start normal serving or self-check mode |
| [DemoApi.cs](./examples/AspNetRequestPipeline/DemoApi.cs) | Middleware, DI, settings, endpoints, and input filter |
| [appsettings.json](./examples/AspNetRequestPipeline/appsettings.json) | Non-secret currency setting |
| [CatalogWorker.cs](./examples/AspNetRequestPipeline/CatalogWorker.cs) | Scoped background-work structure |
| [PipelineChecks.cs](./examples/AspNetRequestPipeline/PipelineChecks.cs) | HTTP, configuration, lifetime, and shutdown checks |
| [Project settings](./examples/AspNetRequestPipeline/AspNetRequestPipeline.csproj) | .NET 10 Web SDK, nullable analysis, and warnings as errors |

## Executable checks and limitations

| Area | What the checks verify |
|---|---|
| Serving | Product JSON, configured currency, and missing routes |
| Binding | Integer query success; malformed and missing values rejected |
| Validation | Invalid name/price; malformed JSON; exact 80/81-character boundary |
| Serialization | A valid request becomes the expected preview response |
| Middleware | The endpoint observes earlier middleware; a response header is added |
| Errors | An unhandled failure returns Problem Details without exception text |
| DI | Singleton reuse, per-request scopes, separate transient resolutions, and captive-scope rejection |
| Configuration | Later providers override matching keys; invalid options fail |
| Options | Cached, scoped, and monitored values differ after an explicit reload |
| CORS | Allowed origin, disallowed origin without an allow header, and preflight |
| Shutdown | A worker waiting for its next tick stops when requested |

Validation: all 21 checks passed locally on Windows with .NET 10 in Release configuration.

The tests do not establish production security or performance. There is no real reverse proxy, browser, controller implementation, JWT integration, database, secret store, distributed worker, or load test. Authentication and authorization remain explanatory topics rather than simulated success.

## Interview recap

| Question | Practical answer | Watch out for |
|---|---|---|
| What roles do Kestrel and a reverse proxy play in hosting ASP.NET Core? | The web server receives requests; a proxy can route traffic in front | Deployment and trusted forwarding rules differ |
| How does middleware ordering affect the request pipeline? | Earlier components wrap later work | Short circuits and errors can change the path |
| When should cross-cutting behavior use middleware, an MVC filter, or an endpoint filter? | Choose by HTTP-wide, controller, or handler scope | Filter stages do not all see the same data |
| How do singleton, scoped, and transient dependency-injection lifetimes differ? | Per container, per scope, or per resolution | Lifetimes do not confer thread safety |
| What is a captive dependency, and why is injecting a scoped service into a singleton dangerous? | A longer-lived object retains shorter-lived state | Factories can still capture the same invalid state |
| How do appsettings.json, environment-specific configuration, environment variables, and secrets interact? | Standard application configuration layers matching keys | Custom providers and host bootstrap settings differ |
| When should you use IOptions, IOptionsSnapshot, or IOptionsMonitor? | Cached value, scoped snapshot, or current monitored values | Reload needs provider support |
| What trade-offs distinguish controllers from Minimal APIs? | Organization, conventions, and required framework features | Neither style fixes architecture automatically |
| How do model binding, validation, and serialization shape an API boundary? | Parse input, enforce rules, and control output | Successful parsing does not imply valid business data |
| How do authentication and authorization differ? | Establish identity, then check access | Signed-in callers still need resource-level permission |
| How should a JWT-based API validate tokens and enforce policy-based authorization? | Validate trusted token properties, then apply access rules | Decoding is not validation |
| What problem does CORS solve, and what does it not protect against? | Browser cross-origin response sharing | Not a login check, firewall, or complete CSRF defense |
| How should BackgroundService manage scoped dependencies and graceful shutdown? | Scope per work unit, await work, and propagate cancellation | Graceful stopping is not crash recovery or durability |

## References

- [Microsoft Learn: Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/?view=aspnetcore-10.0)
- [Microsoft Learn: Proxy servers and load balancers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
- [Microsoft Learn: Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0)
- [Microsoft Learn: MVC filters](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-10.0)
- [Microsoft Learn: Minimal API filters](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0)
- [Microsoft Learn: Dependency injection](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0)
- [Microsoft Learn: Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)
- [Microsoft Learn: Options](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- [Microsoft Learn: Minimal API parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0)
- [Microsoft Learn: Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0)
- [Microsoft Learn: Policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [Microsoft Learn: JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Microsoft Learn: CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0)
- [Microsoft Learn: Scoped services in BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service)
- [Microsoft Learn: Hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
