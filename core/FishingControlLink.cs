using System.Text.Json;
namespace BD2Fishing;
public static class FishingJson
{
 public static T? Read<T>(string path) where T:class
 {try{if(BD2.LocalIpc.DesktopFiles.Read(path,out var live))return live==null?null:JsonSerializer.Deserialize<T>(live);using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return JsonSerializer.Deserialize<T>(s);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or TimeoutException or ObjectDisposedException){if(BD2.LocalIpc.DesktopFiles.Handles(path))FishingDiagnostics.Throttled(Path.GetDirectoryName(path)!,"read."+Path.GetFileName(path),e);return null;}}
 public static void Write<T>(string path,T value)
 {if(BD2.LocalIpc.DesktopFiles.Write(path,JsonSerializer.SerializeToUtf8Bytes(value)))return;var dir=Path.GetDirectoryName(path)!;Directory.CreateDirectory(dir);var tmp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllText(tmp,JsonSerializer.Serialize(value));
 var elapsed=System.Diagnostics.Stopwatch.StartNew();
 while(true){
  try{File.Move(tmp,path,true);break;}
  catch(Exception error)when(error is IOException or UnauthorizedAccessException){
   int code=error.HResult&0xffff;
   // Only retry the atomic local-file replacement, never a pipe write or game request.
   if(code is not (5 or 32 or 33) || elapsed.ElapsedMilliseconds>=300 || (File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReadOnly)!=0))throw;
   Thread.Sleep(10);
  }
 }}finally{if(File.Exists(tmp))File.Delete(tmp);}}
}
public sealed class FishingSettings
{
 public int NextCastMilliseconds {get;set;}=1000;
 public double CastGauge {get;set;}=.9;
 public bool PreferWeak {get;set;}=true;
 public bool AutoSell {get;set;}=true;
 public bool AutoApproach {get;set;}=true;
 // One-way migration from 0.3.2; new writes only use independent retention rules.
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
 public bool? KeepLockedOnly {get;set;}
 private FishingRetentionOptions? retention;
 public FishingRetentionOptions Retention {get=>retention??=new(){KeepLegendary=KeepLockedOnly!=true};set=>retention=value;}
 public bool AutoBait {get;set;}=true;
 public bool AutoMapRenewal {get;set;}=true;
}
public sealed class FishingControlLink:IDisposable
{
 private readonly object sync=new(),sendSync=new();private readonly Timer timer;private readonly string root;
 private FishingControl command=new();private bool disposed;private long revision,stopVersion;private string error="";
 public string Error {get{lock(sync)return error;}}
 public string OwnerId {get{lock(sync)return command.OwnerId;}}
 public bool Enabled {get{lock(sync)return command.Enabled;}}
 public long StopVersion {get{lock(sync)return stopVersion;}}
 public FishingControlLink(string root){this.root=root;BD2.LocalIpc.DesktopFiles.Configure(root,FishingIdentity.LiveEntries);timer=new(_=>Pulse(),null,500,500);}
 public void Configure(FishingSettings s)
 {
  if(!FishingControl.ValidSettings(s.NextCastMilliseconds,s.CastGauge))throw new ArgumentException("下一竿间隔为 0–60000 毫秒，蓄力为 5–95%。");
  var retention=(s.Retention??new()).Clone();
  lock(sendSync)
  {
   lock(sync)if(disposed)throw new ObjectDisposedException(nameof(FishingControlLink));
   FishingJson.Write(Path.Combine(root,"settings.json"),s);
   bool publish;lock(sync){command.NextCastMilliseconds=s.NextCastMilliseconds;command.CastGauge=s.CastGauge;command.PreferWeak=s.PreferWeak;command.AutoSell=s.AutoSell;command.Retention=retention;command.AutoApproach=s.AutoApproach;command.AutoBait=s.AutoBait;command.AutoMapRenewal=s.AutoMapRenewal;revision++;publish=command.Enabled;}
   if(publish)PublishLocked();
  }
 }
 public void Start(int pid,long? expectedStopVersion=null)
 {
  lock(sync){if(disposed)throw new ObjectDisposedException(nameof(FishingControlLink));if(expectedStopVersion.HasValue&&expectedStopVersion!=stopVersion)throw new OperationCanceledException();command.OwnerId=Guid.NewGuid().ToString("N");command.ProcessId=pid;command.Enabled=true;revision++;}
  try{Publish();}catch{RequestStop();throw;}
 }
 // Local state can always be revoked without waiting for a pipe or a heartbeat.
 public void RequestStop(){lock(sync){command.Enabled=false;stopVersion++;revision++;}}
 public void Stop(){RequestStop();Publish();}
 private void Publish(){lock(sendSync)PublishLocked();}
 private void PublishLocked()
 {
  while(true)
  {
   FishingControl value;long sentRevision;
   lock(sync){sentRevision=revision;value=JsonSerializer.Deserialize<FishingControl>(JsonSerializer.Serialize(command))!;value.UntilUtcTicks=value.Enabled?DateTime.UtcNow.AddSeconds(10).Ticks:0;}
   FishingJson.Write(Path.Combine(root,"control.json"),value);
   lock(sync){error="";if(sentRevision==revision)return;}
   // A stop/settings change arrived during the write: immediately publish current state,
   // never let an in-flight renewal be the final command after stopping.
  }
 }
 private void Pulse()
 {
  if(!Monitor.TryEnter(sendSync))return;
  try{lock(sync)if(disposed||!command.Enabled)return;PublishLocked();}
  catch(Exception e){lock(sync){error=e.Message;if(e is BD2.LocalIpc.LeaseRevokedException){command.Enabled=false;revision++;}}FishingDiagnostics.Throttled(root,"heartbeat.failed",e);}
  finally{Monitor.Exit(sendSync);}
 }
 public void Dispose(){lock(sync){if(disposed)return;disposed=true;command.Enabled=false;stopVersion++;revision++;}timer.Dispose();try{Publish();}catch(Exception e){lock(sync)error=e.Message;FishingDiagnostics.Throttled(root,"close.stop",e);}}
 public static bool Fresh(FishingSnapshot? s,DateTime now)=>s!=null && s.Schema==1 && s.Runtime==FishingIdentity.RuntimeName && s.ProcessId>0 && s.CapturedUtcTicks<=now.AddSeconds(2).Ticks && s.CapturedUtcTicks>=now.AddSeconds(-3).Ticks;
}

