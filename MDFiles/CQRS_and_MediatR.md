# CQRS and MediatR in .NET — A Complete Beginner's Guide

---

## Table of Contents

1. [The Problem: Why Do We Need CQRS?](#1-the-problem-why-do-we-need-cqrs)
2. [What is CQRS?](#2-what-is-cqrs)
3. [What is MediatR?](#3-what-is-mediatr)
4. [How CQRS and MediatR Work Together](#4-how-cqrs-and-mediatr-work-together)
5. [Setting Up MediatR in a .NET Project](#5-setting-up-mediatr-in-a-net-project)
6. [Commands — Writing Data](#6-commands--writing-data)
7. [Queries — Reading Data](#7-queries--reading-data)
8. [Pipeline Behaviors — Cross-Cutting Concerns](#8-pipeline-behaviors--cross-cutting-concerns)
9. [Notifications — Events and Broadcasting](#9-notifications--events-and-broadcasting)
10. [A Full Real-World Example: SkillBridge Feature](#10-a-full-real-world-example-skillbridge-feature)
11. [When to Use CQRS and MediatR](#11-when-to-use-cqrs-and-mediatr)
12. [When NOT to Use Them](#12-when-not-to-use-them)
13. [Common Mistakes to Avoid](#13-common-mistakes-to-avoid)
14. [Summary and Mental Model](#14-summary-and-mental-model)

---

## 1. The Problem: Why Do We Need CQRS?

Before jumping into what CQRS is, let's understand the problem it solves.

### The Traditional "Everything in One Place" Approach

Most beginners (and many experienced developers) write code like this:

```csharp
// Traditional approach — one service does everything
public class UserService
{
    private readonly ApplicationDbContext _db;

    public UserService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Reading data
    public async Task<User> GetUserByIdAsync(int id)
    {
        return await _db.Users.FindAsync(id);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _db.Users.ToListAsync();
    }

    // Writing data
    public async Task<User> CreateUserAsync(CreateUserDto dto)
    {
        var user = new User { Name = dto.Name, Email = dto.Email };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        user.Name = dto.Name;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }
}
```

This works fine for small apps. But as the app grows, four serious problems appear. Let's look at each one with real code.

---

### Problem 1: Reads and Writes Have Very Different Performance Needs

Reads happen far more often than writes. A typical app might call `GetUserByIdAsync` hundreds of times per minute, but `CreateUserAsync` only a few times an hour. Because of this imbalance, reads need different optimizations:

- **Caching** — return from memory instead of hitting the database every time
- **Projections** — only `SELECT` the columns you actually need, not all 30
- **Read replicas** — use a separate, read-only database to take load off the primary

The problem is: **when reads and writes share the same service and the same model, you can't cleanly apply these optimizations.**

Here's what happens when you try to add caching to the traditional service:

```csharp
// Traditional UserService — trying to add caching
public class UserService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache; // New dependency needed for reads only

    public UserService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<User> GetUserByIdAsync(int id)
    {
        // Cache key
        var cacheKey = $"user_{id}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out User cachedUser))
            return cachedUser;

        var user = await _db.Users.FindAsync(id);

        // Store in cache for 5 minutes
        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));

        return user;
    }

    // THE PROBLEM: When the user is updated or deleted, we MUST invalidate the cache.
    // But how does UpdateUserAsync even know which cache keys to clear?
    // It's in the SAME service, so we add this:
    public async Task UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        user.Name = dto.Name;
        await _db.SaveChangesAsync();

        // Now we have to manually clear the cache here too
        _cache.Remove($"user_{id}");
        // What if there's also a "GetAllUsers" cache? We'd need to clear that too.
        _cache.Remove("all_users");
        // What about "GetUserByEmail"? "GetUsersByRole"?
        // This becomes a maintenance nightmare.
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        if (_cache.TryGetValue("all_users", out List<User> cachedUsers))
            return cachedUsers;

        // ANOTHER PROBLEM: We're loading the ENTIRE User entity including
        // PasswordHash, SecurityStamp, TwoFactorEnabled, and 20 other columns
        // just to show a name and email in a dropdown list.
        // There's no clean way to return a "slim" version because the method
        // signature returns List<User> — the full entity.
        var users = await _db.Users.ToListAsync();

        _cache.Set("all_users", users, TimeSpan.FromMinutes(5));
        return users;
    }

    // And if we want reads to use a READ REPLICA database,
    // we'd need a second DbContext injected here — the constructor
    // now has to juggle two databases AND a cache, just for one service.
}
```

**What goes wrong:**
- The write methods (`UpdateUserAsync`, `DeleteUserAsync`) now have to know about caching — that's not their job.
- Every time you add a new read method, you have to remember to invalidate the cache in every write method.
- The `User` entity returned from reads is always the full database row — you can't easily slim it down without changing the return type everywhere.
- Adding a read replica means injecting a second `DbContext`, making the constructor and service harder to understand.

The root cause: **reads and writes are tangled together**, so any optimization to one bleeds into the other.

---

### Problem 2: Business Logic Grows Into a "Ball of Mud"

At first, `CreateUserAsync` is simple. But real business requirements keep arriving:

> "When a user registers, send them a welcome email."
> "Also log it to our audit table."
> "Also check against a third-party fraud API."
> "Also invalidate the user-list cache."
> "Also notify the admin via Slack."

Every new requirement gets bolted onto the same method. Here's what that looks like after 6 months of a real project:

```csharp
// CreateUserAsync six months into the project — a real ball of mud
public async Task<User> CreateUserAsync(CreateUserDto dto)
{
    // ─── Validation ─────────────────────────────────────────────────
    if (string.IsNullOrWhiteSpace(dto.Name))
        throw new ArgumentException("Name is required.");
    if (string.IsNullOrWhiteSpace(dto.Email))
        throw new ArgumentException("Email is required.");
    if (!dto.Email.Contains("@"))
        throw new ArgumentException("Invalid email.");
    if (dto.Password.Length < 8)
        throw new ArgumentException("Password too short.");

    var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
    if (emailExists)
        throw new InvalidOperationException("Email already taken.");

    // ─── Fraud Check (external API) ──────────────────────────────────
    // Now we need IFraudDetectionService injected in the constructor
    var fraudResult = await _fraudService.CheckAsync(dto.Email, dto.IpAddress);
    if (fraudResult.IsHighRisk)
        throw new InvalidOperationException("Account creation blocked.");

    // ─── Create the User ─────────────────────────────────────────────
    var user = new User
    {
        Name = dto.Name,
        Email = dto.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        CreatedAt = DateTime.UtcNow
    };
    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    // ─── Send Welcome Email ───────────────────────────────────────────
    // Now we need IEmailService injected too
    try
    {
        await _emailService.SendWelcomeEmailAsync(user.Email, user.Name);
    }
    catch (Exception ex)
    {
        // If email fails, do we roll back the user? Do we swallow the error?
        // No clear answer — this is now a design problem inside a method.
        _logger.LogError(ex, "Welcome email failed for user {UserId}", user.Id);
    }

    // ─── Audit Log ────────────────────────────────────────────────────
    // Now we need IAuditLogService injected too
    await _auditService.LogAsync("UserCreated", user.Id, $"New user {user.Email}");

    // ─── Cache Invalidation ───────────────────────────────────────────
    // Now we need IMemoryCache injected too
    _cache.Remove("all_users");

    // ─── Slack Notification ───────────────────────────────────────────
    // Now we need ISlackService injected too
    if (_config.GetValue<bool>("Notifications:NewUserSlack"))
        await _slackService.NotifyAsync($"New user registered: {user.Email}");

    return user;
}
```

And the constructor has become this:

```csharp
// The constructor is now a symptom of the problem
public UserService(
    ApplicationDbContext db,
    IEmailService emailService,
    IAuditLogService auditService,
    IFraudDetectionService fraudService,
    IMemoryCache cache,
    ISlackService slackService,
    IConfiguration config,
    ILogger<UserService> logger)  // 8 dependencies — and growing
{
    _db = db;
    _emailService = emailService;
    _auditService = auditService;
    _fraudService = fraudService;
    _cache = cache;
    _slackService = slackService;
    _config = config;
    _logger = logger;
}
```

**What goes wrong:**
- The method is now ~60 lines doing 6 completely different things. Changing any one of them risks breaking the others.
- Every new requirement means editing this same method — it never gets smaller.
- The 8-dependency constructor means even instantiating `UserService` requires wiring up 8 things. In tests, you have to mock all 8 just to test one thing.
- If Slack goes down, should user creation fail? Questions like this shouldn't even exist inside `CreateUserAsync`, but here they are.

This is the "ball of mud" — everything is entangled, nothing is easy to change, and nobody wants to touch it.

---

### Problem 3: Testing Becomes a Nightmare

Here's how you'd write a unit test for `GetUserByIdAsync` in the traditional service:

```csharp
// A test for a SIMPLE READ — just "find user by ID"
[Test]
public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
{
    // You have to provide ALL 8 constructor dependencies
    // even though GetUserByIdAsync only uses the database.

    var mockDb = CreateInMemoryDatabase();         // needed — GetUserById uses this
    var mockEmail = Mock.Of<IEmailService>();       // NOT needed for this test — but required by constructor
    var mockAudit = Mock.Of<IAuditLogService>();    // NOT needed for this test — but required
    var mockFraud = Mock.Of<IFraudDetectionService>(); // NOT needed — but required
    var mockCache = Mock.Of<IMemoryCache>();        // sort of needed (caching is in GetById now)
    var mockSlack = Mock.Of<ISlackService>();       // NOT needed — but required
    var mockConfig = Mock.Of<IConfiguration>();     // NOT needed — but required
    var mockLogger = Mock.Of<ILogger<UserService>>(); // NOT needed — but required

    // After all that setup, here's the actual service
    var service = new UserService(
        mockDb, mockEmail, mockAudit, mockFraud,
        mockCache, mockSlack, mockConfig, mockLogger);

    // The actual test — 1 line
    var user = await service.GetUserByIdAsync(1);

    Assert.That(user.Id, Is.EqualTo(1));
}
```

**What goes wrong:**
- Setting up 7 mocks just to test a method that only uses 1 of them.
- When someone adds a 9th dependency to `UserService`, **every single test that constructs the service breaks** — even tests that have nothing to do with the new dependency.
- Tests are brittle. They break not because the logic they're testing changed, but because something unrelated changed.
- Developers start avoiding writing tests because setup takes longer than the test itself.

The problem is that **a test for a read operation is forced to know about all the write operation dependencies** because they live in the same class.

---

### Problem 4: Controllers Grow Out of Control

As the app grows, controllers start coordinating multiple services and doing logic they shouldn't:

```csharp
// UsersController — 6 months into the project
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    // Growing list of injected services — every new feature adds one
    private readonly UserService _userService;
    private readonly CourseService _courseService;
    private readonly NotificationService _notificationService;
    private readonly ReportService _reportService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserService userService,
        CourseService courseService,
        NotificationService notificationService,
        ReportService reportService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _courseService = courseService;
        _notificationService = notificationService;
        _reportService = reportService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto dto)
    {
        // Controller doing validation — this doesn't belong here
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Controller doing business logic coordination — this doesn't belong here
        var user = await _userService.CreateUserAsync(dto);

        // Controller calling 3 services to coordinate a single action
        await _notificationService.NotifyAdminAsync(user.Id);
        var report = await _reportService.GenerateWelcomeReportAsync(user.Id);

        // Controller deciding what to do if a step fails
        if (report == null)
        {
            _logger.LogWarning("Welcome report generation failed for user {UserId}", user.Id);
            // Do we still return success? Partial success? This is a business decision
            // living inside an HTTP handler — wrong place.
        }

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpGet("{id}/dashboard")]
    public async Task<IActionResult> GetDashboard(int id)
    {
        // Controller manually assembling data from multiple services
        // This is business logic, not HTTP handling
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();

        var courses = await _courseService.GetEnrolledCoursesAsync(id);
        var notifications = await _notificationService.GetUnreadAsync(id);
        var report = await _reportService.GetSummaryAsync(id);

        // Controller building the response shape — this is also wrong
        return Ok(new
        {
            User = user,
            Courses = courses,
            Notifications = notifications,
            Report = report
        });
    }
}
```

**What goes wrong:**
- The controller has 5 constructor dependencies. As the app grows, this becomes 10, 15. The controller becomes impossible to test and hard to read.
- Business logic is leaking into the controller (`if (report == null)` — that's a business decision, not an HTTP routing decision).
- `GetDashboard` manually calls 4 services and assembles a response. If the business requirement changes (e.g., also include `RecentActivity`), you have to edit the controller, not the business logic layer.
- Adding a new endpoint means adding more dependencies. Adding a new dependency breaks all existing tests that construct the controller.
- Controllers were designed to handle one thing: map HTTP requests to responses. When they grow into coordinators, they violate that responsibility.

---

### The Pattern That Causes All Four Problems

All four problems trace back to the **same root cause**:

```
┌──────────────────────────────────────────────────────────────────┐
│                        UserService                               │
│                                                                  │
│  GetUserById     ──┐                                            │
│  GetAllUsers     ──┤ READS (need: cache, projections, replica)  │
│  SearchUsers     ──┘                                            │
│                                                                  │
│  CreateUser      ──┐                                            │
│  UpdateUser      ──┤ WRITES (need: validation, email, audit,    │
│  DeleteUser      ──┘          fraud check, notifications)       │
│                                                                  │
│  All 8 dependencies injected at once into one constructor        │
│  All logic in one place — no separation, no isolation            │
└──────────────────────────────────────────────────────────────────┘
```

When reads and writes share a class:
- Read optimizations (caching, projections) bleed into write code
- Write responsibilities (email, audit, fraud) bloat the class
- Tests for reads are burdened by write dependencies
- The class grows indefinitely with no natural stopping point

CQRS fixes this by making the separation **explicit and enforced by the type system**. Every operation is its own class, with only the dependencies it actually needs. That's the entire insight.

---

### The Core Insight

Reading data and writing data are **fundamentally different operations**:

| Reading (Query)                        | Writing (Command)                     |
|----------------------------------------|---------------------------------------|
| Should be fast                         | Can be slower (validation, side effects) |
| Can be cached                          | Should never be cached                |
| Returns data                           | Changes state, often returns nothing or just an ID |
| Can use a simplified/optimized model   | Needs the full domain model           |
| Can hit a read replica                 | Must hit the primary database         |
| No side effects                        | Has side effects (emails, events, logs) |

CQRS formalizes this insight into an architectural pattern.

---

## 2. What is CQRS?

**CQRS** stands for **Command Query Responsibility Segregation**.

The name is a mouthful, but the idea is simple:

> **Separate the code that reads data (Queries) from the code that writes data (Commands).**

That's it. You split your operations into two categories:

- **Command** — An intention to *change* something. "Create this user", "Delete this post", "Enroll in this course". Commands don't return data (or return minimal data like the new ID).
- **Query** — An intention to *read* something. "Give me the user with ID 5", "List all courses". Queries never change state.

### The Command/Query Split Visualized

```
                        ┌─────────────────────────────────────────────────┐
                        │                  Your Application                │
                        │                                                  │
 HTTP Request           │   ┌──────────────┐       ┌──────────────────┐   │
 POST /users    ──────► │   │   Command    │       │     Query        │   │
 GET /users/5   ──────► │   │  (Writes)   │       │    (Reads)       │   │
                        │   │              │       │                  │   │
                        │   │ CreateUser   │       │ GetUserById      │   │
                        │   │ UpdateUser   │       │ GetAllUsers      │   │
                        │   │ DeleteUser   │       │ SearchUsers      │   │
                        │   └──────┬───────┘       └────────┬─────────┘   │
                        │          │                         │             │
                        └──────────┼─────────────────────────┼────────────┘
                                   │                         │
                                   ▼                         ▼
                            ┌──────────┐             ┌──────────────┐
                            │  Write   │             │  Read DB /   │
                            │    DB    │             │  Cache/View  │
                            └──────────┘             └──────────────┘
```

### Why Does This Help?

1. **Single Responsibility** — Each class does one thing. `CreateUserCommandHandler` only handles user creation. It's easy to understand and change.
2. **Independent Scaling** — You can scale your read side independently of your write side. Add caching to all queries without touching commands.
3. **Simpler Testing** — Test each command/query in isolation.
4. **Clear Intent** — When you see `CreateUserCommand`, you instantly know it changes data. When you see `GetUserByIdQuery`, you know it only reads.

---

## 3. What is MediatR?

Now we have CQRS as a *concept*. But how do we actually *implement* it without creating a mess of dependencies?

This is where **MediatR** comes in.

### The Problem MediatR Solves

If you implement CQRS manually, your controller would look like:

```csharp
// Without MediatR — controller depends on many handlers
public class UsersController : ControllerBase
{
    private readonly CreateUserCommandHandler _createHandler;
    private readonly UpdateUserCommandHandler _updateHandler;
    private readonly DeleteUserCommandHandler _deleteHandler;
    private readonly GetUserByIdQueryHandler _getByIdHandler;
    private readonly GetAllUsersQueryHandler _getAllHandler;

    // Constructor has 5 dependencies just for users!
    public UsersController(
        CreateUserCommandHandler createHandler,
        UpdateUserCommandHandler updateHandler,
        DeleteUserCommandHandler deleteHandler,
        GetUserByIdQueryHandler getByIdHandler,
        GetAllUsersQueryHandler getAllHandler)
    {
        _createHandler = createHandler;
        // ... etc
    }
}
```

As you add more operations, the constructor explodes. Adding a new feature requires touching the controller. This is tightly coupled.

### MediatR's Solution: The Mediator Pattern

MediatR implements the **Mediator design pattern**. Instead of the controller knowing about all handlers, it only knows about one thing: the `IMediator` interface.

The controller says: *"Here is a request object. I don't care who handles it — just handle it."*

MediatR figures out which handler to route the request to, runs it, and returns the result.

```
Controller ──── sends ────► IMediator ──── routes to ────► Correct Handler
                               │
                               └── also runs Pipeline Behaviors (logging, validation, etc.)
```

Here's what the controller looks like **with** MediatR:

```csharp
// With MediatR — controller only depends on IMediator
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id));
        return Ok(result);
    }
}
```

**One dependency. Clean. Easy to add new endpoints without touching existing code.**

### Key MediatR Concepts

| Concept | Interface | What It Does |
|---------|-----------|--------------|
| **Request** | `IRequest<TResponse>` | A Command or Query object — carries the data needed to do the operation |
| **Handler** | `IRequestHandler<TRequest, TResponse>` | The class that actually does the work |
| **Notification** | `INotification` | An event that can be handled by multiple handlers |
| **Pipeline Behavior** | `IPipelineBehavior<TRequest, TResponse>` | Middleware that runs before/after every handler (logging, validation, etc.) |

---

## 4. How CQRS and MediatR Work Together

CQRS is the *pattern* (separate reads and writes). MediatR is the *tool* that makes it easy to implement.

```
HTTP Request
     │
     ▼
 Controller
     │
     │ _mediator.Send(new CreateUserCommand(...))
     ▼
  MediatR
     │
     │ Routes to correct handler
     │ Runs Pipeline Behaviors (logging, validation...)
     ▼
 CreateUserCommandHandler.Handle(...)
     │
     │ Does the actual work
     ▼
  Database / External Services
     │
     ▼
  Result returned back through the chain
```

---

## 5. Setting Up MediatR in a .NET Project

### Step 1: Install NuGet Packages

```xml
<!-- In your .csproj file -->
<PackageReference Include="MediatR" Version="12.4.1" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
```

Or via Package Manager Console:
```
Install-Package MediatR
```

### Step 2: Register MediatR in Program.cs

```csharp
// Program.cs
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Tell MediatR to scan this assembly for all IRequestHandler implementations
// and register them automatically
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();
app.MapControllers();
app.Run();
```

> **What does `RegisterServicesFromAssembly` do?**
> It scans your project for every class that implements `IRequestHandler<,>` and automatically registers them with the DI container. You don't need to manually register each handler — MediatR does it for you.

### Project Structure

A well-organized CQRS project looks like this:

```
YourApp/
├── Controllers/
│   └── UsersController.cs
├── Features/
│   └── Users/
│       ├── Commands/
│       │   ├── CreateUser/
│       │   │   ├── CreateUserCommand.cs       ← The request object
│       │   │   ├── CreateUserCommandHandler.cs ← The handler
│       │   │   └── CreateUserCommandValidator.cs ← Validation (optional)
│       │   └── DeleteUser/
│       │       ├── DeleteUserCommand.cs
│       │       └── DeleteUserCommandHandler.cs
│       └── Queries/
│           ├── GetUserById/
│           │   ├── GetUserByIdQuery.cs
│           │   └── GetUserByIdQueryHandler.cs
│           └── GetAllUsers/
│               ├── GetAllUsersQuery.cs
│               └── GetAllUsersQueryHandler.cs
├── Models/
│   └── User.cs
└── Data/
    └── ApplicationDbContext.cs
```

> **Why organize by Feature (vertical slices)?**
> Instead of putting all commands in one folder and all queries in another (horizontal slices), grouping by feature means everything related to "Users" lives together. When you work on user features, you only need to look in one place. This is called **Vertical Slice Architecture** and pairs perfectly with CQRS + MediatR.

---

## 6. Commands — Writing Data

A **Command** is a request to change state. Think of it as an instruction: "Do this thing."

### Anatomy of a Command

A command has two parts:
1. **The Command Object** — Carries the data needed (like a DTO). Implements `IRequest<TResponse>`.
2. **The Handler** — Does the actual work. Implements `IRequestHandler<TCommand, TResponse>`.

### Example 1: Create User Command

```csharp
// Features/Users/Commands/CreateUser/CreateUserCommand.cs

using MediatR;

// This is the Command — it's just a data carrier.
// IRequest<int> means "when this command is handled, it returns an int (the new user's ID)"
public record CreateUserCommand(
    string Name,
    string Email,
    string Password
) : IRequest<int>;
```

> **Why use `record` instead of `class`?**
> Records in C# are immutable by default and have value-based equality. Commands should be immutable — you don't want the handler modifying the command while processing it. Records are the perfect fit.

```csharp
// Features/Users/Commands/CreateUser/CreateUserCommandHandler.cs

using MediatR;
using Microsoft.EntityFrameworkCore;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly ApplicationDbContext _db;

    // Only inject what THIS handler needs — not the entire UserService
    public CreateUserCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    // This is the method MediatR calls when someone sends a CreateUserCommand
    // 'request' is the command object (with Name, Email, Password)
    // 'cancellationToken' lets the operation be cancelled (e.g., if user navigates away)
    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Check for duplicate email
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // Return the new user's ID
        return user.Id;
    }
}
```

```csharp
// Controllers/UsersController.cs

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        // Send the command to MediatR — it figures out which handler to use
        var userId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = userId }, new { id = userId });
    }
}
```

**Flow:**
1. POST /api/users hits the controller
2. ASP.NET deserializes the JSON body into a `CreateUserCommand`
3. Controller calls `_mediator.Send(command)`
4. MediatR looks up `IRequestHandler<CreateUserCommand, int>` — finds `CreateUserCommandHandler`
5. Calls `Handle(command, cancellationToken)`
6. Handler creates the user, returns the ID
7. Controller returns `201 Created`

---

### Example 2: Update User Command (Command that returns nothing)

Sometimes a command has no return value — it just performs an action.

```csharp
// Features/Users/Commands/UpdateUser/UpdateUserCommand.cs

using MediatR;

// IRequest<Unit> means "returns nothing" — Unit is MediatR's equivalent of void
public record UpdateUserCommand(
    int UserId,
    string Name,
    string Email
) : IRequest<Unit>;
```

```csharp
// Features/Users/Commands/UpdateUser/UpdateUserCommandHandler.cs

using MediatR;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Unit>
{
    private readonly ApplicationDbContext _db;

    public UpdateUserCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {request.UserId} not found.");
        }

        user.Name = request.Name;
        user.Email = request.Email;

        await _db.SaveChangesAsync(cancellationToken);

        // Unit.Value is MediatR's "void" — means "I'm done, nothing to return"
        return Unit.Value;
    }
}
```

```csharp
// In the controller
[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] UpdateUserCommand command)
{
    // Ensure the ID in the URL matches the command
    // (alternatively, you can create a separate DTO and construct the command here)
    var updateCommand = command with { UserId = id };
    await _mediator.Send(updateCommand);
    return NoContent(); // 204
}
```

---

### Example 3: Delete User Command

```csharp
// Features/Users/Commands/DeleteUser/DeleteUserCommand.cs

using MediatR;

public record DeleteUserCommand(int UserId) : IRequest<Unit>;
```

```csharp
// Features/Users/Commands/DeleteUser/DeleteUserCommandHandler.cs

using MediatR;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    private readonly ApplicationDbContext _db;

    public DeleteUserCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {request.UserId} not found.");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

---

## 7. Queries — Reading Data

A **Query** is a request to read data. It **never** changes state.

### Why Queries Are Different from Commands

- Queries can return **projections** — you don't need to load the full domain object. If you only need `Name` and `Email` for a dropdown, don't load all 30 columns.
- Queries can be **cached** — the same query with the same parameters returns the same result.
- Queries can hit a **read replica** or different data source.

### Example 1: Get User by ID Query

First, define a **DTO (Data Transfer Object)** — a simple class that holds only the data you want to return (not the full domain entity):

```csharp
// Features/Users/Queries/GetUserById/UserDto.cs

// This is what we return to the caller — not the full User entity
// Only expose what the caller needs
public record UserDto(
    int Id,
    string Name,
    string Email,
    DateTime CreatedAt
);
```

```csharp
// Features/Users/Queries/GetUserById/GetUserByIdQuery.cs

using MediatR;

// IRequest<UserDto> — this query returns a UserDto
public record GetUserByIdQuery(int UserId) : IRequest<UserDto>;
```

```csharp
// Features/Users/Queries/GetUserById/GetUserByIdQueryHandler.cs

using MediatR;
using Microsoft.EntityFrameworkCore;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly ApplicationDbContext _db;

    public GetUserByIdQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        // Use .Select() to project directly to a DTO — more efficient than loading the full entity
        // This generates: SELECT Id, Name, Email, CreatedAt FROM Users WHERE Id = @id
        // It does NOT load PasswordHash or other sensitive/unused fields
        var user = await _db.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {request.UserId} not found.");
        }

        return user;
    }
}
```

```csharp
// In the controller
[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id)
{
    var user = await _mediator.Send(new GetUserByIdQuery(id));
    return Ok(user);
}
```

---

### Example 2: Get All Users with Filtering and Pagination

Real-world queries often need filtering and pagination. Here's how to handle that:

```csharp
// Features/Users/Queries/GetUsers/GetUsersQuery.cs

using MediatR;

public record GetUsersQuery(
    string? SearchTerm,   // nullable — optional filter
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<UserDto>>;

// A generic wrapper for paginated results
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
```

```csharp
// Features/Users/Queries/GetUsers/GetUsersQueryHandler.cs

using MediatR;
using Microsoft.EntityFrameworkCore;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private readonly ApplicationDbContext _db;

    public GetUsersQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        // Start with a base query
        var query = _db.Users.AsQueryable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(u =>
                u.Name.Contains(request.SearchTerm) ||
                u.Email.Contains(request.SearchTerm));
        }

        // Count total results BEFORE pagination (for UI pagination controls)
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and project to DTO
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResult<UserDto>(items, totalCount, request.Page, request.PageSize, totalPages);
    }
}
```

```csharp
// In the controller
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] GetUsersQuery query)
{
    // ASP.NET automatically maps query string params to the record constructor
    // GET /api/users?searchTerm=john&page=2&pageSize=10
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

---

## 8. Pipeline Behaviors — Cross-Cutting Concerns

This is one of the most powerful features of MediatR.

A **Pipeline Behavior** is middleware that wraps around every request handler. It runs before and/or after the handler, letting you add behavior globally without touching individual handlers.

Think of it like ASP.NET middleware, but for your handlers.

```
Request
   │
   ▼
[Logging Behavior]          ← runs first
   │
   ▼
[Validation Behavior]       ← runs second
   │
   ▼
[Performance Behavior]      ← runs third
   │
   ▼
[Actual Handler]            ← does the real work
   │
   ▼
[Performance Behavior]      ← measures time
   │
   ▼
[Logging Behavior]          ← logs completion
   │
   ▼
Response
```

### Example 1: Logging Behavior

Automatically logs every request and response — zero changes to individual handlers:

```csharp
// Behaviors/LoggingBehavior.cs

using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// TRequest is the request type (e.g., CreateUserCommand)
// TResponse is the response type (e.g., int)
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,  // 'next' calls the next behavior or handler
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}: {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Call the next behavior in the pipeline (or the handler itself)
            var response = await next();

            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            throw; // Re-throw — don't swallow the exception
        }
    }
}
```

---

### Example 2: Validation Behavior with FluentValidation

Instead of putting validation logic inside every handler, use a behavior to automatically validate all requests:

First, install FluentValidation:
```
Install-Package FluentValidation
Install-Package FluentValidation.DependencyInjectionExtensions
```

Create a validator for `CreateUserCommand`:

```csharp
// Features/Users/Commands/CreateUser/CreateUserCommandValidator.cs

using FluentValidation;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
```

Now create the behavior that uses validators:

```csharp
// Behaviors/ValidationBehavior.cs

using FluentValidation;
using MediatR;

public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // MediatR's DI will inject ALL validators registered for TRequest
    // If there's no validator, this list will be empty — no error
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            // No validators for this request type — just proceed
            return await next();
        }

        // Create a validation context for the request
        var context = new ValidationContext<TRequest>(request);

        // Run all validators concurrently
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Collect all failures
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Any())
        {
            // Throw a single exception with ALL validation errors
            throw new ValidationException(failures);
        }

        // All validation passed — proceed to the handler
        return await next();
    }
}
```

Register both behaviors in `Program.cs`:

```csharp
// Program.cs

using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    // Register behaviors — order matters! They run in registration order.
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Register all FluentValidation validators in the assembly
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
```

Now every request is automatically validated. If `CreateUserCommand` has invalid data, `ValidationBehavior` throws before the handler ever runs.

Handle validation errors globally in an exception handler:

```csharp
// Middleware/ExceptionHandlingMiddleware.cs

using FluentValidation;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            // Turn validation failures into a 400 Bad Request with clear error messages
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsJsonAsync(new { errors });
        }
        catch (KeyNotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }
}

// Register in Program.cs
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

---

### Example 3: Performance Monitoring Behavior

Warns when a handler takes too long:

```csharp
// Behaviors/PerformanceBehavior.cs

using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

public class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int WarningThresholdMs = 500; // Warn if handler takes more than 500ms

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > WarningThresholdMs)
        {
            _logger.LogWarning(
                "SLOW REQUEST: {RequestName} took {ElapsedMs}ms. Request: {@Request}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                request);
        }

        return response;
    }
}
```

---

## 9. Notifications — Events and Broadcasting

Sometimes, after something happens, you need to notify **multiple** parts of the system. For example, when a user registers:
- Send a welcome email
- Log an audit entry
- Update analytics
- Maybe notify an admin

With MediatR **Notifications**, you can publish an event and have multiple independent handlers respond to it — without the publisher needing to know anything about the subscribers.

### Example: User Registered Notification

```csharp
// Features/Users/Events/UserRegisteredNotification.cs

using MediatR;

// INotification (no generic type) — notifications don't return values
public record UserRegisteredNotification(
    int UserId,
    string Name,
    string Email,
    DateTime RegisteredAt
) : INotification;
```

Create multiple handlers for the same notification:

```csharp
// Features/Users/Events/Handlers/SendWelcomeEmailHandler.cs

using MediatR;

public class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly IEmailService _emailService;

    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendAsync(
            to: notification.Email,
            subject: "Welcome to SkillBridge!",
            body: $"Hi {notification.Name}, thanks for registering!");
    }
}
```

```csharp
// Features/Users/Events/Handlers/LogAuditEntryHandler.cs

using MediatR;

public class LogAuditEntryHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly ApplicationDbContext _db;

    public LogAuditEntryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Event = "UserRegistered",
            EntityId = notification.UserId,
            Timestamp = notification.RegisteredAt,
            Details = $"New user {notification.Email} registered."
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

Publish the notification from the command handler:

```csharp
// Features/Users/Commands/CreateUser/CreateUserCommandHandler.cs

using MediatR;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator; // Inject IMediator to publish notifications

    public CreateUserCommandHandler(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // Publish the event — all INotificationHandler<UserRegisteredNotification>
        // implementations will be called automatically by MediatR
        await _mediator.Publish(new UserRegisteredNotification(
            user.Id,
            user.Name,
            user.Email,
            user.CreatedAt
        ), cancellationToken);

        return user.Id;
    }
}
```

**Key benefit:** If you later need to add a new notification handler (e.g., "notify admin"), you just create a new class implementing `INotificationHandler<UserRegisteredNotification>`. You don't touch any existing code.

---

## 10. A Full Real-World Example: SkillBridge Feature

Let's put it all together with a complete example: **enrolling a student in a course**.

### Domain Models

```csharp
// Models/Course.cs
public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public int MaxStudents { get; set; }
    public bool IsActive { get; set; }
    public List<Enrollment> Enrollments { get; set; } = new();
}

// Models/Enrollment.cs
public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public DateTime EnrolledAt { get; set; }
    public User Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
```

### The Command

```csharp
// Features/Enrollments/Commands/EnrollStudent/EnrollStudentCommand.cs

using MediatR;

public record EnrollStudentCommand(
    int StudentId,
    int CourseId
) : IRequest<EnrollStudentResult>;

public record EnrollStudentResult(
    int EnrollmentId,
    string CourseTitle,
    DateTime EnrolledAt
);
```

### The Validator

```csharp
// Features/Enrollments/Commands/EnrollStudent/EnrollStudentCommandValidator.cs

using FluentValidation;

public class EnrollStudentCommandValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0).WithMessage("Invalid student ID.");

        RuleFor(x => x.CourseId)
            .GreaterThan(0).WithMessage("Invalid course ID.");
    }
}
```

### The Handler

```csharp
// Features/Enrollments/Commands/EnrollStudent/EnrollStudentCommandHandler.cs

using MediatR;
using Microsoft.EntityFrameworkCore;

public class EnrollStudentCommandHandler : IRequestHandler<EnrollStudentCommand, EnrollStudentResult>
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;

    public EnrollStudentCommandHandler(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<EnrollStudentResult> Handle(
        EnrollStudentCommand request,
        CancellationToken cancellationToken)
    {
        // Load the course with its current enrollments
        var course = await _db.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
            throw new KeyNotFoundException($"Course {request.CourseId} not found.");

        if (!course.IsActive)
            throw new InvalidOperationException("This course is no longer active.");

        // Business rule: check capacity
        if (course.Enrollments.Count >= course.MaxStudents)
            throw new InvalidOperationException($"Course '{course.Title}' is full.");

        // Check if already enrolled
        var alreadyEnrolled = course.Enrollments
            .Any(e => e.StudentId == request.StudentId);

        if (alreadyEnrolled)
            throw new InvalidOperationException("Student is already enrolled in this course.");

        // Verify student exists
        var studentExists = await _db.Users
            .AnyAsync(u => u.Id == request.StudentId, cancellationToken);

        if (!studentExists)
            throw new KeyNotFoundException($"Student {request.StudentId} not found.");

        // Create the enrollment
        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            EnrolledAt = DateTime.UtcNow
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync(cancellationToken);

        // Publish event — other parts of the system react independently
        await _mediator.Publish(new StudentEnrolledNotification(
            enrollment.Id,
            request.StudentId,
            request.CourseId,
            course.Title,
            enrollment.EnrolledAt
        ), cancellationToken);

        return new EnrollStudentResult(enrollment.Id, course.Title, enrollment.EnrolledAt);
    }
}
```

### The Notification and Handlers

```csharp
// Features/Enrollments/Events/StudentEnrolledNotification.cs

using MediatR;

public record StudentEnrolledNotification(
    int EnrollmentId,
    int StudentId,
    int CourseId,
    string CourseTitle,
    DateTime EnrolledAt
) : INotification;
```

```csharp
// Features/Enrollments/Events/Handlers/SendEnrollmentConfirmationEmailHandler.cs

using MediatR;

public class SendEnrollmentConfirmationEmailHandler
    : INotificationHandler<StudentEnrolledNotification>
{
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _db;

    public SendEnrollmentConfirmationEmailHandler(IEmailService emailService, ApplicationDbContext db)
    {
        _emailService = emailService;
        _db = db;
    }

    public async Task Handle(StudentEnrolledNotification notification, CancellationToken cancellationToken)
    {
        var student = await _db.Users.FindAsync(
            new object[] { notification.StudentId },
            cancellationToken);

        if (student is not null)
        {
            await _emailService.SendAsync(
                to: student.Email,
                subject: $"Enrollment Confirmed: {notification.CourseTitle}",
                body: $"Hi {student.Name}, you've successfully enrolled in '{notification.CourseTitle}'!");
        }
    }
}
```

### The Query

```csharp
// Features/Enrollments/Queries/GetStudentEnrollments/GetStudentEnrollmentsQuery.cs

using MediatR;

public record GetStudentEnrollmentsQuery(int StudentId) : IRequest<List<EnrollmentDto>>;

public record EnrollmentDto(
    int EnrollmentId,
    int CourseId,
    string CourseTitle,
    string TeacherName,
    DateTime EnrolledAt
);
```

```csharp
// Features/Enrollments/Queries/GetStudentEnrollments/GetStudentEnrollmentsQueryHandler.cs

using MediatR;
using Microsoft.EntityFrameworkCore;

public class GetStudentEnrollmentsQueryHandler
    : IRequestHandler<GetStudentEnrollmentsQuery, List<EnrollmentDto>>
{
    private readonly ApplicationDbContext _db;

    public GetStudentEnrollmentsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<EnrollmentDto>> Handle(
        GetStudentEnrollmentsQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Enrollments
            .Where(e => e.StudentId == request.StudentId)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.CourseId,
                e.Course.Title,
                e.Course.Teacher.Name,
                e.EnrolledAt))
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync(cancellationToken);
    }
}
```

### The Controller

```csharp
// Controllers/EnrollmentsController.cs

[ApiController]
[Route("api/[controller]")]
public class EnrollmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EnrollmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Enroll([FromBody] EnrollStudentCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("student/{studentId}")]
    public async Task<IActionResult> GetStudentEnrollments(int studentId)
    {
        var enrollments = await _mediator.Send(new GetStudentEnrollmentsQuery(studentId));
        return Ok(enrollments);
    }
}
```

### Complete Program.cs

```csharp
// Program.cs

using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register MediatR — scans for all IRequestHandler and INotificationHandler
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
});

// Register all FluentValidation validators
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Register custom services
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

---

## 11. When to Use CQRS and MediatR

Use them when:

| Scenario | Why CQRS/MediatR Helps |
|----------|------------------------|
| **Medium to large applications** (10+ domain entities, 30+ API endpoints) | Keeps code organized as it grows |
| **Complex business logic** with many validation rules, side effects, workflows | Each command/query is isolated and testable |
| **Team projects** where multiple developers work on the same codebase | Features don't step on each other; each command/query is a separate file |
| **Different read/write performance needs** (heavy reads, occasional writes) | Read and write paths can be independently optimized |
| **Audit logging, validation, or other cross-cutting concerns** | Pipeline behaviors apply globally with zero boilerplate |
| **Event-driven features** (send email on registration, update cache on change) | Notifications let you add handlers without touching existing code |
| **Microservices or modular monolith** architectures | CQRS maps naturally to service boundaries |

---

## 12. When NOT to Use Them

| Scenario | Why Not |
|----------|---------|
| **Simple CRUD apps** (< 5 entities, straightforward operations) | Overhead isn't worth it. Plain controllers + services are fine. |
| **Very small teams or solo projects** with tight deadlines | Steeper learning curve. Adds boilerplate. |
| **Simple scripts or one-off utilities** | Complete overkill. |
| **Prototypes or proof-of-concept apps** | Keep it simple until you need the structure. |

**Rule of thumb:** If your service file is getting hard to navigate (100+ lines, many methods), or if you're finding it hard to test things in isolation, it's time to introduce CQRS. Start simple, add structure when you feel the pain.

---

## 13. Common Mistakes to Avoid

### Mistake 1: Fat Handlers

```csharp
// BAD — handler doing too much
public async Task<int> Handle(CreateUserCommand request, CancellationToken ct)
{
    // Validation logic (should be in a validator)
    if (string.IsNullOrEmpty(request.Email)) throw new Exception("Email required");

    // Business logic
    var user = new User { ... };
    _db.Users.Add(user);
    await _db.SaveChangesAsync(ct);

    // Emailing (should be a notification handler)
    await _emailService.SendWelcomeEmail(user.Email);

    // Caching (should be a behavior)
    _cache.Set($"user_{user.Id}", user);

    return user.Id;
}
```

```csharp
// GOOD — handler only does core business logic
public async Task<int> Handle(CreateUserCommand request, CancellationToken ct)
{
    var user = new User { Name = request.Name, Email = request.Email, ... };
    _db.Users.Add(user);
    await _db.SaveChangesAsync(ct);
    await _mediator.Publish(new UserRegisteredNotification(...), ct);
    return user.Id;
}
// Validation → ValidationBehavior
// Email → SendWelcomeEmailHandler (notification)
// Caching → CachingBehavior
```

---

### Mistake 2: Calling One Handler from Another

```csharp
// BAD — handlers should not call other commands directly
public async Task<Unit> Handle(DeleteCourseCommand request, CancellationToken ct)
{
    // Don't do this — tightly couples handlers
    await _mediator.Send(new RemoveAllEnrollmentsCommand(request.CourseId), ct);
    _db.Courses.Remove(course);
    await _db.SaveChangesAsync(ct);
    return Unit.Value;
}
```

```csharp
// GOOD — do the work directly or use domain logic
public async Task<Unit> Handle(DeleteCourseCommand request, CancellationToken ct)
{
    var course = await _db.Courses
        .Include(c => c.Enrollments)
        .FirstOrDefaultAsync(c => c.Id == request.CourseId, ct);

    // Remove enrollments directly as part of this operation
    _db.Enrollments.RemoveRange(course.Enrollments);
    _db.Courses.Remove(course);
    await _db.SaveChangesAsync(ct);
    return Unit.Value;
}
```

---

### Mistake 3: Using Commands for Reads

```csharp
// BAD — Command used to read data
public record GetUserCommand(int Id) : IRequest<User>; // Don't do this
```

Commands and Queries are named conventions that signal intent. Keep them separate.

---

### Mistake 4: Returning Domain Entities from Queries

```csharp
// BAD — returning the full entity exposes sensitive data and couples your API to your DB model
public record GetUserByIdQuery(int Id) : IRequest<User>; // User entity — bad
```

```csharp
// GOOD — return a DTO that only contains what the caller needs
public record GetUserByIdQuery(int Id) : IRequest<UserDto>; // Clean DTO — good
```

---

### Mistake 5: Not Using CancellationToken

```csharp
// BAD — ignoring cancellation
public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
{
    return await _db.Users.FindAsync(request.Id); // Missing cancellationToken
}
```

```csharp
// GOOD — always pass cancellationToken to async operations
public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
{
    return await _db.Users
        .Where(u => u.Id == request.Id)
        .Select(u => new UserDto(u.Id, u.Name, u.Email, u.CreatedAt))
        .FirstOrDefaultAsync(cancellationToken); // Always pass it
}
```

---

## 14. Summary and Mental Model

### The One-Line Summaries

- **CQRS** = Split reading (queries) and writing (commands) into separate code paths.
- **MediatR** = A message bus that routes commands/queries to their handlers and lets you add middleware (behaviors) around all of them.

### The Mental Model

Think of MediatR like a **post office**:

```
You write a letter (Command/Query)
    └── Drop it in the mailbox (mediator.Send)
            └── Post office (MediatR) reads the address
                    └── Delivers to the right person (Handler)
                            └── They do the work and send a reply

Pipeline Behaviors = Security scanner at the post office entrance
                     (every letter goes through it, regardless of destination)

Notifications = Putting a letter in multiple mailboxes at once —
                all recipients get a copy and respond independently
```

### Quick Reference

```
Want to CREATE/UPDATE/DELETE something?
    → Create a record implementing IRequest<TResponse>
    → Name it XxxCommand
    → Create a class implementing IRequestHandler<XxxCommand, TResponse>

Want to READ something?
    → Create a record implementing IRequest<TResponse>
    → Name it XxxQuery
    → Create a class implementing IRequestHandler<XxxQuery, TResponse>
    → Return a DTO, never the raw entity

Want something to happen AFTER an event?
    → Create a record implementing INotification
    → Create handler(s) implementing INotificationHandler<YourNotification>
    → Publish with _mediator.Publish(...)

Want to run code AROUND every handler?
    → Create a class implementing IPipelineBehavior<TRequest, TResponse>
    → Register it in Program.cs
```

### Cheat Sheet — Commands vs. Queries

| | Command | Query |
|---|---------|-------|
| **Purpose** | Change state | Read state |
| **Returns** | `Unit` or minimal result | DTO or list |
| **Side effects** | Yes (save, send, update) | Never |
| **Cacheable** | No | Yes |
| **Example** | `CreateCourseCommand` | `GetCourseByIdQuery` |
| **Handler base** | `IRequestHandler<Command, Unit>` | `IRequestHandler<Query, CourseDto>` |

---

*This guide is part of the DotnetLearning project. For further reading, explore Microsoft's eShopOnContainers reference architecture which uses CQRS and MediatR extensively.*
