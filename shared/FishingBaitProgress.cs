using System;
namespace BD2Fishing
{
    // A request is complete only after the original callback, stack decrement and actual buff agree.
    public sealed class FishingBaitProgress
    {
        public bool Pending {get;private set;}
        public long InvenIndex {get;private set;}
        public int BuffId {get;private set;}
        public int UsedCount {get;private set;}
        public string Error {get;private set;} = "";
        public string Status {get;private set;} = "尚未自动使用鱼饵";
        private int before;
        private long sent;
        public void Begin(long index,int count,int buffId,long now)
        {
            if(Pending || Error.Length>0 || index<=0 || count<=0 || buffId<=0)throw new InvalidOperationException("鱼饵请求尚未完成或库存／增益标识无效");
            InvenIndex=index;before=count;BuffId=buffId;sent=now;Pending=true;Status="等待鱼饵回执、扣减和增益生效";
        }
        public void Observe(bool reply,bool accepted,bool readable,int remaining,bool active,long now)
        {
            if(!Pending)return;
            if(reply && !accepted){Pending=false;Error=Status="鱼饵使用失败，请核对游戏提示";return;}
            if(reply && readable)
            {
                Pending=false;
                if(remaining!=before-1 || !active){Error=Status="鱼饵回执与库存／增益不一致，已暂停；请检查游戏";return;}
                UsedCount++;Status="已确认使用鱼饵，库存扣减与增益一致";return;
            }
            if(now-sent>TimeSpan.FromSeconds(30).Ticks)Status="等待同步鱼饵状态，恢复后自动继续";
        }
        public bool NeedsRefresh(long now)=>Pending&&now-sent>=TimeSpan.FromSeconds(30).Ticks;
        public bool Reconcile(long snapshotTicks,bool nativeIdle)
        {
            if(!Pending||!nativeIdle||snapshotTicks<=sent)return false;
            Pending=false;Error="";Status="鱼饵结果待核对，本次运行不再自动使用鱼饵";
            return true;
        }
        public void AcknowledgeError(){if(!Pending)Error="";}
    }
}
