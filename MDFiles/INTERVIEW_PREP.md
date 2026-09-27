# SkillBridge — Interview Prep Guide

## What is this project?

**SkillBridge** is a marketplace platform where **teachers offer skills** and **students book sessions**. Think of it like an Airbnb for tutoring/skill-sharing.

- **Backend:** ASP.NET Core 8 Web API (REST)
- **Database:** SQL Server with Entity Framework Core (Code-First)
- **Auth:** ASP.NET Core Identity + Google OAuth
- **Real-time:** SignalR (live chat)
- **Email:** SendGrid
- **Architecture:** MVC-style API with Services layer

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core (Code-First) |
| Auth | ASP.NET Identity + Google OAuth (session via cookies) |
| Real-time | SignalR (WebSockets) |
| Email | SendGrid |
| Database | SQL Server |
| Frontend | Separate React app (cross-origin, CORS configured) |

---

## Key Features

### 1. Authentication System

- **Email registration** → sends confirmation email via SendGrid → user must confirm before login
- **Google OAuth** — validates Google ID token server-side using `GoogleJsonWebSignature`, creates user if new, auto-confirms email
- **Roles:** `Student`, `Teacher` — assigned during onboarding
- **Onboarding flow:** New users get `IsOnboardingComplete = false`, frontend redirects them to choose a role
- **Forgot/Reset Password** — token generated, Base64Url encoded, sent via email link

### 2. Role-Based Authorization

- `[Authorize(Roles = "Teacher")]` — only teachers can create skills, set availability, view their stats
- `[Authorize]` — any logged-in user can browse and book
- After choosing a role in onboarding, old roles are removed and new one added, then `RefreshSignInAsync` updates the cookie immediately without requiring re-login

### 3. Skills & Categories

- Teachers create skills with title, description, price per hour, category, and optional image upload (jpg/png, max 5MB)
- Students can filter/search skills by: search term, category, min/max price, sort by price or rating — with **pagination**
- Image files stored in `wwwroot/images/skills/`, named with a **GUID** to avoid filename conflicts

### 4. Booking System

- Students book a skill → price calculated as `PricePerHour × (durationMinutes / 60)`
- **BookingValidator** (service class) enforces business rules:
  - Must be at least 24 hours in the future
  - Teacher cannot book their own skill
  - Skill must be active
- **Conflict check** in DB — `AnyAsync` query ensures no two non-cancelled bookings for same teacher at same time
- On booking: confirmation email sent to both student and teacher via SendGrid (fire-and-forget with `_ =`)

### 5. Teacher Availability

- Teachers set **recurring weekly availability** (day of week + start/end time)
- **AvailabilityException** model handles date-specific overrides:
  - `Block` — removes slots for that date
  - `Extra` — adds extra slots for that date
- `getTeacherAvailability` endpoint generates time slots for a full month, marks each slot as booked or available

### 6. Real-Time Chat (SignalR)

- `ChatHub` is `[Authorize]` — only authenticated users can connect
- Uses `ConcurrentDictionary<userId, ConcurrentBag<connectionId>>` to support **multiple tabs/devices** per user
- `SendMessage` → saves to DB → pushes to both sender and receiver in real-time
- `OnConnected` / `OnDisconnected` manage the connection registry safely across threads
- REST API handles: conversation list (grouped by other user), message history, unread count, mark-as-read

### 7. CORS & Cookie Setup

- Frontend is a separate React app (different origin) → CORS policy `AllowFrontend` configured with `AllowCredentials()`
- Cookie set to `SameSite=None; Secure` so it works cross-origin
- Added **CHIPS (Partitioned cookie)** via custom middleware — appends `; Partitioned` to the Identity cookie for Chrome cross-origin support

---

## Database Models

```
ApplicationUser (extends IdentityUser)
  ├── FirstName, LastName
  ├── IsOnboardingComplete
  └── TeacherProfile (optional, 1-to-1)

TeacherProfile
  ├── Bio, HourlyRate
  └── ApplicationUserId (FK)

Skill
  ├── Title, Description, PricePerHour, ImageUrl
  ├── TeacherId, TeacherName
  ├── CategoryId, Rating, NumberOfReviews, IsActive
  └── CreatedAt

Booking
  ├── StudentId, TeacherId, SkillId
  ├── ScheduledAt, DurationMinutes, TotalPrice
  ├── Status (Pending / Confirmed / Cancelled)
  └── CreatedAt

TeacherAvailability  — recurring weekly slots (DayOfWeek, StartTime, EndTime)
AvailabilityException — date-specific overrides (Block or Extra)
Message              — SenderId, ReceiverId, Content, SentAt, IsRead, ReadAt
Review               — ReviewerId, TeacherId, SkillId, Rating, Comment
```

