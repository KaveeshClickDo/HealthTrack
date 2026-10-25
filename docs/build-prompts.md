# HealthTrack – build prompts for Claude Code

Eleven prompts (Phase 0 to Phase 10) that take the empty Visual Studio project to the finished HealthTrack system described in `docs/design-spec.md`. Paste one phase at a time into Claude Code. The rules every phase follows are in `CLAUDE.md`, which Claude Code reads automatically.

## Before you start (once)

**1. Create the project in Visual Studio**

| Setting | Choose |
| --- | --- |
| Template | ASP.NET Core Web App (Model-View-Controller), C# |
| Project name | `HealthTrack` |
| Solution name | `HealthTrack` ("Place solution and project in the same directory" unticked) |
| Framework | .NET 8.0 (Long Term Support) if it is listed, otherwise .NET 10.0 (Long Term Support) |
| Authentication type | None (we build our own login pages on ASP.NET Core Identity) |
| Configure for HTTPS | Ticked |
| Container support, Aspire | Unticked |
| Do not use top-level statements | Unticked |

Either framework works with this kit. .NET 8 opens in Visual Studio 2022 and 2026 (Microsoft's support for it ends on 10 Nov 2026, which doesn't affect a coursework build); .NET 10 needs Visual Studio 2026. If you already created the project with another name, keep it: Phase 0 adapts the kit to it.

**2. Copy the kit into the solution folder** (the folder that holds `HealthTrack.sln` or `HealthTrack.slnx`):

```
HealthTrack/            <- solution folder
  CLAUDE.md             <- from the kit
  docs/                 <- from the kit (design-spec.md, build-prompts.md, diagrams/)
  HealthTrack.slnx
  HealthTrack/          <- web project (HealthTrack.csproj)
```

**3. Open a terminal in the solution folder and start Claude Code** with `claude`.

## How to run each phase

1. Stop the app in Visual Studio first (Shift+F5). A running app locks the files in `bin/` and the database, and Claude's builds fail.
2. Paste the phase prompt. For the big phases (1, 4, 5, 6 and 8) you can switch to plan mode first (Shift+Tab, or type `/plan`), read Claude's plan, then let it build.
3. When Claude finishes, it has built, tested and committed. Press F5 in Visual Studio and go through the phase's **Check in the browser** list. Tell Claude about anything wrong in the same session.
4. Type `/clear` before the next phase. CLAUDE.md stays loaded and a fresh context gives better work.

Each phase ends with a git commit, so you can always go back to the end of the previous phase.

## Phase map

| Phase | Builds | Brief tasks | Week (spec 6) | Suggested owner (spec 6.1) |
| --- | --- | --- | --- | --- |
| 0 | Project setup | – | 1 | Member 1 |
| 1 | Domain model, database, calculations | ERD | 1 | Member 1 |
| 2 | Accounts, roles, layout, security foundation | Task 1 | 1 | Member 1 |
| 3 | Admin: users, approvals, assignments, libraries, audit log | Task 4 (part) | 2–3 | Member 1 |
| 4 | Workout plans, templates, logging, feedback | Task 5, Task 3 (part) | 2–3 | Member 2 |
| 5 | Diet plans, meals, measurements, progress charts | Task 5, Task 7 (trends) | 2–3 | Member 3 |
| 6 | Availability and appointments | Task 6 | 4–5 | Member 4 |
| 7 | Notifications and reminders | Task 8 | 4–5 | Member 4 |
| 8 | Demo data, three dashboards, three reports | Tasks 2, 3, 4, 7 | 5–6 | Members 2 and 3 (dashboards), Member 4 (reports) |
| 9 | Security hardening and privacy | Security | 6 | Member 1 |
| 10 | Polish, manual tests, installation guide, user manual | Documentation | 7 | Everyone |

**Working as a team.** Phases 0–2 come first, done by one person while the others review. After Phase 2 is merged, Phases 3–7 can run in parallel: each member works on their own branch (for example `git checkout -b phase-4-workouts`) with their own Claude Code session. The whole schema exists from Phase 1 and the whole navigation from Phase 2, so the phases touch different folders. Expect small merge conflicts in `Program.cs` (service registrations) and `DbSeeder.cs`; merge in phase order. Phase 5 adds an action to the Trainer `ClientsController` that Phase 4 creates, so merge Phase 4 first. Phases 8–10 start once everything is merged.

---

## Phase 0 – Project setup

```text
Phase 0 – Project setup.

Read CLAUDE.md and docs/design-spec.md section 1.13 first. Visual Studio has created an ASP.NET Core Web App (Model-View-Controller) project with Authentication type None and HTTPS on. Prepare the solution for the rest of the build. Don't write application code yet.

1. Inspect the solution and report: the solution file (.sln or .slnx), the web project's name, its target framework, its root namespace, and whether its .csproj is in a subfolder of the solution folder (the usual layout) or in the solution folder itself (Visual Studio's "Place solution and project in the same directory" option). Adjust the layout tree and the --project paths in CLAUDE.md to match.
   - If the web project isn't called HealthTrack.Web, don't rename anything: replace HealthTrack.Web with the real name throughout CLAUDE.md and docs/build-prompts.md.
   - If the target framework is net10.0, keep it: use 10.0.x for every Microsoft package and for dotnet-ef, and change the Tech stack lines in CLAUDE.md to say net10.0 / 10.0.x. The spec's ".NET 8" and "EF Core 8" then mean .NET 10 and EF Core 10; add a one-line note saying so under the spec's title.
2. Make sure the web project has <Nullable>enable</Nullable> and <ImplicitUsings>enable</ImplicitUsings>.
3. Add to the web project, at the newest patch of the framework's major version: Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Tools.
4. Create the MSTest project HealthTrack.Tests (same target framework) in a HealthTrack.Tests folder inside the solution folder, add it to the solution, reference the web project and add Microsoft.EntityFrameworkCore.InMemory (same version as the other EF Core packages). Replace the template test with one placeholder test that passes. If the web .csproj sits in the solution folder itself, exclude HealthTrack.Tests/** and docs/** from the web project in its .csproj (Compile, Content, None and EmbeddedResource) so the tests and docs aren't built into the web app.
5. Create a local tool manifest (dotnet new tool-manifest) and install dotnet-ef at the EF Core version, so teammates only need dotnet tool restore.
6. Create the folders from the CLAUDE.md layout that don't exist yet: Areas/Admin, Areas/Trainer and Areas/Client (each with Controllers and Views), Models/Entities, Models/Enums, Models/ViewModels, Data, Services, Security, Helpers.
7. Download Chart.js 4 (the UMD build, chart.umd.js or chart.umd.min.js) and its LICENSE into wwwroot/lib/chart.js/. Nothing may load from a CDN at runtime.
8. appsettings.json: ConnectionStrings:DefaultConnection = Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True (escape the backslash in JSON); an "Smtp" section (Host, Port 587, EnableSsl true, From noreply@healthtrack.test, UserName, Password, UsePickupDirectory true, PickupDirectory "App_Data/mail"); a "Reminders" section (Enabled true, IntervalSeconds 60). Leave credentials empty – real ones would go in user secrets.
9. Add a .NET .gitignore (dotnet new gitignore) plus App_Data/keys/ and App_Data/mail/. If the folder isn't a git repository yet, run git init.
10. Run dotnet build and dotnet test, then commit "Phase 0: project setup".

Done when: the solution builds with 0 errors, dotnet test runs 1 passing test, dotnet ef --version prints the EF Core version, and the app still starts on the template home page.
```

