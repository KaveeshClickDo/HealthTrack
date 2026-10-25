using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Paging for lists: every list shows 20 rows a page (spec 1.12).
    /// </summary>
    public static class Paging
    {
        /// <summary>Rows per page.</summary>
        public const int PageSize = 20;

        /// <summary>
        /// Counts the rows, clamps the page number into range and loads that page. Order the query first.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="query">An ordered query.</param>
        /// <param name="page">The 1-based page wanted; out-of-range values show the nearest page.</param>
        /// <returns>The page.</returns>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page)
        {
            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
            return new PagedResult<T>(items, page, PageSize, totalCount);
        }
    }
}
