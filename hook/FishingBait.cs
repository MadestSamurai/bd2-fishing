using System;
using System.Reflection;
using gamfs.Fishing;
using Proto.Net;
namespace BD2Fishing.Runtime
{
    internal sealed class FishingBait
    {
        internal const int ItemId=1; // The native FishingBaitInfoItem button uses this consumable.
        private readonly FishingBaitProgress progress=new FishingBaitProgress();
        private readonly Action<string> log;
        private long replyAtSend;
        private string lastProgress="";
        internal FishingBait(Action<string> log){this.log=log;}
        internal static MethodInfo UseMethod()=>typeof(FishingManager).GetMethod("UseItem",new[]{typeof(long),typeof(int),typeof(int),typeof(int)})??throw new MissingMethodException("FishingManager.UseItem");
        internal static bool ValidItem(FishingItemDBInfo item)=>item!=null && item.InvenIndex>0 && item.Id==ItemId && item.Type==FishingBindings.EnumValue("ItemType","FishingConsumable") && item.Count>0;
        internal void AcknowledgeError()=>progress.AcknowledgeError();
        private static FishingManager Read(FishingGameFieldDefaultUI ui,FishingSnapshot s,out FishingItemDBInfo item,out int buffId)
        {
            item=null;buffId=0;
            if(!s.Ready || !FishingBindings.Active(ui))return null;
            var manager=(FishingManager)FishingBindings.Get(ui,"ὢὨὠὩὤὬὠὧὬὠὬ");
            var table=FishingBindings.Invoke("Tables.Bait",ItemId);
            if(manager==null || table==null || Convert.ToInt32(FishingBindings.Get(table,"Id"))!=ItemId || ((System.Collections.IList)FishingBindings.Get(table,"BuffId"))==null || ((System.Collections.IList)FishingBindings.Get(table,"BuffId")).Count==0 || Convert.ToInt32(((System.Collections.IList)FishingBindings.Get(table,"BuffId"))[0])<=0)
            {s.BaitStatus="鱼饵或增益资料尚未就绪，可关闭自动鱼饵继续钓鱼";return null;}
            buffId=Convert.ToInt32(((System.Collections.IList)FishingBindings.Get(table,"BuffId"))[0]);
            var buff=FishingBindings.Invoke("Tables.Buff",buffId);
            if(buff==null || Convert.ToInt32(FishingBindings.Get(buff,"Id"))!=buffId){s.BaitStatus="鱼饵增益表尚未就绪";return null;}
            var type=FishingBindings.EnumObject("ItemType","FishingConsumable");
            if(FishingBindings.Invoke("Inventory.BaitList")==null){s.BaitStatus="鱼饵库存尚未就绪";return null;}
            s.BaitCount=Convert.ToInt32(FishingBindings.Invoke("Inventory.BaitCount",type,ItemId));
            item=(FishingItemDBInfo)FishingBindings.Invoke("Inventory.FindBait",type,ItemId);
            if(s.BaitCount<0 || (s.BaitCount>0 && !ValidItem(item))){s.BaitStatus="鱼饵具体库存无法确认，等待刷新";return null;}
            s.BaitActive=manager.HasBuff(buffId);
            var hub=(FishingTimerHub)FishingBindings.Get(manager,"ὥὬὪὥὠὣὪὡὢὥὮ");
            s.BaitRemainingSeconds=s.BaitActive && hub!=null && hub.IsRunning("ADD_BUFF_"+buffId)?hub.GetRemaining("ADD_BUFF_"+buffId):0;
            s.BaitCanUse=FishingBindings.Flag(FishingBindings.Get(ui,"_baitInfo"),"ὩὯὪὩὯὫὮὤὭὧὢ");
            s.BaitReady=true;
            s.BaitStatus=s.BaitActive?"增益生效中，不重复消耗鱼饵":s.BaitCount==0?"鱼饵用尽，继续普通钓鱼":"增益未生效，开启自动鱼饵后在抛竿前补用";
            return manager;
        }
        internal void Fill(FishingGameFieldDefaultUI ui,FishingSnapshot s,long now)
        {
            var manager=Read(ui,s,out var item,out var buffId);
            if(progress.Pending)
            {
                bool reply=s.BaitReplySerial>replyAtSend;
                var used=reply?(FishingItemDBInfo)FishingBindings.Invoke("Inventory.BaitByIndex",progress.InvenIndex):null;
                bool readable=s.BaitReady && manager!=null && buffId==progress.BuffId;
                bool matches=used==null || ValidItem(used);
                progress.Observe(reply,s.BaitReplyAccepted,readable,matches?(used?.Count??0):-1,s.BaitActive,now);
                if(!progress.Pending)log("bait_verify index="+progress.InvenIndex+" reply="+s.BaitReplySerial+" accepted="+s.BaitReplyAccepted+" remaining="+(used?.Count??0)+" active="+s.BaitActive+" status="+progress.Status);
            }
            if(progress.Status!=lastProgress){lastProgress=progress.Status;log("bait_status "+lastProgress);}
            s.BaitPending=progress.Pending;s.BaitUsedCount=progress.UsedCount;
            if(progress.Pending)s.BaitStatus=progress.Status;
            if(progress.Error.Length>0){s.Error=progress.Error;s.BaitStatus=progress.Error;}
        }
        internal bool Use(FishingGameFieldDefaultUI ui,long now,long replySerial)
        {
            // Refresh the exact stack, actual buff and native button before spending a single item.
            var fresh=new FishingSnapshot{Ready=true};
            var manager=Read(ui,fresh,out var item,out var buffId);
            if(progress.Pending || progress.Error.Length>0 || manager==null || !fresh.BaitReady || !fresh.BaitCanUse || fresh.BaitActive || fresh.BaitCount<=0 || !ValidItem(item))return false;
            replyAtSend=replySerial;progress.Begin(item.InvenIndex,item.Count,buffId,now);
            log("bait_send index="+item.InvenIndex+" item="+item.Id+" type="+item.Type+" quantity=1 stack_before="+item.Count+" buff="+buffId);
            // Same entry point as confirming the bait popup. Preserve its original callback and user preference.
            manager.UseItem(item.InvenIndex,ItemId,FishingBindings.EnumValue("ItemType","FishingConsumable"),1);
            return true;
        }
    }
}
