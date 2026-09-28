using System;
using System.IO;
using System.Collections.Generic;

namespace BD2.LocalIpc
{
    public static class RuntimeFiles
    {
        static string root;
        static HashSet<string> names;
        static RuntimeChannel channel;
        public static void Start(string dataRoot, string fingerprint, string liveNames)
        {
            root = Path.GetFullPath(dataRoot).TrimEnd('\\', '/'); names = new HashSet<string>(liveNames.Split('|'), StringComparer.OrdinalIgnoreCase);
            channel = PipeBroker.Register(root, fingerprint, liveNames, "", false);
        }
        static string Name(string path)
        {
            if (root == null) return null;
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            var name = path.Substring(root.Length + 1).Replace('\\', '~').Replace('/', '~');
            if(names.Contains(name))return name;
            foreach(var candidate in names)if(candidate.EndsWith("*",StringComparison.Ordinal)&&name.StartsWith(candidate.Substring(0,candidate.Length-1),StringComparison.OrdinalIgnoreCase))return name;
            return null;
        }
        public static bool Write(string path, byte[] bytes)
        { var name = Name(path); if (name == null) return false; channel.Write(name, bytes); return true; }
        public static byte[] Read(string path)
        { var name = Name(path); if (name == null || channel == null) return null; return channel.Read(name); }
        public static void Activate(){if(channel!=null)channel.Activate();}
        public static void Revoke() { if (channel != null) channel.Revoke(); }
        public static bool TryClaim(string id,long expires)
        {
            lock(AppDomain.CurrentDomain){const string key="BD2.LocalIpc.Claims.v1";
                var claims=AppDomain.CurrentDomain.GetData(key) as Dictionary<string,long>;
                if(claims==null){claims=new Dictionary<string,long>();AppDomain.CurrentDomain.SetData(key,claims);}
                var now=DateTime.UtcNow.Ticks;foreach(var old in new List<string>(claims.Keys))if(claims[old]<now)claims.Remove(old);
                var name=Wire.Channel(root)+":"+id;if(claims.ContainsKey(name))return false;
                if(claims.Count>=10000)throw new IOException("Too many pending command identities");
                claims[name]=Math.Max(expires,now+TimeSpan.FromMinutes(10).Ticks);return true;
            }
        }
        public static bool Handles(string path) { return Name(path) != null; }
        public static byte[] Take(string path) { var name=Name(path);return name==null||channel==null?null:channel.Take(name); }
    }
}
