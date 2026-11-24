using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// Compares entities carrying <c>[GlobalTable]</c> / <c>[RegionalByRow]</c> / <c>[CrdbRegion]</c>
/// with the live multi-region configuration of their database and tables, and suggests SQL for
/// every difference. It only reads; it never changes the schema.
///
/// <para>All queries use <c>SHOW</c> statements and <c>information_schema</c>, not
/// <c>crdb_internal</c>, which recent CockroachDB versions restrict by default.</para>
/// </summary>
internal static class MultiRegionCheck
{
    private const string RegionType = "crdb_internal_region";

    private const string ImplicitRegionColumn = "crdb_region";

    private const string RegionalByRow = "REGIONAL BY ROW";

    /// <summary>
    /// Whether <paramref name="connection"/> is to CockroachDB. The check is on by default, so it
    /// must stay quiet for applications whose tests run against another database.
    /// </summary>
    internal static async Task<bool> IsCockroachDBAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT version()";
            var version = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            return version is not null && version.Contains("CockroachDB", StringComparison.Ordinal);
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>Reads what <see cref="Evaluate"/> needs for <paramref name="tables"/> over <paramref name="connection"/>.</summary>
    internal static async Task<MultiRegionState> LoadAsync(
        DbConnection connection, IReadOnlyList<MultiRegionTable> tables, CancellationToken cancellationToken)
    {
        string database;
        string currentSchema;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT current_database(), current_schema()";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            database = reader.GetString(0);
            currentSchema = reader.IsDBNull(1) ? "public" : reader.GetString(1);
        }

