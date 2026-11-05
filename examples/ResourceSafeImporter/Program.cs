using ResourceSafeImporter;

var checks = new (string Name, Func<Task> Run)[]
{
    ("Parser accepts valid data", ParserAcceptsValidData),
    ("Parser rejects invalid data", ParserRejectsInvalidData),
    ("Importer saves one complete batch and releases the file", ImportSavesBatch),
    ("Malformed input preserves cause and prevents writes", MalformedInput),
    ("Writer failure propagates and releases the file", WriterFailure),
    ("Cancellation before opening prevents writes", CancellationBeforeOpening),
    ("Cancellation during parsing prevents writes and releases the file", CancellationDuringParsing),
    ("Cancellation in the writer propagates and releases the file", CancellationInWriter),
    ("Empty file returns zero without calling the writer", EmptyFile),
    ("Exactly 1,000 rows are accepted", ExactRowLimit),
    ("Row limit rejects the whole batch and releases the file", RowLimit)
};

foreach (var check in checks)
{
    await check.Run();
    Console.WriteLine($"PASS: {check.Name}");
}
Console.WriteLine($"All {checks.Length} checks passed.");

static Task ParserAcceptsValidData()
{
    var result = new ProductRowParser().Parse("42;19.95");
    Require(result == new ProductRow(42, 19.95m), "Parsed values differ.");
    return Task.CompletedTask;
}

static async Task ParserRejectsInvalidData()
{
    foreach (string line in new[] { "", "0;1", "x;1", "1;-1", "1;2;3", "1;1,50" })
    {
        await ExpectThrows<FormatException>(() =>
        {
            new ProductRowParser().Parse(line);
            return Task.CompletedTask;
        });
    }
}

static Task ImportSavesBatch() => WithFile("1;12.50\n2;0\n", async path =>
{
    var writer = new RecordingWriter();
    int count = await new ProductImporter(new ProductRowParser(), writer).ImportAsync(path);
    Require(count == 2 && writer.Calls == 1, "Expected one complete batch.");
    Require(writer.Rows.SequenceEqual(new[] { new ProductRow(1, 12.50m), new ProductRow(2, 0) }),
        "Saved rows differ.");
    Require(!writer.Disposed, "Importer disposed a borrowed writer.");
    AssertFileReleased(path);
});

static Task MalformedInput() => WithFile("1;12.50\ninvalid\n", async path =>
{
    var writer = new RecordingWriter();
    var error = await ExpectThrows<ImportFormatException>(() =>
        new ProductImporter(new ProductRowParser(), writer).ImportAsync(path));
    Require(error.LineNumber == 2 && error.InnerException is FormatException,
        "Missing line number or original failure.");
    Require(writer.Calls == 0, "Malformed input reached the writer.");
    AssertFileReleased(path);
});

static Task WriterFailure() => WithFile("1;12.50", async path =>
{
    var expected = new IOException("Storage unavailable.");
    var error = await ExpectThrows<IOException>(() =>
        new ProductImporter(new ProductRowParser(), new FailingWriter(expected)).ImportAsync(path));
    Require(ReferenceEquals(error, expected), "Writer failure was replaced.");
    AssertFileReleased(path);
});

static Task CancellationBeforeOpening() => WithFile("1;12.50", async path =>
{
    using var source = new CancellationTokenSource();
    source.Cancel();
    var writer = new RecordingWriter();
    await ExpectThrows<OperationCanceledException>(() =>
        new ProductImporter(new ProductRowParser(), writer).ImportAsync(path, source.Token));
    Require(writer.Calls == 0, "Cancelled import reached writer.");
    AssertFileReleased(path);
});

static Task CancellationDuringParsing() => WithFile("1;12.50\n2;5", async path =>
{
    using var source = new CancellationTokenSource();
    var writer = new RecordingWriter();
    await ExpectThrows<OperationCanceledException>(() =>
        new ProductImporter(new CancellingParser(source), writer).ImportAsync(path, source.Token));
    Require(writer.Calls == 0, "Cancelled parsing reached writer.");
    AssertFileReleased(path);
});

static Task CancellationInWriter() => WithFile("1;12.50", async path =>
{
    using var source = new CancellationTokenSource();
    var writer = new CancellingWriter(source);
    await ExpectThrows<OperationCanceledException>(() =>
        new ProductImporter(new ProductRowParser(), writer).ImportAsync(path, source.Token));
    Require(writer.Calls == 1 && writer.ReceivedToken == source.Token,
        "Cancellation token was not passed to writer.");
    AssertFileReleased(path);
});

static Task EmptyFile() => WithFile("", async path =>
{
    var writer = new RecordingWriter();
    int count = await new ProductImporter(new ProductRowParser(), writer).ImportAsync(path);
    Require(count == 0 && writer.Calls == 0, "Empty-file policy was not respected.");
    AssertFileReleased(path);
});

static Task ExactRowLimit() => WithFile(
    string.Join('\n', Enumerable.Range(1, ProductImporter.MaxRows).Select(id => $"{id};1")), async path =>
{
    var writer = new RecordingWriter();
    int count = await new ProductImporter(new ProductRowParser(), writer).ImportAsync(path);
    Require(count == ProductImporter.MaxRows && writer.Rows.Count == count && writer.Calls == 1,
        "Exact row limit was not accepted.");
    AssertFileReleased(path);
});

static Task RowLimit() => WithFile(
    string.Join('\n', Enumerable.Repeat("1;1", ProductImporter.MaxRows + 1)), async path =>
{
    var writer = new RecordingWriter();
    await ExpectThrows<InvalidDataException>(() =>
        new ProductImporter(new ProductRowParser(), writer).ImportAsync(path));
    Require(writer.Calls == 0, "Oversized batch reached writer.");
    AssertFileReleased(path);
});

static void AssertFileReleased(string path)
{
    using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Require(probe.CanRead, "File cannot be reopened.");
}

static async Task WithFile(string content, Func<string, Task> check)
{
    string path = Path.GetTempFileName();
    try
    {
        await File.WriteAllTextAsync(path, content);
        await check(path);
    }
    finally
    {
        File.Delete(path);
    }
}

static async Task<TException> ExpectThrows<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class RecordingWriter : IProductWriter, IDisposable
{
    public int Calls { get; private set; }
    public List<ProductRow> Rows { get; } = [];
    public bool Disposed { get; private set; }

    public Task SaveAsync(IReadOnlyList<ProductRow> rows, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Rows.AddRange(rows);
        return Task.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}

sealed class FailingWriter(IOException exception) : IProductWriter
{
    public Task SaveAsync(IReadOnlyList<ProductRow> rows, CancellationToken cancellationToken) =>
        Task.FromException(exception);
}

sealed class CancellingParser(CancellationTokenSource source) : IProductRowParser
{
    public ProductRow Parse(string line)
    {
        var row = new ProductRowParser().Parse(line);
        source.Cancel();
        return row;
    }
}

sealed class CancellingWriter(CancellationTokenSource source) : IProductWriter
{
    public int Calls { get; private set; }
    public CancellationToken ReceivedToken { get; private set; }

    public Task SaveAsync(IReadOnlyList<ProductRow> rows, CancellationToken cancellationToken)
    {
        Calls++;
        ReceivedToken = cancellationToken;
        source.Cancel();
        return Task.FromCanceled(cancellationToken);
    }
}
