# Designing a resource-safe C# component

A supplier sends a product file. Your application must validate the rows, save the batch, and release the file even when something fails.

That small task gives us practical examples of contracts, composition, SOLID, immutability, resource ownership, exceptions, and testing.

**SOLID** is a group of five design principles: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion. Each is explained below as a decision, not a requirement to add more classes.

This article covers questions 014-025 in the [content calendar](./LinkedIn_Content_Plan.md). The four LinkedIn posts explain the same topics directly; the article adds complete code and deeper trade-offs.

## Reading map

| Post | Questions | Main decision |
|---|---|---|
| [Part 1/4: Contracts and composition](./CSharp_DotNet_Resource_Safe_Components_Part_1_of_4_Contracts_And_Composition.md) | 014-016 | Separate independently changing responsibilities |
| [Part 2/4: Extensibility and contracts](./CSharp_DotNet_Resource_Safe_Components_Part_2_of_4_Extensibility_And_Contracts.md) | 017-019 | Replace behavior without breaking promises |
| [Part 3/4: Immutability and ownership](./CSharp_DotNet_Resource_Safe_Components_Part_3_of_4_Immutability_And_Ownership.md) | 020-022 | Decide what can change and who cleans up |
| [Part 4/4: Errors and testing](./CSharp_DotNet_Resource_Safe_Components_Part_4_of_4_Errors_And_Testing.md) | 023-025 | Make failures diagnosable and verify behavior |

## The example's requirements

The sample uses a deliberately simple supplier format, not a general CSV parser. **CSV** usually means comma-separated values and can require rules for quoting and embedded separators; this example instead accepts exactly two semicolon-separated fields:

```text
42;19.95
43;0
```

The first field is a positive integer product identifier. The second is a nonnegative decimal price using a dot as the decimal separator. There is no header, no quoting, and no blank-line skipping. Whitespace, signs, thousands separators, and extra columns are rejected by this sample's policy.

The importer:

1. Opens a file it owns.
2. Parses and validates at most 1,000 rows.
3. Rejects invalid data before calling the writer.
4. Calls the writer once with the complete nonempty batch.
5. Returns the number of rows when the writer succeeds.
6. Releases the file on success, failure, and cancellation.

An empty file is explicitly accepted as zero rows and causes no writer call. A malformed row is an error, never silently skipped.

These are product decisions, not consequences of SOLID. Another system may require partial acceptance, streaming, or duplicate detection; those need different explicit contracts.

## Run the code and checks

Install the **.NET 10 SDK**, the Software Development Kit containing the compiler and command-line tools. From the repository folder, run:

```powershell
dotnet run --project .\examples\ResourceSafeImporter\ResourceSafeImporter.csproj
```

The sample has no external package dependencies. Compiler warnings are treated as errors.

- [ProductImporter.cs](./examples/ResourceSafeImporter/ProductImporter.cs): the importer, parser, contracts, and row model.
- [Program.cs](./examples/ResourceSafeImporter/Program.cs): executable checks using real temporary files and test substitutes.
- [Project file](./examples/ResourceSafeImporter/ResourceSafeImporter.csproj): .NET 10 console-project settings.

This is a self-checking console example, not an xUnit or NUnit test project. Those are dedicated testing frameworks. Here a failed assertion throws an exception and makes the process fail; a successful run prints a PASS line for each check.

Validation: all 11 checks passed locally on Windows with .NET 10 in Release configuration. The article's C# snippets were checked against the runnable source.

Temporary files are deleted in `finally` blocks, cleanup blocks that execute when control leaves the associated `try`, including on exceptions.

## 014. When should a design use an interface rather than an abstract base class?

An **interface** specifies a contract implemented by another type. An **abstract base class** can define a contract while also sharing implementation and instance state with derived classes.

Our importer needs a parser and a writer:

```csharp
public interface IProductRowParser
{
    ProductRow Parse(string line);
}

public interface IProductWriter
{
    Task SaveAsync(IReadOnlyList<ProductRow> rows, CancellationToken cancellationToken);
}
```

`Task` represents an operation that may complete later. `IReadOnlyList<T>` exposes access to a sequence of items without add/remove operations. `CancellationToken` carries a request to stop; participating code must observe it.

**Scenario.** A supplier changes its price format. Implement another parser without requiring it to inherit a shared internal state model. The importer continues to use `Parse`.

**Trade-off.** An interface does not provide all the implementation reuse of a base class. A base class can be appropriate when related types share essential behavior and must follow common rules. But do not add inheritance only to save a few duplicated lines: changing a base class can unintentionally affect every derived class.

**Contract detail.** Our parser accepts a non-null line and returns a valid `ProductRow` or throws `FormatException`. Null is a caller error. A format exception reports that text does not satisfy the expected structure or values.

