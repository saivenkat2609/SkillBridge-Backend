# SkillBridge — Interview Questions & Ideal Answers

---

## Project & Architecture

### Q: Walk me through your project architecture.

SkillBridge is a REST API backend built with ASP.NET Core 8, consumed by a separate React frontend. The backend handles user auth, skill listings, bookings, real-time chat, and email notifications. It follows a layered approach — controllers handle HTTP, services handle business logic, EF Core handles data access. The frontend and backend are on different origins so CORS is configured with credentials support.

---

### Q: Why did you separate the frontend and backend instead of building a monolith (like MVC with Razor)?

A separate frontend/backend gives clear separation of concerns — the API can serve mobile apps or third-party clients in future, not just the React app. It also lets frontend and backend teams (or in this case, different tech stacks) evolve independently. The tradeoff is CORS complexity and cookie handling across origins, which requires extra configuration.

---

### Q: What is your overall database design and how did you model relationships?

The core entities are `ApplicationUser`, `Skill`, `Booking`, `TeacherProfile`, `TeacherAvailability`, `AvailabilityException`, `Message`, and `Review`. Key relationships:
- User has an optional `TeacherProfile` (1-to-1)
- `Booking` links a Student (User), a Teacher (User), and a Skill — two FK references to the same Users table, so `OnDelete(NoAction)` is set to avoid SQL Server's multiple cascade paths error
- `Message` similarly has two FKs to Users (Sender, Receiver) — same reason for NoAction
- `TeacherAvailability` stores recurring weekly slots, `AvailabilityException` handles date-specific overrides

---

### Q: Why did you use Code-First EF Core instead of Database-First?

Code-First keeps everything in source control — the C# models and migration files describe the full DB schema history. Database-First would require reverse-engineering models from an existing DB, which is harder to version and collaborate on. With Code-First, schema changes are tracked as migrations alongside the code that uses them.

---

### Q: How do migrations run in production — do you run them manually?

No. On startup, `db.Database.Migrate()` is called automatically inside a try/catch. This applies any pending migrations before the app starts serving requests. The try/catch ensures a migration failure doesn't crash the whole app — it logs the error and continues, which is useful when the app can still serve some requests even if the DB is partially unreachable.

---

## Authentication & Authorization

### Q: Why did you use cookie-based auth instead of JWT tokens?

Cookie-based auth with ASP.NET Identity is simpler for a web app — the browser handles the cookie automatically on every request. JWTs require the frontend to store the token (localStorage or memory) and attach it manually to every request. Cookies also benefit from HttpOnly (not accessible to JavaScript, safer against XSS) and can be invalidated server-side. The tradeoff is CORS complexity when frontend and backend are on different origins.

---

### Q: How did you handle the cross-origin cookie issue between your React frontend and the API?

Three things were needed:
1. `SameSite=None` and `Secure=true` on the cookie — required for browsers to send cookies cross-origin
2. `AllowCredentials()` in the CORS policy — without this, the browser strips cookies from cross-origin requests
3. A custom middleware that appends `; Partitioned` to the Identity cookie — this is the Chrome CHIPS requirement for third-party cookies to be accepted in newer browser versions

---

### Q: How does your Google OAuth flow work?

The React frontend handles the Google sign-in popup and receives a Google ID token. It sends that token to our `/api/auth/google` endpoint. The backend validates the token server-side using `GoogleJsonWebSignature.ValidateAsync` — this verifies Google's cryptographic signature and checks the audience matches our ClientId. Once valid, we extract the user's email and name from the payload, create or find the user in our DB, and issue our own session cookie. We never trust the frontend's claim of who the user is.

---

### Q: Why validate the Google token on the backend and not just trust what the frontend sends?

The frontend is always untrusted — anyone can craft a request to your API with fake user data. Validating the Google token on the backend against Google's public keys ensures the token was genuinely issued by Google for our specific app. If we skipped this, an attacker could send `{ "email": "admin@example.com" }` and gain access as that user.

---

### Q: How do you handle the onboarding flow for new users?

New users — whether from email registration or Google login — get `IsOnboardingComplete = false`. The frontend detects this from the login response and redirects to an onboarding screen where the user picks a role (Student or Teacher). The `PATCH /api/auth/role` endpoint removes existing roles, assigns the chosen one, sets `IsOnboardingComplete = true`, and calls `RefreshSignInAsync` to update the cookie immediately so the new role is reflected without re-login.

---

### Q: Why do you have three roles — Student, Teacher, and User — but only use two?

The `User` role was created early as a generic role but was superseded by the more specific `Student` and `Teacher` roles during onboarding design. All new registrations default to `Student`. Ideally the `User` role would be removed to avoid confusion, but it was left in as a harmless leftover from an earlier design iteration.

---

### Q: How does your email confirmation flow work?

