using CockroachDB.DotNet.Abstractions.Cdc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CockroachDB.DotNet.Cdc;

public sealed class ChangefeedManager(
    NpgsqlDataSource dataSource,
    ILogger<ChangefeedManager> logger) : IChangefeedManager
{
    public async Task<long> CreateChangefeedAsync(
        string tableName,
        string webhookUrl,
        ChangefeedOptions? options,
        CancellationToken cancellationToken)
    {
        var opts = options ?? new ChangefeedOptions();
        var safeTable = tableName.Replace("'", "''");
        var safeUrl = webhookUrl.Replace("'", "''");

        var optionsClauses = new List<string>
        {
            $"format = {opts.Format}"
        };

        if (opts.Updated)
            optionsClauses.Add("updated");
        if (opts.Resolved)
            optionsClauses.Add("resolved");
        if (opts.EnvelopeType is not null)
            optionsClauses.Add($"envelope = '{opts.EnvelopeType}'");
        if (opts.SchemaChangePolicy is not null)
            optionsClauses.Add($"schema_change_policy = '{opts.SchemaChangePolicy}'");

        var sql = $"""
            CREATE CHANGEFEED FOR {safeTable}
            INTO 'webhook-https://{safeUrl}'
            WITH {string.Join(", ", optionsClauses)}
            """;

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var jobId = Convert.ToInt64(result);

        logger.LogInformation(
            "Created changefeed for table '{Table}' with job ID {JobId}.",
            tableName, jobId);

        return jobId;
    }

    public async Task CancelChangefeedAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CANCEL JOB {jobId}";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Cancelled changefeed job {JobId}.", jobId);
    }

    public async Task<IReadOnlyList<ChangefeedJob>> ListChangefeedsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT job_id, description, status
            FROM [SHOW CHANGEFEED JOBS]
            ORDER BY created DESC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var jobs = new List<ChangefeedJob>();

        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(new ChangefeedJob(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return jobs;
    }
}
