using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace CockroachDB.DotNet.EntityFramework.Migrations;

/// <summary>
/// Migration SQL generator for CockroachDB, extending Npgsql's PostgreSQL generator
/// with CockroachDB-specific DDL overrides: computed columns (STORED only),
/// JSON default fix, and unique_rowid() support.
/// </summary>
public class CockroachDBMigrationsSqlGenerator : NpgsqlMigrationsSqlGenerator
{
    public CockroachDBMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        INpgsqlSingletonOptions npgsqlOptions)
        : base(dependencies, npgsqlOptions)
    {
    }

    /// <summary>
    /// CockroachDB computed columns must be STORED — virtual columns are not supported.
    /// Emits: colname coltype GENERATED ALWAYS AS ((expr)::coltype) STORED
    /// </summary>
    protected override void ComputedColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.IsStored != true)
        {
            throw new NotSupportedException(
                "CockroachDB computed columns must be stored. "
                + "Specify 'stored: true' in HasComputedColumnSql(). "
                + $"Column: {table}.{name}");
        }

        var columnType = operation.ColumnType
            ?? GetColumnType(schema, table, name, operation, model)!;

        builder
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name))
            .Append(" ")
            .Append(columnType);

        if (operation.Collation is not null)
        {
            builder
                .Append(" COLLATE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Collation));
        }

        builder
            .Append(" GENERATED ALWAYS AS (")
            .Append($"({operation.ComputedColumnSql})::{columnType}")
            .Append(") STORED");

        if (!operation.IsNullable)
        {
            builder.Append(" NOT NULL");
        }
    }

    /// <summary>
    /// Fix EF Core issue where empty string is used as default for JSON columns.
    /// CockroachDB rejects empty string as invalid JSONB — use {} instead.
    /// </summary>
    protected override void DefaultValue(
        object? defaultValue,
        string? defaultValueSql,
        string? columnType,
        MigrationCommandListBuilder builder)
    {
        if (columnType is "jsonb" or "json" && defaultValue is "")
        {
            defaultValue = "{}";
        }

        base.DefaultValue(defaultValue, defaultValueSql, columnType, builder);
    }
}
