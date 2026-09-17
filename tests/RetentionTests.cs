using BD2Fishing;
internal static class RetentionTests
{
    public static int Run()
    {
        int count=0;
        void Check(bool value,string message){count++;if(!value)throw new Exception(message);}
        void Throws<T>(Action action,string message) where T:Exception {bool caught=false;try{action();}catch(T){caught=true;}Check(caught,message);}
        FishingSaleItem Fish(long id,int species=1,int size=20,int grade=4,bool locked=false)=>new(){InvenIndex=id,FishId=species,Size=size,Grade=grade,IsLocked=locked,HasFishTable=true,HasSaleEntry=true};
        FishingRetentionOptions Open()=>new(){KeepLegendary=false,KeepLocked=false,KeepUnknown=false};
        FishingRetentionOptions Rule(FishingSizeMode mode)=>new(){KeepLegendary=false,KeepLocked=false,SizeRules=new[]{new FishingSizeRule{FishId=1,Mode=mode}}};
        long[] Sold(FishingSaleItem[] fish,FishingRetentionOptions options)=>FishingSalePlan.Build(fish,options).Items.Select(x=>x.InvenIndex).ToArray();
        var defaults=new FishingRetentionOptions();
        Check(defaults.KeepLegendary==true && defaults.KeepLocked==true && defaults.KeepUnknown==true && defaults.SizeLegendary==true && defaults.SizeLocked==true && defaults.SizeRules.Length==0,"all retention defaults enabled");
        var nulls=new FishingRetentionOptions{KeepLegendary=null,KeepLocked=null,KeepUnknown=null,SizeLegendary=null,SizeLocked=null,SizeRules=null!};
        Check(defaults.Fingerprint()==nulls.Fingerprint(),"null values have default semantics");
        foreach(bool legend in new[]{false,true})foreach(bool locked in new[]{false,true})foreach(bool unknown in new[]{false,true})
        {
            var o=new FishingRetentionOptions{KeepLegendary=legend,KeepLocked=locked,KeepUnknown=unknown};
            var input=new[]{Fish(1,grade:1),Fish(2),Fish(3,grade:1,locked:true),Fish(4,locked:true),Fish(5,grade:3),Fish(6,size:0,grade:1)};
            var expected=new List<long>{1};if(!legend)expected.Add(2);if(!locked)expected.Add(3);if(!legend&&!locked)expected.Add(4);if(!legend&&!unknown)expected.Add(5);if(!unknown)expected.Add(6);
            Check(Sold(input,o).SequenceEqual(expected),"independent category keep union");
        }
        var group=new[]{Fish(1,size:10),Fish(2,size:20),Fish(3,size:30),Fish(4,species:2,size:99)};
        Check(Sold(group,Rule(FishingSizeMode.Maximum)).SequenceEqual(new long[]{1,2,4}),"maximum per species");
        Check(Sold(group,Rule(FishingSizeMode.Minimum)).SequenceEqual(new long[]{2,3,4}),"minimum per species");
        Check(Sold(group,Rule(FishingSizeMode.Both)).SequenceEqual(new long[]{2,4}),"both per species");
        Check(Sold(group,Rule(FishingSizeMode.None)).Length==4,"none retains no extrema");
        var tied=new[]{Fish(3,locked:true),Fish(2),Fish(1,locked:true)};
        Check(Sold(tied,Rule(FishingSizeMode.Both)).SequenceEqual(new long[]{2,3}),"equal extrema keep one preferring locked then smallest ID");
        Check(Sold(tied.Reverse().ToArray(),Rule(FishingSizeMode.Both)).SequenceEqual(new long[]{2,3}),"tie independent of inventory order");
        tied[2].IsLocked=false;tied[2].WasLocked=true;
        Check(Sold(tied,Rule(FishingSizeMode.Both)).SequenceEqual(new long[]{2,3}),"our unlock preserves category and tie identity");
        var overlap=new[]{Fish(1,size:10,locked:true),Fish(2,size:20),Fish(3,size:30,grade:1,locked:true),Fish(4,size:40,grade:1)};
        Check(Sold(overlap,Rule(FishingSizeMode.Maximum)).SequenceEqual(new long[]{1,4}),"separate category extrema union");
        var onlyLegend=Rule(FishingSizeMode.Maximum);onlyLegend.SizeLocked=false;
        Check(Sold(overlap,onlyLegend).SequenceEqual(new long[]{1,3,4}),"legendary size scope independent");
        var onlyLocked=Rule(FishingSizeMode.Maximum);onlyLocked.SizeLegendary=false;
        Check(Sold(overlap,onlyLocked).SequenceEqual(new long[]{1,2,4}),"locked size scope independent");
        var neither=Rule(FishingSizeMode.Maximum);neither.SizeLegendary=false;neither.SizeLocked=false;
        Check(Sold(overlap,neither).Length==4,"disabled size scopes select all");
        var unknownSize=new[]{Fish(1,size:0),Fish(2,size:20),Fish(3,grade:1),Fish(4,species:2)};
        var permissiveRule=Rule(FishingSizeMode.Maximum);permissiveRule.KeepUnknown=false;
        Check(Sold(unknownSize,permissiveRule).SequenceEqual(new long[]{4}),"unknown size preserves affected whole species");
        var unknownGrade=new[]{Fish(1,size:100,grade:3),Fish(2,size:20),Fish(3,size:40,grade:1)};
        Check(Sold(unknownGrade,permissiveRule).SequenceEqual(new long[]{3}),"unknown grade cannot bypass legendary extrema protection");
        foreach(var mode in new[]{FishingSizeMode.Maximum,FishingSizeMode.Minimum,FishingSizeMode.Both})foreach(bool hasKnown in new[]{false,true})
        {
            var uncertain=Rule(mode);uncertain.KeepUnknown=false;
            var sample=hasKnown?unknownGrade:unknownGrade.Where(f=>f.Grade!=4).ToArray();
            Check(Sold(sample,uncertain).SequenceEqual(new long[]{3}),"uncertain legendary size membership protects every mode with or without known legendary");
            Check(!FishingSalePlan.CanSell(sample[0],uncertain),"final guard rejects uncertain legendary extrema membership");
        }
        var noLegendScope=permissiveRule.Clone();noLegendScope.SizeLegendary=false;
        Check(Sold(unknownGrade,noLegendScope).SequenceEqual(new long[]{1,2,3}),"unknown grade may sell when no uncertain category protection applies");
        var bad=Fish(1);bad.HasSaleEntry=false;var noTable=Fish(2);noTable.HasFishTable=false;var badSpecies=Fish(3,species:0);
        Check(Sold(new[]{bad,noTable,badSpecies},Open()).Length==0,"hard metadata exclusions cannot opt out");
        Throws<InvalidOperationException>(()=>FishingSalePlan.Build(new[]{Fish(1),Fish(1)},Open()),"duplicate ID fails closed");
        Throws<InvalidOperationException>(()=>FishingSalePlan.Build(new[]{Fish(0)},Open()),"invalid ID fails closed");
        foreach(var rules in new[]{new[]{new FishingSizeRule{FishId=0}},new[]{new FishingSizeRule{FishId=1,Mode=(FishingSizeMode)99}},new[]{new FishingSizeRule{FishId=1},new FishingSizeRule{FishId=1}},new FishingSizeRule[]{null!}})
            Throws<ArgumentException>(()=>FishingSalePlan.Build(group,new FishingRetentionOptions{SizeRules=rules}),"invalid rules fail closed");
        var options=Rule(FishingSizeMode.Maximum);var plan=FishingSalePlan.Build(group,options);options.KeepLegendary=true;options.SizeRules[0].Mode=FishingSizeMode.None;
        Check(plan.Options.KeepLegendary==false && plan.Options.SizeRules[0].Mode==FishingSizeMode.Maximum,"plan snapshots options deeply");
        var exposed=plan.Options;exposed.KeepLegendary=true;exposed.SizeRules[0].Mode=FishingSizeMode.None;
        Check(plan.Options.KeepLegendary==false && plan.Options.SizeRules[0].Mode==FishingSizeMode.Maximum,"plan snapshot cannot be mutated through exposed options");
        var clone=plan.Options.Clone();clone.SizeRules[0].FishId=9;Check(plan.Options.SizeRules[0].FishId==1,"clone owns rules");
        var ordered=Open();ordered.SizeRules=new[]{new FishingSizeRule{FishId=2,Mode=FishingSizeMode.Both},new FishingSizeRule{FishId=1,Mode=FishingSizeMode.Minimum}};
        var reversed=ordered.Clone();Array.Reverse(reversed.SizeRules);Check(ordered.Fingerprint()==reversed.Fingerprint(),"fingerprint ignores rule order");
        var noRule=Open();var noneRule=Open();noneRule.SizeRules=new[]{new FishingSizeRule{FishId=1,Mode=FishingSizeMode.None}};Check(noRule.Fingerprint()==noneRule.Fingerprint(),"none equals absent rule");
        var many=Enumerable.Range(1,230).Select(i=>Fish(i,locked:i%2==0)).ToArray();var bounded=FishingSalePlan.Build(many,Open());
        Check(bounded.Items.Length==100 && bounded.Sellable==230 && bounded.Protected==0 && bounded.Total==230 && bounded.KeepIds.Length==130,"bounded plan accounts unsubmitted fish");
        Check(bounded.RequiresUnlock.SequenceEqual(Enumerable.Range(1,50).Select(i=>(long)i*2)),"unlock only selected locked batch");
        Check(!FishingSalePlan.CanSell(Fish(1,locked:true),Open()),"actual lock always blocks direct sale");
        Throws<InvalidOperationException>(()=>new FishingSaleProgress().Begin(bounded,0),"sale progress refuses pending unlock");
        var was=Fish(1,grade:1);was.WasLocked=true;Check(!FishingSalePlan.CanSell(was,new FishingRetentionOptions()),"effective lock retention survives our unlock");
        Check(FishingSalePlan.CanSell(was,Open()),"confirmed unlocked candidate can sell with keep locked off");
        var restricted=bounded.RestrictTo(new long[]{1,2,101,999});
        Check(restricted.Items.Select(f=>f.InvenIndex).SequenceEqual(new long[]{1,2}) && restricted.KeepIds.Length==228 && restricted.RequiresUnlock.SequenceEqual(new long[]{2}),"restriction never expands original selected batch");
        Check(restricted.Total==230 && restricted.Sellable==2 && restricted.Protected==228,"restricted counts describe authorized plan");
        Check(bounded.Items.Length==100 && bounded.KeepIds.Length==130,"restriction leaves original unchanged");
        return count;
    }
}
