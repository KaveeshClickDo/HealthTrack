namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One page of a list (lists show 20 rows a page).
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Creates a page.
        /// </summary>
        /// <param name="items">The rows on this page.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">Rows per page.</param>
        /// <param name="totalCount">Rows across all pages.</param>
        public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        /// <summary>The rows on this page.</summary>
        public IReadOnlyList<T> Items { get; }

        /// <summary>The 1-based page number.</summary>
        public int Page { get; }

        /// <summary>Rows per page.</summary>
        public int PageSize { get; }

        /// <summary>Rows across all pages.</summary>
        public int TotalCount { get; }

        /// <summary>The number of pages; at least 1, so an empty list still has a page.</summary>
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        /// <summary>Whether there is a previous page.</summary>
        public bool HasPrevious => Page > 1;

        /// <summary>Whether there is a next page.</summary>
        public bool HasNext => Page < TotalPages;
    }
}
