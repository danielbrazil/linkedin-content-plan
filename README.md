# Beyond the Interview Answer

A LinkedIn learning series connecting technical interview questions with practical full-stack engineering decisions.

## Topics

C# and .NET, ASP.NET Core, JavaScript and TypeScript, React, Angular, SQL Server, PostgreSQL, Entity Framework Core, and Azure.

## Content

- [Eight-week calendar and 200 interview questions](./LinkedIn_Content_Plan.md)
- [Series introduction - October 30, 2026](./Beyond_The_Interview_Answer_Series_Introduction.md) (draft)
- [C# and .NET: beyond the definitions - November 3, 2026](./CSharp_DotNet_Beyond_The_Definitions.md) (companion article draft, with code)
  - [Part 1/4 - How .NET runs your code](./CSharp_DotNet_Part_1_of_4_How_DotNet_Runs_Your_Code.md) (draft; questions 001-003)
  - [Part 2/4 - Types, copying, and equality](./CSharp_DotNet_Part_2_of_4_Types_Copying_And_Equality.md) (draft; questions 004-008)
  - [Part 3/4 - Generics, delegates, and events](./CSharp_DotNet_Part_3_of_4_Generics_Delegates_And_Events.md) (draft; questions 009-010)
  - [Part 4/4 - Garbage collection and resources](./CSharp_DotNet_Part_4_of_4_Garbage_Collection_And_Resources.md) (draft; questions 011-013)

- [Designing a resource-safe C# component - November 5, 2026](./CSharp_DotNet_Resource_Safe_Components.md) (companion article, with runnable code)
  - [Part 1/4 - Contracts and composition](./CSharp_DotNet_Resource_Safe_Components_Part_1_of_4_Contracts_And_Composition.md) (questions 014-016)
  - [Part 2/4 - Extensibility and contracts](./CSharp_DotNet_Resource_Safe_Components_Part_2_of_4_Extensibility_And_Contracts.md) (questions 017-019)
  - [Part 3/4 - Immutability and ownership](./CSharp_DotNet_Resource_Safe_Components_Part_3_of_4_Immutability_And_Ownership.md) (questions 020-022)
  - [Part 4/4 - Errors and testing](./CSharp_DotNet_Resource_Safe_Components_Part_4_of_4_Errors_And_Testing.md) (questions 023-025)
  - [Executable file-import example and checks](./examples/ResourceSafeImporter/Program.cs)

- [Async .NET: concurrency without confusion - November 10, 2026](./CSharp_DotNet_Async_Concurrency.md) (with runnable checks)
  - [Part 1/4 - Execution and workloads](./CSharp_DotNet_Async_Concurrency_Part_1_of_4_Execution_And_Workloads.md) (questions 026-028)
  - [Part 2/4 - Blocking, context, and coordination](./CSharp_DotNet_Async_Concurrency_Part_2_of_4_Blocking_Context_And_Coordination.md) (questions 029-031)
  - [Part 3/4 - Cancellation and return types](./CSharp_DotNet_Async_Concurrency_Part_3_of_4_Cancellation_And_Return_Types.md) (questions 032-034)
  - [Part 4/4 - Shared state and limits](./CSharp_DotNet_Async_Concurrency_Part_4_of_4_Shared_State_And_Limits.md) (questions 035-038)
  - [Executable async examples and checks](./examples/AsyncConcurrency/Program.cs)

- [From LINQ to an asynchronous data pipeline - November 12, 2026](./CSharp_DotNet_Linq_Data_Pipeline.md) (with runnable checks)
  - [Part 1/4 - Sequences and execution](./CSharp_DotNet_Linq_Data_Pipeline_Part_1_of_4_Sequences_And_Execution.md) (questions 039-041)
  - [Part 2/4 - Choosing operators](./CSharp_DotNet_Linq_Data_Pipeline_Part_2_of_4_Choosing_Operators.md) (questions 042-044)
  - [Part 3/4 - Memory, streaming, and cancellation](./CSharp_DotNet_Linq_Data_Pipeline_Part_3_of_4_Memory_Streaming_And_Cancellation.md) (questions 045-047)
  - [Part 4/4 - Channels, backpressure, and measurement](./CSharp_DotNet_Linq_Data_Pipeline_Part_4_of_4_Channels_Backpressure_And_Measurement.md) (questions 048-050)
  - [Executable pipeline examples and checks](./examples/LinqDataPipeline/Program.cs)

- [ASP.NET Core: from request to response - November 17, 2026](./AspNet_Core_Request_Response.md) (with runnable HTTP checks)
  - [Part 1/4 - Servers and middleware](./AspNet_Core_Request_Response_Part_1_of_4_Servers_And_Middleware.md) (questions 051-053)
  - [Part 2/4 - Services and settings](./AspNet_Core_Request_Response_Part_2_of_4_Services_And_Settings.md) (questions 054-057)
  - [Part 3/4 - Endpoints, input, and access](./AspNet_Core_Request_Response_Part_3_of_4_Endpoints_Input_And_Access.md) (questions 058-060)
  - [Part 4/4 - Tokens, browsers, and workers](./AspNet_Core_Request_Response_Part_4_of_4_Tokens_Browsers_And_Workers.md) (questions 061-063)
  - [Executable request pipeline and checks](./examples/AspNetRequestPipeline/PipelineChecks.cs)

- [Designing a production-ready REST endpoint - November 19, 2026](./AspNet_Core_Rest_Endpoint.md) (with runnable integration checks)
  - [Part 1/4 - Requests, retries, and errors](./AspNet_Core_Rest_Endpoint_Part_1_of_4_Requests_Retries_And_Errors.md) (questions 064-066)
  - [Part 2/4 - Contracts, versions, and pages](./AspNet_Core_Rest_Endpoint_Part_2_of_4_Contracts_Versions_And_Pages.md) (questions 067-069)
  - [Part 3/4 - Caching, limits, and HTTP clients](./AspNet_Core_Rest_Endpoint_Part_3_of_4_Caching_Limits_And_Http_Clients.md) (questions 070-072)
  - [Part 4/4 - Resilience, cancellation, and tests](./AspNet_Core_Rest_Endpoint_Part_4_of_4_Resilience_Cancellation_And_Tests.md) (questions 073-075)
  - [Executable order API and checks](./examples/RestOrders.Checks/Program.cs)

The calendar runs from November 2 through December 27, 2026:

- **Tuesday:** related interview questions, reasoning, and trade-offs.
- **Thursday:** concepts and practical examples.

The questions are a curated selection, not a statistically ranked list. Research sources are included in the calendar.

## Editorial workflow

Each post has its own Markdown file, named `Main_Topic_Part_N_of_Total_Descriptive_Title.md` for a multi-part series or `Main_Topic_Descriptive_Title.md` for standalone content. Companion articles use the main topic and article title. Filenames contain no dates; publication dates stay inside the documents.

Drafts are reviewed before the author manually publishes them on LinkedIn. A file being available in this public repository does not mean the post has been approved or published on LinkedIn. There is no automatic publishing.

Future articles and example code will be added as the series develops.

The November 5 concept topic is also split into four posts, each linking to the companion article in this repository. Local `_LinkedIn.txt` exports provide copy-ready versions with Unicode bold titles and questions. All files ending in `LinkedIn.txt` (case-insensitive) are excluded from Git and must stay local. No LinkedIn publishing is automated.

The November 3 roundup is split into four numbered posts on the same day. Together they answer all 13 planned questions directly on LinkedIn; the companion article adds detail rather than replacing those answers. Each post links to the companion article in this repository. The earlier standalone value-types draft is superseded.
