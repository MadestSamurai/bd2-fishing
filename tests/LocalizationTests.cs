using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BD2Fishing.Localization;
internal static class LocalizationTests
{
 public static int Run()
 {
  int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception("Localization: "+message);}
  var zh=LanguageCatalog.Read("zh-CN");var en=LanguageCatalog.Read("en-US");
  Check(zh.Keys.Order().SequenceEqual(en.Keys.Order()),"catalog keys agree");
  var chinese=new LanguageCatalog("zh-CN");var english=new LanguageCatalog("en-US");
  foreach(var entry in zh){Check(entry.Key==entry.Value,"source message: "+entry.Key);Check(chinese.Text(entry.Key)==entry.Value,"Chinese exact message");Check(!string.IsNullOrWhiteSpace(en[entry.Key]),"English value present");Check(english.Text(entry.Key)==en[entry.Key],"English exact message");}
  Check(english.Text("FishingBiteStart error=3")=="FishingBiteStart error=3","protocol identifiers unchanged");
  var culture=CultureInfo.CurrentUICulture;
  try{CultureInfo.CurrentUICulture=new("zh-TW");Check(LanguageCatalog.Normalize(null)=="zh-CN","Chinese system defaults to Chinese");CultureInfo.CurrentUICulture=new("de-DE");Check(LanguageCatalog.Normalize("invalid")=="en-US","other systems and invalid preferences default to English");Check(LanguageCatalog.Normalize("zh-CN")=="zh-CN","explicit preference wins");}
  finally{CultureInfo.CurrentUICulture=culture;}
  var data=Path.Combine(AppContext.BaseDirectory,"test-data","locale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
  File.WriteAllText(Path.Combine(data,"settings.json"),"{\"IntervalMilliseconds\":250}");
  LanguagePreference.Save(data,"en-US");Check(LanguagePreference.Read(data)=="en-US","English persists");LanguagePreference.Save(data,"zh-CN");Check(LanguagePreference.Read(data)=="zh-CN","Chinese persists");Check(File.ReadAllText(Path.Combine(data,"settings.json"))=="{\"IntervalMilliseconds\":250}","language switch never rewrites operation settings");
  File.WriteAllText(Path.Combine(data,"language.json"),"broken");Check(LanguagePreference.Read(data)==LanguageCatalog.Normalize(null),"corrupt preference falls back");
  var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"Directory.Build.props")))root=root.Parent;
  Check(root!=null,"source root located");
  foreach(var dir in new[]{"desktop","core","shared","hook","compatibility"})foreach(var file in Directory.GetFiles(Path.Combine(root!.FullName,dir),"*.cs"))
  {
   if(file.EndsWith("Smoke.cs"))continue;
   var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file));
   foreach(var node in tree.GetRoot().DescendantNodes())
   {
    if(node.Ancestors().OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.Text=="SmokeAsync"))continue;
    string source=node is LiteralExpressionSyntax lit&&lit.IsKind(SyntaxKind.StringLiteralExpression)?lit.Token.ValueText:node is InterpolatedStringTextSyntax part?part.TextToken.ValueText:"";
    if(Regex.IsMatch(source,@"[\p{IsCJKUnifiedIdeographs}]"))Check(en.ContainsKey(source),"uncatalogued message in "+file+": "+source);
   }
  }
  foreach(var file in Directory.GetFiles(Path.Combine(root!.FullName,"desktop"),"*.xaml"))foreach(var attr in XDocument.Load(file).Descendants().Attributes())if(Regex.IsMatch(attr.Value,@"[\p{IsCJKUnifiedIdeographs}]"))Check(en.ContainsKey(attr.Value),"uncatalogued XAML: "+attr.Value);
  return count;
 }
}
