using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace RestOrders;

public sealed class ApiMarker;

public sealed record CreateOrder(int ProductId, int Quantity);
public sealed record OrderResponse(Guid Id, int ProductId, int Quantity, decimal Total);
public sealed record OrderPage(IReadOnlyList<OrderResponse> Items, long? NextAfter);

public sealed class Order
{
    public long Sequence { get; set; }
    public Guid Id { get; set; }
    public required string Owner { get; set; }
    public required string IdempotencyKey { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public Guid Version { get; set; }
}

public sealed class OrdersDb(DbContextOptions<OrdersDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Order>().HasKey(order => order.Sequence);
        model.Entity<Order>().HasIndex(order => order.Id).IsUnique();
        model.Entity<Order>().HasIndex(order => new { order.Owner, order.IdempotencyKey }).IsUnique();
    }
}

public static class OrderEndpoints
{
    public static async Task<IResult> CreateAsync(
        CreateOrder request, HttpContext context, OrdersDb db, CancellationToken token)
    {
        string key = context.Request.Headers["Idempotency-Key"].ToString();
        var errors = new Dictionary<string, string[]>();
        if (key.Length is < 1 or > 100 || !key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            errors["Idempotency-Key"] = ["Supply 1 to 100 ASCII letters, digits, or hyphens."];
        }
        if (request.ProductId != 1)
        {
            errors["productId"] = ["This demonstration catalog contains only product 1."];
        }
        if (request.Quantity is < 1 or > 10)
        {
            errors["quantity"] = ["Choose a quantity from 1 to 10."];
        }
        if (errors.Count != 0)
        {
            return Results.ValidationProblem(errors);
        }

        string owner = context.User.FindFirstValue("sub")!;
        Order? existing = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.Owner == owner && order.IdempotencyKey == key, token);
        if (existing is not null)
        {
            return Replay(existing, request);
        }
        var created = new Order
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            IdempotencyKey = key,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Total = request.Quantity * 12.50m,
            Version = Guid.NewGuid()
        };
        db.Orders.Add(created);
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            db.ChangeTracker.Clear();
            existing = await db.Orders.AsNoTracking()
                .SingleOrDefaultAsync(order => order.Owner == owner && order.IdempotencyKey == key, token);
            if (existing is null)
            {
                throw;
            }
            return Replay(existing, request);
        }
        return Results.Created($"/api/v1/orders/{created.Id}", ToResponse(created));
    }

    public static async Task<IResult> GetAsync(Guid id, HttpContext context, OrdersDb db, CancellationToken token)
    {
        string owner = context.User.FindFirstValue("sub")!;
        Order? order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == id && order.Owner == owner, token);
        if (order is null)
        {
            return Results.NotFound();
        }
        string tag = $"\"{order.Version:N}\"";
        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";
        IList<EntityTagHeaderValue>? candidates = null;
        if (context.Request.Headers.ContainsKey(HeaderNames.IfNoneMatch) &&
            !EntityTagHeaderValue.TryParseStrictList(
            context.Request.Headers.IfNoneMatch.Select(value => value ?? "").ToArray(),
            out candidates))
        {
            return Results.Problem(statusCode: 400, title: "Invalid If-None-Match header.");
        }
        if (candidates is not null && candidates.Any(candidate =>
            candidate == EntityTagHeaderValue.Any ||
            candidate.Compare(new EntityTagHeaderValue(tag), useStrongComparison: false)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }
        return Results.Ok(ToResponse(order));
    }

    public static async Task<IResult> ListAsync(
        long? after, int? limit, int? productId, HttpContext context, OrdersDb db, CancellationToken token)
    {
        int size = limit ?? 10;
        if (size is < 1 or > 50 || after is < 0 || productId is <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["query"] = ["Use limit 1-50, a nonnegative after cursor, and a positive productId."]
            });
        }
        string owner = context.User.FindFirstValue("sub")!;
        IQueryable<Order> query = db.Orders.AsNoTracking()
            .Where(order => order.Owner == owner && order.Sequence > (after ?? 0));
        if (productId is int product)
        {
            query = query.Where(order => order.ProductId == product);
        }
        List<Order> rows = await query.OrderBy(order => order.Sequence).Take(size + 1).ToListAsync(token);
        bool more = rows.Count > size;
        var page = rows.Take(size).ToArray();
        return Results.Ok(new OrderPage(page.Select(ToResponse).ToArray(), more ? page[^1].Sequence : null));
    }

    private static IResult Replay(Order order, CreateOrder request) =>
        order.ProductId == request.ProductId && order.Quantity == request.Quantity
            ? Results.Created($"/api/v1/orders/{order.Id}", ToResponse(order))
            : Results.Problem(statusCode: 409, title: "This idempotency key was used for different input.");

    private static OrderResponse ToResponse(Order order) =>
        new(order.Id, order.ProductId, order.Quantity, order.Total);
}
