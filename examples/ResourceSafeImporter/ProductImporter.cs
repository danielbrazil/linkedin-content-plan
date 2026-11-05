using System.Globalization;
using System.Text;

namespace ResourceSafeImporter;

public sealed record ProductRow(int Id, decimal Price);

public interface IProductRowParser
{
    ProductRow Parse(string line);
}

public interface IProductWriter
{
    Task SaveAsync(IReadOnlyList<ProductRow> rows, CancellationToken cancellationToken);
}

public sealed class ProductRowParser : IProductRowParser
{
    public ProductRow Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string[] fields = line.Split(';');
        if (fields.Length != 2 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
            id <= 0 ||
            !decimal.TryParse(fields[1], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal price) ||
            price < 0)
        {
            throw new FormatException("Expected a positive integer ID and a nonnegative decimal price.");
        }

        return new ProductRow(id, price);
    }
}

public sealed class ImportFormatException : Exception
{
    public int LineNumber { get; }

    public ImportFormatException(int lineNumber, FormatException innerException)
        : base($"Invalid product at line {lineNumber}.", innerException)
    {
        LineNumber = lineNumber;
    }
}

public sealed class ProductImporter
{
    public const int MaxRows = 1000;
    private readonly IProductRowParser parser;
    private readonly IProductWriter writer;

    public ProductImporter(IProductRowParser parser, IProductWriter writer)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(writer);
        this.parser = parser;
        this.writer = writer;
    }

    public async Task<int> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        using var reader = new StreamReader(
            stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024, leaveOpen: true);

        var rows = new List<ProductRow>();
        int lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (lineNumber > MaxRows)
            {
                throw new InvalidDataException($"An import cannot exceed {MaxRows} rows.");
            }

            try
            {
                rows.Add(parser.Parse(line));
            }
            catch (FormatException exception)
            {
                throw new ImportFormatException(lineNumber, exception);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (rows.Count > 0)
        {
            await writer.SaveAsync(rows.AsReadOnly(), cancellationToken);
        }

        return rows.Count;
    }
}
