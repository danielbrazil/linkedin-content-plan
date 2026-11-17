# ASP.NET Core: from request to response | Part 1/4

Your React app asks for a product:
GET /products/1

What happens before your C# method returns the answer?

51\. What are Kestrel and a reverse proxy?

Kestrel is the web server that receives HTTP requests for your ASP.NET Core app. HTTP is the request-and-response protocol used by web APIs.

A reverse proxy is a server in front of your app. It can forward requests, handle HTTPS connections, and distribute traffic between app instances.

📝 Example:
Browser → reverse proxy → Kestrel → your app.

Kestrel can also receive requests directly. A proxy is a deployment choice, not a requirement for every app.

52\. Why does middleware order matter?

Middleware is a series of steps around your endpoint, the code handling a particular address.

Each step can do work, call the next step, or stop the request early.

📝 Example:
An authentication step establishes who the caller is. An authorization step then checks what that caller may do.

Putting those checks in the wrong order can change the result.

Think of nested boxes: the request goes inward, and the response comes back outward. An error handler must wrap the steps whose errors it should catch.

53\. Middleware, MVC filter, or endpoint filter?

Choose how wide the behavior needs to reach.

• Middleware: work around downstream requests, such as a request ID for logging.
• MVC filter: behavior inside the controller/action pipeline.
• Endpoint filter: behavior around selected Minimal API handlers.

📝 Example:
Add a tracking ID across requests with middleware.
Check a product's name and price before a Minimal API handler with an endpoint filter.

A filter is a hook that lets you run code at a particular stage. MVC has several kinds; not every MVC filter runs at the same time.

📌 Interview takeaway:
"Kestrel receives the request. Middleware handles the surrounding steps, and the selected endpoint produces the response. Order and scope matter."

➡️ Next: where services and settings come from.

Examples and optional deeper explanations, questions 051-053:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/AspNet_Core_Request_Response.md

#AspNetCore #DotNet #WebAPI
