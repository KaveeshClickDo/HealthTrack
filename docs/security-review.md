# HealthTrack – security review

Phase 9 review of every controller action against the rules in `CLAUDE.md` (ownership, anti-forgery, roles) and spec 1.9.
Tests named here are in `HealthTrack.Tests`; manual checks are the browser checklists of `docs/build-prompts.md`.

## Controls that apply everywhere

| Control | Where | Effect |
| --- | --- | --- |
| Sign-in required | Fallback authorization policy (Program.cs) | Every action needs a signed-in user unless it says `[AllowAnonymous]` (Home, the Account login and register actions, Error) |
| One role per area | `[Area]` + `[Authorize(Roles = …)]` on `ClientAreaController`, `TrainerAreaController`, `AdminAreaController` | A user of another role gets 403 (the Forbidden page) for every action of the area |
| Anti-forgery | Global `AutoValidateAntiforgeryTokenAttribute`; AJAX sends the `RequestVerificationToken` header | Every POST without a valid token is rejected with 400; GETs never change data (see the notes below) |
| Ownership | Services resolve "my" data from the signed-in user (`CurrentUserId`, `CurrentClientIdAsync`); trainer-facing methods take the trainer's user ID and check `IsTrainerAssignedToClientAsync` (BR-08) | Someone else's id answers null/false, which the controller turns into 404 (never 403, so ids can't be probed) |
| Security headers | `SecurityHeadersMiddleware` | CSP (scripts only from the site), X-Frame-Options DENY, nosniff, strict referrer policy, Permissions-Policy |
| Cookies and sessions | Program.cs | HttpOnly, Secure, SameSite Lax, 30-minute sliding expiry that the notification poll never renews; security stamp checked every minute |
| Errors | `UseExceptionHandler` + `UseHsts` outside Development, status-code pages always | Friendly pages without exception details (checked in the Production environment, below) |
| Encryption at rest | `EncryptedStringConverter` on the six sensitive columns (BR-20) | `EncryptedStringConverterTests`, `ApplicationDbContextModelTests` |

"Owner" below means: the record is found through the signed-in user, or the service checks that it belongs to them; anything
else is 404. "AF" is the anti-forgery check (global filter) on a POST.

## Root controllers

| Action | Method | Roles | Ownership | AF | Covered by |
| --- | --- | --- | --- | --- | --- |
| Home/Index, Home/Privacy | GET | Anonymous | Not needed: public pages | – | Manual |
| Account/Register, RegisterClient, RegisterTrainer | GET | Anonymous | Not needed: empty forms | – | Manual |
| Account/RegisterClient, RegisterTrainer | POST | Anonymous | Not needed: creates a new account (BR-01–BR-03) | Yes | `AccountServiceTests` (Register…) |
| Account/Login | GET | Anonymous | Not needed | – | Manual |
| Account/Login | POST | Anonymous | Not needed; lockout after 5 failures; `returnUrl` followed only if `Url.IsLocalUrl` | Yes | `AccountServiceTests` (LoginAsync…) |
| Account/Logout | POST | Signed in | Own session only | Yes | `AccountServiceTests.LogoutAsync_SignedInUser_AuditsLogout` |
| Error/NotFound, Forbidden, ServerError | Any | Anonymous | Not needed: renders an error page only | Ignored on purpose (a re-executed POST keeps its method; the page changes nothing) | Manual (Production check below) |
| Manage/Index, EditProfile, ChangePassword | GET | Signed in | Owner (`CurrentUserId`) | – | Manual |
| Manage/EditProfile, ChangePassword | POST | Signed in | Owner (`CurrentUserId`); the current password is checked | Yes | `AccountServiceTests` (UpdateProfileAsync…, ChangePasswordAsync…) |
| Notifications/Index, Preferences | GET | Signed in | Owner (`CurrentUserId`) | – | `NotificationServiceTests` |
| Notifications/Poll | GET (JSON) | Signed in | Owner; creates only the user's own due reminders (idempotent, BR-19); 401 for AJAX when signed out | – | `NotificationServiceTests`, `ReminderServiceTests` |
| Notifications/MarkAsRead | POST (form or AJAX) | Signed in | Owner: someone else's notification is 404 | Yes (header for AJAX) | `OwnershipTests.NotificationService_OtherUsersNotification_ReturnsFalse` |
| Notifications/MarkAllAsRead, Preferences | POST | Signed in | Owner (`CurrentUserId`) | Yes | `NotificationServiceTests` |

## Client area (role Client)

| Action | Method | Ownership | AF | Covered by |
| --- | --- | --- | --- | --- |
| Dashboard/Index | GET | Owner (`CurrentUserId`); also creates the user's own due reminders | – | `DashboardServiceTests` |
| Profile/Edit | GET, POST | Owner (`CurrentUserId`); medical fields encrypted | POST: Yes | `AccountServiceHealthProfileTests` |
| Profile/ExportMyData | POST | Owner (`CurrentUserId`); audited (UC16) | Yes | `AccountServicePrivacyTests.ExportPersonalDataAsync…` |
| Profile/DeleteAccount | GET | Owner; explains what happens | – | Manual |
| Profile/DeleteAccount | POST | Owner; password required, with lockout; one transaction (BR-22) | Yes | `AccountServicePrivacyTests.DeleteAccountAsync…` |
| Workouts/MyPlan, Today, History | GET | Owner (`CurrentClientIdAsync`) | – | `WorkoutServiceTests` |
| Workouts/Log | GET | Owner | – | `WorkoutServiceTests` |
| Workouts/Log | POST | Owner; a plan id must be the client's own | Yes | `OwnershipTests.WorkoutService_LogAgainstOtherClientsPlan_ReturnsNull` |
| Diet/MyPlan, History | GET | Owner | – | `DietServiceTests` |
| Diet/LogMeal | GET | Owner; a planned meal must be in the client's own plan, else 404 | – | `OwnershipTests.DietService_OtherClientsPlannedMeal_NothingForTheClient` |
| Diet/LogMeal | POST | Owner; same planned-meal check | Yes | `OwnershipTests.DietService_OtherClientsPlannedMeal_NothingForTheClient` |
| Measurements/Index, Create | GET | Owner | – | `ProgressServiceTests` |
| Measurements/Create | POST | Owner; no future dates (BR-13) | Yes | `ProgressServiceTests` |
| Progress/Index, WeightData, CaloriesData, VitalsData | GET (page, JSON) | Owner; the range is clamped (no date overflow) | – | `ProgressServiceTests`, `ProgressRangeViewModelTests` |
| Feedback/Index | GET | Owner | – | `FeedbackServiceTests` |
| Appointments/Index | GET | Owner | – | `AppointmentServiceTests` |
| Appointments/Details | GET | Owner: another client's appointment is 404 (the professional's view is refused too) | – | `OwnershipTests.AppointmentService_OtherClientsAppointment_NothingForTheClient` |
| Appointments/Book | GET | Owner; any approved, active professional may be chosen (public) | – | `AppointmentServiceTests` |
| Appointments/Book | POST | The client always comes from the signed-in user, never the form (spec 3.2); slot re-checked in a serializable transaction (BR-15) | Yes | `AppointmentServiceTests` (BookAsync…) |
| Appointments/AvailableSlots | GET (JSON) | Owner (the client's own bookings count); only bookable professionals; no slots outside the booking window | – | `AppointmentServiceTests.GetAvailableSlotsAsync…` |
| Appointments/Cancel | POST | Owner: another client's appointment is 404; RowVersion check | Yes | `OwnershipTests.AppointmentService_OtherClientsAppointment_NothingForTheClient` |

## Trainer area (role Trainer)

| Action | Method | Ownership | AF | Covered by |
| --- | --- | --- | --- | --- |
| Dashboard/Index | GET | Own clients (active assignments) and own appointments | – | `DashboardServiceTests` |
| Clients/Index | GET | Own clients only (BR-08) | – | `DashboardServiceTests` (same query) |
| Clients/Progress | GET | Assigned client only, else 404; medical fields and measurement notes only when the client shares them | – | `OwnershipTests.ProgressService_UnassignedClient_ReturnsNull`, `ProgressServiceTests` |
| WorkoutPlans/Index, Templates | GET | Own plans; templates are a shared library by design | – | `WorkoutServiceTests` |
| WorkoutPlans/Create | GET | A client id in the URL must be assigned, else 404 (fixed in Phase 9) | – | `OwnershipTests.WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| WorkoutPlans/Create | POST | The client must be assigned (BR-08) | Yes | `WorkoutServiceTests` |
| WorkoutPlans/Edit | GET, POST | Own plan whose client is still assigned, else 404 | POST: Yes | `OwnershipTests.WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| WorkoutPlans/AddExercise, SaveAsTemplate, Activate, Archive | POST | Own plan, else 404 | Yes | `OwnershipTests.WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| WorkoutPlans/AssignTemplate | GET, POST | Any active template, but only for an assigned client, else 404 | POST: Yes | `OwnershipTests.WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| DietPlans/Index | GET | Own plans | – | `DietServiceTests` |
| DietPlans/Create | GET, POST | The client must be assigned, else 404 | POST: Yes | `OwnershipTests.DietService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| DietPlans/Edit | GET, POST | Own plan whose client is still assigned, else 404 | POST: Yes | `OwnershipTests.DietService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| DietPlans/AddMeal, Activate | POST | Own plan, else 404 | Yes | `OwnershipTests.DietService_OtherTrainersPlanOrClient_NothingForTheTrainer` |
| Feedback/Create | GET, POST | The client must be assigned and the session must be theirs, else 404 | POST: Yes | `OwnershipTests.FeedbackService_OtherTrainersClientOrSession_ReturnsNull` |
| Appointments/Index | GET | Own appointments only | – | `AppointmentServiceTests` |
| Appointments/Details | GET | Own appointment only, else 404 (BR-08) | – | `OwnershipTests.AppointmentService_OtherProfessionalsAppointment_NothingForTheTrainer` |
| Appointments/Approve, Reject, Cancel, Complete, NoShow | POST | Own appointment only, else 404; RowVersion check; `ReturnUrl` followed only if local | Yes | `OwnershipTests.AppointmentService_OtherProfessionalsAppointment_NothingForTheTrainer`, `AppointmentServiceTests` |
| Appointments/Reschedule | GET, POST | Own appointment only, else 404; slot re-checked (BR-15) | POST: Yes | `OwnershipTests.AppointmentService_OtherProfessionalsAppointment_NothingForTheTrainer`, `AppointmentServiceTests` |
| Availability/Index | GET | Own windows | – | `AppointmentServiceTests` |
| Availability/Create | POST | Always the signed-in trainer's own windows | Yes | `AppointmentServiceTests.AddAvailabilityAsync…` |
| Availability/Delete | POST | Own window only, else 404 | Yes | `AppointmentServiceTests.RemoveAvailabilityAsync_OtherTrainersWindow_ReturnsFalse` |
| Reports/ClientPerformance, PopularRoutines, Engagement | GET | Scope = the trainer's assigned clients and own appointments | – | `ReportServiceTests` (…TrainerScope…) |
| Reports/ExportCsv | GET (download) | Same scope; trainer workload refused (404); audited | – | `ReportServiceTests.ExportCsvAsync…` |

## Admin area (role Admin)

Administrators manage every account, so these actions need no per-record ownership; the area's role check is the control.
They never see clinical notes or measurement values: user details show totals only, and the client performance report
leaves out weight and BMI (fixed in Phase 9).

| Action | Method | Notes | AF | Covered by |
| --- | --- | --- | --- | --- |
| Dashboard/Index | GET | Totals and charts only | – | `DashboardServiceTests` |
| Users/Index, Details | GET | Totals only, no health values; a deleted account shows no personal details | – | `AccountServiceAdminTests` |
| Users/CreateAdmin | GET, POST | BR-04: admins are created by an admin | POST: Yes | `AccountServiceAdminTests.CreateAdminAsync…` |
| Users/Suspend, Reactivate | POST | BR-04: not yourself, not the last active admin; new security stamp (BR-05) | Yes | `AccountServiceAdminTests.SetAccountStatusAsync…` |
| TrainerApprovals/Index | GET | – | – | `AccountServiceAdminTests.GetPendingTrainersAsync…` |
| TrainerApprovals/Approve, Reject | POST | Only a pending registration (BR-03) | Yes | `AccountServiceAdminTests` (Approve…, Reject…) |
| Assignments/Index, Assign | GET | – | – | `AssignmentServiceTests` |
| Assignments/Assign, End | POST | BR-06, BR-07 | Yes | `AssignmentServiceTests` |
| Library/Exercises, Foods, EditExercise, EditFood | GET | – | – | `LibraryServiceTests` |
| Library/EditExercise, EditFood, ToggleActive | POST | Deactivated, never deleted (BR-21); `returnUrl` followed only if local | Yes | `LibraryServiceTests` |
| AuditLog/Index | GET | Filters; a crafted end date no longer overflows (fixed in Phase 9) | – | `AuditServiceTests` |
| Reports/ClientPerformance, PopularRoutines, Engagement, TrainerWorkload | GET | Everyone; no measurement values | – | `ReportServiceTests` |
| Reports/ExportCsv | GET (download) | Audited; no measurement columns | – | `ReportServiceTests.ExportCsvAsync…` |

## Notes on GET actions with a side effect

- **Notifications/Poll and the three dashboards** create the signed-in user's own due reminders. That is idempotent (the
  unique ReminderKey, BR-19) and touches only the user's own data, so a forged request can at most create a reminder that
  was due anyway.
