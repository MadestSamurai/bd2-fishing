using System;
namespace BD2Fishing
{
    // Receipt and local inventory must agree; a timeout never authorizes a resend.
    public sealed class FishingUnlockProgress
    {
        private long sentAt;
        public long InvenIndex {get;private set;}
        public bool Pending {get;private set;}
        public bool Completed {get;private set;}
        public int UnlockedCount {get;private set;}
        public string Error {get;private set;}="";
        public string Status {get;private set;}="";
        public void Begin(long index,long now)
        {
            if(index<=0 || Pending || Error.Length>0)throw new InvalidOperationException("解锁尚未确认或库存 ID 无效");
            InvenIndex=index;sentAt=now;Pending=true;Completed=false;Status="正在解锁待售鱼，等待回执";
        }
        public void Observe(bool responseArrived,bool accepted,long replyIndex,bool exists,bool locked,long now)
        {
            if(!Pending)return;
            if(!responseArrived)
            {
                if(now-sentAt>TimeSpan.FromSeconds(30).Ticks)Status="等待同步鱼锁定状态，恢复后自动继续";
                return;
            }
            Pending=false;
            if(!accepted || replyIndex!=InvenIndex || !exists || locked)
            {Error=Status="解锁回执或背包锁定状态不一致，已暂停出售";return;}
            Completed=true;UnlockedCount++;
            Status="已确认解锁 "+UnlockedCount+" 条待售鱼";
        }
        public bool NeedsRefresh(long now)=>Pending&&now-sentAt>=TimeSpan.FromSeconds(30).Ticks;
        public bool Reconcile(long snapshotTicks,bool nativeIdle)
        {
            if(!Pending||!nativeIdle||snapshotTicks<=sentAt)return false;
            Pending=false;Completed=false;Error="";Status="已同步当前锁定状态，重新核对保留规则";
            return true;
        }
        public void AcknowledgeError(){if(!Pending)Error="";}
    }
}