**Check in the browser:** F5 still shows the template's home page over https.

---

## Phase 1 – Domain model, database and calculations

```text
Phase 1 – Domain model, database and calculations.

Read CLAUDE.md, then docs/design-spec.md sections 1.6 (lifecycle table), 1.10, 1.11, all of section 2, and 5.1–5.2. docs/diagrams/03-erd-physical.svg and 05-class-diagram-domain-model.svg show the same model. Build the complete data layer now – later phases must not need schema changes, so match the spec exactly.

1. Enums (Models/Enums): the 18 enums of 2.10 with their exact int values and a [Display(Name = "...")] label on every member (LightlyActive → "Lightly active", CancelledByClient → "Cancelled by client").

2. Entities (Models/Entities): ApplicationUser : IdentityUser and the 20 entity classes of 2.3–2.8.
   - Every column with the spec's name, type and nullability ("Null: Yes" → nullable type), [MaxLength], [Precision], [Column(TypeName = "date")] on date columns, TimeSpan for time columns, [Timestamp] byte[] RowVersion on Appointment, long for AuditLogId.
   - [Key] where the key isn't <ClassName>Id: ClientProfile.ClientId, TrainerProfile.TrainerId, TrainerAvailability.AvailabilityId, TrainerAssignment.AssignmentId, HealthMeasurement.MeasurementId, NotificationPreference.UserId.
   - Spec defaults as property initialisers (see CLAUDE.md).
   - Navigation properties: virtual references and virtual ICollection<T> initialised to new List<T>(), named after what they point to (User, Client, Trainer, Exercise, WorkoutPlan, SourceTemplate, Copies, Entries, Items, Meals, ...).
   - The methods and computed properties of the 5.1 table as plain C# with no database access ([NotMapped] on computed properties). They work on collections the caller has loaded. XML comments citing the rules. In particular:
     - ApplicationUser: FullName; Age (display only, may use DateTime.Today); CanSignIn() true only when AccountStatus is Active.
     - ClientProfile: GetActiveAssignment(), GetActiveWorkoutPlan(), GetActiveDietPlan(), GetLatestMeasurement() over the loaded collections.
     - TrainerProfile: IsApproved (ApprovedAt has a value); ActiveClientCount() counts assignments with no EndDate; HasCapacity() = ActiveClientCount() < MaxClients; IsAvailable(start, minutes) = some availability window Covers it.
     - TrainerAvailability.Covers(start, minutes): same weekday, starts at or after StartTime and ends at or before EndTime.
     - TrainerAssignment: IsActive (EndDate is null); End(endDate).
     - Exercise.EstimateCalories(weightKg, minutes) and WorkoutLog.AddEntry(exercise, weightKg, minutes, sets, reps, loadKg, distanceKm) use HealthCalculator.CaloriesBurned; AddEntry stores CaloriesBurned as a snapshot; TotalDurationMinutes and TotalCaloriesBurned are sums.
     - WorkoutPlan.CreateCopyForClient(clientId, trainerId, start): a new Draft client plan with copies of every WorkoutPlanExercise, SourceTemplateId = this plan's id, IsTemplate false, the template unchanged (BR-12). GetExercisesFor(day) ordered by SortOrder; GetScheduledDays(); Activate() (Draft → Active, needs at least one exercise, BR-10); Archive().
     - WorkoutPlanExercise.IsValidPrescription(): Sets and Reps, or DurationMinutes (BR-11).
     - FoodItem.CaloriesFor(quantity); MealLog.AddItem(food, quantity) snapshots Calories, ProteinG, CarbsG and FatG as quantity × the per-serving values; TotalCalories, TotalProteinG, TotalCarbsG and TotalFatG are sums.
     - DietPlan: GetMealsFor(day) (DayOfWeek null or equal, ordered by ScheduledTime), IsActiveOn(date), Activate() (needs at least one meal). DietPlanMeal.AppliesTo(day).
     - HealthMeasurement: Bmi and BmiCategory through HealthCalculator.
     - Appointment: EndDateTime; OverlapsWith(start, end) with half-open intervals (an appointment ending at 10:00 doesn't overlap one starting at 10:00); Approve, Reject, Reschedule, CancelByClient, CancelByTrainer, Complete and MarkNoShow follow the lifecycle table in spec 1.6 exactly – allowed-from states, required reasons, the client's 2-hour cancel limit, Complete and MarkNoShow only after the start, CancelByTrainer only before it. An illegal transition throws BusinessRuleException with BR-16 or BR-17.
     - Notification.MarkAsRead(now); static NotificationPreference.CreateDefault(userId).

3. Helpers: IClock + SystemClock (Now, Today); BusinessRuleException(string ruleId, string message) : InvalidOperationException; static HealthCalculator with the 1.10 formulas – CalculateBmi(weightKg, heightCm) rounded to 1 dp, GetBmiCategory (boundaries 18.5, 25 and 30), CalculateAge(dateOfBirth, today), CalculateBmr(weightKg, heightCm, age, gender), CalculateTdee(bmr, activityLevel), SuggestCalorieTarget(tdee, goal) (−500 to lose weight, +300 to gain muscle, never below 1,200), CaloriesBurned(met, weightKg, minutes).

4. Security: IEncryptionService/EncryptionService (Encrypt and Decrypt through IDataProtectionProvider.CreateProtector("HealthTrack.HealthData.v1"); null stays null; if decryption fails, log a warning and return null rather than crash the page) and EncryptedStringConverter : ValueConverter<string?, string?>.

5. Data/ApplicationDbContext : IdentityDbContext<ApplicationUser>, constructor (DbContextOptions<ApplicationDbContext>, IEncryptionService), 20 DbSets. OnModelCreating calls base first, then per entity: ToTable(class name); relationships R3–R32 of 2.9 with the delete rules in CLAUDE.md; the unique and extra indexes of 2.11 (Notification.ReminderKey unique with HasFilter("[ReminderKey] IS NOT NULL")); the check constraints – WorkoutPlan: IsTemplate = 1 exactly when ClientId is null; TrainerAvailability: EndTime > StartTime; EndDate IS NULL OR EndDate >= StartDate on WorkoutPlan, DietPlan and TrainerAssignment; HealthMeasurement: DiastolicBp < SystolicBp when both have values; and the converter on the six encrypted columns. Add a helper for the serializable transaction described in CLAUDE.md (returns null when the provider isn't relational).

6. Program.cs: AddDbContext with ConnectionStrings:DefaultConnection; Data Protection as in CLAUDE.md; IClock and IEncryptionService as singletons. After builder.Build(), await DbSeeder.SeedAsync(app.Services).

7. Data/DbSeeder: creates a scope, runs MigrateAsync, then seeds – only into empty tables – the Exercise library (at least 30 exercises covering every category and muscle group, MET values from the Compendium of Physical Activities, equipment, short instructions) and the FoodItem library (at least 40 foods covering every category with realistic serving size, unit, kcal and macros). Structure SeedAsync as ordered steps; later phases add users, templates, availability and demo history after these.

8. Migration InitialCreate in Data/Migrations. Generate the SQL script to a temp file outside the repo and check it: 27 tables (the 23 ERD tables plus AspNetUserClaims, AspNetUserLogins, AspNetUserTokens and AspNetRoleClaims) and __EFMigrationsHistory; ON DELETE CASCADE only on the 7 compositions and Identity's own 6 foreign keys; the 6 check constraints; the filtered unique index; nvarchar(max) for the encrypted columns; rowversion on Appointment. If anything differs, fix the model, dotnet ef migrations remove, and add it again. Delete the temp file.

9. Tests: HealthCalculator (BMI and every category boundary, BMR for each gender branch, each TDEE factor, the 1,200 kcal floor, calories burned); every Appointment transition, allowed and refused, and OverlapsWith edge cases; CreateCopyForClient copies the lines and leaves the template unchanged; IsValidPrescription; Covers; AppliesTo; AddItem snapshots and totals; AddEntry calories.

Finish as CLAUDE.md says: build, test, start the app once so it creates HealthTrackDb, then commit "Phase 1: domain model and database".
```