On registration, a signed token is generated using Identity's `GenerateEmailConfirmationTokenAsync`. It's Base64Url encoded to be URL-safe, then embedded into a link pointing to the React frontend. The frontend calls our `GET /api/auth/confirm-email?userId=&token=` endpoint, which decodes the token and calls `ConfirmEmailAsync`. Login is blocked for unconfirmed users. For password reset, the same pattern applies with `GeneratePasswordResetTokenAsync`.

---

### Q: The forgot-password endpoint returns the same message whether or not the email exists — is that intentional?

Yes, it's a security practice to prevent **user enumeration**. If we returned "Email not found" for unknown emails and "Reset link sent" for known ones, an attacker could probe your API to discover which emails are registered. Always returning the same response ("If that email exists, we sent a link") prevents that information leak.

---

## Booking System

### Q: Walk me through the booking creation flow.

1. Student sends `POST /api/bookings` with skillId, scheduledAt, and duration
2. We fetch the skill to get the teacher and price
3. `BookingValidator` checks business rules — must be 24hrs in future, student can't be the teacher, skill must be active
4. We check the DB for conflicts — any non-cancelled booking for the same teacher at the same time
5. If all pass, booking is saved with `Pending` status
6. Confirmation emails are sent to both student and teacher asynchronously (fire-and-forget)

---

### Q: Why did you put booking validation in a separate `BookingValidator` class instead of inside the controller?

Controllers should only deal with HTTP concerns — parsing the request and returning a response. Business rules like "must be 24hrs in advance" or "can't self-book" are domain logic that belong in a service. This keeps the controller thin, makes the validation reusable from other places (e.g., a future admin endpoint), and makes it independently testable without needing an HTTP context.

---

### Q: Your conflict check uses `AnyAsync` before saving — does this have a race condition?

Yes, technically it does. Two concurrent requests could both pass the `AnyAsync` check before either commits, resulting in a double booking. The proper fix is a **unique constraint at the database level** on `(TeacherId, ScheduledAt)`. Then if a race condition occurs, the second insert fails with a DB exception which you catch and return as a 409 Conflict. The application-level check is still useful as a fast, friendly error message — the DB constraint is the actual safety net.

---

### Q: Why are booking emails sent fire-and-forget (`_ = _emailService.SendAsync(...)`) instead of being awaited?

Email delivery shouldn't block the API response. SendGrid is a third-party service — if it's slow or temporarily down, the user would experience a slow or failed booking. By firing without awaiting, the booking is confirmed immediately and the email sends in the background. The tradeoff is that if the email fails, the user won't know — for production you'd use a message queue (like Azure Service Bus) with retry logic instead.

---

### Q: How did you calculate the booking price?

`TotalPrice = PricePerHour × (DurationMinutes / 60)`. Price is stored per hour and duration is in minutes, so the fraction handles partial-hour sessions. The result is stored as `decimal(18,2)` in the DB to avoid floating-point precision issues with money values.

---

## Teacher Availability

### Q: How does the availability system work?

Two-layer model:
1. **Regular availability** — `TeacherAvailability` stores recurring weekly slots per day of week (e.g., every Monday 9am–5pm)
2. **Exceptions** — `AvailabilityException` stores date-specific overrides with two types:
   - `Block` — removes slots on a specific date (e.g., teacher is sick on Monday)
   - `Extra` — adds slots on a date outside normal schedule

The endpoint generates a full month of slots by iterating each day, applying the recurring schedule, then applying any exceptions on top.

---

### Q: Why did you model recurring availability separately from exceptions instead of just storing every individual slot?

Storing every individual slot would be impractical — a teacher available 5 days a week for a year would need hundreds of records. Storing a recurring pattern (7 rows max, one per active day) is compact and readable. Exceptions are relatively rare events so storing them individually makes sense. The slot generation logic runs at query time to materialize the actual available times.

---

### Q: How do you handle the case where a teacher wants to set availability for a day they're not regularly available?

That's what `AvailabilityException` with type `Extra` handles. If a teacher normally doesn't work Sundays but wants to offer slots on a specific Sunday, they create an `Extra` exception for that date. The slot generation code applies exceptions after the regular schedule — Extra adds slots, Block removes them.

---

## Real-Time Chat (SignalR)

### Q: Why did you use SignalR instead of polling for the chat feature?

Polling means the frontend repeatedly calls the server every few seconds to check for new messages — wasteful and adds latency. SignalR uses WebSockets (with fallbacks) so the server can push messages to clients the instant they arrive. This gives true real-time feel and is far more efficient — no unnecessary requests when there's nothing new.

---

### Q: How did you handle a user being connected from multiple browser tabs?

Each tab gets a unique `connectionId` from SignalR. I maintain a `ConcurrentDictionary<userId, ConcurrentBag<connectionId>>` — a map from each user to all their active connection IDs. When sending a message to a user, I iterate all their connection IDs and send to each. `OnConnectedAsync` adds the connection, `OnDisconnectedAsync` removes it. `ConcurrentDictionary` and `ConcurrentBag` are used because SignalR hub methods can be called concurrently from multiple threads.

---

