using Mono.Cecil;

namespace BD2Fishing.Compatibility;

public static class BindingResolver
{
    public static ResolvedBindings Resolve(MetadataIndex index,BindingContract contract)
    {
        if(contract.Schema!=1)throw new InvalidDataException("Unsupported compatibility contract schema");
        var types=new Dictionary<string,TypeDefinition>();var members=new Dictionary<string,IMemberDefinition>();var errors=new List<string>();
        var shapes=new Dictionary<TypeDefinition,string>();
        foreach(var expected in contract.Types)
        {
            TypeDefinition[] candidates;
            if(!MetadataIndex.Obfuscated(expected.Name))candidates=index.Types.Where(t=>t.FullName==expected.Name).ToArray();
            else
            {
                candidates=index.Types.Where(t=>MetadataIndex.Obfuscated(t.FullName) && (shapes.TryGetValue(t,out var shape)?shape:shapes[t]=index.Shape(t))==expected.Shape).ToArray();
                if(candidates.Length==0)
                {
                    // A class may gain unrelated methods. Distinct unchanged method bodies anchor it.
                    var ranked=index.Types.Where(t=>MetadataIndex.Obfuscated(t.FullName)).Select(t=>(Type:t,Score:t.Methods.Select(index.Body).Distinct().Count(h=>h.Length>0 && expected.Anchors.Contains(h)))).Where(x=>x.Score>=2).OrderByDescending(x=>x.Score).ToArray();
                    if(ranked.Length>0)candidates=ranked.Where(x=>x.Score==ranked[0].Score).Select(x=>x.Type).ToArray();
                }
            }
            if(candidates.Length!=1){errors.Add($"Type {expected.Name}: {candidates.Length} compatible matches");continue;}
            var type=candidates[0];types.Add(expected.Name,type);
            foreach(var member in expected.Members)
            {
                var choices=MetadataIndex.Members(type).Where(m=>MetadataIndex.Signature(m)==member.Signature).ToArray();
                if(!MetadataIndex.Obfuscated(member.Name))choices=choices.Where(m=>m.Name==member.Name).ToArray();
                else if(choices.Length>1)
                {
                    var ranked=choices.Select(m=>new{Member=m,Score=(member.Body.Length>0 && index.MemberBody(m)==member.Body?10:0)+index.Uses(m).Intersect(member.Uses).Count()*100}).OrderByDescending(x=>x.Score).ToArray();
                    choices=ranked.Length==0 || ranked[0].Score==0?Array.Empty<IMemberDefinition>():ranked.Where(x=>x.Score==ranked[0].Score).Select(x=>x.Member).ToArray();
                }
                if(choices.Length!=1){errors.Add($"Member {expected.Name}.{member.Name}: {choices.Length} compatible matches; refusing to guess");continue;}
                members.Add(Key(expected.Name,member.Name,member.Signature),choices[0]);
            }
        }
        var report=new BindingReport(errors.Count==0?"compatible":"unsupported",index.Module.Mvid.ToString(),types.Count,members.Count,types.Count(x=>x.Key!=x.Value.FullName),members.Count(x=>x.Key.Split('|')[1]!=x.Value.Name),errors.ToArray());
        return new(contract,types,members,report);
    }
    public static string Key(string type,string name,string signature)=>type+"|"+name+"|"+signature;
    public static IMemberDefinition Api(ResolvedBindings resolved,ApiContract api)
    {
        var expected=resolved.Contract.Types.Single(t=>t.Name==api.Type).Members.Where(m=>m.Name==api.Member && (api.Signature.Length==0 || m.Signature==api.Signature)).ToArray();
        var members=expected.Select(m=>resolved.Members[Key(api.Type,m.Name,m.Signature)]).Where(m=>api.Arity<0 || m is MethodDefinition method && method.Parameters.Count==api.Arity).ToArray();
        return members.Single();
    }
}
