using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestOrders;

await using var factory = new OrderFactory();
using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
{
    BaseAddress = new Uri("https://localhost"),
    AllowAutoRedirect = false
});
await using (var scope = factory.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<OrdersDb>().Database.EnsureCreatedAsync();
}
int passed = 0;
await Check("Missing identity returns 401", async () =>
{
    using var response = await Send(HttpMethod.Get, "/api/v1/orders", user: null);
    Require(response.StatusCode == HttpStatusCode.Unauthorized, "Expected 401.");
});
await Check("Authenticated caller without permission gets 403", async () =>
{
    using var response = await Send(HttpMethod.Get, "/api/v1/orders", user: "reader", permission: false);
    Require(response.StatusCode == HttpStatusCode.Forbidden, "Expected 403.");
});
await Check("Validation returns named Problem Details errors without a write", async () =>
{
    using var response = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 0), "invalid");
    Require(response.StatusCode == HttpStatusCode.BadRequest &&
        response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Expected validation problem.");
    using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Require(body.RootElement.GetProperty("errors").TryGetProperty("quantity", out _), "Missing quantity error.");
    Require(await Count() == 0, "Invalid input was stored.");
});
await Check("A missing idempotency key is rejected", async () =>
{
    using var response = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 1));
    Require(response.StatusCode == HttpStatusCode.BadRequest, "Missing key was accepted.");
});
Guid id = Guid.Empty;
string location = "";
await Check("POST returns 201, Location, server price, and persists one order", async () =>
{
    using var response = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 2), "order-a");
    var order = await response.Content.ReadFromJsonAsync<OrderResponse>()
        ?? throw new InvalidOperationException("No response body.");
    id = order.Id;
    location = response.Headers.Location?.ToString() ?? "";
    Require(response.StatusCode == HttpStatusCode.Created && location == $"/api/v1/orders/{id}" &&
        order.Total == 25m && await Count() == 1, "Creation contract differs.");
});
await Check("Same key and input replay the stored result without another row", async () =>
{
    using var response = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 2), "order-a");
    var replay = await response.Content.ReadFromJsonAsync<OrderResponse>();
    Require(response.StatusCode == HttpStatusCode.Created && replay?.Id == id &&
        response.Headers.Location?.ToString() == location && await Count() == 1, "Replay created a duplicate.");
});
await Check("Same key with different input returns 409", async () =>
{
    using var response = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 3), "order-a");
    Require(response.StatusCode == HttpStatusCode.Conflict && await Count() == 1, "Conflicting replay was accepted.");
});
await Check("A unique database constraint enforces owner-key uniqueness", async () =>
{
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDb>();
    db.Orders.Add(new Order
    {
        Id = Guid.NewGuid(), Owner = "alice", IdempotencyKey = "order-a",
        ProductId = 1, Quantity = 2, Total = 25m, Version = Guid.NewGuid()
    });
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
    {
        return;
    }
    throw new InvalidOperationException("Database accepted a duplicate key.");
});
await Check("GET binds an ID, checks ownership, and returns an ETag", async () =>
{
    using var response = await Send(HttpMethod.Get, location);
    Require(response.StatusCode == HttpStatusCode.OK && response.Headers.ETag is not null &&
        response.Headers.CacheControl?.Private == true, "Read contract differs.");
    using var forbiddenRecord = await Send(HttpMethod.Get, location, user: "bob");
    Require(forbiddenRecord.StatusCode == HttpStatusCode.NotFound, "Another owner read the order.");
});
await Check("Matching and weak If-None-Match validators return 304 with no body", async () =>
{
    using var first = await Send(HttpMethod.Get, location);
    string tag = first.Headers.ETag!.ToString();
    foreach (string value in new[] { tag, "W/" + tag, "*", "\"different\", " + tag })
    {
        using var request = Request(HttpMethod.Get, location);
        request.Headers.TryAddWithoutValidation("If-None-Match", value);
        using var response = await client.SendAsync(request);
        Require(response.StatusCode == HttpStatusCode.NotModified &&
            (await response.Content.ReadAsByteArrayAsync()).Length == 0, "Conditional GET differs.");
    }
});
await Check("Different ETag returns the representation", async () =>
{
    using var request = Request(HttpMethod.Get, location);
    request.Headers.TryAddWithoutValidation("If-None-Match", "\"old\"");
    using var response = await client.SendAsync(request);
    Require(response.StatusCode == HttpStatusCode.OK, "Nonmatching ETag suppressed the body.");
});
await Check("Malformed conditional headers return 400", async () =>
{
    using var request = Request(HttpMethod.Get, location);
    request.Headers.TryAddWithoutValidation("If-None-Match", "not-a-quoted-tag");
    using var response = await client.SendAsync(request);
    Require(response.StatusCode == HttpStatusCode.BadRequest, "Malformed validator was accepted.");
});
await Check("Pagination is bounded, stable, filtered, and owner-specific", async () =>
{
    using var created = await Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 1), "order-b");
    Require(created.IsSuccessStatusCode, "Second order failed.");
    using var response = await Send(HttpMethod.Get, "/api/v1/orders?limit=1&productId=1");
    var first = await response.Content.ReadFromJsonAsync<OrderPage>()
        ?? throw new InvalidOperationException("Missing page.");
    Require(first.Items.Count == 1 && first.NextAfter is not null, "Missing page boundary.");
    using var nextResponse = await Send(HttpMethod.Get, $"/api/v1/orders?limit=1&productId=1&after={first.NextAfter}");
    var next = await nextResponse.Content.ReadFromJsonAsync<OrderPage>();
    Require(next?.Items.Count == 1 && next.NextAfter is null &&
        next.Items[0].Id != first.Items[0].Id, "Cursor repeated or skipped the remaining order.");
    using var emptyResponse = await Send(HttpMethod.Get, "/api/v1/orders", user: "bob");
    var empty = await emptyResponse.Content.ReadFromJsonAsync<OrderPage>();
    Require(empty?.Items.Count == 0, "List leaked another owner's orders.");
});
await Check("Malformed queries and out-of-range page sizes return 400", async () =>
{
    foreach (string query in new[] { "limit=abc", "limit=0", "limit=51", "after=-1" })
    {
        using var response = await Send(HttpMethod.Get, "/api/v1/orders?" + query);
        Require(response.StatusCode == HttpStatusCode.BadRequest, "Invalid query accepted.");
    }
});
await Check("An unknown version does not silently select v1", async () =>
{
    using var response = await Send(HttpMethod.Get, "/api/v2/orders");
    Require(response.StatusCode == HttpStatusCode.NotFound, "Unknown version matched.");
});
await Check("A second factory reads the persisted row", async () =>
{
    await using var second = new OrderFactory(factory.DatabasePath, ownsDatabase: false);
    using var secondClient = second.CreateClient();
    using var request = Request(HttpMethod.Get, location);
    using var response = await secondClient.SendAsync(request);
    Require(response.StatusCode == HttpStatusCode.OK, "Persistence was limited to the first host.");
});
await Check("Concurrent same-key requests create only one order", async () =>
{
    int before = await Count();
    HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(1, 3).Select(_ =>
        Send(HttpMethod.Post, "/api/v1/orders", new CreateOrder(1, 1), "concurrent")));
    try
    {
        var bodies = new List<OrderResponse>();
        foreach (HttpResponseMessage response in responses)
        {
            Require(response.StatusCode == HttpStatusCode.Created, "Concurrent replay did not succeed.");
            bodies.Add(await response.Content.ReadFromJsonAsync<OrderResponse>()
                ?? throw new InvalidOperationException("Missing replay body."));
        }
        Require(bodies.Select(order => order.Id).Distinct().Count() == 1 && await Count() == before + 1,
            "Concurrent retry duplicated the order.");
    }
    finally
    {
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }
});
await Check("A dependency 404 means absent, while 503 remains a failure", async () =>
{
    using var missingHttp = new HttpClient(new StubHandler((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))
    {
        BaseAddress = new Uri("https://catalog.example/")
    };
    Require(await new CatalogClient(missingHttp).FindAsync(1, default) is null, "404 did not mean absent.");
    using var failedHttp = new HttpClient(new StubHandler((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
    {
        BaseAddress = new Uri("https://catalog.example/")
    };
    try
    {
        await new CatalogClient(failedHttp).FindAsync(1, default);
    }
    catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.ServiceUnavailable)
    {
        return;
    }
    throw new InvalidOperationException("Dependency failure was hidden as not found.");
});
await Check("Cancellation reaches an active outbound HTTP request", async () =>
{
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    bool cancelled = false;
    using var http = new HttpClient(new StubHandler(async (_, token) =>
    {
        started.SetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Infinite wait completed.");
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
    })) { BaseAddress = new Uri("https://catalog.example/") };
    using var source = new CancellationTokenSource();
    Task pending = new CatalogClient(http).FindAsync(1, source.Token);
    await started.Task;
    source.Cancel();
    try
    {
        await pending;
    }
    catch (OperationCanceledException)
    {
        Require(cancelled, "Handler did not observe cancellation.");
        return;
    }
    throw new InvalidOperationException("Cancelled HTTP request succeeded.");
});
await Check("A per-caller limit returns 429 without consuming another caller's bucket", async () =>
{
    for (int attempt = 0; attempt < 100; attempt++)
    {
        using var response = await Send(HttpMethod.Get, "/api/v1/orders", user: "rate-user");
        Require(response.StatusCode == HttpStatusCode.OK, "Bucket exhausted too early.");
    }
    using var rejected = await Send(HttpMethod.Get, "/api/v1/orders", user: "rate-user");
    Require(rejected.StatusCode == HttpStatusCode.TooManyRequests && rejected.Headers.RetryAfter is not null,
        "Expected 429 and Retry-After.");
    using var independent = await Send(HttpMethod.Get, "/api/v1/orders", user: "another-user");
    Require(independent.StatusCode == HttpStatusCode.OK, "One caller blocked another caller.");
});
Console.WriteLine($"All {passed} checks passed.");

async Task Check(string name, Func<Task> run)
{
    await run().WaitAsync(TimeSpan.FromSeconds(20));
    passed++;
    Console.WriteLine($"PASS: {name}");
}

HttpRequestMessage Request(HttpMethod method, string path, string? user = "alice", bool permission = true)
{
    var request = new HttpRequestMessage(method, path);
    if (user is not null)
    {
        request.Headers.Add("X-Test-User", user);
        if (permission)
        {
            request.Headers.Add("X-Test-Permission", "orders");
        }
    }
    return request;
}

async Task<HttpResponseMessage> Send(
    HttpMethod method, string path, CreateOrder? body = null, string? key = null,
    string? user = "alice", bool permission = true)
{
    using var request = Request(method, path, user, permission);
    if (body is not null)
    {
        request.Content = JsonContent.Create(body);
    }
    if (key is not null)
    {
        request.Headers.Add("Idempotency-Key", key);
    }
    return await client.SendAsync(request);
}

async Task<int> Count()
{
    await using var scope = factory.Services.CreateAsyncScope();
    return await scope.ServiceProvider.GetRequiredService<OrdersDb>().Orders.CountAsync();
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class OrderFactory : WebApplicationFactory<ApiMarker>
{
    private readonly bool ownsDatabase;
    public string DatabasePath { get; }

    public OrderFactory(string? path = null, bool ownsDatabase = true)
    {
        DatabasePath = path ?? Path.Combine(Path.GetTempPath(), $"rest-orders-{Guid.NewGuid():N}.db");
        this.ownsDatabase = ownsDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RestOrders")));
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Orders"] = $"Data Source={DatabasePath};Pooling=False" }));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestOnly";
                options.DefaultChallengeScheme = "TestOnly";
                options.DefaultForbidScheme = "TestOnly";
            }).AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("TestOnly", _ => { });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownsDatabase)
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(DatabasePath + suffix);
            }
        }
    }
}

sealed class TestIdentityHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string user = Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", user) };
        if (Request.Headers["X-Test-Permission"] == "orders")
        {
            claims.Add(new Claim("permission", "orders"));
        }
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        send(request, token);
}
