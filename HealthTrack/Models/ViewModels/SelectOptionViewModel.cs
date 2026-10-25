namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One choice in a drop-down list: the key that is posted and the text that is shown.
    /// </summary>
    public class SelectOptionViewModel
    {
        /// <summary>
        /// Creates an option.
        /// </summary>
        /// <param name="id">The key that is posted.</param>
        /// <param name="label">The text that is shown.</param>
        public SelectOptionViewModel(int id, string label)
        {
            Id = id;
            Label = label;
        }

        /// <summary>The key that is posted.</summary>
        public int Id { get; }

        /// <summary>The text that is shown.</summary>
        public string Label { get; }
    }
}
