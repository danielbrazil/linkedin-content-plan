# LinkedIn content plan: November-December 2026

## Scope

- Audience: senior .NET / full-stack developers and technical interviewers.
- Language: English.
- Period: November 2-December 27, 2026.
- Publishing days: Tuesday and Thursday.
- Deliverables: 16 post outlines containing 200 distinct interview questions.
- Tuesday format: a roundup of related interview questions.
- Thursday format: a concept-led article or carousel, with examples to be developed later.
- This document contains topics and questions only: no answers, code, or finished posts.

### Research and selection

This is a curated 200-question selection, not a statistically verified "top 200" ranking. Public interview collections do not provide comparable employer-wide question-frequency data. Questions are independently worded and selected for stack relevance, foundational importance, and senior-level trade-offs.

Interview collections informed the subject areas; official documentation supplemented modern concepts and platform-specific distinctions. Not every question appears verbatim in a source. Legacy-only material was deprioritized. Source references appear by week and in the bibliography.

### Coverage constraint

Including all 200 questions in 16 posts means 25 questions per week: 13 on Tuesday and 12 on Thursday. That is too much for detailed answers in short text posts. Treat these as long-form article/carousel outlines, with closely related questions combined into sections. The Thursday title supplies the concept and proposed example context; the questions define its scope, not 12 separate examples.

## Calendar

| Week | Tuesday: interview roundup | Thursday: concept + planned example context | Question IDs |
|---|---|---|---|
| 1: Nov 2-8 | Nov 3 - C# and .NET: beyond the definitions | Nov 5 - Designing a resource-safe C# component | 001-025 |
| 2: Nov 9-15 | Nov 10 - Async .NET: concurrency without confusion | Nov 12 - From LINQ to an asynchronous data pipeline | 026-050 |
| 3: Nov 16-22 | Nov 17 - ASP.NET Core: from request to response | Nov 19 - Designing a production-ready REST endpoint | 051-075 |
| 4: Nov 23-29 | Nov 24 - SQL Server and PostgreSQL: queries under pressure | Nov 26 - Diagnosing a slow EF Core endpoint | 076-100 |
| 5: Nov 30-Dec 6 | Dec 1 - JavaScript: what actually runs in the browser? | Dec 3 - Type-safe API boundaries with TypeScript | 101-125 |
| 6: Dec 7-13 | Dec 8 - React: rendering, state, and hooks | Dec 10 - Building a resilient React search screen | 126-150 |
| 7: Dec 14-20 | Dec 15 - Angular: architecture and application boundaries | Dec 17 - Reactive Angular search with signals and RxJS | 151-175 |
| 8: Dec 21-27 | Dec 22 - Azure: choosing services for a .NET application | Dec 24 - Designing a reliable order-processing workflow | 176-200 |

## Week 1 - C# and .NET foundations and design

Research basis: [S1], [S8].

### Post 01 - Tuesday, November 3

**Title:** C# and .NET: beyond the definitions

**Format:** Interview-question roundup.

001. How do .NET Framework, .NET Core, and modern .NET differ, and what drives a migration decision?
002. What responsibilities belong to the CLR, CTS, and Common Language Specification?
003. How do IL, JIT compilation, and Native AOT fit into .NET execution?
004. How do value types and reference types differ in copying, mutation, and allocation?
005. When should you choose a class, struct, record class, or record struct?
006. What causes boxing and unboxing, and where do their costs appear?
007. How do reference equality, value equality, Equals, and GetHashCode interact?
008. How do nullable value types differ from nullable reference types?
009. How do generics, constraints, covariance, and contravariance affect API design?
010. How do delegates, events, and lambda expressions differ?
011. How does generational garbage collection determine which objects can be reclaimed?
012. How do managed resources differ from unmanaged resources?
013. Why can a managed .NET application still leak memory?

### Post 02 - Thursday, November 5

**Title:** Designing a resource-safe C# component

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A file-import component with replaceable dependencies and explicit resource ownership.

