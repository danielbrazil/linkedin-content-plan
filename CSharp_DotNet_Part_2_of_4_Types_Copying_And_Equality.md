# C# and .NET: beyond the definitions - Part 2/4

C# and .NET: beyond the definitions | Part 2/4
"Value types live on the stack" is not the definition.

4\. Value types vs. reference types?

Ordinary assignment copies a value-type variable's value, but a reference-type variable's reference, not its object.

Scenario: a copied coordinate struct can change independently. Copying an Order class reference shares the order: status changes affect both references. Structs with references can still share nested objects.

Allocation means reserving memory. A struct can live inside an object on the managed heap. The garbage collector (GC) reclaims unreachable objects during collection, not immediately. It does not manage all memory or release resources such as file handles. Type category alone does not determine storage.

5\. Class, struct, record class, or record struct?

Use a class for an Order whose identity persists as its status changes.

A struct suits small, preferably immutable values, unchanged after creation, such as coordinates. Large structs cost more to copy; mutable copies can surprise callers.

A record class is a reference type; a record struct is a value type. Both provide compiler-generated value equality; record classes also check runtime type. Consider a record class for address data or a readonly record struct for a small immutable ProductId. Check identity, nullability (whether absence is allowed), and domain meaning.

Records do not freeze nested objects.

6\. Boxing vs. unboxing?

Boxing creates an object containing a copy of a value. Unboxing extracts it through an explicit conversion to the correct stored value type.

Scenario: storing integer measurements in `List<object>` boxes each integer; `List<int>` avoids boxing for storage.

Unbox as the stored type: an int boxed as object cannot be cast directly to long. Repeated boxing adds allocation and copying costs.

7\. Reference equality, Equals, and GetHashCode?

Reference equality asks "same object?"; value equality asks "equivalent data?" Equals defines equality behavior. == is type-dependent: strings compare content, not references.

GetHashCode supplies a number used by Dictionary, a key/value lookup. Equal keys need equal hash codes; matching codes do not prove equality.

Scenario: two ProductId values for product 42 should locate the same stock entry. Changing equality-relevant key data after insertion can break lookup.

8\. int? vs. string?

int? is `Nullable<int>`: an integer or null. string? allows a null reference in compiler static analysis, checking code without running it. string and string? are the same runtime type.

Scenario: int? distinguishes an unknown delivery estimate from zero; string? models optional instructions. Warnings add no runtime null checks: validate external input.

More scenarios and code: article sections 004-008.
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Beyond_The_Definitions.md

#CSharp #DotNet #InterviewPreparation