**Check in the browser:** the app still starts. In Visual Studio, View → SQL Server Object Explorer → (localdb)\MSSQLLocalDB → HealthTrackDb shows the tables, with exercises and foods in their tables.

---

## Phase 2 – Accounts, roles, layout and security foundation

```text
Phase 2 – Accounts, roles, layout and the security foundation (Task 1).

Read CLAUDE.md, then docs/design-spec.md sections 1.2, 1.3, 1.9, 1.11 (BR-01 to BR-08), 3.2, 4.2 (UC01–UC06) and 5.3.

1. Program.cs: AddIdentity<ApplicationUser, IdentityRole> with the options in CLAUDE.md, AddEntityFrameworkStores, AddDefaultTokenProviders; PasswordHasherOptions; ConfigureApplicationCookie; SecurityStampValidatorOptions; the fallback policy; AddControllersWithViews with the global AutoValidateAntiforgeryTokenAttribute; AddAntiforgery header name; AddHttpContextAccessor; the middleware pipeline and routes in CLAUDE.md. Remove the template's HomeController.Error action, ErrorViewModel and Views/Shared/Error.cshtml.

2. Controllers/BaseController (abstract): CurrentUserId (NameIdentifier claim), AuditAsync(action, entityName, entityId, details), SetSuccess(message), SetError(message). Abstract area base controllers ClientAreaController, TrainerAreaController and AdminAreaController, each with [Area] and [Authorize(Roles)]. Each area also gets Views/_ViewStart.cshtml (shared layout) and Views/_ViewImports.cshtml (usings and @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers – without it, tag helpers silently do nothing in area views).

3. Services (interface + implementation, AddScoped):
   - IAuditService/AuditService: LogAsync(userId, action, entityName, entityId, details) recording the IP from IHttpContextAccessor (null outside a request); GetPageAsync(filter, page) for the Phase 3 viewer.
   - INotificationService/NotificationService: NotifyAsync(userId, type, title, message, linkUrl) saves a Notification; LinkUrl must be a local path. The inbox, preferences, reminders and email copies come in Phase 7.
   - IAccountService/AccountService: RegisterClientAsync and RegisterTrainerAsync. One transaction around UserManager.CreateAsync (UserName = Email), AddToRoleAsync, the profile, the client's first HealthMeasurement (height and weight from the form), NotificationPreference.CreateDefault and the Register audit row, so a failure leaves nothing half-created (UserManager uses the same scoped DbContext, so the transaction covers it). Clients start Active. Trainers start PendingApproval with MaxClients 20, and every active admin gets an AccountUpdate notification "New trainer registration". Identity errors go back to the form.
   - IAssignmentService/AssignmentService, read side only (Phase 3 adds assigning): GetActiveTrainerAsync(clientId), IsTrainerAssignedToClientAsync(trainerId, clientId), GetUnassignedClientsAsync().

4. AccountController, [AllowAnonymous] except Logout: Register (two cards: "I'm a client" and "I'm a trainer or health professional"), RegisterClient, RegisterTrainer, Login, Logout (POST). Login follows the CLAUDE.md flow; audit Login, LoginFailed (UserId null for an unknown email; never the password) and AccountLocked; set LastLoginAt; a signed-in user who opens Login or Register goes to their dashboard. Messages: pending trainer → "Your trainer account is waiting for an administrator's approval."; suspended or deactivated → "This account is not active. Please contact the administrator."; locked → "Too many failed attempts. Try again in 15 minutes."

5. View models with full validation: RegisterClientViewModel and RegisterTrainerViewModel (the 1.3 fields; names allow letters, spaces and hyphens with a regex that works in both .NET and JavaScript – no \p{L}; DateOfBirth [MinimumAge(16)]; the privacy consent must be ticked; client height 50–250 cm, weight 20–350 kg, target weight 30–300 kg; trainer years of experience 0–60; LicenseNumber required unless ProfessionalType is FitnessTrainer, through IValidatableObject plus a small client-side rule), LoginViewModel, ProfileViewModel, ChangePasswordViewModel.

6. Security/MinimumAgeAttribute and NotInFutureAttribute: server-side with IClock from validationContext.GetService, plus IClientModelValidator and matching jQuery Validation adapters in wwwroot/js/validators.js, loaded by _ValidationScriptsPartial.

7. ManageController: Index (account summary), EditProfile (first and last name, phone, date of birth, gender), ChangePassword (ChangePasswordAsync, RefreshSignInAsync, audit Update without the password).

8. ErrorController, [AllowAnonymous] and [IgnoreAntiforgeryToken], with the spec's three actions routed for UseStatusCodePagesWithReExecute("/Error/{0}"):
   - [ActionName("NotFound")] PageNotFound() on routes Error/NotFound and Error/404 (a method named NotFound would hide ControllerBase.NotFound());
   - Forbidden() on Error/Forbidden and Error/403;
   - ServerError(int? code) on Error/ServerError and Error/{code:int} – 500, 400 (for example a failed anti-forgery check) and anything else.
   Friendly views with a link home and no exception details outside Development.

9. HomeController, [AllowAnonymous]: Index (a landing page that explains HealthTrack, with Register and Log in buttons; signed-in users are sent to their dashboard) and Privacy (the privacy notice: what is collected and why, encryption of medical notes, who can see what, the medical-sharing setting, the export and delete rights). The consent checkbox links to it.

10. In each area a placeholder DashboardController.Index ("Welcome, {FullName}") so the post-login redirects work. Phase 8 replaces them.

11. Layout: Views/Shared/_Layout.cshtml with a Bootstrap navbar and the complete navigation for each role now, so later phases don't need to edit it (links to pages that don't exist yet are fine for now):
   - Client: Dashboard; My plans (Workouts/MyPlan, Diet/MyPlan); Log (Today's workout, Log a meal, Add a measurement); Progress; Appointments; Feedback.
   - Trainer: Dashboard; Clients; Workout plans; Templates; Diet plans; Appointments; Availability; Reports.
   - Admin: Dashboard; Users; Trainer approvals; Assignments; Library (Exercises, Foods); Reports; Audit log.
   - Right side for signed-in users: a notification bell (a link to /Notifications with a badge span id="notification-count", wired in Phase 7) and a user menu (Manage account, Notification settings, Log out as a POST form). Guests see Log in and Register.
   Also Views/Shared/_Alerts.cshtml (TempData success and error), a footer with the Privacy link, wwwroot/css/site.css with a calm, clean health theme, and wwwroot/js/healthtrack.js loaded on every page (empty for now).

12. DbSeeder, only when there are no users: roles Admin, Trainer and Client, and the demo accounts in CLAUDE.md created through UserManager – client profiles with varied goals and activity levels, one first measurement each, default notification preferences, trainer professional details (physio@ stays PendingApproval with ApprovedAt null), and active TrainerAssignments (client@, client2@ and client3@ → trainer@; client4@ → nutritionist@).

13. Tests: MinimumAge and NotInFuture boundaries with a fixed clock; CanSignIn for each status; the AssignmentService read methods. Use a small test helper that builds Identity (AddIdentityCore + roles + EF stores) on an InMemory context wherever UserManager is needed.

Finish as CLAUDE.md says and commit "Phase 2: accounts, roles and layout".
```

