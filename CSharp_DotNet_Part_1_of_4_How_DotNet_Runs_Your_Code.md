# C# and .NET: beyond the definitions - Part 1/4

C# and .NET: beyond the definitions | Part 1/4
How does your code actually run?

1\. .NET Framework, .NET Core, or modern .NET?

.NET is a development platform: a runtime that executes code, libraries of reusable code, and development tools. C# is one language you can use with it.

.NET Framework is Windows-only. .NET Core introduced a cross-platform, open-source implementation. Modern .NET, starting with .NET 5, continues that line. Versions 6, 7, 8, 9, and 10 belong to the same platform.

As of November 2026, prefer .NET 10 LTS (Long Term Support) for new projects, using the latest patch for security and reliability fixes. Check dependency compatibility first.

Scenario: build a new Linux-hosted order service on .NET 10 LTS. An existing ASP.NET Web Forms application, an older Windows web technology, needs migration work rather than a version change. Check dependencies and cost: cross-platform does not mean every application framework supports every operating system.

2\. What are CLR, CTS, and CLS?

CLR means Common Language Runtime: the execution engine providing memory management, exception handling (responding to failures), and type safety (checking that operations respect type rules).

CTS, the Common Type System, defines how .NET types behave and interact.

CLS, the Common Language Specification, defines a set of rules that .NET languages should follow to ensure interoperability.

Scenario: a C# pricing library also used by Visual Basic code benefits from CLS-compatible public methods. Prefer a signed integer to uint, an unsigned integer type outside the CLS. Shared type rules do not make every C# feature usable in every language.

3\. What are IL, JIT, and Native AOT?

C# normally compiles into IL: Intermediate Language, processor-independent instructions.

With JIT, Just-In-Time compilation, methods are translated into processor instructions as needed during execution.

Native AOT, Ahead-Of-Time compilation, produces native machine code at publish time, without a JIT at runtime.

Native AOT can improve startup time and reduce runtime memory usage; benefits depend on the workload.

Decision example: for a command-line validator launched on every build, compare JIT and AOT execution time and memory. Choose AOT if measured gains justify its build requirements. For a reporting app loading new plug-ins (compiled .NET modules) at runtime, keep JIT: Native AOT cannot load those modules for execution.

AOT targets a specific operating system and processor architecture. Reflection, inspecting types at runtime, needs checks because trimming removes code considered unused. Measure startup, sustained performance, and compatibility.

Next: types, copying, equality, and null.

Expanded scenarios and C# examples: article sections 001-003.
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Beyond_The_Definitions.md

#CSharp #DotNet #InterviewPreparation
