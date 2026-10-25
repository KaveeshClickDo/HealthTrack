# HealthTrack – guide for Claude Code

HealthTrack is a coursework web application (CS6004ES Coursework 2): a personal health and fitness management system with three roles – Client, Trainer (fitness trainers and healthcare professionals) and Administrator.

The full design is in `docs/design-spec.md`: system description, business rules BR-01 to BR-22, the ERD (23 tables, relationships R1–R32, enums), use cases UC01–UC40 and class responsibilities. **It is the source of truth.** If the code has to differ from it, update the spec in the same change and tell me why. Diagrams are in `docs/diagrams/` (PNG to look at, SVG to read as text).

We build in phases with the prompts in `docs/build-prompts.md`. Only do the phase I give you.

## Tech stack (ask before changing anything here)

- ASP.NET Core MVC, C#, nullable reference types on. Target framework: net10.0 (the one Visual Studio created; keep it). Where the spec says .NET 8 or EF Core 8, read .NET 10 and EF Core 10.
- Entity Framework Core (SQL Server provider) on SQL Server Express LocalDB, database `HealthTrackDb`.
- ASP.NET Core Identity with roles and **our own MVC Account pages**: `AddIdentity<ApplicationUser, IdentityRole>()`, never `AddDefaultIdentity` or the Identity UI package.
- Razor views, Bootstrap 5, jQuery Validation (from the template), Chart.js 4 (4.5.1, `wwwroot/lib/chart.js/chart.umd.js`; no CDN at runtime).
- MSTest project `HealthTrack.Tests` using EF Core InMemory for service tests.
- Allowed NuGet packages: Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Tools, Microsoft.EntityFrameworkCore.InMemory (tests only), System.Security.Cryptography.Pkcs (pins the patched version of a vulnerable SqlClient dependency; see the comment in the .csproj), all at the framework's major version (10.0.x), and the MSTest package (versioned separately, 4.x). `dotnet-ef` is a local tool at the same 10.0.x version (`.config/dotnet-tools.json`). Ask before adding anything else.

## Solution layout

```
HealthTrack.slnx
CLAUDE.md
.gitignore
.config/dotnet-tools.json local tool manifest (dotnet-ef)
docs/                     design-spec.md, build-prompts.md, diagrams/
HealthTrack/              the web project (HealthTrack.csproj)
  Program.cs              DI registrations, Identity options, middleware pipeline, startup migration + seed
  appsettings.json        ConnectionStrings:DefaultConnection, Smtp, Reminders
  Areas/Admin|Trainer|Client/
    Controllers/          the area's abstract base controller + its controllers
    Views/                _ViewStart.cshtml, _ViewImports.cshtml (tag helpers!), one folder per controller
  Controllers/            BaseController, Home, Account, Manage, Notifications, Error
  Models/Entities/        ApplicationUser + 20 entity classes
  Models/Enums/           18 enums
  Models/ViewModels/      form, dashboard and report view models
  Data/                   ApplicationDbContext, Migrations/, DbSeeder
  Services/               I<Name>Service + <Name>Service pairs, ReminderBackgroundService
  Security/               EncryptionService, EncryptedStringConverter, MinimumAgeAttribute, NotInFutureAttribute, SecurityHeadersMiddleware
  Helpers/                HealthCalculator, CsvExporter, IClock + SystemClock, BusinessRuleException
  Views/Shared/           _Layout, _Alerts, error views
  wwwroot/                css/site.css, js/ (healthtrack.js, validators.js, one file per page script), lib/
  App_Data/               keys/ (Data Protection), mail/ (dev email pickup) – both git-ignored
HealthTrack.Tests/        MSTest project (references HealthTrack)
```

The web project's root namespace is `HealthTrack`, and namespaces follow folders: `HealthTrack.Models.Entities`, `HealthTrack.Areas.Client.Controllers`, and so on.

## Architecture rules

- **Thin controllers.** An action binds and validates a view model, calls one service method and returns a view, redirect or JSON. No EF queries in controllers.
- **Rules live in services and entity methods** (for example `Appointment.Approve()`), never in views. Each service has an interface, is registered with `AddScoped` in Program.cs and is injected through the constructor.
- **Async naming.** The spec lists method names without the suffix (`Book`, `GetUnread`); in code every async method ends in `Async` (`BookAsync`).
- **View models for every form.** Never bind entities from a request.
- **One area per role.** Each area has an abstract base controller with `[Area("Client")]` and `[Authorize(Roles = "Client")]` (likewise Trainer and Admin) that inherits `BaseController`; every controller in the area inherits it. Root controllers inherit `BaseController` directly. Links into an area use `asp-area`; links from an area to root pages use `asp-area=""`.
- **Ownership.** "My" data is always resolved from the signed-in user (`CurrentUserId` → ClientProfile/TrainerProfile), never from a form field or URL. Trainers reach a client only through an active TrainerAssignment (`IAssignmentService.IsTrainerAssignedToClientAsync`), except the details of their own appointments. Anything not allowed returns `NotFound()`, not 403, so IDs can't be probed.
- **Business rule failures** throw `BusinessRuleException(ruleId, message)` (it derives from InvalidOperationException); controllers catch it and show the message in ModelState or TempData["Error"].
- **Async EF Core** everywhere; `AsNoTracking()` for read-only queries; an explicit transaction for multi-table writes; a serializable transaction when booking or rescheduling an appointment.
- **Time.** The app runs in one time zone and stores local times. Services get the time from `IClock.Now` and entity methods take a `now` parameter – never `DateTime.Now` in business code, so tests can fix the clock.
- **Notifications** are raised through `INotificationService.NotifyAsync` by the service that makes the change.

