using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BD2Fishing.Compatibility;

// Only interface shapes and one-way hashes leave the installed client. Never save game IL.
public sealed class MetadataIndex : IDisposable
{
    public ModuleDefinition Module { get; }
    public TypeDefinition[] Types { get; }
    private readonly Dictionary<string,string> bodies = new();
    private Dictionary<string,List<string>>? uses;
    public MetadataIndex(string path)
    {
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
        Module=ModuleDefinition.ReadModule(path,new ReaderParameters{AssemblyResolver=resolver});
        Types=Walk(Module.Types).ToArray();
    }
    public static IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> types) => types.SelectMany(t=>new[]{t}.Concat(Walk(t.NestedTypes)));
    public static bool Obfuscated(string name)=>name.Any(c=>c>='\u0370' && c<='\u1fff');
    public static string Name(string name)=>Regex.Replace(name,@"[\u0370-\u1fff]+","?");
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string TypeShape(TypeReference type)
    {
        if(type is GenericInstanceType g)return TypeShape(g.ElementType)+"<"+string.Join(",",g.GenericArguments.Select(TypeShape))+">";
        if(type is TypeSpecification s)return s.GetType().Name+"("+TypeShape(s.ElementType)+")";
        return Name(type.FullName);
    }
    public static string Signature(IMemberDefinition m)=>m switch
    {
        MethodDefinition f=>"M|"+f.IsStatic+"|"+f.GenericParameters.Count+"|"+TypeShape(f.ReturnType)+"|"+string.Join(",",f.Parameters.Select(p=>TypeShape(p.ParameterType))),
        FieldDefinition f=>"F|"+f.IsStatic+"|"+TypeShape(f.FieldType),
        PropertyDefinition p=>"P|"+(p.GetMethod??p.SetMethod)?.IsStatic+"|"+TypeShape(p.PropertyType)+"|"+string.Join(",",p.Parameters.Select(x=>TypeShape(x.ParameterType))),
        _=>throw new ArgumentException("Unsupported member")
    };
    public static IEnumerable<IMemberDefinition> Members(TypeDefinition t)=>t.Fields.Cast<IMemberDefinition>().Concat(t.Properties).Concat(t.Methods);
    public string Shape(TypeDefinition t)=>Hash(TypeShape(t.BaseType??Module.TypeSystem.Object)+"|"+t.IsEnum+"|"+string.Join(";",Members(t).Select(m=>Name(m.Name)+":"+Signature(m)).OrderBy(x=>x,StringComparer.Ordinal)));
    public string Body(MethodDefinition? m)
    {
        if(m==null || !m.HasBody)return "";
        if(bodies.TryGetValue(m.FullName,out var known))return known;
        var indices=m.Body.Instructions.Select((x,i)=>(x,i)).ToDictionary(x=>x.x,x=>x.i);
        string Operand(object? value)=>value switch
        {
            null=>"", MethodReference r=>TypeShape(r.DeclaringType)+"::"+Name(r.Name)+"("+string.Join(",",r.Parameters.Select(p=>TypeShape(p.ParameterType)))+")->"+TypeShape(r.ReturnType),
            FieldReference f=>TypeShape(f.DeclaringType)+"::"+Name(f.Name)+":"+TypeShape(f.FieldType),
            TypeReference t=>TypeShape(t), Instruction i=>"@"+indices[i],Instruction[] ii=>string.Join(",",ii.Select(i=>indices[i])),
            VariableDefinition v=>"local"+v.Index+":"+TypeShape(v.VariableType),ParameterDefinition p=>"arg"+p.Index,
            string s=>Name(s),_=>Convert.ToString(value,CultureInfo.InvariantCulture)??""
        };
        var text=Signature(m)+"|"+string.Join(";",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(i.Operand)));
        return bodies[m.FullName]=Hash(text);
    }
    public string MemberBody(IMemberDefinition m)=>m switch {MethodDefinition f=>Body(f),PropertyDefinition p=>Hash(Body(p.GetMethod)+"|"+Body(p.SetMethod)),_=>""};
    private void IndexUses()
    {
        if(uses!=null)return;uses=new();
        foreach(var m in Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody))
        {
            string? hash=null;
            for(int i=0;i<m.Body.Instructions.Count;i++)
            {
                if(m.Body.Instructions[i].Operand is not MemberReference member)continue;
                if(member is not MethodReference && member is not FieldReference)continue;
                hash??=Body(m);
                if(!uses.TryGetValue(member.FullName,out var list))uses[member.FullName]=list=new();
                // Include operand position to distinguish same-typed fields in the same method.
                list.Add(hash+":"+i);
            }
        }
    }
    public string[] Uses(IMemberDefinition m)
    {
        IndexUses();
        var names=m is PropertyDefinition p?new[]{p.GetMethod?.FullName,p.SetMethod?.FullName}:new[]{m.FullName};
        return names.Where(n=>n!=null).SelectMany(n=>uses!.TryGetValue(n!,out var list)?list:Enumerable.Empty<string>()).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    }
    public TypeDefinition Find(string name)=>Types.Single(t=>t.FullName==name || t.FullName.Replace('/','+')==name);
    public void Dispose()=>Module.Dispose();
}

public sealed record MemberContract(string Name,string Signature,string Body,string[] Uses);
public sealed record TypeContract(string Name,string Shape,string[] Anchors,MemberContract[] Members);
public sealed record ApiContract(string Role,string Type,string Member,int Arity=-1,string Signature="");
public sealed record BindingContract(int Schema,TypeContract[] Types,ApiContract[] Apis,Dictionary<string,string> Roles);
public sealed record BoundMember(string Type,string Original,string Actual,string Kind,int Token);
public sealed record BindingReport(string Status,string ClientMvid,int Types,int Members,int RenamedTypes,int RenamedMembers,string[] Errors);
public sealed record ResolvedBindings(BindingContract Contract,Dictionary<string,TypeDefinition> Types,Dictionary<string,IMemberDefinition> Members,BindingReport Report);
