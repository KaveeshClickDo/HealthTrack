using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Availability windows, free slots, booking and the appointment lifecycle (spec 1.6, UC13, UC14, UC23–UC26, UC38).
    /// Methods that act on an appointment answer null or false when the user isn't its client or its professional (BR-08);
    /// a stale or clashing change throws <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>.
    /// </summary>
    public interface IAppointmentService
    {
        /// <summary>
        /// The trainer's weekly availability windows, Monday first.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>The page.</returns>
        Task<AvailabilityPageViewModel> GetAvailabilityAsync(string trainerUserId);

        /// <summary>
        /// Adds a weekly window for the signed-in trainer.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="model">The window.</param>
        /// <returns>False when the user has no trainer profile.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-15: the window ends before it starts or overlaps another that day.</exception>
        Task<bool> AddAvailabilityAsync(string trainerUserId, AvailabilityViewModel model);

        /// <summary>
        /// Removes one of the trainer's windows; appointments already booked in it stay.
        /// </summary>
        /// <param name="availabilityId">The window.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>False when the window isn't the trainer's.</returns>
        Task<bool> RemoveAvailabilityAsync(int availabilityId, string trainerUserId);

        /// <summary>
        /// The free start times on a day (UC38): every 15 minutes inside the weekday's windows, where the appointment
        /// ends inside the window, starts 2 hours to 60 days ahead, and overlaps no open appointment of the trainer or
        /// the client (BR-15). Empty for a professional who isn't approved and active.
        /// </summary>
        /// <param name="trainerId">The professional.</param>
        /// <param name="clientId">The client.</param>
        /// <param name="date">The day.</param>
        /// <param name="minutes">The length: 30, 45, 60 or 90.</param>
        /// <param name="excludeAppointmentId">An appointment to leave out, so a reschedule can reuse its own slot.</param>
        /// <returns>The start times, in order.</returns>
        Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(int trainerId, int clientId, DateTime date, int minutes,
            int? excludeAppointmentId = null);

        /// <summary>
        /// The booking form with the professionals the client can book.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="trainerId">A professional to pre-select.</param>
        /// <returns>The form.</returns>
        Task<BookAppointmentViewModel> GetBookFormAsync(int clientId, int? trainerId);

        /// <summary>
        /// Refills the professionals, the date limits and the chosen day's free times after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="clientId">The client.</param>
        /// <returns>A task.</returns>
        Task FillBookOptionsAsync(BookAppointmentViewModel model, int clientId);

        /// <summary>
        /// Books a Pending appointment for the signed-in client. The slot rules are checked again inside a serializable
        /// transaction (BR-15), the professional is notified (BR-18) and the booking is audited.
        /// </summary>
        /// <param name="clientUserId">The signed-in client's user ID.</param>
        /// <param name="model">The form.</param>
        /// <returns>The new appointment's key, or null when the user or the professional doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-03: the professional isn't approved and active; BR-15: the slot isn't free.</exception>
        Task<int?> BookAsync(string clientUserId, BookAppointmentViewModel model);

        /// <summary>
        /// The professional approves a pending request, optionally with a room or meeting link.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="location">The room or meeting link.</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> ApproveAsync(int appointmentId, string trainerUserId, string? location, string rowVersion);

        /// <summary>
        /// The professional rejects a pending request with a reason.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="reason">Why; required (BR-16).</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> RejectAsync(int appointmentId, string trainerUserId, string? reason, string rowVersion);

        /// <summary>
        /// The professional moves a confirmed appointment to another free slot; its own slot doesn't count as taken.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="newStart">The new start.</param>
        /// <param name="minutes">The new length.</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> RescheduleAsync(int appointmentId, string trainerUserId, DateTime newStart, int minutes, string rowVersion);

        /// <summary>
        /// The client (until 2 hours before) or the professional (with a reason, before the start) cancels.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="userId">The signed-in user: the client or the professional.</param>
        /// <param name="reason">Why; required from the professional.</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the user is neither the client nor the professional.</returns>
        Task<bool> CancelAsync(int appointmentId, string userId, string? reason, string rowVersion);

        /// <summary>
        /// The professional records a confirmed appointment as held, after its start, with encrypted notes.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="notes">The consultation notes.</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> CompleteAsync(int appointmentId, string trainerUserId, string? notes, string rowVersion);

        /// <summary>
        /// The professional records that the client didn't attend, after the start.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="rowVersion">The RowVersion the form was shown, base64.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> MarkNoShowAsync(int appointmentId, string trainerUserId, string rowVersion);

        /// <summary>
        /// The user's next open appointments (Pending, Approved, Rescheduled) that haven't started, soonest first.
        /// </summary>
        /// <param name="userId">A client's or a professional's user ID.</param>
        /// <param name="count">How many.</param>
        /// <returns>The appointments.</returns>
        Task<IReadOnlyList<AppointmentListItemViewModel>> GetUpcomingAsync(string userId, int count);

        /// <summary>
        /// The client's appointments: open ones from today on, then the rest.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="page">The page of the past list.</param>
        /// <returns>The page model.</returns>
        Task<ClientAppointmentsViewModel> GetForClientAsync(int clientId, int page);

        /// <summary>
        /// The professional's appointments: pending requests, today and upcoming, then past.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="page">The page of the past list.</param>
        /// <returns>The page model.</returns>
        Task<TrainerAppointmentsViewModel> GetForTrainerAsync(string trainerUserId, int page);

        /// <summary>
        /// One appointment for its client or its professional, with the encrypted reason and notes.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="userId">The signed-in user.</param>
        /// <returns>The page model, or null for anyone else (BR-08).</returns>
        Task<AppointmentDetailsViewModel?> GetDetailsAsync(int appointmentId, string userId);

        /// <summary>
        /// The reschedule form: the chosen day (the appointment's own by default) and length, with that day's free times.
        /// </summary>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="date">The day to show, or null for the appointment's day.</param>
        /// <param name="minutes">The length, or null for the current length.</param>
        /// <returns>The form, or null when the appointment isn't the trainer's.</returns>
        Task<RescheduleViewModel?> GetRescheduleFormAsync(int appointmentId, string trainerUserId, DateTime? date, int? minutes);

        /// <summary>
        /// Refills the reschedule form's display values after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="appointmentId">The appointment.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>False when the appointment isn't the trainer's.</returns>
        Task<bool> FillRescheduleDisplayAsync(RescheduleViewModel model, int appointmentId, string trainerUserId);
    }
}
