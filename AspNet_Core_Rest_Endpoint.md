# Designing a production-ready REST endpoint

A customer clicks "Place order." Your API saves the order, but the network drops the response.

The customer tries again.

A reliable design needs more than a POST method that works once. It needs a clear contract, safe retries, predictable errors, ownership checks, controlled traffic, and tests.

This article builds those ideas around a small order API. **Production-ready** describes the decisions we need to address, not a claim that the teaching application is a deployable commerce platform.

**REST**, Representational State Transfer, is an architectural style. Here we focus on practical HTTP resource design rather than claiming that URL naming alone satisfies every REST constraint.

Each answer starts with a plain-language explanation, a familiar example, and a short interview answer. **Going deeper** sections contain optional details and trade-offs.

This continues [ASP.NET Core: from request to response](./AspNet_Core_Request_Response.md) and covers questions 064-075 in the [calendar](./LinkedIn_Content_Plan.md).

## Reading map

| Post | Questions | Focus |
|---|---|---|
| [Part 1/4: Requests, retries, and errors](./AspNet_Core_Rest_Endpoint_Part_1_of_4_Requests_Retries_And_Errors.md) | 064-066 | Express outcomes and prevent duplicate orders |
| [Part 2/4: Contracts, versions, and pages](./AspNet_Core_Rest_Endpoint_Part_2_of_4_Contracts_Versions_And_Pages.md) | 067-069 | Keep the API predictable as it grows |
| [Part 3/4: Caching, limits, and HTTP clients](./AspNet_Core_Rest_Endpoint_Part_3_of_4_Caching_Limits_And_Http_Clients.md) | 070-072 | Reduce unnecessary transfers and protect capacity |
| [Part 4/4: Resilience, cancellation, and tests](./AspNet_Core_Rest_Endpoint_Part_4_of_4_Resilience_Cancellation_And_Tests.md) | 073-075 | Bound waits and verify the whole request |

## The example in one minute

The caller submits:

```http
POST /api/v1/orders
Idempotency-Key: checkout-123
Content-Type: application/json

{"productId":1,"quantity":2}
```

After authorization and validation, the server stores an order and responds:

```http
HTTP/1.1 201 Created
Location: /api/v1/orders/7ba2a3c8-9395-4e8f-a453-b1716e7c6516
```

```json
{
  "id": "7ba2a3c8-9395-4e8f-a453-b1716e7c6516",
  "productId": 1,
  "quantity": 2,
  "total": 25
}
```

The ID is illustrative. The owner comes from the authenticated identity, and the total comes from the server's demonstration price, not from caller-supplied values.

Repeat the same key and accepted input for this caller, and the API returns the stored order instead of inserting another.

## 064. How should HTTP methods and status codes express resource operations?

**The simple answer.** Use the method to express what the caller intends and the status code to describe the outcome.

| Method | Common intention | Important distinction |
|---|---|---|
| GET | Read a representation | Should not request a business state change |
| POST | Submit work or create under a collection | Not inherently safe to repeat |
| PUT | Create or replace state at a known target | Defined to have idempotent semantics |
| PATCH | Apply a partial modification | Whether repetition is safe depends on the operation |
| DELETE | Remove the resource's association with its address | Repeating it need not return the same status |

**Example.** POST /api/v1/orders creates an order. Return 201 Created with a Location identifying it. A later GET reads that address.

Common outcomes:

- 200: successful response with a representation.
- 201: a resource was created.
- 202: work was accepted but is not complete; define how to track its outcome.
- 204: successful response with no body.
- 400: malformed or unacceptable request input under the API's contract.
- 401: no acceptable authenticated identity.
- 403: authenticated but not allowed.
- 404: no matching resource, or deliberate concealment of an inaccessible one.
- 409: conflict with current state.
- 412: a supplied precondition failed.
- 429: too many requests.
- 500: unexpected server failure.

**Interview answer.** "I align methods with resource semantics and use meaningful status codes, rather than returning 200 with an error string."

### Going deeper: effects and response codes are different