**Check in the browser:**
- The home page explains HealthTrack. Register as a new client: you land on the Client dashboard. Log out.
- Register as a trainer: you see the pending message, and logging in shows "waiting for approval".
- Under-16 date of birth: the error appears before the form is sent.
- Log in as admin@healthtrack.test → Admin dashboard. As client@healthtrack.test, open /Admin/Dashboard → friendly Forbidden page.
- Five wrong passwords for client2@ → the lockout message.
- /does-not-exist → friendly 404 page. Change password, log out, log in with the new one.

---

## Phase 3 – Administrator module

```text
Phase 3 – Administrator module: users, trainer approvals, assignments, libraries and the audit log (part of Task 4).

Read CLAUDE.md, then docs/design-spec.md section 1.2 (permissions), BR-03 to BR-08 and BR-21, UC28–UC33 and 5.3 (Admin area).

1. AccountService: CreateAdminAsync (Admin role, Active, audit Create); ApproveTrainerAsync (ApprovedAt = now, Active, AccountUpdate notification "Your trainer account has been approved", audit Approve); RejectTrainerAsync(reason) (Deactivated, reason in the audit details, audit Reject); SetAccountStatusAsync(userId, status, actingAdminId) for Suspend and Reactivate – BR-04: an admin can't suspend themselves or the last active admin; always UpdateSecurityStampAsync so a suspended user is signed out within a minute (BR-05); audit Update.

2. AssignmentService, write side: AssignTrainerAsync(clientId, trainerId, startDate, notes, actingAdminId) – the trainer must be approved and Active with capacity (BR-07); ends the client's current assignment in the same transaction (BR-06); AccountUpdate notifications to the client ("Your trainer is …") and the trainer ("New client assigned"); audit Assign. EndAssignmentAsync(assignmentId, endDate). GetAssignmentsAsync(filter, page) and GetTrainerLoadAsync() for the page.

3. ILibraryService/LibraryService: GetExercisesAsync(search, category, includeInactive, page), GetExerciseAsync(id), SaveExerciseAsync (create or update; duplicate names refused in the service), SetExerciseActiveAsync; the same for foods (GetFoodsAsync, GetFoodAsync, SaveFoodAsync, SetFoodActiveAsync); GetActiveExercisesAsync and GetActiveFoodsAsync for the pickers in Phases 4 and 5. Nothing is ever deleted (BR-21).

4. Admin area controllers and views (all inherit AdminAreaController):
   - UsersController: Index (search by name or email, filter by role and status, 20 per page), Details (account details and activity totals – never clinical notes or measurement values), CreateAdmin (GET/POST), Suspend (POST, with confirmation), Reactivate (POST).
   - TrainerApprovalsController: Index (pending trainers with professional type, specialisation, qualifications, licence, experience and bio), Approve (POST), Reject (POST, reason required).
   - AssignmentsController: Index (active assignments, unassigned clients, each trainer's load against MaxClients), Assign (GET/POST AssignTrainerViewModel: client, approved trainer with capacity, start date, notes), End (POST).
   - LibraryController: Exercises, EditExercise (GET/POST; no id = create), Foods, EditFood, ToggleActive (POST with type and id).
   - AuditLogController: Index (filters: user email, action, date range; newest first; 20 per page).

5. Tests: AssignTrainerAsync ends the previous assignment and refuses a full or unapproved trainer; SetAccountStatusAsync refuses self-suspension and never leaves zero active admins; SaveExerciseAsync refuses a duplicate name.

Finish as CLAUDE.md says and commit "Phase 3: administrator module".
```

