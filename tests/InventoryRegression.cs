using BD2Fishing;
using BD2Fishing.Runtime;
using Proto.Net;
static class InventoryRegression {
 public static void Run(Action<bool,string> check){
  long now=DateTime.UtcNow.Ticks;InventoryHost host=null!;FishingInventory inventory=null!;
  FishingSnapshot s=null!;FishingControl c=null!;FishingPolicy policy=null!;
  void Reset(int count=230,bool locked=false,int species=1){
   host=new(){FishingFishInvenSlot=count};FishingBindings.Inventory=host;
   host.Fish=Enumerable.Range(1,count).Select(i=>new FishingFishDBInfo{InvenIndex=i,Id=(i-1)%species+1,Size=i,IsLock=locked}).ToList();
   inventory=new(_=>{});policy=new();
   s=new(){ProcessId=123,Ready=true,CanCast=true,State="None",BagFull=true};
   c=new(){ProcessId=123,OwnerId="sale-cycle",Enabled=true,AutoSell=true,NextCastMilliseconds=0,Retention=new(){KeepLegendary=false,KeepLocked=false}};
  }
  void Fill(bool renew=true,int milliseconds=100){
   now+=TimeSpan.FromMilliseconds(milliseconds).Ticks;if(renew)c.UntilUtcTicks=now+TimeSpan.FromSeconds(10).Ticks;
   s.Error="";s.BagFull=host.Fish.Count>=host.FishingFishInvenSlot;inventory.Fill(s,now,c);
  }
  FishingAction Step(bool renew=true,int milliseconds=100){
   Fill(renew,milliseconds);var action=policy.Next(s,c,now);
   if(action==FishingAction.SellFish)inventory.Sell(now,s,c);
   return action;
  }
  void Reply(bool accepted=true,bool update=true){
   s.SaleReplySerial++;s.SaleReplyAccepted=accepted;
   if(accepted&&update){var ids=host.Sales.Last().ToHashSet();host.Fish.RemoveAll(f=>ids.Contains(f.InvenIndex));}
  }
  // Inventory display must stay current while the game never exposes an idle frame.
  Reset(400);host.FishGrade=1;host.Fish=host.Fish.Take(2).ToList();
  c.Retention.SizeLegendary=false;c.Retention.SizeLocked=false;
  c.Retention.SizeRules=new[]{new FishingSizeRule{FishId=1,Mode=FishingSizeMode.Both}};
  Fill();check(s.ProtectedFishCount==2&&s.SellableCount==0,"ordinary unlocked MIN/MAX retained with category options off");
  foreach(string activeState in new[]{"Casting","WaitingForBite","BiteDetected","Fighting","Caught"}){
   s.State=activeState;int next=host.Fish.Count+1;
   host.Fish.Add(new(){InvenIndex=1000+next,Id=next,Size=next*10});
   c.Retention.SizeRules=c.Retention.SizeRules.Concat(new[]{new FishingSizeRule{FishId=next,Mode=FishingSizeMode.Both}}).ToArray();
   Fill(milliseconds:600);
   check(s.ProtectedFishCount==host.Fish.Count&&s.FishSpecies.Length==host.Fish.Select(f=>f.Id).Distinct().Count(),"display refreshes during active state: "+activeState);
   check(!inventory.Sell(now,s,c)&&host.Sales.Count==0,"active read-only refresh never authorizes sale: "+activeState);
  }
  s.State="Fighting";int beforeCount=s.BagCount;host.Fish.Add(new(){InvenIndex=9999,Id=1,Size=1});
  Fill(milliseconds:600);check(s.BagCount==beforeCount+1&&s.SellableCount==1&&s.FishSpecies.Single(f=>f.FishId==1).Count==3,"new catch updates sale count without idle frame");
  c.Retention.SizeRules=Array.Empty<FishingSizeRule>();Fill(milliseconds:600);
  check(s.ProtectedFishCount==0&&s.SellableCount==host.Fish.Count,"changed size settings refresh during fishing");
  s.State="None";
  Reset();check(Step()==FishingAction.SellFish&&host.Sales.Single().Length==100,"start bounded cleanup from full 230-fish bag");
  for(int i=0;i<10;i++)check(Step()==FishingAction.None&&host.Sales.Count==1,"wait receipt before next batch");
  Reply();check(Step()==FishingAction.SellFish&&!s.BagFull&&host.Sales.Count==2,"continue second batch after bag is no longer full");
  Reply();check(Step()==FishingAction.SellFish&&host.Sales.Last().Length==30,"continue final partial batch");
  Reply();check(Step()==FishingAction.CastPress&&!s.SaleActive&&s.SoldCount==230&&s.SaleStatus.Contains("230")&&s.SaleStatus.Contains("整理完成"),"resume only after confirmed full cleanup with cumulative status");
  check(host.Sales.SelectMany(x=>x).Distinct().Count()==230,"no repeated inventory IDs across batches");
  // A second full bag begins a new displayed cycle while the session total stays cumulative.
  host.Fish=Enumerable.Range(1000,230).Select(i=>new FishingFishDBInfo{InvenIndex=i,Id=1,Size=i}).ToList();
  for(int i=0;i<3;i++){check(Step(milliseconds:600)==FishingAction.SellFish,"second cleanup batch");Reply();}
  Fill();check(s.SoldCount==460&&s.SaleStatus.Contains("230"),"cycle count resets without resetting session total");
  // Match issue #3: 400 slots, multiple species, MIN+MAX retained, including native unlocks.
  foreach(int grade in new[]{1,2,4})foreach(bool locked in new[]{false,true}){
   Reset(400,locked,16);host.FishGrade=grade;c.Retention.SizeRules=Enumerable.Range(1,16).Select(id=>new FishingSizeRule{FishId=id,Mode=FishingSizeMode.Both}).ToArray();
   var kept=host.Fish.GroupBy(f=>f.Id).SelectMany(g=>new[]{g.MinBy(f=>f.Size)!.InvenIndex,g.MaxBy(f=>f.Size)!.InvenIndex}).Order().ToArray();
   int saleReplies=0,unlockReplies=0;FishingAction action=FishingAction.None;
   for(int i=0;i<1000;i++){
    action=Step();if(action==FishingAction.CastPress)break;
    if(host.Unlocks.Count>unlockReplies){long id=host.Unlocks[unlockReplies++];check(!kept.Contains(id),"never unlock an extremum");host.Fish.Single(f=>f.InvenIndex==id).IsLock=false;s.UnlockReplySerial++;s.UnlockReplyIndex=id;s.UnlockReplyAccepted=true;}
    if(host.Sales.Count>saleReplies){check(host.Sales.Last().All(id=>!host.Fish.Single(f=>f.InvenIndex==id).IsLock),"request contains only verified unlocked fish");saleReplies++;Reply();}
   }
   check(action==FishingAction.CastPress&&s.SoldCount==368,"400-slot cleanup completes without recatching to fill bag");
   check(host.Sales.Select(x=>x.Length).SequenceEqual(new[]{100,100,100,68}),"per-request bound retained through full cleanup");
   check(host.Fish.Select(f=>f.InvenIndex).Order().SequenceEqual(kept),"all species MIN and MAX survive all batches");
  }
  // Re-read changed locks/data between batches; do not recycle the original plan.
  Reset();Step();Reply();host.Fish.First().IsLock=true;c.Retention.KeepLocked=true;
  // This changes policy, so cancels the cycle rather than selling under old authorization.
  check(Step()!=FishingAction.SellFish&&host.Sales.Count==1&&!s.SaleActive,"retention change cancels later batches");
  Reset();c.Retention.KeepLocked=true;Step();Reply();host.Fish.First().IsLock=true;
  check(Step()==FishingAction.SellFish&&!host.Sales.Last().Contains(101),"fresh native lock is protected between batches");
  Reset();Step();Reply();host.MissingTable=true;
  check(Step()==FishingAction.CastPress&&s.ProtectedFishCount==130&&host.Sales.Count==1,"newly unreadable fish protected by whole-bag replanning");
  foreach(var gate in new[]{"stop","disable","owner","expired","pid","rules"}){
   Reset();Step();Reply();
   if(gate=="stop")c.Enabled=false;if(gate=="disable")c.AutoSell=false;if(gate=="owner")c.OwnerId="new-owner";
   if(gate=="expired")c.UntilUtcTicks=now-1;if(gate=="pid")c.ProcessId++;
   if(gate=="rules")c.Retention.KeepLegendary=true;
   s.Busy=gate=="scene";s.MapTravelBusy=gate=="travel";s.MapChangePending=gate=="daynight";
   Step(gate!="expired");check(host.Sales.Count==1&&!s.SaleActive,"cycle authorization revoked: "+gate);
  }
  foreach(var gate in new[]{"unready","scene","travel","daynight"}){
   Reset();Step();Reply();
   s.Ready=gate!="unready";s.Busy=gate=="scene";s.MapTravelBusy=gate=="travel";s.MapChangePending=gate=="daynight";
   Step();check(host.Sales.Count==1&&s.SaleActive&&s.Error==""&&policy.Fault=="","transient context waits without revoking cleanup: "+gate);
   s.Ready=true;s.Busy=s.MapTravelBusy=s.MapChangePending=false;
   check(Step()==FishingAction.SellFish&&host.Sales.Count==2,"same cleanup continues from verified bag after context returns: "+gate);
  }
  foreach(var failure in new[]{"rejected","unchanged","protected-missing","timeout"}){
   Reset();Step();
   if(failure=="timeout")Fill(true,31000);
   else {Reply(failure!="rejected",failure!="unchanged");if(failure=="protected-missing")host.Fish.RemoveAt(0);Fill();}
   check((failure=="timeout"?s.Error.Length==0&&s.SalePending:s.Error.Length>0)&&s.SoldCount==0,"uncertain/rejected batch not counted: "+failure);
   check(Step()!=FishingAction.SellFish&&host.Sales.Count==1,"error never sends next batch: "+failure);
   if(failure=="timeout"){
    inventory.AcknowledgeError();check(!inventory.Sell(now,s,c),"acknowledge cannot resend unresolved batch");
    c.Enabled=false;Reply();Fill();check(!s.SaleActive&&s.SoldCount==100&&host.Sales.Count==1,"late confirmation after stop only accounts prior sale");
   }
  }
  Reset();Step();Reply();Fill();
  foreach(var gate in new[]{"modal","network","bait","result","level"}){
   s.BlockReason=gate=="modal"?"popup":"";s.NetworkPending=gate=="network";s.BaitPending=gate=="bait";s.ResultPopup=gate=="result";s.LevelPopup=gate=="level";
   check(!inventory.Sell(now,s,c)&&host.Sales.Count==1,"sender rechecks continuation gate: "+gate);
  }
  Reset();Step();Reply();Fill();c.AutoMapRenewal=true;s.RoomTimerKnown=true;s.RoomDurationSeconds=21600;s.RoomRemainingSeconds=20;s.MapGroupId=3;s.MapUnlocked=true;
  check(policy.Next(s,c,now)==FishingAction.SellFish,"room renewal cannot interrupt confirmed sale batches");
  c.AutoApproach=true;s.CanCast=false;check(!FishingApproach.CanRun(s,c,now),"shoreline approach cannot interrupt cleanup");
  Reset();host.Fish.RemoveAt(0);Fill();check(!inventory.Sell(now,s,c)&&host.Sales.Count==0,"non-full bag alone cannot start unsolicited cleanup");
 }
}
