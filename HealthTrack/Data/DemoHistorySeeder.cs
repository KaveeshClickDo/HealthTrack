using System.Globalization;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data
{
    /// <summary>
    /// Eight weeks of demo history for the seeded accounts (Phase 8): plans, workout and meal logs, measurements,
    /// feedback, appointments in every status, logins and notifications. Dates are relative to <see cref="IClock.Today"/>,
    /// so the demo always looks current, and a fixed random seed makes every run the same. Everything goes through the
    /// entity methods (CreateCopyForClient, AddEntry, AddItem, the Appointment transitions), so every stored snapshot (kcal,
    /// macros, statuses) is what the app itself would have saved.
    /// </summary>
    internal sealed class DemoHistorySeeder
    {
        // Loads that grow 2.5% a week, so strength logs show progress.
        private static readonly Dictionary<string, decimal> StartingLoads = new()
        {
            ["Seated cable row"] = 25m,
            ["Leg press"] = 60m,
            ["Barbell back squat"] = 70m,
            ["Deadlift"] = 90m,
            ["Bench press"] = 60m,
            ["Dumbbell shoulder press"] = 14m,
            ["Triceps dip"] = 0m,
            ["Lat pulldown"] = 45m,
            ["Dumbbell biceps curl"] = 10m
        };

        // Running speeds, for the distance of a logged run.
        private static readonly Dictionary<string, decimal> RunningSpeeds = new()
        {
            ["Running (8 km/h)"] = 8m,
            ["Running (10 km/h)"] = 10m
        };

        private static readonly string[] WorkoutNotes =
        [
            "Felt good today.",
            "Tired legs, kept it steady.",
            "Best session this week.",
            "Short on time, but got it done.",
            "Knees felt fine on the lunges."
        ];

        private readonly ApplicationDbContext _db;
        private readonly DateTime _now;
        private readonly DateTime _today;

        // A fixed seed: the same history on every run (relative to today).
        private readonly Random _random = new(20261025);

        private readonly List<AuditLog> _audit = [];
        private Dictionary<string, Exercise> _exercises = [];
        private Dictionary<int, Exercise> _exercisesById = [];
        private Dictionary<string, FoodItem> _foods = [];

        private DemoHistorySeeder(ApplicationDbContext db, IClock clock)
        {
            _db = db;
            _now = clock.Now;
            _today = clock.Today;
        }

        /// <summary>
        /// Seeds the history once: only when there are no workout logs yet and every demo account exists.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clock">Supplies today's date.</param>
        /// <param name="logger">Reports what was seeded.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the history is saved.</returns>
        public static async Task SeedAsync(ApplicationDbContext db, IClock clock, ILogger logger, CancellationToken cancellationToken)
        {
            if (await db.WorkoutLogs.AnyAsync(cancellationToken))
            {
                return;
            }

            // Messages and dates in en-GB, as the app shows them; the change ends when this method returns.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

            // All or nothing: a half-seeded history would be skipped on the next start because it has workout logs.
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var seeder = new DemoHistorySeeder(db, clock);
            if (!await seeder.RunAsync(cancellationToken))
            {
                logger.LogWarning("Demo history skipped: the demo accounts or templates are missing.");
                return;
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation("Seeded 8 weeks of demo history.");
        }

        private async Task<bool> RunAsync(CancellationToken cancellationToken)
        {
            var users = await _db.Users
                .Include(u => u.ClientProfile)
                .Include(u => u.TrainerProfile)
                .ToDictionaryAsync(u => u.Email ?? string.Empty, cancellationToken);
            var templates = await _db.WorkoutPlans
                .Include(p => p.Exercises)
                .Where(p => p.IsTemplate)
                .ToDictionaryAsync(p => p.Title, cancellationToken);
            _exercises = await _db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Name, cancellationToken);
            _exercisesById = _exercises.Values.ToDictionary(e => e.ExerciseId);
            _foods = await _db.FoodItems.AsNoTracking().ToDictionaryAsync(f => f.Name, cancellationToken);

            string[] emails =
            [
                "admin@healthtrack.test", "trainer@healthtrack.test", "nutritionist@healthtrack.test", "physio@healthtrack.test",
                "client@healthtrack.test", "client2@healthtrack.test", "client3@healthtrack.test", "client4@healthtrack.test",
                "client5@healthtrack.test", "client6@healthtrack.test"
            ];
            if (emails.Any(e => !users.ContainsKey(e))
                || !templates.ContainsKey("Beginner full body") || !templates.ContainsKey("5K runner") || !templates.ContainsKey("Strength split"))
            {
                return false;
            }

            var admin = users["admin@healthtrack.test"];
            var chris = users["trainer@healthtrack.test"];
            var shanika = users["nutritionist@healthtrack.test"];
            var physio = users["physio@healthtrack.test"];
            var amaya = users["client@healthtrack.test"];
            var ravi = users["client2@healthtrack.test"];
            var dilini = users["client3@healthtrack.test"];
            var kasun = users["client4@healthtrack.test"];
            var priya = users["client5@healthtrack.test"];
            var alex = users["client6@healthtrack.test"];

            // Plans, logs, measurements and feedback.
            var amayaPlan = SeedAmaya(amaya, chris, templates["Beginner full body"]);
            var raviPlan = SeedRavi(ravi, chris, templates["Strength split"]);
            var diliniPlan = SeedDilini(dilini, chris, templates["5K runner"]);
            var kasunPlan = SeedKasun(kasun, shanika);
            SeedPriya(priya);
            SeedAlex(alex);
            await _db.SaveChangesAsync(cancellationToken);

            // Appointments need their keys for the notification links.
            var appointments = SeedAppointments(amaya, ravi, dilini, kasun, chris, shanika);
            await _db.SaveChangesAsync(cancellationToken);

            AuditPlanActivations(chris, (amaya, amayaPlan.Workout), (ravi, raviPlan), (dilini, diliniPlan));
            AuditDietPlan(chris, amaya, amayaPlan.Diet);
            AuditDietPlan(shanika, kasun, kasunPlan);
            AuditAppointments(appointments);
            SeedNotifications(admin, chris, shanika, physio, amaya, ravi, dilini, kasun, amayaPlan, raviPlan, diliniPlan, kasunPlan, appointments);
            SeedLogins(admin, chris, shanika, amaya, ravi, dilini, kasun, priya, alex);

            _db.AuditLogs.AddRange(_audit);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        // client@: "Beginner full body" from 8 weeks ago with about 80% of sessions logged, a 4-meal diet plan logged on most
        // days, weekly weigh-ins losing about 0.4 kg a week with vitals, and four pieces of feedback.
        private (WorkoutPlan Workout, DietPlan Diet) SeedAmaya(ApplicationUser user, ApplicationUser trainer, WorkoutPlan template)
        {
            var client = user.ClientProfile!;
            var coach = trainer.TrainerProfile!;
            var joined = client.CreatedAt.Date;
            decimal Weight(DateTime day) => Math.Round(72.0m - 0.4m * (decimal)(day - joined).TotalDays / 7m, 1);

            var start = _today.AddDays(-56);
            var plan = AssignTemplate(template, client, coach, start);
            var logs = LogPlanWorkouts(client, plan, start, _today.AddDays(-1), 0.8, Weight);

            var diet = NewDietPlan(coach, client, "Steady weight loss", 1700, 110, 180, 55, start.AddDays(1),
                "About 500 kcal below your daily needs for a steady loss of around 0.4 kg a week.",
                Meal(MealType.Breakfast, 7, 30, "Porridge with banana and milk, or eggs on toast", 400),
                Meal(MealType.Lunch, 12, 30, "Lean protein with wholegrains and salad", 500),
                Meal(MealType.Snack, 16, 0, "Fruit with yoghurt or a little nut butter", 200),
                Meal(MealType.Dinner, 19, 30, "Fish or lean meat, starchy veg and greens", 550));
            LogPlanMeals(client, diet, diet.StartDate, _today.AddDays(-1), 0.85, 1.25m);

            // Weekly weigh-ins with vitals that improve as the weight comes down.
            for (var week = 0; week <= 7; week++)
            {
                AddMeasurement(client, start.AddDays(week * 7).AddHours(7).AddMinutes(15), Weight(start.AddDays(week * 7)) + Noise(0.15m), 165.0m,
                    bodyFat: 33.0m - 0.3m * week, waist: 84.0m - 0.5m * week,
                    heartRate: 76 - week, systolic: 128 - week, diastolic: 84 - week,
                    notes: week switch
                    {
                        2 => "Felt stronger on the stairs this week.",
                        5 => "Slept badly; weighed in after a late dinner.",
                        _ => null
                    });
            }

            AddMeasurement(client, _today.AddDays(-1).AddHours(7).AddMinutes(10), Weight(_today.AddDays(-1)), 165.0m,
                bodyFat: 30.6m, waist: 80.0m, heartRate: 68, systolic: 119, diastolic: 77, notes: null);

            // Four pieces of feedback across the eight weeks.
            AddFeedback(coach, client, logs.ElementAtOrDefault(3), FeedbackCategory.Workout, 4,
                "Great start! Your lunges are already steadier. Keep the rests to about a minute.");
            AddFeedback(coach, client, null, FeedbackCategory.Diet, 3, start.AddDays(24).AddHours(18),
                "Your lunches are spot on. Try to log the afternoon snack too, so we can see the whole day.");
            AddFeedback(coach, client, null, FeedbackCategory.Progress, 5, start.AddDays(42).AddHours(19),
                "Down more than 2 kg since you started, and your resting heart rate is falling too. Brilliant consistency.");
            AddFeedback(coach, client, logs.LastOrDefault(l => l.WorkoutDate <= _today.AddDays(-3)), FeedbackCategory.Workout, 4,
                "Ready for a challenge: add a fourth set of push-ups on Mondays.");
            return (plan, diet);
        }

        // client2@: muscle gain on "Strength split" from 6 weeks ago, nearly every session logged, weight creeping up.
        private WorkoutPlan SeedRavi(ApplicationUser user, ApplicationUser trainer, WorkoutPlan template)
        {
            var client = user.ClientProfile!;
            var coach = trainer.TrainerProfile!;
            var joined = client.CreatedAt.Date;
            decimal Weight(DateTime day) => Math.Round(74.5m + 0.2m * (decimal)(day - joined).TotalDays / 7m, 1);

            var start = _today.AddDays(-42);
            var plan = AssignTemplate(template, client, coach, start);
            var logs = LogPlanWorkouts(client, plan, start, _today.AddDays(-1), 0.9, Weight);

            foreach (var offset in new[] { -42, -28, -14, -2 })
            {
                var day = _today.AddDays(offset);
                AddMeasurement(client, day.AddHours(8), Weight(day) + Noise(0.2m), 178.0m, bodyFat: 16.0m - offset / -28m * 0.5m,
                    waist: null, heartRate: 58 + _random.Next(0, 3), systolic: null, diastolic: null, notes: null);
            }

            // No diet plan: big everyday meals on most days, and a protein shake after most sessions.
            LogFreeMeals(client, joined.AddDays(1), _today.AddDays(-1), 0.65, 1.5m);
            foreach (var log in logs.Where(_ => _random.NextDouble() < 0.7))
            {
                LogFreeMeal(client, log.WorkoutDate, MealType.Snack, new TimeSpan(19, 15, 0), ("Whey protein powder", 1m), ("Skimmed milk", 1m));
            }

            AddFeedback(coach, client, logs.LastOrDefault(l => l.WorkoutDate <= _today.AddDays(-5)), FeedbackCategory.Workout, 5,
                "Squat depth is much better. Add 2.5 kg next week if the last set still feels smooth.");
            return plan;
        }

        // client3@: "5K runner" from 6 weeks ago, then nothing at all for the last 12 days (flagged on trainer@'s dashboard).
        private WorkoutPlan SeedDilini(ApplicationUser user, ApplicationUser trainer, WorkoutPlan template)
        {
            var client = user.ClientProfile!;
            var coach = trainer.TrainerProfile!;
            decimal Weight(DateTime day) => 58.0m;

            var start = _today.AddDays(-42);
            var lastActive = _today.AddDays(-12);
            var plan = AssignTemplate(template, client, coach, start);
            LogPlanWorkouts(client, plan, start, lastActive, 0.7, Weight);

            foreach (var offset in new[] { -35, -21, -12 })
            {
                var day = _today.AddDays(offset);
                AddMeasurement(client, day.AddHours(7), 58.0m - 0.1m * (offset + 49) / 7m + Noise(0.1m), 160.0m, bodyFat: null, waist: null,
                    heartRate: 62 - (offset + 49) / 14, systolic: null, diastolic: null, notes: null);
            }

            LogFreeMeals(client, start, lastActive, 0.5, 1.0m);
            return plan;
        }

        // client4@: a diet plan from nutritionist@ (with weekday snacks), logged on most days, brisk walks without a workout
        // plan, and weekly readings including blood glucose.
        private DietPlan SeedKasun(ApplicationUser user, ApplicationUser nutritionist)
        {
            var client = user.ClientProfile!;
            var coach = nutritionist.TrainerProfile!;
            var joined = client.CreatedAt.Date;
            decimal Weight(DateTime day) => Math.Round(88.0m - 0.3m * (decimal)(day - joined).TotalDays / 7m, 1);

            var start = _today.AddDays(-35);
            var meals = new List<DietPlanMeal>
            {
                Meal(MealType.Breakfast, 7, 0, "Wholegrain breakfast with protein: eggs on wholemeal toast or porridge", 450),
                Meal(MealType.Lunch, 12, 30, "Half a plate of vegetables, a quarter lean protein, a quarter wholegrains", 600),
                Meal(MealType.Dinner, 19, 0, "Grilled fish or chicken with vegetables and a small portion of potato or rice", 650)
            };
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                meals.Add(Meal(MealType.Snack, 16, 0, "Fruit and a handful of nuts", 250, day));
            }

            var plan = NewDietPlan(coach, client, "Balanced plate for steady blood sugar", 2000, 120, 220, 65, start,
                "Regular, balanced meals that keep blood sugar steady while bringing weight down gently.", [.. meals]);
            LogPlanMeals(client, plan, start, _today.AddDays(-1), 0.75, 1.25m);

            for (var day = start; day < _today; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Saturday && _random.NextDouble() < 0.8)
                {
                    LogFreeWorkout(client, day, Weight(day), ("Brisk walking", 30 + 5 * _random.Next(0, 4)));
                }
            }

            for (var week = 0; week <= 5; week++)
            {
                var day = start.AddDays(week * 7);
                AddMeasurement(client, day.AddHours(7).AddMinutes(30), Weight(day) + Noise(0.2m), 172.0m, bodyFat: null, waist: 102.0m - 0.6m * week,
                    heartRate: 78 - week / 2, systolic: 138 - week, diastolic: 88 - week / 2, notes: null, glucose: 7.8m - 0.2m * week);
            }

            return plan;
        }

        // client5@ (unassigned): yoga and Pilates, a few meals and measurements, no plans.
        private void SeedPriya(ApplicationUser user)
        {
            var client = user.ClientProfile!;
            for (var day = client.CreatedAt.Date.AddDays(1); day < _today; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday or DayOfWeek.Sunday)
                {
                    LogFreeWorkout(client, day, 55.0m, day.DayOfWeek == DayOfWeek.Thursday ? ("Pilates", 30) : ("Hatha yoga", 45));
                }
            }

            LogFreeMeals(client, client.CreatedAt.Date.AddDays(1), _today.AddDays(-1), 0.5, 0.9m);

            AddMeasurement(client, _today.AddDays(-7).AddHours(9), 54.8m, 158.0m, bodyFat: null, waist: null, heartRate: 66, systolic: null, diastolic: null, notes: null);
            AddMeasurement(client, _today.AddDays(-1).AddHours(9), 54.9m, 158.0m, bodyFat: null, waist: null, heartRate: 65, systolic: 112, diastolic: 72, notes: null);
        }

        // client6@ (unassigned, joined 5 days ago): a couple of workouts and meals.
        private void SeedAlex(ApplicationUser user)
        {
            var client = user.ClientProfile!;
            LogFreeWorkout(client, _today.AddDays(-4), 79.0m, ("Running (8 km/h)", 30));
            LogFreeWorkout(client, _today.AddDays(-2), 79.0m, ("Tennis", 60));
            LogFreeMeal(client, _today.AddDays(-4), MealType.Lunch, new TimeSpan(13, 0, 0), Pick(Lunches));
            LogFreeMeal(client, _today.AddDays(-2), MealType.Dinner, new TimeSpan(19, 30, 0), Pick(Dinners));
            AddMeasurement(client, _today.AddDays(-1).AddHours(8), 78.6m, 183.0m, bodyFat: null, waist: null, heartRate: 56, systolic: null, diastolic: null, notes: null);
        }

        // Appointments in every status for client@ with trainer@, plus some for the other clients, each moved through the
        // Appointment methods at a time that makes the move valid.
        private DemoAppointments SeedAppointments(ApplicationUser amaya, ApplicationUser ravi, ApplicationUser dilini, ApplicationUser kasun,
            ApplicationUser chris, ApplicationUser shanika)
        {
            var client = amaya.ClientProfile!;
            var coach = chris.TrainerProfile!;
            var result = new DemoAppointments();

            var assessment = Book(client, coach, PreviousWeekday(_today.AddDays(-35)).AddHours(10), 60, AppointmentType.FitnessAssessment,
                AppointmentMode.InPerson, "First assessment before starting my plan.");
            assessment.Approve();
            assessment.Location = "Studio 2";
            assessment.Complete("Baseline: 72 kg, resting HR 76. Good mobility; weak core endurance. Started on three full-body sessions a week.",
                assessment.StartDateTime.AddHours(1));
            assessment.UpdatedAt = assessment.StartDateTime.AddHours(1);

            var rejected = Book(client, coach, PreviousWeekday(_today.AddDays(-42)).AddHours(16), 45, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, null);
            rejected.Reject("I'm on a course that afternoon. Please pick another time.");
            rejected.UpdatedAt = rejected.CreatedAt.AddHours(3);

            var cancelled = Book(client, coach, PreviousWeekday(_today.AddDays(-28)).AddHours(11), 60, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, null);
            cancelled.CancelByClient("Work trip that week.", cancelled.StartDateTime.AddDays(-3));
            cancelled.UpdatedAt = cancelled.StartDateTime.AddDays(-3);

            var missed = Book(client, coach, PreviousWeekday(_today.AddDays(-21)).AddHours(9).AddMinutes(30), 30, AppointmentType.FollowUp,
                AppointmentMode.Online, null);
            missed.Approve();
            missed.Location = "https://meet.example.com/healthtrack-chris";
            missed.MarkNoShow(missed.StartDateTime.AddMinutes(30));
            missed.UpdatedAt = missed.StartDateTime.AddMinutes(30);

            var session = Book(client, coach, PreviousWeekday(_today.AddDays(-14)).AddHours(15), 45, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, "Would like to check my squat technique.");
            session.Approve();
            session.Location = "Studio 1";
            session.Complete("Squat form much better: depth to parallel, knees tracking well. Progressed leg press to 65 kg.",
                session.StartDateTime.AddMinutes(45));
            session.UpdatedAt = session.StartDateTime.AddMinutes(45);

            var trainerCancelled = Book(client, coach, PreviousWeekday(_today.AddDays(-7)).AddHours(14), 60, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, null);
            trainerCancelled.Approve();
            trainerCancelled.CancelByTrainer("The studio is closed for maintenance that day.", trainerCancelled.StartDateTime.AddDays(-1));
            trainerCancelled.UpdatedAt = trainerCancelled.StartDateTime.AddDays(-1);

            // Within the next 24 hours (the reminder demo): tomorrow morning, or later today if the app starts early.
            var soon = _today.AddDays(1).AddHours(9).AddMinutes(30);
            if (soon - _now > TimeSpan.FromHours(24))
            {
                soon = _today.AddHours(16);
            }

            result.Upcoming = Book(client, coach, soon, 30, AppointmentType.FollowUp, AppointmentMode.Online, "Check-in on my progress.",
                createdAt: _now.AddDays(-2));
            result.Upcoming.Approve();
            result.Upcoming.Location = "https://meet.example.com/healthtrack-chris";
            result.Upcoming.UpdatedAt = _now.AddDays(-1);

            result.Pending = Book(client, coach, NextWeekday(_today.AddDays(4)).AddHours(10), 60, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, "Could we work on upper-body strength?", createdAt: _today.AddDays(-1).AddHours(20));

            result.Moved = Book(client, coach, NextWeekday(_today.AddDays(10)).AddHours(15), 45, AppointmentType.FitnessAssessment,
                AppointmentMode.InPerson, "Eight-week reassessment.", createdAt: _today.AddDays(-3).AddHours(9));
            result.Moved.Approve();
            result.MovedFrom = result.Moved.StartDateTime;
            result.Moved.Reschedule(NextWeekday(_today.AddDays(12)).AddHours(14), 45);
            result.Moved.Location = "Studio 2";
            result.Moved.UpdatedAt = _today.AddDays(-1).AddHours(11);
            result.ForAmaya.AddRange([assessment, rejected, cancelled, missed, session, trainerCancelled]);

            // trainer@'s schedule today, and history with the other clients.
            result.Today = Book(ravi.ClientProfile!, coach, _today.AddHours(15), 60, AppointmentType.PersonalTraining, AppointmentMode.InPerson,
                null, createdAt: _today.AddDays(-6).AddHours(18));
            result.Today.Approve();
            result.Today.Location = "Studio 1";
            result.Today.UpdatedAt = _today.AddDays(-5).AddHours(9);

            var raviPast = Book(ravi.ClientProfile!, coach, PreviousWeekday(_today.AddDays(-20)).AddHours(16), 60, AppointmentType.PersonalTraining,
                AppointmentMode.InPerson, null);
            raviPast.Approve();
            raviPast.Complete("Deadlift technique reviewed; hip hinge solid. Keep the bar closer on the way up.", raviPast.StartDateTime.AddHours(1));
            raviPast.UpdatedAt = raviPast.StartDateTime.AddHours(1);

            var diliniCancelled = Book(dilini.ClientProfile!, coach, PreviousWeekday(_today.AddDays(-16)).AddHours(11), 30, AppointmentType.FollowUp,
                AppointmentMode.Online, null);
            diliniCancelled.CancelByClient("Exams this week.", diliniCancelled.StartDateTime.AddDays(-2));
            diliniCancelled.UpdatedAt = diliniCancelled.StartDateTime.AddDays(-2);

            var nutrition = Book(kasun.ClientProfile!, shanika.TrainerProfile!, PreviousWeekday(_today.AddDays(-21)).AddHours(11), 60,
                AppointmentType.NutritionConsultation, AppointmentMode.InPerson, "Help planning meals around my diabetes medication.");
            nutrition.Approve();
            nutrition.Location = "Consulting room 3";
            nutrition.Complete("Reviewed food diary: breakfast skipped twice a week. Agreed regular meals and a weekday afternoon snack. Glucose trending down.",
                nutrition.StartDateTime.AddHours(1));
            nutrition.UpdatedAt = nutrition.StartDateTime.AddHours(1);

            result.KasunFollowUp = Book(kasun.ClientProfile!, shanika.TrainerProfile!, NextWeekday(_today.AddDays(5)).AddHours(14), 30,
                AppointmentType.FollowUp, AppointmentMode.Online, null, createdAt: _today.AddDays(-4).AddHours(10));
            result.KasunFollowUp.Approve();
            result.KasunFollowUp.Location = "https://meet.example.com/healthtrack-shanika";
            result.KasunFollowUp.UpdatedAt = _today.AddDays(-3).AddHours(9);

            result.Others.AddRange([raviPast, diliniCancelled, nutrition]);
            return result;
        }

        private void SeedNotifications(ApplicationUser admin, ApplicationUser chris, ApplicationUser shanika, ApplicationUser physio,
            ApplicationUser amaya, ApplicationUser ravi, ApplicationUser dilini, ApplicationUser kasun,
            (WorkoutPlan Workout, DietPlan Diet) amayaPlans, WorkoutPlan raviPlan, WorkoutPlan diliniPlan, DietPlan kasunPlan,
            DemoAppointments appointments)
        {
            foreach (var (client, coach) in new[] { (amaya, chris), (ravi, chris), (dilini, chris), (kasun, shanika) })
            {
                var assigned = client.CreatedAt.AddHours(2);
                Notify(client, NotificationType.AccountUpdate, $"Your trainer is {coach.FullName}",
                    $"{coach.FullName} ({coach.TrainerProfile!.ProfessionalType.GetDisplayName().ToLowerInvariant()}) will now plan and follow your training, and can see your plans, logs and progress.",
                    "/Client/Dashboard", assigned, read: true);
                Notify(coach, NotificationType.AccountUpdate, "New client assigned",
                    $"{client.FullName} has been assigned to you from {client.CreatedAt:dd MMM yyyy}.", "/Trainer/Clients", assigned, read: true);
            }

            foreach (var (client, plan) in new[] { (amaya, amayaPlans.Workout), (ravi, raviPlan), (dilini, diliniPlan) })
            {
                Notify(client, NotificationType.PlanAssigned, "New workout plan",
                    $"{chris.FullName} has given you a new workout plan: {plan.Title}, starting {plan.StartDate:dd MMM yyyy}.",
                    "/Client/Workouts/MyPlan", plan.CreatedAt, read: true);
            }

            foreach (var (client, coach, plan) in new[] { (amaya, chris, amayaPlans.Diet), (kasun, shanika, kasunPlan) })
            {
                Notify(client, NotificationType.PlanAssigned, "New diet plan",
                    $"{coach.FullName} has given you a new diet plan: {plan.Title}, {plan.DailyCalorieTarget:N0} kcal a day from {plan.StartDate:dd MMM yyyy}.",
                    "/Client/Diet/MyPlan", plan.CreatedAt, read: true);
            }

            // Feedback: the newest piece for each client is still unread.
            foreach (var group in _db.ChangeTracker.Entries<Feedback>().Select(e => e.Entity).GroupBy(f => f.ClientId))
            {
                var ordered = group.OrderBy(f => f.CreatedAt).ToList();
                var client = new[] { amaya, ravi }.First(u => u.ClientProfile!.ClientId == group.Key);
                var coach = group.First().TrainerId == chris.TrainerProfile!.TrainerId ? chris : shanika;
                foreach (var feedback in ordered)
                {
                    Notify(client, NotificationType.NewFeedback, $"New feedback from {coach.FullName}",
                        $"{coach.FullName} left you {feedback.Category.GetDisplayName().ToLowerInvariant()} feedback.", "/Client/Feedback",
                        feedback.CreatedAt, read: feedback != ordered[^1]);
                }
            }

            var upcoming = appointments.Upcoming!;
            Notify(amaya, NotificationType.AppointmentUpdate, "Appointment approved",
                $"{chris.FullName} approved your {Describe(upcoming)}. Where: {upcoming.Location}",
                $"/Client/Appointments/Details/{upcoming.AppointmentId}", upcoming.UpdatedAt!.Value, read: false);
            var moved = appointments.Moved!;
            Notify(amaya, NotificationType.AppointmentUpdate, "Appointment moved",
                $"{chris.FullName} moved your appointment from {When(appointments.MovedFrom)} to {When(moved.StartDateTime)} ({moved.DurationMinutes} minutes).",
                $"/Client/Appointments/Details/{moved.AppointmentId}", moved.UpdatedAt!.Value, read: false);
            var pending = appointments.Pending!;
            Notify(chris, NotificationType.AppointmentUpdate, "New appointment request",
                $"{amaya.FullName} asked for {Describe(pending)}, {pending.DurationMinutes} minutes.",
                $"/Trainer/Appointments/Details/{pending.AppointmentId}", pending.CreatedAt, read: false);

            Notify(admin, NotificationType.AccountUpdate, "New trainer registration",
                $"{physio.FullName} registered as a {physio.TrainerProfile!.ProfessionalType.GetDisplayName().ToLowerInvariant()} and is waiting for approval.",
                "/Admin/TrainerApprovals", physio.CreatedAt.AddMinutes(5), read: false);
        }

        // Logins over the last 14 days, so the admin dashboard's chart and "active users" have data; client3@ hasn't been
        // back for over 10 days.
        private void SeedLogins(ApplicationUser admin, ApplicationUser chris, ApplicationUser shanika, ApplicationUser amaya,
            ApplicationUser ravi, ApplicationUser dilini, ApplicationUser kasun, ApplicationUser priya, ApplicationUser alex)
        {
            foreach (var (user, chance) in new[] { (admin, 0.7), (chris, 0.9), (shanika, 0.5), (amaya, 0.75), (ravi, 0.6), (kasun, 0.45), (priya, 0.4) })
            {
                for (var offset = -13; offset <= 0; offset++)
                {
                    if (_random.NextDouble() < chance && _today.AddDays(offset) >= user.CreatedAt.Date)
                    {
                        Login(user, _today.AddDays(offset).AddHours(7 + _random.Next(0, 14)).AddMinutes(_random.Next(0, 60)));
                    }
                }
            }

            Login(dilini, _today.AddDays(-13).AddHours(20).AddMinutes(10));
            Login(alex, alex.CreatedAt.AddMinutes(1));
            Login(alex, _today.AddDays(-1).AddHours(18).AddMinutes(40));

            // A mistyped password now and then.
            _audit.Add(new AuditLog
            {
                UserId = amaya.Id, Action = AuditAction.LoginFailed, Details = "Wrong password.", IpAddress = "127.0.0.1",
                Timestamp = _today.AddDays(-3).AddHours(7).AddMinutes(58)
            });
        }

        private void Login(ApplicationUser user, DateTime at)
        {
            // Nothing in the future: the app may start early in the morning.
            if (at > _now)
            {
                return;
            }

            _audit.Add(new AuditLog { UserId = user.Id, Action = AuditAction.Login, IpAddress = "127.0.0.1", Timestamp = at });
            if (user.LastLoginAt is null || user.LastLoginAt < at)
            {
                user.LastLoginAt = at;
            }
        }

        private void AuditPlanActivations(ApplicationUser trainer, params (ApplicationUser Client, WorkoutPlan Plan)[] plans)
        {
            foreach (var (_, plan) in plans)
            {
                Audit(trainer.Id, AuditAction.Update, nameof(WorkoutPlan), plan.WorkoutPlanId, $"Workout plan activated: {plan.Title}.", plan.CreatedAt);
            }
        }

        private void AuditDietPlan(ApplicationUser trainer, ApplicationUser client, DietPlan plan) =>
            Audit(trainer.Id, AuditAction.Update, nameof(DietPlan), plan.DietPlanId, $"Diet plan activated: {plan.Title}.", plan.CreatedAt);

        private void AuditAppointments(DemoAppointments appointments)
        {
            foreach (var appointment in appointments.All)
            {
                var clientUserId = appointment.Client.UserId;
                var trainerUserId = appointment.Trainer.UserId;
                Audit(clientUserId, AuditAction.Create, nameof(Appointment), appointment.AppointmentId,
                    $"Appointment requested with {appointment.Trainer.User.FullName} for {When(appointment.StartDateTime)}.", appointment.CreatedAt);

                var at = appointment.UpdatedAt ?? appointment.CreatedAt;
                switch (appointment.Status)
                {
                    case AppointmentStatus.Rejected:
                        Audit(trainerUserId, AuditAction.Reject, nameof(Appointment), appointment.AppointmentId, "Appointment rejected.", at);
                        break;
                    case AppointmentStatus.CancelledByClient:
                        Audit(clientUserId, AuditAction.Cancel, nameof(Appointment), appointment.AppointmentId, "Appointment cancelled by the client.", at);
                        break;
                    case AppointmentStatus.CancelledByTrainer:
                        Audit(trainerUserId, AuditAction.Cancel, nameof(Appointment), appointment.AppointmentId, "Appointment cancelled by the professional.", at);
                        break;
                    case AppointmentStatus.Completed:
                        Audit(trainerUserId, AuditAction.Update, nameof(Appointment), appointment.AppointmentId, "Appointment completed.", at);
                        break;
                    case AppointmentStatus.NoShow:
                        Audit(trainerUserId, AuditAction.Update, nameof(Appointment), appointment.AppointmentId, "Appointment marked as a no-show.", at);
                        break;
                    case AppointmentStatus.Approved:
                    case AppointmentStatus.Rescheduled:
                        Audit(trainerUserId, AuditAction.Approve, nameof(Appointment), appointment.AppointmentId, "Appointment approved.", at);
                        break;
                }
            }
        }

        private void Audit(string userId, AuditAction action, string entityName, int entityId, string details, DateTime at)
        {
            if (at <= _now)
            {
                _audit.Add(new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityName = entityName,
                    EntityId = entityId.ToString(CultureInfo.InvariantCulture),
                    Details = details,
                    IpAddress = "127.0.0.1",
                    Timestamp = at
                });
            }
        }

        private void Notify(ApplicationUser user, NotificationType type, string title, string message, string linkUrl, DateTime createdAt, bool read)
        {
            var notification = new Notification
            {
                UserId = user.Id,
                Type = type,
                Title = title.Truncate(100)!,
                Message = message.Truncate(500)!,
                LinkUrl = linkUrl,
                CreatedAt = createdAt > _now ? _now : createdAt
            };
            if (read)
            {
                notification.MarkAsRead(createdAt.AddHours(3) > _now ? _now : createdAt.AddHours(3));
            }

            _db.Notifications.Add(notification);
        }

        // BR-12: the client's plan is a copy of the template, activated from its start date.
        private WorkoutPlan AssignTemplate(WorkoutPlan template, ClientProfile client, TrainerProfile trainer, DateTime start)
        {
            var plan = template.CreateCopyForClient(client.ClientId, trainer.TrainerId, start);
            plan.CreatedAt = start.AddDays(-1).AddHours(17);
            plan.Activate();
            _db.WorkoutPlans.Add(plan);
            return plan;
        }

        private DietPlan NewDietPlan(TrainerProfile trainer, ClientProfile client, string title, int calories, int protein, int carbs, int fat,
            DateTime start, string description, params DietPlanMeal[] meals)
        {
            var plan = new DietPlan
            {
                TrainerId = trainer.TrainerId,
                ClientId = client.ClientId,
                Title = title,
                Description = description,
                DailyCalorieTarget = calories,
                ProteinTargetG = protein,
                CarbsTargetG = carbs,
                FatTargetG = fat,
                StartDate = start.Date,
                CreatedAt = start.AddDays(-1).AddHours(16)
            };
            foreach (var meal in meals)
            {
                plan.Meals.Add(meal);
            }

            plan.Activate();
            _db.DietPlans.Add(plan);
            return plan;
        }

        private static DietPlanMeal Meal(MealType type, int hour, int minute, string description, int calories, DayOfWeek? day = null) => new()
        {
            MealType = type,
            DayOfWeek = day,
            ScheduledTime = new TimeSpan(hour, minute, 0),
            MealDescription = description,
            TargetCalories = calories
        };

        // Logs about `chance` of the plan's training days in the range, each with every exercise of the day.
        private List<WorkoutLog> LogPlanWorkouts(ClientProfile client, WorkoutPlan plan, DateTime from, DateTime to, double chance,
            Func<DateTime, decimal> weight)
        {
            var logs = new List<WorkoutLog>();
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                var lines = plan.GetExercisesFor(day.DayOfWeek);
                if (lines.Count == 0 || _random.NextDouble() >= chance)
                {
                    continue;
                }

                var week = (day - from.Date).Days / 7;
                var log = new WorkoutLog
                {
                    ClientId = client.ClientId,
                    WorkoutPlan = plan,
                    WorkoutDate = day,
                    PerceivedEffort = _random.Next(5, 9),
                    Notes = _random.NextDouble() < 0.25 ? Pick(WorkoutNotes) : null,
                    CreatedAt = day.AddHours(18).AddMinutes(_random.Next(0, 90))
                };

                foreach (var line in lines)
                {
                    var exercise = _exercisesById[line.ExerciseId];
                    var minutes = line.DurationMinutes ?? Math.Max(5, (line.Sets ?? 3) * 3);
                    decimal? load = StartingLoads.TryGetValue(exercise.Name, out var startLoad) && startLoad > 0
                        ? Math.Round(startLoad * (1 + 0.025m * week) / 2.5m, 0) * 2.5m
                        : null;
                    decimal? distance = RunningSpeeds.TryGetValue(exercise.Name, out var speed) ? Math.Round(speed * minutes / 60m, 2) : null;
                    log.AddEntry(exercise, weight(day), minutes, line.Sets, line.Reps is { } reps ? reps + _random.Next(-1, 2) : null, load, distance);
                }

                _db.WorkoutLogs.Add(log);
                logs.Add(log);
            }

            return logs;
        }

        private void LogFreeWorkout(ClientProfile client, DateTime day, decimal weightKg, (string Exercise, int Minutes) activity)
        {
            var exercise = _exercises[activity.Exercise];
            var log = new WorkoutLog
            {
                ClientId = client.ClientId,
                WorkoutDate = day.Date,
                PerceivedEffort = _random.Next(3, 7),
                CreatedAt = day.Date.AddHours(17).AddMinutes(_random.Next(0, 120))
            };
            decimal? distance = RunningSpeeds.TryGetValue(exercise.Name, out var speed) ? Math.Round(speed * activity.Minutes / 60m, 2) : null;
            log.AddEntry(exercise, weightKg, activity.Minutes, distanceKm: distance);
            _db.WorkoutLogs.Add(log);
        }

        // Logs about `chance` of the plan's meals in the range, each with one of a few typical menus for that meal.
        private void LogPlanMeals(ClientProfile client, DietPlan plan, DateTime from, DateTime to, double chance, decimal portion)
        {
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                foreach (var meal in plan.GetMealsFor(day.DayOfWeek))
                {
                    if (_random.NextDouble() >= chance)
                    {
                        continue;
                    }

                    var log = new MealLog
                    {
                        ClientId = client.ClientId,
                        DietPlanMeal = meal,
                        MealDate = day,
                        MealType = meal.MealType,
                        CreatedAt = day + meal.ScheduledTime + TimeSpan.FromMinutes(_random.Next(10, 60))
                    };
                    AddItems(log, MenuFor(meal.MealType), portion);
                    _db.MealLogs.Add(log);
                }
            }
        }

        private void LogFreeMeal(ClientProfile client, DateTime day, MealType type, TimeSpan time, params (string Food, decimal Quantity)[] items) =>
            LogFreeMeal(client, day, type, time, 1m, items);

        private void LogFreeMeal(ClientProfile client, DateTime day, MealType type, TimeSpan time, decimal portion, (string Food, decimal Quantity)[] items)
        {
            var log = new MealLog { ClientId = client.ClientId, MealDate = day.Date, MealType = type, CreatedAt = day.Date + time };
            AddItems(log, items, portion);
            _db.MealLogs.Add(log);
        }

        // Everyday eating without a diet plan: on about `chance` of the days, breakfast, lunch and dinner, each eaten most of
        // the time, from the same menus.
        private void LogFreeMeals(ClientProfile client, DateTime from, DateTime to, double chance, decimal portion)
        {
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                if (_random.NextDouble() >= chance)
                {
                    continue;
                }

                foreach (var (type, time) in new[] { (MealType.Breakfast, new TimeSpan(8, 0, 0)), (MealType.Lunch, new TimeSpan(13, 0, 0)), (MealType.Dinner, new TimeSpan(19, 30, 0)) })
                {
                    if (_random.NextDouble() < 0.85)
                    {
                        LogFreeMeal(client, day, type, time, portion, MenuFor(type));
                    }
                }
            }
        }

        private void AddItems(MealLog log, (string Food, decimal Quantity)[] items, decimal portion)
        {
            foreach (var (food, quantity) in items)
            {
                // Portions vary a little from day to day, in quarter servings.
                var servings = Math.Max(0.25m, Math.Round(quantity * portion * (0.9m + (decimal)_random.NextDouble() * 0.3m) * 4m) / 4m);
                log.AddItem(_foods[food], servings);
            }
        }

        private (string Food, decimal Quantity)[] MenuFor(MealType type) => type switch
        {
            MealType.Breakfast => Pick(Breakfasts),
            MealType.Lunch => Pick(Lunches),
            MealType.Dinner => Pick(Dinners),
            _ => Pick(Snacks)
        };

        private static readonly (string Food, decimal Quantity)[][] Breakfasts =
        [
            [("Rolled oats", 1m), ("Semi-skimmed milk", 1m), ("Banana", 1m)],
            [("Wholemeal bread", 2m), ("Egg, boiled", 2m), ("Orange juice", 1m)],
            [("Greek yoghurt, fat-free", 1m), ("Blueberries", 1m), ("Almonds", 0.5m)]
        ];

        private static readonly (string Food, decimal Quantity)[][] Lunches =
        [
            [("Chicken breast, grilled", 1.5m), ("Brown rice, cooked", 1m), ("Mixed salad leaves", 1m), ("Olive oil", 0.5m)],
            [("Tuna in water, drained", 1m), ("Wholemeal tortilla wrap", 1m), ("Tomato", 1m), ("Mixed salad leaves", 1m)],
            [("Lentils, cooked", 1m), ("Quinoa, cooked", 1m), ("Spinach, raw", 1m)]
        ];

        private static readonly (string Food, decimal Quantity)[][] Snacks =
        [
            [("Apple", 1m), ("Peanut butter", 1m)],
            [("Greek yoghurt, fat-free", 1m), ("Strawberries", 1m)],
            [("Cereal bar", 1m), ("Orange", 1m)]
        ];

        private static readonly (string Food, decimal Quantity)[][] Dinners =
        [
            [("Salmon fillet, baked", 1.3m), ("Sweet potato, baked", 1m), ("Broccoli, steamed", 1m)],
            [("Turkey breast, roasted", 1.5m), ("Potato, boiled", 1m), ("Garden peas, boiled", 1m)],
            [("Tofu, firm", 1.5m), ("White rice, cooked", 1m), ("Broccoli, steamed", 1m), ("Olive oil", 0.5m)]
        ];

        private void AddMeasurement(ClientProfile client, DateTime at, decimal weightKg, decimal heightCm, decimal? bodyFat, decimal? waist,
            int? heartRate, int? systolic, int? diastolic, string? notes, decimal? glucose = null)
        {
            if (at > _now)
            {
                return;
            }

            _db.HealthMeasurements.Add(new HealthMeasurement
            {
                ClientId = client.ClientId,
                MeasuredAt = at,
                WeightKg = Math.Round(weightKg, 1),
                HeightCm = heightCm,
                BodyFatPercent = bodyFat is { } fat ? Math.Round(fat, 1) : null,
                WaistCm = waist is { } cm ? Math.Round(cm, 1) : null,
                RestingHeartRate = heartRate,
                SystolicBp = systolic,
                DiastolicBp = diastolic,
                BloodGlucose = glucose is { } mmol ? Math.Round(mmol, 1) : null,
                Notes = notes
            });
        }

        // Feedback on a logged session is given the evening after it.
        private void AddFeedback(TrainerProfile trainer, ClientProfile client, WorkoutLog? log, FeedbackCategory category, int rating, string message)
        {
            if (log is not null)
            {
                AddFeedback(trainer, client, log, category, rating, log.WorkoutDate.AddDays(1).AddHours(19), message);
            }
        }

        private void AddFeedback(TrainerProfile trainer, ClientProfile client, WorkoutLog? log, FeedbackCategory category, int rating,
            DateTime at, string message)
        {
            if (at > _now)
            {
                return;
            }

            _db.Feedback.Add(new Feedback
            {
                TrainerId = trainer.TrainerId,
                ClientId = client.ClientId,
                WorkoutLog = log,
                Category = category,
                Rating = rating,
                Message = message,
                CreatedAt = at
            });
        }

        // A request as the client made it, two days before the start unless given; the caller moves it on.
        private Appointment Book(ClientProfile client, TrainerProfile trainer, DateTime start, int minutes, AppointmentType type,
            AppointmentMode mode, string? reason, DateTime? createdAt = null)
        {
            var appointment = new Appointment
            {
                Client = client,
                Trainer = trainer,
                ClientId = client.ClientId,
                TrainerId = trainer.TrainerId,
                AppointmentType = type,
                Mode = mode,
                StartDateTime = start,
                DurationMinutes = minutes,
                ClientReason = reason,
                Status = AppointmentStatus.Pending,
                CreatedAt = createdAt ?? start.AddDays(-5).Date.AddHours(20)
            };
            _db.Appointments.Add(appointment);
            return appointment;
        }

        private T Pick<T>(IReadOnlyList<T> options) => options[_random.Next(options.Count)];

        private decimal Noise(decimal size) => Math.Round(((decimal)_random.NextDouble() * 2m - 1m) * size, 1);

        // Past appointments fall on a weekday: weekends move back to Friday; future ones move on to Monday.
        private static DateTime PreviousWeekday(DateTime day) => day.DayOfWeek switch
        {
            DayOfWeek.Saturday => day.AddDays(-1),
            DayOfWeek.Sunday => day.AddDays(-2),
            _ => day
        };

        private static DateTime NextWeekday(DateTime day) => day.DayOfWeek switch
        {
            DayOfWeek.Saturday => day.AddDays(2),
            DayOfWeek.Sunday => day.AddDays(1),
            _ => day
        };

        private static string Describe(Appointment a) =>
            $"{a.AppointmentType.GetDisplayName().ToLowerInvariant()} on {When(a.StartDateTime)}";

        private static string When(DateTime start) => start.ToString("dd MMM yyyy 'at' HH:mm", CultureInfo.GetCultureInfo("en-GB"));
    }
}
