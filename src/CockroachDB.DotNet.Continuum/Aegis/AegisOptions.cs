namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// Configuration for the Cockroach Aegis client.
///
/// <code>
/// CockroachDB:Continuum:Aegis:Enabled  = true    // register an IAegisClient
/// CockroachDB:Continuum:Aegis:Endpoint =         // blank -> SimulatedAegisClient
/// CockroachDB:Continuum:Aegis:Token    =
/// </code>
/// </summary>
public sealed class AegisOptions
{
    /// <summary>Configuration section binding path.</summary>
    public const string SectionName = "CockroachDB:Continuum:Aegis";

    /// <summary>Whether an Aegis client is registered. Default <c>true</c> (simulated).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Aegis API endpoint. Blank selects the simulated client.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Bearer token for the Aegis API (used only when <see cref="Endpoint"/> is set).</summary>
    public string Token { get; set; } = "";
}