**Check in the browser** (as admin@healthtrack.test):
- Trainer approvals shows physio@. Approve it, log in as physio@ in a private window: it works.
- Suspend client2@ while client2@ is signed in in a private window: within a minute their next click goes to the login page. Reactivate client2@ afterwards. Try to suspend yourself: refused.
- Assign client5@ to trainer@, then to nutritionist@: the first assignment shows as ended.
- Add an exercise; deactivate a food. The audit log lists everything you just did.

---

## Phase 4 – Workout plans, templates, logging and feedback

```text
Phase 4 – Workout plans, routine templates, workout logging and feedback (Task 5 workouts, part of Task 3).

Read CLAUDE.md, then docs/design-spec.md sections 1.5 (Workout plans), 1.10 (calories), BR-08 to BR-13, UC08, UC09, UC15, UC18–UC20, UC22, UC35, UC39 and 5.3.

1. IWorkoutService/WorkoutService:
   - CreatePlanAsync(trainerUserId, WorkoutPlanViewModel): a client plan (assigned clients only, BR-08; StartDate required) or a template (IsTemplate, no client). Status Draft.
   - AddExerciseAsync(planId, PlanExerciseViewModel): active exercise, valid prescription (BR-11), SortOrder = next on that day. UpdatePlanAsync: details, line edits, removing lines and archiving – plans are archived, never deleted (BR-21). Only the plan's author edits it.
   - SaveAsTemplateAsync(planId): a template copy (UC39); the client plan is unchanged.
   - AssignTemplateAsync(templateId, clientId, startDate, activateNow): CreateCopyForClient (BR-12). Any trainer may use any template, but only for their own assigned clients.
   - ActivatePlanAsync(planId): BR-10 checks; completes the client's current active workout plan in the same transaction (BR-09); PlanAssigned notification to the client; audit Update.
   - GetTodaysWorkoutAsync(clientId, today) and GetWeekAsync(clientId) for MyPlan.
   - LogWorkoutAsync(clientUserId, LogWorkoutViewModel): BR-13 (not in the future, at least one entry); each entry through WorkoutLog.AddEntry with the client's latest weight (calorie snapshot); WorkoutPlanId set when logged from Today; audit Create.
   - GetHistoryAsync(clientId, page).

2. IFeedbackService/FeedbackService: GiveFeedbackAsync(trainerUserId, FeedbackViewModel) – assigned clients only; an optional WorkoutLogId must belong to that client; NewFeedback notification; GetForClientAsync(clientId, page).

3. Trainer area:
   - ClientsController.Index: the trainer's assigned clients with goal, assigned since, last workout date and links (create plan, assign template, give feedback). Phase 5 adds Progress; Phase 8 adds adherence.
   - WorkoutPlansController: Index (this trainer's client plans with status), Create, Edit (details, then the lines grouped by weekday with Remove checkboxes, plus an add-exercise form posting to AddExercise), AddExercise (POST), SaveAsTemplate (POST), Templates (every template with author, exercise count and times assigned), AssignTemplate (GET/POST), Activate (POST, with confirmation).
   - FeedbackController.Create (GET with optional clientId and workoutLogId, POST).

4. Client area:
   - WorkoutsController: MyPlan (the active plan as a Monday–Sunday grid: sets × reps or duration, rest, notes), Today (today's planned exercises pre-filled as entry rows to tick off and adjust; DurationMinutes from the plan, or sets × 2 minutes when the plan only gives sets and reps), Log (GET/POST, a free workout with add and remove rows), History (sessions with total minutes and kcal, their entries and any feedback).
   - FeedbackController.Index.
   - wwwroot/js/workout-log.js adds and removes entry rows, re-indexes the input names so model binding works, and re-parses validation.

5. DbSeeder: if there are no templates, add three by trainer@ – "Beginner full body", "5K runner" and "Strength split" – each with 4–8 exercises over 3 days.

6. Tests: AssignTemplateAsync copies the lines and records SourceTemplateId; ActivatePlanAsync completes the previous active plan and refuses an empty plan; LogWorkoutAsync stores MET × weight × hours and refuses future dates and empty sessions; a trainer can't create a plan for a client who isn't assigned to them.

Finish as CLAUDE.md says and commit "Phase 4: workouts and feedback".
```

**Check in the browser:**
- As trainer@: Templates shows the three templates. Assign "Beginner full body" to client@ starting today and activate it. Build a plan from scratch for client2@ and try to activate it with no exercises (refused).
- As client@: My plan shows the weekly grid; Today's workout is pre-filled; log it and see the kcal in History. Try a future date (refused).
- As trainer@: give feedback on that session. As client@: it appears under Feedback.

---

## Phase 5 – Diet, measurements and progress charts

```text
Phase 5 – Diet plans, meal logging, measurements and progress charts (Task 5 diet, Task 7 trends).

Read CLAUDE.md, then docs/design-spec.md sections 1.5 (Diet plans and measurements), 1.7 (first three bullets), 1.10, BR-09, BR-10, BR-13, BR-14, BR-20, UC10–UC12, UC18, UC21, UC36, UC37 and 5.3.

Forms use DietPlanViewModel, PlanMealViewModel, LogMealViewModel, MeasurementViewModel and HealthProfileViewModel.

1. IProgressService/ProgressService: AddMeasurementAsync (BMI computed, never typed; Notes encrypted by the converter), GetLatestMeasurementAsync, GetWeightTrendAsync, GetBmiTrendAsync, GetCaloriesTrendAsync (weekly kcal burned, and kcal eaten against the diet target), GetVitalsTrendAsync (resting heart rate, systolic and diastolic BP), GetAdherenceAsync(clientId, from, to) (the 1.10 formula over the days the plans scheduled), SuggestCalorieTargetAsync(clientId) (latest weight and height, age, gender, activity level and goal through HealthCalculator). Trend methods return a ChartSeries view model (labels plus one or more datasets).

2. IDietService/DietService: CreatePlanAsync (assigned clients only; DailyCalorieTarget pre-filled from SuggestCalorieTargetAsync, 1,200–5,000), AddMealAsync, UpdatePlanAsync (removing a meal that already has logs is refused), ActivatePlanAsync (at least one meal; completes the previous active diet plan; PlanAssigned notification), GetTodaysMealsAsync(clientId, today) (each with logged or not), LogMealAsync (BR-13; active foods only; items through MealLog.AddItem; DietPlanMealId set when logging a planned meal), GetDailySummaryAsync(clientId, date) (eaten vs target, macros), GetHistoryAsync.

3. Client area:
   - DietController: MyPlan (targets and the meal schedule by day), LogMeal (GET with optional dietPlanMealId; food picker grouped by category with a search box; quantity rows added and removed by wwwroot/js/meal-log.js, which also shows a live kcal total), History (days with totals against target).
   - MeasurementsController: Index (table plus the latest BMI with a category badge), Create (height pre-filled from the last reading).
   - ProgressController: Index (range: 4 weeks, 3 months or custom), WeightData, CaloriesData, VitalsData (GET JSON from ProgressService; WeightData includes BMI and the target weight).
   - ProfileController.Edit: the health profile (goal, activity level, target weight, medical conditions, allergies, medications, emergency contact, ShareMedicalInfoWithTrainer). ExportMyData and DeleteAccount come in Phase 9.

4. Trainer area:
   - DietPlansController: Index, Create, Edit, AddMeal (POST), Activate (POST).
   - ClientsController.Progress(clientId): assigned clients only (anything else is 404); the same charts plus adherence, latest measurements and recent logs; the medical fields only when the client allows sharing.

5. Charts: Views/Shared/_ProgressCharts.cshtml and wwwroot/js/progress-charts.js serve both pages. A canvas with data-url fetches JSON (client page; changing the range re-fetches). A canvas with data-series reads JSON the server embedded (trainer page; changing the range reloads the page). Charts: weight with the target weight as a dashed reference line; BMI; weekly bars of kcal burned and kcal eaten against target; resting heart rate and blood pressure.

6. Tests: SuggestCalorieTargetAsync for each goal and the 1,200 floor; GetAdherenceAsync (3 of 4 scheduled days = 75%); LogMealAsync snapshots and refuses future dates, empty meals and inactive foods; BMI computed on save; a trainer can't open an unassigned client's progress.

Finish as CLAUDE.md says and commit "Phase 5: diet, measurements and progress".
```

