# מדריך התקנה והרצה – מערכת לניהול פניות

המדריך מסביר איך להתקין את כל מה שצריך במחשב Windows, להוריד את הפרויקט, להריץ אותו ולבדוק אותו.


---

## 1. מה צריך להתקין

| רכיב | חובה? | למה הוא נחוץ |
|---|---|---|
| **WSL 2** | חובה | Docker Desktop רץ מעליו |
| **Git** | חובה | הורדת הפרויקט מ־GitHub |
| **Docker Desktop** | חובה | מריץ את SQL Server, את ה־API ואת ממשק ה־Angular בפקודה אחת |
| **.NET 10 SDK** | מומלץ | בנייה והרצת הבדיקות האוטומטיות מחוץ ל־Docker |
| **Node.js LTS** | מומלץ | הרצת ממשק ה־Angular במצב פיתוח |
| Visual Studio Code | רשות | עיון בקוד |
| SQL Server Management Studio | רשות | צפייה בטבלאות וב־Execution Plans |

### דרישות מחשב
- Windows 10 גרסה 22H2 ומעלה, או Windows 11.
- זיכרון: 8GB לפחות (16GB מומלץ). SQL Server לבדו צורך כ־2GB.
- כ־15GB פנויים בדיסק.
- וירטואליזציה מופעלת ב־BIOS. לבדיקה: מנהל המשימות ← ביצועים ← CPU ← "וירטואליזציה: מופעל".
- הרשאת מנהל (Administrator) למחשב.

---

## 2. התקנה אוטומטית (מומלץ)

יש בפרויקט סקריפט שמתקין הכול. הוא מדלג על רכיבים שכבר מותקנים, ולכן מותר להריץ אותו שוב.

1. פותחים את תפריט התחל, מקלידים **PowerShell**, לוחצים עליו בכפתור ימני ובוחרים **"הפעל כמנהל"** (Run as administrator).
2. עוברים לתיקייה שבה נמצא הסקריפט:
   - **לפני שהפרויקט הורד** (בהתקנה הראשונה Git עוד לא מותקן): שומרים את `setup-windows.ps1` ואת `verify-windows.ps1` בתיקיית ההורדות, ועוברים אליה:
     ```powershell
     cd "$env:USERPROFILE\Downloads"
     ```
     אם מופיעה השגיאה "Cannot find path", תיקיית ההורדות הועברה למקום אחר (למשל לכונן `D:`). פותחים את תיקיית ההורדות בסייר הקבצים, מעתיקים את הנתיב משורת הכתובת, ומריצים `cd "<הנתיב>"`.
   - **אחרי שהפרויקט הורד:** הסקריפטים נמצאים בתיקייה `scripts` שבפרויקט.
3. מריצים (אם הסקריפט בתיקיית ההורדות, כותבים `.\setup-windows.ps1` במקום `.\scripts\setup-windows.ps1`):
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\setup-windows.ps1
   ```
   להתקנת הכלים הרשותיים (VS Code ו־SSMS) מוסיפים `-WithTools`.
   להתקנה מינימלית (רק Git, Docker ו־WSL) מוסיפים `-RuntimeOnly`.
4. **מפעילים מחדש את המחשב** אם הסקריפט ביקש זאת. בהתקנה ראשונה של WSL זה נדרש תמיד.
5. אחרי ההפעלה מחדש פותחים את **Docker Desktop** מתפריט התחל:
   - מאשרים את תנאי השימוש. אין צורך להתחבר לחשבון Docker, אפשר לבחור "Skip".
   - ממתינים שבפינה השמאלית התחתונה יופיע **Engine running** בירוק.
6. בודקים את ההתקנה בחלון PowerShell **חדש** (לא חייב כמנהל), מאותה תיקייה:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1
   ```
   כל השורות של רכיבי החובה צריכות להופיע עם `[OK]`.

---

## 3. התקנה ידנית (חלופה)

אם מעדיפים להתקין כל רכיב בנפרד, מריצים ב־PowerShell כמנהל:

```powershell
wsl --install --no-distribution
winget install --id Git.Git -e --source winget
winget install --id Docker.DockerDesktop -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
winget install --id OpenJS.NodeJS.LTS -e --source winget
```
אחר כך מפעילים מחדש את המחשב וממשיכים מצעד 5 בסעיף 2.

---

## 4. הורדת הפרויקט

```powershell
cd "$env:USERPROFILE\source"
git clone https://github.com/rikiz-coder/requests-management-system.git
cd requests-management-system
```

---

## 5. הרצת המערכת

כל הפקודות מורצות מתיקיית הפרויקט, כש־Docker Desktop פתוח ומציג Engine running.

1. **הרמת המערכת:**
   ```powershell
   docker compose up -d --build
   ```
   בפעם הראשונה זה לוקח כמה דקות: הורדת ה־Images, בניית השרת, הקמת בסיס הנתונים ויצירת 100,000 פניות. בהרצות הבאות זה מהיר, והנתונים נשמרים.
2. **בדיקה שהכול עלה:**
   ```powershell
   docker compose ps -a
   ```
   ‏`sqlserver` צריך להיות `healthy`, ‏`api` ו־`web` במצב `running`, ו־`seeder` במצב `exited (0)`. ‏`exited (0)` אומר שהוא סיים בהצלחה, וזה תקין: הוא רץ פעם אחת ונעצר.
