namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// The popular routines report (UC27): templates ranked by use, the top 10 exercises logged and each exercise
    /// category's share.
    /// </summary>
    public class PopularRoutinesReportViewModel
    {
        /// <summary>The date range.</summary>
        public ReportRangeViewModel Range { get; init; } = new();

        /// <summary>Whether the report covers everyone (administrator) rather than one trainer's clients.</summary>
        public bool AllClients { get; init; }

        /// <summary>Templates by clients assigned, then active plans, then adherence.</summary>
        public IReadOnlyList<RoutineUsageRow> Routines { get; init; } = [];

        /// <summary>The 10 exercises logged most often.</summary>
        public IReadOnlyList<ExerciseUsageRow> TopExercises { get; init; } = [];

        /// <summary>Each exercise category's share of the logged exercises.</summary>
        public IReadOnlyList<CountRow> Categories { get; init; } = [];

        /// <summary>The top exercises by times logged (bar chart).</summary>
        public ChartSeries Chart { get; init; } = new();
    }
}
