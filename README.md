# Шкільний розклад — ASP.NET Core MVC (.NET 11)

Source: provided `index (1).html` (version 13). No Google Apps Script.

## Project structure
- `Views/Home/Index.cshtml` — HTML/Razor frontend
- `Views/Shared/_Layout.cshtml` — page layout
- `wwwroot/css/site.css` — extracted styles
- `wwwroot/js/site.js` — client class/day selection, search, fetch, UI, local cache
- `Controllers/HomeController.cs` — MVC homepage
- `Controllers/ScheduleController.cs` — `GET /api/schedule`, `?refresh=true` to force fetching
- `Services/ScheduleService.cs` — downloads the live Google spreadsheet as XLSX and caches 60 minutes
- `Services/XlsxReader.cs` — parses XLSX and actual `mergeCell` ranges, no third-party libraries
- `Services/ScheduleParser.cs` — converts the worksheet into class/day/lesson JSON
- `appsettings.json` — source spreadsheet ID and preferred sheet gid

## Start
1. Install the **.NET 11 SDK** (or update `TargetFramework` to a supported installed SDK, such as `net10.0`).
2. Run `dotnet restore` and `dotnet run` inside this project folder.
3. Open the URL printed by ASP.NET Core, typically `http://localhost:5000` (depends on machine).
4. Open `/api/schedule` to inspect backend JSON independently of the frontend.

## Notes
- This uses Google Sheets XLSX export `https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx`. Google must permit exporting this read-only document for the server. If Google blocks export, the API reports an error; no one can reconstruct arbitrary merged-cell coordinates from plain gviz values alone.
- Merged cells are read from each downloaded XLSX and automatically updated when the school changes them.
- The code selects a worksheet by matching `gid` to OOXML `sheetId` if available; otherwise it uses the first worksheet. If worksheet IDs do not match, change selection logic using workbook sheet names.
- The original frontend filtered out empty subject strings. This version retains that behavior; if a lesson is genuinely blank it is not displayed.
- Backend runs wherever you host ASP.NET Core; you do not need to leave your computer on if you publish to a hosting provider.
- When hosted publicly, configure HTTPS and restrict diagnostic/error disclosure as appropriate.

**Verification:** Source extraction and structural consistency were checked, but the .NET runtime is not installed in the packaging environment, so compiling/running the C# project here was not possible.
