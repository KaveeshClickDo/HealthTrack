# HealthTrack – user manual

HealthTrack helps clients reach their health and fitness goals with the support of fitness trainers and health
professionals. This manual has a part for each kind of user. Each part starts with the steps of the 10-minute demonstration
(spec 6.5) and then covers the rest of that role's pages.

The steps use the demo accounts (installation guide, section 6). Start from fresh demo data so the screens match.

**The 10-minute demonstration in order**

1. Administrator: approve a trainer, then assign a client (Part 1, steps 1–2).
2. Trainer: assign a routine template, approve an appointment, give feedback, run a report (Part 2, steps 1–4).
3. Client: log a workout and a meal, watch the charts update, receive a reminder, book an appointment (Part 3, steps 1–5).

---

## Getting started

### Opening HealthTrack and signing in

1. Open `https://localhost:7144` in your browser. The home page explains what HealthTrack does.
   [Screenshot: Home page]
2. Choose **Log in**, enter your email and password, and choose **Log in**. You arrive on your dashboard.
   [Screenshot: Login page]

After 5 wrong passwords the account is locked for 15 minutes. A trainer who has registered can't sign in until an
administrator approves them.

### Creating an account

1. Choose **Register** at the top right, then **Register as a client** or **Register as a professional** (the home page also
   has **Create a free account**).
   [Screenshot: Choose an account type]
2. Fill in the form. Clients give their height, weight, goal and activity level; professionals give their profession,
   qualifications and, unless they are a fitness trainer, a licence number. Everyone must be 16 or over and accept the
   privacy notice.
   [Screenshot: Client registration form]
3. Clients are signed in straight away. Professionals see that their registration is waiting for approval.
   [Screenshot: Registration waiting for approval]

### Finding your way around

- The **menu bar** shows your role's pages. On a phone, open it with the ☰ button.
- The **bell** shows how many notifications are unread. New notifications also pop up in the bottom corner; click one to
  open what it is about.
- Your **name** at the top right opens the user menu: Manage account, Notification settings (and Health profile for
  clients), and Log out.

[Screenshot: Menu bar with the bell and the user menu open]

---

## Part 1 – Administrator

Sign in as `admin@healthtrack.test`.

### Demo step 1: approve a trainer

1. The dashboard shows **Awaiting approval: 1**. Choose **Review applications**, or open **Trainer approvals** in the menu.
   [Screenshot: Admin dashboard with a trainer awaiting approval]
2. Read Daniel Perera's details (physiotherapist, licence number, qualifications) and choose **Approve**.
   [Screenshot: Trainer approvals with Daniel Perera]
3. The registration disappears from the list. Daniel can now sign in and gets a notification. To turn a registration down
   instead, enter a reason and choose **Reject**.
   [Screenshot: Approval confirmation message]

### Demo step 2: assign a client to a trainer

1. Open **Assignments**. The page lists the current assignments, each trainer's load and the clients without a trainer.
   [Screenshot: Assignments page]
2. Choose **Assign a trainer**, pick **Priya Nair** as the client and **Chris Bandara** as the trainer, keep today's date and
   choose **Assign**.
   [Screenshot: Assign a trainer form]
3. Priya now appears among Chris's clients, and both are notified. A client has one trainer at a time; assigning a new one
   ends the old assignment. To end one, choose **End** on its row.
   [Screenshot: Assignment created]

### The dashboard

The administrator's dashboard shows users by role and status, new registrations this month, users who signed in in the
last 7 days, trainers awaiting approval, unassigned clients, each trainer's load against capacity, logins per day for 14
days, appointments by status, and the latest audit events.
[Screenshot: Administrator dashboard]

### Users

1. **Users** lists every account. Filter by name or email, role and status.
   [Screenshot: Users list]
2. Open a user to see their details and recent activity. Choose **Suspend account** to stop them signing in (they are signed
   out within a minute) and **Reactivate account** to undo it. You can't suspend yourself or the last active administrator.
   [Screenshot: User details]
3. **Create administrator** adds another administrator.
   [Screenshot: Create an administrator]

### Exercise and food libraries

1. **Library → Exercises** lists the exercises trainers can put in plans, with category, muscle group and MET value (used to
   work out calories burned). **Add exercise** adds one; **Edit** changes it.
   [Screenshot: Exercise library]