Safe methods, including GET, should not ask for a business state change, although infrastructure can still log requests.

Idempotent does not mean every response must be identical. A first DELETE may return 204 and a repeat 404 while the intended state is still "no resource at this address."

Some APIs use 422 for well-formed content that fails semantic validation. Choose and document a consistent policy. Our example uses 400 for input validation.

Do not return 202 just because your C# handler is async. It means the business operation is not finished when the response is sent.

## 065. What makes an operation idempotent, and when should an API use idempotency keys?

**The simple answer.** Repeating an idempotent operation has the same intended effect. An idempotency key lets the server recognize a repeated logical operation.

**Example.** checkout-123 identifies one order submission. The first request saves the order. The retry returns that order. Reusing the key with a different quantity returns 409.

**Interview answer.** "For a retryable creation operation, I scope a key to the caller and operation, store it with the result, compare the accepted input, and enforce uniqueness in the database."

### Going deeper: the race matters

This sequence is unsafe by itself:

```text
Request A: key is absent
Request B: key is absent
Both insert an order
```

Our SQLite example has a unique index on Owner plus IdempotencyKey. The order and key are fields in the same inserted row, so one database write records both.

The handler:

1. Validates the request and key.
2. Checks for a previously stored order for this owner and key.
3. If present, compares ProductId and Quantity and returns the stored result or 409.
4. Otherwise, attempts the insert.
5. If SQLite reports a uniqueness conflict, rereads that owner-key pair and applies the same comparison. Unrelated failures propagate.

The reread is important: an early existence check is not the final concurrency guard. The checks submit three same-key requests concurrently and verify one stored order.

This sample has one creation operation and one trusted JWT issuer configured. A broader design must scope keys across endpoints, tenants, and issuers appropriately. Define key retention, replay duration, accepted input canonicalization, and treatment of in-progress/failed operations.

The sample retains keys indefinitely with their orders, compares its two accepted request fields, and replays successful creations as 201 with the same Location and body. It does not cache every validation error.

A payment or message publication is a separate side effect. A local database key does not make an external charge atomic with the order. That may require provider-side idempotency, an outbox, reconciliation, and safe retries. Idempotency keys are not authorization credentials.

## 066. How should centralized exception handling and Problem Details standardize API errors?

**The simple answer.** Use a consistent public error shape, with detailed diagnostics kept in server-side logs.

**Problem Details** is a standard JSON error format describing an HTTP failure. Validation extensions can include field-specific errors.

**Example.** Quantity 0 produces a 400 response containing a quantity error. An unexpected database failure should not send SQL, connection strings, or stack traces to the caller.

Our API registers AddProblemDetails and uses UseExceptionHandler for unhandled failures. Expected input errors return ValidationProblem; key conflicts return a 409 Problem response.

**Interview answer.** "I distinguish expected failures from exceptions, standardize public error responses, and log enough context to investigate without exposing internals."

### Going deeper: registering a formatter does not format every response

Not every framework-generated status, empty NotFound response, authentication challenge, or malformed-binding result automatically becomes a Problem Details body. Configure and test the responses that your contract promises.

Our tests assert the validation response shape and named errors. The sample does not claim that every possible 4xx response has an identical body.

Use correct status codes and a safe correlation identifier where appropriate. Do not broadly catch errors and return an empty successful list. Once response data has started streaming, replacing the response with Problem Details may be impossible.

## 067. Why should request and response DTOs be separated from persistence entities?

**The simple answer.** The API contract and database model have different responsibilities.

**DTO** means Data Transfer Object: a type describing what crosses the boundary.

**Example.** These types are copied from [OrderEndpoints.cs](./examples/RestOrders/OrderEndpoints.cs):

```csharp
public sealed record CreateOrder(int ProductId, int Quantity);
public sealed record OrderResponse(Guid Id, int ProductId, int Quantity, decimal Total);
```

The entity also contains the owner, idempotency key, sequence, and version. Those internal fields are not writable through CreateOrder.

**Interview answer.** "DTOs let me control accepted and returned fields independently of storage. I validate and map them deliberately instead of binding a persistence entity directly."

