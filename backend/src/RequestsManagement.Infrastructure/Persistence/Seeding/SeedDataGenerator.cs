using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Infrastructure.Persistence.Seeding;

/// <summary>
/// Generates fictional test data. Deterministic: the same seed and count always produce exactly
/// the same rows, so measurements can be repeated and compared.
/// </summary>
internal sealed class SeedDataGenerator
{
    public const int DefaultSeed = 20261019;

    /// <summary>Fixed reference date, so that generated dates do not depend on when the seeder runs.</summary>
    public static readonly DateTime ReferenceDate = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    private const int DateRangeDays = 730;
    private const string SystemUser = "מערכת";

    private static readonly string[] OrganizationBases =
    [
        "אורן", "ברקת", "גלעד", "דקל", "הדר", "ורד", "זית", "חרמון", "טל", "יסמין",
        "כרמל", "לוטם", "מור", "נחל", "סלע", "ערבה", "פרי הגליל", "צור", "קשת", "רימון",
        "שקד", "תמר", "אלון", "ברוש", "גפן",
    ];

    private static readonly string[] OrganizationKinds =
    [
        "תעשיות בע\"מ", "שירותי ניקיון", "הנדסה ובנייה", "מזון ומשקאות", "לוגיסטיקה",
        "טכנולוגיות", "חקלאות", "סחר ושיווק", "מוסך ושירות", "הסעות",
        "אבטחה ושמירה", "מערכות מידע", "דפוס והוצאה לאור", "ייצור מתכת", "שיווק ישיר",
        "ייעוץ ארגוני", "רשת חנויות", "מלונאות", "תחזוקת מבנים", "אריזות",
    ];

    private static readonly string[] Handlers =
    [
        "אברהם כהן", "יצחק לוי", "יעקב מזרחי", "משה פרץ", "אהרן ביטון",
        "יוסף אברהם", "דוד פרידמן", "שמואל אזולאי", "בנימין כץ", "אליהו דהן",
        "נתן אוחיון", "מנחם גולדברג", "חיים שפירא", "ראובן חדד", "שמעון רוזנברג",
        "יהודה אלבז", "זאב ויס", "מרדכי עמר", "אליעזר שטרן", "גדליה הלוי",
        "צבי וקנין", "ברוך גבאי", "אפרים קליין", "נחום ברגר", "פנחס סעדה",
        "עזרא מלכה", "יחזקאל טל", "עמרם אוחנה", "שלמה פישר", "מאיר בן דוד",
    ];

    private static readonly string[] Subjects =
    [
        "בקשה להיתר העסקה", "עדכון פרטי מעסיק", "בירור תשלום שכר", "בקשה לאישור משמרות",
        "דיווח על תאונת עבודה", "בקשה להארכת היתר", "שאלה בנושא זכויות עובדים", "עדכון כתובת סניף",
        "בקשה להחזר אגרה", "תלונה על עיכוב בטיפול", "בקשה לפגישת ייעוץ", "עדכון רשימת עובדים",
        "בירור חוב", "בקשה לפריסת תשלומים", "הגשת מסמכים חסרים", "בקשה לאישור הכשרה",
        "שינוי איש קשר", "בקשה לביטול רישום", "ערעור על החלטה", "בקשה לאישור ניכוי",
        "בירור סטטוס בקשה קודמת", "עדכון פרטי חשבון בנק", "בקשה להנפקת אישור", "דיווח על שינוי בעלות",
        "בקשה לתיאום ביקור", "שאלה בנושא שעות נוספות", "בקשה לאישור עבודה בחג", "עדכון היקף משרה",
        "בקשה לקבלת טפסים", "בירור הודעה שהתקבלה",
    ];

    private static readonly string[] Qualifiers =
    [
        "", "", "", " – דחוף", " – המשך טיפול", " – סניף צפון", " – סניף דרום",
        " – סניף מרכז", " – פנייה חוזרת", " – לפי בקשת המנהל",
    ];

    private readonly Random _random;

