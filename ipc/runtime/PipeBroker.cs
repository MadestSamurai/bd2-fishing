using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BD2.LocalIpc
{
    // In-process mailbox. Unity calls remain exclusively in each component's frame callback.
    // The delegate is stored in AppDomain data, so all dynamically loaded versions share one server.
    public static class PipeBroker
    {
        const string Key = "BD2.LocalIpc.Broker.v1";
        const int Revision = 6; // Increment when changing the in-process broker implementation.
        const int MaximumEntries = 2048, MaximumBytes = 64 * 1024 * 1024;
        sealed class Entry { internal byte[] Value; internal DateTime At; }
        sealed class ChannelState
        {
            internal string Generation = Wire.Nonce(), Ticket = "", Fingerprint;
            internal bool Revoked, Ready;
            internal HashSet<string> Names, Prefixes;
            internal Dictionary<string, Entry> Values = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            internal int Bytes;
            internal bool Allows(string name)
            {
                if (name.IndexOfAny(new[] {'/', '\\', ':'}) >= 0 || name.Length > 160) return false;
                if (Names.Contains(name)) return true;
                foreach (var prefix in Prefixes) if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            internal bool Writable(string name)
            {
                // This is a command mailbox, not a general remote filesystem. Snapshot/status
                // entries are runtime-owned. Read-only capture registers no input entries.
                var leaf = name.Substring(name.LastIndexOf('~') + 1);
                return Names.Contains(name) && (leaf == "control.json" || leaf == "config-control.json" || leaf == "ui-command.json" || leaf == "ui-observe" || leaf == "command.json" || leaf == "stop" || leaf == "pause" || leaf == "enabled-until.txt" || leaf == "execution-lease.json" || leaf == "run-command.json" || leaf == "layout-request.json" || leaf == "evidence-config.json" || leaf == "observation-request.json" || leaf == "catalog-request.json" || leaf == "legacy-observation" || leaf == "lease.json" || leaf == "startup-permit.json" || leaf == "guild-command.json" || leaf == "auto-defense-lease.json" || leaf == "settings-control.json" || leaf == "request.json" || leaf == "preview-request.json" || leaf == "mutation-request.json" || leaf == "native-save-capture-request.json" || leaf == "dice-lease.json" || leaf == "capture_settings.json" || leaf == "capture_settings.member.json" || leaf == "equipment_inventory.request" || leaf == "costume_inventory.request" || leaf == "monster_hunt_unified_inventory.request");
            }
        }
        sealed class Broker
        {
            readonly object sync = new object();
            readonly Dictionary<string, ChannelState> channels = new Dictionary<string, ChannelState>();
            readonly string endpoint;
            readonly object transportSync = new object();
            readonly HashSet<NamedPipeServerStream> serving = new HashSet<NamedPipeServerStream>();
            NamedPipeServerStream listening;
            Thread listener;
            volatile bool stopping;
            internal Broker(object saved = null)
            {
                using (var p = Process.GetCurrentProcess()) endpoint = Wire.Endpoint(p.Id, p.StartTime.ToUniversalTime().Ticks);
                // Create the first instance synchronously: do not report success if another process owns the name.
                if(saved!=null)Import(saved);
                listening = Create(endpoint, true);
                listener = new Thread(Listen) { IsBackground = true, Name = "BD2 local IPC" }; listener.Start();
            }
            void Listen()
            {
                while (!stopping)
                {
                    NamedPipeServerStream pipe=null;
                    try
                    {
                        lock(transportSync){if(stopping)return;if(listening==null)listening=Create(endpoint,false);pipe=listening;}
                        pipe.WaitForConnection();
                        lock(transportSync){if(stopping){pipe.Dispose();return;}listening=null;serving.Add(pipe);}
                        var connected=pipe; pipe=null;
                        ThreadPool.QueueUserWorkItem(_ => {try{Serve(connected);}finally{lock(transportSync)serving.Remove(connected);}});
                    }
                    catch(Exception){if(stopping)return;lock(transportSync){if(pipe!=null)pipe.Dispose();if(listening==pipe)listening=null;}Thread.Sleep(100);}
                }
            }
            internal void StopTransport()
            {
                stopping=true;
                lock(transportSync){if(listening!=null){listening.Dispose();listening=null;}foreach(var pipe in serving)try{pipe.Dispose();}catch{}}
                if(!listener.Join(2000))throw new IOException("Local communication is still stopping; reconnect after it finishes.");
            }
            internal object Export()
            {
                lock(sync){var saved=new Dictionary<string,object>();foreach(var pair in channels){var c=pair.Value;
                    var values=new Dictionary<string,object>();foreach(var entry in c.Values)values[entry.Key]=new object[]{entry.Value.Value,entry.Value.At.Ticks};
                    saved[pair.Key]=new object[]{c.Generation,c.Ticket,c.Fingerprint,c.Revoked,new List<string>(c.Names).ToArray(),new List<string>(c.Prefixes).ToArray(),values,c.Ready};
                }return saved;}
            }
            void Import(object saved)
            {
                foreach(var pair in (Dictionary<string,object>)saved){var data=(object[])pair.Value;
                    var c=new ChannelState{Generation=(string)data[0],Ticket=(string)data[1],Fingerprint=(string)data[2],Revoked=(bool)data[3],Ready=data.Length<8||(bool)data[7],Names=new HashSet<string>((string[])data[4],StringComparer.OrdinalIgnoreCase),Prefixes=new HashSet<string>((string[])data[5],StringComparer.OrdinalIgnoreCase)};
                    foreach(var entry in (Dictionary<string,object>)data[6]){var value=(object[])entry.Value;var bytes=(byte[])value[0];c.Values[entry.Key]=new Entry{Value=bytes,At=new DateTime((long)value[1],DateTimeKind.Utc)};c.Bytes+=bytes.Length;}
                    channels[pair.Key]=c;
                }
            }
            void Serve(NamedPipeServerStream pipe)
            {
                // A broken or stalled client cannot retain a worker/pipe indefinitely.
                using (pipe)
                using (var deadline = new Timer(_ => { try { pipe.Dispose(); } catch { } }, null, Wire.TimeoutMilliseconds, Timeout.Infinite))
                {
                    try { var request = Wire.ReadFrame(pipe); Wire.WriteFrame(pipe, Dispatch("request", "", request)); }
                    catch (Exception) { /* Per-connection failure; the listener and other clients remain usable. */ }
                }
            }
            internal byte[] Dispatch(string operation, string channel, byte[] input)
            {
                lock (sync)
                {
                    if (operation == "register")
                    {
                        using (var r = new BinaryReader(new MemoryStream(input)))
                        {
                            var c = new ChannelState { Fingerprint = r.ReadString(), Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
                            foreach (var n in r.ReadString().Split('|')) if (n.Length > 0) { if(n.EndsWith("*",StringComparison.Ordinal)) c.Prefixes.Add(n.Substring(0,n.Length-1)); else c.Names.Add(n); }
                            foreach (var n in r.ReadString().Split('|')) if (n.Length > 0) c.Prefixes.Add(n);
                            c.Ready=r.ReadBoolean();Wire.End(r); channels[channel] = c; return System.Text.Encoding.UTF8.GetBytes(c.Generation);
                        }
                    }
                    ChannelState state;
                    if(operation=="activate"){
                        if(channels.TryGetValue(channel,out state)&&System.Text.Encoding.UTF8.GetString(input)==state.Generation&&!state.Revoked)state.Ready=true;
                        return new byte[0];
                    }
                    if (operation == "revoke")
                    {
                        if (channels.TryGetValue(channel, out state) && System.Text.Encoding.UTF8.GetString(input) == state.Generation)
                        {
                            state.Revoked = true; state.Ticket = "";
                            foreach (var name in new List<string>(state.Values.Keys)) if (state.Writable(name)) Remove(state, name);
                        }
                        return new byte[0];
                    }
                    if(operation=="peek"){
                        if(!channels.TryGetValue(channel,out state))return null;
                        return Access(state,"read",System.Text.Encoding.UTF8.GetString(input),new byte[0]);
                    }
                    if (operation == "local")
                    {
                        using (var r = new BinaryReader(new MemoryStream(input)))
                        {
                            string generation = r.ReadString(), verb = r.ReadString(), name = r.ReadString(); byte[] value = Wire.ReadBytes(r); Wire.End(r);
                            if (!channels.TryGetValue(channel, out state) || state.Generation != generation) return null;
                            // Old engines may finish a callback after retirement. They cannot write into the new generation.
                            return Access(state, verb, name, value);
                        }
                    }
                    if (operation != "request") throw new InvalidDataException("Unknown local IPC operation");
                    try
                    {
                        using (var r = new BinaryReader(new MemoryStream(input)))
                        {
                            if (r.ReadInt32() != Wire.Version) return Reply("protocol", "", "", new byte[0]);
                            string verb = r.ReadString(); channel = r.ReadString();
                            string generation = r.ReadString(), ticket = r.ReadString(), name = r.ReadString(); byte[] value = Wire.ReadBytes(r); Wire.End(r);
                            if (!channels.TryGetValue(channel, out state)) return Reply("missing", "", "", new byte[0]);
                            if (verb == "info") return Reply(state.Revoked ? "revoked" : state.Ready ? "ok" : "waiting", state.Generation, "", System.Text.Encoding.UTF8.GetBytes(state.Fingerprint));
                            if (state.Revoked && verb != "read" && verb != "list") return Reply("revoked", state.Generation, "", new byte[0]);
                            if (verb == "open")
                            {
                                if(!state.Ready)return Reply("waiting",state.Generation,"",new byte[0]);
                                if (name != state.Fingerprint) return Reply("version", state.Generation, "", new byte[0]);
                                state.Ticket = Wire.Nonce();
                                // Explicit ownership change invalidates a previous window's commands/lease.
                                foreach (var key in new List<string>(state.Values.Keys)) if (state.Writable(key)&&key!="settings-control.json"&&key!="evidence-config.json"&&key!="capture_settings.json"&&key!="capture_settings.member.json") Remove(state, key);
                                return Reply("ok", state.Generation, state.Ticket, new byte[0]);
                            }
                            if (verb != "read" && verb != "list" && (generation != state.Generation || ticket.Length == 0 || ticket != state.Ticket))
                                return Reply("revoked", state.Generation, "", new byte[0]);
                            if ((verb == "read" || verb == "list") && ticket.Length > 0 && (state.Revoked || ticket != state.Ticket)) return Reply("revoked", state.Generation, "", new byte[0]);
                            if ((verb == "read" || verb == "list") && generation.Length > 0 && generation != state.Generation) return Reply("revoked", state.Generation, "", new byte[0]);
                            if (verb != "read" && verb != "list" && !state.Writable(name)) return Reply("read-only", state.Generation, "", new byte[0]);
                            var result = Access(state, verb, name, value);
                            return Reply(result == null ? "empty" : "ok", state.Generation, "", result ?? new byte[0]);
                        }
                    }
                    catch (InvalidDataException) { return Reply("invalid", "", "", new byte[0]); }
                    catch (EndOfStreamException) { return Reply("invalid", "", "", new byte[0]); }
                }
            }
            static byte[] Access(ChannelState c, string verb, string name, byte[] value)
            {
                if(verb=="list"){
                    if(name.IndexOfAny(new[]{'/','\\',':'})>=0)throw new InvalidDataException("Invalid entry prefix");
                    var result=new List<string>();foreach(var key in c.Values.Keys)if(key.StartsWith(name,StringComparison.OrdinalIgnoreCase))result.Add(key);
                    return Wire.Encode(w=>{w.Write(result.Count);foreach(var key in result)w.Write(key);});
                }
                if (!c.Allows(name)) throw new InvalidDataException("Unknown IPC channel entry: " + name);
                Entry entry;
                if (verb == "read") return c.Values.TryGetValue(name, out entry) ? entry.Value : null;
                if (verb == "take") { if(!c.Values.TryGetValue(name,out entry))return null; var taken=entry.Value;Remove(c,name);return taken; }
                if(verb=="delete-if"){if(!c.Values.TryGetValue(name,out entry))return null;if(entry.Value.Length!=value.Length)return null;for(int i=0;i<value.Length;i++)if(entry.Value[i]!=value[i])return null;Remove(c,name);return new byte[0];}
                if (verb == "delete") { Remove(c, name); return new byte[0]; }
                if (verb != "write" && verb != "create") throw new InvalidDataException("Unknown IPC verb");
                if (verb == "create" && c.Values.ContainsKey(name)) throw new InvalidDataException("IPC command is already pending");
                if (value.Length > MaximumBytes) throw new InvalidDataException("IPC value too large");
                // Evict old receipts only; never silently discard a pending command or current snapshot.
                foreach (var key in new List<string>(c.Values.Keys))
                    if (((key.StartsWith("receipt-",StringComparison.OrdinalIgnoreCase)||key.StartsWith("receipts~",StringComparison.OrdinalIgnoreCase))&&DateTime.UtcNow-c.Values[key].At>TimeSpan.FromMinutes(4))||(key.StartsWith("events~",StringComparison.OrdinalIgnoreCase)&&DateTime.UtcNow-c.Values[key].At>TimeSpan.FromMinutes(2)))Remove(c,key);
                int previous = c.Values.TryGetValue(name, out entry) ? entry.Value.Length : 0;
                if (c.Bytes - previous + value.Length > MaximumBytes || (!c.Values.ContainsKey(name) && c.Values.Count >= MaximumEntries)) throw new InvalidDataException("IPC mailbox capacity exceeded");
                c.Values[name] = new Entry { Value = value, At = DateTime.UtcNow }; c.Bytes += value.Length - previous; return new byte[0];
            }
            static void Remove(ChannelState c, string name) { Entry old; if (c.Values.TryGetValue(name, out old)) { c.Bytes -= old.Value.Length; c.Values.Remove(name); } }
        }
        internal static byte[] Reply(string status, string generation, string ticket, byte[] value)
        { return Wire.Encode(w => { w.Write(status); w.Write(generation); w.Write(ticket); Wire.Bytes(w, value); }); }
        static Func<string, string, byte[], byte[]> Dispatch
        {
            get
            {
                lock (AppDomain.CurrentDomain)
                {
                    var existing = AppDomain.CurrentDomain.GetData(Key) as Func<string, string, byte[], byte[]>;
                    var revision=AppDomain.CurrentDomain.GetData(Key+".revision");
                    if(existing!=null&&revision!=null&&(int)revision>=Revision)return existing;
                    object saved=null;
                    if(existing!=null){
                        var stop=AppDomain.CurrentDomain.GetData(Key+".stop") as Action;
                        var export=AppDomain.CurrentDomain.GetData(Key+".export") as Func<object>;
                        if(stop==null||export==null)throw new IOException("An older communication component has no hot-transfer interface. Stop that component before reconnecting.");
                        stop();saved=export();
                    }
                    var broker = new Broker(saved); Func<string, string, byte[], byte[]> next = broker.Dispatch;
                    AppDomain.CurrentDomain.SetData(Key, next);
                    AppDomain.CurrentDomain.SetData(Key+".revision",Revision);
                    AppDomain.CurrentDomain.SetData(Key+".stop",(Action)broker.StopTransport);
                    AppDomain.CurrentDomain.SetData(Key+".export",(Func<object>)broker.Export);
                    return next;
                }
            }
        }
        public static byte[] ReadCurrent(string root,string name){return Dispatch("peek",Wire.Channel(root),System.Text.Encoding.UTF8.GetBytes(name));}
        public static RuntimeChannel Register(string root, string fingerprint, string names, string prefixes, bool ready = true)
        {
            string key = Wire.Channel(root);
            var result = Dispatch("register", key, Wire.Encode(w => { w.Write(fingerprint); w.Write(names); w.Write(prefixes); w.Write(ready); }));
            return new RuntimeChannel(key, System.Text.Encoding.UTF8.GetString(result), (verb,id,bytes)=>Dispatch(verb,id,bytes));
        }
        [StructLayout(LayoutKind.Sequential)] struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, out uint size);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr pointer);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafePipeHandle CreateNamedPipe(string name, uint mode, uint pipeMode, uint instances, uint output, uint input, uint timeout, ref SecurityAttributes security);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int kind,IntPtr buffer,int length,out int required);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ConvertSidToStringSid(IntPtr sid,out IntPtr value);
        static string CurrentSid(){
            IntPtr token;if(!OpenProcessToken(GetCurrentProcess(),8,out token))throw new System.ComponentModel.Win32Exception();
            try{int size;GetTokenInformation(token,1,IntPtr.Zero,0,out size);if(size<=0||size>65536)throw new System.ComponentModel.Win32Exception();
                var buffer=Marshal.AllocHGlobal(size);try{if(!GetTokenInformation(token,1,buffer,size,out size))throw new System.ComponentModel.Win32Exception();
                    IntPtr text;if(!ConvertSidToStringSid(Marshal.ReadIntPtr(buffer),out text))throw new System.ComponentModel.Win32Exception();
                    try{return Marshal.PtrToStringUni(text);}finally{LocalFree(text);}
                }finally{Marshal.FreeHGlobal(buffer);}
            }finally{CloseHandle(token);}
        }
        static NamedPipeServerStream Create(string endpoint, bool first)
        {
            // Same account and SYSTEM only. Medium integrity permits a normal desktop to
            // communicate with its elevated game. Remote clients are rejected by Windows.
            var sid = CurrentSid(); IntPtr descriptor; uint size;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;" + sid + ")(A;;GA;;;SY)S:(ML;;NW;;;ME)", 1, out descriptor, out size)) throw new System.ComponentModel.Win32Exception();
            try
            {
                var security = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = descriptor };
                var handle = CreateNamedPipe("\\\\.\\pipe\\" + endpoint, 3u | 0x40000000u | (first ? 0x00080000u : 0u), 8u, 16, 65536, 65536, 0, ref security);
                if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
                try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); } catch { handle.Dispose(); throw; }
            }
            finally { LocalFree(descriptor); }
        }
    }
    public sealed class RuntimeChannel
    {
        readonly string channel, generation;
        readonly Func<string, string, byte[], byte[]> dispatch;
        internal RuntimeChannel(string channel, string generation, Func<string, string, byte[], byte[]> dispatch) { this.channel = channel; this.generation = generation; this.dispatch = dispatch; }
        byte[] Call(string verb, string name, byte[] value) { return dispatch("local", channel, Wire.Encode(w => { w.Write(generation); w.Write(verb); w.Write(name); Wire.Bytes(w, value); })); }
        public byte[] Read(string name) { return Call("read", name, new byte[0]); }
        public byte[] Take(string name) { return Call("take", name, new byte[0]); }
        public void Write(string name, byte[] value) { Call("write", name, value); }
        public void Delete(string name) { Call("delete", name, new byte[0]); }
        public void Activate(){dispatch("activate",channel,System.Text.Encoding.UTF8.GetBytes(generation));}
        public void Revoke() { dispatch("revoke", channel, System.Text.Encoding.UTF8.GetBytes(generation)); }
    }
}