2. **Deactivate** hides an exercise from the pickers without deleting it, so old plans and logs keep it. **Library → Foods**
   works the same way for foods, with calories and macros per serving.
   [Screenshot: Food library]

### Reports

Under **Reports**: Client performance, Popular routines, Engagement and Trainer workload. Pick a period (the last 30 days by
default) and choose **Show**. **Export CSV** downloads the table; **Print** gives a clean printout. Administrators see every
client but no weights or BMI.
[Screenshot: Trainer workload report]

### Audit log

**Audit log** lists sign-ins, failed sign-ins, registrations, approvals, assignments, plan and appointment changes, exports
and deletions, newest first. Filter by email, action and dates. It never shows passwords or health values.
[Screenshot: Audit log filtered by Login]

---

## Part 2 – Trainer

Sign in as `trainer@healthtrack.test` (Chris Bandara, fitness trainer).

### Demo step 1: assign a routine template

1. Open **Templates**. The routine templates are reusable plans; any trainer can use them for their own clients.
   [Screenshot: Routine templates]
2. On **5K runner** choose **Assign to a client**, choose **Priya Nair** (assigned in Part 1), keep today as the start date and
   **Activate it now** ticked, and choose **Assign**.
   [Screenshot: Assign the 5K runner template]
3. Priya gets her own copy of the plan, which you can change without touching the template, and a notification.
   [Screenshot: Priya's new workout plan]

### Demo step 2: approve an appointment

1. The dashboard lists **Requests to answer**. Amaya Perera has asked for a personal training session.
   [Screenshot: Trainer dashboard with a pending request]
2. Choose **Approve**. To turn a request down, type a reason and choose **Reject**.
   [Screenshot: Request approved]
3. Amaya is notified at once. The appointment page (**Appointments**, then the appointment) lets you add a room or meeting
   link, **Reschedule**, **Cancel** with a reason, and after the start time **Mark as completed** with consultation notes or
   record that the client **didn't attend**.
   [Screenshot: Appointment details for the trainer]

### Demo step 3: give feedback

1. On the dashboard choose **Give feedback** (or Clients → Give feedback), choose **Amaya Perera** and **Continue**.
   [Screenshot: Give feedback – choose the client]
2. Optionally pick one of her recent sessions, choose a category and a rating, write your message and choose
   **Send feedback**.
   [Screenshot: Give feedback form]
3. Amaya sees it on her dashboard and under Feedback.
   [Screenshot: Feedback sent]

### Demo step 4: run a report

1. Open **Reports → Client performance**. For each of your clients it shows weight and BMI at the start of the period and
   now, workouts, adherence, eating against the diet target, calories burned and appointments attended, with adherence as a
   chart.
   [Screenshot: Client performance report]
2. Change the dates and choose **Show**. **Export CSV** downloads the table for Excel; **Print** prints the report without
   the menus.
   [Screenshot: Report exported to Excel]
3. **Popular routines** ranks the templates and lists the most logged exercises; **Engagement** shows activity and logins
   week by week.
   [Screenshot: Engagement report]

### The dashboard

Your clients with their last activity, adherence over the last 7 days and weight change. Clients who haven't logged anything
for 7 days or more are listed first with an **Inactive** badge (Dilini Silva in the demo data). Also: requests to answer,
today's schedule, and quick actions to create a plan, assign a routine and give feedback.
[Screenshot: Trainer dashboard]

### Clients and their progress

**Clients** lists your assigned clients. **Progress** opens a client's charts (weight, BMI, calories, heart rate, blood
pressure), workout adherence, recent measurements and logs. Medical details and measurement notes appear only if the client
has chosen to share them.
[Screenshot: Client progress]

### Workout plans

1. **Workout plans → New client plan** (or **New plan** on a client's row under Clients): choose the client, a title, difficulty and start date,
   then **Create and add exercises**.
   [Screenshot: New workout plan]
2. Add exercises day by day with sets and reps or minutes, then **Activate**. A client has one active plan: activating a new
   one completes the old one.
   [Screenshot: Workout plan editor]
3. **Save as template** turns a good plan into a reusable template; **Archive** retires a plan without deleting it.
   [Screenshot: Plan saved as template]

### Diet plans

**Diet plans → New diet plan**: choose the client, the daily calorie target (1,200–5,000 kcal) and macro targets, then add
meals with a time, a description and target calories (every day or a particular weekday), and **Activate**.
[Screenshot: Diet plan editor]

### Availability

**Availability** shows your weekly windows; clients can book you only inside them, at least 2 hours and at most 60 days
ahead. Add a window with a day and times; windows on the same day can't overlap. **Remove** deletes one (appointments already
booked in it stay).
[Screenshot: Availability]