## 015. When is composition preferable to inheritance?

**Composition** means giving an object collaborators that perform parts of its work. **Inheritance** means creating a more specialized type based on another type.

The importer receives its collaborators:

```csharp
public ProductImporter(IProductRowParser parser, IProductWriter writer)
{
    ArgumentNullException.ThrowIfNull(parser);
    ArgumentNullException.ThrowIfNull(writer);
    this.parser = parser;
    this.writer = writer;
}
```

The constructor rejects missing dependencies immediately. `this.parser` and `this.writer` refer to the object's stored fields.

**Scenario.** Two suppliers and two database implementations should not force four importer subclasses. Compose the selected parser and writer instead.

**Trade-off.** Composition requires a place to construct and connect objects. Keep that wiring explicit. Inheritance remains useful where a derived type genuinely satisfies the base contract and benefits from shared implementation.

## 016. How would you apply the single-responsibility principle to an application service?

The **Single Responsibility Principle** asks for one cohesive reason to change, not one method or a fixed maximum class length.

| Component | Reason to change | What should stay elsewhere |
|---|---|---|
| Parser | Supplier row format and row validation | Database commands |
| Importer | Import workflow and batch policy | Supplier-specific splitting rules |
| Writer | Persistence behavior | Reading files |

**Persistence** means storing data beyond the current operation, usually in a database or file.

**Scenario.** A supplier changes its separator while the database schema stays the same. That should affect parsing, not storage.

**Trade-off.** Separate responsibilities that vary independently. Splitting every statement into a new service makes the workflow harder to follow. Our importer owns both the read loop and its file scope because those form a cohesive workflow.

## 017. How do the open/closed principle and strategy pattern support extensibility?

The **Open/Closed Principle** encourages extending stable behavior without repeatedly modifying its internals. A **strategy** is a replaceable implementation of an operation.

`IProductRowParser` is the strategy boundary. The import loop calls `parser.Parse(line)`; it does not branch on every supplier name.

**Scenario.** Add a parser for a second supplier and select it where the application constructs its dependencies. The existing importer does not need another conditional branch.

**Trade-off.** The principle does not prohibit changing code. If the new supplier requires multiline records, the current one-line contract may no longer fit. Change the contract deliberately instead of forcing an unsuitable format into it. No dynamic plug-in loader is needed for this example.

## 018. How can an implementation violate the Liskov substitution principle?

The **Liskov Substitution Principle** means an implementation should preserve the behavior promised by the contract it replaces.

Suppose successful completion of `SaveAsync` means all supplied rows were saved. A writer that silently discards rows it dislikes breaks the contract, even though the compiler accepts its signature.

Similarly, a parser returning null for invalid text breaks the promise to return a valid row or report a format failure.

**Scenario.** The application replaces an in-memory writer with a database writer. Tests that only check interface implementation will miss silent partial acceptance.

**Trade-off.** Specify success, failure, cancellation, and side effects. In our sample, failure behavior does not promise rollback: a writer may fail after beginning work. A real writer requiring all-or-nothing persistence needs a **transaction**, a database unit of work committed or rolled back together, and its own tests.

## 019. How do interface segregation and dependency inversion improve testability?

**Interface Segregation** means consumers should not depend on operations they do not need. **Dependency Inversion** means higher-level policy depends on contracts rather than concrete lower-level implementations.

The importer needs saving, not database administration or reporting. A narrow `IProductWriter` prevents those unrelated operations from becoming part of its dependency.

**Constructor injection** supplies collaborators through a constructor. It is one way to connect dependencies; it does not require a dependency-injection container, a framework that constructs and manages registered services.

**Scenario.** A fake writer stores rows in memory for a test. A real writer can store them in SQL Server or PostgreSQL without changing the parsing workflow.

**Trade-off.** Replacing a database with a fake only isolates the importer. It does not verify SQL, transactions, database constraints, or connection behavior. Do not create interfaces solely to satisfy a slogan when no useful boundary exists.

## 020. When should state be immutable, and what trade-offs does that introduce?

**Immutable** means state does not change after creation. It can prevent validated values from changing before they are consumed.

```csharp
public sealed record ProductRow(int Id, decimal Price);
```

This record generates properties that cannot be reassigned after initialization. `sealed` prevents inheritance. The integer and decimal members are values, not references to mutable nested collections.

**Scenario.** The parser returns product 42 at price 19.95. Another collaborator cannot change that row's price in place while the writer receives it.

The importer uses a mutable `List<ProductRow>` while collecting rows and passes `rows.AsReadOnly()` to the writer. This creates a read-only view, not a deep immutable snapshot. It is sufficient here because the list stays private and the importer does not modify it after handing it to the writer.

