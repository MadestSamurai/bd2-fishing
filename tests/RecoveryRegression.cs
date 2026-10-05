using BD2Fishing;
internal static class RecoveryRegression
{
 public static void Run(Action<bool,string> check)
 {
  long sent=DateTime.UtcNow.Ticks;
  var item=new FishingSaleItem{InvenIndex=1,FishId=10,Size=12,Grade=1,HasFishTable=true,HasSaleEntry=true};
  var sale=new FishingSaleProgress();sale.Begin(FishingSalePlan.Build(new[]{item}),sent);
  sale.Observe(false,false,Array.Empty<long>(),sent+TimeSpan.FromMinutes(2).Ticks);
  check(sale.Pending&&sale.Error==""&&sale.NeedsRefresh(sent+TimeSpan.FromMinutes(2).Ticks),"missing sale response becomes refresh status");
  check(!sale.Reconcile(sent,true)&&!sale.Reconcile(sent+1,false)&&sale.Pending,"stale or native-busy inventory cannot settle unknown sale");
  check(sale.Reconcile(sent+1,true)&&!sale.Pending&&sale.SoldCount==0,"fresh inventory allows replan without invented sale success");
  check(!sale.Reconcile(sent+2,true),"same recovery is idempotent");
  sale.Begin(FishingSalePlan.Build(new[]{item}),sent+10);check(sale.Pending,"fresh sale planning remains available");
  var unlock=new FishingUnlockProgress();unlock.Begin(42,sent);
  unlock.Observe(false,false,0,true,true,sent+TimeSpan.FromMinutes(2).Ticks);
  check(unlock.Pending&&unlock.Error==""&&!unlock.Reconcile(sent,true),"unlock timeout keeps controls usable and requires fresh inventory");
  check(unlock.Reconcile(sent+1,true)&&!unlock.Completed&&unlock.UnlockedCount==0,"unknown unlock does not invent success");
  var bait=new FishingBaitProgress();bait.Begin(12,5,8,sent);bait.Observe(false,false,false,0,false,sent+TimeSpan.FromMinutes(2).Ticks);
  check(bait.Pending&&bait.Error==""&&!bait.Reconcile(sent+1,false),"bait timeout does not fault or repeat while native busy");
  check(bait.Reconcile(sent+1,true)&&!bait.Pending&&bait.UsedCount==0,"unknown bait resolved to status without false use count");
  foreach(string phase in new[]{"None","Casting","BiteDetected","Caught"}){
   var waiting=new FishingPolicy();var frame=new FishingSnapshot{ProcessId=1,Ready=true,CanCast=true,State=phase};
   waiting.Next(frame,C(sent),sent);waiting.Next(frame,C(sent+TimeSpan.FromSeconds(2).Ticks),sent+TimeSpan.FromSeconds(2).Ticks);
   long later=sent+TimeSpan.FromMinutes(2).Ticks;waiting.Next(frame,C(later),later);
   check(waiting.Fault=="","missing phase confirmation remains status: "+phase);
   frame.State="Fighting";frame.WeakHit=true;
   check(waiting.Next(frame,C(later+TimeSpan.TicksPerSecond),later+TimeSpan.TicksPerSecond)==FishingAction.FightClick,"phase changes resume without a restart: "+phase);
  }
  var p=new FishingPolicy();var s=new FishingSnapshot{ProcessId=1,Ready=true,State="Casting",CastRunning=true,Gauge=.1};
  FishingControl C(long t)=>new(){OwnerId="same",ProcessId=1,Enabled=true,UntilUtcTicks=t+TimeSpan.FromSeconds(10).Ticks};
  p.Next(s,C(sent),sent);s.NetworkPending=true;
  for(int i=1;i<=600;i++){long t=sent+TimeSpan.FromSeconds(i).Ticks;p.Next(s,C(t),t);}
  check(p.Fault=="","ten-minute network wait does not expire fishing phase");
  s.NetworkPending=false;s.Gauge=.95;
  check(p.Next(s,C(sent+TimeSpan.FromSeconds(601).Ticks),sent+TimeSpan.FromSeconds(601).Ticks)==FishingAction.CastRelease&&p.Fault=="","same run continues owned phase after long transport wait");
  s.NetworkPending=true;p.Next(s,C(sent+TimeSpan.FromSeconds(602).Ticks),sent+TimeSpan.FromSeconds(602).Ticks);
  p.Next(s,new FishingControl(),sent+TimeSpan.FromSeconds(603).Ticks);check(!p.Holding&&p.Reason=="自动钓鱼已停止","manual stop remains effective during outage");
 }
}