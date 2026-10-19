namespace RequestsManagement.Application.Requests.Entities;

/// <summary>
/// Field lengths, defined once and shared by the database schema and the input validation.
/// </summary>
public static class RequestFieldLimits
{
    public const int TitleMaxLength = 200;
    public const int OrganizationNameMaxLength = 200;
    public const int PersonNameMaxLength = 100;
}
