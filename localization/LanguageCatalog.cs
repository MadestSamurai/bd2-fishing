using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace BD2Fishing.Localization;

/// <summary>Source-message catalogs. Automation snapshots remain language-neutral evidence;
/// translation happens only when text is presented. Full messages win over legacy fragments.</summary>
public sealed class LanguageCatalog
{
    private static readonly Lazy<Dictionary<string,string>> English=new(()=>Read("en-US"));
    private static readonly Lazy<Regex> Fragments=new(()=>new Regex(string.Join("|",English.Value.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape)),RegexOptions.Compiled|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)));
    private readonly Dictionary<string,string> cache=new(StringComparer.Ordinal);
    public string Language {get; private set;}
    public LanguageCatalog(string? language=null)=>Language=Normalize(language);
    public static string Normalize(string? language)=>language is "zh-CN" or "en-US"?language:CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?"zh-CN":"en-US";
    public void Select(string language){Language=Normalize(language);cache.Clear();}
    public static Dictionary<string,string> Read(string language)
    {
        using var stream=typeof(LanguageCatalog).Assembly.GetManifestResourceStream("Locale."+language+".json")??throw new InvalidOperationException("Missing language catalog: "+language);
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidOperationException("Empty language catalog");
    }
    public string Text(string source)
    {
        if(Language=="zh-CN" || string.IsNullOrEmpty(source))return source;
        if(English.Value.TryGetValue(source,out var exact))return exact;
        if(cache.TryGetValue(source,out var translated))return translated;
        // Longest literal wins in one pass. Never recursively translate the result or change evidence files.
        translated=Fragments.Value.Replace(source,m=>English.Value[m.Value]);
        if(cache.Count>=512)cache.Clear();
        cache[source]=translated;return translated;
    }
}

/// <summary>UI language is saved separately so switching never changes a running command or its lease.</summary>
public static class LanguagePreference
{
    public static string Read(string root)
    {
        try{return LanguageCatalog.Normalize(JsonSerializer.Deserialize<Preference>(File.ReadAllText(Path.Combine(root,"language.json")))?.Language);}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){return LanguageCatalog.Normalize(null);}
    }
    public static void Save(string root,string language)
    {
        Directory.CreateDirectory(root);var path=Path.Combine(root,"language.json");var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(new Preference{Language=LanguageCatalog.Normalize(language)}));File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private sealed class Preference {public string? Language {get;set;}}
}
