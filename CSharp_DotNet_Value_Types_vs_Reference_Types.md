# Value types vs. reference types: beyond stack and heap

This early draft is replaced by the [four-part series and companion article](./CSharp_DotNet_Beyond_The_Definitions.md). Retained for review history; do not publish it alongside the replacement drafts.

"Value types live on the stack. Reference types live on the heap."

If that is your answer in a C# interview, you are missing the most useful distinction.

Start with what happens when you copy a variable.

With ordinary assignment:
- A value-type variable copies its value.
- A reference-type variable copies its reference, not the object.

Here is a small example. What will each line print?

```csharp
using System;

var firstValue = new ValueCounter { Count = 1 };
var secondValue = firstValue;
secondValue.Count = 2;
Console.WriteLine(firstValue.Count); // 1

var firstObject = new ObjectCounter { Count = 1 };
var secondObject = firstObject;
secondObject.Count = 2;
Console.WriteLine(firstObject.Count); // 2

struct ValueCounter
{
    public int Count;
}

class ObjectCounter
{
    public int Count;
}
```

The struct was copied. The class instance was not: both references point to the same object.

Important caveat: copying a struct is not a deep copy. If it contains a reference-type field, that reference is copied too, so both structs can still refer to the same nested object.

So, what about memory?

Storage depends on context. A struct can be stored inline inside a heap-allocated object or array. Boxing a value into object creates a boxed copy on the managed heap.

"Value type" does not mean "always on the stack."

Three interview follow-ups worth connecting:

1. Class or struct?
Think about identity, value semantics, size, and copying costs. A struct is not automatically a performance improvement. The mutable struct above illustrates copying; it is not a recommendation to design mutable structs.

2. What about records?
A record class is a reference type. A record struct is a value type. Records provide value-based equality, but do not automatically make all nested data immutable.

3. Why does boxing matter?
It introduces allocation and copying costs that can add up on hot paths. Measure before optimizing.

My takeaway: explain copying and shared state first. Discuss allocation in context, rather than treating stack versus heap as the definition.

Which follow-up would you ask next: boxing, equality, or passing arguments with ref?

#CSharp #DotNet #InterviewPreparation #LearningInPublic

## Technical references - not part of the LinkedIn post

- [Microsoft Learn - Value types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types)
- [Microsoft Learn - Boxing and unboxing](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing)
- [Microsoft Learn - Records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
