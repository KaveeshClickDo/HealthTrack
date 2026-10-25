using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One weekly window in which a trainer accepts bookings, for example Monday 09:00–12:00.
    /// </summary>
    public class TrainerAvailability
    {
        /// <summary>Primary key.</summary>
        [Key]
        public int AvailabilityId { get; set; }

        /// <summary>The trainer offering the window (R8).</summary>
        public int TrainerId { get; set; }

        /// <summary>The weekday, 0 = Sunday to 6 = Saturday.</summary>
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>When the window opens; unique per trainer and weekday.</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>When the window closes; later than <see cref="StartTime"/>.</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>The trainer offering the window (R8).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>
        /// Whether this window covers a whole appointment: same weekday, starting at or after <see cref="StartTime"/>
        /// and ending at or before <see cref="EndTime"/> (BR-15).
        /// </summary>
        /// <param name="start">The appointment's start.</param>
        /// <param name="minutes">The appointment's length in minutes.</param>
        /// <returns>True when the appointment fits inside the window.</returns>
        public bool Covers(DateTime start, int minutes)
        {
            if (minutes <= 0 || start.DayOfWeek != DayOfWeek)
            {
                return false;
            }

            var startTime = start.TimeOfDay;
            return startTime >= StartTime && startTime + TimeSpan.FromMinutes(minutes) <= EndTime;
        }
    }
}
