using HealthTrack.Models.Enums;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// The photo shown for an exercise (thumbnails in lists, tiles on the dashboard). The library's exercises map by
    /// name to a photo in wwwroot/images/exercises; an exercise an administrator adds later gets its category's photo.
    /// The photos are free Unsplash photos, credited in wwwroot/images/CREDITS.md.
    /// </summary>
    public static class ExercisePhotos
    {
        private static readonly Dictionary<string, string> ByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tai chi"] = "tai-chi",
            ["Bosu ball squat"] = "bosu-ball-squat",
            ["Single-leg balance"] = "single-leg-balance",
            ["Elliptical trainer"] = "elliptical-trainer",
            ["Rowing machine"] = "rowing-machine",
            ["Swimming (freestyle)"] = "swimming",
            ["Brisk walking"] = "brisk-walking",
            ["Jump rope"] = "jump-rope",
            ["Running (8 km/h)"] = "running-treadmill",
            ["Running (10 km/h)"] = "running-road",
            ["Stair climber"] = "stair-climber",
            ["Stationary cycling"] = "stationary-cycling",
            ["Pilates"] = "pilates",
            ["Full-body stretching"] = "stretching",
            ["Hatha yoga"] = "yoga",
            ["Mountain climbers"] = "mountain-climbers",
            ["Burpees"] = "burpees",
            ["Circuit training"] = "circuit-training",
            ["Jumping jacks"] = "jumping-jacks",
            ["Kettlebell swing"] = "kettlebell-swing",
            ["Boxing (punch bag)"] = "boxing",
            ["Badminton"] = "badminton",
            ["Basketball"] = "basketball",
            ["Tennis"] = "tennis",
            ["Football (casual)"] = "football",
            ["Dumbbell biceps curl"] = "biceps-curl",
            ["Triceps dip"] = "triceps-dip",
            ["Deadlift"] = "deadlift",
            ["Lat pulldown"] = "lat-pulldown",
            ["Pull-up"] = "pull-up",
            ["Seated cable row"] = "seated-cable-row",
            ["Bench press"] = "bench-press",
            ["Push-up"] = "push-up",
            ["Abdominal crunch"] = "crunch",
            ["Plank"] = "plank",
            ["Barbell back squat"] = "back-squat",
            ["Leg press"] = "leg-press",
            ["Walking lunge"] = "walking-lunge",
            ["Dumbbell shoulder press"] = "shoulder-press",
            ["Lateral raise"] = "lateral-raise"
        };

        private static readonly Dictionary<ExerciseCategory, string> ByCategory = new()
        {
            [ExerciseCategory.Cardio] = "running-road",
            [ExerciseCategory.Strength] = "back-squat",
            [ExerciseCategory.Flexibility] = "stretching",
            [ExerciseCategory.HIIT] = "circuit-training",
            [ExerciseCategory.Balance] = "single-leg-balance",
            [ExerciseCategory.Sports] = "tennis"
        };

        /// <summary>The app-relative URL of the exercise's photo.</summary>
        /// <param name="name">The exercise's name.</param>
        /// <param name="category">Its category, used when the name has no photo of its own.</param>
        /// <returns>A path such as "/images/exercises/push-up.jpg".</returns>
        public static string For(string? name, ExerciseCategory category = ExerciseCategory.Strength)
        {
            var slug = name is not null && ByName.TryGetValue(name.Trim(), out var byName)
                ? byName
                : ByCategory.GetValueOrDefault(category, "back-squat");
            return $"/images/exercises/{slug}.jpg";
        }

        /// <summary>The app-relative URL of one of the large photos used on the public pages.</summary>
        /// <param name="name">treadmill, trail-run, kettlebell or rowing.</param>
        /// <returns>A path such as "/images/hero/treadmill.jpg".</returns>
        public static string Hero(string name) => $"/images/hero/{name}.jpg";
    }
}
