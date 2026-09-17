using System.Text.Json;
using BD2Fishing.Compatibility;

if(args.Length==0){Console.WriteLine("generate <Managed> <hook source> <contract.json> | check <Managed> <output> [contract.json]");return;}
var options=new JsonSerializerOptions{WriteIndented=true};
if(args[0]=="generate")
{
    using var index=new MetadataIndex(Path.Combine(args[1],"Assembly-CSharp.dll"));
    var contract=ContractGenerator.Generate(index,args[2]);
    File.WriteAllText(args[3],JsonSerializer.Serialize(contract,options));
    Console.WriteLine(JsonSerializer.Serialize(new{contract.Types.Length,Members=contract.Types.Sum(t=>t.Members.Length),Apis=contract.Apis.Length}));
}
else if(args[0]=="check")
{
    Directory.CreateDirectory(args[2]);
    try
    {
        var hook=HookCompiler.Prepare(args[1],args.Length>3?JsonSerializer.Deserialize<BindingContract>(File.ReadAllText(args[3])):null);
        File.WriteAllBytes(Path.Combine(args[2],"BD2Fishing.Runtime10.dll"),hook.Payload);
        File.WriteAllText(Path.Combine(args[2],"compatibility.json"),JsonSerializer.Serialize(hook.Report,options));
        Console.WriteLine(JsonSerializer.Serialize(hook.Report));
    }
    catch(CompatibilityException ex){File.WriteAllText(Path.Combine(args[2],"compatibility.json"),JsonSerializer.Serialize(ex.Report,options));throw;}
}
else if(args[0]=="describe")
{
    using var index=new MetadataIndex(Path.Combine(args[1],"Assembly-CSharp.dll"));
    foreach(var t in index.Types.Where(t=>t.FullName==args[2]))
    foreach(var member in MetadataIndex.Members(t))Console.WriteLine(member.FullName+(member is Mono.Cecil.FieldDefinition f && f.HasConstant?" = "+f.Constant:""));
}
else throw new ArgumentException("Unknown command");