    public SeedDataGenerator(int seed = DefaultSeed) => _random = new Random(seed);

    public static IReadOnlyList<string> HandlerNames => Handlers;

    public static IReadOnlyList<string> OrganizationNames { get; } =
        OrganizationBases.SelectMany(b => OrganizationKinds.Select(k => $"{b} {k}")).ToArray();

    public SeedData Generate(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var requests = new List<ServiceRequest>(count);
        var history = new List<RequestStatusHistory>(count * 2);

        for (var id = 1; id <= count; id++)
        {
            var status = PickStatus();
            var createdAt = ReferenceDate
                .AddDays(-_random.Next(DateRangeDays))
                .AddSeconds(-_random.Next(86_400))
                .AddMilliseconds(-_random.Next(1000));

            // A new request may still be unassigned; any request that was worked on has a handler.
            string? assignedTo = status == RequestStatus.New && _random.NextDouble() < 0.4
                ? null
                : Handlers[_random.Next(Handlers.Length)];

            var request = new ServiceRequest
            {
                Id = id,
                Title = Subjects[_random.Next(Subjects.Length)] + Qualifiers[_random.Next(Qualifiers.Length)],
                OrganizationName = OrganizationNames[_random.Next(OrganizationNames.Count)],
                Status = status,
                Priority = PickPriority(),
                AssignedTo = assignedTo,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };

            AddHistory(request, history);
            requests.Add(request);
        }

        return new SeedData(requests, history);
    }

    /// <summary>
    /// Builds a status path that obeys the allowed transitions (New → … → current status)
    /// and writes one audit row per step. UpdatedAt is the time of the last step.
    /// </summary>
    private void AddHistory(ServiceRequest request, List<RequestStatusHistory> history)
    {
        RequestStatus[] path = request.Status switch
        {
            RequestStatus.New => [],
            RequestStatus.InProgress => [RequestStatus.InProgress],
            RequestStatus.Waiting => _random.NextDouble() < 0.5
                ? [RequestStatus.Waiting]
                : [RequestStatus.InProgress, RequestStatus.Waiting],
            RequestStatus.Completed => _random.NextDouble() < 0.7
                ? [RequestStatus.InProgress, RequestStatus.Completed]
                : [RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.InProgress, RequestStatus.Completed],
            _ => throw new InvalidOperationException($"Unknown status {request.Status}."),
        };

        var previous = RequestStatus.New;
        var time = request.CreatedAt;
        foreach (var next in path)
        {
            // Up to a week per step; for recent requests a fraction of the time left, so that changes
            // are spread out before the reference date instead of all landing exactly on it.
            var step = TimeSpan.FromMinutes(_random.Next(10, 60 * 24 * 7));
            var remaining = ReferenceDate - time;
            if (step > remaining / 2)
            {
                step = TimeSpan.FromMilliseconds(Math.Floor(remaining.TotalMilliseconds * (0.1 + (0.4 * _random.NextDouble()))));
            }

            time = Min(time + step, ReferenceDate);
            history.Add(new RequestStatusHistory
            {
                RequestId = request.Id,
                PreviousStatus = previous,
                NewStatus = next,
                ChangedAt = time,
                ChangedBy = request.AssignedTo ?? SystemUser,
            });
            previous = next;
        }

        request.UpdatedAt = time;
    }

    // Realistic, uneven distributions (percentages).
    private RequestStatus PickStatus() => _random.Next(100) switch
    {
        < 20 => RequestStatus.New,
        < 50 => RequestStatus.InProgress,
        < 65 => RequestStatus.Waiting,
        _ => RequestStatus.Completed,
    };

    private RequestPriority PickPriority() => _random.Next(100) switch
    {
        < 30 => RequestPriority.Low,
        < 80 => RequestPriority.Medium,
        _ => RequestPriority.High,
    };

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}

internal sealed record SeedData(IReadOnlyList<ServiceRequest> Requests, IReadOnlyList<RequestStatusHistory> History);
