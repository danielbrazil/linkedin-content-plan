using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace AspNetRequestPipeline;

public static class PipelineChecks
{
    public static async Task RunAsync()
    {
        await using WebApplication app = DemoApi.Build([], checks: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(timeout.Token);
        try
        {
            string address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };
            int passed = 0;
            await Check("Kestrel serves a product with configured currency", async () =>
            {
                var product = await client.GetFromJsonAsync<ProductResponse>("/products/1", timeout.Token);
                Require(product is { Id: 1, Name: "Notebook", Currency: "USD" }, "Product response differs.");
            });
            await Check("Missing product returns 404", async () =>
            {
                using var response = await client.GetAsync("/products/999", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.NotFound, "Expected 404.");
            });
            await Check("Route constraints reject a non-integer product ID", async () =>
            {
                using var response = await client.GetAsync("/products/not-a-number", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.NotFound, "Expected no matching route.");
            });
            await Check("Query binding accepts an integer", async () =>
            {
                using var response = await client.GetAsync("/search?limit=5", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.OK, "Valid query failed.");
            });
            await Check("Query binding rejects a malformed integer", async () =>
            {
                using var response = await client.GetAsync("/search?limit=five", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.BadRequest, "Expected binding failure.");
            });
            await Check("A missing required query parameter returns 400", async () =>
            {
                using var response = await client.GetAsync("/search", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.BadRequest, "Required query parameter was ignored.");
            });
            await Check("An endpoint filter validates business input", async () =>
            {
                using var response = await client.PostAsJsonAsync("/products/preview",
                    new ProductRequest("  ", 0), timeout.Token);
                Require(response.StatusCode == HttpStatusCode.BadRequest, "Invalid input was accepted.");
                using JsonDocument body = await ReadJson(response);
                JsonElement errors = body.RootElement.GetProperty("errors");
                Require(errors.TryGetProperty("name", out _) && errors.TryGetProperty("price", out _),
                    "Validation errors were lost.");
            });
            await Check("A valid body is bound and serialized as a preview, not saved", async () =>
            {
                using var response = await client.PostAsJsonAsync("/products/preview",
                    new ProductRequest("  Pencil  ", 2m), timeout.Token);
                Require(response.StatusCode == HttpStatusCode.OK, "Valid body failed.");
                var preview = await response.Content.ReadFromJsonAsync<ProductPreview>(timeout.Token);
                Require(preview is { Name: "Pencil", Price: 2m, Currency: "USD" }, "Preview differs.");
            });
            await Check("Malformed JSON returns 400 before the handler", async () =>
            {
                using var content = new StringContent("{", Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("/products/preview", content, timeout.Token);
                Require(response.StatusCode == HttpStatusCode.BadRequest, "Malformed JSON was accepted.");
            });
            await Check("A product name of 80 characters is accepted and 81 is rejected", async () =>
            {
                using var accepted = await client.PostAsJsonAsync("/products/preview",
                    new ProductRequest(new string('a', 80), 1m), timeout.Token);
                using var rejected = await client.PostAsJsonAsync("/products/preview",
                    new ProductRequest(new string('a', 81), 1m), timeout.Token);
                Require(accepted.StatusCode == HttpStatusCode.OK && rejected.StatusCode == HttpStatusCode.BadRequest,
                    "Name-length boundary differs.");
            });
            await Check("Middleware runs before the endpoint and adds a response header", async () =>
            {
                using var response = await client.GetAsync("/pipeline", timeout.Token);
                using JsonDocument body = await ReadJson(response);
                Require(body.RootElement.GetProperty("middlewareRanFirst").GetBoolean() &&
                    response.Headers.Contains("X-Learning-Trace"), "Middleware did not wrap the endpoint.");
            });
            await Check("Unhandled failures become Problem Details without exception text", async () =>
            {
                using var response = await client.GetAsync("/failure", timeout.Token);
                Require(response.StatusCode == HttpStatusCode.InternalServerError &&
                    response.Content.Headers.ContentType?.MediaType == "application/problem+json",
                    "Expected a Problem Details error.");
                string text = await response.Content.ReadAsStringAsync(timeout.Token);
                Require(!text.Contains("Deliberate learning-example failure.", StringComparison.Ordinal),
                    "Exception text leaked into the response.");
            });
            await Check("DI lifetimes differ within and across requests", async () =>
            {
                var first = await client.GetFromJsonAsync<LifetimeReport>("/lifetimes", timeout.Token)
                    ?? throw new InvalidOperationException("Missing first lifetime report.");
                var second = await client.GetFromJsonAsync<LifetimeReport>("/lifetimes", timeout.Token)
                    ?? throw new InvalidOperationException("Missing second lifetime report.");
                Require(first.ApplicationId == second.ApplicationId, "Singleton changed between requests.");
                Require(first.RequestId == first.ReaderRequestId && second.RequestId == second.ReaderRequestId &&
                    first.RequestId != second.RequestId, "Scoped objects did not follow request scopes.");
                Require(first.OperationId != first.ReaderOperationId &&
                    second.OperationId != second.ReaderOperationId &&
                    first.OperationId != second.OperationId, "Transient objects were reused.");
            });
            await Check("Allowed browser origin receives a CORS header", async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/products/1");
                request.Headers.Add("Origin", "https://learning.example");
                using var response = await client.SendAsync(request, timeout.Token);
                Require(response.Headers.GetValues("Access-Control-Allow-Origin").Single() ==
                    "https://learning.example", "Allowed origin was not returned.");
            });
            await Check("A disallowed origin gets no CORS header, but the API still executes", async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/products/1");
                request.Headers.Add("Origin", "https://other.example");
                using var response = await client.SendAsync(request, timeout.Token);
                Require(response.StatusCode == HttpStatusCode.OK &&
                    !response.Headers.Contains("Access-Control-Allow-Origin"),
                    "CORS was incorrectly treated as endpoint authorization.");
            });
            await Check("CORS preflight permits the configured method", async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Options, "/products/1");
                request.Headers.Add("Origin", "https://learning.example");
                request.Headers.Add("Access-Control-Request-Method", "GET");
                using var response = await client.SendAsync(request, timeout.Token);
                Require(response.StatusCode == HttpStatusCode.NoContent &&
                    response.Headers.Contains("Access-Control-Allow-Methods"), "Preflight did not succeed.");
            });
            await Check("Later configuration providers override earlier keys", () =>
            {
                using var configuration = new ConfigurationManager();
                configuration
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Catalog:Currency"] = "USD" })
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Catalog:Currency"] = "CAD" });
                Require(configuration["Catalog:Currency"] == "CAD", "Later setting did not win.");
                return Task.CompletedTask;
            });
            await Check("Options snapshots stay scoped while monitor sees a reload", OptionsReload);
            await Check("Invalid options fail validation", () =>
            {
                var services = new ServiceCollection();
                services.AddOptions<CatalogOptions>()
                    .Configure(options => options.Currency = "")
                    .Validate(options => options.Currency.Length == 3, "Currency is required.");
                using var provider = services.BuildServiceProvider();
                ExpectThrows<OptionsValidationException>(() =>
                    _ = provider.GetRequiredService<IOptions<CatalogOptions>>().Value);
                return Task.CompletedTask;
            });
            await Check("Scope validation rejects a singleton capturing a scoped service", () =>
            {
                var services = new ServiceCollection();
                services.AddScoped<RequestStamp>();
                services.AddSingleton<CaptiveSingleton>();
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                ExpectThrows<InvalidOperationException>(() => provider.GetRequiredService<CaptiveSingleton>());
                return Task.CompletedTask;
            });
            await Check("A background worker can stop while waiting for its next tick", async () =>
            {
                using var worker = ActivatorUtilities.CreateInstance<CatalogWorker>(app.Services);
                await worker.StartAsync(timeout.Token);
                await worker.StopAsync(timeout.Token);
                Require(worker.ExecuteTask?.IsCompleted == true, "Background worker did not stop.");
            });
            Console.WriteLine($"All {passed} checks passed.");

            async Task Check(string name, Func<Task> run)
            {
                await run().WaitAsync(timeout.Token);
                passed++;
                Console.WriteLine($"PASS: {name}");
            }

            async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
                JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(shutdown.Token);
        }
    }

    private static Task OptionsReload()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Catalog:Currency"] = "USD" });
        var services = new ServiceCollection();
        services.AddOptions<CatalogOptions>().Bind(configuration.GetSection("Catalog"));
        using var provider = services.BuildServiceProvider();
        var fixedOptions = provider.GetRequiredService<IOptions<CatalogOptions>>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<CatalogOptions>>();
        using var firstScope = provider.CreateScope();
        var firstSnapshot = firstScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CatalogOptions>>();
        Require(fixedOptions.Value.Currency == "USD" && firstSnapshot.Value.Currency == "USD" &&
            monitor.CurrentValue.Currency == "USD", "Initial options differ.");
        configuration["Catalog:Currency"] = "CAD";
        ((IConfigurationRoot)configuration).Reload();
        using var secondScope = provider.CreateScope();
        var secondSnapshot = secondScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CatalogOptions>>();
        Require(fixedOptions.Value.Currency == "USD" && firstSnapshot.Value.Currency == "USD" &&
            secondSnapshot.Value.Currency == "CAD" && monitor.CurrentValue.Currency == "CAD",
            "Options reload lifetimes differ.");
        return Task.CompletedTask;
    }

    private static void ExpectThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CaptiveSingleton(RequestStamp request)
    {
        public RequestStamp Request { get; } = request;
    }
}