014. When should a design use an interface rather than an abstract base class?
015. When is composition preferable to inheritance?
016. How would you apply the single-responsibility principle to an application service?
017. How do the open/closed principle and strategy pattern support extensibility?
018. How can an implementation violate the Liskov substitution principle?
019. How do interface segregation and dependency inversion improve testability?
020. When should state be immutable, and what trade-offs does that introduce?
021. How should IDisposable, IAsyncDisposable, using, and await using express resource ownership?
022. When is a finalizer appropriate, and how does SafeHandle affect that decision?
023. How should exceptions be caught, translated, and rethrown without losing diagnostic information?
024. How do unit tests differ from integration tests for a component with external dependencies?
025. When should a test use a mock, stub, or fake?

## Week 2 - Asynchronous execution and data pipelines

Research basis: [S1], [S16].

### Post 03 - Tuesday, November 10

**Title:** Async .NET: concurrency without confusion

**Format:** Interview-question roundup.

026. What do async and await change about a method's execution?
027. How do Task, Thread, the thread pool, and Task.Run differ?
028. How should CPU-bound work and I/O-bound work be handled differently?
029. Why can Result, Wait, and other sync-over-async patterns cause problems?
030. What role do SynchronizationContext and ConfigureAwait play in asynchronous code?
031. How do Task.WhenAll and Task.WhenAny differ in coordination and failure handling?
032. How should CancellationToken propagate through an application?
033. When is async void appropriate, and why is it risky elsewhere?
034. When might ValueTask be preferable to Task?
035. How do race conditions arise when asynchronous operations share mutable state?
036. When should you use lock, SemaphoreSlim, or a concurrent collection?
037. How would you limit concurrency when calling an external API?
038. How would you diagnose thread-pool starvation in an ASP.NET Core service?

### Post 04 - Thursday, November 12

**Title:** From LINQ to an asynchronous data pipeline

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A paginated data feed that filters records and enriches them through bounded asynchronous calls.

039. How do IEnumerable, IQueryable, and IAsyncEnumerable differ?
040. What is deferred execution, and when does a LINQ query actually run?
041. How can multiple enumeration introduce unexpected work or inconsistent results?
042. How do Select and SelectMany differ when transforming nested data?
043. When should you use Any rather than Count to test for existence?
044. How do First, Single, and their OrDefault variants express different expectations?
045. How does materializing with ToList change execution timing and memory usage?
046. How do iterator methods and yield return support streaming?
047. How should cancellation and disposal work when consuming an asynchronous stream?
048. When is a Channel appropriate for a producer-consumer pipeline?
049. How can backpressure prevent a fast producer from overwhelming downstream work?
050. How would you measure allocations and throughput before optimizing a data pipeline?

## Week 3 - ASP.NET Core and API design

Research basis: [S2], [S9].

### Post 05 - Tuesday, November 17

**Title:** ASP.NET Core: from request to response

**Format:** Interview-question roundup.

051. What roles do Kestrel and a reverse proxy play in hosting ASP.NET Core?
052. How does middleware ordering affect the request pipeline?
053. When should cross-cutting behavior use middleware, an MVC filter, or an endpoint filter?
054. How do singleton, scoped, and transient dependency-injection lifetimes differ?
055. What is a captive dependency, and why is injecting a scoped service into a singleton dangerous?
056. How do appsettings.json, environment-specific configuration, environment variables, and secrets interact?
057. When should you use IOptions, IOptionsSnapshot, or IOptionsMonitor?
058. What trade-offs distinguish controllers from Minimal APIs?
059. How do model binding, validation, and serialization shape an API boundary?
060. How do authentication and authorization differ?
061. How should a JWT-based API validate tokens and enforce policy-based authorization?
062. What problem does CORS solve, and what does it not protect against?
063. How should BackgroundService manage scoped dependencies and graceful shutdown?

### Post 06 - Thursday, November 19

**Title:** Designing a production-ready REST endpoint

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** An authenticated order-creation endpoint with validation, safe retries, and integration tests.

064. How should HTTP methods and status codes express resource operations?
065. What makes an operation idempotent, and when should an API use idempotency keys?
066. How should centralized exception handling and Problem Details standardize API errors?
067. Why should request and response DTOs be separated from persistence entities?
068. How should an API evolve its contract and handle versioning?
069. How should an API expose filtering, sorting, and pagination?
070. When are ETags and conditional requests useful for caching or concurrency control?
071. How should rate limiting protect an endpoint without blocking legitimate clients unnecessarily?
072. Why use IHttpClientFactory, and how should outbound HTTP failures be handled?
073. Where should timeouts, retries, and circuit breakers be applied to an outbound dependency?
074. How should request-abort cancellation propagate to database and HTTP calls?
075. How would you integration-test routing, authentication, validation, and persistence with WebApplicationFactory?