**Check in the browser:**
- As trainer@: create a diet plan for client@ (the calorie target is pre-filled), add four meals, activate it.
- As client@: log a planned meal and a free snack; add two measurements; open Progress and switch ranges. Edit the health profile and turn medical sharing off.
- As trainer@: Clients → client@ → Progress shows the charts but not the medical fields. Change the clientId in the URL to a client who isn't yours → 404.

---

## Phase 6 – Availability and appointments

```text
Phase 6 – Availability and appointments (Task 6).

Read CLAUDE.md, then docs/design-spec.md sections 1.6 (with the lifecycle table), BR-15 to BR-18, UC13, UC14, UC23–UC26, UC38, 3.2 (the booking walkthrough) and 5.3.

1. Trainer availability: Trainer area AvailabilityController – Index, Create (AvailabilityViewModel: day, start, end; end after start; no overlap with another window that day), Delete (POST). The service methods (GetAvailabilityAsync, AddAvailabilityAsync, RemoveAvailabilityAsync) live in AppointmentService.

2. IAppointmentService/AppointmentService:
   - GetAvailableSlotsAsync(trainerId, clientId, date, minutes, excludeAppointmentId = null): starts every 15 minutes inside that weekday's windows, kept only if the appointment ends inside the window, starts 2 hours to 60 days ahead (BR-15) and overlaps no open appointment (Pending, Approved, Rescheduled) of the trainer or the client.
   - BookAsync(clientUserId, BookAppointmentViewModel): the professional is approved and Active; the slot rules are checked again inside the serializable transaction (the helper from Phase 1) before saving; status Pending; notify the trainer; audit Create. The client always comes from the signed-in user, never the form.
   - ApproveAsync(id, trainerUserId, location, rowVersion), RejectAsync(… reason), RescheduleAsync(… newStart, minutes) (the same slot checks, ignoring the appointment's own slot), CancelAsync(id, userId, reason) (client or trainer, mapped to the right entity method), CompleteAsync(… notes), MarkNoShowAsync, GetUpcomingAsync, GetForClientAsync, GetForTrainerAsync, GetDetailsAsync (only the client or the appointment's own professional).
   - Every change goes through the Appointment methods, sets UpdatedAt, sends the other party an AppointmentUpdate notification with a link (BR-18), and is audited.
   - Optimistic concurrency: forms post RowVersion (hidden, base64). The service first compares it with the stored value (a stale form gets the message straight away, before any transition rule runs), then sets it as the original value before saving, so a true race ends in DbUpdateConcurrencyException. Both give "This appointment was changed by someone else. Reload and try again."

3. Client area AppointmentsController: Index (upcoming and past with status badges), Book (choose a professional – approved, active trainers with type, specialisation and bio – then type, mode, date, length 30/45/60/90 and reason; wwwroot/js/appointment-book.js calls AvailableSlots and fills the time list), AvailableSlots (GET JSON), Cancel (POST with confirmation; optional reason; refused within 2 hours of the start).

4. Trainer area AppointmentsController: Index (pending requests first, then today and upcoming, then past), Approve (POST, optional room or meeting link), Reject (POST, reason required), Reschedule (GET shows a date picker and that day's free slots from GetAvailableSlotsAsync, and changing the date reloads the page; POST), Cancel (POST, reason required), Complete (POST, consultation notes), NoShow (POST). The encrypted ClientReason and ProfessionalNotes are shown only to the client and that professional.

5. Status badges use the lifecycle diagram's colours: Completed green, NoShow amber, Rejected and both cancellations red, open states blue or grey.

6. DbSeeder: weekday availability for trainer@ and nutritionist@ (09:00–12:00 and 14:00–17:00) if none exists.

7. Tests: slots respect the windows, the 2-hour and 60-day limits and existing bookings of both trainer and client; BookAsync refuses an overlap and an unapproved trainer; RescheduleAsync ignores the appointment's own slot; a client cancel within 2 hours is refused; Complete before the start is refused.

Finish as CLAUDE.md says and commit "Phase 6: appointments".
```

**Check in the browser:**
- As trainer@: Availability shows the seeded weekday windows; add a Saturday morning window. As client@: book trainer@ on the next weekday – only free times are offered. Start a second booking for the same day – the time you took is gone.
- As trainer@: approve with a meeting link, then reschedule it. As client@: the status and notifications changed each time.
- Open the same pending request as trainer@ in two tabs; approve in one, reject in the other → the "changed by someone else" message.
- In SQL Server Object Explorer, Appointment.ClientReason is unreadable ciphertext.

---

## Phase 7 – Notifications and reminders

