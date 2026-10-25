# HealthTrack – manual test cases

One test case per use case (UC01–UC40, spec 4.2), then the security and boundary checks of spec 6.4. Run them on fresh
demo data (installation guide, section 7) with the app started from Visual Studio. Passwords: `Admin@123` for the
administrator, `Trainer@123` for trainers, `Client@123` for clients.

Fill in **Actual** and **Pass** (Yes/No) as you go, and save each screenshot as `screenshots/UCnn.png` (put its file name in
the **Screenshot** column). The same screenshots serve the user manual.

## Use cases

| ID | Use case | Account | Steps | Expected result | Actual | Pass | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| UC01 | Register as Client | None (signed out) | Register → Register as a client. Fill in every field (age 16+, height 170, weight 70), tick the privacy notice → Create my account | Signed in on the client dashboard with "Welcome to HealthTrack…"; the first weight shows on the dashboard; Notification settings show the defaults (workout 07:00, meals 15 min, appointments 24 h) | | | |
| UC02 | Register as Trainer | None | Register → Register as a professional. Choose Physiotherapist, leave the licence number empty → Send my registration; then enter a licence number → Send my registration | Without a licence: "Enter your licence number. Nutritionists, physiotherapists and medical doctors need one." next to the field. With it: back on the login page with "waiting for an administrator's approval"; signing in shows "Your trainer account is waiting for an administrator's approval." | | | |
| UC03 | Log In | client@ | Log in with a wrong password, then the right one | Wrong: "Invalid email or password." Right: the client dashboard. As admin@ and trainer@ you land on their own dashboards | | | |
| UC04 | Log Out | client@ | User menu → Log out; then open `/Client/Dashboard` | "You have been logged out."; the dashboard URL sends you to the login page | | | |
| UC05 | Manage Profile and Password | client@ | User menu → Manage account → Edit details, change the phone number → Save. Change password with a wrong current password, then correctly | Details saved with a success message. Wrong current password is refused next to the field; the right one changes it (log in again with the new password) | | | |
| UC06 | Manage Notifications | client@ | Bell → Notifications: Mark as read on one, then Mark all as read. User menu → Notification settings: set appointment reminders to 72 hours → Save | The badge count drops, then clears; read items lose their highlight. Settings saved with a message and shown again on reload | | | |
| UC07 | View Client Dashboard | client@ | Open the dashboard | Latest weight, BMI with category, change since the first weigh-in, kcal burned this week, today's workout (or rest day), today's meals with a progress bar, next appointments, latest feedback, unread notifications | | | |
| UC08 | View My Plans | client@ | My plans → Workout plan; My plans → Diet plan | "Beginner full body" by weekday (Monday, Wednesday, Friday) with sets, reps and minutes; the diet plan's four meals with targets | | | |
| UC09 | Log Workout | client@ | Log → Today's workout (on a Mon/Wed/Fri) or My plans → Workout plan → Log a free workout. Keep the exercises, change minutes → Save workout | The session appears in Log → Workout history with kcal per exercise; the dashboard's "Burned this week" goes up | | | |
| UC10 | Log Meal | client@ | Log → Log a meal. Choose Lunch and the planned lunch, add two foods with servings → Save meal | Saved; Diet → History shows the meal with kcal and macros; today's meals on the dashboard show it as logged | | | |
| UC11 | Record Health Measurement | client@ | Log → Add a measurement: weight 68.0, height 165, heart rate 66, blood pressure 118/76 → Save measurement | Listed under Progress → Measurements with BMI and category worked out; the dashboard shows the new weight | | | |
| UC12 | View Progress Trends | client@ | Progress → Charts; switch between 4 weeks, 3 months and Custom | Weight (with the target as a dashed line), BMI, calories per week, heart rate and blood pressure charts redraw; "Show the data as a table" lists the numbers | | | |
| UC13 | Book Appointment | client@ | Appointments → Book an appointment. Choose Chris Bandara, Personal training, In person, a weekday next week, 60 minutes, pick a free time → Send the request | Listed as Pending under Appointments; trainer@ gets a "New appointment request" notification. Times already taken are not offered | | | |
| UC14 | Cancel Appointment | client@, trainer@ | Client: Appointments → the pending request → Cancel appointment (confirm). Trainer: open an approved upcoming appointment → Cancel appointment without a reason, then with one | Client cancel: status Cancelled by client, trainer notified. Trainer without a reason: refused with a message. With a reason: Cancelled by trainer, client notified | | | |
| UC15 | View Feedback | client@ | Feedback | Four pieces of feedback from Chris Bandara, newest first, with category and rating | | | |
| UC16 | Export or Delete My Data | client@, client6@ | client@: User menu → Health profile → Download my data. client6@: Health profile → Delete my account, enter the password → Delete my account (confirm) | A `healthtrack-my-data-….json` file with the profile, measurements, logs and appointments. client6@ is signed out with a confirmation; logging in as client6@ fails; Admin → Users lists "Deleted user". Reset the database afterwards | | | |
| UC17 | View Trainer Dashboard | trainer@ | Open the dashboard | Clients with last activity, 7-day adherence and weight change; Dilini Silva flagged "Inactive … days"; the pending request from Amaya Perera with Approve and Reject; today's schedule; quick actions | | | |
| UC18 | Monitor Client Progress | trainer@ | Clients → Amaya Perera → Progress; then try Ravi Fernando (who doesn't share medical details) | Charts, adherence, recent measurements and logs. Amaya's allergies are shown; Ravi's medical details are hidden ("not shared") | | | |
| UC19 | Create Workout Plan | trainer@ | Workout plans → New client plan (or Clients → New plan): choose a client, title, start date → Create and add exercises; add two exercises on Monday → Activate (confirm) | Draft plan with the exercises by day; after Activate it is Active and the client's previous plan is Completed; the client is notified | | | |
| UC20 | Assign Routine Template | trainer@ | After UC31: Templates → "5K runner" → Assign to a client → Priya Nair (client5@), start today, activate → Assign | A copy appears in Workout plans for that client; the template itself is unchanged | | | |
| UC21 | Create Diet Plan | trainer@ | Diet plans → new plan for a client: 1,800 kcal, macros → Create and add meals; add Breakfast and Lunch → Activate | The plan with its meal schedule is Active; the client sees it under My plans → Diet plan and gets a notification | | | |
| UC22 | Give Feedback | trainer@ | Clients → Give feedback for Amaya Perera, pick a recent session, category Workout, rating 4, a message → Send feedback | Saved; client@ gets "New feedback from Chris Bandara" and sees it under Feedback | | | |
| UC23 | Manage Availability | trainer@ | Availability → Add a window: Saturday 09:00–12:00 → Add window; try 10:00–11:00 on Saturday again; Remove it (confirm) | Added and listed; the overlapping window is refused with a message; removing asks first and then removes it | | | |
| UC24 | Approve or Reject Appointment | trainer@ | Dashboard → the pending request → Approve. Book another as client@, then Reject it with no reason, then with a reason | Approved (client notified, status Approved). Reject without a reason is refused; with a reason: Rejected and the client sees the reason | | | |
| UC25 | Reschedule Appointment | trainer@ | Appointments → an approved upcoming appointment → Reschedule; pick another day, choose a free time → Move the appointment | Status Rescheduled with the new time; the client is notified; only free times are offered | | | |
| UC26 | Complete Appointment | trainer@ | Open an approved appointment whose start time has passed (for example Ravi Fernando's session today at 15:00, after 15:00) → Mark as completed with notes; on another one, Client didn't attend | Completed with the notes shown to the client; the other shows No-show. Before the start time these buttons aren't offered | | | |
| UC27 | Generate Reports | trainer@, admin@ | Reports → Client performance, Popular routines, Engagement (admin also Trainer workload); change the dates → Show; Print | Each report shows a table and a chart for the period; trainer@ sees only their clients; admin@'s client performance has no weight or BMI columns. Print preview hides the navigation and buttons | | | |
| UC28 | View Admin Dashboard | admin@ | Open the dashboard | Users by role and status, new this month, signed in in the last 7 days, 1 trainer awaiting approval, unassigned clients (Priya Nair, Alex Morgan), trainer workload, logins-per-day and appointments-by-status charts, latest audit events | | | |
| UC29 | Manage Users | admin@ | Users: search "client", filter by role. Open client2@ → Suspend account (confirm); try logging in as client2@; Reactivate account. Create administrator | The list filters; a suspended user can't sign in ("This account is not active…") and is signed out within a minute if already signed in; reactivated users can sign in; the new administrator can sign in | | | |
| UC30 | Approve Trainer Registration | admin@ | Trainer approvals → Daniel Perera (physio@) → Approve | physio@ can now sign in and gets an approval notification; the dashboard's "Awaiting approval" drops to 0 | | | |
| UC31 | Assign Trainer to Client | admin@ | Assignments → Assign a trainer: client5@ (Priya Nair) → Chris Bandara → Assign. Then End an assignment (confirm) | client5@ appears in trainer@'s clients and both are notified; the ended assignment shows its end date and the client is unassigned again | | | |
| UC32 | Manage Exercise and Food Library | admin@ | Library → Exercises → Add exercise (MET 6.0) → save; Deactivate it (confirm). Library → Foods → Add food | The new exercise is listed and offered in plan and log pickers; once deactivated it is hidden from pickers but kept in history | | | |
| UC33 | View Audit Log | admin@ | Audit log: filter by action "Login" and a date range; then by email | Matching events, newest first, 20 a page; no passwords or health values anywhere | | | |
| UC34 | Send Reminders | client@, client2@ | client@: with the app running, wait a minute on any page. client2@: set the workout reminder time to a minute ago on a training day, then wait | client@ gets "Appointment coming up" for the approved appointment within 24 hours (bell count and a toast). client2@ gets a workout reminder once; reloading doesn't create another | | | |
| UC35 | Calculate Calories Burned | client@ | Log a workout: Brisk walking (MET 4.3), 30 minutes | kcal = 4.3 × your latest weight × 0.5, shown on the history row (for example 68 kg → 146 kcal) | | | |
| UC36 | Calculate Nutrition | client@ | Log a meal: Rolled oats × 2 servings (152 kcal each) | The meal shows 304 kcal, with protein, carbohydrate and fat × 2 | | | |
| UC37 | Calculate BMI | client@ | Add a measurement: weight 68.0, height 165 | BMI 25.0, category Overweight; with weight 67.0, BMI 24.6 Normal | | | |
| UC38 | Check Availability | client@ | Book trainer@ for a day with an existing booking; choose a weekend day; choose today | Times that overlap an open appointment (the trainer's or yours) aren't offered; a day without windows offers none; today offers nothing earlier than 2 hours from now | | | |
| UC39 | Save as Routine Template | trainer@ | Workout plans → open a client plan → Save as template | A new template with the same exercises appears under Templates; the client plan is unchanged | | | |
| UC40 | Export Report to CSV | trainer@, admin@ | On each report → Export CSV; open the file in Excel | A file like `client-performance-20261025.csv` opens with the report's columns; names with commas stay in one cell; the export appears in the audit log | | | |

## Security checks (spec 6.4)

| Check | Account | Steps | Expected result | Actual | Pass | Screenshot |
| --- | --- | --- | --- | --- | --- | --- |
| Another client's ID in a URL | client2@ | Sign in as client@, open an appointment and note its number in the URL (`/Client/Appointments/Details/7`). Sign in as client2@ and open the same URL; also try `/Client/Diet/LogMeal?dietPlanMealId=1` | "We couldn't find that page" (404) both times, never client@'s data | | | |
| Another trainer's client | nutritionist@ | Open `/Trainer/Clients/Progress?clientId=1` (client@, who isn't theirs) and `/Trainer/WorkoutPlans/Create?clientId=1` | 404 | | | |
| Wrong role | client@ | Open `/Admin/Users` and `/Trainer/Dashboard` | The "access denied" page (403) | | | |
| POST without the anti-forgery token | client@ | In the browser's dev tools, delete the hidden `__RequestVerificationToken` input from the Add a measurement form, then submit | The "We couldn't accept that request" page (400); nothing saved | | | |
| Lockout after 5 wrong passwords | client3@ | Log in 5 times with a wrong password, then with the right one | The fifth attempt and the right password both say "Too many failed attempts. Try again in 15 minutes."; Audit log shows LoginFailed rows and AccountLocked | | | |
| Suspended user signed out | admin@, client2@ | Sign in as client2@ in one browser; as admin@ in another, suspend client2@; wait a minute and click any page as client2@ | client2@ is sent to the login page and can't sign in | | | |
| Encrypted columns | – | View → SQL Server Object Explorer → HealthTrackDb → Tables → dbo.ClientProfile → View Data (also HealthMeasurement.Notes, Appointment.ClientReason and ProfessionalNotes) | MedicalConditions, Allergies, CurrentMedications and the notes are unreadable ciphertext; weights and other numbers are plain | | | |
| Security headers | any | Dev tools → Network → reload any page → the document's response headers; then the Console | Content-Security-Policy, X-Frame-Options DENY, X-Content-Type-Options nosniff, Referrer-Policy, Permissions-Policy; no CSP errors in the Console | | | |
| Session idle time-out | client@ | Sign in and leave the tab open, without clicking, for 31 minutes | The next click goes to the login page (the bell's polling doesn't keep the session alive) | | | |

## Boundary checks (spec 6.4)

| Check | Account | Steps | Expected result | Actual | Pass | Screenshot |
| --- | --- | --- | --- | --- | --- | --- |
| Future dates | client@ | Add a measurement dated tomorrow; log a workout dated tomorrow | Refused next to the date field ("… can't be in the future.") in the browser and, with scripts off, on the server | | | |
| Out-of-range weight | client@ | Add a measurement with weight 19 kg, then 351 kg | "Weight must be between 20 and 350 kg." next to the field; 20 and 350 are accepted | | | |
| Overlapping bookings | client@, client2@ | client@ books trainer@ for a time; client2@ opens the same day | The taken time isn't offered to client2@ | | | |
| Bookings less than 2 hours ahead | client@ | Book trainer@ for today | No time earlier than 2 hours from now is offered | | | |
| Diet plan target | trainer@ | New diet plan with 1,100 kcal, then 5,100 kcal | Both refused with the 1,200–5,000 range in the message | | | |
| Meal servings | client@ | Log a meal with 0.2 servings, then 21 | Refused (0.25–20 servings) | | | |