        string? primaryRegion = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT primary_region FROM [SHOW DATABASES] WHERE database_name = @db";
            AddParameter(command, "@db", database);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && !reader.IsDBNull(0))
            {
                primaryRegion = reader.GetString(0);
            }
        }

        var clusterRegions = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT region FROM [SHOW REGIONS FROM CLUSTER] ORDER BY region";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                clusterRegions.Add(reader.GetString(0));
            }
        }

        var keywords = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT word FROM pg_get_keywords() WHERE catcode IN ('R', 'T')";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                keywords.Add(reader.GetString(0));
            }
        }

        var wanted = new HashSet<TableName>();
        foreach (var table in tables)
        {
            wanted.Add(NameOf(table, currentSchema));
        }

        var found = new Dictionary<TableName, TableInfo>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT schema_name, table_name, locality FROM [SHOW TABLES] WHERE type = 'table'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = new TableName(reader.GetString(0), reader.GetString(1));
                if (wanted.Contains(name))
                {
                    var locality = reader.IsDBNull(2) ? null : reader.GetString(2);
                    found[name] = new TableInfo(locality, new Dictionary<string, string>());
                }
            }
        }

        foreach (var table in tables)
        {
            var name = NameOf(table, currentSchema);
            if (table.Kind == MultiRegionKind.RegionalByRow && found.TryGetValue(name, out var info))
            {
                found[name] = info with
                {
                    ColumnTypes = await ColumnTypesAsync(connection, name, cancellationToken).ConfigureAwait(false)
                };
            }
        }

        return new MultiRegionState(database, currentSchema, primaryRegion, clusterRegions, found, keywords);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ColumnTypesAsync(
        DbConnection connection, TableName name, CancellationToken cancellationToken)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT column_name, crdb_sql_type FROM information_schema.columns "
            + "WHERE table_catalog = current_database() AND table_schema = @schema AND table_name = @table";
        AddParameter(command, "@schema", name.Schema);
        AddParameter(command, "@table", name.Table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            types[reader.GetString(0)] = reader.GetString(1);
        }

        return types;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Lists every difference between <paramref name="tables"/> and <paramref name="state"/>, with
    /// SQL to fix the schema or a description of the attribute to fix. <paramref name="tables"/>
    /// holds every entity, annotated or not: a <c>GLOBAL</c> or <c>REGIONAL BY ROW</c> table needs
    /// its attribute too.
    /// </summary>
    internal static MultiRegionReport Evaluate(MultiRegionState state, IReadOnlyList<MultiRegionTable> tables)
    {
        var report = new MultiRegionReport();
        var keywords = state.Keywords;
        var multiregion = state.PrimaryRegion is not null;
        var annotated = tables.Count(table => table.Kind != MultiRegionKind.None);

        if (!multiregion && annotated > 0)
        {
            var database = Identifiers.Quote(state.Database, keywords);
            var names = string.Join(", ", tables
                .Where(table => table.Kind != MultiRegionKind.None)
                .Select(table => table.Entity));
            var affected = "@GlobalTable/@RegionalByRow on " + Count(annotated, "entity", "entities")
                + " (" + names + ") have no effect.";
            if (state.ClusterRegions.Count == 0)
            {
                report.Problem("Database " + database + " is not multi-region; " + affected
                    + "\nThe cluster has no regions, so the database can't be made multi-region yet:"
                    + " start every node with --locality=region=<region>.");
            }
            else
            {
                report.Problem("Database " + database + " is not multi-region; " + affected
                    + "\nThe cluster has regions: " + string.Join(", ", state.ClusterRegions)
                    + ". Choose the primary region deliberately; the SQL below uses the first one.");
                var regions = state.ClusterRegions;
                report.Statement("ALTER DATABASE " + database + " SET PRIMARY REGION "
                    + Identifiers.RegionLiteral(regions[0]) + ";");
                for (var i = 1; i < regions.Count; i++)
                {
                    report.Statement("ALTER DATABASE " + database + " ADD REGION "
                        + Identifiers.RegionLiteral(regions[i]) + ";");
                }
            }
        }

        var order = new List<TableName>();
        var byTable = new Dictionary<TableName, List<MultiRegionTable>>();
        foreach (var table in tables)
        {
            var name = NameOf(table, state.CurrentSchema);
            if (!byTable.TryGetValue(name, out var list))
            {
                list = [];
                byTable[name] = list;
                order.Add(name);
            }

            list.Add(table);
        }

        foreach (var name in order)
        {
            var entry = byTable[name];
            var qualified = Identifiers.Quote(name.Schema, keywords) + "."
                + Identifiers.Quote(name.Table, keywords);
            state.Tables.TryGetValue(name, out var info);

            var annotatedEntities = entry.Where(table => table.Kind != MultiRegionKind.None).ToList();
            var kinds = new HashSet<MultiRegionKind>(annotatedEntities.Select(table => table.Kind));

            if (kinds.Count == 0)
            {
                Unannotated(report, qualified, info, entry, multiregion);
            }
            else if (kinds.Count > 1 || kinds.Contains(MultiRegionKind.Both))
            {
                Conflicting(report, qualified, annotatedEntities);
            }
            else
            {
                var table = annotatedEntities[0];
                if (table.Kind == MultiRegionKind.Global)
                {
                    Global(report, qualified, info, table, multiregion);
                }
                else
                {
                    RegionalByRowTable(report, state, table, qualified, info, multiregion);
                }
            }
        }

        return report;
    }

    private static void Global(MultiRegionReport report, string qualified, TableInfo? info,
        MultiRegionTable table, bool multiregion)
    {
        if (info is null)
        {
            Missing(report, qualified, "@GlobalTable", table);
        }
        else if (!multiregion || !"GLOBAL".Equals(info.Locality, StringComparison.Ordinal))
        {
            if (multiregion)
            {
                var problem = "Table " + qualified + " (@GlobalTable " + table.Entity + ") is "
                    + info.Locality + ", not GLOBAL.";
                if (RegionalByRowColumn(info.Locality) is not null)
                {
                    report.Contradiction(problem);
                }
                else
                {
                    report.Problem(problem);
                }
            }

            report.Statement("ALTER TABLE " + qualified + " SET LOCALITY GLOBAL;");
        }
    }

    /// <summary>
    /// An entity without either attribute whose table is <c>GLOBAL</c> or <c>REGIONAL BY ROW</c>:
    /// the code, not the schema, needs fixing.
    /// </summary>
    private static void Unannotated(MultiRegionReport report, string qualified, TableInfo? info,
        List<MultiRegionTable> entities, bool multiregion)
    {
        if (info is null || !multiregion)
        {
            return;
        }

        var names = string.Join(", ", entities.Select(table => table.Entity));
        if ("GLOBAL".Equals(info.Locality, StringComparison.Ordinal))
        {
            report.Problem("Table " + qualified + " is GLOBAL, but entity " + names
                + " has no @GlobalTable. Add @GlobalTable to it.");
        }
        else if (RegionalByRowColumn(info.Locality) is { } column)
        {
            report.Problem("Table " + qualified + " is " + info.Locality + ", but entity " + names
                + " has no @RegionalByRow. Add @RegionalByRow to it, and @CrdbRegion to the property mapped to "
                + column + " so rows are homed in the transaction's region.");
        }
    }

    private static void Conflicting(MultiRegionReport report, string qualified, List<MultiRegionTable> entities)
    {
        if (entities.Count == 1)
        {
            report.Contradiction("Entity " + entities[0].Entity
                + " is annotated with both @GlobalTable and @RegionalByRow; keep one.");
            return;
        }

        var described = string.Join(", ", entities.Select(table => table.Entity + " (" + table.Kind switch
        {
            MultiRegionKind.Global => "@GlobalTable",
            MultiRegionKind.RegionalByRow => "@RegionalByRow",
            _ => "@GlobalTable and @RegionalByRow"
        } + ")"));
        report.Contradiction("Table " + qualified + " is mapped by entities with conflicting attributes: "
            + described + ". Annotate them alike.");
    }

    private static void RegionalByRowTable(MultiRegionReport report, MultiRegionState state, MultiRegionTable table,
        string qualified, TableInfo? info, bool multiregion)
    {
        if (info is null)
        {
            Missing(report, qualified, "@RegionalByRow", table);
            return;
        }

        var wantedColumn = table.RegionColumn;
        var currentColumn = RegionalByRowColumn(info.Locality);
        if (multiregion && currentColumn is not null
            && (wantedColumn is null || wantedColumn.Equals(currentColumn, StringComparison.Ordinal)))
        {
            return;
        }

        var column = wantedColumn ?? ImplicitRegionColumn;
        var quotedColumn = Identifiers.Quote(column, state.Keywords);
        info.ColumnTypes.TryGetValue(column, out var type);
        var description = "Table " + qualified + " (@RegionalByRow " + table.Entity + ")";
        var target = ImplicitRegionColumn.Equals(column, StringComparison.Ordinal)
            ? RegionalByRow
            : RegionalByRow + " AS " + quotedColumn;
        var global = multiregion && "GLOBAL".Equals(info.Locality, StringComparison.Ordinal);
        if (global)
        {
            report.Contradiction(description + " is GLOBAL, not " + target + ".");
        }

        if (type is not null && !IsRegionType(type))
        {
            report.Problem(description + ": column " + quotedColumn + " is " + type
                + ", but REGIONAL BY ROW needs a " + RegionType + " column. Change it with your migrations.");
            return;
        }

        if (type is null && !ImplicitRegionColumn.Equals(column, StringComparison.Ordinal))
        {
            report.Problem(description + ": column " + quotedColumn
                + " of the @CrdbRegion property does not exist. Create it, as a " + RegionType
                + " column, with your migrations.");
            return;
        }

        if (multiregion && !global)
        {
            report.Problem(description + " is " + info.Locality + ", not " + target + ".");
        }

        report.Statement("ALTER TABLE " + qualified + " SET LOCALITY " + target + ";  -- rewrites the table");
    }

    private static void Missing(MultiRegionReport report, string qualified, string annotation, MultiRegionTable table)
    {
        report.Problem("Table " + qualified + " (" + annotation + " " + table.Entity
            + ") does not exist. Create it with your migrations.");
    }

    /// <summary>The region column of a <c>REGIONAL BY ROW</c> locality, or <c>null</c> for any other locality.</summary>
    internal static string? RegionalByRowColumn(string? locality)
    {
        if (locality is null || !locality.StartsWith(RegionalByRow, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = locality[RegionalByRow.Length..].Trim();
        if (rest.Length == 0)
        {
            return ImplicitRegionColumn;
        }

        if (rest.StartsWith("AS ", StringComparison.Ordinal))
        {
            return Identifiers.Parse(rest[3..].Trim())[0];
        }

        return null;
    }

    private static bool IsRegionType(string type)
        => type.Equals(RegionType, StringComparison.Ordinal) || type.EndsWith("." + RegionType, StringComparison.Ordinal);

    private static TableName NameOf(MultiRegionTable table, string currentSchema)
        => new(table.Schema ?? currentSchema, table.Table);

    private static string Count(long n, string one, string many) => n + " " + (n == 1 ? one : many);
}
