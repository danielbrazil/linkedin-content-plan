# Designing a resource-safe C# component | Part 4/4

"The import failed" is not enough information to fix it.

23\. How should exceptions be caught, translated, and rethrown?

Catch an exception when you can recover or add useful context.

Our parser throws FormatException for malformed product data. The importer translates it into ImportFormatException with the line number and original exception.

Translation means presenting a failure in terms the caller understands. The original exception remains available as InnerException, preserving its cause and diagnostic details.

Example: "Invalid product at line 3" is useful. Returning zero imported rows would falsely resemble success.

Use `throw;` inside a catch block to rethrow without resetting the original stack trace, the recorded chain of calls leading to the error. Avoid `throw ex;`.

Let cancellation remain cancellation. Do not turn it into a format error.

Log the failure once at the application boundary, where the caller decides how to report or retry it. Avoid logging the same failure in every layer or including sensitive row contents.

24\. Unit tests vs. integration tests?

A unit test checks a focused behavior without real external infrastructure. An integration test checks collaborating components across a real boundary, such as the file system or a database.

Our parser tests cover valid rows, invalid identifiers, and negative prices.

Our file-system tests create a temporary input file, run the importer, and check that:

• A malformed row causes no call to the writer.
• The error identifies the line and retains its original cause.
• The file can be reopened exclusively after success, failure, or cancellation.

The writer is still a fake in these tests. Passing them does not prove SQL transactions or database constraints work.

25\. Mock, stub, or fake?

A stub supplies a predetermined response: for example, a writer configured to fail.

A fake is a simplified working implementation: our in-memory writer stores rows for inspection.

A mock checks expected interactions: for example, that SaveAsync was called exactly once.

Terminology varies; the important question is what behavior the substitute helps verify.

Prefer assertions about outcomes. Only assert exact call sequences when that sequence is part of the requirement, not merely today's implementation.

Our sample parses the complete bounded batch before saving. Invalid input cannot cause a partial write, but database failure still needs a transaction policy in the real writer.

Detailed scenarios and executable checks, sections 023-025:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Resource_Safe_Components.md

#CSharp #DotNet #Testing
