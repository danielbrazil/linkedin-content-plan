# C# and .NET: beyond the definitions

Knowing a definition is a starting point. A useful interview answer also explains when a mechanism matters, why you would choose it, and what can go wrong.

This article answers the first 13 questions in the [content calendar](./LinkedIn_Content_Plan.md). Four companion LinkedIn drafts answer the same questions in shorter form; this article adds code and more detailed reasoning.

## Reading map

| LinkedIn draft | Questions | Focus |
|---|---|---|
| [Part 1/4](./CSharp_DotNet_Part_1_of_4_How_DotNet_Runs_Your_Code.md) | 001-003 | Platform, runtime, and compilation |
| [Part 2/4](./CSharp_DotNet_Part_2_of_4_Types_Copying_And_Equality.md) | 004-008 | Types, allocation, equality, and null |
| [Part 3/4](./CSharp_DotNet_Part_3_of_4_Generics_Delegates_And_Events.md) | 009-010 | Generic contracts and notifications |
| [Part 4/4](./CSharp_DotNet_Part_4_of_4_Garbage_Collection_And_Resources.md) | 011-013 | Memory and resource ownership |

## Running the examples

Each numbered C# example is an independent console program. Do not concatenate them: some deliberately reuse type names.

You need the .NET SDK, the Software Development Kit containing the compiler and development tools. These examples target .NET 8 or later and use no external packages.

Validation: all eight examples were compiled without warnings and executed as separate console programs targeting .NET 8 and .NET 10. Their output matched the expected output shown below.

In PowerShell, create a console project:

```powershell
dotnet new console -n InterviewExamples
Set-Location .\InterviewExamples
```

Replace the generated `Program.cs` with one complete C# block below, then run:

```powershell
dotnet run
```

The statements before the type declarations are **top-level statements**: C# supplies the program's entry method so the example does not need an explicit `Main` method.

## Part 1 - How .NET runs your code

### 001. How do .NET Framework, .NET Core, and modern .NET differ, and what drives a migration decision?

**Answer.** .NET is a development platform consisting of execution support, libraries, and tools. C# is a programming language that targets that platform.

- **.NET Framework** is the original Windows-only implementation. Existing applications may depend on technologies such as ASP.NET Web Forms, an older server-side web framework.
- **.NET Core** introduced a cross-platform, open-source implementation: developers can build and inspect its source, and supported workloads can run on multiple operating systems.
- **Modern .NET**, starting with .NET 5, continues the .NET Core line under the shorter name. .NET 6, 7, 8, 9, and 10 are versions in this line, not unrelated frameworks.

