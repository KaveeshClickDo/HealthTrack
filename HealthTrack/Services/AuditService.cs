using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Writes and reads the AuditLog table.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="httpContextAccessor">Gives the current request, for the IP address; there is none outside a request.</param>
        /// <param name="clock">Supplies the time.</param>
        public AuditService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor, IClock clock)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task LogAsync(string? userId, AuditAction action, string? entityName = null, string? entityId = null, string? details = null)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName.Truncate(50),
                EntityId = entityId.Truncate(50),
                Details = details.Truncate(500),
                IpAddress = GetIpAddress(),
                Timestamp = _clock.Now
            });

            await _db.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<PagedResult<AuditLogItemViewModel>> GetPageAsync(AuditLogFilterViewModel filter, int page)
        {
            var query = _db.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.UserEmail))
            {
                // Compare with the normalised (upper-case) email, so the search ignores case on every provider.
                var email = filter.UserEmail.Trim().ToUpperInvariant();
                query = query.Where(a => a.User != null && a.User.NormalizedEmail != null && a.User.NormalizedEmail.Contains(email));
            }

            if (filter.AuditAction is { } action)
            {
                query = query.Where(a => a.Action == action);
            }

            if (filter.From is { } from)
            {
                var start = from.Date;
                query = query.Where(a => a.Timestamp >= start);
            }

            if (filter.To is { } to)
            {
                // The To day is included in full.
                var end = to.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < end);
            }

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ThenByDescending(a => a.AuditLogId)
                .Select(a => new AuditLogItemViewModel
                {
                    AuditLogId = a.AuditLogId,
                    Timestamp = a.Timestamp,
                    UserEmail = a.User != null ? a.User.Email : null,
                    UserName = a.User != null ? a.User.FirstName + " " + a.User.LastName : null,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Details = a.Details,
                    IpAddress = a.IpAddress
                })
                .ToPagedResultAsync(page);
        }

        private string? GetIpAddress()
        {
            var address = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (address is null)
            {
                return null;
            }

            // Kestrel reports IPv4 clients as IPv4-mapped IPv6 (::ffff:127.0.0.1); store the plain IPv4 form.
            return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }
    }
}
