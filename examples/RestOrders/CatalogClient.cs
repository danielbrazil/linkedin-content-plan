using System.Net;
using System.Net.Http.Json;

namespace RestOrders;

public sealed record CatalogProduct(int Id, string Name);

public sealed class CatalogClient(HttpClient http)
{
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
}
