using System;
namespace BD2Fishing
{
    // Only normal map travel; the client's room clock and limit are never changed.
    public sealed class FishingMapRenewal
    {
        private int stage, completed;
        private long sentAt, lobbySince, originalStart;
        public int Completed => completed;
        public int OriginalMap {get;private set;} = -1;
        public string Fault {get;private set;} = "";
        public string Status {get;private set;} = "";
        public void Cancel(){stage=0;lobbySince=0;OriginalMap=-1;Fault="";}
        public static double LeadSeconds(double duration) => Math.Min(300, duration / 6);
        private bool Finish(FishingSnapshot s,bool block)
        {s.MapRenewalStatus=Status;s.MapRenewals=completed;return block;}
        private bool Fail(FishingSnapshot s,string reason)
        {Fault=Status=reason;return Finish(s,true);}
        public bool Next(FishingSnapshot s,FishingControl c,long now,out FishingAction action)
        {
            action=FishingAction.None;
            if(!c.AutoMapRenewal || !c.Valid(now,s.ProcessId))
            {Cancel();Status="自动往返换图已关闭";return Finish(s,false);}
            if(Fault.Length>0)return Fail(s,Fault);
            bool clear=!s.Busy && !s.MapTravelBusy && !s.MapChangePending && s.BlockReason.Length==0 &&
                !s.NetworkPending && !s.SalePending && !s.BaitPending && !s.ResultPopup && !s.LevelPopup;
            if(stage!=0)
            {
                if(now-sentAt>TimeSpan.FromSeconds(120).Ticks)
                    return Fail(s,stage==1?"前往钓鱼大厅超时，已暂停；请检查游戏后重新开启":"返回原钓场或确认新倒计时超时，已暂停；请检查游戏后重新开启");
                if(!s.MapTravelBusy && s.MapGroupId>=0 && s.MapGroupId!=0 && s.MapGroupId!=OriginalMap)
                    return Fail(s,"检测到手动切换至其他地图，已取消往返并暂停钓鱼");
                if(stage==1)
                {
                    Status="即将达到地图停留上限，正在前往钓鱼大厅";
                    // PrepareEnterPackFishing changes the group ID BEFORE the scene finishes loading.
                    if(!s.LobbyReady || s.MapGroupId!=0 || !clear){lobbySince=0;return Finish(s,true);}
                    if(lobbySince==0)lobbySince=now;
                    Status="已到钓鱼大厅，等待场景稳定后返回原钓场";
                    if(now-lobbySince<TimeSpan.FromSeconds(2).Ticks)return Finish(s,true);
                    stage=2;sentAt=now;action=FishingAction.TravelReturn;
                    return Finish(s,true);
                }
                Status="正在返回原钓场，等待新的入场时间和可钓状态";
                if(!clear || !s.Ready || s.State!="None" || !s.CanCast || s.MapGroupId!=OriginalMap ||
                    !s.RoomTimerKnown || s.RoomStartTicks<=originalStart || s.RoomRemainingSeconds<=LeadSeconds(s.RoomDurationSeconds))return Finish(s,true);
                completed++;stage=0;Status="往返换图完成，新的地图倒计时已确认";
                return Finish(s,true); // Resume through the normal cast policy on the next frame.
            }
            if(!s.RoomTimerKnown)
            {Status="等待游戏地图倒计时";return Finish(s,false);}
            Status="地图剩余 "+TimeSpan.FromSeconds(Math.Max(0,s.RoomRemainingSeconds)).ToString(@"hh\:mm\:ss")+"，剩余 5 分钟时往返换图";
            if(s.RoomRemainingSeconds>LeadSeconds(s.RoomDurationSeconds))return Finish(s,false);
            if(!s.Ready || s.MapGroupId<=0)return Finish(s,false);
            if(!s.MapUnlocked)return Fail(s,"当前钓场未在本账号解锁，无法自动返回；请手动选择钓场");
            if(!clear || s.State!="None")
            {Status="地图即将到期，完成本竿、结算及场景切换后往返";return Finish(s,false);}
            OriginalMap=s.MapGroupId;originalStart=s.RoomStartTicks;sentAt=now;stage=1;
            action=FishingAction.TravelLobby;Status="地图即将到期，前往钓鱼大厅后返回原钓场";
            return Finish(s,true);
        }
    }
}