## Week 4 - SQL Server, PostgreSQL, and EF Core

Research basis: [S6], [S10], [S11].

### Post 07 - Tuesday, November 24

**Title:** SQL Server and PostgreSQL: queries under pressure

**Format:** Interview-question roundup.

076. How do inner joins, outer joins, and cross joins affect result sets?
077. When should filtering occur in WHERE rather than HAVING?
078. When should a query use EXISTS rather than IN or a join?
079. How do window functions differ from GROUP BY, and when would you use ROW_NUMBER or RANK?
080. When should you choose a CTE, subquery, or temporary table?
081. How do normalization and deliberate denormalization affect consistency and performance?
082. How do SQL Server clustered and nonclustered indexes differ from PostgreSQL index and heap organization?
083. How do composite-index column order, included columns, and filtered or partial indexes affect queries?
084. How do execution plans, cardinality estimates, and statistics help diagnose slow queries?
085. What makes a predicate sargable, and how can functions or implicit conversions prevent efficient access?
086. How do ACID properties and isolation levels affect transaction correctness?
087. How do SQL Server locking and row-versioning options compare with PostgreSQL MVCC behavior?
088. How do you identify deadlocks and reduce them without weakening required consistency?

### Post 08 - Thursday, November 26

**Title:** Diagnosing a slow EF Core endpoint

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** An order-history query examined in both SQL Server and PostgreSQL.

089. What responsibilities and lifetime constraints does DbContext have?
090. When should a query use tracking, AsNoTracking, or identity resolution?
091. How do eager, explicit, and lazy loading affect round trips and data volume?
092. How do you detect and eliminate an N+1 query problem?
093. How can projection reduce unnecessary columns and entity materialization?
094. When should you choose a split query rather than a single query with multiple collection includes?
095. How do LINQ-to-SQL translation limits differ from client-side evaluation?
096. Why can keyset pagination outperform offset pagination, and what are its limitations?
097. How should EF Core migrations be reviewed and deployed safely across environments?
098. How do EF Core transactions, SaveChanges, and retrying execution strategies interact?
099. How should optimistic concurrency be implemented using SQL Server rowversion or a suitable PostgreSQL concurrency token?
100. How do parameterized queries prevent SQL injection when using EF Core, raw SQL, or Dapper?

## Week 5 - JavaScript and TypeScript

Research basis: [S3], [S12].

### Post 09 - Tuesday, December 1

**Title:** JavaScript: what actually runs in the browser?

**Format:** Interview-question roundup.

101. How do var, let, and const differ in scope, hoisting, and initialization?
102. How do lexical scope and closures affect captured variables?
103. How is this determined in regular functions, methods, and arrow functions?
104. How do call, apply, and bind change function invocation?
105. How do prototypes and JavaScript classes relate?
106. How do strict equality, coercion, null, and undefined affect comparisons?
107. How do shallow copies, deep copies, and shared references affect object mutation?
108. How do the call stack, task queue, and microtask queue determine execution order?
109. How do promises and async/await propagate success and failure?
110. When should you use Promise.all, allSettled, race, or any?
111. How do event propagation, bubbling, and delegation work in the DOM?
112. How do debouncing and throttling differ for user-input events?
113. How should AbortController and response ordering be used to prevent stale fetch results?

### Post 10 - Thursday, December 3

**Title:** Type-safe API boundaries with TypeScript

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A typed API client that accepts untrusted JSON and models loading, success, and failure.

114. What does TypeScript check at compile time, and what remains unchecked at runtime?
115. When should you use unknown, any, or never?
116. When should you choose an interface rather than a type alias?
117. How do union and intersection types express different constraints?
118. How do control-flow narrowing, type guards, and type predicates work?
119. How do discriminated unions support exhaustive handling of application states?
120. How do generics and generic constraints preserve useful relationships between types?
121. How do keyof, indexed-access types, and mapped types support reusable models?
122. When are Partial, Pick, Omit, Record, and Readonly useful?
123. How do strictNullChecks and optional properties affect API contracts?
124. How do satisfies, type assertions, and non-null assertions differ in safety?
125. How should runtime schema validation complement TypeScript at an external-data boundary?

