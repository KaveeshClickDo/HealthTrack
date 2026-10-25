using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Health measurements, progress trends for the charts, workout adherence and the suggested calorie target (UC11,
    /// UC12, UC18, spec 1.7 and 1.10).
    /// </summary>
    public interface IProgressService
    {
        /// <summary>
        /// The new measurement form: measured now, with the height pre-filled from the last reading.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The form.</returns>
        Task<MeasurementViewModel> GetMeasurementFormAsync(int clientId);

        /// <summary>
        /// Records a measurement for the signed-in client. BMI is calculated from weight and height, never typed (BR-14);
        /// the notes are encrypted by the value converter (BR-20).
        /// </summary>
        /// <param name="clientUserId">The signed-in client's user ID.</param>
        /// <param name="model">The form.</param>
        /// <returns>The new measurement's key, or null when the user has no client profile.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-13: the time is in the future.</exception>
        Task<int?> AddMeasurementAsync(string clientUserId, MeasurementViewModel model);

        /// <summary>
        /// The client's most recent measurement with its BMI and category.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The measurement, or null when there are none.</returns>
        Task<MeasurementRowViewModel?> GetLatestMeasurementAsync(int clientId);

        /// <summary>
        /// The measurements page: the latest and first readings, the target weight and one page of readings.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The page.</returns>
        Task<MeasurementsPageViewModel> GetMeasurementsAsync(int clientId, int page);

        /// <summary>
        /// Weight by day (the last reading of each day), with the target weight as a dashed reference line.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="from">First day.</param>
        /// <param name="to">Last day.</param>
        /// <returns>The series.</returns>
        Task<ChartSeries> GetWeightTrendAsync(int clientId, DateTime from, DateTime to);

        /// <summary>
        /// BMI by day (the last reading of each day).
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="from">First day.</param>
        /// <param name="to">Last day.</param>
        /// <returns>The series.</returns>
        Task<ChartSeries> GetBmiTrendAsync(int clientId, DateTime from, DateTime to);

        /// <summary>
        /// Weekly (Monday-start) kcal burned and kcal eaten, against the diet target added up over the days a plan applied.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="from">First day.</param>
        /// <param name="to">Last day.</param>
        /// <returns>The series.</returns>
        Task<ChartSeries> GetCaloriesTrendAsync(int clientId, DateTime from, DateTime to);

        /// <summary>
        /// Resting heart rate and systolic and diastolic blood pressure by day.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="from">First day.</param>
        /// <param name="to">Last day.</param>
        /// <returns>The series.</returns>
        Task<ChartSeries> GetVitalsTrendAsync(int clientId, DateTime from, DateTime to);

        /// <summary>
        /// Workout adherence (spec 1.10): of the days up to today on which the plan in effect scheduled training, the
        /// share with a logged session.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="from">First day.</param>
        /// <param name="to">Last day.</param>
        /// <returns>The scheduled and completed days.</returns>
        Task<AdherenceViewModel> GetAdherenceAsync(int clientId, DateTime from, DateTime to);

        /// <summary>
        /// The suggested daily calorie target (spec 1.10): TDEE from the latest weight and height, age, gender and
        /// activity level, adjusted for the goal and never below 1,200 kcal.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The target in kcal, or null when the client has no measurement yet.</returns>
        Task<int?> SuggestCalorieTargetAsync(int clientId);

        /// <summary>
        /// The summary at the top of the client's progress page.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="range">The chosen range.</param>
        /// <returns>The page model.</returns>
        Task<ClientProgressViewModel> GetClientProgressAsync(int clientId, ProgressRangeViewModel range);

        /// <summary>
        /// An assigned client's progress for their trainer (UC18): charts, adherence, measurements and recent logs; the
        /// medical details and measurement notes only when the client shares them.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="clientId">The client.</param>
        /// <param name="range">The chosen range.</param>
        /// <returns>The page model, or null when the client isn't actively assigned to the trainer (BR-08).</returns>
        Task<TrainerClientProgressViewModel?> GetTrainerClientProgressAsync(string trainerUserId, int clientId,
            ProgressRangeViewModel range);
    }
}