- **Reports/ExportCsv** adds an audit row; it changes no data, and a forged request can't read the file it returns.
- **Profile/ExportMyData** is a POST on purpose: the file holds decrypted medical details, so it needs the anti-forgery token.

## Gaps found and fixed in Phase 9

| Gap | Fix | Test |
| --- | --- | --- |
| A crafted date (9999-12-31) in `Appointments/AvailableSlots` or the reschedule page overflowed `DateTime` and gave a 500 | `GetAvailableSlotsAsync` answers no slots outside today … 60 days ahead (BR-15) before any date arithmetic | `AppointmentServiceTests.GetAvailableSlotsAsync_OutsideBookingWindow_NoSlotsAndNoError` |
| The same with a report's end date, and with year-1 dates in a report or a custom progress range | `ReportRangeViewModel` and `ProgressRangeViewModel` clamp both dates to the last ten years up to today | `ReportServiceTests.ReportRangeViewModel_Resolve_ClampsToTheLastTenYears`, `ProgressRangeViewModelTests.Resolve_CustomExtremeDates_ClampedWithoutError` |
| The same with the audit log's "to" filter | The last representable day means "no end" | `AuditServiceTests.GetPageAsync_LastPossibleToDate_NoError` |
| The admin's client performance report and its CSV showed every client's weight and BMI | Left out for administrators (spec 1.9); trainers still see their own clients' figures | `ReportServiceTests.GetClientPerformanceAsync_SmallDataset_ComputesEachColumn`, `ReportServiceTests.ExportCsvAsync_ClientPerformanceForAdmin_LeavesOutMeasurements` |
| `WorkoutPlans/Create?clientId=` accepted an unassigned client (no data shown, the POST was refused) | 404, like the diet plan form | `OwnershipTests.WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer` |

