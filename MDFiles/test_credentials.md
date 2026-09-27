# Test Credentials

## Teachers

| Name  | Email                | Password   | Role    |
|-------|----------------------|------------|---------|
| Alice | alice@skillswap.com  | Test@1234  | Teacher |
| Bob   | bob@skillswap.com    | Test@1234  | Teacher |

## Students

| Name    | Email                   | Password   | Role    |
|---------|-------------------------|------------|---------|
| Charlie | charlie@skillswap.com   | Test@1234  | Student |
| Diana   | diana@skillswap.com     | Test@1234  | Student |

---

## What to test

### As Charlie (Student)
- Upcoming: React (Confirmed), Python (Pending), Financial Modelling (Confirmed)
- Completed: React, ASP.NET Core, Public Speaking
- Cancelled: Figma

### As Alice (Teacher)
- Upcoming: React with Charlie, Figma with Diana
- Completed: React, ASP.NET Core, Public Speaking (Charlie), Figma (Diana)
- Cancelled: Figma (Charlie), SEO (Diana)
- Reviews: 3 reviews from Charlie and Diana

### Seed endpoints
- `POST /api/seed` — seed all data (skips existing)
- `POST /api/seed/reset` — clear bookings + reviews and re-seed fresh
