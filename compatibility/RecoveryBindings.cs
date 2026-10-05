using Mono.Cecil;
using Mono.Cecil.Cil;
namespace BD2Fishing.Compatibility;
public static class RecoveryBindings
{
    public static Dictionary<string, MethodDefinition> Resolve(ResolvedBindings r)
    {
        var inventory = r.Types[r.Contract.Roles["Inventory"]];
        var refresh = inventory.Methods.Single(m => m.IsStatic && m.ReturnType.FullName == "System.Void"
            && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Boolean" })
            && m.HasBody && m.Body.Instructions.Any(i => i.OpCode.Code == Code.Newobj
                && i.Operand is MethodReference call && call.DeclaringType.FullName == "Proto.Net.FishingItemInfoRequest"));
        var network = refresh.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>()
            .Where(m => m.ReturnType.FullName == "BDNetwork.NetworkManager" && m.Parameters.Count == 0)
            .Select(m => m.Resolve()).Distinct().Single();
        var manager=network.ReturnType.Resolve();
        bool Packet(TypeReference type){
            for(var next=type?.Resolve();next!=null;next=next.BaseType?.Resolve())
                if(next.Fields.Any(f=>!f.IsStatic&&f.FieldType.FullName=="Google.Protobuf.IMessage"))return true;
            return false;
        }
        var containers=manager.Fields.Where(f=>!f.IsStatic).Select(f=>f.FieldType).OfType<GenericInstanceType>().ToArray();
        if(containers.Count(t=>t.ElementType.FullName==typeof(Queue<>).FullName&&Packet(t.GenericArguments[0]))!=1
            ||containers.Count(t=>t.ElementType.FullName==typeof(Dictionary<,>).FullName&&t.GenericArguments[0].FullName=="System.String"&&Packet(t.GenericArguments[1]))!=1)
            throw new InvalidOperationException("Native network request containers are ambiguous; no injection.");
        return new() { ["Inventory.RefreshItems"] = refresh, ["Network.Instance"] = network };
    }
}
