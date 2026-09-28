using System;
using System.IO;
using System.Collections.Generic;
// Included only by regression/smoke test entry points. Uses real Windows pipes.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BD2.LocalIpc;
public static class TestTransport
{
    static readonly Dictionary<string, RuntimeChannel> channels = new(StringComparer.OrdinalIgnoreCase);
    public static void Start(string root, string names)
    {
        root=Path.GetFullPath(root); if(channels.ContainsKey(root))return;
        using var p=Process.GetCurrentProcess();
        channels[root]=PipeBroker.Register(root,"test",names,"");
        DesktopFiles.Configure(root,names); DesktopFiles.Connect(root,p.Id,p.StartTime.ToUniversalTime().Ticks).Open("test");
    }
    public static byte[] Read(string path)
    { if(!DesktopFiles.Read(path,out var bytes)||bytes==null)throw new IOException("Missing test IPC value: "+path); return bytes; }
    public static string Text(string path)=>Encoding.UTF8.GetString(Read(path));
    public static void Publish<T>(string path,T value)=>PublishBytes(path,JsonSerializer.SerializeToUtf8Bytes(value));
    public static void PublishBytes(string path,byte[] value)
    {
        path=Path.GetFullPath(path);
        foreach(var pair in channels)if(path.StartsWith(pair.Key+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        {pair.Value.Write(path.Substring(pair.Key.Length+1).Replace('\\','~').Replace('/','~'),value);return;}
        throw new IOException("Missing test IPC route: "+path);
    }
}
