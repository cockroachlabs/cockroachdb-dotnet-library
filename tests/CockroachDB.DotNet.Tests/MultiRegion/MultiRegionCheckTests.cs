using System;
using System.Collections.Generic;
using System.Linq;
using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.EntityFramework.MultiRegion;
using Xunit;

namespace CockroachDB.DotNet.Tests.MultiRegion;

/// <summary>
/// Unit tests for the pure diff logic (<see cref="MultiRegionCheck.Evaluate"/>) ported from the
/// Spring starter: it never touches a database, only compares attributes against a
/// <see cref="MultiRegionState"/> snapshot and emits problems + fix-up SQL.
/// </summary>
public class MultiRegionCheckTests
{
    private static readonly IReadOnlySet<string> Keywords =
        new HashSet<string>(StringComparer.Ordinal) { "order", "table" };

    private static MultiRegionState State(
        string? primaryRegion,
        Dictionary<TableName, TableInfo> tables,
        params string[] clusterRegions)
        => new("appdb", "public", primaryRegion, clusterRegions, tables, Keywords);

    private static Dictionary<TableName, TableInfo> One(string table, string? locality,
        Dictionary<string, string>? columnTypes = null)
        => new() { [new TableName("public", table)] = new TableInfo(locality, columnTypes ?? new Dictionary<string, string>()) };

    private static MultiRegionTable Entity(string table, MultiRegionKind kind, string? regionColumn = null)
        => new("App." + table, null, table, kind, regionColumn);

    [Fact]
    public void GlobalEntity_OnGlobalTable_IsClean()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "GLOBAL")),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.True(report.IsEmpty);
    }

    [Fact]
    public void RegionalByRowEntity_OnRegionalByRowTable_IsClean()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("accounts", "REGIONAL BY ROW")),
            [Entity("accounts", MultiRegionKind.RegionalByRow)]);

        Assert.True(report.IsEmpty);
    }

    [Fact]
    public void GlobalEntity_OnRegionalByRowTable_IsContradiction()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "REGIONAL BY ROW")),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.True(report.HasContradictions);
        Assert.Contains(report.Problems, p => p.Contains("@GlobalTable") && p.Contains("not GLOBAL"));
        Assert.Contains("ALTER TABLE public.orders SET LOCALITY GLOBAL;", report.Statements);
    }

    [Fact]
    public void RegionalByRowEntity_OnGlobalTable_IsContradiction()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("accounts", "GLOBAL")),
            [Entity("accounts", MultiRegionKind.RegionalByRow)]);

        Assert.True(report.HasContradictions);
        Assert.Contains(report.Problems, p => p.Contains("is GLOBAL, not REGIONAL BY ROW"));
    }

    [Fact]
    public void EntityWithBothAnnotations_IsContradiction()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "GLOBAL")),
            [Entity("orders", MultiRegionKind.Both)]);

        Assert.True(report.HasContradictions);
        Assert.Contains(report.Problems, p => p.Contains("both @GlobalTable and @RegionalByRow"));
    }

    [Fact]
    public void UnannotatedEntity_OnGlobalTable_IsProblemNotContradiction()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "GLOBAL")),
            [Entity("orders", MultiRegionKind.None)]);

        Assert.False(report.IsEmpty);
        Assert.False(report.HasContradictions);
        Assert.Contains(report.Problems, p => p.Contains("has no @GlobalTable"));
    }

    [Fact]
    public void AnnotationsButDatabaseNotMultiRegion_WithClusterRegions_SuggestsPrimaryAndAddRegion()
    {
        var report = MultiRegionCheck.Evaluate(
            State(null, One("orders", null), "us-east1", "us-west1"),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.Contains(report.Problems, p => p.Contains("is not multi-region"));
        Assert.Contains("ALTER DATABASE appdb SET PRIMARY REGION \"us-east1\";", report.Statements);
        Assert.Contains("ALTER DATABASE appdb ADD REGION \"us-west1\";", report.Statements);
    }

    [Fact]
    public void AnnotationsButDatabaseNotMultiRegion_NoClusterRegions_ExplainsLocality()
    {
        var report = MultiRegionCheck.Evaluate(
            State(null, One("orders", null)),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.Contains(report.Problems, p => p.Contains("cluster has no regions"));
        Assert.DoesNotContain(report.Statements, s => s.Contains("SET PRIMARY REGION"));
    }

    [Fact]
    public void AnnotatedEntity_OnMissingTable_IsProblem()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", new Dictionary<TableName, TableInfo>()),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.False(report.HasContradictions);
        Assert.Contains(report.Problems, p => p.Contains("does not exist"));
    }

    [Fact]
    public void KeywordTableName_IsQuotedInMessages()
    {
        // "order" is a reserved keyword, so it must be double-quoted as an identifier.
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("order", "GLOBAL")),
            [Entity("order", MultiRegionKind.None)]);

        Assert.Contains(report.Problems, p => p.Contains("public.\"order\""));
    }

    [Fact]
    public void RegionalByRow_RegionColumnWrongType_IsProblem()
    {
        // Table is REGIONAL BY ROW on the implicit crdb_region column, but the entity's
        // @CrdbRegion maps to home_region, which exists with the wrong (non-region) type.
        var types = new Dictionary<string, string> { ["home_region"] = "STRING" };
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("accounts", "REGIONAL BY ROW", types)),
            [Entity("accounts", MultiRegionKind.RegionalByRow, "home_region")]);

        Assert.Contains(report.Problems, p => p.Contains("needs a crdb_internal_region column"));
    }

    // Teeth-check: a contradiction must fail startup even in warn mode. Reverting
    // MultiRegionReport.FailsStartup to drop the "|| HasContradictions" clause turns this red.
    [Fact]
    public void Contradiction_FailsStartup_EvenInWarnMode()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "REGIONAL BY ROW")),
            [Entity("orders", MultiRegionKind.Global)]);

        Assert.True(report.HasContradictions);
        Assert.True(report.FailsStartup(MultiRegionCheckMode.Warn));
    }

    [Fact]
    public void NonContradictionProblem_DoesNotFailStartup_InWarnMode()
    {
        var report = MultiRegionCheck.Evaluate(
            State("us-east1", One("orders", "GLOBAL")),
            [Entity("orders", MultiRegionKind.None)]);

        Assert.False(report.IsEmpty);
        Assert.False(report.FailsStartup(MultiRegionCheckMode.Warn));
        Assert.True(report.FailsStartup(MultiRegionCheckMode.Validate));
    }
}
