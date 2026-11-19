using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using RestOrders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri("https://catalog.example/");
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddDbContext<OrdersDb>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Orders")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:Orders.")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Identity:Authority"];
    options.Audience = builder.Configuration["Identity:Audience"];
    options.MapInboundClaims = false;
});
builder.Services.AddAuthorization(options => options.AddPolicy("orders", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("sub").RequireClaim("permission", "orders")));
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retry))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        await Results.Problem(statusCode: 429, title: "Request limit reached.")
            .ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("orders", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
var orders = app.MapGroup("/api/v1/orders").RequireAuthorization("orders").RequireRateLimiting("orders");
orders.MapPost("/", OrderEndpoints.CreateAsync);
orders.MapGet("/{id:guid}", OrderEndpoints.GetAsync);
orders.MapGet("/", OrderEndpoints.ListAsync);
app.Run();

public partial class Program;
