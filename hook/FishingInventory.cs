using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Proto.Net;
namespace BD2Fishing.Runtime
{
    internal sealed class FishingInventory
    {
        private readonly FishingSaleProgress progress=new FishingSaleProgress();
        private readonly FishingUnlockProgress unlock=new FishingUnlockProgress();
        private readonly HashSet<long> unlockedIds=new HashSet<long>();
        private FishingSalePlan batch;
        private string cancelledOwner="",operationStatus="";
        private FishingSaleAuthorization authorization;
        private long unlockReplyAtSend;
        private FishingSpeciesSummary[] species=new FishingSpeciesSummary[0];
        private readonly Action<string> log;
        private FishingSalePlan plan;
        private long lastRead,replyAtSend;
        private int groupId;
        private string problem="等待读取鱼背包",lastProgress="";
        internal FishingInventory(Action<string> log){this.log=log;}
        internal static MethodInfo SaleMethod()=>(MethodInfo)FishingBindings.Api("Inventory.Sell");
        private static List<FishingFishDBInfo> Bag() => ((System.Collections.IEnumerable)FishingBindings.Invoke("Inventory.FishList"))?.Cast<FishingFishDBInfo>().ToList();
        internal void AcknowledgeError(){progress.AcknowledgeError();unlock.AcknowledgeError();}
        private void Refresh(long now,FishingRetentionOptions options)
        {
            lastRead=now;plan=null;groupId=0;problem="";
            try
            {
                var inventory=Bag();
                var shop=FishingBindings.Invoke("Tables.Shop",1);
                if(inventory==null || shop==null || Convert.ToInt32(FishingBindings.Get(shop,"ShopItemId"))<=0)throw new InvalidOperationException("背包或出售商店资料尚未就绪");
                groupId=Convert.ToInt32(FishingBindings.Get(shop,"ShopItemId"));
                var entries=FishingBindings.Invoke("Tables.ShopEntries",groupId) as System.Collections.IEnumerable;
                if(entries==null)throw new InvalidOperationException("鱼出售表尚未就绪");
                var sellableIds=new HashSet<int>(entries.Cast<object>().Where(e=>e!=null && FishingBindings.Num(e,"GroupId")==groupId && FishingBindings.Num(e,"ItemType")==FishingBindings.EnumValue("ItemType","Fish") && FishingBindings.Num(e,"ItemId")>0 && FishingBindings.Num(e,"PriceCount")>0).Select(e=>(int)FishingBindings.Num(e,"ItemId")));
                var items=new List<FishingSaleItem>();
                foreach(var f in inventory)
                {
                    if(f==null)throw new InvalidOperationException("鱼背包包含空记录，暂停出售");
                    var table=FishingBindings.Invoke("Tables.Fish",f.Id);
                    items.Add(new FishingSaleItem{InvenIndex=f.InvenIndex,FishId=f.Id,IsLocked=f.IsLock,Size=f.Size,WasLocked=unlockedIds.Contains(f.InvenIndex),Name=table==null?"":Convert.ToString(FishingBindings.Invoke("Text.FishName",(int)FishingBindings.Num(table,"NameTextId"))),Grade=table==null?0:(int)FishingBindings.Num(table,"Grade"),HasFishTable=table!=null && FishingBindings.Num(table,"Id")==f.Id,HasSaleEntry=sellableIds.Contains(f.Id)});
                }
                plan=FishingSalePlan.Build(items,options);
                species=items.Where(f=>f.FishId>0).GroupBy(f=>f.FishId).OrderBy(g=>g.Key).Select(g=>new FishingSpeciesSummary{FishId=g.Key,Name=g.First().Name,Count=g.Count(),MinSize=g.Where(f=>f.Size>0).Select(f=>f.Size).DefaultIfEmpty(0).Min(),MaxSize=g.Where(f=>f.Size>0).Select(f=>f.Size).DefaultIfEmpty(0).Max()}).ToArray();
            }
            catch(Exception e){problem=e.GetBaseException().Message;}
        }
        internal void Fill(FishingSnapshot s,long now,FishingControl control)
        {
            var options=control.Retention??new FishingRetentionOptions();
            if(batch!=null && (!authorization.Valid(control,now,s.ProcessId) || !s.Ready || s.Busy || s.MapChangePending))
            {
                batch=null;operationStatus="已取消后续出售；已确认解锁 "+unlock.UnlockedCount+" 条，锁定状态不会自动还原";
                cancelledOwner=authorization.OwnerId;
            }
            if(unlock.Pending)
            {
                bool reply=s.UnlockReplySerial>unlockReplyAtSend && s.Ready;
                var fish=reply?Bag()?.FirstOrDefault(f=>f.InvenIndex==unlock.InvenIndex):null;
                unlock.Observe(reply,s.UnlockReplyAccepted,s.UnlockReplyIndex,fish!=null,fish==null || fish.IsLock,now);
                if(reply && unlock.Completed){unlockedIds.Add(unlock.InvenIndex);lastRead=0;}
            }
            if(progress.Pending)
            {
                bool reply=s.SaleReplySerial>replyAtSend && s.Ready;
                var ids=reply?Bag()?.Select(f=>f.InvenIndex).ToArray():new long[0];
                if(!reply || ids!=null)progress.Observe(reply,s.SaleReplyAccepted,ids,now);
                if(reply)lastRead=0;
            }
            s.SalePending=progress.Pending || unlock.Pending;s.SoldCount=progress.SoldCount;s.UnlockedCount=unlock.UnlockedCount;
            s.SaleStatus=unlock.Pending?unlock.Status:operationStatus.Length>0?operationStatus:progress.Status;
            if(unlock.Error.Length>0)s.Error=unlock.Error;
            if(progress.Error.Length>0)s.Error=progress.Error;
            if(control.Enabled && control.OwnerId==cancelledOwner)s.Error=operationStatus+"；请重新开始钓鱼";
            if(s.Error.Length>0)s.SaleStatus=s.Error;
            if(s.SaleStatus!=lastProgress){lastProgress=s.SaleStatus;log("sale_status "+lastProgress);}
            if(!s.Ready)return;
            var bag=Bag();
            s.BagCount=bag?.Count??0;
            s.BagCapacity=(int)FishingBindings.Num(FishingBindings.Read("Player.Data",null),"FishingFishInvenSlot");
            if(!s.SalePending && s.State=="None" && ((plan!=null && plan.Options.Fingerprint()!=options.Fingerprint()) || now-lastRead>=TimeSpan.FromMilliseconds(500).Ticks))Refresh(now,options);
            s.SaleReady=plan!=null && problem.Length==0;s.SellableCount=plan?.Sellable??0;s.ProtectedFishCount=plan?.Protected??0;s.FishSpecies=species;
            if(problem.Length>0)s.SaleStatus=problem;
        }
        internal static System.Collections.IList RequestItems(FishingSalePlan freshPlan)
        {
            if(freshPlan==null || freshPlan.Items.Length==0 || freshPlan.Items.Any(f=>!FishingSalePlan.CanSell(f,freshPlan.Options)))throw new InvalidOperationException("出售清单未通过保护检查");
            var itemType=FishingBindings.Type("SaleItem");
            var list=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            foreach(var f in freshPlan.Items)list.Add(Activator.CreateInstance(itemType,new object[]{f.FishId,FishingBindings.EnumValue("ItemType","Fish"),1,f.InvenIndex}));
            return list;
        }
        internal bool Sell(long now,FishingSnapshot snapshot,FishingControl control)
        {
            if(unlock.Pending || progress.Pending || unlock.Error.Length>0 || progress.Error.Length>0)return false;
            var options=control.Retention??new FishingRetentionOptions();
            Refresh(now,options);
            if(plan==null || problem.Length>0 || plan.Items.Length==0){batch=null;return false;}
            if(control.OwnerId==cancelledOwner)return false;
            if(batch==null){batch=plan;authorization=new FishingSaleAuthorization(control);}
            if(!authorization.Valid(control,now,snapshot.ProcessId)){batch=null;cancelledOwner=authorization.OwnerId;return false;}
            // Replan over the WHOLE bag, then limit to this transaction's original authorization.
            var fresh=plan.RestrictTo(batch.Items.Select(f=>f.InvenIndex));
            if(fresh.Items.Length==0){batch=null;operationStatus="当前批次已无可售鱼，等待重新评估";return false;}
            if(fresh.RequiresUnlock.Length>0)
            {
                long id=fresh.RequiresUnlock[0];
                unlockReplyAtSend=snapshot.UnlockReplySerial;unlock.Begin(id,now);
                log("unlock_send index="+id);
                FishingBindings.Invoke("Inventory.Unlock",id,false,null);
                operationStatus="解锁待售鱼后重新核对保留规则";
                return true;
            }
            var items=RequestItems(fresh);
            replyAtSend=snapshot.SaleReplySerial;progress.Begin(fresh,now);
            batch=null;operationStatus="";
            log("sale_send group="+groupId+" kept="+fresh.KeepIds.Length+" items="+string.Join(",",fresh.Items.Select(f=>f.InvenIndex+":"+f.FishId+":grade"+f.Grade)));
            FishingBindings.Invoke("Inventory.Sell",groupId,items,null);
            return true;
        }
    }
}
