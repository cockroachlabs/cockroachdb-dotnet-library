using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CockroachDB.DotNet.Core.MultiRegion;

/// <summary>
/// Parses and quotes SQL identifiers the way CockroachDB treats them: unquoted names fold to
/// lower case, quoted names are kept verbatim. Used to read the region column out of a
/// <c>REGIONAL BY ROW AS "col"</c> locality string and to emit safe fix-up SQL.
/// </summary>
internal static partial class Identifiers
{
    [GeneratedRegex("^[a-z_][a-z0-9_$]*$")]
    private static partial Regex Plain();

    /// <summary>
    /// Splits a possibly qualified, possibly quoted name such as <c>app."Orders"</c> into its
    /// parts, as CockroachDB stores them.
    /// </summary>
    internal static List<string> Parse(string rendered)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        var inQuotes = false;
        var quoted = false;
        for (var i = 0; i < rendered.Length; i++)
        {
            var c = rendered[i];
            if (inQuotes)
            {
                if (c != '"')
                {
                    part.Append(c);
                }
                else if (i + 1 < rendered.Length && rendered[i + 1] == '"')
                {
                    part.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
                quoted = true;
            }
            else if (c == '.')
            {
                parts.Add(Finish(part, quoted));
                part.Clear();
                quoted = false;
            }
            else
            {
                part.Append(c);
            }
        }

        parts.Add(Finish(part, quoted));
        return parts;
    }

    private static string Finish(StringBuilder part, bool quoted)
    {
        var name = part.ToString();
        return quoted ? name : name.Trim().ToLowerInvariant();
    }

    /// <summary>Quotes <paramref name="name"/> when it can't be written as a plain identifier.</summary>
    internal static string Quote(string name, IReadOnlySet<string> keywords)
    {
        if (Plain().IsMatch(name) && !keywords.Contains(name))
        {
            return name;
        }

        return '"' + name.Replace("\"", "\"\"") + '"';
    }

    /// <summary>Quotes a region name as a string-like double-quoted literal for <c>SET PRIMARY REGION</c>.</summary>
    internal static string RegionLiteral(string region) => '"' + region.Replace("\"", "\"\"") + '"';
}
