namespace RequestsManagement.Api.Http;

/// <summary>
/// The "current user" header. HTTP header values are ASCII, so a Hebrew name is sent URL-encoded
/// (UTF-8, as JavaScript's encodeURIComponent produces) and decoded here. Plain ASCII passes unchanged.
/// </summary>
public static class UserHeader
{
    public const string Name = "X-User-Name";

    public static string Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(value).Trim();
        }
        catch (UriFormatException)
        {
            return value.Trim();
        }
    }
}
