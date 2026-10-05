using System;
namespace BD2Fishing
{
    // Pure decision state: game input and network handlers remain owned by the client.
    public sealed class FishingPolicy
    {
        private string owner = "", state = "";
        private long entered, lastInput, lastObserved;
        private int networkRecoveries;
        private bool hookSent, castSent, released, ownsCharge;
        private int closedPopup;
        private readonly FishingMapRenewal mapRenewal = new FishingMapRenewal();
        public string MapRenewalStatus => mapRenewal.Status;
        public int MapRenewals => mapRenewal.Completed;
        public int ReturnMapGroupId => mapRenewal.OriginalMap;
        public bool Holding {get;private set;}
        public string Fault {get;private set;} = "";
        public string Reason {get;private set;} = "未开启";
        public void Fail(string error) { Fault = error; Reason = error; }
        public FishingAction Next(FishingSnapshot s, FishingControl c, long now, bool holdPhase = false)
        {
            if (c == null || !c.Valid(now,s.ProcessId))
            {
                mapRenewal.Cancel(); owner = ""; Reason = "自动钓鱼已停止";
                if (Holding) {Holding=false; return FishingAction.HoldRelease;}
                // Finish only the charging press we own, without fabricating a cast grade.
                if (ownsCharge && !released && s.State=="Casting" && s.CastRunning) {ownsCharge=false;released=true; return FishingAction.CastRelease;}
                return FishingAction.None;
            }
            if (owner != c.OwnerId)
            {
                mapRenewal.Cancel(); owner=c.OwnerId; state=""; hookSent=castSent=released=false; closedPopup=0; lastInput=0; Fault="";
            }
            if (s.State != state) {if(s.State!="Casting")ownsCharge=false;state=s.State;entered=now;hookSent=castSent=released=false;}
            long elapsed=lastObserved==0?0:Math.Max(0,now-lastObserved);lastObserved=now;
            if(networkRecoveries!=s.NetworkRecoveries){
                networkRecoveries=s.NetworkRecoveries;entered=now;lastInput=now;
                if(s.State=="None")castSent=released=hookSent=false;
            }
            if(s.NetworkPending){
                entered+=elapsed;lastInput=now;mapRenewal.PauseClock(elapsed);
                Reason=s.NetworkWaitSeconds>=30?"等待游戏网络恢复，恢复后自动继续":"等待游戏服务器响应";
                if(Holding&&(!s.HoldActive||s.HoldCompleted||!s.HoldTracking||s.State!="Fighting"||s.NetworkWaitSeconds>=30||s.HoldTargetHit||!s.HoldInside)){Holding=false;return FishingAction.HoldRelease;}
                return FishingAction.None;
            }
            if (!s.ResultPopup && !s.LevelPopup) closedPopup=0;
            if (s.Error.Length>0) Fail(s.Error);
            // A transport wait is recoverable state, not a latched policy failure.
            if (!holdPhase && Fault.Length==0 && mapRenewal.Next(s,c,now,out var travel))
            {
                if(mapRenewal.Fault.Length>0)Fail(mapRenewal.Fault);
                Reason=mapRenewal.Status;
                if(Holding){Holding=false;return FishingAction.HoldRelease;}
                return travel;
            }
            if (Fault.Length>0 || !s.Ready || s.Busy || s.MapTravelBusy || s.BlockReason.Length>0)
            {
                Reason=Fault.Length>0?Fault:s.BlockReason.Length>0?s.BlockReason:!s.Ready?"请进入钓鱼地点并面向可钓区域":"等待场景切换";
                if(Holding) {Holding=false;return FishingAction.HoldRelease;}
                return FishingAction.None;
            }
            if (Holding && (!s.HoldActive || s.HoldCompleted || !s.HoldTracking || s.State!="Fighting"))
            {Holding=false;return FishingAction.HoldRelease;}
            if (holdPhase)
            {
                if (Holding) {Reason="长按收线中"; if(s.HoldTargetHit || !s.HoldInside){Holding=false;return FishingAction.HoldRelease;}return FishingAction.None;}
                if(s.State=="Fighting" && s.HoldActive && !s.HoldCompleted && !s.HoldTracking && !s.NetworkPending && !s.Freeze && (s.HoldStartHit || s.HoldEndHit))
                {Holding=true;Reason="开始长按收线";return FishingAction.HoldPress;}
                return FishingAction.None;
            }
            if (s.BaitPending) {Reason="等待鱼饵回执、扣减和增益生效";return FishingAction.None;}
            if (s.SalePending) {Reason="等待出售回执和背包确认";return FishingAction.None;}
            if (s.NetworkPending) {Reason="等待游戏服务器响应";return FishingAction.None;}
            if (now-lastInput < TimeSpan.FromMilliseconds(80).Ticks) return FishingAction.None;
            if (s.ResultPopup || s.LevelPopup)
            {
                Reason=s.ResultPopup?"确认收获":"确认钓鱼升级";
                if(s.CanClosePopup && (s.PopupId!=closedPopup || now-lastInput>TimeSpan.FromSeconds(2).Ticks))
                {closedPopup=s.PopupId;lastInput=now;return FishingAction.ClosePopup;}
                return FishingAction.None;
            }
            switch(s.State)
            {
                case "None":
                    if(s.MapChangePending){Reason="本竿已结束，等待昼夜切换";break;}
                    if(s.BagFull || s.SaleActive)
                    {
                        if(!c.AutoSell){Reason="鱼背包已满，自动出售未开启";break;}
                        if(!s.SaleReady){Reason=s.SaleStatus;break;}
                        if(s.SellableCount<=0){Reason="背包已满，没有符合当前设置的可售鱼；请调整保留选项、手动整理或扩容";break;}
                        Reason=s.SaleActive?"继续整理鱼背包，按保留规则分批出售":"背包已满，按保留规则整理并出售鱼";
                        lastInput=now;return FishingAction.SellFish;
                    }
                    if(!s.CanCast){Reason=c.AutoApproach?"自动前往可钓区域":"请移动到可钓位置并面向水面";if(FishingApproach.CanRun(s,c,now)){lastInput=now;return FishingAction.ApproachWater;}break;}
                    Reason="准备下一竿";
                    if(!castSent && now-entered>=TimeSpan.FromMilliseconds(c.NextCastMilliseconds).Ticks)
                    {
                        if(c.AutoBait)
                        {
                            if(!s.BaitReady){Reason=s.BaitStatus;break;}
                            if(!s.BaitActive && s.BaitCount>0)
                            {
                                if(!s.BaitCanUse){Reason="等待游戏允许使用鱼饵";break;}
                                Reason="增益未生效，使用一份鱼饵";lastInput=now;return FishingAction.UseBait;
                            }
                            if(!s.BaitActive && s.BaitCount==0)Reason="鱼饵用尽，继续普通钓鱼";
                        }
                        castSent=true;ownsCharge=true;lastInput=now;return FishingAction.CastPress;
                    }
                    if(castSent && now-lastInput>TimeSpan.FromSeconds(5).Ticks) Reason="等待游戏进入抛竿状态，恢复后自动继续";
                    break;
                case "Casting":
                    Reason=s.CastRunning?"抛竿蓄力":"等待抛竿确认";
                    if(s.CastRunning && !released && s.Gauge>=c.CastGauge){released=true;ownsCharge=false;lastInput=now;return FishingAction.CastRelease;}
                    if(now-entered>TimeSpan.FromSeconds(15).Ticks) Reason="等待游戏完成抛竿，恢复后自动继续";
                    break;
                case "WaitingForBite": Reason="等待鱼咬钩";break;
                case "BiteDetected":
                    Reason="已咬钩，等待提竿响应";
                    if(!hookSent){hookSent=true;lastInput=now;return FishingAction.Hook;}
                    if(now-lastInput>TimeSpan.FromSeconds(30).Ticks) Reason="等待游戏完成提竿，恢复后自动继续";
                    break;
                case "Pause": Reason="等待下一次收线";break;
                case "Caught": Reason="等待收获结算与弹窗";if(now-entered>TimeSpan.FromSeconds(45).Ticks)Reason="等待游戏完成收获结算，恢复后自动继续";break;
                case "Auto": Reason="请先关闭游戏内自动钓鱼";break;
                case "Fighting":
                    if(s.Freeze){Reason="解除冰冻";lastInput=now;return FishingAction.FightClick;}
                    if(Holding || (s.HoldActive && s.HoldInside)){Reason="等待长按时机";break;}
                    if(s.Interaction=="useful"){Reason="清除鱼技能目标";lastInput=now;return FishingAction.FightClick;}
                    if(s.Interaction.Length>0){Reason="避开陷阱、护盾或正在消失的目标";break;}
                    if(s.BlockedHit){Reason="等待可命中空隙";break;}
                    if(s.WeakHit || (s.NormalHit && (!c.PreferWeak || !CanWaitForWeak(s))))
                    {Reason=s.WeakHit?"弱点命中":"普通命中";lastInput=now;return FishingAction.FightClick;}
                    Reason="等待有效命中区";break;
            }
            return FishingAction.None;
        }
        public static bool CanWaitForWeak(FishingSnapshot s)
        {
            if(s.WeakWidth<=0 || Math.Abs(s.NeedleVelocity)<1 || s.TimeRemaining<2) return false;
            double seconds=(s.WeakPosition-s.NeedlePosition)/s.NeedleVelocity;
            double life=s.ShrinkSpeed>0?s.NormalWidth/s.ShrinkSpeed:1;
            return seconds>0 && seconds<Math.Min(.35,life*.6);
        }
    }
}