### Going deeper: a DTO is a boundary, not a complete access policy

Omitting Owner from the input prevents that field being assigned through this contract, but the server must still derive and enforce ownership correctly.

The sample filters every order read and list by the authenticated subject. Another caller receives 404 for an order they do not own.

Unknown JSON fields may be ignored under serializer defaults; they do not become mapped entity fields. Decide whether strict rejection is needed for your contract.

## 068. How should an API evolve its contract and handle versioning?

**The simple answer.** Avoid breaking existing callers unexpectedly. Introduce a deliberate migration path when a change cannot be compatible.

**Example.** A frontend reads total as a JSON number. Renaming it or returning a string can break the frontend even when the URL is unchanged.

A version such as /api/v2/orders can allow a transition while v1 remains supported for an announced period.

**Interview answer.** "I prefer compatible changes, test consumer assumptions, and use an explicit version and retirement plan for breaking changes."

### Going deeper: more than URL design

Route, header, and media-type versioning are possible strategies. Choose one the team and tooling can operate consistently.

Adding a field is often compatible but can still break strict clients. Changing validation, defaults, ordering, error codes, or enum values can also change the contract.

The teaching API exposes only /api/v1/orders. Tests verify that v2 is not silently treated as v1. This is explicit route naming, not a complete version negotiation or deprecation system.

## 069. How should an API expose filtering, sorting, and pagination?

**The simple answer.** Offer supported query options, apply them at the data source, and return a bounded page in a stable order.

**Example.**

```http
GET /api/v1/orders?productId=1&limit=10&after=25
```

Our API returns this caller's matching orders after sequence 25. It sorts by the unique increasing Sequence and fetches at most the page size plus one to detect another page.

The response contains Items and NextAfter. Use NextAfter as the next request's after value, retaining the same filter. Null means no next page was found.

**Interview answer.** "I allowlist supported filters and sorts, cap page sizes, apply them in the database, and use a stable order with a unique tie-breaker."

### Going deeper: cursor pagination is not a frozen snapshot

Offset pagination uses a skip count; keyset/cursor pagination continues after a position. Keysets can avoid large offsets and some shifting-page problems, but concurrent inserts, deletes, or changes can still affect traversal.

Our cursor is a plain sequence value, not an opaque signed token. It grants no access: ownership filtering remains mandatory. A production cursor can encode ordering and filters and reject inconsistent use.

The sample supports one fixed sort, an optional productId filter, a default size of 10, and maximum 50. It does not accept arbitrary SQL or arbitrary sort expressions.

## 070. When are ETags and conditional requests useful for caching or concurrency control?

**The simple answer.** An ETag is a server-provided validator for a representation. A caller can use it to avoid receiving unchanged data or to protect an update from stale state.

**Example: conditional read.**

```text
GET order → 200 + body + ETag: "v1"
Next GET with If-None-Match: "v1"
Unchanged representation → 304 + no body
```

The client uses its stored representation. An ETag need not be a hash.

**Example: conditional update.** If-Match: "v1" asks the server to apply a change only if v1 is still current. A failed precondition normally returns 412.

**Interview answer.** "I use If-None-Match for cache validation and If-Match for optimistic concurrency, with a database-atomic version check for updates."

### Going deeper: a validator must track the representation

Strong validators must change when the selected representation changes. Weak validators can describe semantic equivalence and use a W/ prefix.

The sample assigns a version to each immutable order, returns a quoted ETag, and implements conditional GET, including weak comparison, lists, and wildcard. It rejects malformed conditional headers. It has no update endpoint and does not claim to implement If-Match writes.

If adding updates, every relevant representation change must update the validator. Compare the expected version as part of the database write, not through an unprotected read-then-save sequence.

Authorize before returning 304; a matching tag does not grant access. The sample marks responses private, no-cache: a private cache may store them but must revalidate before reuse. No-cache does not mean no-store.

## 071. How should rate limiting protect an endpoint without blocking legitimate clients unnecessarily?

