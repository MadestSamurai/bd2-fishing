using System;
namespace BD2.LocalIpc
{
    // Initial migration only. New components carry Handoff and never use this legacy gate.
    // Resident legacy assemblies have no verifiable ownership/lifetime contract; do not guess how to stop them.
    public static class LegacyPilots
    {
        public static void Discover()
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name=assembly.GetName().Name??"";
                if(!name.StartsWith("BD2",StringComparison.Ordinal)||assembly.GetType("BD2.LocalIpc.Handoff",false)!=null)continue;
                bool legacy=false;
                foreach(var prefix in new[]{"BD2ReplayCaptureHook.","BD2ArenaDefenseWatcher.Active.Runtime","BD2Fishing.Runtime","BD2Sichuan.Runtime","BD2Rhythm.Runtime","BD2InfiniteGacha.Runtime","BD2ApostleDefense.Runtime","BD2Territory.Runtime","BD2SecretVision.Runtime","BD2SecretVisionRuntime","BD2PandoraRuntime","BD2Daily.Runtime","BD2Daily.Live","BD2DailyLive","BD2Equipment.Runtime","BD2Equipment.Live","BD2EquipmentLive","BD2MonsterAssistantRuntime","BD2MonsterCollector.Runtime","BD2MonsterBattleExecutorRuntime"})
                    if(name.StartsWith(prefix,StringComparison.Ordinal)){legacy=true;break;}
                if(legacy)throw new InvalidOperationException("首次迁移：请关闭旧工具并重启游戏一次，再连接新版工具。此后新版更新与切换无需重启。旧组件："+name);
            }
        }
    }
}
