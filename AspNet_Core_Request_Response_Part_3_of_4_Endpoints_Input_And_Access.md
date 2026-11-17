# ASP.NET Core: from request to response | Part 3/4

A request reaches your app. Now it needs a handler, valid input, and permission to proceed.

58\. Controllers or Minimal APIs?

Both define endpoints: code that responds to a route such as /products/1.

Controllers group actions in classes and use MVC conventions and filters.

Minimal APIs map routes directly to handlers with less setup.

📝 Example:
`app.MapGet("/hello", () => "Hello");`

This Minimal API maps a GET request to a short response.

Minimal does not mean "only for tiny apps." Controllers do not automatically make a design better. Choose based on team conventions, required features, and maintainability.

59\. Binding, validation, and serialization?

Think of three separate jobs:

1. Binding turns request data into C# values.
2. Validation checks whether those values are acceptable.
3. Serialization turns a response object into a format such as JSON.

📝 Example:
A product preview receives:
{"name":"Pencil","price":2}

Binding creates the request object.
Validation checks that the name is not blank and the price is positive.
Serialization sends the preview back as JSON.

A price of -2 can be a valid decimal but an invalid business value.

Use separate input and output types, often called DTOs: Data Transfer Objects. Do not let clients set internal fields just because they exist on a database entity.

Malformed input and validation failures commonly return 400 Bad Request. Automatic validation depends on your endpoint style and configuration; our Minimal API sample checks rules explicitly.

60\. Authentication or authorization?

Authentication asks: "Who is calling?"
Authorization asks: "Is this caller allowed to do this?"

📝 Example:
Alice presents a valid access token, so the API recognizes her identity.

She may read products but still lack permission to change prices.

No valid identity normally leads to 401 Unauthorized. A valid identity without the required permission normally leads to 403 Forbidden.

For private records, also check whether the caller may access that specific record. Being signed in is not permission to read everybody's data.

📌 Interview takeaway:
"I choose an endpoint style, bind and validate input separately, and check both identity and permission before returning protected data."

➡️ Next: tokens, browser origins, and background workers.

Examples and optional deeper explanations, questions 058-060:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Request_Response.md

#AspNetCore #DotNet #WebAPI