**The simple answer.** Choose a traffic budget that matches workload and capacity, and keep one noisy caller from consuming everyone's budget.

**Example.** Our local fixed-window limiter allows 100 requests per authenticated subject per minute. Excess requests return 429 with Retry-After when supplied by the limiter.

The number is a teaching setting, not a production recommendation.

**Interview answer.** "I choose a suitable partition and algorithm, measure legitimate traffic, return useful throttling guidance, and distinguish rate limits from concurrency limits."

### Going deeper: algorithms and scope matter

Fixed windows can allow bursts across a boundary. Sliding windows and token buckets have different burst characteristics; concurrency limiters cap active work rather than requests over time.

IP-only partitions can group unrelated users behind a shared network. User-only limits need a trusted identity and are not protection for every anonymous or pre-authentication path.

The sample's limiter runs after authentication and authorization. Its buckets live in one process, not across replicas. An edge limiter, global quota, or shared design may also be required.

Bound queues rather than allowing unlimited waiting. Watch rejection rates, latency, queue depth, and whether retries amplify load. Do not automatically retry every 429 immediately.

## 072. Why use IHttpClientFactory, and how should outbound HTTP failures be handled?

**The simple answer.** The factory creates configured HttpClient instances while managing reuse and lifetime of their underlying handlers and connections.

**Example.** [CatalogClient.cs](./examples/RestOrders/CatalogClient.cs) demonstrates an outbound catalog lookup. This is a separate example component; order creation uses a fixed local price and does not call a real catalog.

```csharp
public async Task<CatalogProduct?> FindAsync(int id, CancellationToken token)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
    using HttpResponseMessage response = await http.GetAsync(
        $"products/{id}", HttpCompletionOption.ResponseHeadersRead, token);
    if (response.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<CatalogProduct>(token)
        ?? throw new InvalidDataException("The catalog returned an empty product.");
}
```

It maps a documented 404 to absence. It does not map a 503 to "not found." The response is disposed and cancellation is passed onward.

**Interview answer.** "I centralize HTTP client configuration and connection management, then preserve meaningful distinctions between absence, failure, and cancellation."

### Going deeper: the factory is not a reliability policy by itself

Named or typed clients can centralize addresses, headers, timeouts, logging, and handlers. A correctly configured long-lived HttpClient with an appropriate connection lifetime is another valid approach.

Do not create a new unmanaged handler for every request. Avoid capturing a typed client indefinitely in a singleton without understanding its intended lifetime and DNS behavior.

Our typed client has an illustrative catalog.example base address and a three-second HttpClient timeout. It uses ResponseHeadersRead, so that timeout covers the request through receipt of headers, not the entire later body consumption. The caller must pass a suitable deadline token for the full operation.

Production clients also need payload-size limits, schema validation, and dependency-specific failure mapping. Never log access tokens or sensitive payloads by default.

## 073. Where should timeouts, retries, and circuit breakers be applied to an outbound dependency?

**The simple answer.** Apply dependency-specific policies around the outbound call, with an overall budget for the operation.

| Tool | Purpose | Everyday example |
|---|---|---|
| Timeout | Bound how long to wait | Stop waiting for the catalog after the allowed budget |
| Retry | Repeat a safe operation after a temporary failure | A transient failure during an independent GET |
| Circuit breaker | Temporarily reject calls when recent failures indicate trouble | Stop hammering an unavailable dependency |

**Example.** A failed catalog read might be retryable. A timed-out payment is not automatically safe to repeat because its result is unknown.

**Interview answer.** "I bound attempts and total duration, retry only safe transient failures, and use a breaker to reduce repeated pressure on an unhealthy dependency."

### Going deeper: policies compose, and mistakes multiply

Use established resilience components, such as supported .NET HTTP resilience handlers, rather than scattered handwritten retry loops.

Plan total timeout, per-attempt timeout, retry count, delay, jitter, and cancellation together. A breaker samples failures, opens to reject calls, and later permits probes; it is not a retry mechanism.