## Audit list check (CLAUDE.md)

| Event | Audited by |
| --- | --- |
| Logins, failed logins, lockouts | `AccountService.LoginAsync` (Login, LoginFailed, AccountLocked) |
| Logouts | `AccountService.LogoutAsync` |
| Registrations | `AccountService.RegisterClientAsync`, `RegisterTrainerAsync`, `CreateAdminAsync` |
| Approvals | `AccountService.ApproveTrainerAsync` / `RejectTrainerAsync`; `AppointmentService.ApproveAsync` / `RejectAsync` |
| Assignments | `AssignmentService.AssignTrainerAsync`, `EndAssignmentAsync` |
| Plan changes | `WorkoutService` (create, update, add exercise, archive, save as template, assign template, activate); `DietService` (create, update, add meal, activate) |
| Appointment changes | `AppointmentService` (book, approve, reject, reschedule, cancel, complete, no-show; availability added and removed) |
| Exports | `ReportService.ExportCsvAsync`; `AccountService.ExportPersonalDataAsync` |
| Deletions | `AccountService.DeleteAccountAsync`; availability removal |

No audit row holds a password or a health value.

## Production environment check

Run with `ASPNETCORE_ENVIRONMENT=Production` outside the launch profile:

- A request that threw (a year-1 custom progress range, before its fix) answered 500 with the friendly "Something went
  wrong" page and no exception text; the error was logged on the server.
- An unknown URL gave the friendly 404 page.
- Every response, error pages included, carried the security headers, with no localhost sources in the policy.
- HSTS is configured with `UseHsts()` outside Development. ASP.NET Core never sends it to localhost, so it can't be seen
  locally.
