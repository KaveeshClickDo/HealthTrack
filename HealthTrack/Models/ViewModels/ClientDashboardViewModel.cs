using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's dashboard (UC07, spec 1.4): health stats, today's workout and meals, appointments, feedback and
    /// notifications.
    /// </summary>
    public class ClientDashboardViewModel
    {
        /// <summary>The client's first name, for the greeting.</summary>
        public string FirstName { get; init; } = string.Empty;

        /// <summary>The active trainer's name, if one is assigned.</summary>
        public string? TrainerName { get; init; }

        /// <summary>The latest weight in kg.</summary>
        public decimal? LatestWeightKg { get; init; }

        /// <summary>When the latest measurement was taken.</summary>
        public DateTime? LatestMeasuredAt { get; init; }

        /// <summary>The latest BMI (spec 1.10).</summary>
        public decimal? LatestBmi { get; init; }

        /// <summary>The WHO category of the latest BMI.</summary>
        public BmiCategory? BmiCategory { get; init; }

        /// <summary>Weight change since the first measurement, in kg (negative is a loss).</summary>
        public decimal? WeightChangeKg { get; init; }

        /// <summary>When the first measurement was taken.</summary>
        public DateTime? FirstMeasuredAt { get; init; }

        /// <summary>The client's target weight, if set.</summary>
        public decimal? TargetWeightKg { get; init; }

        /// <summary>kcal burned in logged workouts this week (Monday to today).</summary>
        public decimal CaloriesBurnedThisWeek { get; init; }

        /// <summary>Workouts logged this week.</summary>
        public int WorkoutsThisWeek { get; init; }

        /// <summary>Today's workout.</summary>
        public TodayWorkoutViewModel TodaysWorkout { get; init; } = new();

        /// <summary>Today's planned meals, with what has been logged.</summary>
        public IReadOnlyList<PlannedMealViewModel> TodaysMeals { get; init; } = [];

        /// <summary>Today's eating against the diet plan's target.</summary>
        public DailyNutritionViewModel Nutrition { get; init; } = new();

        /// <summary>The next 3 open appointments.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> UpcomingAppointments { get; init; } = [];

        /// <summary>The newest feedback, if any.</summary>
        public FeedbackItemViewModel? LatestFeedback { get; init; }

        /// <summary>The newest unread notifications.</summary>
        public IReadOnlyList<NotificationItemViewModel> UnreadNotifications { get; init; } = [];

        /// <summary>How many notifications are unread in all.</summary>
        public int UnreadCount { get; init; }
    }
}
