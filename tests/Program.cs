using BD2Fishing;
int assertions=0;void Check(bool ok,string msg){assertions++;if(!ok)throw new Exception(msg);}
long time=DateTime.UtcNow.Ticks;
FishingSnapshot S(string state="None")=>new(){ProcessId=123,Ready=true,State=state,CanCast=true,TimeRemaining=30};
FishingControl C()=>new(){OwnerId="test",ProcessId=123,Enabled=true,UntilUtcTicks=time+TimeSpan.FromSeconds(10).Ticks,NextCastMilliseconds=1000};
FishingAction Step(FishingPolicy p,FishingSnapshot s,int ms=100,bool hold=false){time+=TimeSpan.FromMilliseconds(ms).Ticks;return p.Next(s,C(),time,hold);}
var p=new FishingPolicy();var s=S();Check(Step(p,s)==FishingAction.None,"wait next cast");Check(Step(p,s,1000)==FishingAction.CastPress,"cast press");
Check(Step(p,s,100)==FishingAction.None,"do not duplicate cast before transition");
s.State="Casting";s.CastRunning=true;s.Gauge=.5;Check(Step(p,s)==FishingAction.None,"wait real gauge");s.Gauge=.92;Check(Step(p,s)==FishingAction.CastRelease,"release real gauge");Check(Step(p,s)==FishingAction.None,"release once");
s.State="WaitingForBite";Check(Step(p,s)==FishingAction.None,"never hook early");s.State="BiteDetected";Check(Step(p,s)==FishingAction.Hook,"hook on bite");
for(int i=0;i<80;i++)Check(Step(p,s,100)==FishingAction.None,"one request throughout delayed response");
s.State="Fighting";s.WeakHit=true;s.NetworkPending=true;s.NetworkWaitSeconds=2;Check(Step(p,s)==FishingAction.None,"wait stamina response");s.NetworkPending=false;Check(Step(p,s)==FishingAction.FightClick,"weak hit");
s.State="Pause";Check(Step(p,s)==FishingAction.None,"respect wave pause");s.State="Caught";Check(Step(p,s)==FishingAction.None,"wait reward popup");s.ResultPopup=true;s.CanClosePopup=true;s.PopupId=4;Check(Step(p,s)==FishingAction.ClosePopup,"close earned reward normally");Check(Step(p,s)==FishingAction.None,"no duplicate popup close");
s.State="None";s.ResultPopup=false;s.LevelPopup=true;s.PopupId=5;Check(Step(p,s)==FishingAction.ClosePopup,"close level up before cast");s.LevelPopup=false;Check(Step(p,s,1500)==FishingAction.CastPress,"next complete cycle");
p=new();s=S("Fighting");s.Freeze=true;Check(Step(p,s)==FishingAction.FightClick,"freeze normal input");s.Freeze=false;s.Interaction="useful";Check(Step(p,s)==FishingAction.FightClick,"useful skill target");
foreach(var interaction in new[]{"hazard","consumed"}){s.Interaction=interaction;s.WeakHit=true;Check(Step(p,s)==FishingAction.None,"no trap/shield/disappearing target click");}
s.Interaction="";s.BlockedHit=true;Check(Step(p,s)==FishingAction.None,"no blocked hit");s.BlockedHit=false;s.WeakHit=false;s.NormalHit=true;s.WeakWidth=0;Check(Step(p,s)==FishingAction.FightClick,"no infinite wait for zero weak zone");
s.WeakWidth=20;s.NeedleVelocity=100;s.NeedlePosition=0;s.WeakPosition=10;s.NormalWidth=100;s.ShrinkSpeed=20;Check(Step(p,s)==FishingAction.None,"wait nearby approaching weak zone");s.NeedleVelocity=-100;Check(Step(p,s)==FishingAction.FightClick,"do not wait weak target moving away");
p=new();s=S("Fighting");s.HoldActive=true;s.HoldInside=true;s.HoldStartHit=true;Check(Step(p,s,100,true)==FishingAction.HoldPress,"hold start left");s.HoldStartHit=false;s.HoldTracking=true;Check(Step(p,s,100,true)==FishingAction.None,"keep hold");s.HoldTargetHit=true;Check(Step(p,s,100,true)==FishingAction.HoldRelease,"release on correct end before game update");
p=new();s=S("Fighting");s.HoldActive=true;s.HoldInside=true;s.HoldEndHit=true;Check(Step(p,s,100,true)==FishingAction.HoldPress,"hold start right");Check(p.Next(s,new FishingControl(),time,true)==FishingAction.HoldRelease,"disabled releases owned hold");Check(!p.Holding,"hold ownership cleared");
p=new();s=S();Step(p,s);Step(p,s,1000);s.State="Casting";s.CastRunning=true;s.Gauge=.1;Step(p,s);Check(p.Next(s,new FishingControl(),time)==FishingAction.CastRelease,"stop releases charging owned by automation");Check(p.Next(s,new FishingControl(),time)==FishingAction.None,"stop does not repeat release");
p=new();s=S("Fighting");s.WeakHit=true;s.NetworkPending=true;s.NetworkWaitSeconds=31;Check(Step(p,s)==FishingAction.None&&p.Fault.Length>0,"timeout latches fault");s.NetworkPending=false;Check(Step(p,s)==FishingAction.None,"fault cannot silently resume");var fresh=C();fresh.OwnerId="explicit-restart";Check(p.Next(s,fresh,time+1000000)==FishingAction.FightClick,"explicit owner may resume after resolved state");
foreach(var mode in new[]{"Auto","WaitingForBite","Pause","Unavailable"}){p=new();s=S(mode);s.WeakHit=true;Check(Step(p,s)==FishingAction.None,"no inputs outside manual fight: "+mode);}
p=new();s=S();s.BagFull=true;Step(p,s);Check(Step(p,s,2000)==FishingAction.None,"bag full blocks cast");s.BagFull=false;Check(Step(p,s)==FishingAction.CastPress,"continue after user clears bag");
foreach(var mode in new[]{"modal","scene","unready"}){p=new();s=S("Fighting");s.WeakHit=true;if(mode=="modal")s.BlockReason="popup";if(mode=="scene")s.Busy=true;if(mode=="unready")s.Ready=false;Check(Step(p,s)==FishingAction.None,"gated "+mode);}
var expired=C();expired.UntilUtcTicks=time;Check(!expired.Valid(time,123),"lease expires");Check(!C().Valid(time,999),"pid fence");var bad=C();bad.UntilUtcTicks=time+TimeSpan.FromDays(1).Ticks;Check(!bad.Valid(time,123),"reject long stale lease");Check(!FishingControl.ValidSettings(1000,double.NaN),"NaN rejected");
var data=Path.Combine(Path.Combine(AppContext.BaseDirectory,"test-data"),"test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
using(var link=new FishingControlLink(data)){link.Configure(new(){NextCastMilliseconds=321,CastGauge=.8});link.Start(123);var v=FishingJson.Read<FishingControl>(Path.Combine(data,"control.json"))!;Check(v.Valid(DateTime.UtcNow.Ticks,123)&&v.NextCastMilliseconds==321,"atomic settings lease");link.Stop();Thread.Sleep(650);Check(!FishingJson.Read<FishingControl>(Path.Combine(data,"control.json"))!.Enabled,"late renewal cannot rearm stopped run");link.Start(123);}
Check(!FishingJson.Read<FishingControl>(Path.Combine(data,"control.json"))!.Enabled,"dispose revokes");
p=new();s=S("Fighting");s.HoldActive=true;s.HoldInside=true;s.HoldStartHit=true;Step(p,s,100,true);s.HoldTracking=true;s.HoldStartHit=false;s.Freeze=true;Check(Step(p,s)==FishingAction.FightClick,"freeze can be cleared without dropping ongoing hold");
var rng=new Random(73021);
for(int i=0;i<1000;i++){p=new();s=S("BiteDetected");Check(Step(p,s)==FishingAction.Hook,"random bite accepted");for(int k=0;k<rng.Next(2,15);k++)Check(Step(p,s,rng.Next(81,501))==FishingAction.None,"jitter cannot repeat hook");}
// Inventory selection and reply verification: never sell legendary/locked/unknown items.
FishingSaleItem Fish(long id,int grade=1,bool locked=false,bool hasTable=true,bool listed=true)=>new(){InvenIndex=id,FishId=(int)id+1000,Grade=grade,IsLocked=locked,HasFishTable=hasTable,HasSaleEntry=listed};
var bag=new[]{Fish(1),Fish(2,2),Fish(3,4),Fish(4,4),Fish(5,1,true),Fish(6,3),Fish(7,0),Fish(8,2,false,false),Fish(9,2,false,true,false)};
var plan=FishingSalePlan.Build(bag);
Check(plan.Items.Select(f=>f.InvenIndex).SequenceEqual(new long[]{1,2}),"only unlocked known normal and rare fish selected");
Check(plan.Protected==7 && plan.KeepIds.Length==7,"all legendary copies and unknown fish retained");
var large=FishingSalePlan.Build(Enumerable.Range(1,230).Select(i=>Fish(i)));
Check(large.Items.Length==100 && large.KeepIds.Length==130 && large.Sellable==230,"bounded batch retains all unsubmitted IDs");
bool throws=false;try{FishingSalePlan.Build(new[]{Fish(1),Fish(1,4)});}catch(InvalidOperationException){throws=true;}Check(throws,"duplicate ID cannot sell protected alias");
throws=false;try{FishingSalePlan.Build(new[]{Fish(0)});}catch(InvalidOperationException){throws=true;}Check(throws,"invalid inventory IDs rejected");
var sale=new FishingSaleProgress();sale.Begin(plan,time);
Check(sale.Pending && sale.SoldCount==0,"sending does not count as sale success");
for(int i=0;i<100;i++)sale.Observe(false,false,plan.KeepIds,time+TimeSpan.FromMilliseconds(i*100).Ticks);
Check(sale.Pending && sale.SoldCount==0,"local disappearance without response is not success");
throws=false;try{sale.Begin(plan,time);}catch(InvalidOperationException){throws=true;}Check(throws,"pending sale cannot resend");
sale.Observe(true,true,plan.KeepIds,time+TimeSpan.FromSeconds(11).Ticks);
Check(!sale.Pending && sale.SoldCount==2 && sale.Error=="","receipt and inventory agree");
sale.Observe(true,true,plan.KeepIds,time);Check(sale.SoldCount==2,"duplicate reply cannot double count");
sale=new();sale.Begin(plan,time);sale.Observe(true,true,bag.Select(f=>f.InvenIndex),time);Check(sale.SoldCount==0 && sale.Error.Length>0,"successful response with unchanged bag pauses");
sale=new();sale.Begin(plan,time);sale.Observe(true,true,plan.KeepIds.Where(id=>id!=3),time);Check(sale.SoldCount==0 && sale.Error.Length>0,"missing protected fish is detected");
sale=new();sale.Begin(plan,time);sale.Observe(true,false,bag.Select(f=>f.InvenIndex),time);Check(!sale.Pending && sale.Error.Length>0 && sale.SoldCount==0,"failed response is not sold");
sale=new();sale.Begin(plan,time);sale.Observe(false,false,plan.KeepIds,time+TimeSpan.FromSeconds(31).Ticks);sale.AcknowledgeError();Check(sale.Pending && sale.Error.Length>0,"uncertain timeout remains in flight after acknowledge");
sale.Observe(true,true,plan.KeepIds,time+TimeSpan.FromSeconds(32).Ticks);Check(!sale.Pending && sale.SoldCount==2,"late receipt resolved without duplicate send");
var changed=FishingSalePlan.Build(new[]{Fish(100)});changed.Items[0].IsLocked=true;throws=false;try{new FishingSaleProgress().Begin(changed,time);}catch(InvalidOperationException){throws=true;}Check(throws,"last moment lock change rejected");
var auto=C();auto.AutoSell=true;p=new();s=S();s.BagFull=true;s.SaleReady=true;s.SellableCount=20;
Check(p.Next(s,auto,time)==FishingAction.SellFish,"full idle bag invokes sale");
s.SalePending=true;Check(p.Next(s,auto,time+1000000)==FishingAction.None,"sale pending prevents next cast or sale");
s.SalePending=false;s.BagFull=false;Check(p.Next(s,auto,time+TimeSpan.FromSeconds(2).Ticks)==FishingAction.CastPress,"verified free space resumes fishing");
foreach(var mode in new[]{"Fighting","Casting","Caught","Pause","BiteDetected","WaitingForBite","Auto"})
{p=new();s=S(mode);s.BagFull=true;s.SaleReady=true;s.SellableCount=50;Check(p.Next(s,auto,time)!=FishingAction.SellFish,"no sale during "+mode);}
foreach(var gate in new[]{"disabled","modal","reward","level","network","protected","unknown","scene"})
{
 p=new();s=S();s.BagFull=true;s.SaleReady=true;s.SellableCount=1;var cc=C();cc.AutoSell=gate!="disabled";
 if(gate=="modal")s.BlockReason="ItemInfoPopupUI";if(gate=="reward")s.ResultPopup=true;if(gate=="level")s.LevelPopup=true;
 if(gate=="network")s.NetworkPending=true;if(gate=="protected")s.SellableCount=0;if(gate=="unknown")s.SaleReady=false;if(gate=="scene")s.Busy=true;
 Check(p.Next(s,cc,time)!=FishingAction.SellFish,"sale respects gate "+gate);
}
Check(p.Next(s,new FishingControl(),time)!=FishingAction.SellFish,"stopped lease cannot sell");
var saleRng=new Random(51741);
for(int trial=0;trial<100;trial++)
{
 var input=Enumerable.Range(1,400).Select(i=>Fish(i,saleRng.Next(0,7),saleRng.Next(4)==0,saleRng.Next(9)!=0,saleRng.Next(7)!=0)).ToArray();
 var chosen=FishingSalePlan.Build(input);var selected=chosen.Items.Select(f=>f.InvenIndex).ToHashSet();
 Check(selected.Count<=100,"random sale stays bounded");
 foreach(var f in input)if(selected.Contains(f.InvenIndex))Check((f.Grade==1||f.Grade==2)&&!f.IsLocked&&f.HasFishTable&&f.HasSaleEntry,"random selected fish satisfies every protection");
 Check(chosen.KeepIds.Length+selected.Count==input.Length,"every non-sold fish retained");
}
// Automatic bait: wait for the native response, concrete stack and buff; never refill per cast.
Check(new FishingSettings().AutoBait && !new FishingControl().AutoBait,"UI opt-out default and fail-closed command default");
var oldSettings=System.Text.Json.JsonSerializer.Deserialize<FishingSettings>("{\"AutoSell\":false}")!;
Check(oldSettings.AutoBait && !oldSettings.AutoSell,"new bait default preserves older sale preference");
var bait=new FishingBaitProgress();bait.Begin(9123456789L,2,101,time);
Check(bait.Pending && bait.UsedCount==0,"bait send is not success");
for(int i=0;i<100;i++){bait.Observe(false,false,true,1,true,time+TimeSpan.FromMilliseconds(i*100).Ticks);Check(bait.Pending && bait.UsedCount==0,"no success without original bait callback");}
throws=false;try{bait.Begin(9123456789L,2,101,time);}catch(InvalidOperationException){throws=true;}Check(throws,"pending bait prevents repeated consumption");
bait.Observe(true,true,true,1,true,time);Check(!bait.Pending && bait.UsedCount==1 && bait.Error=="","bait callback decrement and buff confirmed");
bait.Observe(true,true,true,1,true,time);Check(bait.UsedCount==1,"duplicate bait callback not counted");
bait.Begin(9123456789L,1,101,time);bait.Observe(true,true,true,0,true,time);Check(bait.UsedCount==2 && !bait.Pending,"last bait stack removal confirmed");
foreach(var variant in new[]{"unchanged","overconsumed","no-buff","failed","invalid-stack"})
{
 bait=new();bait.Begin(1,3,101,time);bait.Observe(true,variant!="failed",true,variant=="unchanged"?3:variant=="overconsumed"?1:variant=="invalid-stack"?-1:2,variant!="no-buff",time);
 Check(!bait.Pending && bait.UsedCount==0 && bait.Error.Length>0,"bait failure halts "+variant);
 throws=false;try{bait.Begin(1,2,101,time);}catch(InvalidOperationException){throws=true;}Check(throws,"failed bait cannot retry without explicit restart "+variant);
}
bait=new();bait.Begin(1,3,101,time);bait.Observe(true,true,false,2,true,time);Check(bait.Pending,"wait for readable scene even with successful bait callback");
bait.Observe(true,true,true,2,true,time);Check(!bait.Pending && bait.UsedCount==1,"scene return resolves bait without resending");
bait=new();bait.Begin(1,3,101,time);bait.Observe(false,false,false,0,false,time+TimeSpan.FromSeconds(31).Ticks);bait.AcknowledgeError();
Check(bait.Pending && bait.Error.Length>0,"uncertain bait timeout cannot rearm");bait.Observe(true,true,true,2,true,time+TimeSpan.FromSeconds(32).Ticks);
Check(!bait.Pending && bait.UsedCount==1 && bait.Error.Length>0,"late success counted but pause remains until explicit restart");bait.AcknowledgeError();Check(bait.Error=="","resolved timeout can acknowledge");
foreach(var values in new[]{(0L,1,1),(1L,0,1),(1L,1,0)})
{throws=false;try{new FishingBaitProgress().Begin(values.Item1,values.Item2,values.Item3,time);}catch(InvalidOperationException){throws=true;}Check(throws,"invalid bait request rejected");}
FishingControl B(){var cc=C();cc.AutoBait=true;cc.NextCastMilliseconds=0;return cc;}
FishingSnapshot BS(string mode="None"){var ss=S(mode);ss.BaitReady=true;ss.BaitCanUse=true;ss.BaitCount=10;return ss;}
p=new();s=BS();Check(p.Next(s,B(),time)==FishingAction.UseBait,"fresh idle cast first uses bait");
s.BaitPending=true;Check(p.Next(s,B(),time+1000000)==FishingAction.None,"bait pending blocks repeated use and cast");
s.BaitPending=false;s.BaitCount=9;s.BaitActive=true;Check(p.Next(s,B(),time+2000000)==FishingAction.CastPress,"confirmed buff enables cast");
s.BaitActive=false;Check(p.Next(s,B(),time+3000000)==FishingAction.None,"do not bait while awaiting existing cast transition");
p=new();s=BS();s.BaitActive=true;
for(int cycle=0;cycle<20;cycle++)
{
 time+=TimeSpan.FromSeconds(2).Ticks;s.State="None";Check(p.Next(s,B(),time)==FishingAction.CastPress,"buff survives next cast without consumption");
 s.State="WaitingForBite";Check(p.Next(s,B(),time+1000000)==FishingAction.None,"buff no use waiting");
 s.State="Fighting";s.WeakHit=true;Check(p.Next(s,B(),time+2000000)==FishingAction.FightClick,"bait does not interrupt combat");
}
s.BaitActive=false;time+=TimeSpan.FromSeconds(2).Ticks;Check(p.Next(s,B(),time)==FishingAction.FightClick,"buff expiration mid-fight waits until next cast");s.State="None";Check(p.Next(s,B(),time+1000000)==FishingAction.UseBait,"expired buff replenished before next cast");
p=new();s=BS();s.BaitCount=0;Check(p.Next(s,B(),time)==FishingAction.CastPress,"empty bait continues ordinary fishing");
p=new();s=BS();s.BaitReady=false;Check(p.Next(s,B(),time)==FishingAction.None,"unknown bait waits instead of guessing inventory");var noBait=B();noBait.AutoBait=false;Check(p.Next(s,noBait,time+1000000)==FishingAction.CastPress,"disable bait permits ordinary cast with unknown bait data");
foreach(var mode in new[]{"Fighting","Casting","Caught","Pause","BiteDetected","WaitingForBite","Auto"})
{p=new();s=BS(mode);Check(p.Next(s,B(),time)!=FishingAction.UseBait,"bait never mid-stage "+mode);}
foreach(var gate in new[]{"disabled","modal","reward","level","network","sale","pending","button-disabled","unready","scene","wrong-pid","expired","bag-full"})
{
 p=new();s=BS();var cc=B();
 if(gate=="disabled")cc.AutoBait=false;if(gate=="modal")s.BlockReason="MessagePopupUI";if(gate=="reward")s.ResultPopup=true;if(gate=="level")s.LevelPopup=true;
 if(gate=="network")s.NetworkPending=true;if(gate=="sale")s.SalePending=true;if(gate=="pending")s.BaitPending=true;
 if(gate=="button-disabled")s.BaitCanUse=false;if(gate=="unready")s.Ready=false;if(gate=="scene")s.Busy=true;
 if(gate=="wrong-pid")cc.ProcessId++;if(gate=="expired")cc.UntilUtcTicks=time;if(gate=="bag-full")s.BagFull=true;
 Check(p.Next(s,cc,time)!=FishingAction.UseBait,"bait respects "+gate);
}
p=new();s=BS();s.BagFull=true;s.SaleReady=true;s.SellableCount=5;var both=B();both.AutoSell=true;
Check(p.Next(s,both,time)==FishingAction.SellFish,"full bag resolved before spending bait");s.BagFull=false;s.SalePending=true;Check(p.Next(s,both,time+1000000)==FishingAction.None,"wait sale before bait");
s.SalePending=false;Check(p.Next(s,both,time+2000000)==FishingAction.UseBait,"sale confirmation then bait");s.BaitPending=true;Check(p.Next(s,both,time+3000000)==FishingAction.None,"wait bait confirmation before cast");
s.BaitPending=false;s.BaitActive=true;Check(p.Next(s,both,time+4000000)==FishingAction.CastPress,"sale bait cast closed cycle");
// Day/night changes queue until EndFishing; pending must not block the very cycle they await.
p=new();s=S("Caught");s.MapChangePending=true;s.ResultPopup=true;s.CanClosePopup=true;s.PopupId=31;
Check(p.Next(s,C(),time)==FishingAction.ClosePopup,"queued day/night change permits earned result confirmation");
Check(p.Next(s,C(),time+1000000)==FishingAction.None,"queued transition does not duplicate close");
s.ResultPopup=false;s.State="None";Check(p.Next(s,C(),time+TimeSpan.FromSeconds(2).Ticks)==FishingAction.None,"do not start new cast ahead of queued transition");
Check(p.Reason.Contains("昼夜"),"transition wait explains pending cycle");
s.MapChangePending=false;s.Busy=true;Check(p.Next(s,C(),time+TimeSpan.FromSeconds(3).Ticks)==FishingAction.None,"active day/night change blocks input");
s.Ready=false;Check(p.Next(s,C(),time+TimeSpan.FromSeconds(4).Ticks)==FishingAction.None,"scene unloading stays paused");
s.Ready=true;s.Busy=false;Check(p.Next(s,C(),time+TimeSpan.FromSeconds(5).Ticks)==FishingAction.CastPress,"scene complete resumes same owner automatically");
Check(p.Fault=="","normal transition never requires manual restart");
foreach(var stage in new[]{"Casting","WaitingForBite","BiteDetected","Fighting","Pause","Caught"})
{
 p=new();s=S(stage);s.MapChangePending=true;s.CastRunning=true;s.Gauge=.95;s.WeakHit=true;
 if(stage=="Caught"){s.ResultPopup=true;s.CanClosePopup=true;s.PopupId=41;}
 var expected=stage=="Casting"?FishingAction.CastRelease:stage=="BiteDetected"?FishingAction.Hook:stage=="Fighting"?FishingAction.FightClick:stage=="Caught"?FishingAction.ClosePopup:FishingAction.None;
 Check(p.Next(s,C(),time)==expected,"pending transition completes existing phase "+stage);
 s.Busy=true;Check(new FishingPolicy().Next(s,C(),time)==FishingAction.None,"active transition blocks existing phase "+stage);
}
p=new();s=S("Fighting");s.MapChangePending=true;s.HoldActive=true;s.HoldInside=true;s.HoldStartHit=true;
Check(p.Next(s,C(),time,true)==FishingAction.HoldPress,"pending day change permits hold begin");s.HoldTracking=true;s.HoldStartHit=false;
Check(p.Next(s,C(),time+1000000,true)==FishingAction.None && p.Holding,"pending change preserves owned hold");s.HoldTargetHit=true;
Check(p.Next(s,C(),time+2000000,true)==FishingAction.HoldRelease,"pending change finishes hold at correct point");
foreach(var pendingFlag in new[]{false,true})foreach(var kind in new[]{"sale","bait","cast"})
{
 p=new();s=BS();s.MapChangePending=pendingFlag;var cc=B();cc.AutoSell=true;
 if(kind=="sale"){s.BagFull=true;s.SaleReady=true;s.SellableCount=50;}
 if(kind=="cast")s.BaitActive=true;
 var got=p.Next(s,cc,time);var expected=kind=="sale"?FishingAction.SellFish:kind=="bait"?FishingAction.UseBait:FishingAction.CastPress;
 Check(got==(pendingFlag?FishingAction.None:expected),"idle supplies and new cast respect transition "+kind);
}
p=new();s=S();s.MapChangePending=true;s.LevelPopup=true;s.CanClosePopup=true;s.PopupId=51;
Check(p.Next(s,C(),time)==FishingAction.ClosePopup,"level popup remains confirmable with queued transition");
foreach(var gate in new[]{"disabled","network","other-popup","animation"})
{
 p=new();s=S("Caught");s.MapChangePending=true;s.ResultPopup=true;s.CanClosePopup=gate!="animation";s.PopupId=61;var cc=C();
 if(gate=="disabled")cc.Enabled=false;if(gate=="network")s.NetworkPending=true;if(gate=="other-popup")s.BlockReason="MessagePopupUI";
 Check(p.Next(s,cc,time)==FishingAction.None,"transition fix retains settlement gate "+gate);
}
// A minute-long ordinary scene change with a renewed GUI lease must resume without timing out Caught.
p=new();s=S();s.Busy=true;
for(int i=0;i<70;i++){time+=TimeSpan.FromSeconds(1).Ticks;Check(p.Next(s,C(),time)==FishingAction.None && p.Fault=="","long scene loading does not end automation");}
s.Busy=false;Check(p.Next(s,C(),time)==FishingAction.CastPress,"long scene loading resumes without new owner");
// Room renewal: use real remaining time, settle first, and observe BOTH completed scene and fresh clock.
var mapTime=time;
FishingSnapshot MapSample()=>new(){ProcessId=123,Ready=true,State="None",CanCast=true,MapGroupId=3,MapUnlocked=true,RoomTimerKnown=true,RoomStartTicks=100000,RoomDurationSeconds=21600,RoomRemainingSeconds=299};
FishingControl MapControl()=>new(){OwnerId="map",ProcessId=123,Enabled=true,AutoMapRenewal=true,UntilUtcTicks=mapTime+TimeSpan.FromSeconds(10).Ticks};
var renewal=new FishingMapRenewal();var ms=MapSample();ms.RoomRemainingSeconds=301;
Check(!renewal.Next(ms,MapControl(),mapTime,out var ma)&&ma==FishingAction.None,"do not travel before five-minute threshold");
ms.RoomRemainingSeconds=300;ms.State="Fighting";Check(!renewal.Next(ms,MapControl(),mapTime,out ma),"due renewal keeps fighting");
ms.State="None";
foreach(var gate in new[]{"popup","network","sale","bait","daynight","busy","modal"}){
 ms=MapSample();ms.ResultPopup=gate=="popup";ms.NetworkPending=gate=="network";ms.SalePending=gate=="sale";ms.BaitPending=gate=="bait";ms.MapChangePending=gate=="daynight";ms.Busy=gate=="busy";ms.BlockReason=gate=="modal"?"modal":"";
 Check(!renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"settle before travel: "+gate);
}
ms=MapSample();Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.TravelLobby,"late connection travels based on game clock");
Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"outbound sent once");
ms.MapGroupId=0;ms.Ready=false;ms.MapTravelBusy=true;ms.LobbyReady=false;
mapTime+=TimeSpan.FromSeconds(3).Ticks;Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"eager map ID update is not loaded lobby");
ms.MapTravelBusy=false;ms.LobbyReady=true;renewal.Next(ms,MapControl(),mapTime,out ma);
mapTime+=TimeSpan.FromSeconds(2).Ticks;Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.TravelReturn&&renewal.OriginalMap==3,"return once after stable lobby even without field UI");
ms=MapSample();Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"old room timestamp must not mark success");
ms.RoomStartTicks++;ms.RoomRemainingSeconds=21600;ms.CanCast=false;Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&ms.MapRenewals==1,"new room completes renewal even when spawn is away from water");
ms.CanCast=true;Check(!renewal.Next(ms,MapControl(),mapTime,out ma)&&ms.MapRenewals==1,"confirmed new room does not travel again");
Check(!renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"normal fishing resumes without immediate repeat");
renewal=new();ms=MapSample();renewal.Next(ms,MapControl(),mapTime,out ma);mapTime+=TimeSpan.FromSeconds(121).Ticks;
Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&renewal.Fault.Length>0&&ma==FishingAction.None,"travel timeout never retries blindly");
renewal=new();renewal.Next(ms,MapControl(),mapTime,out ma);var disable=MapControl();disable.AutoMapRenewal=false;
Check(!renewal.Next(ms,disable,mapTime,out ma),"toggle cancels queued return");ms.MapGroupId=0;ms.LobbyReady=true;ms.Ready=false;
mapTime+=TimeSpan.FromSeconds(3).Ticks;Check(!renewal.Next(ms,MapControl(),mapTime,out ma)&&ma==FishingAction.None,"re-enable does not unexpectedly leave lobby");
renewal=new();ms=MapSample();renewal.Next(ms,MapControl(),mapTime,out ma);ms.MapGroupId=9;
Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&renewal.Fault.Length>0,"manual map intervention does not force return");
renewal=new();ms=MapSample();ms.MapUnlocked=false;Check(renewal.Next(ms,MapControl(),mapTime,out ma)&&renewal.Fault.Length>0,"do not leave a map that cannot be returned to");
// Integration with active cycle: same policy finishes combat and popup before the travel action.
p=new();ms=MapSample();ms.State="Fighting";ms.WeakHit=true;
Check(p.Next(ms,MapControl(),mapTime)==FishingAction.FightClick,"due map still permits fighting");
mapTime+=TimeSpan.FromMilliseconds(100).Ticks;ms.State="Caught";ms.ResultPopup=true;ms.CanClosePopup=true;ms.PopupId=100;
Check(p.Next(ms,MapControl(),mapTime)==FishingAction.ClosePopup,"due map closes settlement normally");
mapTime+=TimeSpan.FromMilliseconds(100).Ticks;ms.State="None";ms.ResultPopup=false;
Check(p.Next(ms,MapControl(),mapTime)==FishingAction.TravelLobby,"travel precedes new cast and consumables");
Check(p.Next(ms,new FishingControl(),mapTime)==FishingAction.None,"stop revokes pending map travel");
// End-to-end policy handoff: renewed room away from the water reaches approach and then normal casting.
p=new();ms=MapSample();var returnControl=MapControl();returnControl.AutoApproach=true;
Check(p.Next(ms,returnControl,mapTime)==FishingAction.TravelLobby,"approach flow leaves expired room");
ms.MapGroupId=0;ms.LobbyReady=true;ms.Ready=false;mapTime+=TimeSpan.FromSeconds(1).Ticks;returnControl.UntilUtcTicks=mapTime+TimeSpan.FromSeconds(10).Ticks;p.Next(ms,returnControl,mapTime);
mapTime+=TimeSpan.FromSeconds(2).Ticks;returnControl.UntilUtcTicks=mapTime+TimeSpan.FromSeconds(10).Ticks;Check(p.Next(ms,returnControl,mapTime)==FishingAction.TravelReturn,"approach flow returns to original room");
ms=MapSample();ms.RoomStartTicks++;ms.RoomRemainingSeconds=21600;ms.CanCast=false;mapTime+=TimeSpan.FromSeconds(1).Ticks;returnControl.UntilUtcTicks=mapTime+TimeSpan.FromSeconds(10).Ticks;
Check(p.Next(ms,returnControl,mapTime)==FishingAction.None&&p.MapRenewals==1,"room renewal finishes before shoreline positioning");
mapTime+=TimeSpan.FromMilliseconds(100).Ticks;Check(p.Next(ms,returnControl,mapTime)==FishingAction.ApproachWater,"renewal hands off to approach without reentering map");
ms.CanCast=true;mapTime+=TimeSpan.FromSeconds(2).Ticks;Check(p.Next(ms,returnControl,mapTime)==FishingAction.CastPress,"native casting readiness resumes ordinary fishing");
// Locked-only selling permits known unlocked legendary fish, with every other protection retained.
var lockedOnly=FishingSalePlan.Build(new[]{Fish(1,4),Fish(2,4,true),Fish(3,1),Fish(4,2),Fish(5,0),Fish(6,4,false,false),Fish(7,4,false,true,false)},true);
Check(lockedOnly.KeepLockedOnly&&lockedOnly.Items.Select(f=>f.InvenIndex).SequenceEqual(new long[]{1,3,4}),"locked-only sells known unlocked fish of all supported grades");
Check(lockedOnly.KeepIds.SequenceEqual(new long[]{2,5,6,7}),"locked unknown and unlisted fish are retained");
var lockedProgress=new FishingSaleProgress();lockedProgress.Begin(lockedOnly,time);lockedProgress.Observe(true,true,new long[]{2,5,6,7},time+1);Check(lockedProgress.SoldCount==3&&lockedProgress.Error=="","locked-only sale reconciles exact kept IDs");
lockedOnly=FishingSalePlan.Build(new[]{Fish(1,4)},true);lockedOnly.Items[0].IsLocked=true;throws=false;try{new FishingSaleProgress().Begin(lockedOnly,time);}catch(InvalidOperationException){throws=true;}Check(throws,"new mode rechecks a lock added after planning");
Check(!oldSettings.KeepLockedOnly&&oldSettings.AutoApproach,"older settings preserve legendary protection and enable approach");
for(int seed=0;seed<50;seed++){
 var rr=new Random(seed);var many=Enumerable.Range(1,180).Select(i=>Fish(i,new[]{0,1,2,3,4,5}[rr.Next(6)],rr.Next(3)==0,rr.Next(4)!=0,rr.Next(4)!=0)).ToArray();
 var chosen=FishingSalePlan.Build(many,true);var selected=chosen.Items.Select(x=>x.InvenIndex).ToHashSet();
 Check(chosen.Items.All(f=>!f.IsLocked&&f.HasFishTable&&f.HasSaleEntry&&(f.Grade==1||f.Grade==2||f.Grade==4)),"random locked-only protection");
 Check(chosen.KeepIds.Length+chosen.Items.Length==many.Length&&chosen.KeepIds.All(id=>!selected.Contains(id)),"locked-only sale and retained IDs partition inventory");
}
// First entry and refreshed rooms both approach the casting area, with no action during a fight or popup.
FishingSnapshot Away()=>new(){ProcessId=123,Ready=true,State="None",MapGroupId=3,CanCast=false};
FishingControl Walk()=>new(){ProcessId=123,Enabled=true,OwnerId="walk",AutoApproach=true,UntilUtcTicks=time+TimeSpan.FromSeconds(10).Ticks};
Check(new FishingPolicy().Next(Away(),Walk(),time)==FishingAction.ApproachWater,"away spawn requests native approach");
foreach(var gate in new[]{"disabled","expired","otherPid","busy","travel","daynight","popup","level","network","sale","bait","error","fight","caught","auto","lobby","notReady","canCast","modal"}){
 var a=Away();var cc=Walk();if(gate=="disabled")cc.AutoApproach=false;if(gate=="expired")cc.UntilUtcTicks=time-1;if(gate=="otherPid")cc.ProcessId++;
 a.Busy=gate=="busy";a.MapTravelBusy=gate=="travel";a.MapChangePending=gate=="daynight";a.ResultPopup=gate=="popup";a.LevelPopup=gate=="level";a.NetworkPending=gate=="network";a.SalePending=gate=="sale";a.BaitPending=gate=="bait";a.Error=gate=="error"?"test":"";
 if(gate=="fight")a.State="Fighting";if(gate=="caught")a.State="Caught";if(gate=="auto")a.State="Auto";if(gate=="lobby")a.MapGroupId=0;if(gate=="notReady")a.Ready=false;if(gate=="canCast")a.CanCast=true;if(gate=="modal")a.BlockReason="popup";
 Check(!FishingApproach.CanRun(a,cc,time),"approach gate: "+gate);Check(new FishingPolicy().Next(a,cc,time)!=FishingAction.ApproachWater,"policy gate: "+gate);
}
var walking=new FishingApproach();walking.Begin(time,10);Check(walking.Observe(time+TimeSpan.FromSeconds(7).Ticks,10,false)==ApproachDecision.Continue,"do not repeatedly replace a moving route");
Check(walking.Observe(time+TimeSpan.FromSeconds(8).Ticks,10,false)==ApproachDecision.Retry,"blocked route gets a bounded retry");walking.Begin(time,10);walking.Begin(time,10);Check(walking.Observe(time+TimeSpan.FromSeconds(8).Ticks,10,false)==ApproachDecision.Fail,"third blocked route stops");
walking.Reset();walking.Begin(time,10);Check(walking.Observe(time+TimeSpan.FromSeconds(7).Ticks,5,false)==ApproachDecision.Continue&&walking.Observe(time+TimeSpan.FromSeconds(12).Ticks,4,false)==ApproachDecision.Continue,"progress refreshes stall deadline");
Check(walking.Observe(time+TimeSpan.FromSeconds(91).Ticks,3,false)==ApproachDecision.Retry,"long route still has a deadline");
walking.Reset();walking.Begin(time,0);Check(walking.Observe(time+TimeSpan.FromMilliseconds(500).Ticks,0,true)==ApproachDecision.Continue&&walking.Observe(time+TimeSpan.FromSeconds(1).Ticks,0,true)==ApproachDecision.Retry,"arrival waits for actual native casting permission");
NavigationRegression.Run(Check);
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{status="pass",assertions,gameRequests=0,injection=false}));