## Week 6 - React

Research basis: [S4], [S13].

### Post 11 - Tuesday, December 8

**Title:** React: rendering, state, and hooks

**Format:** Interview-question roundup.

126. What happens during React's render and commit phases?
127. How do reconciliation and stable keys affect component identity?
128. How do props and state differ, and why should render logic remain pure?
129. Why does state behave like a snapshot, and when are functional updates necessary?
130. How does batching affect multiple state updates?
131. What causes a component to re-render?
132. What rules must hooks follow, and why does hook call order matter?
133. When is an Effect needed, and when should logic stay in rendering or an event handler?
134. How do Effect dependencies, cleanup, and stale closures interact?
135. How does development Strict Mode expose missing cleanup or impure behavior?
136. How do useRef and useState differ?
137. When should state be lifted, derived, colocated, or shared through context?
138. When should useReducer replace several related useState calls?

### Post 12 - Thursday, December 10

**Title:** Building a resilient React search screen

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A typed search screen with asynchronous results, filters, and accessible loading/error states.

139. How should reusable behavior be extracted into a custom hook?
140. How should a React screen represent loading, empty, error, and success states?
141. How should Effects or a data-fetching library prevent request races and handle cancellation?
142. How does server state differ from client UI state when choosing a state-management approach?
143. When do memo, useMemo, and useCallback help, and when do they add unnecessary complexity?
144. How should React profiling guide rendering optimizations?
145. How can context value design prevent unnecessary consumer updates?
146. How should controlled and uncontrolled form inputs be chosen?
147. What can error boundaries catch, and what errors require separate handling?
148. How do code splitting and lazy loading reduce initial application work?
149. How do SSR, client rendering, hydration, and React Server Components differ?
150. How would you test user behavior and keyboard accessibility with React Testing Library?

## Week 7 - Angular

Research basis: [S5], [S14].

### Post 13 - Tuesday, December 15

**Title:** Angular: architecture and application boundaries

**Format:** Interview-question roundup.

151. How do standalone components and NgModules differ in application organization?
152. How do components, directives, and pipes divide presentation responsibilities?
153. How do property binding, event binding, and two-way binding differ?
154. How should inputs, outputs, and services support component communication?
155. How do Angular's hierarchical injectors and provider scopes determine service instances?
156. When should dependencies be obtained through inject rather than constructor parameters?
157. How do lifecycle hooks coordinate initialization, view access, and cleanup?
158. What triggers Angular change detection, and how does OnPush change its behavior?
159. How do signals participate in Angular's change-detection model?
160. How does zoneless change detection affect application assumptions?
161. How do lazy-loaded routes, guards, and resolvers support navigation boundaries?
162. Why are route guards insufficient as the sole authorization mechanism?
163. When should a form use reactive forms rather than template-driven forms?

### Post 14 - Thursday, December 17

**Title:** Reactive Angular search with signals and RxJS

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A standalone search component with reactive input, cancellable requests, and derived UI state.

164. How do Observables and Promises differ in execution, cancellation, and value delivery?
165. How do cold and hot Observables differ?
166. When should you use Subject, BehaviorSubject, or ReplaySubject?
167. How do switchMap, mergeMap, concatMap, and exhaustMap express different concurrency policies?
168. How should debounceTime and distinctUntilChanged shape a search-input stream?
169. How do catchError and retry placement affect stream survival and recovery?
170. How do the async pipe and takeUntilDestroyed manage subscription cleanup?
171. How do writable signals, computed signals, and effects differ in responsibility?
172. How do toSignal and toObservable bridge signals and RxJS?
173. How should HttpClient interceptors handle cross-cutting concerns without introducing hidden behavior?
174. How does stable tracking in @for affect DOM reuse and list performance?
175. How would you test an Angular search component, its observable timing, and its HTTP requests?

## Week 8 - Azure and distributed application design

Research basis: [S7], [S15].

### Post 15 - Tuesday, December 22

**Title:** Azure: choosing services for a .NET application

**Format:** Interview-question roundup.