## Database rules (EF Core)

- Follow `docs/design-spec.md` section 2 exactly: names, types, max lengths, nullability, precision, keys, indexes, check constraints and delete rules.
- Table name = class name for our 20 entities (`ToTable` in `OnModelCreating`); Identity tables keep their `AspNet*` names. Configure every relationship explicitly (HasOne/WithMany/HasForeignKey/OnDelete) – don't rely on naming conventions.
- Delete behaviour is `Restrict` on every one of our relationships except the seven compositions, which cascade: NotificationPreference, Notification, TrainerAvailability, WorkoutPlanExercise, WorkoutLogEntry, DietPlanMeal, MealLogItem. Set it per relationship; don't loop over all foreign keys, because Identity's own tables must keep their cascades.
- Enums stored as int. Date-only columns are `DateTime` with `[Column(TypeName = "date")]`; time columns are `TimeSpan` (SQL `time`); `[Precision(p, s)]` on every decimal; `[Timestamp] byte[] RowVersion` on Appointment.
- Spec defaults ("Default 1", "Default 07:00") are C# property initialisers, not database defaults – with a database default on a bool, EF Core can never insert `false`.
- Check constraints use `ToTable("Name", t => t.HasCheckConstraint(...))` (the older `HasCheckConstraint` on the entity builder is obsolete).
- The six encrypted columns (ClientProfile.MedicalConditions, Allergies, CurrentMedications; HealthMeasurement.Notes; Appointment.ClientReason, ProfessionalNotes) use `EncryptedStringConverter`, backed by `IEncryptionService` (Data Protection purpose `HealthTrack.HealthData.v1`, keys in `App_Data/keys`). EncryptionService is a **singleton**: EF Core caches the model, so the converter keeps the first instance it is given.
- Migrations live in `Data/Migrations`. The app runs `Database.MigrateAsync()` and `DbSeeder.SeedAsync()` at startup, so a marker only presses F5.
- **Phase 1 creates the whole schema.** Later phases should not need schema changes; if one does, stop and ask me first. Never edit a migration that has been applied – add a new one.

## Identity and security setup (Program.cs)

