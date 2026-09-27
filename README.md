# SkillBridge — Backend

REST API and real-time hub for SkillBridge, a marketplace where students book one-on-one sessions with teachers. The frontend lives [here](https://github.com/saivenkat2609/SkillBridge-UI).

## What it does

- Auth — registration, email confirmation, login, forgot/reset password, Google OAuth
- Skills — CRUD, category filtering, search, pagination, image upload
- Bookings — availability scheduling, slot booking, status lifecycle (Pending → Confirmed → Completed / Cancelled)
- Reviews — post-session ratings and written reviews
- Messaging — real-time 1-on-1 chat via SignalR
- Teacher dashboard — earnings, upcoming/completed bookings, average rating

## Tech

ASP.NET Core (.NET 8), Entity Framework Core, Azure SQL Database, ASP.NET Identity, SignalR, SendGrid, Google OAuth

---

## Running locally

**Prerequisites:** .NET 8 SDK, SQL Server or LocalDB

```bash
git clone https://github.com/saivenkat2609/SkillBridge-Backend.git
cd SkillBridge-Backend
```

Create `SkillBridge/appsettings.Development.json` (this file is gitignored):

```json
{
  "ConnectionStrings": {
    "sqlServer": "server=(localdb)\\MSSQLLocalDB;Database=SkillBridgeDb;Trusted_Connection=True"
  },
  "SendGrid": {
    "ApiKey": "your_sendgrid_api_key",
    "FromEmail": "you@yourdomain.com",
    "FromName": "SkillBridge"
  },
  "Frontend": {
    "Url": "http://localhost:5173"
  },
  "Google": {
    "ClientId": "your_google_client_id"
  },
  "AllowedOrigins": [
    "http://localhost:5173"
  ]
}
```

Run migrations to create the database:

```bash
cd SkillBridge
dotnet ef database update
```

Start the API:

```bash
dotnet run
```

API runs at `http://localhost:5211`. Swagger is available at `http://localhost:5211/swagger`.

## Seeding test data

Once running, call this endpoint to seed users, skills, availability, bookings and reviews:

```
POST http://localhost:5211/api/seed
```

To wipe and re-seed from scratch:

```
POST http://localhost:5211/api/seed/reset
```

Test accounts after seeding (password: `Test@1234`):

| Email | Role |
|---|---|
| alice@skillbridge.com | Teacher |
| bob@skillbridge.com | Teacher |
| charlie@skillbridge.com | Student |
| diana@skillbridge.com | Student |

## Email

Emails (confirmation, password reset, booking notifications) are sent via SendGrid. For local dev you can leave `ApiKey` empty — the app won't crash, emails just won't send. To test emails locally, get a free SendGrid API key and verify your sender email in their dashboard.

## Deployment

Deployed to Azure App Service via GitHub Actions on push to `master`. The workflow is in `.github/workflows/master_skillswap-api.yml`. Connection strings and secrets for production are set as Azure App Service environment variables — nothing sensitive is in the repo.
