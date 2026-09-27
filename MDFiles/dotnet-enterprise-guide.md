# .NET Enterprise Development — A Complete Beginner-to-Advanced Guide

> This guide explains everything you need to know about .NET beyond just writing controllers and services. Every concept is explained from scratch with real examples so you can understand what is happening and why.

---

## Table of Contents

1. [Dependency Injection & Service Lifetimes](#1-dependency-injection--service-lifetimes)
2. [Middleware Pipeline](#2-middleware-pipeline)
3. [Configuration Management](#3-configuration-management)
4. [Secrets Management](#4-secrets-management)
5. [Logging & Structured Logging](#5-logging--structured-logging)
6. [Global Exception Handling](#6-global-exception-handling)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Entity Framework Core — Relationships](#8-entity-framework-core--relationships)
9. [EF Core — Migrations Strategy](#9-ef-core--migrations-strategy)
10. [EF Core — Performance & Query Optimization](#10-ef-core--performance--query-optimization)
11. [Repository & Unit of Work Pattern](#11-repository--unit-of-work-pattern)
12. [CQRS + MediatR](#12-cqrs--mediatr)
13. [Caching Strategies](#13-caching-strategies)
14. [Background Services & Hosted Services](#14-background-services--hosted-services)
15. [API Versioning](#15-api-versioning)
16. [Filters](#16-filters)
17. [Model Validation & FluentValidation](#17-model-validation--fluentvalidation)
18. [AutoMapper](#18-automapper)
19. [Health Checks](#19-health-checks)
20. [Rate Limiting](#20-rate-limiting)
21. [CORS](#21-cors)
22. [NuGet Package Management](#22-nuget-package-management)
23. [DbContext Lifetime & Scoping](#23-dbcontext-lifetime--scoping)
24. [Transactions in EF Core](#24-transactions-in-ef-core)
25. [Async/Await — Enterprise Pitfalls](#25-asyncawait--enterprise-pitfalls)
26. [Clean Architecture](#26-clean-architecture)
27. [Debugging Workflow in Enterprise Apps](#27-debugging-workflow-in-enterprise-apps)
28. [Distributed Tracing & OpenTelemetry](#28-distributed-tracing--opentelemetry)
29. [Multi-Tenancy](#29-multi-tenancy)
30. [Soft Deletes & Audit Trails](#30-soft-deletes--audit-trails)
31. [Feature Flags](#31-feature-flags)
32. [Docker & Environment Parity](#32-docker--environment-parity)
33. [Testing Strategy](#33-testing-strategy)
34. [Outbox Pattern & Eventual Consistency](#34-outbox-pattern--eventual-consistency)
35. [Minimal APIs vs Controller-Based APIs](#35-minimal-apis-vs-controller-based-apis)

---

## 1. Dependency Injection & Service Lifetimes

### What problem does this solve?

Imagine you have an `OrderService` that needs to talk to the database. Without any system, you would write:

```csharp
public class OrderService
{
    private AppDbContext _db = new AppDbContext(); // creating it yourself
}
```

This works, but now imagine you have 20 services all creating their own `AppDbContext`. If you need to change how `AppDbContext` is created (for example, add a connection string from config), you have to go into all 20 files and change them. That is a maintenance nightmare.

Dependency Injection (DI) solves this by saying: **"Don't create objects yourself. Tell the system what you need, and the system will give it to you."**

IoC stands for **Inversion of Control** — instead of your code controlling how objects are created, you hand that control over to the framework. DI is one way to achieve IoC. You don't need to think about IoC as a separate thing — just understand that DI is the practical pattern you use every day.

### How it works in .NET

You register your services once in `Program.cs`:

```csharp
// Program.cs
builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IEmailService, EmailService>();
```

Then anywhere you need a service, you just declare it in the constructor:

```csharp
public class OrderService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    // .NET automatically provides these — you don't create them
    public OrderService(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }
}
```

When a request comes in and your controller needs `OrderService`, .NET looks at the constructor, sees it needs `AppDbContext` and `IEmailService`, creates them, and passes them in. You never call `new OrderService(...)` yourself.

### The three lifetimes — the most asked interview question

This is about **how long an object lives** before it gets thrown away and a new one is created.

---

**Singleton** — Created once, lives forever (until the app shuts down)

Think of it like a company's CEO. There is only one, and they are there from day one until the company closes.

```csharp
builder.Services.AddSingleton<ISettingsReader, SettingsReader>();
```

Use for: things that are expensive to create and safe to share, like a cache manager, configuration reader, or HTTP client factory.

**Do NOT use for**: anything that holds user-specific data or a database connection. If the singleton holds onto a database connection, that one connection is shared by every single user of your app simultaneously — which will break.

---

**Scoped** — Created once per HTTP request, destroyed when the request ends

Think of it like a shopping cart. Each customer gets their own cart when they walk in, and when they leave the cart is cleared.

```csharp
builder.Services.AddScoped<AppDbContext>();
builder.Services.AddScoped<IOrderService, OrderService>();
```

**Use for:**
- `DbContext` — each request gets its own isolated change tracker
- `IOrderService`, `IUserService` — services that read/write data for the current user
- `CurrentUserService` — holds the logged-in user's ID for the duration of the request
- Any service that builds up state during a request (e.g. collects validation errors across multiple steps)

**Avoid for:**
- Anything you want shared across requests — a Scoped service is thrown away when the request ends, so any data it holds is lost
- Background services — there is no HTTP request, so "per request" has no meaning. Use `IServiceScopeFactory` instead (covered in section 14)

This is the most common lifetime you will use for your services.

---

**Transient** — Created fresh every single time it is requested

Think of it like a paper towel. You take one, use it, throw it away. Next time you need one, you take a fresh one.

```csharp
builder.Services.AddTransient<IEmailFormatter, EmailFormatter>();
```

**Use for:**
- `IEmailFormatter` — just formats a string, holds no state
- `IPasswordHasher` — takes input, returns output, nothing stored
- Small utility classes that are cheap to create and stateless

**Avoid for:**
- Anything that opens a connection or allocates significant resources — creating and destroying it on every injection becomes expensive fast
- Services where you need the same instance used consistently across one request (e.g. if `ServiceA` and `ServiceB` both inject `ICartService` as Transient, they get two separate instances — any state one writes is invisible to the other)

---

### The Captive Dependency trap — a common real bug

This is when a long-lived object holds a reference to a short-lived one, trapping it.

**Real example of the bug:**

```csharp
// This is WRONG
builder.Services.AddSingleton<ReportService>(); // lives forever
// ReportService has AppDbContext in its constructor — which is Scoped (lives per request)
```

What happens: The `ReportService` is created once when the app starts. At that moment, it captures one `AppDbContext`. That same `AppDbContext` is now stuck inside `ReportService` forever. Every user, every request, uses that one database connection. In development, ASP.NET Core throws an error to warn you. In production (if you disable validation), it causes corrupted data, connection errors, and data leaks between users.

**The fix:**

```csharp
public class ReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

    // Inject IServiceScopeFactory instead of AppDbContext
    public ReportService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task GenerateReport()
    {
        // Create a fresh scope (and fresh DbContext) each time the method runs
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var orders = await db.Orders.ToListAsync();
        // db is disposed when the using block ends
    }
}
```

### Registering with an interface (best practice)

Always register services against an interface, not a concrete class:

```csharp
// Good — service depends on the interface
builder.Services.AddScoped<IOrderService, OrderService>();

// Your controller
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    public OrdersController(IOrderService orderService) // asks for interface
    {
        _orderService = orderService;
    }
}
```

Why? Because later if you want to swap `OrderService` with `FastOrderService`, you only change one line in `Program.cs`. The controller code never changes.

---

## 2. Middleware Pipeline

### What problem does this solve?

Every HTTP request your app receives needs to go through several steps before reaching your controller: check if the user is authenticated, check CORS, handle errors, log the request, etc. Middleware is the system that lets you plug these steps in.

### What middleware actually is

Middleware is a chain of functions. Each function can:
1. Do something before passing the request along
2. Pass the request to the next function in the chain
3. Do something after the next function finishes

Think of it like a security checkpoint at an airport. You go through luggage X-ray → passport check → body scan → gate check. Each step can let you through or stop you. Each step happens in order.

```
Request  →  [Error Handler] → [HTTPS Redirect] → [Auth] → [Controller]
Response ←  [Error Handler] ← [HTTPS Redirect] ← [Auth] ← [Controller]
```

The request goes in one direction (down the chain), and the response comes back the other direction (up the chain). This is important — middleware runs **both ways**.

### Order matters — this is where bugs happen

```csharp
// Program.cs — this is the CORRECT order
app.UseExceptionHandler("/error");  // FIRST — must wrap everything to catch all errors
app.UseHttpsRedirection();          // redirect HTTP to HTTPS
app.UseStaticFiles();               // serve CSS/JS/images without auth check
app.UseRouting();                   // figure out which controller/endpoint matches
app.UseCors("MyPolicy");            // CORS after routing, before auth
app.UseAuthentication();            // who are you? (reads the JWT token)
app.UseAuthorization();             // what can you do? (checks permissions)
app.MapControllers();               // finally, call the actual controller
```

**Real bug from wrong order:** If you put `UseAuthorization` before `UseAuthentication`, the user's identity is never set up, so every `[Authorize]` endpoint returns 401 Unauthorized even with a valid token.

### Writing your own middleware

Imagine you want to add the time each request took to the response headers:

```csharp
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;

    // RequestDelegate is just a function: "call the next piece of middleware"
    public RequestTimingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context); // hand off to the next middleware — this is where your controller runs

        stopwatch.Stop();
        // By the time we get here, the response is on its way back
        context.Response.Headers["X-Response-Time-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
    }
}

// Register it in Program.cs
app.UseMiddleware<RequestTimingMiddleware>();
```

---

## 3. Configuration Management

### What problem does this solve?

Your app needs settings: database connection strings, email server addresses, feature on/off switches. These change between environments (dev vs staging vs production). You don't want to hardcode them in the code.

### The layered config system

.NET loads configuration from multiple sources. Each source overrides the previous one:

```
1. appsettings.json               (base values)
2. appsettings.Development.json   (overrides for dev)
3. Environment variables          (overrides for servers)
4. Command-line arguments         (highest priority)
```

So if `appsettings.json` says `"Port": 587` but `appsettings.Development.json` says `"Port": 25`, development uses 25. Production still uses 587 because there's no production-specific file.

### Reading config — the wrong way vs the right way

**Wrong way — scattered magic strings:**

```csharp
// Spread across 10 different files
var host = _configuration["EmailSettings:Host"];
var port = _configuration["EmailSettings:Port"];
var from = _configuration["EmailSettings:From"];
```

If the key name changes in JSON, you have to find and fix every string across the codebase. No compile-time safety.

**Right way — bind to a strongly typed class:**

Step 1: Define a class that matches your JSON shape:

```csharp
public class EmailSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string From { get; set; }
    public bool UseSsl { get; set; }
}
```

Step 2: Add the settings to `appsettings.json`:

```json
{
  "EmailSettings": {
    "Host": "smtp.company.com",
    "Port": 587,
    "From": "noreply@company.com",
    "UseSsl": true
  }
}
```

Step 3: Register in `Program.cs`:

```csharp
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
```

Step 4: Inject and use in your service:

```csharp
public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value; // fully populated object
    }

    public async Task SendEmail(string to, string subject, string body)
    {
        using var client = new SmtpClient(_settings.Host, _settings.Port);
        // no magic strings anywhere
    }
}
```

### IOptions vs IOptionsSnapshot vs IOptionsMonitor

These three are about **when** the settings are re-read:

| Type | Re-reads config? | When to use |
|---|---|---|
| `IOptions<T>` | Never — reads once at startup | 99% of cases |
| `IOptionsSnapshot<T>` | Once per HTTP request | When settings change at runtime and you want fresh values per request |
| `IOptionsMonitor<T>` | Immediately when the file changes | Background services that need to react to config changes while running |

For most services, use `IOptions<T>`. You only need the others in specific scenarios.

---

## 4. Secrets Management

### What problem does this solve?

You need a database password. Where do you put it? Not in code. Not in `appsettings.json`. Both of these end up in your git repository, where anyone with access can see them — including every developer on the team and potentially anyone who gets into your repo.

### Development — User Secrets

User Secrets store sensitive values outside your project folder on your local machine. They never get committed to git.

```bash
# Run once to initialize
dotnet user-secrets init

# Add a secret
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=MyDb;User Id=sa;Password=MyLocalPassword;"

# List all secrets
dotnet user-secrets list
```

The secret is stored at `C:\Users\YourName\AppData\Roaming\Microsoft\UserSecrets\{guid}\secrets.json` — outside your project, so it can't be accidentally committed.

In code, you read it exactly like any other config:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");
// .NET automatically merges user secrets in Development environment
```

### Production — Environment Variables

On production servers (or Docker containers), you inject secrets as environment variables. The app reads them the same way as config.

```bash
# On a Linux server
export ConnectionStrings__Default="Server=prod-server;Database=ProdDb;..."
```

Note the double underscore `__`. In .NET config, a colon `:` separates sections. Environment variables can't have colons on some systems, so double underscore `__` means the same thing:

- `ConnectionStrings:Default` in JSON
- `ConnectionStrings__Default` as an environment variable

Both point to the same value.

### Production — Azure Key Vault (the enterprise way)

Instead of putting secrets anywhere you manage, you put them in Azure Key Vault (a locked vault in the cloud). Your app connects using its identity, no password needed.

```csharp
// The app authenticates to Key Vault using its Azure Managed Identity
// No password, no secret — the app's identity IS the key
builder.Configuration.AddAzureKeyVault(
    new Uri("https://my-company-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

Secrets in Key Vault are named like `ConnectionStrings--Default` (double dash instead of colon or double underscore). .NET translates this automatically.

**The golden rule:** If a secret would give someone access to production data, it must never be in your git repository. Ever. Not in any branch, not in any commit. Once it's in git history, you must rotate (change) the secret even after deleting it, because git history is permanent.

---

## 5. Logging & Structured Logging

### What problem does this solve?

When something goes wrong in production, you need to understand what happened. Logs are your only window into what the app was doing.

In a small personal project, `Console.WriteLine("User logged in")` is fine. In an enterprise app with thousands of requests per second, you need logs that can be searched, filtered, and analyzed.

### Plain text logs vs structured logs

**Plain text log:**
```
User 42 placed order 99 for $150.00
```

You cannot search by user ID. You cannot filter all orders over $100. You cannot count how many orders user 42 placed today. It's just text.

**Structured log:**
```json
{
  "timestamp": "2025-04-28T10:30:00Z",
  "level": "Information",
  "message": "Order placed",
  "userId": 42,
  "orderId": 99,
  "total": 150.00
}
```

Now you can query: "Show me all orders placed by user 42 today" or "Show me all orders over $100" in a log tool like Seq or Splunk.

### Using ILogger — the built-in way

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public async Task<int> PlaceOrder(int userId, decimal total)
    {
        // CORRECT — named placeholders become structured properties
        _logger.LogInformation(
            "Order placed by user {UserId} for total {Total}", 
            userId, total);

        // WRONG — string interpolation loses the structure
        _logger.LogInformation($"Order placed by user {userId} for total {total}");
        // ^ This writes a plain string. The userId and total are not searchable properties.
    }
}
```

The `{UserId}` and `{Total}` are called message templates. The values passed after the string become named properties in the structured log output. This is why you must NOT use string interpolation inside log methods.

### Log levels — what each one means

```csharp
_logger.LogTrace("Entering GetOrder method with id {Id}", id);
// Use for: step-by-step internal details. Never enable in production.

_logger.LogDebug("Cache miss for product {ProductId}", productId);
// Use for: debugging information useful during development.

_logger.LogInformation("User {UserId} logged in", userId);
// Use for: normal application events — things that should happen.

_logger.LogWarning("Payment retry attempt {Attempt} for order {OrderId}", attempt, orderId);
// Use for: something unexpected happened but the app can continue.

_logger.LogError(ex, "Failed to process payment for order {OrderId}", orderId);
// Use for: something failed that needs attention. Always pass the exception as first arg.

_logger.LogCritical("Database connection pool exhausted — app cannot serve requests");
// Use for: the app is about to die or is in a broken state.
```

### Serilog — the enterprise standard

The built-in logger is fine but limited. Serilog is what most enterprise apps use because it supports writing to multiple destinations at once.

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

```csharp
// Program.cs
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration) // reads log levels from appsettings
        .Enrich.FromLogContext()        // adds context properties like request ID
        .Enrich.WithMachineName()       // adds the server name to every log
        .WriteTo.Console()              // print to terminal
        .WriteTo.File(
            "logs/app-.log",            // write to file
            rollingInterval: RollingInterval.Day); // new file each day
});
```

Every log in your entire app now flows through Serilog automatically — your controllers, services, EF Core SQL queries, everything.

---

## 6. Global Exception Handling

### What problem does this solve?

If an exception happens in your controller and you don't catch it, the user gets a 500 error page with a full stack trace — including database table names, file paths, and internal logic. This is a serious security risk and a terrible user experience.

You need one central place that catches all unhandled exceptions, logs them, and returns a clean error response.

### Without global exception handling (bad)

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Order>> GetOrder(int id)
{
    var order = await _db.Orders.FindAsync(id);
    // If order is null and we do order.Customer.Name — NullReferenceException
    // .NET returns a 500 with a full stack trace to the client
    return Ok(order);
}
```

### With global exception handling (good — .NET 8 way)

```csharp
// Create a handler class
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        // Log the full exception details (internal — not sent to client)
        _logger.LogError(exception, "Unhandled exception occurred");

        // Decide what status code and message to return based on exception type
        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (404, "The requested resource was not found"),
            UnauthorizedAccessException => (403, "You do not have permission"),
            ArgumentException => (400, exception.Message),
            _ => (500, "An unexpected error occurred. Please try again later.")
            // For unknown exceptions, never expose internal details
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = message,
            traceId = context.TraceIdentifier // useful for debugging — user can report this
        }, cancellationToken);

        return true; // true means "I handled this, stop propagating"
    }
}

// Register in Program.cs
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

Now your controllers can throw custom exceptions and the handler maps them to proper HTTP responses:

```csharp
// A custom exception class
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

// Service throws it
public async Task<Order> GetOrder(int id)
{
    var order = await _db.Orders.FindAsync(id);
    if (order == null)
        throw new NotFoundException($"Order with ID {id} was not found");
    return order;
}

// Controller is clean — no try/catch needed
[HttpGet("{id}")]
public async Task<ActionResult<Order>> GetOrder(int id)
{
    var order = await _orderService.GetOrder(id); // throws NotFoundException if not found
    return Ok(order);
    // GlobalExceptionHandler catches the NotFoundException and returns 404 automatically
}
```

---

## 7. Authentication & Authorization

### First — understand the difference

**Authentication** answers: **Who are you?**
Example: You show your passport at the airport. The officer checks it's real and knows your identity.

**Authorization** answers: **What are you allowed to do?**
Example: Your boarding pass says seat 12A. You're allowed on this specific flight, not any other.

In code:
- Authentication reads your JWT token and figures out who you are
- Authorization checks if who you are has permission to do what you're trying to do

### JWT — what it is and how it works

JWT (JSON Web Token) is a string that looks like: `xxxxx.yyyyy.zzzzz`

It has three parts separated by dots:
1. **Header** — says what algorithm was used
2. **Payload** — the actual data (your user ID, role, email, etc.)
3. **Signature** — proves the token wasn't tampered with

The payload is just base64 encoded — it is **not encrypted**. Anyone can decode it and read the data inside. That's why you should never put passwords or sensitive data in a JWT. The signature prevents someone from changing the payload without the server's secret key knowing.

**Flow:**
1. User logs in with email + password
2. Server verifies the password
3. Server creates a JWT containing the user's ID, roles, and an expiry time
4. Server signs it with a secret key and sends it to the client
5. Client stores it (usually in memory or localStorage)
6. On every future request, client sends the JWT in the `Authorization: Bearer <token>` header
7. Server verifies the signature to confirm the token is genuine and reads the claims

### Setting up JWT authentication

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,          // check who issued the token
            ValidateAudience = true,        // check who the token is intended for
            ValidateLifetime = true,        // check it hasn't expired
            ValidateIssuerSigningKey = true, // check the signature is valid
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
```

### Generating a JWT when the user logs in

```csharp
public class AuthService
{
    private readonly IConfiguration _config;

    public AuthService(IConfiguration config) => _config = config;

    public string GenerateToken(User user)
    {
        // Claims are the pieces of information stored inside the JWT
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role), // "Admin", "Manager", "User"
            new Claim("department", user.Department) // custom claim
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // short-lived — 15 minutes
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Simple role-based authorization

```csharp
// Only users with Admin role can access this
[Authorize(Roles = "Admin")]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id) { ... }

// Any authenticated user can access this
[Authorize]
[HttpGet("profile")]
public IActionResult GetProfile() { ... }

// Anyone (even unauthenticated) can access this
[AllowAnonymous]
[HttpPost("login")]
public IActionResult Login(LoginDto dto) { ... }
```

### Policy-based authorization — the enterprise way

Role-based authorization is simple but limited. What if the rule is "only Finance department managers can approve orders"? That's not just a role — it's a combination of role AND claim.

```csharp
// Define policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FinanceManagerOnly", policy =>
        policy
            .RequireRole("Manager")
            .RequireClaim("department", "Finance"));

    options.AddPolicy("SeniorEmployeeOnly", policy =>
        policy.RequireClaim("yearsOfService", "5", "6", "7", "8", "9", "10"));
});

// Use on a controller action
[Authorize(Policy = "FinanceManagerOnly")]
[HttpPost("{id}/approve")]
public async Task<IActionResult> ApproveOrder(int id) { ... }
```

### Reading the current user inside a service

```csharp
public class OrderService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderService(IHttpContextAccessor accessor)
    {
        _httpContextAccessor = accessor;
    }

    public int GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier);
        
        if (claim == null) throw new UnauthorizedAccessException("Not logged in");
        
        return int.Parse(claim.Value);
    }
}

// Register in Program.cs
builder.Services.AddHttpContextAccessor();
```

### Refresh tokens — why they exist

A JWT expires after 15 minutes. If you made the JWT valid for 7 days, a stolen JWT would give an attacker access for a week. Refresh tokens solve this:

- JWT: Short-lived (15 minutes), stored in memory
- Refresh token: Long-lived (7 days), stored in a database and as an httpOnly cookie

When the JWT expires, the client sends the refresh token to get a new JWT — without the user having to log in again. If you detect suspicious activity, you can delete the refresh token from the database and the attacker's next refresh will fail.

---

## 8. Entity Framework Core — Relationships

### What EF Core actually does

EF Core lets you work with database tables as if they were C# objects. Instead of writing SQL, you write C# and EF Core generates the SQL for you.

Your C# class = a database table
Your C# property = a column
A navigation property (a reference to another class) = a foreign key relationship

### One-to-Many — the most common relationship

Example: One `Department` has many `Employees`. One `Employee` belongs to one `Department`.

```csharp
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; }

    // Navigation property — EF uses this to understand the relationship
    // List on the "one" side
    public List<Employee> Employees { get; set; } = new();
}

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; }

    // Foreign key — the actual column in the database
    public int DepartmentId { get; set; }

    // Navigation property — lets you write employee.Department.Name
    // Reference on the "many" side
    public Department Department { get; set; }
}
```

EF Core can figure out the relationship from these property names. But in enterprise apps, you configure it explicitly using Fluent API — this is clearer and more reliable:

```csharp
// Inside AppDbContext.OnModelCreating
modelBuilder.Entity<Employee>()
    .HasOne(employee => employee.Department)       // Employee has one Department
    .WithMany(department => department.Employees)  // Department has many Employees
    .HasForeignKey(employee => employee.DepartmentId) // this is the FK column
    .OnDelete(DeleteBehavior.Restrict);            // don't delete employees when department is deleted
```

The `OnDelete` behavior is important in enterprise:
- `DeleteBehavior.Cascade` — delete all employees when their department is deleted. **Avoid this in enterprise** — accidentally deleting a department could wipe out hundreds of employee records.
- `DeleteBehavior.Restrict` — throw an error if you try to delete a department that still has employees. Forces you to handle it explicitly.
- `DeleteBehavior.SetNull` — set `DepartmentId` to null when department is deleted (requires the FK to be nullable).

### Many-to-Many with extra data — the real enterprise case

Simple many-to-many: A `Student` can enroll in many `Courses`. A `Course` can have many `Students`.

But in real apps, you almost always need extra data on the relationship — like when the student enrolled, what their grade is, etc. This requires an explicit join entity (a middle table).

```csharp
public class Student
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Enrollment> Enrollments { get; set; } = new();
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; }
    public List<Enrollment> Enrollments { get; set; } = new();
}

// The join entity — represents the middle table
public class Enrollment
{
    public int StudentId { get; set; }
    public Student Student { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; }

    // Extra data on the relationship
    public DateTime EnrolledAt { get; set; }
    public string? Grade { get; set; } // null until graded
}

// Fluent API configuration
modelBuilder.Entity<Enrollment>()
    .HasKey(e => new { e.StudentId, e.CourseId }); // composite primary key

modelBuilder.Entity<Enrollment>()
    .HasOne(e => e.Student)
    .WithMany(s => s.Enrollments)
    .HasForeignKey(e => e.StudentId);

modelBuilder.Entity<Enrollment>()
    .HasOne(e => e.Course)
    .WithMany(c => c.Enrollments)
    .HasForeignKey(e => e.CourseId);
```

**How to enroll a student:**
```csharp
var enrollment = new Enrollment
{
    StudentId = studentId,
    CourseId = courseId,
    EnrolledAt = DateTime.UtcNow
};
_db.Enrollments.Add(enrollment);
await _db.SaveChangesAsync();
```

**How to get all courses for a student:**
```csharp
var courses = await _db.Enrollments
    .Where(e => e.StudentId == studentId)
    .Include(e => e.Course)
    .Select(e => new { e.Course.Title, e.EnrolledAt, e.Grade })
    .ToListAsync();
```

### One-to-One

Example: A `User` has one `UserProfile`. The profile stores extra info that isn't needed on every query.

```csharp
public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public UserProfile? Profile { get; set; }
}

public class UserProfile
{
    public int Id { get; set; }
    public string Bio { get; set; }
    public string? AvatarUrl { get; set; }

    public int UserId { get; set; }   // FK
    public User User { get; set; }
}

modelBuilder.Entity<User>()
    .HasOne(u => u.Profile)
    .WithOne(p => p.User)
    .HasForeignKey<UserProfile>(p => p.UserId);
```

### Owned Entities — embed a complex type in the same table

Sometimes a concept in your domain has multiple properties but doesn't deserve its own table. Example: An `Address` is made of street, city, zip — but you don't want a separate `Addresses` table.

```csharp
public class Order
{
    public int Id { get; set; }
    public Address ShippingAddress { get; set; }
}

[Owned] // tells EF this lives inside another entity's table
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}
```

EF Core creates columns `ShippingAddress_Street`, `ShippingAddress_City`, etc. in the `Orders` table. No separate table. No join needed.

---

## 9. EF Core — Migrations Strategy

### What a migration is

When you change your C# model (add a property, add a new entity, change a column), you need the database to reflect that change. A migration is EF Core generating the SQL script to make that change.

### Basic workflow

```bash
# Step 1: Make a change to your model (e.g., add a property to Order)
# Step 2: Create a migration
dotnet ef migrations add AddOrderNotes

# Step 3: ALWAYS review the generated file before applying it
# It's in your Migrations/ folder — open it, read it, check it makes sense

# Step 4: Apply to your local database
dotnet ef database update

# Step 5: Generate a SQL script for DBA review (enterprise requirement)
dotnet ef migrations script --idempotent --output migration.sql
```

### What a migration file looks like

```csharp
// Migrations/20250428_AddOrderNotes.cs — auto-generated
public partial class AddOrderNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This runs when applying the migration (going forward)
        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "Orders",
            type: "nvarchar(500)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This runs when reverting the migration (rolling back)
        migrationBuilder.DropColumn(
            name: "Notes",
            table: "Orders");
    }
}
```

### Enterprise rules for migrations

**Rule 1: Never edit a migration that has already been applied to any shared environment.**
Once a migration runs on staging or production, treat it as history. If you made a mistake, create a new migration to fix it.

**Rule 2: Always review the generated migration before applying.**
EF Core is smart but not perfect. It sometimes generates `DropTable` instead of `RenameTable` if you rename a class. If you blindly apply that, you lose data.

**Rule 3: Choose the right migration approach for your environment.**

**Approach 1 — On app startup (simple projects only)**
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // only runs pending ones, skips already-applied
}
```
`MigrateAsync()` checks `__EFMigrationsHistory` first — it does NOT re-run migrations every time. But enterprise teams avoid this because if 5 app instances start simultaneously (load balancing), all 5 try to migrate at the same time — race condition.

**Approach 2 — CI/CD pipeline step (enterprise standard)**

Run migration as a separate step *before* deploying the app:
```yaml
# GitHub Actions / Azure DevOps
- name: Apply DB Migrations
  run: dotnet ef database update
  env:
    ConnectionStrings__Default: ${{ secrets.DB_CONNECTION }}

- name: Deploy App
  run: # deploy here — DB is already up to date
```
Migration runs once. App starts only after it completes. No race conditions.

**Approach 3 — Generate SQL script, DBA runs it (banking/healthcare)**

In regulated industries, a DBA must approve every database change before it runs:
```bash
dotnet ef migrations script --idempotent --output migration.sql
```
`--idempotent` makes the script safe to run multiple times — it checks `__EFMigrationsHistory` and skips already-applied migrations. DBA reviews, approves, and runs it before the deployment window.

**Rule 4: For adding a NOT NULL column to an existing table with data**, always provide a default value:
```csharp
migrationBuilder.AddColumn<string>(
    name: "Status",
    table: "Orders",
    defaultValue: "Pending"); // without this, existing rows fail
```

### Data seeding — pre-populating reference data

```csharp
// In AppDbContext.OnModelCreating
modelBuilder.Entity<Role>().HasData(
    new Role { Id = 1, Name = "Admin" },
    new Role { Id = 2, Name = "Manager" },
    new Role { Id = 3, Name = "User" }
);
```

EF Core tracks this data. If you change the seed data, add a new migration — EF generates the INSERT/UPDATE/DELETE SQL automatically.

---

## 10. EF Core — Performance & Query Optimization

### The N+1 Problem — the most common EF performance bug

This is when you load a list of items and then, for each item, make a separate database query to load related data. If you have 100 orders, you make 1 query for orders + 100 queries for customers = 101 queries. This kills performance.

```csharp
// This looks innocent but is BROKEN for performance
var orders = await _db.Orders.ToListAsync();
// 1 query: SELECT * FROM Orders

foreach (var order in orders)
{
    Console.WriteLine(order.Customer.Name);
    // Each access fires: SELECT * FROM Customers WHERE Id = X
    // 100 orders = 100 extra queries!
}
```

**The fix — eager loading with `.Include()`:**

```csharp
var orders = await _db.Orders
    .Include(o => o.Customer)          // JOIN with Customers in the same query
    .Include(o => o.Items)             // also load order items
        .ThenInclude(i => i.Product)   // and load each item's product
    .ToListAsync();
// Result: 1 query with JOINs — all data loaded at once
```

### Only select what you need — projections

Loading a full entity when you only need two fields wastes memory and bandwidth, especially if the entity has large text or binary columns.

```csharp
// BAD — loads all 20 columns of every order, including large fields
var orders = await _db.Orders.ToListAsync();

// GOOD — only fetches what the response needs
var orderSummaries = await _db.Orders
    .Where(o => o.Status == "Pending")
    .Select(o => new OrderSummaryDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name,  // EF generates a JOIN automatically
        Total = o.Total,
        CreatedAt = o.CreatedAt
    })
    .ToListAsync();
```

### AsNoTracking — for read-only queries

By default, EF Core watches every entity you load for changes (change tracking). This has a memory and CPU cost. For read-only queries where you're just displaying data, turn it off:

```csharp
var products = await _db.Products
    .AsNoTracking()        // skip change tracking
    .Where(p => p.IsActive)
    .ToListAsync();
// Typically 20-30% faster for read-only scenarios
```

### Always paginate — never load all rows

```csharp
// NEVER do this in an enterprise app
var allOrders = await _db.Orders.ToListAsync(); // could be millions of rows

// ALWAYS paginate
public async Task<PagedResult<Order>> GetOrders(int page, int pageSize)
{
    var total = await _db.Orders.CountAsync();
    
    var orders = await _db.Orders
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page - 1) * pageSize)  // skip to the right page
        .Take(pageSize)               // only take one page worth
        .ToListAsync();

    return new PagedResult<Order>
    {
        Items = orders,
        TotalCount = total,
        Page = page,
        PageSize = pageSize
    };
}
```

### Seeing the SQL EF Core generates

During development, enable SQL logging to see exactly what queries are being run:

```csharp
// Program.cs or in DbContext options
options.UseSqlServer(connectionString)
       .LogTo(Console.WriteLine, LogLevel.Information)
       .EnableSensitiveDataLogging(); // shows parameter values (dev only!)
```

Every EF query you write will print its SQL to the console. This is how you catch N+1 bugs.

---

## 11. Repository & Unit of Work Pattern

### What problem does it solve?

Without the repository pattern, your service layer talks directly to `DbContext`:

```csharp
public class OrderService
{
    private readonly AppDbContext _db; // directly dependent on EF Core

    public async Task<List<Order>> GetPendingOrders()
    {
        return await _db.Orders
            .Where(o => o.Status == "Pending")
            .ToListAsync();
    }
}
```

This works but means:
- Testing requires a real database (or in-memory EF database)
- If you ever switch from EF Core to Dapper, you rewrite every service
- Complex queries are scattered across all services

The repository pattern puts all data access code behind an interface.

### Generic Repository

```csharp
// The interface — defines what operations are available
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
}

// The implementation — talks to EF Core
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;

    public Repository(AppDbContext db) => _db = db;

    public async Task<T?> GetByIdAsync(int id) 
        => await _db.Set<T>().FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync() 
        => await _db.Set<T>().AsNoTracking().ToListAsync();

    public async Task AddAsync(T entity) 
        => await _db.Set<T>().AddAsync(entity);

    public void Update(T entity) 
        => _db.Set<T>().Update(entity);

    public void Delete(T entity) 
        => _db.Set<T>().Remove(entity);
}
```

### Specific Repository — for queries beyond CRUD

```csharp
public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> GetPendingOrdersAsync();
    Task<List<Order>> GetOrdersByCustomerAsync(int customerId);
}

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext db) : base(db) { }

    public async Task<List<Order>> GetPendingOrdersAsync()
        => await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == "Pending")
            .ToListAsync();

    public async Task<List<Order>> GetOrdersByCustomerAsync(int customerId)
        => await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
}
```

### Unit of Work

The problem with multiple repositories is: if `OrderRepository.SaveChanges()` and `InvoiceRepository.SaveChanges()` each call `_db.SaveChangesAsync()` separately, they're separate database round trips. If one succeeds and the other fails, your data is inconsistent.

Unit of Work wraps multiple repository operations into a single `SaveChangesAsync`:

```csharp
public interface IUnitOfWork : IDisposable
{
    IOrderRepository Orders { get; }
    ICustomerRepository Customers { get; }
    Task<int> SaveChangesAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public IOrderRepository Orders { get; }
    public ICustomerRepository Customers { get; }

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
        Orders = new OrderRepository(db);
        Customers = new CustomerRepository(db);
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
    public void Dispose() => _db.Dispose();
}

// Usage in a service
public async Task PlaceOrder(CreateOrderDto dto)
{
    var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId);
    var order = new Order { CustomerId = customer.Id, ... };

    await _uow.Orders.AddAsync(order);
    // Both changes saved in ONE database call
    await _uow.SaveChangesAsync();
}
```

### When NOT to use it

EF Core's `DbContext` is already a Unit of Work. `DbSet<T>` is already a Repository. For a team that only uses EF Core and has no plans to switch, adding this extra layer is more complexity without much benefit. Use it when:
- You need to write unit tests without hitting any database
- You have multiple data sources (SQL + MongoDB)
- Your team is large and you want to enforce consistent data access patterns

---

## 12. CQRS + MediatR

### What problem does it solve?

In a typical service, one class handles everything:

```csharp
public class OrderService
{
    public Task<List<Order>> GetAllOrders() { ... }
    public Task<Order> GetOrderById(int id) { ... }
    public Task<int> CreateOrder(CreateOrderDto dto) { ... }
    public Task UpdateOrder(int id, UpdateOrderDto dto) { ... }
    public Task DeleteOrder(int id) { ... }
    public Task ApproveOrder(int id) { ... }
    public Task CancelOrder(int id) { ... }
}
```

As the app grows, this class becomes hundreds of lines, handles different concerns, and every change risks breaking something else.

**CQRS** (Command Query Responsibility Segregation) separates reads from writes:
- **Query** = reading data (no side effects)
- **Command** = changing data (create, update, delete)

**MediatR** is a library that routes requests to handlers, so each operation gets its own small, focused class.

### Setup

```bash
dotnet add package MediatR
```

```csharp
// Program.cs
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

### A Query — reading data

```csharp
// Step 1: Define the query (the "request")
// IRequest<T> means "this returns T"
public record GetOrderByIdQuery(int OrderId) : IRequest<OrderDto?>;

// Step 2: Define the handler (the "handler")
public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly AppDbContext _db;
    public GetOrderByIdHandler(AppDbContext db) => _db = db;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == query.OrderId)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                CustomerName = o.Customer.Name,
                Total = o.Total
            })
            .FirstOrDefaultAsync(ct);
    }
}
```

### A Command — changing data

```csharp
// The command
public record CreateOrderCommand(int CustomerId, List<OrderItemDto> Items) : IRequest<int>;

// The handler
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    private readonly AppDbContext _db;
    public CreateOrderHandler(AppDbContext db) => _db = db;

    public async Task<int> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var order = new Order
        {
            CustomerId = cmd.CustomerId,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        foreach (var item in cmd.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order.Id;
    }
}
```

### The controller becomes very thin

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    public OrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> Get(int id)
    {
        var order = await _mediator.Send(new GetOrderByIdQuery(id));
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, null);
    }
}
```

The controller knows nothing about the database. It just sends a message (command/query) and gets a result back.

### Pipeline Behaviors — cross-cutting concerns

A pipeline behavior wraps around every handler. This is where you add logging, validation, or timing for ALL commands and queries in one place:

```csharp
// Logs every command and how long it took
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) 
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,  // "next" = the actual handler
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        var response = await next(); // run the actual handler
        sw.Stop();

        _logger.LogInformation("Handled {RequestName} in {Ms}ms", requestName, sw.ElapsedMilliseconds);
        return response;
    }
}

// Register
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

---

## 13. Caching Strategies

### What problem does it solve?

Some data doesn't change often — a product catalogue, country list, configuration values. Querying the database every time a user loads a page is wasteful. Cache stores the result in memory so the next request gets it instantly without hitting the database.

### In-Memory Cache — single server

Stores data in the web server's RAM. Fastest possible access. Disappears when the app restarts. Not shared between multiple servers.

```csharp
// Register
builder.Services.AddMemoryCache();

public class ProductService
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;

    public ProductService(IMemoryCache cache, AppDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<List<Product>> GetActiveProducts()
    {
        // GetOrCreateAsync: 
        // - If "active-products" exists in cache, return it immediately
        // - If not, run the factory function, store the result, return it
        return await _cache.GetOrCreateAsync("active-products", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // cache for 10 min
            entry.SlidingExpiration = TimeSpan.FromMinutes(2); // reset timer if accessed within 2 min
            
            return await _db.Products
                .Where(p => p.IsActive)
                .AsNoTracking()
                .ToListAsync();
        });
    }
}
```

### Distributed Cache — Redis (multi-server, enterprise)

When you have multiple servers running your app (for redundancy and load balancing), in-memory cache doesn't work — each server has its own cache and they go out of sync. Redis is a shared cache that all servers connect to.

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

```csharp
// Register
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);

public class ProductService
{
    private readonly IDistributedCache _cache;
    private readonly AppDbContext _db;

    public async Task<List<ProductDto>?> GetActiveProducts()
    {
        // Step 1: try to get from cache
        var cached = await _cache.GetStringAsync("active-products");
        if (cached != null)
        {
            return JsonSerializer.Deserialize<List<ProductDto>>(cached);
        }

        // Step 2: cache miss — fetch from database
        var products = await _db.Products
            .Where(p => p.IsActive)
            .Select(p => new ProductDto { Id = p.Id, Name = p.Name, Price = p.Price })
            .AsNoTracking()
            .ToListAsync();

        // Step 3: store in cache for next time
        await _cache.SetStringAsync(
            "active-products",
            JsonSerializer.Serialize(products),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        return products;
    }

    // Call this whenever a product is updated/added/deleted
    public async Task InvalidateProductCache()
    {
        await _cache.RemoveAsync("active-products");
    }
}
```

### The hardest problem: cache invalidation

When should you remove the cached data and re-fetch from the database? Strategies:

1. **TTL (Time to Live)** — cache expires after X minutes automatically. Simple, but data might be slightly stale.
2. **Write-through** — every time you update the database, also update the cache. Always fresh, but complex.
3. **Cache-aside (most common)** — you manage it manually: when you update data, delete the cache entry. Next request re-populates it.
4. **Event-driven** — a message on a queue triggers cache deletion (for microservices).

---

## 14. Background Services & Hosted Services

### What problem does it solve?

Some tasks should not happen during a user's HTTP request because they take too long, or they need to run on a schedule. Examples:
- Send a digest email every morning at 8am
- Clean up old temporary files every hour
- Process a queue of pending notifications every 30 seconds
- Check for expiring subscriptions daily

These run in the background while the app is serving HTTP requests normally.

### BackgroundService — the base class to use

```csharp
public class DailyEmailDigestService : BackgroundService
{
    private readonly ILogger<DailyEmailDigestService> _logger;
    private readonly IServiceScopeFactory _scopeFactory; // IMPORTANT — explained below

    public DailyEmailDigestService(
        ILogger<DailyEmailDigestService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // stoppingToken fires when the app is shutting down
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Running daily email digest...");

            await SendDailyDigests(stoppingToken);

            // Wait until tomorrow 8am
            var now = DateTime.Now;
            var nextRun = DateTime.Today.AddDays(1).AddHours(8);
            var delay = nextRun - now;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SendDailyDigests(CancellationToken ct)
    {
        // Must create a scope to get Scoped services like DbContext
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var users = await db.Users
            .Where(u => u.DigestEnabled)
            .ToListAsync(ct);

        foreach (var user in users)
        {
            await emailService.SendDigest(user.Email, ct);
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<DailyEmailDigestService>();
```

### Why you can't inject DbContext directly in BackgroundService

`BackgroundService` is registered as a **Singleton** (lives as long as the app). `DbContext` is **Scoped** (lives per request). You can't inject a Scoped service into a Singleton — the captive dependency trap from section 1.

The solution is `IServiceScopeFactory` — it lets a Singleton create a short-lived scope whenever it needs Scoped services. You create the scope inside the method, use `DbContext` within it, and dispose of it when done. This way each execution gets a fresh `DbContext`.

---

## 15. API Versioning

### What problem does it solve?

Once your API is live and clients (mobile apps, third-party systems) are using it, you can't just change the API. If you rename a field or change a response structure, every client that relies on the old format breaks. You need to support the old version while building the new one.

```bash
dotnet add package Asp.Versioning.Mvc
```

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true; // old clients without version get v1
    options.ReportApiVersions = true; // tells clients which versions exist via response headers
});
```

### Three ways clients can specify the version

```csharp
options.ApiVersionReader = ApiVersionReader.Combine(
    new UrlSegmentApiVersionReader(),         // /api/v1/orders
    new HeaderApiVersionReader("X-Api-Version"), // header: X-Api-Version: 1.0
    new QueryStringApiVersionReader("api-version") // /api/orders?api-version=1.0
);
```

### Versioned controller

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/orders")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class OrdersController : ControllerBase
{
    // v1 returns basic order info
    [HttpGet("{id}")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetV1(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        return Ok(new { order.Id, order.Total }); // simple response
    }

    // v2 returns richer info with customer details
    [HttpGet("{id}")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> GetV2(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
        
        return Ok(new 
        { 
            order.Id, 
            order.Total,
            Customer = new { order.Customer.Name, order.Customer.Email },
            ItemCount = order.Items.Count
        });
    }
}
```

---

## 16. Filters

### What are filters?

Filters are similar to middleware but they run inside the MVC pipeline — after routing has matched a controller action but before or after the action executes. They're ideal for things specific to your controllers.

The order of execution:
```
[Authorization Filter] → [Resource Filter] → [Action Filter - Before] → [Controller Action] → [Action Filter - After] → [Result Filter]
```

### Action Filter — runs before and after an action

Practical example — log every API call with timing:

```csharp
public class ApiCallLoggingFilter : IActionFilter
{
    private readonly ILogger<ApiCallLoggingFilter> _logger;
    private Stopwatch _sw;

    public ApiCallLoggingFilter(ILogger<ApiCallLoggingFilter> logger) => _logger = logger;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Runs BEFORE the controller action
        _sw = Stopwatch.StartNew();
        _logger.LogInformation(
            "API call started: {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Runs AFTER the controller action
        _sw.Stop();
        _logger.LogInformation(
            "API call finished: {Method} {Path} — {StatusCode} in {Ms}ms",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            context.HttpContext.Response.StatusCode,
            _sw.ElapsedMilliseconds);
    }
}

// Apply globally to all controllers in Program.cs
builder.Services.AddControllers(options =>
    options.Filters.Add<ApiCallLoggingFilter>());

// Or apply to a single controller or action
[ServiceFilter(typeof(ApiCallLoggingFilter))]
[HttpGet]
public IActionResult GetOrders() { ... }
```

### When to use Middleware vs Filters

| Concern | Use |
|---|---|
| Runs for ALL requests (including static files, Swagger) | Middleware |
| Only runs for controller actions | Filter |
| Error handling | Both work — Middleware for global, Filter for MVC-specific |
| Request/response timing for all traffic | Middleware |
| Logging specific to controller actions | Filter |

---

## 17. Model Validation & FluentValidation

### What problem does it solve?

Users send data to your API. That data might be invalid — missing required fields, numbers out of range, email format wrong. You need to catch this before it reaches your business logic.

### Built-in Data Annotations

For simple rules, .NET provides attributes:

```csharp
public class CreateUserDto
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; }

    [Required]
    [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
    public int Age { get; set; }

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; }
}
```

With `[ApiController]` on your controller, if validation fails, .NET automatically returns a 400 Bad Request with all the errors. You don't write any validation code in the controller.

### FluentValidation — for complex enterprise rules

Data annotations can't express complex rules like "this field is required only if another field has a certain value" or "this value must exist in the database". FluentValidation handles these.

```bash
dotnet add package FluentValidation.AspNetCore
```

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
{
    private readonly AppDbContext _db;

    public CreateOrderValidator(AppDbContext db)
    {
        _db = db;

        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("Customer ID must be positive")
            .MustAsync(CustomerExists).WithMessage("Customer not found in database");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item");

        // Apply rules to each item in the list
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .LessThanOrEqualTo(100).WithMessage("Can't order more than 100 of a single item");
        });

        RuleFor(x => x.DeliveryDate)
            .GreaterThan(DateTime.Today).WithMessage("Delivery date must be in the future")
            .When(x => x.DeliveryDate.HasValue); // only validate if provided
    }

    private async Task<bool> CustomerExists(int customerId, CancellationToken ct)
        => await _db.Customers.AnyAsync(c => c.Id == customerId, ct);
}

// Register in Program.cs
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
// ^ Registers all validators in your project automatically
```

---

## 18. AutoMapper

### What problem does it solve?

Your domain entity (database model) and your DTO (data transfer object — what you expose through the API) are different. You need to copy data between them. Without AutoMapper:

```csharp
// This tedious manual mapping everywhere:
var dto = new OrderDto
{
    Id = order.Id,
    CustomerName = order.Customer.FirstName + " " + order.Customer.LastName,
    Total = order.Total,
    ItemCount = order.Items.Count,
    CreatedAt = order.CreatedAt
};
```

For every endpoint, for every entity, this pile of assignments. AutoMapper automates it.

```bash
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
```

### Defining mappings in a Profile

```csharp
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        // Map from Order entity to OrderDto
        CreateMap<Order, OrderDto>()
            // Handle computed/custom mappings explicitly
            .ForMember(
                dest => dest.CustomerName, 
                opt => opt.MapFrom(src => src.Customer.FullName))
            .ForMember(
                dest => dest.ItemCount, 
                opt => opt.MapFrom(src => src.Items.Count));

        // Map from CreateOrderDto to Order entity
        CreateMap<CreateOrderDto, Order>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => "Pending"));
            // Fields that don't exist in source (Id, etc.) are ignored automatically
    }
}

// Register
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

### Using AutoMapper

```csharp
public class OrderService
{
    private readonly IMapper _mapper;
    private readonly AppDbContext _db;

    public OrderService(IMapper mapper, AppDbContext db)
    {
        _mapper = mapper;
        _db = db;
    }

    public async Task<OrderDto> CreateOrder(CreateOrderDto dto)
    {
        var order = _mapper.Map<Order>(dto); // CreateOrderDto → Order
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return _mapper.Map<OrderDto>(order); // Order → OrderDto
    }

    public async Task<List<OrderDto>> GetAllOrders()
    {
        var orders = await _db.Orders.Include(o => o.Customer).ToListAsync();
        return _mapper.Map<List<OrderDto>>(orders); // maps the whole list at once
    }
}
```

### Validate your mappings at startup

AutoMapper misconfigurations (like forgetting to map a required field) are silent — they just produce null values. Catch them at startup:

```csharp
// In Program.cs after building the app
var mapper = app.Services.GetRequiredService<IMapper>();
mapper.ConfigurationProvider.AssertConfigurationIsValid();
// Throws immediately at startup if any mapping is broken
```

---

## 19. Health Checks

### What problem does it solve?

In enterprise, your app runs behind a load balancer or in Kubernetes. These systems need to know: is this instance healthy and ready to serve traffic? They call a `/health` endpoint every few seconds.

If your database is down, the health check fails, and the load balancer stops sending traffic to that instance.

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")   // checks DB connection
    .AddRedis(builder.Configuration["Redis:ConnectionString"], "cache")
    .AddCheck("disk-space", () =>
    {
        var freeSpaceGb = GetFreeDiskSpaceGb();
        return freeSpaceGb > 1
            ? HealthCheckResult.Healthy($"{freeSpaceGb}GB free")
            : HealthCheckResult.Degraded($"Only {freeSpaceGb}GB free");
    });

app.MapHealthChecks("/health/live");   // liveness: is the process alive?
app.MapHealthChecks("/health/ready");  // readiness: can it handle requests?
```

Call `/health/ready` in your browser — you'll get JSON showing whether each check passed or failed.

---

## 20. Rate Limiting

### What problem does it solve?

Without rate limiting, a single user or a bot can spam your API with thousands of requests per second, overloading your server and denying service to real users (DDoS). Rate limiting puts a cap on how many requests a client can make in a given time window.

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    // Fixed window: 100 requests per minute. Counter resets every minute.
    options.AddFixedWindowLimiter("standard", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 5; // queue up to 5 requests before rejecting
    });

    // Stricter limit for expensive operations
    options.AddFixedWindowLimiter("strict", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
    });

    options.RejectionStatusCode = 429; // Too Many Requests
});

app.UseRateLimiter();

// Apply on endpoints
[EnableRateLimiting("standard")]
[HttpGet]
public async Task<IActionResult> GetProducts() { ... }

[EnableRateLimiting("strict")]
[HttpPost("login")]
public async Task<IActionResult> Login(LoginDto dto) { ... }
```

---

## 21. CORS

### What problem does it solve?

Browsers block JavaScript from calling an API on a different domain. If your frontend is at `https://app.mycompany.com` and your API is at `https://api.mycompany.com`, the browser blocks the call by default. CORS tells the browser which origins (domains) are allowed to call your API.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendApp", policy =>
    {
        policy
            .WithOrigins(
                "https://app.mycompany.com",
                "https://staging.mycompany.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // needed if using cookies
    });

    // For development only — allow any origin
    options.AddPolicy("DevAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// In the middleware pipeline — after UseRouting, before UseAuthorization
app.UseCors(app.Environment.IsDevelopment() ? "DevAll" : "FrontendApp");
```

**Common mistake:** CORS is a **browser** security feature. It doesn't prevent server-to-server calls or Postman/curl requests. It only prevents browser JavaScript from calling your API from an unlisted origin.

---

## 22. NuGet Package Management

### What NuGet is

NuGet is .NET's package manager — like npm for Node.js or pip for Python. Packages are libraries of code other developers have written that you can download and use.

### Daily commands you'll use

```bash
# Add a package
dotnet add package Serilog.AspNetCore

# Add a specific version
dotnet add package Serilog.AspNetCore --version 8.0.1

# Remove a package
dotnet remove package Serilog.AspNetCore

# List all packages in the project
dotnet list package

# Find outdated packages
dotnet list package --outdated

# Check for security vulnerabilities
dotnet list package --vulnerable
dotnet list package --vulnerable --include-transitive  # includes indirect dependencies
```

### Central Package Management — enterprise multi-project solution

When your solution has 5 projects all using the same packages, you end up with versions scattered across 5 `.csproj` files. When a security vulnerability requires upgrading a package, you have to update all 5 files.

**Solution: Central Package Management**

Create `Directory.Packages.props` at the solution root (next to the `.sln` file):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.1" />
    <PackageVersion Include="AutoMapper" Version="12.0.1" />
    <PackageVersion Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.4" />
  </ItemGroup>
</Project>
```

In each project's `.csproj`, reference packages **without specifying a version**:

```xml
<ItemGroup>
  <PackageReference Include="Serilog.AspNetCore" />
  <PackageReference Include="AutoMapper" />
</ItemGroup>
```

Now to upgrade a package across the entire solution, you change one line in one file.

### Dependency conflicts

Two packages requiring different versions of the same library is common. You'll see errors like: `Package X 2.0 requires Library Y >= 5.0, but you have 4.0 installed`.

Fix: Add an explicit reference to the version both require:

```xml
<PackageReference Include="System.Text.Json" Version="8.0.4" />
```

This forces all packages to use your specified version. Usually works if you pick a version that satisfies all requirements.

---

## 23. DbContext Lifetime & Scoping

### Why DbContext is Scoped (not Singleton)

`DbContext` tracks changes in memory (change tracker). If it were a Singleton shared across all users simultaneously:
- User A loads Order #1 and modifies it
- User B loads Order #1 at the same time
- User A saves — User B's change tracker is now confused
- Data corruption

Scoped means one `DbContext` per HTTP request. One user, one request, one isolated context. Safe.

```csharp
// This is the correct registration — Scoped is the default
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

### DbContext pooling — for high-traffic apps

Creating and destroying a `DbContext` on every request has overhead. DbContext pooling reuses instances after resetting their state:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")),
    poolSize: 128); // keep up to 128 instances in the pool
```

**Limitation:** With pooling, you can't put custom initialization code in the `DbContext` constructor, because the instance is reused and the constructor only runs once.

### Read replica — separate databases for reads and writes

At scale, your primary database handles writes. A read replica (a synchronized copy) handles queries. This reduces load on the primary:

```csharp
// Write operations
builder.Services.AddDbContext<WriteDbContext>(opts =>
    opts.UseSqlServer(config["DB:Primary"]));

// Read operations — connects to the replica
builder.Services.AddDbContext<ReadDbContext>(opts =>
    opts.UseSqlServer(config["DB:ReadReplica"])
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)); // reads don't need tracking
```

---

## 24. Transactions in EF Core

### What a transaction is

A transaction is a set of database operations that either all succeed or all fail together. No partial states.

Real example: Transfer $100 from Account A to Account B.
1. Deduct $100 from A
2. Add $100 to B

If step 1 succeeds but step 2 fails, money disappears. A transaction wraps both steps — if step 2 fails, step 1 is also rolled back.

### Implicit transaction — automatic

Every call to `SaveChangesAsync()` is automatically wrapped in a transaction. All changes tracked since the last `SaveChanges` either all save or all fail:

```csharp
order.Status = "Confirmed";
order.ConfirmedAt = DateTime.UtcNow;
invoice.IssueDate = DateTime.UtcNow;
// Both changes save together — or neither saves
await _db.SaveChangesAsync();
```

### Explicit transaction — when you need multiple SaveChanges calls

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // First operation
    _db.Orders.Add(order);
    await _db.SaveChangesAsync();

    // Second operation — uses the order.Id from above
    var invoice = new Invoice { OrderId = order.Id, Amount = order.Total };
    _db.Invoices.Add(invoice);
    await _db.SaveChangesAsync();

    // Both succeeded — commit
    await transaction.CommitAsync();
}
catch
{
    // Something failed — roll back BOTH operations
    await transaction.RollbackAsync();
    throw;
}
```

### Optimistic Concurrency — handling simultaneous edits

When two users edit the same record at the same time, the second save would silently overwrite the first user's changes. Optimistic concurrency detects this and throws an error.

```csharp
public class Product
{
    public int Id { get; set; }
    public decimal Price { get; set; }

    [Timestamp] // EF adds a rowversion column to the database
    public byte[] RowVersion { get; set; }
}

// When two users try to update the same product simultaneously:
// User A loads product (RowVersion = 1)
// User B loads product (RowVersion = 1)
// User A saves successfully (RowVersion becomes 2)
// User B tries to save (their RowVersion = 1, but DB has 2)
// EF throws DbUpdateConcurrencyException

try
{
    product.Price = newPrice;
    await _db.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    // Option 1: tell the user their version is stale and ask them to refresh
    throw new ApplicationException("This record was modified by another user. Please reload and try again.");
    
    // Option 2: reload from database and retry
}
```

---

## 25. Async/Await — Enterprise Pitfalls

You already know how to write async code. These are the subtle mistakes that cause bugs in production.

### Never block on async — causes deadlocks

```csharp
// BAD — .Result blocks the current thread while waiting
var orders = _orderService.GetOrdersAsync().Result; // can deadlock

// BAD — .Wait() is the same problem
_orderService.GetOrdersAsync().Wait();

// GOOD — always await
var orders = await _orderService.GetOrdersAsync();
```

Why it deadlocks: `.Result` blocks the current thread. The async operation needs that thread to continue. Neither can proceed — deadlock.

### async void — never use it

```csharp
// BAD — if this throws, the exception is unobservable and can crash the process
public async void ProcessOrder(int id) { ... }

// GOOD
public async Task ProcessOrder(int id) { ... }
```

`async void` is only acceptable for event handlers in UI frameworks (WinForms, WPF). In ASP.NET Core, never.

### Run independent tasks in parallel

```csharp
// BAD — sequential: takes 3 seconds total
var user = await GetUserAsync();     // 1 second
var orders = await GetOrdersAsync(); // 1 second
var settings = await GetSettingsAsync(); // 1 second

// GOOD — parallel: takes ~1 second total
var userTask = GetUserAsync();
var ordersTask = GetOrdersAsync();
var settingsTask = GetSettingsAsync();

// Wait for all three to finish
await Task.WhenAll(userTask, ordersTask, settingsTask);

var user = userTask.Result;       // .Result is safe here — task is already completed
var orders = ordersTask.Result;
var settings = settingsTask.Result;
```

### Always pass CancellationToken through

When a user closes the browser or the request times out, the `CancellationToken` fires. If you pass it to your database queries, EF Core cancels the SQL query too — saving database resources:

```csharp
// Controller passes its CancellationToken to the service
[HttpGet]
public async Task<IActionResult> GetOrders(CancellationToken ct)
{
    var orders = await _orderService.GetOrdersAsync(ct);
    return Ok(orders);
}

// Service passes it to the database
public async Task<List<Order>> GetOrdersAsync(CancellationToken ct)
{
    return await _db.Orders
        .AsNoTracking()
        .ToListAsync(ct); // cancels the SQL query if request is cancelled
}
```

---

## 26. Clean Architecture

### What it is and why it matters

In a small app, putting everything in one project is fine. In an enterprise app with 50+ developers, you need clear boundaries so teams can work independently without breaking each other.

Clean Architecture defines layers with strict rules about which layer can depend on which.

### The layers

```
YourApp/
├── YourApp.Api/              → Controllers, Middleware, Program.cs
├── YourApp.Application/      → Use cases, commands, queries, DTOs, interfaces
├── YourApp.Domain/           → Entities, business rules, domain exceptions
└── YourApp.Infrastructure/   → EF Core, email sender, file storage, external APIs
```

### The dependency rule (the most important rule)

**Dependencies only point inward.** Inner layers know nothing about outer layers.

```
Api depends on → Application depends on → Domain
Infrastructure depends on → Application and Domain (to implement interfaces)
```

Domain knows nothing about databases, HTTP, or EF Core. It's pure C# business logic.
Application defines interfaces (`IEmailService`) but doesn't implement them.
Infrastructure implements those interfaces (`SmtpEmailService : IEmailService`).

This means you can swap your database from SQL Server to PostgreSQL by changing only the Infrastructure layer. The Domain and Application layers don't change.

### What goes in each layer

**Domain layer** — The heart of your app. No framework dependencies.
```csharp
// Domain entity with business rules built in
public class Order
{
    private readonly List<OrderItem> _items = new();

    public int Id { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public decimal Total => _items.Sum(i => i.Price * i.Quantity);

    public void AddItem(int productId, int quantity, decimal price)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot add items to a non-draft order");

        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");

        _items.Add(new OrderItem(productId, quantity, price));
    }

    public void Confirm()
    {
        if (!_items.Any())
            throw new DomainException("Cannot confirm an empty order");

        Status = OrderStatus.Confirmed;
    }
}
```

**Application layer** — orchestrates the use case, defines interfaces
```csharp
// Interface defined here — not implemented here
public interface IEmailService
{
    Task SendOrderConfirmation(string email, int orderId);
}

// Command handler — calls domain logic, calls interfaces
public class ConfirmOrderHandler : IRequestHandler<ConfirmOrderCommand>
{
    private readonly IOrderRepository _repo;
    private readonly IEmailService _email;

    public async Task Handle(ConfirmOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId)
            ?? throw new NotFoundException("Order not found");

        order.Confirm(); // domain business logic

        await _repo.SaveAsync(ct);
        await _email.SendOrderConfirmation(order.Customer.Email, order.Id); // interface call
    }
}
```

**Infrastructure layer** — implements the interfaces
```csharp
public class SmtpEmailService : IEmailService
{
    public async Task SendOrderConfirmation(string email, int orderId)
    {
        // actual SMTP code here
    }
}

// In Program.cs (Api layer)
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
```

---

## 27. Debugging Workflow in Enterprise Apps

### The approach — systematic, not random

In a personal project you can add `Console.WriteLine` everywhere and rerun. In enterprise, you can't redeploy to production every time you need a print statement, and you often don't have access to the production database directly.

### Step 1 — Get the exact request

Get the full HTTP request that caused the bug: method, URL, headers, and request body. Ask the person who reported it or get it from logs.

In enterprise apps, every request gets a unique `traceId` (also called `requestId` or `correlationId`). This ID appears in every log line for that request. Ask the user for the time the error occurred, then search logs by time range.

### Step 2 — Read the logs before touching code

```
[ERR] Unhandled exception occurred | TraceId: 0HMTK8LN97N6G:00000001
System.NullReferenceException: Object reference not set to an instance of an object
   at SkillBridge.Services.OrderService.GetOrderTotal(Int32 orderId) in OrderService.cs:line 47
```

The stack trace tells you: the error is in `OrderService.cs` line 47. Open that file, go to that line. What could be null there?

### Step 3 — Reproduce locally

Run the app in Debug mode (F5 in Visual Studio). Make the same request with the same data (use Postman, Swagger, or your frontend). Does it reproduce?

If not, it might be environment-specific (production data differs from your local data).

### Step 4 — Set a breakpoint and walk through

In Visual Studio:
- Click in the gray margin next to a line of code — a red dot appears
- When the code reaches that line, execution pauses
- Hover over variables to see their values
- Press F10 to step to the next line
- Press F11 to step into a method call
- Press F5 to continue to the next breakpoint

**Conditional breakpoint** — only pause when a specific condition is true (useful for bugs that only happen with certain data):
- Right-click the red dot
- Click "Conditions"
- Enter: `orderId == 42`
- Now the breakpoint only fires for order ID 42, not every single order

### Step 5 — Check the database

Often the bug is not in the code but in the data:
```sql
-- Check what's actually in the database
SELECT * FROM Orders WHERE Id = 42

-- Check the migration history
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC

-- Check for NULL values that shouldn't be null
SELECT * FROM Orders WHERE CustomerId IS NULL
```

Enable EF Core SQL logging in development so you can see what queries your code generates:
```csharp
options.UseSqlServer(conn)
       .LogTo(Console.WriteLine, LogLevel.Information)
       .EnableSensitiveDataLogging();
```

### Common enterprise bugs and where to look

| Symptom | Most likely cause |
|---|---|
| 401 Unauthorized | JWT expired, `UseAuthentication()` missing or in wrong order |
| 403 Forbidden | Policy misconfigured, missing role or claim in the JWT |
| 500 on app startup | Missing config key, wrong DI registration, migration failed |
| Slow endpoint | N+1 query — check EF Core SQL logs, count the queries |
| Data not saving | Missing `await SaveChangesAsync()`, wrong transaction scope |
| Intermittent 500 | Deadlock in database, timeout, or concurrency conflict |
| Memory growing over time | Singleton holding Scoped service, HttpClient not from factory |
| Returns wrong user's data | Multi-tenancy filter not applied, shared DbContext across requests |

---

## 28. Distributed Tracing & OpenTelemetry

### What problem does it solve?

In a single app, when something is slow you check the logs for that app. In microservices, one user action triggers calls to 5 different services. Which one is slow? Where did the error actually originate?

Distributed tracing tracks a request across all services using a single `TraceId`. Every service adds its own "span" (a unit of work with start/end time) to the trace. You can see the entire journey in one view.

```
User request → API (50ms) → Auth Service (10ms) → Order Service (200ms) → DB (180ms)
                                                                         ↑ this is the bottleneck
```

### Setting up OpenTelemetry

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.Jaeger
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()           // traces every HTTP request
            .AddEntityFrameworkCoreInstrumentation()  // traces every database query
            .AddHttpClientInstrumentation()           // traces outgoing HTTP calls
            .AddJaegerExporter(options =>
                options.AgentHost = "jaeger");        // sends traces to Jaeger UI
    });
```

Every request now gets a `TraceId`. You can search in the Jaeger UI by TraceId and see every operation across every service, with timing, for that request.

### Correlation ID in logs

Even without full OpenTelemetry, you should include a correlation ID in all log entries so you can find all logs for a specific request:

```csharp
// Middleware that ensures every request has a correlation ID
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // Add to logging context — appears in every log line for this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

---

## 29. Multi-Tenancy

### What it means

A multi-tenant app serves multiple companies (tenants) from one codebase and one database. Company A and Company B both use your app but see only their own data. They share the infrastructure but are completely isolated from each other's data.

Example: Salesforce, GitHub, Jira — one platform, thousands of separate organizations.

### Strategy: Shared tables with a TenantId column

The simplest approach. Every table has a `TenantId` column. Every query filters by it.

```csharp
// Every entity that belongs to a tenant
public abstract class TenantEntity
{
    public int Id { get; set; }
    public string TenantId { get; set; }
}

public class Order : TenantEntity
{
    public decimal Total { get; set; }
    public string Status { get; set; }
}

// Service to get the current tenant from the JWT
public interface ITenantContext
{
    string TenantId { get; }
}

public class TenantContext : ITenantContext
{
    public string TenantId { get; }

    public TenantContext(IHttpContextAccessor accessor)
    {
        TenantId = accessor.HttpContext?.User.FindFirst("tenant_id")?.Value
            ?? throw new UnauthorizedAccessException("No tenant claim in token");
    }
}

// DbContext applies the filter automatically to every query
public class AppDbContext : DbContext
{
    private readonly string _tenantId;

    public AppDbContext(DbContextOptions options, ITenantContext tenantCtx) 
        : base(options)
    {
        _tenantId = tenantCtx.TenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This filter is applied to EVERY query on Order — you can never forget it
        modelBuilder.Entity<Order>()
            .HasQueryFilter(o => o.TenantId == _tenantId);
    }
}

// When creating records, set the TenantId
public async Task CreateOrder(CreateOrderDto dto)
{
    var order = new Order
    {
        Total = dto.Total,
        TenantId = _tenantContext.TenantId // always set it
    };
    _db.Orders.Add(order);
    await _db.SaveChangesAsync();
}
```

The global query filter means every `_db.Orders.ToListAsync()` automatically adds `WHERE TenantId = 'company-abc'`. Developers can't accidentally query another tenant's data.

---

## 30. Soft Deletes & Audit Trails

### Soft Deletes — never physically delete rows

In enterprise, deleting a database row is almost never the right move:
- A customer wants to "delete" their account but you need to keep their purchase history for accounting
- An admin accidentally deletes a user — you need to recover it
- Regulatory requirements say you must keep records for 7 years

Soft delete means setting a flag (`IsDeleted = true`) instead of actually running `DELETE`.

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

public class Order : ISoftDeletable
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

// In DbContext — hide deleted records from all queries automatically
modelBuilder.Entity<Order>()
    .HasQueryFilter(o => !o.IsDeleted);

// "Deleting" an order
public async Task DeleteOrder(int id, string userId)
{
    var order = await _db.Orders.FindAsync(id);
    if (order == null) return;
    
    // Don't call _db.Orders.Remove(order)!
    order.IsDeleted = true;
    order.DeletedAt = DateTime.UtcNow;
    order.DeletedBy = userId;
    
    await _db.SaveChangesAsync();
    // The row stays in the database, but is invisible to all normal queries
}

// If you ever need to see deleted records (admin restore feature)
var deletedOrders = await _db.Orders
    .IgnoreQueryFilters()  // bypasses the HasQueryFilter
    .Where(o => o.IsDeleted)
    .ToListAsync();
```

### Audit Trail — who changed what and when

In enterprise, you need to know when a record was created, by whom, when it was last changed, and by whom. This is required for compliance (SOX, HIPAA) and debugging.

```csharp
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

// Override SaveChangesAsync in DbContext to auto-fill audit fields
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var currentUser = _currentUserService.UserId; // injected service
    var now = DateTime.UtcNow;

    foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = currentUser;
                break;

            case EntityState.Modified:
                entry.Entity.LastModifiedAt = now;
                entry.Entity.LastModifiedBy = currentUser;
                break;
        }
    }

    return await base.SaveChangesAsync(ct);
}
```

Now every time you save an entity that extends `AuditableEntity`, the audit fields are filled automatically. No developer can forget to set them.

---

## 31. Feature Flags

### What problem does it solve?

You want to deploy new code to production but not show it to users yet. Or you want to show it to 10% of users first. Or you want to be able to turn it off instantly if something goes wrong, without a new deployment.

Feature flags are on/off switches for features, controlled from configuration.

```bash
dotnet add package Microsoft.FeatureManagement.AspNetCore
```

```csharp
// appsettings.json
{
  "FeatureManagement": {
    "NewCheckoutFlow": false,
    "BetaDashboard": true,
    "PaymentV2": false
  }
}

// Register
builder.Services.AddFeatureManagement();
```

**In a service:**
```csharp
public class CheckoutService
{
    private readonly IFeatureManager _features;

    public CheckoutService(IFeatureManager features) => _features = features;

    public async Task<CheckoutResult> Checkout(CartDto cart)
    {
        if (await _features.IsEnabledAsync("NewCheckoutFlow"))
        {
            return await NewCheckout(cart); // new code path
        }
        return await LegacyCheckout(cart); // old code path
    }
}
```

**On a controller action:**
```csharp
[FeatureGate("BetaDashboard")]
[HttpGet("beta")]
public IActionResult BetaDashboard()
{
    return Ok(new { message = "You're in the beta!" });
}
// Returns 404 when BetaDashboard is false
```

**Percentage rollout (show to 20% of users):**
```json
{
  "FeatureManagement": {
    "NewCheckoutFlow": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": { "Value": 20 }
        }
      ]
    }
  }
}
```

---

## 32. Docker & Environment Parity

### What problem does it solve?

"It works on my machine" is one of the most common sources of bugs in teams. Docker packages your app and all its dependencies into a container — the same image runs on every developer's machine, in CI/CD, and in production.

### Dockerfile for a .NET app

```dockerfile
# Stage 1: Build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies first (layer caching — faster rebuilds)
COPY ["Api/Api.csproj", "Api/"]
RUN dotnet restore "Api/Api.csproj"

# Copy everything and build
COPY . .
RUN dotnet publish "Api/Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime image (smaller — doesn't include build tools)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
```

### docker-compose for local development

Instead of running SQL Server separately, define everything together:

```yaml
# docker-compose.yml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "Dev@Password123!"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"

  api:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Server=db;Database=AppDb;User Id=sa;Password=Dev@Password123!;TrustServerCertificate=True
    ports:
      - "5000:8080"
    depends_on:
      - db  # wait for db to be ready
```

Run the entire stack with one command:
```bash
docker-compose up
```

### Environment variable naming in Docker

```yaml
environment:
  - ConnectionStrings__Default=Server=...
```

The double underscore `__` maps to `:` in .NET config hierarchy. So `ConnectionStrings__Default` in an env var is the same as `ConnectionStrings:Default` in `appsettings.json`.

### ASPNETCORE_ENVIRONMENT

This environment variable controls which `appsettings.{env}.json` loads:

| Value | What changes |
|---|---|
| `Development` | Developer exception page, verbose EF logging, User Secrets loaded |
| `Staging` | Production-like config, no developer tools |
| `Production` | Minimal logging, no stack traces in error responses, Key Vault |

---

## 33. Testing Strategy

### The three types of tests in enterprise

**Unit tests** — test one class in isolation, mocking all dependencies.
Fast. Run in milliseconds. Test business logic.

**Integration tests** — test multiple layers together, usually with a real database.
Slower. Test that the full request-to-database flow works.

**End-to-end tests** — test the whole system including the frontend.
Slowest. Test real user flows.

### Unit test example

```csharp
// Using xUnit (test framework) + NSubstitute (mocking library)
public class OrderServiceTests
{
    [Fact] // marks this as a test
    public async Task PlaceOrder_WhenCustomerExists_ReturnsOrderId()
    {
        // Arrange — set up everything needed
        var fakeRepo = Substitute.For<IOrderRepository>(); // fake implementation
        fakeRepo.GetCustomerAsync(1).Returns(new Customer { Id = 1, Name = "John" });
        
        var service = new OrderService(fakeRepo);

        // Act — run the thing you're testing
        var orderId = await service.PlaceOrder(new CreateOrderDto
        {
            CustomerId = 1,
            Items = new List<OrderItemDto> { new() { ProductId = 1, Quantity = 2 } }
        });

        // Assert — verify the result
        orderId.Should().BeGreaterThan(0); // FluentAssertions library
        await fakeRepo.Received(1).AddAsync(Arg.Any<Order>()); // verify AddAsync was called once
    }

    [Fact]
    public async Task PlaceOrder_WhenNoItems_ThrowsException()
    {
        var fakeRepo = Substitute.For<IOrderRepository>();
        var service = new OrderService(fakeRepo);

        // Assert that calling with no items throws the right exception
        await Assert.ThrowsAsync<DomainException>(() =>
            service.PlaceOrder(new CreateOrderDto { CustomerId = 1, Items = new() }));
    }
}
```

### Integration test example — tests the real HTTP pipeline

```csharp
// Uses WebApplicationFactory — spins up a real in-memory version of your app
public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the real database with an in-memory one for tests
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase("TestDatabase"));
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task GET_Order_ReturnsOk_WhenOrderExists()
    {
        // This calls the real controller → real service → real validator → in-memory DB
        var response = await _client.GetAsync("/api/v1/orders/1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_CreateOrder_Returns201()
    {
        var dto = new { CustomerId = 1, Items = new[] { new { ProductId = 1, Quantity = 2 } } };
        var response = await _client.PostAsJsonAsync("/api/v1/orders", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## 34. Outbox Pattern & Eventual Consistency

### What problem does it solve?

Imagine: when an order is placed, you need to:
1. Save the order to the database
2. Send a message to a queue (RabbitMQ or Azure Service Bus) to trigger other systems (send email, update inventory, notify the warehouse)

What if step 1 succeeds but the app crashes before step 2? The order is saved but the email is never sent, inventory is never updated. Data is inconsistent.

You can't just put both in a transaction — database transactions and message queues are separate systems that don't share transactions.

### The solution — Outbox Pattern

Save the event to the database in the **same transaction** as the business data. A separate background worker then reads the events and publishes them to the queue. If the background worker fails, the event is still in the database and will be retried.

```
1. Transaction: Save Order + Save "OrderCreated" event to OutboxMessages table
2. Transaction commits — both happen or neither happens
3. Background worker reads OutboxMessages and publishes to queue
4. Background worker marks the event as processed
```

```csharp
// The outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; }   // e.g. "OrderCreated"
    public string Payload { get; set; }     // JSON of the event data
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; } // null = not yet sent
}

// Saving order + outbox event in ONE transaction
public async Task PlaceOrder(CreateOrderDto dto)
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        var order = new Order { CustomerId = dto.CustomerId, Total = dto.Total };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Save the event to the outbox in the SAME transaction
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "OrderCreated",
            Payload = JsonSerializer.Serialize(new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Total = order.Total
            }),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
        // Either BOTH the order AND the outbox event are saved, or NEITHER is
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

// Background worker that publishes outbox events
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageBus _bus;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unprocessed = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            foreach (var message in unprocessed)
            {
                await _bus.PublishAsync(message.EventType, message.Payload, ct);
                message.ProcessedAt = DateTime.UtcNow;
            }

            if (unprocessed.Any())
                await db.SaveChangesAsync(ct);

            await Task.Delay(TimeSpan.FromSeconds(5), ct); // check every 5 seconds
        }
    }
}
```

---

## 35. Minimal APIs vs Controller-Based APIs

### Controller-based API (the classic way)

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Products.AsNoTracking().ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var product = await _db.Products.FindAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductDto dto)
    {
        var product = new Product { Name = dto.Name, Price = dto.Price };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }
}
```

### Minimal API (modern, .NET 6+)

```csharp
// In Program.cs or a separate file using extension methods
app.MapGet("/api/products", async (AppDbContext db) =>
    await db.Products.AsNoTracking().ToListAsync());

app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.MapPost("/api/products", async (CreateProductDto dto, AppDbContext db) =>
{
    var product = new Product { Name = dto.Name, Price = dto.Price };
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{product.Id}", product);
});
```

### When to use which

| Scenario | Recommended |
|---|---|
| Large enterprise app with 50+ endpoints | Controller-based |
| Many filters, versioning, complex auth | Controller-based |
| Simple service with a few endpoints | Minimal API |
| Microservice / serverless function | Minimal API |
| Learning .NET or building a prototype | Either is fine |

Both approaches can coexist in the same application. Many enterprise apps use controller-based for complex endpoints and minimal APIs for simple ones.

---

## Production Readiness Checklist

Before any enterprise app goes live, verify:

- [ ] All secrets in Azure Key Vault or environment variables — nothing hardcoded or in git
- [ ] Serilog configured with structured logging and at least one persistent sink
- [ ] Global exception handler returns clean error responses (no stack traces to clients)
- [ ] Health check endpoints at `/health/live` and `/health/ready`
- [ ] All database queries paginated — no `ToListAsync()` without `Take()`
- [ ] `AsNoTracking()` on all read-only queries
- [ ] N+1 queries identified and fixed (check EF Core SQL logs)
- [ ] DB migrations run as part of deployment process
- [ ] `CancellationToken` passed through all async methods
- [ ] No `.Result` or `.Wait()` calls anywhere
- [ ] Soft delete + audit trail on all business entities
- [ ] Rate limiting on login and public endpoints
- [ ] CORS locked to known origins (no `AllowAnyOrigin` in production)
- [ ] API versioning in place before first external consumer
- [ ] `dotnet list package --vulnerable` runs in CI/CD pipeline
- [ ] Correlation IDs present in all log entries
- [ ] Background services handle `CancellationToken` for graceful shutdown
