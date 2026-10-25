using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Workout plans, routine templates and workout logging (spec 1.5, UC08, UC09, UC19, UC20, UC39).
    /// Trainer methods take the trainer's user ID and only reach the trainer's own plans and assigned clients (BR-08);
    /// anything else answers null or false, so the controller can return 404.
    /// </summary>
    public interface IWorkoutService
    {
        /// <summary>
        /// A new plan form, for a client plan or a template, with the trainer's assigned clients to choose from.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <param name="clientId">The client to pre-select.</param>
        /// <param name="isTemplate">Whether the form is for a template.</param>
        /// <returns>The form, or null when the client isn't assigned to the trainer.</returns>
        Task<WorkoutPlanViewModel?> GetCreatePlanFormAsync(string trainerUserId, int? clientId, bool isTemplate);

        /// <summary>
        /// Fills the plan form's client choices, for example after a failed post.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <returns>A task that completes when the choices are filled.</returns>
        Task FillPlanOptionsAsync(WorkoutPlanViewModel model, string trainerUserId);

        /// <summary>
        /// Creates a Draft client plan (assigned clients only, BR-08) or template (UC19).
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer, who becomes the author.</param>
        /// <param name="model">The validated form.</param>
        /// <returns>The new plan's ID, or null when the client isn't assigned to the trainer.</returns>
        Task<int?> CreatePlanAsync(string trainerUserId, WorkoutPlanViewModel model);

        /// <summary>
        /// One page of the trainer's client plans: open ones first, then newest.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<WorkoutPlanListItemViewModel>> GetTrainerPlansAsync(string trainerUserId, int page);

        /// <summary>
        /// The plan editor for one of the trainer's own plans.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <returns>The editor, or null when the plan isn't the trainer's (or its client is no longer assigned).</returns>
        Task<WorkoutPlanEditViewModel?> GetPlanForEditAsync(int planId, string trainerUserId);

        /// <summary>
        /// Refills the editor's display-only parts after a failed post, keeping what the trainer typed.
        /// </summary>
        /// <param name="model">The posted editor.</param>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <returns>False when the plan isn't the trainer's.</returns>
        Task<bool> FillEditDisplayAsync(WorkoutPlanEditViewModel model, int planId, string trainerUserId);

        /// <summary>
        /// Saves the plan's details and line changes, removing ticked lines. Completed and archived plans can't change (BR-21).
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer, who must be the author.</param>
        /// <param name="model">The validated editor.</param>
        /// <returns>False when the plan isn't the trainer's.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-10, BR-11 or BR-21 is broken.</exception>
        Task<bool> UpdatePlanAsync(int planId, string trainerUserId, WorkoutPlanEditViewModel model);

        /// <summary>
        /// Adds an active library exercise to the plan on one weekday, after the day's other lines (BR-11).
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer, who must be the author.</param>
        /// <param name="model">The validated line.</param>
        /// <returns>False when the plan isn't the trainer's.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-11 or BR-21 is broken.</exception>
        Task<bool> AddExerciseAsync(int planId, string trainerUserId, PlanExerciseViewModel model);

        /// <summary>
        /// Archives the plan; plans are archived, never deleted (BR-21).
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer, who must be the author.</param>
        /// <returns>False when the plan isn't the trainer's.</returns>
        Task<bool> ArchivePlanAsync(int planId, string trainerUserId);

        /// <summary>
        /// Saves a client plan as a new template with the same lines; the client plan is unchanged (UC39).
        /// </summary>
        /// <param name="planId">The client plan.</param>
        /// <param name="trainerUserId">The signed-in trainer, who must be the author.</param>
        /// <returns>The new template's ID, or null when the plan isn't the trainer's.</returns>
        Task<int?> SaveAsTemplateAsync(int planId, string trainerUserId);

        /// <summary>
        /// Activates a draft client plan (BR-10), completing the client's current active workout plan in the same
        /// transaction (BR-09), and tells the client.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer, who must be the author.</param>
        /// <returns>False when the plan isn't the trainer's.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-09, BR-10 or BR-12 is broken.</exception>
        Task<bool> ActivatePlanAsync(int planId, string trainerUserId);

        /// <summary>
        /// Every template that isn't archived, with its author, size and how often it was assigned.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer, to flag their own templates.</param>
        /// <returns>The templates by title.</returns>
        Task<IReadOnlyList<TemplateListItemViewModel>> GetTemplatesAsync(string trainerUserId);

        /// <summary>
        /// The form for assigning a template to one of the trainer's clients. Any trainer may use any template.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <param name="clientId">The client to pre-select; must be assigned to the trainer.</param>
        /// <returns>The form, or null when the template or client isn't available.</returns>
        Task<AssignTemplateViewModel?> GetAssignTemplateFormAsync(int templateId, string trainerUserId, int? clientId);

        /// <summary>
        /// Refills the assign form's display-only parts after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="templateId">The template.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <returns>False when the template isn't available.</returns>
        Task<bool> FillAssignTemplateDisplayAsync(AssignTemplateViewModel model, int templateId, string trainerUserId);

        /// <summary>
        /// Copies a template to one of the trainer's clients (BR-12), optionally activating the copy straight away.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <param name="clientId">The client; must be assigned to the trainer (BR-08).</param>
        /// <param name="startDate">The first day of the client's plan.</param>
        /// <param name="activateNow">Activate the copy, completing the client's current plan (BR-09).</param>
        /// <param name="trainerUserId">The signed-in trainer, who becomes the copy's author.</param>
        /// <returns>The new plan's ID, or null when the template or client isn't available.</returns>
        Task<int?> AssignTemplateAsync(int templateId, int clientId, DateTime startDate, bool activateNow, string trainerUserId);

        /// <summary>
        /// The client's active plan as a Monday–Sunday week (UC08).
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The week; HasPlan is false when there is no active plan.</returns>
        Task<WorkoutWeekViewModel> GetWeekAsync(int clientId);

        /// <summary>
        /// Today's planned exercises as pre-filled log rows: the plan's minutes, or sets × 2 minutes when it only gives sets
        /// and reps.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>The log form; without rows when there is no plan for today.</returns>
        Task<LogWorkoutViewModel> GetTodaysWorkoutAsync(int clientId, DateTime today);

        /// <summary>
        /// An empty free-workout form for today with one row.
        /// </summary>
        /// <returns>The form.</returns>
        Task<LogWorkoutViewModel> GetLogFormAsync();

        /// <summary>
        /// Fills the log form's exercise choices (active exercises plus any already on the form).
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>A task that completes when the choices are filled.</returns>
        Task FillLogOptionsAsync(LogWorkoutViewModel model);

        /// <summary>
        /// Saves a session (UC09): not in the future and with at least one exercise (BR-13); each entry's calories are
        /// worked out from the client's latest weight and stored as a snapshot (BR-14, UC35).
        /// </summary>
        /// <param name="clientUserId">The signed-in client.</param>
        /// <param name="model">The validated form.</param>
        /// <returns>The new log's ID, or null when the plan isn't the client's.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-13, BR-14 or BR-21 is broken.</exception>
        Task<int?> LogWorkoutAsync(string clientUserId, LogWorkoutViewModel model);

        /// <summary>
        /// One page of the client's sessions, newest first, with their entries and any feedback.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<WorkoutHistoryItemViewModel>> GetHistoryAsync(int clientId, int page);
    }
}
