namespace HealthTrack.Helpers
{
    /// <summary>
    /// Small string helpers used before saving text.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Trims a value and turns an empty result into null, so optional fields are stored as NULL.
        /// </summary>
        /// <param name="value">The value from a form.</param>
        /// <returns>The trimmed value, or null when it is empty.</returns>
        public static string? NullIfBlank(this string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// Cuts a value to a column's maximum length, so a long generated message can't fail the save.
        /// </summary>
        /// <param name="value">The value to store.</param>
        /// <param name="maxLength">The column's maximum length.</param>
        /// <returns>The value, shortened when needed; null stays null.</returns>
        public static string? Truncate(this string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : value[..maxLength];

        /// <summary>
        /// The first letters of a name's first two words, for an avatar ("Chris Bandara" → "CB").
        /// </summary>
        /// <param name="name">A person's full name.</param>
        /// <returns>One or two upper-case letters; empty for a blank name.</returns>
        public static string Initials(this string? name) =>
            string.Concat((name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }
}
