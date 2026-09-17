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
                if(now-sentAt>TimeSpan.FromSeconds(30).Ticks)Error=Status="解锁超过 30 秒，未重发；请核对游戏";
                return;
            }
            Pending=false;
            if(!accepted || replyIndex!=InvenIndex || !exists || locked)
            {Error=Status="解锁回执或背包锁定状态不一致，已暂停出售";return;}
            Completed=true;UnlockedCount++;
            Status="已确认解锁 "+UnlockedCount+" 条待售鱼";
        }
        public void AcknowledgeError(){if(!Pending)Error="";}
    }
}
