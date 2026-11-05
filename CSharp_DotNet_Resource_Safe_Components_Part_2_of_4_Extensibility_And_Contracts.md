# Designing a resource-safe C# component | Part 2/4

An interface makes implementations interchangeable in code. Their behavior must make them interchangeable in practice.

17\. Open/closed principle and the strategy pattern?

The Open/Closed Principle encourages extending behavior without repeatedly changing stable code.

A strategy is a replaceable implementation of an operation. Our `IProductRowParser` is a parsing strategy: the importer calls `Parse(line)` without knowing the supplier's format.

Scenario: introduce a parser for a new supplier and select it where the application creates its dependencies. The import loop stays unchanged.

"Closed for modification" does not mean code must never change. New validation policy may legitimately require changing the importer. Avoid building a plug-in framework before there is a real need.

18\. What violates Liskov substitution?

The Liskov Substitution Principle says an implementation must preserve the behavior promised by its contract.

Suppose `SaveAsync(rows, cancellationToken)` promises that successful completion means the supplied batch was saved.

A writer that silently skips products it dislikes violates that promise, even if it implements the interface perfectly.

Likewise, a parser that returns null for invalid rows violates our contract: return a valid product or throw a format error.

Define the promised inputs, outputs, failures, and side effects. A matching method signature alone does not guarantee substitutability.

19\. Interface segregation and dependency inversion?

Interface segregation means callers should not depend on operations they do not need.

Our importer needs `SaveAsync`, not a giant storage interface with reporting, deletion, and administration methods.

Dependency inversion means the import policy depends on contracts rather than a specific database implementation.

Constructor injection supplies those collaborators from outside. It is a technique for wiring dependencies, not a requirement to use a dependency-injection framework.

Scenario: test the importer using a fake writer that stores rows in memory, then supply a database writer in the real application.

That makes focused tests easier, but does not prove database behavior. Test the actual writer separately against its database.

The trade-off: useful boundaries isolate changing requirements; unnecessary interfaces add navigation and maintenance without useful flexibility.

Next: immutable data and explicit resource ownership.

Detailed scenarios and runnable C# example, sections 017-019:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Resource_Safe_Components.md

#CSharp #DotNet #SoftwareDesign
