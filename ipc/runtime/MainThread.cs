using System;
using System.Reflection;

namespace BD2.LocalIpc
{
    // Reflection avoids coupling the common lifecycle to a particular Unity build.
    // The subscribed delegate is removed once the operation is complete.
    public sealed class MainThread : IDisposable
    {
        readonly Func<bool> tick; readonly Action<Exception> failed;
        EventInfo frame; Delegate handler;
        public MainThread(Func<bool> tick, Action<Exception> failed = null) { this.tick = tick; this.failed=failed; }
        public static void Drain(Action unload, Action<string,string> status)
        {
            var began=DateTime.UtcNow;
            new MainThread(()=>{try{unload();return false;}catch(Exception e){
                if(DateTime.UtcNow-began>TimeSpan.FromSeconds(30)){status("error",e.GetBaseException().Message);return false;}
                status("handoff",e.GetBaseException().Message);return true;
            }},e=>status("error",e.GetBaseException().Message)).Schedule();
        }
        public void Schedule()
        {
            if (handler != null) return;
            Type canvas = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { canvas = assembly.GetType("UnityEngine.Canvas", false); if (canvas != null) break; }
            if (canvas == null) throw new InvalidOperationException("Waiting for Unity UI to load; reconnect once the game UI is visible.");
            frame = canvas.GetEvent("willRenderCanvases", BindingFlags.Static | BindingFlags.Public);
            if (frame == null) throw new MissingMemberException("UnityEngine.Canvas.willRenderCanvases");
            handler = Delegate.CreateDelegate(frame.EventHandlerType, this, GetType().GetMethod("Frame", BindingFlags.Instance | BindingFlags.NonPublic));
            frame.AddEventHandler(null, handler);
        }
        void Frame() { try { if (!tick()) Dispose(); } catch(Exception error) { Dispose(); if(failed!=null){try{failed(error);}catch{}} else System.Diagnostics.Trace.TraceError(error.ToString()); } }
        public void Dispose() { if (handler == null) return; var previous = handler; handler = null; frame.RemoveEventHandler(null, previous); }
    }
}
