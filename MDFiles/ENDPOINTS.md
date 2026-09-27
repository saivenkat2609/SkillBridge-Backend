# SkillBridge API Endpoints

## Authentication — `/api/auth`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/auth/register` | No | Register a new user with first name, last name, email, and password. Assigns the **Student** role by default. Sends a confirmation email with a verification link. |
| GET | `/api/auth/confirm-email` | No | Confirms a user's email address. Accepts `userId` and `token` as query params (token is Base64Url-encoded). |
| POST | `/api/auth/login` | No | Login with email and password. Returns user id, email, full name, role, and onboarding status. Rejects unconfirmed emails. |
| POST | `/api/auth/logout` | No | Logs the current user out (clears the session/cookie). |
| POST | `/api/auth/google` | No | Google OAuth login. Validates a Google ID token, creates a new account if the email doesn't exist, or links Google login to an existing account. Returns user info and a flag indicating if the user is new. |
| POST | `/api/auth/forgot-password` | No | Sends a password reset link to the given email if a confirmed account exists. Always returns a generic success message to prevent email enumeration. |
| POST | `/api/auth/reset-password` | No | Resets a user's password using a userId, a Base64Url-encoded reset token, and a new password. |
| GET | `/api/auth/me` | Yes | Returns the currently authenticated user's id, email, full name, role, and onboarding status. |
| PATCH | `/api/auth/role` | Yes | Updates the authenticated user's role to either `Student` or `Teacher`. Also marks onboarding as complete and refreshes the session so the new role takes effect immediately. |

---

## Skills — `/api/skills`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| GET | `/api/skills` | No | Returns a paginated list of skills. Supports query filters: `searchTerm`, `categoryId`, `minPrice`, `maxPrice`, `sortBy` (`price_asc`, `price_desc`, `rating`, default is newest), `page`, `pageSize`. Returns total count alongside the results. |
| GET | `/api/skills/{id}` | No | Returns a single skill by its ID, including its category. Returns 404 if not found. |
| POST | `/api/skills` | Yes (Teacher) | Creates a new skill listing. Accepts `multipart/form-data` with title, description, price per hour, category ID, and an optional image (jpg/png, max 5MB). Saves the image to local storage and stores the URL. |

---

## Categories — `/api/categories`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| GET | `/api/categories` | No | Returns a list of all skill categories. |

---

## Teachers — `/api/teachers`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| GET | `/api/teachers/{teacherId}/profile` | Yes | Returns the public profile of a teacher: name, bio, hourly rate, active skills (with categories), average rating, and total review count. |
| POST | `/api/teachers/profile` | Yes (Teacher) | Creates or updates the authenticated teacher's profile (bio and hourly rate). Upserts — updates if a profile exists, creates one if not. |
| GET | `/api/teachers/{teacherId}/availability` | Yes | Returns a teacher's available time slots for the rest of the month starting from `fromDate`. Accepts `durationInMinutes` to split the day into appropriately-sized slots. Applies weekly recurring availability, exception overrides (blocks or extra slots), and marks already-booked slots. |
| POST | `/api/teachers/availability` | Yes (Teacher) | Replaces the authenticated teacher's entire weekly availability schedule. Accepts a list of entries each with day of week, start time, and end time. |
| GET | `/api/teachers/stats` | Yes (Teacher) | Returns the authenticated teacher's dashboard summary: their skill listings, upcoming bookings, completed bookings, total earnings, and average rating across all skills. |

---

## Bookings — `/api/bookings`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/bookings` | Yes | Creates a new booking for a skill. Validates the skill exists, there are no time conflicts, and the booking passes business rules. Calculates total price from duration and skill hourly rate. Sends confirmation emails to both the student and the teacher. |
| GET | `/api/bookings/my` | Yes | Returns all bookings made by the authenticated student, including skill title, teacher name, scheduled time, duration, total price, and status. |

---

## Reviews — `/api/reviews`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/reviews` | Yes (implicit) | Submits a review for a completed booking. Validates: booking must be completed, reviewer must be the student who made the booking, rating must be 1–5, content must be 10–500 characters, and only one review per booking is allowed. After saving, recalculates and updates the skill's aggregate rating and review count. |
| GET | `/api/reviews?skillId={skillId}` | No | Returns all reviews for a given skill. |

---

## Messages — `/api/messages`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| GET | `/api/messages/conversations` | Yes | Returns a summary of all conversations for the authenticated user, grouped by the other participant. Each entry includes the other user's name, last message content, timestamp, and unread message count. |
| GET | `/api/messages/{otherUserId}` | Yes | Returns the full message history between the authenticated user and the specified other user, sorted oldest to newest. |
| GET | `/api/messages/unread-count` | Yes | Returns the total count of unread messages received by the authenticated user. |
| PUT | `/api/messages/mark-as-read/{userId}` | Yes | Marks all unread messages from the specified sender as read (sets `isRead = true` and records `readAt` timestamp). |

---

## Real-Time Chat — SignalR Hub

**Hub URL:** `/chathub`  
Requires authentication. Uses cookie-based identity.

| Event / Method | Direction | Description |
|----------------|-----------|-------------|
| `SendMessage(receiverId, message)` | Client → Server | Sends a chat message to another user. Saves the message to the database and pushes it in real time to all active connections of both the sender and the receiver via the `ReceiveMessage` event. |
| `ReceiveMessage` | Server → Client | Pushed to the client when a new message is sent or received. Payload is the full message object. |
| `OnConnectedAsync` | Lifecycle | Tracks the user's connection ID mapped to their user ID (supports multiple tabs/devices). |
| `OnDisconnectedAsync` | Lifecycle | Removes the disconnected connection ID from the user's tracked connections. Cleans up the entry entirely if no connections remain. |

---

## Seed (Dev/Admin Only) — `/api/seed`

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/seed` | No | Seeds the database with initial demo data. Skips records that already exist. |
| POST | `/api/seed/reset` | No | Clears bookings and reviews, then re-seeds all demo data from scratch. |
