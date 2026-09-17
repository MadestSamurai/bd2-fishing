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
        private readonly Action<string> log;
        private FishingSalePlan plan;
        private long lastRead,replyAtSend;
        private int groupId;
        private string problem="等待读取鱼背包",lastProgress="";
        internal FishingInventory(Action<string> log){this.log=log;}
        internal static MethodInfo SaleMethod()=>(MethodInfo)FishingBindings.Api("Inventory.Sell");
        private static List<FishingFishDBInfo> Bag() => ((System.Collections.IEnumerable)FishingBindings.Invoke("Inventory.FishList"))?.Cast<FishingFishDBInfo>().ToList();
        internal void AcknowledgeError()=>progress.AcknowledgeError();
        private void Refresh(long now,bool keepLegendaryAndLocked)
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
                    items.Add(new FishingSaleItem{InvenIndex=f.InvenIndex,FishId=f.Id,IsLocked=f.IsLock,Grade=table==null?0:(int)FishingBindings.Num(table,"Grade"),HasFishTable=table!=null && FishingBindings.Num(table,"Id")==f.Id,HasSaleEntry=sellableIds.Contains(f.Id)});
                }
                plan=FishingSalePlan.Build(items,keepLegendaryAndLocked);
            }
            catch(Exception e){problem=e.GetBaseException().Message;}
        }
        internal void Fill(FishingSnapshot s,long now,bool keepLegendaryAndLocked)
        {
            if(progress.Pending)
            {
                bool reply=s.SaleReplySerial>replyAtSend;
                var ids=reply?Bag().Select(f=>f.InvenIndex).ToArray():new long[0];
                progress.Observe(reply,s.SaleReplyAccepted,ids,now);
                if(reply)lastRead=0;
            }
            if(progress.Status!=lastProgress){lastProgress=progress.Status;log("sale_status "+lastProgress);}
            s.SalePending=progress.Pending;s.SoldCount=progress.SoldCount;
            s.SaleStatus=progress.Status;
            if(progress.Error.Length>0)s.Error=progress.Error;
            if(!s.Ready)return;
            var bag=Bag();
            s.BagCount=bag?.Count??0;
            s.BagCapacity=(int)FishingBindings.Num(FishingBindings.Read("Player.Data",null),"FishingFishInvenSlot");
            if(!progress.Pending && s.State=="None" && ((plan!=null && plan.KeepLegendaryAndLocked!=keepLegendaryAndLocked) || now-lastRead>=TimeSpan.FromMilliseconds(500).Ticks))Refresh(now,keepLegendaryAndLocked);
            s.SaleReady=plan!=null && problem.Length==0;s.SellableCount=plan?.Sellable??0;s.ProtectedFishCount=plan?.Protected??0;
            if(problem.Length>0)s.SaleStatus=problem;
        }
        internal static System.Collections.IList RequestItems(FishingSalePlan freshPlan)
        {
            if(freshPlan==null || freshPlan.Items.Length==0 || freshPlan.Items.Any(f=>!FishingSalePlan.CanSell(f,freshPlan.KeepLegendaryAndLocked)))throw new InvalidOperationException("出售清单未通过保护检查");
            var itemType=FishingBindings.Type("SaleItem");
            var list=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            foreach(var f in freshPlan.Items)list.Add(Activator.CreateInstance(itemType,new object[]{f.FishId,FishingBindings.EnumValue("ItemType","Fish"),1,f.InvenIndex}));
            return list;
        }
        internal bool Sell(long now,long replySerial,bool keepLegendaryAndLocked)
        {
            // Re-read locks, rarity, shop group and concrete inventory IDs immediately before sending.
            Refresh(now,keepLegendaryAndLocked);
            if(plan==null || problem.Length>0 || plan.Items.Length==0)return false;
            var items=RequestItems(plan);
            replyAtSend=replySerial;progress.Begin(plan,now);
            log("sale_send group="+groupId+" kept="+plan.KeepIds.Length+" items="+string.Join(",",plan.Items.Select(f=>f.InvenIndex+":"+f.FishId+":grade"+f.Grade)));
            // Same helper as the game's confirmed batch-sell button; it updates inventory and rewards on success.
            FishingBindings.Invoke("Inventory.Sell",groupId,items,null);
            return true;
        }
    }
}
