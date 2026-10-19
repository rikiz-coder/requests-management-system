using RequestsManagement.Application.Requests.Contracts;

namespace RequestsManagement.Api.Http;

/// <summary>
/// The request's row version as an HTTP entity tag: returned in <c>ETag</c>, expected back in <c>If-Match</c>.
/// </summary>
public static class EntityTags
{
    public static string Format(string version) => $"\"{version}\"";

    /// <summary>
    /// Parses an If-Match header. Only a single, strong tag holding a row version is usable:
    /// <c>*</c> means "overwrite whatever is there" – exactly what optimistic concurrency must prevent –
    /// and a weak tag or a list of tags cannot identify one version.
    /// </summary>
    public static IfMatchResult ParseIfMatch(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return IfMatchResult.Missing;
        }

        var value = header.Trim();
        if (value == "*" || value.StartsWith("W/", StringComparison.Ordinal) || value.Contains(','))
        {
            return IfMatchResult.Invalid;
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return RowVersions.TryDecode(value, out var version) ? IfMatchResult.Valid(version) : IfMatchResult.Invalid;
    }
}

public sealed record IfMatchResult(IfMatchStatus Status, byte[] Version)
{
    public static readonly IfMatchResult Missing = new(IfMatchStatus.Missing, []);
    public static readonly IfMatchResult Invalid = new(IfMatchStatus.Invalid, []);

    public static IfMatchResult Valid(byte[] version) => new(IfMatchStatus.Valid, version);
}

public enum IfMatchStatus
{
    Missing,
    Invalid,
    Valid,
}
