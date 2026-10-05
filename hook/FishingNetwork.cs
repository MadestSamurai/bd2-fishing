using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Proto.Net;
using BD2.LocalIpc;
namespace BD2Fishing.Runtime
{
    // Original responses and native queues remain owned by the game.
    internal sealed class FishingNetwork : IDisposable
    {
        private static FishingNetwork current;
        private readonly Harmony patch = new Harmony("bd2.fishing.network");
        private readonly object sync = new object();
        private readonly RequestObservation pending = new RequestObservation();
        private readonly NativeNetworkProbe native = new NativeNetworkProbe();
        private Dictionary<MethodBase,string> handlers;
        private readonly Queue<string> events = new Queue<string>();
        private string last = "尚无钓鱼请求";
        private int catches, recoveries;
        private long saleReplySerial, baitReplySerial, unlockReplySerial, unlockReplyIndex, refreshTicks, lastRefresh;
        private bool saleReplyAccepted, baitReplyAccepted, unlockReplyAccepted;
        private bool nativeIdle, nativeKnown;
        internal static MethodInfo SendMethod() => typeof(BDNetwork.NetworkManager).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .Single(m=>m.Name=="Send" && m.ReturnType==typeof(void) && m.GetParameters().Length==6 && m.GetParameters()[0].ParameterType==typeof(Google.Protobuf.IMessage));
        internal static Dictionary<MethodBase,string> ResolveHandlers()
        {
            var result=new Dictionary<MethodBase,string>();
            var helper=FishingBindings.Type("Inventory");
            var methods=helper.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic).Concat(new[]{helper})
                .SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
                .Where(m=>m.ReturnType==typeof(bool) && m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(byte[]),typeof(int),typeof(int)})).ToArray();
            foreach(var type in new[]{typeof(FishingCastingResponse),typeof(FishingBiteStartResponse),typeof(FishingBiteFishStaminaUpdateResponse),typeof(FishingBiteEndResponse),typeof(FishingShopSellResponse),typeof(FishingBaitUseResponse),typeof(FishingItemInfoResponse)})
            {
                var matches=methods.Where(m=>FishingIl.CallsParser(m,type)).ToArray();
                if(matches.Length!=1)throw new InvalidOperationException(type.Name+" 回调数量异常："+matches.Length);
                result[matches[0]]=type.Name.Replace("Response","");
            }
            return result;
        }
        internal void Start()
        {
            handlers=ResolveHandlers();current=this;
            try {
                foreach(var m in handlers.Keys)patch.Patch(m,postfix:new HarmonyMethod(typeof(FishingNetwork),m.IsStatic?nameof(StaticResponse):nameof(Response)));
                patch.Patch((MethodInfo)FishingBindings.Api("Inventory.UnlockReply"),postfix:new HarmonyMethod(typeof(FishingNetwork),nameof(UnlockResponse)));
                patch.Patch(SendMethod(),prefix:new HarmonyMethod(typeof(FishingNetwork),nameof(Sent)));
            } catch {Dispose();throw;}
        }
        private void Note(string message) {last=message;if(events.Count>=128)events.Dequeue();events.Enqueue(message);}
        private static void Sent(object __0,object __1,object __3)
        {
            var c=current;if(c==null||__0==null)return;
            var name=__0.GetType().Name;if(!name.EndsWith("Request",StringComparison.Ordinal))return;
            var kind=name.Substring(0,name.Length-7);
            if(!c.handlers.Values.Contains(kind)&&kind!="FishingFishLock")return;
            lock(c.sync) {
                if(c.pending.Add(__0,(__1 as Delegate)??(__3 as Delegate),kind,DateTime.UtcNow.Ticks)!=null)c.recoveries++;
                c.Note(kind+" 等待响应");
            }
        }
        private static void StaticResponse(byte[] __0,int __2,bool __result,MethodBase __originalMethod)
            {Observe(null,__0,__2,__result,__originalMethod);}
        private static void Response(object __instance,byte[] __0,int __2,bool __result,MethodBase __originalMethod)
            {Observe(__instance,__0,__2,__result,__originalMethod);}
        private static void Observe(object target,byte[] bytes,int error,bool accepted,MethodBase method)
        {
            var c=current;if(c==null)return;
            lock(c.sync) {
                var item=c.pending.Complete(method,target);
                if(item==null){c.Note("收到旧回执，已按当前状态处理");return;}
                var kind=item.Kind;
                if(kind=="FishingShopSell"){c.saleReplySerial++;c.saleReplyAccepted=error==0&&accepted;}
                if(kind=="FishingBaitUse"){c.baitReplySerial++;c.baitReplyAccepted=error==0&&accepted;}
                if(kind=="FishingItemInfo"&&error==0&&accepted)c.refreshTicks=item.Started;
                c.Note(kind+" error="+error+" accepted="+accepted);
                if(kind=="FishingBiteEnd"&&error==0&&accepted) {
                    try {var response=FishingBiteEndResponse.Parser.ParseFrom(bytes);if(response.RewardInfo!=null)c.catches+=response.RewardInfo.FishInfo.Count;}
                    catch(Exception e){c.Note("收获统计暂不可用："+e.GetBaseException().Message);}
                }
            }
        }
        // Called from the main frame, not the pipe/handoff worker.
        internal void Reconcile(long now)
        {
            var manager=FishingBindings.Invoke("Network.Instance");
            bool idle;bool known=native.TryIdle(manager,out idle);
            lock(sync) {
                nativeKnown=known;nativeIdle=known&&idle;
                var retired=pending.Reconcile(now,nativeIdle);
                if(retired.Length>0){recoveries+=retired.Length;Note("网络已恢复，已回收 "+retired.Length+" 条过期观察；按当前游戏状态继续");}
            }
        }
        internal void RefreshInventory(long now)
        {
            lock(sync) {
                if(!nativeIdle||pending.Count>0||now-lastRefresh<TimeSpan.FromSeconds(RequestObservation.TimeoutSeconds).Ticks)return;
                lastRefresh=now;
                Note("正在同步鱼背包和鱼饵，未重复旧操作");
            }
            FishingBindings.Invoke("Inventory.RefreshItems",false);
        }
        internal void Fill(FishingSnapshot s)
        {
            lock(sync) {
                long oldest=pending.Oldest;
                s.NetworkPending=pending.Count>0||!nativeIdle;s.NetworkIdle=nativeIdle;
                s.NetworkWaitSeconds=oldest==0?0:Math.Max(0,(DateTime.UtcNow.Ticks-oldest)/(double)TimeSpan.TicksPerSecond);
                s.SaleReplySerial=saleReplySerial;s.SaleReplyAccepted=saleReplyAccepted;
                s.BaitReplySerial=baitReplySerial;s.BaitReplyAccepted=baitReplyAccepted;
                s.UnlockReplySerial=unlockReplySerial;s.UnlockReplyIndex=unlockReplyIndex;s.UnlockReplyAccepted=unlockReplyAccepted;
                s.InventoryRefreshTicks=refreshTicks;s.NetworkRecoveries=recoveries;
                s.Network=!nativeKnown?"等待读取游戏网络状态":s.NetworkPending&&s.NetworkWaitSeconds>=RequestObservation.TimeoutSeconds?"等待游戏网络恢复，恢复后自动继续":last;
                s.Catches=catches;
            }
        }
        internal void AcknowledgeError() {}
        private static void UnlockResponse(FishingFishLockRequest __0,int __3,bool __result)
        {
            var c=current;if(c==null)return;
            lock(c.sync) {
                if(c.pending.CompleteRequest(__0)==null)return;
                c.unlockReplySerial++;c.unlockReplyIndex=__0.FishLockInfo.Count==1?__0.FishLockInfo[0].InvenIndex:0;
                c.unlockReplyAccepted=__3==0&&__result&&c.unlockReplyIndex>0&&!__0.FishLockInfo[0].IsLock;
                c.Note("FishingFishLock error="+__3+" accepted="+__result);
            }
        }
        internal void Flush(){string[] list;lock(sync){list=events.ToArray();events.Clear();}foreach(var e in list)LocalStorage.Log("network "+e);}
        public void Dispose(){current=null;patch.UnpatchAll("bd2.fishing.network");}
    }
}
