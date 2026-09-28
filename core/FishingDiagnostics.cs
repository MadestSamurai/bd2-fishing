using System.Diagnostics;
using System.Text.Json;
namespace BD2Fishing;

public static class FishingDiagnostics
{
 private static readonly object sync=new();
 private static readonly Dictionary<string,long> recent=new();
 public static string PathFor(string root)=>Path.Combine(root,"connection.log");
 public static void Write(string root,string stage,string message="",Exception? error=null)
 {
  var line=JsonSerializer.Serialize(new{atUtc=DateTime.UtcNow,version=typeof(FishingDiagnostics).Assembly.GetName().Version?.ToString(),stage,message,error=error?.ToString()})+Environment.NewLine;
  lock(sync){try{Append(PathFor(root),line);}catch{try{Append(PathFor(Path.Combine(Path.GetTempPath(),"BD2Fishing")),line);}catch{}}}
 }
 private static void Append(string path,string line)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  if(File.Exists(path)&&new FileInfo(path).Length>2*1024*1024)File.Move(path,path+".previous",true);
  File.AppendAllText(path,line);
 }
 public static void Throttled(string root,string stage,Exception error)
 {
  lock(sync){var key=root+"|"+stage;var now=Environment.TickCount64;if(recent.TryGetValue(key,out var last)&&now-last<15000)return;recent[key]=now;}
  Write(root,stage,error:error);
 }
}

public sealed class FishingConnectionTrace:IDisposable
{
 private readonly string root,id=Guid.NewGuid().ToString("N");private readonly Stopwatch clock=Stopwatch.StartNew();
 private readonly object sync=new();private readonly Timer timer;private readonly Action<string>? progress;
 private string stage="connect.requested",caption="正在连接钓鱼组件…";private bool ended;private double stageAt;
 public FishingConnectionTrace(string root,Action<string>? progress=null)
 {
  this.root=root;this.progress=progress;Stage(stage,caption);
  timer=new(_=>Waiting(),null,5000,5000);
 }
 public void Stage(string name,string? message=null)
 {
  lock(sync){if(ended)return;stage=name;stageAt=clock.Elapsed.TotalSeconds;if(message!=null)caption=message;FishingDiagnostics.Write(root,stage,$"attempt={id}; elapsed={clock.Elapsed.TotalSeconds:F1}s");}
  if(message!=null)Report(message);
 }
 private void Report(string message){try{progress?.Invoke(message);}catch{}}
 private void Waiting()
 {
  string message;lock(sync){if(ended)return;var seconds=(int)(clock.Elapsed.TotalSeconds-stageAt);if(seconds<5)return;FishingDiagnostics.Write(root,stage+".waiting",$"attempt={id}; stageSeconds={seconds}; totalSeconds={clock.Elapsed.TotalSeconds:F1}");message=caption+"\n"+$"连接仍在进行，已等待 {seconds} 秒；可打开诊断目录查看 connection.log。";}
  Report(message);
 }
 public void Fail(Exception error)=>FishingDiagnostics.Write(root,stage+".failed",$"attempt={id}; elapsed={clock.Elapsed.TotalSeconds:F1}s",error);
 public void Dispose(){lock(sync)ended=true;timer.Dispose();}
}