**Trade-off.** Replacing immutable values may allocate or copy data. Records containing mutable lists do not automatically freeze them. Also, the record itself does not enforce positive identifiers or nonnegative prices: the parser establishes those conditions in this workflow. A broader domain model may need validation at construction too.

## 021. How should IDisposable, IAsyncDisposable, using, and await using express resource ownership?

`IDisposable` supplies synchronous cleanup through `Dispose()`. `IAsyncDisposable` supplies `DisposeAsync()`, allowing cleanup that may complete asynchronously. **Asynchronous** means the caller can wait for completion without necessarily blocking a thread during the wait.

`using` ensures disposal when execution leaves the scope. `await using` awaits asynchronous disposal. Neither guarantees successful cleanup: implementations can fail, and asynchronous disposal may complete synchronously.

**Scenario.** The importer creates a `FileStream`, a managed object wrapping an operating-system file handle. It also creates a `StreamReader`, which decodes bytes into text.

Our ownership rules are:

- The importer owns the stream it opens.
- The importer owns the reader it creates.
- The reader uses `leaveOpen: true`, so its disposal leaves the stream open.
- The importer disposes the reader first, then asynchronously disposes the stream.
- The parser and writer are borrowed dependencies, not owned resources.

### Complete import operation

This method appears in the [runnable implementation](./examples/ResourceSafeImporter/ProductImporter.cs). It uses the constructor fields above and the `MaxRows` constant, set to 1,000.

```csharp
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
```

Terms in this example:

- `async` allows `await` within the method; `Task<int>` represents eventual completion with an integer result.
- `FileShare.None` requests exclusive file access while the stream is open.
- `FileOptions.Asynchronous` requests asynchronous file operations; `SequentialScan` is an access-pattern hint.
- `Encoding.UTF8` specifies a text encoding, rules for converting bytes to characters. Byte-order-mark detection recognizes an encoding marker at the start of a file.
- `is { } line` matches a non-null result and names it `line`; a null result from `ReadLineAsync` means the end of the file.
- A **cancellation token** is a cooperative signal. Passing it through does not forcibly terminate a dependency that ignores it.
- Using declarations dispose at the end of their scope in reverse declaration order.

**Trade-off.** This compact version keeps the file open through saving. A longer-running writer could justify narrowing the read scope so the file is released before persistence begins. That would change when the file becomes available to other processes, so make the policy explicit.

## 022. When is a finalizer appropriate, and how does SafeHandle affect that decision?

A **finalizer** is fallback cleanup invoked by the runtime for eligible objects. It has unpredictable timing and is not guaranteed to run at process exit.

**SafeHandle** is a .NET base class for implementing reliable ownership wrappers around native handles. A **native handle** is an operating-system resource identifier, rather than an ordinary managed object reference.

**Scenario.** This importer uses FileStream. It does not directly own a raw operating-system handle, so it should not add a finalizer. FileStream already provides the wrapper and disposal contract.

If integrating with a native library, prefer an existing appropriate SafeHandle implementation or implement a SafeHandle-derived wrapper for that handle's release function. Deterministic disposal, cleanup at an explicit program point, is still important.

**Trade-off.** Custom finalization introduces lifecycle complexity and can delay reclamation. Do not write a finalizer that reaches into ordinary managed collaborators and assumes they remain usable.

## 023. How should exceptions be caught, translated, and rethrown without losing diagnostic information?

An **exception** reports a failure that interrupts normal execution. Catch it when you can recover or add meaningful context.

The importer catches only parser `FormatException` failures and adds the line number:

```csharp
public sealed class ImportFormatException : Exception
{
    public int LineNumber { get; }

    public ImportFormatException(int lineNumber, FormatException innerException)
        : base($"Invalid product at line {lineNumber}.", innerException)
    {
        LineNumber = lineNumber;
    }
}
```

`InnerException` retains the original error and its diagnostic information. The new exception describes the import-level failure without embedding raw supplier data.

**Scenario.** The second line is malformed. The caller receives "Invalid product at line 2," and the writer has not been called. It does not receive a misleading zero-row success result.

Use `throw;` inside a catch block for an unchanged rethrow. `throw ex;` restarts the exception's **stack trace**, the recorded call chain used to locate the error. Wrapping intentionally creates a new outer failure while preserving the original as its inner exception.

**Trade-off.** Do not catch every exception just to rename it. The sample lets storage failures, file-access failures, and cancellation propagate. The application boundary, such as a job runner, should log and report the failure once with appropriate context. A reusable component should not silently return success or duplicate logs from each layer.

## 024. How do unit tests differ from integration tests for a component with external dependencies?

