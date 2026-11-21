using Microsoft.Extensions.Caching.Memory;
using SchoolScheduleMvc.Models;
namespace SchoolScheduleMvc.Services;
public class ScheduleService(HttpClient client, IMemoryCache cache, IConfiguration settings, ILogger<ScheduleService> logger)
{
    private readonly SemaphoreSlim _lock = new(1,1);
    const string Key="schedule-v1";
    public async Task<ScheduleResponse> GetAsync(bool refresh,CancellationToken ct)
    {
        if(!refresh && cache.TryGetValue(Key,out ScheduleResponse? cached) && cached is not null)return cached;
        await _lock.WaitAsync(ct);
        try
        {
            if(!refresh && cache.TryGetValue(Key,out cached) && cached is not null)return cached;
            string id=settings["GoogleSheets:SpreadsheetId"]??throw new InvalidDataException("Не задано SpreadsheetId");
            string? gid=settings["GoogleSheets:Gid"];
            // Export current document, including actual mergeCells. Requires Google to allow XLSX export.
            var url=$"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(id)}/export?format=xlsx";
            using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
            response.EnsureSuccessStatusCode();
            byte[] bytes=await response.Content.ReadAsByteArrayAsync(ct);
            if(bytes.Length<4||bytes[0]!=0x50||bytes[1]!=0x4b)
                throw new InvalidDataException("Google повернув не XLSX. Перевірте, чи дозволено скачування таблиці для читачів.");
            var worksheet=XlsxReader.Read(bytes,gid);
            var schedule=ScheduleParser.Parse(worksheet);
            schedule.Updated=DateTimeOffset.UtcNow;
            int minutes=Math.Clamp(settings.GetValue<int?>("GoogleSheets:CacheMinutes")??60,1,1440);
            cache.Set(Key,schedule,TimeSpan.FromMinutes(minutes));
            logger.LogInformation("Schedule refreshed: {Classes} classes, {Merges} merges",schedule.Classes.Count,worksheet.Merges.Count);
            return schedule;
        }
        finally{_lock.Release();}
    }
}
