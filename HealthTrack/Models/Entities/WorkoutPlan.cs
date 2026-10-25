using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A client's workout plan, or a reusable routine template (IsTemplate, no client).
    /// </summary>
    public class WorkoutPlan
    {
        /// <summary>Primary key.</summary>
        public int WorkoutPlanId { get; set; }

        /// <summary>The trainer who wrote the plan (R11).</summary>
        public int TrainerId { get; set; }

        /// <summary>The client following the plan; null for templates (R12).</summary>
        public int? ClientId { get; set; }

        /// <summary>The template this plan was copied from (R13).</summary>
        public int? SourceTemplateId { get; set; }

        /// <summary>Title.</summary>
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Goal and guidance.</summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>How demanding the plan is.</summary>
        public DifficultyLevel Difficulty { get; set; }

        /// <summary>True exactly when <see cref="ClientId"/> is null (check constraint).</summary>
        public bool IsTemplate { get; set; }

        /// <summary>First day of the plan; required for client plans.</summary>
        [Column(TypeName = "date")]
        public DateTime? StartDate { get; set; }

        /// <summary>Last day of the plan; on or after <see cref="StartDate"/> (BR-10).</summary>
        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }

        /// <summary>Where the plan is in its lifecycle; new plans start as drafts.</summary>
        public PlanStatus Status { get; set; } = PlanStatus.Draft;

        /// <summary>When the plan was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the plan was last changed.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>The trainer who wrote the plan (R11).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>The client following the plan; null for templates (R12).</summary>
        public virtual ClientProfile? Client { get; set; }

        /// <summary>The template this plan was copied from (R13).</summary>
        public virtual WorkoutPlan? SourceTemplate { get; set; }

        /// <summary>Client plans copied from this template (R13).</summary>
        public virtual ICollection<WorkoutPlan> Copies { get; set; } = new List<WorkoutPlan>();

        /// <summary>The plan's lines: one exercise on one weekday each (R14).</summary>
        public virtual ICollection<WorkoutPlanExercise> Exercises { get; set; } = new List<WorkoutPlanExercise>();

        /// <summary>Sessions logged against this plan (R17).</summary>
        public virtual ICollection<WorkoutLog> WorkoutLogs { get; set; } = new List<WorkoutLog>();

        /// <summary>
        /// Copies this template into a new Draft plan for a client, line by line, recording this template as its source.
        /// The template itself is not changed (BR-12). The caller sets CreatedAt and saves the copy.
        /// </summary>
        /// <param name="clientId">The client who gets the plan.</param>
        /// <param name="trainerId">The trainer assigning it, who becomes the copy's author.</param>
        /// <param name="start">The first day of the client's plan (the time part is ignored).</param>
        /// <returns>The new, unsaved client plan.</returns>
        /// <exception cref="BusinessRuleException">This plan is not a template.</exception>
        public WorkoutPlan CreateCopyForClient(int clientId, int trainerId, DateTime start)
        {
            if (!IsTemplate)
            {
                throw new BusinessRuleException("BR-12", "Only a routine template can be assigned to a client.");
            }

            if (WorkoutPlanId == 0)
            {
                throw new InvalidOperationException("Save the template before copying it, so the copy can record its source.");
            }

            var copy = new WorkoutPlan
            {
                TrainerId = trainerId,
                ClientId = clientId,
                SourceTemplateId = WorkoutPlanId,
                Title = Title,
                Description = Description,
                Difficulty = Difficulty,
                IsTemplate = false,
                StartDate = start.Date,
                Status = PlanStatus.Draft
            };

            // Only the foreign keys are copied, not the Exercise navigations: attaching library entities loaded
            // without tracking would make EF Core try to insert them again.
            foreach (var line in Exercises)
            {
                copy.Exercises.Add(new WorkoutPlanExercise
                {
                    ExerciseId = line.ExerciseId,
                    DayOfWeek = line.DayOfWeek,
                    SortOrder = line.SortOrder,
                    Sets = line.Sets,
                    Reps = line.Reps,
                    DurationMinutes = line.DurationMinutes,
                    RestSeconds = line.RestSeconds,
                    Notes = line.Notes
                });
            }

            return copy;
        }

        /// <summary>
        /// The plan's lines for one weekday, in their order within the day.
        /// </summary>
        /// <param name="day">The weekday.</param>
        /// <returns>The lines for that day ordered by SortOrder; empty on a rest day.</returns>
        public List<WorkoutPlanExercise> GetExercisesFor(DayOfWeek day) =>
            Exercises.Where(e => e.DayOfWeek == day).OrderBy(e => e.SortOrder).ToList();

        /// <summary>
        /// The weekdays the plan schedules training on; the denominator of workout adherence (spec 1.10).
        /// </summary>
        /// <returns>Each training day once, Sunday first.</returns>
        public List<DayOfWeek> GetScheduledDays() =>
            Exercises.Select(e => e.DayOfWeek).Distinct().OrderBy(d => d).ToList();

        /// <summary>
        /// Moves a draft client plan to Active. It needs at least one exercise (BR-10). The service completes the client's
        /// previous active plan in the same transaction (BR-09).
        /// </summary>
        /// <exception cref="BusinessRuleException">The plan is a template, not a draft, or has no exercises.</exception>
        public void Activate()
        {
            if (IsTemplate)
            {
                throw new BusinessRuleException("BR-12", "A routine template cannot be activated. Assign it to a client instead.");
            }

            if (Status != PlanStatus.Draft)
            {
                throw new BusinessRuleException("BR-09", "Only a draft plan can be activated.");
            }

            if (Exercises.Count == 0)
            {
                throw new BusinessRuleException("BR-10", "Add at least one exercise before activating the plan.");
            }

            Status = PlanStatus.Active;
        }

        /// <summary>
        /// Moves an active plan to Completed, for example when a newer plan is activated for the client (BR-09).
        /// </summary>
        /// <exception cref="BusinessRuleException">The plan is not active.</exception>
        public void Complete()
        {
            if (Status != PlanStatus.Active)
            {
                throw new BusinessRuleException("BR-09", "Only an active plan can be completed.");
            }

            Status = PlanStatus.Completed;
        }

        /// <summary>
        /// Archives the plan. Plans are archived, never deleted (BR-21).
        /// </summary>
        /// <exception cref="BusinessRuleException">The plan is already archived.</exception>
        public void Archive()
        {
            if (Status == PlanStatus.Archived)
            {
                throw new BusinessRuleException("BR-21", "This plan is already archived.");
            }

            Status = PlanStatus.Archived;
        }
    }
}
