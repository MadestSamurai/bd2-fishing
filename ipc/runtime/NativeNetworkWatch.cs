using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
namespace BD2.LocalIpc
{
    // Observe the actual manager used by Send. No singleton creation and no client queue mutation.
    public sealed class NativeNetworkWatch : IDisposable
    {
        private static NativeNetworkWatch current;
        private object manager;
        private Harmony patch;
        private readonly NativeNetworkProbe probe=new NativeNetworkProbe();
        public void Start(Type managerType,string patchId)
        {
            var send=managerType.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                .Single(m=>m.Name=="Send"&&m.ReturnType==typeof(void)&&m.GetParameters().Length==6
                    &&m.GetParameters()[0].ParameterType.FullName=="Google.Protobuf.IMessage");
            current=this;patch=new Harmony(patchId);
            patch.Patch(send,prefix:new HarmonyMethod(typeof(NativeNetworkWatch),nameof(Sent)));
        }
        private static void Sent(object __instance){var watch=current;if(watch!=null)watch.manager=__instance;}
        // Main-thread only. Unknown or still-active native transport is never considered settled.
        public bool Idle { get { bool idle;return probe.TryIdle(manager,out idle)&&idle; } }
        public void Dispose(){if(current==this)current=null;if(patch!=null){patch.UnpatchAll(patch.Id);patch=null;}}
    }
}