using System.Net;
using Microsoft.Extensions.Options;

namespace AspNetRequestPipeline;

public static class DemoApi
{
    public static WebApplication Build(string[] args, bool checks = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = checks ? Environments.Production : null
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        if (checks)
        {
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
        }
        else if (string.IsNullOrEmpty(builder.Configuration["urls"]))
        {
            builder.WebHost.UseUrls("http://127.0.0.1:5085");
        }

        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton<ApplicationStamp>();
        builder.Services.AddScoped<RequestStamp>();
        builder.Services.AddTransient<OperationStamp>();
        builder.Services.AddScoped<StampReader>();
        builder.Services.AddScoped<CatalogJob>();
        builder.Services.AddHostedService<CatalogWorker>();
        builder.Services.AddOptions<CatalogOptions>()
            .Bind(builder.Configuration.GetSection("Catalog"))
            .Validate(options => options.Currency is { Length: 3 } currency &&
                currency.All(character => character is >= 'A' and <= 'Z'),
                "Catalog:Currency must contain three uppercase letters.")
            .ValidateOnStart();
        builder.Services.AddCors(options => options.AddPolicy("LearningBrowser", policy =>
            policy.WithOrigins("https://learning.example")
                .WithMethods("GET")
                .WithHeaders("Content-Type")));

        WebApplication app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Learning-Trace"] = context.TraceIdentifier;
                return Task.CompletedTask;
            });
            context.Items["ReachedMiddleware"] = true;
            await next(context);
        });
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors("LearningBrowser");

        app.MapGet("/products/{id:int}", (int id, IOptionsSnapshot<CatalogOptions> options) =>
            id == 1
                ? Results.Ok(new ProductResponse(1, "Notebook", 12.50m, options.Value.Currency))
                : Results.NotFound());
        app.MapGet("/search", (int limit) =>
            limit is >= 1 and <= 100
                ? Results.Ok(new { limit })
                : Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["Choose a limit from 1 to 100."]
                }));
        app.MapPost("/products/preview",
            (ProductRequest request, IOptionsSnapshot<CatalogOptions> options) =>
                Results.Ok(new ProductPreview(request.Name!.Trim(), request.Price, options.Value.Currency)))
            .AddEndpointFilter<ProductValidationFilter>();
        app.MapGet("/lifetimes", (ApplicationStamp application, RequestStamp request,
            OperationStamp operation, StampReader reader) =>
                new LifetimeReport(application.Id, request.Id, reader.RequestId,
                    operation.Id, reader.OperationId));
        app.MapGet("/pipeline", (HttpContext context) => new
        {
            middlewareRanFirst = context.Items.ContainsKey("ReachedMiddleware")
        });
        app.MapGet("/failure", Fail);
        return app;
    }

    private static IResult Fail() => throw new InvalidOperationException("Deliberate learning-example failure.");
}

public sealed class CatalogOptions
{
    public string Currency { get; set; } = "";
}

public sealed record ProductRequest(string? Name, decimal Price);
public sealed record ProductPreview(string Name, decimal Price, string Currency);
public sealed record ProductResponse(int Id, string Name, decimal Price, string Currency);
public sealed record LifetimeReport(
    Guid ApplicationId, Guid RequestId, Guid ReaderRequestId, Guid OperationId, Guid ReaderOperationId);

public sealed class ApplicationStamp
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class RequestStamp
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class OperationStamp
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class StampReader(RequestStamp request, OperationStamp operation)
{
    public Guid RequestId => request.Id;
    public Guid OperationId => operation.Id;
}

public sealed class ProductValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ProductRequest request = context.Arguments.OfType<ProductRequest>().Single();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
        {
            errors["name"] = ["Supply a product name of 1 to 80 characters, not just whitespace."];
        }
        if (request.Price <= 0)
        {
            errors["price"] = ["Price must be greater than zero."];
        }
        return errors.Count == 0 ? await next(context) : Results.ValidationProblem(errors);
    }
}