**When it matters.** As of November 2026, prefer **.NET 10 LTS (Long Term Support)** for new projects unless a concrete dependency or platform requirement prevents it. LTS provides three years of support from the release date; .NET 10 is supported until November 14, 2028. Use the latest available patch, an update containing security and reliability fixes, to remain supported. Confirm compatibility with your libraries and deployment environment. See the [official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

For a migration, inventory libraries, application frameworks, operating systems, deployment requirements, and tests before changing the target.

**Real-world scenario.** A team building a new order service to run on Linux can choose .NET 10 LTS after checking its dependencies. A company maintaining a Web Forms billing application may first extract pricing logic into a reusable library, then replace web screens incrementally. Keeping the existing application on a supported environment during that transition can be more realistic than promising an immediate rewrite.

**Trade-off and failure mode.** Migration can provide newer capabilities and deployment options, but it can require replacing unsupported components. A Web Forms application cannot become an ASP.NET Core application merely by changing a version number.

Cross-platform does not mean every workload supports every operating system. For example, Windows-specific desktop frameworks remain Windows-specific. Also, "old" and "unsupported" are different: check the official support policy for the exact release and environment.

### 002. What responsibilities belong to the CLR, CTS, and Common Language Specification?

**Answer.**

| Term | Meaning | Practical purpose |
|---|---|---|
| CLR | Common Language Runtime | The execution engine providing services such as garbage collection, exception handling, and type safety |
| CTS | Common Type System | Rules for defining and using types consistently across .NET |
| CLS | Common Language Specification | Defines a set of rules that .NET languages should follow to ensure interoperability |

**Garbage collection** means automatically reclaiming managed memory that is no longer reachable; question 011 explains reachability. **Exception handling** is the mechanism for reporting and responding to failures. **Type safety** means operations respect type rules instead of treating arbitrary data as interchangeable.

**When it matters.** Suppose a C# library will be consumed from another .NET language. Common execution does not guarantee that the other language exposes every C# feature. Consider CLS compatibility in the library's **public API**, the types and operations callers can access.

**Real-world scenario.** A C# pricing library is called from a Visual Basic billing application. The CLR executes both, and CTS rules give their types shared meaning. For a CLS-compatible public method, prefer an appropriate signed integer type over `uint`, an unsigned integer type outside the CLS. Unsigned means it cannot represent negative values. Choose a type large enough for the business range; changing the signature without checking that range can introduce a different bug.

**Trade-off and failure mode.** A broader C# feature set may be appropriate inside an application, but exposing language-specific assumptions can make a shared library harder to use. The CLR is an execution service, CTS is a type model, and CLS is an interoperability contract; these are not three names for the same thing. **Interoperability** means different languages or components can work together.

### 003. How do IL, JIT compilation, and Native AOT fit into .NET execution?

**Answer.** In a typical build, the C# compiler produces an **assembly**, a compiled unit usually stored in a DLL or executable. It contains **IL**, Intermediate Language instructions, and **metadata**, descriptions of types and members.

With **JIT**, Just-In-Time compilation, the runtime translates methods into native processor instructions as needed. Runtime information can support later optimization.

With **Native AOT**, Ahead-Of-Time compilation, publishing produces native machine code for a particular operating system and processor architecture. No JIT compiler is used when the application runs. Runtime services such as garbage collection still exist: AOT does not mean manual memory management.

**When it matters.** Native AOT can be attractive when startup time and memory use matter, such as short-lived processes. JIT-based deployment can suit applications that depend on runtime flexibility or libraries not compatible with AOT.

**Real-world scenarios.**

- **AOT candidate:** a small command-line configuration validator launched once per build. If the program runs briefly and often, reducing startup overhead may improve its total duration. Known dependencies make compatibility easier to evaluate. Compare complete execution time and memory against JIT-based publishing before adopting it.
- **Not suitable without redesign:** a reporting application that discovers arbitrary managed plug-ins at runtime. A plug-in is an extra module loaded to extend behavior; a managed assembly is compiled .NET code. Native AOT does not support loading new managed assemblies for execution this way. Keep a compatible JIT-based deployment or redesign the extension boundary.
- **Benchmark rather than assume:** a long-running order service may spend little of its lifetime starting up. Its steady-state processing rate, library compatibility, and deployment needs may matter more than startup gains. Long-running does not automatically make AOT unsuitable.

**Practical decision.** Your team maintains both a configuration validator and a reporting application. For the validator, publish JIT-based and Native AOT builds for the same operating system and processor architecture. Run both against the same representative files and measure total execution time, peak memory, and correctness across repeated runs. Choose AOT only if the measured benefit justifies the extra publishing requirements and all dependencies are compatible; otherwise retain JIT. For the reporting application, the requirement to load newly supplied managed plug-ins for execution rules out Native AOT without redesign. Keep a compatible JIT-based deployment rather than sacrificing a required feature for a possible startup improvement. These are decision criteria, not claimed benchmark results.

**Trade-offs and failure modes.**

- **Reflection**, examining types and members while a program runs, may need additional information to work safely with AOT.
- **Trimming**, removing code considered unused during publishing, can remove members accessed only through patterns the build tools cannot identify.
- **Runtime code generation**, creating executable code after the program starts, is restricted in Native AOT.
- AOT publishing targets a specific operating system and processor architecture and requires appropriate build tools.

Investigate compatibility warnings rather than suppressing them to obtain a successful build. Neither deployment model wins every benchmark: measure startup, sustained processing rate, memory, and deployment constraints.

## Part 2 - Types, copying, equality, and null

### 004. How do value types and reference types differ in copying, mutation, and allocation?

**Answer.** With ordinary assignment:

- A value-type variable copies its value.
- A reference-type variable copies its reference, not the referenced object.

A **reference** is the managed connection to an object. **Mutation** means changing existing data. **Allocation** means reserving memory for data.

#### Example 1 - Copying a struct versus copying an object reference

```csharp
using System;

var firstValue = new ValueCounter { Count = 1 };
var secondValue = firstValue;
secondValue.Count = 2;
Console.WriteLine(firstValue.Count);

var firstObject = new ObjectCounter { Count = 1 };
var secondObject = firstObject;
secondObject.Count = 2;
Console.WriteLine(firstObject.Count);

struct ValueCounter
{
    public int Count;
}

class ObjectCounter
{
    public int Count;
}
```

Expected output:

```text
1
2
```

Changing the copied struct does not change the original. Both class variables, however, refer to the same object.

**When it matters.** This distinction determines whether a caller receives independent data or access to shared mutable state.

**Real-world scenario.** A delivery planner copies a coordinate struct before adjusting a proposed route. The original coordinates remain unchanged. By contrast, assigning an `Order` class instance to a second variable does not create an order snapshot: changing its status through either reference changes the shared order. Use an intentional snapshot model when independent state is required.

**Trade-off and failure mode.** A struct copy is not a **deep copy**, which would also duplicate the objects referenced by its members. Reference-type fields still refer to the same nested objects after copying.

Do not define the distinction as "stack versus heap." A **stack** holds execution-related data for a thread; the **managed heap** is memory the runtime manages for allocated objects. A struct may live inline inside a heap-allocated class instance or array, and boxing creates a heap-allocated copy. Storage and runtime optimizations depend on context.

The explanation above concerns ordinary assignment and ordinary argument passing. Parameters marked `ref`, `out`, or `in` introduce by-reference behavior: they allow a method to access a caller's storage rather than receiving an ordinary independent value copy.

### 005. When should you choose a class, struct, record class, or record struct?

**Answer.** Choose the required behavior before choosing a storage category.

| Choice | Typical fit | Main caution |
|---|---|---|
| Class | Shared identity and object lifecycle | Multiple callers can mutate the same object |
| Struct | Small values intended to be copied independently | Large copies cost time; mutable copies surprise callers |
| Record class | Reference-type data models with generated value-based equality | Does not guarantee deep immutability |
| Record struct | Value-type data models with generated value-based equality | Still has copying costs and may be mutable |

**Identity** means an object remains a particular entity even when its data changes. **Value semantics** means the represented data, rather than a unique object identity, determines how a value should be treated.

**Immutable** means data cannot change after construction. A `readonly record struct` prevents reassignment of its instance state after initialization, but it does not freeze objects held through references.

Records provide compiler-generated value-based equality unless you customize it. By default, that comparison uses instance fields, including compiler-generated backing fields that store property values; a computed property does not add a separate stored value to compare. Record classes also use an **equality contract**, a runtime-type check that prevents records of different derived types from comparing equal merely because some data matches. Record structs do not have that inheritance-based equality contract. Member comparisons use each member type's equality rules, not an automatic deep comparison of nested objects.

#### Example 2 - Records compare members, but nested objects can still change

```csharp
using System;
using System.Collections.Generic;

var first = new Product("Keyboard", 75m);
var second = new Product("Keyboard", 75m);
Console.WriteLine(first == second);
Console.WriteLine(ReferenceEquals(first, second));

var names = new List<string> { "Ada" };
var original = new Team(names);
var copy = original with { };
copy.Members.Add("Grace");
Console.WriteLine(original.Members.Count);

public record Product(string Name, decimal Price);
public record Team(List<string> Members);
```

Expected output:

```text
True
False
2
```

`decimal` is a base-10 numeric type often used for monetary values; `75m` is a decimal literal. `List<string>` is a mutable sequence of strings.

The two products have equal member values but separate identities. The record's `with` expression creates a shallow copy: a new outer record that still shares the list. Generated equality compares members using their own equality behavior; it does not recursively compare every collection's contents.

**When it matters.** Records can remove repetitive equality code for data models. Classes often suit entities whose identity is independent of their current field values.

**Real-world scenario.** Model an order with a class when its identity must persist as its status changes. Consider a record class for delivery-address data compared by value, or a `readonly record struct` for a small immutable product identifier. These are candidates, not universal rules: check identity requirements, nullability (whether absence is allowed), and domain semantics (what the value means in the business). A struct has a default value even without explicit construction; decide whether a default identifier is valid, and use `ProductId?` if absence must be represented. If an address record contains a mutable list of instructions, changing that list still changes shared data: record syntax alone does not produce a safe historical snapshot.

**Trade-off and failure mode.** Do not choose structs just to "avoid garbage collection." Large copies or boxing may cancel the expected benefit. Do not assume a record containing a mutable collection is immutable or compares that collection by its elements.

### 006. What causes boxing and unboxing, and where do their costs appear?

**Answer.** **Boxing** converts a value type to `object` or an interface it implements, creating an object on the managed heap that contains a copy of the value. An **interface** specifies a contract that implementing types provide. **Unboxing** extracts that value through an explicit conversion to the correct stored value type. A **cast** is syntax that requests an explicit conversion; it does not automatically make an incompatible unboxing conversion valid.

#### Example 3 - The box holds a copy

```csharp
using System;

int number = 7;
object boxed = number;
number = 9;
Console.WriteLine((int)boxed);

int unboxed = (int)boxed;
long widened = unboxed;
Console.WriteLine(widened);
```

Expected output:

```text
7
7
```

Changing `number` does not change the boxed copy. A direct `(long)boxed` would throw `InvalidCastException`, an error reporting an invalid conversion: the box contains an `int`, not a `long`. Unbox as `int` first, then perform the numeric conversion.

**When it matters.** Repeated conversions on a **hot path**, code executed very frequently, can add allocation, copying, and garbage-collection work.

**Real-world scenario.** An import worker accumulates a million integer measurements. Storing them in `List<object>` boxes each integer; `List<int>` avoids boxing for storage and prevents accidentally adding a string. An older component may still require `object`, so keep the conversion at that boundary rather than changing the whole internal model. Profile the full import before claiming a measurable improvement.

**Trade-off and failure mode.** Using `object` can be convenient, but generic collections such as `List<int>` avoid boxing just to store integers. Not every operation on a struct or every interface-related operation necessarily boxes; the compiler and runtime can avoid boxing in some generic scenarios. Measure the actual operation.

### 007. How do reference equality, value equality, Equals, and GetHashCode interact?

**Answer.**

- **Reference equality** asks whether two references identify the same object. For reference-type instances, `ReferenceEquals` expresses that question.
- **Value equality** asks whether the represented data is considered equivalent.
- `Equals` provides a type's equality behavior. A plain class inherits reference-based behavior unless it changes it; records generate value-based behavior.
- `==` is an operator whose meaning depends on its operands. Types can overload it, meaning they provide their own implementation.
- `GetHashCode` produces a number used by **hash-based collections**, structures that use that number to locate candidate entries efficiently.

**Practical distinction.** For variables declared as `string`, `==` compares text content. Two separate `new object()` instances, however, compare unequal with both the default `==` and default `Equals`. Do not infer object identity from `==` without checking the operand types and their operator implementation.

Identical string literals such as `"Hello"` may share an instance through **string interning**, the reuse of an instance for identical text. Repeated occurrences of the same literal in the same assembly normally share that interned instance. Therefore, `ReferenceEquals` is not a reliable way to demonstrate different identities merely by assigning the same literal to two variables. Use `ReferenceEquals` when identity is the actual question, and string equality when content is the question.

A `Dictionary<TKey, TValue>` associates keys with values. It first uses a key's hash code and then checks equality among candidates. Equal keys must produce equal hash codes. Different keys can produce the same hash code; that is a **collision**, not proof of equality.

#### Example 4 - Value equality supports dictionary lookup

```csharp
using System;
using System.Collections.Generic;

var firstKey = new ProductKey(42);
var equivalentKey = new ProductKey(42);
var stock = new Dictionary<ProductKey, int>
{
    [firstKey] = 5
};

Console.WriteLine(firstKey.Equals(equivalentKey));
Console.WriteLine(stock[equivalentKey]);

public readonly record struct ProductKey(int Id);
```

Expected output:

```text
True
5
```

**When it matters.** Correct equality is essential for dictionary keys, duplicate detection, and sets, which are collections of unique values.

**Real-world scenario.** A stock lookup constructs a new `ProductKey(42)` from an incoming request. It should find the entry created using another `ProductKey(42)`, as example 4 demonstrates. A plain class with default reference equality would treat the two instances as different keys. Conversely, making a mutable product name part of a key and renaming it after insertion can make its stored entry difficult to find.

**Trade-off and failure mode.** Override `Equals` and `GetHashCode` consistently. Do not change equality-relevant fields while an object is a dictionary key: its current hash code may no longer locate the entry stored under its earlier hash code.

Do not persist hash codes as stable identifiers or treat them as cryptographic hashes, which are designed for different security-related properties. Ordinary hash codes are not guaranteed stable across processes.

### 008. How do nullable value types differ from nullable reference types?

**Answer.** `null` represents absence, but the two features implement it differently:

- `int?` means `Nullable<int>`, a value type that holds an integer or the absence of one.
- `string?` is a compiler annotation indicating a reference is allowed to be null. It does not create a different runtime type from `string`.

**Nullable-reference analysis** is **static analysis**, checking code without executing it. The compiler follows whether a reference may be null and reports warnings for potentially unsafe use. It does not insert automatic runtime null checks or prevent null assignments at runtime. `string` and `string?` are the same runtime type; their annotations express different expectations to the compiler, not different execution-time guarantees.

#### Example 5 - Express absence, then handle it

```csharp
#nullable enable
using System;

int? attempts = null;
Console.WriteLine(attempts.HasValue);
Console.WriteLine(attempts ?? 0);

string? name = FindName();
if (name is null)
{
    Console.WriteLine("No name supplied");
}
else
{
    Console.WriteLine(name.Length);
}

static string? FindName() => null;
```

Expected output:

```text
False
0
No name supplied
```

`HasValue` reports whether a nullable value contains data. `??` supplies an alternative when the left side is null. `is null` checks for absence. `#nullable enable` turns on nullable-reference annotations and warnings for this source.

**When it matters.** Nullable declarations make absence explicit in function contracts and data models.

**Real-world scenario.** A delivery estimate of `int?` distinguishes "not calculated yet" from zero days. Optional delivery instructions can be `string?`. Handle absence deliberately: do not automatically replace an unknown estimate with zero and tell a customer delivery is immediate. Data received from outside the application still needs validation because annotations do not enforce a runtime contract.

**Trade-off and failure mode.** Reading `Nullable<T>.Value` without a value throws an exception. Using `!`, the null-forgiving operator, merely suppresses a compiler warning; it does not make a null reference safe. Validate external inputs even when a parameter is declared non-nullable.

## Part 3 - Flexible APIs without losing type safety

### 009. How do generics, constraints, covariance, and contravariance affect API design?

**Answer.** **Generics** let types and methods use a **type parameter**, a placeholder such as `T`, while preserving checks for the actual type supplied. A **constraint** limits the acceptable types and tells the compiler which operations are available.

For example, `where T : IDisposable` requires `T` to implement `IDisposable`, allowing generic code to call `Dispose()`, the cleanup method described in question 012. This enforces the interface contract, not the implementation's correctness: it does not guarantee that calling the method successfully releases a resource.

**Inheritance** allows a more specific type, such as `Dog`, to be based on a broader type, such as `Animal`. **Variance** describes when those relationships allow substitutions in generic interfaces and delegates.

- **Covariance**, declared with `out`, allows a producer of a more specific reference type to be used as a producer of a broader one.
- **Contravariance**, declared with `in`, allows a consumer of a broader reference type to be used as a consumer of a more specific one.
- **Invariance** requires the original type argument; neither substitution is permitted.

A **delegate** is a type-safe reference to callable code; question 010 expands this.

#### Example 6 - Produce dogs, consume animals

```csharp
using System;
using System.Collections.Generic;

IEnumerable<Dog> dogs = new List<Dog> { new Dog() };
IEnumerable<Animal> animals = dogs;
foreach (Animal animal in animals)
{
    Console.WriteLine(animal.GetType().Name);
}

Action<Animal> inspectAnimal = animal =>
    Console.WriteLine("Inspecting " + animal.GetType().Name);
Action<Dog> inspectDog = inspectAnimal;
inspectDog(new Dog());

UseResource(new DemoResource());

static void UseResource<T>(T resource) where T : IDisposable
{
    using (resource)
    {
        Console.WriteLine("Resource in use");
    }
}

class Animal { }
class Dog : Animal { }

sealed class DemoResource : IDisposable
{
    public void Dispose() => Console.WriteLine("Resource disposed");
}
```

Expected output:

```text
Dog
Inspecting Dog
Resource in use
Resource disposed
```

`IEnumerable<T>` describes a sequence you can iterate over; here the contract provides values, not insertion methods. `Action<T>` represents a callable operation taking one argument and returning no value. `GetType().Name` reports an object's runtime type name. `sealed` prevents another class from inheriting from the declared class.

The `using` statement ensures cleanup when control leaves its block. In this example, `UseResource` explicitly takes responsibility for cleanup; that ownership decision must be part of a real method's contract.

**When it matters.** Exposing a sequence for reading can permit safe substitutions that exposing a mutable list does not.

**Real-world scenario.** An export routine works with `Invoice` and `Receipt` models through a generic type parameter. A report reader accepting `IEnumerable<Document>` can also consume `IEnumerable<Invoice>` when `Invoice` inherits from `Document`; that is covariance. An audit delegate that accepts any `Document` can be reused where an `Action<Invoice>` is required; that is contravariance. But a mutable `List<Invoice>` cannot be exposed as `List<Document>`, because a caller could insert a receipt. A cleanup-owning generic helper can require `IDisposable` rather than guessing whether each type has a cleanup method.

**Trade-off and failure mode.** `List<Dog>` cannot be assigned to `List<Animal>`. If it could, a caller could insert a `Cat`, breaking the promise that the original list contains dogs. Variance conversions require appropriately declared interfaces or delegates and reference-type arguments; `IEnumerable<int>` does not become `IEnumerable<object>` through covariance.

Also, accepting `IEnumerable<T>` does not make the underlying collection immutable. It only restricts what this particular interface exposes.

### 010. How do delegates, events, and lambda expressions differ?

**Answer.**

- A **delegate** identifies callable code with specified parameter and return types. It can reference a **static method**, which belongs to a type without requiring an object instance; an **instance method**, called on a particular object; or an **anonymous function**, an unnamed function such as a lambda.
- A **lambda expression** is concise syntax for an unnamed function. It can provide a delegate's implementation; in other contexts it can describe code as data for another component to inspect.
- An **event** controls subscription to a notification. Outside the declaring type, callers can attach and detach handlers but cannot raise the event or replace the subscription list.

A **publisher** raises an event. A **subscriber** attaches a **handler**, the code that runs in response.

#### Example 7 - Supply an operation; subscribe to a notification

```csharp
using System;

Func<decimal, decimal> addTax = price => price * 1.10m;
Console.WriteLine(addTax(100m) == 110m);

var publisher = new OrderPublisher();
EventHandler handler = (sender, args) =>
    Console.WriteLine("Order notification received");

publisher.OrderPlaced += handler;
publisher.PlaceOrder();
publisher.OrderPlaced -= handler;
publisher.PlaceOrder();

sealed class OrderPublisher
{
    public event EventHandler? OrderPlaced;

    public void PlaceOrder()
    {
        Console.WriteLine("Order placed");
        OrderPlaced?.Invoke(this, EventArgs.Empty);
    }
}
```

Expected output:

```text
True
Order placed
Order notification received
Order placed
```

`Func<decimal, decimal>` accepts one decimal and returns a decimal. `EventHandler` is a standard delegate receiving the publisher and event information. `EventArgs.Empty` means no additional data accompanies this notification. `?.Invoke` calls the handler list only when it is not null.

`+=` subscribes and `-=` unsubscribes. Saving the handler makes it possible to remove the same delegate later.

**When it matters.** Use a delegate to supply an operation, such as a calculation. Use an event to announce that something happened while controlling who can publish the notification.

**Real-world scenario.** A checkout calculator receives a discount function as a delegate; a lambda supplies the current promotion's calculation. After an order changes, an event can notify an open desktop screen to refresh. That screen should unsubscribe when it closes. Do not treat this in-process event, a notification within one running program, as a reliable message to another service: it does not persist notifications or retry delivery after a crash. A separate messaging mechanism is needed for those requirements.

**Trade-off and failure mode.** Events are not automatically asynchronous: these handlers run during the publishing call. A handler's exception can interrupt the call. A long-lived publisher can also keep subscribers alive through their handlers. End subscriptions when they are no longer needed.

## Part 4 - Garbage collection and resource ownership

### 011. How does generational garbage collection determine which objects can be reclaimed?

**Answer.** The **garbage collector**, or GC, finds objects that are no longer **reachable** and reclaims their managed memory.

Reachability means references form a path from a **root**, an execution starting point such as a static field, a reference held by running code, or a runtime-maintained reference, to an object.

The collector does not ask whether a developer still wants an object. If a valid path keeps it reachable, it remains alive.

The managed heap is organized around generations:

- **Generation 0:** where most new small objects begin.
- **Generation 1:** an intermediate group for survivors.
- **Generation 2:** longer-lived survivors.

Surviving objects can be **promoted**, meaning they become part of an older generation. Younger generations are collected more frequently because many allocations are short-lived. A collection of an older generation also includes the younger generations.

Large objects are typically allocated on the **Large Object Heap (LOH)**, which is collected as part of generation 2 garbage collections. Their behavior is not identical to the normal small-object promotion path.

**When it matters.** A service producing many temporary objects may spend time repeatedly collecting them. Long-lived retained objects can increase the work of older-generation collections.

**Real-world scenario.** An order service builds temporary response objects on every request. At high traffic, frequent allocation can increase collection work. Measure allocation rate, collection pauses, and response time; remove unnecessary intermediate objects if measurements justify it. If memory instead grows because a static list retains every response, reducing temporary allocations does not fix that retention bug.

**Trade-off and failure mode.** Automatic reclamation reduces manual memory-management errors, but garbage collection consumes processor resources and may temporarily suspend managed application threads. **Background GC** performs parts of a collection while application threads continue running, though some phases still require suspension. **Workstation GC** and **server GC** are configurations tuned for different workloads, such as interactive applications and high-throughput services. These configurations and background collection affect how work and pauses are scheduled; a collection is not necessarily one continuous application-wide pause. Generation is not a fixed age in seconds. Collection timing is adaptive, not a predictable schedule.

Do not add routine `GC.Collect()` calls to treat unexplained memory growth. Determine whether the issue is allocation rate, retained objects, unmanaged resources, or expected runtime memory behavior first.

### 012. How do managed resources differ from unmanaged resources?

**Answer.** **Managed memory** is memory tracked by the GC. **Unmanaged resources** are resources outside that automatic memory-reclamation model, such as operating-system file handles. A **handle** is an identifier used to access an external resource.

A managed object can wrap an unmanaged resource. For example, a `FileStream` is a managed object that wraps an underlying operating-system file handle. When the stream owns that handle, disposing the stream releases it. The wrapper's eventual collection is not a promise of prompt handle release.

`IDisposable` defines `Dispose`, a method for explicit cleanup. `using` arranges for `Dispose` to run when control leaves the scope, including during exception handling.

#### Example 8 - Release a file handle at a known point

```csharp
using System;
using System.IO;

string path = Path.GetTempFileName();
try
{
    using (var stream = new FileStream(
        path, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Console.WriteLine(stream.CanRead);
    }

    using (var reopened = new FileStream(
        path, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Console.WriteLine(reopened.CanRead);
    }
}
finally
{
    File.Delete(path);
}
```

Expected output:

```text
True
True
```

`Path.GetTempFileName` creates a uniquely named temporary file. `FileShare.None` requests exclusive access: reopening succeeds after the first stream has been disposed. The `finally` block attempts to delete this example's temporary file whether the work succeeds or an exception occurs. It does not hide exceptions.

`IAsyncDisposable` defines `DisposeAsync`. **Asynchronous cleanup** can complete without blocking a thread while waiting for an operation. In an asynchronous method, `await using` waits for that cleanup before leaving the scope.

A **finalizer** is a runtime-invoked fallback cleanup method. Its timing is not predictable and it is not guaranteed to run at process exit. **SafeHandle** is a .NET helper for owning native handles and supporting reliable release; prefer that established mechanism over inventing finalizer logic when wrapping a native handle.

**When it matters.** Files, connections, and other limited resources need clear ownership and timely release, not just eventual memory reclamation.

**Real-world scenario.** A nightly import worker opens thousands of files. Wrap each owned stream in `using` so the operating-system handle is released after that import, including if parsing fails. Waiting for collection can leave files locked or consume limited handles. If the worker receives a shared stream from its caller, agree on who closes it rather than disposing it unconditionally. Example 8 demonstrates explicit file-handle ownership and reopening.

**Trade-off and failure mode.** The GC does not automatically invoke `Dispose`. Disposing an object does not force its managed memory to be collected. An owner should release its resource; a borrower should not dispose a dependency unless the ownership contract gives it that responsibility. Cleanup also does not necessarily mean destroying the underlying resource: for example, a connection wrapper may return a connection to a reusable pool.

### 013. Why can a managed .NET application still leak memory?

**Answer.** Managed memory can be retained unintentionally while still being reachable. The GC correctly preserves it, even though the application no longer needs it.

Common causes include:

- A **static collection**, shared through a type rather than a short-lived instance, that grows indefinitely.
- A **cache**, stored data kept for reuse, with no size limit or expiration.
- Event subscriptions where a publisher outlives its subscribers.
- A **closure**, a function together with references to variables it captures, retained longer than intended.

The event example in question 010 shows an important cleanup step: remove the stored handler when the subscription should end. The handler may refer to a subscriber instance; while the publisher retains the handler, that instance can remain reachable.

**When it matters.** Long-running services can slowly retain data across many requests or jobs even when each individual operation appears correct.

**Real-world scenario.** A price service caches every product requested, with no size limit or expiration. Its dictionary remains reachable for the service's lifetime, so the GC cannot remove its entries. Set a retention policy based on data freshness and memory needs, then confirm that retained memory stabilizes under representative traffic. Separately, a long-lived publisher can retain closed screens through event handlers; expiring cache entries does not fix those subscriptions.

**Trade-off and failure mode.** Caches deliberately retain memory for speed; the problem is unbounded or unnecessary retention, not retention itself. Calling the GC cannot reclaim objects that remain reachable.

A **memory profiler** helps identify retained objects and inspect the reference paths preventing them from being garbage collected. Exact capabilities depend on the tool. Where supported, compare **heap snapshots**, records of live objects at particular times, and follow **paths to roots**, the reference chains keeping an object alive. Fix the lifetime or retention rule rather than merely collecting more often.

High process memory alone does not prove a managed leak: memory reserved by the runtime, native allocations outside the managed heap, or a growing amount of legitimate work can also explain it.

## Interview recap

| Question | Direct answer | Decision or pitfall |
|---|---|---|
| How do .NET Framework, .NET Core, and modern .NET differ, and what drives a migration decision? | Modern .NET continues the .NET Core line; Framework is Windows-only | Check compatibility and migration cost |
| What responsibilities belong to the CLR, CTS, and Common Language Specification? | CLR executes, CTS defines type rules, CLS supports language interoperability | Design shared public APIs deliberately |
| How do IL, JIT compilation, and Native AOT fit into .NET execution? | JIT compiles during execution; Native AOT compiles during publishing | Measure startup and check dynamic-feature compatibility |
| How do value types and reference types differ in copying, mutation, and allocation? | Values copy as values; references copy as references | Shared nested objects remain shared |
| When should you choose a class, struct, record class, or record struct? | Choose identity, copying, and equality behavior deliberately | Records do not guarantee deep immutability |
| What causes boxing and unboxing, and where do their costs appear? | Boxing allocates a boxed value; unboxing extracts its actual value type | Hot-path allocations and invalid casts |
| How do reference equality, value equality, Equals, and GetHashCode interact? | Equality defines equivalence; hash codes organize lookup | Equal values need equal hashes, not the reverse |
| How do nullable value types differ from nullable reference types? | Nullable values carry absence; nullable references add compiler analysis | Warnings do not enforce runtime checks |
| How do generics, constraints, covariance, and contravariance affect API design? | Generics preserve type relationships; variance permits safe substitutions | Mutable lists are invariant |
| How do delegates, events, and lambda expressions differ? | Delegates call code, lambdas express it, events control subscription | Publishers can retain subscribers |
| How does generational garbage collection determine which objects can be reclaimed? | GC reclaims unreachable managed objects using generations | Collection has costs and adaptive timing |
| How do managed resources differ from unmanaged resources? | GC-managed memory and external-resource ownership are separate | Dispose owned resources explicitly |
| Why can a managed .NET application still leak memory? | Reachable objects can be retained unintentionally | Fix retention and lifetime rules |

## Official references

- [Introduction to .NET](https://learn.microsoft.com/en-us/dotnet/core/introduction)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Common Language Runtime](https://learn.microsoft.com/en-us/dotnet/standard/clr)
- [Common Type System](https://learn.microsoft.com/en-us/dotnet/standard/base-types/common-type-system)
- [Language independence and CLS](https://learn.microsoft.com/en-us/dotnet/standard/language-independence)
- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Value types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types)
- [Records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Boxing and unboxing](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing)
- [Equality comparisons](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/equality-comparisons/)
- [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
- [Covariance and contravariance](https://learn.microsoft.com/en-us/dotnet/standard/generics/covariance-and-contravariance)
- [Events](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/)
- [Garbage collection fundamentals](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals)
- [Implementing the Dispose pattern](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
