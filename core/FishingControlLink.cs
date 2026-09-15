using System.Text.Json;
namespace BD2Fishing;
public static class FishingJson
{
 public static T? Read<T>(string path) where T:class
 {try{using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return JsonSerializer.Deserialize<T>(s);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){return null;}}
 public static void Write<T>(string path,T value)
 {var dir=Path.GetDirectoryName(path)!;Directory.CreateDirectory(dir);var tmp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllText(tmp,JsonSerializer.Serialize(value));File.Move(tmp,path,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
}
public sealed class FishingSettings
{
 public int NextCastMilliseconds {get;set;}=1000;
 public double CastGauge {get;set;}=.9;
 public bool PreferWeak {get;set;}=true;
 public bool AutoSell {get;set;}=true;
 public bool AutoBait {get;set;}=true;
}
public sealed class FishingControlLink:IDisposable
{
 private readonly object sync=new();private readonly Timer timer;private readonly string root;
 private FishingControl command=new();private bool disposed;
 public string Error {get;private set;}="";
 public string OwnerId {get{lock(sync)return command.OwnerId;}}
 public bool Enabled {get{lock(sync)return command.Enabled;}}
 public FishingControlLink(string root){this.root=root;timer=new(_=>Pulse(),null,500,500);}
 public void Configure(FishingSettings s)
 {
  if(!FishingControl.ValidSettings(s.NextCastMilliseconds,s.CastGauge))throw new ArgumentException("下一竿间隔为 0–60000 毫秒，蓄力为 5–95%。");
  lock(sync){command.NextCastMilliseconds=s.NextCastMilliseconds;command.CastGauge=s.CastGauge;command.PreferWeak=s.PreferWeak;command.AutoSell=s.AutoSell;command.AutoBait=s.AutoBait;FishingJson.Write(Path.Combine(root,"settings.json"),s);if(command.Enabled)Write();}
 }
 public void Start(int pid){lock(sync){if(disposed)throw new ObjectDisposedException(nameof(FishingControlLink));command.OwnerId=Guid.NewGuid().ToString("N");command.ProcessId=pid;command.Enabled=true;try{Write();}catch{command.Enabled=false;throw;}}}
 public void Stop(){lock(sync){command.Enabled=false;Write();}}
 private void Write(){command.UntilUtcTicks=command.Enabled?DateTime.UtcNow.AddSeconds(10).Ticks:0;FishingJson.Write(Path.Combine(root,"control.json"),command);Error="";}
 private void Pulse(){lock(sync){if(disposed || !command.Enabled)return;try{Write();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Error=e.Message;}}}
 public void Dispose(){lock(sync){if(disposed)return;disposed=true;timer.Dispose();try{Stop();}catch(Exception e){Error=e.Message;}}}
 public static bool Fresh(FishingSnapshot? s,DateTime now)=>s!=null && s.Schema==1 && s.Runtime==FishingIdentity.RuntimeName && s.ProcessId>0 && s.CapturedUtcTicks<=now.AddSeconds(2).Ticks && s.CapturedUtcTicks>=now.AddSeconds(-3).Ticks;
}
