namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One choice in a drop-down list: the key that is posted, the text that is shown and, optionally, its group.
    /// </summary>
    public class SelectOptionViewModel
    {
        /// <summary>
        /// Creates an option.
        /// </summary>
        /// <param name="id">The key that is posted.</param>
        /// <param name="label">The text that is shown.</param>
        /// <param name="group">The optgroup the option belongs to, for example an exercise category.</param>
        public SelectOptionViewModel(int id, string label, string? group = null)
        {
            Id = id;
            Label = label;
            Group = group;
        }

        /// <summary>The key that is posted.</summary>
        public int Id { get; }

        /// <summary>The text that is shown.</summary>
        public string Label { get; }

        /// <summary>The optgroup the option belongs to, if the list is grouped.</summary>
        public string? Group { get; }
    }
}