- Leave `options.Stores.SchemaVersion` at its default. .NET 10's `IdentitySchemaVersions.Version3` adds an AspNetUserPasskeys table, which would be a schema change.
- Password: at least 8 characters with upper case, lower case, a digit and a symbol; `RequireUniqueEmail = true`; `SignIn.RequireConfirmedAccount = false`; `PasswordHasherOptions.IterationCount = 100_000` (stated explicitly for the report).
- Lockout: 5 failed attempts → 15 minutes, on for every user; sign-in checks always use `lockoutOnFailure: true`.
- Login: find the user, `CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`, and only after the password is right check `user.CanSignIn()` (Pending, Suspended and Deactivated get their own message), then `SignInAsync`. Unknown email and wrong password show the same message. Redirect to `returnUrl` only if `Url.IsLocalUrl(returnUrl)`, otherwise to the role's dashboard.
- Cookie: LoginPath `/Account/Login`, AccessDeniedPath `/Error/Forbidden`, 30-minute sliding expiration, HttpOnly, `SecurePolicy = Always`, SameSite Lax.
- `SecurityStampValidatorOptions.ValidationInterval = 1 minute`; changing a user's status updates their security stamp, so a suspended user is signed out within a minute.
- Fallback authorization policy requires an authenticated user; `[AllowAnonymous]` only on Home, the Account login/register actions and Error.
- `AutoValidateAntiforgeryTokenAttribute` as a global filter; AJAX sends the token in the `RequestVerificationToken` header (`AddAntiforgery(o => o.HeaderName = "RequestVerificationToken")`).
- Errors: `UseExceptionHandler("/Error/ServerError")` and `UseHsts()` outside Development, `UseStatusCodePagesWithReExecute("/Error/{0}")` always. ErrorController is `[AllowAnonymous]` and `[IgnoreAntiforgeryToken]` (re-executed requests keep their POST method) and its actions accept any HTTP method.
- Pipeline order: exception handler → HSTS → HTTPS redirection → security headers (Phase 9) → static files → routing → authentication → authorization → area route `{area:exists}/{controller=Dashboard}/{action=Index}/{id?}` → default route. (This project's .NET 10 template serves static files with `MapStaticAssets()` instead of `UseStaticFiles()`; keep it. Its files are endpoints, so the fallback policy would cover them too: map them with `MapStaticAssets().AllowAnonymous()` so the login page gets its CSS and JS.)
- Data Protection: `PersistKeysToFileSystem(App_Data/keys)`, `SetApplicationName("HealthTrack")`, and `ProtectKeysWithDpapi()` when `OperatingSystem.IsWindows()`.
- No inline `<script>` in views (a Content-Security-Policy arrives in Phase 9); page scripts live in `wwwroot/js` and get data from `data-` attributes or JSON actions.
- Never `Html.Raw` user input. Never log or audit passwords or health values.
- Audit (`IAuditService.LogAsync`) logins, failed logins, lockouts, logouts, registrations, approvals, assignments, plan and appointment changes, exports and deletions.

## UI conventions

- Bootstrap 5 cards and tables, one consistent navigation per role, page titles via `ViewData["Title"]`.
- Success and error messages through TempData and the `_Alerts` partial.
- Destructive actions confirm first. Lists page at 20 rows. Empty states say what to do next.
- Forms show validation messages next to fields and a summary at the top. Enum dropdowns use `Html.GetEnumSelectList<T>()` with the enums' `[Display]` names.
- Dates `dd MMM yyyy`, times `HH:mm`; weight and BMI 1 dp, kcal 0 dp.

## Coding conventions (these are marked)

- PascalCase for types, methods and properties; camelCase for parameters and locals; `I` prefix for interfaces; `Async` suffix; `Controller`, `Service`, `ViewModel` suffixes; one class per file.
- XML doc comments (`///`) on every public class and member. Short comments that explain *why*, citing the rule (for example `// BR-15: bookings must be 2 hours to 60 days ahead`).
- Validate input with data annotations on view models plus the custom attributes; check the rules again in services.
- Catch only what you can handle (for example `DbUpdateConcurrencyException` → friendly message); let the global handler deal with the rest.

## Testing (HealthTrack.Tests)

- Test HealthCalculator, entity methods and services. Each service test gets a fresh InMemory database (`Guid.NewGuid()` name), a fixed `IClock` and a fake or ephemeral `IEncryptionService`.
- EF Core InMemory limits: configure `ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))`; relational-only APIs throw on it, so open the serializable booking transaction through one helper that skips it when `!Database.IsRelational()`; don't use `ExecuteUpdate`/`ExecuteDelete`, raw SQL or `EF.Functions` in services; InMemory doesn't enforce foreign keys or unique indexes, so services check uniqueness themselves.
- Test names: `MethodName_Situation_ExpectedResult`.

## Demo accounts (created by DbSeeder)

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Admin | admin@healthtrack.test | Admin@123 | |
| Trainer | trainer@healthtrack.test | Trainer@123 | Fitness trainer, approved; coaches client@, client2@, client3@ |
| Trainer | nutritionist@healthtrack.test | Trainer@123 | Nutritionist, approved; coaches client4@ |
| Trainer | physio@healthtrack.test | Trainer@123 | Physiotherapist, **pending approval** (to demo UC30) |
| Client | client@healthtrack.test | Client@123 | The main demo client, with 8 weeks of history from Phase 8 |
| Client | client2@ … client6@healthtrack.test | Client@123 | Mixed goals and activity; client5@ and client6@ start unassigned |

## Commands

```
dotnet build
dotnet test
dotnet run --project HealthTrack --launch-profile https
dotnet tool restore
dotnet ef migrations add <Name> --project HealthTrack --output-dir Data/Migrations
dotnet ef migrations script --project HealthTrack --output <file outside the repo>
dotnet ef database drop --force --project HealthTrack     (development reset; the next start recreates and reseeds it)
```

- If a build fails because files in `bin/` are locked, the app is still running in Visual Studio – ask me to stop it.
- You may start the app in the background for about 20 seconds to check startup (migrations, seeding, no exceptions in the log), then stop it. For UI checks, give me a short list of things to click; I run it in Visual Studio.

## Working agreement

- Read this file and the spec sections a phase names before starting it.
- Make each change completely: entities, services, controllers, views, scripts and tests together.
- End every phase with `dotnet build` (0 errors, aim for 0 warnings), `dotnet test` all green, a short summary of the files changed, a checklist of what I should check in the browser, and a git commit `Phase N: <title>`. Don't push.
- Don't stop to ask about small UI choices – follow these conventions. Do ask before changing the schema, adding packages or departing from the spec.
