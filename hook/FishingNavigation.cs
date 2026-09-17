#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using gamfs.Fishing;
namespace BD2Fishing.Runtime {
 internal sealed class FishingNavigation {
  readonly Action<string> log;readonly FishingApproach progress=new FishingApproach();
  readonly HashSet<int> rejected=new HashSet<int>();
  PlayerController player;PlayerMoveController move;FishingCastingArea target;Component boat;
  string key="",previousMode="",fault="";long readySince,lastObservation,facingAt,lastDiagnostic;bool owns,walking;
  Vector3 destination;public string Status {get;private set;}="";
  internal FishingNavigation(Action<string> log){this.log=log;}
  NavMeshAgent Agent()=>move==null?null:FishingBindings.Get(move,"ὦὣὠὨὣὨὡὯὪὢὦ") as NavMeshAgent;
  CharacterController Body()=>move==null?null:FishingBindings.Get(move,"ὫὬὤὪὠὧὨὩὬὬὩ") as CharacterController;
  void ChangeMode(string mode)=>FishingBindings.Call(move,"ChangeMoveType",FishingBindings.EnumObject("MoveKind",mode));
  void FaceOutward(){if(player==null||boat==null)return;var direction=player.transform.position-boat.transform.position;direction.y=0;if(direction.sqrMagnitude>.0001f)player.SetRotationForce(direction.normalized);}
  internal void Stop(){
   if(owns&&move!=null){
    // Release only this route. Do not turn a scene's DontMove/Anchored state into Stop.
    move.ClearMove();move.SetNavStop();
    var state=FishingBindings.Get(move,"ὭὠὢὩὫὤὥὬὧὡὢ").ToString();
    if(state!="DontMove"&&state!="Anchored"){move.StopMove();if(previousMode.Length>0)ChangeMode(previousMode);}
    log("approach_stop");
   }
   owns=false;walking=false;target=null;facingAt=0;
  }
  internal void Reconcile(FishingSnapshot s,FishingControl c,long now,bool policyFault){
   var next=c.OwnerId+"|"+s.MapGroupId+"|"+s.RoomStartTicks+"|"+s.Scene;
   if(next!=key){Stop();key=next;progress.Reset();rejected.Clear();readySince=0;fault="";Status="";lastDiagnostic=0;}
   if(!FishingApproach.CanRun(s,c,now)||policyFault){
    bool arrived=owns&&s.CanCast&&s.State=="None"&&!s.Busy&&!s.MapTravelBusy;
    Stop();readySince=0;
    if(arrived&&player!=null){FaceOutward();Status="已到钓区，等待下一竿";progress.Reset();rejected.Clear();}
   }else if(readySince==0)readySince=now;
   if(fault.Length>0)s.Error=fault;s.ApproachStatus=Status;
  }
  void Diagnostic(long now,string phase){
   if(now-lastDiagnostic<TimeSpan.FromSeconds(2).Ticks)return;lastDiagnostic=now;
   var a=Agent();var b=Body();
   log("approach_state phase="+phase+" mode="+FishingBindings.Get(move,"ὠὤὣὫὮὢὬὨὫὢὡ")+" state="+FishingBindings.Get(move,"ὭὠὢὩὫὤὥὬὧὡὢ")+" position="+player.transform.position+" nav="+(a==null?"missing":("enabled:"+a.isActiveAndEnabled+",onMesh:"+a.isOnNavMesh))+" body="+(b!=null&&b.enabled)+" walking="+walking+" target="+(target==null?"none":target.GetInstanceID().ToString())+" destination="+destination);
  }
  bool SelectWalking(object manager,out Vector3 chosen,out FishingCastingArea selected,out double best){
   chosen=Vector3.zero;selected=null;best=double.PositiveInfinity;
   var areas=FishingBindings.Get(manager,"ὫὨὥὣὫὪὨὥὪὡὣ") as IEnumerable;
   if(areas==null)return false;
   // Fishing boat decks need not contain any NavMesh. The game's touch/keyboard path
   // uses CharController and real collision, so choose a nearby actual casting area.
   foreach(var area in areas.OfType<FishingCastingArea>().Where(a=>FishingBindings.Active(a)&&!rejected.Contains(a.GetInstanceID()))){
    float radius=(float)FishingBindings.Num(area,"diameter")*.5f;
    if(radius<=0)continue;
    var center=area.transform.position;var delta=player.transform.position-center;
    double distance=new Vector2(delta.x,delta.z).magnitude;
    if(Math.Abs(delta.y)>2 || distance>30 || distance>=best)continue;
    best=distance;chosen=center;selected=area;
   }
   return selected!=null;
  }
  void WalkToward(){
   var delta=destination-player.transform.position;delta.y=0;
   // Same mode, direction and movement entry as native touch/keyboard movement.
   // The original controller owns speed, gravity and collision; never move transforms.
   ChangeMode("CharController");
   player.SetRotationForce(delta.normalized*Mathf.Clamp(delta.magnitude/.75f,.1f,1f));
   player.SetMoveStart();
  }
  bool Fail(FishingSnapshot s,string reason){Stop();fault=reason;Status=reason;s.Error=reason;log("approach_error "+reason);return false;}
  static double Length(NavMeshPath path){double distance=0;var p=path.corners;for(int i=1;i<p.Length;i++)distance+=Vector3.Distance(p[i-1],p[i]);return distance;}
  bool Select(object manager,NavMeshAgent agent,out Vector3 chosen,out FishingCastingArea selected,out double best){
   chosen=Vector3.zero;selected=null;best=double.PositiveInfinity;
   var areas=FishingBindings.Get(manager,"ὫὨὥὣὫὪὨὥὪὡὣ") as IEnumerable;
   if(areas==null)return false;
   var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
   foreach(var area in areas.OfType<FishingCastingArea>().Where(a=>FishingBindings.Active(a)&&!rejected.Contains(a.GetInstanceID()))){
    float radius=(float)FishingBindings.Num(area,"diameter")*.5f,halfHeight=(float)FishingBindings.Num(area,"height")*.5f;
    if(radius<=0||halfHeight<=0)continue;
    var center=area.transform.position;
    for(int i=0;i<9;i++){
     var p=center;if(i>0){var a=(i-1)*Mathf.PI/4;p+=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.65f;}
     NavMeshHit hit;var path=new NavMeshPath();
     if(!NavMesh.SamplePosition(p,out hit,Math.Max(.3f,halfHeight),filter))continue;
     // NavMesh points are on the floor; the avatar root includes the native agent base offset.
     var d=hit.position+Vector3.up*agent.baseOffset-center;
     if(Math.Abs(d.y)>halfHeight+.05f||new Vector2(d.x,d.z).magnitude>radius*.9f)continue;
     if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
     double length=Length(path);if(length>=best||length>200)continue;
     best=length;chosen=hit.position;selected=area;
    }
   }
   return selected!=null;
  }
  internal bool Approach(object manager,FishingSnapshot s,FishingControl c,long now){
   if(!FishingApproach.CanRun(s,c,now)||fault.Length>0)return false;
   if(readySince==0||now-readySince<TimeSpan.FromSeconds(1).Ticks){Status="等待落地和钓区稳定";return false;}
   var field=FishingBindings.Read("Field.Instance",null);
   var p=FishingBindings.Get(field,"ὬὪὧὦὭὬὪὧὣὣὫ") as PlayerController;
   var m=p==null?null:FishingBindings.Get(p,"ὥὠὬὪὨὯὭὬὡὡὣ") as PlayerMoveController;
   if(p==null||m==null){Status="等待角色落地";return false;}
   if(move!=m){Stop();player=p;move=m;}
   var state=FishingBindings.Get(move,"ὭὠὢὩὫὤὥὬὧὡὢ").ToString();
   if(state=="DontMove"||state=="Anchored"){Stop();readySince=now;Status="等待游戏允许移动";return false;}
   if(owns){
    if(now-lastObservation<TimeSpan.FromMilliseconds(50).Ticks)return false;lastObservation=now;
    var agent=Agent();var body=Body();bool valid=target!=null&&FishingBindings.Active(target)&&(walking?body!=null&&body.enabled:agent!=null&&agent.isActiveAndEnabled&&agent.isOnNavMesh);
    if(!valid){Stop();readySince=now;Status="等待重新识别钓区";return false;}
    if(walking)destination=target.transform.position;
    var delta=player.transform.position-destination;double distance=new Vector2(delta.x,delta.z).magnitude;
    if(distance<.2 && facingAt==0){move.ClearMove();move.StopMove();FaceOutward();facingAt=now;Status="已到钓区，转向水面并等待可抛竿确认";return false;}
    if(facingAt>0 && now-facingAt<TimeSpan.FromSeconds(1).Ticks)return false;
    double remaining=!walking&&agent.hasPath?agent.remainingDistance:distance;if(double.IsNaN(remaining)||double.IsInfinity(remaining))remaining=distance;
    Status="正在走向钓区，剩余 "+remaining.ToString("F1")+" 米";
    var decision=progress.Observe(now,remaining,distance<.2);
    Diagnostic(now,"moving");
    if(decision==ApproachDecision.Continue){if(walking)WalkToward();return false;}
    int id=target.GetInstanceID();rejected.Add(id);Stop();
    if(decision==ApproachDecision.Fail)return Fail(s,"多次未能抵达可钓区域，已停止移动；请检查挡路或落点");
    log("approach_replan target="+id);readySince=now;return false;
   }
   if(progress.Attempts>=FishingApproach.MaxAttempts)return Fail(s,"钓区移动已达重试上限，请检查当前位置");
   var nav=Agent();double best=0;FishingCastingArea area=null;Vector3 point=Vector3.zero;
   bool useNav=nav!=null&&nav.isActiveAndEnabled&&nav.isOnNavMesh&&Select(manager,nav,out point,out area,out best);
   if(!useNav){
    var body=Body();
    if(body==null||!body.enabled){Status="等待角色行走组件就绪";Diagnostic(now,"waiting_body");if(now-readySince>TimeSpan.FromSeconds(10).Ticks)return Fail(s,"角色行走组件未就绪，已暂停；请查看移动诊断");return false;}
    if(!SelectWalking(manager,out point,out area,out best)){Status="等待当前船体的钓区载入";Diagnostic(now,"waiting_area");if(now-readySince>TimeSpan.FromSeconds(10).Ticks)return Fail(s,"未找到附近的钓区，已暂停；请查看移动诊断");return false;}
   }
   boat=FishingBindings.Get(manager,"ὪὪὯὪὪὡὠὬὣὭὥ") as Component;
   if(boat==null){Status="等待钓鱼船载入";Diagnostic(now,"waiting_boat");if(now-readySince>TimeSpan.FromSeconds(10).Ticks)return Fail(s,"钓鱼船未载入，已暂停");return false;}
   previousMode=FishingBindings.Get(move,"ὠὤὣὫὮὢὬὨὫὢὡ").ToString();owns=true;walking=!useNav;
   target=area;destination=point;facingAt=0;progress.Begin(now,best);lastObservation=now;
   if(walking)WalkToward();
   else{
    ChangeMode("Navigation");player.SetMoveStart();
    if(!(bool)FishingBindings.Call(move,"SetMoveNav",point,null,true)){rejected.Add(area.GetInstanceID());Stop();readySince=now;Status="路径未生效，重新选择钓区";return false;}
   }
   Diagnostic(now,"start");
   log("approach_start attempt="+progress.Attempts+" mode="+(walking?"CharController":"Navigation")+" target="+area.GetInstanceID()+" from="+player.transform.position+" to="+point+" length="+best.ToString("F2"));
   Status="自动走向最近可达钓区";return true;
  }
 }
}
