using Npgsql;

namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Shared helper that applies a Cockroach vCluster <see cref="VirtualClusterBinding"/>
/// to a connection as transaction-scoped session settings. Both
/// <see cref="VirtualClusterMiddleware"/> and integration tests use this exact code
/// path so behavior is verified end-to-end.
///
/// <para><c>SET LOCAL</c> scopes the tags to the current transaction so pooled
/// connections are not polluted for the next borrower.</para>
/// </summary>
public static class VirtualClusterCommands
{
    /// <summary>
    /// Issue <c>SET LOCAL application_name</c> and, when a priority is present,
    /// <c>SET LOCAL default_transaction_quality_of_service</c> on
    /// <paramref name="connection"/> within <paramref name="transaction"/>.
    /// </summary>
    public static async Task ApplyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        VirtualClusterBinding binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(binding);

        await using (var appCmd = connection.CreateCommand())
        {
            appCmd.Transaction = transaction;
            appCmd.CommandText = $"SET LOCAL application_name = '{Escape(binding.Name)}'";
            await appCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (binding.Priority is { } priority)
        {
            await using var qosCmd = connection.CreateCommand();
            qosCmd.Transaction = transaction;
            qosCmd.CommandText =
                $"SET LOCAL default_transaction_quality_of_service = '{priority.Qos()}'";
            await qosCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
