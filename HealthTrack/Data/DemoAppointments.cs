using HealthTrack.Models.Entities;

namespace HealthTrack.Data
{
    /// <summary>
    /// The demo appointments <see cref="DemoHistorySeeder"/> creates, kept so their notifications and audit rows can link to them.
    /// </summary>
    internal sealed class DemoAppointments
    {
        /// <summary>client@'s approved appointment within the next 24 hours.</summary>
        public Appointment? Upcoming { get; set; }

        /// <summary>client@'s request waiting for trainer@.</summary>
        public Appointment? Pending { get; set; }

        /// <summary>client@'s rescheduled appointment.</summary>
        public Appointment? Moved { get; set; }

        /// <summary>Where <see cref="Moved"/> was before it was rescheduled.</summary>
        public DateTime MovedFrom { get; set; }

        /// <summary>trainer@'s appointment today.</summary>
        public Appointment? Today { get; set; }

        /// <summary>client4@'s approved follow-up with nutritionist@.</summary>
        public Appointment? KasunFollowUp { get; set; }

        /// <summary>client@'s past appointments: completed, rejected, cancelled by either side and missed.</summary>
        public List<Appointment> ForAmaya { get; } = [];

        /// <summary>The other clients' past appointments.</summary>
        public List<Appointment> Others { get; } = [];

        /// <summary>Every appointment above.</summary>
        public IEnumerable<Appointment> All =>
            ForAmaya.Concat(Others).Concat(new[] { Upcoming, Pending, Moved, Today, KasunFollowUp }.OfType<Appointment>());
    }
}