3. **יצירה מחדש של נתוני הבדיקה** (מוחק הכול ויוצר מחדש, תמיד אותם נתונים):
   ```powershell
   docker compose run --rm seeder --reseed
   ```

### כתובות
| מה | כתובת |
|---|---|
| ממשק המשתמש (Angular) | http://localhost:8080 |
| תיעוד ה־API (Swagger) | http://localhost:5080/swagger |
| בדיקת תקינות של ה־API | http://localhost:5080/health (מחזיר `Healthy`) |
| SQL Server (ל־SSMS) | שרת: `localhost,14333` · משתמש: `sa` · סיסמה: `Requests_Dev_Passw0rd!` |

> SQL Server ממופה לפורט 14333 ולא לפורט הרגיל 1433, כדי לא להתנגש ב־SQL Server מקומי אם מותקן אחד. הסיסמה מיועדת לפיתוח מקומי בלבד.

### עצירה וניקוי
```powershell
docker compose down        # עוצר, הנתונים נשמרים
docker compose down -v     # עוצר ומוחק גם את הנתונים
```

---

## 6. הרצת הבדיקות האוטומטיות

הבדיקות דורשות ‎.NET 10 SDK, ו־Docker פתוח: בדיקות ה־Integration מרימות SQL Server זמני משלהן.

```powershell
cd backend
dotnet test
```

בדיקות ה־Angular (דורש Node.js):
```powershell
cd frontend\requests-web
npm ci
npx ng test --watch=false
```

---

## 7. הרצה במצב פיתוח (רשות, השרת מחוץ ל־Docker)

```powershell
# SQL Server ונתוני הבדיקה ב-Docker
docker compose up -d sqlserver seeder

# ה-API (בחלון נוסף) – http://localhost:5080/swagger
cd backend
dotnet run --project src\RequestsManagement.Api

# הממשק (בחלון נוסף) – http://localhost:4200, עם Proxy ל-API
cd frontend\requests-web
npm ci
npm start
```

---

## 8. פתרון תקלות

| תקלה | פתרון |
|---|---|
| `winget` לא מזוהה | מתקינים את **App Installer** מ־Microsoft Store, וסוגרים ופותחים מחדש את PowerShell |
| winget: "Failed when searching source: msstore" / "0x8a15005e: The server certificate did not match" | מקור ה־Microsoft Store נחסם ברשת. הסקריפט כבר משתמש ב־`--source winget`. אם גם ההורדות נכשלות בשגיאת תעודה, ייתכן שסינון אינטרנט או אנטי־וירוס בודק את התעבורה המוצפנת. במקרה כזה צריך לאשר אצלם את האתרים, או להוריד את תוכנות ההתקנה ידנית מאתרי היצרנים |
| WARNING: Virtualization is disabled | אחרי הפעלה מחדש בודקים במנהל המשימות ← ביצועים ← CPU. אם כתוב "וירטואליזציה: מופעל", אין בעיה. אם לא, מפעילים אותה ב־BIOS |
| "running scripts is disabled on this system" | מריצים עם `powershell -ExecutionPolicy Bypass -File ...` כמו בהוראות |
| Docker Desktop מציג "WSL needs updating" | מריצים `wsl --update` ב־PowerShell כמנהל ומפעילים את Docker מחדש |
| Docker Desktop מציג "Virtualization support not detected" | מפעילים וירטואליזציה ב־BIOS (Intel VT-x / AMD-V, לפעמים בשם SVM) |
| `docker` / `dotnet` / `node` לא מזוהים אחרי ההתקנה (או `[MISSING]` בסקריפט הבדיקה) | החלון נפתח לפני ההתקנה ולא מכיר את התוכנות החדשות. סוגרים את **כל** חלונות PowerShell ופותחים חלון חדש. אם זה לא עוזר, מתנתקים ומתחברים מחדש ל־Windows |
| "permission denied" / "docker-users" | מתנתקים ומתחברים מחדש ל־Windows. הסקריפט הוסיף אותך לקבוצה, וזה נכנס לתוקף רק אחרי התחברות מחדש |
| "port is already allocated" | פורט תפוס על ידי תוכנה אחרת. יוצרים ליד `docker-compose.yml` קובץ `.env` עם `SQL_PORT=14334` או `API_PORT=5081`, ומריצים שוב `docker compose up -d` |
| ה־API לא עולה, או `seeder` לא במצב `exited (0)` | `docker compose logs seeder` מציג מה קרה. בדרך כלל SQL Server עוד לא סיים לעלות: ממתינים חצי דקה ומריצים שוב `docker compose up -d` |
| בניית `web` נכשלת עם "unable to get local issuer certificate" | סינון אינטרנט או אנטי־וירוס בודק את התעבורה המוצפנת, והקונטיינר לא מכיר את התעודה שלו. הבנייה כבר לא מורידה גופנים מהאינטרנט (הדפדפן טוען אותם בזמן הצפייה). אם `npm ci` נכשל באותה שגיאה, צריך לאשר את `registry.npmjs.org` אצל ספק הסינון |
| SQL Server נופל מיד | לא הוקצה מספיק זיכרון. ב־Docker Desktop: Settings ← Resources, מוודאים 4GB לפחות |
| הצגת לוגים של שירות | `docker compose logs -f api` (או `sqlserver` / `web`) |