---

## Part 3 – Client

Sign in as `client@healthtrack.test` (Amaya Perera).

### Demo step 1: log a workout

1. On the dashboard, **Today's workout** shows today's exercises from your plan. Choose **Start** (on a rest day, use
   **Log → Today's workout** or **My plans → Workout plan → Log a free workout**).
   [Screenshot: Client dashboard with today's workout]
2. Tick the exercises you did, adjust the minutes, sets, reps and load, and choose **Save workout**. Calories burned are
   worked out for you from each exercise's MET value, your weight and the minutes.
   [Screenshot: Log a workout]
3. The workout appears in **Log → Workout history** and in "Burned this week" on the dashboard.
   [Screenshot: Workout history]

### Demo step 2: log a meal

1. On the dashboard, choose **Log** next to one of today's planned meals (or **Log → Log a meal**).
   [Screenshot: Today's meals on the dashboard]
2. Choose foods and servings and choose **Save meal**. Calories, protein, carbohydrate and fat are worked out from the food
   library.
   [Screenshot: Log a meal]
3. The meal is marked as logged and the bar shows how much of today's target you've eaten.
   [Screenshot: Today's meals with the progress bar]

### Demo step 3: see the charts update

1. Open **Progress → Charts**. Choose 4 weeks, 3 months or your own dates.
   [Screenshot: Progress charts]
2. The calories chart now includes today's workout and meal. Add a weigh-in with **Log → Add a measurement** and the weight
   and BMI charts gain a point; the dashed line is your target weight. Every chart can also be shown as a table.
   [Screenshot: Weight chart with the new reading]

### Demo step 4: receive a reminder

1. Reminders arrive by themselves while you use the site: a toast pops up and the bell's count goes up. Amaya has an
   approved appointment within 24 hours, so an **Appointment coming up** reminder appears within a minute.
   [Screenshot: Reminder toast]
2. Click the toast to open the appointment, or open the bell to see all notifications. Each reminder is sent only once.
   [Screenshot: Notifications page]
3. **Notification settings** (user menu) turns workout, meal and appointment reminders on or off, sets their times, and
   turns on email copies.
   [Screenshot: Notification settings]

### Demo step 5: book an appointment

1. Open **Appointments → Book an appointment** and choose a professional.
   [Screenshot: Choose a professional]
2. Choose the type, in person or online, a day and a length. Only free times are offered: inside the professional's hours, at
   least 2 hours from now and not clashing with your or their other appointments. Pick a time, add a reason if you like, and
   choose **Send the request**.
   [Screenshot: Booking form with free times]
3. The request is **Pending** until the professional answers; you are notified either way.
   [Screenshot: Appointments list with the pending request]

### The dashboard

Your latest weight and BMI with its category, the change since your first weigh-in, calories burned this week, today's
workout and meals, your next 3 appointments, the latest feedback and unread notifications.
[Screenshot: Client dashboard]

### My plans

**My plans → Workout plan** shows your active plan by weekday; **My plans → Diet plan** shows your meals with their times and
targets.
[Screenshot: My workout plan]

### Measurements

**Progress → Measurements** lists your weigh-ins with BMI and vital signs; **Add a measurement** records a new one (no future
dates).
[Screenshot: Measurements]

### Appointments

**Appointments** lists upcoming and past appointments with their status. Open one for the details, the room or meeting link
and, afterwards, the professional's notes. **Cancel appointment** works until 2 hours before the start.
[Screenshot: Appointment details for the client]

### Feedback

**Feedback** lists your trainer's comments, newest first, with a rating for progress.
[Screenshot: Feedback]

### Your account and your data

1. **Manage account** (user menu) changes your name, phone number and password.
   [Screenshot: Manage account]
2. **Health profile** (user menu) holds your goal, activity level, target weight, medical conditions, allergies and
   medications (stored encrypted) and whether your trainer may see them.
   [Screenshot: Health profile]
3. Under **Your data** on the same page, **Download my data** saves everything HealthTrack holds about you as a file, and
   **Delete my account** removes your health records for good after you confirm with your password.
   [Screenshot: Your data]