---

## Common Interview Questions & Answers

### Q: How does Google login work?

Frontend gets a Google ID token and sends it to `/api/auth/google`. The backend validates it using `GoogleJsonWebSignature.ValidateAsync` against the configured Google ClientId. If the user doesn't exist, we create one with the email already confirmed (Google already verified it). Then we sign them in with `SignInAsync` and return user info including role and onboarding status.

---

### Q: How do you prevent double bookings?

After the `BookingValidator` passes local rules, we do a DB-level check using `AnyAsync` — checking if any **non-cancelled** booking already exists for the same teacher at the same `ScheduledAt` time. If yes, return `400 Bad Request` with a conflict message.

---

### Q: Why `ConcurrentDictionary` and `ConcurrentBag` in ChatHub?

SignalR hubs are instantiated per connection and methods can be called from multiple threads simultaneously. A single user can also have **multiple browser tabs open**, meaning multiple `connectionId`s per user. `ConcurrentDictionary` gives thread-safe access to the userId→connections map. `ConcurrentBag` is a thread-safe collection for storing multiple connection IDs per user.

---

### Q: How does the teacher availability system work?

Teachers set recurring availability per day of week (e.g., Monday 9am–5pm). The `getTeacherAvailability` endpoint iterates every day in the requested month, finds the matching weekly schedule, and splits it into time slots based on the session duration requested. `AvailabilityException` records are applied after — `Block` removes slots for a specific date, `Extra` adds extra slots. Booked slots are flagged by cross-referencing existing bookings.

---

### Q: What are the service lifetimes used?

| Service | Lifetime | Reason |
|---------|----------|--------|
| `BookingValidator` | `Scoped` | Created once per HTTP request |
| `IEmailService` (SendGrid) | `Transient` | New instance each injection — stateless HTTP client |
| `AppDbContext` | `Scoped` | EF Core DbContext should be scoped per request |
| `SignalR` | Singleton-managed | Hub instances are transient but connection state is in static `ConcurrentDictionary` |

---

### Q: How do you handle the onboarding flow?

`IsOnboardingComplete = false` is set on all new users. The `PATCH /api/auth/role` endpoint lets users pick Student or Teacher. It removes all existing roles, assigns the new one, sets `IsOnboardingComplete = true`, and calls `RefreshSignInAsync` so the cookie reflects the updated role without requiring a re-login.

---

### Q: How do migrations run in production?

Using Code-First EF Core. On every app startup, `db.Database.Migrate()` is called automatically inside a try/catch — it applies any pending migrations. Roles (Teacher, Student, User) are also seeded at startup using `RoleManager` if they don't already exist.

---

### Q: How is email confirmation handled?

Token is generated with `GenerateEmailConfirmationTokenAsync`, then encoded with `WebEncoders.Base64UrlEncode` to make it URL-safe. The link points to the React frontend (`/confirm-email?userId=&token=`). Frontend calls `/api/auth/confirm-email` which decodes the token with `WebEncoders.Base64UrlDecode` and calls `ConfirmEmailAsync`. Login is blocked for unconfirmed emails.

---

### Q: How is file upload handled for skill images?

The endpoint accepts `[FromForm]` with `IFormFile`. Validation checks extension (jpg/png only) and file size (max 5MB). The file is saved to `wwwroot/images/skills/{guid}{extension}` — using a GUID prevents filename collisions. The relative URL `/images/skills/{guid}{extension}` is stored in the DB and served as a static file.

---

### Q: How does the `[Authorize]` attribute work with cookies?

ASP.NET Core Identity sets a cookie on login via `SignInAsync`. The middleware pipeline reads that cookie on every request (`UseAuthentication`), populates `HttpContext.User`, and `UseAuthorization` then checks `[Authorize]` attributes. For 401 responses, the default redirect-to-login is overridden to return a `401` status code directly (needed for API, not browser).

---

## What I Would Improve

- Add JWT tokens as an alternative to cookies (better for mobile/native clients)
- Add unit tests for `BookingValidator` and the availability slot logic
- Add pagination to bookings list and message history endpoints
- Teacher stats endpoint hits the DB on every call — could benefit from caching
- SignalR connection dictionary is in-memory — a Redis backplane would be needed for multi-server (horizontal scaling) deployments
- Add a review system endpoint (model exists but no controller yet)