### Q: Messages are saved to the DB in the SignalR hub — is that a good practice?

It works, but it's not ideal. The hub handles real-time transport concerns and also does DB persistence — that's two responsibilities. A cleaner approach would be to have the hub call a `MessageService` that handles persistence, keeping the hub focused on just routing messages to connected clients. For this project the simplicity was acceptable, but in production you'd separate those concerns.

---

### Q: What happens to messages sent when the recipient is offline?

The message is still saved to the DB via the `SendMessage` hub method. When the recipient comes online, they call `GET /api/messages/{userId}` to load message history from the DB — they'll see all messages sent while offline. The `IsRead` flag tracks unread messages and the frontend can show an unread count via `GET /api/messages/unread-count`.

---

### Q: How would you scale the SignalR chat to multiple servers?

The current `ConcurrentDictionary` lives in memory on one server. If you add a second server, connections on Server A can't be reached from Server B. The fix is a **Redis backplane** (`AddStackExchangeRedis`) — all servers publish and subscribe through Redis, so any server can push to any connection. Azure SignalR Service is another option that fully offloads connection management.

---

## Skills & File Upload

### Q: How did you handle image uploads for skills?

File uploads use `[FromForm]` with `IFormFile` because JSON can't carry binary data. Validation checks the file extension against a whitelist (jpg, png) and enforces a 5MB size limit. The file is saved to `wwwroot/images/skills/` with a **GUID as the filename** — this prevents collisions and avoids path traversal attacks from user-supplied filenames. The relative URL is stored in the DB and served as a static file.

---

### Q: Why use a GUID for the saved filename instead of the original filename?

Two reasons:
1. **Security** — a user could upload a file named `../../appsettings.json` and overwrite a config file (path traversal attack). A server-generated GUID is always safe.
2. **Collision prevention** — two users uploading `profile.jpg` would overwrite each other's file. GUIDs are globally unique.

---

### Q: For production, would you store files in `wwwroot` on the server?

No. Local disk storage doesn't scale — if you have multiple API servers, only the one that received the upload has the file. If the server is redeployed or crashes, files are lost. Production approach is cloud storage like Azure Blob Storage or AWS S3 — files are stored externally, accessible from any server, durable, and can be served via CDN for better performance.

---

## Email (SendGrid)

### Q: Why use an interface (`IEmailService`) instead of injecting `SendGridEmailService` directly?

Two benefits:
1. **Testability** — in unit tests you inject a mock `IEmailService` that does nothing instead of actually sending emails during tests.
2. **Swappability** — if you switch from SendGrid to another provider (Mailgun, AWS SES), you write a new implementation and change one line in `Program.cs`. Every controller and service that depends on `IEmailService` stays unchanged.

---

### Q: How are email templates managed?

HTML email templates are in a static `EmailTemplates` utility class as methods that return HTML strings. Each method takes the relevant data (name, link, booking details) and returns a formatted HTML body. This keeps template logic out of controllers and in one maintainable place. For a larger project, you'd use a templating engine like Scriban or load templates from files to avoid HTML strings in C# code.

---

## General Design Decisions

### Q: How did you handle the many-to-many relationship between Users and Bookings (a user can be both a teacher and a student)?

`Booking` has two separate FKs — `StudentId` and `TeacherId` — both referencing the same `ApplicationUser` table. EF Core needs `OnDelete(NoAction)` on both to avoid a "multiple cascade paths" SQL Server error. The application handles which user is which by role context — when creating a booking, the logged-in user is always the student and the teacher comes from the skill.

---

### Q: If you were to rebuild this, what would you do differently?

- Add a **repository/service layer** between controllers and DbContext for better testability and separation
- Use a **message queue** (Azure Service Bus, RabbitMQ) for email sending instead of fire-and-forget, for reliability and retry support
- Add a **unique DB constraint** on `(TeacherId, ScheduledAt)` to make booking conflict detection race-condition safe
- Store images in **cloud storage** (Azure Blob) instead of local disk
- Add **proper unit and integration tests** from the start
- Add **rate limiting** on auth endpoints to prevent brute-force attacks

---

### Q: How do you handle authorization — what if a student tries to access a teacher-only endpoint?

`[Authorize(Roles = "Teacher")]` on those endpoints. If a `Student` calls them, ASP.NET Core returns `403 Forbidden`. The role is stored as a claim in the Identity cookie — the framework checks it automatically without any manual code in the controller. The cookie is updated when the user changes roles via `RefreshSignInAsync`.

---

### Q: How does your API communicate errors to the frontend?

- `400 Bad Request` — validation failures, with an error message or list in the body
- `401 Unauthorized` — not logged in (the default redirect-to-login is overridden to return 401 for API clients)
- `403 Forbidden` — logged in but wrong role
- `404 Not Found` — resource doesn't exist
- `200 OK` — success with data

The cookie 401 override (`OnRedirectToLogin`) is important — without it, ASP.NET would return a 302 redirect to `/Account/Login` which makes no sense for an API client.

---
