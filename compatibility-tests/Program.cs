using BD2Fishing.Compatibility;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.Json;

// Synthetic assemblies contain only this test's code. No installed game or fixture DLL required.
var root=Path.Combine(AppContext.BaseDirectory,"test-data",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
int checks=0;void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
string Fixture(string name,bool renamed=false,bool extra=false,bool broken=false,bool ambiguous=false,bool reordered=false)
{
    using var module=ModuleDefinition.CreateModule("FishingFixture",ModuleKind.Dll);
    var type=new TypeDefinition("",renamed?"ὠὠὣ":"ὡὡὢ",TypeAttributes.Public|TypeAttributes.Class,module.TypeSystem.Object);module.Types.Add(type);
    var fieldA=new FieldDefinition(renamed?"ὣὣὣ":"ὤὤὤ",FieldAttributes.Public,module.TypeSystem.Int32);
    var fieldB=new FieldDefinition(renamed?"ὥὥὥ":"ὦὦὦ",FieldAttributes.Public,module.TypeSystem.Int32);
    type.Fields.Add(reordered?fieldB:fieldA);type.Fields.Add(reordered?fieldA:fieldB);
    MethodDefinition Method(string methodName,FieldDefinition field,int constant)
    {
        var method=new MethodDefinition(methodName,MethodAttributes.Public,module.TypeSystem.Int32);type.Methods.Add(method);
        var il=method.Body.GetILProcessor();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,field);il.Emit(OpCodes.Ldc_I4,constant);il.Emit(OpCodes.Add);il.Emit(OpCodes.Ret);return method;
    }
    Method("GetFirst",fieldA,11);Method("GetSecond",fieldB,29);
    var action=Method(renamed?"ὧὧὧ":"ὨὨὨ",fieldA,43);
    if(broken)action.ReturnType=module.TypeSystem.Int64;
    if(ambiguous)Method("ὩὩὩ",fieldA,43);
    // Longer independent anchors let a class acquire new unrelated members without losing identity.
    foreach(var method in type.Methods){var il=method.Body.GetILProcessor();for(int i=0;i<10;i++)il.InsertBefore(method.Body.Instructions[0],il.Create(OpCodes.Nop));}
    if(extra){var m=new MethodDefinition("NewUnrelatedFeature",MethodAttributes.Public,module.TypeSystem.Void);m.Body.GetILProcessor().Emit(OpCodes.Ret);type.Methods.Add(m);}
    if(reordered){var methods=type.Methods.Reverse().ToArray();type.Methods.Clear();foreach(var m in methods)type.Methods.Add(m);}
    var path=Path.Combine(root,name+".dll");module.Write(path);return path;
}
var baseline=Fixture("base");BindingContract contract;
using(var index=new MetadataIndex(baseline))
{
    var type=index.Types.Single(t=>MetadataIndex.Obfuscated(t.Name));
    var selected=MetadataIndex.Members(type).ToArray();
    contract=new(1,new[]{new TypeContract(type.FullName,index.Shape(type),type.Methods.Select(index.Body).ToArray(),selected.Select(m=>new MemberContract(m.Name,MetadataIndex.Signature(m),index.MemberBody(m),index.Uses(m))).ToArray())},Array.Empty<ApiContract>(),new());
}
foreach(var scenario in new[]{("same",false,false,false),("new-build",false,false,true),("renamed",true,false,false),("renamed-reordered",true,false,true),("extended",true,true,true)})
{
    using var index=new MetadataIndex(Fixture(scenario.Item1,scenario.Item2,scenario.Item3,reordered:scenario.Item4));
    var result=BindingResolver.Resolve(index,contract);Check(result.Report.Status=="compatible",scenario.Item1+": "+string.Join(";",result.Report.Errors));
    Check(result.Report.Members==5,scenario.Item1+" resolves entire feature");
    if(scenario.Item2)
    {
        Check(result.Report.RenamedTypes==1,scenario.Item1+" renamed type");Check(result.Report.RenamedMembers==3,scenario.Item1+" renamed field/method");
        var a=contract.Types[0].Members.Single(m=>m.Name=="ὤὤὤ");Check(result.Members[BindingResolver.Key(contract.Types[0].Name,a.Name,a.Signature)].Name=="ὣὣὣ","same-type fields distinguished by consumers, not declaration order");
    }
}
foreach(var scenario in new[]{("broken",true,false),("ambiguous",false,true)})
{
    using var index=new MetadataIndex(Fixture(scenario.Item1,renamed:true,broken:scenario.Item2,ambiguous:scenario.Item3));
    var result=BindingResolver.Resolve(index,contract);Check(result.Report.Status=="unsupported" && result.Report.Errors.Length>0,scenario.Item1+" must not guess or inject");
}
Check(!typeof(HookCompiler).Assembly.GetReferencedAssemblies().Any(a=>a.Name=="Assembly-CSharp" || (a.Name??"").StartsWith("UnityEngine")),"public executable has no linked game libraries");
Check(HookCompiler.Contract().Types.Length>=20,"public package embeds interface contract");
Check(HookCompiler.Resource("Hook.RuntimeEngine.cs").Length>1000,"public package embeds owned runtime source");
Console.WriteLine(JsonSerializer.Serialize(new{status="pass",assertions=checks,gameRequired=false,injection=false}));
