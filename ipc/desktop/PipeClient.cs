using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace BD2.LocalIpc
{
    public sealed class PipeClient
    {
        readonly int pid; readonly long start; readonly string channel, dataRoot;
        readonly object sync = new object();
        string generation = "", ticket = "";
        public bool HasLease { get { lock(sync) return ticket.Length > 0; } }
        public PipeClient(string root, int pid, long start) { dataRoot=root; channel = Wire.Channel(root); this.pid = pid; this.start = start; }
        sealed class Response { internal string Status, Generation, Ticket; internal byte[] Value; }
        Response Call(string verb, string name, byte[] value)
        {
            lock (sync)
            {
                try
                {
                    using (var process = System.Diagnostics.Process.GetProcessById(pid))
                        if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != start)
                            throw new IOException("Game session ended; return to the host to connect again.");
                }
                catch (ArgumentException error) { throw new IOException("Game process is no longer running.", error); }
                catch (System.ComponentModel.Win32Exception error) { throw new IOException("Game session cannot be accessed.", error); }

                using (var pipe = new NamedPipeClientStream(".", Wire.Endpoint(pid, start), PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    pipe.Connect(Wire.TimeoutMilliseconds); Wire.VerifyServer(pipe, pid, start);
                    using (var deadline = new Timer(_ => { try { pipe.Dispose(); } catch { } }, null, Wire.TimeoutMilliseconds, Timeout.Infinite))
                    {
                        Wire.WriteFrame(pipe, Wire.Encode(w => { w.Write(Wire.Version); w.Write(verb); w.Write(channel); w.Write(generation); w.Write(ticket); w.Write(name); Wire.Bytes(w, value); }));
                        using (var r = new BinaryReader(new MemoryStream(Wire.ReadFrame(pipe))))
                        {
                            var response = new Response { Status = r.ReadString(), Generation = r.ReadString(), Ticket = r.ReadString(), Value = Wire.ReadBytes(r) }; Wire.End(r);
                            if (response.Status == "revoked") { ticket = ""; throw new LeaseRevokedException(); }
                            if (response.Status != "ok" && response.Status != "empty" && response.Status != "missing") throw new IOException("IPC: " + response.Status);
                            return response;
                        }
                    }
                }
            }
        }
        public string Fingerprint() { var r = Call("info", "", new byte[0]); return r.Status == "missing" ? null : Encoding.UTF8.GetString(r.Value); }
        public void Open(string fingerprint)
        {
            lock (sync) { var r = Call("open", fingerprint, new byte[0]); if (r.Status != "ok") throw new IOException("Component is not ready"); generation = r.Generation; ticket = r.Ticket; }
        }
        public string[] List(string prefix){var reply=Call("list",prefix,new byte[0]);if(reply.Status!="ok")return new string[0];using(var r=new BinaryReader(new MemoryStream(reply.Value))){int n=r.ReadInt32();if(n<0||n>10000)throw new IOException("Invalid IPC listing");var result=new string[n];for(int i=0;i<n;i++)result[i]=r.ReadString();Wire.End(r);return result;}}
        public byte[] Read(string name) { var r = Call("read", name, new byte[0]); return r.Status == "ok" ? r.Value : null; }
        public void Write(string name, byte[] value, bool createOnly = false) { var guard=AppDomain.CurrentDomain.GetData("BD2Daily.HostedWriteGuard") as Action<string,string,byte[]>; if(guard!=null)guard(dataRoot,name,value); if (Call(createOnly ? "create" : "write", name, value).Status != "ok") throw new IOException("Component is not ready"); }
        public bool DeleteIf(string name,byte[] expected){return Call("delete-if",name,expected).Status=="ok";}
        public void Delete(string name) { if (Call("delete", name, new byte[0]).Status != "ok") throw new IOException("Component is not ready"); }
    }
}