Respect applicable Retry-After guidance. Do not retry validation errors as transient failures. Side-effecting operations require an idempotency contract, including any external payment provider.

Avoid stacking independent retry layers without calculating amplification. Three attempts in each of two layers can mean nine dependency calls.

The sample does not install a retry or circuit-breaker policy. This is intentional: it demonstrates failure propagation and cancellation without silently retrying writes or claiming production resilience.

## 074. How should request-abort cancellation propagate to database and HTTP calls?

**The simple answer.** Pass the request token through the call chain to work that should stop with that request.

**Example.** ASP.NET Core binds a Minimal API CancellationToken parameter to HttpContext.RequestAborted. The sample passes it to EF Core query/materialization/save operations. The catalog component passes its token into both the HTTP request and JSON read.

This EF Core usage fragment assumes a configured context:

```csharp
await db.SaveChangesAsync(cancellationToken);
```

**Interview answer.** "I propagate request cancellation into participating I/O instead of just checking it at the handler. Cancellation is cooperative and cannot prove that a side effect did not happen."

### Going deeper: cancellation is not a transaction rollback signal

The caller may disconnect just after an order commits. The response is lost, but the order exists. A same-key retry can recover the result.

Providers vary in how promptly they honor cancellation. Passing a token expresses intent; it is not proof of interruption or rollback.

If work must continue reliably beyond the request, design a durable handoff with an explicit acknowledgment and processing policy. Do not ignore cancellation by starting a fire-and-forget task with request-scoped dependencies.

The outbound cancellation check uses a controlled HTTP handler, not a real remote service. Database tokens are present in the implementation; the suite does not claim to verify cancellation inside a real SQL Server/PostgreSQL operation or at every SQLite commit boundary.

## 075. How would you integration-test routing, authentication, validation, and persistence with WebApplicationFactory?

**The simple answer.** Send HTTP requests through a test host, then verify both the public response and stored effects.

**WebApplicationFactory** creates an in-process host for your application, including routing, middleware, binding, authorization, and dependency injection.

**Example.** Our checks verify:

- Missing identity → 401.
- Identity without permission → 403.
- Invalid quantity → 400, named validation error, and no row.
- Valid order → 201, Location, server-calculated total, and one row.
- Same-key retry → same order; changed accepted input → 409.
- Concurrent same-key calls → one row.
- Another owner cannot read the order.
- A second host can read the stored order.

**Interview answer.** "I exercise the HTTP pipeline and assert database effects, with isolated state and a clear statement of which infrastructure is real and which is substituted."

### Going deeper: test doubles have boundaries

The checks use a temporary file-backed SQLite database, not EF's non-relational in-memory provider. They verify an actual unique constraint and persisted data. SQLite behavior still differs from SQL Server and PostgreSQL; test against the intended production engine too.

A test-only identity handler is registered only by the check project's factory. It accepts X-Test-User and X-Test-Permission headers there so tests can express policy cases. The serving project does not register that handler or accept those headers as authentication.

These tests verify authorization integration, not real JWT signature/issuer/audience validation. Add separate tests using trusted test keys or a controlled identity provider for that boundary.

TestServer is not Kestrel, a reverse proxy, real network transport, TLS termination, or a load test. Pair these checks with deployment-level tests where needed.

## Optional: run the checks

Use a **.NET 10 SDK or newer capable of targeting .NET 10**. The first run restores the package versions in the project manifests.

From the repository folder:

```powershell
dotnet run --project .\examples\RestOrders.Checks\RestOrders.Checks.csproj --configuration Release
```

The checks create isolated temporary SQLite files and delete them when the factories are disposed. No production database, credentials, token service, or real catalog is contacted.

The intentionally rejected duplicate insert can produce an expected EF Core error log while the corresponding check passes. A failed assertion throws; successful checks print PASS.

