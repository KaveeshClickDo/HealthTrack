using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// Makes SaveChanges fail with a SQL Server error, the way EF Core reports it (a DbUpdateException around a SqlException),
/// while it would insert or update a <typeparamref name="TEntity"/>. Used to act out a lost deadlock or a duplicate key.
/// </summary>
/// <typeparam name="TEntity">The entity whose saves fail.</typeparam>
public sealed class SqlErrorInterceptor<TEntity> : SaveChangesInterceptor where TEntity : class
{
    private bool _runningOtherRequest;

    /// <summary>
    /// Creates the interceptor; it fails nothing until <see cref="FailuresLeft"/> is set.
    /// </summary>
    /// <param name="errorNumber">The SQL Server error number, for example 1205 for a deadlock.</param>
    public SqlErrorInterceptor(int errorNumber)
    {
        ErrorNumber = errorNumber;
    }

    /// <summary>The SQL Server error number thrown.</summary>
    public int ErrorNumber { get; }

    /// <summary>How many more saves of <typeparamref name="TEntity"/> fail.</summary>
    public int FailuresLeft { get; set; }

    /// <summary>How many saves have failed so far.</summary>
    public int Failures { get; private set; }

    /// <summary>
    /// Runs once, just before the first failure: the "other request" that wins the race, for example a booking of the
    /// same time saved through another scope. Its own saves pass.
    /// </summary>
    public Func<Task>? OtherRequest { get; set; }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_runningOtherRequest || FailuresLeft == 0 || eventData.Context is not { } context
            || !context.ChangeTracker.Entries<TEntity>().Any(e => e.State is EntityState.Added or EntityState.Modified))
        {
            return result;
        }

        FailuresLeft--;
        Failures++;
        if (OtherRequest is { } otherRequest)
        {
            OtherRequest = null;
            _runningOtherRequest = true;
            try
            {
                await otherRequest();
            }
            finally
            {
                _runningOtherRequest = false;
            }
        }

        throw new DbUpdateException($"Simulated SQL Server error {ErrorNumber}.", SqlExceptionFactory.Create(ErrorNumber));
    }
}
