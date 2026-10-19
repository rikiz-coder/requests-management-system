# מערכת לניהול פניות – מבדק Full Stack

[![CI](https://github.com/rikiz-coder/requests-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/rikiz-coder/requests-management-system/actions/workflows/ci.yml)

מערכת לניהול פניות שמגיעות מארגונים ומעסיקים. כמה משתמשים עובדים בה במקביל על 100,000 פניות: איתור, סינון, מיון ודפדוף בצד השרת, עדכון סטטוס עם הגנה מפני עדכונים מתחרים, עדכון מרוכז (Bulk), היסטוריית שינויים ונתונים מסכמים.

> 🚧 **בפיתוח.** הפרויקט נבנה בשלבים לפי [תוכנית העבודה](PLAN.md). סעיפים שמסומנים "בשלב הבא" יושלמו עם הקוד.

| מסמך | מה יש בו |
|---|---|
| README (כאן) | מדריך לבודק, הרצה, טכנולוגיות, מבנה, בסיס נתונים, Concurrency, Bulk, Cache, החלטות, מגבלות, שימוש ב־AI |
| [PLAN.md](PLAN.md) | פירוק האפיון למשימות: תוצרים, תלויות, סדר ביצוע, הערכת מאמץ וקישור לדרישות |
| [docs/INSTALL.he.md](docs/INSTALL.he.md) | התקנת הסביבה ב־Windows (Git, Docker Desktop, ‎.NET, Node) |
| docs/PERFORMANCE.md | מדידות על 100,000 פניות: Execution Plans, אינדקסים, Bottlenecks ושיפורים (בשלב הבא) |

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

בשלב הבא.

### בדיקות אוטומטיות

```powershell
cd backend
dotnet test     # דורש ‎.NET 10 SDK ו־Docker פתוח: בדיקות ה־Integration מרימות SQL Server זמני משלהן
```

### איפה כל דרישה

| דרישה | קוד | בדיקות | תיעוד |
|---|---|---|---|
| נתונים: מבנה פנייה, 100K רשומות, יצירה חוזרת | [`ServiceRequest`](backend/src/RequestsManagement.Application/Requests/Entities/ServiceRequest.cs), [`SeedDataGenerator`](backend/src/RequestsManagement.Infrastructure/Persistence/Seeding/SeedDataGenerator.cs), [`DatabaseSeeder`](backend/src/RequestsManagement.Infrastructure/Persistence/Seeding/DatabaseSeeder.cs) | `SeedDataGeneratorTests`, `SeedingTests` | [בסיס הנתונים](#בסיס-הנתונים) |
| מעברי סטטוס | [`StatusTransitions`](backend/src/RequestsManagement.Application/Requests/Entities/StatusTransitions.cs) | `StatusTransitionsTests` | בשלב הבא |
| שאר הדרישות | בשלב הבא | | |

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

בשלב הבא.

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

**אינדקסים** (הנימוקים והמדידות יופיעו ב־docs/PERFORMANCE.md):

| אינדקס | בשביל מה |
|---|---|
| `IX_Requests_Status_CreatedAt` INCLUDE `Priority` | סינון לפי סטטוס עם מיון ברירת המחדל (החדשות קודם), וספירה לפי סטטוס |
| `IX_Requests_CreatedAt` | הרשימה ללא סינון ממוינת לפי תאריך, וטווח תאריכים |
| `IX_Requests_AssignedTo_Status` | "הפניות של מטפל X" |
| `IX_Requests_OrganizationName` | סינון לפי ארגון |
| `IX_Requests_Priority_Status` | סינון וספירה לפי עדיפות |
| `IX_RequestStatusHistory_RequestId_ChangedAt` | היסטוריה של פנייה, מהחדש לישן |

**נתוני הבדיקה:** 100,000 פניות בדיוניות בעברית: 500 ארגונים, 30 מטפלים, תאריכים על פני שנתיים, והתפלגות לא אחידה של סטטוסים ועדיפויות. לכל פנייה שאינה "חדשה" נוצרת היסטוריה שעוברת רק במעברים מותרים ומסתיימת בסטטוס הנוכחי שלה. המחולל דטרמיניסטי (Seed קבוע), כך שכל הרצה יוצרת אותם נתונים ואפשר להשוות מדידות. הטעינה ב־`SqlBulkCopy`, ואחריה בנייה מחדש של האינדקסים ועדכון סטטיסטיקות.

## ביצועים

בשלב הבא.

## עדכון סטטוס ו־Concurrency

בשלב הבא.

## עדכון מרוכז (Bulk) – הצלחה חלקית

בשלב הבא.

## Cache

בשלב הבא.

## החלטות טכנולוגיות משמעותיות

בשלב הבא.

## מגבלות ידועות

בשלב הבא.

## Logging

בשלב הבא.

## שימוש בכלי AI

בשלב הבא.