| File | Purpose |
|---|---|
| [API startup](./examples/RestOrders/Program.cs) | JWT bearer registration, policy, middleware, limiter, and v1 routes |
| [Order endpoints and storage](./examples/RestOrders/OrderEndpoints.cs) | DTOs, ownership, SQLite uniqueness, replay, conditional GET, and paging |
| [Catalog client](./examples/RestOrders/CatalogClient.cs) | Outbound failure and cancellation example |
| [Integration checks](./examples/RestOrders.Checks/Program.cs) | WebApplicationFactory, SQLite, test-only identity, and assertions |
| [API project](./examples/RestOrders/RestOrders.csproj) | Web SDK and dependency versions |
| [Check project](./examples/RestOrders.Checks/RestOrders.Checks.csproj) | Console runner and testing package |
| [Example solution](./examples/RestOrders.slnx) | Project grouping and test-host content-root discovery |

The serving API intentionally has no ready-to-use development login or credentials. Running it outside the test factory requires a configured ConnectionStrings:Orders, trusted Identity:Authority and Identity:Audience, an initialized database schema, TLS/hosting decisions, and a real access token with the required claims.

The tests use EnsureCreated for isolated databases only. They are not a production schema migration or startup-provisioning strategy.

## Executable checks and limitations

All 20 checks passed locally in Release configuration. They cover route versions, access policy, ownership, validation, persistence, replay, a database uniqueness rule, concurrent retry behavior, ETag comparisons, bounded pages, rate limits, and controlled outbound errors/cancellation.

The order example has one product, one currency-free demonstration total, and no inventory, payments, fulfillment, migrations, retention job, ETag-protected update, real catalog integration, retry handler, or circuit breaker. A persisted order is not a completed purchase.

A complete deployment additionally needs deliberate observability, payload limits, operational secret handling, database recovery, distributed capacity controls, dependency contracts, security tests, and service-specific behavior. This article teaches those decisions incrementally rather than hiding them behind a "production-ready" label.

## Interview recap

| Question | Practical answer | Watch out for |
|---|---|---|
| How should HTTP methods and status codes express resource operations? | State the intention and outcome clearly | Async C# does not imply 202; errors should not look successful |
| What makes an operation idempotent, and when should an API use idempotency keys? | Repeat a logical operation without another intended effect | A pre-insert lookup alone is not a concurrency guard |
| How should centralized exception handling and Problem Details standardize API errors? | Predictable safe public errors and detailed server diagnostics | Not every status automatically gets a Problem Details body |
| Why should request and response DTOs be separated from persistence entities? | Explicit accepted and returned fields | DTOs still need mapping, validation, and authorization |
| How should an API evolve its contract and handle versioning? | Compatible changes first; deliberate migration for breaking changes | A version number does not create compatibility |
| How should an API expose filtering, sorting, and pagination? | Allowlisted options, stable order, capped database-side pages | Cursors do not freeze changing data |
| When are ETags and conditional requests useful for caching or concurrency control? | Validate cached reads or protect writes from stale state | If-Match writes require an atomic database check |
| How should rate limiting protect an endpoint without blocking legitimate clients unnecessarily? | Partition and measure appropriate budgets | Local limits are not automatically distributed |
| Why use IHttpClientFactory, and how should outbound HTTP failures be handled? | Configured clients with managed handlers and meaningful failures | A timeout after headers needs separate body-budget care |
| Where should timeouts, retries, and circuit breakers be applied to an outbound dependency? | Around the dependency under a total budget | Retrying side effects can duplicate them |
| How should request-abort cancellation propagate to database and HTTP calls? | Pass tokens into participating work | Cancellation is not proof of rollback |
| How would you integration-test routing, authentication, validation, and persistence with WebApplicationFactory? | HTTP outcomes plus isolated database assertions | A test identity and SQLite do not prove real JWT or production-engine behavior |

## References

- [RFC 9110: HTTP semantics](https://www.rfc-editor.org/rfc/rfc9110.html)
- [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html)
- [Microsoft Learn: Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [Microsoft Learn: EF Core indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)
- [Microsoft Learn: EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [Microsoft Learn: EF Core pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination)
- [Microsoft Learn: Rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft Learn: IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [Microsoft Learn: HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [Microsoft Learn: Integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [Microsoft Learn: Choosing a database testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy)