A **unit test** checks focused behavior without real external infrastructure. An **integration test** checks components working together through an actual boundary such as the file system or database.

**Scenario.** Parser checks are unit-style checks. Tests that create an actual temporary file and run the importer are file-system integration checks, even though they use a fake writer.

The executable example checks:

| Check | Expected behavior |
|---|---|
| Valid row | Correct identifier and price |
| Invalid row variants | Format error, not silent coercion |
| Successful import | Exact rows, one writer call, file released, borrowed writer not disposed |
| Malformed second row | Correct line number and original cause; zero writer calls |
| Writer failure | Original failure propagates; file released |
| Cancellation before opening | No write |
| Cancellation during parsing | No write; file released |
| Cancellation inside writer | Token forwarded, cancellation propagated, file released |
| Empty file | Zero result, no writer call |
| Exactly 1,000 rows | Complete batch accepted and file released |
| More than 1,000 rows | Error before saving; file released |

Tests reopen the file with exclusive access after the operation. This checks a visible resource-ownership outcome rather than merely asserting that a method named Dispose was called.

**Trade-off.** File-sharing behavior is platform-dependent; run integration checks on deployment platforms too. These checks do not test database transactions because this sample has no database writer.

## 025. When should a test use a mock, stub, or fake?

These are **test doubles**, substitutes for real collaborators. Terminology varies across teams and frameworks:

- A **stub** supplies predetermined behavior, such as a writer that always returns a failed task.
- A **fake** is a simplified working implementation, such as a writer that stores rows in memory.
- A **mock** verifies expected interactions, such as a required call count or argument.

Our `RecordingWriter` is a fake with recording behavior: it stores rows and records calls. The test assertions can use that recording to verify meaningful interactions. The sample does not need a mocking library.

**Scenario.** A failing writer checks that the importer propagates a storage failure and releases its file. A recording writer checks that malformed input never reaches saving.

**Trade-off.** A test that specifies every internal call and its order can fail during harmless refactoring. Assert observable results first, and assert interactions only where they represent a requirement.

## Scope and limitations

This is an educational component, not a production-ready import platform:

- The parser recognizes a strict two-field format, not general CSV or arbitrary encodings.
- There is a row-count limit, but no byte or line-length limit. A huge single line can still allocate substantial memory. Add file-size and incremental line-length controls for untrusted uploads.
- Parsed rows are buffered before saving. This avoids writes on parsing errors but is not appropriate for unlimited-size files.
- Duplicate identifiers are not rejected. Decide whether they mean updates, errors, or duplicates to ignore before implementing persistence.
- The sample writer is in-memory. There is no SQL Server/PostgreSQL implementation, transaction, retry policy, or durable storage.
- Cancellation does not imply rollback. A real writer must document what may already have been saved when cancellation is observed.
- Only owned objects are disposed. Registration in a dependency-injection container may assign ownership to the container instead.

## Interview recap

| Question | Practical answer | Watch out for |
|---|---|---|
| Interface or abstract base class? | Use a contract when shared implementation is unnecessary | Inheritance couples implementations to base behavior |
| Composition or inheritance? | Supply independently varying collaborators | Do not create a subclass for every combination |
| What is single responsibility? | Separate format, workflow, and storage changes | One responsibility does not mean one method |
| How does open/closed work with strategies? | Add another parser behind the existing contract | A new format may legitimately require a new contract |
| What violates Liskov substitution? | Returning success after silently skipping required work | A matching signature is not enough |
| Why segregation and inversion? | Depend on small contracts needed by the importer | Fakes do not verify the real database |
| When use immutable state? | Keep validated product values stable | Read-only views and nested collections are not deep immutability |
| Who disposes resources? | The owner; use scoped synchronous or asynchronous cleanup | Do not dispose borrowed dependencies |
| Finalizer or SafeHandle? | Prefer established handle wrappers; no custom finalizer here | Finalization is not prompt cleanup |
| How handle exceptions? | Add useful context and preserve the cause | No silent success or cancellation-to-error translation |
| Unit or integration test? | Focused parser checks versus actual file/database boundaries | Name what the test really exercises |
| Mock, stub, or fake? | Choose the substitute for the behavior being checked | Avoid tests coupled to incidental call sequences |

## References

- [Microsoft Learn: Interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces)
- [Microsoft Learn: Inheritance](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/object-oriented/inheritance)
- [Microsoft Learn: Common web application architectures and dependency inversion](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Microsoft Learn: Records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Microsoft Learn: Implementing Dispose](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
- [Microsoft Learn: Implementing DisposeAsync](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync)
- [Microsoft Learn: SafeHandle](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle)
- [Microsoft Learn: Exception best practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Microsoft Learn: Unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
