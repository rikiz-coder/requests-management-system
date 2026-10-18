# מדריך התקנה והרצה – מערכת לניהול פניות

המדריך מסביר איך להתקין את כל מה שצריך במחשב Windows, להוריד את הפרויקט, להריץ אותו ולבדוק אותו.

> **הערה:** סעיפים 4–7 (הרצה, כתובות ובדיקות) מתארים את המבנה המתוכנן. כתובת ה־repo והפורטים הסופיים יעודכנו כשהקוד יעלה ל־GitHub.

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

1. **הרמת המערכת** (DB, API וממשק). בפעם הראשונה זה לוקח כמה דקות, בגלל הורדת ה־Images:
   ```powershell
   docker compose up -d --build
   ```
2. **יצירת נתוני הבדיקה** (100,000 פניות). אפשר להריץ שוב בכל עת: הפקודה מוחקת את הנתונים ומייצרת אותם מחדש, ותמיד אותם נתונים:
   ```powershell
   docker compose run --rm seeder
   ```
3. **בדיקה שהכול עלה:**
   ```powershell
   docker compose ps
   ```
   כל השירותים צריכים להיות במצב `running` או `healthy`.

### כתובות
| מה | כתובת |
|---|---|
| ממשק המשתמש (Angular) | http://localhost:8080 |
| תיעוד ה־API (Swagger) | http://localhost:5080/swagger |
| SQL Server (ל־SSMS) | שרת: `localhost,14333` · משתמש: `sa` · סיסמה: מופיעה בקובץ `.env` |

> SQL Server ממופה לפורט 14333 ולא לפורט הרגיל 1433, כדי לא להתנגש ב־SQL Server מקומי אם מותקן אחד.

### עצירה וניקוי
```powershell
docker compose down        # עוצר, הנתונים נשמרים
docker compose down -v     # עוצר ומוחק גם את הנתונים
```

---

## 6. הרצת הבדיקות האוטומטיות

הבדיקות דורשות .NET 10 SDK, ו־Docker פתוח: בדיקות ה־Integration מרימות SQL Server זמני משלהן.

```powershell
cd backend
dotnet test
```

בדיקות ה־Angular (אם קיימות):
```powershell
cd frontend\requests-web
npm ci
npm test
```

---

## 7. הרצה במצב פיתוח (רשות, ללא Docker ל־API ולממשק)

```powershell
# DB בלבד ב-Docker
docker compose up -d sqlserver

# API
cd backend
dotnet run --project src\RequestsManagement.Api

# ממשק (בחלון נוסף)
cd frontend\requests-web
npm ci
npm start      # http://localhost:4200
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
| `docker` לא מזוהה אחרי ההתקנה | פותחים חלון PowerShell חדש, או מתנתקים ומתחברים מחדש ל־Windows |
| "permission denied" / "docker-users" | מתנתקים ומתחברים מחדש ל־Windows. הסקריפט הוסיף אותך לקבוצה, וזה נכנס לתוקף רק אחרי התחברות מחדש |
| "port is already allocated" | פורט תפוס על ידי תוכנה אחרת. משנים את הפורט בקובץ `.env` ומריצים שוב `docker compose up -d` |
| ה־API לא עולה ובלוג מופיע "Login failed" | SQL Server עוד לא סיים לעלות. ממתינים כחצי דקה ומריצים `docker compose restart api` |
| SQL Server נופל מיד | לא הוקצה מספיק זיכרון. ב־Docker Desktop: Settings ← Resources, מוודאים 4GB לפחות |
| הצגת לוגים של שירות | `docker compose logs -f api` (או `sqlserver` / `web`) |
