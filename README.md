# HealthTrack

HealthTrack is a personal health and fitness management web application, built for CS6004ES Coursework 2. Clients follow
workout and diet plans, log their sessions, meals and measurements, watch their progress on charts and book appointments
with professionals. Trainers and health professionals plan for their assigned clients, follow their progress, manage their
availability and give feedback. Administrators approve professionals, assign clients, keep the exercise and food libraries,
run reports and read the audit log.

## Features

| Role | Highlights |
| --- | --- |
| Client | Dashboard (weight, BMI, today's workout and meals, next appointments, feedback); workout and meal logging with calories and nutrition worked out automatically; measurements and progress charts; appointment booking in free slots; reminders and notifications with optional email copies; download my data, delete my account |
| Trainer | Dashboard with each client's last activity, 7-day adherence and inactive flags; workout plans and reusable routine templates; diet plans; client progress; feedback; weekly availability; appointment requests (approve, reject, reschedule, complete); three reports with CSV export and print |
| Administrator | Dashboard with users, approvals, workload and charts; trainer approvals; client–trainer assignments; user management; exercise and food libraries; reports including trainer workload; audit log |

Security: ASP.NET Core Identity with PBKDF2 password hashing, lockout after 5 failed logins, role areas and per-record
ownership checks, anti-forgery tokens, HTTPS with HSTS, a strict Content-Security-Policy, encryption at rest for the
sensitive health text, an audit trail and privacy features. See `docs/security-review.md`.

## Technology

- ASP.NET Core MVC on .NET 10 (C#, Razor views), with areas per role
- Entity Framework Core 10 on SQL Server Express LocalDB (database `HealthTrackDb`)
- ASP.NET Core Identity with roles; ASP.NET Core Data Protection for field encryption
- Bootstrap 5, jQuery Validation, Chart.js 4 (all served locally from `wwwroot/lib`)
- MSTest with the EF Core InMemory provider for the unit tests

## Running it

You need Windows with Visual Studio 2026 and the **ASP.NET and web development** workload (it brings the .NET 10 SDK and
LocalDB).

1. Open `HealthTrack.slnx`.
2. Press **F5** (profile **https**) and trust the development certificate when asked.
3. The first start creates the database, applies the migrations and seeds the demo data; then the browser opens
   `https://localhost:7144`.

From the command line: `dotnet tool restore`, then `dotnet run --project HealthTrack --launch-profile https`.

The full steps, resetting the database and troubleshooting are in [docs/installation-guide.md](docs/installation-guide.md).

## Demo accounts

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Administrator | admin@healthtrack.test | Admin@123 | |
| Trainer | trainer@healthtrack.test | Trainer@123 | Fitness trainer; coaches client@, client2@ and client3@ |
| Trainer | nutritionist@healthtrack.test | Trainer@123 | Nutritionist; coaches client4@ |
| Trainer | physio@healthtrack.test | Trainer@123 | Physiotherapist waiting for approval |
| Client | client@healthtrack.test | Client@123 | Main demo client with 8 weeks of history |
| Client | client2@ … client6@healthtrack.test | Client@123 | client5@ and client6@ start unassigned |

## Project structure

```
HealthTrack.slnx
HealthTrack/                 the web application
  Program.cs                 services, Identity and security settings, request pipeline, start-up migration and seeding
  Areas/Client|Trainer|Admin one area per role: controllers (each area's base controller checks the role) and views
  Controllers/               shared pages: home, account, manage account, notifications, errors
  Models/Entities|Enums|ViewModels
  Data/                      ApplicationDbContext, EF Core migrations, demo data seeder
  Services/                  the business logic: one interface and class per service, plus the reminder background service
  Security/                  roles, encryption, validation attributes, security headers
  Helpers/                   health calculations, CSV export, clock, paging
  Views/                     shared layout, partials, report and error pages
  wwwroot/                   CSS, page scripts and the client libraries
HealthTrack.Tests/           MSTest unit tests (services, entities, calculations, validation, security)
docs/                        design spec, build prompts, installation guide, user manual, manual tests, security review
```

The design (business rules BR-01 to BR-22, the ERD, use cases UC01–UC40 and class responsibilities) is in
[docs/design-spec.md](docs/design-spec.md).

## Tests

```
dotnet test
```

or **Test → Run All Tests** in Visual Studio. The 551 tests cover the health formulas, entity rules, every service
(including ownership, the booking race, reminders, reports, privacy and the demo data), validation and the security
middleware. They use an in-memory database, so LocalDB isn't needed. The manual test plan, one case per use case plus the
security checks, is [docs/manual-tests.md](docs/manual-tests.md).

## Documentation

- [Installation guide](docs/installation-guide.md)
- [User manual](docs/user-manual.md)
- [Manual tests](docs/manual-tests.md)
- [Security review](docs/security-review.md)
- [Design specification](docs/design-spec.md)
