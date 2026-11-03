# C# and .NET: beyond the definitions - Part 3/4

C# and .NET: beyond the definitions | Part 3/4
Flexible code should not require giving up type safety.

An API, Application Programming Interface, is the contract callers use to access code. Type safety means operations respect type rules.

9\. Generics, constraints, covariance, and contravariance?

Generics let code work with a type parameter, a placeholder such as T, while the compiler checks the actual types used.

Scenario: a generic export routine processes Invoice or Receipt data without treating every item as object.

A constraint limits accepted types. "where T : IDisposable" requires T to implement IDisposable, allowing generic code to call Dispose(), the cleanup method. The contract does not guarantee successful resource release.

Variance describes when related types can be substituted in a generic interface or delegate:

- Covariance, declared with out, supports producing a more specific type. A report reader accepting `IEnumerable<Document>` can read `IEnumerable<Invoice>` when Invoice inherits from Document. `IEnumerable<T>` describes a sequence you can iterate over.

- Contravariance, declared with in, supports consuming a broader type. `Action<Document>` can serve as `Action<Invoice>`: an audit operation handling any Document can handle an Invoice. `Action<T>` takes T and returns no value.

- Invariance forbids either substitution. `List<Invoice>` cannot become `List<Document>`: someone could add a Receipt.

These conversions apply to reference-type arguments in interfaces and delegates declared with the appropriate variance. `IEnumerable<int>` does not become `IEnumerable<object>` this way.

`IEnumerable<T>` exposes iteration, not methods to add or remove items. It does not guarantee immutability (data remaining unchanged): other code may modify the underlying collection.

10\. Delegates vs. events vs. lambdas?

A delegate is a type-safe reference to callable code: its parameter and return types define what can be called. It can reference a static method (no instance), an instance method (on an object), or an anonymous function (unnamed).

A lambda is a concise unnamed function, such as x => x * 2. It can supply the code a delegate invokes.

An event provides controlled subscription to notifications. Outside the declaring type, subscribers can attach or detach handlers; they cannot raise the event or replace its subscribers.

Scenario: supply a discount calculation as a delegate, written as a lambda. Raise an event to refresh an order screen after a local change. An event is not a durable message between services: notifications are not stored for later delivery.

Watch the lifetime: a long-lived publisher can keep a subscriber alive through its handler. Unsubscribe when the relationship ends.

Expanded scenarios and C# examples: article sections 009-010.
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Beyond_The_Definitions.md

#CSharp #DotNet #InterviewPreparation