176. How do IaaS, PaaS, and serverless options change operational responsibilities?
177. When should a .NET workload use App Service, Functions, Container Apps, or AKS?
178. How do Azure Functions triggers, bindings, hosting plans, and cold starts affect design?
179. How should Azure SQL Database and Azure Database for PostgreSQL be evaluated for an application?
180. When should application data use Blob Storage rather than a relational database?
181. How do Azure Service Bus and Event Grid serve different messaging needs?
182. How do Microsoft Entra ID, managed identities, and service principals support application identity?
183. How should Key Vault and managed identity protect access to secrets?
184. How do Azure RBAC and application-level authorization differ?
185. How do private endpoints, virtual networks, and public-access restrictions affect service connectivity?
186. How should App Service deployment slots and configuration be used for safer releases?
187. How should Application Insights, Azure Monitor, and OpenTelemetry support observability?
188. How would you design a CI/CD pipeline using GitHub Actions or Azure DevOps and infrastructure as code?

### Post 16 - Thursday, December 24

**Title:** Designing a reliable order-processing workflow

**Format:** Concept explanation with examples to be developed later.

**Planned example context:** A .NET order API, relational database, Service Bus queue, and background worker deployed to Azure.

189. When is a modular monolith preferable to microservices?
190. When should services communicate synchronously rather than asynchronously?
191. How do at-least-once delivery and idempotent consumers affect message processing?
192. How does the transactional outbox address the database-and-message dual-write problem?
193. How should eventual consistency and compensating actions be handled in a multi-step workflow?
194. How should retries, exponential backoff, jitter, and circuit breakers cooperate?
195. How should poison messages, dead-letter queues, and replay be handled safely?
196. When should a workflow introduce caching, and how should cache invalidation be designed?
197. How should autoscaling account for queue depth, processing time, and downstream limits?
198. How do availability zones, regional recovery, RTO, and RPO affect reliability planning?
199. How should logs, metrics, and distributed traces reveal a failed order across service boundaries?
200. How would you balance reliability, security, cost, operational excellence, and performance when reviewing this architecture?

## Research sources

Interview collections establish topic relevance, not measured question frequency. Official documentation supports technical framing, not claims about interview popularity.

- **[S1] C# interview topics:** [InterviewBit - C# Interview Questions](https://www.interviewbit.com/c-sharp-interview-questions/).
- **[S2] ASP.NET interview topics:** [InterviewBit - ASP.NET Interview Questions](https://www.interviewbit.com/asp-net-interview-questions/). Legacy material filtered out of the plan.
- **[S3] JavaScript interview topics:** [Front End Interview Handbook - JavaScript Questions](https://www.frontendinterviewhandbook.com/javascript-questions).
- **[S4] React interview topics:** [GreatFrontEnd - React Interview Questions](https://www.greatfrontend.com/questions/react-interview-questions).
- **[S5] Angular interview topics:** [InterviewBit - Angular Interview Questions](https://www.interviewbit.com/angular-interview-questions/). Supplemented with current Angular documentation.
- **[S6] SQL interview topics:** [InterviewBit - SQL Interview Questions](https://www.interviewbit.com/sql-interview-questions/). Platform-specific questions adapted for SQL Server and PostgreSQL.
- **[S7] Azure interview topics:** [InterviewBit - Azure Interview Questions](https://www.interviewbit.com/azure-interview-questions/). Legacy service terminology and historical market statistics excluded.
- **[S8] .NET memory management:** [Microsoft Learn - Fundamentals of Garbage Collection](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals).
- **[S9] ASP.NET Core dependency injection:** [Microsoft Learn - Dependency Injection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection).
- **[S10] EF Core query performance:** [Microsoft Learn - Efficient Querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
- **[S11] PostgreSQL concurrency:** [PostgreSQL Documentation - Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html).
- **[S12] TypeScript type safety:** [TypeScript Handbook - Narrowing](https://www.typescriptlang.org/docs/handbook/2/narrowing.html).
- **[S13] React state design:** [React Documentation - Managing State](https://react.dev/learn/managing-state).
- **[S14] Angular reactivity:** [Angular Documentation - Signals](https://angular.dev/guide/signals).
- **[S15] Azure architecture:** [Microsoft Learn - Azure Well-Architected Framework](https://learn.microsoft.com/en-us/azure/well-architected/).
- **[S16] Asynchronous C#:** [Microsoft Learn - Asynchronous Programming](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/).
