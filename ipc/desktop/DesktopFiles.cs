using System;
using System.IO;
using System.Collections.Generic;

namespace BD2.LocalIpc
{
    // Transitional adapter for existing serializers. Only explicitly listed live entries
    // use the pipe; persisted settings/evidence never silently migrate into volatile memory.
    public static class DesktopFiles
    {
        sealed class Route { internal string Root; internal HashSet<string> Names; internal PipeClient Client; }
        static readonly object sync = new object();
        static readonly Dictionary<string, Route> routes = new Dictionary<string, Route>(StringComparer.OrdinalIgnoreCase);
        public static void Configure(string root, string names)
        {
            root = Path.GetFullPath(root).TrimEnd('\\', '/');
            lock (sync) { if (!routes.ContainsKey(root)) routes[root] = new Route { Root = root, Names = new HashSet<string>(names.Split('|'), StringComparer.OrdinalIgnoreCase) }; }
        }
        public static PipeClient Connect(string root, int pid, long start)
        {
            root = Path.GetFullPath(root).TrimEnd('\\', '/');
            lock (sync) { Route r; if (!routes.TryGetValue(root, out r)) throw new InvalidOperationException("IPC route must be configured first"); r.Client = new PipeClient(root, pid, start); return r.Client; }
        }
        public static bool IsConnected(string root){lock(sync){Route r;return routes.TryGetValue(Path.GetFullPath(root).TrimEnd('\\','/'),out r)&&r.Client!=null;}}
        public static bool HasLease(string root)
        { lock(sync) { Route r; return routes.TryGetValue(Path.GetFullPath(root).TrimEnd('\\','/'),out r) && r.Client != null && r.Client.HasLease; } }
        static Route Find(string path, out string name)
        {
            path = Path.GetFullPath(path); name = "";
            lock (sync) foreach (var r in routes.Values)
                if (path.StartsWith(r.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = path.Substring(r.Root.Length + 1).Replace('\\', '~').Replace('/', '~');
                    if (r.Names.Contains(candidate)) { name = candidate; return r; }
                    foreach(var pattern in r.Names)if(pattern.EndsWith("*",StringComparison.Ordinal)&&candidate.StartsWith(pattern.Substring(0,pattern.Length-1),StringComparison.OrdinalIgnoreCase)){name=candidate;return r;}
                }
            return null;
        }
        public static bool Handles(string path){string name;return Find(path,out name)!=null;}
        public static bool Read(string path, out byte[] value)
        { string name; var r = Find(path, out name); value = r == null || r.Client == null ? null : r.Client.Read(name); return r != null; }
        public static bool Write(string path, byte[] value, bool createOnly=false)
        { string name; var r = Find(path, out name); if (r == null) return false; if (r.Client == null) throw new IOException("Connect to the game before sending a command"); r.Client.Write(name, value,createOnly); return true; }
        public static bool Exists(string path)
        {byte[] value;return Read(path,out value)?value!=null:File.Exists(path);}
        public static void Remove(string path){if(!Delete(path))File.Delete(path);}
        public static bool DeleteIf(string path,byte[] expected){string name;var r=Find(path,out name);return r!=null&&r.Client!=null&&r.Client.DeleteIf(name,expected);}
        public static bool Delete(string path)
        { string name;var r=Find(path,out name);if(r==null)return false;if(r.Client!=null)r.Client.Delete(name);return true; }
    }
}
