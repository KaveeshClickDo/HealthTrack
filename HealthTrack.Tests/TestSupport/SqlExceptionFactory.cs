using System.Reflection;
using Microsoft.Data.SqlClient;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// Builds a <see cref="SqlException"/> with a given error number, so tests on EF Core InMemory can act out SQL Server
/// failures such as a deadlock (1205) or a duplicate key (2601). SqlException has no public constructor, so this goes
/// through SqlClient's own internal factory by reflection.
/// </summary>
public static class SqlExceptionFactory
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="number">The SQL Server error number.</param>
    /// <returns>An exception whose <see cref="SqlException.Number"/> is <paramref name="number"/>.</returns>
    public static SqlException Create(int number)
    {
        const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;

        // SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage, ...): the shortest
        // overload, with the number first and defaults for the rest.
        var errorConstructor = typeof(SqlError).GetConstructors(Internal)
            .Where(c => c.GetParameters() is { Length: > 0 } p && p[0].ParameterType == typeof(int))
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var arguments = errorConstructor.GetParameters()
            .Select((p, i) => i == 0 ? number : DefaultFor(p.ParameterType))
            .ToArray();
        var error = (SqlError)errorConstructor.Invoke(arguments);

        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", Internal)!.Invoke(errors, [error]);

        var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [errors, "16.0"])!;
    }

    private static object? DefaultFor(Type type) =>
        type == typeof(string) ? "Simulated SQL Server error"
        : type.IsValueType ? Activator.CreateInstance(type)
        : null;
}
