using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BD2Fishing.Runtime
{
    internal sealed class RuntimeEngine
    {
        private static RuntimeEngine current;
        private readonly Harmony patch=new Harmony("bd2.fishing.inputs");
        private readonly FishingPolicy policy=new FishingPolicy();
        private readonly FishingNetwork network=new FishingNetwork();
        private FishingInventory inventory;
        private FishingBait bait;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> diagnostics=new System.Collections.Concurrent.ConcurrentQueue<string>();
        private readonly HashSet<int> consumed=new HashSet<int>();
        private Timer timer;private int ioBusy;private bool stopped;
        private volatile FishingControl control=new FishingControl();
        private volatile FishingSnapshot latest=new FishingSnapshot();
        private FishingGameFieldDefaultUI ui;private UIBase popup;private FishingSkillHoldItem ownedHold;
        private UIBase[] surfaces=new UIBase[0];private long lastScan,lastStatus;private string observedOwner="",lastAction="";
        private string lastUiLayers="",lastMapTransition="";
        private long actionCount;private double previousX;private float previousTime;private int previousNeedle;
        private readonly int pid=System.Diagnostics.Process.GetCurrentProcess().Id;
        internal static MethodInfo Pump()=>typeof(GameCameraManager).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic,null,Type.EmptyTypes,null)??throw new MissingMethodException("GameCameraManager.LateUpdate");
        internal void Start()
        {
            if(timer!=null)return;
            FishingBindings.ValidateCompiledClient();
            FishingBindings.Validate();FishingInventory.SaleMethod();inventory=new FishingInventory(LogDiagnostic);FishingBait.UseMethod();bait=new FishingBait(LogDiagnostic);network.Start();current=this;
            try
            {
                patch.Patch(Pump(),postfix:new HarmonyMethod(typeof(RuntimeEngine),nameof(Frame)));
                patch.Patch((MethodInfo)FishingBindings.Member(typeof(FishingSkillHoldItem),"Update"),prefix:new HarmonyMethod(typeof(RuntimeEngine),nameof(HoldFrame)));
                foreach(var t in new[]{typeof(FishingSkillTeethItem),typeof(FishingSkillRecoveryItem)})patch.Patch((MethodInfo)FishingBindings.Member(t,"Active"),postfix:new HarmonyMethod(typeof(RuntimeEngine),nameof(TargetActivated)));
                timer=new Timer(_=>IO(),null,0,50);
            }
            catch{Stop();throw;}
        }
        private static void Frame(){current?.Tick(false,null);}
        private static void HoldFrame(FishingSkillHoldItem __instance){current?.Tick(true,__instance);}
        private static void TargetActivated(Component __instance){current?.consumed.Remove(__instance.GetInstanceID());}
        private void IO()
        {
            if(Interlocked.Exchange(ref ioBusy,1)!=0)return;
            try
            {
                if(stopped)return;
                var file=Path.Combine(LocalStorage.DataRoot,"control.json");
                try
                {
                    using(var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                    {if(f.Length>16000)throw new IOException("控制文件过大");control=(FishingControl)new DataContractJsonSerializer(typeof(FishingControl)).ReadObject(f);}
                }
                catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Runtime.Serialization.SerializationException){}
                var now=DateTime.UtcNow.Ticks;
                if(now-lastStatus>TimeSpan.FromMilliseconds(250).Ticks)
                {lastStatus=now;LocalStorage.WriteJsonAtomically(Path.Combine(LocalStorage.DataRoot,"latest.json"),latest);network.Flush();while(diagnostics.TryDequeue(out var entry))LocalStorage.Log(entry);Loader.WriteStatus("active","");}
            }
            catch(Exception e){LocalStorage.Log("io "+e.Message);}
            finally{Interlocked.Exchange(ref ioBusy,0);}
        }
        private void Tick(bool holdPhase,FishingSkillHoldItem hold)
        {
            if(stopped)return;
            try
            {
                var now=DateTime.UtcNow.Ticks;
                if(now-lastScan>TimeSpan.FromMilliseconds(250).Ticks)
                {
                    lastScan=now;surfaces=UnityEngine.Object.FindObjectsOfType<UIBase>().Where(FishingBindings.Active).ToArray();
                    var next=surfaces.OfType<FishingGameFieldDefaultUI>().FirstOrDefault();
                    if(ui!=next){ReleaseHold();ui=next;consumed.Clear();previousNeedle=0;}
                }
                if(holdPhase && (ui==null || !ReferenceEquals(FishingBindings.Get(FishingBindings.Get(ui,"_skillCaster"),"_holdItem"),hold)))return;
                var s=Read(now);var c=control??new FishingControl();
                if(c.Valid(now,pid) && observedOwner!=c.OwnerId){observedOwner=c.OwnerId;network.AcknowledgeError();inventory.AcknowledgeError();bait.AcknowledgeError();}
                network.Fill(s);inventory.Fill(s,now);bait.Fill(ui,s,now);
                var action=policy.Next(s,c,now,holdPhase);
                if(action!=FishingAction.None)Apply(action,s);
                s.Enabled=c.Valid(now,pid) && policy.Fault.Length==0;
                s.OwnerId=c.OwnerId;s.Reason=policy.Reason;s.LastAction=lastAction;s.ActionCount=actionCount;
                if(s.Enabled && s.MapChangePending && !s.Busy && s.State!="None")s.Reason+="；昼夜切换排队，先完成本竿";
                if(policy.Fault.Length>0)s.Error=policy.Fault;
                latest=s;
            }
            catch(Exception e)
            {
                var error=e.GetBaseException().Message;policy.Fail(error);
                try{ReleaseHold();}catch{}
                latest=new FishingSnapshot{ProcessId=pid,CapturedUtcTicks=DateTime.UtcNow.Ticks,OwnerId=control?.OwnerId??"",Error=error,Reason="钓鱼已暂停："+error};
            }
        }
        private FishingSnapshot Read(long now)
        {
            var s=new FishingSnapshot{ProcessId=pid,CapturedUtcTicks=now,Scene=SceneManager.GetActiveScene().name};
            if(!FishingBindings.Active(ui))return s;
            var manager=FishingBindings.Get(ui,"ὢὨὠὩὤὬὠὧὬὠὬ");
            var model=FishingBindings.Get(manager,"ὧὠὤὦὠὡὠὪὪὢὠ");
            var settings=FishingBindings.Get(manager,"ὨὥὯὤὪὣὬὪὤὬὬ");
            if(model==null || settings==null)return s;
            s.Ready=true;s.State=FishingBindings.Get(manager,"ὮὧὥὥὦὬὤὭὤὡὪ").ToString();
            ReadMapTransition(manager,s);
            var transition=s.Scene+" pending="+s.MapChangePending+" active="+s.Busy;
            if(transition!=lastMapTransition){lastMapTransition=transition;LogDiagnostic("map_transition "+transition+" fishing="+s.State);}
            var charger=(FishingCastingCharger)FishingBindings.Get(ui,"_castingCharger");
            s.CanCast=FishingBindings.Flag(charger,"ὩὯὪὩὯὫὮὤὭὧὢ");s.CastRunning=FishingBindings.Flag(charger,"ὩὡὣὢὬὦὥὧὥὭὦ");
            s.BagFull=(bool)FishingBindings.Call(ui,"ὬὣὦὫὬὯὧὮὣὣὡ");s.Gauge=charger.GetNormalizeValue();
            s.CastGrade=Convert.ToInt32(FishingBindings.Invoke("Tables.CastGrade",(float)s.Gauge,0));
            var activePopups=surfaces.Where(v=>FishingBindings.Active(v) && FishingBindings.Flag(v,"ὡὡὡὯὬὨὢὧὦὦὫ")).ToArray();
            var open=activePopups.Where(FishingPopupClassifier.IsBlockingPopup).OrderByDescending(v=>FishingBindings.Num(v,"ὤὩὪὬὬὤὪὮὣὩὩ")).ToArray();
            s.UiLayers=string.Join("; ",activePopups.Select(v=>v.GetType().Name+"["+FishingBindings.Get(v,"ὧὨὦὯὣὣὡὣὪὮὨ")+"]="+(FishingPopupClassifier.IsBlockingPopup(v)?"popup":"hud")).OrderBy(v=>v));
            if(s.UiLayers!=lastUiLayers){lastUiLayers=s.UiLayers;if(diagnostics.Count<256)diagnostics.Enqueue("ui_layers "+lastUiLayers);}
            popup=open.FirstOrDefault(v=>v is AvatarFishingGetPopupUI || v is AvatarFishingLevelUpPopupUI);
            s.ResultPopup=popup is AvatarFishingGetPopupUI;s.LevelPopup=popup is AvatarFishingLevelUpPopupUI;
            if(popup!=null){s.CanClosePopup=popup.CanCloseUI();s.PopupId=popup.GetInstanceID();}
            var other=open.FirstOrDefault(v=>v!=popup);
            if(other!=null)s.BlockReason="等待关闭游戏弹窗："+other.GetType().Name;
            if(s.State!="Fighting")return s;
            s.FishId=(int)FishingBindings.Num(model,"ὦὡὬὨὤὢὠὥὬὡὣ");
            s.FishHp=FishingBindings.Num(FishingBindings.Get(FishingBindings.Get(model,"ὮὬὠὯὡὧὢὥὥὢὦ"),"ὦὬὢὢὮὩὨὨὠὠὯ"),"ὬὤὦὩὮὨὠὬὯὯὣ");
            s.TimeRemaining=FishingBindings.Num(FishingBindings.Get(FishingBindings.Get(model,"ὤὥὫὤὦὧὪὤὤὢὯ"),"ὩὤὮὠὠὥὪὢὩὯὡ"),"ὬὤὦὩὮὨὠὬὯὯὣ");
            var picker=(FishingHitzonePicker)FishingBindings.Get(ui,"_hitzonePicker");
            var skills=(FishingSkillCaster)FishingBindings.Get(ui,"_skillCaster");
            bool normal,weak;picker.TryPickHitzone(out normal,out weak);s.NormalHit=normal;s.WeakHit=weak;
            var nr=(RectTransform)FishingBindings.Get(ui,"_rectHitzoneNormal");var wr=(RectTransform)FishingBindings.Get(ui,"_rectHitzoneWeak");
            var needle=(RectTransform)FishingBindings.Read("Needle.Rect",FishingBindings.Read("Field.Needle",ui));
            s.NormalWidth=nr.rect.width;s.WeakWidth=wr.rect.width;s.WeakPosition=wr.anchoredPosition.x;s.NeedlePosition=needle.anchoredPosition.x;
            s.ShrinkSpeed=FishingBindings.Num(settings,"NormalHitzoneDecreaseSpeed");
            if(!float.IsNaN(Time.unscaledTime) && previousNeedle==needle.GetInstanceID() && Time.unscaledTime>previousTime)
                s.NeedleVelocity=(s.NeedlePosition-previousX)/(Time.unscaledTime-previousTime);
            previousTime=Time.unscaledTime;previousX=s.NeedlePosition;previousNeedle=needle.GetInstanceID();
            s.BlockedHit=normal && skills.IsActiveBlock() && picker.Overlaps(skills.GetRectBlock());s.Freeze=!skills.IsSatisfied();
            var pools=new[]{"ὡὮὮὪὨὨὪὪὫὣὩ","ὦὣὪὬὫὢὢὩὤὥὤ","ὦὪὦὭὧὯὮὮὪὫὣ","ὦὭὠὠὣὢὯὢὩὣὪ"};
            var enabled=new[]{skills.IsActiveTeeth(),skills.IsActiveRecovery(),skills.IsActiveTrap(),skills.IsActiveShellShield()};
            for(int i=0;i<pools.Length && s.Interaction.Length==0;i++)if(enabled[i])
            {
                var pool=FishingBindings.Get(skills,pools[i]);var list=(IEnumerable)FishingBindings.Get(pool,"ὡὫὮὠὡὩὮὨὮὡὬ");
                foreach(Component item in list)if(FishingBindings.Active(item) && picker.Overlaps((RectTransform)FishingBindings.Get(item,"ὤὨὪὫὧὥὫὮὣὮὥ")))
                {s.TargetId=item.GetInstanceID();s.Interaction=i>=2?"hazard":consumed.Contains(s.TargetId)?"consumed":"useful";break;}
            }
            var h=(FishingSkillHoldItem)FishingBindings.Get(skills,"_holdItem");s.HoldActive=FishingBindings.Active(h);
            if(s.HoldActive)
            {
                s.HoldCompleted=FishingBindings.Flag(h,"ὬὩὡὩὦὧὤὤὠὩὠ");s.HoldTracking=FishingBindings.Flag(h,"ὪὤὧὥὡὬὩὪὤὨὣ");
                var hn=(RectTransform)FishingBindings.Get(h,"ὯὩὬὯὮὯὦὧὮὪὢ");
                var camera=(Camera)FishingBindings.Read("Camera.UiCamera",FishingBindings.Read("Camera.Instance",null));
                Func<string,bool> hit=name=>{var target=(RectTransform)FishingBindings.Get(h,name);return target!=null && hn!=null && FishingHitzonePicker.Overlaps(hn,target,camera);};
                s.HoldStartHit=hit("_rectStart");s.HoldEndHit=hit("_rectEnd");s.HoldInside=hit("_rectKeep");s.HoldTargetHit=hit("ὮὢὣὩὫὥὠὨὢὬὧ");
            }
            return s;
        }
        internal static void ReadMapTransition(object manager,FishingSnapshot s)
        {
            // ProcessMapChange waits for None with pending=true. Treating this as Busy deadlocks
            // the fight/result popup whose normal close callback calls EndFishing and reaches None.
            s.MapChangePending=FishingBindings.Flag(manager,"ὢὣὢὬὣὧὣὫὭὤὬ");
            s.Busy=FishingBindings.Flag(manager,"ὯὥὬὦὤὬὡὤὡὡὨ");
        }
        private void Apply(FishingAction action,FishingSnapshot s)
        {
            switch(action)
            {
                case FishingAction.SellFish:
                    var c=control;
                    if(c==null || !c.Valid(DateTime.UtcNow.Ticks,pid) || !c.AutoSell || !s.Ready || s.State!="None" || !s.BagFull || s.Busy || s.MapChangePending || s.BlockReason.Length>0 || s.ResultPopup || s.LevelPopup || s.NetworkPending || s.SalePending || s.BaitPending)return;
                    if(!inventory.Sell(DateTime.UtcNow.Ticks,s.SaleReplySerial))return;
                    break;
                case FishingAction.UseBait:
                    var baitControl=control;
                    if(baitControl==null || !baitControl.Valid(DateTime.UtcNow.Ticks,pid) || !baitControl.AutoBait || !s.Ready || s.State!="None" || s.BagFull || !s.CanCast || s.Busy || s.MapChangePending || s.BlockReason.Length>0 || s.ResultPopup || s.LevelPopup || s.NetworkPending || s.SalePending || s.BaitPending)return;
                    if(!bait.Use(ui,DateTime.UtcNow.Ticks,s.BaitReplySerial))return;
                    break;
                case FishingAction.CastPress: FishingBindings.Call(ui,"ὣὥὪὥὫὬὬὮὦὭὩ");break;
                case FishingAction.CastRelease: if(ui!=null)FishingBindings.Call(ui,"ὯὦὦὯὤὪὡὮὢὫὮ");break;
                case FishingAction.Hook: ui.OnClickUI((GameObject)FishingBindings.Get(ui,"_goBtnHook"));break;
                case FishingAction.FightClick:
                    ui.OnClickUI((GameObject)FishingBindings.Get(ui,"_goBtnFight"));
                    if(!s.Freeze && s.Interaction=="useful")consumed.Add(s.TargetId);break;
                case FishingAction.HoldPress:
                    ownedHold=(FishingSkillHoldItem)FishingBindings.Get(FishingBindings.Get(ui,"_skillCaster"),"_holdItem");ownedHold.OnPointerDown(null);break;
                case FishingAction.HoldRelease: ReleaseHold();break;
                case FishingAction.ClosePopup: if(popup!=null && popup.CanCloseUI()){LogDiagnostic("settlement_close popup="+popup.GetType().Name+" pending="+s.MapChangePending+" active="+s.Busy);popup.CloseUI();}break;
            }
            lastAction=action.ToString();actionCount++;
            if(diagnostics.Count<256)diagnostics.Enqueue("input "+lastAction+" state="+s.State+" fish="+s.FishId+" target="+s.TargetId+" weak="+s.WeakHit+" gauge="+s.Gauge+" reason="+policy.Reason);
        }
        private void LogDiagnostic(string message){if(diagnostics.Count<256)diagnostics.Enqueue(message);}
        private void ReleaseHold(){if(ownedHold!=null)ownedHold.OnPointerUp(null);ownedHold=null;}
        internal void Stop(){stopped=true;current=null;timer?.Dispose();timer=null;patch.UnpatchAll("bd2.fishing.inputs");network.Dispose();}
    }
}
