# מערכת לניהול פניות – מבדק Full Stack

[![CI](https://github.com/rikiz-coder/requests-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/rikiz-coder/requests-management-system/actions/workflows/ci.yml)

מערכת לניהול פניות שמגיעות מארגונים ומעסיקים. כמה משתמשים עובדים בה במקביל על 100,000 פניות: איתור, סינון, מיון ודפדוף בצד השרת, עדכון סטטוס עם הגנה מפני עדכונים מתחרים, עדכון מרוכז (Bulk), היסטוריית שינויים ונתונים מסכמים.

> 🚧 **בפיתוח.** הפרויקט נבנה בשלבים לפי [תוכנית העבודה](PLAN.md). סעיפים שמסומנים "בשלב הבא" יושלמו עם הקוד.

| מסמך | מה יש בו |
|---|---|
| README (כאן) | מדריך לבודק, הרצה, טכנולוגיות, מבנה, בסיס נתונים, Concurrency, Bulk, Cache, החלטות, מגבלות, שימוש ב־AI |
| [PLAN.md](PLAN.md) | פירוק האפיון למשימות: תוצרים, תלויות, סדר ביצוע, הערכת מאמץ וקישור לדרישות |
| [docs/INSTALL.he.md](docs/INSTALL.he.md) | התקנת הסביבה ב־Windows (Git, Docker Desktop, ‎.NET, Node) |
| [docs/PERFORMANCE.md](docs/PERFORMANCE.md) | מדידות על 100,000 פניות: Execution Plans, אינדקסים, צוואר בקבוק ושיפורים |
| [docs/performance/measure.sql](docs/performance/measure.sql) | סקריפט המדידה (רץ גם ב־SSMS) |

---

## מדריך לבודק

### הרצה

**דרישות:** Docker Desktop (או Docker Engine עם Compose). זה הכול – אין צורך ב־‎.NET, ב־Node או ב־SQL Server מקומי. להתקנה ב־Windows: [docs/INSTALL.he.md](docs/INSTALL.he.md).

```powershell
git clone https://github.com/rikiz-coder/requests-management-system.git
cd requests-management-system
docker compose up -d --build
```

ההרצה הראשונה בונה את ה־Images, מרימה SQL Server 2022, יוצרת את בסיס הנתונים ומכניסה 100,000 פניות (כמה דקות בפעם הראשונה, בעיקר הורדות). אחר כך:

| מה | כתובת |
|---|---|
| ממשק המשתמש | http://localhost:8080 (בשלב הבא) |
| Swagger (תיעוד ה־API) | http://localhost:5080/swagger |
| בדיקת תקינות | http://localhost:5080/health |
| SQL Server (ל־SSMS) | `localhost,14333` · משתמש `sa` · סיסמה `Requests_Dev_Passw0rd!` |

**יצירה מחדש של נתוני הבדיקה** (מוחק הכול ויוצר מחדש, עם אותו Seed, כלומר אותם נתונים בדיוק):

```powershell
docker compose run --rm seeder --reseed                 # 100,000
docker compose run --rm seeder --reseed --count 250000  # כמות אחרת
```

**עצירה:** `docker compose down` (הנתונים נשמרים) · `docker compose down -v` (מוחק גם את הנתונים).

### מסלול בדיקה בממשק

בשלב הבא: טבלה של מה עושים, מה אמור לקרות ואיזו דרישה זה מדגים.

### לראות 409 משתי פקודות

שני "משתמשים" מעדכנים את אותה פנייה עם אותו `ETag`. מתיקיית הפרויקט, כשהמערכת רצה:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\demo-409.ps1
```

הסקריפט בוחר פנייה במצב "בטיפול", קורא אותה פעם אחת, ושולח שני עדכונים עם אותה גרסה:

```text
Dana  -> Waiting:   HTTP 200
Yossi -> Completed: HTTP 409
Body of the 409 response:
{"title":"Concurrency conflict","status":409,"detail":"Request … was modified by another user. Reload it and try again.",
 "currentState":{"id":…,"status":"Waiting","version":"…","allowedNextStatuses":["InProgress","Completed"],…},
 "lastChange":{"previousStatus":"InProgress","newStatus":"Waiting","changedAt":"…Z","changedBy":"Dana"}}
Without If-Match:   HTTP 428
```

אותו דבר ב־Bash (Git Bash / Mac / Linux):

```bash
ID=$(curl -s "http://localhost:5080/api/requests?status=InProgress&pageSize=1" | grep -o '"id":[0-9]*' | head -1 | cut -d: -f2)
ETAG=$(curl -si http://localhost:5080/api/requests/$ID | grep -i '^etag:' | cut -d' ' -f2 | tr -d '\r')
curl -s -o /dev/null -w "Dana:  %{http_code}\n" -X PATCH http://localhost:5080/api/requests/$ID/status \
  -H "If-Match: $ETAG" -H "X-User-Name: Dana" -H "Content-Type: application/json" -d '{"newStatus":"Waiting"}'
curl -s -w "\nYossi: %{http_code}\n" -X PATCH http://localhost:5080/api/requests/$ID/status \
  -H "If-Match: $ETAG" -H "X-User-Name: Yossi" -H "Content-Type: application/json" -d '{"newStatus":"Completed"}'
```

הסקריפט רץ גם ב־CI מול המערכת המלאה, כך שהפלט שלמעלה נבדק בכל שינוי.

### בדיקות אוטומטיות

```powershell
cd backend
dotnet test     # דורש ‎.NET 10 SDK ו־Docker פתוח: בדיקות ה־Integration מרימות SQL Server זמני משלהן
```

* **50 בדיקות Unit:** כללי מעברי סטטוס, שינוי סטטוס בישות, ולידציה (דפדוף, תאריכים, ערכים לא מוכרים, Bulk), פענוח `If-Match`, מחולל הנתונים.
* **34 בדיקות Integration** מול SQL Server 2022 אמיתי (Testcontainers) דרך ה־API עצמו (`WebApplicationFactory`): סינון, חיפוש, מיון ודפדוף מול ספירה ישירה בבסיס הנתונים; 400 לכל פרמטר לא תקין; Summary ו־Invalidation של ה־Cache; ‏404/409/422/428; **5 עדכונים מקבילים עם אותו `ETag` – בדיוק אחד מצליח ונכתבת שורת היסטוריה אחת**; Bulk עם הצלחה חלקית; Migrations, אינדקסים ו־Seed חוזר.

בדיקות אמיתיות מול SQL Server ולא מול ספק ה־InMemory של EF: הוא לא אוכף `rowversion`, ובדיקת Concurrency מולו הייתה עוברת גם אם המנגנון שבור.

### איפה כל דרישה

| דרישה | קוד | בדיקות | תיעוד |
|---|---|---|---|
| נתונים: מבנה פנייה, 100K רשומות, יצירה חוזרת | [`ServiceRequest`](backend/src/RequestsManagement.Application/Requests/Entities/ServiceRequest.cs), [`SeedDataGenerator`](backend/src/RequestsManagement.Infrastructure/Persistence/Seeding/SeedDataGenerator.cs), [`DatabaseSeeder`](backend/src/RequestsManagement.Infrastructure/Persistence/Seeding/DatabaseSeeder.cs) | `SeedDataGeneratorTests`, `SeedingTests` | [בסיס הנתונים](#בסיס-הנתונים) |
| 1 שליפה: דפדוף, סינון, חיפוש, מיון, Aggregations, ולידציה, Cancellation, DTOs | [`RequestRepository`](backend/src/RequestsManagement.Infrastructure/Persistence/Repositories/RequestRepository.cs), [`RequestQueryExtensions`](backend/src/RequestsManagement.Infrastructure/Persistence/Repositories/RequestQueryExtensions.cs), [`RequestValidators`](backend/src/RequestsManagement.Application/Requests/Validation/RequestValidators.cs), [`RequestQueryService`](backend/src/RequestsManagement.Application/Requests/Services/RequestQueryService.cs) | `SearchTests`, `SummaryAndDetailsTests`, `ValidatorTests` | [API](#api) |
| 2 עדכון סטטוס, מעברים, Optimistic Concurrency | [`RequestCommandService`](backend/src/RequestsManagement.Application/Requests/Services/RequestCommandService.cs), [`StatusTransitions`](backend/src/RequestsManagement.Application/Requests/Entities/StatusTransitions.cs), [`EntityTags`](backend/src/RequestsManagement.Api/Http/EntityTags.cs), [`GlobalExceptionHandler`](backend/src/RequestsManagement.Api/ErrorHandling/GlobalExceptionHandler.cs) | `UpdateStatusTests`, `StatusTransitionsTests`, `ServiceRequestTests`, `EntityTagsTests` | [Concurrency](#עדכון-סטטוס-ו־concurrency) |
| 3 היסטוריה (Audit) | [`RequestStatusHistory`](backend/src/RequestsManagement.Application/Requests/Entities/RequestStatusHistory.cs), [`ServiceRequest.ChangeStatus`](backend/src/RequestsManagement.Application/Requests/Entities/ServiceRequest.cs) | `UpdateStatusTests` (כל עדכון בודק את שורת ההיסטוריה), `SummaryAndDetailsTests` | [API](#api) |
| 4 Bulk עד 100, הצלחה חלקית | [`RequestCommandService.BulkUpdateStatusAsync`](backend/src/RequestsManagement.Application/Requests/Services/RequestCommandService.cs), [`RequestRepository.SaveStatusChangesAsync`](backend/src/RequestsManagement.Infrastructure/Persistence/Repositories/RequestRepository.cs) | `BulkUpdateTests`, `ValidatorTests` | [Bulk](#עדכון-מרוכז-bulk--הצלחה-חלקית) |
| 7 Cache | [`MemorySummaryCache`](backend/src/RequestsManagement.Infrastructure/Caching/MemorySummaryCache.cs), [`RequestQueryService.GetSummaryAsync`](backend/src/RequestsManagement.Application/Requests/Services/RequestQueryService.cs) | `SummaryAndDetailsTests` | [Cache](#cache) |
| 8 בדיקות, כולל Integration ו־Concurrency | [`tests/`](backend/tests) | 50 Unit + 34 Integration | [בדיקות אוטומטיות](#בדיקות-אוטומטיות) |
| 9 תכנון | – | – | [PLAN.md](PLAN.md) |
| 10 איכות קוד, שגיאות, Logging | [`Program.cs`](backend/src/RequestsManagement.Api/Program.cs), [`GlobalExceptionHandler`](backend/src/RequestsManagement.Api/ErrorHandling/GlobalExceptionHandler.cs) | `Invalid_parameters_return_400_naming_the_field`, `Unknown_request_returns_404_problem_details` | [Logging](#logging) |
| 6 ביצועים על 100K | [`ServiceRequestConfiguration`](backend/src/RequestsManagement.Infrastructure/Persistence/Configurations/ServiceRequestConfiguration.cs) (אינדקסים), [`measure.py`](scripts/perf/measure.py) | [`measure.sql`](docs/performance/measure.sql), [`runs/`](docs/performance/runs) | [PERFORMANCE.md](docs/PERFORMANCE.md) |
| 5, 11 | בשלבים הבאים | | |

### פתרון תקלות

| תקלה | פתרון |
|---|---|
| `port is already allocated` | פורט תפוס. משנים בקובץ `.env` ליד `docker-compose.yml`: `SQL_PORT=14334` או `API_PORT=5081`, ומריצים שוב `docker compose up -d` |
| ה־API לא עולה, או `seeder` נכשל | `docker compose ps -a` ו־`docker compose logs seeder`. SQL Server צריך להיות `healthy`; בהרצה הראשונה זה לוקח עד דקה |
| SQL Server נופל מיד | חסר זיכרון. ב־Docker Desktop: Settings ← Resources, לפחות 4GB |
| Mac עם Apple Silicon | ה־Image של SQL Server הוא amd64. ב־Docker Desktop מפעילים "Use Rosetta for x86_64/amd64 emulation" |
| תקלות התקנה ב־Windows | [docs/INSTALL.he.md – פתרון תקלות](docs/INSTALL.he.md#8-פתרון-תקלות) |

---

## טכנולוגיות וגרסאות

| שכבה | טכנולוגיה |
|---|---|
| שרת | ‎.NET 10 (LTS), ASP.NET Core Web API (Controllers), EF Core 10 |
| בסיס נתונים | SQL Server 2022 (Docker) |
| לקוח | Angular, Angular Material, RxJS (בשלב הבא) |
| בדיקות שרת | xUnit v3, Testcontainers (SQL Server אמיתי בקונטיינר), `WebApplicationFactory` |
| נתוני בדיקה | מחולל דטרמיניסטי (Seed קבוע) + `SqlBulkCopy` |
| תיעוד API | Swagger (Swashbuckle) |
| הרצה | Docker Compose |
| CI | GitHub Actions: Build, בדיקה שה־Migrations תואמות למודל, בדיקות, והרצת Docker Compose מלאה עם 100,000 פניות |

## מבנה הפתרון

```text
backend/
  src/
    RequestsManagement.Api/              HTTP בלבד: Controllers, טיפול בשגיאות, הרכבת DI
    RequestsManagement.Application/      הלוגיקה, בלי EF Core ובלי ASP.NET Core
      Requests/Entities/                   ServiceRequest, RequestStatusHistory, StatusTransitions
    RequestsManagement.Infrastructure/   EF Core: DbContext, קונפיגורציה ואינדקסים, Migrations, Seeding
  tools/
    RequestsManagement.Seeder/           כלי שורת פקודה: Migrations ויצירת נתוני הבדיקה
  tests/
    RequestsManagement.UnitTests/        בדיקות לוגיקה ומחולל הנתונים
    RequestsManagement.IntegrationTests/ בדיקות מול SQL Server אמיתי (Testcontainers)
docs/                                    התקנה, ביצועים
scripts/                                 התקנה ובדיקת סביבה ב־Windows
```

**כיוון התלויות:** `Api → Application ← Infrastructure`. שכבת Application לא מכירה אף שכבה אחרת; היא מגדירה ממשקים, ו־Infrastructure מממשת אותם. כך הקומפיילר אוכף את ההפרדה.

## API

| Method | Endpoint | תיאור | קודים |
|---|---|---|---|
| GET | `/api/requests` | רשימה: סינון, חיפוש, מיון ודפדוף בצד השרת | 200, 400 |
| GET | `/api/requests/summary` | נתונים מסכמים (לפי סטטוס ולפי עדיפות) לאותם סינונים | 200, 400 |
| GET | `/api/requests/{id}` | פנייה + `allowedNextStatuses`; כותרת `ETag` עם הגרסה | 200, 404 |
| GET | `/api/requests/{id}/history` | היסטוריית שינויי סטטוס, מהחדש לישן | 200, 404 |
| PATCH | `/api/requests/{id}/status` | עדכון סטטוס `{ newStatus }` + כותרות `If-Match`, `X-User-Name` | 200, 400, 404, **409**, 422, 428 |
| POST | `/api/requests/bulk/status` | עדכון עד 100 פניות `{ newStatus, items: [{ id, version }] }` + `X-User-Name` | 200, 400 |
| GET | `/api/lookups` | מטפלים וארגונים לתיבות הסינון ולבחירת המשתמש הנוכחי | 200 |

**פרמטרי סינון** (ברשימה וב־Summary, כולם אופציונליים, משולבים ב־AND):
`status` / `priority` – אחד או יותר (`?status=New&status=Waiting`) · `organizationName` – מתחיל ב־ · `assignedTo` – שם מדויק · `createdFrom` / `createdTo` – תאריכים, כולל (UTC) · `search` – מכיל, בכותרת או בשם הארגון.
ברשימה בלבד: `page` (1–100,000), `pageSize` (1–100, ברירת מחדל 20), `sortBy` (`createdAt` · `updatedAt` · `priority` · `status` · `title` · `organizationName`), `sortDirection` (`asc` · `desc`). המיון תמיד ממשיך לפי `Id`, כדי שהדפדוף יהיה יציב.

**ולידציה ושגיאות:** פרמטר לא חוקי (`pageSize=500`, `status=99`, `sortBy=password`, טווח תאריכים הפוך, תאריך לא תקין, סטטוס לא מוכר בגוף הבקשה) מחזיר **400** עם שם השדה. כל שגיאה מוחזרת כ־**Problem Details** (RFC 9457) מ־[`GlobalExceptionHandler`](backend/src/RequestsManagement.Api/ErrorHandling/GlobalExceptionHandler.cs), כולל `traceId`. שגיאה לא צפויה נרשמת בלוג ומוחזרת כ־500 בלי פרטים פנימיים. Enums מוחזרים כשמות (`"InProgress"`) בכל תשובה, גם בגוף של שגיאה.

**CancellationToken** עובר מה־Controller ועד שאילתת EF Core. כשהלקוח מבטל בקשה (למשל חיפוש חדש מחליף קודם), השאילתה ב־SQL Server מבוטלת, ונרשמת שורת Information ולא שגיאה.

**מי ביצע את השינוי:** אין מערכת התחברות (לא נדרשה). הלקוח שולח את שם המשתמש בכותרת `X-User-Name`. ערכי כותרות HTTP הם ASCII בלבד, ולכן שם בעברית נשלח מקודד (`encodeURIComponent`) והשרת מפענח אותו ([`UserHeader`](backend/src/RequestsManagement.Api/Http/UserHeader.cs)). ראו [מגבלות ידועות](#מגבלות-ידועות).

**איך נמנעת טעינת כל הנתונים לזיכרון:** כל שאילתה נבנית כ־`IQueryable` ורצה כולה ב־SQL Server: `WHERE` לפי הסינונים, `ORDER BY` לפי שדה מרשימה סגורה, `OFFSET/FETCH`, והטלה (`Select`) רק לעמודות שבתשובה. רק העמוד המבוקש (עד 100 שורות) יוצא מבסיס הנתונים. מספר העמודים מגיע מ־`COUNT(*)` נפרד עם אותם סינונים, והנתונים המסכמים הם `GROUP BY` שמחזיר עד 7 שורות.

## בסיס הנתונים

```text
Requests                                  RequestStatusHistory
  Id               int identity PK          Id              bigint identity PK
  Title            nvarchar(200)            RequestId       int FK → Requests
  OrganizationName nvarchar(200)            PreviousStatus  tinyint
  Status           tinyint                  NewStatus       tinyint
  Priority         tinyint                  ChangedAt       datetime2(3)  UTC
  AssignedTo       nvarchar(100) null       ChangedBy       nvarchar(100)
  CreatedAt        datetime2(3)  UTC
  UpdatedAt        datetime2(3)  UTC
  RowVersion       rowversion
```

* `Status` ו־`Priority` נשמרים כ־`tinyint` עם ערכים מפורשים: עמודות ואינדקסים צרים, ושינוי סדר ב־enum לא משנה את משמעות הנתונים.
* אורכי השדות מוגדרים פעם אחת ([`RequestFieldLimits`](backend/src/RequestsManagement.Application/Requests/Entities/RequestFieldLimits.cs)) ומשמשים גם את הסכמה וגם את הוולידציה.
* כל התאריכים ב־UTC, בדיוק של מילישנייה.
* `RowVersion` מתעדכן על ידי SQL Server בכל שינוי, ומשמש לזיהוי עדכונים מתחרים.

**אינדקסים** (הנימוקים, המדידות ו־Execution Plans ב־[docs/PERFORMANCE.md](docs/PERFORMANCE.md)):

| אינדקס | בשביל מה |
|---|---|
| `IX_Requests_CreatedAt_Id` ‏(CreatedAt DESC, Id DESC) | הרשימה ללא סינון בסדר ברירת המחדל (ובסדר ההפוך); טווח תאריכים |
| `IX_Requests_Status_CreatedAt_Id` INCLUDE `Priority` | סינון לפי סטטוס בסדר הרשימה; ספירה לפי סטטוס |
| `IX_Requests_AssignedTo_CreatedAt_Id` INCLUDE `Status` | הפניות של מטפל (גם עם סטטוס); רשימת המטפלים |
| `IX_Requests_OrganizationName_CreatedAt_Id` | סינון לפי ארגון; רשימת הארגונים |
| `IX_Requests_Priority_Status` | סינון וספירה לפי עדיפות; הנתונים המסכמים |
| `IX_RequestStatusHistory_RequestId_ChangedAt` | היסטוריה של פנייה, מהחדש לישן; "מי שינה אחרון" ב־409 |

כל אינדקס שמשרת את הרשימה מסתיים ב־`CreatedAt DESC, Id DESC`, בדיוק כמו ה־`ORDER BY`. כך SQL Server קורא את העמוד בסדר האינדקס ועוצר אחרי 20 שורות. במדידה הראשונה האינדקסים נגמרו ב־`CreatedAt DESC` בלבד, ושאילתת העמוד סרקה ומיינה את כל הטבלה. התיקון הוריד את הרשימה ללא סינון מ־2,067 ל־221 reads, ואת הסינון לפי סטטוס מ־1,866 ל־110 ([הפירוט](docs/PERFORMANCE.md#מה-המדידה-הראשונה-גילתה--ומה-תוקן)).

**נתוני הבדיקה:** 100,000 פניות בדיוניות בעברית: 500 ארגונים, 30 מטפלים, תאריכים על פני שנתיים, והתפלגות לא אחידה של סטטוסים ועדיפויות. לכל פנייה שאינה "חדשה" נוצרת היסטוריה שעוברת רק במעברים מותרים ומסתיימת בסטטוס הנוכחי שלה. המחולל דטרמיניסטי (Seed קבוע), כך שכל הרצה יוצרת אותם נתונים ואפשר להשוות מדידות. הטעינה ב־`SqlBulkCopy`, ואחריה בנייה מחדש של האינדקסים ועדכון סטטיסטיקות.

## ביצועים

המדידות המלאות – Execution Plans בפועל, השוואה עם ובלי כל אינדקס, והסקריפט להרצה חוזרת – ב־[docs/PERFORMANCE.md](docs/PERFORMANCE.md). כל המספרים נמדדו על 100,000 פניות.

| קריאה ל־API | זמן (חציון) | Logical reads |
|---|---:|---:|
| רשימה ללא סינון, עמוד 1 | 13ms | 221 |
| סינון לפי סטטוס | 6ms | 110 |
| 2 סטטוסים + עדיפות + תאריך | 10ms | 871 |
| מטפל | 4ms | 92 |
| **חיפוש חופשי** | **315ms** | 2,502 – צוואר הבקבוק |
| עמוד 500, מיון לפי כותרת | 211ms | 1,916 |
| נתונים מסכמים ללא סינון (Cache) | 1ms | – |
| נתונים מסכמים עם סינון | 8ms | 66 |
| פנייה בודדת / היסטוריה | 2ms | 6 |

**צוואר הבקבוק:** חיפוש "מכיל" (`LIKE '%x%'`) לא יכול להשתמש באינדקס, וה־`COUNT` שלו סורק את כל הטבלה. רוב הזמן הוא CPU של השוואת מחרוזות לפי כללי השפה. ניסוי מדוד עם עמודה מחושבת ו־Collation בינארי הוריד את אותה שאילתה מ־306ms ל־22ms CPU (פי 14). זה השיפור המוצע, ובייצור – Full-Text Search ([פירוט](docs/PERFORMANCE.md#צוואר-הבקבוק-חיפוש-מכיל)).

## עדכון סטטוס ו־Concurrency

**הבעיה:** שני משתמשים פותחים את אותה פנייה. הראשון מעביר אותה ל"ממתינה", והשני, שעדיין רואה את המצב הקודם, ל"הושלמה". בלי הגנה, העדכון השני דורס את הראשון ואף אחד לא יודע (Lost Update).

**הפתרון: עדכון מותנה בגרסה, שנאכף בבסיס הנתונים:**
* לכל פנייה עמודת `rowversion`, ש־SQL Server מעדכן בעצמו בכל שינוי.
* `GET /api/requests/{id}` מחזיר אותה בכותרת `ETag`. ‏`PATCH` חייב לשלוח אותה ב־`If-Match`.
* ה־Repository מגדיר אותה כגרסה הצפויה, ו־EF Core שולח `UPDATE … WHERE Id = @id AND RowVersion = @expected`.
* אם מישהו אחר עדכן בינתיים, אף שורה לא מתעדכנת, וחוזר **409**. הבדיקה מתבצעת **בתוך ה־UPDATE**, כך שאין חלון זמן בין הבדיקה לכתיבה. בדיקה אוטומטית שולחת 5 עדכונים מקבילים עם אותה גרסה: בדיוק אחד מצליח, ונכתבת בדיוק שורת היסטוריה אחת.
* העדכון ושורת ההיסטוריה נשמרים באותה טרנזקציה (`SaveChanges` אחד). `UpdatedAt` מתעדכן יחד עם הסטטוס, מעוגל למילישניות כמו העמודה, כך שהערך שחוזר ללקוח הוא בדיוק הערך השמור.

| מצב | קוד |
|---|---|
| עודכן | 200 + `ETag` חדש |
| גוף לא תקין (סטטוס לא מוכר או חסר), או `X-User-Name` חסר | 400 |
| `If-Match` חסר | 428 |
| `If-Match: *`, תג חלש, כמה תגים או ערך שאינו גרסה | 400. `*` פירושו "דרוס מה שיש", בדיוק מה שהמנגנון נועד למנוע |
| הפנייה לא קיימת | 404 |
| הגרסה לא עדכנית | **409**, עם `currentState` (הפנייה כפי שהיא עכשיו) ו־`lastChange` (מי שינה, מה ומתי) |
| מעבר סטטוס אסור | 422, עם `currentStatus` ו־`allowedStatuses` |

**409 ולא 412:** ‏412 (Precondition Failed) הוא הקוד הצמוד לכישלון של `If-Match`, וגם הוא היה בחירה תקינה. בחרנו ב־409 כי מבחינת הלקוח זו התנגשות עם המצב הנוכחי של הפנייה, שאותה הוא יכול לפתור ולשלוח שוב (ההגדרה של 409 ב־RFC 9110), ובאותו מושג משתמש גם ה־Bulk, שבו אין `If-Match` והפריט מקבל תוצאה `Conflict`. הגוף מחזיר את המצב הנוכחי ואת מי ששינה, וזה מה שהממשק צריך כדי להסביר למשתמש מה קרה.

**מעברי סטטוס מותרים** (האפיון דורש לחסום מעברים לא מותרים אבל לא מגדיר אותם; זו ההנחה):

| מ־ | אל |
|---|---|
| חדשה (New) | בטיפול, ממתינה |
| בטיפול (InProgress) | ממתינה, הושלמה |
| ממתינה (Waiting) | בטיפול, הושלמה |
| הושלמה (Completed) | בטיפול (פתיחה מחדש) |

הכללים נמצאים במקום אחד ([`StatusTransitions`](backend/src/RequestsManagement.Application/Requests/Entities/StatusTransitions.cs)), והשרת מחזיר `allowedNextStatuses` כדי שהממשק יציג רק אפשרויות חוקיות.

## עדכון מרוכז (Bulk) – הצלחה חלקית

`POST /api/requests/bulk/status` מקבל עד 100 פניות, ולכל אחת את הגרסה (`version`) שהמשתמש ראה. כותרת `If-Match` אחת לא יכולה לשאת 100 גרסאות, ולכן כאן הגרסה בגוף הבקשה.

1. ולידציה של הבקשה כולה: 1–100 פריטים, בלי כפילויות, גרסאות תקינות, סטטוס מוכר, משתמש. אם היא נכשלת: **400**, ושום דבר לא מתעדכן.
2. כל הפניות נטענות בשאילתה אחת.
3. כל פריט נבדק לחוד: לא קיים → `NotFound`; גרסה לא עדכנית → `Conflict`; מעבר אסור → `InvalidTransition`.
4. כל הפריטים התקינים נשמרים **יחד, בטרנזקציה אחת**, כל אחד עם שורת היסטוריה ועם בדיקת הגרסה שלו.
5. אם בזמן השמירה מתברר שפנייה השתנתה (בין הקריאה לכתיבה), בסיס הנתונים דוחה את הטרנזקציה. מוציאים רק את הפנייה הזו (`Conflict`) ושומרים שוב את השאר. כל סבב מוציא לפחות פנייה אחת, כך שהלולאה תמיד מסתיימת.
6. התשובה: **200**, עם סיכום ותוצאה לכל פריט, באותו סדר כמו בבקשה:

```json
{ "requested": 5, "succeeded": 2, "failed": 3,
  "results": [
    { "id": 12,     "outcome": "Updated",           "error": null, "version": "AAAAAAAB4Kc=" },
    { "id": 999999, "outcome": "NotFound",          "error": "The request was not found.", "version": null },
    { "id": 31,     "outcome": "Conflict",          "error": "The request was modified by another user.", "version": null },
    { "id": 1,      "outcome": "InvalidTransition", "error": "Cannot move from New to Completed.", "version": null },
    { "id": 40,     "outcome": "Updated",           "error": null, "version": "AAAAAAAB4Kg=" } ] }
```

**למה הצלחה חלקית ולא "הכול או כלום":** כשמעדכנים עשרות פניות במערכת שבה הרבה משתמשים עובדים במקביל, סביר שאחת מהן שונתה בינתיים. ב"הכול או כלום" פנייה אחת הייתה חוסמת את כל הפעולה, והמשתמש היה צריך לרענן ולנסות שוב בלי לדעת מה השתנה. כאן מתעדכן מה שאפשר, וכל מה שלא עודכן מופיע עם הסיבה. כל פריט עדיין מוגן בבדיקת גרסה, כך שגם כאן אין דריסה.

**למה 200 ולא 207:** ‏207 Multi-Status שייך ל־WebDAV, ולקוחות רבים לא מטפלים בו. הבקשה עצמה עובדה; הפירוט בגוף. לכן הלקוח חייב לקרוא את `succeeded` / `failed` ואת ה־`outcome` של כל פריט, גם כשהקוד 200.

## Cache

**מה נשמר:** הנתונים המסכמים של **התצוגה ללא סינון**: המסך שכל משתמש מגיע אליו, והאגרגציה היקרה ביותר (כל 100,000 הפניות). תצוגות מסוננות מחושבות בכל בקשה: הן מצומצמות יותר, משתמשות באינדקסים, ויש להן אינסוף צירופים.

**למה לא את הרשימה:** לפני עדכון, המשתמש צריך לראות את המצב האמיתי. רשימה ישנה הייתה מובילה בדיוק להתנגשויות (409).

**המימוש** ([`MemorySummaryCache`](backend/src/RequestsManagement.Infrastructure/Caching/MemorySummaryCache.cs), ‏`IMemoryCache`, מאחורי הממשק `ISummaryCache`):
* **Expiration:** ‏60 שניות (`Cache:SummaryTtlSeconds`). רשת ביטחון, גם לשינויים שלא עברו דרך ה־API.
* **Invalidation:** כל עדכון סטטוס שנשמר, בודד או מרוכז, מוחק את הערך. הבקשה הבאה מחשבת אותו מחדש. בדיקה אוטומטית מוודאת שהספירות משתנות מיד אחרי עדכון.

**כמה מופעי שרת:** `IMemoryCache` שייך למופע אחד. מחיקה במופע A לא מוחקת במופע B, שימשיך להציג נתונים ישנים עד שיפוג התוקף. ההתאמה הנדרשת:
1. **Cache משותף (Redis)** דרך `IDistributedCache`: ה־Invalidation מוחק את המפתח ב־Redis, וכל המופעים רואים זאת מיד. מחליפים רק את המימוש של `ISummaryCache` ב־Infrastructure.
2. אם רוצים גם עותק מקומי מהיר בכל מופע: הודעת Invalidation דרך **Redis Pub/Sub**, שכל מופע מאזין לה ומוחק את העותק שלו (או `HybridCache` של ‎.NET עם Tags).
3. בשני המקרים התפוגה נשארת כרשת ביטחון.

## החלטות טכנולוגיות משמעותיות

בשלב הבא.

## מגבלות ידועות

בשלב הבא.

## Logging

* עדכון סטטוס: Information, עם מזהה, סטטוס קודם וחדש. Bulk: Information, כמה עודכנו וכמה לא.
* ‏404 / 409 / 422: Warning. שגיאה לא צפויה: Error עם ה־Exception.
* ‏400 (ולידציה או קלט שלא ניתן לקרוא): Warning עם **שמות השדות והנתיב בלבד**, בלי הערכים שנשלחו.
* בקשה שבוטלה על ידי הלקוח: Information, לא שגיאה.
* לא נרשמים גופי בקשות, מחרוזות התחברות או שמות משתמשים. פקודות SQL לא נרשמות כברירת מחדל.

## שימוש בכלי AI

בשלב הבא.
