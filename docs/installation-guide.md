# HealthTrack – installation guide

HealthTrack runs on your own Windows computer with Visual Studio and SQL Server Express LocalDB. The first start creates
the database and fills it with demo data, so you only press F5.

## 1. Requirements

| What | Version | Notes |
| --- | --- | --- |
| Windows | 10 or 11 | LocalDB and the Data Protection key encryption (DPAPI) are Windows features |
| Visual Studio | 2026, with the **ASP.NET and web development** workload | The workload installs the .NET 10 SDK and SQL Server Express LocalDB. The project targets .NET 10, which Visual Studio 2022 can't build |
| .NET SDK | 10.0 | Installed with the workload; check with `dotnet --list-sdks` |
| SQL Server Express LocalDB | 2019 or later | Installed with the workload; check with `sqllocaldb info` (it should list `MSSQLLocalDB`) |
| Internet | First build only | NuGet packages are restored once. The running app needs no internet: Bootstrap, jQuery and Chart.js are in `wwwroot/lib` |

Without Visual Studio you can use the .NET 10 SDK and LocalDB on their own, with any editor (section 4).

## 2. Get the code

Clone the repository or unzip the hand-in, then open the solution folder, the one that contains `HealthTrack.slnx`:

```
HealthTrack/
  HealthTrack.slnx
  HealthTrack/          the web application
  HealthTrack.Tests/    the unit tests
  docs/                 design spec, manual tests, this guide, user manual
```

## 3. First run in Visual Studio

1. Open `HealthTrack.slnx`.
2. Make sure **HealthTrack** is the startup project and the run profile next to the green arrow is **https**.
3. Press **F5**. The first build restores the NuGet packages.
4. Visual Studio asks to trust the ASP.NET Core HTTPS development certificate. Choose **Yes** in both dialogs (section 5).
5. On the first start the app:
   - creates the `HealthTrackDb` database on `(localdb)\MSSQLLocalDB` and applies the EF Core migrations;
   - seeds the exercise and food libraries (40 exercises, 57 foods), the ten demo accounts, three routine templates,
     the trainers' availability and eight weeks of demo history (plans, logs, measurements, appointments, feedback).

   This takes 10–20 seconds. Then the browser opens `https://localhost:7144`.
6. Sign in with one of the demo accounts below.

Every later start finds the database and the data already there and starts straight away.

## 4. Running from the command line

From the solution folder:

```
dotnet tool restore
dotnet run --project HealthTrack --launch-profile https
```

Then open `https://localhost:7144`. Stop the app with Ctrl+C.

## 5. Trusting the HTTPS certificate

The app only runs over HTTPS. If the browser warns that the connection isn't private, trust the development certificate
once and restart the browser:

```
dotnet dev-certs https --trust
```

If the warning stays, recreate it: `dotnet dev-certs https --clean`, then `dotnet dev-certs https --trust`.

## 6. Demo accounts

| Role | Email | Password | What to show with it |
| --- | --- | --- | --- |
| Administrator | admin@healthtrack.test | Admin@123 | Dashboard, users, trainer approvals, assignments, libraries, reports, audit log |
| Trainer | trainer@healthtrack.test | Trainer@123 | Fitness trainer coaching client@, client2@ and client3@ (client3@ is flagged inactive) |
| Trainer | nutritionist@healthtrack.test | Trainer@123 | Nutritionist coaching client4@ |
| Trainer | physio@healthtrack.test | Trainer@123 | Physiotherapist **waiting for approval**: can't sign in until an administrator approves them |
| Client | client@healthtrack.test | Client@123 | The main demo client: 8 weeks of workouts, meals and weigh-ins, and appointments in every status |
| Client | client2@ … client4@healthtrack.test | Client@123 | Assigned clients with different goals |
| Client | client5@, client6@healthtrack.test | Client@123 | Not assigned to a trainer yet (to demo assignments) |

The demo history is dated relative to the day the database is seeded, so it always ends "yesterday". Reset the database
before a demo or a viva (section 7) so the dashboards look current.

## 7. Resetting the database

To start again with fresh demo data:

1. Stop the app (Shift+F5 in Visual Studio).
2. Drop the database, from the solution folder:

   ```
   dotnet ef database drop --force --project HealthTrack
   ```

   Or, in Visual Studio: **View → SQL Server Object Explorer → (localdb)\MSSQLLocalDB → Databases → HealthTrackDb →
   Delete**, ticking **Close existing connections**.
3. Press F5. The app recreates the database and reseeds it.

## 8. Where things are kept

| Item | Location | Notes |
| --- | --- | --- |
| Database | `HealthTrackDb` on `(localdb)\MSSQLLocalDB` | Connection string in `HealthTrack/appsettings.json` |
| Encryption keys | `HealthTrack/App_Data/keys` | ASP.NET Core Data Protection keys. They encrypt the medical details, measurement notes, appointment reasons and consultation notes, and the sign-in cookie. If they are deleted, those fields can't be read any more: reset the database too |
| Email copies | `HealthTrack/App_Data/mail` | In development, email copies of notifications are written here as `.eml` files (open them in Outlook or a text editor). For real email, set `Smtp:Host`, `Port`, `UserName` and `UsePickupDirectory: false` in `appsettings.json` and keep the password in user secrets |
| Reminders | `Reminders` in `appsettings.json` | `Enabled` and `IntervalSeconds` (60) for the background reminder and email service |

`App_Data` is not in Git: each computer makes its own keys and mail folder.

## 9. Running the tests

In Visual Studio: **Test → Run All Tests** (Test Explorer). From the command line, in the solution folder:

```
dotnet test
```

The 551 unit tests use an in-memory database, so they need neither LocalDB nor the app running.

## 10. Troubleshooting

| Problem | What you see | Fix |
| --- | --- | --- |
| LocalDB is missing or stopped | "A network-related or instance-specific error occurred…" or "Cannot open database HealthTrackDb" at start-up | Run `sqllocaldb info`. If `MSSQLLocalDB` isn't listed, install **SQL Server Express LocalDB** (Visual Studio Installer → Modify → Individual components) or run `sqllocaldb create MSSQLLocalDB`. If it is listed, run `sqllocaldb start MSSQLLocalDB` |
| Port already in use | "Failed to bind to address https://127.0.0.1:7144: address already in use" | Another copy of the app is still running: stop it (Shift+F5, or end `dotnet.exe` in Task Manager). Or change the ports in `HealthTrack/Properties/launchSettings.json` |
| Locked files in `bin` | The build fails with "The process cannot access the file … because it is being used by another process" | The app is still running from an earlier start. Stop debugging (Shift+F5), then build again |
| Blocked program | "An Application Control policy has blocked this file" (Windows Smart App Control) | The app always runs through the signed `dotnet.exe` (`UseAppHost` is off), so this shouldn't happen. If it does, run it with `dotnet run` as in section 4 |
| Browser certificate warning | "Your connection isn't private" | Section 5 |
| Medical details or notes show blank | Encrypted fields read as empty | The keys in `App_Data/keys` were deleted or come from another computer. Reset the database (section 7) |
| "Your account is locked" | After 5 wrong passwords | Wait 15 minutes, or reset the database |
| Demo data looks old | Dashboards show nothing for the last few days | Reset the database (section 7) |