```text
Phase 7 – Notifications and reminders (Task 8).

Read CLAUDE.md, then docs/design-spec.md sections 1.8, BR-19, UC06, UC34 and 5.3.

1. Complete NotificationService: GetUnreadAsync(userId, take), GetUnreadCountAsync, GetPageAsync(userId, page), MarkAsReadAsync(userId, notificationId) (own notifications only), MarkAllAsReadAsync, GetPreferencesAsync, SavePreferencesAsync (ranges from 2.8). NotifyAsync now also sends an email copy through IEmailService when the user's EmailEnabled is on and sets EmailSentAt; an email failure is logged and never breaks the action that caused it.

2. IEmailService/EmailService: System.Net.Mail SmtpClient configured from the "Smtp" section through an options class; when UsePickupDirectory is true it writes .eml files to App_Data/mail (created if missing).

3. IReminderService/ReminderService: GenerateDueRemindersAsync(userId, now) and GenerateAllDueRemindersAsync(now), following each user's preferences:
   - Workout: today is a scheduled day of the active plan, now is at or after WorkoutReminderTime, and nothing is logged today. Key WORKOUT-{clientId}-{yyyyMMdd}.
   - Meal: a planned meal of the active diet plan applies today, now is within MealReminderLeadMinutes before its ScheduledTime, and no meal for that plan meal (or of that meal type) is logged today. Key MEAL-{dietPlanMealId}-{yyyyMMdd}.
   - Appointment: Approved or Rescheduled, starts within AppointmentReminderLeadHours and hasn't started; one reminder for the client and one for the trainer. Key APPT-{appointmentId}-{C or T}-{start yyyyMMddHHmm}, so a rescheduled appointment gets a fresh reminder.
   Check the key before inserting and also catch the unique-index violation, so each reminder is created exactly once (BR-19). Reminders go through NotifyAsync, so email copies work too.

4. ReminderBackgroundService : BackgroundService: a PeriodicTimer from Reminders:IntervalSeconds, a new DI scope each tick, GenerateAllDueRemindersAsync(clock.Now), exceptions logged without stopping the loop, nothing at all when Reminders:Enabled is false. Register with AddHostedService.

5. NotificationsController (root): Index (paged, unread highlighted, mark as read), Poll (GET JSON: the unread count and the 5 newest unread items {id, title, message, linkUrl, createdAt}; it runs GenerateDueRemindersAsync for the current user first), MarkAsRead (POST, form or AJAX), MarkAllAsRead (POST), Preferences (GET/POST ReminderPreferencesViewModel).

6. wwwroot/js/healthtrack.js, only when the bell is on the page (signed-in users): poll on load and every 60 seconds with fetch and the X-Requested-With: XMLHttpRequest header (so an expired session answers 401 instead of a login page; stop polling then); update the badge; show a Bootstrap toast for each notification newer than the last one shown (remember the last id in sessionStorage inside try/catch); clicking a toast marks it read (POST with the RequestVerificationToken header, read from a token rendered once in the layout) and follows its link if it is local.

7. The three dashboards also call GenerateDueRemindersAsync for the signed-in user when they load (the Timer actor of UC34).

8. Tests: a second run creates no duplicates; the workout reminder appears only after the reminder time and only when nothing is logged; the meal reminder only inside the lead time; appointment reminders for both people; switched-off preferences create nothing.

Finish as CLAUDE.md says and commit "Phase 7: notifications and reminders".
```

**Check in the browser:**
- As client@: Notification settings → turn on email copies and set appointment reminders to 72 hours ahead. Book trainer@ for a slot in the next 3 days; as trainer@, approve it. Within a minute both people get an appointment reminder (bell count and toast), and App_Data/mail has an .eml file for client@.
- As trainer@: give client2@ an active workout plan with an exercise on today's weekday. As client2@: set the workout reminder time to a minute ago; within a minute a workout reminder appears, and reloading pages doesn't create a second one.
- Mark all as read; the badge clears.

---

## Phase 8 – Demo data, dashboards and reports

```text
Phase 8 – Demo data, the three dashboards and the three reports (Tasks 2, 3, 4 and 7).

Read CLAUDE.md, then docs/design-spec.md sections 1.4, 1.7, 1.10, UC07, UC17, UC27, UC28, UC40 and 5.3. First drop the development database (dotnet ef database drop --force) so the seeder starts clean.

1. Demo history in DbSeeder, only when there are no workout logs. Use Random with a fixed seed so every run is identical, dates relative to IClock.Today so the demo always looks current, and the entity methods (CreateCopyForClient, AddEntry, AddItem, the Appointment transitions with a suitable now) so every snapshot is right. Insert appointments directly – BookAsync's rules are about the present.
   - client@: "Beginner full body" assigned and active from 8 weeks ago with about 80% of sessions logged; an active diet plan with 4 meals a day, logged on most days; weekly measurements with weight falling about 0.4 kg a week, plus vitals; 4 pieces of feedback; appointments in every status (past Completed with notes, one NoShow, one CancelledByClient, one Rejected, an Approved one starting within 24 hours, one Pending).
   - client2@ to client4@: varied plans, goals and activity from their assigned trainers (client4@ gets a diet plan from nutritionist@); client3@ has logged nothing for 10+ days, so trainer@'s dashboard flags them.
   - client5@ and client6@ stay unassigned (the admin dashboard shows unassigned clients): only free workouts, meals and measurements, no plans.
   - AuditLog Login events spread over the last 14 days; some read and some unread notifications.
   SeedAsync order: libraries, users, templates, availability, history.

2. IDashboardService/DashboardService: GetClientDashboardAsync, GetTrainerDashboardAsync, GetAdminDashboardAsync – everything in the 1.4 table, AsNoTracking, one view model each (ClientDashboardViewModel, TrainerDashboardViewModel, AdminDashboardViewModel).

3. Replace the placeholder dashboards:
   - Client: stat cards (latest weight, BMI with category, change since the first measurement, kcal burned this week), today's workout with a Start button, today's meals with an eaten-vs-target progress bar, the next 3 appointments, the latest feedback, unread notifications.
   - Trainer: assigned clients (last activity, 7-day adherence %, weight change; inactive 7+ days flagged), pending appointment requests with Approve and Reject, today's schedule, quick actions (create plan, assign routine, give feedback). Also show adherence on Clients/Index.
   - Admin: users by role and status, new registrations this month, active users in the last 7 days, trainers awaiting approval, unassigned clients, trainer workload against capacity, logins per day for 14 days (line chart), appointments by status (bar chart), the 10 latest audit events.
   - Charts read JSON from data-series attributes through wwwroot/js/dashboard-charts.js.

4. IReportService/ReportService, each with a date range and an optional trainer scope: GetClientPerformanceAsync, GetPopularRoutinesAsync, GetEngagementAsync, GetTrainerWorkloadAsync – the columns listed in 1.7. Row classes in Models/ViewModels/Reports.

5. ReportsController in the Trainer area (ClientPerformance, PopularRoutines, Engagement, ExportCsv; scope = the trainer's assigned clients) and in the Admin area (the same plus TrainerWorkload; scope = everyone): a date-range form (default the last 30 days), the table, one chart, and Export CSV and Print buttons (wwwroot/js/reports.js). Helpers/CsvExporter: RFC 4180 quoting; a cell starting with =, +, - or @ gets a leading ' to stop spreadsheet formula injection; file name like client-performance-20261025.csv; audit Export. A print stylesheet (@media print) hides the navigation and buttons.

6. Tests: each report on a small InMemory dataset; the trainer scope leaves out other trainers' clients; CsvExporter quoting and the injection guard; dashboard counts.

Finish as CLAUDE.md says and commit "Phase 8: demo data, dashboards and reports".
```

