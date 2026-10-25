using HealthTrack.Models.Entities;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The assignments page (UC31): assignments, unassigned clients and each trainer's load.
    /// </summary>
    public class AssignmentsOverviewViewModel
    {
        /// <summary>The filters in use.</summary>
        public AssignmentFilterViewModel Filter { get; init; } = new();

        /// <summary>One page of assignments, active first.</summary>
        public PagedResult<AssignmentListItemViewModel> Assignments { get; init; } = new([], 1, 20, 0);

        /// <summary>Active clients without a trainer.</summary>
        public IReadOnlyList<ClientProfile> UnassignedClients { get; init; } = [];

        /// <summary>Approved trainers' load against capacity.</summary>
        public IReadOnlyList<TrainerLoadViewModel> TrainerLoad { get; init; } = [];

        /// <summary>Today's date, used as the end date when an assignment is ended from the page.</summary>
        public DateTime Today { get; init; }
    }
}
