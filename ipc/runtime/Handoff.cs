using System;
using System.Collections.Generic;

namespace BD2.LocalIpc
{
    // All callbacks run from Tick on the game's main thread. No Unity/reflection calls on IPC workers.
    public sealed class Handoff
    {
        const string Key = "BD2.LocalIpc.Handoff.v1";
        readonly string module, family, group;
        readonly bool passive;
        readonly Action start, pause, stop;
        readonly Func<string> busy;
        readonly Action<string, string> status;
        List<Dictionary<string, object>> retiring;
        DateTime requested, idle;
        bool pending, stopped, starting;
        public bool Pending { get { return pending; } }
        public bool IsActive { get { foreach(var old in Registry) if((string)old["module"]==module&&(bool)old["active"]&&!Paused(old))return true;return false; } }
        static bool Paused(Dictionary<string,object> entry){return entry.ContainsKey("paused")&&(bool)entry["paused"];}
        public void Fail(Exception error){pending=false;ReleasePending();status("error",error.GetBaseException().Message);}
        public Handoff(string module, string family, string group, bool passive, Action start, Action pause, Func<string> busy, Action stop, Action<string, string> status)
        { this.module = module; this.family = family; this.group = group; this.passive = passive; this.start = start; this.pause = pause; this.busy = busy; this.stop = stop; this.status = status; }
        static List<Dictionary<string, object>> Registry
        {
            get
            {
                lock (AppDomain.CurrentDomain)
                {
                    var value = AppDomain.CurrentDomain.GetData(Key) as List<Dictionary<string, object>>;
                    if (value == null) { value = new List<Dictionary<string, object>>(); AppDomain.CurrentDomain.SetData(Key, value); }
                    return value;
                }
            }
        }
        // Standalone tools are exclusive. Only explicitly coordinated suites may coexist.
        public static bool HasActive(string group,string family){foreach(var entry in Registry)if((string)entry["group"]==group&&(string)entry["family"]==family&&(bool)entry["active"]&&!Paused(entry))return true;return false;}
        static bool Cooperates(string a,string b){return a==b&&(a=="daily"||a=="workbench");}
        public void Request(DateTime now)
        {
            if (pending) return;
            requested = now; idle = default(DateTime); retiring = null; pending = true; stopped = false; starting=false;
            status("handoff", "Waiting for main-thread component handoff");
        }
        public void Tick(DateTime now)
        {
            if (!pending) return;
            try
            {
                if (retiring == null)
                {
                    foreach (var old in Registry) if ((string)old["module"] == module && (bool)old["active"] && !Paused(old)) { pending = false; status("active", ""); return; }
                    var owner = AppDomain.CurrentDomain.GetData(Key + ".pending") as string;
                    if (owner != null && owner != module)
                    {
                        if (now - requested > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("Another component handoff is still pending; reconnect after it finishes.");
                        return;
                    }
                    AppDomain.CurrentDomain.SetData(Key + ".pending", module);
                    retiring = new List<Dictionary<string, object>>();
                    foreach (var old in Registry)
                        if ((bool)old["active"] && ((string)old["family"] == family || (!passive && !(bool)old["passive"] && !Cooperates((string)old["group"],group)))) retiring.Add(old);
                    foreach (var old in retiring) { old["paused"]=true; ((Action)old["pause"])(); }
                }
                string reason = "";
                foreach (var old in retiring)
                {
                    ((Action)old["pause"])();
                    var current = ((Func<string>)old["busy"])();
                    if (!string.IsNullOrEmpty(current)) reason = (string)old["family"] + ": " + current;
                }
                if (reason.Length > 0)
                {
                    idle = default(DateTime);
                    if (now - requested > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("handoff-busy: " + reason + ". Finish the pending action, then reconnect; the game does not need to restart.");
                    status("handoff", reason); return;
                }
                if (idle == default(DateTime)) { idle = now; return; }
                if (now - idle < TimeSpan.FromMilliseconds(200)) return;
                if (!stopped)
                {
                    foreach (var old in retiring) { ((Action)old["stop"])(); old["active"] = false; }
                    stopped = true; return; // A separate frame permits Unity's deferred destruction to finish.
                }
                starting=true; start(); starting=false;
                Registry.RemoveAll(x => !(bool)x["active"]);
                Registry.Add(new Dictionary<string, object> { { "module", module }, { "family", family }, { "group", group }, { "passive", passive }, { "active", true }, { "pause", pause }, { "busy", busy }, { "stop", stop } });
                pending = false; ReleasePending(); status("active", "");
            }
            catch (Exception error)
            {
                pending = false; ReleasePending();
                if(starting){try { pause(); stop(); } catch { }}
                status("error", error.GetBaseException().Message);
            }
        }
        public void Unload()
        {
            pending = false; ReleasePending(); foreach(var old in Registry)if((string)old["module"]==module)old["paused"]=true; pause();
            string reason = busy();
            if (!string.IsNullOrEmpty(reason)) throw new InvalidOperationException("handoff-busy: " + reason);
            stop(); foreach (var old in Registry) if ((string)old["module"] == module) old["active"] = false;
            status("inactive", "");
        }
        void ReleasePending() { if ((AppDomain.CurrentDomain.GetData(Key + ".pending") as string) == module) AppDomain.CurrentDomain.SetData(Key + ".pending", null); }
    }
}
