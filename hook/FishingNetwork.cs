using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Proto.Net;
namespace BD2Fishing.Runtime
{
    // Postfixes observe original responses. They never replace callbacks or consume a packet.
    internal sealed class FishingNetwork : IDisposable
    {
        private static FishingNetwork current;
        private readonly Harmony patch = new Harmony("bd2.fishing.network");
        private readonly object sync=new object();
        private readonly Dictionary<string,Queue<DateTime>> pending=new Dictionary<string,Queue<DateTime>>();
        private Dictionary<MethodBase,string> handlers;
        private readonly Queue<string> events=new Queue<string>();
        private string last="尚无钓鱼请求", error="";
        private int catches;private long saleReplySerial;private bool saleReplyAccepted;
        private long baitReplySerial;private bool baitReplyAccepted;
        internal static MethodInfo SendMethod()=>typeof(BDNetwork.NetworkManager).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name=="Send" && m.ReturnType==typeof(void) && m.GetParameters().Length==6 && m.GetParameters()[0].ParameterType==typeof(Google.Protobuf.IMessage));
        internal static Dictionary<MethodBase,string> ResolveHandlers()
        {
            var result=new Dictionary<MethodBase,string>();
            var helper=FishingBindings.Type("Inventory");
            var methods=helper.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic).Concat(new[]{helper}).SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)).Where(m=>m.ReturnType==typeof(bool) && m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(byte[]),typeof(int),typeof(int)})).ToArray();
            foreach(var type in new[]{typeof(FishingCastingResponse),typeof(FishingBiteStartResponse),typeof(FishingBiteFishStaminaUpdateResponse),typeof(FishingBiteEndResponse),typeof(FishingShopSellResponse),typeof(FishingBaitUseResponse)})
            {
                var matches=methods.Where(m=>FishingIl.CallsParser(m,type)).ToArray();
                if(matches.Length!=1) throw new InvalidOperationException(type.Name+" 回调数量异常："+matches.Length);
                result[matches[0]]=type.Name.Replace("Response","");
            }
            return result;
        }
        internal void Start()
        {
            handlers=ResolveHandlers(); current=this;
            try {foreach(var m in handlers.Keys)patch.Patch(m,postfix:new HarmonyMethod(typeof(FishingNetwork),nameof(Response)));patch.Patch(SendMethod(),prefix:new HarmonyMethod(typeof(FishingNetwork),nameof(Sent)));}
            catch {Dispose();throw;}
        }
        private static void Sent(object __0)
        {
            var c=current;if(c==null || __0==null)return;var name=__0.GetType().Name;
            if(!name.EndsWith("Request",StringComparison.Ordinal))return;var kind=name.Substring(0,name.Length-7);
            if(!c.handlers.Values.Contains(kind))return;
            lock(c.sync){if(!c.pending.TryGetValue(kind,out var q))c.pending[kind]=q=new Queue<DateTime>();q.Enqueue(DateTime.UtcNow);c.last=kind+" 等待响应";c.events.Enqueue(c.last);}
        }
        private static void Response(byte[] __0,int __1,int __2,bool __result,MethodBase __originalMethod)
        {
            var c=current;if(c==null || !c.handlers.TryGetValue(__originalMethod,out var kind))return;
            lock(c.sync)
            {
                if(c.pending.TryGetValue(kind,out var q) && q.Count>0)q.Dequeue();
                if(kind=="FishingShopSell"){c.saleReplySerial++;c.saleReplyAccepted=__2==0 && __result;}
                if(kind=="FishingBaitUse"){c.baitReplySerial++;c.baitReplyAccepted=__2==0 && __result;}
                c.last=kind+" error="+__2+" accepted="+__result;
                if(__2!=0 || !__result)c.error=c.last;
                else if(kind=="FishingBiteEnd")
                {try{var parser=(object)FishingBiteEndResponse.Parser;var response=(FishingBiteEndResponse)parser.GetType().GetMethod("ParseFrom",new[]{typeof(byte[])}).Invoke(parser,new object[]{__0});var reward=response.RewardInfo;if(reward!=null && reward.FishInfo.Count>0)c.catches+=reward.FishInfo.Count;else c.error="收获响应未包含鱼奖励，请核对游戏";}catch(Exception e){c.error="无法确认收获响应："+e.Message;}}
                c.events.Enqueue(c.last);
            }
        }
        internal void Fill(FishingSnapshot s)
        {
            lock(sync)
            {
                var dates=pending.Values.Where(q=>q.Count>0).Select(q=>q.Peek()).ToArray();
                s.NetworkPending=dates.Length>0;s.NetworkWaitSeconds=dates.Length==0?0:(DateTime.UtcNow-dates.Min()).TotalSeconds;
                s.SaleReplySerial=saleReplySerial;s.SaleReplyAccepted=saleReplyAccepted;
                s.BaitReplySerial=baitReplySerial;s.BaitReplyAccepted=baitReplyAccepted;
                s.Network=last;s.Catches=catches;if(error.Length>0)s.Error=error;
            }
        }
        internal void AcknowledgeError(){lock(sync){error="";}}
        internal void Flush(){string[] list;lock(sync){list=events.ToArray();events.Clear();}foreach(var e in list)LocalStorage.Log("network "+e);}
        public void Dispose(){current=null;patch.UnpatchAll("bd2.fishing.network");}
    }
}