**Check in the browser:**
- Log in as each demo account: every dashboard is full of believable data and its charts draw.
- As trainer@: one client is flagged inactive; the pending request can be approved from the dashboard.
- Run each report for trainer@ and admin@, export CSV (opens in Excel), and use Print preview.

---

## Phase 9 – Security hardening and privacy

```text
Phase 9 – Security hardening and privacy (the security criterion).

Read CLAUDE.md, then docs/design-spec.md sections 1.9, BR-20, BR-22, UC16 and 6.4 (security checks).

1. Security/SecurityHeadersMiddleware, registered after HTTPS redirection: Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'"; X-Frame-Options DENY; X-Content-Type-Options nosniff; Referrer-Policy strict-origin-when-cross-origin; Permissions-Policy "camera=(), microphone=(), geolocation=()". In Development only, also allow http://localhost:* and https://localhost:* in script-src and ws://localhost:* and wss://localhost:* in connect-src, so Visual Studio's Browser Link and hot reload keep working. ('unsafe-inline' for styles covers Bootstrap's style="width:…" progress bars; scripts stay strict.) Then go through every page and fix anything the policy blocks, such as inline scripts or onclick attributes.

2. Privacy in the Client area ProfileController: ExportMyData (POST; AccountService.ExportPersonalDataAsync builds a JSON file with the profile including the decrypted medical fields, measurements, plans, logs, appointments, feedback and notifications; audit Export) and DeleteAccount (GET explains exactly what happens; POST requires the password). AccountService.DeleteAccountAsync runs in one transaction and in dependency order, because most foreign keys are Restrict: delete feedback, workout logs, meal logs, plans, appointments, measurements, assignments, notifications and the client profile; anonymise the AspNetUsers row (name "Deleted user", email and username deleted-<id>@healthtrack.invalid, no phone, no password, Deactivated, locked out permanently, new security stamp); keep the AuditLog rows; sign the user out; audit Delete.

3. Medical sharing: wherever a trainer sees client data, the three medical fields appear only when ShareMedicalInfoWithTrainer is on. Admins never see clinical notes or measurement values.

4. Ownership review: write docs/security-review.md with one row per controller action – area, roles, how ownership is enforced (or why it isn't needed), whether it is a POST with anti-forgery, and the test or manual check that covers it. Fix every gap you find.

5. Check that everything in the CLAUDE.md audit list is actually audited.

6. Run the app once in the Production environment (outside the launch profile's Development setting) and confirm an error shows the friendly page without exception details. HSTS is never sent to localhost (ASP.NET Core excludes it on purpose), so don't try to see that header locally; just confirm UseHsts is configured outside Development.

7. Tests: each service returns nothing (or NotFound) for another client's ids; DeleteAccountAsync removes the health rows and anonymises the user; EncryptedStringConverter round-trips a value and stores ciphertext, not the plain text.

Finish as CLAUDE.md says and commit "Phase 9: security hardening and privacy".
```

**Check in the browser:**
- Browser dev tools → Network → any page: the security headers are there; the Console shows no CSP errors on any page.
- As client2@: change an appointment or plan id in a URL to one of client@'s → 404.
- Download my data gives a JSON file. Delete client6@'s account: the login no longer works; Admin → Users shows "Deleted user". Afterwards use the "Fresh demo data" prompt below to bring client6@ back.

---

## Phase 10 – Polish, testing and hand-in documents

```text
Phase 10 – Polish, testing and the hand-in documents.

Read CLAUDE.md, then docs/design-spec.md sections 1.12, 6.2 (marking scheme), 6.3, 6.4 and 6.5.

1. UI pass over every page for each role: consistent titles and headings, back links, helpful empty states, confirmation before destructive actions, a label on every input, a usable layout at 375 px wide, no console errors, a favicon.
2. Validation pass: every form field has the spec's ranges and clear messages, both in the browser and on the server. Try the boundaries: future dates, out-of-range weights, bookings less than 2 hours ahead.
3. docs/manual-tests.md: one row per use case UC01–UC40 – steps, demo account, expected result – with empty Actual, Pass and Screenshot columns for the team; then the security checks from 6.4.
4. docs/installation-guide.md: requirements (Visual Studio 2022 17.8+ or 2026 with the ASP.NET and web development workload, the .NET SDK, LocalDB), open the solution → F5 → the first run migrates and seeds, trusting the HTTPS certificate, the demo accounts table, resetting the database, and troubleshooting (LocalDB missing, port in use, locked bin files).
5. docs/user-manual.md: a section per role following the 10-minute demo path in 6.5, every step with a [Screenshot: …] placeholder.
6. README.md: what HealthTrack is, the stack, how to run it, the demo accounts, the project structure and how to run the tests.
7. Tidy the code: remove leftover template code, XML comments on every public member, no TODOs, run dotnet format, 0 warnings.
8. Final check from scratch: drop the database, start the app, confirm it migrates and seeds, run all tests, commit "Phase 10: polish and hand-in docs" and tag v1.0.
```

**Check in the browser:** walk the 10-minute demo path in spec 6.5 from start to finish, then fill in docs/manual-tests.md as a team, taking the screenshots for the user manual as you go.

---

## Handy prompts

**Something is broken**

```text
When I <what you did>, I get <the error or the wrong result>. Error details: <paste the message or stack trace>.
Find the root cause, fix it, add a test that would have caught it, then build and run the tests.
```

**Review a phase before moving on**

```text
Review the last commit against CLAUDE.md and the spec sections for that phase. List every broken rule, missing validation, missing ownership check, missing audit entry and missing test, then fix them.
```

**Prepare for the viva**

```text
Explain how <feature, e.g. booking an appointment> works end to end for my viva: the controller action, service method, entity methods and tables involved, the business rules it enforces, and the questions an examiner is likely to ask, with short answers. Don't change any code.
```

**Screenshots for the user manual**

```text
List the screens I need for the <Client / Trainer / Admin> section of the user manual in demo order, with the URL, the demo account to use, and what must be visible in each screenshot.
```

**Fresh demo data**

```text
Drop the development database and start the app so it migrates and reseeds, then confirm that every demo account in CLAUDE.md can log in.
```
