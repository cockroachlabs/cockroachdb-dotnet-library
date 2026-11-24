using System.Collections.Generic;
using System.Text;
using CockroachDB.DotNet.Abstractions.Configuration;

namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// The problems the multi-region check found, and SQL that fixes them. A contradiction (an
/// attribute that names one locality while the table has the other, or attributes that disagree
/// with each other) is a problem that stops startup even in <see cref="MultiRegionCheckMode.Warn"/> mode.
/// </summary>
internal sealed class MultiRegionReport
{
    internal const string ContradictionPrefix = "Contradiction: ";

    /// <summary>Who produced the report, by full type name, so a developer can find the class behind the message.</summary>
    internal static readonly string Source = typeof(MultiRegionCheck).FullName!;

    internal const string Property = "CockroachDB:MultiRegion:Mode";

    internal const string Overview = "https://docs.cockroachlabs.com/docs/stable/multiregion-overview";

    private readonly List<string> _problems = [];

    private readonly List<string> _statements = [];

    private int _contradictions;

    internal void Problem(string problem) => _problems.Add(problem);

    internal void Contradiction(string problem)
    {
        _problems.Add(ContradictionPrefix + problem);
        _contradictions++;
    }

    internal void Statement(string statement) => _statements.Add(statement);

    internal IReadOnlyList<string> Problems => _problems;

    internal IReadOnlyList<string> Statements => _statements;

    internal bool IsEmpty => _problems.Count == 0;

    internal bool HasContradictions => _contradictions > 0;

    /// <summary>
    /// Whether this report should stop startup under <paramref name="mode"/>: any problem in
    /// <see cref="MultiRegionCheckMode.Validate"/>, or any contradiction in any mode but
    /// <see cref="MultiRegionCheckMode.None"/>.
    /// </summary>
    internal bool FailsStartup(MultiRegionCheckMode mode)
        => !IsEmpty && (mode == MultiRegionCheckMode.Validate || HasContradictions);

    /// <summary>
    /// The report as one message. Its first line names the class that produced it and the
    /// configuration key, with its current value, that decides whether it logs or stops startup.
    /// </summary>
    internal string Format(MultiRegionCheckMode mode)
    {
        var modeName = mode.ToString().ToLowerInvariant();
        var problemCount = _problems.Count;
        var builder = new StringBuilder("CockroachDB multiregion check (")
            .Append(Source).Append(", ").Append(Property).Append('=').Append(modeName)
            .Append(") found ").Append(problemCount)
            .Append(problemCount == 1 ? " problem:" : " problems:");

        foreach (var problem in _problems)
        {
            builder.Append("\n  - ").Append(problem.Replace("\n", "\n    "));
        }

        if (_contradictions > 0)
        {
            builder.Append("\n\nContradictions stop startup in every mode but none: the attributes and the schema")
                .Append(" disagree about where rows live, so one of them is wrong.");
        }

        if (_statements.Count > 0)
        {
            builder.Append("\n\nSuggested SQL (review, then add to a migration; or alter the attributes):");
            foreach (var statement in _statements)
            {
                builder.Append("\n  ").Append(statement);
            }
        }

        builder.Append("\n\nSee also: ").Append(Overview);
        return builder.ToString();
    }
}
