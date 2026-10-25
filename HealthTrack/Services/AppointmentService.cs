using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Availability, free slots, booking and the appointment lifecycle. Every status change goes through the
    /// Appointment methods, notifies the other party (BR-18) and is audited.
    /// </summary>
    public class AppointmentService : IAppointmentService
    {
        /// <summary>What a stale form or a clashing simultaneous change is told (spec 1.6, RowVersion).</summary>
        public const string ConcurrencyMessage = "This appointment was changed by someone else. Reload and try again.";

        /// <summary>What a booking or reschedule is told when another appointment of the professional took the time first (BR-15).</summary>
        public const string SlotTakenMessage = "That time has just been taken – please pick another.";

        // How often a booking that lost a deadlock runs again before giving up with SlotTakenMessage.
        private const int SlotSaveAttempts = 3;

        // BR-15: start times every 15 minutes, 2 hours to 60 days ahead.
        private const int MaximumDaysAhead = 60;
        private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(2);

        // The longest appointment, so an overlap search can start this far before a range.
        private static readonly TimeSpan LongestAppointment = TimeSpan.FromMinutes(Appointment.AllowedDurations.Max());

        // Pending, Approved and Rescheduled can still change and hold their slot (spec 1.6).
        private static readonly AppointmentStatus[] OpenStatuses =
            [AppointmentStatus.Pending, AppointmentStatus.Approved, AppointmentStatus.Rescheduled];

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="notifications">Tells the other party about every change (BR-18).</param>
        /// <param name="audit">Records bookings, decisions and availability changes.</param>
        /// <param name="clock">Supplies the time.</param>
        public AppointmentService(ApplicationDbContext db, INotificationService notifications, IAuditService audit, IClock clock)
        {
            _db = db;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<AvailabilityPageViewModel> GetAvailabilityAsync(string trainerUserId)
        {
            var trainerId = await TrainerIdAsync(trainerUserId) ?? 0;
            var windows = await _db.TrainerAvailabilities.AsNoTracking().Where(a => a.TrainerId == trainerId).ToListAsync();

            return new AvailabilityPageViewModel
            {
                Windows = windows
                    .OrderBy(w => MondayFirst(w.DayOfWeek))
                    .ThenBy(w => w.StartTime)
                    .Select(w => new AvailabilityWindowViewModel
                    {
                        AvailabilityId = w.AvailabilityId,
                        DayOfWeek = w.DayOfWeek,
                        StartTime = w.StartTime,
                        EndTime = w.EndTime
                    })
                    .ToList()
            };
        }

        /// <inheritdoc />
        public async Task<bool> AddAvailabilityAsync(string trainerUserId, AvailabilityViewModel model)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return false;
            }

            var day = model.DayOfWeek!.Value;
            var start = model.StartTime!.Value;
            var end = model.EndTime!.Value;
            if (end <= start)
            {
                throw new BusinessRuleException("BR-15", "The end time must be after the start time.");
            }

            // Spec 2.4: windows on one day can't overlap (half-open, so 12:00–14:00 may follow 09:00–12:00).
            var clash = (await _db.TrainerAvailabilities.AsNoTracking()
                    .Where(a => a.TrainerId == trainerId && a.DayOfWeek == day)
                    .ToListAsync())
                .FirstOrDefault(a => a.StartTime < end && start < a.EndTime);
            if (clash is not null)
            {
                throw new BusinessRuleException("BR-15",
                    $"That overlaps your {day} window {Time(clash.StartTime)}–{Time(clash.EndTime)}.");
            }

            var window = new TrainerAvailability { TrainerId = trainerId, DayOfWeek = day, StartTime = start, EndTime = end };
            _db.TrainerAvailabilities.Add(window);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Create, nameof(TrainerAvailability), Key(window.AvailabilityId),
                $"Availability added: {day} {Time(start)}–{Time(end)}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAvailabilityAsync(int availabilityId, string trainerUserId)
        {
            var trainerId = await TrainerIdAsync(trainerUserId);
            var window = await _db.TrainerAvailabilities
                .FirstOrDefaultAsync(a => a.AvailabilityId == availabilityId && a.TrainerId == trainerId);
            if (window is null)
            {
                return false;
            }

            // Appointments already booked in the window keep their time; only new bookings stop.
            _db.TrainerAvailabilities.Remove(window);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Delete, nameof(TrainerAvailability), Key(availabilityId),
                $"Availability removed: {window.DayOfWeek} {Time(window.StartTime)}–{Time(window.EndTime)}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(int trainerId, int clientId, DateTime date, int minutes,
            int? excludeAppointmentId = null)
        {
            if (!Appointment.AllowedDurations.Contains(minutes))
            {
                return [];
            }

            var trainer = await _db.TrainerProfiles.AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.Availabilities)
                .FirstOrDefaultAsync(t => t.TrainerId == trainerId);
            if (trainer is null || !IsBookable(trainer))
            {
                return [];
            }

            var day = date.Date;
            var windows = trainer.Availabilities.Where(a => a.DayOfWeek == day.DayOfWeek).OrderBy(a => a.StartTime).ToList();
            if (windows.Count == 0)
            {
                return [];
            }

            var taken = await OpenAppointmentsAsync(trainerId, clientId, day, day.AddDays(1), excludeAppointmentId);
            var now = _clock.Now;
            var earliest = now + MinimumNotice;
            var latest = now.AddDays(MaximumDaysAhead);
            var length = TimeSpan.FromMinutes(minutes);

            var slots = new List<DateTime>();
            foreach (var window in windows)
            {
                // BR-15: the whole appointment fits inside the window.
                for (var time = window.StartTime; time + length <= window.EndTime; time += SlotStep)
                {
                    var start = day + time;
                    if (start >= earliest && start <= latest && !taken.Any(a => a.OverlapsWith(start, start + length)))
                    {
                        slots.Add(start);
                    }
                }
            }

            return slots;
        }

        /// <inheritdoc />
        public async Task<BookAppointmentViewModel> GetBookFormAsync(int clientId, int? trainerId)
        {
            var model = new BookAppointmentViewModel { TrainerId = trainerId, Mode = AppointmentMode.InPerson };
            await FillBookOptionsAsync(model, clientId);

            var chosen = model.Professionals.FirstOrDefault(p => p.TrainerId == trainerId);
            model.TrainerId = chosen?.TrainerId;
            model.AppointmentType = chosen?.SuggestedType;
            return model;
        }

        /// <inheritdoc />
        public async Task FillBookOptionsAsync(BookAppointmentViewModel model, int clientId)
        {
            var today = _clock.Today;
            var myTrainerId = await _db.TrainerAssignments.AsNoTracking()
                .Where(a => a.ClientId == clientId && a.EndDate == null)
                .Select(a => (int?)a.TrainerId)
                .FirstOrDefaultAsync();

            // Only approved professionals whose accounts are active can be booked.
            var professionals = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.ApprovedAt != null && t.User.AccountStatus == AccountStatus.Active)
                .Select(t => new ProfessionalOptionViewModel
                {
                    TrainerId = t.TrainerId,
                    Name = t.User.FirstName + " " + t.User.LastName,
                    ProfessionalType = t.ProfessionalType,
                    Specialization = t.Specialization,
                    YearsOfExperience = t.YearsOfExperience,
                    Bio = t.Bio,
                    IsMyTrainer = t.TrainerId == myTrainerId
                })
                .ToListAsync();

            model.Professionals = professionals
                .OrderByDescending(p => p.IsMyTrainer)
                .ThenBy(p => p.ProfessionalType)
                .ThenBy(p => p.Name)
                .ToList();
            model.EarliestDate = today;
            model.LatestDate = today.AddDays(MaximumDaysAhead);
            model.Slots = model is { TrainerId: { } trainerId, Date: { } date, DurationMinutes: { } minutes }
                ? (await GetAvailableSlotsAsync(trainerId, clientId, date, minutes)).Select(s => s.TimeOfDay).ToList()
                : [];
        }

        /// <inheritdoc />
        public async Task<int?> BookAsync(string clientUserId, BookAppointmentViewModel model)
        {
            var client = await _db.ClientProfiles.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == clientUserId);
            var trainer = await _db.TrainerProfiles.AsNoTracking().Include(t => t.User).FirstOrDefaultAsync(t => t.TrainerId == model.TrainerId);
            if (client is null || trainer is null)
            {
                return null;
            }

            if (!IsBookable(trainer))
            {
                throw new BusinessRuleException("BR-03", "This professional isn't taking bookings.");
            }

            var start = model.StartDateTime!.Value;
            var minutes = model.DurationMinutes!.Value;
            var appointment = new Appointment
            {
                ClientId = client.ClientId,
                TrainerId = trainer.TrainerId,
                AppointmentType = model.AppointmentType!.Value,
                Mode = model.Mode!.Value,
                StartDateTime = start,
                DurationMinutes = minutes,
                ClientReason = model.ClientReason.NullIfBlank(),
                Status = AppointmentStatus.Pending,
                CreatedAt = _clock.Now
            };

            _db.Appointments.Add(appointment);
            await SaveInFreeSlotAsync(appointment, trainer.User.FullName, excludeAppointmentId: null, byClient: true);

            await NotifyAsync(trainer.UserId, toTrainer: true, appointment.AppointmentId, "New appointment request",
                $"{client.User.FullName} asked for {Describe(appointment)}, {minutes} minutes.");
            await _audit.LogAsync(clientUserId, AuditAction.Create, nameof(Appointment), Key(appointment.AppointmentId),
                $"Appointment requested with {trainer.User.FullName} for {When(start)}.");
            return appointment.AppointmentId;
        }

        /// <inheritdoc />
        public async Task<bool> ApproveAsync(int appointmentId, string trainerUserId, string? location, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, trainerUserId, trainerOnly: true);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            appointment.Approve();
            appointment.Location = location.NullIfBlank();
            await SaveAsync(appointment);

            await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Appointment approved",
                $"{appointment.Trainer.User.FullName} approved your {Describe(appointment)}."
                + (appointment.Location is { } where ? $" Where: {where}" : string.Empty));
            await _audit.LogAsync(trainerUserId, AuditAction.Approve, nameof(Appointment), Key(appointmentId), "Appointment approved.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RejectAsync(int appointmentId, string trainerUserId, string? reason, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, trainerUserId, trainerOnly: true);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            appointment.Reject(reason ?? string.Empty);
            await SaveAsync(appointment);

            await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Appointment request declined",
                $"{appointment.Trainer.User.FullName} declined your {Describe(appointment)}. Reason: {appointment.CancellationReason}");
            await _audit.LogAsync(trainerUserId, AuditAction.Reject, nameof(Appointment), Key(appointmentId), "Appointment rejected.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RescheduleAsync(int appointmentId, string trainerUserId, DateTime newStart, int minutes, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, trainerUserId, trainerOnly: true);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            var oldStart = appointment.StartDateTime;

            // The entity checks the lifecycle (BR-16) and the length; then the new slot must pass BR-15, leaving out the
            // appointment's own current slot.
            appointment.Reschedule(newStart, minutes);
            appointment.UpdatedAt = _clock.Now;
            await SaveInFreeSlotAsync(appointment, appointment.Trainer.User.FullName, appointment.AppointmentId, byClient: false);

            await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Appointment moved",
                $"{appointment.Trainer.User.FullName} moved your appointment from {When(oldStart)} to {When(newStart)} ({minutes} minutes).");
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(Appointment), Key(appointmentId),
                $"Appointment rescheduled from {When(oldStart)} to {When(newStart)}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> CancelAsync(int appointmentId, string userId, string? reason, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, userId, trainerOnly: false);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            var byClient = appointment.Client.UserId == userId;
            var now = _clock.Now;
            if (byClient)
            {
                // BR-17: until 2 hours before the start; the reason is optional.
                appointment.CancelByClient(reason, now);
            }
            else
            {
                // BR-16, BR-17: a confirmed appointment, before its start, with a reason.
                appointment.CancelByTrainer(reason ?? string.Empty, now);
            }

            await SaveAsync(appointment);

            var reasonText = appointment.CancellationReason is { } why ? $" Reason: {why}" : string.Empty;
            if (byClient)
            {
                await NotifyAsync(appointment.Trainer.UserId, toTrainer: true, appointmentId, "Appointment cancelled",
                    $"{appointment.Client.User.FullName} cancelled the {Describe(appointment)}.{reasonText}");
            }
            else
            {
                await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Appointment cancelled",
                    $"{appointment.Trainer.User.FullName} cancelled your {Describe(appointment)}.{reasonText}");
            }

            await _audit.LogAsync(userId, AuditAction.Cancel, nameof(Appointment), Key(appointmentId),
                $"Appointment cancelled by the {(byClient ? "client" : "professional")}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> CompleteAsync(int appointmentId, string trainerUserId, string? notes, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, trainerUserId, trainerOnly: true);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            // BR-17: only once it has started; the notes are encrypted by the value converter (BR-20).
            appointment.Complete(notes, _clock.Now);
            await SaveAsync(appointment);

            await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Appointment completed",
                $"{appointment.Trainer.User.FullName} marked your {Describe(appointment)} as completed.");
            // No notes in the audit row: they are health information.
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(Appointment), Key(appointmentId), "Appointment completed.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> MarkNoShowAsync(int appointmentId, string trainerUserId, string rowVersion)
        {
            var appointment = await LoadAsync(appointmentId, trainerUserId, trainerOnly: true);
            if (appointment is null)
            {
                return false;
            }

            EnsureFresh(appointment, rowVersion);
            appointment.MarkNoShow(_clock.Now);
            await SaveAsync(appointment);

            await NotifyAsync(appointment.Client.UserId, toTrainer: false, appointmentId, "Missed appointment",
                $"{appointment.Trainer.User.FullName} recorded your {Describe(appointment)} as missed.");
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(Appointment), Key(appointmentId), "Appointment marked as a no-show.");
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AppointmentListItemViewModel>> GetUpcomingAsync(string userId, int count)
        {
            var now = _clock.Now;
            var appointments = await WithPeople()
                .Where(a => (a.Client.UserId == userId || a.Trainer.UserId == userId)
                    && OpenStatuses.Contains(a.Status) && a.StartDateTime >= now)
                .OrderBy(a => a.StartDateTime)
                .Take(count)
                .ToListAsync();
            return appointments.Select(a => Item(a, viewerIsTrainer: a.Trainer.UserId == userId, now)).ToList();
        }

        /// <inheritdoc />
        public async Task<ClientAppointmentsViewModel> GetForClientAsync(int clientId, int page)
        {
            var now = _clock.Now;
            var today = _clock.Today;
            var mine = WithPeople().Where(a => a.ClientId == clientId);

            var upcoming = await mine
                .Where(a => OpenStatuses.Contains(a.Status) && a.StartDateTime >= today)
                .OrderBy(a => a.StartDateTime)
                .ToListAsync();
            var past = await mine
                .Where(a => !(OpenStatuses.Contains(a.Status) && a.StartDateTime >= today))
                .OrderByDescending(a => a.StartDateTime)
                .ToPagedResultAsync(page);

            return new ClientAppointmentsViewModel
            {
                Upcoming = upcoming.Select(a => Item(a, viewerIsTrainer: false, now)).ToList(),
                Past = Map(past, viewerIsTrainer: false, now)
            };
        }

        /// <inheritdoc />
        public async Task<TrainerAppointmentsViewModel> GetForTrainerAsync(string trainerUserId, int page)
        {
            var now = _clock.Now;
            var today = _clock.Today;
            var trainerId = await TrainerIdAsync(trainerUserId) ?? 0;
            var mine = WithPeople().Where(a => a.TrainerId == trainerId);

            var pending = await mine.Where(a => a.Status == AppointmentStatus.Pending).OrderBy(a => a.StartDateTime).ToListAsync();
            var upcoming = await mine
                .Where(a => a.Status != AppointmentStatus.Pending && a.StartDateTime >= today)
                .OrderBy(a => a.StartDateTime)
                .ToListAsync();
            var past = await mine
                .Where(a => a.Status != AppointmentStatus.Pending && a.StartDateTime < today)
                .OrderByDescending(a => a.StartDateTime)
                .ToPagedResultAsync(page);

            return new TrainerAppointmentsViewModel
            {
                Pending = pending.Select(a => Item(a, viewerIsTrainer: true, now)).ToList(),
                Upcoming = upcoming.Select(a => Item(a, viewerIsTrainer: true, now)).ToList(),
                Past = Map(past, viewerIsTrainer: true, now),
                Today = today
            };
        }

        /// <inheritdoc />
        public async Task<AppointmentDetailsViewModel?> GetDetailsAsync(int appointmentId, string userId)
        {
            // BR-08, BR-20: only the client and the appointment's own professional see it, reason and notes included.
            var appointment = await WithPeople()
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && (a.Client.UserId == userId || a.Trainer.UserId == userId));
            if (appointment is null)
            {
                return null;
            }

            var now = _clock.Now;
            var viewerIsTrainer = appointment.Trainer.UserId == userId;
            var confirmed = appointment.Status is AppointmentStatus.Approved or AppointmentStatus.Rescheduled;
            return new AppointmentDetailsViewModel
            {
                Appointment = Item(appointment, viewerIsTrainer, now),
                ViewerIsTrainer = viewerIsTrainer,
                ProfessionalNotes = appointment.ProfessionalNotes,
                CreatedAt = appointment.CreatedAt,
                UpdatedAt = appointment.UpdatedAt,
                CanDecide = viewerIsTrainer && appointment.Status == AppointmentStatus.Pending,
                CanReschedule = viewerIsTrainer && confirmed && now < appointment.StartDateTime,
                CanRecordOutcome = viewerIsTrainer && confirmed && now >= appointment.StartDateTime,
                Action = new AppointmentActionViewModel { RowVersion = Convert.ToBase64String(appointment.RowVersion) }
            };
        }

        /// <inheritdoc />
        public async Task<RescheduleViewModel?> GetRescheduleFormAsync(int appointmentId, string trainerUserId, DateTime? date, int? minutes)
        {
            var appointment = await WithPeople()
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.Trainer.UserId == trainerUserId);
            if (appointment is null)
            {
                return null;
            }

            var model = new RescheduleViewModel
            {
                RowVersion = Convert.ToBase64String(appointment.RowVersion),
                Date = (date ?? appointment.StartDateTime).Date,
                DurationMinutes = minutes is { } length && Appointment.AllowedDurations.Contains(length) ? length : appointment.DurationMinutes
            };
            await FillRescheduleDisplayAsync(model, appointment);
            return model;
        }

        /// <inheritdoc />
        public async Task<bool> FillRescheduleDisplayAsync(RescheduleViewModel model, int appointmentId, string trainerUserId)
        {
            var appointment = await WithPeople()
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.Trainer.UserId == trainerUserId);
            if (appointment is null)
            {
                return false;
            }

            await FillRescheduleDisplayAsync(model, appointment);
            return true;
        }

        // The reschedule page's display values: the appointment, the date limits and the day's free times, leaving out
        // the appointment's own slot (UC25).
        private async Task FillRescheduleDisplayAsync(RescheduleViewModel model, Appointment appointment)
        {
            var today = _clock.Today;
            model.Appointment = Item(appointment, viewerIsTrainer: true, _clock.Now);
            model.EarliestDate = today;
            model.LatestDate = today.AddDays(MaximumDaysAhead);
            model.Slots = model is { Date: { } date, DurationMinutes: { } minutes }
                ? (await GetAvailableSlotsAsync(appointment.TrainerId, appointment.ClientId, date, minutes, appointment.AppointmentId))
                    .Select(s => s.TimeOfDay).ToList()
                : [];
        }

        // BR-15, checked when booking and rescheduling: an allowed length, 2 hours to 60 days ahead, inside one of the
        // professional's windows, and no overlap with an open appointment of the professional or the client.
        private async Task EnsureSlotAsync(int trainerId, string trainerName, int clientId, DateTime start, int minutes,
            int? excludeAppointmentId, bool byClient)
        {
            if (!Appointment.AllowedDurations.Contains(minutes))
            {
                throw new BusinessRuleException("BR-15", "Appointments last 30, 45, 60 or 90 minutes.");
            }

            var now = _clock.Now;
            if (start < now + MinimumNotice)
            {
                throw new BusinessRuleException("BR-15", "Appointments must start at least 2 hours from now.");
            }

            if (start > now.AddDays(MaximumDaysAhead))
            {
                throw new BusinessRuleException("BR-15", "Appointments can be booked up to 60 days ahead.");
            }

            var windows = await _db.TrainerAvailabilities.AsNoTracking()
                .Where(a => a.TrainerId == trainerId && a.DayOfWeek == start.DayOfWeek)
                .ToListAsync();
            if (!windows.Any(w => w.Covers(start, minutes)))
            {
                throw new BusinessRuleException("BR-15", $"That time is outside {trainerName}'s availability.");
            }

            var end = start.AddMinutes(minutes);
            var clash = (await OpenAppointmentsAsync(trainerId, clientId, start, end, excludeAppointmentId))
                .FirstOrDefault(a => a.OverlapsWith(start, end));
            if (clash is not null)
            {
                throw new BusinessRuleException("BR-15", clash.TrainerId == trainerId
                    ? SlotTakenMessage
                    : byClient
                        ? "You already have another appointment at that time."
                        : "The client already has another appointment at that time.");
            }
        }

        // BR-15: the slot is checked again and the booking or move saved inside one serializable transaction, so two people
        // can't take the same time. When two run at the same moment, both read the free range, both try to write, and
        // SQL Server breaks the deadlock by rolling one back (error 1205) instead of letting both through. That one runs
        // again: the fresh check then finds the time taken and says so, or saves when the two didn't actually clash.
        private async Task SaveInFreeSlotAsync(Appointment appointment, string trainerName, int? excludeAppointmentId, bool byClient)
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        await using var transaction = await _db.BeginSerializableTransactionAsync();
                        await EnsureSlotAsync(appointment.TrainerId, trainerName, appointment.ClientId, appointment.StartDateTime,
                            appointment.DurationMinutes, excludeAppointmentId, byClient);
                        await _db.SaveChangesAsync();
                        if (transaction is not null)
                        {
                            await transaction.CommitAsync();
                        }

                        return;
                    }
                    catch (Exception ex) when (SqlErrors.IsDeadlock(ex))
                    {
                        if (attempt == SlotSaveAttempts)
                        {
                            throw new BusinessRuleException("BR-15", SlotTakenMessage);
                        }
                    }
                }
            }
            catch
            {
                // Nothing was saved: forget the change, so no later SaveChanges in this request can save it by accident.
                _db.Entry(appointment).State = EntityState.Detached;
                throw;
            }
        }

        // Open appointments of the professional or the client that could overlap [from, to).
        private Task<List<Appointment>> OpenAppointmentsAsync(int trainerId, int clientId, DateTime from, DateTime to,
            int? excludeAppointmentId)
        {
            var earliestStart = from - LongestAppointment;
            return _db.Appointments.AsNoTracking()
                .Where(a => (a.TrainerId == trainerId || a.ClientId == clientId)
                    && OpenStatuses.Contains(a.Status)
                    && a.StartDateTime < to && a.StartDateTime > earliestStart
                    && a.AppointmentId != excludeAppointmentId)
                .ToListAsync();
        }

        // The appointment for its professional (trainerOnly) or for either party, tracked for a change.
        private Task<Appointment?> LoadAsync(int appointmentId, string userId, bool trainerOnly) =>
            _db.Appointments
                .Include(a => a.Client).ThenInclude(c => c.User)
                .Include(a => a.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId
                    && (a.Trainer.UserId == userId || (!trainerOnly && a.Client.UserId == userId)));

        // Spec 1.6, RowVersion: a stale form is turned away before any lifecycle rule runs; the posted value then becomes
        // the original value, so a change saved in between makes SaveChanges throw DbUpdateConcurrencyException.
        private void EnsureFresh(Appointment appointment, string rowVersion)
        {
            byte[] posted;
            try
            {
                posted = Convert.FromBase64String(rowVersion);
            }
            catch (FormatException)
            {
                posted = [];
            }

            if (!posted.AsSpan().SequenceEqual(appointment.RowVersion))
            {
                throw new DbUpdateConcurrencyException(ConcurrencyMessage);
            }

            _db.Entry(appointment).Property(a => a.RowVersion).OriginalValue = posted;
        }

        private async Task SaveAsync(Appointment appointment)
        {
            appointment.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
        }

        // BR-18: every change tells the other party, with a link to the appointment.
        private Task NotifyAsync(string userId, bool toTrainer, int appointmentId, string title, string message) =>
            _notifications.NotifyAsync(userId, NotificationType.AppointmentUpdate, title, message,
                (toTrainer ? "/Trainer" : "/Client") + "/Appointments/Details/" + Key(appointmentId));

        private IQueryable<Appointment> WithPeople() =>
            _db.Appointments.AsNoTracking()
                .Include(a => a.Client).ThenInclude(c => c.User)
                .Include(a => a.Trainer).ThenInclude(t => t.User);

        private Task<int?> TrainerIdAsync(string trainerUserId) =>
            _db.TrainerProfiles.Where(t => t.UserId == trainerUserId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();

        private static PagedResult<AppointmentListItemViewModel> Map(PagedResult<Appointment> page, bool viewerIsTrainer, DateTime now) =>
            new(page.Items.Select(a => Item(a, viewerIsTrainer, now)).ToList(), page.Page, page.PageSize, page.TotalCount);

        private static AppointmentListItemViewModel Item(Appointment a, bool viewerIsTrainer, DateTime now) => new()
        {
            AppointmentId = a.AppointmentId,
            StartDateTime = a.StartDateTime,
            DurationMinutes = a.DurationMinutes,
            AppointmentType = a.AppointmentType,
            Mode = a.Mode,
            Status = a.Status,
            ClientName = a.Client.User.FullName,
            TrainerName = a.Trainer.User.FullName,
            ProfessionalType = a.Trainer.ProfessionalType,
            Location = a.Location,
            CancellationReason = a.CancellationReason,
            ClientReason = a.ClientReason,
            RowVersion = Convert.ToBase64String(a.RowVersion),
            // The same rules as CancelByClient and CancelByTrainer, so the button only shows when it would work.
            CanCancel = viewerIsTrainer
                ? a.Status is AppointmentStatus.Approved or AppointmentStatus.Rescheduled && now < a.StartDateTime
                : OpenStatuses.Contains(a.Status) && now <= a.StartDateTime - Appointment.ClientCancellationCutoff
        };

        // BR-07, BR-03: only approved professionals with an active account take bookings.
        private static bool IsBookable(TrainerProfile trainer) =>
            trainer.IsApproved && trainer.User.AccountStatus == AccountStatus.Active;

        private static string Describe(Appointment a) =>
            $"{a.AppointmentType.GetDisplayName().ToLowerInvariant()} on {When(a.StartDateTime)}";

        private static string When(DateTime start) => start.ToString("dd MMM yyyy 'at' HH:mm", CultureInfo.CurrentCulture);

        private static string Time(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        // System.DayOfWeek starts on Sunday; the app's weeks start on Monday.
        private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

        private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}
