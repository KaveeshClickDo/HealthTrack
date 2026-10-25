using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A consultation between a client and a professional. Its status changes only through the lifecycle methods
    /// below (spec 1.6, BR-16, BR-17), so no caller can put an appointment into an impossible state.
    /// </summary>
    public class Appointment
    {
        /// <summary>The lengths a client can book, in minutes (spec 1.6).</summary>
        public static readonly IReadOnlyList<int> AllowedDurations = [30, 45, 60, 90];

        /// <summary>Clients can cancel until this long before the start (BR-17).</summary>
        public static readonly TimeSpan ClientCancellationCutoff = TimeSpan.FromHours(2);

        /// <summary>Primary key.</summary>
        public int AppointmentId { get; set; }

        /// <summary>The client who booked (R28).</summary>
        public int ClientId { get; set; }

        /// <summary>The professional (R29).</summary>
        public int TrainerId { get; set; }

        /// <summary>What the appointment is for.</summary>
        public AppointmentType AppointmentType { get; set; }

        /// <summary>In person or online.</summary>
        public AppointmentMode Mode { get; set; }

        /// <summary>The start; 2 hours to 60 days ahead when booked (BR-15).</summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>Length in minutes: 30, 45, 60 or 90.</summary>
        public int DurationMinutes { get; set; }

        /// <summary>The room, or the meeting link when online.</summary>
        [MaxLength(200)]
        public string? Location { get; set; }

        /// <summary>Why the client booked; encrypted at rest (BR-20).</summary>
        public string? ClientReason { get; set; }

        /// <summary>Where the appointment is in its lifecycle; new bookings are pending.</summary>
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        /// <summary>The professional's consultation notes, written on completion; encrypted at rest (BR-20).</summary>
        public string? ProfessionalNotes { get; set; }

        /// <summary>Why it was cancelled; also holds a rejection reason.</summary>
        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        /// <summary>When the appointment was booked.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the appointment was last changed.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Optimistic concurrency token (SQL Server rowversion), so clashing simultaneous edits are rejected.</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        /// <summary>The client who booked (R28).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The professional (R29).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>When the appointment ends: the start plus its length.</summary>
        [NotMapped]
        public DateTime EndDateTime => StartDateTime.AddMinutes(DurationMinutes);

        /// <summary>
        /// Whether this appointment overlaps a time range. Intervals are half-open, so an appointment ending at 10:00
        /// does not overlap one starting at 10:00 (BR-15).
        /// </summary>
        /// <param name="start">The start of the other range.</param>
        /// <param name="end">The end of the other range.</param>
        /// <returns>True when the two ranges share any time.</returns>
        public bool OverlapsWith(DateTime start, DateTime end) => StartDateTime < end && start < EndDateTime;

        /// <summary>
        /// The professional confirms a pending request: Pending → Approved (BR-16).
        /// </summary>
        /// <exception cref="BusinessRuleException">The appointment is not pending.</exception>
        public void Approve()
        {
            EnsureStatus("Only a pending appointment can be approved.", AppointmentStatus.Pending);
            Status = AppointmentStatus.Approved;
        }

        /// <summary>
        /// The professional turns down a pending request with a reason: Pending → Rejected (BR-16).
        /// </summary>
        /// <param name="reason">Why the request was rejected; required.</param>
        /// <exception cref="BusinessRuleException">The appointment is not pending, or no reason was given.</exception>
        public void Reject(string reason)
        {
            EnsureStatus("Only a pending appointment can be rejected.", AppointmentStatus.Pending);
            CancellationReason = RequireReason(reason, "Give a reason for rejecting the appointment.");
            Status = AppointmentStatus.Rejected;
        }

        /// <summary>
        /// The professional moves a confirmed appointment: Approved or Rescheduled → Rescheduled (BR-16).
        /// The service checks beforehand that the new slot passes the BR-15 booking rules.
        /// </summary>
        /// <param name="newStart">The new start.</param>
        /// <param name="minutes">The new length: 30, 45, 60 or 90 minutes.</param>
        /// <exception cref="BusinessRuleException">The appointment is not confirmed, or the length isn't allowed.</exception>
        public void Reschedule(DateTime newStart, int minutes)
        {
            EnsureStatus("Only an approved or rescheduled appointment can be rescheduled.",
                AppointmentStatus.Approved, AppointmentStatus.Rescheduled);

            if (!AllowedDurations.Contains(minutes))
            {
                throw new BusinessRuleException("BR-15", "Appointments last 30, 45, 60 or 90 minutes.");
            }

            StartDateTime = newStart;
            DurationMinutes = minutes;
            Status = AppointmentStatus.Rescheduled;
        }

        /// <summary>
        /// The client cancels until 2 hours before the start: Pending, Approved or Rescheduled → CancelledByClient (BR-16, BR-17).
        /// </summary>
        /// <param name="reason">Why the client cancelled; optional.</param>
        /// <param name="now">The current time.</param>
        /// <exception cref="BusinessRuleException">The appointment can no longer change, or it starts in less than 2 hours.</exception>
        public void CancelByClient(string? reason, DateTime now)
        {
            EnsureStatus("This appointment can no longer be cancelled.",
                AppointmentStatus.Pending, AppointmentStatus.Approved, AppointmentStatus.Rescheduled);

            // BR-17: exactly 2 hours before the start is still allowed.
            if (now > StartDateTime - ClientCancellationCutoff)
            {
                throw new BusinessRuleException("BR-17", "Appointments can only be cancelled up to 2 hours before the start.");
            }

            CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            Status = AppointmentStatus.CancelledByClient;
        }

        /// <summary>
        /// The professional cancels a confirmed appointment before its start, with a reason:
        /// Approved or Rescheduled → CancelledByTrainer (BR-16, BR-17).
        /// </summary>
        /// <param name="reason">Why the professional cancelled; required.</param>
        /// <param name="now">The current time.</param>
        /// <exception cref="BusinessRuleException">The appointment is not confirmed, no reason was given, or it has started.</exception>
        public void CancelByTrainer(string reason, DateTime now)
        {
            EnsureStatus("Only an approved or rescheduled appointment can be cancelled.",
                AppointmentStatus.Approved, AppointmentStatus.Rescheduled);
            var trimmedReason = RequireReason(reason, "Give a reason for cancelling the appointment.");

            if (now >= StartDateTime)
            {
                throw new BusinessRuleException("BR-17", "An appointment that has already started cannot be cancelled.");
            }

            CancellationReason = trimmedReason;
            Status = AppointmentStatus.CancelledByTrainer;
        }

        /// <summary>
        /// The professional records a confirmed appointment as held, once it has started: Approved or Rescheduled → Completed
        /// (BR-16, BR-17). The notes are encrypted at rest by the value converter.
        /// </summary>
        /// <param name="notes">The professional's consultation notes; optional.</param>
        /// <param name="now">The current time.</param>
        /// <exception cref="BusinessRuleException">The appointment is not confirmed, or it hasn't started yet.</exception>
        public void Complete(string? notes, DateTime now)
        {
            EnsureStatus("Only an approved or rescheduled appointment can be marked as completed.",
                AppointmentStatus.Approved, AppointmentStatus.Rescheduled);
            EnsureStarted(now, "An appointment can only be completed once it has started.");

            ProfessionalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            Status = AppointmentStatus.Completed;
        }

        /// <summary>
        /// The professional records that the client didn't attend, once the start has passed:
        /// Approved or Rescheduled → NoShow (BR-16, BR-17).
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <exception cref="BusinessRuleException">The appointment is not confirmed, or it hasn't started yet.</exception>
        public void MarkNoShow(DateTime now)
        {
            EnsureStatus("Only an approved or rescheduled appointment can be marked as a no-show.",
                AppointmentStatus.Approved, AppointmentStatus.Rescheduled);
            EnsureStarted(now, "An appointment can only be marked as a no-show once it has started.");

            Status = AppointmentStatus.NoShow;
        }

        // BR-16: only the transitions of the lifecycle table in spec 1.6 are allowed.
        private void EnsureStatus(string message, params AppointmentStatus[] allowedFrom)
        {
            if (!allowedFrom.Contains(Status))
            {
                throw new BusinessRuleException("BR-16", message);
            }
        }

        // BR-17: Completed and No-show only from the start time on.
        private void EnsureStarted(DateTime now, string message)
        {
            if (now < StartDateTime)
            {
                throw new BusinessRuleException("BR-17", message);
            }
        }

        // BR-16: rejections and trainer cancellations need a reason.
        private static string RequireReason(string? reason, string message)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessRuleException("BR-16", message);
            }

            return reason.Trim();
        }
    }
}
