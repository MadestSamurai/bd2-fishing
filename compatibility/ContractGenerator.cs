using Mono.Cecil;

namespace BD2Fishing.Compatibility;

// Maintainer-only bootstrap. Existing releases resolve contract.json; they never require these names.
public static class ContractGenerator
{
    public static BindingContract Generate(MetadataIndex index,string hookSource)
    {
        var selected=new HashSet<IMemberDefinition>();var selectedTypes=new HashSet<TypeDefinition>();
        var roles=new Dictionary<string,string>();var apis=new List<ApiContract>();
        IMemberDefinition[] Select(string typeName,string name,int arity=-1)
        {
            var t=index.Find(typeName);
            for(var next=t;next!=null;next=next.BaseType?.Resolve())
            {
                var matches=MetadataIndex.Members(next).Where(m=>m.Name==name && (arity<0 || m is MethodDefinition f && f.Parameters.Count==arity)).ToArray();
                if(matches.Length==0)continue;
                foreach(var m in matches){selected.Add(m);selectedTypes.Add(m.DeclaringType);}return matches;
            }
            throw new MissingMemberException(typeName,name);
        }
        void Api(string role,string type,string name,int arity=-1)
        {
            var choices=Select(type,name,arity);
            if(role=="Inventory.Sell")choices=choices.Where(m=>m is MethodDefinition f && f.Parameters[1].ParameterType.FullName.Contains("System.Collections.Generic.List`1<ὥὣὮὨὫὣὩὨὪὣὭ>")).ToArray();
            if(choices.Length!=1)throw new InvalidOperationException(role+": "+string.Join("\n",choices.Select(m=>m.FullName)));
            var m=choices.Single();apis.Add(new(role,m.DeclaringType.FullName,name,arity,MetadataIndex.Signature(m)));
        }
        void Role(string role,string name){roles.Add(role,name);selectedTypes.Add(index.Find(name));}
        // All reflection lookups explicitly declared by the runtime's admission check.
        var text=File.ReadAllText(Path.Combine(hookSource,"FishingBindings.cs"));
        foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,"\\{\"([^\"]+)\",new\\[\\]\\{([^}]+)\\}\\}"))
        {
            var type=match.Groups[1].Value;
            foreach(System.Text.RegularExpressions.Match n in System.Text.RegularExpressions.Regex.Matches(match.Groups[2].Value,"\"([^\"]+)\""))Select(type,n.Groups[1].Value);
        }
        foreach(var field in new[]{"ὡὮὮὪὨὨὪὪὫὣὩ","ὦὣὪὬὫὢὢὩὤὥὤ","ὦὪὦὭὧὯὮὮὪὫὣ","ὦὭὠὠὣὢὯὢὩὣὪ"})
        {var f=(FieldDefinition)Select("FishingSkillCaster",field).Single();Select(f.FieldType.Resolve().FullName,"ὡὫὮὠὡὩὮὨὮὡὬ");}
        foreach(var name in new[]{"FishingSkillTeethItem","FishingSkillRecoveryItem","FishingSkillTrapItem","FishingSkillShellShieldItem"})Select(name,"ὤὨὪὫὧὥὫὮὣὮὥ");
        foreach(var pair in new[]{new[]{"ὣὠὦὨὩὫὩὩὣὤὮ","ὦὬὢὢὮὩὨὨὠὠὯ"},new[]{"ὧὭὥὩὯὦὧὠὧὡὦ","ὩὤὮὠὠὥὪὢὩὯὡ"}})
        {var p=(PropertyDefinition)Select(pair[0],pair[1]).Single();Select(p.PropertyType.Resolve().FullName,"ὬὤὦὩὮὨὠὬὯὯὣ");}
        const string tables="ὥὧὣὭὠὬὣὡὬὫὯ", inventory="ὧὨὮὤὤὠὯὧὧὦὠ";
        Role("Inventory",inventory);Role("SaleItem","ὥὣὮὨὫὣὩὨὪὣὭ");Role("ItemType","ὪὡὢὦὢὡὯὩὫὤὥ");Role("FishGrade","ὥὡὯὭὫὨὯὧὡὯὤ/ὠὯὯὮὯὧὩὪὡὫὧ");
        Select(roles["SaleItem"],".ctor",4);
        foreach(var property in new[]{"ὢὯὧὤὮὭὮὣὭὪὣ","ὠὬὩὥὥὨὠὭὩὬὬ","ὬὯὭὩὡὮὩὨὢὭὣ","ὬὯὫὯὮὠὡὪὤὣὬ"})Select(roles["SaleItem"],property);
        Api("Tables.CastGrade",tables,"ὪὬὠὬὮὮὬὠὮὢὨ",2);
        Api("Tables.Shop",tables,"ὤὠὡὭὩὥὥὥὦὨὧ",1);Api("Tables.ShopEntries",tables,"ὧὮὮὤὠὨὩὣὢὪὦ",1);
        Api("Tables.Fish",tables,"ὦὮὩὣὥὡὨὪὤὠὤ",1);Api("Tables.Bait",tables,"ὠὫὯὬὡὮὦὣὨὧὭ",1);Api("Tables.Buff",tables,"ὣὪὯὧὦὮὡὥὣὫὬ",1);
        Api("Inventory.FishList",inventory,"ὡὬὠὨὫὦὥὪὡὮὣ",0);Api("Inventory.Sell",inventory,"ὫὡὮὫὢὭὢὡὣὭὫ",3);
        Api("Inventory.BaitList",inventory,"ὣὣὪὪὧὫὥὡὮὣὨ",0);Api("Inventory.BaitCount",inventory,"ὩὪὧὬὥὪὠὣὥὥὯ",2);
        Api("Inventory.FindBait",inventory,"ὬὧὩὢὡὤὮὦὠὫὯ",2);Api("Inventory.BaitByIndex",inventory,"ὬὧὩὢὡὤὮὦὠὫὯ",1);
        Api("Ui.IsHud","ὨὧὠὯὪὦὩὣὤὢὡ","ὩὠὮὥὫὧὢὣὯὯὫ",1);
        Api("Player.Data","ὨὬὣὫὩὯὩὩὣὠὧ","ὫὩὢὤὬὭὢὣὭὮὣ");
        Api("Camera.Instance","GameCameraManager","ὪὨὦὬὡὬὠὧὥὪὭ");Api("Camera.UiCamera","GameCameraManager","ὭὣὭὮὣὮὨὯὥὭὭ");
        Api("Field.Needle","FishingGameFieldDefaultUI","ὠὭὩὭὮὤὣὣὥὥὨ");
        var needle=(PropertyDefinition)Select("FishingGameFieldDefaultUI","ὠὭὩὭὮὤὣὣὥὥὨ").Single();
        Api("Needle.Rect",needle.PropertyType.Resolve().FullName,"ὥὭὫὩὫὦὦὫὫὯὤ");
        Select("UIBase","ὡὡὡὯὬὨὢὧὦὦὫ");
        return new(1,selectedTypes.OrderBy(t=>t.FullName,StringComparer.Ordinal).Select(t=>new TypeContract(t.FullName,index.Shape(t),t.Methods.Where(m=>m.HasBody && m.Body.Instructions.Count>=10).OrderByDescending(m=>m.Body.Instructions.Count).Take(8).Select(index.Body).ToArray(),selected.Where(m=>m.DeclaringType==t).OrderBy(m=>m.FullName,StringComparer.Ordinal).Select(m=>new MemberContract(m.Name,MetadataIndex.Signature(m),index.MemberBody(m),index.Uses(m))).ToArray())).ToArray(),apis.ToArray(),roles);
    }
}
