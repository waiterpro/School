using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SchoolScheduleMvc.Services;

public record CellMerge(int R1, int C1, int R2, int C2);
public record WorksheetData(List<List<string>> Rows, List<CellMerge> Merges);

// No extra NuGet packages: reads real .xlsx mergeCell boundaries from OpenXML.
public static class XlsxReader
{
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace RelDoc = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace RelPkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static WorksheetData Read(byte[] content, string? gid)
    {
        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var workbook = Load(archive, "xl/workbook.xml");
        var relationships = Load(archive,"xl/_rels/workbook.xml.rels");
        var sheetElements = workbook.Descendants(Main + "sheet").ToList();
        if (sheetElements.Count == 0) throw new InvalidDataException("XLSX не містить аркушів");
        // Google gid normally equals worksheet sheetId. Select it if present.
        var sheet = sheetElements.FirstOrDefault(x => (string?)x.Attribute("sheetId") == gid)
            ?? sheetElements.First();
        string? relId = (string?)sheet.Attribute(RelDoc + "id");
        var relation = relationships.Descendants(RelPkg + "Relationship")
            .FirstOrDefault(r => (string?)r.Attribute("Id") == relId);
        var target = (string?)relation?.Attribute("Target")
            ?? throw new InvalidDataException("Не знайдено аркуш у XLSX");
        string path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        // Relative paths can contain ../ ; canonicalize within ZIP directory.
        var normalized = new List<string>();
        foreach (var part in path.Replace('\\','/').Split('/'))
        {
            if (part == "..") { if(normalized.Count>0)normalized.RemoveAt(normalized.Count-1); }
            else if (part != "." && part.Length>0) normalized.Add(part);
        }
        var sheetXml = Load(archive, string.Join('/',normalized));
        List<string> strings = [];
        var shared = archive.GetEntry("xl/sharedStrings.xml");
        if(shared is not null)
        {
            using var sr = shared.Open();
            var x = XDocument.Load(sr);
            strings=x.Descendants(Main+"si").Select(si=>string.Concat(si.Descendants(Main+"t").Select(t=>t.Value))).ToList();
        }
        var cells = new Dictionary<(int,int),string>();
        int maxR=0,maxC=0;
        foreach(var cell in sheetXml.Descendants(Main+"sheetData").Descendants(Main+"c"))
        {
            var addr=(string?)cell.Attribute("r");
            if(addr is null)continue;
            var (r,c)=Coords(addr);
            if(r<0||c<0||r>20000||c>2000)continue;
            var type=(string?)cell.Attribute("t");
            var value=(string?)cell.Element(Main+"v") ?? string.Empty;
            if(type=="s" && int.TryParse(value,out var ix) && ix>=0 && ix<strings.Count)value=strings[ix];
            else if(type=="inlineStr") value=string.Concat(cell.Descendants(Main+"t").Select(t=>t.Value));
            else if(type=="str") value=(string?)cell.Element(Main+"v")??"";
            else if(type=="b")value=value=="1"?"TRUE":"FALSE";
            cells[(r,c)]=value.Trim();maxR=Math.Max(maxR,r);maxC=Math.Max(maxC,c);
        }
        var merges=new List<CellMerge>();
        foreach(var merge in sheetXml.Descendants(Main+"mergeCell"))
        {
            var reference=(string?)merge.Attribute("ref");
            if(reference is null)continue;
            var parts=reference.Split(':');
            var (r1,c1)=Coords(parts[0]);var (r2,c2)=Coords(parts[^1]);
            if(r1>=0 && c1>=0 && r2>=r1 && c2>=c1 && r2-r1<200 && c2-c1<200)
            {merges.Add(new(r1,c1,r2,c2));maxR=Math.Max(maxR,r2);maxC=Math.Max(maxC,c2);}
        }
        if(maxR>2000||maxC>300)throw new InvalidDataException("Занадто великий аркуш");
        var rows=Enumerable.Range(0,maxR+1).Select(_=>Enumerable.Repeat("",maxC+1).ToList()).ToList();
        foreach(var (key,val) in cells) rows[key.Item1][key.Item2]=val;
        foreach(var m in merges)
        {
            var contentValue=rows[m.R1][m.C1];
            if(string.IsNullOrWhiteSpace(contentValue)) continue;
            for(int r=m.R1;r<=m.R2;r++)
                for(int c=m.C1;c<=m.C2;c++)
                    if(string.IsNullOrWhiteSpace(rows[r][c]))rows[r][c]=contentValue;
        }
        return new WorksheetData(rows,merges);
    }
    private static XDocument Load(ZipArchive zip,string path)
    {
        var entry=zip.GetEntry(path)??throw new InvalidDataException("Відсутній файл XLSX: "+path);
        using var stream=entry.Open();return XDocument.Load(stream);
    }
    private static (int,int) Coords(string reference)
    {
        var m=Regex.Match(reference,@"^\$?([A-Z]+)\$?(\d+)$",RegexOptions.IgnoreCase);
        if(!m.Success)return(-1,-1);
        int col=0;foreach(char ch in m.Groups[1].Value.ToUpperInvariant())col=col*26+ch-'A'+1;
        return(int.Parse(m.Groups[2].Value)-1,col-1);
    }
}
