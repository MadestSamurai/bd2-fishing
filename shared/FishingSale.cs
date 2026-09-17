using System;
using System.Collections.Generic;
using System.Linq;
namespace BD2Fishing
{
    public sealed class FishingSaleItem
    {
        public long InvenIndex {get;set;}
        public int FishId {get;set;}
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
        public bool KeepLockedOnly {get;private set;}
        public int Total {get;private set;}
        public int Sellable {get;private set;}
        public int Protected {get;private set;}
        public static bool CanSell(FishingSaleItem fish,bool keepLockedOnly=false) => fish!=null && fish.InvenIndex>0 && fish.FishId>0
            && fish.HasFishTable && fish.HasSaleEntry && !fish.IsLocked && (fish.Grade==1 || fish.Grade==2 || keepLockedOnly && fish.Grade==LegendaryGrade);
        public static FishingSalePlan Build(IEnumerable<FishingSaleItem> inventory,bool keepLockedOnly=false)
        {
            if(inventory==null)throw new InvalidOperationException("鱼背包尚未载入");
            var all=inventory.ToArray();
            if(all.Any(f=>f==null || f.InvenIndex<=0) || all.Select(f=>f.InvenIndex).Distinct().Count()!=all.Length)
                throw new InvalidOperationException("鱼背包实例 ID 无效或重复，暂停自动出售");
            var sale=all.Where(f=>CanSell(f,keepLockedOnly)).OrderBy(f=>f.InvenIndex).ToArray();
            var batch=sale.Take(MaxBatch).ToArray();var ids=new HashSet<long>(batch.Select(f=>f.InvenIndex));
            return new FishingSalePlan{KeepLockedOnly=keepLockedOnly,Items=batch,KeepIds=all.Where(f=>!ids.Contains(f.InvenIndex)).Select(f=>f.InvenIndex).ToArray(),Total=all.Length,Sellable=sale.Length,Protected=all.Length-sale.Length};
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
            if(plan==null || plan.Items.Length==0 || plan.Items.Any(f=>!FishingSalePlan.CanSell(f,plan.KeepLockedOnly)))throw new InvalidOperationException("没有可安全出售的鱼");
            soldIds=plan.Items.Select(f=>f.InvenIndex).ToArray();keepIds=plan.KeepIds.ToArray();sentAt=now;Pending=true;
            Status="正在出售 "+soldIds.Length+" 条鱼，"+(plan.KeepLockedOnly?"仅保留上锁鱼及资料不明的鱼":"保留传说和上锁鱼");
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
