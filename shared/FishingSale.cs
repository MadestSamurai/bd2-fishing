#pragma warning disable 8625 // Shared with the legacy game compiler; null options intentionally mean defaults.
using System;
using System.Collections.Generic;
using System.Linq;
namespace BD2Fishing
{
    public sealed class FishingSaleItem
    {
        public long InvenIndex {get;set;}
        public int FishId {get;set;}
        public int Size {get;set;}
        public string Name {get;set;}="";
        public bool WasLocked {get;set;}
        public int Grade {get;set;}
        public bool IsLocked {get;set;}
        public bool HasFishTable {get;set;}
        public bool HasSaleEntry {get;set;}
    }
    public sealed class FishingSalePlan
    {
        public const int MaxBatch = 100;
        public const int LegendaryGrade = 4;
        public FishingSaleItem[] Items {get;private set;} = new FishingSaleItem[0];
        public long[] KeepIds {get;private set;} = new long[0];
        public int Total {get;private set;}
        public int Sellable {get;private set;}
        public int Protected {get;private set;}
        private FishingRetentionOptions optionsSnapshot = new FishingRetentionOptions();
        public FishingRetentionOptions Options {get{return optionsSnapshot.Clone();} private set{optionsSnapshot=value.Clone();}}
        public long[] RequiresUnlock {get;private set;} = new long[0];
        // This is the final per-item send guard; extrema require a fresh whole-inventory plan.
        public static bool CanSell(FishingSaleItem fish,FishingRetentionOptions options=null)
        {
            var snapshot=(options??new FishingRetentionOptions()).Clone();
            return fish!=null && !fish.IsLocked && Candidate(fish,snapshot);
        }
        private static bool EffectiveLocked(FishingSaleItem fish) => fish.IsLocked || fish.WasLocked;
        private static bool Candidate(FishingSaleItem fish,FishingRetentionOptions options)
        {
            if(fish==null || fish.InvenIndex<=0 || fish.FishId<=0 || !fish.HasFishTable || !fish.HasSaleEntry)return false;
            bool known=fish.Grade==1 || fish.Grade==2 || fish.Grade==LegendaryGrade;
            if(!known && (options.KeepUnknown!=false || options.KeepLegendary!=false))return false;
            // Unknown rarity might belong to the legendary size scope, even when blanket keeps are off.
            if(!known && options.SizeLegendary!=false && options.SizeRules.Any(r=>r.FishId==fish.FishId && r.Mode!=FishingSizeMode.None))return false;
            if(fish.Size<=0 && options.KeepUnknown!=false)return false;
            if(fish.Grade==LegendaryGrade && options.KeepLegendary!=false)return false;
            return !EffectiveLocked(fish) || options.KeepLocked==false;
        }
        public static FishingSalePlan Build(IEnumerable<FishingSaleItem> inventory,FishingRetentionOptions options=null)
        {
            var snapshot=(options??new FishingRetentionOptions()).Clone();
            if(inventory==null)throw new InvalidOperationException("鱼背包尚未载入");
            var all=inventory.ToArray();
            if(all.Any(f=>f==null || f.InvenIndex<=0) || all.Select(f=>f.InvenIndex).Distinct().Count()!=all.Length)
                throw new InvalidOperationException("鱼背包实例 ID 无效或重复，暂停自动出售");
            var retained=new HashSet<long>();
            foreach(var rule in snapshot.SizeRules.Where(r=>r.Mode!=FishingSizeMode.None))
            {
                var species=all.Where(f=>f.FishId==rule.FishId).ToArray();
                var groups=new[]{species.Where(f=>snapshot.SizeLegendary!=false && f.Grade==LegendaryGrade).ToArray(),species.Where(f=>snapshot.SizeLocked!=false && EffectiveLocked(f)).ToArray()};
                if(groups.Any(g=>g.Length>0) && species.Any(f=>f.Size<=0))
                {
                    foreach(var fish in species)retained.Add(fish.InvenIndex);
                    continue;
                }
                foreach(var group in groups)
                {
                    if(group.Length==0)continue;
                    if(rule.Mode==FishingSizeMode.Maximum || rule.Mode==FishingSizeMode.Both)
                        retained.Add(group.OrderByDescending(f=>f.Size).ThenByDescending(EffectiveLocked).ThenBy(f=>f.InvenIndex).First().InvenIndex);
                    if(rule.Mode==FishingSizeMode.Minimum || rule.Mode==FishingSizeMode.Both)
                        retained.Add(group.OrderBy(f=>f.Size).ThenByDescending(EffectiveLocked).ThenBy(f=>f.InvenIndex).First().InvenIndex);
                }
            }
            var sale=all.Where(f=>Candidate(f,snapshot) && !retained.Contains(f.InvenIndex)).OrderBy(f=>f.InvenIndex).ToArray();
            var batch=sale.Take(MaxBatch).ToArray();var ids=new HashSet<long>(batch.Select(f=>f.InvenIndex));
            return new FishingSalePlan{Options=snapshot,Items=batch,RequiresUnlock=batch.Where(f=>f.IsLocked).Select(f=>f.InvenIndex).ToArray(),KeepIds=all.Where(f=>!ids.Contains(f.InvenIndex)).Select(f=>f.InvenIndex).ToArray(),Total=all.Length,Sellable=sale.Length,Protected=all.Length-sale.Length};
        }
        public FishingSalePlan RestrictTo(IEnumerable<long> authorizedIds)
        {
            if(authorizedIds==null)throw new ArgumentNullException(nameof(authorizedIds));
            var allowed=new HashSet<long>(authorizedIds);
            var batch=Items.Where(f=>allowed.Contains(f.InvenIndex)).ToArray();
            return new FishingSalePlan{Options=Options.Clone(),Items=batch,RequiresUnlock=batch.Where(f=>f.IsLocked).Select(f=>f.InvenIndex).ToArray(),
                KeepIds=KeepIds.Concat(Items.Where(f=>!allowed.Contains(f.InvenIndex)).Select(f=>f.InvenIndex)).OrderBy(id=>id).ToArray(),Total=Total,Sellable=batch.Length,Protected=Total-batch.Length};
        }
    }
    public sealed class FishingSaleProgress
    {
        private long sentAt;private long[] soldIds=new long[0],keepIds=new long[0];
        public bool Pending {get;private set;}
        public int SoldCount {get;private set;}
        public string Error {get;private set;}="";
        public string Status {get;private set;}="尚未自动出售";
        public void Begin(FishingSalePlan plan,long now)
        {
            if(Pending || Error.Length>0)throw new InvalidOperationException("上一批出售尚未确认");
            if(plan==null || plan.Items.Length==0 || plan.Items.Any(f=>!FishingSalePlan.CanSell(f,plan.Options)))throw new InvalidOperationException("没有可安全出售的鱼");
            soldIds=plan.Items.Select(f=>f.InvenIndex).ToArray();keepIds=plan.KeepIds.ToArray();sentAt=now;Pending=true;
            Status="正在出售 "+soldIds.Length+" 条鱼，按当前保留规则筛选";
        }
        public void Observe(bool responseArrived,bool accepted,IEnumerable<long> currentIds,long now)
        {
            if(!Pending)return;
            if(!responseArrived)
            {
                if(now-sentAt>TimeSpan.FromSeconds(30).Ticks){Error="出售响应超过 30 秒，未重发；请检查游戏网络提示";Status=Error;}
                return;
            }
            Pending=false;
            if(!accepted){Error="游戏拒绝了出售请求，请核对提示后再开启";Status=Error;return;}
            var current=new HashSet<long>(currentIds);
            if(soldIds.Any(current.Contains) || keepIds.Any(id=>!current.Contains(id)))
            {Error="出售回执与背包回读不一致，已暂停；请检查诊断";Status=Error;return;}
            Error="";SoldCount+=soldIds.Length;Status="已确认出售 "+soldIds.Length+" 条，保留鱼回读一致";
        }
        public void AcknowledgeError(){if(!Pending)Error="";}
    }
}
