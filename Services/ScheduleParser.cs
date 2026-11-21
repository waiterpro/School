using System.Text.RegularExpressions;
using SchoolScheduleMvc.Models;
namespace SchoolScheduleMvc.Services;
public static class ScheduleParser
{
    static readonly Regex ClassName=new(@"^(?:[1-9]|10|11)\s*[-–]?[\sА-ЯІЇЄҐA-Zа-яіїєґa-z]*(?:\s|$)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    static readonly Regex Time=new(@"\b\d{1,2}\s*[:.]\s*\d{2}\b",RegexOptions.Compiled);
    static readonly Regex Number=new(@"^(?:№\s*)?(\d{1,2})(?:\s*[.№)]|\s*$)",RegexOptions.Compiled|RegexOptions.IgnoreCase);
    static string N(string? v)=>Regex.Replace(v??"",@"\s+"," ").Trim();
    static int Day(string v)
    {
        v=N(v).ToUpperInvariant().Replace("’","").Replace("'","");
        if(v.StartsWith("ПОНЕД"))return 0;
        if(v.StartsWith("ВІВТ")||v.StartsWith("ВТОР"))return 1;
        if(v.StartsWith("СЕРЕД"))return 2;
        if(v.StartsWith("ЧЕТВ"))return 3;
        if(v.StartsWith("ПЯТ"))return 4;
        return -1;
    }
    public static ScheduleResponse Parse(WorksheetData sheet)
    {
        var rows=sheet.Rows;
        List<ClassInfo> classes=[];int header=-1;
        for(int r=0;r<Math.Min(35,rows.Count);r++)
        {
            List<ClassInfo> candidates=[];
            for(int c=0;c<rows[r].Count;c++)
            {
                var name=N(rows[r][c]);
                if(name.Length<1||name.Length>=75||Regex.IsMatch(name,@"додаткові|примітк|номер|урок|годин|час|перерва",RegexOptions.IgnoreCase))continue;
                if(ClassName.IsMatch(name)&&!Regex.IsMatch(name,@"^[0-9]{1,2}\s*[-–]\s*[0-9]{1,2}$"))
                    candidates.Add(new ClassInfo(c.ToString(),name,c));
            }
            if(candidates.Count>classes.Count){classes=candidates;header=r;}
        }
        if(classes.Count==0)throw new InvalidDataException("Не знайдено заголовків класів");
        var result=new ScheduleResponse();result.Classes=classes;
        for(int i=0;i<5;i++)result.Days[i]=[];
        int current=-1,last=0;var prefixWidth=Math.Max(3,classes.Min(c=>c.Col));
        for(int r=0;r<rows.Count;r++)
        {
            var row=rows[r];var prefix=row.Take(prefixWidth).ToList();
            var nextDay=prefix.Select(Day).FirstOrDefault(d=>d>=0,-1);
            if(nextDay>=0){current=nextDay;last=0;}
            if(current<0||r==header)continue;
            if(prefix.Any(v=>v.Contains("ГПД",StringComparison.OrdinalIgnoreCase)))
            {
                if(!result.Extras.ContainsKey(current)) result.Extras[current]=[];
                foreach(var cl in classes)
                {
                    var subject=cl.Col<row.Count?N(row[cl.Col]):"";
                    if(subject.Length>0)result.Extras[current][cl.Id]=subject;
                }
                continue;
            }
            int? number=null;
            foreach(var prefixValue in prefix)
            {
                var match=Number.Match(prefixValue);
                if(match.Success && int.TryParse(match.Groups[1].Value,out int num) && num is >=1 and <=12)
                {number=num;break;}
            }
            var time=prefix.FirstOrDefault(x=>Time.IsMatch(x))??"";
            if(number==null && string.IsNullOrEmpty(time))continue;
            if(number==null)number=last+1;
            if(number<1||number>12)continue;
            var subjects=classes.ToDictionary(c=>c.Id,c=>c.Col<row.Count?N(row[c.Col]):"");
            if(subjects.Values.All(string.IsNullOrWhiteSpace)&&string.IsNullOrEmpty(time))continue;
            var old=result.Days[current].FirstOrDefault(x=>x.Num==number);
            if(old is null)result.Days[current].Add(new Lesson(number.Value,time.Length>0?time:"—",subjects));
            else foreach(var (id,subject) in subjects)
                if(subject.Length>0 && string.IsNullOrWhiteSpace(old.Subjects[id]))old.Subjects[id]=subject;
            last=number.Value;
        }
        foreach(var day in result.Days.Values)day.Sort((a,b)=>a.Num.CompareTo(b.Num));
        if(result.Days.Values.All(x=>x.Count==0))throw new InvalidDataException("Не вдалося визначити уроки");
        return result;
    }
}
