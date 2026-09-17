using BD2Fishing;
using BD2Fishing.Runtime;
using UnityEngine;
using UnityEngine.AI;
using gamfs.Fishing;
static class NavigationRegression {
 public static void Run(Action<bool,string> check){
  long now=DateTime.UtcNow.Ticks;
  var logs=new List<string>();var host=new NavigationHost();FishingNavigation nav=null!;
  FishingSnapshot snap=null!;FishingControl ctrl=null!;
  void Reset(string agent="missing",float startX=0){
   logs.Clear();host=new();host.Player.transform.position=new(startX,0,0);
   host.Areas=new[]{new FishingCastingArea{transform=new Transform{position=new(3,0,0)}}};
   if(agent!="missing")host.Player.Move.Nav=new(){isActiveAndEnabled=agent!="disabled",isOnNavMesh=agent=="ready",transform=host.Player.transform};
   FishingBindings.Host=host;nav=new(logs.Add);
   snap=new(){ProcessId=123,Ready=true,State="None",MapGroupId=7,Scene="boat",RoomStartTicks=1};
   ctrl=new(){OwnerId=Guid.NewGuid().ToString(),ProcessId=123,Enabled=true,AutoApproach=true};
  }
  void Step(bool simulateMovement=true,bool renew=true){
   now+=TimeSpan.FromMilliseconds(100).Ticks;if(renew)ctrl.UntilUtcTicks=now+TimeSpan.FromSeconds(10).Ticks;
   var p=host.Player;var m=p.Move;
   if(simulateMovement&&m.State=="Moving"){
    if(m.Mode=="CharController"&&m.Body?.enabled==true)p.transform.position+=m.Direction*.3f;
    if(m.Mode=="Navigation"&&m.Nav?.hasPath==true){var d=m.Destination-p.transform.position;p.transform.position+=d.normalized*Math.Min(.3f,d.magnitude);m.Nav.remainingDistance=(m.Destination-p.transform.position).magnitude;}
   }
   var delta=host.Areas.Length==0?new Vector3(100,0,0):p.transform.position-host.Areas[0].transform.position;
   var outward=(p.transform.position-host.Boat.transform.position).normalized;
   var dir=m.Direction.normalized;
   snap.CanCast=delta.magnitude<=.5f&&(dir.x*outward.x+dir.z*outward.z)>.7f;
   snap.Error="";
   nav.Reconcile(snap,ctrl,now,false);
   if(FishingApproach.CanRun(snap,ctrl,now))nav.Approach(host,snap,ctrl,now);
  }
  foreach(var agent in new[]{"missing","disabled","off_mesh"}){
   Reset(agent);for(int i=0;i<90&&!snap.CanCast;i++)Step();Step();
   check(host.Player.Starts>0&&host.Player.transform.position.x>2,"no NavMesh drives real production coordinator: "+agent);
   check(snap.CanCast&&host.Player.Move.State=="Stop","arrives, turns and releases movement: "+agent);
   check(host.Player.Move.NavStarts==0&&logs.Any(x=>x.Contains("mode=CharController")),"fallback never starts unavailable native navigation: "+agent);
   check(logs.Count(x=>x=="approach_stop")==1,"no per-frame failed start/stop churn: "+agent);
  }
  Reset("missing",4);for(int i=0;i<100&&!snap.CanCast;i++)Step();Step();
  check(snap.CanCast&&host.Player.Move.Direction.x>0,"arriving from outside turns outward before CanCast");
  Reset("ready");for(int i=0;i<20;i++)Step(false);
  check(host.Player.Move.NavStarts==1&&logs.Any(x=>x.Contains("mode=Navigation")),"available NavMesh retains normal path workflow");
  foreach(var gate in new[]{"disabled","lease","popup","scene","fighting","setting"}){
   Reset();for(int i=0;i<16;i++)Step();check(host.Player.Move.State=="Moving","movement active before "+gate);
   if(gate=="disabled")ctrl.Enabled=false;
   if(gate=="lease")ctrl.UntilUtcTicks=0;
   if(gate=="popup")snap.BlockReason="MessagePopupUI";
   if(gate=="scene")snap.Busy=true;
   if(gate=="fighting")snap.State="Fighting";
   if(gate=="setting")ctrl.AutoApproach=false;
   Step(false,gate!="lease");check(host.Player.Move.State=="Stop","owned movement released on "+gate);
  }
  foreach(var state in new[]{"DontMove","Anchored"}){
   Reset();for(int i=0;i<16;i++)Step();host.Player.Move.State=state;Step(false);
   check(host.Player.Move.State==state,"native movement lock preserved: "+state);
  }
  Reset();host.Areas=Array.Empty<FishingCastingArea>();for(int i=0;i<130;i++)Step();
  check(snap.Error.Contains("钓区")&&host.Player.Starts==0&&host.Player.Move.Stops==0,"missing target pauses without unsolicited movement or stop spam");
  Reset();for(int i=0;i<240;i++)Step(false);
  check(snap.Error.Length>0&&host.Player.Move.State=="Stop","collision stall bounded and stops");
  ctrl.OwnerId=Guid.NewGuid().ToString();for(int i=0;i<100&&!snap.CanCast;i++)Step();Step();
  check(snap.Error.Length==0&&snap.CanCast,"restart clears rejected target and movement fault");
  Reset();host.Player.Move.Body=null;for(int i=0;i<130;i++)Step();
  check(snap.Error.Contains("行走组件")&&host.Player.Starts==0,"missing body waits then explains failure");
 }
}
