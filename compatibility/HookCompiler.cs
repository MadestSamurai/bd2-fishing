using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace BD2Fishing.Compatibility;

public sealed record PreparedHook(byte[] Payload,BindingReport Report);
public static class HookCompiler
{
    public static string ToolFingerprint => MetadataIndex.Hash(typeof(HookCompiler).Module.ModuleVersionId+"|"+string.Join("|",typeof(HookCompiler).Assembly.GetManifestResourceNames().OrderBy(n=>n,StringComparer.Ordinal).Select(n=>MetadataIndex.Hash(Convert.ToBase64String(Resource(n))))));
    public static byte[] Resource(string name)
    {using var s=typeof(HookCompiler).Assembly.GetManifestResourceStream(name)??throw new InvalidDataException("Missing embedded resource: "+name);using var b=new MemoryStream();s.CopyTo(b);return b.ToArray();}
    public static BindingContract Contract()=>JsonSerializer.Deserialize<BindingContract>(Resource("BD2Fishing.Contract.json"))!;
    public static PreparedHook Prepare(string managed,BindingContract? contract=null)
    {
        using var index=new MetadataIndex(Path.Combine(managed,"Assembly-CSharp.dll"));
        var resolved=BindingResolver.Resolve(index,contract??Contract());
        if(resolved.Report.Status!="compatible")throw new CompatibilityException(resolved.Report);
        ValidateEnums(resolved);
        ValidateDataProperties(resolved);
        ValidateUnlockApi(resolved);
        var assembly=typeof(HookCompiler).Assembly;
        var sources=assembly.GetManifestResourceNames().Where(n=>n.StartsWith("Hook.",StringComparison.Ordinal)).OrderBy(n=>n,StringComparer.Ordinal).Select(n=>CSharpSyntaxTree.ParseText(Encoding.UTF8.GetString(Resource(n)),path:n)).ToList();
        sources.Add(CSharpSyntaxTree.ParseText(GenerateSource(resolved),path:"FishingClient.g.cs"));
        var refs=new List<MetadataReference>();
        // Read metadata only. Do not execute or copy game assemblies into the application directory.
        foreach(var file in Directory.EnumerateFiles(managed,"*.dll").OrderBy(x=>x,StringComparer.Ordinal))
        {try{refs.Add(MetadataReference.CreateFromFile(file));}catch(BadImageFormatException){}}
        refs.Add(MetadataReference.CreateFromImage(Resource("BD2Fishing.Harmony.dll")));
        var compilation=CSharpCompilation.Create("BD2Fishing.Runtime7",sources,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release,platform:Platform.X64,deterministic:true));
        using var stream=new MemoryStream();
        var emit=compilation.Emit(stream,manifestResources:new[]{new ResourceDescription("BD2Fishing.Harmony.dll",()=>new MemoryStream(Resource("BD2Fishing.Harmony.dll")),true)});
        if(!emit.Success)throw new InvalidOperationException("当前客户端接口无法编译，尚未注入。\n"+string.Join("\n",emit.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).Take(30)));
        var payload=stream.ToArray();
        ValidateObservers(index,resolved);
        return new(payload,resolved.Report);
    }
    private static void ValidateEnums(ResolvedBindings r)
    {
        int Value(string role,string name)=>Convert.ToInt32(r.Types[r.Contract.Roles[role]].Fields.Single(f=>f.Name==name && f.HasConstant).Constant);
        if(Value("FishGrade","Legendary")!=4 || Value("FishGrade","Normal")!=1 || Value("FishGrade","Rare")!=2)
            throw new InvalidOperationException("鱼稀有度定义发生变化，保留策略需要更新；尚未注入。");
        if(Value("ItemType","FishingConsumable")<=0 || Value("ItemType","Fish")<=0)throw new InvalidOperationException("鱼和鱼饵类别无法确认。");
    }
    private static void ValidateDataProperties(ResolvedBindings r)
    {
        foreach(var check in new[]{("Tables.Default","RoomDuration"),("Tables.Bait","Id,BuffId"),("Tables.Buff","Id"),("Tables.Fish","Id,Grade,NameTextId"),("Tables.Shop","ShopItemId"),("Tables.ShopEntries","GroupId,ItemType,ItemId,PriceCount"),("Inventory.FishList","InvenIndex,Id,Size,IsLock"),("Player.Data","FishingFishInvenSlot")})
        {
            var member=BindingResolver.Api(r,r.Contract.Apis.Single(a=>a.Role==check.Item1));
            TypeReference result=member switch {MethodDefinition m=>m.ReturnType,PropertyDefinition p=>p.PropertyType,FieldDefinition f=>f.FieldType,_=>throw new InvalidOperationException("Unsupported data API")};
            if(check.Item1=="Tables.ShopEntries" || check.Item1=="Inventory.FishList")result=((GenericInstanceType)result).GenericArguments.Single();
            var type=result.Resolve();
            foreach(var name in check.Item2.Split(','))if(!type.Properties.Any(p=>p.Name==name && p.GetMethod!=null) && !type.Fields.Any(f=>f.Name==name))
                throw new InvalidOperationException(check.Item1+" 缺少数据属性 "+name+"，尚未注入。");
        }
    }
    public static void ValidateObservers(MetadataIndex index,ResolvedBindings r)
    {
        var helper=r.Types[r.Contract.Roles["Inventory"]];
        var methods=MetadataIndex.Walk(new[]{helper}).SelectMany(t=>t.Methods).Where(m=>m.ReturnType.FullName=="System.Boolean" && m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(new[]{"System.Byte[]","System.Int32","System.Int32"}) && m.HasBody).ToArray();
        foreach(var kind in new[]{"Casting","BiteStart","BiteFishStaminaUpdate","BiteEnd","ShopSell","BaitUse"})
        {
            string response="Proto.Net.Fishing"+kind+"Response";
            int count=methods.Count(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.FullName==response && call.Name=="get_Parser"));
            if(count!=1)throw new InvalidOperationException(response+" 原生回执入口不唯一，尚未注入："+count);
        }
    }
    private static void ValidateUnlockApi(ResolvedBindings r)
    {
        foreach(var item in new[]{("Inventory.Unlock","System.Void",new[]{"System.Int64","System.Boolean","System.Action"}),
            ("Inventory.UnlockReply","System.Boolean",new[]{"Proto.Net.FishingFishLockRequest","System.Byte[]","System.Int32","System.Int32","System.Action"})})
        {
            var method=(MethodDefinition)BindingResolver.Api(r,r.Contract.Apis.Single(a=>a.Role==item.Item1));
            if(!method.IsStatic || method.ReturnType.FullName!=item.Item2 || !method.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(item.Item3))
                throw new InvalidOperationException("解锁接口签名发生变化，尚未注入："+item.Item1);
        }
    }
    public static string GenerateSource(ResolvedBindings r)
    {
        static string Q(string s)=>JsonSerializer.Serialize(s);
        var types=r.Types.ToDictionary(x=>x.Key,x=>x.Value.FullName.Replace('/','+'));
        foreach(var role in r.Contract.Roles)types[role.Key]=r.Types[role.Value].FullName.Replace('/','+');
        var names=new Dictionary<string,string>();
        foreach(var type in r.Contract.Types)foreach(var member in type.Members)
        {
            var actual=r.Members[BindingResolver.Key(type.Name,member.Name,member.Signature)];
            var key=actual.DeclaringType.FullName.Replace('/','+')+"|"+member.Name;
            if(names.TryGetValue(key,out var previous) && previous!=actual.Name)throw new InvalidOperationException("Reflection overload mapping is ambiguous: "+key);
            names[key]=actual.Name;
        }
        var apiEntries=r.Contract.Apis.Select(api=>
        {
            var m=BindingResolver.Api(r,api);var method=m is MethodDefinition;
            return "{"+Q(api.Role)+",new[]{"+Q(m.DeclaringType.FullName.Replace('/','+'))+","+Q(method?m.MetadataToken.ToInt32().ToString():m.Name)+","+Q(method?"method":"member")+"}}";
        });
        string Dictionary(Dictionary<string,string> d)=>"new System.Collections.Generic.Dictionary<string,string>{"+string.Join(",",d.Select(x=>"{"+Q(x.Key)+","+Q(x.Value)+"}"))+"}";
        return "namespace BD2Fishing.Runtime { internal static class FishingClient { internal const string CompiledMvid="+Q(r.Report.ClientMvid)+"; internal static readonly System.Collections.Generic.Dictionary<string,string> TypeNames="+Dictionary(types)+"; internal static readonly System.Collections.Generic.Dictionary<string,string> MemberNames="+Dictionary(names)+"; internal static readonly System.Collections.Generic.Dictionary<string,string[]> Apis=new System.Collections.Generic.Dictionary<string,string[]>{"+string.Join(",",apiEntries)+"}; }}";
    }
}
public sealed class CompatibilityException : Exception
{
    public BindingReport Report {get;}
    public CompatibilityException(BindingReport report):base("当前客户端有无法确认的钓鱼接口，尚未注入。\n"+string.Join("\n",report.Errors.Take(12))){Report=report;}
}
