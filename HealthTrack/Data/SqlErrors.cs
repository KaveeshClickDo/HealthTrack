using Microsoft.Data.SqlClient;

namespace HealthTrack.Data
{
    /// <summary>
    /// Recognises the SQL Server errors that services handle themselves instead of letting them reach the error page.
    /// </summary>
    public static class SqlErrors
    {
        // SQL Server chose this transaction as the deadlock victim and rolled it back.
        private const int DeadlockVictim = 1205;

        // A row would repeat the key of a unique index (2601) or a unique constraint (2627).
        private const int DuplicateKeyInIndex = 2601;
        private const int DuplicateKeyInConstraint = 2627;

        /// <summary>
        /// Whether SQL Server rolled the transaction back to break a deadlock (error 1205), anywhere in the exception chain
        /// (EF Core wraps it in a DbUpdateException when it happens in SaveChanges).
        /// </summary>
        /// <param name="exception">The exception caught.</param>
        /// <returns>True for a deadlock; false for anything else, including every non-SQL Server provider.</returns>
        public static bool IsDeadlock(Exception exception) => HasError(exception, DeadlockVictim);

        /// <summary>
        /// Whether an insert broke a unique index or constraint (errors 2601 and 2627), anywhere in the exception chain.
        /// EF Core InMemory never enforces unique indexes, so services check first and use this only as the last guard.
        /// </summary>
        /// <param name="exception">The exception caught.</param>
        /// <returns>True for a duplicate key; false for anything else.</returns>
        public static bool IsDuplicateKey(Exception exception) =>
            HasError(exception, DuplicateKeyInIndex) || HasError(exception, DuplicateKeyInConstraint);

        private static bool HasError(Exception exception, int number)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException sqlException && sqlException.Number == number)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
