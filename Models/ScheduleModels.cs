namespace SchoolScheduleMvc.Models;
public record ClassInfo(string Id, string Name, int Col);
public record Lesson(int Num, string Time, Dictionary<string,string> Subjects);
public class ScheduleResponse
{
    public List<ClassInfo> Classes {get;set;} = [];
    public Dictionary<int,List<Lesson>> Days {get;set;} = [];
    public Dictionary<int,Dictionary<string,string>> Extras {get;set;} = [];
    public DateTimeOffset Updated {get;set;} = DateTimeOffset.UtcNow;
    public string Source {get;set;} = "Google XLSX";
}
