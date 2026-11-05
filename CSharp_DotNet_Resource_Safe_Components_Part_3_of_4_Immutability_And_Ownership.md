# Designing a resource-safe C# component | Part 3/4

Who owns the file handle when an import fails halfway through?

20\. When should state be immutable?

Immutable state cannot change after creation. It helps prevent already-validated data from changing before it is saved.

Our product row is a record containing an integer identifier and a decimal price:

`public sealed record ProductRow(int Id, decimal Price);`

These properties cannot be reassigned after initialization. A decimal represents base-10 numeric values, commonly used for prices.

Scenario: pass a validated row to the writer without another component changing its price in place.

Records do not automatically freeze nested objects: a record containing a mutable list can still expose changing data.

The trade-off: replacing values can require copying and allocation. Use a local mutable collection while building the batch, then expose a read-only view. Read-only access restricts operations; it does not make every underlying object immutable.

21\. IDisposable, IAsyncDisposable, using, and await using?

`IDisposable` defines `Dispose()` for explicit cleanup. `IAsyncDisposable` defines `DisposeAsync()` for cleanup that may need asynchronous work.

`using` calls Dispose when execution leaves its scope, including through an exception. `await using` waits for asynchronous disposal; it does not guarantee that cleanup is always nonblocking.

Our importer opens a FileStream, a managed object wrapping a file handle, so it owns that stream.

It also creates a StreamReader, which turns bytes into text. We configure `leaveOpen: true`: disposing the reader leaves the stream for the importer to dispose.

The parser and writer are borrowed constructor dependencies. The importer does not dispose them; their owner decides their lifetime.

This matters on invalid input and cancellation, not just the successful path. A cancellation token communicates a request to stop; it does not terminate work automatically.

22\. Do we need a finalizer or SafeHandle?

A finalizer is fallback cleanup invoked by the runtime at an unpredictable time. It is not guaranteed to run at process exit.

Our importer needs no finalizer: FileStream already provides managed ownership of its underlying handle.

SafeHandle is a .NET base class for implementing wrappers around native handles. Use an appropriate existing wrapper, or derive from SafeHandle when directly owning a native handle. Do not invent finalizer logic for a class that merely uses FileStream.

Next: useful failures and tests that check ownership.

Details and runnable ownership example, sections 020-022:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Resource_Safe_Components.md

#CSharp #DotNet #SoftwareDesign
