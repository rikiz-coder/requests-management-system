namespace RequestsManagement.Application.Requests.Contracts;

/// <summary>Converts the 8-byte SQL Server rowversion to and from the string the client sees.</summary>
public static class RowVersions
{
    public const int Length = 8;

    public static string Encode(byte[] version) => Convert.ToBase64String(version);

    public static bool TryDecode(string? value, out byte[] version)
    {
        version = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[Length];
        if (!Convert.TryFromBase64String(value.Trim(), buffer, out var written) || written != Length)
        {
            return false;
        }

        version = buffer;
        return true;
    }
}
