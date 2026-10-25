namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// What the shared _Pager partial needs: the page position and the filter values to keep in the links.
    /// </summary>
    public class PagerViewModel
    {
        /// <summary>
        /// Creates the pager for a page of results.
        /// </summary>
        /// <param name="page">The current page.</param>
        /// <param name="totalPages">The number of pages.</param>
        /// <param name="totalCount">The number of rows across all pages.</param>
        /// <param name="routeValues">The filter values to repeat in each page link.</param>
        public PagerViewModel(int page, int totalPages, int totalCount, IDictionary<string, string?> routeValues)
        {
            Page = page;
            TotalPages = totalPages;
            TotalCount = totalCount;
            RouteValues = routeValues;
        }

        /// <summary>The current page.</summary>
        public int Page { get; }

        /// <summary>The number of pages.</summary>
        public int TotalPages { get; }

        /// <summary>The number of rows across all pages.</summary>
        public int TotalCount { get; }

        /// <summary>The filter values to repeat in each page link.</summary>
        public IDictionary<string, string?> RouteValues { get; }

        /// <summary>
        /// The route values for a link to another page: the filters plus the page number.
        /// </summary>
        /// <param name="page">The page to link to.</param>
        /// <returns>The values for asp-all-route-data.</returns>
        public Dictionary<string, string> For(int page)
        {
            var values = RouteValues
                .Where(v => !string.IsNullOrEmpty(v.Value))
                .ToDictionary(v => v.Key, v => v.Value!);
            values["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return values;
        }
    }
}
