using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using BD2.LocalIpc;
namespace BD2Fishing.Desktop;
public partial class FishingWindow
{
 public async Task ConnectionSmokeAsync(string output)
 {
  timer.Stop();ShowActivated=false;ShowInTaskbar=false;Left=-10000;Show();
  var checks=new List<string>();void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
  using var process=Process.GetCurrentProcess();
  var start=process.StartTime.ToUniversalTime().Ticks;
  DesktopFiles.Connect(root,process.Id,start);
  int beats=0;double previous=0,maxGap=0;var clock=Stopwatch.StartNew();
  var heartbeat=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(15)};
  heartbeat.Tick+=(_,_)=>{var now=clock.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-previous);previous=now;beats++;};heartbeat.Start();
  try
  {
   var first=RefreshAsync();
   await Task.Delay(50);
   Check(!first.IsCompleted,"missing pipe is still waiting in background");
   await RefreshAsync();Check(!first.IsCompleted,"overlapping refresh is skipped");
   await first;Check(beats>20&&maxGap<700,"dispatcher stays responsive during cold connection timeout");
   Check(File.ReadAllText(FishingDiagnostics.PathFor(root)).Contains("TimeoutException"),"cold connection timeout is logged before hook starts");
   connecting=true;var skipped=RefreshAsync();Check(skipped.IsCompleted,"no snapshot polling during connection");connecting=false;
   using(var cancelled=new CancellationTokenSource())
   {
    cancelled.Cancel();bool rejected=false;
    try{await Task.Run(()=>new FishingConnection(root).Connect(cancellationToken:cancelled.Token));}catch(OperationCanceledException){rejected=true;}
    Check(rejected,"cancelled connection never reaches process discovery or injection");
    var log=File.ReadAllText(FishingDiagnostics.PathFor(root));Check(log.Contains("connect.requested")&&log.Contains("connect.requested.failed"),"early connection stages and failure are recorded");
   }
   // This peer belongs to this test process. No game process or game command is involved.
   var accepted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   var commands=new List<FishingControl>();using var cancelPeer=new CancellationTokenSource();
   var peer=Task.Run(async()=>
   {
    try
    {
     while(!cancelPeer.IsCancellationRequested)
     {
      using var server=new NamedPipeServerStream(Wire.Endpoint(process.Id,start),PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
      await server.WaitForConnectionAsync(cancelPeer.Token);
      using var reader=new BinaryReader(new MemoryStream(Wire.ReadFrame(server)));
      reader.ReadInt32();var verb=reader.ReadString();reader.ReadString();reader.ReadString();reader.ReadString();reader.ReadString();var bytes=Wire.ReadBytes(reader);
      if(verb!="write")throw new Exception("Unexpected test request: "+verb);
      commands.Add(JsonSerializer.Deserialize<FishingControl>(bytes)!);
      if(commands.Count==1){accepted.SetResult();await release.Task.WaitAsync(cancelPeer.Token);}
      Wire.WriteFrame(server,Wire.Encode(w=>{w.Write("ok");w.Write("");w.Write("");Wire.Bytes(w,Array.Empty<byte>());}));
     }
    }catch(OperationCanceledException){}
   });
   var startControl=Task.Run(()=>link.Start(123));await accepted.Task.WaitAsync(TimeSpan.FromSeconds(3));
   var stopWatch=Stopwatch.StartNew();var stopped=StopAsync();
   Check(!link.Enabled&&stopWatch.ElapsedMilliseconds<150,"stop immediately revokes local state while write is blocked");
   int before=beats;await Task.Delay(200);Check(beats>before+3,"dispatcher remains responsive while stop awaits acknowledgement");
   release.SetResult();await Task.WhenAll(startControl,stopped).WaitAsync(TimeSpan.FromSeconds(4));
   Check(commands.Count>=2&&commands[0].Enabled&&commands.Skip(1).All(c=>!c.Enabled&&c.UntilUtcTicks==0),"in-flight start cannot leave a renewed lease after stop");
   cancelPeer.Cancel();await peer;
   var closeWatch=Stopwatch.StartNew();Close();await shutdownTask;
   Check(closeWatch.ElapsedMilliseconds<2700,"close finishes within deadline without pipe server");
   Check(!link.Enabled,"closed window cannot renew control");
   FishingJson.Write(Path.Combine(output,"connection-smoke.json"),new{status="pass",gameRequests=0,injection=false,dispatcherMaximumGapMs=maxGap,dispatcherTicks=beats,assertions=checks});
  }
  finally{heartbeat.Stop();}
 }
}
