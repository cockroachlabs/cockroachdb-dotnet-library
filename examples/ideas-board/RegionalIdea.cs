using CockroachDB.DotNet.Abstractions.Attributes;

namespace IdeasBoard;

// tag::regional-entity[]
// A REGIONAL BY ROW idea. ApplyCockroachDBAnnotations() reads [RegionalByRow] and
// annotates the table; [CrdbRegion] maps the Region property to the hidden
// crdb_region column, so each row is pinned to the region stored in that column.
[RegionalByRow(DefaultRegion = "us-east1")]
public partial class RegionalIdea
{
    public Guid Id { get; set; }

    public string Text { get; set; } = "";

    public int Votes { get; set; }

    [CrdbRegion]
    public string Region { get; set; } = "";
}
// end::regional-entity[]

public partial class RegionalIdea
{
    // Idempotent DDL for a cluster that is NOT multi-region (crdb_region as plain
    // text). On a real multi-region cluster the table would already exist as
    // REGIONAL BY ROW with crdb_region typed as crdb_internal_region, and this
    // CREATE IF NOT EXISTS no-ops.
    public const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS "RegionalIdeas" (
            "Id"          uuid    NOT NULL,
            "Text"        text    NOT NULL,
            "Votes"       integer NOT NULL,
            "crdb_region" text    NOT NULL,
            CONSTRAINT "PK_RegionalIdeas" PRIMARY KEY ("Id")
        );
        """;
}
