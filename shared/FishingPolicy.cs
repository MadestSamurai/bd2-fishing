using System;
namespace BD2Fishing
{
    // Pure decision state: game input and network handlers remain owned by the client.
    public sealed class FishingPolicy
    {
        private string owner = "", state = "";
        private long entered, lastInput;
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
            if (!s.ResultPopup && !s.LevelPopup) closedPopup=0;
            if (s.Error.Length>0) Fail(s.Error);
            if (s.NetworkPending && s.NetworkWaitSeconds>30) Fail("网络响应超过 30 秒，请核对游戏提示后停止并重新开启");
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
                    if(s.BagFull)
                    {
                        if(!c.AutoSell){Reason="鱼背包已满，自动出售未开启";break;}
                        if(!s.SaleReady){Reason=s.SaleStatus;break;}
                        if(s.SellableCount<=0){Reason="背包已满，仅剩保留鱼；请手动整理或扩容";break;}
                        Reason="背包已满，出售普通／稀有鱼，保留传说和锁定鱼";
                        lastInput=now;return FishingAction.SellFish;
                    }
                    if(!s.CanCast){Reason="请移动到可钓位置并面向水面";break;}
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
                    if(castSent && now-lastInput>TimeSpan.FromSeconds(5).Ticks) Fail("抛竿输入未生效，请检查游戏界面");
                    break;
                case "Casting":
                    Reason=s.CastRunning?"抛竿蓄力":"等待抛竿确认";
                    if(s.CastRunning && !released && s.Gauge>=c.CastGauge){released=true;ownsCharge=false;lastInput=now;return FishingAction.CastRelease;}
                    if(now-entered>TimeSpan.FromSeconds(15).Ticks) Fail("抛竿阶段未推进，请检查网络或游戏界面");
                    break;
                case "WaitingForBite": Reason="等待鱼咬钩";break;
                case "BiteDetected":
                    Reason="已咬钩，等待提竿响应";
                    if(!hookSent){hookSent=true;lastInput=now;return FishingAction.Hook;}
                    if(now-lastInput>TimeSpan.FromSeconds(30).Ticks) Fail("提竿未返回，请检查游戏网络提示");
                    break;
                case "Pause": Reason="等待下一次收线";break;
                case "Caught": Reason="等待收获结算与弹窗";if(now-entered>TimeSpan.FromSeconds(45).Ticks)Fail("收获未完成，请核对游戏提示");break;
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
