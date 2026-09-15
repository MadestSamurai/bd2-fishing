using System;
using System.Reflection;
using UnityEngine;
namespace BD2Fishing.Runtime
{
    internal static class FishingMap
    {
        internal static void Read(object manager,FishingSnapshot s)
        {
            if(manager==null || manager is UnityEngine.Object obj && obj==null)return;
            s.MapGroupId=(int)FishingBindings.Num(manager,"ὭὡὧὡὢὡὬὬὧὥὯ");
            s.MapTravelBusy=FishingBindings.Get(manager,"ὡὣὧὫὦὯὧὤὧὦὬ")!=null;
            RuntimeEngine.ReadMapTransition(manager,s);
            var lobby=FishingBindings.Get(manager,"ὣὤὣὬὪὡὥὨὪὫὤ") as Component;
            s.LobbyReady=s.MapGroupId==0 && !s.MapTravelBusy && FishingBindings.Active(lobby);
            if(s.MapGroupId<=0)return;
            s.MapUnlocked=Unlocked(s.MapGroupId);
            var start=(DateTime)FishingBindings.Get(manager,"ὢὪὠὠὩὪὧὧὭὨὬ");
            var table=FishingBindings.Read("Tables.Default",null);
            if(start==default(DateTime) || table==null)return;
            var clock=FishingBindings.Read("Clock.Instance",null);
            if(clock==null)return;
            var now=(DateTime)((MethodInfo)FishingBindings.Api("Clock.Now")).Invoke(clock,null);
            var duration=FishingBindings.Num(table,"RoomDuration");
            FillClock(s,start,now,duration);
        }
        internal static void FillClock(FishingSnapshot s,DateTime start,DateTime now,double duration)
        {
            // Both timestamps use the game's synchronized clock; never mix PC local/UTC time.
            var elapsed=(now-start).TotalSeconds;
            s.RoomTimerKnown=start!=default(DateTime) && duration>0 && duration<=7*86400 && elapsed>=-5;
            if(!s.RoomTimerKnown)return;
            s.RoomStartTicks=start.Ticks;s.RoomDurationSeconds=duration;s.RoomRemainingSeconds=duration-elapsed;
        }
        internal static bool Unlocked(int map) => (bool)FishingBindings.Invoke("Inventory.MapUnlocked",map);
        internal static void Travel(object manager,int map)
        {
            if(manager==null || map<0 || map>0 && !Unlocked(map))throw new InvalidOperationException("原钓场未解锁，已暂停自动返回");
            FishingBindings.Call(manager,"EnterPackFishing",map);
        }
    }
}
