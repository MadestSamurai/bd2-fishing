using System;
using System.IO;
using System.Reflection;
using BD2.LocalIpc;
namespace BD2Fishing.Runtime
{
    public static class Loader
    {
        private static object engine;
        private static bool resolver;
        private static Handoff handoff;
        private static MainThread frame;
        public static void Load()
        {
            lock(typeof(Loader))
            {
                if(handoff==null)handoff=new Handoff(typeof(Loader).Assembly.FullName,"fishing",Build.Group,false,Start,Pause,Busy,Stop,WriteStatus);
                if(!handoff.IsActive&&!handoff.Pending)RuntimeFiles.Start(LocalStorage.DataRoot,Build.Fingerprint,FishingIdentity.LiveEntries);
                handoff.Request(DateTime.UtcNow);
                if(frame==null)frame=new MainThread(()=>{LegacyPilots.Discover();handoff.Tick(DateTime.UtcNow);return handoff.Pending;},handoff.Fail);
                frame.Schedule();
            }
        }
        private static void Start()
        {
            if(Build.Group=="daily"&&!Handoff.HasActive("daily","daily-live"))throw new InvalidOperationException("日常执行器未激活，不能单独启动日常钓鱼组件。");
            RuntimeFiles.Start(LocalStorage.DataRoot,Build.Fingerprint,FishingIdentity.LiveEntries);
            if(!resolver){AppDomain.CurrentDomain.AssemblyResolve+=Resolve;resolver=true;}
            engine=Activator.CreateInstance(typeof(Loader).Assembly.GetType("BD2Fishing.Runtime.RuntimeEngine",true),true);Invoke("Start");
        }
        private static void Pause(){RuntimeFiles.Revoke();if(engine!=null)Invoke("PrepareHandoff");}
        private static string Busy(){return engine==null?"":(string)Invoke("HandoffBusy");}
        private static void Stop()
        {
            if(engine!=null)Invoke("Stop");engine=null;
            if(resolver){AppDomain.CurrentDomain.AssemblyResolve-=Resolve;resolver=false;}
        }
        private static object Invoke(string method){return engine.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(engine,null);}
        public static void Unload()
        {
            MainThread.Drain(()=>{if(handoff!=null)handoff.Unload();},WriteStatus);
        }
        internal static void WriteStatus(string state,string error)
        {if(state=="active"&&handoff!=null&&handoff.IsActive)RuntimeFiles.Activate();try{LocalStorage.WriteJsonAtomically(Path.Combine(LocalStorage.DataRoot,"runtime.json"),new FishingRuntimeStatus{State=state,Error=error,Runtime=FishingIdentity.RuntimeName,AtUtc=DateTime.UtcNow.ToString("O"),ProcessId=System.Diagnostics.Process.GetCurrentProcess().Id});}catch(Exception e){LocalStorage.Log(e.Message);}}
        private static Assembly Resolve(object sender,ResolveEventArgs args)
        {if(new AssemblyName(args.Name).Name!="0Harmony")return null;using(var s=typeof(Loader).Assembly.GetManifestResourceStream("BD2Fishing.Harmony.dll"))using(var b=new MemoryStream()){s.CopyTo(b);return Assembly.Load(b.ToArray());}}
    }
}
